using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using FishNet.Managing;

public enum CombatTurn
{
    None,
    PlayerTurn,
    PetTurn,
    SharedTurn  // Both player and pet can act simultaneously
}

// Fight data structure - each fight has two participants
[System.Serializable]
public struct Fight
{
    public int fightId;
    public NetworkEntity leftEntity;
    public NetworkEntity rightEntity;
    public int round;
    public CombatTurn turn;
    public bool leftReady;
    public bool rightReady;
    public bool isActive;
    
    public Fight(int id, NetworkEntity left, NetworkEntity right)
    {
        fightId = id;
        leftEntity = left;
        rightEntity = right;
        round = 0;
        turn = CombatTurn.None;
        leftReady = false;
        rightReady = false;
        isActive = true;
    }
    
    public bool ContainsEntity(NetworkEntity entity)
    {
        return leftEntity == entity || rightEntity == entity;
    }
    
    public NetworkEntity GetOpponent(NetworkEntity entity)
    {
        if (leftEntity == entity) return rightEntity;
        if (rightEntity == entity) return leftEntity;
        return null;
    }
    
    public bool BothEntitiesReady()
    {
        return leftReady && rightReady;
    }
}

/// <summary>
/// Manages combat encounters between entities.
/// Handles multiple simultaneous fights in a fight-centric way.
/// </summary>
public class CombatManager : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private CombatCanvasManager combatCanvasManager;
    [SerializeField] private FightManager fightManager;
    [SerializeField] private DraftSetup draftSetup;
    [SerializeField] private FightConclusionManager fightConclusionManager;
    [SerializeField] private CombatCardQueue combatCardQueue;

    // Fight-centric data structures
    private readonly SyncDictionary<int, Fight> activeFights = new SyncDictionary<int, Fight>();
    private readonly SyncDictionary<int, bool> fightResults = new SyncDictionary<int, bool>(); // true if left entity won, false if right entity won
    private readonly Dictionary<int, Fight> completedFights = new Dictionary<int, Fight>(); // Store completed fight data for result conversion
    
    // Entity readiness tracking (applies to all entities regardless of type)
    private readonly SyncDictionary<NetworkEntity, bool> entitiesReadyToEndTurn = new SyncDictionary<NetworkEntity, bool>();
    
    // Client synchronization tracking
    private readonly SyncDictionary<NetworkConnection, bool> clientsCompletedCardExecution = new SyncDictionary<NetworkConnection, bool>();
    private readonly SyncDictionary<NetworkConnection, bool> clientsCompletedHandDiscard = new SyncDictionary<NetworkConnection, bool>();
    
    // Client-side tracking
    private bool clientCardExecutionInProgress = false;
    private bool clientHandDiscardInProgress = false;
    
    private int nextFightId = 1;
    
    private void Awake()
    {
        if (combatCanvasManager == null) combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
        if (fightManager == null) fightManager = FindFirstObjectByType<FightManager>();
        if (draftSetup == null) draftSetup = FindFirstObjectByType<DraftSetup>();
        if (fightConclusionManager == null) fightConclusionManager = FindFirstObjectByType<FightConclusionManager>();
        if (combatCardQueue == null) combatCardQueue = FindFirstObjectByType<CombatCardQueue>();
        
        // Subscribe to card execution events
        CombatCardQueue.OnCardExecutionStarted += OnClientCardExecutionStarted;
        CombatCardQueue.OnCardExecutionCompleted += OnClientCardExecutionCompleted;
        
        // If CombatCardQueue doesn't exist, create it
        if (combatCardQueue == null)
        {
            GameObject queueObject = new GameObject("CombatCardQueue");
            combatCardQueue = queueObject.AddComponent<CombatCardQueue>();
            // Add NetworkObject component for networking
            if (queueObject.GetComponent<NetworkObject>() == null)
            {
                queueObject.AddComponent<NetworkObject>();
            }
            Debug.Log("CombatManager: Created CombatCardQueue instance");
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        CombatCardQueue.OnCardExecutionStarted -= OnClientCardExecutionStarted;
        CombatCardQueue.OnCardExecutionCompleted -= OnClientCardExecutionCompleted;
    }
    
    /// <summary>
    /// Called when card execution starts on client
    /// </summary>
    private void OnClientCardExecutionStarted()
    {
        clientCardExecutionInProgress = true;
        Debug.Log("CombatManager (Client): Card execution started, tracking in progress");
    }
    
    /// <summary>
    /// Called when card execution completes on client
    /// </summary>
    private void OnClientCardExecutionCompleted()
    {
        clientCardExecutionInProgress = false;
        Debug.Log("CombatManager (Client): Card execution completed, no longer in progress");
    }

    [Server]
    public void StartCombat()
    {
        if (!IsServerInitialized) return;

        // Ensure CombatCardQueue is spawned as NetworkObject
        if (combatCardQueue != null && combatCardQueue.GetComponent<NetworkObject>() != null)
        {
            NetworkObject queueNetworkObject = combatCardQueue.GetComponent<NetworkObject>();
            if (!queueNetworkObject.IsSpawned)
            {
                NetworkManager.ServerManager.Spawn(queueNetworkObject);
                Debug.Log("CombatManager: Spawned CombatCardQueue as NetworkObject");
            }
        }

        // Get all spawned entities
        List<NetworkEntity> entities = GetAllSpawnedEntities();
        var players = entities.Where(e => e.EntityType == EntityType.Player).ToList();
        
        if (players.Count == 0)
        {
            Debug.LogError("CombatManager: No players found! Combat cannot start.");
            return;
        }
        
        if (players.Count % 2 != 0)
        {
            Debug.LogError($"CombatManager: Need even number of players for combat, found {players.Count}");
            return;
        }
        
        // Create all fights based on existing FightManager assignments
        var fightAssignments = fightManager.GetAllFightAssignments();
        Debug.Log($"CombatManager: Found {fightAssignments.Count} fight assignments from FightManager");
        
        foreach (var assignment in fightAssignments)
        {
            NetworkEntity leftEntity = GetNetworkEntityById(assignment.LeftFighterObjectId);
            NetworkEntity rightEntity = GetNetworkEntityById(assignment.RightFighterObjectId);
            
            if (leftEntity != null && rightEntity != null)
            {
                Debug.Log($"CombatManager: Creating fight: {leftEntity.EntityName.Value} vs {rightEntity.EntityName.Value}");
                CreateNewFight(leftEntity, rightEntity);
                
                // Create the ally fight
                NetworkEntity leftAlly = GetAllyForEntity(leftEntity);
                NetworkEntity rightAlly = GetAllyForEntity(rightEntity);
                
                if (leftAlly != null && rightAlly != null)
                {
                    Debug.Log($"CombatManager: Creating ally fight: {leftAlly.EntityName.Value} vs {rightAlly.EntityName.Value}");
                    CreateNewFight(leftAlly, rightAlly);
                }
                else
                {
                    Debug.LogWarning($"CombatManager: Could not create ally fight - LeftAlly: {leftAlly?.EntityName.Value ?? "NULL"}, RightAlly: {rightAlly?.EntityName.Value ?? "NULL"}");
                }
            }
            else
            {
                Debug.LogError($"CombatManager: Could not find entities for fight assignment - Left: {assignment.LeftFighterObjectId}, Right: {assignment.RightFighterObjectId}");
            }
        }
    }
    
    /// <summary>
    /// Draws initial hands for all active fights - called after loading screen is hidden
    /// </summary>
    [Server]
    public void DrawInitialHands()
    {
        if (!IsServerInitialized) return;
        
        Debug.Log("CombatManager: Drawing initial hands for all active fights");
        
        foreach (var fight in activeFights)
        {
            NetworkEntity leftEntity = fight.Value.leftEntity;
            NetworkEntity rightEntity = fight.Value.rightEntity;
            
            if (leftEntity != null && rightEntity != null)
            {
                // Draw cards for both entities in the fight
                DrawCardsForEntity(leftEntity);
                DrawCardsForEntity(rightEntity);
                
                // Draw cards for their allies
                NetworkEntity leftAlly = GetAllyForEntity(leftEntity);
                NetworkEntity rightAlly = GetAllyForEntity(rightEntity);
                
                if (leftAlly != null) DrawCardsForEntity(leftAlly);
                if (rightAlly != null) DrawCardsForEntity(rightAlly);
                
                // Queue cards for AI entities in SharedTurn fights
                if (fight.Value.turn == CombatTurn.SharedTurn)
                {
                    QueueCardsForAIEntity(leftEntity, 1);
                    QueueCardsForAIEntity(rightEntity, 1);
                    if (leftAlly != null) QueueCardsForAIEntity(leftAlly, 1);
                    if (rightAlly != null) QueueCardsForAIEntity(rightAlly, 1);
                }
            }
        }
        
        CheckAllEntitiesReadyToEndTurn();
        Debug.Log("CombatManager: Finished drawing initial hands for all fights");
    }

    private List<NetworkEntity> GetAllSpawnedEntities()
    {
        var entities = NetworkManager.ServerManager.Objects.Spawned.Values
            .Select(nob => nob.GetComponent<NetworkEntity>())
            .Where(e => e != null)
            .ToList();
        
        Debug.Log($"CombatManager: Found {entities.Count} entities in GetAllSpawnedEntities");
        return entities;
    }
    
    private NetworkEntity GetNetworkEntityById(uint objectId)
    {
        if (NetworkManager.ServerManager.Objects.Spawned.TryGetValue((int)objectId, out var networkObject))
        {
            return networkObject.GetComponent<NetworkEntity>();
        }
        return null;
    }
    
    /// <summary>
    /// Queues cards for an AI entity if it's a pet
    /// </summary>
    private void QueueCardsForAIEntity(NetworkEntity entity, int round)
    {
        if (entity == null || entity.EntityType != EntityType.Pet) return;
        
        PetCombatAI aiComponent = entity.GetComponent<PetCombatAI>();
        if (aiComponent != null)
        {
            Debug.Log($"AI_COMBAT_DEBUG: Queuing cards for AI entity {entity.EntityName.Value} (Round {round})");
            aiComponent.QueueCardsForSharedTurn();
            entitiesReadyToEndTurn[entity] = true;
        }
        else
        {
            Debug.LogError($"AI_COMBAT_DEBUG: No PetCombatAI component found on {entity.EntityName.Value}");
        }
    }
    
    /// <summary>
    /// Resets an entity's state for a new round
    /// </summary>
    private void ResetEntityForNewRound(NetworkEntity entity)
    {
        if (entity == null) return;
        
        // Reset entity tracker
        EntityTracker tracker = entity.GetComponent<EntityTracker>();
        if (tracker != null)
        {
            tracker.ResetTurnData();
        }
        
        // Reset AI state for pets
        if (entity.EntityType == EntityType.Pet)
        {
            PetCombatAI ai = entity.GetComponent<PetCombatAI>();
            if (ai != null)
            {
                ai.ResetTurnState();
            }
        }
    }
    
    /// <summary>
    /// Processes start of round effects for an entity
    /// </summary>
    private void ProcessStartOfRoundEffects(NetworkEntity entity)
    {
        if (entity == null) return;
        
        EffectHandler effectHandler = entity.GetComponent<EffectHandler>();
        if (effectHandler != null)
        {
            effectHandler.ProcessStartOfRoundEffects();
        }
    }
    
    /// <summary>
    /// Refreshes energy for an entity at the start of a round
    /// </summary>
    private void RefreshEntityEnergyForRound(NetworkEntity entity)
    {
        if (entity == null) return;
        
        EnergyHandler energyHandler = entity.GetComponent<EnergyHandler>();
        if (energyHandler != null)
        {
            energyHandler.ReplenishEnergy();
        }
    }

    [Server]
    private int CreateNewFight(NetworkEntity leftEntity, NetworkEntity rightEntity)
    {
        int fightId = nextFightId++;
        Fight newFight = new Fight(fightId, leftEntity, rightEntity);
        activeFights.Add(fightId, newFight);
        
        // Initialize entity readiness tracking
        entitiesReadyToEndTurn.Add(leftEntity, false);
        entitiesReadyToEndTurn.Add(rightEntity, false);
        
        // Initialize entity trackers
        EntityTracker leftTracker = leftEntity.GetComponent<EntityTracker>();
        if (leftTracker != null)
        {
            leftTracker.ResetForNewFight();
        }
        
        EntityTracker rightTracker = rightEntity.GetComponent<EntityTracker>();
        if (rightTracker != null)
        {
            rightTracker.ResetForNewFight();
        }
        
        Debug.Log($"CombatManager: Created new fight with ID {fightId} for {leftEntity.EntityName.Value} vs {rightEntity.EntityName.Value}");
        
        // Start the first round for this fight
        StartNewRound(leftEntity);
        
        return fightId;
    }

    [Server]
    private void StartNewRound(NetworkEntity entity)
    {
        if (!IsServerInitialized || entity == null) 
        {
            Debug.LogError("CombatManager: Cannot start round - server not initialized or entity is null");
            return;
        }

        // Find the fight this entity belongs to
        Fight currentFight = default;
        int fightId = -1;
        foreach (var fight in activeFights)
        {
            if (fight.Value.ContainsEntity(entity))
            {
                fightId = fight.Key;
                currentFight = fight.Value;
                currentFight.round++;
                activeFights[fightId] = currentFight;
                Debug.Log($"CombatManager: Starting round {currentFight.round} for fight: {currentFight.leftEntity.EntityName.Value} vs {currentFight.rightEntity.EntityName.Value}");
                break;
            }
        }
        
        if (fightId == -1)
        {
            Debug.LogError($"CombatManager: Could not find fight for entity {entity.EntityName.Value}");
            return;
        }

        NetworkEntity leftEntity = currentFight.leftEntity;
        NetworkEntity rightEntity = currentFight.rightEntity;
        
        // Reset turn state for both entities
        entitiesReadyToEndTurn[leftEntity] = false;
        entitiesReadyToEndTurn[rightEntity] = false;
        
        // Reset entity trackers and AI state
        ResetEntityForNewRound(leftEntity);
        ResetEntityForNewRound(rightEntity);
        
        // Process start of round effects for both entities
        ProcessStartOfRoundEffects(leftEntity);
        ProcessStartOfRoundEffects(rightEntity);
        
        // Refresh energy for both entities
        RefreshEntityEnergyForRound(leftEntity);
        RefreshEntityEnergyForRound(rightEntity);

        // Notify clients to refresh energy for their local fight
        RpcStartNewRound(leftEntity.ObjectId, rightEntity.ObjectId, fightId);

        // Draw cards for subsequent rounds (initial hands are drawn separately)
        if (currentFight.round > 1)
        {
            DrawCardsForEntity(leftEntity);
            DrawCardsForEntity(rightEntity);
            
            // Draw cards for allies
            NetworkEntity leftAlly = GetAllyForEntity(leftEntity);
            NetworkEntity rightAlly = GetAllyForEntity(rightEntity);
            
            if (leftAlly != null) DrawCardsForEntity(leftAlly);
            if (rightAlly != null) DrawCardsForEntity(rightAlly);
            
            Debug.Log($"CombatManager: Finished drawing cards for round {currentFight.round}");
        }

        SetTurn(entity, CombatTurn.SharedTurn);
    }
    
    [ObserversRpc]
    private void RpcStartNewRound(int playerObjectId, int petObjectId, int fightId)
    {

        // Check if we should refresh energy for this specific fight
        if (playerObjectId != -1 && petObjectId != -1 &&
            fightManager.ClientLeftFighter != null &&
            fightManager.ClientRightFighter != null &&
            fightManager.ClientLeftFighter.ObjectId == playerObjectId &&
            fightManager.ClientRightFighter.ObjectId == petObjectId)
        {
            // This is the client's active fight - refresh both entities
            Debug.Log($"Refreshing energy for client's fight: Player {playerObjectId} vs Opponent {petObjectId}");
            RefreshEntityEnergy(fightManager.ClientLeftFighter);
            RefreshEntityEnergy(fightManager.ClientRightFighter);
        }
    }
    
    private void RefreshEntityEnergy(NetworkEntity entity)
    {
        if (entity == null) return;
        
        EnergyHandler energyHandler = entity.GetComponent<EnergyHandler>();
        if (energyHandler != null)
        {
            Debug.Log($"CombatManager: Refreshing energy for {entity.EntityName.Value}");
            
            // Always request refresh energy for both player and opponent entities in local fights
            ClientRequestRefreshEnergy(entity.ObjectId);
        }
        else
        {
            Debug.LogError($"CombatManager: No EnergyHandler found on entity {entity.EntityName.Value}");
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void ClientRequestRefreshEnergy(int entityObjectId)
    {
        if (!IsServerInitialized) return;
        
        NetworkObject networkObject;
        if (NetworkManager.ServerManager.Objects.Spawned.TryGetValue(entityObjectId, out networkObject))
        {
            NetworkEntity entity = networkObject.GetComponent<NetworkEntity>();
            if (entity != null)
            {
                EnergyHandler energyHandler = entity.GetComponent<EnergyHandler>();
                if (energyHandler != null)
                {
                    Debug.Log($"CombatManager: Server refreshing energy for {entity.EntityName.Value}");
                    energyHandler.ReplenishEnergy();
                }
            }
        }
    }

    [Server]
    private void DrawCardsForEntity(NetworkEntity entity)
    {
        if (entity == null)
        {
            Debug.LogError("CombatManager: Attempted to draw cards for null entity");
            return;
        }

        Debug.Log($"CombatManager: DrawCardsForEntity called for {entity.EntityName.Value} (ID: {entity.ObjectId})");

        HandManager handManager = GetHandManagerForEntity(entity);
        if (handManager != null)
        {
            Debug.Log($"CombatManager: Found HandManager for {entity.EntityName.Value}, calling DrawCards() on HandManager instance {handManager.GetInstanceID()}");
            handManager.DrawCards();
        }
        else
        {
            Debug.LogError($"CombatManager: No HandManager found for entity {entity.EntityName.Value}");
        }
    }

    /// <summary>
    /// Gets the HandManager for an entity by finding its hand entity through RelationshipManager
    /// </summary>
    private HandManager GetHandManagerForEntity(NetworkEntity entity)
    {
        if (entity == null) return null;

        Debug.Log($"CombatManager: GetHandManagerForEntity called for {entity.EntityName.Value} (ID: {entity.ObjectId})");

        // Find the hand entity through RelationshipManager
        var relationshipManager = entity.GetComponent<RelationshipManager>();
        if (relationshipManager == null)
        {
            Debug.LogError($"CombatManager: No RelationshipManager found on entity {entity.EntityName.Value}");
            return null;
        }

        Debug.Log($"CombatManager: Found RelationshipManager for {entity.EntityName.Value}");

        if (relationshipManager.HandEntity == null)
        {
            Debug.LogError($"CombatManager: No hand entity found for entity {entity.EntityName.Value}");
            return null;
        }

        Debug.Log($"CombatManager: HandEntity found for {entity.EntityName.Value}");

        var handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
        if (handEntity == null)
        {
            Debug.LogError($"CombatManager: Hand entity is not a valid NetworkEntity for entity {entity.EntityName.Value}");
            return null;
        }

        Debug.Log($"CombatManager: Hand NetworkEntity found for {entity.EntityName.Value}: {handEntity.EntityName.Value} (ID: {handEntity.ObjectId})");

        var handManager = handEntity.GetComponent<HandManager>();
        if (handManager == null)
        {
            Debug.LogError($"CombatManager: No HandManager found on hand entity for entity {entity.EntityName.Value}");
            return null;
        }

        Debug.Log($"CombatManager: HandManager found for {entity.EntityName.Value} -> Hand entity: {handEntity.EntityName.Value} (ID: {handEntity.ObjectId}), HandManager object: {handManager.GetInstanceID()}");
        return handManager;
    }

    [Server]
    private void SetTurn(NetworkEntity player, CombatTurn turn)
    {
        if (!IsServerInitialized || player == null) return;

        // Update turn state for this specific fight
        int fightId = -1;
        Fight currentFight = default;
        foreach (var fight in activeFights)
        {
            if (fight.Value.leftEntity == player || fight.Value.rightEntity == player)
            {
                fightId = fight.Key;
                currentFight = fight.Value;
                break;
            }
        }
        
        if (fightId != -1 && currentFight.isActive)
        {
            CombatTurn previousTurn = currentFight.turn;
            currentFight.turn = turn;
            activeFights[fightId] = currentFight;
            RpcUpdateTurnUI(currentFight.leftEntity, turn);
            RpcUpdateTurnUI(currentFight.rightEntity, turn);

            // Process turn end for the entity whose turn is ending
            if (previousTurn == CombatTurn.PlayerTurn && turn != CombatTurn.PlayerTurn)
            {
                // Player's turn is ending
                EntityTracker playerTracker = player.GetComponent<EntityTracker>();
                if (playerTracker != null)
                {
                    playerTracker.OnTurnEnd();
                    Debug.Log($"CombatManager: Called OnTurnEnd for player {player.EntityName.Value}");
                }
            }
            else if (previousTurn == CombatTurn.PetTurn && turn != CombatTurn.PetTurn)
            {
                // Pet's turn is ending
                NetworkEntity pet = currentFight.GetOpponent(player);
                if (pet != null)
                {
                    EntityTracker petTracker = pet.GetComponent<EntityTracker>();
                    if (petTracker != null)
                    {
                        petTracker.OnTurnEnd();
                        /* Debug.Log($"CombatManager: Called OnTurnEnd for pet {pet.EntityName.Value}"); */
                    }
                }
            }
            else if (previousTurn == CombatTurn.SharedTurn && turn != CombatTurn.SharedTurn)
            {
                // Shared turn is ending - both player and pet end their turns
                EntityTracker playerTracker = player.GetComponent<EntityTracker>();
                if (playerTracker != null)
                {
                    playerTracker.OnTurnEnd();
                }
                
                NetworkEntity pet = currentFight.GetOpponent(player);
                if (pet != null)
                {
                    EntityTracker petTracker = pet.GetComponent<EntityTracker>();
                    if (petTracker != null)
                    {
                        petTracker.OnTurnEnd();
                    }
                }
                
                Debug.Log($"CombatManager: Called OnTurnEnd for both player and pet in shared turn");
            }

            // Process turn start for the entity whose turn is beginning
            if (turn == CombatTurn.PlayerTurn)
            {
                EntityTracker playerTracker = player.GetComponent<EntityTracker>();
                if (playerTracker != null)
                {
                    playerTracker.OnTurnStart();
                }
            }
            else if (turn == CombatTurn.PetTurn)
            {
                NetworkEntity pet = currentFight.GetOpponent(player);
                if (pet != null)
                {
                    EntityTracker petTracker = pet.GetComponent<EntityTracker>();
                    if (petTracker != null)
                    {
                        petTracker.OnTurnStart();
                    }
                }
            }
            else if (turn == CombatTurn.SharedTurn)
            {
                // Both player and pet start their turns simultaneously
                EntityTracker playerTracker = player.GetComponent<EntityTracker>();
                if (playerTracker != null)
                {
                    playerTracker.OnTurnStart();
                }
                
                NetworkEntity pet = currentFight.GetOpponent(player);
                if (pet != null)
                {
                    EntityTracker petTracker = pet.GetComponent<EntityTracker>();
                    if (petTracker != null)
                    {
                        petTracker.OnTurnStart();
                    }
                    
                    // AI cards will be queued after initial hands are drawn in DrawInitialHands()
                    // For subsequent rounds, queue cards immediately since hands are already present
                    int currentRound = currentFight.round;
                    Debug.Log($"AI_COMBAT_DEBUG: Setting SharedTurn for fight {currentFight.leftEntity.EntityName.Value} vs {currentFight.rightEntity.EntityName.Value} (Round {currentRound})");
                    
                    if (currentRound > 1)
                    {
                        Debug.Log($"AI_COMBAT_DEBUG: Round {currentRound} > 1, queueing cards immediately for AI entities");
                        
                        // Queue cards for the first entity if it's AI-controlled
                        if (player.EntityType == EntityType.Pet)
                        {
                            PetCombatAI playerAI = player.GetComponent<PetCombatAI>();
                            if (playerAI != null)
                            {
                                Debug.Log($"AI_COMBAT_DEBUG: Queuing cards for AI entity {player.EntityName.Value} in SetTurn (Round {currentRound})");
                                playerAI.QueueCardsForSharedTurn();
                                // Mark AI as ready to end turn after queuing cards
                                entitiesReadyToEndTurn[player] = true;
                                Debug.Log($"AI_COMBAT_DEBUG: Marked AI entity {player.EntityName.Value} as ready to end turn");
                            }
                            else
                            {
                                Debug.LogError($"AI_COMBAT_DEBUG: AI entity {player.EntityName.Value} missing PetCombatAI component!");
                            }
                        }
                        else
                        {
                            // Check if this player has an AI ally that should queue cards
                            NetworkEntity playerAlly = GetAllyForPlayer(player);
                            if (playerAlly != null && playerAlly.EntityType == EntityType.Pet)
                            {
                                PetCombatAI allyAI = playerAlly.GetComponent<PetCombatAI>();
                                if (allyAI != null)
                                {
                                    Debug.Log($"AI_COMBAT_DEBUG: Queuing cards for player ally {playerAlly.EntityName.Value} in SetTurn (Round {currentRound})");
                                    allyAI.QueueCardsForSharedTurn();
                                    // Mark AI as ready to end turn after queuing cards
                                    if (entitiesReadyToEndTurn.ContainsKey(playerAlly))
                                    {
                                        entitiesReadyToEndTurn[playerAlly] = true;
                                        Debug.Log($"AI_COMBAT_DEBUG: Marked AI ally {playerAlly.EntityName.Value} as ready to end turn");
                                    }
                                }
                                else
                                {
                                    Debug.LogError($"AI_COMBAT_DEBUG: No PetCombatAI component found on player ally {playerAlly.EntityName.Value}");
                                }
                            }
                        }
                        
                        // Queue cards for the second entity if it's AI-controlled
                        if (pet.EntityType == EntityType.Pet)
                        {
                            PetCombatAI petAI = pet.GetComponent<PetCombatAI>();
                            if (petAI != null)
                            {
                                Debug.Log($"AI_COMBAT_DEBUG: Queuing cards for AI entity {pet.EntityName.Value} in SetTurn (Round {currentRound})");
                                petAI.QueueCardsForSharedTurn();
                                // Mark AI as ready to end turn after queuing cards
                                entitiesReadyToEndTurn[pet] = true;
                                Debug.Log($"AI_COMBAT_DEBUG: Marked AI entity {pet.EntityName.Value} as ready to end turn");
                            }
                            else
                            {
                                Debug.LogError($"AI_COMBAT_DEBUG: AI entity {pet.EntityName.Value} missing PetCombatAI component!");
                            }
                        }
                        else
                        {
                            // Check if this player has an AI ally that should queue cards
                            NetworkEntity petAlly = GetAllyForPlayer(pet);
                            if (petAlly != null && petAlly.EntityType == EntityType.Pet)
                            {
                                PetCombatAI allyAI = petAlly.GetComponent<PetCombatAI>();
                                if (allyAI != null)
                                {
                                    Debug.Log($"AI_COMBAT_DEBUG: Queuing cards for player ally {petAlly.EntityName.Value} in SetTurn (Round {currentRound})");
                                    allyAI.QueueCardsForSharedTurn();
                                    // Mark AI as ready to end turn after queuing cards
                                    if (entitiesReadyToEndTurn.ContainsKey(petAlly))
                                    {
                                        entitiesReadyToEndTurn[petAlly] = true;
                                        Debug.Log($"AI_COMBAT_DEBUG: Marked AI ally {petAlly.EntityName.Value} as ready to end turn");
                                    }
                                }
                                else
                                {
                                    Debug.LogError($"AI_COMBAT_DEBUG: No PetCombatAI component found on player ally {petAlly.EntityName.Value}");
                                }
                            }
                        }
                        
                        // Check if this fight is AI vs AI and automatically trigger turn end
                        CheckAllEntitiesReadyToEndTurn();
                    }
                    else
                    {
                        Debug.Log($"AI_COMBAT_DEBUG: Round {currentRound} <= 1, AI cards will be queued in DrawInitialHands()");
                    }
                }
                
                Debug.Log($"PET_AI_DEBUG: Started shared turn for {player.EntityName.Value} and {pet?.EntityName.Value}");
            }
        }
    }

    [ObserversRpc]
    private void RpcUpdateTurnUI(NetworkEntity player, CombatTurn turn)
    {
        // Only update UI for the local player's fight
        if (player.IsOwner && combatCanvasManager != null)
        {
            combatCanvasManager.UpdateTurnUI(turn);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void OnEndTurnButtonPressed(NetworkConnection conn)
    {
        if (!IsServerInitialized)
        {
            Debug.LogError("CombatManager: Cannot process end turn - server not initialized");
            return;
        }

        /* Debug.Log($"CombatManager: Processing end turn request from connection {conn.ClientId}"); */

        // Find the player entity associated with this connection
        NetworkEntity playerEntity = GetPlayerEntityFromConnection(conn);
        if (playerEntity == null)
        {
            Debug.LogError($"CombatManager: Could not find player entity for connection {conn.ClientId}");
            return;
        }

        /* Debug.Log($"CombatManager: Found player entity {playerEntity.EntityName.Value} for end turn request"); */

        // Find the fight this player is in
        Fight playerFight = default;
        bool foundFight = false;
        foreach (var fight in activeFights)
        {
            if (fight.Value.leftEntity == playerEntity || fight.Value.rightEntity == playerEntity)
            {
                playerFight = fight.Value;
                foundFight = true;
                break;
            }
        }

        if (!foundFight)
        {
            Debug.LogWarning($"CombatManager: Cannot end turn for {playerEntity.EntityName.Value} - not in any active fight");
            return;
        }

        // Only allow ending turn if it's SharedTurn or the player's specific turn
        if (playerFight.turn != CombatTurn.SharedTurn && playerFight.turn != CombatTurn.PlayerTurn)
        {
            Debug.LogWarning($"CombatManager: Cannot end turn for {playerEntity.EntityName.Value} - not their turn (current turn: {playerFight.turn})");
            return;
        }

        // Mark this player as ready to end turn
        entitiesReadyToEndTurn[playerEntity] = true;
        Debug.Log($"AI_COMBAT_DEBUG: Player {playerEntity.EntityName.Value} pressed end turn and is now ready");

        // If this player has an AI ally, automatically mark the ally as ready too
        NetworkEntity playerAlly = GetAllyForPlayer(playerEntity);
        if (playerAlly != null && playerAlly.EntityType == EntityType.Pet)
        {
            if (entitiesReadyToEndTurn.ContainsKey(playerAlly))
            {
                entitiesReadyToEndTurn[playerAlly] = true;
                Debug.Log($"AI_COMBAT_DEBUG: Auto-marking AI ally {playerAlly.EntityName.Value} as ready to end turn");
            }
            else
            {
                Debug.LogWarning($"AI_COMBAT_DEBUG: Player {playerEntity.EntityName.Value} has ally {playerAlly.EntityName.Value} but ally not in entitiesReadyToEndTurn dictionary!");
            }
        }
        else
        {
            Debug.Log($"AI_COMBAT_DEBUG: Player {playerEntity.EntityName.Value} has no AI ally to auto-mark ready");
        }

        // Update UI to show waiting state
        RpcUpdatePlayerWaitingState(playerEntity, true);

        // Check if all entities are ready to end turn
        CheckAllEntitiesReadyToEndTurn();
    }

    /// <summary>
    /// Checks if all entities (players and AI) are ready to end turn and executes if so
    /// </summary>
    [Server]
    private void CheckAllEntitiesReadyToEndTurn()
    {
        Debug.Log($"AI_COMBAT_DEBUG: CheckAllEntitiesReadyToEndTurn called - checking {activeFights.Count} active fights");
        
        // Check if all entities in all active fights are ready
        bool allEntitiesReady = true;
        
        foreach (var fight in activeFights)
        {
            NetworkEntity leftEntity = fight.Value.leftEntity;
            NetworkEntity rightEntity = fight.Value.rightEntity;
            
            Debug.Log($"AI_COMBAT_DEBUG: Checking fight {leftEntity.EntityName.Value} ({leftEntity.EntityType}) vs {rightEntity.EntityName.Value} ({rightEntity.EntityType})");
            
            // Check if left entity is ready
            bool leftReady = false;
            if (leftEntity.EntityType == EntityType.Player)
            {
                leftReady = entitiesReadyToEndTurn.TryGetValue(leftEntity, out bool playerReady) && playerReady;
                Debug.Log($"AI_COMBAT_DEBUG: Player {leftEntity.EntityName.Value} ready: {leftReady}");
            }
            else if (leftEntity.EntityType == EntityType.Pet)
            {
                leftReady = entitiesReadyToEndTurn.TryGetValue(leftEntity, out bool aiReady) && aiReady;
                Debug.Log($"AI_COMBAT_DEBUG: AI entity {leftEntity.EntityName.Value} ready: {leftReady}");
            }
            
            // Check if right entity is ready
            bool rightReady = false;
            if (rightEntity.EntityType == EntityType.Player)
            {
                rightReady = entitiesReadyToEndTurn.TryGetValue(rightEntity, out bool playerReady) && playerReady;
                Debug.Log($"AI_COMBAT_DEBUG: Player {rightEntity.EntityName.Value} ready: {rightReady}");
            }
            else if (rightEntity.EntityType == EntityType.Pet)
            {
                rightReady = entitiesReadyToEndTurn.TryGetValue(rightEntity, out bool aiReady) && aiReady;
                Debug.Log($"AI_COMBAT_DEBUG: AI entity {rightEntity.EntityName.Value} ready: {rightReady}");
            }
            
            if (!leftReady || !rightReady)
            {
                allEntitiesReady = false;
                Debug.Log($"AI_COMBAT_DEBUG: Fight {leftEntity.EntityName.Value} vs {rightEntity.EntityName.Value} - Left Ready: {leftReady}, Right Ready: {rightReady} - NOT ALL READY");
                break;
            }
            else
            {
                Debug.Log($"AI_COMBAT_DEBUG: Fight {leftEntity.EntityName.Value} vs {rightEntity.EntityName.Value} - Both entities ready!");
            }
        }

        if (allEntitiesReady)
        {
            Debug.Log("AI_COMBAT_DEBUG: All entities are ready to end turn, executing card plays");
            
            // Reset all entities' ready states
            foreach (var fight in activeFights)
            {
                NetworkEntity leftEntity = fight.Value.leftEntity;
                NetworkEntity rightEntity = fight.Value.rightEntity;
                
                if (leftEntity.EntityType == EntityType.Player)
                {
                    entitiesReadyToEndTurn[leftEntity] = false;
                    RpcUpdatePlayerWaitingState(leftEntity, false);
                }
                else if (leftEntity.EntityType == EntityType.Pet)
                {
                    entitiesReadyToEndTurn[leftEntity] = false;
                }
                
                if (rightEntity.EntityType == EntityType.Player)
                {
                    entitiesReadyToEndTurn[rightEntity] = false;
                    RpcUpdatePlayerWaitingState(rightEntity, false);
                }
                else if (rightEntity.EntityType == EntityType.Pet)
                {
                    entitiesReadyToEndTurn[rightEntity] = false;
                }
            }
            
            // Execute queued card plays for all fights simultaneously
            StartCoroutine(ExecuteAllFightsAndEndTurn());
        }
        else
        {
            Debug.Log("AI_COMBAT_DEBUG: Waiting for more entities to end turn");
        }
    }

    /// <summary>
    /// Updates the UI to show player waiting state
    /// </summary>
    [ObserversRpc]
    private void RpcUpdatePlayerWaitingState(NetworkEntity player, bool isWaiting)
    {
        // Only update UI for the local player
        if (player.IsOwner && combatCanvasManager != null)
        {
            combatCanvasManager.UpdateWaitingForPlayersUI(isWaiting);
        }
    }

    /// <summary>
    /// Executes all queued card plays for all fights in global initiative order, then ends their shared turns
    /// </summary>
    [Server]
    private IEnumerator ExecuteAllFightsAndEndTurn()
    {
        Debug.Log("CombatManager: Starting turn end sequence - executing card plays for all active fights");
        
        // Reset client completion tracking
        clientsCompletedCardExecution.Clear();
        clientsCompletedHandDiscard.Clear();
        
        // Get all active connections for tracking
        var activeConnections = new List<NetworkConnection>();
        foreach (var fight in activeFights)
        {
            NetworkEntity player = fight.Value.leftEntity;
            if (player.Owner != null)
            {
                activeConnections.Add(player.Owner);
            }
            NetworkEntity pet = fight.Value.rightEntity;
            if (pet.Owner != null)
            {
                activeConnections.Add(pet.Owner);
            }
        }
        
        // Notify all clients that card execution is starting
        RpcStartCardExecution();
        
        // Execute all queued cards across all fights in initiative order
        if (CombatCardQueue.Instance != null)
        {
            yield return StartCoroutine(CombatCardQueue.Instance.ExecuteAllQueuedCardPlaysInInitiativeOrder());
        }
        else
        {
            Debug.LogError("CombatManager: CombatCardQueue.Instance is null!");
        }
        
        Debug.Log("CombatManager: Server card execution complete, notifying clients");
        
        // Notify all clients that server-side card execution is complete
        RpcServerCardExecutionComplete();
        
        // Wait for all clients to confirm they've finished processing card execution
        Debug.Log("CombatManager: Waiting for all clients to complete card execution processing");
        yield return StartCoroutine(WaitForAllClientsToCompleteCardExecution(activeConnections));
        
        Debug.Log("CombatManager: All clients completed card execution, starting hand discard");
        
        // Notify clients to start discarding hands
        RpcStartHandDiscard();
        
        // Discard hands for all entities in all fights
        foreach (var fight in activeFights)
        {
            NetworkEntity player = fight.Value.leftEntity;
            NetworkEntity pet = fight.Value.rightEntity;
            
            HandManager playerHandManager = GetHandManagerForEntity(player);
            if (playerHandManager != null)
            {
                playerHandManager.DiscardHand();
            }

            HandManager petHandManager = GetHandManagerForEntity(pet);
            if (petHandManager != null)
            {
                petHandManager.DiscardHand();
            }
        }
        
        Debug.Log("CombatManager: Server hand discard complete, notifying clients");
        
        // Notify clients that server-side hand discard is complete
        RpcServerHandDiscardComplete();
        
        // Wait for all clients to confirm they've finished processing hand discard
        Debug.Log("CombatManager: Waiting for all clients to complete hand discard processing");
        yield return StartCoroutine(WaitForAllClientsToCompleteHandDiscard(activeConnections));
        
        Debug.Log("CombatManager: All clients completed hand discard, starting new rounds");
        
        // Start new rounds for all fights
        foreach (var fight in activeFights.ToList()) // ToList to avoid modification during iteration
        {
            NetworkEntity player = fight.Value.leftEntity;
            if (activeFights.ContainsKey(fight.Key)) // Double-check fight is still active
            {
                StartNewRound(player);
            }
        }
        
        Debug.Log("CombatManager: Turn end sequence complete");
    }
    
    /// <summary>
    /// Waits for all clients to complete card execution processing
    /// </summary>
    [Server]
    private IEnumerator WaitForAllClientsToCompleteCardExecution(List<NetworkConnection> activeConnections)
    {
        while (true)
        {
            bool allClientsComplete = true;
            
            foreach (var connection in activeConnections)
            {
                if (!clientsCompletedCardExecution.ContainsKey(connection) || !clientsCompletedCardExecution[connection])
                {
                    allClientsComplete = false;
                    break;
                }
            }
            
            if (allClientsComplete)
            {
                Debug.Log("CombatManager: All clients have completed card execution processing");
                break;
            }
            
            yield return null; // Wait one frame and check again
        }
    }
    
    /// <summary>
    /// Waits for all clients to complete hand discard processing
    /// </summary>
    [Server]
    private IEnumerator WaitForAllClientsToCompleteHandDiscard(List<NetworkConnection> activeConnections)
    {
        while (true)
        {
            bool allClientsComplete = true;
            
            foreach (var connection in activeConnections)
            {
                if (!clientsCompletedHandDiscard.ContainsKey(connection) || !clientsCompletedHandDiscard[connection])
                {
                    allClientsComplete = false;
                    break;
                }
            }
            
            if (allClientsComplete)
            {
                Debug.Log("CombatManager: All clients have completed hand discard processing");
                break;
            }
            
            yield return null; // Wait one frame and check again
        }
    }
    
    /// <summary>
    /// Notifies all clients that card execution is starting
    /// </summary>
    [ObserversRpc]
    private void RpcStartCardExecution()
    {
        Debug.Log("CombatManager (Client): Card execution phase starting");
        // Clients can use this to prepare for card execution (e.g., disable UI)
    }
    
    /// <summary>
    /// Notifies all clients that server-side card execution is complete
    /// </summary>
    [ObserversRpc]
    private void RpcServerCardExecutionComplete()
    {
        Debug.Log("CombatManager (Client): Server card execution complete, processing client-side completion");
        
        // Give clients a moment to process any remaining card effects/animations
        StartCoroutine(ClientProcessCardExecutionCompletion());
    }
    
    /// <summary>
    /// Client-side coroutine to process card execution completion and notify server
    /// </summary>
    private IEnumerator ClientProcessCardExecutionCompletion()
    {
        Debug.Log("CombatManager (Client): Processing card execution completion");
        
        // Wait for card execution to actually complete (using event-based tracking)
        while (clientCardExecutionInProgress)
        {
            yield return null; // Wait one frame and check again
        }
        
        // Wait a few additional frames to ensure all effects and animations are processed
        yield return null;
        yield return null;
        yield return null;
        
        Debug.Log("CombatManager (Client): Client card execution processing complete, notifying server");
        
        // Notify server that this client has completed card execution processing
        ServerRpcClientCompletedCardExecution();
    }
    
    /// <summary>
    /// Notifies all clients to start hand discard
    /// </summary>
    [ObserversRpc]
    private void RpcStartHandDiscard()
    {
        Debug.Log("CombatManager (Client): Hand discard phase starting");
        clientHandDiscardInProgress = true;
        // Clients can use this to prepare for hand discard (e.g., update UI)
    }
    
    /// <summary>
    /// Notifies all clients that server-side hand discard is complete
    /// </summary>
    [ObserversRpc]
    private void RpcServerHandDiscardComplete()
    {
        Debug.Log("CombatManager (Client): Server hand discard complete, processing client-side completion");
        
        // Give clients a moment to process hand discard animations
        StartCoroutine(ClientProcessHandDiscardCompletion());
    }
    
    /// <summary>
    /// Client-side coroutine to process hand discard completion and notify server
    /// </summary>
    private IEnumerator ClientProcessHandDiscardCompletion()
    {
        Debug.Log("CombatManager (Client): Processing hand discard completion");
        
        // Wait for any ongoing discard animations to complete
        // Check if HandManager discard animations are still running
        int maxWaitFrames = 300; // Maximum 5 seconds at 60fps
        int frameCount = 0;
        
        while (clientHandDiscardInProgress && frameCount < maxWaitFrames)
        {
            // Check if any HandManager components are still processing discard animations
            bool anyDiscardInProgress = false;
            
            // Find all HandManager components and check if they're still discarding
            HandManager[] handManagers = FindObjectsByType<HandManager>(FindObjectsSortMode.None);
            foreach (HandManager handManager in handManagers)
            {
                if (handManager.IsDiscardInProgress)
                {
                    anyDiscardInProgress = true;
                    break;
                }
            }
            
            if (!anyDiscardInProgress)
            {
                Debug.Log("CombatManager (Client): All HandManager discard operations completed");
                break;
            }
            
            yield return null;
            frameCount++;
        }
        
        if (frameCount >= maxWaitFrames)
        {
            Debug.LogWarning("CombatManager (Client): Hand discard completion timed out, proceeding anyway");
        }
        
        // Mark hand discard as complete
        clientHandDiscardInProgress = false;
        
        // Wait a few additional frames to ensure all effects are processed
        yield return null;
        yield return null;
        yield return null;
        
        Debug.Log("CombatManager (Client): Client hand discard processing complete, notifying server");
        
        // Notify server that this client has completed hand discard processing
        ServerRpcClientCompletedHandDiscard();
    }
    
    /// <summary>
    /// Server RPC called by clients when they complete card execution processing
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void ServerRpcClientCompletedCardExecution(NetworkConnection sender = null)
    {
        if (sender != null)
        {
            clientsCompletedCardExecution[sender] = true;
            Debug.Log($"CombatManager: Client {sender.ClientId} completed card execution processing");
        }
    }
    
    /// <summary>
    /// Server RPC called by clients when they complete hand discard processing
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void ServerRpcClientCompletedHandDiscard(NetworkConnection sender = null)
    {
        if (sender != null)
        {
            clientsCompletedHandDiscard[sender] = true;
            Debug.Log($"CombatManager: Client {sender.ClientId} completed hand discard processing");
        }
    }



    

    private NetworkEntity GetPlayerEntityFromConnection(NetworkConnection conn)
    {
        // Search through spawned objects for the player entity with matching connection
        foreach (NetworkObject networkObject in NetworkManager.ServerManager.Objects.Spawned.Values)
        {
            NetworkEntity entity = networkObject.GetComponent<NetworkEntity>();
            if (entity != null && entity.EntityType == EntityType.Player && networkObject.Owner == conn)
            {
                return entity;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Gets the AI ally (pet) for a given player, if any
    /// </summary>
    private NetworkEntity GetAllyForPlayer(NetworkEntity player)
    {
        if (player == null || player.EntityType != EntityType.Player) return null;

        RelationshipManager relationshipManager = player.GetComponent<RelationshipManager>();
        if (relationshipManager?.AllyEntity != null)
        {
            return relationshipManager.AllyEntity.GetComponent<NetworkEntity>();
        }

        return null;
    }
    
    /// <summary>
    /// Gets the ally for any entity (player or pet)
    /// </summary>
    private NetworkEntity GetAllyForEntity(NetworkEntity entity)
    {
        if (entity == null) return null;
        
        if (entity.EntityType == EntityType.Player)
        {
            // For players, get their pet ally
            return GetAllyForPlayer(entity);
        }
        else if (entity.EntityType == EntityType.Pet)
        {
            // For pets, get their owner player as ally
            return entity.GetOwnerEntity();
        }
        
        return null;
    }
    
    /// <summary>
    /// Called when an entity dies during combat
    /// </summary>
    [Server]
    public void HandleEntityDeath(NetworkEntity deadEntity, NetworkEntity killer)
    {
        if (!IsServerInitialized)
        {
            Debug.LogError("CombatManager: Cannot handle entity death - server not initialized");
            return;
        }
        
        /* Debug.Log($"CombatManager: Handling death of {deadEntity.EntityName.Value} killed by {(killer != null ? killer.EntityName.Value : "unknown")}"); */
        
        // Find the fight this entity was involved in
        NetworkEntity player = null;
        NetworkEntity pet = null;
        bool playerWon = false;
        
        if (deadEntity.EntityType == EntityType.Player)
        {
            // Player died, find their opponent pet
            player = deadEntity;
            foreach (var fight in activeFights)
            {
                if (fight.Value.leftEntity == player)
                {
                    pet = fight.Value.rightEntity;
                    playerWon = false; // Pet won
                }
                else if (fight.Value.rightEntity == player)
                {
                    pet = fight.Value.leftEntity;
                    playerWon = true; // Player won
                }
                if (pet != null) break;
            }
        }
        else if (deadEntity.EntityType == EntityType.Pet)
        {
            // Pet died, find the player they were fighting
            pet = deadEntity;
            foreach (var fight in activeFights)
            {
                if (fight.Value.leftEntity == pet)
                {
                    player = fight.Value.rightEntity;
                }
                else if (fight.Value.rightEntity == pet)
                {
                    player = fight.Value.leftEntity;
                }
                if (player != null) break;
            }
        }
        
        if (player != null && pet != null)
        {
            // Notify the card queue to remove any remaining cards for this fight
            if (CombatCardQueue.Instance != null)
            {
                CombatCardQueue.Instance.RemoveCardsForEndedFight(player.ObjectId, pet.ObjectId);
            }
            
            // Despawn all remaining cards for both entities in this fight
            DespawnAllCardsForFight(player, pet);
            
            // Find the fight and record the result
            int completedFightId = -1;
            Fight completedFight = default;
            foreach (var fight in activeFights)
            {
                if (fight.Value.leftEntity == player || fight.Value.rightEntity == player)
                {
                    completedFightId = fight.Key;
                    completedFight = fight.Value;
                    break;
                }
            }
            
            if (completedFightId != -1)
            {
                // Record the fight result
                fightResults[completedFightId] = playerWon;
                
                // Store the completed fight data for result conversion
                completedFights[completedFightId] = completedFight;
                
                // Remove the fight from active fights
                activeFights.Remove(completedFightId);
            }
            
            // Notify clients about the fight end
            RpcNotifyFightEnded(player, pet, playerWon);
            
            /* Debug.Log($"CombatManager: Fight ended - {(playerWon ? player.EntityName.Value : pet.EntityName.Value)} won"); */
            
            // Check if all fights are complete
            CheckAllFightsComplete();
        }
        else
        {
            Debug.LogWarning($"CombatManager: Could not determine fight participants for dead entity {deadEntity.EntityName.Value}");
        }
    }
    
    /// <summary>
    /// Despawns all remaining cards for both entities in a fight
    /// </summary>
    [Server]
    private void DespawnAllCardsForFight(NetworkEntity leftEntity, NetworkEntity rightEntity)
    {
        DespawnCardsForEntity(leftEntity);
        DespawnCardsForEntity(rightEntity);
    }
    
    /// <summary>
    /// Despawns all cards for a single entity
    /// </summary>
    private void DespawnCardsForEntity(NetworkEntity entity)
    {
        if (entity == null) return;
        
        HandManager handManager = GetHandManagerForEntity(entity);
        if (handManager != null)
        {
            handManager.DespawnAllCards();
        }
        else
        {
            Debug.LogWarning($"CombatManager: Entity {entity.EntityName.Value} has no HandManager component");
        }
    }
    

    
    /// <summary>
    /// Checks if all fights are complete and transitions to fight conclusion if so
    /// </summary>
    [Server]
    private void CheckAllFightsComplete()
    {
        if (activeFights.Count == 0)
        {
            Debug.Log("CombatManager: All fights complete, showing fight conclusion");
            
            // Clear any remaining queued cards since all fights are over
            if (CombatCardQueue.Instance != null)
            {
                CombatCardQueue.Instance.ClearAllQueuedCardPlays();
            }
            
            ShowFightConclusion();
        }
        else
        {
            Debug.Log($"CombatManager: {activeFights.Count} fights still active");
        }
    }

    /// <summary>
    /// Checks if a fight is still active for the given entity
    /// </summary>
    [Server]
    public bool IsFightActive(int entityId)
    {
        if (!IsServerInitialized) return false;
        
        Debug.Log($"AI_COMBAT_DEBUG: IsFightActive check for entity {entityId}");
        
        // Check if the entity is in an active fight (includes both player vs player and AI ally vs AI ally fights)
        foreach (var fight in activeFights)
        {
            if (fight.Value.leftEntity.ObjectId == entityId || fight.Value.rightEntity.ObjectId == entityId)
            {
                Debug.Log($"AI_COMBAT_DEBUG: Entity {entityId} found in active fights - fight is active");
                return true;
            }
        }
        
        Debug.Log($"AI_COMBAT_DEBUG: Entity {entityId} not found in any active fights");
        return false;
    }
    
    /// <summary>
    /// Shows the fight conclusion screen with all fight results
    /// </summary>
    [Server]
    private void ShowFightConclusion()
    {
        if (fightConclusionManager != null)
        {
            fightConclusionManager.ShowFightConclusion(GetFightResultsByEntity());
            Debug.Log("CombatManager: Fight conclusion started successfully");
        }
        else
        {
            Debug.LogError("CombatManager: FightConclusionManager not found, falling back to direct draft transition");
            TransitionToDraftPhaseDirectly();
        }
    }
    
    /// <summary>
    /// Fallback method to transition directly to draft phase (used if conclusion manager is missing)
    /// </summary>
    [Server]
    private void TransitionToDraftPhaseDirectly()
    {
        if (draftSetup != null)
        {
            draftSetup.ResetSetup();
            Debug.Log("CombatManager: Initializing new draft round");
            draftSetup.InitializeDraft();
        }
        else
        {
            Debug.LogError("CombatManager: DraftSetup not found, cannot transition to draft phase");
        }
    }
    
    /// <summary>
    /// Gets the results of all completed fights by fight ID
    /// </summary>
    public Dictionary<int, bool> GetFightResults()
    {
        Dictionary<int, bool> results = new Dictionary<int, bool>();
        foreach (var result in fightResults)
        {
            results[result.Key] = result.Value;
        }
        return results;
    }
    
    /// <summary>
    /// Gets the results of all completed fights by left entity (for legacy compatibility)
    /// </summary>
    public Dictionary<NetworkEntity, bool> GetFightResultsByEntity()
    {
        Dictionary<NetworkEntity, bool> results = new Dictionary<NetworkEntity, bool>();
        
        // Convert fight results back to entity-based format using completed fight data
        foreach (var fightResult in fightResults)
        {
            int fightId = fightResult.Key;
            bool leftEntityWon = fightResult.Value;
            
            if (completedFights.TryGetValue(fightId, out Fight completedFight))
            {
                // Use the left entity as the key (maintaining the old convention)
                results[completedFight.leftEntity] = leftEntityWon;
            }
        }
        
        return results;
    }
    
    [ObserversRpc]
    private void RpcNotifyFightEnded(NetworkEntity player, NetworkEntity pet, bool playerWon)
    {
        // Check if this is the local player's fight by checking if the player entity is owned by this client
        bool isLocalPlayerFight = (player != null && player.IsOwner);
        
        // Notify clients about the fight end
        if (combatCanvasManager != null)
        {
            combatCanvasManager.ShowFightEndedPanel(player, pet, !playerWon);
            
            // Only disable the end turn button if this is specifically the local player's fight
            if (isLocalPlayerFight)
            {
                combatCanvasManager.OnLocalFightEnded();
                Debug.Log($"CombatManager: Local player {player.EntityName.Value}'s fight ended - disabling end turn button");
            }
        }
        
        Debug.Log($"CombatManager: Client notified - Fight ended, {(playerWon ? player.EntityName.Value : pet.EntityName.Value)} won");
    }
} 