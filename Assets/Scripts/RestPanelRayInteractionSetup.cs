using Oculus.Interaction;
using UnityEngine;
using UnityEngine.UI;

// Adds Meta ISDK ray-canvas interaction to the rest/next panel so the Resume
// button receives RayInteractor pointer clicks. Clones an existing
// ISDK_RayCanvasInteraction from the same parent canvas when present.
[DisallowMultipleComponent]
public class RestPanelRayInteractionSetup : MonoBehaviour
{
    [Tooltip("Optional template. If null, searches sibling panels on the same Canvas.")]
    public RectTransform rayCanvasTemplate;

    void Awake()
    {
        if (GetComponentInChildren<PointableCanvas>(true) != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[RestPanel] No parent Canvas found for ray interaction setup.");
            return;
        }

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        // Remove the wrong grab-only interactable if it was added by mistake.
        Transform wrongGrab = transform.Find("ISDK_RayGrabInteraction");
        if (wrongGrab != null) Destroy(wrongGrab.gameObject);

        RectTransform template = rayCanvasTemplate != null ? rayCanvasTemplate : FindSiblingRayCanvasTemplate();
        if (template == null)
        {
            Debug.LogWarning("[RestPanel] No ISDK_RayCanvasInteraction template found on this canvas.");
            return;
        }

        GameObject rayGo = Instantiate(template.gameObject, transform);
        rayGo.name = "ISDK_RayCanvasInteraction";
        ResetStretchRect(rayGo.GetComponent<RectTransform>());

        PointableCanvas pointable = rayGo.GetComponent<PointableCanvas>();
        if (pointable != null) pointable.InjectCanvas(canvas);
    }

    RectTransform FindSiblingRayCanvasTemplate()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return null;

        foreach (PointableCanvas pc in canvas.GetComponentsInChildren<PointableCanvas>(true))
        {
            if (pc.transform == transform || pc.transform.IsChildOf(transform)) continue;
            if (pc.name != "ISDK_RayCanvasInteraction") continue;
            return pc.GetComponent<RectTransform>();
        }
        return null;
    }

    static void ResetStretchRect(RectTransform rt)
    {
        if (rt == null) return;
        rt.localPosition = Vector3.zero;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }
}
