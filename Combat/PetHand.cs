using UnityEngine;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using DG.Tweening;

namespace Combat
{
    public class PetHand : NetworkBehaviour
    {
        [Header("Hand Settings")]
        [SerializeField] private int maxHandSize = 7;
        [SerializeField] private float cardSpacing = 0.8f;
        [SerializeField] private float handCurveHeight = 100f;
        [SerializeField] private float dealAnimationDuration = 0.3f;
        
        [Header("References")]
        [SerializeField] private CombatPet combatPet;
        [SerializeField] private NetworkPlayer petOwner;
        
        // Current hand of cards
        private readonly List<Card> cardsInHand = new List<Card>();
        
        // Reference to pet's deck
        private RuntimeDeck petDeck;
        
        // Synced cards data for networked visibility
        private readonly SyncList<string> syncedCardIDs = new SyncList<string>();
        
        public int HandSize => cardsInHand.Count;

        private void Awake()
        {
            Debug.Log($"PetHand Awake - Initializing");
            
            // Register callback for synced card changes
            syncedCardIDs.OnChange += OnSyncedCardsChanged;
        }
        
        // All clients can see the pet's hand
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            Debug.Log($"PetHand OnStartClient - PetOwner: {(petOwner != null ? petOwner.GetSteamName() : "Unknown")}");
            
            this.gameObject.SetActive(true);
            
            // Pet cards are visible but not interactive for any player
            SetCardsInteractivity(false);
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
        public void Initialize(CombatPet pet)
        {
            combatPet = pet;
            
            // Set owner reference from combat pet if available
            if (combatPet != null && combatPet.ReferencePet != null)
            {
                petOwner = combatPet.ReferencePet.PlayerOwner;
            }
            
            // Get the runtime deck from the combat pet
            if (combatPet != null && combatPet.PetDeck != null)
            {
                petDeck = combatPet.PetDeck;
                Debug.Log($"PetHand initialized with combat pet's deck containing {petDeck.DrawPileCount} cards");
            }
            else
            {
                Debug.LogError("PetHand initialized with null combat pet or pet deck");
            }
            
            string ownerName = "Unknown";
            if (petOwner != null)
                ownerName = petOwner.GetSteamName();
            else if (combatPet != null && combatPet.ReferencePet != null && combatPet.ReferencePet.PlayerOwner != null)
                ownerName = combatPet.ReferencePet.PlayerOwner.GetSteamName();
                
            Debug.Log($"PetHand initialized for combat pet owned by {ownerName}");
        }
        
        [Server]
        public void DrawInitialHand(int cardCount)
        {
            Debug.Log($"[Server] Drawing initial hand of {cardCount} cards for pet");
            DrawCards(cardCount);
        }
        
        [Server]
        public void DrawCards(int count)
        {
            // Ensure combat pet reference is valid
            if (combatPet == null)
            {
                Debug.LogError("[Server] DrawCards - CombatPet is null, cannot draw cards.");
                return;
            }

            // Ensure pet deck reference is valid
            if (petDeck == null)
            {
                Debug.LogError("[Server] DrawCards - Pet deck is null, cannot draw cards.");
                return;
            }
            
            Debug.Log($"[Server] DrawCards - Drawing {count} cards for pet");
            
            // Draw the requested number of cards
            for (int i = 0; i < count; i++)
            {
                if (cardsInHand.Count >= maxHandSize)
                {
                    Debug.Log($"Pet hand is full ({maxHandSize} cards), stopping draw");
                    break;
                }
                
                // Get a card from the combat pet's deck
                CardData card = combatPet.DrawCardFromDeck();
                if (card != null)
                {
                    // Add to synced cards for network visibility
                    syncedCardIDs.Add(card.cardName);
                    
                    // Create card for all clients
                    RpcAddCardToHand(card.cardName);
                }
                else
                {
                    Debug.LogError("Pet drew null card");
                }
            }
        }
        
        [ObserversRpc]
        private void RpcAddCardToHand(string cardName)
        {
            // Find the card data
            if (DeckManager.Instance == null)
            {
                Debug.LogError("DeckManager instance is null in RpcAddCardToHand");
                return;
            }
            
            CardData cardData = DeckManager.Instance.FindCardByName(cardName);
            if (cardData == null)
            {
                Debug.LogError($"Could not find CardData for card: {cardName} in RpcAddCardToHand");
                return;
            }
            
            // Create the card visually for all clients
            CreateCardInHand(cardData);
        }
        
        // Create a card in the pet's hand
        private void CreateCardInHand(CardData cardData)
        {
            try
            {
                // Instantiate card using DeckManager, parent directly to this PetHand transform
                GameObject cardObj = DeckManager.Instance.CreateCardObject(cardData, this.transform, this, combatPet);
                
                if (cardObj == null)
                {
                    Debug.LogError("DeckManager failed to create card object for pet.");
                    return;
                }
                
                // Get the Card component
                Card card = cardObj.GetComponent<Card>();
                
                if (card != null)
                {
                    // Add to the hand list
                    cardsInHand.Add(card);
                    
                    Debug.Log($"Card {cardData.cardName} added to pet hand, current hand size: {cardsInHand.Count}");
                    
                    // Setup the card's Canvas and CanvasGroup
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
                    
                    // Make cards visible but not interactive
                    canvasGroup.alpha = 1f;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                    
                    // Animate the card being drawn
                    cardObj.transform.localPosition = new Vector3(0, -300, 0); // Start position (off-screen)
                    cardObj.transform.localScale = Vector3.zero;
                    
                    // First show the card
                    cardObj.transform.DOScale(Vector3.one, dealAnimationDuration)
                        .SetEase(Ease.OutBack);
                    
                    // Position the cards in the hand after a slight delay
                    DOVirtual.DelayedCall(dealAnimationDuration * 0.5f, () => {
                        ArrangeCardsInHand();
                        Debug.Log($"Pet hand arranged with {cardsInHand.Count} cards");
                    });
                }
                else
                {
                    Debug.LogError("Card component not found on card prefab for pet");
                    Destroy(cardObj);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error adding card to pet hand: {e.Message}\n{e.StackTrace}");
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
                    Debug.Log($"[SyncList] Pet card added: {newItem}");
                    break;
                    
                case SyncListOperation.RemoveAt:
                    Debug.Log($"[SyncList] Pet card removed: {oldItem}");
                    break;
                    
                case SyncListOperation.Clear:
                    Debug.Log("[SyncList] Pet hand cleared");
                    break;
            }
        }
        
        // Called when a pet plays a card
        [Server]
        public void PlayCard(int cardIndex)
        {
            // Ensure the index is valid
            if (cardIndex < 0 || cardIndex >= cardsInHand.Count || cardIndex >= syncedCardIDs.Count)
            {
                Debug.LogError($"[Server] Invalid card index {cardIndex} for pet hand with {cardsInHand.Count} cards");
                return;
            }
            
            // Get card info
            string cardName = syncedCardIDs[cardIndex];
            Debug.Log($"[Server] Pet playing card {cardName} at index {cardIndex}");
            
            // Remove from synced list and notify clients
            syncedCardIDs.RemoveAt(cardIndex);
            RpcRemoveCardFromHand(cardIndex, cardName);
            
            // The actual card effect would be handled by the pet AI
            // combatPet.PlayCard(cardName);
        }
        
        [ObserversRpc]
        private void RpcRemoveCardFromHand(int cardIndex, string cardName)
        {
            if (cardIndex >= 0 && cardIndex < cardsInHand.Count)
            {
                // Remove the card and rearrange
                Destroy(cardsInHand[cardIndex].gameObject);
                cardsInHand.RemoveAt(cardIndex);
                ArrangeCardsInHand();
                Debug.Log($"[Client] Removed card {cardName} from pet's visual hand");
            }
        }
        
        // Update the visual position of all cards in hand
        private void ArrangeCardsInHand()
        {
            int cardCount = cardsInHand.Count;
            if (cardCount == 0) return;
            
            // Calculate the width needed for all cards
            float totalWidth = cardCount * cardSpacing;
            float startX = -totalWidth / 2f;
            
            // Position each card
            for (int i = 0; i < cardCount; i++)
            {
                Card card = cardsInHand[i];
                if (card != null)
                {
                    float t = cardCount > 1 ? (float)i / (cardCount - 1) : 0.5f;
                    float xPos = startX + i * cardSpacing;
                    float yPos = CalculateHandCurve(t) * handCurveHeight;
                    
                    // Animate to the new position
                    card.transform.DOLocalMove(new Vector3(xPos, yPos, 0), dealAnimationDuration)
                        .SetEase(Ease.OutQuint);
                }
            }
        }
        
        // Calculate a curve for the hand shape (parabola)
        private float CalculateHandCurve(float t)
        {
            // Simple parabola: y = -4 * (x - 0.5)^2
            return -4f * (t - 0.5f) * (t - 0.5f);
        }
        
        // Discard the hand
        [Server]
        public void DiscardHand()
        {
            Debug.Log($"[Server] Discarding pet hand with {cardsInHand.Count} cards");
            
            // Clear the synced list
            syncedCardIDs.Clear();
            
            // Notify clients
            RpcDiscardHand();
        }
        
        [ObserversRpc]
        private void RpcDiscardHand()
        {
            Debug.Log($"[Client] Discarding {cardsInHand.Count} cards from pet hand");
            
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
        
        // Set the pet's deck
        [Server]
        public void SetDeck(RuntimeDeck deck)
        {
            petDeck = deck;
            Debug.Log($"[Server] PetHand deck set with {deck.DrawPileCount} cards");
        }
    }
} 