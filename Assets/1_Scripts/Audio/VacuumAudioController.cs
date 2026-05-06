using UnityEngine;

/// <summary>
    /// Manages the vacuum sound effects with customizable parameters for frequency, timbre, and resonance.
    /// Designed to be modulated per-player for a unique auditory identity.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class VacuumAudioController : MonoBehaviour
    {
        [Header("Audio Source References")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioLowPassFilter _lowPassFilter;

        [Header("Modulation Settings (Lobby)")]
        [Range(0.1f, 3.0f)] public float BasePitch = 1.0f;
        [Range(500f, 20000f)] public float FilterCutoff = 5000f;
        [Range(1.0f, 10.0f)] public float Resonance = 1.0f;

        private bool _isActive = false;
        private System.Random _rand = new System.Random();

        private void Awake()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_lowPassFilter == null) _lowPassFilter = GetComponent<AudioLowPassFilter>();

            if (_lowPassFilter == null)
            {
                _lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();
            }

            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            _audioSource.Stop();
        }

        /// <summary>
        /// Toggles the vacuum sound on or off.
        /// </summary>
        /// <param name="active">True to start playing, false to stop.</param>
        public void SetVacuumState(bool active)
        {
            if (active && !_isActive)
            {
                _audioSource.Play();
                _isActive = true;
            }
            else if (!active && _isActive)
            {
                _audioSource.Stop();
                _isActive = false;
            }
        }

        private void Update()
        {
            if (!_isActive) return;

            // Apply real-time modulation
            // These will eventually be set by the player's custom profile
            _audioSource.pitch = BasePitch;
            _lowPassFilter.cutoffFrequency = FilterCutoff;
            _lowPassFilter.lowpassResonanceQ = Resonance;
        }

        /// <summary>
        /// Updates the vacuum parameters (e.g., from Lobby settings).
        /// </summary>
        public void UpdateParameters(float pitch, float cutoff, float resonance)
        {
            BasePitch = pitch;
            FilterCutoff = cutoff;
            Resonance = resonance;
        }

        /// <summary>
        /// Generates procedural white noise when the vacuum is active.
        /// This acts as a 'custom filter', preventing Unity from complaining about missing AudioClips.
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_isActive) return;

            for (int i = 0; i < data.Length; i++)
            {
                // Generate white noise between -0.5 and 0.5
                data[i] = (float)(_rand.NextDouble() * 2.0 - 1.0) * 0.5f;
            }
        }
    }
