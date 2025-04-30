using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet;
using System.Collections;
using UnityEngine.SceneManagement;
using Combat; // If needed for other references

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private SteamManager steamManager;
    [SerializeField] private LobbyManager lobbyManager;

    [Header("Prefabs")]
    [SerializeField] public GameObject playerPrefab;

    // Game state
    private GameState currentState = GameState.StartScreen;
    public GameState CurrentGameState => currentState;
    
    private bool isHost = false;

    // Network initialization tracking
    private bool networkEventsSubscribed = false;
    private bool networkInitializationAttempted = false;

    // Add this property to hold the pet prefab
    public GameObject PetPrefab { get; private set; }

    [SerializeField] private string lobbySceneName = "LobbyScene";
    [SerializeField] private string combatSceneName = "CombatScene";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Find references if not set
        if (networkManager == null)
            networkManager = FindFirstObjectByType<NetworkManager>();

        if (uiManager == null)
            uiManager = FindFirstObjectByType<UIManager>();

        if (steamManager == null)
            steamManager = FindFirstObjectByType<SteamManager>();
            
        if (lobbyManager == null)
            lobbyManager = FindFirstObjectByType<LobbyManager>();
        
        // Initialize NetworkManager
        InitializeNetworkManager();
    }

    private void InitializeNetworkManager()
    {
        if (networkManager == null)
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError("Failed to find NetworkManager in scene");
                return;
            }
        }
        
        networkInitializationAttempted = true;
        
        if (!networkEventsSubscribed)
        {
            StartCoroutine(SubscribeToNetworkEvents());
        }
    }

    private System.Collections.IEnumerator SubscribeToNetworkEvents()
    {
        int attempts = 0;
        int maxAttempts = 30;
        
        while (attempts < maxAttempts)
        {
            Debug.Log($"Attempt {attempts+1}: Looking for NetworkManager components...");
            
            if (networkManager != null)
            {
                try 
                {
                    if (InstanceFinder.ClientManager != null && InstanceFinder.ServerManager != null)
                    {
                        // Subscribe to events using InstanceFinder
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
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Exception during network initialization (attempt {attempts+1}): {ex.Message}");
                }
            }
            
            yield return new WaitForSeconds(0.5f);
            attempts++;
        }
        
        Debug.LogError("Failed to initialize network managers after multiple attempts");
    }

    private void Start()
    {
        SetGameState(GameState.StartScreen);
        
        if (networkManager != null && 
            (networkManager.ClientManager == null || networkManager.ServerManager == null))
        {
            StartCoroutine(DelayedNetworkInitialization());
        }
    }

    private System.Collections.IEnumerator DelayedNetworkInitialization()
    {
        yield return new WaitForSeconds(1.0f);
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
        
        // Update UI based on state
        if (uiManager != null)
            uiManager.OnGameStateChanged(newState);
            
        // Handle state-specific logic
        switch (newState)
        {
            case GameState.Lobby:
                if (lobbyManager != null)
                    lobbyManager.InitializeLobby(isHost);
                break;
                
            case GameState.Combat:
                if (lobbyManager != null)
                    lobbyManager.CloseLobby();
                break;
        }
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
            }
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            Debug.Log("Disconnected from server");
            SetGameState(GameState.StartScreen);
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

            // Spawn the PlayerSpawner from prefab
            SpawnPlayerSpawner();
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            Debug.Log("Server stopped");
            isHost = false;
            SetGameState(GameState.StartScreen);
        }
    }

    private void ServerManager_OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            Debug.Log($"Client connected: {conn.ClientId}");
            // Player info is now added via LobbyPlayerInfo.CmdJoinLobby
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            Debug.Log($"Client disconnected: {conn.ClientId}");
            
            // Notify LobbyManager about disconnection
            if (lobbyManager != null && currentState == GameState.Lobby)
            {
                lobbyManager.RemovePlayer(conn.ClientId);
            }
        }
    }
    #endregion

    #region Host/Server Methods
    public void StartHost()
    {
        Debug.Log("StartHost called");
        
        if (!networkInitializationAttempted)
        {
            InitializeNetworkManager();
        }
        
        if (InstanceFinder.ServerManager != null)
        {
            InstanceFinder.ServerManager.StartConnection();
            
            if (InstanceFinder.ClientManager != null)
            {
                InstanceFinder.ClientManager.StartConnection();
            }
            return;
        }
        
        // Fall back to direct reference
        if (networkManager != null)
        {
            if (networkManager.ServerManager != null)
            {
                networkManager.ServerManager.StartConnection();
            }
            
            if (networkManager.ClientManager != null)
            {
                networkManager.ClientManager.StartConnection();
            }
        }
    }
    #endregion

    #region Client Methods
    public void JoinGame(string serverAddress)
    {
        Debug.Log("JoinGame called");
        
        if (!networkInitializationAttempted)
        {
            InitializeNetworkManager();
        }
        
        if (InstanceFinder.ClientManager != null && !InstanceFinder.ClientManager.Started)
        {
            InstanceFinder.ClientManager.StartConnection();
            return;
        }
        
        if (networkManager != null && networkManager.ClientManager != null && !networkManager.ClientManager.Started)
        {
            networkManager.ClientManager.StartConnection();
        }
    }
    
    public void LeaveGame()
    {
        Debug.Log("LeaveGame called");
        
        bool serverStopped = false;
        bool clientStopped = false;
        
        if (InstanceFinder.ServerManager != null && InstanceFinder.ServerManager.Started)
        {
            InstanceFinder.ServerManager.StopConnection(true);
            serverStopped = true;
        }
        
        if (InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Started)
        {
            InstanceFinder.ClientManager.StopConnection();
            clientStopped = true;
        }
        
        if (!serverStopped && networkManager != null && networkManager.IsServerStarted && networkManager.ServerManager != null)
        {
            networkManager.ServerManager.StopConnection(true);
        }
        
        if (!clientStopped && networkManager != null && networkManager.IsClientStarted && networkManager.ClientManager != null)
        {
            networkManager.ClientManager.StopConnection();
        }
    }
    #endregion

    private void Update()
    {
        if (networkEventsSubscribed)
            return;
        
        if (Time.frameCount % 30 != 0)
            return;
        
        if (InstanceFinder.NetworkManager != null)
        {
            if (!networkInitializationAttempted)
            {
                InitializeNetworkManager();
            }
            else if (InstanceFinder.ClientManager != null && InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
                InstanceFinder.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
                InstanceFinder.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
                
                networkEventsSubscribed = true;
            }
        }
    }

    private void SpawnPlayerSpawner()
    {
        Debug.Log("GameManager.SpawnPlayerSpawner looking for scene instance...");

        try
        {
            if (InstanceFinder.NetworkManager == null || !InstanceFinder.NetworkManager.IsServerStarted)
            {
                Debug.LogWarning("Cannot spawn PlayerSpawner: NetworkManager not available or server not started");
                return;
            }

            PlayerSpawner spawnerInstance = FindFirstObjectByType<PlayerSpawner>();

            if (spawnerInstance == null)
            {
                Debug.LogError("PlayerSpawner instance not found in the scene!");
                return;
            }

            NetworkObject nob = spawnerInstance.GetComponent<NetworkObject>();
            if (nob == null)
            {
                Debug.LogError("PlayerSpawner instance in scene is missing its NetworkObject component!");
                return;
            }

            if (playerPrefab != null)
            {
                 spawnerInstance.playerPrefab = playerPrefab;
            }

          if (!nob.IsSpawned)
            {
                InstanceFinder.ServerManager.Spawn(spawnerInstance.gameObject);
                Debug.Log("Scene PlayerSpawner instance spawned on the network");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Exception in SpawnPlayerSpawner: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // Add a method for the initializer script to call
    public void SetPetPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("Attempted to set a null PetPrefab in GameManager!");
            return;
        }
        PetPrefab = prefab;
        Debug.Log($"GameManager: PetPrefab set to {prefab.name}");
    }
}

public enum GameState
{
    StartScreen,
    Lobby,
    Combat
}