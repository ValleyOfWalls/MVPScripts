using UnityEngine;
using FishNet.Connection;
using System.Collections.Generic;

/// <summary>
/// Manages the visibility of NetworkEntity objects based on game state.
/// Attach to: The same GameObject as GamePhaseManager to centralize visibility control.
/// </summary>
public class EntityVisibilityManager : MonoBehaviour
{
    [Header("Debug Options")]
    [SerializeField] private bool debugLogEnabled = false;
    
    // Cache all entities for easier management
    private List<NetworkEntity> allEntities = new List<NetworkEntity>();
    
    // Tracking current game state
    public enum GameState
    {
        Start,
        Lobby,
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
        if (currentGameState == newState) return;
        
        currentGameState = newState;
        LogDebug($"Game state changed to: {newState}");
        
        switch (newState)
        {
            case GameState.Lobby:
                UpdateVisibilityForLobby();
                break;
            case GameState.Draft:
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
    }
    
    private void UpdateVisibilityForCombat()
    {
        TryFindFightManager();
        if (fightManager == null)
        {
            LogDebug("FightManager instance not found! Cannot update visibility for combat.");
            return;
        }
        
        // Get the local client's connection
        NetworkConnection localConnection = GetLocalPlayerConnection();
        if (localConnection == null)
        {
            LogDebug("No local player connection found. Cannot determine combat visibility.");
            return;
        }
        
        // Get the fight assignment for the local client
        var fightAssignment = fightManager.GetFightForConnection(localConnection);
        
        // IDs of entities in the fight
        uint playerInFightId = 0;
        uint petInFightId = 0;
        
        if (fightAssignment.HasValue)
        {
            playerInFightId = fightAssignment.Value.PlayerObjectId;
            petInFightId = fightAssignment.Value.PetObjectId;
            LogDebug($"Combat participants - Player ID: {playerInFightId}, Pet ID: {petInFightId}");
        }
        else
        {
            LogDebug("No fight assignment found for this client");
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
            // We need to determine if this entity is involved in the local client's fight
            NetworkConnection localConnection = GetLocalPlayerConnection();
            if (localConnection == null) return;
            
            TryFindFightManager();
            if (fightManager == null) return;
            
            var fightAssignment = fightManager.GetFightForConnection(localConnection);
            
            if (fightAssignment.HasValue)
            {
                bool shouldBeVisible = false;
                if (entity.EntityType == EntityType.Player)
                {
                    shouldBeVisible = (uint)entity.ObjectId == fightAssignment.Value.PlayerObjectId;
                }
                else if (entity.EntityType == EntityType.Pet)
                {
                    shouldBeVisible = (uint)entity.ObjectId == fightAssignment.Value.PetObjectId;
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
        
        // Get the local client's connection
        var networkManager = FishNet.InstanceFinder.NetworkManager;
        if (networkManager == null || !networkManager.IsClientStarted)
        {
            return false;
        }
        
        NetworkConnection localConnection = networkManager.ClientManager.Connection;
        if (localConnection == null)
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
        
        // Get the fight assignment for this client
        var fightAssignment = fightManager.GetFightForConnection(localConnection);
        if (!fightAssignment.HasValue)
        {
            // If no fight assignment, fall back to simple ownership check
            return card.OwnerEntity.IsOwner;
        }
        
        // Check if the card belongs to entities involved in this client's fight
        uint cardOwnerObjectId = (uint)card.OwnerEntity.ObjectId;
        uint playerInFightId = fightAssignment.Value.PlayerObjectId;
        uint opponentPetInFightId = fightAssignment.Value.PetObjectId;
        
        // Card should be visible if it belongs to:
        // 1. The player in the fight (the client's own player)
        // 2. The opponent pet in the fight (the pet the client is fighting)
        bool shouldBeVisible = (cardOwnerObjectId == playerInFightId) || (cardOwnerObjectId == opponentPetInFightId);
        
        LogDebug($"Combat card visibility check: Card {card.gameObject.name} owner ID: {cardOwnerObjectId}, Player in fight: {playerInFightId}, Opponent pet: {opponentPetInFightId}, Visible: {shouldBeVisible}");
        
        return shouldBeVisible;
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
        
        // Get all card transforms for this entity
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
    
    #region Helper Methods
    
    /// <summary>
    /// Get the local player's connection
    /// </summary>
    private NetworkConnection GetLocalPlayerConnection()
    {
        var localPlayer = allEntities.Find(e => e.EntityType == EntityType.Player && e.IsOwner);
        return localPlayer?.Owner;
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