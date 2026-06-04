using UnityEngine;
using System.Collections.Generic;

public class RWSCombinedMatrixGrid : MonoBehaviour
{
    public enum VoxelState { Unvisited, LeftHand, RightHand, Both }

    [Header("Tracking References")]
    public Transform leftHandTransform;
    public Transform rightHandTransform;
    [Tooltip("Assign your Headset or Avatar Torso here. The grid will translate with it, but ignore its rotation.")]
    public Transform positionAnchor; 

    [Header("Matrix Grid Dimensions")]
    public int gridWidth = 12;   // X axis (Left/Right)
    public int gridHeight = 12;  // Y axis (Up/Down)
    public int gridDepth = 6;    // Z axis (Forward Reach Depth)
    public float cellSize = 0.08f; // Size of each cubic voxel block (8cm resolution)
    public float sphereVisualSize = 0.03f; // Visual sphere scale
    public Material matrixSphereMaterial;

    [Header("Quadrant Color Themes")]
    // Upper Right (Q1)
    private readonly Color Q1_Unvisited = new Color(1f, 0.3f, 0.3f, 0.12f);
    // Lower Right (Q2)
    private readonly Color Q2_Unvisited = new Color(0.3f, 0.5f, 1f, 0.12f);
    // Upper Left (Q3)
    private readonly Color Q3_Unvisited = new Color(0.3f, 1f, 0.5f, 0.12f);
    // Lower Left (Q4)
    private readonly Color Q4_Unvisited = new Color(1f, 0.8f, 0.3f, 0.12f);

    [Header("Interaction State Colors")]
    public Color colorLeftHand = new Color(1f, 0.08f, 0.57f, 0.85f);   // Hot Pink
    public Color colorRightHand = new Color(0f, 0.9f, 1f, 0.85f);       // Electric Cyan
    public Color colorBothHands = new Color(0.6f, 0.2f, 1f, 0.95f);     // Vibrant Purple

    // Data Structures
    private Dictionary<Vector3Int, VoxelState> _matrixStates = new Dictionary<Vector3Int, VoxelState>();
    private Dictionary<Vector3Int, Renderer> _matrixRenderers = new Dictionary<Vector3Int, Renderer>();
    private Dictionary<Vector3Int, int> _matrixQuadrants = new Dictionary<Vector3Int, int>();
    private GameObject _matrixContainer;

    void Start()
    {
        GenerateStaticMatrix();
    }

    void GenerateStaticMatrix()
    {
        _matrixContainer = new GameObject("RWS_CombinedMatrix_Container");
        _matrixContainer.transform.SetParent(transform, false);

        // Calculate centering offsets so (0,0) sits directly in front of the center anchor point
        float offsetX = (gridWidth - 1) * cellSize * 0.5f;
        float offsetY = (gridHeight - 1) * cellSize * 0.5f;
        float offsetZ = 0.15f; // Starts 15cm in front of body plane to prevent view clipping

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

                    GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.name = $"VoxelNode_{x}_{y}_{z}";
                    sphere.transform.SetParent(_matrixContainer.transform, false);
                    sphere.transform.localPosition = localPos;
                    sphere.transform.localScale = Vector3.one * sphereVisualSize;

                    // Strip colliders instantly to optimize performance overhead
                    Collider sphereCollider = sphere.GetComponent<Collider>();
                    if (sphereCollider != null) DestroyImmediate(sphereCollider);

                    int quadrant = CalculateMatrixQuadrant(localPos);
                    Renderer rend = sphere.GetComponent<Renderer>();
                    rend.material = matrixSphereMaterial;
                    
                    // Assign default translucent quadrant color baseline
                    rend.material.color = GetUnvisitedQuadrantColor(quadrant);

                    _matrixStates.Add(index, VoxelState.Unvisited);
                    _matrixRenderers.Add(index, rend);
                    _matrixQuadrants.Add(index, quadrant);
                }
            }
        }
    }

    void Update()
    {
        if (positionAnchor == null) return;

        // CRUCIAL: Synchronize the translation position to follow the anchor, 
        // but completely clamp rotation to identity to prevent spinning when looking around!
        _matrixContainer.transform.position = positionAnchor.position;
        _matrixContainer.transform.rotation = Quaternion.identity;

        // Perform real-time sweep checks for both physical hands
        EvaluateHandTracking(leftHandTransform, true);
        EvaluateHandTracking(rightHandTransform, false);
    }

    void EvaluateHandTracking(Transform hand, bool isLeftHand)
    {
        if (hand == null) return;

        // Inverse transform the absolute world tracking coordinates down into our locked matrix grid container
        Vector3 localHandPos = _matrixContainer.transform.InverseTransformPoint(hand.position);

        float offsetX = (gridWidth - 1) * cellSize * 0.5f;
        float offsetY = (gridHeight - 1) * cellSize * 0.5f;
        float offsetZ = 0.15f;

        // Quantize position space values back safely to multi-dimensional coordinate bounds indices
        int x = Mathf.RoundToInt((localHandPos.x + offsetX) / cellSize);
        int y = Mathf.RoundToInt((localHandPos.y + offsetY) / cellSize);
        int z = Mathf.RoundToInt((localHandPos.z - offsetZ) / cellSize);

        Vector3Int targetIndex = new Vector3Int(x, y, z);

        // Grid bounds check protection
        if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight && z >= 0 && z < gridDepth)
        {
            VoxelState currentState = _matrixStates[targetIndex];

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
        _matrixStates[index] = newState;
        
        _matrixRenderers[index].material.color = newState switch
        {
            VoxelState.LeftHand => colorLeftHand,
            VoxelState.RightHand => colorRightHand,
            VoxelState.Both => colorBothHands,
            _ => GetUnvisitedQuadrantColor(_matrixQuadrants[index])
        };
    }

    int CalculateMatrixQuadrant(Vector3 localPos)
    {
        // Dissects the matrix space cleanly into 4 quadrants around local (0,0) matching clinical specs
        bool isUpper = localPos.y > 0;
        bool isRight = localPos.x > 0;

        if (isUpper && isRight) return 1;  // Upper Right
        if (!isUpper && isRight) return 2; // Lower Right
        if (isUpper && !isRight) return 3; // Upper Left
        return 4;                          // Lower Left
    }

    Color GetUnvisitedQuadrantColor(int quadrant)
    {
        return quadrant switch
        {
            1 => Q1_Unvisited, // Upper Right
            2 => Q2_Unvisited, // Lower Right
            3 => Q3_Unvisited, // Upper Left
            4 => Q4_Unvisited, // Lower Left
            _ => Color.gray
        };
    }

    public void ResetMatrixGrid()
    {
        foreach (var index in _matrixRenderers.Keys)
        {
            _matrixStates[index] = VoxelState.Unvisited;
            _matrixRenderers[index].material.color = GetUnvisitedQuadrantColor(_matrixQuadrants[index]);
        }
    }
}