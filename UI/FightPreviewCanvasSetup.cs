using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Helper script to set up the Fight Preview Canvas structure programmatically.
/// This follows SOLID principles by separating UI creation from UI management.
/// Attach to: A GameObject that will create the fight preview UI structure.
/// </summary>
public class FightPreviewCanvasSetup : MonoBehaviour
{
    [Header("Prefab References")]
    [SerializeField] private Canvas canvasPrefab;
    [SerializeField] private Font defaultFont;
    
    [Header("Styling Options")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.8f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color versusTextColor = Color.yellow;
    [SerializeField] private int playerNameFontSize = 48;
    [SerializeField] private int versusTextFontSize = 36;
    [SerializeField] private int petNameFontSize = 48;
    [SerializeField] private Vector2 imageSize = new Vector2(128f, 128f);
    
    [Header("Auto Setup")]
    [SerializeField] private bool createOnAwake = false;
    [SerializeField] private bool assignToFightPreviewUIManager = true;
    
    private GameObject createdCanvas;
    private FightPreviewUIManager targetUIManager;

    #region Lifecycle

    private void Awake()
    {
        if (createOnAwake)
        {
            CreateFightPreviewCanvas();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Creates the complete fight preview canvas structure
    /// </summary>
    [ContextMenu("Create Fight Preview Canvas")]
    public GameObject CreateFightPreviewCanvas()
    {
        if (createdCanvas != null)
        {
            Debug.LogWarning("FightPreviewCanvasSetup: Canvas already created, destroying previous version");
            DestroyCreatedCanvas();
        }

        // Create main canvas
        createdCanvas = CreateMainCanvas();
        
        // Create background panel
        GameObject backgroundPanel = CreateBackgroundPanel(createdCanvas);
        
        // Create fight info panel
        GameObject fightInfoPanel = CreateFightInfoPanel(createdCanvas);
        
        // Create UI elements
        CreateUIElements(fightInfoPanel, out var uiElements);
        
        // Auto-assign to FightPreviewUIManager if requested
        if (assignToFightPreviewUIManager)
        {
            AutoAssignToUIManager(createdCanvas, backgroundPanel, uiElements);
        }
        
        Debug.Log("FightPreviewCanvasSetup: Fight preview canvas created successfully");
        return createdCanvas;
    }

    /// <summary>
    /// Destroys the created canvas
    /// </summary>
    [ContextMenu("Destroy Created Canvas")]
    public void DestroyCreatedCanvas()
    {
        if (createdCanvas != null)
        {
            if (Application.isPlaying)
            {
                Destroy(createdCanvas);
            }
            else
            {
                DestroyImmediate(createdCanvas);
            }
            createdCanvas = null;
        }
    }

    #endregion

    #region Canvas Creation

    private GameObject CreateMainCanvas()
    {
        GameObject canvasGO = new GameObject("FightPreviewCanvas");
        
        // Add Canvas component
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // High order to appear on top
        
        // Add CanvasScaler
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        
        // Add GraphicRaycaster
        canvasGO.AddComponent<GraphicRaycaster>();
        
        // Add CanvasGroup for fading
        CanvasGroup canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f; // Start invisible
        
        // Initially disable the canvas
        canvasGO.SetActive(false);
        
        return canvasGO;
    }

    private GameObject CreateBackgroundPanel(GameObject parent)
    {
        GameObject backgroundGO = new GameObject("BackgroundPanel");
        backgroundGO.transform.SetParent(parent.transform, false);
        
        // Add RectTransform and set to fill parent
        RectTransform bgRect = backgroundGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        
        // Add Image component
        Image bgImage = backgroundGO.AddComponent<Image>();
        bgImage.color = backgroundColor;
        
        return backgroundGO;
    }

    private GameObject CreateFightInfoPanel(GameObject parent)
    {
        GameObject fightInfoGO = new GameObject("FightInfoPanel");
        fightInfoGO.transform.SetParent(parent.transform, false);
        
        // Add RectTransform and center it
        RectTransform infoRect = fightInfoGO.AddComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0.5f, 0.5f);
        infoRect.anchorMax = new Vector2(0.5f, 0.5f);
        infoRect.sizeDelta = new Vector2(800f, 400f);
        infoRect.anchoredPosition = Vector2.zero;
        
        return fightInfoGO;
    }

    private void CreateUIElements(GameObject parent, out UIElements elements)
    {
        elements = new UIElements();
        
        // Create horizontal layout for main content
        GameObject mainLayoutGO = new GameObject("MainLayout");
        mainLayoutGO.transform.SetParent(parent.transform, false);
        
        RectTransform mainLayoutRect = mainLayoutGO.AddComponent<RectTransform>();
        mainLayoutRect.anchorMin = Vector2.zero;
        mainLayoutRect.anchorMax = Vector2.one;
        mainLayoutRect.sizeDelta = Vector2.zero;
        mainLayoutRect.anchoredPosition = Vector2.zero;
        
        HorizontalLayoutGroup mainLayout = mainLayoutGO.AddComponent<HorizontalLayoutGroup>();
        mainLayout.spacing = 50f;
        mainLayout.childAlignment = TextAnchor.MiddleCenter;
        mainLayout.childControlWidth = false;
        mainLayout.childControlHeight = false;
        
        // Create player section
        GameObject playerSection = CreatePlayerSection(mainLayoutGO, "Player Section");
        elements.playerImage = CreateImageElement(playerSection, "PlayerImage");
        elements.playerNameText = CreateTextElement(playerSection, "PlayerNameText", "Player Name", playerNameFontSize);
        
        // Create versus text
        elements.versusText = CreateTextElement(mainLayoutGO, "VersusText", "VS", versusTextFontSize);
        elements.versusText.color = versusTextColor;
        
        // Create opponent section
        GameObject opponentSection = CreatePlayerSection(mainLayoutGO, "Opponent Section");
        elements.opponentPetNameText = CreateTextElement(opponentSection, "OpponentPetNameText", "Opponent Pet", petNameFontSize);
        elements.opponentPetImage = CreateImageElement(opponentSection, "OpponentPetImage");
    }

    private GameObject CreatePlayerSection(GameObject parent, string name)
    {
        GameObject sectionGO = new GameObject(name);
        sectionGO.transform.SetParent(parent.transform, false);
        
        RectTransform sectionRect = sectionGO.AddComponent<RectTransform>();
        sectionRect.sizeDelta = new Vector2(200f, 300f);
        
        VerticalLayoutGroup layout = sectionGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        
        return sectionGO;
    }

    private TextMeshProUGUI CreateTextElement(GameObject parent, string name, string text, int fontSize)
    {
        GameObject textGO = new GameObject(name);
        textGO.transform.SetParent(parent.transform, false);
        
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(200f, 60f);
        
        TextMeshProUGUI textMesh = textGO.AddComponent<TextMeshProUGUI>();
        textMesh.text = text;
        textMesh.fontSize = fontSize;
        textMesh.color = textColor;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontStyle = FontStyles.Bold;
        
        return textMesh;
    }

    private Image CreateImageElement(GameObject parent, string name)
    {
        GameObject imageGO = new GameObject(name);
        imageGO.transform.SetParent(parent.transform, false);
        
        RectTransform imageRect = imageGO.AddComponent<RectTransform>();
        imageRect.sizeDelta = imageSize;
        
        Image image = imageGO.AddComponent<Image>();
        image.color = Color.white;
        image.preserveAspect = true;
        
        // Create a placeholder sprite if none exists
        if (image.sprite == null)
        {
            // Create a simple white square as placeholder
            Texture2D placeholderTexture = new Texture2D(1, 1);
            placeholderTexture.SetPixel(0, 0, Color.gray);
            placeholderTexture.Apply();
            
            image.sprite = Sprite.Create(placeholderTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        }
        
        return image;
    }

    #endregion

    #region Auto Assignment

    private void AutoAssignToUIManager(GameObject canvas, GameObject backgroundPanel, UIElements elements)
    {
        // Find FightPreviewUIManager if not already assigned
        if (targetUIManager == null)
        {
            targetUIManager = FindFirstObjectByType<FightPreviewUIManager>();
        }
        
        if (targetUIManager == null)
        {
            Debug.LogWarning("FightPreviewCanvasSetup: No FightPreviewUIManager found for auto-assignment");
            return;
        }
        
        // Use reflection to assign private fields (for auto-setup convenience)
        var type = typeof(FightPreviewUIManager);
        
        SetPrivateField(type, "fightPreviewCanvas", canvas);
        SetPrivateField(type, "backgroundPanel", backgroundPanel);
        SetPrivateField(type, "playerNameText", elements.playerNameText);
        SetPrivateField(type, "versusText", elements.versusText);
        SetPrivateField(type, "opponentPetNameText", elements.opponentPetNameText);
        SetPrivateField(type, "playerImage", elements.playerImage);
        SetPrivateField(type, "opponentPetImage", elements.opponentPetImage);
        
        Debug.Log("FightPreviewCanvasSetup: Auto-assigned UI elements to FightPreviewUIManager");
    }
    
    private void SetPrivateField(System.Type type, string fieldName, object value)
    {
        var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null && targetUIManager != null)
        {
            field.SetValue(targetUIManager, value);
        }
        else
        {
            Debug.LogWarning($"FightPreviewCanvasSetup: Could not set field {fieldName}");
        }
    }

    #endregion

    #region Helper Structures

    private struct UIElements
    {
        public TextMeshProUGUI playerNameText;
        public TextMeshProUGUI versusText;
        public TextMeshProUGUI opponentPetNameText;
        public Image playerImage;
        public Image opponentPetImage;
    }

    #endregion
} 