using Adrenak.UniVoice.Outputs;
using Adrenak.UniVoice.Samples;
using Mirror;
using UnityEngine;

public class UniVoicePlayerAudio : NetworkBehaviour

{
    private int _cachedId = -1;

    public override void OnStartClient()
    {
        // Cache the ID once when the object spawns
        if (TryGetComponent(out PlayerPhysicsMovement m)) _cachedId = m.ConnectionId;
        else if (TryGetComponent(out PlayerObjectController c)) _cachedId = c.ConnectionId;
    }

    void Update()
    {
        if (isLocalPlayer || _cachedId == -1 || UniVoiceMirrorSetupSample.ClientSession == null) return;

        if (UniVoiceMirrorSetupSample.ClientSession.PeerOutputs.TryGetValue(_cachedId, out var output))
        {
            var audioSource = (output as StreamedAudioSourceOutput).Stream.UnityAudioSource;

            // Minimal follow logic

            audioSource.transform.position = transform.position + Vector3.up * 1.5f;
            audioSource.spatialBlend = 1f;
        }
    }
}
