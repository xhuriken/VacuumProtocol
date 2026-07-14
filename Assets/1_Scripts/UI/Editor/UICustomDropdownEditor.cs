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

    private SerializedProperty _arrowLine1Prop;
    private SerializedProperty _arrowLine2Prop;
    private SerializedProperty _arrowLineSizeXProp;
    private SerializedProperty _arrowLineSizeYProp;
    private SerializedProperty _arrowParentProp;
    private SerializedProperty _arrowParentOffsetYProp;
    private SerializedProperty _arrowAnimDurationProp;
    private SerializedProperty _arrowAnimEaseProp;

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

        _arrowLine1Prop = serializedObject.FindProperty("_arrowLine1");
        _arrowLine2Prop = serializedObject.FindProperty("_arrowLine2");
        _arrowLineSizeXProp = serializedObject.FindProperty("_arrowLineSizeX");
        _arrowLineSizeYProp = serializedObject.FindProperty("_arrowLineSizeY");
        _arrowParentProp = serializedObject.FindProperty("_arrowParent");
        _arrowParentOffsetYProp = serializedObject.FindProperty("_arrowParentOffsetY");
        _arrowAnimDurationProp = serializedObject.FindProperty("_arrowAnimDuration");
        _arrowAnimEaseProp = serializedObject.FindProperty("_arrowAnimEase");

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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Arrow Animations", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_arrowLine1Prop);
            EditorGUILayout.PropertyField(_arrowLine2Prop);
            EditorGUILayout.PropertyField(_arrowLineSizeXProp);
            EditorGUILayout.PropertyField(_arrowLineSizeYProp);
            EditorGUILayout.PropertyField(_arrowParentProp);
            EditorGUILayout.PropertyField(_arrowParentOffsetYProp);
            EditorGUILayout.PropertyField(_arrowAnimDurationProp);
            EditorGUILayout.PropertyField(_arrowAnimEaseProp);
            
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
}
