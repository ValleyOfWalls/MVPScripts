using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Object;

/// <summary>
/// Represents an artifact item in the game
/// </summary>
public class Artifact : MonoBehaviour
{
    [SerializeField] private ArtifactData artifactData;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Button selectionButton;

    // Reference to owning entity if equipped
    private NetworkBehaviour owningEntity;

    // Public accessors
    public ArtifactData ArtifactData => artifactData;
    public int Id => artifactData ? artifactData.Id : -1;
    
    /// <summary>
    /// Initializes the artifact with data
    /// </summary>
    public void Initialize(ArtifactData data)
    {
        artifactData = data;
        UpdateVisuals();
    }

    /// <summary>
    /// Updates the visual elements to match current artifact data
    /// </summary>
    private void UpdateVisuals()
    {
        if (artifactData == null) return;

        if (iconImage) iconImage.sprite = artifactData.Icon;
        if (nameText) nameText.text = artifactData.ArtifactName;
        if (descriptionText) descriptionText.text = artifactData.Description;
        if (costText) costText.text = artifactData.Cost.ToString();
    }

    /// <summary>
    /// Sets up the artifact for shop display
    /// </summary>
    public void SetupForShop(ArtifactSelectionManager selectionManager)
    {
        // Enable button and add listener for selection
        if (selectionButton)
        {
            selectionButton.onClick.AddListener(() => selectionManager.OnArtifactSelected(this));
        }
    }

    /// <summary>
    /// Sets up the artifact for inventory display
    /// </summary>
    public void SetupForInventory(NetworkBehaviour owner)
    {
        owningEntity = owner;
        
        // Disable selection functionality when in inventory
        if (selectionButton)
        {
            selectionButton.interactable = false;
        }
    }

    /// <summary>
    /// Apply the artifact's effect based on trigger type
    /// Called by ArtifactManager
    /// </summary>
    public void ApplyEffect(ArtifactTriggerType trigger)
    {
        if (artifactData == null || owningEntity == null) return;
        
        // Only apply if trigger matches
        if (artifactData.TriggerType != trigger && artifactData.TriggerType != ArtifactTriggerType.Passive) return;

        // Process effect based on type
        switch (artifactData.EffectType)
        {
            case ArtifactEffectType.MaxHealthBoost:
                if (owningEntity is NetworkEntity entity)
                {
                    entity.IncreaseMaxHealth(artifactData.EffectMagnitude);
                }
                break;
                
            case ArtifactEffectType.MaxEnergyBoost:
                if (owningEntity is NetworkEntity entityEnergy)
                {
                    entityEnergy.IncreaseMaxEnergy(artifactData.EffectMagnitude);
                }
                break;
                
            // Additional cases would be implemented for other effect types
            
            default:
                Debug.Log($"Effect {artifactData.EffectType} not implemented yet for {artifactData.ArtifactName}");
                break;
        }
    }
} 