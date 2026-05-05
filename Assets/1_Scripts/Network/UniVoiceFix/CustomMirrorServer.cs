#if MIRROR
using System;
using System.Linq;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Adrenak.UniVoice.Networks {
    public class CustomMirrorServer {
        public List<int> ClientIDs { get; private set; } = new List<int>();

        public CustomMirrorServer() {
            NetworkServer.RegisterHandler<CustomVoiceMessage>(OnReceivedMessage, false);
        }

        public void StartServer() {
            ClientIDs.Clear();
            // Le Host se connecte toujours avec l'ID 0
            if (NetworkServer.active) {
                OnServerConnected(0);
            }
        }

        public void StopServer() {
            ClientIDs.Clear();
        }

        public void Dispose() {
            NetworkServer.UnregisterHandler<CustomVoiceMessage>();
        }

        public void OnServerConnected(int connId) {
            if (!ClientIDs.Contains(connId)) {
                // Notifier les anciens du nouveau
                var writerJoined = new NetworkWriter();
                writerJoined.WriteString(MirrorMessageTags.PEER_JOINED);
                writerJoined.WriteInt(connId);
                var joinedMsg = new CustomVoiceMessage { data = writerJoined.ToArray() };

                foreach (var peer in ClientIDs) {
                    if (NetworkServer.connections.ContainsKey(peer))
                        NetworkServer.connections[peer].Send(joinedMsg);
                }

                ClientIDs.Add(connId);

                // Notifier le nouveau de tous les anciens (incluant lui-même pour l'init)
                var writerInit = new NetworkWriter();
                writerInit.WriteString(MirrorMessageTags.PEER_INIT);
                writerInit.WriteInt(connId);
                writerInit.WriteInt(ClientIDs.Count);
                foreach (var peer in ClientIDs) writerInit.WriteInt(peer);
                
                var initMsg = new CustomVoiceMessage { data = writerInit.ToArray() };
                
                if (NetworkServer.connections.ContainsKey(connId)) {
                    NetworkServer.connections[connId].Send(initMsg);
                }
            }
        }

        public void OnServerDisconnected(int connId) {
            if (ClientIDs.Contains(connId)) {
                ClientIDs.Remove(connId);
                
                var writer = new NetworkWriter();
                writer.WriteString(MirrorMessageTags.PEER_LEFT);
                writer.WriteInt(connId);
                var msg = new CustomVoiceMessage { data = writer.ToArray() };

                foreach (var peer in ClientIDs) {
                    if (NetworkServer.connections.ContainsKey(peer))
                        NetworkServer.connections[peer].Send(msg);
                }
            }
        }

        void OnReceivedMessage(NetworkConnectionToClient connection, CustomVoiceMessage message) {
            var clientId = connection.connectionId;
            var reader = new NetworkReader(new ArraySegment<byte>(message.data));
            var tag = reader.ReadString();

            if (tag.Equals(MirrorMessageTags.AUDIO_FRAME)) {
                // On broadcast à tout le monde sauf l'envoyeur
                foreach (var recipientId in ClientIDs) {
                    if (recipientId == clientId) continue;

                    if (NetworkServer.connections.ContainsKey(recipientId)) {
                        NetworkServer.connections[recipientId].Send(message, Channels.Unreliable);
                    }
                }
            }
        }
    }
}
#endif
