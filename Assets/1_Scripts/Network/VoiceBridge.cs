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
                
                float timeout = 10f;
                GameObject targetRobot = null;
                Debug.Log($"[Voice] Trying to link audio for peer ID: {id}. Searching for robot with ConnectionId: {id}...");

                while(targetRobot == null && timeout > 0){
                    foreach (var identity in NetworkClient.spawned.Values) {
                        // On cherche d'abord le Robot (Jeu)
                        if (identity.TryGetComponent(out PlayerPhysicsMovement movement)) {
                            if (movement.ConnectionId == id) {
                                targetRobot = identity.gameObject;
                                break;
                            }
                        }
                        // Sinon on cherche le PlayerController (Lobby)
                        else if (identity.TryGetComponent(out PlayerObjectController lobbyPlayer)) {
                            if (lobbyPlayer.ConnectionId == id) {
                                targetRobot = identity.gameObject;
                                break;
                            }
                        }
                    }
                    
                    if (targetRobot == null) {
                        timeout -= 0.5f;
                        yield return new WaitForSeconds(0.5f);
                    }
                }

                var source = audioOutput.GetComponent<AudioSource>();
                if (targetRobot != null) {
                    audioOutput.transform.SetParent(targetRobot.transform);
                    audioOutput.transform.localPosition = Vector3.up; 

                    if (source != null) {
                        source.spatialBlend = 1.0f; // 3D
                        source.minDistance = 1f;
                        source.maxDistance = 30f;
                    }
                    Debug.Log($"[Voice] SUCCESS! Peer {id} linked to {targetRobot.name}");
                }
                else {
                    // FALLBACK : Si on ne trouve rien, on laisse en 2D pour pouvoir parler quand même
                    if (source != null) {
                        source.spatialBlend = 0f; // 2D (Global)
                    }
                    Debug.LogWarning($"[Voice] Fallback to 2D for peer {id} (No robot found)");
                }
            }
        }
    }
}
