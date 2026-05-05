#if MIRROR
using UnityEngine;
using Adrenak.UniVoice;
using Adrenak.UniVoice.Outputs;
using Adrenak.UniVoice.Inputs;
using Adrenak.UniVoice.Networks;
using Adrenak.UniMic; // Important pour Mic et UniMicInput

public class CustomUniVoiceSetup : MonoBehaviour {
    public static ClientSession<int> ClientSession { get; private set; }
    
    [SerializeField] int _frameDurationMS = 60;
    
    private CustomMirrorServer _server;

    void Awake() {
        // Initialisation du micro (UniMic)
        Mic.Init();
        IAudioInput input;
        
        if (Mic.AvailableDevices.Count == 0) {
            Debug.LogWarning("[Voice Fix] No microphone found, using EmptyAudioInput.");
            input = new EmptyAudioInput();
        } else {
            var mic = Mic.AvailableDevices[0];
            mic.StartRecording(_frameDurationMS);
            input = new UniMicInput(mic);
            Debug.Log($"[Voice Fix] Recording started with {mic.Name}");
        }

        var client = new CustomMirrorClient();
        var outputFactory = new StreamedAudioSourceOutput.Factory();

        ClientSession = new ClientSession<int>(client, input, outputFactory);
        
        // On crée et démarre le serveur
        _server = new CustomMirrorServer();
        _server.StartServer();
        
        Debug.Log("[Voice Fix] Custom UniVoice Setup fully initialized.");
    }

    private void OnDestroy() {
        ClientSession?.Dispose();
        _server?.Dispose();
    }
}
#endif
