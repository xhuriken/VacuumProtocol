using DG.Tweening;
using UnityEngine;

/// <summary>
/// Manages the vacuum sound effects with a fun, modulated robot synth voice mixed with pink noise.
/// Features a "Tomodachi / Animalese" babble mode for organic, funny vocalization.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class VacuumAudioController : MonoBehaviour
{
    [Header("Audio Source References")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioLowPassFilter _lowPassFilter;

    [Header("Audio Dynamics (DOTween)")]
    [Range(0f, 1f)] public float MaxVolume = 0.6f;
    public float FadeInDuration = 0.1f;
    public float FadeOutDuration = 0.2f;
    public Ease FadeCurve = Ease.OutSine;

    [Header("Modulation Settings (Lobby)")]
    [Range(0.1f, 3.0f)] public float BasePitch = 1.0f;
    [Range(500f, 20000f)] public float FilterCutoff = 3500f;
    [Range(1.0f, 10.0f)] public float Resonance = 1.0f;

    [Header("Air Noise Settings")]
    [Range(0f, 1f)] public float PinkNoiseVolume = 0.5f;

    [Header("Robot Synth Settings")]
    [Range(0f, 1f)] public float RobotToneMix = 0.5f; // Mix between air noise and robot buzz
    [Range(10f, 1000f)] public float BaseFrequency = 120f; // The base note of the motor

    [Header("LFO / Babble Settings")]
    public bool UseBabbleMode = true; // Random jumping pitch like Tomodachi/Animal Crossing
    [Range(0.1f, 50f)] public float LfoSpeed = 15f; // How fast it changes notes or wobbles
    [Range(0f, 1f)] public float LfoIntensity = 0.5f; // Pitch variation range

    [Header("Debug")]
    public bool AlwaysPlay = false; // Forces the sound to play continuously


    private bool _isActive = false;
    private System.Random _rand = new System.Random();
    private Tween _volumeTween;
    private Tween _pitchTween;

    // Audio thread safe variables
    private volatile float _currentPitch = 1f;
    private double _sampleRate;
    private double _phase;
    private double _phase2; // Used for organic detune
    private double _lfoPhase;
    private float _babbleTarget = 0f;
    private float _currentBabble = 0f;

    // Pink noise variables
    private float b0, b1, b2, b3, b4, b5, b6;

    private void Awake()
    {
        if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
        if (_lowPassFilter == null) _lowPassFilter = GetComponent<AudioLowPassFilter>();

        if (_lowPassFilter == null)
        {
            _lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();
        }

        _sampleRate = AudioSettings.outputSampleRate;
        if (_sampleRate == 0) _sampleRate = 48000;

        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        _audioSource.volume = 0f;
        _audioSource.Stop();
    }

    public void SetVacuumState(bool active)
    {
        if (AlwaysPlay) active = true;

        if (active == _isActive) return;
        _isActive = active;

        _volumeTween?.Kill();
        _pitchTween?.Kill();

        if (active)
        {
            if (!_audioSource.isPlaying) _audioSource.Play();

            _volumeTween = _audioSource.DOFade(MaxVolume, FadeInDuration).SetEase(FadeCurve);

            if (_audioSource.pitch < BasePitch * 0.5f) _audioSource.pitch = BasePitch * 0.5f;
            _pitchTween = _audioSource.DOPitch(BasePitch, FadeInDuration).SetEase(FadeCurve);
        }
        else
        {
            _volumeTween = _audioSource.DOFade(0f, FadeOutDuration).SetEase(FadeCurve)
                .OnComplete(() => _audioSource.Stop());

            _pitchTween = _audioSource.DOPitch(BasePitch * 0.3f, FadeOutDuration).SetEase(Ease.OutQuad);
        }
    }

    private void Update()
    {
        if (AlwaysPlay && !_isActive)
        {
            SetVacuumState(true);
        }

        _lowPassFilter.cutoffFrequency = FilterCutoff;
        _lowPassFilter.lowpassResonanceQ = Resonance;
        
        // Cache the pitch for the audio thread
        _currentPitch = _audioSource.pitch;
    }

    public void UpdateParameters(float pitch, float cutoff, float resonance)
    {
        BasePitch = pitch;
        FilterCutoff = cutoff;
        Resonance = resonance;
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        // Cache variables to avoid thread locking issues
        float lfoSpeed = LfoSpeed;
        float lfoIntensity = LfoIntensity;
        float baseFreq = BaseFrequency;
        float toneMix = RobotToneMix;
        float currentPitch = _currentPitch;
        float pinkVol = PinkNoiseVolume;
        bool babble = UseBabbleMode;

        int sampleFrames = data.Length / channels;

        for (int i = 0; i < sampleFrames; i++)
        {
            // 1. Generate LFO (Sine Wave for wobbling, or Sample&Hold for Babble)
            _lfoPhase += lfoSpeed / _sampleRate;
            float lfoValue = 0f;

            if (babble)
            {
                if (_lfoPhase > 1.0)
                {
                    _lfoPhase -= 1.0;
                    _babbleTarget = (float)(_rand.NextDouble() * 2.0 - 1.0); // Pick a new random note
                }
                // Smoothly slide to the new pitch (portamento effect)
                _currentBabble = Mathf.Lerp(_currentBabble, _babbleTarget, 100f / (float)_sampleRate);
                lfoValue = _currentBabble;
            }
            else
            {
                if (_lfoPhase > 1.0) _lfoPhase -= 1.0;
                lfoValue = Mathf.Sin((float)(_lfoPhase * System.Math.PI * 2.0));
            }

            // 2. Modulate Frequency
            float targetFreq = baseFreq * currentPitch;
            // Exponential pitch modulation sounds much more natural for voice ranges
            targetFreq *= Mathf.Pow(2f, lfoValue * lfoIntensity); 

            // 3. Generate Organic Voice Tone (Triangle + Detuned Sawtooth = Thick Chorus)
            _phase += targetFreq / _sampleRate;
            if (_phase > 1.0) _phase -= 1.0;

            _phase2 += (targetFreq * 1.015f) / _sampleRate; // 1.5% detune
            if (_phase2 > 1.0) _phase2 -= 1.0;
            
            // Triangle wave (smoother, acts like vocal cords)
            float tri = (float)(_phase < 0.5 ? _phase * 4.0 - 1.0 : 3.0 - _phase * 4.0);
            // Sawtooth wave (adds that raspy mechanical buzz)
            float saw = (float)(_phase2 * 2.0 - 1.0);
            
            float robotTone = (tri * 0.7f) + (saw * 0.3f);

            // 4. Generate Pink Noise (Air rush)
            float white = (float)(_rand.NextDouble() * 2.0 - 1.0);
            b0 = 0.99886f * b0 + white * 0.0555179f;
            b1 = 0.99332f * b1 + white * 0.0750759f;
            b2 = 0.96900f * b2 + white * 0.1538520f;
            b3 = 0.86650f * b3 + white * 0.3104856f;
            b4 = 0.55000f * b4 + white * 0.5329522f;
            b5 = -0.7616f * b5 - white * 0.0168980f;
            float pink = b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362f;
            b6 = white * 0.115926f;
            pink *= 0.11f;

            // 5. Mix everything together
            float finalSample = Mathf.Lerp(pink * pinkVol, robotTone * 0.2f, toneMix);

            // 6. Write to all channels (Stereo/Mono)
            for (int c = 0; c < channels; c++)
            {
                data[i * channels + c] = finalSample;
            }
        }
    }
}

