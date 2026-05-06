using Adrenak.UniMic;
using Adrenak.UniVoice.Filters;
using Adrenak.UniVoice.Inputs;
using Adrenak.UniVoice.Networks;
using Adrenak.UniVoice.Outputs;
using UnityEngine;

namespace Adrenak.UniVoice.Samples
{
    /// <summary>
    /// To get this setup sample to work, ensure that you have done the following:
    /// - Import Mirror and add the UNIVOICE_NETWORK_MIRROR compilation symbol to your project
    /// - If you want to use RNNoise filter, import RNNoise4Unity into your project and add UNIVOICE_FILTER_RNNOISE4UNITY
    /// - Add this component to the first scene of your Unity project
    /// </summary>
    public class UniVoiceMirrorSetupSample : MonoBehaviour
    {
        const string TAG = "[BasicUniVoiceSetupSample]";

        /// <summary>
        /// Whether UniVoice has been setup successfully. This field will return true if the setup was successful.
        /// It runs on both server and client.
        /// </summary>
        public static bool HasSetUp { get; private set; }

        /// <summary>
        /// The server object.
        /// </summary>
        public static IAudioServer<int> AudioServer { get; private set; }

        /// <summary>
        /// The client session.
        /// </summary>
        public static ClientSession<int> ClientSession { get; private set; }

        [SerializeField] bool useConcentusEncodeAndDecode = true;
        [SerializeField] bool useVad = true;

        void Start()
        {
            if (HasSetUp)
            {
                Debug.Log($"[{TAG}] UniVoice is already set up. Ignoring...");
                return;
            }
            HasSetUp = Setup();
        }

        void Update()
        {
            // Fix for Host ID being stuck at -1 (common with Steamworks/FizzySteamworks)
            if (Mirror.NetworkServer.active && Mirror.NetworkClient.active)
            {
                if (ClientSession != null && ClientSession.Client != null && ClientSession.Client.ID == -1)
                {
                    var prop = ClientSession.Client.GetType().GetProperty("ID", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (prop != null)
                    {
                        prop.SetValue(ClientSession.Client, 0);
                        Debug.Log("<color=cyan>[UniVoice Fix]</color> Host ID forced to 0 (Steamworks Fix)");
                    }
                }
            }

            // Server-side debug: Log all connected clients (only on Host/Server)
            if (Mirror.NetworkServer.active && Time.frameCount % 60 == 0)
            {
                if (AudioServer is Adrenak.UniVoice.Networks.MirrorServer ms)
                {
                    Debug.Log($"<color=orange>[UniVoice Server]</color> Connected Clients IDs: {string.Join(", ", ms.ClientIDs)}");
                }
            }
        }

        bool Setup()
        {
            Debug.Log($"[{TAG}] Trying to setup UniVoice");

            bool failed = false;

            var createdAudioServer = SetupAudioServer();
            if (!createdAudioServer)
            {
                Debug.LogError($"[{TAG}] Could not setup UniVoice server.");
                failed = true;
            }

            var setupAudioClient = SetupClientSession();
            if (!setupAudioClient)
            {
                Debug.LogError($"[{TAG}] Could not setup UniVoice client.");
                failed = true;
            }

            if (!failed)
                Debug.Log($"[{TAG}] UniVoice successfully setup!");


            return !failed;
        }

        bool SetupAudioServer()
        {
#if MIRROR || UNIVOICE_NETWORK_MIRROR
            AudioServer = new MirrorServer();
            Debug.Log($"[{TAG}] Created MirrorServer object");

            AudioServer.OnServerStart += () =>
            {
                Debug.Log($"[{TAG}] Server started");
            };

            AudioServer.OnServerStop += () =>
            {
                Debug.Log($"[{TAG}] Server stopped");
            };
            return true;
#else
            Debug.LogError($"[{TAG}] MirrorServer implementation not found! Ensure MIRROR or UNIVOICE_NETWORK_MIRROR is defined.");
            return false;
#endif
        }

        bool SetupClientSession()
        {
#if MIRROR || UNIVOICE_NETWORK_MIRROR
            IAudioClient<int> client = new MirrorClient();
            client.OnJoined += (id, peerIds) =>
            {
                Debug.Log($"[{TAG}] You are Peer ID {id}");
            };

            client.OnLeft += () =>
            {
                Debug.Log($"[{TAG}] You left the chatroom");
            };

            client.OnPeerJoined += id =>
            {
                Debug.Log($"[{TAG}] Peer {id} joined");
            };

            client.OnPeerLeft += id =>
            {
                Debug.Log($"[{TAG}] Peer {id} left");
            };

            Debug.Log($"[{TAG}] Created MirrorClient object");

            IAudioInput input;
            Mic.Init();

            if (Mic.AvailableDevices.Count == 0)
            {
                Debug.LogWarning($"[{TAG}] Device has no microphones. Will only be able to hear other clients.");
                input = new EmptyAudioInput();
            }
            else
            {
                var mic = Mic.AvailableDevices[0];
                mic.StartRecording(60);
                Debug.Log($"[{TAG}] Started recording with Mic device: {mic.Name}");
                input = new UniMicInput(mic);
            }

            IAudioOutputFactory outputFactory = new StreamedAudioSourceOutput.Factory();
            Debug.Log($"[{TAG}] Using StreamedAudioSourceOutput.Factory");

            ClientSession = new ClientSession<int>(client, input, outputFactory);

            // --- DEBUG LOGS ---
            ClientSession.Client.OnJoined += (id, peers) =>
            {
                Debug.Log($"<color=green>[UniVoice Debug]</color> Local Client Initialized! My ID: {id}. Peers already here: {string.Join(", ", peers)}");
            };

            ClientSession.Client.OnPeerJoined += id =>
            {
                Debug.Log($"<color=yellow>[UniVoice Debug]</color> A new peer joined: {id}");
            };

            ClientSession.Client.OnReceivedPeerAudioFrame += (id, frame) =>
            {
                // Log every 200 frames received to avoid spamming but confirm reception
                if (Time.frameCount % 200 == 0)
                    Debug.Log($"<color=cyan>[UniVoice Debug]</color> Receiving audio from Peer {id} ({frame.samples.Length} bytes)");
            };
            // -------------------


            if (useVad)
            {
                ClientSession.InputFilters.Add(new SimpleVadFilter(new SimpleVad()));
            }

            if (useConcentusEncodeAndDecode)
            {
                ClientSession.InputFilters.Add(new ConcentusEncodeFilter());
                ClientSession.AddOutputFilter<ConcentusDecodeFilter>(() => new ConcentusDecodeFilter());
                Debug.Log($"[{TAG}] Registered Concentus filters");
            }

            return true;
#else
            Debug.LogError($"[{TAG}] MirrorClient implementation not found!");
            return false;
#endif
        }
    }
}
