using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;

/// <summary>
/// Identifies the source and target of a card play with enhanced targeting support.
/// Attach to: Card prefabs alongside the Card component.
/// </summary>
public class SourceAndTargetIdentifier : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Card card;
    [SerializeField] private FightManager fightManager;
    [SerializeField] private DamageCalculator damageCalculator;

    [Header("Debug Info (Read Only)")]
    [SerializeField, ReadOnly] private NetworkEntity sourceEntity;
    [SerializeField, ReadOnly] private NetworkEntity targetEntity;
    [SerializeField, ReadOnly] private List<NetworkEntity> allTargets = new List<NetworkEntity>();
    [SerializeField, ReadOnly] private string currentSourceName;
    [SerializeField, ReadOnly] private string currentTargetName;
    [SerializeField, ReadOnly] private string allTargetNames;

    // Flag to track if we've already updated on this hover
    private bool hasUpdatedThisHover = false;

    public NetworkEntity SourceEntity => sourceEntity;
    public NetworkEntity TargetEntity => targetEntity;
    public List<NetworkEntity> AllTargets => new List<NetworkEntity>(allTargets);

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

        // Find DamageCalculator if not already assigned
        if (damageCalculator == null)
        {
            damageCalculator = FindDamageCalculator();
            if (damageCalculator == null)
            {
                Debug.LogError($"SourceAndTargetIdentifier on {gameObject.name}: Could not find DamageCalculator instance!");
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

    private DamageCalculator FindDamageCalculator()
    {
        // Try to find DamageCalculator in scene (usually on GameManager)
        DamageCalculator dc = FindFirstObjectByType<DamageCalculator>();
        if (dc != null)
        {
            Debug.Log($"SourceAndTargetIdentifier on {gameObject.name}: Found DamageCalculator via FindFirstObjectByType");
        }
        return dc;
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

        // Show damage previews
        ShowDamagePreviews();
    }
    
    public void OnMouseDown()
    {
        if (!IsOwner) return;
        
        // Always update source and target on mouse down to ensure they're current when the card is played
        // This is critical because Card.OnMouseDown() needs valid source/target entities
        UpdateSourceAndTarget();
        
        // Reset the hover flag since we've just updated
        hasUpdatedThisHover = true;
    }
    
    public void OnMouseExit()
    {
        // Reset the flag when mouse exits so we can update again on the next hover
        hasUpdatedThisHover = false;

        // Hide damage previews
        HideDamagePreviews();
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
        
        // Get targets based on the card's target type
        allTargets.Clear();
        GetTargetEntities(allTargets);
        
        // Set primary target (first valid target or null)
        targetEntity = allTargets.Count > 0 ? allTargets[0] : null;
        
        if (targetEntity == null)
        {
            Debug.LogWarning($"SourceAndTargetIdentifier: Failed to determine target entity for card {gameObject.name}");
        }
        else
        {
            Debug.Log($"SourceAndTargetIdentifier: Primary target entity for card {gameObject.name} is {targetEntity.EntityName.Value}");
        }

        // Update debug info
        UpdateDebugInfo();
    }

    private void UpdateDebugInfo()
    {
        currentSourceName = sourceEntity != null ? sourceEntity.EntityName.Value : "None";
        currentTargetName = targetEntity != null ? targetEntity.EntityName.Value : "None";
        
        // Build all targets string
        if (allTargets.Count > 0)
        {
            List<string> targetNames = new List<string>();
            foreach (var target in allTargets)
            {
                if (target != null)
                    targetNames.Add(target.EntityName.Value);
            }
            allTargetNames = string.Join(", ", targetNames);
        }
        else
        {
            allTargetNames = "None";
        }
        
        Debug.Log($"SourceAndTargetIdentifier: Debug info updated - Source: {currentSourceName}, Primary Target: {currentTargetName}, All Targets: {allTargetNames}");
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

    /// <summary>
    /// Gets all valid target entities based on the card's target type
    /// </summary>
    private void GetTargetEntities(List<NetworkEntity> targets)
    {
        if (card == null || card.CardData == null || sourceEntity == null || fightManager == null)
            return;

        CardTargetType effectiveTargetType = card.CardData.GetEffectiveTargetType();

        switch (effectiveTargetType)
        {
            case CardTargetType.Self:
                targets.Add(sourceEntity);
                break;
                
            case CardTargetType.Opponent:
                NetworkEntity opponent = fightManager.GetOpponentForPlayer(sourceEntity);
                if (opponent != null)
                    targets.Add(opponent);
                break;
                
            case CardTargetType.Ally:
                NetworkEntity ally = GetAllyForEntity(sourceEntity);
                if (ally != null)
                    targets.Add(ally);
                break;
                
            case CardTargetType.Random:
                List<NetworkEntity> allPossibleTargets = GetAllPossibleTargets();
                if (allPossibleTargets.Count > 0)
                {
                    int randomIndex = Random.Range(0, allPossibleTargets.Count);
                    targets.Add(allPossibleTargets[randomIndex]);
                }
                break;
                
            case CardTargetType.All:
                targets.AddRange(GetAllPossibleTargets());
                break;
                
            case CardTargetType.AllAllies:
                targets.Add(sourceEntity); // Self is an ally
                NetworkEntity allyEntity = GetAllyForEntity(sourceEntity);
                if (allyEntity != null)
                    targets.Add(allyEntity);
                break;
                
            case CardTargetType.AllEnemies:
                NetworkEntity enemyEntity = fightManager.GetOpponentForPlayer(sourceEntity);
                if (enemyEntity != null)
                    targets.Add(enemyEntity);
                // Note: In a 1v1 system, there's only one enemy
                break;
        }
    }

    /// <summary>
    /// Gets the ally entity for the given entity
    /// </summary>
    private NetworkEntity GetAllyForEntity(NetworkEntity entity)
    {
        if (entity == null) return null;

        RelationshipManager relationshipManager = entity.GetComponent<RelationshipManager>();
        if (relationshipManager?.AllyEntity != null)
        {
            return relationshipManager.AllyEntity.GetComponent<NetworkEntity>();
        }

        return null;
    }

    /// <summary>
    /// Gets all possible target entities in the current fight
    /// </summary>
    private List<NetworkEntity> GetAllPossibleTargets()
    {
        List<NetworkEntity> allTargets = new List<NetworkEntity>();
        
        if (sourceEntity == null) return allTargets;

        // Add self
        allTargets.Add(sourceEntity);
        
        // Add ally if exists
        NetworkEntity ally = GetAllyForEntity(sourceEntity);
        if (ally != null)
            allTargets.Add(ally);
            
        // Add opponent
        NetworkEntity opponent = fightManager.GetOpponentForPlayer(sourceEntity);
        if (opponent != null)
            allTargets.Add(opponent);

        return allTargets;
    }

    /// <summary>
    /// Forces update of source and target entities for AI-controlled cards
    /// </summary>
    public void ForceUpdateSourceAndTarget(NetworkEntity source, NetworkEntity target)
    {
        Debug.Log($"SourceAndTargetIdentifier: ForceUpdateSourceAndTarget called for card {gameObject.name} with source: {source.EntityName.Value}, target: {target.EntityName.Value}");
        
        sourceEntity = source;
        targetEntity = target;
        
        // Clear and set all targets list
        allTargets.Clear();
        if (target != null)
            allTargets.Add(target);
        
        // Update debug info
        UpdateDebugInfo();
    }

    /// <summary>
    /// Forces update with multiple targets for multi-target cards
    /// </summary>
    public void ForceUpdateSourceAndTargets(NetworkEntity source, List<NetworkEntity> targets)
    {
        Debug.Log($"SourceAndTargetIdentifier: ForceUpdateSourceAndTargets called for card {gameObject.name} with {targets.Count} targets");
        
        sourceEntity = source;
        allTargets.Clear();
        allTargets.AddRange(targets);
        
        // Set primary target
        targetEntity = allTargets.Count > 0 ? allTargets[0] : null;
        
        // Update debug info
        UpdateDebugInfo();
    }

    /// <summary>
    /// Shows damage/heal previews on all target entities
    /// </summary>
    private void ShowDamagePreviews()
    {
        if (sourceEntity == null || card?.CardData == null || allTargets.Count == 0)
            return;

        Debug.Log($"SourceAndTargetIdentifier: Showing damage previews for card {card.CardData.CardName}");

        foreach (var target in allTargets)
        {
            if (target == null) continue;

            // Calculate damage/heal amounts for this target
            var (damageAmount, healAmount) = CalculateCardEffectsOnTarget(target);

            // Show preview on the target's UI
            ShowPreviewOnTarget(target, damageAmount, healAmount);
        }
    }

    /// <summary>
    /// Hides damage/heal previews on all target entities
    /// </summary>
    private void HideDamagePreviews()
    {
        if (allTargets.Count == 0) return;

        Debug.Log($"SourceAndTargetIdentifier: Hiding damage previews");

        foreach (var target in allTargets)
        {
            if (target == null) continue;
            HidePreviewOnTarget(target);
        }
    }

    /// <summary>
    /// Calculates damage and heal amounts this card would do to a specific target
    /// </summary>
    private (int damageAmount, int healAmount) CalculateCardEffectsOnTarget(NetworkEntity target)
    {
        int damageAmount = 0;
        int healAmount = 0;

        if (card?.CardData?.Effects == null) return (0, 0);

        // Check if card has any damage effects
        bool hasDamageEffects = false;
        foreach (var effect in card.CardData.Effects)
        {
            if (effect.effectType == CardEffectType.Damage)
            {
                hasDamageEffects = true;
                break;
            }
        }

        // Calculate total damage using DamageCalculator if card has damage effects
        if (hasDamageEffects && damageCalculator != null)
        {
            damageAmount = damageCalculator.CalculateDamage(sourceEntity, target, card.CardData);
        }

        // Calculate heal amounts by summing heal effects
        foreach (var effect in card.CardData.Effects)
        {
            switch (effect.effectType)
            {
                case CardEffectType.Heal:
                    healAmount += effect.amount;
                    break;

                case CardEffectType.RestoreEnergy:
                    // Could show energy restore as green text too
                    healAmount += effect.amount;
                    break;
            }
        }

        return (damageAmount, healAmount);
    }

    /// <summary>
    /// Shows preview on a specific target's UI
    /// </summary>
    private void ShowPreviewOnTarget(NetworkEntity target, int damageAmount, int healAmount)
    {
        // Determine which value to show (prioritize damage over heal)
        bool showDamage = damageAmount > 0;
        int amountToShow = showDamage ? damageAmount : healAmount;

        if (amountToShow <= 0) return; // Nothing to show

        Debug.Log($"SourceAndTargetIdentifier: Attempting to show preview on {target.EntityName.Value} (Type: {target.EntityType})");

        // Check if this is an ally pet (different from opponent pets)
        bool isAllyPet = IsAllyPet(target);

        if (isAllyPet)
        {
            // For ally pets, use OwnPetVisualDisplay
            var petDisplay = FindPetDisplayForEntity(target);
            if (petDisplay != null)
            {
                var petImage = petDisplay.GetPetImage();
                if (petImage != null)
                {
                    petDisplay.ShowDamagePreview(amountToShow, showDamage);
                    Debug.Log($"SourceAndTargetIdentifier: Showing preview over ally pet {target.EntityName.Value}'s image via OwnPetVisualDisplay");
                }
                else
                {
                    Debug.LogWarning($"SourceAndTargetIdentifier: No pet image found in OwnPetVisualDisplay for ally pet {target.EntityName.Value}");
                    petDisplay.ShowDamagePreview(amountToShow, showDamage); // Show anyway
                }
            }
            else
            {
                Debug.LogError($"SourceAndTargetIdentifier: No OwnPetVisualDisplay found for ally pet {target.EntityName.Value}");
            }
        }
        else if (target.EntityType == EntityType.Player || target.EntityType == EntityType.Pet)
        {
            // Use NetworkEntityUI for main entities (players and opponent pets)
            var entityUI = target.GetComponent<NetworkEntityUI>();
            if (entityUI != null)
            {
                var entityImage = entityUI.GetEntityImage();
                if (entityImage != null)
                {
                    entityUI.ShowDamagePreview(amountToShow, showDamage);
                    Debug.Log($"SourceAndTargetIdentifier: Showing preview over {target.EntityName.Value}'s entity image");
                }
                else
                {
                    Debug.LogWarning($"SourceAndTargetIdentifier: No entity image found for {target.EntityName.Value} - preview may not position correctly");
                    entityUI.ShowDamagePreview(amountToShow, showDamage); // Show anyway
                }
            }
            else
            {
                Debug.LogError($"SourceAndTargetIdentifier: No NetworkEntityUI found on {target.EntityName.Value}");
            }
        }
    }

    /// <summary>
    /// Determines if a target entity is an ally pet (vs opponent pet)
    /// </summary>
    private bool IsAllyPet(NetworkEntity target)
    {
        if (target.EntityType != EntityType.Pet || sourceEntity == null)
            return false;

        // Check if this pet is an ally of the source entity
        var sourceRelationships = sourceEntity.GetComponent<RelationshipManager>();
        if (sourceRelationships?.AllyEntity != null)
        {
            var allyEntity = sourceRelationships.AllyEntity.GetComponent<NetworkEntity>();
            return allyEntity == target;
        }

        return false;
    }

    /// <summary>
    /// Hides preview on a specific target's UI
    /// </summary>
    private void HidePreviewOnTarget(NetworkEntity target)
    {
        // Check if this is an ally pet (different from opponent pets)
        bool isAllyPet = IsAllyPet(target);

        if (isAllyPet)
        {
            // For ally pets, use OwnPetVisualDisplay
            var petDisplay = FindPetDisplayForEntity(target);
            if (petDisplay != null)
            {
                petDisplay.HideDamagePreview();
            }
        }
        else if (target.EntityType == EntityType.Player || target.EntityType == EntityType.Pet)
        {
            // Use NetworkEntityUI for main entities (players and opponent pets)
            var entityUI = target.GetComponent<NetworkEntityUI>();
            if (entityUI != null)
            {
                entityUI.HideDamagePreview();
            }
        }
    }

    /// <summary>
    /// Finds the OwnPetVisualDisplay component for an ally pet entity
    /// </summary>
    private OwnPetVisualDisplay FindPetDisplayForEntity(NetworkEntity petEntity)
    {
        // Find all OwnPetVisualDisplay components in the scene
        var petDisplays = FindObjectsByType<OwnPetVisualDisplay>(FindObjectsSortMode.None);
        
        foreach (var display in petDisplays)
        {
            if (display.GetCurrentPet() == petEntity)
            {
                return display;
            }
        }

        return null;
    }
} 