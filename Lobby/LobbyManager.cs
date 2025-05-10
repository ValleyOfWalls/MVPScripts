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
        fishNetManager = FindFirstObjectByType<NetworkManager>();
        gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
        
        if (fishNetManager == null) Debug.LogError("LobbyManager: FishNet NetworkManager not found in scene.");
        if (uiManager == null) Debug.LogError("LobbyManager: LobbyUIManager reference not assigned in the Inspector.");
        if (gamePhaseManager == null) Debug.LogError("LobbyManager: GamePhaseManager not found in scene.");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Get player name from Steam if available, otherwise use a placeholder
        string initialPlayerName = (SteamNetworkIntegration.Instance != null && SteamNetworkIntegration.Instance.IsSteamInitialized) 
            ? SteamNetworkIntegration.Instance.GetPlayerName() 
            : "Player";
        
        initialPlayerName += " (" + LocalConnection.ClientId + ")";
        CmdServerAddPlayer(LocalConnection, initialPlayerName);
    }

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
            playerReadyStates[conn] = false; // Default to not ready
            playerDisplayNames[conn] = playerName; // Store base name
            BroadcastFullPlayerList();
            CheckAllPlayersReady(); // Server checks ready state
        }
    }

    // Toggles a player's ready state (called from UI)
    [ServerRpc(RequireOwnership = false)]
    public void CmdTogglePlayerReadyState(NetworkConnection conn = null) // conn is auto-filled by FishNet
    {
        if (conn == null) return;

        if (playerReadyStates.TryGetValue(conn, out bool currentState))
        {
            playerReadyStates[conn] = !currentState;
            BroadcastFullPlayerList();
            CheckAllPlayersReady(); // Server re-checks
        }
    }

    // Broadcasts the player list to all clients
    [Server]
    private void BroadcastFullPlayerList()
    {
        List<string> currentDisplayNames = new List<string>();
        foreach (NetworkConnection pc in connectedPlayers) // Iterate in connection order
        {
            if (playerDisplayNames.TryGetValue(pc, out string baseName) && playerReadyStates.TryGetValue(pc, out bool isReady))
            {
                currentDisplayNames.Add($"{baseName} {(isReady ? "(Ready)" : "(Not Ready)")}");
            }
        }

        foreach (NetworkConnection clientConn in ServerManager.Clients.Values)
        {
            TargetRpcUpdatePlayerListUI(clientConn, currentDisplayNames);
        }
    }

    // Target RPC to update a specific client's player list UI
    [TargetRpc]
    private void TargetRpcUpdatePlayerListUI(NetworkConnection conn, List<string> displayNamesToShow)
    {
        uiManager.UpdatePlayerListUI(displayNamesToShow);
    }

    // Checks if all players are ready and updates the start button state
    [Server] 
    private void CheckAllPlayersReady()
    {
        if (!IsServerStarted) return;

        int totalPlayers = connectedPlayers.Count;
        int currentReadyCount = 0;
        foreach (bool ready in playerReadyStates.Values)
        {
            if (ready) currentReadyCount++;
        }
        
        bool allReady = (totalPlayers > 0 && currentReadyCount == totalPlayers);
        RpcUpdateStartButtonState(allReady);
    }

    // Observer RPC to update the start button state on all clients
    [ObserversRpc]
    private void RpcUpdateStartButtonState(bool interactable)
    {
        uiManager.SetStartButtonInteractable(interactable);
    }

    // Initiates the game start process when conditions are met
    public void RequestStartGame()
    {
        if (IsServerStarted) // Only server can start the game
        {
            // Re-check conditions directly here as a final safeguard
            int totalPlayers = connectedPlayers.Count;
            int currentReadyCount = 0;
            foreach (bool ready in playerReadyStates.Values)
            {
                if (ready) currentReadyCount++;
            }
            bool canStart = (totalPlayers >= 2 && currentReadyCount == totalPlayers);

            if (canStart)
            {
                RpcStartGame();
            }
        }
    }

    // Observer RPC to start the game on all clients
    [ObserversRpc]
    private void RpcStartGame()
    {
        // Tell UI manager to hide lobby UI
        uiManager.HideLobbyUI();
        
        // Use GamePhaseManager to transition to Combat phase
        if (gamePhaseManager != null)
        {
            gamePhaseManager.SetCombatPhase();
        }
        
        // Find and initialize the combat setup
        CombatSetup combatSetup = FindFirstObjectByType<CombatSetup>();
        if (combatSetup != null && IsServerStarted)
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