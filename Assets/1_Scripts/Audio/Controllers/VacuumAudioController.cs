using DG.Tweening;
using UnityEngine;

/// <summary>
/// Description: Manages the vacuum sound effects using a pre-rendered Audio Clip.
/// Context: Attached to the player's vacuum component and used in the lobby UI for previews.
/// Justification: Rather than generating audio purely in Unity, we use a synthesized clip from Vital and manipulate its pitch/volume dynamically with DOTween.
/// </summary>
public enum MusicalNote
{
    C = 0, CSharp = 1, D = 2, DSharp = 3, E = 4, F = 5,
    FSharp = 6, G = 7, GSharp = 8, A = 9, ASharp = 10, B = 11
}

[RequireComponent(typeof(AudioSource))]
public class VacuumAudioController : MonoBehaviour
{
    [Header("Audio Source References")]
    [Tooltip("Role: The AudioSource component.\nUse Case: Sound emission.\nJustification: Must contain the looping Vital audio clip.")]
    [SerializeField] private AudioSource _audioSource;

    [Header("Audio Dynamics (DOTween)")]
    [Range(0f, 1f)] public float MaxVolume = 0.6f;
    public float FadeInDuration = 0.05f;
    public float FadeOutDuration = 0.2f;
    public Ease FadeCurve = Ease.OutQuad;

    [Header("Musical Pitching")]
    [Tooltip("Role: Base pitch multiplier.\nUse Case: Tuning.\nJustification: 1 = Original DO/C from Vital. Used as the foundation for semitone shifts.")]
    [Range(0.1f, 3.0f)] public float BasePitch = 1.0f;


    [Tooltip("Role: Base musical note of the vacuum.\nUse Case: Lobby Customization.\nJustification: Players can select a root note to harmonize with others.")]
    public MusicalNote RootNote = MusicalNote.C;

    [Tooltip("Role: Dynamic pitch shift in semitones.\nUse Case: Chord resolution during gameplay.\nJustification: Allows scripts to dynamically alter the pitch without changing the base RootNote.")]
    public int SemitoneShift = 0;

    [Header("Debug")]
    [SerializeField] private bool _enableDebugLogs = false;

    private bool _isActive = false;
    public bool IsActive => _isActive;

    private Tween _volumeTween;
    private Tween _pitchTween;

    /// <summary>
    /// Description: Validates components and configures the audio source.
    /// Context: Awake lifecycle event.
    /// Justification: Ensures the audio source loops but starts silent, allowing DOTween to handle the fade-ins.
    /// </summary>
    private void Awake()
    {
        if (_audioSource == null) _audioSource = GetComponent<AudioSource>();

        // We no longer force 3D spatialBlend here so you can set it to 2D in the Lobby!

        _audioSource.loop = true;
        _audioSource.playOnAwake = false;


        _audioSource.volume = 0f;
    }

    /// <summary>
    /// Description: Starts the audio source playback (silently).
    /// Context: Start lifecycle event.
    /// Justification: The clip must be playing continuously in the background so that pitch/volume tweens can smoothly fade it in/out at any time.
    /// </summary>
    private void Start()
    {
        if (!_audioSource.isPlaying)
        {
            _audioSource.Play();
        }
    }

    /// <summary>
    /// Description: Toggles the vacuum sound on or off with smooth DOTween transitions.
    /// Context: Called by PlayerVacuumController or Lobby UI when the player activates the vacuum.
    /// Justification: Avoids abrupt audio cuts by fading volume and pitch simultaneously.
    /// </summary>
    /// <param name="active">True to turn on, false to turn off.</param>
    public void SetVacuumState(bool active)
    {
        if (_enableDebugLogs) Debug.Log($"[VacuumAudio] SetVacuumState({active}). IsPlaying={_audioSource.isPlaying}, CurrentVol={_audioSource.volume}");
        if (active == _isActive) return;
        _isActive = active;

        _volumeTween?.Kill();
        _pitchTween?.Kill();

        if (active)
        {
            // Safety catch: if it stopped for any reason, force it to play again!
            if (!_audioSource.isPlaying) _audioSource.Play();

            _volumeTween = _audioSource.DOFade(MaxVolume, FadeInDuration).SetEase(FadeCurve).SetUpdate(true);

            float targetPitch = GetCurrentTargetPitch();
            if (_audioSource.pitch < targetPitch * 0.5f) _audioSource.pitch = targetPitch * 0.5f;
            _pitchTween = _audioSource.DOPitch(targetPitch, FadeInDuration).SetEase(FadeCurve).SetUpdate(true);
        }
        else
        {
            // On ne fait plus Stop(), on laisse tourner en silence
            _volumeTween = _audioSource.DOFade(0f, FadeOutDuration).SetEase(FadeCurve).SetUpdate(true);

            _pitchTween = _audioSource.DOPitch(GetCurrentTargetPitch() * 0.3f, FadeOutDuration).SetEase(FadeCurve).SetUpdate(true);
        }
    }

    /// <summary>
    /// Description: Update the note offset dynamically to resolve chords.
    /// Context: Can be called by gameplay managers to alter the pitch of the vacuum.
    /// Justification: Allows musical interplay during gameplay without permanently modifying the user's customized RootNote.
    /// </summary>
    public void SetNoteOffset(int semitones)
    {
        SemitoneShift = semitones;
        if (_isActive)
        {
            _pitchTween?.Kill();
            _pitchTween = _audioSource.DOPitch(GetCurrentTargetPitch(), 0.1f).SetEase(Ease.InOutSine).SetUpdate(true);
        }
    }

    /// <summary>
    /// Description: Changes the fundamental base note of the vacuum sound.
    /// Context: Used by the Lobby Customization UI when the player selects a new note.
    /// Justification: Immediately updates the running tween to provide real-time audio feedback to the player.
    /// </summary>
    public void SetRootNote(MusicalNote newRootNote)
    {
        RootNote = newRootNote;
        if (_isActive)
        {
            _pitchTween?.Kill();
            _pitchTween = _audioSource.DOPitch(GetCurrentTargetPitch(), 0.1f).SetEase(Ease.InOutSine).SetUpdate(true);
        }
    }

    /// <summary>
    /// Description: Calculates the combined target pitch multiplier.
    /// Context: Called internally before triggering pitch tweens.
    /// Justification: Consolidates the RootNote and SemitoneShift into a single mathematical pitch float.
    /// </summary>
    private float GetCurrentTargetPitch()
    {
        int totalSemitones = (int)RootNote + SemitoneShift;
        return GetMusicalPitch(BasePitch, totalSemitones);
    }

    /// <summary>
    /// Description: Calculates the exact pitch multiplier needed for musical semitone shifts.
    /// Context: Mathematical audio helper function.
    /// Justification: Standard music theory formula (12th root of 2) used to correctly scale audio playback speeds to musical notes.
    /// </summary>
    public static float GetMusicalPitch(float basePitch, int semitones)
    {
        // The constant 1.059463094359f is the 12th root of 2.
        return basePitch * Mathf.Pow(1.059463094359f, semitones);
    }

    /// <summary>
    /// Description: Allows live-testing the note change directly from the Unity Inspector.
    /// Context: Unity Editor OnValidate event.
    /// Justification: Rapid prototyping and testing of audio levels without needing a custom inspector script.
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying && _isActive && _audioSource != null)
        {
            _audioSource.pitch = GetCurrentTargetPitch();
        }
    }
}

