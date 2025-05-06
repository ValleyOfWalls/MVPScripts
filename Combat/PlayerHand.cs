using UnityEngine;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using DG.Tweening;
using System.Collections;
using FishNet;
using System.Linq;

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
            // Log for tracing
            Debug.Log($"[Server DrawCards] Entered for PlayerHand ID: {this.GetInstanceID()}, Count: {count}. Current hand count: {cardsInHand.Count}");
            
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
                    // Use the CardHandManager.ServerAddCard method to create the card object
                    ServerAddCard(cardData);
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
            // Log for tracing
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
        
        // Server authoritative discard hand - override to handle deck discarding
        [Server]
        public override void ServerDiscardHand()
        {
            Debug.Log($"[DIAGNOSTIC] PlayerHand.ServerDiscardHand called for {this.name}. Owner: {(combatPlayer?.NetworkPlayer?.GetSteamName() ?? "Unknown")}");
            
            // Get cards to discard
            List<Card> cardsToDiscard = new List<Card>(cardsInHand);
            
            Debug.Log($"[DIAGNOSTIC] PlayerHand created discard list with {cardsToDiscard.Count} cards");
            if (cardsToDiscard.Count > 0)
            {
                string cardNames = string.Join(", ", cardsToDiscard.Select(c => c?.CardName ?? "NULL_CARD"));
                Debug.Log($"[DIAGNOSTIC] PlayerHand cards to discard: {cardNames}");
            }
            
            // Add the card data for each card to discard pile before destroying the objects
            if (combatPlayer != null && combatPlayer.PlayerDeck != null)
            {
                int discardedCount = 0;
                foreach (Card card in cardsToDiscard)
                {
                    if (card != null && card.Data != null)
                    {
                        // Add card data to discard pile in the deck
                        combatPlayer.PlayerDeck.DiscardCard(card.Data);
                        discardedCount++;
                    }
                }
                Debug.Log($"[DIAGNOSTIC] PlayerHand added {discardedCount} cards to discard pile in deck");
            }
            else
            {
                Debug.LogError("FALLBACK: Could not discard cards to deck - missing combatPlayer or playerDeck");
            }

            // Now handle each card object
            int animatedCount = 0;
            int despawnedCount = 0;
            int destroyedCount = 0;
            
            foreach (Card card in cardsToDiscard)
            {
                if (card != null)
                {
                    Debug.Log($"[DIAGNOSTIC] PlayerHand discarding card: {card.CardName}, IsSpawned: {card.IsSpawned}");
                    
                    // Tell clients to animate
                    RpcAnimateDiscard(card.NetworkObject);
                    animatedCount++;
                    
                    // Despawn the card object
                    if (card.IsSpawned)
                    {
                        InstanceFinder.ServerManager.Despawn(card.gameObject, DespawnType.Destroy);
                        despawnedCount++;
                    }
                    else 
                    {
                        Debug.LogWarning($"FALLBACK: Card {card.CardName} was not spawned, destroying directly");
                        Destroy(card.gameObject);
                        destroyedCount++;
                    }
                }
            }
            
            Debug.Log($"[DIAGNOSTIC] PlayerHand discarded cards: {animatedCount} animations sent, {despawnedCount} despawned, {destroyedCount} destroyed directly");
            
            // Call base implementation to clear list and send RPC
            base.ServerDiscardHand();
            
            Debug.Log($"[DIAGNOSTIC] PlayerHand.ServerDiscardHand completed for {this.name}");
        }
        #endregion

        #region Observer RPCs
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
                // Store a local reference to the game object to prevent null reference issues
                GameObject cardGameObject = card.gameObject;
                Transform cardTransform = card.transform;
                
                // Ensure the card has a unique identifier for the animation
                string tweenId = $"Discard_{cardNetworkObject.ObjectId}";
                
                // Kill any existing tweens with this ID
                DOTween.Kill(tweenId);

                // Animate the card flying off screen
                Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0).normalized;
                float duration = 0.5f;
                
                Sequence sequence = DOTween.Sequence();
                sequence.stringId = tweenId;
                
                // Add safety check for destroyed objects
                sequence.OnUpdate(() => {
                    if (cardGameObject == null || !cardGameObject.activeInHierarchy)
                    {
                        sequence.Kill(false);
                    }
                });
                
                // Only add the move animation if the transform is valid
                if (cardTransform != null)
                {
                    sequence.Append(cardTransform.DOMove(cardTransform.position + (randomDirection * 1000), duration)
                        .SetEase(Ease.InBack));
                    sequence.Join(cardTransform.DOScale(Vector3.zero, duration));
                }
                 
                // Remove from local list immediately if owner (visual only)
                if (IsOwner && cardsInHand.Contains(card))
                {
                    cardsInHand.Remove(card);
                }

                sequence.OnComplete(() => {
                    // Hide the card after animation completes
                    if (cardGameObject != null && cardGameObject.activeInHierarchy)
                    {
                        cardGameObject.SetActive(false);
                    }
                });
            }
            else
            {
                Debug.LogWarning("FALLBACK: RpcAnimateDiscard could not find Card component");
                // Potentially just disable the object if Card component is missing
                if (cardNetworkObject != null && cardNetworkObject.gameObject != null)
                {
                    cardNetworkObject.gameObject.SetActive(false);
                }
            }
        }
        #endregion
    }
} 