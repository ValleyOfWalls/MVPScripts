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
            bool sequenceValid = cardData.CanPlayWithSequence(
                sourceTracker.TrackingData.lastPlayedCardType,
                sourceTracker.ComboCount,
                sourceTracker.CurrentStance
            );

            if (!sequenceValid)
            {
                canPlay = false;
                playBlockReason = cardData.GetSequenceRequirementText();
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
    /// Checks if the card meets sequence requirements
    /// </summary>
    public bool MeetsSequenceRequirements()
    {
        var sourceEntity = sourceAndTargetIdentifier?.SourceEntity;
        if (sourceEntity == null || card?.CardData == null)
            return false;

        EntityTracker sourceTracker = sourceEntity.GetComponent<EntityTracker>();
        if (sourceTracker == null)
            return true; // No tracker means no restrictions

        return card.CardData.CanPlayWithSequence(
            sourceTracker.TrackingData.lastPlayedCardType,
            sourceTracker.ComboCount,
            sourceTracker.CurrentStance
        );
    }

    /// <summary>
    /// Gets a description of what this card needs to be played
    /// </summary>
    public string GetCardRequirementsDescription()
    {
        if (card?.CardData == null)
            return "No card data";

        string description = $"Cost: {card.CardData.EnergyCost} energy";

        // Add sequence requirement if any
        string sequenceText = card.CardData.GetSequenceRequirementText();
        if (!string.IsNullOrEmpty(sequenceText))
        {
            description += $"\n{sequenceText}";
        }

        // Add combo information
        if (card.CardData.HasComboModifier)
        {
            description += "\nBuilds combo";
        }

        if (card.CardData.IsFinisher)
        {
            description += "\nFinisher card";
        }

        // Add stance information
        if (card.CardData.AffectsStance)
        {
            description += $"\nChanges stance to {card.CardData.StanceEffect.stanceType}";
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

        // Process the card effect
        if (cardEffectResolver != null)
        {
            // Since we're already on server, call the effect resolver directly
            var sourceEntity = sourceAndTargetIdentifier.SourceEntity;
            var targetEntity = sourceAndTargetIdentifier.AllTargets.Count > 0 ? sourceAndTargetIdentifier.AllTargets[0] : null;
            
            if (sourceEntity != null && targetEntity != null)
            {
                cardEffectResolver.ServerResolveCardEffect(sourceEntity, targetEntity, card.CardData);
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