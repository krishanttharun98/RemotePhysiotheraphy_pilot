using UnityEngine;
using UnityEngine.UI;

public class VoxelPhaseReplayUI : MonoBehaviour
{
    [Header("VoxelGrid Reference")]
    public VoxelGridManager voxelGridManager;

    [Header("Hemisphere Analytics (beside voxel grid)")]
    public HemisphereAnalyticsStation hemisphereAnalytics;

    [Header("UI Buttons - 5 Phases")]
    public Button phase1Button;
    public Button phase2Button;
    public Button phase3FrontButton;
    public Button phase3RearButton;
    public Button phase4Button;

    private int currentDisplayingPhase = -1; // Track which phase is currently displayed

    void Start()
    {
        // Validate voxel grid reference
        if (voxelGridManager == null)
        {
            voxelGridManager = FindObjectOfType<VoxelGridManager>();
            if (voxelGridManager == null)
            {
                Debug.LogError("VoxelGridManager not found in scene!");
                return;
            }
        }

        if (hemisphereAnalytics == null)
            hemisphereAnalytics = FindObjectOfType<HemisphereAnalyticsStation>();

        // Assign button click listeners
        if (phase1Button != null)
            phase1Button.onClick.AddListener(() => DisplayPhase(1, "Phase 1"));
        
        if (phase2Button != null)
            phase2Button.onClick.AddListener(() => DisplayPhase(2, "Phase 2"));
        
        if (phase3FrontButton != null)
            phase3FrontButton.onClick.AddListener(() => DisplayPhase(3, "Phase 3 Front"));
        
        if (phase3RearButton != null)
            phase3RearButton.onClick.AddListener(() => DisplayPhase(4, "Phase 3 Rear"));
        
        if (phase4Button != null)
            phase4Button.onClick.AddListener(() => DisplayPhase(5, "Phase 4"));
    }

    void DisplayPhase(int phase, string phaseName)
    {
        if (voxelGridManager == null) return;

        // If clicking the same phase, toggle clear
        if (currentDisplayingPhase == phase)
        {
            voxelGridManager.ClearGridDisplay();
            if (hemisphereAnalytics != null) hemisphereAnalytics.ClearDisplay();
            currentDisplayingPhase = -1;
            Debug.Log($"{phaseName} cleared");
            return;
        }

        // Auto-clear previous phase before displaying new one
        if (currentDisplayingPhase != -1)
        {
            voxelGridManager.ClearGridDisplay();
            if (hemisphereAnalytics != null) hemisphereAnalytics.ClearDisplay();
        }

        // Display the new phase
        voxelGridManager.DisplayPhaseHistory(phase);
        if (hemisphereAnalytics != null) hemisphereAnalytics.DisplayPhaseHistory(phase);
        currentDisplayingPhase = phase;
        Debug.Log($"Displaying {phaseName} data");
    }
}
