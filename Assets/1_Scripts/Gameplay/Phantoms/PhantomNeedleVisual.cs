using UnityEngine;

namespace Gameplay.Phantoms
{
    /// <summary>
    /// Description: Client-side visual representation of an electric ectoplasm needle fired by a phantom wasp.
    /// Features high-speed streak rendering via local-space LineRenderer, incandescent gradient, and automatic timeout cleanup.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PhantomNeedleVisual : MonoBehaviour
    {
        #region Runtime State
        private Vector3 _velocity;
        private float _lifetime;
        private LineRenderer _line;
        #endregion

        #region Initialization
        /// <summary>
        /// Description: Configures needle trajectory, lifetime, and visual styling.
        /// </summary>
        public void Init(Vector3 velocity, float lifetime, Material material)
        {
            _velocity = velocity;
            _lifetime = lifetime;

            _line = GetComponent<LineRenderer>();
            if (_line == null)
            {
                Debug.LogError($"[PhantomNeedleVisual] LineRenderer missing on '{gameObject.name}'!", this);
                Destroy(gameObject);
                return;
            }

            if (material == null)
            {
                Debug.LogError($"[PhantomNeedleVisual] Material parameter is null on '{gameObject.name}'! LineRenderer requires a valid material.", this);
            }
            else
            {
                _line.sharedMaterial = material;
            }

            // Local space setup: point 0 is tip (0,0,0), point 1 is trail tail behind (-Z)
            _line.useWorldSpace = false;
            _line.positionCount = 2;
            _line.SetPosition(0, Vector3.zero);
            _line.SetPosition(1, new Vector3(0f, 0f, -0.65f));

            // Sharp needle width profile (thin tip, taper body, fading tail)
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.05f);
            curve.AddKey(0.2f, 0.04f);
            curve.AddKey(1f, 0.005f);
            _line.widthCurve = curve;

            // Electric incandescence gradient: white tip -> bright electric cyan -> deep blue fade
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.25f, 0.95f, 1f), 0.35f),
                    new GradientColorKey(new Color(0.1f, 0.45f, 1f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.9f, 0.35f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            _line.colorGradient = gradient;

            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
        }
        #endregion

        #region Unity Lifecycle
        private void Update()
        {
            _lifetime -= Time.deltaTime;
            if (_lifetime <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            transform.position += _velocity * Time.deltaTime;
        }
        #endregion
    }
}
