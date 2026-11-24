using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Description: Lightweight runtime Gizmos renderer that works in play mode and in standalone builds.
/// Context: Called by PlayerV2_Gizmos and any other debug script.
/// Justification: Draws wireframe lines, rays, circles and spheres through GL.LINES directly on the GPU with zero allocation.
/// </summary>
public static class RuntimeGizmos
{
    /// <summary>
    /// Description: One queued line segment with its color.
    /// Context: Stored in the static line buffer until the next camera render.
    /// Justification: A struct keeps the preallocated buffer allocation-free.
    /// </summary>
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
    /// Description: Initializes the system and subscribes to the camera rendering callbacks.
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
    /// Description: Returns whether debug gizmos are enabled in the user settings (false when no SettingsManager exists).
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
    /// Description: Draws a line between two points with the given color.
    /// </summary>
    public static void DrawLine(Vector3 start, Vector3 end, Color color)
    {
        _lines.Add(new LineData(start, end, color));
    }

    /// <summary>
    /// Description: Draws a ray starting at an origin along a direction.
    /// </summary>
    public static void DrawRay(Vector3 origin, Vector3 direction, Color color)
    {
        _lines.Add(new LineData(origin, origin + direction, color));
    }

    /// <summary>
    /// Description: Draws a 3D wireframe sphere.
    /// </summary>
    public static void DrawWireSphere(Vector3 center, float radius, Color color, int segments = 16)
    {
        if (radius <= 0.001f) return;

        // 3 orthogonal circles (XY, XZ, YZ)
        DrawWireCircle(center, Vector3.up, radius, color, segments);
        DrawWireCircle(center, Vector3.right, radius, color, segments);
        DrawWireCircle(center, Vector3.forward, radius, color, segments);
    }

    /// <summary>
    /// Description: Draws a wireframe circle oriented along a normal.
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

        // Clear the list after a Game or SceneView camera has rendered
        if (camera.cameraType == CameraType.Game || camera.cameraType == CameraType.SceneView)
        {
            _lines.Clear();
        }
    }
}
