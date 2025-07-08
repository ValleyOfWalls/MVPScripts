using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// Manages online/network-synchronized game state and rules.
/// This includes game rules, card settings, and other state that must be consistent across all players.
/// Spawned by the host and synchronized to all clients when they join.
/// </summary>
public class OnlineGameManager : NetworkBehaviour
{
    [Header("Singleton")]
    public static OnlineGameManager Instance { get; private set; }

    [Header("Game Session Settings")]
    [SerializeField, Tooltip("Game session ID for this match")]
    private string gameSessionId = "";

    [Header("Player Settings")]
    [SerializeField, Tooltip("Initial number of cards drawn at game start")]
    private int playerInitialDraw = 5;

    [SerializeField, Tooltip("Target number of cards in player's hand after drawing each round")]
    private int playerHandTarget = 5;

    [Header("Pet Settings")]
    [SerializeField, Tooltip("Initial number of cards drawn at game start")]
    private int petInitialDraw = 3;

    [SerializeField, Tooltip("Target number of cards in pet's hand after drawing each round")]
    private int petHandTarget = 3;

    [SerializeField, Tooltip("Delay in seconds between each card played by opponent pets")]
    private float petCardPlayDelay = 1.5f;

    [Header("Draft Settings")]
    [SerializeField, Tooltip("Number of cards in each draft pack")]
    private int draftPackSize = 15;

    [Header("Shop Settings")]
    [SerializeField, Tooltip("Number of cards available in the shop")]
    private int shopSize = 6;

    [SerializeField, Tooltip("Minimum cost for cards in the shop")]
    private int shopMinCardCost = 0;

    [SerializeField, Tooltip("Maximum cost for cards in the shop")]
    private int shopMaxCardCost = 10;

    [Header("Damage Modifiers")]
    [SerializeField, Tooltip("Enable critical hit system")]
    private bool criticalHitsEnabled = true;

    [SerializeField, Tooltip("Base chance of critical hit occurring (0.05 = 5%)")]
    private float baseCriticalChance = 0.05f;

    [SerializeField, Tooltip("Damage multiplier when a critical hit occurs")]
    private float criticalHitModifier = 1.5f;

    [SerializeField, Tooltip("Damage multiplier when attacker has Weak status")]
    private float weakStatusModifier = 0.75f;

    [SerializeField, Tooltip("Damage multiplier when target has Break status")]
    private float breakStatusModifier = 1.25f;

    // Network synchronized variables
    public readonly SyncVar<bool> RandomizationEnabled = new SyncVar<bool>();
    public readonly SyncVar<string> SessionId = new SyncVar<string>();
    
    // Player settings SyncVars
    public readonly SyncVar<int> PlayerDrawAmount = new SyncVar<int>();
    public readonly SyncVar<int> PetDrawAmount = new SyncVar<int>();
    public readonly SyncVar<int> PlayerMaxEnergy = new SyncVar<int>();
    public readonly SyncVar<int> PetMaxEnergy = new SyncVar<int>();
    public readonly SyncVar<int> PlayerMaxHealth = new SyncVar<int>();
    public readonly SyncVar<int> PetMaxHealth = new SyncVar<int>();
    public readonly SyncVar<int> PlayerTargetHandSize = new SyncVar<int>();
    public readonly SyncVar<int> PetTargetHandSize = new SyncVar<int>();
    public readonly SyncVar<float> PetCardPlayDelay = new SyncVar<float>();

    // Damage modifier SyncVars
    public readonly SyncVar<bool> CriticalHitsEnabled = new SyncVar<bool>();
    public readonly SyncVar<float> BaseCriticalChance = new SyncVar<float>();
    public readonly SyncVar<float> CriticalHitModifier = new SyncVar<float>();
    public readonly SyncVar<float> WeakStatusModifier = new SyncVar<float>();
    public readonly SyncVar<float> BreakStatusModifier = new SyncVar<float>();

    // Draft settings SyncVars
    public readonly SyncVar<int> DraftPackSize = new SyncVar<int>();

    // Shop settings SyncVars
    public readonly SyncVar<int> ShopSize = new SyncVar<int>();
    public readonly SyncVar<int> ShopMinCardCost = new SyncVar<int>();
    public readonly SyncVar<int> ShopMaxCardCost = new SyncVar<int>();

    private void Awake()
    {
        Debug.Log("[NETSPAWN] OnlineGameManager.Awake() called");
        
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[NETSPAWN] OnlineGameManager: Initialized and set as singleton - Instance is now available!");
        }
        else
        {
            Debug.LogWarning("[NETSPAWN] OnlineGameManager: Instance already exists, this should not happen with proper network spawning");
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        Debug.Log("[NETSPAWN] OnlineGameManager: OnStartServer - Initializing network game state");
        
        // Generate unique session ID
        gameSessionId = System.Guid.NewGuid().ToString();
        SessionId.Value = gameSessionId;
        
        // Get randomization setting from OfflineGameManager
        bool randomizationEnabled = false;
        if (OfflineGameManager.Instance != null)
        {
            randomizationEnabled = OfflineGameManager.Instance.EnableRandomizedCards;
            Debug.Log($"OnlineGameManager: Randomization setting from OfflineGameManager: {randomizationEnabled}");
        }
        else
        {
            Debug.LogWarning("OnlineGameManager: OfflineGameManager not found, defaulting randomization to false");
        }
        
        // Initialize all SyncVars from serialized fields
        RandomizationEnabled.Value = randomizationEnabled;
        
        // Initialize player/pet settings
        PlayerDrawAmount.Value = playerInitialDraw;
        PetDrawAmount.Value = petInitialDraw;
        PlayerTargetHandSize.Value = playerHandTarget;
        PetTargetHandSize.Value = petHandTarget;
        PetCardPlayDelay.Value = petCardPlayDelay;
        
        // Initialize damage modifiers
        CriticalHitsEnabled.Value = criticalHitsEnabled;
        BaseCriticalChance.Value = baseCriticalChance;
        CriticalHitModifier.Value = criticalHitModifier;
        WeakStatusModifier.Value = weakStatusModifier;
        BreakStatusModifier.Value = breakStatusModifier;
        
        // Initialize draft settings
        DraftPackSize.Value = draftPackSize;
        
        // Initialize shop settings
        ShopSize.Value = shopSize;
        ShopMinCardCost.Value = shopMinCardCost;
        ShopMaxCardCost.Value = shopMaxCardCost;
        
        Debug.Log($"OnlineGameManager: Server initialized with session ID: {gameSessionId}");
        Debug.Log($"OnlineGameManager: Randomization enabled: {RandomizationEnabled.Value}");
        LogGameSettings();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        Debug.Log("OnlineGameManager: OnStartClient - Receiving network game state from server");
        Debug.Log($"OnlineGameManager: Session ID: {SessionId.Value}");
        Debug.Log($"OnlineGameManager: Randomization enabled: {RandomizationEnabled.Value}");
        
        // Subscribe to important changes
        RandomizationEnabled.OnChange += OnRandomizationChanged;
        SessionId.OnChange += OnSessionIdChanged;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Unsubscribe from changes
        RandomizationEnabled.OnChange -= OnRandomizationChanged;
        SessionId.OnChange -= OnSessionIdChanged;
    }

    /// <summary>
    /// Called when randomization setting changes
    /// </summary>
    private void OnRandomizationChanged(bool prev, bool next, bool asServer)
    {
        Debug.Log($"OnlineGameManager: Randomization setting changed from {prev} to {next} (Server: {asServer})");
        
        // Notify other systems that randomization setting has changed
        if (!asServer)
        {
            // This is a client receiving the change from server
            // Trigger any necessary updates for client-side systems
        }
    }

    /// <summary>
    /// Called when session ID changes
    /// </summary>
    private void OnSessionIdChanged(string prev, string next, bool asServer)
    {
        Debug.Log($"OnlineGameManager: Session ID changed from '{prev}' to '{next}' (Server: {asServer})");
    }

    /// <summary>
    /// Server-only method to update randomization setting during game
    /// </summary>
    [Server]
    public void SetRandomizationEnabled(bool enabled)
    {
        RandomizationEnabled.Value = enabled;
        Debug.Log($"OnlineGameManager: [Server] Randomization setting updated to {enabled}");
    }

    /// <summary>
    /// Server-only method to update player max stats (called from character selection)
    /// </summary>
    [Server]
    public void SetPlayerMaxStats(int maxHealth, int maxEnergy)
    {
        PlayerMaxHealth.Value = maxHealth;
        PlayerMaxEnergy.Value = maxEnergy;
        Debug.Log($"OnlineGameManager: [Server] Player max stats set - Health: {maxHealth}, Energy: {maxEnergy}");
    }

    /// <summary>
    /// Server-only method to update pet max stats (called from character selection)
    /// </summary>
    [Server]
    public void SetPetMaxStats(int maxHealth, int maxEnergy)
    {
        PetMaxHealth.Value = maxHealth;
        PetMaxEnergy.Value = maxEnergy;
        Debug.Log($"OnlineGameManager: [Server] Pet max stats set - Health: {maxHealth}, Energy: {maxEnergy}");
    }

    /// <summary>
    /// Log current game settings for debugging
    /// </summary>
    [ContextMenu("Log Game Settings")]
    public void LogGameSettings()
    {
        Debug.Log($"OnlineGameManager: Current Game Settings:");
        Debug.Log($"  Session ID: {SessionId.Value}");
        Debug.Log($"  Randomization: {RandomizationEnabled.Value}");
        Debug.Log($"  Player - Draw: {PlayerDrawAmount.Value}, Target Hand: {PlayerTargetHandSize.Value}");
        Debug.Log($"  Player - Max Health: {PlayerMaxHealth.Value}, Max Energy: {PlayerMaxEnergy.Value}");
        Debug.Log($"  Pet - Draw: {PetDrawAmount.Value}, Target Hand: {PetTargetHandSize.Value}");
        Debug.Log($"  Pet - Max Health: {PetMaxHealth.Value}, Max Energy: {PetMaxEnergy.Value}, Play Delay: {PetCardPlayDelay.Value}");
        Debug.Log($"  Damage - Crits: {CriticalHitsEnabled.Value}, Crit Chance: {BaseCriticalChance.Value}, Crit Mult: {CriticalHitModifier.Value}");
        Debug.Log($"  Damage - Weak Mult: {WeakStatusModifier.Value}, Break Mult: {BreakStatusModifier.Value}");
        Debug.Log($"  Draft - Pack Size: {DraftPackSize.Value}");
        Debug.Log($"  Shop - Size: {ShopSize.Value}, Cost Range: {ShopMinCardCost.Value}-{ShopMaxCardCost.Value}");
    }

    /// <summary>
    /// Check if we're the server/host
    /// </summary>
    public bool IsHost => IsServerInitialized;

    /// <summary>
    /// Get the current session info
    /// </summary>
    public string GetSessionInfo()
    {
        return $"Session: {SessionId.Value}, Randomization: {RandomizationEnabled.Value}, Players Online: {ClientManager.Clients.Count}";
    }
} 