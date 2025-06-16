using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

/// <summary>
/// Editor helper to automatically set up EntityModelManager components on NetworkEntity prefabs
/// </summary>
public class EntityModelManagerSetup : EditorWindow
{
    [MenuItem("MVPTools/Setup Entity Model Managers")]
    public static void ShowWindow()
    {
        GetWindow<EntityModelManagerSetup>("Entity Model Manager Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("Entity Model Manager Setup", EditorStyles.boldLabel);
        GUILayout.Space(10);

        GUILayout.Label("This tool will automatically add EntityModelManager components to all NetworkEntity prefabs that need them.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Add EntityModelManager to All NetworkEntity Prefabs"))
        {
            SetupEntityModelManagers();
        }

        GUILayout.Space(10);
        GUILayout.Label("Note: This will only affect prefabs that don't already have EntityModelManager components.", EditorStyles.helpBox);
    }

    private static void SetupEntityModelManagers()
    {
        int processedCount = 0;
        int modifiedCount = 0;

        // Find all prefabs in the project
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null) continue;

            // Check if this prefab has a NetworkEntity component
            NetworkEntity networkEntity = prefab.GetComponent<NetworkEntity>();
            if (networkEntity == null) continue;

            processedCount++;

            // Skip if it already has EntityModelManager
            EntityModelManager existingManager = prefab.GetComponent<EntityModelManager>();
            if (existingManager != null) continue;

            // Only add to Player and Pet entities (not Hand entities)
            if (networkEntity.EntityType != EntityType.Player && networkEntity.EntityType != EntityType.Pet)
                continue;

            // Add EntityModelManager component
            EntityModelManager modelManager = prefab.AddComponent<EntityModelManager>();
            
            // Save the prefab
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            
            modifiedCount++;
            Debug.Log($"EntityModelManagerSetup: Added EntityModelManager to {prefab.name} ({networkEntity.EntityType})");
        }

        AssetDatabase.Refresh();
        
        string message = $"Setup complete!\nProcessed {processedCount} NetworkEntity prefabs.\nAdded EntityModelManager to {modifiedCount} prefabs.";
        EditorUtility.DisplayDialog("Entity Model Manager Setup", message, "OK");
        
        Debug.Log($"EntityModelManagerSetup: {message}");
    }
} 