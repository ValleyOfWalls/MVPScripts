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
    
    [ServerRpc(RequireOwnership = false)]
    private void CmdResolveEffect(int sourceEntityId, int[] targetEntityIds, int cardDataId)
    {
        Debug.Log($"CARDPLAY_DEBUG: CmdResolveEffect called - Card: {cardDataId}, Source: {sourceEntityId}, Targets: [{string.Join(", ", targetEntityIds)}]");
        Debug.Log($"CARDPLAY_DEBUG: CardEffectResolver ownership - IsOwner: {IsOwner}, HasOwner: {Owner != null}, OwnerClientId: {(Owner != null ? Owner.ClientId : -1)}");
        
        // Find the source entity
        NetworkEntity sourceEntity = FindEntityById(sourceEntityId);
        if (sourceEntity == null)
        {
            Debug.LogError($"CARDPLAY_DEBUG: Could not find source entity with ID {sourceEntityId}");
            return;
        }
        
        Debug.Log($"CARDPLAY_DEBUG: Found source entity {sourceEntity.EntityName.Value} (ID: {sourceEntityId})");
        
        // Find all target entities
        List<NetworkEntity> targetEntities = new List<NetworkEntity>();
        foreach (int targetId in targetEntityIds)
        {
            NetworkEntity target = FindEntityById(targetId);
            if (target != null)
            {
                targetEntities.Add(target);
                Debug.Log($"CARDPLAY_DEBUG: Found target entity {target.EntityName.Value} (ID: {targetId})");
            }
            else
            {
                Debug.LogError($"CARDPLAY_DEBUG: Could not find target entity with ID {targetId}");
            }
        }
        
        if (targetEntities.Count == 0)
        {
            Debug.LogError($"CARDPLAY_DEBUG: Could not find any target entities");
            return;
        }
        
        // Get the card data from the database
        CardData cardData = CardDatabase.Instance.GetCardById(cardDataId);
        if (cardData == null)
        {
            Debug.LogError($"CARDPLAY_DEBUG: Could not find card data for ID {cardDataId}");
            return;
        }
        
        Debug.Log($"CARDPLAY_DEBUG: Found card data for {cardData.CardName}, processing effects...");
        
        // TRIGGER VISUAL EFFECTS BEFORE PROCESSING CARD EFFECTS
        if (targetEntities.Count > 0 && handleCardPlay != null)
        {
            Debug.Log($"CARDPLAY_DEBUG: Triggering visual effects for card {cardData.CardName}");
            // Use RPC to trigger visual effects on all clients
            RpcTriggerVisualEffects(sourceEntity.ObjectId, targetEntities[0].ObjectId, cardData.CardId);
        }
        else
        {
            Debug.LogWarning($"CARDPLAY_DEBUG: Cannot trigger visual effects - targetEntities.Count: {targetEntities.Count}, handleCardPlay: {handleCardPlay != null}");
        }
        
        // Record card play in tracking systems
        RecordCardPlay(sourceEntity, cardData);
        
        // Get tracking data for scaling calculations
        EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
        EntityTrackingData trackingData = sourceTracker?.GetTrackingDataForScaling() ?? new EntityTrackingData();
        
        // Process stance effects if card changes stance
        if (cardData.ChangesStance)
        {
            ProcessStanceChange(sourceEntity, cardData.NewStance);
        }
        
        // Process all card effects with scaling support
        ProcessCardEffects(sourceEntity, targetEntities, cardData);
        
        Debug.Log($"CARDPLAY_DEBUG: CmdResolveEffect completed for card {cardData.CardName}");
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
            Debug.Log($"CardEffectResolver: Processing {cardData.Effects.Count} effects for card {cardData.CardName}");
            
            // Separate conditional and non-conditional effects
            var conditionalEffects = cardData.Effects.Where(e => e.conditionType != ConditionalType.None).ToList();
            var nonConditionalEffects = cardData.Effects.Where(e => e.conditionType == ConditionalType.None).ToList();
            
            Debug.Log($"CardEffectResolver: Found {nonConditionalEffects.Count} non-conditional effects and {conditionalEffects.Count} conditional effects");
            
            // Process non-conditional effects first (these always execute)
            foreach (var effect in nonConditionalEffects)
            {
                Debug.Log($"CardEffectResolver: Processing non-conditional effect {effect.effectType} with amount {effect.amount}");
                ProcessSingleEffectWithScaling(sourceEntity, targetEntities, effect, trackingData);
            }
            
            // Process conditional effects with proper conditional logic
            foreach (var effect in conditionalEffects)
            {
                Debug.Log($"CardEffectResolver: Processing conditional effect {effect.effectType} with condition {effect.conditionType}");
                ProcessConditionalEffectSingle(sourceEntity, targetEntities, effect, trackingData);
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
    /// Processes a single effect with scaling and targeting
    /// </summary>
    private void ProcessSingleEffectWithScaling(NetworkEntity sourceEntity, List<NetworkEntity> targetEntities, CardEffect effect, EntityTrackingData trackingData)
    {
        /* Debug.Log($"CardEffectResolver: Processing effect {effect.effectType} with amount {effect.amount}, duration {effect.duration}, targetType {effect.targetType}"); */
        
        // Get the correct targets for this specific effect based on its targetType
        List<NetworkEntity> effectTargets = GetTargetsForEffect(sourceEntity, effect.targetType, targetEntities);
        
        if (effectTargets.Count == 0)
        {
            Debug.LogWarning($"CardEffectResolver: No valid targets found for effect {effect.effectType} with targetType {effect.targetType}");
            return;
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
    
    /// <summary>
    /// Processes a single conditional effect based on its condition and alternative logic
    /// </summary>
    private void ProcessConditionalEffectSingle(NetworkEntity sourceEntity, List<NetworkEntity> targetEntities, CardEffect effect, EntityTrackingData trackingData)
    {
        Debug.Log($"CardEffectResolver: Processing conditional effect {effect.effectType} with condition {effect.conditionType}, hasAlternativeEffect: {effect.hasAlternativeEffect}, alternativeLogic: {effect.alternativeLogic}");
        
        // Get the correct targets for this specific effect
        List<NetworkEntity> effectTargets = GetTargetsForEffect(sourceEntity, effect.targetType, targetEntities);
        
        if (effectTargets.Count == 0)
        {
            Debug.LogWarning($"CardEffectResolver: No valid targets found for conditional effect {effect.effectType} with targetType {effect.targetType}");
            return;
        }
        
        // Check the condition
        bool conditionMet = CheckConditionForEffect(sourceEntity, effectTargets, effect);
        Debug.Log($"CardEffectResolver: Condition {effect.conditionType} for effect {effect.effectType}: {(conditionMet ? "MET" : "NOT MET")}");
        
        if (conditionMet)
        {
            // Condition is met - execute alternative effect if using Replace logic, or both if using Additional logic
            if (effect.hasAlternativeEffect && effect.alternativeLogic == AlternativeEffectLogic.Replace)
            {
                // Replace logic: execute only alternative effect when condition is met
                Debug.Log($"CardEffectResolver: Condition MET with Replace logic - executing alternative effect {effect.alternativeEffectType} with amount {effect.alternativeEffectAmount} INSTEAD of main effect");
                ProcessSingleEffect(sourceEntity, effectTargets, effect.alternativeEffectType, effect.alternativeEffectAmount, 0, ElementalType.None);
            }
            else
            {
                // No alternative effect or Additional logic: execute main effect
                int mainAmount = effect.amount;
                
                // Apply scaling if the effect uses it
                if (effect.scalingType != ScalingType.None)
                {
                    mainAmount = CalculateScaledAmountFromEffect(effect, trackingData);
                }
                
                Debug.Log($"CardEffectResolver: Condition MET - executing main effect {effect.effectType} with amount {mainAmount}");
                ProcessSingleEffect(sourceEntity, effectTargets, effect.effectType, mainAmount, effect.duration, effect.elementalType);
                
                // If has alternative effect with "Additional" logic, also execute alternative
                if (effect.hasAlternativeEffect && effect.alternativeLogic == AlternativeEffectLogic.Additional)
                {
                    Debug.Log($"CardEffectResolver: Also executing additional alternative effect {effect.alternativeEffectType} with amount {effect.alternativeEffectAmount}");
                    ProcessSingleEffect(sourceEntity, effectTargets, effect.alternativeEffectType, effect.alternativeEffectAmount, 0, ElementalType.None);
                }
            }
        }
        else
        {
            // Condition not met - execute main effect (alternative only triggers when condition IS met)
            int mainAmount = effect.amount;
            
            // Apply scaling to main effect if it uses it
            if (effect.scalingType != ScalingType.None)
            {
                mainAmount = CalculateScaledAmountFromEffect(effect, trackingData);
            }
            
            Debug.Log($"CardEffectResolver: Condition NOT MET - executing main effect {effect.effectType} with amount {mainAmount}");
            ProcessSingleEffect(sourceEntity, effectTargets, effect.effectType, mainAmount, effect.duration, effect.elementalType);
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
    /// Checks if a conditional effect's condition is met
    /// </summary>
    private bool CheckConditionForEffect(NetworkEntity sourceEntity, List<NetworkEntity> targetEntities, CardEffect effect)
    {
        switch (effect.conditionType)
        {
            case ConditionalType.IfSourceHealthBelow:
                bool sourceHealthBelow = sourceEntity.CurrentHealth.Value < effect.conditionValue;
                Debug.Log($"CardEffectResolver: IfSourceHealthBelow - Current: {sourceEntity.CurrentHealth.Value}, Condition: < {effect.conditionValue}, Result: {sourceHealthBelow}");
                return sourceHealthBelow;
                
            case ConditionalType.IfSourceHealthAbove:
                bool sourceHealthAbove = sourceEntity.CurrentHealth.Value > effect.conditionValue;
                Debug.Log($"CardEffectResolver: IfSourceHealthAbove - Current: {sourceEntity.CurrentHealth.Value}, Condition: > {effect.conditionValue}, Result: {sourceHealthAbove}");
                return sourceHealthAbove;
                
            case ConditionalType.IfTargetHealthBelow:
                if (targetEntities.Count > 0)
                {
                    bool targetHealthBelow = targetEntities[0].CurrentHealth.Value < effect.conditionValue;
                    Debug.Log($"CardEffectResolver: IfTargetHealthBelow - Target: {targetEntities[0].EntityName.Value}, Current: {targetEntities[0].CurrentHealth.Value}, Condition: < {effect.conditionValue}, Result: {targetHealthBelow}");
                    return targetHealthBelow;
                }
                else
                {
                    Debug.Log($"CardEffectResolver: IfTargetHealthBelow - No target entities, returning false");
                    return false;
                }
                
            case ConditionalType.IfTargetHealthAbove:
                if (targetEntities.Count > 0)
                {
                    bool targetHealthAbove = targetEntities[0].CurrentHealth.Value > effect.conditionValue;
                    Debug.Log($"CardEffectResolver: IfTargetHealthAbove - Target: {targetEntities[0].EntityName.Value}, Current: {targetEntities[0].CurrentHealth.Value}, Condition: > {effect.conditionValue}, Result: {targetHealthAbove}");
                    return targetHealthAbove;
                }
                else
                {
                    Debug.Log($"CardEffectResolver: IfTargetHealthAbove - No target entities, returning false");
                    return false;
                }
                
            default:
                // For more complex conditions, delegate to tracking systems
                EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
                if (sourceTracker != null)
                {
                    bool trackingResult = sourceTracker.CheckCondition(effect.conditionType, effect.conditionValue);
                    Debug.Log($"CardEffectResolver: Delegated condition {effect.conditionType} to EntityTracker, result: {trackingResult}");
                    return trackingResult;
                }
                Debug.Log($"CardEffectResolver: Unknown condition type {effect.conditionType} and no EntityTracker, returning false");
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
        Debug.Log($"CardEffectResolver: ProcessDamageEffect - Amount: {amount}");
        
        // Use DamageCalculator for consistent damage calculation instead of duplicating the logic
        int finalDamage = 0;
        if (damageCalculator != null)
        {
            finalDamage = damageCalculator.CalculateDamage(sourceEntity, targetEntity, amount);
            Debug.Log($"CardEffectResolver: DamageCalculator returned final damage: {finalDamage}");
        }
        else
        {
            Debug.LogError($"CardEffectResolver: DamageCalculator not available, applying damage without modifiers!");
            finalDamage = amount;
        }
        
        // Apply the damage through LifeHandler
        LifeHandler targetLifeHandler = targetEntity.GetComponent<LifeHandler>();
        if (targetLifeHandler != null)
        {
            targetLifeHandler.TakeDamage(finalDamage, sourceEntity);
            
            // Record damage dealt in source entity tracker
            EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
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
        // Always use EnergyHandler for consistent server-authoritative energy management
        EnergyHandler energyHandler = targetEntity.GetComponent<EnergyHandler>();
        if (energyHandler != null)
        {
            energyHandler.AddEnergy(amount, null);
        }
        else
        {
            Debug.LogError($"CardEffectResolver: Target entity {targetEntity.EntityName.Value} has no EnergyHandler! Energy effects require EnergyHandler component.");
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
            
            // Notify upgrade manager about status effect application
            if (CardUpgradeManager.Instance != null)
            {
                Debug.Log($"[CARD_UPGRADE] CardEffectResolver: Notifying upgrade manager of status effect: {effectName} applied to {targetEntity.EntityName.Value}");
                CardUpgradeManager.Instance.OnStatusEffectApplied(targetEntity, effectName);
            }
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
    /// Server-side method to resolve card effects - redirects to main processing method
    /// </summary>
    [Server]
    public void ServerResolveCardEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, CardData cardData)
    {
        Debug.Log($"CARDPLAY_DEBUG: ServerResolveCardEffect called for card {cardData?.CardName}");
        
        if (!IsServerInitialized)
        {
            Debug.LogError($"CARDPLAY_DEBUG: ServerResolveCardEffect called but server not initialized");
            return;
        }
        
        if (sourceEntity == null || targetEntity == null || cardData == null)
        {
            Debug.LogError($"CARDPLAY_DEBUG: ServerResolveCardEffect called with null parameters - Source: {sourceEntity != null}, Target: {targetEntity != null}, CardData: {cardData != null}");
            return;
        }
        
        Debug.Log($"CARDPLAY_DEBUG: Routing to CmdResolveEffect - Source: {sourceEntity.EntityName.Value} (ID: {sourceEntity.ObjectId}), Target: {targetEntity.EntityName.Value} (ID: {targetEntity.ObjectId})");
        
        // Route through the main server processing method for consistency
        CmdResolveEffect(sourceEntity.ObjectId, new int[] { targetEntity.ObjectId }, cardData.CardId);
    }
} 