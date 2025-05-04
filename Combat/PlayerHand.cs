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
    public class PlayerHand : CardHandManager
    {
        #region Fields and Properties
        [Header("Player Hand References")]
        [SerializeField] private NetworkPlayer owner;
        [SerializeField] private CombatPlayer combatPlayer;
        #endregion

        #region Network Callbacks
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Set interactability based on ownership
            SetCardsInteractivity(IsOwner);
        }
        
        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            
            // When ownership changes, make sure interactivity is updated
            SetCardsInteractivity(IsOwner);
        }
        #endregion

        #region CardHandManager Abstract Implementation
        protected override void SetupReferences()
        {
            // Try to find owner and combatPlayer references if they're null
            if (owner == null || combatPlayer == null)
            {
                TryFindReferences();
            }
        }
        
        protected override ICombatant GetCombatant()
        {
            return combatPlayer;
        }
        
        protected override void HandleCardPlayed(Card card, int cardIndex)
        {
            // Only the owner should tell the server about played cards
            if (IsOwner)
            {
                // Tell server to remove card from hand
                CmdRemoveCardFromHand(cardIndex, card.CardName);
                
                // Get card type and base value to send to server
                CardType cardType = card.Type;
                int baseValue = card.BaseValue;
                
                // Tell server to play this card's effect
                CmdPlayCard(cardIndex, card.CardName, cardType, baseValue);
            }
        }
        
        [Client]
        public override void DrawInitialHand(int cardCount)
        {
            if (!IsOwner) return;
            
            CmdDrawCards(cardCount);
        }
        
        [Server]
        public override void DrawCards(int count)
        {
            // This method is for server-side card drawing
            // Check if there's a valid reference to the player's deck
            if (runtimeDeck == null && combatPlayer != null)
            {
                runtimeDeck = combatPlayer.PlayerDeck;
            }
            
            // Draw and create cards
            for (int i = 0; i < count; i++)
            {
                if (cardsInHand.Count >= maxHandSize)
                {
                    break;
                }
                
                // Draw card from player's deck
                CardData cardData = combatPlayer?.DrawCardFromDeck();
                if (cardData != null && DeckManager.Instance != null)
                {
                    // Create card object in the hand
                    DeckManager.Instance.CreateCardObject(cardData, this.transform, this, combatPlayer);
                }
                else if (cardData == null)
                {
                    Debug.LogError("FALLBACK: DrawCardFromDeck returned null card");
                }
            }
        }
        #endregion

        #region Reference Finding
        private void TryFindReferences()
        {
            // Try to find parent NetworkPlayer
            Transform parent = transform.parent;
            if (parent != null)
            {
                // Try to get NetworkPlayer from parent
                NetworkPlayer parentPlayer = parent.GetComponent<NetworkPlayer>();
                if (parentPlayer != null)
                {
                    owner = parentPlayer;
                    
                    // Find the CombatPlayer in children of NetworkPlayer
                    CombatPlayer[] combatPlayers = parent.GetComponentsInChildren<CombatPlayer>();
                    if (combatPlayers.Length > 0)
                    {
                        combatPlayer = combatPlayers[0];
                    }
                }
            }
            
            // If still not found, try searching in the scene
            if (owner == null || combatPlayer == null)
            {
                // Try to find owner in the scene if we're the owner
                if (IsOwner)
                {
                    NetworkPlayer[] players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
                    foreach (var player in players)
                    {
                        if (player.IsOwner)
                        {
                            owner = player;
                            break;
                        }
                    }
                    
                    // Try to find combatPlayer
                    if (owner != null)
                    {
                        CombatPlayer[] combatPlayers = owner.GetComponentsInChildren<CombatPlayer>();
                        if (combatPlayers.Length > 0)
                        {
                            combatPlayer = combatPlayers[0];
                        }
                    }
                }
            }
        }
        #endregion

        #region Server RPCs
        [Server]
        public void Initialize(NetworkPlayer player, CombatPlayer combatPlayerRef)
        {
            owner = player;
            combatPlayer = combatPlayerRef;
            
            // Set the deck
            if (combatPlayerRef != null && combatPlayerRef.PlayerDeck != null)
            {
                runtimeDeck = combatPlayerRef.PlayerDeck;
            }
        }
        
        [ServerRpc]
        public void CmdDrawCards(int count)
        {
            // Ensure owner reference is valid before proceeding
            if (owner == null)
            {
                Debug.LogError("FALLBACK: CmdDrawCards - Owner is null, cannot draw cards");
                return;
            }

            // Ensure combat player reference is valid
            if (combatPlayer == null)
            {
                // Attempt to find it if missing
                CombatPlayer[] players = FindObjectsByType<CombatPlayer>(FindObjectsSortMode.None);
                foreach(var cp in players)
                {
                    if (cp.NetworkPlayer == owner)
                    {
                        combatPlayer = cp;
                        Debug.LogWarning("FALLBACK: CmdDrawCards - Found missing combatPlayer reference");
                        break;
                    }
                }
                 
                if (combatPlayer == null)
                {
                    Debug.LogError("FALLBACK: CmdDrawCards - CombatPlayer reference is null even after search");
                    return;
                }
            }
            
            // Call the DrawCards method to handle card creation
            DrawCards(count);
        }
        
        [ServerRpc]
        private void CmdRemoveCardFromHand(int cardIndex, string cardName)
        {
            if (cardIndex < 0 || cardIndex >= cardsInHand.Count)
            {
                Debug.LogError($"FALLBACK: Invalid card index {cardIndex} for hand with {cardsInHand.Count} cards");
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
        
        [ServerRpc]
        private void CmdPlayCard(int cardIndex, string cardName, CardType cardType, int baseValue)
        {
            if (!IsOwner) return;
            
            // Let the combat player handle the card effect
            combatPlayer.PlayCard(cardName, cardType, baseValue);
        }
        
        // Server authoritative discard hand - override to handle deck discarding
        [Server]
        public override void ServerDiscardHand()
        {
            // Get cards to discard
            List<Card> cardsToDiscard = new List<Card>(cardsInHand);
            
            // Add the card data for each card to discard pile before destroying the objects
            if (combatPlayer != null && combatPlayer.PlayerDeck != null)
            {
                foreach (Card card in cardsToDiscard)
                {
                    if (card != null && card.Data != null)
                    {
                        // Add card data to discard pile in the deck
                        combatPlayer.PlayerDeck.DiscardCard(card.Data);
                    }
                }
            }
            else
            {
                Debug.LogError("FALLBACK: Could not discard cards to deck - missing combatPlayer or playerDeck");
            }

            // Call base implementation which clears list and triggers RPC
            base.ServerDiscardHand();
            
            // Now destroy the card objects
            foreach (Card card in cardsToDiscard)
            {
                if (card != null)
                {
                    // Tell clients to animate
                    RpcAnimateDiscard(card.NetworkObject);
                    
                    // Despawn the card object
                    if (card.IsSpawned)
                    {
                        InstanceFinder.ServerManager.Despawn(card.gameObject, DespawnType.Destroy);
                    }
                    else 
                    {
                        Debug.LogWarning($"FALLBACK: Card {card.CardName} was not spawned, destroying directly");
                        Destroy(card.gameObject);
                    }
                }
            }
        }
        #endregion

        #region Observer RPCs
        [ObserversRpc]
        private void RpcRemoveCardFromHand(int cardIndex, string cardName)
        {
            // Skip for the server and owner - they'll see the card removal through network sync
            if (IsServer || IsOwner) return;
            
            if (cardIndex >= 0 && cardIndex < cardsInHand.Count)
            {
                // Remove from local list
                cardsInHand.RemoveAt(cardIndex);
                
                // Arrange remaining cards
                ArrangeCardsInHand();
            }
        }
        
        // Server telling clients to animate discard and destroy
        [ObserversRpc]
        private void RpcAnimateDiscard(NetworkObject cardNetworkObject)
        {
            if (cardNetworkObject == null) 
            {
                Debug.LogWarning("FALLBACK: RpcAnimateDiscard received null NetworkObject");
                return;
            }
             
            Card card = cardNetworkObject.GetComponent<Card>();
            if (card != null)
            {
                // Animate the card flying off screen
                Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0).normalized;
                float duration = 0.5f;
                card.transform.DOMove(card.transform.position + (randomDirection * 1000), duration)
                    .SetEase(Ease.InBack);
                 
                card.transform.DOScale(Vector3.zero, duration);
                 
                // Remove from local list immediately if owner (visual only)
                if (IsOwner && cardsInHand.Contains(card))
                {
                    cardsInHand.Remove(card);
                }

                // Hide it while waiting for despawn
                card.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning("FALLBACK: RpcAnimateDiscard could not find Card component");
                // Potentially just disable the object if Card component is missing
                cardNetworkObject.gameObject.SetActive(false);
            }
        }
        #endregion
    }
} 