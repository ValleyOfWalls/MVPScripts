using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Tracks card-specific data like play counts and deck composition.
/// Attach to: Card prefabs alongside Card component.
/// </summary>
public class CardTracker : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Card card;
    [SerializeField] private NetworkEntity ownerEntity;

    [Header("Debug Info (Read Only)")]
    [SerializeField, ReadOnly] private CardTrackingData trackingData = new CardTrackingData();

    // Network synchronized tracking data
    private readonly SyncVar<int> _timesPlayedThisFight = new SyncVar<int>();

    // Public accessors
    public int TimesPlayedThisFight => _timesPlayedThisFight.Value;
    public CardTrackingData TrackingData => trackingData;

    private void Awake()
    {
        // Get required references
        if (card == null) card = GetComponent<Card>();

        // Subscribe to sync var changes for debug display
        _timesPlayedThisFight.OnChange += (prev, next, asServer) => UpdateTrackingData();
    }

    private void Start()
    {
        // Get owner entity from card
        if (card != null)
        {
            ownerEntity = card.OwnerEntity;
        }

        // Initialize tracking data
        UpdateTrackingData();
    }

    public void UpdateTrackingData()
    {
        trackingData.timesPlayedThisFight = _timesPlayedThisFight.Value;
        
        // Update deck composition if card data is available
        if (card != null && card.CardData != null)
        {
            trackingData.upgradedVersion = card.CardData.UpgradedVersion;
            UpdateDeckComposition();
        }
    }

    /// <summary>
    /// Updates deck composition tracking
    /// </summary>
    private void UpdateDeckComposition()
    {
        if (ownerEntity == null || card?.CardData == null) return;

        string cardName = card.CardData.CardName;
        
        // Reset counts
        trackingData.cardsWithSameNameInDeck = 0;
        trackingData.cardsWithSameNameInHand = 0;
        trackingData.cardsWithSameNameInDiscard = 0;

        // Get deck composition from NetworkEntityDeck
        NetworkEntityDeck entityDeck = ownerEntity.GetComponent<NetworkEntityDeck>();
        if (entityDeck != null)
        {
            List<int> deckCardIds = entityDeck.GetAllCardIds();
            if (deckCardIds != null)
            {
                trackingData.cardsWithSameNameInDeck = deckCardIds.Count(id => 
                {
                    CardData deckCard = CardDatabase.Instance.GetCardById(id);
                    return deckCard != null && deckCard.CardName == cardName;
                });
            }
        }

        // Count cards in hand
        HandManager handManager = GetHandManagerForEntity(ownerEntity);
        if (handManager != null)
        {
            trackingData.cardsWithSameNameInHand = CountCardsInHand(handManager, cardName);
            trackingData.cardsWithSameNameInDiscard = CountCardsInDiscard(handManager, cardName);
        }
    }

    /// <summary>
    /// Counts cards with the same name in a list of card IDs
    /// </summary>
    private int CountCardsWithName(List<int> cardIds, string cardName)
    {
        int count = 0;
        foreach (int cardId in cardIds)
        {
            CardData cardData = CardDatabase.Instance?.GetCardById(cardId);
            if (cardData != null && cardData.CardName == cardName)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Generic method to count cards with a specific name in any card collection
    /// </summary>
    private int CountCardsWithNameInCollection(List<GameObject> cardCollection, string cardName)
    {
        if (cardCollection == null) return 0;
        
        int count = 0;
        foreach (GameObject cardObj in cardCollection)
        {
            Card cardComponent = cardObj.GetComponent<Card>();
            if (cardComponent?.CardData != null && cardComponent.CardData.CardName == cardName)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Counts cards with the same name in hand
    /// </summary>
    private int CountCardsInHand(HandManager handManager, string cardName)
    {
        return CountCardsWithNameInCollection(handManager.GetCardsInHand(), cardName);
    }

    /// <summary>
    /// Counts cards with the same name in discard pile
    /// </summary>
    private int CountCardsInDiscard(HandManager handManager, string cardName)
    {
        return CountCardsWithNameInCollection(handManager.GetCardsInDiscard(), cardName);
    }

    /// <summary>
    /// Gets the HandManager for an entity
    /// </summary>
    private HandManager GetHandManagerForEntity(NetworkEntity entity)
    {
        if (entity == null) return null;

        // Check if this entity is already a Hand entity
        if (entity.EntityType == EntityType.PlayerHand || entity.EntityType == EntityType.PetHand)
        {
            // Entity is already a hand entity, get HandManager directly
            return entity.GetComponent<HandManager>();
        }

        // Entity is a Player/Pet, find the hand entity through RelationshipManager
        var relationshipManager = entity.GetComponent<RelationshipManager>();
        if (relationshipManager?.HandEntity == null) return null;

        var handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
        if (handEntity == null) return null;

        return handEntity.GetComponent<HandManager>();
    }

    /// <summary>
    /// Records that this card was played
    /// </summary>
    [Server]
    public void RecordCardPlayed()
    {
        if (!IsServerInitialized) return;

        _timesPlayedThisFight.Value++;
        
        // Check for back-to-back play
        if (ownerEntity != null)
        {
            EntityTracker entityTracker = ownerEntity.GetComponent<EntityTracker>();
            if (entityTracker != null && card?.CardData != null)
            {
                CardData cardData = card.CardData;
                bool isComboCard = cardData.BuildsCombo;
                
                // Check if this was played back-to-back with same card type
                if (entityTracker.TrackingData.lastPlayedCardType == cardData.CardType)
                {
                    trackingData.timesPlayedBackToBack++;
                    trackingData.timesPlayedBackToBackLifetime++;
                    
                    // Notify upgrade manager for persistent tracking
                    if (CardUpgradeManager.Instance != null)
                    {
                        CardUpgradeManager.Instance.UpdatePersistentBackToBackCount(cardData.CardId);
                    }
                }
                
                // Check if this is the only card played this turn
                if (entityTracker.TrackingData.cardsPlayedThisTurn == 0) // About to be 1 after this play
                {
                    trackingData.timesOnlyCardPlayedInTurn++;
                    trackingData.timesOnlyCardPlayedInTurnLifetime++;
                    
                    // Notify upgrade manager for persistent tracking
                    if (CardUpgradeManager.Instance != null)
                    {
                        CardUpgradeManager.Instance.UpdatePersistentSoloPlayCount(cardData.CardId);
                    }
                }
                
                entityTracker.RecordCardPlayed(
                    cardData.CardId, 
                    isComboCard, 
                    cardData.CardType, 
                    cardData.IsZeroCost
                );
            }
        }
        
        // Notify the upgrade manager
        if (CardUpgradeManager.Instance != null && card != null && ownerEntity != null)
        {
            CardUpgradeManager.Instance.OnCardPlayed(card, ownerEntity);
        }

        /* Debug.Log($"CardTracker: Card {card?.CardData?.CardName} played {_timesPlayedThisFight.Value} times this fight"); */
    }

    /// <summary>
    /// Resets tracking data for a new fight
    /// </summary>
    [Server]
    public void ResetForNewFight()
    {
        if (!IsServerInitialized) return;

        _timesPlayedThisFight.Value = 0;
        
        // Reset per-fight tracking (keep lifetime data)
        trackingData.timesDrawnThisFight = 0;
        trackingData.timesHeldAtTurnEnd = 0;
        trackingData.timesDiscardedManually = 0;
        trackingData.timesFinalCardInHand = 0;
        trackingData.timesPlayedBackToBack = 0;
        trackingData.timesOnlyCardPlayedInTurn = 0;
        
        UpdateTrackingData();

       // Debug.Log($"CardTracker: Reset tracking data for card {card?.CardData?.CardName}");
    }

    /// <summary>
    /// Checks if a condition is met based on this card's tracking data
    /// </summary>
    public bool CheckCondition(ConditionalType conditionType, int conditionValue)
    {
        switch (conditionType)
        {
            case ConditionalType.IfTimesPlayedThisFight:
                return _timesPlayedThisFight.Value >= conditionValue;
            case ConditionalType.IfCardsInHand:
                return trackingData.cardsWithSameNameInHand >= conditionValue;
            case ConditionalType.IfCardsInDeck:
                return trackingData.cardsWithSameNameInDeck >= conditionValue;
            case ConditionalType.IfCardsInDiscard:
                return trackingData.cardsWithSameNameInDiscard >= conditionValue;
            default:
                return false;
        }
    }

    /// <summary>
    /// Updates the owner entity reference
    /// </summary>
    public void SetOwnerEntity(NetworkEntity entity)
    {
        if (entity != null)
        {
            //Debug.Log($"[CARD_OWNERSHIP] CardTracker {gameObject.name}: SetOwnerEntity called with entity '{entity.EntityName.Value}' (Type: {entity.EntityType}, ObjectId: {entity.GetComponent<NetworkObject>()?.ObjectId})");
        }
        else
        {
            //Debug.Log($"[CARD_OWNERSHIP] CardTracker {gameObject.name}: SetOwnerEntity called with null entity");
        }
        
        ownerEntity = entity;
        UpdateTrackingData();
        
        if (entity != null)
        {
            //Debug.Log($"[CARD_OWNERSHIP] CardTracker {gameObject.name}: Owner entity set to '{entity.EntityName.Value}' successfully");
        }
    }
    
    /// <summary>
    /// Checks if all copies of this card in hand have been played this turn
    /// </summary>
    public bool AllCopiesPlayedFromHand()
    {
        if (card?.CardData == null) return false;
        
        // Get current copies in hand
        int copiesInHand = trackingData.cardsWithSameNameInHand;
        
        // If there are no copies in hand and we've played at least one this fight, then all copies were played
        return copiesInHand == 0 && _timesPlayedThisFight.Value > 0;
    }
    
    /// <summary>
    /// Gets the total number of copies of this card across all zones (deck, hand, discard)
    /// </summary>
    public int GetTotalCopiesCount()
    {
        return trackingData.cardsWithSameNameInDeck + 
               trackingData.cardsWithSameNameInHand + 
               trackingData.cardsWithSameNameInDiscard;
    }
    
    /// <summary>
    /// Checks if this card was played while the entity was in a specific stance
    /// </summary>
    public bool WasPlayedInStance(StanceType stance)
    {
        if (ownerEntity == null) return false;
        
        var entityTracker = ownerEntity.GetComponent<EntityTracker>();
        return entityTracker?.CurrentStance == stance;
    }
    
    /// <summary>
    /// Checks if this card was played as part of a combo sequence
    /// </summary>
    public bool WasPlayedWithCombo()
    {
        if (ownerEntity == null) return false;
        
        var entityTracker = ownerEntity.GetComponent<EntityTracker>();
        return entityTracker?.ComboCount > 0;
    }
    
    /// <summary>
    /// Records that this card was drawn
    /// </summary>
    [Server]
    public void RecordCardDrawn()
    {
        //Debug.Log($"[CARD_UPGRADE] CardTracker.RecordCardDrawn() called for {card?.CardData?.CardName}, IsServerInitialized: {IsServerInitialized}");
        
        if (!IsServerInitialized) 
        {
            //Debug.LogWarning($"[CARD_UPGRADE] RecordCardDrawn() called but server not initialized for {card?.CardData?.CardName}");
            return;
        }
        
        trackingData.timesDrawnThisFight++;
        trackingData.timesDrawnLifetime++;
        
        //Debug.Log($"[CARD_UPGRADE] Updated draw counts for {card?.CardData?.CardName}: thisFight={trackingData.timesDrawnThisFight}, lifetime={trackingData.timesDrawnLifetime}");
        
        // Notify upgrade manager for persistent tracking
        if (CardUpgradeManager.Instance != null && card?.CardData != null)
        {
            CardUpgradeManager.Instance.UpdatePersistentDrawCount(card.CardData.CardId);
            //Debug.Log($"[CARD_UPGRADE] Updated persistent draw count for {card.CardData.CardName}");
        }
        
        //Debug.Log($"CardTracker: Card {card?.CardData?.CardName} drawn {trackingData.timesDrawnThisFight} times this fight, {trackingData.timesDrawnLifetime} times lifetime");
        
        // Check for upgrades after draw (for conditions based on hand composition)
        //Debug.Log($"[CARD_UPGRADE] About to check upgrade conditions: CardUpgradeManager.Instance={CardUpgradeManager.Instance != null}, card={card != null}, ownerEntity={ownerEntity != null}");
        
        if (CardUpgradeManager.Instance != null && card != null && ownerEntity != null)
        {
            Debug.Log($"[CARD_UPGRADE] Checking upgrades after drawing {card?.CardData?.CardName} for owner {ownerEntity.EntityName.Value}");
            CardUpgradeManager.Instance.OnCardDrawn(card, ownerEntity);
        }
        else
        {
          //  Debug.LogWarning($"[CARD_UPGRADE] Cannot check upgrades after drawing {card?.CardData?.CardName}: CardUpgradeManager.Instance={CardUpgradeManager.Instance != null}, card={card != null}, ownerEntity={ownerEntity != null}");
        }
    }
    
    /// <summary>
    /// Records that this card was manually discarded
    /// </summary>
    [Server]
    public void RecordCardDiscarded()
    {
        if (!IsServerInitialized) return;
        
        trackingData.timesDiscardedManually++;
        trackingData.timesDiscardedManuallyLifetime++;
        
        // Notify upgrade manager for persistent tracking
        if (CardUpgradeManager.Instance != null && card?.CardData != null)
        {
            CardUpgradeManager.Instance.UpdatePersistentDiscardCount(card.CardData.CardId);
        }
        
       // Debug.Log($"CardTracker: Card {card?.CardData?.CardName} discarded {trackingData.timesDiscardedManually} times this fight, {trackingData.timesDiscardedManuallyLifetime} times lifetime");
    }
    
    /// <summary>
    /// Records that this card was held at turn end
    /// </summary>
    [Server]
    public void RecordHeldAtTurnEnd()
    {
        if (!IsServerInitialized) return;
        
        trackingData.timesHeldAtTurnEnd++;
        trackingData.timesHeldAtTurnEndLifetime++;
        
        // Notify upgrade manager for persistent tracking
        if (CardUpgradeManager.Instance != null && card?.CardData != null)
        {
            CardUpgradeManager.Instance.UpdatePersistentHeldAtTurnEndCount(card.CardData.CardId);
        }
        
        //Debug.Log($"CardTracker: Card {card?.CardData?.CardName} held at turn end {trackingData.timesHeldAtTurnEnd} times this fight, {trackingData.timesHeldAtTurnEndLifetime} times lifetime");
    }
    
    /// <summary>
    /// Records that this card was the final card in hand
    /// </summary>
    [Server]
    public void RecordFinalCardInHand()
    {
        if (!IsServerInitialized) return;
        
        trackingData.timesFinalCardInHand++;
        trackingData.timesFinalCardInHandLifetime++;
        
        // Notify upgrade manager for persistent tracking
        if (CardUpgradeManager.Instance != null && card?.CardData != null)
        {
            CardUpgradeManager.Instance.UpdatePersistentFinalCardCount(card.CardData.CardId);
        }
        
       // Debug.Log($"CardTracker: Card {card?.CardData?.CardName} was final card in hand {trackingData.timesFinalCardInHand} times this fight, {trackingData.timesFinalCardInHandLifetime} times lifetime");
    }
} 