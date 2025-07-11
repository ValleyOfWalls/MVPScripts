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
    public uint LeftFighterObjectId;    // Previously PlayerObjectId - the player fighting on the left
    public uint RightFighterObjectId;   // Previously PetObjectId - the pet fighting on the right
    public NetworkConnection LeftFighterConnection;

    public FightAssignmentData(NetworkEntity leftFighter, NetworkEntity rightFighter)
    {
        LeftFighterObjectId = leftFighter != null ? (uint)leftFighter.ObjectId : 0U;
        RightFighterObjectId = rightFighter != null ? (uint)rightFighter.ObjectId : 0U;
        LeftFighterConnection = leftFighter != null ? leftFighter.Owner : null;
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

    // Quick lookup dictionaries for performance (private, implementation details)
    private readonly Dictionary<uint, uint> leftToRightMap = new Dictionary<uint, uint>();
    private readonly Dictionary<uint, uint> rightToLeftMap = new Dictionary<uint, uint>();
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
        public string LeftFighterName;
        public uint LeftFighterObjectId;
        public string RightFighterName;
        public uint RightFighterObjectId;
    }

    [Header("Combat References")]
    [SerializeField, Tooltip("Reference to the network entity of the left fighter in the local client's fight")]
    private NetworkEntity clientLeftFighter;
    [SerializeField, Tooltip("Reference to the network entity of the right fighter in the local client's fight")]
    private NetworkEntity clientRightFighter;
    [SerializeField, Tooltip("Reference to the network entity of the left fighter in the currently viewed fight")]
    private NetworkEntity viewedLeftFighter;
    [SerializeField, Tooltip("Reference to the network entity of the right fighter in the currently viewed fight")]
    private NetworkEntity viewedRightFighter;

    // Public properties for combat references
    public NetworkEntity ClientLeftFighter => clientLeftFighter;
    public NetworkEntity ClientRightFighter => clientRightFighter;
    public NetworkEntity ViewedLeftFighter => viewedLeftFighter;
    public NetworkEntity ViewedRightFighter => viewedRightFighter;
    
    // Legacy compatibility properties (for gradual migration)
    public NetworkEntity ClientCombatPlayer => clientLeftFighter;
    public NetworkEntity ClientCombatOpponentPet => clientRightFighter;
    public NetworkEntity ViewedCombatPlayer => viewedLeftFighter;
    public NetworkEntity ViewedCombatOpponentPet => viewedRightFighter;

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
        Debug.Log($"FLEXPAIRING: OnFightAssignmentsChanged - Operation: {op}, Index: {index}, AsServer: {asServer}");
        Debug.Log($"FLEXPAIRING: Total fight assignments: {fightAssignments.Count}");
        
        foreach (var assignment in fightAssignments)
        {
            Debug.Log($"FLEXPAIRING: Assignment - Left: {assignment.LeftFighterObjectId}, Right: {assignment.RightFighterObjectId}");
        }
        
        RebuildLocalLookups(fightAssignments.ToList());
        
        // Notify about fight status changes
        bool hasFights = fightAssignments.Count > 0;
        Debug.Log($"FLEXPAIRING: Has fights: {hasFights}, invoking OnFightsChanged");
        OnFightsChanged?.Invoke(hasFights);
        
        // Update entity visibility on clients
        if (!asServer)
        {
            Debug.Log("FLEXPAIRING: Client side - updating entity visibility and combat references");
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
        Debug.Log($"FLEXPAIRING: UpdateCombatReferences - Local connection: {localConnection?.ClientId}");
        if (localConnection == null) return;

        // Get the fight assignment for the local player
        var localFightAssignment = GetFightForConnection(localConnection);
        Debug.Log($"FLEXPAIRING: Local fight assignment found: {localFightAssignment.HasValue}");
        
        // Update client combat references
        if (localFightAssignment.HasValue)
        {
            Debug.Log($"FLEXPAIRING: Local fight - Left: {localFightAssignment.Value.LeftFighterObjectId}, Right: {localFightAssignment.Value.RightFighterObjectId}");
            
            clientLeftFighter = GetNetworkObjectComponent<NetworkEntity>(localFightAssignment.Value.LeftFighterObjectId);
            clientRightFighter = GetNetworkObjectComponent<NetworkEntity>(localFightAssignment.Value.RightFighterObjectId);
            
            Debug.Log($"FLEXPAIRING: Client combat references - Left: {clientLeftFighter?.EntityName.Value}, Right: {clientRightFighter?.EntityName.Value}");
            
            // Initially set viewed combat references to the local combat
            viewedLeftFighter = clientLeftFighter;
            viewedRightFighter = clientRightFighter;
            
            // Notify EntityVisibilityManager that combat references are ready
            NotifyEntityVisibilityManagerReady();
        }
        else
        {
            Debug.Log("FLEXPAIRING: No local fight assignment found, clearing combat references");
            clientLeftFighter = null;
            clientRightFighter = null;
            viewedLeftFighter = null;
            viewedRightFighter = null;
        }
    }

    /// <summary>
    /// Notifies the EntityVisibilityManager that fight assignments and combat references are ready
    /// </summary>
    private void NotifyEntityVisibilityManagerReady()
    {
        // Only notify if we have fight assignments and valid viewed combat references
        if (fightAssignments.Count > 0 && viewedLeftFighter != null && viewedRightFighter != null)
        {
            EntityVisibilityManager entityVisManager = FindEntityVisibilityManager();
            if (entityVisManager != null)
            {
                Debug.Log("FightManager: Notifying EntityVisibilityManager that combat references are ready");
                entityVisManager.OnFightManagerReady();
            }
            else
            {
                Debug.LogWarning("FightManager: Could not find EntityVisibilityManager to notify readiness");
            }
        }
    }

    /// <summary>
    /// Debug method to manually notify EntityVisibilityManager
    /// </summary>
    [ContextMenu("Force Notify EntityVisibilityManager")]
    public void ForceNotifyEntityVisibilityManager()
    {
        Debug.Log($"FightManager: Force notifying EntityVisibilityManager. Fight assignments: {fightAssignments.Count}, ViewedLeftFighter: {(viewedLeftFighter != null ? viewedLeftFighter.EntityName.Value : "null")}, ViewedRightFighter: {(viewedRightFighter != null ? viewedRightFighter.EntityName.Value : "null")}");
        NotifyEntityVisibilityManagerReady();
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
            debug.LeftFighterObjectId = assignment.LeftFighterObjectId;
            debug.RightFighterObjectId = assignment.RightFighterObjectId;
            
            // Try to get names
            NetworkEntity player = GetNetworkObjectComponent<NetworkEntity>(assignment.LeftFighterObjectId);
            NetworkEntity pet = GetNetworkObjectComponent<NetworkEntity>(assignment.RightFighterObjectId);
            
            debug.LeftFighterName = player != null ? player.EntityName.Value : "Unknown Fighter";
            debug.RightFighterName = pet != null ? pet.EntityName.Value : "Unknown Fighter";
            
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
        leftToRightMap.Clear();
        rightToLeftMap.Clear();
        // Note: connectionToFightMap is now managed manually in AddFightAssignment, not rebuilt here
        
        foreach (var assignment in assignments)
        {
            leftToRightMap[(uint)assignment.LeftFighterObjectId] = (uint)assignment.RightFighterObjectId;
            rightToLeftMap[(uint)assignment.RightFighterObjectId] = (uint)assignment.LeftFighterObjectId;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Checks if two entities are allies (same owner or one owns the other)
    /// </summary>
    private bool AreEntitiesAllies(NetworkEntity entity1, NetworkEntity entity2)
    {
        if (entity1 == null || entity2 == null) return false;

        // Check if they are the same entity
        if (entity1.ObjectId == entity2.ObjectId) return true;

        // Check if one owns the other
        if (entity1.EntityType == EntityType.Player && entity2.EntityType == EntityType.Pet)
        {
            return entity2.OwnerEntityId.Value == entity1.ObjectId;
        }
        if (entity2.EntityType == EntityType.Player && entity1.EntityType == EntityType.Pet)
        {
            return entity1.OwnerEntityId.Value == entity2.ObjectId;
        }

        // Check if they have the same owner (both are pets owned by the same player)
        if (entity1.EntityType == EntityType.Pet && entity2.EntityType == EntityType.Pet)
        {
            return entity1.OwnerEntityId.Value == entity2.OwnerEntityId.Value;
        }

        // Players are not allies with other players by default
        return false;
    }

    #endregion

    #region Public API

    [Server]
    public void AddFightAssignment(NetworkEntity leftFighter, NetworkEntity rightFighter)
    {
        Debug.Log($"FLEXPAIRING: AddFightAssignment called with {leftFighter?.EntityName.Value} ({leftFighter?.EntityType}) vs {rightFighter?.EntityName.Value} ({rightFighter?.EntityType})");
        
        if (leftFighter == null || rightFighter == null)
        {
            Debug.LogError("FLEXPAIRING: Cannot add fight assignment: Left Fighter or Right Fighter is null.");
            return;
        }

        // Check if flexible pairing is enabled
        OfflineGameManager offlineManager = OfflineGameManager.Instance;
        bool flexiblePairingEnabled = offlineManager != null && offlineManager.EnableFlexiblePairing;

        Debug.Log($"FLEXPAIRING: Flexible pairing enabled: {flexiblePairingEnabled}");

        if (!flexiblePairingEnabled)
        {
            // Original rigid pairing rules
            if (leftFighter.EntityType != EntityType.Player || rightFighter.EntityType != EntityType.Pet)
            {
                Debug.LogError("FLEXPAIRING: Invalid entity types for fight assignment. First argument must be a Player, second must be a Pet.");
                return;
            }
        }
        else
        {
            // Flexible pairing rules - just ensure both are valid combat entities
            if ((leftFighter.EntityType != EntityType.Player && leftFighter.EntityType != EntityType.Pet) ||
                (rightFighter.EntityType != EntityType.Player && rightFighter.EntityType != EntityType.Pet))
            {
                Debug.LogError("FLEXPAIRING: Invalid entity types for fight assignment. Both entities must be either Player or Pet.");
                return;
            }

            // Ensure entities are not allies (same owner or one owns the other)
            Debug.Log($"FLEXPAIRING: Checking if {leftFighter.EntityName.Value} and {rightFighter.EntityName.Value} are allies");
            if (AreEntitiesAllies(leftFighter, rightFighter))
            {
                Debug.LogError($"FLEXPAIRING: Cannot pair allies: {leftFighter.EntityName.Value} and {rightFighter.EntityName.Value} are allies.");
                return;
            }
            Debug.Log($"FLEXPAIRING: Entities are not allies, proceeding with pairing");
        }

        // Check if leftFighter or rightFighter is already in a fight
        bool leftAlreadyFighting = leftToRightMap.ContainsKey((uint)leftFighter.ObjectId);
        bool rightAlreadyFighting = rightToLeftMap.ContainsKey((uint)rightFighter.ObjectId);
        
        Debug.Log($"FLEXPAIRING: Left fighter already fighting: {leftAlreadyFighting}, Right fighter already fighting: {rightAlreadyFighting}");
        
        if (leftAlreadyFighting || rightAlreadyFighting)
        {
            Debug.LogWarning($"FLEXPAIRING: Cannot add fight: Left Fighter {leftFighter.ObjectId} or Right Fighter {rightFighter.ObjectId} is already in a fight.");
            return;
        }

        FightAssignmentData assignment = new FightAssignmentData(leftFighter, rightFighter);
        Debug.Log($"FLEXPAIRING: Adding fight assignment to SyncList");
        fightAssignments.Add(assignment);

        // Update server-side quick lookups
        leftToRightMap[(uint)leftFighter.ObjectId] = (uint)rightFighter.ObjectId;
        rightToLeftMap[(uint)rightFighter.ObjectId] = (uint)leftFighter.ObjectId;
        
        // Add to connection lookup - ONLY map clients to fights where they have a Player entity participating
        // This ensures each client sees the fight where their Player is involved
        
        if (leftFighter.EntityType == EntityType.Player && leftFighter.Owner != null)
        {
            Debug.Log($"FLEXPAIRING: Checking left Player {leftFighter.EntityName.Value} (Client {leftFighter.Owner.ClientId})");
            Debug.Log($"FLEXPAIRING: connectionToFightMap contains client {leftFighter.Owner.ClientId}: {connectionToFightMap.ContainsKey(leftFighter.Owner.ClientId)}");
            
            // Only add the mapping if this client isn't already mapped to a fight
            if (!connectionToFightMap.ContainsKey(leftFighter.Owner.ClientId))
            {
                connectionToFightMap[leftFighter.Owner.ClientId] = assignment;
                Debug.Log($"FLEXPAIRING: Added connection mapping for client {leftFighter.Owner.ClientId} (Player entity: {leftFighter.EntityName.Value})");
            }
            else
            {
                var existingAssignment = connectionToFightMap[leftFighter.Owner.ClientId];
                Debug.Log($"FLEXPAIRING: Client {leftFighter.Owner.ClientId} already mapped to fight (Left: {existingAssignment.LeftFighterObjectId}, Right: {existingAssignment.RightFighterObjectId}), skipping mapping for Player {leftFighter.EntityName.Value}");
            }
        }
        
        if (rightFighter.EntityType == EntityType.Player && rightFighter.Owner != null)
        {
            Debug.Log($"FLEXPAIRING: Checking right Player {rightFighter.EntityName.Value} (Client {rightFighter.Owner.ClientId})");
            Debug.Log($"FLEXPAIRING: connectionToFightMap contains client {rightFighter.Owner.ClientId}: {connectionToFightMap.ContainsKey(rightFighter.Owner.ClientId)}");
            
            // Only add the mapping if this client isn't already mapped to a fight
            if (!connectionToFightMap.ContainsKey(rightFighter.Owner.ClientId))
            {
                connectionToFightMap[rightFighter.Owner.ClientId] = assignment;
                Debug.Log($"FLEXPAIRING: Added connection mapping for client {rightFighter.Owner.ClientId} (Player entity: {rightFighter.EntityName.Value})");
            }
            else
            {
                var existingAssignment = connectionToFightMap[rightFighter.Owner.ClientId];
                Debug.Log($"FLEXPAIRING: Client {rightFighter.Owner.ClientId} already mapped to fight (Left: {existingAssignment.LeftFighterObjectId}, Right: {existingAssignment.RightFighterObjectId}), skipping mapping for Player {rightFighter.EntityName.Value}");
            }
        }
        
        // If neither fighter is a Player, don't map any clients to this fight
        if (leftFighter.EntityType != EntityType.Player && rightFighter.EntityType != EntityType.Player)
        {
            Debug.Log($"FLEXPAIRING: Pet vs Pet fight - no client mapping needed for {leftFighter.EntityName.Value} vs {rightFighter.EntityName.Value}");
        }

        Debug.Log($"FLEXPAIRING: Fight assignment successfully added: {leftFighter.EntityName.Value} ({leftFighter.EntityType}) vs {rightFighter.EntityName.Value} ({rightFighter.EntityType})");
    }

    [Server]
    public void RemoveFightAssignment(NetworkEntity leftFighter)
    {
        if (leftFighter == null || leftFighter.EntityType != EntityType.Player) return;
        
        if (leftToRightMap.TryGetValue((uint)leftFighter.ObjectId, out uint rightFighterId))
        {
            fightAssignments.RemoveAll(fa => fa.LeftFighterObjectId == (uint)leftFighter.ObjectId);
            leftToRightMap.Remove((uint)leftFighter.ObjectId);
            rightToLeftMap.Remove(rightFighterId);
            
            // Remove from connection lookup
            if (leftFighter.Owner != null)
            {
                connectionToFightMap.Remove(leftFighter.Owner.ClientId);
            }
        }
    }

    [Server]
    public void ClearAllAssignments()
    {
        fightAssignments.Clear();
        leftToRightMap.Clear();
        rightToLeftMap.Clear();
        connectionToFightMap.Clear();
    }

    /// <summary>
    /// Gets the opponent entity for any fighter in a combat
    /// </summary>
    /// <param name="fighter">The fighter to find an opponent for</param>
    /// <returns>The opponent entity, or null if not found</returns>
    public NetworkEntity GetOpponent(NetworkEntity fighter)
    {
        if (fighter == null) return null;
        
        if (fighter.EntityType == EntityType.Player)
        {
            // Player fights against a pet
            if (leftToRightMap.TryGetValue((uint)fighter.ObjectId, out uint opponentId))
            {
                return GetNetworkObjectComponent<NetworkEntity>(opponentId);
            }
        }
        else if (fighter.EntityType == EntityType.Pet)
        {
            // Pet fights against a player
            if (rightToLeftMap.TryGetValue((uint)fighter.ObjectId, out uint opponentId))
            {
                return GetNetworkObjectComponent<NetworkEntity>(opponentId);
            }
        }
        
        // Fallback: scan through assignments
        foreach (var assignment in fightAssignments)
        {
            if (assignment.LeftFighterObjectId == (uint)fighter.ObjectId)
            {
                return GetNetworkObjectComponent<NetworkEntity>(assignment.RightFighterObjectId);
            }
            if (assignment.RightFighterObjectId == (uint)fighter.ObjectId)
            {
                return GetNetworkObjectComponent<NetworkEntity>(assignment.LeftFighterObjectId);
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets the ally entity for any fighter in a combat
    /// </summary>
    /// <param name="fighter">The fighter to find an ally for</param>
    /// <returns>The ally entity, or null if not found</returns>
    public NetworkEntity GetAlly(NetworkEntity fighter)
    {
        if (fighter == null) return null;
        
        if (fighter.EntityType == EntityType.Player)
        {
            // Player's ally is their pet
            RelationshipManager relationshipManager = fighter.GetComponent<RelationshipManager>();
            if (relationshipManager?.AllyEntity != null)
            {
                return relationshipManager.AllyEntity.GetComponent<NetworkEntity>();
            }
        }
        else if (fighter.EntityType == EntityType.Pet)
        {
            // Pet's ally is their owner player
            return fighter.GetOwnerEntity();
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets the fight assignment for any entity involved in combat
    /// </summary>
    /// <param name="entity">The entity to find a fight assignment for</param>
    /// <returns>The fight assignment, or null if not found</returns>
    public FightAssignmentData? GetFight(NetworkEntity entity)
    {
        if (entity == null) return null;
        
        foreach (var assignment in fightAssignments)
        {
            if (assignment.LeftFighterObjectId == (uint)entity.ObjectId ||
                assignment.RightFighterObjectId == (uint)entity.ObjectId)
            {
                return assignment;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets all entities involved in a fight
    /// </summary>
    /// <param name="entity">Any entity involved in the fight</param>
    /// <returns>All entities in the fight [leftfighter, leftally, rightfighter, rightally]</returns>
    public List<NetworkEntity> GetFightEntities(NetworkEntity entity)
    {
        List<NetworkEntity> entities = new List<NetworkEntity>();
        
        var fight = GetFight(entity);
        if (!fight.HasValue) return entities;
        
        NetworkEntity leftFighter = GetNetworkObjectComponent<NetworkEntity>(fight.Value.LeftFighterObjectId);
        NetworkEntity rightFighter = GetNetworkObjectComponent<NetworkEntity>(fight.Value.RightFighterObjectId);
        
        if (leftFighter != null)
        {
            entities.Add(leftFighter);
            
            NetworkEntity leftAlly = GetAlly(leftFighter);
            if (leftAlly != null)
            {
                entities.Add(leftAlly);
            }
        }
        
        if (rightFighter != null)
        {
            entities.Add(rightFighter);
            
            NetworkEntity rightAlly = GetAlly(rightFighter);
            if (rightAlly != null)
            {
                entities.Add(rightAlly);
            }
        }
        
        return entities;
    }

    public FightAssignmentData? GetFightForConnection(NetworkConnection connection)
    {
        if (connection == null) return null;
        
        Debug.Log($"FLEXPAIRING: GetFightForConnection called for client {connection.ClientId}");
        
        // First try connection lookup for better performance
        if (connectionToFightMap.TryGetValue(connection.ClientId, out FightAssignmentData directMatch))
        {
            Debug.Log($"FLEXPAIRING: Found direct mapping for client {connection.ClientId} - Left: {directMatch.LeftFighterObjectId}, Right: {directMatch.RightFighterObjectId}");
            return directMatch;
        }
        
        Debug.Log($"FLEXPAIRING: No direct mapping found for client {connection.ClientId}, scanning assignments");
        
        // Fall back to assignment list scan - look for fights where this client owns a PLAYER entity
        foreach (var assignment in fightAssignments)
        {
            Debug.Log($"FLEXPAIRING: Checking assignment - Left: {assignment.LeftFighterObjectId}, Right: {assignment.RightFighterObjectId}");
            
            // Get the actual entities to check their types and owners
            NetworkEntity leftFighter = GetNetworkObjectComponent<NetworkEntity>(assignment.LeftFighterObjectId);
            NetworkEntity rightFighter = GetNetworkObjectComponent<NetworkEntity>(assignment.RightFighterObjectId);
            
            // Check if this client owns a Player entity in this fight
            if (leftFighter != null && leftFighter.EntityType == EntityType.Player && leftFighter.Owner != null && leftFighter.Owner.ClientId == connection.ClientId)
            {
                Debug.Log($"FLEXPAIRING: Found fight for client {connection.ClientId} via left Player entity {leftFighter.EntityName.Value}");
                return assignment;
            }
            
            if (rightFighter != null && rightFighter.EntityType == EntityType.Player && rightFighter.Owner != null && rightFighter.Owner.ClientId == connection.ClientId)
            {
                Debug.Log($"FLEXPAIRING: Found fight for client {connection.ClientId} via right Player entity {rightFighter.EntityName.Value}");
                return assignment;
            }
        }
        
        Debug.Log($"FLEXPAIRING: No fight found for client {connection.ClientId}");
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
    /// <param name="leftFighter">The left fighter whose fight you want to view</param>
    /// <returns>True if the fight was found and view was updated, false otherwise</returns>
    public bool SetViewedFight(NetworkEntity leftFighter)
    {
        if (leftFighter == null) 
        {
            Debug.Log("FightManager.SetViewedFight: Left Fighter is null");
            return false;
        }
        
        Debug.Log($"FightManager.SetViewedFight: Looking for fight for left fighter {leftFighter.EntityName.Value} (ID: {leftFighter.ObjectId})");
        
        var fightAssignment = GetFight(leftFighter);
        if (!fightAssignment.HasValue) 
        {
            Debug.Log($"FightManager.SetViewedFight: No fight assignment found for left fighter {leftFighter.EntityName.Value} (ID: {leftFighter.ObjectId})");
            return false;
        }
        
        Debug.Log($"FightManager.SetViewedFight: Found fight assignment - Left Fighter: {fightAssignment.Value.LeftFighterObjectId}, Right Fighter: {fightAssignment.Value.RightFighterObjectId}");
        
        var newViewedLeftFighter = GetNetworkObjectComponent<NetworkEntity>(fightAssignment.Value.LeftFighterObjectId);
        var newViewedRightFighter = GetNetworkObjectComponent<NetworkEntity>(fightAssignment.Value.RightFighterObjectId);
        
        Debug.Log($"FightManager.SetViewedFight: Resolved entities - Left Fighter: {(newViewedLeftFighter?.EntityName.Value ?? "null")} (ID: {newViewedLeftFighter?.ObjectId ?? 0}), Right Fighter: {(newViewedRightFighter?.EntityName.Value ?? "null")} (ID: {newViewedRightFighter?.ObjectId ?? 0})");
        
        viewedLeftFighter = newViewedLeftFighter;
        viewedRightFighter = newViewedRightFighter;
        
        Debug.Log($"FightManager.SetViewedFight: Set viewed combat references - ViewedLeftFighter: {(viewedLeftFighter?.EntityName.Value ?? "null")}, ViewedRightFighter: {(viewedRightFighter?.EntityName.Value ?? "null")}");
        Debug.Log($"HAND_FILTER: Viewed fight changed to LeftFighter: {(viewedLeftFighter?.EntityName.Value ?? "null")} (ID: {viewedLeftFighter?.ObjectId ?? 0}), RightFighter: {(viewedRightFighter?.EntityName.Value ?? "null")} (ID: {viewedRightFighter?.ObjectId ?? 0})");
        
        // Notify EntityVisibilityManager that the viewed fight has changed
        NotifyEntityVisibilityManagerReady();
        
        return true;
    }
    
    /// <summary>
    /// Gets all entities involved in the currently viewed fight
    /// </summary>
    /// <returns>All entities in the viewed fight [leftfighter, leftally, rightfighter, rightally]</returns>
    public List<NetworkEntity> GetViewedFightEntities()
    {
        if (viewedLeftFighter != null)
        {
            return GetFightEntities(viewedLeftFighter);
        }
        else if (viewedRightFighter != null)
        {
            return GetFightEntities(viewedRightFighter);
        }
        
        return new List<NetworkEntity>();
    }
    
    #endregion
}