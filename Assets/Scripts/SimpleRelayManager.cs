using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using TMPro;

public class SimpleRelayManager : MonoBehaviour
{
    public static SimpleRelayManager Instance;

    public enum UserRole { Unassigned, Patient, Therapist }
    public UserRole LocalRole { get; private set; } = UserRole.Unassigned;

    [Header("Role Selection Controls")]
    public Button hostButton;
    public Button joinButton;
    public TMP_InputField joinInput;

    [Header("Patient Session UI")]
    public TextMeshProUGUI codeDisplayMesh;
    public TextMeshProUGUI statusDisplayMesh;
    public TextMeshProUGUI patientInstructionMesh;

    [Header("Therapist Session UI")]
    public TextMeshProUGUI therapistInstructionMesh;
    public GameObject[] therapistPhaseButtonObjects;

    private TouchScreenKeyboard overlayKeyboard;
    private RectTransform _uiPanelRoot;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    async void Start()
    {
        if (hostButton != null) hostButton.onClick.AddListener(CreateRelay);
        if (joinButton != null) joinButton.onClick.AddListener(OnJoinButtonClicked);

        CachePanelRoot();
        AutoWirePhaseButtons();
        EnsureInstructionTexts();
        ShowRoleSelectionUI();

        UpdateStatusText("Initializing Unity Cloud Services...");

        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                InitializationOptions options = new InitializationOptions();

                #if !UNITY_EDITOR
                string uniqueProfileId = "QuestRoom_" + UnityEngine.Random.Range(1000, 9999).ToString();
                options.SetProfile(uniqueProfileId);
                #endif

                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            UpdateStatusText("Cloud Connected. Choose Host or Join.", Color.green);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Relay Init Error]: {e.Message}");
            UpdateStatusText($"Cloud Init Error: {e.Message}. Forcing UI active.", Color.red);
        }
    }

    void Update()
    {
        if (overlayKeyboard != null)
        {
            string cleanText = overlayKeyboard.text.Trim().ToUpper();
            if (joinInput != null)
            {
                joinInput.text = cleanText;
            }

            if (overlayKeyboard.status == TouchScreenKeyboard.Status.Done || overlayKeyboard.status == TouchScreenKeyboard.Status.Canceled)
            {
                overlayKeyboard = null;
            }
        }
    }

    public async void CreateRelay()
    {
        if (LocalRole != UserRole.Unassigned) return;

        LocalRole = UserRole.Patient;
        UpdateStatusText("Allocating global cloud room...");
        HideRoleSelectionControls();

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(2);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            if (codeDisplayMesh != null)
            {
                codeDisplayMesh.text = $"ROOM CODE:\n{joinCode}";
            }
            UpdateStatusText("Hosting session successfully on cloud!", Color.green);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            var relayServerData = allocation.ToRelayServerData("dtls");
            transport.SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartHost();
            ShowPatientSessionUI();
        }
        catch (System.Exception e)
        {
            LocalRole = UserRole.Unassigned;
            UpdateStatusText($"Host Allocation Failure: {e.Message}", Color.red);
            ShowRoleSelectionUI();
        }
    }

    public void OnJoinButtonClicked()
    {
        if (LocalRole != UserRole.Unassigned) return;

        string currentCode = joinInput != null ? joinInput.text.Trim().ToUpper() : "";

        if (string.IsNullOrEmpty(currentCode))
        {
            UpdateStatusText("Opening Meta OS System Keyboard...", Color.yellow);
            overlayKeyboard = TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default);
            return;
        }

        if (currentCode.Length != 6)
        {
            UpdateStatusText($"Error: Code must be exactly 6 characters long!", Color.red);
            return;
        }

        JoinRelay(currentCode);
    }

    public async void JoinRelay(string code)
    {
        if (LocalRole != UserRole.Unassigned) return;

        LocalRole = UserRole.Therapist;
        UpdateStatusText($"Connecting to room: {code}...");
        HideRoleSelectionControls();

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(code);
            UpdateStatusText("Connected to remote patient!", Color.green);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            var relayServerData = joinAllocation.ToRelayServerData("dtls");
            transport.SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartClient();
            ShowTherapistSessionUI();
        }
        catch (System.Exception e)
        {
            LocalRole = UserRole.Unassigned;
            UpdateStatusText($"Failed to locate session code. Double check text inputs.", Color.red);
            ShowRoleSelectionUI();
        }
    }

    void ShowRoleSelectionUI()
    {
        SetButtonActive(hostButton, true);
        SetButtonActive(joinButton, true);
        SetObjectActive(joinInput != null ? joinInput.gameObject : null, true);

        SetInstructionActive(patientInstructionMesh, false);
        SetInstructionActive(therapistInstructionMesh, false);
        SetObjectActive(codeDisplayMesh != null ? codeDisplayMesh.gameObject : null, false);
        SetPhaseButtonsActive(false);
        SetObjectActive(statusDisplayMesh != null ? statusDisplayMesh.gameObject : null, true);
    }

    void ShowPatientSessionUI()
    {
        HideRoleSelectionControls();

        if (patientInstructionMesh != null)
        {
            patientInstructionMesh.text = "Press button A";
            patientInstructionMesh.gameObject.SetActive(true);
        }

        SetInstructionActive(therapistInstructionMesh, false);
        SetObjectActive(codeDisplayMesh != null ? codeDisplayMesh.gameObject : null, true);
        SetObjectActive(statusDisplayMesh != null ? statusDisplayMesh.gameObject : null, true);
        SetPhaseButtonsActive(false);
    }

    void ShowTherapistSessionUI()
    {
        HideRoleSelectionControls();

        if (therapistInstructionMesh != null)
        {
            therapistInstructionMesh.text = "Place the grids as per your wish";
            therapistInstructionMesh.gameObject.SetActive(true);
        }

        SetInstructionActive(patientInstructionMesh, false);
        SetObjectActive(codeDisplayMesh != null ? codeDisplayMesh.gameObject : null, false);
        SetObjectActive(statusDisplayMesh != null ? statusDisplayMesh.gameObject : null, false);
        SetPhaseButtonsActive(true);
    }

    void HideRoleSelectionControls()
    {
        SetButtonActive(hostButton, false);
        SetButtonActive(joinButton, false);
        SetObjectActive(joinInput != null ? joinInput.gameObject : null, false);
    }

    void SetPhaseButtonsActive(bool visible)
    {
        if (therapistPhaseButtonObjects == null) return;

        foreach (GameObject buttonObject in therapistPhaseButtonObjects)
        {
            SetObjectActive(buttonObject, visible);
        }
    }

    void CachePanelRoot()
    {
        if (hostButton != null)
        {
            _uiPanelRoot = hostButton.transform.parent as RectTransform;
        }
    }

    void AutoWirePhaseButtons()
    {
        if (therapistPhaseButtonObjects != null && therapistPhaseButtonObjects.Length > 0) return;

        VoxelPhaseReplayUI voxelUi = FindObjectOfType<VoxelPhaseReplayUI>();
        if (voxelUi == null) return;

        therapistPhaseButtonObjects = new[]
        {
            voxelUi.phase1Button != null ? voxelUi.phase1Button.gameObject : null,
            voxelUi.phase2Button != null ? voxelUi.phase2Button.gameObject : null,
            voxelUi.phase3FrontButton != null ? voxelUi.phase3FrontButton.gameObject : null,
            voxelUi.phase3RearButton != null ? voxelUi.phase3RearButton.gameObject : null,
            voxelUi.phase4Button != null ? voxelUi.phase4Button.gameObject : null
        };
    }

    void EnsureInstructionTexts()
    {
        if (_uiPanelRoot == null) CachePanelRoot();
        if (_uiPanelRoot == null) return;

        if (patientInstructionMesh == null)
        {
            patientInstructionMesh = CreateInstructionText("PatientInstructionTMP", "Press button A", new Vector2(0f, 170f));
        }

        if (therapistInstructionMesh == null)
        {
            therapistInstructionMesh = CreateInstructionText("TherapistInstructionTMP", "Place the grids as per your wish", new Vector2(0f, 170f));
        }

        SetInstructionActive(patientInstructionMesh, false);
        SetInstructionActive(therapistInstructionMesh, false);
    }

    TextMeshProUGUI CreateInstructionText(string objectName, string message, Vector2 anchoredPosition)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(_uiPanelRoot, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(260f, 60f);
        rect.localScale = Vector3.one * 0.5f;

        TextMeshProUGUI textMesh = textObject.GetComponent<TextMeshProUGUI>();
        textMesh.text = message;
        textMesh.fontSize = 24f;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.color = Color.white;
        textMesh.raycastTarget = false;

        if (statusDisplayMesh != null && statusDisplayMesh.font != null)
        {
            textMesh.font = statusDisplayMesh.font;
            textMesh.fontSharedMaterial = statusDisplayMesh.fontSharedMaterial;
        }

        return textMesh;
    }

    void SetButtonActive(Button button, bool active)
    {
        SetObjectActive(button != null ? button.gameObject : null, active);
    }

    void SetInstructionActive(TextMeshProUGUI instructionMesh, bool active)
    {
        SetObjectActive(instructionMesh != null ? instructionMesh.gameObject : null, active);
    }

    void SetObjectActive(GameObject target, bool active)
    {
        if (target != null) target.SetActive(active);
    }

    void UpdateStatusText(string msg, Color? color = null)
    {
        if (statusDisplayMesh != null)
        {
            statusDisplayMesh.text = msg;
            statusDisplayMesh.color = color ?? Color.white;
        }
    }
}
