using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

/// <summary>
/// Manages the draft flow including pack circulation, card selection, and draft completion.
/// Attach to: A NetworkObject in the scene that coordinates the draft process.
/// </summary>
public class DraftManager : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private DraftCanvasManager draftCanvasManager;
    [SerializeField] private DraftPackSetup draftPackSetup;
    [SerializeField] private EntityVisibilityManager entityVisibilityManager;
    [SerializeField] private GameManager gameManager;
    
    // Track which players have completed the draft
    private readonly SyncDictionary<NetworkConnection, bool> playersReady = new SyncDictionary<NetworkConnection, bool>();
    
    // Track pack circulation queue for each player
    private readonly Dictionary<NetworkEntity, Queue<DraftPack>> playerPackQueues = new Dictionary<NetworkEntity, Queue<DraftPack>>();
    
    // Track current visible pack for each player
    private readonly Dictionary<NetworkEntity, DraftPack> currentVisiblePacks = new Dictionary<NetworkEntity, DraftPack>();
    
    private bool isDraftActive = false;
    private List<NetworkEntity> allPlayers = new List<NetworkEntity>();
    
    public bool IsDraftActive => isDraftActive;
    
    private void Awake()
    {
        ResolveReferences();
    }
    
    private void ResolveReferences()
    {
        if (draftCanvasManager == null) draftCanvasManager = FindFirstObjectByType<DraftCanvasManager>();
        if (draftPackSetup == null) draftPackSetup = FindFirstObjectByType<DraftPackSetup>();
        if (entityVisibilityManager == null) entityVisibilityManager = FindFirstObjectByType<EntityVisibilityManager>();
        if (gameManager == null) gameManager = GameManager.Instance;
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        playersReady.OnChange += OnPlayerReadyChanged;
    }
    
    public override void OnStopServer()
    {
        base.OnStopServer();
        playersReady.OnChange -= OnPlayerReadyChanged;
    }
    
    /// <summary>
    /// Starts the draft process
    /// </summary>
    [Server]
    public void StartDraft()
    {
        if (!IsServerInitialized)
        {
            Debug.LogError("DraftManager: Cannot start draft - server not initialized");
            return;
        }
        
        if (isDraftActive)
        {
            Debug.LogWarning("DraftManager: Draft is already active");
            return;
        }
        
        Debug.Log("DraftManager: Starting draft process");
        
        // Get all players
        allPlayers = GetAllPlayerEntities();
        if (allPlayers.Count == 0)
        {
            Debug.LogError("DraftManager: No players found to start draft");
            return;
        }
        
        // Clear ready states
        playersReady.Clear();
        
        // Initialize pack queues for each player
        InitializePackQueues();
        
        // Set up pack visibility
        SetupPackVisibility();
        
        isDraftActive = true;
        
        // Notify clients to set up draft UI
        RpcSetupDraftUI();
        
        Debug.Log($"DraftManager: Draft started with {allPlayers.Count} players");
    }
    
    /// <summary>
    /// Initializes the pack circulation queues for all players
    /// </summary>
    [Server]
    private void InitializePackQueues()
    {
        playerPackQueues.Clear();
        currentVisiblePacks.Clear();
        
        List<DraftPack> allPacks = draftPackSetup.GetAllActivePacks();
        
        // Create a queue for each player
        foreach (NetworkEntity player in allPlayers)
        {
            playerPackQueues[player] = new Queue<DraftPack>();
        }
        
        // Distribute packs to players (each player starts with their own pack)
        for (int i = 0; i < allPlayers.Count; i++)
        {
            NetworkEntity player = allPlayers[i];
            
            // Find the pack originally assigned to this player
            DraftPack playerPack = allPacks.FirstOrDefault(pack => pack.GetOriginalOwner() == player);
            if (playerPack != null)
            {
                playerPackQueues[player].Enqueue(playerPack);
                currentVisiblePacks[player] = playerPack;
                Debug.Log($"DraftManager: Assigned pack to player {player.EntityName.Value}");
            }
        }
        
        Debug.Log("DraftManager: Pack queues initialized");
    }
    
    /// <summary>
    /// Sets up pack visibility using the EntityVisibilityManager
    /// </summary>
    [Server]
    private void SetupPackVisibility()
    {
        if (entityVisibilityManager == null)
        {
            Debug.LogWarning("DraftManager: EntityVisibilityManager not found, cannot set pack visibility");
            return;
        }
        
        // Register all draft packs with the visibility manager
        List<DraftPack> allPacks = draftPackSetup.GetAllActivePacks();
        foreach (DraftPack pack in allPacks)
        {
            // For now, we'll handle visibility manually in the draft manager
            // The EntityVisibilityManager is more focused on entity visibility during combat
        }
        
        // Update pack visibility for each player
        UpdateAllPackVisibility();
    }
    
    /// <summary>
    /// Updates pack visibility for all players
    /// </summary>
    [Server]
    private void UpdateAllPackVisibility()
    {
        foreach (NetworkEntity player in allPlayers)
        {
            UpdatePackVisibilityForPlayer(player);
        }
    }
    
    /// <summary>
    /// Updates pack visibility for a specific player
    /// </summary>
    [Server]
    private void UpdatePackVisibilityForPlayer(NetworkEntity player)
    {
        if (!currentVisiblePacks.ContainsKey(player))
        {
            Debug.LogWarning($"DraftManager: No visible pack found for player {player.EntityName.Value}");
            return;
        }
        
        DraftPack visiblePack = currentVisiblePacks[player];
        
        // Notify the specific player about their visible pack
        RpcUpdatePackVisibility(player.Owner, visiblePack.ObjectId);
    }
    
    /// <summary>
    /// Called when a card is clicked during draft
    /// </summary>
    public void OnCardClicked(GameObject cardObject)
    {
        // Find the local player
        NetworkEntity localPlayer = GetLocalPlayer();
        if (localPlayer == null)
        {
            Debug.LogError("DraftManager: Cannot find local player for card selection");
            return;
        }
        
        // Show the card selection UI
        if (draftCanvasManager != null)
        {
            draftCanvasManager.ShowCardSelectionUI(cardObject);
        }
    }
    
    /// <summary>
    /// Handles card selection for a specific entity type
    /// </summary>
    public void SelectCard(GameObject cardObject, EntityType targetEntityType)
    {
        NetworkEntity localPlayer = GetLocalPlayer();
        if (localPlayer == null)
        {
            Debug.LogError("DraftManager: Cannot find local player for card selection");
            return;
        }
        
        Card cardComponent = cardObject.GetComponent<Card>();
        if (cardComponent == null || cardComponent.CardData == null)
        {
            Debug.LogError("DraftManager: Selected card has no Card component or CardData");
            return;
        }
        
        // Send selection to server
        ServerSelectCard(localPlayer.Owner, cardComponent.CardData.CardId, targetEntityType);
    }
    
    /// <summary>
    /// Server-side card selection handling
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void ServerSelectCard(NetworkConnection playerConnection, int cardId, EntityType targetEntityType)
    {
        if (!IsServerInitialized || !isDraftActive)
        {
            Debug.LogError("DraftManager: Cannot process card selection - server not initialized or draft not active");
            return;
        }
        
        // Find the player entity
        NetworkEntity player = GetPlayerEntityFromConnection(playerConnection);
        if (player == null)
        {
            Debug.LogError($"DraftManager: Could not find player entity for connection {playerConnection.ClientId}");
            return;
        }
        
        // Find the card object
        GameObject cardObject = FindCardObjectById(cardId);
        if (cardObject == null)
        {
            Debug.LogError($"DraftManager: Could not find card object with ID {cardId}");
            return;
        }
        
        // Process the card selection
        ProcessCardSelection(player, cardObject, targetEntityType);
    }
    
    /// <summary>
    /// Processes a card selection on the server
    /// </summary>
    [Server]
    private void ProcessCardSelection(NetworkEntity player, GameObject cardObject, EntityType targetEntityType)
    {
        Debug.Log($"DraftManager: Processing card selection for player {player.EntityName.Value}");
        
        // Find the target entity (player or their pet)
        NetworkEntity targetEntity = GetTargetEntity(player, targetEntityType);
        if (targetEntity == null)
        {
            Debug.LogError($"DraftManager: Could not find target entity of type {targetEntityType} for player {player.EntityName.Value}");
            return;
        }
        
        // Add card to target entity's deck
        NetworkEntityDeck targetDeck = targetEntity.GetComponent<NetworkEntityDeck>();
        if (targetDeck == null)
        {
            Debug.LogError($"DraftManager: Target entity {targetEntity.EntityName.Value} has no NetworkEntityDeck component");
            return;
        }
        
        Card cardComponent = cardObject.GetComponent<Card>();
        targetDeck.AddCard(cardComponent.CardData.CardId);
        
        Debug.Log($"DraftManager: Added card {cardComponent.CardData.CardName} to {targetEntity.EntityName.Value}'s deck");
        
        // Remove card from the pack
        RemoveCardFromPack(player, cardObject);
        
        // Move to next pack for this player
        MoveToNextPack(player);
        
        // Check if draft is complete
        CheckDraftCompletion();
    }
    
    /// <summary>
    /// Gets the target entity based on the player and entity type
    /// </summary>
    private NetworkEntity GetTargetEntity(NetworkEntity player, EntityType targetEntityType)
    {
        if (targetEntityType == EntityType.Player)
        {
            return player;
        }
        else if (targetEntityType == EntityType.Pet)
        {
            // Find the player's pet using RelationshipManager
            RelationshipManager relationshipManager = player.GetComponent<RelationshipManager>();
            if (relationshipManager != null && relationshipManager.AllyEntity != null)
            {
                return relationshipManager.AllyEntity.GetComponent<NetworkEntity>();
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Removes a card from its current pack
    /// </summary>
    [Server]
    private void RemoveCardFromPack(NetworkEntity player, GameObject cardObject)
    {
        if (!currentVisiblePacks.ContainsKey(player))
        {
            Debug.LogError($"DraftManager: No visible pack found for player {player.EntityName.Value}");
            return;
        }
        
        DraftPack currentPack = currentVisiblePacks[player];
        bool removed = currentPack.RemoveCard(cardObject);
        
        if (removed)
        {
            Debug.Log($"DraftManager: Removed card from pack for player {player.EntityName.Value}");
            
            // Despawn the card object
            NetworkObject cardNetObj = cardObject.GetComponent<NetworkObject>();
            if (cardNetObj != null && cardNetObj.IsSpawned)
            {
                FishNet.InstanceFinder.ServerManager.Despawn(cardNetObj);
            }
        }
        else
        {
            Debug.LogError($"DraftManager: Failed to remove card from pack for player {player.EntityName.Value}");
        }
    }
    
    /// <summary>
    /// Moves a player to their next pack in the queue
    /// </summary>
    [Server]
    private void MoveToNextPack(NetworkEntity player)
    {
        if (!playerPackQueues.ContainsKey(player))
        {
            Debug.LogError($"DraftManager: No pack queue found for player {player.EntityName.Value}");
            return;
        }
        
        Queue<DraftPack> playerQueue = playerPackQueues[player];
        
        // Remove the current pack from the queue
        if (playerQueue.Count > 0)
        {
            DraftPack currentPack = playerQueue.Dequeue();
            
            // Pass the pack to the next player
            PassPackToNextPlayer(currentPack, player);
        }
        
        // Set the next pack as visible for this player
        if (playerQueue.Count > 0)
        {
            DraftPack nextPack = playerQueue.Peek();
            currentVisiblePacks[player] = nextPack;
            UpdatePackVisibilityForPlayer(player);
            
            Debug.Log($"DraftManager: Player {player.EntityName.Value} moved to next pack");
        }
        else
        {
            // No more packs for this player
            currentVisiblePacks.Remove(player);
            Debug.Log($"DraftManager: Player {player.EntityName.Value} has no more packs");
        }
    }
    
    /// <summary>
    /// Passes a pack to the next player in the circulation
    /// </summary>
    [Server]
    private void PassPackToNextPlayer(DraftPack pack, NetworkEntity currentPlayer)
    {
        if (pack.IsEmpty)
        {
            Debug.Log($"DraftManager: Pack is empty, not passing to next player");
            return;
        }
        
        // Find the next player in the circulation
        int currentPlayerIndex = allPlayers.IndexOf(currentPlayer);
        int nextPlayerIndex = (currentPlayerIndex + 1) % allPlayers.Count;
        NetworkEntity nextPlayer = allPlayers[nextPlayerIndex];
        
        // Add the pack to the next player's queue
        if (playerPackQueues.ContainsKey(nextPlayer))
        {
            playerPackQueues[nextPlayer].Enqueue(pack);
            pack.SetCurrentOwner(nextPlayer);
            
            Debug.Log($"DraftManager: Passed pack from {currentPlayer.EntityName.Value} to {nextPlayer.EntityName.Value}");
            
            // If the next player doesn't have a visible pack, make this one visible
            if (!currentVisiblePacks.ContainsKey(nextPlayer))
            {
                currentVisiblePacks[nextPlayer] = pack;
                UpdatePackVisibilityForPlayer(nextPlayer);
            }
        }
    }
    
    /// <summary>
    /// Checks if the draft is complete
    /// </summary>
    [Server]
    private void CheckDraftCompletion()
    {
        bool allPacksEmpty = draftPackSetup.AreAllPacksEmpty();
        bool noVisiblePacks = currentVisiblePacks.Count == 0;
        
        if (allPacksEmpty || noVisiblePacks)
        {
            Debug.Log("DraftManager: Draft is complete");
            CompleteDraft();
        }
    }
    
    /// <summary>
    /// Completes the draft and transitions to the next phase
    /// </summary>
    [Server]
    private void CompleteDraft()
    {
        isDraftActive = false;
        
        // Notify all clients that draft is complete
        RpcDraftComplete();
        
        Debug.Log("DraftManager: Draft completed, waiting for players to continue");
    }
    
    /// <summary>
    /// Checks if a card is selectable by a specific player
    /// </summary>
    public bool IsCardSelectableByPlayer(GameObject cardObject, NetworkEntity player)
    {
        if (!isDraftActive || !currentVisiblePacks.ContainsKey(player))
        {
            return false;
        }
        
        DraftPack visiblePack = currentVisiblePacks[player];
        List<GameObject> packCards = visiblePack.GetCards();
        
        return packCards.Contains(cardObject);
    }
    
    /// <summary>
    /// Called when continue button is pressed
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void OnContinueButtonPressed(NetworkConnection playerConnection)
    {
        if (!IsServerInitialized)
        {
            Debug.LogError("DraftManager: Cannot process continue button - server not initialized");
            return;
        }
        
        Debug.Log($"DraftManager: Continue button pressed by connection {playerConnection.ClientId}");
        
        playersReady[playerConnection] = true;
        CheckAllPlayersReady();
    }
    
    /// <summary>
    /// Checks if all players are ready to continue
    /// </summary>
    [Server]
    private void CheckAllPlayersReady()
    {
        var connectedClientIds = FishNet.InstanceFinder.NetworkManager.ServerManager.Clients.Keys.ToList();
        
        bool allReady = true;
        foreach (int clientId in connectedClientIds)
        {
            if (FishNet.InstanceFinder.NetworkManager.ServerManager.Clients.TryGetValue(clientId, out NetworkConnection clientConn))
            {
                bool isClientReady = playersReady.ContainsKey(clientConn) && playersReady[clientConn];
                if (!isClientReady)
                {
                    allReady = false;
                    break;
                }
            }
        }
        
        if (allReady && connectedClientIds.Count > 0)
        {
            Debug.Log("DraftManager: All players ready, starting next combat round");
            StartNextCombatRound();
        }
    }
    
    /// <summary>
    /// Starts the next combat round
    /// </summary>
    [Server]
    private void StartNextCombatRound()
    {
        // Find and trigger CombatSetup to start a new combat round
        CombatSetup combatSetup = FindFirstObjectByType<CombatSetup>();
        if (combatSetup != null)
        {
            combatSetup.InitializeCombat();
        }
        else
        {
            Debug.LogError("DraftManager: Could not find CombatSetup to start next combat round");
        }
    }
    
    // RPC methods
    [ObserversRpc]
    private void RpcSetupDraftUI()
    {
        if (draftCanvasManager != null)
        {
            NetworkEntity localPlayer = GetLocalPlayer();
            if (localPlayer != null)
            {
                draftCanvasManager.Initialize(this, localPlayer);
                draftCanvasManager.EnableDraftCanvas();
                draftCanvasManager.UpdateDraftStatus("Draft in progress - select a card!");
            }
        }
    }
    
    [TargetRpc]
    private void RpcUpdatePackVisibility(NetworkConnection target, int visiblePackId)
    {
        // Handle pack visibility on the client side
        // For now, we'll just log this - the actual visibility will be handled by the UI
        Debug.Log($"DraftManager: Client {target.ClientId} should see pack {visiblePackId}");
    }
    
    [ObserversRpc]
    private void RpcDraftComplete()
    {
        if (draftCanvasManager != null)
        {
            draftCanvasManager.ShowDraftCompleteMessage();
        }
    }
    
    // Helper methods
    private void OnPlayerReadyChanged(SyncDictionaryOperation op, NetworkConnection key, bool value, bool asServer)
    {
        if (asServer)
        {
            Debug.Log($"DraftManager: Player {key.ClientId} ready state changed to {value}");
        }
    }
    
    private List<NetworkEntity> GetAllPlayerEntities()
    {
        return FishNet.InstanceFinder.NetworkManager.ServerManager.Objects.Spawned.Values
            .Select(nob => nob.GetComponent<NetworkEntity>())
            .Where(entity => entity != null && entity.EntityType == EntityType.Player)
            .ToList();
    }
    
    private NetworkEntity GetPlayerEntityFromConnection(NetworkConnection conn)
    {
        foreach (NetworkObject networkObject in FishNet.InstanceFinder.NetworkManager.ServerManager.Objects.Spawned.Values)
        {
            NetworkEntity entity = networkObject.GetComponent<NetworkEntity>();
            if (entity != null && entity.EntityType == EntityType.Player && networkObject.Owner == conn)
            {
                return entity;
            }
        }
        return null;
    }
    
    private NetworkEntity GetLocalPlayer()
    {
        NetworkEntity[] entities = FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        foreach (NetworkEntity entity in entities)
        {
            if (entity.EntityType == EntityType.Player && entity.IsOwner)
            {
                return entity;
            }
        }
        return null;
    }
    
    private GameObject FindCardObjectById(int cardId)
    {
        // This is a simplified approach - in practice, you might want to maintain a lookup table
        Card[] allCards = FindObjectsByType<Card>(FindObjectsSortMode.None);
        foreach (Card card in allCards)
        {
            if (card.CardId == cardId)
            {
                return card.gameObject;
            }
        }
        return null;
    }
} 