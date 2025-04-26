using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using UnityEngine.SceneManagement;

public class SceneExporter : EditorWindow
{
    private const string ExportDirectory = "Assets/Scripts/Assets";
    private const string ExportFileName = "SceneHierarchy.txt";

    [MenuItem("Tools/Export Scene Hierarchy")]
    public static void ExportSceneHierarchy()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Scene: {currentScene.name}");
        sb.AppendLine("--------------------");

        GameObject[] rootObjects = currentScene.GetRootGameObjects();
        foreach (GameObject rootObject in rootObjects)
        {
            AppendGameObjectHierarchy(rootObject.transform, sb, 0);
        }

        // Ensure the target directory exists
        if (!Directory.Exists(ExportDirectory))
        {
            Directory.CreateDirectory(ExportDirectory);
            Debug.Log($"Created directory: {ExportDirectory}");
        }

        string filePath = Path.Combine(ExportDirectory, ExportFileName);
        File.WriteAllText(filePath, sb.ToString());

        Debug.Log($"Scene hierarchy exported to: {filePath}");
        AssetDatabase.Refresh(); // Refresh AssetDatabase to show the new file in Unity Editor
    }

    private static void AppendGameObjectHierarchy(Transform transform, StringBuilder sb, int indentLevel)
    {
        sb.Append(new string(' ', indentLevel * 4)); // Indentation
        sb.Append("- " + transform.name);

        // Optionally add component info
        // Component[] components = transform.GetComponents<Component>();
        // if (components.Length > 1) // All GameObjects have a Transform component
        // {
        //     sb.Append(" (Components: ");
        //     bool first = true;
        //     foreach (var component in components)
        //     {
        //          if (component is Transform) continue; // Skip Transform
        //         if (!first) sb.Append(", ");
        //         sb.Append(component.GetType().Name);
        //         first = false;
        //     }
        //      sb.Append(")");
        // }

        sb.AppendLine();

        foreach (Transform child in transform)
        {
            AppendGameObjectHierarchy(child, sb, indentLevel + 1);
        }
    }
} 