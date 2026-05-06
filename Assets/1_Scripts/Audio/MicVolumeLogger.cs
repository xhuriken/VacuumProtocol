using UnityEngine;
using Adrenak.UniVoice;
using Adrenak.UniVoice.Samples;

/// <summary>
/// Monitors and logs the microphone input volume peaks for debugging UniVoice audio.
/// </summary>
public class MicVolumeLogger : MonoBehaviour
{
    [SerializeField] private bool _enableLogging = true;
    private float _lastPeak = 0f;

    void Start() 
    {
        StartCoroutine(SetupLogger());
    }

    /// <summary>
    /// Waits for the UniVoice session to initialize and subscribes to audio frame events.
    /// </summary>
    private System.Collections.IEnumerator SetupLogger() 
    {
        // List available microphones to help identify the active device
        string devices = string.Join(", ", Microphone.devices);
        Debug.Log($"[Voice Debug] Available Microphones: {devices}");

        // Wait until the global client session is available
        while (UniVoiceMirrorSetupSample.ClientSession == null) yield return null;
        
        // Subscribe to audio frame data from the input device
        UniVoiceMirrorSetupSample.ClientSession.Input.OnFrameReady += frame => 
        {
            if (!_enableLogging || frame.samples == null) return;

            float peak = 0;
            // Process samples (assuming 32-bit float format, 4 bytes per sample)
            // Common for high-end mics like Samson C03U at 48kHz in Unity
            for (int i = 0; i < frame.samples.Length; i += 4) 
            {
                if (i + 3 >= frame.samples.Length) break;
                
                float sample = System.BitConverter.ToSingle(frame.samples, i);
                float abs = Mathf.Abs(sample);
                if (abs > peak) peak = abs;
            }
            _lastPeak = peak;
        };
    }

    void Update() 
    {
        // Log the current peak level every 60 frames if it exceeds the noise threshold
        if (_enableLogging && Time.frameCount % 60 == 0) 
        {
            if (_lastPeak > 0.005f)
                Debug.Log($"[Voice Debug] Mic Volume Peak: {(_lastPeak * 100).ToString("F2")}%");
        }
    }
}
