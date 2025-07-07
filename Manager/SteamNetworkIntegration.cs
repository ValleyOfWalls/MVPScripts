using UnityEngine;
using Steamworks;
using System.Collections.Generic;
using System;
using System.Collections;
using FishNet.Managing; // Ensured this is active
using FishNet.Transporting;
using FishNet.Connection; // Added this line
// using FishNet.Transporting; // Example for transport states

/// <summary>
/// Centralizes Steamworks integration with FishNet networking, handling lobbies, player connections, and Steam callbacks.
/// Attach to: A persistent GameObject in the scene, often alongside a PlayerSpawner component.
/// </summary>
public class SteamNetworkIntegration : MonoBehaviour
{
    // --- Singleton Pattern ---
    private static SteamNetworkIntegration s_instance;
    public static SteamNetworkIntegration Instance { 
        get { 
            if (s_instance == null)
            {
                Debug.LogError("SteamNetworkIntegration instance is null! Make sure there is a SteamNetworkIntegration object in your scene.");
            }
            return s_instance; 
        } 
    }

    // --- SteamManager Fields ---
    [Header("Steam Settings")]
    [SerializeField] private bool initializeSteamOnAwake = true;
    [SerializeField] private uint appId = 3527170; // Your actual Steam App ID

    private bool m_steamInitialized = false;
    public bool IsSteamInitialized { get { return m_steamInitialized; } }

    // --- SteamLobbyManager Fields ---
    [Header("Lobby Settings")]
    [SerializeField] private int maxPlayers = 4;
    private const string LOBBY_GAME_ID_KEY = "GameID";
    private const string LOBBY_GAME_ID_VALUE = "3DMVP_LOBBY_V1"; // Unique ID for your game
    private const string LOBBY_VERSION_KEY = "Version";
    // New lobby status constants
    private const string LOBBY_STATUS_KEY = "Status";
    private const string LOBBY_STATUS_OPEN = "Open";
    private const string LOBBY_STATUS_IN_PROGRESS = "InProgress";
    private const string LOBBY_STATUS_CLOSED = "Closed";

    // Delegates
    public delegate void LobbyCreatedDelegate(bool success, CSteamID lobbyId);
    public delegate void LobbyJoinedDelegate(bool success, CSteamID lobbyId, bool isSteamHost);
    public delegate void LobbiesListDelegate(List<CSteamID> lobbies);
    public delegate void PlayerListUpdatedDelegate(List<CSteamID> playerIds);
    public delegate void PlayerJoinedLobbyDelegate(CSteamID newPlayerId); // For PlayerSpawner
    public delegate void PlayerLeftLobbyDelegate(CSteamID newPlayerId); // For PlayerSpawner

    // New event for graceful disconnection
    public delegate void DisconnectedAndReturnedToStartDelegate();
    public event DisconnectedAndReturnedToStartDelegate OnDisconnectedAndReturnedToStart;

    // Callback handles
    private Callback<LobbyCreated_t> m_lobbyCreated;
    private Callback<LobbyEnter_t> m_lobbyEntered;
    private Callback<LobbyMatchList_t> m_lobbyList;
    private Callback<LobbyDataUpdate_t> m_lobbyDataUpdated;
    private Callback<PersonaStateChange_t> m_personaStateChange;
    private Callback<LobbyChatUpdate_t> m_lobbyChatUpdate;


    // Events
    public event LobbyCreatedDelegate OnLobbyCreatedEvent;
    public event LobbyJoinedDelegate OnLobbyJoinedEvent;
    public event LobbiesListDelegate OnLobbiesListedEvent;
    public event PlayerListUpdatedDelegate OnPlayerListUpdatedEvent;
    public event PlayerJoinedLobbyDelegate OnPlayerJoinedLobbyEvent; // For PlayerSpawner
    public event PlayerLeftLobbyDelegate OnPlayerLeftLobbyEvent; // For PlayerSpawner

    // State
    private CSteamID m_currentLobbyId;
    private bool m_isSteamHost = false;
    private List<CSteamID> m_currentLobbyMembers = new List<CSteamID>();
    private List<CSteamID> m_availableLobbies = new List<CSteamID>();
    private CSteamID m_lobbyIdJustCreated = CSteamID.Nil;

    public bool IsInLobby { get { return m_currentLobbyId.IsValid(); } }
    public bool IsUserSteamHost { get { return m_isSteamHost; } }
    public CSteamID CurrentLobbyID { get { return m_currentLobbyId; } }


    // --- Network Integration Fields ---
    [Header("Network Prefabs")]
    [SerializeField] public GameObject NetworkEntityPlayerPrefab; // Assign in Inspector
    [SerializeField] public GameObject NetworkEntityPetPrefab;    // Assign in Inspector
    [SerializeField] public GameObject NetworkEntityPlayerHandPrefab; // Assign in Inspector
    [SerializeField] public GameObject NetworkEntityPetHandPrefab;    // Assign in Inspector
    [SerializeField] public GameObject NetworkEntityPlayerStatsUIPrefab; // Assign in Inspector
    [SerializeField] public GameObject NetworkEntityPetStatsUIPrefab;    // Assign in Inspector

    private PlayerSpawner playerSpawner; // Reference to component rather than instance
    private NetworkManager fishNetManager; // To cache NetworkManager instance
    
    // Disconnect handling
    private bool m_intentionalDisconnect = false;

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }
        s_instance = this;
        DontDestroyOnLoad(gameObject);

        if (initializeSteamOnAwake)
        {
            InitializeSteam();
        }
    }

    private void Start()
    {
        if (!m_steamInitialized)
        {
            Debug.LogWarning("Steam is not initialized. Steam Network Integration will not function fully.");
            // Optionally, attempt to initialize again or disable functionality
            // return; 
        }
        
        // Initialize Steam Callbacks for Lobby
        m_lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreatedCallback);
        m_lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEnteredCallback);
        m_lobbyList = Callback<LobbyMatchList_t>.Create(OnLobbyListCallback);
        m_lobbyDataUpdated = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdatedCallback);
        m_personaStateChange = Callback<PersonaStateChange_t>.Create(OnPersonaStateChangeCallback);
        m_lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdateCallback);

        fishNetManager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
        if (fishNetManager == null)
        {
            Debug.LogError("SteamNetworkIntegration: NetworkManager not found in scene! Player spawning will fail.");
            return;
        }

        // Get the PlayerSpawner component instead of creating a new instance
        playerSpawner = GetComponent<PlayerSpawner>();
        if (playerSpawner == null)
        {
            Debug.LogError("SteamNetworkIntegration: PlayerSpawner component not found on the same GameObject.");
        }

        // Subscribe to FishNet events
        fishNetManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
        fishNetManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
    }

    private void OnDestroy() // Or OnDisable
    {
        if (fishNetManager != null)
        {
            fishNetManager.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState;
            fishNetManager.ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
        }
        // Steam Callbacks are automatically unregistered by Steamworks.NET if this GameObject is destroyed.
    }

    private void Update()
    {
        if (m_steamInitialized)
        {
            SteamAPI.RunCallbacks();
        }
    }

    private void OnApplicationQuit()
    {
        LeaveLobby(); // Ensure we leave the lobby if we're in one
        if (m_steamInitialized)
        {
            Debug.Log("Shutting down Steam API...");
            SteamAPI.Shutdown();
            m_steamInitialized = false;
        }
    }

    #region Steam Initialization and Basic Info
    public bool InitializeSteam()
    {
        if (m_steamInitialized)
            return true;

        try
        {
            if (SteamAPI.RestartAppIfNecessary(new AppId_t(appId)))
            {
                Debug.Log("Restarting app via Steam...");
                Application.Quit();
                return false; // Important: return false as the app will quit
            }

            m_steamInitialized = SteamAPI.Init();
            if (!m_steamInitialized)
            {
                Debug.LogWarning("SteamAPI_Init failed. Steam is not running or you don't own the app.");
                return false;
            }
        }
        catch (DllNotFoundException e)
        {
            Debug.LogError($"Steam DLL not found: {e.Message}");
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"Steam initialization error: {e.Message}");
            return false;
        }

        /* Debug.Log("Steam Initialized Successfully!"); */
        return true;
    }

    public ulong GetSteamID()
    {
        if (!m_steamInitialized) return 0;
        return SteamUser.GetSteamID().m_SteamID;
    }

    public string GetPlayerName()
    {
        if (!m_steamInitialized) return "Player";
        return SteamFriends.GetPersonaName();
    }

    public string GetFriendName(CSteamID steamID)
    {
        if (!m_steamInitialized) return "Friend";
        return SteamFriends.GetFriendPersonaName(steamID);
    }
    #endregion

    #region Lobby Management
    public void CreateLobby(ELobbyType lobbyType = ELobbyType.k_ELobbyTypePublic)
    {
        if (!m_steamInitialized)
        {
            Debug.LogError("Steam not initialized. Cannot create lobby.");
            OnLobbyCreatedEvent?.Invoke(false, CSteamID.Nil);
            return;
        }
        SteamMatchmaking.CreateLobby(lobbyType, maxPlayers);
    }

    public void JoinLobby(CSteamID lobbyId)
    {
        if (!m_steamInitialized)
        {
            Debug.LogError("Steam not initialized. Cannot join lobby.");
            OnLobbyJoinedEvent?.Invoke(false, CSteamID.Nil, false);
            return;
        }
        SteamMatchmaking.JoinLobby(lobbyId);
    }

    public void LeaveLobby()
    {
        if (!m_steamInitialized || !m_currentLobbyId.IsValid())
        {
            Debug.Log("SteamNetworkIntegration: LeaveLobby called but no active lobby to leave");
            return;
        }

        Debug.Log($"SteamNetworkIntegration: Leaving Steam lobby: {m_currentLobbyId}");
        SteamMatchmaking.LeaveLobby(m_currentLobbyId);
        
        // Notify PlayerSpawner for local player leaving
        // OnPlayerLeftLobbyEvent?.Invoke(SteamUser.GetSteamID()); // Or handle through FishNet disconnect

        m_currentLobbyId = CSteamID.Nil;
        m_isSteamHost = false;
        m_currentLobbyMembers.Clear();
        OnPlayerListUpdatedEvent?.Invoke(new List<CSteamID>(m_currentLobbyMembers)); // Send empty list

        if (fishNetManager != null)
        {
            if (fishNetManager.IsClientStarted)
            {
                Debug.Log("SteamNetworkIntegration: Stopping FishNet client connection");
                fishNetManager.ClientManager.StopConnection();
            }
            if (fishNetManager.IsServerStarted)
            {
                Debug.Log("SteamNetworkIntegration: Stopping FishNet server connection");
                fishNetManager.ServerManager.StopConnection(true);
            }
        }
        else
        {
            Debug.LogWarning("SteamNetworkIntegration: FishNetManager is null, cannot stop network connections");
        }
        
        Debug.Log("SteamNetworkIntegration: Successfully left lobby and stopped network connections");
    }

    public void RequestLobbiesList()
    {
        if (!m_steamInitialized)
        {
            Debug.LogError("Steam not initialized. Cannot request lobbies list.");
            OnLobbiesListedEvent?.Invoke(new List<CSteamID>());
            return;
        }

        SteamMatchmaking.AddRequestLobbyListStringFilter(LOBBY_GAME_ID_KEY, LOBBY_GAME_ID_VALUE, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListStringFilter(LOBBY_VERSION_KEY, Application.version, ELobbyComparison.k_ELobbyComparisonEqual);
        // Only find open lobbies that are available for joining
        SteamMatchmaking.AddRequestLobbyListStringFilter(LOBBY_STATUS_KEY, LOBBY_STATUS_OPEN, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(50); // Limit results
        SteamMatchmaking.RequestLobbyList();
    }

    private void RefreshPlayerList()
    {
        if (!m_steamInitialized || !m_currentLobbyId.IsValid())
            return;

        List<CSteamID> previousMembers = new List<CSteamID>(m_currentLobbyMembers);
        m_currentLobbyMembers.Clear();
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(m_currentLobbyId);

        for (int i = 0; i < memberCount; i++)
        {
            CSteamID playerId = SteamMatchmaking.GetLobbyMemberByIndex(m_currentLobbyId, i);
            m_currentLobbyMembers.Add(playerId);

            // Notify if a new player joined (compared to previous list)
            if (!previousMembers.Contains(playerId) && playerId != SteamUser.GetSteamID())
            {
                /* Debug.Log($"New player detected in RefreshPlayerList: {GetFriendName(playerId)} ({playerId})"); */
                OnPlayerJoinedLobbyEvent?.Invoke(playerId);
                // if (playerSpawner != null && NetworkEntityPlayerPrefab != null)
                // {
                //    playerSpawner.SpawnPlayer(playerId, GetFriendName(playerId), NetworkEntityPlayerPrefab, NetworkEntityPetPrefab);
                // }
            }
        }
        
        // Check for players who left
        foreach (var oldMember in previousMembers)
        {
            if (!m_currentLobbyMembers.Contains(oldMember))
            {
                 /* Debug.Log($"Player left detected in RefreshPlayerList: {GetFriendName(oldMember)} ({oldMember})"); */
                 OnPlayerLeftLobbyEvent?.Invoke(oldMember);
                // if (playerSpawner != null)
                // {
                // playerSpawner.DespawnPlayer(oldMember);
                // }
            }
        }


        OnPlayerListUpdatedEvent?.Invoke(new List<CSteamID>(m_currentLobbyMembers));
    }

    public List<CSteamID> GetPlayerList()
    {
        return new List<CSteamID>(m_currentLobbyMembers);
    }

    public CSteamID GetLobbyOwner()
    {
        if (!m_currentLobbyId.IsValid()) return CSteamID.Nil;
        return SteamMatchmaking.GetLobbyOwner(m_currentLobbyId);
    }

    public bool IsCurrentPlayerLobbyOwner()
    {
        if (!m_currentLobbyId.IsValid() || !m_steamInitialized) return false;
        return SteamUser.GetSteamID() == SteamMatchmaking.GetLobbyOwner(m_currentLobbyId);
    }
    
    public List<CSteamID> GetAvailableLobbies()
    {
        return new List<CSteamID>(m_availableLobbies);
    }

    public void SetLobbyData(string key, string value)
    {
        if (IsInLobby && IsUserSteamHost)
        {
            SteamMatchmaking.SetLobbyData(m_currentLobbyId, key, value);
        }
    }
    
    /// <summary>
    /// Marks the current lobby as in-progress (unavailable for new players to join)
    /// Call this when transitioning from character selection to combat
    /// </summary>
    public void MarkLobbyAsInProgress()
    {
        if (IsInLobby && IsUserSteamHost)
        {
            SteamMatchmaking.SetLobbyData(m_currentLobbyId, LOBBY_STATUS_KEY, LOBBY_STATUS_IN_PROGRESS);
            Debug.Log("SteamNetworkIntegration: Lobby marked as in-progress - no longer discoverable by new players");
        }
        else
        {
            Debug.LogWarning("SteamNetworkIntegration: Cannot mark lobby as in-progress - not lobby host or not in lobby");
        }
    }
    
    /// <summary>
    /// Marks the current lobby as closed (completely unavailable)
    /// Call this when the game ends or lobby should be permanently closed
    /// </summary>
    public void MarkLobbyAsClosed()
    {
        if (IsInLobby && IsUserSteamHost)
        {
            SteamMatchmaking.SetLobbyData(m_currentLobbyId, LOBBY_STATUS_KEY, LOBBY_STATUS_CLOSED);
            Debug.Log("SteamNetworkIntegration: Lobby marked as closed - completely unavailable");
        }
        else
        {
            Debug.LogWarning("SteamNetworkIntegration: Cannot mark lobby as closed - not lobby host or not in lobby");
        }
    }
    
    /// <summary>
    /// Marks the current lobby as open (available for new players to join)
    /// Call this when returning to character selection or starting a new game
    /// </summary>
    public void MarkLobbyAsOpen()
    {
        if (IsInLobby && IsUserSteamHost)
        {
            SteamMatchmaking.SetLobbyData(m_currentLobbyId, LOBBY_STATUS_KEY, LOBBY_STATUS_OPEN);
            Debug.Log("SteamNetworkIntegration: Lobby marked as open - available for new players");
        }
        else
        {
            Debug.LogWarning("SteamNetworkIntegration: Cannot mark lobby as open - not lobby host or not in lobby");
        }
    }
    
    /// <summary>
    /// Gets the current lobby status
    /// </summary>
    public string GetCurrentLobbyStatus()
    {
        if (IsInLobby)
        {
            return GetLobbyData(m_currentLobbyId, LOBBY_STATUS_KEY);
        }
        return "None";
    }

    public string GetLobbyData(CSteamID lobbyId, string key)
    {
        return SteamMatchmaking.GetLobbyData(lobbyId, key);
    }

    #endregion

    #region Steam Callbacks
    private void OnLobbyCreatedCallback(LobbyCreated_t param)
    {
        if (param.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"Failed to create lobby: {param.m_eResult}");
            OnLobbyCreatedEvent?.Invoke(false, CSteamID.Nil);
            return;
        }

        m_currentLobbyId = new CSteamID(param.m_ulSteamIDLobby);
        m_lobbyIdJustCreated = m_currentLobbyId;
        m_isSteamHost = true;
        /* Debug.Log($"Lobby created successfully: {m_currentLobbyId}. This client IS THE STEAM HOST."); */

        SteamMatchmaking.SetLobbyData(m_currentLobbyId, LOBBY_GAME_ID_KEY, LOBBY_GAME_ID_VALUE);
        SteamMatchmaking.SetLobbyData(m_currentLobbyId, LOBBY_VERSION_KEY, Application.version);
        SteamMatchmaking.SetLobbyData(m_currentLobbyId, "HostName", GetPlayerName());
        // Mark new lobby as open for joining
        SteamMatchmaking.SetLobbyData(m_currentLobbyId, LOBBY_STATUS_KEY, LOBBY_STATUS_OPEN);

        OnLobbyCreatedEvent?.Invoke(true, m_currentLobbyId);
        RefreshPlayerList();

        if (fishNetManager != null)
        {
            fishNetManager.ServerManager.StartConnection();
            fishNetManager.ClientManager.StartConnection(); 
            /* Debug.Log("FishNet Host Started (Server and Client for host)."); */
        }
        else
        {
            Debug.LogError("Cannot start FishNet Host: NetworkManager instance not found!");
        }
    }

    private void OnLobbyEnteredCallback(LobbyEnter_t param)
    {
        CSteamID enteredLobbyId = new CSteamID(param.m_ulSteamIDLobby);
        /* Debug.Log($"OnLobbyEnteredCallback received for lobby {enteredLobbyId}. Response code: {param.m_EChatRoomEnterResponse}"); */

        if (param.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Debug.LogError($"Failed to join lobby: {(EChatRoomEnterResponse)param.m_EChatRoomEnterResponse}");
            m_currentLobbyId = CSteamID.Nil; 
            OnLobbyJoinedEvent?.Invoke(false, CSteamID.Nil, false);
            m_lobbyIdJustCreated = CSteamID.Nil;
            return;
        }

        m_currentLobbyId = enteredLobbyId;
        bool shouldConnectAsFishNetClient = false;

        if (m_lobbyIdJustCreated.IsValid() && m_lobbyIdJustCreated == enteredLobbyId)
        {
            m_isSteamHost = true;
            shouldConnectAsFishNetClient = false;
            /* Debug.Log($"Entered own created lobby ({enteredLobbyId}). Confirmed Steam Host. FishNet client connection handled by creator path."); */
        }
        else
        {
            m_isSteamHost = IsCurrentPlayerLobbyOwner();
            shouldConnectAsFishNetClient = true;
            /* Debug.Log($"Joining existing lobby ({enteredLobbyId}). Current Steam Owner status: {m_isSteamHost}. Will attempt to connect as FishNet client."); */
        }
        m_lobbyIdJustCreated = CSteamID.Nil;
        
        OnLobbyJoinedEvent?.Invoke(true, m_currentLobbyId, m_isSteamHost);
        RefreshPlayerList(); 

        if (shouldConnectAsFishNetClient)
        {
            if (fishNetManager != null)
            {
                Debug.LogWarning("FishNet Client StartConnection: Attempting to connect to localhost for testing. Host address retrieval from lobby data is recommended for production.");
                fishNetManager.ClientManager.StartConnection();
            }
            else
            {
                Debug.LogError("Cannot start FishNet Client: NetworkManager instance not found!");
            }
        }
    }

    private void OnLobbyListCallback(LobbyMatchList_t param)
    {
        m_availableLobbies.Clear();
        Debug.Log($"SteamNetworkIntegration: Found {param.m_nLobbiesMatching} open lobbies matching criteria");

        for (int i = 0; i < param.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
            m_availableLobbies.Add(lobbyId);
            
            string hostName = GetLobbyData(lobbyId, "HostName");
            string status = GetLobbyData(lobbyId, LOBBY_STATUS_KEY);
            Debug.Log($"SteamNetworkIntegration: Available lobby {i}: Host={hostName}, Status={status}, ID={lobbyId}");
        }
        OnLobbiesListedEvent?.Invoke(new List<CSteamID>(m_availableLobbies));

        // If lobbies were found, join the first one. Otherwise, create a new one.
        if (m_availableLobbies.Count > 0)
        {
            Debug.Log($"SteamNetworkIntegration: Found {m_availableLobbies.Count} open lobbies. Auto-joining the first one: {m_availableLobbies[0]}");
            JoinLobby(m_availableLobbies[0]); 
        }
        else
        {
            Debug.Log("SteamNetworkIntegration: No open lobbies found. Creating a new lobby automatically.");
            CreateLobby(); // Defaulting to public lobby
        }
        
        // After lobby operations, we'll transition directly to character selection
        // This will be handled by the lobby connection callbacks
    }

    private void OnLobbyDataUpdatedCallback(LobbyDataUpdate_t param)
    {
        //This callback is triggered when lobby metadata is updated.
        //It's also triggered when a user joins or leaves the lobby.
        //param.m_ulSteamIDMember is the user who caused the update (joined, left, or changed data).
        //param.m_ulSteamIDLobby is the lobby ID.
        //param.m_bSuccess is 1 if the update is for a lobby we are in.

        if (param.m_ulSteamIDLobby == m_currentLobbyId.m_SteamID)
        {
            Debug.Log($"Lobby data updated for current lobby. Member: {param.m_ulSteamIDMember}, Success: {param.m_bSuccess}");
            RefreshPlayerList(); // Refresh list as player data or lobby data might have changed
            
            // Optional: Trigger a more specific event if needed
            // OnSpecificLobbyDataUpdatedEvent?.Invoke(param);
        }
    }
    
    private void OnPersonaStateChangeCallback(PersonaStateChange_t param)
    {
        // Triggered when a friend's persona state (name, avatar, etc.) changes.
        // Useful for updating UI if displaying player names from Steam.
        if (m_currentLobbyId.IsValid() && m_currentLobbyMembers.Contains(new CSteamID(param.m_ulSteamID)))
        {
            Debug.Log($"Persona state change for user {param.m_ulSteamID} in current lobby. Flags: {param.m_nChangeFlags}");
            OnPlayerListUpdatedEvent?.Invoke(new List<CSteamID>(m_currentLobbyMembers)); // Re-send player list to update UI
        }
    }

    private void OnLobbyChatUpdateCallback(LobbyChatUpdate_t param)
    {
        // This callback is triggered when a user joins or leaves the lobby, or is Kicked/Banned.
        // param.m_ulSteamIDLobby: The Steam ID of the lobby.
        // param.m_ulSteamIDUserChanged: The Steam ID of the user whose status changed.
        // param.m_ulMakingChange: The Steam ID of the user who made the change (e.g., kicked someone). For join/leave, this is the same as m_ulSteamIDUserChanged.
        // param.m_rgfChatMemberStateChange: A flag indicating the type of change (joined, left, disconnected, kicked, banned).
        
        if (param.m_ulSteamIDLobby != m_currentLobbyId.m_SteamID)
            return; // Not our current lobby

        CSteamID userChangedID = new CSteamID(param.m_ulSteamIDUserChanged);
        EChatMemberStateChange stateChange = (EChatMemberStateChange)param.m_rgfChatMemberStateChange;

        /* Debug.Log($"LobbyChatUpdate: User {GetFriendName(userChangedID)} ({userChangedID}) changed state: {stateChange}"); */

        switch (stateChange)
        {
            case EChatMemberStateChange.k_EChatMemberStateChangeEntered:
                /* Debug.Log($"User {GetFriendName(userChangedID)} entered the lobby."); */
                if (!m_currentLobbyMembers.Contains(userChangedID)) // Should usually be new
                {
                    // m_currentLobbyMembers.Add(userChangedID); // RefreshPlayerList will handle this
                    OnPlayerJoinedLobbyEvent?.Invoke(userChangedID);
                    // if (playerSpawner != null && NetworkEntityPlayerPrefab != null && userChangedID != SteamUser.GetSteamID()) // Don't spawn local player again here
                    // {
                    //    playerSpawner.SpawnPlayer(userChangedID, GetFriendName(userChangedID), NetworkEntityPlayerPrefab, NetworkEntityPetPrefab);
                    // }
                }
                break;
            case EChatMemberStateChange.k_EChatMemberStateChangeLeft:
            case EChatMemberStateChange.k_EChatMemberStateChangeDisconnected:
            case EChatMemberStateChange.k_EChatMemberStateChangeKicked:
            case EChatMemberStateChange.k_EChatMemberStateChangeBanned:
                /* Debug.Log($"User {GetFriendName(userChangedID)} left, disconnected, was kicked or banned from the lobby."); */
                if (m_currentLobbyMembers.Contains(userChangedID))
                {
                    // m_currentLobbyMembers.Remove(userChangedID); // RefreshPlayerList will handle this
                    OnPlayerLeftLobbyEvent?.Invoke(userChangedID);
                    // if (playerSpawner != null)
                    // {
                    // playerSpawner.DespawnPlayer(userChangedID);
                    // }
                }
                // If the host left and we are now the host (host migration)
                if (userChangedID == GetLobbyOwner() && stateChange != EChatMemberStateChange.k_EChatMemberStateChangeEntered) // Check if owner left
                {
                    // Steam automatically handles host migration if SetLobbyOwner is used by new host.
                    // We just need to update our internal m_isSteamHost state.
                    m_isSteamHost = IsCurrentPlayerLobbyOwner();
                    /* Debug.Log($"Lobby owner may have changed. Current client is host: {m_isSteamHost}"); */
                }
                break;
        }
        RefreshPlayerList(); // Always refresh the list to ensure consistency
    }

    #endregion

    #region FishNet Integration (Placeholders/Examples)

    // Example method to be called when FishNet client connects
    public void OnClientConnected()
    {
        Debug.Log("FishNet Client Connected to Server.");
        // Potentially send a message to the server with SteamID for authentication/linking
        // Or, if PlayerSpawner handles it on server-side instantiation, this might not be needed here.
    }

    // Example method to be called when FishNet client disconnects
    public void OnClientDisconnected()
    {
        Debug.Log("FishNet Client Disconnected from Server.");
        // If disconnected from server, good idea to also leave the Steam lobby
        if (IsInLobby)
        {
            LeaveLobby();
        }
    }
    
    // Example method to be called when FishNet server starts
    public void OnServerStarted()
    {
        Debug.Log("FishNet Server Started.");
    }

    // Example method to be called when a client connects to our server
    public void OnRemoteClientConnected(int connectionId) // FishNet often uses connectionId
    {
        /* Debug.Log($"FishNet: Remote client connected with connectionId {connectionId}."); */
        // Here, you'd need a way to map connectionId to SteamID.
        // This usually involves the client sending its SteamID to the server upon connection.
        // Once you have the SteamID, you can call:
        // OnPlayerJoinedLobbyEvent?.Invoke(steamIdOfJoiningPlayer);
        // if (playerSpawner != null && NetworkEntityPlayerPrefab != null)
        // {
        //    playerSpawner.SpawnPlayer(steamIdOfJoiningPlayer, "PlayerNameFromServer", NetworkEntityPlayerPrefab, NetworkEntityPetPrefab);
        // }
    }
    
    // Example method to be called when a client disconnects from our server
    public void OnRemoteClientDisconnected(int connectionId)
    {
        /* Debug.Log($"FishNet: Remote client disconnected with connectionId {connectionId}."); */
        // Map connectionId to SteamID and then:
        // OnPlayerLeftLobbyEvent?.Invoke(steamIdOfLeavingPlayer);
        // if (playerSpawner != null)
        // {
        // playerSpawner.DespawnPlayer(steamIdOfLeavingPlayer);
        // }
    }

    #endregion

    #region FishNet Event Handlers
    private void ServerManager_OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            // If the connecting client has ClientId -1, it's the host's server-side connection.
            // We want to skip spawning for this connection to avoid duplicates.
            if (conn.ClientId == -1) 
            {
                Debug.Log($"ServerManager_OnRemoteConnectionState: ClientId {conn.ClientId} connected. This is the server-side connection. Skipping player spawn.");
                return;
            }

            Debug.Log($"Remote client {conn.ClientId} connected. Spawning player.");
            if (playerSpawner != null)
            {
                playerSpawner.SpawnPlayerForConnection(conn);
            }
            else
            {
                Debug.LogError("PlayerSpawner component is null when remote client connected.");
            }
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {   
            // Ensure we don't try to despawn for server connection if it was never fully processed as a separate player
            if (conn.ClientId != -1)
            {
                /* Debug.Log($"Remote client {conn.ClientId} disconnected."); */
                // if (playerSpawner != null) playerSpawner.DespawnEntitiesForConnection(conn);
            }
            else
            {
                /* Debug.Log($"Host's server-side connection (ClientId {conn.ClientId}) disconnected/stopped."); */
            }
        }
    }

    private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            /* Debug.Log($"Local client (ClientId: {fishNetManager.ClientManager.Connection.ClientId}) connected to server."); */
            // This instance is the host if ServerManager is started and it's the Steam Host.
            // We want to spawn for ClientId 0 (client connection) but not for server connection
            if (fishNetManager.ServerManager.Started && IsUserSteamHost && fishNetManager.ClientManager.Connection.ClientId == 0) 
            {
                Debug.Log("Host's client connection (ClientId 0) connected. Spawning player for client component.");
                if (playerSpawner != null)
                {
                    playerSpawner.SpawnPlayerForConnection(fishNetManager.ClientManager.Connection);
                }
                else
                {
                    Debug.LogError("PlayerSpawner component is null when host's client connected.");
                }
            }
            // Non-host clients that connect will be handled by ServerManager_OnRemoteConnectionState on the server.
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            // Client disconnection handling
            Debug.Log($"SteamNetworkIntegration: Local client disconnected (Reason: {args.ConnectionState})");
            
            // Check if we're in a lobby and this wasn't an intentional disconnect
            if (IsInLobby && !m_intentionalDisconnect)
            {
                Debug.Log("SteamNetworkIntegration: Unexpected disconnection detected (likely host left), initiating graceful return to start screen");
                
                // Clear lobby state since we're no longer connected
                m_currentLobbyId = CSteamID.Nil;
                m_isSteamHost = false;
                m_currentLobbyMembers.Clear();
                
                // Trigger graceful return to start screen
                StartCoroutine(HandleUnexpectedDisconnectReturnToStart());
            }
            else if (m_intentionalDisconnect)
            {
                Debug.Log("SteamNetworkIntegration: Intentional disconnect detected, cleanup already handled");
                m_intentionalDisconnect = false; // Reset flag
            }
            else
            {
                Debug.Log("SteamNetworkIntegration: Client disconnected but not in lobby, no special handling needed");
            }
        }
    }
    #endregion

    #region Graceful Disconnect and Return to Start
    
    /// <summary>
    /// Gracefully disconnects from the current lobby and network session, then returns to start screen.
    /// This method handles the complete cleanup process for leaving a game session.
    /// </summary>
    public void DisconnectAndReturnToStart()
    {
        Debug.Log("SteamNetworkIntegration: Starting graceful disconnect and return to start screen");
        
        // Mark this as an intentional disconnect to prevent unexpected disconnect handling
        m_intentionalDisconnect = true;
        
        // Mark lobby as closed before leaving (if we're the host)
        MarkLobbyAsClosed();
        
        // FIRST: Reset to start phase while still connected (since GamePhaseManager is a NetworkObject)
        GamePhaseManager gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
        if (gamePhaseManager != null)
        {
            Debug.Log("SteamNetworkIntegration: Resetting to start phase before disconnecting");
            gamePhaseManager.SetStartPhase();
        }
        else
        {
            Debug.LogError("SteamNetworkIntegration: GamePhaseManager not found, cannot reset phase before disconnect");
        }
        
        // SECOND: Clean up all selection NetworkObjects before disconnecting to prevent NetworkTransform errors
        CleanupSelectionNetworkObjects();
        
        // THIRD: Leave the Steam lobby and disconnect (this also handles FishNet disconnection)
        LeaveLobby();
        
        // Give a brief moment for network cleanup to complete
        StartCoroutine(HandleReturnToStartAfterDisconnect());
    }
    
    /// <summary>
    /// Handles unexpected disconnections (like when host leaves) and gracefully returns to start screen
    /// </summary>
    private IEnumerator HandleUnexpectedDisconnectReturnToStart()
    {
        Debug.Log("SteamNetworkIntegration: Handling unexpected disconnect - restarting application for clean state");
        
        // Wait a brief moment for any ongoing operations to complete
        yield return new WaitForSeconds(1.0f);
        
        // Clear lobby state
        m_currentLobbyId = CSteamID.Nil;
        m_isSteamHost = false;
        m_currentLobbyMembers.Clear();
        
        // Simple and clean solution: restart the application
        // This ensures all NetworkObjects are properly reset and we start in a clean state
        Debug.Log("SteamNetworkIntegration: Restarting application due to unexpected disconnect");
        
        #if UNITY_EDITOR
            // In editor, stop play mode
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            // In build, restart the application
            Application.Quit();
            
            // For platforms that support it, you could also use:
            // System.Diagnostics.Process.Start(Application.dataPath.Replace("_Data", ".exe"));
        #endif
    }
    
    /// <summary>
    /// Cleans up character selection entities to prevent conflicts during phase transitions
    /// </summary>
    private void CleanupCharacterSelectionEntities()
    {
        Debug.Log("SteamNetworkIntegration: Cleaning up character selection entities");
        
        // Clean up selection models through UI manager
        CharacterSelectionUIManager uiManager = FindFirstObjectByType<CharacterSelectionUIManager>();
        if (uiManager != null)
        {
            uiManager.CleanupSelectionModels();
            Debug.Log("SteamNetworkIntegration: Character selection models cleaned up via UI manager");
        }
        else
        {
            Debug.LogWarning("SteamNetworkIntegration: CharacterSelectionUIManager not found during cleanup");
        }
        
        Debug.Log("SteamNetworkIntegration: Character selection entity cleanup complete");
    }
    
    /// <summary>
    /// Properly despawns all selection NetworkObjects before disconnecting to prevent NetworkTransform errors
    /// </summary>
    private void CleanupSelectionNetworkObjects()
    {
        Debug.Log("SteamNetworkIntegration: Cleaning up selection NetworkObjects before disconnect");
        
        // Clean up deck preview cards first (before NetworkObjects are despawned)
        CharacterSelectionUIManager uiManager = FindFirstObjectByType<CharacterSelectionUIManager>();
        if (uiManager != null)
        {
            // Get the deck preview controller and clean up cards
            DeckPreviewController deckPreview = uiManager.GetComponent<DeckPreviewController>();
            if (deckPreview != null)
            {
                deckPreview.ClearAllDeckPreviews();
                Debug.Log("SteamNetworkIntegration: Cleared deck preview cards before NetworkObject cleanup");
            }
        }
        
        // Find all SelectionNetworkObjects in the scene
        SelectionNetworkObject[] selectionObjects = FindObjectsOfType<SelectionNetworkObject>();
        
        if (selectionObjects.Length == 0)
        {
            Debug.Log("SteamNetworkIntegration: No selection NetworkObjects found to clean up");
            return;
        }
        
        Debug.Log($"SteamNetworkIntegration: Found {selectionObjects.Length} selection NetworkObjects to despawn");
        
        // Despawn all selection NetworkObjects if we're the server
        if (fishNetManager != null && fishNetManager.IsServerStarted)
        {
            foreach (SelectionNetworkObject selectionObj in selectionObjects)
            {
                if (selectionObj != null)
                {
                    FishNet.Object.NetworkObject networkObject = selectionObj.GetComponent<FishNet.Object.NetworkObject>();
                    if (networkObject != null && networkObject.IsSpawned)
                    {
                        try
                        {
                            fishNetManager.ServerManager.Despawn(networkObject);
                            Debug.Log($"SteamNetworkIntegration: Despawned selection NetworkObject: {selectionObj.gameObject.name}");
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"SteamNetworkIntegration: Error despawning {selectionObj.gameObject.name}: {e.Message}");
                        }
                    }
                }
            }
        }
        else
        {
            Debug.Log("SteamNetworkIntegration: Not server, skipping NetworkObject despawn (will be handled by server)");
        }
        
        Debug.Log("SteamNetworkIntegration: Selection NetworkObject cleanup complete");
    }
    
    /// <summary>
    /// Coroutine to handle the return to start screen after intentional network cleanup
    /// </summary>
    private IEnumerator HandleReturnToStartAfterDisconnect()
    {
        // Wait a brief moment for network cleanup
        yield return new WaitForSeconds(0.5f);
        
        // Ensure we're fully disconnected
        if (fishNetManager != null)
        {
            if (fishNetManager.IsClientStarted)
            {
                fishNetManager.ClientManager.StopConnection();
                yield return new WaitForSeconds(0.2f);
            }
            
            if (fishNetManager.IsServerStarted)
            {
                fishNetManager.ServerManager.StopConnection(true);
                yield return new WaitForSeconds(0.2f);
            }
        }
        
        // Clean up any remaining network entities from character selection/lobby
        CleanupCharacterSelectionEntities();
        
        // DON'T call GamePhaseManager after disconnect since it's a NetworkObject
        // The UI transition will be handled by the StartScreenManager when reconnecting
        
        // Reset the intentional disconnect flag
        m_intentionalDisconnect = false;
        
        // Notify that the disconnect and return process is complete
        OnDisconnectedAndReturnedToStart?.Invoke();
        
        Debug.Log("SteamNetworkIntegration: Successfully disconnected from network session");
    }
    
    #endregion
} 