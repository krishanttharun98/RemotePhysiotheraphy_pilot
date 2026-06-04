using UnityEngine;
using System.Collections.Generic;

public class AvatarSphereMirror : MonoBehaviour
{
    public Transform headReference; // Drag the Avatar's Head Joint here
    public GameObject spherePrefab;
    public Material[] quadrantMaterials;
    
    private Dictionary<int, GameObject> _mirrorSpheres = new Dictionary<int, GameObject>();

    public void CreateMirrorSphere(Vector3 localPos, int id, int qIndex, float scale)
    {
        if (headReference == null) return;

        // Instantiate as child of the head reference
        GameObject mirrorBall = Instantiate(spherePrefab, headReference);
        
        // localPos from the user's headset will now be relative to the avatar's head
        mirrorBall.transform.localPosition = localPos;
        mirrorBall.transform.localScale = Vector3.one * scale;

        if (qIndex >= 0 && qIndex < quadrantMaterials.Length)
        {
            Renderer r = mirrorBall.GetComponentInChildren<Renderer>();
            if (r != null) r.material = quadrantMaterials[qIndex];
        }

        // Remove interactions
        Destroy(mirrorBall.GetComponent<DisappearOnSelect>());
        Destroy(mirrorBall.GetComponent<Collider>());

        if (!_mirrorSpheres.ContainsKey(id)) _mirrorSpheres.Add(id, mirrorBall);
    }

    public void PopMirrorSphere(int id)
    {
        // Looks up the specific sphere by its ID in the dictionary
        if (_mirrorSpheres.TryGetValue(id, out GameObject ball))
        {
            if (ball != null) Destroy(ball);
            _mirrorSpheres.Remove(id);
        }
    }

    public void ClearMirror()
    {
        foreach (var ball in _mirrorSpheres.Values) if (ball != null) Destroy(ball);
        _mirrorSpheres.Clear();
    }
}