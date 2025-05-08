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

    // Prefabs for card game objects (visual representation in scene)
    [SerializeField] public GameObject cardGamePrefab; // Made public

    private bool setupDone = false;

    private void Awake()
    {
        // It's better to find these in OnStartServer or ensure they are ready
        // as their Awake might not have run yet depending on script execution order.
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!IsServerStarted) return;

        Debug.Log("CombatSetup OnStartServer: Initializing combat...");

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
        if (cardGamePrefab == null) Debug.LogError("Card Game Prefab is not assigned in CombatSetup.");

        if (steamNetworkIntegration != null && fightManager != null && combatManager != null && gameManager != null && cardGamePrefab != null)
        {
            InitializeCombat();
        }
        else
        {
            Debug.LogError("CombatSetup: Missing critical references. Combat will not be initialized.");
        }
    }

    [Server]
    public void InitializeCombat()
    {
        if (setupDone) return;
        Debug.Log("CombatSetup: Server is initializing combat.");

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
            Debug.LogError("Combat Canvas reference is null.");
        }

        CombatCanvasManager canvasManager = FindFirstObjectByType<CombatCanvasManager>();
        if (canvasManager != null)
        {
            canvasManager.SetupCombatUI();
        }
        else
        {
            Debug.LogError("CombatCanvasManager not found in the scene.");
        }
    }
    
        // This method is quite problematic because it tries to instantiate a prefab and get a Card component
        // which implies the card data is ON the prefab. A real system would look up CardData (e.g. ScriptableObject)
        // based on cardId and then potentially instantiate a visual prefab, applying the data to it.
        // The current CombatSetup.cardGamePrefab is a generic visual prefab.

        // For now, returning null as this part needs a bigger refactor with a Card Database.
        // CombatManager's GetCardDataFromId also relies on this flawed approach.
    private Card SpawnCardObject(int cardId, NetworkConnection ownerConn, Transform parent)
    {
        Debug.LogWarning("SpawnCardObject in CombatSetup is a placeholder and needs a proper Card Database system.");
        return null; 
        /*
        if (cardGamePrefab == null) 
        {
            Debug.LogError("CardGamePrefab not set on CombatSetup!");
            return null;
        }

        GameObject cardInstanceGo = Instantiate(cardGamePrefab, parent);
        Card cardComponent = cardInstanceGo.GetComponent<Card>();
        
        // TODO: Populate cardComponent with actual data based on cardId from a CardDatabase/ResourceManager
        // cardComponent.InitializeCard(cardId, ...);

        Debug.Log($"Visually spawned card with ID {cardId} for owner {ownerConn.ClientId} under {parent.name}. (This is simplified)");
        return cardComponent; // Placeholder
        */
    }
} 