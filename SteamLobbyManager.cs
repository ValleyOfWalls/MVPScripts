using UnityEngine;
using Steamworks;
using System.Collections.Generic;
using System;

public class SteamLobbyManager : MonoBehaviour
{
    private static SteamLobbyManager s_instance;
    public static SteamLobbyManager Instance { get { return s_instance; } }

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
        
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxPlayers);
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
        
        // Set lobby data
        SteamMatchmaking.SetLobbyData(m_currentLobbyId, "name", "Game Lobby");
        SteamMatchmaking.SetLobbyData(m_currentLobbyId, "game_version", Application.version);
        
        OnLobbyCreated?.Invoke(true, m_currentLobbyId);
        
        // Start the host
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartHost();
        }
    }
    
    private void OnLobbyEnteredCallback(LobbyEnter_t param)
    {
        if (param.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Debug.LogError($"Failed to join lobby: {param.m_EChatRoomEnterResponse}");
            OnLobbyJoined?.Invoke(false, CSteamID.Nil);
            return;
        }
        
        m_currentLobbyId = new CSteamID(param.m_ulSteamIDLobby);
        m_isHost = SteamUser.GetSteamID() == SteamMatchmaking.GetLobbyOwner(m_currentLobbyId);
        
        RefreshPlayerList();
        
        OnLobbyJoined?.Invoke(true, m_currentLobbyId);
        
        // If we're not the host, connect to the host's server
        if (!m_isHost && GameManager.Instance != null)
        {
            // The lobby owner is the server
            string playerName = SteamFriends.GetPersonaName();
            GameManager.Instance.JoinGame(playerName);
        }
    }
    
    private void OnLobbyListCallback(LobbyMatchList_t param)
    {
        m_availableLobbies.Clear();
        
        for (int i = 0; i < param.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
            
            // Add lobby to the list
            m_availableLobbies.Add(lobbyId);
        }
        
        OnLobbiesListed?.Invoke(m_availableLobbies);
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