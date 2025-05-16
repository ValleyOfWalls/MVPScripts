using UnityEngine;
using FishNet.Managing;
using FishNet.Connection;
using FishNet.Object;
using System.Collections.Generic;

/// <summary>
/// Manages the core lobby functionality, player tracking, ready states, and game start logic.
/// Attach to: A dedicated LobbyManager GameObject with NetworkObject component.
/// </summary>
public class LobbyManager : NetworkBehaviour
{
    [SerializeField] private LobbyUIManager uiManager;
    
    private NetworkManager fishNetManager;
    private GamePhaseManager gamePhaseManager;
    
    private List<NetworkConnection> connectedPlayers = new List<NetworkConnection>();
    private Dictionary<NetworkConnection, bool> playerReadyStates = new Dictionary<NetworkConnection, bool>();
    private Dictionary<NetworkConnection, string> playerDisplayNames = new Dictionary<NetworkConnection, string>();

    private void Awake()
    {
        FindRequiredComponents();
    }
    
    private void FindRequiredComponents()
    {
        fishNetManager = FindFirstObjectByType<NetworkManager>();
        gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
        
        if (fishNetManager == null) Debug.LogError("LobbyManager: FishNet NetworkManager not found in scene.");
        if (uiManager == null) Debug.LogError("LobbyManager: LobbyUIManager reference not assigned in the Inspector.");
        if (gamePhaseManager == null) Debug.LogError("LobbyManager: GamePhaseManager not found in scene.");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        string initialPlayerName = GetInitialPlayerName();
        CmdServerAddPlayer(LocalConnection, initialPlayerName);
    }
    
    private string GetInitialPlayerName()
    {
        string name = (SteamNetworkIntegration.Instance != null && SteamNetworkIntegration.Instance.IsSteamInitialized) 
            ? SteamNetworkIntegration.Instance.GetPlayerName() 
            : "Player";
        
        return name + " (" + LocalConnection.ClientId + ")";
    }

    #region Server Methods
    
    // Server RPC to add a player to the lobby
    [ServerRpc(RequireOwnership = false)]
    private void CmdServerAddPlayer(NetworkConnection conn, string playerName)
    {
        ServerAddPlayerLogic(conn, playerName);
    }

    // Server-side logic to add a player
    [Server]
    private void ServerAddPlayerLogic(NetworkConnection conn, string playerName)
    {
        if (!connectedPlayers.Contains(conn))
        {
            connectedPlayers.Add(conn);
            if (GameManager.Instance != null)
            {
                playerReadyStates[conn] = GameManager.Instance.AutoReadyPlayersOnJoin;
            }
            else
            {
                playerReadyStates[conn] = false;
                Debug.LogWarning("LobbyManager: GameManager.Instance not found. Defaulting player ready state to false.");
            }
            playerDisplayNames[conn] = playerName;
            BroadcastFullPlayerList();
            CheckAllPlayersReady();
        }
    }

    // Toggles a player's ready state
    [ServerRpc(RequireOwnership = false)]
    public void CmdTogglePlayerReadyState(NetworkConnection conn = null)
    {
        if (conn == null) return;

        if (playerReadyStates.TryGetValue(conn, out bool currentState))
        {
            playerReadyStates[conn] = !currentState;
            BroadcastFullPlayerList();
            CheckAllPlayersReady();
        }
    }

    // Broadcasts the player list to all clients
    [Server]
    private void BroadcastFullPlayerList()
    {
        List<string> currentDisplayNames = GetFormattedPlayerNames();
        BroadcastPlayerListToAllClients(currentDisplayNames);
    }
    
    [Server]
    private List<string> GetFormattedPlayerNames()
    {
        List<string> currentDisplayNames = new List<string>();
        foreach (NetworkConnection pc in connectedPlayers)
        {
            if (playerDisplayNames.TryGetValue(pc, out string baseName) && playerReadyStates.TryGetValue(pc, out bool isReady))
            {
                currentDisplayNames.Add($"{baseName} {(isReady ? "(Ready)" : "(Not Ready)")}");
            }
        }
        return currentDisplayNames;
    }
    
    [Server]
    private void BroadcastPlayerListToAllClients(List<string> playerList)
    {
        foreach (NetworkConnection clientConn in ServerManager.Clients.Values)
        {
            TargetRpcUpdatePlayerListUI(clientConn, playerList);
        }
    }

    // Target RPC to update a specific client's player list UI
    [TargetRpc]
    private void TargetRpcUpdatePlayerListUI(NetworkConnection conn, List<string> displayNamesToShow)
    {
        if (uiManager != null)
        {
            uiManager.UpdatePlayerListUI(displayNamesToShow);
        }
    }

    // Checks if all players are ready and updates the start button state
    [Server] 
    private void CheckAllPlayersReady()
    {
        if (!IsServerStarted) return;

        bool allReady = AreAllPlayersReady();
        RpcUpdateStartButtonState(allReady);
    }
    
    [Server]
    private bool AreAllPlayersReady()
    {
        int totalPlayers = connectedPlayers.Count;
        if (totalPlayers == 0) return false;
        
        int readyCount = 0;
        foreach (bool ready in playerReadyStates.Values)
        {
            if (ready) readyCount++;
        }
        
        return readyCount == totalPlayers;
    }

    // Observer RPC to update the start button state on all clients
    [ObserversRpc]
    private void RpcUpdateStartButtonState(bool interactable)
    {
        if (uiManager != null)
        {
            uiManager.SetStartButtonInteractable(interactable);
        }
    }
    
    #endregion

    // Initiates the game start process when conditions are met
    [ServerRpc(RequireOwnership = false)]
    public void RequestStartGame(NetworkConnection conn = null)
    {
        if (conn == null) return;

        bool canStart = AreAllPlayersReady() && connectedPlayers.Count >= 2;
        if (canStart)
        {
            RpcStartGame();
        }
    }

    // Observer RPC to start the game on all clients
    [ObserversRpc]
    private void RpcStartGame()
    {
        // Tell UI manager to hide lobby UI
        if (uiManager != null)
        {
            uiManager.HideLobbyUI();
        }
        
        // Use GamePhaseManager to transition to Combat phase
        if (gamePhaseManager != null)
        {
            gamePhaseManager.SetCombatPhase();
        }
        
        // Find and initialize the combat setup
        InitializeCombat();
    }
    
    private void InitializeCombat()
    {
        if (!IsServerStarted) return;
        
        CombatSetup combatSetup = FindFirstObjectByType<CombatSetup>();
        if (combatSetup != null)
        {
            combatSetup.InitializeCombat();
        }
    }

    // Server-side logic to handle a player disconnecting
    [Server]
    public void ServerHandlePlayerDisconnect(NetworkConnection conn)
    {
        if (connectedPlayers.Contains(conn))
        {
            connectedPlayers.Remove(conn);
            playerReadyStates.Remove(conn);
            playerDisplayNames.Remove(conn);
            
            BroadcastFullPlayerList();
            CheckAllPlayersReady();
        }
    }
} 