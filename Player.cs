using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;

public class Player : NetworkBehaviour
{
    [Header("Player Properties")]
    private readonly SyncVar<string> _playerName = new SyncVar<string>("");
    public string PlayerName
    {
        get => _playerName.Value;
        private set => _playerName.Value = value;
    }
    
    private readonly SyncVar<bool> _isReady = new SyncVar<bool>(false);
    public bool IsReady
    {
        get => _isReady.Value;
        private set => _isReady.Value = value;
    }
    
    private readonly SyncVar<int> _playerId = new SyncVar<int>(0);
    public int PlayerId
    {
        get => _playerId.Value;
        private set => _playerId.Value = value;
    }
    
    [Header("References")]
    [SerializeField] public MeshRenderer visualRenderer;
    
    // Private variables
    private Color playerColor;
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Set up callbacks for SyncVars
        _playerName.OnChange += OnPlayerNameChanged;
        _isReady.OnChange += OnReadyStatusChanged;
        
        if (IsOwner)
        {
            // Set player name from local storage if available
            if (PlayerPrefs.HasKey("PlayerName"))
            {
                string savedName = PlayerPrefs.GetString("PlayerName");
                CmdSetPlayerName(savedName);
            }
        }
        
        // Assign random color
        playerColor = new Color(
            Random.Range(0.2f, 0.9f),
            Random.Range(0.2f, 0.9f),
            Random.Range(0.2f, 0.9f)
        );
        
        if (visualRenderer != null)
        {
            visualRenderer.material.color = playerColor;
        }
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Unsubscribe from callbacks - Removed unnecessary null checks
        _playerName.OnChange -= OnPlayerNameChanged;
        _isReady.OnChange -= OnReadyStatusChanged;
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Assign sequential player ID
        PlayerId = GameManager.Instance != null ? GameManager.Instance.GetNextPlayerId() : 0;
    }
    
    [ServerRpc]
    public void CmdSetReady(bool isReady)
    {
        IsReady = isReady;
    }
    
    [ServerRpc]
    public void CmdSetPlayerName(string name)
    {
        if (string.IsNullOrEmpty(name))
            name = "Player" + PlayerId;
        
        PlayerName = name;
    }
    
    // Sync var callbacks
    private void OnPlayerNameChanged(string oldValue, string newValue, bool asServer)
    {
        // You can add additional logic here when the player name changes
        Debug.Log($"Player name changed: {oldValue} -> {newValue}");
    }
    
    private void OnReadyStatusChanged(bool oldValue, bool newValue, bool asServer)
    {
        // You can add additional logic here when the ready status changes
        Debug.Log($"Player {PlayerName} ready status changed: {oldValue} -> {newValue}");
        
        if (asServer)
        {
            // Notify game manager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPlayerReady(Owner, newValue);
            }
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void CmdLeaveGame(NetworkConnection conn = null)
    {
        // This lets us call this both as the owner and from the server with a specific connection
        if (conn == null)
            conn = Owner;
        
        // Notify game manager before disconnecting
        if (GameManager.Instance != null)
        {
            // Any cleanup needed
        }
        
        // Disconnect the client
        if (conn != null && conn.IsActive)
            conn.Disconnect(true);
    }
} 