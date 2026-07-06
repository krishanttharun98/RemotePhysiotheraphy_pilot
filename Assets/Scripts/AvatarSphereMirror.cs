using UnityEngine;
using System.Collections.Generic;

// Mirror bubbles on the movinAvatar head during Phase 3 rear (paired with live RWS bubbles by id).
public class AvatarSphereMirror : MonoBehaviour
{
    public Transform headReference;
    public GameObject spherePrefab;
    public Material[] quadrantMaterials;

    readonly Dictionary<int, GameObject> _mirrorSpheres = new Dictionary<int, GameObject>();

    public void CreateMirrorSphere(Vector3 localPos, int id, int qIndex, float scale)
    {
        if (headReference == null) return;

        GameObject mirrorBall;
        if (spherePrefab != null)
        {
            mirrorBall = Instantiate(spherePrefab, headReference);
            mirrorBall.name = $"MirrorBubble_{id}";
            mirrorBall.transform.localPosition = localPos;
            mirrorBall.transform.localScale = Vector3.one * scale;
        }
        else
        {
            mirrorBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mirrorBall.name = $"MirrorBubble_{id}";
            mirrorBall.transform.SetParent(headReference, false);
            mirrorBall.transform.localPosition = localPos;
            mirrorBall.transform.localScale = Vector3.one * scale;
            Collider col = mirrorBall.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        foreach (Renderer rend in mirrorBall.GetComponentsInChildren<Renderer>(true))
            rend.enabled = true;

        if (qIndex >= 0 && quadrantMaterials != null && qIndex < quadrantMaterials.Length)
        {
            Renderer r = mirrorBall.GetComponentInChildren<Renderer>();
            if (r != null) r.material = quadrantMaterials[qIndex];
        }

        DisappearOnSelect dos = mirrorBall.GetComponent<DisappearOnSelect>();
        if (dos != null) Destroy(dos);
        Collider bubbleCol = mirrorBall.GetComponent<Collider>();
        if (bubbleCol != null) Destroy(bubbleCol);

        _mirrorSpheres[id] = mirrorBall;
    }

    public bool TryPopAtPoint(Vector3 worldPoint, float radius, out int poppedId)
    {
        poppedId = -1;
        float bestDist = radius;

        foreach (KeyValuePair<int, GameObject> kv in _mirrorSpheres)
        {
            if (kv.Value == null) continue;
            float dist = Vector3.Distance(worldPoint, kv.Value.transform.position);
            if (dist <= bestDist)
            {
                bestDist = dist;
                poppedId = kv.Key;
            }
        }

        if (poppedId < 0) return false;
        PopMirrorSphere(poppedId);
        return true;
    }

    public void PopMirrorSphere(int id)
    {
        if (!_mirrorSpheres.TryGetValue(id, out GameObject ball)) return;
        if (ball != null) Destroy(ball);
        _mirrorSpheres.Remove(id);
    }

    public void ClearMirror()
    {
        foreach (GameObject ball in _mirrorSpheres.Values)
            if (ball != null) Destroy(ball);
        _mirrorSpheres.Clear();
    }
}
