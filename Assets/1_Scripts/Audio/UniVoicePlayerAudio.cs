using Adrenak.UniVoice.Outputs;
using Mirror;
using UnityEngine;

/// <summary>
/// Description: Synchronizes the position of the UniVoice audio output with the networked player's position for 3D spatialization.
/// Context: Attached to the player prefab.
/// Justification: Without this, all voice audio would play at the origin (0,0,0) instead of from the player's physical location in the game world.
/// </summary>
public class UniVoicePlayerAudio : NetworkBehaviour
{
    private int _cachedId = -1;
    private bool _hasConfiguredAudio = false;

    /// <summary>
    /// Description: Caches the Mirror connection ID of this player.
    /// Context: OnStartClient lifecycle event.
    /// Justification: The Connection ID is needed to find the specific UniVoice audio stream corresponding to this player model.
    /// </summary>
    public override void OnStartClient()
    {
        // Cache the connection ID from the player's movement or controller component
        if (TryGetComponent(out PlayerController m)) _cachedId = m.ConnectionId;
        else if (TryGetComponent(out PlayerObjectController c)) _cachedId = c.ConnectionId;
    }

    /// <summary>
    /// Description: Continuously moves the audio source to follow the player model.
    /// Context: Update lifecycle event.
    /// Justification: Required for real-time 3D spatial audio tracking as the player moves. Also configures the 3D falloff parameters once the audio source is bound.
    /// </summary>
    void Update()
    {
        // We only spatialized other players' audio, and only if we have a valid ID and session
        if (isLocalPlayer || _cachedId == -1 || UniVoiceMirrorSetupSample.ClientSession == null) return;

        // Find the audio output associated with this peer's ID
        if (UniVoiceMirrorSetupSample.ClientSession.PeerOutputs.TryGetValue(_cachedId, out var output))
        {
            var audioSource = (output as StreamedAudioSourceOutput).Stream.UnityAudioSource;

            // Move the audio source to the player's position (slightly elevated for better voice source feel)
            audioSource.transform.position = transform.position + Vector3.up * 1.5f;
            
            // Configure the 3D audio settings only once to avoid overriding inspector tweaks and save performance
            if (!_hasConfiguredAudio)
            {
                audioSource.spatialBlend = 1f;
                // Linear Rolloff helps voices carry further without dropping off too steeply
                audioSource.rolloffMode = AudioRolloffMode.Linear; 
                audioSource.minDistance = 3f; // Full volume up to 3 meters
                audioSource.maxDistance = 40f; // Silence after 40 meters
                
                _hasConfiguredAudio = true;
            }
        }
    }
}
