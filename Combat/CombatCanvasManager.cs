using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet;
using FishNet.Managing; // Required for NetworkManager
using FishNet.Connection; // Required for NetworkConnection
using FishNet.Object; // Required for NetworkObject
using Combat; // Your combat namespace
using System.Collections; 
using FishNet.Transporting; // Required for ClientConnectionStateArgs

/// <summary>
/// Manages the UI elements on the Combat Canvas. 
/// This script runs on the client and updates the UI based on the local player's perspective.
/// It is NOT a NetworkBehaviour.
/// </summary>
public class CombatCanvasManager : MonoBehaviour
{
    [Header("Player UI References")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI playerEnergyText;
    // Add references for player health display if needed (e.g., TextMeshProUGUI or Slider)
    // [SerializeField] private TextMeshProUGUI playerHealthText; 
    [SerializeField] private Button endTurnButton;
    [SerializeField] private GameObject turnIndicatorPlayer; // e.g., a highlight or image

    [Header("Player Hand UI")]
    [SerializeField] private Transform playerHandArea; // Parent transform for player card UI objects
    // Add reference to Card UI prefab if needed
    // [SerializeField] private GameObject cardUiPrefab; 

    [Header("Player Pet UI")]
    [SerializeField] private TextMeshProUGUI playerPetNameText;
    [SerializeField] private TextMeshProUGUI playerPetHealthText;
    // Add references for pet status icons (defending, buffs, etc.) if needed

    [Header("Opponent Pet UI")]
    [SerializeField] private TextMeshProUGUI opponentPetNameText;
    [SerializeField] private TextMeshProUGUI opponentPetHealthText;
    // Add references for opponent pet status icons

    [Header("Combat Result UI")]
    [SerializeField] private GameObject resultPanel; // Panel containing result text
    [SerializeField] private TextMeshProUGUI resultText; // Text showing "Victory" or "Defeat"

    // Added [SerializeField] for Inspector visibility during runtime
    [SerializeField] private NetworkPlayer localNetworkPlayer;
    [SerializeField] private CombatPlayer localCombatPlayer;
    [SerializeField] private CombatPet localPlayerCombatPet;
    [SerializeField] private CombatPet opponentCombatPet;

    private NetworkManager _networkManager;

    void Start()
    {
        if (InstanceFinder.NetworkManager != null)
        {
            _networkManager = InstanceFinder.NetworkManager;
            _networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
            // If already connected when Start runs
            if (_networkManager.IsClientStarted) 
            {
                FindLocalPlayerAndInitializeUI();
            }
        }
        else
        {
            Debug.LogError("[CombatCanvasManager] NetworkManager not found!");
        }

        // Initially hide result panel and disable end turn button
        if (resultPanel != null) resultPanel.SetActive(false);
        if (endTurnButton != null) endTurnButton.interactable = false;
        if (turnIndicatorPlayer != null) turnIndicatorPlayer.SetActive(false);

        // Add listener to End Turn button
        if (endTurnButton != null)
        {
            endTurnButton.onClick.AddListener(OnEndTurnButtonPressed);
        }
    }
    
    private void OnDestroy()
    {
         // Clean up listeners
         if (_networkManager != null)
         {
             _networkManager.ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
         }
         if (endTurnButton != null)
         {
             endTurnButton.onClick.RemoveListener(OnEndTurnButtonPressed);
         }
         
         // Unsubscribe from SyncVar changes if subscribed
         UnsubscribeFromCombatChanges();
    }

    private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
    {
        // Attempt to find local player when client is started/stopped
        if (obj.ConnectionState == LocalConnectionState.Started)
        {
            FindLocalPlayerAndInitializeUI();
        }
        else if (obj.ConnectionState == LocalConnectionState.Stopped)
        {
            ClearReferences();
        }
    }

    void FindLocalPlayerAndInitializeUI()
    {
        // Wait a frame or two in case the player object isn't ready immediately after connection
        StartCoroutine(FindPlayerRoutine());
    }

    IEnumerator FindPlayerRoutine()
    {
        // Wait until the client is connected and has its first object (usually the player)
        yield return new WaitUntil(() => _networkManager.IsClientStarted && _networkManager.ClientManager.Connection.FirstObject != null);

        NetworkObject localIdentity = _networkManager.ClientManager.Connection.FirstObject;
        if (localIdentity != null)
        {
            localNetworkPlayer = localIdentity.GetComponent<NetworkPlayer>();
            if (localNetworkPlayer != null)
            {
                Debug.Log($"[CombatCanvasManager] Found local player: {localNetworkPlayer.GetSteamName()}");
                InitializeUI(); // Initialize based on the found player
            }
            else
            {
                Debug.LogError("[CombatCanvasManager] Local player NetworkObject does not have a NetworkPlayer component!");
            }
        }
        else
        {
             Debug.LogError("[CombatCanvasManager] Could not find local player NetworkObject (_networkManager.ClientManager.Connection.FirstObject is null).");
             // Optionally retry after a delay
             // yield return new WaitForSeconds(1f);
             // StartCoroutine(FindPlayerRoutine());
        }
    }

    void InitializeUI()
    {
        if (localNetworkPlayer == null)
        {
            Debug.LogError("[CombatCanvasManager] Cannot initialize UI - localNetworkPlayer is null.");
            return;
        }

        // Clear previous subscriptions before setting up new ones
        UnsubscribeFromCombatChanges(); 

        // Get references from the local NetworkPlayer
        localCombatPlayer = localNetworkPlayer.CombatPlayer;
        localPlayerCombatPet = localNetworkPlayer.CombatPet;
        opponentCombatPet = localNetworkPlayer.OpponentCombatPet;

        // Basic Info Update
        if (playerNameText != null) playerNameText.text = localNetworkPlayer.GetSteamName();
        
        // Subscribe to changes and update UI initially
        SubscribeToCombatChanges();
        UpdateAllUI(); // Update all UI elements with initial state

        Debug.Log("[CombatCanvasManager] UI Initialized.");
    }
    
    void ClearReferences()
    {
        UnsubscribeFromCombatChanges();
        localNetworkPlayer = null;
        localCombatPlayer = null;
        localPlayerCombatPet = null;
        opponentCombatPet = null;
        Debug.Log("[CombatCanvasManager] References cleared due to client stop.");
    }

    void SubscribeToCombatChanges()
    {
        // Subscribe to SyncVar changes on the relevant components
        if (localCombatPlayer != null)
        {
            localCombatPlayer.SyncEnergy.OnChange += OnEnergyChanged;
            localCombatPlayer.SyncIsMyTurn.OnChange += OnTurnChanged;
        }
        if (localPlayerCombatPet != null)
        {
            localPlayerCombatPet.SyncHealth.OnChange += OnPlayerPetHealthChanged;
            // Subscribe to other relevant Pet SyncVars (e.g., IsDefending, buffs)
        }
        if (opponentCombatPet != null)
        {
             opponentCombatPet.SyncHealth.OnChange += OnOpponentPetHealthChanged;
             // Subscribe to opponent pet SyncVars
        }
    }

    void UnsubscribeFromCombatChanges()
    {
        // Make sure to unsubscribe to prevent errors when objects are destroyed
        if (localCombatPlayer != null)
        {
             localCombatPlayer.SyncEnergy.OnChange -= OnEnergyChanged;
             localCombatPlayer.SyncIsMyTurn.OnChange -= OnTurnChanged;
        }
         if (localPlayerCombatPet != null)
        {
             localPlayerCombatPet.SyncHealth.OnChange -= OnPlayerPetHealthChanged;
        }
         if (opponentCombatPet != null)
        {
             opponentCombatPet.SyncHealth.OnChange -= OnOpponentPetHealthChanged;
        }
    }

    // --- Update Methods (Called by Callbacks or InitializeUI) ---

    void UpdateAllUI()
    {
        // Call individual update methods based on current references
        OnEnergyChanged(0, localCombatPlayer != null ? localCombatPlayer.CurrentEnergy : 0, false);
        OnTurnChanged(false, localCombatPlayer != null ? localCombatPlayer.IsMyTurn : false, false);
        OnPlayerPetHealthChanged(0, localPlayerCombatPet != null ? localPlayerCombatPet.CurrentHealth : 0, false);
        OnOpponentPetHealthChanged(0, opponentCombatPet != null ? opponentCombatPet.CurrentHealth : 0, false);
        
        // Update names (less likely to change, but good practice)
        if (localPlayerCombatPet != null && playerPetNameText != null) playerPetNameText.text = localPlayerCombatPet.ReferencePet?.PetName ?? "Player Pet";
        if (opponentCombatPet != null && opponentPetNameText != null) opponentPetNameText.text = opponentCombatPet.ReferencePet?.PetName ?? "Opponent Pet";
    }

    private void OnEnergyChanged(int prev, int next, bool asServer)
    {
        if (asServer) return; // Only update UI on clients
        if (playerEnergyText != null && localCombatPlayer != null)
        {
            playerEnergyText.text = $"Energy: {next}/{localCombatPlayer.MaxEnergy}";
        }
    }

    private void OnTurnChanged(bool prev, bool next, bool asServer)
    {
         if (asServer) return;
         if (endTurnButton != null) endTurnButton.interactable = next; // Enable button only on player's turn
         if (turnIndicatorPlayer != null) turnIndicatorPlayer.SetActive(next);
         // Potentially add visual indicator for opponent's turn
    }

    private void OnPlayerPetHealthChanged(int prev, int next, bool asServer)
    {
         if (asServer) return;
         if (playerPetHealthText != null && localPlayerCombatPet != null)
         {
             playerPetHealthText.text = $"HP: {next}/{localPlayerCombatPet.MaxHealth}";
             // Add health bar update logic here if using one
         }
    }
    
    private void OnOpponentPetHealthChanged(int prev, int next, bool asServer)
    {
         if (asServer) return;
         if (opponentPetHealthText != null && opponentCombatPet != null)
         {
            opponentPetHealthText.text = $"HP: {next}/{opponentCombatPet.MaxHealth}";
             // Add health bar update logic here if using one
         }
    }

    // --- UI Event Handlers ---

    public void OnEndTurnButtonPressed()
    {
        if (localCombatPlayer != null && localCombatPlayer.IsOwner && localCombatPlayer.IsMyTurn)
        {
            Debug.Log("[CombatCanvasManager] End Turn button pressed.");
            localCombatPlayer.CmdEndTurn(); // Call the server command on the CombatPlayer
            // Optionally disable button immediately to prevent double clicks
            if (endTurnButton != null) endTurnButton.interactable = false; 
        }
        else
        {
             Debug.LogWarning("[CombatCanvasManager] End Turn button pressed, but it's not the local player's turn or player not found.");
        }
    }

    // --- Public Methods (Called by CombatManager RPCs) ---

    /// <summary>
    /// Shows the combat result panel. Called via RPC from CombatManager.
    /// </summary>
    /// <param name="victory">True if the local player won, false otherwise.</param>
    public void ShowCombatResult(bool victory)
    {
        if (resultPanel != null && resultText != null)
        {
            resultText.text = victory ? "Victory!" : "Defeat";
            resultPanel.SetActive(true);
            // Optionally add animations, sounds, etc.
        }
        else
        {
            Debug.LogError("[CombatCanvasManager] Cannot show combat result - Result Panel or Result Text is not assigned.");
        }
    }
} 