using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Managing; // Added for NetworkManager
using FishNet.Observing; // Added for NetworkObserver
using System.Collections;

/// <summary>
/// Initializes the combat phase including entity deck setup, fight assignments, and UI preparation.
/// Attach to: A NetworkObject in the scene that coordinates the combat setup process.
/// </summary>
public class CombatSetup : NetworkBehaviour // Needs to be a NetworkBehaviour to perform server-side setup
{
    [SerializeField] private GameObject combatCanvas; // Should already be active when this runs
    
    // References to be set in inspector for better dependency management
    [Header("Required Components")]
    [SerializeField] private FightManager fightManager;
    [SerializeField] private CombatManager combatManager;
    [SerializeField] private CombatCanvasManager combatCanvasManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GamePhaseManager gamePhaseManager; // Added GamePhaseManager reference
    
    // Optional reference that can be resolved at runtime
    private SteamNetworkIntegration steamNetworkIntegration;

    private bool setupDone = false;

    private void Awake()
    {
        // Register our combat canvas with the GamePhaseManager if available
        if (gamePhaseManager == null)
        {
            gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
        }
        
        if (gamePhaseManager != null && combatCanvas != null)
        {
            gamePhaseManager.SetCombatCanvas(combatCanvas);
        }
    }

    // We'll initialize references here but NOT automatically start combat setup
    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!IsServerStarted) return;

        Debug.Log("CombatSetup OnStartServer: Initializing references only, combat will be triggered later...");

        // Initialize any missing references
        ResolveReferences();
    }

    // Centralized method to resolve component references
    private void ResolveReferences()
    {
        // Only try to find components that aren't already assigned in inspector
        steamNetworkIntegration = SteamNetworkIntegration.Instance;
        
        if (fightManager == null) fightManager = FindFirstObjectByType<FightManager>();
        if (combatManager == null) combatManager = FindFirstObjectByType<CombatManager>();
        if (combatCanvasManager == null) combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (gamePhaseManager == null) gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();

        // Log any missing components as errors
        if (steamNetworkIntegration == null) Debug.LogError("SteamNetworkIntegration not found by CombatSetup.");
        if (fightManager == null) Debug.LogError("FightManager not found by CombatSetup.");
        if (combatManager == null) Debug.LogError("CombatManager not found by CombatSetup.");
        if (combatCanvasManager == null) Debug.LogError("CombatCanvasManager not found by CombatSetup.");
        if (gameManager == null) Debug.LogError("GameManager not found by CombatSetup.");
        if (gamePhaseManager == null) Debug.LogError("GamePhaseManager not found by CombatSetup.");
        
        // Register our combat canvas with the GamePhaseManager if not done in Awake
        if (gamePhaseManager != null && combatCanvas != null)
        {
            gamePhaseManager.SetCombatCanvas(combatCanvas);
        }
    }

    [Server]
    public void InitializeCombat()
    {
        if (!IsServerStarted) 
        {
            Debug.LogWarning("CombatSetup.InitializeCombat called but server is not started.");
            return;
        }

        if (setupDone) 
        {
            Debug.Log("CombatSetup: Combat setup already done. Ignoring duplicate call.");
            return;
        }

        Debug.Log("CombatSetup: Server is initializing combat.");

        // Ensure we have all the required references
        ResolveReferences();

        // Verify all required components are available
        if (steamNetworkIntegration == null || fightManager == null || combatManager == null || 
            gameManager == null)
        {
            Debug.LogError("CombatSetup: Missing critical references. Combat will not be initialized.");
            return;
        }

        // Update the GamePhaseManager to transition to Combat phase
        if (gamePhaseManager != null)
        {
            gamePhaseManager.SetCombatPhase();
            Debug.Log("CombatSetup: Triggered GamePhaseManager to set Combat phase");
        }
        else
        {
            Debug.LogWarning("CombatSetup: GamePhaseManager is null, cannot transition game phase properly");
        }

        // 1. Setup combat decks for all players and pets
        Debug.Log("CombatSetup: Starting to setup combat decks...");
        SetupCombatDecks();
        Debug.Log("CombatSetup: Combat decks setup complete.");

        // 2. Assign fights
        Debug.Log("CombatSetup: Starting to assign fights...");
        AssignFights();
        Debug.Log("CombatSetup: Fight assignments complete.");
        
        // 3. Add all player connections as observers to the CombatManager
        Debug.Log("CombatSetup: Setting up player observers...");
        EnsurePlayersAreObservers();
        Debug.Log("CombatSetup: Player observers setup complete.");

        // 4. Trigger CombatCanvasManager (usually client-side for local UI setup)
        Debug.Log("CombatSetup: Triggering combat canvas setup on clients...");
        RpcTriggerCombatCanvasManagerSetup();
        Debug.Log("CombatSetup: Combat canvas RPC sent to clients.");

        // 5. Trigger CombatManager to start combat
        if (combatManager != null)
        {
            Debug.Log("CombatSetup: Triggering CombatManager to start combat process.");
            combatManager.StartCombat();
            Debug.Log("CombatSetup: CombatManager.StartCombat() called successfully.");
        }
        else
        {
            Debug.LogError("CombatManager reference is null in CombatSetup.");
        }
        
        setupDone = true;
        Debug.Log("CombatSetup: Initialization complete.");
    }

    [Server]
    private void SetupCombatDecks()
    {
        if (FishNet.InstanceFinder.NetworkManager == null || !FishNet.InstanceFinder.NetworkManager.ServerManager.Started)
        {
            Debug.LogError("Cannot setup combat decks: FishNet NetworkManager is not available or server not started.");
            return;
        }

        List<NetworkPlayer> players = FishNet.InstanceFinder.NetworkManager.ServerManager.Objects.Spawned.Values
            .Select(nob => nob.GetComponent<NetworkPlayer>())
            .Where(p => p != null)
            .ToList();

        List<NetworkPet> pets = FishNet.InstanceFinder.NetworkManager.ServerManager.Objects.Spawned.Values
            .Select(nob => nob.GetComponent<NetworkPet>())
            .Where(p => p != null)
            .ToList();

        // Setup combat decks for players
        foreach (NetworkPlayer player in players)
        {
            CombatDeckSetup deckSetup = player.GetComponent<CombatDeckSetup>();
            if (deckSetup != null)
            {
                deckSetup.SetupCombatDeck();
                Debug.Log($"Setup combat deck for player {player.PlayerName.Value}");
            }
            else
            {
                Debug.LogError($"CombatDeckSetup component not found on player {player.PlayerName.Value}");
            }
        }

        // Setup combat decks for pets
        foreach (NetworkPet pet in pets)
        {
            CombatDeckSetup deckSetup = pet.GetComponent<CombatDeckSetup>();
            if (deckSetup != null)
            {
                deckSetup.SetupCombatDeck();
                Debug.Log($"Setup combat deck for pet {pet.PetName.Value}");
            }
            else
            {
                Debug.LogError($"CombatDeckSetup component not found on pet {pet.PetName.Value}");
            }
        }
    }

    [Server]
    private void AssignFights()
    {
        if (fightManager == null)
        {
            Debug.LogError("Cannot assign fights: FightManager is missing.");
            return;
        }

        if (FishNet.InstanceFinder.NetworkManager == null || !FishNet.InstanceFinder.NetworkManager.ServerManager.Started)
        {
            Debug.LogError("Cannot assign fights: FishNet NetworkManager is not available or server not started.");
            return;
        }

        List<NetworkPlayer> players = FishNet.InstanceFinder.NetworkManager.ServerManager.Objects.Spawned.Values
            .Select(nob => nob.GetComponent<NetworkPlayer>())
            .Where(p => p != null)
            .ToList();

        List<NetworkPet> pets = FishNet.InstanceFinder.NetworkManager.ServerManager.Objects.Spawned.Values
            .Select(nob => nob.GetComponent<NetworkPet>())
            .Where(p => p != null)
            .ToList();

        Debug.Log($"Assigning fights. Players: {players.Count}, Pets: {pets.Count}");

        List<NetworkPet> availablePets = new List<NetworkPet>(pets);
        foreach (NetworkPlayer player in players)
        {
            if (availablePets.Count > 0)
            {
                // Find player's own pet to avoid pairing with it
                uint playerOwnedPetId = 0;
                foreach (NetworkPet pet in pets)
                {
                    if (pet.OwnerPlayerObjectId.Value == (uint)player.ObjectId)
                    {
                        playerOwnedPetId = (uint)pet.ObjectId;
                        break;
                    }
                }

                // Select a pet that is not owned by this player, if possible
                NetworkPet opponent = null;
                foreach (NetworkPet pet in availablePets)
                {
                    if ((uint)pet.ObjectId != playerOwnedPetId)
                    {
                        opponent = pet;
                        break;
                    }
                }

                // If we couldn't find a non-owned pet, just take the first available
                if (opponent == null && availablePets.Count > 0)
                {
                    opponent = availablePets[0];
                }

                if (opponent != null)
                {
                    fightManager.AddFightAssignment(player, opponent);
                    availablePets.Remove(opponent);
                    Debug.Log($"Assigned player {player.PlayerName.Value} to fight against pet {opponent.PetName.Value}");
                }
                else
                {
                    Debug.LogWarning($"Could not find suitable pet opponent for player {player.PlayerName.Value}");
                }
            }
            else
            {
                Debug.LogWarning($"No available pets for player {player.PlayerName.Value}");
            }
        }
    }

    [Server]
    private void EnsurePlayersAreObservers()
    {
        if (combatManager == null || fightManager == null)
        {
            Debug.LogError("CombatSetup: Cannot set up combat visibility - CombatManager or FightManager is null");
            return;
        }
        
        Debug.Log("CombatSetup: Setting up visibility using EntityVisibilityManager");
        
        // Get the EntityVisibilityManager to update visibility based on fight assignments
        EntityVisibilityManager visibilityManager = FindFirstObjectByType<EntityVisibilityManager>();
        if (visibilityManager == null)
        {
            Debug.LogError("CombatSetup: EntityVisibilityManager not found. UI visibility may not work correctly.");
        }
        else
        {
            // Tell the EntityVisibilityManager to update visibility for all entities
            // It will use the fight assignments from the FightManager to determine what each client should see
            visibilityManager.SetCombatState();
            visibilityManager.UpdateAllEntitiesVisibility();
            Debug.Log("CombatSetup: Updated entity visibility for combat");
        }
        
        // Log the fight assignments for debugging
        if (fightManager != null)
        {
            var fightAssignments = fightManager.GetAllFightAssignments();
            Debug.Log($"Combat has {fightAssignments.Count} active fights assigned");
            
            foreach (var assignment in fightAssignments)
            {
                // Find the player and pet objects to get their names
                NetworkPlayer player = null;
                NetworkPet pet = null;
                
                foreach (var nob in FishNet.InstanceFinder.NetworkManager.ServerManager.Objects.Spawned.Values)
                {
                    if (nob.ObjectId == assignment.PlayerObjectId)
                        player = nob.GetComponent<NetworkPlayer>();
                    else if (nob.ObjectId == assignment.PetObjectId)
                        pet = nob.GetComponent<NetworkPet>();
                    
                    if (player != null && pet != null) break;
                }
                
                string playerName = player != null ? player.PlayerName.Value : "Unknown";
                string petName = pet != null ? pet.PetName.Value : "Unknown";
                int clientId = player != null && player.Owner != null ? player.Owner.ClientId : -1;
                
                Debug.Log($"Fight Assignment: Player {playerName} (ID: {assignment.PlayerObjectId}, ClientId: {clientId}) " +
                         $"vs Pet {petName} (ID: {assignment.PetObjectId})");
            }
        }
    }

    [ObserversRpc]
    private void RpcTriggerCombatCanvasManagerSetup()
    {
        Debug.Log("RpcTriggerCombatCanvasManagerSetup received on client. Setting up combat UI.");
        
        // Debug: Log all NetworkPlayer objects in the scene
        var networkPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        Debug.Log($"Found {networkPlayers.Length} NetworkPlayer objects in the scene:");
        foreach (var player in networkPlayers)
        {
            Debug.Log($"  - Player: {player.PlayerName.Value}, ObjectId: {player.ObjectId}, " +
                      $"IsOwner: {player.IsOwner}, HasOwner: {player.Owner != null}, " +
                      $"OwnerId: {(player.Owner != null ? player.Owner.ClientId : -1)}");
        }
        
        // Debug: Log all NetworkPet objects in the scene
        var networkPets = FindObjectsByType<NetworkPet>(FindObjectsSortMode.None);
        Debug.Log($"Found {networkPets.Length} NetworkPet objects in the scene:");
        foreach (var pet in networkPets)
        {
            Debug.Log($"  - Pet: {pet.PetName.Value}, ObjectId: {pet.ObjectId}, " +
                      $"IsOwner: {pet.IsOwner}, HasOwner: {pet.Owner != null}, " +
                      $"OwnerId: {(pet.Owner != null ? pet.Owner.ClientId : -1)}, " +
                      $"OwnerPlayerObjectId: {pet.OwnerPlayerObjectId.Value}");
        }
        
        // Debug: Log local connection info
        var localConnection = FishNet.InstanceFinder.ClientManager.Connection;
        Debug.Log($"Local connection: {(localConnection != null ? $"ClientId: {localConnection.ClientId}" : "null")}");
        
        if (combatCanvas != null)
        {
            combatCanvas.SetActive(true);
        }
        else
        {
            Debug.LogWarning("combatCanvas is null in CombatSetup.RpcTriggerCombatCanvasManagerSetup.");
        }

        // Delay UI setup to allow FightManager time to process all network updates
        StartCoroutine(SetupCombatUIWithDelay());
    }
    
    private IEnumerator SetupCombatUIWithDelay()
    {
        // Wait a short time to ensure all FightManager assignments are received and processed
        // This solves timing issues where UI setup happens before combat assignments are fully synced
        yield return new WaitForSeconds(0.5f);
        
        // Verify FightManager has assignments before proceeding
        FightManager fightManager = FindFirstObjectByType<FightManager>();
        int maxAttempts = 5;
        int attempts = 0;
        
        while (fightManager != null && fightManager.GetAllFightAssignments().Count == 0 && attempts < maxAttempts)
        {
            Debug.Log("Waiting for FightManager to receive assignments...");
            yield return new WaitForSeconds(0.5f);
            attempts++;
        }
        
        if (combatCanvasManager == null)
        {
            // Try to find it if not already set
            combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
        }

        if (combatCanvasManager != null)
        {
            Debug.Log("Found CombatCanvasManager, initializing UI...");
            combatCanvasManager.SetupCombatUI();
        }
        else
        {
            Debug.LogError("CombatCanvasManager not found in RpcTriggerCombatCanvasManagerSetup.");
        }
    }
} 