using Adrenak.UniVoice;
using Mirror;
using UnityEngine;

/// <summary>
/// Description: Animates the character's mouth scale based on voice volume.
/// Context: Attached to the player's head/mouth mesh in the gameplay scene and lobby dummy.
/// Justification: Handles both the local player (reading raw Microphone data) and remote players (reading from the UniVoice AudioSource) to create an immersive VoIP experience.
/// </summary>
public class MouthAnimator : NetworkBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] 
    [Tooltip("Role: The transform of the mouth to scale.\nUse Case: Scaling the mesh.\nJustification: Decoupled from the root to allow animating only a specific child part.")]
    private Transform _mouthTransform;

    [SerializeField] 
    [Tooltip("Role: The base scale when silent.\nUse Case: Rest state.\nJustification: Prevents the mouth from completely disappearing (scale 0) when not talking.")]
    private Vector3 _minScale = Vector3.one;

    [SerializeField] 
    [Tooltip("Role: The peak scale when shouting.\nUse Case: Active state.\nJustification: Limits maximum mouth size to prevent model clipping.")]
    private Vector3 _maxScale = new Vector3(1.5f, 1.5f, 1.5f);

    [SerializeField] 
    [Tooltip("Role: Multiplier for raw RMS volume.\nUse Case: Visual exaggeration.\nJustification: Audio peak values are often very small (0.05), so we multiply them to reach a 0-1 range for the Lerp.")]
    private float _sensitivity = 15f;

    [SerializeField] 
    [Tooltip("Role: Speed of the mouth opening/closing.\nUse Case: Smoothing.\nJustification: Raw volume data is jittery; interpolating it creates a natural jaw movement.")]
    private float _smoothSpeed = 15f;

    [Tooltip("Role: Link to the vacuum controller.\nUse Case: Checking vacuum state.\nJustification: When vacuuming, we bypass the mic to force the mouth wide open.")]
    [SerializeField] private PlayerVacuumController _vacuumController;

    [Tooltip("Role: Link to the vacuum audio controller.\nUse Case: Lobby Dummy vacuum state.\nJustification: In the lobby, there's no gameplay controller, so we read the audio state directly.")]
    [SerializeField] private VacuumAudioController _vacuumAudioController;

    [Header("Preview Settings")]
    [Tooltip("Role: Flag to bypass network authority.\nUse Case: Lobby dummy preview.\nJustification: Allows a non-networked local prefab to react to the mic without triggering Mirror's isLocalPlayer errors.")]
    public bool IsLobbyPreviewDummy = false;

    // Hidden because it's assigned dynamically at runtime from UniVoice
    private AudioSource _remoteVoiceSource;

    [Header("Debug")]
    [SerializeField] 
    [Tooltip("Role: Enable console spam for debugging.\nUse Case: Tracing peer IDs.\nJustification: Only active when actively debugging VoIP issues.")]
    private bool _enableDebugLogs = false;

    private float _lastPeak = 0f;
    private float _currentVolume = 0f;
    private float[] _sampleBuffer = new float[256];
    private int _peerId = -1;

    /// <summary>
    /// Description: Auto-assigns references on startup.
    /// Context: Awake lifecycle event.
    /// Justification: Reduces manual inspector setup by attempting to find missing components on the same hierarchy.
    /// </summary>
    private void Awake()
    {
        if (_mouthTransform == null) _mouthTransform = transform;
        if (_vacuumController == null) _vacuumController = GetComponentInParent<PlayerVacuumController>();
        if (_vacuumAudioController == null) _vacuumAudioController = GetComponentInParent<VacuumAudioController>();
    }

    /// <summary>
    /// Description: Initializes local mic listening for the Lobby Dummy.
    /// Context: Start lifecycle event.
    /// Justification: The Lobby Dummy has no NetworkIdentity authority, so OnStartClient will not be reliable. We bypass it here.
    /// </summary>
    private void Start()
    {
        // If it's a dummy, we don't wait for OnStartClient. Start listening immediately.
        if (IsLobbyPreviewDummy)
        {
            StartCoroutine(SetupLocalMicLogging());
        }
    }

    /// <summary>
    /// Description: Initializes local mic listening for the actual networked player.
    /// Context: Mirror NetworkBehaviour lifecycle event.
    /// Justification: Ensures that only the true local player intercepts raw mic frames. Remote players will read from AudioSources instead.
    /// </summary>
    public override void OnStartClient()
    {
        if (isLocalPlayer && !IsLobbyPreviewDummy)
        {
            StartCoroutine(SetupLocalMicLogging());
        }
    }

    /// <summary>
    /// Description: Recursively searches for the Network Connection ID in parent components.
    /// Context: Needed by remote players to map this GameObject to the correct UniVoice audio stream.
    /// Justification: The hierarchy structure varies between Gameplay (PlayerController) and Lobby (PlayerObjectController). This robust search handles both.
    /// </summary>
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

    /// <summary>
    /// Description: Coroutine that hooks into the UniVoice local microphone stream.
    /// Context: Started once local authority is established.
    /// Justification: UniVoice might take a few frames to initialize the ClientSession. Yielding until it's ready prevents null reference crashes on startup.
    /// </summary>
    private System.Collections.IEnumerator SetupLocalMicLogging()
    {
        while (UniVoiceMirrorSetupSample.ClientSession == null) yield return null;

        if (_enableDebugLogs) Debug.Log("<color=green>[MouthAnimator]</color> Subscribed to Local Mic events.");

        UniVoiceMirrorSetupSample.ClientSession.Input.OnFrameReady += frame =>
        {
            if (frame.samples == null) return;

            // If the local voice activity detector determines the user is not speaking,
            // we discard the noise peak to prevent mouth wobbling.
            if (UniVoiceMirrorSetupSample.LocalVad != null && !UniVoiceMirrorSetupSample.LocalVad.IsSpeaking)
            {
                _lastPeak = 0f;
                return;
            }

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

    /// <summary>
    /// Description: Core loop updating the visual scale of the mouth every frame.
    /// Context: Update lifecycle event.
    /// Justification: Calculates target volume dynamically (either from the local mic peak or the remote AudioSource buffer), checks for vacuum overrides, and Lerps the scale.
    /// </summary>
    private void Update()
    {
        float targetVolume = 0f;

        // 1. Get volume from the appropriate source
        if (isLocalPlayer || IsLobbyPreviewDummy)
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

            // For remote players, we MUST find the UniVoice AudioSource.
            // Even if one was assigned in the inspector, it's likely the vacuum source, not the voice.
            if (_peerId != -1 && UniVoiceMirrorSetupSample.ClientSession != null)
            {
                if (UniVoiceMirrorSetupSample.ClientSession.PeerOutputs.TryGetValue(_peerId, out var output))
                {
                    if (output is Adrenak.UniVoice.Outputs.StreamedAudioSourceOutput streamedOutput)
                    {
                        AudioSource uniVoiceSource = streamedOutput.Stream.UnityAudioSource;

                        // If we haven't linked it yet or it changed, update it

                        if (_remoteVoiceSource != uniVoiceSource)
                        {
                            _remoteVoiceSource = uniVoiceSource;
                            if (_enableDebugLogs) Debug.Log($"<color=cyan>[MouthAnimator]</color> Successfully linked to Peer {_peerId}'s actual VOICE source.");
                        }
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
        bool isVacuumingGame = (_vacuumController != null && _vacuumController.IsVacuuming);
        bool isVacuumingLobby = (_vacuumAudioController != null && _vacuumAudioController.IsActive);

        if (isVacuumingGame || isVacuumingLobby)
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
