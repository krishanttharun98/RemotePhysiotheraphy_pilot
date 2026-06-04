using UnityEngine;

public class TouchToDestroy : MonoBehaviour
{
    // This will trigger when the Kinect Proxy Hand OR Quest Hand touches it
    private void OnTriggerEnter(Collider other)
    {
        // Check if the object touching it is a 'Hand' (Tag your Hand/Proxies as "Player")
        if (other.CompareTag("Hand") || other.name.Contains("Proxy"))
        {
            OnTouched();
        }
    }

    public void OnTouched()
    {
        // Play a small sound or effect here if needed
        Debug.Log(gameObject.name + " was touched!");
        
        // Make it disappear
        gameObject.SetActive(false);
        
        // Optional: Re-spawn or count score
    }
}