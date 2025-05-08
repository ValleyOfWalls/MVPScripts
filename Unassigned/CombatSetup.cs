using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Managing; // Added for NetworkManager

/// <summary>
/// Initializes the combat phase including entity deck setup, fight assignments, and UI preparation.
/// Attach to: A NetworkObject in the scene that coordinates the combat setup process.
/// </summary>
public class CombatSetup : NetworkBehaviour // Needs to be a NetworkBehaviour to perform server-side setup
{
    [SerializeField] private GameObject combatCanvas; // Should already be active when this runs
    
    // References to be set in inspector or found
    private SteamNetworkIntegration steamNetworkIntegration;
    private FightManager fightManager;
    private CombatManager combatManager;
    private CombatCanvasManager combatCanvasManager;
    private GameManager gameManager;

    private bool setupDone = false;

    // We'll initialize references here but NOT automatically start combat setup
    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!IsServerStarted) return;

        Debug.Log("CombatSetup OnStartServer: Initializing references only, combat will be triggered later...");

        // Just initialize references, but don't start combat setup yet
        steamNetworkIntegration = SteamNetworkIntegration.Instance;
        fightManager = FindFirstObjectByType<FightManager>();
        combatManager = FindFirstObjectByType<CombatManager>();
        combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
        gameManager = FindFirstObjectByType<GameManager>();

        if (steamNetworkIntegration == null) Debug.LogError("SteamNetworkIntegration not found by CombatSetup.");
        if (fightManager == null) Debug.LogError("FightManager not found by CombatSetup.");
        if (combatManager == null) Debug.LogError("CombatManager not found by CombatSetup.");
        if (combatCanvasManager == null) Debug.LogError("CombatCanvasManager not found by CombatSetup.");
        if (gameManager == null) Debug.LogError("GameManager not found by CombatSetup.");
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
        if (steamNetworkIntegration == null) steamNetworkIntegration = SteamNetworkIntegration.Instance;
        if (fightManager == null) fightManager = FindFirstObjectByType<FightManager>();
        if (combatManager == null) combatManager = FindFirstObjectByType<CombatManager>();
        if (combatCanvasManager == null) combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();

        // Verify all required components are available
        if (steamNetworkIntegration == null || fightManager == null || combatManager == null || 
            gameManager == null)
        {
            Debug.LogError("CombatSetup: Missing critical references. Combat will not be initialized.");
            return;
        }

        // 1. Setup combat decks for all players and pets
        SetupCombatDecks();

        // 2. Assign fights
        AssignFights();

        // 3. Trigger CombatCanvasManager (usually client-side for local UI setup)
        // This is tricky. CombatCanvasManager is likely a local script.
        // The server can send an RPC to all clients to trigger their local CombatCanvasManager setup.
        RpcTriggerCombatCanvasManagerSetup();

        // 4. Trigger CombatManager to start combat
        if (combatManager != null)
        {
            Debug.Log("CombatSetup: Triggering CombatManager to start combat process.");
            combatManager.StartCombat();
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
        if (fightManager == null) // steamNetworkIntegration no longer directly needed for player/pet lists here
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

        // Basic assignment: try to pair each player with a unique pet.
        // This needs more robust logic for different numbers of players/pets.
        // For now, simple 1-to-1 if counts match or take available.
        // Also needs to handle cases where pets are owned by players and shouldn't fight their own owner's pet (or should, depending on game rules)

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

    [ObserversRpc]
    private void RpcTriggerCombatCanvasManagerSetup()
    {
        Debug.Log("RpcTriggerCombatCanvasManagerSetup received on client. Setting up combat UI.");
        if (combatCanvas != null)
        {
            combatCanvas.SetActive(true);
        }
        else
        {
            Debug.LogWarning("combatCanvas is null in CombatSetup.RpcTriggerCombatCanvasManagerSetup.");
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