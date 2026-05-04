using Mirror;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// This script is on an sphere (an eye)
/// If something are in range of the visibility on the player, the eye will look at it. (Danger entity level parametter) // local
/// Just sync the roration of the eye.
/// </summary>
public class Eye : NetworkBehaviour
{
    [SerializeField] private PlayerViewRange _playerViewRange;

    [SerializeField] private float _rotationSpeed = 5f;

    private Quaternion _targetRotation = Quaternion.identity;

    private void Update()
    {
        if(!isLocalPlayer) return;

        if (_playerViewRange.HighestPriorityEntity != null)
        {
            Vector3 directionToTarget = _playerViewRange.HighestPriorityEntity.gameObject.transform.position - transform.position;
            _targetRotation = Quaternion.LookRotation(directionToTarget);
        }
        else
        {
            // If no entity is detected, reset the target rotation to the PLAYER forward direction (or any default orientation you prefer).
            _targetRotation = transform.parent.rotation;
        }
        transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * _rotationSpeed);
    }
}
