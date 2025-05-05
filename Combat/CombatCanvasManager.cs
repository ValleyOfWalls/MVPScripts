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
using System.Collections.Generic; // Added for List
using DG.Tweening; // Added for DOTween
using System.Reflection; // Added for reflection methods

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
    [SerializeField] private Transform playerAreaTransform; // UI area for the CombatPlayer

    [Header("Player Hand UI")]
    [SerializeField] private Transform playerHandArea; // Parent transform for player card UI objects
    // Add reference to Card UI prefab if needed
    // [SerializeField] private GameObject cardUiPrefab; 

    [Header("Opponent Pet Hand UI")]
    [SerializeField] private Transform opponentPetHandArea; // Parent transform for opponent pet hand cards

    [Header("Player Pet UI")]
    [SerializeField] private TextMeshProUGUI playerPetNameText;
    [SerializeField] private TextMeshProUGUI playerPetHealthText;
    [SerializeField] private Transform playerPetAreaTransform; // UI area for the player's CombatPet
    // Add references for pet status icons (defending, buffs, etc.) if needed

    [Header("Opponent Pet UI")]
    [SerializeField] private TextMeshProUGUI opponentPetNameText;
    [SerializeField] private TextMeshProUGUI opponentPetHealthText;
    [SerializeField] private Transform opponentPetAreaTransform; // UI area for the opponent's CombatPet
    // Add references for opponent pet status icons

    [Header("Combat Result UI")]
    [SerializeField] private GameObject resultPanel; // Panel containing result text
    [SerializeField] private TextMeshProUGUI resultText; // Text showing "Victory" or "Defeat"

    [Header("Battle Observation")]
    [SerializeField] private Button nextBattleButton; // Button to observe next battle

    // Added [SerializeField] for Inspector visibility during runtime
    [SerializeField] private NetworkPlayer localNetworkPlayer;
    [SerializeField] private CombatPlayer localCombatPlayer;
    [SerializeField] private CombatPet localPlayerCombatPet;
    [SerializeField] private CombatPet opponentCombatPet;
    
    // Currently observed combat references
    [Header("Currently Observed References")]
    [SerializeField] private NetworkPlayer currentObservedNetworkPlayer;
    [SerializeField] private CombatPlayer currentObservedCombatPlayer;
    [SerializeField] private CombatPet currentObservedPlayerPet;
    [SerializeField] private CombatPet currentObservedOpponentPet;
    [SerializeField] private PlayerHand currentObservedPlayerHand;
    [SerializeField] private PetHand currentObservedPetHand;

    private NetworkManager _networkManager;
    
    // Variables for battle observation
    private List<NetworkPlayer> allPlayers = new List<NetworkPlayer>();
    private int currentObservedPlayerIndex = -1;
    private bool isObservingOwnBattle = true;

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
        
        // Add listener to Next Battle button
        if (nextBattleButton != null)
        {
            nextBattleButton.onClick.AddListener(OnNextBattleButtonPressed);
        }
        
        // Schedule an early positioning of all combat objects with a short delay
        // This helps with objects that might appear before the network initialization is complete
        Invoke(nameof(FindAndPositionAllCombatObjects), 1.0f);
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
         if (nextBattleButton != null)
         {
             nextBattleButton.onClick.RemoveListener(OnNextBattleButtonPressed);
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
       // UnityEngine.Debug.Log("[CombatCanvasManager] Starting FindPlayerRoutine...");
        // Wait until the client is connected and has its first object (usually the player)
        yield return new WaitUntil(() => _networkManager != null && _networkManager.IsClientStarted && _networkManager.ClientManager.Connection.FirstObject != null);
      //  UnityEngine.Debug.Log("[CombatCanvasManager] NetworkManager is ready.");

        NetworkObject localIdentity = _networkManager.ClientManager.Connection.FirstObject;
        if (localIdentity != null)
        {
            localNetworkPlayer = localIdentity.GetComponent<NetworkPlayer>();
            if (localNetworkPlayer != null)
            {
              //  UnityEngine.Debug.Log($"[CombatCanvasManager] Found local NetworkPlayer: {localNetworkPlayer.GetSteamName()} ({localNetworkPlayer.NetworkObject.ObjectId})");
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
        Debug.Log("[CombatCanvasManager] Starting WaitForCombatData...");
        if (localNetworkPlayer == null)
        {
            Debug.LogError("[CombatCanvasManager] Cannot wait for combat data - localNetworkPlayer is null.");
            yield break;
        }

        // --- Wait for Local Combat Player ---
        Debug.Log("[CombatCanvasManager] Waiting for local CombatPlayer...");
        float timeout = Time.time + 10f; // 10 second timeout
        while (localCombatPlayer == null && Time.time < timeout)
        {
            CombatPlayer[] allCombatPlayers = FindObjectsByType<CombatPlayer>(FindObjectsSortMode.None);
            Debug.Log($"[CombatCanvasManager] Found {allCombatPlayers.Length} CombatPlayer objects in scene");
            
            foreach(CombatPlayer cp in allCombatPlayers)
            {
                // Check if the CombatPlayer's NetworkPlayer SyncVar has arrived and matches our local player
                if (cp.NetworkPlayer != null && cp.NetworkPlayer == localNetworkPlayer)
                {
                    localCombatPlayer = cp;
                    Debug.Log($"[CombatCanvasManager] Found local CombatPlayer: {localCombatPlayer.name} ({localCombatPlayer.NetworkObject.ObjectId}) IsMyTurn={localCombatPlayer.IsMyTurn}");
                    break; 
                }
                
                Debug.Log($"[CombatCanvasManager] CombatPlayer check: {cp.name}, NetworkPlayer={cp.NetworkPlayer?.GetSteamName() ?? "null"}, IsOwner={cp.IsOwner}, IsMyTurn={cp.IsMyTurn}");
            }
            if (localCombatPlayer == null) yield return null; // Wait a frame
        }
        if (localCombatPlayer == null) 
        {
            Debug.LogError("[CombatCanvasManager] Timed out waiting for local CombatPlayer.");
            yield break; // Exit if we can't find the combat player
        }

        // --- Wait for Local Combat Pet ---
        Debug.Log("[CombatCanvasManager] Waiting for local CombatPet...");
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
                   //   UnityEngine.Debug.Log($"[CombatCanvasManager] Found local CombatPet: {localPlayerCombatPet.name} ({localPlayerCombatPet.NetworkObject.ObjectId}) under {localNetworkPlayer.playerPet.Value.name}");
                      break;
                 }
            }
            yield return null; // Wait a frame
        }
        if (localPlayerCombatPet == null) UnityEngine.Debug.LogError("[CombatCanvasManager] Timed out waiting for local CombatPet.");

        // --- Wait for Opponent Combat Pet ---
        Debug.Log("[CombatCanvasManager] Waiting for opponent CombatPet...");
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
            Debug.Log("[CombatCanvasManager] All references found. Subscribing and updating UI.");
            // Basic Info Update
            if (playerNameText != null) playerNameText.text = localNetworkPlayer.GetSteamName();
            
            // Now subscribe to everything - This was the critical line!
            SubscribeToCombatChanges(); 
            
            // Force update UI with initial state AFTER subscribing
            UpdateAllUI(); 
            
            // Initialize battle observation
            InitializeBattleObservation();
            
            // Find and properly parent ALL combat objects first
            FindAndPositionAllCombatObjects();
            
            // Immediately position all combatants and cards to fix initial positioning issues
            PositionObservedCombatants();
            ForceCardArrangement();
            
            // Position ALL hands in the scene to fix unobserved cards
            PositionAllHandsInScene();
            
            // Position the player hand area correctly at the start
            PositionPlayerHandOnStartup();
            
            Debug.Log("[CombatCanvasManager] WaitForCombatData completed successfully");
        }
        else
        {
            string missing = "";
            if (localCombatPlayer == null) missing += "localCombatPlayer ";
            if (localPlayerCombatPet == null) missing += "localPlayerCombatPet ";
            if (opponentCombatPet == null) missing += "opponentCombatPet ";
            
            Debug.LogError($"[CombatCanvasManager] Failed to find all necessary combat references. UI will not be fully initialized. Missing: {missing}");
        }
    }
    
    private void PositionPlayerHandOnStartup()
    {
        // Wait for a moment to ensure all network objects are fully initialized
        StartCoroutine(DelayedHandPositioning());
    }
    
    private IEnumerator DelayedHandPositioning()
    {
        // Wait a short time for all network objects to be properly set up
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log("[CombatCanvasManager] Performing initial hand positioning");
        
        // Ensure the player hand is properly positioned
        if (localNetworkPlayer != null && localNetworkPlayer.PlayerHand != null && playerHandArea != null)
        {
            currentObservedPlayerHand = localNetworkPlayer.PlayerHand;
            
            // Position the hand in the correct UI area
            Transform handTransform = currentObservedPlayerHand.transform;
            handTransform.SetParent(playerHandArea, false);
            handTransform.localPosition = Vector3.zero;
            handTransform.localScale = Vector3.one;
            handTransform.gameObject.SetActive(true);
            
            // Use our direct ManualCardArrangement method
            StartCoroutine(ManualCardArrangement(handTransform));
        }
        
        // Position the opponent pet hand as well
        if (localNetworkPlayer != null && localNetworkPlayer.OpponentNetworkPlayer != null && 
            localNetworkPlayer.OpponentNetworkPlayer.PetHand != null && opponentPetHandArea != null)
        {
            currentObservedPetHand = localNetworkPlayer.OpponentNetworkPlayer.PetHand;
            
            // Position the opponent pet hand in the correct UI area
            Transform petHandTransform = currentObservedPetHand.transform;
            petHandTransform.SetParent(opponentPetHandArea, false);
            petHandTransform.localPosition = Vector3.zero;
            petHandTransform.localScale = Vector3.one;
            petHandTransform.gameObject.SetActive(true);
            
            // Use our direct ManualCardArrangement method
            StartCoroutine(ManualCardArrangement(petHandTransform));
        }
        
        // After positioning hands, also position the combat player and pets
        PositionCombatantsOnStartup();
    }

    private void PositionCombatantsOnStartup()
    {
        Debug.Log("[CombatCanvasManager] Positioning CombatPlayer and CombatPet objects in UI");
        
        // Position the local combat player in the player area
        if (localNetworkPlayer != null && localNetworkPlayer.CombatPlayer != null && playerAreaTransform != null)
        {
            // Set the combat player as the observed player
            currentObservedCombatPlayer = localNetworkPlayer.CombatPlayer;
            
            // Position the combat player in the correct UI area
            Transform playerTransform = currentObservedCombatPlayer.transform;
            playerTransform.SetParent(playerAreaTransform, false);
            playerTransform.localPosition = Vector3.zero;
            playerTransform.localScale = Vector3.one;
            playerTransform.gameObject.SetActive(true);
            
            Debug.Log($"[CombatCanvasManager] Positioned CombatPlayer {playerTransform.name} in UI area");
        }
        else
        {
            Debug.LogWarning("[CombatCanvasManager] Cannot position CombatPlayer - missing references");
        }
        
        // Position the local player's pet in the player pet area
        if (localNetworkPlayer != null && localNetworkPlayer.CombatPet != null && playerPetAreaTransform != null)
        {
            // Set the player's combat pet as the observed pet
            currentObservedPlayerPet = localNetworkPlayer.CombatPet;
            
            // Position the player's pet in the correct UI area
            Transform petTransform = currentObservedPlayerPet.transform;
            petTransform.SetParent(playerPetAreaTransform, false);
            petTransform.localPosition = Vector3.zero;
            petTransform.localScale = Vector3.one;
            petTransform.gameObject.SetActive(true);
            
            Debug.Log($"[CombatCanvasManager] Positioned player's CombatPet {petTransform.name} in UI area");
        }
        else
        {
            Debug.LogWarning("[CombatCanvasManager] Cannot position player's CombatPet - missing references");
        }
        
        // Position the opponent's pet in the opponent pet area
        if (localNetworkPlayer != null && localNetworkPlayer.OpponentCombatPet != null && opponentPetAreaTransform != null)
        {
            // Set the opponent's combat pet as the observed opponent pet
            currentObservedOpponentPet = localNetworkPlayer.OpponentCombatPet;
            
            // Position the opponent's pet in the correct UI area
            Transform opponentPetTransform = currentObservedOpponentPet.transform;
            opponentPetTransform.SetParent(opponentPetAreaTransform, false);
            opponentPetTransform.localPosition = Vector3.zero;
            opponentPetTransform.localScale = Vector3.one;
            opponentPetTransform.gameObject.SetActive(true);
            
            Debug.Log($"[CombatCanvasManager] Positioned opponent's CombatPet {opponentPetTransform.name} in UI area");
        }
        else
        {
            Debug.LogWarning("[CombatCanvasManager] Cannot position opponent's CombatPet - missing references");
        }
    }

    // Add this method after PositionCombatantsOnStartup
    
    private void PositionAllHandsInScene()
    {
        Debug.Log("[CombatCanvasManager] Positioning ALL hands in scene");
        
        // Find all PlayerHand objects in the scene and position them
        PlayerHand[] allPlayerHands = FindObjectsByType<PlayerHand>(FindObjectsSortMode.None);
        foreach (PlayerHand playerHand in allPlayerHands)
        {
            if (playerHand != null && playerHandArea != null)
            {
                // First, save the current parent and active state to restore later if needed
                Transform originalParent = playerHand.transform.parent;
                bool wasActive = playerHand.gameObject.activeSelf;
                
                // Temporarily activate and parent to the hand area for positioning
                playerHand.gameObject.SetActive(true);
                playerHand.transform.SetParent(playerHandArea, false);
                playerHand.transform.localPosition = Vector3.zero;
                playerHand.transform.localScale = Vector3.one;
                
                // Make sure all child cards are active for arrangement
                foreach (Transform child in playerHand.transform)
                {
                    if (!child.gameObject.activeSelf)
                    {
                        child.gameObject.SetActive(true);
                    }
                }
                
                // Apply card arrangement - use synchronous method for non-observed hands
                if (playerHand == currentObservedPlayerHand) 
                {
                    StartCoroutine(ManualCardArrangement(playerHand.transform));
                }
                else 
                {
                    ArrangeCardsImmediately(playerHand.transform);
                }
                
                // Restore original parent and state if this is not the observed hand
                if (playerHand != currentObservedPlayerHand)
                {
                    playerHand.transform.SetParent(originalParent, false);
                    playerHand.gameObject.SetActive(wasActive);
                }
            }
        }
        
        // Find all PetHand objects in the scene and position them
        PetHand[] allPetHands = FindObjectsByType<PetHand>(FindObjectsSortMode.None);
        foreach (PetHand petHand in allPetHands)
        {
            if (petHand != null && opponentPetHandArea != null)
            {
                // First, save the current parent and active state to restore later if needed
                Transform originalParent = petHand.transform.parent;
                bool wasActive = petHand.gameObject.activeSelf;
                
                // Temporarily activate and parent to the hand area for positioning
                petHand.gameObject.SetActive(true);
                petHand.transform.SetParent(opponentPetHandArea, false);
                petHand.transform.localPosition = Vector3.zero;
                petHand.transform.localScale = Vector3.one;
                
                // Make sure all child cards are active for arrangement
                foreach (Transform child in petHand.transform)
                {
                    if (!child.gameObject.activeSelf)
                    {
                        child.gameObject.SetActive(true);
                    }
                }
                
                // Apply card arrangement - use synchronous method for non-observed hands
                if (petHand == currentObservedPetHand) 
                {
                    StartCoroutine(ManualCardArrangement(petHand.transform));
                }
                else 
                {
                    ArrangeCardsImmediately(petHand.transform);
                }
                
                // Restore original parent and state if this is not the observed hand
                if (petHand != currentObservedPetHand)
                {
                    petHand.transform.SetParent(originalParent, false);
                    petHand.gameObject.SetActive(wasActive);
                }
            }
        }
        
        // Finally, ensure the current observed hands are active and positioned correctly
        if (currentObservedPlayerHand != null)
        {
            currentObservedPlayerHand.transform.SetParent(playerHandArea, false);
            currentObservedPlayerHand.transform.localPosition = Vector3.zero;
            currentObservedPlayerHand.transform.localScale = Vector3.one;
            currentObservedPlayerHand.gameObject.SetActive(true);
        }
        
        if (currentObservedPetHand != null)
        {
            currentObservedPetHand.transform.SetParent(opponentPetHandArea, false);
            currentObservedPetHand.transform.localPosition = Vector3.zero;
            currentObservedPetHand.transform.localScale = Vector3.one;
            currentObservedPetHand.gameObject.SetActive(true);
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
       // UnityEngine.Debug.Log("[CombatCanvasManager] References cleared and unsubscribed.");
    }

    // Combined subscription
    void SubscribeToCombatChanges()
    {
        UnsubscribeFromCombatChanges(); // Ensure no duplicates
        
        Debug.Log($"[CombatCanvasManager] SubscribeToCombatChanges called. localCombatPlayer: {(localCombatPlayer != null ? localCombatPlayer.name : "null")}, " +
                  $"currentObservedCombatPlayer: {(currentObservedCombatPlayer != null ? currentObservedCombatPlayer.name : "null")}");

        if (localCombatPlayer != null)
        {
            // IMPORTANT: Check if IsMyTurn is already set before subscribing
            Debug.Log($"[CombatCanvasManager] Before subscribing - localCombatPlayer.IsMyTurn = {localCombatPlayer.IsMyTurn}");
            
            localCombatPlayer.SyncEnergy.OnChange += OnEnergyChanged;
            localCombatPlayer.SyncIsMyTurn.OnChange += OnTurnChanged;
            Debug.Log($"[CombatCanvasManager] ✓ Subscribed to localCombatPlayer changes: {localCombatPlayer.name}. Current IsMyTurn={localCombatPlayer.IsMyTurn}");
            
            // Manually trigger the turn update once during initial subscription if it's already the player's turn
            if (localCombatPlayer.IsMyTurn)
            {
                Debug.Log("[CombatCanvasManager] Player turn is already active - manually calling OnTurnChanged");
                OnTurnChanged(false, true, false); // prev=false, next=true, asServer=false
            }
        }
        else
        {
            Debug.LogError("[CombatCanvasManager] Failed to subscribe - localCombatPlayer is null!");
        }
        
        if (localPlayerCombatPet != null)
        {
            localPlayerCombatPet.SyncHealth.OnChange += OnPlayerPetHealthChanged;
            Debug.Log($"[CombatCanvasManager] ✓ Subscribed to localPlayerCombatPet changes: {localPlayerCombatPet.name}");
        }
        
        if (opponentCombatPet != null)
        {
            opponentCombatPet.SyncHealth.OnChange += OnOpponentPetHealthChanged;
            Debug.Log($"[CombatCanvasManager] ✓ Subscribed to opponentCombatPet changes: {opponentCombatPet.name}");
        }
    }
    
    // Remove split subscriptions
    // void SubscribeToLocalCombatChanges() { ... }
    // void SubscribeToOpponentCombatChanges() { ... }

    // Unified unsubscribe logic - simplified
    void UnsubscribeFromCombatChanges()
    {
        Debug.Log($"[CombatCanvasManager] UnsubscribeFromCombatChanges called. localCombatPlayer: {(localCombatPlayer != null ? localCombatPlayer.name : "null")}");
        
        // Unsubscribe from local references instead of observed references
        if (localCombatPlayer != null)
        {
            localCombatPlayer.SyncEnergy.OnChange -= OnEnergyChanged;
            localCombatPlayer.SyncIsMyTurn.OnChange -= OnTurnChanged;
            Debug.Log($"[CombatCanvasManager] ✓ Unsubscribed from localCombatPlayer: {localCombatPlayer.name}");
        }
        
        if (localPlayerCombatPet != null)
        {
            localPlayerCombatPet.SyncHealth.OnChange -= OnPlayerPetHealthChanged;
            Debug.Log($"[CombatCanvasManager] ✓ Unsubscribed from localPlayerCombatPet: {localPlayerCombatPet.name}");
        }
        
        if (opponentCombatPet != null)
        {
            opponentCombatPet.SyncHealth.OnChange -= OnOpponentPetHealthChanged;
            Debug.Log($"[CombatCanvasManager] ✓ Unsubscribed from opponentCombatPet: {opponentCombatPet.name}");
        }
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
        // Update energy display
        if (localCombatPlayer != null && playerEnergyText != null)
        {
            playerEnergyText.text = $"Energy: {localCombatPlayer.CurrentEnergy}";
        }
        
        // Update turn indicator
        if (turnIndicatorPlayer != null && localCombatPlayer != null)
        {
            turnIndicatorPlayer.SetActive(localCombatPlayer.IsMyTurn);
        }
        
        // Update player pet health
        if (localPlayerCombatPet != null && playerPetHealthText != null)
        {
            playerPetHealthText.text = $"Health: {localPlayerCombatPet.CurrentHealth}";
        }
        
        // Update player pet name
        if (localPlayerCombatPet != null && playerPetNameText != null && localPlayerCombatPet.ReferencePet != null)
        {
            playerPetNameText.text = localPlayerCombatPet.ReferencePet.PetName;
        }
    }
    
    // Update only opponent elements
    void UpdateOpponentUI()
    {
        // Update opponent pet health
        if (opponentCombatPet != null && opponentPetHealthText != null)
        {
            opponentPetHealthText.text = $"Health: {opponentCombatPet.CurrentHealth}";
        }
        
        // Update opponent pet name
        if (opponentCombatPet != null && opponentPetNameText != null && opponentCombatPet.ReferencePet != null)
        {
            opponentPetNameText.text = opponentCombatPet.ReferencePet.PetName;
        }
    }

    private void OnEnergyChanged(int prev, int next, bool asServer)
    {
        // Skip updates on server
        if (asServer) return;
        
        // Update energy text
        if (playerEnergyText != null)
        {
            playerEnergyText.text = $"Energy: {next}";
        }
    }

    private void OnTurnChanged(bool prev, bool next, bool asServer)
    {
        if (asServer) 
        {
            return;
        }
        
        // Update the end turn button visibility and interactability
        if (endTurnButton != null) 
        {
            // Button should only be interactable when:
            // 1. It's the local player's turn
            // 2. We're currently observing our own battle (not someone else's)
            bool shouldBeInteractable = next && isObservingOwnBattle;
            
            // Force enable the button for testing if it's the player's turn
            if (next && isObservingOwnBattle)
            {
                endTurnButton.interactable = true;
            }
            else
            {
                endTurnButton.interactable = shouldBeInteractable;
            }
        
            // When turn starts, animate the button
            if (!prev && next && isObservingOwnBattle)
            {
                endTurnButton.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f);
                
                // Add extra visibility for testing
                if (endTurnButton.GetComponentInChildren<UnityEngine.UI.Text>() != null)
                {
                    endTurnButton.GetComponentInChildren<UnityEngine.UI.Text>().text = "END TURN ✓";
                }
                else if (endTurnButton.GetComponentInChildren<TMPro.TextMeshProUGUI>() != null)
                {
                    endTurnButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "END TURN ✓";
                }
                
                // Force a layout refresh
                if (endTurnButton.transform.parent is RectTransform rectTransform)
                {
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
                }
            }
        }
        else
        {
            Debug.LogError("[CombatCanvasManager] OnTurnChanged - endTurnButton reference is null!");
        }
        
        // Update turn indicator
        if (turnIndicatorPlayer != null) 
        {
            turnIndicatorPlayer.SetActive(next);
        }
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
        if (localCombatPlayer != null && localCombatPlayer.IsOwner)
        {
            if (localCombatPlayer.IsMyTurn)
            {
                localCombatPlayer.CmdEndTurn(); // Call the server command on the CombatPlayer
                
                // Disable button immediately to prevent double clicks
                if (endTurnButton != null)
                {
                    endTurnButton.interactable = false;
                }
            }
            else
            {
                Debug.LogWarning($"[CombatCanvasManager] End Turn button pressed, but it's not the local player's turn. Current turn state: {localCombatPlayer.IsMyTurn}");
            }
        }
        else
        {
            Debug.LogError($"[CombatCanvasManager] End Turn button pressed, but localCombatPlayer is {(localCombatPlayer == null ? "null" : "not owned by this client")}");
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

    // Methods for observing different battles
    
    private void InitializeBattleObservation()
    {
        // Clear existing list
        allPlayers.Clear();
        
        // On clients, NetworkPlayer.Players might not contain all networked players
        // Find all NetworkPlayer objects in the scene instead
        NetworkPlayer[] sceneNetworkPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        foreach (NetworkPlayer player in sceneNetworkPlayers)
        {
            // Only add valid, active players
            if (player != null && player.gameObject.activeInHierarchy)
            {
                allPlayers.Add(player);
                Debug.Log($"[CombatCanvasManager] Found player: {player.GetSteamName()}");
            }
        }
        
        // Set current observed player to local player
        currentObservedNetworkPlayer = localNetworkPlayer;
        currentObservedCombatPlayer = localCombatPlayer;
        currentObservedPlayerPet = localPlayerCombatPet;
        currentObservedOpponentPet = opponentCombatPet;
        
        // Get the player's hand
        currentObservedPlayerHand = localNetworkPlayer.PlayerHand;
        currentObservedPetHand = localNetworkPlayer.OpponentNetworkPlayer?.PetHand;
        
        currentObservedPlayerIndex = allPlayers.IndexOf(localNetworkPlayer);
        isObservingOwnBattle = true;
        
        // Enable next battle button only if there are other players
        if (nextBattleButton != null)
        {
            bool hasMultiplePlayers = allPlayers.Count > 1;
            nextBattleButton.interactable = hasMultiplePlayers;
            Debug.Log($"[CombatCanvasManager] NextBattleButton interactable set to: {hasMultiplePlayers} (Found {allPlayers.Count} players)");
        }
        else
        {
            Debug.LogError("[CombatCanvasManager] NextBattleButton reference is null!");
        }
        
        // Call refresh again after a short delay, in case more network objects are still spawning
        Invoke(nameof(DelayedRefresh), 2.0f);
    }
    
    private void DelayedRefresh()
    {
        RefreshPlayerList();
        EnableBattleSwitch();
    }
    
    // Public method that can be called to force enable the battle switching button
    public void EnableBattleSwitch()
    {
        if (nextBattleButton != null)
        {
            Debug.Log("[CombatCanvasManager] Force enabling NextBattleButton");
            nextBattleButton.interactable = true;
        }
    }
    
    public void OnNextBattleButtonPressed()
    {
        Debug.Log("[CombatCanvasManager] NextBattleButton pressed");
        
        // Refresh the player list in case players have joined or left
        RefreshPlayerList();
        
        // If we don't have at least two players, we can't switch
        if (allPlayers.Count < 2)
        {
            Debug.LogWarning("[CombatCanvasManager] Cannot switch battle - not enough players");
            return;
        }
        
        // Determine the next player index to observe
        int nextPlayerIndex = (currentObservedPlayerIndex + 1) % allPlayers.Count;
        
        // Observe the next player's battle
        NetworkPlayer nextPlayer = allPlayers[nextPlayerIndex];
        if (nextPlayer == localNetworkPlayer)
        {
            // If we've cycled back to our own battle
            ObserveOwnBattle();
        }
        else
        {
            // Observe someone else's battle
            ObservePlayerBattle(nextPlayer);
        }
        
        // Immediately force card arrangement and position combat elements
        PositionObservedCombatants();
        ForceCardArrangement();
        
        // Also find and position ALL combat objects to ensure proper parenting
        FindAndPositionAllCombatObjects();
        
        // Also position all other hands in the scene
        PositionAllHandsInScene();
    }
    
    private void RefreshPlayerList()
    {
        // Save the current observed player reference before refreshing
        NetworkPlayer currentObserved = currentObservedNetworkPlayer;
        
        // Get all NetworkPlayer objects in the scene
        NetworkPlayer[] sceneNetworkPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        
        // Clear and repopulate the list
        allPlayers.Clear();
        foreach (NetworkPlayer player in sceneNetworkPlayers)
        {
            if (player != null && player.gameObject.activeInHierarchy)
            {
                allPlayers.Add(player);
                Debug.Log($"[CombatCanvasManager] Refreshed player list - Found: {player.GetSteamName()}");
            }
        }
        
        // Update the current observed player index
        if (isObservingOwnBattle)
        {
            currentObservedPlayerIndex = allPlayers.IndexOf(localNetworkPlayer);
        }
        else if (currentObserved != null)
        {
            currentObservedPlayerIndex = allPlayers.IndexOf(currentObserved);
            if (currentObservedPlayerIndex < 0)
            {
                // If we can't find the previously observed player, reset to local player
                Debug.LogWarning("[CombatCanvasManager] Previously observed player no longer available, resetting to local player");
                ObserveOwnBattle();
            }
        }
        
        // Update button interactability
        if (nextBattleButton != null)
        {
            nextBattleButton.interactable = (allPlayers.Count > 1);
        }
    }
    
    private void ObservePlayerBattle(NetworkPlayer player)
    {
        Debug.Log($"[CombatCanvasManager] Observing battle of player: {player.GetSteamName()}");
        
        currentObservedNetworkPlayer = player;
        currentObservedCombatPlayer = player.CombatPlayer;
        currentObservedPlayerPet = player.CombatPet;
        currentObservedOpponentPet = player.OpponentCombatPet;
        
        // Get the player's hand
        currentObservedPlayerHand = player.PlayerHand;
        currentObservedPetHand = player.OpponentNetworkPlayer?.PetHand;
        
        currentObservedPlayerIndex = allPlayers.IndexOf(player);
        isObservingOwnBattle = false;
        
        // Immediately position combatants and arrange cards
        PositionObservedCombatants();
        ToggleHandVisibility();
        ForceCardArrangement();
        
        // Update the UI for the observed player
        UpdateObservedPlayerUI(player);
        SubscribeToObservedPlayerCombatChanges(player);
    }
    
    private void ObserveOwnBattle()
    {
        Debug.Log("[CombatCanvasManager] Observing own battle");
        
        if (localNetworkPlayer == null)
        {
            Debug.LogError("[CombatCanvasManager] Cannot observe own battle - localNetworkPlayer is null");
            return;
        }
        
        currentObservedNetworkPlayer = localNetworkPlayer;
        currentObservedCombatPlayer = localCombatPlayer;
        currentObservedPlayerPet = localPlayerCombatPet;
        currentObservedOpponentPet = opponentCombatPet;
        
        // Get the player's hand
        currentObservedPlayerHand = localNetworkPlayer.PlayerHand;
        currentObservedPetHand = localNetworkPlayer.OpponentNetworkPlayer?.PetHand;
        
        currentObservedPlayerIndex = allPlayers.IndexOf(localNetworkPlayer);
        isObservingOwnBattle = true;
        
        // Immediately position combatants and arrange cards
        PositionObservedCombatants();
        ToggleHandVisibility();
        ForceCardArrangement();
        
        // Enable next battle button only if there are other players
        if (nextBattleButton != null)
        {
            bool hasMultiplePlayers = allPlayers.Count > 1;
            nextBattleButton.interactable = hasMultiplePlayers;
            Debug.Log($"[CombatCanvasManager] NextBattleButton interactable set to: {hasMultiplePlayers} (Found {allPlayers.Count} players)");
        }
        else
        {
            Debug.LogError("[CombatCanvasManager] NextBattleButton reference is null!");
        }
        
        // Update UI for own battle
        UpdateObservedPlayerUI(localNetworkPlayer);
        SubscribeToCombatChanges();
        
        // Call refresh again after a short delay, in case more network objects are still spawning
        Invoke(nameof(DelayedRefresh), 2.0f);
    }
    
    // Helper method to position the currently observed combat objects in the UI
    private void PositionObservedCombatants()
    {
        Debug.Log("[CombatCanvasManager] Positioning observed combat objects");
        
        // Position the observed combat player
        if (currentObservedCombatPlayer != null && playerAreaTransform != null)
        {
            Transform playerTransform = currentObservedCombatPlayer.transform;
            playerTransform.SetParent(playerAreaTransform, false);
            playerTransform.localPosition = Vector3.zero;
            playerTransform.localScale = Vector3.one;
            playerTransform.gameObject.SetActive(true);
            
            // Hide other combat players
            CombatPlayer[] allPlayers = FindObjectsByType<CombatPlayer>(FindObjectsSortMode.None);
            foreach (CombatPlayer otherPlayer in allPlayers)
            {
                if (otherPlayer != currentObservedCombatPlayer)
                {
                    // Only hide if they're a child of the UI (not in world)
                    Transform parent = otherPlayer.transform.parent;
                    while (parent != null)
                    {
                        if (parent == playerAreaTransform)
                        {
                            otherPlayer.gameObject.SetActive(false);
                            break;
                        }
                        parent = parent.parent;
                    }
                }
            }
        }
        
        // Position the observed player's combat pet
        if (currentObservedPlayerPet != null && playerPetAreaTransform != null)
        {
            Transform petTransform = currentObservedPlayerPet.transform;
            petTransform.SetParent(playerPetAreaTransform, false);
            petTransform.localPosition = Vector3.zero;
            petTransform.localScale = Vector3.one;
            petTransform.gameObject.SetActive(true);
        }
        
        // Position the observed opponent's combat pet
        if (currentObservedOpponentPet != null && opponentPetAreaTransform != null)
        {
            Transform opponentPetTransform = currentObservedOpponentPet.transform;
            opponentPetTransform.SetParent(opponentPetAreaTransform, false);
            opponentPetTransform.localPosition = Vector3.zero;
            opponentPetTransform.localScale = Vector3.one;
            opponentPetTransform.gameObject.SetActive(true);
            
            // Hide other opponent pets
            CombatPet[] allPets = FindObjectsByType<CombatPet>(FindObjectsSortMode.None);
            foreach (CombatPet otherPet in allPets)
            {
                if (otherPet != currentObservedPlayerPet && otherPet != currentObservedOpponentPet)
                {
                    // Only hide if they're a child of the UI (not in world)
                    Transform parent = otherPet.transform.parent;
                    while (parent != null)
                    {
                        if (parent == playerPetAreaTransform || parent == opponentPetAreaTransform)
                        {
                            otherPet.gameObject.SetActive(false);
                            break;
                        }
                        parent = parent.parent;
                    }
                }
            }
        }
    }
    
    private void ToggleHandVisibility()
    {
        Debug.Log("[CombatCanvasManager] Toggling hand visibility");
        
        // Update player hand visibility based on what we're observing
        if (currentObservedPlayerHand != null && playerHandArea != null)
        {
            // Position the observed player's hand in the player hand area
            Transform handTransform = currentObservedPlayerHand.transform;
            handTransform.SetParent(playerHandArea, false);
            handTransform.localPosition = Vector3.zero;
            handTransform.localScale = Vector3.one;
            handTransform.gameObject.SetActive(true);
            
            // Hide other player hands that might be visible
            foreach (NetworkPlayer player in allPlayers)
            {
                if (player != currentObservedNetworkPlayer && player.PlayerHand != null)
                {
                    player.PlayerHand.gameObject.SetActive(false);
                }
            }
            
            // Always use ManualCardArrangement after switching hands
            Debug.Log($"[CombatCanvasManager] Hand {handTransform.name} activated, calling ManualCardArrangement.");
            StartCoroutine(ManualCardArrangement(handTransform));
        }
        
        // Update opponent pet hand visibility based on what we're observing
        if (currentObservedPetHand != null && opponentPetHandArea != null)
        {
            // Position the observed opponent's pet hand in the opponent pet hand area
            Transform petHandTransform = currentObservedPetHand.transform;
            petHandTransform.SetParent(opponentPetHandArea, false);
            petHandTransform.localPosition = Vector3.zero;
            petHandTransform.localScale = Vector3.one;
            petHandTransform.gameObject.SetActive(true);
            
            // Hide other pet hands that might be visible
            foreach (NetworkPlayer player in allPlayers)
            {
                if (player != currentObservedNetworkPlayer.OpponentNetworkPlayer && player.PetHand != null)
                {
                    player.PetHand.gameObject.SetActive(false);
                }
            }
            
            // Arrange cards in the pet hand
            Debug.Log($"[CombatCanvasManager] Pet Hand {petHandTransform.name} activated, calling ManualCardArrangement.");
            StartCoroutine(ManualCardArrangement(petHandTransform));
        }
    }
    
    private IEnumerator ManualCardArrangement(Transform handTransform)
    {
        // No delay - arrange cards immediately
        Debug.Log("[CombatCanvasManager] Performing immediate card arrangement");
        
        // Count active card objects
        List<Transform> cardTransforms = new List<Transform>();
        foreach (Transform child in handTransform)
        {
            cardTransforms.Add(child);
            child.gameObject.SetActive(true); // Ensure all cards are active
        }
        
        int cardCount = cardTransforms.Count;
        if (cardCount > 0)
        {
            // Calculate simple horizontal positions
            float cardWidth = 120f; // Estimated width, adjust as needed
            float spacing = 10f;   // Basic spacing between cards
            float totalWidth = (cardCount * cardWidth) + ((cardCount - 1) * spacing);
            float startX = -totalWidth / 2f + cardWidth / 2f; // Start from the left edge
            
            for (int i = 0; i < cardCount; i++)
            {
                Transform card = cardTransforms[i];
                
                // Calculate simple horizontal position
                float xPos = startX + (i * (cardWidth + spacing));
                float yPos = 0; // Keep cards aligned horizontally
                
                // Set local position directly without animation
                card.localPosition = new Vector3(xPos, yPos, 0);
                
                // Reset rotation and scale
                card.localRotation = Quaternion.identity;
                card.localScale = Vector3.one;
            }
        }
        
        yield return null;
    }
    
    private void UpdateObservedPlayerUI(NetworkPlayer player)
    {
        if (player == null) return;
        
        // Get combat references for observed player
        CombatPlayer observedCombatPlayer = player.CombatPlayer;
        CombatPet observedPlayerPet = player.CombatPet;
        CombatPet observedOpponentPet = player.OpponentCombatPet;
        
        // Update player name text with clear indicator
        if (playerNameText != null)
        {
            string playerName = player.GetSteamName();
            if (!isObservingOwnBattle)
            {
                playerNameText.text = $"{playerName}'s Battle [OBSERVING]";
                // Change color to indicate observing mode
                playerNameText.color = Color.cyan;
            }
            else
            {
                playerNameText.text = $"{playerName} [YOU]";
                // Reset to default color
                playerNameText.color = Color.white;
            }
        }
        
        // Update energy UI if available
        if (observedCombatPlayer != null && playerEnergyText != null)
        {
            playerEnergyText.text = $"Energy: {observedCombatPlayer.CurrentEnergy}";
        }
        
        // Update player pet information with owner indication
        if (observedPlayerPet != null)
        {
            if (playerPetHealthText != null)
            {
                playerPetHealthText.text = $"Health: {observedPlayerPet.CurrentHealth}/{observedPlayerPet.MaxHealth}";
            }
            
            if (playerPetNameText != null && observedPlayerPet.ReferencePet != null)
            {
                string petName = observedPlayerPet.ReferencePet.PetName;
                string ownerName = player.GetSteamName();
                playerPetNameText.text = $"{petName} ({ownerName}'s Pet)";
            }
        }
        
        // Update opponent pet information with owner indication
        if (observedOpponentPet != null)
        {
            if (opponentPetHealthText != null)
            {
                opponentPetHealthText.text = $"Health: {observedOpponentPet.CurrentHealth}/{observedOpponentPet.MaxHealth}";
            }
            
            if (opponentPetNameText != null && observedOpponentPet.ReferencePet != null)
            {
                string petName = observedOpponentPet.ReferencePet.PetName;
                NetworkPlayer opponentPlayer = player.OpponentNetworkPlayer;
                string opponentName = opponentPlayer != null ? opponentPlayer.GetSteamName() : "Unknown";
                opponentPetNameText.text = $"{petName} ({opponentName}'s Pet)";
            }
        }
        
        // Update turn indicator if available
        if (turnIndicatorPlayer != null && observedCombatPlayer != null)
        {
            turnIndicatorPlayer.SetActive(observedCombatPlayer.IsMyTurn);
        }
        
        // Disable end turn button when observing other players
        if (endTurnButton != null)
        {
            endTurnButton.interactable = isObservingOwnBattle && observedCombatPlayer != null && observedCombatPlayer.IsMyTurn;
        }
    }
    
    private void SubscribeToObservedPlayerCombatChanges(NetworkPlayer player)
    {
        if (player == null) return;
        
        if (currentObservedCombatPlayer != null)
        {
            currentObservedCombatPlayer.SyncEnergy.OnChange += OnEnergyChanged;
            currentObservedCombatPlayer.SyncIsMyTurn.OnChange += OnTurnChanged;
        }
        
        if (currentObservedPlayerPet != null)
        {
            currentObservedPlayerPet.SyncHealth.OnChange += OnPlayerPetHealthChanged;
        }
        
        if (currentObservedOpponentPet != null)
        {
            currentObservedOpponentPet.SyncHealth.OnChange += OnOpponentPetHealthChanged;
        }
        
        // No need to subscribe to hand changes directly, as cards 
        // will be updated visually through their own network syncing
    }

    // Debug method to log all players and their status
    public void LogAllPlayers()
    {
        Debug.Log("-------- [CombatCanvasManager] Logging All Players --------");
        
        // Log the static list from NetworkPlayer class
        Debug.Log($"NetworkPlayer.Players list contains {NetworkPlayer.Players.Count} players:");
        foreach (NetworkPlayer player in NetworkPlayer.Players)
        {
            Debug.Log($"  Static list: {player.GetSteamName()} (IsOwner: {player.IsOwner}, IsServer: {player.IsServer})");
        }
        
        // Find all NetworkPlayer objects in the scene
        NetworkPlayer[] sceneNetworkPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        Debug.Log($"Scene search found {sceneNetworkPlayers.Length} NetworkPlayer objects:");
        foreach (NetworkPlayer player in sceneNetworkPlayers)
        {
            string status = player.gameObject.activeInHierarchy ? "Active" : "Inactive";
            Debug.Log($"  Scene search: {player.GetSteamName()} ({status}, IsOwner: {player.IsOwner}, IsServer: {player.IsServer})");
            
            // Log combat objects
            if (player.CombatPlayer != null)
            {
                Debug.Log($"    - Has CombatPlayer: {player.CombatPlayer.name}");
            }
            if (player.CombatPet != null)
            {
                Debug.Log($"    - Has CombatPet: {player.CombatPet.name}");
            }
            if (player.PlayerHand != null)
            {
                Debug.Log($"    - Has PlayerHand: {player.PlayerHand.name}");
            }
        }
        
        // Log our tracked list
        Debug.Log($"Our allPlayers list contains {allPlayers.Count} players:");
        foreach (NetworkPlayer player in allPlayers)
        {
            Debug.Log($"  Tracked list: {player.GetSteamName()}");
        }
        
        // Log button state
        if (nextBattleButton != null)
        {
            Debug.Log($"NextBattleButton interactable: {nextBattleButton.interactable}");
        }
        else
        {
            Debug.Log("NextBattleButton reference is null!");
        }
        
        Debug.Log("--------------------------------------------------------");
    }

    // Public method to force card arrangement - can be called by a UI button if needed
    public void ForceCardArrangement()
    {
        Debug.Log("[CombatCanvasManager] Force card arrangement requested");
        
        if (currentObservedPlayerHand != null && currentObservedPlayerHand.gameObject.activeInHierarchy)
        {
            Transform handTransform = currentObservedPlayerHand.transform;
            
            // Force repositioning
            if (playerHandArea != null)
            {
                handTransform.SetParent(playerHandArea, false);
                handTransform.localPosition = Vector3.zero;
                handTransform.localScale = Vector3.one;
            }
            
            // Try to arrange cards
            StartCoroutine(ManualCardArrangement(handTransform));
            
            // If specific cards need adjustment
            foreach (Transform cardTransform in handTransform)
            {
                if (!cardTransform.gameObject.activeInHierarchy)
                {
                    cardTransform.gameObject.SetActive(true);
                }
            }
        }
        else
        {
            Debug.LogWarning("[CombatCanvasManager] Cannot force card arrangement - no active player hand");
        }
        
        // Also arrange opponent pet hand cards
        if (currentObservedPetHand != null && currentObservedPetHand.gameObject.activeInHierarchy)
        {
            Transform petHandTransform = currentObservedPetHand.transform;
            
            // Force repositioning
            if (opponentPetHandArea != null)
            {
                petHandTransform.SetParent(opponentPetHandArea, false);
                petHandTransform.localPosition = Vector3.zero;
                petHandTransform.localScale = Vector3.one;
            }
            
            // Try to arrange cards
            StartCoroutine(ManualCardArrangement(petHandTransform));
            
            // If specific cards need adjustment
            foreach (Transform cardTransform in petHandTransform)
            {
                if (!cardTransform.gameObject.activeInHierarchy)
                {
                    cardTransform.gameObject.SetActive(true);
                }
            }
        }
        
        // Also position the combat players and pets
        PositionObservedCombatants();
    }
    
    // Called when the Next Battle button is pressed again to refresh the view
    public void RefreshCurrentView()
    {
        Debug.Log("[CombatCanvasManager] Refreshing current view");
        
        if (isObservingOwnBattle)
        {
            ObserveOwnBattle();
        }
        else if (currentObservedNetworkPlayer != null)
        {
            ObservePlayerBattle(currentObservedNetworkPlayer);
        }
        
        // Force card arrangement and positioning combat elements
        StartCoroutine(DelayedCardArrangementRefresh());
    }
    
    // Coroutine to arrange cards after a short delay to ensure UI has updated
    private IEnumerator DelayedCardArrangementRefresh()
    {
        // No delay, arrange cards immediately
        yield return null;
        ForceCardArrangement();
    }

    // Add a synchronous version of ManualCardArrangement that doesn't use a coroutine
    private void ArrangeCardsImmediately(Transform handTransform)
    {
        Debug.Log("[CombatCanvasManager] Arranging cards immediately (sync)");
        
        // Count card objects
        List<Transform> cardTransforms = new List<Transform>();
        foreach (Transform child in handTransform)
        {
            cardTransforms.Add(child);
            child.gameObject.SetActive(true); // Ensure all cards are active
        }
        
        int cardCount = cardTransforms.Count;
        if (cardCount > 0)
        {
            // Calculate simple horizontal positions
            float cardWidth = 120f; // Estimated width, adjust as needed
            float spacing = 10f;   // Basic spacing between cards
            float totalWidth = (cardCount * cardWidth) + ((cardCount - 1) * spacing);
            float startX = -totalWidth / 2f + cardWidth / 2f; // Start from the left edge
            
            for (int i = 0; i < cardCount; i++)
            {
                Transform card = cardTransforms[i];
                
                // Calculate simple horizontal position
                float xPos = startX + (i * (cardWidth + spacing));
                float yPos = 0; // Keep cards aligned horizontally
                
                // Set local position directly without animation
                card.localPosition = new Vector3(xPos, yPos, 0);
                
                // Reset rotation and scale
                card.localRotation = Quaternion.identity;
                card.localScale = Vector3.one;
            }
        }
    }

    // Add a new method to find and properly parent ALL combat-related objects
    private void FindAndPositionAllCombatObjects()
    {
        Debug.Log("[CombatCanvasManager] Finding and positioning ALL combat objects, even those not properly parented yet");
        
        // Find ALL CombatPlayer objects in the scene
        CombatPlayer[] allCombatPlayers = FindObjectsByType<CombatPlayer>(FindObjectsSortMode.None);
        foreach(CombatPlayer combatPlayer in allCombatPlayers)
        {
            if (combatPlayer != null)
            {
                bool isCurrentlyObserved = (combatPlayer == currentObservedCombatPlayer);
                
                // Temporarily parent to the player area for positioning
                if (playerAreaTransform != null)
                {
                    // Only change the parent if this is the observed player or not parented yet
                    if (isCurrentlyObserved || combatPlayer.transform.parent == null)
                    {
                        combatPlayer.transform.SetParent(playerAreaTransform, false);
                        combatPlayer.transform.localPosition = Vector3.zero;
                        combatPlayer.transform.localScale = Vector3.one;
                    }
                }
                
                // Only hide non-observed players if they're not actively being displayed somewhere
                if (!isCurrentlyObserved && combatPlayer != currentObservedCombatPlayer)
                {
                    combatPlayer.gameObject.SetActive(false);
                }
            }
        }
        
        // Find ALL CombatPet objects in the scene
        CombatPet[] allCombatPets = FindObjectsByType<CombatPet>(FindObjectsSortMode.None);
        
        // In a two-player scenario, we need to ensure pets are visible when switching views
        foreach(CombatPet combatPet in allCombatPets)
        {
            if (combatPet != null)
            {
                NetworkPlayer petOwner = GetPetOwner(combatPet);
                if (petOwner != null)
                {
                    // When observing a player's battle, their pet should be in the player area
                    // and their opponent's pet in the opponent area
                    if (petOwner == currentObservedNetworkPlayer)
                    {
                        // This is the player pet in the current view
                        if (playerPetAreaTransform != null)
                        {
                            combatPet.transform.SetParent(playerPetAreaTransform, false);
                            combatPet.transform.localPosition = Vector3.zero;
                            combatPet.transform.localScale = Vector3.one;
                            
                            // Ensure the player pet is visible
                            combatPet.gameObject.SetActive(true);
                        }
                    }
                    else if (petOwner == currentObservedNetworkPlayer.OpponentNetworkPlayer)
                    {
                        // This is the opponent pet in the current view
                        if (opponentPetAreaTransform != null)
                        {
                            combatPet.transform.SetParent(opponentPetAreaTransform, false);
                            combatPet.transform.localPosition = Vector3.zero;
                            combatPet.transform.localScale = Vector3.one;
                            
                            // Ensure the opponent pet is visible
                            combatPet.gameObject.SetActive(true);
                        }
                    }
                    else
                    {
                        // This pet isn't part of the currently observed battle, so hide it
                        combatPet.gameObject.SetActive(false);
                    }
                }
            }
        }
        
        // Find ALL PlayerHand objects regardless of parent
        PlayerHand[] allPlayerHands = FindObjectsByType<PlayerHand>(FindObjectsSortMode.None);
        foreach(PlayerHand playerHand in allPlayerHands)
        {
            if (playerHand != null && playerHandArea != null)
            {
                bool isCurrentlyObserved = (playerHand == currentObservedPlayerHand);
                
                // Only position and arrange cards for hands that need it
                if (!isCurrentlyObserved)
                {
                    // Save original state
                    bool wasActive = playerHand.gameObject.activeSelf;
                    
                    // Temporarily activate for positioning
                    playerHand.gameObject.SetActive(true);
                    
                    // Parent to hand area for positioning
                    playerHand.transform.SetParent(playerHandArea, false);
                    playerHand.transform.localPosition = Vector3.zero;
                    playerHand.transform.localScale = Vector3.one;
                    
                    // Activate all cards for positioning
                    foreach(Transform child in playerHand.transform)
                    {
                        child.gameObject.SetActive(true);
                    }
                    
                    // Position cards
                    ArrangeCardsImmediately(playerHand.transform);
                    
                    // Set hand back to inactive if it wasn't originally active
                    playerHand.gameObject.SetActive(wasActive);
                }
            }
        }
        
        // Find ALL PetHand objects regardless of parent
        PetHand[] allPetHands = FindObjectsByType<PetHand>(FindObjectsSortMode.None);
        foreach(PetHand petHand in allPetHands)
        {
            if (petHand != null && opponentPetHandArea != null)
            {
                bool isCurrentlyObserved = (petHand == currentObservedPetHand);
                
                // Only position and arrange cards for hands that need it
                if (!isCurrentlyObserved)
                {
                    // Save original state
                    bool wasActive = petHand.gameObject.activeSelf;
                    
                    // Temporarily activate for positioning
                    petHand.gameObject.SetActive(true);
                    
                    // Parent to hand area for positioning
                    petHand.transform.SetParent(opponentPetHandArea, false);
                    petHand.transform.localPosition = Vector3.zero;
                    petHand.transform.localScale = Vector3.one;
                    
                    // Activate all cards for positioning
                    foreach(Transform child in petHand.transform)
                    {
                        child.gameObject.SetActive(true);
                    }
                    
                    // Position cards
                    ArrangeCardsImmediately(petHand.transform);
                    
                    // Set hand back to inactive if it wasn't originally active
                    petHand.gameObject.SetActive(wasActive);
                }
            }
        }
    }
    
    // Helper method to find the owner of a pet
    private NetworkPlayer GetPetOwner(CombatPet pet)
    {
        // Try all players to find the owner of this pet
        NetworkPlayer[] players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        foreach(NetworkPlayer player in players)
        {
            if (player.CombatPet == pet)
            {
                return player;
            }
        }
        return null;
    }
} 