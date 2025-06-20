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
/// Positioning mode for 3D models in character selection
/// </summary>
public enum ModelPositioningMode
{
    Grid,   // Use grid-based positioning with automatic layout
    Manual  // Use manually specified world coordinates
}

/// <summary>
/// Manages the character selection UI interactions with Mario Kart-style shared selection grids.
/// </summary>
public class CharacterSelectionUIManager : NetworkBehaviour
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
    
    [Header("3D Model Positioning")]
    [SerializeField] private bool use3DModelPositioning = false;
    [SerializeField] private ModelPositioningMode positioningMode = ModelPositioningMode.Grid;
    [SerializeField] private Transform characterModelsParent;
    [SerializeField] private Transform petModelsParent;
    [SerializeField] private Camera modelViewCamera; // Camera for viewing the 3D models
    
    [Header("Grid Positioning Settings")]
    [Header("Character Grid (relative to characterModelsParent)")]
    [SerializeField] private Vector3 characterModelSpacing = new Vector3(2f, 0f, 0f);
    [SerializeField] private int characterModelsPerRow = 4;
    [SerializeField] private Vector3 characterRowOffset = new Vector3(0f, 0f, -2f);
    
    [Header("Pet Grid (relative to petModelsParent)")]
    [SerializeField] private Vector3 petModelSpacing = new Vector3(2f, 0f, 0f);
    [SerializeField] private int petModelsPerRow = 3;
    [SerializeField] private Vector3 petRowOffset = new Vector3(0f, 0f, -2f);
    
    [Header("Manual Positioning Settings")]
    [SerializeField] private List<Vector3> characterPositions = new List<Vector3>();
    [SerializeField] private List<Vector3> petPositions = new List<Vector3>();
    [SerializeField] private bool autoGeneratePositions = true; // Generate positions if lists are empty
    
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
    private List<GameObject> playerListItems = new List<GameObject>();
    
    // Entity selection controllers (for interfacing with prefab controllers)
    private List<EntitySelectionController> characterControllers = new List<EntitySelectionController>();
    private List<EntitySelectionController> petControllers = new List<EntitySelectionController>();
    
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
        
        Debug.Log("CharacterSelectionUIManager: Setting up UI (Part 1 - Core UI and Animator)...");
        SetupCoreUI();
        
        Debug.Log("CharacterSelectionUIManager: Initializing deck preview controller...");
        InitializeDeckPreviewController();
        
        Debug.Log("CharacterSelectionUIManager: Finalizing UI animator setup...");
        FinalizeUIAnimatorSetup();
        
        Debug.Log("CharacterSelectionUIManager: Setting up UI (Part 2 - Panel setup)...");
        FinishUISetup();
        
        Debug.Log("CharacterSelectionUIManager: Creating selection items...");
        CreateSelectionItems();
        
        Debug.Log("CharacterSelectionUIManager: Making default selections (this will trigger deck previews)...");
        MakeDefaultSelections();
        
        Debug.Log("CharacterSelectionUIManager: Updating ready button state...");
        UpdateReadyButtonState();
        
        isInitialized = true;
        Debug.Log($"CharacterSelectionUIManager: Initialized - Player ID: {myPlayerID}, Color: {myPlayerColor}");
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
        if (characterItems.Count > 0 || petItems.Count > 0)
        {
            Debug.Log($"CharacterSelectionUIManager: Selection objects already in lists - Characters: {characterItems.Count}, Pets: {petItems.Count}");
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
        Debug.Log("CharacterSelectionUIManager: Server spawning selection objects for new client");
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
        
        // Set up click detection areas using the grid parents instead of scroll views
        uiAnimator.SetClickDetectionAreas(
            characterGridParent?.GetComponent<RectTransform>(), 
            petGridParent?.GetComponent<RectTransform>()
        );
        
        Debug.Log("CharacterSelectionUIManager: UI Animator created and basic setup complete");
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
        // Check if network is ready, if not wait for it
        if (!IsNetworkManagerReady())
        {
            Debug.Log("CharacterSelectionUIManager: Network not ready, waiting...");
            StartCoroutine(WaitForNetworkAndCreateItems());
            return;
        }
        
        // First, check if there are already spawned selection objects (for late-joining clients)
        SelectionNetworkObject[] existingObjects = FindObjectsOfType<SelectionNetworkObject>();
        if (existingObjects.Length > 0)
        {
            Debug.Log($"CharacterSelectionUIManager: Found {existingObjects.Length} existing spawned selection objects, registering them");
            DiscoverAndRegisterSpawnedObjects(existingObjects);
            return;
        }
        
        // If we're the server (or host) and no objects exist, spawn them
        if (FishNet.InstanceFinder.IsServerStarted)
        {
            Debug.Log("CharacterSelectionUIManager: Server spawning new selection objects");
            CreateCharacterItems();
            CreatePetItems();
        }
        else
        {
            // We're a client and no objects exist yet, wait for them
            Debug.Log("CharacterSelectionUIManager: Client waiting for server to spawn selection objects");
            StartCoroutine(WaitForServerSpawnedObjects());
        }
    }

    private void CreateCharacterItems()
    {
        // Clear existing items and controllers
        foreach (GameObject item in characterItems)
        {
            if (item != null) Destroy(item);
        }
        characterItems.Clear();
        characterControllers.Clear();
        
        // Create character selection items
        for (int i = 0; i < availableCharacters.Count; i++)
        {
            CharacterData character = availableCharacters[i];
            if (character == null) continue;
            
            GameObject item;
            EntitySelectionController controller;
            
            if (use3DModelPositioning)
            {
                // Create 3D model-based selection item
                item = Create3DSelectionItem(character, i, true);
                controller = item.GetComponent<EntitySelectionController>();
                if (controller == null)
                {
                    controller = item.AddComponent<EntitySelectionController>();
                }
                
                // Set to 3D model mode and configure
                controller.SetSelectionMode(EntitySelectionController.SelectionMode.Model_3D);
                
                // Set the camera for raycast detection
                if (modelViewCamera != null)
                {
                    // We'll need to add a method to set the camera in EntitySelectionController
                    SetControllerCamera(controller, modelViewCamera);
                }
            }
            else
            {
                // Create traditional UI-based selection item
                item = CreateSelectionItem(characterGridParent, character.CharacterName, character.CharacterPortrait, character.CharacterDescription, true, i);
                controller = item.GetComponent<EntitySelectionController>();
                if (controller == null)
                {
                    controller = item.AddComponent<EntitySelectionController>();
                }
            }
            
            if (item != null && controller != null)
            {
                controller.InitializeWithCharacter(character, i, this, deckPreviewController, selectionManager);
                characterControllers.Add(controller);
                characterItems.Add(item);
                
                // Position 3D model if using 3D positioning
                if (use3DModelPositioning)
                {
                    Position3DModel(item, i, true);
                }
            }
        }
    }

    private void CreatePetItems()
    {
        // Clear existing items and controllers
        foreach (GameObject item in petItems)
        {
            if (item != null) Destroy(item);
        }
        petItems.Clear();
        petControllers.Clear();
        
        // Create pet selection items
        for (int i = 0; i < availablePets.Count; i++)
        {
            PetData pet = availablePets[i];
            if (pet == null) continue;
            
            GameObject item;
            EntitySelectionController controller;
            
            if (use3DModelPositioning)
            {
                // Create 3D model-based selection item
                item = Create3DSelectionItem(pet, i, false);
                controller = item.GetComponent<EntitySelectionController>();
                if (controller == null)
                {
                    controller = item.AddComponent<EntitySelectionController>();
                }
                
                // Set to 3D model mode and configure
                controller.SetSelectionMode(EntitySelectionController.SelectionMode.Model_3D);
                
                // Set the camera for raycast detection
                if (modelViewCamera != null)
                {
                    SetControllerCamera(controller, modelViewCamera);
                }
            }
            else
            {
                // Create traditional UI-based selection item
                item = CreateSelectionItem(petGridParent, pet.PetName, pet.PetPortrait, pet.PetDescription, false, i);
                controller = item.GetComponent<EntitySelectionController>();
                if (controller == null)
                {
                    controller = item.AddComponent<EntitySelectionController>();
                }
            }
            
            if (item != null && controller != null)
            {
                controller.InitializeWithPet(pet, i, this, deckPreviewController, selectionManager);
                petControllers.Add(controller);
                petItems.Add(item);
                
                // Position 3D model if using 3D positioning
                if (use3DModelPositioning)
                {
                    Position3DModel(item, i, false);
                }
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
    
    /// <summary>
    /// Creates a 3D model-based selection item for characters or pets using prefabs
    /// </summary>
    private GameObject Create3DSelectionItem(object data, int index, bool isCharacter)
    {
        GameObject prefabToUse = null;
        
        // Get the appropriate prefab from the selection manager
        if (isCharacter)
        {
            prefabToUse = selectionManager.GetCharacterPrefabByIndex(index);
        }
        else
        {
            prefabToUse = selectionManager.GetPetPrefabByIndex(index);
        }
        
        if (prefabToUse == null)
        {
            Debug.LogError($"CharacterSelectionUIManager: No prefab found for {(isCharacter ? "character" : "pet")} at index {index}");
            return null;
        }
        
        // Set parent based on type
        Transform parentTransform = isCharacter ? 
            (characterModelsParent != null ? characterModelsParent : transform) : 
            (petModelsParent != null ? petModelsParent : transform);
        
        GameObject item;
        
        // Check if the prefab has a NetworkObject component
        NetworkObject networkObjectComponent = prefabToUse.GetComponent<NetworkObject>();
        if (networkObjectComponent != null)
        {
            // Since we're in a networked context, spawn the NetworkObject properly
            Debug.Log($"CharacterSelectionUIManager: Detected NetworkObject on {prefabToUse.name}, spawning through network system for character selection");
            item = SpawnNetworkObjectForSelection(prefabToUse, parentTransform, index, isCharacter);
        }
        else
        {
            // Instantiate normally if no NetworkObject
            item = Instantiate(prefabToUse, parentTransform);
            item.name = isCharacter ? $"Character_{index}_{prefabToUse.name}" : $"Pet_{index}_{prefabToUse.name}";
        }
        
        if (item == null)
        {
            Debug.LogError($"CharacterSelectionUIManager: Failed to create selection item for {(isCharacter ? "character" : "pet")} at index {index}");
            return null;
        }
        
        // Ensure the item has an EntitySelectionController
        EntitySelectionController controller = item.GetComponent<EntitySelectionController>();
        if (controller == null)
        {
            controller = item.AddComponent<EntitySelectionController>();
        }
        
        // Set the prefab's 3D model as the selection target
        // The prefab itself contains the visual model, so we set it as the model3D
        controller.SetModel3D(item);
        
        return item;
    }
    
    /// <summary>
    /// Spawns a NetworkObject prefab for character selection using the network system
    /// </summary>
    private GameObject SpawnNetworkObjectForSelection(GameObject networkPrefab, Transform parent, int index, bool isCharacter)
    {
        // Only the server should spawn NetworkObjects
        if (!FishNet.InstanceFinder.IsServerStarted)
        {
            Debug.LogWarning($"CharacterSelectionUIManager: Client cannot spawn NetworkObjects. Server should handle character selection spawning. Skipping {networkPrefab.name}");
            return null;
        }
        
        // Check if we have a valid network manager
        if (!IsNetworkManagerReady())
        {
            Debug.LogError($"CharacterSelectionUIManager: Network manager not ready, cannot spawn NetworkObject for selection");
            return null;
        }
        
        try
        {
            // Spawn the NetworkObject at root level first (FishNet default behavior)
            GameObject spawnedObject = Instantiate(networkPrefab);
            spawnedObject.name = isCharacter ? $"Character_{index}_{networkPrefab.name}" : $"Pet_{index}_{networkPrefab.name}";
            
            // Get the NetworkObject component
            NetworkObject networkObject = spawnedObject.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"CharacterSelectionUIManager: NetworkObject component missing on {spawnedObject.name}");
                Destroy(spawnedObject);
                return null;
            }
            
            // Spawn the object on the network for all clients to see
            FishNet.InstanceFinder.ServerManager.Spawn(networkObject);
            Debug.Log($"CharacterSelectionUIManager: Server spawned NetworkObject {spawnedObject.name} for character selection");
            
            // Mark this as a selection object with desired parent info
            SelectionNetworkObject selectionMarker = spawnedObject.AddComponent<SelectionNetworkObject>();
            selectionMarker.isCharacterSelectionObject = true;
            selectionMarker.selectionIndex = index;
            selectionMarker.isCharacter = isCharacter;
            selectionMarker.SetDesiredParent(parent);
            
            // Use a coroutine to handle parenting after the object is fully spawned and synchronized
            StartCoroutine(ParentNetworkObjectAfterSpawn(spawnedObject, parent, index, isCharacter));
            
            return spawnedObject;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"CharacterSelectionUIManager: Error spawning NetworkObject for selection: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Parents a NetworkObject after it has been spawned and synchronized
    /// </summary>
    private System.Collections.IEnumerator ParentNetworkObjectAfterSpawn(GameObject spawnedObject, Transform parent, int index, bool isCharacter)
    {
        // Wait a frame to ensure the object is fully spawned
        yield return null;
        
        if (spawnedObject != null && parent != null)
        {
            // Move to desired position in hierarchy
            spawnedObject.transform.SetParent(parent, false);
            
            // Position the object correctly
            Position3DModel(spawnedObject, index, isCharacter);
            
            Debug.Log($"CharacterSelectionUIManager: Successfully parented NetworkObject {spawnedObject.name} to {parent.name}");
        }
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
        Debug.Log("CharacterSelectionUIManager: Waiting for network to be ready...");
        
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
        Debug.Log("CharacterSelectionUIManager: Client waiting for server-spawned selection objects...");
        
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
                    Debug.Log($"CharacterSelectionUIManager: Discovered and tagged NetworkObject {obj.name} as {(isCharacterPrefab ? "character" : "pet")} at index {prefabIndex}");
                }
            }
            
            // If we discovered enough objects, use them
            int expectedCount = (availableCharacters?.Count ?? 0) + (availablePets?.Count ?? 0);
            if (discoveredObjects.Count >= expectedCount)
            {
                Debug.Log($"CharacterSelectionUIManager: Client discovered {discoveredObjects.Count} NetworkObjects matching our prefabs");
                DiscoverAndRegisterSpawnedObjects(discoveredObjects.ToArray());
                yield break;
            }
            
            if (expectedCount > 0)
            {
                Debug.Log($"CharacterSelectionUIManager: Still waiting... Expected {expectedCount} objects, found {discoveredObjects.Count} NetworkObjects");
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
        Debug.Log($"CharacterSelectionUIManager: Client registering {spawnedObjects.Length} discovered selection objects");
        
        // Clear any existing items
        characterItems.Clear();
        petItems.Clear();
        characterControllers.Clear();
        petControllers.Clear();
        
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
                (characterModelsParent != null ? characterModelsParent : transform) : 
                (petModelsParent != null ? petModelsParent : transform);
            
            if (targetParent != null && item.transform.parent != targetParent)
            {
                // Parent the object to the correct UI hierarchy position
                item.transform.SetParent(targetParent, false);
                Debug.Log($"CharacterSelectionUIManager: Parented {item.name} to {targetParent.name}");
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
                characterItems.Add(item);
                characterControllers.Add(controller);
                
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
                petItems.Add(item);
                petControllers.Add(controller);
                
                // Position the model correctly
                Position3DModel(item, selectionObj.selectionIndex, false);
            }
            
            Debug.Log($"CharacterSelectionUIManager: Client registered {(selectionObj.isCharacter ? "character" : "pet")} selection object at index {selectionObj.selectionIndex}");
        }
        
        Debug.Log($"CharacterSelectionUIManager: Client registration complete - {characterItems.Count} characters, {petItems.Count} pets");
    }
    
    /// <summary>
    /// Positions a 3D model based on the current positioning mode
    /// </summary>
    private void Position3DModel(GameObject modelItem, int index, bool isCharacter)
    {
        if (modelItem == null) return;
        
        Vector3 position;
        
        switch (positioningMode)
        {
            case ModelPositioningMode.Grid:
                position = CalculateGridPosition(index, isCharacter);
                break;
                
            case ModelPositioningMode.Manual:
                position = GetManualPosition(index, isCharacter);
                break;
                
            default:
                position = CalculateGridPosition(index, isCharacter);
                break;
        }
        
        // Set the position
        modelItem.transform.position = position;
        
        Debug.Log($"CharacterSelectionUIManager: Positioned {(isCharacter ? "character" : "pet")} model {index} at {position} using {positioningMode} mode");
    }
    
    /// <summary>
    /// Calculates a grid-based position for a model
    /// </summary>
    private Vector3 CalculateGridPosition(int index, bool isCharacter)
    {
        if (isCharacter)
        {
            // Calculate character grid position relative to characterModelsParent
            int row = index / characterModelsPerRow;
            int col = index % characterModelsPerRow;
            
            // Calculate center offset - center the grid horizontally around the parent
            float centerOffset = -(characterModelsPerRow - 1) * characterModelSpacing.x / 2f;
            Vector3 gridStartOffset = new Vector3(centerOffset, 0f, 0f);
            
            Vector3 relativePosition = gridStartOffset + (col * characterModelSpacing) + (row * characterRowOffset);
            
            // Convert to world position using the parent transform
            if (characterModelsParent != null)
            {
                return characterModelsParent.TransformPoint(relativePosition);
            }
            else
            {
                Debug.LogWarning("CharacterSelectionUIManager: characterModelsParent is null, using world coordinates");
                return relativePosition;
            }
        }
        else
        {
            // Calculate pet grid position relative to petModelsParent
            int row = index / petModelsPerRow;
            int col = index % petModelsPerRow;
            
            // Calculate center offset - center the grid horizontally around the parent
            float centerOffset = -(petModelsPerRow - 1) * petModelSpacing.x / 2f;
            Vector3 gridStartOffset = new Vector3(centerOffset, 0f, 0f);
            
            Vector3 relativePosition = gridStartOffset + (col * petModelSpacing) + (row * petRowOffset);
            
            // Convert to world position using the parent transform
            if (petModelsParent != null)
            {
                return petModelsParent.TransformPoint(relativePosition);
            }
            else
            {
                Debug.LogWarning("CharacterSelectionUIManager: petModelsParent is null, using world coordinates");
                return relativePosition;
            }
        }
    }
    
    /// <summary>
    /// Gets a manually specified position for a model
    /// </summary>
    private Vector3 GetManualPosition(int index, bool isCharacter)
    {
        List<Vector3> positions = isCharacter ? characterPositions : petPositions;
        
        // Check if we have enough positions defined
        if (index < positions.Count)
        {
            return positions[index];
        }
        
        // Handle missing positions
        if (autoGeneratePositions)
        {
            // Generate a position based on grid layout as fallback
            Vector3 generatedPosition = CalculateGridPosition(index, isCharacter);
            
            // Add missing positions to the list up to this index
            while (positions.Count <= index)
            {
                Vector3 fallbackPosition = CalculateGridPosition(positions.Count, isCharacter);
                positions.Add(fallbackPosition);
                Debug.Log($"CharacterSelectionUIManager: Auto-generated {(isCharacter ? "character" : "pet")} position {positions.Count - 1}: {fallbackPosition}");
            }
            
            return positions[index];
        }
        else
        {
            // Use grid position as fallback without modifying the lists
            Debug.LogWarning($"CharacterSelectionUIManager: No manual position defined for {(isCharacter ? "character" : "pet")} {index}, using grid fallback");
            return CalculateGridPosition(index, isCharacter);
        }
    }
    
    /// <summary>
    /// Sets the raycast camera for an EntitySelectionController
    /// </summary>
    private void SetControllerCamera(EntitySelectionController controller, Camera camera)
    {
        if (controller != null)
        {
            controller.SetRaycastCamera(camera);
        }
    }
    
    /// <summary>
    /// Updates the positioning of all 3D models
    /// </summary>
    public void RefreshModelPositioning()
    {
        if (!use3DModelPositioning) return;
        
        // Reposition character models
        for (int i = 0; i < characterItems.Count; i++)
        {
            if (characterItems[i] != null)
            {
                Position3DModel(characterItems[i], i, true);
            }
        }
        
        // Reposition pet models
        for (int i = 0; i < petItems.Count; i++)
        {
            if (petItems[i] != null)
            {
                Position3DModel(petItems[i], i, false);
            }
        }
    }
    
    /// <summary>
    /// Switches between UI and 3D model positioning modes
    /// </summary>
    public void SetUse3DModelPositioning(bool use3D)
    {
        if (use3DModelPositioning == use3D) return;
        
        use3DModelPositioning = use3D;
        
        // Recreate all selection items with new positioning mode
        CreateSelectionItems();
        MakeDefaultSelections();
        
        Debug.Log($"CharacterSelectionUIManager: Switched to {(use3D ? "3D model" : "UI")} positioning mode");
    }
    
    /// <summary>
    /// Sets the positioning mode for 3D models
    /// </summary>
    public void SetPositioningMode(ModelPositioningMode mode)
    {
        if (positioningMode == mode) return;
        
        positioningMode = mode;
        
        // Refresh positioning if using 3D models
        if (use3DModelPositioning)
        {
            RefreshModelPositioning();
        }
        
        Debug.Log($"CharacterSelectionUIManager: Switched positioning mode to {mode}");
    }
    
    /// <summary>
    /// Sets character positions for manual positioning mode
    /// </summary>
    public void SetCharacterPositions(List<Vector3> positions)
    {
        characterPositions = new List<Vector3>(positions);
        
        if (use3DModelPositioning && positioningMode == ModelPositioningMode.Manual)
        {
            RefreshModelPositioning();
        }
        
        Debug.Log($"CharacterSelectionUIManager: Set {positions.Count} character positions");
    }
    
    /// <summary>
    /// Sets pet positions for manual positioning mode
    /// </summary>
    public void SetPetPositions(List<Vector3> positions)
    {
        petPositions = new List<Vector3>(positions);
        
        if (use3DModelPositioning && positioningMode == ModelPositioningMode.Manual)
        {
            RefreshModelPositioning();
        }
        
        Debug.Log($"CharacterSelectionUIManager: Set {positions.Count} pet positions");
    }
    
    /// <summary>
    /// Adds a character position to the manual positions list
    /// </summary>
    public void AddCharacterPosition(Vector3 position)
    {
        characterPositions.Add(position);
        
        if (use3DModelPositioning && positioningMode == ModelPositioningMode.Manual)
        {
            RefreshModelPositioning();
        }
        
        Debug.Log($"CharacterSelectionUIManager: Added character position {characterPositions.Count - 1}: {position}");
    }
    
    /// <summary>
    /// Adds a pet position to the manual positions list
    /// </summary>
    public void AddPetPosition(Vector3 position)
    {
        petPositions.Add(position);
        
        if (use3DModelPositioning && positioningMode == ModelPositioningMode.Manual)
        {
            RefreshModelPositioning();
        }
        
        Debug.Log($"CharacterSelectionUIManager: Added pet position {petPositions.Count - 1}: {position}");
    }
    
    /// <summary>
    /// Clears all manual positions and optionally regenerates them
    /// </summary>
    public void ClearManualPositions(bool regenerate = true)
    {
        characterPositions.Clear();
        petPositions.Clear();
        
        if (regenerate && autoGeneratePositions && use3DModelPositioning && positioningMode == ModelPositioningMode.Manual)
        {
            RefreshModelPositioning();
        }
        
        Debug.Log($"CharacterSelectionUIManager: Cleared all manual positions{(regenerate ? " and regenerated" : "")}");
    }
    
    /// <summary>
    /// Gets a copy of the current character positions
    /// </summary>
    public List<Vector3> GetCharacterPositions()
    {
        return new List<Vector3>(characterPositions);
    }
    
    /// <summary>
    /// Gets a copy of the current pet positions
    /// </summary>
    public List<Vector3> GetPetPositions()
    {
        return new List<Vector3>(petPositions);
    }
    
    /// <summary>
    /// Configures the character grid layout (relative to characterModelsParent)
    /// </summary>
    public void SetCharacterGridLayout(Vector3 spacing, int modelsPerRow, Vector3 rowOffset)
    {
        characterModelSpacing = spacing;
        characterModelsPerRow = modelsPerRow;
        characterRowOffset = rowOffset;
        
        if (use3DModelPositioning && positioningMode == ModelPositioningMode.Grid)
        {
            RefreshModelPositioning();
        }
        
        Debug.Log($"CharacterSelectionUIManager: Updated character grid - Spacing: {spacing}, Per Row: {modelsPerRow}, Row Offset: {rowOffset}");
    }
    
    /// <summary>
    /// Configures the pet grid layout (relative to petModelsParent)
    /// </summary>
    public void SetPetGridLayout(Vector3 spacing, int modelsPerRow, Vector3 rowOffset)
    {
        petModelSpacing = spacing;
        petModelsPerRow = modelsPerRow;
        petRowOffset = rowOffset;
        
        if (use3DModelPositioning && positioningMode == ModelPositioningMode.Grid)
        {
            RefreshModelPositioning();
        }
        
        Debug.Log($"CharacterSelectionUIManager: Updated pet grid - Spacing: {spacing}, Per Row: {modelsPerRow}, Row Offset: {rowOffset}");
    }
    
    /// <summary>
    /// Gets the current character grid configuration
    /// </summary>
    public (Vector3 spacing, int modelsPerRow, Vector3 rowOffset) GetCharacterGridConfig()
    {
        return (characterModelSpacing, characterModelsPerRow, characterRowOffset);
    }
    
    /// <summary>
    /// Gets the current pet grid configuration
    /// </summary>
    public (Vector3 spacing, int modelsPerRow, Vector3 rowOffset) GetPetGridConfig()
    {
        return (petModelSpacing, petModelsPerRow, petRowOffset);
    }

    private void MakeDefaultSelections()
    {
        Debug.Log($"CharacterSelectionUIManager: MakeDefaultSelections() called - availableCharacters: {availableCharacters.Count}, availablePets: {availablePets.Count}");
        
        // Auto-select first character and first pet if available
        if (availableCharacters.Count > 0 && characterControllers.Count > 0)
        {
            Debug.Log($"CharacterSelectionUIManager: Auto-selecting first character: {availableCharacters[0].CharacterName}");
            
            // Trigger selection through the first character controller
            EntitySelectionController firstCharacterController = characterControllers[0];
            if (firstCharacterController != null && firstCharacterController.IsInitialized())
            {
                // Simulate the selection logic that would happen in EntitySelectionController
                OnCharacterSelectionChanged(0);
                
                // Trigger deck preview
                if (deckPreviewController != null)
                {
                    deckPreviewController.SetCurrentCharacterIndex(0);
                    deckPreviewController.ShowCharacterDeck(0, isReady);
                }
                
                Debug.Log($"CharacterSelectionUIManager: Auto-selected first character: {availableCharacters[0].CharacterName}");
            }
        }
        
        if (availablePets.Count > 0 && petControllers.Count > 0)
        {
            Debug.Log($"CharacterSelectionUIManager: Auto-selecting first pet: {availablePets[0].PetName}");
            
            // Trigger selection through the first pet controller
            EntitySelectionController firstPetController = petControllers[0];
            if (firstPetController != null && firstPetController.IsInitialized())
            {
                // Simulate the selection logic that would happen in EntitySelectionController
                OnPetSelectionChanged(0);
                
                // Trigger deck preview
                if (deckPreviewController != null)
                {
                    deckPreviewController.SetCurrentPetIndex(0);
                    deckPreviewController.ShowPetDeck(0, isReady);
                }
                
                Debug.Log($"CharacterSelectionUIManager: Auto-selected first pet: {availablePets[0].PetName}");
            }
        }
        
        // Update selection state and send to server
        UpdateSelectionState();
        
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

    /// <summary>
    /// Called by EntitySelectionController when character selection changes
    /// </summary>
    public void OnCharacterSelectionChanged(int characterIndex)
    {
        Debug.Log($"CharacterSelectionUIManager: OnCharacterSelectionChanged({characterIndex}) called via EntitySelectionController");
        
        if (characterIndex < 0 || characterIndex >= availableCharacters.Count) return;
        
        // Update selection
        selectedCharacterIndex = characterIndex;
        
        // Update visual selection
        UpdateMySelectionVisuals();
        
        Debug.Log($"CharacterSelectionUIManager: Character selection changed to {availableCharacters[characterIndex].CharacterName}");
    }

    /// <summary>
    /// Called by EntitySelectionController when pet selection changes
    /// </summary>
    public void OnPetSelectionChanged(int petIndex)
    {
        Debug.Log($"CharacterSelectionUIManager: OnPetSelectionChanged({petIndex}) called via EntitySelectionController");
        
        if (petIndex < 0 || petIndex >= availablePets.Count) return;
        
        // Update selection
        selectedPetIndex = petIndex;
        
        // Update visual selection
        UpdateMySelectionVisuals();
        
        Debug.Log($"CharacterSelectionUIManager: Pet selection changed to {availablePets[petIndex].PetName}");
    }

    /// <summary>
    /// Called by EntitySelectionController to finalize selection and update server
    /// </summary>
    public void UpdateSelectionFromController(EntitySelectionController.EntityType entityType, int selectionIndex)
    {
        Debug.Log($"CharacterSelectionUIManager: UpdateSelectionFromController({entityType}, {selectionIndex}) called");
        
        // Update selection state
        UpdateSelectionState();
    }

    private void UpdateMySelectionVisuals()
    {
        // Update character selection border using controllers
        for (int i = 0; i < characterControllers.Count; i++)
        {
            EntitySelectionController controller = characterControllers[i];
            if (controller != null)
            {
                if (i == selectedCharacterIndex)
                {
                    controller.AddPlayerSelection(myPlayerID, myPlayerColor);
                }
                else
                {
                    controller.RemovePlayerSelection(myPlayerID);
                }
            }
        }
        
        // Update pet selection border using controllers
        for (int i = 0; i < petControllers.Count; i++)
        {
            EntitySelectionController controller = petControllers[i];
            if (controller != null)
            {
                if (i == selectedPetIndex)
                {
                    controller.AddPlayerSelection(myPlayerID, myPlayerColor);
                }
                else
                {
                    controller.RemovePlayerSelection(myPlayerID);
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
                
                // Update deck preview controller with ready state
                if (wasReady != isReady && deckPreviewController != null)
                {
                    deckPreviewController.SetPlayerReadyState(isReady);
                    Debug.Log($"CharacterSelectionUIManager: Player ready state changed to {isReady} - updated deck preview controller");
                }
                continue; // Skip showing selection indicators for self
            }
            
            Color playerColor = GetPlayerColor(info.playerName);
            
            // Show character selection using controllers
            if (info.hasSelection && info.characterIndex >= 0 && info.characterIndex < characterControllers.Count)
            {
                EntitySelectionController controller = characterControllers[info.characterIndex];
                if (controller != null)
                {
                    controller.AddPlayerSelection(info.playerName, playerColor);
                }
            }
            
            // Show pet selection using controllers
            if (info.hasSelection && info.petIndex >= 0 && info.petIndex < petControllers.Count)
            {
                EntitySelectionController controller = petControllers[info.petIndex];
                if (controller != null)
                {
                    controller.AddPlayerSelection(info.playerName, playerColor);
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
        // Clear character selections using controllers
        foreach (EntitySelectionController controller in characterControllers)
        {
            if (controller != null)
            {
                controller.ClearAllPlayerSelectionsExcept(myPlayerID);
            }
        }
        
        // Clear pet selections using controllers
        foreach (EntitySelectionController controller in petControllers)
        {
            if (controller != null)
            {
                controller.ClearAllPlayerSelectionsExcept(myPlayerID);
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
        Debug.Log($"CharacterSelectionUIManager: Cleaning up {characterItems.Count} character models and {petItems.Count} pet models");
        
        // Force cleanup on all controllers first to ensure proper cleanup of dynamically created models
        ForceCleanupAllControllers();
        
        // Cleanup character selection models (including NetworkObjects)
        foreach (GameObject item in characterItems)
        {
            if (item != null)
            {
                Debug.Log($"CharacterSelectionUIManager: Cleaning up character selection model: {item.name}");
                CleanupSelectionItem(item);
            }
        }
        characterItems.Clear();
        characterControllers.Clear();
        
        // Cleanup pet selection models (including NetworkObjects)
        foreach (GameObject item in petItems)
        {
            if (item != null)
            {
                Debug.Log($"CharacterSelectionUIManager: Cleaning up pet selection model: {item.name}");
                CleanupSelectionItem(item);
            }
        }
        petItems.Clear();
        petControllers.Clear();
        
        Debug.Log("CharacterSelectionUIManager: Selection model cleanup complete");
    }
    
    /// <summary>
    /// Properly cleans up a selection item, handling both regular GameObjects and NetworkObjects
    /// </summary>
    private void CleanupSelectionItem(GameObject item)
    {
        if (item == null) return;
        
        // Check if this is a selection NetworkObject
        SelectionNetworkObject selectionMarker = item.GetComponent<SelectionNetworkObject>();
        if (selectionMarker != null)
        {
            // Use the proper cleanup method for NetworkObjects
            selectionMarker.CleanupSelectionObject();
        }
        else
        {
            // Regular GameObject cleanup
            Destroy(item);
        }
    }
    
    /// <summary>
    /// Debug method to manually trigger cleanup from the inspector
    /// </summary>
    [ContextMenu("Force Cleanup Selection Models")]
    public void ForceCleanupSelectionModels()
    {
        CleanupSelectionModels();
    }

    /// <summary>
    /// Forces cleanup on all entity selection controllers before destroying them
    /// </summary>
    private void ForceCleanupAllControllers()
    {
        // Cleanup character controllers
        foreach (EntitySelectionController controller in characterControllers)
        {
            if (controller != null && controller.IsUsing3DModel())
            {
                // The controller's OnDestroy will handle cleanup, but we can call RefreshModel3DFromData 
                // with null to force cleanup of any dynamically created models
                try
                {
                    if (controller.GetModel3D() != null)
                    {
                        Debug.Log($"CharacterSelectionUIManager: Force cleaning model from character controller: {controller.name}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"CharacterSelectionUIManager: Error during character controller cleanup: {e.Message}");
                }
            }
        }
        
        // Cleanup pet controllers
        foreach (EntitySelectionController controller in petControllers)
        {
            if (controller != null && controller.IsUsing3DModel())
            {
                try
                {
                    if (controller.GetModel3D() != null)
                    {
                        Debug.Log($"CharacterSelectionUIManager: Force cleaning model from pet controller: {controller.name}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"CharacterSelectionUIManager: Error during pet controller cleanup: {e.Message}");
                }
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