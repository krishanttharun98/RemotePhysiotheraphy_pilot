using UnityEngine;
using System.Collections.Generic;

// Unified observation station (local / single-user).
//
// After the therapist places & locks the cylinder, this script:
//   1. Replaces the cylinder with a digital-twin avatar anchor (optional driver).
//   2. Builds a per-phase 4-colour quadrant sphere around the anchor, sized to the
//      live RWS phase radius (reproducing the multi-ring radii for Phase 4).
//   3. Drops a coloured marker on that sphere wherever a bubble is popped
//      (this is the merged "replay" of BubbleTrajectoryReplayManager).
//   4. Paints an accumulating cold->hot heatmap ONTO THE SPHERE SURFACE wherever
//      the user's controllers interact, tracked per phase, in real time.
//   5. Snapshots each phase so the existing phase buttons can recall it.
public class VoxelGridManager : MonoBehaviour
{
    [Header("Live Task Source")]
    [Tooltip("Drag the Solid Sphere Follower component from the RWS object in the Hierarchy.")]
    public SolidSphereFollower solidSphereFollower;

    [Header("Tracking Targets")]
    public Transform mainCamera;
    public Transform leftController;
    public Transform rightController;

    [Header("Rendering & Material")]
    public Mesh baseCubeMesh;
    public Material heatmapMaterial;
    [Tooltip("Edge length of each heatmap surface cell cube.")]
    public float voxelSize = 0.04f;

    [Header("Surface Heatmap Resolution")]
    public int longitudeSegments = 24;
    public int latitudeSegments = 13;
    [Tooltip("How close (metres) a controller must be to the shell radius to paint it.")]
    public float surfaceBand = 0.12f;
    [Tooltip("Heat added per second of dwell on a cell.")]
    public float heatPerSecond = 2.5f;
    [Tooltip("Heat value that maps to full 'hot' colour in the shader.")]
    public float maxHeat = 6f;

    [Header("Quadrant Sphere")]
    [Tooltip("4 materials = top-left, bottom-left, top-right, bottom-right quadrant colours.")]
    public Material[] quadrantMaterials;

    [Header("Pop Markers")]
    [Tooltip("Small sphere prefab spawned where a bubble is popped. Falls back to a primitive sphere if null.")]
    public GameObject popMarkerPrefab;

    [Header("Meta SDK Placement Settings")]
    public Transform placementPointer;
    public float placementDistance = 1.2f;

    [Header("Lightweight Performance Placement Visualizer")]
    [Tooltip("Cylinder GameObject that previews the station centre. Its mesh is hidden after lock.")]
    public GameObject placementCylinderGizmo;

    [Header("Digital Twin Avatar (Movement SDK driven)")]
    [Tooltip("The duplicated Movement-SDK avatar that should stand at the cylinder. Kept inactive until the cylinder is locked, then placed at the anchor each frame. Its body pose comes from the Movement SDK; we only move its root.")]
    public GameObject cylinderAvatarObject;
    [Tooltip("The HEAD bone/transform of the cylinder avatar. The round quadrant sphere + heatmap + pop markers are centred here and follow it, just like the live RWS follows the real headset.")]
    public Transform voxelAvatarHead;
    [Tooltip("If true the cylinder avatar root is re-snapped to the anchor every LateUpdate (so the Movement SDK can't drag it back onto the user).")]
    public bool keepCylinderAvatarAtAnchor = true;
    [Tooltip("World Y (metres) of the floor. The twin's feet are pinned here so it stands on the floor at the cylinder's XZ, just like the headset overlay avatar. 0 = floor-level tracking origin.")]
    public float avatarFloorY = 0f;

    public bool IsStationLocked => !_isPlacingGrid;
    public Transform StationRoot => _stationRoot;

    // ---------- Internal data ----------
    private class ShellData
    {
        public float radius;
        public float[] heat; // length = longitudeSegments * latitudeSegments
    }

    private struct MarkerData
    {
        public Vector3 localPos;
        public int quadrant;
        public float scale;
    }

    private bool _isPlacingGrid = true;
    private Vector3 _displayPos;
    private Quaternion _displayRot = Quaternion.identity;

    // Live (current phase) layers
    private List<ShellData> _liveShells = new List<ShellData>();
    private readonly List<MarkerData> _liveMarkers = new List<MarkerData>();
    private readonly List<GameObject> _liveMarkerObjects = new List<GameObject>();

    // Per-phase history (slots 1..5 = P1, P2, P3Front, P3Rear, P4)
    private readonly Dictionary<int, List<ShellData>> _historyShells = new Dictionary<int, List<ShellData>>();
    private readonly Dictionary<int, List<MarkerData>> _historyMarkers = new Dictionary<int, List<MarkerData>>();
    private readonly List<GameObject> _historyMarkerObjects = new List<GameObject>();

    private int _displayingPhase = -1; // -1 = render live

    // Per-phase bubble grid for quadrant stats + analytics heatmap (history 1..5).
    private VoxelPhaseGridData _liveGridData;
    private readonly Dictionary<int, VoxelPhaseGridData> _historyGridData = new Dictionary<int, VoxelPhaseGridData>();
    private int _livePhase;
    private bool _liveRear;

    private Transform _stationRoot;
    private GameObject _stationQuadrant;

    // GPU buffers
    private int _cellsPerShell;
    private int _capacity;
    private Vector3[] _renderPositions;
    private Vector2[] _renderInteractions;
    private ComputeBuffer _positionBuffer;
    private ComputeBuffer _interactionBuffer;
    private Bounds _renderBounds;

    void Start()
    {
        if (solidSphereFollower == null)
            solidSphereFollower = FindObjectOfType<SolidSphereFollower>();

        if (mainCamera == null) mainCamera = Camera.main != null ? Camera.main.transform : null;
        if (placementPointer == null) placementPointer = rightController;

        _cellsPerShell = longitudeSegments * latitudeSegments;
        _capacity = _cellsPerShell * 4; // up to 4 shells (Phase 4)
        _renderPositions = new Vector3[_capacity];
        _renderInteractions = new Vector2[_capacity];

        _positionBuffer = new ComputeBuffer(_capacity, sizeof(float) * 3);
        _interactionBuffer = new ComputeBuffer(_capacity, sizeof(float) * 2);

        if (heatmapMaterial != null)
        {
            heatmapMaterial.SetBuffer("_VoxelPositions", _positionBuffer);
            heatmapMaterial.SetBuffer("_VoxelInteractions", _interactionBuffer);
            heatmapMaterial.SetFloat("_VoxelScale", voxelSize);
            heatmapMaterial.SetFloat("_MaxHeat", maxHeat);
            heatmapMaterial.SetInt("_IsVisualizationActive", 3);
        }
        _renderBounds = new Bounds(Vector3.zero, Vector3.one * 25f);

        // Make sure we always have a VISIBLE cylinder to place. If none was assigned,
        // create a primitive cylinder so the placement preview is visible.
        if (placementCylinderGizmo == null)
        {
            placementCylinderGizmo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            placementCylinderGizmo.name = "StationCylinder";
            placementCylinderGizmo.transform.localScale = new Vector3(0.08f, 0.25f, 0.08f);
            var c = placementCylinderGizmo.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Debug.LogWarning("[Station] No placementCylinderGizmo assigned - created a runtime cylinder so placement is visible.");
        }

        // Force the cylinder visible at start (a prior lock may have left its renderers disabled).
        EnsureGizmoVisible();

        // Station root is top-level; we drive it onto the voxel avatar's head each frame.
        _stationRoot = new GameObject("StationRoot").transform;

        if (cylinderAvatarObject != null) cylinderAvatarObject.SetActive(false);

        if (solidSphereFollower != null)
        {
            solidSphereFollower.PhaseStarted += OnPhaseStarted;
            solidSphereFollower.PhaseCompleted += OnPhaseCompleted;
            solidSphereFollower.BubbleSpawned += OnBubbleSpawned;
            solidSphereFollower.BubblePopped += OnBubblePopped;
            solidSphereFollower.TestEnded += OnTestEnded;
        }
    }

    void OnDestroy()
    {
        if (solidSphereFollower != null)
        {
            solidSphereFollower.PhaseStarted -= OnPhaseStarted;
            solidSphereFollower.PhaseCompleted -= OnPhaseCompleted;
            solidSphereFollower.BubbleSpawned -= OnBubbleSpawned;
            solidSphereFollower.BubblePopped -= OnBubblePopped;
            solidSphereFollower.TestEnded -= OnTestEnded;
        }
        if (_positionBuffer != null) _positionBuffer.Release();
        if (_interactionBuffer != null) _interactionBuffer.Release();
    }

    void Update()
    {
        // --- Placement (right controller + B/X to lock) ---
        if (_isPlacingGrid)
        {
            // Keep the cylinder visible/active the whole time until B is pressed.
            EnsureGizmoVisible();
            HandleManualPlacementMode();
        }

        // Keep the cylinder gizmo at the chosen placement point.
        if (placementCylinderGizmo != null)
        {
            placementCylinderGizmo.transform.SetPositionAndRotation(_displayPos, _displayRot);
        }

        // Centre the whole station (round sphere + heatmap + markers) on the voxel avatar's head,
        // so it follows the twin's head exactly like the live RWS follows the real headset.
        UpdateStationAnchor();

        // --- Live heatmap accumulation onto the sphere surface ---
        if (!_isPlacingGrid && _displayingPhase == -1 && solidSphereFollower != null && solidSphereFollower.IsTestRunning)
        {
            AccumulateSurface(leftController);
            AccumulateSurface(rightController);
        }

        RenderHeatmap();
    }

    void LateUpdate()
    {
        // Keep the Movement-SDK digital twin standing on the floor at the locked cylinder anchor.
        // We only move the root; the SDK supplies the live body/hand pose on top.
        if (!_isPlacingGrid && keepCylinderAvatarAtAnchor && cylinderAvatarObject != null && cylinderAvatarObject.activeInHierarchy)
        {
            cylinderAvatarObject.transform.SetPositionAndRotation(AnchorFloorPos(), AnchorYaw());
        }
    }

    // Feet-on-floor position at the cylinder's XZ.
    Vector3 AnchorFloorPos()
    {
        Vector3 p = placementCylinderGizmo.transform.position;
        return new Vector3(p.x, avatarFloorY, p.z);
    }

    // Yaw-only rotation from the cylinder placement (keeps the avatar upright, fixed facing).
    Quaternion AnchorYaw()
    {
        Vector3 f = placementCylinderGizmo.transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(f.normalized) : Quaternion.identity;
    }

    // Guarantees there is always a visible cylinder mesh to aim while placing.
    void EnsureGizmoVisible()
    {
        if (placementCylinderGizmo == null) return;
        placementCylinderGizmo.SetActive(true);

        var rends = placementCylinderGizmo.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0)
        {
            // The assigned object has no mesh - attach a primitive so it is actually visible.
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "CylinderVisual";
            var c = visual.GetComponent<Collider>();
            if (c != null) Destroy(c);
            visual.transform.SetParent(placementCylinderGizmo.transform, false);
            visual.transform.localScale = new Vector3(0.08f, 0.25f, 0.08f);
        }
        else
        {
            foreach (var r in rends) r.enabled = true;
        }
    }

    void UpdateStationAnchor()
    {
        if (_stationRoot == null) return;

        Vector3 pos;
        if (!_isPlacingGrid && voxelAvatarHead != null && voxelAvatarHead.gameObject.activeInHierarchy)
            pos = voxelAvatarHead.position;                       // follow the twin's head
        else
            pos = placementCylinderGizmo.transform.position;      // before lock / no head assigned

        // Identity rotation so coordinates match the live RWS world frame (translational only).
        _stationRoot.SetPositionAndRotation(pos, Quaternion.identity);
    }

    // ---------------- Placement ----------------
    void HandleManualPlacementMode()
    {
        if (placementPointer == null) return;

        _displayPos = placementPointer.position + (placementPointer.forward * placementDistance);

        if (mainCamera != null)
        {
            Vector3 lookDir = _displayPos - mainCamera.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero) _displayRot = Quaternion.LookRotation(lookDir.normalized);
        }

        if (OVRInput.GetDown(OVRInput.RawButton.B) || OVRInput.GetDown(OVRInput.RawButton.X) || Input.GetKeyDown(KeyCode.L))
        {
            LockStation();
        }
    }

    void LockStation()
    {
        if (!_isPlacingGrid) return;
        _isPlacingGrid = false;

        // Hide the cylinder mesh but keep the transform as the station anchor.
        foreach (var r in placementCylinderGizmo.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

        // Bring up the Movement-SDK digital-twin avatar standing on the floor at the cylinder XZ.
        if (cylinderAvatarObject != null)
        {
            cylinderAvatarObject.SetActive(true);
            cylinderAvatarObject.transform.SetPositionAndRotation(AnchorFloorPos(), AnchorYaw());
        }

        Debug.Log("[Station] Cylinder locked. Digital-twin avatar placed on the floor at the anchor.");
    }

    // ---------------- Phase lifecycle (from SolidSphereFollower events) ----------------
    void OnPhaseStarted(int phase, bool rear)
    {
        _displayingPhase = -1;
        _livePhase = phase;
        _liveRear = rear;
        ClearLiveMarkerObjects();
        _liveMarkers.Clear();
        ClearHistoryMarkerObjects();

        if (solidSphereFollower != null)
        {
            _liveGridData = VoxelPhaseGridData.CreateForPhase(
                phase, rear, solidSphereFollower.longitudeSegments, solidSphereFollower.GetCurrentPhaseRadii());
        }

        // Build live shells for this phase's radii.
        _liveShells.Clear();
        if (solidSphereFollower != null)
        {
            float[] radii = solidSphereFollower.GetCurrentPhaseRadii();
            foreach (float r in radii)
                _liveShells.Add(new ShellData { radius = r, heat = new float[_cellsPerShell] });

            // 4-quadrant coloured sphere(s) around the avatar head. Built by the live task
            // itself so the colours line up EXACTLY with where bubbles/pop markers land,
            // and parented to the same station root as the markers (the avatar head).
            if (_stationQuadrant != null) Destroy(_stationQuadrant);
            _stationQuadrant = solidSphereFollower.BuildAlignedQuadrantSphere(_stationRoot);
        }
    }

    void OnPhaseCompleted(int phase, bool rear)
    {
        int histIndex = HistoryIndex(phase, rear);
        if (histIndex < 1) return;

        _historyShells[histIndex] = CloneShells(_liveShells);
        _historyMarkers[histIndex] = new List<MarkerData>(_liveMarkers);
        if (_liveGridData != null) _historyGridData[histIndex] = _liveGridData.Clone();
    }

    void OnBubbleSpawned(Vector3 localPos, int quadrant, float scale, int lonIdx, int latIdx, int ringIdx)
    {
        if (_displayingPhase != -1 || _liveGridData == null) return;
        _liveGridData.RegisterSpawn(lonIdx, latIdx, ringIdx, quadrant);
    }

    void OnBubblePopped(Vector3 localPos, int quadrant, float scale, int lonIdx, int latIdx, int ringIdx)
    {
        if (_displayingPhase != -1) return;

        if (_liveGridData != null)
            _liveGridData.RegisterPop(lonIdx, latIdx, ringIdx, quadrant);

        var data = new MarkerData { localPos = localPos, quadrant = quadrant, scale = scale };
        _liveMarkers.Add(data);
        _liveMarkerObjects.Add(SpawnMarker(data));
    }

    void OnTestEnded()
    {
        // Leave the last live layer visible; phase buttons can recall any phase.
    }

    int HistoryIndex(int phase, bool rear)
    {
        if (phase == 4) return 5;
        if (phase == 3) return rear ? 4 : 3;
        return phase; // 1 or 2
    }

    // ---------------- Pop markers ----------------
    GameObject SpawnMarker(MarkerData data)
    {
        GameObject marker;
        if (popMarkerPrefab != null)
        {
            marker = Instantiate(popMarkerPrefab);
        }
        else
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        }

        marker.transform.SetParent(_stationRoot, false);
        marker.transform.localPosition = data.localPos;
        marker.transform.localRotation = Quaternion.identity;
        marker.transform.localScale = Vector3.one * data.scale;

        var col = marker.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var dos = marker.GetComponent<DisappearOnSelect>();
        if (dos != null) Destroy(dos);

        if (quadrantMaterials != null && data.quadrant >= 0 && data.quadrant < quadrantMaterials.Length)
        {
            var rend = marker.GetComponentInChildren<Renderer>();
            if (rend != null) rend.material = quadrantMaterials[data.quadrant];
        }
        return marker;
    }

    // ---------------- Surface heatmap accumulation ----------------
    void AccumulateSurface(Transform controller)
    {
        if (controller == null || solidSphereFollower == null) return;

        if (_stationRoot == null) return;

        // Paint relative to the voxel station (same frame as pop markers), not the live RWS centre.
        Vector3 local = controller.position - _stationRoot.position;
        float dist = local.magnitude;
        if (dist < 0.0001f) return;

        for (int s = 0; s < _liveShells.Count; s++)
        {
            ShellData shell = _liveShells[s];
            if (Mathf.Abs(dist - shell.radius) > surfaceBand) continue;

            int idx = BinIndex(local);
            if (idx >= 0 && idx < shell.heat.Length)
            {
                shell.heat[idx] = Mathf.Min(maxHeat, shell.heat[idx] + heatPerSecond * Time.deltaTime);
            }
        }
    }

    int BinIndex(Vector3 localDir)
    {
        Vector3 n = localDir.normalized;
        float vAngle = Mathf.Asin(Mathf.Clamp(n.y, -1f, 1f));
        float latP = vAngle / Mathf.PI + 0.5f;
        float hAngle = Mathf.Atan2(n.x, n.z);
        float lonP = hAngle / (2f * Mathf.PI);
        if (lonP < 0f) lonP += 1f;

        int lat = Mathf.Clamp(Mathf.RoundToInt(latP * (latitudeSegments - 1)), 0, latitudeSegments - 1);
        int lon = ((Mathf.RoundToInt(lonP * longitudeSegments) % longitudeSegments) + longitudeSegments) % longitudeSegments;
        return lon * latitudeSegments + lat;
    }

    Vector3 SpherePoint(float lonP, float latP, float r)
    {
        float hAngle = lonP * Mathf.PI * 2f;
        float vAngle = (latP - 0.5f) * Mathf.PI;
        return new Vector3(r * Mathf.Cos(vAngle) * Mathf.Sin(hAngle), r * Mathf.Sin(vAngle), r * Mathf.Cos(vAngle) * Mathf.Cos(hAngle));
    }

    Vector3 CellLocalPos(int flatIndex, float r)
    {
        int lon = flatIndex / latitudeSegments;
        int lat = flatIndex % latitudeSegments;
        float lonP = (float)lon / longitudeSegments;
        float latP = (float)lat / (latitudeSegments - 1);
        return SpherePoint(lonP, latP, r);
    }

    // ---------------- Rendering ----------------
    void RenderHeatmap()
    {
        if (heatmapMaterial == null || baseCubeMesh == null) return;

        List<ShellData> source = (_displayingPhase != -1 && _historyShells.ContainsKey(_displayingPhase))
            ? _historyShells[_displayingPhase]
            : _liveShells;

        if (source == null) return;

        int count = 0;
        for (int s = 0; s < source.Count && count < _capacity; s++)
        {
            ShellData shell = source[s];
            if (shell == null || shell.heat == null) continue;
            for (int i = 0; i < shell.heat.Length && count < _capacity; i++)
            {
                if (shell.heat[i] > 0f)
                {
                    _renderPositions[count] = CellLocalPos(i, shell.radius);
                    _renderInteractions[count] = new Vector2(shell.heat[i], 0f);
                    count++;
                }
            }
        }

        if (count == 0) return;

        _positionBuffer.SetData(_renderPositions, 0, 0, count);
        _interactionBuffer.SetData(_renderInteractions, 0, 0, count);

        // Heatmap is centred on the same station root as the round sphere & markers (the avatar head).
        Matrix4x4 m = Matrix4x4.TRS(_stationRoot.position, _stationRoot.rotation, Vector3.one);
        heatmapMaterial.SetMatrix("_LocalToWorldMatrix", m);
        heatmapMaterial.SetFloat("_MaxHeat", maxHeat);
        heatmapMaterial.SetInt("_IsVisualizationActive", 3);

        Graphics.DrawMeshInstancedProcedural(baseCubeMesh, 0, heatmapMaterial, _renderBounds, count, null,
            UnityEngine.Rendering.ShadowCastingMode.Off, false);
    }

    // ---------------- Phase button API (used by VoxelPhaseReplayUI) ----------------
    public void DisplayPhaseHistory(int phase)
    {
        if (phase < 1 || phase > 5) return;

        _displayingPhase = phase;
        ClearLiveMarkerObjects();
        ClearHistoryMarkerObjects();
        SetQuadrantWedgesActive(false);

        if (_historyMarkers.TryGetValue(phase, out var markers))
        {
            foreach (var md in markers) _historyMarkerObjects.Add(SpawnMarker(md));
        }
    }

    public void ClearGridDisplay()
    {
        _displayingPhase = -1;
        ClearHistoryMarkerObjects();
        SetQuadrantWedgesActive(true);
        // Restore live markers for the current/last phase.
        if (_liveMarkerObjects.Count == 0)
        {
            foreach (var md in _liveMarkers) _liveMarkerObjects.Add(SpawnMarker(md));
        }
    }

    // ---------------- Helpers ----------------
    void SetQuadrantWedgesActive(bool on)
    {
        if (_stationQuadrant != null) _stationQuadrant.SetActive(on);
    }

    void ClearLiveMarkerObjects()
    {
        foreach (var go in _liveMarkerObjects) if (go != null) Destroy(go);
        _liveMarkerObjects.Clear();
    }

    void ClearHistoryMarkerObjects()
    {
        foreach (var go in _historyMarkerObjects) if (go != null) Destroy(go);
        _historyMarkerObjects.Clear();
    }

    List<ShellData> CloneShells(List<ShellData> src)
    {
        var clone = new List<ShellData>();
        foreach (var s in src)
        {
            clone.Add(new ShellData { radius = s.radius, heat = (float[])s.heat.Clone() });
        }
        return clone;
    }

    // ---------------- Analytics API (hemisphere station) ----------------
    public VoxelPhaseGridData GetGridDataForDisplay(int historyIndex = -1)
    {
        if (historyIndex >= 1)
        {
            if (_historyGridData.TryGetValue(historyIndex, out VoxelPhaseGridData hist))
                return hist;

            // Fall back to the live capture only while that same phase is still running.
            if (_liveGridData != null && HistoryIndex(_livePhase, _liveRear) == historyIndex)
                return _liveGridData;

            return null;
        }

        return _liveGridData;
    }

    public void GetQuadrantStatistics(int historyIndex, out int[] totals, out int[] popped, out float[] percentages)
    {
        totals = new int[4];
        popped = new int[4];
        percentages = new float[4];
        VoxelPhaseGridData data = GetGridDataForDisplay(historyIndex);
        if (data == null) return;
        for (int q = 0; q < 4; q++)
        {
            totals[q] = data.quadrantTotal[q];
            popped[q] = data.quadrantPopped[q];
            percentages[q] = data.GetQuadrantPopPercent(q);
        }
    }
}
