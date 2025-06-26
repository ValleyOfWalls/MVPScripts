#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using CardUpgrade;

/// <summary>
/// One-time editor script to generate the CardUpgradeDisplay prefab
/// </summary>
public class CardUpgradePrefabGenerator : EditorWindow
{
    [MenuItem("Tools/Card Upgrade/Generate Upgrade Display Prefab")]
    public static void ShowWindow()
    {
        GetWindow<CardUpgradePrefabGenerator>("Card Upgrade Prefab Generator");
    }
    
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Card Upgrade Display Prefab Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.LabelField("This tool will create a CardUpgradeDisplay.prefab in the CardObject/Upgrade/ folder.");
        EditorGUILayout.LabelField("The prefab will include all necessary UI components for the upgrade animation.");
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Generate CardUpgradeDisplay Prefab", GUILayout.Height(40)))
        {
            GenerateCardUpgradeDisplayPrefab();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Note: This operation will overwrite any existing prefab with the same name.", EditorStyles.helpBox);
    }
    
    private void GenerateCardUpgradeDisplayPrefab()
    {
        try
        {
            // Create the root GameObject
            GameObject upgradeDisplayRoot = new GameObject("CardUpgradeDisplay");
            
            // Add Canvas component (for screen overlay)
            Canvas canvas = upgradeDisplayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // High sorting order to appear on top
            
            // Add CanvasScaler
            CanvasScaler canvasScaler = upgradeDisplayRoot.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            canvasScaler.matchWidthOrHeight = 0.5f;
            
            // Add GraphicRaycaster
            upgradeDisplayRoot.AddComponent<GraphicRaycaster>();
            
            // Add CanvasGroup for fading
            CanvasGroup canvasGroup = upgradeDisplayRoot.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            
            // Create card display container first
            GameObject cardContainer = CreateCardContainer(upgradeDisplayRoot.transform);
            
            // Create all UI elements and store references
            var uiElements = new Dictionary<string, UnityEngine.Object>();
            CreateCardFrame(cardContainer.transform, uiElements);
            CreateCardArtwork(cardContainer.transform, uiElements);
            CreateCardText(cardContainer.transform, uiElements);
            CreateStatsDisplay(cardContainer.transform, uiElements);
            CreateEffectsDisplay(cardContainer.transform, uiElements);
            CreateTransitionOverlay(cardContainer.transform, uiElements);
            
            // Add CardUpgradeUIController and assign all references at once
            CardUpgradeUIController uiController = upgradeDisplayRoot.AddComponent<CardUpgradeUIController>();
            AssignUIReferences(uiController, uiElements, cardContainer);
            
            // Save as prefab
            string prefabPath = "Assets/MVPScripts/CardObject/Upgrade/CardUpgradeDisplay.prefab";
            
            // Ensure directory exists
            string directory = System.IO.Path.GetDirectoryName(prefabPath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }
            
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(upgradeDisplayRoot, prefabPath);
            
            // Clean up the scene GameObject
            DestroyImmediate(upgradeDisplayRoot);
            
            // Select the created prefab
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            
            Debug.Log($"CardUpgradeDisplay prefab created successfully at: {prefabPath}");
            EditorUtility.DisplayDialog("Success", $"CardUpgradeDisplay prefab created at:\n{prefabPath}", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to create CardUpgradeDisplay prefab: {e.Message}");
            EditorUtility.DisplayDialog("Error", $"Failed to create prefab:\n{e.Message}", "OK");
        }
    }
    
    private GameObject CreateCardContainer(Transform parent)
    {
        GameObject container = new GameObject("CardContainer");
        container.transform.SetParent(parent, false);
        
        RectTransform rectTransform = container.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(400, 600); // Card size
        
        return container;
    }
    
    private void CreateCardFrame(Transform parent, Dictionary<string, UnityEngine.Object> uiElements)
    {
        GameObject frame = new GameObject("CardFrame");
        frame.transform.SetParent(parent, false);
        
        RectTransform rectTransform = frame.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        Image frameImage = frame.AddComponent<Image>();
        frameImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f); // Dark frame
        
        uiElements["cardFrame"] = frameImage;
    }
    
    private void CreateCardArtwork(Transform parent, Dictionary<string, UnityEngine.Object> uiElements)
    {
        GameObject artwork = new GameObject("CardArtwork");
        artwork.transform.SetParent(parent, false);
        
        RectTransform rectTransform = artwork.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.1f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.9f, 0.9f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        Image artworkImage = artwork.AddComponent<Image>();
        artworkImage.color = Color.white;
        artworkImage.preserveAspect = true;
        
        uiElements["cardArtwork"] = artworkImage;
    }
    
    private void CreateCardText(Transform parent, Dictionary<string, UnityEngine.Object> uiElements)
    {
        // Card Name
        GameObject nameObj = new GameObject("CardName");
        nameObj.transform.SetParent(parent, false);
        
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.05f, 0.85f);
        nameRect.anchorMax = new Vector2(0.95f, 0.95f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;
        
        TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = "Card Name";
        nameText.fontSize = 24;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.fontStyle = FontStyles.Bold;
        
        // Card Cost
        GameObject costObj = new GameObject("CardCost");
        costObj.transform.SetParent(parent, false);
        
        RectTransform costRect = costObj.AddComponent<RectTransform>();
        costRect.anchorMin = new Vector2(0.8f, 0.9f);
        costRect.anchorMax = new Vector2(0.95f, 1.0f);
        costRect.offsetMin = Vector2.zero;
        costRect.offsetMax = Vector2.zero;
        
        TextMeshProUGUI costText = costObj.AddComponent<TextMeshProUGUI>();
        costText.text = "0";
        costText.fontSize = 20;
        costText.color = Color.yellow;
        costText.alignment = TextAlignmentOptions.Center;
        costText.fontStyle = FontStyles.Bold;
        
        // Card Description
        GameObject descObj = new GameObject("CardDescription");
        descObj.transform.SetParent(parent, false);
        
        RectTransform descRect = descObj.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0.05f, 0.05f);
        descRect.anchorMax = new Vector2(0.95f, 0.3f);
        descRect.offsetMin = Vector2.zero;
        descRect.offsetMax = Vector2.zero;
        
        TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
        descText.text = "Card description will appear here.";
        descText.fontSize = 16;
        descText.color = Color.white;
        descText.alignment = TextAlignmentOptions.TopLeft;
        descText.enableWordWrapping = true;
        
        uiElements["cardName"] = nameText;
        uiElements["cardCost"] = costText;
        uiElements["cardDescription"] = descText;
    }
    
    private void CreateStatsDisplay(Transform parent, Dictionary<string, UnityEngine.Object> uiElements)
    {
        GameObject statsContainer = new GameObject("StatsContainer");
        statsContainer.transform.SetParent(parent, false);
        
        RectTransform statsRect = statsContainer.AddComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0.05f, 0.3f);
        statsRect.anchorMax = new Vector2(0.5f, 0.5f);
        statsRect.offsetMin = Vector2.zero;
        statsRect.offsetMax = Vector2.zero;
        
        // Create individual stat texts
        CreateStatText("DamageText", statsContainer.transform, "damageText", uiElements, new Vector2(0, 0.75f), new Vector2(1, 1f));
        CreateStatText("ShieldText", statsContainer.transform, "shieldText", uiElements, new Vector2(0, 0.5f), new Vector2(1, 0.75f));
        CreateStatText("EnergyText", statsContainer.transform, "energyText", uiElements, new Vector2(0, 0.25f), new Vector2(1, 0.5f));
        CreateStatText("OtherStatsText", statsContainer.transform, "otherStatsText", uiElements, new Vector2(0, 0f), new Vector2(1, 0.25f));
        
        uiElements["statsContainer"] = statsContainer.transform;
    }
    
    private void CreateStatText(string name, Transform parent, string propertyName, Dictionary<string, UnityEngine.Object> uiElements, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject statObj = new GameObject(name);
        statObj.transform.SetParent(parent, false);
        
        RectTransform statRect = statObj.AddComponent<RectTransform>();
        statRect.anchorMin = anchorMin;
        statRect.anchorMax = anchorMax;
        statRect.offsetMin = Vector2.zero;
        statRect.offsetMax = Vector2.zero;
        
        TextMeshProUGUI statText = statObj.AddComponent<TextMeshProUGUI>();
        statText.text = "";
        statText.fontSize = 14;
        statText.color = Color.cyan;
        statText.alignment = TextAlignmentOptions.TopLeft;
        
        // Set active to false initially
        statObj.SetActive(false);
        
        uiElements[propertyName] = statText;
    }
    
    private void CreateEffectsDisplay(Transform parent, Dictionary<string, UnityEngine.Object> uiElements)
    {
        GameObject effectsContainer = new GameObject("EffectsContainer");
        effectsContainer.transform.SetParent(parent, false);
        
        RectTransform effectsRect = effectsContainer.AddComponent<RectTransform>();
        effectsRect.anchorMin = new Vector2(0.5f, 0.3f);
        effectsRect.anchorMax = new Vector2(0.95f, 0.5f);
        effectsRect.offsetMin = Vector2.zero;
        effectsRect.offsetMax = Vector2.zero;
        
        GameObject effectsTextObj = new GameObject("EffectsListText");
        effectsTextObj.transform.SetParent(effectsContainer.transform, false);
        
        RectTransform effectsTextRect = effectsTextObj.AddComponent<RectTransform>();
        effectsTextRect.anchorMin = Vector2.zero;
        effectsTextRect.anchorMax = Vector2.one;
        effectsTextRect.offsetMin = Vector2.zero;
        effectsTextRect.offsetMax = Vector2.zero;
        
        TextMeshProUGUI effectsText = effectsTextObj.AddComponent<TextMeshProUGUI>();
        effectsText.text = "Effects will appear here";
        effectsText.fontSize = 12;
        effectsText.color = Color.green;
        effectsText.alignment = TextAlignmentOptions.TopLeft;
        effectsText.enableWordWrapping = true;
        
        uiElements["effectsContainer"] = effectsContainer.transform;
        uiElements["effectsListText"] = effectsText;
    }
    
    private void CreateTransitionOverlay(Transform parent, Dictionary<string, UnityEngine.Object> uiElements)
    {
        GameObject overlay = new GameObject("TransitionOverlay");
        overlay.transform.SetParent(parent, false);
        
        RectTransform overlayRect = overlay.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        
        Image overlayImage = overlay.AddComponent<Image>();
        overlayImage.color = new Color(1f, 1f, 1f, 0f); // White, fully transparent initially
        
        uiElements["transitionOverlay"] = overlayImage;
    }
    
    private void AssignUIReferences(CardUpgradeUIController uiController, Dictionary<string, UnityEngine.Object> uiElements, GameObject cardContainer)
    {
        SerializedObject serializedController = new SerializedObject(uiController);
        
        // Assign main container reference
        serializedController.FindProperty("cardDisplayArea").objectReferenceValue = cardContainer.transform;
        
        // Assign all UI element references
        foreach (var kvp in uiElements)
        {
            SerializedProperty property = serializedController.FindProperty(kvp.Key);
            if (property != null)
            {
                property.objectReferenceValue = kvp.Value;
            }
            else
            {
                Debug.LogWarning($"Property '{kvp.Key}' not found in CardUpgradeUIController");
            }
        }
        
        // Apply all changes at once
        serializedController.ApplyModifiedProperties();
        
        Debug.Log("All UI references assigned to CardUpgradeUIController");
    }
}
#endif 