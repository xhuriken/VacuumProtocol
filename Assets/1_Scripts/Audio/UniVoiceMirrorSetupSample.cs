using Adrenak.UniMic;
using Adrenak.UniVoice.Filters;
using Adrenak.UniVoice.Inputs;
using Adrenak.UniVoice.Networks;
using Adrenak.UniVoice.Outputs;
using UnityEngine;

namespace Adrenak.UniVoice.Samples
{
    /// <summary>
    /// Handles the initialization and lifecycle of UniVoice with Mirror networking.
    /// Setup instructions:
    /// - Import Mirror and add 'UNIVOICE_NETWORK_MIRROR' to compilation symbols.
    /// - (Optional) Import RNNoise4Unity and add 'UNIVOICE_FILTER_RNNOISE4UNITY' for noise suppression.
    /// - Place this component in the first scene of your project.
    /// </summary>
    public class UniVoiceMirrorSetupSample : MonoBehaviour
    {
        const string TAG = "[BasicUniVoiceSetupSample]";

        /// <summary>
        /// Indicates if UniVoice has been initialized successfully.
        /// </summary>
        public static bool HasSetUp { get; private set; }

        /// <summary>
        /// Global reference to the UniVoice audio server.
        /// </summary>
        public static IAudioServer<int> AudioServer { get; private set; }

        /// <summary>
        /// Global reference to the UniVoice client session.
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
            // Mirror/Steamworks Fix: Sometimes the Host ID is incorrectly set to -1.
            // We force it to 0 to ensure local audio processing works correctly.
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
        }

        /// <summary>
        /// Orchestrates the setup of both server and client components.
        /// </summary>
        /// <returns>True if setup succeeded.</returns>
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

        /// <summary>
        /// Initializes the Mirror-specific audio server.
        /// </summary>
        /// <returns>True if successful.</returns>
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

        /// <summary>
        /// Initializes the Mirror-specific audio client session, microphone, and filters.
        /// </summary>
        /// <returns>True if successful.</returns>
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

            // Debug logs for session events
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
                // Log frame reception periodically to verify network traffic
                if (Time.frameCount % 200 == 0)
                    Debug.Log($"<color=cyan>[UniVoice Debug]</color> Receiving audio from Peer {id} ({frame.samples.Length} bytes)");
            };

            // Voice Activity Detection filter
            if (useVad)
            {
                ClientSession.InputFilters.Add(new SimpleVadFilter(new SimpleVad()));
            }

            // Opus encoding/decoding using Concentus
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
