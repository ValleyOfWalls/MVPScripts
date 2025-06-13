using UnityEngine;
using FishNet.Managing;
using FishNet.Connection;
using FishNet.Object;
using System.Collections.Generic;
using System;

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

    // Event that triggers when player ready states change
    public event Action OnPlayersReadyStateChanged;
    
    // Static event that fires when LobbyManager becomes available for clients
    public static event System.Action<LobbyManager> OnLobbyManagerAvailable;

    private void Awake()
    {
        FindRequiredComponents();
    }
    
    // Get the current number of connected players
    public int GetConnectedPlayerCount()
    {
        return connectedPlayers.Count;
    }
    
    // Check if all players are in ready state
    public bool AreAllPlayersReady()
    {
        return connectedPlayers.Count > 0 && AreAllPlayersReadyInternal();
    }
    
    // Returns connected players list for testing purposes
    public IReadOnlyList<NetworkConnection> GetConnectedPlayers()
    {
        return connectedPlayers.AsReadOnly();
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
        
        // No longer show lobby UI - we go directly to character selection
        // The character selection phase now includes lobby functionality
        
        // Notify that LobbyManager is now available (for any remaining dependencies)
        OnLobbyManagerAvailable?.Invoke(this);
        Debug.Log("LobbyManager: OnStartClient called, transitioning directly to character selection");
        
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
            
            // Transition to character selection when first player joins
            if (connectedPlayers.Count == 1)
            {
                Debug.Log("LobbyManager: First player joined, transitioning to character selection phase");
                InitializeCharacterSelection();
            }
            else
            {
                // For subsequent players, notify the existing character selection manager
                Debug.Log($"LobbyManager: Player {connectedPlayers.Count} joined, notifying character selection manager");
                NotifyCharacterSelectionManagerOfNewPlayer(conn, playerName);
            }
            
            Debug.Log($"LobbyManager: Added player {playerName} (total players: {connectedPlayers.Count})");
        }
    }
    
    /// <summary>
    /// Notifies the CharacterSelectionManager about a new player joining during character selection phase
    /// </summary>
    [Server]
    private void NotifyCharacterSelectionManagerOfNewPlayer(NetworkConnection conn, string playerName)
    {
        CharacterSelectionManager characterSelectionManager = FindFirstObjectByType<CharacterSelectionManager>();
        if (characterSelectionManager != null)
        {
            // Manually add the player to the character selection manager
            characterSelectionManager.ServerAddPlayerDirectly(conn, playerName);
            Debug.Log($"LobbyManager: Notified CharacterSelectionManager about new player {playerName}");
        }
        else
        {
            Debug.LogWarning($"LobbyManager: CharacterSelectionManager not found, cannot add player {playerName} to character selection");
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
            
            // Notify about player ready state change
            OnPlayersReadyStateChanged?.Invoke();
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

        bool allReady = AreAllPlayersReadyInternal();
        RpcUpdateStartButtonState(allReady);
    }
    
    [Server]
    private bool AreAllPlayersReadyInternal()
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

        bool canStart = AreAllPlayersReadyInternal() && connectedPlayers.Count >= 2;
        if (canStart)
        {
            RpcStartGame();
        }
    }

    // Observer RPC to start the game on all clients
    [ObserversRpc]
    private void RpcStartGame()
    {
        // Tell UI manager to hide lobby UI (if it exists)
        if (uiManager != null)
        {
            uiManager.HideLobbyUI();
        }
        
        // Transition directly to character selection phase (which now includes lobby functionality)
        if (gamePhaseManager != null)
        {
            gamePhaseManager.SetCharacterSelectionPhase();
        }
        
        // Find and initialize the character selection setup
        InitializeCharacterSelection();
    }
    
    private void InitializeCharacterSelection()
    {
        if (!IsServerStarted) return;
        
        CharacterSelectionSetup characterSelectionSetup = FindFirstObjectByType<CharacterSelectionSetup>();
        if (characterSelectionSetup != null)
        {
            characterSelectionSetup.InitializeCharacterSelection();
            Debug.Log("LobbyManager: Initialized character selection setup (with integrated lobby functionality)");
        }
        else
        {
            Debug.LogError("LobbyManager: CharacterSelectionSetup not found, cannot initialize character selection");
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
            
            // Notify about player ready state change
            OnPlayersReadyStateChanged?.Invoke();
        }
    }
} 