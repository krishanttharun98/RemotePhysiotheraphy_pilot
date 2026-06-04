using UnityEngine;

public class VRCanvasController : MonoBehaviour
{
    [Header("Spawn Settings")]
    public Transform headsetCamera;
    public float spawnDistance = 1.2f;
    public float spawnHeightOffset = -0.1f;

    void Start()
    {
        SpawnInFrontOfHeadset();
    }

    public void SpawnInFrontOfHeadset()
    {
        if (headsetCamera == null)
        {
            headsetCamera = Camera.main.transform;
        }

        // Calculate a comfortable viewing position in front of the headset
        Vector3 targetPosition = headsetCamera.position + (headsetCamera.forward * spawnDistance);
        targetPosition.y += spawnHeightOffset; // Lower it slightly so it doesn't block direct eye-line
        
        transform.position = targetPosition;

        // Make the canvas flatly face the player's horizon direction
        Vector3 forwardLook = headsetCamera.forward;
        forwardLook.y = 0; // Flatten it out so it doesn't tilt weirdly
        if (forwardLook != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(forwardLook.normalized);
        }
    }
}