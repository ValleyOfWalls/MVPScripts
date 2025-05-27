using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using FishNet.Connection;
using System.Linq;

// Struct to hold fight assignment data, to be used in a SyncList
[System.Serializable]
public struct FightAssignmentData
{
    public uint PlayerObjectId;
    public uint PetObjectId;
    public NetworkConnection PlayerConnection;

    public FightAssignmentData(NetworkEntity player, NetworkEntity pet)
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

    private readonly SyncList<FightAssignmentData> fightAssignments = new SyncList<FightAssignmentData>();

    // Server-side dictionaries for quick lookups
    private readonly Dictionary<uint, uint> playerToPetMap = new Dictionary<uint, uint>();
    private readonly Dictionary<uint, uint> petToPlayerMap = new Dictionary<uint, uint>();
    private readonly Dictionary<int, FightAssignmentData> connectionToFightMap = new Dictionary<int, FightAssignmentData>();
    
    // Event for fight assignments changes
    public event System.Action<bool> OnFightsChanged;

    // Debug visualization for the inspector
    [Header("Debug Visualization")]
    [SerializeField, Tooltip("Debug visualization of current fight assignments")] 
    private List<FightAssignmentDebug> debugAssignments = new List<FightAssignmentDebug>();

    [System.Serializable]
    private class FightAssignmentDebug
    {
        public string PlayerName;
        public uint PlayerObjectId;
        public string PetName;
        public uint PetObjectId;
    }

    [Header("Combat References")]
    [SerializeField, Tooltip("Reference to the network entity of the player in the local player's fight")]
    private NetworkEntity clientCombatPlayer;
    [SerializeField, Tooltip("Reference to the network entity of the pet the local player is fighting")]
    private NetworkEntity clientCombatOpponentPet;
    [SerializeField, Tooltip("Reference to the network entity of the player in the currently observed fight")]
    private NetworkEntity viewedCombatPlayer;
    [SerializeField, Tooltip("Reference to the network entity of the pet in the currently observed fight")]
    private NetworkEntity viewedCombatOpponentPet;

    // Public properties for combat references
    public NetworkEntity ClientCombatPlayer => clientCombatPlayer;
    public NetworkEntity ClientCombatOpponentPet => clientCombatOpponentPet;
    public NetworkEntity ViewedCombatPlayer => viewedCombatPlayer;
    public NetworkEntity ViewedCombatOpponentPet => viewedCombatOpponentPet;

    #region Lifecycle Methods

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
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
        fightAssignments.OnChange += OnFightAssignmentsChanged;
        RebuildLocalLookups(fightAssignments.ToList());
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        fightAssignments.OnChange -= OnFightAssignmentsChanged;
    }

    #endregion

    #region Event Handlers

    private void OnFightAssignmentsChanged(SyncListOperation op, int index, FightAssignmentData oldData, FightAssignmentData newData, bool asServer)
    {
        RebuildLocalLookups(fightAssignments.ToList());
        
        // Notify about fight status changes
        bool hasFights = fightAssignments.Count > 0;
        OnFightsChanged?.Invoke(hasFights);
        
        // Update entity visibility on clients
        if (!asServer)
        {
            UpdateEntityVisibility();
            UpdateCombatReferences();
        }

        UpdateDebugVisualization();
    }
    
    private void UpdateEntityVisibility()
    {
        EntityVisibilityManager entityVisManager = FindEntityVisibilityManager();
        if (entityVisManager != null)
        {
            entityVisManager.UpdateAllEntitiesVisibility();
        }
    }
    
    private EntityVisibilityManager FindEntityVisibilityManager()
    {
        GamePhaseManager gamePhaseManager = GamePhaseManager.Instance;
        EntityVisibilityManager entityVisManager = null;
        
        if (gamePhaseManager != null)
        {
            entityVisManager = gamePhaseManager.GetComponent<EntityVisibilityManager>();
        }
        
        if (entityVisManager == null)
        {
            entityVisManager = FindFirstObjectByType<EntityVisibilityManager>();
        }
        
        return entityVisManager;
    }

    private void UpdateCombatReferences()
    {
        // Get the local player's connection
        NetworkConnection localConnection = NetworkManager.ClientManager.Connection;
        if (localConnection == null) return;

        // Get the fight assignment for the local player
        var localFightAssignment = GetFightForConnection(localConnection);
        
        // Update client combat references
        if (localFightAssignment.HasValue)
        {
            clientCombatPlayer = GetNetworkObjectComponent<NetworkEntity>(localFightAssignment.Value.PlayerObjectId);
            clientCombatOpponentPet = GetNetworkObjectComponent<NetworkEntity>(localFightAssignment.Value.PetObjectId);
            
            // Initially set viewed combat references to the local combat
            viewedCombatPlayer = clientCombatPlayer;
            viewedCombatOpponentPet = clientCombatOpponentPet;
        }
        else
        {
            clientCombatPlayer = null;
            clientCombatOpponentPet = null;
            viewedCombatPlayer = null;
            viewedCombatOpponentPet = null;
        }
    }

    #endregion

    #region Debug Methods

    // Update the inspector visualization for debugging
    private void UpdateDebugVisualization()
    {
        debugAssignments.Clear();
        
        foreach (var assignment in fightAssignments)
        {
            FightAssignmentDebug debug = new FightAssignmentDebug();
            debug.PlayerObjectId = assignment.PlayerObjectId;
            debug.PetObjectId = assignment.PetObjectId;
            
            // Try to get names
            NetworkEntity player = GetNetworkObjectComponent<NetworkEntity>(assignment.PlayerObjectId);
            NetworkEntity pet = GetNetworkObjectComponent<NetworkEntity>(assignment.PetObjectId);
            
            debug.PlayerName = player != null ? player.EntityName.Value : "Unknown Player";
            debug.PetName = pet != null ? pet.EntityName.Value : "Unknown Pet";
            
            debugAssignments.Add(debug);
        }
    }

    #endregion

    #region Helper Methods

    // Helper function to get a component from a NetworkObject ID
    private T GetNetworkObjectComponent<T>(uint objectId) where T : NetworkBehaviour
    {
        NetworkObject nob = null;
        if (base.IsServerStarted)
        {
            NetworkManager.ServerManager.Objects.Spawned.TryGetValue((int)objectId, out nob);
        }
        else if (base.IsClientStarted)
        {
            NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)objectId, out nob);
        }
        
        return nob != null ? nob.GetComponent<T>() : null;
    }
    
    private void RebuildLocalLookups(List<FightAssignmentData> assignments)
    {
        playerToPetMap.Clear();
        petToPlayerMap.Clear();
        connectionToFightMap.Clear();
        
        foreach (var assignment in assignments)
        {
            playerToPetMap[(uint)assignment.PlayerObjectId] = (uint)assignment.PetObjectId;
            petToPlayerMap[(uint)assignment.PetObjectId] = (uint)assignment.PlayerObjectId;
            
            if (assignment.PlayerConnection != null)
            {
                connectionToFightMap[assignment.PlayerConnection.ClientId] = assignment;
            }
        }
    }

    #endregion

    #region Public API

    [Server]
    public void AddFightAssignment(NetworkEntity player, NetworkEntity pet)
    {
        if (player == null || pet == null)
        {
            Debug.LogError("Cannot add fight assignment: Player or Pet is null.");
            return;
        }

        if (player.EntityType != EntityType.Player || pet.EntityType != EntityType.Pet)
        {
            Debug.LogError("Invalid entity types for fight assignment. First argument must be a Player, second must be a Pet.");
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
    }

    [Server]
    public void RemoveFightAssignment(NetworkEntity player)
    {
        if (player == null || player.EntityType != EntityType.Player) return;
        
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
        }
    }

    [Server]
    public void ClearAllAssignments()
    {
        fightAssignments.Clear();
        playerToPetMap.Clear();
        petToPlayerMap.Clear();
        connectionToFightMap.Clear();
    }

    public NetworkEntity GetOpponentForPlayer(NetworkEntity player)
    {
        if (player == null || player.EntityType != EntityType.Player) return null;
        
        // First, try the direct lookup
        if (playerToPetMap.TryGetValue((uint)player.ObjectId, out uint petId))
        {
            return GetNetworkObjectComponent<NetworkEntity>(petId);
        }
        
        // If that fails, scan through assignments
        foreach (var assignment in fightAssignments)
        {
            if (assignment.PlayerObjectId == (uint)player.ObjectId)
            {
                return GetNetworkObjectComponent<NetworkEntity>(assignment.PetObjectId);
            }
        }
        
        return null;
    }

    public NetworkEntity GetOpponentForPet(NetworkEntity pet)
    {
        if (pet == null || pet.EntityType != EntityType.Pet) return null;
        
        // First, try the direct lookup
        if (petToPlayerMap.TryGetValue((uint)pet.ObjectId, out uint playerId))
        {
            return GetNetworkObjectComponent<NetworkEntity>(playerId);
        }
        
        // If that fails, scan through assignments
        foreach (var assignment in fightAssignments)
        {
            if (assignment.PetObjectId == (uint)pet.ObjectId)
            {
                return GetNetworkObjectComponent<NetworkEntity>(assignment.PlayerObjectId);
            }
        }
        
        return null;
    }

    public FightAssignmentData? GetFightForConnection(NetworkConnection connection)
    {
        if (connection == null) return null;
        
        // First try connection lookup for better performance
        if (connectionToFightMap.TryGetValue(connection.ClientId, out FightAssignmentData directMatch))
        {
            return directMatch;
        }
        
        // Fall back to assignment list scan
        foreach (var assignment in fightAssignments)
        {
            if (assignment.PlayerConnection != null && assignment.PlayerConnection.ClientId == connection.ClientId)
            {
                return assignment;
            }
        }
        
        return null;
    }

    public FightAssignmentData? GetFightForPlayer(NetworkEntity player)
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

    public FightAssignmentData? GetFightForPet(NetworkEntity pet)
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

    public List<FightAssignmentData> GetAllFightAssignments()
    {
        return fightAssignments.ToList();
    }

    public bool AreAssignmentsComplete()
    {
        List<NetworkEntity> players = Object.FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None).ToList();
        return players.Count > 0 && fightAssignments.Count == players.Count;
    }

    public bool HasActiveFights()
    {
        return fightAssignments.Count > 0;
    }
    
    /// <summary>
    /// Changes which fight is currently being viewed
    /// </summary>
    /// <param name="player">The player whose fight you want to view</param>
    /// <returns>True if the fight was found and view was updated, false otherwise</returns>
    public bool SetViewedFight(NetworkEntity player)
    {
        if (player == null) return false;
        
        var fightAssignment = GetFightForPlayer(player);
        if (!fightAssignment.HasValue) return false;
        
        viewedCombatPlayer = GetNetworkObjectComponent<NetworkEntity>(fightAssignment.Value.PlayerObjectId);
        viewedCombatOpponentPet = GetNetworkObjectComponent<NetworkEntity>(fightAssignment.Value.PetObjectId);
        
        // Update entity visibility for the new viewed fight
        UpdateEntityVisibility();
        
        return true;
    }
    
    #endregion
} 