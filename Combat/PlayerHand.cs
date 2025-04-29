using UnityEngine;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using DG.Tweening;

namespace Combat
{
    public class PlayerHand : NetworkBehaviour
    {
        [Header("Hand Settings")]
        [SerializeField] private int maxHandSize = 10;
        [SerializeField] private Transform cardParent;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private float cardSpacing = 0.8f;
        [SerializeField] private float handCurveHeight = 100f;
        [SerializeField] private float dealAnimationDuration = 0.3f;
        
        [Header("References")]
        [SerializeField] private NetworkPlayer owner;
        [SerializeField] private CombatPlayer combatPlayer;
        
        // Current hand of cards
        private readonly List<Card> cardsInHand = new List<Card>();
        
        // Only the owner can see their own hand
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Only show the hand UI to the owner
            if (!IsOwner)
            {
                if (cardParent != null)
                    cardParent.gameObject.SetActive(false);
            }
        }
        
        [Server]
        public void Initialize(NetworkPlayer player, CombatPlayer combatPlayerRef)
        {
            owner = player;
            combatPlayer = combatPlayerRef;
        }
        
        [Client]
        public void DrawInitialHand(int cardCount)
        {
            if (!IsOwner) return;
            
            CmdDrawCards(cardCount);
        }
        
        [ServerRpc]
        private void CmdDrawCards(int count)
        {
            // Only the owner can request cards
            if (!IsOwner) return;
            
            // Draw the requested number of cards
            for (int i = 0; i < count; i++)
            {
                if (cardsInHand.Count >= maxHandSize)
                    break;
                
                // Get a card from the player's deck
                CardData card = combatPlayer.DrawCardFromDeck();
                if (card != null)
                {
                    // Send the card to the client
                    TargetAddCardToHand(Owner, card);
                }
            }
        }
        
        [TargetRpc]
        private void TargetAddCardToHand(NetworkConnection conn, CardData cardData)
        {
            // This only runs on the target client
            if (cardPrefab == null)
            {
                Debug.LogError("Card prefab is null in PlayerHand");
                return;
            }
            
            // Instantiate a new card GameObject
            GameObject cardObj = Instantiate(cardPrefab, cardParent);
            Card card = cardObj.GetComponent<Card>();
            
            if (card != null)
            {
                // Initialize the card with the data
                card.Initialize(
                    cardData.CardName,
                    cardData.Description,
                    cardData.ManaCost,
                    cardData.Type,
                    cardData.BaseValue,
                    this);
                
                // Add to the hand
                cardsInHand.Add(card);
                
                // Animate the card being drawn
                cardObj.transform.localPosition = new Vector3(0, -300, 0); // Start position (off-screen)
                cardObj.transform.localScale = Vector3.zero;
                
                // First show the card
                cardObj.transform.DOScale(Vector3.one, dealAnimationDuration)
                    .SetEase(Ease.OutBack);
                
                // Position the cards in the hand after a slight delay
                DOVirtual.DelayedCall(dealAnimationDuration * 0.5f, () => ArrangeCardsInHand());
            }
            else
            {
                Debug.LogError("Card component not found on card prefab");
                Destroy(cardObj);
            }
        }
        
        // Called when a card is played
        public void OnCardPlayed(Card card)
        {
            // Remove the card from the hand
            if (cardsInHand.Contains(card))
            {
                cardsInHand.Remove(card);
                
                // Tell the server the card was played
                CmdPlayCard(cardsInHand.IndexOf(card), card.CardName, card.Type, card.BaseValue);
                
                // Rearrange the remaining cards
                DOVirtual.DelayedCall(0.5f, () => ArrangeCardsInHand());
            }
        }
        
        [ServerRpc]
        private void CmdPlayCard(int cardIndex, string cardName, CardType cardType, int baseValue)
        {
            if (!IsOwner) return;
            
            // Let the combat player handle the card effect
            combatPlayer.PlayCard(cardName, cardType, baseValue);
        }
        
        // Update the visual position of all cards in hand
        private void ArrangeCardsInHand()
        {
            if (cardsInHand.Count == 0) return;
            
            // Calculate total width
            float totalWidth = cardSpacing * (cardsInHand.Count - 1);
            float startX = -totalWidth / 2f;
            
            for (int i = 0; i < cardsInHand.Count; i++)
            {
                Card card = cardsInHand[i];
                if (card == null) continue;
                
                float xPos = startX + (i * cardSpacing);
                
                // Calculate position on a curve
                float normalizedPos = cardsInHand.Count > 1 ? (float)i / (cardsInHand.Count - 1) : 0.5f;
                float yPos = CalculateHandCurve(normalizedPos) * handCurveHeight;
                
                // Calculate rotation (cards fan outward)
                float rotation = Mathf.Lerp(-10f, 10f, normalizedPos);
                
                // Animate to position
                card.transform.DOLocalMove(new Vector3(xPos, yPos, 0), 0.3f)
                    .SetEase(Ease.OutQuint);
                
                card.transform.DOLocalRotate(new Vector3(0, 0, rotation), 0.3f)
                    .SetEase(Ease.OutQuint);
            }
        }
        
        // Calculate a point on a curve for card positioning (simple parabola)
        private float CalculateHandCurve(float t)
        {
            // Simple parabola curve that peaks at t=0.5
            return -4 * (t - 0.5f) * (t - 0.5f) + 1;
        }
        
        // Discard the entire hand
        [Client]
        public void DiscardHand()
        {
            if (!IsOwner) return;
            
            // Animate discarding each card
            foreach (Card card in cardsInHand)
            {
                if (card != null)
                {
                    // Animate the card flying off screen
                    Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0).normalized;
                    card.transform.DOMove(card.transform.position + (randomDirection * 1000), 0.5f)
                        .SetEase(Ease.InBack);
                    
                    card.transform.DOScale(Vector3.zero, 0.5f);
                    
                    // Destroy after animation
                    Destroy(card.gameObject, 0.5f);
                }
            }
            
            cardsInHand.Clear();
            
            // Tell the server the hand was discarded
            CmdDiscardHand();
        }
        
        [ServerRpc]
        private void CmdDiscardHand()
        {
            if (!IsOwner) return;
            
            // Let the combat player handle discarding
            combatPlayer.DiscardHand();
        }
    }
    
    // Simple data structure to pass card information over the network
    [System.Serializable]
    public class CardData
    {
        public string CardName;
        public string Description;
        public int ManaCost;
        public CardType Type;
        public int BaseValue;
        
        // Default constructor required by FishNet for serialization
        public CardData() { }
        
        public CardData(string name, string desc, int cost, CardType type, int value)
        {
            CardName = name;
            Description = desc;
            ManaCost = cost;
            Type = type;
            BaseValue = value;
        }
    }
} 