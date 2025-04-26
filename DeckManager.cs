using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class DeckManager
{
    private GameManager gameManager;
    
    // Core deck components
    private List<CardData> deck = new List<CardData>();
    private List<CardData> hand = new List<CardData>();
    private List<CardData> discardPile = new List<CardData>();
    
    // Configuration
    private int cardsToDraw = 5;
    private List<CardData> starterDeck = new List<CardData>();
    
    public DeckManager(GameManager gameManager, List<CardData> starterDeck, int cardsToDraw)
    {
        this.gameManager = gameManager;
        this.starterDeck = new List<CardData>(starterDeck);
        this.cardsToDraw = cardsToDraw;
    }
    
    public void InitializeDecks()
    {
        // Check if this is likely the first round (deck, hand, discard are empty)
        bool isFirstRound = (deck.Count == 0 && hand.Count == 0 && discardPile.Count == 0);
        if (isFirstRound)
        {
            Debug.Log("First round detected: Initializing player deck from starterDeck.");
            deck = new List<CardData>(starterDeck);
        }
        else
        {
            // Subsequent rounds: Combine hand/discard into deck
            Debug.Log("Subsequent round detected: Reshuffling existing player deck/hand/discard.");
            discardPile.AddRange(hand);
            hand.Clear();
            deck.AddRange(discardPile);
            discardPile.Clear();
        }
        ShuffleDeck(); // Shuffle the full player deck (either new or combined)
    }
    
    public void DrawHand()
    {
        hand.Clear();
        Debug.Log($"DrawHand START: Attempting to draw {cardsToDraw} cards. Current state - Deck: {deck.Count}, Discard: {discardPile.Count}");
        int cardsDrawnThisTurn = 0;
        for (int i = 0; i < cardsToDraw; i++)
        {
            if (!DrawCard()) // If DrawCard returns false, stop drawing
            {
                Debug.Log($"Could not draw card {i+1} of {cardsToDraw} (deck/discard likely empty). Stopping draw.");
                break;
            }
            cardsDrawnThisTurn++;
        }
        Debug.Log($"DrawHand END: Finished drawing. Hand size: {hand.Count}. Cards drawn this turn: {cardsDrawnThisTurn}. Final state - Deck: {deck.Count}, Discard: {discardPile.Count}");
    }
    
    /// <summary>
    /// Draws a single card from the deck into the hand.
    /// Handles reshuffling the discard pile if the deck is empty.
    /// </summary>
    /// <returns>True if a card was successfully drawn, false otherwise (deck and discard are empty).</returns>
    public bool DrawCard()
    {
        if (deck.Count == 0)
        {
            if (discardPile.Count == 0)
            {
                Debug.Log("No cards left to draw!");
                return false; // Out of cards
            }
            
            ReshuffleDiscardPile();
        }
        
        if (deck.Count > 0) // Check again after potential reshuffle
        {
            CardData drawnCard = deck[0];
            deck.RemoveAt(0);
            hand.Add(drawnCard);
            
            // --- ADDED: Ensure proper layering for drawn cards ---
            // After updating the UI with the new card, we need to ensure sibling indices are properly sorted
            gameManager.StartCoroutine(EnsureProperCardLayering());
            // --- END ADDED ---
            
            Debug.Log($"DrawCard SUCCESS: Drew '{drawnCard.cardName}'. New state - Deck: {deck.Count}, Hand: {hand.Count}, Discard: {discardPile.Count}");
            return true;
        }
        return false;
    }
    
    // --- ADDED: Helper method to ensure card layering ---
    private IEnumerator EnsureProperCardLayering()
    {
        // Wait for a brief moment to let the UI update happen first (cards are instantiated)
        yield return new WaitForEndOfFrame();
        
        // Get hand panel hover manager
        CombatUIManager combatUI = gameManager.GetCombatUIManager();
        if (combatUI != null)
        {
            GameObject playerHandPanel = combatUI.GetPlayerHandPanel();
            if (playerHandPanel != null)
            {
                HandPanelHoverManager hoverManager = playerHandPanel.GetComponent<HandPanelHoverManager>();
                if (hoverManager != null)
                {
                    hoverManager.ResortSiblingIndices();
                    Debug.Log("[DeckManager] Called ResortSiblingIndices after card draw.");
                }
            }
        }
    }
    // --- END ADDED ---
    
    public void DiscardHand()
    {
        List<CardData> cardsToDiscard = new List<CardData>(hand); // Create a copy to iterate over
        hand.Clear();
        foreach(CardData card in cardsToDiscard)
        {
            discardPile.Add(card);
            // Discard triggers are handled by CardEffectService
        }
    }
    
    public bool DiscardCard(CardData card, GameObject cardGO = null)
    {
        // Trigger animation *before* removing from hand list
        CombatUIManager combatUIManager = gameManager.GetCombatUIManager();
        if (combatUIManager != null)
        {   
            // Pass the optional GameObject along
            combatUIManager.TriggerDiscardAnimation(card, cardGO);
        }
        else
        {
            Debug.LogWarning("CombatUIManager not found, cannot trigger discard animation for single card discard.");
        }
        
        // Now attempt to remove from hand and add to discard
        if (hand.Remove(card))
        {
            discardPile.Add(card);
            return true;
        }
        return false;
    }
    
    public void ReshuffleDiscardPile()
    {
        Debug.Log($"ReshuffleDiscardPile START: Reshuffling {discardPile.Count} cards from discard into deck (current deck size: {deck.Count}).");
        deck.AddRange(discardPile);
        discardPile.Clear();
        ShuffleDeck();
        Debug.Log($"ReshuffleDiscardPile END: Deck size after reshuffle: {deck.Count}, Discard size: {discardPile.Count}");
    }
    
    public void ShuffleDeck()
    {
        System.Random rng = new System.Random();
        int n = deck.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            CardData value = deck[k];
            deck[k] = deck[n];
            deck[n] = value;
        }
        Debug.Log("Deck shuffled.");
    }
    
    public List<CardData> DiscardRandomCards(int amount, CardDropZone.TargetType targetType = CardDropZone.TargetType.PlayerSelf)
    {
        List<CardData> discardedCards = new List<CardData>(); // List to return
        System.Random rng = new System.Random();
        int cardsToDiscardCount = Mathf.Min(amount, hand.Count); // Can't discard more than in hand
        
        // We need to find the cards first, trigger animations, then remove/add
        List<CardData> cardsToProcess = new List<CardData>();
        List<int> indicesToRemove = new List<int>();
        
        if (hand.Count > 0 && cardsToDiscardCount > 0)
        {
            // Select indices to remove without duplicates
            List<int> availableIndices = Enumerable.Range(0, hand.Count).ToList();
            for (int i = 0; i < cardsToDiscardCount; i++)
            {
                int randomIndexInAvailable = rng.Next(availableIndices.Count);
                int actualHandIndex = availableIndices[randomIndexInAvailable];
                indicesToRemove.Add(actualHandIndex);
                availableIndices.RemoveAt(randomIndexInAvailable); // Prevent selecting the same index again
            }
            
            // Sort indices descending so removing doesn't mess up subsequent indices
            indicesToRemove.Sort((a, b) => b.CompareTo(a)); 

            // Get the cards and trigger animations
            CombatUIManager combatUIManager = gameManager.GetCombatUIManager();
            foreach (int index in indicesToRemove)
            {
                 CardData cardToDiscard = hand[index];
                 cardsToProcess.Add(cardToDiscard);
                 combatUIManager?.TriggerDiscardAnimation(cardToDiscard);
            }

            // Now actually remove from hand, add to discard, and track for return
            foreach (CardData cardToDiscard in cardsToProcess)
            {
                if (hand.Remove(cardToDiscard)) // Remove the specific card instance
                {
                    discardPile.Add(cardToDiscard);
                    discardedCards.Add(cardToDiscard); // Add to return list
                    Debug.Log($"Randomly discarded player card: {cardToDiscard.cardName}");

                    // The discard trigger is handled by CardManager (if the card has an effect)
                    gameManager.GetCardManager().HandleDiscardTrigger(cardToDiscard, targetType);
                }
                else
                {
                     Debug.LogWarning($"[DiscardRandomCards] Failed to remove card {cardToDiscard.cardName} from hand list during processing. Was it removed elsewhere?");
                }
            }
        }
        
        return discardedCards; // Return the list
    }
    
    public void AddCardToPlayerDeck(CardData card)
    {
        if (card != null)
        {
            deck.Add(card);
            ShuffleDeck();
        }
    }
    
    // Getters
    public List<CardData> GetDeck() => deck;
    public List<CardData> GetHand() => hand;
    public List<CardData> GetDiscardPile() => discardPile;
    public int GetDeckCount() => deck.Count;
    public int GetDiscardCount() => discardPile.Count;
    public int GetHandCount() => hand.Count;
    
    public List<CardData> GetAllOwnedPlayerCards()
    {
        // Creates a new list containing cards from deck, hand, and discard
        List<CardData> allCards = new List<CardData>(deck);
        allCards.AddRange(hand);
        allCards.AddRange(discardPile);
        return allCards;
    }
}