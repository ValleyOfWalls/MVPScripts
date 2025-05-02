using UnityEngine;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using DG.Tweening;
using System.Collections;
using FishNet;

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
        
        // Current hand of cards - populated by Card.OnStartClient -> RegisterNetworkedCard
        private readonly List<Card> cardsInHand = new List<Card>();
        
        // Reference to owned cards deck (Set via SetDeck method)
        private RuntimeDeck ownerDeck;
        
        public int HandSize => cardsInHand.Count;

        private void Awake()
        {
            //Debug.Log($"PlayerHand Awake - Initializing");
        }
        
        // Always show cards, but only make them interactive for the owner
        public override void OnStartClient()
        {
            base.OnStartClient();
            //Debug.Log($"[PlayerHand] OnStartClient for {gameObject.name}. IsOwner: {IsOwner}. Parent: {(transform.parent != null ? transform.parent.name : "null")}");
            
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
            
            // Schedule card discovery after a short delay to ensure all objects are spawned
            StartCoroutine(DelayedCardDiscovery());
        }
        
        private System.Collections.IEnumerator DelayedCardDiscovery()
        {
            // Wait for a short time to ensure all objects are spawned
            yield return new WaitForSeconds(0.5f);
            
            // Discover and arrange cards
            DiscoverCardObjects();
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
            
           // Debug.Log($"PlayerHand initialized for player {(player != null ? player.GetSteamName() : "Unknown")}");
        }
        
        [Client]
        public void DrawInitialHand(int cardCount)
        {
            if (!IsOwner) return;
            
            //Debug.Log($"DrawInitialHand called for {cardCount} cards. IsOwner: {IsOwner}");
            
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
            
           // Debug.Log($"[Server] CmdDrawCards - Drawing {count} cards for player {owner.GetSteamName()}");
            
            // Draw the requested number of cards
            for (int i = 0; i < count; i++)
            {
                if (cardsInHand.Count >= maxHandSize)
                {
                   // Debug.Log($"Hand is full ({maxHandSize} cards), stopping draw");
                    break;
                }
                
                // Get a card from the player's deck
                CardData card = combatPlayer.DrawCardFromDeck();
                if (card != null)
                {
                    // Create and Spawn the Card NetworkObject using DeckManager
                    // The Card's OnStartClient will handle client-side setup
                    DeckManager.Instance.CreateCardObject(card, this.transform, this, combatPlayer);
                }
                else
                {
                    Debug.LogError("DrawCardFromDeck returned null card");
                }
            }
        }
        
        // This should be called when a networked card is added to the hand
        public void RegisterNetworkedCard(Card card)
        {
            if (card == null) return;
            
            // Make sure it's not already in the list
            if (!cardsInHand.Contains(card))
            {
                cardsInHand.Add(card);
                //Debug.Log($"[{(IsServer ? "Server" : "Client")}] Registered networked card {card.CardName} to hand, current hand size: {cardsInHand.Count}");
                
                // Arrange cards in hand
                ArrangeCardsInHand();
            }
        }
        
        // Improved method to discover card objects spawned into this hand
        public void DiscoverCardObjects()
        {
            // Find all Card components that are children of this hand
            Card[] cards = GetComponentsInChildren<Card>(true); // Include inactive cards
            
           // Debug.Log($"[{(IsServer ? "Server" : "Client")}] Discovering cards in hand: found {cards.Length}");
            
            // Register any cards not already in our list
            foreach (Card card in cards)
            {
                if (card != null && !cardsInHand.Contains(card))
                {
                    cardsInHand.Add(card);
                   // Debug.Log($"[{(IsServer ? "Server" : "Client")}] Discovered card: {card.CardName}");
                }
            }
            
            // Arrange cards in hand after discovery
            ArrangeCardsInHand();
        }
        
        // Called when a card is played
        public void OnCardPlayed(Card card)
        {
            if (card == null) return;
            
            // Find the index of the card in the hand
            int cardIndex = cardsInHand.IndexOf(card);
            
            if (cardIndex != -1)
            {
                // Get the card name
                string cardName = card.CardName;
                
                // Log card play
              //  Debug.Log($"[{(IsServer ? "Server" : "Client")}] Card played: {cardName} from index {cardIndex}");
                
                // Only the owner should tell the server about played cards
                if (IsOwner)
                {
                    // Tell server to remove card from hand
                    CmdRemoveCardFromHand(cardIndex, cardName);
                    
                    // Get card type and base value to send to server
                    CardType cardType = card.Type;
                    int baseValue = card.BaseValue;
                    
                    // Tell server to play this card's effect
                    CmdPlayCard(cardIndex, cardName, cardType, baseValue);
                }
                
                // If we're the server, we don't need to remove the card here
                // since CmdRemoveCardFromHand will handle that
            }
            else
            {
                Debug.LogError($"Card {card.CardName} not found in hand");
            }
        }
        
        [ServerRpc]
        private void CmdRemoveCardFromHand(int cardIndex, string cardName)
        {
            if (cardIndex < 0 || cardIndex >= cardsInHand.Count)
            {
                Debug.LogError($"Invalid card index {cardIndex} for hand with {cardsInHand.Count} cards");
                return;
            }
            
            // Get the card reference
            Card card = cardsInHand[cardIndex];
            
            // Remove from list
            cardsInHand.RemoveAt(cardIndex);
            
            // Tell all clients to remove the card
            RpcRemoveCardFromHand(cardIndex, cardName);
            
            // Destroy the networked card GameObject
            if (card != null)
            {
                // Despawn the networked object
                InstanceFinder.ServerManager.Despawn(card.gameObject);
            }
            
            // Arrange remaining cards
            ArrangeCardsInHand();
        }
        
        [ObserversRpc]
        private void RpcRemoveCardFromHand(int cardIndex, string cardName)
        {
            // Skip for the server, as it already handled removal
            if (IsServer) return;
            
            // Skip for owners, as they'll see the card removal through network sync
            if (IsOwner) return;
            
            if (cardIndex >= 0 && cardIndex < cardsInHand.Count)
            {
                // Remove from local list - the card GameObject will be despawned by the server
                cardsInHand.RemoveAt(cardIndex);
                
                // Arrange remaining cards
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
            int cardCount = cardsInHand.Count;
            if (cardCount == 0) return;
            
            // Use simple horizontal layout (similar to CombatCanvasManager.ManualCardArrangement)
            float cardWidth = 120f; // Match value from ManualCardArrangement if possible
            float spacing = 10f;   // Match value from ManualCardArrangement
            float totalWidth = (cardCount * cardWidth) + ((cardCount - 1) * spacing);
            float startX = -totalWidth / 2f + cardWidth / 2f; // Start from the left edge
            
            // Calculate card positions
            for (int i = 0; i < cardCount; i++)
            {
                Card card = cardsInHand[i];
                if (card == null) continue;
                
                // Calculate simple horizontal position
                float xPos = startX + (i * (cardWidth + spacing));
                float yPos = 0; // Keep cards aligned horizontally
                
                // Set position and rotation directly
                Vector3 targetPosition = new Vector3(xPos, yPos, 0);
                Quaternion targetRotation = Quaternion.identity; // Reset rotation
                
                // Set directly regardless of server/client, removing animation
                card.transform.localPosition = targetPosition;
                card.transform.localRotation = targetRotation;
                card.transform.localScale = Vector3.one; // Ensure scale is reset

                // Update sorting order for all clients
                Canvas cardCanvas = card.GetComponent<Canvas>();
                if (cardCanvas != null)
                {
                    // Ensure canvas is enabled for sorting order to apply
                    if (!cardCanvas.enabled) cardCanvas.enabled = true;
                    cardCanvas.overrideSorting = true; // Take control of sorting
                    cardCanvas.sortingOrder = 100 + i; // Simple left-to-right sort order
                }
            }
        }
        
        // Discard the entire hand
        [Client]
        public void DiscardHand()
        {
            if (!IsOwner) return;
            
           // Debug.Log($"Discarding entire hand of {cardsInHand.Count} cards");
            
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
           // Debug.Log($"PlayerHand deck set: {(deck != null ? deck.DeckName : "null")}");
        }
        
        // Add a card to the hand
        public void AddCard(Card card)
        {
            if (card != null && !cardsInHand.Contains(card))
            {
                cardsInHand.Add(card);
              //  Debug.Log($"Card {card.CardName} added directly to hand");
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

        // --- Parenting RPC --- 
        [ObserversRpc(ExcludeOwner = false, BufferLast = true)]
        public void RpcSetParent(NetworkObject parentNetworkObject)
        {
             if (parentNetworkObject == null)
             {
                 Debug.LogError($"[PlayerHand:{NetworkObject.ObjectId}] RpcSetParent received null parentNetworkObject.");
                 return;
             }

             Transform parentTransform = parentNetworkObject.transform;
             if (parentTransform != null)
             {
                 transform.SetParent(parentTransform, false);
                // Debug.Log($"[PlayerHand:{NetworkObject.ObjectId}] Set parent to {parentTransform.name} ({parentNetworkObject.ObjectId}) via RPC.");
             }
             else
             {
                  Debug.LogError($"[PlayerHand:{NetworkObject.ObjectId}] Could not find transform for parent NetworkObject {parentNetworkObject.ObjectId} in RpcSetParent.");
             }
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            
            // When ownership changes, make sure interactivity is updated
            SetCardsInteractivity(IsOwner);
            
           // Debug.Log($"[Client] PlayerHand ownership changed to {(IsOwner ? "self" : "someone else")}");
        }
    }
} 