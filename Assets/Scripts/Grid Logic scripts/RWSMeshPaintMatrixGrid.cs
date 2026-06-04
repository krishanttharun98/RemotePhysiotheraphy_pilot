using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RWSMeshPaintMatrixGrid : MonoBehaviour
{
    public enum VoxelState { Unvisited, LeftHand, RightHand, Both }

    [System.Serializable]
    public class VoxelNodeData
    {
        public VoxelState state = VoxelState.Unvisited;
        public int quadrant = 0;
    }

    [Header("Tracking References")]
    public Transform leftHandTransform;
    public Transform rightHandTransform;
    [Tooltip("Assign your Headset or Avatar Torso. The grid will follow its position but ignore its rotation completely.")]
    public Transform positionAnchor; 

    [Header("Matrix Grid Dimensions")]
    public int gridWidth = 12;     // X axis (Left/Right)
    public int gridHeight = 12;    // Y axis (Up/Down)
    public int gridDepth = 6;      // Z axis (Forward Reach Depth)
    public float cellSize = 0.08f; // Cubic size of each individual block cell (e.g. 8cm)
    public Material voxelMaterial; // Must use a shader that reads Vertex Colors (e.g., URP/Unlit)

    [Header("Quadrant Baseline Faint Colors (Unvisited)")]
    public Color colorQ1_UpperRight = new Color(1f, 0.3f, 0.3f, 0.12f); // Faint Red
    public Color colorQ2_LowerRight = new Color(0.3f, 0.5f, 1f, 0.12f); // Faint Blue
    public Color colorQ3_UpperLeft  = new Color(0.3f, 1f, 0.5f, 0.12f); // Faint Green
    public Color colorQ4_LowerLeft  = new Color(1f, 0.8f, 0.3f, 0.12f); // Faint Yellow

    [Header("Interaction Painting Colors")]
    public Color colorLeftHand = new Color(1f, 0.08f, 0.57f, 0.90f);   // Hot Pink
    public Color colorRightHand = new Color(0f, 0.9f, 1f, 0.90f);       // Electric Cyan
    public Color colorBothHands = new Color(0.6f, 0.2f, 1f, 1.00f);     // Vibrant Purple

    // Logic Architecture
    private Dictionary<Vector3Int, VoxelNodeData> _matrixGrid = new Dictionary<Vector3Int, VoxelNodeData>();
    
    // Procedural Mesh Buffers
    private MeshFilter _meshFilter;
    private Mesh _proceduralMesh;
    private List<Vector3> _vertices = new List<Vector3>();
    private List<int> _triangles = new List<int>();
    private List<Color> _colors = new List<Color>();
    private bool _meshDirty = false;

    void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        var meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = voxelMaterial;
        
        _proceduralMesh = new Mesh();
        _proceduralMesh.name = "RWS_MatrixVoxel_Mesh";
        _meshFilter.mesh = _proceduralMesh;
    }

    void Start()
    {
        InitializeVoxelMatrix();
    }

    void InitializeVoxelMatrix()
    {
        _matrixGrid.Clear();
        
        // Compute centering offsets so structural (0,0) centers right against the reference anchor transform orientation
        float offsetX = (gridWidth - 1) * cellSize * 0.5f;
        float offsetY = (gridHeight - 1) * cellSize * 0.5f;
        float offsetZ = 0.15f; // Pushes matrix 15cm forward to prevent face clipping with camera

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                for (int z = 0; z < gridDepth; z++)
                {
                    Vector3Int index = new Vector3Int(x, y, z);
                    
                    Vector3 localPos = new Vector3(
                        (x * cellSize) - offsetX,
                        (y * cellSize) - offsetY,
                        (z * cellSize) + offsetZ
                    );

                    VoxelNodeData voxel = new VoxelNodeData
                    {
                        state = VoxelState.Unvisited,
                        quadrant = CalculateMatrixQuadrant(localPos)
                    };

                    _matrixGrid.Add(index, voxel);
                }
            }
        }
        
        _meshDirty = true;
    }

    void Update()
    {
        if (positionAnchor == null) return;

        // Translation Only: Glide smoothly with your body coordinates, 
        // completely resetting rotation matrix to identity to remain forward stable.
        transform.position = positionAnchor.position;
        transform.rotation = Quaternion.identity;

        // Intercept tracking input points down inside the locked local mesh matrix transform space
        EvaluateHandTracking(leftHandTransform, true);
        EvaluateHandTracking(rightHandTransform, false);

        // Re-generate vertex array streams only when tracking states have mutated
        if (_meshDirty)
        {
            RebuildProceduralMesh();
            _meshDirty = false;
        }
    }

    void EvaluateHandTracking(Transform hand, bool isLeftHand)
    {
        if (hand == null) return;

        // Map absolute world tracking vectors to local matrix offset configurations
        Vector3 localHandPos = transform.InverseTransformPoint(hand.position);

        float offsetX = (gridWidth - 1) * cellSize * 0.5f;
        float offsetY = (gridHeight - 1) * cellSize * 0.5f;
        float offsetZ = 0.15f;

        // Quantize position mapping calculations back securely to integer coordinate indexes
        int x = Mathf.RoundToInt((localHandPos.x + offsetX) / cellSize);
        int y = Mathf.RoundToInt((localHandPos.y + offsetY) / cellSize);
        int z = Mathf.RoundToInt((localHandPos.z - offsetZ) / cellSize);

        Vector3Int targetIndex = new Vector3Int(x, y, z);

        if (_matrixGrid.TryGetValue(targetIndex, out VoxelNodeData voxel))
        {
            VoxelState currentState = voxel.state;

            if (isLeftHand)
            {
                if (currentState == VoxelState.Unvisited) UpdateVoxelState(targetIndex, VoxelState.LeftHand);
                else if (currentState == VoxelState.RightHand) UpdateVoxelState(targetIndex, VoxelState.Both);
            }
            else // Right Hand
            {
                if (currentState == VoxelState.Unvisited) UpdateVoxelState(targetIndex, VoxelState.RightHand);
                else if (currentState == VoxelState.LeftHand) UpdateVoxelState(targetIndex, VoxelState.Both);
            }
        }
    }

    void UpdateVoxelState(Vector3Int index, VoxelState newState)
    {
        _matrixGrid[index].state = newState;
        _meshDirty = true;
    }

    private void RebuildProceduralMesh()
    {
        _vertices.Clear();
        _triangles.Clear();
        _colors.Clear();

        float offsetX = (gridWidth - 1) * cellSize * 0.5f;
        float offsetY = (gridHeight - 1) * cellSize * 0.5f;
        float offsetZ = 0.15f;
        float h = cellSize * 0.5f;

        foreach (var cell in _matrixGrid)
        {
            Vector3Int idx = cell.Key;
            
            // Reconstruct the 3D center location vector of the voxel block
            Vector3 centerPos = new Vector3(
                (idx.x * cellSize) - offsetX,
                (idx.y * cellSize) - offsetY,
                (idx.z * cellSize) + offsetZ
            );

            Color nodePaintColor = GetNodeColorMap(cell.Value);

            // Construct local vector offset bounds for all 8 corners of the cube
            Vector3[] corners = new Vector3[] {
                centerPos + new Vector3(-h, -h, -h), centerPos + new Vector3(h, -h, -h),
                centerPos + new Vector3(h, h, -h),   centerPos + new Vector3(-h, h, -h),
                centerPos + new Vector3(-h, -h, h),  centerPos + new Vector3(h, -h, h),
                centerPos + new Vector3(h, h, h),    centerPos + new Vector3(-h, h, h)
            };

            // Build out all 6 outer solid polygon surfaces of the cubic mesh structure block
            AddCubeFace(corners[0], corners[1], corners[2], corners[3], nodePaintColor); // Front
            AddCubeFace(corners[1], corners[5], corners[6], corners[2], nodePaintColor); // Right
            AddCubeFace(corners[5], corners[4], corners[7], corners[6], nodePaintColor); // Back
            AddCubeFace(corners[4], corners[0], corners[3], corners[7], nodePaintColor); // Left
            AddCubeFace(corners[3], corners[2], corners[6], corners[7], nodePaintColor); // Top
            AddCubeFace(corners[4], corners[5], corners[1], corners[0], nodePaintColor); // Bottom
        }

        // Bake unified graphic buffer streams onto your object data framework
        _proceduralMesh.Clear();
        _proceduralMesh.SetVertices(_vertices);
        _proceduralMesh.SetTriangles(_triangles, 0);
        _proceduralMesh.SetColors(_colors);
        _proceduralMesh.RecalculateNormals();
    }

    private void AddCubeFace(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Color faceColor)
    {
        int vCount = _vertices.Count;

        _vertices.Add(v0); _vertices.Add(v1); _vertices.Add(v2); _vertices.Add(v3);
        _colors.Add(faceColor); _colors.Add(faceColor); _colors.Add(faceColor); _colors.Add(faceColor);

        _triangles.Add(vCount); _triangles.Add(vCount + 2); _triangles.Add(vCount + 1);
        _triangles.Add(vCount); _triangles.Add(vCount + 3); _triangles.Add(vCount + 2);
    }

    Color GetNodeColorMap(VoxelNodeData voxel)
    {
        return voxel.state switch
        {
            VoxelState.LeftHand => colorLeftHand,
            VoxelState.RightHand => colorRightHand,
            VoxelState.Both => colorBothHands,
            _ => GetUnvisitedQuadrantColor(voxel.quadrant)
        };
    }

    Color GetUnvisitedQuadrantColor(int quadrant)
    {
        return quadrant switch
        {
            1 => colorQ1_UpperRight,
            2 => colorQ2_LowerRight,
            3 => colorQ3_UpperLeft,
            4 => colorQ4_LowerLeft,
            _ => Color.gray
        };
    }

    int CalculateMatrixQuadrant(Vector3 localPos)
    {
        bool isUpper = localPos.y > 0;
        bool isRight = localPos.x > 0;

        if (isUpper && isRight) return 1;  // Upper Right (Q1)
        if (!isUpper && isRight) return 2; // Lower Right (Q2)
        if (isUpper && !isRight) return 3; // Upper Left (Q3)
        return 4;                          // Lower Left (Q4)
    }

    public void ResetMatrixGrid()
    {
        foreach (var voxel in _matrixGrid.Values)
        {
            voxel.state = VoxelState.Unvisited;
        }
        _meshDirty = true;
    }
}