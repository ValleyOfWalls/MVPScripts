using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;

public class CombatSetup : NetworkBehaviour // Needs to be a NetworkBehaviour to perform server-side setup
{
    [SerializeField] private GameObject combatCanvas; // Should already be active when this runs
    
    // References to be set in inspector or found
    private SteamAndLobbyHandler steamAndLobbyHandler;
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

        steamAndLobbyHandler = SteamAndLobbyHandler.Instance;
        fightManager = FindFirstObjectByType<FightManager>();
        combatManager = FindFirstObjectByType<CombatManager>();
        combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
        gameManager = FindFirstObjectByType<GameManager>();

        if (steamAndLobbyHandler == null) Debug.LogError("SteamAndLobbyHandler not found by CombatSetup.");
        if (fightManager == null) Debug.LogError("FightManager not found by CombatSetup.");
        if (combatManager == null) Debug.LogError("CombatManager not found by CombatSetup.");
        if (combatCanvasManager == null) Debug.LogError("CombatCanvasManager not found by CombatSetup.");
        if (gameManager == null) Debug.LogError("GameManager not found by CombatSetup.");
        if (cardGamePrefab == null) Debug.LogError("Card Game Prefab is not assigned in CombatSetup.");

        if (steamAndLobbyHandler != null && fightManager != null && combatManager != null && gameManager != null && cardGamePrefab != null)
        {
            InitializeCombat();
        }
        else
        {
            Debug.LogError("CombatSetup: Missing critical references. Combat will not be initialized.");
        }
    }

    [Server]
    private void InitializeCombat()
    {
        if (setupDone) return;
        Debug.Log("CombatSetup: Server is initializing combat.");

        // 1. Create/Instantiate cards from player and pet decks
        // This step is tricky. "Spawning cards under a deck gameobject" implies physical cards.
        // NetworkPlayer/Pet already have card IDs. We need a system to map IDs to CardData (prefabs/ScriptableObjects).
        // Then, for visual representation, CombatSetup (server-side) could tell clients to display these cards.
        // Or, if Card GameObjects are NetworkObjects themselves, spawn them.
        // Let's assume NetworkPlayer/Pet have StarterDeckPrefabs and their currentDeckCardIds are populated.
        // We will instantiate visual card representations based on these IDs.

        // This part is more about preparing the card data structures than spawning GameObjects immediately,
        // as HandManager will deal with drawing and moving visual cards.
        // However, the request mentions spawning under deck gameobject.
        // Let's assume NetworkPlayer/Pet have DeckTransforms where these *could* be parented if physically represented from start.
        // For now, ensure NetworkPlayer/Pet have their decks initialized.
        // The actual spawning of card GameObjects will likely be handled by HandManager when cards are drawn.

        Debug.Log("Card data initialization in NetworkPlayer/Pet should already be done. Visual card spawning handled by HandManager/CombatManager during draw.");

        // 2. Assign fights
        AssignFights();

        // 3. Trigger CombatCanvasManager (usually client-side for local UI setup)
        // This is tricky. CombatCanvasManager is likely a local script.
        // The server can send an RPC to all clients to trigger their local CombatCanvasManager setup.
        RpcTriggerCombatCanvasManagerSetup();

        // 4. Trigger CombatManager
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
    private void AssignFights()
    {
        if (fightManager == null || steamAndLobbyHandler == null)
        {
            Debug.LogError("Cannot assign fights: FightManager or SteamAndLobbyHandler is missing.");
            return;
        }

        List<NetworkPlayer> players = steamAndLobbyHandler.networkPlayers.Select(no => no.GetComponent<NetworkPlayer>()).Where(p => p != null).ToList();
        List<NetworkPet> pets = steamAndLobbyHandler.networkPets.Select(no => no.GetComponent<NetworkPet>()).Where(p => p != null).ToList();

        // Basic assignment: try to pair each player with a unique pet.
        // This needs more robust logic for different numbers of players/pets.
        // For now, simple 1-to-1 if counts match or take available.
        // Also needs to handle cases where pets are owned by players and shouldn't fight their own owner's pet (or should, depending on game rules)

        Debug.Log($"Assigning fights. Players: {players.Count}, Pets: {pets.Count}");

        List<NetworkPet> availablePets = new List<NetworkPet>(pets);
        foreach (NetworkPlayer player in players)
        {
            if (availablePets.Count == 0)
            {
                Debug.LogWarning($"Not enough pets to assign to all players. Player {player.PlayerName} will not have an opponent.");
                continue;
            }

            // Simple assignment: take the first available pet.
            // TODO: Add logic to prevent a player fighting their own pet if pets are tied to specific players.
            NetworkPet opponentPet = availablePets[0];
            availablePets.RemoveAt(0);

            fightManager.AddFightAssignment(player, opponentPet);
            Debug.Log($"Assigned Player {player.PlayerName} (ID: {player.ObjectId}) vs Pet {opponentPet.PetName} (ID: {opponentPet.ObjectId})");
        }

        if (availablePets.Count > 0)
        {
            Debug.LogWarning($"{availablePets.Count} pets were not assigned to any fight.");
        }
    }

    [ObserversRpc]
    private void RpcTriggerCombatCanvasManagerSetup()
    {
        Debug.Log("RpcTriggerCombatCanvasManagerSetup called on clients.");
        CombatCanvasManager localCombatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
        if (localCombatCanvasManager != null)
        {
            localCombatCanvasManager.SetupCombatUI();
        }
        else
        {
            Debug.LogError("Local CombatCanvasManager not found on client.");
        }
    }
    
    // This method will be called by CombatManager or other systems that need card data.
    // Card instances are GameObjects based on cardGamePrefab, with a Card component holding data.
    [Server]
    public Card SpawnCardObject(NetworkConnection ownerConn, Transform parent, int cardId /* or CardData */)
    {
        // This method is quite problematic because it tries to instantiate a prefab and get a Card component
        // which implies the card data is ON the prefab. A real system would look up CardData (e.g. ScriptableObject)
        // based on cardId and then potentially instantiate a visual prefab, applying the data to it.
        // The current CombatSetup.cardGamePrefab is a generic visual prefab.

        // For now, returning null as this part needs a bigger refactor with a Card Database.
        // CombatManager's GetCardDataFromId also relies on this flawed approach.
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