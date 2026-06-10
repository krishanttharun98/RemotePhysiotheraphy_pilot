using UnityEngine;

public class VoxelGridManager : MonoBehaviour
{
    [Header("Grid Configurations")]
    [Tooltip("Increase Y dimension to expand vertical space.")]
    public Vector3Int gridDimensions = new Vector3Int(30, 40, 30); // Raised Y to 40 for extra bottom length
    public float voxelSize = 0.05f;

    [Header("Vertical Bias Tuning")]
    [Tooltip("Shifts the box center upwards relative to the headset so more tracking volume extends downward.")]
    public float verticalCenterOffset = -0.3f; // Negative values push the tracking grid lower down the body

    [Header("Tracking Targets")]
    public Transform mainCamera; 
    public Transform leftController; 
    public Transform rightController; 

    [Header("Rendering & Material")]
    public Mesh baseCubeMesh; 
    public Material heatmapMaterial;

    [Header("Meta SDK Placement Settings")]
    public Transform placementPointer; 
    public float placementDistance = 1.2f;

    [Header("SolidSphereFollower Alignment Links")]
    public float phase4YOffset = -0.50f;
    
    private bool isPlacingGrid = true;
    private bool isLiveTracking = true; // Flag to pause hardware tracking updates during replay mode
    private Vector2[][] taskInteractionData = new Vector2[4][];
    private Vector2[][] historicalPhaseData = new Vector2[6][]; // Expanded to size 6 to safely support index 5
    private int currentActiveTask = 0;
    private int displayingHistoricalPhase = -1; // -1 = no historical phase displayed

    private Vector3[] voxelPositions;

    private ComputeBuffer positionBuffer;
    private ComputeBuffer interactionBuffer;
    private Bounds renderBounds;

    private Vector3 displayGridWorldPos;
    private Quaternion displayGridWorldRot = Quaternion.identity;
    
    private Vector3 trackingGridWorldPos;
    private Quaternion trackingGridWorldRot = Quaternion.identity;

    private int trackingCurrentPhase = 0;
    private Vector3 trackingLocalNosePos = Vector3.zero;

    void Start()
    {
        InitializeGridData();
        
        if (placementPointer == null) placementPointer = rightController;
        if (mainCamera == null) mainCamera = Camera.main?.transform;
        
        heatmapMaterial.SetInt("_IsVisualizationActive", 2); 
    }

    void InitializeGridData()
    {
        int totalVoxels = gridDimensions.x * gridDimensions.y * gridDimensions.z;

        for (int i = 0; i < 4; i++)
        {
            taskInteractionData[i] = new Vector2[totalVoxels];
        }

        // Initialize historical phase data array with 6 allocations to avoid array boundaries exception
        for (int i = 0; i < 6; i++)
        {
            historicalPhaseData[i] = new Vector2[totalVoxels];
        }

        voxelPositions = new Vector3[totalVoxels];

        int index = 0;
        Vector3 halfSizeOffset = new Vector3(gridDimensions.x, gridDimensions.y, gridDimensions.z) * voxelSize * 0.5f;
        
        // Apply vertical shift to lower bounds safely
        halfSizeOffset.y += (verticalCenterOffset / voxelSize) * voxelSize;

        for (int x = 0; x < gridDimensions.x; x++)
        {
            for (int y = 0; y < gridDimensions.y; y++)
            {
                for (int z = 0; z < gridDimensions.z; z++)
                {
                    Vector3 localPos = new Vector3(x, y, z) * voxelSize - halfSizeOffset;
                    voxelPositions[index] = localPos;
                    index++;
                }
            }
        }

        if (positionBuffer != null) positionBuffer.Release();
        if (interactionBuffer != null) interactionBuffer.Release();

        positionBuffer = new ComputeBuffer(totalVoxels, sizeof(float) * 3);
        positionBuffer.SetData(voxelPositions);

        interactionBuffer = new ComputeBuffer(totalVoxels, sizeof(float) * 2); 
        interactionBuffer.SetData(taskInteractionData[currentActiveTask]);

        heatmapMaterial.SetBuffer("_VoxelPositions", positionBuffer);
        heatmapMaterial.SetBuffer("_VoxelInteractions", interactionBuffer);
        heatmapMaterial.SetFloat("_VoxelScale", voxelSize);

        renderBounds = new Bounds(Vector3.zero, Vector3.one * 25f); 
    }

    void Update()
    {
        if (mainCamera == null) return;

        if (isPlacingGrid)
        {
            HandleManualPlacementMode();
            return; 
        }

        // Align tracking coordinates with phase states
        if (trackingCurrentPhase == 3)
        {
            trackingGridWorldPos = mainCamera.TransformPoint(trackingLocalNosePos);
        }
        else if (trackingCurrentPhase == 4)
        {
            trackingGridWorldPos = mainCamera.position + new Vector3(0, phase4YOffset, 0);
        }
        else
        {
            trackingGridWorldPos = mainCamera.position;
        }
        
        Vector3 flatForward = mainCamera.forward;
        flatForward.y = 0;
        if (flatForward != Vector3.zero)
        {
            trackingGridWorldRot = Quaternion.LookRotation(flatForward.normalized);
        }
        else
        {
            trackingGridWorldRot = mainCamera.rotation;
        }

        // Only poll and save hardware interaction data if live session monitoring is active
        if (isLiveTracking)
        {
            TrackControllerInWorkspace(leftController, isRightHand: false);
            TrackControllerInWorkspace(rightController, isRightHand: true);
            interactionBuffer.SetData(taskInteractionData[currentActiveTask]);
        }

        Matrix4x4 displayMatrix = Matrix4x4.TRS(displayGridWorldPos, displayGridWorldRot, Vector3.one);
        heatmapMaterial.SetMatrix("_LocalToWorldMatrix", displayMatrix);
        
        // Set shader mode to show only tracking voxels (no cube mesh)
        heatmapMaterial.SetInt("_IsVisualizationActive", 3);
        
        // Render only the voxel tracking visualization (shader handles not rendering the mesh)
        Graphics.DrawMeshInstancedProcedural(baseCubeMesh, 0, heatmapMaterial, renderBounds, voxelPositions.Length);
    }

    void HandleManualPlacementMode()
    {
        if (placementPointer == null) return;

        displayGridWorldPos = placementPointer.position + (placementPointer.forward * placementDistance);

        if (mainCamera != null)
        {
            Vector3 lookDir = displayGridWorldPos - mainCamera.position;
            lookDir.y = 0; 
            if (lookDir != Vector3.zero)
            {
                displayGridWorldRot = Quaternion.LookRotation(lookDir.normalized);
            }
        }

        heatmapMaterial.SetInt("_IsVisualizationActive", 2); 
        Matrix4x4 previewMatrix = Matrix4x4.TRS(displayGridWorldPos, displayGridWorldRot, Vector3.one);
        heatmapMaterial.SetMatrix("_LocalToWorldMatrix", previewMatrix);
        
        // Show base cube mesh during placement for alignment reference
        Graphics.DrawMeshInstancedProcedural(baseCubeMesh, 0, heatmapMaterial, renderBounds, voxelPositions.Length);

        if (OVRInput.GetDown(OVRInput.RawButton.B))
        {
            TriggerLockSpatialAnchor();
        }
    }

    public void TriggerLockSpatialAnchor()
    {
        if (!isPlacingGrid) return;
        isPlacingGrid = false;
        heatmapMaterial.SetInt("_IsVisualizationActive", 3); 
    }

    /// <summary>
    /// Force clears all recorded paint points on the active data arrays
    /// </summary>
    public void ResetAllVoxelData()
    {
        isLiveTracking = true; // Automatically resume live calculations when starting a new runtime phase
        displayingHistoricalPhase = -1;

        if (taskInteractionData == null) return;
        
        for (int i = 0; i < 4; i++)
        {
            if (taskInteractionData[i] != null)
            {
                System.Array.Clear(taskInteractionData[i], 0, taskInteractionData[i].Length);
            }
        }
        if (interactionBuffer != null)
        {
            interactionBuffer.SetData(taskInteractionData[currentActiveTask]);
        }
    }

    public void SynchronizeTrackingState(int activePhase, Vector3 localNosePos)
    {
        trackingCurrentPhase = activePhase;
        trackingLocalNosePos = localNosePos;
    }

    void TrackControllerInWorkspace(Transform controller, bool isRightHand)
    {
        if (controller == null) return;

        Vector3 localPos = controller.position - trackingGridWorldPos;
        localPos = Quaternion.Inverse(trackingGridWorldRot) * localPos;

        Vector3 halfSizeOffset = new Vector3(gridDimensions.x, gridDimensions.y, gridDimensions.z) * voxelSize * 0.5f;
        halfSizeOffset.y += (verticalCenterOffset / voxelSize) * voxelSize;
        
        Vector3 samplePos = localPos + halfSizeOffset;

        if (samplePos.x >= 0 && samplePos.x < gridDimensions.x * voxelSize &&
            samplePos.y >= 0 && samplePos.y < gridDimensions.y * voxelSize &&
            samplePos.z >= 0 && samplePos.z < gridDimensions.z * voxelSize)
        {
            int x = Mathf.FloorToInt(samplePos.x / voxelSize);
            int y = Mathf.FloorToInt(samplePos.y / voxelSize);
            int z = Mathf.FloorToInt(samplePos.z / voxelSize);

            int flatIndex = x * (gridDimensions.y * gridDimensions.z) + y * gridDimensions.z + z;

            if (isRightHand) taskInteractionData[currentActiveTask][flatIndex].y = 1f; 
            else taskInteractionData[currentActiveTask][flatIndex].x = 1f; 
        }
    }

    public void SwitchActiveTask(int taskIndex)
    {
        if (taskIndex < 0 || taskIndex >= 4) return;
        currentActiveTask = taskIndex;
        if (interactionBuffer != null && isLiveTracking) 
        {
            interactionBuffer.SetData(taskInteractionData[currentActiveTask]);
        }
    }

    /// <summary>
    /// Save the current phase's tracking data to historical storage
    /// For Phase 3, it automatically detects front/rear part and saves separately
    /// phaseNumber: 1-4 (regular phases), with phase 3 handling front/rear internally
    /// </summary>
    public void SavePhaseData(int phase)
    {
        if (phase < 1 || phase > 4) return;
        if (taskInteractionData[phase - 1] == null) return;
        
        // For phase 3, save to both front (phase 3) and rear (phase 4) in history
        if (phase == 3)
        {
            System.Array.Copy(taskInteractionData[phase - 1], historicalPhaseData[phase], taskInteractionData[phase - 1].Length);
            Debug.Log($"Phase 3 data saved to history");
        }
        else
        {
            // Normal phase mapping: phase 1->1, 2->2, 4->5
            int historyIndex = (phase == 4) ? 5 : phase;
            System.Array.Copy(taskInteractionData[phase - 1], historicalPhaseData[historyIndex], taskInteractionData[phase - 1].Length);
            Debug.Log($"Phase {phase} data saved to history");
        }
    }

    /// <summary>
    /// Save phase 3 data with explicit front/rear designation
    /// isRear: true for Phase 3 Rear, false for Phase 3 Front
    /// </summary>
    public void SavePhase3Data(bool isRear)
    {
        if (taskInteractionData[2] == null) return; // Phase 3 is index 2
        
        // Phase 3 Front -> history index 3, Phase 3 Rear -> history index 4
        int historyIndex = isRear ? 4 : 3;
        System.Array.Copy(taskInteractionData[2], historicalPhaseData[historyIndex], taskInteractionData[2].Length);
        Debug.Log($"Phase 3 {(isRear ? "Rear" : "Front")} data saved to history");
    }

    /// <summary>
    /// Display historical data for a specific phase (1-5)
    /// 1 = Phase 1, 2 = Phase 2, 3 = Phase 3 Front, 4 = Phase 3 Rear, 5 = Phase 4
    /// Automatically clears previous display when switching phases
    /// </summary>
    public void DisplayPhaseHistory(int phase)
    {
        if (phase < 1 || phase > 5) return;
        if (historicalPhaseData[phase] == null) return;
        
        isLiveTracking = false; // Disconnect update loops from re-writing over custom chosen history view
        displayingHistoricalPhase = phase;

        // Update the interaction buffer with historical data
        if (interactionBuffer != null)
        {
            interactionBuffer.SetData(historicalPhaseData[phase]);
        }
        Debug.Log($"Displaying Phase {phase} historical data");
    }

    /// <summary>
    /// Clear all display (no tracing visible)
    /// Call this when you want to hide all tracking visualization
    /// </summary>
    public void ClearGridDisplay()
    {
        isLiveTracking = false; // Turn off live loops to prevent automatic refresh overrides
        displayingHistoricalPhase = -1;
        
        // Create empty data array
        int totalVoxels = gridDimensions.x * gridDimensions.y * gridDimensions.z;
        Vector2[] emptyData = new Vector2[totalVoxels];
        
        if (interactionBuffer != null)
        {
            interactionBuffer.SetData(emptyData);
        }
        Debug.Log("Grid display cleared");
    }

    private void OnDestroy()
    {
        if (positionBuffer != null) positionBuffer.Release();
        if (interactionBuffer != null) interactionBuffer.Release();
    }
}