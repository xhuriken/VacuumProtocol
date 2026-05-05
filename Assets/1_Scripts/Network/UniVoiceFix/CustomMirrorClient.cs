#if MIRROR
using System;
using System.Linq;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Adrenak.UniVoice.Networks {
    public class CustomMirrorClient : IAudioClient<int> {
        public int ID { get; private set; } = -1;
        public List<int> PeerIDs { get; private set; } = new List<int>();
        public VoiceSettings YourVoiceSettings { get; private set; } = new VoiceSettings();

        public event Action<int, List<int>> OnJoined;
        public event Action OnLeft;
        public event Action<int> OnPeerJoined;
        public event Action<int> OnPeerLeft;
        public event Action<int, AudioFrame> OnReceivedPeerAudioFrame;

        public CustomMirrorClient() {
            NetworkClient.RegisterHandler<CustomVoiceMessage>(OnReceivedMessage, false);
            
            // FIX : Si on est déjà Host/Client, on essaie d'initialiser immédiatement
            if (NetworkClient.active) {
                CheckInitialStatus();
            }
        }

        private void CheckInitialStatus() {
            if (NetworkServer.active && NetworkClient.isConnected) {
                // On est le Host, on s'auto-identifie ID 0 si rien n'est fait
                if (ID == -1) {
                    Debug.Log("[Voice Fix] Host detected, forcing ID 0");
                    ID = 0;
                    OnJoined?.Invoke(ID, PeerIDs);
                }
            }
        }

        public void Dispose() {
            PeerIDs.Clear();
            NetworkClient.UnregisterHandler<CustomVoiceMessage>();
        }

        void OnReceivedMessage(CustomVoiceMessage msg) {
            var reader = new NetworkReader(new ArraySegment<byte>(msg.data));
            var tag = reader.ReadString();

            switch (tag) {
                case MirrorMessageTags.PEER_INIT:
                    ID = reader.ReadInt();
                    int count = reader.ReadInt();
                    PeerIDs.Clear();
                    for(int i=0; i<count; i++) PeerIDs.Add(reader.ReadInt());

                    OnJoined?.Invoke(ID, PeerIDs);
                    foreach (var peerId in PeerIDs) OnPeerJoined?.Invoke(peerId);
                    break;

                case MirrorMessageTags.PEER_JOINED:
                    var newPeerID = reader.ReadInt();
                    if (!PeerIDs.Contains(newPeerID)) {
                        PeerIDs.Add(newPeerID);
                        OnPeerJoined?.Invoke(newPeerID);
                    }
                    break;

                case MirrorMessageTags.PEER_LEFT:
                    var leftPeerID = reader.ReadInt();
                    if (PeerIDs.Contains(leftPeerID)) {
                        PeerIDs.Remove(leftPeerID);
                        OnPeerLeft?.Invoke(leftPeerID);
                    }
                    break;

                case MirrorMessageTags.AUDIO_FRAME:
                    var sender = reader.ReadInt();
                    if (sender == ID || !PeerIDs.Contains(sender)) return;
                    
                    var frame = new AudioFrame {
                        timestamp = reader.ReadLong(),
                        frequency = reader.ReadInt(),
                        channelCount = reader.ReadInt(),
                        samples = reader.ReadBytesAndSize()
                    };
                    OnReceivedPeerAudioFrame?.Invoke(sender, frame);
                    break;
            }
        }

        public void SendAudioFrame(AudioFrame frame) {
            if (ID == -1) {
                CheckInitialStatus(); // On retente une init si on est host
                if (ID == -1) return;
            }

            var writer = new NetworkWriter();
            writer.WriteString(MirrorMessageTags.AUDIO_FRAME);
            writer.WriteInt(ID);
            writer.WriteLong(frame.timestamp);
            writer.WriteInt(frame.frequency);
            writer.WriteInt(frame.channelCount);
            writer.WriteBytesAndSize(frame.samples);

            NetworkClient.Send(new CustomVoiceMessage { data = writer.ToArray() }, Channels.Unreliable);
        }

        public void SubmitVoiceSettings() {
            // Optionnel pour l'instant
        }

        public void UpdateVoiceSettings(Action<VoiceSettings> modification) {
            modification?.Invoke(YourVoiceSettings);
        }
    }
}
#endif
