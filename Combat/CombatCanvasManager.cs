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
            ClearAndUnsubscribe();
        }
    }

    void FindLocalPlayerAndInitializeUI()
    {
        // Wait a frame or two in case the player object isn't ready immediately after connection
        StartCoroutine(FindPlayerRoutine());
    }

    IEnumerator FindPlayerRoutine()
    {
        UnityEngine.Debug.Log("[CombatCanvasManager] Starting FindPlayerRoutine...");
        // Wait until the client is connected and has its first object (usually the player)
        yield return new WaitUntil(() => _networkManager != null && _networkManager.IsClientStarted && _networkManager.ClientManager.Connection.FirstObject != null);
        UnityEngine.Debug.Log("[CombatCanvasManager] NetworkManager is ready.");

        NetworkObject localIdentity = _networkManager.ClientManager.Connection.FirstObject;
        if (localIdentity != null)
        {
            localNetworkPlayer = localIdentity.GetComponent<NetworkPlayer>();
            if (localNetworkPlayer != null)
            {
                UnityEngine.Debug.Log($"[CombatCanvasManager] Found local NetworkPlayer: {localNetworkPlayer.GetSteamName()} ({localNetworkPlayer.NetworkObject.ObjectId})");
                // Start the next phase: waiting for combat data
                StartCoroutine(WaitForCombatData());
            }
            else
            {
                UnityEngine.Debug.LogError("[CombatCanvasManager] Local player NetworkObject does not have a NetworkPlayer component!");
            }
        }
        else
        {
             UnityEngine.Debug.LogError("[CombatCanvasManager] Could not find local player NetworkObject (_networkManager.ClientManager.Connection.FirstObject is null).");
        }
    }

    // Coroutine to wait for necessary combat references to be available
    IEnumerator WaitForCombatData()
    {
        UnityEngine.Debug.Log("[CombatCanvasManager] Starting WaitForCombatData...");
        if (localNetworkPlayer == null)
        {
            UnityEngine.Debug.LogError("[CombatCanvasManager] Cannot wait for combat data - localNetworkPlayer is null.");
            yield break;
        }

        // --- Wait for Local Combat Player ---
        UnityEngine.Debug.Log("[CombatCanvasManager] Waiting for local CombatPlayer...");
        float timeout = Time.time + 10f; // 10 second timeout
        while (localCombatPlayer == null && Time.time < timeout)
        {
            CombatPlayer[] allCombatPlayers = FindObjectsByType<CombatPlayer>(FindObjectsSortMode.None);
            foreach(CombatPlayer cp in allCombatPlayers)
            {
                // Check if the CombatPlayer's NetworkPlayer SyncVar has arrived and matches our local player
                if (cp.NetworkPlayer != null && cp.NetworkPlayer == localNetworkPlayer)
                {
                    localCombatPlayer = cp;
                     UnityEngine.Debug.Log($"[CombatCanvasManager] Found local CombatPlayer: {localCombatPlayer.name} ({localCombatPlayer.NetworkObject.ObjectId})");
                    break; 
                }
            }
            if (localCombatPlayer == null) yield return null; // Wait a frame
        }
         if (localCombatPlayer == null) UnityEngine.Debug.LogError("[CombatCanvasManager] Timed out waiting for local CombatPlayer.");

        // --- Wait for Local Combat Pet ---
        UnityEngine.Debug.Log("[CombatCanvasManager] Waiting for local CombatPet...");
        timeout = Time.time + 10f;
        while (localPlayerCombatPet == null && Time.time < timeout)
        {
            // Wait for the persistent pet reference to sync
            if (localNetworkPlayer.playerPet.Value != null)
            {
                // Search children ONLY after the parent ref is valid
                 localPlayerCombatPet = localNetworkPlayer.playerPet.Value.GetComponentInChildren<CombatPet>();
                 if(localPlayerCombatPet != null) 
                 {
                      UnityEngine.Debug.Log($"[CombatCanvasManager] Found local CombatPet: {localPlayerCombatPet.name} ({localPlayerCombatPet.NetworkObject.ObjectId}) under {localNetworkPlayer.playerPet.Value.name}");
                      break;
                 }
            }
            yield return null; // Wait a frame
        }
        if (localPlayerCombatPet == null) UnityEngine.Debug.LogError("[CombatCanvasManager] Timed out waiting for local CombatPet.");

        // --- Wait for Opponent Combat Pet ---
         UnityEngine.Debug.Log("[CombatCanvasManager] Waiting for opponent CombatPet...");
        timeout = Time.time + 10f;
        NetworkPlayer opponentPlayer = null;
        while (opponentCombatPet == null && Time.time < timeout)
        {
             // Wait for opponent player ref to sync
             if (localNetworkPlayer.SyncedOpponentPlayer.Value != null)
             {
                 opponentPlayer = localNetworkPlayer.SyncedOpponentPlayer.Value;
                 // Wait for opponent's persistent pet ref to sync
                 if (opponentPlayer.playerPet.Value != null)
                 {
                     opponentCombatPet = opponentPlayer.playerPet.Value.GetComponentInChildren<CombatPet>();
                     if (opponentCombatPet != null)
                     {
                          UnityEngine.Debug.Log($"[CombatCanvasManager] Found opponent CombatPet: {opponentCombatPet.name} ({opponentCombatPet.NetworkObject.ObjectId}) under {opponentPlayer.playerPet.Value.name}");
                         break;
                     }
                 }
             }
            yield return null; // Wait a frame
        }
        if (opponentCombatPet == null) UnityEngine.Debug.LogError("[CombatCanvasManager] Timed out waiting for opponent CombatPet.");

        // --- Final Setup ---
        if (localCombatPlayer != null && localPlayerCombatPet != null && opponentCombatPet != null)
        {
            UnityEngine.Debug.Log("[CombatCanvasManager] All references found. Subscribing and updating UI.");
             // Basic Info Update
            if (playerNameText != null) playerNameText.text = localNetworkPlayer.GetSteamName();
            SubscribeToCombatChanges(); // Now subscribe to everything
            UpdateAllUI(); // Update all UI elements with initial state
        }
        else
        {
             UnityEngine.Debug.LogError("[CombatCanvasManager] Failed to find all necessary combat references. UI will not be fully initialized.");
        }
    }
    
    // Modified cleanup - no longer called ClearReferences
    void ClearAndUnsubscribe()
    {
        // Stop the waiting coroutine if it's running
        StopCoroutine(nameof(WaitForCombatData)); // Use nameof to avoid typos

        UnsubscribeFromCombatChanges(); // Unsubscribe from everything
        // localNetworkPlayer = null; // Keep localNetworkPlayer until client disconnects
        localCombatPlayer = null;
        localPlayerCombatPet = null;
        opponentCombatPet = null;
        UnityEngine.Debug.Log("[CombatCanvasManager] References cleared and unsubscribed.");
    }

    // Combined subscription
    void SubscribeToCombatChanges()
    {
        UnsubscribeFromCombatChanges(); // Ensure no duplicates

        if (localCombatPlayer != null)
        {
            localCombatPlayer.SyncEnergy.OnChange += OnEnergyChanged;
            localCombatPlayer.SyncIsMyTurn.OnChange += OnTurnChanged;
             UnityEngine.Debug.Log("[CombatCanvasManager] Subscribed to local CombatPlayer changes.");
        }
        if (localPlayerCombatPet != null)
        {
            localPlayerCombatPet.SyncHealth.OnChange += OnPlayerPetHealthChanged;
             UnityEngine.Debug.Log("[CombatCanvasManager] Subscribed to local CombatPet changes.");
        }
        if (opponentCombatPet != null)
        {
             opponentCombatPet.SyncHealth.OnChange += OnOpponentPetHealthChanged;
              UnityEngine.Debug.Log("[CombatCanvasManager] Subscribed to opponent CombatPet changes.");
        }
        // No longer need to subscribe to SyncedOpponentPlayer here, handled by initial find
    }
    
    // Remove split subscriptions
    // void SubscribeToLocalCombatChanges() { ... }
    // void SubscribeToOpponentCombatChanges() { ... }

    // Unified unsubscribe logic - simplified
    void UnsubscribeFromCombatChanges()
    {
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
        // No longer need to unsubscribe from SyncedOpponentPlayer here
    }

    // --- Update Methods (Called by Callbacks or InitializeUI) ---

    // Combined initial update
    void UpdateAllUI()
    {
       UpdateLocalPlayerUI();
       UpdateOpponentUI();
    }
    
    // Update only local player elements
    void UpdateLocalPlayerUI()
    {
        OnEnergyChanged(0, localCombatPlayer != null ? localCombatPlayer.CurrentEnergy : 0, false);
        OnTurnChanged(false, localCombatPlayer != null ? localCombatPlayer.IsMyTurn : false, false);
        OnPlayerPetHealthChanged(0, localPlayerCombatPet != null ? localPlayerCombatPet.CurrentHealth : 0, false);
        if (localPlayerCombatPet != null && playerPetNameText != null) playerPetNameText.text = localPlayerCombatPet.ReferencePet?.PetName ?? "Player Pet";
         else if (playerPetNameText != null) playerPetNameText.text = "Player Pet: N/A";
    }
    
    // Update only opponent elements
    void UpdateOpponentUI()
    {
        OnOpponentPetHealthChanged(0, opponentCombatPet != null ? opponentCombatPet.CurrentHealth : 0, false);
        if (opponentCombatPet != null && opponentPetNameText != null) opponentPetNameText.text = opponentCombatPet.ReferencePet?.PetName ?? "Opponent Pet";
        else if (opponentPetNameText != null) opponentPetNameText.text = "Opponent Pet: N/A";
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