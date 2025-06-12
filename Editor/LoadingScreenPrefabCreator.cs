using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using FishNet.Object;

/// <summary>
/// One-time editor script to create the LoadingScreen prefab with proper setup
/// Use: Window -> Custom Tools -> Create Loading Screen Prefab
/// </summary>
public class LoadingScreenPrefabCreator : EditorWindow
{
    private string prefabSavePath = "Assets/Prefabs/UI/";
    private string prefabName = "LoadingScreenPrefab";
    
    [MenuItem("Window/Custom Tools/Create Loading Screen Prefab")]
    public static void ShowWindow()
    {
        GetWindow<LoadingScreenPrefabCreator>("Loading Screen Creator");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Loading Screen Prefab Creator", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        prefabSavePath = EditorGUILayout.TextField("Save Path:", prefabSavePath);
        prefabName = EditorGUILayout.TextField("Prefab Name:", prefabName);
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Create Loading Screen Prefab", GUILayout.Height(30)))
        {
            CreateLoadingScreenPrefab();
        }
        
        GUILayout.Space(10);
        EditorGUILayout.HelpBox("This will create a complete loading screen prefab with:\n" +
                               "• Canvas with high sort order\n" +
                               "• NetworkObject component\n" +
                               "• LoadingScreenManager script\n" +
                               "• UI hierarchy (Panel, Background, Tip Image, Loading Text)\n" +
                               "• Proper component references", MessageType.Info);
    }
    
    private void CreateLoadingScreenPrefab()
    {
        try
        {
            // Create main LoadingScreen GameObject
            GameObject loadingScreen = new GameObject("LoadingScreen");
            
            // Add Canvas component and configure
            Canvas canvas = loadingScreen.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            canvas.overrideSorting = true;
            
            // Add CanvasScaler for responsive UI
            CanvasScaler canvasScaler = loadingScreen.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;
            
            // Add GraphicRaycaster for UI interactions
            loadingScreen.AddComponent<GraphicRaycaster>();
            
            // Add NetworkObject component
            NetworkObject networkObject = loadingScreen.AddComponent<NetworkObject>();
            
            // Add LoadingScreenManager component
            LoadingScreenManager loadingScreenManager = loadingScreen.AddComponent<LoadingScreenManager>();
            
            // Create LoadingScreenPanel
            GameObject loadingPanel = new GameObject("LoadingScreenPanel");
            loadingPanel.transform.SetParent(loadingScreen.transform, false);
            
            // Configure panel RectTransform to fill screen
            RectTransform panelRect = loadingPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            
            // Add panel background (optional invisible image for raycast blocking)
            Image panelImage = loadingPanel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f); // Semi-transparent black
            
            // Create BackgroundImage
            GameObject backgroundImage = new GameObject("BackgroundImage");
            backgroundImage.transform.SetParent(loadingPanel.transform, false);
            
            RectTransform bgRect = backgroundImage.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            
            Image bgImage = backgroundImage.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.15f, 1f); // Dark blue-gray background
            
            // Create LoadingTipImage
            GameObject tipImage = new GameObject("LoadingTipImage");
            tipImage.transform.SetParent(loadingPanel.transform, false);
            
            RectTransform tipRect = tipImage.AddComponent<RectTransform>();
            tipRect.anchorMin = Vector2.zero;
            tipRect.anchorMax = Vector2.one;
            tipRect.offsetMin = Vector2.zero;
            tipRect.offsetMax = Vector2.zero;
            
            Image tipImageComponent = tipImage.AddComponent<Image>();
            tipImageComponent.preserveAspect = true;
            tipImageComponent.color = Color.white;
            
            // Create LoadingText
            GameObject loadingText = new GameObject("LoadingText");
            loadingText.transform.SetParent(loadingPanel.transform, false);
            
            RectTransform textRect = loadingText.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 0.2f);
            textRect.offsetMin = new Vector2(50, 50);
            textRect.offsetMax = new Vector2(-50, -20);
            
            TextMeshProUGUI textComponent = loadingText.AddComponent<TextMeshProUGUI>();
            textComponent.text = "Preparing for battle...";
            textComponent.fontSize = 36;
            textComponent.color = Color.white;
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.fontStyle = FontStyles.Bold;
            
            // Try to assign a default font
            TMP_FontAsset defaultFont = Resources.GetBuiltinResource<TMP_FontAsset>("LegacyRuntime.fontsettings");
            if (defaultFont != null)
            {
                textComponent.font = defaultFont;
            }
            
            // Configure LoadingScreenManager references
            loadingScreenManager.GetType().GetField("loadingCanvas", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(loadingScreenManager, canvas);
                
            loadingScreenManager.GetType().GetField("loadingScreenPanel", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(loadingScreenManager, loadingPanel);
                
            loadingScreenManager.GetType().GetField("loadingTipImage", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(loadingScreenManager, tipImageComponent);
                
            loadingScreenManager.GetType().GetField("loadingText", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(loadingScreenManager, textComponent);
            
            // Initially disable the panel
            loadingPanel.SetActive(false);
            
            // Ensure the save directory exists
            if (!System.IO.Directory.Exists(prefabSavePath))
            {
                System.IO.Directory.CreateDirectory(prefabSavePath);
            }
            
            // Create prefab
            string fullPath = prefabSavePath + prefabName + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(loadingScreen, fullPath);
            
            // Clean up the scene instance
            DestroyImmediate(loadingScreen);
            
            // Select the created prefab
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            
            Debug.Log($"LoadingScreen prefab created successfully at: {fullPath}");
            EditorUtility.DisplayDialog("Success", 
                $"Loading Screen prefab created successfully!\n\nLocation: {fullPath}\n\n" +
                "Next steps:\n" +
                "1. Add your loading tip sprites to the 'Loading Tip Sprites' array\n" +
                "2. Customize loading messages if desired\n" +
                "3. Assign CombatSetup and GamePhaseManager references\n" +
                "4. Place the prefab in your scene", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to create LoadingScreen prefab: {e.Message}");
            EditorUtility.DisplayDialog("Error", 
                $"Failed to create LoadingScreen prefab:\n{e.Message}", "OK");
        }
    }
} 