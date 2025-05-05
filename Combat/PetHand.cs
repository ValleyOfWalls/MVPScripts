using UnityEngine;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using DG.Tweening;
using System.Collections;

namespace Combat
{
    public class PetHand : NetworkBehaviour, IHand
    {
        #region Fields and Properties
        [Header("Hand Settings")]
        [SerializeField] private int maxHandSize = 7;
        [SerializeField] private float cardSpacing = 0.8f;
        [SerializeField] private float arcHeight = 30f;
        
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
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Register callback for synced card changes
            syncedCardIDs.OnChange += OnSyncedCardsChanged;
        }
        #endregion

        #region Network Callbacks
        // All clients can see the pet's hand
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            this.gameObject.SetActive(true);
            
            // Try to find combatPet and petOwner references if they're null
            if (combatPet == null || petOwner == null)
            {
                TryFindReferences();
            }
            
            // Pet cards are visible but not interactive for any player
            SetCardsInteractivity(false);
            
            // Schedule a delayed check to find references if they're still missing
            if (combatPet == null || petOwner == null)
            {
                StartCoroutine(DelayedReferenceSearch());
            }
        }
        #endregion

        #region Reference Finding
        private IEnumerator DelayedReferenceSearch()
        {
            // Wait a bit for parenting to be established
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForSeconds(0.5f);
                
                if (combatPet != null && petOwner != null)
                    break;
                    
                // Try to find references again
                TryFindReferences();
                
                if (combatPet != null && petOwner != null)
                    break;
            }
            
            if (combatPet == null || petOwner == null)
            {
                Debug.LogError("FALLBACK: Failed to find references after multiple attempts");
            }
        }
        
        private void TryFindReferences()
        {
            // Try to find parent Pet
            Transform parent = transform.parent;
            if (parent != null)
            {
                // Try to get Pet from parent
                Pet parentPet = parent.GetComponent<Pet>();
                if (parentPet != null)
                {
                    petOwner = parentPet.PlayerOwner;
                    
                    // Find the CombatPet in children of Pet
                    CombatPet[] combatPets = parent.GetComponentsInChildren<CombatPet>();
                    if (combatPets.Length > 0)
                    {
                        combatPet = combatPets[0];
                    }
                }
            }
            
            // If still not found, try searching in the scene
            if (combatPet == null || petOwner == null)
            {
                // First try to find relevant pets in the scene
                Pet[] pets = FindObjectsByType<Pet>(FindObjectsSortMode.None);
                foreach (var pet in pets)
                {
                    if (pet.PlayerOwner != null && (IsOwner == pet.PlayerOwner.IsOwner))
                    {
                        petOwner = pet.PlayerOwner;
                        
                        // Find CombatPet as child of this Pet
                        CombatPet[] combatPets = pet.GetComponentsInChildren<CombatPet>();
                        if (combatPets.Length > 0)
                        {
                            combatPet = combatPets[0];
                            break;
                        }
                    }
                }
                
                // Last resort: search for CombatPet objects directly
                if (combatPet == null)
                {
                    CombatPet[] allCombatPets = FindObjectsByType<CombatPet>(FindObjectsSortMode.None);
                    if (allCombatPets.Length > 0)
                    {
                        // Prefer the one that belongs to this client if IsOwner
                        foreach (var cp in allCombatPets)
                        {
                            if (cp.ReferencePet != null && cp.ReferencePet.PlayerOwner != null)
                            {
                                if (IsOwner == cp.ReferencePet.PlayerOwner.IsOwner)
                                {
                                    combatPet = cp;
                                    petOwner = cp.ReferencePet.PlayerOwner;
                                    break;
                                }
                            }
                        }
                        
                        // If still not found, just take the first one
                        if (combatPet == null && allCombatPets.Length > 0)
                        {
                            combatPet = allCombatPets[0];
                            if (combatPet.ReferencePet != null)
                            {
                                petOwner = combatPet.ReferencePet.PlayerOwner;
                            }
                        }
                    }
                }
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
        #endregion

        #region Server Initialization
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
            }
            else
            {
                Debug.LogError("FALLBACK: PetHand initialized with null combat pet or pet deck");
            }
        }
        
        [Server]
        public void DrawInitialHand(int cardCount)
        {
            DrawCards(cardCount);
        }
        
        [Server]
        public void DrawCards(int count)
        {
            // Ensure combat pet reference is valid
            if (combatPet == null)
            {
                Debug.LogError("FALLBACK: DrawCards - CombatPet is null, cannot draw cards");
                return;
            }

            // Ensure pet deck reference is valid
            if (petDeck == null)
            {
                Debug.LogError("FALLBACK: DrawCards - Pet deck is null, cannot draw cards");
                return;
            }
            
            // Draw the requested number of cards
            for (int i = 0; i < count; i++)
            {
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
                            Debug.LogError("FALLBACK: DeckManager failed to create/spawn card object for pet hand");
                        }
                    }
                    else
                    {
                        Debug.LogError("FALLBACK: DeckManager instance is null in PetHand.DrawCards");
                    }
                }
                else
                {
                    Debug.LogWarning("FALLBACK: Pet drew null card (Deck empty?)");
                    break; // Stop drawing if deck is empty
                }
            }
        }
        #endregion

        #region Card Management
        // Handle synced card changes
        private void OnSyncedCardsChanged(SyncListOperation op, int index, string oldItem, string newItem, bool asServer)
        {
            if (asServer) return; // Only react on clients
        }
        
        // Add a card to the pet's hand (server-side)
        [Server]
        public void ServerAddCard(CardData cardData)
        {
            if (cardData == null)
            {
                Debug.LogError("FALLBACK: Cannot add null card to pet hand");
                return;
            }
            
            // If we have a valid DeckManager, use it to create the card
            if (DeckManager.Instance != null)
            {
                // Create the card object parented to this hand
                GameObject cardObj = DeckManager.Instance.CreateCardObject(cardData, this.transform, this, combatPet);
                if (cardObj == null)
                {
                    Debug.LogError("FALLBACK: DeckManager failed to create card object for pet hand");
                }
            }
            else
            {
                Debug.LogError("FALLBACK: DeckManager instance is null in PetHand.ServerAddCard");
            }
        }
        
        // Called when a pet plays a card
        [Server]
        public void PlayCard(int cardIndex)
        {
            // Ensure the index is valid
            if (cardIndex < 0 || cardIndex >= cardsInHand.Count || cardIndex >= syncedCardIDs.Count)
            {
                Debug.LogError($"FALLBACK: Invalid card index {cardIndex} for pet hand with {cardsInHand.Count} cards");
                return;
            }
            
            // Get card info
            string cardName = syncedCardIDs[cardIndex];
            
            // Remove from synced list and notify clients
            syncedCardIDs.RemoveAt(cardIndex);
            RpcRemoveCardFromHand(cardIndex, cardName);
        }
        
        // Update the visual position of all cards in hand
        public void ArrangeCardsInHand()
        {
            int cardCount = cardsInHand.Count;
            if (cardCount == 0) return;
            
            // Use arc-based layout similar to PlayerHand
            float cardWidth = 120f;
            float spacing = Mathf.Min(400f / cardCount, cardWidth * 0.8f); // Dynamic spacing
            float totalWidth = spacing * (cardCount - 1);
            float startX = -totalWidth / 2f;
            
            // Position each card
            for (int i = 0; i < cardCount; i++)
            {
                Card card = cardsInHand[i];
                if (card != null)
                {
                    // Calculate position along a gentle arc
                    float xPos = startX + (i * spacing);
                    float normalizedPos = (float)i / (cardCount > 1 ? cardCount - 1 : 1); // 0 to 1
                    float yPos = arcHeight * Mathf.Sin(Mathf.PI * normalizedPos); // Arc using sine
                    
                    // Calculate rotation (fan effect)
                    float rotationAngle = Mathf.Lerp(-5f, 5f, normalizedPos);
                    Quaternion targetRotation = Quaternion.Euler(0, 0, rotationAngle);
                    
                    // Set position directly
                    card.transform.localPosition = new Vector3(xPos, yPos, 0);
                    card.transform.localRotation = targetRotation;
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
        }
        
        // Get a copy of the cards in hand for AI to use
        public List<Card> GetCardsInHand()
        {
            return new List<Card>(cardsInHand);
        }

        // Remove a specific card from hand (for AI usage)
        [Server]
        public void RemoveCard(Card card)
        {
            if (card == null) return;
            
            int cardIndex = cardsInHand.IndexOf(card);
            if (cardIndex >= 0)
            {
                string cardName = card.CardName;
                
                // Remove from synced list and destroy the card
                if (cardIndex < syncedCardIDs.Count)
                {
                    syncedCardIDs.RemoveAt(cardIndex);
                }
                
                // Tell clients to remove the card
                RpcRemoveCardFromHand(cardIndex, cardName);
                
                // Destroy the card on server
                Destroy(card.gameObject);
            }
        }
        #endregion

        #region RPCs
        [ObserversRpc]
        private void RpcRemoveCardFromHand(int cardIndex, string cardName)
        {
            if (cardIndex >= 0 && cardIndex < cardsInHand.Count)
            {
                // Remove the card and rearrange
                Destroy(cardsInHand[cardIndex].gameObject);
                cardsInHand.RemoveAt(cardIndex);
                ArrangeCardsInHand();
            }
        }
        
        [ObserversRpc]
        private void RpcDiscardHand()
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
        
        // Parenting RPC
        [ObserversRpc(ExcludeOwner = false, BufferLast = true)]
        public void RpcSetParent(NetworkObject parentNetworkObject)
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

        #region Discard and Deck Management
        // Discard the hand
        [Server]
        public void DiscardHand()
        {
            // Clear the synced list
            syncedCardIDs.Clear();
            
            // Notify clients
            RpcDiscardHand();
        }
        
        // Set the pet's deck
        [Server]
        public void SetDeck(RuntimeDeck deck)
        {
            petDeck = deck;
        }

        // Server-side discard hand implementation
        [Server]
        public void ServerDiscardHand()
        {
            // Clear synced list
            syncedCardIDs.Clear();
            
            // Get a copy of the cards to avoid issues while modifying the list
            List<Card> cardsToDiscard = new List<Card>(cardsInHand);
            
            // Add the cards to the discard pile in the deck before destroying them
            if (combatPet != null && combatPet.PetDeck != null)
            {
                foreach (Card card in cardsToDiscard)
                {
                    if (card != null && card.Data != null)
                    {
                        // Add card data to discard pile in the deck
                        combatPet.PetDeck.DiscardCard(card.Data);
                    }
                }
            }
            else
            {
                Debug.LogError("FALLBACK: Could not discard pet cards to deck - missing combatPet or petDeck");
            }
            
            // Destroy card objects on server
            foreach (Card card in cardsToDiscard)
            {
                if (card != null)
                {
                    Destroy(card.gameObject);
                }
            }
            
            // Clear local list
            cardsInHand.Clear();
            
            // Notify clients to discard their hand
            RpcDiscardHand();
        }
        #endregion
    }
} 