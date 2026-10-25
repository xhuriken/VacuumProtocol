using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gameplay.Phantoms
{
    /// <summary>
    /// Description: Dynamic real-time visual debugging tool (Gizmos) for Phantom entities.
    /// Context: Attached to Phantom prefabs alongside PhantomBase (pattern identical to PlayerV2_Gizmos).
    /// Justification: Visualizes FOV cones (solid transparent), live sightlines to players, obstacle blockage points,
    /// last-known-position investigation markers, and NavMesh paths in the Unity Scene View with zero runtime overhead in player builds.
    /// </summary>
    [RequireComponent(typeof(PhantomBase))]
    [ExecuteInEditMode]
    public class PhantomGizmos : MonoBehaviour
    {
        [Header("Gizmo Modules")]
        [BoxGroup("Modules")] public bool ShowVisionCone = true;
        [BoxGroup("Modules")] public bool ShowPlayerSightlines = true;
        [BoxGroup("Modules")] public bool ShowObstacleHitPoints = true;
        [BoxGroup("Modules")] public bool ShowLastKnownPosition = true;
        [BoxGroup("Modules")] public bool ShowNavMeshPath = true;
        [BoxGroup("Modules")] public bool ShowDrainAura = true;
        [BoxGroup("Modules")] public bool ShowStateBadge = true;
        [BoxGroup("Modules")] public bool ShowEscapeRaycasts = true;

        [Header("Display Settings")]
        [BoxGroup("Display")]
        [Tooltip("When true, gizmos only draw when the phantom is selected in the hierarchy. When false, draws continuously.")]
        public bool OnlyWhenSelected = false;

        [BoxGroup("Display")]
        [Range(0.05f, 0.6f)]
        [Tooltip("Transparency multiplier for the solid vision cone.")]
        public float ConeTransparency = 0.18f;

        [BoxGroup("Display")]
        [Tooltip("Enables pulsation in chase and investigation states.")]
        public bool EnableDynamicPulsing = true;

        private PhantomBase _phantom;
        private NavMeshAgent _agent;

        private void OnEnable()
        {
            _phantom = GetComponent<PhantomBase>();
            _agent = GetComponent<NavMeshAgent>();

            if (_phantom == null)
            {
                Debug.LogError($"[PhantomGizmos] Critical Error: Missing 'PhantomBase' on '{gameObject.name}'! Fail loudly per workspace rules.");
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (OnlyWhenSelected) return;
            RenderAllGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (!OnlyWhenSelected) return;
            RenderAllGizmos();
        }

        private void RenderAllGizmos()
        {
            if (_phantom == null)
            {
                _phantom = GetComponent<PhantomBase>();
                if (_phantom == null) return;
            }

            if (_agent == null)
            {
                _agent = GetComponent<NavMeshAgent>();
            }

            Vector3 eyeOrigin = _phantom.LookAtPoint != null 
                ? _phantom.LookAtPoint.position 
                : (transform.position + Vector3.up * 1.5f);

            // Get dynamic colors based on current state
            GetStateColors(_phantom.CurrentState, out Color coneSolidColor, out Color coneWireColor);

            // 1. Vision Cone (Solid Transparent + Wireframe 3D)
            if (ShowVisionCone)
            {
                DrawVisionCone(eyeOrigin, coneSolidColor, coneWireColor);
            }

            // 2. Real-time Player Sightlines & Obstacle Hits
            if (ShowPlayerSightlines)
            {
                DrawPlayerSightlines(eyeOrigin);
            }

            // 3. Last Known Position Investigation Marker
            if (ShowLastKnownPosition && _phantom.HasLastKnownPosition)
            {
                DrawLastKnownPositionMarker(eyeOrigin);
            }

            // 4. NavMesh Agent Real-time Path
            if (ShowNavMeshPath && _agent != null && _agent.hasPath)
            {
                DrawNavMeshPath(coneWireColor);
            }

            // 5. Terror Proximity Drain Aura
            if (ShowDrainAura && _phantom.EnableProximityDrain)
            {
                DrawDrainAura();
            }

            // 6. Intelligent Escape Raycasts & Chosen Dash Trajectory
            if (ShowEscapeRaycasts)
            {
                DrawEscapeRaycasts();
            }

            // 7. Floating Status Badge Label
            if (ShowStateBadge)
            {
                DrawStateBadge(coneWireColor);
            }
        }

        /// <summary>
        /// Description: Determines state-specific colors and dynamic pulsing.
        /// </summary>
        private void GetStateColors(PhantomState state, out Color solidColor, out Color wireColor)
        {
            float pulse = 0f;
            if (EnableDynamicPulsing && Application.isEditor)
            {
                pulse = Mathf.Sin((float)EditorApplication.timeSinceStartup * 5f) * 0.5f + 0.5f;
            }

            switch (state)
            {
                case PhantomState.Idle:
                    solidColor = new Color(0.2f, 0.75f, 1f, ConeTransparency);
                    wireColor = new Color(0.25f, 0.85f, 1f, 0.85f);
                    break;

                case PhantomState.Wandering:
                    solidColor = new Color(0.25f, 0.95f, 0.45f, ConeTransparency);
                    wireColor = new Color(0.3f, 1f, 0.5f, 0.85f);
                    break;

                case PhantomState.Chasing:
                    // Intense pulsating scarlet red
                    float chaseAlpha = Mathf.Clamp01(ConeTransparency * 1.6f + pulse * 0.15f);
                    solidColor = new Color(1f, 0.12f, 0.15f, chaseAlpha);
                    wireColor = Color.Lerp(new Color(1f, 0.15f, 0.15f, 1f), Color.white, pulse * 0.35f);
                    break;

                case PhantomState.Investigating:
                    // Amber / Alert Orange
                    float investAlpha = Mathf.Clamp01(ConeTransparency * 1.4f + pulse * 0.1f);
                    solidColor = new Color(1f, 0.6f, 0.05f, investAlpha);
                    wireColor = new Color(1f, 0.7f, 0.1f, 0.95f);
                    break;

                case PhantomState.Attacking:
                    solidColor = new Color(1f, 0f, 0.45f, ConeTransparency * 1.8f);
                    wireColor = new Color(1f, 0.1f, 0.5f, 1f);
                    break;

                case PhantomState.BeingVacuumed:
                    float vacAlpha = Mathf.Clamp01(ConeTransparency * 2f + pulse * 0.25f);
                    solidColor = new Color(0.6f, 0.1f, 1f, vacAlpha);
                    wireColor = Color.Lerp(new Color(0.8f, 0.2f, 1f, 1f), Color.cyan, pulse);
                    break;

                default:
                    solidColor = new Color(1f, 1f, 1f, ConeTransparency);
                    wireColor = Color.white;
                    break;
            }
        }

        /// <summary>
        /// Description: Draws a 3D volumetric transparent solid arc representing the FOV.
        /// </summary>
        private void DrawVisionCone(Vector3 eyeOrigin, Color solidColor, Color wireColor)
        {
            float fovAngle = _phantom.FieldOfViewAngle;
            float detectionRadius = _phantom.DetectionRadius;
            Vector3 forward = transform.forward;

            Vector3 leftBoundary = Quaternion.AngleAxis(-fovAngle / 2f, Vector3.up) * forward;
            Vector3 rightBoundary = Quaternion.AngleAxis(fovAngle / 2f, Vector3.up) * forward;

            Vector3 groundCenter = transform.position + Vector3.up * 0.04f;

            // 1. Solid Transparent Arc on the ground
            Handles.color = solidColor;
            Handles.DrawSolidArc(groundCenter, Vector3.up, leftBoundary, fovAngle, detectionRadius);

            // 2. Wireframe arc border
            Handles.color = wireColor;
            Handles.DrawWireArc(groundCenter, Vector3.up, leftBoundary, fovAngle, detectionRadius);

            // Ground boundary lines
            Gizmos.color = wireColor;
            Gizmos.DrawLine(groundCenter, groundCenter + leftBoundary * detectionRadius);
            Gizmos.DrawLine(groundCenter, groundCenter + rightBoundary * detectionRadius);

            // 3. 3D pyramid rays from eye point down to cone edges
            Color rayColor = new Color(wireColor.r, wireColor.g, wireColor.b, 0.35f);
            Gizmos.color = rayColor;
            Gizmos.DrawLine(eyeOrigin, groundCenter + leftBoundary * detectionRadius);
            Gizmos.DrawLine(eyeOrigin, groundCenter + rightBoundary * detectionRadius);
            Gizmos.DrawLine(eyeOrigin, groundCenter + forward * detectionRadius);

            // 4. Subtle lose target boundary when chasing
            if (_phantom.CurrentState == PhantomState.Chasing)
            {
                Handles.color = new Color(1f, 0.2f, 0.2f, 0.2f);
                Handles.DrawWireArc(groundCenter, Vector3.up, leftBoundary, fovAngle, _phantom.LoseTargetRadius);
            }
        }

        /// <summary>
        /// Description: Visualizes live line-of-sight tracking to all players in the scene.
        /// </summary>
        private void DrawPlayerSightlines(Vector3 eyeOrigin)
        {
            IReadOnlyList<PlayerV2_Controller> players = PlayerV2_Controller.ActivePlayers;
            if (players == null || players.Count == 0) return;

            GUIStyle targetLabelStyle = new GUIStyle();
            targetLabelStyle.normal.textColor = Color.red;
            targetLabelStyle.fontStyle = FontStyle.Bold;
            targetLabelStyle.fontSize = 11;

            GUIStyle visibleLabelStyle = new GUIStyle();
            visibleLabelStyle.normal.textColor = Color.green;
            visibleLabelStyle.fontStyle = FontStyle.Bold;
            visibleLabelStyle.fontSize = 10;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerV2_Controller player = players[i];
                if (player == null || !player.gameObject.activeInHierarchy) continue;

                Vector3 targetPoint = player.LookAtPoint != null 
                    ? player.LookAtPoint.position 
                    : (player.WorldPosition + Vector3.up * 0.8f);

                Vector3 directionToPlayer = targetPoint - eyeOrigin;
                float distance = directionToPlayer.magnitude;

                bool isVisible = _phantom.IsPlayerVisible(player, out float visibleDist);

                if (isVisible)
                {
                    if (player == _phantom.CurrentTarget)
                    {
                        // Active Chased Target: Bold Red Line + Target Disc
                        Handles.color = new Color(1f, 0.1f, 0.15f, 1f);
                        Handles.DrawLine(eyeOrigin, targetPoint);

                        // Pulsing target ring
                        Handles.color = Color.red;
                        Handles.DrawWireDisc(targetPoint + Vector3.up * 0.4f, Vector3.up, 0.35f);
                        Handles.DrawWireDisc(targetPoint + Vector3.up * 0.4f, Vector3.forward, 0.35f);
                        Handles.Label(targetPoint + Vector3.up * 0.65f, $"[TARGET: {player.Name} ({distance:F1}m)]", targetLabelStyle);
                    }
                    else
                    {
                        // Visible in FOV: Vibrant Green Line
                        Handles.color = new Color(0.35f, 1f, 0.25f, 0.85f);
                        Handles.DrawLine(eyeOrigin, targetPoint);
                        Handles.DrawWireDisc(targetPoint + Vector3.up * 0.3f, Vector3.up, 0.2f);
                        Handles.Label(targetPoint + Vector3.up * 0.5f, $"[VISIBLE: {player.Name} ({distance:F1}m)]", visibleLabelStyle);
                    }
                }
                else if (ShowObstacleHitPoints && distance <= _phantom.DetectionRadius)
                {
                    // Player is in distance range, but obstructed by a wall!
                    if (Physics.Raycast(eyeOrigin, directionToPlayer.normalized, out RaycastHit hit, distance, _phantom.ObstacleLayer, QueryTriggerInteraction.Ignore))
                    {
                        if (!hit.transform.IsChildOf(player.transform))
                        {
                            // 1. Ray to the obstacle wall
                            Handles.color = new Color(1f, 0.3f, 0.3f, 0.5f);
                            Handles.DrawLine(eyeOrigin, hit.point);

                            // 2. Obstacle Impact point marker
                            Gizmos.color = Color.red;
                            Gizmos.DrawWireSphere(hit.point, 0.12f);

                            // 3. Dashed line from wall to hidden player
                            Handles.color = new Color(0.6f, 0.6f, 0.6f, 0.35f);
                            Handles.DrawDottedLine(hit.point, targetPoint, 4f);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Description: Highlights the last known position when in or entering investigation mode.
        /// </summary>
        private void DrawLastKnownPositionMarker(Vector3 eyeOrigin)
        {
            Vector3 lastPos = _phantom.LastKnownPosition;
            float pulse = 0f;
            if (EnableDynamicPulsing && Application.isEditor)
            {
                pulse = Mathf.Sin((float)EditorApplication.timeSinceStartup * 4f) * 0.12f;
            }

            // Ground Disc
            Handles.color = new Color(1f, 0.6f, 0f, 0.3f);
            Handles.DrawSolidDisc(lastPos + Vector3.up * 0.02f, Vector3.up, 0.75f + pulse);

            Handles.color = new Color(1f, 0.7f, 0f, 0.95f);
            Handles.DrawWireDisc(lastPos + Vector3.up * 0.02f, Vector3.up, 0.75f + pulse);

            // Vertical Holographic Beacon
            Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.85f);
            Gizmos.DrawLine(lastPos, lastPos + Vector3.up * 2.5f);
            Gizmos.DrawWireSphere(lastPos + Vector3.up * 2.5f, 0.15f);

            // Dotted trail between phantom and destination
            Handles.color = new Color(1f, 0.65f, 0.1f, 0.75f);
            Handles.DrawDottedLine(eyeOrigin, lastPos, 5f);

            GUIStyle investStyle = new GUIStyle();
            investStyle.normal.textColor = new Color(1f, 0.75f, 0.1f);
            investStyle.fontStyle = FontStyle.Bold;
            investStyle.fontSize = 10;

            Handles.Label(lastPos + Vector3.up * 2.7f, "[LAST KNOWN POSITION]", investStyle);
        }

        /// <summary>
        /// Description: Renders the active NavMesh path to visualize the agent's route.
        /// </summary>
        private void DrawNavMeshPath(Color pathColor)
        {
            Vector3[] corners = _agent.path.corners;
            if (corners == null || corners.Length < 2) return;

            Handles.color = new Color(pathColor.r, pathColor.g, pathColor.b, 0.9f);
            Gizmos.color = pathColor;

            for (int i = 0; i < corners.Length - 1; i++)
            {
                Vector3 start = corners[i] + Vector3.up * 0.08f;
                Vector3 end = corners[i + 1] + Vector3.up * 0.08f;

                Handles.DrawLine(start, end);
                Gizmos.DrawWireSphere(end, 0.1f);
            }
        }

        /// <summary>
        /// Description: Renders a high-contrast floating badge above the phantom.
        /// </summary>
        private void DrawStateBadge(Color badgeColor)
        {
            Vector3 badgePos = transform.position + Vector3.up * 2.4f;

            string statusText = $"[{_phantom.Name.ToUpper()}] : {_phantom.CurrentState.ToString().ToUpper()}";

            if (_phantom.CurrentState == PhantomState.Chasing && _phantom.CurrentTarget != null)
            {
                float dist = Vector3.Distance(transform.position, _phantom.CurrentTarget.WorldPosition);
                statusText += $"\n>>> CHASING: {_phantom.CurrentTarget.Name} ({dist:F1}m)";
            }
            else if (_phantom.CurrentState == PhantomState.Investigating)
            {
                float dist = Vector3.Distance(transform.position, _phantom.LastKnownPosition);
                statusText += $"\n>>> INVESTIGATING SPOT ({dist:F1}m)";
            }
            else if (_phantom.CurrentState == PhantomState.BeingVacuumed)
            {
                int streamCount = _phantom.ActiveSuctionStreamCount;
                string streamInfo = streamCount > 1 ? $" [{streamCount}x MULTI-SUCTION]" : "";
                string dashInfo = _phantom.IsDashing ? " \n>>> [DASH BURST!]" : (_phantom.IsTelegraphingDash ? " \n>>> [TELEGRAPHING DASH!]" : "");
                statusText += $"\n>>> BEING VACUUMED ({_phantom.CurrentEctoplasm:F0}/{_phantom.MaxEctoplasm:F0} HP){streamInfo}{dashInfo}";
            }

            GUIStyle badgeStyle = new GUIStyle();
            badgeStyle.normal.textColor = badgeColor;
            badgeStyle.fontStyle = FontStyle.Bold;
            badgeStyle.alignment = TextAnchor.MiddleCenter;
            badgeStyle.fontSize = 11;

            Handles.Label(badgePos, statusText, badgeStyle);
        }

        /// <summary>
        /// Description: Renders the real-time escape raycasts and chosen dash trajectory in Scene View.
        /// Red line + sphere = blocked obstacle; Yellow line = clear open path; Green line + cone = chosen best direction.
        /// </summary>
        private void DrawEscapeRaycasts()
        {
            var rays = _phantom.LastEscapeRays;
            if (rays == null || rays.Count == 0) return;

            // Display while caught in vacuum or for 3 seconds after an escape evaluation
            if (_phantom.CurrentState != PhantomState.BeingVacuumed && Time.time - _phantom.LastEscapeEvaluationTime > 3.0f)
            {
                return;
            }

            Vector3 origin = transform.position + Vector3.up * 0.8f;

            for (int i = 0; i < rays.Count; i++)
            {
                var r = rays[i];
                if (r.HitObstacle)
                {
                    Handles.color = new Color(1f, 0.2f, 0.2f, 0.7f);
                    Handles.DrawLine(origin, r.HitPoint);
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(r.HitPoint, 0.08f);
                }
                else
                {
                    Handles.color = new Color(1f, 0.9f, 0.2f, 0.45f);
                    Handles.DrawLine(origin, origin + r.Direction * r.Distance);
                }
            }

            // Bold Green Arrow for the chosen escape path
            if (_phantom.ChosenEscapeDirection.sqrMagnitude > 0.01f)
            {
                Handles.color = new Color(0.2f, 1f, 0.35f, 1f);
                Vector3 chosenEnd = origin + _phantom.ChosenEscapeDirection * 2.8f;
                Handles.DrawLine(origin, chosenEnd);
                Handles.ConeHandleCap(0, chosenEnd, Quaternion.LookRotation(_phantom.ChosenEscapeDirection), 0.35f, EventType.Repaint);

                GUIStyle escapeStyle = new GUIStyle();
                escapeStyle.normal.textColor = Color.green;
                escapeStyle.fontStyle = FontStyle.Bold;
                escapeStyle.fontSize = 11;
                Handles.Label(chosenEnd + Vector3.up * 0.2f, "[CHOSEN ESCAPE PATH]", escapeStyle);
            }
        }

        private void DrawDrainAura()
        {
            float radius = _phantom.ProximityDrainRadius;
            Gizmos.color = new Color(0.9f, 0.1f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, radius);

            Handles.color = new Color(0.9f, 0.1f, 0.3f, 0.05f);
            Handles.DrawSolidDisc(transform.position, Vector3.up, radius);
        }
#endif
    }
}
