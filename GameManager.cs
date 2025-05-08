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

    // Game Rules - Synced so clients can see them if needed for UI or predictions (though server is authoritative)
    public readonly SyncVar<int> PlayerDrawAmount = new SyncVar<int>(5);
    public readonly SyncVar<int> PetDrawAmount = new SyncVar<int>(3);
    public readonly SyncVar<int> PlayerMaxEnergy = new SyncVar<int>(3);
    public readonly SyncVar<int> PetMaxEnergy = new SyncVar<int>(2);
    public readonly SyncVar<int> PlayerMaxHealth = new SyncVar<int>(100);
    public readonly SyncVar<int> PetMaxHealth = new SyncVar<int>(50);

    // Add other game-wide settings here, e.g., round limits, special rules flags, etc.

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
        // Values are initialized with defaults. Server can override them here if needed.
        // e.g., PlayerMaxHealth.Value = 120;
        Debug.Log("GameManager started on Server. Initializing game rules.");
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