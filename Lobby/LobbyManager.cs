using UnityEngine;
using FishNet.Connection;
using FishNet.Managing;
using System.Collections.Generic;
using FishNet.Transporting;
using Steamworks;
using FishNet;
using FishNet.Object;

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject lobbyCanvas;
    [SerializeField] private LobbyUIManager lobbyUIManager;
    [SerializeField] private GameManager gameManager;

    // Lobby state
    private List<PlayerInfo> players = new List<PlayerInfo>();
    private bool isHost = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (lobbyUIManager == null)
            lobbyUIManager = FindFirstObjectByType<LobbyUIManager>();

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();
    }

    public void InitializeLobby(bool isHost)
    {
        this.isHost = isHost;
        players.Clear();
        
        // Show lobby canvas
        if (lobbyCanvas != null)
            lobbyCanvas.SetActive(true);
        
        // Update UI
        UpdatePlayerList();
        UpdateLobbyControls();
    }

    public void CloseLobby()
    {
        // Hide lobby canvas
        if (lobbyCanvas != null)
            lobbyCanvas.SetActive(false);
        
        players.Clear();
    }

    public void AddPlayer(NetworkConnection conn)
    {
        if (InstanceFinder.NetworkManager == null || !InstanceFinder.NetworkManager.IsServerStarted) return;
        
        // Find the NetworkPlayer object for this connection
        NetworkPlayer networkPlayer = conn.FirstObject?.GetComponent<NetworkPlayer>();
        if (networkPlayer == null)
        {
            Debug.LogError($"Could not find NetworkPlayer for connection {conn.ClientId} in AddPlayer.");
            return;
        }

        Debug.Log($"Attempting to add player: (ConnId: {conn.ClientId})");
        
        // Avoid adding duplicates
        if (!players.Exists(p => p.ConnectionId == conn.ClientId))
        {
            // Create PlayerInfo but get name dynamically later
            PlayerInfo newPlayer = new PlayerInfo
            {
                ConnectionId = conn.ClientId,
                PlayerName = "Connecting...", // Temporary name
                IsReady = false
            };
            players.Add(newPlayer);
            Debug.Log($"Added player placeholder (ConnId: {conn.ClientId}). Current count: {players.Count}");
            
            // Update player list for all clients (will fetch current names)
            UpdateAndSendPlayerList();
        }
        else
        {
            Debug.LogWarning($"Player (ConnId: {conn.ClientId}) already exists in the list.");
            // Still update the list in case their name changed somehow before fully joining?
            UpdateAndSendPlayerList();
        }
    }
    
    public void SetPlayerReady(NetworkConnection conn, bool isReady)
    {
        if (InstanceFinder.NetworkManager == null || !InstanceFinder.NetworkManager.IsServerStarted) return;

        PlayerInfo playerInfo = players.Find(p => p.ConnectionId == conn.ClientId);
        if (playerInfo != null)
        {
            playerInfo.IsReady = isReady;
            Debug.Log($"Player (ConnId: {conn.ClientId}) ready status set to {isReady}");
            
            // Update player list for all clients (also fetches current names)
            UpdateAndSendPlayerList();
            
            // Check if all players are ready
            CheckAllPlayersReady();
        }
        else
        {
             Debug.LogWarning($"Could not find player with ConnId {conn.ClientId} in SetPlayerReady.");
        }
    }
    
    private void CheckAllPlayersReady()
    {
        if (InstanceFinder.NetworkManager == null || !InstanceFinder.NetworkManager.IsServerStarted) return;

        if (players.Count == 0)
        {
            RpcSetStartGameButtonActive(false); // Cannot start with 0 players
            return;
        }
            
        // Check server's internal list
        bool allReady = players.TrueForAll(p => p.IsReady);
        
        // Enable start game button for host if all players are ready
        // Send the state to clients via RPC
        RpcSetStartGameButtonActive(allReady);
    }
    
    public void StartGame()
    {
        if (InstanceFinder.NetworkManager == null || !InstanceFinder.NetworkManager.IsServerStarted) return;

        // Add extra check: ensure all players are ready before starting
        if (!AreAllPlayersReady(true))
        {
            Debug.LogWarning("Attempted to start game, but not all players are ready.");
            return;
        }
        
        Debug.Log("Server starting game...");
        // Transition to game state
        RpcStartGame();
    }

    public void RemovePlayer(int connectionId)
    {
        // Remove player from list
        int removedCount = players.RemoveAll(p => p.ConnectionId == connectionId);
        
        if (removedCount > 0)
        {
            Debug.Log($"Removed player with ConnectionId {connectionId}. Current count: {players.Count}");
            // Update player list for all clients
            UpdateAndSendPlayerList();
            // Update controls in case the leaving player affected readiness
            CheckAllPlayersReady();
        }
        else
        {
             Debug.LogWarning($"Tried to remove player with ConnectionId {connectionId}, but they were not found.");
        }
    }

    [Server]
    private void UpdateAndSendPlayerList()
    {
        if (InstanceFinder.NetworkManager == null || !InstanceFinder.NetworkManager.IsServerStarted) return;
        
        List<PlayerInfo> currentPlayersInfo = new List<PlayerInfo>();
        
        // Iterate through the Connection IDs stored internally
        // Use a copy of the list to avoid modification issues if a player disconnects during iteration
        List<PlayerInfo> playersCopy = new List<PlayerInfo>(players); 
        
        foreach (var playerInternalInfo in playersCopy)
        {
            // Find the corresponding NetworkConnection
            if (InstanceFinder.ServerManager.Clients.TryGetValue(playerInternalInfo.ConnectionId, out NetworkConnection conn))
            {
                 // Find the NetworkPlayer object for this connection
                NetworkPlayer networkPlayer = conn.FirstObject?.GetComponent<NetworkPlayer>();
                if (networkPlayer != null)
                {
                    // Create a new PlayerInfo with the *current* name from NetworkPlayer
                    currentPlayersInfo.Add(new PlayerInfo
                    {
                        ConnectionId = playerInternalInfo.ConnectionId,
                        PlayerName = networkPlayer.GetSteamName(), // Fetch current name
                        IsReady = playerInternalInfo.IsReady // Keep stored ready state
                    });
                }
                else
                {
                     Debug.LogWarning($"Could not find NetworkPlayer for active connection {playerInternalInfo.ConnectionId} when updating list.");
                     // Optionally add with a placeholder name or skip
                     currentPlayersInfo.Add(new PlayerInfo {
                         ConnectionId = playerInternalInfo.ConnectionId,
                         PlayerName = "Unknown",
                         IsReady = playerInternalInfo.IsReady
                     });
                }
            }
            else
            {
                // Connection might have dropped between adding and this update
                Debug.LogWarning($"Connection {playerInternalInfo.ConnectionId} not found in ServerManager.Clients during list update. Removing stale entry.");
                // Ensure the stale entry is removed from the main list
                players.RemoveAll(p => p.ConnectionId == playerInternalInfo.ConnectionId);
            }
        }
        
        // Send the freshly constructed list to all clients
        ObserversUpdatePlayerList(currentPlayersInfo);
    }

    [ObserversRpc]
    public void ObserversUpdatePlayerList(List<PlayerInfo> updatedPlayers)
    {
        // Update UI
        if (lobbyUIManager != null)
        {
            lobbyUIManager.UpdatePlayerList(updatedPlayers);
        }
        else
        {
            Debug.LogWarning("LobbyUIManager is null in ObserversUpdatePlayerList");
        }
        
        // Also update controls based on the received list (for clients)
        if (!IsServerInitialized && lobbyUIManager != null)
        {
            bool allReady = updatedPlayers.Count > 0 && updatedPlayers.TrueForAll(p => p.IsReady);
            lobbyUIManager.SetStartGameButtonActive(allReady && InstanceFinder.IsHost); // Show button only if host and all ready
        }
    }

    [ObserversRpc]
    public void RpcSetStartGameButtonActive(bool active)
    {
        if (lobbyUIManager != null)
        {
            // Only show the button for the host client
            lobbyUIManager.SetStartGameButtonActive(active && InstanceFinder.IsHost);
        }
        else
        {
            Debug.LogWarning("LobbyUIManager is null in RpcSetStartGameButtonActive");
        }
    }

    [ObserversRpc]
    public void RpcStartGame()
    {
        Debug.Log("RpcStartGame received. Requesting state change to Combat.");
        if (gameManager != null)
            gameManager.SetGameState(GameState.Combat);
    }

    public void UpdatePlayerList()
    {
        if (lobbyUIManager != null)
        {
            // Send the current server-side list to all clients via RPC
            if (InstanceFinder.IsServerStarted) // Simplified check
            {
                 // Call the method that constructs the list with current names
                UpdateAndSendPlayerList();
            }
        }
        else Debug.LogWarning("LobbyUIManager null in UpdatePlayerList");
    }

    public void UpdateLobbyControls()
    {
        if (lobbyUIManager != null)
        {
            if (InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.IsServerStarted)
            {
                CheckAllPlayersReady(); // This will trigger the RPC to update start button
            }
        }
        else Debug.LogWarning("LobbyUIManager null in UpdateLobbyControls");
    }

    public bool AreAllPlayersReady(bool serverOnly = false)
    {
        if (!serverOnly && !IsServerInitialized) // Use IsServerInitialized for clarity
        {
            Debug.LogWarning("AreAllPlayersReady called on client - relying on UI state might be inaccurate.");
            // Clients should ideally rely on the UI state driven by ObserversUpdatePlayerList/RpcSetStartGameButtonActive
            // Returning false here might be safer than trying to guess state.
            return false; 
        }

        if (players.Count == 0)
            return false; // Cannot be ready with 0 players

        // Check server's internal list
        return players.TrueForAll(p => p.IsReady);
    }

    // Called by CombatManager when combat finishes
    [Server]
    public void OnCombatEnded()
    {
        Debug.Log("[LobbyManager] Combat has ended. Returning to Lobby state.");
        
        // Reset player ready states
        foreach (PlayerInfo player in players)
        {
            player.IsReady = false;
        }
        
        // Update player list on all clients
        ObserversUpdatePlayerList(players);
        
        // Deactivate start game button
        RpcSetStartGameButtonActive(false);
        
        // Set game state back to Lobby
        if (gameManager != null)
            gameManager.SetGameState(GameState.Lobby);
    }

    // New ServerRpc for clients to request starting the game
    [ServerRpc(RequireOwnership = false)] // Allow any client to call this
    public void CmdRequestStartGame(NetworkConnection sender = null) // Use sender for potential validation
    {
        Debug.Log($"CmdRequestStartGame received from ClientId: {sender?.ClientId ?? -1}");
        
        // Only start if all players are ready
        if (AreAllPlayersReady(true)) // Use serverOnly check
        {
            Debug.Log("Server starting game based on client request...");
            // Call the existing ObserversRpc to notify all clients and change state
            RpcStartGame();
        }
        else
        {
            Debug.LogWarning($"Client {sender?.ClientId ?? -1} requested start game, but not all players are ready.");
            // Optionally send a TargetRpc back to the sender indicating failure
        }
    }
}