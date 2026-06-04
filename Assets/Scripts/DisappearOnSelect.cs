using UnityEngine;
using System;

public class DisappearOnSelect : MonoBehaviour
{
    public GameObject bubbleVisual; 
    public AudioClip popClip;
    public Action<bool> OnDestroyed; 

    [Header("Fresh Splash Settings")]
    [Tooltip("Drag your Water Splash PREFAB from your Project folders here.")]
    public GameObject waterSplashPrefab;

    private bool _isPopped = false;

    // The Manager script calls this function directly!
    public void Pop(bool isRightHand)
    {
        if (_isPopped) return;
        _isPopped = true;

        OnDestroyed?.Invoke(isRightHand);

        // 1. Play Audio clip if available
        if (popClip != null) AudioSource.PlayClipAtPoint(popClip, transform.position);

        // 2. Spawn a fresh copy of our splash effect prefab right at the bubble's position
        if (waterSplashPrefab != null)
        {
            // Anchors the splash to the exact same parent coordinate space as the bubble
            GameObject splashClone = Instantiate(waterSplashPrefab, transform.position, Quaternion.identity, transform.parent);
            
            // Forces the splash clone to perfectly match the custom size of this bubble
            splashClone.transform.localScale = transform.localScale;
        }
        else
        {
            Debug.LogWarning("Water Splash Prefab is not assigned inside the Water Bubble asset prefab slot!");
        }

        // 3. Hide the visual immediately
        if (bubbleVisual != null) bubbleVisual.SetActive(false);
        
        // 4. Clean up and remove this bubble object entirely
        Destroy(gameObject); 
    }
}