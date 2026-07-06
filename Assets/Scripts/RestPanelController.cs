using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Shows a "rest" checkpoint panel between live-task phases.
// SolidSphereFollower pauses and fires RestStateChanged(true) when a phase ends;
// this panel appears, and the Resume button calls back to continue the next phase.
public class RestPanelController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the Solid Sphere Follower component from the RWS object in the Hierarchy.")]
    public SolidSphereFollower solidSphereFollower;
    [Tooltip("The root panel GameObject to show/hide. If null, this GameObject is used.")]
    public GameObject panelRoot;
    public Button resumeButton;
    public Text statusLabel;          // optional (legacy UI Text)
    public TMPro.TMP_Text statusLabelTMP; // optional (TextMeshPro)

    [Header("Placement")]
    [Tooltip("If set, the panel re-positions in front of this headset each time it appears.")]
    public Transform headsetCamera;
    public float spawnDistance = 1.0f;
    public float spawnHeightOffset = -0.1f;

    [Header("Fallback Input")]
    [Tooltip("Controller buttons that also resume while the rest panel is showing.")]
    public bool enableControllerFallback = true;

    private bool _resting;
    private Canvas _parentCanvas;
    private readonly List<GameObject> _hiddenCanvasSiblings = new List<GameObject>();

    void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;
        _parentCanvas = GetComponentInParent<Canvas>();

        if (GetComponent<RestPanelRayInteractionSetup>() == null)
            gameObject.AddComponent<RestPanelRayInteractionSetup>();
    }

    void Start()
    {
        if (solidSphereFollower == null)
            solidSphereFollower = FindObjectOfType<SolidSphereFollower>();
        if (headsetCamera == null && Camera.main != null) headsetCamera = Camera.main.transform;

        if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);

        if (solidSphereFollower != null)
            solidSphereFollower.RestStateChanged += OnRestStateChanged;

        SetVisible(false);
    }

    void OnDestroy()
    {
        if (solidSphereFollower != null)
            solidSphereFollower.RestStateChanged -= OnRestStateChanged;
    }

    void LateUpdate()
    {
        if (_resting) PlaceInFront();
    }

    void Update()
    {
        if (!_resting) return;

        bool controller = enableControllerFallback &&
                          (OVRInput.GetDown(OVRInput.RawButton.B) ||
                           OVRInput.GetDown(OVRInput.RawButton.Y));
        bool keyboard = Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Space);
        if (controller || keyboard) OnResumeClicked();
    }

    void OnRestStateChanged(bool resting)
    {
        _resting = resting;
        if (resting)
        {
            HideCanvasSiblings();
            PlaceInFront();
            UpdateStatusText();
        }
        else
        {
            RestoreCanvasSiblings();
        }

        SetVisible(resting);
    }

    void UpdateStatusText()
    {
        string msg = "Rest. Press Next when ready for the next phase.";
        if (solidSphereFollower != null && solidSphereFollower.CurrentPhase == 2)
            msg = "Press Next to begin nose calibration for Phase 3.";

        if (statusLabel != null) statusLabel.text = msg;
        if (statusLabelTMP != null) statusLabelTMP.text = msg;
    }

    void OnResumeClicked()
    {
        if (!_resting) return;
        _resting = false;
        RestoreCanvasSiblings();
        if (solidSphereFollower != null) solidSphereFollower.ResumeFromRest();
        SetVisible(false);
    }

    void HideCanvasSiblings()
    {
        _hiddenCanvasSiblings.Clear();
        if (_parentCanvas == null) return;

        foreach (Transform child in _parentCanvas.transform)
        {
            if (child.gameObject == gameObject || !child.gameObject.activeSelf) continue;
            _hiddenCanvasSiblings.Add(child.gameObject);
            child.gameObject.SetActive(false);
        }
    }

    void RestoreCanvasSiblings()
    {
        foreach (GameObject go in _hiddenCanvasSiblings)
        {
            if (go != null) go.SetActive(true);
        }
        _hiddenCanvasSiblings.Clear();
    }

    void PlaceInFront()
    {
        if (headsetCamera == null) return;

        Transform moveTarget = _parentCanvas != null ? _parentCanvas.transform : transform;
        Vector3 pos = headsetCamera.position + headsetCamera.forward * spawnDistance;
        pos.y += spawnHeightOffset;
        moveTarget.position = pos;

        Vector3 fwd = headsetCamera.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 0.0001f)
            moveTarget.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
    }

    void SetVisible(bool on)
    {
        if (panelRoot != null) panelRoot.SetActive(on);
    }
}
