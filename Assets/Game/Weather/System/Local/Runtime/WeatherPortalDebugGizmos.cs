using System.Collections.Generic;
using UnityEngine;

namespace MSC.Weather.System.Local
{
    /// <summary>Editor gizmos for topology, direction, leakage and current path.</summary>
    [DisallowMultipleComponent]
    public sealed class WeatherPortalDebugGizmos : MonoBehaviour
    {
        [SerializeField] private WeatherPortalSystem portalSystem;
        [SerializeField] private InteriorZoneResolver resolver;
        [SerializeField] private bool drawAllConnections = true;
        [SerializeField] private bool drawCurrentOutdoorPath = true;
        [SerializeField, Min(0.05f)] private float endpointRadius = 0.15f;

        private readonly List<IEnvironmentPortal> currentPath =
            new List<IEnvironmentPortal>(8);

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            WeatherPortalSystem graph = portalSystem != null
                ? portalSystem
                : WeatherPortalSystem.Active;
            if (graph == null)
            {
                return;
            }

            if (drawAllConnections)
            {
                IReadOnlyList<IEnvironmentPortal> portals = graph.Portals;
                for (int index = 0; index < portals.Count; index++)
                {
                    DrawPortal(portals[index], false);
                }
            }

            if (drawCurrentOutdoorPath && resolver != null &&
                graph.TryBuildStrongestVisualPath(
                    resolver.CurrentZone,
                    currentPath))
            {
                for (int index = 0; index < currentPath.Count; index++)
                {
                    DrawPortal(currentPath[index], true);
                }
            }
        }

        private void DrawPortal(IEnvironmentPortal portal, bool current)
        {
            if (portal == null || portal.OpeningTransform == null)
            {
                return;
            }

            Vector3 opening = portal.OpeningTransform.position;
            float openness = Mathf.Clamp01(portal.Openness01);
            Gizmos.color = current
                ? new Color(1f, 0.25f, 0.95f, 1f)
                : Color.Lerp(
                    new Color(0.9f, 0.15f, 0.08f, 0.65f),
                    new Color(0.1f, 0.9f, 0.35f, 0.8f),
                    openness);
            Gizmos.DrawWireSphere(opening, current
                ? endpointRadius * 1.5f
                : endpointRadius);
            Gizmos.DrawRay(
                opening,
                portal.OpeningTransform.forward *
                Mathf.Max(0.35f, portal.OpeningSizeMeters.x * 0.5f));
            DrawEndpoint(opening, portal.ZoneA);
            DrawEndpoint(opening, portal.ZoneB);
        }

        private static void DrawEndpoint(
            Vector3 opening,
            IInteriorZone zone)
        {
            if (zone is Component component && component != null)
            {
                Gizmos.DrawLine(opening, component.transform.position);
                return;
            }

            // Outdoor endpoint: a short upward tick keeps the topology visible
            // without inventing a world-space Outdoor object.
            Gizmos.DrawLine(opening, opening + Vector3.up * 0.5f);
        }
#endif
    }
}
