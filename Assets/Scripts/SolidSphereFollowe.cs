using UnityEngine;
using System.Collections.Generic;
using System;
using Unity.Netcode; // Required for multiplayer synchronization

public class SolidSphereFollower : NetworkBehaviour
{
    [Header("Tracking")]
    public Transform headset; 
    public Transform leftHand;
    public Transform rightHand;

    [Header("Avatar Settings")]
    public GameObject movinAvatar;
    public Transform avatarHead; // Assign the Avatar's Head bone here
    public Transform avatarLeftHand; 
    public Transform avatarRightHand; 
    public AvatarSphereMirror avatarMirror;

    [Header("Structure")]
    public int longitudeSegments = 24; 
    public GameObject waterBubblePrefab; // Network Prefab replacing old sphere
    public Material[] quadrantMaterials;
    public Material transparentBackgroundMaterial;

    [Header("Clinical Settings")]
    public float noseBuffer = 0.05f;      
    public float phase4YOffset = -0.50f; 

    [Header("Clinical Phase Durations (Seconds)")]
    public float phase1Duration = 15f;
    public float phase2Duration = 15f;
    public float phase3FrontDuration = 15f;
    public float phase3RearDuration = 15f;
    public float phase4Duration = 15f;

    [Tooltip("Assign the Movable UIPanel parent object script here")]
    public VRCanvasController uiPanelController;

    [Header("Dynamic Scaling")]
    public float targetVisualSize = 0.045f;

    [Header("Avatar Overlay")]
    [Tooltip("Distance in front of the patient during Phase 3 rear mirror tasks.")]
    public float avatarForwardOffsetPhase3Rear = 1.0f;

    // --- Core State Variables (NetworkVariables to mirror state down to Client) ---
    private NetworkVariable<int> _currentPhaseNet = new NetworkVariable<int>(0);
    private NetworkVariable<bool> _isPhase3RearPartNet = new NetworkVariable<bool>(false);
    private NetworkVariable<float> _timerNet = new NetworkVariable<float>(0f);
    private NetworkVariable<bool> _isTestRunningNet = new NetworkVariable<bool>(true);

    private float _userReach;
    private float _calibratedNoseDistance = 0.15f; 
    private Vector3 _localNosePos; 
    private int _bubbleIdCounter = 0;

    private int[,] _totalSpheresPerPhase = new int[6, 4];
    private int[,,] _hitsByHand = new int[6, 4, 2]; 

    public VoxelGridManager voxelGridManager;

    private PatientDigitalTwinSync _digitalTwinSync;
    private readonly NetworkVariable<Vector3> _localNosePosNet = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    void Start()
    {
        // Tracking Hardware Auto-Discovery
        if (headset == null) headset = Camera.main != null ? Camera.main.transform : null;
        
        if (rightHand == null) {
            GameObject rHand = GameObject.Find("RightHandAnchor") ?? GameObject.Find("RightControllerAnchor");
            if (rHand != null) rightHand = rHand.transform;
        }
        if (leftHand == null) {
            GameObject lHand = GameObject.Find("LeftHandAnchor") ?? GameObject.Find("LeftControllerAnchor");
            if (lHand != null) leftHand = lHand.transform;
        }

        if (movinAvatar != null) movinAvatar.SetActive(false);

        ResolveAvatarReferences();
        
        if (headset != null && rightHand != null) {
            _userReach = Vector3.Distance(headset.position, rightHand.position);
        }
        if (_userReach < 0.2f) _userReach = 0.6f;
    }

    public override void OnNetworkSpawn()
    {
        _currentPhaseNet.OnValueChanged += OnPhaseOrRearChanged;
        _isPhase3RearPartNet.OnValueChanged += OnPhaseOrRearChanged;

        _digitalTwinSync = GetComponent<PatientDigitalTwinSync>();
        if (_digitalTwinSync == null)
        {
            _digitalTwinSync = gameObject.AddComponent<PatientDigitalTwinSync>();
        }

        ResolveAvatarReferences();
        _digitalTwinSync.BindAvatar(movinAvatar, avatarHead, avatarLeftHand, avatarRightHand);

        if (movinAvatar != null)
        {
            NetworkObject avatarNetworkObject = movinAvatar.GetComponent<NetworkObject>();
            if (avatarNetworkObject != null)
            {
                avatarNetworkObject.SynchronizeTransform = false;
            }
        }

        if (IsServer)
        {
            _currentPhaseNet.Value = 0;
            _isPhase3RearPartNet.Value = false;
            _timerNet.Value = 10f; 
            _isTestRunningNet.Value = true;
            SetAvatarPresentationActive(false);
        }
    }

    void Update()
    {
        if (!_isTestRunningNet.Value) return;

        UpdateAssessmentAnchorPosition();
        UpdateAvatarOverlay();

        if (ColocationRoleHelper.IsLocalPatient())
        {
            EvaluateProximityIntersections();
        }

        // --- HOST / SERVER ONLY TIMING LAYER ---
        if (!IsServer) return;

        if (!ColocationRoleHelper.IsLocalPatient())
        {
            return;
        }

        if (_currentPhaseNet.Value == 0)
        {
            HandleCalibration();
        }
        else
        {
            _timerNet.Value -= Time.deltaTime;
            if (_timerNet.Value <= 0)
            {
                AdvancePhase();
            }
        }
    }

    void HandleCalibration()
    {
        if (!ColocationRoleHelper.IsLocalPatient()) return;

        if (OVRInput.GetDown(OVRInput.RawButton.A) || Input.GetKeyDown(KeyCode.Space))
        {
            if (headset != null && rightHand != null) 
            {
                _calibratedNoseDistance = Vector3.Distance(headset.position, rightHand.position);
                _localNosePos = headset.InverseTransformPoint(rightHand.position);
                if (IsServer)
                {
                    _localNosePosNet.Value = _localNosePos;
                }
                if (voxelGridManager != null) 
                {
                    voxelGridManager.ResetAllVoxelData();
                    voxelGridManager.SynchronizeTrackingState(_currentPhaseNet.Value, _localNosePos);
                }
            }
            StartCoroutine(ShowCalibrationSuccess());
        }
    }

    System.Collections.IEnumerator ShowCalibrationSuccess()
    {
        if (IsServer) {
            yield return new WaitForSeconds(2f);
            AdvancePhase();
        }
    }

    private void OnPhaseOrRearChanged(int oldVal, int newVal) => BuildSetup();
    private void OnPhaseOrRearChanged(bool oldVal, bool newVal) => BuildSetup();

    public void BuildSetup()
    {
        // Safe backward deletion cleanup loop
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isEditor) DestroyImmediate(child);
            else Destroy(child);
        }
        
        // Safeguard avatar interaction if it's currently dormant
        if (avatarMirror != null && avatarMirror.gameObject.activeInHierarchy)
        {
            avatarMirror.ClearMirror();
        }

        // Network instantiation rules dictate targets are generated and pushed exclusively by Host/Server
        if (!IsServer) return;

        if (_currentPhaseNet.Value == 4) GenerateExtendedPhase4();
        else if (_currentPhaseNet.Value > 0) GenerateStandardSpheres();
    }

    void GenerateStandardSpheres()
    {
        float r = (_currentPhaseNet.Value == 3) ? (_calibratedNoseDistance + noseBuffer) :
                  (_currentPhaseNet.Value == 1 ? _userReach * 0.75f : _userReach);
        
        float finalScale = (_currentPhaseNet.Value == 3) ? (targetVisualSize * 0.35f) : targetVisualSize;

        for (int i = 0; i < longitudeSegments; i++)
        {
            for (int j = 0; j <= 12; j++)
            {
                float lonP = (float)i / longitudeSegments;
                float latP = (float)j / 12;
                
                Vector3 pos;
                if (_currentPhaseNet.Value == 3) {
                    float lonAdj = _isPhase3RearPartNet.Value ? (lonP * 0.5f + 0.25f) : (lonP * 0.5f + 0.75f);
                    pos = CalculateFullSpherePoint(lonAdj % 1.0f, latP, r);
                } else {
                    pos = CalculateFrontalPoint(lonP, latP, r);
                }
                SpawnWaterBubble(pos, GetStandardQuadrantIndex(lonP, latP), finalScale);
            }
        }
        CreatePhaseSpecificBackground(r);
    }

    void SpawnWaterBubble(Vector3 localPos, int qIndex, float scale)
    {
        if (!IsServer || waterBubblePrefab == null) return;

        Vector3 worldPos = transform.TransformPoint(localPos);
        GameObject bubble = Instantiate(waterBubblePrefab, worldPos, Quaternion.identity);
        bubble.transform.localScale = Vector3.one * scale;

        NetworkObject netObj = bubble.GetComponent<NetworkObject>();
        if (netObj != null) netObj.Spawn();

        bubble.transform.SetParent(this.transform, true);

        int id = _bubbleIdCounter++;
        bubble.name = $"Bubble_{id}";

        int trackingIdx = (_currentPhaseNet.Value == 3 && _isPhase3RearPartNet.Value) ? 5 : _currentPhaseNet.Value;
        _totalSpheresPerPhase[trackingIdx, qIndex]++;

        // RESTORED: Only mirrors spheres down pipeline if the original criteria match
        if (_currentPhaseNet.Value == 3 && _isPhase3RearPartNet.Value && avatarMirror != null && IsAvatarPresentationActive())
        {
            BroadcastMirrorSphereCreate(localPos, id, qIndex, scale);
        }

        var script = bubble.GetComponent<DisappearOnSelect>();
        if (script != null) {
            script.OnDestroyed = (isRight) => {
                HandleHit(trackingIdx, qIndex, isRight);
                if (_currentPhaseNet.Value == 3 && _isPhase3RearPartNet.Value && avatarMirror != null && IsAvatarPresentationActive())
                {
                    BroadcastMirrorSpherePop(id);
                }
            };
        }
    }

    void UpdateAssessmentAnchorPosition()
    {
        if (!ShouldDriveAssessmentFromLocalPatient()) return;
        if (headset == null) return;

        if (_currentPhaseNet.Value == 3)
        {
            transform.position = headset.TransformPoint(_localNosePos);
        }
        else if (_currentPhaseNet.Value == 4)
        {
            transform.position = headset.position + new Vector3(0, phase4YOffset, 0);
        }
        else
        {
            transform.position = headset.position;
        }
    }

    void UpdateAvatarOverlay()
    {
        bool shouldShowAvatar = _currentPhaseNet.Value > 0;
        bool shouldShowMirror = _currentPhaseNet.Value == 3 && _isPhase3RearPartNet.Value;

        if (avatarMirror != null)
        {
            avatarMirror.gameObject.SetActive(shouldShowMirror);
        }

        if (!ShouldDriveAssessmentFromLocalPatient())
        {
            return;
        }

        if (movinAvatar == null || headset == null) return;

        if (_currentPhaseNet.Value == 3 && _isPhase3RearPartNet.Value)
        {
            Vector3 forwardDirection = headset.forward;
            forwardDirection.y = 0f;
            if (forwardDirection.sqrMagnitude < 0.001f)
            {
                forwardDirection = headset.forward;
            }
            forwardDirection.Normalize();

            movinAvatar.transform.position = headset.position + (forwardDirection * avatarForwardOffsetPhase3Rear);
            movinAvatar.transform.rotation = Quaternion.LookRotation(-forwardDirection, Vector3.up);
        }
        else
        {
            movinAvatar.transform.position = headset.position;
            movinAvatar.transform.rotation = headset.rotation;
        }

        if (avatarMirror != null && avatarHead != null)
        {
            avatarMirror.transform.position = avatarHead.position;
            avatarMirror.transform.rotation = avatarHead.rotation;
        }

        ResolveAvatarHandPoses(out Transform leftPoseSource, out Transform rightPoseSource);

        if (movinAvatar != null)
        {
            movinAvatar.SetActive(shouldShowAvatar);
        }

        if (_digitalTwinSync != null)
        {
            _digitalTwinSync.PublishPatientPose(
                shouldShowAvatar,
                movinAvatar.transform.position,
                movinAvatar.transform.rotation,
                leftPoseSource,
                rightPoseSource);
        }
    }

    void ResolveAvatarReferences()
    {
        if (avatarHead == null && avatarMirror != null)
        {
            avatarHead = avatarMirror.headReference;
        }

        if (avatarLeftHand == null && movinAvatar != null)
        {
            avatarLeftHand = FindChildTransform(movinAvatar.transform, "LeftHand", "left_hand", "Hand_L", "LeftHandAnchor");
        }

        if (avatarRightHand == null && movinAvatar != null)
        {
            avatarRightHand = FindChildTransform(movinAvatar.transform, "RightHand", "right_hand", "Hand_R", "RightHandAnchor");
        }

        if (avatarLeftHand == null) avatarLeftHand = leftHand;
        if (avatarRightHand == null) avatarRightHand = rightHand;
    }

    void ResolveAvatarHandPoses(out Transform leftPoseSource, out Transform rightPoseSource)
    {
        leftPoseSource = avatarLeftHand != null ? avatarLeftHand : leftHand;
        rightPoseSource = avatarRightHand != null ? avatarRightHand : rightHand;
    }

    Transform FindChildTransform(Transform root, params string[] candidateNames)
    {
        if (root == null) return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            string lowered = child.name.ToLowerInvariant();
            foreach (string candidate in candidateNames)
            {
                if (lowered.Contains(candidate.ToLowerInvariant()))
                {
                    return child;
                }
            }
        }

        return null;
    }

    bool ShouldDriveAssessmentFromLocalPatient()
    {
        if (SimpleRelayManager.Instance == null && SessionRoleManager.Instance == null)
        {
            return true;
        }

        return ColocationRoleHelper.IsLocalPatient();
    }

    bool IsAvatarPresentationActive()
    {
        if (_digitalTwinSync != null)
        {
            return _digitalTwinSync.IsAvatarVisible;
        }

        return movinAvatar != null && movinAvatar.activeSelf;
    }

    void SetAvatarPresentationActive(bool active)
    {
        if (movinAvatar != null)
        {
            movinAvatar.SetActive(active);
        }

        if (_digitalTwinSync != null && IsServer)
        {
            ResolveAvatarHandPoses(out Transform leftPoseSource, out Transform rightPoseSource);
            _digitalTwinSync.PublishPatientPose(
                active,
                movinAvatar != null ? movinAvatar.transform.position : Vector3.zero,
                movinAvatar != null ? movinAvatar.transform.rotation : Quaternion.identity,
                leftPoseSource,
                rightPoseSource);
        }
    }

    void BroadcastMirrorSphereCreate(Vector3 localPos, int id, int qIndex, float scale)
    {
        CreateMirrorSphereLocal(localPos, id, qIndex, scale);
        SyncMirrorSphereCreateClientRpc(localPos, id, qIndex, scale);
    }

    void BroadcastMirrorSpherePop(int id)
    {
        PopMirrorSphereLocal(id);
        SyncMirrorSpherePopClientRpc(id);
    }

    void CreateMirrorSphereLocal(Vector3 localPos, int id, int qIndex, float scale)
    {
        if (avatarMirror == null) return;
        avatarMirror.CreateMirrorSphere(localPos, id, qIndex, scale);
    }

    void PopMirrorSphereLocal(int id)
    {
        if (avatarMirror == null) return;
        avatarMirror.PopMirrorSphere(id);
    }

    [ClientRpc]
    void SyncMirrorSphereCreateClientRpc(Vector3 localPos, int id, int qIndex, float scale)
    {
        if (IsServer) return;
        CreateMirrorSphereLocal(localPos, id, qIndex, scale);
    }

    [ClientRpc]
    void SyncMirrorSpherePopClientRpc(int id)
    {
        if (IsServer) return;
        PopMirrorSphereLocal(id);
    }

    void EvaluateProximityIntersections()
    {
        if (rightHand == null || leftHand == null) return;
        float interactRadius = targetVisualSize * 1.5f;

        // 1. Core Hand Proximity Overlap Checks
        foreach (Transform child in transform)
        {
            var script = child.GetComponent<DisappearOnSelect>();
            if (script == null) continue;

            if (Vector3.Distance(child.position, rightHand.position) < interactRadius)
            {
                script.Pop(true); 
                break;
            }
            if (Vector3.Distance(child.position, leftHand.position) < interactRadius)
            {
                script.Pop(false); 
                break;
            }
        }

        // 2. Active Virtual Avatar Mirror Overlap System (Phase 3 Rear Specific)
        if (_currentPhaseNet.Value == 3 && _isPhase3RearPartNet.Value && avatarMirror != null && 
            IsAvatarPresentationActive())
        {
            // RESTORED: Re-synchronized the mirror anchor position explicitly to the avatar's bone configuration
            if (avatarHead != null)
            {
                avatarMirror.transform.position = avatarHead.position;
                avatarMirror.transform.rotation = avatarHead.rotation;
            }

            foreach (Transform mirrorChild in avatarMirror.transform)
            {
                float mirrorInteractRadius = (targetVisualSize * 0.35f) * 1.5f;

                if (Vector3.Distance(mirrorChild.position, avatarRightHand.position) < mirrorInteractRadius)
                {
                    int id = GetIdFromMirroredObjName(mirrorChild.name);
                    avatarMirror.PopMirrorSphere(id);
                    TriggerMainBubblePopById(id, true);
                    break;
                }
                if (Vector3.Distance(mirrorChild.position, avatarLeftHand.position) < mirrorInteractRadius)
                {
                    int id = GetIdFromMirroredObjName(mirrorChild.name);
                    avatarMirror.PopMirrorSphere(id);
                    TriggerMainBubblePopById(id, false);
                    break;
                }
            }
        }
    }

    int GetIdFromMirroredObjName(string name)
    {
        string[] parts = name.Split('_');
        if (parts.Length > 1 && int.TryParse(parts[1], out int id)) return id;
        return -1;
    }

    void TriggerMainBubblePopById(int id, bool isRightHand)
    {
        foreach (Transform child in transform)
        {
            if (child.name == $"Bubble_{id}")
            {
                var script = child.GetComponent<DisappearOnSelect>();
                if (script != null) script.Pop(isRightHand);
                break;
            }
        }
    }

    void CreatePhaseSpecificBackground(float r)
    {
        if (_currentPhaseNet.Value == 4) return;

        if (quadrantMaterials != null && quadrantMaterials.Length >= 4)
        {
            float lonStart = 0f;
            float lonEnd = 1f;

            if (_currentPhaseNet.Value == 3) {
                lonStart = _isPhase3RearPartNet.Value ? 0.25f : 0.75f;
                lonEnd = _isPhase3RearPartNet.Value ? 0.75f : 1.25f;
            }

            CreateWedge(lonStart, (lonStart + lonEnd)/2f, 0.5f, 1.0f, r, quadrantMaterials[0]); 
            CreateWedge(lonStart, (lonStart + lonEnd)/2f, 0.0f, 0.5f, r, quadrantMaterials[1]); 
            CreateWedge((lonStart + lonEnd)/2f, lonEnd, 0.5f, 1.0f, r, quadrantMaterials[2]); 
            CreateWedge((lonStart + lonEnd)/2f, lonEnd, 0.0f, 0.5f, r, quadrantMaterials[3]); 
        }
        else if (transparentBackgroundMaterial != null)
        {
            float lonStart = _currentPhaseNet.Value == 3 ? (_isPhase3RearPartNet.Value ? 0.25f : 0.75f) : 0f;
            float lonEnd = _currentPhaseNet.Value == 3 ? (_isPhase3RearPartNet.Value ? 0.75f : 1.25f) : 1f;
            CreateWedge(lonStart, lonEnd, 0.0f, 1.0f, r, transparentBackgroundMaterial);
        }
    }

    void CreateWedge(float lonS, float lonE, float latS, float latE, float r, Material mat) {
        GameObject go = new GameObject("BgWedge");
        go.transform.SetParent(this.transform, false);
        MeshFilter mf = go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>().material = mat;
        Mesh mesh = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        int res = 8;
        for (int j = 0; j <= res; j++) {
            for (int i = 0; i <= res; i++) {
                float lon = Mathf.Lerp(lonS, lonE, (float)i/res);
                float lat = Mathf.Lerp(latS, latE, (float)j/res);
                verts.Add(CalculateFullSpherePoint(lon % 1.0f, lat, r * 0.98f));
            }
        }
        for (int j = 0; j < res; j++) {
            for (int i = 0; i < res; i++) {
                int root = j * (res + 1) + i;
                tris.Add(root); tris.Add(root + res + 1); tris.Add(root + 1);
                tris.Add(root + 1); tris.Add(root + res + 1); tris.Add(root + res + 2);
            }
        }
        mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mf.mesh = mesh;
    }

    Vector3 CalculateFullSpherePoint(float lonP, float latP, float r) {
        float hAngle = lonP * Mathf.PI * 2f;
        float vAngle = (latP - 0.5f) * Mathf.PI;
        return new Vector3(r * Mathf.Cos(vAngle) * Mathf.Sin(hAngle), r * Mathf.Sin(vAngle), r * Mathf.Cos(vAngle) * Mathf.Cos(hAngle));
    }

    Vector3 CalculateFrontalPoint(float lonP, float latP, float r) {
        float hAngle = (lonP - 0.5f) * Mathf.PI;
        float vAngle = (latP - 0.5f) * Mathf.PI * 0.8f;
        return new Vector3(r * Mathf.Sin(hAngle), r * Mathf.Sin(vAngle), r * Mathf.Cos(hAngle) * Mathf.Cos(vAngle));
    }

    void HandleHit(int phase, int qIndex, bool isRightHand) => _hitsByHand[phase, qIndex, isRightHand ? 1 : 0]++;
    int GetStandardQuadrantIndex(float lonP, float latP) => (latP > 0.5f ? (lonP > 0.5f ? 2 : 0) : (lonP > 0.5f ? 3 : 1));

    float GetDurationForPhase(int phase, bool isRear)
    {
        return phase switch {
            1 => phase1Duration,
            2 => phase2Duration,
            3 => isRear ? phase3RearDuration : phase3FrontDuration,
            4 => phase4Duration,
            _ => 15f
        };
    }

    void AdvancePhase() {
        if (!IsServer) return;

        // NEW: Force copy current tracing paths into historical caches prior to changing running numbers
        if (voxelGridManager != null)
        {
            if (_currentPhaseNet.Value == 1 || _currentPhaseNet.Value == 2)
            {
                voxelGridManager.SavePhaseData(_currentPhaseNet.Value);
            }
            else if (_currentPhaseNet.Value == 3)
            {
                voxelGridManager.SavePhase3Data(_isPhase3RearPartNet.Value);
            }
        }

        if (_currentPhaseNet.Value == 3 && !_isPhase3RearPartNet.Value) {
            _isPhase3RearPartNet.Value = true;
            StartPhase(3);
        }
        else if (_currentPhaseNet.Value < 4) {
            _isPhase3RearPartNet.Value = false;
            StartPhase(_currentPhaseNet.Value + 1);
        }
        else {
            EndTest();
        }
    }

    void StartPhase(int phase) 
    {
        if (!IsServer) return;

        _currentPhaseNet.Value = phase;
        _timerNet.Value = GetDurationForPhase(phase, _isPhase3RearPartNet.Value); 
        _bubbleIdCounter = 0;

        SetAvatarPresentationActive(_currentPhaseNet.Value > 0);

        if (avatarMirror != null)
        {
            avatarMirror.gameObject.SetActive(_currentPhaseNet.Value == 3 && _isPhase3RearPartNet.Value);
        }
        
        if (voxelGridManager != null) 
        {
            voxelGridManager.ResetAllVoxelData();
            voxelGridManager.SynchronizeTrackingState(phase, _localNosePos);

            int taskIndex = phase - 1; // Default mapping: P1->0, P2->1, P3Front->2
            if (phase == 3 && _isPhase3RearPartNet.Value) taskIndex = 3; // P3 Rear -> slot 3
            else if (phase == 4) taskIndex = 4; // P4 -> slot 4
            
            voxelGridManager.SwitchActiveTask(taskIndex);
        }
        if (_currentPhaseNet.Value > 0) BuildSetup();
    }

    void EndTest() 
    { 
        if (!IsServer) return;
        _isTestRunningNet.Value = false;
        
        // Save final phase data
        if (voxelGridManager != null && _currentPhaseNet.Value == 4)
        {
            voxelGridManager.SavePhaseData(4);
            voxelGridManager.ClearGridDisplay();
        }

        // NEW CLEANUP ROUTINE: Clear all remaining bubble objects & background wedges left in the scene
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isEditor && !Application.isPlaying) 
                DestroyImmediate(child);
            else 
                Destroy(child); // Destroys the NetworkObject across all connected clients automatically
        }

        SetAvatarPresentationActive(false);
        if (avatarMirror != null) avatarMirror.gameObject.SetActive(false);
        
        if (uiPanelController != null)
        {
            ActivateAndBringBackUI();
        }
    }

    void ActivateAndBringBackUI()
    {
        uiPanelController.gameObject.SetActive(true);
        uiPanelController.SpawnInFrontOfHeadset();
    }

    void GenerateExtendedPhase4() {
        float[] radii = { 0.15f, 0.30f, 0.45f, 0.60f };
        for (int q = 0; q < radii.Length; q++)
            for (int i = 0; i < longitudeSegments * 2; i++)
                SpawnWaterBubble(CalculateFullSpherePoint((float)i / (longitudeSegments*2), 0.5f, radii[q]), GetStandardQuadrantIndex((float)i / (longitudeSegments*2), 0.5f), targetVisualSize);
    }

    public override void OnNetworkDespawn()
    {
        _currentPhaseNet.OnValueChanged -= OnPhaseOrRearChanged;
        _isPhase3RearPartNet.OnValueChanged -= OnPhaseOrRearChanged;
    }
}