using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Shapes;


/// <summary>
/// Description: Custom Inspector editor for UICustomDropdown.
/// Context: Used within the Unity Editor to configure options, validate references, and support canvas hierarchy creation.
/// Justification: Improves the designer workflow by organizing visual settings, exposing warnings for missing dependencies, and adding a quick creation menu item.
/// </summary>
[CustomEditor(typeof(UICustomDropdown))]
[CanEditMultipleObjects]
public class UICustomDropdownEditor : Editor
{
    private SerializedProperty _rectProp;
    private SerializedProperty _backgroundRectProp;
    private SerializedProperty _buttonTextProp;

    private SerializedProperty _templateContainerProp;
    private SerializedProperty _itemTemplateProp;
    private SerializedProperty _itemParentProp;
    private SerializedProperty _listBorderProp;

    private SerializedProperty _hoverDurationProp;
    private SerializedProperty _hoverSizeOffsetProp;
    private SerializedProperty _hoverThicknessMultiplierProp;
    private SerializedProperty _dashSizeProp;
    private SerializedProperty _dashSpacingProp;
    private SerializedProperty _dashRotationSpeedProp;

    private SerializedProperty _animationDurationProp;
    private SerializedProperty _listHoverThicknessMultiplierProp;
    private SerializedProperty _listDashSizeProp;
    private SerializedProperty _listDashSpacingProp;
    private SerializedProperty _listDashRotationSpeedProp;

    private SerializedProperty _optionsProp;
    private SerializedProperty _valueProp;
    private SerializedProperty _onValueChangedProp;
    private SerializedProperty _interactableProp;
    private SerializedProperty _enableDebugLogsProp;

    private bool _showVisualGroups = true;
    private bool _showTemplateGroups = true;
    private bool _showAnimationGroups = false;
    private bool _showDataGroups = true;

    /// <summary>
    /// Description: Unity OnEnable callback. Resolves serializable properties.
    /// </summary>
    private void OnEnable()
    {
        _rectProp = serializedObject.FindProperty("_rect");
        _backgroundRectProp = serializedObject.FindProperty("_backgroundRect");
        _buttonTextProp = serializedObject.FindProperty("_buttonText");

        _templateContainerProp = serializedObject.FindProperty("_templateContainer");
        _itemTemplateProp = serializedObject.FindProperty("_itemTemplate");
        _itemParentProp = serializedObject.FindProperty("_itemParent");
        _listBorderProp = serializedObject.FindProperty("_listBorder");

        _hoverDurationProp = serializedObject.FindProperty("_hoverDuration");
        _hoverSizeOffsetProp = serializedObject.FindProperty("_hoverSizeOffset");
        _hoverThicknessMultiplierProp = serializedObject.FindProperty("_hoverThicknessMultiplier");
        _dashSizeProp = serializedObject.FindProperty("_dashSize");
        _dashSpacingProp = serializedObject.FindProperty("_dashSpacing");
        _dashRotationSpeedProp = serializedObject.FindProperty("_dashRotationSpeed");

        _animationDurationProp = serializedObject.FindProperty("_animationDuration");
        _listHoverThicknessMultiplierProp = serializedObject.FindProperty("_listHoverThicknessMultiplier");
        _listDashSizeProp = serializedObject.FindProperty("_listDashSize");
        _listDashSpacingProp = serializedObject.FindProperty("_listDashSpacing");
        _listDashRotationSpeedProp = serializedObject.FindProperty("_listDashRotationSpeed");

        _optionsProp = serializedObject.FindProperty("_options");
        _valueProp = serializedObject.FindProperty("_value");
        _onValueChangedProp = serializedObject.FindProperty("_onValueChanged");
        _interactableProp = serializedObject.FindProperty("_interactable");
        _enableDebugLogsProp = serializedObject.FindProperty("_enableDebugLogs");
    }

    /// <summary>
    /// Description: Standard OnInspectorGUI override to display the custom UI.
    /// </summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 1. Drawing Validation Checks
        DrawValidationWarnings();

        EditorGUILayout.Space();

        // 2. Base Interaction State
        EditorGUILayout.LabelField("Base Selectable Configuration", EditorStyles.boldLabel);
        if (_interactableProp != null) EditorGUILayout.PropertyField(_interactableProp);
        if (_enableDebugLogsProp != null) EditorGUILayout.PropertyField(_enableDebugLogsProp);

        EditorGUILayout.Space();

        // 3. Dropdown Data Settings Group
        _showDataGroups = EditorGUILayout.Foldout(_showDataGroups, "Dropdown Data & Options", true);
        if (_showDataGroups)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_optionsProp, true);
            EditorGUILayout.PropertyField(_valueProp);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        // 4. Visual Header References Group
        _showVisualGroups = EditorGUILayout.Foldout(_showVisualGroups, "Header Visual Components", true);
        if (_showVisualGroups)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_rectProp);
            EditorGUILayout.PropertyField(_backgroundRectProp);
            EditorGUILayout.PropertyField(_buttonTextProp);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        // 5. Template Container References Group
        _showTemplateGroups = EditorGUILayout.Foldout(_showTemplateGroups, "Template Container References", true);
        if (_showTemplateGroups)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_templateContainerProp);
            EditorGUILayout.PropertyField(_itemTemplateProp);
            EditorGUILayout.PropertyField(_itemParentProp);
            EditorGUILayout.PropertyField(_listBorderProp);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        // 6. Animation Settings Group (Collapsed by default)
        _showAnimationGroups = EditorGUILayout.Foldout(_showAnimationGroups, "Animation Settings", true);
        if (_showAnimationGroups)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Header Animations", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_hoverDurationProp);
            EditorGUILayout.PropertyField(_hoverSizeOffsetProp);
            EditorGUILayout.PropertyField(_hoverThicknessMultiplierProp);
            EditorGUILayout.PropertyField(_dashSizeProp);
            EditorGUILayout.PropertyField(_dashSpacingProp);
            EditorGUILayout.PropertyField(_dashRotationSpeedProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("List Transitions", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_animationDurationProp);
            EditorGUILayout.PropertyField(_listHoverThicknessMultiplierProp);
            EditorGUILayout.PropertyField(_listDashSizeProp);
            EditorGUILayout.PropertyField(_listDashSpacingProp);
            EditorGUILayout.PropertyField(_listDashRotationSpeedProp);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        // 7. Events Trigger Binding
        EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_onValueChangedProp);

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Description: Validates required inspector variables and renders dynamic help boxes for missing parts.
    /// </summary>
    private void DrawValidationWarnings()
    {
        if (_rectProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Header Outline Rectangle reference is missing. Hover outlines will not be drawn.", MessageType.Warning);
        }

        if (_buttonTextProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Button TextMeshProUGUI reference is missing. Selection labels will not display.", MessageType.Error);
        }

        if (_templateContainerProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Template Container RectTransform is missing. Options list cannot open.", MessageType.Error);
        }

        if (_itemTemplateProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Item Template component is missing. Options elements cannot be spawned at runtime.", MessageType.Error);
        }

        if (_itemParentProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Items parent RectTransform is missing. Spawned options will not layout correctly.", MessageType.Warning);
        }
    }

    /// <summary>
    /// Description: GameObject Menu item shortcut to create a pre-configured Shapes-based dropdown menu.
    /// </summary>
    /// <param name="menuCommand">Context metadata from the hierarchy menu.</param>
    [MenuItem("GameObject/UI/Shapes-Based Dropdown", false, 10)]
    public static void CreateShapesBasedDropdown(MenuCommand menuCommand)
    {
        // 1. Create root GameObject
        GameObject rootGo = new GameObject("Custom Dropdown", typeof(RectTransform), typeof(UICustomDropdown));

        // 2. Parent to context (such as active Canvas) and setup undo tracing
        GameObject parent = menuCommand.context as GameObject;
        if (parent == null)
        {
            // Try to find any active canvas in scene
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                parent = canvas.gameObject;
            }
            else
            {
                // Create a temporary canvas if none is present
                GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
                parent = canvasGo;

                // Ensure an EventSystem exists
                if (FindAnyObjectByType<EventSystem>() == null)
                {
                    GameObject eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                    Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");
                }
            }
        }

        GameObjectUtility.SetParentAndAlign(rootGo, parent);
        Undo.RegisterCreatedObjectUndo(rootGo, "Create Custom Dropdown");
        Selection.activeObject = rootGo;

        RectTransform rootRt = rootGo.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(250f, 50f);

        // 3. Create Header Outline Rectangle
        GameObject rectGo = new GameObject("Header Outline", typeof(RectTransform), typeof(Rectangle));
        rectGo.transform.SetParent(rootGo.transform, false);
        RectTransform rectRt = rectGo.GetComponent<RectTransform>();
        rectRt.anchorMin = Vector2.zero;
        rectRt.anchorMax = Vector2.one;
        rectRt.sizeDelta = Vector2.zero;
        Rectangle rect = rectGo.GetComponent<Rectangle>();
        rect.Type = Rectangle.RectangleType.RoundedBorder;
        rect.Thickness = 2f;
        rect.Color = Color.white;
        rect.CornerRadiusMode = Rectangle.RectangleCornerRadiusMode.Uniform;
        rect.CornerRadius = 4f;

        // 4. Create Header Background Rectangle
        GameObject bgGo = new GameObject("Header Background", typeof(RectTransform), typeof(Rectangle));
        bgGo.transform.SetParent(rootGo.transform, false);
        bgGo.transform.SetSiblingIndex(0); // Position under outline visual block
        RectTransform bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;
        Rectangle bgRect = bgGo.GetComponent<Rectangle>();
        bgRect.Type = Rectangle.RectangleType.RoundedSolid;
        bgRect.Color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        bgRect.CornerRadiusMode = Rectangle.RectangleCornerRadiusMode.Uniform;
        bgRect.CornerRadius = 4f;

        // 5. Create Header Text Label
        GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(rootGo.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = new Vector2(-24f, -10f); // Horizontal and vertical indent bounds
        TextMeshProUGUI text = textGo.GetComponent<TextMeshProUGUI>();
        text.text = "Select Option...";
        text.fontSize = 16f;
        text.alignment = TextAlignmentOptions.Left;
        text.color = Color.white;

        // 6. Create Template Container
        GameObject templateGo = new GameObject("Template", typeof(RectTransform));
        templateGo.transform.SetParent(rootGo.transform, false);
        RectTransform templateRt = templateGo.GetComponent<RectTransform>();
        templateRt.anchorMin = new Vector2(0f, 0f);
        templateRt.anchorMax = new Vector2(1f, 0f);
        templateRt.pivot = new Vector2(0.5f, 1f); // Pivot top center
        templateRt.anchoredPosition = new Vector2(0f, -2f); // Spacing margin
        templateRt.sizeDelta = new Vector2(0f, 120f);
        templateGo.SetActive(false); // Template is deactivated by default

        // 7. Create Template Outline Border
        GameObject listBorderGo = new GameObject("List Outline", typeof(RectTransform), typeof(Rectangle));
        listBorderGo.transform.SetParent(templateGo.transform, false);
        RectTransform listBorderRt = listBorderGo.GetComponent<RectTransform>();
        listBorderRt.anchorMin = Vector2.zero;
        listBorderRt.anchorMax = Vector2.one;
        listBorderRt.sizeDelta = Vector2.zero;
        Rectangle listBorder = listBorderGo.GetComponent<Rectangle>();
        listBorder.Type = Rectangle.RectangleType.RoundedBorder;
        listBorder.Thickness = 2f;
        listBorder.Color = Color.white;
        listBorder.CornerRadiusMode = Rectangle.RectangleCornerRadiusMode.Uniform;
        listBorder.CornerRadius = 4f;

        // 8. Create Template Background
        GameObject listBgGo = new GameObject("List Background", typeof(RectTransform), typeof(Rectangle));
        listBgGo.transform.SetParent(templateGo.transform, false);
        listBgGo.transform.SetSiblingIndex(0);
        RectTransform listBgRt = listBgGo.GetComponent<RectTransform>();
        listBgRt.anchorMin = Vector2.zero;
        listBgRt.anchorMax = Vector2.one;
        listBgRt.sizeDelta = Vector2.zero;
        Rectangle listBg = listBgGo.GetComponent<Rectangle>();
        listBg.Type = Rectangle.RectangleType.RoundedSolid;
        listBg.Color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        listBg.CornerRadiusMode = Rectangle.RectangleCornerRadiusMode.Uniform;
        listBg.CornerRadius = 4f;

        // 9. Create Option Layout Parent Content
        GameObject itemParentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        itemParentGo.transform.SetParent(templateGo.transform, false);
        RectTransform itemParentRt = itemParentGo.GetComponent<RectTransform>();
        itemParentRt.anchorMin = new Vector2(0f, 1f); // Anchor top-left
        itemParentRt.anchorMax = new Vector2(1f, 1f); // Anchor top-right
        itemParentRt.pivot = new Vector2(0.5f, 1f);  // Pivot top-center
        itemParentRt.anchoredPosition = new Vector2(0f, -4f); // Spacing from top border
        itemParentRt.sizeDelta = new Vector2(-8f, 0f); // 4px margin on left/right


        VerticalLayoutGroup layoutGroup = itemParentGo.GetComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = 2f;
        layoutGroup.childAlignment = TextAnchor.UpperCenter;
        layoutGroup.childControlHeight = true;
        layoutGroup.childControlWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.padding = new RectOffset(4, 4, 4, 4);

        ContentSizeFitter sizeFitter = itemParentGo.GetComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 10. Create Option Item Template
        GameObject itemTemplateGo = new GameObject("Item", typeof(RectTransform), typeof(UICustomDropdownItem));
        itemTemplateGo.transform.SetParent(itemParentRt, false);
        RectTransform itemRt = itemTemplateGo.GetComponent<RectTransform>();
        itemRt.sizeDelta = new Vector2(0f, 35f);

        // 11. Create Item Background, Item Outline, and label text fields
        GameObject itemBgGo = new GameObject("Item Background", typeof(RectTransform), typeof(Rectangle));
        itemBgGo.transform.SetParent(itemTemplateGo.transform, false);
        RectTransform itemBgRt = itemBgGo.GetComponent<RectTransform>();
        itemBgRt.anchorMin = Vector2.zero;
        itemBgRt.anchorMax = Vector2.one;
        itemBgRt.sizeDelta = Vector2.zero;
        Rectangle itemBg = itemBgGo.GetComponent<Rectangle>();
        itemBg.Type = Rectangle.RectangleType.RoundedSolid;
        itemBg.Color = new Color(0f, 0f, 0f, 0f);
        itemBg.CornerRadiusMode = Rectangle.RectangleCornerRadiusMode.Uniform;
        itemBg.CornerRadius = 2f;

        GameObject itemOutlineGo = new GameObject("Item Outline", typeof(RectTransform), typeof(Rectangle));
        itemOutlineGo.transform.SetParent(itemTemplateGo.transform, false);
        RectTransform itemOutlineRt = itemOutlineGo.GetComponent<RectTransform>();
        itemOutlineRt.anchorMin = Vector2.zero;
        itemOutlineRt.anchorMax = Vector2.one;
        itemOutlineRt.sizeDelta = Vector2.zero;
        Rectangle itemOutline = itemOutlineGo.GetComponent<Rectangle>();
        itemOutline.Type = Rectangle.RectangleType.RoundedBorder;
        itemOutline.Thickness = 2f;
        itemOutline.Color = Color.white;
        itemOutline.CornerRadiusMode = Rectangle.RectangleCornerRadiusMode.Uniform;
        itemOutline.CornerRadius = 2f;

        GameObject itemTextGo = new GameObject("Item Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        itemTextGo.transform.SetParent(itemTemplateGo.transform, false);
        RectTransform itemTextRt = itemTextGo.GetComponent<RectTransform>();
        itemTextRt.anchorMin = Vector2.zero;
        itemTextRt.anchorMax = Vector2.one;
        itemTextRt.sizeDelta = new Vector2(-12f, 0f);
        TextMeshProUGUI itemText = itemTextGo.GetComponent<TextMeshProUGUI>();
        itemText.text = "Option Item";
        itemText.fontSize = 14f;
        itemText.alignment = TextAlignmentOptions.Left;
        itemText.color = Color.white;

        // Binds Custom Item fields using SerializedObject utility for safety
        UICustomDropdownItem dropdownItem = itemTemplateGo.GetComponent<UICustomDropdownItem>();
        SerializedObject itemSerializedObj = new SerializedObject(dropdownItem);
        itemSerializedObj.FindProperty("_backgroundRect").objectReferenceValue = itemBg;
        itemSerializedObj.FindProperty("_rect").objectReferenceValue = itemOutline;
        itemSerializedObj.FindProperty("_itemText").objectReferenceValue = itemText;
        itemSerializedObj.ApplyModifiedProperties();

        // 12. Binds Custom Dropdown fields using SerializedObject
        UICustomDropdown dropdownComponent = rootGo.GetComponent<UICustomDropdown>();
        SerializedObject dropdownSerializedObj = new SerializedObject(dropdownComponent);
        dropdownSerializedObj.FindProperty("_rect").objectReferenceValue = rect;
        dropdownSerializedObj.FindProperty("_backgroundRect").objectReferenceValue = bgRect;
        dropdownSerializedObj.FindProperty("_buttonText").objectReferenceValue = text;
        dropdownSerializedObj.FindProperty("_templateContainer").objectReferenceValue = templateRt;
        dropdownSerializedObj.FindProperty("_itemTemplate").objectReferenceValue = dropdownItem;
        dropdownSerializedObj.FindProperty("_itemParent").objectReferenceValue = itemParentRt;
        dropdownSerializedObj.FindProperty("_listBorder").objectReferenceValue = listBorder;

        // Adds 3 default option strings to avoid empty initial states
        SerializedProperty optionsListProperty = dropdownSerializedObj.FindProperty("_options");
        optionsListProperty.ClearArray();
        optionsListProperty.InsertArrayElementAtIndex(0);
        optionsListProperty.GetArrayElementAtIndex(0).stringValue = "Option 1";
        optionsListProperty.InsertArrayElementAtIndex(1);
        optionsListProperty.GetArrayElementAtIndex(1).stringValue = "Option 2";
        optionsListProperty.InsertArrayElementAtIndex(2);
        optionsListProperty.GetArrayElementAtIndex(2).stringValue = "Option 3";

        dropdownSerializedObj.ApplyModifiedProperties();

        // Enforce the visual draw updates immediately
        dropdownComponent.SendMessage("OnValidate", SendMessageOptions.DontRequireReceiver);
    }
}
