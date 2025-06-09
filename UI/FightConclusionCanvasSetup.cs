using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Helper script to set up the Fight Conclusion Canvas structure programmatically.
/// This follows SOLID principles by separating UI creation from UI management.
/// Attach to: A GameObject that will create the fight conclusion UI structure.
/// </summary>
public class FightConclusionCanvasSetup : MonoBehaviour
{
    [Header("Prefab References")]
    [SerializeField] private Canvas canvasPrefab;
    [SerializeField] private Font defaultFont;
    
    [Header("Styling Options")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.9f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color victoryColor = Color.green;
    [SerializeField] private Color defeatColor = Color.red;
    [SerializeField] private Color summaryTitleColor = Color.yellow;
    [SerializeField] private int titleFontSize = 72;
    [SerializeField] private int nameFontSize = 36;
    [SerializeField] private int summaryTitleFontSize = 32;
    [SerializeField] private int entryFontSize = 24;
    [SerializeField] private Vector2 imageSize = new Vector2(128f, 128f);
    [SerializeField] private Vector2 summaryImageSize = new Vector2(64f, 64f);
    
    [Header("Auto Setup")]
    [SerializeField] private bool createOnAwake = false;
    [SerializeField] private bool assignToFightConclusionUIManager = true;
    
    private GameObject createdCanvas;
    private FightConclusionUIManager targetUIManager;

    #region Lifecycle

    private void Awake()
    {
        if (createOnAwake)
        {
            CreateFightConclusionCanvas();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Creates the complete fight conclusion canvas structure
    /// </summary>
    [ContextMenu("Create Fight Conclusion Canvas")]
    public GameObject CreateFightConclusionCanvas()
    {
        if (createdCanvas != null)
        {
            Debug.LogWarning("FightConclusionCanvasSetup: Canvas already created, destroying previous version");
            DestroyCreatedCanvas();
        }

        // Create main canvas
        createdCanvas = CreateMainCanvas();
        
        // Create background panel
        GameObject backgroundPanel = CreateBackgroundPanel(createdCanvas);
        
        // Create main content panel
        GameObject mainContentPanel = CreateMainContentPanel(createdCanvas);
        
        // Create local result section
        GameObject localResultPanel = CreateLocalResultSection(mainContentPanel, out var localElements);
        
        // Create other results section
        GameObject otherResultsPanel = CreateOtherResultsSection(mainContentPanel, out var summaryElements);
        
        // Auto-assign to FightConclusionUIManager if requested
        if (assignToFightConclusionUIManager)
        {
            AutoAssignToUIManager(createdCanvas, backgroundPanel, localResultPanel, localElements, summaryElements);
        }
        
        Debug.Log("FightConclusionCanvasSetup: Fight conclusion canvas created successfully");
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
        GameObject canvasGO = new GameObject("FightConclusionCanvas");
        
        // Add Canvas component
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110; // Higher than fight preview to appear on top
        
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

    private GameObject CreateMainContentPanel(GameObject parent)
    {
        GameObject contentGO = new GameObject("MainContentPanel");
        contentGO.transform.SetParent(parent.transform, false);
        
        // Add RectTransform and center it
        RectTransform contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(1600f, 900f);
        contentRect.anchoredPosition = Vector2.zero;
        
        // Add horizontal layout for main content
        HorizontalLayoutGroup layout = contentGO.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 100f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        
        return contentGO;
    }

    private GameObject CreateLocalResultSection(GameObject parent, out LocalResultElements elements)
    {
        elements = new LocalResultElements();
        
        // Create local result panel
        GameObject localResultGO = new GameObject("LocalResultPanel");
        localResultGO.transform.SetParent(parent.transform, false);
        
        RectTransform localRect = localResultGO.AddComponent<RectTransform>();
        localRect.sizeDelta = new Vector2(600f, 800f);
        
        VerticalLayoutGroup localLayout = localResultGO.AddComponent<VerticalLayoutGroup>();
        localLayout.spacing = 30f;
        localLayout.childAlignment = TextAnchor.MiddleCenter;
        localLayout.childControlWidth = false;
        localLayout.childControlHeight = false;
        
        // Create result title text
        elements.resultText = CreateTextElement(localResultGO, "LocalPlayerResultText", "VICTORY!", titleFontSize);
        elements.resultText.color = victoryColor;
        
        // Create fight info section
        GameObject fightInfoGO = CreateFightInfoSection(localResultGO, out elements.playerNameText, out elements.opponentNameText, out elements.playerImage, out elements.opponentImage);
        
        return localResultGO;
    }

    private GameObject CreateFightInfoSection(GameObject parent, out TextMeshProUGUI playerNameText, out TextMeshProUGUI opponentNameText, out Image playerImage, out Image opponentImage)
    {
        GameObject fightInfoGO = new GameObject("FightInfoSection");
        fightInfoGO.transform.SetParent(parent.transform, false);
        
        RectTransform fightInfoRect = fightInfoGO.AddComponent<RectTransform>();
        fightInfoRect.sizeDelta = new Vector2(500f, 300f);
        
        VerticalLayoutGroup fightLayout = fightInfoGO.AddComponent<VerticalLayoutGroup>();
        fightLayout.spacing = 20f;
        fightLayout.childAlignment = TextAnchor.MiddleCenter;
        fightLayout.childControlWidth = false;
        fightLayout.childControlHeight = false;
        
        // Create player section
        GameObject playerSection = CreatePlayerSection(fightInfoGO, "PlayerSection");
        playerImage = CreateImageElement(playerSection, "LocalPlayerImage");
        playerNameText = CreateTextElement(playerSection, "LocalPlayerNameText", "Player Name", nameFontSize);
        
        // Create VS text
        CreateTextElement(fightInfoGO, "VersusText", "VS", nameFontSize);
        
        // Create opponent section
        GameObject opponentSection = CreatePlayerSection(fightInfoGO, "OpponentSection");
        opponentImage = CreateImageElement(opponentSection, "LocalOpponentImage");
        opponentNameText = CreateTextElement(opponentSection, "LocalOpponentNameText", "Opponent Pet", nameFontSize);
        
        return fightInfoGO;
    }

    private GameObject CreateOtherResultsSection(GameObject parent, out SummaryElements elements)
    {
        elements = new SummaryElements();
        
        // Create other results panel
        GameObject otherResultsGO = new GameObject("OtherResultsPanel");
        otherResultsGO.transform.SetParent(parent.transform, false);
        
        RectTransform otherRect = otherResultsGO.AddComponent<RectTransform>();
        otherRect.sizeDelta = new Vector2(800f, 800f);
        
        VerticalLayoutGroup otherLayout = otherResultsGO.AddComponent<VerticalLayoutGroup>();
        otherLayout.spacing = 20f;
        otherLayout.childAlignment = TextAnchor.UpperCenter;
        otherLayout.childControlWidth = true;
        otherLayout.childControlHeight = false;
        
        // Create summary title
        elements.summaryTitleText = CreateTextElement(otherResultsGO, "SummaryTitleText", "All Fight Results", summaryTitleFontSize);
        elements.summaryTitleText.color = summaryTitleColor;
        
        // Create scroll view for results
        GameObject scrollViewGO = CreateScrollView(otherResultsGO, out elements.scrollRect, out elements.container);
        
        // Create entry prefab
        elements.entryPrefab = CreateOtherResultEntryPrefab();
        
        return otherResultsGO;
    }

    private GameObject CreateScrollView(GameObject parent, out ScrollRect scrollRect, out Transform container)
    {
        // Create scroll view
        GameObject scrollViewGO = new GameObject("OtherResultsScrollView");
        scrollViewGO.transform.SetParent(parent.transform, false);
        
        RectTransform scrollRect_RT = scrollViewGO.AddComponent<RectTransform>();
        scrollRect_RT.sizeDelta = new Vector2(750f, 600f);
        
        scrollRect = scrollViewGO.AddComponent<ScrollRect>();
        
        // Create viewport
        GameObject viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollViewGO.transform, false);
        
        RectTransform viewportRect = viewportGO.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.anchoredPosition = Vector2.zero;
        
        Image viewportImage = viewportGO.AddComponent<Image>();
        viewportImage.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
        
        Mask viewportMask = viewportGO.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;
        
        // Create content
        GameObject contentGO = new GameObject("OtherResultsContainer");
        contentGO.transform.SetParent(viewportGO.transform, false);
        
        RectTransform contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 0f);
        contentRect.anchoredPosition = Vector2.zero;
        
        VerticalLayoutGroup contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 10f;
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        
        ContentSizeFitter contentSizeFitter = contentGO.AddComponent<ContentSizeFitter>();
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        // Setup scroll rect
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        
        container = contentGO.transform;
        return scrollViewGO;
    }

    private GameObject CreateOtherResultEntryPrefab()
    {
        GameObject entryGO = new GameObject("OtherResultEntry");
        
        RectTransform entryRect = entryGO.AddComponent<RectTransform>();
        entryRect.sizeDelta = new Vector2(700f, 80f);
        
        // Add background
        Image entryBG = entryGO.AddComponent<Image>();
        entryBG.color = new Color(0.2f, 0.2f, 0.2f, 0.7f);
        
        HorizontalLayoutGroup entryLayout = entryGO.AddComponent<HorizontalLayoutGroup>();
        entryLayout.spacing = 15f;
        entryLayout.padding = new RectOffset(10, 10, 10, 10);
        entryLayout.childAlignment = TextAnchor.MiddleLeft;
        entryLayout.childControlWidth = false;
        entryLayout.childControlHeight = false;
        
        // Create player section
        GameObject playerSection = new GameObject("PlayerSection");
        playerSection.transform.SetParent(entryGO.transform, false);
        
        HorizontalLayoutGroup playerLayout = playerSection.AddComponent<HorizontalLayoutGroup>();
        playerLayout.spacing = 10f;
        playerLayout.childAlignment = TextAnchor.MiddleLeft;
        playerLayout.childControlWidth = false;
        playerLayout.childControlHeight = false;
        
        Image playerImage = CreateImageElement(playerSection, "PlayerImage");
        playerImage.GetComponent<RectTransform>().sizeDelta = summaryImageSize;
        CreateTextElement(playerSection, "PlayerName", "Player", entryFontSize);
        
        // Create VS text
        CreateTextElement(entryGO, "VersusText", "VS", entryFontSize);
        
        // Create opponent section
        GameObject opponentSection = new GameObject("OpponentSection");
        opponentSection.transform.SetParent(entryGO.transform, false);
        
        HorizontalLayoutGroup opponentLayout = opponentSection.AddComponent<HorizontalLayoutGroup>();
        opponentLayout.spacing = 10f;
        opponentLayout.childAlignment = TextAnchor.MiddleLeft;
        opponentLayout.childControlWidth = false;
        opponentLayout.childControlHeight = false;
        
        Image opponentImage = CreateImageElement(opponentSection, "OpponentImage");
        opponentImage.GetComponent<RectTransform>().sizeDelta = summaryImageSize;
        CreateTextElement(opponentSection, "OpponentName", "Opponent", entryFontSize);
        
        // Create result text
        TextMeshProUGUI resultText = CreateTextElement(entryGO, "Result", "Won", entryFontSize);
        resultText.color = victoryColor;
        
        return entryGO;
    }

    private GameObject CreatePlayerSection(GameObject parent, string name)
    {
        GameObject sectionGO = new GameObject(name);
        sectionGO.transform.SetParent(parent.transform, false);
        
        RectTransform sectionRect = sectionGO.AddComponent<RectTransform>();
        sectionRect.sizeDelta = new Vector2(200f, 180f);
        
        VerticalLayoutGroup layout = sectionGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
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

    private void AutoAssignToUIManager(GameObject canvas, GameObject backgroundPanel, GameObject localResultPanel, LocalResultElements localElements, SummaryElements summaryElements)
    {
        // Find FightConclusionUIManager if not already assigned
        if (targetUIManager == null)
        {
            targetUIManager = FindFirstObjectByType<FightConclusionUIManager>();
        }
        
        if (targetUIManager == null)
        {
            Debug.LogWarning("FightConclusionCanvasSetup: No FightConclusionUIManager found for auto-assignment");
            return;
        }
        
        // Use reflection to assign private fields (for auto-setup convenience)
        var type = typeof(FightConclusionUIManager);
        
        SetPrivateField(type, "fightConclusionCanvas", canvas);
        SetPrivateField(type, "localPlayerResultText", localElements.resultText);
        SetPrivateField(type, "localPlayerNameText", localElements.playerNameText);
        SetPrivateField(type, "localOpponentNameText", localElements.opponentNameText);
        SetPrivateField(type, "localPlayerImage", localElements.playerImage);
        SetPrivateField(type, "localOpponentImage", localElements.opponentImage);
        SetPrivateField(type, "localResultPanel", localResultPanel);
        SetPrivateField(type, "summaryTitleText", summaryElements.summaryTitleText);
        SetPrivateField(type, "otherResultsContainer", summaryElements.container);
        SetPrivateField(type, "otherResultEntryPrefab", summaryElements.entryPrefab);
        SetPrivateField(type, "otherResultsScrollRect", summaryElements.scrollRect);
        
        Debug.Log("FightConclusionCanvasSetup: Auto-assigned UI elements to FightConclusionUIManager");
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
            Debug.LogWarning($"FightConclusionCanvasSetup: Could not set field {fieldName}");
        }
    }

    #endregion

    #region Helper Structures

    private struct LocalResultElements
    {
        public TextMeshProUGUI resultText;
        public TextMeshProUGUI playerNameText;
        public TextMeshProUGUI opponentNameText;
        public Image playerImage;
        public Image opponentImage;
    }

    private struct SummaryElements
    {
        public TextMeshProUGUI summaryTitleText;
        public Transform container;
        public GameObject entryPrefab;
        public ScrollRect scrollRect;
    }

    #endregion
} 