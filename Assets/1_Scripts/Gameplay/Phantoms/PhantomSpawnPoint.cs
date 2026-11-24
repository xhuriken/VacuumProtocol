using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gameplay.Phantoms
{
    /// <summary>
    /// Description: Represents an oriented surface spawn portal (wall, ceiling, or floor) from which Phantoms emerge.
    /// Context: Placed around rooms as children of PhantomRoomSpawner.
    /// Justification: Orients the phantom along the surface normal (transform.forward) so it visually crawls or drifts out of the wall/floor/ceiling.
    /// Features a one-click 'Snap to Nearest Surface' editor tool for fast, professional level design.
    /// </summary>
    [SelectionBase]
    public class PhantomSpawnPoint : MonoBehaviour
    {
        [Header("Surface Settings")]
        [Tooltip("Role: Layers considered valid surfaces for snapping (walls, floors, ceilings).")]
        [SerializeField] private LayerMask _surfaceLayer;

        [Header("Gizmo Settings")]
        [Tooltip("Role: Radius of the visual spectral disc on the surface.")]
        [SerializeField] private float _discRadius = 0.6f;

        [Tooltip("Role: Color of the portal disk in the scene view.")]
        [SerializeField] private Color _portalColor = new Color(0.65f, 0.2f, 1f, 0.35f);

        [Tooltip("Role: Length of the emergence vector arrow in the scene view.")]
        [SerializeField] private float _arrowLength = 1.0f;

        /// <summary>
        /// Direction pointing perpendicularly outward from the surface into the room.
        /// </summary>
        public Vector3 EmergenceDirection => transform.forward;

        /// <summary>
        /// Exact world position on the surface.
        /// </summary>
        public Vector3 SurfacePosition => transform.position;

        private void Reset()
        {
            _surfaceLayer = LayerMask.GetMask("Default", "Ground", "Obstacle", "Obstable");
            if (_surfaceLayer.value == 0)
            {
                _surfaceLayer = (1 << 0) | (1 << 3) | (1 << 8);
            }
        }

        #region Level Design Utilities
        /// <summary>
        /// Description: Casts rays in all 6 cardinal directions to locate the nearest wall, floor, or ceiling,
        /// then snaps position and aligns transform.forward with the surface normal.
        /// </summary>
        [Button("Snap to Nearest Surface", ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.9f, 0.4f)]
        public void SnapToNearestSurface()
        {
            if (_surfaceLayer.value == 0)
            {
                _surfaceLayer = LayerMask.GetMask("Default", "Ground", "Obstacle", "Obstable");
                if (_surfaceLayer.value == 0) _surfaceLayer = (1 << 0) | (1 << 3) | (1 << 8);
            }

            Vector3[] directions = new Vector3[]
            {
                Vector3.down,
                Vector3.up,
                transform.forward,
                -transform.forward,
                transform.right,
                -transform.right
            };

            float closestDist = float.MaxValue;
            RaycastHit bestHit = default;
            bool hitFound = false;

            for (int i = 0; i < directions.Length; i++)
            {
                if (Physics.Raycast(transform.position, directions[i], out RaycastHit hit, 15f, _surfaceLayer, QueryTriggerInteraction.Ignore))
                {
                    if (hit.distance < closestDist)
                    {
                        closestDist = hit.distance;
                        bestHit = hit;
                        hitFound = true;
                    }
                }
            }

            if (hitFound)
            {
#if UNITY_EDITOR
                Undo.RecordObject(transform, "Snap Spawn Point to Surface");
#endif
                // Position slightly offset along normal to prevent z-fighting
                transform.position = bestHit.point + bestHit.normal * 0.02f;

                // Align forward with normal (emergence direction), and up with world up (or forward if vertical)
                Vector3 upward = Mathf.Abs(bestHit.normal.y) > 0.85f ? Vector3.forward : Vector3.up;
                transform.rotation = Quaternion.LookRotation(bestHit.normal, upward);

                Debug.Log($"<color=green>[PhantomSpawnPoint]</color> Snapped '{name}' to surface '{bestHit.collider.name}' (Normal: {bestHit.normal}).");
            }
            else
            {
                Debug.LogWarning($"[PhantomSpawnPoint] Could not find any surface within 15m of '{name}'.");
            }
        }
        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Vector3 center = transform.position;
            Vector3 normal = transform.forward;

            // 1. Draw flat circular portal disc against surface
            Handles.color = _portalColor;
            Handles.DrawSolidDisc(center, normal, _discRadius);

            Handles.color = new Color(_portalColor.r, _portalColor.g, _portalColor.b, 0.9f);
            Handles.DrawWireDisc(center, normal, _discRadius);

            // 2. Draw emergence arrow pointing into the room
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(center, normal * _arrowLength);
            Gizmos.DrawWireSphere(center + normal * _arrowLength, 0.08f);

            // 3. Status label
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.magenta;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 10;
            Handles.Label(center + normal * (_arrowLength + 0.2f), $"[SPAWN PORTAL: {gameObject.name}]", style);
        }
#endif
    }
}
