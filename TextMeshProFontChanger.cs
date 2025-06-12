using UnityEngine;
using UnityEditor;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class TextMeshProFontChanger : EditorWindow
{
    private TMP_FontAsset targetFont;
    private bool includeScene = true;
    private bool includePrefabs = true;
    private Vector2 scrollPosition;
    private List<TextMeshProUGUI> sceneTexts = new List<TextMeshProUGUI>();
    private List<TextMeshProUGUI> prefabTexts = new List<TextMeshProUGUI>();
    private bool showSceneResults = true;
    private bool showPrefabResults = true;

    [MenuItem("Tools/TextMeshPro Font Changer")]
    public static void ShowWindow()
    {
        GetWindow<TextMeshProFontChanger>("TMP Font Changer");
    }

    private void OnGUI()
    {
        GUILayout.Label("TextMeshPro Font Changer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Target Font", targetFont, typeof(TMP_FontAsset), false);
        EditorGUILayout.Space();

        includeScene = EditorGUILayout.Toggle("Include Scene Objects", includeScene);
        includePrefabs = EditorGUILayout.Toggle("Include Prefabs", includePrefabs);
        EditorGUILayout.Space();

        if (GUILayout.Button("Find TextMeshPro Components"))
        {
            FindTextComponents();
        }

        EditorGUILayout.Space();

        if (targetFont != null && (sceneTexts.Count > 0 || prefabTexts.Count > 0))
        {
            if (GUILayout.Button("Apply Font to All Found Components"))
            {
                ApplyFontToAll();
            }
        }

        EditorGUILayout.Space();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (sceneTexts.Count > 0)
        {
            showSceneResults = EditorGUILayout.Foldout(showSceneResults, $"Scene Objects ({sceneTexts.Count})");
            if (showSceneResults)
            {
                foreach (var text in sceneTexts)
                {
                    if (text != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.ObjectField(text, typeof(TextMeshProUGUI), true);
                        if (GUILayout.Button("Apply", GUILayout.Width(60)))
                        {
                            ApplyFont(text);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
        }

        if (prefabTexts.Count > 0)
        {
            showPrefabResults = EditorGUILayout.Foldout(showPrefabResults, $"Prefab Objects ({prefabTexts.Count})");
            if (showPrefabResults)
            {
                foreach (var text in prefabTexts)
                {
                    if (text != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.ObjectField(text, typeof(TextMeshProUGUI), true);
                        if (GUILayout.Button("Apply", GUILayout.Width(60)))
                        {
                            ApplyFont(text);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void FindTextComponents()
    {
        sceneTexts.Clear();
        prefabTexts.Clear();

        if (includeScene)
        {
            // Find all TextMeshProUGUI components in the scene
            sceneTexts = FindObjectsOfType<TextMeshProUGUI>().ToList();
        }

        if (includePrefabs)
        {
            // Find all prefabs in the project
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    TextMeshProUGUI[] texts = prefab.GetComponentsInChildren<TextMeshProUGUI>(true);
                    prefabTexts.AddRange(texts);
                }
            }
        }
    }

    private void ApplyFontToAll()
    {
        if (targetFont == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a target font first!", "OK");
            return;
        }

        int sceneCount = 0;
        int prefabCount = 0;

        // Apply to scene objects
        foreach (var text in sceneTexts)
        {
            if (text != null)
            {
                Undo.RecordObject(text, "Change TMP Font");
                text.font = targetFont;
                sceneCount++;
            }
        }

        // Apply to prefab objects
        foreach (var text in prefabTexts)
        {
            if (text != null)
            {
                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(text.gameObject);
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                TextMeshProUGUI[] allTexts = prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
                
                foreach (var prefabText in allTexts)
                {
                    Undo.RecordObject(prefabText, "Change TMP Font");
                    prefabText.font = targetFont;
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                prefabCount++;
            }
        }

        EditorUtility.DisplayDialog("Font Change Complete", 
            $"Font changed successfully!\nScene objects updated: {sceneCount}\nPrefab objects updated: {prefabCount}", 
            "OK");
    }

    private void ApplyFont(TextMeshProUGUI text)
    {
        if (targetFont == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a target font first!", "OK");
            return;
        }

        if (text == null) return;

        // Check if the text is part of a prefab
        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(text.gameObject);
        if (!string.IsNullOrEmpty(prefabPath))
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            TextMeshProUGUI[] allTexts = prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            
            foreach (var prefabText in allTexts)
            {
                if (prefabText == text)
                {
                    Undo.RecordObject(prefabText, "Change TMP Font");
                    prefabText.font = targetFont;
                    break;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
        else
        {
            Undo.RecordObject(text, "Change TMP Font");
            text.font = targetFont;
        }
    }
} 