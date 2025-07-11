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
    
    // Singleton instance
    public static EntityVisibilityManager Instance { get; private set; }
    
    private void Awake()
    {
        InitializeSingleton();
        TryFindFightManager();
    }
    
    private void InitializeSingleton()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Multiple EntityVisibilityManager instances found! Destroying duplicate.");
            Destroy(gameObject);
        }
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
            UpdateVisibilityForGameState();
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
        
        UpdateVisibilityForGameState();
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
            UpdateVisibilityForGameState();
        }
    }
    
    /// <summary>
    /// Unified method to update visibility based on current game state
    /// </summary>
    private void UpdateVisibilityForGameState()
    {
        switch (currentGameState)
        {
            case GameState.Lobby:
                SetAllEntitiesVisibility(entitiesVisibleInLobby);
                break;
            case GameState.CharacterSelection:
            case GameState.Draft:
                SetAllEntitiesVisibility(false);
                if (currentGameState == GameState.Draft)
                {
                    UpdateDraftPackVisibility();
                }
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
    /// Set visibility for all entities to a specific state
    /// </summary>
    private void SetAllEntitiesVisibility(bool visible)
    {
        foreach (var entity in allEntities)
        {
            if (entity != null)
            {
                SetEntityVisibility(entity, visible);
            }
        }
        LogDebug($"All entities set to {(visible ? "visible" : "hidden")}");
    }
    
    /// <summary>
    /// Set visibility for a specific entity
    /// </summary>
    private void SetEntityVisibility(NetworkEntity entity, bool visible)
    {
        if (entity == null) return;
        
        // Handle stats UI entities with EntityStatsUIController
        if (entity.EntityType == EntityType.PlayerStatsUI || entity.EntityType == EntityType.PetStatsUI)
        {
            var statsUIController = entity.GetComponent<EntityStatsUIController>();
            if (statsUIController != null)
            {
                statsUIController.SetVisible(visible);
            }
        }
        else
        {
            // Handle other entities with NetworkEntityUI
            var entityUI = entity.GetComponent<NetworkEntityUI>();
            if (entityUI != null)
            {
                entityUI.SetVisible(visible);
            }
        }
    }
    
    private void UpdateVisibilityForCombat()
    {
        TryFindFightManager();
        if (fightManager == null)
        {
            LogDebug("FightManager instance not found! Cannot update visibility for combat.");
            return;
        }
        
        // Don't update visibility if there are no fight assignments yet
        var allFights = fightManager.GetAllFightAssignments();
        if (allFights.Count == 0)
        {
            LogDebug("No fight assignments yet - waiting for FightManager to be ready");
            return;
        }
        
        // Get all entities involved in the currently viewed fight
        List<NetworkEntity> viewedFightEntities = fightManager.GetViewedFightEntities();
        
        if (viewedFightEntities == null || viewedFightEntities.Count == 0)
        {
            LogDebug("No viewed fight entities found in FightManager despite having fight assignments");
            SetAllEntitiesVisibility(false);
            return;
        }
        
        LogDebug($"Combat participants (viewed fight) - Total entities: {viewedFightEntities.Count}");
        
        // Update visibility for all entities
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
        LogDebug("FightManager is ready - updating combat visibility");
        
        if (currentGameState == GameState.Combat)
        {
            UpdateVisibilityForCombat();
        }
    }
    
    /// <summary>
    /// Update visibility for a specific entity based on current game state
    /// </summary>
    private void UpdateEntityVisibility(NetworkEntity entity)
    {
        if (entity == null) return;
        
        // Simple visibility for non-combat states
        if (currentGameState != GameState.Combat)
        {
            bool isVisible = (currentGameState == GameState.Lobby) ? entitiesVisibleInLobby : false;
            SetEntityVisibility(entity, isVisible);
            return;
        }
        
        // Combat-specific visibility logic
        TryFindFightManager();
        if (fightManager == null) return;
        
        List<NetworkEntity> viewedFightEntities = fightManager.GetViewedFightEntities();
        if (viewedFightEntities == null || viewedFightEntities.Count == 0)
        {
            SetEntityVisibility(entity, false);
            return;
        }
        
        bool shouldBeVisible = DetermineEntityVisibilityInCombat(entity, viewedFightEntities);
        SetEntityVisibility(entity, shouldBeVisible);
    }
    
    /// <summary>
    /// Determines if an entity should be visible during combat
    /// </summary>
    private bool DetermineEntityVisibilityInCombat(NetworkEntity entity, List<NetworkEntity> viewedFightEntities)
    {
        // Arena system: All player and pet models are always visible
        if (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet)
        {
            return true;
        }
        
        // UI elements: Only show for viewed player and opponent (not allies)
        if (entity.EntityType == EntityType.PlayerHand || entity.EntityType == EntityType.PetHand)
        {
            return IsUIElementForPlayerOrOpponent(entity, viewedFightEntities, IsHandOwnedByEntity);
        }
        
        if (entity.EntityType == EntityType.PlayerStatsUI || entity.EntityType == EntityType.PetStatsUI)
        {
            return IsUIElementForPlayerOrOpponent(entity, viewedFightEntities, IsStatsUIOwnedByEntity);
        }
        
        // Default: hide other entity types
        return false;
    }
    
    /// <summary>
    /// Generic method to check if a UI element belongs to the viewed player or opponent
    /// </summary>
    private bool IsUIElementForPlayerOrOpponent(NetworkEntity uiEntity, List<NetworkEntity> viewedFightEntities, System.Func<NetworkEntity, NetworkEntity, bool> ownershipCheck)
    {
        NetworkEntity localPlayer = GetLocalPlayer();
        if (localPlayer == null) return false;
        
        // Determine viewed player and opponent
        NetworkEntity viewedPlayer = null;
        NetworkEntity viewedOpponent = null;
        
        if (fightManager.ViewedLeftFighter != null && localPlayer.ObjectId == fightManager.ViewedLeftFighter.ObjectId)
        {
            viewedPlayer = fightManager.ViewedLeftFighter;
            viewedOpponent = fightManager.ViewedRightFighter;
        }
        else if (fightManager.ViewedRightFighter != null && localPlayer.ObjectId == fightManager.ViewedRightFighter.ObjectId)
        {
            viewedPlayer = fightManager.ViewedRightFighter;
            viewedOpponent = fightManager.ViewedLeftFighter;
        }
        else
        {
            // Spectating: show UI for both main fighters
            viewedPlayer = fightManager.ViewedLeftFighter;
            viewedOpponent = fightManager.ViewedRightFighter;
        }
        
        // Check ownership and determine visibility
        foreach (var entity in allEntities)
        {
            if (entity != null && ownershipCheck(uiEntity, entity))
            {
                bool isViewedPlayer = viewedPlayer != null && entity.ObjectId == viewedPlayer.ObjectId;
                bool isViewedOpponent = viewedOpponent != null && entity.ObjectId == viewedOpponent.ObjectId;
                return isViewedPlayer || isViewedOpponent;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Checks if a hand entity is owned by a specific entity
    /// </summary>
    private bool IsHandOwnedByEntity(NetworkEntity handEntity, NetworkEntity ownerEntity)
    {
        if (handEntity == null || ownerEntity == null) return false;
        
        var relationshipManager = ownerEntity.GetComponent<RelationshipManager>();
        if (relationshipManager == null || relationshipManager.HandEntity == null) return false;
        
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
        if (relationshipManager == null || relationshipManager.StatsUIEntity == null) return false;
        
        var entityStatsUI = relationshipManager.StatsUIEntity.GetComponent<NetworkEntity>();
        return entityStatsUI != null && entityStatsUI.ObjectId == statsUIEntity.ObjectId;
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
        
        UpdateAllCardVisibility();
    }
    
    /// <summary>
    /// Hides all registered entities regardless of current game state
    /// </summary>
    public void HideAllEntities()
    {
        SetAllEntitiesVisibility(false);
    }
    
    #endregion
    
    #region Card Visibility Management
    
    /// <summary>
    /// Applies combat-aware visibility filtering to a card
    /// </summary>
    public void ApplyCardVisibilityFilter(GameObject cardObject, bool serverRequestedState)
    {
        if (cardObject == null) return;
        
        Card card = cardObject.GetComponent<Card>();
        if (card == null) return;
        
        bool shouldBeVisible = ShouldCardBeVisibleToLocalClient(card);
        bool finalVisibility = serverRequestedState && shouldBeVisible;
        
        cardObject.SetActive(finalVisibility);
        
        LogDebug($"Card visibility filter applied: {cardObject.name} -> SetActive({finalVisibility})");
    }
    
    /// <summary>
    /// Determines if a card should be visible to the local client
    /// </summary>
    private bool ShouldCardBeVisibleToLocalClient(Card card)
    {
        if (card == null || card.OwnerEntity == null) return false;
        
        // During combat, use fight-based visibility
        if (currentGameState == GameState.Combat)
        {
            return ShouldCardBeVisibleInCombat(card);
        }
        
        // For other game states, use simple ownership
        return card.OwnerEntity.IsOwner;
    }
    
    /// <summary>
    /// Determines if a card should be visible during combat
    /// </summary>
    private bool ShouldCardBeVisibleInCombat(Card card)
    {
        if (card == null || card.OwnerEntity == null) return false;
        
        TryFindFightManager();
        if (fightManager == null) return card.OwnerEntity.IsOwner;
        
        List<NetworkEntity> viewedFightEntities = fightManager.GetViewedFightEntities();
        if (viewedFightEntities == null || viewedFightEntities.Count == 0)
        {
            return card.OwnerEntity.IsOwner;
        }
        
        // Get the main entity that owns this card
        NetworkEntity cardMainOwner = GetMainEntityForCard(card);
        if (cardMainOwner == null) return false;
        
        // Card should be visible if its main owner is involved in the viewed fight
        return viewedFightEntities.Contains(cardMainOwner);
    }
    
    /// <summary>
    /// Gets the main entity (Player/Pet) that owns a card
    /// </summary>
    private NetworkEntity GetMainEntityForCard(Card card)
    {
        if (card == null || card.OwnerEntity == null) return null;
        
        NetworkEntity cardOwner = card.OwnerEntity;
        
        // If card is owned by a Hand entity, find the main entity that owns the hand
        if (cardOwner.EntityType == EntityType.PlayerHand || cardOwner.EntityType == EntityType.PetHand)
        {
            foreach (var entity in allEntities)
            {
                if (entity != null && (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet))
                {
                    if (IsHandOwnedByEntity(cardOwner, entity))
                    {
                        return entity;
                    }
                }
            }
            return null;
        }
        
        // If card is owned by a main entity, return it directly
        if (cardOwner.EntityType == EntityType.Player || cardOwner.EntityType == EntityType.Pet)
        {
            return cardOwner;
        }
        
        return null;
    }
    
    /// <summary>
    /// Updates visibility for all cards belonging to registered entities
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
        
        // For main entities, find their hand entity
        if (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet)
        {
            var relationshipManager = entity.GetComponent<RelationshipManager>();
            if (relationshipManager != null && relationshipManager.HandEntity != null)
            {
                var handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
                if (handEntity != null)
                {
                    UpdateCardVisibilityForEntity(handEntity);
                }
            }
            return;
        }
        
        // For Hand entities, update cards in transforms
        var entityUI = entity.GetComponent<NetworkEntityUI>();
        if (entityUI == null) return;
        
        UpdateCardVisibilityInTransform(entityUI.GetHandTransform(), true);
        UpdateCardVisibilityInTransform(entityUI.GetDeckTransform(), false);
        UpdateCardVisibilityInTransform(entityUI.GetDiscardTransform(), false);
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
    /// </summary>
    public void UpdateDraftPackVisibility()
    {
        if (currentGameState != GameState.Draft) return;
        
        NetworkEntity localPlayer = GetLocalPlayer();
        if (localPlayer == null) return;
        
        DraftManager draftManager = FindFirstObjectByType<DraftManager>();
        if (draftManager == null) return;
        
        DraftPack[] allDraftPacks = FindObjectsByType<DraftPack>(FindObjectsSortMode.None);
        
        foreach (DraftPack pack in allDraftPacks)
        {
            if (pack == null || pack.CardContainer == null) continue;
            
            bool isOwnedByLocalPlayer = pack.IsOwnedBy(localPlayer);
            bool isSelectableByLocalPlayer = pack.GetCards().Any(card => 
                draftManager.IsCardSelectableByPlayer(card, localPlayer));
            
            bool shouldBeVisible = isOwnedByLocalPlayer && isSelectableByLocalPlayer;
            UpdateDraftPackCardVisibility(pack, shouldBeVisible);
        }
    }
    
    /// <summary>
    /// Updates visibility for a specific draft pack when ownership changes
    /// </summary>
    public void UpdateDraftPackVisibilityForPack(DraftPack pack)
    {
        if (currentGameState != GameState.Draft || pack == null) return;
        
        NetworkEntity localPlayer = GetLocalPlayer();
        if (localPlayer == null) return;
        
        DraftManager draftManager = FindFirstObjectByType<DraftManager>();
        if (draftManager == null) return;
        
        bool isOwnedByLocalPlayer = pack.IsOwnedBy(localPlayer);
        bool isSelectableByLocalPlayer = pack.GetCards().Any(card => 
            draftManager.IsCardSelectableByPlayer(card, localPlayer));
        
        bool shouldBeVisible = isOwnedByLocalPlayer && isSelectableByLocalPlayer;
        UpdateDraftPackCardVisibility(pack, shouldBeVisible);
    }
    
    /// <summary>
    /// Updates visibility for cards in a specific draft pack
    /// </summary>
    private void UpdateDraftPackCardVisibility(DraftPack pack, bool shouldBeVisible)
    {
        if (pack == null || pack.CardContainer == null) return;
        
        for (int i = 0; i < pack.CardContainer.childCount; i++)
        {
            Transform cardTransform = pack.CardContainer.GetChild(i);
            if (cardTransform != null && cardTransform.gameObject != null)
            {
                cardTransform.gameObject.SetActive(shouldBeVisible);
            }
        }
    }
    
    #endregion
    
    #region Visual Effects Management
    
    /// <summary>
    /// Determines if visual effects should be shown for given entities
    /// </summary>
    public bool ShouldShowVisualEffectsForEntities(uint sourceEntityId, uint targetEntityId)
    {
        if (currentGameState != GameState.Combat) return true;
        
        TryFindFightManager();
        if (fightManager == null) return true;
        
        var viewedEntities = fightManager.GetViewedFightEntities();
        if (viewedEntities == null || viewedEntities.Count == 0) return true;
        
        // Check if both entities are in the viewed fight
        bool sourceInViewedFight = viewedEntities.Any(e => e != null && (uint)e.ObjectId == sourceEntityId);
        bool targetInViewedFight = viewedEntities.Any(e => e != null && (uint)e.ObjectId == targetEntityId);
        
        return sourceInViewedFight && targetInViewedFight;
    }
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Get the local player entity
    /// </summary>
    private NetworkEntity GetLocalPlayer()
    {
        foreach (var entity in allEntities)
        {
            if (entity != null && entity.EntityType == EntityType.Player && entity.IsOwner)
            {
                return entity;
            }
        }
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