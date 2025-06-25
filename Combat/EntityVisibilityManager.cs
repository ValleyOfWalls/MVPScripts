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
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Multiple EntityVisibilityManager instances found! Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        
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
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
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
        /* Debug.Log("[ENTITY_VISIBILITY] UpdateVisibilityForCombat called"); */
        TryFindFightManager();
        if (fightManager == null)
        {
            Debug.LogError("[ENTITY_VISIBILITY] FightManager instance not found! Cannot update visibility for combat.");
            LogDebug("FightManager instance not found! Cannot update visibility for combat.");
            return;
        }
        
        // Get all fight assignments for debugging
        var allFights = fightManager.GetAllFightAssignments();
        /* Debug.Log($"[ENTITY_VISIBILITY] Total fight assignments: {allFights.Count}"); */
        
        // Don't update visibility if there are no fight assignments yet - FightManager will notify us when ready
        if (allFights.Count == 0)
        {
            Debug.Log("[ENTITY_VISIBILITY] No fight assignments yet - waiting for FightManager to be ready");
            LogDebug("No fight assignments yet - waiting for FightManager to be ready");
            return;
        }
        
        // Get all entities involved in the currently viewed fight
        List<NetworkEntity> viewedFightEntities = fightManager.GetViewedFightEntities();
        
        if (viewedFightEntities == null || viewedFightEntities.Count == 0)
        {
            Debug.LogWarning("[ENTITY_VISIBILITY] No viewed fight entities found in FightManager despite having fight assignments");
            LogDebug("No viewed fight entities found in FightManager despite having fight assignments");
            
            // Hide all entities if no fight is being viewed despite having assignments
            HideAllEntities();
            return;
        }
        
        LogDebug($"Combat participants (viewed fight) - Total entities: {viewedFightEntities.Count}");
        
        // Update visibility for all entities using the unified system
        foreach (var entity in allEntities)
        {
            if (entity != null)
            {
                UpdateEntityVisibility(entity);
            }
        }
        
        // Also update card visibility for combat
        UpdateAllCardVisibility();
    }
    
    /// <summary>
    /// Called by FightManager when combat assignments are ready and visibility should be updated
    /// </summary>
    public void OnFightManagerReady()
    {
        /* Debug.Log("[ENTITY_VISIBILITY] OnFightManagerReady called - updating combat visibility"); */
        LogDebug("FightManager is ready - updating combat visibility");
        
        if (currentGameState == GameState.Combat)
        {
            UpdateVisibilityForCombat();
        }
        else
        {
            Debug.LogWarning($"[ENTITY_VISIBILITY] OnFightManagerReady called but not in combat state (current: {currentGameState})");
        }
    }

    
    /// <summary>
    /// Gets the local player's own pet (the pet they control)
    /// </summary>
    private NetworkEntity GetLocalPlayerOwnPet(NetworkEntity localPlayer)
    {
        if (localPlayer == null) return null;
        
        var relationshipManager = localPlayer.GetComponent<RelationshipManager>();
        if (relationshipManager == null || relationshipManager.AllyEntity == null)
        {
            return null;
        }
        
        return relationshipManager.AllyEntity.GetComponent<NetworkEntity>();
    }

    /// <summary>
    /// Gets the viewed player's own pet (the pet they control in the currently viewed fight)
    /// </summary>
    private NetworkEntity GetViewedPlayerOwnPet()
    {
        TryFindFightManager();
        if (fightManager == null) return null;
        
        NetworkEntity viewedPlayer = fightManager.ViewedCombatPlayer;
        if (viewedPlayer == null) return null;
        
        return GetLocalPlayerOwnPet(viewedPlayer);
    }

    /// <summary>
    /// Checks if a hand entity is owned by a specific entity
    /// </summary>
    private bool IsHandOwnedByEntity(NetworkEntity handEntity, NetworkEntity ownerEntity)
    {
        if (handEntity == null || ownerEntity == null) return false;
        
        var relationshipManager = ownerEntity.GetComponent<RelationshipManager>();
        if (relationshipManager == null || relationshipManager.HandEntity == null)
        {
            return false;
        }
        
        var entityHand = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
        return entityHand != null && entityHand.ObjectId == handEntity.ObjectId;
    }

    /// <summary>
    /// Checks if a stats UI entity is owned by a specific entity
    /// </summary>
    private bool IsStatsUIOwnedByEntity(NetworkEntity statsUIEntity, NetworkEntity ownerEntity)
    {
        if (statsUIEntity == null || ownerEntity == null) return false;
        
        var relationshipManager = ownerEntity.GetComponent<RelationshipManager>();
        if (relationshipManager == null || relationshipManager.StatsUIEntity == null)
        {
            return false;
        }
        
        var entityStatsUI = relationshipManager.StatsUIEntity.GetComponent<NetworkEntity>();
        return entityStatsUI != null && entityStatsUI.ObjectId == statsUIEntity.ObjectId;
    }
    
    /// <summary>
    /// Update visibility for a specific entity based on current game state
    /// </summary>
    private void UpdateEntityVisibility(NetworkEntity entity)
    {
        if (entity == null) return;
        
        // Handle stats UI entities specially
        if (entity.EntityType == EntityType.PlayerStatsUI || entity.EntityType == EntityType.PetStatsUI)
        {
            var statsUIController = entity.GetComponent<EntityStatsUIController>();
            if (statsUIController == null) return;
            
            if (currentGameState == GameState.Lobby)
            {
                statsUIController.SetVisible(entitiesVisibleInLobby);
            }
            else if (currentGameState == GameState.Combat)
            {
                TryFindFightManager();
                if (fightManager == null) return;

                // Get the local player and their own pet
                NetworkEntity currentLocalPlayer = GetLocalPlayer();
                NetworkEntity currentLocalPlayerOwnPet = GetLocalPlayerOwnPet(currentLocalPlayer);

                // Hide stats UI for local player's own pet (handled by OwnPetView)
                // Only hide if we're currently viewing the local player's fight
                NetworkEntity viewedPlayer = fightManager.ViewedCombatPlayer;
                bool isViewingLocalPlayerFight = (currentLocalPlayer != null && viewedPlayer != null && currentLocalPlayer.ObjectId == viewedPlayer.ObjectId);
                
                if (currentLocalPlayerOwnPet != null && IsStatsUIOwnedByEntity(entity, currentLocalPlayerOwnPet))
                {
                    if (isViewingLocalPlayerFight)
                    {
                        // Hide local player's own pet stats UI when viewing their own fight (handled by OwnPetView)
                        LogDebug($"[ENTITY_VISIBILITY] Local player's own pet stats UI {entity.EntityName.Value} (ID: {entity.ObjectId}) - hidden because viewing local player's fight (handled by OwnPetView)");
                        statsUIController.SetVisible(false);
                        return;
                    }
                    else
                    {
                        // When spectating other fights, the local player's pet might be the opponent pet, so don't auto-hide its stats UI
                        LogDebug($"[ENTITY_VISIBILITY] Local player's own pet stats UI {entity.EntityName.Value} (ID: {entity.ObjectId}) - not auto-hiding because viewing other player's fight");
                    }
                }
                
                List<NetworkEntity> viewedFightEntities = fightManager.GetViewedFightEntities();
                
                if (viewedFightEntities == null || viewedFightEntities.Count == 0)
                {
                    statsUIController.SetVisible(false);
                }
                else
                {
                    // For stats UI, we need to distinguish between:
                    // 1. Viewed player's own pet stats UI (should be hidden - OwnPetView handles it)
                    // 2. Opponent pet stats UI (should be shown)
                    NetworkEntity viewedPlayerOwnPet = GetViewedPlayerOwnPet();
                    
                    if (viewedPlayerOwnPet != null && IsStatsUIOwnedByEntity(entity, viewedPlayerOwnPet))
                    {
                        // This is the viewed player's own pet stats UI - hide it (OwnPetView handles it)
                        LogDebug($"[ENTITY_VISIBILITY] Stats UI {entity.EntityName.Value} (ID: {entity.ObjectId}) - hidden because it's the viewed player's own pet stats UI (handled by OwnPetView)");
                        statsUIController.SetVisible(false);
                    }
                    else
                    {
                        // Check if this stats UI belongs to an entity in the viewed fight
                        bool shouldBeVisible = IsStatsUIEntityInViewedFight(entity, viewedFightEntities);
                        statsUIController.SetVisible(shouldBeVisible);
                    }
                }
            }
        }
        else
        {
            // Handle other entities with NetworkEntityUI
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

                // Get the local player and their own pet
                NetworkEntity localPlayer = GetLocalPlayer();
                NetworkEntity localPlayerOwnPet = GetLocalPlayerOwnPet(localPlayer);
                
                List<NetworkEntity> viewedFightEntities = fightManager.GetViewedFightEntities();

                // Handle special cases for local player's own entities (handled by OwnPetView)
                // Only hide the local player's own pet if we're currently viewing the local player's fight
                NetworkEntity viewedPlayer = fightManager.ViewedCombatPlayer;
                bool isViewingLocalPlayerFight = (localPlayer != null && viewedPlayer != null && localPlayer.ObjectId == viewedPlayer.ObjectId);
                
                if (entity.EntityType == EntityType.Pet && localPlayerOwnPet != null && entity.ObjectId == localPlayerOwnPet.ObjectId)
                {
                    if (isViewingLocalPlayerFight)
                    {
                        // Hide local player's own pet when viewing their own fight (handled by OwnPetView)
                        LogDebug($"[ENTITY_VISIBILITY] Local player's own pet {entity.EntityName.Value} (ID: {entity.ObjectId}) - hidden because viewing local player's fight (handled by OwnPetView)");
                        entityUI.SetVisible(false);
                        return;
                    }
                    else
                    {
                        // When spectating other fights, the local player's pet might be the opponent pet, so don't auto-hide it
                        LogDebug($"[ENTITY_VISIBILITY] Local player's own pet {entity.EntityName.Value} (ID: {entity.ObjectId}) - not auto-hiding because viewing other player's fight");
                    }
                }
                
                if (entity.EntityType == EntityType.PetHand && localPlayerOwnPet != null && IsHandOwnedByEntity(entity, localPlayerOwnPet))
                {
                    if (isViewingLocalPlayerFight)
                    {
                        // Hide local player's own pet hand when viewing their own fight (handled by OwnPetView)
                        entityUI.SetVisible(false);
                        return;
                    }
                }
                
                if (viewedFightEntities == null || viewedFightEntities.Count == 0)
                {
                    entityUI.SetVisible(false);
                }
                else
                {
                    bool shouldBeVisible = false;
                    if (entity.EntityType == EntityType.Player)
                    {
                        shouldBeVisible = viewedFightEntities.Contains(entity);
                    }
                    else if (entity.EntityType == EntityType.Pet)
                    {
                        // For pets, we need to distinguish between:
                        // 1. Viewed player's own pet (should be hidden - OwnPetView handles it)
                        // 2. Opponent pet (should be shown)
                        NetworkEntity viewedPlayerOwnPet = GetViewedPlayerOwnPet();
                        
                        if (viewedPlayerOwnPet != null && entity.ObjectId == viewedPlayerOwnPet.ObjectId)
                        {
                            // This is the viewed player's own pet - hide it (OwnPetView handles it)
                            shouldBeVisible = false;
                            LogDebug($"[ENTITY_VISIBILITY] Pet {entity.EntityName.Value} (ID: {entity.ObjectId}) - hidden because it's the viewed player's own pet (handled by OwnPetView)");
                        }
                        else if (viewedFightEntities.Contains(entity))
                        {
                            // This is the opponent pet in the viewed fight - show it
                            shouldBeVisible = true;
                            LogDebug($"[ENTITY_VISIBILITY] Pet {entity.EntityName.Value} (ID: {entity.ObjectId}) - shown as opponent pet in viewed fight");
                        }
                        else
                        {
                            // This pet is not in the viewed fight at all - hide it
                            shouldBeVisible = false;
                            LogDebug($"[ENTITY_VISIBILITY] Pet {entity.EntityName.Value} (ID: {entity.ObjectId}) - hidden because not in viewed fight");
                        }
                    }
                    else if (entity.EntityType == EntityType.PlayerHand || entity.EntityType == EntityType.PetHand)
                    {
                        // For hand entities, we need to distinguish between:
                        // 1. Viewed player's own pet hand (should be hidden - OwnPetView handles it)
                        // 2. Opponent pet/player hand (should be shown if owner is in viewed fight)
                        NetworkEntity viewedPlayerOwnPet = GetViewedPlayerOwnPet();
                        
                        if (viewedPlayerOwnPet != null && IsHandOwnedByEntity(entity, viewedPlayerOwnPet))
                        {
                            // This is the viewed player's own pet hand - hide it (OwnPetView handles it)
                            shouldBeVisible = false;
                            LogDebug($"[ENTITY_VISIBILITY] Hand {entity.EntityName.Value} (ID: {entity.ObjectId}) - hidden because it's the viewed player's own pet hand (handled by OwnPetView)");
                        }
                        else
                        {
                            // Check if this hand belongs to an entity in the viewed fight
                            shouldBeVisible = IsHandEntityInViewedFight(entity, viewedFightEntities);
                        }
                    }
                    entityUI.SetVisible(shouldBeVisible);
                }
            }
        }
    }
    
    /// <summary>
    /// Determines if a hand entity should be visible based on whether its owner is in the viewed fight (List version)
    /// </summary>
    private bool IsHandEntityInViewedFight(NetworkEntity handEntity, List<NetworkEntity> viewedFightEntities)
    {
        if (handEntity == null || viewedFightEntities == null) return false;
        
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
                    bool isInViewedFight = viewedFightEntities.Contains(entity);
                    
                    // Don't apply special IsOwner filtering here anymore since we handle that at a higher level
                    return isInViewedFight;
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Determines if a stats UI entity should be visible based on whether its owner is in the viewed fight (List version)
    /// </summary>
    private bool IsStatsUIEntityInViewedFight(NetworkEntity statsUIEntity, List<NetworkEntity> viewedFightEntities)
    {
        if (statsUIEntity == null || viewedFightEntities == null) return false;
        
        // Find all entities to check for ownership relationships
        foreach (var entity in allEntities)
        {
            if (entity == null) continue;
            
            // Check if this entity has a relationship to the stats UI
            var relationshipManager = entity.GetComponent<RelationshipManager>();
            if (relationshipManager != null && relationshipManager.StatsUIEntity != null)
            {
                var entityStatsUI = relationshipManager.StatsUIEntity.GetComponent<NetworkEntity>();
                if (entityStatsUI != null && (uint)entityStatsUI.ObjectId == (uint)statsUIEntity.ObjectId)
                {
                    // This entity owns the stats UI, check if the entity is in the viewed fight
                    bool isInViewedFight = viewedFightEntities.Contains(entity);
                    
                    // Don't apply special IsOwner filtering here anymore since we handle that at a higher level
                    return isInViewedFight;
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
                // Handle stats UI entities with EntityStatsUIController
                if (entity.EntityType == EntityType.PlayerStatsUI || entity.EntityType == EntityType.PetStatsUI)
                {
                    var statsUIController = entity.GetComponent<EntityStatsUIController>();
                    if (statsUIController != null)
                    {
                        statsUIController.SetVisible(false);
                    }
                }
                else
                {
                    // Handle other entities with NetworkEntityUI
                    var entityUI = entity.GetComponent<NetworkEntityUI>();
                    if (entityUI != null)
                    {
                        entityUI.SetVisible(false);
                    }
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
        List<NetworkEntity> viewedFightEntities = fightManager.GetViewedFightEntities();
        
        if (viewedFightEntities == null || viewedFightEntities.Count == 0)
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
        
        // Card should be visible if its main owner is:
        // 1. The player in the viewed fight
        // 2. The opponent pet in the viewed fight
        bool shouldBeVisible = viewedFightEntities.Contains(cardMainOwner);
        
        LogDebug($"Combat card visibility check: Card {card.gameObject.name} main owner: {cardMainOwner.EntityName.Value} (ID: {cardMainOwnerObjectId}), Visible: {shouldBeVisible}");
        
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
        /* Debug.Log($"[EntityVisibilityManager] UpdateDraftPackVisibility called - Current game state: {currentGameState}"); */
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
        
        /* Debug.Log($"[EntityVisibilityManager] Local player found: {localPlayer.EntityName.Value} (ID: {localPlayer.ObjectId})"); */
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
        /* Debug.Log($"[EntityVisibilityManager] Found {allDraftPacks.Length} draft packs"); */
        LogDebug($"Found {allDraftPacks.Length} draft packs");
        
        foreach (DraftPack pack in allDraftPacks)
        {
            if (pack == null || pack.CardContainer == null) 
            {
                /* Debug.Log($"[EntityVisibilityManager] Skipping null pack or pack with null CardContainer"); */
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
            
            /* Debug.Log($"[EntityVisibilityManager] Pack {pack.name}: owned={isOwnedByLocalPlayer}, selectable={isSelectableByLocalPlayer}, shouldBeVisible={shouldBeVisible} (CurrentOwnerPlayerId: {pack.CurrentOwnerPlayerId.Value}, LocalPlayer ObjectId: {localPlayer.ObjectId})"); */
            LogDebug($"Pack {pack.name}: owned={isOwnedByLocalPlayer}, selectable={isSelectableByLocalPlayer}, shouldBeVisible={shouldBeVisible} (CurrentOwnerPlayerId: {pack.CurrentOwnerPlayerId.Value}, LocalPlayer ObjectId: {localPlayer.ObjectId})");
            
            // Update visibility for all cards in this pack
            UpdateDraftPackCardVisibility(pack, shouldBeVisible);
        }
        
        /* Debug.Log($"[EntityVisibilityManager] Updated draft pack visibility for {allDraftPacks.Length} packs"); */
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
    
    #region Visual Effects Visibility Management
    
    /// <summary>
    /// Centralized method to check if visual effects should be shown for given entities
    /// Used by both CardEffectResolver and AttackEffectManager
    /// </summary>
    public bool ShouldShowVisualEffectsForEntities(uint sourceEntityId, uint targetEntityId)
    {
        // During combat, use fight-based visibility
        if (currentGameState == GameState.Combat)
        {
            return ShouldShowVisualEffectsInCombat(sourceEntityId, targetEntityId);
        }
        
        // For other game states, allow effects to show
        return true;
    }
    
    /// <summary>
    /// Determines if visual effects should be shown during combat based on fight assignments
    /// </summary>
    private bool ShouldShowVisualEffectsInCombat(uint sourceEntityId, uint targetEntityId)
    {
        // Get the fight manager to check current fights
        TryFindFightManager();
        if (fightManager == null)
        {
            Debug.Log("EntityVisibilityManager: No FightManager found for visual effects check, allowing effects to show");
            return true;
        }
        
        // Get the currently viewed fight entities from FightManager
        var viewedEntities = fightManager.GetViewedFightEntities();
        if (viewedEntities == null || viewedEntities.Count == 0)
        {
            Debug.Log("EntityVisibilityManager: No viewed fight entities for visual effects check, allowing effects to show");
            return true;
        }
        
        // Check if both source and target entities are in the viewed fight
        bool sourceInViewedFight = viewedEntities.Any(e => e != null && (uint)e.ObjectId == sourceEntityId);
        bool targetInViewedFight = viewedEntities.Any(e => e != null && (uint)e.ObjectId == targetEntityId);
        bool shouldShow = sourceInViewedFight && targetInViewedFight;
        
        /* Debug.Log($"EntityVisibilityManager: Visual effects visibility check - Source entity (ID: {sourceEntityId}) in viewed fight: {sourceInViewedFight}, Target entity (ID: {targetEntityId}) in viewed fight: {targetInViewedFight}, Should show: {shouldShow}"); */
        
        return shouldShow;
    }
    
    /// <summary>
    /// Get the singleton instance of EntityVisibilityManager
    /// </summary>
    public static EntityVisibilityManager Instance { get; private set; }
    
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
    
    /// <summary>
    /// Debug method to manually update combat visibility from inspector
    /// </summary>
    [ContextMenu("Force Update Combat Visibility")]
    public void ForceUpdateCombatVisibility()
    {
        if (currentGameState == GameState.Combat)
        {
            UpdateVisibilityForCombat();
            Debug.Log("EntityVisibilityManager: Forced combat visibility update");
        }
        else
        {
            Debug.LogWarning($"EntityVisibilityManager: Not in combat state (current: {currentGameState}), cannot force combat visibility update");
        }
    }
    
    /// <summary>
    /// Debug method to log all registered entities and their stats UI relationships
    /// </summary>
    [ContextMenu("Debug Entity Stats UI Relationships")]
    public void DebugEntityStatsUIRelationships()
    {
        /* Debug.Log("=== ENTITY STATS UI RELATIONSHIPS DEBUG ==="); */
        
        // Find all stats UI entities
        var statsUIEntities = allEntities.Where(e => e != null && 
            (e.EntityType == EntityType.PlayerStatsUI || e.EntityType == EntityType.PetStatsUI)).ToList();
            
        /* Debug.Log($"Found {statsUIEntities.Count} stats UI entities:"); */
        
        foreach (var statsUI in statsUIEntities)
        {
            /* Debug.Log($"Stats UI: {statsUI.EntityName.Value} (ID: {statsUI.ObjectId}, Type: {statsUI.EntityType})"); */
            
            // Check if it has EntityStatsUIController
            var controller = statsUI.GetComponent<EntityStatsUIController>();
            if (controller != null)
            {
                Debug.Log($"  - Has EntityStatsUIController: YES (Linked to: {(controller.GetLinkedEntity()?.EntityName.Value ?? "null")})");
                Debug.Log($"  - Current visibility: {controller.IsVisible()}");
            }
            else
            {
                /* Debug.Log($"  - Has EntityStatsUIController: NO"); */
            }
            
            // Check if it has NetworkEntityUI
            var entityUI = statsUI.GetComponent<NetworkEntityUI>();
            /* Debug.Log($"  - Has NetworkEntityUI: {(entityUI != null ? "YES" : "NO")}"); */
            
            // Find which main entity owns this stats UI
            NetworkEntity ownerEntity = null;
            foreach (var entity in allEntities)
            {
                if (entity == null) continue;
                var relationshipManager = entity.GetComponent<RelationshipManager>();
                if (relationshipManager != null && relationshipManager.StatsUIEntity != null)
                {
                    var linkedStatsUI = relationshipManager.StatsUIEntity.GetComponent<NetworkEntity>();
                    if (linkedStatsUI != null && linkedStatsUI.ObjectId == statsUI.ObjectId)
                    {
                        ownerEntity = entity;
                        break;
                    }
                }
            }
            
            if (ownerEntity != null)
            {
                /* Debug.Log($"  - Owner: {ownerEntity.EntityName.Value} (ID: {ownerEntity.ObjectId}, Type: {ownerEntity.EntityType})"); */
            }
            else
            {
                /* Debug.Log($"  - Owner: NOT FOUND"); */
            }
        }
        
        /* Debug.Log("=== END STATS UI DEBUG ==="); */
    }
    
    /// <summary>
    /// Debug method to test stats UI visibility for currently viewed fight
    /// </summary>
    [ContextMenu("Test Stats UI Visibility for Viewed Fight")]
    public void TestStatsUIVisibilityForViewedFight()
    {
        if (currentGameState != GameState.Combat)
        {
            Debug.LogWarning("Not in combat state - cannot test stats UI visibility");
            return;
        }
        
        TryFindFightManager();
        if (fightManager == null)
        {
            Debug.LogError("FightManager not found");
            return;
        }
        
        var viewedFightEntities = fightManager.GetViewedFightEntities();
        if (viewedFightEntities == null || viewedFightEntities.Count == 0)
        {
            Debug.LogWarning("No viewed fight entities found");
            return;
        }
        
        /* Debug.Log($"=== TESTING STATS UI VISIBILITY ==="); */
        /* Debug.Log($"Viewed fight entities: {string.Join(", ", viewedFightEntities.Select(e => $"{e.EntityName.Value} (ID: {e.ObjectId})"))}"); */
        
        // Find all stats UI entities and test their visibility
        var statsUIEntities = allEntities.Where(e => e != null && 
            (e.EntityType == EntityType.PlayerStatsUI || e.EntityType == EntityType.PetStatsUI)).ToList();
            
        foreach (var statsUI in statsUIEntities)
        {
            bool shouldBeVisible = IsStatsUIEntityInViewedFight(statsUI, viewedFightEntities);
            var controller = statsUI.GetComponent<EntityStatsUIController>();
            bool actuallyVisible = controller != null ? controller.IsVisible() : false;
            
            /* Debug.Log($"Stats UI: {statsUI.EntityName.Value} - Should be visible: {shouldBeVisible}, Actually visible: {actuallyVisible}"); */
        }
        
        /* Debug.Log("=== END VISIBILITY TEST ==="); */
    }
    

    
    #endregion
} 