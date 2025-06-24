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
    
    [Header("Required Components")]
    [SerializeField] private FightManager fightManager;
    [SerializeField] private CombatManager combatManager;
    [SerializeField] private CombatCanvasManager combatCanvasManager;
    [SerializeField] private GameManager gameManager;
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
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
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

        // Notify loading screen that setup is complete
        if (loadingScreenManager != null)
        {
            /* Debug.Log("CombatSetup: Notifying loading screen that setup is complete"); */
            RpcNotifyLoadingScreenSetupComplete();
        }

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
            
            // Network the phase change to all clients using PhaseNetworker
            PhaseNetworker phaseNetworker = gamePhaseManager.GetComponent<PhaseNetworker>();
            if (phaseNetworker != null)
            {
                Debug.Log("CombatSetup: Sending combat phase change to all clients via PhaseNetworker");
                phaseNetworker.SendPhaseChangeToClients((int)GamePhaseManager.GamePhase.Combat);
            }
            else
            {
                Debug.LogWarning("CombatSetup: PhaseNetworker not found, using fallback RPC method");
            }
            
            // Also use direct RPC as fallback to ensure phase change reaches all clients
            RpcUpdateGamePhaseToCombat();
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
        if (!IsServerInitialized || fightManager == null) return;

        // Get all entities
        List<NetworkEntity> entities = GetAllSpawnedEntities<NetworkEntity>();
        
        // Separate players and pets
        var players = entities.Where(e => e.EntityType == EntityType.Player).ToList();
        var pets = entities.Where(e => e.EntityType == EntityType.Pet).ToList();
        
        AssignPlayersToPets(players, pets);
    }
    
    private void AssignPlayersToPets(List<NetworkEntity> players, List<NetworkEntity> pets)
    {
        List<NetworkEntity> availablePets = new List<NetworkEntity>(pets);
        
        foreach (NetworkEntity player in players)
        {
            if (availablePets.Count == 0) break;
            
            // Find player's own pet to avoid pairing with it
            NetworkEntity playerOwnedPet = pets.FirstOrDefault(p => p.GetOwnerEntity() == player);
            
            // Find a suitable pet to fight against
            NetworkEntity opponentPet = FindOpponentPet(availablePets, playerOwnedPet);
            
            if (opponentPet != null)
            {
                fightManager.AddFightAssignment(player, opponentPet);
                availablePets.Remove(opponentPet);
            }
        }
    }
    
    private NetworkEntity FindOpponentPet(List<NetworkEntity> availablePets, NetworkEntity playerOwnedPet)
    {
        // First try to find a pet that's not owned by the player
        foreach (NetworkEntity pet in availablePets)
        {
            if (pet != playerOwnedPet)
            {
                return pet;
            }
        }
        
        // If no other pet is available, return the first pet (even if it's the player's own)
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
        // Wait a short time to ensure all entities are spawned
        yield return new WaitForSeconds(0.2f);

        var entities = FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        if (entities.Length == 0)
        {
            Debug.LogWarning($"Client {LocalConnection.ClientId}: No NetworkEntities found yet, waiting longer...");
            yield return new WaitForSeconds(0.5f);
            entities = FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
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

        // Monitor until all setups are complete
        bool allComplete = false;
        int maxWaitTime = 30; // Maximum 30 seconds
        float startTime = Time.time;

        while (!allComplete && (Time.time - startTime) < maxWaitTime)
        {
            allComplete = true;
            string statusUpdate = $"Client {LocalConnection.ClientId} setup status:";

            foreach (var setup in requiredSetups)
            {
                if (setup == null) continue; // Entity might have been destroyed

                NetworkEntity entity = setup.GetComponent<NetworkEntity>();
                bool isComplete = setup.IsSetupComplete;
                statusUpdate += $"\n - Entity '{entity.name}' (ID: {entity.ObjectId}): IsSetupComplete = {isComplete}";

                if (!isComplete)
                {
                    allComplete = false;
                }
            }

            if (!allComplete)
            {
                // Log status every 2 seconds while waiting
                if (Mathf.FloorToInt(Time.time - startTime) % 2 == 0 && Time.time - startTime > 1f)
                {
                    /* Debug.Log(statusUpdate); */
                }
                yield return new WaitForSeconds(0.1f); // Check every 100ms
            }
        }

        if (allComplete)
        {
            /* Debug.Log($"Client {LocalConnection.ClientId} combat setup check passed! All entities ready. Notifying server."); */
            ServerSetClientReady(LocalConnection);
        }
        else
        {
            Debug.LogError($"Client {LocalConnection.ClientId} combat setup check timed out after {maxWaitTime} seconds. Some entities never completed setup.");
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
            Debug.Log("CombatSetup: Found LoadingScreenManager, hiding loading screen");
            loadingScreenManager.HideLoadingScreen();
        }
        else
        {
            // Try to find LoadingScreenManager if reference is missing
            loadingScreenManager = FindFirstObjectByType<LoadingScreenManager>();
            if (loadingScreenManager != null)
            {
                Debug.Log("CombatSetup: Found LoadingScreenManager via FindFirstObjectByType, hiding loading screen");
                loadingScreenManager.HideLoadingScreen();
            }
            else
            {
                Debug.LogWarning("CombatSetup: LoadingScreenManager not found, cannot hide loading screen");
            }
        }
    }
} 