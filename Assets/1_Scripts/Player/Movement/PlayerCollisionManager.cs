using System.Collections.Generic;
using UnityEngine;

namespace VacuumProtocol.Player
{
    /// <summary>
    /// Description: Defines the different types of collider groups on the player.
    /// </summary>
    public enum ColliderGroupType
    {
        None,
        LeftArm,
        RightArm,
        HeadNeck,
        Wheels,
        TorsoA,
        TorsoB,
        OtherBody
    }

    /// <summary>
    /// Description: Centralized manager for handling all player-internal collision ignoring rules (SSOT).
    /// Context: Attached to the player prefab root.
    /// Justification: Centralizes physics collision exemptions in a single place instead of dispersing them across arms/head scripts.
    /// Allows the designer to edit, add, or delete collision ignore rules directly in the Unity Inspector.
    /// </summary>
    public class PlayerCollisionManager : MonoBehaviour
    {
        [System.Serializable]
        public struct CollisionIgnoreRule
        {
            [Tooltip("The first group of colliders.")]
            public ColliderGroupType GroupA;

            [Tooltip("The second group of colliders to ignore.")]
            public ColliderGroupType GroupB;
        }

        [Header("Torso Colliders")]
        [Tooltip("Torso Collider A: Ignores both wheels and arm colliders.")]
        [SerializeField] private Collider _torsoColliderA;

        [Tooltip("Torso Collider B: Ignores wheels, but collides (interacts physically) with the arms.")]
        [SerializeField] private Collider _torsoColliderB;

        [Header("Hierarchy Roots")]
        [Tooltip("The root transform of the wheels chassis hierarchy to automatically discover wheel colliders.")]
        [SerializeField] private Transform _wheelsRoot;

        [Tooltip("The root transform of the left arm bone hierarchy.")]
        [SerializeField] private Transform _leftArmRoot;

        [Tooltip("The root transform of the right arm bone hierarchy.")]
        [SerializeField] private Transform _rightArmRoot;

        [Tooltip("The root transform of the neck/head bone hierarchy.")]
        [SerializeField] private Transform _neckRootTransform;

        [Header("Explicit Wheel Override")]
        [Tooltip("Explicitly assigned wheel colliders (fallback if wheels root is not used).")]
        [SerializeField] private List<Collider> _wheelsColliders = new List<Collider>();

        [Header("Custom Collision Ignore Rules")]
        [Tooltip("List of collision ignore rules between groups. Pre-populated with defaults, editable in the Inspector.")]
        [SerializeField] private List<CollisionIgnoreRule> _ignoreRules = new List<CollisionIgnoreRule>();

        [Header("Debug")]
        [Tooltip("Enable verbose logging for collision setup diagnostic checks.")]
        [SerializeField] private bool _enableDebugLogs = false;

        private readonly List<Collider> _leftArmColliders = new List<Collider>();
        private readonly List<Collider> _rightArmColliders = new List<Collider>();
        private readonly List<Collider> _neckColliders = new List<Collider>();
        private readonly List<Collider> _otherColliders = new List<Collider>();

        /// <summary>
        /// Description: Start callback. Gathers all colliders on the player, classifies them, and configures IgnoreCollision rules.
        /// </summary>
        private void Start()
        {
            ClassifyColliders();
            ApplyCollisionRules();
        }

        /// <summary>
        /// Description: Reset callback. Pre-populates the default collision ignore rules in the Editor.
        /// </summary>
        private void Reset()
        {
            _ignoreRules.Clear();

            // Rule 1: Torso A & B ignore Wheels
            _ignoreRules.Add(new CollisionIgnoreRule { GroupA = ColliderGroupType.TorsoA, GroupB = ColliderGroupType.Wheels });
            _ignoreRules.Add(new CollisionIgnoreRule { GroupA = ColliderGroupType.TorsoB, GroupB = ColliderGroupType.Wheels });

            // Rule 2: Left Arm ignores Right Arm, Wheels, Head/Neck, Other Body, and Torso A
            _ignoreRules.Add(new CollisionIgnoreRule { GroupA = ColliderGroupType.LeftArm, GroupB = ColliderGroupType.RightArm });
            _ignoreRules.Add(new CollisionIgnoreRule { GroupA = ColliderGroupType.LeftArm, GroupB = ColliderGroupType.Wheels });
            _ignoreRules.Add(new CollisionIgnoreRule { GroupA = ColliderGroupType.LeftArm, GroupB = ColliderGroupType.HeadNeck });
            _ignoreRules.Add(new CollisionIgnoreRule { GroupA = ColliderGroupType.LeftArm, GroupB = ColliderGroupType.OtherBody });
            _ignoreRules.Add(new CollisionIgnoreRule { GroupA = ColliderGroupType.LeftArm, GroupB = ColliderGroupType.TorsoA });

            // Rule 3: Right Arm ignores Wheels, Head/Neck, Other Body, and Torso A
            _ignoreRules.Add(new CollisionIgnoreRule { GroupA = ColliderGroupType.RightArm, GroupB = ColliderGroupType.Wheels });
            _ignoreRules.Add(new CollisionIgnoreRule { GroupA = ColliderGroupType.RightArm, GroupB = ColliderGroupType.HeadNeck });
            _ignoreRules.Add(new CollisionIgnoreRule { GroupA = ColliderGroupType.RightArm, GroupB = ColliderGroupType.OtherBody });
            _ignoreRules.Add(new CollisionIgnoreRule { GroupA = ColliderGroupType.RightArm, GroupB = ColliderGroupType.TorsoA });

            // Rule 4: Head/Neck ignores Wheels, Other Body, Torso A, and Torso B
            _ignoreRules.Add(new CollisionIgnoreRule { GroupA = ColliderGroupType.HeadNeck, GroupB = ColliderGroupType.Wheels });
            _ignoreRules.Add(new CollisionIgnoreRule { GroupA = ColliderGroupType.HeadNeck, GroupB = ColliderGroupType.OtherBody });
            _ignoreRules.Add(new CollisionIgnoreRule { GroupA = ColliderGroupType.HeadNeck, GroupB = ColliderGroupType.TorsoA });
            _ignoreRules.Add(new CollisionIgnoreRule { GroupA = ColliderGroupType.HeadNeck, GroupB = ColliderGroupType.TorsoB });
        }

        /// <summary>
        /// Description: Gathers and groups all colliders on the player based on hierarchy roots and explicit settings.
        /// </summary>
        private void ClassifyColliders()
        {
            // Gather all colliders in this entire player hierarchy
            Collider[] allColliders = transform.root.GetComponentsInChildren<Collider>(true);

            // 1. Resolve left arm colliders
            if (_leftArmRoot != null)
            {
                _leftArmColliders.AddRange(_leftArmRoot.GetComponentsInChildren<Collider>(true));
            }

            // 2. Resolve right arm colliders
            if (_rightArmRoot != null)
            {
                _rightArmColliders.AddRange(_rightArmRoot.GetComponentsInChildren<Collider>(true));
            }

            // 3. Resolve neck/head colliders
            if (_neckRootTransform != null)
            {
                _neckColliders.AddRange(_neckRootTransform.GetComponentsInChildren<Collider>(true));
            }

            // 4. Resolve wheels colliders from root
            if (_wheelsRoot != null)
            {
                foreach (Collider col in _wheelsRoot.GetComponentsInChildren<Collider>(true))
                {
                    if (col != null && !_wheelsColliders.Contains(col))
                    {
                        _wheelsColliders.Add(col);
                    }
                }
            }

            // Fallback: search by name keyword for wheels
            foreach (Collider col in allColliders)
            {
                if (col == null) continue;
                string lowerName = col.name.ToLower();
                if (lowerName.Contains("wheel") || lowerName.Contains("caster"))
                {
                    if (!_wheelsColliders.Contains(col))
                    {
                        _wheelsColliders.Add(col);
                    }
                }
            }

            // 5. Classify all remaining colliders as other body/torso parts
            foreach (Collider col in allColliders)
            {
                if (col == null) continue;
                if (col == _torsoColliderA || col == _torsoColliderB) continue;
                if (_leftArmColliders.Contains(col)) continue;
                if (_rightArmColliders.Contains(col)) continue;
                if (_neckColliders.Contains(col)) continue;
                if (_wheelsColliders.Contains(col)) continue;

                _otherColliders.Add(col);
            }

            if (_enableDebugLogs)
            {
                Debug.Log($"[PlayerCollisionManager] Classification Complete: " +
                          $"LeftArm={_leftArmColliders.Count}, " +
                          $"RightArm={_rightArmColliders.Count}, " +
                          $"Neck={_neckColliders.Count}, " +
                          $"Wheels={_wheelsColliders.Count}, " +
                          $"OtherBody={_otherColliders.Count}");
            }
        }

        /// <summary>
        /// Description: Applies all Physics.IgnoreCollision rules between collider groups.
        /// </summary>
        private void ApplyCollisionRules()
        {
            // Map the groups to a dictionary for fast lookup
            var groupMap = new Dictionary<ColliderGroupType, List<Collider>>
            {
                { ColliderGroupType.LeftArm, _leftArmColliders },
                { ColliderGroupType.RightArm, _rightArmColliders },
                { ColliderGroupType.HeadNeck, _neckColliders },
                { ColliderGroupType.Wheels, _wheelsColliders },
                { ColliderGroupType.TorsoA, _torsoColliderA != null ? new List<Collider> { _torsoColliderA } : new List<Collider>() },
                { ColliderGroupType.TorsoB, _torsoColliderB != null ? new List<Collider> { _torsoColliderB } : new List<Collider>() },
                { ColliderGroupType.OtherBody, _otherColliders }
            };

            int ignoredPairs = 0;

            // Apply standard internal rules (like self-collision ignore within arms to prevent joint lockups)
            IgnoreSelfCollisions(_leftArmColliders, ref ignoredPairs);
            IgnoreSelfCollisions(_rightArmColliders, ref ignoredPairs);

            // Apply custom ignore rules configured in the inspector
            foreach (CollisionIgnoreRule rule in _ignoreRules)
            {
                if (rule.GroupA == ColliderGroupType.None || rule.GroupB == ColliderGroupType.None) continue;

                if (groupMap.TryGetValue(rule.GroupA, out List<Collider> listA) &&
                    groupMap.TryGetValue(rule.GroupB, out List<Collider> listB))
                {
                    foreach (Collider colA in listA)
                    {
                        if (colA == null) continue;
                        foreach (Collider colB in listB)
                        {
                            if (colB == null || colB == colA) continue;
                            Physics.IgnoreCollision(colA, colB, true);
                            ignoredPairs++;
                        }
                    }
                }
            }

            if (_enableDebugLogs)
            {
                Debug.Log($"[PlayerCollisionManager] Applied {ignoredPairs} Physics.IgnoreCollision rules based on Inspector configuration.");
            }
        }

        /// <summary>
        /// Description: Ignores collisions within a single list of colliders to prevent self-collision constraints locking.
        /// </summary>
        private void IgnoreSelfCollisions(List<Collider> colliders, ref int counter)
        {
            for (int i = 0; i < colliders.Count; i++)
            {
                if (colliders[i] == null) continue;
                for (int j = i + 1; j < colliders.Count; j++)
                {
                    if (colliders[j] == null) continue;
                    Physics.IgnoreCollision(colliders[i], colliders[j], true);
                    counter++;
                }
            }
        }
    }
}
