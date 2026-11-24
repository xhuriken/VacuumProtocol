using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Description: Système de rendu de Gizmos léger en temps réel (Runtime) fonctionnant en jeu et dans les Builds standalone.
/// Context: Appelé par PlayerV2_Gizmos et tout autre script de debug.
/// Justification: Permet d'afficher des lignes, cercles, sphères et cônes wireframe via GL.LINES directement sur le GPU avec 0 allocation.
/// </summary>
public static class RuntimeGizmos
{
    private struct LineData
    {
        public Vector3 Start;
        public Vector3 End;
        public Color Color;

        public LineData(Vector3 start, Vector3 end, Color color)
        {
            Start = start;
            End = end;
            Color = color;
        }
    }

    private static readonly List<LineData> _lines = new List<LineData>(2048);
    private static Material _lineMaterial;
    private static bool _isInitialized = false;

    /// <summary>
    /// Description: Initialise le système et souscrit au callback de rendu de la caméra.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Initialize()
    {
        if (_isInitialized) return;

        CreateLineMaterial();
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        Camera.onPostRender += OnCameraPostRender;
        _isInitialized = true;
    }

    private static void CreateLineMaterial()
    {
        if (_lineMaterial != null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) shader = Shader.Find("GUI/Text Shader");

        if (shader != null)
        {
            _lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull", (int)CullMode.Off);
            _lineMaterial.SetInt("_ZWrite", 0);
            _lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
        }
    }

    /// <summary>
    /// Description: Vérifie si les gizmos de debug sont activés dans les paramètres utilisateur.
    /// </summary>
    public static bool IsEnabled
    {
        get
        {
            if (SettingsManager.HasInstance)
            {
                return SettingsManager.Instance.CurrentSettings.ShowDebugGizmos;
            }
            return false;
        }
    }

    /// <summary>
    /// Description: Dessine une ligne entre deux points avec une couleur donnée.
    /// </summary>
    public static void DrawLine(Vector3 start, Vector3 end, Color color)
    {
        _lines.Add(new LineData(start, end, color));
    }

    /// <summary>
    /// Description: Dessine un rayon partant d'une origine dans une direction.
    /// </summary>
    public static void DrawRay(Vector3 origin, Vector3 direction, Color color)
    {
        _lines.Add(new LineData(origin, origin + direction, color));
    }

    /// <summary>
    /// Description: Dessine une sphère fil de fer en 3D.
    /// </summary>
    public static void DrawWireSphere(Vector3 center, float radius, Color color, int segments = 16)
    {
        if (radius <= 0.001f) return;

        // 3 cercles orthogonaux (XY, XZ, YZ)
        DrawWireCircle(center, Vector3.up, radius, color, segments);
        DrawWireCircle(center, Vector3.right, radius, color, segments);
        DrawWireCircle(center, Vector3.forward, radius, color, segments);
    }

    /// <summary>
    /// Description: Dessine un cercle fil de fer orienté selon une normale.
    /// </summary>
    public static void DrawWireCircle(Vector3 center, Vector3 normal, float radius, Color color, int segments = 24)
    {
        if (radius <= 0.001f || segments < 3) return;

        Vector3 forward = normal.normalized;
        Vector3 right = Vector3.Cross(forward, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.001f)
        {
            right = Vector3.Cross(forward, Vector3.right).normalized;
        }
        Vector3 up = Vector3.Cross(right, forward).normalized;

        Vector3 prevPoint = center + right * radius;
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i * 360f / segments) * Mathf.Deg2Rad;
            Vector3 nextPoint = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
            DrawLine(prevPoint, nextPoint, color);
            prevPoint = nextPoint;
        }
    }

    private static void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        RenderLines(camera);
    }

    private static void OnCameraPostRender(Camera camera)
    {
        RenderLines(camera);
    }

    private static void RenderLines(Camera camera)
    {
        if (_lines.Count == 0) return;
        if (camera == null || camera.cameraType == CameraType.Preview) return;

        CreateLineMaterial();
        if (_lineMaterial == null) return;

        _lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.LoadProjectionMatrix(camera.projectionMatrix);
        GL.modelview = camera.worldToCameraMatrix;
        GL.Begin(GL.LINES);

        for (int i = 0; i < _lines.Count; i++)
        {
            LineData line = _lines[i];
            GL.Color(line.Color);
            GL.Vertex(line.Start);
            GL.Vertex(line.End);
        }

        GL.End();
        GL.PopMatrix();

        // On vide la liste après le rendu de la caméra principale
        if (camera.cameraType == CameraType.Game || camera.cameraType == CameraType.SceneView)
        {
            _lines.Clear();
        }
    }
}
