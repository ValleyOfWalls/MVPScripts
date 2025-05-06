using UnityEngine;
using FishNet.Object;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat; // Assuming Tugboat is still the transport
using Steamworks;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing.Server;
using FishNet.Managing.Client;

public class SteamAndLobbyHandler : NetworkBehaviour
{
    public static SteamAndLobbyHandler Instance { get; private set; }

    private Tugboat transport; // This might need to be fetched from NetworkManager.Instance.TransportManager
    private CSteamID currentLobbyId;
    private bool isConnectedToSteam = false;

    [Header("Prefabs")]
    [SerializeField] public NetworkObject playerPrefab;
    [SerializeField] public NetworkObject petPrefab;

    [Header("Networked Entities (Server-side view)")]
    [SerializeField] public List<NetworkObject> networkPlayers = new List<NetworkObject>();
    [SerializeField] public List<NetworkObject> networkPets = new List<NetworkObject>();

    private PlayerSpawner playerSpawner;
    private LobbyManagerScript lobbyManagerScript;

    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;
    protected Callback<SteamNetConnectionStatusChangedCallback_t> connectionStatusChanged;
    
    private NetworkManager fishNetManager;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // It's important that NetworkManager is ready before this.
        // Consider moving some initialization to OnStartServer or ensure script execution order.
    }

    public override void OnStartServer() // Or OnStartNetwork, depending on when NM is reliably available
    {
        base.OnStartServer(); // Important for NetworkBehaviour
        Initialize();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient(); // Important for NetworkBehaviour
        if(IsServerStarted) return; // Avoid double init if host. Use IsServerStarted
        InitializeSteamOnly(); // Clients only need Steam for callbacks, not for starting server components
    }

    private void InitializeSteamOnly()
    {
        if (isConnectedToSteam) return;

        // IMPORTANT: For SteamAPI.Init() to succeed during development (especially with AppID 480 for Spacewar),
        // you typically need a "steam_appid.txt" file in the root of your Unity project 
        // (next to Assets, Library folders) containing only the AppID (e.g., "480").
        try
        {
            if (SteamAPI.Init())
            {
                isConnectedToSteam = true;
                Debug.Log("SteamAndLobbyHandler: SteamAPI initialized successfully (Client-side).");
                SetupSteamCallbacks();
            }
            else
            {
                Debug.LogError("SteamAndLobbyHandler: SteamAPI.Init() failed (Client-side). Ensure Steam client is running and App ID is configured.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("SteamAndLobbyHandler: Steamworks initialization error (Client-side): " + e.Message);
        }
    }


    private void Initialize()
    {
        fishNetManager = FishNet.InstanceFinder.NetworkManager;
        if (fishNetManager == null)
        {
            Debug.LogError("SteamAndLobbyHandler: FishNet NetworkManager not found! SteamAndLobbyHandler requires a NetworkManager in the scene.");
            enabled = false;
            return;
        }

        // Transport for Steam P2P - Tugboat is commonly used.
        // Ensure your NetworkManager is configured with Tugboat.
        transport = fishNetManager.TransportManager.GetTransport<Tugboat>();
        if (transport == null)
        {
            Debug.LogError("SteamAndLobbyHandler: Tugboat transport not found on NetworkManager. Please add and configure it for Steam P2P.");
            // Not disabling the whole script, as some parts might still work or be needed without P2P transport.
        }

        playerSpawner = new PlayerSpawner(this);
        lobbyManagerScript = FindFirstObjectByType<LobbyManagerScript>();


        // Initialize Steamworks only on server, or once per client
        if (!isConnectedToSteam)
        {
            // IMPORTANT: For SteamAPI.Init() to succeed during development (especially with AppID 480 for Spacewar),
            // you typically need a "steam_appid.txt" file in the root of your Unity project 
            // (next to Assets, Library folders) containing only the AppID (e.g., "480").
            try
            {
                if (SteamAPI.Init())
                {
                    isConnectedToSteam = true;
                    Debug.Log("SteamAndLobbyHandler: SteamAPI initialized successfully.");
                    SetupSteamCallbacks();
                }
                else
                {
                    Debug.LogError("SteamAndLobbyHandler: SteamAPI.Init() failed. Ensure Steam client is running and App ID is configured.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("SteamAndLobbyHandler: Steamworks initialization error: " + e.Message);
            }
        }
        
        // Subscribe to FishNet NetworkManager events
        // These events should only be subscribed to by the server.
        if (IsServerStarted) // Use IsServerStarted
        {
            fishNetManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
        }
        // Client connection state changes can be useful for all clients
        fishNetManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;

        Debug.Log("SteamAndLobbyHandler Initialized.");
    }
    
    private void SetupSteamCallbacks()
    {
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        // Only if you are using SteamNetworkingSockets directly, which Tugboat abstracts for P2P.
        // connectionStatusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);
    }

    private void OnDestroy()
    {
        if (isConnectedToSteam)
        {
            SteamAPI.Shutdown();
        }
        if (Instance == this) Instance = null;

        // Unsubscribe from FishNet NetworkManager events
        if (fishNetManager != null)
        {
            if (fishNetManager.ServerManager != null && IsServerStarted) // Use IsServerStarted
            {
                 fishNetManager.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState;
            }
            if (fishNetManager.ClientManager != null)
            {
                fishNetManager.ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
            }
        }
    }

    private void Update()
    {
        if (isConnectedToSteam)
        {
            SteamAPI.RunCallbacks();
        }
    }

    public void InitiateSteamConnectionAndLobby()
    {
        if (!isConnectedToSteam)
        {
             // Attempt to initialize Steam if not already (e.g. client clicking a button)
            InitializeSteamOnly();
            if (!isConnectedToSteam) {
                 Debug.LogError("SteamAndLobbyHandler: Steam not initialized! Cannot host lobby.");
                 return;
            }
        }
        Debug.Log("SteamAndLobbyHandler: Attempting to host a Steam lobby...");
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4); // Or k_ELobbyTypePublic
    }

    public void JoinSteamLobby(CSteamID lobbyId)
    {
        if (!isConnectedToSteam)
        {
            InitializeSteamOnly();
             if (!isConnectedToSteam) {
                Debug.LogError("SteamAndLobbyHandler: Steam not initialized! Cannot join lobby.");
                return;
            }
        }
        Debug.Log("SteamAndLobbyHandler: Attempting to join Steam lobby: " + lobbyId);
        SteamMatchmaking.JoinLobby(lobbyId);
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult == EResult.k_EResultOK)
        {
            currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
            Debug.Log("SteamAndLobbyHandler: Lobby created successfully! Lobby ID: " + currentLobbyId);
            SteamMatchmaking.SetLobbyData(currentLobbyId, "HostAddress", SteamUser.GetSteamID().ToString());
            SteamMatchmaking.SetLobbyData(currentLobbyId, "name", SteamFriends.GetPersonaName() + "'s Lobby");

            // Start FishNet host
            StartFishNetHostWithSteam();
        }
        else
        {
            Debug.LogError("SteamAndLobbyHandler: Failed to create lobby. Error: " + callback.m_eResult);
        }
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("SteamAndLobbyHandler: Game lobby join requested for lobby ID: " + callback.m_steamIDLobby);
        JoinSteamLobby(callback.m_steamIDLobby); // Calls our JoinSteamLobby method
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        Debug.Log("SteamAndLobbyHandler: Entered lobby! Lobby ID: " + currentLobbyId);
        
        if (fishNetManager == null) {
            Debug.LogError("SteamAndLobbyHandler: FishNetManager is null on LobbyEntered. Cannot start client.");
            return;
        }

        // If I am the host of this Steam lobby, I should have already started the FishNet server.
        if (SteamMatchmaking.GetLobbyOwner(currentLobbyId) == SteamUser.GetSteamID())
        {
            Debug.Log("SteamAndLobbyHandler: I am the owner of this lobby. Server should be running or starting.");
            if (!fishNetManager.IsServerStarted) {
                 StartFishNetHostWithSteam(); // Ensure host is started if it wasn't (e.g. joined own lobby via invite)
            } else if (!fishNetManager.IsClientStarted){
                 // If server is running but client part of host isn't, start it.
                 // This typically happens if StartHostWithSteam only started server.
                 // However, standard FishNet StartHost usually starts both.
                 Debug.Log("SteamAndLobbyHandler: Server is started. Ensuring client (host) is also started.");
                 fishNetManager.ClientManager.StartConnection();
            }
            return;
        }

        // If not the host, then try to connect as a client
        string hostSteamIdAddress = SteamMatchmaking.GetLobbyData(currentLobbyId, "HostAddress");
        if (!string.IsNullOrEmpty(hostSteamIdAddress))
        {
            Debug.Log("SteamAndLobbyHandler: Joining lobby as client. Host Steam ID Address: " + hostSteamIdAddress);
            StartFishNetClientWithSteam(hostSteamIdAddress);
        }
        else
        {
            Debug.LogError("SteamAndLobbyHandler: Could not get host address from lobby data.");
        }
    }

    private void StartFishNetHostWithSteam()
    {
        if (fishNetManager == null || transport == null)
        {
            Debug.LogError("SteamAndLobbyHandler: Cannot start FishNet host. NetworkManager or Tugboat transport is missing.");
            return;
        }
        // Tugboat uses SteamID as address. The port is less critical for P2P but should be set.
        transport.SetClientAddress(SteamUser.GetSteamID().ToString()); 
        transport.SetPort((ushort)Random.Range(7770, 7780)); // Example port
        
        fishNetManager.ServerManager.StartConnection(); // Start server
        fishNetManager.ClientManager.StartConnection(); // Start client (for host)
        Debug.Log("SteamAndLobbyHandler: FishNet Host started with SteamP2P via Tugboat.");
    }

    private void StartFishNetClientWithSteam(string hostSteamIdAddress)
    {
         if (fishNetManager == null || transport == null)
        {
            Debug.LogError("SteamAndLobbyHandler: Cannot start FishNet client. NetworkManager or Tugboat transport is missing.");
            return;
        }
        transport.SetClientAddress(hostSteamIdAddress);
        // Port should ideally match what host expects, but for Steam P2P via Tugboat, the SteamID is the primary address.
        // Tugboat may ignore the port when using Steam P2P if Steam handles the relay.
        transport.SetPort((ushort)Random.Range(7770, 7780)); // This might need to be the host's actual port if not purely P2P relying on SteamID alone.
                                                             // For Tugboat, this port is often just for the transport's internal state.
        
        fishNetManager.ClientManager.StartConnection();
        Debug.Log("SteamAndLobbyHandler: FishNet Client started, attempting to connect to host via SteamP2P: " + hostSteamIdAddress);
    }
    
    // FishNet Server Event
    private void ServerManager_OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            Debug.Log($"SteamAndLobbyHandler: Client {conn.ClientId} connected to server.");
            if (playerSpawner != null) 
            {
                // Spawning should only happen on the server.
                // The playerSpawner itself also checks steamAndLobbyHandlerInstance.IsServerStarted
                if (IsServerStarted) playerSpawner.SpawnPlayerForConnection(conn); // Use IsServerStarted
            } else {
                Debug.LogError("SteamAndLobbyHandler: PlayerSpawner is null. Cannot spawn player.");
            }
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            Debug.Log($"SteamAndLobbyHandler: Client {conn.ClientId} disconnected from server.");
            RemoveNetworkedEntitiesForConnection(conn);
            if (lobbyManagerScript != null)
            {
                lobbyManagerScript.HandlePlayerDisconnect(conn);
            }
        }
    }

    // FishNet Client Event
    private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            Debug.Log("SteamAndLobbyHandler: Client connected to server successfully.");
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            Debug.Log("SteamAndLobbyHandler: Client disconnected from server.");
            // Potentially return to main menu or show error.
            // If I was the host and my client part disconnected, I might want to shut down the server too.
            if (fishNetManager.IsServerStarted && SteamMatchmaking.GetLobbyOwner(currentLobbyId) == SteamUser.GetSteamID()) {
                Debug.Log("SteamAndLobbyHandler: Host-Client disconnected. Shutting down server and leaving Steam lobby.");
                fishNetManager.ServerManager.StopConnection(true);
                if(currentLobbyId.IsValid()) SteamMatchmaking.LeaveLobby(currentLobbyId);
                currentLobbyId = CSteamID.Nil;
            }
        }
    }

    public void RegisterNetworkPlayer(NetworkObject playerNOB)
    {
        if (!networkPlayers.Contains(playerNOB)) networkPlayers.Add(playerNOB);
    }

    public void UnregisterNetworkPlayer(NetworkObject playerNOB)
    {
        networkPlayers.Remove(playerNOB);
    }

    public void RegisterNetworkPet(NetworkObject petNOB)
    {
        if (!networkPets.Contains(petNOB)) networkPets.Add(petNOB);
    }

    public void UnregisterNetworkPet(NetworkObject petNOB)
    {
        networkPets.Remove(petNOB);
    }

    private void RemoveNetworkedEntitiesForConnection(NetworkConnection conn)
    {
        // FishNet usually handles despawning objects owned by a disconnected client.
        // This is for cleaning up our manual tracking lists.
        networkPlayers.RemoveAll(p => p.Owner == conn);
        networkPets.RemoveAll(p => p.Owner == conn); // Assuming pets are directly owned by connection for this list.
        Debug.Log($"SteamAndLobbyHandler: Removed entities from tracking lists for disconnected client {conn.ClientId}");
    }
} 