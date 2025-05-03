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

    [Header("Opponent Pet Hand UI")]
    [SerializeField] private Transform opponentPetHandArea; // Parent transform for opponent pet hand cards

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
      //  UnityEngine.Debug.Log("[CombatCanvasManager] Starting WaitForCombatData...");
        if (localNetworkPlayer == null)
        {
            UnityEngine.Debug.LogError("[CombatCanvasManager] Cannot wait for combat data - localNetworkPlayer is null.");
            yield break;
        }

        // --- Wait for Local Combat Player ---
      //  UnityEngine.Debug.Log("[CombatCanvasManager] Waiting for local CombatPlayer...");
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
              //       UnityEngine.Debug.Log($"[CombatCanvasManager] Found local CombatPlayer: {localCombatPlayer.name} ({localCombatPlayer.NetworkObject.ObjectId})");
                    break; 
                }
            }
            if (localCombatPlayer == null) yield return null; // Wait a frame
        }
         if (localCombatPlayer == null) UnityEngine.Debug.LogError("[CombatCanvasManager] Timed out waiting for local CombatPlayer.");

        // --- Wait for Local Combat Pet ---
       // UnityEngine.Debug.Log("[CombatCanvasManager] Waiting for local CombatPet...");
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
       //  UnityEngine.Debug.Log("[CombatCanvasManager] Waiting for opponent CombatPet...");
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
                         // UnityEngine.Debug.Log($"[CombatCanvasManager] Found opponent CombatPet: {opponentCombatPet.name} ({opponentCombatPet.NetworkObject.ObjectId}) under {opponentPlayer.playerPet.Value.name}");
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
         //   UnityEngine.Debug.Log("[CombatCanvasManager] All references found. Subscribing and updating UI.");
             // Basic Info Update
            if (playerNameText != null) playerNameText.text = localNetworkPlayer.GetSteamName();
            SubscribeToCombatChanges(); // Now subscribe to everything
            UpdateAllUI(); // Update all UI elements with initial state
            
            // Initialize battle observation system
            InitializeBattleObservation();
            
            // Position the player hand area correctly at the start
            PositionPlayerHandOnStartup();
        }
        else
        {
             UnityEngine.Debug.LogError("[CombatCanvasManager] Failed to find all necessary combat references. UI will not be fully initialized.");
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
        yield return new WaitForSeconds(1.5f);
        
       // Debug.Log("[CombatCanvasManager] Performing initial hand positioning");
        
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
            
            // If the hand has a method to arrange cards, try to call it
            PlayerHand playerHand = handTransform.GetComponent<PlayerHand>();
            if (playerHand != null)
            {
                // Use reflection to call the ArrangeCardsInHand method even if it's private
                System.Reflection.MethodInfo arrangeMethod = 
                    typeof(PlayerHand).GetMethod("ArrangeCardsInHand", 
                    System.Reflection.BindingFlags.Instance | 
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.NonPublic);
                
                if (arrangeMethod != null)
                {
                   // Debug.Log("[CombatCanvasManager] Found and calling ArrangeCardsInHand method");
                    arrangeMethod.Invoke(playerHand, null);
                }
                else
                {
                    // Try to call a public RPC method that might trigger card arrangement
                  //  Debug.Log("[CombatCanvasManager] ArrangeCardsInHand method not found, trying to trigger a refresh");
                    // This is a fallback in case there's no direct method access
                    playerHand.gameObject.SetActive(false);
                    playerHand.gameObject.SetActive(true);
                }
            }
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
            
            // If the pet hand has a method to arrange cards, try to call it
            PetHand petHand = petHandTransform.GetComponent<PetHand>();
            if (petHand != null)
            {
                // Try to access ArrangeCardsInHand via reflection (similar to player hand)
                System.Reflection.MethodInfo arrangeMethod = 
                    typeof(PetHand).GetMethod("ArrangeCardsInHand", 
                    System.Reflection.BindingFlags.Instance | 
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.NonPublic);
                
                if (arrangeMethod != null)
                {
                  //  Debug.Log("[CombatCanvasManager] Found and calling ArrangeCardsInHand method for pet hand");
                    arrangeMethod.Invoke(petHand, null);
                }
                else
                {
                    // Try to call a public RPC method that might trigger card arrangement
                   // Debug.Log("[CombatCanvasManager] ArrangeCardsInHand method not found for pet hand, trying to trigger a refresh");
                    petHand.gameObject.SetActive(false);
                    petHand.gameObject.SetActive(true);
                }
            }
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

        if (localCombatPlayer != null)
        {
            localCombatPlayer.SyncEnergy.OnChange += OnEnergyChanged;
            localCombatPlayer.SyncIsMyTurn.OnChange += OnTurnChanged;
          //   UnityEngine.Debug.Log("[CombatCanvasManager] Subscribed to local CombatPlayer changes.");
        }
        if (localPlayerCombatPet != null)
        {
            localPlayerCombatPet.SyncHealth.OnChange += OnPlayerPetHealthChanged;
           //  UnityEngine.Debug.Log("[CombatCanvasManager] Subscribed to local CombatPet changes.");
        }
        if (opponentCombatPet != null)
        {
             opponentCombatPet.SyncHealth.OnChange += OnOpponentPetHealthChanged;
            //  UnityEngine.Debug.Log("[CombatCanvasManager] Subscribed to opponent CombatPet changes.");
        }
        // No longer need to subscribe to SyncedOpponentPlayer here, handled by initial find
    }
    
    // Remove split subscriptions
    // void SubscribeToLocalCombatChanges() { ... }
    // void SubscribeToOpponentCombatChanges() { ... }

    // Unified unsubscribe logic - simplified
    void UnsubscribeFromCombatChanges()
    {
        if (currentObservedCombatPlayer != null)
        {
             currentObservedCombatPlayer.SyncEnergy.OnChange -= OnEnergyChanged;
             currentObservedCombatPlayer.SyncIsMyTurn.OnChange -= OnTurnChanged;
        }
        
        if (currentObservedPlayerPet != null)
        {
             currentObservedPlayerPet.SyncHealth.OnChange -= OnPlayerPetHealthChanged;
        }
        
        if (currentObservedOpponentPet != null)
        {
             currentObservedOpponentPet.SyncHealth.OnChange -= OnOpponentPetHealthChanged;
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
        //    Debug.Log("[CombatCanvasManager] End Turn button pressed.");
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
               // Debug.Log($"[CombatCanvasManager] Found player: {player.GetSteamName()}");
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
           // Debug.Log($"[CombatCanvasManager] NextBattleButton interactable set to: {hasMultiplePlayers} (Found {allPlayers.Count} players)");
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
          //  Debug.Log("[CombatCanvasManager] Force enabling NextBattleButton");
            nextBattleButton.interactable = true;
        }
    }
    
    public void OnNextBattleButtonPressed()
    {
        //Debug.Log("[CombatCanvasManager] NextBattleButton pressed");
        
        // Refresh the player list in case players have joined or left
        RefreshPlayerList();
        
        // Check if we have enough players to switch
        if (allPlayers.Count <= 1)
        {
            Debug.LogWarning("[CombatCanvasManager] Cannot switch battles - only one player found");
            return;
        }
        
        // If this is the first time pressing the button, just refresh the current view
        // This helps with initial positioning issues
        if (isObservingOwnBattle && playerHandArea != null && localNetworkPlayer.PlayerHand != null)
        {
            Transform handTransform = localNetworkPlayer.PlayerHand.transform;
            if (handTransform.parent != playerHandArea)
            {
               // Debug.Log("[CombatCanvasManager] First button press - positioning hand correctly");
                handTransform.SetParent(playerHandArea, false);
                handTransform.localPosition = Vector3.zero;
                handTransform.localScale = Vector3.one;
                StartCoroutine(DelayedCardArrangementRefresh());
                return;
            }
        }
        
        // If we're currently observing our own battle, switch to another player's battle
        if (isObservingOwnBattle || currentObservedPlayerIndex < 0)
        {
            isObservingOwnBattle = false;
            
            // Find the first player that isn't the local player
            for (int i = 0; i < allPlayers.Count; i++)
            {
                if (allPlayers[i] != localNetworkPlayer)
                {
                    currentObservedPlayerIndex = i;
                    ObservePlayerBattle(allPlayers[i]);
                    StartCoroutine(DelayedCardArrangementRefresh());
                    return;
                }
            }
        }
        else
        {
            // Find the next player in the list
            int nextIndex = (currentObservedPlayerIndex + 1) % allPlayers.Count;
            
            // If we've cycled back to the local player, observe our own battle
            if (allPlayers[nextIndex] == localNetworkPlayer)
            {
                ObserveOwnBattle();
            }
            else
            {
                currentObservedPlayerIndex = nextIndex;
                ObservePlayerBattle(allPlayers[nextIndex]);
            }
            
            // Ensure cards are arranged properly after switching
            StartCoroutine(DelayedCardArrangementRefresh());
        }
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
               // Debug.Log($"[CombatCanvasManager] Refreshed player list - Found: {player.GetSteamName()}");
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
        if (player == null) return;
        
        // Unsubscribe from current combat changes
        UnsubscribeFromCombatChanges();
        
        // Update references to observed player's objects
        currentObservedNetworkPlayer = player;
        currentObservedCombatPlayer = player.CombatPlayer;
        currentObservedPlayerPet = player.CombatPet;
        currentObservedOpponentPet = player.OpponentCombatPet;
        currentObservedPlayerHand = player.PlayerHand;
        currentObservedPetHand = player.OpponentNetworkPlayer?.PetHand;
        isObservingOwnBattle = (player == localNetworkPlayer);
        
        // Toggle visibility of player hand UI
        ToggleHandVisibility();
        
        // Update UI for observed player's battle
        UpdateObservedPlayerUI(player);
        
        // Set SyncVar change subscriptions to the new player's combat objects
        SubscribeToObservedPlayerCombatChanges(player);
    }
    
    private void ObserveOwnBattle()
    {
        // Unsubscribe from current observed player's combat changes
        UnsubscribeFromCombatChanges();
        
        // Reset to observing own battle
        currentObservedNetworkPlayer = localNetworkPlayer;
        currentObservedCombatPlayer = localCombatPlayer;
        currentObservedPlayerPet = localPlayerCombatPet;
        currentObservedOpponentPet = opponentCombatPet;
        currentObservedPlayerHand = localNetworkPlayer.PlayerHand;
        currentObservedPetHand = localNetworkPlayer.OpponentNetworkPlayer?.PetHand;
        currentObservedPlayerIndex = allPlayers.IndexOf(localNetworkPlayer);
        isObservingOwnBattle = true;
        
        // Toggle visibility of player hand UI
        ToggleHandVisibility();
        
        // Re-subscribe to local player's combat changes
        SubscribeToCombatChanges();
        
        // Update UI with local player's combat data
        UpdateAllUI();
        
        // Update player name
        if (playerNameText != null)
        {
            playerNameText.text = localNetworkPlayer.GetSteamName();
        }
    }
    
    private void ToggleHandVisibility()
    {
        //Debug.Log("[CombatCanvasManager] Toggling hand visibility");
        
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
            //Debug.Log($"[CombatCanvasManager] Hand {handTransform.name} activated, calling ManualCardArrangement.");
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
            //Debug.Log($"[CombatCanvasManager] Pet Hand {petHandTransform.name} activated, calling ManualCardArrangement.");
            StartCoroutine(ManualCardArrangement(petHandTransform));
        }
    }
    
    private IEnumerator ManualCardArrangement(Transform handTransform)
    {
        // Give the system a moment to process other changes
        yield return new WaitForSeconds(0.1f);
        
       // Debug.Log("[CombatCanvasManager] Performing simplified card arrangement (no animation)");
        
        // Count active card objects
        List<Transform> cardTransforms = new List<Transform>();
        foreach (Transform child in handTransform)
        {
            if (child.gameObject.activeSelf)
            {
                cardTransforms.Add(child);
            }
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
    }
    
    // Called when the Next Battle button is pressed again to refresh the view
    public void RefreshCurrentView()
    {
       // Debug.Log("[CombatCanvasManager] Refreshing current view");
        
        if (isObservingOwnBattle)
        {
            ObserveOwnBattle();
        }
        else if (currentObservedNetworkPlayer != null)
        {
            ObservePlayerBattle(currentObservedNetworkPlayer);
        }
        
        // Force card arrangement
        StartCoroutine(DelayedCardArrangementRefresh());
    }
    
    // Coroutine to arrange cards after a short delay to ensure UI has updated
    private IEnumerator DelayedCardArrangementRefresh()
    {
        yield return new WaitForSeconds(0.5f);
        ForceCardArrangement();
    }
} 