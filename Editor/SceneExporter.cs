using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Reflection;
using System;

public class SceneExporter : EditorWindow
{
    private const string ExportFileName = "scene_script_references.txt";

    [MenuItem("Tools/Export Scene Script References")]
    public static void ExportSceneScriptReferences()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        if (!currentScene.IsValid())
        {
            Debug.LogError("No active scene or scene is not valid.");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Scene: {currentScene.name}");
        sb.AppendLine("====================================");

        GameObject[] rootObjects = currentScene.GetRootGameObjects();
        foreach (GameObject rootObject in rootObjects)
        {
            AppendGameObjectInfo(rootObject.transform, sb, 0);
        }

        // --- Determine Export Path ---
        string scriptPath = GetScriptPath();
        if (string.IsNullOrEmpty(scriptPath))
        {
             Debug.LogError("Could not find the SceneExporter script path. Cannot determine export directory.");
             return;
        }
        string exportDirectory = Path.GetDirectoryName(scriptPath);
        string filePath = Path.Combine(exportDirectory, ExportFileName);

        // --- Write to File ---
        try
        {
            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"Scene script references exported successfully to: {filePath}");
            AssetDatabase.Refresh(); // Refresh AssetDatabase to show the new file in Unity Editor
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to write scene export file to {filePath}. Reason: {e.Message}");
        }
    }

    private static void AppendGameObjectInfo(Transform transform, StringBuilder sb, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 4);
        sb.AppendLine($"{indent}- GameObject: {transform.name} (ID: {transform.gameObject.GetInstanceID()})");

        Component[] components = transform.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component == null)
            {
                sb.AppendLine($"{indent}  * [Missing Component]");
                continue;
            }

            // Check if it's a C# script (MonoBehaviour)
            // We can also check if the namespace is not Unity's to filter better,
            // but checking for MonoBehaviour is generally sufficient for user scripts.
            if (component is MonoBehaviour)
            {
                sb.AppendLine($"{indent}  * Component: {component.GetType().FullName}");
                AppendReferenceFields(component, sb, indentLevel + 1);
            }
            else
            {
                // Optionally list other component types if desired
                // sb.AppendLine($"{indent}  * Component: {component.GetType().FullName} (Non-Script)");
            }
        }

        // Recurse through children
        foreach (Transform child in transform)
        {
            AppendGameObjectInfo(child, sb, indentLevel + 1);
        }
    }

    private static void AppendReferenceFields(Component component, StringBuilder sb, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 4);
        Type componentType = component.GetType();
        FieldInfo[] fields = componentType.GetFields(BindingFlags.Public | BindingFlags.Instance);

        bool referencesFound = false;
        foreach (FieldInfo field in fields)
        {
            // Check if the field type derives from UnityEngine.Object (includes GameObjects, Components, Assets, etc.)
            if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
            {
                referencesFound = true;
                object value = field.GetValue(component);
                string valueName = "null";
                int valueInstanceID = 0;

                if (value != null)
                {
                    // Attempt to cast to UnityEngine.Object to get name and InstanceID
                    UnityEngine.Object unityObject = value as UnityEngine.Object;
                    if (unityObject != null)
                    {
                         valueName = unityObject.name;
                         valueInstanceID = unityObject.GetInstanceID();
                    }
                    else
                    {
                        // Handle cases where the object might not directly be a UnityEngine.Object but is assignable
                        // (less common for typical reference fields, might indicate complex serialization)
                        valueName = value.ToString();
                    }
                }

                sb.AppendLine($"{indent}  - Field: {field.Name} ({field.FieldType.Name}) = {valueName}{(valueInstanceID != 0 ? $" (ID: {valueInstanceID})" : "")}");
            }
        }

        if (!referencesFound)
        {
           // Optionally indicate if no reference fields were found
           // sb.AppendLine($"{indent}  (No public reference fields)");
        }
    }

     private static string GetScriptPath()
     {
        // Find the path of this script
        string[] guids = AssetDatabase.FindAssets($"t:Script {nameof(SceneExporter)}");
        return guids.Length > 0 ? AssetDatabase.GUIDToAssetPath(guids[0]) : null;
     }
} 