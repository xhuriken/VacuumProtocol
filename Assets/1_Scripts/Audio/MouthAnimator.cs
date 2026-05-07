using UnityEngine;
using Mirror;
using Adrenak.UniVoice;
using Adrenak.UniVoice.Samples;

/// <summary>
/// Animates the character's mouth scale based on voice volume.
/// Handles both the local player (Microphone) and remote players (AudioSource).
/// </summary>
public class MouthAnimator : NetworkBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private Transform _mouthTransform;
    [SerializeField] private Vector3 _minScale = Vector3.one;
    [SerializeField] private Vector3 _maxScale = new Vector3(1.5f, 1.5f, 1.5f);
    [SerializeField] private float _sensitivity = 5f;
    [SerializeField] private float _smoothSpeed = 15f;

    [Header("References")]
    [Tooltip("Leave empty to auto-find from UniVoice.")]
    [SerializeField] private AudioSource _remoteVoiceSource;
    [SerializeField] private PlayerVacuumController _vacuumController;

    private float _lastPeak = 0f;
    private float _currentVolume = 0f;
    private float[] _sampleBuffer = new float[256];
    private int _peerId = -1;

    private void Awake()
    {
        if (_mouthTransform == null) _mouthTransform = transform;
        if (_vacuumController == null) _vacuumController = GetComponentInParent<PlayerVacuumController>();
    }

    public override void OnStartClient()
    {
        if (isLocalPlayer)
        {
            StartCoroutine(SetupLocalMicLogging());
        }
        else
        {
            // Get the connection ID to identify this peer in UniVoice
            if (TryGetComponent(out PlayerController m)) 
                _peerId = m.ConnectionId;
            else if (GetComponentInParent<PlayerController>()) 
                _peerId = GetComponentInParent<PlayerController>().ConnectionId;
            else if (TryGetComponent(out PlayerObjectController c)) 
                _peerId = c.ConnectionId;
            else if (GetComponentInParent<PlayerObjectController>()) 
                _peerId = GetComponentInParent<PlayerObjectController>().ConnectionId;

            if (_peerId == -1) Debug.LogWarning($"[MouthAnimator] Could not find ConnectionId on {gameObject.name} or parents.");
        }
    }

    private System.Collections.IEnumerator SetupLocalMicLogging()
    {
        while (UniVoiceMirrorSetupSample.ClientSession == null) yield return null;

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

    private void Update()
    {
        float targetVolume = 0f;

        // 1. Get volume from the appropriate source
        if (isLocalPlayer)
        {
            targetVolume = Mathf.Clamp01(_lastPeak * _sensitivity);
        }
        else
        {
            // For remote players, ensure we have the correct AudioSource from UniVoice
            if (_remoteVoiceSource == null && _peerId != -1 && UniVoiceMirrorSetupSample.ClientSession != null)
            {
                if (UniVoiceMirrorSetupSample.ClientSession.PeerOutputs.TryGetValue(_peerId, out var output))
                {
                    if (output is Adrenak.UniVoice.Outputs.StreamedAudioSourceOutput streamedOutput)
                    {
                        _remoteVoiceSource = streamedOutput.Stream.UnityAudioSource;
                    }
                }
            }

            if (_remoteVoiceSource != null)
            {
                _remoteVoiceSource.GetOutputData(_sampleBuffer, 0);
                float peak = 0;
                foreach (var sample in _sampleBuffer)
                {
                    float abs = Mathf.Abs(sample);
                    if (abs > peak) peak = abs;
                }
                targetVolume = Mathf.Clamp01(peak * _sensitivity);
            }
        }

        // 2. Bypass: Force mouth open if vacuuming
        if (_vacuumController != null && _vacuumController.IsVacuuming)
        {
            targetVolume = 1f;
        }

        // 3. Smooth interpolation
        _currentVolume = Mathf.Lerp(_currentVolume, targetVolume, Time.deltaTime * _smoothSpeed);

        // 4. Apply scale
        if (_mouthTransform != null)
        {
            _mouthTransform.localScale = Vector3.Lerp(_minScale, _maxScale, _currentVolume);
        }
    }
}
