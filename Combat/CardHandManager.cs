using UnityEngine;
using System.Collections.Generic;
using FishNet.Object;
using System.Collections;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using DG.Tweening;
using FishNet;
using System.Linq;

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
            // Add log here
            Debug.Log($"[ArrangeCardsInHand] Called on {(IsServer ? "Server" : "Client")}. Hand: {this.name}, Card Count: {cardsInHand.Count}");
            
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
            Debug.Log($"[DIAGNOSTIC] ServerDiscardHand called for {this.name}. Total cards in hand: {cardsInHand.Count}");
            
            // If hand is already empty, just notify clients and return
            if (cardsInHand.Count == 0)
            {
                Debug.Log($"[DIAGNOSTIC] Hand already empty for {this.name}, just notifying clients");
                RpcDiscardHand();
                return;
            }
            
            // Log all card names in the hand for debugging
            if (cardsInHand.Count > 0)
            {
                string cardNames = string.Join(", ", cardsInHand.Select(c => c?.CardName ?? "NULL_CARD"));
                Debug.Log($"[DIAGNOSTIC] Cards in hand before discard: {cardNames}");
            }
            
            // Create a temporary list to avoid issues while iterating and removing
            List<Card> cardsToDiscard = new List<Card>();
            
            // Copy only non-null cards
            foreach (Card card in cardsInHand)
            {
                if (card != null)
                {
                    cardsToDiscard.Add(card);
                }
            }
            
            Debug.Log($"[DIAGNOSTIC] Created discard list with {cardsToDiscard.Count} cards, clearing original hand list");
            
            // Clear server's list AFTER creating the copy
            cardsInHand.Clear();
            
            // Despawn all card NetworkObjects properly
            foreach (Card card in cardsToDiscard)
            {
                if (card != null && card.NetworkObject != null && card.NetworkObject.IsSpawned)
                {
                    try
                    {
                        // Schedule despawn after a brief delay to allow the animation to complete
                        // But ONLY if the gameObject is active, otherwise despawn immediately
                        if (this.gameObject.activeInHierarchy)
                        {
                            StartCoroutine(DespawnCardWithDelay(card.NetworkObject, 0.2f));
                        }
                        else
                        {
                            // Despawn immediately if game object is inactive
                            card.NetworkObject.Despawn();
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[DIAGNOSTIC] Error scheduling card despawn: {ex.Message}");
                        // Try a direct despawn as fallback
                        try 
                        {
                            if (card.NetworkObject.IsSpawned)
                                card.NetworkObject.Despawn();
                        }
                        catch {}
                    }
                }
            }
            
            // Tell clients to discard their hands via RPC
            RpcDiscardHand();
            
            Debug.Log($"[DIAGNOSTIC] ServerDiscardHand completed for {this.name}");
        }
        
        /// <summary>
        /// RPC to discard client-side hand representation
        /// </summary>
        [ObserversRpc]
        protected virtual void RpcDiscardHand()
        {
            Debug.Log($"[DIAGNOSTIC] RpcDiscardHand received on {(IsServer ? "Server" : "Client")} for {this.name}. Current hand has {cardsInHand.Count} cards");
            
            if (cardsInHand.Count > 0)
            {
                string cardNames = string.Join(", ", cardsInHand.Select(c => c?.CardName ?? "NULL_CARD"));
                Debug.Log($"[DIAGNOSTIC] Client cards to discard: {cardNames}");
            }
            
            // Destroy all card GameObjects
            int destroyedCount = 0;
            
            // Make a copy to avoid modification issues during iteration
            List<Card> cardsCopy = new List<Card>(cardsInHand);
            
            foreach (Card card in cardsCopy)
            {
                if (card != null && card.gameObject != null)
                {
                    Debug.Log($"[DIAGNOSTIC] RpcDiscardHand destroying card: {card.CardName}");
                    
                    // Create a quick fade out animation
                    try
                    {
                        // Only animate if we have the components
                        CanvasGroup canvasGroup = card.GetComponent<CanvasGroup>();
                        if (canvasGroup != null)
                        {
                            // Create a unique tween ID
                            string tweenId = $"DiscardCard_{card.GetInstanceID()}";
                            
                            // Kill any existing tweens
                            DOTween.Kill(tweenId);
                            
                            // Create a simple fade out + scale down animation
                            Sequence sequence = DOTween.Sequence();
                            sequence.stringId = tweenId;
                            
                            sequence.Append(canvasGroup.DOFade(0f, 0.3f));
                            sequence.Join(card.transform.DOScale(Vector3.zero, 0.3f));
                            
                            // Set safety check to prevent null reference errors
                            sequence.OnUpdate(() => {
                                if (card == null || card.gameObject == null || !card.gameObject.activeInHierarchy)
                                {
                                    sequence.Kill(false);
                                }
                            });
                            
                            // Destroy the card after animation completes
                            sequence.OnComplete(() => {
                                if (card != null && card.gameObject != null)
                                {
                                    Destroy(card.gameObject);
                                }
                            });
                        }
                        else
                        {
                            // No CanvasGroup, just destroy immediately
                            Destroy(card.gameObject);
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Error while animating card discard: {e.Message}");
                        // Fallback to immediate destruction
                        Destroy(card.gameObject);
                    }
                    
                    destroyedCount++;
                }
                else if (card == null)
                {
                    Debug.LogWarning("[DIAGNOSTIC] RpcDiscardHand found null card in hand");
                }
                else if (card.gameObject == null)
                {
                    Debug.LogWarning($"[DIAGNOSTIC] RpcDiscardHand found card {card.CardName} with null gameObject");
                }
            }
            
            Debug.Log($"[DIAGNOSTIC] RpcDiscardHand destroyed {destroyedCount} cards");
            
            // Clear the list
            cardsInHand.Clear();
            Debug.Log("[DIAGNOSTIC] RpcDiscardHand cleared hand list");
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
                 // Corrected Call: Add Owner and ensure correct parameters
                 if (Owner != null) 
                 {
                      RpcRemoveCardFromHandVisuals(Owner, cardIndex, cardName); // Still tell clients to clean up visuals
                 }
                 else
                 {
                      Debug.LogError($"[Server] Cannot send RpcRemoveCardFromHandVisuals for null card because Owner is null.");
                 }
                return;
            }
            
            // Remove from server list
            cardsInHand.RemoveAt(cardIndex);
            
            // Instead of despawning here, trigger client RPC for animation + destroy
            RpcAnimateAndDestroyCard(card.NetworkObject);
            
            // Tell clients to remove the logical card reference and rearrange
            // Corrected Call: Ensure correct parameters and Owner check
             if (Owner != null)
             {
                 Debug.Log($"[Server] Sending TargetRpc RpcRemoveCardFromHandVisuals to Owner ({Owner.ClientId}) for index {cardIndex}, card '{cardName}' on Hand: {this.name} ({this.NetworkObject.ObjectId})");
                 RpcRemoveCardFromHandVisuals(Owner, cardIndex, cardName); // Use correct parameters
             }
             else
             {
                  Debug.LogError($"[Server] Cannot send TargetRpc RpcRemoveCardFromHandVisuals because Owner connection is null for Hand: {this.name}");
             }
            
            // Rearrange the cards on server
            Debug.Log($"[Server] Calling ArrangeCardsInHand for Hand: {this.name} ({this.NetworkObject.ObjectId})");
            ArrangeCardsInHand();
        }
        #endregion

        #region Observer RPCs
        // Renamed for clarity - handles visual removal and rearrangement
        [TargetRpc]
        private void RpcRemoveCardFromHandVisuals(NetworkConnection conn, int cardIndex, string cardName)
        {
            // Log owner check just in case
            Debug.Log($"[Client TargetRpc] RpcRemoveCardFromHandVisuals received. IsOwner: {IsOwner}. Index: {cardIndex}, Name: {cardName}. Current hand size: {cardsInHand.Count}");
            
            // Find the card visually - important if list order differs slightly
            Card cardToRemove = null;
            if (cardIndex >= 0 && cardIndex < cardsInHand.Count)
            {
                 // Try index first
                 if (cardsInHand[cardIndex] != null && cardsInHand[cardIndex].CardName == cardName) {
                     cardToRemove = cardsInHand[cardIndex];
                     Debug.Log($"[Client TargetRpc] Found card '{cardName}' at index {cardIndex}");
                 }
                 else if(cardsInHand[cardIndex] != null)
                 {
                    Debug.LogWarning($"[Client TargetRpc] Card at index {cardIndex} is {cardsInHand[cardIndex].CardName}, not {cardName}. Searching by name.");
                 }
                 else 
                 {
                    Debug.LogWarning($"[Client TargetRpc] Card at index {cardIndex} is null. Searching by name.");
                 }
            }
            else
            {
                Debug.LogWarning($"[Client TargetRpc] Invalid index {cardIndex} received. Searching by name.");
            }
            
            // Fallback: search by name if index failed or list was modified
            if (cardToRemove == null) {
                Debug.Log($"[Client TargetRpc] Searching for card '{cardName}' by name...");
                foreach(Card card in cardsInHand) {
                    if (card != null && card.CardName == cardName) {
                        cardToRemove = card;
                        Debug.Log($"[Client TargetRpc] Found card '{cardName}' by name search.");
                        break;
                    }
                }
            }

            if (cardToRemove != null)
            {
                // Remove from local list BEFORE arranging
                bool removed = cardsInHand.Remove(cardToRemove);
                Debug.Log($"[Client TargetRpc] Removed card '{cardName}' from local list: {removed}. New hand size: {cardsInHand.Count}");
            }
            else
            {
                 Debug.LogWarning($"[Client TargetRpc] RpcRemoveCardFromHandVisuals: Could not find card {cardName} to remove visually.");
            }
            
            // Arrange remaining cards
            Debug.Log($"[Client TargetRpc] Calling ArrangeCardsInHand after removal attempt.");
            ArrangeCardsInHand();
        }
        
        // NEW RPC: Client handles animation and destruction for a specific card
        [ObserversRpc(BufferLast = false)] 
        private void RpcAnimateAndDestroyCard(NetworkObject cardNetworkObject)
        {
            if (cardNetworkObject == null)
            {
                Debug.LogWarning("RpcAnimateAndDestroyCard received null NetworkObject.");
                return;
            }

            // Find the SPECIFIC card instance by its NetworkObject ID, not by name
            Card targetCard = null;
            uint targetNetworkId = (uint)cardNetworkObject.ObjectId; // Explicit cast here
            
            // Debug logging
            Debug.Log($"[RpcAnimateAndDestroyCard] Animating card with NetworkObject ID: {targetNetworkId}");
            
            // First try to get the card directly from the NetworkObject
            targetCard = cardNetworkObject.GetComponent<Card>();
            
            // If that failed, we'll search through children to find by ID
            if (targetCard == null)
            {
                // We want to be absolutely certain we're only affecting the specific card instance
                foreach (Card card in GetComponentsInChildren<Card>(true))
                {
                    if (card != null && card.NetworkObject != null && card.NetworkObject.ObjectId == targetNetworkId)
                    {
                        targetCard = card;
                        Debug.Log($"[RpcAnimateAndDestroyCard] Found exact card match: {card.CardName} with ID {targetNetworkId}");
                        break;
                    }
                }
            }
            
            if (targetCard == null)
            {
                Debug.LogWarning($"[RpcAnimateAndDestroyCard] Could not find Card component on NetworkObject {cardNetworkObject.ObjectId}. Disabling only this specific object.");
                // Fallback: just disable the object if Card component is missing
                if (cardNetworkObject.gameObject != null) 
                    cardNetworkObject.gameObject.SetActive(false);
                return;
            }
            
            // Store a local reference to the game object to avoid null references during animation
            GameObject cardGameObject = targetCard.gameObject;
            string cardName = targetCard.CardName;
            
            Debug.Log($"[RpcAnimateAndDestroyCard] Found card {cardName} with ID {targetNetworkId}, starting animation");
            
            // Ensure card is not interactable during animation
            CanvasGroup cg = targetCard.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = false;

            // Simple fade-out and scale-down animation
            float duration = 0.4f;
            Sequence sequence = DOTween.Sequence();
            
            // Set an ID for this sequence that's unique to the instance - IMPORTANT
            sequence.stringId = $"Card_{targetNetworkId}_FadeOut";
            
            // Safety check - kill any existing sequences with the same ID
            DOTween.Kill(sequence.stringId);
            
            // Add null checks during animation to prevent errors
            sequence.OnUpdate(() => {
                if (cardGameObject == null || !cardGameObject.activeInHierarchy)
                {
                    if (sequence.IsActive() && !sequence.IsComplete())
                    {
                        sequence.Kill(false);
                    }
                }
            });
            
            // Transform animation
            if (cardGameObject != null && cardGameObject.transform != null)
            {
                sequence.Append(cardGameObject.transform.DOScale(Vector3.zero, duration)
                    .SetEase(Ease.InBack));
                    
                // Add fade if canvas group exists
                if (cg != null)
                {
                    sequence.Join(cg.DOFade(0, duration));
                }
            }
            
            sequence.OnComplete(() =>
            {
                // Check if object hasn't been destroyed already
                if (cardGameObject != null && cardGameObject.activeInHierarchy)
                {
                    Debug.Log($"[RpcAnimateAndDestroyCard] Animation complete, disabling specific card {cardName} with ID {targetNetworkId}");
                    // IMPORTANT: Destroy this specific card, don't just disable it
                    Destroy(cardGameObject);
                }
            });
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
            // --- Log Entry ---
            Debug.Log($"[Server CardHandManager] ServerAddCard called for hand ID: {this.GetInstanceID()}, Card: {(cardData != null ? cardData.cardName : "null")}");

            if (cardData == null)
            {
                Debug.LogError("FALLBACK: Cannot add null card to hand");
                return;
            }

            // If we have a valid DeckManager, use it to create the card
            if (DeckManager.Instance != null)
            {
                // Create the card object
                GameObject cardObj = DeckManager.Instance.CreateCardObject(cardData, this.transform, this, GetCombatant());

                // --- FIX: Explicitly add the created card to the server's list ---
                if (cardObj != null)
                {
                    Card cardComponent = cardObj.GetComponent<Card>();
                    if (cardComponent != null)
                    {
                        if (!cardsInHand.Contains(cardComponent))
                        {
                            cardsInHand.Add(cardComponent);
                            Debug.Log($"[Server CardHandManager ID: {this.GetInstanceID()}] Added card '{cardComponent.CardName}' via ServerAddCard. New count: {cardsInHand.Count}");
                        }
                        else
                        {
                             Debug.LogWarning($"[Server CardHandManager ID: {this.GetInstanceID()}] Card '{cardComponent.CardName}' already in hand list when added via ServerAddCard.");
                        }
                    }
                    else
                    {
                        Debug.LogError($"[Server CardHandManager] Created card object for {cardData.cardName} is missing Card component!");
                    }
                }
                // --- END FIX ---
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
                    
                    // Tell clients to animate and destroy the card visually
                    if (card.NetworkObject != null)
                    {
                        // Trigger animation on clients first (ObserversRpc)
                        RpcAnimateAndDestroyCard(card.NetworkObject);

                        // Wait briefly to allow the animation to start before despawning
                        if (this.gameObject.activeInHierarchy)
                        {
                            // Only start a coroutine if the game object is active
                            StartCoroutine(DespawnCardWithDelay(card.NetworkObject, 0.5f));
                        }
                        else
                        {
                            // If inactive, despawn immediately
                            Debug.Log($"[Server] GameObject inactive, despawning card immediately: {cardName}");
                            if (card.NetworkObject.IsSpawned)
                            {
                                card.NetworkObject.Despawn();
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[ServerRemoveCard] Card '{cardName}' has null NetworkObject, skipping animation");
                    }
                    
                    // Also notify ONLY THE OWNER to update their logical card list (TargetRpc)
                    if (Owner != null) // Ensure Owner connection is valid
                    {
                         Debug.Log($"[Server] Sending TargetRpc RpcRemoveCardFromHandVisuals to Owner ({Owner.ClientId}) for index {i}, card '{cardName}' on Hand: {this.name} ({this.NetworkObject.ObjectId})"); 
                         RpcRemoveCardFromHandVisuals(Owner, i, cardName); // Pass Owner connection
                    }
                    else
                    {
                        Debug.LogError($"[Server] Cannot send TargetRpc RpcRemoveCardFromHandVisuals because Owner connection is null for Hand: {this.name}");
                    }
                    
                    // Rearrange the cards on server
                    Debug.Log($"[Server] Calling ArrangeCardsInHand for Hand: {this.name} ({this.NetworkObject.ObjectId})");
                    ArrangeCardsInHand();
                    
                    return;
                }
            }
            
            Debug.LogWarning($"FALLBACK: Card '{cardName}' not found in hand to remove");
        }
        
        // Helper method to despawn card after a delay
        private IEnumerator DespawnCardWithDelay(NetworkObject cardNetworkObject, float delay)
        {
            // Safety check for null references before starting
            if (cardNetworkObject == null)
            {
                Debug.LogWarning("[DIAGNOSTIC] DespawnCardWithDelay received null NetworkObject");
                yield break;
            }
            
            // Wait for animation to start - do this outside the try block
            yield return new WaitForSeconds(delay);
            
            try
            {
                // Check if the card is still spawned before despawning
                if (cardNetworkObject != null && cardNetworkObject.IsSpawned)
                {
                    cardNetworkObject.Despawn();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DIAGNOSTIC] Error in DespawnCardWithDelay: {ex.Message}");
                // Try a direct despawn as fallback in case of exception
                try
                {
                    if (cardNetworkObject != null && cardNetworkObject.IsSpawned)
                        cardNetworkObject.Despawn();
                }
                catch {}
            }
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

        // Server-side method to remove a card at specific index
        [Server]
        public virtual void RemoveCardAtIndex(int index)
        {
            if (index < 0 || index >= cardsInHand.Count)
            {
                Debug.LogError($"[Server] RemoveCardAtIndex: Invalid index {index} for hand with {cardsInHand.Count} cards");
                return;
            }
            
            Card card = cardsInHand[index];
            if (card == null)
            {
                Debug.LogError($"[Server] RemoveCardAtIndex: Card at index {index} is null");
                cardsInHand.RemoveAt(index);
                return;
            }
            
            // Get the card name for logging
            string cardName = card.CardName;
            
            // Remove the card from the hand list
            cardsInHand.RemoveAt(index);
            
            // Tell clients to animate and destroy the card visually
            if (card.NetworkObject != null)
            {
                // Trigger animation on clients first (ObserversRpc)
                RpcAnimateAndDestroyCard(card.NetworkObject);

                // Wait briefly to allow the animation to start before despawning
                if (this.gameObject.activeInHierarchy)
                {
                    // Only start a coroutine if the game object is active
                    StartCoroutine(DespawnCardWithDelay(card.NetworkObject, 0.5f));
                }
                else
                {
                    // If inactive, despawn immediately
                    Debug.Log($"[Server] GameObject inactive, despawning card immediately: {cardName}");
                    if (card.NetworkObject.IsSpawned)
                    {
                        card.NetworkObject.Despawn();
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[RemoveCardAtIndex] Card '{cardName}' has null NetworkObject, skipping animation");
            }
            
            // Notify ONLY THE OWNER to update their logical card list (TargetRpc)
            if (Owner != null) 
            {
                Debug.Log($"[Server] Sending TargetRpc RpcRemoveCardFromHandVisuals to Owner ({Owner.ClientId}) for index {index}, card '{cardName}' on Hand: {this.name} ({this.NetworkObject.ObjectId})"); 
                RpcRemoveCardFromHandVisuals(Owner, index, cardName);
            }
            else
            {
                Debug.LogError($"[Server] Cannot send TargetRpc RpcRemoveCardFromHandVisuals because Owner connection is null for Hand: {this.name}");
            }
            
            // Rearrange the cards on server
            Debug.Log($"[Server] Calling ArrangeCardsInHand for Hand: {this.name} ({this.NetworkObject.ObjectId}) after removing card at index {index}");
            ArrangeCardsInHand();
        }

        // Get a list of cards in the hand
        public List<Card> GetCardsInHand()
        {
            return new List<Card>(cardsInHand);
        }
        #endregion
    }
} 