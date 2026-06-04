using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RWSCombinedMatrixMesh : MonoBehaviour
{
    public enum VoxelState { Unvisited, LeftHand, RightHand, Both }

    [Header("Tracking References")]
    public Transform leftHandTransform;
    public Transform rightHandTransform;
    [Tooltip("Assign your Headset or Avatar Torso here. The grid follows it but remains straight.")]
    public Transform positionAnchor;

    [Header("Matrix Grid Dimensions")]
    public int gridWidth = 12;     // X axis
    public int gridHeight = 12;    // Y axis
    public int gridDepth = 6;      // Z axis
    public float cellSize = 0.08f;  // 8cm resolution per voxel block
    public Material matrixMaterial;

    [Header("Interaction State Colors")]
    private Color colorInvisible = new Color(0f, 0f, 0f, 0f);
    public Color colorLeftHand = new Color(1f, 0.08f, 0.57f, 0.85f);   // Hot Pink
    public Color colorRightHand = new Color(0f, 0.9f, 1f, 0.85f);       // Electric Cyan
    public Color colorBothHands = new Color(0.6f, 0.2f, 1f, 0.95f);     // Vibrant Purple

    [Header("Intensity Settings")]
    [Tooltip("How fast the color reaches maximum intensity per frame of interaction.")]
    [Range(0.1f, 5f)] public float interactionSpeed = 2.0f;

    // Core Data Matrices
    private VoxelState[,,] _matrixStates;
    
    // Intensity Tracking Matrices (0.0 to 1.0)
    private float[,,] _leftIntensity;
    private float[,,] _rightIntensity;
    
    // Procedural Mesh components
    private MeshFilter _containerFilter;
    private MeshRenderer _containerRenderer;
    private Mesh _proceduralMesh;
    private GameObject _matrixContainer;

    // Geometric Arrays
    private Vector3[] _vertices;
    private int[] _triangles;
    private Color[] _colors;
    private bool _meshDirty = false;

    // Offset anchors
    private float _offsetX;
    private float _offsetY;
    private float _offsetZ = 0.15f; 

    void Start()
    {
        _offsetX = (gridWidth - 1) * cellSize * 0.5f;
        _offsetY = (gridHeight - 1) * cellSize * 0.5f;

        InitializeDataStructures();
        GenerateProceduralMatrixMesh();
    }

    void InitializeDataStructures()
    {
        _matrixStates = new VoxelState[gridWidth, gridHeight, gridDepth];
        _leftIntensity = new float[gridWidth, gridHeight, gridDepth];
        _rightIntensity = new float[gridWidth, gridHeight, gridDepth];

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                for (int z = 0; z < gridDepth; z++)
                {
                    _matrixStates[x, y, z] = VoxelState.Unvisited; 
                    _leftIntensity[x, y, z] = 0f;
                    _rightIntensity[x, y, z] = 0f;
                }
            }
        }
    }

    void GenerateProceduralMatrixMesh()
    {
        _matrixContainer = new GameObject("RWS_ProceduralMatrix_Container");
        _matrixContainer.transform.SetParent(transform, false);
        
        _containerFilter = _matrixContainer.AddComponent<MeshFilter>();
        _containerRenderer = _matrixContainer.AddComponent<MeshRenderer>();
        _containerRenderer.material = matrixMaterial;

        _proceduralMesh = new Mesh();
        _proceduralMesh.name = "RWS_ActiveVoxels_Mesh";
        _proceduralMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; 
        _containerFilter.mesh = _proceduralMesh;

        int totalVoxels = gridWidth * gridHeight * gridDepth;
        
        _vertices = new Vector3[totalVoxels * 24];
        _triangles = new int[totalVoxels * 36];
        _colors = new Color[totalVoxels * 24];

        int vIndex = 0;
        int tIndex = 0;
        float h = cellSize * 0.5f;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                for (int z = 0; z < gridDepth; z++)
                {
                    Vector3 center = CalculateLocalVoxelCenter(x, y, z);
                    Color initialColor = colorInvisible;

                    Vector3 c0 = center + new Vector3(-h, -h, -h);
                    Vector3 c1 = center + new Vector3(h, -h, -h);
                    Vector3 c2 = center + new Vector3(h, h, -h);
                    Vector3 c3 = center + new Vector3(-h, h, -h);
                    Vector3 c4 = center + new Vector3(-h, -h, h);
                    Vector3 c5 = center + new Vector3(h, -h, h);
                    Vector3 c6 = center + new Vector3(h, h, h);
                    Vector3 c7 = center + new Vector3(-h, h, h);

                    BuildFace(c0, c1, c2, c3, initialColor, ref vIndex, ref tIndex); // Front
                    BuildFace(c1, c5, c6, c2, initialColor, ref vIndex, ref tIndex); // Right
                    BuildFace(c5, c4, c7, c6, initialColor, ref vIndex, ref tIndex); // Back
                    BuildFace(c4, c0, c3, c7, initialColor, ref vIndex, ref tIndex); // Left
                    BuildFace(c3, c2, c6, c7, initialColor, ref vIndex, ref tIndex); // Top
                    BuildFace(c4, c5, c1, c0, initialColor, ref vIndex, ref tIndex); // Bottom
                }
            }
        }

        _proceduralMesh.vertices = _vertices;
        _proceduralMesh.triangles = _triangles;
        _proceduralMesh.colors = _colors;
        _proceduralMesh.RecalculateNormals();
    }

    void BuildFace(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Color color, ref int vIdx, ref int tIdx)
    {
        _vertices[vIdx] = v0; _vertices[vIdx + 1] = v1; _vertices[vIdx + 2] = v2; _vertices[vIdx + 3] = v3;
        _colors[vIdx] = color; _colors[vIdx + 1] = color; _colors[vIdx + 2] = color; _colors[vIdx + 3] = color;

        _triangles[tIdx]     = vIdx; _triangles[tIdx + 1] = vIdx + 2; _triangles[tIdx + 2] = vIdx + 1;
        _triangles[tIdx + 3] = vIdx; _triangles[tIdx + 4] = vIdx + 3; _triangles[tIdx + 5] = vIdx + 2;

        vIdx += 4;
        tIdx += 6;
    }

    void Update()
    {
        if (positionAnchor == null) return;

        _matrixContainer.transform.position = positionAnchor.position;
        _matrixContainer.transform.rotation = Quaternion.identity;

        EvaluateHandTracking(leftHandTransform, true);
        EvaluateHandTracking(rightHandTransform, false);

        if (_meshDirty)
        {
            _proceduralMesh.colors = _colors;
            _meshDirty = false;
        }
    }

    void EvaluateHandTracking(Transform hand, bool isLeftHand)
    {
        if (hand == null) return;

        Vector3 localHandPos = _matrixContainer.transform.InverseTransformPoint(hand.position);

        int x = Mathf.RoundToInt((localHandPos.x + _offsetX) / cellSize);
        int y = Mathf.RoundToInt((localHandPos.y + _offsetY) / cellSize);
        int z = Mathf.RoundToInt((localHandPos.z - _offsetZ) / cellSize);

        if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight && z >= 0 && z < gridDepth)
        {
            // Increase intensity based on frame delta time and your speed modifier
            float deltaIntensity = Time.deltaTime * interactionSpeed;

            if (isLeftHand)
            {
                _leftIntensity[x, y, z] = Mathf.Clamp01(_leftIntensity[x, y, z] + deltaIntensity);
            }
            else
            {
                _rightIntensity[x, y, z] = Mathf.Clamp01(_rightIntensity[x, y, z] + deltaIntensity);
            }

            // Dynamically recalculate state and color density
            RecalculateVoxelColor(x, y, z);
        }
    }

    void RecalculateVoxelColor(int x, int y, int z)
    {
        float left = _leftIntensity[x, y, z];
        float right = _rightIntensity[x, y, z];

        // Update structural state representation
        if (left > 0 && right > 0) _matrixStates[x, y, z] = VoxelState.Both;
        else if (left > 0) _matrixStates[x, y, z] = VoxelState.LeftHand;
        else if (right > 0) _matrixStates[x, y, z] = VoxelState.RightHand;
        else _matrixStates[x, y, z] = VoxelState.Unvisited;

        Color finalColor = colorInvisible;

        if (left > 0 && right == 0)
        {
            // Only Left Hand: Scale baseline color by left intensity factor
            finalColor = Color.Lerp(colorInvisible, colorLeftHand, left);
        }
        else if (right > 0 && left == 0)
        {
            // Only Right Hand: Scale baseline color by right intensity factor
            finalColor = Color.Lerp(colorInvisible, colorRightHand, right);
        }
        else if (left > 0 && right > 0)
        {
            // Both Hands Overlapping: Determine base mixture weight, then scale total visibility density
            float totalWeight = left + right;
            Color mixedTarget = Color.Lerp(colorLeftHand, colorRightHand, right / totalWeight);
            
            // Overlapping interactions force it towards the combined purple state at high intensity
            float overlapFactor = Mathf.Min(left, right);
            Color baseColor = Color.Lerp(mixedTarget, colorBothHands, overlapFactor);

            finalColor = Color.Lerp(colorInvisible, baseColor, Mathf.Max(left, right));
        }

        // Apply to mesh arrays
        int totalVoxelsPassedBefore = (x * gridHeight * gridDepth) + (y * gridDepth) + z;
        int vertexStartOffset = totalVoxelsPassedBefore * 24;

        for (int i = 0; i < 24; i++)
        {
            _colors[vertexStartOffset + i] = finalColor;
        }

        _meshDirty = true;
    }

    Vector3 CalculateLocalVoxelCenter(int x, int y, int z)
    {
        return new Vector3(
            (x * cellSize) - _offsetX,
            (y * cellSize) - _offsetY,
            (z * cellSize) + _offsetZ
        );
    }

    public void ResetMatrixGrid()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                for (int z = 0; z < gridDepth; z++)
                {
                    _matrixStates[x, y, z] = VoxelState.Unvisited;
                    _leftIntensity[x, y, z] = 0f;
                    _rightIntensity[x, y, z] = 0f;
                    
                    int totalVoxelsPassedBefore = (x * gridHeight * gridDepth) + (y * gridDepth) + z;
                    int vertexStartOffset = totalVoxelsPassedBefore * 24;

                    for (int i = 0; i < 24; i++)
                    {
                        _colors[vertexStartOffset + i] = colorInvisible;
                    }
                }
            }
        }
        _meshDirty = true;
    }
}