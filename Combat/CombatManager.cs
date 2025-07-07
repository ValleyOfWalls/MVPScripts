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

/// <summary>
/// Orchestrates the combat flow including turns, rounds, and fight progression.
/// Attach to: A persistent NetworkObject in the scene that manages combat gameplay.
/// </summary>
public class CombatManager : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private CombatCanvasManager combatCanvasManager;
    [SerializeField] private FightManager fightManager;
    [SerializeField] private DraftSetup draftSetup;
    [SerializeField] private FightConclusionManager fightConclusionManager;
    [SerializeField] private CombatCardQueue combatCardQueue;

    // Track round numbers for each fight independently
    private readonly SyncDictionary<NetworkEntity, int> fightRounds = new SyncDictionary<NetworkEntity, int>();
    
    // Track active fights
    private readonly SyncDictionary<NetworkEntity, NetworkEntity> activeFights = new SyncDictionary<NetworkEntity, NetworkEntity>();
    
    // Track current turn for each fight
    private readonly SyncDictionary<NetworkEntity, CombatTurn> fightTurns = new SyncDictionary<NetworkEntity, CombatTurn>();
    
    // Track fight results
    private readonly SyncDictionary<NetworkEntity, bool> fightResults = new SyncDictionary<NetworkEntity, bool>(); // true if player won, false if pet won
    
    // Track which players have pressed end turn this round
    private readonly SyncDictionary<NetworkEntity, bool> playersReadyToEndTurn = new SyncDictionary<NetworkEntity, bool>();
    
    // Track which clients have completed card execution
    private readonly SyncDictionary<NetworkConnection, bool> clientsCompletedCardExecution = new SyncDictionary<NetworkConnection, bool>();
    
    // Track which clients have completed hand discard
    private readonly SyncDictionary<NetworkConnection, bool> clientsCompletedHandDiscard = new SyncDictionary<NetworkConnection, bool>();
    
    // Client-side tracking
    private bool clientCardExecutionInProgress = false;
    private bool clientHandDiscardInProgress = false;
    
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
        /* Debug.Log("CombatManager: Starting combat..."); */

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
        /* Debug.Log($"CombatManager: Found {entities.Count} total entities"); */
        
        // Find players and their assigned opponent pets
        var players = entities.Where(e => e.EntityType == EntityType.Player);
        /* Debug.Log($"CombatManager: Found {players.Count()} players"); */

        if (players.Count() == 0)
        {
            Debug.LogError("CombatManager: No players found! Combat cannot start.");
            return;
        }

        foreach (var player in players)
        {
            /* Debug.Log($"CombatManager: Processing player {player.EntityName.Value}"); */
            NetworkEntity opponentPet = fightManager.GetOpponentForPlayer(player);
            if (opponentPet != null)
            {
                /* Debug.Log($"CombatManager: Found opponent pet {opponentPet.EntityName.Value} for player {player.EntityName.Value}"); */
                activeFights.Add(player, opponentPet);
                fightRounds.Add(player, 0); // Initialize round counter for this fight
                fightTurns.Add(player, CombatTurn.None); // Initialize turn state
                playersReadyToEndTurn.Add(player, false); // Initialize end turn state
                
                // Initialize entity trackers for new fight
                EntityTracker playerTracker = player.GetComponent<EntityTracker>();
                if (playerTracker != null)
                {
                    playerTracker.ResetForNewFight();
                }
                
                EntityTracker petTracker = opponentPet.GetComponent<EntityTracker>();
                if (petTracker != null)
                {
                    petTracker.ResetForNewFight();
                }
                
                // Start first round for this fight
                /* Debug.Log($"CombatManager: Starting first round for player {player.EntityName.Value}"); */
                StartNewRound(player);
            }
            else
            {
                Debug.LogError($"No opponent pet found for player {player.EntityName.Value}");
            }
        }

        /* Debug.Log("CombatManager: Combat initialization complete!"); */
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
            NetworkEntity player = fight.Key;
            NetworkEntity pet = fight.Value;
            
            if (player != null && pet != null)
            {
                Debug.Log($"CombatManager: Drawing initial cards for player {player.EntityName.Value}");
                DrawCardsForEntity(player);
                Debug.Log($"CombatManager: Drawing initial cards for pet {pet.EntityName.Value}");
                DrawCardsForEntity(pet);
                
                // Now that hands are drawn, queue cards for pets that are in SharedTurn
                if (fightTurns.TryGetValue(player, out CombatTurn currentTurn) && currentTurn == CombatTurn.SharedTurn)
                {
                    PetCombatAI petAI = pet.GetComponent<PetCombatAI>();
                    if (petAI != null)
                    {
                        Debug.Log($"CombatManager: Queuing cards for pet {pet.EntityName.Value} after initial hand draw");
                        petAI.QueueCardsForSharedTurn();
                    }
                }
            }
        }
        
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

    [Server]
    private void StartNewRound(NetworkEntity player)
    {
        if (!IsServerInitialized || player == null) 
        {
            Debug.LogError("CombatManager: Cannot start round - server not initialized or player is null");
            return;
        }

        // Get the pet for this player's fight
        if (!activeFights.TryGetValue(player, out NetworkEntity pet))
        {
            Debug.LogError($"CombatManager: Could not find pet for player {player.EntityName.Value} in active fights");
            return;
        }

        // Increment round counter for this specific fight
        if (fightRounds.ContainsKey(player))
        {
            fightRounds[player]++;
            Debug.Log($"CombatManager: Starting round {fightRounds[player]} for fight: {player.EntityName.Value} vs {pet.EntityName.Value}");
        }
        else
        {
            Debug.LogError($"CombatManager: Fight rounds dictionary doesn't contain entry for player {player.EntityName.Value}");
            return;
        }
        
        // Reset end turn ready state for new round
        playersReadyToEndTurn[player] = false;
        
        // Notify entity trackers about new round
        EntityTracker playerTracker = player.GetComponent<EntityTracker>();
        if (playerTracker != null)
        {
            playerTracker.ResetTurnData();
        }
        
        EntityTracker petTracker = pet.GetComponent<EntityTracker>();
        if (petTracker != null)
        {
            petTracker.ResetTurnData();
        }
        
        // Process start of round effects (Shield clearing, etc.) for both entities
        EffectHandler playerEffectHandler = player.GetComponent<EffectHandler>();
        if (playerEffectHandler != null)
        {
            playerEffectHandler.ProcessStartOfRoundEffects();
            Debug.Log($"CombatManager: Processed start of round effects for player {player.EntityName.Value}");
        }
        
        EffectHandler petEffectHandler = pet.GetComponent<EffectHandler>();
        if (petEffectHandler != null)
        {
            petEffectHandler.ProcessStartOfRoundEffects();
            Debug.Log($"CombatManager: Processed start of round effects for pet {pet.EntityName.Value}");
        }
        
        // Notify clients to refresh energy for their local fight
        RpcStartNewRound(player.ObjectId, pet.ObjectId, fightRounds[player]);

        // Draw cards for both entities - but only after the first round (initial hands are drawn separately)
        int currentRound = fightRounds[player];
        if (currentRound > 1)
        {
            Debug.Log($"CombatManager: Drawing cards for round {currentRound} - player {player.EntityName.Value}");
            DrawCardsForEntity(player);
            Debug.Log($"CombatManager: Drawing cards for round {currentRound} - pet {pet.EntityName.Value}");
            DrawCardsForEntity(pet);
            Debug.Log($"CombatManager: Finished drawing cards for round {currentRound}");
        }
        else
        {
            Debug.Log($"CombatManager: Skipping card draw for round {currentRound} (initial hands drawn separately)");
        }

        // Set turn to shared turn for this fight (both player and pet can act)
        /* Debug.Log($"CombatManager: Setting turn to SharedTurn for fight: {player.EntityName.Value} vs {pet.EntityName.Value}"); */
        SetTurn(player, CombatTurn.SharedTurn);
    }
    
    [ObserversRpc]
    private void RpcStartNewRound(int playerObjectId, int petObjectId, int roundNumber)
    {
        // Check if this is the local player's fight
        if (fightManager != null && 
            fightManager.ClientCombatPlayer != null && 
            fightManager.ClientCombatOpponentPet != null && 
            fightManager.ClientCombatPlayer.ObjectId == playerObjectId && 
            fightManager.ClientCombatOpponentPet.ObjectId == petObjectId)
        {
            Debug.Log($"CombatManager: Local fight round {roundNumber} started. Refreshing energy for local player and opponent pet.");
            
            // Refresh energy for both entities in the local fight
            RefreshEntityEnergy(fightManager.ClientCombatPlayer);
            RefreshEntityEnergy(fightManager.ClientCombatOpponentPet);
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
        if (fightTurns.ContainsKey(player))
        {
            CombatTurn previousTurn = fightTurns[player];
            fightTurns[player] = turn;
            RpcUpdateTurnUI(player, turn);

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
                if (activeFights.TryGetValue(player, out NetworkEntity pet))
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
                
                if (activeFights.TryGetValue(player, out NetworkEntity pet))
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
                if (activeFights.TryGetValue(player, out NetworkEntity pet))
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
                
                if (activeFights.TryGetValue(player, out NetworkEntity pet))
                {
                    EntityTracker petTracker = pet.GetComponent<EntityTracker>();
                    if (petTracker != null)
                    {
                        petTracker.OnTurnStart();
                    }
                    
                    // Pet cards will be queued after initial hands are drawn in DrawInitialHands()
                    // For subsequent rounds, queue cards immediately since hands are already present
                    int currentRound = fightRounds.TryGetValue(player, out int round) ? round : 0;
                    if (currentRound > 1)
                    {
                        PetCombatAI petAI = pet.GetComponent<PetCombatAI>();
                        if (petAI != null)
                        {
                            petAI.QueueCardsForSharedTurn();
                        }
                    }
                }
                
                Debug.Log($"CombatManager: Started shared turn for {player.EntityName.Value} and {pet?.EntityName.Value}");
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

        // Only allow ending turn if it's actually this player's turn
        if (!fightTurns.TryGetValue(playerEntity, out CombatTurn currentTurn))
        {
            Debug.LogError($"CombatManager: No turn state found for player {playerEntity.EntityName.Value}");
            return;
        }

        if (currentTurn != CombatTurn.PlayerTurn && currentTurn != CombatTurn.SharedTurn)
        {
            Debug.LogWarning($"CombatManager: Cannot end turn for {playerEntity.EntityName.Value} - not their turn (current turn: {currentTurn})");
            return;
        }

        // Mark this player as ready to end turn
        playersReadyToEndTurn[playerEntity] = true;
        Debug.Log($"CombatManager: Player {playerEntity.EntityName.Value} is ready to end turn");

        // Update UI to show waiting state
        RpcUpdatePlayerWaitingState(playerEntity, true);

        // Check if all players are ready to end turn
        CheckAllPlayersReadyToEndTurn();
    }

    /// <summary>
    /// Checks if all players are ready to end turn and executes if so
    /// </summary>
    [Server]
    private void CheckAllPlayersReadyToEndTurn()
    {
        // Check if all active players are ready
        bool allPlayersReady = true;
        foreach (var fight in activeFights)
        {
            NetworkEntity player = fight.Key;
            if (!playersReadyToEndTurn.TryGetValue(player, out bool isReady) || !isReady)
            {
                allPlayersReady = false;
                break;
            }
        }

        if (allPlayersReady)
        {
            Debug.Log("CombatManager: All players are ready to end turn, executing card plays");
            
            // Reset all players' ready states
            foreach (var fight in activeFights)
            {
                NetworkEntity player = fight.Key;
                playersReadyToEndTurn[player] = false;
                RpcUpdatePlayerWaitingState(player, false);
            }
            
            // Execute queued card plays for all fights simultaneously
            StartCoroutine(ExecuteAllFightsAndEndTurn());
        }
        else
        {
            Debug.Log("CombatManager: Waiting for more players to end turn");
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
            NetworkEntity player = fight.Key;
            if (player.Owner != null)
            {
                activeConnections.Add(player.Owner);
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
            NetworkEntity player = fight.Key;
            NetworkEntity pet = fight.Value;
            
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
            NetworkEntity player = fight.Key;
            if (activeFights.ContainsKey(player)) // Double-check fight is still active
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
            HandManager[] handManagers = FindObjectsOfType<HandManager>();
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
            if (activeFights.TryGetValue(player, out pet))
            {
                playerWon = false; // Pet won
            }
        }
        else if (deadEntity.EntityType == EntityType.Pet)
        {
            // Pet died, find the player they were fighting
            pet = deadEntity;
            player = GetPlayerFightingPet(pet);
            if (player != null)
            {
                playerWon = true; // Player won
            }
        }
        
        if (player != null && pet != null)
        {
            // Despawn all remaining cards for both entities in this fight
            DespawnAllCardsForFight(player, pet);
            
            // Record the fight result
            fightResults[player] = playerWon;
            
            // Remove the fight from active fights
            activeFights.Remove(player);
            fightRounds.Remove(player);
            fightTurns.Remove(player);
            playersReadyToEndTurn.Remove(player);
            
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
    private void DespawnAllCardsForFight(NetworkEntity player, NetworkEntity pet)
    {
        /* Debug.Log($"CombatManager: Despawning all cards for fight between {player.EntityName.Value} and {pet.EntityName.Value}"); */
        
        // Despawn player's cards
        HandManager playerHandManager = GetHandManagerForEntity(player);
        if (playerHandManager != null)
        {
            Debug.Log($"CombatManager: Despawning cards for player {player.EntityName.Value}");
            playerHandManager.DespawnAllCards();
        }
        else
        {
            Debug.LogWarning($"CombatManager: Player {player.EntityName.Value} has no HandManager component");
        }
        
        // Despawn pet's cards
        HandManager petHandManager = GetHandManagerForEntity(pet);
        if (petHandManager != null)
        {
            Debug.Log($"CombatManager: Despawning cards for pet {pet.EntityName.Value}");
            petHandManager.DespawnAllCards();
        }
        else
        {
            Debug.LogWarning($"CombatManager: Pet {pet.EntityName.Value} has no HandManager component");
        }
        
        /* Debug.Log($"CombatManager: Finished despawning cards for fight between {player.EntityName.Value} and {pet.EntityName.Value}"); */
    }
    
    /// <summary>
    /// Gets the player that is fighting against a specific pet
    /// </summary>
    [Server]
    private NetworkEntity GetPlayerFightingPet(NetworkEntity pet)
    {
        foreach (var fight in activeFights)
        {
            if (fight.Value == pet)
            {
                return fight.Key;
            }
        }
        return null;
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
            ShowFightConclusion();
        }
        else
        {
            Debug.Log($"CombatManager: {activeFights.Count} fights still active");
        }
    }
    
    /// <summary>
    /// Shows the fight conclusion screen with all fight results
    /// </summary>
    [Server]
    private void ShowFightConclusion()
    {
        /* Debug.Log("CombatManager: Starting fight conclusion display"); */
        
        if (fightConclusionManager != null)
        {
            // Pass the fight results to the conclusion manager
            fightConclusionManager.ShowFightConclusion(GetFightResults());
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
        /* Debug.Log("CombatManager: Starting direct transition to draft phase"); */
        
        if (draftSetup != null)
        {
            // Reset the draft setup state to allow a new draft to begin
            /* Debug.Log("CombatManager: Resetting draft setup for new draft round"); */
            draftSetup.ResetSetup();
            
            // Initialize the new draft
            Debug.Log("CombatManager: Initializing new draft round");
            draftSetup.InitializeDraft();
        }
        else
        {
            Debug.LogError("CombatManager: DraftSetup not found, cannot transition to draft phase");
        }
    }
    
    /// <summary>
    /// Gets the results of all completed fights
    /// </summary>
    public Dictionary<NetworkEntity, bool> GetFightResults()
    {
        Dictionary<NetworkEntity, bool> results = new Dictionary<NetworkEntity, bool>();
        foreach (var result in fightResults)
        {
            results[result.Key] = result.Value;
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