using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Helper script to set up the Character Selection Canvas structure programmatically.
/// This follows SOLID principles by separating UI creation from UI management.
/// Attach to: A GameObject that will create the character selection UI structure.
/// </summary>
public class CharacterSelectionCanvasSetup : MonoBehaviour
{
    [Header("Prefab References")]
    [SerializeField] private Canvas canvasPrefab;
    [SerializeField] private Font defaultFont;
    
    [Header("Styling Options")]
    [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
    [SerializeField] private Color panelColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color accentColor = new Color(0.3f, 0.6f, 1f, 1f);
    [SerializeField] private int titleFontSize = 36;
    [SerializeField] private int headerFontSize = 24;
    [SerializeField] private int normalFontSize = 16;
    [SerializeField] private Vector2 selectionItemSize = new Vector2(200f, 250f);
    
    [Header("Auto Setup")]
    [SerializeField] private bool createOnAwake = false;
    [SerializeField] private bool assignToUIManager = true;
    
    private GameObject createdCanvas;
    private CharacterSelectionUIManager targetUIManager;

    #region Lifecycle

    private void Awake()
    {
        if (createOnAwake)
        {
            CreateCharacterSelectionCanvas();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Creates the complete character selection canvas structure
    /// </summary>
    [ContextMenu("Create Character Selection Canvas")]
    public GameObject CreateCharacterSelectionCanvas()
    {
        if (createdCanvas != null)
        {
            Debug.LogWarning("CharacterSelectionCanvasSetup: Canvas already created, destroying previous version");
            DestroyCreatedCanvas();
        }

        // Create main canvas
        createdCanvas = CreateMainCanvas();
        
        // Create background panel
        GameObject backgroundPanel = CreateBackgroundPanel(createdCanvas);
        
        // Create main container
        GameObject mainContainer = CreateMainContainer(createdCanvas);
        
        // Create UI sections
        CreateUIElements(mainContainer, out var uiElements);
        
        // Auto-assign to CharacterSelectionUIManager if requested
        if (assignToUIManager)
        {
            AutoAssignToUIManager(createdCanvas, backgroundPanel, uiElements);
        }
        
        Debug.Log("CharacterSelectionCanvasSetup: Character selection canvas created successfully");
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
        GameObject canvasGO = new GameObject("CharacterSelectionCanvas");
        
        // Add Canvas component
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        
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
        canvasGroup.alpha = 1f;
        
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

    private GameObject CreateMainContainer(GameObject parent)
    {
        GameObject containerGO = new GameObject("MainContainer");
        containerGO.transform.SetParent(parent.transform, false);
        
        // Add RectTransform and set margins
        RectTransform containerRect = containerGO.AddComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.sizeDelta = Vector2.zero;
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.offsetMin = new Vector2(50, 50); // Left, Bottom margin
        containerRect.offsetMax = new Vector2(-50, -50); // Right, Top margin
        
        // Add vertical layout
        VerticalLayoutGroup layout = containerGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 20f;
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        
        return containerGO;
    }

    private void CreateUIElements(GameObject parent, out UIElements elements)
    {
        elements = new UIElements();
        
        // Create title
        elements.titleText = CreateTextElement(parent, "TitleText", "Select Your Character & Pet", titleFontSize, 60f);
        
        // Create player info section
        GameObject playerInfoSection = CreatePlayerInfoSection(parent);
        elements.playerNameInputField = CreateInputField(playerInfoSection, "PlayerNameInput", "Enter player name...", out elements.playerNameText);
        elements.readyButton = CreateButton(playerInfoSection, "ReadyButton", "Ready", accentColor);
        
        // Create main selection area
        GameObject selectionArea = CreateSelectionArea(parent);
        
        // Create character selection section
        GameObject characterSection = CreateSelectionSection(selectionArea, "Character Selection", out elements.characterScrollView, out elements.characterGridParent);
        
        // Create pet selection section  
        GameObject petSection = CreateSelectionSection(selectionArea, "Pet Selection", out elements.petScrollView, out elements.petGridParent);
        
        // Create deck preview section
        elements.deckPreviewPanel = CreateDeckPreviewSection(parent, out elements.deckPreviewScrollView, out elements.deckPreviewGridParent, out elements.deckPreviewTitle);
        
        // Create status section
        elements.statusText = CreateTextElement(parent, "StatusText", "Select a character and pet to continue...", normalFontSize, 40f);
        
        // Create ready counter
        elements.readyCounterText = CreateTextElement(parent, "ReadyCounterText", "0/1 Ready", normalFontSize - 2, 30f);
    }

    private GameObject CreatePlayerInfoSection(GameObject parent)
    {
        GameObject sectionGO = new GameObject("PlayerInfoSection");
        sectionGO.transform.SetParent(parent.transform, false);
        
        RectTransform sectionRect = sectionGO.AddComponent<RectTransform>();
        sectionRect.sizeDelta = new Vector2(0, 80f);
        
        HorizontalLayoutGroup layout = sectionGO.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        
        return sectionGO;
    }

    private GameObject CreateSelectionArea(GameObject parent)
    {
        GameObject areaGO = new GameObject("SelectionArea");
        areaGO.transform.SetParent(parent.transform, false);
        
        RectTransform areaRect = areaGO.AddComponent<RectTransform>();
        areaRect.sizeDelta = new Vector2(0, 400f);
        
        HorizontalLayoutGroup layout = areaGO.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 30f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        
        return areaGO;
    }

    private GameObject CreateSelectionSection(GameObject parent, string title, out ScrollRect scrollView, out Transform gridParent)
    {
        GameObject sectionGO = new GameObject($"{title}Section");
        sectionGO.transform.SetParent(parent.transform, false);
        
        RectTransform sectionRect = sectionGO.AddComponent<RectTransform>();
        
        // Add vertical layout for title + scroll view
        VerticalLayoutGroup layout = sectionGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        
        // Create title
        CreateTextElement(sectionGO, $"{title}Title", title, headerFontSize, 30f);
        
        // Create scroll view
        GameObject scrollViewGO = new GameObject($"{title}ScrollView");
        scrollViewGO.transform.SetParent(sectionGO.transform, false);
        
        RectTransform scrollRect = scrollViewGO.AddComponent<RectTransform>();
        scrollRect.sizeDelta = new Vector2(0, 360f);
        
        scrollView = scrollViewGO.AddComponent<ScrollRect>();
        scrollView.horizontal = false;
        scrollView.vertical = true;
        
        // Create viewport
        GameObject viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollViewGO.transform, false);
        
        RectTransform viewportRect = viewportGO.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.anchoredPosition = Vector2.zero;
        
        Image viewportMask = viewportGO.AddComponent<Image>();
        viewportMask.color = Color.clear;
        Mask mask = viewportGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        
        scrollView.viewport = viewportRect;
        
        // Create content
        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        
        RectTransform contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.sizeDelta = new Vector2(0, 0);
        contentRect.anchoredPosition = Vector2.zero;
        
        GridLayoutGroup grid = contentGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = selectionItemSize;
        grid.spacing = new Vector2(10f, 10f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        
        ContentSizeFitter fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        scrollView.content = contentRect;
        gridParent = contentGO.transform;
        
        return sectionGO;
    }

    private GameObject CreateDeckPreviewSection(GameObject parent, out ScrollRect scrollView, out Transform gridParent, out TextMeshProUGUI title)
    {
        GameObject panelGO = new GameObject("DeckPreviewPanel");
        panelGO.transform.SetParent(parent.transform, false);
        
        RectTransform panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(0, 300f);
        
        Image panelImage = panelGO.AddComponent<Image>();
        panelImage.color = panelColor;
        
        VerticalLayoutGroup layout = panelGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(15, 15, 15, 15);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        
        // Create title
        title = CreateTextElement(panelGO, "DeckPreviewTitle", "Deck Preview", headerFontSize, 30f);
        
        // Create scroll view for deck cards (similar to selection section but horizontal)
        GameObject scrollViewGO = new GameObject("DeckPreviewScrollView");
        scrollViewGO.transform.SetParent(panelGO.transform, false);
        
        RectTransform scrollRect = scrollViewGO.AddComponent<RectTransform>();
        scrollRect.sizeDelta = new Vector2(0, 240f);
        
        scrollView = scrollViewGO.AddComponent<ScrollRect>();
        scrollView.horizontal = true;
        scrollView.vertical = false;
        
        // Create viewport
        GameObject viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollViewGO.transform, false);
        
        RectTransform viewportRect = viewportGO.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.anchoredPosition = Vector2.zero;
        
        Image viewportMask = viewportGO.AddComponent<Image>();
        viewportMask.color = Color.clear;
        Mask mask = viewportGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        
        scrollView.viewport = viewportRect;
        
        // Create content
        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        
        RectTransform contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 0);
        contentRect.anchorMax = new Vector2(0, 1);
        contentRect.sizeDelta = new Vector2(0, 0);
        contentRect.anchoredPosition = Vector2.zero;
        
        HorizontalLayoutGroup grid = contentGO.AddComponent<HorizontalLayoutGroup>();
        grid.spacing = 10f;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.childControlWidth = false;
        grid.childControlHeight = false;
        
        ContentSizeFitter fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        scrollView.content = contentRect;
        gridParent = contentGO.transform;
        
        return panelGO;
    }



    private TextMeshProUGUI CreateTextElement(GameObject parent, string name, string text, int fontSize, float height)
    {
        GameObject textGO = new GameObject(name);
        textGO.transform.SetParent(parent.transform, false);
        
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(0, height);
        
        TextMeshProUGUI textMesh = textGO.AddComponent<TextMeshProUGUI>();
        textMesh.text = text;
        textMesh.fontSize = fontSize;
        textMesh.color = textColor;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontStyle = FontStyles.Bold;
        
        return textMesh;
    }

    private TMP_InputField CreateInputField(GameObject parent, string name, string placeholder, out TextMeshProUGUI label)
    {
        GameObject inputGO = new GameObject(name);
        inputGO.transform.SetParent(parent.transform, false);
        
        RectTransform inputRect = inputGO.AddComponent<RectTransform>();
        inputRect.sizeDelta = new Vector2(300f, 40f);
        
        Image inputImage = inputGO.AddComponent<Image>();
        inputImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        
        TMP_InputField inputField = inputGO.AddComponent<TMP_InputField>();
        inputField.characterLimit = 20; // Reasonable character limit for names
        
        // Create text area
        GameObject textAreaGO = new GameObject("Text Area");
        textAreaGO.transform.SetParent(inputGO.transform, false);
        
        RectTransform textAreaRect = textAreaGO.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.sizeDelta = Vector2.zero;
        textAreaRect.anchoredPosition = Vector2.zero;
        textAreaRect.offsetMin = new Vector2(10, 5);
        textAreaRect.offsetMax = new Vector2(-10, -5);
        
        // Create placeholder
        GameObject placeholderGO = new GameObject("Placeholder");
        placeholderGO.transform.SetParent(textAreaGO.transform, false);
        
        RectTransform placeholderRect = placeholderGO.AddComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.sizeDelta = Vector2.zero;
        placeholderRect.anchoredPosition = Vector2.zero;
        
        TextMeshProUGUI placeholderText = placeholderGO.AddComponent<TextMeshProUGUI>();
        placeholderText.text = placeholder;
        placeholderText.fontSize = normalFontSize;
        placeholderText.color = new Color(textColor.r, textColor.g, textColor.b, 0.5f);
        placeholderText.alignment = TextAlignmentOptions.Left;
        
        // Create input text
        GameObject inputTextGO = new GameObject("Text");
        inputTextGO.transform.SetParent(textAreaGO.transform, false);
        
        RectTransform inputTextRect = inputTextGO.AddComponent<RectTransform>();
        inputTextRect.anchorMin = Vector2.zero;
        inputTextRect.anchorMax = Vector2.one;
        inputTextRect.sizeDelta = Vector2.zero;
        inputTextRect.anchoredPosition = Vector2.zero;
        
        label = inputTextGO.AddComponent<TextMeshProUGUI>();
        label.text = "";
        label.fontSize = normalFontSize;
        label.color = textColor;
        label.alignment = TextAlignmentOptions.Left;
        
        inputField.textViewport = textAreaRect;
        inputField.placeholder = placeholderText;
        inputField.textComponent = label;
        
        return inputField;
    }

    private Button CreateButton(GameObject parent, string name, string text, Color buttonColor)
    {
        GameObject buttonGO = new GameObject(name);
        buttonGO.transform.SetParent(parent.transform, false);
        
        RectTransform buttonRect = buttonGO.AddComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(150f, 50f);
        
        Image buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = buttonColor;
        
        Button button = buttonGO.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        
        // Create button text
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);
        
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        
        TextMeshProUGUI buttonText = textGO.AddComponent<TextMeshProUGUI>();
        buttonText.text = text;
        buttonText.fontSize = normalFontSize;
        buttonText.color = textColor;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.fontStyle = FontStyles.Bold;
        
        return button;
    }

    #endregion

    #region Auto Assignment

    private void AutoAssignToUIManager(GameObject canvas, GameObject backgroundPanel, UIElements elements)
    {
        // Find CharacterSelectionUIManager if not already assigned
        if (targetUIManager == null)
        {
            targetUIManager = FindFirstObjectByType<CharacterSelectionUIManager>();
        }
        
        if (targetUIManager == null)
        {
            Debug.LogWarning("CharacterSelectionCanvasSetup: No CharacterSelectionUIManager found for auto-assignment");
            return;
        }
        
        // Use reflection to assign private fields (for auto-setup convenience)
        var type = typeof(CharacterSelectionUIManager);
        
        SetPrivateField(type, "characterSelectionCanvas", canvas);
        SetPrivateField(type, "backgroundPanel", backgroundPanel);
        SetPrivateField(type, "titleText", elements.titleText);
        SetPrivateField(type, "playerNameInputField", elements.playerNameInputField);
        SetPrivateField(type, "readyButton", elements.readyButton);
        SetPrivateField(type, "characterScrollView", elements.characterScrollView);
        SetPrivateField(type, "characterGridParent", elements.characterGridParent);
        SetPrivateField(type, "petScrollView", elements.petScrollView);
        SetPrivateField(type, "petGridParent", elements.petGridParent);
        SetPrivateField(type, "deckPreviewPanel", elements.deckPreviewPanel);
        SetPrivateField(type, "deckPreviewScrollView", elements.deckPreviewScrollView);
        SetPrivateField(type, "deckPreviewGridParent", elements.deckPreviewGridParent);
        SetPrivateField(type, "deckPreviewTitle", elements.deckPreviewTitle);
        SetPrivateField(type, "statusText", elements.statusText);
        SetPrivateField(type, "readyCounterText", elements.readyCounterText);
        
        Debug.Log("CharacterSelectionCanvasSetup: Auto-assigned UI elements to CharacterSelectionUIManager");
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
            Debug.LogWarning($"CharacterSelectionCanvasSetup: Could not set field {fieldName}");
        }
    }

    #endregion

    #region Helper Structures

    private struct UIElements
    {
        public TextMeshProUGUI titleText;
        public TMP_InputField playerNameInputField;
        public TextMeshProUGUI playerNameText;
        public Button readyButton;
        public ScrollRect characterScrollView;
        public Transform characterGridParent;
        public ScrollRect petScrollView;
        public Transform petGridParent;
        public GameObject deckPreviewPanel;
        public ScrollRect deckPreviewScrollView;
        public Transform deckPreviewGridParent;
        public TextMeshProUGUI deckPreviewTitle;
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI readyCounterText;
    }

    #endregion
} 