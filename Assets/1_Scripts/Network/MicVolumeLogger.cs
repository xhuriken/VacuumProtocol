using UnityEngine;
using Adrenak.UniVoice;
using Adrenak.UniVoice.Samples;

public class MicVolumeLogger : MonoBehaviour
{
    [SerializeField] private bool _enableLogging = true;
    private float _lastPeak = 0f;

    void Start() {
        StartCoroutine(SetupLogger());
    }

    private System.Collections.IEnumerator SetupLogger() {
        // Liste des micros dispo pour aider l'utilisateur
        string devices = string.Join(", ", Microphone.devices);
        Debug.Log($"[Voice Debug] Available Microphones: {devices}");

        while (UniVoiceMirrorSetupSample.ClientSession == null) yield return null;
        
        // UniVoice 4.x utilise OnFrameReady avec un objet AudioFrame
        UniVoiceMirrorSetupSample.ClientSession.Input.OnFrameReady += frame => {
            if (!_enableLogging || frame.samples == null) return;

            float peak = 0;
            // On teste si les données sont en 32-bit float (4 bytes par échantillon)
            // car le Samson C03U à 48kHz utilise souvent ce format dans Unity.
            for (int i = 0; i < frame.samples.Length; i += 4) {
                if (i + 3 >= frame.samples.Length) break;
                
                float sample = System.BitConverter.ToSingle(frame.samples, i);
                float abs = Mathf.Abs(sample);
                if (abs > peak) peak = abs;
            }
            _lastPeak = peak;
        };
    }

    void Update() {
        if (_enableLogging && Time.frameCount % 30 == 0) {
            if (_lastPeak > 0.001f)
                Debug.Log($"[Voice Debug] Mic Volume Peak: {(_lastPeak * 100).ToString("F2")}%");
            else
                Debug.Log("[Voice Debug] Mic is silent or not sending data.");
        }
    }
}
