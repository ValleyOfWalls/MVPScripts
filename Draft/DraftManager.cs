using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

/// <summary>
/// Debug information for tracking pack circulation in the inspector
/// </summary>
[System.Serializable]
public class PackDebugInfo
{
    [SerializeField] public DraftPack pack;
    [SerializeField] public string packName;
    [SerializeField] public string originalOwner;
    [SerializeField] public string currentOwner;
    [SerializeField] public int queuePosition;
    [SerializeField] public bool isCurrentlyVisible;
    
    public PackDebugInfo(DraftPack pack, string currentOwner, int queuePosition, bool isCurrentlyVisible)
    {
        this.pack = pack;
        this.packName = pack != null ? pack.name : "NULL";
        this.originalOwner = pack?.GetOriginalOwner()?.EntityName?.Value ?? "Unknown";
        this.currentOwner = currentOwner;
        this.queuePosition = queuePosition;
        this.isCurrentlyVisible = isCurrentlyVisible;
    }
    
    // Constructor for network-safe version (without pack reference)
    public PackDebugInfo(string packName, string originalOwner, string currentOwner, int queuePosition, bool isCurrentlyVisible)
    {
        this.pack = null; // Will be null on clients
        this.packName = packName;
        this.originalOwner = originalOwner;
        this.currentOwner = currentOwner;
        this.queuePosition = queuePosition;
        this.isCurrentlyVisible = isCurrentlyVisible;
    }
}

/// <summary>
/// Network-safe debug info for RPC transmission (no object references)
/// </summary>
[System.Serializable]
public struct NetworkPackDebugInfo
{
    public string packName;
    public string originalOwner;
    public string currentOwner;
    public int queuePosition;
    public bool isCurrentlyVisible;
    
    public NetworkPackDebugInfo(PackDebugInfo debugInfo)
    {
        this.packName = debugInfo.packName;
        this.originalOwner = debugInfo.originalOwner;
        this.currentOwner = debugInfo.currentOwner;
        this.queuePosition = debugInfo.queuePosition;
        this.isCurrentlyVisible = debugInfo.isCurrentlyVisible;
    }
}

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
    
    [Header("Debug Info - Pack Circulation")]
    [SerializeField] private List<PackDebugInfo> packCirculationDebug = new List<PackDebugInfo>();
    [SerializeField] private bool autoUpdateDebugInfo = true;
    
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
        Debug.Log($"DraftManager.InitializePackQueues: Found {allPacks.Count} active packs");
        Debug.Log($"DraftManager.InitializePackQueues: Found {allPlayers.Count} players");
        
        // Create a queue for each player
        foreach (NetworkEntity player in allPlayers)
        {
            playerPackQueues[player] = new Queue<DraftPack>();
            Debug.Log($"DraftManager.InitializePackQueues: Created queue for player {player.EntityName.Value}");
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
                Debug.Log($"DraftManager: Assigned pack {playerPack.name} to player {player.EntityName.Value}");
            }
            else
            {
                Debug.LogError($"DraftManager: Could not find pack for player {player.EntityName.Value}");
            }
        }
        
        Debug.Log("DraftManager: Pack queues initialized");
        Debug.Log($"DraftManager: playerPackQueues.Count = {playerPackQueues.Count}");
        Debug.Log($"DraftManager: currentVisiblePacks.Count = {currentVisiblePacks.Count}");
        
        // Update debug info after initialization (with delay for pack names)
        Debug.Log("DraftManager: About to call UpdateDebugInfoDelayed");
        UpdateDebugInfoDelayed();
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
        Debug.Log($"DraftManager: Attempting to remove card {cardObject.name} (ObjectId: {cardObjectId}) for player {player.EntityName.Value}");
        
        // Search across all draft packs to find the one containing this card by ObjectId
        List<DraftPack> allPacks = draftPackSetup.GetAllActivePacks();
        DraftPack packContainingCard = null;
        
        foreach (DraftPack pack in allPacks)
        {
            // Check if this pack contains the card by ObjectId
            if (pack.ContainsCardWithObjectId(cardObjectId))
            {
                packContainingCard = pack;
                Debug.Log($"DraftManager: Found card with ObjectId {cardObjectId} in pack {pack.name}");
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
            Debug.Log($"DraftManager: Successfully removed card {cardObject.name} from pack {packContainingCard.name}");
            
            // Despawn the card object
            if (cardNetObj.IsSpawned)
            {
                FishNet.InstanceFinder.ServerManager.Despawn(cardNetObj);
                Debug.Log($"DraftManager: Despawned card {cardObject.name}");
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
        
        // Update debug info after pack movement
        UpdateDebugInfoDelayed();
        
        // Update draft pack visibility for all clients after pack circulation
        RpcUpdateDraftPackVisibility();
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
        
        // Update debug info after pack passing
        UpdateDebugInfoDelayed();
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
        
        // Update debug info after draft completion
        UpdateDebugInfoDelayed();
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
            Debug.Log("DraftManager: Initial draft pack visibility updated");
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
        Debug.Log($"DraftManager.IsCardSelectableByPlayer called - Card: {cardObject.name}, Player: {player.EntityName.Value}");
        Debug.Log($"DraftManager.IsCardSelectableByPlayer - isDraftActive: {isDraftActive}");
        
        if (!isDraftActive)
        {
            Debug.Log($"DraftManager.IsCardSelectableByPlayer - Draft not active");
            return false;
        }
        
        // On server, use the currentVisiblePacks dictionary for efficiency
        if (IsServerInitialized && currentVisiblePacks.ContainsKey(player))
        {
            DraftPack visiblePack = currentVisiblePacks[player];
            Debug.Log($"DraftManager.IsCardSelectableByPlayer (Server) - Player's visible pack: {(visiblePack != null ? visiblePack.name : "null")}");
            
            List<GameObject> packCards = visiblePack.GetCards();
            Debug.Log($"DraftManager.IsCardSelectableByPlayer (Server) - Pack has {packCards.Count} cards");
            
            bool contains = packCards.Contains(cardObject);
            Debug.Log($"DraftManager.IsCardSelectableByPlayer (Server) - Pack contains this card: {contains}");
            
            return contains;
        }
        
        // On client, or if server doesn't have visible pack info, check pack ownership directly
        Debug.Log($"DraftManager.IsCardSelectableByPlayer - Using client-side pack ownership check");
        
        // Find all draft packs and check if any owned by this player contains the card
        DraftPack[] allPacks = FindObjectsByType<DraftPack>(FindObjectsSortMode.None);
        Debug.Log($"DraftManager.IsCardSelectableByPlayer - Found {allPacks.Length} draft packs");
        
        foreach (DraftPack pack in allPacks)
        {
            if (pack.IsOwnedBy(player))
            {
                Debug.Log($"DraftManager.IsCardSelectableByPlayer - Found pack owned by player: {pack.name}");
                
                List<GameObject> packCards = pack.GetCards();
                Debug.Log($"DraftManager.IsCardSelectableByPlayer - Pack has {packCards.Count} cards");
                
                bool contains = packCards.Contains(cardObject);
                Debug.Log($"DraftManager.IsCardSelectableByPlayer - Pack contains this card: {contains}");
                
                if (!contains)
                {
                    Debug.Log($"DraftManager.IsCardSelectableByPlayer - Card names in pack:");
                    for (int i = 0; i < packCards.Count; i++)
                    {
                        Debug.Log($"  [{i}]: {packCards[i].name}");
                    }
                }
                
                return contains;
            }
        }
        
        Debug.Log($"DraftManager.IsCardSelectableByPlayer - No pack owned by player found");
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
        // Update pack visibility on the client side using EntityVisibilityManager
        if (entityVisibilityManager != null)
        {
            entityVisibilityManager.UpdateDraftPackVisibility();
            Debug.Log($"DraftManager: Updated draft pack visibility for client {target.ClientId}");
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
        Debug.Log("DraftManager: RpcActivateDraftOnClients called on client");
        
        // Activate draft on client
        isDraftActive = true;
        
        // Initialize client-side pack visibility
        if (entityVisibilityManager != null)
        {
            entityVisibilityManager.UpdateDraftPackVisibility();
            Debug.Log("DraftManager: Client draft activated and pack visibility updated");
        }
        else
        {
            Debug.LogWarning("DraftManager: EntityVisibilityManager not found on client");
        }
    }
    
    [ObserversRpc]
    private void RpcUpdateDraftPackVisibility()
    {
        Debug.Log("DraftManager: RpcUpdateDraftPackVisibility called on client");
        
        // Update draft pack visibility on all clients
        EntityVisibilityManager entityVisibilityManager = FindFirstObjectByType<EntityVisibilityManager>();
        if (entityVisibilityManager != null)
        {
            Debug.Log($"DraftManager: EntityVisibilityManager found: {entityVisibilityManager.name}");
            Debug.Log($"DraftManager: EntityVisibilityManager current game state before SetGameState: {entityVisibilityManager.GetType().GetField("currentGameState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(entityVisibilityManager)}");
            
            // Ensure the EntityVisibilityManager is in Draft state
            entityVisibilityManager.SetGameState(EntityVisibilityManager.GameState.Draft);
            Debug.Log($"DraftManager: Called SetGameState(Draft) on EntityVisibilityManager");
            
            Debug.Log($"DraftManager: About to call UpdateDraftPackVisibility()");
            entityVisibilityManager.UpdateDraftPackVisibility();
            Debug.Log("DraftManager: Draft pack visibility updated on client");
        }
        else
        {
            Debug.LogWarning("DraftManager: EntityVisibilityManager not found on client for visibility update");
        }
    }
    
    /// <summary>
    /// Syncs debug info to all clients
    /// </summary>
    [ObserversRpc]
    private void RpcSyncDebugInfo(NetworkPackDebugInfo[] debugInfoArray)
    {
        if (!IsServerInitialized) // Only update on clients
        {
            packCirculationDebug.Clear();
            
            // Convert network-safe debug info back to PackDebugInfo
            foreach (var networkInfo in debugInfoArray)
            {
                PackDebugInfo debugInfo = new PackDebugInfo(
                    networkInfo.packName,
                    networkInfo.originalOwner,
                    networkInfo.currentOwner,
                    networkInfo.queuePosition,
                    networkInfo.isCurrentlyVisible
                );
                packCirculationDebug.Add(debugInfo);
            }
            
            Debug.Log($"DraftManager: Received debug info on client - {debugInfoArray.Length} entries");
        }
    }
    
    /// <summary>
    /// Updates debug info with a delay to allow pack names to be set
    /// </summary>
    private void UpdateDebugInfoDelayed()
    {
        if (IsServerInitialized)
        {
            StartCoroutine(UpdateDebugInfoWithDelay());
        }
    }
    
    private System.Collections.IEnumerator UpdateDebugInfoWithDelay()
    {
        // Wait a bit for pack names to be set via RPC
        yield return new WaitForSeconds(0.2f);
        
        UpdatePackCirculationDebugInfo();
        
        // Sync to clients
        if (packCirculationDebug.Count > 0)
        {
            RpcSyncDebugInfo(packCirculationDebug.Select(info => new NetworkPackDebugInfo(info)).ToArray());
        }
    }
    
    // Helper methods
    private void OnPlayerReadyChanged(SyncDictionaryOperation op, NetworkConnection key, bool value, bool asServer)
    {
        if (asServer && key != null)
        {
            Debug.Log($"DraftManager: Player {key.ClientId} ready state changed to {value}");
        }
        else if (asServer && key == null)
        {
            Debug.Log($"DraftManager: Player ready state changed (null connection) - operation: {op}, value: {value}");
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
    
    // Debug methods for pack circulation tracking
    /// <summary>
    /// Updates the debug information for pack circulation in the inspector
    /// </summary>
    private void UpdatePackCirculationDebugInfo()
    {
        Debug.Log($"DraftManager.UpdatePackCirculationDebugInfo: autoUpdateDebugInfo = {autoUpdateDebugInfo}");
        
        if (!autoUpdateDebugInfo)
            return;
            
        packCirculationDebug.Clear();
        
        // Get all active packs
        List<DraftPack> allPacks = draftPackSetup?.GetAllActivePacks() ?? new List<DraftPack>();
        Debug.Log($"DraftManager.UpdatePackCirculationDebugInfo: Found {allPacks.Count} active packs");
        
        // Track which packs we've already processed to avoid duplicates
        HashSet<DraftPack> processedPacks = new HashSet<DraftPack>();
        
        Debug.Log($"DraftManager.UpdatePackCirculationDebugInfo: playerPackQueues.Count = {playerPackQueues.Count}");
        
        // Go through each player's queue and add debug info for each pack
        foreach (var kvp in playerPackQueues)
        {
            NetworkEntity player = kvp.Key;
            Queue<DraftPack> queue = kvp.Value;
            
            string playerName = player?.EntityName?.Value ?? "Unknown Player";
            Debug.Log($"DraftManager.UpdatePackCirculationDebugInfo: Processing player {playerName} with {queue.Count} packs in queue");
            
            // Convert queue to array to access by index
            DraftPack[] queueArray = queue.ToArray();
            
            for (int i = 0; i < queueArray.Length; i++)
            {
                DraftPack pack = queueArray[i];
                if (pack != null && !processedPacks.Contains(pack))
                {
                    bool isCurrentlyVisible = currentVisiblePacks.ContainsKey(player) && currentVisiblePacks[player] == pack;
                    
                    PackDebugInfo debugInfo = new PackDebugInfo(pack, playerName, i, isCurrentlyVisible);
                    packCirculationDebug.Add(debugInfo);
                    processedPacks.Add(pack);
                    
                    Debug.Log($"DraftManager.UpdatePackCirculationDebugInfo: Added debug info for pack {pack.name} - Player: {playerName}, Queue: {i}, Visible: {isCurrentlyVisible}");
                }
                else if (pack == null)
                {
                    Debug.LogWarning($"DraftManager.UpdatePackCirculationDebugInfo: Found null pack in queue for player {playerName} at index {i}");
                }
                else if (processedPacks.Contains(pack))
                {
                    Debug.Log($"DraftManager.UpdatePackCirculationDebugInfo: Pack {pack.name} already processed, skipping");
                }
            }
        }
        
        // Add any packs that might not be in any queue (shouldn't happen in normal flow, but good for debugging)
        foreach (DraftPack pack in allPacks)
        {
            if (pack != null && !processedPacks.Contains(pack))
            {
                PackDebugInfo debugInfo = new PackDebugInfo(pack, "No Owner", -1, false);
                packCirculationDebug.Add(debugInfo);
                Debug.Log($"DraftManager.UpdatePackCirculationDebugInfo: Added orphaned pack {pack.name}");
            }
        }
        
        // Sort by pack name for consistent display
        packCirculationDebug.Sort((a, b) => 
        {
            if (a.pack == null && b.pack == null) return 0;
            if (a.pack == null) return 1;
            if (b.pack == null) return -1;
            return string.Compare(a.pack.name, b.pack.name);
        });
        
        Debug.Log($"DraftManager.UpdatePackCirculationDebugInfo: Final packCirculationDebug.Count = {packCirculationDebug.Count}");
    }
    
    /// <summary>
    /// Public method to refresh debug info (can be called externally)
    /// </summary>
    public void RefreshDebugInfo()
    {
        if (IsServerInitialized)
        {
            UpdateDebugInfoDelayed();
        }
    }
    
    /// <summary>
    /// Public method to manually update debug info (useful for testing)
    /// </summary>
    [ContextMenu("Update Pack Circulation Debug Info")]
    public void ManualUpdateDebugInfo()
    {
        UpdatePackCirculationDebugInfo();
        
        // If on server, sync to clients
        if (IsServerInitialized && packCirculationDebug.Count > 0)
        {
            RpcSyncDebugInfo(packCirculationDebug.Select(info => new NetworkPackDebugInfo(info)).ToArray());
        }
    }
    
    /// <summary>
    /// Clears the debug information
    /// </summary>
    [ContextMenu("Clear Pack Circulation Debug Info")]
    public void ClearDebugInfo()
    {
        packCirculationDebug.Clear();
    }
} 