using UnityEngine;
using System.Collections.Generic;
using FishNet.Object;
using System.Collections;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using DG.Tweening;
using FishNet;

namespace Combat
{
    /// <summary>
    /// Abstract base class that handles common functionality for card hands
    /// Derived classes should implement specific player and pet hand behavior
    /// </summary>
    public abstract class CardHandManager : NetworkBehaviour, IHand
    {
        #region Fields and Properties
        [Header("Hand Settings")]
        [SerializeField] protected int maxHandSize = 10;
        [SerializeField] protected float cardSpacing = 0.8f;
        [SerializeField] protected float arcHeight = 30f;
        [SerializeField] protected float arcWidth = 500f;
        
        // List to track cards in the hand
        protected readonly List<Card> cardsInHand = new List<Card>();
        
        // Reference to the deck this hand draws from 
        protected RuntimeDeck runtimeDeck;
        
        public int HandSize => cardsInHand.Count;
        #endregion

        #region Abstract Members
        // These methods must be implemented by derived classes
        protected abstract void SetupReferences();
        protected abstract ICombatant GetCombatant();
        protected abstract void HandleCardPlayed(Card card, int cardIndex);
        public abstract void DrawInitialHand(int count);
        public abstract void DrawCards(int count);
        #endregion

        #region Unity Lifecycle and Network Callbacks
        public override void OnStartClient()
        {
            base.OnStartClient();
            this.gameObject.SetActive(true);
            
            // Setup references on client start
            SetupReferences();
            
            // Schedule card discovery to ensure all cards are in the right place
            StartCoroutine(DelayedCardDiscovery());
        }
        #endregion

        #region Card Management
        /// <summary>
        /// Update the visual arrangement of cards in the hand
        /// </summary>
        public virtual void ArrangeCardsInHand()
        {
            int cardCount = cardsInHand.Count;
            if (cardCount == 0) return;
            
            // Use simple horizontal layout instead of arc-based layout
            float cardWidth = 120f;
            float spacing = 10f;   // Basic spacing between cards
            float totalWidth = (cardCount * cardWidth) + ((cardCount - 1) * spacing);
            float startX = -totalWidth / 2f + cardWidth / 2f; // Start from the left edge
            
            // Define a consistent z-position for all cards in hand to prevent issues with cards disappearing
            const float consistentZPosition = 0f;
            
            for (int i = 0; i < cardCount; i++)
            {
                Card card = cardsInHand[i];
                if (card == null) continue;
                
                // Position horizontally
                float xPos = startX + (i * (cardWidth + spacing));
                float yPos = 0f; // Keep cards aligned horizontally
                
                // Set positions directly - IMPORTANT: Always use consistent Z position
                card.transform.localPosition = new Vector3(xPos, yPos, consistentZPosition);
                card.transform.localRotation = Quaternion.identity;
                card.transform.localScale = Vector3.one;

                // Update sorting order
                Canvas cardCanvas = card.GetComponent<Canvas>();
                if (cardCanvas != null)
                {
                    cardCanvas.overrideSorting = true;
                    cardCanvas.sortingOrder = 100 + i;
                }
            }
        }

        /// <summary>
        /// Find and register all card objects under this hand
        /// </summary>
        protected virtual void DiscoverCardObjects()
        {
            // Find all Card components that are children of this hand
            Card[] cards = GetComponentsInChildren<Card>(true); // Include inactive cards
            
            // Register any cards not already in our list
            bool newCardsAdded = false;
            foreach (Card card in cards)
            {
                if (card != null && !cardsInHand.Contains(card))
                {
                    cardsInHand.Add(card);
                    newCardsAdded = true;
                }
            }
            
            // Only arrange if we actually added any new cards
            if (newCardsAdded || cardsInHand.Count > 0)
            {
                ArrangeCardsInHand();
            }
        }

        /// <summary>
        /// Register a networked card with this hand
        /// </summary>
        public virtual void RegisterNetworkedCard(Card card)
        {
            if (card == null) return;
            
            // Make sure it's not already in the list
            if (!cardsInHand.Contains(card))
            {
                cardsInHand.Add(card);
                ArrangeCardsInHand();
            }
        }

        /// <summary>
        /// Called when a card is played from this hand
        /// </summary>
        public virtual void OnCardPlayed(Card card)
        {
            if (card == null) return;
            
            // Find the index of the card in the hand
            int cardIndex = cardsInHand.IndexOf(card);
            
            if (cardIndex != -1)
            {
                // Let the derived class handle specific card play logic
                HandleCardPlayed(card, cardIndex);
            }
            else
            {
                Debug.LogError($"FALLBACK: Card {card.CardName} not found in hand");
            }
        }
        
        /// <summary>
        /// Helper method to set the interactivity of all cards in hand
        /// </summary>
        protected virtual void SetCardsInteractivity(bool interactive)
        {
            foreach (Card card in cardsInHand)
            {
                if (card != null)
                {
                    CanvasGroup canvasGroup = card.GetComponent<CanvasGroup>();
                    if (canvasGroup != null)
                    {
                        canvasGroup.interactable = interactive;
                        canvasGroup.blocksRaycasts = interactive;
                    }
                }
            }
        }
        
        private IEnumerator DelayedCardDiscovery()
        {
            // Wait for a short time to ensure all objects are spawned
            yield return new WaitForSeconds(0.5f);
            
            // Discover and arrange cards
            DiscoverCardObjects();
        }
        #endregion

        #region Common Functionality
        public virtual void SetDeck(RuntimeDeck deck)
        {
            this.runtimeDeck = deck;
        }
        
        /// <summary>
        /// Server-side method to discard all cards in hand
        /// </summary>
        [Server]
        public virtual void ServerDiscardHand()
        {
            // Create a temporary list to avoid issues while iterating and removing
            List<Card> cardsToDiscard = new List<Card>(cardsInHand);
            cardsInHand.Clear(); // Clear server's list
            
            // Derived classes should call this base method
            // and then handle deck discard logic specific to their implementation
            
            // Tell clients to discard their hands via RPC
            RpcDiscardHand();
        }
        
        /// <summary>
        /// RPC to discard client-side hand representation
        /// </summary>
        [ObserversRpc]
        protected virtual void RpcDiscardHand()
        {
            // Destroy all card GameObjects
            foreach (Card card in cardsInHand)
            {
                if (card != null && card.gameObject != null)
                {
                    Destroy(card.gameObject);
                }
            }
            
            // Clear the list
            cardsInHand.Clear();
        }
        
        /// <summary>
        /// RPC to set the parent of this hand
        /// </summary>
        [ObserversRpc(ExcludeOwner = false, BufferLast = true)]
        public virtual void RpcSetParent(NetworkObject parentNetworkObject)
        {
            if (parentNetworkObject == null)
            {
                Debug.LogError("FALLBACK: RpcSetParent received null parentNetworkObject");
                return;
            }

            Transform parentTransform = parentNetworkObject.transform;
            if (parentTransform != null)
            {
                transform.SetParent(parentTransform, false);
            }
            else
            {
                Debug.LogError("FALLBACK: Could not find transform for parent NetworkObject in RpcSetParent");
            }
        }
        #endregion

        #region Server RPCs
        [ServerRpc(RequireOwnership = true)]
        protected void CmdRemoveCardFromHand(int cardIndex, string cardName)
        {
            if (cardIndex < 0 || cardIndex >= cardsInHand.Count)
            {
                Debug.LogError($"[Server] Invalid card index {cardIndex} for hand removal. Hand count: {cardsInHand.Count}, Card: {cardName}");
                return;
            }
            
            Card card = cardsInHand[cardIndex];
            if (card == null)
            {
                Debug.LogError($"[Server] Card at index {cardIndex} is null when trying to remove {cardName}.");
                 // Attempt to remove the null entry anyway and rearrange
                 cardsInHand.RemoveAt(cardIndex);
                 RpcRemoveCardFromHandVisuals(cardIndex, cardName); // Still tell clients to clean up visuals
                return;
            }
            
            // Remove from server list
            cardsInHand.RemoveAt(cardIndex);
            
            // Instead of despawning here, trigger client RPC for animation + destroy
            RpcAnimateAndDestroyCard(card.NetworkObject);
            
            // Tell clients to remove the logical card reference and rearrange
            // Note: RpcRemoveCardFromHandVisuals might be better name now
            RpcRemoveCardFromHandVisuals(cardIndex, cardName);
            
            // Server doesn't arrange visuals
        }
        #endregion

        #region Observer RPCs
        // Renamed for clarity - handles visual removal and rearrangement
        [ObserversRpc]
        private void RpcRemoveCardFromHandVisuals(int cardIndex, string cardName)
        {
            // Find the card visually - important if list order differs slightly
            Card cardToRemove = null;
            if (cardIndex >= 0 && cardIndex < cardsInHand.Count)
            {
                 // Try index first
                 if (cardsInHand[cardIndex] != null && cardsInHand[cardIndex].CardName == cardName) {
                     cardToRemove = cardsInHand[cardIndex];
                 }
            }
            
            // Fallback: search by name if index failed or list was modified
            if (cardToRemove == null) {
                foreach(Card card in cardsInHand) {
                    if (card != null && card.CardName == cardName) {
                        cardToRemove = card;
                        break;
                    }
                }
            }

            if (cardToRemove != null)
            {
                // Remove from local list
                cardsInHand.Remove(cardToRemove);
            }
            else
            {
                 Debug.LogWarning($"RpcRemoveCardFromHandVisuals: Could not find card {cardName} to remove visually.");
            }
            
            // Arrange remaining cards
            ArrangeCardsInHand();
        }
        
        // NEW RPC: Client handles animation and destruction
        [ObserversRpc(BufferLast = false)] // Don't buffer, it's a one-time event
        private void RpcAnimateAndDestroyCard(NetworkObject cardNetworkObject)
        {
            if (cardNetworkObject == null)
            {
                Debug.LogWarning("RpcAnimateAndDestroyCard received null NetworkObject.");
                return;
            }

            Card card = cardNetworkObject.GetComponent<Card>();
            if (card != null)
            {
                // Ensure card is not interactable during animation
                CanvasGroup cg = card.GetComponent<CanvasGroup>();
                if (cg != null) cg.blocksRaycasts = false;

                // Simple fade-out and scale-down animation
                float duration = 0.4f;
                Sequence sequence = DOTween.Sequence();
                sequence.Append(card.transform.DOScale(Vector3.zero, duration).SetEase(Ease.InBack));
                sequence.Join(card.GetComponent<CanvasGroup>()?.DOFade(0, duration)); // Fade out if CanvasGroup exists
                sequence.OnComplete(() =>
                {
                    // Check if object hasn't been destroyed already (e.g., by scene change)
                    if (card != null && card.gameObject != null)
                    {
                        // If we are NOT the server, destroy the GameObject.
                        // The server despawned the NetworkObject, which handles destruction there.
                        if (!IsServer)
                        {
                           Destroy(card.gameObject);
                        }
                    }
                });
            }
            else
            {
                 Debug.LogWarning($"RpcAnimateAndDestroyCard could not find Card component on NetworkObject {cardNetworkObject.ObjectId}. Disabling object.");
                 // Fallback: just disable the object if Card component is missing
                 if (cardNetworkObject.gameObject != null) 
                    cardNetworkObject.gameObject.SetActive(false);
            }
        }

        [ObserversRpc]
        private void RpcClearHand()
        {
            // Clear visual cards on clients
            foreach (Card card in cardsInHand)
            {
                if (card != null) 
                   Destroy(card.gameObject);
            }
            cardsInHand.Clear();
        }
        #endregion

        #region Helper Methods for Cards
        // Server-side method to add a card to the hand
        [Server]
        public virtual void ServerAddCard(CardData cardData)
        {
            if (cardData == null)
            {
                Debug.LogError("FALLBACK: Cannot add null card to hand");
                return;
            }
            
            // If we have a valid DeckManager, use it to create the card
            if (DeckManager.Instance != null)
            {
                // Create the card (this will register it with the hand)
                DeckManager.Instance.CreateCardObject(cardData, this.transform, this, GetCombatant());
            }
            else
            {
                Debug.LogError("FALLBACK: DeckManager.Instance is null, cannot create card object");
            }
        }

        // Server-side method to remove a card by name from the hand
        [Server]
        public virtual void ServerRemoveCard(string cardName)
        {
            if (string.IsNullOrEmpty(cardName))
            {
                Debug.LogError("FALLBACK: Cannot remove card with null or empty name");
                return;
            }
            
            // Find the card with the specified name
            for (int i = 0; i < cardsInHand.Count; i++)
            {
                Card card = cardsInHand[i];
                if (card != null && card.CardName == cardName)
                {
                    // Remove the card from the hand list
                    cardsInHand.RemoveAt(i);
                    
                    // Destroy the card object
                    if (card.NetworkObject != null)
                        card.NetworkObject.Despawn();
                    
                    // Rearrange the remaining cards
                    ArrangeCardsInHand();
                    
                    // Notify clients to remove this card (if needed)
                    RpcRemoveCardFromHandVisuals(i, cardName);
                    
                    return;
                }
            }
            
            Debug.LogWarning($"FALLBACK: Card '{cardName}' not found in hand to remove");
        }

        // Client-side method to animate drawing a card
        public virtual void ClientAnimateCardDraw(int count)
        {
            // Default implementation is to just discover cards
            // Derived classes can override with more elaborate animations
            DiscoverCardObjects();
        }

        // Client-side method to animate a card being played
        public virtual void ClientAnimateCardPlayed(string cardName)
        {
            // Default implementation does nothing
            // Derived classes can override with specific animations
        }

        // Returns whether this hand can animate card draws
        public virtual bool CanAnimateCardDraw()
        {
            return true;
        }

        // Get a list of cards in the hand
        public List<Card> GetCardsInHand()
        {
            return new List<Card>(cardsInHand);
        }
        #endregion
    }
} 