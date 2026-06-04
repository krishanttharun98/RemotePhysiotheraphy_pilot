using UnityEngine;
using System.Collections.Generic;

public class RWSHeatmapManager : MonoBehaviour
{
    public enum VoxelState { Unvisited, LeftHand, RightHand, Both }

    [System.Serializable]
    public class VoxelTrackingPoint
    {
        public VoxelState state = VoxelState.Unvisited;
        public Vector3 localPosition;
    }

    [Header("Resolution Settings")]
    public float resolutionSpacing = 0.06f; // Voxel cell node precision side limits
    public float sphereVisualSize = 0.025f;  
    public Material matrixMaterial;

    [Header("Heatmap State Parent Holders")]
    [Tooltip("Create and assign 5 empty child GameObjects to act as containers for each phase layout.")]
    public GameObject[] phaseContainers = new GameObject[5]; 

    // Internal Dictionary matrix mapped uniquely for 5 separate phases
    private Dictionary<Vector3Int, VoxelTrackingPoint>[] _phaseMatrices = new Dictionary<Vector3Int, VoxelTrackingPoint>[5];
    private List<GameObject> _spawnedVisualSpheres = new List<GameObject>();

    // Visual Color Profiles
    private readonly Color ColorUnvisited = new Color(0.4f, 0.4f, 0.4f, 0.1f);
    private readonly Color ColorLeftHand = new Color(1f, 0.08f, 0.57f, 0.8f);   // Pink
    private readonly Color ColorRightHand = new Color(0f, 0.9f, 1f, 0.8f);       // Cyan
    private readonly Color ColorBoth = new Color(0.6f, 0.2f, 1f, 0.9f);          // Purple

    void Awake()
    {
        for (int i = 0; i < 5; i++)
        {
            _phaseMatrices[i] = new Dictionary<Vector3Int, VoxelTrackingPoint>();
            if (phaseContainers[i] != null) phaseContainers[i].SetActive(false); // Hide until button choice
        }
    }

    public void RecordHandData(int phaseIdx, Transform leftHand, Transform rightHand)
    {
        if (phaseIdx < 0 || phaseIdx >= 5) return;

        EvaluateSingleHand(phaseIdx, leftHand, true);
        EvaluateSingleHand(phaseIdx, rightHand, false);
    }

    void EvaluateSingleHand(int phaseIdx, Transform hand, bool isLeft)
    {
        if (hand == null) return;

        // Convert world coordinates relative to this manager's center context anchor frame
        Vector3 localPos = transform.InverseTransformPoint(hand.position);
        Vector3Int gridIndex = new Vector3Int(
            Mathf.RoundToInt(localPos.x / resolutionSpacing),
            Mathf.RoundToInt(localPos.y / resolutionSpacing),
            Mathf.RoundToInt(localPos.z / resolutionSpacing)
        );

        if (!_phaseMatrices[phaseIdx].ContainsKey(gridIndex))
        {
            _phaseMatrices[phaseIdx][gridIndex] = new VoxelTrackingPoint {
                state = VoxelState.Unvisited,
                localPosition = new Vector3(gridIndex.x * resolutionSpacing, gridIndex.y * resolutionSpacing, gridIndex.z * resolutionSpacing)
            };
        }

        VoxelState currentState = _phaseMatrices[phaseIdx][gridIndex].state;
        if (isLeft)
        {
            if (currentState == VoxelState.Unvisited) _phaseMatrices[phaseIdx][gridIndex].state = VoxelState.LeftHand;
            else if (currentState == VoxelState.RightHand) _phaseMatrices[phaseIdx][gridIndex].state = VoxelState.Both;
        }
        else
        {
            if (currentState == VoxelState.Unvisited) _phaseMatrices[phaseIdx][gridIndex].state = VoxelState.RightHand;
            else if (currentState == VoxelState.LeftHand) _phaseMatrices[phaseIdx][gridIndex].state = VoxelState.Both;
        }
    }

    public void BakeAllHeatmaps()
    {
        for (int p = 0; p < 5; p++)
        {
            if (phaseContainers[p] == null) continue;

            foreach (var kvp in _phaseMatrices[p])
            {
                // Only spawn visible visual mesh indicator nodes for areas touched by the patient
                if (kvp.Value.state == VoxelState.Unvisited) continue;

                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.SetParent(phaseContainers[p].transform, false);
                sphere.transform.localPosition = kvp.Value.localPosition;
                sphere.transform.localScale = Vector3.one * sphereVisualSize;

                Destroy(sphere.GetComponent<Collider>()); // Strip physical body profiles immediately

                Renderer rend = sphere.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.material = matrixMaterial;
                    rend.material.color = kvp.Value.state switch {
                        VoxelState.LeftHand => ColorLeftHand,
                        VoxelState.RightHand => ColorRightHand,
                        VoxelState.Both => ColorBoth,
                        _ => ColorUnvisited
                    };
                }
            }
        }
        Debug.Log("3D Heatmaps baked successfully into all phase container targets.");
    }

    // High-performance clean entry function invoked straight from your drop-down GUI buttons
    public void SelectActiveHeatmapDisplay(int selectedPhase)
    {
        for (int i = 0; i < 5; i++)
        {
            if (phaseContainers[i] != null)
            {
                phaseContainers[i].SetActive(i == selectedPhase);
            }
        }
    }
}