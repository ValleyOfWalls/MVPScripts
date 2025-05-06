using UnityEngine;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using DG.Tweening;
using System.Collections;

namespace Combat
{
    public class PetHand : CardHandManager
    {
        #region Fields and Properties
        [Header("Pet Hand References")]
        [SerializeField] private CombatPet combatPet;
        [SerializeField] private NetworkPlayer petOwner;
        
        // Synced cards data for networked visibility
        private readonly SyncList<string> syncedCardIDs = new SyncList<string>();
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
            
            // Pet cards are visible but not interactive for any player
            SetCardsInteractivity(false);
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
        #endregion

        #region CardHandManager Implementation
        // Implementation of abstract methods from CardHandManager
        protected override void SetupReferences()
        {
            if (combatPet == null || petOwner == null)
            {
                TryFindReferences();
                
                // Schedule a delayed check to find references if they're still missing
                if (combatPet == null || petOwner == null)
                {
                    StartCoroutine(DelayedReferenceSearch());
                }
            }
        }
        
        protected override ICombatant GetCombatant()
        {
            return combatPet;
        }
        
        protected override void HandleCardPlayed(Card card, int cardIndex)
        {
            if (card != null && cardIndex >= 0 && cardIndex < syncedCardIDs.Count)
            {
                string cardName = syncedCardIDs[cardIndex];
                
                // Remove from synced list and notify clients
                syncedCardIDs.RemoveAt(cardIndex);
                RpcRemoveCardFromHand(cardIndex, cardName);
            }
        }
        
        [Server]
        public override void DrawInitialHand(int cardCount)
        {
            DrawCards(cardCount);
        }
        
        [Server]
        public override void DrawCards(int count)
        {
            // Ensure combat pet reference is valid
            if (combatPet == null)
            {
                Debug.LogError("FALLBACK: DrawCards - CombatPet is null, cannot draw cards");
                return;
            }

            // Ensure pet deck reference is valid
            if (runtimeDeck == null)
            {
                runtimeDeck = combatPet.PetDeck;
                
                if (runtimeDeck == null)
                {
                    Debug.LogError("FALLBACK: DrawCards - Pet deck is null, cannot draw cards");
                    return;
                }
            }
            
            // Draw the requested number of cards
            for (int i = 0; i < count; i++)
            {
                // Check if hand is full
                if (cardsInHand.Count >= maxHandSize)
                {
                    Debug.LogWarning($"FALLBACK: Pet hand is full ({cardsInHand.Count}/{maxHandSize})");
                    break;
                }
                
                // Get a card from the combat pet's deck
                CardData cardData = combatPet.DrawCardFromDeck();
                if (cardData != null)
                {
                    // Use the base class implementation to add the card
                    ServerAddCard(cardData);
                    
                    // Add to synced list - ensure syncedCardIDs and cardsInHand match
                    int lastCardIndex = cardsInHand.Count - 1;
                    if (lastCardIndex >= 0 && lastCardIndex < cardsInHand.Count)
                    {
                        syncedCardIDs.Add(cardData.cardName);
                        Debug.Log($"Added card '{cardData.cardName}' to syncedCardIDs, current count: {syncedCardIDs.Count}");
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
            
            // Get the card from cardsInHand
            Card card = cardsInHand[cardIndex];
            
            // Remove from both lists
            syncedCardIDs.RemoveAt(cardIndex);
            cardsInHand.RemoveAt(cardIndex);
            
            // Tell clients to remove the card
            RpcRemoveCardFromHand(cardIndex, cardName);
            
            // Destroy the card on server
            if (card != null)
            {
                Destroy(card.gameObject);
            }
            
            // Rearrange the cards
            ArrangeCardsInHand();
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
                
                // Remove from both lists
                if (cardIndex < syncedCardIDs.Count)
                {
                    syncedCardIDs.RemoveAt(cardIndex);
                }
                cardsInHand.RemoveAt(cardIndex);
                
                // Tell clients to remove the card
                RpcRemoveCardFromHand(cardIndex, cardName);
                
                // Destroy the card on server
                Destroy(card.gameObject);
                
                // Rearrange the cards
                ArrangeCardsInHand();
            }
        }
        #endregion

        #region RPCs
        [ObserversRpc]
        private void RpcRemoveCardFromHand(int cardIndex, string cardName)
        {
            Debug.Log($"[CLIENT] RpcRemoveCardFromHand: Removing card {cardName} at index {cardIndex} from hand with {cardsInHand.Count} cards");
            
            // Check if hand is already empty - do nothing in this case
            if (cardsInHand.Count == 0)
            {
                Debug.Log($"[CLIENT] RpcRemoveCardFromHand: Hand is already empty, nothing to remove for {cardName}");
                return;
            }
            
            Card cardToRemove = null;
            
            // First try to get the card by index if valid
            if (cardIndex >= 0 && cardIndex < cardsInHand.Count)
            {
                cardToRemove = cardsInHand[cardIndex];
            }
            else
            {
                Debug.LogError($"[CLIENT] RpcRemoveCardFromHand: Invalid index {cardIndex} for hand with {cardsInHand.Count} cards");
                
                // If index is invalid, try to find by name as fallback
                foreach (Card card in cardsInHand)
                {
                    if (card != null && card.CardName == cardName)
                    {
                        cardToRemove = card;
                        cardIndex = cardsInHand.IndexOf(card);
                        Debug.Log($"[CLIENT] Found card '{cardName}' by name instead of index");
                        break;
                    }
                }
            }
            
            if (cardToRemove != null)
            {
                // Store a local reference to prevent null reference exceptions
                GameObject cardObject = cardToRemove.gameObject;
                
                // Remove from list FIRST before destroying to prevent inconsistencies
                if (cardIndex >= 0 && cardIndex < cardsInHand.Count)
                {
                    cardsInHand.RemoveAt(cardIndex);
                }
                else
                {
                    cardsInHand.Remove(cardToRemove);
                }
                
                // Animate and destroy with safety checks
                if (cardObject != null)
                {
                    // Simple fade out animation
                    CanvasGroup canvasGroup = cardObject.GetComponent<CanvasGroup>();
                    if (canvasGroup != null)
                    {
                        // Create a unique ID for this tween
                        string tweenId = $"PetCard_{cardName}_{cardObject.GetInstanceID()}";
                        DOTween.Kill(tweenId); // Kill any existing tween with this ID
                        
                        // Create a sequence for the animation
                        Sequence sequence = DOTween.Sequence();
                        sequence.stringId = tweenId;
                        
                        // Animate fade and scale
                        sequence.Append(canvasGroup.DOFade(0f, 0.3f));
                        sequence.Join(cardObject.transform.DOScale(Vector3.zero, 0.3f));
                        
                        // Add safety check during animation
                        sequence.OnUpdate(() => {
                            if (cardObject == null || !cardObject.activeInHierarchy)
                            {
                                sequence.Kill(false);
                            }
                        });
                        
                        // Destroy the object when animation completes
                        sequence.OnComplete(() => {
                            if (cardObject != null)
                            {
                                Destroy(cardObject);
                            }
                        });
                    }
                    else
                    {
                        // No canvas group, just destroy immediately
                        Destroy(cardObject);
                    }
                }
                
                // Rearrange the remaining cards
                ArrangeCardsInHand();
            }
            else
            {
                Debug.LogWarning($"[CLIENT] RpcRemoveCardFromHand: Could not find card '{cardName}' in hand with {cardsInHand.Count} cards");
            }
        }
        
        [ObserversRpc]
        private void RpcDiscardHand()
        {
            // Invoke the base implementation
            base.RpcDiscardHand();
        }
        #endregion

        #region Server Methods
        // Server-side initialization
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
                runtimeDeck = combatPet.PetDeck;
            }
            else
            {
                Debug.LogError("FALLBACK: PetHand initialized with null combat pet or pet deck");
            }
        }
        
        // Server-side discard hand implementation
        [Server]
        public override void ServerDiscardHand()
        {
            Debug.Log($"[DIAGNOSTIC] PetHand.ServerDiscardHand called for {this.name}. Cards in hand: {cardsInHand.Count}, synced cards: {syncedCardIDs.Count}");
            
            if (cardsInHand.Count == 0)
            {
                Debug.Log($"[DIAGNOSTIC] PetHand.ServerDiscardHand - Hand is already empty, nothing to discard");
                // Ensure synced list is cleared
                syncedCardIDs.Clear();
                
                // Still notify clients to ensure UI is updated properly
                RpcDiscardHand();
                return;
            }
            
            // Add cards to the pet's discard pile first
            if (combatPet != null && combatPet.PetDeck != null)
            {
                List<CardData> cardsToDiscard = new List<CardData>();
                foreach (Card card in cardsInHand)
                {
                    if (card != null && card.Data != null)
                    {
                        cardsToDiscard.Add(card.Data);
                        Debug.Log($"[DIAGNOSTIC] Adding card {card.CardName} to pet's discard pile");
                    }
                }

                if (cardsToDiscard.Count > 0)
                {
                    combatPet.PetDeck.DiscardHand(cardsToDiscard);
                    Debug.Log($"[DIAGNOSTIC] Discarded {cardsToDiscard.Count} cards to pet's discard pile");
                }
            }
            else
            {
                Debug.LogWarning($"[DIAGNOSTIC] Cannot discard to pet deck - CombatPet or deck is null");
            }
            
            // Get a copy of the cards to avoid issues while modifying the list
            List<Card> handCopy = new List<Card>(cardsInHand);
            Debug.Log($"[DIAGNOSTIC] PetHand.ServerDiscardHand - Created discard list with {handCopy.Count} cards");
            
            // Clear synced card list
            syncedCardIDs.Clear();
            
            // Clear hand list
            cardsInHand.Clear();
            Debug.Log($"[DIAGNOSTIC] Cleared cards in hand list and synced IDs list");
            
            // Despawn all card NetworkObjects
            foreach (Card card in handCopy)
            {
                if (card != null && card.NetworkObject != null && card.NetworkObject.IsSpawned)
                {
                    card.NetworkObject.Despawn();
                }
            }
            
            // Tell clients to discard their hand
            RpcDiscardHand();
            
            Debug.Log($"[DIAGNOSTIC] PetHand.ServerDiscardHand completed for {this.name}");
        }
        #endregion
    }
} 