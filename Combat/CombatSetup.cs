using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Observing;
using System.Collections;
using FishNet.Object.Synchronizing;

/// <summary>
/// Initializes the combat phase including entity deck setup, fight assignments, and UI preparation.
/// Attach to: A NetworkObject in the scene that coordinates the combat setup process.
/// </summary>
public class CombatSetup : NetworkBehaviour
{
    [SerializeField] private GameObject combatCanvas;
    //
    [Header("Required Components")]
    [SerializeField] private FightManager fightManager;
    [SerializeField] private CombatManager combatManager;
    [SerializeField] private CombatCanvasManager combatCanvasManager;
    [SerializeField] private OnlineGameManager gameManager;
    [SerializeField] private GamePhaseManager gamePhaseManager;
    [SerializeField] private FightPreviewManager fightPreviewManager;
    [SerializeField] private LoadingScreenManager loadingScreenManager;
    
    private SteamNetworkIntegration steamNetworkIntegration;

    // Track ready state for each client
    private readonly SyncDictionary<NetworkConnection, bool> readyClients = new SyncDictionary<NetworkConnection, bool>();
    private bool allClientsReady = false;

    [Header("Combat Participants")]
    private NetworkEntity player1;
    private NetworkEntity pet1;
    private NetworkEntity player2;
    private NetworkEntity pet2;

    [Header("Combat State")]
    private bool isCombatActive = false;
    private readonly SyncVar<bool> isSetupComplete = new SyncVar<bool>(false);

    public bool IsCombatActive => isCombatActive;
    public bool IsSetupComplete => isSetupComplete.Value;

    private void Awake()
    {
        RegisterCombatCanvasWithPhaseManager();
    }
    
    private void RegisterCombatCanvasWithPhaseManager()
    {
        if (gamePhaseManager == null)
        {
            gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
        }
        
        if (gamePhaseManager != null && combatCanvas != null)
        {
            gamePhaseManager.SetCombatCanvas(combatCanvas);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!IsServerStarted) return;
        
        // Set up SyncVar change callback
        isSetupComplete.OnChange += OnSetupCompleteChanged;
        
        ResolveReferences();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsClientStarted) return;
        
        // Set up SyncVar change callback for clients
        isSetupComplete.OnChange += OnSetupCompleteChanged;
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        isSetupComplete.OnChange -= OnSetupCompleteChanged;
    }
    
    public override void OnStopServer()
    {
        base.OnStopServer();
        isSetupComplete.OnChange -= OnSetupCompleteChanged;
    }
    
    private void OnSetupCompleteChanged(bool prev, bool next, bool asServer)
    {
        Debug.Log($"CombatSetup: Setup complete changed from {prev} to {next} on {(asServer ? "Server" : "Client")}");
    }

    [ServerRpc(RequireOwnership = false)]
    private void ServerSetClientReady(NetworkConnection connection)
    {
        if (!IsServerInitialized || connection == null)
        {
            Debug.LogWarning("CombatSetup: ServerSetClientReady called with null connection or server not initialized.");
            return;
        }


        readyClients[connection] = true;
        CheckAllClientsReady();
    }

    private void CheckAllClientsReady()
    {
        if (!IsServerInitialized) return;

        // Get all connected client IDs
        var connectedClientIds = NetworkManager.ServerManager.Clients.Keys.ToList();


        // Check if all connected clients are ready
        bool allReady = true;
        foreach (int clientId in connectedClientIds)
        {
            if (NetworkManager.ServerManager.Clients.TryGetValue(clientId, out NetworkConnection clientConn))
            {
                bool isClientReady = readyClients.ContainsKey(clientConn) && readyClients[clientConn];
    
                if (!isClientReady)
                {
                    allReady = false;
                }
            }
            else
            {
                Debug.LogWarning($"CombatSetup: Could not find NetworkConnection for ClientId {clientId} during readiness check.");
                allReady = false; // If we can't find the connection, assume not ready.
            }
        }

        if (allReady && connectedClientIds.Count > 0 && !allClientsReady) // Ensure there's at least one client and we haven't already set allClientsReady
        {
            allClientsReady = true;
            /* Debug.Log("CombatSetup: All clients are ready, starting combat"); */
            StartCombat();
        }
        else if (!allReady)
        {
            /* Debug.Log("CombatSetup: Not all clients are ready yet, waiting..."); */
        }
        else if (connectedClientIds.Count == 0 && !allClientsReady)
        {
            /* Debug.Log("CombatSetup: No clients connected, waiting for connections or manual start if applicable."); */
        }
    }

    [Server]
    private void StartCombat()
    {
        if (!IsServerInitialized || !allClientsReady) 
        {
            Debug.LogWarning("CombatSetup: Cannot start combat - server not initialized or clients not ready");
            return;
        }
        
        /* Debug.Log("CombatSetup: All clients ready, starting fight preview before combat"); */
        
        // Notify loading screen that setup is complete - all clients are ready
        if (loadingScreenManager != null)
        {
            /* Debug.Log("CombatSetup: All clients ready, notifying loading screen that setup is complete"); */
            RpcNotifyLoadingScreenSetupComplete();
        }
        
        // Start the fight preview interstitial instead of going directly to combat
        if (fightPreviewManager != null)
        {
            /* Debug.Log("CombatSetup: Starting fight preview sequence"); */
            fightPreviewManager.StartFightPreview();
            
            // Note: The FightPreviewManager will handle calling combatManager.StartCombat() after the preview
            // Set combat as active now since the preview is part of the combat flow
            isCombatActive = true;
            Debug.Log("CombatSetup: Fight preview started, combat will begin after preview completes");
        }
        else
        {
            // Fallback to direct combat start if preview manager is missing
            Debug.LogWarning("CombatSetup: FightPreviewManager not found, starting combat directly");
            if (combatManager != null)
            {
                isCombatActive = true;
                combatManager.StartCombat();
                /* Debug.Log("CombatSetup: Combat started directly (no preview)"); */
            }
            else
            {
                Debug.LogError("CombatSetup: Cannot start combat - both FightPreviewManager and CombatManager references are missing");
            }
        }
    }

    private void ResolveReferences()
    {
        steamNetworkIntegration = SteamNetworkIntegration.Instance;
        
        if (fightManager == null) fightManager = FindFirstObjectByType<FightManager>();
        if (combatManager == null) combatManager = FindFirstObjectByType<CombatManager>();
        if (combatCanvasManager == null) combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
        if (gameManager == null) gameManager = OnlineGameManager.Instance;
        if (gamePhaseManager == null) gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
        if (fightPreviewManager == null) fightPreviewManager = FindFirstObjectByType<FightPreviewManager>();
        if (loadingScreenManager == null) loadingScreenManager = FindFirstObjectByType<LoadingScreenManager>();
        
        RegisterCombatCanvasWithPhaseManager();
    }

    [Server]
    public void InitializeCombat()
    {
        if (!IsServerStarted) 
        {
            Debug.LogError("CombatSetup: Cannot initialize combat - server not started");
            return;
        }
        
        if (isSetupComplete.Value) 
        {
            Debug.LogWarning("CombatSetup: Combat setup already complete, skipping initialization");
            return;
        }
        
        /* Debug.Log("CombatSetup: Starting combat initialization from draft transition..."); */
        
        // Reset client ready states for new combat round
        readyClients.Clear();
        allClientsReady = false;
        /* Debug.Log("CombatSetup: Reset client ready states"); */
        
        ResolveReferences();
        /* Debug.Log("CombatSetup: References resolved"); */

        if (!AreRequiredComponentsAvailable())
        {
            Debug.LogError("CombatSetup: Missing critical references. Combat will not be initialized.");
            return;
        }
        
        /* Debug.Log("CombatSetup: All required components available, proceeding with setup"); */
        
        /* Debug.Log("CombatSetup: Resetting entity health and energy..."); */
        ResetEntityHealthAndEnergy();
        
        /* Debug.Log("CombatSetup: Setting up combat decks..."); */
        SetupCombatDecks();
        
        /* Debug.Log("CombatSetup: Assigning fights..."); */
        AssignFights();
        
        /* Debug.Log("CombatSetup: Ensuring players are observers..."); */
        EnsurePlayersAreObservers();
        
        /* Debug.Log("CombatSetup: Transitioning to combat phase (after fight assignments)..."); */
        TransitionToPhase();
        
        /* Debug.Log("CombatSetup: Triggering combat canvas setup..."); */
        RpcTriggerCombatCanvasManagerSetup();
        
        isSetupComplete.Value = true; // Mark setup as complete
        /* Debug.Log("CombatSetup: Setup completed successfully, waiting for client readiness checks"); */

        // Instead of starting combat immediately, notify clients to check their setup
        RpcCheckClientSetup();
    }
    
    private bool AreRequiredComponentsAvailable()
    {
        return steamNetworkIntegration != null && 
               fightManager != null && 
               combatManager != null && 
               gameManager != null &&
               fightPreviewManager != null;
    }
    
    private void TransitionToPhase()
    {
        if (gamePhaseManager != null)
        {
            /* Debug.Log("CombatSetup: Setting combat phase on server"); */
            gamePhaseManager.SetCombatPhase();
            
            // Use RPC method as primary route (since it works reliably)
            RpcUpdateGamePhaseToCombat();
            Debug.Log("CombatSetup: Sent combat phase change to all clients via RPC");
        }
        else
        {
            Debug.LogError("CombatSetup: GamePhaseManager not found, cannot transition to combat phase");
        }
    }

    [Server]
    private void SetupCombatDecks()
    {
        if (!IsServerInitialized) return;

        // Get all spawned entities
        List<NetworkEntity> entities = GetAllSpawnedEntities<NetworkEntity>();
        
        foreach (NetworkEntity entity in entities)
        {
            // Only look for CombatDeckSetup on Player and Pet entities, not Hand entities
            if (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet)
            {
                CombatDeckSetup deckSetup = entity.GetComponent<CombatDeckSetup>();
                if (deckSetup != null)
                {
                    deckSetup.SetupCombatDeck();
                }
                else
                {
                    Debug.LogError($"Entity {entity.EntityName.Value} ({entity.EntityType}) is missing CombatDeckSetup component");
                }
            }
            // Hand entities don't need CombatDeckSetup - they have CardSpawner/CardParenter/HandManager instead
        }
    }

    /// <summary>
    /// Resets all entity health and energy to maximum for the start of combat
    /// </summary>
    [Server]
    private void ResetEntityHealthAndEnergy()
    {
        if (!IsServerInitialized) return;

        /* Debug.Log("CombatSetup: Resetting all entity health and energy to maximum"); */

        // Get all spawned entities
        List<NetworkEntity> entities = GetAllSpawnedEntities<NetworkEntity>();
        
        foreach (NetworkEntity entity in entities)
        {
            if (entity != null && (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet))
            {
                // Reset health to maximum
                entity.CurrentHealth.Value = entity.MaxHealth.Value;
                
                // Reset energy to maximum
                entity.CurrentEnergy.Value = entity.MaxEnergy.Value;
                
                Debug.Log($"CombatSetup: Reset {entity.EntityName.Value} - Health: {entity.CurrentHealth.Value}/{entity.MaxHealth.Value}, Energy: {entity.CurrentEnergy.Value}/{entity.MaxEnergy.Value}");
            }
        }
        
        /* Debug.Log("CombatSetup: Entity health and energy reset completed"); */
    }

    [Server]
    private void AssignFights()
    {
        if (!IsServerInitialized || fightManager == null) 
        {
            Debug.LogError("FLEXPAIRING: Cannot assign fights - server not initialized or fightManager is null");
            return;
        }

        // Get all entities
        List<NetworkEntity> entities = GetAllSpawnedEntities<NetworkEntity>();
        Debug.Log($"FLEXPAIRING: Found {entities.Count} total entities");
        
        var players = entities.Where(e => e.EntityType == EntityType.Player).ToList();
        var pets = entities.Where(e => e.EntityType == EntityType.Pet).ToList();
        
        Debug.Log($"FLEXPAIRING: Entity breakdown - {players.Count} players, {pets.Count} pets");
        foreach (var player in players)
        {
            Debug.Log($"FLEXPAIRING: Player - {player.EntityName.Value} (Owner: {player.OwnerId}, ObjectId: {player.ObjectId})");
        }
        foreach (var pet in pets)
        {
            Debug.Log($"FLEXPAIRING: Pet - {pet.EntityName.Value} (Owner: {pet.OwnerId}, ObjectId: {pet.ObjectId}, OwnerEntityId: {pet.OwnerEntityId.Value})");
        }
        
        // Check if flexible pairing is enabled
        OfflineGameManager offlineManager = OfflineGameManager.Instance;
        bool flexiblePairingEnabled = offlineManager != null && offlineManager.EnableFlexiblePairing;

        Debug.Log($"FLEXPAIRING: OfflineGameManager.Instance: {offlineManager != null}");
        if (offlineManager != null)
        {
            Debug.Log($"FLEXPAIRING: EnableFlexiblePairing: {offlineManager.EnableFlexiblePairing}, PrioritizePlayerVsPlayer: {offlineManager.PrioritizePlayerVsPlayer}");
        }

        if (flexiblePairingEnabled)
        {
            Debug.Log("FLEXPAIRING: Using flexible pairing algorithm");
            AssignFlexibleFights(entities);
        }
        else
        {
            Debug.Log("FLEXPAIRING: Using traditional Player vs Pet pairing");
            AssignPlayersToPets(players, pets);
        }
    }
    
    /// <summary>
    /// Flexible pairing algorithm that can pair any entity with any other entity
    /// Ensures: 1) Everyone is paired, 2) No one fights their own ally, 3) Each entity is in only one fight
    /// </summary>
    private void AssignFlexibleFights(List<NetworkEntity> entities)
    {
        // Filter to only combat entities (Player and Pet)
        var combatEntities = entities.Where(e => e.EntityType == EntityType.Player || e.EntityType == EntityType.Pet).ToList();
        
        Debug.Log($"FLEXPAIRING: AssignFlexibleFights called with {entities.Count} entities, {combatEntities.Count} combat entities");
        
        if (combatEntities.Count < 2)
        {
            Debug.LogWarning("FLEXPAIRING: Not enough combat entities for flexible pairing");
            return;
        }
        
        // Create a list of available entities for pairing
        List<NetworkEntity> availableEntities = new List<NetworkEntity>(combatEntities);
        
        OfflineGameManager offlineManager = OfflineGameManager.Instance;
        bool prioritizePlayerVsPlayer = offlineManager != null && offlineManager.PrioritizePlayerVsPlayer;
        
        Debug.Log($"FLEXPAIRING: Flexible pairing with {availableEntities.Count} entities (prioritize PvP: {prioritizePlayerVsPlayer})");
        
        // Phase 1: If prioritizing Player vs Player, try to pair players first
        if (prioritizePlayerVsPlayer)
        {
            Debug.Log("FLEXPAIRING: Phase 1 - Pairing players vs players");
            PairPlayerVsPlayer(availableEntities);
        }
        
        // Phase 2: Pair remaining entities using optimal algorithm
        Debug.Log("FLEXPAIRING: Phase 2 - Pairing remaining entities");
        PairRemainingEntities(availableEntities);
        
        Debug.Log($"FLEXPAIRING: Flexible pairing complete. {availableEntities.Count} entities remain unpaired.");
    }
    
    /// <summary>
    /// Pairs players against each other when prioritizing Player vs Player fights
    /// </summary>
    private void PairPlayerVsPlayer(List<NetworkEntity> availableEntities)
    {
        var players = availableEntities.Where(e => e.EntityType == EntityType.Player).ToList();
        
        Debug.Log($"FLEXPAIRING: Attempting to pair {players.Count} players against each other");
        
        // Pair players in groups of 2
        for (int i = 0; i < players.Count - 1; i += 2)
        {
            NetworkEntity player1 = players[i];
            NetworkEntity player2 = players[i + 1];
            
            Debug.Log($"FLEXPAIRING: Checking if {player1.EntityName.Value} and {player2.EntityName.Value} are allies");
            
            // Ensure they're not allies (shouldn't happen with players, but safety check)
            if (!AreEntitiesAllies(player1, player2))
            {
                Debug.Log($"FLEXPAIRING: Adding fight assignment - {player1.EntityName.Value} vs {player2.EntityName.Value}");
                fightManager.AddFightAssignment(player1, player2);
                availableEntities.Remove(player1);
                availableEntities.Remove(player2);
                
                Debug.Log($"FLEXPAIRING: Successfully paired Player vs Player - {player1.EntityName.Value} vs {player2.EntityName.Value}");
            }
            else
            {
                Debug.LogWarning($"FLEXPAIRING: Players {player1.EntityName.Value} and {player2.EntityName.Value} are allies, skipping pairing");
            }
        }
    }
    
    /// <summary>
    /// Pairs remaining entities using an optimal algorithm
    /// </summary>
    private void PairRemainingEntities(List<NetworkEntity> availableEntities)
    {
        Debug.Log($"FLEXPAIRING: Pairing {availableEntities.Count} remaining entities");
        
        while (availableEntities.Count >= 2)
        {
            NetworkEntity leftFighter = availableEntities[0];
            NetworkEntity rightFighter = null;
            
            Debug.Log($"FLEXPAIRING: Looking for opponent for {leftFighter.EntityName.Value} ({leftFighter.EntityType})");
            
            // Find the best opponent for leftFighter
            for (int i = 1; i < availableEntities.Count; i++)
            {
                NetworkEntity candidate = availableEntities[i];
                
                Debug.Log($"FLEXPAIRING: Checking candidate {candidate.EntityName.Value} ({candidate.EntityType})");
                
                // Check if they can fight (not allies)
                if (!AreEntitiesAllies(leftFighter, candidate))
                {
                    rightFighter = candidate;
                    Debug.Log($"FLEXPAIRING: Found valid opponent - {candidate.EntityName.Value}");
                    break;
                }
                else
                {
                    Debug.Log($"FLEXPAIRING: {candidate.EntityName.Value} is ally of {leftFighter.EntityName.Value}, skipping");
                }
            }
            
            if (rightFighter != null)
            {
                Debug.Log($"FLEXPAIRING: Adding fight assignment - {leftFighter.EntityName.Value} vs {rightFighter.EntityName.Value}");
                fightManager.AddFightAssignment(leftFighter, rightFighter);
                availableEntities.Remove(leftFighter);
                availableEntities.Remove(rightFighter);
                
                Debug.Log($"FLEXPAIRING: Successfully paired {leftFighter.EntityName.Value} ({leftFighter.EntityType}) vs {rightFighter.EntityName.Value} ({rightFighter.EntityType})");
            }
            else
            {
                // No valid opponent found for leftFighter, remove them from pairing
                Debug.LogWarning($"FLEXPAIRING: No valid opponent found for {leftFighter.EntityName.Value}, removing from pairing");
                availableEntities.Remove(leftFighter);
            }
        }
        
        if (availableEntities.Count > 0)
        {
            Debug.LogWarning($"FLEXPAIRING: {availableEntities.Count} entities could not be paired:");
            foreach (var entity in availableEntities)
            {
                Debug.LogWarning($"FLEXPAIRING:   - {entity.EntityName.Value} ({entity.EntityType})");
            }
        }
    }
    
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

    private void AssignPlayersToPets(List<NetworkEntity> players, List<NetworkEntity> pets)
    {
        List<NetworkEntity> availablePets = new List<NetworkEntity>(pets);
        
        foreach (NetworkEntity leftFighter in players)
        {
            if (availablePets.Count == 0) break;
            
            // Find left fighter's own pet to avoid pairing with it
            NetworkEntity leftFighterOwnedPet = pets.FirstOrDefault(p => p.GetOwnerEntity() == leftFighter);
            
            // Find a suitable pet to fight against (right fighter)
            NetworkEntity rightFighter = FindOpponentPet(availablePets, leftFighterOwnedPet);
            
            if (rightFighter != null)
            {
                fightManager.AddFightAssignment(leftFighter, rightFighter);
                availablePets.Remove(rightFighter);
            }
        }
    }
    
    private NetworkEntity FindOpponentPet(List<NetworkEntity> availablePets, NetworkEntity leftFighterOwnedPet)
    {
        // First try to find a pet that's not owned by the left fighter
        foreach (NetworkEntity pet in availablePets)
        {
            if (pet != leftFighterOwnedPet)
            {
                return pet;
            }
        }
        
        // If no other pet is available, return the first pet (even if it's the left fighter's own)
        return availablePets.Count > 0 ? availablePets[0] : null;
    }

    private List<T> GetAllSpawnedEntities<T>() where T : NetworkBehaviour
    {
        return FishNet.InstanceFinder.NetworkManager.ServerManager.Objects.Spawned.Values
            .Select(nob => nob.GetComponent<T>())
            .Where(p => p != null)
            .ToList();
    }

    [Server]
    private void EnsurePlayersAreObservers()
    {
        if (!IsServerInitialized || combatManager == null) return;
        
        NetworkObject combatManagerNob = combatManager.GetComponent<NetworkObject>();
        if (combatManagerNob == null) return;

        if (!combatManagerNob.IsSpawned)
        {
            FishNet.InstanceFinder.ServerManager.Spawn(combatManagerNob);
        }
        
        Debug.Log("CombatSetup: Players set as observers of combat manager");
    }

    [ObserversRpc]
    private void RpcTriggerCombatCanvasManagerSetup()
    {
        /* Debug.Log("CombatSetup: RpcTriggerCombatCanvasManagerSetup called on client"); */
        
        if (combatCanvasManager != null)
        {
            /* Debug.Log("CombatSetup: Found CombatCanvasManager, proceeding with setup"); */
            
            // Reset positioning state for new combat round
            combatCanvasManager.ResetPositioningState();
            
            // Ensure draft canvas is disabled before enabling combat canvas
            DraftCanvasManager draftCanvasManager = FindFirstObjectByType<DraftCanvasManager>();
            if (draftCanvasManager != null)
            {
                Debug.Log("CombatSetup: Found DraftCanvasManager, ensuring draft canvas is disabled");
                draftCanvasManager.DisableDraftCanvas();
                Debug.Log("CombatSetup: Draft canvas disabled successfully");
            }
            else
            {
                Debug.LogWarning("CombatSetup: DraftCanvasManager not found, cannot ensure draft canvas is disabled");
            }
            
            // Enable the combat canvas
            if (combatCanvas != null)
            {
                /* Debug.Log("CombatSetup: Enabling combat canvas"); */
                combatCanvas.SetActive(true);
                /* Debug.Log("CombatSetup: Combat canvas enabled successfully"); */
            }
            else
            {
                Debug.LogError("CombatSetup: Combat canvas reference is missing");
            }
            
            /* Debug.Log("CombatSetup: Starting combat UI setup with delay"); */
            StartCoroutine(SetupCombatUIWithDelay());
        }
        else
        {
            Debug.LogError("CombatSetup: CombatCanvasManager reference is missing");
        }
    }

    private IEnumerator SetupCombatUIWithDelay()
    {
        yield return null;
        /* Debug.Log("[COMBAT_SETUP] Setting up combat UI after delay"); */
        
        // Ensure canvas is enabled before setting up UI
        if (combatCanvas != null && !combatCanvas.activeSelf)
        {
            Debug.Log("[COMBAT_SETUP] Re-enabling combat canvas before UI setup");
            combatCanvas.SetActive(true);
        }
        
        Debug.Log("[COMBAT_SETUP] About to call SetupCombatUI on CombatCanvasManager");
        combatCanvasManager.SetupCombatUI();
        Debug.Log("[COMBAT_SETUP] Combat UI setup completed");
    }

    /// <summary>
    /// Sets up a combat between two player-pet pairs
    /// </summary>
    [Server]
    public void SetupCombat(NetworkEntity player1, NetworkEntity pet1, NetworkEntity player2, NetworkEntity pet2)
    {
        if (!IsServerInitialized) return;

        // Validate entity types
        if (!ValidateEntities(player1, pet1, player2, pet2))
        {
            Debug.LogError("CombatSetup: Invalid entity types provided");
            return;
        }

        // Store references
        this.player1 = player1;
        this.pet1 = pet1;
        this.player2 = player2;
        this.pet2 = pet2;

        // Initialize combat state
        isCombatActive = true;
        isSetupComplete.Value = true;

        // Notify clients about combat setup
        RpcNotifyCombatSetup();

        /* Debug.Log($"Combat setup complete between {player1.EntityName.Value} & {pet1.EntityName.Value} vs {player2.EntityName.Value} & {pet2.EntityName.Value}"); */
    }

    private bool ValidateEntities(NetworkEntity player1, NetworkEntity pet1, NetworkEntity player2, NetworkEntity pet2)
    {
        // Check for null references
        if (player1 == null || pet1 == null || player2 == null || pet2 == null)
            return false;

        // Validate entity types
        if (player1.EntityType != EntityType.Player || 
            pet1.EntityType != EntityType.Pet ||
            player2.EntityType != EntityType.Player || 
            pet2.EntityType != EntityType.Pet)
            return false;

        // Validate ownership relationships
        if (pet1.GetOwnerEntity() != player1 || pet2.GetOwnerEntity() != player2)
            return false;

        return true;
    }

    [ObserversRpc]
    private void RpcNotifyCombatSetup()
    {
        // Client-side setup logic here
        Debug.Log("Combat setup notification received on client");
    }

    [Server]
    public void EndCombat()
    {
        if (!IsServerInitialized || !isCombatActive) return;

        isCombatActive = false;
        isSetupComplete.Value = false;

        // Clear references
        player1 = null;
        pet1 = null;
        player2 = null;
        pet2 = null;

        // Notify clients about combat end
        RpcNotifyCombatEnd();

        Debug.Log("Combat ended");
    }

    [ObserversRpc]
    private void RpcNotifyCombatEnd()
    {
        // Client-side cleanup logic here
        Debug.Log("Combat end notification received on client");
    }

    public NetworkEntity GetPlayer1() => player1;
    public NetworkEntity GetPet1() => pet1;
    public NetworkEntity GetPlayer2() => player2;
    public NetworkEntity GetPet2() => pet2;

    [ObserversRpc]
    private void RpcCheckClientSetup()
    {
        if (!IsClientStarted) return;

        StartCoroutine(MonitorClientSetupComplete());
    }

    private IEnumerator MonitorClientSetupComplete()
    {
        // Monitor for NetworkEntities to be spawned using frame-based checking instead of time delays
        var entities = FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        int frameCount = 0;
        const int maxFramesToWaitForEntities = 150; // 2.5 seconds at 60fps

        // Wait for entities to spawn - check every frame instead of time delay
        while (entities.Length == 0 && frameCount < maxFramesToWaitForEntities)
        {
            yield return null; // Wait one frame
            frameCount++;
            entities = FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        }

        if (entities.Length == 0)
        {
            Debug.LogWarning($"Client {LocalConnection.ClientId}: No NetworkEntities found after {frameCount} frames");
            yield break;
        }

        // Find all Player and Pet entities that need CombatDeckSetup
        List<CombatDeckSetup> requiredSetups = new List<CombatDeckSetup>();
        foreach (var entity in entities)
        {
            if (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet)
            {
                var deckSetup = entity.GetComponent<CombatDeckSetup>();
                if (deckSetup != null)
                {
                    requiredSetups.Add(deckSetup);
                }
                else
                {
                    Debug.LogError($"Client {LocalConnection.ClientId}: Entity '{entity.name}' (ID: {entity.ObjectId}) is missing CombatDeckSetup component!");
                    // Can't proceed without required components
                    yield break;
                }
            }
        }

        if (requiredSetups.Count == 0)
        {
            Debug.LogWarning($"Client {LocalConnection.ClientId}: No Player/Pet entities found with CombatDeckSetup components");
            yield break;
        }

        /* Debug.Log($"Client {LocalConnection.ClientId}: Monitoring {requiredSetups.Count} CombatDeckSetup components for completion..."); */

        // Use event-driven monitoring instead of polling with delays
        yield return StartCoroutine(WaitForAllSetupComplete(requiredSetups));
    }

    /// <summary>
    /// Waits for all CombatDeckSetup components to complete using frame-based checking
    /// More responsive than time-based polling
    /// </summary>
    private IEnumerator WaitForAllSetupComplete(List<CombatDeckSetup> requiredSetups)
    {
        const int maxFramesToWait = 1800; // 30 seconds at 60fps as safety fallback
        int frameCount = 0;
        bool allComplete = false;

        while (!allComplete && frameCount < maxFramesToWait)
        {
            yield return null; // Check every frame instead of every 100ms
            frameCount++;

            allComplete = true;
            
            // Log status every 120 frames (about 2 seconds at 60fps) while waiting
            bool shouldLogStatus = frameCount % 120 == 0 && frameCount > 60;
            string statusUpdate = shouldLogStatus ? $"Client {LocalConnection.ClientId} setup status:" : "";

            foreach (var setup in requiredSetups)
            {
                if (setup == null) continue; // Entity might have been destroyed

                NetworkEntity entity = setup.GetComponent<NetworkEntity>();
                bool isComplete = setup.IsSetupComplete;
                
                if (shouldLogStatus)
                {
                    statusUpdate += $"\n - Entity '{entity.name}' (ID: {entity.ObjectId}): IsSetupComplete = {isComplete}";
                }

                if (!isComplete)
                {
                    allComplete = false;
                }
            }

            if (shouldLogStatus && !allComplete)
            {
                /* Debug.Log(statusUpdate); */
            }
        }

        if (allComplete)
        {
            /* Debug.Log($"Client {LocalConnection.ClientId} combat setup check passed! All entities ready after {frameCount} frames. Notifying server."); */
            ServerSetClientReady(LocalConnection);
        }
        else
        {
            Debug.LogError($"Client {LocalConnection.ClientId} combat setup check timed out after {frameCount} frames ({frameCount/60f:F1} seconds). Some entities never completed setup.");
        }
    }

    [ObserversRpc]
    private void RpcUpdateGamePhaseToCombat()
    {
        /* Debug.Log("CombatSetup: RpcUpdateGamePhaseToCombat called on client"); */
        
        if (gamePhaseManager != null)
        {
            Debug.Log($"CombatSetup: Found GamePhaseManager, current phase before change: {gamePhaseManager.GetCurrentPhase()}");
            Debug.Log("CombatSetup: Setting to combat phase");
            gamePhaseManager.SetCombatPhase();
            Debug.Log($"CombatSetup: Game phase after change: {gamePhaseManager.GetCurrentPhase()}");
        }
        else
        {
            // Try to find GamePhaseManager if reference is missing
            gamePhaseManager = GamePhaseManager.Instance;
            if (gamePhaseManager != null)
            {
                Debug.Log($"CombatSetup: Found GamePhaseManager via Instance, current phase before change: {gamePhaseManager.GetCurrentPhase()}");
                Debug.Log("CombatSetup: Setting to combat phase");
                gamePhaseManager.SetCombatPhase();
                Debug.Log($"CombatSetup: Game phase after change: {gamePhaseManager.GetCurrentPhase()}");
            }
            else
            {
                Debug.LogError("CombatSetup: GamePhaseManager reference is missing and Instance is null");
            }
        }
    }

    [ObserversRpc]
    private void RpcNotifyLoadingScreenSetupComplete()
    {
        /* Debug.Log("CombatSetup: RpcNotifyLoadingScreenSetupComplete called on client"); */
        
        if (loadingScreenManager != null)
        {
            Debug.Log("CombatSetup: Found LoadingScreenManager, notifying deck setup complete");
            loadingScreenManager.OnDeckSetupComplete();
        }
        else
        {
            // Try to find LoadingScreenManager if reference is missing
            loadingScreenManager = FindFirstObjectByType<LoadingScreenManager>();
            if (loadingScreenManager != null)
            {
                Debug.Log("CombatSetup: Found LoadingScreenManager via FindFirstObjectByType, notifying deck setup complete");
                loadingScreenManager.OnDeckSetupComplete();
            }
            else
            {
                Debug.LogWarning("CombatSetup: LoadingScreenManager not found, cannot notify deck setup completion");
            }
        }
        
        // After deck setup completion, trigger initial card drawing (server only)
        if (IsServerInitialized && combatManager != null)
        {
            Debug.Log("CombatSetup: Deck setup complete, now drawing initial hands");
            combatManager.DrawInitialHands();
        }
    }
} 