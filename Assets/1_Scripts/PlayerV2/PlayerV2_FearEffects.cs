using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Description: Drives the fear post-processing effect (Vignette) when the player is scared.
/// Context: Attached to Player_V2, runs only for the local player.
/// Justification: Dynamically changes the Vignette intensity and color to create a claustrophobic fear effect while looking at an entity of priority level 3 or higher.
/// </summary>
[RequireComponent(typeof(PlayerViewRange))]
public class PlayerV2_FearEffects : MonoBehaviour
{
    [Header("Fear Post Processing Settings")]
    [Tooltip("Role: Vignette intensity while the player is afraid (tighter vignette).")]
    public float FearVignetteIntensity = 0.6f;
    [Tooltip("Role: Vignette color while the player is afraid.")]
    public Color FearVignetteColor = new Color(0.6f, 0f, 0f); // Dark reddish
    [Tooltip("Role: Transition speed of the effect (lerp factor per second).")]
    public float TransitionSpeed = 5f;

    private Volume _globalVolume;
    private Vignette _vignette;
    
    private float _defaultIntensity;
    private Color _defaultColor;

    private PlayerViewRange _viewRange;

    private void Start()
    {
        _viewRange = GetComponent<PlayerViewRange>();

        // The effect must only run for the local player (the local camera)
        if (_viewRange != null && !_viewRange.isLocalPlayer)
        {
            enabled = false;
            return;
        }

        // Fetch the global volume to modify its overrides
        _globalVolume = FindObjectOfType<Volume>();
        if (_globalVolume != null && _globalVolume.profile != null)
        {
            // .profile creates an instance (clone) so the original asset is not modified in the Editor
            if (_globalVolume.profile.TryGet(out _vignette))
            {
                _defaultIntensity = _vignette.intensity.value;
                _defaultColor = _vignette.color.value;
            }
        }
    }

    private void Update()
    {
        if (_vignette == null || _viewRange == null) return;

        // Fear condition: looking at an entity of priority level 3 or higher
        bool isAfraid = _viewRange.HighestPriorityEntity != null && _viewRange.HighestPriorityEntity.PriorityLevel >= 3;

        // Targets
        float targetIntensity = isAfraid ? FearVignetteIntensity : _defaultIntensity;
        Color targetColor = isAfraid ? FearVignetteColor : _defaultColor;

        // Smooth interpolation
        _vignette.intensity.value = Mathf.Lerp(_vignette.intensity.value, targetIntensity, Time.deltaTime * TransitionSpeed);
        _vignette.color.value = Color.Lerp(_vignette.color.value, targetColor, Time.deltaTime * TransitionSpeed);
    }

    private void OnDisable()
    {
        // Restore the default values when the script is disabled (e.g. player dies)
        if (_vignette != null)
        {
            _vignette.intensity.value = _defaultIntensity;
            _vignette.color.value = _defaultColor;
        }
    }
}
