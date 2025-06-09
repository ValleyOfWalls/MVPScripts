using UnityEngine;
using FishNet.Connection;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages the visibility of NetworkEntity objects based on game state.
/// Attach to: The same GameObject as GamePhaseManager to centralize visibility control.
/// </summary>
public class EntityVisibilityManager : MonoBehaviour
{
    [Header("Debug Options")]
    [SerializeField] private bool debugLogEnabled = true;
    
    // Cache all entities for easier management
    private List<NetworkEntity> allEntities = new List<NetworkEntity>();
    
    // Tracking current game state
    public enum GameState
    {
        Start,
        Lobby,
        CharacterSelection,
        Draft,
        Combat
    }
    
    // Current game state
    private GameState currentGameState = GameState.Lobby;
    
    // Start with all entities hidden in lobby
    private bool entitiesVisibleInLobby = false;
    
    // Reference to FightManager for combat visibility
    private FightManager fightManager;
    
    private void Awake()
    {
        TryFindFightManager();
    }
    
    private void TryFindFightManager()
    {
        if (fightManager == null)
        {
            fightManager = FindFirstObjectByType<FightManager>();
        }
    }
    
    private void Start()
    {
        LogDebug("EntityVisibilityManager started");
        
        // Initially set all entities to be hidden in lobby
        if (currentGameState == GameState.Lobby)
        {
            UpdateVisibilityForLobby();
        }
    }
    
    #region Registration Methods
    
    /// <summary>
    /// Register a NetworkEntity to be managed by this component
    /// </summary>
    public void RegisterEntity(NetworkEntity entity)
    {
        if (entity == null) return;
        
        if (!allEntities.Contains(entity))
        {
            allEntities.Add(entity);
            LogDebug($"Registered {entity.EntityType} entity: {entity.EntityName.Value}");
            
            // Update visibility based on current state
            UpdateEntityVisibility(entity);
        }
    }
    
    /// <summary>
    /// Unregister a NetworkEntity from management
    /// </summary>
    public void UnregisterEntity(NetworkEntity entity)
    {
        if (entity == null) return;
        
        if (allEntities.Remove(entity))
        {
            LogDebug($"Unregistered {entity.EntityType} entity: {entity.EntityName.Value}");
        }
    }
    
    #endregion
    
    #region State Management
    
    /// <summary>
    /// Set the current game state and update visibility accordingly
    /// </summary>
    public void SetGameState(GameState newState)
    {
        if (currentGameState == newState) 
        {
            LogDebug($"Game state already set to {newState}, skipping update");
            return;
        }
        
        GameState previousState = currentGameState;
        currentGameState = newState;
        LogDebug($"Game state changed from {previousState} to {newState}");
        
        switch (newState)
        {
            case GameState.Lobby:
                UpdateVisibilityForLobby();
                break;
            case GameState.CharacterSelection:
                UpdateVisibilityForCharacterSelection();
                break;
            case GameState.Draft:
                LogDebug("Updating visibility for Draft state");
                UpdateVisibilityForDraft();
                break;
            case GameState.Combat:
                UpdateVisibilityForCombat();
                break;
            default:
                UpdateAllEntitiesVisibility();
                break;
        }
    }
    
    /// <summary>
    /// Toggle visibility of entities in lobby
    /// </summary>
    public void SetLobbyVisibility(bool visible)
    {
        if (entitiesVisibleInLobby == visible) return;
        
        entitiesVisibleInLobby = visible;
        if (currentGameState == GameState.Lobby)
        {
            UpdateVisibilityForLobby();
        }
    }
    
    private void UpdateVisibilityForLobby()
    {
        foreach (var entity in allEntities)
        {
            if (entity != null)
            {
                var entityUI = entity.GetComponent<NetworkEntityUI>();
                if (entityUI != null)
                {
                    entityUI.SetVisible(entitiesVisibleInLobby);
                }
            }
        }
    }
    
    private void UpdateVisibilityForCharacterSelection()
    {
        // During character selection phase, hide all network entities until they are spawned with selections
        foreach (var entity in allEntities)
        {
            if (entity != null)
            {
                var entityUI = entity.GetComponent<NetworkEntityUI>();
                if (entityUI != null)
                {
                    entityUI.SetVisible(false);
                    LogDebug($"Hidden entity {entity.EntityName.Value} for character selection phase");
                }
            }
        }
        LogDebug("All entities hidden for character selection phase");
    }
    
    private void UpdateVisibilityForDraft()
    {
        // During draft phase, hide all network entities
        foreach (var entity in allEntities)
        {
            if (entity != null)
            {
                var entityUI = entity.GetComponent<NetworkEntityUI>();
                if (entityUI != null)
                {
                    entityUI.SetVisible(false);
                    LogDebug($"Hidden entity {entity.EntityName.Value} for draft phase");
                }
            }
        }
        LogDebug("All entities hidden for draft phase");
        
        // Update draft pack visibility to show only the local player's current pack
        UpdateDraftPackVisibility();
    }
    
    private void UpdateVisibilityForCombat()
    {
        TryFindFightManager();
        if (fightManager == null)
        {
            LogDebug("FightManager instance not found! Cannot update visibility for combat.");
            return;
        }
        
        // Use the viewed combat references from FightManager instead of local player's fight
        // This allows spectating to work by changing what fight is being viewed
        NetworkEntity viewedPlayer = fightManager.ViewedCombatPlayer;
        NetworkEntity viewedPet = fightManager.ViewedCombatOpponentPet;
        
        // IDs of entities in the currently viewed fight
        uint playerInFightId = 0;
        uint petInFightId = 0;
        
        if (viewedPlayer != null && viewedPet != null)
        {
            playerInFightId = (uint)viewedPlayer.ObjectId;
            petInFightId = (uint)viewedPet.ObjectId;
            LogDebug($"Combat participants (viewed fight) - Player ID: {playerInFightId}, Pet ID: {petInFightId}");
        }
        else
        {
            LogDebug("No viewed fight found in FightManager");
        }
        
        UpdateEntitiesVisibilityForCombat(playerInFightId, petInFightId);
        
        // Also update card visibility for combat
        UpdateAllCardVisibility();
    }
    
    private void UpdateEntitiesVisibilityForCombat(uint visiblePlayerId, uint visiblePetId)
    {
        foreach (var entity in allEntities)
        {
            if (entity == null) continue;
            
            bool shouldBeVisible = false;
            if (entity.EntityType == EntityType.Player)
            {
                shouldBeVisible = (uint)entity.ObjectId == visiblePlayerId;
            }
            else if (entity.EntityType == EntityType.Pet)
            {
                shouldBeVisible = (uint)entity.ObjectId == visiblePetId;
            }
            else if (entity.EntityType == EntityType.PlayerHand || entity.EntityType == EntityType.PetHand)
            {
                // Hand entities should be visible if their owner is in the viewed fight
                shouldBeVisible = IsHandEntityInViewedFight(entity, visiblePlayerId, visiblePetId);
            }
            
            var entityUI = entity.GetComponent<NetworkEntityUI>();
            if (entityUI != null)
            {
                entityUI.SetVisible(shouldBeVisible);
                LogDebug($"{entity.EntityType} {entity.EntityName.Value} (ID: {entity.ObjectId}): {(shouldBeVisible ? "Visible" : "Hidden")}");
            }
        }
    }
    
    /// <summary>
    /// Update visibility for a specific entity based on current game state
    /// </summary>
    private void UpdateEntityVisibility(NetworkEntity entity)
    {
        if (entity == null) return;
        
        var entityUI = entity.GetComponent<NetworkEntityUI>();
        if (entityUI == null) return;
        
        if (currentGameState == GameState.Lobby)
        {
            entityUI.SetVisible(entitiesVisibleInLobby);
        }
        else if (currentGameState == GameState.Combat)
        {
            // Use the viewed combat references from FightManager instead of local player's fight
            // This allows spectating to work by showing entities for the currently viewed fight
            TryFindFightManager();
            if (fightManager == null) return;
            
            NetworkEntity viewedPlayer = fightManager.ViewedCombatPlayer;
            NetworkEntity viewedPet = fightManager.ViewedCombatOpponentPet;
            
            if (viewedPlayer != null && viewedPet != null)
            {
                bool shouldBeVisible = false;
                if (entity.EntityType == EntityType.Player)
                {
                    shouldBeVisible = (uint)entity.ObjectId == (uint)viewedPlayer.ObjectId;
                }
                else if (entity.EntityType == EntityType.Pet)
                {
                    shouldBeVisible = (uint)entity.ObjectId == (uint)viewedPet.ObjectId;
                }
                else if (entity.EntityType == EntityType.PlayerHand || entity.EntityType == EntityType.PetHand)
                {
                    // Hand entities should be visible if their owner is in the viewed fight
                    shouldBeVisible = IsHandEntityInViewedFight(entity, (uint)viewedPlayer.ObjectId, (uint)viewedPet.ObjectId);
                }
                entityUI.SetVisible(shouldBeVisible);
            }
            else
            {
                entityUI.SetVisible(false);
            }
        }
    }
    
    /// <summary>
    /// Determines if a hand entity should be visible based on whether its owner is in the viewed fight
    /// </summary>
    private bool IsHandEntityInViewedFight(NetworkEntity handEntity, uint visiblePlayerId, uint visiblePetId)
    {
        if (handEntity == null) return false;
        
        // Find all entities to check for ownership relationships
        foreach (var entity in allEntities)
        {
            if (entity == null) continue;
            
            // Check if this entity has a relationship to the hand
            var relationshipManager = entity.GetComponent<RelationshipManager>();
            if (relationshipManager != null && relationshipManager.HandEntity != null)
            {
                var entityHand = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
                if (entityHand != null && (uint)entityHand.ObjectId == (uint)handEntity.ObjectId)
                {
                    // This entity owns the hand, check if the entity is in the viewed fight
                    uint entityId = (uint)entity.ObjectId;
                    return entityId == visiblePlayerId || entityId == visiblePetId;
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Update visibility for all registered entities
    /// </summary>
    public void UpdateAllEntitiesVisibility()
    {
        foreach (var entity in allEntities)
        {
            if (entity != null)
            {
                UpdateEntityVisibility(entity);
            }
        }
        
        // Also update card visibility
        UpdateAllCardVisibility();
    }
    
    /// <summary>
    /// Hides all registered entities regardless of current game state
    /// </summary>
    public void HideAllEntities()
    {
        foreach (var entity in allEntities)
        {
            if (entity != null)
            {
                var entityUI = entity.GetComponent<NetworkEntityUI>();
                if (entityUI != null)
                {
                    entityUI.SetVisible(false);
                }
            }
        }
        LogDebug("All entities forcibly hidden");
    }
    
    #endregion
    
    #region Card Visibility Management
    
    /// <summary>
    /// Applies combat-aware visibility filtering to a card
    /// Should be called whenever a card's visibility state changes
    /// </summary>
    public void ApplyCardVisibilityFilter(GameObject cardObject, bool serverRequestedState)
    {
        if (cardObject == null) return;
        
        Card card = cardObject.GetComponent<Card>();
        if (card == null) return;
        
        bool shouldBeVisible = ShouldCardBeVisibleToLocalClient(card);
        bool finalVisibility = serverRequestedState && shouldBeVisible;
        
        cardObject.SetActive(finalVisibility);
        
        LogDebug($"Card visibility filter applied: {cardObject.name} -> SetActive({finalVisibility}) (server: {serverRequestedState}, shouldBeVisible: {shouldBeVisible})");
    }
    
    /// <summary>
    /// Determines if a card should be visible to the local client based on current game state and combat assignments
    /// </summary>
    private bool ShouldCardBeVisibleToLocalClient(Card card)
    {
        if (card == null || card.OwnerEntity == null)
        {
            return false;
        }
        
        // During combat, use fight-based visibility
        if (currentGameState == GameState.Combat)
        {
            return ShouldCardBeVisibleInCombat(card);
        }
        
        // For other game states, use simple ownership
        return card.OwnerEntity.IsOwner;
    }
    
    /// <summary>
    /// Determines if a card should be visible during combat based on fight assignments
    /// </summary>
    private bool ShouldCardBeVisibleInCombat(Card card)
    {
        if (card == null || card.OwnerEntity == null)
        {
            return false;
        }
        
        // Get the fight manager to check combat assignments
        TryFindFightManager();
        if (fightManager == null)
        {
            // If no fight manager, fall back to simple ownership check
            return card.OwnerEntity.IsOwner;
        }
        
        // Use the viewed combat references from FightManager instead of local player's fight
        // This allows spectating to work by showing cards for the currently viewed fight
        NetworkEntity viewedPlayer = fightManager.ViewedCombatPlayer;
        NetworkEntity viewedPet = fightManager.ViewedCombatOpponentPet;
        
        if (viewedPlayer == null || viewedPet == null)
        {
            // If no viewed fight, fall back to simple ownership check
            return card.OwnerEntity.IsOwner;
        }
        
        // Get the main entity (Player/Pet) that owns this card
        NetworkEntity cardMainOwner = GetMainEntityForCard(card);
        if (cardMainOwner == null)
        {
            LogDebug($"Combat card visibility: Could not find main owner for card {card.gameObject.name}");
            return false;
        }
        
        // Check if the card's main owner is involved in the currently viewed fight
        uint cardMainOwnerObjectId = (uint)cardMainOwner.ObjectId;
        uint playerInFightId = (uint)viewedPlayer.ObjectId;
        uint opponentPetInFightId = (uint)viewedPet.ObjectId;
        
        // Card should be visible if its main owner is:
        // 1. The player in the viewed fight
        // 2. The opponent pet in the viewed fight
        bool shouldBeVisible = (cardMainOwnerObjectId == playerInFightId) || (cardMainOwnerObjectId == opponentPetInFightId);
        
        LogDebug($"Combat card visibility check: Card {card.gameObject.name} main owner: {cardMainOwner.EntityName.Value} (ID: {cardMainOwnerObjectId}), Player in viewed fight: {playerInFightId}, Opponent pet: {opponentPetInFightId}, Visible: {shouldBeVisible}");
        
        return shouldBeVisible;
    }
    
    /// <summary>
    /// Gets the main entity (Player/Pet) that owns a card, handling the case where cards are owned by Hand entities
    /// </summary>
    private NetworkEntity GetMainEntityForCard(Card card)
    {
        if (card == null || card.OwnerEntity == null)
        {
            return null;
        }
        
        NetworkEntity cardOwner = card.OwnerEntity;
        
        // If the card is owned by a Hand entity, find the main entity that owns the hand
        if (cardOwner.EntityType == EntityType.PlayerHand || cardOwner.EntityType == EntityType.PetHand)
        {
            // Search through all entities to find the one that has this hand
            foreach (var entity in allEntities)
            {
                if (entity != null && (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet))
                {
                    var relationshipManager = entity.GetComponent<RelationshipManager>();
                    if (relationshipManager != null && relationshipManager.HandEntity != null)
                    {
                        var handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
                        if (handEntity != null && (uint)handEntity.ObjectId == (uint)cardOwner.ObjectId)
                        {
                            // Found the main entity that owns this hand
                            return entity;
                        }
                    }
                }
            }
            
            LogDebug($"GetMainEntityForCard: Could not find main entity for hand {cardOwner.EntityName.Value} (ID: {cardOwner.ObjectId})");
            return null;
        }
        
        // If the card is owned by a main entity (Player/Pet), return it directly
        if (cardOwner.EntityType == EntityType.Player || cardOwner.EntityType == EntityType.Pet)
        {
            return cardOwner;
        }
        
        LogDebug($"GetMainEntityForCard: Unknown entity type {cardOwner.EntityType} for card owner {cardOwner.EntityName.Value}");
        return null;
    }
    
    /// <summary>
    /// Updates visibility for all cards belonging to registered entities
    /// Call this when game state changes or fight assignments change
    /// </summary>
    public void UpdateAllCardVisibility()
    {
        foreach (var entity in allEntities)
        {
            if (entity != null)
            {
                UpdateCardVisibilityForEntity(entity);
            }
        }
    }
    
    /// <summary>
    /// Updates visibility for all cards belonging to a specific entity
    /// </summary>
    public void UpdateCardVisibilityForEntity(NetworkEntity entity)
    {
        if (entity == null) return;
        
        // For main entities (Player/Pet), find their hand entity to get card transforms
        if (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet)
        {
            var relationshipManager = entity.GetComponent<RelationshipManager>();
            if (relationshipManager != null && relationshipManager.HandEntity != null)
            {
                var handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
                if (handEntity != null)
                {
                    // Update card visibility for the hand entity instead
                    UpdateCardVisibilityForEntity(handEntity);
                }
            }
            return;
        }
        
        // For Hand entities, get the transforms directly
        var entityUI = entity.GetComponent<NetworkEntityUI>();
        if (entityUI == null) return;
        
        var handTransform = entityUI.GetHandTransform();
        var deckTransform = entityUI.GetDeckTransform();
        var discardTransform = entityUI.GetDiscardTransform();
        
        // Update visibility for cards in each location
        UpdateCardVisibilityInTransform(handTransform, true);  // Hand cards should be visible when enabled
        UpdateCardVisibilityInTransform(deckTransform, false); // Deck cards should be hidden
        UpdateCardVisibilityInTransform(discardTransform, false); // Discard cards should be hidden
    }
    
    /// <summary>
    /// Updates card visibility for all cards in a specific transform
    /// </summary>
    private void UpdateCardVisibilityInTransform(Transform parentTransform, bool locationShouldBeVisible)
    {
        if (parentTransform == null) return;
        
        for (int i = 0; i < parentTransform.childCount; i++)
        {
            Transform childTransform = parentTransform.GetChild(i);
            if (childTransform != null && childTransform.gameObject != null)
            {
                ApplyCardVisibilityFilter(childTransform.gameObject, locationShouldBeVisible);
            }
        }
    }
    
    #endregion
    
    #region Draft Pack Visibility Management
    
    /// <summary>
    /// Updates visibility for all draft pack cards based on current pack ownership
    /// Call this when pack ownership changes during draft
    /// </summary>
    public void UpdateDraftPackVisibility()
    {
        Debug.Log($"[EntityVisibilityManager] UpdateDraftPackVisibility called - Current game state: {currentGameState}");
        LogDebug($"UpdateDraftPackVisibility called - Current game state: {currentGameState}");
        
        if (currentGameState != GameState.Draft)
        {
            Debug.Log($"[EntityVisibilityManager] Not in draft state, skipping draft pack visibility update");
            LogDebug($"Not in draft state, skipping draft pack visibility update");
            return;
        }
        
        // Find the local player
        NetworkEntity localPlayer = GetLocalPlayer();
        if (localPlayer == null)
        {
            Debug.Log("[EntityVisibilityManager] No local player found for draft pack visibility update");
            LogDebug("No local player found for draft pack visibility update");
            return;
        }
        
        Debug.Log($"[EntityVisibilityManager] Local player found: {localPlayer.EntityName.Value} (ID: {localPlayer.ObjectId})");
        LogDebug($"Local player found: {localPlayer.EntityName.Value} (ID: {localPlayer.ObjectId})");
        
        // Find the DraftManager to get the currently visible pack for the local player
        DraftManager draftManager = FindFirstObjectByType<DraftManager>();
        if (draftManager == null)
        {
            Debug.LogWarning("[EntityVisibilityManager] DraftManager not found, cannot determine currently visible pack");
            LogDebug("DraftManager not found, cannot determine currently visible pack");
            return;
        }
        
        // Find all draft packs
        DraftPack[] allDraftPacks = FindObjectsByType<DraftPack>(FindObjectsSortMode.None);
        Debug.Log($"[EntityVisibilityManager] Found {allDraftPacks.Length} draft packs");
        LogDebug($"Found {allDraftPacks.Length} draft packs");
        
        foreach (DraftPack pack in allDraftPacks)
        {
            if (pack == null || pack.CardContainer == null) 
            {
                Debug.Log($"[EntityVisibilityManager] Skipping null pack or pack with null CardContainer");
                LogDebug($"Skipping null pack or pack with null CardContainer");
                continue;
            }
            
            // Check if this pack is owned by the local player
            bool isOwnedByLocalPlayer = pack.IsOwnedBy(localPlayer);
            
            // Check if this pack contains cards that are selectable by the local player (i.e., it's the currently visible pack)
            bool isSelectableByLocalPlayer = false;
            List<GameObject> packCards = pack.GetCards();
            if (packCards.Count > 0)
            {
                // Check if any card in this pack is selectable by the local player
                isSelectableByLocalPlayer = packCards.Any(card => draftManager.IsCardSelectableByPlayer(card, localPlayer));
            }
            
            // A pack should be visible if it's owned by the local player AND it's selectable (currently visible)
            bool shouldBeVisible = isOwnedByLocalPlayer && isSelectableByLocalPlayer;
            
            Debug.Log($"[EntityVisibilityManager] Pack {pack.name}: owned={isOwnedByLocalPlayer}, selectable={isSelectableByLocalPlayer}, shouldBeVisible={shouldBeVisible} (CurrentOwnerPlayerId: {pack.CurrentOwnerPlayerId.Value}, LocalPlayer ObjectId: {localPlayer.ObjectId})");
            LogDebug($"Pack {pack.name}: owned={isOwnedByLocalPlayer}, selectable={isSelectableByLocalPlayer}, shouldBeVisible={shouldBeVisible} (CurrentOwnerPlayerId: {pack.CurrentOwnerPlayerId.Value}, LocalPlayer ObjectId: {localPlayer.ObjectId})");
            
            // Update visibility for all cards in this pack
            UpdateDraftPackCardVisibility(pack, shouldBeVisible);
        }
        
        Debug.Log($"[EntityVisibilityManager] Updated draft pack visibility for {allDraftPacks.Length} packs");
        LogDebug($"Updated draft pack visibility for {allDraftPacks.Length} packs");
    }
    
    /// <summary>
    /// Updates visibility for cards in a specific draft pack
    /// </summary>
    private void UpdateDraftPackCardVisibility(DraftPack pack, bool shouldBeVisible)
    {
        if (pack == null || pack.CardContainer == null) 
        {
            LogDebug("UpdateDraftPackCardVisibility: pack or CardContainer is null");
            return;
        }
        
        LogDebug($"UpdateDraftPackCardVisibility for pack {pack.name}: shouldBeVisible = {shouldBeVisible}, CardContainer child count = {pack.CardContainer.childCount}");
        
        // Update visibility for all cards in the pack's card container
        for (int i = 0; i < pack.CardContainer.childCount; i++)
        {
            Transform cardTransform = pack.CardContainer.GetChild(i);
            if (cardTransform != null && cardTransform.gameObject != null)
            {
                bool wasActive = cardTransform.gameObject.activeSelf;
                cardTransform.gameObject.SetActive(shouldBeVisible);
                LogDebug($"Draft pack card {cardTransform.gameObject.name} visibility changed from {wasActive} to {shouldBeVisible}");
            }
        }
    }
    
    /// <summary>
    /// Updates visibility for a specific draft pack when ownership changes
    /// </summary>
    public void UpdateDraftPackVisibilityForPack(DraftPack pack)
    {
        LogDebug($"UpdateDraftPackVisibilityForPack called for pack {(pack != null ? pack.name : "null")} - Current game state: {currentGameState}");
        
        if (currentGameState != GameState.Draft || pack == null)
        {
            LogDebug($"Skipping pack visibility update - not in draft state or pack is null");
            return;
        }
        
        // Find the local player
        NetworkEntity localPlayer = GetLocalPlayer();
        if (localPlayer == null)
        {
            LogDebug("No local player found for pack-specific visibility update");
            return;
        }
        
        LogDebug($"Local player: {localPlayer.EntityName.Value} (ID: {localPlayer.ObjectId})");
        
        // Find the DraftManager to check if cards are selectable
        DraftManager draftManager = FindFirstObjectByType<DraftManager>();
        if (draftManager == null)
        {
            LogDebug("DraftManager not found for pack-specific visibility update");
            return;
        }
        
        // Check if this pack is owned by the local player
        bool isOwnedByLocalPlayer = pack.IsOwnedBy(localPlayer);
        LogDebug($"Pack {pack.name} owned by local player: {isOwnedByLocalPlayer} (CurrentOwnerPlayerId: {pack.CurrentOwnerPlayerId.Value})");
        
        // Check if this pack contains cards that are selectable by the local player (i.e., it's the currently visible pack)
        bool isSelectableByLocalPlayer = false;
        List<GameObject> packCards = pack.GetCards();
        if (packCards.Count > 0)
        {
            // Check if any card in this pack is selectable by the local player
            isSelectableByLocalPlayer = packCards.Any(card => draftManager.IsCardSelectableByPlayer(card, localPlayer));
        }
        
        // A pack should be visible if it's owned by the local player AND it's selectable (currently visible)
        bool shouldBeVisible = isOwnedByLocalPlayer && isSelectableByLocalPlayer;
        
        LogDebug($"Pack {pack.name}: owned={isOwnedByLocalPlayer}, selectable={isSelectableByLocalPlayer}, shouldBeVisible={shouldBeVisible}");
        
        // Update visibility for all cards in this pack
        UpdateDraftPackCardVisibility(pack, shouldBeVisible);
        
        LogDebug($"Updated visibility for draft pack {pack.name}: {(shouldBeVisible ? "Visible" : "Hidden")}");
    }
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Get the local player's connection
    /// </summary>
    private NetworkConnection GetLocalPlayerConnection()
    {
        var localPlayer = GetLocalPlayer();
        return localPlayer?.Owner;
    }
    
    /// <summary>
    /// Get the local player entity
    /// </summary>
    private NetworkEntity GetLocalPlayer()
    {
        LogDebug($"GetLocalPlayer called - allEntities count: {allEntities.Count}");
        
        foreach (var entity in allEntities)
        {
            if (entity != null)
            {
                LogDebug($"Checking entity: {entity.EntityName.Value} (Type: {entity.EntityType}, IsOwner: {entity.IsOwner})");
                if (entity.EntityType == EntityType.Player && entity.IsOwner)
                {
                    LogDebug($"Found local player: {entity.EntityName.Value}");
                    return entity;
                }
            }
        }
        
        LogDebug("No local player found in allEntities");
        return null;
    }
    
    private void LogDebug(string message)
    {
        if (debugLogEnabled)
        {
            Debug.Log($"[EntityVisibilityManager] {message}");
        }
    }
    
    #endregion
} 