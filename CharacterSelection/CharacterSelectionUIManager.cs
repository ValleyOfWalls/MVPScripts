using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using FishNet.Object;

/// <summary>
/// Manages the character selection UI interactions with Mario Kart-style shared selection grids.
/// </summary>
public class CharacterSelectionUIManager : MonoBehaviour
{
    [Header("UI References - Set by CharacterSelectionCanvasSetup")]
    [SerializeField] private GameObject characterSelectionCanvas;
    [SerializeField] private GameObject backgroundPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TMP_InputField playerNameInputField;
    [SerializeField] private Button readyButton;
    [SerializeField] private ScrollRect characterScrollView;
    [SerializeField] private Transform characterGridParent;
    [SerializeField] private ScrollRect petScrollView;
    [SerializeField] private Transform petGridParent;
    [SerializeField] private GameObject characterDeckPanel;
    [SerializeField] private ScrollRect characterDeckScrollView;
    [SerializeField] private Transform characterDeckGridParent;
    [SerializeField] private TextMeshProUGUI characterDeckTitle;
    [SerializeField] private GameObject petDeckPanel;
    [SerializeField] private ScrollRect petDeckScrollView;
    [SerializeField] private Transform petDeckGridParent;
    [SerializeField] private TextMeshProUGUI petDeckTitle;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI readyCounterText;
    
    [Header("Player List Panel")]
    [SerializeField] private Button showPlayersButton;
    [SerializeField] private Button leaveGameButton;
    [SerializeField] private GameObject playerListPanel;
    [SerializeField] private ScrollRect playerListScrollView;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private Button playerListCloseButton;
    [SerializeField] private TextMeshProUGUI playerListTitle;
    
    [Header("Selection Item Prefab")]
    [SerializeField] private GameObject selectionItemPrefab;
    [SerializeField] private GameObject deckCardPrefab;
    [SerializeField] private GameObject playerListItemPrefab;
    
    [Header("Styling")]
    [SerializeField] private Color selectedColor = new Color(0.3f, 0.6f, 1f, 1f);
    [SerializeField] private Color unselectedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color notReadyColor = Color.gray;
    
    // Player color system
    private static readonly Color[] PLAYER_COLORS = new Color[]
    {
        new Color(1f, 0.2f, 0.2f, 1f),     // Red
        new Color(0.2f, 0.5f, 1f, 1f),     // Blue  
        new Color(0.2f, 1f, 0.2f, 1f),     // Green
        new Color(1f, 1f, 0.2f, 1f),       // Yellow
        new Color(1f, 0.5f, 0.2f, 1f),     // Orange
        new Color(0.8f, 0.2f, 1f, 1f),     // Purple
        new Color(0.2f, 1f, 1f, 1f),       // Cyan
        new Color(1f, 0.2f, 0.8f, 1f)      // Pink
    };
    
    // Manager reference
    private CharacterSelectionManager selectionManager;
    
    // Current selections
    private int selectedCharacterIndex = -1;
    private int selectedPetIndex = -1;
    private string customPlayerName = "";
    
    // Available options
    private List<CharacterData> availableCharacters;
    private List<PetData> availablePets;
    
    // UI element lists
    private List<GameObject> characterItems = new List<GameObject>();
    private List<GameObject> petItems = new List<GameObject>();
    private List<GameObject> characterDeckItems = new List<GameObject>();
    private List<GameObject> petDeckItems = new List<GameObject>();
    private List<GameObject> playerListItems = new List<GameObject>();
    
    // State
    private bool isReady = false;
    private bool hasValidSelection = false;
    private float lastReadyButtonClickTime = 0f;
    private const float READY_BUTTON_COOLDOWN = 0.2f; // 200ms cooldown
    private bool isInitialized = false; // Prevent multiple initializations
    
    // UI Animator
    private CharacterSelectionUIAnimator uiAnimator;
    
    // Player data for Mario Kart-style display
    private Dictionary<string, PlayerSelectionDisplayInfo> otherPlayersData = new Dictionary<string, PlayerSelectionDisplayInfo>();
    private Color myPlayerColor = Color.white;
    private string myPlayerID = "";
    
    // Store the latest player info for when panel becomes visible
    private List<PlayerSelectionInfo> latestPlayerInfos = new List<PlayerSelectionInfo>();

    #region Initialization

    public void Initialize(CharacterSelectionManager manager, List<CharacterData> characters, List<PetData> pets)
    {
        Debug.Log($"CharacterSelectionUIManager: Initialize() called - isInitialized: {isInitialized}");
        
        if (isInitialized)
        {
            Debug.Log("CharacterSelectionUIManager: Already initialized, skipping duplicate initialization to prevent panel disruption");
            return;
        }
        
        Debug.Log("CharacterSelectionUIManager: Starting initialization sequence");
        
        selectionManager = manager;
        availableCharacters = characters;
        availablePets = pets;
        
        // Assign player color and ID
        myPlayerID = GetPlayerID();
        myPlayerColor = GetPlayerColor(myPlayerID);
        
        Debug.Log("CharacterSelectionUIManager: Setting up UI...");
        SetupUI();
        
        Debug.Log("CharacterSelectionUIManager: Creating selection items...");
        CreateSelectionItems();
        
        Debug.Log("CharacterSelectionUIManager: Making default selections (this will trigger deck previews)...");
        MakeDefaultSelections();
        
        Debug.Log("CharacterSelectionUIManager: Updating ready button state...");
        UpdateReadyButtonState();
        
        isInitialized = true;
        Debug.Log($"CharacterSelectionUIManager: Initialized - Player ID: {myPlayerID}, Color: {myPlayerColor}");
    }
    
    private string GetPlayerID()
    {
        // Use connection ID or steam ID as unique identifier
        return FishNet.InstanceFinder.ClientManager?.Connection?.ClientId.ToString() ?? System.Guid.NewGuid().ToString();
    }
    
    private Color GetPlayerColor(string playerID)
    {
        // Assign color based on player ID hash for consistency
        int colorIndex = Mathf.Abs(playerID.GetHashCode()) % PLAYER_COLORS.Length;
        return PLAYER_COLORS[colorIndex];
    }
    
    private void SetupUI()
    {
        // Enable the canvas
        if (characterSelectionCanvas != null)
        {
            characterSelectionCanvas.SetActive(true);
        }
        
        // Set up UI animator
        SetupUIAnimator();
        
        // Set up input field
        if (playerNameInputField != null)
        {
            playerNameInputField.onValueChanged.RemoveAllListeners();
            playerNameInputField.onValueChanged.AddListener(OnPlayerNameChanged);
        }
        
        // Set up ready button
        if (readyButton != null)
        {
            // Remove any existing listeners to prevent duplicates
            readyButton.onClick.RemoveAllListeners();
            readyButton.onClick.AddListener(OnReadyButtonClicked);
            readyButton.interactable = false;
        }
        
        // Set up player list panel and buttons
        SetupPlayerListPanel();
        
        // Initialize status text
        if (statusText != null)
        {
            statusText.text = "Select a character and pet to continue...";
        }
        
        // Initialize ready counter
        UpdateReadyCounter(0, 1); // Start with 0/1 until we know player count
    }
    
    private void SetupUIAnimator()
    {
        // Create or get the UI animator component
        uiAnimator = GetComponent<CharacterSelectionUIAnimator>();
        if (uiAnimator == null)
        {
            uiAnimator = gameObject.AddComponent<CharacterSelectionUIAnimator>();
        }
        
        // Initialize the animator with panel references
        uiAnimator.Initialize(playerListPanel, characterDeckPanel, petDeckPanel);
        
        // Subscribe to visibility change events
        uiAnimator.OnPlayerListVisibilityChanged += OnPlayerListVisibilityChanged;
        uiAnimator.OnCharacterDeckVisibilityChanged += OnCharacterDeckVisibilityChanged;
        uiAnimator.OnPetDeckVisibilityChanged += OnPetDeckVisibilityChanged;
        
        // Validate grid parent references before setting them
        ValidateGridParentReferences();
        
        // Set up click detection areas using the grid parents instead of scroll views
        uiAnimator.SetClickDetectionAreas(
            characterGridParent?.GetComponent<RectTransform>(), 
            petGridParent?.GetComponent<RectTransform>()
        );
        
        Debug.Log("CharacterSelectionUIManager: UI Animator setup complete");
    }
    
    private void ValidateGridParentReferences()
    {
        // Check and log grid parent reference status
        Debug.Log($"CharacterSelectionUIManager: Validating grid parent references:");
        Debug.Log($"  - characterGridParent: {characterGridParent?.name ?? "NULL"}");
        Debug.Log($"  - petGridParent: {petGridParent?.name ?? "NULL"}");
        
        if (characterGridParent == null)
        {
            Debug.LogError("CharacterSelectionUIManager: characterGridParent is null! This will prevent click detection from working properly.");
        }
        else
        {
            RectTransform rectTransform = characterGridParent.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                Debug.LogError($"CharacterSelectionUIManager: characterGridParent '{characterGridParent.name}' has no RectTransform component!");
            }
            else
            {
                Debug.Log($"CharacterSelectionUIManager: characterGridParent '{characterGridParent.name}' has {characterGridParent.childCount} children");
            }
        }
        
        if (petGridParent == null)
        {
            Debug.LogError("CharacterSelectionUIManager: petGridParent is null! This will prevent click detection from working properly.");
        }
        else
        {
            RectTransform rectTransform = petGridParent.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                Debug.LogError($"CharacterSelectionUIManager: petGridParent '{petGridParent.name}' has no RectTransform component!");
            }
            else
            {
                Debug.Log($"CharacterSelectionUIManager: petGridParent '{petGridParent.name}' has {petGridParent.childCount} children");
            }
        }
    }
    
    private void OnPlayerListVisibilityChanged(bool isVisible)
    {
        // Handle any additional logic when player list visibility changes
        Debug.Log($"CharacterSelectionUIManager: Player list visibility changed to {isVisible}");
        
        // Update player list with current data when panel becomes visible
        if (isVisible && latestPlayerInfos != null && latestPlayerInfos.Count > 0)
        {
            UpdatePlayerListPanel(latestPlayerInfos);
            Debug.Log($"CharacterSelectionUIManager: Player list populated with {latestPlayerInfos.Count} players");
        }
    }
    
    private void OnCharacterDeckVisibilityChanged(bool isVisible)
    {
        // Handle any additional logic when character deck visibility changes
        Debug.Log($"CharacterSelectionUIManager: Character deck visibility changed to {isVisible}");
    }
    
    private void OnPetDeckVisibilityChanged(bool isVisible)
    {
        // Handle any additional logic when pet deck visibility changes
        Debug.Log($"CharacterSelectionUIManager: Pet deck visibility changed to {isVisible}");
    }
    
    private void SetupPlayerListPanel()
    {
        // Set up show players button
        if (showPlayersButton != null)
        {
            showPlayersButton.onClick.RemoveAllListeners();
            showPlayersButton.onClick.AddListener(OnShowPlayersButtonClicked);
        }
        
        // Set up leave game button
        if (leaveGameButton != null)
        {
            leaveGameButton.onClick.RemoveAllListeners();
            leaveGameButton.onClick.AddListener(OnLeaveGameButtonClicked);
        }
        
        // Set up player list close button
        if (playerListCloseButton != null)
        {
            playerListCloseButton.onClick.RemoveAllListeners();
            playerListCloseButton.onClick.AddListener(OnPlayerListCloseButtonClicked);
        }
        
        // Initialize player list panel as hidden
        if (playerListPanel != null)
        {
            playerListPanel.SetActive(false);
        }
        
        // Set player list title
        if (playerListTitle != null)
        {
            playerListTitle.text = "Players in Game";
        }
    }

    private void CreateSelectionItems()
    {
        CreateCharacterItems();
        CreatePetItems();
    }

    private void CreateCharacterItems()
    {
        // Clear existing items
        foreach (GameObject item in characterItems)
        {
            if (item != null) Destroy(item);
        }
        characterItems.Clear();
        
        // Create character selection items
        for (int i = 0; i < availableCharacters.Count; i++)
        {
            CharacterData character = availableCharacters[i];
            if (character == null) continue;
            
            GameObject item = CreateSelectionItem(characterGridParent, character.CharacterName, character.CharacterPortrait, character.CharacterDescription, true, i);
            if (item != null)
            {
                int index = i; // Capture for closure
                Button button = item.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => OnCharacterSelected(index));
                }
                characterItems.Add(item);
            }
        }
    }

    private void CreatePetItems()
    {
        // Clear existing items
        foreach (GameObject item in petItems)
        {
            if (item != null) Destroy(item);
        }
        petItems.Clear();
        
        // Create pet selection items
        for (int i = 0; i < availablePets.Count; i++)
        {
            PetData pet = availablePets[i];
            if (pet == null) continue;
            
            GameObject item = CreateSelectionItem(petGridParent, pet.PetName, pet.PetPortrait, pet.PetDescription, false, i);
            if (item != null)
            {
                int index = i; // Capture for closure
                Button button = item.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => OnPetSelected(index));
                }
                petItems.Add(item);
            }
        }
    }

    private GameObject CreateSelectionItem(Transform parent, string itemName, Sprite portrait, string description, bool isCharacter, int index)
    {
        // Since prefab is provided, we should always use it
        if (selectionItemPrefab == null)
        {
            Debug.LogError("CharacterSelectionUIManager: Selection Item Prefab is not assigned!");
            return null;
        }
        
        GameObject item = Instantiate(selectionItemPrefab, parent);
        
        // Set up the item data using the prefab structure
        // Find the name text component
        TextMeshProUGUI nameText = item.GetComponentInChildren<TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text = itemName;
        }
        
        // Find the portrait image component specifically (not the root image)
        // Look for an Image component that's likely the portrait (not the background)
        Image[] images = item.GetComponentsInChildren<Image>();
        Image portraitImage = null;
        
        // Try to find portrait by name or component order
        foreach (Image img in images)
        {
            // Look for portrait-specific naming or the second image (assuming first is background)
            if (img.gameObject.name.ToLower().Contains("portrait") || 
                img.gameObject.name.ToLower().Contains("image") ||
                img != item.GetComponent<Image>()) // Not the root image
            {
                portraitImage = img;
                break;
            }
        }
        
        // If no specific portrait found, use the second image component (skip root)
        if (portraitImage == null && images.Length > 1)
        {
            portraitImage = images[1]; // Assume index 0 is root background
        }
        
        // Set the portrait sprite
        if (portraitImage != null && portrait != null)
        {
            portraitImage.sprite = portrait;
        }
        else if (portrait != null)
        {
            Debug.LogWarning($"CharacterSelectionUIManager: Could not find portrait Image component in prefab for {itemName}");
        }
        
        return item;
    }

    private void MakeDefaultSelections()
    {
        Debug.Log($"CharacterSelectionUIManager: MakeDefaultSelections() called - availableCharacters: {availableCharacters.Count}, availablePets: {availablePets.Count}");
        
        // Auto-select first character and first pet if available
        if (availableCharacters.Count > 0)
        {
            Debug.Log($"CharacterSelectionUIManager: Auto-selecting first character: {availableCharacters[0].CharacterName}");
            OnCharacterSelected(0);
            Debug.Log($"CharacterSelectionUIManager: Auto-selected first character: {availableCharacters[0].CharacterName}");
        }
        
        if (availablePets.Count > 0)
        {
            Debug.Log($"CharacterSelectionUIManager: Auto-selecting first pet: {availablePets[0].PetName}");
            OnPetSelected(0);
            Debug.Log($"CharacterSelectionUIManager: Auto-selected first pet: {availablePets[0].PetName}");
        }
        
        // Ensure the selection gets sent to server after both are selected
        if (hasValidSelection)
        {
            Debug.Log("CharacterSelectionUIManager: Both character and pet selected, sending to server");
            if (selectionManager != null)
            {
                selectionManager.RequestSelectionUpdate(selectedCharacterIndex, selectedPetIndex, customPlayerName, "");
            }
        }
        
        // Check if auto-test runner wants us to auto-ready
        AutoTestRunner autoTestRunner = FindFirstObjectByType<AutoTestRunner>();
        if (autoTestRunner != null && autoTestRunner.enableAutoTesting)
        {
            // Auto-ready after a short delay to ensure UI is set up
            Debug.Log("CharacterSelectionUIManager: Auto-test runner detected, will auto-ready after delay");
            StartCoroutine(AutoReadyAfterDelay());
        }
    }
    
    private System.Collections.IEnumerator AutoReadyAfterDelay()
    {
        yield return new WaitForSeconds(0.5f); // Allow UI to update
        
        if (hasValidSelection)
        {
            Debug.Log("CharacterSelectionUIManager: Auto-ready triggered by AutoTestRunner");
            OnReadyButtonClicked();
        }
    }

    #endregion

    #region Selection Handling

    private void OnCharacterSelected(int characterIndex)
    {
        Debug.Log($"CharacterSelectionUIManager: OnCharacterSelected({characterIndex}) called - isReady: {isReady}");
        
        if (characterIndex < 0 || characterIndex >= availableCharacters.Count) return;
        
        // Check if this is a different selection than current
        bool isSelectionChange = selectedCharacterIndex != characterIndex && selectedCharacterIndex >= 0;
        
        // Update selection
        selectedCharacterIndex = characterIndex;
        
        // Update visual selection
        UpdateMySelectionVisuals();
        
        // Show character deck preview
        if (!isReady)
        {
            Debug.Log("CharacterSelectionUIManager: Player not ready, showing character deck");
            if (isSelectionChange)
            {
                Debug.Log("CharacterSelectionUIManager: Character selection changed - refreshing deck with animation");
                RefreshCharacterDeckWithAnimation();
            }
            else
            {
                Debug.Log("CharacterSelectionUIManager: First character selection - showing deck normally");
                ShowCharacterDeck();
            }
        }
        else
        {
            Debug.Log("CharacterSelectionUIManager: Player is ready, NOT showing character deck");
        }
        
        // Update ready state
        UpdateSelectionState();
        
        Debug.Log($"CharacterSelectionUIManager: Selected character {availableCharacters[characterIndex].CharacterName}");
    }

    private void OnPetSelected(int petIndex)
    {
        Debug.Log($"CharacterSelectionUIManager: OnPetSelected({petIndex}) called - isReady: {isReady}");
        
        if (petIndex < 0 || petIndex >= availablePets.Count) return;
        
        // Check if this is a different selection than current
        bool isSelectionChange = selectedPetIndex != petIndex && selectedPetIndex >= 0;
        
        // Update selection
        selectedPetIndex = petIndex;
        
        // Update visual selection
        UpdateMySelectionVisuals();
        
        // Show pet deck preview
        if (!isReady)
        {
            Debug.Log("CharacterSelectionUIManager: Player not ready, showing pet deck");
            if (isSelectionChange)
            {
                Debug.Log("CharacterSelectionUIManager: Pet selection changed - refreshing deck with animation");
                RefreshPetDeckWithAnimation();
            }
            else
            {
                Debug.Log("CharacterSelectionUIManager: First pet selection - showing deck normally");
                ShowPetDeck();
            }
        }
        else
        {
            Debug.Log("CharacterSelectionUIManager: Player is ready, NOT showing pet deck");
        }
        
        // Update ready state
        UpdateSelectionState();
        
        Debug.Log($"CharacterSelectionUIManager: Selected pet {availablePets[petIndex].PetName}");
    }

    private void UpdateMySelectionVisuals()
    {
        // Update character selection border
        for (int i = 0; i < characterItems.Count; i++)
        {
            if (characterItems[i] != null)
            {
                PlayerSelectionIndicator indicator = characterItems[i].GetComponentInChildren<PlayerSelectionIndicator>();
                if (indicator != null)
                {
                    if (i == selectedCharacterIndex)
                    {
                        indicator.AddPlayerSelection(myPlayerID, myPlayerColor);
                    }
                    else
                    {
                        indicator.RemovePlayerSelection(myPlayerID);
                    }
                }
            }
        }
        
        // Update pet selection border
        for (int i = 0; i < petItems.Count; i++)
        {
            if (petItems[i] != null)
            {
                PlayerSelectionIndicator indicator = petItems[i].GetComponentInChildren<PlayerSelectionIndicator>();
                if (indicator != null)
                {
                    if (i == selectedPetIndex)
                    {
                        indicator.AddPlayerSelection(myPlayerID, myPlayerColor);
                    }
                    else
                    {
                        indicator.RemovePlayerSelection(myPlayerID);
                    }
                }
            }
        }
    }

    private void UpdateSelectionState()
    {
        hasValidSelection = selectedCharacterIndex >= 0 && selectedPetIndex >= 0;
        
        // Update status text
        if (statusText != null)
        {
            if (hasValidSelection)
            {
                string charName = !string.IsNullOrEmpty(customPlayerName) ? customPlayerName : availableCharacters[selectedCharacterIndex].CharacterName;
                string petName = availablePets[selectedPetIndex].PetName;
                statusText.text = $"Selected: {charName} & {petName}";
            }
            else if (selectedCharacterIndex >= 0)
            {
                statusText.text = "Now select a pet...";
            }
            else if (selectedPetIndex >= 0)
            {
                statusText.text = "Now select a character...";
            }
            else
            {
                statusText.text = "Select a character and pet to continue...";
            }
        }
        
        // Update ready button
        UpdateReadyButtonState();
        
        // Send selection to server if valid
        if (hasValidSelection)
        {
            if (selectionManager != null)
            {
                Debug.Log($"CharacterSelectionUIManager: Sending selection to server - Character: {selectedCharacterIndex}, Pet: {selectedPetIndex}, CustomName: '{customPlayerName}'");
                selectionManager.RequestSelectionUpdate(selectedCharacterIndex, selectedPetIndex, customPlayerName, "");
            }
            else
            {
                Debug.LogWarning("CharacterSelectionUIManager: Cannot send selection - selectionManager is null");
            }
        }
    }

    #endregion

    #region Deck Refresh Animation
    
    private void RefreshCharacterDeckWithAnimation()
    {
        if (uiAnimator == null) 
        {
            Debug.LogWarning("CharacterSelectionUIManager: No UI animator available for deck refresh animation");
            ShowCharacterDeck();
            return;
        }
        
        // Hide the current character deck panel
        uiAnimator.HideCharacterDeckPanel();
        
        // Wait a brief moment for hide animation, then update content and show
        StartCoroutine(RefreshCharacterDeckCoroutine());
    }
    
    private void RefreshPetDeckWithAnimation()
    {
        if (uiAnimator == null) 
        {
            Debug.LogWarning("CharacterSelectionUIManager: No UI animator available for deck refresh animation");
            ShowPetDeck();
            return;
        }
        
        // Hide the current pet deck panel
        uiAnimator.HidePetDeckPanel();
        
        // Wait a brief moment for hide animation, then update content and show
        StartCoroutine(RefreshPetDeckCoroutine());
    }
    
    private IEnumerator RefreshCharacterDeckCoroutine()
    {
        // Wait for hide animation to complete (adjust timing as needed)
        yield return new WaitForSeconds(0.3f);
        
        // Update the deck content while hidden
        PopulateCharacterDeckContent();
        
        // Show the deck panel with new content
        uiAnimator.ShowCharacterDeckPanel();
    }
    
    private IEnumerator RefreshPetDeckCoroutine()
    {
        // Wait for hide animation to complete (adjust timing as needed)
        yield return new WaitForSeconds(0.3f);
        
        // Update the deck content while hidden
        PopulatePetDeckContent();
        
        // Show the deck panel with new content
        uiAnimator.ShowPetDeckPanel();
    }
    
    private void PopulateCharacterDeckContent()
    {
        Debug.Log($"CharacterSelectionUIManager: PopulateCharacterDeckContent() called for character index {selectedCharacterIndex}");
        
        // Clear existing character deck preview
        ClearCharacterDeckPreview();
        
        if (selectedCharacterIndex < 0 || selectedCharacterIndex >= availableCharacters.Count)
        {
            Debug.Log($"CharacterSelectionUIManager: Invalid character index {selectedCharacterIndex}, cannot populate deck");
            return;
        }
            
        CharacterData character = availableCharacters[selectedCharacterIndex];
        DeckData deckToShow = character.StarterDeck;
        string deckTitle = $"Character Deck: {character.CharacterName}";
        
        // Update character deck title
        if (characterDeckTitle != null)
        {
            characterDeckTitle.text = deckTitle;
            Debug.Log($"CharacterSelectionUIManager: Set character deck title to: {deckTitle}");
        }
        
        // Create character deck preview items
        if (deckToShow != null && deckToShow.CardsInDeck != null)
        {
            Debug.Log($"CharacterSelectionUIManager: Creating {deckToShow.CardsInDeck.Count} character card previews");
            foreach (CardData card in deckToShow.CardsInDeck)
            {
                if (card != null)
                {
                    GameObject previewItem = CreateDeckPreviewItem(card, characterDeckGridParent);
                    if (previewItem != null)
                    {
                        characterDeckItems.Add(previewItem);
                        Debug.Log($"CharacterSelectionUIManager: Created character card preview for {card.CardName}");
                    }
                }
            }
            Debug.Log($"CharacterSelectionUIManager: Total character deck items created: {characterDeckItems.Count}");
        }
    }
    
    private void PopulatePetDeckContent()
    {
        Debug.Log($"CharacterSelectionUIManager: PopulatePetDeckContent() called for pet index {selectedPetIndex}");
        
        // Clear existing pet deck preview
        ClearPetDeckPreview();
        
        if (selectedPetIndex < 0 || selectedPetIndex >= availablePets.Count)
        {
            Debug.Log($"CharacterSelectionUIManager: Invalid pet index {selectedPetIndex}, cannot populate deck");
            return;
        }
            
        PetData pet = availablePets[selectedPetIndex];
        DeckData deckToShow = pet.StarterDeck;
        string deckTitle = $"Pet Deck: {pet.PetName}";
        
        // Update pet deck title
        if (petDeckTitle != null)
        {
            petDeckTitle.text = deckTitle;
            Debug.Log($"CharacterSelectionUIManager: Set pet deck title to: {deckTitle}");
        }
        
        // Create pet deck preview items
        if (deckToShow != null && deckToShow.CardsInDeck != null)
        {
            Debug.Log($"CharacterSelectionUIManager: Creating {deckToShow.CardsInDeck.Count} pet card previews");
            foreach (CardData card in deckToShow.CardsInDeck)
            {
                if (card != null)
                {
                    GameObject previewItem = CreateDeckPreviewItem(card, petDeckGridParent);
                    if (previewItem != null)
                    {
                        petDeckItems.Add(previewItem);
                        Debug.Log($"CharacterSelectionUIManager: Created pet card preview for {card.CardName}");
                    }
                }
            }
            Debug.Log($"CharacterSelectionUIManager: Total pet deck items created: {petDeckItems.Count}");
        }
    }
    
    #endregion

    #region Deck Preview

    private void ShowIndividualDeckPreview()
    {
        Debug.Log($"CharacterSelectionUIManager: ShowIndividualDeckPreview() called - isReady: {isReady}, selectedCharacterIndex: {selectedCharacterIndex}, selectedPetIndex: {selectedPetIndex}");
        
        if (isReady)
        {
            // Hide all deck previews when ready
            Debug.Log("CharacterSelectionUIManager: Player is READY - HIDING ALL DECK PANELS in ShowIndividualDeckPreview()");
            if (uiAnimator != null)
            {
                uiAnimator.HideAllDeckPanels();
            }
            return;
        }
        
        // Show both decks if both are selected (allow simultaneous preview)
        if (selectedCharacterIndex >= 0)
        {
            Debug.Log($"CharacterSelectionUIManager: Showing character deck for index {selectedCharacterIndex}");
            ShowCharacterDeck();
        }
        
        if (selectedPetIndex >= 0)
        {
            Debug.Log($"CharacterSelectionUIManager: Showing pet deck for index {selectedPetIndex}");
            ShowPetDeck();
        }
    }
    
    private void ShowCharacterDeck()
    {
        Debug.Log($"CharacterSelectionUIManager: ShowCharacterDeck() called for character index {selectedCharacterIndex}");
        
        // Clear existing character deck preview
        ClearCharacterDeckPreview();
        
        if (selectedCharacterIndex < 0 || selectedCharacterIndex >= availableCharacters.Count)
        {
            Debug.Log($"CharacterSelectionUIManager: Invalid character index {selectedCharacterIndex}, cannot show deck");
            return;
        }
            
        CharacterData character = availableCharacters[selectedCharacterIndex];
        DeckData deckToShow = character.StarterDeck;
        string deckTitle = $"Character Deck: {character.CharacterName}";
        
        // Update character deck title
        if (characterDeckTitle != null)
        {
            characterDeckTitle.text = deckTitle;
            Debug.Log($"CharacterSelectionUIManager: Set character deck title to: {deckTitle}");
        }
        
        // Create character deck preview items
        if (deckToShow != null && deckToShow.CardsInDeck != null)
        {
            Debug.Log($"CharacterSelectionUIManager: Creating {deckToShow.CardsInDeck.Count} character card previews");
            foreach (CardData card in deckToShow.CardsInDeck)
            {
                if (card != null)
                {
                    GameObject previewItem = CreateDeckPreviewItem(card, characterDeckGridParent);
                    if (previewItem != null)
                    {
                        characterDeckItems.Add(previewItem);
                        Debug.Log($"CharacterSelectionUIManager: Created character card preview for {card.CardName}");
                    }
                }
            }
            Debug.Log($"CharacterSelectionUIManager: Total character deck items created: {characterDeckItems.Count}");
        }
        
        // Show the character deck panel with animation
        if (uiAnimator != null)
        {
            Debug.Log("CharacterSelectionUIManager: Calling uiAnimator.ShowCharacterDeckPanel()");
            uiAnimator.ShowCharacterDeckPanel();
        }
        else
        {
            Debug.LogError("CharacterSelectionUIManager: uiAnimator is null, cannot show character deck panel");
        }
    }
    
    private void ShowPetDeck()
    {
        Debug.Log($"CharacterSelectionUIManager: ShowPetDeck() called for pet index {selectedPetIndex}");
        
        // Clear existing pet deck preview
        ClearPetDeckPreview();
        
        if (selectedPetIndex < 0 || selectedPetIndex >= availablePets.Count)
        {
            Debug.Log($"CharacterSelectionUIManager: Invalid pet index {selectedPetIndex}, cannot show deck");
            return;
        }
            
        PetData pet = availablePets[selectedPetIndex];
        DeckData deckToShow = pet.StarterDeck;
        string deckTitle = $"Pet Deck: {pet.PetName}";
        
        // Update pet deck title
        if (petDeckTitle != null)
        {
            petDeckTitle.text = deckTitle;
            Debug.Log($"CharacterSelectionUIManager: Set pet deck title to: {deckTitle}");
        }
        
        // Create pet deck preview items
        if (deckToShow != null && deckToShow.CardsInDeck != null)
        {
            Debug.Log($"CharacterSelectionUIManager: Creating {deckToShow.CardsInDeck.Count} pet card previews");
            foreach (CardData card in deckToShow.CardsInDeck)
            {
                if (card != null)
                {
                    GameObject previewItem = CreateDeckPreviewItem(card, petDeckGridParent);
                    if (previewItem != null)
                    {
                        petDeckItems.Add(previewItem);
                        Debug.Log($"CharacterSelectionUIManager: Created pet card preview for {card.CardName}");
                    }
                }
            }
            Debug.Log($"CharacterSelectionUIManager: Total pet deck items created: {petDeckItems.Count}");
        }
        
        // Show the pet deck panel with animation
        if (uiAnimator != null)
        {
            Debug.Log("CharacterSelectionUIManager: Calling uiAnimator.ShowPetDeckPanel()");
            uiAnimator.ShowPetDeckPanel();
        }
        else
        {
            Debug.LogError("CharacterSelectionUIManager: uiAnimator is null, cannot show pet deck panel");
        }
    }

    private void ClearCharacterDeckPreview()
    {
        foreach (GameObject item in characterDeckItems)
        {
            if (item != null) Destroy(item);
        }
        characterDeckItems.Clear();
    }
    
    private void ClearPetDeckPreview()
    {
        foreach (GameObject item in petDeckItems)
        {
            if (item != null) Destroy(item);
        }
        petDeckItems.Clear();
    }
    
    private void ClearAllDeckPreviews()
    {
        ClearCharacterDeckPreview();
        ClearPetDeckPreview();
    }

    private GameObject CreateDeckPreviewItem(CardData cardData, Transform parentTransform)
    {
        // Create basic preview item if no prefab
        if (deckCardPrefab == null)
        {
            return CreateBasicDeckPreviewItem(cardData, parentTransform);
        }
        
        GameObject item = Instantiate(deckCardPrefab, parentTransform);
        
        // IMPORTANT: Remove NetworkObject to prevent network/visibility management interference
        NetworkObject networkObject = item.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            DestroyImmediate(networkObject);
            Debug.Log($"CharacterSelectionUIManager: Removed NetworkObject from preview card {cardData.CardName}");
        }
        
        // Initialize the Card component with the CardData
        Card cardComponent = item.GetComponent<Card>();
        if (cardComponent != null)
        {
            cardComponent.Initialize(cardData);
            Debug.Log($"CharacterSelectionUIManager: Initialized card preview for {cardData.CardName}");
        }
        else
        {
            Debug.LogWarning($"CharacterSelectionUIManager: deckCardPrefab missing Card component for {cardData.CardName}");
            
            // Fallback: Try to set name text manually if no Card component
            TextMeshProUGUI nameText = item.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = cardData.CardName;
            }
        }
        
        // Make sure the card is active and visible for deck preview
        item.SetActive(true);
        
        // Force override any visibility management
        StartCoroutine(ForceCardVisibilityNextFrame(item, cardData.CardName));
        
        // For deck preview, we might want to disable interaction components to prevent dragging/clicking
        CardDragDrop dragDrop = item.GetComponent<CardDragDrop>();
        if (dragDrop != null)
        {
            dragDrop.enabled = false;
            Debug.Log($"  Disabled CardDragDrop component");
        }
        
        // Disable any colliders to prevent unwanted interactions
        Collider2D cardCollider = item.GetComponent<Collider2D>();
        if (cardCollider != null)
        {
            cardCollider.enabled = false;
            Debug.Log($"  Disabled Collider2D component");
        }
        
        Debug.Log($"CharacterSelectionUIManager: Created preview card {cardData.CardName}, active: {item.activeInHierarchy}");
        
        return item;
    }
    
    /// <summary>
    /// Coroutine to force card visibility on the next frame, after any visibility management has run
    /// </summary>
    private System.Collections.IEnumerator ForceCardVisibilityNextFrame(GameObject card, string cardName)
    {
        yield return null; // Wait one frame
        
        if (card != null && !card.activeInHierarchy)
        {
            Debug.LogWarning($"CharacterSelectionUIManager: Card {cardName} was disabled by another system, forcing it back on");
            card.SetActive(true);
        }
        
        // Check again after a short delay
        yield return new WaitForSeconds(0.1f);
        
        if (card != null && !card.activeInHierarchy)
        {
            Debug.LogWarning($"CharacterSelectionUIManager: Card {cardName} disabled again, forcing it back on (second attempt)");
            card.SetActive(true);
        }
    }

    private GameObject CreateBasicDeckPreviewItem(CardData cardData, Transform parentTransform)
    {
        GameObject item = new GameObject(cardData.CardName + "_Preview");
        item.transform.SetParent(parentTransform, false);
        
        RectTransform itemRect = item.AddComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(100f, 140f);
        
        Image itemImage = item.AddComponent<Image>();
        itemImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        
        // Create card name text
        GameObject nameGO = new GameObject("CardName");
        nameGO.transform.SetParent(item.transform, false);
        
        RectTransform nameRect = nameGO.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0);
        nameRect.anchorMax = new Vector2(1, 0.3f);
        nameRect.sizeDelta = Vector2.zero;
        nameRect.anchoredPosition = Vector2.zero;
        
        TextMeshProUGUI nameText = nameGO.AddComponent<TextMeshProUGUI>();
        nameText.text = cardData.CardName;
        nameText.fontSize = 10;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.overflowMode = TextOverflowModes.Ellipsis;
        
        return item;
    }

    #endregion

    #region UI Event Handlers

    private void OnPlayerNameChanged(string newName)
    {
        customPlayerName = newName;
        UpdateSelectionState();
    }

    private void OnReadyButtonClicked()
    {
        if (!hasValidSelection) return;
        
        // Prevent rapid button clicks with cooldown
        float currentTime = Time.time;
        if (currentTime - lastReadyButtonClickTime < READY_BUTTON_COOLDOWN)
        {
            Debug.Log($"CharacterSelectionUIManager: Ready button click ignored due to cooldown ({currentTime - lastReadyButtonClickTime:F2}s since last click)");
            return;
        }
        
        lastReadyButtonClickTime = currentTime;
        
        // Don't change local isReady state immediately - let server be authoritative
        // The server will broadcast the update back to us and we'll update then
        
        if (selectionManager != null)
        {
            Debug.Log("CharacterSelectionUIManager: Sending ready toggle request to server");
            selectionManager.RequestReadyToggle();
        }
    }
    
    private void OnShowPlayersButtonClicked()
    {
        if (uiAnimator != null)
        {
            uiAnimator.TogglePlayerListPanel();
        }
    }
    
    private void OnLeaveGameButtonClicked()
    {
        // Leave the game and return to start screen
        LeaveGame();
    }
    
    private void OnPlayerListCloseButtonClicked()
    {
        if (uiAnimator != null)
        {
            uiAnimator.HidePlayerListPanel();
        }
    }

    private void UpdateReadyButtonState()
    {
        if (readyButton != null)
        {
            readyButton.interactable = hasValidSelection;
            
            Image buttonImage = readyButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = isReady ? readyColor : (hasValidSelection ? selectedColor : notReadyColor);
            }
            
            TextMeshProUGUI buttonText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = isReady ? "Not Ready" : "Ready";
            }
        }
    }

    #endregion

    #region Mario Kart Style Updates

    public void UpdateOtherPlayersSelections(List<PlayerSelectionInfo> playerInfos)
    {
        // Update ready counter
        int readyCount = playerInfos.Count(p => p.isReady);
        int totalCount = playerInfos.Count;
        UpdateReadyCounter(readyCount, totalCount);
        
        // Clear previous player data
        ClearAllPlayerSelections();
        
        // Update visual indicators for each player
        foreach (PlayerSelectionInfo info in playerInfos)
        {
            // Check if this is the local player and update ready state
            if (info.playerName == myPlayerID)
            {
                // Update local ready state from server
                bool wasReady = isReady;
                isReady = info.isReady;
                UpdateReadyButtonState();
                
                // Only hide deck panels if player manually became ready (not from selection changes)
                // We can detect this by checking if we have a valid selection - if we do and we became unready,
                // it's likely due to a selection change on the server, not manual ready toggle
                if (wasReady != isReady)
                {
                    if (isReady)
                    {
                        // Player became ready - always hide deck panels
                        ClearAllDeckPreviews();
                        if (uiAnimator != null)
                            uiAnimator.HideAllDeckPanels();
                        Debug.Log("CharacterSelectionUIManager: Player became ready - hiding deck panels");
                    }
                    else
                    {
                        // Player became unready - only show deck previews if they don't have a valid selection
                        // If they have a valid selection but became unready, it's likely due to server auto-unreadying
                        // on selection change, so we should keep existing deck previews open
                        if (!hasValidSelection)
                        {
                            ShowIndividualDeckPreview();
                            Debug.Log("CharacterSelectionUIManager: Player became not ready (no selection) - showing deck panels");
                        }
                        else
                        {
                            Debug.Log("CharacterSelectionUIManager: Player became not ready (has selection) - keeping existing deck panels");
                        }
                    }
                }
                continue; // Skip showing selection indicators for self
            }
            
            Color playerColor = GetPlayerColor(info.playerName);
            
            // Show character selection
            if (info.hasSelection && info.characterIndex >= 0 && info.characterIndex < characterItems.Count)
            {
                PlayerSelectionIndicator indicator = characterItems[info.characterIndex].GetComponentInChildren<PlayerSelectionIndicator>();
                if (indicator != null)
                {
                    indicator.AddPlayerSelection(info.playerName, playerColor);
                }
            }
            
            // Show pet selection
            if (info.hasSelection && info.petIndex >= 0 && info.petIndex < petItems.Count)
            {
                PlayerSelectionIndicator indicator = petItems[info.petIndex].GetComponentInChildren<PlayerSelectionIndicator>();
                if (indicator != null)
                {
                    indicator.AddPlayerSelection(info.playerName, playerColor);
                }
            }
        }
        
        // Always store the latest player info for when panel becomes visible
        latestPlayerInfos = new List<PlayerSelectionInfo>(playerInfos);
        
        // Update player list panel if visible
        if (uiAnimator != null && uiAnimator.IsPlayerListVisible)
        {
            UpdatePlayerListPanel(playerInfos);
        }
    }

    private void ClearAllPlayerSelections()
    {
        // Clear character selections
        foreach (GameObject item in characterItems)
        {
            if (item != null)
            {
                PlayerSelectionIndicator indicator = item.GetComponentInChildren<PlayerSelectionIndicator>();
                if (indicator != null)
                {
                    indicator.ClearAllExcept(myPlayerID);
                }
            }
        }
        
        // Clear pet selections
        foreach (GameObject item in petItems)
        {
            if (item != null)
            {
                PlayerSelectionIndicator indicator = item.GetComponentInChildren<PlayerSelectionIndicator>();
                if (indicator != null)
                {
                    indicator.ClearAllExcept(myPlayerID);
                }
            }
        }
    }

    private void UpdateReadyCounter(int readyCount, int totalCount)
    {
        if (readyCounterText != null)
        {
            readyCounterText.text = $"{readyCount}/{totalCount} Ready";
            readyCounterText.color = (readyCount == totalCount) ? readyColor : Color.white;
        }
    }

    #endregion
    
    #region Player List Panel
    
    private void UpdatePlayerListPanel(List<PlayerSelectionInfo> playerInfos)
    {
        // Clear existing player list items
        ClearPlayerListItems();
        
        // Create new player list items
        foreach (PlayerSelectionInfo info in playerInfos)
        {
            GameObject playerItem = CreatePlayerListItem(info);
            if (playerItem != null)
            {
                playerListItems.Add(playerItem);
            }
        }
    }
    
    private void ClearPlayerListItems()
    {
        foreach (GameObject item in playerListItems)
        {
            if (item != null) Destroy(item);
        }
        playerListItems.Clear();
    }
    
    private GameObject CreatePlayerListItem(PlayerSelectionInfo info)
    {
        GameObject item = null;
        
        if (playerListItemPrefab != null)
        {
            item = Instantiate(playerListItemPrefab, playerListContent);
        }
        else
        {
            // Create basic player list item if no prefab
            item = CreateBasicPlayerListItem(info);
        }
        
        if (item != null)
        {
            UpdatePlayerListItemContent(item, info);
        }
        
        return item;
    }
    
    private GameObject CreateBasicPlayerListItem(PlayerSelectionInfo info)
    {
        GameObject item = new GameObject(info.playerName + "_ListItem");
        item.transform.SetParent(playerListContent, false);
        
        RectTransform itemRect = item.AddComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(300f, 60f);
        
        Image itemImage = item.AddComponent<Image>();
        itemImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        // Create player info text
        GameObject textGO = new GameObject("PlayerInfo");
        textGO.transform.SetParent(item.transform, false);
        
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        
        TextMeshProUGUI infoText = textGO.AddComponent<TextMeshProUGUI>();
        infoText.fontSize = 12;
        infoText.color = Color.white;
        infoText.alignment = TextAlignmentOptions.Left;
        infoText.margin = new Vector4(10, 5, 10, 5);
        
        return item;
    }
    
    private void UpdatePlayerListItemContent(GameObject item, PlayerSelectionInfo info)
    {
        Debug.Log($"CharacterSelectionUIManager: Updating player list item - Name: '{info.playerName}', HasSelection: {info.hasSelection}, IsReady: {info.isReady}, CharacterName: '{info.characterName}', PetName: '{info.petName}'");
        
        TextMeshProUGUI infoText = item.GetComponentInChildren<TextMeshProUGUI>();
        if (infoText != null)
        {
            string readyStatus = info.isReady ? "<color=green>[Ready]</color>" : "<color=red>[Not Ready]</color>";
            string selectionInfo = "";
            
            if (info.hasSelection)
            {
                selectionInfo = $"\nCharacter: {info.characterName}\nPet: {info.petName}";
                infoText.color = Color.white;
            }
            else
            {
                selectionInfo = "\n<color=yellow>Selecting...</color>";
                infoText.color = Color.gray;
            }
            
            infoText.text = $"{info.playerName} {readyStatus}{selectionInfo}";
        }
        
        // Set background color based on player
        Image itemImage = item.GetComponent<Image>();
        if (itemImage != null)
        {
            Color playerColor = GetPlayerColor(info.playerName);
            itemImage.color = new Color(playerColor.r * 0.3f, playerColor.g * 0.3f, playerColor.b * 0.3f, 0.8f);
        }
    }
    
    private void LeaveGame()
    {
        Debug.Log("CharacterSelectionUIManager: Player requested to leave game");
        
        // Find the SteamNetworkIntegration to handle leaving
        SteamNetworkIntegration steamNetwork = FindFirstObjectByType<SteamNetworkIntegration>();
        if (steamNetwork != null)
        {
            steamNetwork.LeaveLobby();
        }
        
        // Also disconnect from FishNet
        FishNet.InstanceFinder.ClientManager?.StopConnection();
        FishNet.InstanceFinder.ServerManager?.StopConnection(true);
        
        // Return to start screen
        // This would typically be handled by a scene manager or game state manager
        Debug.Log("CharacterSelectionUIManager: Returning to start screen");
        }
    
    private void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (uiAnimator != null)
        {
            uiAnimator.OnPlayerListVisibilityChanged -= OnPlayerListVisibilityChanged;
            uiAnimator.OnCharacterDeckVisibilityChanged -= OnCharacterDeckVisibilityChanged;
            uiAnimator.OnPetDeckVisibilityChanged -= OnPetDeckVisibilityChanged;
        }
    }

    #endregion

    #region External Interface

    public void ShowTransitionMessage(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        
        // Disable interactions during transition
        if (readyButton != null)
        {
            readyButton.interactable = false;
        }
    }

    public void HideCharacterSelectionUI()
    {
        if (characterSelectionCanvas != null)
        {
            characterSelectionCanvas.SetActive(false);
        }
    }

    /// <summary>
    /// Hides any existing lobby UI (compatibility method for transition period)
    /// </summary>
    public void HideLobbyUI()
    {
        // This method exists for compatibility during the transition from separate lobby phase
        // Since we no longer have a separate lobby UI, this method doesn't need to do anything
        Debug.Log("CharacterSelectionUIManager: HideLobbyUI called (no action needed - lobby UI is integrated)");
    }

    #endregion
}

/// <summary>
/// Data structure for player selection display info
/// </summary>
[System.Serializable]
public class PlayerSelectionDisplayInfo
{
    public string playerName;
    public int characterIndex;
    public int petIndex;
    public bool hasSelection;
    public bool isReady;
    public Color playerColor;
}