using System;
using System.Collections.Generic;
using UnityEngine;

namespace VacuumProtocol.UI.TextureEditor
{
    /// <summary>
    /// Description: Core dynamic non-UI painting engine managing pixel buffers, line interpolation, brush algorithms, and Undo/Redo operations.
    /// Context: Instantiated or managed by TexturePainterUI presenter.
    /// Justification: Decouples rendering algorithms from UI event listeners for extreme KISS flexibility and fast dynamic texture painting.
    /// </summary>
    public class TexturePainter
    {
        private Texture2D _targetTexture;
        private Color32[] _pixelBuffer;
        private int _width;
        private int _height;
        private Color32 _backgroundColor;
        private TextureUndoSystem _undoSystem;
        private BrushSettings _brushSettings;

        private Vector2Int? _lastPixelPosition;
        private bool _isStrokeActive;

        // Stroke layer buffers to prevent double-accumulation during a single drag event
        private Color32[] _strokeStartBuffer;
        private float[] _strokeAlphaBuffer;

        /// <summary>
        /// Description: Event raised when the Eyedropper tool picks a color from the canvas.
        /// Context: Subscribed to by UI presenter to update active color palette choice.
        /// Justification: Emits color pick notifications cleanly without direct UI coupling.
        /// </summary>
        public event Action<Color> OnColorPicked;

        /// <summary>
        /// Description: Event raised when canvas texture is updated or restored.
        /// Context: Subscribed to by UI presenter to refresh display or update undo/redo buttons.
        /// Justification: Notifies listeners of canvas mutations.
        /// </summary>
        public event Action OnCanvasUpdated;

        /// <summary>
        /// Description: Gets the active Texture2D instance being painted on.
        /// Context: Read by UI Presenters to assign to RawImage.
        /// Justification: Exposes texture target.
        /// </summary>
        public Texture2D TargetTexture => _targetTexture;

        /// <summary>
        /// Description: Gets whether a painting stroke is currently active.
        /// Context: Read by UI Presenter.
        /// Justification: Allows resuming stroke dynamically during pointer exit/re-entry.
        /// </summary>
        public bool IsStrokeActive => _isStrokeActive;

        /// <summary>
        /// Description: Gets the active BrushSettings reference.
        /// Context: Read/Modified by UI controls.
        /// Justification: Provides direct access to brush configurations.
        /// </summary>
        public BrushSettings BrushSettings => _brushSettings;

        /// <summary>
        /// Description: Gets the Undo/Redo history system instance.
        /// Context: Read by UI presentable action buttons.
        /// Justification: Exposes Undo/Redo capabilities.
        /// </summary>
        public TextureUndoSystem UndoSystem => _undoSystem;

        /// <summary>
        /// Description: Canvas pixel width.
        /// Context: Read accessor.
        /// Justification: Exposes dynamic canvas width.
        /// </summary>
        public int Width => _width;

        /// <summary>
        /// Description: Canvas pixel height.
        /// Context: Read accessor.
        /// Justification: Exposes dynamic canvas height.
        /// </summary>
        public int Height => _height;

        /// <summary>
        /// Description: Constructor for TexturePainter.
        /// Context: Initialization.
        /// Justification: Initializes brush settings and history engine.
        /// </summary>
        /// <param name="maxUndoSteps">Max history buffer steps.</param>
        public TexturePainter(int maxUndoSteps = 20)
        {
            _brushSettings = new BrushSettings();
            _undoSystem = new TextureUndoSystem(maxUndoSteps);
        }

        /// <summary>
        /// Description: Initializes a fresh blank texture with dynamic dimensions and background color.
        /// Context: Texture editor startup or New Canvas command.
        /// Justification: Supports arbitrary resolution dynamic texture creation.
        /// </summary>
        /// <param name="width">Canvas pixel width.</param>
        /// <param name="height">Canvas pixel height.</param>
        /// <param name="backgroundColor">Default canvas fill color.</param>
        public void InitializeCanvas(int width, int height, Color backgroundColor)
        {
            _width = Mathf.Max(16, width);
            _height = Mathf.Max(16, height);
            _backgroundColor = (Color32)backgroundColor;

            // Re-create Texture2D instance
            if (_targetTexture != null)
            {
                UnityEngine.Object.Destroy(_targetTexture);
            }

            _targetTexture = new Texture2D(_width, _height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, // Crisp pixel preview for drawing
                wrapMode = TextureWrapMode.Clamp
            };

            _pixelBuffer = new Color32[_width * _height];
            ClearCanvasSilently(_backgroundColor);

            _targetTexture.SetPixels32(_pixelBuffer);
            _targetTexture.Apply();

            _undoSystem.ClearHistory();
            OnCanvasUpdated?.Invoke();
        }

        /// <summary>
        /// Description: Initializes canvas from an existing Texture2D instance.
        /// Context: Editing an existing player texture asset or sprite.
        /// Justification: Allows dynamic texture loading into the painter.
        /// </summary>
        /// <param name="source">Source texture to copy pixel data from.</param>
        public void LoadTexture(Texture2D source)
        {
            if (source == null)
            {
                Debug.LogWarning("[TexturePainter] Attempted to load a null texture source.");
                return;
            }

            _width = source.width;
            _height = source.height;

            if (_targetTexture != null)
            {
                UnityEngine.Object.Destroy(_targetTexture);
            }

            _targetTexture = new Texture2D(_width, _height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            // Read source pixels
            if (source.isReadable)
            {
                _pixelBuffer = source.GetPixels32();
            }
            else
            {
                // Create temporary RenderTexture to copy unreadable textures safely
                RenderTexture tempRt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default);
                Graphics.Blit(source, tempRt);
                RenderTexture previousRt = RenderTexture.active;
                RenderTexture.active = tempRt;
                
                _targetTexture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                _targetTexture.Apply();
                
                RenderTexture.active = previousRt;
                RenderTexture.ReleaseTemporary(tempRt);

                _pixelBuffer = _targetTexture.GetPixels32();
            }

            _targetTexture.SetPixels32(_pixelBuffer);
            _targetTexture.Apply();

            _undoSystem.ClearHistory();
            OnCanvasUpdated?.Invoke();
        }

        /// <summary>
        /// Description: Clears the entire canvas to a solid target color.
        /// Context: Called by Clear UI command or reset.
        /// Justification: Resets pixel buffer and records state in undo history.
        /// </summary>
        /// <param name="color">Fill color.</param>
        public void ClearCanvas(Color color)
        {
            _undoSystem.PushState(_pixelBuffer);
            ClearCanvasSilently((Color32)color);
            ApplyPixelBuffer();
            OnCanvasUpdated?.Invoke();
        }

        private void ClearCanvasSilently(Color32 color)
        {
            for (int i = 0; i < _pixelBuffer.Length; i++)
            {
                _pixelBuffer[i] = color;
            }
        }

        /// <summary>
        /// Description: Begins a stroke action (e.g. mouse down event on canvas).
        /// Context: Triggered on PointerDown.
        /// Justification: Saves undo snapshot before drawing and starts line interpolation tracking.
        /// </summary>
        /// <param name="pixelPos">Target canvas pixel coordinate.</param>
        public void BeginStroke(Vector2Int pixelPos)
        {
            if (_pixelBuffer == null)
            {
                return;
            }

            _isStrokeActive = true;

            if (_brushSettings.Tool == PainterTool.Eyedropper)
            {
                SampleColorAt(pixelPos);
                return;
            }

            if (_brushSettings.Tool == PainterTool.FloodFill)
            {
                _undoSystem.PushState(_pixelBuffer);
                PerformFloodFill(pixelPos, (Color32)_brushSettings.Color);
                ApplyPixelBuffer();
                OnCanvasUpdated?.Invoke();
                return;
            }

            // Standard painting stroke: Push state to history stack
            _undoSystem.PushState(_pixelBuffer);

            // Initialize stroke layer buffers to prevent double-accumulation during a single drag event
            if (_strokeStartBuffer == null || _strokeStartBuffer.Length != _pixelBuffer.Length)
            {
                _strokeStartBuffer = new Color32[_pixelBuffer.Length];
            }
            System.Array.Copy(_pixelBuffer, _strokeStartBuffer, _pixelBuffer.Length);

            if (_strokeAlphaBuffer == null || _strokeAlphaBuffer.Length != _pixelBuffer.Length)
            {
                _strokeAlphaBuffer = new float[_pixelBuffer.Length];
            }
            System.Array.Clear(_strokeAlphaBuffer, 0, _strokeAlphaBuffer.Length);

            _lastPixelPosition = pixelPos;
            ApplyStamp(pixelPos);
            ApplyPixelBuffer();
        }

        /// <summary>
        /// Description: Continues an ongoing stroke during mouse drag.
        /// Context: Triggered on Drag.
        /// Justification: Uses Bresenham line algorithm to connect gaps between fast drag points.
        /// </summary>
        /// <param name="pixelPos">Target canvas pixel coordinate.</param>
        public void ContinueStroke(Vector2Int pixelPos)
        {
            if (!_isStrokeActive || _pixelBuffer == null)
            {
                return;
            }

            if (_brushSettings.Tool == PainterTool.Eyedropper)
            {
                SampleColorAt(pixelPos);
                return;
            }

            if (_brushSettings.Tool == PainterTool.FloodFill)
            {
                return; // Bucket fill is single click only
            }

            if (_lastPixelPosition.HasValue)
            {
                DrawLineInterpolated(_lastPixelPosition.Value, pixelPos);
            }
            else
            {
                ApplyStamp(pixelPos);
            }

            _lastPixelPosition = pixelPos;
            ApplyPixelBuffer();
        }

        /// <summary>
        /// Description: Ends the current active stroke.
        /// Context: Triggered on PointerUp or PointerExit.
        /// Justification: Resets drag state and invokes update callback.
        /// </summary>
        public void EndStroke()
        {
            _isStrokeActive = false;
            _lastPixelPosition = null;
            OnCanvasUpdated?.Invoke();
        }

        /// <summary>
        /// Description: Performs Undo operation.
        /// Context: Triggered by UI Undo button.
        /// Justification: Reverts canvas pixel buffer to previous state.
        /// </summary>
        public void Undo()
        {
            Color32[] restored = _undoSystem.PerformUndo(_pixelBuffer);
            if (restored != null)
            {
                System.Array.Copy(restored, _pixelBuffer, _pixelBuffer.Length);
                ApplyPixelBuffer();
                OnCanvasUpdated?.Invoke();
            }
        }

        /// <summary>
        /// Description: Performs Redo operation.
        /// Context: Triggered by UI Redo button.
        /// Justification: Restores previously undone pixel state.
        /// </summary>
        public void Redo()
        {
            Color32[] restored = _undoSystem.PerformRedo(_pixelBuffer);
            if (restored != null)
            {
                System.Array.Copy(restored, _pixelBuffer, _pixelBuffer.Length);
                ApplyPixelBuffer();
                OnCanvasUpdated?.Invoke();
            }
        }

        #region Painting Algorithms (Bresenham, Stamp, BFS Fill)

        /// <summary>
        /// Description: Connects two pixel points using Bresenham's line algorithm to paint seamless strokes.
        /// Context: Internal line rendering helper.
        /// Justification: Prevents ugly gaps when dragging mouse quickly across screen.
        /// </summary>
        private void DrawLineInterpolated(Vector2Int p0, Vector2Int p1)
        {
            int dx = Mathf.Abs(p1.x - p0.x);
            int dy = Mathf.Abs(p1.y - p0.y);
            int sx = p0.x < p1.x ? 1 : -1;
            int sy = p0.y < p1.y ? 1 : -1;
            int err = dx - dy;

            int x = p0.x;
            int y = p0.y;

            while (true)
            {
                ApplyStamp(new Vector2Int(x, y));

                if (x == p1.x && y == p1.y)
                {
                    break;
                }

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }
        }

        private void ApplyStamp(Vector2Int center)
        {
            int r = _brushSettings.Radius;
            Color32 brushColor = (Color32)_brushSettings.Color;
            float opacity = _brushSettings.Opacity;

            int minX = Mathf.Clamp(center.x - r, 0, _width - 1);
            int maxX = Mathf.Clamp(center.x + r, 0, _width - 1);
            int minY = Mathf.Clamp(center.y - r, 0, _height - 1);
            int maxY = Mathf.Clamp(center.y + r, 0, _height - 1);

            float radiusSq = r * r;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float distSq = (x - center.x) * (x - center.x) + (y - center.y) * (y - center.y);
                    if (distSq > radiusSq)
                    {
                        continue;
                    }

                    int index = y * _width + x;
                    float stampAlpha = 0f;
                    Color32 targetColor = brushColor;

                    switch (_brushSettings.Tool)
                    {
                        case PainterTool.Pencil:
                            stampAlpha = opacity;
                            targetColor = brushColor;
                            break;

                        case PainterTool.Eraser:
                            stampAlpha = opacity;
                            targetColor = _backgroundColor;
                            break;

                        case PainterTool.SoftBrush:
                            float dist = Mathf.Sqrt(distSq);
                            float normDist = dist / Mathf.Max(1f, r);
                            float alphaFactor = Mathf.Clamp01((1f - normDist) / Mathf.Max(0.01f, 1f - _brushSettings.Hardness));
                            stampAlpha = alphaFactor * opacity;
                            targetColor = brushColor;
                            break;

                        case PainterTool.Airbrush:
                            if (UnityEngine.Random.value < _brushSettings.SprayDensity)
                            {
                                stampAlpha = 0.4f * opacity;
                                targetColor = brushColor;
                            }
                            else
                            {
                                continue;
                            }
                            break;
                    }

                    // Update stroke alpha using Max operator to prevent double-accumulation during a single drag
                    if (_strokeAlphaBuffer != null && index < _strokeAlphaBuffer.Length)
                    {
                        _strokeAlphaBuffer[index] = Mathf.Max(_strokeAlphaBuffer[index], stampAlpha);
                        float finalAlpha = _strokeAlphaBuffer[index];

                        if (_strokeStartBuffer != null && index < _strokeStartBuffer.Length)
                        {
                            _pixelBuffer[index] = Color32.Lerp(_strokeStartBuffer[index], targetColor, finalAlpha);
                        }
                    }
                    else
                    {
                        // Fallback in case buffers are not active (e.g. single click fallback)
                        _pixelBuffer[index] = Color32.Lerp(_pixelBuffer[index], targetColor, stampAlpha);
                    }
                }
            }
        }

        /// <summary>
        /// Description: Performs a Breadth-First-Search (BFS) flood fill algorithm.
        /// Context: FloodFill tool.
        /// Justification: Fills contiguous pixel region without recursive stack overflow risk.
        /// </summary>
        private void PerformFloodFill(Vector2Int start, Color32 fillColor)
        {
            if (start.x < 0 || start.x >= _width || start.y < 0 || start.y >= _height)
            {
                return;
            }

            int targetIndex = start.y * _width + start.x;
            Color32 targetColor = _pixelBuffer[targetIndex];

            // Blend fill color onto target region using active brush opacity settings
            float opacity = _brushSettings.Opacity;
            Color32 blendedColor = Color32.Lerp(targetColor, fillColor, opacity);

            if (ColorsMatch(targetColor, blendedColor))
            {
                return;
            }

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            _pixelBuffer[targetIndex] = blendedColor;

            while (queue.Count > 0)
            {
                Vector2Int curr = queue.Dequeue();

                Vector2Int[] neighbors = new Vector2Int[]
                {
                    new Vector2Int(curr.x + 1, curr.y),
                    new Vector2Int(curr.x - 1, curr.y),
                    new Vector2Int(curr.x, curr.y + 1),
                    new Vector2Int(curr.x, curr.y - 1)
                };

                foreach (Vector2Int n in neighbors)
                {
                    if (n.x >= 0 && n.x < _width && n.y >= 0 && n.y < _height)
                    {
                        int idx = n.y * _width + n.x;
                        if (ColorsMatch(_pixelBuffer[idx], targetColor))
                        {
                            _pixelBuffer[idx] = blendedColor;
                            queue.Enqueue(n);
                        }
                    }
                }
            }
        }

        private bool ColorsMatch(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        /// <summary>
        /// Description: Samples color at the specified pixel position and fires OnColorPicked event.
        /// Context: Eyedropper tool.
        /// Justification: Allows player to sample existing canvas colors cleanly.
        /// </summary>
        private void SampleColorAt(Vector2Int pos)
        {
            if (pos.x < 0 || pos.x >= _width || pos.y < 0 || pos.y >= _height)
            {
                return;
            }

            int idx = pos.y * _width + pos.x;
            Color sampled = _pixelBuffer[idx];
            _brushSettings.Color = sampled;
            OnColorPicked?.Invoke(sampled);
        }

        private void ApplyPixelBuffer()
        {
            _targetTexture.SetPixels32(_pixelBuffer);
            _targetTexture.Apply();
        }

        #endregion
    }
}
