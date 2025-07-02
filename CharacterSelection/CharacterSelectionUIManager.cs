using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using FishNet.Object;
using FishNet.Connection;

/// <summary>
/// Manages the character selection UI interactions with Mario Kart-style shared selection grids.
/// </summary>
public class CharacterSelectionUIManager : NetworkBehaviour
{
    [Header("UI References - Set by CharacterSelectionCanvasSetup")]
    [SerializeField] private GameObject characterSelectionCanvas;
    [SerializeField] private Button readyButton;
    [SerializeField] private Transform characterGridParent; // Fallback if no sharedGridParent

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
    [SerializeField] private GameObject playerListItemPrefab;
    
    [Header("Portrait Grid Toggle")]
    [SerializeField] private Button charactersToggleButton;
    [SerializeField] private Button petsToggleButton;
    [SerializeField] private Transform sharedGridParent; // Single grid that shows either characters or pets
    
    [Header("Model Spawning")]
    [SerializeField] private Transform selectedModelSpawn; // Shared location for both character and pet models
    
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
    
    // Portrait button lists
    private List<Button> characterPortraitButtons = new List<Button>();
    private List<Button> petPortraitButtons = new List<Button>();
    
    // Spawned models
    private GameObject currentCharacterModel;
    private GameObject currentPetModel;
    
    // UI element lists
    private List<GameObject> playerListItems = new List<GameObject>();
    
    // Grid toggle state
    private bool showingCharacters = true;
    
    // Deck preview controller
    private DeckPreviewController deckPreviewController;
    
    // State
    private bool isReady = false;
    private bool hasValidSelection = false;
    private float lastReadyButtonClickTime = 0f;
    private const float READY_BUTTON_COOLDOWN = 0.2f; // 200ms cooldown
    private bool isInitialized = false; // Prevent multiple initializations
    
    // Public property for EntitySelectionController to check ready state
    public bool IsPlayerReady => isReady;
    
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
        Debug.Log($"[CHAR_SELECT_REVAMP] Initialize() called - isInitialized: {isInitialized}");
        
        if (isInitialized)
        {
            Debug.Log("[CHAR_SELECT_REVAMP] Already initialized, skipping duplicate initialization to prevent panel disruption");
            return;
        }
        
        Debug.Log("[CHAR_SELECT_REVAMP] Starting initialization sequence");
        
        selectionManager = manager;
        availableCharacters = characters;
        availablePets = pets;
        
        // Assign player color and ID
        myPlayerID = GetPlayerID();
        myPlayerColor = GetPlayerColor(myPlayerID);
        
        Debug.Log("[CHAR_SELECT_REVAMP] Setting up UI (Part 1 - Core UI and Animator)...");
        SetupCoreUI();
        
        Debug.Log("[CHAR_SELECT_REVAMP] Initializing deck preview controller...");
        InitializeDeckPreviewController();
        
        Debug.Log("[CHAR_SELECT_REVAMP] Finalizing UI animator setup...");
        FinalizeUIAnimatorSetup();
        
        Debug.Log("[CHAR_SELECT_REVAMP] Setting up UI (Part 2 - Panel setup)...");
        FinishUISetup();
        
        Debug.Log("[CHAR_SELECT_REVAMP] Creating portrait button grids...");
        CreatePortraitButtonGrids();
        
        Debug.Log("[CHAR_SELECT_REVAMP] Making default selections (this will trigger model spawning)...");
        MakeDefaultSelections();
        
        Debug.Log("[CHAR_SELECT_REVAMP] Updating ready button state...");
        UpdateReadyButtonState();
        
        isInitialized = true;
        Debug.Log($"[CHAR_SELECT_REVAMP] Initialized - Player ID: {myPlayerID}, Color: {myPlayerColor}");
    }
    
    /// <summary>
    /// Called when this NetworkBehaviour starts on the server
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("CharacterSelectionUIManager: Started on server");
        
        // Server initialization happens through the normal Initialize() -> CreateSelectionItems() flow
        // No need for additional spawning here since Initialize() handles it
    }
    
    /// <summary>
    /// Called when this NetworkBehaviour starts on the client
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log("CharacterSelectionUIManager: Started on client");
        
        // Reset initialization flag so the UI can be properly initialized for this new session
        isInitialized = false;
        Debug.Log("CharacterSelectionUIManager: Reset isInitialized to false for new client session");
    }
    
    /// <summary>
    /// Server method to ensure selection objects are spawned (called when new clients join)
    /// </summary>
    [Server]
    public void ServerEnsureSelectionObjectsSpawned()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("CharacterSelectionUIManager: Cannot spawn objects - not initialized yet");
            return;
        }
        
        // Check if objects are already spawned
        if (characterPortraitButtons.Count > 0 || petPortraitButtons.Count > 0)
        {
            Debug.Log($"CharacterSelectionUIManager: Selection objects already in lists - Characters: {characterPortraitButtons.Count}, Pets: {petPortraitButtons.Count}");
            return;
        }
        
        // Check if objects exist in the scene but aren't in our lists
        SelectionNetworkObject[] existingObjects = FindObjectsOfType<SelectionNetworkObject>();
        if (existingObjects.Length > 0)
        {
            Debug.Log($"CharacterSelectionUIManager: Found {existingObjects.Length} existing selection objects, registering them");
            DiscoverAndRegisterSpawnedObjects(existingObjects);
            return;
        }
        
        // No objects exist, spawn them
        /* Debug.Log("CharacterSelectionUIManager: Server spawning selection objects for new client"); */
        CreateCharacterItems();
        CreatePetItems();
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
    
    private void SetupCoreUI()
    {
        // Enable the canvas
        if (characterSelectionCanvas != null)
        {
            characterSelectionCanvas.SetActive(true);
        }
        
        // Set up UI animator first (needed by deck preview controller)
        SetupUIAnimator();
        
        // Set up ready button
        if (readyButton != null)
        {
            // Remove any existing listeners to prevent duplicates
            readyButton.onClick.RemoveAllListeners();
            readyButton.onClick.AddListener(OnReadyButtonClicked);
            readyButton.interactable = false;
        }
    }
    
    private void FinishUISetup()
    {
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
        
        // Subscribe to visibility change events
        uiAnimator.OnPlayerListVisibilityChanged += OnPlayerListVisibilityChanged;
        uiAnimator.OnCharacterDeckVisibilityChanged += OnCharacterDeckVisibilityChanged;
        uiAnimator.OnPetDeckVisibilityChanged += OnPetDeckVisibilityChanged;
        
        // Validate grid parent references before setting them
        ValidateGridParentReferences();
        
        // Set up click detection areas using the shared grid parent
        uiAnimator.SetClickDetectionAreas(
            sharedGridParent?.GetComponent<RectTransform>(), 
            sharedGridParent?.GetComponent<RectTransform>()
        );
        
        /* Debug.Log("CharacterSelectionUIManager: UI Animator created and basic setup complete"); */
    }
    
    private void FinalizeUIAnimatorSetup()
    {
        // Initialize the animator with panel references (get deck panels from deck preview controller)
        GameObject characterDeckPanel = deckPreviewController?.GetCharacterDeckPanel();
        GameObject petDeckPanel = deckPreviewController?.GetPetDeckPanel();
        uiAnimator.Initialize(playerListPanel, characterDeckPanel, petDeckPanel);
        
        Debug.Log("CharacterSelectionUIManager: UI Animator fully initialized with panel references");
    }
    
    private void InitializeDeckPreviewController()
    {
        // Get or create the deck preview controller
        deckPreviewController = GetComponent<DeckPreviewController>();
        if (deckPreviewController == null)
        {
            deckPreviewController = gameObject.AddComponent<DeckPreviewController>();
        }
        
        // Initialize the deck preview controller
        deckPreviewController.Initialize(uiAnimator, availableCharacters, availablePets);
        
        Debug.Log("CharacterSelectionUIManager: Deck preview controller initialized");
    }
    
    private void ValidateGridParentReferences()
    {
        // Check and log grid parent reference status
        Debug.Log($"[CHAR_SELECT_REVAMP] Validating grid parent references:");
        Debug.Log($"  - characterGridParent: {characterGridParent?.name ?? "NULL"}");
        Debug.Log($"  - sharedGridParent: {sharedGridParent?.name ?? "NULL"}");
        
        if (characterGridParent == null)
        {
            Debug.LogWarning("[CHAR_SELECT_REVAMP] characterGridParent is null! Using sharedGridParent or transform as fallback.");
        }
        else
        {
            RectTransform rectTransform = characterGridParent.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                Debug.LogError($"[CHAR_SELECT_REVAMP] characterGridParent '{characterGridParent.name}' has no RectTransform component!");
            }
            else
            {
                Debug.Log($"[CHAR_SELECT_REVAMP] characterGridParent '{characterGridParent.name}' has {characterGridParent.childCount} children");
            }
        }
        
        if (sharedGridParent == null)
        {
            Debug.LogWarning("[CHAR_SELECT_REVAMP] sharedGridParent is null! Portrait buttons will use fallback parent and may not layout correctly.");
        }
        else
        {
            RectTransform rectTransform = sharedGridParent.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                Debug.LogError($"[CHAR_SELECT_REVAMP] sharedGridParent '{sharedGridParent.name}' has no RectTransform component!");
            }
            else
            {
                Debug.Log($"[CHAR_SELECT_REVAMP] sharedGridParent '{sharedGridParent.name}' has {sharedGridParent.childCount} children");
                
                GridLayoutGroup gridLayout = sharedGridParent.GetComponent<GridLayoutGroup>();
                if (gridLayout == null)
                {
                    Debug.LogWarning("[CHAR_SELECT_REVAMP] sharedGridParent does not have a GridLayoutGroup component. Portrait buttons may not layout correctly.");
                }
            }
        }
    }
    
    private void OnPlayerListVisibilityChanged(bool isVisible)
    {
        // Handle any additional logic when player list visibility changes
        /* Debug.Log($"CharacterSelectionUIManager: Player list visibility changed to {isVisible}"); */
        
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

    #endregion

    private void CreateSelectionItems()
    {
        Debug.Log("[CHAR_SELECT_REVAMP] Using new portrait button system - skipping old network object spawning");
        
        // Use the new portrait button system instead of the old network object system
        CreatePortraitButtonGrids();
    }

    private void CreateCharacterItems()
    {
        // Clear existing items and controllers
        foreach (Button button in characterPortraitButtons)
        {
            if (button != null) Destroy(button.gameObject);
        }
        characterPortraitButtons.Clear();
        
        // Create character selection items
        for (int i = 0; i < availableCharacters.Count; i++)
        {
            CharacterData character = availableCharacters[i];
            if (character == null) continue;
            
            Button button = Instantiate(selectionItemPrefab, characterGridParent).GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError("CharacterSelectionUIManager: Selection Item Prefab is not a Button!");
                continue;
            }
            
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnCharacterSelectionChanged(i));
            
            characterPortraitButtons.Add(button);
            
            // Set up the item data using the prefab structure
            // Find the name text component
            TextMeshProUGUI nameText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = character.CharacterName;
            }
            
            // Find the portrait image component specifically (not the root image)
            // Look for an Image component that's likely the portrait (not the background)
            Image[] images = button.GetComponentsInChildren<Image>();
            Image portraitImage = null;
            
            // Try to find portrait by name or component order
            foreach (Image img in images)
            {
                // Look for portrait-specific naming or the second image (assuming first is background)
                if (img.gameObject.name.ToLower().Contains("portrait") || 
                    img.gameObject.name.ToLower().Contains("image") ||
                    img != button.GetComponent<Image>()) // Not the root image
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
            if (portraitImage != null && character.CharacterPortrait != null)
            {
                portraitImage.sprite = character.CharacterPortrait;
            }
            else if (character.CharacterPortrait != null)
            {
                Debug.LogWarning($"CharacterSelectionUIManager: Could not find portrait Image component in prefab for {character.CharacterName}");
            }
        }
    }

    private void CreatePetItems()
    {
        // Clear existing items and controllers
        foreach (Button button in petPortraitButtons)
        {
            if (button != null) Destroy(button.gameObject);
        }
        petPortraitButtons.Clear();
        
        // Create pet selection items
        for (int i = 0; i < availablePets.Count; i++)
        {
            PetData pet = availablePets[i];
            if (pet == null) continue;
            
            Button button = Instantiate(selectionItemPrefab, characterGridParent).GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError("CharacterSelectionUIManager: Selection Item Prefab is not a Button!");
                continue;
            }
            
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnPetSelectionChanged(i));
            
            petPortraitButtons.Add(button);
            
            // Set up the item data using the prefab structure
            SetupPortraitButton(button, pet.PetName, pet.PetPortrait);
        }
    }

         public void OnCharacterSelectionChanged(int characterIndex)
     {
         if (characterIndex < 0 || characterIndex >= availableCharacters.Count) return;
         
         // Prevent re-selecting the same character (no action if already selected)
         if (characterIndex == selectedCharacterIndex)
         {
             Debug.Log($"[CHAR_SELECT_REVAMP] Character {availableCharacters[characterIndex].CharacterName} already selected, ignoring duplicate selection");
             return;
         }
         
         Debug.Log($"[CHAR_SELECT_REVAMP] Character selection changed to {availableCharacters[characterIndex].CharacterName}");
         
         // Update selection
         selectedCharacterIndex = characterIndex;
         
         // Request model transition (don't create model immediately - let animation system handle it)
         RequestCharacterModelTransition(characterIndex);
         
         // Update visual selection
         UpdateMySelectionVisuals();
         
         // Update deck preview
         if (deckPreviewController != null)
         {
             deckPreviewController.SetCurrentCharacterIndex(characterIndex);
             deckPreviewController.ShowCharacterDeck(characterIndex, isReady);
         }
         
         // Update selection state
         UpdateSelectionState();
     }

         public void OnPetSelectionChanged(int petIndex)
     {
         if (petIndex < 0 || petIndex >= availablePets.Count) return;
         
         // Prevent re-selecting the same pet (no action if already selected)
         if (petIndex == selectedPetIndex)
         {
             Debug.Log($"[CHAR_SELECT_REVAMP] Pet {availablePets[petIndex].PetName} already selected, ignoring duplicate selection");
             return;
         }
         
         Debug.Log($"[CHAR_SELECT_REVAMP] Pet selection changed to {availablePets[petIndex].PetName}");
         
         // Update selection
         selectedPetIndex = petIndex;
         
         // Request model transition (don't create model immediately - let animation system handle it)
         RequestPetModelTransition(petIndex);
         
         // Update visual selection
         UpdateMySelectionVisuals();
         
         // Update deck preview
         if (deckPreviewController != null)
         {
             deckPreviewController.SetCurrentPetIndex(petIndex);
             deckPreviewController.ShowPetDeck(petIndex, isReady);
         }
         
         // Update selection state
         UpdateSelectionState();
     }

    private void UpdateMySelectionVisuals()
    {
        // Update character selection border using controllers
        for (int i = 0; i < characterPortraitButtons.Count; i++)
        {
            Button button = characterPortraitButtons[i];
            if (button != null)
            {
                if (i == selectedCharacterIndex)
                {
                    button.image.color = selectedColor;
            }
            else
            {
                    button.image.color = unselectedColor;
                }
            }
        }
        
        // Update pet selection border using controllers
        for (int i = 0; i < petPortraitButtons.Count; i++)
        {
            Button button = petPortraitButtons[i];
            if (button != null)
            {
                if (i == selectedPetIndex)
                {
                    button.image.color = selectedColor;
                }
                else
                {
                    button.image.color = unselectedColor;
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
                /* Debug.Log($"CharacterSelectionUIManager: Sending selection to server - Character: {selectedCharacterIndex}, Pet: {selectedPetIndex}, CustomName: '{customPlayerName}'"); */
                selectionManager.RequestSelectionUpdate(selectedCharacterIndex, selectedPetIndex, customPlayerName, "");
            }
            else
            {
                Debug.LogWarning("CharacterSelectionUIManager: Cannot send selection - selectionManager is null");
            }
        }
    }

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

    private void LeaveGame()
    {
        Debug.Log("CharacterSelectionUIManager: Player requested to leave game");
        
        // Find the SteamNetworkIntegration to handle graceful disconnection and return to start
        SteamNetworkIntegration steamNetwork = SteamNetworkIntegration.Instance;
        if (steamNetwork != null)
        {
            // Use the comprehensive disconnect and return to start method
            steamNetwork.DisconnectAndReturnToStart();
        }
        else
        {
            Debug.LogError("CharacterSelectionUIManager: SteamNetworkIntegration.Instance not found, cannot gracefully disconnect");
            
            // Fallback: basic disconnect without proper transition
            FishNet.InstanceFinder.ClientManager?.StopConnection();
            FishNet.InstanceFinder.ServerManager?.StopConnection(true);
            
            // Try to transition through GamePhaseManager as fallback
            GamePhaseManager gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
            if (gamePhaseManager != null)
            {
                gamePhaseManager.SetStartPhase();
            }
        }
    }

    private void OnDestroy()
    {
        // Clean up selection models to prevent memory leaks
        CleanupSelectionModels();
        
        // Unsubscribe from events to prevent memory leaks
        if (uiAnimator != null)
        {
            uiAnimator.OnPlayerListVisibilityChanged -= OnPlayerListVisibilityChanged;
            uiAnimator.OnCharacterDeckVisibilityChanged -= OnCharacterDeckVisibilityChanged;
            uiAnimator.OnPetDeckVisibilityChanged -= OnPetDeckVisibilityChanged;
        }
    }

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
        
        // Clean up character selection models to free memory
        CleanupSelectionModels();
    }
    
    /// <summary>
    /// Cleans up all instantiated character and pet selection models to free memory
    /// Called automatically during combat transition and UI destruction.
    /// This system ensures that character selection models don't persist into combat,
    /// preventing memory leaks and visual conflicts.
    /// </summary>
    public void CleanupSelectionModels()
    {
        Debug.Log($"[CHAR_SELECT_REVAMP] Cleaning up {characterPortraitButtons.Count} character buttons and {petPortraitButtons.Count} pet buttons, plus spawned models");
        
        // Clean up spawned character and pet models
        if (currentCharacterModel != null)
        {
            Debug.Log($"[CHAR_SELECT_REVAMP] Destroying spawned character model: {currentCharacterModel.name}");
            Destroy(currentCharacterModel);
            currentCharacterModel = null;
        }
        
        if (currentPetModel != null)
        {
            Debug.Log($"[CHAR_SELECT_REVAMP] Destroying spawned pet model: {currentPetModel.name}");
            Destroy(currentPetModel);
            currentPetModel = null;
        }
        
        // Clean up deck preview cards to prevent NetworkTransform errors
        if (deckPreviewController != null)
        {
            deckPreviewController.ClearAllDeckPreviews();
            Debug.Log("[CHAR_SELECT_REVAMP] Cleared all deck preview cards");
        }
        
        // Cleanup character portrait buttons
        foreach (Button button in characterPortraitButtons)
        {
            if (button != null)
            {
                Debug.Log($"[CHAR_SELECT_REVAMP] Cleaning up character portrait button: {button.gameObject.name}");
                Destroy(button.gameObject);
            }
        }
        characterPortraitButtons.Clear();
        
        // Cleanup pet portrait buttons
        foreach (Button button in petPortraitButtons)
        {
            if (button != null)
            {
                Debug.Log($"[CHAR_SELECT_REVAMP] Cleaning up pet portrait button: {button.gameObject.name}");
                Destroy(button.gameObject);
            }
        }
        petPortraitButtons.Clear();
        
        // Reset initialization flag so the UI can be properly initialized again when rejoining
        isInitialized = false;
        
        Debug.Log("[CHAR_SELECT_REVAMP] Selection model cleanup complete");
    }
    
    /// <summary>
    /// Forces cleanup on all entity selection controllers before destroying them
    /// </summary>
    private void ForceCleanupAllControllers()
    {
        // Cleanup character controllers
        foreach (Button button in characterPortraitButtons)
        {
            if (button != null)
            {
                Debug.Log($"CharacterSelectionUIManager: Force cleaning model from character controller: {button.gameObject.name}");
            }
        }
        
        // Cleanup pet controllers
        foreach (Button button in petPortraitButtons)
        {
            if (button != null)
            {
                Debug.Log($"CharacterSelectionUIManager: Force cleaning model from pet controller: {button.gameObject.name}");
            }
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
    
    /// <summary>
    /// Checks if the network manager is ready for spawning
    /// </summary>
    private bool IsNetworkManagerReady()
    {
        if (FishNet.InstanceFinder.NetworkManager == null)
        {
            Debug.LogWarning("CharacterSelectionUIManager: NetworkManager not found");
            return false;
        }
        
        if (!FishNet.InstanceFinder.NetworkManager.IsServerStarted && !FishNet.InstanceFinder.NetworkManager.IsClientStarted)
        {
            Debug.LogWarning("CharacterSelectionUIManager: Network not started");
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Waits for the network to be ready before creating selection items
    /// </summary>
    private System.Collections.IEnumerator WaitForNetworkAndCreateItems()
    {
        /* Debug.Log("CharacterSelectionUIManager: Waiting for network to be ready..."); */
        
        // Wait for network manager to be available
        while (FishNet.InstanceFinder.NetworkManager == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        // Wait for server or client to start
        while (!FishNet.InstanceFinder.NetworkManager.IsServerStarted && !FishNet.InstanceFinder.NetworkManager.IsClientStarted)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        // Additional wait to ensure everything is properly initialized
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log("CharacterSelectionUIManager: Network ready, creating selection items");
        
        // Now create the selection items
        CreateSelectionItems();
    }
    
    /// <summary>
    /// Client waits for server to spawn selection objects and discovers them
    /// </summary>
    private System.Collections.IEnumerator WaitForServerSpawnedObjects()
    {
        /* Debug.Log("CharacterSelectionUIManager: Client waiting for server-spawned selection objects..."); */
        
        float timeout = 5f; // 5 second timeout
        float elapsed = 0f;
        
        while (elapsed < timeout)
        {
            // First look for existing SelectionNetworkObject components
            SelectionNetworkObject[] existingSelectionObjects = FindObjectsOfType<SelectionNetworkObject>();
            
            if (existingSelectionObjects.Length > 0)
            {
                Debug.Log($"CharacterSelectionUIManager: Client found {existingSelectionObjects.Length} existing SelectionNetworkObject components");
                DiscoverAndRegisterSpawnedObjects(existingSelectionObjects);
                yield break;
            }
            
            // If no SelectionNetworkObject components found, look for NetworkObjects that match our prefabs
            FishNet.Object.NetworkObject[] allNetworkObjects = FindObjectsOfType<FishNet.Object.NetworkObject>();
            List<SelectionNetworkObject> discoveredObjects = new List<SelectionNetworkObject>();
            
            foreach (var networkObj in allNetworkObjects)
            {
                GameObject obj = networkObj.gameObject;
                
                // Skip if this already has a SelectionNetworkObject component
                if (obj.GetComponent<SelectionNetworkObject>() != null)
                    continue;
                
                // Check if this object matches any of our character or pet prefabs by name
                bool isCharacterPrefab = false;
                bool isPetPrefab = false;
                int prefabIndex = -1;
                
                // Check against character prefabs
                for (int i = 0; i < selectionManager.GetAvailableCharacterPrefabs().Count; i++)
                {
                    GameObject prefab = selectionManager.GetAvailableCharacterPrefabs()[i];
                    if (prefab != null && obj.name.Contains(prefab.name.Replace("(Clone)", "")))
                    {
                        isCharacterPrefab = true;
                        prefabIndex = i;
                        break;
                    }
                }
                
                // Check against pet prefabs if not a character
                if (!isCharacterPrefab)
                {
                    for (int i = 0; i < selectionManager.GetAvailablePetPrefabs().Count; i++)
                    {
                        GameObject prefab = selectionManager.GetAvailablePetPrefabs()[i];
                        if (prefab != null && obj.name.Contains(prefab.name.Replace("(Clone)", "")))
                        {
                            isPetPrefab = true;
                            prefabIndex = i;
                            break;
                        }
                    }
                }
                
                // If we found a matching prefab, add SelectionNetworkObject component and configure it
                if (isCharacterPrefab || isPetPrefab)
                {
                    SelectionNetworkObject selectionComponent = obj.AddComponent<SelectionNetworkObject>();
                    selectionComponent.isCharacterSelectionObject = true;
                    selectionComponent.selectionIndex = prefabIndex;
                    selectionComponent.isCharacter = isCharacterPrefab;
                    
                    discoveredObjects.Add(selectionComponent);
                    /* Debug.Log($"CharacterSelectionUIManager: Discovered and tagged NetworkObject {obj.name} as {(isCharacterPrefab ? "character" : "pet")} at index {prefabIndex}"); */
                }
            }
            
            // If we discovered enough objects, use them
            int expectedCount = (availableCharacters?.Count ?? 0) + (availablePets?.Count ?? 0);
            if (discoveredObjects.Count >= expectedCount)
            {
                /* Debug.Log($"CharacterSelectionUIManager: Client discovered {discoveredObjects.Count} NetworkObjects matching our prefabs"); */
                DiscoverAndRegisterSpawnedObjects(discoveredObjects.ToArray());
                yield break;
            }
            
            if (expectedCount > 0)
            {
                /* Debug.Log($"CharacterSelectionUIManager: Still waiting... Expected {expectedCount} objects, found {discoveredObjects.Count} NetworkObjects"); */
            }
            
            elapsed += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }
        
        Debug.LogWarning("CharacterSelectionUIManager: Client timed out waiting for server-spawned selection objects. Creating local fallback objects.");
        // Fallback to creating local objects (non-NetworkObject versions)
        CreateCharacterItems();
        CreatePetItems();
    }
    
    /// <summary>
    /// Discovers and registers server-spawned selection objects on the client
    /// </summary>
    private void DiscoverAndRegisterSpawnedObjects(SelectionNetworkObject[] spawnedObjects)
    {
        /* Debug.Log($"CharacterSelectionUIManager: Client registering {spawnedObjects.Length} discovered selection objects"); */
        
        // Clear any existing items
        characterPortraitButtons.Clear();
        petPortraitButtons.Clear();
        
        // Sort objects by type and index
        System.Array.Sort(spawnedObjects, (a, b) => 
        {
            if (a.isCharacter != b.isCharacter)
                return a.isCharacter ? -1 : 1;
            return a.selectionIndex.CompareTo(b.selectionIndex);
        });
        
        foreach (SelectionNetworkObject selectionObj in spawnedObjects)
        {
            GameObject item = selectionObj.gameObject;
            
            // CRITICAL: Set the correct parent for this object on the client
            Transform targetParent = selectionObj.isCharacter ? 
                (characterGridParent != null ? characterGridParent : transform) : 
                (characterGridParent != null ? characterGridParent : transform);
            
            if (targetParent != null && item.transform.parent != targetParent)
            {
                // Parent the object to the correct UI hierarchy position
                item.transform.SetParent(targetParent, false);
                /* Debug.Log($"CharacterSelectionUIManager: Parented {item.name} to {targetParent.name}"); */
            }
            
            // Ensure the item has an EntitySelectionController
            EntitySelectionController controller = item.GetComponent<EntitySelectionController>();
            if (controller == null)
            {
                controller = item.AddComponent<EntitySelectionController>();
            }
            
            // Set the prefab's 3D model as the selection target
            controller.SetModel3D(item);
            
            if (selectionObj.isCharacter)
            {
                // Initialize as character controller
                if (selectionObj.selectionIndex < availableCharacters.Count)
                {
                    controller.InitializeWithCharacter(availableCharacters[selectionObj.selectionIndex], 
                                                     selectionObj.selectionIndex, this, deckPreviewController, selectionManager);
                }
                characterPortraitButtons.Add(item.GetComponent<Button>());
                
                // Position the model correctly
                Position3DModel(item, selectionObj.selectionIndex, true);
            }
            else
            {
                // Initialize as pet controller
                if (selectionObj.selectionIndex < availablePets.Count)
                {
                    controller.InitializeWithPet(availablePets[selectionObj.selectionIndex], 
                                                selectionObj.selectionIndex, this, deckPreviewController, selectionManager);
                }
                petPortraitButtons.Add(item.GetComponent<Button>());
                
                // Position the model correctly
                Position3DModel(item, selectionObj.selectionIndex, false);
            }
            
            /* Debug.Log($"CharacterSelectionUIManager: Client registered {(selectionObj.isCharacter ? "character" : "pet")} selection object at index {selectionObj.selectionIndex}"); */
        }
        
        /* Debug.Log($"CharacterSelectionUIManager: Client registration complete - {characterPortraitButtons.Count} characters, {petPortraitButtons.Count} pets"); */
    }
    
    /// <summary>
    /// Positions a 3D model based on the current positioning mode
    /// </summary>
    private void Position3DModel(GameObject modelItem, int index, bool isCharacter)
    {
        if (modelItem == null) return;
        
        Vector3 position;
        
        // Implement the logic to position the model based on the current mode
        // This is a placeholder and should be replaced with the actual implementation
        position = Vector3.zero; // Placeholder position
        
        // Set the position
        modelItem.transform.position = position;
        
        /* Debug.Log($"CharacterSelectionUIManager: Positioned {(isCharacter ? "character" : "pet")} model {index} at {position} using {positioningMode} mode"); */
    }
    
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
        /* Debug.Log($"CharacterSelectionUIManager: Updating player list item - Name: '{info.playerName}', HasSelection: {info.hasSelection}, IsReady: {info.isReady}, CharacterName: '{info.characterName}', PetName: '{info.petName}'"); */
        
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
    
    #endregion

    private void CreatePortraitButtonGrids()
    {
        Debug.Log($"[CHAR_SELECT_REVAMP] CreatePortraitButtonGrids() called");
        
        // Clear existing grids if any
        CleanupPortraitGrids();
        
        // Setup toggle button listeners
        SetupToggleButtons();
        
        // Create character portrait buttons
        CreateCharacterPortraitButtons();
        
        // Create pet portrait buttons
        CreatePetPortraitButtons();
        
        // Ensure we use the shared grid parent if specified, otherwise fall back to original parents
        SetupSharedGridParent();
        
        // Set initial display state (showing characters)
        SetGridDisplayState(true);
        
        Debug.Log($"[CHAR_SELECT_REVAMP] Portrait grids created - Characters: {characterPortraitButtons.Count}, Pets: {petPortraitButtons.Count}");
    }
    
    private void CleanupPortraitGrids()
    {
        Debug.Log($"[CHAR_SELECT_REVAMP] Cleaning up existing portrait grids");
        
        foreach (Button button in characterPortraitButtons)
        {
            if (button != null) Destroy(button.gameObject);
        }
        characterPortraitButtons.Clear();
        
        foreach (Button button in petPortraitButtons)
        {
            if (button != null) Destroy(button.gameObject);
        }
        petPortraitButtons.Clear();
    }
    
    private void SetupToggleButtons()
    {
        Debug.Log($"[CHAR_SELECT_REVAMP] Setting up toggle buttons");
        
        if (charactersToggleButton != null)
        {
            charactersToggleButton.onClick.RemoveAllListeners();
            charactersToggleButton.onClick.AddListener(() => OnToggleGridDisplay(true));
        }
        
        if (petsToggleButton != null)
        {
            petsToggleButton.onClick.RemoveAllListeners();
            petsToggleButton.onClick.AddListener(() => OnToggleGridDisplay(false));
        }
    }
    
    private void OnToggleGridDisplay(bool showCharacters)
    {
        Debug.Log($"[CHAR_SELECT_REVAMP] Toggling grid display to show {(showCharacters ? "characters" : "pets")}");
        showingCharacters = showCharacters;
        SetGridDisplayState(showCharacters);
        
        // Request appropriate model for the current view (only if needed)
        if (showCharacters && selectedCharacterIndex >= 0)
        {
            // Only request character model if we don't already have one or it's not the right one
            bool needsNewCharacterModel = currentCharacterModel == null || 
                !currentCharacterModel.name.Contains(availableCharacters[selectedCharacterIndex].CharacterName);
                
            if (needsNewCharacterModel)
            {
                Debug.Log($"[CHAR_SELECT_REVAMP] Switching to character view - requesting character model for index {selectedCharacterIndex}");
                RequestCharacterModelTransition(selectedCharacterIndex);
            }
            else
            {
                Debug.Log($"[CHAR_SELECT_REVAMP] Character model already exists for {availableCharacters[selectedCharacterIndex].CharacterName}, no transition needed");
            }
        }
        else if (!showCharacters && selectedPetIndex >= 0)
        {
            // Only request pet model if we don't already have one or it's not the right one
            bool needsNewPetModel = currentPetModel == null || 
                !currentPetModel.name.Contains(availablePets[selectedPetIndex].PetName);
                
            if (needsNewPetModel)
            {
                Debug.Log($"[CHAR_SELECT_REVAMP] Switching to pet view - requesting pet model for index {selectedPetIndex}");
                RequestPetModelTransition(selectedPetIndex);
            }
            else
            {
                Debug.Log($"[CHAR_SELECT_REVAMP] Pet model already exists for {availablePets[selectedPetIndex].PetName}, no transition needed");
            }
        }
    }
    
    private void SetGridDisplayState(bool showCharacters)
    {
        Debug.Log($"[CHAR_SELECT_REVAMP] Setting grid display state - showing characters: {showCharacters}");
        
        // Show/hide character buttons
        foreach (Button button in characterPortraitButtons)
        {
            if (button != null) button.gameObject.SetActive(showCharacters);
        }
        
        // Show/hide pet buttons
        foreach (Button button in petPortraitButtons)
        {
            if (button != null) button.gameObject.SetActive(!showCharacters);
        }
        
        // Update toggle button states
        UpdateToggleButtonVisuals(showCharacters);
    }
    
    private void UpdateToggleButtonVisuals(bool showingCharacters)
    {
        if (charactersToggleButton != null)
        {
            Image charButtonImage = charactersToggleButton.GetComponent<Image>();
            if (charButtonImage != null)
            {
                charButtonImage.color = showingCharacters ? selectedColor : unselectedColor;
            }
        }
        
        if (petsToggleButton != null)
        {
            Image petButtonImage = petsToggleButton.GetComponent<Image>();
            if (petButtonImage != null)
            {
                petButtonImage.color = !showingCharacters ? selectedColor : unselectedColor;
            }
        }
    }

    private void MakeDefaultSelections()
    {
        Debug.Log($"[CHAR_SELECT_REVAMP] MakeDefaultSelections() called - availableCharacters: {availableCharacters.Count}, availablePets: {availablePets.Count}");
        
        // Auto-select first character and first pet if available (but only spawn character model due to mutual exclusivity)
        if (availableCharacters.Count > 0)
        {
            Debug.Log($"[CHAR_SELECT_REVAMP] Auto-selecting first character: {availableCharacters[0].CharacterName}");
            
            // For the initial selection, create the model directly without animation
            selectedCharacterIndex = 0;
            GameObject initialCharacterModel = CreateCharacterModel(0);
            if (initialCharacterModel != null)
            {
                currentCharacterModel = initialCharacterModel;
                HandleModelVisibility(currentCharacterModel, true);
                Debug.Log($"[CHAR_SELECT_REVAMP] Created initial character model: {initialCharacterModel.name}");
            }
            
            // Update visual selection and deck preview
            UpdateMySelectionVisuals();
                if (deckPreviewController != null)
                {
                    deckPreviewController.SetCurrentCharacterIndex(0);
                    deckPreviewController.ShowCharacterDeck(0, isReady);
                }
        }
        else
        {
            Debug.LogError("[CHAR_SELECT_REVAMP] No available characters to auto-select! Check if character data is properly loaded.");
        }
        
        if (availablePets.Count > 0)
        {
            Debug.Log($"[CHAR_SELECT_REVAMP] Auto-selecting first pet: {availablePets[0].PetName}");
            selectedPetIndex = 0;
                
            // NOTE: Don't spawn pet model here due to mutual exclusivity with character model
            // The pet model will be shown when user toggles to pet view or selects a different pet
            Debug.Log($"[CHAR_SELECT_REVAMP] Pet selection registered but model not spawned (character model has priority by default)");
                
            // Set up deck preview data (but don't show it since character deck is shown)
                if (deckPreviewController != null)
                {
                    deckPreviewController.SetCurrentPetIndex(0);
                // Note: ShowPetDeck not called here - character deck has priority by default
            }
        }
        
        // Update selection state and send to server
        UpdateSelectionState();
        
        // Check if auto-test runner wants us to auto-ready
        AutoTestRunner autoTestRunner = FindFirstObjectByType<AutoTestRunner>();
        if (autoTestRunner != null && autoTestRunner.enableAutoTesting)
        {
            Debug.Log("[CHAR_SELECT_REVAMP] Auto-test runner detected, will auto-ready after delay");
            StartCoroutine(AutoReadyAfterDelay());
        }
    }
    
    private System.Collections.IEnumerator AutoReadyAfterDelay()
    {
        yield return new WaitForSeconds(0.5f); // Allow UI to update
        
        if (hasValidSelection)
        {
            Debug.Log("[CHAR_SELECT_REVAMP] Auto-ready triggered by AutoTestRunner");
            OnReadyButtonClicked();
        }
    }

    private void SpawnCharacterModel(int characterIndex)
    {
        Debug.Log($"[CHAR_SELECT_REVAMP] SpawnCharacterModel({characterIndex}) called");
        
        if (characterIndex < 0 || characterIndex >= availableCharacters.Count)
        {
            Debug.LogWarning($"[CHAR_SELECT_REVAMP] Invalid character index for model spawning: {characterIndex} (available: 0-{availableCharacters.Count - 1})");
            return;
        }
        
        if (selectedModelSpawn == null)
        {
            Debug.LogError("[CHAR_SELECT_REVAMP] selectedModelSpawn is null - CANNOT SPAWN CHARACTER MODEL! Please assign the selectedModelSpawn Transform in the inspector.");
            return;
        }
        
        // Check if we already have the correct character model
        string targetCharacterName = availableCharacters[characterIndex].CharacterName;
        if (currentCharacterModel != null && currentCharacterModel.name.Contains(targetCharacterName))
        {
            Debug.Log($"[CHAR_SELECT_REVAMP] Character model for {targetCharacterName} already exists, skipping spawn");
            return;
        }
        
        // Get the character prefab from selection manager
        if (selectionManager == null)
        {
            Debug.LogError("[CHAR_SELECT_REVAMP] Selection manager is null - cannot get character prefab");
            return;
        }
        
        Debug.Log($"[CHAR_SELECT_REVAMP] Getting character prefab for index {characterIndex} from selection manager...");
        GameObject characterPrefab = selectionManager.GetCharacterPrefabByIndex(characterIndex);
        if (characterPrefab == null)
        {
            Debug.LogError($"[CHAR_SELECT_REVAMP] No character prefab found for index: {characterIndex}. Check if CharacterSelectionManager has character prefabs assigned.");
            return;
        }
        
        Debug.Log($"[CHAR_SELECT_REVAMP] Found character prefab: {characterPrefab.name}");
        
        // Create the new character model
        GameObject newCharacterModel = Instantiate(characterPrefab, selectedModelSpawn);
        newCharacterModel.name = $"SelectedCharacter_{availableCharacters[characterIndex].CharacterName}";
        
        Debug.Log($"[CHAR_SELECT_REVAMP] Created character model: {newCharacterModel.name} at {selectedModelSpawn.position}");
        
        // Determine what old model to animate out (character or pet - mutual exclusivity)
        GameObject oldModel = currentCharacterModel != null ? currentCharacterModel : currentPetModel;
        
        // Use the UI animator for smooth transition if available
        if (uiAnimator != null)
        {
            Debug.Log($"[CHAR_SELECT_REVAMP] Starting animated transition - Old: {oldModel?.name ?? "None"}, New: {newCharacterModel.name}");
            
            // Hide the new model initially so the animation can control its appearance
            PrepareModelForAnimation(newCharacterModel);
            
            uiAnimator.AnimateModelTransition(oldModel, newCharacterModel, () => {
                // Callback when animation completes
                Debug.Log($"[CHAR_SELECT_REVAMP] Character model transition completed");
                
                // Clean up old model references
                if (currentCharacterModel != null && currentCharacterModel != newCharacterModel)
                {
                    Destroy(currentCharacterModel);
                }
                if (currentPetModel != null)
                {
                    Destroy(currentPetModel);
                    currentPetModel = null;
                }
                
                // Set new model as current
                currentCharacterModel = newCharacterModel;
            });
        }
        else
        {
            // Fallback to instant spawning if no animator available
            Debug.LogWarning("[CHAR_SELECT_REVAMP] No UI animator available - using instant spawning fallback");
            
            // Clean up existing models instantly
            if (currentCharacterModel != null)
            {
                Debug.Log($"[CHAR_SELECT_REVAMP] Destroying existing character model: {currentCharacterModel.name}");
                Destroy(currentCharacterModel);
            }
            if (currentPetModel != null)
            {
                Debug.Log($"[CHAR_SELECT_REVAMP] Destroying existing pet model for character exclusivity: {currentPetModel.name}");
                Destroy(currentPetModel);
                currentPetModel = null;
            }
            
            currentCharacterModel = newCharacterModel;
            HandleModelVisibility(currentCharacterModel, true);
        }
    }
    
    private void SpawnPetModel(int petIndex)
    {
        if (petIndex < 0 || petIndex >= availablePets.Count)
        {
            Debug.LogWarning($"[CHAR_SELECT_REVAMP] Invalid pet index for model spawning: {petIndex}");
            return;
        }
        
        if (selectedModelSpawn == null)
        {
            Debug.LogWarning("[CHAR_SELECT_REVAMP] selectedModelSpawn is null - cannot spawn pet model");
            return;
        }
        
        // Check if we already have the correct pet model
        string targetPetName = availablePets[petIndex].PetName;
        if (currentPetModel != null && currentPetModel.name.Contains(targetPetName))
        {
            Debug.Log($"[CHAR_SELECT_REVAMP] Pet model for {targetPetName} already exists, skipping spawn");
            return;
        }
        
        // Get the pet prefab from selection manager
        if (selectionManager == null)
        {
            Debug.LogWarning("[CHAR_SELECT_REVAMP] Selection manager is null - cannot get pet prefab");
            return;
        }
        
        GameObject petPrefab = selectionManager.GetPetPrefabByIndex(petIndex);
        if (petPrefab == null)
        {
            Debug.LogWarning($"[CHAR_SELECT_REVAMP] No pet prefab found for index: {petIndex}");
            return;
        }
        
        // Create the new pet model
        GameObject newPetModel = Instantiate(petPrefab, selectedModelSpawn);
        newPetModel.name = $"SelectedPet_{availablePets[petIndex].PetName}";
        
        Debug.Log($"[CHAR_SELECT_REVAMP] Created pet model: {newPetModel.name} at {selectedModelSpawn.position}");
        
        // Determine what old model to animate out (pet or character - mutual exclusivity)
        GameObject oldModel = currentPetModel != null ? currentPetModel : currentCharacterModel;
        
        // Use the UI animator for smooth transition if available
        if (uiAnimator != null)
        {
            Debug.Log($"[CHAR_SELECT_REVAMP] Starting animated transition - Old: {oldModel?.name ?? "None"}, New: {newPetModel.name}");
            
            // Hide the new model initially so the animation can control its appearance
            PrepareModelForAnimation(newPetModel);
            
            uiAnimator.AnimateModelTransition(oldModel, newPetModel, () => {
                // Callback when animation completes
                Debug.Log($"[CHAR_SELECT_REVAMP] Pet model transition completed");
                
                // Clean up old model references
                if (currentPetModel != null && currentPetModel != newPetModel)
                {
                    Destroy(currentPetModel);
                }
                if (currentCharacterModel != null)
                {
                    Destroy(currentCharacterModel);
                    currentCharacterModel = null;
                }
                
                // Set new model as current
                currentPetModel = newPetModel;
            });
                }
                else
                {
            // Fallback to instant spawning if no animator available
            Debug.LogWarning("[CHAR_SELECT_REVAMP] No UI animator available - using instant spawning fallback");
            
            // Clean up existing models instantly
            if (currentPetModel != null)
            {
                Debug.Log($"[CHAR_SELECT_REVAMP] Destroying existing pet model: {currentPetModel.name}");
                Destroy(currentPetModel);
            }
            if (currentCharacterModel != null)
            {
                Debug.Log($"[CHAR_SELECT_REVAMP] Destroying existing character model for pet exclusivity: {currentCharacterModel.name}");
                Destroy(currentCharacterModel);
                currentCharacterModel = null;
            }
            
            currentPetModel = newPetModel;
            HandleModelVisibility(currentPetModel, false);
        }
    }
    
    /// <summary>
    /// Prepares a newly created model for animation by making it initially invisible
    /// This prevents visual conflicts when the animation system takes control
    /// </summary>
    private void PrepareModelForAnimation(GameObject model)
    {
        if (model == null) return;
        
        Debug.Log($"[CHAR_SELECT_REVAMP] Preparing model for animation: {model.name}");
        
        // Make all renderers initially invisible so the animation can control the appearance
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }
        
        // Keep the GameObject active but invisible through renderer control
        model.SetActive(true);
        
        Debug.Log($"[CHAR_SELECT_REVAMP] Model {model.name} prepared for animation - {renderers.Length} renderers disabled, awaiting animation");
    }

    private void RequestCharacterModelTransition(int characterIndex)
    {
        Debug.Log($"[CHAR_SELECT_REVAMP] Requesting character model transition to index {characterIndex}");
        
        // Get current model
        GameObject currentModel = GetCurrentVisibleModel();
        
        // Use animation system to handle the transition (it will create the target model)
        if (uiAnimator != null)
        {
            uiAnimator.RequestModelTransition(currentModel, null, () => CreateCharacterModelForAnimation(characterIndex), () => {
                // Callback when animation completes
                Debug.Log($"[CHAR_SELECT_REVAMP] Character model transition to {availableCharacters[characterIndex].CharacterName} completed");
                
                // Update current model references
                UpdateCurrentModelReferences();
            });
        }
    }

    private void RequestPetModelTransition(int petIndex)
    {
        Debug.Log($"[CHAR_SELECT_REVAMP] Requesting pet model transition to index {petIndex}");
        
        // Get current model
        GameObject currentModel = GetCurrentVisibleModel();
        
        // Use animation system to handle the transition (it will create the target model)
        if (uiAnimator != null)
        {
            uiAnimator.RequestModelTransition(currentModel, null, () => CreatePetModelForAnimation(petIndex), () => {
                // Callback when animation completes
                Debug.Log($"[CHAR_SELECT_REVAMP] Pet model transition to {availablePets[petIndex].PetName} completed");
                
                // Update current model references
                UpdateCurrentModelReferences();
            });
        }
    }

    private GameObject CreateCharacterModelForAnimation(int characterIndex)
    {
        Debug.Log($"[CHAR_SELECT_REVAMP] Creating character model for animation: index {characterIndex}");
        
        // CRITICAL: With immediate cleanup system, we should NEVER reuse models
        // Always create a fresh model to prevent race conditions
        
        if (characterIndex < 0 || characterIndex >= availableCharacters.Count)
        {
            Debug.LogWarning($"[CHAR_SELECT_REVAMP] Invalid character index for model creation: {characterIndex} (available: 0-{availableCharacters.Count - 1})");
            return null;
        }
        
        // Create new model - no reuse to prevent conflicts
        var newModel = CreateCharacterModel(characterIndex);
        if (newModel != null)
        {
            PrepareModelForAnimation(newModel);
            Debug.Log($"[CHAR_SELECT_REVAMP] Created fresh character model: {newModel.name}");
        }
        else
        {
            Debug.LogError($"[CHAR_SELECT_REVAMP] Failed to create character model for index {characterIndex}");
        }
        return newModel;
    }

    private GameObject CreatePetModelForAnimation(int petIndex)
    {
        Debug.Log($"[CHAR_SELECT_REVAMP] Creating pet model for animation: index {petIndex}");
        
        // CRITICAL: With immediate cleanup system, we should NEVER reuse models
        // Always create a fresh model to prevent race conditions
        
        if (petIndex < 0 || petIndex >= availablePets.Count)
        {
            Debug.LogWarning($"[CHAR_SELECT_REVAMP] Invalid pet index for model creation: {petIndex} (available: 0-{availablePets.Count - 1})");
            return null;
        }
        
        // Create new model - no reuse to prevent conflicts
        var newModel = CreatePetModel(petIndex);
        if (newModel != null)
        {
            PrepareModelForAnimation(newModel);
            Debug.Log($"[CHAR_SELECT_REVAMP] Created fresh pet model: {newModel.name}");
        }
        else
        {
            Debug.LogError($"[CHAR_SELECT_REVAMP] Failed to create pet model for index {petIndex}");
        }
        return newModel;
    }

    private GameObject CreateCharacterModel(int characterIndex)
    {
        if (characterIndex < 0 || characterIndex >= availableCharacters.Count)
        {
            Debug.LogWarning($"[CHAR_SELECT_REVAMP] Invalid character index for model creation: {characterIndex} (available: 0-{availableCharacters.Count - 1})");
            return null;
        }
        
        if (selectedModelSpawn == null)
        {
            Debug.LogError("[CHAR_SELECT_REVAMP] selectedModelSpawn is null - CANNOT CREATE CHARACTER MODEL! Please assign the selectedModelSpawn Transform in the inspector.");
            return null;
        }
        
        // Get the character prefab from selection manager
        if (selectionManager == null)
        {
            Debug.LogError("[CHAR_SELECT_REVAMP] Selection manager is null - cannot get character prefab");
            return null;
        }
        
        Debug.Log($"[CHAR_SELECT_REVAMP] Getting character prefab for index {characterIndex} from selection manager...");
        GameObject characterPrefab = selectionManager.GetCharacterPrefabByIndex(characterIndex);
        if (characterPrefab == null)
        {
            Debug.LogError($"[CHAR_SELECT_REVAMP] No character prefab found for index: {characterIndex}. Check if CharacterSelectionManager has character prefabs assigned.");
            return null;
        }
        
        Debug.Log($"[CHAR_SELECT_REVAMP] Found character prefab: {characterPrefab.name}");
        
        // Create the new character model
        GameObject newCharacterModel = Instantiate(characterPrefab, selectedModelSpawn);
        newCharacterModel.name = $"SelectedCharacter_{availableCharacters[characterIndex].CharacterName}";
        
        Debug.Log($"[CHAR_SELECT_REVAMP] Created character model: {newCharacterModel.name} at {selectedModelSpawn.position}");
        
        return newCharacterModel;
    }

    private GameObject CreatePetModel(int petIndex)
    {
        if (petIndex < 0 || petIndex >= availablePets.Count)
        {
            Debug.LogWarning($"[CHAR_SELECT_REVAMP] Invalid pet index for model creation: {petIndex}");
            return null;
        }
        
        if (selectedModelSpawn == null)
        {
            Debug.LogWarning("[CHAR_SELECT_REVAMP] selectedModelSpawn is null - cannot create pet model");
            return null;
        }
        
        // Get the pet prefab from selection manager
        if (selectionManager == null)
        {
            Debug.LogWarning("[CHAR_SELECT_REVAMP] Selection manager is null - cannot get pet prefab");
            return null;
        }
        
        GameObject petPrefab = selectionManager.GetPetPrefabByIndex(petIndex);
        if (petPrefab == null)
        {
            Debug.LogWarning($"[CHAR_SELECT_REVAMP] No pet prefab found for index: {petIndex}");
            return null;
        }
        
        // Create the new pet model
        GameObject newPetModel = Instantiate(petPrefab, selectedModelSpawn);
        newPetModel.name = $"SelectedPet_{availablePets[petIndex].PetName}";
        
        Debug.Log($"[CHAR_SELECT_REVAMP] Created pet model: {newPetModel.name} at {selectedModelSpawn.position}");
        
        return newPetModel;
    }

    private GameObject GetCurrentVisibleModel()
    {
        // Look for currently visible models
        if (currentCharacterModel != null)
        {
            Renderer[] renderers = currentCharacterModel.GetComponentsInChildren<Renderer>();
            if (renderers.Any(r => r.enabled))
            {
                return currentCharacterModel;
            }
        }
        
        if (currentPetModel != null)
        {
            Renderer[] renderers = currentPetModel.GetComponentsInChildren<Renderer>();
            if (renderers.Any(r => r.enabled))
            {
                return currentPetModel;
            }
        }
        
        return null;
    }

    private void UpdateCurrentModelReferences()
    {
        Debug.Log($"[CHAR_SELECT_REVAMP] Updating current model references");
        
        // With immediate cleanup system, we just need to find the one remaining model
        // The ModelDissolveAnimator handles cleanup, so there should only be one model
        
        // Clear old references if models were destroyed
        if (currentCharacterModel != null && !IsModelValid(currentCharacterModel))
        {
            Debug.Log($"[CHAR_SELECT_REVAMP] Clearing invalid character model reference");
            currentCharacterModel = null;
        }
        
        if (currentPetModel != null && !IsModelValid(currentPetModel))
        {
            Debug.Log($"[CHAR_SELECT_REVAMP] Clearing invalid pet model reference");
            currentPetModel = null;
        }
        
        // Find the current model (there should only be one after immediate cleanup)
        GameObject[] allModels = FindAllModelsInScene();
        
        if (allModels.Length > 1)
        {
            Debug.LogWarning($"[CHAR_SELECT_REVAMP] Multiple models found ({allModels.Length}) - immediate cleanup may have failed!");
            foreach (GameObject model in allModels)
            {
                Debug.LogWarning($"[CHAR_SELECT_REVAMP] Found model: {model.name}");
            }
        }
        
        // Update references to the single remaining model
        foreach (GameObject model in allModels)
        {
            if (model.name.StartsWith("SelectedCharacter_"))
            {
                currentCharacterModel = model;
                currentPetModel = null; // Ensure mutual exclusivity
                Debug.Log($"[CHAR_SELECT_REVAMP] Updated character model reference to: {model.name}");
            }
            else if (model.name.StartsWith("SelectedPet_"))
            {
                currentPetModel = model;
                currentCharacterModel = null; // Ensure mutual exclusivity
                Debug.Log($"[CHAR_SELECT_REVAMP] Updated pet model reference to: {model.name}");
            }
        }
        
        Debug.Log($"[CHAR_SELECT_REVAMP] Model references updated - Character: {currentCharacterModel?.name ?? "None"}, Pet: {currentPetModel?.name ?? "None"}");
    }

    private GameObject[] FindAllModelsInScene()
    {
        var models = new List<GameObject>();
        
        // Find all GameObjects that start with "SelectedCharacter_" or "SelectedPet_"
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.StartsWith("SelectedCharacter_") || obj.name.StartsWith("SelectedPet_"))
            {
                models.Add(obj);
            }
        }
        
        return models.ToArray();
    }

    private bool IsModelValid(GameObject model)
    {
        return model != null && model.gameObject != null;
    }

    private void HandleModelVisibility(GameObject model, bool isCharacter)
    {
        if (model == null) return;
        
        Debug.Log($"[CHAR_SELECT_REVAMP] Handling visibility for {(isCharacter ? "character" : "pet")} model: {model.name}");
        
        // Character/Pet selection preview models should always be visible during character selection
        // These are local preview models, not NetworkEntity objects managed by EntityVisibilityManager
        
        // Ensure all renderers in the model are enabled
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }
        
        // Ensure the GameObject itself is active
        model.SetActive(true);
        
        Debug.Log($"[CHAR_SELECT_REVAMP] {(isCharacter ? "Character" : "Pet")} model {model.name} set to visible - {renderers.Length} renderers enabled");
        
        // EntityVisibilityManager is for NetworkEntity objects in combat/multiplayer scenarios
        // Our selection preview models are local GameObjects that should bypass that system
        EntityVisibilityManager visibilityManager = FindFirstObjectByType<EntityVisibilityManager>();
        if (visibilityManager != null)
        {
            Debug.Log($"[CHAR_SELECT_REVAMP] EntityVisibilityManager present but bypassed for local selection preview model");
        }
    }

    #region Mario Kart Style Updates

    public void UpdateOtherPlayersSelections(List<PlayerSelectionInfo> playerInfos)
    {
        Debug.Log($"[CHAR_SELECT_REVAMP] UpdateOtherPlayersSelections called with {playerInfos.Count} players");
        
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
                
                // Update deck preview controller with ready state
                if (wasReady != isReady && deckPreviewController != null)
                {
                    deckPreviewController.SetPlayerReadyState(isReady);
                    Debug.Log($"[CHAR_SELECT_REVAMP] Player ready state changed to {isReady} - updated deck preview controller");
                }
                continue; // Skip showing selection indicators for self
            }
            
            Color playerColor = GetPlayerColor(info.playerName);
            
            // Show character selection using button highlighting
            if (info.hasSelection && info.characterIndex >= 0 && info.characterIndex < characterPortraitButtons.Count)
            {
                Button characterButton = characterPortraitButtons[info.characterIndex];
                if (characterButton != null)
                {
                    // Add a subtle border or glow effect to show other players' selections
                    AddPlayerSelectionIndicator(characterButton.gameObject, info.playerName, playerColor);
                    Debug.Log($"[CHAR_SELECT_REVAMP] Added selection indicator for player {info.playerName} on character {info.characterIndex}");
                }
            }
            
            // Show pet selection using button highlighting  
            if (info.hasSelection && info.petIndex >= 0 && info.petIndex < petPortraitButtons.Count)
            {
                Button petButton = petPortraitButtons[info.petIndex];
                if (petButton != null)
                {
                    // Add a subtle border or glow effect to show other players' selections
                    AddPlayerSelectionIndicator(petButton.gameObject, info.playerName, playerColor);
                    Debug.Log($"[CHAR_SELECT_REVAMP] Added selection indicator for player {info.playerName} on pet {info.petIndex}");
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
    
    private void AddPlayerSelectionIndicator(GameObject button, string playerName, Color playerColor)
    {
        // For now, we'll implement a simple approach
        // In a full implementation, you might add an outline component or child indicator object
        
        // Create a simple colored indicator (e.g., a small colored square in the corner)
        GameObject indicator = new GameObject($"PlayerIndicator_{playerName}");
        indicator.transform.SetParent(button.transform, false);
        
        // Add an Image component to show the player's color
        UnityEngine.UI.Image indicatorImage = indicator.AddComponent<UnityEngine.UI.Image>();
        indicatorImage.color = playerColor;
        
        // Position it in the bottom-right corner
        RectTransform indicatorRect = indicator.GetComponent<RectTransform>();
        indicatorRect.anchorMin = new Vector2(0.8f, 0.1f);
        indicatorRect.anchorMax = new Vector2(0.95f, 0.25f);
        indicatorRect.offsetMin = Vector2.zero;
        indicatorRect.offsetMax = Vector2.zero;
        
        Debug.Log($"[CHAR_SELECT_REVAMP] Added visual indicator for player {playerName} with color {playerColor}");
    }

    private void ClearAllPlayerSelections()
    {
        Debug.Log("[CHAR_SELECT_REVAMP] Clearing all player selection indicators");
        
        // Clear character selection indicators
        foreach (Button button in characterPortraitButtons)
        {
            if (button != null)
            {
                ClearPlayerIndicators(button.gameObject);
            }
        }
        
        // Clear pet selection indicators
        foreach (Button button in petPortraitButtons)
        {
            if (button != null)
            {
                ClearPlayerIndicators(button.gameObject);
            }
        }
    }
    
    private void ClearPlayerIndicators(GameObject button)
    {
        // Find and destroy all player indicator children
        Transform[] children = button.GetComponentsInChildren<Transform>();
        foreach (Transform child in children)
        {
            if (child.name.StartsWith("PlayerIndicator_"))
            {
                Destroy(child.gameObject);
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

    private void SetupSharedGridParent()
    {
        // If we have a shared grid parent, move all buttons to it
        if (sharedGridParent != null)
        {
            Debug.Log($"[CHAR_SELECT_REVAMP] Setting up shared grid parent - moving {characterPortraitButtons.Count} character buttons and {petPortraitButtons.Count} pet buttons");
            
            // Move character buttons to shared parent
            foreach (Button button in characterPortraitButtons)
            {
                if (button != null)
                {
                    button.transform.SetParent(sharedGridParent, false);
                }
            }
            
            // Move pet buttons to shared parent
            foreach (Button button in petPortraitButtons)
            {
                if (button != null)
                {
                    button.transform.SetParent(sharedGridParent, false);
                }
            }
            
            Debug.Log("[CHAR_SELECT_REVAMP] All portrait buttons moved to shared grid parent");
        }
        else
        {
            Debug.Log("[CHAR_SELECT_REVAMP] No shared grid parent specified - using separate character and pet grid parents");
        }
    }
    
    #endregion

    /// <summary>
    /// Called by EntitySelectionController to finalize selection and update server
    /// </summary>
    public void UpdateSelectionFromController(EntitySelectionController.EntityType entityType, int selectionIndex)
    {
        Debug.Log($"[CHAR_SELECT_REVAMP] UpdateSelectionFromController({entityType}, {selectionIndex}) called");
        
        // Update selection state
        UpdateSelectionState();
    }

    private void CreateCharacterPortraitButtons()
    {
        Debug.Log($"[CHAR_SELECT_REVAMP] Creating {availableCharacters.Count} character portrait buttons");
        
        for (int i = 0; i < availableCharacters.Count; i++)
        {
            CharacterData character = availableCharacters[i];
            if (character == null) continue;
            
            // Use characterGridParent initially, then move to shared parent later if needed
            Transform parentTransform = characterGridParent != null ? characterGridParent : transform;
            Button button = Instantiate(selectionItemPrefab, parentTransform).GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError("[CHAR_SELECT_REVAMP] Selection Item Prefab is not a Button!");
                continue;
            }
            
            int index = i; // Capture for closure
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnCharacterSelectionChanged(index));
            
            characterPortraitButtons.Add(button);
            
            // Set up the item data using the prefab structure
            SetupPortraitButton(button, character.CharacterName, character.CharacterPortrait);
        }
        
        Debug.Log($"[CHAR_SELECT_REVAMP] Created {characterPortraitButtons.Count} character portrait buttons");
    }
    
    private void CreatePetPortraitButtons()
    {
        Debug.Log($"[CHAR_SELECT_REVAMP] Creating {availablePets.Count} pet portrait buttons");
        
        for (int i = 0; i < availablePets.Count; i++)
        {
            PetData pet = availablePets[i];
            if (pet == null) continue;
            
            // Use characterGridParent initially, then move to shared parent later if needed
            Transform parentTransform = characterGridParent != null ? characterGridParent : transform;
            Button button = Instantiate(selectionItemPrefab, parentTransform).GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError("[CHAR_SELECT_REVAMP] Selection Item Prefab is not a Button!");
                continue;
            }
            
            int index = i; // Capture for closure
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnPetSelectionChanged(index));
            
            petPortraitButtons.Add(button);
            
            // Set up the item data using the prefab structure
            SetupPortraitButton(button, pet.PetName, pet.PetPortrait);
        }
        
        Debug.Log($"[CHAR_SELECT_REVAMP] Created {petPortraitButtons.Count} pet portrait buttons");
    }
    
    private void SetupPortraitButton(Button button, string entityName, Sprite portraitSprite)
    {
        // Find the name text component
        TextMeshProUGUI nameText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text = entityName;
        }
        
        // Find the portrait image component more reliably
        Image portraitImage = FindPortraitImageComponent(button);
        
        // Set the portrait sprite and preserve scaling
        if (portraitImage != null && portraitSprite != null)
        {
            // Store original scale before changing sprite
            Vector3 originalScale = portraitImage.transform.localScale;
            
            // Set the sprite
            portraitImage.sprite = portraitSprite;
            
            // Restore the original scale to respect prefab scaling
            portraitImage.transform.localScale = originalScale;
            
            Debug.Log($"[CHAR_SELECT_REVAMP] Set portrait for {entityName} on {portraitImage.gameObject.name} with scale {originalScale}");
        }
        else if (portraitSprite != null)
        {
            Debug.LogWarning($"[CHAR_SELECT_REVAMP] Could not find portrait Image component in prefab for {entityName}");
        }
    }

    private Image FindPortraitImageComponent(Button button)
    {
        // Get all Image components in the button hierarchy
        Image[] images = button.GetComponentsInChildren<Image>();
        Image buttonRootImage = button.GetComponent<Image>();
        
        // Strategy 1: Look for "Portrait" specifically in the name
        foreach (Image img in images)
        {
            if (img.gameObject.name.Equals("Portrait", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[CHAR_SELECT_REVAMP] Found Portrait image by exact name match: {img.gameObject.name}");
                return img;
            }
        }
        
        // Strategy 2: Look for names containing "portrait" 
        foreach (Image img in images)
        {
            if (img.gameObject.name.ToLower().Contains("portrait"))
            {
                Debug.Log($"[CHAR_SELECT_REVAMP] Found Portrait image by name pattern: {img.gameObject.name}");
                return img;
            }
        }
        
        // Strategy 3: Exclude known non-portrait names and find the deepest/most nested Image
        Image deepestImage = null;
        int maxDepth = -1;
        
        foreach (Image img in images)
        {
            // Skip the button's root image and known non-portrait components
            if (img == buttonRootImage) continue;
            
            string imgName = img.gameObject.name.ToLower();
            if (imgName.Contains("border") || imgName.Contains("background") || imgName.Contains("innerbox")) 
                continue;
            
            // Calculate depth in hierarchy
            int depth = GetTransformDepth(img.transform, button.transform);
            if (depth > maxDepth)
            {
                maxDepth = depth;
                deepestImage = img;
            }
        }
        
        if (deepestImage != null)
        {
            Debug.Log($"[CHAR_SELECT_REVAMP] Found Portrait image by depth analysis: {deepestImage.gameObject.name} (depth: {maxDepth})");
            return deepestImage;
        }
        
        // Strategy 4: Last resort - use the last Image component (often the most specific one)
        if (images.Length > 1)
        {
            for (int i = images.Length - 1; i >= 0; i--)
            {
                if (images[i] != buttonRootImage)
                {
                    Debug.Log($"[CHAR_SELECT_REVAMP] Found Portrait image by last resort: {images[i].gameObject.name}");
                    return images[i];
                }
            }
        }
        
        Debug.LogWarning("[CHAR_SELECT_REVAMP] Could not find suitable Portrait Image component");
        return null;
    }
    
    private int GetTransformDepth(Transform child, Transform root)
    {
        int depth = 0;
        Transform current = child;
        while (current != null && current != root)
        {
            depth++;
            current = current.parent;
        }
        return depth;
    }
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