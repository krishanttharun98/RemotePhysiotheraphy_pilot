using UnityEngine;

public class WaterBubble : MonoBehaviour
{
    [Header("Effects")]
    [Tooltip("Assign your Splash Particle System prefab or scene instance here.")]
    public ParticleSystem splashParticleSystem;

    [Tooltip("How many droplets burst out when the bubble pops.")]
    public int splashParticleCount = 30;

    /// <summary>
    /// Call this function from your interaction system, collision logic, 
    /// or animation events to pop the bubble.
    /// </summary>
    public void PopBubble()
    {
        if (splashParticleSystem != null)
        {
            // 1. Move the particle system to the bubble's exact current position
            splashParticleSystem.transform.position = transform.position;

            // 2. Force an instant burst of particles
            splashParticleSystem.Emit(splashParticleCount);
        }
        else
        {
            Debug.LogWarning($"Splash Particle System is missing on {gameObject.name}!");
        }

        // 3. Destroy or disable the bubble mesh so it disappears
        // If you are reusing bubbles via pooling, use gameObject.SetActive(false); instead!
        Destroy(gameObject);
    }

    // Example testing logic: Pops the bubble if it hits any surface
    private void OnCollisionEnter(Collision collision)
    {
        PopBubble();
    }

    // Example testing logic: Pops the bubble if a hand trigger enters it
    private void OnTriggerEnter(Collider other)
    {
        // Change "Hand" to match your avatar's hand collider tag if necessary
        if (other.CompareTag("Hand") || other.name.Contains("Hand"))
        {
            PopBubble();
        }
    }
}