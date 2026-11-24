using UnityEngine;
using Mirror;

namespace VacuumProtocol.Core
{
    /// <summary>
    /// Description: Universal contract for any inanimate world element that can be pulled, drained, or absorbed by the player's arm vacuum nozzle.
    /// Context: Implemented by Collectibles (physical items) and DirtStains (surface dirt).
    /// Justification: Eliminates rigid concrete type checks (GetComponent<Collectible>, GetComponent<DirtStain>) in VacuumSuctionZone, adhering to the Open-Closed Principle (OCP).
    /// Note: Phantoms are spectral enemies and are vacuumed exclusively via the mouth dual-click system, not via this arm vacuum interface.
    /// </summary>
    public interface IVacuumable
    {
        /// <summary>
        /// Description: Rigidbody of the vacuumable object, if it uses physics simulation.
        /// </summary>
        Rigidbody Rigidbody { get; }

        /// <summary>
        /// Description: Pull resistance factor for suction physics calculations.
        /// </summary>
        float PullResistance { get; }

        /// <summary>
        /// Description: Called each physics tick while the object is inside an active suction cone.
        /// </summary>
        /// <param name="nozzlePosition">World position of the suction nozzle tip.</param>
        /// <param name="suctionDirection">Normalized direction vector the nozzle is pulling towards.</param>
        /// <param name="pullForce">Magnitude of the suction pull force.</param>
        /// <param name="visibilityFactor">Occlusion/visibility factor (0 = fully occluded, 1 = direct line of sight).</param>
        void OnSuctionPulled(Vector3 nozzlePosition, Vector3 suctionDirection, float pullForce, float visibilityFactor);

        /// <summary>
        /// Description: Drains a continuous resource quantity (e.g. dirt stain volume) from the entity.
        /// </summary>
        void OnSuctionDrained(float drainAmount, NetworkConnectionToClient playerConnection);

        /// <summary>
        /// Description: Checks if the object is close enough and eligible to be fully absorbed.
        /// </summary>
        bool CanBeAbsorbed(float distanceToNozzle, float absorbThreshold);

        /// <summary>
        /// Description: Authoritative absorption hook when the object reaches the nozzle.
        /// </summary>
        void OnAbsorbed(PlayerVacuumController playerVacuum);
    }
}
