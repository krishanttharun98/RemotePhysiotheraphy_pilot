using UnityEngine;
using TMPro;

// Nose calibration prompt shown after Phase 2, before Phase 3 front/rear.
public class RWSCalibrationPanel : MonoBehaviour
{
    public SolidSphereFollower solidSphereFollower;
    public Transform headsetCamera;
    public float spawnDistance = 0.9f;
    public float spawnHeightOffset = 0f;
    public float fontSize = 0.085f;

    const string Prompt = "Keep your right controller on the nose and press A.";

    TextMeshPro _label;
    GameObject _panelRoot;
    bool _visible;

    void Start()
    {
        if (solidSphereFollower == null)
            solidSphereFollower = FindObjectOfType<SolidSphereFollower>();
        if (headsetCamera == null && Camera.main != null)
            headsetCamera = Camera.main.transform;

        _panelRoot = new GameObject("RWSCalibrationPanel");
        _panelRoot.transform.SetParent(transform, false);

        GameObject textGo = new GameObject("CalibrationText");
        textGo.transform.SetParent(_panelRoot.transform, false);
        _label = textGo.AddComponent<TextMeshPro>();
        _label.fontSize = fontSize;
        _label.alignment = TextAlignmentOptions.Center;
        _label.color = Color.white;
        _label.text = Prompt;
        _label.rectTransform.sizeDelta = new Vector2(1.05f, 0.2f);

        if (solidSphereFollower != null)
            solidSphereFollower.NoseCalibrationChanged += OnNoseCalibrationChanged;

        SetVisible(false);
    }

    void OnDestroy()
    {
        if (solidSphereFollower != null)
            solidSphereFollower.NoseCalibrationChanged -= OnNoseCalibrationChanged;
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

    void OnNoseCalibrationChanged(bool active)
    {
        if (active && _label != null) _label.text = Prompt;
        SetVisible(active);
    }

    void SetVisible(bool on)
    {
        _visible = on;
        if (_panelRoot != null) _panelRoot.SetActive(on);
    }
}
