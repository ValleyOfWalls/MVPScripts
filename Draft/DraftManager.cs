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
    
    // Track the currently visible pack for each player (synchronized to clients)
    private readonly SyncDictionary<int, int> currentVisiblePacksByPlayerId = new SyncDictionary<int, int>();
    
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
        currentVisiblePacksByPlayerId.OnChange += OnCurrentVisiblePackChanged;
        Debug.Log("DraftManager: Server started, sync dictionary events registered");
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        playersReady.OnChange += OnPlayerReadyChanged;
        currentVisiblePacksByPlayerId.OnChange += OnCurrentVisiblePackChanged;
        Debug.Log("DraftManager: Client started, sync dictionary events registered");
    }
    
    public override void OnStopServer()
    {
        base.OnStopServer();
        playersReady.OnChange -= OnPlayerReadyChanged;
        currentVisiblePacksByPlayerId.OnChange -= OnCurrentVisiblePackChanged;
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        playersReady.OnChange -= OnPlayerReadyChanged;
        currentVisiblePacksByPlayerId.OnChange -= OnCurrentVisiblePackChanged;
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
        
        // Get all players
        allPlayers = GetAllPlayerEntities();
        if (allPlayers.Count == 0)
        {
            Debug.LogError("DraftManager: No players found to start draft");
            return;
        }
        
        // Clear ready states (with safety check)
        try
        {
            playersReady.Clear();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"DraftManager: Error clearing playersReady: {e.Message}");
            // Continue anyway - this shouldn't stop the draft
        }
        
        // Initialize pack queues for each player
        InitializePackQueues();
        
        // Set up pack visibility
        SetupPackVisibility();
        
        isDraftActive = true;
        
        // Notify clients to set up draft UI and activate draft state
        RpcSetupDraftUI();
        RpcActivateDraftOnClients();
    }
    
    /// <summary>
    /// Initializes the pack circulation queues for all players
    /// </summary>
    [Server]
    private void InitializePackQueues()
    {
        playerPackQueues.Clear();
        currentVisiblePacks.Clear();
        currentVisiblePacksByPlayerId.Clear();
        
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
                currentVisiblePacksByPlayerId[player.ObjectId] = playerPack.ObjectId;
            }
            else
            {
                Debug.LogError($"DraftManager: Could not find pack for player {player.EntityName.Value}");
            }
        }
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
            // Clear the synchronized visible pack if there's no visible pack
            currentVisiblePacksByPlayerId[player.ObjectId] = -1;
            return;
        }
        
        DraftPack visiblePack = currentVisiblePacks[player];
        
        // Update the synchronized dictionary so clients know which pack should be visible
        currentVisiblePacksByPlayerId[player.ObjectId] = visiblePack.ObjectId;
        
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
        
        NetworkObject cardNetObj = cardObject.GetComponent<NetworkObject>();
        if (cardNetObj == null)
        {
            Debug.LogError("DraftManager: Selected card has no NetworkObject component");
            return;
        }
        
        // Send selection to server using NetworkObject.ObjectId
        ServerSelectCard(localPlayer.Owner, cardNetObj.ObjectId, targetEntityType);
    }
    
    /// <summary>
    /// Server-side card selection handling
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void ServerSelectCard(NetworkConnection playerConnection, int cardObjectId, EntityType targetEntityType)
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
        
        // Find the card object by NetworkObject.ObjectId
        GameObject cardObject = FindCardObjectByNetworkObjectId(cardObjectId);
        if (cardObject == null)
        {
            Debug.LogError($"DraftManager: Could not find card object with NetworkObject.ObjectId {cardObjectId}");
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
        if (cardObject == null)
        {
            Debug.LogError("DraftManager: Cannot remove card - cardObject is null");
            return;
        }
        
        NetworkObject cardNetObj = cardObject.GetComponent<NetworkObject>();
        if (cardNetObj == null)
        {
            Debug.LogError("DraftManager: Card object has no NetworkObject component");
            return;
        }
        
        int cardObjectId = cardNetObj.ObjectId;
        
        // Search across all draft packs to find the one containing this card by ObjectId
        List<DraftPack> allPacks = draftPackSetup.GetAllActivePacks();
        DraftPack packContainingCard = null;
        
        foreach (DraftPack pack in allPacks)
        {
            // Check if this pack contains the card by ObjectId
            if (pack.ContainsCardWithObjectId(cardObjectId))
            {
                packContainingCard = pack;
                break;
            }
        }
        
        if (packContainingCard == null)
        {
            Debug.LogError($"DraftManager: Could not find card {cardObject.name} (ObjectId: {cardObjectId}) in any draft pack");
            return;
        }
        
        // Remove the card from the pack
        bool removed = packContainingCard.RemoveCard(cardObject);
        
        if (removed)
        {
            // Despawn the card object
            if (cardNetObj.IsSpawned)
            {
                FishNet.InstanceFinder.ServerManager.Despawn(cardNetObj);
            }
        }
        else
        {
            Debug.LogError($"DraftManager: Failed to remove card {cardObject.name} from pack {packContainingCard.name}");
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
        Debug.Log($"DraftManager: MoveToNextPack called for {player.EntityName.Value} (queue count: {playerQueue.Count})");
        
        // Remove the current pack from the queue
        if (playerQueue.Count > 0)
        {
            DraftPack currentPack = playerQueue.Dequeue();
            Debug.Log($"DraftManager: Removed pack {currentPack.name} from {player.EntityName.Value}'s queue");
            
            // Pass the pack to the next player
            PassPackToNextPlayer(currentPack, player);
        }
        
        // Set the next pack as visible for this player
        if (playerQueue.Count > 0)
        {
            DraftPack nextPack = playerQueue.Peek();
            currentVisiblePacks[player] = nextPack;
            UpdatePackVisibilityForPlayer(player);
            Debug.Log($"DraftManager: Next pack {nextPack.name} is now visible to {player.EntityName.Value}");
        }
        else
        {
            // No more packs for this player
            currentVisiblePacks.Remove(player);
            // Clear the synchronized visible pack
            currentVisiblePacksByPlayerId[player.ObjectId] = -1;
            Debug.Log($"DraftManager: No more packs for {player.EntityName.Value}");
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
            Debug.Log($"DraftManager: Not passing empty pack {pack.name} from {currentPlayer.EntityName.Value}");
            return;
        }
        
        // Find the next player in the circulation
        int currentPlayerIndex = allPlayers.IndexOf(currentPlayer);
        int nextPlayerIndex = (currentPlayerIndex + 1) % allPlayers.Count;
        NetworkEntity nextPlayer = allPlayers[nextPlayerIndex];
        
        Debug.Log($"DraftManager: Passing pack {pack.name} from {currentPlayer.EntityName.Value} to {nextPlayer.EntityName.Value}");
        
        // Add the pack to the next player's queue
        if (playerPackQueues.ContainsKey(nextPlayer))
        {
            playerPackQueues[nextPlayer].Enqueue(pack);
            pack.SetCurrentOwner(nextPlayer);
            
            Debug.Log($"DraftManager: Pack {pack.name} queued for {nextPlayer.EntityName.Value} (queue count: {playerPackQueues[nextPlayer].Count})");
            
            // If the next player doesn't have a visible pack, make this one visible immediately
            if (!currentVisiblePacks.ContainsKey(nextPlayer))
            {
                currentVisiblePacks[nextPlayer] = pack;
                UpdatePackVisibilityForPlayer(nextPlayer);
                Debug.Log($"DraftManager: Pack {pack.name} immediately visible to {nextPlayer.EntityName.Value} (no current visible pack)");
            }
            // If the next player has a visible pack but their queue only contains this new pack,
            // it means they should see this pack next (their current visible pack is about to be finished)
            else if (playerPackQueues[nextPlayer].Count == 1)
            {
                // The next player's current visible pack will be processed by MoveToNextPack
                // This ensures proper sequencing
                Debug.Log($"DraftManager: Pack {pack.name} queued for {nextPlayer.EntityName.Value} (will be visible after current pack)");
            }
            else
            {
                Debug.Log($"DraftManager: Pack {pack.name} queued for {nextPlayer.EntityName.Value} behind {playerPackQueues[nextPlayer].Count - 1} other packs");
            }
        }
        else
        {
            Debug.LogError($"DraftManager: No pack queue found for next player {nextPlayer.EntityName.Value}");
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
    }
    
    /// <summary>
    /// Updates initial draft pack visibility when draft starts
    /// Called from DraftSetup after draft manager starts
    /// </summary>
    public void UpdateInitialDraftPackVisibility()
    {
        if (entityVisibilityManager != null)
        {
            entityVisibilityManager.UpdateDraftPackVisibility();
        }
        else
        {
            Debug.LogWarning("DraftManager: EntityVisibilityManager not found, cannot update initial draft pack visibility");
        }
    }
    
    /// <summary>
    /// Checks if a card is selectable by a specific player
    /// </summary>
    public bool IsCardSelectableByPlayer(GameObject cardObject, NetworkEntity player)
    {
        if (!isDraftActive)
        {
            Debug.Log($"DraftManager: IsCardSelectableByPlayer - Draft not active");
            return false;
        }
        
        // On server, use the currentVisiblePacks dictionary for efficiency
        if (IsServerInitialized && currentVisiblePacks.ContainsKey(player))
        {
            DraftPack visiblePack = currentVisiblePacks[player];
            List<GameObject> packCards = visiblePack.GetCards();
            bool result = packCards.Contains(cardObject);
            Debug.Log($"DraftManager: IsCardSelectableByPlayer (Server) - Player {player.EntityName.Value}, Card {cardObject.name}, Pack {visiblePack.name}, Result: {result}");
            return result;
        }
        
        // Declare allPacks once for use in client-side logic
        DraftPack[] allPacks = FindObjectsByType<DraftPack>(FindObjectsSortMode.None);
        
        Debug.Log($"DraftManager: IsCardSelectableByPlayer (Client) - Player {player.EntityName.Value} (ID: {player.ObjectId}), Card {cardObject.name}");
        Debug.Log($"DraftManager: currentVisiblePacksByPlayerId contains player {player.ObjectId}: {currentVisiblePacksByPlayerId.ContainsKey(player.ObjectId)}");
        Debug.Log($"DraftManager: currentVisiblePacksByPlayerId count: {currentVisiblePacksByPlayerId.Count}");
        
        // Log all entries in the sync dictionary for debugging
        foreach (var kvp in currentVisiblePacksByPlayerId)
        {
            Debug.Log($"DraftManager: Sync dict entry - Player {kvp.Key}: Pack {kvp.Value}");
        }
        
        // On client, use the synchronized currentVisiblePacksByPlayerId to determine the visible pack
        if (currentVisiblePacksByPlayerId.ContainsKey(player.ObjectId))
        {
            int visiblePackId = currentVisiblePacksByPlayerId[player.ObjectId];
            Debug.Log($"DraftManager: Visible pack ID for player {player.ObjectId}: {visiblePackId}");
            
            if (visiblePackId == -1)
            {
                // No visible pack for this player
                Debug.Log($"DraftManager: No visible pack for player {player.ObjectId} (ID = -1)");
                return false;
            }
            
            // Find the pack with this ObjectId
            foreach (DraftPack pack in allPacks)
            {
                Debug.Log($"DraftManager: Checking pack {pack.name} with ObjectId {pack.ObjectId} against visible pack ID {visiblePackId}");
                if (pack.ObjectId == visiblePackId)
                {
                    List<GameObject> packCards = pack.GetCards();
                    bool result = packCards.Contains(cardObject);
                    Debug.Log($"DraftManager: Found matching pack {pack.name}, card {cardObject.name} selectable: {result}");
                    return result;
                }
            }
            Debug.Log($"DraftManager: No pack found with ObjectId {visiblePackId}");
        }
        else
        {
            Debug.Log($"DraftManager: currentVisiblePacksByPlayerId does not contain player {player.ObjectId}");
        }
        
        // Fallback: check pack ownership directly (this was the old behavior)
        Debug.Log($"DraftManager: Using fallback pack ownership check");
        foreach (DraftPack pack in allPacks)
        {
            if (pack.IsOwnedBy(player))
            {
                List<GameObject> packCards = pack.GetCards();
                bool result = packCards.Contains(cardObject);
                Debug.Log($"DraftManager: Fallback - Player owns pack {pack.name}, card {cardObject.name} selectable: {result}");
                return result;
            }
        }
        
        Debug.Log($"DraftManager: No selectable pack found for player {player.ObjectId}");
        return false;
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
            StartNextCombatRound();
        }
    }
    
    /// <summary>
    /// Starts the next combat round
    /// </summary>
    [Server]
    private void StartNextCombatRound()
    {
        // Clean up draft state
        CleanupDraftState();
        
        // Notify clients to disable draft UI and prepare for combat
        RpcTransitionToCombat();
        
        // Add a small delay to ensure clients have time to process the transition
        StartCoroutine(InitializeCombatWithDelay());
    }
    
    /// <summary>
    /// Initializes combat after a small delay to ensure proper transition
    /// </summary>
    [Server]
    private System.Collections.IEnumerator InitializeCombatWithDelay()
    {
        // Wait a bit for clients to process the transition
        yield return new WaitForSeconds(0.5f);
        
        // Find and trigger CombatSetup to start a new combat round
        CombatSetup combatSetup = FindFirstObjectByType<CombatSetup>();
        if (combatSetup != null)
        {
            // Reset combat setup state if needed
            if (combatSetup.IsSetupComplete)
            {
                combatSetup.EndCombat();
            }
            
            combatSetup.InitializeCombat();
        }
        else
        {
            Debug.LogError("DraftManager: Could not find CombatSetup to start next combat round");
        }
    }
    
    /// <summary>
    /// Cleans up draft state on the server
    /// </summary>
    [Server]
    private void CleanupDraftState()
    {
        // Clear pack queues and visibility
        playerPackQueues.Clear();
        currentVisiblePacks.Clear();
        currentVisiblePacksByPlayerId.Clear();
        
        // Clear ready states
        playersReady.Clear();
        
        // Reset draft state
        isDraftActive = false;
    }
    
    /// <summary>
    /// Notifies clients to transition from draft to combat
    /// </summary>
    [ObserversRpc]
    private void RpcTransitionToCombat()
    {
        // Update game phase to combat first
        GamePhaseManager gamePhaseManager = GamePhaseManager.Instance;
        if (gamePhaseManager != null)
        {
            gamePhaseManager.SetCombatPhase();
        }
        else
        {
            Debug.LogWarning("DraftManager: GamePhaseManager not found on client");
        }
        
        // Disable draft canvas
        if (draftCanvasManager != null)
        {
            draftCanvasManager.DisableDraftCanvas();
        }
        else
        {
            Debug.LogWarning("DraftManager: DraftCanvasManager not found on client");
        }
        
        // Update entity visibility manager to combat state
        if (entityVisibilityManager != null)
        {
            entityVisibilityManager.SetGameState(EntityVisibilityManager.GameState.Combat);
        }
        else
        {
            Debug.LogWarning("DraftManager: EntityVisibilityManager not found on client");
        }
        
        // Reset local draft state
        isDraftActive = false;
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
        // Update pack visibility on the client side using EntityVisibilityManager
        if (entityVisibilityManager != null)
        {
            entityVisibilityManager.UpdateDraftPackVisibility();
        }
        else
        {
            Debug.LogWarning($"DraftManager: EntityVisibilityManager not found, cannot update pack visibility for client {target.ClientId}");
        }
    }
    
    [ObserversRpc]
    private void RpcDraftComplete()
    {
        if (draftCanvasManager != null)
        {
            draftCanvasManager.ShowDraftCompleteMessage();
        }
    }
    
    [ObserversRpc]
    private void RpcActivateDraftOnClients()
    {
        // Activate draft on client
        isDraftActive = true;
        
        // Initialize client-side pack visibility
        if (entityVisibilityManager != null)
        {
            entityVisibilityManager.UpdateDraftPackVisibility();
        }
        else
        {
            Debug.LogWarning("DraftManager: EntityVisibilityManager not found on client");
        }
    }
    
    [ObserversRpc]
    private void RpcUpdateDraftPackVisibility()
    {
        // Update draft pack visibility on all clients
        EntityVisibilityManager entityVisibilityManager = FindFirstObjectByType<EntityVisibilityManager>();
        if (entityVisibilityManager != null)
        {
            // Ensure the EntityVisibilityManager is in Draft state
            entityVisibilityManager.SetGameState(EntityVisibilityManager.GameState.Draft);
            entityVisibilityManager.UpdateDraftPackVisibility();
        }
        else
        {
            Debug.LogWarning("DraftManager: EntityVisibilityManager not found on client for visibility update");
        }
    }
    
    // Helper methods
    private void OnPlayerReadyChanged(SyncDictionaryOperation op, NetworkConnection key, bool value, bool asServer)
    {
        // Event handler for player ready state changes
    }
    
    private void OnCurrentVisiblePackChanged(SyncDictionaryOperation op, int playerObjectId, int packObjectId, bool asServer)
    {
        // Event handler for current visible pack changes
        // This can be used for client-side UI updates if needed
        Debug.Log($"DraftManager: CurrentVisiblePack changed for player {playerObjectId} to pack {packObjectId} (operation: {op}, asServer: {asServer})");
        
        // On client, trigger visibility update when the sync data changes
        if (!asServer && entityVisibilityManager != null)
        {
            Debug.Log($"DraftManager: Triggering visibility update on client due to sync dictionary change");
            entityVisibilityManager.UpdateDraftPackVisibility();
        }
        else if (!asServer)
        {
            Debug.LogWarning($"DraftManager: Cannot trigger visibility update on client - entityVisibilityManager is null");
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
    
    private GameObject FindCardObjectByNetworkObjectId(int cardObjectId)
    {
        // Search for the card by NetworkObject.ObjectId
        NetworkObject netObj = null;
        
        if (FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(cardObjectId, out netObj))
        {
            return netObj.gameObject;
        }
        
        return null;
    }
} 