using UnityEngine;

/// <summary>
/// Description: Standard archetype implementation of PhantomBase representing a basic wandering phantom.
/// Context: Attached to standard Phantom enemy prefabs.
/// Justification: Implements basic levitation/floating forces and sound/particle hooks upon suction and absorption.
/// </summary>
public class BasicPhantom : PhantomBase
{
    [Header("Organic Floating & Oscillation")]
    [Tooltip("Role: Anti-gravity floating force.\nUse Case: Keeps the phantom suspended in the air.")]
    [SerializeField]
    private bool _useZeroGravity = true;

    [Tooltip("Role: Frequency of vertical bobbing oscillation.\nUse Case: Rhythm of the floating motion.")]
    [SerializeField]
    private float _bobbingFrequency = 1.2f;

    [Tooltip("Role: Amplitude of vertical bobbing force.\nUse Case: Height variation of float.")]
    [SerializeField]
    private float _bobbingAmplitude = 2.0f;

    [Tooltip("Role: Subtle horizontal wobble force for natural organic floating.")]
    [SerializeField]
    private float _horizontalWobbleForce = 0.8f;

    [Tooltip("Role: Ambient damping to stabilize floating drift.")]
    [SerializeField]
    private float _floatingDamping = 2.5f;

    private float _randomSeedX;
    private float _randomSeedZ;

    [Header("Visual Effects & Audio")]
    [Tooltip("Role: Optional particle effect spawned upon being absorbed.")]
    [SerializeField]
    private ParticleSystem _absorptionVFX;

    [Tooltip("Role: Optional audio source for phantom sounds.")]
    [SerializeField]
    private AudioSource _audioSource;

    /// <summary>
    /// Description: Awake callback. Initializes random oscillation seeds.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        _randomSeedX = Random.Range(0f, 100f);
        _randomSeedZ = Random.Range(0f, 100f);
    }

    /// <summary>
    /// Description: FixedUpdate override to apply organic floating and oscillation forces while alive.
    /// </summary>
    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        if (IsAbsorbed || Rb == null)
        {
            return;
        }

        ApplyOrganicFloating();
    }

    /// <summary>
    /// Description: Applies anti-gravity, sinusoidal vertical bobbing, and subtle Perlin wobble to simulate organic floating.
    /// </summary>
    private void ApplyOrganicFloating()
    {
        // 1. Anti-Gravity to float effortlessly
        if (_useZeroGravity && Rb.useGravity)
        {
            Rb.AddForce(-Physics.gravity * Rb.mass, ForceMode.Force);
        }

        // If actively being vacuumed, external suction forces govern movement
        if (IsBeingVacuumed)
        {
            return;
        }

        // 2. Vertical Bobbing (Sine + slight noise)
        float time = Time.time;
        float verticalForce = Mathf.Sin(time * _bobbingFrequency + _randomSeedX) * _bobbingAmplitude;

        // 3. Subtle Horizontal Wobble (Perlin noise drift)
        float noiseX = (Mathf.PerlinNoise(time * 0.5f, _randomSeedX) - 0.5f) * 2f;
        float noiseZ = (Mathf.PerlinNoise(time * 0.5f, _randomSeedZ) - 0.5f) * 2f;
        Vector3 wobbleForce = new Vector3(noiseX, 0f, noiseZ) * _horizontalWobbleForce;

        Vector3 totalFloatForce = new Vector3(wobbleForce.x, verticalForce, wobbleForce.z);
        Rb.AddForce(totalFloatForce, ForceMode.Acceleration);

        // 4. Soft Damping to prevent runaway physics momentum while idle
        Vector3 linearVel = Rb.linearVelocity;
        Vector3 dragForce = -linearVel * _floatingDamping;
        Rb.AddForce(dragForce, ForceMode.Acceleration);
    }

    /// <summary>
    /// Description: Hook executed when the phantom starts being vacuumed.
    /// </summary>
    protected override void OnVacuumStart()
    {
        base.OnVacuumStart();
        // Custom visual/sound cue can be triggered here
    }

    /// <summary>
    /// Description: Client-side hook when the phantom is absorbed.
    /// </summary>
    /// <param name="suctionPlayer">The player GameObject that absorbed this phantom.</param>
    protected override void OnAbsorbedClient(GameObject suctionPlayer)
    {
        base.OnAbsorbedClient(suctionPlayer);

        if (_absorptionVFX != null)
        {
            _absorptionVFX.transform.SetParent(null);
            _absorptionVFX.Play();
            Destroy(_absorptionVFX.gameObject, 3f);
        }
    }
}
