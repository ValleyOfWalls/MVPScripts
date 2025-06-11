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
        // Find managers if not already assigned
        if (fightManager == null) fightManager = FindFirstObjectByType<FightManager>();
        if (combatManager == null) combatManager = FindFirstObjectByType<CombatManager>();

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
            
            // Position entities for this specific fight
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
        Debug.Log($"[POSITIONING_DEBUG] PositionHandEntityAlways called for {handDescription}");
        
        if (ownerEntity == null || targetPosition == null)
        {
            if (ownerEntity == null)
                Debug.LogError($"[POSITIONING_DEBUG] Owner entity for {handDescription} is null");
            if (targetPosition == null)
                Debug.LogError($"[POSITIONING_DEBUG] Target position for {handDescription} is null");
            return;
        }

        Debug.Log($"[POSITIONING_DEBUG] Owner entity: {ownerEntity.EntityName.Value}");
        Debug.Log($"[POSITIONING_DEBUG] Target position: {targetPosition.name}");

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

        Debug.Log($"[POSITIONING_DEBUG] Hand entity found: {handEntity.EntityName.Value}");

        // Check combat canvas availability
        if (combatCanvas == null)
        {
            Debug.LogError($"[POSITIONING_DEBUG] Combat canvas is NULL! Cannot reparent {handDescription}");
            return;
        }

        Debug.Log($"[POSITIONING_DEBUG] Combat canvas available: {combatCanvas.name}");

        // Hand entity should be a RectTransform, target position can be regular Transform (position marker)
        RectTransform handRectTransform = handEntity.transform as RectTransform;
        
        if (handRectTransform == null)
        {
            Debug.LogError($"[POSITIONING_DEBUG] Hand entity {handEntity.EntityName.Value} does not have RectTransform! Hand entities must be UI elements.");
            return;
        }

        Debug.Log($"[POSITIONING_DEBUG] Hand entity current parent: {(handRectTransform.parent != null ? handRectTransform.parent.name : "NULL/ROOT")}");
        Debug.Log($"[POSITIONING_DEBUG] Combat canvas transform: {combatCanvas.transform.name}");
        
        // Ensure the hand entity is a child of the main combat canvas if it isn't already
        if (handRectTransform.parent != combatCanvas.transform)
        {
            Debug.Log($"[UI_HIERARCHY] {handDescription} {handEntity.EntityName.Value}: Moving to be child of combat canvas");
            Debug.Log($"[UI_HIERARCHY] Before reparenting - Parent: {(handRectTransform.parent != null ? handRectTransform.parent.name : "NULL")}");
            
            handRectTransform.SetParent(combatCanvas.transform, false);
            
            Debug.Log($"[UI_HIERARCHY] After reparenting - Parent: {(handRectTransform.parent != null ? handRectTransform.parent.name : "NULL")}");
        }
        else
        {
            Debug.Log($"[UI_HIERARCHY] {handDescription} {handEntity.EntityName.Value}: Already child of combat canvas");
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
            
            Debug.Log($"[UI_POSITIONING] {handDescription} {handEntity.EntityName.Value}: Copied RectTransform properties from target");
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
            
            Debug.Log($"[UI_POSITIONING] {handDescription} {handEntity.EntityName.Value}: Converted Transform position {targetPosition.position} to anchored position {handRectTransform.anchoredPosition}");
        }
        
        // Ensure proper layer ordering
        handRectTransform.SetAsLastSibling();
        
        Debug.Log($"[UI_POSITIONING] {handDescription} {handEntity.EntityName.Value}: Positioned from {oldAnchoredPosition} to {handRectTransform.anchoredPosition}");
    }

    /// <summary>
    /// Debug method to manually trigger positioning for testing
    /// </summary>
    [ContextMenu("Debug Manual Position Entities")]
    public void DebugManualPositionEntities()
    {
        Debug.Log("=== DEBUG: Manual positioning trigger ===");
        
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
        Debug.Log("=== DEBUG: Starting hand entity parenting check ===");
        
        // Check if combat canvas is available
        if (combatCanvas == null)
        {
            Debug.LogError("DEBUG: combatCanvas is NULL! Cannot reparent hand entities.");
            return;
        }
        
        Debug.Log($"DEBUG: Combat canvas found: {combatCanvas.name}");
        
        // Find all hand entities in the scene
        NetworkEntity[] allEntities = FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        
        foreach (var entity in allEntities)
        {
            if (entity.EntityType == EntityType.PlayerHand || entity.EntityType == EntityType.PetHand)
            {
                Debug.Log($"DEBUG: Found hand entity: {entity.EntityName.Value} (Type: {entity.EntityType})");
                Debug.Log($"DEBUG: Current parent: {(entity.transform.parent != null ? entity.transform.parent.name : "NULL/ROOT")}");
                Debug.Log($"DEBUG: Current position: {entity.transform.position}");
                
                RectTransform handRectTransform = entity.transform as RectTransform;
                if (handRectTransform == null)
                {
                    Debug.LogError($"DEBUG: Hand entity {entity.EntityName.Value} does not have RectTransform!");
                    continue;
                }
                
                // Force reparent to combat canvas
                if (handRectTransform.parent != combatCanvas.transform)
                {
                    Debug.Log($"DEBUG: Reparenting {entity.EntityName.Value} to combat canvas");
                    handRectTransform.SetParent(combatCanvas.transform, false);
                    Debug.Log($"DEBUG: New parent: {handRectTransform.parent.name}");
                }
                else
                {
                    Debug.Log($"DEBUG: {entity.EntityName.Value} is already a child of combat canvas");
                }
            }
        }
        
        Debug.Log("=== DEBUG: Hand entity parenting check complete ===");
    }
} 