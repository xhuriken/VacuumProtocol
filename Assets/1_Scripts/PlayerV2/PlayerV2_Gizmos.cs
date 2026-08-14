using Sirenix.OdinInspector;
using UnityEngine;

namespace VacuumProtocol.PlayerV2
{
    /// <summary>
    /// Description: Outil de débogage visuel (Gizmos) pour le système V2.
    /// Context: Attaché au Player_V2.
    /// Justification: Permet aux designers de visualiser les cibles invisibles (ressorts de bras, raycasts de suspension, etc.) directement dans l'éditeur.
    /// </summary>
    [RequireComponent(typeof(PlayerV2_Controller))]
    public class PlayerV2_Gizmos : MonoBehaviour
    {
        [Header("Gizmo Toggles")]
        [BoxGroup("Modules")]
        [Tooltip("Affiche les sphères de la racine des bras, de la main, et la ligne de visée.")]
        public bool ShowArmsGizmos = true;
        
        [BoxGroup("Modules")]
        [Tooltip("Affiche les forces de la tête et du cou.")]
        public bool ShowHeadGizmos = true;

        [BoxGroup("Modules")]
        [Tooltip("Affiche les raycasts de suspension.")]
        public bool ShowSuspensionGizmos = true;

        private PlayerV2_Controller _controller;

        private void OnDrawGizmos()
        {
            if (_controller == null)
            {
                _controller = GetComponent<PlayerV2_Controller>();
                if (_controller == null) return;
            }

            if (ShowArmsGizmos && _controller.ArmsController != null)
            {
                DrawArmsGizmos();
            }

            if (ShowHeadGizmos && _controller.HeadController != null)
            {
                DrawHeadGizmos();
            }

            if (ShowSuspensionGizmos)
            {
                DrawSuspensionGizmos();
            }
        }

        private void DrawArmsGizmos()
        {
            var arms = _controller.ArmsController;
            
            // Left Arm
            if (_controller.LeftArmRoot != null && arms.LeftHand != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(_controller.LeftArmRoot.position, 0.1f);
                Gizmos.DrawWireSphere(arms.LeftHand.position, 0.1f);
                Gizmos.DrawLine(_controller.LeftArmRoot.position, arms.LeftHand.position);
                
                if (arms.IsLeftArmExtended)
                {
                    Transform head = _controller.CameraTransform != null ? _controller.CameraTransform : transform;
                    Vector3 targetPos = head.position + head.forward * (Vector3.Distance(_controller.LeftArmRoot.position, arms.LeftHand.position) * arms.ReachLengthFactor + arms.ForwardOffset) + head.up * arms.VerticalOffset;
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(targetPos, 0.15f);
                    Gizmos.DrawLine(arms.LeftHand.position, targetPos);
                }
            }

            // Right Arm
            if (_controller.RightArmRoot != null && arms.RightHand != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_controller.RightArmRoot.position, 0.1f);
                Gizmos.DrawWireSphere(arms.RightHand.position, 0.1f);
                Gizmos.DrawLine(_controller.RightArmRoot.position, arms.RightHand.position);

                if (arms.IsRightArmExtended)
                {
                    Transform head = _controller.CameraTransform != null ? _controller.CameraTransform : transform;
                    Vector3 targetPos = head.position + head.forward * (Vector3.Distance(_controller.RightArmRoot.position, arms.RightHand.position) * arms.ReachLengthFactor + arms.ForwardOffset) + head.up * arms.VerticalOffset;
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireSphere(targetPos, 0.15f);
                    Gizmos.DrawLine(arms.RightHand.position, targetPos);
                }
            }
        }

        private void DrawHeadGizmos()
        {
            var head = _controller.HeadController;
            if (head.NeckJoints == null || head.NeckJoints.Length == 0) return;

            Gizmos.color = Color.yellow;
            foreach (var joint in head.NeckJoints)
            {
                if (joint != null)
                {
                    Gizmos.DrawWireSphere(joint.transform.position, 0.05f);
                    if (joint.transform.parent != null)
                    {
                        Gizmos.DrawLine(joint.transform.position, joint.transform.parent.position);
                    }
                }
            }

            if (head.UseAlignmentForce && head.NeckJoints.Length > 0)
            {
                Rigidbody headRb = head.NeckJoints[head.NeckJoints.Length - 1].GetComponent<Rigidbody>();
                if (headRb != null && _controller.TorsoRigidbody != null)
                {
                    Quaternion targetRot = _controller.TorsoRigidbody.rotation * Quaternion.Euler(head.CurrentTargetPitch, 0f, 0f);
                    Vector3 targetForward = targetRot * Vector3.forward;
                    
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawRay(headRb.position, targetForward * 0.5f);
                    
                    Gizmos.color = Color.red;
                    Gizmos.DrawRay(headRb.position, headRb.transform.forward * 0.5f);
                }
            }
        }

        private void DrawSuspensionGizmos()
        {
            var suspension = GetComponent<PlayerV2_Suspension>();
            if (suspension != null && suspension.WheelJoints != null)
            {
                foreach (var joint in suspension.WheelJoints)
                {
                    if (joint != null)
                    {
                        // Affiche la position de la roue
                        Gizmos.color = Color.green;
                        Gizmos.DrawWireSphere(joint.transform.position, 0.2f);
                        
                        // Si le joint a un parent (le chassis), on dessine une ligne
                        if (joint.transform.parent != null)
                        {
                            Gizmos.color = Color.gray;
                            Gizmos.DrawLine(joint.transform.position, joint.transform.parent.position);
                        }
                    }
                }
            }
        }
    }
}
