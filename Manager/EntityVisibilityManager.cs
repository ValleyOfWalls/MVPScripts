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