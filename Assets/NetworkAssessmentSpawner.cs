using Unity.Netcode;
using UnityEngine;

public class NetworkAssessmentSpawner : NetworkBehaviour
{
    [Header("Assign the SolidSphereFollower Prefab Here")]
    public GameObject solidSphereFollowerPrefab;

    public override void OnNetworkSpawn()
    {
        // Only the Server (User 1 / Host) handles the spawning of global session objects
        if (!IsServer) return;

        // Instantiate the prefab at the origin
        GameObject spawnedAssessment = Instantiate(solidSphereFollowerPrefab, Vector3.zero, Quaternion.identity);

        // This line replicates the object to all connected clients (User 2) automatically!
        spawnedAssessment.GetComponent<NetworkObject>().Spawn();
    }
}