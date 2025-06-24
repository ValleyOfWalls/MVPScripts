using UnityEngine;
using UnityEngine.EventSystems;
using FishNet.Object;
using System.Collections.Generic;

/// <summary>
/// Identifies the source and target of a card play with enhanced targeting support.
/// Attach to: Card prefabs alongside the Card component.
/// Now controlled by UIHoverDetector for hover events.
/// </summary>
public class SourceAndTargetIdentifier : NetworkBehaviour, UnityEngine.EventSystems.IPointerDownHandler
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
    
    // Override target for drag and drop
    private NetworkEntity overrideTarget;
    private bool hasOverrideTarget = false;
    
    // Drag state tracking
    private bool isDragging = false;
    private bool previewsActive = false;
    
    // Global drag state - static to track if ANY card is being dragged
    private static bool isAnyCardBeingDragged = false;
    
    // Component references
    private CardDragDrop cardDragDrop;

    public NetworkEntity SourceEntity => sourceEntity;
    public NetworkEntity TargetEntity => targetEntity;
    public List<NetworkEntity> AllTargets => new List<NetworkEntity>(allTargets);

    private void Awake()
    {
        // Setup required references
        SetupRequiredReferences();
        
        // Get CardDragDrop component if it exists
        cardDragDrop = GetComponent<CardDragDrop>();
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

    // Unity's built-in mouse events (for 3D colliders)
    public void OnMouseEnter()
    {
        Debug.Log($"SourceAndTargetIdentifier: OnMouseEnter (3D) called for {gameObject.name}");
        HandlePointerEnter();
    }
    
    public void OnMouseDown()
    {
        Debug.Log($"SourceAndTargetIdentifier: OnMouseDown (3D) called for {gameObject.name}");
        HandlePointerDown();
    }
    
    public void OnMouseExit()
    {
        Debug.Log($"SourceAndTargetIdentifier: OnMouseExit (3D) called for {gameObject.name}");
        HandlePointerExit();
    }

    // UI event system interfaces (for UI elements)
    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
    {
        Debug.Log($"SourceAndTargetIdentifier: OnPointerDown (UI) called for {gameObject.name}");
        HandlePointerDown();
    }

    // Public methods for UIHoverDetector to call
    public void HandlePointerEnter()
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
    
    private void HandlePointerDown()
    {
        if (!IsOwner) return;
        
        // Always update source and target on mouse down to ensure they're current when the card is played
        // This is critical because Card.OnMouseDown() needs valid source/target entities
        UpdateSourceAndTarget();
        
        // Reset the hover flag since we've just updated
        hasUpdatedThisHover = true;
    }
    
    public void HandlePointerExit()
    {
        // Reset the flag when mouse exits so we can update again on the next hover
        hasUpdatedThisHover = false;

        // Only hide damage previews if we're not dragging
        // If we're dragging, the previews should stay visible until drag ends
        if (!isDragging)
        {
            HideDamagePreviews();
        }
        else
        {
            Debug.Log($"SourceAndTargetIdentifier: Skipping damage preview hide because card is being dragged");
        }
    }

    /// <summary>
    /// Called by CardDragDrop when drag starts - keeps damage previews visible
    /// </summary>
    public void OnDragStart()
    {
        isDragging = true;
        isAnyCardBeingDragged = true; // Set global drag state
        Debug.Log($"SourceAndTargetIdentifier: Drag started - damage previews will remain visible, global drag state set");
    }
    
    /// <summary>
    /// Called by CardDragDrop when drag ends - allows damage previews to be hidden
    /// </summary>
    public void OnDragEnd()
    {
        isDragging = false;
        isAnyCardBeingDragged = false; // Clear global drag state
        Debug.Log($"SourceAndTargetIdentifier: Drag ended - damage previews can now be hidden, global drag state cleared");
        
        // Hide previews since drag is over and mouse is likely not over card anymore
        HideDamagePreviews();
    }
    
    /// <summary>
    /// Called when the card is played/discarded to force cleanup of damage previews
    /// </summary>
    public void OnCardPlayedOrDiscarded()
    {
        /* Debug.Log($"SourceAndTargetIdentifier: Card played/discarded - forcing damage preview cleanup"); */
        
        // Force hide all previews regardless of drag state
        HideDamagePreviews();
        
        // Reset all states
        if (isDragging)
        {
            isAnyCardBeingDragged = false; // Clear global drag state if this card was being dragged
        }
        isDragging = false;
        previewsActive = false;
        hasUpdatedThisHover = false;
    }

    /// <summary>
    /// Sets an override target for drag and drop functionality
    /// </summary>
    /// <param name="target">The entity to override as the target</param>
    public void SetOverrideTarget(NetworkEntity target)
    {
        overrideTarget = target;
        hasOverrideTarget = target != null;
        
        /* Debug.Log($"SourceAndTargetIdentifier: Override target set to {(target != null ? target.EntityName.Value : "null")}"); */
        
        // Update targeting with the override
        if (hasOverrideTarget)
        {
            UpdateSourceAndTarget();
        }
    }
    
    /// <summary>
    /// Clears the override target
    /// </summary>
    public void ClearOverrideTarget()
    {
        overrideTarget = null;
        hasOverrideTarget = false;
        
        Debug.Log($"SourceAndTargetIdentifier: Override target cleared");
    }

    /// <summary>
    /// Updates the source and target entities based on the card's target type
    /// </summary>
    public void UpdateSourceAndTarget()
    {
        /* Debug.Log($"SourceAndTargetIdentifier: UpdateSourceAndTarget called for card {gameObject.name}"); */
        
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
            /* Debug.Log($"SourceAndTargetIdentifier: Source entity for card {gameObject.name} is {sourceEntity.EntityName.Value}"); */
        }
        
        // Check if we have an override target from drag and drop
        if (hasOverrideTarget && overrideTarget != null)
        {
            /* Debug.Log($"SourceAndTargetIdentifier: Using override target {overrideTarget.EntityName.Value} for drag and drop"); */
            
            // Set the override target as the primary target
            allTargets.Clear();
            allTargets.Add(overrideTarget);
            targetEntity = overrideTarget;
        }
        else
        {
            // Get targets based on the card's target type (normal behavior)
            allTargets.Clear();
            GetTargetEntities(allTargets);
            
            // Set primary target (first valid target or null)
            targetEntity = allTargets.Count > 0 ? allTargets[0] : null;
        }
        
        if (targetEntity == null)
        {
            Debug.LogWarning($"SourceAndTargetIdentifier: Failed to determine target entity for card {gameObject.name}");
        }
        else
        {
            /* Debug.Log($"SourceAndTargetIdentifier: Primary target entity for card {gameObject.name} is {targetEntity.EntityName.Value}"); */
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
        
        /* Debug.Log($"SourceAndTargetIdentifier: Debug info updated - Source: {currentSourceName}, Primary Target: {currentTargetName}, All Targets: {allTargetNames}"); */
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
                
            // Removed All, AllAllies, and AllEnemies targeting options
            // These were replaced with single target + "can also target" system for flexibility
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
        /* Debug.Log($"SourceAndTargetIdentifier: ForceUpdateSourceAndTarget called for card {gameObject.name} with source: {source.EntityName.Value}, target: {target.EntityName.Value}"); */
        
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
        /* Debug.Log($"SourceAndTargetIdentifier: ForceUpdateSourceAndTargets called for card {gameObject.name} with {targets.Count} targets"); */
        
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

        // Don't show damage previews on other cards when any card is being dragged
        // Only the card being dragged should keep its previews visible
        if (isAnyCardBeingDragged && !isDragging)
        {
            Debug.Log($"SourceAndTargetIdentifier: Skipping damage preview show because another card is being dragged");
            return;
        }

        /* Debug.Log($"SourceAndTargetIdentifier: Showing damage previews for card {card.CardData.CardName}"); */
        
        // Mark previews as active
        previewsActive = true;

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
        if (!previewsActive || allTargets.Count == 0) return;

        /* Debug.Log($"SourceAndTargetIdentifier: Hiding damage previews"); */
        
        // Mark previews as inactive
        previewsActive = false;

        foreach (var target in allTargets)
        {
            if (target == null) continue;
            HidePreviewOnTarget(target);
        }
    }
    
    /// <summary>
    /// Force cleanup on destruction to ensure damage previews are hidden
    /// </summary>
    private void OnDestroy()
    {
        // Force cleanup of any active damage previews when the card is destroyed
        if (previewsActive && allTargets.Count > 0)
        {
            /* Debug.Log($"SourceAndTargetIdentifier: Card being destroyed - forcing damage preview cleanup"); */
            
            foreach (var target in allTargets)
            {
                if (target == null) continue;
                HidePreviewOnTarget(target);
            }
            
            previewsActive = false;
        }
        
        // Clear global drag state if this card was being dragged when destroyed
        if (isDragging)
        {
            isAnyCardBeingDragged = false;
            Debug.Log($"SourceAndTargetIdentifier: Card being destroyed during drag - clearing global drag state");
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

        /* Debug.Log($"SourceAndTargetIdentifier: Attempting to show preview on {target.EntityName.Value} (Type: {target.EntityType})"); */

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
                    /* Debug.Log($"SourceAndTargetIdentifier: Showing preview over ally pet {target.EntityName.Value}'s image via OwnPetVisualDisplay"); */
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
            // Use EntityStatsUIController through RelationshipManager for main entities (players and opponent pets)
            var relationshipManager = target.GetComponent<RelationshipManager>();
            if (relationshipManager != null && relationshipManager.StatsUIEntity != null)
            {
                var statsUIController = relationshipManager.StatsUIEntity.GetComponent<EntityStatsUIController>();
                if (statsUIController != null)
                {
                    statsUIController.ShowDamagePreview(amountToShow, showDamage);
                    /* Debug.Log($"SourceAndTargetIdentifier: Showing preview over {target.EntityName.Value}'s stats UI"); */
                }
                else
                {
                    Debug.LogError($"SourceAndTargetIdentifier: No EntityStatsUIController found on stats UI for {target.EntityName.Value}");
                }
            }
            else
            {
                Debug.LogError($"SourceAndTargetIdentifier: No RelationshipManager or stats UI entity found for {target.EntityName.Value}");
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
            // Use EntityStatsUIController through RelationshipManager for main entities (players and opponent pets)
            var relationshipManager = target.GetComponent<RelationshipManager>();
            if (relationshipManager != null && relationshipManager.StatsUIEntity != null)
            {
                var statsUIController = relationshipManager.StatsUIEntity.GetComponent<EntityStatsUIController>();
                if (statsUIController != null)
                {
                    statsUIController.HideDamagePreview();
                }
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