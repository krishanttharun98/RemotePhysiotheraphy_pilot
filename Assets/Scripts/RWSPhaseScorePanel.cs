using UnityEngine;
using TMPro;

// Live RWS RSA score panel shown in front of the user during each task phase.
public class RWSPhaseScorePanel : MonoBehaviour
{
    [Header("References")]
    public SolidSphereFollower solidSphereFollower;
    public Transform headsetCamera;

    [Header("Layout")]
    public float spawnDistance = 0.85f;
    public float spawnHeightOffset = 0.05f;
    public float fontSize = 0.07f;

    TextMeshPro _label;
    GameObject _panelRoot;
    bool _visible;

    void Start()
    {
        if (solidSphereFollower == null)
            solidSphereFollower = FindObjectOfType<SolidSphereFollower>();
        if (headsetCamera == null && Camera.main != null)
            headsetCamera = Camera.main.transform;

        BuildPanel();

        if (solidSphereFollower != null)
        {
            solidSphereFollower.PhaseStarted += OnPhaseChanged;
            solidSphereFollower.PhaseCompleted += OnPhaseCompleted;
            solidSphereFollower.BubblePopped += OnBubblePopped;
            solidSphereFollower.TestEnded += OnTestEnded;
            solidSphereFollower.RestStateChanged += OnRestChanged;
            solidSphereFollower.TaskStartPromptChanged += OnPromptChanged;
            solidSphereFollower.NoseCalibrationChanged += OnNoseCalibrationChanged;
        }

        SetVisible(false);
    }

    void OnDestroy()
    {
        if (solidSphereFollower == null) return;
        solidSphereFollower.PhaseStarted -= OnPhaseChanged;
        solidSphereFollower.PhaseCompleted -= OnPhaseCompleted;
        solidSphereFollower.BubblePopped -= OnBubblePopped;
        solidSphereFollower.TestEnded -= OnTestEnded;
        solidSphereFollower.RestStateChanged -= OnRestChanged;
        solidSphereFollower.TaskStartPromptChanged -= OnPromptChanged;
        solidSphereFollower.NoseCalibrationChanged -= OnNoseCalibrationChanged;
    }

    void BuildPanel()
    {
        _panelRoot = new GameObject("RWSPhaseScorePanel");
        _panelRoot.transform.SetParent(transform, false);

        GameObject textGo = new GameObject("ScoreText");
        textGo.transform.SetParent(_panelRoot.transform, false);
        _label = textGo.AddComponent<TextMeshPro>();
        _label.fontSize = fontSize;
        _label.alignment = TextAlignmentOptions.MidlineLeft;
        _label.color = Color.white;
        _label.rectTransform.sizeDelta = new Vector2(1.4f, 0.7f);
    }

    void LateUpdate()
    {
        if (!_visible || _panelRoot == null || headsetCamera == null) return;

        Vector3 pos = headsetCamera.position + headsetCamera.forward * spawnDistance;
        pos.y += spawnHeightOffset;
        _panelRoot.transform.position = pos;

        Vector3 fwd = headsetCamera.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 0.0001f)
            _panelRoot.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
    }

    void OnPhaseChanged(int phase, bool rear) => RefreshScore();
    void OnPhaseCompleted(int phase, bool rear) => RefreshScore();
    void OnBubblePopped(Vector3 _, int __, float ___, int ____, int _____, int ______) => RefreshScore();

    void OnTestEnded() => SetVisible(false);

    void OnRestChanged(bool resting)
    {
        if (resting) SetVisible(false);
        else RefreshScore();
    }

    void OnPromptChanged(bool show)
    {
        if (show)
        {
            if (_label != null) _label.text = "Press Y on the left controller to start the RWS task.";
            SetVisible(true);
        }
        else if (!solidSphereFollower.IsNoseCalibrating)
        {
            RefreshScore();
        }
    }

    void OnNoseCalibrationChanged(bool active)
    {
        if (active) SetVisible(false);
        else RefreshScore();
    }

    void RefreshScore()
    {
        if (solidSphereFollower == null || _label == null) return;
        if (solidSphereFollower.IsAwaitingTaskStart || solidSphereFollower.IsNoseCalibrating ||
            solidSphereFollower.IsResting || !solidSphereFollower.IsTestRunning)
        {
            return;
        }

        int hist = solidSphereFollower.CurrentHistoryIndex;
        RWSScoreCalculator.PhaseScore score = RWSScoreCalculator.Calculate(solidSphereFollower, hist);
        string label = RWSScoreCalculator.PhaseLabel(solidSphereFollower.CurrentPhase, solidSphereFollower.IsPhase3Rear);
        _label.text = RWSScoreCalculator.FormatLivePanel(score, label);
        SetVisible(true);
    }

    void SetVisible(bool on)
    {
        _visible = on;
        if (_panelRoot != null) _panelRoot.SetActive(on);
    }
}
