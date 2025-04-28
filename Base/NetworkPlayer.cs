using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using Steamworks;

public class NetworkPlayer : NetworkBehaviour
{
    // Static collection of all players
    public static readonly List<NetworkPlayer> Players = new List<NetworkPlayer>();
    
    [Header("Steam Identity")]
    private readonly SyncVar<string> _steamName = new SyncVar<string>("");
    private readonly SyncVar<ulong> _steamId = new SyncVar<ulong>(0);
    
    // Properties
    public string SteamName => _steamName.Value;
    public ulong SteamID => _steamId.Value;
    
    private void Awake()
    {
        // Handle any non-network initialization
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        Players.Add(this); // Add server instance to list
        
        // If GameManager exists, update UI
        if (GameManager.Instance != null && LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdatePlayerList();
            LobbyManager.Instance.UpdateLobbyControls();
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        Players.Remove(this); // Remove server instance from list
        
        // If GameManager exists, update UI
        if (GameManager.Instance != null && LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdatePlayerList();
            LobbyManager.Instance.UpdateLobbyControls();
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Add player to the static list
        if (!Players.Contains(this))
            Players.Add(this);
            
        // Setup change callbacks
        _steamName.OnChange += OnSteamNameChanged;
        
        if (IsOwner)
        {
            // Initialize steam info
            if (SteamManager.Instance && SteamManager.Instance.Initialized)
            {
                string name = SteamFriends.GetPersonaName();
                ulong id = SteamUser.GetSteamID().m_SteamID;
                Debug.Log($"Owner setting Steam info: Name={name}, ID={id}");
                CmdSetSteamInfo(name, id);
            }
            else
            {
                Debug.LogWarning("Steam not initialized for owner. Using default name.");
                string defaultName = PlayerPrefs.GetString("PlayerName", "Player");
                CmdSetSteamInfo(defaultName, 0); // Use default name if Steam fails
            }
        }
        
        // Update the UI
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdatePlayerList();
            LobbyManager.Instance.UpdateLobbyControls();
        }
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Remove player from the static list
        Players.Remove(this);
        
        // Clean up callbacks
        _steamName.OnChange -= OnSteamNameChanged;
       
        // Update the UI only if LobbyManager still exists
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdatePlayerList();
            LobbyManager.Instance.UpdateLobbyControls();
        }
    }
    
    #region Server RPCs (Commands)
    [ServerRpc]
    public void CmdSetSteamInfo(string steamName, ulong steamId)
    {
        _steamName.Value = steamName;
        _steamId.Value = steamId;
        Debug.Log($"Server received Steam info: Name={steamName}, ID={steamId}");
        
        // Update UI if needed
        if (LobbyManager.Instance != null) 
            LobbyManager.Instance.UpdatePlayerList();
    }
    #endregion

    #region Client and Host Methods
    public string GetSteamName()
    {
        return string.IsNullOrEmpty(SteamName) ? "Player" : SteamName;
    }
    #endregion
    
    #region Callbacks
    private void OnSteamNameChanged(string oldValue, string newValue, bool asServer)
    {
        Debug.Log($"OnSteamNameChanged: Player ID {SteamID} changed name from '{oldValue}' to '{newValue}'. IsServer: {asServer}");
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdatePlayerList();
        }
    }
    #endregion

    // Combat-related methods can be added here as needed
}