using UnityEngine;
using FishNet.Object;

/// <summary>
/// Identifies the source and target of a card play.
/// Attach to: Card prefabs alongside the Card component.
/// </summary>
public class SourceAndTargetIdentifier : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Card card;
    [SerializeField] private FightManager fightManager;

    [Header("Debug Info (Read Only)")]
    [SerializeField, ReadOnly] private NetworkEntity sourceEntity;
    [SerializeField, ReadOnly] private NetworkEntity targetEntity;
    [SerializeField, ReadOnly] private string currentSourceName;
    [SerializeField, ReadOnly] private string currentTargetName;

    // Flag to track if we've already updated on this hover
    private bool hasUpdatedThisHover = false;

    public NetworkEntity SourceEntity => sourceEntity;
    public NetworkEntity TargetEntity => targetEntity;

    private void Awake()
    {
        // Setup required references
        SetupRequiredReferences();
    }

    private void SetupRequiredReferences()
    {
        // Get Card component from the same GameObject
        if (card == null)
        {
            card = GetComponent<Card>();
            if (card == null)
            {
                Debug.LogError($"SourceAndTargetIdentifier on {gameObject.name}: Missing Card component on the same GameObject!");
                // You could add it, but it's better if Card adds this component instead
                // since Card is the primary component and this is a helper
            }
        }

        // Find FightManager if not already assigned
        if (fightManager == null)
        {
            fightManager = FindFightManager();
            if (fightManager == null)
            {
                Debug.LogError($"SourceAndTargetIdentifier on {gameObject.name}: Could not find FightManager instance!");
            }
        }
    }

    private FightManager FindFightManager()
    {
        // Try to get reference from singleton instance if available
        FightManager fm = FightManager.Instance;
        
        // If not available via singleton, try to find in scene
        if (fm == null)
        {
            fm = FindFirstObjectByType<FightManager>();
            Debug.Log($"SourceAndTargetIdentifier on {gameObject.name}: Found FightManager via FindFirstObjectByType");
        }
        
        return fm;
    }

    public void OnMouseEnter()
    {
        if (!IsOwner) return;
        
        // Only update source and target if we haven't done it for this hover yet
        if (!hasUpdatedThisHover)
        {
            // Update source and target entities
            UpdateSourceAndTarget();
            
            // Set the flag to true so we don't update again until mouse exit
            hasUpdatedThisHover = true;
        }
    }
    
    public void OnMouseExit()
    {
        // Reset the flag when mouse exits so we can update again on the next hover
        hasUpdatedThisHover = false;
    }

    /// <summary>
    /// Updates the source and target entities based on the card's target type
    /// </summary>
    public void UpdateSourceAndTarget()
    {
        Debug.Log($"SourceAndTargetIdentifier: UpdateSourceAndTarget called for card {gameObject.name}");
        
        // Ensure we have everything we need
        if (card == null)
        {
            Debug.LogError($"SourceAndTargetIdentifier: UpdateSourceAndTarget failed - Missing Card component reference on {gameObject.name}");
            return;
        }
        
        if (fightManager == null)
        {
            fightManager = FindFightManager();
            if (fightManager == null)
            {
                Debug.LogError($"SourceAndTargetIdentifier: UpdateSourceAndTarget failed - Missing FightManager reference on {gameObject.name}");
                return;
            }
        }
        
        // Get the source entity (the player playing the card)
        sourceEntity = GetSourceEntity();
        if (sourceEntity == null)
        {
            Debug.LogWarning($"SourceAndTargetIdentifier: Failed to determine source entity for card {gameObject.name}");
        }
        else
        {
            Debug.Log($"SourceAndTargetIdentifier: Source entity for card {gameObject.name} is {sourceEntity.EntityName.Value}");
        }
        
        // Get the target based on the card's target type
        targetEntity = GetTargetEntity();
        if (targetEntity == null)
        {
            Debug.LogWarning($"SourceAndTargetIdentifier: Failed to determine target entity for card {gameObject.name}");
        }
        else
        {
            Debug.Log($"SourceAndTargetIdentifier: Target entity for card {gameObject.name} is {targetEntity.EntityName.Value}");
        }

        // Update debug info
        UpdateDebugInfo();
    }

    private void UpdateDebugInfo()
    {
        currentSourceName = sourceEntity != null ? sourceEntity.EntityName.Value : "None";
        currentTargetName = targetEntity != null ? targetEntity.EntityName.Value : "None";
        
        Debug.Log($"SourceAndTargetIdentifier: Debug info updated - Source: {currentSourceName}, Target: {currentTargetName}");
    }

    private NetworkEntity GetSourceEntity()
    {
        // The source is the owner of the card
        if (card != null && card.CurrentContainer == CardLocation.Hand)
        {
            NetworkEntity cardOwner = card.OwnerEntity;
            if (cardOwner == null) return null;
            
            // If the card is owned by a Hand entity, find the main entity that owns the hand
            if (cardOwner.EntityType == EntityType.PlayerHand || cardOwner.EntityType == EntityType.PetHand)
            {
                // Find the main entity (Player/Pet) that owns this hand
                NetworkEntity mainEntity = GetMainEntityForHand(cardOwner);
                if (mainEntity != null)
                {
                    Debug.Log($"SourceAndTargetIdentifier: Card {gameObject.name} owned by hand {cardOwner.EntityName.Value}, main entity is {mainEntity.EntityName.Value}");
                    return mainEntity;
                }
                else
                {
                    Debug.LogWarning($"SourceAndTargetIdentifier: Could not find main entity for hand {cardOwner.EntityName.Value}");
                    return null;
                }
            }
            
            // If the card is owned by a main entity (Player/Pet), return it directly
            if (cardOwner.EntityType == EntityType.Player || cardOwner.EntityType == EntityType.Pet)
            {
                return cardOwner;
            }
            
            Debug.LogWarning($"SourceAndTargetIdentifier: Unknown entity type {cardOwner.EntityType} for card owner {cardOwner.EntityName.Value}");
            return null;
        }
        return null;
    }
    
    /// <summary>
    /// Gets the main entity (Player/Pet) that owns a hand entity
    /// </summary>
    private NetworkEntity GetMainEntityForHand(NetworkEntity handEntity)
    {
        if (handEntity == null) return null;
        
        // Search through all spawned NetworkObjects to find the one that has this hand
        var spawnedObjects = IsServerInitialized ? 
            NetworkManager.ServerManager.Objects.Spawned.Values :
            NetworkManager.ClientManager.Objects.Spawned.Values;
            
        foreach (var networkObject in spawnedObjects)
        {
            if (networkObject == null) continue;
            
            var entity = networkObject.GetComponent<NetworkEntity>();
            if (entity != null && (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet))
            {
                var relationshipManager = entity.GetComponent<RelationshipManager>();
                if (relationshipManager != null && relationshipManager.HandEntity != null)
                {
                    var entityHand = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
                    if (entityHand != null && (uint)entityHand.ObjectId == (uint)handEntity.ObjectId)
                    {
                        // Found the main entity that owns this hand
                        return entity;
                    }
                }
            }
        }
        
        Debug.LogWarning($"GetMainEntityForHand: Could not find main entity for hand {handEntity.EntityName.Value} (ID: {handEntity.ObjectId})");
        return null;
    }

    private NetworkEntity GetTargetEntity()
    {
        if (card == null || card.CardData == null || sourceEntity == null || fightManager == null)
            return null;

        switch (card.CardData.TargetType)
        {
            case CardTargetType.Self:
                return sourceEntity;
                
            case CardTargetType.Opponent:
                return fightManager.GetOpponentForPlayer(sourceEntity);
                
            case CardTargetType.Ally:
                // TODO: Implement ally targeting
                return null;
                
            default:
                return null;
        }
    }

    /// <summary>
    /// Forces update of source and target entities for AI-controlled cards
    /// </summary>
    public void ForceUpdateSourceAndTarget(NetworkEntity source, NetworkEntity target)
    {
        Debug.Log($"SourceAndTargetIdentifier: ForceUpdateSourceAndTarget called for card {gameObject.name} with source: {source.EntityName.Value}, target: {target.EntityName.Value}");
        
        sourceEntity = source;
        targetEntity = target;
        
        // Update debug info
        UpdateDebugInfo();
    }
} 