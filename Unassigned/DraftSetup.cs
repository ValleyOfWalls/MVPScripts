using UnityEngine;
using FishNet.Object;

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
    
    /// <summary>
    /// Called by CombatManager when all combats are complete
    /// Transitions from combat to draft phase
    /// </summary>
    [Server]
    public void InitializeDraft()
    {
        if (!IsServerInitialized) return;
        
        // Disable combat canvas and enable draft canvas
        RpcSwitchToDraftCanvas();
        
        // Setup draft packs
        if (draftPackSetup != null)
        {
            draftPackSetup.SetupDraftPacks();
        }
        else
        {
            Debug.LogError("DraftSetup: No DraftPackSetup component assigned.");
        }
        
        // Setup card shop
        if (cardShopManager != null)
        {
            cardShopManager.SetupShop();
        }
        else
        {
            Debug.LogError("DraftSetup: No CardShopManager component assigned.");
        }
        
        // Setup artifact shop
        if (artifactShopManager != null)
        {
            artifactShopManager.SetupShop();
        }
        else
        {
            Debug.LogError("DraftSetup: No ArtifactShopManager component assigned.");
        }
    }

    /// <summary>
    /// Switches from combat canvas to draft canvas on all clients
    /// </summary>
    [ObserversRpc]
    private void RpcSwitchToDraftCanvas()
    {
        // Disable combat canvas
        if (combatCanvas != null)
        {
            combatCanvas.SetActive(false);
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
            }
        }
    }
} 