using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Automates the creation of a procedural softbody-like chain of Rigidbodies and ConfigurableJoints.
/// Useful for tubes, arms, or flexible robotic appendages.
/// </summary>
public class ProceduralTubePhysics : MonoBehaviour
{
    [BoxGroup("General Settings")]
    [Range(0.01f, 10f)]
    public float segmentMass = 0.5f;


    [BoxGroup("General Settings")]
    public float linearDamping = 1f;


    [BoxGroup("General Settings")]
    public float angularDamping = 5f;

    [BoxGroup("Joint Settings (Softbody Feel)")]
    public float stiffness = 100f;
    [BoxGroup("Joint Settings (Softbody Feel)")]
    public float damping = 10f;
    [BoxGroup("Joint Settings (Softbody Feel)")]
    [Tooltip("Multiplier for the stiffness of the last segment (the hand/tip) to make it more stable.")]
    public float tipStiffnessMultiplier = 2f;
    [BoxGroup("Joint Settings (Softbody Feel)")]
    [Range(0, 180)]
    public float angularLimit = 45f;

    [BoxGroup("Collider Settings")]
    public float colliderRadius = 0.05f;

    /// <summary>
    /// Clears existing components and sets up the procedural physics chain from this transform downwards.
    /// </summary>
    [HorizontalGroup("Actions")]
    [Button(ButtonSizes.Large), GUIColor(0.4f, 0.8f, 0.4f)]
    public void Setup()
    {
        Clear();
        SetupRecursive(transform, null);
    }

    /// <summary>
    /// Removes all Rigidbody, ConfigurableJoint, and CapsuleCollider components from child objects.
    /// </summary>
    [HorizontalGroup("Actions")]
    [Button(ButtonSizes.Large), GUIColor(0.8f, 0.4f, 0.4f)]
    public void Clear()
    {
        UnityEngine.Rigidbody[] rbs = GetComponentsInChildren<UnityEngine.Rigidbody>();
        foreach (var rb in rbs)
        {
            if (rb.gameObject == gameObject) continue;


            UnityEngine.ConfigurableJoint joint = rb.GetComponent<UnityEngine.ConfigurableJoint>();
            if (joint) DestroyImmediate(joint);


            UnityEngine.CapsuleCollider col = rb.GetComponent<UnityEngine.CapsuleCollider>();
            if (col) DestroyImmediate(col);


            DestroyImmediate(rb);
        }
    }

    /// <summary>
    /// Recursively traverses the hierarchy to add and configure physics components.
    /// </summary>
    /// <param name="current">The current transform being processed.</param>
    /// <param name="parentRb">The Rigidbody of the parent object to connect the joint to.</param>
    private void SetupRecursive(Transform current, UnityEngine.Rigidbody parentRb)
    {
        if (current != transform)
        {
            // Add and configure the Rigidbody
            UnityEngine.Rigidbody rb = current.gameObject.AddComponent<UnityEngine.Rigidbody>();
            rb.mass = segmentMass;
            rb.linearDamping = linearDamping;
            rb.angularDamping = angularDamping;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // Improve physics accuracy for this specific segment

            rb.solverIterations = 20;
            rb.solverVelocityIterations = 10;

            // Add and configure the Collider
            UnityEngine.CapsuleCollider col = current.gameObject.AddComponent<UnityEngine.CapsuleCollider>();


            float maxScale = Mathf.Max(current.lossyScale.x, Mathf.Max(current.lossyScale.y, current.lossyScale.z));
            col.radius = colliderRadius / (maxScale > 0 ? maxScale : 1f);

            bool isLast = current.childCount == 0;

            if (!isLast)
            {
                Transform child = current.GetChild(0);
                Vector3 localChildPos = current.InverseTransformPoint(child.position);
                float localDist = localChildPos.magnitude;


                col.height = localDist;
                col.center = localChildPos * 0.5f;

                float absX = Mathf.Abs(localChildPos.x);
                float absY = Mathf.Abs(localChildPos.y);
                float absZ = Mathf.Abs(localChildPos.z);

                if (absX > absY && absX > absZ) col.direction = 0;
                else if (absY > absX && absY > absZ) col.direction = 1;
                else col.direction = 2;
            }
            else
            {
                col.height = col.radius * 2;
                col.center = Vector3.zero;
            }

            // Add and configure the Configurable Joint
            UnityEngine.ConfigurableJoint joint = current.gameObject.AddComponent<UnityEngine.ConfigurableJoint>();
            joint.connectedBody = parentRb;


            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;

            SoftJointLimit limit = new SoftJointLimit { limit = angularLimit };
            SoftJointLimit lowLimit = new SoftJointLimit { limit = -angularLimit };

            joint.lowAngularXLimit = lowLimit;
            joint.highAngularXLimit = limit;
            joint.angularYLimit = limit;
            joint.angularZLimit = limit;

            joint.rotationDriveMode = RotationDriveMode.Slerp;

            // Apply higher stiffness to the tip segment to keep the arm stable

            float finalStiffness = isLast ? stiffness * tipStiffnessMultiplier : stiffness;

            JointDrive slerpDrive = new JointDrive
            {
                positionSpring = finalStiffness,
                positionDamper = damping,
                maximumForce = float.MaxValue
            };
            joint.slerpDrive = slerpDrive;

            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.01f;
            joint.projectionAngle = 1f;

            parentRb = rb;
        }
        else
        {
            // Root object is usually kinematic to anchor the procedural arm
            UnityEngine.Rigidbody rootRb = current.GetComponent<UnityEngine.Rigidbody>();
            if (!rootRb) rootRb = current.gameObject.AddComponent<UnityEngine.Rigidbody>();
            rootRb.isKinematic = true;
            parentRb = rootRb;
        }

        // Continue processing children
        for (int i = 0; i < current.childCount; i++)
        {
            SetupRecursive(current.GetChild(i), parentRb);
        }
    }
}