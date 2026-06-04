using Unity.Netcode;
using UnityEngine;

public class AutoStartHost : MonoBehaviour
{
    void Start()
    {
        // Find the active NetworkManager in the scene
        NetworkManager netManager = NetworkManager.Singleton;

        if (netManager != null)
        {
            // For standalone headset builds, we force it to become the Host instantly
            netManager.StartHost();
            Debug.Log("Network Manager started hosting automatically!");
        }
        else
        {
            Debug.LogError("Could not find NetworkManager in the scene!");
        }
    }
}