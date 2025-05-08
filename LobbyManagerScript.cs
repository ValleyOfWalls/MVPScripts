using UnityEngine;
using UnityEngine.UI;
using FishNet.Managing;
using FishNet.Connection;
using FishNet.Object;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Manages the player lobby UI and functionality, including player list, ready states, and game start logic.
/// Attach to: The LobbyCanvas GameObject or a dedicated NetworkObject that manages the lobby scene.
/// </summary>
public class LobbyManagerScript : NetworkBehaviour
{
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startButton;
    [SerializeField] private GameObject lobbyCanvas;
    [SerializeField] private GameObject combatCanvas;
    [SerializeField] private TextMeshProUGUI playerListText; // For displaying player names

    private NetworkManager fishNetManager; // Standard FishNet NetworkManager
    // private SteamAndLobbyHandler steamAndLobbyHandler; // If needed for specific Steam interactions beyond what FishNet handles

    private List<NetworkConnection> connectedPlayers = new List<NetworkConnection>();
    private Dictionary<NetworkConnection, bool> playerReadyStates = new Dictionary<NetworkConnection, bool>();
    private Dictionary<NetworkConnection, string> playerDisplayNames = new Dictionary<NetworkConnection, string>(); // Stores name like "Player X (Ready)"

    private void Awake()
    {
        fishNetManager = Object.FindFirstObjectByType<NetworkManager>();
        if (fishNetManager == null)
        {
            Debug.LogError("LobbyManagerScript: FishNet NetworkManager not found in scene.");
            return;
        }
        // steamAndLobbyHandler = SteamAndLobbyHandler.Instance; // Get instance if needed

        // The actual Steam connection initiation is now handled by StartScreenManager calling SteamAndLobbyHandler.
        // This script reacts to players joining via FishNet events.

        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyButtonPressed);
        }
        else
        {
            Debug.LogError("Ready Button is not assigned in the Inspector.");
        }

        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartButtonPressed);
            startButton.interactable = false; // Disabled until conditions are met
        }
        else
        {
            Debug.LogError("Start Button is not assigned in the Inspector.");
        }

        if (lobbyCanvas == null) Debug.LogError("Lobby Canvas is not assigned.");
        if (combatCanvas == null) Debug.LogError("Combat Canvas is not assigned.");
        if (playerListText == null) Debug.LogError("Player List Text is not assigned.");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // Client is responsible for requesting its name to be added.
        // Using LocalConnection.ClientId provides a unique ID for the placeholder name.
        // TODO: Get actual Steam Name here if possible
        string initialPlayerName = (SteamNetworkIntegration.Instance != null && SteamNetworkIntegration.Instance.IsSteamInitialized) ? SteamNetworkIntegration.Instance.GetPlayerName() : "Player";
        initialPlayerName += " (" + LocalConnection.ClientId + ")"; // ClientId will be -1 for Host, >=1 for Remote
        CmdServerAddPlayer(LocalConnection, initialPlayerName);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // REMOVED: Server no longer adds its internal ClientId 0 connection here.
        // The host's actual player data (ClientId -1) gets added via its OnStartClient -> CmdServerAddPlayer call.
        // if (IsServerStarted && !IsClientOnlyStarted) 
        // {
        //     string hostPlayerName = (SteamNetworkIntegration.Instance != null && SteamNetworkIntegration.Instance.IsSteamInitialized) ? SteamNetworkIntegration.Instance.GetPlayerName() : "Host";
        //     hostPlayerName += " (" + LocalConnection.ClientId + ")";
        //     ServerAddPlayerLogic(LocalConnection, hostPlayerName); 
        // }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CmdServerAddPlayer(NetworkConnection conn, string playerName)
    {
        ServerAddPlayerLogic(conn, playerName);
    }

    [Server]
    private void ServerAddPlayerLogic(NetworkConnection conn, string playerName)
    {
        if (!connectedPlayers.Contains(conn))
        {
            connectedPlayers.Add(conn);
            playerReadyStates[conn] = false; // Default to not ready
            playerDisplayNames[conn] = playerName; // Store base name
            Debug.Log($"Player {playerName} (ConnId: {conn.ClientId}) added to lobby on server.");
            BroadcastFullPlayerList();
            CheckAllPlayersReady(); // Server checks ready state
        }
    }

    [ServerRpc(RequireOwnership = false)] // Called by client when they press ready button
    private void CmdTogglePlayerReadyState(NetworkConnection conn = null) // conn is auto-filled by FishNet
    {
        if (conn == null) return; // Should not happen with RequireOwnership = false if called correctly

        if (playerReadyStates.TryGetValue(conn, out bool currentState))
        {
            playerReadyStates[conn] = !currentState;
            Debug.Log($"Player {playerDisplayNames[conn]} (ConnId: {conn.ClientId}) toggled ready state to: {!currentState} on server.");
            BroadcastFullPlayerList();
            CheckAllPlayersReady(); // Server re-checks
        }
        else
        {
            Debug.LogWarning($"CmdTogglePlayerReadyState: Could not find player for connection {conn.ClientId}");
        }
    }

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

    [TargetRpc]
    private void TargetRpcUpdatePlayerListUI(NetworkConnection conn, List<string> displayNamesToShow)
    {
        if (playerListText != null)
        {
            playerListText.text = "Players in Lobby:\n" + string.Join("\n", displayNamesToShow);
        }
        // The CheckAllPlayersReady call is removed from here, server handles it.
    }

    // This is called by the local client's ready button.
    private void OnReadyButtonPressed()
    {
        CmdTogglePlayerReadyState(); // ServerRpc will use LocalConnection automatically
    }

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

        Debug.Log($"CheckAllPlayersReady (Server): Total={totalPlayers}, Ready={currentReadyCount}, AllReadyLogicResult={allReady}. Setting startButton.interactable for server instance.");

        if (startButton != null) 
        {
            // The server (host) instance directly sets its button state.
            // This is authoritative for the server logic.
            startButton.interactable = allReady;
        }
        // This RPC will ensure all clients, including the host acting as a client/observer, get the state.
        RpcUpdateStartButtonState(allReady);
    }

    [ObserversRpc]
    private void RpcUpdateStartButtonState(bool interactable)
    {
        Debug.Log($"RpcUpdateStartButtonState called on {(IsServerStarted ? "Host/Server" : "Client")}. Setting startButton.interactable to: {interactable}");
        if (startButton != null) 
        {
            startButton.interactable = interactable;
        }
        else
        {
            Debug.LogWarning("RpcUpdateStartButtonState: startButton is null on this instance.");
        }
    }

    private void OnStartButtonPressed()
    {
        if (IsServerStarted) // Only server can start the game
        {
            // Re-check conditions directly here as a final safeguard, though CheckAllPlayersReady should keep interactable state correct.
            int totalPlayers = connectedPlayers.Count;
            int currentReadyCount = 0;
            foreach (bool ready in playerReadyStates.Values)
            {
                if (ready) currentReadyCount++;
            }
            bool canStart = (totalPlayers > 0 && currentReadyCount == totalPlayers);
            // bool canStart = (totalPlayers > 1 && currentReadyCount == totalPlayers);

            if (canStart)
            {
                Debug.Log("All players ready. Server is starting game...");
                RpcStartGame();
            }
            else
            {
                Debug.LogWarning("Not all players are ready or not enough players to start.");
            }
        }
        else
        {
            Debug.LogWarning("Client tried to press Start Game button. This action is server-authoritative.");
        }
    }

    [ObserversRpc]
    private void RpcStartGame()
    {
        Debug.Log("RpcStartGame received. Switching to combat canvas.");
        if (lobbyCanvas != null) lobbyCanvas.SetActive(false);
        if (combatCanvas != null) combatCanvas.SetActive(true);
        
        // If CombatSetup is scene object and needs initialization on clients after canvas switch
        CombatSetup combatSetup = FindFirstObjectByType<CombatSetup>();
        if (combatSetup != null)
        {
            // combatSetup.InitializeClientCombatUI(); // Example: if you have such a method
        }
    }

    // Server-side logic to handle a player disconnecting
    [Server]
    public void ServerHandlePlayerDisconnect(NetworkConnection conn)
    {
        if (connectedPlayers.Contains(conn))
        {
            Debug.Log($"Player (ConnId: {conn.ClientId}, Name: {playerDisplayNames.GetValueOrDefault(conn, "N/A")}) disconnected. Removing from lobby.");
            connectedPlayers.Remove(conn);
            playerReadyStates.Remove(conn);
            playerDisplayNames.Remove(conn);
            
            BroadcastFullPlayerList();
            CheckAllPlayersReady();
        }
    }

    // This method needs to be called from SteamNetworkIntegration when FishNet's ServerManager.OnRemoteConnectionState (Stopped) fires.
    // In SteamNetworkIntegration, in ServerManager_OnRemoteConnectionState:
    // else if (args.ConnectionState == RemoteConnectionState.Stopped)
    // {
    //     LobbyManagerScript lobbyManager = FindObjectOfType<LobbyManagerScript>();
    //     if (lobbyManager != null) lobbyManager.ServerHandlePlayerDisconnect(conn);
    // }

    // Called when a client initially joins a lobby to set up its UI correctly
    // This is more of a local setup method.
    public void PrepareUIForLobbyJoin()
    {
        Debug.Log("LobbyManagerScript: Preparing UI for client joining lobby");
        if (lobbyCanvas != null) lobbyCanvas.SetActive(true);
        if (combatCanvas != null) combatCanvas.SetActive(false);
        if (playerListText != null) playerListText.text = "Players in Lobby:\nConnecting...";
        if (startButton != null && !IsServerStarted) startButton.interactable = false; // Client's start button is initially off
        if (readyButton != null) readyButton.interactable = true;
    }

    // REMOVE OLD/UNUSED METHODS like AddPlayerName, UpdatePlayerListUI (now TargetRpcUpdatePlayerListUI), 
    // and the old ServerAddPlayerToList if the new CmdServerAddPlayer/ServerAddPlayerLogic replaces it fully.
    // The old HandlePlayerDisconnect also needs to be integrated or replaced by ServerHandlePlayerDisconnect.
} 