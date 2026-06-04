using UnityEngine;
using System.Collections.Generic;

public class WaterBubbleHemisphereManager : MonoBehaviour
{
    [Header("Tracking & Core References")]
    public Transform headset; 
    public Transform leftHand;
    public Transform rightHand;

    [Header("Heatmap System Link")]
    public RWSHeatmapManager heatmapManager;

    [Header("Avatar & Mirror Configuration")]
    public GameObject movinAvatar;
    public Transform avatarHead; 
    public Transform avatarLeftHand; 
    public Transform avatarRightHand; 
    public AvatarSphereMirror avatarMirror;

    [Header("Structure")]
    public int longitudeSegments = 24; 
    public GameObject waterBubblePrefab;
    public Material transparentBackgroundMaterial;

    [Header("Clinical Settings")]
    public float noseBuffer = 0.05f;      
    public float phase4YOffset = -0.50f; 

    [Header("Debug Phase Durations (Seconds)")]
    public float phase1Duration = 15f;
    public float phase2Duration = 15f;
    public float phase3FrontDuration = 15f;
    public float phase3RearDuration = 15f;
    public float phase4Duration = 15f;

    [Header("Dynamic Scaling")]
    public float targetVisualSize = 0.045f; 

    private int _currentPhase = 0; 
    private bool _isPhase3RearPart = false; 
    private float _userReach;
    private float _calibratedNoseDistance = 0.15f; 
    private Vector3 _localNosePos; 
    private float _timer;
    private bool _isTestRunning = true;
    private int _bubbleIdCounter = 0;

    void Start()
    {
        if (movinAvatar != null) movinAvatar.SetActive(false);
        
        _userReach = Vector3.Distance(headset.position, rightHand.position);
        if (_userReach < 0.2f) _userReach = 0.6f; 
        
        StartPhase(0); 
    }

    void Update()
    {
        if (!_isTestRunning) return;

        HandleAvatarAndMirrorPositions();

        if (_currentPhase == 0) 
        {
            HandleCalibration();
        }
        else 
        {
            _timer -= Time.deltaTime;
            
            if (heatmapManager != null)
            {
                int trackingMapIdx = GetTrackingPhaseIndex();
                heatmapManager.RecordHandData(trackingMapIdx, leftHand, rightHand);
            }

            // Real-time distance checking to clear bubbles via real-world controller intersections
            EvaluateControllerIntersections();

            if (_timer <= 0) AdvancePhase();
        }
    }

    void HandleAvatarAndMirrorPositions()
    {
        if (movinAvatar == null) return;

        if (_currentPhase == 3 && _isPhase3RearPart)
        {
            Vector3 forwardDirection = headset.forward;
            forwardDirection.y = 0; 
            forwardDirection.Normalize();

            movinAvatar.transform.position = headset.position + (forwardDirection * 1.0f);
            movinAvatar.transform.rotation = Quaternion.LookRotation(-forwardDirection, Vector3.up);
        }
        else
        {
            movinAvatar.transform.position = headset.position;
            movinAvatar.transform.rotation = headset.rotation;
        }

        if (_currentPhase == 3) transform.position = headset.TransformPoint(_localNosePos);
        else if (_currentPhase == 4) transform.position = headset.position + new Vector3(0, phase4YOffset, 0);
        else transform.position = headset.position;

        if (avatarMirror != null && avatarHead != null)
        {
            avatarMirror.transform.position = avatarHead.position;
            avatarMirror.transform.rotation = avatarHead.rotation;
        }
    }

    void EvaluateControllerIntersections()
    {
        float interactRadius = targetVisualSize * 1.5f;

        foreach (Transform child in transform)
        {
            var script = child.GetComponent<DisappearOnSelect>();
            if (script == null) continue;

            // Uses your customized DisappearOnSelect.Pop script to run the particle transition
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

        // Bi-manual interactive evaluation inside the mirrored zone (Phase 3 Rear specific)
        if (_currentPhase == 3 && _isPhase3RearPart && avatarMirror != null && avatarLeftHand != null && avatarRightHand != null)
        {
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

    int GetTrackingPhaseIndex()
    {
        if (_currentPhase == 1) return 0;
        if (_currentPhase == 2) return 1;
        if (_currentPhase == 3) return _isPhase3RearPart ? 3 : 2;
        if (_currentPhase == 4) return 4;
        return -1;
    }

    void HandleCalibration()
    {
        // Keeps Button A / Spacebar to capture user nose distance baseline for phase 3 configurations
        if (OVRInput.GetDown(OVRInput.RawButton.A) || Input.GetKeyDown(KeyCode.Space)) 
        {
            _calibratedNoseDistance = Vector3.Distance(headset.position, rightHand.position);
            _localNosePos = headset.InverseTransformPoint(rightHand.position);
            StartCoroutine(ShowCalibrationSuccess());
        }
    }

    System.Collections.IEnumerator ShowCalibrationSuccess()
    {
        yield return new WaitForSeconds(1.5f);
        AdvancePhase();
    }

    public void BuildSetup()
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
        if (avatarMirror != null) avatarMirror.ClearMirror();

        if (_currentPhase == 4) GenerateExtendedPhase4();
        else GenerateStandardLayout();
    }

    void GenerateStandardLayout()
    {
        float r = (_currentPhase == 3) ? (_calibratedNoseDistance + noseBuffer) : 
                  (_currentPhase == 1 ? _userReach * 0.75f : _userReach);
        
        float finalScale = (_currentPhase == 3) ? (targetVisualSize * 0.35f) : targetVisualSize;

        for (int i = 0; i < longitudeSegments; i++)
        {
            for (int j = 0; j <= 12; j++)
            {
                float lonP = (float)i / longitudeSegments;
                float latP = (float)j / 12;
                
                Vector3 pos;
                if (_currentPhase == 3) {
                    float lonAdj = _isPhase3RearPart ? (lonP * 0.5f + 0.25f) : (lonP * 0.5f + 0.75f);
                    pos = CalculateFullSpherePoint(lonAdj % 1.0f, latP, r);
                } else {
                    pos = CalculateFrontalPoint(lonP, latP, r);
                }
                SpawnWaterBubble(pos, GetStandardQuadrantIndex(lonP, latP), finalScale);
            }
        }
        CreateTransparentBackground(r);
    }

    void CreateTransparentBackground(float r)
    {
        if (_currentPhase == 4 || transparentBackgroundMaterial == null) return;
        float lonStart = _currentPhase == 3 ? (_isPhase3RearPart ? 0.25f : 0.75f) : 0f;
        float lonEnd = _currentPhase == 3 ? (_isPhase3RearPart ? 0.75f : 1.25f) : 1f;
        CreateWedge(lonStart, lonEnd, 0.0f, 1.0f, r, transparentBackgroundMaterial); 
    }

    void SpawnWaterBubble(Vector3 localPos, int qIndex, float scale)
    {
        if (waterBubblePrefab == null) return;

        GameObject bubble = Instantiate(waterBubblePrefab, this.transform);
        bubble.transform.localPosition = localPos;
        bubble.transform.localScale = Vector3.one * scale;
        bubble.name = $"Bubble_{_bubbleIdCounter}";

        int id = _bubbleIdCounter++;
        if (_currentPhase == 3 && _isPhase3RearPart && avatarMirror != null)
        {
            avatarMirror.CreateMirrorSphere(localPos, id, qIndex, scale);
        }

        var script = bubble.GetComponent<DisappearOnSelect>();
        if (script != null) {
            script.OnDestroyed = (isRight) => {
                if (avatarMirror != null) avatarMirror.PopMirrorSphere(id);
            };
        }
    }

    void CreateWedge(float lonS, float lonE, float latS, float latE, float r, Material mat) {
        GameObject go = new GameObject("BgTransparentWedge");
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

    int GetStandardQuadrantIndex(float lonP, float latP) => (latP > 0.5f ? (lonP > 0.5f ? 2 : 0) : (lonP > 0.5f ? 3 : 1));

    float GetDurationForPhase(int phase, bool isRear)
    {
        return phase switch {
            1 => phase1Duration,
            2 => phase2Duration,
            3 => isRear ? phase3RearDuration : phase3FrontDuration,
            4 => phase4Duration,
            _ => 10f
        };
    }

    void AdvancePhase() {
        if (_currentPhase == 3 && !_isPhase3RearPart) { 
            _isPhase3RearPart = true; 
            StartPhase(3); 
        }
        else if (_currentPhase < 4) { 
            _isPhase3RearPart = false; 
            StartPhase(_currentPhase + 1); 
        }
        else {
            EndTest();
        }
    }

    void StartPhase(int phase) {
        _currentPhase = phase; 
        _timer = GetDurationForPhase(phase, _isPhase3RearPart); 
        _bubbleIdCounter = 0;

        if (movinAvatar != null) movinAvatar.SetActive(_currentPhase == 3 || _currentPhase == 1 || _currentPhase == 2 || _currentPhase == 4);
        if (_currentPhase > 0) BuildSetup();
    }

    void EndTest() { 
        _isTestRunning = false; 
        if (heatmapManager != null) heatmapManager.BakeAllHeatmaps();
    }

    void GenerateExtendedPhase4() {
        float[] radii = { 0.15f, 0.30f, 0.45f, 0.60f }; 
        for (int q = 0; q < radii.Length; q++)
            for (int i = 0; i < longitudeSegments * 2; i++)
                SpawnWaterBubble(CalculateFullSpherePoint((float)i / (longitudeSegments*2), 0.5f, radii[q]), GetStandardQuadrantIndex((float)i / (longitudeSegments*2), 0.5f), targetVisualSize);
    }
}