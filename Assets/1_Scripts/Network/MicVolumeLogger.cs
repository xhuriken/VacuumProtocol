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
        while (UniVoiceMirrorSetupSample.ClientSession == null) yield return null;
        
        // UniVoice 4.x utilise OnFrameReady avec un objet AudioFrame
        UniVoiceMirrorSetupSample.ClientSession.Input.OnFrameReady += frame => {
            if (!_enableLogging || frame.samples == null) return;

            float peak = 0;
            // On convertit les bytes (PCM 16-bit) en volume pour le log
            for (int i = 0; i < frame.samples.Length; i += 2) {
                if (i + 1 >= frame.samples.Length) break;
                
                // Conversion 2 bytes -> short (16-bit)
                short sample = System.BitConverter.ToInt16(frame.samples, i);
                float abs = Mathf.Abs(sample / 32768f);
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
