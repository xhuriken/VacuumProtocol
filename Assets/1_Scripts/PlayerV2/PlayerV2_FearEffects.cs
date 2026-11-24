using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Description: Gère les effets visuels (PostProcessing) lorsque le joueur est effrayé.
/// Context: Attaché au Player_V2.
/// Justification: Modifie dynamiquement la Vignette (intensité et couleur) pour créer un effet de peur claustrophobique quand on regarde une entité de niveau 3+.
/// </summary>
[RequireComponent(typeof(PlayerViewRange))]
public class PlayerV2_FearEffects : MonoBehaviour
{
    [Header("Fear Post Processing Settings")]
    [Tooltip("Intensité de la vignette quand le joueur a peur (plus petite/serrée).")]
    public float FearVignetteIntensity = 0.6f;
    [Tooltip("Couleur de la vignette quand le joueur a peur.")]
    public Color FearVignetteColor = new Color(0.6f, 0f, 0f); // Rougeâtre sombre
    [Tooltip("Vitesse de transition de l'effet.")]
    public float TransitionSpeed = 5f;

    private Volume _globalVolume;
    private Vignette _vignette;
    
    private float _defaultIntensity;
    private Color _defaultColor;

    private PlayerViewRange _viewRange;

    private void Start()
    {
        _viewRange = GetComponent<PlayerViewRange>();

        // L'effet ne doit tourner que pour le joueur local (la caméra locale)
        if (_viewRange != null && !_viewRange.isLocalPlayer)
        {
            enabled = false;
            return;
        }

        // On récupère le volume global pour modifier ses overrides
        _globalVolume = FindObjectOfType<Volume>();
        if (_globalVolume != null && _globalVolume.profile != null)
        {
            // .profile crée une instance (clone) pour ne pas modifier l'Asset original dans l'éditeur
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

        // Condition de peur : On regarde une entité de niveau 3 ou plus
        bool isAfraid = _viewRange.HighestPriorityEntity != null && _viewRange.HighestPriorityEntity.PriorityLevel >= 3;

        // Cibles
        float targetIntensity = isAfraid ? FearVignetteIntensity : _defaultIntensity;
        Color targetColor = isAfraid ? FearVignetteColor : _defaultColor;

        // Interpolation fluide
        _vignette.intensity.value = Mathf.Lerp(_vignette.intensity.value, targetIntensity, Time.deltaTime * TransitionSpeed);
        _vignette.color.value = Color.Lerp(_vignette.color.value, targetColor, Time.deltaTime * TransitionSpeed);
    }

    private void OnDisable()
    {
        // On remet les valeurs par défaut si le script est désactivé (ex: joueur meurt)
        if (_vignette != null)
        {
            _vignette.intensity.value = _defaultIntensity;
            _vignette.color.value = _defaultColor;
        }
    }
}
