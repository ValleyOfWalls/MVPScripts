using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using TMPro;
using System.Linq;
using System.Collections;

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

    private NetworkEntity localPlayer;
    private NetworkEntity opponentPetForLocalPlayer;

    private FightManager fightManager;
    private CombatManager combatManager;

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
                _ => "Waiting..."
            };
            turnIndicatorText.text = turnText;
        }

        // Enable/disable end turn button based on whose turn it is
        if (endTurnButton != null)
        {
            bool isLocalPlayerTurn = turn == CombatTurn.PlayerTurn && localPlayer != null && localPlayer.IsOwner;
            endTurnButton.interactable = isLocalPlayerTurn;
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
        Debug.Log("[COMBAT_UI_SETUP] SetupCombatUI called");
        
        // Find managers if not already assigned
        if (fightManager == null) fightManager = FindFirstObjectByType<FightManager>();
        if (combatManager == null) combatManager = FindFirstObjectByType<CombatManager>();

        if (fightManager == null) Debug.LogError("[COMBAT_UI_SETUP] FightManager not found by CombatCanvasManager.");
        if (combatManager == null) Debug.LogError("[COMBAT_UI_SETUP] CombatManager not found by CombatCanvasManager.");

        Debug.Log("[COMBAT_UI_SETUP] Attempting to find local player");
        // Try multiple approaches to find the local player
        FindLocalPlayer();

        if (localPlayer == null)
        {
            Debug.LogError("[COMBAT_UI_SETUP] Local player not found");
            LogLocalPlayerError();
            return;
        }

        Debug.Log($"[COMBAT_UI_SETUP] Local player found: {localPlayer.EntityName.Value} (ObjectId: {localPlayer.ObjectId}, IsOwner: {localPlayer.IsOwner})");

        // Find opponent
        Debug.Log("[COMBAT_UI_SETUP] Finding opponent for local player");
        opponentPetForLocalPlayer = fightManager.GetOpponentForPlayer(localPlayer);

        if (opponentPetForLocalPlayer == null)
        {
            Debug.LogWarning("[COMBAT_UI_SETUP] Opponent not found immediately, will retry");
            // Try to wait and retry if opponent isn't available yet
            StartCoroutine(RetryFindOpponent());
        }
        else
        {
            Debug.Log($"[COMBAT_UI_SETUP] Opponent found: {opponentPetForLocalPlayer.EntityName.Value} (ObjectId: {opponentPetForLocalPlayer.ObjectId}, IsOwner: {opponentPetForLocalPlayer.IsOwner})");
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
        Debug.Log("[COMBAT_UI_SETUP] CompleteUISetup called");
        
        // Set up UI elements based on the local player and their opponent
        if (localPlayer != null && opponentPetForLocalPlayer != null)
        {
            Debug.Log($"[COMBAT_UI_SETUP] Setting up combat UI for {localPlayer.EntityName.Value} vs {opponentPetForLocalPlayer.EntityName.Value}");
            
            // Initialize button listeners
            Debug.Log("[COMBAT_UI_SETUP] Initializing button listeners");
            InitializeButtonListeners();
            
            // Setup own pet view
            Debug.Log("[COMBAT_UI_SETUP] Setting up own pet view");
            SetupOwnPetView();
            
            // Setup deck viewer
            Debug.Log("[COMBAT_UI_SETUP] Setting up deck viewer");
            SetupDeckViewer();
            
            // Position combat entities for all fights
            Debug.Log("[COMBAT_UI_SETUP] About to position combat entities for all fights");
            PositionCombatEntitiesForAllFights();
            Debug.Log("[COMBAT_UI_SETUP] Combat entity positioning completed for all fights");
            
            Debug.Log("[COMBAT_UI_SETUP] CompleteUISetup finished successfully");
            // Additional UI setup code here
        }
        else
        {
            Debug.LogError($"[COMBAT_UI_SETUP] Cannot complete UI setup - localPlayer: {(localPlayer != null ? "found" : "null")}, opponentPet: {(opponentPetForLocalPlayer != null ? "found" : "null")}");
        }
    }

    private void InitializeButtonListeners()
    {
        Debug.Log("CombatCanvasManager: Initializing button listeners");
        SetupEndTurnButton();
    }

    /// <summary>
    /// Sets up the end turn button to correctly end the local player's turn.
    /// </summary>
    private void SetupEndTurnButton()
    {
        if (endTurnButton != null)
        {
            Debug.Log("CombatCanvasManager: Setting up end turn button");
            endTurnButton.onClick.RemoveAllListeners();
            endTurnButton.onClick.AddListener(() => {
                Debug.Log("CombatCanvasManager: End turn button clicked");
                if (combatManager != null && localPlayer != null)
                {
                    Debug.Log($"CombatCanvasManager: Sending end turn request for player {localPlayer.EntityName.Value}");
                    // Call the server method to end the turn
                    combatManager.OnEndTurnButtonPressed(localPlayer.Owner);
                }
                else
                {
                    Debug.LogError($"Cannot end turn: Missing {(combatManager == null ? "CombatManager" : "local player")} reference");
                }
            });
            
            Debug.Log("CombatCanvasManager: End turn button setup complete");
        }
        else
        {
            Debug.LogError("End Turn Button not assigned in CombatCanvasManager");
        }
    }

    /// <summary>
    /// Sets up the own pet view functionality
    /// </summary>
    private void SetupOwnPetView()
    {
        Debug.Log("CombatCanvasManager: SetupOwnPetView() called");
        
        // Validate container
        if (ownPetViewContainer == null)
        {
            Debug.LogWarning("CombatCanvasManager: OwnPetViewContainer not assigned. Own pet view will not be available.");
            return;
        }
        
        Debug.Log($"CombatCanvasManager: Container found: {ownPetViewContainer.name}, active: {ownPetViewContainer.gameObject.activeInHierarchy}");
        
        // Find existing OwnPetViewController if not assigned
        if (ownPetViewController == null)
        {
            ownPetViewController = ownPetViewContainer.GetComponentInChildren<OwnPetViewController>();
            if (ownPetViewController != null)
            {
                Debug.Log($"CombatCanvasManager: Found existing OwnPetViewController: {ownPetViewController.name}, active: {ownPetViewController.gameObject.activeInHierarchy}");
            }
        }
        
        // If still not found, try to spawn the prefab (server only)
        if (ownPetViewController == null)
        {
            if (ownPetViewPrefab != null)
            {
                Debug.Log($"CombatCanvasManager: Prefab assigned: {ownPetViewPrefab.name}");
                
                // Check if the prefab has a NetworkObject component
                NetworkObject prefabNetworkObject = ownPetViewPrefab.GetComponent<NetworkObject>();
                if (prefabNetworkObject != null)
                {
                    Debug.Log("CombatCanvasManager: Prefab has NetworkObject component");
                    
                    // Only spawn on server, clients will receive it automatically
                    var networkManager = FishNet.InstanceFinder.NetworkManager;
                    if (networkManager != null && networkManager.IsServerStarted)
                    {
                        Debug.Log("CombatCanvasManager: Server spawning OwnPetView NetworkObject");
                        
                        // Spawn at root first to avoid NetworkObject parenting issues
                        GameObject spawnedObject = Instantiate(ownPetViewPrefab);
                        Debug.Log($"CombatCanvasManager: Instantiated object: {spawnedObject.name}, active: {spawnedObject.activeInHierarchy}");
                        
                        NetworkObject spawnedNetworkObject = spawnedObject.GetComponent<NetworkObject>();
                        
                        if (spawnedNetworkObject != null)
                        {
                            Debug.Log("CombatCanvasManager: About to spawn NetworkObject");
                            
                            // Spawn the NetworkObject first
                            networkManager.ServerManager.Spawn(spawnedNetworkObject);
                            Debug.Log($"CombatCanvasManager: NetworkObject spawned, active: {spawnedObject.activeInHierarchy}");
                            
                            // Then move to correct parent after spawning
                            spawnedObject.transform.SetParent(ownPetViewContainer, false);
                            Debug.Log($"CombatCanvasManager: Set parent, active: {spawnedObject.activeInHierarchy}");
                            
                            // Ensure it's active
                            spawnedObject.SetActive(true);
                            Debug.Log($"CombatCanvasManager: Explicitly set active: {spawnedObject.activeInHierarchy}");
                            
                            ownPetViewController = spawnedObject.GetComponent<OwnPetViewController>();
                            
                            // Use RPC to notify clients about the correct parent
                            if (spawnedNetworkObject.IsServerInitialized)
                            {
                                Debug.Log("CombatCanvasManager: Sending RPC to clients");
                                SetOwnPetViewParentRpc(spawnedNetworkObject.ObjectId);
                            }
                            
                            // Check if it's still active after a frame
                            StartCoroutine(CheckActiveStatusAfterFrame(spawnedObject));
                        }
                        else
                        {
                            Debug.LogError("CombatCanvasManager: Failed to get NetworkObject component from spawned prefab!");
                            Destroy(spawnedObject);
                            return;
                        }
                    }
                    else if (networkManager != null && networkManager.IsClientStarted)
                    {
                        Debug.Log("CombatCanvasManager: Client - looking for spawned NetworkObject");
                        // On client, look for spawned NetworkObject and move it to correct parent
                        // Note: The RPC might handle this, but we have a backup coroutine
                        StartCoroutine(FindAndParentSpawnedOwnPetView());
                        return;
                    }
                }
                else
                {
                    // Fallback to regular instantiation if not a NetworkObject
                    Debug.LogWarning("CombatCanvasManager: OwnPetViewPrefab is not a NetworkObject, using regular instantiation");
                    GameObject instantiatedPrefab = Instantiate(ownPetViewPrefab, ownPetViewContainer);
                    Debug.Log($"CombatCanvasManager: Regular instantiation complete, active: {instantiatedPrefab.activeInHierarchy}");
                    ownPetViewController = instantiatedPrefab.GetComponent<OwnPetViewController>();
                }
                
                if (ownPetViewController == null)
                {
                    Debug.LogError("CombatCanvasManager: Spawned/instantiated prefab does not contain OwnPetViewController component!");
                    return;
                }
            }
            else
            {
                Debug.LogWarning("CombatCanvasManager: OwnPetViewPrefab not assigned. Cannot spawn own pet view.");
                return;
            }
        }
        
        // Final fallback - search entire scene
        if (ownPetViewController == null)
        {
            Debug.Log("CombatCanvasManager: Searching entire scene for OwnPetViewController");
            ownPetViewController = FindFirstObjectByType<OwnPetViewController>();
            if (ownPetViewController != null)
            {
                Debug.Log($"CombatCanvasManager: Found OwnPetViewController in scene: {ownPetViewController.name}, active: {ownPetViewController.gameObject.activeInHierarchy}");
            }
        }
        
        if (ownPetViewController != null)
        {
            Debug.Log($"CombatCanvasManager: Final check - OwnPetViewController active: {ownPetViewController.gameObject.activeInHierarchy}");
            
            // Refresh the displayed pet to show the currently viewed player's pet
            ownPetViewController.RefreshDisplayedPet();
            Debug.Log("CombatCanvasManager: Own pet view setup complete");
        }
        else
        {
            Debug.LogWarning("CombatCanvasManager: OwnPetViewController not found. Own pet view will not be available.");
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
            Debug.Log("CombatCanvasManager: Updated own pet view for new viewed combat");
        }
        
        // Update deck viewer button states for the new viewed combat
        if (deckViewerManager != null)
        {
            deckViewerManager.UpdateButtonStates();
            Debug.Log("CombatCanvasManager: Updated deck viewer button states for new viewed combat");
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
            Debug.Log("CombatCanvasManager: End turn button disabled - fight is over");
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
            Debug.Log("CombatCanvasManager: End turn button enabled");
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
            Debug.Log("CombatCanvasManager: Combat canvas GameObject disabled");
        }
        else
        {
            Debug.LogError("CombatCanvasManager: Combat canvas reference is not assigned");
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
            Debug.Log("CombatCanvasManager: Combat canvas GameObject enabled");
            
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
        else
        {
            Debug.LogError("CombatCanvasManager: Combat canvas reference is not assigned");
        }
    }
    
    /// <summary>
    /// Called when the local player's fight ends to update UI accordingly
    /// </summary>
    public void OnLocalFightEnded()
    {
        DisableEndTurnButton();
        Debug.Log("CombatCanvasManager: Local fight ended - UI updated");
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
        Debug.Log("CombatCanvasManager: SetupDeckViewer() called");
        
        // Find DeckViewerManager if not assigned
        if (deckViewerManager == null)
        {
            deckViewerManager = FindFirstObjectByType<DeckViewerManager>();
            if (deckViewerManager != null)
            {
                Debug.Log($"CombatCanvasManager: Found DeckViewerManager: {deckViewerManager.name}");
            }
        }
        
        if (deckViewerManager != null)
        {
            // Update button states for the initial combat setup
            deckViewerManager.UpdateButtonStates();
            Debug.Log("CombatCanvasManager: Deck viewer setup complete");
        }
        else
        {
            Debug.LogWarning("CombatCanvasManager: DeckViewerManager not found. Deck viewing will not be available.");
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
    /// Positions combat entities for all ongoing fights
    /// Only positions entities owned by the local client to respect NetworkTransform ownership
    /// </summary>
    public void PositionCombatEntitiesForAllFights()
    {
        Debug.Log("[COMBAT_POSITIONING] PositionCombatEntitiesForAllFights called");
        
        if (fightManager == null)
        {
            Debug.LogError("[COMBAT_POSITIONING] FightManager is null, cannot position entities for all fights");
            return;
        }
        
        // Get all fight assignments from the FightManager
        var allFights = fightManager.GetAllFightAssignments();
        Debug.Log($"[COMBAT_POSITIONING] Found {allFights.Count} total fights to process");
        
        foreach (var fightAssignment in allFights)
        {
            // Get the actual NetworkEntity objects from the IDs
            NetworkEntity player = GetNetworkEntityByObjectId(fightAssignment.PlayerObjectId);
            NetworkEntity opponentPet = GetNetworkEntityByObjectId(fightAssignment.PetObjectId);
            
            if (player == null || opponentPet == null)
            {
                Debug.LogWarning($"[COMBAT_POSITIONING] Cannot find entities for fight: Player ID {fightAssignment.PlayerObjectId}, Pet ID {fightAssignment.PetObjectId}");
                continue;
            }
            
            Debug.Log($"[COMBAT_POSITIONING] Processing fight: {player.EntityName.Value} vs {opponentPet.EntityName.Value}");
            
            // Position entities for this specific fight
            PositionCombatEntitiesForSpecificFight(player, opponentPet);
        }
        
        Debug.Log("[COMBAT_POSITIONING] PositionCombatEntitiesForAllFights completed");
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
        Debug.Log($"[COMBAT_POSITIONING] PositionCombatEntitiesForSpecificFight called - Player: {player?.EntityName.Value} (IsOwner: {player?.IsOwner}), OpponentPet: {opponentPet?.EntityName.Value} (IsOwner: {opponentPet?.IsOwner})");
        
        if (player == null || opponentPet == null)
        {
            Debug.LogWarning("[COMBAT_POSITIONING] Cannot position entities - player or opponent pet is null");
            return;
        }

        // Position player only if owned by local client (has NetworkTransform)
        PositionEntityIfOwned(player, playerPositionTransform, "Player");

        // Position opponent pet only if owned by local client (has NetworkTransform)
        PositionEntityIfOwned(opponentPet, opponentPetPositionTransform, "Opponent Pet");

        // Position ALL hands locally since they don't have NetworkTransforms and are always in the same positions
        PositionHandEntityAlways(player, playerHandPositionTransform, "Player Hand");
        PositionHandEntityAlways(opponentPet, opponentPetHandPositionTransform, "Opponent Pet Hand");
        
        Debug.Log($"[COMBAT_POSITIONING] PositionCombatEntitiesForSpecificFight completed for {player.EntityName.Value} vs {opponentPet.EntityName.Value}");
    }

    /// <summary>
    /// Positions combat entities in their designated UI positions (legacy method for current viewed fight)
    /// Only positions entities owned by the local client to respect NetworkTransform ownership
    /// </summary>
    public void PositionCombatEntities(NetworkEntity player, NetworkEntity opponentPet)
    {
        Debug.Log($"[COMBAT_POSITIONING] PositionCombatEntities (legacy) called - delegating to specific fight method");
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
                Debug.LogError($"[COMBAT_POSITIONING] {entityDescription} entity is null");
            if (targetPosition == null)
                Debug.LogError($"[COMBAT_POSITIONING] {entityDescription} target position transform is null");
            return;
        }

        Debug.Log($"[COMBAT_POSITIONING] Checking {entityDescription}: {entity.EntityName.Value} (ObjectId: {entity.ObjectId}, IsOwner: {entity.IsOwner}, Current Position: {entity.transform.position})");

        if (entity.IsOwner)
        {
            Vector3 oldPosition = entity.transform.position;
            entity.transform.position = targetPosition.position;
            Debug.Log($"[COMBAT_POSITIONING] POSITIONED {entityDescription} {entity.EntityName.Value} from {oldPosition} to {entity.transform.position} (Target: {targetPosition.position})");
        }
        else
        {
            Debug.Log($"[COMBAT_POSITIONING] SKIPPING {entityDescription} {entity.EntityName.Value} - not owned by local client. Current position: {entity.transform.position}");
        }
    }

    /// <summary>
    /// Positions a hand entity regardless of ownership since hands don't have NetworkTransforms
    /// and are always in the same positions locally
    /// </summary>
    private void PositionHandEntityAlways(NetworkEntity ownerEntity, Transform targetPosition, string handDescription)
    {
        if (ownerEntity == null || targetPosition == null)
        {
            if (ownerEntity == null)
                Debug.LogError($"[COMBAT_POSITIONING] Owner entity for {handDescription} is null");
            if (targetPosition == null)
                Debug.LogError($"[COMBAT_POSITIONING] {handDescription} target position transform is null");
            return;
        }

        var relationshipManager = ownerEntity.GetComponent<RelationshipManager>();
        if (relationshipManager == null)
        {
            Debug.LogError($"[COMBAT_POSITIONING] {ownerEntity.EntityName.Value} missing RelationshipManager - cannot find {handDescription}");
            return;
        }

        if (relationshipManager.HandEntity == null)
        {
            Debug.LogWarning($"[COMBAT_POSITIONING] {ownerEntity.EntityName.Value} has no hand entity - cannot position {handDescription}");
            return;
        }

        var handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
        if (handEntity == null)
        {
            Debug.LogError($"[COMBAT_POSITIONING] {handDescription} entity found but missing NetworkEntity component");
            return;
        }

        Debug.Log($"[COMBAT_POSITIONING] Positioning {handDescription}: {handEntity.EntityName.Value} (ObjectId: {handEntity.ObjectId}, IsOwner: {handEntity.IsOwner}, Owner Entity: {ownerEntity.EntityName.Value}, Current Position: {handEntity.transform.position})");

        // Always position the hand locally since it doesn't have NetworkTransform
        Vector3 oldPosition = handEntity.transform.position;
        handEntity.transform.position = targetPosition.position;
        Debug.Log($"[COMBAT_POSITIONING] POSITIONED {handDescription} {handEntity.EntityName.Value} from {oldPosition} to {handEntity.transform.position} (Target: {targetPosition.position}) - ALWAYS POSITIONED LOCALLY (no NetworkTransform)");
    }

    /// <summary>
    /// Positions a hand entity only if the owning player/pet is owned by the local client (legacy method)
    /// </summary>
    private void PositionHandEntityIfPlayerOwned(NetworkEntity ownerEntity, Transform targetPosition, string handDescription)
    {
        if (ownerEntity == null || targetPosition == null)
        {
            if (ownerEntity == null)
                Debug.LogError($"[COMBAT_POSITIONING] Owner entity for {handDescription} is null");
            if (targetPosition == null)
                Debug.LogError($"[COMBAT_POSITIONING] {handDescription} target position transform is null");
            return;
        }

        var relationshipManager = ownerEntity.GetComponent<RelationshipManager>();
        if (relationshipManager == null)
        {
            Debug.LogError($"[COMBAT_POSITIONING] {ownerEntity.EntityName.Value} missing RelationshipManager - cannot find {handDescription}");
            return;
        }

        if (relationshipManager.HandEntity == null)
        {
            Debug.LogWarning($"[COMBAT_POSITIONING] {ownerEntity.EntityName.Value} has no hand entity - cannot position {handDescription}");
            return;
        }

        var handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
        if (handEntity == null)
        {
            Debug.LogError($"[COMBAT_POSITIONING] {handDescription} entity found but missing NetworkEntity component");
            return;
        }

        Debug.Log($"[COMBAT_POSITIONING] Checking {handDescription}: {handEntity.EntityName.Value} (ObjectId: {handEntity.ObjectId}, IsOwner: {handEntity.IsOwner}, Owner Entity IsOwner: {ownerEntity.IsOwner}, Current Position: {handEntity.transform.position})");

        // Position the hand if the owner entity is owned by the local client
        // This ensures that each client positions their own player's hand and their own pet's hand
        if (ownerEntity.IsOwner)
        {
            Vector3 oldPosition = handEntity.transform.position;
            handEntity.transform.position = targetPosition.position;
            Debug.Log($"[COMBAT_POSITIONING] POSITIONED {handDescription} {handEntity.EntityName.Value} from {oldPosition} to {handEntity.transform.position} (Target: {targetPosition.position}) - Owner {ownerEntity.EntityName.Value} is local");
        }
        else
        {
            Debug.Log($"[COMBAT_POSITIONING] SKIPPING {handDescription} {handEntity.EntityName.Value} - owner {ownerEntity.EntityName.Value} not owned by local client. Current position: {handEntity.transform.position}");
        }
    }

    private IEnumerator FindAndParentSpawnedOwnPetView()
    {
        // Wait for a few frames to allow the NetworkObject to spawn
        int maxRetries = 10;
        int retryCount = 0;
        
        while (retryCount < maxRetries)
        {
            yield return new WaitForSeconds(0.1f);
            
            // First check if the RPC has already handled this
            if (ownPetViewController != null)
            {
                Debug.Log("CombatCanvasManager: RPC has already handled OwnPetView setup, stopping coroutine");
                yield break;
            }
            
            // Look for NetworkObjects with OwnPetViewController component
            OwnPetViewController[] controllers = FindObjectsByType<OwnPetViewController>(FindObjectsSortMode.None);
            
            foreach (var controller in controllers)
            {
                NetworkObject networkObject = controller.GetComponent<NetworkObject>();
                if (networkObject != null)
                {
                    // Check if it's already parented correctly
                    if (controller.transform.IsChildOf(ownPetViewContainer))
                    {
                        Debug.Log("CombatCanvasManager: Found already parented OwnPetView (likely via RPC)");
                        ownPetViewController = controller;
                        ownPetViewController.RefreshDisplayedPet();
                        yield break;
                    }
                    // Check if it's unparented and needs to be moved
                    else if (networkObject.transform.parent == null)
                    {
                        // Found an unparented NetworkObject with OwnPetViewController
                        networkObject.transform.SetParent(ownPetViewContainer, false);
                        networkObject.gameObject.SetActive(true);
                        
                        ownPetViewController = controller;
                        ownPetViewController.RefreshDisplayedPet();
                        
                        Debug.Log("CombatCanvasManager: Found and parented spawned OwnPetView on client");
                        yield break;
                    }
                }
            }
            
            retryCount++;
        }
        
        Debug.LogWarning("CombatCanvasManager: Failed to find spawned OwnPetView NetworkObject on client after retries");
    }

    [ObserversRpc]
    private void SetOwnPetViewParentRpc(int objectId)
    {
        // Find the NetworkObject by ID and set its parent
        var networkManager = FishNet.InstanceFinder.NetworkManager;
        if (networkManager != null && networkManager.IsClientStarted)
        {
            if (networkManager.ClientManager.Objects.Spawned.TryGetValue(objectId, out NetworkObject networkObject))
            {
                if (networkObject != null && ownPetViewContainer != null)
                {
                    networkObject.transform.SetParent(ownPetViewContainer, false);
                    networkObject.gameObject.SetActive(true);
                    
                    // Update our reference
                    ownPetViewController = networkObject.GetComponent<OwnPetViewController>();
                    if (ownPetViewController != null)
                    {
                        Debug.Log($"CombatCanvasManager: RPC found OwnPetViewController, refreshing display");
                        ownPetViewController.RefreshDisplayedPet();
                    }
                    else
                    {
                        Debug.LogError("CombatCanvasManager: RPC could not find OwnPetViewController component");
                    }
                    
                    Debug.Log("CombatCanvasManager: Set OwnPetView parent via RPC");
                }
                else
                {
                    Debug.LogError($"CombatCanvasManager: RPC failed - networkObject: {networkObject != null}, container: {ownPetViewContainer != null}");
                }
            }
            else
            {
                Debug.LogError($"CombatCanvasManager: RPC could not find NetworkObject with ID {objectId}");
            }
        }
        else
        {
            Debug.LogError("CombatCanvasManager: RPC called but NetworkManager not available or not client");
        }
    }

    private IEnumerator CheckActiveStatusAfterFrame(GameObject spawnedObject)
    {
        yield return null; // Wait one frame
        Debug.Log($"CombatCanvasManager: After one frame - spawned object active: {spawnedObject.activeInHierarchy}");
        
        yield return new WaitForSeconds(0.5f); // Wait half a second
        Debug.Log($"CombatCanvasManager: After 0.5 seconds - spawned object active: {spawnedObject.activeInHierarchy}");
        
        yield return new WaitForSeconds(1.0f); // Wait another second
        Debug.Log($"CombatCanvasManager: After 1.5 seconds total - spawned object active: {spawnedObject.activeInHierarchy}");
    }
} 