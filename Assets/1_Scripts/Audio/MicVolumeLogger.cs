using UnityEngine;
using Adrenak.UniVoice;
using Adrenak.UniVoice.Samples;

/// <summary>
/// Animates the object's scale (e.g., character's mouth) based on microphone input volume.
/// Can be bypassed to force max scale when vacuuming.
/// </summary>
public class MicVolumeLogger : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private Vector3 _minScale = Vector3.one;
    [SerializeField] private Vector3 _maxScale = new Vector3(1.5f, 1.5f, 1.5f);
    [SerializeField] private float _sensitivity = 5f; // Multiplier to reach MaxScale more easily
    [SerializeField] private float _smoothSpeed = 15f;
    
    [Header("Vacuum Bypass")]
    [Tooltip("Drag the PlayerInputHandler here to detect when vacuuming is active.")]
    [SerializeField] private PlayerInputHandler _playerInput; 

    [Header("Debug")]
    [SerializeField] private bool _enableLogging = false;

    private float _lastPeak = 0f;
    private float _currentVolume = 0f;

    void Start() 
    {
        transform.localScale = _minScale;
        StartCoroutine(SetupLogger());
    }

    private System.Collections.IEnumerator SetupLogger() 
    {
        while (UniVoiceMirrorSetupSample.ClientSession == null) yield return null;
        
        // Listen to the LOCAL microphone input
        UniVoiceMirrorSetupSample.ClientSession.Input.OnFrameReady += frame => 
        {
            if (frame.samples == null) return;

            float peak = 0;
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
        // Safety check: if this is on a network player, we shouldn't animate THEIR mouth 
        // using OUR local microphone!
        if (_playerInput != null && !_playerInput.isLocalPlayer) return;

        // 1. Calculate raw volume (0 to 1) based on mic peak and sensitivity
        float targetVolume = Mathf.Clamp01(_lastPeak * _sensitivity);

        // 2. Bypass: If vacuuming, force mouth wide open (100%)
        if (_playerInput != null && _playerInput.IsVacuuming)
        {
            targetVolume = 1f;
        }

        // 3. Smoothly interpolate current volume for fluid animation
        _currentVolume = Mathf.Lerp(_currentVolume, targetVolume, Time.deltaTime * _smoothSpeed);

        // 4. Apply to Local Scale
        transform.localScale = Vector3.Lerp(_minScale, _maxScale, _currentVolume);

        // Debug
        if (_enableLogging && Time.frameCount % 60 == 0 && _lastPeak > 0.005f) 
        {
            Debug.Log($"[Voice Debug] Mic Volume: {(_lastPeak * 100).ToString("F2")}% -> Scale Lerp: {targetVolume}");
        }
    }
}
