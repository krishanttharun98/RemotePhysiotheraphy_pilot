using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using System.Threading.Tasks;
using TMPro;

public class RemoteSessionManager : MonoBehaviour
{
    public static RemoteSessionManager Instance;

    [Header("UI Canvas References")]
    public GameObject roleSelectionCanvas;
    public Button patientButton;
    public Button therapistButton;

    [Header("Therapist Input Fields")]
    public TMP_InputField joinCodeInputField;

    [Header("Cloud Status Output")]
    public TextMeshProUGUI statusDisplayMesh;

    public enum UserRole { Unassigned, Patient, Therapist }
    public UserRole LocalRole { get; private set; } = UserRole.Unassigned;

    // META DOCUMENTATION VARS: References for tracking the overlay keyboard instance
    private TouchScreenKeyboard overlayKeyboard;
    private string currentTypedText = "";
    private bool _isInitializing = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    async void Start()
    {
        if (patientButton != null) patientButton.onClick.AddListener(OnPatientClicked);
        if (therapistButton != null) therapistButton.onClick.AddListener(OnTherapistClicked);
        if (roleSelectionCanvas != null) roleSelectionCanvas.SetActive(true);

        if (_isInitializing) return;
        _isInitializing = true;

        UpdateStatusText("Initializing Cloud Services...");

        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }

            AuthenticationService.Instance.ClearSessionToken();

            if (AuthenticationService.Instance.IsSignedIn)
            {
                UpdateStatusText("Ready. Select your role.", Color.green);
                _isInitializing = false;
                return;
            }

            InitializeMetaPlatform();
        }
        catch (System.Exception e)
        {
            UpdateStatusText($"Init Error:\n{e.Message}", Color.red);
            _isInitializing = false;
        }
    }

    private void InitializeMetaPlatform()
    {
        UpdateStatusText("Verifying Meta Identity...");
        Oculus.Platform.Core.AsyncInitialize().OnComplete(message =>
        {
            if (message.IsError)
            {
                UpdateStatusText("Meta SDK Init Failed", Color.red);
                _isInitializing = false;
                return;
            }
            Oculus.Platform.Users.GetUserProof().OnComplete(OnMetaNonceGenerated);
        });
    }

    private async void OnMetaNonceGenerated(Oculus.Platform.Message<Oculus.Platform.Models.UserProof> message)
    {
        if (message.IsError)
        {
            UpdateStatusText("Failed to fetch login ticket.", Color.red);
            _isInitializing = false;
            return;
        }

        string nonceTicket = message.Data.Value;
        string metaUserId = Oculus.Platform.PlatformSettings.AppID; 

        try
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInWithOculusAsync(nonceTicket, metaUserId);
            }
            UpdateStatusText("Ready. Select your role.", Color.green);
            _isInitializing = false;
        }
        catch (System.Exception e)
        {
            UpdateStatusText($"Oculus Auth Failed:\n{e.Message}", Color.red);
            _isInitializing = false;
        }
    }

    void Update()
    {
        // META DOCUMENTATION PATTERN: Actively monitor text changes while the OS overlay is open
        if (overlayKeyboard != null)
        {
            currentTypedText = overlayKeyboard.text.Trim().ToUpper();
            
            // Sync the text live back into the UI box so the therapist can see what they are typing
            if (joinCodeInputField != null)
            {
                joinCodeInputField.text = currentTypedText;
            }

            // Clean up reference when closed
            if (overlayKeyboard.status == TouchScreenKeyboard.Status.Done || overlayKeyboard.status == TouchScreenKeyboard.Status.Canceled)
            {
                overlayKeyboard = null;
            }
        }
    }

    // ==================== PATIENT LOGIC ====================
    private async void OnPatientClicked()
    {
        if (LocalRole != UserRole.Unassigned) return;
        LocalRole = UserRole.Patient;

        HideUIElements();
        UpdateStatusText("Requesting cloud allocation...");
        string code = await SetupHostWithRelay();

        if (!string.IsNullOrEmpty(code))
        {
            UpdateStatusText($"Session Live!\nJoin Code: {code}", Color.green);
        }
        else
        {
            UpdateStatusText("Relay allocation failed.", Color.red);
        }
    }

    private async Task<string> SetupHostWithRelay()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(2);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            NetworkManager.Singleton.StartHost();
            return joinCode;
        }
        catch (System.Exception e)
        {
            return null;
        }
    }

    // ==================== THERAPIST LOGIC ====================
    private async void OnTherapistClicked()
    {
        if (LocalRole != UserRole.Unassigned) return;

        string validationCode = "";
        
        // SANITIZATION FIX: Force string processing to strip out any hidden space formatting
        if (joinCodeInputField != null)
        {
            validationCode = joinCodeInputField.text.Replace(" ", "").Trim().ToUpper();
        }
        else if (!string.IsNullOrEmpty(currentTypedText))
        {
            validationCode = currentTypedText.Replace(" ", "").Trim().ToUpper();
        }

        if (string.IsNullOrEmpty(validationCode))
        {
            UpdateStatusText("Opening System Keyboard Overlay...", Color.yellow);
            overlayKeyboard = TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default);
            return;
        }

        // Double-check the exact character length requirements
        if (validationCode.Length != 6)
        {
            UpdateStatusText($"Error: Code is {validationCode.Length}/6 digits!", Color.red);
            return;
        }
        LocalRole = UserRole.Therapist;
        HideUIElements();

        UpdateStatusText($"Connecting to cloud room: {validationCode}...");

        bool success = await SetupClientWithRelay(validationCode);
        if (success)
        {
            UpdateStatusText("Connected to Remote Patient!", Color.green);
        }
        else
        {
            UpdateStatusText("Failed to find session code.", Color.red);
            LocalRole = UserRole.Unassigned; // Reset state container to allow retry
            if (patientButton != null) patientButton.gameObject.SetActive(true);
            if (therapistButton != null) therapistButton.gameObject.SetActive(true);
            if (joinCodeInputField != null) joinCodeInputField.gameObject.SetActive(true);
        }
    }

    private async Task<bool> SetupClientWithRelay(string code)
    {
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(code);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData
            );

            return NetworkManager.Singleton.StartClient();
        }
        catch (System.Exception e)
        {
            return false;
        }
    }

    private void HideUIElements()
    {
        if (patientButton != null) patientButton.gameObject.SetActive(false);
        if (therapistButton != null) therapistButton.gameObject.SetActive(false);
        if (joinCodeInputField != null) joinCodeInputField.gameObject.SetActive(false);
    }

    private void UpdateStatusText(string msg, Color? color = null)
    {
        if (statusDisplayMesh != null)
        {
            statusDisplayMesh.text = msg;
            statusDisplayMesh.color = color ?? Color.white;
        }
    }
}