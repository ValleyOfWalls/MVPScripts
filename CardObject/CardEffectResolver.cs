using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Enhanced resolver for card effects supporting zone effects, persistent effects, stance system, and scaling.
/// Attach to: Card prefabs alongside Card, HandleCardPlay, and SourceAndTargetIdentifier.
/// </summary>
public class CardEffectResolver : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Card card;
    [SerializeField] private HandleCardPlay handleCardPlay;
    [SerializeField] private SourceAndTargetIdentifier sourceAndTargetIdentifier;
    [SerializeField] private CardTracker cardTracker;
    
    // References to the services we need
    private DamageCalculator damageCalculator;
    
    private void Awake()
    {
        // Get required component references if not assigned
        if (card == null) card = GetComponent<Card>();
        if (handleCardPlay == null) handleCardPlay = GetComponent<HandleCardPlay>();
        if (sourceAndTargetIdentifier == null) sourceAndTargetIdentifier = GetComponent<SourceAndTargetIdentifier>();
        if (cardTracker == null) cardTracker = GetComponent<CardTracker>();
        
        // Validate components
        ValidateComponents();
    }
    
    private void Start()
    {
        // Find the DamageCalculator (now on CombatManager)
        if (damageCalculator == null)
        {
            CombatManager combatManager = FindFirstObjectByType<CombatManager>();
            if (combatManager != null)
            {
                damageCalculator = combatManager.GetComponent<DamageCalculator>();
                if (damageCalculator == null)
                {
                    Debug.LogError($"CardEffectResolver on {gameObject.name}: Could not find DamageCalculator on CombatManager!");
                }
            }
            else
            {
                Debug.LogError($"CardEffectResolver on {gameObject.name}: Could not find CombatManager!");
            }
        }
    }
    
    private void ValidateComponents()
    {
        if (card == null)
            Debug.LogError($"CardEffectResolver on {gameObject.name}: Missing Card component!");
        
        if (handleCardPlay == null)
            Debug.LogError($"CardEffectResolver on {gameObject.name}: Missing HandleCardPlay component!");
        
        if (sourceAndTargetIdentifier == null)
            Debug.LogError($"CardEffectResolver on {gameObject.name}: Missing SourceAndTargetIdentifier component!");
    }
    
    /// <summary>
    /// Called by HandleCardPlay when the card is being played.
    /// </summary>
    public void ResolveCardEffect()
    {
        if (!IsOwner)
        {
            Debug.LogWarning($"CardEffectResolver: Cannot resolve effect, not network owner of card {gameObject.name}");
            return;
        }
        
        // Make sure we have source and target entities
        var sourceEntity = sourceAndTargetIdentifier.SourceEntity;
        var allTargets = sourceAndTargetIdentifier.AllTargets;
        
        if (sourceEntity == null || allTargets.Count == 0)
        {
            Debug.LogError($"CardEffectResolver: Missing source or target entities for card {gameObject.name}");
            return;
        }
        
        // Get the card data
        CardData cardData = card.CardData;
        if (cardData == null)
        {
            Debug.LogError($"CardEffectResolver: No card data for card {gameObject.name}");
            return;
        }
        
        // Check sequence requirements before playing
        EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
        if (sourceTracker != null)
        {
            bool canPlay = cardData.CanPlayWithSequence(
                sourceTracker.TrackingData.lastPlayedCardType,
                sourceTracker.ComboCount,
                sourceTracker.CurrentStance
            );
            
            if (!canPlay)
            {
                Debug.LogWarning($"CardEffectResolver: Card {cardData.CardName} cannot be played due to sequence requirements");
                return;
            }
        }
        
        // Execute the card effect on the server
        List<int> targetIds = new List<int>();
        foreach (var target in allTargets)
        {
            if (target != null)
                targetIds.Add(target.ObjectId);
        }
        
        CmdResolveEffect(sourceEntity.ObjectId, targetIds.ToArray(), cardData.CardId);
    }
    
    [ServerRpc]
    private void CmdResolveEffect(int sourceEntityId, int[] targetEntityIds, int cardDataId)
    {
        Debug.Log($"CardEffectResolver: Server resolving effect for card {cardDataId} from entity {sourceEntityId} to {targetEntityIds.Length} targets");
        
        // Find the source entity
        NetworkEntity sourceEntity = FindEntityById(sourceEntityId);
        if (sourceEntity == null)
        {
            Debug.LogError($"CardEffectResolver: Could not find source entity with ID {sourceEntityId}");
            return;
        }
        
        // Find all target entities
        List<NetworkEntity> targetEntities = new List<NetworkEntity>();
        foreach (int targetId in targetEntityIds)
        {
            NetworkEntity target = FindEntityById(targetId);
            if (target != null)
                targetEntities.Add(target);
        }
        
        if (targetEntities.Count == 0)
        {
            Debug.LogError($"CardEffectResolver: Could not find any target entities");
            return;
        }
        
        // Get the card data from the database
        CardData cardData = CardDatabase.Instance.GetCardById(cardDataId);
        if (cardData == null)
        {
            Debug.LogError($"CardEffectResolver: Could not find card data for ID {cardDataId}");
            return;
        }
        
        // Record card play in tracking systems
        RecordCardPlay(sourceEntity, cardData);
        
        // Process the effect based on the card type
        ProcessCardEffects(sourceEntity, targetEntities, cardData);
    }
    
    /// <summary>
    /// Records the card play in various tracking systems
    /// </summary>
    private void RecordCardPlay(NetworkEntity sourceEntity, CardData cardData)
    {
        // Record in entity tracker with enhanced data
        EntityTracker entityTracker = sourceEntity.GetComponent<EntityTracker>();
        if (entityTracker != null)
        {
            entityTracker.RecordCardPlayed(cardData.CardId, cardData.HasComboModifier, cardData.CardType, cardData.IsZeroCost);
        }
        
        // Record in card tracker if available
        if (cardTracker != null)
        {
            cardTracker.RecordCardPlayed();
        }
    }
    
    /// <summary>
    /// Processes all effects for a card (main effect, multi-effects, conditional effects, zone effects, persistent effects)
    /// </summary>
    private void ProcessCardEffects(NetworkEntity sourceEntity, List<NetworkEntity> targetEntities, CardData cardData)
    {
        // Get tracking data for scaling calculations
        EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
        EntityTrackingData trackingData = sourceTracker?.GetTrackingDataForScaling() ?? new EntityTrackingData();
        
        // Process zone effects first (they affect all players/pets globally)
        if (cardData.HasZoneEffect)
        {
            ProcessZoneEffects(sourceEntity, cardData, trackingData);
        }
        
        // Process persistent fight effects
        if (cardData.HasPersistentEffect)
        {
            ProcessPersistentEffects(sourceEntity, cardData);
        }
        
        // Process stance effects
        if (cardData.AffectsStance)
        {
            ProcessStanceEffects(sourceEntity, cardData);
        }
        
        // Process main effect with scaling
        int scaledAmount = cardData.GetScalingAmount(ScalingType.None, cardData.Amount, trackingData);
        ProcessSingleEffect(sourceEntity, targetEntities, cardData.EffectType, scaledAmount, cardData.Duration, cardData.ElementalType);
        
        // Process scaling effects
        if (cardData.HasScalingEffect)
        {
            ProcessScalingEffects(sourceEntity, targetEntities, cardData, trackingData);
        }
        
        // Process multi-effects if any
        if (cardData.HasMultipleEffects)
        {
            foreach (var effect in cardData.Effects)
            {
                List<NetworkEntity> effectTargets = GetTargetsForEffect(sourceEntity, effect.targetType, targetEntities);
                
                // Apply scaling if the effect uses it
                int effectAmount = effect.amount;
                if (effect.scalingType != ScalingType.None)
                {
                    effectAmount = CalculateScaledAmountFromEffect(effect, trackingData);
                }
                
                ProcessSingleEffect(sourceEntity, effectTargets, effect.effectType, effectAmount, effect.duration, effect.elementalType);
            }
        }
        
        // Process conditional effects if any
        if (cardData.HasConditionalEffect)
        {
            ProcessConditionalEffect(sourceEntity, targetEntities, cardData, trackingData);
        }
    }
    
    /// <summary>
    /// Processes zone effects that affect all players/pets globally
    /// </summary>
    private void ProcessZoneEffects(NetworkEntity sourceEntity, CardData cardData, EntityTrackingData trackingData)
    {
        foreach (var zoneEffect in cardData.ZoneEffects)
        {
            // Get all entities based on zone effect targeting
            List<NetworkEntity> zoneTargets = new List<NetworkEntity>();
            
            if (zoneEffect.affectAllPlayers || zoneEffect.affectAllPets)
            {
                List<NetworkEntity> globalEntities = EntityTracker.GetAllEntitiesForZoneEffect(
                    zoneEffect.affectAllPlayers, 
                    zoneEffect.affectAllPets
                );
                zoneTargets.AddRange(globalEntities);
            }
            
            // Include caster if specified
            if (zoneEffect.affectCaster)
            {
                zoneTargets.Add(sourceEntity);
            }
            
            // Exclude opponents if specified
            if (zoneEffect.excludeOpponents)
            {
                // This would need logic to determine who are opponents vs allies
                // For now, placeholder for the concept
            }
            
            // Calculate scaled amount
            int zoneAmount = zoneEffect.baseAmount;
            if (zoneEffect.scalingType != ScalingType.None)
            {
                int scalingValue = GetScalingValue(zoneEffect.scalingType, trackingData);
                zoneAmount += Mathf.FloorToInt(scalingValue * zoneEffect.scalingMultiplier);
            }
            
            // Apply the zone effect to all targets
            ProcessSingleEffect(sourceEntity, zoneTargets, zoneEffect.effectType, zoneAmount, zoneEffect.duration, zoneEffect.elementalType);
            
            Debug.Log($"CardEffectResolver: Applied zone effect {zoneEffect.effectType} to {zoneTargets.Count} entities");
        }
    }
    
    /// <summary>
    /// Processes persistent fight effects
    /// </summary>
    private void ProcessPersistentEffects(NetworkEntity sourceEntity, CardData cardData)
    {
        EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
        if (sourceTracker == null) return;
        
        foreach (var persistentEffect in cardData.PersistentEffects)
        {
            // Convert persistent effect to string for storage
            string effectData = $"{persistentEffect.effectName}|{persistentEffect.effectType}|{persistentEffect.potency}|{persistentEffect.triggerInterval}|{persistentEffect.turnDuration}|{persistentEffect.lastEntireFight}";
            
            sourceTracker.AddPersistentEffect(effectData);
            
            Debug.Log($"CardEffectResolver: Added persistent effect {persistentEffect.effectName} to {sourceEntity.EntityName.Value}");
        }
    }
    
    /// <summary>
    /// Processes stance effects
    /// </summary>
    private void ProcessStanceEffects(NetworkEntity sourceEntity, CardData cardData)
    {
        EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
        if (sourceTracker == null) return;
        
        var stanceEffect = cardData.StanceEffect;
        
        // Apply stance change
        sourceTracker.SetStance(stanceEffect.stanceType);
        
        // Apply immediate stance benefits
        if (stanceEffect.grantsThorns)
        {
            EffectHandler effectHandler = sourceEntity.GetComponent<EffectHandler>();
            if (effectHandler != null)
            {
                effectHandler.AddEffect("Thorns", stanceEffect.thornsAmount, 999, sourceEntity); // Long duration
            }
        }
        
        if (stanceEffect.grantsShield)
        {
            EffectHandler effectHandler = sourceEntity.GetComponent<EffectHandler>();
            if (effectHandler != null)
            {
                effectHandler.AddEffect("Shield", stanceEffect.shieldAmount, 999, sourceEntity);
            }
        }
        
        Debug.Log($"CardEffectResolver: {sourceEntity.EntityName.Value} entered {stanceEffect.stanceType} stance");
    }
    
    /// <summary>
    /// Processes scaling effects
    /// </summary>
    private void ProcessScalingEffects(NetworkEntity sourceEntity, List<NetworkEntity> targetEntities, CardData cardData, EntityTrackingData trackingData)
    {
        foreach (var scalingEffect in cardData.ScalingEffects)
        {
            int scaledAmount = CalculateScaledAmount(scalingEffect, trackingData);
            ProcessSingleEffect(sourceEntity, targetEntities, scalingEffect.effectType, scaledAmount, 0, scalingEffect.elementalType);
        }
    }
    
    /// <summary>
    /// Calculates scaled amount for scaling effects
    /// </summary>
    private int CalculateScaledAmount(ScalingEffect scalingEffect, EntityTrackingData trackingData)
    {
        int scalingValue = GetScalingValue(scalingEffect.scalingType, trackingData);
        int scaledAmount = scalingEffect.baseAmount + Mathf.FloorToInt(scalingValue * scalingEffect.scalingMultiplier);
        return Mathf.Min(scaledAmount, scalingEffect.maxScaling);
    }
    
    /// <summary>
    /// Calculates scaled amount for a CardEffect with scaling
    /// </summary>
    private int CalculateScaledAmountFromEffect(CardEffect effect, EntityTrackingData trackingData)
    {
        int scalingValue = GetScalingValue(effect.scalingType, trackingData);
        int scaledAmount = effect.amount + Mathf.FloorToInt(scalingValue * effect.scalingMultiplier);
        return Mathf.Min(scaledAmount, effect.maxScaling);
    }
    
    /// <summary>
    /// Gets the current value for a scaling type
    /// </summary>
    private int GetScalingValue(ScalingType scalingType, EntityTrackingData trackingData)
    {
        switch (scalingType)
        {
            case ScalingType.ZeroCostCardsThisTurn:
                return trackingData.zeroCostCardsThisTurn;
            case ScalingType.ZeroCostCardsThisFight:
                return trackingData.zeroCostCardsThisFight;
            case ScalingType.CardsPlayedThisTurn:
                return trackingData.cardsPlayedThisTurn;
            case ScalingType.CardsPlayedThisFight:
                return trackingData.cardsPlayedThisFight;
            case ScalingType.DamageDealtThisTurn:
                return trackingData.damageDealtLastRound;
            case ScalingType.DamageDealtThisFight:
                return trackingData.damageDealtThisFight;
            case ScalingType.ComboCount:
                return trackingData.comboCount;
            default:
                return 0;
        }
    }
    
    /// <summary>
    /// Gets the appropriate targets for a specific effect based on its target type
    /// </summary>
    private List<NetworkEntity> GetTargetsForEffect(NetworkEntity sourceEntity, CardTargetType targetType, List<NetworkEntity> originalTargets)
    {
        List<NetworkEntity> effectTargets = new List<NetworkEntity>();
        
        switch (targetType)
        {
            case CardTargetType.Self:
                effectTargets.Add(sourceEntity);
                break;
            case CardTargetType.Ally:
                NetworkEntity ally = GetAllyForEntity(sourceEntity);
                if (ally != null)
                    effectTargets.Add(ally);
                break;
            case CardTargetType.AllPlayers:
                effectTargets.AddRange(EntityTracker.GetAllEntitiesForZoneEffect(true, false));
                break;
            case CardTargetType.AllPets:
                effectTargets.AddRange(EntityTracker.GetAllEntitiesForZoneEffect(false, true));
                break;
            case CardTargetType.Everyone:
                effectTargets.AddRange(EntityTracker.GetAllEntitiesForZoneEffect(true, true));
                break;
            default:
                // Use original targets for other types
                effectTargets.AddRange(originalTargets);
                break;
        }
        
        return effectTargets;
    }
    
    /// <summary>
    /// Processes conditional effects based on current game state
    /// </summary>
    private void ProcessConditionalEffect(NetworkEntity sourceEntity, List<NetworkEntity> targetEntities, CardData cardData, EntityTrackingData trackingData)
    {
        // Process all effects that have conditions
        foreach (var effect in cardData.Effects.Where(e => e.conditionType != ConditionalType.None))
        {
            bool conditionMet = CheckConditionForEffect(sourceEntity, targetEntities, effect);
            
            if (conditionMet)
            {
                int effectAmount = effect.amount;
                
                // Apply scaling if the effect uses it
                if (effect.scalingType != ScalingType.None)
                {
                    effectAmount = CalculateScaledAmountFromEffect(effect, trackingData);
                }
                
                ProcessSingleEffect(sourceEntity, targetEntities, effect.effectType, 
                    effectAmount, effect.duration, effect.elementalType);
            }
            else if (effect.hasAlternativeEffect)
            {
                ProcessSingleEffect(sourceEntity, targetEntities, effect.alternativeEffectType, 
                    effect.alternativeEffectAmount, 0, ElementalType.None);
            }
        }
    }
    
    /// <summary>
    /// Checks if a conditional effect's condition is met
    /// </summary>
    private bool CheckConditionForEffect(NetworkEntity sourceEntity, List<NetworkEntity> targetEntities, CardEffect effect)
    {
        switch (effect.conditionType)
        {
            case ConditionalType.IfSourceHealthBelow:
                return sourceEntity.CurrentHealth.Value < effect.conditionValue;
            case ConditionalType.IfSourceHealthAbove:
                return sourceEntity.CurrentHealth.Value > effect.conditionValue;
            case ConditionalType.IfTargetHealthBelow:
                return targetEntities.Count > 0 && targetEntities[0].CurrentHealth.Value < effect.conditionValue;
            case ConditionalType.IfTargetHealthAbove:
                return targetEntities.Count > 0 && targetEntities[0].CurrentHealth.Value > effect.conditionValue;
            default:
                // For more complex conditions, delegate to tracking systems
                EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
                if (sourceTracker != null)
                {
                    return sourceTracker.CheckCondition(effect.conditionType, effect.conditionValue);
                }
                return false;
        }
    }
    
    /// <summary>
    /// Processes a single effect on the given targets
    /// </summary>
    private void ProcessSingleEffect(NetworkEntity sourceEntity, List<NetworkEntity> targetEntities, CardEffectType effectType, int amount, int duration, ElementalType elementalType)
    {
        foreach (NetworkEntity targetEntity in targetEntities)
        {
            if (targetEntity == null) continue;
            
            switch (effectType)
            {
                case CardEffectType.Damage:
                    ProcessDamageEffect(sourceEntity, targetEntity, amount);
                    break;
                    
                case CardEffectType.Heal:
                    ProcessHealEffect(sourceEntity, targetEntity, amount);
                    break;
                    
                case CardEffectType.DrawCard:
                    ProcessDrawCardEffect(targetEntity, amount);
                    break;
                    
                case CardEffectType.RestoreEnergy:
                    ProcessRestoreEnergyEffect(targetEntity, amount);
                    break;
                    
                case CardEffectType.ApplyBreak:
                    ProcessStatusEffect(sourceEntity, targetEntity, "Break", amount, duration);
                    break;
                    
                case CardEffectType.ApplyWeak:
                    ProcessStatusEffect(sourceEntity, targetEntity, "Weak", amount, duration);
                    break;
                    
                case CardEffectType.ApplyDamageOverTime:
                    ProcessStatusEffect(sourceEntity, targetEntity, "DamageOverTime", amount, duration);
                    break;
                    
                case CardEffectType.ApplyHealOverTime:
                    ProcessStatusEffect(sourceEntity, targetEntity, "HealOverTime", amount, duration);
                    break;
                    
                case CardEffectType.RaiseCriticalChance:
                    ProcessStatusEffect(sourceEntity, targetEntity, "CriticalUp", amount, duration);
                    break;
                    
                case CardEffectType.ApplyThorns:
                    ProcessStatusEffect(sourceEntity, targetEntity, "Thorns", amount, duration);
                    break;
                    
                case CardEffectType.ApplyShield:
                    ProcessStatusEffect(sourceEntity, targetEntity, "Shield", amount, duration);
                    break;
                    
                case CardEffectType.ApplyStun:
                    ProcessStunEffect(targetEntity, duration);
                    break;
                    
                case CardEffectType.ApplyLimitBreak:
                    ProcessLimitBreakEffect(targetEntity);
                    break;
                    
                case CardEffectType.ApplyStrength:
                    ProcessStrengthEffect(targetEntity, amount);
                    break;
                    
                case CardEffectType.ApplyElementalStatus:
                    ProcessElementalStatusEffect(sourceEntity, targetEntity, elementalType, amount, duration);
                    break;
                    
                case CardEffectType.DiscardRandomCards:
                    ProcessDiscardRandomCardsEffect(targetEntity, amount);
                    break;
                    
                case CardEffectType.EnterStance:
                    ProcessStanceChangeEffect(targetEntity, (StanceType)amount); // amount represents stance type
                    break;
                    
                case CardEffectType.ExitStance:
                    ProcessStanceChangeEffect(targetEntity, StanceType.None); // Exit to neutral stance
                    break;
                    
                case CardEffectType.BuffStats:
                    ProcessStatusEffect(sourceEntity, targetEntity, "Buff", amount, duration);
                    break;
                    
                case CardEffectType.DebuffStats:
                    ProcessStatusEffect(sourceEntity, targetEntity, "Debuff", amount, duration);
                    break;
                    
                default:
                    Debug.LogWarning($"CardEffectResolver: Effect type {effectType} not implemented");
                    break;
            }
        }
    }
    
    private void ProcessDamageEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, int amount)
    {
        // Add strength bonus to damage
        EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
        int strengthBonus = sourceTracker != null ? sourceTracker.StrengthStacks : 0;
        int totalDamage = amount + strengthBonus;
        
        // Get damage calculator from CombatManager
        if (damageCalculator == null)
        {
            CombatManager combatManager = FindFirstObjectByType<CombatManager>();
            damageCalculator = combatManager?.GetComponent<DamageCalculator>();
            
            if (damageCalculator == null)
            {
                Debug.LogError("CardEffectResolver: Could not find DamageCalculator on CombatManager!");
                return;
            }
        }
        
        // Get the original card data from the card component
        CardData originalCardData = card?.CardData;
        if (originalCardData == null)
        {
            Debug.LogError("CardEffectResolver: Could not find CardData on card component!");
            return;
        }
        
        // Debug card data information
        Debug.Log($"CardEffectResolver: Processing damage for card '{originalCardData.CardName}'");
        Debug.Log($"CardEffectResolver: Card Amount property: {originalCardData.Amount}");
        Debug.Log($"CardEffectResolver: Card HasEffects: {originalCardData.HasEffects}");
        Debug.Log($"CardEffectResolver: Card Effects count: {originalCardData.Effects?.Count ?? 0}");
        Debug.Log($"CardEffectResolver: Input amount parameter: {amount}");
        Debug.Log($"CardEffectResolver: Strength bonus: {strengthBonus}");
        Debug.Log($"CardEffectResolver: Total damage calculation: {totalDamage}");
        
        if (originalCardData.HasEffects && originalCardData.Effects.Count > 0)
        {
            var firstEffect = originalCardData.Effects[0];
            Debug.Log($"CardEffectResolver: First effect type: {firstEffect.effectType}, amount: {firstEffect.amount}");
        }
        
        // Calculate damage based on card and status effects using the original CardData
        int finalDamage = damageCalculator.CalculateDamage(sourceEntity, targetEntity, originalCardData);
        
        // If the calculated damage is 0 but we have an amount, apply the amount directly
        // This handles cases where DamageCalculator might not be working as expected
        if (finalDamage == 0 && totalDamage > 0)
        {
            Debug.LogWarning($"CardEffectResolver: DamageCalculator returned 0 damage, using direct amount {totalDamage}");
            finalDamage = totalDamage;
        }
        
        Debug.Log($"CardEffectResolver: Final damage to apply: {finalDamage}");
        
        // Apply the damage through the life handler
        LifeHandler targetLifeHandler = targetEntity.GetComponent<LifeHandler>();
        if (targetLifeHandler != null)
        {
            targetLifeHandler.TakeDamage(finalDamage, sourceEntity);
            
            // Record damage dealt in source entity tracker
            if (sourceTracker != null)
            {
                sourceTracker.RecordDamageDealt(finalDamage);
            }
        }
        else
        {
            Debug.LogError($"CardEffectResolver: Target entity {targetEntity.EntityName.Value} has no LifeHandler!");
        }
    }
    
    private void ProcessHealEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, int amount)
    {
        LifeHandler targetLifeHandler = targetEntity.GetComponent<LifeHandler>();
        if (targetLifeHandler != null)
        {
            targetLifeHandler.Heal(amount, sourceEntity);
            
            // Record healing given in source entity tracker
            EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
            if (sourceTracker != null)
            {
                sourceTracker.RecordHealingGiven(amount);
            }
        }
    }
    
    private void ProcessDrawCardEffect(NetworkEntity targetEntity, int amount)
    {
        HandManager handManager = GetHandManagerForEntity(targetEntity);
        if (handManager != null)
        {
            for (int i = 0; i < amount; i++)
            {
                handManager.DrawOneCard(); // Use DrawOneCard for multiple single draws
            }
        }
    }
    
    private void ProcessRestoreEnergyEffect(NetworkEntity targetEntity, int amount)
    {
        EnergyHandler energyHandler = targetEntity.GetComponent<EnergyHandler>();
        if (energyHandler != null)
        {
            energyHandler.AddEnergy(amount, null);
        }
        else
        {
            // Fallback: directly modify energy
            targetEntity.ChangeEnergy(amount);
        }
    }
    
    private void ProcessStatusEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, string effectName, int potency, int duration)
    {
        EffectHandler targetEffectHandler = targetEntity.GetComponent<EffectHandler>();
        if (targetEffectHandler != null)
        {
            targetEffectHandler.AddEffect(effectName, potency, duration, sourceEntity);
        }
    }
    
    private void ProcessStunEffect(NetworkEntity targetEntity, int duration)
    {
        EntityTracker targetTracker = targetEntity.GetComponent<EntityTracker>();
        if (targetTracker != null)
        {
            targetTracker.SetStunned(true);
        }
        
        // Also add as a status effect for duration tracking
        ProcessStatusEffect(null, targetEntity, "Stun", 1, duration);
    }
    
    private void ProcessLimitBreakEffect(NetworkEntity targetEntity)
    {
        EntityTracker targetTracker = targetEntity.GetComponent<EntityTracker>();
        if (targetTracker != null)
        {
            targetTracker.SetLimitBreak(true);
        }
        
        // Add as a status effect
        ProcessStatusEffect(null, targetEntity, "LimitBreak", 1, 3); // Default 3 turn duration
    }
    
    private void ProcessStrengthEffect(NetworkEntity targetEntity, int amount)
    {
        EntityTracker targetTracker = targetEntity.GetComponent<EntityTracker>();
        if (targetTracker != null)
        {
            targetTracker.AddStrength(amount);
        }
        
        // Also add as a status effect for display
        ProcessStatusEffect(null, targetEntity, "Strength", amount, 999); // Permanent until removed
    }
    
    private void ProcessStanceChangeEffect(NetworkEntity targetEntity, StanceType stance)
    {
        EntityTracker targetTracker = targetEntity.GetComponent<EntityTracker>();
        if (targetTracker != null)
        {
            targetTracker.SetStance(stance);
        }
    }
    
    private void ProcessElementalStatusEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, ElementalType elementalType, int potency, int duration)
    {
        string effectName = elementalType.ToString();
        ProcessStatusEffect(sourceEntity, targetEntity, effectName, potency, duration);
    }
    
    private void ProcessDiscardRandomCardsEffect(NetworkEntity targetEntity, int amount)
    {
        HandManager handManager = GetHandManagerForEntity(targetEntity);
        if (handManager != null)
        {
            List<GameObject> cardsInHand = handManager.GetCardsInHand();
            
            for (int i = 0; i < amount && cardsInHand.Count > 0; i++)
            {
                int randomIndex = Random.Range(0, cardsInHand.Count);
                GameObject cardToDiscard = cardsInHand[randomIndex];
                handManager.DiscardCard(cardToDiscard);
                cardsInHand.RemoveAt(randomIndex);
            }
        }
    }
    
    /// <summary>
    /// Gets the HandManager for an entity
    /// </summary>
    private HandManager GetHandManagerForEntity(NetworkEntity entity)
    {
        if (entity == null) return null;

        // Find the hand entity through RelationshipManager
        var relationshipManager = entity.GetComponent<RelationshipManager>();
        if (relationshipManager?.HandEntity == null) return null;

        var handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
        if (handEntity == null) return null;

        return handEntity.GetComponent<HandManager>();
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
    
    private NetworkEntity FindEntityById(int entityId)
    {
        NetworkObject netObj = null;
        
        if (IsServerInitialized)
        {
            FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(entityId, out netObj);
        }
        else if (IsClientInitialized)
        {
            FishNet.InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(entityId, out netObj);
        }
        
        return netObj?.GetComponent<NetworkEntity>();
    }
    
    /// <summary>
    /// Directly resolves card effects on the server for AI-controlled entities
    /// </summary>
    [Server]
    public void ServerResolveCardEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, CardData cardData)
    {
        if (!IsServerInitialized)
        {
            Debug.LogError("CardEffectResolver: Cannot resolve effect on server - server not initialized");
            return;
        }
        
        Debug.Log($"CardEffectResolver: ServerResolveCardEffect for card {cardData.CardName} from {sourceEntity.EntityName.Value} to {targetEntity.EntityName.Value}");
        
        // Create target list
        List<NetworkEntity> targetEntities = new List<NetworkEntity> { targetEntity };
        
        // Record card play
        RecordCardPlay(sourceEntity, cardData);
        
        // Process the effects directly since we're already on the server
        ProcessCardEffects(sourceEntity, targetEntities, cardData);
    }
} 