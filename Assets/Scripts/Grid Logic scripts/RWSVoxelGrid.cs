using UnityEngine;
using System.Collections.Generic;

public class RWSVoxelGrid : MonoBehaviour
{
    [System.Serializable]
    public class VoxelData
    {
        public int hitCount = 0;
        public float timeSpent = 0f;
        public int quadrant = 0;
    }

    [Header("Tracking References")]
    [Tooltip("Assign the XR Meta Movement SDK hand bone transform here.")]
    public Transform userHandTransform;
    [Tooltip("The reference center point (e.g., headset or shoulder position).")]
    public Transform gridCenterAnchor;

    [Header("Grid Configuration")]
    [Tooltip("The size of each voxel cube side in meters (0.05 = 5cm cubic resolutions).")]
    public float cellSize = 0.05f;
    [Tooltip("Maximum radius of the reachable workspace envelope to track.")]
    public float maxTrackingRadius = 1.2f;
    public Material voxelMaterial;

    // Core Data Structures
    private Dictionary<Vector3Int, VoxelData> _activeVoxels = new Dictionary<Vector3Int, VoxelData>();
    
    // Mesh Generation Variables
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _proceduralMesh;
    private List<Vector3> _vertices = new List<Vector3>();
    private List<int> _triangles = new List<int>();
    private List<Color> _colors = new List<Color>();
    private bool _meshDirty = false;

    void Awake()
    {
        // Setup procedural mesh components natively
        _meshFilter = gameObject.AddComponent<MeshFilter>();
        _meshRenderer = gameObject.AddComponent<MeshRenderer>();
        _meshRenderer.material = voxelMaterial;
        
        _proceduralMesh = new Mesh();
        _proceduralMesh.name = "RWS_VoxelVolume_Mesh";
        _meshFilter.mesh = _proceduralMesh;
    }

    void Update()
    {
        if (userHandTransform == null || gridCenterAnchor == null) return;

        Vector3 handPos = userHandTransform.position;
        float distanceToCenter = Vector3.Distance(handPos, gridCenterAnchor.position);

        // Bound tracking to the clinical Reachable Workspace sphere limit
        if (distanceToCenter <= maxTrackingRadius)
        {
            // Convert absolute world coordinate space into a quantized discrete grid index
            Vector3Int gridIndex = WorldToGridPosition(handPos);

            // Register or update the voxel space data
            if (!_activeVoxels.ContainsKey(gridIndex))
            {
                VoxelData newData = new VoxelData
                {
                    quadrant = CalculateQuadrant(handPos - gridCenterAnchor.position)
                };
                _activeVoxels.Add(gridIndex, newData);
                _meshDirty = true; // Flag that geometry must be rebuilt
            }

            // Track continuous residence time within this cubic cell volume
            _activeVoxels[gridIndex].timeSpent += Time.deltaTime;
        }

        // Rebuild mesh geometry smoothly if structural values changed
        if (_meshDirty)
        {
            RebuildGridMesh();
            _meshDirty = false;
        }
    }

    private Vector3Int WorldToGridPosition(Vector3 worldPos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(worldPos.x / cellSize),
            Mathf.FloorToInt(worldPos.y / cellSize),
            Mathf.FloorToInt(worldPos.z / cellSize)
        );
    }

    private Vector3 GridToWorldPosition(Vector3Int gridPos)
    {
        return new Vector3(
            gridPos.x * cellSize + (cellSize * 0.5f),
            gridPos.y * cellSize + (cellSize * 0.5f),
            gridPos.z * cellSize + (cellSize * 0.5f)
        );
    }

    private int CalculateQuadrant(Vector3 localPos)
    {
        bool isUpper = localPos.y > 0;
        bool isRight = localPos.x > 0;

        if (isUpper && isRight) return 0;  // Q1
        if (!isUpper && isRight) return 1; // Q2
        if (isUpper && !isRight) return 2; // Q3
        return 3;                          // Q4
    }

    private Color GetQuadrantColor(int quadrant)
    {
        // Custom aesthetic palette matching standard clinical visual splits
        return quadrant switch
        {
            0 => new Color(1f, 0.2f, 0.2f, 0.4f), // Q1: Translucent Soft Red
            1 => new Color(0.2f, 0.4f, 1f, 0.4f), // Q2: Translucent Soft Blue
            2 => new Color(0.2f, 1f, 0.4f, 0.4f), // Q3: Translucent Soft Green
            3 => new Color(1f, 0.8f, 0.2f, 0.4f), // Q4: Translucent Soft Amber
            _ => Color.white
        };
    }

    private void RebuildGridMesh()
    {
        _vertices.Clear();
        _triangles.Clear();
        _colors.Clear();

        // Direction offsets to generate cube face vertices sequentially
        Vector3[] faceOffsets = {
            new Vector3(0,0,1), new Vector3(1,0,0), new Vector3(0,0,-1),
            new Vector3(-1,0,0), new Vector3(0,1,0), new Vector3(0,-1,0)
        };

        foreach (var voxel in _activeVoxels)
        {
            Vector3 centerPos = GridToWorldPosition(voxel.Key);
            Color quadColor = GetQuadrantColor(voxel.Value.quadrant);
            float h = cellSize * 0.5f;

            // Generate structural box data around voxel center
            Vector3[] cVerts = new Vector3[] {
                centerPos + new Vector3(-h, -h, -h), centerPos + new Vector3(h, -h, -h),
                centerPos + new Vector3(h, h, -h),   centerPos + new Vector3(-h, h, -h),
                centerPos + new Vector3(-h, -h, h),  centerPos + new Vector3(h, -h, h),
                centerPos + new Vector3(h, h, h),    centerPos + new Vector3(-h, h, h)
            };

            // Build individual quad faces sequentially
            BuildFace(cVerts[0], cVerts[1], cVerts[2], cVerts[3], quadColor); // Front
            BuildFace(cVerts[1], cVerts[5], cVerts[6], cVerts[2], quadColor); // Right
            BuildFace(cVerts[5], cVerts[4], cVerts[7], cVerts[6], quadColor); // Back
            BuildFace(cVerts[4], cVerts[0], cVerts[3], cVerts[7], quadColor); // Left
            BuildFace(cVerts[3], cVerts[2], cVerts[6], cVerts[7], quadColor); // Top
            BuildFace(cVerts[4], cVerts[5], cVerts[1], cVerts[0], quadColor); // Bottom
        }

        _proceduralMesh.Clear();
        _proceduralMesh.SetVertices(_vertices);
        _proceduralMesh.SetTriangles(_triangles, 0);
        _proceduralMesh.SetColors(_colors);
        _proceduralMesh.RecalculateNormals();
    }

    private void BuildFace(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Color color)
    {
        int vCount = _vertices.Count;

        _vertices.Add(v0); _vertices.Add(v1); _vertices.Add(v2); _vertices.Add(v3);
        _colors.Add(color); _colors.Add(color); _colors.Add(color); _colors.Add(color);

        // First triangle of quad face
        _triangles.Add(vCount); _triangles.Add(vCount + 2); _triangles.Add(vCount + 1);
        // Second triangle of quad face
        _triangles.Add(vCount); _triangles.Add(vCount + 3); _triangles.Add(vCount + 2);
    }

    public int GetActiveVoxelCount() => _activeVoxels.Count;
    
    public void ResetGridVolume()
    {
        _activeVoxels.Clear();
        _meshDirty = true;
    }
}