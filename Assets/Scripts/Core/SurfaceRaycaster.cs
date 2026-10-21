using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Finds a real-world surface under a screen point. On a phone it raycasts against detected AR planes;
/// in desktop simulation it raycasts against the simulated room's colliders.
/// </summary>
public class SurfaceRaycaster : MonoBehaviour
{
    public ARRaycastManager raycastManager;
    public ARPlaneManager planeManager;
    public Camera arCamera;

    static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

    public bool UsingSimulation => SimulationMode.Active;

    public bool TryHit(Vector2 screenPoint, out Pose pose, out bool verticalSurface)
    {
        verticalSurface = false;
        if (!UsingSimulation && raycastManager && raycastManager.Raycast(screenPoint, hits, TrackableType.PlaneWithinPolygon))
        {
            ARRaycastHit hit = hits[0];
            pose = hit.pose;
            if (planeManager)
            {
                ARPlane plane = planeManager.GetPlane(hit.trackableId);
                verticalSurface = plane && plane.alignment == PlaneAlignment.Vertical;
            }
            return true;
        }

        if (UsingSimulation && arCamera)
        {
            Ray ray = arCamera.ScreenPointToRay(screenPoint);
            foreach (RaycastHit h in Physics.RaycastAll(ray, 50f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!h.collider.GetComponentInParent<SimulatedSurface>()) continue;
                verticalSurface = Mathf.Abs(h.normal.y) < 0.5f;
                Vector3 forward = Vector3.ProjectOnPlane(arCamera.transform.forward, h.normal);
                if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(Vector3.forward, h.normal);
                pose = new Pose(h.point, Quaternion.LookRotation(forward.normalized, h.normal));
                return true;
            }
        }
        pose = default;
        return false;
    }

    /// <summary>Finds a placed object under a screen point (objects carry colliders in both modes).</summary>
    public T PickObject<T>(Vector2 screenPoint) where T : Component
    {
        if (!arCamera) return null;
        Ray ray = arCamera.ScreenPointToRay(screenPoint);
        float best = float.MaxValue;
        T found = null;
        foreach (RaycastHit h in Physics.RaycastAll(ray, 50f, ~0, QueryTriggerInteraction.Ignore))
        {
            T t = h.collider.GetComponentInParent<T>();
            if (t && h.distance < best)
            {
                best = h.distance;
                found = t;
            }
        }
        return found;
    }

    public int PlaneCount => UsingSimulation ? FindObjectsByType<SimulatedSurface>().Length : planeManager ? planeManager.trackables.count : 0;
}
