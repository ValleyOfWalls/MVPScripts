using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// Manages relationships between game entities (allies, enemies)
/// </summary>
public class RelationshipManager : NetworkBehaviour
{
    // Reference to allied entity using SyncVar to ensure proper replication to clients
    // Updated to FishNet v4 syntax
    public readonly SyncVar<NetworkBehaviour> allyEntity = new SyncVar<NetworkBehaviour>();
    
    // Inspector-visible representation of the allied entity (read-only)
    [Header("Alliance Information (Read-Only)")]
    [SerializeField, Tooltip("Current ally of this entity. For debugging only.")]
    private NetworkBehaviour inspectorAllyReference;
    
    [Header("Connection Information (Read-Only)")]
    [SerializeField, Tooltip("Client ID that owns this entity. For debugging and tracking.")]
    private int ownerClientId = -1;
    
    [SerializeField, Tooltip("Whether this entity is owned by the server (host)")]
    private bool isOwnedByServer = false;
    
    [SerializeField, Tooltip("Whether this entity is owned by the local player")]
    private bool isOwnedByLocalPlayer = false;
    
    /// <summary>
    /// Gets the allied entity
    /// </summary>
    public NetworkBehaviour Ally => allyEntity.Value;
    
    /// <summary>
    /// Gets the client ID of the entity's owner
    /// </summary>
    public int OwnerClientId => ownerClientId;
    
    /// <summary>
    /// Gets whether this entity is owned by the server (host)
    /// </summary>
    public bool IsOwnedByServer => isOwnedByServer;
    
    /// <summary>
    /// Gets whether this entity is owned by the local player
    /// </summary>
    public bool IsOwnedByLocalPlayer => isOwnedByLocalPlayer;
    
    private void Awake()
    {
        // Initialize connection tracking fields 
        UpdateConnectionInfo();
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        UpdateConnectionInfo();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        UpdateConnectionInfo();
    }
    
    // Fix: Change from override to a custom method with a different name
    // Method is called when ownership changes - must be custom rather than override
    public void OnOwnershipChange(FishNet.Connection.NetworkConnection prevOwner, FishNet.Connection.NetworkConnection newOwner)
    {
        UpdateConnectionInfo();
        Debug.Log($"Ownership changed for {gameObject.name} from ClientId: {(prevOwner != null ? prevOwner.ClientId.ToString() : "null")} to ClientId: {(newOwner != null ? newOwner.ClientId.ToString() : "null")}");
    }
    
    // This override method will be called by FishNet
    public override void OnOwnershipClient(FishNet.Connection.NetworkConnection prevOwner)
    {
        base.OnOwnershipClient(prevOwner);
        UpdateConnectionInfo();
        Debug.Log($"OnOwnershipClient called for {gameObject.name}. New ClientId: {OwnerClientId}");
    }

    // This override method will be called by FishNet
    public override void OnOwnershipServer(FishNet.Connection.NetworkConnection prevOwner)
    {
        base.OnOwnershipServer(prevOwner);
        UpdateConnectionInfo();
        Debug.Log($"OnOwnershipServer called for {gameObject.name}. New ClientId: {OwnerClientId}");
    }
    
    private void Update()
    {
        // Update inspector reference (only in editor)
        #if UNITY_EDITOR
        if (allyEntity.Value != inspectorAllyReference)
        {
            inspectorAllyReference = allyEntity.Value;
        }
        
        // In editor, periodically refresh connection info for debugging visibility
        if (Time.frameCount % 30 == 0) // Update every 30 frames to avoid performance impact
        {
            UpdateConnectionInfo();
        }
        #endif
    }
    
    /// <summary>
    /// Sets the allied entity reference
    /// </summary>
    /// <param name="ally">The allied entity</param>
    [Server]
    public void SetAlly(NetworkBehaviour ally)
    {
        if (!IsServerInitialized) return;
        allyEntity.Value = ally;
        inspectorAllyReference = ally; // Update the inspector reference immediately
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
    
    /// <summary>
    /// Sets up the relationship between player and pet
    /// Called by PlayerSpawner after network entities are created
    /// </summary>
    /// <param name="player">The player entity</param>
    /// <param name="pet">The pet entity</param>
    [Server]
    public static void SetupPlayerPetRelationship(NetworkPlayer player, NetworkPet pet)
    {
        if (player == null || pet == null) return;
        
        // Get relationship managers
        RelationshipManager playerRelations = player.GetComponent<RelationshipManager>();
        RelationshipManager petRelations = pet.GetComponent<RelationshipManager>();
        
        if (playerRelations != null && petRelations != null)
        {
            // Set mutual alliance
            playerRelations.SetAlly(pet);
            petRelations.SetAlly(player);
            
            // Update connection info
            playerRelations.UpdateConnectionInfo();
            petRelations.UpdateConnectionInfo();
            
            Debug.Log($"Established alliance between {player.name} (ClientID: {playerRelations.OwnerClientId}) and {pet.name} (ClientID: {petRelations.OwnerClientId})");
            
            // Update pet name based on player relationship
            pet.UpdatePetName();
        }
    }
} 