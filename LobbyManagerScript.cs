using UnityEngine;
using UnityEngine.UI;
using FishNet.Managing;
using FishNet.Connection;
using FishNet.Object;
using System.Collections.Generic;
using TMPro;

public class LobbyManagerScript : NetworkBehaviour
{
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startButton;
    [SerializeField] private GameObject lobbyCanvas;
    [SerializeField] private GameObject combatCanvas;
    [SerializeField] private TextMeshProUGUI playerListText; // For displaying player names

    private NetworkManager fishNetManager; // Standard FishNet NetworkManager
    // private SteamAndLobbyHandler steamAndLobbyHandler; // If needed for specific Steam interactions beyond what FishNet handles

    private List<NetworkConnection> readyPlayers = new List<NetworkConnection>();
    private List<string> playerNames = new List<string>(); // To store and display names

    private void Awake()
    {
        fishNetManager = FishNet.InstanceFinder.NetworkManager;
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
        if (IsClientStarted)
        {
            // Client tells server it has joined and its name (e.g., from Steamworks)
            // This part needs integration with your Steamworks name retrieval
            string playerName = "Player_" + LocalConnection.ClientId; // Placeholder
            ServerAddPlayerToList(LocalConnection, playerName);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // Server will also add itself if it's a host
        if (IsServerStarted && !IsClientOnlyStarted)
        {
             string hostPlayerName = "Host_" + LocalConnection.ClientId; // Placeholder
             AddPlayerName(hostPlayerName);
             UpdatePlayerListUI();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ServerAddPlayerToList(NetworkConnection conn, string playerName)
    {
        // This logic should ideally be in NetworkManager or a dedicated player management script
        // For now, directly adding to LobbyManagerScript for simplicity
        playerNames.Add(playerName + " (Not Ready)");
        UpdatePlayerListUI();
        TargetRpcUpdatePlayerList(conn, new List<string>(playerNames)); // Send full list to new client

        // Update all other clients
        foreach (NetworkConnection clientConn in ServerManager.Clients.Values)
        {
            if (clientConn != conn) // Don't send to the new client again
            {
                TargetRpcUpdatePlayerList(clientConn, new List<string>(playerNames));
            }
        }
    }

    [TargetRpc]
    private void TargetRpcUpdatePlayerList(NetworkConnection conn, List<string> currentPlayers)
    {
        playerNames = currentPlayers;
        UpdatePlayerListUI();
    }

    private void AddPlayerName(string name)
    {
        if (!playerNames.Contains(name))
        {
            playerNames.Add(name);
        }
    }

    private void UpdatePlayerListUI()
    {
        if (playerListText != null)
        {
            playerListText.text = "Players in Lobby:\n" + string.Join("\n", playerNames);
        }
        CheckAllPlayersReady(); // Check if start button should be enabled
    }

    private void OnReadyButtonPressed()
    {
        if (IsClientStarted)
        {
            CmdTogglePlayerReadyState(LocalConnection);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CmdTogglePlayerReadyState(NetworkConnection conn)
    {
        int playerIndex = -1;
        // Find the player by connection to update their ready status text
        // This assumes playerNames list is ordered by connection or has a way to map conn to index
        // This part is a bit simplified and might need a more robust way to identify players.
        // For now, let's assume we can find a matching name to update. 
        // A better approach would be to store player objects with their connection and ready state.

        string connStringId = "Player_" + conn.ClientId; // Placeholder to match initial name
        if (conn.IsHost) connStringId = "Host_" + conn.ClientId;

        for(int i=0; i < playerNames.Count; ++i)
        {
            if(playerNames[i].StartsWith(connStringId))
            {
                playerIndex = i;
                break;
            }
        }

        if (readyPlayers.Contains(conn))
        {
            readyPlayers.Remove(conn);
            if(playerIndex != -1) playerNames[playerIndex] = playerNames[playerIndex].Replace(" (Ready)", " (Not Ready)");
            Debug.Log("Player " + conn.ClientId + " is no longer ready.");
        }
        else
        {
            readyPlayers.Add(conn);
            if(playerIndex != -1) playerNames[playerIndex] = playerNames[playerIndex].Replace(" (Not Ready)", " (Ready)");
            Debug.Log("Player " + conn.ClientId + " is ready.");
        }

        // Update UI for all clients
        foreach (NetworkConnection clientConn in ServerManager.Clients.Values)
        {
            TargetRpcUpdatePlayerList(clientConn, new List<string>(playerNames));
        }
        CheckAllPlayersReady();
    }

    [Server]
    private void CheckAllPlayersReady()
    {
        // Number of connected clients + host (if server is also a player)
        int totalPlayers = ServerManager.Clients.Count;
        if (IsServerStarted && !IsClientOnlyStarted && !ServerManager.Clients.ContainsKey(LocalConnection.ClientId)) {
            // If host is not in clients list (e.g. pure server starts first)
            // This logic might depend on how FishNet counts host
        }


        bool allReady = readyPlayers.Count == totalPlayers && totalPlayers > 1;
        if (startButton != null) 
        {
            startButton.interactable = allReady;
        }

        // Propagate interactable state to clients
        RpcUpdateStartButtonState(allReady);
    }

    [ObserversRpc]
    private void RpcUpdateStartButtonState(bool interactable)
    {
        if (startButton != null) startButton.interactable = interactable;
    }

    private void OnStartButtonPressed()
    {
        if (IsServerStarted) // Only server can start the game
        {
            if (readyPlayers.Count == ServerManager.Clients.Count && ServerManager.Clients.Count > 1)
            {
                Debug.Log("All players ready. Starting game...");
                // Tell all clients to switch canvas
                RpcStartGame();
            }
            else
            {
                Debug.LogWarning("Not all players are ready or not enough players to start.");
            }
        }
    }

    [ObserversRpc]
    private void RpcStartGame()
    {
        if (lobbyCanvas != null) lobbyCanvas.SetActive(false);
        if (combatCanvas != null) combatCanvas.SetActive(true);
        
        // Optionally, notify other scripts that the game has started
        // e.g., combatSetup.InitializeCombat(); (if combatSetup is accessible)
    }

    // Called by SteamAndLobbyHandler when a player disconnects (server-side)
    public void HandlePlayerDisconnect(NetworkConnection conn)
    {
        if (IsServerStarted)
        {
            readyPlayers.Remove(conn);

            string connStringId = "Player_" + conn.ClientId; 
             if (conn.IsHost) connStringId = "Host_" + conn.ClientId;

            string toRemove = "";
            foreach(var name in playerNames)
            {
                if(name.StartsWith(connStringId))
                {
                    toRemove = name;
                    break;
                }
            }
            if(!string.IsNullOrEmpty(toRemove)) playerNames.Remove(toRemove);

            CheckAllPlayersReady();
            // Update UI for all remaining clients
            foreach (NetworkConnection clientConn in ServerManager.Clients.Values)
            {
                TargetRpcUpdatePlayerList(clientConn, new List<string>(playerNames));
            }
        }
    }
} 