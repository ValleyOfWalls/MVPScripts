using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor script to automatically generate the health and energy bar prefab structure
/// with all components properly configured.
/// </summary>
public class HealthEnergyBarPrefabGenerator : EditorWindow
{
    [Header("Prefab Configuration")]
    public string prefabName = "PlayerStatsPanel";
    public Vector2 panelSize = new Vector2(400, 150);
    public Vector2 barSize = new Vector2(200, 20);
    public float barSpacing = 10f;
    public bool includeBackgroundPanel = true;
    
    [Header("Sprites (Optional)")]
    public Sprite backgroundBarSprite;
    public Sprite healthBarSprite;
    public Sprite energyBarSprite;
    public Sprite panelBackgroundSprite;
    
    [Header("Colors")]
    public Color healthBarColor = Color.red;
    public Color energyBarColor = Color.blue;
    public Color backgroundBarColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
    public Color panelBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);
    
    [MenuItem("Tools/Health Energy Bar Generator")]
    public static void ShowWindow()
    {
        GetWindow<HealthEnergyBarPrefabGenerator>("Health Energy Bar Generator");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Health & Energy Bar Prefab Generator", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
        prefabName = EditorGUILayout.TextField("Prefab Name", prefabName);
        panelSize = EditorGUILayout.Vector2Field("Panel Size", panelSize);
        barSize = EditorGUILayout.Vector2Field("Bar Size", barSize);
        barSpacing = EditorGUILayout.FloatField("Bar Spacing", barSpacing);
        includeBackgroundPanel = EditorGUILayout.Toggle("Include Background Panel", includeBackgroundPanel);
        
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Sprites (Optional)", EditorStyles.boldLabel);
        backgroundBarSprite = (Sprite)EditorGUILayout.ObjectField("Background Bar Sprite", backgroundBarSprite, typeof(Sprite), false);
        healthBarSprite = (Sprite)EditorGUILayout.ObjectField("Health Bar Sprite", healthBarSprite, typeof(Sprite), false);
        energyBarSprite = (Sprite)EditorGUILayout.ObjectField("Energy Bar Sprite", energyBarSprite, typeof(Sprite), false);
        panelBackgroundSprite = (Sprite)EditorGUILayout.ObjectField("Panel Background Sprite", panelBackgroundSprite, typeof(Sprite), false);
        
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);
        healthBarColor = EditorGUILayout.ColorField("Health Bar Color", healthBarColor);
        energyBarColor = EditorGUILayout.ColorField("Energy Bar Color", energyBarColor);
        backgroundBarColor = EditorGUILayout.ColorField("Background Bar Color", backgroundBarColor);
        panelBackgroundColor = EditorGUILayout.ColorField("Panel Background Color", panelBackgroundColor);
        
        GUILayout.Space(20);
        
        if (GUILayout.Button("Generate Prefab Structure", GUILayout.Height(30)))
        {
            GeneratePrefabStructure();
        }
        
        GUILayout.Space(10);
        
        EditorGUILayout.HelpBox(
            "This will create a complete health and energy bar structure in the scene. " +
            "You can then create a prefab from it and use it in your UI.",
            MessageType.Info);
    }
    
    private void GeneratePrefabStructure()
    {
        // Create main panel
        GameObject mainPanel = CreateMainPanel();
        
        // Create health bar structure
        CreateHealthBarStructure(mainPanel);
        
        // Create energy bar structure
        CreateEnergyBarStructure(mainPanel);
        
        // Create text elements
        CreateTextElements(mainPanel);
        
        // Add the controller components
        AddControllerComponents(mainPanel);
        
        // Select the created object
        Selection.activeGameObject = mainPanel;
        
        Debug.Log($"Health and Energy Bar prefab structure '{prefabName}' created successfully!");
        Debug.Log("You can now create a prefab from this GameObject and use it in your UI.");
    }
    
    private GameObject CreateMainPanel()
    {
        GameObject panel = new GameObject(prefabName);
        
        // Add Canvas component if we're not in a Canvas
        Canvas parentCanvas = FindObjectOfType<Canvas>();
        if (parentCanvas != null)
        {
            panel.transform.SetParent(parentCanvas.transform, false);
        }
        else
        {
            // Create a Canvas if none exists
            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            panel.transform.SetParent(canvasObj.transform, false);
        }
        
        // Add RectTransform and configure
        RectTransform rectTransform = panel.AddComponent<RectTransform>();
        rectTransform.sizeDelta = panelSize;
        
        // Add background panel if requested
        if (includeBackgroundPanel)
        {
            Image panelImage = panel.AddComponent<Image>();
            panelImage.sprite = panelBackgroundSprite;
            panelImage.color = panelBackgroundColor;
            panelImage.type = panelBackgroundSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        }
        
        return panel;
    }
    
    private void CreateHealthBarStructure(GameObject parent)
    {
        // Health Bar Container
        GameObject healthContainer = new GameObject("HealthBarContainer");
        healthContainer.transform.SetParent(parent.transform, false);
        RectTransform healthContainerRect = healthContainer.AddComponent<RectTransform>();
        
        // Position at top of panel
        float yPos = (panelSize.y / 2) - (barSize.y / 2) - barSpacing;
        healthContainerRect.anchoredPosition = new Vector2(0, yPos);
        healthContainerRect.sizeDelta = barSize;
        
        // Health Bar Background
        GameObject healthBackground = new GameObject("HealthBarBackground");
        healthBackground.transform.SetParent(healthContainer.transform, false);
        RectTransform healthBgRect = healthBackground.AddComponent<RectTransform>();
        healthBgRect.anchorMin = Vector2.zero;
        healthBgRect.anchorMax = Vector2.one;
        healthBgRect.sizeDelta = Vector2.zero;
        healthBgRect.anchoredPosition = Vector2.zero;
        
        Image healthBgImage = healthBackground.AddComponent<Image>();
        healthBgImage.sprite = backgroundBarSprite;
        healthBgImage.color = backgroundBarColor;
        healthBgImage.type = backgroundBarSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        
        // Health Bar Foreground (Mask)
        GameObject healthForeground = new GameObject("HealthBarForeground");
        healthForeground.transform.SetParent(healthContainer.transform, false);
        RectTransform healthFgRect = healthForeground.AddComponent<RectTransform>();
        healthFgRect.anchorMin = Vector2.zero;
        healthFgRect.anchorMax = Vector2.one;
        healthFgRect.sizeDelta = Vector2.zero;
        healthFgRect.anchoredPosition = Vector2.zero;
        
        Image healthFgImage = healthForeground.AddComponent<Image>();
        healthFgImage.color = new Color(1, 1, 1, 0); // Transparent
        
        Mask healthMask = healthForeground.AddComponent<Mask>();
        healthMask.showMaskGraphic = false;
        
        // Health Bar Fill
        GameObject healthFill = new GameObject("HealthBarFill");
        healthFill.transform.SetParent(healthForeground.transform, false);
        RectTransform healthFillRect = healthFill.AddComponent<RectTransform>();
        
        // Critical: Set up anchors and pivot for left-to-right shrinking
        healthFillRect.anchorMin = new Vector2(0, 0);
        healthFillRect.anchorMax = new Vector2(0, 1);
        healthFillRect.pivot = new Vector2(0, 0.5f);
        healthFillRect.anchoredPosition = Vector2.zero;
        healthFillRect.sizeDelta = new Vector2(barSize.x, 0);
        
        Image healthFillImage = healthFill.AddComponent<Image>();
        healthFillImage.sprite = healthBarSprite;
        healthFillImage.color = healthBarColor;
        healthFillImage.type = healthBarSprite != null ? Image.Type.Sliced : Image.Type.Simple;
    }
    
    private void CreateEnergyBarStructure(GameObject parent)
    {
        // Energy Bar Container
        GameObject energyContainer = new GameObject("EnergyBarContainer");
        energyContainer.transform.SetParent(parent.transform, false);
        RectTransform energyContainerRect = energyContainer.AddComponent<RectTransform>();
        
        // Position below health bar
        float yPos = (panelSize.y / 2) - (barSize.y / 2) - barSpacing - barSize.y - barSpacing;
        energyContainerRect.anchoredPosition = new Vector2(0, yPos);
        energyContainerRect.sizeDelta = barSize;
        
        // Energy Bar Background
        GameObject energyBackground = new GameObject("EnergyBarBackground");
        energyBackground.transform.SetParent(energyContainer.transform, false);
        RectTransform energyBgRect = energyBackground.AddComponent<RectTransform>();
        energyBgRect.anchorMin = Vector2.zero;
        energyBgRect.anchorMax = Vector2.one;
        energyBgRect.sizeDelta = Vector2.zero;
        energyBgRect.anchoredPosition = Vector2.zero;
        
        Image energyBgImage = energyBackground.AddComponent<Image>();
        energyBgImage.sprite = backgroundBarSprite;
        energyBgImage.color = backgroundBarColor;
        energyBgImage.type = backgroundBarSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        
        // Energy Bar Foreground (Mask)
        GameObject energyForeground = new GameObject("EnergyBarForeground");
        energyForeground.transform.SetParent(energyContainer.transform, false);
        RectTransform energyFgRect = energyForeground.AddComponent<RectTransform>();
        energyFgRect.anchorMin = Vector2.zero;
        energyFgRect.anchorMax = Vector2.one;
        energyFgRect.sizeDelta = Vector2.zero;
        energyFgRect.anchoredPosition = Vector2.zero;
        
        Image energyFgImage = energyForeground.AddComponent<Image>();
        energyFgImage.color = new Color(1, 1, 1, 0); // Transparent
        
        Mask energyMask = energyForeground.AddComponent<Mask>();
        energyMask.showMaskGraphic = false;
        
        // Energy Bar Fill
        GameObject energyFill = new GameObject("EnergyBarFill");
        energyFill.transform.SetParent(energyForeground.transform, false);
        RectTransform energyFillRect = energyFill.AddComponent<RectTransform>();
        
        // Critical: Set up anchors and pivot for left-to-right shrinking
        energyFillRect.anchorMin = new Vector2(0, 0);
        energyFillRect.anchorMax = new Vector2(0, 1);
        energyFillRect.pivot = new Vector2(0, 0.5f);
        energyFillRect.anchoredPosition = Vector2.zero;
        energyFillRect.sizeDelta = new Vector2(barSize.x, 0);
        
        Image energyFillImage = energyFill.AddComponent<Image>();
        energyFillImage.sprite = energyBarSprite;
        energyFillImage.color = energyBarColor;
        energyFillImage.type = energyBarSprite != null ? Image.Type.Sliced : Image.Type.Simple;
    }
    
    private void CreateTextElements(GameObject parent)
    {
        // Health Text
        GameObject healthTextObj = new GameObject("HealthText");
        healthTextObj.transform.SetParent(parent.transform, false);
        RectTransform healthTextRect = healthTextObj.AddComponent<RectTransform>();
        
        float healthTextY = (panelSize.y / 2) - (barSize.y / 2) - barSpacing;
        healthTextRect.anchoredPosition = new Vector2(barSize.x / 2 + 20, healthTextY);
        healthTextRect.sizeDelta = new Vector2(100, 30);
        
        TextMeshProUGUI healthText = healthTextObj.AddComponent<TextMeshProUGUI>();
        healthText.text = "100/100";
        healthText.fontSize = 14;
        healthText.color = Color.white;
        healthText.alignment = TextAlignmentOptions.MidlineLeft;
        
        // Energy Text
        GameObject energyTextObj = new GameObject("EnergyText");
        energyTextObj.transform.SetParent(parent.transform, false);
        RectTransform energyTextRect = energyTextObj.AddComponent<RectTransform>();
        
        float energyTextY = (panelSize.y / 2) - (barSize.y / 2) - barSpacing - barSize.y - barSpacing;
        energyTextRect.anchoredPosition = new Vector2(barSize.x / 2 + 20, energyTextY);
        energyTextRect.sizeDelta = new Vector2(100, 30);
        
        TextMeshProUGUI energyText = energyTextObj.AddComponent<TextMeshProUGUI>();
        energyText.text = "100/100";
        energyText.fontSize = 14;
        energyText.color = Color.white;
        energyText.alignment = TextAlignmentOptions.MidlineLeft;
        
        // Health Label
        GameObject healthLabelObj = new GameObject("HealthLabel");
        healthLabelObj.transform.SetParent(parent.transform, false);
        RectTransform healthLabelRect = healthLabelObj.AddComponent<RectTransform>();
        healthLabelRect.anchoredPosition = new Vector2(-barSize.x / 2 - 50, healthTextY);
        healthLabelRect.sizeDelta = new Vector2(80, 30);
        
        TextMeshProUGUI healthLabel = healthLabelObj.AddComponent<TextMeshProUGUI>();
        healthLabel.text = "Health:";
        healthLabel.fontSize = 14;
        healthLabel.color = Color.white;
        healthLabel.alignment = TextAlignmentOptions.MidlineRight;
        
        // Energy Label
        GameObject energyLabelObj = new GameObject("EnergyLabel");
        energyLabelObj.transform.SetParent(parent.transform, false);
        RectTransform energyLabelRect = energyLabelObj.AddComponent<RectTransform>();
        energyLabelRect.anchoredPosition = new Vector2(-barSize.x / 2 - 50, energyTextY);
        energyLabelRect.sizeDelta = new Vector2(80, 30);
        
        TextMeshProUGUI energyLabel = energyLabelObj.AddComponent<TextMeshProUGUI>();
        energyLabel.text = "Energy:";
        energyLabel.fontSize = 14;
        energyLabel.color = Color.white;
        energyLabel.alignment = TextAlignmentOptions.MidlineRight;
    }
    
    private void AddControllerComponents(GameObject parent)
    {
        // Add HealthEnergyBarController
        HealthEnergyBarController barController = parent.AddComponent<HealthEnergyBarController>();
        
        // Auto-assign references using the structure we just created
        Transform healthFillTransform = parent.transform.Find("HealthBarContainer/HealthBarForeground/HealthBarFill");
        Transform energyFillTransform = parent.transform.Find("EnergyBarContainer/EnergyBarForeground/EnergyBarFill");
        Transform healthTextTransform = parent.transform.Find("HealthText");
        Transform energyTextTransform = parent.transform.Find("EnergyText");
        
        // Use reflection to set private serialized fields
        var barControllerType = typeof(HealthEnergyBarController);
        
        // Set health bar references
        if (healthFillTransform != null)
        {
            var healthBarFillField = barControllerType.GetField("healthBarFill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var healthBarFillImageField = barControllerType.GetField("healthBarFillImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var healthBarWidthField = barControllerType.GetField("healthBarWidth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            healthBarFillField?.SetValue(barController, healthFillTransform.GetComponent<RectTransform>());
            healthBarFillImageField?.SetValue(barController, healthFillTransform.GetComponent<Image>());
            healthBarWidthField?.SetValue(barController, barSize.x);
        }
        
        // Set energy bar references
        if (energyFillTransform != null)
        {
            var energyBarFillField = barControllerType.GetField("energyBarFill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var energyBarFillImageField = barControllerType.GetField("energyBarFillImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var energyBarWidthField = barControllerType.GetField("energyBarWidth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            energyBarFillField?.SetValue(barController, energyFillTransform.GetComponent<RectTransform>());
            energyBarFillImageField?.SetValue(barController, energyFillTransform.GetComponent<Image>());
            energyBarWidthField?.SetValue(barController, barSize.x);
        }
        
        // Set text references
        if (healthTextTransform != null)
        {
            var healthTextField = barControllerType.GetField("healthText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            healthTextField?.SetValue(barController, healthTextTransform.GetComponent<TextMeshProUGUI>());
        }
        
        if (energyTextTransform != null)
        {
            var energyTextField = barControllerType.GetField("energyText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            energyTextField?.SetValue(barController, energyTextTransform.GetComponent<TextMeshProUGUI>());
        }
        
        // Set colors
        var healthBarColorField = barControllerType.GetField("healthBarColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var energyBarColorField = barControllerType.GetField("energyBarColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        healthBarColorField?.SetValue(barController, healthBarColor);
        energyBarColorField?.SetValue(barController, energyBarColor);
        
        // Also add EntityStatsUIController for reference
        EntityStatsUIController statsController = parent.AddComponent<EntityStatsUIController>();
        
        // Set the bar controller reference in EntityStatsUIController
        var statsControllerType = typeof(EntityStatsUIController);
        var barControllerField = statsControllerType.GetField("barController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        barControllerField?.SetValue(statsController, barController);
        
        Debug.Log("Controller components added and configured!");
    }
} 