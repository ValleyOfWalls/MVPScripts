using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Attribute to conditionally show/hide fields in the inspector based on other field values
/// </summary>
public class ConditionalFieldAttribute : PropertyAttribute
{
    public string ConditionalSourceField { get; private set; }
    public object[] CompareValues { get; private set; }
    public bool HideInInspector { get; private set; }

    public ConditionalFieldAttribute(string conditionalSourceField, object compareValue = null, bool hideInInspector = false)
    {
        ConditionalSourceField = conditionalSourceField;
        CompareValues = new object[] { compareValue ?? true };
        HideInInspector = hideInInspector;
    }
    
    public ConditionalFieldAttribute(string conditionalSourceField, object[] compareValues, bool hideInInspector = false)
    {
        ConditionalSourceField = conditionalSourceField;
        CompareValues = compareValues ?? new object[] { true };
        HideInInspector = hideInInspector;
    }
}

/// <summary>
/// Multi-condition attribute for OR logic
/// </summary>
public class ShowIfAnyAttribute : PropertyAttribute
{
    public string ConditionalSourceField { get; private set; }
    public object[] CompareValues { get; private set; }

    public ShowIfAnyAttribute(string conditionalSourceField, params object[] compareValues)
    {
        ConditionalSourceField = conditionalSourceField;
        CompareValues = compareValues;
    }
}

#if UNITY_EDITOR

/// <summary>
/// Property drawer for conditional fields
/// </summary>
[CustomPropertyDrawer(typeof(ConditionalFieldAttribute))]
public class ConditionalFieldPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        ConditionalFieldAttribute conditionalAttribute = (ConditionalFieldAttribute)attribute;
        
        // Find the source property
        SerializedProperty sourceProperty = property.serializedObject.FindProperty(conditionalAttribute.ConditionalSourceField);
        
        if (sourceProperty == null)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        // Check if condition is met
        bool conditionMet = IsAnyConditionMet(sourceProperty, conditionalAttribute.CompareValues);
        
        if (!conditionMet && conditionalAttribute.HideInInspector)
        {
            return; // Don't draw anything
        }

        // Draw the field with conditional styling
        bool wasEnabled = GUI.enabled;
        if (!conditionMet)
        {
            GUI.enabled = false;
        }

        EditorGUI.PropertyField(position, property, label);
        
        GUI.enabled = wasEnabled;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        ConditionalFieldAttribute conditionalAttribute = (ConditionalFieldAttribute)attribute;
        SerializedProperty sourceProperty = property.serializedObject.FindProperty(conditionalAttribute.ConditionalSourceField);
        
        if (sourceProperty != null)
        {
            bool conditionMet = IsAnyConditionMet(sourceProperty, conditionalAttribute.CompareValues);
            
            if (!conditionMet && conditionalAttribute.HideInInspector)
            {
                return 0f; // Hide completely
            }
        }

        return EditorGUI.GetPropertyHeight(property, label);
    }

    private bool IsAnyConditionMet(SerializedProperty sourceProperty, object[] compareValues)
    {
        foreach (var compareValue in compareValues)
        {
            if (IsConditionMet(sourceProperty, compareValue))
                return true;
        }
        return false;
    }

    private bool IsConditionMet(SerializedProperty sourceProperty, object compareValue)
    {
        switch (sourceProperty.propertyType)
        {
            case SerializedPropertyType.Boolean:
                return sourceProperty.boolValue.Equals(compareValue);
            case SerializedPropertyType.Integer:
                return sourceProperty.intValue.Equals(compareValue);
            case SerializedPropertyType.Float:
                return sourceProperty.floatValue.Equals(compareValue);
            case SerializedPropertyType.String:
                return sourceProperty.stringValue.Equals(compareValue);
            case SerializedPropertyType.Enum:
                // Handle enum comparison properly
                if (compareValue is int intValue)
                {
                    return sourceProperty.enumValueIndex == intValue;
                }
                return sourceProperty.enumValueIndex.Equals((int)compareValue);
            default:
                return false;
        }
    }
}

/// <summary>
/// Property drawer for ShowIfAny attribute
/// </summary>
[CustomPropertyDrawer(typeof(ShowIfAnyAttribute))]
public class ShowIfAnyPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        ShowIfAnyAttribute showIfAttribute = (ShowIfAnyAttribute)attribute;
        
        // Find the source property
        SerializedProperty sourceProperty = property.serializedObject.FindProperty(showIfAttribute.ConditionalSourceField);
        
        if (sourceProperty == null)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        // Check if any condition is met
        bool conditionMet = IsAnyConditionMet(sourceProperty, showIfAttribute.CompareValues);
        
        if (!conditionMet)
        {
            return; // Don't draw anything
        }

        EditorGUI.PropertyField(position, property, label);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        ShowIfAnyAttribute showIfAttribute = (ShowIfAnyAttribute)attribute;
        SerializedProperty sourceProperty = property.serializedObject.FindProperty(showIfAttribute.ConditionalSourceField);
        
        if (sourceProperty != null)
        {
            bool conditionMet = IsAnyConditionMet(sourceProperty, showIfAttribute.CompareValues);
            
            if (!conditionMet)
            {
                return 0f; // Hide completely
            }
        }

        return EditorGUI.GetPropertyHeight(property, label);
    }

    private bool IsAnyConditionMet(SerializedProperty sourceProperty, object[] compareValues)
    {
        foreach (var compareValue in compareValues)
        {
            if (IsConditionMet(sourceProperty, compareValue))
                return true;
        }
        return false;
    }

    private bool IsConditionMet(SerializedProperty sourceProperty, object compareValue)
    {
        switch (sourceProperty.propertyType)
        {
            case SerializedPropertyType.Boolean:
                return sourceProperty.boolValue.Equals(compareValue);
            case SerializedPropertyType.Integer:
                return sourceProperty.intValue.Equals(compareValue);
            case SerializedPropertyType.Float:
                return sourceProperty.floatValue.Equals(compareValue);
            case SerializedPropertyType.String:
                return sourceProperty.stringValue.Equals(compareValue);
            case SerializedPropertyType.Enum:
                if (compareValue is int intValue)
                {
                    return sourceProperty.enumValueIndex == intValue;
                }
                return sourceProperty.enumValueIndex.Equals((int)compareValue);
            default:
                return false;
        }
    }
}

/// <summary>
/// Custom property drawer for MultiEffect to ensure proper display
/// </summary>
[CustomPropertyDrawer(typeof(MultiEffect))]
public class MultiEffectPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.PropertyField(position, property, label, true);
        EditorGUI.EndProperty();
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}

/// <summary>
/// Custom property drawer for ConditionalEffect to ensure proper display
/// </summary>
[CustomPropertyDrawer(typeof(ConditionalEffect))]
public class ConditionalEffectPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.PropertyField(position, property, label, true);
        EditorGUI.EndProperty();
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}

/// <summary>
/// Custom property drawer for ScalingEffect to ensure proper display
/// </summary>
[CustomPropertyDrawer(typeof(ScalingEffect))]
public class ScalingEffectPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.PropertyField(position, property, label, true);
        EditorGUI.EndProperty();
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}

/// <summary>
/// Custom property drawer for PersistentFightEffect to ensure proper display
/// </summary>
[CustomPropertyDrawer(typeof(PersistentFightEffect))]
public class PersistentFightEffectPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.PropertyField(position, property, label, true);
        EditorGUI.EndProperty();
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}

/// <summary>
/// Custom property drawer for CardEffect to ensure proper display
/// </summary>
[CustomPropertyDrawer(typeof(CardEffect))]
public class CardEffectPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.PropertyField(position, property, label, true);
        EditorGUI.EndProperty();
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}

#endif 