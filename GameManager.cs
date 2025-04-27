using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing;
using System.Collections.Generic;
using FishNet.Transporting;
using Steamworks;
using DG.Tweening;
using FishNet;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject startScreenCanvas;
    [SerializeField] private GameObject lobbyCanvas;
    [SerializeField] private GameObject combatCanvas;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private SteamManager steamManager;

    [Header("Prefabs")]
    [SerializeField] public GameObject playerPrefab;

    // Game state
    private GameState currentState = GameState.StartScreen;
    private List<PlayerInfo> players = new List<PlayerInfo>();
    private bool isHost = false;
    private int nextPlayerId = 0;

    // Add at the top of the class after variable declarations
    private bool networkEventsSubscribed = false;
    private bool networkInitializationAttempted = false;

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

        if (networkManager == null)
            networkManager = FindFirstObjectByType<NetworkManager>();

        if (uiManager == null)
            uiManager = FindFirstObjectByType<UIManager>();

        if (steamManager == null)
            steamManager = FindFirstObjectByType<SteamManager>();

        // Initialize DOTween
        DOTween.SetTweensCapacity(200, 50);
        
        // Initialize NetworkManager properly
        InitializeNetworkManager();
    }

    private void InitializeNetworkManager()
    {
        if (networkManager == null)
        {
            // Try to find NetworkManager first
            networkManager = FindFirstObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError("Failed to find NetworkManager in scene");
                return;
            }
        }
        
        // Mark that we've attempted initialization
        networkInitializationAttempted = true;
        
        // Only try to subscribe to events if we haven't already
        if (!networkEventsSubscribed)
        {
            StartCoroutine(SubscribeToNetworkEvents());
        }
    }

    private System.Collections.IEnumerator SubscribeToNetworkEvents()
    {
        int attempts = 0;
        int maxAttempts = 30; // Increased number of attempts
        
        while (attempts < maxAttempts)
        {
            Debug.Log($"Attempt {attempts+1}: Looking for NetworkManager components...");
            
            // Check if NetworkManager exists and has required components
            if (networkManager != null)
            {
                try 
                {
                    // Check if ClientManager and ServerManager are ready
                    if (InstanceFinder.ClientManager != null && InstanceFinder.ServerManager != null)
                    {
                        // Success - subscribe to events using InstanceFinder
                        InstanceFinder.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
                        InstanceFinder.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
                        InstanceFinder.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
                        
                        Debug.Log("Successfully subscribed to network events using InstanceFinder.");
                        networkEventsSubscribed = true;
                        yield break;
                    }
                    else if (networkManager.ClientManager != null && networkManager.ServerManager != null)
                    {
                        // Alternative success path - use direct references
                        networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
                        networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
                        networkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
                        
                        Debug.Log("Successfully subscribed to network events using direct references.");
                        networkEventsSubscribed = true;
                        yield break;
                    }
                    else
                    {
                        // Log what we found
                        Debug.Log($"NetworkManager components not ready: " +
                                  $"InstanceFinder.ClientManager={InstanceFinder.ClientManager != null}, " +
                                  $"InstanceFinder.ServerManager={InstanceFinder.ServerManager != null}, " +
                                  $"networkManager.ClientManager={networkManager.ClientManager != null}, " +
                                  $"networkManager.ServerManager={networkManager.ServerManager != null}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Exception during network initialization (attempt {attempts+1}): {ex.Message}");
                }
            }
            
            // Wait a bit longer between attempts
            yield return new WaitForSeconds(0.5f); // Longer wait time
            attempts++;
        }
        
        Debug.LogError("Failed to initialize network managers after multiple attempts");
        Debug.LogWarning("The game will continue, but networking features might not work properly.");
    }

    private void Start()
    {
        SetGameState(GameState.StartScreen);
        
        // Check if NetworkManager was found but ClientManager/ServerManager weren't available
        if (networkManager != null && 
            (networkManager.ClientManager == null || networkManager.ServerManager == null))
        {
            Debug.Log("NetworkManager found but managers not ready. Starting delayed initialization...");
            // Try a delayed initialization as a backup
            StartCoroutine(DelayedNetworkInitialization());
        }
    }

    private System.Collections.IEnumerator DelayedNetworkInitialization()
    {
        Debug.Log("Starting delayed NetworkManager initialization");
        
        // Wait a bit longer before trying again - give the NetworkManager time to fully initialize
        yield return new WaitForSeconds(1.0f);
        
        // Try initialization again
        InitializeNetworkManager();
    }

    private void OnDestroy()
    { 
        if (networkManager != null)
        { 
            if (networkManager.ClientManager != null)
                networkManager.ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
            
            if (networkManager.ServerManager != null)
            {
                networkManager.ServerManager.OnServerConnectionState -= ServerManager_OnServerConnectionState; 
                networkManager.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState;
            }
        }
        if (Instance == this) Instance = null;
    }

    public void SetGameState(GameState newState)
    {
        currentState = newState;
        
        // Update UI based on state (ensure references are set)
        if (startScreenCanvas == null) startScreenCanvas = GameObject.Find("StartScreenCanvas");
        if (lobbyCanvas == null) lobbyCanvas = GameObject.Find("LobbyCanvas");
        if (combatCanvas == null) combatCanvas = GameObject.Find("CombatCanvas");

        if (startScreenCanvas != null)
            startScreenCanvas.SetActive(newState == GameState.StartScreen);
        else
            Debug.LogWarning("StartScreenCanvas reference missing or object not found.");
        
        if (lobbyCanvas != null)
            lobbyCanvas.SetActive(newState == GameState.Lobby);
        else
             Debug.LogWarning("LobbyCanvas reference missing or object not found.");
       
        if (combatCanvas != null)
            combatCanvas.SetActive(newState == GameState.Combat);
         else
             Debug.LogWarning("CombatCanvas reference missing or object not found.");
           
        // Inform UI manager of state change
        if (uiManager != null)
            uiManager.OnGameStateChanged(newState);
        else 
            Debug.LogWarning("UIManager reference missing in SetGameState.");
    }

    // Get the next player ID to assign
    public int GetNextPlayerId()
    {
        return nextPlayerId++;
    }

    #region Network Callbacks
    private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            Debug.Log("Connected to server");
            
            // If we're a client, wait for server to send us to lobby
            if (!isHost)
            {
                SetGameState(GameState.Lobby);
                // Tell the server about this client (player name) - MOVED to NetworkPlayer.OnStartClient
                // if (PlayerPrefs.HasKey("PlayerName"))
                // {
                //     CmdJoinLobby(PlayerPrefs.GetString("PlayerName"));
                // }
                // else
                // {
                //      CmdJoinLobby("ClientPlayer"); // Fallback name
                // }
            }
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            Debug.Log("Disconnected from server");
            SetGameState(GameState.StartScreen);
            players.Clear(); // Clear player list on disconnect
            nextPlayerId = 0; // Reset player ID counter
            isHost = false;
        }
    }

    private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            Debug.Log("Server started");
            isHost = true;
            SetGameState(GameState.Lobby);
            
            // Spawn the PlayerSpawner
            SpawnPlayerSpawner();
            
            // Add host as a player using the local client's connection
            try 
            {
                if (InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Started)
                {
                    NetworkConnection localConnection = InstanceFinder.ClientManager.Connection;
                    if (localConnection.IsValid) // Check if local client connection is valid
                    {
                        string hostName = PlayerPrefs.GetString("PlayerName", "HostPlayer"); // Get saved name or default
                        AddPlayer(localConnection, hostName); 
                    }
                    else
                    {
                        Debug.LogError("Server started, but local client connection is not valid!");
                    }
                }
                else
                {
                    Debug.LogWarning("Unable to add host player - ClientManager is null or not initialized yet");
                    // Add a delayed attempt to add the host player when client connects
                    StartCoroutine(TryAddHostPlayerDelayed());
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error while adding host player: {ex.Message}");
            }
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            Debug.Log("Server stopped");
            isHost = false;
            players.Clear();
            nextPlayerId = 0;
            SetGameState(GameState.StartScreen); // Go back to start screen when server stops
        }
    }

    private System.Collections.IEnumerator TryAddHostPlayerDelayed()
    {
        // Try several times with a delay
        for (int i = 0; i < 5; i++)
        {
            yield return new WaitForSeconds(0.5f);
            
            if (InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Started)
            {
                NetworkConnection localConnection = InstanceFinder.ClientManager.Connection;
                if (localConnection.IsValid)
                {
                    string hostName = PlayerPrefs.GetString("PlayerName", "HostPlayer");
                    AddPlayer(localConnection, hostName);
                    Debug.Log("Successfully added host player after delay");
                    yield break;
                }
            }
        }
        
        Debug.LogWarning("Failed to add host player after multiple attempts");
    }

    private void ServerManager_OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            Debug.Log($"Client connected: {conn.ClientId}");
            // Player info is now added via CmdJoinLobby
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            Debug.Log($"Client disconnected: {conn.ClientId}");
            
            // Remove player from list
            PlayerInfo disconnectedPlayer = players.Find(p => p.ConnectionId == conn.ClientId);
            if (disconnectedPlayer != null)
            {
                players.Remove(disconnectedPlayer);
                // Update player list for all clients
                RpcUpdatePlayerList(players);
                 // Update controls in case the leaving player affected readiness
                CheckAllPlayersReady();
            }
        }
    }
    #endregion

    #region Host/Server Methods
    public void StartHost()
    {
        Debug.Log("StartHost called");
        
        // Ensure we've tried to initialize
        if (!networkInitializationAttempted)
        {
            InitializeNetworkManager();
        }
        
        // Try to start server through InstanceFinder first
        if (InstanceFinder.ServerManager != null)
        {
            Debug.Log("Starting server via InstanceFinder.ServerManager");
            InstanceFinder.ServerManager.StartConnection();
            
            if (InstanceFinder.ClientManager != null)
            {
                Debug.Log("Starting client via InstanceFinder.ClientManager");
                InstanceFinder.ClientManager.StartConnection();
            }
            return;
        }
        
        // Fall back to direct reference
        if (networkManager != null)
        {
            if (networkManager.ServerManager != null)
            {
                Debug.Log("Starting server via direct ServerManager reference");
                networkManager.ServerManager.StartConnection();
            }
            else
            {
                Debug.LogError("Cannot start host: ServerManager is null");
            }
            
            if (networkManager.ClientManager != null)
            {
                Debug.Log("Starting client via direct ClientManager reference");
                networkManager.ClientManager.StartConnection();
            }
            else
            {
                Debug.LogError("Cannot start host: ClientManager is null");
            }
        }
        else
        {
            Debug.LogError("Cannot start host, NetworkManager is null!");
        }
    }
    
    // [Server] attribute is not needed on public methods in a MonoBehaviour called by ServerRpc
    // Make this internal or private if only called by CmdJoinLobby
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
            RpcUpdatePlayerList(players);
        }
        else
        {
             Debug.LogWarning($"Player {playerName} (ConnId: {conn.ClientId}) already exists in the list.");
        }
    }
    
    // [Server] attribute is not needed here either
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
        RpcUpdatePlayerList(players);
        
        // Check if all players are ready
        CheckAllPlayersReady();
    }
    
    // [Server] attribute not needed
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
        if (uiManager != null)
        {
            // Pass the isHost flag to the RPC
            RpcSetStartGameButtonActive(allReady);
        }
    }
    
    // [Server] attribute not needed
    public void StartGame()
    {
        if (InstanceFinder.NetworkManager == null || !InstanceFinder.NetworkManager.IsServerStarted) return;

        // Add extra check: ensure all players are ready before starting
        if (!AreAllPlayersReady(true))
        {
            Debug.LogWarning("Attempted to start game, but not all players are ready.");
            // Optionally, provide feedback to the host
            return;
        }
        
        Debug.Log("Server starting game...");
        // Transition to game state
        RpcStartGame();
    }
    #endregion

    #region Client Methods
    public void JoinGame(string serverAddress) // Assume serverAddress for joining later
    {
        Debug.Log("JoinGame called");
        
        // Ensure we've tried to initialize
        if (!networkInitializationAttempted)
        {
            InitializeNetworkManager();
        }
        
        // Try InstanceFinder approach first
        if (InstanceFinder.ClientManager != null && !InstanceFinder.ClientManager.Started)
        {
            Debug.Log("Connecting to server via InstanceFinder.ClientManager");
            InstanceFinder.ClientManager.StartConnection();
            return;
        }
        
        // Fall back to direct reference
        if (networkManager != null && networkManager.ClientManager != null && !networkManager.ClientManager.Started)
        {
            Debug.Log("Connecting to server via direct ClientManager reference");
            networkManager.ClientManager.StartConnection();
        }
        else if (networkManager == null)
        {
            Debug.LogError("Cannot join game, NetworkManager is null!");
        }
        else if (networkManager.ClientManager == null)
        {
            Debug.LogError("Cannot join game, ClientManager is null!");
        }
        else if (networkManager.ClientManager.Started)
        {
            Debug.LogWarning("Client already connected or connection attempt in progress.");
        }
    }
    
    // REMOVED ReadyUp method - UIManager calls NetworkPlayer.ToggleReady now
    // public void ReadyUp(bool isReady)
    // {
    //     // Send command to server
    //     CmdSetPlayerReady(isReady);
    // }
    
    public void LeaveGame()
    {
        Debug.Log("LeaveGame called");
        
        bool serverStopped = false;
        bool clientStopped = false;
        
        // Try InstanceFinder first
        if (InstanceFinder.ServerManager != null && InstanceFinder.ServerManager.Started)
        {
            Debug.Log("Stopping server via InstanceFinder.ServerManager");
            InstanceFinder.ServerManager.StopConnection(true);
            serverStopped = true;
        }
        
        if (InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Started)
        {
            Debug.Log("Stopping client via InstanceFinder.ClientManager");
            InstanceFinder.ClientManager.StopConnection();
            clientStopped = true;
        }
        
        // If either operation didn't succeed, try with direct references
        if (!serverStopped && networkManager != null && networkManager.IsServerStarted && networkManager.ServerManager != null)
        {
            Debug.Log("Stopping server via direct ServerManager reference");
            networkManager.ServerManager.StopConnection(true);
        }
        
        if (!clientStopped && networkManager != null && networkManager.IsClientStarted && networkManager.ClientManager != null)
        {
            Debug.Log("Stopping client via direct ClientManager reference");
            networkManager.ClientManager.StopConnection();
        }
        
        if (!serverStopped && !clientStopped && networkManager == null)
        {
            Debug.LogWarning("NetworkManager was null in LeaveGame.");
        }
    }
    #endregion

    #region RPCs and Commands
    // These need to be in a NetworkBehaviour. Since GameManager is not one anymore,
    // these need to be moved, likely to NetworkPlayer or a dedicated NetworkHelper class.
    // For now, let's assume NetworkPlayer handles these.
    // We will modify NetworkPlayer later.

    // --- Methods needed by NetworkPlayer --- 
    public void RpcUpdatePlayerList(List<PlayerInfo> updatedPlayers)
    {
        // This logic now happens directly in the RPC on NetworkPlayer or via UIManager
        // Update the local player list - NO! Client RPCs don't run on the server.
        // players = new List<PlayerInfo>(updatedPlayers);
        
        // Update UI
        if (uiManager != null)
        {
            uiManager.UpdatePlayerList(updatedPlayers); // Pass the updated list directly
        }
        else
        {
             Debug.LogWarning("UIManager is null in RpcUpdatePlayerList");
        }
    }

    public void RpcSetStartGameButtonActive(bool active)
    {
        if (uiManager != null)
        {
            // Only the host should see the start game button
            uiManager.SetStartGameButtonActive(active && isHost);
        }
         else
        {
             Debug.LogWarning("UIManager is null in RpcSetStartGameButtonActive");
        }
    }

    public void RpcStartGame()
    {
        Debug.Log("RpcStartGame received. Setting state to Combat.");
        SetGameState(GameState.Combat);
    }
    
    // Placeholder - Needs to be called via ServerRpc from NetworkPlayer
    public void ServerSetPlayerReady(NetworkConnection conn, bool isReady)
    {
        SetPlayerReady(conn, isReady);
    }
    
    // Placeholder - Needs to be called via ServerRpc from NetworkPlayer
    public void ServerAddPlayer(NetworkConnection conn, string playerName)
    {
        AddPlayer(conn, playerName);
    }
    
    // --- Added Methods for UI Interaction --- 
    public void UpdatePlayerList()
    {
        if (uiManager != null)
        {
            // Send the current server-side list to all clients via RPC
            if (InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.IsServerStarted)
            {
                RpcUpdatePlayerList(players);
            }
            // uiManager.UpdatePlayerList(players);
        }
        else Debug.LogWarning("UIManager null in UpdatePlayerList");
    }

    public void UpdateLobbyControls()
    {
        if (uiManager != null)
        {
            // Update controls based on current state
            // uiManager.UpdateReadyButtonState(); // REMOVED: UIManager handles its own button state
            if (InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.IsServerStarted)
            {
                CheckAllPlayersReady(); // This will trigger the RPC to update start button
            }
            // uiManager.SetStartGameButtonActive(isHost && AreAllPlayersReady());
        }
        else Debug.LogWarning("UIManager null in UpdateLobbyControls");
    }

    // Added helper method to check readiness, optionally only checking server list
    public bool AreAllPlayersReady(bool serverOnly = false)
    {
        if (!serverOnly && (InstanceFinder.NetworkManager == null || !InstanceFinder.NetworkManager.IsServerStarted))
        {
            // Clients don't have the definitive list, rely on UI state?
            // This needs careful consideration. For now, only server checks definitively.
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

    #endregion

    private void OnEnable()
    {
        // Subscribe to FishNet's initialization event if InstanceFinder is available
        if (InstanceFinder.NetworkManager != null)
        {
            Debug.Log("InstanceFinder.NetworkManager already available in OnEnable");
            // Try to initialize right away if InstanceFinder is already set
            InitializeNetworkManager();
        }
        else
        {
            Debug.Log("InstanceFinder.NetworkManager not available yet in OnEnable");
            // We'll rely on our delayed initialization approaches
        }
    }

    // Add this method to check for NetworkManager component availability 
    private void Update()
    {
        // If we've already subscribed to network events, we don't need to check anymore
        if (networkEventsSubscribed)
            return;
        
        // Check every half second instead of every frame
        if (Time.frameCount % 30 != 0)
            return;
        
        // Check if InstanceFinder components have become available
        if (InstanceFinder.NetworkManager != null)
        {
            if (!networkInitializationAttempted)
            {
                Debug.Log("NetworkManager detected in Update, initializing network...");
                InitializeNetworkManager();
            }
            else if (InstanceFinder.ClientManager != null && InstanceFinder.ServerManager != null)
            {
                Debug.Log("Network components detected in Update, subscribing to events...");
                
                // Subscribe to events
                InstanceFinder.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
                InstanceFinder.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
                InstanceFinder.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
                
                networkEventsSubscribed = true;
                Debug.Log("Successfully subscribed to network events in Update");
            }
        }
    }

    private void SpawnPlayerSpawner()
    {
        Debug.Log("GameManager.SpawnPlayerSpawner called");
        
        try
        {
            // Find existing PlayerSpawner to avoid duplication
            PlayerSpawner existingSpawner = FindFirstObjectByType<PlayerSpawner>();
            if (existingSpawner != null)
            {
                Debug.Log($"Found existing PlayerSpawner: {existingSpawner.name}");
                
                // Check if already spawned on network
                if (existingSpawner.IsSpawned)
                {
                    Debug.Log("PlayerSpawner already exists and is spawned on the network");
                    return;
                }
                else if (existingSpawner.NetworkObject != null)
                {
                    Debug.Log("Found existing PlayerSpawner with NetworkObject, spawning it");
                    if (InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.IsServerStarted)
                    {
                        try
                        {
                            InstanceFinder.ServerManager.Spawn(existingSpawner.gameObject);
                            Debug.Log($"Successfully spawned existing PlayerSpawner on network");
                            return;
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"Error spawning existing PlayerSpawner: {ex.Message}");
                            // Continue to create a new one
                        }
                    }
                }
            }
            
            // Verify server is running before spawning
            if (InstanceFinder.NetworkManager == null || !InstanceFinder.NetworkManager.IsServerStarted)
            {
                Debug.LogWarning("Cannot spawn PlayerSpawner: NetworkManager not available or server not started");
                return;
            }
            
            // Create a dynamic PlayerSpawner
            Debug.Log("Creating a dynamic PlayerSpawner");
            GameObject spawnerObj = new GameObject("PlayerSpawner");
            PlayerSpawner spawner = spawnerObj.AddComponent<PlayerSpawner>();
            NetworkObject nob = spawnerObj.AddComponent<NetworkObject>();
            
            // Set the player prefab
            if (playerPrefab != null)
            {
                Debug.Log($"Setting playerPrefab on dynamic PlayerSpawner to: {playerPrefab.name}");
                spawner.playerPrefab = playerPrefab;
            }
            
            // Spawn it on the network
            try
            {
                InstanceFinder.ServerManager.Spawn(spawnerObj);
                Debug.Log("Dynamic PlayerSpawner created and spawned on the network");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error spawning dynamic PlayerSpawner: {ex.Message}\n{ex.StackTrace}");
                Destroy(spawnerObj);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Exception in SpawnPlayerSpawner: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private GameObject FindPlayerPrefab()
    {
        Debug.Log("Searching for player prefab...");
        
        // Try to find in Resources folder
        GameObject resourcePrefab = Resources.Load<GameObject>("PlayerPrefab");
        if (resourcePrefab != null)
        {
            Debug.Log($"Found player prefab in Resources: {resourcePrefab.name}");
            return resourcePrefab;
        }
        
        // Try to find player GameObject with NetworkObject component in the scene
        if (networkManager != null)
        {
            Debug.Log("Looking for Player prefabs in the scene");
            
            // Look for existing Player GameObjects in the scene
            Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
            if (players != null && players.Length > 0)
            {
                foreach (Player player in players)
                {
                    if (player != null && player.gameObject != null && player.GetComponent<NetworkObject>() != null)
                    {
                        Debug.Log($"Found player prefab from scene: {player.gameObject.name}");
                        return player.gameObject;
                    }
                }
            }
        }
        
        Debug.LogWarning("Could not find a player prefab in any location");
        return null;
    }

}

[System.Serializable]
public class PlayerInfo
{
    public int ConnectionId;
    public string PlayerName;
    public bool IsReady;
}

public enum GameState
{
    StartScreen,
    Lobby,
    Combat
}