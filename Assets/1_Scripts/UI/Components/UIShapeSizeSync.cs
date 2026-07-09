using UnityEngine;
using Shapes;

/// <summary>
/// Description: Utility component that automatically synchronizes a Shapes Rectangle's width and height with its RectTransform dimensions.
/// Context: Placed on a UI GameObject containing a Shapes Rectangle component (e.g. background panels).
/// Justification: Provides an editor-friendly, automated sizing tool that mimics standard Image sizing without manual dimension entries.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Rectangle))]
public class UIShapeSizeSync : MonoBehaviour
{
    [Tooltip("Role: Cached reference to the local Shapes Rectangle component.\nUse Case: Size updates.\nJustification: Avoids GetComponent calls during Update loop.")]
    [SerializeField] private Rectangle _rectangle;

    [Tooltip("Role: Caches the local RectTransform component.\nUse Case: Size reading.\nJustification: Avoids GetComponent calls during Update loop.")]
    [SerializeField] private RectTransform _rectTransform;

    /// <summary>
    /// Description: Unity Awake lifecycle event. Caches the required component references.
    /// </summary>
    private void Awake()
    {
        if (_rectangle == null) _rectangle = GetComponent<Rectangle>();
        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// Description: Unity Update lifecycle event. Continuously synchronizes dimensions in editor and play mode.
    /// </summary>
    private void Update()
    {
        if (_rectangle == null)
        {
            _rectangle = GetComponent<Rectangle>();
        }

        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        if (_rectangle != null && _rectTransform != null)
        {
            float targetWidth = _rectTransform.rect.width;
            float targetHeight = _rectTransform.rect.height;

            // Prevent redundant updates if dimensions are already equal
            if (!Mathf.Approximately(_rectangle.Width, targetWidth) || !Mathf.Approximately(_rectangle.Height, targetHeight))
            {
                _rectangle.Width = targetWidth;
                _rectangle.Height = targetHeight;
            }
        }
    }
}
