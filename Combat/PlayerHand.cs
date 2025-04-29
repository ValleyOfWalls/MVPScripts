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
        [SerializeField] private float arcHeight = 30f;
        [SerializeField] private float arcWidth = 500f;
        
        [Header("References")]
        [SerializeField] private NetworkPlayer owner;
        [SerializeField] private CombatPlayer combatPlayer;
        
        // Current hand of cards
        private readonly List<Card> cardsInHand = new List<Card>();
        
        // Reference to owned cards
        private RuntimeDeck ownerDeck;
        
        public int HandSize => cardsInHand.Count;

        private void Awake()
        {
            Debug.Log($"PlayerHand Awake - Card Parent: {(cardParent != null ? "Found" : "Missing")}, Card Prefab: {(cardPrefab != null ? "Found" : "Missing")}");
            
            // Ensure card parent exists
            if (cardParent == null)
            {
                Debug.LogWarning("No card parent assigned to PlayerHand, creating one");
                cardParent = new GameObject("CardParent").transform;
                cardParent.SetParent(transform);
                cardParent.localPosition = Vector3.zero;
            }
        }
        
        // Only the owner can see their own hand
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            Debug.Log($"PlayerHand OnStartClient - IsOwner: {IsOwner}");
            
            // Only show the hand UI to the owner
            if (!IsOwner)
            {
                if (cardParent != null)
                    cardParent.gameObject.SetActive(false);
            }
            else
            {
                // Make sure card parent is active for owner
                if (cardParent != null)
                    cardParent.gameObject.SetActive(true);
            }
        }
        
        [Server]
        public void Initialize(NetworkPlayer player, CombatPlayer combatPlayerRef)
        {
            owner = player;
            combatPlayer = combatPlayerRef;
            
            Debug.Log($"PlayerHand initialized for player {(player != null ? player.GetSteamName() : "Unknown")}");
        }
        
        [Client]
        public void DrawInitialHand(int cardCount)
        {
            if (!IsOwner) return;
            
            Debug.Log($"DrawInitialHand called for {cardCount} cards. IsOwner: {IsOwner}");
            
            CmdDrawCards(cardCount);
        }
        
        [ServerRpc]
        public void CmdDrawCards(int count)
        {
            // Only the owner can request cards
            if (!IsOwner) return;
            
            Debug.Log($"[Server] CmdDrawCards - Drawing {count} cards for player {(owner != null ? owner.GetSteamName() : "Unknown")}");
            
            // Draw the requested number of cards
            for (int i = 0; i < count; i++)
            {
                if (cardsInHand.Count >= maxHandSize)
                {
                    Debug.Log($"Hand is full ({maxHandSize} cards), stopping draw");
                    break;
                }
                
                // Get a card from the player's deck
                CardData card = combatPlayer.DrawCardFromDeck();
                if (card != null)
                {
                    // Send the card to the client
                    TargetAddCardToHand(Owner, card);
                }
                else
                {
                    Debug.LogError("DrawCardFromDeck returned null card");
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
            
            // Log the card draw event
            Debug.Log($"[Combat] Player {(owner != null ? owner.GetSteamName() : "Unknown")} drew card: {cardData.cardName}");
            
            try
            {
                // Instantiate a new card GameObject
                GameObject cardObj = Instantiate(cardPrefab, cardParent);
                
                if (cardObj == null)
                {
                    Debug.LogError("Failed to instantiate card prefab");
                    return;
                }
                
                Card card = cardObj.GetComponent<Card>();
                
                if (card != null)
                {
                    // Initialize the card with the data
                    card.Initialize(cardData, this, combatPlayer);
                    
                    // Add to the hand
                    cardsInHand.Add(card);
                    
                    Debug.Log($"Card {cardData.cardName} added to hand, current hand size: {cardsInHand.Count}");
                    
                    // Setup the card's Canvas and CanvasGroup if needed
                    Canvas cardCanvas = cardObj.GetComponent<Canvas>();
                    if (cardCanvas == null)
                    {
                        cardCanvas = cardObj.AddComponent<Canvas>();
                        cardCanvas.overrideSorting = true;
                        cardCanvas.sortingOrder = 100;
                    }
                    
                    CanvasGroup canvasGroup = cardObj.GetComponent<CanvasGroup>();
                    if (canvasGroup == null)
                    {
                        canvasGroup = cardObj.AddComponent<CanvasGroup>();
                    }
                    
                    // Make sure card is visible
                    canvasGroup.alpha = 1f;
                    
                    // Animate the card being drawn
                    cardObj.transform.localPosition = new Vector3(0, -300, 0); // Start position (off-screen)
                    cardObj.transform.localScale = Vector3.zero;
                    
                    // First show the card
                    cardObj.transform.DOScale(Vector3.one, dealAnimationDuration)
                        .SetEase(Ease.OutBack);
                    
                    // Position the cards in the hand after a slight delay
                    DOVirtual.DelayedCall(dealAnimationDuration * 0.5f, () => {
                        ArrangeCardsInHand();
                        Debug.Log($"Hand arranged with {cardsInHand.Count} cards");
                    });
                }
                else
                {
                    Debug.LogError("Card component not found on card prefab");
                    Destroy(cardObj);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error adding card to hand: {e.Message}\n{e.StackTrace}");
            }
        }
        
        // Called when a card is played
        public void OnCardPlayed(Card card)
        {
            if (cardsInHand.Contains(card))
            {
                // Remove from hand
                cardsInHand.Remove(card);
                
                Debug.Log($"Card {card.CardName} removed from hand, current hand size: {cardsInHand.Count}");
                
                // Add to discard pile
                if (ownerDeck != null && card.Data != null)
                {
                    ownerDeck.DiscardCard(card.Data);
                }
                
                // Re-arrange remaining cards
                ArrangeCardsInHand();
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
            
            Debug.Log($"Arranging {cardsInHand.Count} cards in hand");
            
            // Calculate total width
            float totalWidth = cardSpacing * (cardsInHand.Count - 1);
            float startX = -totalWidth / 2f;
            
            for (int i = 0; i < cardsInHand.Count; i++)
            {
                Card card = cardsInHand[i];
                if (card == null)
                {
                    Debug.LogWarning($"Null card at index {i} in cardsInHand");
                    continue;
                }
                
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
                
                // Ensure card is visible (reset opacity)
                CanvasGroup canvasGroup = card.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                }
                
                // Set the sorting order based on position (left to right, increasing)
                Canvas cardCanvas = card.GetComponent<Canvas>();
                if (cardCanvas != null)
                {
                    cardCanvas.sortingOrder = 100 + i;
                }
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
            
            Debug.Log($"Discarding entire hand of {cardsInHand.Count} cards");
            
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
        
        // Set the deck this hand is drawing from
        public void SetDeck(RuntimeDeck deck)
        {
            ownerDeck = deck;
            Debug.Log($"PlayerHand deck set: {(deck != null ? deck.DeckName : "null")}");
        }
        
        // Add a card to the hand
        public void AddCard(Card card)
        {
            if (card != null && !cardsInHand.Contains(card))
            {
                cardsInHand.Add(card);
                Debug.Log($"Card {card.CardName} added directly to hand");
                ArrangeCardsInHand();
            }
        }
        
        // Arrange cards in a nice arc formation
        public void ArrangeCards()
        {
            if (cardsInHand.Count == 0) return;
            
            int cardCount = cardsInHand.Count;
            
            // Calculate the total width needed
            float totalWidth = cardSpacing * (cardCount - 1);
            float startX = -totalWidth / 2f;
            
            // Arrange cards in an arc
            for (int i = 0; i < cardCount; i++)
            {
                Card card = cardsInHand[i];
                if (card == null) continue;
                
                // Calculate position along the arc
                float xPos = startX + (i * cardSpacing);
                float normalizedPos = xPos / (arcWidth / 2f); // -1 to 1
                
                // Calculate height based on parabola (highest in the middle)
                float yPos = -arcHeight * normalizedPos * normalizedPos + arcHeight;
                
                // Calculate rotation (tilt based on position)
                float zRot = normalizedPos * 10f; // More tilt at the edges
                
                // Set the card's local position and rotation
                card.transform.localPosition = new Vector3(xPos, yPos, 0);
                card.transform.localRotation = Quaternion.Euler(0, 0, zRot);
                
                // Set the sorting order (cards on the left have higher order)
                Canvas canvas = card.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.sortingOrder = cardCount - i;
                }
            }
        }
    }
} 