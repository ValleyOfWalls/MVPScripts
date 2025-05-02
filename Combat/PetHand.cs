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
            Debug.Log($"[PetHand] OnStartClient for {gameObject.name}. IsOwner: {IsOwner}. Parent: {(transform.parent != null ? transform.parent.name : "null")}");
            
            // Debug.Log($"PetHand OnStartClient - PetOwner: {(petOwner != null ? petOwner.GetSteamName() : "Unknown")}");
            
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
                // TODO: Check if hand is full based on NetworkObjects, not local list
                // if (cardsInHand.Count >= maxHandSize)
                // {
                //     Debug.Log($"Pet hand is full ({maxHandSize} cards), stopping draw");
                //     break;
                // }
                
                // Get a card from the combat pet's deck
                CardData cardData = combatPet.DrawCardFromDeck();
                if (cardData != null)
                {
                    // SERVER: Create and spawn the card object
                    if (DeckManager.Instance != null)
                    {
                        // Instantiate/Spawn the card, parented to this PetHand
                        GameObject cardObj = DeckManager.Instance.CreateCardObject(cardData, this.transform, this, combatPet);
                        if (cardObj == null)
                        {
                             Debug.LogError($"[Server] DeckManager failed to create/spawn card object '{cardData.cardName}' for pet hand.");
                        }
                        // else: Card successfully created and spawned by DeckManager
                    }
                    else
                    {
                         Debug.LogError("[Server] DeckManager instance is null in PetHand.DrawCards!");
                    }

                    // REMOVED: SyncList add - CardData now synced via Card's SyncVar
                    // syncedCardIDs.Add(cardData.cardName);
                    
                    // REMOVED: RPC call - Clients will get the card via spawn message
                    // RpcAddCardToHand(cardData.cardName);
                }
                else
                {
                    Debug.LogWarning("[Server] Pet drew null card (Deck empty?)");
                    break; // Stop drawing if deck is empty
                }
            }
        }
        
        [ObserversRpc]
        private void RpcAddCardToHand(string cardName)
        {
            // OBSOLETE: Clients no longer create cards here. 
            // They rely on the spawned NetworkObject and Card.OnStartClient.
            // The server manages card object creation and parenting.
            Debug.Log($"[Client] PetHand RpcAddCardToHand received for card: {cardName}. Waiting for Card object spawn.");
            
            // OLD CODE REMOVED:
            // // Find the card data
            // if (DeckManager.Instance == null)
            // {
            //     Debug.LogError("DeckManager instance is null in RpcAddCardToHand");
            //     return;
            // }
            // 
            // CardData cardData = DeckManager.Instance.FindCardByName(cardName);
            // if (cardData == null)
            // {
            //     Debug.LogError($"Could not find CardData for card: {cardName} in RpcAddCardToHand");
            //     return;
            // }
            // 
            // // Create the card visually for all clients
            // CreateCardInHand(cardData);
        }
        
        // Handle synced card changes
        private void OnSyncedCardsChanged(SyncListOperation op, int index, string oldItem, string newItem, bool asServer)
        {
            if (asServer) return; // Only react on clients
            
            // Debug.Log($"[SyncList] PetHand Change: Op={op}, Index={index}, Old='{oldItem}', New='{newItem}'"); // COMMENT OUT
            
            // Handle different operations (Add, Remove, etc.)
            // switch (op)
            // {
            //     case SyncListOperation.Add:
            //         Debug.Log($"[SyncList] Pet card added: {newItem}");
            //         break;
                    
            //     case SyncListOperation.RemoveAt:
            //         Debug.Log($"[SyncList] Pet card removed: {oldItem}");
            //         break;
                    
            //     case SyncListOperation.Clear:
            //         Debug.Log("[SyncList] Pet hand cleared");
            //         break;
            // }
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
            
            // Use simple horizontal layout (similar to CombatCanvasManager.ManualCardArrangement)
            float cardWidth = 120f; // Match value from ManualCardArrangement if possible
            float spacing = 10f;   // Match value from ManualCardArrangement
            float totalWidth = (cardCount * cardWidth) + ((cardCount - 1) * spacing);
            float startX = -totalWidth / 2f + cardWidth / 2f; // Start from the left edge
            
            // Position each card
            for (int i = 0; i < cardCount; i++)
            {
                Card card = cardsInHand[i];
                if (card != null)
                {
                     // Calculate simple horizontal position
                    float xPos = startX + (i * (cardWidth + spacing));
                    float yPos = 0; // Keep cards aligned horizontally
                    
                    // Set position directly
                    card.transform.localPosition = new Vector3(xPos, yPos, 0);
                    card.transform.localRotation = Quaternion.identity; // Reset rotation
                    card.transform.localScale = Vector3.one; // Ensure scale is reset
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
            // Debug.Log($"[Server] PetHand deck set with {deck.DrawPileCount} cards"); // Less important log
        }

        // --- Parenting RPC --- 
        [ObserversRpc(ExcludeOwner = false, BufferLast = true)]
        public void RpcSetParent(NetworkObject parentNetworkObject)
        {
            if (parentNetworkObject == null)
            {
                Debug.LogError($"[PetHand:{NetworkObject.ObjectId}] RpcSetParent received null parentNetworkObject.");
                return;
            }

            Transform parentTransform = parentNetworkObject.transform;
            if (parentTransform != null)
            {
                transform.SetParent(parentTransform, false);
                Debug.Log($"[PetHand:{NetworkObject.ObjectId}] Set parent to {parentTransform.name} ({parentNetworkObject.ObjectId}) via RPC.");
            }
            else
            {
                 Debug.LogError($"[PetHand:{NetworkObject.ObjectId}] Could not find transform for parent NetworkObject {parentNetworkObject.ObjectId} in RpcSetParent.");
            }
        }
    }
} 