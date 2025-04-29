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
    private int nextPlayerId = 0;

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
        nextPlayerId = 0;
        
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
        nextPlayerId = 0;
    }

    // Get the next player ID to assign
    public int GetNextPlayerId()
    {
        return nextPlayerId++;
    }

    public void AddPlayer(NetworkConnection conn, string playerName)
    {
        if (InstanceFinder.NetworkManager == null || !InstanceFinder.NetworkManager.IsServerStarted) return;

        Debug.Log($"Adding player: {playerName} (ConnId: {conn.ClientId})");
        PlayerInfo newPlayer = new PlayerInfo
        {
            ConnectionId = conn.ClientId,
            PlayerName = playerName,
            IsReady = false
        };
        
        // Avoid adding duplicates if client connects/disconnects rapidly
        if (!players.Exists(p => p.ConnectionId == conn.ClientId))
        {
            players.Add(newPlayer);
            // Update player list for all clients
            ObserversUpdatePlayerList(players);
        }
        else
        {
            Debug.LogWarning($"Player {playerName} (ConnId: {conn.ClientId}) already exists in the list.");
        }
        
        // Update player list for all clients
        ObserversUpdatePlayerList(players);
    }
    
    public void SetPlayerReady(NetworkConnection conn, bool isReady)
    {
        if (InstanceFinder.NetworkManager == null || !InstanceFinder.NetworkManager.IsServerStarted) return;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].ConnectionId == conn.ClientId)
            {
                players[i].IsReady = isReady;
                Debug.Log($"Player {players[i].PlayerName} ready status set to {isReady}");
                break;
            }
        }
        
        // Update player list for all clients
        ObserversUpdatePlayerList(players);
        
        // Check if all players are ready
        CheckAllPlayersReady();
    }
    
    private void CheckAllPlayersReady()
    {
        if (InstanceFinder.NetworkManager == null || !InstanceFinder.NetworkManager.IsServerStarted) return;

        if (players.Count == 0)
        {
            RpcSetStartGameButtonActive(false); // Cannot start with 0 players
            return;
        }
            
        bool allReady = true;
        foreach (PlayerInfo player in players)
        {
            if (!player.IsReady)
            {
                allReady = false;
                break;
            }
        }
        
        // Enable start game button for host if all players are ready
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
        PlayerInfo disconnectedPlayer = players.Find(p => p.ConnectionId == connectionId);
        if (disconnectedPlayer != null)
        {
            players.Remove(disconnectedPlayer);
            // Update player list for all clients
            ObserversUpdatePlayerList(players);
            // Update controls in case the leaving player affected readiness
            CheckAllPlayersReady();
        }
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
    }

    [ObserversRpc]
    public void RpcSetStartGameButtonActive(bool active)
    {
        if (lobbyUIManager != null)
        {
            // Allow any player to see the button if active
            lobbyUIManager.SetStartGameButtonActive(active);
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
            if (InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.IsServerStarted)
            {
                ObserversUpdatePlayerList(players);
            }
        }
        else Debug.LogWarning("LobbyUIManager null in UpdatePlayerList");
    }

// Inside LobbyManager.cs - make sure this method exists
public void ServerSetPlayerReady(NetworkConnection conn, bool isReady)
{
    SetPlayerReady(conn, isReady);
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
        if (!serverOnly && (InstanceFinder.NetworkManager == null || !InstanceFinder.NetworkManager.IsServerStarted))
        {
            Debug.LogWarning("AreAllPlayersReady called on client - returning false.");
            return false; 
        }
        
        if (players.Count == 0)
            return false;
            
        foreach (PlayerInfo player in players)
        {
            if (!player.IsReady)
                return false;
        }
        
        return true;
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