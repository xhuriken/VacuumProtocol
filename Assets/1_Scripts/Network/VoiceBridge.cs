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
                Debug.Log($"[Voice] Trying to link audio for peer ID: {id}. Searching for robot with ConnectionId: {id}...");

                while(targetRobot == null && timeout > 0){
                    foreach (var identity in NetworkClient.spawned.Values) {
                        if (identity.TryGetComponent(out PlayerPhysicsMovement movement)) {
                            // On log tous les candidats pour débugger les IDs
                            // Debug.Log($"[Voice Debug] Candidate: {identity.name}, netId: {identity.netId}, ConnectionId: {movement.ConnectionId}");

                            if (movement.ConnectionId == id) {
                                targetRobot = identity.gameObject;
                                break;
                            }
                        }
                    }
                    
                    if (targetRobot == null) {
                        timeout -= 0.2f;
                        yield return new WaitForSeconds(0.2f);
                    }
                }

                if (targetRobot != null) {
                    audioOutput.transform.SetParent(targetRobot.transform);
                    audioOutput.transform.localPosition = Vector3.up; 

                    var source = audioOutput.GetComponent<AudioSource>();
                    if (source != null) {
                        source.spatialBlend = 1.0f; 
                        source.minDistance = 1f;
                        source.maxDistance = 30f;
                        source.rolloffMode = AudioRolloffMode.Linear;
                    }
                    Debug.Log($"[Voice] SUCCESS! Peer {id} linked to robot {targetRobot.name} (netId: {targetRobot.GetComponent<NetworkIdentity>().netId})");
                }
                else {
                    Debug.LogError($"[Voice] FAILED to find robot for peer {id} after timeout. (Searched for ConnectionId {id})");
                }
            }
        }
    }
}
