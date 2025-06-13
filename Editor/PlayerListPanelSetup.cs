using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One-time setup script to create the player list panel UI structure for CharacterSelectionUIManager
/// </summary>
public class PlayerListPanelSetup : EditorWindow
{
    [MenuItem("Tools/Setup Player List Panel")]
    public static void ShowWindow()
    {
        GetWindow<PlayerListPanelSetup>("Player List Panel Setup");
    }

    private CharacterSelectionUIManager targetUIManager;
    private Transform canvasTransform;

    private void OnGUI()
    {
        GUILayout.Label("Player List Panel Setup", EditorStyles.boldLabel);
        GUILayout.Space(10);

        targetUIManager = (CharacterSelectionUIManager)EditorGUILayout.ObjectField(
            "Character Selection UI Manager", 
            targetUIManager, 
            typeof(CharacterSelectionUIManager), 
            true);

        canvasTransform = (Transform)EditorGUILayout.ObjectField(
            "Canvas Transform", 
            canvasTransform, 
            typeof(Transform), 
            true);

        GUILayout.Space(10);

        if (GUILayout.Button("Create Player List Panel"))
        {
            CreatePlayerListPanel();
        }

        GUILayout.Space(10);
        
        if (GUILayout.Button("Auto-Find References"))
        {
            AutoFindReferences();
        }

        GUILayout.Space(5);
        GUILayout.Label("Instructions:", EditorStyles.boldLabel);
        GUILayout.Label("1. Assign your CharacterSelectionUIManager");
        GUILayout.Label("2. Assign the Canvas containing your character selection UI");
        GUILayout.Label("3. Click 'Create Player List Panel' to generate the UI");
        GUILayout.Label("4. The script will automatically assign all references");
    }

    private void AutoFindReferences()
    {
        if (targetUIManager == null)
        {
            targetUIManager = FindObjectOfType<CharacterSelectionUIManager>();
        }

        if (canvasTransform == null)
        {
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            foreach (Canvas canvas in canvases)
            {
                if (canvas.name.ToLower().Contains("character") && canvas.name.ToLower().Contains("selection"))
                {
                    canvasTransform = canvas.transform;
                    break;
                }
            }
        }

        if (targetUIManager != null && canvasTransform == null)
        {
            canvasTransform = targetUIManager.transform.GetComponentInParent<Canvas>()?.transform;
        }
    }

    private void CreatePlayerListPanel()
    {
        if (targetUIManager == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a CharacterSelectionUIManager", "OK");
            return;
        }

        if (canvasTransform == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a Canvas Transform", "OK");
            return;
        }

        // Create the main player list panel
        GameObject playerListPanel = CreatePlayerListPanelStructure();
        
        // Create the show players button
        GameObject showPlayersButton = CreateShowPlayersButton();
        
        // Create the leave game button
        GameObject leaveGameButton = CreateLeaveGameButton();
        
        // Create player list item prefab
        GameObject playerListItemPrefab = CreatePlayerListItemPrefab();

        // Assign all references using reflection to access private fields
        AssignReferencesToUIManager(playerListPanel, showPlayersButton, leaveGameButton, playerListItemPrefab);

        Debug.Log("Player List Panel setup complete!");
        EditorUtility.DisplayDialog("Success", "Player List Panel created and references assigned!", "OK");
    }

    private GameObject CreatePlayerListPanelStructure()
    {
        // Create main panel
        GameObject panel = new GameObject("PlayerListPanel");
        panel.transform.SetParent(canvasTransform, false);

        // Add RectTransform and set up as side panel
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 0.5f);
        panelRect.anchoredPosition = new Vector2(-300, 0); // Start off-screen
        panelRect.sizeDelta = new Vector2(300, 0);

        // Add background image
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        // Create title
        GameObject title = new GameObject("Title");
        title.transform.SetParent(panel.transform, false);
        
        RectTransform titleRect = title.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.9f);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.sizeDelta = Vector2.zero;
        titleRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI titleText = title.AddComponent<TextMeshProUGUI>();
        titleText.text = "Players in Game";
        titleText.fontSize = 18;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontStyle = FontStyles.Bold;

        // Create close button
        GameObject closeButton = new GameObject("CloseButton");
        closeButton.transform.SetParent(panel.transform, false);
        
        RectTransform closeButtonRect = closeButton.AddComponent<RectTransform>();
        closeButtonRect.anchorMin = new Vector2(0.85f, 0.9f);
        closeButtonRect.anchorMax = new Vector2(1, 1);
        closeButtonRect.sizeDelta = Vector2.zero;
        closeButtonRect.anchoredPosition = Vector2.zero;

        Button closeButtonComponent = closeButton.AddComponent<Button>();
        Image closeButtonImage = closeButton.AddComponent<Image>();
        closeButtonImage.color = new Color(0.8f, 0.2f, 0.2f, 1f);

        GameObject closeButtonText = new GameObject("Text");
        closeButtonText.transform.SetParent(closeButton.transform, false);
        
        RectTransform closeButtonTextRect = closeButtonText.AddComponent<RectTransform>();
        closeButtonTextRect.anchorMin = Vector2.zero;
        closeButtonTextRect.anchorMax = Vector2.one;
        closeButtonTextRect.sizeDelta = Vector2.zero;
        closeButtonTextRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI closeButtonTextComponent = closeButtonText.AddComponent<TextMeshProUGUI>();
        closeButtonTextComponent.text = "X";
        closeButtonTextComponent.fontSize = 14;
        closeButtonTextComponent.color = Color.white;
        closeButtonTextComponent.alignment = TextAlignmentOptions.Center;

        // Create scroll view
        GameObject scrollView = new GameObject("ScrollView");
        scrollView.transform.SetParent(panel.transform, false);
        
        RectTransform scrollViewRect = scrollView.AddComponent<RectTransform>();
        scrollViewRect.anchorMin = new Vector2(0, 0);
        scrollViewRect.anchorMax = new Vector2(1, 0.9f);
        scrollViewRect.sizeDelta = Vector2.zero;
        scrollViewRect.anchoredPosition = Vector2.zero;

        ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // Create viewport
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollView.transform, false);
        
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.anchoredPosition = Vector2.zero;

        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        
        Mask viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        // Create content
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);
        contentRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.childControlHeight = false;
        contentLayout.childControlWidth = true;
        contentLayout.spacing = 5;
        contentLayout.padding = new RectOffset(10, 10, 10, 10);

        ContentSizeFitter contentSizeFitter = content.AddComponent<ContentSizeFitter>();
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Connect scroll rect references
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        // Store references in the panel for easy access
        PlayerListPanelReferences references = panel.AddComponent<PlayerListPanelReferences>();
        references.playerListPanel = panel;
        references.playerListScrollView = scrollRect;
        references.playerListContent = content.transform;
        references.playerListCloseButton = closeButtonComponent;
        references.playerListTitle = titleText;

        panel.SetActive(false); // Start hidden

        return panel;
    }

    private GameObject CreateShowPlayersButton()
    {
        GameObject button = new GameObject("ShowPlayersButton");
        button.transform.SetParent(canvasTransform, false);
        
        RectTransform buttonRect = button.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0, 1);
        buttonRect.anchorMax = new Vector2(0, 1);
        buttonRect.pivot = new Vector2(0, 1);
        buttonRect.anchoredPosition = new Vector2(10, -10);
        buttonRect.sizeDelta = new Vector2(120, 40);

        Button buttonComponent = button.AddComponent<Button>();
        Image buttonImage = button.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.5f, 0.8f, 1f);

        GameObject buttonText = new GameObject("Text");
        buttonText.transform.SetParent(button.transform, false);
        
        RectTransform buttonTextRect = buttonText.AddComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.sizeDelta = Vector2.zero;
        buttonTextRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI buttonTextComponent = buttonText.AddComponent<TextMeshProUGUI>();
        buttonTextComponent.text = "Show Players";
        buttonTextComponent.fontSize = 12;
        buttonTextComponent.color = Color.white;
        buttonTextComponent.alignment = TextAlignmentOptions.Center;

        return button;
    }

    private GameObject CreateLeaveGameButton()
    {
        GameObject button = new GameObject("LeaveGameButton");
        button.transform.SetParent(canvasTransform, false);
        
        RectTransform buttonRect = button.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0, 1);
        buttonRect.anchorMax = new Vector2(0, 1);
        buttonRect.pivot = new Vector2(0, 1);
        buttonRect.anchoredPosition = new Vector2(140, -10);
        buttonRect.sizeDelta = new Vector2(100, 40);

        Button buttonComponent = button.AddComponent<Button>();
        Image buttonImage = button.AddComponent<Image>();
        buttonImage.color = new Color(0.8f, 0.3f, 0.2f, 1f);

        GameObject buttonText = new GameObject("Text");
        buttonText.transform.SetParent(button.transform, false);
        
        RectTransform buttonTextRect = buttonText.AddComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.sizeDelta = Vector2.zero;
        buttonTextRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI buttonTextComponent = buttonText.AddComponent<TextMeshProUGUI>();
        buttonTextComponent.text = "Leave Game";
        buttonTextComponent.fontSize = 12;
        buttonTextComponent.color = Color.white;
        buttonTextComponent.alignment = TextAlignmentOptions.Center;

        return button;
    }

    private GameObject CreatePlayerListItemPrefab()
    {
        GameObject prefab = new GameObject("PlayerListItemPrefab");
        
        RectTransform prefabRect = prefab.AddComponent<RectTransform>();
        prefabRect.sizeDelta = new Vector2(280, 80);

        Image prefabImage = prefab.AddComponent<Image>();
        prefabImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        GameObject textObject = new GameObject("PlayerInfo");
        textObject.transform.SetParent(prefab.transform, false);
        
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI textComponent = textObject.AddComponent<TextMeshProUGUI>();
        textComponent.fontSize = 12;
        textComponent.color = Color.white;
        textComponent.alignment = TextAlignmentOptions.TopLeft;
        textComponent.margin = new Vector4(10, 5, 10, 5);
        textComponent.text = "Player Name [Ready]\nCharacter: Character Name\nPet: Pet Name";

        // Save as prefab
        string prefabPath = "Assets/Prefabs/UI/PlayerListItemPrefab.prefab";
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(prefabPath));
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
        
        DestroyImmediate(prefab); // Remove the temporary object
        
        return savedPrefab;
    }

    private void AssignReferencesToUIManager(GameObject playerListPanel, GameObject showPlayersButton, GameObject leaveGameButton, GameObject playerListItemPrefab)
    {
        SerializedObject serializedUIManager = new SerializedObject(targetUIManager);
        
        PlayerListPanelReferences references = playerListPanel.GetComponent<PlayerListPanelReferences>();
        
        // Assign the references using SerializedProperty
        serializedUIManager.FindProperty("showPlayersButton").objectReferenceValue = showPlayersButton.GetComponent<Button>();
        serializedUIManager.FindProperty("leaveGameButton").objectReferenceValue = leaveGameButton.GetComponent<Button>();
        serializedUIManager.FindProperty("playerListPanel").objectReferenceValue = references.playerListPanel;
        serializedUIManager.FindProperty("playerListScrollView").objectReferenceValue = references.playerListScrollView;
        serializedUIManager.FindProperty("playerListContent").objectReferenceValue = references.playerListContent;
        serializedUIManager.FindProperty("playerListCloseButton").objectReferenceValue = references.playerListCloseButton;
        serializedUIManager.FindProperty("playerListTitle").objectReferenceValue = references.playerListTitle;
        serializedUIManager.FindProperty("playerListItemPrefab").objectReferenceValue = playerListItemPrefab;
        
        serializedUIManager.ApplyModifiedProperties();
        
        // Clean up the temporary component
        DestroyImmediate(references);
    }
}

/// <summary>
/// Temporary component to hold references during setup
/// </summary>
public class PlayerListPanelReferences : MonoBehaviour
{
    public GameObject playerListPanel;
    public ScrollRect playerListScrollView;
    public Transform playerListContent;
    public Button playerListCloseButton;
    public TextMeshProUGUI playerListTitle;
} 