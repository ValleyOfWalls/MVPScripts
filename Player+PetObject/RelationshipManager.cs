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
    
    // Networked reference to hand entity
    public readonly SyncVar<NetworkBehaviour> handEntity = new SyncVar<NetworkBehaviour>();
    
    // Networked reference to stats UI entity
    public readonly SyncVar<NetworkBehaviour> statsUIEntity = new SyncVar<NetworkBehaviour>();
    
    // Inspector references for debugging
    [SerializeField] private NetworkBehaviour inspectorAllyReference;
    [SerializeField] private NetworkBehaviour inspectorHandReference;
    [SerializeField] private NetworkBehaviour inspectorStatsUIReference;
    
    // Connection tracking
    private int ownerClientId = -1;
    private bool isOwnedByServer = false;
    private bool isOwnedByLocalPlayer = false;
    
    public int OwnerClientId => ownerClientId;
    public bool IsOwnedByServer => isOwnedByServer;
    public bool IsOwnedByLocalPlayer => isOwnedByLocalPlayer;
    
    public NetworkBehaviour AllyEntity => allyEntity.Value;
    public NetworkBehaviour HandEntity => handEntity.Value;
    public NetworkBehaviour StatsUIEntity => statsUIEntity.Value;

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
        handEntity.OnChange += OnHandChanged;
        statsUIEntity.OnChange += OnStatsUIChanged;
        if (IsClientOnlyInitialized && allyEntity.Value != null)
        {
            inspectorAllyReference = allyEntity.Value;
        }
        if (IsClientOnlyInitialized && handEntity.Value != null)
        {
            inspectorHandReference = handEntity.Value;
        }
        if (IsClientOnlyInitialized && statsUIEntity.Value != null)
        {
            inspectorStatsUIReference = statsUIEntity.Value;
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
            handEntity.Value = null;
            statsUIEntity.Value = null;
        }
        inspectorAllyReference = null;
        inspectorHandReference = null;
        inspectorStatsUIReference = null;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        allyEntity.OnChange -= OnAllyChanged;
        handEntity.OnChange -= OnHandChanged;
        statsUIEntity.OnChange -= OnStatsUIChanged;
        inspectorAllyReference = null;
        inspectorHandReference = null;
        inspectorStatsUIReference = null;
    }

    /// <summary>
    /// Called when the allyEntity SyncVar changes.
    /// </summary>
    private void OnAllyChanged(NetworkBehaviour prevAlly, NetworkBehaviour newAlly, bool asServer)
    {
        NetworkEntity thisEntity = GetComponent<NetworkEntity>();
        NetworkEntity prevAllyEntity = prevAlly?.GetComponent<NetworkEntity>();
        NetworkEntity newAllyEntity = newAlly?.GetComponent<NetworkEntity>();
        
        Debug.Log($"[RELATIONSHIP_CHANGE] OnAllyChanged for {(thisEntity != null ? thisEntity.EntityName.Value : "unknown")} (ID: {(thisEntity != null ? thisEntity.ObjectId : 0)}, Type: {(thisEntity != null ? thisEntity.EntityType : EntityType.Player)}): {(prevAllyEntity != null ? prevAllyEntity.EntityName.Value : "null")} -> {(newAllyEntity != null ? newAllyEntity.EntityName.Value : "null")} (asServer: {asServer})");
        
        if (!asServer) // This means the change was received from the server for a client
        {
            inspectorAllyReference = newAlly; // Update client's inspector for debugging
            
            // If this is a stats UI entity and it has a new ally, trigger linking
            if (thisEntity != null && (thisEntity.EntityType == EntityType.PlayerStatsUI || thisEntity.EntityType == EntityType.PetStatsUI) && newAllyEntity != null)
            {
                var statsUIController = thisEntity.GetComponent<EntityStatsUIController>();
                if (statsUIController != null)
                {
                    Debug.Log($"[RELATIONSHIP_CHANGE] Stats UI {thisEntity.EntityName.Value} got new ally {newAllyEntity.EntityName.Value}, triggering TryLinkToMainEntity");
                    statsUIController.TryLinkToMainEntity();
                }
            }
        }
        // If asServer is true, this callback is also invoked on the server when it changes the value.
        // In that case, SetAlly already updated inspectorAllyReference.
    }

    /// <summary>
    /// Called when the handEntity SyncVar changes.
    /// </summary>
    private void OnHandChanged(NetworkBehaviour prevHand, NetworkBehaviour newHand, bool asServer)
    {
        if (!asServer) // This means the change was received from the server for a client
        {
            inspectorHandReference = newHand; // Update client's inspector for debugging
    
        }
        // If asServer is true, this callback is also invoked on the server when it changes the value.
        // In that case, SetHand already updated inspectorHandReference.
    }

    /// <summary>
    /// Called when the statsUIEntity SyncVar changes.
    /// </summary>
    private void OnStatsUIChanged(NetworkBehaviour prevStatsUI, NetworkBehaviour newStatsUI, bool asServer)
    {
        NetworkEntity thisEntity = GetComponent<NetworkEntity>();
        NetworkEntity prevStatsUIEntity = prevStatsUI?.GetComponent<NetworkEntity>();
        NetworkEntity newStatsUIEntity = newStatsUI?.GetComponent<NetworkEntity>();
        
        Debug.Log($"[RELATIONSHIP_CHANGE] OnStatsUIChanged for {(thisEntity != null ? thisEntity.EntityName.Value : "unknown")} (ID: {(thisEntity != null ? thisEntity.ObjectId : 0)}): {(prevStatsUIEntity != null ? prevStatsUIEntity.EntityName.Value : "null")} -> {(newStatsUIEntity != null ? newStatsUIEntity.EntityName.Value : "null")} (asServer: {asServer})");
        
        if (!asServer) // This means the change was received from the server for a client
        {
            inspectorStatsUIReference = newStatsUI; // Update client's inspector for debugging
            
            // If there's a new stats UI, trigger its linking
            if (newStatsUIEntity != null)
            {
                var statsUIController = newStatsUIEntity.GetComponent<EntityStatsUIController>();
                if (statsUIController != null)
                {
                    Debug.Log($"[RELATIONSHIP_CHANGE] Triggering stats UI controller to link to {thisEntity.EntityName.Value}");
                    // The controller should automatically link via TryLinkToMainEntity when the relationship changes
                }
            }
        }
        // If asServer is true, this callback is also invoked on the server when it changes the value.
        // In that case, SetStatsUI already updated inspectorStatsUIReference.
    }

    /// <summary>
    /// Sets the ally entity for this entity
    /// </summary>
    [Server]
    public void SetAlly(NetworkBehaviour ally)
    {
        if (!IsServerInitialized) return;
        
        NetworkEntity thisEntity = GetComponent<NetworkEntity>();
        NetworkEntity allyEntity = ally?.GetComponent<NetworkEntity>();
        
        Debug.Log($"[RELATIONSHIP] SetAlly called: {(thisEntity != null ? thisEntity.EntityName.Value : "unknown")} (ID: {(thisEntity != null ? thisEntity.ObjectId : 0)}) -> Ally: {(allyEntity != null ? allyEntity.EntityName.Value : "null")} (ID: {(allyEntity != null ? allyEntity.ObjectId : 0)})");
        
        this.allyEntity.Value = ally;
        inspectorAllyReference = ally; // Update the server's inspector reference
    }
    
    /// <summary>
    /// Sets the hand entity for this entity
    /// </summary>
    [Server]
    public void SetHand(NetworkBehaviour hand)
    {
        if (!IsServerInitialized) return;
        handEntity.Value = hand;
        inspectorHandReference = hand; // Update the server's inspector reference
    }
    
    /// <summary>
    /// Sets the stats UI entity for this entity
    /// </summary>
    [Server]
    public void SetStatsUI(NetworkBehaviour statsUI)
    {
        if (!IsServerInitialized) return;
        
        NetworkEntity thisEntity = GetComponent<NetworkEntity>();
        NetworkEntity statsUIEntity = statsUI?.GetComponent<NetworkEntity>();
        
        Debug.Log($"[RELATIONSHIP] SetStatsUI called: {(thisEntity != null ? thisEntity.EntityName.Value : "unknown")} (ID: {(thisEntity != null ? thisEntity.ObjectId : 0)}) -> StatsUI: {(statsUIEntity != null ? statsUIEntity.EntityName.Value : "null")} (ID: {(statsUIEntity != null ? statsUIEntity.ObjectId : 0)})");
        
        this.statsUIEntity.Value = statsUI;
        inspectorStatsUIReference = statsUI; // Update the server's inspector reference
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