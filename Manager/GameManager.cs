using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// Centralized manager for global game rules and settings.
/// Attach to: A persistent NetworkObject in the scene that will manage game-wide rules.
/// </summary>
public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Player Card Settings")]
    [SerializeField, Tooltip("Number of cards a player draws on the first round")] 
    private int playerInitialDraw = 5;
    [SerializeField, Tooltip("Target number of cards in player's hand after drawing each round")]
    private int playerHandTarget = 3;
    [SerializeField, Tooltip("Maximum energy a player can have")]
    private int playerMaxEnergyAmount = 3;
    [SerializeField, Tooltip("Maximum health a player can have")]
    private int playerMaxHealthAmount = 100;

    [Header("Pet Card Settings")]
    [SerializeField, Tooltip("Number of cards a pet draws on the first round")]
    private int petInitialDraw = 3;
    [SerializeField, Tooltip("Target number of cards in pet's hand after drawing each round")]
    private int petHandTarget = 3;
    [SerializeField, Tooltip("Maximum energy a pet can have")]
    private int petMaxEnergyAmount = 2;
    [SerializeField, Tooltip("Maximum health a pet can have")]
    private int petMaxHealthAmount = 50;

    [Header("Game Rules")]
    [SerializeField, Tooltip("If true, players will automatically be set to ready when they join the lobby.")]
    public bool AutoReadyPlayersOnJoin = false;

    [Header("Draft Settings")]
    [SerializeField, Tooltip("Number of cards in each draft pack")]
    private int draftPackSize = 4;

    [Header("Damage Modifiers")]
    [SerializeField, Tooltip("If true, critical hits can occur during combat")]
    private bool criticalHitsEnabled = true;
    [SerializeField, Tooltip("Base chance of critical hit occurring (0.05 = 5%)")]
    private float baseCriticalChance = 0.05f;
    [SerializeField, Tooltip("Damage multiplier when a critical hit occurs")]
    private float criticalHitModifier = 1.5f;
    [SerializeField, Tooltip("Damage multiplier when attacker has Weak status")]
    private float weakStatusModifier = 0.75f;
    [SerializeField, Tooltip("Damage multiplier when target has Break status")]
    private float breakStatusModifier = 1.5f;

    // Game Rules - Synced so clients can see them if needed for UI or predictions (though server is authoritative)
    public readonly SyncVar<int> PlayerDrawAmount = new SyncVar<int>();
    public readonly SyncVar<int> PetDrawAmount = new SyncVar<int>();
    public readonly SyncVar<int> PlayerMaxEnergy = new SyncVar<int>();
    public readonly SyncVar<int> PetMaxEnergy = new SyncVar<int>();
    public readonly SyncVar<int> PlayerMaxHealth = new SyncVar<int>();
    public readonly SyncVar<int> PetMaxHealth = new SyncVar<int>();
    
    // Target hand sizes for each round - how many cards entities should have after drawing
    public readonly SyncVar<int> PlayerTargetHandSize = new SyncVar<int>();
    public readonly SyncVar<int> PetTargetHandSize = new SyncVar<int>();
    
    // Damage modifier SyncVars
    public readonly SyncVar<bool> CriticalHitsEnabled = new SyncVar<bool>();
    public readonly SyncVar<float> BaseCriticalChance = new SyncVar<float>();
    public readonly SyncVar<float> CriticalHitModifier = new SyncVar<float>();
    public readonly SyncVar<float> WeakStatusModifier = new SyncVar<float>();
    public readonly SyncVar<float> BreakStatusModifier = new SyncVar<float>();

    // Draft settings SyncVars
    public readonly SyncVar<int> DraftPackSize = new SyncVar<int>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // Removed as per FN0002
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        Debug.Log($"GameManager: OnStartServer - About to initialize SyncVars");
        Debug.Log($"GameManager: Serialized field values - playerInitialDraw: {playerInitialDraw}, playerHandTarget: {playerHandTarget}");
        Debug.Log($"GameManager: Serialized field values - petInitialDraw: {petInitialDraw}, petHandTarget: {petHandTarget}");
        
        // Initialize SyncVars from serialized fields
        PlayerDrawAmount.Value = playerInitialDraw;
        PetDrawAmount.Value = petInitialDraw;
        PlayerTargetHandSize.Value = playerHandTarget;
        PetTargetHandSize.Value = petHandTarget;
        PlayerMaxEnergy.Value = playerMaxEnergyAmount;
        PetMaxEnergy.Value = petMaxEnergyAmount;
        PlayerMaxHealth.Value = playerMaxHealthAmount;
        PetMaxHealth.Value = petMaxHealthAmount;
        
        // Initialize damage modifier SyncVars
        CriticalHitsEnabled.Value = criticalHitsEnabled;
        BaseCriticalChance.Value = baseCriticalChance;
        CriticalHitModifier.Value = criticalHitModifier;
        WeakStatusModifier.Value = weakStatusModifier;
        BreakStatusModifier.Value = breakStatusModifier;
        
        // Initialize draft settings SyncVars
        DraftPackSize.Value = draftPackSize;
        
        Debug.Log("GameManager started on Server. Initializing game rules.");
        Debug.Log($"Player settings - Initial Draw: {PlayerDrawAmount.Value}, Target Hand: {PlayerTargetHandSize.Value}, Max Energy: {PlayerMaxEnergy.Value}, Max Health: {PlayerMaxHealth.Value}");
        Debug.Log($"Pet settings - Initial Draw: {PetDrawAmount.Value}, Target Hand: {PetTargetHandSize.Value}, Max Energy: {PetMaxEnergy.Value}, Max Health: {PetMaxHealth.Value}");
        Debug.Log($"Damage Modifiers - Crits Enabled: {CriticalHitsEnabled.Value}, Base Crit Chance: {BaseCriticalChance.Value}, Crit Multiplier: {CriticalHitModifier.Value}, Weak Modifier: {WeakStatusModifier.Value}, Break Modifier: {BreakStatusModifier.Value}");
        Debug.Log($"Draft Settings - Pack Size: {DraftPackSize.Value}");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // Clients will receive the SyncVar values from the server automatically.
        // Can subscribe to OnChange events if UI needs to react to rule changes dynamically.
        // PlayerDrawAmount.OnChange += OnRuleChanged_PlayerDrawAmount;
        Debug.Log("GameManager started on Client. Game rules will be synced from server.");
    }

    // Example OnChange handler (optional, SyncVars update automatically)
    /*
    private void OnRuleChanged_PlayerDrawAmount(int prev, int next, bool asServer)
    {
        Debug.Log($"Player Draw Amount rule changed from {prev} to {next}. (Client: {!asServer})");
        // Update any UI that displays this rule, if necessary
    }
    */

    // Methods to get game rules can be added for convenience, though direct access to SyncVars is also possible.
    // public int GetPlayerMaxHealth() => PlayerMaxHealth;
    // etc.
} 