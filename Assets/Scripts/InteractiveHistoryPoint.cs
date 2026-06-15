using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class InteractiveHistoryPoint : MonoBehaviour
{
    private List<Vector3> _recordedPath = new List<Vector3>();
    private LineRenderer _lineRenderer;
    
    [Header("Interaction Settings")]
    public float activationDistance = 0.12f; // Proximity trigger comfort window
    private Transform _leftHand;
    private Transform _rightHand;
    private bool _isHoveredByRay = false;

    public void Initialize(List<Vector3> worldSpacePath, Transform left, Transform right)
    {
        _recordedPath = new List<Vector3>(worldSpacePath);
        _leftHand = left;
        _rightHand = right;
        
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.positionCount = 0;
        _lineRenderer.startWidth = 0.008f;
        _lineRenderer.endWidth = 0.008f;
        
        // Premium clinical asset styling (Bright Cyan trajectory pathing)
        _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _lineRenderer.startColor = Color.cyan;
        _lineRenderer.endColor = Color.blue;
        
        _lineRenderer.enabled = false;
    }

    void Update()
    {
        bool handIsNear = false;
        if (_leftHand != null && Vector3.Distance(transform.position, _leftHand.position) < activationDistance) handIsNear = true;
        if (_rightHand != null && Vector3.Distance(transform.position, _rightHand.position) < activationDistance) handIsNear = true;

        if (handIsNear || _isHoveredByRay)
        {
            DrawTrajectory();
        }
        else
        {
            HideTrajectory();
        }
    }

    public void DrawTrajectory()
    {
        if (_recordedPath == null || _recordedPath.Count == 0) return;
        
        _lineRenderer.enabled = true;
        _lineRenderer.positionCount = _recordedPath.Count;
        _lineRenderer.SetPositions(_recordedPath.ToArray());
    }

    public void HideTrajectory()
    {
        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = false;
        }
    }

    public void OnRayEnter() => _isHoveredByRay = true;
    public void OnRayExit() => _isHoveredByRay = false;
}