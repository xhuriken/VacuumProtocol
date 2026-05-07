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

    [Header("Debug")]
    [SerializeField] private bool _enableDebugLogs = false;

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
    }

    private void TryFindPeerId()
    {
        // Try to find the ConnectionId on this object or parents
        if (TryGetComponent(out PlayerController m) && m.ConnectionId != -1) 
            _peerId = m.ConnectionId;
        else if (GetComponentInParent<PlayerController>() != null && GetComponentInParent<PlayerController>().ConnectionId != -1) 
            _peerId = GetComponentInParent<PlayerController>().ConnectionId;
        else if (TryGetComponent(out PlayerObjectController c) && c.ConnectionId != -1) 
            _peerId = c.ConnectionId;
        else if (GetComponentInParent<PlayerObjectController>() != null && GetComponentInParent<PlayerObjectController>().ConnectionId != -1) 
            _peerId = GetComponentInParent<PlayerObjectController>().ConnectionId;

        if (_peerId != -1 && _enableDebugLogs)
        {
            Debug.Log($"<color=orange>[MouthAnimator]</color> Successfully found PeerID: {_peerId} for {gameObject.name}");
        }
    }

    private System.Collections.IEnumerator SetupLocalMicLogging()
    {
        while (UniVoiceMirrorSetupSample.ClientSession == null) yield return null;

        if (_enableDebugLogs) Debug.Log("<color=green>[MouthAnimator]</color> Subscribed to Local Mic events.");

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
            // Try to find PeerID if we don't have it yet
            if (_peerId == -1)
            {
                TryFindPeerId();
            }

            // For remote players, ensure we have the correct AudioSource from UniVoice
            if (_remoteVoiceSource == null && _peerId != -1 && UniVoiceMirrorSetupSample.ClientSession != null)
            {
                if (UniVoiceMirrorSetupSample.ClientSession.PeerOutputs.TryGetValue(_peerId, out var output))
                {
                    if (output is Adrenak.UniVoice.Outputs.StreamedAudioSourceOutput streamedOutput)
                    {
                        _remoteVoiceSource = streamedOutput.Stream.UnityAudioSource;
                        if (_enableDebugLogs) Debug.Log($"<color=cyan>[MouthAnimator]</color> Linked AudioSource for Peer {_peerId}");
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

                if (_enableDebugLogs && peak > 0.001f && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[MouthAnimator] Peer {_peerId} speaking. Peak: {peak:F4} -> Target Vol: {targetVolume:F2}");
                }
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
