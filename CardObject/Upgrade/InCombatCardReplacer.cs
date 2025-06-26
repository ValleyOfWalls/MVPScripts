using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using System.Linq;

namespace CardUpgrade
{
    /// <summary>
    /// Handles replacing cards in all combat zones during a fight
    /// Attach to: Singleton GameObject in scene (same as CardUpgradeAnimator or GameManager)
    /// </summary>
    public class InCombatCardReplacer : MonoBehaviour
    {
        // Singleton
        public static InCombatCardReplacer Instance { get; private set; }
        
        private void Awake()
        {
            // Singleton setup
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Debug.Log("InCombatCardReplacer initialized successfully");
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// Replace card in all combat zones (hand, deck, discard)
        /// </summary>
        public void ReplaceCardInAllZones(int baseCardId, int upgradedCardId, NetworkEntity entity, bool upgradeAllCopies)
        {
            if (entity == null)
            {
                Debug.LogError("InCombatCardReplacer: Cannot replace cards for null entity");
                return;
            }
            
            Debug.Log($"InCombatCardReplacer: Starting card replacement for entity {entity.EntityName.Value} - {baseCardId} → {upgradedCardId} (All copies: {upgradeAllCopies})");
            
            int totalReplacements = 0;
            
            // Find all card instances with the base card ID
            List<Card> cardInstances = FindAllCardInstances(baseCardId, entity);
            
            if (cardInstances.Count == 0)
            {
                Debug.LogWarning($"InCombatCardReplacer: No card instances found with ID {baseCardId} for entity {entity.EntityName.Value}");
                return;
            }
            
            // Determine how many to replace
            int cardsToReplace = upgradeAllCopies ? cardInstances.Count : 1;
            
            for (int i = 0; i < cardsToReplace && i < cardInstances.Count; i++)
            {
                Card cardInstance = cardInstances[i];
                if (UpdateCardInstance(cardInstance, upgradedCardId))
                {
                    totalReplacements++;
                }
            }
            
            Debug.Log($"InCombatCardReplacer: Successfully replaced {totalReplacements} card instances in combat zones");
        }
        
        /// <summary>
        /// Find all card instances with a specific ID for an entity
        /// </summary>
        private List<Card> FindAllCardInstances(int cardId, NetworkEntity entity)
        {
            List<Card> foundCards = new List<Card>();
            
            // Get the entity's relationship manager to find hand entity
            RelationshipManager relationshipManager = entity.GetComponent<RelationshipManager>();
            if (relationshipManager?.HandEntity == null)
            {
                Debug.LogWarning($"InCombatCardReplacer: Entity {entity.EntityName.Value} has no hand entity in RelationshipManager");
                return foundCards;
            }
            
            NetworkEntity handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
            if (handEntity == null)
            {
                Debug.LogWarning($"InCombatCardReplacer: Hand entity missing NetworkEntity component");
                return foundCards;
            }
            
            // Search in hand
            foundCards.AddRange(FindCardsInHand(cardId, handEntity));
            
            // Search in deck (cards parented to deck transform)
            foundCards.AddRange(FindCardsInDeck(cardId, handEntity));
            
            // Search in discard pile
            foundCards.AddRange(FindCardsInDiscard(cardId, handEntity));
            
            Debug.Log($"InCombatCardReplacer: Found {foundCards.Count} instances of card ID {cardId} across all zones");
            return foundCards;
        }
        
        /// <summary>
        /// Find cards in hand
        /// </summary>
        private List<Card> FindCardsInHand(int cardId, NetworkEntity handEntity)
        {
            List<Card> handCards = new List<Card>();
            
            HandManager handManager = handEntity.GetComponent<HandManager>();
            if (handManager == null)
            {
                Debug.LogWarning("InCombatCardReplacer: Hand entity missing HandManager component");
                return handCards;
            }
            
            // Get hand transform from HandManager
            Transform handTransform = handManager.GetHandTransform();
            if (handTransform != null)
            {
                handCards.AddRange(FindCardsInTransform(cardId, handTransform));
            }
            
            Debug.Log($"InCombatCardReplacer: Found {handCards.Count} cards with ID {cardId} in hand");
            return handCards;
        }
        
        /// <summary>
        /// Find cards in deck
        /// </summary>
        private List<Card> FindCardsInDeck(int cardId, NetworkEntity handEntity)
        {
            List<Card> deckCards = new List<Card>();
            
            // Find deck transform - typically a child of the hand entity or referenced in HandManager
            HandManager handManager = handEntity.GetComponent<HandManager>();
            if (handManager == null) return deckCards;
            
            Transform deckTransform = handManager.GetDeckTransform();
            if (deckTransform != null)
            {
                deckCards.AddRange(FindCardsInTransform(cardId, deckTransform));
            }
            
            Debug.Log($"InCombatCardReplacer: Found {deckCards.Count} cards with ID {cardId} in deck");
            return deckCards;
        }
        
        /// <summary>
        /// Find cards in discard pile
        /// </summary>
        private List<Card> FindCardsInDiscard(int cardId, NetworkEntity handEntity)
        {
            List<Card> discardCards = new List<Card>();
            
            HandManager handManager = handEntity.GetComponent<HandManager>();
            if (handManager == null) return discardCards;
            
            Transform discardTransform = handManager.GetDiscardTransform();
            if (discardTransform != null)
            {
                discardCards.AddRange(FindCardsInTransform(cardId, discardTransform));
            }
            
            Debug.Log($"InCombatCardReplacer: Found {discardCards.Count} cards with ID {cardId} in discard");
            return discardCards;
        }
        
        /// <summary>
        /// Find all cards with a specific ID in a transform hierarchy
        /// </summary>
        private List<Card> FindCardsInTransform(int cardId, Transform parentTransform)
        {
            List<Card> foundCards = new List<Card>();
            
            if (parentTransform == null) return foundCards;
            
            // Search direct children
            for (int i = 0; i < parentTransform.childCount; i++)
            {
                Transform child = parentTransform.GetChild(i);
                Card card = child.GetComponent<Card>();
                
                if (card != null && card.CardData != null && card.CardData.CardId == cardId)
                {
                    foundCards.Add(card);
                }
                
                // Recursively search children (in case cards are nested)
                foundCards.AddRange(FindCardsInTransform(cardId, child));
            }
            
            return foundCards;
        }
        
        /// <summary>
        /// Update a card instance with new card data
        /// </summary>
        private bool UpdateCardInstance(Card cardInstance, int upgradedCardId)
        {
            if (cardInstance == null)
            {
                Debug.LogError("InCombatCardReplacer: Cannot update null card instance");
                return false;
            }
            
            // Get the upgraded card data
            CardData upgradedCardData = CardDatabase.Instance?.GetCardById(upgradedCardId);
            if (upgradedCardData == null)
            {
                Debug.LogError($"InCombatCardReplacer: Could not find card data for upgraded card ID {upgradedCardId}");
                return false;
            }
            
            // Validate the replacement
            if (!ValidateCardReplacement(cardInstance, upgradedCardData))
            {
                return false;
            }
            
            // Store current state before replacement
            CardLocation currentLocation = cardInstance.CurrentContainer;
            Vector3 currentPosition = cardInstance.transform.position;
            Quaternion currentRotation = cardInstance.transform.rotation;
            Transform currentParent = cardInstance.transform.parent;
            
            // Update card data
            bool success = cardInstance.UpdateCardData(upgradedCardData);
            
            if (success)
            {
                // Restore position and state
                cardInstance.transform.position = currentPosition;
                cardInstance.transform.rotation = currentRotation;
                cardInstance.transform.SetParent(currentParent, false);
                cardInstance.SetCurrentContainer(currentLocation);
                
                // Refresh visuals
                cardInstance.RefreshVisuals();
                
                Debug.Log($"InCombatCardReplacer: Successfully updated card {cardInstance.name} to {upgradedCardData.CardName}");
                return true;
            }
            else
            {
                Debug.LogError($"InCombatCardReplacer: Failed to update card data for {cardInstance.name}");
                return false;
            }
        }
        
        /// <summary>
        /// Validate that a card replacement is valid
        /// </summary>
        private bool ValidateCardReplacement(Card card, CardData newCardData)
        {
            if (card?.CardData == null)
            {
                Debug.LogError("InCombatCardReplacer: Card or CardData is null");
                return false;
            }
            
            if (newCardData == null)
            {
                Debug.LogError("InCombatCardReplacer: New CardData is null");
                return false;
            }
            
            // Basic validation - ensure we're not replacing with the same card
            if (card.CardData.CardId == newCardData.CardId)
            {
                Debug.LogWarning($"InCombatCardReplacer: Attempting to replace card {card.CardData.CardName} with itself");
                return false;
            }
            
            // Additional validation could be added here
            // For example: checking if the replacement makes sense, category restrictions, etc.
            
            return true;
        }
        
        /// <summary>
        /// Get all card instances in a specific zone for debugging
        /// </summary>
        public List<Card> GetCardsInZone(NetworkEntity entity, CardLocation zone)
        {
            List<Card> cards = new List<Card>();
            
            RelationshipManager relationshipManager = entity.GetComponent<RelationshipManager>();
            if (relationshipManager?.HandEntity == null) return cards;
            
            NetworkEntity handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
            if (handEntity == null) return cards;
            
            HandManager handManager = handEntity.GetComponent<HandManager>();
            if (handManager == null) return cards;
            
            Transform targetTransform = null;
            switch (zone)
            {
                case CardLocation.Hand:
                    targetTransform = handManager.GetHandTransform();
                    break;
                case CardLocation.Deck:
                    targetTransform = handManager.GetDeckTransform();
                    break;
                case CardLocation.Discard:
                    targetTransform = handManager.GetDiscardTransform();
                    break;
            }
            
            if (targetTransform != null)
            {
                for (int i = 0; i < targetTransform.childCount; i++)
                {
                    Card card = targetTransform.GetChild(i).GetComponent<Card>();
                    if (card != null)
                    {
                        cards.Add(card);
                    }
                }
            }
            
            return cards;
        }
        
        /// <summary>
        /// Get statistics about card replacement operations for debugging
        /// </summary>
        public void LogReplacementStatistics(NetworkEntity entity)
        {
            if (entity == null) return;
            
            Debug.Log($"=== Card Replacement Statistics for {entity.EntityName.Value} ===");
            
            var handCards = GetCardsInZone(entity, CardLocation.Hand);
            var deckCards = GetCardsInZone(entity, CardLocation.Deck);
            var discardCards = GetCardsInZone(entity, CardLocation.Discard);
            
            Debug.Log($"Hand: {handCards.Count} cards");
            Debug.Log($"Deck: {deckCards.Count} cards");
            Debug.Log($"Discard: {discardCards.Count} cards");
            Debug.Log($"Total in-combat cards: {handCards.Count + deckCards.Count + discardCards.Count}");
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
} 