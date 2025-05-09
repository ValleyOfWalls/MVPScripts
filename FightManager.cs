using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using FishNet.Connection;
using System.Linq;

// Struct to hold fight assignment data, to be used in a SyncList
[System.Serializable] // Make it serializable to be used in SyncList if storing simple data types
public struct FightAssignmentData // Not a NetworkBehaviour
{
    public uint PlayerObjectId;
    public uint PetObjectId;
    public NetworkConnection PlayerConnection; // Store connection for easier lookup if needed

    public FightAssignmentData(NetworkPlayer player, NetworkPet pet)
    {
        PlayerObjectId = player != null ? (uint)player.ObjectId : 0U;
        PetObjectId = pet != null ? (uint)pet.ObjectId : 0U;
        PlayerConnection = player != null ? player.Owner : null;
    }
}

/// <summary>
/// Manages the assignments of players to pets for combat encounters.
/// Attach to: A persistent NetworkObject in the scene to track fight pairings.
/// </summary>
public class FightManager : NetworkBehaviour
{
    public static FightManager Instance { get; private set; }

    // We will sync a list of these structs. FishNet needs to know how to serialize/deserialize it.
    // For complex types or NetworkBehaviour references directly in SyncList, custom serializers are needed.
    // Storing ObjectIds is generally safer and simpler for SyncLists.
    private readonly SyncList<FightAssignmentData> fightAssignments = new SyncList<FightAssignmentData>();

    // Server-side dictionary for quick lookups. This is not synced directly.
    private readonly Dictionary<uint, uint> playerToPetMap = new Dictionary<uint, uint>(); // PlayerID -> PetID
    private readonly Dictionary<uint, uint> petToPlayerMap = new Dictionary<uint, uint>(); // PetID -> PlayerID
    
    // Connection-based lookup for better performance with many players
    private readonly Dictionary<int, FightAssignmentData> connectionToFightMap = new Dictionary<int, FightAssignmentData>(); // ConnectionId -> Fight

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // If this is a scene object that persists across scene loads
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // Server can initialize anything it needs here
        fightAssignments.OnChange += OnFightAssignmentsChanged;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        fightAssignments.OnChange -= OnFightAssignmentsChanged;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // Clients subscribe to changes to update their local understanding if needed
        fightAssignments.OnChange += OnFightAssignmentsChanged;
        // Rebuild local lookup dictionaries if needed (though direct iteration of SyncList is often fine for clients)
        RebuildLocalLookups(fightAssignments.ToList()); // Initial population
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        fightAssignments.OnChange -= OnFightAssignmentsChanged;
    }

    private void OnFightAssignmentsChanged(SyncListOperation op, int index, FightAssignmentData oldData, FightAssignmentData newData, bool asServer)
    {
        // When the list changes, server might update its dictionaries.
        // Clients might update their local view or caches if they use them.
        Debug.Log($"FightAssignments changed. Op: {op}, Index: {index}, NewPlayer: {newData.PlayerObjectId}, NewPet: {newData.PetObjectId}. Rebuilding lookups.");
        
        // Both server and client can rebuild their fast-access maps if they use them.
        RebuildLocalLookups(fightAssignments.ToList());
    }

    [Server]
    public void AddFightAssignment(NetworkPlayer player, NetworkPet pet)
    {
        if (player == null || pet == null)
        {
            Debug.LogError("Cannot add fight assignment: Player or Pet is null.");
            return;
        }

        // Check if player or pet is already in a fight
        if (playerToPetMap.ContainsKey((uint)player.ObjectId) || petToPlayerMap.ContainsKey((uint)pet.ObjectId))
        {
            Debug.LogWarning($"Cannot add fight: Player {player.ObjectId} or Pet {pet.ObjectId} is already in a fight.");
            return;
        }

        FightAssignmentData assignment = new FightAssignmentData(player, pet);
        fightAssignments.Add(assignment);

        // Update server-side quick lookups
        playerToPetMap[(uint)player.ObjectId] = (uint)pet.ObjectId;
        petToPlayerMap[(uint)pet.ObjectId] = (uint)player.ObjectId;
        
        // Add to connection lookup if player has an owner connection
        if (player.Owner != null)
        {
            connectionToFightMap[player.Owner.ClientId] = assignment;
        }

        Debug.Log($"FightManager: Added assignment PlayerID {player.ObjectId} vs PetID {pet.ObjectId}");
    }

    [Server]
    public void RemoveFightAssignment(NetworkPlayer player)
    {
        if (player == null) return;
        if (playerToPetMap.TryGetValue((uint)player.ObjectId, out uint petId))
        {
            fightAssignments.RemoveAll(fa => fa.PlayerObjectId == (uint)player.ObjectId);
            playerToPetMap.Remove((uint)player.ObjectId);
            petToPlayerMap.Remove(petId);
            
            // Remove from connection lookup
            if (player.Owner != null)
            {
                connectionToFightMap.Remove(player.Owner.ClientId);
            }
            
            Debug.Log($"FightManager: Removed assignment for PlayerID {player.ObjectId}");
        }
    }

    [Server]
    public void ClearAllAssignments()
    {
        fightAssignments.Clear();
        playerToPetMap.Clear();
        petToPlayerMap.Clear();
        connectionToFightMap.Clear();
        Debug.Log("FightManager: All fight assignments cleared.");
    }

    // Client or Server can call these to get opponents
    public NetworkPet GetOpponentForPlayer(NetworkPlayer player)
    {
        if (player == null) return null;
        if (playerToPetMap.TryGetValue((uint)player.ObjectId, out uint petObjectId))
        {
            NetworkObject petNob = null;
            if (base.IsServerStarted)
            {
                base.NetworkManager.ServerManager.Objects.Spawned.TryGetValue((int)petObjectId, out petNob);
            }
            else if (base.IsClientStarted)
            {
                base.NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)petObjectId, out petNob);
            }

            if (petNob != null)
            {
                return petNob.GetComponent<NetworkPet>();
            }
        }
        return null;
    }

    public NetworkPlayer GetOpponentForPet(NetworkPet pet)
    {
        if (pet == null) return null;
        if (petToPlayerMap.TryGetValue((uint)pet.ObjectId, out uint playerObjectId))
        {
            NetworkObject playerNob = null;
            if (base.IsServerStarted)
            {
                base.NetworkManager.ServerManager.Objects.Spawned.TryGetValue((int)playerObjectId, out playerNob);
            }
            else if (base.IsClientStarted)
            {
                base.NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)playerObjectId, out playerNob);
            }

            if (playerNob != null)
            {
                return playerNob.GetComponent<NetworkPlayer>();
            }
        }
        return null;
    }
    
    // Get fight assignment for a specific connection - optimized for per-client access
    public FightAssignmentData? GetFightForConnection(NetworkConnection connection)
    {
        if (connection == null) return null;
        
        // Try fast lookup first
        if (connectionToFightMap.TryGetValue(connection.ClientId, out FightAssignmentData fight))
        {
            return fight;
        }
        
        // Fallback to iterating through assignments if not in cache
        foreach (var assignment in fightAssignments)
        {
            if (assignment.PlayerConnection == connection)
            {
                // Cache for future lookups
                connectionToFightMap[connection.ClientId] = assignment;
                return assignment;
            }
        }
        
        return null;
    }
    
    // Get fight assignment for a specific player - optimized lookup
    public FightAssignmentData? GetFightForPlayer(NetworkPlayer player)
    {
        if (player == null) return null;
        
        foreach (var assignment in fightAssignments)
        {
            if (assignment.PlayerObjectId == (uint)player.ObjectId)
            {
                return assignment;
            }
        }
        
        return null;
    }
    
    // Get fight assignment for a specific pet - optimized lookup
    public FightAssignmentData? GetFightForPet(NetworkPet pet)
    {
        if (pet == null) return null;
        
        foreach (var assignment in fightAssignments)
        {
            if (assignment.PetObjectId == (uint)pet.ObjectId)
            {
                return assignment;
            }
        }
        
        return null;
    }

    // Helper to rebuild local dictionaries from the SyncList (can be called by client or server on change)
    private void RebuildLocalLookups(List<FightAssignmentData> assignments)
    {
        playerToPetMap.Clear();
        petToPlayerMap.Clear();
        connectionToFightMap.Clear();
        
        foreach (var assignment in assignments)
        {
            if (assignment.PlayerObjectId != 0 && assignment.PetObjectId != 0)
            {
                playerToPetMap[assignment.PlayerObjectId] = assignment.PetObjectId;
                petToPlayerMap[assignment.PetObjectId] = assignment.PlayerObjectId;
                
                // Rebuild connection mapping if PlayerConnection is available
                if (assignment.PlayerConnection != null)
                {
                    connectionToFightMap[assignment.PlayerConnection.ClientId] = assignment;
                }
            }
        }
    }
    
    public List<FightAssignmentData> GetAllFightAssignments()
    {
        return new List<FightAssignmentData>(fightAssignments);
    }
} 