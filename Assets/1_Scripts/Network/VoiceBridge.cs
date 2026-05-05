using UnityEngine;
using Mirror;
using Adrenak.UniVoice; // Pour IAudioOutput
using Adrenak.UniVoice.Samples;
using Adrenak.UniVoice.Outputs;
using System.Collections;

public class VoiceBridge : MonoBehaviour 
{
    void Start() {
        StartCoroutine(SetupBridge());
    }

    private IEnumerator SetupBridge() {
        while (UniVoiceMirrorSetupSample.ClientSession == null) yield return null;

        // On s'abonne à l'événement du client réseau
        UniVoiceMirrorSetupSample.ClientSession.Client.OnPeerJoined += id => {
            StartCoroutine(LinkAudioToRobot(id));
        };
    }

    private IEnumerator LinkAudioToRobot(int id) {
        yield return new WaitForEndOfFrame();

        var session = UniVoiceMirrorSetupSample.ClientSession;
        
        // On récupère la sortie (IAudioOutput est dans Adrenak.UniVoice)
        if (session.PeerOutputs.TryGetValue(id, out IAudioOutput output)) {
            if (output is StreamedAudioSourceOutput audioOutput) {
                uint netId = (uint)id;
                
                float timeout = 5f;
                GameObject targetRobot = null;
                
                while(targetRobot == null && timeout > 0){
                    if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity))
                        targetRobot = identity.gameObject;
                    
                    timeout -= 0.1f;
                    yield return new WaitForSeconds(0.1f);
                }

                if (targetRobot != null) {
                    // TECHNIQUE : On déplace l'objet de voix d'UniVoice SUR le robot
                    // Pour que le son le suive dans l'espace 3D
                    audioOutput.transform.SetParent(targetRobot.transform);
                    audioOutput.transform.localPosition = Vector3.up; // Un peu au dessus du robot

                    // On configure l'AudioSource interne pour qu'il soit en 3D
                    var source = audioOutput.GetComponent<AudioSource>();
                    if (source != null) {
                        source.spatialBlend = 1.0f; // 100% 3D
                        source.minDistance = 1f;
                        source.maxDistance = 30f;
                        source.rolloffMode = AudioRolloffMode.Linear;
                    }
                    Debug.Log($"[Voice] Success! Voice object parented to {targetRobot.name}");
                }
            }
        }
    }
}
