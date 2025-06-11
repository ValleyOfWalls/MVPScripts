using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using System.Collections;

/// <summary>
/// Manages the deck viewing functionality during combat, allowing players to view full deck contents.
/// Attach to: The CombatManager GameObject or CombatCanvas to handle deck viewing.
/// </summary>
public class DeckViewerManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject deckViewPanel;
    [SerializeField] private Transform deckViewContainer;
    [SerializeField] private Button closeDeckViewButton;
    [SerializeField] private TextMeshProUGUI deckViewTitle;
    [SerializeField] private ScrollRect deckViewScrollRect;
    
    [Header("Deck View Buttons")]
    [SerializeField] private Button viewMyDeckButton;
    [SerializeField] private Button viewOpponentDeckButton;
    [SerializeField] private Button viewAllyDeckButton;
    
    [Header("Card Display Settings")]
    [SerializeField] private Vector3 cardSpacing = new Vector3(1.2f, 0, 0);
    [SerializeField] private float cardScale = 0.8f;
    [SerializeField] private int cardsPerRow = 6;
    
    [Header("Debug")]
    [SerializeField] private bool debugLogEnabled = true;
    
    // Manager references
    private FightManager fightManager;
    private CombatCanvasManager combatCanvasManager;
    private EntityVisibilityManager entityVisibilityManager;
    private GamePhaseManager gamePhaseManager;
    
    // Currently displayed cards
    private List<GameObject> spawnedViewCards = new List<GameObject>();
    private NetworkEntity currentViewedDeckOwner;
    private bool isDeckViewOpen = false;
    private DeckType? currentlyViewedDeckType = null;
    
    // Card spawning
    private Dictionary<NetworkEntity, CardSpawner> entityCardSpawners = new Dictionary<NetworkEntity, CardSpawner>();

    private void Awake()
    {
        FindManagerReferences();
        ValidateComponents();
        
        // Hide deck viewer buttons immediately in Awake to ensure they start hidden
        SetDeckViewerButtonsVisible(false);
    }

    private void Start()
    {
        // Hide deck viewer buttons immediately (before network initialization)
        // This ensures they start hidden regardless of their default state in the scene
        SetDeckViewerButtonsVisible(false);
        
        // Don't setup full UI here - wait for network initialization
        SubscribeToGamePhaseChanges();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        SetupUI();
        CacheEntityCardSpawners();
        UpdateButtonVisibilityForCurrentPhase();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // UI setup is handled in OnStartClient, but cache spawners here too for server
        CacheEntityCardSpawners();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        UnsubscribeFromGamePhaseChanges();
        // Clean up any open deck views when network stops
        if (isDeckViewOpen)
        {
            CloseDeckView();
        }
    }

    private void Update()
    {
        // Allow Escape key to close deck view if open
        if (isDeckViewOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            LogDebug("Escape key pressed - closing deck view");
            CloseDeckView();
        }
    }

    #region Initialization

    private void FindManagerReferences()
    {
        if (fightManager == null)
        {
            fightManager = FindFirstObjectByType<FightManager>();
        }
        
        if (combatCanvasManager == null)
        {
            combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
        }
            
        if (entityVisibilityManager == null)
        {
            entityVisibilityManager = FindFirstObjectByType<EntityVisibilityManager>();
        }
        
        if (gamePhaseManager == null)
        {
            gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
        }
    }

    private void ValidateComponents()
    {
        if (deckViewPanel == null)
        {
            Debug.LogError("DeckViewerManager: deckViewPanel is not assigned!");
        }
        
        if (deckViewContainer == null)
        {
            Debug.LogError("DeckViewerManager: deckViewContainer is not assigned!");
        }
        
        if (fightManager == null)
        {
            Debug.LogError("DeckViewerManager: Could not find FightManager!");
        }
    }

    private void SetupUI()
    {
        // Initially hide the deck view panel (the overlay), but keep buttons enabled for phase control
        if (deckViewPanel != null)
        {
            deckViewPanel.SetActive(false);
        }
        
        // Hide deck viewer buttons by default - phase system will show them when appropriate
        SetDeckViewerButtonsVisible(false);
        
        // Setup button listeners
        if (viewMyDeckButton != null)
        {
            viewMyDeckButton.onClick.RemoveAllListeners();
            viewMyDeckButton.onClick.AddListener(() => ViewDeck(DeckType.MyDeck));
        }
        else
        {
            Debug.LogError("viewMyDeckButton is null - cannot setup listener!");
        }
        
        if (viewOpponentDeckButton != null)
        {
            viewOpponentDeckButton.onClick.RemoveAllListeners();
            viewOpponentDeckButton.onClick.AddListener(() => ViewDeck(DeckType.OpponentDeck));
        }
        else
        {
            Debug.LogError("viewOpponentDeckButton is null - cannot setup listener!");
        }
        
        if (viewAllyDeckButton != null)
        {
            viewAllyDeckButton.onClick.RemoveAllListeners();
            viewAllyDeckButton.onClick.AddListener(() => ViewDeck(DeckType.AllyDeck));
        }
        else
        {
            Debug.LogError("viewAllyDeckButton is null - cannot setup listener!");
        }
        
        if (closeDeckViewButton != null)
        {
            closeDeckViewButton.onClick.RemoveAllListeners();
            closeDeckViewButton.onClick.AddListener(() => CloseDeckView());
        }
    }

    private void CacheEntityCardSpawners()
    {
        // Find all NetworkEntities and cache their CardSpawners
        NetworkEntity[] entities = FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        
        foreach (var entity in entities)
        {
            // For Player/Pet entities, we need to find their HandEntity which contains the CardSpawner
            if (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet)
            {
                RelationshipManager relationshipManager = entity.GetComponent<RelationshipManager>();
                
                if (relationshipManager != null && relationshipManager.HandEntity != null)
                {
                    NetworkEntity handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
                    if (handEntity != null)
                    {
                        CardSpawner spawner = handEntity.GetComponent<CardSpawner>();
                        if (spawner != null)
                        {
                            // Cache the CardSpawner using the main entity as the key (Player/Pet),
                            // but the spawner is actually on the HandEntity
                            entityCardSpawners[entity] = spawner;
                        }
                    }
                }
            }
            else
            {
                // For other entity types (like Hand entities themselves), check if they have CardSpawner directly
                CardSpawner spawner = entity.GetComponent<CardSpawner>();
                if (spawner != null)
                {
                    entityCardSpawners[entity] = spawner;
                }
            }
        }
    }

    #endregion

    #region Public API

    public enum DeckType
    {
        MyDeck,
        OpponentDeck,
        AllyDeck
    }

    /// <summary>
    /// Views the specified deck type based on the currently viewed combat
    /// </summary>
    public void ViewDeck(DeckType deckType)
    {
        if (fightManager == null)
        {
            return;
        }

        // Check if we're clicking the same deck button when overlay is open - if so, close it
        if (isDeckViewOpen && currentlyViewedDeckType == deckType)
        {
            CloseDeckView();
            return;
        }

        // Get the entities from the currently viewed combat
        NetworkEntity viewedPlayer = fightManager.ViewedCombatPlayer;
        NetworkEntity viewedOpponentPet = fightManager.ViewedCombatOpponentPet;
        
        if (viewedPlayer == null || viewedOpponentPet == null)
        {
            return;
        }

        NetworkEntity targetEntity = null;
        string deckTitle = "";

        switch (deckType)
        {
            case DeckType.MyDeck:
                targetEntity = viewedPlayer;
                deckTitle = $"{viewedPlayer.EntityName.Value}'s Deck";
                break;
                
            case DeckType.OpponentDeck:
                targetEntity = viewedOpponentPet;
                deckTitle = $"{viewedOpponentPet.EntityName.Value}'s Deck";
                break;
                
            case DeckType.AllyDeck:
                // Find the ally pet for the viewed player
                RelationshipManager playerRelationship = viewedPlayer.GetComponent<RelationshipManager>();
                if (playerRelationship != null && playerRelationship.AllyEntity != null)
                {
                    targetEntity = playerRelationship.AllyEntity.GetComponent<NetworkEntity>();
                    if (targetEntity != null)
                    {
                        deckTitle = $"{targetEntity.EntityName.Value}'s Deck (Ally)";
                    }
                }
                
                if (targetEntity == null)
                {
                    return;
                }
                break;
        }

        if (targetEntity != null)
        {
            // Store which deck type we're viewing
            currentlyViewedDeckType = deckType;
            ShowDeckContents(targetEntity, deckTitle);
        }
    }

    /// <summary>
    /// Closes the deck view and cleans up spawned cards
    /// </summary>
    public void CloseDeckView()
    {
        CleanupSpawnedCards();
        
        if (deckViewPanel != null)
        {
            deckViewPanel.SetActive(false);
        }
        
        isDeckViewOpen = false;
        currentViewedDeckOwner = null;
        currentlyViewedDeckType = null;
    }

    /// <summary>
    /// Check if deck view is currently open
    /// </summary>
    public bool IsDeckViewOpen => isDeckViewOpen;

    /// <summary>
    /// Refreshes the CardSpawner cache - call this if HandEntities are spawned after initial caching
    /// </summary>
    public void RefreshCardSpawnerCache()
    {
        entityCardSpawners.Clear();
        CacheEntityCardSpawners();
    }

    /// <summary>
    /// Try to find CardSpawner for a specific entity, with cache refresh if not found
    /// </summary>
    private CardSpawner FindCardSpawnerForEntity(NetworkEntity entity)
    {
        // First try to get from cache
        if (entityCardSpawners.TryGetValue(entity, out CardSpawner cachedSpawner))
        {
            LogDebug($"Found cached CardSpawner for {entity.EntityName.Value}");
            return cachedSpawner;
        }

        LogDebug($"No cached CardSpawner for {entity.EntityName.Value} - trying direct lookup...");

        // Try direct lookup in case HandEntity was spawned after initial caching
        if (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet)
        {
            RelationshipManager relationshipManager = entity.GetComponent<RelationshipManager>();
            if (relationshipManager != null && relationshipManager.HandEntity != null)
            {
                NetworkEntity handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
                if (handEntity != null)
                {
                    CardSpawner spawner = handEntity.GetComponent<CardSpawner>();
                    if (spawner != null)
                    {
                        // Cache it for future use
                        entityCardSpawners[entity] = spawner;
                        LogDebug($"Found and cached CardSpawner for {entity.EntityName.Value} via direct lookup");
                        return spawner;
                    }
                }
            }
        }
        else
        {
            // For non-Player/Pet entities, check direct CardSpawner
            CardSpawner spawner = entity.GetComponent<CardSpawner>();
            if (spawner != null)
            {
                entityCardSpawners[entity] = spawner;
                LogDebug($"Found and cached direct CardSpawner for {entity.EntityName.Value}");
                return spawner;
            }
        }

        LogDebug($"Could not find CardSpawner for {entity.EntityName.Value} even with direct lookup");
        return null;
    }

    #endregion

    #region Deck Display Logic

    private void ShowDeckContents(NetworkEntity entity, string title)
    {
        LogDebug($"=== ShowDeckContents() called for {entity.EntityName.Value} ===");
        
        if (entity == null)
        {
            LogDebug("Cannot show deck contents - entity is null");
            return;
        }

        // Close any existing deck view first
        if (isDeckViewOpen)
        {
            LogDebug("Closing existing deck view first");
            CloseDeckView();
        }

        // Get the entity's deck
        NetworkEntityDeck entityDeck = entity.GetComponent<NetworkEntityDeck>();
        if (entityDeck == null)
        {
            LogDebug($"Cannot show deck contents - {entity.EntityName.Value} has no NetworkEntityDeck component");
            return;
        }

        // Get all card IDs from the deck
        List<int> cardIds = entityDeck.GetAllCardIds();
        LogDebug($"Entity deck has {cardIds.Count} cards");
        
        if (cardIds.Count == 0)
        {
            LogDebug($"Deck is empty for {entity.EntityName.Value}");
            ShowEmptyDeckMessage(title);
            return;
        }

        LogDebug($"Displaying deck for {entity.EntityName.Value} with {cardIds.Count} cards");

        // Set the title
        if (deckViewTitle != null)
        {
            deckViewTitle.text = title;
            LogDebug($"Set deck title to: {title}");
        }

        // Show the deck view panel
        if (deckViewPanel != null)
        {
            deckViewPanel.SetActive(true);
            LogDebug("DeckViewPanel set to active");
        }

        // Spawn and display cards
        LogDebug("Starting card spawn coroutine");
        StartCoroutine(SpawnDeckCardsCoroutine(entity, cardIds));

        isDeckViewOpen = true;
        currentViewedDeckOwner = entity;
        
        LogDebug($"=== ShowDeckContents() completed ===");
    }

    private void ShowEmptyDeckMessage(string title)
    {
        if (deckViewTitle != null)
            deckViewTitle.text = $"{title} (Empty)";
        
        if (deckViewPanel != null)
            deckViewPanel.SetActive(true);
        
        isDeckViewOpen = true;
        
        LogDebug("Showing empty deck message");
    }

    private IEnumerator SpawnDeckCardsCoroutine(NetworkEntity entity, List<int> cardIds)
    {
        // Get the card spawner for this entity (to get the card prefab reference)
        CardSpawner spawner = FindCardSpawnerForEntity(entity);
        if (spawner == null)
        {
            LogDebug($"No CardSpawner found for entity {entity.EntityName.Value}");
            yield break;
        }

        LogDebug($"Starting to spawn {cardIds.Count} cards for deck view using spawner on {spawner.gameObject.name}");

        // Get the card prefab from the spawner
        GameObject cardPrefab = GetCardPrefabFromSpawner(spawner);
        if (cardPrefab == null)
        {
            LogDebug("Could not get card prefab from CardSpawner");
            yield break;
        }

        for (int i = 0; i < cardIds.Count; i++)
        {
            // Get card data
            CardData cardData = CardDatabase.Instance?.GetCardById(cardIds[i]);
            if (cardData == null)
            {
                LogDebug($"Could not find card data for ID {cardIds[i]}");
                continue;
            }

            // Create local-only card representation for viewing
            GameObject cardObject = CreateLocalViewCard(cardPrefab, cardData);
            if (cardObject == null)
            {
                LogDebug($"Failed to create view card {cardData.CardName}");
                continue;
            }

            // Set scale after spawning
            cardObject.transform.localScale = Vector3.one * cardScale;

            // Parent to the deck view container - GridLayoutGroup will handle positioning
            cardObject.transform.SetParent(deckViewContainer, false); // Use false to maintain local positioning

            // Disable card interactions (make it view-only)
            DisableCardInteractions(cardObject);

            // Add to spawned cards list for cleanup
            spawnedViewCards.Add(cardObject);

            LogDebug($"Created deck view card: {cardData.CardName} (GridLayoutGroup will position)");

            // Small delay to prevent frame drops
            if (i % 5 == 0)
                yield return null;
        }

        LogDebug($"Finished creating {spawnedViewCards.Count} cards for deck view");

        // Wait a frame for GridLayoutGroup to layout the cards
        yield return null;

        // Update scroll rect content size if available
        UpdateScrollRectContent();
    }

    /// <summary>
    /// Gets the card prefab from a CardSpawner
    /// </summary>
    private GameObject GetCardPrefabFromSpawner(CardSpawner spawner)
    {
        try
        {
            // Try to use the public property first (if available)
            GameObject cardPrefab = spawner.CardPrefab;
            if (cardPrefab != null)
            {
                LogDebug($"Retrieved card prefab via public property: {cardPrefab.name}");
                return cardPrefab;
            }
            
            LogDebug("CardPrefab property returned null, trying reflection fallback...");
            
            // Fallback: Use reflection to access the private cardPrefab field
            var cardPrefabField = typeof(CardSpawner).GetField("cardPrefab", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (cardPrefabField != null)
            {
                cardPrefab = cardPrefabField.GetValue(spawner) as GameObject;
                LogDebug($"Retrieved card prefab via reflection: {(cardPrefab != null ? cardPrefab.name : "null")}");
                return cardPrefab;
            }
            else
            {
                LogDebug("Could not find cardPrefab field via reflection");
                return null;
            }
        }
        catch (System.Exception ex)
        {
            LogDebug($"Exception getting card prefab: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates a local-only card representation for deck viewing (not networked)
    /// </summary>
    private GameObject CreateLocalViewCard(GameObject cardPrefab, CardData cardData)
    {
        if (cardPrefab == null || cardData == null) return null;

        try
        {
            // Instantiate a local copy of the card prefab
            GameObject cardObject = Instantiate(cardPrefab);
            
            // Remove NetworkObject component if present (we don't want this networked)
            NetworkObject netObj = cardObject.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                DestroyImmediate(netObj);
                LogDebug($"Removed NetworkObject from view card {cardData.CardName}");
            }

            // Initialize the card with data
            Card card = cardObject.GetComponent<Card>();
            if (card != null)
            {
                card.Initialize(cardData);
                LogDebug($"Initialized view card {cardData.CardName} with data");
            }
            else
            {
                LogDebug($"Warning: Card component not found on view card {cardData.CardName}");
            }

            return cardObject;
        }
        catch (System.Exception ex)
        {
            LogDebug($"Exception creating local view card: {ex.Message}");
            return null;
        }
    }

    private void DisableCardInteractions(GameObject cardObject)
    {
        // Disable any interactive components on the card to make it view-only
        Button[] buttons = cardObject.GetComponentsInChildren<Button>();
        foreach (var button in buttons)
        {
            button.interactable = false;
        }

        // Disable colliders to prevent clicking
        Collider[] colliders = cardObject.GetComponentsInChildren<Collider>();
        foreach (var collider in colliders)
        {
            collider.enabled = false;
        }

        // You might want to add a visual indicator that this is a view-only card
        // For example, reduce alpha or add an overlay
    }

    private void UpdateScrollRectContent()
    {
        if (deckViewScrollRect == null || deckViewContainer == null) return;

        RectTransform contentRect = deckViewContainer.GetComponent<RectTransform>();
        if (contentRect == null) return;

        // Check if there's a GridLayoutGroup component
        GridLayoutGroup gridLayout = deckViewContainer.GetComponent<GridLayoutGroup>();
        if (gridLayout != null)
        {
            LogDebug("GridLayoutGroup found - letting it handle content sizing");
            
            // Wait for GridLayoutGroup to calculate layout, then force a rebuild
            Canvas.ForceUpdateCanvases();
            
            // Force the GridLayoutGroup to recalculate
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            
            LogDebug($"Content size after GridLayoutGroup rebuild: {contentRect.sizeDelta}");
        }
        else
        {
            // Fallback to manual calculation if no GridLayoutGroup
            LogDebug("No GridLayoutGroup found - using manual content size calculation");
            
            // Calculate content size based on spawned cards
            int totalRows = Mathf.CeilToInt((float)spawnedViewCards.Count / cardsPerRow);
            float contentHeight = totalRows * Mathf.Abs(cardSpacing.y);

            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, contentHeight);
            LogDebug($"Manual content size set to: {contentRect.sizeDelta}");
        }
    }

    #endregion

    #region Cleanup

    private void CleanupSpawnedCards()
    {
        LogDebug($"Cleaning up {spawnedViewCards.Count} spawned deck view cards");

        // Clean up all cards that were created for viewing
        foreach (var card in spawnedViewCards)
        {
            if (card != null)
            {
                // These are local-only cards, so we can destroy them directly
                // No need to use network despawning since they weren't networked
                Destroy(card);
                LogDebug($"Destroyed local view card: {card.name}");
            }
        }

        spawnedViewCards.Clear();
        LogDebug("Finished cleaning up deck view cards");
    }

    #endregion

    #region Lifecycle

    private void OnDestroy()
    {
        UnsubscribeFromGamePhaseChanges();
        CleanupSpawnedCards();
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        CleanupSpawnedCards();
    }

    #endregion

    #region Debug and Testing

    private void LogDebug(string message)
    {
        if (debugLogEnabled)
        {
            Debug.Log($"[DeckViewerManager] {message}");
        }
    }

    /// <summary>
    /// Test method to validate that all buttons are properly setup - call this from inspector or code
    /// </summary>
    [ContextMenu("Test Button Setup")]
    public void TestButtonSetup()
    {
        LogDebug("=== TESTING BUTTON SETUP ===");
        
        LogDebug($"DeckViewerManager enabled: {enabled}");
        LogDebug($"GameObject active: {gameObject.activeInHierarchy}");
        LogDebug($"Component enabled: {this.enabled}");
        
        // Test button references
        LogDebug($"viewMyDeckButton: {(viewMyDeckButton != null ? "ASSIGNED" : "NULL")}");
        if (viewMyDeckButton != null)
        {
            LogDebug($"  - Active: {viewMyDeckButton.gameObject.activeInHierarchy}");
            LogDebug($"  - Interactable: {viewMyDeckButton.interactable}");
            LogDebug($"  - Listener count: {viewMyDeckButton.onClick.GetPersistentEventCount()}");
        }
        
        LogDebug($"viewOpponentDeckButton: {(viewOpponentDeckButton != null ? "ASSIGNED" : "NULL")}");
        if (viewOpponentDeckButton != null)
        {
            LogDebug($"  - Active: {viewOpponentDeckButton.gameObject.activeInHierarchy}");
            LogDebug($"  - Interactable: {viewOpponentDeckButton.interactable}");
            LogDebug($"  - Listener count: {viewOpponentDeckButton.onClick.GetPersistentEventCount()}");
        }
        
        LogDebug($"viewAllyDeckButton: {(viewAllyDeckButton != null ? "ASSIGNED" : "NULL")}");
        if (viewAllyDeckButton != null)
        {
            LogDebug($"  - Active: {viewAllyDeckButton.gameObject.activeInHierarchy}");
            LogDebug($"  - Interactable: {viewAllyDeckButton.interactable}");
            LogDebug($"  - Listener count: {viewAllyDeckButton.onClick.GetPersistentEventCount()}");
        }
        
        // Test other UI components
        LogDebug($"deckViewPanel: {(deckViewPanel != null ? "ASSIGNED" : "NULL")}");
        if (deckViewPanel != null)
        {
            LogDebug($"  - Active: {deckViewPanel.activeInHierarchy}");
        }
        
        LogDebug($"FightManager: {(fightManager != null ? "FOUND" : "NULL")}");
        if (fightManager != null)
        {
            LogDebug($"  - ViewedCombatPlayer: {(fightManager.ViewedCombatPlayer != null ? fightManager.ViewedCombatPlayer.EntityName.Value : "null")}");
            LogDebug($"  - ViewedCombatOpponentPet: {(fightManager.ViewedCombatOpponentPet != null ? fightManager.ViewedCombatOpponentPet.EntityName.Value : "null")}");
        }
        
        LogDebug("=== BUTTON SETUP TEST COMPLETE ===");
    }

    /// <summary>
    /// Test method to manually trigger deck view - call this to test if the system works
    /// </summary>
    [ContextMenu("Test View My Deck")]
    public void TestViewMyDeck()
    {
        LogDebug("=== MANUAL TEST: View My Deck ===");
        ViewDeck(DeckType.MyDeck);
    }

    /// <summary>
    /// Test method to manually trigger opponent deck view
    /// </summary>
    [ContextMenu("Test View Opponent Deck")]
    public void TestViewOpponentDeck()
    {
        LogDebug("=== MANUAL TEST: View Opponent Deck ===");
        ViewDeck(DeckType.OpponentDeck);
    }

    /// <summary>
    /// Force button state update for testing
    /// </summary>
    [ContextMenu("Force Update Button States")]
    public void ForceUpdateButtonStates()
    {
        LogDebug("=== MANUAL TEST: Force Update Button States ===");
        UpdateButtonStates();
    }

    /// <summary>
    /// Manually setup UI - call this if Start() doesn't work due to NetworkBehaviour timing
    /// </summary>
    [ContextMenu("Manual Setup UI")]
    public void ManualSetupUI()
    {
        LogDebug("=== MANUAL SETUP UI CALLED ===");
        SetupUI();
        CacheEntityCardSpawners();
        LogDebug("=== MANUAL SETUP UI COMPLETE ===");
    }

    /// <summary>
    /// Check if initialization methods were called
    /// </summary>
    [ContextMenu("Check Initialization Status")]
    public void CheckInitializationStatus()
    {
        LogDebug("=== CHECKING INITIALIZATION STATUS ===");
        LogDebug($"IsServerInitialized: {IsServerInitialized}");
        LogDebug($"IsClientInitialized: {IsClientInitialized}");
        LogDebug($"IsOwner: {IsOwner}");
        LogDebug($"NetworkObject: {(NetworkObject != null ? "FOUND" : "NULL")}");
        if (NetworkObject != null)
        {
            LogDebug($"NetworkObject IsSpawned: {NetworkObject.IsSpawned}");
            LogDebug($"NetworkObject ObjectId: {NetworkObject.ObjectId}");
        }
        
        // Test if we can manually add a listener
        if (viewMyDeckButton != null)
        {
            LogDebug("Testing manual listener addition...");
            viewMyDeckButton.onClick.RemoveAllListeners();
            viewMyDeckButton.onClick.AddListener(() => {
                LogDebug("*** TEST LISTENER TRIGGERED! ***");
            });
            LogDebug($"After manual add - Listener count: {viewMyDeckButton.onClick.GetPersistentEventCount()}");
        }
        
        LogDebug("=== INITIALIZATION STATUS CHECK COMPLETE ===");
    }

    /// <summary>
    /// Comprehensive UI interaction debugging - call this to check for common button click issues
    /// </summary>
    [ContextMenu("Debug UI Interaction Issues")]
    public void DebugUIInteractionIssues()
    {
        LogDebug("=== DEBUGGING UI INTERACTION ISSUES ===");
        
        // Check EventSystem
        UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.EventSystems.EventSystem.current;
        LogDebug($"EventSystem present: {eventSystem != null}");
        if (eventSystem != null)
        {
            LogDebug($"EventSystem enabled: {eventSystem.enabled}");
            LogDebug($"EventSystem gameObject active: {eventSystem.gameObject.activeInHierarchy}");
        }
        
        // Check Canvas and GraphicRaycaster
        if (viewMyDeckButton != null)
        {
            Canvas canvas = viewMyDeckButton.GetComponentInParent<Canvas>();
            LogDebug($"Button Canvas found: {canvas != null}");
            if (canvas != null)
            {
                LogDebug($"Canvas enabled: {canvas.enabled}");
                LogDebug($"Canvas gameObject active: {canvas.gameObject.activeInHierarchy}");
                LogDebug($"Canvas renderMode: {canvas.renderMode}");
                
                UnityEngine.UI.GraphicRaycaster raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                LogDebug($"GraphicRaycaster present: {raycaster != null}");
                if (raycaster != null)
                {
                    LogDebug($"GraphicRaycaster enabled: {raycaster.enabled}");
                }
            }
            
            // Check button hierarchy and state
            LogDebug($"Button GameObject active: {viewMyDeckButton.gameObject.activeInHierarchy}");
            LogDebug($"Button component enabled: {viewMyDeckButton.enabled}");
            LogDebug($"Button interactable: {viewMyDeckButton.interactable}");
            LogDebug($"Button raycastTarget: {viewMyDeckButton.targetGraphic?.raycastTarget ?? false}");
            
            // Check if button is being blocked by other UI elements
            RectTransform buttonRect = viewMyDeckButton.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                Vector3[] corners = new Vector3[4];
                buttonRect.GetWorldCorners(corners);
                Vector3 center = (corners[0] + corners[2]) / 2;
                LogDebug($"Button world center position: {center}");
                LogDebug($"Button world size: {Vector3.Distance(corners[0], corners[2])}");
            }
        }
        
        LogDebug("=== UI INTERACTION DEBUG COMPLETE ===");
    }
    
    /// <summary>
    /// Test button click programmatically
    /// </summary>
    [ContextMenu("Test Button Click Programmatically")]
    public void TestButtonClickProgrammatically()
    {
        LogDebug("=== TESTING BUTTON CLICK PROGRAMMATICALLY ===");
        
        if (viewMyDeckButton != null)
        {
            LogDebug("Invoking button click manually...");
            viewMyDeckButton.onClick.Invoke();
            LogDebug("Manual button click invoked");
        }
        else
        {
            LogDebug("viewMyDeckButton is null - cannot test");
        }
        
        LogDebug("=== PROGRAMMATIC CLICK TEST COMPLETE ===");
    }

    /// <summary>
    /// Debug GridLayoutGroup setup and settings
    /// </summary>
    [ContextMenu("Debug GridLayoutGroup Setup")]
    public void DebugGridLayoutGroupSetup()
    {
        LogDebug("=== DEBUGGING GRIDLAYOUTGROUP SETUP ===");
        
        if (deckViewContainer == null)
        {
            LogDebug("deckViewContainer is null!");
            return;
        }
        
        LogDebug($"deckViewContainer: {deckViewContainer.name}");
        
        // Check for GridLayoutGroup
        GridLayoutGroup gridLayout = deckViewContainer.GetComponent<GridLayoutGroup>();
        if (gridLayout != null)
        {
            LogDebug("GridLayoutGroup found!");
            LogDebug($"  - Cell Size: {gridLayout.cellSize}");
            LogDebug($"  - Spacing: {gridLayout.spacing}");
            LogDebug($"  - Start Corner: {gridLayout.startCorner}");
            LogDebug($"  - Start Axis: {gridLayout.startAxis}");
            LogDebug($"  - Child Alignment: {gridLayout.childAlignment}");
            LogDebug($"  - Constraint: {gridLayout.constraint}");
            if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                LogDebug($"  - Constraint Count: {gridLayout.constraintCount}");
            LogDebug($"  - Enabled: {gridLayout.enabled}");
        }
        else
        {
            LogDebug("NO GridLayoutGroup found on deckViewContainer!");
        }
        
        // Check RectTransform
        RectTransform rectTransform = deckViewContainer.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            LogDebug($"RectTransform found:");
            LogDebug($"  - SizeDelta: {rectTransform.sizeDelta}");
            LogDebug($"  - AnchoredPosition: {rectTransform.anchoredPosition}");
            LogDebug($"  - Rect: {rectTransform.rect}");
            LogDebug($"  - Child Count: {rectTransform.childCount}");
        }
        else
        {
            LogDebug("NO RectTransform found on deckViewContainer!");
        }
        
        // Check ContentSizeFitter
        ContentSizeFitter sizeFitter = deckViewContainer.GetComponent<ContentSizeFitter>();
        if (sizeFitter != null)
        {
            LogDebug($"ContentSizeFitter found:");
            LogDebug($"  - Horizontal Fit: {sizeFitter.horizontalFit}");
            LogDebug($"  - Vertical Fit: {sizeFitter.verticalFit}");
            LogDebug($"  - Enabled: {sizeFitter.enabled}");
        }
        else
        {
            LogDebug("No ContentSizeFitter found");
        }
        
        // Check parent ScrollRect
        if (deckViewScrollRect != null)
        {
            LogDebug($"ScrollRect found:");
            LogDebug($"  - Content: {(deckViewScrollRect.content != null ? deckViewScrollRect.content.name : "null")}");
            LogDebug($"  - Viewport: {(deckViewScrollRect.viewport != null ? deckViewScrollRect.viewport.name : "null")}");
            LogDebug($"  - Vertical: {deckViewScrollRect.vertical}");
            LogDebug($"  - Horizontal: {deckViewScrollRect.horizontal}");
        }
        else
        {
            LogDebug("deckViewScrollRect is null!");
        }
        
        LogDebug("=== GRIDLAYOUTGROUP DEBUG COMPLETE ===");
    }

    /// <summary>
    /// Manually refresh CardSpawner cache for debugging
    /// </summary>
    [ContextMenu("Refresh CardSpawner Cache")]
    public void ManualRefreshCardSpawnerCache()
    {
        LogDebug("=== MANUAL REFRESH CardSpawner CACHE ===");
        RefreshCardSpawnerCache();
    }

    #endregion

    #region Button State Management

    /// <summary>
    /// Updates button states based on available entities in the current viewed combat
    /// </summary>
    public void UpdateButtonStates()
    {
        if (fightManager == null) return;

        NetworkEntity viewedPlayer = fightManager.ViewedCombatPlayer;
        NetworkEntity viewedOpponentPet = fightManager.ViewedCombatOpponentPet;

        // Update My Deck button
        if (viewMyDeckButton != null)
        {
            bool hasPlayerDeck = viewedPlayer != null && viewedPlayer.GetComponent<NetworkEntityDeck>() != null;
            viewMyDeckButton.interactable = hasPlayerDeck;
            
            var buttonText = viewMyDeckButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = viewedPlayer != null ? $"View {viewedPlayer.EntityName.Value}'s Deck" : "View My Deck";
            }
        }

        // Update Opponent Deck button
        if (viewOpponentDeckButton != null)
        {
            bool hasOpponentDeck = viewedOpponentPet != null && viewedOpponentPet.GetComponent<NetworkEntityDeck>() != null;
            viewOpponentDeckButton.interactable = hasOpponentDeck;
            
            var buttonText = viewOpponentDeckButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = viewedOpponentPet != null ? $"View {viewedOpponentPet.EntityName.Value}'s Deck" : "View Opponent Deck";
            }
        }

        // Update Ally Deck button
        if (viewAllyDeckButton != null)
        {
            NetworkEntity allyEntity = null;
            if (viewedPlayer != null)
            {
                RelationshipManager playerRelationship = viewedPlayer.GetComponent<RelationshipManager>();
                if (playerRelationship != null && playerRelationship.AllyEntity != null)
                {
                    allyEntity = playerRelationship.AllyEntity.GetComponent<NetworkEntity>();
                }
            }

            bool hasAllyDeck = allyEntity != null && allyEntity.GetComponent<NetworkEntityDeck>() != null;
            viewAllyDeckButton.interactable = hasAllyDeck;
            
            var buttonText = viewAllyDeckButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = allyEntity != null ? $"View {allyEntity.EntityName.Value}'s Deck" : "View Ally Deck";
            }
        }

        LogDebug($"Updated button states - Player: {viewedPlayer?.EntityName.Value}, Opponent: {viewedOpponentPet?.EntityName.Value}, Ally: {GetAllyName(viewedPlayer)}");
    }

    private string GetAllyName(NetworkEntity player)
    {
        if (player == null) return "None";
        
        RelationshipManager relationship = player.GetComponent<RelationshipManager>();
        if (relationship?.AllyEntity != null)
        {
            NetworkEntity ally = relationship.AllyEntity.GetComponent<NetworkEntity>();
            return ally?.EntityName.Value ?? "Unknown";
        }
        
        return "None";
    }

    #endregion

    #region Game Phase Integration

    /// <summary>
    /// Subscribe to game phase changes to show/hide buttons appropriately
    /// </summary>
    private void SubscribeToGamePhaseChanges()
    {
        LogDebug("Subscribing to game phase changes...");
        
        if (gamePhaseManager != null)
        {
            gamePhaseManager.OnPhaseChanged += OnGamePhaseChanged;
            LogDebug("Successfully subscribed to GamePhaseManager.OnPhaseChanged");
        }
        else
        {
            LogDebug("GamePhaseManager not found - cannot subscribe to phase changes");
        }
    }

    /// <summary>
    /// Unsubscribe from game phase changes
    /// </summary>
    private void UnsubscribeFromGamePhaseChanges()
    {
        LogDebug("Unsubscribing from game phase changes...");
        
        if (gamePhaseManager != null)
        {
            gamePhaseManager.OnPhaseChanged -= OnGamePhaseChanged;
            LogDebug("Successfully unsubscribed from GamePhaseManager.OnPhaseChanged");
        }
    }

    /// <summary>
    /// Called when the game phase changes
    /// </summary>
    private void OnGamePhaseChanged(GamePhaseManager.GamePhase newPhase)
    {
        LogDebug($"Game phase changed to: {newPhase}");
        UpdateButtonVisibilityForPhase(newPhase);
    }

    /// <summary>
    /// Updates button visibility based on current game phase from GamePhaseManager
    /// </summary>
    private void UpdateButtonVisibilityForCurrentPhase()
    {
        if (gamePhaseManager != null)
        {
            GamePhaseManager.GamePhase currentPhase = gamePhaseManager.GetCurrentPhase();
            LogDebug($"Updating button visibility for current phase: {currentPhase}");
            UpdateButtonVisibilityForPhase(currentPhase);
        }
        else
        {
            LogDebug("GamePhaseManager not found - hiding deck viewer buttons by default");
            SetDeckViewerButtonsVisible(false);
        }
    }

    /// <summary>
    /// Updates button visibility based on the specified game phase
    /// </summary>
    private void UpdateButtonVisibilityForPhase(GamePhaseManager.GamePhase phase)
    {
        bool shouldShowButtons = phase == GamePhaseManager.GamePhase.Combat || 
                                phase == GamePhaseManager.GamePhase.Draft;
        
        LogDebug($"Phase {phase}: Setting deck viewer buttons visible = {shouldShowButtons}");
        SetDeckViewerButtonsVisible(shouldShowButtons);
        
        // If we're hiding buttons and deck view is open, close it
        if (!shouldShowButtons && isDeckViewOpen)
        {
            LogDebug("Phase changed to non-deck-viewing phase while deck view was open - closing deck view");
            CloseDeckView();
        }
    }

    /// <summary>
    /// Sets the visibility of all deck viewer buttons
    /// </summary>
    private void SetDeckViewerButtonsVisible(bool visible)
    {
        LogDebug($"=== SetDeckViewerButtonsVisible({visible}) called ===");
        
        if (viewMyDeckButton != null)
        {
            LogDebug($"Setting viewMyDeckButton from {viewMyDeckButton.gameObject.activeInHierarchy} to {visible}");
            viewMyDeckButton.gameObject.SetActive(visible);
            LogDebug($"viewMyDeckButton visibility now: {viewMyDeckButton.gameObject.activeInHierarchy}");
        }
        else
        {
            LogDebug("viewMyDeckButton is null!");
        }
        
        if (viewOpponentDeckButton != null)
        {
            LogDebug($"Setting viewOpponentDeckButton from {viewOpponentDeckButton.gameObject.activeInHierarchy} to {visible}");
            viewOpponentDeckButton.gameObject.SetActive(visible);
            LogDebug($"viewOpponentDeckButton visibility now: {viewOpponentDeckButton.gameObject.activeInHierarchy}");
        }
        else
        {
            LogDebug("viewOpponentDeckButton is null!");
        }
        
        if (viewAllyDeckButton != null)
        {
            LogDebug($"Setting viewAllyDeckButton from {viewAllyDeckButton.gameObject.activeInHierarchy} to {visible}");
            viewAllyDeckButton.gameObject.SetActive(visible);
            LogDebug($"viewAllyDeckButton visibility now: {viewAllyDeckButton.gameObject.activeInHierarchy}");
        }
        else
        {
            LogDebug("viewAllyDeckButton is null!");
        }
        
        LogDebug($"=== SetDeckViewerButtonsVisible({visible}) completed ===");
    }

    #endregion
} 