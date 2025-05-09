using UnityEngine;
using FishNet.Object;
using System.Collections;

/// <summary>
/// Initializes the draft phase after combat
/// </summary>
public class DraftSetup : NetworkBehaviour
{
    [Header("Canvas References")]
    [SerializeField] private GameObject combatCanvas;
    [SerializeField] private GameObject draftCanvas;
    
    [Header("Draft Components")]
    [SerializeField] private DraftPackSetup draftPackSetup;
    [SerializeField] private CardShopManager cardShopManager;
    [SerializeField] private ArtifactShopManager artifactShopManager;
    
    [Header("Game Management")]
    [SerializeField] private GamePhaseManager gamePhaseManager;
    
    // Track if we've already initialized
    private bool initialized = false;

    private void Awake()
    {
        // Register draft canvas with GamePhaseManager if available
        if (gamePhaseManager == null)
        {
            gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
        }
        
        if (gamePhaseManager != null && draftCanvas != null)
        {
            gamePhaseManager.SetDraftCanvas(draftCanvas);
        }
    }
    
    private void Start()
    {
        // Subscribe to GamePhaseManager phase change events
        if (gamePhaseManager != null)
        {
            gamePhaseManager.OnPhaseChanged += OnGamePhaseChanged;
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from phase change events
        if (gamePhaseManager != null)
        {
            gamePhaseManager.OnPhaseChanged -= OnGamePhaseChanged;
        }
    }
    
    // On server start, find any missing components
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Try to find components if not set in inspector
        if (draftPackSetup == null) draftPackSetup = FindFirstObjectByType<DraftPackSetup>();
        if (cardShopManager == null) cardShopManager = FindFirstObjectByType<CardShopManager>();
        if (artifactShopManager == null) artifactShopManager = FindFirstObjectByType<ArtifactShopManager>();
        if (gamePhaseManager == null) gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
        
        if (gamePhaseManager == null)
        {
            Debug.LogError("DraftSetup: GamePhaseManager not found, phase transitions will not work correctly.");
        }
        else
        {
            // Register canvas and subscribe to phase events
            if (draftCanvas != null)
            {
                gamePhaseManager.SetDraftCanvas(draftCanvas);
            }
            gamePhaseManager.OnPhaseChanged += OnGamePhaseChanged;
        }
    }
    
    /// <summary>
    /// Called when the GamePhaseManager changes phases
    /// </summary>
    private void OnGamePhaseChanged(GamePhaseManager.GamePhase newPhase)
    {
        if (newPhase == GamePhaseManager.GamePhase.Draft)
        {
            if (IsServerInitialized && !initialized)
            {
                Debug.Log("DraftSetup: Detected phase change to Draft. Setting up draft components...");
                InitializeDraftComponents();
            }
        }
    }
    
    /// <summary>
    /// Called by CombatManager when all combats are complete (legacy method kept for backwards compatibility)
    /// Transitions from combat to draft phase
    /// </summary>
    [Server]
    public void InitializeDraft()
    {
        if (!IsServerInitialized) return;
        
        if (initialized)
        {
            Debug.LogWarning("DraftSetup: Draft phase already initialized. Ignoring duplicate call.");
            return;
        }
        
        Debug.Log("DraftSetup: Initializing draft phase...");
        
        // Use GamePhaseManager to transition to Draft phase
        if (gamePhaseManager != null)
        {
            gamePhaseManager.SetDraftPhase();
            // The OnGamePhaseChanged event will trigger InitializeDraftComponents
        }
        else
        {
            // Fallback to direct canvas switching if GamePhaseManager not available
            RpcSwitchToDraftCanvas();
            Debug.LogWarning("DraftSetup: GamePhaseManager not available, using direct canvas switching");
            
            // Initialize draft components directly since we won't get the phase change event
            InitializeDraftComponents();
        }
    }
    
    /// <summary>
    /// Initializes draft components (separated from phase transition logic)
    /// </summary>
    [Server]
    private void InitializeDraftComponents()
    {
        if (!IsServerInitialized || initialized) return;
        
        // Setup draft packs
        if (draftPackSetup != null)
        {
            StartCoroutine(SetupDraftComponentsWithDelay());
        }
        else
        {
            Debug.LogError("DraftSetup: No DraftPackSetup component assigned.");
        }
        
        initialized = true;
    }
    
    /// <summary>
    /// Sets up draft components with a small delay to ensure UI is ready
    /// </summary>
    [Server]
    private IEnumerator SetupDraftComponentsWithDelay()
    {
        // Short delay to allow UI transition to complete
        yield return new WaitForSeconds(0.5f);
        
        // Setup draft packs
        if (draftPackSetup != null)
        {
            draftPackSetup.SetupDraftPacks();
            Debug.Log("DraftSetup: Draft packs initialized.");
        }
        
        // Setup card shop
        if (cardShopManager != null)
        {
            cardShopManager.SetupShop();
            Debug.Log("DraftSetup: Card shop initialized.");
        }
        
        // Setup artifact shop
        if (artifactShopManager != null)
        {
            artifactShopManager.SetupShop();
            Debug.Log("DraftSetup: Artifact shop initialized.");
        }
    }

    /// <summary>
    /// Switches from combat canvas to draft canvas on all clients
    /// This is a fallback method if GamePhaseManager is not available
    /// </summary>
    [ObserversRpc]
    private void RpcSwitchToDraftCanvas()
    {
        Debug.Log("DraftSetup: Switching from combat to draft UI...");
        
        // Disable combat canvas
        if (combatCanvas != null)
        {
            combatCanvas.SetActive(false);
        }
        else
        {
            Debug.LogWarning("DraftSetup: Combat canvas reference is missing.");
        }
        
        // Enable draft canvas
        if (draftCanvas != null)
        {
            draftCanvas.SetActive(true);
            
            // Setup the draft UI
            DraftCanvasManager draftCanvasManager = draftCanvas.GetComponent<DraftCanvasManager>();
            if (draftCanvasManager != null)
            {
                draftCanvasManager.SetupDraftUI();
                Debug.Log("DraftSetup: Draft UI initialized.");
            }
            else
            {
                Debug.LogError("DraftSetup: DraftCanvasManager component not found on draft canvas.");
            }
        }
        else
        {
            Debug.LogError("DraftSetup: Draft canvas reference is missing.");
        }
    }
    
    /// <summary>
    /// Resets initialization flag for a new game
    /// </summary>
    [Server]
    public new void Reset()
    {
        if (!IsServerInitialized) return;
        initialized = false;
        Debug.Log("DraftSetup: Reset for new game.");
    }
} 