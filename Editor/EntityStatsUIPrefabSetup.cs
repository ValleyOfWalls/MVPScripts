using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using FishNet.Object;

/// <summary>
/// Unity Editor script to create EntityStatsUI prefabs
/// Run this once to set up the new prefab structure
/// </summary>
public class EntityStatsUIPrefabSetup : EditorWindow
{
    [MenuItem("Tools/Setup Entity Stats UI Prefab")]
    public static void ShowWindow()
    {
        EntityStatsUIPrefabSetup window = GetWindow<EntityStatsUIPrefabSetup>();
        window.titleContent = new GUIContent("Entity Stats UI Setup");
        window.Show();
    }
    
    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        
        EditorGUILayout.LabelField("Entity Stats UI Prefab Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.LabelField("This will create EntityStatsUI prefabs for both Player and Pet entities.");
        EditorGUILayout.LabelField("The prefabs will be saved in Assets/Prefabs/UI/");
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Create Player Stats UI Prefab"))
        {
            try
            {
                CreateStatsUIPrefab(EntityType.PlayerStatsUI, "PlayerStatsUI");
                EditorUtility.DisplayDialog("Success", "PlayerStatsUI prefab created successfully!", "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to create PlayerStatsUI prefab: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to create PlayerStatsUI prefab: {e.Message}", "OK");
            }
        }
        
        if (GUILayout.Button("Create Pet Stats UI Prefab"))
        {
            try
            {
                CreateStatsUIPrefab(EntityType.PetStatsUI, "PetStatsUI");
                EditorUtility.DisplayDialog("Success", "PetStatsUI prefab created successfully!", "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to create PetStatsUI prefab: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to create PetStatsUI prefab: {e.Message}", "OK");
            }
        }
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Create Both Prefabs"))
        {
            try
            {
                CreateStatsUIPrefab(EntityType.PlayerStatsUI, "PlayerStatsUI");
                CreateStatsUIPrefab(EntityType.PetStatsUI, "PetStatsUI");
                EditorUtility.DisplayDialog("Success", "Both prefabs created successfully!", "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to create prefabs: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to create prefabs: {e.Message}", "OK");
            }
        }
        
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Instructions:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("1. Run this script to create the prefabs");
        EditorGUILayout.LabelField("2. Customize the UI layout in the prefabs as needed");
        EditorGUILayout.LabelField("3. Set up positioning transforms in CombatCanvasManager");
        EditorGUILayout.LabelField("4. The prefabs will be spawned automatically with entities");
        
        EditorGUILayout.EndVertical();
    }
    
    private void CreateStatsUIPrefab(EntityType entityType, string prefabName)
    {
        // Create the root GameObject
        GameObject statsUIRoot = new GameObject(prefabName);
        
        // Add Canvas components for UI rendering - using ScreenSpace-Overlay like hands
        Canvas canvas = statsUIRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Note: Overlay canvases don't need worldCamera assignment
        
        CanvasScaler canvasScaler = statsUIRoot.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);
        
        GraphicRaycaster graphicRaycaster = statsUIRoot.AddComponent<GraphicRaycaster>();
        
        // Add RectTransform and set initial size
        RectTransform rootRect = statsUIRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(300, 200);
        
        // Add NetworkObject component
        NetworkObject networkObject = statsUIRoot.AddComponent<NetworkObject>();
        
        // Add NetworkEntity component
        NetworkEntity networkEntity = statsUIRoot.AddComponent<NetworkEntity>();
        // Set the entity type via reflection since it's private
        var entityTypeField = typeof(NetworkEntity).GetField("entityType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (entityTypeField != null)
        {
            entityTypeField.SetValue(networkEntity, entityType);
        }
        
        // Add RelationshipManager
        RelationshipManager relationshipManager = statsUIRoot.AddComponent<RelationshipManager>();
        
        // Add EntityStatsUIController
        EntityStatsUIController statsUIController = statsUIRoot.AddComponent<EntityStatsUIController>();
        
        // Note: NetworkEntityUI is not needed for stats UI - EntityStatsUIController handles all UI needs
        
        // Create main panel
        GameObject mainPanel = CreateUIPanel("MainPanel", statsUIRoot.transform);
        RectTransform mainPanelRect = mainPanel.GetComponent<RectTransform>();
        mainPanelRect.anchorMin = Vector2.zero;
        mainPanelRect.anchorMax = Vector2.one;
        mainPanelRect.offsetMin = Vector2.zero;
        mainPanelRect.offsetMax = Vector2.zero;
        
        // Add background image to main panel
        Image mainPanelImage = mainPanel.GetComponent<Image>();
        mainPanelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f); // Dark semi-transparent
        
        // Create header with entity name
        GameObject nameContainer = CreateUIPanel("NameContainer", mainPanel.transform);
        RectTransform nameRect = nameContainer.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.8f);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = new Vector2(10, 0);
        nameRect.offsetMax = new Vector2(-10, -5);
        
        TextMeshProUGUI nameText = CreateTextElement("NameText", nameContainer.transform, "Entity Name", 16, TextAlignmentOptions.Center);
        nameText.color = Color.white;
        nameText.fontStyle = FontStyles.Bold;
        
        // Create stats container
        GameObject statsContainer = CreateUIPanel("StatsContainer", mainPanel.transform);
        RectTransform statsRect = statsContainer.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0, 0.4f);
        statsRect.anchorMax = new Vector2(1, 0.8f);
        statsRect.offsetMin = new Vector2(10, 0);
        statsRect.offsetMax = new Vector2(-10, 0);
        
        // Create health section
        GameObject healthContainer = CreateUIPanel("HealthContainer", statsContainer.transform);
        RectTransform healthRect = healthContainer.GetComponent<RectTransform>();
        healthRect.anchorMin = new Vector2(0, 0.6f);
        healthRect.anchorMax = new Vector2(1, 1);
        healthRect.offsetMin = new Vector2(5, 0);
        healthRect.offsetMax = new Vector2(-5, 0);
        
        TextMeshProUGUI healthText = CreateTextElement("HealthText", healthContainer.transform, "100/100", 12, TextAlignmentOptions.MidlineLeft);
        healthText.color = Color.green;
        
        GameObject healthBarBG = CreateUIPanel("HealthBarBackground", healthContainer.transform);
        RectTransform healthBarBGRect = healthBarBG.GetComponent<RectTransform>();
        healthBarBGRect.anchorMin = new Vector2(0, 0);
        healthBarBGRect.anchorMax = new Vector2(1, 0.4f);
        healthBarBGRect.offsetMin = new Vector2(0, 2);
        healthBarBGRect.offsetMax = new Vector2(0, -2);
        healthBarBG.GetComponent<Image>().color = Color.red;
        
        GameObject healthBarFill = CreateUIPanel("HealthBar", healthBarBG.transform);
        RectTransform healthBarRect = healthBarFill.GetComponent<RectTransform>();
        healthBarRect.anchorMin = Vector2.zero;
        healthBarRect.anchorMax = Vector2.one;
        healthBarRect.offsetMin = Vector2.zero;
        healthBarRect.offsetMax = Vector2.zero;
        Image healthBar = healthBarFill.GetComponent<Image>();
        healthBar.color = Color.green;
        healthBar.type = Image.Type.Filled;
        healthBar.fillMethod = Image.FillMethod.Horizontal;
        
        // Create energy section
        GameObject energyContainer = CreateUIPanel("EnergyContainer", statsContainer.transform);
        RectTransform energyRect = energyContainer.GetComponent<RectTransform>();
        energyRect.anchorMin = new Vector2(0, 0);
        energyRect.anchorMax = new Vector2(1, 0.4f);
        energyRect.offsetMin = new Vector2(5, 0);
        energyRect.offsetMax = new Vector2(-5, 0);
        
        TextMeshProUGUI energyText = CreateTextElement("EnergyText", energyContainer.transform, "3/3", 12, TextAlignmentOptions.MidlineLeft);
        energyText.color = Color.blue;
        
        GameObject energyBarBG = CreateUIPanel("EnergyBarBackground", energyContainer.transform);
        RectTransform energyBarBGRect = energyBarBG.GetComponent<RectTransform>();
        energyBarBGRect.anchorMin = new Vector2(0, 0);
        energyBarBGRect.anchorMax = new Vector2(1, 0.4f);
        energyBarBGRect.offsetMin = new Vector2(0, 2);
        energyBarBGRect.offsetMax = new Vector2(0, -2);
        energyBarBG.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.8f);
        
        GameObject energyBarFill = CreateUIPanel("EnergyBar", energyBarBG.transform);
        RectTransform energyBarFillRect = energyBarFill.GetComponent<RectTransform>();
        energyBarFillRect.anchorMin = Vector2.zero;
        energyBarFillRect.anchorMax = Vector2.one;
        energyBarFillRect.offsetMin = Vector2.zero;
        energyBarFillRect.offsetMax = Vector2.zero;
        Image energyBar = energyBarFill.GetComponent<Image>();
        energyBar.color = Color.blue;
        energyBar.type = Image.Type.Filled;
        energyBar.fillMethod = Image.FillMethod.Horizontal;
        
        // Create effects section
        GameObject effectsContainer = CreateUIPanel("EffectsContainer", mainPanel.transform);
        RectTransform effectsRect = effectsContainer.GetComponent<RectTransform>();
        effectsRect.anchorMin = new Vector2(0, 0.25f);
        effectsRect.anchorMax = new Vector2(1, 0.4f);
        effectsRect.offsetMin = new Vector2(10, 0);
        effectsRect.offsetMax = new Vector2(-10, 0);
        
        TextMeshProUGUI effectsText = CreateTextElement("EffectsText", effectsContainer.transform, "Effects: None", 10, TextAlignmentOptions.MidlineLeft);
        effectsText.color = Color.yellow;
        
        // Create currency section (only for players)
        GameObject currencyContainer = null;
        if (entityType == EntityType.PlayerStatsUI)
        {
            currencyContainer = CreateUIPanel("CurrencyContainer", mainPanel.transform);
            RectTransform currencyRect = currencyContainer.GetComponent<RectTransform>();
            currencyRect.anchorMin = new Vector2(0, 0.1f);
            currencyRect.anchorMax = new Vector2(1, 0.25f);
            currencyRect.offsetMin = new Vector2(10, 0);
            currencyRect.offsetMax = new Vector2(-10, 0);
            
            TextMeshProUGUI currencyText = CreateTextElement("CurrencyText", currencyContainer.transform, "20 Gold", 10, TextAlignmentOptions.MidlineLeft);
            currencyText.color = Color.yellow;
        }
        
        // Create deck info section
        GameObject deckContainer = CreateUIPanel("DeckContainer", mainPanel.transform);
        RectTransform deckRect = deckContainer.GetComponent<RectTransform>();
        deckRect.anchorMin = new Vector2(0, 0);
        deckRect.anchorMax = new Vector2(1, 0.1f);
        deckRect.offsetMin = new Vector2(10, 5);
        deckRect.offsetMax = new Vector2(-10, 0);
        
        TextMeshProUGUI deckText = CreateTextElement("DeckCountText", deckContainer.transform, "Deck: 30", 8, TextAlignmentOptions.MidlineLeft);
        deckText.color = Color.white;
        deckText.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1);
        
        TextMeshProUGUI discardText = CreateTextElement("DiscardCountText", deckContainer.transform, "Discard: 0", 8, TextAlignmentOptions.MidlineLeft);
        discardText.color = Color.gray;
        RectTransform discardRect = discardText.GetComponent<RectTransform>();
        discardRect.anchorMin = new Vector2(0.5f, 0);
        discardRect.anchorMax = new Vector2(1, 1);
        
        // Create damage preview (hidden by default)
        GameObject damagePreviewContainer = CreateUIPanel("DamagePreviewContainer", mainPanel.transform);
        RectTransform damagePreviewRect = damagePreviewContainer.GetComponent<RectTransform>();
        damagePreviewRect.anchorMin = new Vector2(0.7f, 0.7f);
        damagePreviewRect.anchorMax = new Vector2(1, 1);
        damagePreviewRect.offsetMin = Vector2.zero;
        damagePreviewRect.offsetMax = Vector2.zero;
        damagePreviewContainer.SetActive(false);
        
        TextMeshProUGUI damagePreviewText = CreateTextElement("DamagePreviewText", damagePreviewContainer.transform, "-10", 14, TextAlignmentOptions.Center);
        damagePreviewText.color = Color.red;
        damagePreviewText.fontStyle = FontStyles.Bold;
        
        // Wire up the EntityStatsUIController references
        var statsEntityField = typeof(EntityStatsUIController).GetField("statsEntity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var relationshipManagerField = typeof(EntityStatsUIController).GetField("relationshipManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var nameTextField = typeof(EntityStatsUIController).GetField("nameText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var healthTextField = typeof(EntityStatsUIController).GetField("healthText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var energyTextField = typeof(EntityStatsUIController).GetField("energyText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var healthBarField = typeof(EntityStatsUIController).GetField("healthBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var energyBarField = typeof(EntityStatsUIController).GetField("energyBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var effectsTextField = typeof(EntityStatsUIController).GetField("effectsText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var currencyTextField = typeof(EntityStatsUIController).GetField("currencyText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var currencyContainerField = typeof(EntityStatsUIController).GetField("currencyContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var deckCountTextField = typeof(EntityStatsUIController).GetField("deckCountText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var discardCountTextField = typeof(EntityStatsUIController).GetField("discardCountText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var damagePreviewTextField = typeof(EntityStatsUIController).GetField("damagePreviewText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var damagePreviewContainerField = typeof(EntityStatsUIController).GetField("damagePreviewContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (statsEntityField != null) statsEntityField.SetValue(statsUIController, networkEntity);
        if (relationshipManagerField != null) relationshipManagerField.SetValue(statsUIController, relationshipManager);
        if (nameTextField != null) nameTextField.SetValue(statsUIController, nameText);
        if (healthTextField != null) healthTextField.SetValue(statsUIController, healthText);
        if (energyTextField != null) energyTextField.SetValue(statsUIController, energyText);
        if (healthBarField != null) healthBarField.SetValue(statsUIController, healthBar);
        if (energyBarField != null) energyBarField.SetValue(statsUIController, energyBar);
        if (effectsTextField != null) effectsTextField.SetValue(statsUIController, effectsText);
        if (deckCountTextField != null) deckCountTextField.SetValue(statsUIController, deckText);
        if (discardCountTextField != null) discardCountTextField.SetValue(statsUIController, discardText);
        if (damagePreviewTextField != null) damagePreviewTextField.SetValue(statsUIController, damagePreviewText);
        if (damagePreviewContainerField != null) damagePreviewContainerField.SetValue(statsUIController, damagePreviewContainer);
        
        if (entityType == EntityType.PlayerStatsUI && currencyContainer != null)
        {
            var currencyTextComponent = currencyContainer.GetComponentInChildren<TextMeshProUGUI>();
            if (currencyTextField != null) currencyTextField.SetValue(statsUIController, currencyTextComponent);
            if (currencyContainerField != null) currencyContainerField.SetValue(statsUIController, currencyContainer);
        }
        
        // Create the prefab directory if it doesn't exist
        string folderPath = "Assets/Prefabs/UI";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            string parentFolder = "Assets/Prefabs";
            if (!AssetDatabase.IsValidFolder(parentFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            AssetDatabase.CreateFolder(parentFolder, "UI");
        }
        
        // Save as prefab
        string prefabPath = $"{folderPath}/{prefabName}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(statsUIRoot, prefabPath);
        
        // Clean up the scene object
        DestroyImmediate(statsUIRoot);
        
        // Select the created prefab
        Selection.activeObject = prefab;
        
        Debug.Log($"Created {prefabName} prefab at {prefabPath}");
        AssetDatabase.Refresh();
    }
    
    private GameObject CreateUIPanel(string name, Transform parent)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent);
        
        RectTransform rectTransform = panel.AddComponent<RectTransform>();
        Image image = panel.AddComponent<Image>();
        image.color = Color.clear; // Transparent by default
        
        return panel;
    }
    
    private TextMeshProUGUI CreateTextElement(string name, Transform parent, string text, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent);
        
        RectTransform rectTransform = textObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        TextMeshProUGUI textComponent = textObject.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = Color.white;
        
        return textComponent;
    }
} 