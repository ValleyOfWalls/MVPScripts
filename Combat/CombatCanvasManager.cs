using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using TMPro;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
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

    // Legacy Own Pet View system removed - replaced by FighterAllyDisplayController

    [Header("Fighter Ally Displays")]
    [SerializeField] private Transform leftAllyDisplayContainer;
    [SerializeField] private Transform rightAllyDisplayContainer;
    [SerializeField] private GameObject fighterAllyDisplayPrefab;
    [SerializeField] private FighterAllyDisplayController leftAllyDisplayController;
    [SerializeField] private FighterAllyDisplayController rightAllyDisplayController;

    [Header("Deck Viewer")]
    [SerializeField] private DeckViewerManager deckViewerManager;

    [Header("Queue Visualization")]
    [SerializeField] private CardQueueVisualizationManager queueVisualizationManager;

    [Header("Combat Entity Positioning")]
    [SerializeField] private Transform playerPositionTransform;
    [SerializeField] private Transform opponentPetPositionTransform;
    [SerializeField] private Transform playerHandPositionTransform;
    [SerializeField] private Transform opponentPetHandPositionTransform;
    
    [Header("Stats UI Positioning")]
    [SerializeField] private Transform playerStatsUIPositionTransform;
    [SerializeField] private Transform opponentPetStatsUIPositionTransform;

    private NetworkEntity localPlayer;
    private NetworkEntity opponentForLocalPlayer;
    
    private FightManager fightManager;
    private CombatManager combatManager;
    private CombatTestManager testManager;
    
    // Arena integration
    private ArenaManager arenaManager;
    private CameraManager cameraManager;
    
    // Model positioning tracking
    private bool isModelPositioningComplete = false;
    private LoadingScreenManager loadingScreenManager;
    
    // Public property to check if positioning is complete
    public bool IsModelPositioningComplete => isModelPositioningComplete;

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
        if (arenaManager == null) arenaManager = FindFirstObjectByType<ArenaManager>();
        if (cameraManager == null) cameraManager = FindFirstObjectByType<CameraManager>();

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
        opponentForLocalPlayer = fightManager.GetOpponent(localPlayer);

        if (opponentForLocalPlayer == null)
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

        while (opponentForLocalPlayer == null && retryCount < maxRetries)
        {
            yield return new WaitForSeconds(retryDelay);
            opponentForLocalPlayer = fightManager.GetOpponent(localPlayer);
            retryCount++;
        }

        if (opponentForLocalPlayer != null)
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
        if (localPlayer != null && opponentForLocalPlayer != null)
        {
            // Initialize button listeners
            InitializeButtonListeners();
            
            // Setup dual ally displays (replaces legacy OwnPetViewController)
            SetupFighterAllyDisplays();
            
            // Setup deck viewer
            SetupDeckViewer();
            
            // Setup queue visualization
            SetupQueueVisualization();
            
            // Position combat entities for all fights
            PositionCombatEntitiesForAllFights();
            
            // Position camera for the local player's arena
            Debug.Log("[ARENA_CAMERA] CombatCanvasManager: About to call SetupInitialCameraPosition()");
            SetupInitialCameraPosition();
            
            // NOTE: We don't mark positioning as complete here anymore
            // That will happen when all facing operations are finished
            Debug.Log("CombatCanvasManager: Entity positioning completed, waiting for facing operations to complete");
        }
        else
        {
            Debug.LogError($"Cannot complete UI setup - localPlayer: {(localPlayer != null ? "found" : "null")}, opponentPet: {(opponentForLocalPlayer != null ? "found" : "null")}");
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
                    testManager.StartOpponentPerspectiveTests();
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
                // Use reflection to get the CardEffectGenerationValidator type since it's in Editor assembly
                var validatorType = System.Type.GetType("CardEffectGenerationValidator");
                if (validatorType != null)
                {
                    var window = UnityEditor.EditorWindow.GetWindow(validatorType, false, "Card Effect Validator");
                    window.Show();
                    Debug.Log("Opening Card Effect Validator window...");
                }
                else
                {
                    Debug.LogError("CardEffectGenerationValidator class not found. Make sure it's in the Editor folder.");
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
                // Use reflection to get the DescriptionReviewGenerator type since it's in Editor assembly
                var descriptionReviewType = System.Type.GetType("DescriptionReviewGenerator");
                if (descriptionReviewType != null)
                {
                    var window = UnityEditor.EditorWindow.GetWindow(descriptionReviewType, false, "Description Review Generator");
                    window.Show();
                    Debug.Log("Opening Description Review Generator window...");
                }
                else
                {
                    Debug.LogError("DescriptionReviewGenerator class not found. Make sure it's in the Editor folder.");
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

    // Legacy SetupOwnPetView method removed - replaced by SetupFighterAllyDisplays()

    /// <summary>
    /// Called when the viewed combat changes (e.g., when spectating)
    /// Updates the own pet view to show the new viewed player's pet
    /// </summary>
    public void OnViewedCombatChanged()
    {
        Debug.Log("CombatCanvasManager: Viewed combat changed, updating displays and positioning");

        // Update ally displays (replaces legacy OwnPetViewController)
        if (leftAllyDisplayController != null)
        {
            leftAllyDisplayController.ForceUpdate();
        }
        
        if (rightAllyDisplayController != null)
        {
            rightAllyDisplayController.ForceUpdate();
        }
        
        // Update entity positioning and facing for the new viewed combat
        if (fightManager != null)
        {
                    var viewedLeftFighter = fightManager.ViewedLeftFighter;
        var viewedRightFighter = fightManager.ViewedRightFighter;
            
            if (viewedLeftFighter != null && viewedRightFighter != null)
            {
                /* Debug.Log($"CombatCanvasManager: Updating entity positioning and facing for viewed combat - Left Fighter: {viewedLeftFighter.EntityName.Value}, Right Fighter: {viewedRightFighter.EntityName.Value}"); */
                PositionCombatEntitiesForSpecificFight(viewedLeftFighter, viewedRightFighter);
                
                // Move camera to view the new arena
                if (cameraManager != null)
                {
                    Debug.Log($"[ARENA_CAMERA] CombatCanvasManager requesting camera move to view {viewedLeftFighter.EntityName.Value}'s arena");
                    cameraManager.MoveCameraToCurrentViewedFight(true);
                    Debug.Log($"CombatCanvasManager: Moving camera to view {viewedLeftFighter.EntityName.Value}'s arena");
                }
                else
                {
                    Debug.LogWarning("CombatCanvasManager: CameraManager not found, cannot move camera for arena viewing");
                    Debug.Log("[ARENA_CAMERA] ERROR: CameraManager not found in CombatCanvasManager");
                }
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
            
            // Refresh ally displays when canvas is re-enabled
            if (leftAllyDisplayController != null)
            {
                leftAllyDisplayController.ForceUpdate();
            }
            
            if (rightAllyDisplayController != null)
            {
                rightAllyDisplayController.ForceUpdate();
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

    // Legacy GetOwnPetViewController method removed - use GetLeftAllyDisplayController() or GetRightAllyDisplayController() instead

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
    /// Sets up the queue visualization manager
    /// </summary>
    private void SetupQueueVisualization()
    {
        if (queueVisualizationManager == null)
        {
            queueVisualizationManager = FindFirstObjectByType<CardQueueVisualizationManager>();
        }
        
        if (queueVisualizationManager != null)
        {
            Debug.Log("CombatCanvasManager: Queue visualization manager found and assigned");
        }
        else
        {
            Debug.LogWarning("CombatCanvasManager: Queue visualization manager not found");
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
    /// Gets the queue visualization manager for external access
    /// </summary>
    public CardQueueVisualizationManager GetQueueVisualizationManager()
    {
        return queueVisualizationManager;
    }
    
    // Legacy RPC methods for OwnPetViewController removed

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
        
        // Reset facing operation counters
        totalFacingOperations = 0;
        completedFacingOperations = 0;
        
        // Get all fight assignments from the FightManager
        var allFights = fightManager.GetAllFightAssignments();
        
        // Count total facing operations needed
        totalFacingOperations = allFights.Count;
        
        Debug.Log($"[ENTITY_FACING] Starting entity positioning for {allFights.Count} fights");
        
        foreach (var fightAssignment in allFights)
        {
            // Get the actual NetworkEntity objects from the IDs
            NetworkEntity leftFighter = GetNetworkEntityByObjectId(fightAssignment.LeftFighterObjectId);
            NetworkEntity rightFighter = GetNetworkEntityByObjectId(fightAssignment.RightFighterObjectId);
            
            if (leftFighter == null || rightFighter == null)
            {
                Debug.LogWarning($"Cannot find entities for fight: Left Fighter ID {fightAssignment.LeftFighterObjectId}, Right Fighter ID {fightAssignment.RightFighterObjectId}");
                // Reduce total count if fight can't be processed
                totalFacingOperations--;
                continue;
            }
            
            // Position entities for this specific fight (includes facing setup)
            PositionCombatEntitiesForSpecificFight(leftFighter, rightFighter);
        }
        
        // If no facing operations needed, mark as complete immediately
        if (totalFacingOperations == 0)
        {
            OnAllFacingOperationsComplete();
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

        // Use arena-based positioning if ArenaManager is available
        if (arenaManager != null && arenaManager.IsInitialized)
        {
            PositionEntitiesInArena(player, opponentPet);
        }
        else
        {
            // Fallback to original positioning system
            PositionEntitiesLegacy(player, opponentPet);
        }
    }
    
    /// <summary>
    /// Positions entities in their designated arena using ArenaManager
    /// </summary>
    private void PositionEntitiesInArena(NetworkEntity player, NetworkEntity opponentPet)
    {
        // Get the arena for this player
        uint playerObjectId = (uint)player.ObjectId;
        int arenaIndex = arenaManager.GetArenaForPlayer(playerObjectId);
        
        if (arenaIndex < 0)
        {
            Debug.LogWarning($"No arena found for player {playerObjectId}, using fallback positioning");
            PositionEntitiesLegacy(player, opponentPet);
            return;
        }
        
        // Get exact positions for entities in this arena (based on captured reference positions)
        Vector3 playerArenaPos = arenaManager.GetPlayerPositionInArena(arenaIndex);
        Vector3 opponentArenaPos = arenaManager.GetOpponentPetPositionInArena(arenaIndex);
        
        // Position main entities (player and opponent pet) only if owned by local client
        if (player.IsOwner)
        {
            Debug.Log($"NETPOS: Setting {player.EntityName.Value} (ID: {player.ObjectId}) position from {player.transform.position} to {playerArenaPos} - IsOwner: {player.IsOwner}");
            player.transform.position = playerArenaPos;
            Debug.Log($"NETPOS: After setting, {player.EntityName.Value} position is {player.transform.position}");
            Debug.Log($"[ARENA_POSITIONING] Player {player.EntityName.Value} positioned at arena {arenaIndex}: {playerArenaPos}");
        }
        else
        {
            Debug.Log($"NETPOS: Skipping {player.EntityName.Value} (ID: {player.ObjectId}) positioning - IsOwner: {player.IsOwner}, NetworkTransform should handle this");
        }
        
        if (opponentPet.IsOwner)
        {
            Debug.Log($"NETPOS: Setting {opponentPet.EntityName.Value} (ID: {opponentPet.ObjectId}) position from {opponentPet.transform.position} to {opponentArenaPos} - IsOwner: {opponentPet.IsOwner}");
            opponentPet.transform.position = opponentArenaPos;
            Debug.Log($"NETPOS: After setting, {opponentPet.EntityName.Value} position is {opponentPet.transform.position}");
            Debug.Log($"[ARENA_POSITIONING] Opponent Pet {opponentPet.EntityName.Value} positioned at arena {arenaIndex}: {opponentArenaPos}");
        }
        else
        {
            Debug.Log($"NETPOS: Skipping {opponentPet.EntityName.Value} (ID: {opponentPet.ObjectId}) positioning - IsOwner: {opponentPet.IsOwner}, NetworkTransform should handle this");
        }
        
        // Position hands and stats UI for ALL entities (these don't have NetworkTransforms)
        // Calculate relative positions from the captured reference positions
        if (arenaManager.ReferencePositionsCaptured)
        {
            Vector3 referenceCenter = (arenaManager.ReferencePlayerPosition + arenaManager.ReferenceOpponentPetPosition) * 0.5f;
            Vector3 playerHandRelative = playerHandPositionTransform.position - referenceCenter;
            Vector3 opponentHandRelative = opponentPetHandPositionTransform.position - referenceCenter;
            Vector3 playerStatsRelative = playerStatsUIPositionTransform.position - referenceCenter;
            Vector3 opponentStatsRelative = opponentPetStatsUIPositionTransform.position - referenceCenter;
            
            PositionHandEntityInArena(player, arenaIndex, playerHandRelative, "Player Hand");
            PositionHandEntityInArena(opponentPet, arenaIndex, opponentHandRelative, "Opponent Pet Hand");
            PositionStatsUIInArena(player, arenaIndex, playerStatsRelative, "Player Stats UI");
            PositionStatsUIInArena(opponentPet, arenaIndex, opponentStatsRelative, "Opponent Pet Stats UI");
        }
        else
        {
            // Fallback to local positions if reference positions weren't captured
            PositionHandEntityInArena(player, arenaIndex, playerHandPositionTransform.localPosition, "Player Hand");
            PositionHandEntityInArena(opponentPet, arenaIndex, opponentPetHandPositionTransform.localPosition, "Opponent Pet Hand");
            PositionStatsUIInArena(player, arenaIndex, playerStatsUIPositionTransform.localPosition, "Player Stats UI");
            PositionStatsUIInArena(opponentPet, arenaIndex, opponentPetStatsUIPositionTransform.localPosition, "Opponent Pet Stats UI");
        }
        
        // Make entities face each other after positioning
        SetupEntityFacingWhenReady(player, opponentPet);
        
        Debug.Log($"[ARENA_POSITIONING] Completed positioning for fight in arena {arenaIndex}");
    }
    
    /// <summary>
    /// Legacy positioning method (original system)
    /// </summary>
    private void PositionEntitiesLegacy(NetworkEntity player, NetworkEntity opponentPet)
    {
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

    // Position tracking for event-driven facing setup
    private Dictionary<NetworkEntity, Vector3> lastTrackedPositions = new Dictionary<NetworkEntity, Vector3>();
    private Dictionary<string, System.Action> pendingFacingCallbacks = new Dictionary<string, System.Action>();
    private int totalFacingOperations = 0;
    private int completedFacingOperations = 0;
    
    /// <summary>
    /// Called when all facing operations have completed
    /// </summary>
    private void OnAllFacingOperationsComplete()
    {
        Debug.Log("[ENTITY_FACING] All facing operations completed, marking positioning as complete");
        
        // Mark positioning as complete and notify loading screen
        isModelPositioningComplete = true;
        NotifyLoadingScreenPositioningComplete();
        
        Debug.Log("CombatCanvasManager: All positioning and facing completed, loading screen can be hidden");
    }
    
    /// <summary>
    /// Called when a single facing operation completes
    /// </summary>
    private void OnFacingOperationComplete()
    {
        completedFacingOperations++;
        Debug.Log($"[ENTITY_FACING] Facing operation completed ({completedFacingOperations}/{totalFacingOperations})");
        
        if (completedFacingOperations >= totalFacingOperations)
        {
            OnAllFacingOperationsComplete();
        }
    }
    
    /// <summary>
    /// Sets up entity facing once entities are properly positioned
    /// Uses event-driven approach instead of time delays
    /// </summary>
    private void SetupEntityFacingWhenReady(NetworkEntity player, NetworkEntity opponentPet)
    {
        Debug.Log($"[ENTITY_FACING] SetupEntityFacingWhenReady called for {player?.EntityName.Value ?? "null"} vs {opponentPet?.EntityName.Value ?? "null"}");
        
        // Check if entities are immediately ready
        if (AreEntitiesReadyForFacing(player, opponentPet))
        {
            Debug.Log($"[ENTITY_FACING] Entities immediately ready, setting up facing between {player.EntityName.Value} and {opponentPet.EntityName.Value}");
            SetupEntityFacing(player, opponentPet);
            OnFacingOperationComplete(); // Mark this operation as complete
            return;
        }

        // Set up position tracking for these entities
        string fightKey = $"{player.ObjectId}_{opponentPet.ObjectId}";
        pendingFacingCallbacks[fightKey] = () => {
            SetupEntityFacing(player, opponentPet);
            OnFacingOperationComplete(); // Mark this operation as complete
        };
        
        Debug.Log($"[ENTITY_FACING] Entities not immediately ready, setting up position tracking for {player.EntityName.Value} vs {opponentPet.EntityName.Value}");
        
        // Start tracking both entities
        StartTrackingEntityPosition(player);
        StartTrackingEntityPosition(opponentPet);
    }
    
    /// <summary>
    /// Starts tracking position changes for an entity
    /// </summary>
    private void StartTrackingEntityPosition(NetworkEntity entity)
    {
        if (entity == null) return;
        
        // Initialize or update the tracked position
        lastTrackedPositions[entity] = entity.transform.position;
        
        Debug.Log($"NETPOS: Started tracking {entity.EntityName.Value} (ID: {entity.ObjectId}) at {entity.transform.position}, IsOwner: {entity.IsOwner}");
        Debug.Log($"[ENTITY_FACING] Started tracking position for {entity.EntityName.Value}: {entity.transform.position}");
    }
    
    /// <summary>
    /// Monitors tracked entity positions and triggers facing setup when ready
    /// Called from Update() for event-driven position monitoring
    /// </summary>
    private void CheckTrackedEntityPositions()
    {
        if (pendingFacingCallbacks.Count == 0) return;
        
        List<string> readyFights = new List<string>();
        
        foreach (var kvp in pendingFacingCallbacks)
        {
            string fightKey = kvp.Key;
            var parts = fightKey.Split('_');
            if (parts.Length != 2) continue;
            
            if (!uint.TryParse(parts[0], out uint playerObjectId) || !uint.TryParse(parts[1], out uint petObjectId))
                continue;
                
            NetworkEntity player = GetNetworkEntityByObjectId(playerObjectId);
            NetworkEntity opponentPet = GetNetworkEntityByObjectId(petObjectId);
            
            if (player == null || opponentPet == null)
            {
                Debug.LogWarning($"[ENTITY_FACING] Lost entity references for fight {fightKey}, removing from tracking");
                readyFights.Add(fightKey);
                continue;
            }
            
            // Check if positions have changed (indicating NetworkTransform sync)
            bool playerPositionChanged = HasEntityPositionChanged(player);
            bool petPositionChanged = HasEntityPositionChanged(opponentPet);
            
            Debug.Log($"NETPOS: Tracking {player.EntityName.Value} (ID: {player.ObjectId}) at {player.transform.position}, IsOwner: {player.IsOwner}, Changed: {playerPositionChanged}");
            Debug.Log($"NETPOS: Tracking {opponentPet.EntityName.Value} (ID: {opponentPet.ObjectId}) at {opponentPet.transform.position}, IsOwner: {opponentPet.IsOwner}, Changed: {petPositionChanged}");
            
            if (playerPositionChanged || petPositionChanged)
            {
                Debug.Log($"NETPOS: Position change detected - Player: {playerPositionChanged}, Pet: {petPositionChanged}");
                Debug.Log($"[ENTITY_FACING] Position change detected - Player: {playerPositionChanged}, Pet: {petPositionChanged}");
            }
            
            // Check if both entities are now ready for facing
            if (AreEntitiesReadyForFacing(player, opponentPet))
            {
                Debug.Log($"NETPOS: Entities now ready for facing: {player.EntityName.Value} vs {opponentPet.EntityName.Value}");
                Debug.Log($"[ENTITY_FACING] Entities now ready after position tracking: {player.EntityName.Value} vs {opponentPet.EntityName.Value}");
                readyFights.Add(fightKey);
                kvp.Value.Invoke(); // Call the facing setup callback
            }
            else
            {
                Debug.Log($"NETPOS: Entities still not ready for facing: {player.EntityName.Value} vs {opponentPet.EntityName.Value}");
            }
        }
        
        // Remove completed fights from tracking
        foreach (string fightKey in readyFights)
        {
            pendingFacingCallbacks.Remove(fightKey);
            
            // Also remove entities from position tracking if no longer needed
            var parts = fightKey.Split('_');
            if (parts.Length == 2 && uint.TryParse(parts[0], out uint playerObjectId) && uint.TryParse(parts[1], out uint petObjectId))
            {
                NetworkEntity player = GetNetworkEntityByObjectId(playerObjectId);
                NetworkEntity opponentPet = GetNetworkEntityByObjectId(petObjectId);
                
                // Only remove if not used by other pending fights
                if (player != null && !IsEntityNeededForOtherFights(player, fightKey))
                {
                    lastTrackedPositions.Remove(player);
                    Debug.Log($"[ENTITY_FACING] Stopped tracking {player.EntityName.Value}");
                }
                    
                if (opponentPet != null && !IsEntityNeededForOtherFights(opponentPet, fightKey))
                {
                    lastTrackedPositions.Remove(opponentPet);
                    Debug.Log($"[ENTITY_FACING] Stopped tracking {opponentPet.EntityName.Value}");
                }
            }
        }
    }
    
    /// <summary>
    /// Checks if an entity's position has changed since last tracking update
    /// </summary>
    private bool HasEntityPositionChanged(NetworkEntity entity)
    {
        if (entity == null || !lastTrackedPositions.ContainsKey(entity)) 
        {
            Debug.Log($"NETPOS: HasEntityPositionChanged - {entity?.EntityName.Value ?? "null"} not in tracking dictionary");
            return false;
        }
        
        Vector3 currentPos = entity.transform.position;
        Vector3 lastPos = lastTrackedPositions[entity];
        
        float distance = Vector3.Distance(currentPos, lastPos);
        bool hasChanged = distance > 0.01f; // Small threshold for floating point precision
        
        Debug.Log($"NETPOS: HasEntityPositionChanged - {entity.EntityName.Value}: Last {lastPos}, Current {currentPos}, Distance {distance:F3}, Changed {hasChanged}");
        
        if (hasChanged)
        {
            Debug.Log($"NETPOS: Position change detected for {entity.EntityName.Value}: {lastPos} -> {currentPos} (distance: {distance:F3})");
            Debug.Log($"[ENTITY_FACING] Position change detected for {entity.EntityName.Value}: {lastPos} -> {currentPos} (distance: {distance:F3})");
            lastTrackedPositions[entity] = currentPos; // Update tracked position
        }
        
        return hasChanged;
    }
    
    /// <summary>
    /// Checks if an entity is needed for tracking other pending fights
    /// </summary>
    private bool IsEntityNeededForOtherFights(NetworkEntity entity, string excludeFightKey)
    {
        string entityIdStr = entity.ObjectId.ToString();
        
        foreach (string fightKey in pendingFacingCallbacks.Keys)
        {
            if (fightKey == excludeFightKey) continue;
            
            if (fightKey.Contains(entityIdStr)) return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Update method to handle event-driven position tracking
    /// </summary>
    private void Update()
    {
        // Only run position tracking if we have pending facing setups
        if (pendingFacingCallbacks.Count > 0)
        {
            CheckTrackedEntityPositions();
        }
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
            Debug.Log($"[ENTITY_FACING] AreEntitiesReadyForFacing: Entities not active - Player: {player.gameObject.activeInHierarchy}, OpponentPet: {opponentPet.gameObject.activeInHierarchy}");
            return false;
        }
        
        // Find ArenaManager if not available
        if (arenaManager == null)
        {
            arenaManager = FindFirstObjectByType<ArenaManager>();
        }
        
        // NEW: Check if entities are positioned in their expected arena locations
        if (arenaManager != null && arenaManager.ReferencePositionsCaptured)
        {
            uint playerObjectId = (uint)player.ObjectId;
            int arenaIndex = arenaManager.GetArenaForPlayer(playerObjectId);
            
            if (arenaIndex >= 0)
            {
                Vector3 expectedPlayerPos = arenaManager.GetPlayerPositionInArena(arenaIndex);
                Vector3 expectedOpponentPos = arenaManager.GetOpponentPetPositionInArena(arenaIndex);
                
                Vector3 actualPlayerPos = player.transform.position;
                Vector3 actualOpponentPos = opponentPet.transform.position;
                
                // Check if entities are close to their expected positions (tolerance for floating point precision)
                float playerDistanceFromExpected = Vector3.Distance(actualPlayerPos, expectedPlayerPos);
                float opponentDistanceFromExpected = Vector3.Distance(actualOpponentPos, expectedOpponentPos);
                
                const float positionTolerance = 0.5f; // Allow small differences for network sync
                
                bool playerInPosition = playerDistanceFromExpected < positionTolerance;
                bool opponentInPosition = opponentDistanceFromExpected < positionTolerance;
                
                if (!playerInPosition || !opponentInPosition)
                {
                    Debug.Log($"[ENTITY_FACING] AreEntitiesReadyForFacing: Entities not in expected arena positions");
                    Debug.Log($"[ENTITY_FACING] - Player {player.EntityName.Value}: Expected {expectedPlayerPos}, Actual {actualPlayerPos}, Distance {playerDistanceFromExpected:F2}");
                    Debug.Log($"[ENTITY_FACING] - Opponent {opponentPet.EntityName.Value}: Expected {expectedOpponentPos}, Actual {actualOpponentPos}, Distance {opponentDistanceFromExpected:F2}");
                    return false;
                }
                
                Debug.Log($"[ENTITY_FACING] AreEntitiesReadyForFacing: Entities properly positioned in arena {arenaIndex}");
                Debug.Log($"[ENTITY_FACING] - Player {player.EntityName.Value}: {actualPlayerPos} (expected {expectedPlayerPos})");
                Debug.Log($"[ENTITY_FACING] - Opponent {opponentPet.EntityName.Value}: {actualOpponentPos} (expected {expectedOpponentPos})");
                return true;
            }
        }
        
        // FALLBACK: Use distance check if arena system not available
        Vector3 playerPos = player.transform.position;
        Vector3 opponentPos = opponentPet.transform.position;
        
        // If both entities are at origin or very close to each other, they might not be positioned yet
        float distance = Vector3.Distance(playerPos, opponentPos);
        if (distance < 0.1f)
        {
            Debug.Log($"[ENTITY_FACING] AreEntitiesReadyForFacing: Entities too close together (distance: {distance:F3}), might not be positioned yet");
            return false;
        }
        
        Debug.Log($"[ENTITY_FACING] AreEntitiesReadyForFacing: Entities ready (fallback) - Player: {playerPos}, OpponentPet: {opponentPos}, Distance: {distance:F3}");
        return true;
    }

    /// <summary>
    /// Sets up facing between combat entities so they look at each other
    /// Only rotates entities owned by the local client to respect NetworkTransform ownership
    /// Only faces entities within the same arena
    /// </summary>
    private void SetupEntityFacing(NetworkEntity player, NetworkEntity opponentPet)
    {
        Debug.Log($"[ENTITY_FACING] SetupEntityFacing called for player {player?.EntityName.Value ?? "null"} vs opponent pet {opponentPet?.EntityName.Value ?? "null"}");
        
        if (player == null || opponentPet == null)
        {
            Debug.LogWarning("[ENTITY_FACING] Cannot setup entity facing - player or opponent pet is null");
            return;
        }

        // Get the ally pet for the player
        NetworkEntity allyPet = GetAllyPetForPlayer(player);
        
        Debug.Log($"[ENTITY_FACING] Found ally pet: {allyPet?.EntityName.Value ?? "null"}");

        // Make player face opponent pet (only if owned by local client and in same arena)
        if (player.IsOwner)
        {
            Debug.Log($"[ENTITY_FACING] Player {player.EntityName.Value} is owned by local client, setting facing");
            SetEntityFacing(player, opponentPet, "Player");
        }
        else
        {
            Debug.Log($"[ENTITY_FACING] Player {player.EntityName.Value} is NOT owned by local client, skipping facing");
        }

        // Make ally pet face opponent pet (only if owned by local client and in same arena)
        if (allyPet != null && allyPet.IsOwner)
        {
            // Check if ally pet and opponent pet are in the same arena
            if (AreEntitiesInSameArena(allyPet, opponentPet))
            {
                Debug.Log($"[ENTITY_FACING] Ally pet {allyPet.EntityName.Value} is owned by local client and in same arena as opponent, setting facing");
                SetEntityFacing(allyPet, opponentPet, "Ally Pet");
            }
            else
            {
                Debug.Log($"[ENTITY_FACING] Ally pet {allyPet.EntityName.Value} is NOT in same arena as opponent pet {opponentPet.EntityName.Value}, skipping cross-arena facing");
            }
        }
        else if (allyPet != null)
        {
            Debug.Log($"[ENTITY_FACING] Ally pet {allyPet.EntityName.Value} is NOT owned by local client, skipping facing");
        }

        // Make opponent pet face player (only if owned by local client)
        if (opponentPet.IsOwner)
        {
            Debug.Log($"[ENTITY_FACING] Opponent pet {opponentPet.EntityName.Value} is owned by local client, setting facing");
            SetEntityFacing(opponentPet, player, "Opponent Pet");
        }
        else
        {
            Debug.Log($"[ENTITY_FACING] Opponent pet {opponentPet.EntityName.Value} is NOT owned by local client, skipping facing");
        }
    }
    
    /// <summary>
    /// Checks if two entities are in the same arena
    /// </summary>
    private bool AreEntitiesInSameArena(NetworkEntity entity1, NetworkEntity entity2)
    {
        if (entity1 == null || entity2 == null || arenaManager == null)
            return false;
            
        // Find which arena each entity belongs to based on fight assignments
        if (fightManager == null) return false;
        
        var allFights = fightManager.GetAllFightAssignments();
        int entity1Arena = -1;
        int entity2Arena = -1;
        
        foreach (var fight in allFights)
        {
            uint entity1Id = (uint)entity1.ObjectId;
            uint entity2Id = (uint)entity2.ObjectId;
            
            // Check if entity1 is in this fight
            if (fight.LeftFighterObjectId == entity1Id || fight.RightFighterObjectId == entity1Id)
            {
                entity1Arena = arenaManager.GetArenaForPlayer(fight.LeftFighterObjectId);
            }
            
            // Check if entity2 is in this fight
            if (fight.LeftFighterObjectId == entity2Id || fight.RightFighterObjectId == entity2Id)
            {
                entity2Arena = arenaManager.GetArenaForPlayer(fight.LeftFighterObjectId);
            }
        }
        
        bool sameArena = entity1Arena >= 0 && entity1Arena == entity2Arena;
        Debug.Log($"[ENTITY_FACING] Arena check: {entity1.EntityName.Value} (arena {entity1Arena}) vs {entity2.EntityName.Value} (arena {entity2Arena}) = {(sameArena ? "SAME" : "DIFFERENT")}");
        
        return sameArena;
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
        Debug.Log($"[ENTITY_FACING] SetEntityFacing called: {entityDescription} {entity?.EntityName.Value ?? "null"} -> {target?.EntityName.Value ?? "null"}");
        
        if (entity == null || target == null)
        {
            Debug.LogWarning($"[ENTITY_FACING] Cannot set facing for {entityDescription} - entity or target is null");
            return;
        }

        // Calculate direction from entity to target
        Vector3 entityPos = entity.transform.position;
        Vector3 targetPos = target.transform.position;
        Vector3 directionToTarget = targetPos - entityPos;
        
        Debug.Log($"[ENTITY_FACING] {entityDescription} position: {entityPos}, target position: {targetPos}");
        Debug.Log($"[ENTITY_FACING] Direction vector: {directionToTarget}");
        
        // Remove Y component to only rotate around Y axis
        directionToTarget.y = 0;

        // Check if there's any horizontal distance
        if (directionToTarget.magnitude < 0.01f)
        {
            Debug.LogWarning($"[ENTITY_FACING] Entities {entity.EntityName.Value} and {target.EntityName.Value} are too close horizontally to determine facing direction (distance: {directionToTarget.magnitude})");
            return;
        }

        // Calculate the rotation needed to face the target
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

        // Store the old rotation for logging
        Vector3 oldEulerAngles = entity.transform.rotation.eulerAngles;

        // Apply the rotation
        entity.transform.rotation = targetRotation;

        Vector3 newEulerAngles = entity.transform.rotation.eulerAngles;
        Debug.Log($"[ENTITY_FACING] {entityDescription} {entity.EntityName.Value}: Rotated from Y={oldEulerAngles.y:F1}° to Y={newEulerAngles.y:F1}° to face {target.EntityName.Value}");
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
    /// Positions a hand entity in a specific arena
    /// </summary>
    private void PositionHandEntityInArena(NetworkEntity ownerEntity, int arenaIndex, Vector3 relativePosition, string handDescription)
    {
        if (ownerEntity == null || arenaManager == null)
        {
            Debug.LogError($"Cannot position {handDescription} in arena - missing requirements");
            return;
        }
        
        var relationshipManager = ownerEntity.GetComponent<RelationshipManager>();
        if (relationshipManager == null || relationshipManager.HandEntity == null)
        {
            Debug.LogError($"Cannot position {handDescription} - no hand entity found for {ownerEntity.EntityName.Value}");
            return;
        }
        
        var handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
        if (handEntity == null)
        {
            Debug.LogError($"Cannot position {handDescription} - hand entity missing NetworkEntity component");
            return;
        }
        
        // Calculate world position in arena
        Vector3 arenaWorldPosition = arenaManager.CalculateEntityPositionInArena(arenaIndex, relativePosition);
        
        // Hand entity should be a RectTransform for UI positioning
        RectTransform handRectTransform = handEntity.transform as RectTransform;
        if (handRectTransform == null)
        {
            Debug.LogError($"Hand entity {handEntity.EntityName.Value} does not have RectTransform!");
            return;
        }
        
        // Ensure proper parenting to combat canvas
        if (combatCanvas != null && handRectTransform.parent != combatCanvas.transform)
        {
            handRectTransform.SetParent(combatCanvas.transform, false);
        }
        
        // Convert arena world position to UI anchored position
        Canvas parentCanvas = combatCanvas.GetComponent<Canvas>();
        if (parentCanvas != null)
        {
            Vector3 canvasLocalPos = combatCanvas.transform.InverseTransformPoint(arenaWorldPosition);
            handRectTransform.anchoredPosition = new Vector2(canvasLocalPos.x, canvasLocalPos.y);
            
            // Set default anchoring
            handRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            handRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            handRectTransform.pivot = new Vector2(0.5f, 0.5f);
        }
        
        handRectTransform.SetAsLastSibling();
        
        Debug.Log($"[ARENA_UI_POSITIONING] {handDescription} {handEntity.EntityName.Value} positioned in arena {arenaIndex} at {handRectTransform.anchoredPosition}");
    }
    
    /// <summary>
    /// Positions a stats UI entity in a specific arena
    /// </summary>
    private void PositionStatsUIInArena(NetworkEntity ownerEntity, int arenaIndex, Vector3 relativePosition, string statsUIDescription)
    {
        if (ownerEntity == null || arenaManager == null)
        {
            Debug.LogError($"Cannot position {statsUIDescription} in arena - missing requirements");
            return;
        }
        
        var relationshipManager = ownerEntity.GetComponent<RelationshipManager>();
        if (relationshipManager == null || relationshipManager.StatsUIEntity == null)
        {
            Debug.LogError($"Cannot position {statsUIDescription} - no stats UI entity found for {ownerEntity.EntityName.Value}");
            return;
        }
        
        var statsUIEntity = relationshipManager.StatsUIEntity.GetComponent<NetworkEntity>();
        if (statsUIEntity == null)
        {
            Debug.LogError($"Cannot position {statsUIDescription} - stats UI entity missing NetworkEntity component");
            return;
        }
        
        // Calculate world position in arena
        Vector3 arenaWorldPosition = arenaManager.CalculateEntityPositionInArena(arenaIndex, relativePosition);
        
        // Stats UI entity should be a RectTransform for UI positioning
        RectTransform statsUIRectTransform = statsUIEntity.transform as RectTransform;
        if (statsUIRectTransform == null)
        {
            Debug.LogError($"Stats UI entity {statsUIEntity.EntityName.Value} does not have RectTransform!");
            return;
        }
        
        // Ensure proper parenting to combat canvas
        if (combatCanvas != null && statsUIRectTransform.parent != combatCanvas.transform)
        {
            statsUIRectTransform.SetParent(combatCanvas.transform, false);
        }
        
        // Convert arena world position to UI anchored position
        Canvas parentCanvas = combatCanvas.GetComponent<Canvas>();
        if (parentCanvas != null)
        {
            Vector3 canvasLocalPos = combatCanvas.transform.InverseTransformPoint(arenaWorldPosition);
            statsUIRectTransform.anchoredPosition = new Vector2(canvasLocalPos.x, canvasLocalPos.y);
            
            // Set default anchoring
            statsUIRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            statsUIRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            statsUIRectTransform.pivot = new Vector2(0.5f, 0.5f);
        }
        
        statsUIRectTransform.SetAsLastSibling();
        
        Debug.Log($"[ARENA_UI_POSITIONING] {statsUIDescription} {statsUIEntity.EntityName.Value} positioned in arena {arenaIndex} at {statsUIRectTransform.anchoredPosition}");
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
            NetworkEntity leftFighter = GetNetworkEntityByObjectId(fightAssignment.LeftFighterObjectId);
            NetworkEntity rightFighter = GetNetworkEntityByObjectId(fightAssignment.RightFighterObjectId);
            
            if (leftFighter == null || rightFighter == null)
            {
                Debug.LogWarning($"DEBUG: Could not find entities for fight - Left Fighter ID: {fightAssignment.LeftFighterObjectId}, Right Fighter ID: {fightAssignment.RightFighterObjectId}");
                continue;
            }
            
            /* Debug.Log($"DEBUG: Testing facing for fight - Left Fighter: {leftFighter.EntityName.Value}, Right Fighter: {rightFighter.EntityName.Value}"); */
            /* Debug.Log($"DEBUG: Left Fighter position: {leftFighter.transform.position}, rotation: {leftFighter.transform.rotation.eulerAngles}"); */
            /* Debug.Log($"DEBUG: Right Fighter position: {rightFighter.transform.position}, rotation: {rightFighter.transform.rotation.eulerAngles}"); */
            
            // Test the facing setup
            SetupEntityFacing(leftFighter, rightFighter);
            
            /* Debug.Log($"DEBUG: After facing setup - Left Fighter rotation: {leftFighter.transform.rotation.eulerAngles}, Right Fighter rotation: {rightFighter.transform.rotation.eulerAngles}"); */
        }
        
        /* Debug.Log("=== DEBUG: Entity facing test complete ==="); */
    }

    /// <summary>
    /// Notifies the loading screen that model positioning is complete
    /// </summary>
    private void NotifyLoadingScreenPositioningComplete()
    {
        if (loadingScreenManager == null)
        {
            loadingScreenManager = FindFirstObjectByType<LoadingScreenManager>();
        }
        
        if (loadingScreenManager != null)
        {
            loadingScreenManager.OnModelPositioningComplete();
            Debug.Log("CombatCanvasManager: Notified LoadingScreenManager that positioning is complete");
        }
        else
        {
            Debug.LogWarning("CombatCanvasManager: LoadingScreenManager not found, cannot notify positioning completion");
        }
    }

    /// <summary>
    /// Resets positioning completion state (for new combat rounds)
    /// </summary>
    public void ResetPositioningState()
    {
        isModelPositioningComplete = false;
        Debug.Log("CombatCanvasManager: Reset positioning state for new combat round");
    }

    /// <summary>
    /// Sets up the initial camera position for the local player's arena
    /// </summary>
    private void SetupInitialCameraPosition()
    {
        Debug.Log("[ARENA_CAMERA] CombatCanvasManager.SetupInitialCameraPosition() called");
        
        if (cameraManager == null)
        {
            cameraManager = FindFirstObjectByType<CameraManager>();
            if (cameraManager == null)
            {
                Debug.LogWarning("[ARENA_CAMERA] CombatCanvasManager: CameraManager not found, cannot setup initial camera position");
                return;
            }
        }
        
        if (arenaManager == null)
        {
            arenaManager = FindFirstObjectByType<ArenaManager>();
            if (arenaManager == null)
            {
                Debug.LogWarning("[ARENA_CAMERA] CombatCanvasManager: ArenaManager not found, cannot setup initial camera position");
                return;
            }
        }
        
        if (localPlayer == null)
        {
            Debug.LogWarning("[ARENA_CAMERA] CombatCanvasManager: Local player not found, cannot setup initial camera position");
            return;
        }
        
        Debug.Log($"[ARENA_CAMERA] CombatCanvasManager: Setting up initial camera for local player {localPlayer.EntityName.Value} (ID: {localPlayer.ObjectId})");
        
        // Use CameraManager's existing logic to move camera to the currently viewed fight
        // This will position the camera for the local player's arena
        cameraManager.MoveCameraToCurrentViewedFight(false); // false = not from spectating, so no transition
        
        Debug.Log("[ARENA_CAMERA] CombatCanvasManager: Initial camera positioning completed");
    }

    /// <summary>
    /// Sets up the dual fighter ally display system for both left and right fighters
    /// </summary>
    private void SetupFighterAllyDisplays()
    {
        Debug.Log("ALLY_SETUP: CombatCanvasManager.SetupFighterAllyDisplays() called");
        
        // Setup left ally display
        SetupLeftAllyDisplay();
        
        // Setup right ally display  
        SetupRightAllyDisplay();
        
        Debug.Log("ALLY_SETUP: CombatCanvasManager.SetupFighterAllyDisplays() completed");
    }
    
    /// <summary>
    /// Sets up the left ally display (traditionally the player's pet)
    /// </summary>
    private void SetupLeftAllyDisplay()
    {
        Debug.Log("ALLY_SETUP: SetupLeftAllyDisplay() called");
        Debug.Log($"ALLY_SETUP: leftAllyDisplayContainer: {(leftAllyDisplayContainer != null ? leftAllyDisplayContainer.name : "null")}");
        Debug.Log($"ALLY_SETUP: leftAllyDisplayContainer active: {(leftAllyDisplayContainer != null ? leftAllyDisplayContainer.gameObject.activeSelf : false)}");
        Debug.Log($"ALLY_SETUP: leftAllyDisplayContainer activeInHierarchy: {(leftAllyDisplayContainer != null ? leftAllyDisplayContainer.gameObject.activeInHierarchy : false)}");
        Debug.Log($"ALLY_SETUP: fighterAllyDisplayPrefab: {(fighterAllyDisplayPrefab != null ? fighterAllyDisplayPrefab.name : "null")}");
        
        if (leftAllyDisplayContainer == null)
        {
            Debug.LogWarning("ALLY_SETUP: Left ally display container not assigned, skipping left ally setup");
            return;
        }
        
        // If controller is not assigned, try to find or create it
        if (leftAllyDisplayController == null)
        {
            Debug.Log("ALLY_SETUP: leftAllyDisplayController is null, trying to find existing one...");
            leftAllyDisplayController = leftAllyDisplayContainer.GetComponentInChildren<FighterAllyDisplayController>();
            
            if (leftAllyDisplayController != null)
            {
                Debug.Log($"ALLY_SETUP: Found existing left ally display controller: {leftAllyDisplayController.name}");
            }
            else if (fighterAllyDisplayPrefab != null)
            {
                Debug.Log("ALLY_SETUP: Creating left ally display controller from prefab");
                GameObject leftDisplayObject = Instantiate(fighterAllyDisplayPrefab, leftAllyDisplayContainer);
                Debug.Log($"ALLY_SETUP: Instantiated prefab as: {leftDisplayObject.name}");
                
                leftAllyDisplayController = leftDisplayObject.GetComponent<FighterAllyDisplayController>();
                
                if (leftAllyDisplayController != null)
                {
                    Debug.Log("ALLY_SETUP: Found FighterAllyDisplayController component, configuring for left side");
                    // Configure for left side
                    leftAllyDisplayController.SetFighterSide(FighterSide.Left);
                    Debug.Log("ALLY_SETUP: Left side configuration applied");
                }
                else
                {
                    Debug.LogError("ALLY_SETUP: FighterAllyDisplayController component not found on instantiated prefab");
                }
            }
            else
            {
                Debug.LogError("ALLY_SETUP: fighterAllyDisplayPrefab is null, cannot create left ally display");
            }
        }
        else
        {
            Debug.Log($"ALLY_SETUP: leftAllyDisplayController already assigned: {leftAllyDisplayController.name}");
        }
        
        if (leftAllyDisplayController != null)
        {
            Debug.Log("ALLY_SETUP: Calling ForceUpdate() on left ally display controller");
            leftAllyDisplayController.ForceUpdate();
            Debug.Log("ALLY_SETUP: Left ally display controller setup completed");
        }
        else
        {
            Debug.LogWarning("ALLY_SETUP: Failed to setup left ally display controller");
        }
    }
    
    /// <summary>
    /// Sets up the right ally display (traditionally the opponent player)
    /// </summary>
    private void SetupRightAllyDisplay()
    {
        Debug.Log("ALLY_SETUP: SetupRightAllyDisplay() called");
        Debug.Log($"ALLY_SETUP: rightAllyDisplayContainer: {(rightAllyDisplayContainer != null ? rightAllyDisplayContainer.name : "null")}");
        Debug.Log($"ALLY_SETUP: rightAllyDisplayContainer active: {(rightAllyDisplayContainer != null ? rightAllyDisplayContainer.gameObject.activeSelf : false)}");
        Debug.Log($"ALLY_SETUP: rightAllyDisplayContainer activeInHierarchy: {(rightAllyDisplayContainer != null ? rightAllyDisplayContainer.gameObject.activeInHierarchy : false)}");
        
        if (rightAllyDisplayContainer == null)
        {
            Debug.LogWarning("ALLY_SETUP: Right ally display container not assigned, skipping right ally setup");
            return;
        }
        
        // If controller is not assigned, try to find or create it
        if (rightAllyDisplayController == null)
        {
            Debug.Log("ALLY_SETUP: rightAllyDisplayController is null, trying to find existing one...");
            rightAllyDisplayController = rightAllyDisplayContainer.GetComponentInChildren<FighterAllyDisplayController>();
            
            if (rightAllyDisplayController != null)
            {
                Debug.Log($"ALLY_SETUP: Found existing right ally display controller: {rightAllyDisplayController.name}");
            }
            else if (fighterAllyDisplayPrefab != null)
            {
                Debug.Log("ALLY_SETUP: Creating right ally display controller from prefab");
                GameObject rightDisplayObject = Instantiate(fighterAllyDisplayPrefab, rightAllyDisplayContainer);
                Debug.Log($"ALLY_SETUP: Instantiated prefab as: {rightDisplayObject.name}");
                
                rightAllyDisplayController = rightDisplayObject.GetComponent<FighterAllyDisplayController>();
                
                if (rightAllyDisplayController != null)
                {
                    Debug.Log("ALLY_SETUP: Found FighterAllyDisplayController component, configuring for right side");
                    // Configure for right side
                    rightAllyDisplayController.SetFighterSide(FighterSide.Right);
                    Debug.Log("ALLY_SETUP: Right side configuration applied");
                }
                else
                {
                    Debug.LogError("ALLY_SETUP: FighterAllyDisplayController component not found on instantiated prefab");
                }
            }
            else
            {
                Debug.LogError("ALLY_SETUP: fighterAllyDisplayPrefab is null, cannot create right ally display");
            }
        }
        else
        {
            Debug.Log($"ALLY_SETUP: rightAllyDisplayController already assigned: {rightAllyDisplayController.name}");
        }
        
        if (rightAllyDisplayController != null)
        {
            Debug.Log("ALLY_SETUP: Calling ForceUpdate() on right ally display controller");
            rightAllyDisplayController.ForceUpdate();
            Debug.Log("ALLY_SETUP: Right ally display controller setup completed");
        }
        else
        {
            Debug.LogWarning("ALLY_SETUP: Failed to setup right ally display controller");
        }
    }

    /// <summary>
    /// Gets the left ally display controller
    /// </summary>
    public FighterAllyDisplayController GetLeftAllyDisplayController()
    {
        return leftAllyDisplayController;
    }
    
    /// <summary>
    /// Gets the right ally display controller
    /// </summary>
    public FighterAllyDisplayController GetRightAllyDisplayController()
    {
        return rightAllyDisplayController;
    }
} 