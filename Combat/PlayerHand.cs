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
        public NetworkPlayer NetworkPlayer { get; private set; }
        #endregion

        #region Network Callbacks
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Set interactability based on ownership
            SetCardsInteractivity(IsOwner);
            
            // Try to find references if they're missing on client
            if (owner == null || combatPlayer == null)
            {
                // First try the quick find
                TryFindReferences();
                
                // If still missing, do a more thorough search
                if (owner == null || combatPlayer == null)
                {
                    StartCoroutine(FindReferencesViaNetwork());
                }
            }
        }
        
        private System.Collections.IEnumerator FindReferencesViaNetwork()
        {
            // Wait a bit for network sync to complete
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForSeconds(0.2f);
                
                // Try to find the references via the player's synced combat reference
                NetworkPlayer[] players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
                foreach (var player in players)
                {
                    // Check if this is the reference player hand for any player
                    if (player.SyncedPlayerHand.Value == this)
                    {
                        // Set owner reference
                        owner = player;
                        
                        // Set combat player reference
                        combatPlayer = player.SyncedCombatPlayer.Value;
                        
                        // Update interactivity based on ownership
                        SetCardsInteractivity(IsOwner);
                        
                        Debug.Log($"[CLIENT] Found owner and combatPlayer for PlayerHand on client");
                        yield break;
                    }
                }
                
                // If we're the owner, try to find references based on ownership
                if (IsOwner)
                {
                    foreach (var player in players)
                    {
                        if (player.IsOwner)
                        {
                            owner = player;
                            combatPlayer = player.SyncedCombatPlayer.Value;
                            
                            if (owner != null && combatPlayer != null)
                            {
                                Debug.Log($"[CLIENT] Found owner and combatPlayer for PlayerHand based on ownership");
                                yield break;
                            }
                        }
                    }
                }
            }
            
            Debug.LogWarning("[CLIENT] Failed to find owner or combatPlayer after multiple attempts");
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
                // We no longer need to send separate RPCs from here.
                // The CardTargetingSystem's CmdApplyCardEffect handles both effect application AND card removal from hand on the server.
                Debug.Log($"[PlayerHand] HandleCardPlayed called for {card.CardName}, but RPCs are now handled by CardTargetingSystem.");
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
            // --- Log Entry --- 
            Debug.Log($"[Server DrawCards] Entered for PlayerHand ID: {this.GetInstanceID()}, Count: {count}. Current hand count: {cardsInHand.Count}");
            
            // This method is for server-side card drawing
            // Check if there's a valid reference to the player's deck
            if (runtimeDeck == null && combatPlayer != null)
            {
                runtimeDeck = combatPlayer.PlayerDeck;
                Debug.Log($"[Server DrawCards ID: {this.GetInstanceID()}] Assigned runtimeDeck.");
            }
            else if(runtimeDeck == null)
            {
                Debug.LogWarning($"[Server DrawCards ID: {this.GetInstanceID()}] runtimeDeck is null and combatPlayer is null or has no deck.");
            }
            
            // Draw and create cards
            for (int i = 0; i < count; i++)
            {
                Debug.Log($"[Server DrawCards ID: {this.GetInstanceID()}] Loop {i+1}/{count}. Checking hand size ({cardsInHand.Count} < {maxHandSize})");
                if (cardsInHand.Count >= maxHandSize)
                {
                    Debug.Log($"[Server DrawCards ID: {this.GetInstanceID()}] Hand is full, breaking loop.");
                    break;
                }
                
                // Draw card from player's deck
                Debug.Log($"[Server DrawCards ID: {this.GetInstanceID()}] Attempting to draw card from deck...");
                CardData cardData = combatPlayer?.DrawCardFromDeck();
                
                if (cardData != null && DeckManager.Instance != null)
                {
                    Debug.Log($"[Server DrawCards ID: {this.GetInstanceID()}] Drawn '{cardData.cardName}'. Attempting to create object...");
                    // Create card object (returns the GameObject)
                    GameObject cardObj = DeckManager.Instance.CreateCardObject(cardData, this.transform, this, combatPlayer); 
                    
                    // --- CLEANUP: Remove redundant card addition logic --- 
                    // The card is now added to the list within CardHandManager.ServerAddCard 
                    // or potentially needs to be added where DeckManager.CreateCardObject is called if not via ServerAddCard.
                    // if (cardObj != null)
                    // {
                    //     Card cardComponent = cardObj.GetComponent<Card>();
                    //     if (cardComponent != null)
                    //     {
                    //         if (!cardsInHand.Contains(cardComponent))
                    //         {
                    //             cardsInHand.Add(cardComponent);
                    //             Debug.Log($"[Server PlayerHand ID: {this.GetInstanceID()}] Added card '{cardComponent.CardName}' to server hand list. New count: {cardsInHand.Count}");
                    //         }
                    //     }
                    //     else
                    //     {
                    //         Debug.LogError($"[Server PlayerHand] Created card object for {cardData.cardName} is missing Card component!");
                    //     }
                    // }
                    // --- END CLEANUP ---
                }
                else if (cardData == null)
                {
                    Debug.LogError($"[Server DrawCards ID: {this.GetInstanceID()}] DrawCardFromDeck returned null card. CombatPlayer null? {combatPlayer == null}");
                }
                else if (DeckManager.Instance == null)
                {
                     Debug.LogError($"[Server DrawCards ID: {this.GetInstanceID()}] DeckManager.Instance is null!");
                }
            }
             Debug.Log($"[Server DrawCards ID: {this.GetInstanceID()}] Finished drawing loop. Final hand count: {cardsInHand.Count}");
            // Optional: Arrange cards on server if needed for logic (visuals are client-side)
            // ArrangeCardsInHand(); 
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
        public void Initialize(NetworkPlayer networkPlayer, CombatPlayer combatPlayerRef)
        {
            NetworkPlayer = networkPlayer;
            owner = networkPlayer;
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
            // --- Log Entry --- 
            Debug.Log($"[Server CmdDrawCards] Received for PlayerHand ID: {this.GetInstanceID()}, Owner: {(owner != null ? owner.GetSteamName() : "null")}, Count: {count}");
            
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
        private void CmdPlayCard(int cardIndex, string cardName, CardType cardType)
        {
            if (!IsOwner) return;
            
            // Let the combat player handle the card effect
            combatPlayer.PlayCard(cardName, cardType);
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