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
    
    // Flag to track if this card is currently queued
    private bool isCurrentlyQueued = false;
    
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
    /// Called when a card play is attempted - now routes directly to server
    /// </summary>
    public void OnCardPlayAttempt()
    {
        Debug.Log($"CARDPLAY_DEBUG: OnCardPlayAttempt called for card {card?.CardData?.CardName}");
        
        // Ensure source/target identification is updated on client before server validation
        if (sourceAndTargetIdentifier != null)
        {
            Debug.Log($"CARDPLAY_DEBUG: Updating source and target before validation");
            sourceAndTargetIdentifier.UpdateSourceAndTarget();
            
            // Log current source/target state
            Debug.Log($"CARDPLAY_DEBUG: After update - Source: {(sourceAndTargetIdentifier.SourceEntity != null ? sourceAndTargetIdentifier.SourceEntity.EntityName.Value : "null")}, Targets: {(sourceAndTargetIdentifier.AllTargets?.Count ?? 0)}");
        }
        else
        {
            Debug.LogError($"CARDPLAY_DEBUG: sourceAndTargetIdentifier is null for card {gameObject.name}");
        }
        
        // Check if this card is already queued - check both local flag and server queue state
        // This must happen BEFORE CanPlayCard validation so queued cards can be unqueued even if they fail validation
        bool isLocallyQueued = isCurrentlyQueued;
        bool isServerQueued = false;
        
        if (sourceAndTargetIdentifier != null && sourceAndTargetIdentifier.SourceEntity != null && CombatCardQueue.Instance != null)
        {
            var queuedCards = CombatCardQueue.Instance.GetQueuedCardPlays(sourceAndTargetIdentifier.SourceEntity.ObjectId);
            isServerQueued = queuedCards.Any(qc => qc.cardObject == gameObject);
        }
        
        bool cardIsQueued = isLocallyQueued || isServerQueued;
        
        Debug.Log($"CARDPLAY_DEBUG: Queue state check - Local: {isLocallyQueued}, Server: {isServerQueued}, Final: {cardIsQueued}");
        
        if (cardIsQueued)
        {
            Debug.Log($"CARDPLAY_DEBUG: Card {card.CardData?.CardName} is already queued, unqueueing it");
            
            // Get source ID for unqueueing
            var unqueueSourceEntity = sourceAndTargetIdentifier.SourceEntity;
            int unqueueSourceId = unqueueSourceEntity != null ? unqueueSourceEntity.ObjectId : 0;
            
            // Call server to unqueue the card
            ServerUnqueueCard(unqueueSourceId);
            return;
        }

        // Only validate if the card is not already queued
        if (!CanPlayCard())
        {
            Debug.LogWarning($"CARDPLAY_DEBUG: Cannot play card {card.CardData?.CardName}. Reason: {playBlockReason}");
            return;
        }

        Debug.Log($"CARDPLAY_DEBUG: OnCardPlayAttempt - Client validation passed, routing card {card.CardData?.CardName} to server");
        
        // Get source and target IDs to pass to server
        var clientSourceEntity = sourceAndTargetIdentifier.SourceEntity;
        var clientAllTargets = sourceAndTargetIdentifier.AllTargets;
        
        int sourceId = clientSourceEntity != null ? clientSourceEntity.ObjectId : 0;
        int[] targetIds = clientAllTargets != null ? clientAllTargets.Select(t => t != null ? t.ObjectId : 0).Where(id => id != 0).ToArray() : new int[0];
        
        Debug.Log($"CARDPLAY_DEBUG: Calling ServerPlayCard with sourceId: {sourceId}, targetIds: [{string.Join(", ", targetIds)}]");
        
        // Route all card plays through server for consistent processing
        ServerPlayCard(sourceId, targetIds);
    }

    /// <summary>
    /// Checks if the card can be played based on various conditions using passed parameters
    /// </summary>
    private bool CanPlayCardWithParams(int sourceId, int[] targetIds)
    {
        canPlay = true;
        playBlockReason = "";

        Debug.Log($"CARDPLAY_DEBUG: CanPlayCardWithParams validation starting for {card?.CardData?.CardName} with sourceId: {sourceId}, targetIds: [{string.Join(", ", targetIds)}]");

        // Check if we have valid card data
        if (card?.CardData == null)
        {
            canPlay = false;
            playBlockReason = "No card data";
            Debug.Log($"CARDPLAY_DEBUG: Validation failed - No card data");
            return false;
        }

        // Check if we have source entity
        if (sourceId <= 0)
        {
            canPlay = false;
            playBlockReason = "No source entity";
            Debug.Log($"CARDPLAY_DEBUG: Validation failed - No source entity ID provided");
            return false;
        }
        
        NetworkEntity sourceEntity = FindEntityById(sourceId);
        if (sourceEntity == null)
        {
            canPlay = false;
            playBlockReason = "No source entity";
            Debug.Log($"CARDPLAY_DEBUG: Validation failed - Could not find source entity with ID {sourceId}");
            return false;
        }

        Debug.Log($"CARDPLAY_DEBUG: Found source entity: {sourceEntity.EntityName.Value}");

        // Check for fizzle effects (cards can be played but may have no effect)
        EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
        if (sourceTracker != null && sourceTracker.HasFizzleEffect)
        {
            Debug.Log($"CARDPLAY_DEBUG: Warning - Source entity has {sourceTracker.FizzleCardCount} fizzle effects remaining");
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
                Debug.Log($"CARDPLAY_DEBUG: Validation failed - Sequence requirement not met");
                return false;
            }
        }

        // Check if we have valid targets
        if (targetIds == null || targetIds.Length == 0 || targetIds.All(id => id <= 0))
        {
            canPlay = false;
            playBlockReason = "No valid targets";
            Debug.Log($"CARDPLAY_DEBUG: Validation failed - No valid target IDs provided");
            return false;
        }

        // Validate that target entities exist
        var targetEntities = targetIds.Select(id => id > 0 ? FindEntityById(id) : null).Where(e => e != null).ToList();
        if (targetEntities.Count == 0)
        {
            canPlay = false;
            playBlockReason = "No valid targets";
            Debug.Log($"CARDPLAY_DEBUG: Validation failed - Could not find any target entities");
            return false;
        }

        Debug.Log($"CARDPLAY_DEBUG: Found {targetEntities.Count} valid targets");

        // Check energy cost - need to account for already queued cards
        int currentCardCost = card.CardData.EnergyCost;
        int queuedCardsCost = 0;
        
        // Calculate total energy cost of already queued cards for this entity
        if (CombatCardQueue.Instance != null)
        {
            var queuedCards = CombatCardQueue.Instance.GetQueuedCardPlays(sourceId);
            foreach (var queuedCard in queuedCards)
            {
                if (queuedCard.CardData != null)
                {
                    queuedCardsCost += queuedCard.CardData.EnergyCost;
                }
            }
        }
        
        int totalEnergyCost = currentCardCost + queuedCardsCost;
        int availableEnergy = sourceEntity.CurrentEnergy.Value;
        
        if (availableEnergy < totalEnergyCost)
        {
            canPlay = false;
            playBlockReason = $"Not enough energy (need {totalEnergyCost} total: {currentCardCost} for this card + {queuedCardsCost} for queued cards, have {availableEnergy})";
            Debug.Log($"CARDPLAY_DEBUG: Validation failed - Not enough energy for queued cards. Current card: {currentCardCost}, Queued: {queuedCardsCost}, Total needed: {totalEnergyCost}, Available: {availableEnergy}");
            return false;
        }
        
        Debug.Log($"CARDPLAY_DEBUG: Energy validation passed - Current card: {currentCardCost}, Queued: {queuedCardsCost}, Total: {totalEnergyCost}, Available: {availableEnergy}");

        // Check combo requirements
        if (card.CardData.RequiresCombo)
        {
            bool sequenceValid = card.CardData.CanPlayWithCombo(sourceTracker?.ComboCount ?? 0);
            if (!sequenceValid)
            {
                canPlay = false;
                playBlockReason = $"Combo requirement not met (have {(sourceTracker?.ComboCount ?? 0)}, need {card.CardData.RequiredComboAmount})";
                Debug.Log($"CARDPLAY_DEBUG: Validation failed - Combo requirement not met");
                return false;
            }
        }

        Debug.Log($"CARDPLAY_DEBUG: All validation checks passed for {card.CardData.CardName}");
        return true;
    }

    /// <summary>
    /// Checks if the card can be played based on various conditions
    /// </summary>
    private bool CanPlayCard()
    {
        canPlay = true;
        playBlockReason = "";

        Debug.Log($"CARDPLAY_DEBUG: CanPlayCard validation starting for {card?.CardData?.CardName}");

        // Check if we have valid card data
        if (card?.CardData == null)
        {
            canPlay = false;
            playBlockReason = "No card data";
            Debug.Log($"CARDPLAY_DEBUG: Validation failed - No card data");
            return false;
        }

        // Check if we have source entity
        var sourceEntity = sourceAndTargetIdentifier?.SourceEntity;
        if (sourceEntity == null)
        {
            canPlay = false;
            playBlockReason = "No source entity";
            Debug.Log($"CARDPLAY_DEBUG: Validation failed - No source entity (sourceAndTargetIdentifier: {sourceAndTargetIdentifier != null})");
            return false;
        }

        Debug.Log($"CARDPLAY_DEBUG: Found source entity: {sourceEntity.EntityName.Value}");

        // Check for fizzle effects (cards can be played but may have no effect)
        EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
        if (sourceTracker != null && sourceTracker.HasFizzleEffect)
        {
            Debug.Log($"CARDPLAY_DEBUG: Warning - Source entity has {sourceTracker.FizzleCardCount} fizzle effects remaining");
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
                Debug.Log($"CARDPLAY_DEBUG: Validation failed - Sequence requirement not met");
                return false;
            }
        }

        // Check if we have valid targets
        var allTargets = sourceAndTargetIdentifier?.AllTargets;
        if (allTargets == null || allTargets.Count == 0)
        {
            canPlay = false;
            playBlockReason = "No valid targets";
            Debug.Log($"CARDPLAY_DEBUG: Validation failed - No valid targets (allTargets: {(allTargets?.Count ?? 0)})");
            return false;
        }

        Debug.Log($"CARDPLAY_DEBUG: Found {allTargets.Count} valid targets");

        // Check energy cost - need to account for already queued cards
        int currentCardCost = card.CardData.EnergyCost;
        int queuedCardsCost = 0;
        
        // Calculate total energy cost of already queued cards for this entity
        if (CombatCardQueue.Instance != null)
        {
            var queuedCards = CombatCardQueue.Instance.GetQueuedCardPlays(sourceEntity.ObjectId);
            foreach (var queuedCard in queuedCards)
            {
                if (queuedCard.CardData != null)
                {
                    queuedCardsCost += queuedCard.CardData.EnergyCost;
                }
            }
        }
        
        int totalEnergyCost = currentCardCost + queuedCardsCost;
        int availableEnergy = sourceEntity.CurrentEnergy.Value;
        
        if (availableEnergy < totalEnergyCost)
        {
            canPlay = false;
            playBlockReason = $"Not enough energy (need {totalEnergyCost} total: {currentCardCost} for this card + {queuedCardsCost} for queued cards, have {availableEnergy})";
            Debug.Log($"CARDPLAY_DEBUG: Validation failed - Not enough energy for queued cards. Current card: {currentCardCost}, Queued: {queuedCardsCost}, Total needed: {totalEnergyCost}, Available: {availableEnergy}");
            return false;
        }
        
        Debug.Log($"CARDPLAY_DEBUG: Energy validation passed - Current card: {currentCardCost}, Queued: {queuedCardsCost}, Total: {totalEnergyCost}, Available: {availableEnergy}");

        // Check combo requirements
        if (card.CardData.RequiresCombo)
        {
            bool sequenceValid = card.CardData.CanPlayWithCombo(sourceTracker.ComboCount);
            if (!sequenceValid)
            {
                canPlay = false;
                playBlockReason = $"Combo requirement not met (have {sourceTracker.ComboCount}, need {card.CardData.RequiredComboAmount})";
                Debug.Log($"CARDPLAY_DEBUG: Validation failed - Combo requirement not met");
                return false;
            }
        }

        Debug.Log($"CARDPLAY_DEBUG: All validation checks passed for {card.CardData.CardName}");
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
    /// Gets the total energy cost including this card and all currently queued cards
    /// </summary>
    public int GetTotalEnergyCostWithQueued()
    {
        if (card?.CardData == null || sourceAndTargetIdentifier?.SourceEntity == null)
            return 0;
        
        int currentCardCost = card.CardData.EnergyCost;
        int queuedCardsCost = 0;
        
        // Calculate total energy cost of already queued cards for this entity
        if (CombatCardQueue.Instance != null)
        {
            var queuedCards = CombatCardQueue.Instance.GetQueuedCardPlays(sourceAndTargetIdentifier.SourceEntity.ObjectId);
            foreach (var queuedCard in queuedCards)
            {
                if (queuedCard.CardData != null)
                {
                    queuedCardsCost += queuedCard.CardData.EnergyCost;
                }
            }
        }
        
        // If this card is already queued, don't double-count it
        if (isCurrentlyQueued)
        {
            return queuedCardsCost; // This card is already included in the queued cost
        }
        else
        {
            return currentCardCost + queuedCardsCost; // Add this card to the existing queued cost
        }
    }
    
    /// <summary>
    /// Gets the current available energy for the source entity
    /// </summary>
    public int GetAvailableEnergy()
    {
        if (sourceAndTargetIdentifier?.SourceEntity == null)
            return 0;
        
        return sourceAndTargetIdentifier.SourceEntity.CurrentEnergy.Value;
    }

    /// <summary>
    /// Server method to validate and queue card play instead of executing immediately
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void ServerPlayCard(int sourceId, int[] targetIds, NetworkConnection conn = null)
    {
        Debug.Log($"CARDPLAY_DEBUG: ServerPlayCard called for card {gameObject.name} - CardData: {card?.CardData?.CardName}");
        
        if (card?.CardData != null)
        {
            Debug.Log($"CARDPLAY_DEBUG: Card data validated - CardName: {card.CardData.CardName}");
        }
        else
        {
            Debug.LogWarning($"CARDPLAY_DEBUG: Card or CardData is null for GameObject: {gameObject.name}");
            return;
        }
        
        // Validate on server side using passed parameters
        if (!CanPlayCardWithParams(sourceId, targetIds))
        {
            Debug.LogWarning($"CARDPLAY_DEBUG: Server rejected card play. Reason: {playBlockReason}");
            ClientRejectCardPlay(conn, playBlockReason);
            return;
        }

        Debug.Log($"CARDPLAY_DEBUG: Server validation passed for card {gameObject.name}");

        // QUEUE THE CARD PLAY instead of executing immediately
        if (CombatCardQueue.Instance != null)
        {
            Debug.Log($"CARDPLAY_DEBUG: About to queue card play for {card.CardData.CardName} with sourceId: {sourceId}");
            
            // QUEUENAME: Debug card data before queuing
            Debug.Log($"QUEUENAME: HandleCardPlay queuing - card.CardData.CardName='{card.CardData?.CardName ?? "NULL"}', gameObject.name='{gameObject.name}'");
            if (card.CardData != null)
            {
                Debug.Log($"QUEUENAME: CardData details - Type={card.CardData.GetType().Name}, ToString='{card.CardData.ToString()}'");
                // Check if it's a ScriptableObject and get its name
                if (card.CardData is ScriptableObject so)
                {
                    Debug.Log($"QUEUENAME: ScriptableObject name='{so.name}'");
                }
            }
            
            CombatCardQueue.Instance.QueueCardPlay(sourceId, targetIds, gameObject, card.CardData);
            Debug.Log($"CARDPLAY_DEBUG: Queued card play for {card.CardData.CardName}");
            
            // Add visual indicator for queued card on all clients
            RpcSetCardQueuedVisualState(true);
            
            // Update validation for all cards since energy requirements have changed
            RpcUpdateAllCardValidation();
            
            // Verify the card was actually queued
            int queuedCount = CombatCardQueue.Instance.GetQueuedCardCount(sourceId);
            Debug.Log($"CARDPLAY_DEBUG: Queue now has {queuedCount} cards for entity {sourceId}");
        }
        else
        {
            Debug.LogError($"CARDPLAY_DEBUG: CombatCardQueue.Instance is null! Cannot queue card play.");
            return;
        }
        
        Debug.Log($"CARDPLAY_DEBUG: ServerPlayCard completed for card {card?.CardData?.CardName}");
    }

    /// <summary>
    /// Server method to unqueue a card that was previously queued
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void ServerUnqueueCard(int sourceId, NetworkConnection conn = null)
    {
        Debug.Log($"CARDPLAY_DEBUG: ServerUnqueueCard called for card {gameObject.name} - CardData: {card?.CardData?.CardName}");
        
        if (card?.CardData == null)
        {
            Debug.LogWarning($"CARDPLAY_DEBUG: Card or CardData is null for GameObject: {gameObject.name}");
            return;
        }
        
        // Remove the card from the queue
        if (CombatCardQueue.Instance != null)
        {
            bool removed = CombatCardQueue.Instance.RemoveQueuedCard(sourceId, gameObject);
            if (removed)
            {
                Debug.Log($"CARDPLAY_DEBUG: Successfully unqueued card {card.CardData.CardName}");
                
                // Remove visual indicator for queued card on all clients
                RpcSetCardQueuedVisualState(false);
                
                // Update validation for all cards since energy requirements have changed
                RpcUpdateAllCardValidation();
                
                // Verify the card was actually removed
                int queuedCount = CombatCardQueue.Instance.GetQueuedCardCount(sourceId);
                Debug.Log($"CARDPLAY_DEBUG: Queue now has {queuedCount} cards for entity {sourceId}");
            }
            else
            {
                Debug.LogWarning($"CARDPLAY_DEBUG: Failed to unqueue card {card.CardData.CardName} - not found in queue");
            }
        }
        else
        {
            Debug.LogError($"CARDPLAY_DEBUG: CombatCardQueue.Instance is null! Cannot unqueue card.");
        }
        
        Debug.Log($"CARDPLAY_DEBUG: ServerUnqueueCard completed for card {card?.CardData?.CardName}");
    }

    /// <summary>
    /// Executes a card play directly without queuing (used by the queue system)
    /// </summary>
    [Server]
    public void ExecuteCardPlayDirectly(int sourceId, int[] targetIds)
    {
        Debug.Log($"CARDPLAY_DEBUG: ExecuteCardPlayDirectly called for card {gameObject.name} - CardData: {card?.CardData?.CardName}");
        
        if (card?.CardData == null)
        {
            Debug.LogWarning($"CARDPLAY_DEBUG: Card or CardData is null for GameObject: {gameObject.name}");
            return;
        }
        
        // Prevent double processing if already processing
        if (isProcessingCardPlay)
        {
            Debug.LogWarning($"CARDPLAY_DEBUG: Card {card.CardData?.CardName} is already being processed, ignoring duplicate request");
            return;
        }
        
        isProcessingCardPlay = true;
        Debug.Log($"CARDPLAY_DEBUG: Set processing flag for card {card.CardData?.CardName}");

        // Clear the queued visual state when executing on all clients
        RpcSetCardQueuedVisualState(false);

        // TRIGGER CARD PLAY ANIMATION - Do this BEFORE processing effects so the card animates out immediately
        TriggerCardPlayAnimationWithCleanup();

        // Deduct energy cost BEFORE processing effects 
        if (sourceId > 0 && card?.CardData != null && !energyAlreadyDeducted)
        {
            NetworkEntity sourceEntity = FindEntityById(sourceId);
            if (sourceEntity != null)
            {
                int energyCost = card.CardData.EnergyCost;
                
                Debug.Log($"CARDPLAY_DEBUG: Attempting to deduct {energyCost} energy from {sourceEntity.EntityName.Value}");
                
                // Use EnergyHandler for proper server-authoritative energy management
                EnergyHandler energyHandler = sourceEntity.GetComponent<EnergyHandler>();
                if (energyHandler != null)
                {
                    energyHandler.SpendEnergy(energyCost, sourceEntity);
                    Debug.Log($"CARDPLAY_DEBUG: Used EnergyHandler to deduct energy");
                }
                else
                {
                    // Fallback to direct method if no EnergyHandler
                    sourceEntity.ChangeEnergy(-energyCost);
                    Debug.Log($"CARDPLAY_DEBUG: Used fallback direct energy deduction");
                }
                
                energyAlreadyDeducted = true;
                Debug.Log($"CARDPLAY_DEBUG: Deducted {energyCost} energy from {sourceEntity.EntityName.Value}");
            }
        }

        // Check for fizzle effect before processing card effects
        NetworkEntity fizzleSourceEntity = FindEntityById(sourceId);
        bool cardFizzled = false;
        if (fizzleSourceEntity != null)
        {
            EntityTracker sourceTracker = fizzleSourceEntity.GetComponent<EntityTracker>();
            if (sourceTracker != null)
            {
                cardFizzled = sourceTracker.ConsumeAndCheckFizzle();
                if (cardFizzled)
                {
                    Debug.Log($"CARDPLAY_DEBUG: Card {card.CardData?.CardName} fizzled! No effects will be processed.");
                }
            }
        }

        // Process card effects through the resolver - this is the SINGLE processing route
        if (cardEffectResolver != null && !cardFizzled)
        {
            var allTargets = targetIds.Select(id => id > 0 ? FindEntityById(id) : null).Where(e => e != null).ToArray();
            
            Debug.Log($"CARDPLAY_DEBUG: Processing effects - Source: {(sourceId > 0 ? FindEntityById(sourceId)?.EntityName.Value : "null")}, Targets: {(allTargets?.Length ?? 0)}");
            
            if (sourceId > 0 && allTargets != null && allTargets.Length > 0)
            {
                if (fizzleSourceEntity != null)
                {
                    // Use the server-side resolver method for consistent processing
                    foreach (var targetEntity in allTargets)
                    {
                        Debug.Log($"CARDPLAY_DEBUG: Calling ServerResolveCardEffect for target {targetEntity.EntityName.Value}");
                        cardEffectResolver.ServerResolveCardEffect(fizzleSourceEntity, targetEntity, card.CardData);
                    }
                }
            }
            else
            {
                Debug.LogError($"CARDPLAY_DEBUG: Missing entities - Source: {sourceId > 0}, Targets: {(allTargets?.Length ?? 0)}");
            }
        }
        else if (cardFizzled)
        {
            Debug.Log($"CARDPLAY_DEBUG: Card fizzled - skipping effect processing but still recording card play for tracking");
            // Still record the card play for combo tracking even if effects fizzled
            if (fizzleSourceEntity != null)
            {
                EntityTracker sourceTracker = fizzleSourceEntity.GetComponent<EntityTracker>();
                if (sourceTracker != null)
                {
                    sourceTracker.RecordCardPlayed(card.CardData.CardId, card.CardData.BuildsCombo, card.CardData.CardType, card.CardData.EnergyCost == 0);
                }
            }
        }
        else
        {
            Debug.LogError($"CARDPLAY_DEBUG: cardEffectResolver is null!");
        }

        // Handle TestCombat return-to-hand mode
        #if UNITY_EDITOR
        if (TestCombat.Instance != null && TestCombat.Instance.ShouldReturnToHand(gameObject))
        {
            Debug.Log($"CARDPLAY_DEBUG: Using TestCombat return-to-hand mode");
            StartCoroutine(ReturnToHandAfterDelay());
        }
        else
        {
            Debug.Log($"CARDPLAY_DEBUG: Scheduling delayed card discard");
            StartCoroutine(DelayedServerCardDiscard());
        }
        #else
        Debug.Log($"CARDPLAY_DEBUG: Scheduling delayed card discard");
        StartCoroutine(DelayedServerCardDiscard());
        #endif
        
        Debug.Log($"CARDPLAY_DEBUG: ExecuteCardPlayDirectly completed for card {card?.CardData?.CardName}");
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
                
                // Call cleanup if we're the owner OR if we're on the server (for AI-played cards)
                if (IsOwner || IsServerInitialized)
                {
                    Debug.Log($"HandleCardPlay: {(IsOwner ? "Card owner" : "Server")} calling cleanup for GameObject: {gameObject.name}");
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
            // If no animator, call cleanup if we're the owner OR if we're on the server (for AI-played cards)
            if (IsOwner || IsServerInitialized)
            {
                Debug.Log($"HandleCardPlay: {(IsOwner ? "Card owner" : "Server")} calling immediate cleanup for GameObject: {gameObject.name}");
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
                
                // Call cleanup if we're the owner OR if we're on the server (for AI-played cards)
                if (IsOwner || IsServerInitialized)
                {
                    Debug.Log($"HandleCardPlay: {(IsOwner ? "Card owner" : "Server")} calling cleanup for GameObject: {gameObject.name}");
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
            // If no animator, call cleanup if we're the owner OR if we're on the server (for AI-played cards)
            if (IsOwner || IsServerInitialized)
            {
                Debug.Log($"HandleCardPlay: {(IsOwner ? "Card owner" : "Server")} calling immediate cleanup for GameObject: {gameObject.name}");
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
    /// Updates play validation (used after queuing/unqueueing cards to refresh energy validation)
    /// </summary>
    public void UpdatePlayValidation()
    {
        // Simply call CanPlayCard to update the validation state and reason
        CanPlayCard();
    }
    
    /// <summary>
    /// Triggers a validation update for all cards in hand when energy state changes
    /// </summary>
    public static void UpdateAllCardValidation()
    {
        // Find all HandleCardPlay components in the scene and update their validation
        var allCardPlayers = FindObjectsByType<HandleCardPlay>(FindObjectsSortMode.None);
        foreach (var cardPlayer in allCardPlayers)
        {
            cardPlayer.UpdatePlayValidation();
        }
    }

    /// <summary>
    /// Client-side cleanup trigger - now smarter about avoiding duplicate disposal
    /// </summary>
    private void ProcessCardPlayCleanup()
    {
        // Allow cleanup if we're the owner OR if we're on the server (for AI-played cards)
        if (!IsOwner && !IsServerInitialized)
        {
            Debug.LogWarning($"HandleCardPlay: Cannot process cleanup, not network owner or server for card {gameObject.name}");
            return;
        }
        
        // Check if the main server disposal path is already handling this card
        if (isProcessingCardPlay)
        {
            Debug.Log($"CARDPLAY_DEBUG: Card {gameObject.name} is already being processed through main server path, skipping animation callback cleanup");
            return;
        }
        
        Debug.Log($"CARDPLAY_DEBUG: ProcessCardPlayCleanup called for {gameObject.name} - using legacy path");
        
        // Call server to handle the cleanup (now uses RequireOwnership = false)
        if (sourceAndTargetIdentifier?.SourceEntity != null && card?.CardData != null)
        {
            CmdProcessCardPlayCleanup(sourceAndTargetIdentifier.SourceEntity.ObjectId, card.CardData.EnergyCost);
        }
    }
    
    /// <summary>
    /// Sets the visual state for a queued card (golden tint)
    /// </summary>
    [ObserversRpc]
    private void RpcSetCardQueuedVisualState(bool isQueued)
    {
        isCurrentlyQueued = isQueued;
        SetCardQueuedVisualState(isQueued);
    }
    
    /// <summary>
    /// Triggers validation update for all cards on all clients
    /// </summary>
    [ObserversRpc]
    private void RpcUpdateAllCardValidation()
    {
        Debug.Log("CARDPLAY_DEBUG: RpcUpdateAllCardValidation - updating validation for all cards");
        UpdateAllCardValidation();
    }
    
    /// <summary>
    /// Sets the visual state for a queued card
    /// </summary>
    private void SetCardQueuedVisualState(bool isQueued)
    {
        if (card == null) return;
        
        // Update the queued state tracking
        isCurrentlyQueued = isQueued;
        
        // Get the card's Image component
        UnityEngine.UI.Image cardImage = card.GetComponent<UnityEngine.UI.Image>();
        if (cardImage == null)
        {
            // Try to find it in Card component
            cardImage = card.GetCardImage();
        }
        
        if (cardImage != null)
        {
            if (isQueued)
            {
                // Apply a golden/orange tint to indicate the card is queued
                Color queuedColor = new Color(1f, 0.8f, 0.3f, 1f); // Golden tint
                cardImage.color = queuedColor;
                Debug.Log($"CARDPLAY_DEBUG: Applied queued visual state to {card.CardData.CardName}");
            }
            else
            {
                // Restore original color
                cardImage.color = Color.white;
                Debug.Log($"CARDPLAY_DEBUG: Removed queued visual state from {card.CardData.CardName}");
            }
        }
        else
        {
            Debug.LogWarning($"CARDPLAY_DEBUG: Could not find Image component on card {gameObject.name} for visual state");
        }
    }
    
    /// <summary>
    /// Server RPC to handle energy deduction and card discarding
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void CmdProcessCardPlayCleanup(int sourceEntityId, int energyCost)
    {
        Debug.Log($"CARDPLAY_DEBUG: CmdProcessCardPlayCleanup called (legacy path) for sourceEntityId: {sourceEntityId}");
        
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
            Debug.Log($"HandleCardPlay: Deducted {energyCost} energy from {sourceEntity.EntityName.Value} during cleanup");
        }
        else
        {
            Debug.Log($"HandleCardPlay: Energy already deducted for {sourceEntity.EntityName.Value}, skipping deduction in cleanup");
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
        Debug.Log($"HandleCardPlay: TriggerVisualEffects called - Source: {sourceEntity?.EntityName.Value}, Target: {targetEntity?.EntityName.Value}, Card: {cardData?.CardName}");
        
        if (sourceEntity == null || targetEntity == null || cardData == null) 
        {
            Debug.LogError($"HandleCardPlay: TriggerVisualEffects - Missing parameters - Source: {sourceEntity != null}, Target: {targetEntity != null}, CardData: {cardData != null}");
            return;
        }
        
        bool hasTriggeredAttackAnimation = false;
        float maxEffectDuration = 0f;
        
        Debug.Log($"HandleCardPlay: TriggerVisualEffects - Processing {cardData.Effects.Count} effects for card {cardData.CardName}");
        
        // Process each effect that needs visual representation
        foreach (var effect in cardData.Effects)
        {
            Debug.Log($"HandleCardPlay: TriggerVisualEffects - Processing effect {effect.effectType} with animation behavior {effect.animationBehavior}");
            
            if (!ShouldEffectTriggerVisual(effect, cardData))
            {
                /* Debug.Log($"HandleCardPlay: TriggerVisualEffects - Effect {effect.effectType} skipped (no visual needed)"); */
                continue;
            }
                
                            Debug.Log($"HandleCardPlay: TriggerVisualEffects - Effect {effect.effectType} will trigger visual");
            
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
            Debug.Log($"HandleCardPlay: TriggerVisualEffects - Triggering visual for effect {effect.effectType}");
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
                // EVENT-DRIVEN: No longer using hardcoded delay, monitoring actual animation completion
                Coroutine damageCoroutine = EffectAnimationManager.Instance.StartCoroutine(TriggerDamageAnimationDelayed(targetEntity));
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
    /// Coroutine to trigger default effect and wait for animation completion
    /// EVENT-DRIVEN: Uses frame-based monitoring instead of hardcoded delay
    /// </summary>
    private System.Collections.IEnumerator TriggerDefaultEffectDelayed(NetworkEntity sourceEntity, NetworkEntity targetEntity, float duration, float maxWaitTime = 3f)
    {
        Debug.Log($"HandleCardPlay: Starting default effect from {sourceEntity.EntityName.Value} to {targetEntity.EntityName.Value}");
        EffectAnimationManager.TriggerEffectAnimation(sourceEntity, targetEntity, duration);
        
        // EVENT-DRIVEN: Monitor for effect completion instead of fixed delay
        // We'll use the duration as expected time, but also monitor for actual completion
        float startTime = Time.time;
        float expectedEndTime = startTime + duration;
        
        while (Time.time < expectedEndTime && Time.time - startTime < maxWaitTime)
        {
            yield return null; // Check every frame
        }
        
        float actualDuration = Time.time - startTime;
        Debug.Log($"HandleCardPlay: Default effect completed after {actualDuration:F2}s (expected: {duration:F2}s)");
    }

    /// <summary>
    /// Coroutine to trigger named custom effect and wait for completion
    /// EVENT-DRIVEN: Uses frame-based monitoring instead of hardcoded delay
    /// </summary>
    private System.Collections.IEnumerator TriggerNamedCustomEffectDelayed(NetworkEntity sourceEntity, NetworkEntity targetEntity, string effectName, float duration, float maxWaitTime = 3f)
    {
        Debug.Log($"HandleCardPlay: Starting custom effect '{effectName}' from {sourceEntity.EntityName.Value} to {targetEntity.EntityName.Value}");
        EffectAnimationManager.TriggerNamedCustomEffect(sourceEntity, targetEntity, effectName, duration);
        
        // EVENT-DRIVEN: Monitor for effect completion instead of fixed delay
        float startTime = Time.time;
        float expectedEndTime = startTime + duration;
        
        while (Time.time < expectedEndTime && Time.time - startTime < maxWaitTime)
        {
            yield return null; // Check every frame
        }
        
        float actualDuration = Time.time - startTime;
        Debug.Log($"HandleCardPlay: Custom effect '{effectName}' completed after {actualDuration:F2}s (expected: {duration:F2}s)");
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
    /// Coroutine to trigger finishing animation with frame-based delay
    /// EVENT-DRIVEN: Uses frame-based timing instead of hardcoded delay
    /// </summary>
    private System.Collections.IEnumerator TriggerFinishingAnimationDelayed(NetworkEntity targetEntity, CardEffect effect, float delaySeconds)
    {
        // Convert delay seconds to frames for more responsive timing
        float startTime = Time.time;
        float targetTime = startTime + delaySeconds;
        
        while (Time.time < targetTime)
        {
            yield return null; // Frame-based waiting
        }
        
        // Trigger finishing animation
        EffectAnimationManager.TriggerNamedCustomEffect(targetEntity, targetEntity, effect.finishingAnimationName, 0f);
        
        // Trigger finishing sound effect if specified
        if (!string.IsNullOrEmpty(effect.finishingSoundEffectName))
        {
            Vector3 soundPosition = targetEntity.transform.position;
            SoundEffectManager.TriggerNamedSoundEffect(soundPosition, effect.finishingSoundEffectName, 
                (uint)targetEntity.ObjectId, (uint)targetEntity.ObjectId);
        }
        
        float actualDelay = Time.time - startTime;
        Debug.Log($"HandleCardPlay: Triggered finishing animation '{effect.finishingAnimationName}' on {targetEntity.EntityName.Value} after {actualDelay:F2}s (expected: {delaySeconds:F2}s)");
    }
    
    /// <summary>
    /// Coroutine to trigger instant effect with frame-based delay
    /// EVENT-DRIVEN: Uses frame-based timing instead of hardcoded delay
    /// </summary>
    private System.Collections.IEnumerator TriggerInstantEffectDelayed(NetworkEntity targetEntity, CardEffect effect, float delaySeconds)
    {
        // Convert delay seconds to frames for more responsive timing
        float startTime = Time.time;
        float targetTime = startTime + delaySeconds;
        
        while (Time.time < targetTime)
        {
            yield return null; // Frame-based waiting
        }
        
        // For instant effects, we could trigger a different type of particle system
        // that plays directly on the target without projectile movement
        float actualDelay = Time.time - startTime;
        Debug.Log($"HandleCardPlay: Triggered instant {effect.effectType} effect on {targetEntity.EntityName.Value} after {actualDelay:F2}s (expected: {delaySeconds:F2}s)");
        
        // TODO: Implement instant effect visuals (could be a different particle system or just animation)
    }
    
    /// <summary>
    /// Coroutine to trigger source-only effect with frame-based delay
    /// EVENT-DRIVEN: Uses frame-based timing instead of hardcoded delay
    /// </summary>
    private System.Collections.IEnumerator TriggerSourceEffectDelayed(NetworkEntity sourceEntity, CardEffect effect, float delaySeconds)
    {
        // Convert delay seconds to frames for more responsive timing
        float startTime = Time.time;
        float targetTime = startTime + delaySeconds;
        
        while (Time.time < targetTime)
        {
            yield return null; // Frame-based waiting
        }
        
        float actualDelay = Time.time - startTime;
        Debug.Log($"HandleCardPlay: Triggered source-only {effect.effectType} effect on {sourceEntity.EntityName.Value} after {actualDelay:F2}s (expected: {delaySeconds:F2}s)");
        
        // TODO: Implement source-only effect visuals (buffs, self-heals, etc.)
    }
    
    /// <summary>
    /// Coroutine to trigger area effect with frame-based delay
    /// EVENT-DRIVEN: Uses frame-based timing instead of hardcoded delay
    /// </summary>
    private System.Collections.IEnumerator TriggerAreaEffectDelayed(NetworkEntity sourceEntity, NetworkEntity targetEntity, CardEffect effect, float delaySeconds)
    {
        // Convert delay seconds to frames for more responsive timing
        float startTime = Time.time;
        float targetTime = startTime + delaySeconds;
        
        while (Time.time < targetTime)
        {
            yield return null; // Frame-based waiting
        }
        
        float actualDelay = Time.time - startTime;
        Debug.Log($"HandleCardPlay: Triggered area {effect.effectType} effect from {sourceEntity.EntityName.Value} after {actualDelay:F2}s (expected: {delaySeconds:F2}s)");
        
        // TODO: Implement area effect visuals (could affect multiple targets)
    }
    
    /// <summary>
    /// Triggers damage animation on target entity and waits for completion
    /// EVENT-DRIVEN: Monitors actual animation state instead of hardcoded delay
    /// </summary>
    private System.Collections.IEnumerator TriggerDamageAnimationDelayed(NetworkEntity targetEntity)
    {
        int targetId = (int)targetEntity.NetworkObject.ObjectId;
        
        NetworkEntityAnimator targetAnimator = targetEntity.GetComponent<NetworkEntityAnimator>();
        if (targetAnimator == null)
        {
            Debug.LogWarning($"HandleCardPlay: No NetworkEntityAnimator found on target entity {targetEntity.EntityName.Value}");
            yield break;
        }

        Debug.Log($"HandleCardPlay: Triggering damage animation on {targetEntity.EntityName.Value}");
        targetAnimator.PlayTakeDamageAnimation();
        
        // EVENT-DRIVEN: Monitor for animation completion instead of fixed delay
        float startTime = Time.time;
        bool animationCompleted = false;
        
        while (Time.time - startTime < 2f && !animationCompleted) // Default max wait time
        {
            // Check if damage animation has completed
            // (NetworkEntityAnimator tracks isPlayingDamageAnimation)
            if (!targetAnimator.IsPlayingDamageAnimation)
            {
                animationCompleted = true;
                Debug.Log($"HandleCardPlay: Damage animation completed on {targetEntity.EntityName.Value} after {Time.time - startTime:F2}s");
            }
            
            yield return null; // Check every frame
        }
        
        if (!animationCompleted)
        {
            Debug.LogWarning($"HandleCardPlay: Damage animation timeout after 2s on {targetEntity.EntityName.Value}");
        }
        
        // Remove this coroutine from tracking
        if (activeDamageAnimations.ContainsKey(targetId))
        {
            activeDamageAnimations.Remove(targetId);
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
    /// Delayed server-side card discarding with proper animation timing
    /// EVENT-DRIVEN: Monitors animation state and waits for dissolve to complete
    /// </summary>
    private System.Collections.IEnumerator DelayedServerCardDiscard()
    {
        // EVENT-DRIVEN: Monitor card animation state instead of fixed delay
        // Wait for card play animation to start and progress
        Card card = GetComponent<Card>();
        CardAnimator cardAnimator = GetComponent<CardAnimator>();
        
        // Wait a few frames to allow animation setup
        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }
        
        // Start the dissolve animation on the card
        if (cardAnimator != null)
        {
            Debug.Log($"CARDPLAY_DEBUG: Starting dissolve animation for card {card?.CardData?.CardName}");
            cardAnimator.AnimateDissolveOut(() => {
                Debug.Log($"CARDPLAY_DEBUG: Dissolve animation completed for card {card?.CardData?.CardName}");
            });
            
            // Wait for the dissolve animation to complete
            float startTime = Time.time;
            const float maxWaitTime = 2f; // Maximum time to wait for animation completion
            
            while (cardAnimator.IsAnimating && Time.time - startTime < maxWaitTime)
            {
                yield return null; // Check every frame
            }
            
            float actualWaitTime = Time.time - startTime;
            if (cardAnimator.IsAnimating)
            {
                Debug.LogWarning($"CARDPLAY_DEBUG: Dissolve animation timeout after {actualWaitTime:F2}s for {card?.CardData?.CardName}, proceeding anyway");
            }
            else
            {
                Debug.Log($"CARDPLAY_DEBUG: Dissolve animation completed after {actualWaitTime:F2}s for {card?.CardData?.CardName}");
            }
        }
        else
        {
            // No animator, just wait a minimal amount
            Debug.Log($"CARDPLAY_DEBUG: No CardAnimator found for {card?.CardData?.CardName}, skipping dissolve animation");
            yield return null;
        }
        
        // Reset processing flag
        isProcessingCardPlay = false;
        energyAlreadyDeducted = false;
        
        // DIRECT SERVER AUTHORITY - Handle card disposal without RPC
        // This follows the same pattern as ServerPlayCard - server has full authority regardless of ownership
        if (sourceAndTargetIdentifier?.SourceEntity != null)
        {
            NetworkEntity sourceEntity = sourceAndTargetIdentifier.SourceEntity;
            Debug.Log($"CARDPLAY_DEBUG: Server directly handling card disposal for {sourceEntity.EntityName.Value}");
            
            // Find the hand manager and discard the card directly
            HandManager handManager = GetHandManagerForEntity(sourceEntity);
            if (handManager != null)
            {
                // Server directly calls DiscardCard - no RPC needed since server has full authority
                handManager.DiscardCard(gameObject);
                Debug.Log($"CARDPLAY_DEBUG: Server successfully discarded card {card?.CardData?.CardName}");
                
                // Trigger immediate layout update since HandAnimator is waiting for this card removal
                HandLayoutManager handLayoutManager = handManager.GetHandTransform()?.GetComponent<HandLayoutManager>();
                if (handLayoutManager != null)
                {
                    Debug.Log($"CARDPLAY_DEBUG: Triggering layout update after card disposal");
                    handLayoutManager.UpdateLayout();
                }
                else
                {
                    Debug.LogWarning($"CARDPLAY_DEBUG: No HandLayoutManager found to trigger layout update");
                }
            }
            else
            {
                Debug.LogError($"CARDPLAY_DEBUG: Could not find HandManager for entity {sourceEntity.EntityName.Value}");
            }
        }
        else
        {
            Debug.LogError($"CARDPLAY_DEBUG: No source entity available for card disposal");
        }
        
        Debug.Log($"HandleCardPlay: DelayedServerCardDiscard completed for card {card?.CardData?.CardName}");
    }
} 