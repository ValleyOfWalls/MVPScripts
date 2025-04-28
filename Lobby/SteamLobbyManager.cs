using UnityEngine;
using Steamworks;
using System.Collections.Generic;
using System;

public class SteamLobbyManager : MonoBehaviour
{
    private static SteamLobbyManager s_instance;
    public static SteamLobbyManager Instance { get { return s_instance; } }

    // Constants for Lobby Filtering
    private const string LOBBY_GAME_ID_KEY = "GameID";
    private const string LOBBY_GAME_ID_VALUE = "3DMVP_LOBBY_V1"; // Unique ID for your game
    private const string LOBBY_VERSION_KEY = "Version"; // Key for game version

    [SerializeField] private int maxPlayers = 8;
    
    // Delegates
    public delegate void LobbyCreatedDelegate(bool success, CSteamID lobbyId);
    public delegate void LobbyJoinedDelegate(bool success, CSteamID lobbyId);
    public delegate void LobbiesListDelegate(List<CSteamID> lobbies);
    public delegate void PlayerListUpdatedDelegate();

    // Callback handles
    private Callback<LobbyCreated_t> m_lobbyCreated;
    private Callback<LobbyEnter_t> m_lobbyEntered;
    private Callback<LobbyMatchList_t> m_lobbyList;
    private Callback<LobbyDataUpdate_t> m_lobbyDataUpdated;
    
    // Events
    public event LobbyCreatedDelegate OnLobbyCreated;
    public event LobbyJoinedDelegate OnLobbyJoined;
    public event LobbiesListDelegate OnLobbiesListed;
    public event PlayerListUpdatedDelegate OnPlayerListUpdated;
    
    // State
    private CSteamID m_currentLobbyId;
    private bool m_isHost = false;
    private List<CSteamID> m_currentLobbyMembers = new List<CSteamID>();
    private List<CSteamID> m_availableLobbies = new List<CSteamID>();
    
    public bool IsInLobby { get { return m_currentLobbyId.IsValid(); } }
    public bool IsUserHost { get { return m_isHost; } }
    public CSteamID CurrentLobbyID { get { return m_currentLobbyId; } }
    
    private void Awake()
    {
        if (s_instance != null)
        {
            Destroy(gameObject);
            return;
        }
        s_instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    private void Start()
    {
        if (!SteamManager.Instance || !SteamManager.Instance.Initialized)
        {
            Debug.LogWarning("Steam is not initialized. SteamLobbyManager will not function correctly.");
            return;
        }
        
        // Initialize callbacks
        m_lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreatedCallback);
        m_lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEnteredCallback);
        m_lobbyList = Callback<LobbyMatchList_t>.Create(OnLobbyListCallback);
        m_lobbyDataUpdated = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdatedCallback);
    }
    
    #region Public Methods
    public void CreateLobby()
    {
        if (!SteamManager.Instance || !SteamManager.Instance.Initialized)
        {
            OnLobbyCreated?.Invoke(false, CSteamID.Nil);
            return;
        }
        
        // SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxPlayers);
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxPlayers); // Use Public for easier testing
    }
    
    public void JoinLobby(CSteamID lobbyId)
    {
        if (!SteamManager.Instance || !SteamManager.Instance.Initialized)
        {
            OnLobbyJoined?.Invoke(false, CSteamID.Nil);
            return;
        }
        
        SteamMatchmaking.JoinLobby(lobbyId);
    }
    
    public void LeaveLobby()
    {
        if (!SteamManager.Instance || !SteamManager.Instance.Initialized || !m_currentLobbyId.IsValid())
            return;
            
        SteamMatchmaking.LeaveLobby(m_currentLobbyId);
        m_currentLobbyId = CSteamID.Nil;
        m_isHost = false;
        m_currentLobbyMembers.Clear();
        
        // If we're in the game, disconnect from the server
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LeaveGame();
        }
    }
    
    public void RequestLobbiesList()
    {
        if (!SteamManager.Instance || !SteamManager.Instance.Initialized)
        {
            OnLobbiesListed?.Invoke(new List<CSteamID>());
            return;
        }
        
        // Add filters before requesting the list
        SteamMatchmaking.AddRequestLobbyListStringFilter(LOBBY_GAME_ID_KEY, LOBBY_GAME_ID_VALUE, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListStringFilter(LOBBY_VERSION_KEY, Application.version, ELobbyComparison.k_ELobbyComparisonEqual);
        // You can add other filters here if needed (e.g., distance, slots available)
        
        SteamMatchmaking.RequestLobbyList();
    }
    
    public void RefreshPlayerList()
    {
        if (!SteamManager.Instance || !SteamManager.Instance.Initialized || !m_currentLobbyId.IsValid())
            return;
            
        m_currentLobbyMembers.Clear();
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(m_currentLobbyId);
        
        for (int i = 0; i < memberCount; i++)
        {
            CSteamID playerId = SteamMatchmaking.GetLobbyMemberByIndex(m_currentLobbyId, i);
            m_currentLobbyMembers.Add(playerId);
        }
        
        OnPlayerListUpdated?.Invoke();
    }
    
    public string GetPlayerName(CSteamID playerId)
    {
        if (!SteamManager.Instance || !SteamManager.Instance.Initialized)
            return "Unknown Player";
            
        return SteamFriends.GetFriendPersonaName(playerId);
    }
    
    public int GetPlayerCount()
    {
        if (!m_currentLobbyId.IsValid())
            return 0;
            
        return SteamMatchmaking.GetNumLobbyMembers(m_currentLobbyId);
    }
    
    public List<CSteamID> GetPlayerList()
    {
        return new List<CSteamID>(m_currentLobbyMembers);
    }
    
    public CSteamID GetLobbyOwner()
    {
        if (!m_currentLobbyId.IsValid())
            return CSteamID.Nil;
            
        return SteamMatchmaking.GetLobbyOwner(m_currentLobbyId);
    }
    
    public bool IsLobbyOwner()
    {
        if (!m_currentLobbyId.IsValid())
            return false;
            
        return SteamUser.GetSteamID() == SteamMatchmaking.GetLobbyOwner(m_currentLobbyId);
    }
    
    public List<CSteamID> GetAvailableLobbies()
    {
        return new List<CSteamID>(m_availableLobbies);
    }
    #endregion
    
    #region Steam Callbacks
    private void OnLobbyCreatedCallback(LobbyCreated_t param)
    {
        if (param.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"Failed to create lobby: {param.m_eResult}");
            OnLobbyCreated?.Invoke(false, CSteamID.Nil);
            return;
        }
        
        m_currentLobbyId = new CSteamID(param.m_ulSteamIDLobby);
        m_isHost = true;
        
        // Set lobby data for filtering
        SteamMatchmaking.SetLobbyData(m_currentLobbyId, LOBBY_GAME_ID_KEY, LOBBY_GAME_ID_VALUE);
        SteamMatchmaking.SetLobbyData(m_currentLobbyId, LOBBY_VERSION_KEY, Application.version); 
        // SteamMatchmaking.SetLobbyData(m_currentLobbyId, "name", "Game Lobby"); // Redundant if filtering by GameID
        // SteamMatchmaking.SetLobbyData(m_currentLobbyId, "game_version", Application.version); // Replaced by constant
        
        OnLobbyCreated?.Invoke(true, m_currentLobbyId);
        
        // Start the host
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartHost();
        }
    }
    
    private void OnLobbyEnteredCallback(LobbyEnter_t param)
    {
        CSteamID lobbyId = new CSteamID(param.m_ulSteamIDLobby);
        Debug.Log($"OnLobbyEnteredCallback received for lobby {lobbyId}. Response code: {param.m_EChatRoomEnterResponse}");

        if (param.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Debug.LogError($"Failed to join lobby: {(EChatRoomEnterResponse)param.m_EChatRoomEnterResponse}");
            OnLobbyJoined?.Invoke(false, CSteamID.Nil);
            return;
        }
        
        Debug.Log($"Successfully entered lobby {lobbyId}.");
        m_currentLobbyId = lobbyId;
        m_isHost = false; // Explicitly set as client because we joined an existing lobby
        Debug.Log($"Is this client the host? {m_isHost} (Forced to false as we joined)");
        
        RefreshPlayerList();
        
        OnLobbyJoined?.Invoke(true, m_currentLobbyId);
        
        // If we're not the host, connect to the host's server
        if (!m_isHost)
        {
            if (GameManager.Instance != null)
            {
                string playerName = SteamFriends.GetPersonaName();
                Debug.Log($"GameManager instance found. Calling JoinGame({playerName}) for lobby {m_currentLobbyId}...");
                GameManager.Instance.JoinGame(playerName);
            }
            else
            {
                 Debug.LogError("Cannot JoinGame: GameManager instance is null!");
            }
        }
        else
        {
            Debug.Log("This client is the host, not calling JoinGame.");
        }
    }
    
    private void OnLobbyListCallback(LobbyMatchList_t param)
    {
        m_availableLobbies.Clear();
        
        for (int i = 0; i < param.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
            
            // You might want to add more filtering here based on lobby data
            // e.g., SteamMatchmaking.GetLobbyData(lobbyId, "game_version") == Application.version
            
            // Add lobby to the list
            m_availableLobbies.Add(lobbyId);
        }
        
        OnLobbiesListed?.Invoke(m_availableLobbies);
        
        // If lobbies were found, join the first one. Otherwise, create a new one.
        if (m_availableLobbies.Count > 0)
        {
            Debug.Log($"Found {m_availableLobbies.Count} lobbies. Joining the first one: {m_availableLobbies[0]}");
            JoinLobby(m_availableLobbies[0]); 
        }
        else
        {
            Debug.Log("No lobbies found. Creating a new lobby.");
            CreateLobby();
        }
    }
    
    private void OnLobbyDataUpdatedCallback(LobbyDataUpdate_t param)
    {
        if (param.m_ulSteamIDLobby == m_currentLobbyId.m_SteamID)
        {
            RefreshPlayerList();
        }
    }
    #endregion
} 