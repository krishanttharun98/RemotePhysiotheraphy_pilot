using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using UnityEngine.XR;

public class BubbleTrajectoryReplayManager : MonoBehaviour
{
    [Header("Core Reference Components")]
    public SolidSphereFollower solidSphereFollower;
    public GameObject yellowHistoryPrefab;
    public Transform leftHand;
    public Transform rightHand;

    [Header("Spatial Alignment Pointer")]
    public Transform placementPointer;
    public float placementDistance = 1.2f;

    [Header("Lightweight Performance Placement Visualizer")]
    [Tooltip("Assign a Cylinder GameObject here to act as the center anchor point visualization")]
    public GameObject placementCylinderGizmo;

    [Header("UI Synchronized Phase Buttons")]
    public Button phase1Button;
    public Button phase2Button;
    public Button phase3FrontButton;
    public Button phase3RearButton;
    public Button phase4Button;

    private class BubblePopSnapshot
    {
        public Vector3 localPopPos;
        public List<Vector3> localHandPathTrajectory;
        public float bubbleScale; // Stores the exact visual dimensions of the popped target
        public float recordedHeadsetHeight; // Tracks original elevation relative to floor
    }

    private struct TrackedBubbleMetadata
    {
        public Vector3 position;
        public int phaseIndex;
    }

    private List<BubblePopSnapshot>[] _phaseHistoryDatabase = new List<BubblePopSnapshot>[6];
    private List<Vector3> _leftHandLivePath = new List<Vector3>();
    private List<Vector3> _rightHandLivePath = new List<Vector3>();
    private Dictionary<Transform, TrackedBubbleMetadata> _trackedActiveBubbles = new Dictionary<Transform, TrackedBubbleMetadata>();

    // Reflection cache variables to ensure absolute isolation from core script files
    private FieldRef<int> _currentPhaseRef;
    private FieldRef<bool> _isPhase3RearRef;
    private FieldRef<bool> _isTestRunningRef;

    private int _lastFramePhase = -1;
    private bool _lastFrameRear = false;
    private int _currentActiveReviewPhase = -1;
    private List<GameObject> _spawnedVisualHistoryPoints = new List<GameObject>();

    // Grid System Transform Anchors
    private Vector3 _trackingCenterWorldPos;
    private Quaternion _trackingCenterWorldRot = Quaternion.identity;
    
    private Vector3 _displayGridWorldPos;
    private Quaternion _displayGridWorldRot = Quaternion.identity;
    private bool _isPlacingHistoryGrid = true;

    delegate T FieldRef<T>();

    void Start()
    {
        for (int i = 1; i <= 5; i++) _phaseHistoryDatabase[i] = new List<BubblePopSnapshot>();
        
        if (solidSphereFollower == null) solidSphereFollower = FindObjectOfType<SolidSphereFollower>();
        SetupReflectionAccessors();
        LinkUiButtonListeners();

        // Ensure the cylinder center tracker is visible from the very start
        if (placementCylinderGizmo != null) placementCylinderGizmo.SetActive(true);

        // Establish the fixed patient tracking base right at startup
        if (solidSphereFollower != null && solidSphereFollower.headset != null)
        {
            _trackingCenterWorldPos = solidSphereFollower.headset.position;
            _trackingCenterWorldRot = Quaternion.Euler(0, solidSphereFollower.headset.eulerAngles.y, 0);
        }
        else
        {
            _trackingCenterWorldPos = Vector3.zero;
        }
    }

    void SetupReflectionAccessors()
    {
        if (solidSphereFollower == null) return;

        FieldInfo phaseField = typeof(SolidSphereFollower).GetField("_currentPhaseNet", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo rearField = typeof(SolidSphereFollower).GetField("_isPhase3RearPartNet", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo runningField = typeof(SolidSphereFollower).GetField("_isTestRunningNet", BindingFlags.NonPublic | BindingFlags.Instance);

        if (phaseField != null) {
            var netVar = phaseField.GetValue(solidSphereFollower) as NetworkVariable<int>;
            _currentPhaseRef = () => netVar.Value;
        }
        if (rearField != null) {
            var netVar = rearField.GetValue(solidSphereFollower) as NetworkVariable<bool>;
            _isPhase3RearRef = () => netVar.Value;
        }
        if (runningField != null) {
            var netVar = runningField.GetValue(solidSphereFollower) as NetworkVariable<bool>;
            _isTestRunningRef = () => netVar.Value;
        }
    }

    void LinkUiButtonListeners()
    {
        if (phase1Button != null) phase1Button.onClick.AddListener(() => ToggleTrajectoryReviewMode(1));
        if (phase2Button != null) phase2Button.onClick.AddListener(() => ToggleTrajectoryReviewMode(2));
        if (phase3FrontButton != null) phase3FrontButton.onClick.AddListener(() => ToggleTrajectoryReviewMode(3));
        if (phase3RearButton != null) phase3RearButton.onClick.AddListener(() => ToggleTrajectoryReviewMode(4));
        if (phase4Button != null) phase4Button.onClick.AddListener(() => ToggleTrajectoryReviewMode(5));
    }

    void Update()
    {
        HandleGridPlacementInput();

        bool isTestRunning = _isTestRunningRef != null ? _isTestRunningRef() : false;

        // If the test isn't running and we have no more bubbles to clean up into history, stop here
        if (!isTestRunning && _trackedActiveBubbles.Count == 0) return;

        int livePhase = _currentPhaseRef != null ? _currentPhaseRef() : 0;
        bool liveRear = _isPhase3RearRef != null ? _isPhase3RearRef() : false;

        if (livePhase != _lastFramePhase || liveRear != _lastFrameRear)
        {
            _leftHandLivePath.Clear();
            _rightHandLivePath.Clear();
            _trackedActiveBubbles.Clear();
            
            if (solidSphereFollower.headset != null)
            {
                _trackingCenterWorldPos = solidSphereFollower.headset.position;
                _trackingCenterWorldRot = Quaternion.Euler(0, solidSphereFollower.headset.eulerAngles.y, 0);
            }
            
            _lastFramePhase = livePhase;
            _lastFrameRear = liveRear;
        }

        if (leftHand != null) _leftHandLivePath.Add(leftHand.position);
        if (rightHand != null) _rightHandLivePath.Add(rightHand.position);

        foreach (Transform child in solidSphereFollower.transform)
        {
            if (child.name.StartsWith("Bubble_") && !_trackedActiveBubbles.ContainsKey(child))
            {
                int bubbleTrackingIndex = (livePhase == 3 && liveRear) ? 4 : (livePhase == 4 ? 5 : livePhase);
                _trackedActiveBubbles.Add(child, new TrackedBubbleMetadata { position = child.position, phaseIndex = bubbleTrackingIndex });
            }
        }

        List<Transform> missingBubbles = new List<Transform>();
        foreach (var pair in _trackedActiveBubbles)
        {
            if (pair.Key == null)
            {
                missingBubbles.Add(pair.Key);
                ProcessBubblePopEvent(pair.Value.position, pair.Value.phaseIndex);
            }
        }

        foreach (var trans in missingBubbles) _trackedActiveBubbles.Remove(trans);
    }

    void HandleGridPlacementInput()
    {
        if (_isPlacingHistoryGrid)
        {
            if (placementPointer != null)
            {
                _displayGridWorldPos = placementPointer.position + (placementPointer.forward * placementDistance);
                
                if (solidSphereFollower != null && solidSphereFollower.headset != null)
                {
                    Vector3 lookDir = _displayGridWorldPos - solidSphereFollower.headset.position;
                    lookDir.y = 0;
                    if (lookDir != Vector3.zero) _displayGridWorldRot = Quaternion.LookRotation(lookDir.normalized);
                }
            }

            // Sync the cylinder tracker position in real time during placement mode
            if (placementCylinderGizmo != null)
            {
                placementCylinderGizmo.transform.position = _displayGridWorldPos;
                placementCylinderGizmo.transform.rotation = _displayGridWorldRot;
            }
        }

        // Detect Button B (Right) or Button X (Left) using OVRInput to lock placement anchor
        if (OVRInput.GetDown(OVRInput.RawButton.B) || OVRInput.GetDown(OVRInput.RawButton.X))
        {
            _isPlacingHistoryGrid = !_isPlacingHistoryGrid; 
            
            // Retain cylinder visibility permanently as the structural anchor center point
            if (placementCylinderGizmo != null)
            {
                placementCylinderGizmo.SetActive(true);
            }
            
            Debug.Log($"[Bubble History Grid] Spatial placement locked state toggled. IsPlacing: {_isPlacingHistoryGrid}");
        }
    }

    void ProcessBubblePopEvent(Vector3 lastKnownBubblePos, int bubblePhaseIndex)
    {
        if (leftHand == null || rightHand == null || solidSphereFollower == null) return;

        // Use core hardware controller positions for direct intersection mapping across all phase data
        Vector3 checkPosL = leftHand.position;
        Vector3 checkPosR = rightHand.position;

        float distLeft = Vector3.Distance(lastKnownBubblePos, checkPosL);
        float distRight = Vector3.Distance(lastKnownBubblePos, checkPosR);
        
        float bubbleScale = (bubblePhaseIndex == 3 || bubblePhaseIndex == 4) ? (solidSphereFollower.targetVisualSize * 0.35f) : solidSphereFollower.targetVisualSize;
        float interactThreshold = bubbleScale * 2.2f;

        bool poppedByLeft = distLeft < distRight && distLeft < interactThreshold;
        bool poppedByRight = distRight <= distLeft && distRight < interactThreshold;

        if (poppedByLeft || poppedByRight)
        {
            BubblePopSnapshot snapshot = new BubblePopSnapshot();
            snapshot.bubbleScale = bubbleScale; 

            Vector3 instantaneousCenterPos = solidSphereFollower.transform.position;
            Quaternion instantaneousCenterRot = Quaternion.identity;
            
            if (solidSphereFollower.headset != null)
            {
                instantaneousCenterRot = Quaternion.Euler(0, solidSphereFollower.headset.eulerAngles.y, 0);
                snapshot.recordedHeadsetHeight = solidSphereFollower.headset.position.y;
            }
            
            snapshot.localPopPos = Quaternion.Inverse(instantaneousCenterRot) * (lastKnownBubblePos - instantaneousCenterPos);
            
            List<Vector3> activeWorldPath = poppedByRight ? _rightHandLivePath : _leftHandLivePath;
            snapshot.localHandPathTrajectory = new List<Vector3>();

            foreach (Vector3 worldPt in activeWorldPath)
            {
                Vector3 localPt = Quaternion.Inverse(instantaneousCenterRot) * (worldPt - instantaneousCenterPos);
                snapshot.localHandPathTrajectory.Add(localPt);
            }
            
            _phaseHistoryDatabase[bubblePhaseIndex].Add(snapshot);

            _leftHandLivePath.Clear();
            _rightHandLivePath.Clear();
        }
    }

    void ToggleTrajectoryReviewMode(int targetPhaseIndex)
    {
        if (_currentActiveReviewPhase == targetPhaseIndex)
        {
            ClearSpawnedHistoryPoints();
            _currentActiveReviewPhase = -1;
            return;
        }

        ClearSpawnedHistoryPoints();
        _currentActiveReviewPhase = targetPhaseIndex;

        List<BubblePopSnapshot> targetHistory = _phaseHistoryDatabase[targetPhaseIndex];
        foreach (var popData in targetHistory)
        {
            // Reconstruct historical points completely relative to the cylinder workspace base coordinates
            Vector3 anchorPos = new Vector3(_displayGridWorldPos.x, popData.recordedHeadsetHeight, _displayGridWorldPos.z);
            
            Vector3 finalDisplayWorldPos = (_displayGridWorldRot * popData.localPopPos) + anchorPos;
            
            GameObject yellowPoint = Instantiate(yellowHistoryPrefab, finalDisplayWorldPos, Quaternion.identity);
            yellowPoint.transform.SetParent(this.transform, true);
            
            yellowPoint.transform.localScale = Vector3.one * popData.bubbleScale;
            
            List<Vector3> displayWorldTrajectoryPath = new List<Vector3>();
            foreach (Vector3 localPt in popData.localHandPathTrajectory)
            {
                Vector3 finalPathWorldPos = (_displayGridWorldRot * localPt) + anchorPos;
                displayWorldTrajectoryPath.Add(finalPathWorldPos);
            }

            InteractiveHistoryPoint pointScript = yellowPoint.GetComponent<InteractiveHistoryPoint>();
            if (pointScript != null)
            {
                pointScript.Initialize(displayWorldTrajectoryPath, leftHand, rightHand);
            }
            
            _spawnedVisualHistoryPoints.Add(yellowPoint);
        }
    }

    void ClearSpawnedHistoryPoints()
    {
        foreach (GameObject pt in _spawnedVisualHistoryPoints)
        {
            if (pt != null) Destroy(pt);
        }
        _spawnedVisualHistoryPoints.Clear();
    }
}