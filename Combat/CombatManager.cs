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
    PetTurn
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

    // Track round numbers for each fight independently
    private readonly SyncDictionary<NetworkEntity, int> fightRounds = new SyncDictionary<NetworkEntity, int>();
    
    // Track active fights
    private readonly SyncDictionary<NetworkEntity, NetworkEntity> activeFights = new SyncDictionary<NetworkEntity, NetworkEntity>();
    
    // Track current turn for each fight
    private readonly SyncDictionary<NetworkEntity, CombatTurn> fightTurns = new SyncDictionary<NetworkEntity, CombatTurn>();
    
    // Track fight results
    private readonly SyncDictionary<NetworkEntity, bool> fightResults = new SyncDictionary<NetworkEntity, bool>(); // true if player won, false if pet won
    
    private void Awake()
    {
        if (combatCanvasManager == null) combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
        if (fightManager == null) fightManager = FindFirstObjectByType<FightManager>();
        if (draftSetup == null) draftSetup = FindFirstObjectByType<DraftSetup>();
    }

    [Server]
    public void StartCombat()
    {
        if (!IsServerInitialized) return;
        Debug.Log("CombatManager: Starting combat...");

        // Get all spawned entities
        List<NetworkEntity> entities = GetAllSpawnedEntities();
        Debug.Log($"CombatManager: Found {entities.Count} total entities");
        
        // Find players and their assigned opponent pets
        var players = entities.Where(e => e.EntityType == EntityType.Player);
        Debug.Log($"CombatManager: Found {players.Count()} players");

        if (players.Count() == 0)
        {
            Debug.LogError("CombatManager: No players found! Combat cannot start.");
            return;
        }

        foreach (var player in players)
        {
            Debug.Log($"CombatManager: Processing player {player.EntityName.Value}");
            NetworkEntity opponentPet = fightManager.GetOpponentForPlayer(player);
            if (opponentPet != null)
            {
                Debug.Log($"CombatManager: Found opponent pet {opponentPet.EntityName.Value} for player {player.EntityName.Value}");
                activeFights.Add(player, opponentPet);
                fightRounds.Add(player, 0); // Initialize round counter for this fight
                fightTurns.Add(player, CombatTurn.None); // Initialize turn state
                
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
                Debug.Log($"CombatManager: Starting first round for player {player.EntityName.Value}");
                StartNewRound(player);
            }
            else
            {
                Debug.LogError($"No opponent pet found for player {player.EntityName.Value}");
            }
        }

        Debug.Log("CombatManager: Combat initialization complete!");
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
        
        // Notify clients to refresh energy for their local fight
        RpcStartNewRound(player.ObjectId, pet.ObjectId, fightRounds[player]);

        // Draw cards for both entities in this specific fight
        Debug.Log($"CombatManager: Drawing cards for player {player.EntityName.Value}");
        DrawCardsForEntity(player);
        Debug.Log($"CombatManager: Drawing cards for pet {pet.EntityName.Value}");
        DrawCardsForEntity(pet);

        // Set turn to player for this fight
        Debug.Log($"CombatManager: Setting turn to PlayerTurn for fight: {player.EntityName.Value} vs {pet.EntityName.Value}");
        SetTurn(player, CombatTurn.PlayerTurn);
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

        HandManager handManager = GetHandManagerForEntity(entity);
        if (handManager != null)
        {
            Debug.Log($"CombatManager: Found HandManager for {entity.EntityName.Value}, calling DrawCards()");
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

        // Find the hand entity through RelationshipManager
        var relationshipManager = entity.GetComponent<RelationshipManager>();
        if (relationshipManager == null)
        {
            Debug.LogError($"CombatManager: No RelationshipManager found on entity {entity.EntityName.Value}");
            return null;
        }

        if (relationshipManager.HandEntity == null)
        {
            Debug.LogError($"CombatManager: No hand entity found for entity {entity.EntityName.Value}");
            return null;
        }

        var handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
        if (handEntity == null)
        {
            Debug.LogError($"CombatManager: Hand entity is not a valid NetworkEntity for entity {entity.EntityName.Value}");
            return null;
        }

        var handManager = handEntity.GetComponent<HandManager>();
        if (handManager == null)
        {
            Debug.LogError($"CombatManager: No HandManager found on hand entity for entity {entity.EntityName.Value}");
            return null;
        }

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
                        Debug.Log($"CombatManager: Called OnTurnEnd for pet {pet.EntityName.Value}");
                    }
                }
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
                StartPetTurn(player);
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

        Debug.Log($"CombatManager: Processing end turn request from connection {conn.ClientId}");

        // Find the player entity associated with this connection
        NetworkEntity playerEntity = GetPlayerEntityFromConnection(conn);
        if (playerEntity == null)
        {
            Debug.LogError($"CombatManager: Could not find player entity for connection {conn.ClientId}");
            return;
        }

        Debug.Log($"CombatManager: Found player entity {playerEntity.EntityName.Value} for end turn request");

        // Only allow ending turn if it's actually this player's turn
        if (!fightTurns.TryGetValue(playerEntity, out CombatTurn currentTurn))
        {
            Debug.LogError($"CombatManager: No turn state found for player {playerEntity.EntityName.Value}");
            return;
        }

        if (currentTurn != CombatTurn.PlayerTurn)
        {
            Debug.LogWarning($"CombatManager: Cannot end turn for {playerEntity.EntityName.Value} - not their turn (current turn: {currentTurn})");
            return;
        }

        Debug.Log($"CombatManager: Processing end turn for {playerEntity.EntityName.Value}");

        // Discard player's hand
        HandManager handManager = GetHandManagerForEntity(playerEntity);
        if (handManager != null)
        {
            Debug.Log($"CombatManager: Discarding hand for {playerEntity.EntityName.Value}");
            handManager.DiscardHand();
        }
        else
        {
            Debug.LogError($"CombatManager: No HandManager found for player {playerEntity.EntityName.Value}");
            return;
        }

        // Switch to pet turn for this specific fight
        Debug.Log($"CombatManager: Switching to pet turn for {playerEntity.EntityName.Value}");
        SetTurn(playerEntity, CombatTurn.PetTurn);
    }

    [Server]
    private void StartPetTurn(NetworkEntity player)
    {
        if (!activeFights.TryGetValue(player, out NetworkEntity pet)) return;

        PetCombatAI petAI = pet.GetComponent<PetCombatAI>();
        if (petAI != null)
        {
            StartCoroutine(HandlePetTurn(player, pet, petAI));
        }
    }

    [Server]
    private IEnumerator HandlePetTurn(NetworkEntity player, NetworkEntity pet, PetCombatAI petAI)
    {
        // Let the pet AI take its turn
        yield return StartCoroutine(petAI.TakeTurn());

        // Discard pet's remaining cards
        HandManager handManager = GetHandManagerForEntity(pet);
        if (handManager != null)
        {
            Debug.Log($"CombatManager: Pet {pet.EntityName.Value} discarding hand.");
            handManager.DiscardHand();
        }
        else
        {
            Debug.LogError($"CombatManager: No HandManager found for pet {pet.EntityName.Value} to discard hand.");
        }

        // Check if the fight is still active before starting a new round
        // (The fight might have ended during the pet's turn if someone died)
        if (!activeFights.ContainsKey(player))
        {
            Debug.Log($"CombatManager: Fight involving player {player.EntityName.Value} has ended, not starting new round.");
            yield break;
        }

        // Add a short pause before starting the new round
        Debug.Log($"CombatManager: Adding a 0.25s delay before starting new round for fight involving player {player.EntityName.Value}.");
        yield return new WaitForSeconds(0.25f);

        // Double-check that the fight is still active after the delay
        if (!activeFights.ContainsKey(player))
        {
            Debug.Log($"CombatManager: Fight involving player {player.EntityName.Value} ended during delay, not starting new round.");
            yield break;
        }

        // Start new round for this specific fight
        // Note: OnTurnEnd for the pet will be called automatically in SetTurn when transitioning from PetTurn to PlayerTurn
        Debug.Log($"CombatManager: Delay complete. Starting new round for fight involving player {player.EntityName.Value}.");
        StartNewRound(player);
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
        
        Debug.Log($"CombatManager: Handling death of {deadEntity.EntityName.Value} killed by {(killer != null ? killer.EntityName.Value : "unknown")}");
        
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
            
            // Notify clients about the fight end
            RpcNotifyFightEnded(player, pet, playerWon);
            
            Debug.Log($"CombatManager: Fight ended - {(playerWon ? player.EntityName.Value : pet.EntityName.Value)} won");
            
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
        Debug.Log($"CombatManager: Despawning all cards for fight between {player.EntityName.Value} and {pet.EntityName.Value}");
        
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
        
        Debug.Log($"CombatManager: Finished despawning cards for fight between {player.EntityName.Value} and {pet.EntityName.Value}");
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
    /// Checks if all fights are complete and transitions to draft phase if so
    /// </summary>
    [Server]
    private void CheckAllFightsComplete()
    {
        if (activeFights.Count == 0)
        {
            Debug.Log("CombatManager: All fights complete, transitioning to draft phase");
            TransitionToDraftPhase();
        }
        else
        {
            Debug.Log($"CombatManager: {activeFights.Count} fights still active");
        }
    }
    
    /// <summary>
    /// Transitions from combat to draft phase
    /// </summary>
    [Server]
    private void TransitionToDraftPhase()
    {
        Debug.Log("CombatManager: Starting transition to draft phase");
        
        if (draftSetup != null)
        {
            // Reset the draft setup state to allow a new draft to begin
            Debug.Log("CombatManager: Resetting draft setup for new draft round");
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