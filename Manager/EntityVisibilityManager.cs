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
    [SerializeField] private bool debugLogEnabled = true;
    
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
        fightManager = FindFirstObjectByType<FightManager>();
        if (fightManager == null)
        {
            Debug.LogWarning("EntityVisibilityManager: FightManager not found at startup. Will try again when needed.");
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
        if (player == null) return;
        
        if (allPlayers.Contains(player))
        {
            allPlayers.Remove(player);
            LogDebug($"Unregistered player (ID: {player.ObjectId})");
        }
    }
    
    /// <summary>
    /// Unregister a NetworkPet when it's no longer needed
    /// </summary>
    public void UnregisterPet(NetworkPet pet)
    {
        if (pet == null) return;
        
        if (allPets.Contains(pet))
        {
            allPets.Remove(pet);
            LogDebug($"Unregistered pet (ID: {pet.ObjectId})");
        }
    }
    
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
    
    /// <summary>
    /// Hide all entities in Lobby state
    /// </summary>
    private void UpdateVisibilityForLobby()
    {
        foreach (var player in allPlayers)
        {
            if (player == null) continue;
            
            var playerUI = player.GetComponent<NetworkPlayerUI>();
            if (playerUI != null)
            {
                playerUI.SetVisible(entitiesVisibleInLobby);
            }
        }
        
        foreach (var pet in allPets)
        {
            if (pet == null) continue;
            
            var petUI = pet.GetComponent<NetworkPetUI>();
            if (petUI != null)
            {
                petUI.SetVisible(entitiesVisibleInLobby);
            }
        }
        
        LogDebug($"Updated visibility for Lobby - All entities {(entitiesVisibleInLobby ? "visible" : "hidden")}");
    }
    
    /// <summary>
    /// In Combat, show only the entities involved in the current client's fight
    /// </summary>
    private void UpdateVisibilityForCombat()
    {
        // Ensure we have a reference to the FightManager
        if (fightManager == null)
        {
            fightManager = FindFirstObjectByType<FightManager>();
            if (fightManager == null)
            {
                LogDebug("FightManager instance not found! Cannot update visibility for combat.");
                return;
            }
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
        
        // Update visibility for all players
        foreach (var player in allPlayers)
        {
            if (player == null) continue;
            
            bool shouldBeVisible = (uint)player.ObjectId == playerInFightId;
            var playerUI = player.GetComponent<NetworkPlayerUI>();
            if (playerUI != null)
            {
                playerUI.SetVisible(shouldBeVisible);
                LogDebug($"Player {player.PlayerName.Value} (ID: {player.ObjectId}): {(shouldBeVisible ? "Visible" : "Hidden")}");
            }
        }
        
        // Update visibility for all pets
        foreach (var pet in allPets)
        {
            if (pet == null) continue;
            
            bool shouldBeVisible = (uint)pet.ObjectId == petInFightId;
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
            // Ensure we have a reference to the FightManager
            if (fightManager == null)
            {
                fightManager = FindFirstObjectByType<FightManager>();
                if (fightManager == null)
                {
                    LogDebug("FightManager instance not found! Cannot update player visibility for combat.");
                    return;
                }
            }
            
            // Get the local client's connection
            NetworkConnection localConnection = GetLocalPlayerConnection();
            if (localConnection == null)
            {
                LogDebug("No local player connection found. Cannot determine player visibility.");
                return;
            }
            
            // Get the fight assignment for the local client
            var fightAssignment = fightManager.GetFightForConnection(localConnection);
            
            // Check if this player is in the local client's fight
            bool shouldBeVisible = fightAssignment.HasValue && fightAssignment.Value.PlayerObjectId == (uint)player.ObjectId;
            playerUI.SetVisible(shouldBeVisible);
            
            LogDebug($"Combat visibility - Player {player.PlayerName.Value}: {(shouldBeVisible ? "Visible" : "Hidden")}");
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
            // Ensure we have a reference to the FightManager
            if (fightManager == null)
            {
                fightManager = FindFirstObjectByType<FightManager>();
                if (fightManager == null)
                {
                    LogDebug("FightManager instance not found! Cannot update pet visibility for combat.");
                    return;
                }
            }
            
            // Get the local client's connection
            NetworkConnection localConnection = GetLocalPlayerConnection();
            if (localConnection == null)
            {
                LogDebug("No local player connection found. Cannot determine pet visibility.");
                return;
            }
            
            // Get the fight assignment for the local client
            var fightAssignment = fightManager.GetFightForConnection(localConnection);
            
            // Check if this pet is in the local client's fight
            bool shouldBeVisible = fightAssignment.HasValue && fightAssignment.Value.PetObjectId == (uint)pet.ObjectId;
            petUI.SetVisible(shouldBeVisible);
            
            LogDebug($"Combat visibility - Pet {pet.PetName.Value}: {(shouldBeVisible ? "Visible" : "Hidden")}");
        }
    }
    
    /// <summary>
    /// Update visibility for all entities
    /// </summary>
    public void UpdateAllEntitiesVisibility()
    {
        if (currentGameState == GameState.Lobby)
        {
            UpdateVisibilityForLobby();
        }
        else if (currentGameState == GameState.Combat)
        {
            UpdateVisibilityForCombat();
        }
    }
    
    /// <summary>
    /// Helper method to get the local player's connection
    /// </summary>
    private NetworkConnection GetLocalPlayerConnection()
    {
        // Find the local player (the one owned by this client)
        NetworkPlayer localPlayer = null;
        
        // Try different approaches to find the local player
        // 1. First check for IsOwner flag
        foreach (var player in allPlayers)
        {
            if (player != null && player.IsOwner)
            {
                localPlayer = player;
                LogDebug($"Found local player by IsOwner: {player.PlayerName.Value}");
                break;
            }
        }
        
        // 2. If that fails, try FishNet connection matching
        if (localPlayer == null && FishNet.InstanceFinder.ClientManager != null)
        {
            var localConnection = FishNet.InstanceFinder.ClientManager.Connection;
            foreach (var player in allPlayers)
            {
                if (player != null && player.Owner == localConnection)
                {
                    localPlayer = player;
                    LogDebug($"Found local player by Connection match: {player.PlayerName.Value}");
                    break;
                }
            }
        }
        
        // 3. Special handling for host
        if (localPlayer == null && FishNet.InstanceFinder.IsHostStarted)
        {
            // For host, if we can't find a match by ownership, try to find a player with the host's connection (ClientId 0)
            var localConnection = FishNet.InstanceFinder.ClientManager.Connection;
            if (localConnection != null)
            {
                foreach (var player in allPlayers)
                {
                    if (player != null && player.Owner != null && player.Owner.ClientId == localConnection.ClientId)
                    {
                        localPlayer = player;
                        LogDebug($"Found local player by host ClientId match: {player.PlayerName.Value}");
                        break;
                    }
                }
                
                // If still no match, host may own server objects (OwnerId = -1)
                if (localPlayer == null)
                {
                    foreach (var player in allPlayers)
                    {
                        if (player != null && player.Owner != null && player.Owner.ClientId == -1)
                        {
                            localPlayer = player;
                            LogDebug($"Found local player as server-owned object for host: {player.PlayerName.Value}");
                            break;
                        }
                    }
                }
            }
        }
        
        // If we found a local player, return its connection
        if (localPlayer != null && localPlayer.Owner != null)
        {
            LogDebug($"Using connection from player {localPlayer.PlayerName.Value} (ClientId: {localPlayer.Owner.ClientId})");
            return localPlayer.Owner;
        }
        
        // Fallback: Try to get the client connection directly from FishNet
        if (FishNet.InstanceFinder.ClientManager != null && FishNet.InstanceFinder.ClientManager.Connection != null)
        {
            LogDebug($"Using direct ClientManager connection (ClientId: {FishNet.InstanceFinder.ClientManager.Connection.ClientId})");
            return FishNet.InstanceFinder.ClientManager.Connection;
        }
        
        LogDebug("Failed to find any valid connection for visibility management");
        return null;
    }
    
    /// <summary>
    /// Log debug messages if debug is enabled
    /// </summary>
    private void LogDebug(string message)
    {
        if (debugLogEnabled)
        {
            Debug.Log($"[EntityVisibilityManager] {message}");
        }
    }
} 