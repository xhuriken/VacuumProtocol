using Mirror;

namespace Adrenak.UniVoice.Networks {
    // On change le nom pour éviter tout conflit avec la DLL UniVoice
    public struct CustomVoiceMessage : NetworkMessage {
        public byte[] data;
    }

    public static class CustomVoiceMessageFunctions {
        public static void WriteCustomVoiceMessage(this NetworkWriter writer, CustomVoiceMessage msg) {
            writer.WriteBytesAndSize(msg.data);
        }

        public static CustomVoiceMessage ReadCustomVoiceMessage(this NetworkReader reader) {
            return new CustomVoiceMessage {
                data = reader.ReadBytesAndSize()
            };
        }
    }

    public static class MirrorMessageTags {
        public const string PEER_INIT = "PEER_INIT";
        public const string PEER_JOINED = "PEER_JOINED";
        public const string PEER_LEFT = "PEER_LEFT";
        public const string AUDIO_FRAME = "AUDIO_FRAME";
        public const string VOICE_SETTINGS = "VOICE_SETTINGS";
    }
}
