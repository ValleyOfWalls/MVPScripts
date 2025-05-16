using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;

/// <summary>
/// Manages relationships between networked entities (player-pet pairs, allies, etc.)
/// Attach to: NetworkEntity prefabs
/// </summary>
public class RelationshipManager : NetworkBehaviour
{
    // Networked reference to ally entity
    public readonly SyncVar<NetworkBehaviour> allyEntity = new SyncVar<NetworkBehaviour>();
    
    // Inspector reference for debugging
    [SerializeField] private NetworkBehaviour inspectorAllyReference;
    
    // Connection tracking
    private int ownerClientId = -1;
    private bool isOwnedByServer = false;
    private bool isOwnedByLocalPlayer = false;
    
    public int OwnerClientId => ownerClientId;
    public bool IsOwnedByServer => isOwnedByServer;
    public bool IsOwnedByLocalPlayer => isOwnedByLocalPlayer;
    
    public NetworkBehaviour AllyEntity => allyEntity.Value;

    public override void OnStartServer()
    {
        base.OnStartServer();
        UpdateConnectionInfo();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        UpdateConnectionInfo();
        allyEntity.OnChange += OnAllyChanged;
        if (IsClientOnly && allyEntity.Value != null)
        {
            inspectorAllyReference = allyEntity.Value;
        }
    }

    public override void OnOwnershipClient(NetworkConnection prevOwner)
    {
        base.OnOwnershipClient(prevOwner);
        UpdateConnectionInfo();
    }

    public override void OnOwnershipServer(NetworkConnection prevOwner)
    {
        base.OnOwnershipServer(prevOwner);
        UpdateConnectionInfo();
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (IsServerInitialized)
        {
            allyEntity.Value = null;
        }
        inspectorAllyReference = null;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        allyEntity.OnChange -= OnAllyChanged;
        inspectorAllyReference = null;
    }

    /// <summary>
    /// Called when the allyEntity SyncVar changes.
    /// </summary>
    private void OnAllyChanged(NetworkBehaviour prevAlly, NetworkBehaviour newAlly, bool asServer)
    {
        if (!asServer) // This means the change was received from the server for a client
        {
            inspectorAllyReference = newAlly; // Update client's inspector for debugging
            Debug.Log($"Client (ID: {OwnerClientId}): AllyEntity updated from '{prevAlly?.name}' to '{newAlly?.name}'. IsOwnedByLocalPlayer: {IsOwnedByLocalPlayer}");
        }
        // If asServer is true, this callback is also invoked on the server when it changes the value.
        // In that case, SetAlly already updated inspectorAllyReference.
    }

    /// <summary>
    /// Sets the ally entity for this entity
    /// </summary>
    [Server]
    public void SetAlly(NetworkBehaviour ally)
    {
        if (!IsServerInitialized) return;
        allyEntity.Value = ally;
        inspectorAllyReference = ally; // Update the server's inspector reference
        Debug.Log($"Server: SetAlly called for {gameObject.name}. New ally: {ally?.name}. Current allyEntity: {allyEntity.Value?.name}");
    }
    
    /// <summary>
    /// Updates the connection tracking properties
    /// </summary>
    private void UpdateConnectionInfo()
    {
        if (Owner != null)
        {
            ownerClientId = Owner.ClientId;
            isOwnedByServer = Owner.IsHost || Owner.ClientId == 0;
            
            // Check if this entity is owned by the local client
            if (IsClientInitialized)
            {
                // Compare the Owner's ClientId with the local client's connection ClientId
                isOwnedByLocalPlayer = Owner.ClientId == FishNet.InstanceFinder.ClientManager.Connection.ClientId;
            }
            else
            {
                isOwnedByLocalPlayer = false;
            }
        }
        else
        {
            ownerClientId = -1;
            isOwnedByServer = false;
            isOwnedByLocalPlayer = false;
        }
    }
} 