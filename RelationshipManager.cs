using UnityEngine;
using FishNet.Object;

/// <summary>
/// Manages relationships between game entities (allies, enemies)
/// </summary>
public class RelationshipManager : NetworkBehaviour
{
    // Reference to allied entity
    [SerializeField] private NetworkBehaviour allyEntity;
    
    /// <summary>
    /// Gets the allied entity
    /// </summary>
    public NetworkBehaviour Ally => allyEntity;
    
    /// <summary>
    /// Sets the allied entity reference
    /// </summary>
    /// <param name="ally">The allied entity</param>
    public void SetAlly(NetworkBehaviour ally)
    {
        allyEntity = ally;
    }
    
    /// <summary>
    /// Sets up the relationship between player and pet
    /// Called by PlayerSpawner after network entities are created
    /// </summary>
    /// <param name="player">The player entity</param>
    /// <param name="pet">The pet entity</param>
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