using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// Manages all card movement between deck, hand, and discard pile during gameplay.
/// Attach to: NetworkEntity prefabs to handle their card operations.
/// </summary>
public class HandManager : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkEntityUI entityUI;
    
    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private Transform handTransform;
    private Transform deckTransform;
    private Transform discardTransform;

    private void Awake()
    {
        // Get required components
        if (entityUI == null) entityUI = GetComponent<NetworkEntityUI>();

        // Get transforms from UI
        if (entityUI != null)
        {
            handTransform = entityUI.GetHandTransform();
            deckTransform = entityUI.GetDeckTransform();
            discardTransform = entityUI.GetDiscardTransform();
        }

        ValidateComponents();
    }

    private void ValidateComponents()
    {
        if (entityUI == null)
            Debug.LogError($"HandManager on {gameObject.name}: Missing NetworkEntityUI component");
        if (handTransform == null)
            Debug.LogError($"HandManager on {gameObject.name}: Missing hand transform reference");
        if (deckTransform == null)
            Debug.LogError($"HandManager on {gameObject.name}: Missing deck transform reference");
        if (discardTransform == null)
            Debug.LogError($"HandManager on {gameObject.name}: Missing discard transform reference");
    }

    /// <summary>
    /// Draws cards from deck to hand at the start of a round
    /// </summary>
    [Server]
    public void DrawCards()
    {
        if (!IsServerInitialized) return;
        
        // Get the entity for better logging
        NetworkEntity entity = GetComponent<NetworkEntity>();
        string entityName = entity != null ? entity.EntityName.Value : gameObject.name;
        int entityId = entity != null ? entity.ObjectId : -999;
        
        Debug.Log($"HandManager: DrawCards called for {entityName} (ID: {entityId}), HandManager instance: {GetInstanceID()}");
        
        if (verboseLogging) /* Debug.Log($"HandManager: Drawing cards for {gameObject.name}"); */

        // Check if transforms are available
        if (deckTransform == null || handTransform == null)
        {
            Debug.LogError($"HandManager: Missing deck or hand transform for {entityName}");
            return;
        }

        Debug.Log($"HandManager: Transforms validated for {entityName} (ID: {entityId})");

        if (entity == null)
        {
            Debug.LogError($"HandManager: Missing NetworkEntity component on {gameObject.name}");
            return;
        }

        // DEBUG: Check GameManager instance and values
        if (GameManager.Instance == null)
        {
            Debug.LogError($"HandManager: GameManager.Instance is NULL for {entityName}");
            return;
        }
        
        Debug.Log($"HandManager: GameManager validated for {entityName} (ID: {entityId})");
        
        // Get current hand size to determine if this is initial draw
        List<GameObject> currentHand = GetCardsInTransform(handTransform);
        int currentHandSize = currentHand.Count;

        Debug.Log($"HandManager: Current hand size for {entityName} (ID: {entityId}): {currentHandSize}");

        // FIXED: Use initial draw amount for first draw (empty hand), target hand size for subsequent draws
        int targetHandSize;
        bool isInitialDraw = (currentHandSize == 0);
        
        if (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.PlayerHand)
        {
            if (isInitialDraw)
            {
                targetHandSize = GameManager.Instance.PlayerDrawAmount.Value; // Use initial draw amount
            }
            else
            {
                targetHandSize = GameManager.Instance.PlayerTargetHandSize.Value; // Use target hand size
            }
        }
        else if (entity.EntityType == EntityType.Pet || entity.EntityType == EntityType.PetHand)
        {
            if (isInitialDraw)
            {
                targetHandSize = GameManager.Instance.PetDrawAmount.Value; // Use initial draw amount
            }
            else
            {
                targetHandSize = GameManager.Instance.PetTargetHandSize.Value; // Use target hand size
            }
        }
        else
        {
            Debug.LogError($"HandManager: Unknown entity type {entity.EntityType} for {gameObject.name}");
            return;
        }

        // Calculate how many cards to draw
        int remainingCardsToDraw = targetHandSize - currentHandSize;
        if (remainingCardsToDraw <= 0)
        {
            return;
        }

        // First, check if we need to recycle before starting to draw
        List<GameObject> deckCards = GetCardsInTransform(deckTransform);
        List<GameObject> discardCards = GetCardsInTransform(discardTransform);
        
        if (deckCards.Count < remainingCardsToDraw && discardCards.Count > 0)
        {
            RecycleAndShuffleDiscardPile();
            // Get updated deck cards after recycling
            deckCards = GetCardsInTransform(deckTransform);
        }

        // Now draw as many cards as we can
        int cardsToDrawThisTime = Mathf.Min(remainingCardsToDraw, deckCards.Count);
        
        Debug.Log($"[CARD_UPGRADE] HandManager: About to draw {cardsToDrawThisTime} cards for {entityName}");
        
        for (int i = 0; i < cardsToDrawThisTime; i++)
        {
            GameObject card = deckCards[i];
            if (card == null)
            {
                Debug.LogError($"HandManager: Null card found in deck for {gameObject.name}");
                continue;
            }
            
            Debug.Log($"[CARD_UPGRADE] HandManager: Moving card {card.name} to hand for {entityName}");
            MoveCardToHand(card);
        }
        
        Debug.Log($"[CARD_UPGRADE] HandManager: Finished drawing cards for {entityName}");
    }

    /// <summary>
    /// Discards all cards from hand with animation
    /// </summary>
    [Server]
    public void DiscardHand()
    {
        if (!IsServerInitialized) return;
        
        // Get the entity for better logging
        NetworkEntity entity = GetComponent<NetworkEntity>();
        string entityName = entity != null ? entity.EntityName.Value : gameObject.name;
        int entityId = entity != null ? entity.ObjectId : -999;
        
        Debug.Log($"HandManager: DiscardHand called for {entityName} (ID: {entityId})");
        
        // Check if transforms are available
        if (handTransform == null || discardTransform == null)
        {
            Debug.LogError($"HandManager: Missing hand or discard transform for {entityName}");
            return;
        }

        Debug.Log($"HandManager: Transforms validated for discard operation on {entityName} (ID: {entityId})");

        // Get all card objects in hand transform
        List<GameObject> handCards = GetCardsInTransform(handTransform);
        
        Debug.Log($"HandManager: Found {handCards.Count} cards in hand to discard for {entityName} (ID: {entityId})");
        
        if (handCards.Count == 0)
        {
            Debug.Log($"HandManager: No cards to discard for {entityName} (ID: {entityId}), returning early");
            return;
        }
        
        Debug.Log($"HandManager: Starting animated discard of {handCards.Count} cards for {entityName} (ID: {entityId})");
        
        // Trigger animations on all clients (including server)
        List<int> cardNetObjIds = new List<int>();
        foreach (GameObject card in handCards)
        {
            if (card != null)
            {
                NetworkObject cardNetObj = card.GetComponent<NetworkObject>();
                if (cardNetObj != null)
                {
                    cardNetObjIds.Add(cardNetObj.ObjectId);
                    Debug.Log($"HandManager: Added card {card.name} (ID: {cardNetObj.ObjectId}) to discard list for {entityName}");
                }
                else
                {
                    Debug.LogError($"HandManager: Card {card.name} missing NetworkObject for {entityName}");
                }
            }
            else
            {
                Debug.LogError($"HandManager: Found null card in hand for {entityName}");
            }
        }
        
        Debug.Log($"HandManager: Prepared {cardNetObjIds.Count} cards for RPC discard animation for {entityName} (ID: {entityId})");
        
        // Start animations on all clients
        RpcStartDiscardAnimations(cardNetObjIds.ToArray());
        
        // Start the server-side discard sequence (handles actual card movement without additional animations)
        StartCoroutine(ServerDiscardHandSequence(handCards));
    }
    
    /// <summary>
    /// RPC to trigger discard animations on all clients
    /// </summary>
    [ObserversRpc]
    private void RpcStartDiscardAnimations(int[] cardNetObjIds)
    {
        /* Debug.Log($"HandManager (RPC): Starting discard animations for {cardNetObjIds.Length} cards"); */
        
        StartCoroutine(ClientAnimateDiscardSequence(cardNetObjIds));
    }
    
    /// <summary>
    /// Client-side coroutine to animate cards dissolving out
    /// </summary>
    private System.Collections.IEnumerator ClientAnimateDiscardSequence(int[] cardNetObjIds)
    {
        if (cardNetObjIds == null || cardNetObjIds.Length == 0)
        {
            yield break;
        }
        
        /* Debug.Log($"HandManager (Client): Animating discard sequence for {cardNetObjIds.Length} cards"); */
        
        // Start animations for all cards with staggered timing
        foreach (int cardNetObjId in cardNetObjIds)
        {
            // Find the card NetworkObject
            NetworkObject cardNetObj = null;
            bool foundCard = false;
            
            if (NetworkManager.IsServerStarted)
            {
                foundCard = NetworkManager.ServerManager.Objects.Spawned.TryGetValue(cardNetObjId, out cardNetObj);
            }
            else if (NetworkManager.IsClientStarted)
            {
                foundCard = NetworkManager.ClientManager.Objects.Spawned.TryGetValue(cardNetObjId, out cardNetObj);
            }
            
            if (foundCard && cardNetObj != null)
            {
                GameObject card = cardNetObj.gameObject;
                CardAnimator cardAnimator = card.GetComponent<CardAnimator>();
                
                if (cardAnimator != null)
                {
                    // Animate the card dissolving out (no callback needed on clients)
                    cardAnimator.AnimateDissolveOut();
                    /* Debug.Log($"HandManager (Client): Started animation for card {card.name}"); */
                }
                else
                {
                    /* Debug.Log($"HandManager (Client): Card {card.name} has no CardAnimator"); */
                }
            }
            else
            {
                Debug.LogWarning($"HandManager (Client): Could not find card with NetworkObject ID {cardNetObjId}");
            }
            
            // Small delay between starting each card animation for staggered effect
            yield return new WaitForSeconds(0.05f);
        }
        
        /* Debug.Log($"HandManager (Client): Finished starting discard animations"); */
    }
    
    /// <summary>
    /// Server-side coroutine to handle card movement without additional animations (animations are handled by RPC)
    /// </summary>
    [Server]
    private System.Collections.IEnumerator ServerDiscardHandSequence(List<GameObject> cardsToDiscard)
    {
        if (cardsToDiscard == null || cardsToDiscard.Count == 0)
        {
            yield break;
        }
        
        // Get the entity for better logging
        NetworkEntity entity = GetComponent<NetworkEntity>();
        string entityName = entity != null ? entity.EntityName.Value : gameObject.name;
        int entityId = entity != null ? entity.ObjectId : -999;
        
        Debug.Log($"HandManager: ServerDiscardHandSequence starting for {entityName} (ID: {entityId}) with {cardsToDiscard.Count} cards");
        
        // Wait for animations to start and progress
        // The dissolve animation duration is typically 0.3 seconds in CardAnimator
        yield return new WaitForSeconds(0.35f);
        
        Debug.Log($"HandManager: ServerDiscardHandSequence proceeding to move cards for {entityName} (ID: {entityId})");
        
        // Move all cards to discard immediately (animations are handled by RPC)
        int successfullyMoved = 0;
        foreach (GameObject card in cardsToDiscard)
        {
            if (card != null)
            {
                Debug.Log($"HandManager: Moving card {card.name} to discard for {entityName} (ID: {entityId})");
                MoveCardToDiscardImmediate(card);
                successfullyMoved++;
            }
            else
            {
                Debug.LogError($"HandManager: Found null card during discard sequence for {entityName} (ID: {entityId})");
            }
        }
        
        Debug.Log($"HandManager: ServerDiscardHandSequence completed for {entityName} (ID: {entityId}) - moved {successfullyMoved}/{cardsToDiscard.Count} cards");
        
        // Verify hand is actually empty after discard
        List<GameObject> remainingHandCards = GetCardsInTransform(handTransform);
        Debug.Log($"HandManager: After discard, {entityName} (ID: {entityId}) has {remainingHandCards.Count} cards remaining in hand");
        
        if (remainingHandCards.Count > 0)
        {
            Debug.LogError($"HandManager: ERROR - {entityName} (ID: {entityId}) still has {remainingHandCards.Count} cards in hand after discard!");
            foreach (GameObject remainingCard in remainingHandCards)
            {
                if (remainingCard != null)
                {
                    Debug.LogError($"HandManager: Remaining card: {remainingCard.name}");
                }
            }
        }
    }
    
    /// <summary>
    /// Immediately moves card to discard without animation (used after animation completes)
    /// </summary>
    [Server]
    private void MoveCardToDiscardImmediate(GameObject card)
    {
        if (!IsServerInitialized || card == null) return;
        
        // This is the same logic as MoveCardToDiscard but without triggering additional animations
        NetworkObject cardNetObj = card.GetComponent<NetworkObject>();
        if (cardNetObj == null)
        {
            Debug.LogError($"HandManager: Card {card.name} is missing NetworkObject component for {gameObject.name}");
            return;
        }

        // Update Card's CurrentContainer
        Card cardComponent = card.GetComponent<Card>();
        if (cardComponent != null)
        {
            cardComponent.SetCurrentContainer(CardLocation.Discard);
        }

        NetworkObject parentEntityNetObj = discardTransform.GetComponentInParent<NetworkObject>();
        if (parentEntityNetObj == null)
        {
            Debug.LogError($"HandManager: Discard transform has no parent with NetworkObject component for {gameObject.name}");
            return;
        }

        // Direct server-side manipulation
        card.SetActive(false);
        card.transform.SetParent(discardTransform);
        card.transform.localPosition = Vector3.zero;

        // RPCs for clients only
        RpcSetCardEnabled(cardNetObj.ObjectId, false);
        RpcSetCardParent(cardNetObj.ObjectId, parentEntityNetObj.ObjectId, "Discard");
    }

    /// <summary>
    /// Draws a single card from deck to hand
    /// </summary>
    [Server]
    public void DrawOneCard()
    {
        if (!IsServerInitialized) return;
        /* Debug.Log($"HandManager: Drawing one card for {gameObject.name}"); */

        // Check if transforms are available
        if (deckTransform == null || handTransform == null)
        {
            Debug.LogError($"HandManager: Missing deck or hand transform for {gameObject.name}");
            return;
        }

        // Get cards in deck
        List<GameObject> deckCards = GetCardsInTransform(deckTransform);
        List<GameObject> discardCards = GetCardsInTransform(discardTransform);

        // If deck is empty, try to recycle discard pile
        if (deckCards.Count == 0 && discardCards.Count > 0)
        {
            Debug.Log($"HandManager: Deck is empty. Recycling and shuffling {discardCards.Count} cards from discard pile.");
            RecycleAndShuffleDiscardPile();
            // Get updated deck cards after recycling
            deckCards = GetCardsInTransform(deckTransform);
            /* Debug.Log($"HandManager: After recycling, deck now has {deckCards.Count} cards."); */
        }

        // Check if we have any cards to draw
        if (deckCards.Count == 0)
        {
            Debug.LogWarning($"HandManager: No cards available to draw for {gameObject.name}");
            return;
        }

        // Draw one card
        GameObject card = deckCards[0];
        if (card == null)
        {
            Debug.LogError($"HandManager: Null card found in deck for {gameObject.name}");
            return;
        }

        /* Debug.Log($"HandManager: Moving card {card.name} to hand for {gameObject.name}"); */
        MoveCardToHand(card);
    }

    /// <summary>
    /// Discards a specific card from hand
    /// </summary>
    [Server]
    public void DiscardCard(GameObject card)
    {
        if (!IsServerInitialized || card == null) return;
        /* Debug.Log($"HandManager: Discarding specific card {card.name} for {gameObject.name}"); */

        // Check if transforms are available
        if (handTransform == null || discardTransform == null)
        {
            Debug.LogError($"HandManager: Missing hand or discard transform for {gameObject.name}");
            return;
        }

        // Check the card's CurrentContainer status instead of physical location
        // (cards can be temporarily moved during drag operations)
        Card cardComponent = card.GetComponent<Card>();
        if (cardComponent == null)
        {
            Debug.LogError($"HandManager: Card {card.name} is missing Card component for {gameObject.name}");
            return;
        }
        
        if (cardComponent.CurrentContainer != CardLocation.Hand)
        {
            Debug.LogError($"HandManager: Card {card.name} is not in hand (current container: {cardComponent.CurrentContainer}) for {gameObject.name}");
            return;
        }
        
        // Additional verification: try to find it in hand, but also check if it might be temporarily moved (e.g., during drag)
        List<GameObject> handCards = GetCardsInTransform(handTransform);
        bool cardIsInHandTransform = handCards.Contains(card);
        
        if (!cardIsInHandTransform)
        {
            Debug.LogWarning($"HandManager: Card {card.name} has Hand container status but is not physically in hand transform (possibly due to drag operation). Current parent: {(card.transform.parent?.name ?? "none")}. Proceeding with discard anyway.");
        }

        // Notify SourceAndTargetIdentifier for damage preview cleanup before discarding
        SourceAndTargetIdentifier sourceAndTargetIdentifier = card.GetComponent<SourceAndTargetIdentifier>();
        if (sourceAndTargetIdentifier != null)
        {
            sourceAndTargetIdentifier.OnCardPlayedOrDiscarded();
        }

        /* Debug.Log($"HandManager: Moving card {card.name} to discard for {gameObject.name}"); */
        MoveCardToDiscard(card);
    }

    [Server]
    private void MoveCardToHand(GameObject card)
    {
        if (!IsServerInitialized || card == null)
        {
            if (card == null) Debug.LogError($"HandManager: Attempted to move null card to hand for {gameObject.name}");
            return;
        }

        NetworkObject cardNetObj = card.GetComponent<NetworkObject>();
        if (cardNetObj == null)
        {
            Debug.LogError($"HandManager: Card {card.name} is missing NetworkObject component for {gameObject.name}");
            return;
        }

        // Get the entity for better logging
        NetworkEntity entity = GetComponent<NetworkEntity>();
        string entityName = entity != null ? entity.EntityName.Value : gameObject.name;

        // Update Card's CurrentContainer
        Card cardComponent = card.GetComponent<Card>();
        if (cardComponent != null)
        {
            Debug.Log($"[CARD_UPGRADE] HandManager: About to move card {cardComponent.CardData?.CardName} to hand for {entityName}");
            Debug.Log($"HandManager: Setting {card.name} container to Hand for {gameObject.name}");
            cardComponent.SetCurrentContainer(CardLocation.Hand);
        }
        else
        {
            Debug.LogError($"HandManager: Card {card.name} is missing Card component for {gameObject.name}");
        }

        NetworkObject parentEntityNetObj = handTransform.GetComponentInParent<NetworkObject>();
        if (parentEntityNetObj == null)
        {
            Debug.LogError($"HandManager: Hand transform has no parent with NetworkObject component for {gameObject.name}");
            return;
        }

        // Direct server-side manipulation
        /* Debug.Log($"HandManager (ServerDirect): Enabling card {card.name} and moving to hand for {gameObject.name}"); */
        card.SetActive(true);
        card.transform.SetParent(handTransform);
        
        // Reset card visual state for redraw
        CanvasGroup cardCanvasGroup = card.GetComponent<CanvasGroup>();
        if (cardCanvasGroup != null)
        {
            cardCanvasGroup.alpha = 1.0f; // Reset alpha from previous play animation
            /* Debug.Log($"HandManager: Reset alpha to 1.0 for redrawn card {card.name}"); */
        }
        
        // Reset CardAnimator state if present
        CardAnimator cardAnimator = card.GetComponent<CardAnimator>();
        if (cardAnimator != null)
        {
            cardAnimator.StoreOriginalState(); // Store fresh state with alpha = 1.0
            /* Debug.Log($"HandManager: Refreshed CardAnimator state for redrawn card {card.name}"); */
        }

        // Record the card draw for tracking and potential upgrades
        CardTracker cardTracker = card.GetComponent<CardTracker>();
        if (cardTracker != null)
        {
            Debug.Log($"[CARD_UPGRADE] HandManager: About to call RecordCardDrawn for {cardComponent?.CardData?.CardName}");
            Debug.Log($"[CARD_UPGRADE] HandManager: Calling RecordCardDrawn for {card.name}");
            cardTracker.RecordCardDrawn();
        }
        else
        {
            Debug.LogWarning($"[CARD_UPGRADE] HandManager: No CardTracker found on {card.name}");
        }
        
        // Only reset position if no HandLayoutManager is present to avoid interfering with custom layouts
        HandLayoutManager handLayoutManager = handTransform.GetComponent<HandLayoutManager>();
        if (handLayoutManager == null)
        {
        card.transform.localPosition = Vector3.zero;
            /* Debug.Log($"HandManager (ServerDirect): Reset {card.name} position to zero (no HandLayoutManager found)"); */
        }
        else
        {
            /* Debug.Log($"HandManager (ServerDirect): Skipped position reset for {card.name} - HandLayoutManager detected on {handLayoutManager.gameObject.name}"); */
        }
        
        // RPCs for clients only
        RpcSetCardEnabled(cardNetObj.ObjectId, true); 
        RpcSetCardParent(cardNetObj.ObjectId, parentEntityNetObj.ObjectId, "Hand");
        RpcResetCardVisualState(cardNetObj.ObjectId); // Reset alpha and visual state on clients

        Debug.Log($"[CARD_UPGRADE] HandManager: Moving card {cardComponent?.CardData?.CardName} to hand for {entityName}");
    }

    [Server]
    private void MoveCardToDiscard(GameObject card)
    {
        if (!IsServerInitialized || card == null)
        {
            if (card == null) Debug.LogError($"HandManager: Attempted to move null card to discard for {gameObject.name}");
            return;
        }

        NetworkObject cardNetObj = card.GetComponent<NetworkObject>();
        if (cardNetObj == null)
        {
            Debug.LogError($"HandManager: Card {card.name} is missing NetworkObject component for {gameObject.name}");
            return;
        }

        // Update Card's CurrentContainer
        Card cardComponent = card.GetComponent<Card>();
        if (cardComponent != null)
        {
            Debug.Log($"HandManager: Setting {card.name} container to Discard for {gameObject.name}");
            cardComponent.SetCurrentContainer(CardLocation.Discard);
            
            // Notify upgrade manager about card discard
            NetworkEntity ownerEntity = cardComponent.OwnerEntity;
            if (ownerEntity != null && CardUpgradeManager.Instance != null)
            {
                // Determine if this was a manual discard (not from playing a card)
                // Manual discards are typically from card effects or player choice, not from playing
                bool wasManualDiscard = true; // For now, assume all discards from hand are manual
                // In the future, you could track play state to distinguish between play discards and manual discards
                
                Debug.Log($"[CARD_UPGRADE] HandManager: Notifying upgrade manager of card discard: {cardComponent.CardData?.CardName} (manual: {wasManualDiscard})");
                CardUpgradeManager.Instance.OnCardDiscarded(cardComponent, ownerEntity, wasManualDiscard);
            }
        }
        else
        {
            Debug.LogError($"HandManager: Card {card.name} is missing Card component for {gameObject.name}");
        }

        NetworkObject parentEntityNetObj = discardTransform.GetComponentInParent<NetworkObject>();
        if (parentEntityNetObj == null)
        {
            Debug.LogError($"HandManager: Discard transform has no parent with NetworkObject component for {gameObject.name}");
            return;
        }

        // Direct server-side manipulation
        /* Debug.Log($"HandManager (ServerDirect): Disabling card {card.name} and moving to discard for {gameObject.name}"); */
        card.SetActive(false);
        card.transform.SetParent(discardTransform);
        card.transform.localPosition = Vector3.zero;
        
        // RPCs for clients only
        RpcSetCardEnabled(cardNetObj.ObjectId, false); 
        RpcSetCardParent(cardNetObj.ObjectId, parentEntityNetObj.ObjectId, "Discard");
    }

    [Server]
    private void MoveCardToDeck(GameObject card)
    {
        if (!IsServerInitialized || card == null)
        {
            if (card == null) Debug.LogError($"HandManager: Attempted to move null card to deck for {gameObject.name}");
            return;
        }

        NetworkObject cardNetObj = card.GetComponent<NetworkObject>();
        if (cardNetObj == null)
        {
            Debug.LogError($"HandManager: Card {card.name} is missing NetworkObject component for {gameObject.name}");
            return;
        }
        
        // Update Card's CurrentContainer
        Card cardComponent = card.GetComponent<Card>();
        if (cardComponent != null)
        {
            Debug.Log($"HandManager: Setting {card.name} container to Deck for {gameObject.name}");
            cardComponent.SetCurrentContainer(CardLocation.Deck);
        }
        else
        {
            Debug.LogError($"HandManager: Card {card.name} is missing Card component for {gameObject.name}");
        }
        
        // Get the NetworkObject ID for the parent transform's owner (the entity)
        NetworkObject parentEntityNetObj = deckTransform.GetComponentInParent<NetworkObject>();
        if (parentEntityNetObj == null)
        {
            Debug.LogError($"HandManager: Deck transform has no parent with NetworkObject component for {gameObject.name}");
            return;
        }

        // Direct server-side manipulation
        /* Debug.Log($"HandManager (ServerDirect): Moving card {card.name} to deck (disabled) for {gameObject.name}"); */
        card.transform.SetParent(deckTransform);
        card.transform.localPosition = Vector3.zero;
        card.SetActive(false); 
        
        // RPCs for clients only
        RpcSetCardEnabled(cardNetObj.ObjectId, false); // Send ObjectId instead of GameObject
        RpcSetCardParent(cardNetObj.ObjectId, parentEntityNetObj.ObjectId, "Deck");
    }

    [Server]
    private void RecycleAndShuffleDiscardPile()
    {
        if (!IsServerInitialized) return;

        List<GameObject> discardCards = GetCardsInTransform(discardTransform);
        if (discardCards.Count == 0)
        {
            Debug.Log($"HandManager: No cards in discard pile to recycle for {gameObject.name}");
            return;
        }
        /* Debug.Log($"HandManager: Starting recycle of {discardCards.Count} cards from discard to deck for {gameObject.name}"); */

        List<GameObject> initialExistingDeckCards = GetCardsInTransform(deckTransform); // Get initial state
        /* Debug.Log($"HandManager: Found {initialExistingDeckCards.Count} existing cards in deck that will remain on top for {gameObject.name}"); */

        System.Random rng = new System.Random();
        int n = discardCards.Count;
        /* Debug.Log($"HandManager: Shuffling {n} discard cards for {gameObject.name}"); */
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            GameObject temp = discardCards[k];
            discardCards[k] = discardCards[n];
            discardCards[n] = temp;
        }
        /* Debug.Log($"HandManager: Finished shuffling discard cards. About to move {discardCards.Count} cards to deck for {gameObject.name}"); */

        int movedCardThisRecycle = 0;
        for(int i=0; i < discardCards.Count; i++)
        {
            GameObject card = discardCards[i];
            if (card != null)
            {
                /* Debug.Log($"HandManager (RecycleLoop {i+1}/{discardCards.Count}): Processing card {card.name} for {gameObject.name}"); */
                MoveCardToDeck(card); // This now does direct server parenting
                movedCardThisRecycle++;
            }
            else
            {
                Debug.LogError($"HandManager (RecycleLoop {i+1}/{discardCards.Count}): Found a NULL card in the discardCards list for {gameObject.name}");
            }
        }
        /* Debug.Log($"HandManager: Loop for moving cards from discard to deck completed. Moved {movedCardThisRecycle} cards this cycle for {gameObject.name}."); */

        // Sibling index management
        // initialExistingDeckCards contains the references to the cards that were originally in the deck.
        // discardCards list contains the references to the cards that were in discard and just moved to deck.
        // All these GameObjects should now be children of deckTransform due to direct server-side parenting.

        int siblingIndex = 0;
        /* Debug.Log($"HandManager: Setting sibling indices for {initialExistingDeckCards.Count} original deck cards."); */
        foreach (GameObject card in initialExistingDeckCards)
        {
            if (card == null) { Debug.LogError("Null card found in initialExistingDeckCards during sibling sort!"); continue; }
            if (card.transform.parent != deckTransform) Debug.LogError($"Card {card.name} was expected to be child of deckTransform for original sort but is not! Parent is {card.transform.parent?.name}");
            card.transform.SetSiblingIndex(siblingIndex++);
        }
        
        /* Debug.Log($"HandManager: Setting sibling indices for {discardCards.Count} recycled cards."); */
        foreach (GameObject card in discardCards) 
        {
            if (card == null) { Debug.LogError("Null card found in discardCards during sibling sort!"); continue; }
            if (card.transform.parent != deckTransform) Debug.LogError($"Card {card.name} was expected to be child of deckTransform for recycled sort but is not! Parent is {card.transform.parent?.name}");
            card.transform.SetSiblingIndex(siblingIndex++);
        }
        
        List<GameObject> finalDeckContents = GetCardsInTransform(deckTransform);
        /* Debug.Log($"HandManager: Completed recycle and shuffle. Final deck count: {finalDeckContents.Count}. Expected {initialExistingDeckCards.Count + discardCards.Count} for {gameObject.name}"); */
    }

    [ObserversRpc]
    private void RpcSetCardEnabled(int cardNetObjId, bool enabled)
    {
        /* Debug.Log($"HandManager (Client RPC): RpcSetCardEnabled called for card NOB ID: {cardNetObjId}, enabled: {enabled}"); */
        
        NetworkObject cardNetObj = null;
        bool foundCard = false;
        
        if (NetworkManager.IsClientStarted)
        {
            foundCard = NetworkManager.ClientManager.Objects.Spawned.TryGetValue(cardNetObjId, out cardNetObj);
        }
        
        if (!foundCard || cardNetObj == null)
        {
            Debug.LogError($"HandManager (Client RPC): RpcSetCardEnabled - Failed to find card NetworkObject with ID {cardNetObjId}");
            return;
        }

        GameObject cardObject = cardNetObj.gameObject;
        
        // Use EntityVisibilityManager for proper visibility filtering
        EntityVisibilityManager entityVisManager = FindEntityVisibilityManager();
        if (entityVisManager != null)
        {
            entityVisManager.ApplyCardVisibilityFilter(cardObject, enabled);
        }
        else
        {
            // Fallback: set active state directly if no EntityVisibilityManager found
            cardObject.SetActive(enabled);
            Debug.LogWarning($"HandManager (Client RPC): No EntityVisibilityManager found, using fallback for card {cardObject.name}");
        }
        
        /* Debug.Log($"HandManager (Client RPC): RpcSetCardEnabled - Card {cardObject.name} visibility updated (requested: {enabled})"); */
    }

    /// <summary>
    /// Finds the EntityVisibilityManager instance
    /// </summary>
    private EntityVisibilityManager FindEntityVisibilityManager()
    {
        // Try to find via GamePhaseManager first
        GamePhaseManager gamePhaseManager = GamePhaseManager.Instance;
        if (gamePhaseManager != null)
        {
            EntityVisibilityManager entityVisManager = gamePhaseManager.GetComponent<EntityVisibilityManager>();
            if (entityVisManager != null) return entityVisManager;
        }
        
        // Fallback to direct search
        return FindFirstObjectByType<EntityVisibilityManager>();
    }

    [ObserversRpc]
    private void RpcSetCardParent(int cardNetObjId, int parentEntityNetObjId, string targetTransformName)
    {
        /* Debug.Log($"HandManager (Client RPC): RpcSetCardParent - Setting card with ID {cardNetObjId} parent to {targetTransformName} on entity with ID {parentEntityNetObjId}"); */
        /* Debug.Log($"HandManager (Client RPC): RpcSetCardParent - This HandManager is on entity {gameObject.name} with NOB ID: {this.NetworkObject.ObjectId}"); */
        
        // First check if this HandManager instance is for the correct entity
        Debug.Log($"HandManager (Client RPC): RpcSetCardParent - VALIDATION 1: this.NetworkObject.ObjectId ({this.NetworkObject.ObjectId}) vs parentEntityNetObjId ({parentEntityNetObjId})");
        if (this.NetworkObject.ObjectId != parentEntityNetObjId)
        {
            Debug.Log($"HandManager (Client RPC): RpcSetCardParent - This HandManager is for entity {this.NetworkObject.ObjectId}, but card belongs to entity {parentEntityNetObjId}. Ignoring.");
            return;
        }
        
        // Find the card by NetworkObject ID
        NetworkObject cardNetObj = null;
        bool foundCard = false;
        
        if (NetworkManager.IsClientStarted) // Should only execute on clients now
        {
            foundCard = NetworkManager.ClientManager.Objects.Spawned.TryGetValue(cardNetObjId, out cardNetObj);
        }
                
        if (!foundCard || cardNetObj == null)
        {
            Debug.LogError($"HandManager (Client RPC): RpcSetCardParent - Failed to find card NetworkObject with ID {cardNetObjId}");
            return;
        }
        
        // Additional validation: Check if the card actually belongs to this entity
        Card card = cardNetObj.GetComponent<Card>();
        /* Debug.Log($"HandManager (Client RPC): RpcSetCardParent - VALIDATION 2: Card component found: {card != null}"); */
        if (card != null && card.OwnerEntity != null)
        {
            NetworkObject cardOwnerNetObj = card.OwnerEntity.GetComponent<NetworkObject>();
            /* Debug.Log($"HandManager (Client RPC): RpcSetCardParent - VALIDATION 2: Card.OwnerEntity = {card.OwnerEntity.EntityName.Value}, cardOwnerNetObj.ObjectId = {cardOwnerNetObj?.ObjectId ?? -1}, this.NetworkObject.ObjectId = {this.NetworkObject.ObjectId}"); */
            if (cardOwnerNetObj != null && cardOwnerNetObj.ObjectId != this.NetworkObject.ObjectId)
            {
                /* Debug.Log($"HandManager (Client RPC): RpcSetCardParent - Card {card.name} belongs to entity {card.OwnerEntity.EntityName.Value} (NOB ID: {cardOwnerNetObj.ObjectId}), not this entity (NOB ID: {this.NetworkObject.ObjectId}). Ignoring."); */
                return;
            }
        }
        
        // Find the parent entity by NetworkObject ID
        NetworkObject parentEntityNetObj = null;
        bool foundParent = false;
        
        if (NetworkManager.IsClientStarted)
        {
            foundParent = NetworkManager.ClientManager.Objects.Spawned.TryGetValue(parentEntityNetObjId, out parentEntityNetObj);
        }
                
        if (!foundParent || parentEntityNetObj == null)
        {
            Debug.LogError($"HandManager (Client RPC): RpcSetCardParent - Failed to find parent entity NetworkObject with ID {parentEntityNetObjId}");
            return;
        }
        
        // Find the target transform within the parent entity
        Transform targetTransform = null;
        NetworkEntityUI entityUI = parentEntityNetObj.GetComponent<NetworkEntityUI>();
        
        if (entityUI != null)
        {
            switch (targetTransformName)
            {
                case "Hand":
                    targetTransform = entityUI.GetHandTransform();
                    break;
                case "Deck":
                    targetTransform = entityUI.GetDeckTransform();
                    break;
                case "Discard":
                    targetTransform = entityUI.GetDiscardTransform();
                    break;
            }
        }
        
        if (targetTransform == null)
        {
            Debug.LogError($"HandManager (Client RPC): RpcSetCardParent - Failed to find {targetTransformName} transform on entity with ID {parentEntityNetObjId}");
            return;
        }
        
        // Set the card's parent to the target transform
        GameObject cardObject = cardNetObj.gameObject;
        /* Debug.Log($"HandManager (Client RPC): RpcSetCardParent - VALIDATION PASSED - Setting card {cardObject.name} parent to {targetTransform.name} on {gameObject.name}"); */
        cardObject.transform.SetParent(targetTransform);
        
        // Only reset position if no HandLayoutManager is present to avoid interfering with custom layouts
        HandLayoutManager handLayoutManager = targetTransform.GetComponent<HandLayoutManager>();
        if (handLayoutManager == null && targetTransformName == "Hand")
        {
            cardObject.transform.localPosition = Vector3.zero;
            /* Debug.Log($"HandManager (Client RPC): Reset {cardObject.name} position to zero (no HandLayoutManager found)"); */
        }
        else if (targetTransformName != "Hand")
        {
            // Always reset position for Deck and Discard (they don't use custom layouts)
        cardObject.transform.localPosition = Vector3.zero;
            /* Debug.Log($"HandManager (Client RPC): Reset {cardObject.name} position to zero for {targetTransformName}"); */
        }
        else
        {
            /* Debug.Log($"HandManager (Client RPC): Skipped position reset for {cardObject.name} - HandLayoutManager detected on {handLayoutManager.gameObject.name}"); */
        }
    }

    /// <summary>
    /// RPC to reset card visual state on clients (alpha, scale, etc.) when moving to hand
    /// </summary>
    [ObserversRpc]
    private void RpcResetCardVisualState(int cardNetObjId)
    {
        // Find the card NetworkObject
        NetworkObject cardNetObj = null;
        bool foundCard = false;
        
        if (NetworkManager.IsClientStarted)
        {
            foundCard = NetworkManager.ClientManager.Objects.Spawned.TryGetValue(cardNetObjId, out cardNetObj);
        }
        
        if (!foundCard || cardNetObj == null)
        {
            Debug.LogError($"HandManager (Client RPC): RpcResetCardVisualState - Failed to find card NetworkObject with ID {cardNetObjId}");
            return;
        }
        
        GameObject cardObject = cardNetObj.gameObject;
        
        // Reset card visual state for redraw - same logic as server-side MoveCardToHand
        CanvasGroup cardCanvasGroup = cardObject.GetComponent<CanvasGroup>();
        if (cardCanvasGroup != null)
        {
            cardCanvasGroup.alpha = 1.0f; // Reset alpha from previous play animation
            /* Debug.Log($"HandManager (Client RPC): Reset alpha to 1.0 for redrawn card {cardObject.name}"); */
        }
        
        // Reset CardAnimator state if present
        CardAnimator cardAnimator = cardObject.GetComponent<CardAnimator>();
        if (cardAnimator != null)
        {
            cardAnimator.StoreOriginalState(); // Store fresh state with alpha = 1.0
            /* Debug.Log($"HandManager (Client RPC): Refreshed CardAnimator state for redrawn card {cardObject.name}"); */
        }
    }

    private List<GameObject> GetCardsInTransform(Transform parent)
    {
        if (parent == null)
        {
            Debug.LogError($"HandManager: Attempted to get cards from null transform on {gameObject.name}");
            return new List<GameObject>();
        }

        List<GameObject> cards = new List<GameObject>();
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform childTransform = parent.GetChild(i);
            if (childTransform != null && childTransform.gameObject != null)
            {
                cards.Add(childTransform.gameObject);
                // Debug.Log($"HandManager: Added card {childTransform.gameObject.name} from {parent.name}"); // Optional: very verbose
            }
            else
            {
                Debug.LogError($"HandManager: Found null child transform or null gameObject at index {i} in {parent.name}");
            }
        }
        
        /* Debug.Log($"HandManager: Found {cards.Count} cards in {parent.name} for {gameObject.name}"); */
        return cards;
    }

    // Helper method to get transform path for debugging
    private string GetTransformPath(Transform transform)
    {
        if (transform == null) return "null";
        
        string path = transform.name;
        Transform parent = transform.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }

    /// <summary>
    /// Gets the hand transform for direct access
    /// </summary>
    public Transform GetHandTransform()
    {
        return handTransform;
    }

    /// <summary>
    /// Gets the deck transform for direct access
    /// </summary>
    public Transform GetDeckTransform()
    {
        return deckTransform;
    }

    /// <summary>
    /// Gets the discard transform for direct access
    /// </summary>
    public Transform GetDiscardTransform()
    {
        return discardTransform;
    }

    /// <summary>
    /// Gets all cards currently in the hand
    /// </summary>
    public List<GameObject> GetCardsInHand()
    {
        return GetCardsInTransform(handTransform);
    }

    /// <summary>
    /// Gets all cards currently in the discard pile
    /// </summary>
    public List<GameObject> GetCardsInDiscard()
    {
        return GetCardsInTransform(discardTransform);
    }

    /// <summary>
    /// Gets all cards currently in the deck
    /// </summary>
    public List<GameObject> GetCardsInDeck()
    {
        return GetCardsInTransform(deckTransform);
    }

    /// <summary>
    /// Despawns all cards for this entity (hand, deck, and discard)
    /// Called when a fight ends to clean up remaining cards
    /// </summary>
    [Server]
    public void DespawnAllCards()
    {
        if (!IsServerInitialized)
        {
            Debug.LogError($"HandManager: Cannot despawn cards - server not initialized for {gameObject.name}");
            return;
        }
        
        /* Debug.Log($"HandManager: Despawning all cards for {gameObject.name}"); */
        
        // Get CardSpawner component
        CardSpawner cardSpawner = GetComponent<CardSpawner>();
        if (cardSpawner == null)
        {
            Debug.LogError($"HandManager: Missing CardSpawner component on {gameObject.name}");
            return;
        }
        
        // Collect all cards from all locations
        List<GameObject> allCards = new List<GameObject>();
        
        // Add cards from hand
        if (handTransform != null)
        {
            List<GameObject> handCards = GetCardsInTransform(handTransform);
            allCards.AddRange(handCards);
            /* Debug.Log($"HandManager: Found {handCards.Count} cards in hand for {gameObject.name}"); */
        }
        
        // Add cards from deck
        if (deckTransform != null)
        {
            List<GameObject> deckCards = GetCardsInTransform(deckTransform);
            allCards.AddRange(deckCards);
            /* Debug.Log($"HandManager: Found {deckCards.Count} cards in deck for {gameObject.name}"); */
        }
        
        // Add cards from discard
        if (discardTransform != null)
        {
            List<GameObject> discardCards = GetCardsInTransform(discardTransform);
            allCards.AddRange(discardCards);
            /* Debug.Log($"HandManager: Found {discardCards.Count} cards in discard for {gameObject.name}"); */
        }
        
        // Despawn all cards
        /* Debug.Log($"HandManager: Despawning {allCards.Count} total cards for {gameObject.name}"); */
        foreach (GameObject card in allCards)
        {
            if (card != null)
            {
                /* Debug.Log($"HandManager: Despawning card {card.name} for {gameObject.name}"); */
                cardSpawner.DespawnCard(card);
            }
        }
        
        /* Debug.Log($"HandManager: Finished despawning all cards for {gameObject.name}"); */
    }
    
    private void LogVerbose(string message)
    {
        if (verboseLogging)
        {
            Debug.Log($"[HandManager] {message}");
        }
    }
}

// Keep the CardLocation enum definition
public enum CardLocation
{
    Deck,
    Hand,
    Discard
} 