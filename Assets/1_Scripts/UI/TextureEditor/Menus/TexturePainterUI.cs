using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VacuumProtocol.UI.TextureEditor
{
    /// <summary>
    /// Description: UI canvas presenter linking UGUI RawImage pointer events to the non-UI TexturePainter engine.
    /// Context: Attached to the UI GameObject containing the painting RawImage.
    /// Justification: Translates screen interactions into exact canvas UV pixel coordinates and triggers SSOT brush cursor updates.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    [RequireComponent(typeof(RectTransform))]
    public class TexturePainterUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private RawImage _rawImage;
        private RectTransform _rectTransform;
        private TexturePainter _painter;
        private Canvas _rootCanvas;
        private bool _isHoveringCanvas;

        /// <summary>
        /// Description: Gets the underlying TexturePainter engine instance.
        /// Context: Read by TextureEditorPanelUI controller.
        /// Justification: Grants access to active painter algorithms and brush settings.
        /// </summary>
        public TexturePainter Painter => _painter;

        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            _rectTransform = GetComponent<RectTransform>();
            _rootCanvas = GetComponentInParent<Canvas>();

            _painter = new TexturePainter(20);
            _painter.OnCanvasUpdated += RefreshCanvasDisplay;
        }

        private void OnDestroy()
        {
            if (_painter != null)
            {
                _painter.OnCanvasUpdated -= RefreshCanvasDisplay;
            }
        }

        private void OnDisable()
        {
            if (_isHoveringCanvas)
            {
                _isHoveringCanvas = false;
                if (CustomCursorFollower.Instance != null)
                {
                    CustomCursorFollower.Instance.ResetCursorMode();
                }
            }
        }

        /// <summary>
        /// Description: Binds painter to an existing Texture2D or creates a new dynamic canvas.
        /// Context: Called by panel presenter when opening the editor.
        /// Justification: Supports both existing texture editing and blank canvas initialization.
        /// </summary>
        public void InitializeCanvas(int width = 128, int height = 128, Color? backgroundColor = null)
        {
            Color bg = backgroundColor ?? Color.white;
            _painter.InitializeCanvas(width, height, bg);
            _rawImage.texture = _painter.TargetTexture;
        }

        /// <summary>
        /// Description: Loads a pre-existing texture onto the canvas presenter.
        /// Context: Editing an existing asset texture.
        /// Justification: Sets up painter with loaded asset data.
        /// </summary>
        public void LoadTexture(Texture2D source)
        {
            _painter.LoadTexture(source);
            _rawImage.texture = _painter.TargetTexture;
        }

        #region EventSystem Pointer Handlers

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHoveringCanvas = true;
            UpdateCursorVisuals();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHoveringCanvas = false;
            if (_painter != null)
            {
                _painter.EndStroke();
            }

            if (CustomCursorFollower.Instance != null)
            {
                CustomCursorFollower.Instance.ResetCursorMode();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (TryGetPixelCoordinates(eventData, out Vector2Int pixelPos))
            {
                _painter.BeginStroke(pixelPos);
                UpdateCursorVisuals();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (TryGetPixelCoordinates(eventData, out Vector2Int pixelPos))
            {
                if (!_painter.IsStrokeActive)
                {
                    _painter.BeginStroke(pixelPos);
                }
                else
                {
                    _painter.ContinueStroke(pixelPos);
                }
                UpdateCursorVisuals();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _painter.EndStroke();
            UpdateCursorVisuals();
        }

        #endregion

        #region Coordinate Mapping & Cursor Dynamics

        /// <summary>
        /// Description: Converts screen space pointer input into discrete canvas pixel coordinates (x, y).
        /// Context: Called during pointer interaction callbacks.
        /// Justification: Handles UI scaling, resolution differences, and RectTransform pivots.
        /// </summary>
        private bool TryGetPixelCoordinates(PointerEventData eventData, out Vector2Int pixelPos)
        {
            pixelPos = Vector2Int.zero;
            Camera cam = (_rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _rootCanvas.worldCamera : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, eventData.position, cam, out Vector2 localPoint))
            {
                return false;
            }

            Rect rect = _rectTransform.rect;
            float normalizedX = (localPoint.x - rect.x) / rect.width;
            float normalizedY = (localPoint.y - rect.y) / rect.height;

            if (normalizedX < 0f || normalizedX > 1f || normalizedY < 0f || normalizedY > 1f)
            {
                return false;
            }

            int x = Mathf.Clamp(Mathf.FloorToInt(normalizedX * _painter.Width), 0, _painter.Width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(normalizedY * _painter.Height), 0, _painter.Height - 1);

            pixelPos = new Vector2Int(x, y);
            return true;
        }

        /// <summary>
        /// Description: Synchronizes SSOT CustomCursorFollower with current brush size and active color.
        /// Context: Called on pointer enter, hover, and drag.
        /// Justification: Provides high-polish dynamic brush outline preview on the canvas.
        /// </summary>
        private void UpdateCursorVisuals()
        {
            if (!_isHoveringCanvas || CustomCursorFollower.Instance == null || _painter == null)
            {
                return;
            }

            BrushSettings brush = _painter.BrushSettings;
            
            // Calculate exact UI unit pixel size of one canvas pixel
            float uiPixelSize = _rectTransform.rect.width / Mathf.Max(1, _painter.Width);
            
            // Calculate exact UI radius covering the full pixel stamp diameter (R + 0.5 pixels)
            float uiRadius = (brush.Radius + 0.5f) * uiPixelSize;

            bool isEraser = brush.Tool == PainterTool.Eraser;
            CustomCursorFollower.Instance.SetBrushCursorMode(true, uiRadius, brush.Color, isEraser);
        }

        private void RefreshCanvasDisplay()
        {
            if (_rawImage != null && _painter != null && _painter.TargetTexture != null)
            {
                _rawImage.texture = _painter.TargetTexture;
            }
        }

        #endregion
    }
}
