using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VRCanvasController : MonoBehaviour
{
    [Header("Spawn Settings")]
    public Transform headsetCamera;
    public float spawnDistance = 1.2f;
    public float spawnHeightOffset = -0.1f;

    [Header("Raycast Controller")]
    public Transform rightController;
    public Transform leftController;
    public float raycastDistance = 100f;

    private Canvas canvas;
    private RectTransform canvasRect;
    private BoxCollider canvasCollider;

    void Start()
    {
        SpawnInFrontOfHeadset();
        SetupRaycastInteraction();
    }

    public void SpawnInFrontOfHeadset()
    {
        if (headsetCamera == null)
        {
            headsetCamera = Camera.main.transform;
        }

        Vector3 targetPosition = headsetCamera.position + (headsetCamera.forward * spawnDistance);
        targetPosition.y += spawnHeightOffset;
        
        transform.position = targetPosition;

        Vector3 forwardLook = headsetCamera.forward;
        forwardLook.y = 0;
        if (forwardLook != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(forwardLook.normalized);
        }
    }

    void SetupRaycastInteraction()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
        }

        canvasRect = GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            canvasRect = gameObject.AddComponent<RectTransform>();
        }

        canvasCollider = GetComponent<BoxCollider>();
        if (canvasCollider == null)
        {
            canvasCollider = gameObject.AddComponent<BoxCollider>();
            canvasCollider.size = new Vector3(800, 600, 10);
            canvasCollider.isTrigger = true;
        }

        if (EventSystem.current == null)
        {
            GameObject eventSystemGO = new GameObject("EventSystem");
            EventSystem eventSystem = eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<StandaloneInputModule>();
        }
    }

    void Update()
    {
        HandleRaycastInteraction();
    }

    void HandleRaycastInteraction()
    {
        if (rightController != null)
        {
            PerformRaycast(rightController, OVRInput.RawButton.RIndexTrigger);
        }

        if (leftController != null)
        {
            PerformRaycast(leftController, OVRInput.RawButton.LIndexTrigger);
        }
    }

    void PerformRaycast(Transform controller, OVRInput.RawButton triggerButton)
    {
        Ray ray = new Ray(controller.position, controller.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance))
        {
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
            {
                PointerEventData pointerData = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };

                if (OVRInput.GetDown(triggerButton))
                {
                    ExecuteEvents.Execute(hit.collider.gameObject, pointerData, ExecuteEvents.pointerClickHandler);
                    Debug.Log("Canvas clicked via controller raycast");
                }
            }
        }
    }
}