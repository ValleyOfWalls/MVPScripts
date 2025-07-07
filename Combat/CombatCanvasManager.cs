using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using TMPro;
using System.Linq;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>//
/// Manages the UI elements for the combat phase, including turn indicators, and notifications.
/// Attach to: The CombatCanvas GameObject that contains all combat UI elements.
/// </summary>
public class CombatCanvasManager : NetworkBehaviour
{
    [Header("Controls")]
    [SerializeField] private Button endTurnButton;
    [SerializeField] private Button spectateButton;
    [SerializeField] private Button returnToOwnFightButton;
    
    [Header("Testing Controls")]
    [SerializeField] private Button testPlayerPerspectiveButton;
    [SerializeField] private Button testOpponentPerspectiveButton;
    [SerializeField] private Button stopTestsButton;
    [SerializeField] private Button generateTestCardsButton;
    [SerializeField] private Button generateTestDecksButton;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI turnIndicatorText;
    [SerializeField] private GameObject fightEndedPanel;
    [SerializeField] private TextMeshProUGUI fightEndedText;
    [SerializeField] private GameObject combatCanvas;

    [Header("Own Pet View")]
    [SerializeField] private Transform ownPetViewContainer;
    [SerializeField] private GameObject ownPetViewPrefab;
    [SerializeField] private OwnPetViewController ownPetViewController;

    [Header("Deck Viewer")]
    [SerializeField] private DeckViewerManager deckViewerManager;

    [Header("Combat Entity Positioning")]
    [SerializeField] private Transform playerPositionTransform;
    [SerializeField] private Transform opponentPetPositionTransform;
    [SerializeField] private Transform playerHandPositionTransform;
    [SerializeField] private Transform opponentPetHandPositionTransform;
    
    [Header("Stats UI Positioning")]
    [SerializeField] private Transform playerStatsUIPositionTransform;
    [SerializeField] private Transform opponentPetStatsUIPositionTransform;

    private NetworkEntity localPlayer;
    private NetworkEntity opponentPetForLocalPlayer;

    private FightManager fightManager;
    private CombatManager combatManager;
    private CombatTestManager testManager;

    // Public properties for accessing UI elements
    public Button SpectateButton => spectateButton;
    public Button ReturnToOwnFightButton => returnToOwnFightButton;

    public void Initialize(CombatManager manager, NetworkEntity player)
    {
        combatManager = manager;
        localPlayer = player;
    }

    public void UpdateTurnUI(CombatTurn turn)
    {
        if (turnIndicatorText != null)
        {
            string turnText = turn switch
            {
                CombatTurn.PlayerTurn => "Player's Turn",
                CombatTurn.PetTurn => "Pet's Turn",
                CombatTurn.SharedTurn => "Your Turn",
                _ => "Waiting..."
            };
            turnIndicatorText.text = turnText;
        }

        // Enable/disable end turn button based on whose turn it is
        if (endTurnButton != null)
        {
            bool isLocalPlayerTurn = (turn == CombatTurn.PlayerTurn || turn == CombatTurn.SharedTurn) && localPlayer != null && localPlayer.IsOwner;
            endTurnButton.interactable = isLocalPlayerTurn;
        }
    }

    /// <summary>
    /// Updates the UI to show waiting for other players state
    /// </summary>
    public void UpdateWaitingForPlayersUI(bool isWaiting)
    {
        if (turnIndicatorText != null)
        {
            if (isWaiting)
            {
                turnIndicatorText.text = "Waiting for other players...";
            }
            else
            {
                // Reset to normal turn text - this will be updated by UpdateTurnUI
                turnIndicatorText.text = "Your Turn";
            }
        }

        // Disable end turn button while waiting
        if (endTurnButton != null)
        {
            endTurnButton.interactable = !isWaiting;
        }
    }

    public void ShowCardPlayedEffect(int cardId, NetworkBehaviour caster, NetworkBehaviour target)
    {
        // Simplified version that just logs to console
        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null) return;

        string casterName = caster.GetComponent<NetworkEntity>()?.EntityName.Value ?? "Unknown";
        string targetName = target.GetComponent<NetworkEntity>()?.EntityName.Value ?? "Unknown";
        
        Debug.Log($"Card played: {casterName} played {cardData.CardName} on {targetName}");
    }

    public void ShowFightEndedPanel(NetworkEntity player, NetworkEntity pet, bool petWon)
    {
        if (fightEndedPanel == null || fightEndedText == null) return;

        string winnerName = petWon ? pet.EntityName.Value : player.EntityName.Value;
        fightEndedText.text = $"{winnerName} has won the fight!";
        fightEndedPanel.SetActive(true);
    }

    public void ShowNotificationMessage(string message)
    {
        // Simplified version that just logs to console
        Debug.Log($"Combat notification: {message}");
    }

    public void SetupCombatUI()
    {
        // Find managers if not already assigned
        if (fightManager == null) fightManager = FindFirstObjectByType<FightManager>();
        if (combatManager == null) combatManager = FindFirstObjectByType<CombatManager>();
        if (testManager == null) testManager = FindFirstObjectByType<CombatTestManager>();

        if (fightManager == null) Debug.LogError("FightManager not found by CombatCanvasManager.");
        if (combatManager == null) Debug.LogError("CombatManager not found by CombatCanvasManager.");

        // Try multiple approaches to find the local player
        FindLocalPlayer();

        if (localPlayer == null)
        {
            LogLocalPlayerError();
            return;
        }

        // Find opponent
        opponentPetForLocalPlayer = fightManager.GetOpponentForPlayer(localPlayer);

        if (opponentPetForLocalPlayer == null)
        {
            // Try to wait and retry if opponent isn't available yet
            StartCoroutine(RetryFindOpponent());
        }
        else
        {
            // Complete UI setup since we found opponent
            CompleteUISetup();
        }
    }
    
    private void FindLocalPlayer()
    {
        // Try to find local player through various means
        localPlayer = FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None)
            .FirstOrDefault(p => p.EntityType == EntityType.Player && p.IsOwner);

        if (localPlayer == null)
        {
            Debug.LogWarning("Could not find local player through direct search.");
        }
    }

    private void LogLocalPlayerError()
    {
        Debug.LogError("CombatCanvasManager: Could not find local player. UI setup failed.");
    }

    private IEnumerator RetryFindOpponent()
    {
        int retryCount = 0;
        const int maxRetries = 5;
        const float retryDelay = 0.5f;

        while (opponentPetForLocalPlayer == null && retryCount < maxRetries)
        {
            yield return new WaitForSeconds(retryDelay);
            opponentPetForLocalPlayer = fightManager.GetOpponentForPlayer(localPlayer);
            retryCount++;
        }

        if (opponentPetForLocalPlayer != null)
        {
            CompleteUISetup();
        }
        else
        {
            Debug.LogError("Failed to find opponent pet after retries.");
        }
    }

    private void CompleteUISetup()
    {
        // Set up UI elements based on the local player and their opponent
        if (localPlayer != null && opponentPetForLocalPlayer != null)
        {
            // Initialize button listeners
            InitializeButtonListeners();
            
            // Setup own pet view
            SetupOwnPetView();
            
            // Setup deck viewer
            SetupDeckViewer();
            
            // Position combat entities for all fights
            PositionCombatEntitiesForAllFights();
            
            // Additional UI setup code here
        }
        else
        {
            Debug.LogError($"Cannot complete UI setup - localPlayer: {(localPlayer != null ? "found" : "null")}, opponentPet: {(opponentPetForLocalPlayer != null ? "found" : "null")}");
        }
    }

    private void InitializeButtonListeners()
    {
        SetupEndTurnButton();
        SetupTestButtons();
    }

    /// <summary>
    /// Sets up the end turn button to correctly end the local player's turn.
    /// </summary>
    private void SetupEndTurnButton()
    {
        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveAllListeners();
            endTurnButton.onClick.AddListener(() => {
                if (combatManager != null && localPlayer != null)
                {
                    // Call the server method to end the turn
                    combatManager.OnEndTurnButtonPressed(localPlayer.Owner);
                }
                else
                {
                    Debug.LogError($"Cannot end turn: Missing {(combatManager == null ? "CombatManager" : "local player")} reference");
                }
            });
        }
        else
        {
            Debug.LogError("End Turn Button not assigned in CombatCanvasManager");
        }
    }
    
    /// <summary>
    /// Sets up the test buttons for combat testing
    /// </summary>
    private void SetupTestButtons()
    {
        // Test Player Perspective Button
        if (testPlayerPerspectiveButton != null)
        {
            testPlayerPerspectiveButton.onClick.RemoveAllListeners();
            testPlayerPerspectiveButton.onClick.AddListener(() => {
                if (testManager != null)
                {
                    testManager.StartPlayerPerspectiveTests();
                    Debug.Log("Starting player perspective tests...");
                }
                else
                {
                    Debug.LogWarning("CombatTestManager not found. Cannot start tests.");
                }
            });
        }
        
        // Test Opponent Perspective Button
        if (testOpponentPerspectiveButton != null)
        {
            testOpponentPerspectiveButton.onClick.RemoveAllListeners();
            testOpponentPerspectiveButton.onClick.AddListener(() => {
                if (testManager != null)
                {
                    testManager.StartOpponentPetPerspectiveTests();
                    Debug.Log("Starting opponent pet perspective tests...");
                }
                else
                {
                    Debug.LogWarning("CombatTestManager not found. Cannot start tests.");
                }
            });
        }
        
        // Stop Tests Button
        if (stopTestsButton != null)
        {
            stopTestsButton.onClick.RemoveAllListeners();
            stopTestsButton.onClick.AddListener(() => {
                if (testManager != null)
                {
                    testManager.StopTests();
                    Debug.Log("Stopping tests...");
                }
                else
                {
                    Debug.LogWarning("CombatTestManager not found. Cannot stop tests.");
                }
            });
        }
        
        // Generate Test Cards Button (Editor only)
        if (generateTestCardsButton != null)
        {
            generateTestCardsButton.onClick.RemoveAllListeners();
            #if UNITY_EDITOR
            generateTestCardsButton.onClick.AddListener(() => {
                // Use reflection to get the TestCardGenerator type since it's in Editor assembly
                var testCardGeneratorType = System.Type.GetType("TestCardGenerator");
                if (testCardGeneratorType != null)
                {
                    var window = UnityEditor.EditorWindow.GetWindow(testCardGeneratorType, false, "Test Card Generator");
                    window.Show();
                    Debug.Log("Opening Test Card Generator window...");
                }
                else
                {
                    Debug.LogError("TestCardGenerator class not found. Make sure it's in the Editor folder.");
                }
            });
            #else
            generateTestCardsButton.onClick.AddListener(() => {
                Debug.LogWarning("Test card generation is only available in the editor.");
            });
            #endif
            
            // Hide in builds since it's editor-only
            #if !UNITY_EDITOR
            generateTestCardsButton.gameObject.SetActive(false);
            #endif
        }
        
        // Generate Test Decks Button (Editor only)
        if (generateTestDecksButton != null)
        {
            generateTestDecksButton.onClick.RemoveAllListeners();
            #if UNITY_EDITOR
            generateTestDecksButton.onClick.AddListener(() => {
                // Use reflection to get the TestDeckGenerator type since it's in Editor assembly
                var testDeckGeneratorType = System.Type.GetType("TestDeckGenerator");
                if (testDeckGeneratorType != null)
                {
                    var window = UnityEditor.EditorWindow.GetWindow(testDeckGeneratorType, false, "Test Deck Generator");
                    window.Show();
                    Debug.Log("Opening Test Deck Generator window...");
                }
                else
                {
                    Debug.LogError("TestDeckGenerator class not found. Make sure it's in the Editor folder.");
                }
            });
            #else
            generateTestDecksButton.onClick.AddListener(() => {
                Debug.LogWarning("Test deck generation is only available in the editor.");
            });
            #endif
            
            // Hide in builds since it's editor-only
            #if !UNITY_EDITOR
            generateTestDecksButton.gameObject.SetActive(false);
            #endif
        }
    }

    /// <summary>
    /// Sets up the own pet view functionality
    /// </summary>
    private void SetupOwnPetView()
    {
        // Validate container
        if (ownPetViewContainer == null)
        {
            Debug.LogWarning("CombatCanvasManager: OwnPetViewContainer not assigned. Own pet view will not be available.");
            return;
        }
        
        // Find existing OwnPetViewController if not assigned
        if (ownPetViewController == null)
        {
            ownPetViewController = ownPetViewContainer.GetComponentInChildren<OwnPetViewController>();
        }
        
        // If still not found, try to spawn the prefab (server only)
        if (ownPetViewController == null)
        {
            if (ownPetViewPrefab != null)
            {
                // Check if the prefab has a NetworkObject component
                NetworkObject prefabNetworkObject = ownPetViewPrefab.GetComponent<NetworkObject>();
                if (prefabNetworkObject != null)
                {
                    // Only spawn on server, clients will receive it automatically
                    var networkManager = FishNet.InstanceFinder.NetworkManager;
                    if (networkManager != null && networkManager.IsServerStarted)
                    {
                        // Spawn at root first to avoid NetworkObject parenting issues
                        GameObject spawnedObject = Instantiate(ownPetViewPrefab);
                        
                        NetworkObject spawnedNetworkObject = spawnedObject.GetComponent<NetworkObject>();
                        
                        if (spawnedNetworkObject != null)
                        {
                                                    // Spawn the NetworkObject first
                        networkManager.ServerManager.Spawn(spawnedNetworkObject);
                        
                        // Then move to correct parent after spawning
                        spawnedObject.transform.SetParent(ownPetViewContainer, false);
                        
                        // Ensure it's active
                        spawnedObject.SetActive(true);
                        
                        ownPetViewController = spawnedObject.GetComponent<OwnPetViewController>();
                        
                        // Notify clients to move the spawned object to correct parent
                        RpcSetOwnPetViewParent(spawnedNetworkObject.ObjectId);
                        }
                    }
                }
            }
        }
        
        if (ownPetViewController != null)
        {
            // Refresh the displayed pet to show the currently viewed player's pet
            ownPetViewController.RefreshDisplayedPet();
        }
    }

    /// <summary>
    /// Called when the viewed combat changes (e.g., when spectating)
    /// Updates the own pet view to show the new viewed player's pet
    /// </summary>
    public void OnViewedCombatChanged()
    {
        if (ownPetViewController != null)
        {
            ownPetViewController.RefreshDisplayedPet();
        }
        
        // Update deck viewer button states for the new viewed combat
        if (deckViewerManager != null)
        {
            deckViewerManager.UpdateButtonStates();
        }
        
        // Update entity positioning and facing for the new viewed combat
        if (fightManager != null)
        {
            var viewedPlayer = fightManager.ViewedCombatPlayer;
            var viewedOpponentPet = fightManager.ViewedCombatOpponentPet;
            
            if (viewedPlayer != null && viewedOpponentPet != null)
            {
                /* Debug.Log($"CombatCanvasManager: Updating entity positioning and facing for viewed combat - Player: {viewedPlayer.EntityName.Value}, Opponent Pet: {viewedOpponentPet.EntityName.Value}"); */
                PositionCombatEntitiesForSpecificFight(viewedPlayer, viewedOpponentPet);
            }
            else
            {
                Debug.LogWarning("CombatCanvasManager: Cannot update entity positioning - viewed combat entities are null");
            }
        }
    }

    private void OnDestroy()
    {
        if (endTurnButton != null) endTurnButton.onClick.RemoveAllListeners();
    }

    public void SetEndTurnButtonInteractable(bool interactable)
    {
        if (endTurnButton != null)
        {
            endTurnButton.interactable = interactable;
        }
    }
    
    /// <summary>
    /// Disables the end turn button when the local player's fight is over
    /// </summary>
    public void DisableEndTurnButton()
    {
        if (endTurnButton != null)
        {
            endTurnButton.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// Enables the end turn button (used when starting a new fight)
    /// </summary>
    public void EnableEndTurnButton()
    {
        if (endTurnButton != null)
        {
            endTurnButton.gameObject.SetActive(true);
        }
    }
    
    /// <summary>
    /// Disables the entire combat canvas (used during draft transition)
    /// </summary>
    public void DisableCombatCanvas()
    {
        if (combatCanvas != null)
        {
            combatCanvas.SetActive(false);
        }
    }
    
    /// <summary>
    /// Enables the entire combat canvas (used when returning to combat)
    /// </summary>
    public void EnableCombatCanvas()
    {
        if (combatCanvas != null)
        {
            combatCanvas.SetActive(true);
            
            // Refresh own pet view when canvas is re-enabled
            if (ownPetViewController != null)
            {
                ownPetViewController.RefreshDisplayedPet();
            }
            
            // Update deck viewer button states when canvas is re-enabled
            if (deckViewerManager != null)
            {
                deckViewerManager.UpdateButtonStates();
            }
        }
    }
    
    /// <summary>
    /// Called when the local player's fight ends to update UI accordingly
    /// </summary>
    public void OnLocalFightEnded()
    {
        DisableEndTurnButton();
    }

    /// <summary>
    /// Gets the OwnPetViewController for external access
    /// </summary>
    public OwnPetViewController GetOwnPetViewController()
    {
        return ownPetViewController;
    }

    /// <summary>
    /// Sets up the deck viewer functionality
    /// </summary>
    private void SetupDeckViewer()
    {
        if (deckViewerManager == null)
        {
            deckViewerManager = FindFirstObjectByType<DeckViewerManager>();
        }
        
        if (deckViewerManager != null)
        {
            // Update button states for the initial combat setup
            deckViewerManager.UpdateButtonStates();
        }
    }

    /// <summary>
    /// Gets the DeckViewerManager for external access
    /// </summary>
    public DeckViewerManager GetDeckViewerManager()
    {
        return deckViewerManager;
    }
    
    /// <summary>
    /// RPC to set the correct parent for the OwnPetView prefab on clients
    /// </summary>
    [ObserversRpc]
    private void RpcSetOwnPetViewParent(int networkObjectId)
    {
        StartCoroutine(SetOwnPetViewParentWithRetry(networkObjectId));
    }
    
    /// <summary>
    /// Coroutine to handle client-side parenting with retry logic
    /// </summary>
    private System.Collections.IEnumerator SetOwnPetViewParentWithRetry(int networkObjectId)
    {
        NetworkObject spawnedNetworkObject = null;
        int retryCount = 0;
        int maxRetries = 10;
        
        // Wait for the NetworkObject to be spawned on the client
        while (retryCount < maxRetries && spawnedNetworkObject == null)
        {
            yield return new WaitForSeconds(0.1f);
            
            var networkManager = FishNet.InstanceFinder.NetworkManager;
            if (networkManager != null && networkManager.IsClientStarted)
            {
                networkManager.ClientManager.Objects.Spawned.TryGetValue(networkObjectId, out spawnedNetworkObject);
            }
            
            retryCount++;
        }
        
        if (spawnedNetworkObject != null && ownPetViewContainer != null)
        {
            /* Debug.Log($"CombatCanvasManager: Moving OwnPetView from root to container on client"); */
            spawnedNetworkObject.transform.SetParent(ownPetViewContainer, false);
            spawnedNetworkObject.gameObject.SetActive(true);
            
            // Update the reference
            if (ownPetViewController == null)
            {
                ownPetViewController = spawnedNetworkObject.GetComponent<OwnPetViewController>();
            }
        }
        else
        {
            Debug.LogWarning($"CombatCanvasManager: Failed to find spawned OwnPetView NetworkObject with ID {networkObjectId} after {maxRetries} retries");
        }
    }

    /// <summary>
    /// Positions combat entities for all ongoing fights
    /// Only positions entities owned by the local client to respect NetworkTransform ownership
    /// </summary>
    public void PositionCombatEntitiesForAllFights()
    {
        if (fightManager == null)
        {
            Debug.LogError("FightManager is null, cannot position entities for all fights");
            return;
        }
        
        // Get all fight assignments from the FightManager
        var allFights = fightManager.GetAllFightAssignments();
        
        foreach (var fightAssignment in allFights)
        {
            // Get the actual NetworkEntity objects from the IDs
            NetworkEntity player = GetNetworkEntityByObjectId(fightAssignment.PlayerObjectId);
            NetworkEntity opponentPet = GetNetworkEntityByObjectId(fightAssignment.PetObjectId);
            
            if (player == null || opponentPet == null)
            {
                Debug.LogWarning($"Cannot find entities for fight: Player ID {fightAssignment.PlayerObjectId}, Pet ID {fightAssignment.PetObjectId}");
                continue;
            }
            
            // Position entities for this specific fight (includes facing setup)
            PositionCombatEntitiesForSpecificFight(player, opponentPet);
        }
    }

    /// <summary>
    /// Helper method to get a NetworkEntity component from an ObjectId
    /// </summary>
    private NetworkEntity GetNetworkEntityByObjectId(uint objectId)
    {
        NetworkObject nob = null;
        if (base.IsServerStarted)
        {
            FishNet.InstanceFinder.NetworkManager.ServerManager.Objects.Spawned.TryGetValue((int)objectId, out nob);
        }
        else if (base.IsClientStarted)
        {
            FishNet.InstanceFinder.NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)objectId, out nob);
        }
        
        return nob != null ? nob.GetComponent<NetworkEntity>() : null;
    }

    /// <summary>
    /// Positions combat entities for a specific fight
    /// Only positions entities owned by the local client to respect NetworkTransform ownership
    /// </summary>
    public void PositionCombatEntitiesForSpecificFight(NetworkEntity player, NetworkEntity opponentPet)
    {
        if (player == null || opponentPet == null)
        {
            Debug.LogWarning("Cannot position entities - player or opponent pet is null");
            return;
        }

        // Position player only if owned by local client (has NetworkTransform)
        PositionEntityIfOwned(player, playerPositionTransform, "Player");

        // Position opponent pet only if owned by local client (has NetworkTransform)
        PositionEntityIfOwned(opponentPet, opponentPetPositionTransform, "Opponent Pet");

        // Position ALL hands locally since they don't have NetworkTransforms and are always in the same positions
        PositionHandEntityAlways(player, playerHandPositionTransform, "Player Hand");
        PositionHandEntityAlways(opponentPet, opponentPetHandPositionTransform, "Opponent Pet Hand");
        
        // Position ALL stats UI locally since they don't have NetworkTransforms
        PositionStatsUIAlways(player, playerStatsUIPositionTransform, "Player Stats UI");
        PositionStatsUIAlways(opponentPet, opponentPetStatsUIPositionTransform, "Opponent Pet Stats UI");

        // Make entities face each other after positioning (event-driven readiness check)
        SetupEntityFacingWhenReady(player, opponentPet);
    }

    /// <summary>
    /// Sets up entity facing once entities are properly positioned
    /// Uses event-driven approach instead of time delays
    /// </summary>
    private void SetupEntityFacingWhenReady(NetworkEntity player, NetworkEntity opponentPet)
    {
        // Check if entities are immediately ready
        if (AreEntitiesReadyForFacing(player, opponentPet))
        {
            /* Debug.Log($"SetupEntityFacingWhenReady: Entities immediately ready, setting up facing between {player.EntityName.Value} and {opponentPet.EntityName.Value}"); */
            SetupEntityFacing(player, opponentPet);
            return;
        }

        // If not immediately ready, start monitoring for readiness
        StartCoroutine(MonitorEntitiesForFacingReadiness(player, opponentPet));
    }

    /// <summary>
    /// Monitors entities until they're ready for facing setup, checking every frame
    /// More responsive than time-based delays
    /// </summary>
    private IEnumerator MonitorEntitiesForFacingReadiness(NetworkEntity player, NetworkEntity opponentPet)
    {
        const int maxFramesToWait = 300; // 5 seconds at 60fps as safety fallback
        int frameCount = 0;

        while (frameCount < maxFramesToWait)
        {
            // Check every frame for readiness
            yield return null;
            frameCount++;

            // Verify entities are still valid
            if (player == null || opponentPet == null)
            {
                Debug.LogWarning("MonitorEntitiesForFacingReadiness: Entities became null during monitoring");
                yield break;
            }

            // Check if entities are now ready
            if (AreEntitiesReadyForFacing(player, opponentPet))
            {
                /* Debug.Log($"MonitorEntitiesForFacingReadiness: Entities ready after {frameCount} frames, setting up facing between {player.EntityName.Value} and {opponentPet.EntityName.Value}"); */
                SetupEntityFacing(player, opponentPet);
                yield break;
            }
        }

        // Safety fallback if entities never become ready
        Debug.LogWarning($"MonitorEntitiesForFacingReadiness: Entities still not ready after {maxFramesToWait} frames - setting up facing anyway");
        SetupEntityFacing(player, opponentPet);
    }

    /// <summary>
    /// Checks if entities are properly positioned and ready for facing setup
    /// </summary>
    private bool AreEntitiesReadyForFacing(NetworkEntity player, NetworkEntity opponentPet)
    {
        if (player == null || opponentPet == null)
        {
            return false;
        }
        
        // Check if entities are active and positioned
        if (!player.gameObject.activeInHierarchy || !opponentPet.gameObject.activeInHierarchy)
        {
            Debug.Log($"AreEntitiesReadyForFacing: Entities not active - Player: {player.gameObject.activeInHierarchy}, OpponentPet: {opponentPet.gameObject.activeInHierarchy}");
            return false;
        }
        
        // Check if entities have moved from default positions (indicating they've been positioned)
        Vector3 playerPos = player.transform.position;
        Vector3 opponentPos = opponentPet.transform.position;
        
        // If both entities are at origin or very close to each other, they might not be positioned yet
        float distance = Vector3.Distance(playerPos, opponentPos);
        if (distance < 0.1f)
        {
            /* Debug.Log($"AreEntitiesReadyForFacing: Entities too close together (distance: {distance:F3}), might not be positioned yet"); */
            return false;
        }
        
        /* Debug.Log($"AreEntitiesReadyForFacing: Entities ready - Player: {playerPos}, OpponentPet: {opponentPos}, Distance: {distance:F3}"); */
        return true;
    }

    /// <summary>
    /// Sets up facing between combat entities so they look at each other
    /// Only rotates entities owned by the local client to respect NetworkTransform ownership
    /// </summary>
    private void SetupEntityFacing(NetworkEntity player, NetworkEntity opponentPet)
    {
        if (player == null || opponentPet == null)
        {
            Debug.LogWarning("Cannot setup entity facing - player or opponent pet is null");
            return;
        }

        // Get the ally pet for the player
        NetworkEntity allyPet = GetAllyPetForPlayer(player);

        // Make player face opponent pet (only if owned by local client)
        if (player.IsOwner)
        {
            SetEntityFacing(player, opponentPet, "Player");
        }

        // Make ally pet face opponent pet (only if owned by local client)
        if (allyPet != null && allyPet.IsOwner)
        {
            SetEntityFacing(allyPet, opponentPet, "Ally Pet");
        }

        // Make opponent pet face player (only if owned by local client)
        if (opponentPet.IsOwner)
        {
            // Choose the primary target (prefer player over ally pet)
            NetworkEntity targetEntity = player;
            SetEntityFacing(opponentPet, targetEntity, "Opponent Pet");
        }
    }

    /// <summary>
    /// Helper method to get the ally pet for a player
    /// </summary>
    private NetworkEntity GetAllyPetForPlayer(NetworkEntity player)
    {
        if (player == null) return null;

        RelationshipManager relationshipManager = player.GetComponent<RelationshipManager>();
        if (relationshipManager == null || relationshipManager.AllyEntity == null)
        {
            return null;
        }

        return relationshipManager.AllyEntity.GetComponent<NetworkEntity>();
    }

    /// <summary>
    /// Makes one entity face another entity (Y rotation only)
    /// </summary>
    /// <param name="entity">The entity that will be rotated to face the target</param>
    /// <param name="target">The entity to face towards</param>
    /// <param name="entityDescription">Description for debugging</param>
    private void SetEntityFacing(NetworkEntity entity, NetworkEntity target, string entityDescription)
    {
        if (entity == null || target == null)
        {
            Debug.LogWarning($"Cannot set facing for {entityDescription} - entity or target is null");
            return;
        }

        // Calculate direction from entity to target
        Vector3 directionToTarget = target.transform.position - entity.transform.position;
        
        // Remove Y component to only rotate around Y axis
        directionToTarget.y = 0;

        // Check if there's any horizontal distance
        if (directionToTarget.magnitude < 0.01f)
        {
            Debug.LogWarning($"Entities {entity.EntityName.Value} and {target.EntityName.Value} are too close horizontally to determine facing direction");
            return;
        }

        // Calculate the rotation needed to face the target
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

        // Store the old rotation for logging
        Vector3 oldEulerAngles = entity.transform.rotation.eulerAngles;

        // Apply the rotation
        entity.transform.rotation = targetRotation;

        /* Debug.Log($"[ENTITY_FACING] {entityDescription} {entity.EntityName.Value}: Rotated from Y={oldEulerAngles.y:F1}° to Y={targetRotation.eulerAngles.y:F1}° to face {target.EntityName.Value}"); */
    }

    /// <summary>
    /// Positions combat entities in their designated UI positions (legacy method for current viewed fight)
    /// Only positions entities owned by the local client to respect NetworkTransform ownership
    /// </summary>
    public void PositionCombatEntities(NetworkEntity player, NetworkEntity opponentPet)
    {
        PositionCombatEntitiesForSpecificFight(player, opponentPet);
    }

    /// <summary>
    /// Positions an entity only if it's owned by the local client
    /// </summary>
    private void PositionEntityIfOwned(NetworkEntity entity, Transform targetPosition, string entityDescription)
    {
        if (entity == null || targetPosition == null)
        {
            if (entity == null)
                Debug.LogError($"{entityDescription} entity is null");
            if (targetPosition == null)
                Debug.LogError($"{entityDescription} target position transform is null");
            return;
        }

        if (entity.IsOwner)
        {
            Vector3 oldPosition = entity.transform.position;
            entity.transform.position = targetPosition.position;
        }
        else
        {
            Debug.Log($"SKIPPING {entityDescription} {entity.EntityName.Value} - not owned by local client. Current position: {entity.transform.position}");
        }
    }

    /// <summary>
    /// Enhanced positioning method with better debugging
    /// </summary>
    private void PositionHandEntityAlways(NetworkEntity ownerEntity, Transform targetPosition, string handDescription)
    {
        /* Debug.Log($"[POSITIONING_DEBUG] PositionHandEntityAlways called for {handDescription}"); */
        
        if (ownerEntity == null || targetPosition == null)
        {
            if (ownerEntity == null)
                Debug.LogError($"[POSITIONING_DEBUG] Owner entity for {handDescription} is null");
            if (targetPosition == null)
                Debug.LogError($"[POSITIONING_DEBUG] Target position for {handDescription} is null");
            return;
        }

        /* Debug.Log($"[POSITIONING_DEBUG] Owner entity: {ownerEntity.EntityName.Value}"); */
        /* Debug.Log($"[POSITIONING_DEBUG] Target position: {targetPosition.name}"); */

        var relationshipManager = ownerEntity.GetComponent<RelationshipManager>();
        if (relationshipManager == null)
        {
            Debug.LogError($"[POSITIONING_DEBUG] {ownerEntity.EntityName.Value} missing RelationshipManager - cannot find {handDescription}");
            return;
        }

        if (relationshipManager.HandEntity == null)
        {
            Debug.LogWarning($"[POSITIONING_DEBUG] {ownerEntity.EntityName.Value} has no hand entity - cannot position {handDescription}");
            return;
        }

        var handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
        if (handEntity == null)
        {
            Debug.LogError($"[POSITIONING_DEBUG] {handDescription} entity found but missing NetworkEntity component");
            return;
        }

        /* Debug.Log($"[POSITIONING_DEBUG] Hand entity found: {handEntity.EntityName.Value}"); */

        // Check combat canvas availability
        if (combatCanvas == null)
        {
            Debug.LogError($"[POSITIONING_DEBUG] Combat canvas is NULL! Cannot reparent {handDescription}");
            return;
        }

        /* Debug.Log($"[POSITIONING_DEBUG] Combat canvas available: {combatCanvas.name}"); */

        // Hand entity should be a RectTransform, target position can be regular Transform (position marker)
        RectTransform handRectTransform = handEntity.transform as RectTransform;
        
        if (handRectTransform == null)
        {
            Debug.LogError($"[POSITIONING_DEBUG] Hand entity {handEntity.EntityName.Value} does not have RectTransform! Hand entities must be UI elements.");
            return;
        }

        /* Debug.Log($"[POSITIONING_DEBUG] Hand entity current parent: {(handRectTransform.parent != null ? handRectTransform.parent.name : "NULL/ROOT")}"); */
        /* Debug.Log($"[POSITIONING_DEBUG] Combat canvas transform: {combatCanvas.transform.name}"); */
        
        // Ensure the hand entity is a child of the main combat canvas if it isn't already
        if (handRectTransform.parent != combatCanvas.transform)
        {
            /* Debug.Log($"[UI_HIERARCHY] {handDescription} {handEntity.EntityName.Value}: Moving to be child of combat canvas"); */
            /* Debug.Log($"[UI_HIERARCHY] Before reparenting - Parent: {(handRectTransform.parent != null ? handRectTransform.parent.name : "NULL")}"); */
            
            handRectTransform.SetParent(combatCanvas.transform, false);
            
            /* Debug.Log($"[UI_HIERARCHY] After reparenting - Parent: {(handRectTransform.parent != null ? handRectTransform.parent.name : "NULL")}"); */
        }
        else
        {
            /* Debug.Log($"[UI_HIERARCHY] {handDescription} {handEntity.EntityName.Value}: Already child of combat canvas"); */
        }
        
        // Position the hand entity based on the target position
        Vector2 oldAnchoredPosition = handRectTransform.anchoredPosition;
        
        // Check if target is a RectTransform (copy properties) or regular Transform (convert position)
        RectTransform targetRectTransform = targetPosition as RectTransform;
        
        if (targetRectTransform != null)
        {
            // Target is also a RectTransform - copy its properties
            handRectTransform.anchorMin = targetRectTransform.anchorMin;
            handRectTransform.anchorMax = targetRectTransform.anchorMax;
            handRectTransform.anchoredPosition = targetRectTransform.anchoredPosition;
            handRectTransform.sizeDelta = targetRectTransform.sizeDelta;
            handRectTransform.pivot = targetRectTransform.pivot;
            
            /* Debug.Log($"[UI_POSITIONING] {handDescription} {handEntity.EntityName.Value}: Copied RectTransform properties from target"); */
        }
        else
        {
            // Target is a regular Transform (position marker) - convert its local position to anchored position
            Canvas parentCanvas = combatCanvas.GetComponent<Canvas>();
            if (parentCanvas == null)
            {
                Debug.LogError($"[POSITIONING_DEBUG] Combat canvas missing Canvas component!");
                return;
            }
            
            // Convert the target position to local position relative to the canvas
            Vector3 targetLocalPosition = combatCanvas.transform.InverseTransformPoint(targetPosition.position);
            
            // Use the target's local position as the anchored position
            handRectTransform.anchoredPosition = new Vector2(targetLocalPosition.x, targetLocalPosition.y);
            
            // Set reasonable default anchoring (centered)
            handRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            handRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            handRectTransform.pivot = new Vector2(0.5f, 0.5f);
            
            /* Debug.Log($"[UI_POSITIONING] {handDescription} {handEntity.EntityName.Value}: Converted Transform position {targetPosition.position} to anchored position {handRectTransform.anchoredPosition}"); */
        }
        
        // Ensure proper layer ordering
        handRectTransform.SetAsLastSibling();
        
        /* Debug.Log($"[UI_POSITIONING] {handDescription} {handEntity.EntityName.Value}: Positioned from {oldAnchoredPosition} to {handRectTransform.anchoredPosition}"); */
    }

    /// <summary>
    /// Enhanced positioning method for stats UI with better debugging
    /// </summary>
    private void PositionStatsUIAlways(NetworkEntity ownerEntity, Transform targetPosition, string statsUIDescription)
    {
        /* Debug.Log($"[POSITIONING_DEBUG] PositionStatsUIAlways called for {statsUIDescription}"); */
        
        if (ownerEntity == null || targetPosition == null)
        {
            if (ownerEntity == null)
                Debug.LogError($"[POSITIONING_DEBUG] Owner entity for {statsUIDescription} is null");
            if (targetPosition == null)
                Debug.LogError($"[POSITIONING_DEBUG] Target position for {statsUIDescription} is null");
            return;
        }

        /* Debug.Log($"[POSITIONING_DEBUG] Owner entity: {ownerEntity.EntityName.Value}"); */
        /* Debug.Log($"[POSITIONING_DEBUG] Target position: {targetPosition.name}"); */

        var relationshipManager = ownerEntity.GetComponent<RelationshipManager>();
        if (relationshipManager == null)
        {
            Debug.LogError($"[POSITIONING_DEBUG] {ownerEntity.EntityName.Value} missing RelationshipManager - cannot find {statsUIDescription}");
            return;
        }

        if (relationshipManager.StatsUIEntity == null)
        {
            Debug.LogWarning($"[POSITIONING_DEBUG] {ownerEntity.EntityName.Value} has no stats UI entity - cannot position {statsUIDescription}");
            return;
        }

        var statsUIEntity = relationshipManager.StatsUIEntity.GetComponent<NetworkEntity>();
        if (statsUIEntity == null)
        {
            Debug.LogError($"[POSITIONING_DEBUG] {statsUIDescription} entity found but missing NetworkEntity component");
            return;
        }

        /* Debug.Log($"[POSITIONING_DEBUG] Stats UI entity found: {statsUIEntity.EntityName.Value}"); */

        // Check combat canvas availability
        if (combatCanvas == null)
        {
            Debug.LogError($"[POSITIONING_DEBUG] Combat canvas is NULL! Cannot reparent {statsUIDescription}");
            return;
        }

        /* Debug.Log($"[POSITIONING_DEBUG] Combat canvas available: {combatCanvas.name}"); */

        // Stats UI entity should be a RectTransform, target position can be regular Transform (position marker)
        RectTransform statsUIRectTransform = statsUIEntity.transform as RectTransform;
        
        if (statsUIRectTransform == null)
        {
            Debug.LogError($"[POSITIONING_DEBUG] Stats UI entity {statsUIEntity.EntityName.Value} does not have RectTransform! Stats UI entities must be UI elements.");
            return;
        }

        /* Debug.Log($"[POSITIONING_DEBUG] Stats UI entity current parent: {(statsUIRectTransform.parent != null ? statsUIRectTransform.parent.name : "NULL/ROOT")}"); */
        /* Debug.Log($"[POSITIONING_DEBUG] Combat canvas transform: {combatCanvas.transform.name}"); */
        
        // Ensure the stats UI entity is a child of the main combat canvas if it isn't already
        if (statsUIRectTransform.parent != combatCanvas.transform)
        {
            /* Debug.Log($"[UI_HIERARCHY] {statsUIDescription} {statsUIEntity.EntityName.Value}: Moving to be child of combat canvas"); */
            /* Debug.Log($"[UI_HIERARCHY] Before reparenting - Parent: {(statsUIRectTransform.parent != null ? statsUIRectTransform.parent.name : "NULL")}"); */
            
            statsUIRectTransform.SetParent(combatCanvas.transform, false);
            
            /* Debug.Log($"[UI_HIERARCHY] After reparenting - Parent: {(statsUIRectTransform.parent != null ? statsUIRectTransform.parent.name : "NULL")}"); */
        }
        else
        {
            /* Debug.Log($"[UI_HIERARCHY] {statsUIDescription} {statsUIEntity.EntityName.Value}: Already child of combat canvas"); */
        }
        
        // Position the stats UI entity based on the target position
        Vector2 oldAnchoredPosition = statsUIRectTransform.anchoredPosition;
        
        // Check if target is a RectTransform (copy properties) or regular Transform (convert position)
        RectTransform targetRectTransform = targetPosition as RectTransform;
        
        if (targetRectTransform != null)
        {
            // Target is also a RectTransform - copy its properties
            statsUIRectTransform.anchorMin = targetRectTransform.anchorMin;
            statsUIRectTransform.anchorMax = targetRectTransform.anchorMax;
            statsUIRectTransform.anchoredPosition = targetRectTransform.anchoredPosition;
            statsUIRectTransform.sizeDelta = targetRectTransform.sizeDelta;
            statsUIRectTransform.pivot = targetRectTransform.pivot;
            
            /* Debug.Log($"[UI_POSITIONING] {statsUIDescription} {statsUIEntity.EntityName.Value}: Copied RectTransform properties from target"); */
        }
        else
        {
            // Target is a regular Transform (position marker) - convert its local position to anchored position
            Canvas parentCanvas = combatCanvas.GetComponent<Canvas>();
            if (parentCanvas == null)
            {
                Debug.LogError($"[POSITIONING_DEBUG] Combat canvas missing Canvas component!");
                return;
            }
            
            // Convert the target position to local position relative to the canvas
            Vector3 targetLocalPosition = combatCanvas.transform.InverseTransformPoint(targetPosition.position);
            
            // Use the target's local position as the anchored position
            statsUIRectTransform.anchoredPosition = new Vector2(targetLocalPosition.x, targetLocalPosition.y);
            
            // Set reasonable default anchoring (centered)
            statsUIRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            statsUIRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            statsUIRectTransform.pivot = new Vector2(0.5f, 0.5f);
            
            /* Debug.Log($"[UI_POSITIONING] {statsUIDescription} {statsUIEntity.EntityName.Value}: Converted Transform position {targetPosition.position} to anchored position {statsUIRectTransform.anchoredPosition}"); */
        }
        
        // Ensure proper layer ordering
        statsUIRectTransform.SetAsLastSibling();
        
        /* Debug.Log($"[UI_POSITIONING] {statsUIDescription} {statsUIEntity.EntityName.Value}: Positioned from {oldAnchoredPosition} to {statsUIRectTransform.anchoredPosition}"); */
    }

    /// <summary>
    /// Debug method to manually trigger positioning for testing
    /// </summary>
    [ContextMenu("Debug Manual Position Entities")]
    public void DebugManualPositionEntities()
    {
        /* Debug.Log("=== DEBUG: Manual positioning trigger ==="); */
        
        if (combatCanvas == null)
        {
            Debug.LogError("DEBUG: Combat canvas is null!");
            return;
        }
        
        // Try to call the positioning method directly
        PositionCombatEntitiesForAllFights();
        
        Debug.Log("=== DEBUG: Manual positioning complete ===");
    }

    /// <summary>
    /// Debug method to manually check and fix hand entity parenting
    /// Call this from the Unity Inspector or via code to test reparenting
    /// </summary>
    [ContextMenu("Debug Fix Hand Entity Parenting")]
    public void DebugFixHandEntityParenting()
    {
        /* Debug.Log("=== DEBUG: Starting hand entity parenting check ==="); */
        
        // Check if combat canvas is available
        if (combatCanvas == null)
        {
            Debug.LogError("DEBUG: combatCanvas is NULL! Cannot reparent hand entities.");
            return;
        }
        
        /* Debug.Log($"DEBUG: Combat canvas found: {combatCanvas.name}"); */
        
        // Find all hand entities in the scene
        NetworkEntity[] allEntities = FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        
        foreach (var entity in allEntities)
        {
            if (entity.EntityType == EntityType.PlayerHand || entity.EntityType == EntityType.PetHand)
            {
                /* Debug.Log($"DEBUG: Found hand entity: {entity.EntityName.Value} (Type: {entity.EntityType})"); */
                /* Debug.Log($"DEBUG: Current parent: {(entity.transform.parent != null ? entity.transform.parent.name : "NULL/ROOT")}"); */
                /* Debug.Log($"DEBUG: Current position: {entity.transform.position}"); */
                
                RectTransform handRectTransform = entity.transform as RectTransform;
                if (handRectTransform == null)
                {
                    Debug.LogError($"DEBUG: Hand entity {entity.EntityName.Value} does not have RectTransform!");
                    continue;
                }
                
                // Force reparent to combat canvas
                if (handRectTransform.parent != combatCanvas.transform)
                {
                    /* Debug.Log($"DEBUG: Reparenting {entity.EntityName.Value} to combat canvas"); */
                    handRectTransform.SetParent(combatCanvas.transform, false);
                    /* Debug.Log($"DEBUG: New parent: {handRectTransform.parent.name}"); */
                }
                else
                {
                    /* Debug.Log($"DEBUG: {entity.EntityName.Value} is already a child of combat canvas"); */
                }
            }
        }
        
        /* Debug.Log("=== DEBUG: Hand entity parenting check complete ==="); */
    }

    /// <summary>
    /// Debug method to manually test entity facing functionality
    /// Call this from the Unity Inspector to test entity rotation
    /// </summary>
    [ContextMenu("Debug Test Entity Facing")]
    public void DebugTestEntityFacing()
    {
        /* Debug.Log("=== DEBUG: Starting entity facing test ==="); */
        
        if (fightManager == null)
        {
            Debug.LogError("DEBUG: FightManager is null, cannot test entity facing");
            return;
        }
        
        // Get all fight assignments
        var allFights = fightManager.GetAllFightAssignments();
        
        if (allFights.Count == 0)
        {
            Debug.LogWarning("DEBUG: No fight assignments found, cannot test entity facing");
            return;
        }
        
        /* Debug.Log($"DEBUG: Found {allFights.Count} fight assignments, testing facing for each"); */
        
        foreach (var fightAssignment in allFights)
        {
            NetworkEntity player = GetNetworkEntityByObjectId(fightAssignment.PlayerObjectId);
            NetworkEntity opponentPet = GetNetworkEntityByObjectId(fightAssignment.PetObjectId);
            
            if (player == null || opponentPet == null)
            {
                Debug.LogWarning($"DEBUG: Could not find entities for fight - Player ID: {fightAssignment.PlayerObjectId}, Pet ID: {fightAssignment.PetObjectId}");
                continue;
            }
            
            /* Debug.Log($"DEBUG: Testing facing for fight - Player: {player.EntityName.Value}, Opponent Pet: {opponentPet.EntityName.Value}"); */
            /* Debug.Log($"DEBUG: Player position: {player.transform.position}, rotation: {player.transform.rotation.eulerAngles}"); */
            /* Debug.Log($"DEBUG: Opponent Pet position: {opponentPet.transform.position}, rotation: {opponentPet.transform.rotation.eulerAngles}"); */
            
            // Test the facing setup
            SetupEntityFacing(player, opponentPet);
            
            /* Debug.Log($"DEBUG: After facing setup - Player rotation: {player.transform.rotation.eulerAngles}, Opponent Pet rotation: {opponentPet.transform.rotation.eulerAngles}"); */
        }
        
        /* Debug.Log("=== DEBUG: Entity facing test complete ==="); */
    }
} 