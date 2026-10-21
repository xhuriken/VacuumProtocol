using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Description: Outil de débogage visuel (Gizmos) pour le système V2.
/// Context: Attaché au Player_V2.
/// Justification: Permet aux designers de visualiser les cibles invisibles (ressorts de bras, raycasts de suspension, etc.) directement dans l'éditeur.
/// </summary>
[RequireComponent(typeof(PlayerV2_Controller))]
public class PlayerV2_Gizmos : MonoBehaviour
{
    [Header("Gizmo Toggles")]
    [BoxGroup("Modules")] public bool ShowViewRangeGizmos = true;
    [BoxGroup("Modules")] public bool ShowVacuumArmGizmos = true;
    [BoxGroup("Modules")] public bool ShowShooterArmGizmos = true;
    [BoxGroup("Modules")] public bool ShowRightArmSuctionConeGizmos = true;
    [BoxGroup("Modules")] public bool ShowMouthSuctionConeGizmos = true;
    [BoxGroup("Modules")] public bool ShowHeadAnglesGizmos = true;
    [BoxGroup("Modules")] public bool ShowEyesGizmos = true;
    [BoxGroup("Modules")] public bool ShowCameraGizmos = true;
    [BoxGroup("Modules")] public bool ShowSuspensionGizmos = true;

    private PlayerV2_Controller _controller;

    private void LateUpdate()
    {
        if (!RuntimeGizmos.IsEnabled) return;

        if (_controller == null)
        {
            _controller = GetComponent<PlayerV2_Controller>();
            if (_controller == null) return;
        }

        DrawAllGizmos(isRuntime: true);
    }

    private void OnDrawGizmos()
    {
        if (_controller == null)
        {
            _controller = GetComponent<PlayerV2_Controller>();
            if (_controller == null) return;
        }

        DrawAllGizmos(isRuntime: false);
    }

    private void DrawAllGizmos(bool isRuntime)
    {
        if (ShowVacuumArmGizmos && _controller.ArmsController != null)
        {
            DrawArmsGizmos(isRuntime);
        }

        if (ShowRightArmSuctionConeGizmos)
        {
            DrawRightArmSuctionConeGizmos(isRuntime);
        }

        if (ShowMouthSuctionConeGizmos)
        {
            DrawMouthSuctionConeGizmos(isRuntime);
        }

        if (ShowHeadAnglesGizmos && _controller.HeadController != null)
        {
            DrawHeadAnglesGizmos(isRuntime);
        }

        if (ShowEyesGizmos)
        {
            DrawEyesGizmos(isRuntime);
        }

        if (ShowCameraGizmos && _controller.CameraTransform != null)
        {
            DrawCameraGizmos(isRuntime);
        }

        if (ShowSuspensionGizmos)
        {
            DrawSuspensionGizmos(isRuntime);
        }
    }

    private void DrawArmsGizmos(bool isRuntime)
    {
        var arms = _controller.ArmsController;

        // Left Arm
        if (_controller.LeftArmRoot != null && arms.LeftHand != null)
        {
            DrawWireSphereHelper(_controller.LeftArmRoot.position, 0.1f, Color.blue, isRuntime);
            DrawWireSphereHelper(arms.LeftHand.position, 0.1f, Color.blue, isRuntime);
            DrawLineHelper(_controller.LeftArmRoot.position, arms.LeftHand.position, Color.blue, isRuntime);

            if (arms.IsLeftArmExtended)
            {
                Transform head = _controller.CameraTransform != null ? _controller.CameraTransform : transform;
                Vector3 targetPos = head.position + head.forward * (Vector3.Distance(_controller.LeftArmRoot.position, arms.LeftHand.position) * arms.ReachLengthFactor + arms.ForwardOffset) + head.up * arms.VerticalOffset;
                DrawWireSphereHelper(targetPos, 0.15f, Color.cyan, isRuntime);
                DrawLineHelper(arms.LeftHand.position, targetPos, Color.cyan, isRuntime);
            }
        }

        // Right Arm
        if (_controller.RightArmRoot != null && arms.RightHand != null)
        {
            DrawWireSphereHelper(_controller.RightArmRoot.position, 0.1f, Color.red, isRuntime);
            DrawWireSphereHelper(arms.RightHand.position, 0.1f, Color.red, isRuntime);
            DrawLineHelper(_controller.RightArmRoot.position, arms.RightHand.position, Color.red, isRuntime);

            if (arms.IsRightArmExtended)
            {
                Transform head = _controller.CameraTransform != null ? _controller.CameraTransform : transform;
                Vector3 targetPos = head.position + head.forward * (Vector3.Distance(_controller.RightArmRoot.position, arms.RightHand.position) * arms.ReachLengthFactor + arms.ForwardOffset) + head.up * arms.VerticalOffset;
                DrawWireSphereHelper(targetPos, 0.15f, Color.magenta, isRuntime);
                DrawLineHelper(arms.RightHand.position, targetPos, Color.magenta, isRuntime);
            }
        }
    }

    private void DrawHeadAnglesGizmos(bool isRuntime)
    {
        var head = _controller.HeadController;
        if (head.NeckJoints == null || head.NeckJoints.Length == 0) return;

        foreach (var joint in head.NeckJoints)
        {
            if (joint != null)
            {
                DrawWireSphereHelper(joint.transform.position, 0.05f, Color.yellow, isRuntime);
                if (joint.transform.parent != null)
                {
                    DrawLineHelper(joint.transform.position, joint.transform.parent.position, Color.yellow, isRuntime);
                }
            }
        }

        if (head.UseAlignmentForce && head.NeckJoints.Length > 0)
        {
            Rigidbody headRb = head.NeckJoints[head.NeckJoints.Length - 1].GetComponent<Rigidbody>();
            if (headRb != null && _controller.TorsoRigidbody != null)
            {
                // 1. Envie de la Tête (70% Pitch) - JAUNE
                Quaternion targetRot = _controller.TorsoRigidbody.rotation * Quaternion.Euler(head.CurrentTargetPitch, 0f, 0f);
                Vector3 targetForward = targetRot * Vector3.forward;
                DrawRayHelper(headRb.position, targetForward * 1.5f, Color.yellow, isRuntime);

                // 2. Tête Actuelle Physique - ROUGE
                DrawRayHelper(headRb.position, headRb.transform.forward * 1.5f, Color.red, isRuntime);
            }
        }
    }

    private void DrawCameraGizmos(bool isRuntime)
    {
        // 3. Caméra (100% Pitch / Input) - CYAN
        DrawRayHelper(_controller.CameraTransform.position, _controller.CameraTransform.forward * 2f, Color.cyan, isRuntime);
    }

    private void DrawEyesGizmos(bool isRuntime)
    {
        // 4 & 5. Yeux (75%) et Pupilles (100%) - MAGENTA & VERT
        Eye[] eyes = GetComponentsInChildren<Eye>();
        foreach (var eye in eyes)
        {
            if (eye != null)
            {
                DrawRayHelper(eye.transform.position, eye.transform.forward * 2.5f, Color.magenta, isRuntime);

                Transform pupil = eye.GetPupilBone();
                if (pupil != null)
                {
                    DrawRayHelper(pupil.position, pupil.forward * 3f, Color.green, isRuntime);
                }
            }
        }
    }

    private void DrawSuspensionGizmos(bool isRuntime)
    {
        var suspension = GetComponent<PlayerV2_Suspension>();
        if (suspension != null && suspension.WheelJoints != null)
        {
            foreach (var joint in suspension.WheelJoints)
            {
                if (joint != null)
                {
                    DrawWireSphereHelper(joint.transform.position, 0.2f, Color.green, isRuntime);

                    if (joint.transform.parent != null)
                    {
                        DrawLineHelper(joint.transform.position, joint.transform.parent.position, Color.gray, isRuntime);
                    }
                }
            }
        }
    }

    private void DrawRightArmSuctionConeGizmos(bool isRuntime)
    {
        VacuumSuctionZone armZone = GetComponentInChildren<VacuumSuctionZone>(true);
        if (armZone == null || armZone.NozzleTransform == null) return;

        DrawSuctionCone(
            armZone.NozzleTransform,
            armZone.GetSuctionDirection(),
            armZone.SuctionRange,
            armZone.ConeAngle,
            armZone.AbsorbDistance,
            armZone.ShrinkStartDistance,
            Color.yellow,
            Color.green,
            armZone.IsActive,
            isRuntime);
    }

    private void DrawMouthSuctionConeGizmos(bool isRuntime)
    {
        MouthSuctionZone mouthZone = GetComponentInChildren<MouthSuctionZone>(true);
        if (mouthZone == null || mouthZone.NozzleTransform == null) return;

        DrawSuctionCone(
            mouthZone.NozzleTransform,
            mouthZone.GetSuctionDirection(),
            mouthZone.SuctionRange,
            mouthZone.ConeAngle,
            mouthZone.AbsorbDistance,
            0f,
            new Color(0.2f, 0.8f, 1f, 0.8f),
            Color.green,
            mouthZone.IsActive,
            isRuntime);
    }

    /// <summary>
    /// Description: Draws a complete 3D wireframe suction cone with opening angle, base circle, boundary lines, and absorption spheres.
    /// </summary>
    private static void DrawSuctionCone(
        Transform nozzle,
        Vector3 direction,
        float range,
        float coneAngle,
        float absorbDistance,
        float shrinkDistance,
        Color idleColor,
        Color activeColor,
        bool isActive,
        bool isRuntime)
    {
        if (nozzle == null || range <= 0f) return;

        Vector3 pos = nozzle.position;
        Vector3 forward = direction.normalized;

        // Base circle calculation based on opening angle
        float radAngle = coneAngle * Mathf.Deg2Rad;
        float baseRadius = range * Mathf.Tan(radAngle);
        Vector3 baseCenter = pos + forward * range;

        // Orthogonal axes for base circle
        Vector3 right = Vector3.Cross(forward, nozzle.up).normalized;
        if (right.sqrMagnitude < 0.001f)
        {
            right = Vector3.Cross(forward, nozzle.forward).normalized;
        }
        Vector3 up = Vector3.Cross(right, forward).normalized;

        Color mainColor = isActive ? activeColor : idleColor;

        // Draw base circle
        int segments = 24;
        Vector3 prevPoint = baseCenter + right * baseRadius;
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i * 360f / segments) * Mathf.Deg2Rad;
            Vector3 nextPoint = baseCenter + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * baseRadius;
            DrawLineHelper(prevPoint, nextPoint, mainColor, isRuntime);
            prevPoint = nextPoint;
        }

        // Draw 4 boundary lines
        DrawLineHelper(pos, baseCenter + right * baseRadius, mainColor, isRuntime);
        DrawLineHelper(pos, baseCenter - right * baseRadius, mainColor, isRuntime);
        DrawLineHelper(pos, baseCenter + up * baseRadius, mainColor, isRuntime);
        DrawLineHelper(pos, baseCenter - up * baseRadius, mainColor, isRuntime);

        // Draw central aiming ray
        DrawRayHelper(pos, forward * range, mainColor, isRuntime);

        // Draw absorption sphere
        if (absorbDistance > 0f)
        {
            DrawWireSphereHelper(pos, absorbDistance, Color.red, isRuntime);
        }

        // Draw shrink boundary sphere if configured
        if (shrinkDistance > 0f)
        {
            DrawWireSphereHelper(pos, shrinkDistance, new Color(1f, 0.5f, 0f, 0.7f), isRuntime);
        }
    }

    #region Line Drawing Helpers

    private static void DrawLineHelper(Vector3 start, Vector3 end, Color color, bool isRuntime)
    {
        if (isRuntime)
        {
            RuntimeGizmos.DrawLine(start, end, color);
        }
        else
        {
            Gizmos.color = color;
            Gizmos.DrawLine(start, end);
        }
    }

    private static void DrawWireSphereHelper(Vector3 center, float radius, Color color, bool isRuntime)
    {
        if (isRuntime)
        {
            RuntimeGizmos.DrawWireSphere(center, radius, color);
        }
        else
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(center, radius);
        }
    }

    private static void DrawRayHelper(Vector3 origin, Vector3 dir, Color color, bool isRuntime)
    {
        if (isRuntime)
        {
            RuntimeGizmos.DrawRay(origin, dir, color);
        }
        else
        {
            Gizmos.color = color;
            Gizmos.DrawRay(origin, dir);
        }
    }

    #endregion
}

