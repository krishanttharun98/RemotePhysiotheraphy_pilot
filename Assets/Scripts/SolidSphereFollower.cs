using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Local single-user RWS bubble task (no Netcode). Observation scripts subscribe to the
// events below; bubble grid indices are passed as plain parameters (no shared struct type).
public class SolidSphereFollower : MonoBehaviour
{
    [Header("Tracking")]
    public Transform headset;
    public Transform leftHand;
    public Transform rightHand;

    [Header("Avatar Settings")]
    public GameObject movinAvatar;
    public Transform avatarHead;
    public Transform avatarLeftHand;
    public Transform avatarRightHand;
    public AvatarSphereMirror avatarMirror;

    [Header("Structure")]
    public int longitudeSegments = 24;
    public GameObject waterBubblePrefab;
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

    [Header("Rest Between Phases")]
    public bool useRestBetweenPhases = true;

    [Header("Phase 4 Ring Radii")]
    public float[] phase4Radii = { 0.15f, 0.30f, 0.45f, 0.60f };

    public event Action<int, bool> PhaseStarted;
    public event Action<int, bool> PhaseCompleted;
    public event Action TestEnded;
    public event Action<bool> RestStateChanged;

    // localPos, quadrant, scale, lonIdx, latIdx, ringIdx (-1 except phase 4)
    public event Action<Vector3, int, float, int, int, int> BubbleSpawned;
    public event Action<Vector3, int, float, int, int, int> BubblePopped;

    int _currentPhase;
    bool _isPhase3Rear;
    float _timer;
    bool _isTestRunning = true;
    bool _isResting;

    float _userReach;
    float _calibratedNoseDistance = 0.15f;
    Vector3 _localNosePos;
    int _bubbleIdCounter;

    readonly int[,] _totalSpheresPerPhase = new int[6, 4];
    readonly int[,,] _hitsByHand = new int[6, 4, 2];

    public int CurrentPhase => _currentPhase;
    public bool IsPhase3Rear => _isPhase3Rear;
    public bool IsResting => _isResting;
    public bool IsTestRunning => _isTestRunning;
    public Transform RwsCenter => transform;

    public int CurrentHistoryIndex
    {
        get
        {
            if (_currentPhase == 4) return 5;
            if (_currentPhase == 3) return _isPhase3Rear ? 4 : 3;
            return _currentPhase;
        }
    }

    public static int HistoryToTrackingIndex(int historyIndex) =>
        historyIndex == 4 ? 5 : historyIndex;

    public float[] GetCurrentPhaseRadii()
    {
        if (_currentPhase == 4) return (float[])phase4Radii.Clone();
        float r = _currentPhase == 3 ? (_calibratedNoseDistance + noseBuffer)
            : (_currentPhase == 1 ? _userReach * 0.75f : _userReach);
        return new float[] { r };
    }

    public float CurrentBubbleScale =>
        _currentPhase == 3 ? targetVisualSize * 0.35f : targetVisualSize;

    public int GetQuadrantBubbleTotal(int historyIndex, int quadrant)
    {
        if (quadrant < 0 || quadrant > 3 || historyIndex < 1 || historyIndex > 5) return 0;
        return _totalSpheresPerPhase[HistoryToTrackingIndex(historyIndex), quadrant];
    }

    public int GetQuadrantPoppedTotal(int historyIndex, int quadrant)
    {
        if (quadrant < 0 || quadrant > 3 || historyIndex < 1 || historyIndex > 5) return 0;
        int row = HistoryToTrackingIndex(historyIndex);
        return _hitsByHand[row, quadrant, 0] + _hitsByHand[row, quadrant, 1];
    }

    public float GetQuadrantPopPercent(int historyIndex, int quadrant)
    {
        int total = GetQuadrantBubbleTotal(historyIndex, quadrant);
        return total <= 0 ? 0f : (float)GetQuadrantPoppedTotal(historyIndex, quadrant) / total;
    }

    public void GetPhaseQuadrantSummary(int historyIndex, out int[] totals, out int[] popped, out float[] percentages)
    {
        totals = new int[4];
        popped = new int[4];
        percentages = new float[4];
        for (int q = 0; q < 4; q++)
        {
            totals[q] = GetQuadrantBubbleTotal(historyIndex, q);
            popped[q] = GetQuadrantPoppedTotal(historyIndex, q);
            percentages[q] = GetQuadrantPopPercent(historyIndex, q);
        }
    }

    void Start()
    {
        if (headset == null && Camera.main != null) headset = Camera.main.transform;

        if (rightHand == null)
        {
            GameObject rHand = GameObject.Find("RightHandAnchor") ?? GameObject.Find("RightControllerAnchor");
            if (rHand != null) rightHand = rHand.transform;
        }

        if (leftHand == null)
        {
            GameObject lHand = GameObject.Find("LeftHandAnchor") ?? GameObject.Find("LeftControllerAnchor");
            if (lHand != null) leftHand = lHand.transform;
        }

        if (movinAvatar != null) movinAvatar.SetActive(false);

        if (headset != null && rightHand != null)
            _userReach = Vector3.Distance(headset.position, rightHand.position);
        if (_userReach < 0.2f) _userReach = 0.6f;

        _currentPhase = 0;
        _isPhase3Rear = false;
        _timer = 10f;
        _isTestRunning = true;
        _isResting = false;
    }

    void Update()
    {
        if (_isResting || !_isTestRunning) return;

        if (headset != null)
        {
            if (_currentPhase == 3) transform.position = headset.TransformPoint(_localNosePos);
            else if (_currentPhase == 4) transform.position = headset.position + new Vector3(0f, phase4YOffset, 0f);
            else transform.position = headset.position;
        }

        EvaluateProximityIntersections();

        if (_currentPhase == 0) HandleCalibration();
        else
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f) OnPhaseTimerExpired();
        }
    }

    void HandleCalibration()
    {
        if (!OVRInput.GetDown(OVRInput.RawButton.A) && !Input.GetKeyDown(KeyCode.Space)) return;

        if (headset != null && rightHand != null)
        {
            _calibratedNoseDistance = Vector3.Distance(headset.position, rightHand.position);
            _localNosePos = headset.InverseTransformPoint(rightHand.position);
        }

        StartCoroutine(ShowCalibrationSuccess());
    }

    IEnumerator ShowCalibrationSuccess()
    {
        yield return new WaitForSeconds(2f);
        AdvancePhase();
    }

    public void BuildSetup()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isEditor && !Application.isPlaying) DestroyImmediate(child);
            else Destroy(child);
        }

        if (avatarMirror != null && avatarMirror.gameObject.activeInHierarchy)
            avatarMirror.ClearMirror();

        if (_currentPhase == 4) GenerateExtendedPhase4();
        else if (_currentPhase > 0) GenerateStandardSpheres();
    }

    void GenerateStandardSpheres()
    {
        float r = _currentPhase == 3 ? (_calibratedNoseDistance + noseBuffer)
            : (_currentPhase == 1 ? _userReach * 0.75f : _userReach);
        float finalScale = CurrentBubbleScale;

        for (int i = 0; i < longitudeSegments; i++)
        {
            for (int j = 0; j <= 12; j++)
            {
                float lonP = (float)i / longitudeSegments;
                float latP = (float)j / 12f;
                Vector3 pos = _currentPhase == 3
                    ? CalculateFullSpherePoint(
                        (_isPhase3Rear ? lonP * 0.5f + 0.25f : lonP * 0.5f + 0.75f) % 1f,
                        latP, r)
                    : CalculateFrontalPoint(lonP, latP, r);

                SpawnWaterBubble(pos, GetStandardQuadrantIndex(lonP, latP), finalScale, i, j, -1);
            }
        }

        CreatePhaseSpecificBackground(r, transform);
    }

    void GenerateExtendedPhase4()
    {
        float[] radii = phase4Radii != null && phase4Radii.Length > 0
            ? phase4Radii
            : new[] { 0.15f, 0.30f, 0.45f, 0.60f };

        int lonTotal = longitudeSegments * 2;
        for (int ring = 0; ring < radii.Length; ring++)
        {
            for (int i = 0; i < lonTotal; i++)
            {
                float lonP = (float)i / lonTotal;
                SpawnWaterBubble(
                    CalculateFullSpherePoint(lonP, 0.5f, radii[ring]),
                    GetStandardQuadrantIndex(lonP, 0.5f),
                    targetVisualSize,
                    i, 0, ring);
            }
        }
    }

    void SpawnWaterBubble(Vector3 localPos, int qIndex, float scale, int lonIdx, int latIdx, int ringIdx)
    {
        if (waterBubblePrefab == null) return;

        GameObject bubble = Instantiate(waterBubblePrefab, transform.TransformPoint(localPos), Quaternion.identity);
        bubble.transform.localScale = Vector3.one * scale;
        bubble.transform.SetParent(transform, true);

        int id = _bubbleIdCounter++;
        bubble.name = $"Bubble_{id}";

        int trackingIdx = _currentPhase == 3 && _isPhase3Rear ? 5 : _currentPhase;
        _totalSpheresPerPhase[trackingIdx, qIndex]++;

        BubbleSpawned?.Invoke(localPos, qIndex, scale, lonIdx, latIdx, ringIdx);

        if (_currentPhase == 3 && _isPhase3Rear && avatarMirror != null &&
            movinAvatar != null && movinAvatar.activeInHierarchy)
        {
            avatarMirror.CreateMirrorSphere(localPos, id, qIndex, scale);
        }

        DisappearOnSelect script = bubble.GetComponent<DisappearOnSelect>();
        if (script == null) return;

        script.OnDestroyed = isRight =>
        {
            HandleHit(trackingIdx, qIndex, isRight);
            if (avatarMirror != null && movinAvatar != null && movinAvatar.activeInHierarchy)
                avatarMirror.PopMirrorSphere(id);
            BubblePopped?.Invoke(localPos, qIndex, scale, lonIdx, latIdx, ringIdx);
        };
    }

    void EvaluateProximityIntersections()
    {
        if (rightHand == null || leftHand == null) return;

        float interactRadius = targetVisualSize * 1.5f;
        foreach (Transform child in transform)
        {
            DisappearOnSelect script = child.GetComponent<DisappearOnSelect>();
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

        if (_currentPhase != 3 || !_isPhase3Rear || avatarMirror == null ||
            movinAvatar == null || !movinAvatar.activeInHierarchy ||
            avatarLeftHand == null || avatarRightHand == null) return;

        if (avatarHead != null)
        {
            avatarMirror.transform.position = avatarHead.position;
            avatarMirror.transform.rotation = avatarHead.rotation;
        }

        foreach (Transform mirrorChild in avatarMirror.transform)
        {
            float mirrorInteractRadius = targetVisualSize * 0.35f * 1.5f;
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

    static int GetIdFromMirroredObjName(string name)
    {
        string[] parts = name.Split('_');
        return parts.Length > 1 && int.TryParse(parts[1], out int id) ? id : -1;
    }

    void TriggerMainBubblePopById(int id, bool isRightHand)
    {
        foreach (Transform child in transform)
        {
            if (child.name != $"Bubble_{id}") continue;
            DisappearOnSelect script = child.GetComponent<DisappearOnSelect>();
            if (script != null) script.Pop(isRightHand);
            break;
        }
    }

    void OnPhaseTimerExpired()
    {
        PhaseCompleted?.Invoke(_currentPhase, _isPhase3Rear);
        if (useRestBetweenPhases) EnterRest();
        else AdvancePhase();
    }

    void EnterRest()
    {
        _isResting = true;
        _isTestRunning = false;
        ClearAllChildren();

        if (movinAvatar != null) movinAvatar.SetActive(false);
        if (avatarMirror != null) avatarMirror.gameObject.SetActive(false);
        RestStateChanged?.Invoke(true);
    }

    public void ResumeFromRest()
    {
        if (!_isResting) return;
        _isResting = false;
        _isTestRunning = true;
        RestStateChanged?.Invoke(false);
        AdvancePhase();
    }

    void AdvancePhase()
    {
        if (_currentPhase == 3 && !_isPhase3Rear)
        {
            _isPhase3Rear = true;
            StartPhase(3);
        }
        else if (_currentPhase < 4)
        {
            _isPhase3Rear = false;
            StartPhase(_currentPhase + 1);
        }
        else EndTest();
    }

    void StartPhase(int phase)
    {
        _currentPhase = phase;
        _timer = GetDurationForPhase(phase, _isPhase3Rear);
        _bubbleIdCounter = 0;
        _isTestRunning = true;

        bool phase3Rear = phase == 3 && _isPhase3Rear;
        if (movinAvatar != null) movinAvatar.SetActive(phase3Rear);
        if (avatarMirror != null) avatarMirror.gameObject.SetActive(phase3Rear);

        // Listeners must create per-phase grid data before bubbles spawn.
        PhaseStarted?.Invoke(_currentPhase, _isPhase3Rear);

        if (_currentPhase > 0) BuildSetup();
    }

    void EndTest()
    {
        _isTestRunning = false;
        _isResting = false;
        ClearAllChildren();

        if (movinAvatar != null) movinAvatar.SetActive(false);
        if (avatarMirror != null) avatarMirror.gameObject.SetActive(false);
        TestEnded?.Invoke();

        if (uiPanelController == null) return;
        uiPanelController.gameObject.SetActive(true);
        uiPanelController.SpawnInFrontOfHeadset();
    }

    void ClearAllChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isEditor && !Application.isPlaying) DestroyImmediate(child);
            else Destroy(child);
        }
    }

    float GetDurationForPhase(int phase, bool isRear) => phase switch
    {
        1 => phase1Duration,
        2 => phase2Duration,
        3 => isRear ? phase3RearDuration : phase3FrontDuration,
        4 => phase4Duration,
        _ => 15f
    };

    void HandleHit(int phase, int qIndex, bool isRightHand) =>
        _hitsByHand[phase, qIndex, isRightHand ? 1 : 0]++;

    int GetStandardQuadrantIndex(float lonP, float latP) =>
        latP > 0.5f ? (lonP > 0.5f ? 2 : 0) : (lonP > 0.5f ? 3 : 1);

    public GameObject BuildAlignedQuadrantSphere(Transform parent)
    {
        if (_currentPhase <= 0) return CreateEmptyShellRoot(parent);
        return BuildQuadrantSphereForPhase(_currentPhase, _isPhase3Rear, GetCurrentPhaseRadii(), parent);
    }

    // Build the coloured quadrant shell for a specific phase (live or history replay).
    public GameObject BuildQuadrantSphereForPhase(int phase, bool isPhase3Rear, float[] radii, Transform parent)
    {
        GameObject container = CreateEmptyShellRoot(parent);
        if (phase <= 0 || quadrantMaterials == null || quadrantMaterials.Length < 4)
            return container;

        if (radii == null || radii.Length == 0)
            radii = new float[] { 0.5f };

        foreach (float rr in radii)
            BuildLayoutSurface(container.transform, rr * 0.98f, phase, isPhase3Rear);

        return container;
    }

    static GameObject CreateEmptyShellRoot(Transform parent)
    {
        GameObject container = new GameObject("StationQuadrantSphere");
        container.transform.SetParent(parent, false);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;
        return container;
    }

    void BuildLayoutSurface(Transform parent, float r, int phase, bool isPhase3Rear)
    {
        const int latRes = 12;
        var verts = new List<Vector3>[4];
        var tris = new List<int>[4];
        for (int q = 0; q < 4; q++)
        {
            verts[q] = new List<Vector3>();
            tris[q] = new List<int>();
        }

        for (int i = 0; i < longitudeSegments; i++)
        {
            for (int j = 0; j < latRes; j++)
            {
                float lonP0 = (float)i / longitudeSegments;
                float lonP1 = (float)(i + 1) / longitudeSegments;
                float latP0 = (float)j / latRes;
                float latP1 = (float)(j + 1) / latRes;

                Vector3 p00 = MapPhasePointFor(phase, isPhase3Rear, lonP0, latP0, r);
                Vector3 p10 = MapPhasePointFor(phase, isPhase3Rear, lonP1, latP0, r);
                Vector3 p01 = MapPhasePointFor(phase, isPhase3Rear, lonP0, latP1, r);
                Vector3 p11 = MapPhasePointFor(phase, isPhase3Rear, lonP1, latP1, r);

                int q = GetStandardQuadrantIndex((lonP0 + lonP1) * 0.5f, (latP0 + latP1) * 0.5f);
                int b = verts[q].Count;
                verts[q].Add(p00); verts[q].Add(p10); verts[q].Add(p01); verts[q].Add(p11);
                tris[q].Add(b); tris[q].Add(b + 2); tris[q].Add(b + 1);
                tris[q].Add(b + 1); tris[q].Add(b + 2); tris[q].Add(b + 3);
            }
        }

        for (int q = 0; q < 4; q++)
        {
            if (verts[q].Count == 0) continue;
            GameObject go = new GameObject("StationQuadrant" + q);
            go.transform.SetParent(parent, false);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().material = quadrantMaterials[q];
            Mesh mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts[q]);
            mesh.SetTriangles(tris[q], 0);
            mesh.RecalculateNormals();
            mf.mesh = mesh;
        }
    }

    public Vector3 MapBubbleGridToLocal(int phase, bool rear, int lonIdx, int latIdx, float radius, int ringIdx = -1)
    {
        if (phase == 4 && ringIdx >= 0)
        {
            float lonP = (float)lonIdx / (longitudeSegments * 2);
            return MapPhasePointFor(phase, rear, lonP, 0.5f, radius);
        }

        return MapPhasePointFor(phase, rear, (float)lonIdx / longitudeSegments, (float)latIdx / 12f, radius);
    }

    Vector3 MapPhasePoint(float lonP, float latP, float r) =>
        MapPhasePointFor(_currentPhase, _isPhase3Rear, lonP, latP, r);

    Vector3 MapPhasePointFor(int phase, bool rear, float lonP, float latP, float r)
    {
        if (phase == 3)
        {
            float lonAdj = rear ? lonP * 0.5f + 0.25f : lonP * 0.5f + 0.75f;
            return CalculateFullSpherePoint(lonAdj % 1f, latP, r);
        }

        if (phase == 4) return CalculateFullSpherePoint(lonP, latP, r);
        return CalculateFrontalPoint(lonP, latP, r);
    }

    void CreatePhaseSpecificBackground(float r, Transform parent)
    {
        if (_currentPhase == 4) return;

        if (quadrantMaterials != null && quadrantMaterials.Length >= 4)
        {
            float lonStart = _currentPhase == 3 ? (_isPhase3Rear ? 0.25f : 0.75f) : 0f;
            float lonEnd = _currentPhase == 3 ? (_isPhase3Rear ? 0.75f : 1.25f) : 1f;
            float mid = (lonStart + lonEnd) * 0.5f;

            CreateWedge(lonStart, mid, 0.5f, 1f, r, quadrantMaterials[0], parent);
            CreateWedge(lonStart, mid, 0f, 0.5f, r, quadrantMaterials[1], parent);
            CreateWedge(mid, lonEnd, 0.5f, 1f, r, quadrantMaterials[2], parent);
            CreateWedge(mid, lonEnd, 0f, 0.5f, r, quadrantMaterials[3], parent);
        }
        else if (transparentBackgroundMaterial != null)
        {
            float lonStart = _currentPhase == 3 ? (_isPhase3Rear ? 0.25f : 0.75f) : 0f;
            float lonEnd = _currentPhase == 3 ? (_isPhase3Rear ? 0.75f : 1.25f) : 1f;
            CreateWedge(lonStart, lonEnd, 0f, 1f, r, transparentBackgroundMaterial, parent);
        }
    }

    void CreateWedge(float lonS, float lonE, float latS, float latE, float r, Material mat, Transform parent)
    {
        GameObject go = new GameObject("BgWedge");
        go.transform.SetParent(parent, false);
        MeshFilter mf = go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>().material = mat;

        var verts = new List<Vector3>();
        var tris = new List<int>();
        const int res = 8;

        for (int j = 0; j <= res; j++)
        {
            for (int i = 0; i <= res; i++)
            {
                float lon = Mathf.Lerp(lonS, lonE, (float)i / res);
                float lat = Mathf.Lerp(latS, latE, (float)j / res);
                verts.Add(CalculateFullSpherePoint(lon % 1f, lat, r * 0.98f));
            }
        }

        for (int j = 0; j < res; j++)
        {
            for (int i = 0; i < res; i++)
            {
                int root = j * (res + 1) + i;
                tris.Add(root); tris.Add(root + res + 1); tris.Add(root + 1);
                tris.Add(root + 1); tris.Add(root + res + 1); tris.Add(root + res + 2);
            }
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mf.mesh = mesh;
    }

    public Vector3 CalculateFullSpherePoint(float lonP, float latP, float r)
    {
        float hAngle = lonP * Mathf.PI * 2f;
        float vAngle = (latP - 0.5f) * Mathf.PI;
        return new Vector3(
            r * Mathf.Cos(vAngle) * Mathf.Sin(hAngle),
            r * Mathf.Sin(vAngle),
            r * Mathf.Cos(vAngle) * Mathf.Cos(hAngle));
    }

    Vector3 CalculateFrontalPoint(float lonP, float latP, float r)
    {
        float hAngle = (lonP - 0.5f) * Mathf.PI;
        float vAngle = (latP - 0.5f) * Mathf.PI * 0.8f;
        return new Vector3(
            r * Mathf.Sin(hAngle),
            r * Mathf.Sin(vAngle),
            r * Mathf.Cos(hAngle) * Mathf.Cos(vAngle));
    }
}
