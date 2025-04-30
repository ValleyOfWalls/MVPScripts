using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using Steamworks;

public class LobbyPlayerInfo : NetworkBehaviour
{
    [Header("Lobby State")]
    private readonly SyncVar<bool> _isReady = new SyncVar<bool>(false);
    
    // Properties
    public bool IsReady => _isReady.Value;
    
    private NetworkPlayer _networkPlayer;
    
    private void Awake()
    {
        // Cache the NetworkPlayer reference
        _networkPlayer = GetComponent<NetworkPlayer>();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Setup change callbacks
        _isReady.OnChange += OnReadyStateChanged;
        
        if (IsOwner)
        {
            Debug.Log("LobbyPlayerInfo started as owner - joining lobby");
            // Notify LobbyManager that we've joined. Pass SteamID.
            if (SteamManager.Instance != null && SteamManager.Instance.Initialized)
            {
                ulong steamId = SteamUser.GetSteamID().m_SteamID;
                CmdJoinLobby(steamId); 
            }
            else
            {
                 Debug.LogError("Cannot get SteamID: SteamManager not initialized!");
                 // Optionally handle this error, maybe send 0 or a default?
                 CmdJoinLobby(0); // Send 0 if Steam isn't ready
            }
        }
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Clean up callbacks
        _isReady.OnChange -= OnReadyStateChanged;
    }
    
    private string GetPlayerName()
    {
        // Try to get player name from NetworkPlayer
        if (_networkPlayer != null)
        {
            return _networkPlayer.GetSteamName();
        }
        
        // Fallback to Steam name if available
        if (SteamManager.Instance && SteamManager.Instance.Initialized)
        {
            return SteamFriends.GetPersonaName();
        }
        
        // Final fallback
        if (PlayerPrefs.HasKey("PlayerName"))
        {
            return PlayerPrefs.GetString("PlayerName");
        }
        
        return "Player";
    }
    
    #region Server RPCs (Commands)
    [ServerRpc]
    public void CmdSetReady(bool isReady)
    {
        _isReady.Value = isReady;
        Debug.Log($"Player ready value set to {isReady} on server");
        
        // LobbyManager needs to be notified on the server
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.SetPlayerReady(Owner, isReady);
        }
        else
        {
            Debug.LogError("LobbyManager instance is null on server during CmdSetReady!");
        }
    }

    [ServerRpc(RequireOwnership = false)] // Allow server to call this if needed
    public void CmdJoinLobby(ulong steamId, NetworkConnection sender = null) // Added steamId parameter
    {
        if (sender == null) sender = Owner; // Default to owner if called by client
        
        Debug.Log($"CmdJoinLobby received on server from {sender.ClientId} with SteamID {steamId}.");
        
        if (LobbyManager.Instance != null)
        {
            // Pass the steamId to AddPlayer
            LobbyManager.Instance.AddPlayer(sender, steamId); 
        }
        else
        {
            Debug.LogError("LobbyManager instance is null on server during CmdJoinLobby!");
        }
    }
    #endregion
    
    #region Client Methods
    public void ToggleReady()
    {
        if (this == null)
        {
            Debug.LogError("LobbyPlayerInfo is null when ToggleReady is called!");
            return;
        }
        
        if (NetworkObject == null)
        {
            Debug.LogError("NetworkObject is null in LobbyPlayerInfo.ToggleReady!");
            return;
        }
        
        if (IsOwner)
        {
            Debug.Log($"ToggleReady called by owner. Current state: {IsReady}, sending {!IsReady}");
            CmdSetReady(!IsReady);
        }
        else
        {
            Debug.LogError("ToggleReady called by non-owner!");
        }
    }
    
    public bool GetIsReady()
    {
        return IsReady;
    }
    #endregion
    
    #region Callbacks
    private void OnReadyStateChanged(bool oldValue, bool newValue, bool asServer)
    {
        Debug.Log($"OnReadyStateChanged: Player changed ready from {oldValue} to {newValue}. IsServer: {asServer}");
        
        // Only clients need to update UI
        if (!asServer)
        {
            if (IsOwner && LobbyUIManager.Instance != null) 
                LobbyUIManager.Instance.UpdateReadyButtonState(newValue);
        }
        
        // Let LobbyManager handle broad updates
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdateLobbyControls();
        }
        else
        {
            Debug.LogWarning("LobbyManager is null in OnReadyStateChanged");
        }
    }
    #endregion
}