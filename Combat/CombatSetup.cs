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
    
    private SteamNetworkIntegration steamNetworkIntegration;
    private bool setupCompleted = false;

    private readonly SyncDictionary<uint, bool> readyPlayers = new SyncDictionary<uint, bool>();

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
        
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        steamNetworkIntegration = SteamNetworkIntegration.Instance;
        
        if (fightManager == null) fightManager = FindFirstObjectByType<FightManager>();
        if (combatManager == null) combatManager = FindFirstObjectByType<CombatManager>();
        if (combatCanvasManager == null) combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (gamePhaseManager == null) gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
        
        RegisterCombatCanvasWithPhaseManager();
    }

    [Server]
    public void InitializeCombat()
    {
        if (!IsServerStarted || setupCompleted) return;
        
        ResolveReferences();

        if (!AreRequiredComponentsAvailable())
        {
            Debug.LogError("CombatSetup: Missing critical references. Combat will not be initialized.");
            return;
        }
        
        TransitionToPhase();
        SetupCombatDecks();
        AssignFights();
        EnsurePlayersAreObservers();
        RpcTriggerCombatCanvasManagerSetup();
        
        if (combatManager != null)
        {
            combatManager.StartCombat();
        }
        
        setupCompleted = true;
    }
    
    private bool AreRequiredComponentsAvailable()
    {
        return steamNetworkIntegration != null && 
               fightManager != null && 
               combatManager != null && 
               gameManager != null;
    }
    
    private void TransitionToPhase()
    {
        if (gamePhaseManager != null)
        {
            gamePhaseManager.SetCombatPhase();
        }
    }

    [Server]
    private void SetupCombatDecks()
    {
        if (FishNet.InstanceFinder.NetworkManager == null || 
            !FishNet.InstanceFinder.NetworkManager.ServerManager.Started)
        {
            return;
        }

        SetupPlayerDecks();
        SetupPetDecks();
    }
    
    private void SetupPlayerDecks()
    {
        List<NetworkPlayer> players = GetAllSpawnedEntities<NetworkPlayer>();
        
        foreach (NetworkPlayer player in players)
        {
            InitializeEntityDeck(player.gameObject);
        }
    }
    
    private void SetupPetDecks()
    {
        List<NetworkPet> pets = GetAllSpawnedEntities<NetworkPet>();
        
        foreach (NetworkPet pet in pets)
        {
            InitializeEntityDeck(pet.gameObject);
        }
    }
    
    private List<T> GetAllSpawnedEntities<T>() where T : NetworkBehaviour
    {
        return FishNet.InstanceFinder.NetworkManager.ServerManager.Objects.Spawned.Values
            .Select(nob => nob.GetComponent<T>())
            .Where(p => p != null)
            .ToList();
    }

    [Server]
    private void AssignFights()
    {
        if (fightManager == null || 
            FishNet.InstanceFinder.NetworkManager == null || 
            !FishNet.InstanceFinder.NetworkManager.ServerManager.Started)
        {
            return;
        }

        List<NetworkPlayer> players = GetAllSpawnedEntities<NetworkPlayer>();
        List<NetworkPet> pets = GetAllSpawnedEntities<NetworkPet>();
        
        AssignPlayersToPets(players, pets);
    }
    
    private void AssignPlayersToPets(List<NetworkPlayer> players, List<NetworkPet> pets)
    {
        List<NetworkPet> availablePets = new List<NetworkPet>(pets);
        
        foreach (NetworkPlayer player in players)
        {
            if (availablePets.Count == 0) break;
            
            // Find player's own pet to avoid pairing with it
            uint playerOwnedPetId = GetPlayerOwnedPetId(player, pets);
            
            // Find a suitable pet to fight against
            NetworkPet opponentPet = FindOpponentPet(availablePets, playerOwnedPetId);
            
            if (opponentPet != null)
            {
                fightManager.AddFightAssignment(player, opponentPet);
                availablePets.Remove(opponentPet);
            }
        }
    }
    
    private uint GetPlayerOwnedPetId(NetworkPlayer player, List<NetworkPet> allPets)
    {
        foreach (NetworkPet pet in allPets)
        {
            if (pet.OwnerPlayerObjectId.Value == (uint)player.ObjectId)
            {
                return (uint)pet.ObjectId;
            }
        }
        return 0;
    }
    
    private NetworkPet FindOpponentPet(List<NetworkPet> availablePets, uint playerOwnedPetId)
    {
        // First try to find a pet that's not owned by the player
        foreach (NetworkPet pet in availablePets)
        {
            if ((uint)pet.ObjectId != playerOwnedPetId)
            {
                return pet;
            }
        }
        
        // If no other pet is available, return the first pet (even if it's the player's own)
        return availablePets.Count > 0 ? availablePets[0] : null;
    }

    [Server]
    private void EnsurePlayersAreObservers()
    {
        if (FishNet.InstanceFinder.NetworkManager == null || 
            combatManager == null ||
            !FishNet.InstanceFinder.NetworkManager.ServerManager.Started)
        {
            return;
        }
        
        NetworkObject combatManagerNob = combatManager.GetComponent<NetworkObject>();
        if (combatManagerNob == null) return;

        // If the object isn't spawned yet, spawn it with default settings
        if (!combatManagerNob.IsSpawned)
        {
            FishNet.InstanceFinder.ServerManager.Spawn(combatManagerNob);
            return;
        }
        
        // For an already spawned object, update its visibility to all clients
        // This is typically handled by FishNet's default observation system
        // We can force an observation update or use a custom observer if needed,
        // but in most cases this isn't necessary if the object is properly spawned
        
        // If you need custom observer logic, you can implement this using 
        // a custom NetworkObserver component or other FishNet methods
    }

    [ObserversRpc]
    private void RpcTriggerCombatCanvasManagerSetup()
    {
        if (combatCanvasManager != null)
        {
            StartCoroutine(SetupCombatUIWithDelay());
        }
    }

    private IEnumerator SetupCombatUIWithDelay()
    {
        // Wait one frame to ensure everything is properly set up
        yield return null;
        
        if (combatCanvasManager != null)
        {
            combatCanvasManager.SetupCombatUI();
        }
    }

    /// <summary>
    /// Initializes the combat deck from the entity's network deck
    /// </summary>
    [Server]
    private void InitializeEntityDeck(GameObject entity)
    {
        if (!IsServerInitialized || entity == null) return;
        
        // Get required components
        NetworkEntityDeck entityDeck = entity.GetComponent<NetworkEntityDeck>();
        CombatDeck combatDeck = entity.GetComponent<CombatDeck>();
        CombatDeckSetup deckSetup = entity.GetComponent<CombatDeckSetup>();
        
        if (entityDeck == null)
        {
            Debug.LogError($"Cannot initialize combat deck: {entity.name} has no NetworkEntityDeck component");
            return;
        }
        
        if (combatDeck == null)
        {
            Debug.LogError($"Cannot initialize combat deck: {entity.name} has no CombatDeck component");
            return;
        }
        
        if (deckSetup == null)
        {
            Debug.LogError($"Cannot initialize combat deck: {entity.name} has no CombatDeckSetup component");
            return;
        }
        
        // Get the card IDs from the entity's network deck
        List<int> deckCardIds = entityDeck.GetAllCardIds();
        
        Debug.Log($"Initializing combat deck for {entity.name} with {deckCardIds.Count} cards from network deck");
        
        // Let the combat deck setup handle the actual deck initialization
        // This will spawn the actual card GameObjects for each card ID
        deckSetup.SetupCombatDeck(deckCardIds);
    }
} 