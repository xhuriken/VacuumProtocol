using UnityEngine;

namespace VacuumProtocol.PlayerV2
{
    /// <summary>
    /// Description: Gère les paramètres de suspension des roues.
    /// Context: Attaché au Player_V2.
    /// Justification: Permet de régler facilement les ressorts et limites des ConfigurableJoints des roues depuis l'inspecteur.
    /// </summary>
    public class PlayerV2_Suspension : MonoBehaviour
    {
        [Header("Wheels Configuration")]
        [Tooltip("Les ConfigurableJoints des 4 roues.")]
        public ConfigurableJoint[] WheelJoints;

        [Header("Suspension Settings")]
        public float SpringForce = 500f;
        public float Damper = 50f;
        public float SuspensionTravel = 0.5f;

        [Tooltip("La distance à laquelle le ressort repousse la roue (doit être négatif pour repousser vers le bas).")]
        public float TargetExtension = -0.4f;

        private void Start()
        {
            ApplySuspensionSettings();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                ApplySuspensionSettings();
            }
        }

        private void ApplySuspensionSettings()
        {
            if (WheelJoints == null) return;

            foreach (var joint in WheelJoints)
            {
                if (joint == null) continue;

                // Configurer la limite de mouvement Y (coulissement)
                SoftJointLimit limit = new SoftJointLimit();
                limit.limit = SuspensionTravel;
                joint.linearLimit = limit;

                // Configurer le ressort (Drive) sur l'axe Y
                JointDrive drive = new JointDrive();
                drive.positionSpring = SpringForce;
                drive.positionDamper = Damper;
                drive.maximumForce = Mathf.Infinity;

                // Unity utilise le XDrive, YDrive, ZDrive pour les ConfigurableJoints
                // Dans le repère local du joint, le coulissement est généralement sur l'axe Y ou Z selon la configuration.
                // Si l'axe est Y (0,1,0), c'est le yDrive.

                joint.yDrive = drive;
                
                // Forcer l'extension du ressort
                joint.targetPosition = new Vector3(0, TargetExtension, 0);

                // Assurons-nous que le reste est bien bloqué
                joint.xMotion = ConfigurableJointMotion.Locked;
                joint.zMotion = ConfigurableJointMotion.Locked;
                joint.yMotion = ConfigurableJointMotion.Limited;

                joint.angularXMotion = ConfigurableJointMotion.Locked;
                joint.angularYMotion = ConfigurableJointMotion.Free;
                joint.angularZMotion = ConfigurableJointMotion.Locked;

                // IMPORTANT : Pour éviter les explosions physiques (le robot expulsé en l'air) et les vrilles,
                // il FAUT que les roues glissent parfaitement, sinon la friction fait levier et détruit le joint.
                Collider wheelCollider = joint.GetComponent<Collider>();
                if (wheelCollider != null)
                {
                    PhysicsMaterial zeroFriction = new PhysicsMaterial("ZeroFrictionWheel");
                    zeroFriction.dynamicFriction = 0f;
                    zeroFriction.staticFriction = 0f;
                    zeroFriction.frictionCombine = PhysicsMaterialCombine.Minimum;
                    zeroFriction.bounciness = 0f;
                    zeroFriction.bounceCombine = PhysicsMaterialCombine.Minimum;
                    wheelCollider.material = zeroFriction;
                }
            }
        }
    }
}
