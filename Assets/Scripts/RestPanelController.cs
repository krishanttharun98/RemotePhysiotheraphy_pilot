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
    [Tooltip("If the ISDK ray-click on the Resume button ever fails, this controller button also resumes while the rest panel is showing.")]
    public bool enableControllerFallback = true;
    public OVRInput.RawButton fallbackResumeButton = OVRInput.RawButton.A;

    private bool _resting;

    void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;

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

    void Update()
    {
        if (!_resting) return;

        bool controller = enableControllerFallback && OVRInput.GetDown(fallbackResumeButton);
        bool keyboard = Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Space);
        if (controller || keyboard) OnResumeClicked();
    }

    void OnRestStateChanged(bool resting)
    {
        _resting = resting;
        if (resting) PlaceInFront();
        SetVisible(resting);

        if (resting)
        {
            string msg = "Rest. Press Resume when ready for the next phase.";
            if (statusLabel != null) statusLabel.text = msg;
            if (statusLabelTMP != null) statusLabelTMP.text = msg;
        }
    }

    void OnResumeClicked()
    {
        _resting = false;
        if (solidSphereFollower != null) solidSphereFollower.ResumeFromRest();
        SetVisible(false);
    }

    void PlaceInFront()
    {
        if (headsetCamera == null) return;
        Vector3 pos = headsetCamera.position + headsetCamera.forward * spawnDistance;
        pos.y += spawnHeightOffset;
        transform.position = pos;

        Vector3 fwd = headsetCamera.forward;
        fwd.y = 0;
        if (fwd != Vector3.zero) transform.rotation = Quaternion.LookRotation(fwd.normalized);
    }

    void SetVisible(bool on)
    {
        if (panelRoot != null) panelRoot.SetActive(on);
    }
}
