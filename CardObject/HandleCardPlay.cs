using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems; // Required for IPointerClickHandler
using UnityEngine.UI; // Required for Button

/// <summary>
/// Enhanced handler for card play with stun checking and sequence validation.
/// Attach to: Card prefabs alongside Card, CardEffectResolver, and SourceAndTargetIdentifier.
/// </summary>
public class HandleCardPlay : NetworkBehaviour
{
    [Header("Card Components")]
    [SerializeField] private Card card;
    [SerializeField] private CardEffectResolver cardEffectResolver;
    [SerializeField] private SourceAndTargetIdentifier sourceAndTargetIdentifier;

    [Header("Debug")]
    [SerializeField, ReadOnly] private bool canPlay = true;
    [SerializeField, ReadOnly] private string playBlockReason = "";

    private CardData cardData;
    
    // Flag to prevent double processing of the same card play
    private bool isProcessingCardPlay = false;

    private void Awake()
    {
        // Get required component references
        if (card == null) card = GetComponent<Card>();
        if (cardEffectResolver == null) cardEffectResolver = GetComponent<CardEffectResolver>();
        if (sourceAndTargetIdentifier == null) sourceAndTargetIdentifier = GetComponent<SourceAndTargetIdentifier>();
        
        ValidateComponents();
    }

    private void ValidateComponents()
    {
        if (card == null)
            Debug.LogError($"HandleCardPlay on {gameObject.name}: Missing Card component!");
        
        if (cardEffectResolver == null)
            Debug.LogError($"HandleCardPlay on {gameObject.name}: Missing CardEffectResolver component!");
        
        if (sourceAndTargetIdentifier == null)
            Debug.LogError($"HandleCardPlay on {gameObject.name}: Missing SourceAndTargetIdentifier component!");
    }

    public void Initialize(CardData data)
    {
        ValidateComponents();
        cardData = data;
        if (sourceAndTargetIdentifier != null)
        {
            sourceAndTargetIdentifier.UpdateSourceAndTarget();
        }
    }

    /// <summary>
    /// Called when the player attempts to play this card
    /// </summary>
    public void OnCardPlayAttempt()
    {
        if (!CanPlayCard())
        {
            Debug.LogWarning($"HandleCardPlay: Cannot play card {card.CardData?.CardName}. Reason: {playBlockReason}");
            return;
        }

        Debug.Log($"HandleCardPlay: Playing card {card.CardData?.CardName}");
        
        // Check if TestCombat return-to-hand mode is enabled
        #if UNITY_EDITOR
        if (TestCombat.Instance != null && TestCombat.Instance.ShouldReturnToHand(gameObject))
        {
            // In return-to-hand mode, still process the effects but return the card to hand afterwards
            if (cardEffectResolver != null)
            {
                cardEffectResolver.ResolveCardEffect();
            }
            
            // Schedule the card to return to hand after a short delay
            StartCoroutine(ReturnToHandAfterDelay());
            return;
        }
        #endif
        
        // Normal card play processing
        if (cardEffectResolver != null)
        {
            cardEffectResolver.ResolveCardEffect();
        }
        else
        {
            Debug.LogError("HandleCardPlay: Cannot resolve effect - missing CardEffectResolver");
        }
        
        // After resolving effects, handle card cleanup (deduct energy and discard)
        ProcessCardPlayCleanup();
    }

    /// <summary>
    /// Checks if the card can be played based on various conditions
    /// </summary>
    private bool CanPlayCard()
    {
        canPlay = true;
        playBlockReason = "";

        // Check if we have valid card data
        if (card?.CardData == null)
        {
            canPlay = false;
            playBlockReason = "No card data";
            return false;
        }

        // Check if we have source entity
        var sourceEntity = sourceAndTargetIdentifier?.SourceEntity;
        if (sourceEntity == null)
        {
            canPlay = false;
            playBlockReason = "No source entity";
            return false;
        }

        // Check stun status
        EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
        if (sourceTracker != null && sourceTracker.IsStunned)
        {
            canPlay = false;
            playBlockReason = "Source entity is stunned";
            return false;
        }

        // Check sequence requirements
        if (sourceTracker != null)
        {
            CardData cardData = card.CardData;
            bool sequenceValid = cardData.CanPlayWithCombo(sourceTracker.ComboCount);

            if (!sequenceValid)
            {
                canPlay = false;
                playBlockReason = $"Requires {cardData.RequiredComboAmount} combo (have {sourceTracker.ComboCount})";
                return false;
            }
        }

        // Check if we have valid targets
        var allTargets = sourceAndTargetIdentifier?.AllTargets;
        if (allTargets == null || allTargets.Count == 0)
        {
            canPlay = false;
            playBlockReason = "No valid targets";
            return false;
        }

        // Check energy cost
        if (sourceEntity.CurrentEnergy.Value < card.CardData.EnergyCost)
        {
            canPlay = false;
            playBlockReason = $"Not enough energy (need {card.CardData.EnergyCost}, have {sourceEntity.CurrentEnergy.Value})";
            return false;
        }

        // Check combo requirements
        if (card.CardData.RequiresCombo)
        {
            bool sequenceValid = card.CardData.CanPlayWithCombo(sourceTracker.ComboCount);
            if (!sequenceValid)
            {
                Debug.Log($"HandleCardPlay: Cannot play {card.CardData.CardName} - combo requirement not met (have {sourceTracker.ComboCount}, need {card.CardData.RequiredComboAmount})");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Public method to check if card can be played (for UI feedback)
    /// </summary>
    public bool CanCardBePlayed()
    {
        return CanPlayCard();
    }

    /// <summary>
    /// Gets the reason why the card cannot be played (for UI feedback)
    /// </summary>
    public string GetPlayBlockReason()
    {
        CanPlayCard(); // Update the reason
        return playBlockReason;
    }

    /// <summary>
    /// Checks if the card meets sequence requirements (combo system)
    /// </summary>
    public bool MeetsSequenceRequirements()
    {
        var sourceEntity = sourceAndTargetIdentifier?.SourceEntity;
        if (sourceEntity == null || card?.CardData == null)
            return false;

        EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
        if (sourceTracker == null)
            return true; // No tracker means no restrictions

        return card.CardData.CanPlayWithCombo(sourceTracker.ComboCount);
    }

    /// <summary>
    /// Gets a description of what this card needs to be played
    /// </summary>
    public string GetCardRequirementsDescription()
    {
        if (card?.CardData == null)
            return "No card data";

        string description = $"Cost: {card.CardData.EnergyCost} energy";

        // Add combo information
        if (card.CardData.BuildsCombo)
        {
            description += "\nBuilds combo";
        }

        if (card.CardData.RequiresCombo)
        {
            description += $"\nRequires: {card.CardData.RequiredComboAmount} combo";
        }

        // Add stance information
        if (card.CardData.ChangesStance)
        {
            description += $"\nChanges stance to {card.CardData.NewStance}";
        }

        return description;
    }

    /// <summary>
    /// Server method to validate and process card play
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void ServerPlayCard(NetworkConnection conn = null)
    {
        // Validate on server side as well
        if (!CanPlayCard())
        {
            Debug.LogWarning($"HandleCardPlay: Server rejected card play. Reason: {playBlockReason}");
            ClientRejectCardPlay(conn, playBlockReason);
            return;
        }

        // FIXED: Deduct energy cost BEFORE processing effects (was happening after)
        // This ensures restore energy effects work correctly with the proper order
        if (sourceAndTargetIdentifier?.SourceEntity != null && card?.CardData != null)
        {
            var sourceEntity = sourceAndTargetIdentifier.SourceEntity;
            int energyCost = card.CardData.EnergyCost;
            
            // Deduct energy cost first
            sourceEntity.ChangeEnergy(-energyCost);
            Debug.Log($"HandleCardPlay: Deducted {energyCost} energy from {sourceEntity.EntityName.Value} before applying effects");
        }

        // Process the card effect AFTER energy deduction
        Debug.Log($"HandleCardPlay: Processing card effects - cardEffectResolver: {cardEffectResolver != null}");
        
        if (cardEffectResolver != null)
        {
            // Since we're already on server, call the effect resolver directly
            var sourceEntity = sourceAndTargetIdentifier.SourceEntity;
            var targetEntity = sourceAndTargetIdentifier.AllTargets.Count > 0 ? sourceAndTargetIdentifier.AllTargets[0] : null;
            
            Debug.Log($"HandleCardPlay: Entities - Source: {sourceEntity?.EntityName.Value}, Target: {targetEntity?.EntityName.Value}, CardData: {card?.CardData?.CardName}");
            
            if (sourceEntity != null && targetEntity != null)
            {
                // Check if this card should trigger visual effects
                Debug.Log($"HandleCardPlay: Checking if card {card.CardData?.CardName} should trigger visual effects...");
                if (ShouldTriggerVisualEffects(card.CardData))
                {
                    Debug.Log($"HandleCardPlay: Card {card.CardData?.CardName} SHOULD trigger visual effects");
                    TriggerVisualEffects(sourceEntity, targetEntity, card.CardData);
                }
                else
                {
                    Debug.Log($"HandleCardPlay: Card {card.CardData?.CardName} should NOT trigger visual effects");
                }
                
                Debug.Log($"HandleCardPlay: Calling cardEffectResolver.ServerResolveCardEffect");
                cardEffectResolver.ServerResolveCardEffect(sourceEntity, targetEntity, card.CardData);
            }
            else
            {
                Debug.LogError($"HandleCardPlay: Missing entities - Source: {sourceEntity != null}, Target: {targetEntity != null}");
            }
        }
        else
        {
            Debug.LogError($"HandleCardPlay: cardEffectResolver is null!");
        }
        
        // Handle card discarding after effects are processed
        if (sourceAndTargetIdentifier?.SourceEntity != null && card?.CardData != null)
        {
            var sourceEntity = sourceAndTargetIdentifier.SourceEntity;
            
            // Find the hand manager and discard the card
            HandManager handManager = GetHandManagerForEntity(sourceEntity);
            if (handManager != null)
            {
                handManager.DiscardCard(gameObject);
                Debug.Log($"HandleCardPlay: Discarded card {card?.CardData?.CardName} to discard pile");
            }
            else
            {
                Debug.LogError($"HandleCardPlay: Could not find HandManager for entity {sourceEntity.EntityName.Value}");
            }
        }
    }

    /// <summary>
    /// Client notification that card play was rejected
    /// </summary>
    [TargetRpc]
    private void ClientRejectCardPlay(NetworkConnection conn, string reason)
    {
        Debug.LogWarning($"HandleCardPlay: Card play rejected by server. Reason: {reason}");
        
        // Could trigger UI feedback here
        // e.g., show error message, play sound, etc.
    }

    /// <summary>
    /// Called by the card system when it's this entity's turn
    /// </summary>
    public void OnTurnStart()
    {
        // Reset any turn-based restrictions
        canPlay = true;
        playBlockReason = "";
        
        // Check if still stunned
        var sourceEntity = sourceAndTargetIdentifier?.SourceEntity;
        if (sourceEntity != null)
        {
            EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
            if (sourceTracker != null && sourceTracker.IsStunned)
            {
                Debug.Log($"HandleCardPlay: {sourceEntity.EntityName.Value} is stunned and cannot play cards this turn");
            }
        }
    }

    /// <summary>
    /// Called when the turn ends
    /// </summary>
    public void OnTurnEnd()
    {
        // Any cleanup needed at turn end
        canPlay = false;
        playBlockReason = "Not your turn";
    }

    /// <summary>
    /// Forces an update of the play validation
    /// </summary>
    public void UpdatePlayValidation()
    {
        CanPlayCard();
    }

    /// <summary>
    /// Handles the cleanup after a card is played (energy cost, discarding)
    /// </summary>
    private void ProcessCardPlayCleanup()
    {
        if (!IsOwner)
        {
            Debug.LogWarning($"HandleCardPlay: Cannot process cleanup, not network owner of card {gameObject.name}");
            return;
        }
        
        // Call server to handle the cleanup
        var sourceEntity = sourceAndTargetIdentifier?.SourceEntity;
        if (sourceEntity != null && card?.CardData != null)
        {
            CmdProcessCardPlayCleanup(sourceEntity.ObjectId, card.CardData.EnergyCost);
        }
    }
    
    /// <summary>
    /// Server RPC to handle energy deduction and card discarding
    /// </summary>
    [ServerRpc]
    private void CmdProcessCardPlayCleanup(int sourceEntityId, int energyCost)
    {
        // Find the source entity
        NetworkEntity sourceEntity = FindEntityById(sourceEntityId);
        if (sourceEntity == null)
        {
            Debug.LogError($"HandleCardPlay: Could not find source entity with ID {sourceEntityId}");
            return;
        }
        
        // Deduct energy cost
        sourceEntity.ChangeEnergy(-energyCost);
        Debug.Log($"HandleCardPlay: Deducted {energyCost} energy from {sourceEntity.EntityName.Value}");
        
        // Find the hand manager and discard the card
        HandManager handManager = GetHandManagerForEntity(sourceEntity);
        if (handManager != null)
        {
            handManager.DiscardCard(gameObject);
            Debug.Log($"HandleCardPlay: Discarded card {card?.CardData?.CardName} to discard pile");
        }
        else
        {
            Debug.LogError($"HandleCardPlay: Could not find HandManager for entity {sourceEntity.EntityName.Value}");
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
    /// Helper method to find entity by ID
    /// </summary>
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
    /// Checks if a card should trigger visual attack effects based on its effects
    /// </summary>
    private bool ShouldTriggerVisualEffects(CardData cardData)
    {
        if (cardData == null)
        {
            Debug.Log("HandleCardPlay: CardData is null");
            return false;
        }
        
        if (!cardData.HasEffects)
        {
            Debug.Log($"HandleCardPlay: Card {cardData.CardName} has no effects");
            return false;
        }
        
        Debug.Log($"HandleCardPlay: Card {cardData.CardName} has {cardData.Effects.Count} effects, checking each...");
        
        // Check each effect to see if any need visual effects
        foreach (var effect in cardData.Effects)
        {
            Debug.Log($"HandleCardPlay: Checking effect {effect.effectType} with behavior {effect.animationBehavior}");
            if (ShouldEffectTriggerVisual(effect, cardData))
            {
                Debug.Log($"HandleCardPlay: Effect {effect.effectType} SHOULD trigger visual");
                return true;
            }
            else
            {
                Debug.Log($"HandleCardPlay: Effect {effect.effectType} should NOT trigger visual");
            }
        }
        
        Debug.Log($"HandleCardPlay: No effects on card {cardData.CardName} need visual effects");
        return false;
    }
    
    /// <summary>
    /// Checks if a specific effect should trigger visual animation
    /// </summary>
    private bool ShouldEffectTriggerVisual(CardEffect effect, CardData cardData)
    {
        // Check explicit animation behavior first
        switch (effect.animationBehavior)
        {
            case EffectAnimationBehavior.None:
                return false;
            case EffectAnimationBehavior.InstantOnTarget:
            case EffectAnimationBehavior.ProjectileFromSource:
            case EffectAnimationBehavior.OnSourceOnly:
            case EffectAnimationBehavior.AreaEffect:
            case EffectAnimationBehavior.BeamToTarget:
                return true;
            case EffectAnimationBehavior.Auto:
                // Fall through to auto-detection
                break;
        }
        
        // Auto-detection based on effect type
        switch (effect.effectType)
        {
            case CardEffectType.Damage:
            case CardEffectType.ApplyWeak:
            case CardEffectType.ApplyBreak:
            case CardEffectType.ApplyStun:
            case CardEffectType.ApplyCurse:
            case CardEffectType.ApplyDamageOverTime:
                return true;
            default:
                return false;
        }
    }
    
    /// <summary>
    /// Determines attack type for a specific effect
    /// </summary>
    private AttackEffectSource.AttackType DetermineAttackType(CardEffect effect, CardData cardData)
    {
        if (effect == null || cardData == null) return AttackEffectSource.AttackType.Default;
        
        // Check if effect has a fallback attack type
        if (effect.animationBehavior == EffectAnimationBehavior.ProjectileFromSource)
        {
            return effect.fallbackAttackType;
        }
        
        // Auto-detect based on effect type
        switch (effect.effectType)
        {
            case CardEffectType.Damage:
                return DetermineAttackTypeFromCardKeywords(cardData);
            case CardEffectType.ApplyWeak:
            case CardEffectType.ApplyBreak:
            case CardEffectType.ApplyStun:
            case CardEffectType.ApplyCurse:
                return AttackEffectSource.AttackType.Magic; // Status effects are magical
            case CardEffectType.ApplyDamageOverTime:
                return AttackEffectSource.AttackType.Magic; // DoT effects are magical
            default:
                return AttackEffectSource.AttackType.Default;
        }
    }
    
    /// <summary>
    /// Determines attack type based on card keywords (fallback method)
    /// </summary>
    private AttackEffectSource.AttackType DetermineAttackTypeFromCardKeywords(CardData cardData)
    {
        if (cardData == null) return AttackEffectSource.AttackType.Default;
        
        string cardName = cardData.CardName.ToLower();
        string description = cardData.Description.ToLower();
        
        // Magic attacks
        if (cardName.Contains("spell") || cardName.Contains("magic") || cardName.Contains("bolt") ||
            description.Contains("magic") || description.Contains("spell"))
        {
            return AttackEffectSource.AttackType.Magic;
        }
        
        // Ranged attacks
        if (cardName.Contains("shot") || cardName.Contains("arrow") || cardName.Contains("projectile") ||
            description.Contains("ranged") || description.Contains("shoot"))
        {
            return AttackEffectSource.AttackType.Ranged;
        }
        
        // Melee attacks
        if (cardName.Contains("strike") || cardName.Contains("slash") || cardName.Contains("punch") ||
            cardName.Contains("melee") || description.Contains("melee"))
        {
            return AttackEffectSource.AttackType.Melee;
        }
        
        return AttackEffectSource.AttackType.Default;
    }
    
    /// <summary>
    /// Triggers attack visual effects including animations and particles based on card effects
    /// </summary>
    public void TriggerVisualEffects(NetworkEntity sourceEntity, NetworkEntity targetEntity, CardData cardData)
    {
        Debug.Log($"HandleCardPlay: TriggerAttackVisualEffects called - Source: {sourceEntity?.EntityName.Value}, Target: {targetEntity?.EntityName.Value}, Card: {cardData?.CardName}");
        
        if (sourceEntity == null || targetEntity == null || cardData == null) 
        {
            Debug.LogError($"HandleCardPlay: Missing parameters - Source: {sourceEntity != null}, Target: {targetEntity != null}, CardData: {cardData != null}");
            return;
        }
        
        bool hasTriggeredAttackAnimation = false;
        float maxEffectDuration = 0f;
        
        Debug.Log($"HandleCardPlay: Processing {cardData.Effects.Count} effects for card {cardData.CardName}");
        
        // Process each effect that needs visual representation
        foreach (var effect in cardData.Effects)
        {
            Debug.Log($"HandleCardPlay: Processing effect {effect.effectType} with animation behavior {effect.animationBehavior}");
            
            if (!ShouldEffectTriggerVisual(effect, cardData))
            {
                Debug.Log($"HandleCardPlay: Effect {effect.effectType} skipped (no visual needed)");
                continue;
            }
                
            Debug.Log($"HandleCardPlay: Effect {effect.effectType} will trigger visual");
            
            // Trigger attack animation once for any effect that needs it
            if (!hasTriggeredAttackAnimation && ShouldTriggerAttackAnimation(effect))
            {
                Debug.Log($"HandleCardPlay: Triggering attack animation on source {sourceEntity.EntityName.Value}");
                NetworkEntityAnimator sourceAnimator = sourceEntity.GetComponent<NetworkEntityAnimator>();
                if (sourceAnimator != null)
                {
                    sourceAnimator.PlayAttackAnimation();
                    hasTriggeredAttackAnimation = true;
                    Debug.Log($"HandleCardPlay: Attack animation triggered successfully");
                }
                else
                {
                    Debug.LogWarning($"HandleCardPlay: No NetworkEntityAnimator found on source entity {sourceEntity.EntityName.Value}");
                }
            }
            
            // Handle the specific effect's visual behavior
            Debug.Log($"HandleCardPlay: Triggering visual for effect {effect.effectType}");
            float effectDuration = TriggerEffectVisual(sourceEntity, targetEntity, effect, cardData);
            maxEffectDuration = Mathf.Max(maxEffectDuration, effectDuration);
            Debug.Log($"HandleCardPlay: Effect duration: {effectDuration}, max so far: {maxEffectDuration}");
        }
        
        // Schedule damage animation on target after effects complete
        if (maxEffectDuration > 0f)
        {
            Debug.Log($"HandleCardPlay: Scheduling damage animation on target {targetEntity.EntityName.Value} with delay {maxEffectDuration * 0.8f}");
            StartCoroutine(TriggerDamageAnimationDelayed(targetEntity, maxEffectDuration * 0.8f)); // 80% through effect
        }
        else
        {
            Debug.Log($"HandleCardPlay: No damage animation scheduled (maxEffectDuration = {maxEffectDuration})");
        }
    }
    
    /// <summary>
    /// Determines if an effect should trigger an attack animation
    /// </summary>
    private bool ShouldTriggerAttackAnimation(CardEffect effect)
    {
        switch (effect.animationBehavior)
        {
            case EffectAnimationBehavior.ProjectileFromSource:
            case EffectAnimationBehavior.BeamToTarget:
                return true;
            case EffectAnimationBehavior.InstantOnTarget:
            case EffectAnimationBehavior.OnSourceOnly:
            case EffectAnimationBehavior.AreaEffect:
            case EffectAnimationBehavior.None:
                return false;
            case EffectAnimationBehavior.Auto:
                // For auto, only trigger on damage effects
                return effect.effectType == CardEffectType.Damage;
            default:
                return false;
        }
    }
    
    /// <summary>
    /// Triggers the visual effect for a specific card effect
    /// </summary>
    private float TriggerEffectVisual(NetworkEntity sourceEntity, NetworkEntity targetEntity, CardEffect effect, CardData cardData)
    {
        float duration = effect.customDuration > 0 ? effect.customDuration : 0f; // 0 means use default
        
        switch (effect.animationBehavior)
        {
            case EffectAnimationBehavior.None:
                return 0f;
                
            case EffectAnimationBehavior.InstantOnTarget:
                // Play effect instantly on target with delay
                StartCoroutine(TriggerInstantEffectDelayed(targetEntity, effect, effect.animationDelay));
                return effect.animationDelay + 0.5f; // Short duration for instant effect
                
                         case EffectAnimationBehavior.ProjectileFromSource:
             case EffectAnimationBehavior.Auto:
                 // Projectile from source to target
                 AttackEffectSource.AttackType attackType = DetermineAttackType(effect, cardData);
                 
                 if (effect.animationDelay > 0f)
                 {
                     if (!string.IsNullOrEmpty(effect.customEffectName))
                     {
                         Debug.Log($"HandleCardPlay: Using delayed custom effect name: {effect.customEffectName}");
                         StartCoroutine(TriggerNamedCustomEffectDelayed(sourceEntity, targetEntity, effect.customEffectName, duration, effect.animationDelay));
                     }
                     else
                     {
                         Debug.Log($"HandleCardPlay: Using delayed default attack type: {attackType}");
                         StartCoroutine(TriggerProjectileEffectDelayed(sourceEntity, targetEntity, attackType, duration, effect.animationDelay));
                     }
                 }
                 else
                 {
                                      if (!string.IsNullOrEmpty(effect.customEffectName))
                 {
                     Debug.Log($"HandleCardPlay: Using custom effect name: {effect.customEffectName}");
                     AttackEffectManager.TriggerNamedCustomEffect(sourceEntity, targetEntity, effect.customEffectName, duration);
                 }
                 else
                 {
                     Debug.Log($"HandleCardPlay: Using default attack type: {attackType}");
                     AttackEffectManager.TriggerAttackEffect(sourceEntity, targetEntity, attackType, duration);
                 }
                 }
                 
                 float projectileDuration = duration > 0 ? duration : 2f; // Default duration
                 return effect.animationDelay + projectileDuration;
                
            case EffectAnimationBehavior.OnSourceOnly:
                // Effect plays on source only
                StartCoroutine(TriggerSourceEffectDelayed(sourceEntity, effect, effect.animationDelay));
                return effect.animationDelay + 1f;
                
            case EffectAnimationBehavior.AreaEffect:
                // Area effect (could be enhanced later for multiple targets)
                StartCoroutine(TriggerAreaEffectDelayed(sourceEntity, targetEntity, effect, effect.animationDelay));
                return effect.animationDelay + 1.5f;
                
            case EffectAnimationBehavior.BeamToTarget:
                // Continuous beam effect
                AttackEffectSource.AttackType beamType = DetermineAttackType(effect, cardData);
                float beamDuration = duration > 0 ? duration : 3f; // Beams are longer
                
                if (effect.animationDelay > 0f)
                {
                    StartCoroutine(TriggerProjectileEffectDelayed(sourceEntity, targetEntity, beamType, beamDuration, effect.animationDelay));
                }
                else
                {
                    AttackEffectManager.TriggerAttackEffect(sourceEntity, targetEntity, beamType, beamDuration);
                }
                
                return effect.animationDelay + beamDuration;
                
            default:
                return 0f;
        }
    }
    
    /// <summary>
    /// Coroutine to trigger projectile effect after a delay
    /// </summary>
    private System.Collections.IEnumerator TriggerProjectileEffectDelayed(NetworkEntity sourceEntity, NetworkEntity targetEntity, AttackEffectSource.AttackType attackType, float duration, float delay)
    {
        yield return new WaitForSeconds(delay);
        AttackEffectManager.TriggerAttackEffect(sourceEntity, targetEntity, attackType, duration);
    }
    
    /// <summary>
    /// Coroutine to trigger custom projectile effect after a delay
    /// </summary>
    private System.Collections.IEnumerator TriggerCustomProjectileEffectDelayed(NetworkEntity sourceEntity, NetworkEntity targetEntity, GameObject customPrefab, AttackEffectSource.AttackType fallbackAttackType, float duration, float delay)
    {
        yield return new WaitForSeconds(delay);
        AttackEffectManager.TriggerCustomAttackEffect(sourceEntity, targetEntity, customPrefab, fallbackAttackType, duration);
    }
    
    /// <summary>
    /// Coroutine to trigger named custom effect after a delay
    /// </summary>
    private System.Collections.IEnumerator TriggerNamedCustomEffectDelayed(NetworkEntity sourceEntity, NetworkEntity targetEntity, string effectName, float duration, float delay)
    {
        yield return new WaitForSeconds(delay);
        AttackEffectManager.TriggerNamedCustomEffect(sourceEntity, targetEntity, effectName, duration);
    }
    
    /// <summary>
    /// Coroutine to trigger instant effect on target after a delay
    /// </summary>
    private System.Collections.IEnumerator TriggerInstantEffectDelayed(NetworkEntity targetEntity, CardEffect effect, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // For instant effects, we could trigger a different type of particle system
        // that plays directly on the target without projectile movement
        Debug.Log($"HandleCardPlay: Triggered instant {effect.effectType} effect on {targetEntity.EntityName.Value}");
        
        // TODO: Implement instant effect visuals (could be a different particle system or just animation)
    }
    
    /// <summary>
    /// Coroutine to trigger source-only effect after a delay
    /// </summary>
    private System.Collections.IEnumerator TriggerSourceEffectDelayed(NetworkEntity sourceEntity, CardEffect effect, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        Debug.Log($"HandleCardPlay: Triggered source-only {effect.effectType} effect on {sourceEntity.EntityName.Value}");
        
        // TODO: Implement source-only effect visuals (buffs, self-heals, etc.)
    }
    
    /// <summary>
    /// Coroutine to trigger area effect after a delay
    /// </summary>
    private System.Collections.IEnumerator TriggerAreaEffectDelayed(NetworkEntity sourceEntity, NetworkEntity targetEntity, CardEffect effect, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        Debug.Log($"HandleCardPlay: Triggered area {effect.effectType} effect from {sourceEntity.EntityName.Value}");
        
        // TODO: Implement area effect visuals (could affect multiple targets)
    }
    
    /// <summary>
    /// Triggers damage animation on target entity after a delay
    /// </summary>
    private System.Collections.IEnumerator TriggerDamageAnimationDelayed(NetworkEntity targetEntity, float delay)
    {
        Debug.Log($"HandleCardPlay: Starting damage animation delay of {delay} seconds for {targetEntity.EntityName.Value}");
        yield return new WaitForSeconds(delay);
        
        Debug.Log($"HandleCardPlay: Delay complete, triggering damage animation on {targetEntity.EntityName.Value}");
        NetworkEntityAnimator targetAnimator = targetEntity.GetComponent<NetworkEntityAnimator>();
        if (targetAnimator != null)
        {
            Debug.Log($"HandleCardPlay: Found NetworkEntityAnimator, calling PlayTakeDamageAnimation");
            targetAnimator.PlayTakeDamageAnimation();
        }
        else
        {
            Debug.LogWarning($"HandleCardPlay: No NetworkEntityAnimator found on target entity {targetEntity.EntityName.Value}");
        }
    }

    #if UNITY_EDITOR
    /// <summary>
    /// Coroutine to return card to hand after a delay (for testing mode)
    /// </summary>
    private System.Collections.IEnumerator ReturnToHandAfterDelay()
    {
        // Wait a short time to let effects process
        yield return new WaitForSeconds(0.5f);
        
        // Return the card to hand
        if (TestCombat.Instance != null)
        {
            TestCombat.Instance.ReturnCardToHand(gameObject);
        }
    }
    #endif
} 