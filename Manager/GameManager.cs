using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// Centralized manager for global game rules and settings.
/// 
/// Health & Energy: GameManager provides default/fallback values for health and energy.
/// Character-specific values from CharacterData.BaseHealth/BaseEnergy take precedence
/// when entities are spawned through the character selection system.
/// 
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

    [Header("Pet Card Settings")]
    [SerializeField, Tooltip("Number of cards a pet draws on the first round")]
    private int petInitialDraw = 3;
    [SerializeField, Tooltip("Target number of cards in pet's hand after drawing each round")]
    private int petHandTarget = 3;



    [Header("Game Rules")]
    [SerializeField, Tooltip("If true, players will automatically be set to ready when they join the lobby.")]
    public bool AutoReadyPlayersOnJoin = false;
    
    [SerializeField, Tooltip("If true, enables VSync to synchronize frame rate with monitor refresh rate. Note: VSync is automatically disabled when using frame rate limiting (maxFrameRate > 0).")]
    public bool enableVSync = false;

    [SerializeField, Tooltip("Maximum frame rate limit. Set to -1 for unlimited, 0 to use platform default, or any positive value.")]
    public int maxFrameRate = -1;

    [Header("Draft Settings")]
    [SerializeField, Tooltip("Number of cards in each draft pack")]
    private int draftPackSize = 4;

    [Header("Shop Settings")]
    [SerializeField, Tooltip("Number of cards available in the shop")]
    private int shopSize = 6;
    [SerializeField, Tooltip("Minimum cost for cards in the shop")]
    private int shopMinCardCost = 1;
    [SerializeField, Tooltip("Maximum cost for cards in the shop")]
    private int shopMaxCardCost = 5;

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

    // Shop settings SyncVars
    public readonly SyncVar<int> ShopSize = new SyncVar<int>();
    public readonly SyncVar<int> ShopMinCardCost = new SyncVar<int>();
    public readonly SyncVar<int> ShopMaxCardCost = new SyncVar<int>();

    private void Awake()
    {
        // Apply frame rate and VSync settings
        ApplyDisplaySettings();
        
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

    private void Start()
    {
        // Reapply display settings in Start to ensure they take effect
        // Sometimes Awake is too early and settings get overridden
        Debug.Log("GameManager: Start() - Reapplying display settings to ensure they take effect");
        ApplyDisplaySettings();
    }

    private void ApplyDisplaySettings()
    {
        Debug.Log($"GameManager: ApplyDisplaySettings called - maxFrameRate: {maxFrameRate}, enableVSync: {enableVSync}");
        
        // When using frame rate limiting, VSync must be disabled
        if (maxFrameRate > 0)
        {
            // Disable VSync to allow frame rate limiting
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = maxFrameRate;
            Debug.Log($"GameManager: Frame rate limited to {maxFrameRate} FPS (VSync disabled for frame rate limiting)");
            
            if (enableVSync)
            {
                Debug.LogWarning("GameManager: VSync was enabled but has been disabled to allow frame rate limiting. To use VSync, set maxFrameRate to -1 or 0.");
            }
        }
        else if (maxFrameRate == -1)
        {
            // Unlimited frame rate
            if (enableVSync)
            {
                // Use VSync for unlimited but synchronized frame rate
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = -1;
                Debug.Log("GameManager: VSync enabled with unlimited frame rate");
            }
            else
            {
                // True unlimited frame rate
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                Debug.Log("GameManager: Frame rate set to unlimited (VSync disabled)");
            }
        }
        else // maxFrameRate == 0 (platform default)
        {
            if (enableVSync)
            {
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = 0;
                Debug.Log("GameManager: VSync enabled with platform default frame rate");
            }
            else
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 0;
                Debug.Log("GameManager: Frame rate set to platform default (VSync disabled)");
            }
        }
        
        // Log final settings for debugging
        Debug.Log($"GameManager: Final settings - VSync: {QualitySettings.vSyncCount}, TargetFrameRate: {Application.targetFrameRate}");
        
        // Force a frame to ensure settings take effect
        Canvas.ForceUpdateCanvases();
    }

    // Public method to reapply display settings at runtime (for debugging)
    [ContextMenu("Reapply Display Settings")]
    public void ReapplyDisplaySettings()
    {
        Debug.Log("GameManager: Manually reapplying display settings...");
        ApplyDisplaySettings();
    }

    // Public method to check current display settings (for debugging)
    [ContextMenu("Log Current Display Settings")]
    public void LogCurrentDisplaySettings()
    {
        Debug.Log($"GameManager: Current Runtime Settings:");
        Debug.Log($"  - QualitySettings.vSyncCount: {QualitySettings.vSyncCount}");
        Debug.Log($"  - Application.targetFrameRate: {Application.targetFrameRate}");
        Debug.Log($"  - Time.deltaTime: {Time.deltaTime}");
        Debug.Log($"  - Calculated FPS: {1.0f / Time.deltaTime:F1}");
        Debug.Log($"GameManager: Inspector Settings:");
        Debug.Log($"  - maxFrameRate: {maxFrameRate}");
        Debug.Log($"  - enableVSync: {enableVSync}");
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
        // Note: PlayerMaxEnergy, PetMaxEnergy, PlayerMaxHealth, PetMaxHealth are not set here
        // These values should come from CharacterData and PetData during character selection
        
        // Initialize damage modifier SyncVars
        CriticalHitsEnabled.Value = criticalHitsEnabled;
        BaseCriticalChance.Value = baseCriticalChance;
        CriticalHitModifier.Value = criticalHitModifier;
        WeakStatusModifier.Value = weakStatusModifier;
        BreakStatusModifier.Value = breakStatusModifier;
        
        // Initialize draft settings SyncVars
        DraftPackSize.Value = draftPackSize;
        
        // Initialize shop settings SyncVars
        ShopSize.Value = shopSize;
        ShopMinCardCost.Value = shopMinCardCost;
        ShopMaxCardCost.Value = shopMaxCardCost;
        
        Debug.Log("GameManager started on Server. Initializing game rules.");
        Debug.Log($"Player settings - Initial Draw: {PlayerDrawAmount.Value}, Target Hand: {PlayerTargetHandSize.Value}, Max Energy: {PlayerMaxEnergy.Value}, Max Health: {PlayerMaxHealth.Value}");
        Debug.Log($"Pet settings - Initial Draw: {PetDrawAmount.Value}, Target Hand: {PetTargetHandSize.Value}, Max Energy: {PetMaxEnergy.Value}, Max Health: {PetMaxHealth.Value}");
        Debug.Log($"Damage Modifiers - Crits Enabled: {CriticalHitsEnabled.Value}, Base Crit Chance: {BaseCriticalChance.Value}, Crit Multiplier: {CriticalHitModifier.Value}, Weak Modifier: {WeakStatusModifier.Value}, Break Modifier: {BreakStatusModifier.Value}");
        Debug.Log($"Draft Settings - Pack Size: {DraftPackSize.Value}");
        Debug.Log($"Shop Settings - Size: {ShopSize.Value}, Cost Range: {ShopMinCardCost.Value}-{ShopMaxCardCost.Value}");
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