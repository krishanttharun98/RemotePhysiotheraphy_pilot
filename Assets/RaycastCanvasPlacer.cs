using UnityEngine;

public class RaycastCanvasPlacer : MonoBehaviour
{
    [Header("Raycast Tracking Settings")]
    [Tooltip("Assign the transform of your interacting hand controller (the one shooting the raycast pointer).")]
    public Transform pointerHand;
    
    [Tooltip("Maximum distance out into your room that you can shoot the placement raycast.")]
    public float maxPlacementDistance = 5f;

    [Tooltip("How far out the canvas should float if the raycast doesn't hit any physical walls or floors.")]
    public float defaultFloatingDistance = 1.5f;

    [Tooltip("LayerMask to filter what surfaces the canvas can snap to. Leave as 'Everything' by default.")]
    public LayerMask surfaceLayerMask = ~0;

    [Header("Visual Offset Adjustments")]
    [Tooltip("Pushes the canvas slightly away from walls so it doesn't clip inside them.")]
    public float wallOffsetDistance = 0.02f;

    private bool _isPlacingModeActive = true;

    void Update()
    {
        // If we have already clicked and locked it down, stop calculation checks entirely
        if (!_isPlacingModeActive) return;

        HandleRaycastMovement();
        HandlePlacementInput();
    }

    void HandleRaycastMovement()
    {
        if (pointerHand == null) return;

        Ray ray = new Ray(pointerHand.position, pointerHand.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxPlacementDistance, surfaceLayerMask))
        {
            // Position the canvas exactly at the intersection point, but pull it back slightly along the surface normal
            transform.position = hit.point + (hit.normal * wallOffsetDistance);
            
            // Make the canvas look perfectly flush against the surface or snap straight back to face the camera
            transform.rotation = Quaternion.LookRotation(-hit.normal, Vector3.up);
        }
        else
        {
            // Fallback: If you aim into completely empty space, let the canvas float comfortably in front of your hand
            Vector3 targetPosition = pointerHand.position + (pointerHand.forward * defaultFloatingDistance);
            transform.position = targetPosition;
            
            // Keep the canvas facing you smoothly while floating
            Vector3 lookDirection = transform.position - Camera.main.transform.position;
            lookDirection.y = 0; // Lock the roll axis to avoid weird tilting
            if (lookDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            }
        }
    }

    void HandlePlacementInput()
    {
        // Listen for standard Meta Quest Touch index trigger clicks or a Spacebar press on desktop
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch) || 
            OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch) || 
            Input.GetKeyDown(KeyCode.Space))
        {
            LockCanvasPosition();
        }
    }

    public void LockCanvasPosition()
    {
        _isPlacingModeActive = false;
        Debug.Log($"[{Time.time:F2}s] UI Canvas successfully locked at position: {transform.position}");
    }

    /// <summary>
    /// Call this public method from your developer UI settings menu to pick up and move the canvas again.
    /// </summary>
    public void UnlockAndReposition()
    {
        _isPlacingModeActive = true;
        Debug.Log($"[{Time.time:F2}s] Placement mode re-enabled. Aim to relocate canvas.");
    }
}