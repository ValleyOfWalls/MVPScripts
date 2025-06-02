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

    private void UpdateTrackingData()
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
    /// Counts cards with the same name in hand
    /// </summary>
    private int CountCardsInHand(HandManager handManager, string cardName)
    {
        int count = 0;
        List<GameObject> cardsInHand = handManager.GetCardsInHand();
        
        foreach (GameObject cardObj in cardsInHand)
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
    /// Counts cards with the same name in discard pile
    /// </summary>
    private int CountCardsInDiscard(HandManager handManager, string cardName)
    {
        int count = 0;
        List<GameObject> cardsInDiscard = handManager.GetCardsInDiscard();
        
        foreach (GameObject cardObj in cardsInDiscard)
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
    /// Records that this card was played
    /// </summary>
    [Server]
    public void RecordCardPlayed()
    {
        if (!IsServerInitialized) return;

        _timesPlayedThisFight.Value++;
        
        // Also record in the entity tracker if available
        if (ownerEntity != null)
        {
            EntityTracker entityTracker = ownerEntity.GetComponent<EntityTracker>();
            if (entityTracker != null && card?.CardData != null)
            {
                entityTracker.RecordCardPlayed(
                    card.CardData.CardId, 
                    card.CardData.HasComboModifier, 
                    card.CardData.CardType, 
                    card.CardData.IsZeroCost
                );
            }
        }

        Debug.Log($"CardTracker: Card {card?.CardData?.CardName} played {_timesPlayedThisFight.Value} times this fight");
    }

    /// <summary>
    /// Resets tracking data for a new fight
    /// </summary>
    [Server]
    public void ResetForNewFight()
    {
        if (!IsServerInitialized) return;

        _timesPlayedThisFight.Value = 0;
        UpdateTrackingData();

        Debug.Log($"CardTracker: Reset tracking data for card {card?.CardData?.CardName}");
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
        ownerEntity = entity;
        UpdateTrackingData();
    }
} 