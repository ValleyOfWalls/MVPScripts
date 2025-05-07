using UnityEngine;
// using UnityEngine.UI; // Slider is no longer used
using TMPro; // For TextMeshProUGUI
using FishNet.Object; // For NetworkBehaviour
// using FishNet.Object.Synchronizing; // Not directly needed for SyncVar access here, NetworkPlayer has them

[RequireComponent(typeof(NetworkPlayer))]
public class PlayerStatDisplay : MonoBehaviour
{
    [Header("UI References (Assign in Prefab)")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI playerHealthText;
    // [SerializeField] private Slider playerHealthSlider; // Removed
    [SerializeField] private TextMeshProUGUI playerEnergyText;
    // [SerializeField] private Slider playerEnergySlider; // Removed

    private NetworkPlayer networkPlayer;

    private void Awake()
    {
        networkPlayer = GetComponent<NetworkPlayer>();
        if (networkPlayer == null)
        {
            Debug.LogError("PlayerStatDisplay: NetworkPlayer component not found on this GameObject.", this);
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        // Subscribe to SyncVar changes only if this client has authority or is observing.
        // This check might be redundant if OnChange events are filtered by FishNet internally for non-observers,
        // but explicit check is safer depending on FishNet version nuances.
        // However, UI should update for everyone observing this player.
        if (networkPlayer.IsClientInitialized) // Ensures SyncVars are ready to be subscribed to
        {
            SubscribeToChanges();
            InitialUISetup();
        }
        else
        {
            // If not yet initialized, OnStartClient of NetworkPlayer might be a good place to trigger this, 
            // or use a small delay/coroutine. For now, assume direct Start() is okay or NetworkPlayer calls a method here.
            // A simpler approach: NetworkPlayer itself could have an event like `OnClientDetailsReady` that this script subscribes to.
            // For now, we'll rely on Start() and ensure NetworkPlayer's SyncVars are subscribable.
            // Let's try calling subscribe directly, OnChange should handle if values are not ready yet or fire when they are.
            SubscribeToChanges();
            InitialUISetup(); // This might show default values initially if called before first sync
        }
    }

    private void SubscribeToChanges()
    {
        networkPlayer.PlayerName.OnChange += OnPlayerNameChanged;
        networkPlayer.CurrentHealth.OnChange += OnCurrentHealthChanged;
        networkPlayer.MaxHealth.OnChange += OnMaxHealthChanged;
        networkPlayer.CurrentEnergy.OnChange += OnCurrentEnergyChanged;
        networkPlayer.MaxEnergy.OnChange += OnMaxEnergyChanged;
    }

    private void OnDestroy()
    {
        if (networkPlayer == null) return;

        networkPlayer.PlayerName.OnChange -= OnPlayerNameChanged;
        networkPlayer.CurrentHealth.OnChange -= OnCurrentHealthChanged;
        networkPlayer.MaxHealth.OnChange -= OnMaxHealthChanged;
        networkPlayer.CurrentEnergy.OnChange -= OnCurrentEnergyChanged;
        networkPlayer.MaxEnergy.OnChange -= OnMaxEnergyChanged;
    }
    
    private void InitialUISetup()
    {
        // Called after subscribing, so if OnChange fires immediately, these might be redundant
        // but good for setting initial state based on potentially already synced values.
        OnPlayerNameChanged(networkPlayer.PlayerName.Value, networkPlayer.PlayerName.Value, false);
        UpdateHealthUI(); // Will use current and max health from NetworkPlayer
        UpdateEnergyUI(); // Will use current and max energy from NetworkPlayer
    }

    private void OnPlayerNameChanged(string prev, string next, bool asServer)
    {
        if (playerNameText != null)
        {
            playerNameText.text = next;
        }
    }

    private void OnCurrentHealthChanged(int prev, int next, bool asServer)
    {
        UpdateHealthUI();
    }

    private void OnMaxHealthChanged(int prev, int next, bool asServer)
    {
        UpdateHealthUI();
    }

    private void UpdateHealthUI()
    {
        if (networkPlayer == null) return;

        int current = networkPlayer.CurrentHealth.Value;
        int max = networkPlayer.MaxHealth.Value;

        if (playerHealthText != null)
        {
            playerHealthText.text = $"HP: {current} / {max}";
        }
        // Slider logic removed
    }

    private void OnCurrentEnergyChanged(int prev, int next, bool asServer)
    {
        UpdateEnergyUI();
    }

    private void OnMaxEnergyChanged(int prev, int next, bool asServer)
    {
        UpdateEnergyUI();
    }

    private void UpdateEnergyUI()
    {
        if (networkPlayer == null) return;

        int current = networkPlayer.CurrentEnergy.Value;
        int max = networkPlayer.MaxEnergy.Value;

        if (playerEnergyText != null)
        {
            playerEnergyText.text = $"EN: {current} / {max}";
        }
        // Slider logic removed
    }
} 