using UnityEngine;
using FishNet.Connection;
using System.Collections.Generic;

/// <summary>
/// Manages the visibility of NetworkPlayer and NetworkPet entities based on game state.
/// Attach to: The same GameObject as GamePhaseManager to centralize visibility control.
/// </summary>
public class EntityVisibilityManager : MonoBehaviour
{
    [Header("Debug Options")]
    [SerializeField] private bool debugLogEnabled = false;
    
    // Cache all players and pets for easier management
    private List<NetworkPlayer> allPlayers = new List<NetworkPlayer>();
    private List<NetworkPet> allPets = new List<NetworkPet>();
    
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
        
        // Initially set all pets and players to be hidden in lobby
        if (currentGameState == GameState.Lobby)
        {
            UpdateVisibilityForLobby();
        }
    }
    
    #region Registration Methods
    
    /// <summary>
    /// Register a NetworkPlayer to be managed by this component
    /// </summary>
    public void RegisterPlayer(NetworkPlayer player)
    {
        if (player == null) return;
        
        if (!allPlayers.Contains(player))
        {
            allPlayers.Add(player);
            LogDebug($"Registered player {player.PlayerName.Value} (ID: {player.ObjectId})");
            
            // Apply current visibility state
            UpdateEntityVisibility(player);
        }
    }
    
    /// <summary>
    /// Register a NetworkPet to be managed by this component
    /// </summary>
    public void RegisterPet(NetworkPet pet)
    {
        if (pet == null) return;
        
        if (!allPets.Contains(pet))
        {
            allPets.Add(pet);
            LogDebug($"Registered pet {pet.PetName.Value} (ID: {pet.ObjectId})");
            
            // Apply current visibility state
            UpdateEntityVisibility(pet);
        }
    }
    
    /// <summary>
    /// Unregister a NetworkPlayer when it's no longer needed
    /// </summary>
    public void UnregisterPlayer(NetworkPlayer player)
    {
        if (player == null || !allPlayers.Contains(player)) return;
        
        allPlayers.Remove(player);
        LogDebug($"Unregistered player (ID: {player.ObjectId})");
    }
    
    /// <summary>
    /// Unregister a NetworkPet when it's no longer needed
    /// </summary>
    public void UnregisterPet(NetworkPet pet)
    {
        if (pet == null || !allPets.Contains(pet)) return;
        
        allPets.Remove(pet);
        LogDebug($"Unregistered pet (ID: {pet.ObjectId})");
    }
    
    #endregion
    
    #region Game State Methods
    
    /// <summary>
    /// Set the game state to Lobby and update visibility accordingly
    /// </summary>
    public void SetLobbyState()
    {
        currentGameState = GameState.Lobby;
        LogDebug("Game state changed to Lobby");
        UpdateVisibilityForLobby();
    }
    
    /// <summary>
    /// Set the game state to Combat and update visibility accordingly
    /// </summary>
    public void SetCombatState()
    {
        currentGameState = GameState.Combat;
        LogDebug("Game state changed to Combat");
        UpdateVisibilityForCombat();
    }
    
    #endregion
    
    #region Visibility Update Methods
    
    /// <summary>
    /// Hide all entities in Lobby state
    /// </summary>
    private void UpdateVisibilityForLobby()
    {
        SetAllPlayersVisibility(entitiesVisibleInLobby);
        SetAllPetsVisibility(entitiesVisibleInLobby);
        
        LogDebug($"Updated visibility for Lobby - All entities {(entitiesVisibleInLobby ? "visible" : "hidden")}");
    }
    
    private void SetAllPlayersVisibility(bool isVisible)
    {
        foreach (var player in allPlayers)
        {
            if (player == null) continue;
            
            var playerUI = player.GetComponent<NetworkPlayerUI>();
            if (playerUI != null)
            {
                playerUI.SetVisible(isVisible);
            }
        }
    }
    
    private void SetAllPetsVisibility(bool isVisible)
    {
        foreach (var pet in allPets)
        {
            if (pet == null) continue;
            
            var petUI = pet.GetComponent<NetworkPetUI>();
            if (petUI != null)
            {
                petUI.SetVisible(isVisible);
            }
        }
    }
    
    /// <summary>
    /// In Combat, show only the entities involved in the current client's fight
    /// </summary>
    private void UpdateVisibilityForCombat()
    {
        TryFindFightManager();
        if (fightManager == null)
        {
            LogDebug("FightManager instance not found! Cannot update visibility for combat.");
            return;
        }
        
        // Get the local client's connection - use the local player's connection
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
        
        UpdatePlayersVisibilityForCombat(playerInFightId);
        UpdatePetsVisibilityForCombat(petInFightId);
    }
    
    private void UpdatePlayersVisibilityForCombat(uint visiblePlayerId)
    {
        foreach (var player in allPlayers)
        {
            if (player == null) continue;
            
            bool shouldBeVisible = (uint)player.ObjectId == visiblePlayerId;
            var playerUI = player.GetComponent<NetworkPlayerUI>();
            if (playerUI != null)
            {
                playerUI.SetVisible(shouldBeVisible);
                LogDebug($"Player {player.PlayerName.Value} (ID: {player.ObjectId}): {(shouldBeVisible ? "Visible" : "Hidden")}");
            }
        }
    }
    
    private void UpdatePetsVisibilityForCombat(uint visiblePetId)
    {
        foreach (var pet in allPets)
        {
            if (pet == null) continue;
            
            bool shouldBeVisible = (uint)pet.ObjectId == visiblePetId;
            var petUI = pet.GetComponent<NetworkPetUI>();
            if (petUI != null)
            {
                petUI.SetVisible(shouldBeVisible);
                LogDebug($"Pet {pet.PetName.Value} (ID: {pet.ObjectId}): {(shouldBeVisible ? "Visible" : "Hidden")}");
            }
        }
    }
    
    /// <summary>
    /// Update visibility for a specific player based on current game state
    /// </summary>
    private void UpdateEntityVisibility(NetworkPlayer player)
    {
        if (player == null) return;
        
        var playerUI = player.GetComponent<NetworkPlayerUI>();
        if (playerUI == null) return;
        
        if (currentGameState == GameState.Lobby)
        {
            playerUI.SetVisible(entitiesVisibleInLobby);
        }
        else if (currentGameState == GameState.Combat)
        {
            // We need to determine if this player is involved in the local client's fight
            NetworkConnection localConnection = GetLocalPlayerConnection();
            if (localConnection == null) return;
            
            TryFindFightManager();
            if (fightManager == null) return;
            
            var fightAssignment = fightManager.GetFightForConnection(localConnection);
            
            if (fightAssignment.HasValue)
            {
                bool shouldBeVisible = (uint)player.ObjectId == fightAssignment.Value.PlayerObjectId;
                playerUI.SetVisible(shouldBeVisible);
            }
            else
            {
                playerUI.SetVisible(false);
            }
        }
    }
    
    /// <summary>
    /// Update visibility for a specific pet based on current game state
    /// </summary>
    private void UpdateEntityVisibility(NetworkPet pet)
    {
        if (pet == null) return;
        
        var petUI = pet.GetComponent<NetworkPetUI>();
        if (petUI == null) return;
        
        if (currentGameState == GameState.Lobby)
        {
            petUI.SetVisible(entitiesVisibleInLobby);
        }
        else if (currentGameState == GameState.Combat)
        {
            // We need to determine if this pet is involved in the local client's fight
            NetworkConnection localConnection = GetLocalPlayerConnection();
            if (localConnection == null) return;
            
            TryFindFightManager();
            if (fightManager == null) return;
            
            var fightAssignment = fightManager.GetFightForConnection(localConnection);
            
            if (fightAssignment.HasValue)
            {
                bool shouldBeVisible = (uint)pet.ObjectId == fightAssignment.Value.PetObjectId;
                petUI.SetVisible(shouldBeVisible);
            }
            else
            {
                petUI.SetVisible(false);
            }
        }
    }
    
    /// <summary>
    /// Update visibility for all registered entities
    /// </summary>
    public void UpdateAllEntitiesVisibility()
    {
        foreach (var player in allPlayers)
        {
            if (player != null)
            {
                UpdateEntityVisibility(player);
            }
        }
        
        foreach (var pet in allPets)
        {
            if (pet != null)
            {
                UpdateEntityVisibility(pet);
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
        // This implementation depends on your specific NetworkPlayer setup
        
        // Use FishNet's ClientManager to get the local connection directly
        if (FishNet.InstanceFinder.ClientManager != null && FishNet.InstanceFinder.ClientManager.Connection != null)
        {
            return FishNet.InstanceFinder.ClientManager.Connection;
        }
        
        // Alternative approach: try to find a player owned by the local client
        foreach (var player in allPlayers)
        {
            if (player != null && player.IsOwner)
            {
                return player.Owner;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Logs a debug message if debug logging is enabled
    /// </summary>
    private void LogDebug(string message)
    {
        if (debugLogEnabled)
        {
            Debug.Log($"[EntityVisibilityManager] {message}");
        }
    }
    
    #endregion
} 