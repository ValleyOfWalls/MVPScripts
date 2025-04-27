using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using Steamworks;
using FishNet;

public class NetworkPlayer : NetworkBehaviour
{
    // Static collection of all players
    public static readonly List<NetworkPlayer> Players = new List<NetworkPlayer>();
    
    [Header("Steam Identity")]
    private readonly SyncVar<string> _steamName = new SyncVar<string>("");
    private readonly SyncVar<ulong> _steamId = new SyncVar<ulong>(0);
    
    [Header("Player State")]
    private readonly SyncVar<bool> _isReady = new SyncVar<bool>(false);
    
    // Properties
    public string SteamName => _steamName.Value;
    public ulong SteamID => _steamId.Value;
    public bool IsReady => _isReady.Value;
    
    private void Awake()
    {
        // Handle any non-network initialization
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        Players.Add(this); // Add server instance to list
         // If GameManager exists, update UI
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UpdatePlayerList();
            GameManager.Instance.UpdateLobbyControls();
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        Players.Remove(this); // Remove server instance from list
         // If GameManager exists, update UI
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UpdatePlayerList();
            GameManager.Instance.UpdateLobbyControls();
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Add player to the static list
        if (!Players.Contains(this))
            Players.Add(this);
            
        // Setup change callbacks
        _isReady.OnChange += OnReadyStateChanged;
        _steamName.OnChange += OnSteamNameChanged; // Add callback for name change
        
        if (IsOwner)
        {
            // Initialize steam info
            if (SteamManager.Instance && SteamManager.Instance.Initialized)
            {
                string name = SteamFriends.GetPersonaName();
                 ulong id = SteamUser.GetSteamID().m_SteamID;
                 Debug.Log($"Owner setting Steam info: Name={name}, ID={id}");
                 CmdSetSteamInfo(name, id);
                 // Also inform GameManager about the join
                 CmdJoinLobby(name); 
            }
            else
            {
                 Debug.LogWarning("Steam not initialized for owner. Using default name.");
                 string defaultName = PlayerPrefs.GetString("PlayerName", "Player");
                 CmdSetSteamInfo(defaultName, 0); // Use default name if Steam fails
                 CmdJoinLobby(defaultName);
            }
        }
        
        // Update the UI (ensure GameManager instance exists)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UpdatePlayerList(); // Request UI update
            GameManager.Instance.UpdateLobbyControls();
        }
        else
        {
            Debug.LogWarning("GameManager instance not found in OnStartClient");
        }
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Remove player from the static list
        Players.Remove(this);
        
        // Clean up callbacks
        _isReady.OnChange -= OnReadyStateChanged;
        _steamName.OnChange -= OnSteamNameChanged;
       
        // Update the UI only if the GameManager still exists (not during scene unload)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UpdatePlayerList();
            GameManager.Instance.UpdateLobbyControls();
        }
    }
    
    #region Server RPCs (Commands)
    [ServerRpc]
    public void CmdSetSteamInfo(string steamName, ulong steamId)
    {
        _steamName.Value = steamName;
        _steamId.Value = steamId;
         Debug.Log($"Server received Steam info: Name={steamName}, ID={steamId}");
         // Potentially trigger UI update here too if name display needs instant server update
         if (GameManager.Instance != null) 
            GameManager.Instance.UpdatePlayerList();
    }
    
    [ServerRpc]
    public void CmdSetReady(bool isReady)
    {
        _isReady.Value = isReady;
        // GameManager needs to be notified on the server
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ServerSetPlayerReady(Owner, isReady);
        }
    }

    [ServerRpc(RequireOwnership = false)] // Allow server to call this if needed
    public void CmdJoinLobby(string playerName, NetworkConnection sender = null)
    {
        if (sender == null) sender = Owner; // Default to owner if called by client
        Debug.Log($"CmdJoinLobby received on server from {sender.ClientId} with name {playerName}");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ServerAddPlayer(sender, playerName);
        }
        else
        {
            Debug.LogError("GameManager instance is null on server during CmdJoinLobby!");
        }
    }
    #endregion

    #region Observer RPCs
    // These RPCs can be called from the server to update all clients.
    // Note: These methods were previously in GameManager, but GameManager is no longer a NetworkBehaviour.
    // We might not need these specific RPCs if the SyncVar updates handle UI refresh adequately.
    // Let's comment them out for now and rely on SyncVar OnChange callbacks.

    /*
    [ObserversRpc(BufferLast = true)]
    private void RpcUpdatePlayerListInternal(List<PlayerInfo> updatedPlayers)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RpcUpdatePlayerList(updatedPlayers); // Forward call
        else 
             Debug.LogWarning("GameManager null in RpcUpdatePlayerListInternal");
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcSetStartGameButtonActiveInternal(bool active)
    {
         if (GameManager.Instance != null)
            GameManager.Instance.RpcSetStartGameButtonActive(active); // Forward call
         else 
             Debug.LogWarning("GameManager null in RpcSetStartGameButtonActiveInternal");
    }

    [ObserversRpc(BufferLast = true)]
    private void RpcStartGameInternal()
    {
         if (GameManager.Instance != null)
            GameManager.Instance.RpcStartGame(); // Forward call
         else 
             Debug.LogWarning("GameManager null in RpcStartGameInternal");
    }
    */

    #endregion

    #region Client and Host Methods
    public void ToggleReady()
    {
        if (IsOwner)
        {
             Debug.Log($"ToggleReady called by owner. Current state: {IsReady}, sending {!IsReady}");
            CmdSetReady(!IsReady);
        }
           
    }
    
    public string GetSteamName()
    {
        return string.IsNullOrEmpty(SteamName) ? "Player" : SteamName;
    }
    
    public bool GetIsReady()
    {
        return IsReady;
    }
    #endregion
    
    #region Callbacks
    // Updated signature
    private void OnReadyStateChanged(bool oldValue, bool newValue, bool asServer)
    {
        Debug.Log($"OnReadyStateChanged: Player {SteamName} changed ready from {oldValue} to {newValue}. IsServer: {asServer}");
        // Check for GameManager instance before updating UI
        if (GameManager.Instance != null)
        {
            // UpdatePlayerList is called by GameManager when notified by server
            // UpdateLobbyControls is called by GameManager when notified by server
             if (!asServer) // Only clients need to react directly to update their own button maybe?
             {
                  if(IsOwner && UIManager.Instance != null) UIManager.Instance.UpdateReadyButtonState(newValue);
             }
             // Let GameManager handle broad updates
             GameManager.Instance.UpdateLobbyControls();
        }
        else
        {
            Debug.LogWarning("GameManager instance not found in OnReadyStateChanged");
        }
    }

    private void OnSteamNameChanged(string oldValue, string newValue, bool asServer)
    {
         Debug.Log($"OnSteamNameChanged: Player ID {SteamID} changed name from '{oldValue}' to '{newValue}'. IsServer: {asServer}");
         if (GameManager.Instance != null)
         {
             GameManager.Instance.UpdatePlayerList();
         }
    }
    #endregion
} 