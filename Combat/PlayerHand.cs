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
        
        // Synced cards data for networked visibility
        private readonly SyncList<string> syncedCardIDs = new SyncList<string>();
        
        public int HandSize => cardsInHand.Count;

        private void Awake()
        {
            Debug.Log($"PlayerHand Awake - Initializing");
            
            // Register callback for synced card changes
            syncedCardIDs.OnChange += OnSyncedCardsChanged;
        }
        
        // Always show cards, but only make them interactive for the owner
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            Debug.Log($"PlayerHand OnStartClient - IsOwner: {IsOwner}, PlayerName: {(owner != null ? owner.GetSteamName() : "Unknown")}");
            
            this.gameObject.SetActive(true);
            
            // Set interactability based on ownership
            if (!IsOwner)
            {
                // Make cards visible but not interactive for non-owners
                SetCardsInteractivity(false);
            }
            else
            {
                // Make cards fully interactive for the owner
                SetCardsInteractivity(true);
            }
        }
        
        // Helper to set interactivity of all cards
        private void SetCardsInteractivity(bool interactive)
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
            // Ensure owner reference is valid before proceeding
            if (owner == null)
            {
                Debug.LogError("[Server] CmdDrawCards - Owner is null, cannot draw cards.");
                return;
            }

            // Ensure combat player reference is valid
            if (combatPlayer == null)
            {
                // Attempt to find it if missing (maybe Initialize wasn't called correctly?)
                 CombatPlayer[] players = FindObjectsByType<CombatPlayer>(FindObjectsSortMode.None);
                 foreach(var cp in players)
                 {
                     if (cp.NetworkPlayer == owner)
                     {
                         combatPlayer = cp;
                         Debug.LogWarning("[Server] CmdDrawCards - Found missing combatPlayer reference.");
                         break;
                     }
                 }
                 
                 if (combatPlayer == null)
                 {
                      Debug.LogError("[Server] CmdDrawCards - CombatPlayer reference is null even after search, cannot draw cards.");
                      return;
                 }
            }
            
            Debug.Log($"[Server] CmdDrawCards - Drawing {count} cards for player {owner.GetSteamName()}");
            
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
                    // Add to synced cards for network visibility
                    syncedCardIDs.Add(card.cardName);
                    
                    // Send the card to the client
                    TargetAddCardToHand(Owner, card);
                    
                    // Send the card to all other clients (observers)
                    RpcAddCardToObservers(card.cardName);
                }
                else
                {
                    Debug.LogError("DrawCardFromDeck returned null card");
                }
            }
        }
        
        [ObserversRpc]
        private void RpcAddCardToObservers(string cardName)
        {
            // Skip this client if they're the owner (they already got the card via TargetRpc)
            if (IsOwner)
                return;
                
            // Find the card data
            if (DeckManager.Instance == null)
            {
                Debug.LogError("DeckManager instance is null in RpcAddCardToObservers");
                return;
            }
            
            CardData cardData = DeckManager.Instance.FindCardByName(cardName);
            if (cardData == null)
            {
                Debug.LogError($"Could not find CardData for card: {cardName} in RpcAddCardToObservers");
                return;
            }
            
            // Create the card for observer clients
            CreateCardInHand(cardData, false); // false = not interactive
        }
        
        [TargetRpc]
        private void TargetAddCardToHand(NetworkConnection conn, CardData cardData)
        {
            // This only runs on the target client (owner)
            if (DeckManager.Instance == null)
            {
                Debug.LogError("DeckManager instance is null. Cannot create card object.");
                return;
            }
            
            // Log the card draw event
            Debug.Log($"[Client] Received TargetAddCardToHand for card: {cardData?.cardName ?? "NULL"}");
            
            if (cardData == null)
            {
                Debug.LogError("[Client] Received null CardData in TargetAddCardToHand.");
                return;
            }

            // Create card for the owner client
            CreateCardInHand(cardData, true); // true = interactive
        }
        
        // Common method to create a card in hand (used by both owner and observers)
        private void CreateCardInHand(CardData cardData, bool interactive)
        {
            try
            {
                // Instantiate card using DeckManager, parent directly to this PlayerHand transform
                GameObject cardObj = DeckManager.Instance.CreateCardObject(cardData, this.transform, this, combatPlayer);
                
                if (cardObj == null)
                {
                    Debug.LogError("DeckManager failed to create card object.");
                    return;
                }
                
                // Get the Card component (already initialized by DeckManager.CreateCardObject)
                Card card = cardObj.GetComponent<Card>();
                
                if (card != null)
                {
                    // Add to the hand list
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
                    
                    // Make sure card is visible but set interactivity based on ownership
                    canvasGroup.alpha = 1f;
                    canvasGroup.interactable = interactive;
                    canvasGroup.blocksRaycasts = interactive;
                    
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
        
        // Handle synced card changes
        private void OnSyncedCardsChanged(SyncListOperation op, int index, string oldItem, string newItem, bool asServer)
        {
            if (asServer) return; // Only react on clients
            
            // Handle different operations (Add, Remove, etc.)
            switch (op)
            {
                case SyncListOperation.Add:
                    Debug.Log($"[SyncList] Card added: {newItem}");
                    break;
                    
                case SyncListOperation.RemoveAt:
                    Debug.Log($"[SyncList] Card removed: {oldItem}");
                    break;
                    
                case SyncListOperation.Clear:
                    Debug.Log("[SyncList] Hand cleared");
                    break;
            }
        }
        
        // Called when a card is played
        public void OnCardPlayed(Card card)
        {
            if (cardsInHand.Contains(card))
            {
                // Get the index and card name before removing
                int cardIndex = cardsInHand.IndexOf(card);
                string cardName = card.CardName;
                
                // Remove from hand
                cardsInHand.Remove(card);
                
                Debug.Log($"Card {card.CardName} removed from hand, current hand size: {cardsInHand.Count}");
                
                // Update the synced list on server
                if (IsOwner)
                {
                    CmdRemoveCardFromHand(cardIndex, cardName);
                }
                
                // Add to discard pile
                if (ownerDeck != null && card.Data != null)
                {
                    ownerDeck.DiscardCard(card.Data);
                }
                
                // Rearrange the remaining cards
                ArrangeCardsInHand();
            }
        }
        
        [ServerRpc]
        private void CmdRemoveCardFromHand(int cardIndex, string cardName)
        {
            // Ensure the index is valid
            if (cardIndex >= 0 && cardIndex < syncedCardIDs.Count)
            {
                // Remove the card from synced list
                syncedCardIDs.RemoveAt(cardIndex);
                Debug.Log($"[Server] Removed card {cardName} from synced hand");
                
                // Notify all clients to remove the card
                RpcRemoveCardFromHand(cardIndex, cardName);
            }
        }
        
        [ObserversRpc]
        private void RpcRemoveCardFromHand(int cardIndex, string cardName)
        {
            // Skip the owner (they already handled this locally)
            if (IsOwner)
                return;
                
            if (cardIndex >= 0 && cardIndex < cardsInHand.Count)
            {
                // Remove from observers' hands and rearrange
                Destroy(cardsInHand[cardIndex].gameObject);
                cardsInHand.RemoveAt(cardIndex);
                ArrangeCardsInHand();
                Debug.Log($"[Observer] Removed card {cardName} from visual hand");
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