using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class PrefabExporter : Editor
{
    private const string ExportDirectory = "Assets/Scripts/Assets";
    private const string ExportFileName = "PrefabDetails.txt";
    private static readonly string[] SearchFolders = { "Assets/Prefabs" }; // Limit search to specific folders

    [MenuItem("Tools/Export Prefab Details")]
    public static void ExportPrefabDetails()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Prefab Details Export");
        sb.AppendLine("=====================");
        sb.AppendLine($"Search Folders: {string.Join(", ", SearchFolders)}");
        sb.AppendLine();

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", SearchFolders);

        if (prefabGuids.Length == 0)
        {
            Debug.LogWarning($"No prefabs found in the specified folders: {string.Join(", ", SearchFolders)}");
            return;
        }

        int exportedCount = 0;
        foreach (string guid in prefabGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (prefabRoot != null)
            {
                sb.AppendLine($"--- Prefab: {assetPath} ---");
                AppendPrefabGameObjectDetails(prefabRoot.transform, sb, 0);
                sb.AppendLine(); // Add spacing between prefabs
                exportedCount++;
            }
            else
            {
                Debug.LogWarning($"Failed to load prefab at path: {assetPath}");
            }
        }

        // Ensure the target directory exists
        if (!Directory.Exists(ExportDirectory))
        {
            Directory.CreateDirectory(ExportDirectory);
            Debug.Log($"Created directory: {ExportDirectory}");
        }

        string filePath = Path.Combine(ExportDirectory, ExportFileName);
        File.WriteAllText(filePath, sb.ToString());

        Debug.Log($"Successfully exported details for {exportedCount} prefabs to: {filePath}");
        AssetDatabase.Refresh(); // Refresh AssetDatabase to show the new file in Unity Editor
    }

    private static void AppendPrefabGameObjectDetails(Transform transform, StringBuilder sb, int indentLevel)
    {
        sb.Append(new string(' ', indentLevel * 4)); // Indentation
        sb.Append("- " + transform.name);

        // Add component info
        List<string> componentNames = new List<string>();
        Component[] components = transform.GetComponents<Component>();
        foreach (var component in components)
        {
            if (component == null)
            {
                componentNames.Add("<Missing Component>");
                continue;
            }
            if (component is Transform) continue; // Skip Transform itself
            componentNames.Add(component.GetType().Name);
        }

        if (componentNames.Count > 0)
        {
            sb.Append(" (Components: ");
            sb.Append(string.Join(", ", componentNames));
            sb.Append(")");
        }

        sb.AppendLine();

        // Recursively process children
        foreach (Transform child in transform)
        {
            AppendPrefabGameObjectDetails(child, sb, indentLevel + 1);
        }
    }
} 