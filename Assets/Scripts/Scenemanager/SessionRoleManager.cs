using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode; // Required to call the Host and Client methods

public class SessionRoleManager : MonoBehaviour
{
    public static SessionRoleManager Instance;

    [Header("UI References")]
    [Tooltip("Assign the root Canvas GameObject or the Panel containing the buttons here")]
    public GameObject roleSelectionCanvas; 
    public Button patientButton;
    public Button therapistButton;

    [Header("Canvas Placement Settings")]
    public float spawnDistance = 1.2f;      
    public float spawnHeightOffset = -0.1f; 

    // Local state tracking for this specific headset
    public enum UserRole { Unassigned, Patient, Therapist }
    public UserRole LocalRole { get; private set; } = UserRole.Unassigned;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Explicitly set up the UI button click listeners automatically via code
        if (patientButton != null) patientButton.onClick.AddListener(SelectPatientRole);
        if (therapistButton != null) therapistButton.onClick.AddListener(SelectTherapistRole);

        // Make sure the panel is visible initially
        if (roleSelectionCanvas != null) roleSelectionCanvas.SetActive(true);

        // Instantly position the UI canvas in front of this specific user's view
        PositionCanvasInFrontOfHeadset();
    }

    private void PositionCanvasInFrontOfHeadset()
    {
        Transform centerEyeCamera = Camera.main != null ? Camera.main.transform : null;

        if (centerEyeCamera != null && roleSelectionCanvas != null)
        {
            Vector3 targetPos = centerEyeCamera.position + (centerEyeCamera.forward * spawnDistance);
            targetPos.y += spawnHeightOffset;
            roleSelectionCanvas.transform.position = targetPos;

            Vector3 lookTarget = centerEyeCamera.position;
            lookTarget.y = roleSelectionCanvas.transform.position.y; 
            roleSelectionCanvas.transform.LookAt(lookTarget);
            roleSelectionCanvas.transform.Rotate(0, 180, 0); 
        }
    }

    public void SelectPatientRole()
    {
        if (LocalRole != UserRole.Unassigned) return;

        LocalRole = UserRole.Patient;
        Debug.Log("[Role Selected] Local user assigned as: PATIENT");

        // FIX: Start the Network Manager as HOST
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.StartHost();
            Debug.Log("[Netcode] Started Session as HOST.");
        }
        else
        {
            Debug.LogError("NetworkManager Singleton is missing from the scene!");
        }

        HideSelectionUI();
    }

    public void SelectTherapistRole()
    {
        if (LocalRole != UserRole.Unassigned) return;

        LocalRole = UserRole.Therapist;
        Debug.Log("[Role Selected] Local user assigned as: THERAPIST");

        // FIX: Start the Network Manager as CLIENT
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.StartClient();
            Debug.Log("[Netcode] Connecting to Host as CLIENT...");
        }
        else
        {
            Debug.LogError("NetworkManager Singleton is missing from the scene!");
        }

        HideSelectionUI();
    }

    private void HideSelectionUI()
    {
        if (roleSelectionCanvas != null) roleSelectionCanvas.SetActive(false);
    }
}