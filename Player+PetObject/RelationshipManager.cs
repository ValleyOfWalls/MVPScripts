using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// Manages relationships between game entities (allies, enemies)
/// </summary>
public class RelationshipManager : NetworkBehaviour
{
    // Reference to allied entity, synchronized across network
    private readonly SyncVar<NetworkObject> _syncedAllyObject = new SyncVar<NetworkObject>();
    
    // Cached reference to ally entity
    private NetworkBehaviour _cachedAlly;
    
    /// <summary>
    /// Gets the allied entity
    /// </summary>
    public NetworkBehaviour Ally 
    { 
        get 
        {
            if (_cachedAlly == null && _syncedAllyObject.Value != null)
            {
                _cachedAlly = _syncedAllyObject.Value.GetComponent<NetworkBehaviour>();
            }
            return _cachedAlly;
        }
    }
    
    /// <summary>
    /// Sets the allied entity reference
    /// </summary>
    /// <param name="ally">The allied entity</param>
    [Server]
    public void SetAlly(NetworkBehaviour ally)
    {
        if (!IsServerInitialized) return;
        
        _cachedAlly = ally;
        _syncedAllyObject.Value = ally?.GetComponent<NetworkObject>();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Try to cache the ally reference when object starts on client
        if (_syncedAllyObject.Value != null && _cachedAlly == null)
        {
            _cachedAlly = _syncedAllyObject.Value.GetComponent<NetworkBehaviour>();
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
            
            Debug.Log($"Established alliance between {player.name} and {pet.name}");
        }
    }
} 