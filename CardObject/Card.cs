using UnityEngine;
using UnityEngine.UI; // For Image
using TMPro; // For TextMeshProUGUI
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;

/// <summary>
/// Represents a card in the game, handling visual display and interaction.
/// Attach to: Card prefabs that will be instantiated for visual representation in the UI.
/// </summary>
public class Card : NetworkBehaviour
{
    [Header("Card Data")]
    [SerializeField] private NetworkEntity ownerEntity;
    // Use SyncVar<T> and subscribe to its OnChange event for synchronization
    private readonly SyncVar<int> _ownerEntityId = new SyncVar<int>();
    public NetworkEntity OwnerEntity => ownerEntity;
    private readonly SyncVar<int> _cardId = new SyncVar<int>();
    public int CardId => _cardId.Value;
    
    [Header("Network Ownership Info (Inspector Display)")]
    [SerializeField] private int inspectorNetworkOwnerClientId = -1;
    
    [Header("Card Properties")]
    private readonly SyncVar<bool> _isPurchasable = new SyncVar<bool>();
    public bool IsPurchasable => _isPurchasable.Value;
    
    private readonly SyncVar<bool> _isDraftable = new SyncVar<bool>();
    public bool IsDraftable => _isDraftable.Value;

    private readonly SyncVar<int> _purchaseCost = new SyncVar<int>();
    public int PurchaseCost => _purchaseCost.Value;
    
    [Header("Card State")]
    // Using SyncVar for card location and subscribing to its OnChange event
    private readonly SyncVar<CardLocation> _currentContainer = new SyncVar<CardLocation>();
    public CardLocation CurrentContainer => _currentContainer.Value;
    
    [Header("UI References (Assign in Prefab)")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image artworkImage;
    [SerializeField] private TextMeshProUGUI energyCostText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI initiativeText;
    [SerializeField] private Image cardImage; // Will handle both card artwork and background color

    // Local reference to card data (not synced over network)
    [SerializeField]
    protected CardData cardData;

    // Public quick accessors (optional, can just use cardData.PropertyName)
    public string CardName => cardData != null ? cardData.CardName : "No Data";
    public CardData CardData => cardData;

    // Add reference to SourceAndTargetIdentifier
    private SourceAndTargetIdentifier sourceAndTargetIdentifier;

    [Header("Components")]
    [SerializeField] private HandleCardPlay handleCardPlay;
    [SerializeField] private BoxCollider2D cardCollider;
    
    private void Awake()
    {
        // Setup and validate required components
        SetupRequiredComponents();
        
        // Subscribe to the OnChange event for _ownerEntityId
        _ownerEntityId.OnChange += OnOwnerEntityIdChanged;
        
        // Subscribe to the OnChange event for _currentContainer
        _currentContainer.OnChange += OnContainerChanged;
        
        // Subscribe to the OnChange event for _isDraftable
        _isDraftable.OnChange += OnDraftableChanged;
    }

    private void SetupRequiredComponents()
    {
        // Check and get/add SourceAndTargetIdentifier
        sourceAndTargetIdentifier = GetComponent<SourceAndTargetIdentifier>();
        if (sourceAndTargetIdentifier == null)
        {
            Debug.Log($"Card {gameObject.name}: Adding missing SourceAndTargetIdentifier component");
            sourceAndTargetIdentifier = gameObject.AddComponent<SourceAndTargetIdentifier>();
        }

        // Check and get/add HandleCardPlay
        handleCardPlay = GetComponent<HandleCardPlay>();
        if (handleCardPlay == null)
        {
            Debug.Log($"Card {gameObject.name}: Adding missing HandleCardPlay component");
            handleCardPlay = gameObject.AddComponent<HandleCardPlay>();
        }

        // Check and get/add CardTracker
        CardTracker cardTracker = GetComponent<CardTracker>();
        if (cardTracker == null)
        {
            /* Debug.Log($"Card {gameObject.name}: Adding missing CardTracker component"); */
            cardTracker = gameObject.AddComponent<CardTracker>();
        }
        
        // Check and get/add CardDragDrop for drag and drop functionality
        CardDragDrop cardDragDrop = GetComponent<CardDragDrop>();
        if (cardDragDrop == null)
        {
            /* Debug.Log($"Card {gameObject.name}: Adding missing CardDragDrop component"); */
            cardDragDrop = gameObject.AddComponent<CardDragDrop>();
        }
        
        // Check for collider for mouse interactions
        cardCollider = GetComponent<BoxCollider2D>();
        if (cardCollider == null)
        {
            // If we're not using a collider, we might be using UI elements
            var button = GetComponent<UnityEngine.UI.Button>();
            if (button == null)
            {
                // Only add a collider if we don't have a UI button AND no CardDragDrop
                // CardDragDrop uses UI events, so we don't need mouse colliders when it's present
                if (cardDragDrop == null)
                {
                    /* Debug.Log($"Card {gameObject.name}: Adding missing BoxCollider2D for mouse interactions"); */
                    cardCollider = gameObject.AddComponent<BoxCollider2D>();
                    // Set appropriate size
                    cardCollider.size = new Vector2(2f, 3f); // Default card size, adjust as needed
                    cardCollider.isTrigger = true;
                }
                else
                {
                    /* Debug.Log($"Card {gameObject.name}: CardDragDrop present - both UI events and mouse hover enabled"); */
                }
            }
        }
        else if (cardDragDrop != null)
        {
            // Keep CardDragDrop and BoxCollider2D both enabled
            // CardDragDrop will handle UI events, BoxCollider2D will handle mouse hover for damage previews
            /* Debug.Log($"Card {gameObject.name}: CardDragDrop present - both UI events and mouse hover enabled"); */
        }
        
        // If CardDragDrop is present, ensure we have UI components for proper event handling
        if (cardDragDrop != null)
        {
            // Ensure we have a RectTransform (required for UI events)
            RectTransform rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                /* Debug.Log($"Card {gameObject.name}: CardDragDrop present but no RectTransform - adding RectTransform for UI events"); */
                // Convert the Transform to a RectTransform if needed
                gameObject.AddComponent<RectTransform>();
            }
            
            // Ensure we have a CanvasGroup for alpha/raycast control
            CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                /* Debug.Log($"Card {gameObject.name}: Adding CanvasGroup for drag and drop visual effects"); */
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        _ownerEntityId.OnChange -= OnOwnerEntityIdChanged;
        _currentContainer.OnChange -= OnContainerChanged;
        _isDraftable.OnChange -= OnDraftableChanged;
    }

    private void OnContainerChanged(CardLocation oldValue, CardLocation newValue, bool asServer)
    {
        Debug.Log($"Card {gameObject.name}: Container changed from {oldValue} to {newValue}, asServer: {asServer}");
    }

    /// <summary>
    /// Initializes the card with the provided card data
    /// </summary>
    /// <param name="data">The CardData ScriptableObject containing the card's information</param>
    public void Initialize(CardData data)
    {
        if (data == null)
        {
            Debug.LogError($"[CARD-FLOW] Card {gameObject.name}: CRITICAL - Attempted to initialize with null CardData!");
            return;
        }

        Debug.Log($"[CARD-FLOW] Card {gameObject.name}: Initialized with {data.CardName} (ID: {data.CardId}, Cost: {data.EnergyCost})");

        cardData = data;
        _cardId.Value = data.CardId;

        // Update UI elements if they exist
        if (nameText != null) nameText.text = data.CardName;
        if (descriptionText != null) descriptionText.text = data.Description;
        if (energyCostText != null) energyCostText.text = data.EnergyCost.ToString();
        if (artworkImage != null && data.CardArtwork != null) artworkImage.sprite = data.CardArtwork;

        // Update initiative text (only show if initiative > 0)
        if (initiativeText != null)
        {
            initiativeText.gameObject.SetActive(data.Initiative > 0);
            if (data.Initiative > 0)
            {
                initiativeText.text = $"⚡{data.Initiative}";
            }
        }

        // Hide cost text by default (only shown in shop)
        if (costText != null) costText.gameObject.SetActive(false);
        
        // Initialize the HandleCardPlay component with this card data
        if (handleCardPlay != null)
        {
            /* Debug.Log($"Card {gameObject.name}: Initializing HandleCardPlay with card data"); */
            handleCardPlay.Initialize(data);
        }
    }

    /// <summary>
    /// Gets the card's image component for visual effects
    /// </summary>
    /// <returns>The Image component used for the card's visual representation</returns>
    public Image GetCardImage()
    {
        return cardImage;
    }

    /// <summary>
    /// Sets the logical owner of this card.
    /// Should be called by the spawning authority (e.g., CardSpawner) ON THE SERVER.
    /// </summary>
    /// <param name="entity">The NetworkEntity that owned this card.</param>
    [Server]
    public void SetOwnerEntity(NetworkEntity entity)
    {
        if (entity == null)
        {
            Debug.LogError($"Card {gameObject.name}: Attempted to set null owner entity on server!");
            _ownerEntityId.Value = 0; // Ensure it's reset if entity is null
            return;
        }

        Debug.Log($"[CARD_OWNERSHIP] Card {gameObject.name}: SetOwnerEntity called with entity '{entity.EntityName.Value}' (Type: {entity.EntityType}, ObjectId: {entity.GetComponent<NetworkObject>()?.ObjectId})");
        
        // Set the reference directly for server-side use
        ownerEntity = entity;
        
        // Set the synchronized ID to ensure clients can find it
        NetworkObject netObj = entity.GetComponent<NetworkObject>();
        int entityObjectId = netObj ? netObj.ObjectId : 0;
        
        // This will trigger the OnChange event on clients if the value changes
        _ownerEntityId.Value = entityObjectId; 
        
        // Update CardTracker's owner entity reference
        CardTracker cardTracker = GetComponent<CardTracker>();
        if (cardTracker != null)
        {
            Debug.Log($"[CARD_OWNERSHIP] Card {gameObject.name}: Updating CardTracker with owner entity '{entity.EntityName.Value}'");
            cardTracker.SetOwnerEntity(entity);
        }
        else
        {
            Debug.LogWarning($"[CARD_OWNERSHIP] Card {gameObject.name}: CardTracker component not found!");
        }
        
        Debug.Log($"[CARD_OWNERSHIP] Card {gameObject.name}: SetOwnerEntity completed for '{entity.EntityName.Value}'");
    }

    // SyncVar hook called when _ownerEntityId changes
    private void OnOwnerEntityIdChanged(int oldValue, int newValue, bool asServer)
    {
        /* Debug.Log($"Card {gameObject.name}: OnOwnerEntityIdChanged from {oldValue} to {newValue}, asServer: {asServer}"); */
        
        // On clients, when the ID changes (and it's not 0), try to find the entity reference.
        if (!asServer && newValue != 0)
        {
            // Check if we already have the correct reference or if the new ID is different
            if (ownerEntity == null || ownerEntity.GetComponent<NetworkObject>().ObjectId != newValue)
            {
                 Debug.Log($"Card {gameObject.name} (Client): _ownerEntityId changed to {newValue}. Attempting to find NetworkEntity.");
                FindOwnerEntityById(newValue);
            }
            else if (ownerEntity != null && ownerEntity.GetComponent<NetworkObject>().ObjectId == newValue)
            {
                Debug.Log($"Card {gameObject.name} (Client): _ownerEntityId changed to {newValue}, but ownerEntity is already correctly set.");
            }
        }
        else if (!asServer && newValue == 0)
        {
            Debug.Log($"Card {gameObject.name} (Client): _ownerEntityId changed to 0. Clearing ownerEntity reference.");
            ownerEntity = null;
            
            // Update CardTracker's owner entity reference
            CardTracker cardTracker = GetComponent<CardTracker>();
            if (cardTracker != null)
            {
                cardTracker.SetOwnerEntity(null);
            }
        }
    }

    /// <summary>
    /// Sets whether the card is purchasable and its cost
    /// </summary>
    public void SetPurchasable(bool purchasable, int cost = 0)
    {
        _isPurchasable.Value = purchasable;
        _purchaseCost.Value = cost;

        // Show/hide and update cost text if it exists
        if (costText != null)
        {
            costText.gameObject.SetActive(purchasable);
            if (purchasable) costText.text = cost.ToString();
        }
        
        // Add ShopCardSelection component when card becomes purchasable
        if (purchasable)
        {
            ShopCardSelection shopSelection = GetComponent<ShopCardSelection>();
            if (shopSelection == null)
            {
                shopSelection = gameObject.AddComponent<ShopCardSelection>();
                Debug.Log($"Card {gameObject.name}: Added ShopCardSelection component (purchasable = {purchasable})");
            }
        }
        else
        {
            // Remove ShopCardSelection component when card is no longer purchasable
            ShopCardSelection shopSelection = GetComponent<ShopCardSelection>();
            if (shopSelection != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(shopSelection);
                }
                else
                {
                    DestroyImmediate(shopSelection);
                }
                /* Debug.Log($"Card {gameObject.name}: Removed ShopCardSelection component (purchasable = {purchasable})"); */
            }
        }
    }

    /// <summary>
    /// Sets whether the card is draftable
    /// </summary>
    public void SetDraftable(bool draftable)
    {
        _isDraftable.Value = draftable;
        
        // Add DraftCardSelection component when card becomes draftable
        if (draftable)
        {
            DraftCardSelection draftSelection = GetComponent<DraftCardSelection>();
            if (draftSelection == null)
            {
                draftSelection = gameObject.AddComponent<DraftCardSelection>();
                Debug.Log($"Card {gameObject.name}: Added DraftCardSelection component (draftable = {draftable})");
            }
        }
        else
        {
            // Remove DraftCardSelection component when card is no longer draftable
            DraftCardSelection draftSelection = GetComponent<DraftCardSelection>();
            if (draftSelection != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(draftSelection);
                }
                else
                {
                    DestroyImmediate(draftSelection);
                }
                /* Debug.Log($"Card {gameObject.name}: Removed DraftCardSelection component (draftable = {draftable})"); */
            }
        }
    }

    /// <summary>
    /// Sets the current container location of the card
    /// </summary>
    public void SetCurrentContainer(CardLocation location)
    {
        Debug.Log($"Card {gameObject.name}: SetCurrentContainer called with location {location}, previous value was {_currentContainer.Value}");
        _currentContainer.Value = location;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        // Update inspector network owner ID
        inspectorNetworkOwnerClientId = Owner?.ClientId ?? -1;

        // Initialize SyncVar defaults on the server if needed, although CardSpawner should set it.
        if (_ownerEntityId.Value == 0 && ownerEntity != null)
        {
            // This case might happen if ownerEntity was assigned in prefab but not through SetOwnerEntity yet
            NetworkObject netObj = ownerEntity.GetComponent<NetworkObject>();
            _ownerEntityId.Value = netObj ? netObj.ObjectId : 0;
            Debug.Log($"Card {gameObject.name} (Server OnStartServer): Initialized _ownerEntityId to {_ownerEntityId.Value} from existing ownerEntity field.");
        }
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();

        // Update inspector network owner ID
        inspectorNetworkOwnerClientId = Owner?.ClientId ?? -1;

        /* Debug.Log($"Card {gameObject.name} (Client): OnStartClient BEGIN - Network Owner: {(Owner != null ? Owner.ClientId.ToString() : "null")}, IsNetworkOwner: {IsOwner}"); */
        /* Debug.Log($"Card {gameObject.name} (Client): - Current logical ownerEntity is {(ownerEntity != null ? $"SET to {ownerEntity.EntityName.Value}" : "NULL")}"); */
        /* Debug.Log($"Card {gameObject.name} (Client): - Current _ownerEntityId.Value is {_ownerEntityId.Value}"); */
        /* Debug.Log($"Card {gameObject.name} (Client): - Current _currentContainer.Value is {_currentContainer.Value}"); */

        // The OnChange event should handle finding the entity.
        // However, if the value was already set before this client connected (e.g. late join),
        // we might need to explicitly check and find it here too.
        if (_ownerEntityId.Value != 0 && ownerEntity == null)
        {
            Debug.Log($"Card {gameObject.name} (Client): OnStartClient - _ownerEntityId is {_ownerEntityId.Value} but ownerEntity is null. Attempting find.");
            FindOwnerEntityById(_ownerEntityId.Value);
        }
        else if (_ownerEntityId.Value != 0 && ownerEntity != null && ownerEntity.GetComponent<NetworkObject>().ObjectId != _ownerEntityId.Value)
        {
             /* Debug.Log($"Card {gameObject.name} (Client): OnStartClient - Mismatch! _ownerEntityId is {_ownerEntityId.Value} but current ownerEntity is {ownerEntity.GetComponent<NetworkObject>().ObjectId}. Attempting re-find."); */
             FindOwnerEntityById(_ownerEntityId.Value);
        }


        // If we have a card ID but no card data, try to load it from the database
        if (_cardId.Value != 0 && cardData == null)
        {
            // Try NetworkCardDatabase first (for randomized cards), then fallback to CardDatabase
            CardData foundCard = null;
            
            // Check if NetworkCardDatabase is available and has the card
            if (NetworkCardDatabase.Instance != null && NetworkCardDatabase.Instance.AreCardsSynced)
            {
                foundCard = NetworkCardDatabase.Instance.GetSyncedCard(_cardId.Value);
                Debug.Log($"[CARD-FLOW] Card {gameObject.name} (Client): NetworkDB lookup for ID {_cardId.Value} - " + 
                         (foundCard != null ? $"SUCCESS - Found {foundCard.CardName}" : "NOT FOUND"));
            }
            
            // If not found in NetworkCardDatabase, try original CardDatabase
            if (foundCard == null && CardDatabase.Instance != null)
            {
                foundCard = CardDatabase.Instance.GetCardById(_cardId.Value);
                Debug.Log($"[CARD-FLOW] Card {gameObject.name} (Client): CardDB fallback for ID {_cardId.Value} - " + 
                         (foundCard != null ? $"SUCCESS - Found {foundCard.CardName}" : "NOT FOUND"));
            }
            
            if (foundCard != null)
            {
                cardData = foundCard;
                Initialize(foundCard);
            }
            else
            {
                Debug.LogError($"[CARD-FLOW] Card {gameObject.name} (Client): CRITICAL - Card ID {_cardId.Value} not found in any database!");
            }
        }

        /* Debug.Log($"Card {gameObject.name} (Client): OnStartClient END - Final logical ownerEntity is {(ownerEntity != null ? $"SET to {ownerEntity.EntityName.Value}" : "NULL")}"); */
    }

    private void FindOwnerEntityById(int entityObjectId)
    {
        if (entityObjectId == 0) 
        {
            Debug.Log($"Card {gameObject.name}: FindOwnerEntityById - entityObjectId is 0. Clearing ownerEntity.");
            ownerEntity = null; // Clear if ID is 0
            
            // Update CardTracker's owner entity reference
            CardTracker cardTracker = GetComponent<CardTracker>();
            if (cardTracker != null)
            {
                cardTracker.SetOwnerEntity(null);
            }
            
            return;
        }

        /* Debug.Log($"Card {gameObject.name}: FindOwnerEntityById BEGIN - Looking for entity with ObjectId: {entityObjectId}"); */
        
        NetworkObject ownerObj = null;
        // It's safer to use InstanceFinder.ClientManager.Objects or ServerManager.Objects
        // depending on whether this code is running on client or server.
        // Since this is primarily for clients reacting to SyncVar changes, use ClientManager.
        // For server-side lookups (if any), use ServerManager.

        if (IsClientInitialized) // Check if we are on a client
        {
            bool found = FishNet.InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(entityObjectId, out ownerObj);
            Debug.Log($"Card {gameObject.name} (Client): - Client lookup for ObjectId {entityObjectId}. Found={found}");
        }
        // If this method could also be called on the server (e.g., via OnStartServer if needed):
        // else if (IsServerInitialized)
        // {
        //     bool found = FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(entityObjectId, out ownerObj);
        //     Debug.Log($"Card {gameObject.name} (Server): - Server lookup for ObjectId {entityObjectId}. Found={found}");
        // }


        if (ownerObj != null)
        {
            NetworkEntity foundEntity = ownerObj.GetComponent<NetworkEntity>();
            if (foundEntity != null)
            {
                ownerEntity = foundEntity;
                
                // Update CardTracker's owner entity reference
                CardTracker cardTracker = GetComponent<CardTracker>();
                if (cardTracker != null)
                {
                    cardTracker.SetOwnerEntity(foundEntity);
                }
                
                /* Debug.Log($"Card {gameObject.name}: - SUCCESS - Found and set ownerEntity to {ownerEntity.EntityName.Value} (GameObject: {ownerEntity.gameObject.name})"); */
            }
            else
            {
                Debug.LogWarning($"Card {gameObject.name}: - FAILURE - Found NetworkObject for ID {entityObjectId} but it has no NetworkEntity component.");
                ownerEntity = null; // Ensure ownerEntity is null if component is missing
            }
        }
        else
        {
            Debug.LogWarning($"Card {gameObject.name}: - FAILURE - Could not find NetworkObject with ID {entityObjectId}.");
            ownerEntity = null; // Ensure ownerEntity is null if object is not found
        }

        /* Debug.Log($"Card {gameObject.name}: FindOwnerEntityById END - Final ownerEntity is {(ownerEntity != null ? $"SET to {ownerEntity.EntityName.Value}" : "NULL")}"); */
    }

    private void OnMouseEnter()
    {
        // We don't need to call UpdateSourceAndTarget here anymore
        // The SourceAndTargetIdentifier component will handle this with its own OnMouseEnter
        
        // Still log this event for debugging
        Debug.Log($"Card {gameObject.name}: OnMouseEnter");
    }

    public void OnMouseDown()
    {
        /* Debug.Log($"Card {gameObject.name}: OnMouseDown called - Starting input handling"); */
        
        // Check the current game phase to determine container requirements
        GamePhaseManager gamePhaseManager = GamePhaseManager.Instance;
        bool isDraftPhase = gamePhaseManager != null && gamePhaseManager.GetCurrentPhase() == GamePhaseManager.GamePhase.Draft;
        
        Debug.Log($"Card {gameObject.name}: OnMouseDown - GamePhaseManager found: {gamePhaseManager != null}");
        if (gamePhaseManager != null)
        {
            Debug.Log($"Card {gameObject.name}: OnMouseDown - Current phase: {gamePhaseManager.GetCurrentPhase()}");
        }
        /* Debug.Log($"Card {gameObject.name}: OnMouseDown - isDraftPhase: {isDraftPhase}"); */
        /* Debug.Log($"Card {gameObject.name}: OnMouseDown - IsDraftable: {IsDraftable}"); */
        /* Debug.Log($"Card {gameObject.name}: OnMouseDown - IsPurchasable: {IsPurchasable}"); */
        /* Debug.Log($"Card {gameObject.name}: OnMouseDown - CurrentContainer: {CurrentContainer}"); */
        
        // Priority 1: Handle shop cards in draft phase
        if (isDraftPhase && IsPurchasable)
        {
            /* Debug.Log($"Card {gameObject.name}: OnMouseDown - Draft phase detected, card is purchasable. Delegating to ShopCardSelection."); */
            
            // For shop cards, delegate to the ShopCardSelection component
            ShopCardSelection shopSelection = GetComponent<ShopCardSelection>();
            if (shopSelection != null)
            {
                /* Debug.Log($"Card {gameObject.name}: OnMouseDown - Calling ShopCardSelection.OnCardClicked()"); */
                shopSelection.OnCardClicked();
            }
            else
            {
                Debug.LogError($"Card {gameObject.name}: OnMouseDown - Shop card but no ShopCardSelection component found!");
            }
            return;
        }
        
        // Priority 2: Handle draft cards in draft phase
        if (isDraftPhase && IsDraftable)
        {
            /* Debug.Log($"Card {gameObject.name}: OnMouseDown - Draft phase detected, card is draftable. Delegating to DraftCardSelection."); */
            
            // For draft cards, delegate to the DraftCardSelection component
            DraftCardSelection draftSelection = GetComponent<DraftCardSelection>();
            if (draftSelection != null)
            {
                /* Debug.Log($"Card {gameObject.name}: OnMouseDown - Calling DraftCardSelection.OnCardClicked()"); */
                draftSelection.OnCardClicked();
            }
            else
            {
                Debug.LogError($"Card {gameObject.name}: OnMouseDown - Draft card but no DraftCardSelection component found!");
            }
            return;
        }
        
        // Priority 3: Handle cards with CardDragDrop component (normal gameplay)
        CardDragDrop cardDragDrop = GetComponent<CardDragDrop>();
        if (cardDragDrop != null)
        {
            /* Debug.Log($"Card {gameObject.name}: OnMouseDown - CardDragDrop component present, delegating input handling"); */
            return;
        }
        
        // Priority 4: Handle normal card play
        /* Debug.Log($"Card {gameObject.name}: OnMouseDown - Continuing with normal ownership validation"); */
        
        // First check if card is in the correct container
        // In draft phase, draftable cards can be clicked regardless of container
        // In combat phase, cards must be in hand
        if (!isDraftPhase && CurrentContainer != CardLocation.Hand) 
        {
            /* Debug.Log($"Card {gameObject.name}: OnMouseDown - Cannot play. CurrentContainer: {CurrentContainer}. Expected Hand."); */
            return;
        }
        else if (isDraftPhase && !IsDraftable && !IsPurchasable)
        {
            /* Debug.Log($"Card {gameObject.name}: OnMouseDown - Cannot select in draft. Card is neither draftable nor purchasable."); */
            return;
        }
        
        // For non-draft cards, continue with normal ownership validation
        /* Debug.Log($"Card {gameObject.name}: OnMouseDown - Continuing with normal ownership validation for non-draft card"); */
        
        // Check logical owner (who the card belongs to in game terms)
        if (ownerEntity == null)
        {
            Debug.LogWarning($"Card {gameObject.name}: OnMouseDown - Cannot play. ownerEntity is null.");
            
            // Try to refresh the owner entity in case it wasn't properly initialized
            if (_ownerEntityId.Value != 0)
            {
                /* Debug.Log($"Card {gameObject.name}: Attempting to refresh ownerEntity from _ownerEntityId: {_ownerEntityId.Value}"); */
                FindOwnerEntityById(_ownerEntityId.Value);
                
                // If we found it, continue with the check
                if (ownerEntity == null)
                {
                    Debug.LogWarning($"Card {gameObject.name}: Still no ownerEntity after refresh attempt.");
                    return;
                }
            }
            else
            {
                return;
            }
        }
        
        // Check if network is properly initialized
        if (!IsNetworkInitialized())
        {
            Debug.LogWarning($"Card {gameObject.name}: Network not properly initialized. Delaying card play.");
            // Try again after a short delay
            StartCoroutine(RetryCardPlayAfterDelay(0.1f));
            return;
        }
        
        // Primary ownership check: Does the logical owner have network authority?
        bool canPlay = false;
        string ownershipStatus = "";
        
        if (ownerEntity.IsOwner)
        {
            // The logical owner has network authority - this is the ideal case
            canPlay = true;
            ownershipStatus = "Logical owner has network authority";
        }
        else if (IsOwner)
        {
            // The card's NetworkObject is owned by this client, but logical owner doesn't have authority
            // This can happen during transitions - check if we're the local player
            NetworkEntity localPlayer = GetLocalPlayer();
            if (localPlayer != null && localPlayer == ownerEntity)
            {
                canPlay = true;
                ownershipStatus = "Card network owner matches local player (transition state)";
            }
            else
            {
                ownershipStatus = $"Card network owned but logical owner ({ownerEntity.EntityName.Value}) is not local player";
            }
        }
        else
        {
            // Neither ownership check passed - this might be a timing issue
            NetworkEntity localPlayer = GetLocalPlayer();
            if (localPlayer != null && localPlayer == ownerEntity)
            {
                // The logical owner is the local player, but network ownership isn't established yet
                Debug.LogWarning($"Card {gameObject.name}: Network ownership not established but logical owner is local player. This might be a timing/focus issue.");
                
                // Log detailed debug info to help diagnose
                LogOwnershipDebugInfo();
                
                // Try again after a short delay to allow network state to stabilize
                StartCoroutine(RetryCardPlayAfterDelay(0.2f));
                return;
            }
            
            ownershipStatus = $"Neither card network ownership nor logical owner authority. Card IsOwner: {IsOwner}, ownerEntity.IsOwner: {ownerEntity.IsOwner}";
        }
        
        /* Debug.Log($"Card {gameObject.name}: OnMouseDown - Ownership check: {ownershipStatus}"); */
        
        if (!canPlay)
        {
            /* Debug.Log($"Card {gameObject.name}: OnMouseDown - Cannot play. {ownershipStatus}"); */
            
            // Log additional debug info for failed attempts
            LogOwnershipDebugInfo();
            return;
        }
        
        if (handleCardPlay != null)
        {
            /* Debug.Log($"Card {gameObject.name}: OnMouseDown - Ownership validated. Calling handleCardPlay.OnCardPlayAttempt()"); */
            
            // Ensure source and target are updated before playing the card
            if (sourceAndTargetIdentifier != null)
            {
                /* Debug.Log($"Card {gameObject.name}: OnMouseDown - Updating source and target before card play"); */
                sourceAndTargetIdentifier.UpdateSourceAndTarget();
            }
            
            handleCardPlay.OnCardPlayAttempt();
        }
        else
        {
            Debug.LogError($"Card {gameObject.name}: OnMouseDown - handleCardPlay is null!");
        }
    }
    
    /// <summary>
    /// Checks if the network is properly initialized for this card
    /// </summary>
    private bool IsNetworkInitialized()
    {
        // Check if FishNet is properly initialized
        if (FishNet.InstanceFinder.NetworkManager == null)
        {
            Debug.LogWarning($"Card {gameObject.name}: NetworkManager is null");
            return false;
        }
        
        // Check if we're properly connected
        var networkManager = FishNet.InstanceFinder.NetworkManager;
        if (!networkManager.IsClientStarted && !networkManager.IsServerStarted)
        {
            Debug.LogWarning($"Card {gameObject.name}: Neither client nor server is started");
            return false;
        }
        
        // Check if this NetworkObject is properly spawned
        if (!IsSpawned)
        {
            Debug.LogWarning($"Card {gameObject.name}: NetworkObject is not spawned");
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Retries card play after a delay to handle timing/focus issues
    /// </summary>
    private System.Collections.IEnumerator RetryCardPlayAfterDelay(float delay)
    {
        Debug.Log($"Card {gameObject.name}: Retrying card play after {delay} seconds...");
        yield return new WaitForSeconds(delay);
        
        // Try the card play again
        OnMouseDown();
    }
    
    /// <summary>
    /// Gets the local player entity (the one owned by this client)
    /// </summary>
    private NetworkEntity GetLocalPlayer()
    {
        NetworkEntity[] entities = FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        foreach (NetworkEntity entity in entities)
        {
            if (entity.EntityType == EntityType.Player && entity.IsOwner)
            {
                return entity;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Public method to check if this card can be played by the local player
    /// </summary>
    public bool CanBePlayedByLocalPlayer()
    {
        if (CurrentContainer != CardLocation.Hand) return false;
        if (ownerEntity == null) return false;
        
        if (ownerEntity.IsOwner) return true;
        
        if (IsOwner)
        {
            NetworkEntity localPlayer = GetLocalPlayer();
            return localPlayer != null && localPlayer == ownerEntity;
        }
        
        return false;
    }
    
    /// <summary>
    /// Debug method to log detailed ownership information
    /// </summary>
    public void LogOwnershipDebugInfo()
    {
        /* Debug.Log($"=== Card Ownership Debug Info for {gameObject.name} ==="); */
        /* Debug.Log($"CurrentContainer: {CurrentContainer}"); */
        /* Debug.Log($"Card NetworkObject IsOwner: {IsOwner}"); */
        /* Debug.Log($"Card NetworkObject Owner ClientId: {(Owner != null ? Owner.ClientId.ToString() : "null")}"); */
        /* Debug.Log($"ownerEntity: {(ownerEntity != null ? ownerEntity.EntityName.Value : "null")}"); */
        /* Debug.Log($"ownerEntity IsOwner: {(ownerEntity != null ? ownerEntity.IsOwner.ToString() : "N/A")}"); */
        /* Debug.Log($"ownerEntity Owner ClientId: {(ownerEntity?.Owner != null ? ownerEntity.Owner.ClientId.ToString() : "null")}"); */
        
        NetworkEntity localPlayer = GetLocalPlayer();
        Debug.Log($"Local Player: {(localPlayer != null ? localPlayer.EntityName.Value : "null")}");
        Debug.Log($"Local Player ClientId: {(localPlayer?.Owner != null ? localPlayer.Owner.ClientId.ToString() : "null")}");
        Debug.Log($"Can Be Played: {CanBePlayedByLocalPlayer()}");
        Debug.Log($"=== End Debug Info ===");
    }

    private void OnDraftableChanged(bool oldValue, bool newValue, bool asServer)
    {
        /* Debug.Log($"Card {gameObject.name}: OnDraftableChanged from {oldValue} to {newValue}, asServer: {asServer}"); */
        
        // Add or remove DraftCardSelection component based on draftable state
        if (newValue)
        {
            DraftCardSelection draftSelection = GetComponent<DraftCardSelection>();
            if (draftSelection == null)
            {
                draftSelection = gameObject.AddComponent<DraftCardSelection>();
                Debug.Log($"Card {gameObject.name}: Added DraftCardSelection component via OnDraftableChanged (asServer: {asServer})");
            }
        }
        else
        {
            DraftCardSelection draftSelection = GetComponent<DraftCardSelection>();
            if (draftSelection != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(draftSelection);
                }
                else
                {
                    DestroyImmediate(draftSelection);
                }
                /* Debug.Log($"Card {gameObject.name}: Removed DraftCardSelection component via OnDraftableChanged (asServer: {asServer})"); */
            }
        }
    }

    /// <summary>
    /// Update card data while preserving current state (for upgrades)
    /// </summary>
    /// <param name="newCardData">The new card data to apply</param>
    /// <param name="preserveState">Whether to preserve current card state</param>
    /// <returns>True if update was successful</returns>
    public bool UpdateCardData(CardData newCardData, bool preserveState = true)
    {
        if (newCardData == null)
        {
            Debug.LogError("Card: Cannot update with null card data");
            return false;
        }

        // Store current state if preserving
        CardLocation currentLocation = preserveState ? CurrentContainer : CardLocation.Deck;
        NetworkEntity currentOwner = preserveState ? ownerEntity : null;
        bool wasPurchasable = preserveState ? IsPurchasable : false;
        int oldCost = preserveState ? PurchaseCost : 0;

        // Update card data
        cardData = newCardData;
        _cardId.Value = newCardData.CardId;

        // Refresh visuals immediately
        RefreshVisuals();

        // Restore state if preserving
        if (preserveState)
        {
            SetCurrentContainer(currentLocation);
            if (currentOwner != null)
            {
                SetOwnerEntity(currentOwner);
            }
            if (wasPurchasable)
            {
                SetPurchasable(wasPurchasable, oldCost);
            }
        }

        Debug.Log($"Card: Successfully updated card data to {newCardData.CardName}");
        return true;
    }

    /// <summary>
    /// Refresh all visual elements to match current card data
    /// </summary>
    public void RefreshVisuals()
    {
        if (cardData == null)
        {
            Debug.LogWarning("Card: Cannot refresh visuals - no card data assigned");
            return;
        }

        // Update text elements
        if (nameText != null) nameText.text = cardData.CardName;
        if (descriptionText != null) descriptionText.text = cardData.Description;
        if (energyCostText != null) energyCostText.text = cardData.EnergyCost.ToString();

        // Update initiative text (only show if initiative > 0)
        if (initiativeText != null)
        {
            initiativeText.gameObject.SetActive(cardData.Initiative > 0);
            if (cardData.Initiative > 0)
            {
                initiativeText.text = $"⚡{cardData.Initiative}";
            }
        }

        // Update artwork
        if (artworkImage != null && cardData.CardArtwork != null)
        {
            artworkImage.sprite = cardData.CardArtwork;
        }

        // Update initiative text if it's greater than 0
        if (initiativeText != null)
        {
            initiativeText.gameObject.SetActive(cardData.Initiative > 0);
            if (cardData.Initiative > 0)
            {
                initiativeText.text = cardData.Initiative.ToString();
            }
        }

        // Update card background/frame based on card type or other properties
        UpdateCardVisualStyle();

        // Trigger any other visual updates
        UpdateCardAppearance();

        Debug.Log($"Card: Refreshed visuals for {cardData.CardName}");
    }

    /// <summary>
    /// Update card visual style based on card properties
    /// </summary>
    private void UpdateCardVisualStyle()
    {
        if (cardData == null || cardImage == null) return;

        // Example: Change card background color based on card type
        Color cardColor = Color.white;
        switch (cardData.CardType)
        {
            case CardType.Attack:
                cardColor = new Color(1f, 0.8f, 0.8f); // Light red
                break;
            case CardType.Skill:
                cardColor = new Color(0.8f, 1f, 0.8f); // Light green
                break;
            case CardType.Spell:
                cardColor = new Color(0.8f, 0.8f, 1f); // Light blue
                break;
            case CardType.Combo:
                cardColor = new Color(1f, 1f, 0.8f); // Light yellow
                break;
            case CardType.Finisher:
                cardColor = new Color(1f, 0.8f, 1f); // Light magenta
                break;
        }

        cardImage.color = cardColor;
    }

    /// <summary>
    /// Update card appearance (called during refresh and initialization)
    /// </summary>
    private void UpdateCardAppearance()
    {
        // Update any additional visual elements
        // This method can be extended for more complex visual updates
        
        // Example: Show/hide elements based on card properties
        if (cardData != null)
        {
            // Update energy cost visibility
            if (energyCostText != null)
            {
                energyCostText.gameObject.SetActive(cardData.EnergyCost > 0);
            }
            
            // Update initiative visibility
            if (initiativeText != null)
            {
                initiativeText.gameObject.SetActive(cardData.Initiative > 0);
            }
        }
    }

    /// <summary>
    /// Get current card state for preservation during upgrades
    /// </summary>
    public CardState GetCurrentState()
    {
        return new CardState
        {
            container = CurrentContainer,
            ownerEntityId = ownerEntity?.ObjectId ?? -1,
            isPurchasable = IsPurchasable,
            purchaseCost = PurchaseCost,
            isDraftable = IsDraftable,
            position = transform.position,
            rotation = transform.rotation,
            scale = transform.localScale
        };
    }

    /// <summary>
    /// Restore card state after upgrade
    /// </summary>
    public void RestoreState(CardState state)
    {
        if (state == null) return;

        SetCurrentContainer(state.container);
        
        if (state.ownerEntityId != -1)
        {
            // Find entity by ID and restore ownership
            var entities = FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
            foreach (var entity in entities)
            {
                if (entity.ObjectId == state.ownerEntityId)
                {
                    SetOwnerEntity(entity);
                    break;
                }
            }
        }

        if (state.isPurchasable)
        {
            SetPurchasable(state.isPurchasable, state.purchaseCost);
        }

        SetDraftable(state.isDraftable);

        // Restore transform
        // CENTRALIZED: Delegate transform restoration to HandLayoutManager if available
        HandLayoutManager handLayoutManager = GetComponentInParent<HandLayoutManager>();
        if (handLayoutManager != null)
        {
            handLayoutManager.SetCardTransformState(gameObject, state.position, state.scale, state.rotation);
            Debug.Log($"Card: CENTRALIZED state restoration via HandLayoutManager for {CardName}");
        }
        else
        {
            // Fallback for cards not in hand
            transform.position = state.position;
            transform.rotation = state.rotation;
            transform.localScale = state.scale;
            Debug.Log($"Card: FALLBACK state restoration directly for {CardName}");
        }
    }
}

/// <summary>
/// Data structure for preserving card state during upgrades
/// </summary>
[System.Serializable]
public class CardState
{
    public CardLocation container;
    public int ownerEntityId;
    public bool isPurchasable;
    public int purchaseCost;
    public bool isDraftable;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
}