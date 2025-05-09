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
    
    // Track if we've already initialized
    private bool initialized = false;
    
    // On server start, find any missing components
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Try to find components if not set in inspector
        if (draftPackSetup == null) draftPackSetup = FindFirstObjectByType<DraftPackSetup>();
        if (cardShopManager == null) cardShopManager = FindFirstObjectByType<CardShopManager>();
        if (artifactShopManager == null) artifactShopManager = FindFirstObjectByType<ArtifactShopManager>();
    }
    
    /// <summary>
    /// Called by CombatManager when all combats are complete
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
        
        // Disable combat canvas and enable draft canvas
        RpcSwitchToDraftCanvas();
        
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