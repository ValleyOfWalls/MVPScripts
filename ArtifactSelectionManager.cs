using UnityEngine;

/// <summary>
/// Handles selecting artifacts in the draft phase
/// </summary>
public class ArtifactSelectionManager : MonoBehaviour
{
    // Reference to the artifact shop manager
    private ArtifactShopManager shopManager;
    
    // Reference to the artifact being managed
    private Artifact managedArtifact;

    private void Awake()
    {
        managedArtifact = GetComponent<Artifact>();
        
        if (managedArtifact == null)
        {
            Debug.LogError("ArtifactSelectionManager requires an Artifact component on the same GameObject.");
        }
    }

    /// <summary>
    /// Sets the shop manager reference
    /// </summary>
    /// <param name="manager">The shop manager to use</param>
    public void SetShopManager(ArtifactShopManager manager)
    {
        shopManager = manager;
    }

    /// <summary>
    /// Called when this artifact is selected in the shop
    /// </summary>
    /// <param name="artifact">The artifact that was selected</param>
    public void OnArtifactSelected(Artifact artifact)
    {
        if (shopManager == null)
        {
            Debug.LogError("ArtifactSelectionManager: No shop manager assigned.");
            return;
        }
        
        // Attempt to purchase the artifact through the shop manager
        shopManager.AttemptPurchaseArtifact(artifact);
    }
} 