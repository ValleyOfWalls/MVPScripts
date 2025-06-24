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
        /* Debug.Log($"CardEffectResolver: ResolveCardEffect called for card {gameObject.name}"); */
        
        if (!IsOwner)
        {
            Debug.LogWarning($"CardEffectResolver: Cannot resolve effect, not network owner of card {gameObject.name}");
            return;
        }
        
        /* Debug.Log($"CardEffectResolver: Network ownership validated for card {gameObject.name}"); */
        
        // Make sure we have source and target entities
        var sourceEntity = sourceAndTargetIdentifier.SourceEntity;
        var allTargets = sourceAndTargetIdentifier.AllTargets;
        
        /* Debug.Log($"CardEffectResolver: Source entity: {(sourceEntity != null ? sourceEntity.EntityName.Value : "null")}, Target count: {allTargets?.Count ?? 0}"); */
        
        if (sourceEntity == null || allTargets.Count == 0)
        {
            Debug.LogError($"CardEffectResolver: Missing source or target entities for card {gameObject.name} - Source: {sourceEntity != null}, Targets: {allTargets?.Count ?? 0}");
            return;
        }
        
        // Get the card data
        CardData cardData = card.CardData;
        if (cardData == null)
        {
            Debug.LogError($"CardEffectResolver: No card data for card {gameObject.name}");
            return;
        }
        
        /* Debug.Log($"CardEffectResolver: Found card data for {cardData.CardName}"); */
        
        // Get tracking data for combo check
        EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
        
        // Check sequence requirements (combo system)
        bool canPlay = cardData.CanPlayWithCombo(sourceTracker?.ComboCount ?? 0);
        /* Debug.Log($"CardEffectResolver: Combo check - Can play: {canPlay}, Combo count: {sourceTracker?.ComboCount ?? 0}, Required: {cardData.RequiredComboAmount}"); */
        
        if (!canPlay)
        {
            /* Debug.Log($"CardEffectResolver: Card {cardData.CardName} cannot be played - combo requirement not met"); */
            return;
        }
        
        // Execute the card effect on the server
        List<int> targetIds = new List<int>();
        foreach (var target in allTargets)
        {
            if (target != null)
            {
                targetIds.Add(target.ObjectId);
                /* Debug.Log($"CardEffectResolver: Added target {target.EntityName.Value} (ID: {target.ObjectId}) to target list"); */
            }
        }
        
        /* Debug.Log($"CardEffectResolver: Calling CmdResolveEffect with source {sourceEntity.ObjectId}, {targetIds.Count} targets, card {cardData.CardId}"); */
        CmdResolveEffect(sourceEntity.ObjectId, targetIds.ToArray(), cardData.CardId);
    }
    
    [ServerRpc]
    private void CmdResolveEffect(int sourceEntityId, int[] targetEntityIds, int cardDataId)
    {
        /* Debug.Log($"CardEffectResolver: CmdResolveEffect called - Card: {cardDataId}, Source: {sourceEntityId}, Targets: [{string.Join(", ", targetEntityIds)}]"); */
        
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
            {
                targetEntities.Add(target);
                Debug.Log($"CardEffectResolver: Found target entity {target.EntityName.Value} (ID: {targetId})");
            }
            else
            {
                Debug.LogError($"CardEffectResolver: Could not find target entity with ID {targetId}");
            }
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
        
        Debug.Log($"CardEffectResolver: Found card data for {cardData.CardName}, checking if should trigger visual effects...");
        
        // TRIGGER VISUAL EFFECTS BEFORE PROCESSING CARD EFFECTS
        if (targetEntities.Count > 0 && handleCardPlay != null)
        {
            Debug.Log($"CardEffectResolver: Triggering visual effects for card {cardData.CardName} - calling RpcTriggerVisualEffects");
            // Use RPC to trigger visual effects on all clients
            RpcTriggerVisualEffects(sourceEntity.ObjectId, targetEntities[0].ObjectId, cardData.CardId);
        }
        else
        {
            Debug.LogWarning($"CardEffectResolver: Cannot trigger visual effects - targetEntities.Count: {targetEntities.Count}, handleCardPlay: {handleCardPlay != null}");
        }
        
        // Record card play in tracking systems
        RecordCardPlay(sourceEntity, cardData);
        
        // Process the effect based on the card type
        ProcessCardEffects(sourceEntity, targetEntities, cardData);
        
        /* Debug.Log($"CardEffectResolver: CmdResolveEffect completed for card {cardData.CardName}"); */
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
            entityTracker.RecordCardPlayed(cardData.CardId, cardData.BuildsCombo, cardData.CardType, cardData.IsZeroCost);
        }
        
        // Record in card tracker if available
        if (cardTracker != null)
        {
            cardTracker.RecordCardPlayed();
        }
    }
    
    /// <summary>
    /// Processes all effects for a card using the clean Effects system
    /// </summary>
    private void ProcessCardEffects(NetworkEntity sourceEntity, List<NetworkEntity> targetEntities, CardData cardData)
    {
        // Get tracking data for scaling calculations
        EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
        EntityTrackingData trackingData = sourceTracker?.GetTrackingDataForScaling() ?? new EntityTrackingData();
        
        // Process stance effects if card changes stance
        if (cardData.ChangesStance)
        {
            ProcessStanceChange(sourceEntity, cardData.NewStance);
        }
        
        // Process all effects from the Effects list
        if (cardData.HasEffects)
        {
            /* Debug.Log($"CardEffectResolver: Processing {cardData.Effects.Count} effects for card {cardData.CardName}"); */
            
            foreach (var effect in cardData.Effects)
            {
                /* Debug.Log($"CardEffectResolver: Processing effect {effect.effectType} with amount {effect.amount}, duration {effect.duration}, targetType {effect.targetType}"); */
                
                // Get the correct targets for this specific effect based on its targetType
                List<NetworkEntity> effectTargets = GetTargetsForEffect(sourceEntity, effect.targetType, targetEntities);
                
                if (effectTargets.Count == 0)
                {
                    Debug.LogWarning($"CardEffectResolver: No valid targets found for effect {effect.effectType} with targetType {effect.targetType}");
                    continue;
                }
                
                /* Debug.Log($"CardEffectResolver: Effect {effect.effectType} targeting {effectTargets.Count} entities: {string.Join(", ", effectTargets.Select(e => e.EntityName.Value))}"); */
                
                // Apply scaling if defined on the effect
                int amount = effect.amount;
                if (effect.scalingType != ScalingType.None && trackingData != null)
                {
                    int scalingValue = GetScalingValue(effect.scalingType, trackingData);
                    int scalingBonus = Mathf.FloorToInt(scalingValue * effect.scalingMultiplier);
                    int scaledAmount = amount + scalingBonus;
                    
                    /* Debug.Log($"CardEffectResolver: Scaling details - scalingType: {effect.scalingType}, scalingValue: {scalingValue}, multiplier: {effect.scalingMultiplier}, bonus: {scalingBonus}, maxScaling: {effect.maxScaling}"); */
                    
                    // Only apply max scaling if it's higher than the base amount
                    // This prevents maxScaling from reducing the base effect
                    if (effect.maxScaling > effect.amount)
                    {
                        amount = Mathf.Min(scaledAmount, effect.maxScaling);
                        /* Debug.Log($"CardEffectResolver: Applied max scaling cap - final amount: {amount}"); */
                    }
                    else
                    {
                        amount = scaledAmount;
                        /* Debug.Log($"CardEffectResolver: Max scaling ({effect.maxScaling}) lower than base amount ({effect.amount}), ignoring cap - final amount: {amount}"); */
                    }
                    
                    /* Debug.Log($"CardEffectResolver: Scaling applied - original: {effect.amount}, scaled: {amount}"); */
                }
                else if (effect.scalingType != ScalingType.None)
                {
                    /* Debug.Log($"CardEffectResolver: Effect has scaling type {effect.scalingType} but no tracking data available"); */
                }
                
                /* Debug.Log($"CardEffectResolver: Calling ProcessSingleEffect with amount {amount}, duration {effect.duration}"); */
                ProcessSingleEffect(sourceEntity, effectTargets, effect.effectType, amount, effect.duration, effect.elementalType);
            }
        }
        else
        {
            Debug.LogWarning($"CardEffectResolver: Card {cardData.CardName} has no effects defined");
        }
    }
    
    /// <summary>
    /// Processes stance change effect
    /// </summary>
    private void ProcessStanceChange(NetworkEntity sourceEntity, StanceType newStance)
    {
        EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
        if (sourceTracker != null)
        {
            sourceTracker.SetStance(newStance);
            Debug.Log($"CardEffectResolver: {sourceEntity.EntityName.Value} entered {newStance} stance");
        }
    }
    
    /// <summary>
    /// Calculate scaling value based on tracking data
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
            case ScalingType.MissingHealth:
                // This would need current health calculation - simplified for now
                return 0;
            default:
                return 0;
        }
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
            case CardTargetType.Opponent:
                // Find the opponent for this source entity
                FightManager fightManager = FindFirstObjectByType<FightManager>();
                if (fightManager != null)
                {
                    NetworkEntity opponent = null;
                    if (sourceEntity.EntityType == EntityType.Player)
                    {
                        opponent = fightManager.GetOpponentForPlayer(sourceEntity);
                    }
                    else if (sourceEntity.EntityType == EntityType.Pet)
                    {
                        opponent = fightManager.GetOpponentForPet(sourceEntity);
                    }
                    
                    if (opponent != null)
                    {
                        effectTargets.Add(opponent);
                    }
                    else
                    {
                        Debug.LogWarning($"CardEffectResolver: Could not find opponent for {sourceEntity.EntityName.Value} ({sourceEntity.EntityType})");
                    }
                }
                else
                {
                    Debug.LogError($"CardEffectResolver: FightManager not found, cannot determine opponent for effect targeting");
                }
                break;
            case CardTargetType.Random:
                // Get all possible targets and pick one randomly
                List<NetworkEntity> allTargets = GetAllPossibleTargetsForEntity(sourceEntity);
                if (allTargets.Count > 0)
                {
                    int randomIndex = UnityEngine.Random.Range(0, allTargets.Count);
                    effectTargets.Add(allTargets[randomIndex]);
                }
                break;
            default:
                // Use original targets for any other types (should be rare)
                effectTargets.AddRange(originalTargets);
                Debug.LogWarning($"CardEffectResolver: Unhandled targetType {targetType}, using originalTargets");
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
                
                // Always execute the main effect when condition is met
                ProcessSingleEffect(sourceEntity, targetEntities, effect.effectType, 
                    effectAmount, effect.duration, effect.elementalType);
                
                // If has alternative effect with "Additional" logic, also execute alternative
                if (effect.hasAlternativeEffect && effect.alternativeLogic == AlternativeEffectLogic.Additional)
                {
                    ProcessSingleEffect(sourceEntity, targetEntities, effect.alternativeEffectType, 
                        effect.alternativeEffectAmount, 0, ElementalType.None);
                    /* Debug.Log($"CardEffectResolver: Conditional met - executed both main effect ({effect.effectType}) AND alternative effect ({effect.alternativeEffectType})"); */
                }
            }
            else if (effect.hasAlternativeEffect)
            {
                // Condition not met - handle based on logic type
                if (effect.alternativeLogic == AlternativeEffectLogic.Replace)
                {
                    // Replace logic: execute only alternative effect
                    ProcessSingleEffect(sourceEntity, targetEntities, effect.alternativeEffectType, 
                        effect.alternativeEffectAmount, 0, ElementalType.None);
                    /* Debug.Log($"CardEffectResolver: Conditional failed - executed alternative effect ({effect.alternativeEffectType}) INSTEAD of main effect"); */
                }
                else if (effect.alternativeLogic == AlternativeEffectLogic.Additional)
                {
                    // Additional logic: execute main effect + alternative effect
                    int effectAmount = effect.amount;
                    
                    // Apply scaling to main effect if it uses it
                    if (effect.scalingType != ScalingType.None)
                    {
                        effectAmount = CalculateScaledAmountFromEffect(effect, trackingData);
                    }
                    
                    ProcessSingleEffect(sourceEntity, targetEntities, effect.effectType, 
                        effectAmount, effect.duration, effect.elementalType);
                    ProcessSingleEffect(sourceEntity, targetEntities, effect.alternativeEffectType, 
                        effect.alternativeEffectAmount, 0, ElementalType.None);
                    /* Debug.Log($"CardEffectResolver: Conditional failed - executed main effect ({effect.effectType}) AND alternative effect ({effect.alternativeEffectType})"); */
                }
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
        /* Debug.Log($"CardEffectResolver: ProcessSingleEffect called with type={effectType}, amount={amount}, duration={duration}"); */
        
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
                    
                case CardEffectType.ApplyBurn:
                    ProcessStatusEffect(sourceEntity, targetEntity, "Burn", amount, 999); // High duration since it ticks down by potency
                    break;
                    
                case CardEffectType.ApplySalve:
                    ProcessStatusEffect(sourceEntity, targetEntity, "Salve", amount, 999); // High duration since it ticks down by potency
                    break;
                    
                case CardEffectType.RaiseCriticalChance:
                    ProcessStatusEffect(sourceEntity, targetEntity, "CriticalUp", amount, duration);
                    break;
                    
                case CardEffectType.ApplyThorns:
                    /* Debug.Log($"CardEffectResolver: ApplyThorns case - passing amount={amount}, duration={duration}"); */
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
                    
                case CardEffectType.ApplyCurse:
                    ProcessStatusEffect(sourceEntity, targetEntity, "Curse", amount, duration);
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
        /* Debug.Log($"CardEffectResolver: Processing damage for card '{originalCardData.CardName}'"); */
        /* Debug.Log($"CardEffectResolver: Input amount parameter: {amount}"); */
        /* Debug.Log($"CardEffectResolver: Strength bonus: {strengthBonus}"); */
        /* Debug.Log($"CardEffectResolver: Total damage calculation: {totalDamage}"); */
        
        if (originalCardData.HasEffects && originalCardData.Effects.Count > 0)
        {
            var firstEffect = originalCardData.Effects[0];
            /* Debug.Log($"CardEffectResolver: First effect type: {firstEffect.effectType}, amount: {firstEffect.amount}"); */
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
        
        /* Debug.Log($"CardEffectResolver: Final damage to apply: {finalDamage}"); */
        
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
        /* Debug.Log($"CardEffectResolver: ProcessStatusEffect called with effectName={effectName}, potency={potency}, duration={duration}"); */
        
        EffectHandler targetEffectHandler = targetEntity.GetComponent<EffectHandler>();
        if (targetEffectHandler != null)
        {
            Debug.Log($"CardEffectResolver: Calling EffectHandler.AddEffect with potency={potency}");
            targetEffectHandler.AddEffect(effectName, potency, duration, sourceEntity);
        }
        else
        {
            Debug.LogError($"CardEffectResolver: No EffectHandler found on target {targetEntity.EntityName.Value}");
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
    
    /// <summary>
    /// Gets all possible target entities for random targeting
    /// </summary>
    private List<NetworkEntity> GetAllPossibleTargetsForEntity(NetworkEntity sourceEntity)
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
        FightManager fightManager = FindFirstObjectByType<FightManager>();
        if (fightManager != null)
        {
            NetworkEntity opponent = null;
            if (sourceEntity.EntityType == EntityType.Player)
            {
                opponent = fightManager.GetOpponentForPlayer(sourceEntity);
            }
            else if (sourceEntity.EntityType == EntityType.Pet)
            {
                opponent = fightManager.GetOpponentForPet(sourceEntity);
            }
            
            if (opponent != null)
                allTargets.Add(opponent);
        }

        return allTargets;
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
    /// RPC to trigger visual effects on all clients
    /// </summary>
    [ObserversRpc]
    private void RpcTriggerVisualEffects(int sourceEntityId, int targetEntityId, int cardDataId)
    {
        Debug.Log($"CardEffectResolver: RpcTriggerVisualEffects called - Source: {sourceEntityId}, Target: {targetEntityId}, Card: {cardDataId}");
        
        // Find entities
        NetworkEntity sourceEntity = FindEntityById(sourceEntityId);
        NetworkEntity targetEntity = FindEntityById(targetEntityId);
        
        Debug.Log($"CardEffectResolver: Entity lookup results - Source: {(sourceEntity != null ? sourceEntity.EntityName.Value : "null")}, Target: {(targetEntity != null ? targetEntity.EntityName.Value : "null")}");
        
        if (sourceEntity == null || targetEntity == null)
        {
            Debug.LogError($"CardEffectResolver: Could not find entities for visual effects - Source: {sourceEntity != null}, Target: {targetEntity != null}");
            return;
        }
        
        // Check if these entities are in the currently viewed fight
        bool shouldShow = ShouldShowVisualEffectsForEntities(sourceEntity, targetEntity);
        Debug.Log($"CardEffectResolver: ShouldShowVisualEffectsForEntities result: {shouldShow}");
        
        if (!shouldShow)
        {
            Debug.Log($"CardEffectResolver: Skipping visual effects - entities not in currently viewed fight");
            return;
        }
        
        // Get card data
        CardData cardData = CardDatabase.Instance.GetCardById(cardDataId);
        if (cardData == null)
        {
            Debug.LogError($"CardEffectResolver: Could not find card data for visual effects - ID: {cardDataId}");
            return;
        }
        
        Debug.Log($"CardEffectResolver: Found card data for {cardData.CardName}, checking handleCardPlay...");
        
        // Trigger visual effects
        if (handleCardPlay != null)
        {
            Debug.Log($"CardEffectResolver: Calling TriggerVisualEffects for card {cardData.CardName} - Source: {sourceEntity.EntityName.Value}, Target: {targetEntity.EntityName.Value}");
            handleCardPlay.TriggerVisualEffects(sourceEntity, targetEntity, cardData);
            Debug.Log($"CardEffectResolver: TriggerVisualEffects call completed for card {cardData.CardName}");
        }
        else
        {
            Debug.LogError($"CardEffectResolver: handleCardPlay is null, cannot trigger visual effects");
        }
    }
    
    /// <summary>
    /// Checks if visual effects should be shown for the given entities using centralized EntityVisibilityManager
    /// </summary>
    private bool ShouldShowVisualEffectsForEntities(NetworkEntity sourceEntity, NetworkEntity targetEntity)
    {
        if (sourceEntity == null || targetEntity == null)
        {
            Debug.LogWarning($"CardEffectResolver: Source or target entity is null, skipping visual effects");
            return false;
        }
        
        // Use centralized visibility management from EntityVisibilityManager
        EntityVisibilityManager visibilityManager = EntityVisibilityManager.Instance;
        if (visibilityManager == null)
        {
            Debug.LogWarning($"CardEffectResolver: No EntityVisibilityManager instance found, allowing visual effects");
            return true;
        }
        
        return visibilityManager.ShouldShowVisualEffectsForEntities((uint)sourceEntity.ObjectId, (uint)targetEntity.ObjectId);
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
        
        // TRIGGER VISUAL EFFECTS BEFORE PROCESSING CARD EFFECTS (same as CmdResolveEffect)
        if (targetEntities.Count > 0 && handleCardPlay != null)
        {
            Debug.Log($"CardEffectResolver: ServerResolveCardEffect - Triggering visual effects for card {cardData.CardName} - calling RpcTriggerVisualEffects");
            // Use RPC to trigger visual effects on all clients
            RpcTriggerVisualEffects(sourceEntity.ObjectId, targetEntities[0].ObjectId, cardData.CardId);
        }
        else
        {
            Debug.LogWarning($"CardEffectResolver: ServerResolveCardEffect - Cannot trigger visual effects - targetEntities.Count: {targetEntities.Count}, handleCardPlay: {handleCardPlay != null}");
        }
        
        // Record card play
        RecordCardPlay(sourceEntity, cardData);
        
        // Process the effects directly since we're already on the server
        ProcessCardEffects(sourceEntity, targetEntities, cardData);
    }
} 