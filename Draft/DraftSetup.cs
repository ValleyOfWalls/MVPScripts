using UnityEngine;
using FishNet.Object;
using System.Collections;

/// <summary>
/// Coordinates the transition from combat to draft phase and initializes the draft system.
/// Attach to: A NetworkObject in the scene that manages the draft setup process.
/// </summary>
public class DraftSetup : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private CombatCanvasManager combatCanvasManager;
    [SerializeField] private DraftCanvasManager draftCanvasManager;
    [SerializeField] private DraftPackSetup draftPackSetup;
    [SerializeField] private DraftManager draftManager;
    [SerializeField] private GamePhaseManager gamePhaseManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private EntityVisibilityManager entityVisibilityManager;
    
    [Header("Draft Canvas")]
    [SerializeField] private GameObject draftCanvas;
    
    private bool isSetupComplete = false;
    
    public bool IsSetupComplete => isSetupComplete;
    
    private void Awake()
    {
        ResolveReferences();
        RegisterDraftCanvasWithPhaseManager();
    }
    
    private void ResolveReferences()
    {
        if (combatCanvasManager == null) combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
        if (draftCanvasManager == null) draftCanvasManager = FindFirstObjectByType<DraftCanvasManager>();
        if (draftPackSetup == null) draftPackSetup = FindFirstObjectByType<DraftPackSetup>();
        if (draftManager == null) draftManager = FindFirstObjectByType<DraftManager>();
        if (gamePhaseManager == null) gamePhaseManager = GamePhaseManager.Instance;
        if (gameManager == null) gameManager = GameManager.Instance;
        if (entityVisibilityManager == null) entityVisibilityManager = FindFirstObjectByType<EntityVisibilityManager>();
        
        // Log what we found for debugging
        Debug.Log($"DraftSetup.ResolveReferences: DraftCanvasManager = {(draftCanvasManager != null ? "Found" : "NULL")}");
        Debug.Log($"DraftSetup.ResolveReferences: DraftCanvas GameObject = {(draftCanvas != null ? "Found" : "NULL")}");
    }
    
    /// <summary>
    /// Public method to re-resolve references if needed
    /// </summary>
    public void RefreshReferences()
    {
        ResolveReferences();
        RegisterDraftCanvasWithPhaseManager();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Re-resolve references when client starts to ensure we have all components
        ResolveReferences();
        Debug.Log("DraftSetup: Client started, references resolved");
    }
    
    private void RegisterDraftCanvasWithPhaseManager()
    {
        if (gamePhaseManager != null && draftCanvas != null)
        {
            gamePhaseManager.SetDraftCanvas(draftCanvas);
        }
    }
    
    /// <summary>
    /// Initiates the draft phase setup
    /// </summary>
    [Server]
    public void InitializeDraft()
    {
        if (!IsServerInitialized)
        {
            Debug.LogError("DraftSetup: Cannot initialize draft - server not initialized");
            return;
        }
        
        if (isSetupComplete)
        {
            Debug.LogWarning("DraftSetup: Draft setup already completed");
            return;
        }
        
        Debug.Log("DraftSetup: Starting draft phase initialization...");
        
        StartCoroutine(SetupDraftPhase());
    }
    
    /// <summary>
    /// Coroutine to handle the draft setup process
    /// </summary>
    [Server]
    private IEnumerator SetupDraftPhase()
    {
        // Step 1: Hide all network entities for draft transition
        Debug.Log("DraftSetup: Step 1 - Hiding all network entities for draft transition");
        HideAllNetworkEntities();
        
        // Step 2: Disable combat canvas
        Debug.Log("DraftSetup: Step 2 - Disabling combat canvas");
        DisableCombatCanvas();
        
        // Small delay to ensure combat canvas is properly disabled
        yield return new WaitForSeconds(0.1f);
        
        // Step 3: Update game phase to Draft
        Debug.Log("DraftSetup: Step 3 - Updating game phase to Draft");
        UpdateGamePhase();
        
        // Step 4: Enable draft canvas
        Debug.Log("DraftSetup: Step 4 - Enabling draft canvas");
        EnableDraftCanvas();
        
        // Small delay to ensure UI is ready
        yield return new WaitForSeconds(0.2f);
        
        // Step 5: Create draft packs
        Debug.Log("DraftSetup: Step 5 - Creating draft packs");
        CreateDraftPacks();
        
        // Step 6: Assign draft packs to players
        Debug.Log("DraftSetup: Step 6 - Assigning draft packs to players");
        AssignDraftPacks();
        
        // Step 7: Start the draft manager
        Debug.Log("DraftSetup: Step 7 - Starting draft manager");
        StartDraftManager();
        
        isSetupComplete = true;
        Debug.Log("DraftSetup: Draft phase initialization complete!");
    }
    
    /// <summary>
    /// Disables the combat canvas
    /// </summary>
    [Server]
    private void DisableCombatCanvas()
    {
        // Notify clients to disable combat canvas via CombatCanvasManager
        RpcDisableCombatCanvas();
    }
    
    /// <summary>
    /// Hides all network entities during draft transition
    /// </summary>
    [Server]
    private void HideAllNetworkEntities()
    {
        if (entityVisibilityManager != null)
        {
            // Set the game state to Draft which will hide all entities
            entityVisibilityManager.SetGameState(EntityVisibilityManager.GameState.Draft);
            Debug.Log("DraftSetup: All network entities hidden via EntityVisibilityManager");
        }
        else
        {
            Debug.LogError("DraftSetup: EntityVisibilityManager not found, cannot hide network entities");
        }
        
        // Also notify clients to hide entities
        RpcHideAllNetworkEntities();
    }
    
    /// <summary>
    /// Updates the game phase to Draft
    /// </summary>
    [Server]
    private void UpdateGamePhase()
    {
        if (gamePhaseManager != null)
        {
            gamePhaseManager.SetDraftPhase();
            Debug.Log("DraftSetup: Game phase updated to Draft on server");
            
            // Also notify clients to update their game phase
            RpcUpdateGamePhase();
        }
        else
        {
            Debug.LogError("DraftSetup: GamePhaseManager not found, cannot update game phase");
        }
    }
    
    /// <summary>
    /// Enables the draft canvas
    /// </summary>
    [Server]
    private void EnableDraftCanvas()
    {
        // Notify clients to enable draft canvas
        RpcEnableDraftCanvas();
    }
    
    /// <summary>
    /// Creates draft packs using DraftPackSetup
    /// </summary>
    [Server]
    private void CreateDraftPacks()
    {
        if (draftPackSetup != null)
        {
            draftPackSetup.CreateDraftPacks();
            Debug.Log("DraftSetup: Draft packs created successfully");
        }
        else
        {
            Debug.LogError("DraftSetup: DraftPackSetup not found, cannot create draft packs");
        }
    }
    
    /// <summary>
    /// Assigns draft packs to players (handled by DraftPackSetup)
    /// </summary>
    [Server]
    private void AssignDraftPacks()
    {
        // Pack assignment is handled automatically by DraftPackSetup.CreateDraftPacks()
        // Each pack is assigned to its original owner during creation
        Debug.Log("DraftSetup: Draft pack assignment complete");
    }
    
    /// <summary>
    /// Starts the draft manager to begin the draft flow
    /// </summary>
    [Server]
    private void StartDraftManager()
    {
        if (draftManager != null)
        {
            draftManager.StartDraft();
            Debug.Log("DraftSetup: Draft manager started successfully");
            
            // Trigger initial draft pack visibility update on all clients
            RpcUpdateInitialDraftPackVisibility();
        }
        else
        {
            Debug.LogError("DraftSetup: DraftManager not found, cannot start draft");
        }
    }
    
    /// <summary>
    /// Resets the setup state for a new draft
    /// </summary>
    [Server]
    public void ResetSetup()
    {
        Debug.Log("DraftSetup: Resetting setup state for new draft round");
        
        // Reset the setup completion flag
        isSetupComplete = false;
        
        // Clear existing draft packs from previous round
        if (draftPackSetup != null)
        {
            Debug.Log("DraftSetup: Clearing existing draft packs from previous round");
            // Note: We can't call ClearExistingPacks directly as it's private, but CreateDraftPacks will handle this
            Debug.Log("DraftSetup: DraftPackSetup found, existing packs will be cleared when new packs are created");
        }
        else
        {
            Debug.LogWarning("DraftSetup: DraftPackSetup not found during reset");
        }
        
        // Reset the draft manager if it exists
        if (draftManager != null)
        {
            // The DraftManager doesn't have a public reset method, but we can ensure it's in a clean state
            // by checking if it's still active and logging the state
            Debug.Log($"DraftSetup: DraftManager found, current draft active state: {draftManager.IsDraftActive}");
        }
        else
        {
            Debug.LogWarning("DraftSetup: DraftManager not found during reset");
        }
        
        Debug.Log("DraftSetup: Setup state reset completed");
    }
    
    // RPC Methods
    [ObserversRpc]
    private void RpcDisableCombatCanvas()
    {
        if (combatCanvasManager != null)
        {
            combatCanvasManager.DisableCombatCanvas();
            Debug.Log("DraftSetup: Combat canvas disabled via CombatCanvasManager on client");
        }
        else
        {
            Debug.LogWarning("DraftSetup: CombatCanvasManager not found on client");
        }
    }
    
    [ObserversRpc]
    private void RpcEnableDraftCanvas()
    {
        Debug.Log("DraftSetup: RpcEnableDraftCanvas called");
        
        // First, directly enable the draft canvas GameObject (like CombatSetup does)
        if (draftCanvas != null)
        {
            Debug.Log("DraftSetup: Enabling draft canvas GameObject directly");
            draftCanvas.SetActive(true);
        }
        else
        {
            Debug.LogError("DraftSetup: Draft canvas GameObject reference is missing");
        }
        
        // Use coroutine for additional setup with proper timing
        StartCoroutine(SetupDraftUIWithDelay());
    }
    
    private IEnumerator SetupDraftUIWithDelay()
    {
        yield return null; // Wait one frame
        Debug.Log("DraftSetup: Setting up draft UI after delay");
        
        // Ensure canvas is enabled before setting up UI
        if (draftCanvas != null && !draftCanvas.activeSelf)
        {
            Debug.Log("DraftSetup: Re-enabling draft canvas before UI setup");
            draftCanvas.SetActive(true);
        }
        
        // Try to use DraftCanvasManager for additional setup
        if (draftCanvasManager == null)
        {
            draftCanvasManager = FindFirstObjectByType<DraftCanvasManager>();
            Debug.Log($"DraftSetup: Attempting to find DraftCanvasManager on client - Found: {draftCanvasManager != null}");
        }
        
        if (draftCanvasManager != null)
        {
            draftCanvasManager.EnableDraftCanvas();
            Debug.Log("DraftSetup: Draft canvas enabled via DraftCanvasManager on client");
        }
        else
        {
            Debug.LogWarning("DraftSetup: DraftCanvasManager not found on client, but canvas should be enabled directly");
        }
        
        Debug.Log("DraftSetup: Draft UI setup completed");
    }
    
    [ObserversRpc]
    private void RpcHideAllNetworkEntities()
    {
        Debug.Log("DraftSetup: RpcHideAllNetworkEntities called on client");
        
        if (entityVisibilityManager != null)
        {
            // Set the game state to Draft on the client as well
            entityVisibilityManager.SetGameState(EntityVisibilityManager.GameState.Draft);
            Debug.Log("DraftSetup: Game state set to Draft and entities hidden on client");
        }
        else
        {
            Debug.LogWarning("DraftSetup: EntityVisibilityManager not found on client");
        }
    }
    
    [ObserversRpc]
    private void RpcUpdateInitialDraftPackVisibility()
    {
        Debug.Log("DraftSetup: RpcUpdateInitialDraftPackVisibility called on client");
        
        // Use a coroutine with a small delay to ensure draft packs are fully initialized
        StartCoroutine(UpdateDraftPackVisibilityWithDelay());
    }
    
    private System.Collections.IEnumerator UpdateDraftPackVisibilityWithDelay()
    {
        // Wait a frame to ensure all draft pack setup is complete
        yield return null;
        
        // Wait a small additional delay
        yield return new WaitForSeconds(0.1f);
        
        Debug.Log("DraftSetup: Updating draft pack visibility after delay");
        
        if (entityVisibilityManager != null)
        {
            Debug.Log("DraftSetup: EntityVisibilityManager found, calling UpdateDraftPackVisibility");
            entityVisibilityManager.UpdateDraftPackVisibility();
            Debug.Log("DraftSetup: Initial draft pack visibility updated on client");
        }
        else
        {
            Debug.LogWarning("DraftSetup: EntityVisibilityManager not found on client, cannot update initial draft pack visibility");
        }
    }
    
    [ObserversRpc]
    private void RpcUpdateGamePhase()
    {
        Debug.Log("DraftSetup: RpcUpdateGamePhase called on client");
        
        if (gamePhaseManager != null)
        {
            gamePhaseManager.SetDraftPhase();
            Debug.Log("DraftSetup: Game phase updated to Draft on client");
        }
        else
        {
            Debug.LogWarning("DraftSetup: GamePhaseManager not found on client");
        }
    }
} 