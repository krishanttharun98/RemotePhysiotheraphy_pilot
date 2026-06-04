using UnityEngine;
using Oculus.Interaction;

public class GrabToDestroy : MonoBehaviour
{
    private IPointable _pointable;

    void Awake()
    {
        _pointable = GetComponent<IPointableElement>() as IPointable;
    }

    void OnEnable()
    {
        if (_pointable != null) _pointable.WhenPointerEventRaised += HandleGrab;
    }

    void OnDisable()
    {
        if (_pointable != null) _pointable.WhenPointerEventRaised -= HandleGrab;
    }

    private void HandleGrab(PointerEvent args)
    {
        // Select = The moment the pinch/grab is completed
        if (args.Type == PointerEventType.Select)
        {
            Debug.Log("Research Data: Sphere Captured");
            Destroy(gameObject);
        }
    }
}