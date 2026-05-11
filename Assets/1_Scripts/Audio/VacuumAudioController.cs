using DG.Tweening;
using UnityEngine;

/// <summary>
/// Manages the vacuum sound effects using a pre-rendered Audio Clip.
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
    [Tooltip("Put your Vital sound here (exported WITH its own LFO if you want). It must be set to loop.")]
    [SerializeField] private AudioSource _audioSource;

    [Header("Audio Dynamics (DOTween)")]
    [Range(0f, 1f)] public float MaxVolume = 0.6f;
    public float FadeInDuration = 0.05f;
    public float FadeOutDuration = 0.2f;
    public Ease FadeCurve = Ease.OutQuad;

    [Header("Musical Pitching")]
    [Tooltip("Base pitch multiplier (1 = Original DO/C from Vital)")]
    [Range(0.1f, 3.0f)] public float BasePitch = 1.0f;


    [Tooltip("Choose the root note you want this vacuum to play!")]
    public MusicalNote RootNote = MusicalNote.C;

    [Tooltip("Allows shifting pitch dynamically by exact semitones during gameplay to resolve chords")]
    public int SemitoneShift = 0;


    private bool _isActive = false;
    public bool IsActive => _isActive;

    private Tween _volumeTween;
    private Tween _pitchTween;

    private void Awake()
    {
        if (_audioSource == null) _audioSource = GetComponent<AudioSource>();

        // We no longer force 3D spatialBlend here so you can set it to 2D in the Lobby!

        _audioSource.loop = true;
        _audioSource.playOnAwake = false;


        _audioSource.volume = 0f;
    }

    private void Start()
    {
        if (!_audioSource.isPlaying)
        {
            _audioSource.Play();
        }
    }

    public void SetVacuumState(bool active)
    {
        Debug.Log($"[VacuumAudio] SetVacuumState({active}). IsPlaying={_audioSource.isPlaying}, CurrentVol={_audioSource.volume}");
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
    /// Update the note offset dynamically from outside scripts to resolve chords!
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
    /// Future method for the Lobby Radio Buttons to change the root note.
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

    private float GetCurrentTargetPitch()
    {
        int totalSemitones = (int)RootNote + SemitoneShift;
        return GetMusicalPitch(BasePitch, totalSemitones);
    }

    /// <summary>
    /// Calculates the exact pitch multiplier needed for musical semitone shifts.
    /// </summary>
    public static float GetMusicalPitch(float basePitch, int semitones)
    {
        // The constant 1.059463094359f is the 12th root of 2.
        return basePitch * Mathf.Pow(1.059463094359f, semitones);
    }

    // Allows live-testing the note change directly from the Unity Inspector
    private void OnValidate()
    {
        if (Application.isPlaying && _isActive && _audioSource != null)
        {
            _audioSource.pitch = GetCurrentTargetPitch();
        }
    }
}

