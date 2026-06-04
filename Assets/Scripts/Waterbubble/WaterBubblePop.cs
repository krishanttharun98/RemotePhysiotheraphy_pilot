using UnityEngine;

public class WaterBubblePop : MonoBehaviour
{
    [Header("Effect Prefabs")]
    [Tooltip("The water splash particle system prefab to spawn.")]
    [SerializeField] private GameObject waterSplashPrefab;

    [Header("Quest 3 Controller References")]
    [Tooltip("Drag the Left Controller GameObject from your hierarchy here.")]
    [SerializeField] private GameObject leftController;
    
    [Tooltip("Drag the Right Controller GameObject from your hierarchy here.")]
    [SerializeField] private GameObject rightController;

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering the bubble is either the left or right controller slot
        if (other.gameObject == leftController || other.gameObject == rightController)
        {
            TriggerSplash();
        }
        else if (other.CompareTag("GameController"))
        {
            TriggerSplash();
        }

    }

    private void TriggerSplash()
    {
        if (waterSplashPrefab != null)
        {
            // Spawn the splash at the bubble's exact position
            Instantiate(waterSplashPrefab, transform.position, transform.rotation);
        }

        // Destroy the bubble
        Destroy(gameObject);
    }
}
