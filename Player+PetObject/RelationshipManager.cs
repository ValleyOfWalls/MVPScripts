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
    
    /// <summary>
    /// Gets the allied entity
    /// </summary>
    public NetworkBehaviour Ally => allyEntity.Value;
    
    private void Update()
    {
        // Update inspector reference (only in editor)
        #if UNITY_EDITOR
        if (allyEntity.Value != inspectorAllyReference)
        {
            inspectorAllyReference = allyEntity.Value;
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
            
            Debug.Log($"Established alliance between {player.name} and {pet.name}");
        }
    }
} 