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
    
    // Flag to track if energy has already been deducted (to prevent double deduction)
    private bool energyAlreadyDeducted = false;
    
    // Static tracking to prevent multiple damage animations on the same target
    private static Dictionary<int, Coroutine> activeDamageAnimations = new Dictionary<int, Coroutine>();
    
    // Tracking for finishing animations
    private static Dictionary<int, List<System.Collections.IEnumerator>> activeFinishingAnimations = new Dictionary<int, List<System.Collections.IEnumerator>>();

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
    /// Called when a card play is attempted
    /// </summary>
    public void OnCardPlayAttempt()
    {
        if (!CanPlayCard())
        {
            Debug.LogWarning($"HandleCardPlay: Cannot play card {card.CardData?.CardName}. Reason: {playBlockReason}");
            return;
        }

        Debug.Log($"HandleCardPlay: OnCardPlayAttempt - Playing card {card.CardData?.CardName}");
        
        // TRIGGER CARD PLAY ANIMATION - Do this at the start so the card animates out immediately
        // The animation completion callback will handle the cleanup
        TriggerCardPlayAnimationWithCleanup();
        
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
        
        // Normal card play processing - effects are resolved, but cleanup is delayed until animation completes
        if (cardEffectResolver != null)
        {
            cardEffectResolver.ResolveCardEffect();
        }
        else
        {
            Debug.LogError("HandleCardPlay: Cannot resolve effect - missing CardEffectResolver");
        }
        
        // NOTE: ProcessCardPlayCleanup() is now called from the animation completion callback
        // instead of immediately, so the card can animate out before being discarded
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
                /* Debug.Log($"HandleCardPlay: Cannot play {card.CardData.CardName} - combo requirement not met (have {sourceTracker.ComboCount}, need {card.CardData.RequiredComboAmount})"); */
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
        Debug.Log($"HandleCardPlay: ServerPlayCard called for card GameObject: {gameObject.name}");
        
        if (card?.CardData != null)
        {
            Debug.Log($"HandleCardPlay: Card data found - CardName: {card.CardData.CardName}");
        }
        else
        {
            Debug.LogWarning($"HandleCardPlay: Card or CardData is null for GameObject: {gameObject.name}");
        }
        
        // Validate on server side as well
        if (!CanPlayCard())
        {
            Debug.LogWarning($"HandleCardPlay: Server rejected card play. Reason: {playBlockReason}");
            ClientRejectCardPlay(conn, playBlockReason);
            return;
        }

        Debug.Log($"HandleCardPlay: Server validation passed for GameObject: {gameObject.name}");

        // TRIGGER CARD PLAY ANIMATION - Do this BEFORE processing effects so the card animates out immediately
        // The animation completion callback will handle the discard
        TriggerCardPlayAnimationWithCleanup();

        // FIXED: Deduct energy cost BEFORE processing effects (was happening after)
        // This ensures restore energy effects work correctly with the proper order
        if (sourceAndTargetIdentifier?.SourceEntity != null && card?.CardData != null)
        {
            var sourceEntity = sourceAndTargetIdentifier.SourceEntity;
            int energyCost = card.CardData.EnergyCost;
            
            // Deduct energy cost first and mark as deducted
            sourceEntity.ChangeEnergy(-energyCost);
            energyAlreadyDeducted = true;
            /* Debug.Log($"HandleCardPlay: Deducted {energyCost} energy from {sourceEntity.EntityName.Value} before applying effects"); */
        }

        // Process the card effect AFTER energy deduction
        /* Debug.Log($"HandleCardPlay: Processing card effects - cardEffectResolver: {cardEffectResolver != null}"); */
        
        if (cardEffectResolver != null)
        {
            // Since we're already on server, call the effect resolver directly
            var sourceEntity = sourceAndTargetIdentifier.SourceEntity;
            var targetEntity = sourceAndTargetIdentifier.AllTargets.Count > 0 ? sourceAndTargetIdentifier.AllTargets[0] : null;
            
            /* Debug.Log($"HandleCardPlay: Entities - Source: {sourceEntity?.EntityName.Value}, Target: {targetEntity?.EntityName.Value}, CardData: {card?.CardData?.CardName}"); */
            
            if (sourceEntity != null && targetEntity != null)
            {
                // Check if this card should trigger visual effects
                /* Debug.Log($"HandleCardPlay: Checking if card {card.CardData?.CardName} should trigger visual effects..."); */
                bool shouldTriggerVisuals = ShouldTriggerVisualEffects(card.CardData);
                /* Debug.Log($"HandleCardPlay: ShouldTriggerVisualEffects result: {shouldTriggerVisuals}"); */
                
                if (shouldTriggerVisuals)
                {
                    /* Debug.Log($"HandleCardPlay: Card {card.CardData?.CardName} SHOULD trigger visual effects - calling TriggerVisualEffects"); */
                    TriggerVisualEffects(sourceEntity, targetEntity, card.CardData);
                }
                else
                {
                    /* Debug.Log($"HandleCardPlay: Card {card.CardData?.CardName} should NOT trigger visual effects"); */
                }
                
                /* Debug.Log($"HandleCardPlay: Calling cardEffectResolver.ServerResolveCardEffect"); */
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
        
        // Schedule delayed card discarding on server to allow animation to play first
        // This ensures pet cards are properly discarded even when ownership check fails
        if (sourceAndTargetIdentifier?.SourceEntity != null)
        {
            StartCoroutine(DelayedServerCardDiscard());
        }
        
        Debug.Log($"HandleCardPlay: ServerPlayCard completed for card {card?.CardData?.CardName}");
    }

    /// <summary>
    /// Triggers card play animation on all clients
    /// </summary>
    private void TriggerCardPlayAnimation()
    {
        Debug.Log($"HandleCardPlay: TriggerCardPlayAnimation called for GameObject: {gameObject.name}");
        
        // Trigger the animation on all clients
        ClientTriggerCardPlayAnimation();
    }

    /// <summary>
    /// Triggers card play animation and schedules cleanup after animation completes
    /// </summary>
    private void TriggerCardPlayAnimationWithCleanup()
    {
        Debug.Log($"HandleCardPlay: TriggerCardPlayAnimationWithCleanup called for GameObject: {gameObject.name}");
        
        if (IsServerInitialized)
        {
            // Server: Use RPC to trigger animation on all clients
            ClientTriggerCardPlayAnimationWithCleanup();
        }
        else
        {
            // Client: Trigger animation directly (no RPC needed)
            Debug.Log($"HandleCardPlay: Client calling animation directly for GameObject: {gameObject.name}");
            TriggerCardPlayAnimationDirectly();
        }
    }

    /// <summary>
    /// Triggers card play animation directly without RPC (for client-side calls)
    /// </summary>
    private void TriggerCardPlayAnimationDirectly()
    {
        Debug.Log($"HandleCardPlay: TriggerCardPlayAnimationDirectly called for GameObject: {gameObject.name}");
        
        // Get the CardAnimator component
        CardAnimator cardAnimator = GetComponent<CardAnimator>();
        if (cardAnimator != null)
        {
            Debug.Log($"HandleCardPlay: Found CardAnimator, triggering play success animation directly for GameObject: {gameObject.name}");
            
            // Trigger the play success animation with cleanup callback
            cardAnimator.AnimatePlaySuccess(null, () => {
                // Animation complete callback
                Debug.Log($"HandleCardPlay: Play animation completed for GameObject: {gameObject.name} - calling HandAnimator callback");
                
                // Notify the HandAnimator that this card played successfully
                HandAnimator handAnimator = cardAnimator.GetComponentInParent<HandAnimator>();
                if (handAnimator != null)
                {
                    Debug.Log($"HandleCardPlay: Found HandAnimator on GameObject: {handAnimator.gameObject.name}, calling OnCardPlaySuccessful for card: {gameObject.name}");
                    handAnimator.OnCardPlaySuccessful(cardAnimator);
                    Debug.Log($"HandleCardPlay: OnCardPlaySuccessful call completed for card: {gameObject.name}");
                }
                else
                {
                    Debug.LogWarning($"HandleCardPlay: No HandAnimator found for GameObject: {gameObject.name}");
                }
                
                // Only call cleanup on the card owner to prevent ServerRPC errors on clients
                if (IsOwner)
                {
                    Debug.Log($"HandleCardPlay: Card owner calling cleanup for GameObject: {gameObject.name}");
                    ProcessCardPlayCleanup();
                }
                else
                {
                    Debug.Log($"HandleCardPlay: Non-owner client skipping cleanup for GameObject: {gameObject.name}");
                }
            });
        }
        else
        {
            Debug.LogWarning($"HandleCardPlay: No CardAnimator found on GameObject: {gameObject.name}");
            // If no animator, only call cleanup if we're the owner
            if (IsOwner)
            {
                Debug.Log($"HandleCardPlay: Card owner calling immediate cleanup for GameObject: {gameObject.name}");
                ProcessCardPlayCleanup();
            }
            else
            {
                Debug.Log($"HandleCardPlay: Non-owner client skipping immediate cleanup for GameObject: {gameObject.name}");
            }
        }
    }

    /// <summary>
    /// Client RPC to trigger card play animation
    /// </summary>
    [ObserversRpc]
    private void ClientTriggerCardPlayAnimation()
    {
        Debug.Log($"HandleCardPlay: ClientTriggerCardPlayAnimation RPC received for GameObject: {gameObject.name}");
        
        // Get the CardAnimator component
        CardAnimator cardAnimator = GetComponent<CardAnimator>();
        if (cardAnimator != null)
        {
            Debug.Log($"HandleCardPlay: Found CardAnimator, triggering play success animation for GameObject: {gameObject.name}");
            
            // Trigger the play success animation
            cardAnimator.AnimatePlaySuccess(null, () => {
                // Animation complete callback
                Debug.Log($"HandleCardPlay: Play animation completed for GameObject: {gameObject.name}");
                
                // Notify the HandAnimator that this card played successfully
                HandAnimator handAnimator = cardAnimator.GetComponentInParent<HandAnimator>();
                if (handAnimator != null)
                {
                    handAnimator.OnCardPlaySuccessful(cardAnimator);
                }
                else
                {
                    Debug.LogWarning($"HandleCardPlay: No HandAnimator found for GameObject: {gameObject.name}");
                }
            });
        }
        else
        {
            Debug.LogWarning($"HandleCardPlay: No CardAnimator found on GameObject: {gameObject.name} - cannot trigger play animation");
        }
    }

    /// <summary>
    /// Client RPC to trigger card play animation with cleanup after completion
    /// </summary>
    [ObserversRpc]
    private void ClientTriggerCardPlayAnimationWithCleanup()
    {
        Debug.Log($"HandleCardPlay: ClientTriggerCardPlayAnimationWithCleanup RPC received for GameObject: {gameObject.name}");
        
        // Get the CardAnimator component
        CardAnimator cardAnimator = GetComponent<CardAnimator>();
        if (cardAnimator != null)
        {
            Debug.Log($"HandleCardPlay: Found CardAnimator, triggering play success animation with cleanup for GameObject: {gameObject.name}");
            
            // Trigger the play success animation with cleanup callback
            cardAnimator.AnimatePlaySuccess(null, () => {
                // Animation complete callback
                Debug.Log($"HandleCardPlay: Play animation completed for GameObject: {gameObject.name} - calling HandAnimator callback");
                
                // Notify the HandAnimator that this card played successfully
                HandAnimator handAnimator = cardAnimator.GetComponentInParent<HandAnimator>();
                if (handAnimator != null)
                {
                    Debug.Log($"HandleCardPlay: Found HandAnimator on GameObject: {handAnimator.gameObject.name}, calling OnCardPlaySuccessful for card: {gameObject.name}");
                    handAnimator.OnCardPlaySuccessful(cardAnimator);
                    Debug.Log($"HandleCardPlay: OnCardPlaySuccessful call completed for card: {gameObject.name}");
                }
                else
                {
                    Debug.LogWarning($"HandleCardPlay: No HandAnimator found for GameObject: {gameObject.name}");
                }
                
                // Only call cleanup on the card owner to prevent ServerRPC errors on clients
                if (IsOwner)
                {
                    Debug.Log($"HandleCardPlay: Card owner calling cleanup for GameObject: {gameObject.name}");
                    ProcessCardPlayCleanup();
                }
                else
                {
                    Debug.Log($"HandleCardPlay: Non-owner client skipping cleanup for GameObject: {gameObject.name}");
                }
            });
        }
        else
        {
            Debug.LogWarning($"HandleCardPlay: No CardAnimator found on GameObject: {gameObject.name}");
            // If no animator, only call cleanup if we're the owner
            if (IsOwner)
            {
                Debug.Log($"HandleCardPlay: Card owner calling immediate cleanup for GameObject: {gameObject.name}");
                ProcessCardPlayCleanup();
            }
            else
            {
                Debug.Log($"HandleCardPlay: Non-owner client skipping immediate cleanup for GameObject: {gameObject.name}");
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
        
        // Only deduct energy if it hasn't been deducted already (for player cards vs pet cards)
        if (!energyAlreadyDeducted)
        {
            sourceEntity.ChangeEnergy(-energyCost);
            /* Debug.Log($"HandleCardPlay: Deducted {energyCost} energy from {sourceEntity.EntityName.Value} during cleanup"); */
        }
        else
        {
            /* Debug.Log($"HandleCardPlay: Energy already deducted for {sourceEntity.EntityName.Value}, skipping deduction in cleanup"); */
        }
        
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
            Debug.Log("HandleCardPlay: ShouldTriggerVisualEffects - CardData is null");
            return false;
        }
        
        if (!cardData.HasEffects)
        {
            Debug.Log($"HandleCardPlay: ShouldTriggerVisualEffects - Card {cardData.CardName} has no effects");
            return false;
        }
        
        /* Debug.Log($"HandleCardPlay: ShouldTriggerVisualEffects - Card {cardData.CardName} has {cardData.Effects.Count} effects, checking each..."); */
        
        // Check each effect to see if any need visual effects
        foreach (var effect in cardData.Effects)
        {
            Debug.Log($"HandleCardPlay: ShouldTriggerVisualEffects - Checking effect {effect.effectType} with behavior {effect.animationBehavior}");
            if (ShouldEffectTriggerVisual(effect, cardData))
            {
                /* Debug.Log($"HandleCardPlay: ShouldTriggerVisualEffects - Effect {effect.effectType} SHOULD trigger visual"); */
                return true;
            }
            else
            {
                /* Debug.Log($"HandleCardPlay: ShouldTriggerVisualEffects - Effect {effect.effectType} should NOT trigger visual"); */
            }
        }
        
        /* Debug.Log($"HandleCardPlay: ShouldTriggerVisualEffects - No effects on card {cardData.CardName} need visual effects"); */
        return false;
    }
    
    /// <summary>
    /// Checks if a specific effect should trigger visual animation
    /// </summary>
    private bool ShouldEffectTriggerVisual(CardEffect effect, CardData cardData)
    {
        /* Debug.Log($"HandleCardPlay: ShouldEffectTriggerVisual - Checking effect {effect.effectType} with animation behavior {effect.animationBehavior}"); */
        
        // Check explicit animation behavior first
        switch (effect.animationBehavior)
        {
            case EffectAnimationBehavior.None:
                /* Debug.Log($"HandleCardPlay: ShouldEffectTriggerVisual - Effect {effect.effectType} has None behavior, returning false"); */
                return false;
            case EffectAnimationBehavior.InstantOnTarget:
            case EffectAnimationBehavior.ProjectileFromSource:
            case EffectAnimationBehavior.OnSourceOnly:
            case EffectAnimationBehavior.AreaEffect:
            case EffectAnimationBehavior.BeamToTarget:
                /* Debug.Log($"HandleCardPlay: ShouldEffectTriggerVisual - Effect {effect.effectType} has explicit visual behavior {effect.animationBehavior}, returning true"); */
                return true;
            case EffectAnimationBehavior.Auto:
                Debug.Log($"HandleCardPlay: ShouldEffectTriggerVisual - Effect {effect.effectType} has Auto behavior, falling through to auto-detection");
                // Fall through to auto-detection
                break;
        }
        
        // Auto-detection based on effect type
        bool shouldTrigger = false;
        switch (effect.effectType)
        {
            case CardEffectType.Damage:
            case CardEffectType.ApplyWeak:
            case CardEffectType.ApplyBreak:
            case CardEffectType.ApplyStun:
            case CardEffectType.ApplyCurse:
            case CardEffectType.ApplyBurn:
                shouldTrigger = true;
                break;
            default:
                shouldTrigger = false;
                break;
        }
        
        /* Debug.Log($"HandleCardPlay: ShouldEffectTriggerVisual - Auto-detection for effect {effect.effectType}: {shouldTrigger}"); */
        return shouldTrigger;
    }
    

    
    /// <summary>
    /// Triggers attack visual effects including animations and particles based on card effects
    /// </summary>
    public void TriggerVisualEffects(NetworkEntity sourceEntity, NetworkEntity targetEntity, CardData cardData)
    {
        /* Debug.Log($"HandleCardPlay: TriggerVisualEffects called - Source: {sourceEntity?.EntityName.Value}, Target: {targetEntity?.EntityName.Value}, Card: {cardData?.CardName}"); */
        
        if (sourceEntity == null || targetEntity == null || cardData == null) 
        {
            Debug.LogError($"HandleCardPlay: TriggerVisualEffects - Missing parameters - Source: {sourceEntity != null}, Target: {targetEntity != null}, CardData: {cardData != null}");
            return;
        }
        
        bool hasTriggeredAttackAnimation = false;
        float maxEffectDuration = 0f;
        
        /* Debug.Log($"HandleCardPlay: TriggerVisualEffects - Processing {cardData.Effects.Count} effects for card {cardData.CardName}"); */
        
        // Process each effect that needs visual representation
        foreach (var effect in cardData.Effects)
        {
            /* Debug.Log($"HandleCardPlay: TriggerVisualEffects - Processing effect {effect.effectType} with animation behavior {effect.animationBehavior}"); */
            
            if (!ShouldEffectTriggerVisual(effect, cardData))
            {
                /* Debug.Log($"HandleCardPlay: TriggerVisualEffects - Effect {effect.effectType} skipped (no visual needed)"); */
                continue;
            }
                
            /* Debug.Log($"HandleCardPlay: TriggerVisualEffects - Effect {effect.effectType} will trigger visual"); */
            
            // Trigger attack animation once for any effect that needs it
            bool shouldTriggerAttackAnim = ShouldTriggerAttackAnimation(effect);
            /* Debug.Log($"HandleCardPlay: TriggerVisualEffects - ShouldTriggerAttackAnimation for effect {effect.effectType}: {shouldTriggerAttackAnim}"); */
            
            if (!hasTriggeredAttackAnimation && shouldTriggerAttackAnim)
            {
                /* Debug.Log($"HandleCardPlay: TriggerVisualEffects - Attempting to trigger attack animation on source {sourceEntity.EntityName.Value}"); */
                NetworkEntityAnimator sourceAnimator = sourceEntity.GetComponent<NetworkEntityAnimator>();
                if (sourceAnimator != null)
                {
                    /* Debug.Log($"HandleCardPlay: TriggerVisualEffects - Found NetworkEntityAnimator on {sourceEntity.EntityName.Value}, calling PlayAttackAnimation"); */
                    sourceAnimator.PlayAttackAnimation();
                    hasTriggeredAttackAnimation = true;
                    /* Debug.Log($"HandleCardPlay: TriggerVisualEffects - Attack animation triggered successfully on {sourceEntity.EntityName.Value}"); */
                }
                else
                {
                    Debug.LogWarning($"HandleCardPlay: TriggerVisualEffects - No NetworkEntityAnimator found on source entity {sourceEntity.EntityName.Value}");
                }
            }
            else if (hasTriggeredAttackAnimation)
            {
                /* Debug.Log($"HandleCardPlay: TriggerVisualEffects - Attack animation already triggered for this card"); */
            }
            else
            {
                /* Debug.Log($"HandleCardPlay: TriggerVisualEffects - Effect {effect.effectType} does not need attack animation"); */
            }
            
            // Handle the specific effect's visual behavior
            /* Debug.Log($"HandleCardPlay: TriggerVisualEffects - Triggering visual for effect {effect.effectType}"); */
            float effectDuration = TriggerEffectVisual(sourceEntity, targetEntity, effect, cardData);
            maxEffectDuration = Mathf.Max(maxEffectDuration, effectDuration);
            /* Debug.Log($"HandleCardPlay: TriggerVisualEffects - Effect duration: {effectDuration}, max so far: {maxEffectDuration}"); */
        }
        
        // Schedule damage animation on target after effects complete
        if (maxEffectDuration > 0f)
        {
            float damageAnimDelay = maxEffectDuration * 0.8f;
            int targetId = (int)targetEntity.NetworkObject.ObjectId;
            
            // Check if there's already a damage animation scheduled for this target
            if (activeDamageAnimations.ContainsKey(targetId))
            {
                /* Debug.Log($"HandleCardPlay: TriggerVisualEffects - Damage animation already scheduled for {targetEntity.EntityName.Value} (ID: {targetId}), canceling previous one"); */
                
                // Cancel the existing coroutine
                if (EffectAnimationManager.Instance != null && activeDamageAnimations[targetId] != null)
                {
                    EffectAnimationManager.Instance.StopCoroutine(activeDamageAnimations[targetId]);
                }
                
                activeDamageAnimations.Remove(targetId);
            }
            
            /* Debug.Log($"HandleCardPlay: TriggerVisualEffects - Scheduling damage animation on target {targetEntity.EntityName.Value} (ID: {targetId}) with delay {damageAnimDelay}"); */
            
            // Use EffectAnimationManager to start the coroutine since the card GameObject may become inactive
            if (EffectAnimationManager.Instance != null)
            {
                Coroutine damageCoroutine = EffectAnimationManager.Instance.StartCoroutine(TriggerDamageAnimationDelayed(targetEntity, damageAnimDelay));
                activeDamageAnimations[targetId] = damageCoroutine;
            }
            else
            {
                Debug.LogError($"HandleCardPlay: TriggerVisualEffects - EffectAnimationManager.Instance is null, cannot schedule damage animation");
            }
        }
        else
        {
            /* Debug.Log($"HandleCardPlay: TriggerVisualEffects - No damage animation scheduled (maxEffectDuration = {maxEffectDuration})"); */
        }
        
        /* Debug.Log($"HandleCardPlay: TriggerVisualEffects - Completed processing all effects for card {cardData.CardName}"); */
    }
    
    /// <summary>
    /// Determines if an effect should trigger an attack animation
    /// </summary>
    private bool ShouldTriggerAttackAnimation(CardEffect effect)
    {
        /* Debug.Log($"HandleCardPlay: ShouldTriggerAttackAnimation - Checking effect {effect.effectType} with animation behavior {effect.animationBehavior}"); */
        
        bool shouldTrigger = false;
        string reason = "";
        
        switch (effect.animationBehavior)
        {
            case EffectAnimationBehavior.ProjectileFromSource:
            case EffectAnimationBehavior.BeamToTarget:
                shouldTrigger = true;
                reason = $"has explicit animation behavior {effect.animationBehavior}";
                break;
            case EffectAnimationBehavior.InstantOnTarget:
            case EffectAnimationBehavior.OnSourceOnly:
            case EffectAnimationBehavior.AreaEffect:
            case EffectAnimationBehavior.None:
                shouldTrigger = false;
                reason = $"has non-attacking animation behavior {effect.animationBehavior}";
                break;
            case EffectAnimationBehavior.Auto:
                // For auto, only trigger on damage effects
                shouldTrigger = effect.effectType == CardEffectType.Damage;
                reason = shouldTrigger ? 
                    $"has Auto behavior and is Damage effect" : 
                    $"has Auto behavior but is {effect.effectType} (not Damage)";
                break;
            default:
                shouldTrigger = false;
                reason = $"has unknown animation behavior {effect.animationBehavior}";
                break;
        }
        
        /* Debug.Log($"HandleCardPlay: ShouldTriggerAttackAnimation - Effect {effect.effectType} {reason}, returning {shouldTrigger}"); */
        return shouldTrigger;
    }
    
    /// <summary>
    /// Triggers the visual effect for a specific card effect
    /// </summary>
    private float TriggerEffectVisual(NetworkEntity sourceEntity, NetworkEntity targetEntity, CardEffect effect, CardData cardData)
    {
        float duration = effect.customDuration > 0 ? effect.customDuration : 0f; // 0 means use default
        
        // Trigger sound effect if specified
        if (!string.IsNullOrEmpty(effect.customSoundEffectName))
        {
            Vector3 soundPosition = sourceEntity.transform.position;
            SoundEffectManager.TriggerNamedSoundEffect(soundPosition, effect.customSoundEffectName, 
                (uint)sourceEntity.ObjectId, (uint)targetEntity.ObjectId);
        }
        
        switch (effect.animationBehavior)
        {
            case EffectAnimationBehavior.None:
                // Still trigger finishing animation if it exists
                if (effect.hasFinishingAnimation)
                {
                    TriggerFinishingAnimation(targetEntity, effect, effect.finishingAnimationDelay);
                }
                return 0f;
                
            case EffectAnimationBehavior.InstantOnTarget:
                // Play effect instantly on target with delay
                if (EffectAnimationManager.Instance != null)
                {
                    EffectAnimationManager.Instance.StartCoroutine(TriggerInstantEffectDelayed(targetEntity, effect, effect.animationDelay));
                }
                
                // Trigger finishing animation if specified (can play simultaneously)
                if (effect.hasFinishingAnimation)
                {
                    float finishingDelay = effect.animationDelay + effect.finishingAnimationDelay;
                    TriggerFinishingAnimation(targetEntity, effect, finishingDelay);
                }
                
                return effect.animationDelay + 0.5f; // Short duration for instant effect
                
            case EffectAnimationBehavior.ProjectileFromSource:
            case EffectAnimationBehavior.Auto:
                // Projectile from source to target
                float totalDelay = effect.animationDelay;
                float effectDuration = duration > 0 ? duration : 2f; // Default duration
                
                if (effect.animationDelay > 0f)
                {
                    if (!string.IsNullOrEmpty(effect.customEffectName))
                    {
                        /* Debug.Log($"HandleCardPlay: Using delayed custom effect name: {effect.customEffectName}"); */
                        if (EffectAnimationManager.Instance != null)
                        {
                            EffectAnimationManager.Instance.StartCoroutine(TriggerNamedCustomEffectDelayed(sourceEntity, targetEntity, effect.customEffectName, duration, effect.animationDelay));
                        }
                    }
                    else
                    {
                        /* Debug.Log($"HandleCardPlay: Using delayed default effect"); */
                        if (EffectAnimationManager.Instance != null)
                        {
                            EffectAnimationManager.Instance.StartCoroutine(TriggerDefaultEffectDelayed(sourceEntity, targetEntity, duration, effect.animationDelay));
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(effect.customEffectName))
                    {
                        /* Debug.Log($"HandleCardPlay: Using custom effect name: {effect.customEffectName}"); */
                        EffectAnimationManager.TriggerNamedCustomEffect(sourceEntity, targetEntity, effect.customEffectName, duration);
                    }
                    else
                    {
                        /* Debug.Log($"HandleCardPlay: Using default effect"); */
                        EffectAnimationManager.TriggerEffectAnimation(sourceEntity, targetEntity, duration);
                    }
                }
                
                // Trigger finishing animation if specified (waits for projectile to reach target)
                if (effect.hasFinishingAnimation)
                {
                    float finishingDelay = totalDelay + effectDuration + effect.finishingAnimationDelay;
                    TriggerFinishingAnimation(targetEntity, effect, finishingDelay);
                }
                
                return effect.animationDelay + effectDuration;
                
            case EffectAnimationBehavior.OnSourceOnly:
                // Effect plays on source only
                if (EffectAnimationManager.Instance != null)
                {
                    EffectAnimationManager.Instance.StartCoroutine(TriggerSourceEffectDelayed(sourceEntity, effect, effect.animationDelay));
                }
                
                // Trigger finishing animation if specified (can play simultaneously)
                if (effect.hasFinishingAnimation)
                {
                    float finishingDelay = effect.animationDelay + effect.finishingAnimationDelay;
                    TriggerFinishingAnimation(sourceEntity, effect, finishingDelay);
                }
                
                return effect.animationDelay + 1f;
                
            case EffectAnimationBehavior.AreaEffect:
                // Area effect (could be enhanced later for multiple targets)
                if (EffectAnimationManager.Instance != null)
                {
                    EffectAnimationManager.Instance.StartCoroutine(TriggerAreaEffectDelayed(sourceEntity, targetEntity, effect, effect.animationDelay));
                }
                
                // Trigger finishing animation if specified (can play simultaneously)
                if (effect.hasFinishingAnimation)
                {
                    float finishingDelay = effect.animationDelay + effect.finishingAnimationDelay;
                    TriggerFinishingAnimation(targetEntity, effect, finishingDelay);
                }
                
                return effect.animationDelay + 1.5f;
                
            case EffectAnimationBehavior.BeamToTarget:
                // Continuous beam effect
                float beamDuration = duration > 0 ? duration : 3f; // Beams are longer
                
                if (effect.animationDelay > 0f)
                {
                    if (!string.IsNullOrEmpty(effect.customEffectName))
                    {
                        if (EffectAnimationManager.Instance != null)
                        {
                            EffectAnimationManager.Instance.StartCoroutine(TriggerNamedCustomEffectDelayed(sourceEntity, targetEntity, effect.customEffectName, beamDuration, effect.animationDelay));
                        }
                    }
                    else
                    {
                        if (EffectAnimationManager.Instance != null)
                        {
                            EffectAnimationManager.Instance.StartCoroutine(TriggerDefaultEffectDelayed(sourceEntity, targetEntity, beamDuration, effect.animationDelay));
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(effect.customEffectName))
                    {
                        EffectAnimationManager.TriggerNamedCustomEffect(sourceEntity, targetEntity, effect.customEffectName, beamDuration);
                    }
                    else
                    {
                        EffectAnimationManager.TriggerEffectAnimation(sourceEntity, targetEntity, beamDuration);
                    }
                }
                
                // Trigger finishing animation if specified (waits for beam to complete)
                if (effect.hasFinishingAnimation)
                {
                    float finishingDelay = effect.animationDelay + beamDuration + effect.finishingAnimationDelay;
                    TriggerFinishingAnimation(targetEntity, effect, finishingDelay);
                }
                
                return effect.animationDelay + beamDuration;
                
            default:
                return 0f;
        }
    }
    
    /// <summary>
    /// Coroutine to trigger default effect after a delay
    /// </summary>
    private System.Collections.IEnumerator TriggerDefaultEffectDelayed(NetworkEntity sourceEntity, NetworkEntity targetEntity, float duration, float delay)
    {
        yield return new WaitForSeconds(delay);
        EffectAnimationManager.TriggerEffectAnimation(sourceEntity, targetEntity, duration);
    }
    
    /// <summary>
    /// Coroutine to trigger named custom effect after a delay
    /// </summary>
    private System.Collections.IEnumerator TriggerNamedCustomEffectDelayed(NetworkEntity sourceEntity, NetworkEntity targetEntity, string effectName, float duration, float delay)
    {
        yield return new WaitForSeconds(delay);
        EffectAnimationManager.TriggerNamedCustomEffect(sourceEntity, targetEntity, effectName, duration);
    }
    
    /// <summary>
    /// Triggers a finishing animation on the target entity
    /// </summary>
    private void TriggerFinishingAnimation(NetworkEntity targetEntity, CardEffect effect, float delay)
    {
        if (string.IsNullOrEmpty(effect.finishingAnimationName))
        {
            Debug.LogWarning($"HandleCardPlay: Finishing animation requested but no animation name specified for effect {effect.effectType}");
            return;
        }
        
        Debug.Log($"HandleCardPlay: Scheduling finishing animation '{effect.finishingAnimationName}' on {targetEntity.EntityName.Value} with delay {delay}");
        
        if (EffectAnimationManager.Instance != null)
        {
            EffectAnimationManager.Instance.StartCoroutine(TriggerFinishingAnimationDelayed(targetEntity, effect, delay));
        }
        else
        {
            Debug.LogError("HandleCardPlay: EffectAnimationManager.Instance is null, cannot schedule finishing animation");
        }
    }
    
    /// <summary>
    /// Coroutine to trigger finishing animation after a delay
    /// </summary>
    private System.Collections.IEnumerator TriggerFinishingAnimationDelayed(NetworkEntity targetEntity, CardEffect effect, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Trigger finishing animation
        EffectAnimationManager.TriggerNamedCustomEffect(targetEntity, targetEntity, effect.finishingAnimationName, 0f);
        
        // Trigger finishing sound effect if specified
        if (!string.IsNullOrEmpty(effect.finishingSoundEffectName))
        {
            Vector3 soundPosition = targetEntity.transform.position;
            SoundEffectManager.TriggerNamedSoundEffect(soundPosition, effect.finishingSoundEffectName, 
                (uint)targetEntity.ObjectId, (uint)targetEntity.ObjectId);
        }
        
        Debug.Log($"HandleCardPlay: Triggered finishing animation '{effect.finishingAnimationName}' on {targetEntity.EntityName.Value}");
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
        int targetId = (int)targetEntity.NetworkObject.ObjectId;
        /* Debug.Log($"HandleCardPlay: Starting damage animation delay of {delay} seconds for {targetEntity.EntityName.Value} (ID: {targetId})"); */
        
        yield return new WaitForSeconds(delay);
        
        // Remove this coroutine from tracking
        if (activeDamageAnimations.ContainsKey(targetId))
        {
            activeDamageAnimations.Remove(targetId);
        }
        
        /* Debug.Log($"HandleCardPlay: Delay complete, triggering damage animation on {targetEntity.EntityName.Value}"); */
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
    
    /// <summary>
    /// Delayed server-side card discarding to allow animation to complete first
    /// </summary>
    private System.Collections.IEnumerator DelayedServerCardDiscard()
    {
        // Wait for the card play animation to complete (typical duration is 0.4s)
        yield return new WaitForSeconds(0.5f);
        
        if (sourceAndTargetIdentifier?.SourceEntity != null)
        {
            HandManager handManager = GetHandManagerForEntity(sourceAndTargetIdentifier.SourceEntity);
            if (handManager != null)
            {
                handManager.DiscardCard(gameObject);
                Debug.Log($"HandleCardPlay: Server discarded card {card?.CardData?.CardName} to discard pile after animation delay");
            }
            else
            {
                Debug.LogError($"HandleCardPlay: Could not find HandManager for entity {sourceAndTargetIdentifier.SourceEntity.EntityName.Value}");
            }
        }
    }
} 