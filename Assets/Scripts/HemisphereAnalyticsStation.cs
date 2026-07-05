using UnityEngine;
using System.Collections.Generic;
using TMPro;

// Hemisphere analytics beside the voxel grid.
// Stats: quadrant popped/total from VoxelGridManager (voxel replay logic).
// Visual: smooth 2D heatmap patches on the shell — one patch per 2x2 live-task bubble block;
// darkness = how many of the 4 bubbles in that block were popped (4/4 = darkest).
public class HemisphereAnalyticsStation : MonoBehaviour
{
    static readonly string[] QuadrantLabels = { "Top-Left", "Bottom-Left", "Top-Right", "Bottom-Right" };

    [Header("References")]
    public VoxelGridManager voxelGridManager;
    [Tooltip("Drag the Solid Sphere Follower component from the RWS object in the Hierarchy.")]
    public SolidSphereFollower solidSphereFollower;

    [Header("Layout")]
    public float sideOffset = 1.2f;

    [Header("2D Heatmap Overlay")]
    public Material heatmapOverlayMaterial;
    [Range(0.1f, 1f)] public float maxDarkness = 0.85f;
    [Tooltip("Slight shell offset so the overlay sits on top of the base hemisphere.")]
    public float overlayRadiusScale = 1.015f;

    [Header("Optional Stats Labels (4 quadrants)")]
    [Tooltip("Optional scene UI labels. World-space labels are created beside the hemisphere at runtime.")]
    public TMP_Text[] quadrantStatLabels;

    [Header("World-Space Stats Beside Hemisphere")]
    public bool createWorldSpaceStats = true;
    public float statsPanelLocalX = 0.35f;
    public float statsPanelTopY = 0.28f;
    public float statsLabelSpacing = 0.11f;
    public float statsFontSize = 0.045f;

    private Transform _analyticsRoot;
    private GameObject _hemisphereBase;
    private GameObject _heatmapOverlay;
    private GameObject _statsPanelRoot;
    private Material _heatmapMatInstance;
    private TMP_Text[] _worldStatLabels;

    private int _displayingPhase = -1;
    private bool _heatmapDirty = true;

    void Start()
    {
        if (voxelGridManager == null) voxelGridManager = FindObjectOfType<VoxelGridManager>();
        if (solidSphereFollower == null)
            solidSphereFollower = FindObjectOfType<SolidSphereFollower>();

        _analyticsRoot = new GameObject("HemisphereAnalyticsRoot").transform;

        if (heatmapOverlayMaterial != null)
        {
            _heatmapMatInstance = Instantiate(heatmapOverlayMaterial);
            _heatmapMatInstance.SetFloat("_MaxDarkness", maxDarkness);
        }

        if (solidSphereFollower != null)
        {
            solidSphereFollower.PhaseStarted += OnPhaseStarted;
            solidSphereFollower.PhaseCompleted += OnPhaseCompletedHandler;
            solidSphereFollower.BubbleSpawned += OnBubbleEvent;
            solidSphereFollower.BubblePopped += OnBubbleEvent;
        }
    }

    void OnDestroy()
    {
        if (solidSphereFollower != null)
        {
            solidSphereFollower.PhaseStarted -= OnPhaseStarted;
            solidSphereFollower.PhaseCompleted -= OnPhaseCompletedHandler;
            solidSphereFollower.BubbleSpawned -= OnBubbleEvent;
            solidSphereFollower.BubblePopped -= OnBubbleEvent;
        }
        if (_heatmapMatInstance != null) Destroy(_heatmapMatInstance);
    }

    void OnPhaseCompletedHandler(int _, bool __) => _heatmapDirty = true;
    void OnBubbleEvent(Vector3 _, int __, float ___, int ____, int _____, int ______) => _heatmapDirty = true;

    void Update()
    {
        UpdateAnalyticsAnchor();

        if (_heatmapDirty)
        {
            RebuildHeatmapOverlay();
            _heatmapDirty = false;
        }

        RefreshStatsLabels();
        UpdateStatsBillboard();
    }

    void EnsureWorldStatLabels()
    {
        if (!createWorldSpaceStats || _analyticsRoot == null || _worldStatLabels != null) return;

        _statsPanelRoot = new GameObject("QuadrantStatsPanel");
        _statsPanelRoot.transform.SetParent(_analyticsRoot, false);

        _worldStatLabels = new TMP_Text[4];
        for (int q = 0; q < 4; q++)
        {
            GameObject go = new GameObject($"QuadrantStat_{q}");
            go.transform.SetParent(_statsPanelRoot.transform, false);
            go.transform.localPosition = new Vector3(statsPanelLocalX, statsPanelTopY - q * statsLabelSpacing, 0f);

            TMP_Text tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = statsFontSize;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;
            tmp.text = QuadrantLabels[q];
            tmp.rectTransform.sizeDelta = new Vector2(0.55f, 0.08f);
            _worldStatLabels[q] = tmp;
        }
    }

    void UpdateStatsBillboard()
    {
        if (_worldStatLabels == null || Camera.main == null) return;

        Vector3 camPos = Camera.main.transform.position;
        foreach (TMP_Text label in _worldStatLabels)
        {
            if (label == null) continue;
            Vector3 toCam = label.transform.position - camPos;
            if (toCam.sqrMagnitude > 0.0001f)
                label.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }
    }

    void UpdateAnalyticsAnchor()
    {
        if (_analyticsRoot == null || voxelGridManager == null) return;
        if (!voxelGridManager.IsStationLocked)
        {
            _analyticsRoot.gameObject.SetActive(false);
            if (_statsPanelRoot != null) _statsPanelRoot.SetActive(false);
            return;
        }

        _analyticsRoot.gameObject.SetActive(true);
        EnsureWorldStatLabels();
        if (_statsPanelRoot != null) _statsPanelRoot.SetActive(true);

        Vector3 anchor = voxelGridManager.StationRoot != null
            ? voxelGridManager.StationRoot.position
            : transform.position;

        Vector3 right = voxelGridManager.placementCylinderGizmo != null
            ? voxelGridManager.placementCylinderGizmo.transform.right
            : transform.right;

        _analyticsRoot.SetPositionAndRotation(anchor + right * sideOffset, Quaternion.identity);
    }

    void OnPhaseStarted(int phase, bool rear)
    {
        _displayingPhase = -1;

        if (_hemisphereBase != null) Destroy(_hemisphereBase);
        if (solidSphereFollower != null)
            _hemisphereBase = solidSphereFollower.BuildAlignedQuadrantSphere(_analyticsRoot);

        // Bubbles spawn after this callback; overlay rebuilds on BubbleSpawned events.
        _heatmapDirty = true;
    }

    void RefreshStatsLabels()
    {
        if (voxelGridManager == null) return;
        int hist = _displayingPhase;
        voxelGridManager.GetQuadrantStatistics(hist, out int[] totals, out int[] popped, out float[] pct);

        UpdateLabelSet(quadrantStatLabels, totals, popped, pct);
        UpdateLabelSet(_worldStatLabels, totals, popped, pct);
    }

    void UpdateLabelSet(TMP_Text[] labels, int[] totals, int[] popped, float[] pct)
    {
        if (labels == null) return;
        for (int q = 0; q < 4; q++)
        {
            if (q >= labels.Length || labels[q] == null) continue;
            labels[q].text = $"{QuadrantLabels[q]}\n{popped[q]}/{totals[q]} ({pct[q] * 100f:0}%)";
        }
    }

    void RebuildHeatmapOverlay()
    {
        if (_heatmapOverlay != null) Destroy(_heatmapOverlay);
        if (_analyticsRoot == null || voxelGridManager == null || solidSphereFollower == null) return;
        if (_heatmapMatInstance == null) return;

        VoxelPhaseGridData grid = voxelGridManager.GetGridDataForDisplay(_displayingPhase);
        if (grid == null)
        {
            if (_displayingPhase >= 1)
                Debug.LogWarning($"[HemisphereAnalytics] No grid data for history slot {_displayingPhase}.");
            return;
        }

        _heatmapMatInstance.SetFloat("_MaxDarkness", maxDarkness);

        var verts = new List<Vector3>();
        var tris = new List<int>();
        var colors = new List<Color>();

        if (grid.isPhase4)
        {
            for (int br = 0; br < grid.ringCount - 1; br++)
                for (int bl = 0; bl < grid.lonCount - 1; bl++)
                    TryAddBlockPatch(grid, br, bl, verts, tris, colors);
        }
        else
        {
            for (int bl = 0; bl < grid.lonCount - 1; bl++)
                for (int bt = 0; bt < VoxelPhaseGridData.LatRows - 1; bt++)
                    TryAddBlockPatch(grid, bl, bt, verts, tris, colors);
        }

        if (verts.Count == 0) return;

        _heatmapOverlay = new GameObject("HeatmapOverlay2D");
        _heatmapOverlay.transform.SetParent(_analyticsRoot, false);
        _heatmapOverlay.transform.localPosition = Vector3.zero;
        _heatmapOverlay.transform.localRotation = Quaternion.identity;

        MeshFilter mf = _heatmapOverlay.AddComponent<MeshFilter>();
        MeshRenderer mr = _heatmapOverlay.AddComponent<MeshRenderer>();
        mr.material = _heatmapMatInstance;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        Mesh mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        mf.mesh = mesh;
    }

    void TryAddBlockPatch(VoxelPhaseGridData grid, int blockA, int blockB,
        List<Vector3> verts, List<int> tris, List<Color> colors)
    {
        if (!grid.TryGetBlockPopCount(blockA, blockB, out int popCount)) return;

        float darkness = popCount / 4f;
        Color patchColor = HeatmapColor(darkness);

        Vector3 p00, p10, p01, p11;
        if (grid.isPhase4)
        {
            int br = blockA;
            int bl = blockB;
            float r00 = grid.radii[br] * overlayRadiusScale;
            float r10 = grid.radii[br] * overlayRadiusScale;
            float r01 = grid.radii[br + 1] * overlayRadiusScale;
            float r11 = grid.radii[br + 1] * overlayRadiusScale;
            p00 = solidSphereFollower.MapBubbleGridToLocal(grid.phase, grid.isRear, bl, 0, r00, br);
            p10 = solidSphereFollower.MapBubbleGridToLocal(grid.phase, grid.isRear, bl + 1, 0, r10, br);
            p01 = solidSphereFollower.MapBubbleGridToLocal(grid.phase, grid.isRear, bl, 0, r01, br + 1);
            p11 = solidSphereFollower.MapBubbleGridToLocal(grid.phase, grid.isRear, bl + 1, 0, r11, br + 1);
        }
        else
        {
            float r = grid.radii[0] * overlayRadiusScale;
            int bl = blockA;
            int bt = blockB;
            p00 = solidSphereFollower.MapBubbleGridToLocal(grid.phase, grid.isRear, bl, bt, r);
            p10 = solidSphereFollower.MapBubbleGridToLocal(grid.phase, grid.isRear, bl + 1, bt, r);
            p01 = solidSphereFollower.MapBubbleGridToLocal(grid.phase, grid.isRear, bl, bt + 1, r);
            p11 = solidSphereFollower.MapBubbleGridToLocal(grid.phase, grid.isRear, bl + 1, bt + 1, r);
        }

        int baseIdx = verts.Count;
        verts.Add(p00); verts.Add(p10); verts.Add(p01); verts.Add(p11);
        colors.Add(patchColor); colors.Add(patchColor); colors.Add(patchColor); colors.Add(patchColor);
        tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
        tris.Add(baseIdx + 1); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
    }

    // 0/4 pops = light tint; 4/4 pops = darkest warm tone.
    static Color HeatmapColor(float darkness)
    {
        darkness = Mathf.Clamp01(darkness);

        Color cold = new Color(0.75f, 0.82f, 0.92f, 0.18f);
        Color warm = new Color(0.95f, 0.15f, 0.05f, 0.75f);
        Color hot = new Color(0.15f, 0.02f, 0.02f, 1f);

        if (darkness < 0.5f)
            return Color.Lerp(cold, warm, darkness / 0.5f);
        return Color.Lerp(warm, hot, (darkness - 0.5f) / 0.5f);
    }

    public void DisplayPhaseHistory(int phase)
    {
        if (phase < 1 || phase > 5) return;
        _displayingPhase = phase;

        if (_hemisphereBase != null) Destroy(_hemisphereBase);

        VoxelPhaseGridData grid = voxelGridManager != null
            ? voxelGridManager.GetGridDataForDisplay(phase)
            : null;

        if (solidSphereFollower != null && grid != null)
        {
            _hemisphereBase = solidSphereFollower.BuildQuadrantSphereForPhase(
                grid.phase, grid.isRear, grid.radii, _analyticsRoot);
        }
        else if (solidSphereFollower != null)
        {
            Debug.LogWarning($"[HemisphereAnalytics] No grid snapshot for history slot {phase} yet.");
        }

        RebuildHeatmapOverlay();
    }

    public void ClearDisplay()
    {
        _displayingPhase = -1;

        if (_hemisphereBase != null) Destroy(_hemisphereBase);

        if (solidSphereFollower != null && voxelGridManager != null && voxelGridManager.IsStationLocked)
            _hemisphereBase = solidSphereFollower.BuildAlignedQuadrantSphere(_analyticsRoot);

        RebuildHeatmapOverlay();
    }
}
