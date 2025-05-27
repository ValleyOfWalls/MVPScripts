using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using TMPro;
using System.Linq;
using System.Collections;

/// <summary>
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
            Debug.Log($"Setting up combat UI for {localPlayer.EntityName.Value} vs {opponentPetForLocalPlayer.EntityName.Value}");
            
            // Initialize button listeners
            InitializeButtonListeners();
            
            // Setup own pet view
            SetupOwnPetView();
            
            // Position combat entities
            PositionCombatEntities(localPlayer, opponentPetForLocalPlayer);
            
            // Additional UI setup code here
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
    /// Positions combat entities in their designated UI positions
    /// </summary>
    public void PositionCombatEntities(NetworkEntity player, NetworkEntity opponentPet)
    {
        Debug.Log($"CombatCanvasManager: PositionCombatEntities called - Player: {player?.EntityName.Value}, OpponentPet: {opponentPet?.EntityName.Value}");
        
        if (player == null || opponentPet == null)
        {
            Debug.LogWarning("CombatCanvasManager: Cannot position entities - player or opponent pet is null");
            return;
        }

        // Position player
        if (playerPositionTransform != null)
        {
            Debug.Log($"CombatCanvasManager: Positioning player {player.EntityName.Value} from {player.transform.position} to {playerPositionTransform.position}");
            player.transform.position = playerPositionTransform.position;
            Debug.Log($"CombatCanvasManager: Player positioned at {player.transform.position}");
        }
        else
        {
            Debug.LogError("CombatCanvasManager: playerPositionTransform is null - cannot position player");
        }

        // Position opponent pet
        if (opponentPetPositionTransform != null)
        {
            Debug.Log($"CombatCanvasManager: Positioning opponent pet {opponentPet.EntityName.Value} from {opponentPet.transform.position} to {opponentPetPositionTransform.position}");
            opponentPet.transform.position = opponentPetPositionTransform.position;
            Debug.Log($"CombatCanvasManager: Opponent pet positioned at {opponentPet.transform.position}");
        }
        else
        {
            Debug.LogError("CombatCanvasManager: opponentPetPositionTransform is null - cannot position opponent pet");
        }

        // Position player hand
        var playerRelationship = player.GetComponent<RelationshipManager>();
        if (playerRelationship != null)
        {
            if (playerRelationship.HandEntity != null)
            {
                if (playerHandPositionTransform != null)
                {
                    var playerHand = playerRelationship.HandEntity.GetComponent<NetworkEntity>();
                    if (playerHand != null)
                    {
                        Debug.Log($"CombatCanvasManager: Positioning player hand from {playerHand.transform.position} to {playerHandPositionTransform.position}");
                        playerHand.transform.position = playerHandPositionTransform.position;
                        Debug.Log($"CombatCanvasManager: Player hand positioned at {playerHand.transform.position}");
                    }
                    else
                    {
                        Debug.LogError("CombatCanvasManager: Player hand entity found but missing NetworkEntity component");
                    }
                }
                else
                {
                    Debug.LogError("CombatCanvasManager: playerHandPositionTransform is null - cannot position player hand");
                }
            }
            else
            {
                Debug.LogWarning($"CombatCanvasManager: Player {player.EntityName.Value} has no hand entity - cannot position player hand");
            }
        }
        else
        {
            Debug.LogError($"CombatCanvasManager: Player {player.EntityName.Value} missing RelationshipManager - cannot find hand");
        }

        // Position opponent pet hand
        var petRelationship = opponentPet.GetComponent<RelationshipManager>();
        if (petRelationship != null)
        {
            if (petRelationship.HandEntity != null)
            {
                if (opponentPetHandPositionTransform != null)
                {
                    var petHand = petRelationship.HandEntity.GetComponent<NetworkEntity>();
                    if (petHand != null)
                    {
                        Debug.Log($"CombatCanvasManager: Positioning opponent pet hand from {petHand.transform.position} to {opponentPetHandPositionTransform.position}");
                        petHand.transform.position = opponentPetHandPositionTransform.position;
                        Debug.Log($"CombatCanvasManager: Opponent pet hand positioned at {petHand.transform.position}");
                    }
                    else
                    {
                        Debug.LogError("CombatCanvasManager: Opponent pet hand entity found but missing NetworkEntity component");
                    }
                }
                else
                {
                    Debug.LogError("CombatCanvasManager: opponentPetHandPositionTransform is null - cannot position opponent pet hand");
                }
            }
            else
            {
                Debug.LogWarning($"CombatCanvasManager: Opponent pet {opponentPet.EntityName.Value} has no hand entity - cannot position opponent pet hand");
            }
        }
        else
        {
            Debug.LogError($"CombatCanvasManager: Opponent pet {opponentPet.EntityName.Value} missing RelationshipManager - cannot find hand");
        }
        
        Debug.Log("CombatCanvasManager: PositionCombatEntities completed");
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