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

    [Header("Lightweight Performance Placement Visualizer")]
    [Tooltip("Assign a Cylinder GameObject here to act as the voxel grid center anchor preview")]
    public GameObject placementCylinderGizmo;

    [Header("SolidSphereFollower Alignment Links")]
    public float phase4YOffset = -0.50f;
    
    private bool isPlacingGrid = true;
    private bool isLiveTracking = true; // Flag to pause hardware tracking updates during replay mode
    private Vector2[][] taskInteractionData = new Vector2[5][];
    private Vector2[][] historicalPhaseData = new Vector2[6][]; // Expanded to size 6 to safely support index 5
    private int currentActiveTask = 0;
    private int displayingHistoricalPhase = -1; // -1 = no historical phase displayed

    private Vector3[] voxelPositions;
    
    // Performance Optimization Arrays to filter out empty voxels
    private Vector3[] activePositions;
    private Vector2[] activeInteractions;

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

        if (!ColocationRoleHelper.IsLocalTherapist())
        {
            isPlacingGrid = false;
            if (placementCylinderGizmo != null) placementCylinderGizmo.SetActive(false);
        }
        else if (placementCylinderGizmo != null)
        {
            placementCylinderGizmo.SetActive(true);
        }
    }

    void InitializeGridData()
    {
        int totalVoxels = gridDimensions.x * gridDimensions.y * gridDimensions.z;

        for (int i = 0; i < 5; i++)
        {
            taskInteractionData[i] = new Vector2[totalVoxels];
        }

        // Initialize historical phase data array with 6 allocations to avoid array boundaries exception
        for (int i = 0; i < 6; i++)
        {
            historicalPhaseData[i] = new Vector2[totalVoxels];
        }

        voxelPositions = new Vector3[totalVoxels];
        activePositions = new Vector3[totalVoxels];
        activeInteractions = new Vector2[totalVoxels];

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

        // Buffers are initialized to total maximum capacity but populated dynamically
        positionBuffer = new ComputeBuffer(totalVoxels, sizeof(float) * 3);
        interactionBuffer = new ComputeBuffer(totalVoxels, sizeof(float) * 2); 

        heatmapMaterial.SetBuffer("_VoxelPositions", positionBuffer);
        heatmapMaterial.SetBuffer("_VoxelInteractions", interactionBuffer);
        heatmapMaterial.SetFloat("_VoxelScale", voxelSize);

        renderBounds = new Bounds(Vector3.zero, Vector3.one * 25f); 
    }

    void Update()
    {
        if (mainCamera == null) return;

        if (ColocationRoleHelper.IsLocalTherapist())
        {
            if (placementCylinderGizmo != null)
            {
                placementCylinderGizmo.SetActive(true);
                placementCylinderGizmo.transform.position = displayGridWorldPos;
                placementCylinderGizmo.transform.rotation = displayGridWorldRot;
            }

            if (isPlacingGrid)
            {
                HandleManualPlacementMode();
                return;
            }
        }
        else if (placementCylinderGizmo != null)
        {
            placementCylinderGizmo.SetActive(false);
        }

        if (isLiveTracking && ColocationRoleHelper.IsLocalPatient())
        {
            Vector3 currentTrackingOriginPos;
            Quaternion currentTrackingOriginRot;

            // Align tracking tracking origin offset dynamically based on tracking phase
            if (trackingCurrentPhase == 3)
            {
                currentTrackingOriginPos = mainCamera.TransformPoint(trackingLocalNosePos);
            }
            else if (trackingCurrentPhase == 4)
            {
                currentTrackingOriginPos = mainCamera.position + new Vector3(0, phase4YOffset, 0);
            }
            else
            {
                currentTrackingOriginPos = mainCamera.position;
            }

            Vector3 flatForward = mainCamera.forward;
            flatForward.y = 0;
            if (flatForward != Vector3.zero)
            {
                currentTrackingOriginRot = Quaternion.LookRotation(flatForward.normalized);
            }
            else
            {
                currentTrackingOriginRot = mainCamera.rotation;
            }

            // Save tracking coordinate systems
            trackingGridWorldPos = currentTrackingOriginPos;
            trackingGridWorldRot = currentTrackingOriginRot;

            // Track hardware controllers directly into the current active task layer
            TrackControllerInWorkspace(leftController, false, taskInteractionData[currentActiveTask]);
            TrackControllerInWorkspace(rightController, true, taskInteractionData[currentActiveTask]);
        }

        // Determine whether to evaluate live active task layers or historical trace layers
        Vector2[] currentDataSource = (displayingHistoricalPhase != -1) 
            ? historicalPhaseData[displayingHistoricalPhase] 
            : taskInteractionData[currentActiveTask];

        // Always render the voxels centered cleanly around the display grid position (the Cylinder center)
        RenderActiveVoxelsOnly(currentDataSource, displayGridWorldPos);
    }

    void HandleManualPlacementMode()
    {
        if (!ColocationRoleHelper.IsLocalTherapist()) return;
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

        // Detect Button B (Right) or Button X (Left) to lock spatial position
        if (OVRInput.GetDown(OVRInput.RawButton.B) || OVRInput.GetDown(OVRInput.RawButton.X))
        {
            TriggerLockSpatialAnchor();
        }
    }

    void RenderActiveVoxelsOnly(Vector2[] sourceData, Vector3 renderPosition)
    {
        if (sourceData == null || positionBuffer == null || interactionBuffer == null) return;

        int activeCount = 0;
        int totalCount = sourceData.Length;

        // Extract and pack only the points that are actively being drawn/interacted with
        for (int i = 0; i < totalCount; i++)
        {
            if (sourceData[i].x > 0f || sourceData[i].y > 0f)
            {
                activePositions[activeCount] = voxelPositions[i];
                activeInteractions[activeCount] = sourceData[i];
                activeCount++;
            }
        }

        // Only make the draw call if there are active painted voxels present in the workspace
        if (activeCount > 0)
        {
            positionBuffer.SetData(activePositions, 0, 0, activeCount);
            interactionBuffer.SetData(activeInteractions, 0, 0, activeCount);

            Matrix4x4 displayMatrix = Matrix4x4.TRS(renderPosition, displayGridWorldRot, Vector3.one);
            heatmapMaterial.SetMatrix("_LocalToWorldMatrix", displayMatrix);
            
            // Set shader mode to trace mode (making un-interacted cells completely transparent)
            heatmapMaterial.SetInt("_IsVisualizationActive", 3);
            
            // Render optimized procedural instances cleanly without duplication overhead
            Graphics.DrawMeshInstancedProcedural(baseCubeMesh, 0, heatmapMaterial, renderBounds, activeCount, null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
        }
    }

    public void TriggerLockSpatialAnchor()
    {
        if (!ColocationRoleHelper.IsLocalTherapist()) return;
        if (!isPlacingGrid) return;
        isPlacingGrid = false;
        heatmapMaterial.SetInt("_IsVisualizationActive", 3);

        // Keep the cylinder visualizer completely active as your permanent structural anchor
        if (placementCylinderGizmo != null) placementCylinderGizmo.SetActive(true);

        Debug.Log("Spatial anchor position locked. Cylinder retained as permanent visualization center.");
    }

    public void ResetAllVoxelData()
    {
        isLiveTracking = true; 
        displayingHistoricalPhase = -1;

        if (taskInteractionData == null) return;
        
        for (int i = 0; i < 5; i++)
        {
            if (taskInteractionData[i] != null)
            {
                System.Array.Clear(taskInteractionData[i], 0, taskInteractionData[i].Length);
            }
        }
    }

    public void SynchronizeTrackingState(int activePhase, Vector3 localNosePos)
    {
        trackingCurrentPhase = activePhase;
        trackingLocalNosePos = localNosePos;
    }

    void TrackControllerInWorkspace(Transform controller, bool isRightHand, Vector2[] targetData)
    {
        if (controller == null || targetData == null) return;

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

            if (flatIndex >= 0 && flatIndex < targetData.Length)
            {
                if (isRightHand) targetData[flatIndex].y = 1f; 
                else targetData[flatIndex].x = 1f;
            }
        }
    }

    public void SwitchActiveTask(int taskIndex)
    {
        if (taskIndex < 0 || taskIndex >= 5) return;
        currentActiveTask = taskIndex;
    }

    public void SavePhaseData(int phase)
    {
        if (phase < 1 || phase > 4) return;

        int taskIndex = phase - 1;
        if (phase == 4) taskIndex = 4; 
        
        if (taskInteractionData[taskIndex] == null) return;
        
        if (phase == 3)
        {
            System.Array.Copy(taskInteractionData[2], historicalPhaseData[3], taskInteractionData[2].Length);
            Debug.Log($"Phase 3 Front data saved to history");
        }
        else
        {
            int historyIndex = (phase == 4) ? 5 : phase;
            System.Array.Copy(taskInteractionData[taskIndex], historicalPhaseData[historyIndex], taskInteractionData[taskIndex].Length);
            Debug.Log($"Phase {phase} data saved to history");
        }
    }

    public void SavePhase3Data(bool isRear)
    {
        int taskIndex = isRear ? 3 : 2;
        if (taskInteractionData[taskIndex] == null) return;
        
        int historyIndex = isRear ? 4 : 3;
        System.Array.Copy(taskInteractionData[taskIndex], historicalPhaseData[historyIndex], taskInteractionData[taskIndex].Length);
        Debug.Log($"Phase 3 {(isRear ? "Rear" : "Front")} data saved to history");
    }

    public void DisplayPhaseHistory(int phase)
    {
        if (phase < 1 || phase > 5) return;
        if (historicalPhaseData[phase] == null) return;
        
        isLiveTracking = false; 
        displayingHistoricalPhase = phase;
        Debug.Log($"Displaying Phase {phase} historical data");
    }

    public void ClearGridDisplay()
    {
        isLiveTracking = false; 
        displayingHistoricalPhase = -1;
        Debug.Log("Grid display cleared");
    }

    private void OnDestroy()
    {
        if (positionBuffer != null) positionBuffer.Release();
        if (interactionBuffer != null) pointerBufferRelease();
    }

    private void pointerBufferRelease()
    {
        if (interactionBuffer != null) interactionBuffer.Release();
    }
}