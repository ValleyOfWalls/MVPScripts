using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Store the original GUI enabled state
        bool previousGUIState = GUI.enabled;
        
        // Disable the GUI
        GUI.enabled = false;
        
        // Draw the property
        EditorGUI.PropertyField(position, property, label);
        
        // Restore the original GUI enabled state
        GUI.enabled = previousGUIState;
    }
} 