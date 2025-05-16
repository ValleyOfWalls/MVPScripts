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

    // Track round numbers for each fight independently
    private readonly SyncDictionary<NetworkEntity, int> fightRounds = new SyncDictionary<NetworkEntity, int>();
    
    // Track active fights
    private readonly SyncDictionary<NetworkEntity, NetworkEntity> activeFights = new SyncDictionary<NetworkEntity, NetworkEntity>();
    
    // Track current turn for each fight
    private readonly SyncDictionary<NetworkEntity, CombatTurn> fightTurns = new SyncDictionary<NetworkEntity, CombatTurn>();
    
    private void Awake()
    {
        if (combatCanvasManager == null) combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
        if (fightManager == null) fightManager = FindFirstObjectByType<FightManager>();
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

        // Draw cards for both entities in this specific fight
        Debug.Log($"CombatManager: Drawing cards for player {player.EntityName.Value}");
        DrawCardsForEntity(player);
        Debug.Log($"CombatManager: Drawing cards for pet {pet.EntityName.Value}");
        DrawCardsForEntity(pet);

        // Set turn to player for this fight
        Debug.Log($"CombatManager: Setting turn to PlayerTurn for fight: {player.EntityName.Value} vs {pet.EntityName.Value}");
        SetTurn(player, CombatTurn.PlayerTurn);
    }

    [Server]
    private void DrawCardsForEntity(NetworkEntity entity)
    {
        if (entity == null)
        {
            Debug.LogError("CombatManager: Attempted to draw cards for null entity");
            return;
        }

        HandManager handManager = entity.GetComponent<HandManager>();
        if (handManager != null)
        {
            Debug.Log($"CombatManager: Found HandManager for {entity.EntityName.Value}, calling DrawCards()");
            handManager.DrawCards();
        }
        else
        {
            Debug.LogError($"CombatManager: No HandManager found on entity {entity.EntityName.Value}");
        }
    }

    [Server]
    private void SetTurn(NetworkEntity player, CombatTurn turn)
    {
        if (!IsServerInitialized || player == null) return;

        // Update turn state for this specific fight
        if (fightTurns.ContainsKey(player))
        {
            fightTurns[player] = turn;
            RpcUpdateTurnUI(player, turn);

            if (turn == CombatTurn.PetTurn)
            {
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
        HandManager handManager = playerEntity.GetComponent<HandManager>();
        if (handManager != null)
        {
            Debug.Log($"CombatManager: Discarding hand for {playerEntity.EntityName.Value}");
            handManager.DiscardHand();
        }
        else
        {
            Debug.LogError($"CombatManager: No HandManager found on player {playerEntity.EntityName.Value}");
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
        HandManager handManager = pet.GetComponent<HandManager>();
        if (handManager != null)
        {
            Debug.Log($"CombatManager: Pet {pet.EntityName.Value} discarding hand.");
            handManager.DiscardHand();
        }
        else
        {
            Debug.LogError($"CombatManager: No HandManager found on pet {pet.EntityName.Value} to discard hand.");
        }

        // Add a short pause before starting the new round
        Debug.Log($"CombatManager: Adding a 0.25s delay before starting new round for fight involving player {player.EntityName.Value}.");
        yield return new WaitForSeconds(0.25f);

        // Start new round for this specific fight
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
} 