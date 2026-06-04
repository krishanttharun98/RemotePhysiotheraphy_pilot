using Unity.Netcode;
using UnityEngine;

public class RemoteObjectSpawner : NetworkBehaviour
{
    // Assign your networked virtual object prefab here in the Inspector
    public GameObject virtualObjectPrefab; 

    // Call this function via a UI button press or when a user joins the room
    public void SpawnObjectInFrontOfHost()
    {
        // Only the Server/Host is allowed to spawn global network objects
        if (!IsServer) return; 

        // 1. Find the Host's local headset camera
        Camera hostCamera = Camera.main;
        if (hostCamera == null) return;

        // 2. Calculate a point exactly 2 meters directly in front of the Host's gaze
        Vector3 spawnPosition = hostCamera.transform.position + (hostCamera.transform.forward * 2.0f);
        
        // Optional: Keep the object flat on the user's eye level or table height
        Quaternion spawnRotation = Quaternion.Euler(0, hostCamera.transform.rotation.eulerAngles.y, 0);

        // 3. Instantiate the object locally on the server
        GameObject spawnedObj = Instantiate(virtualObjectPrefab, spawnPosition, spawnRotation);

        // 4. Spawn it across the network so the Client sees it instantly
        spawnedObj.GetComponent<NetworkObject>().Spawn();
    }
}