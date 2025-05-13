using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages card movement between deck, hand, and discard pile during gameplay.
/// Attach to: Both NetworkPlayer and NetworkPet prefabs to handle their card operations.
/// </summary>
// Convert to MonoBehaviour to be attached to NetworkPlayer and NetworkPet prefabs
public class HandManager : MonoBehaviour
{
    private System.Random rng = new System.Random();
    private NetworkBehaviour parentEntity; // Reference to the NetworkPlayer or NetworkPet this is attached to
    private CombatHand combatHand; // Reference to the CombatHand component
    private CombatDeck combatDeck; // Reference to the CombatDeck component
    private CombatDiscard combatDiscard; // Reference to the CombatDiscard component
    private CardSpawner cardSpawner; // Reference to the CardSpawner component

    private void Awake()
    {
        // Get reference to the parent entity
        parentEntity = GetComponent<NetworkPlayer>() as NetworkBehaviour;
        if (parentEntity == null)
        {
            parentEntity = GetComponent<NetworkPet>() as NetworkBehaviour;
        }
        
        // Get references to required components
        combatHand = GetComponent<CombatHand>();
        combatDeck = GetComponent<CombatDeck>();
        combatDiscard = GetComponent<CombatDiscard>();
        cardSpawner = GetComponent<CardSpawner>();
        
        // Validate required components
        if (combatHand == null) Debug.LogError("HandManager requires a CombatHand component");
        if (combatDeck == null) Debug.LogError("HandManager requires a CombatDeck component");
        if (combatDiscard == null) Debug.LogError("HandManager requires a CombatDiscard component");
        if (cardSpawner == null) Debug.LogError("HandManager requires a CardSpawner component");
    }

    /// <summary>
    /// Draws a single card from the deck to the hand
    /// </summary>
    /// <returns>True if a card was drawn, false if deck is empty or hand is full</returns>
    public bool DrawCard()
    {
        // Check if we're on the server
        if (!FishNet.InstanceFinder.IsServerStarted)
        {
            Debug.LogWarning("DrawCard can only be called on the server");
            return false;
        }
        
        // Check if hand is full
        if (combatHand.IsFull())
        {
            Debug.Log($"Cannot draw card: Hand is full ({combatHand.GetCardCount()}/{combatHand.GetAvailableSpace()})");
            return false;
        }
        
        // Draw a card from the deck
        List<GameObject> drawnCards = combatDeck.DrawCards(1);
        
        // If we got a card, add it to the hand
        if (drawnCards.Count > 0)
        {
            GameObject drawnCard = drawnCards[0];
            return combatHand.AddCard(drawnCard);
        }
        else
        {
            Debug.Log("Cannot draw card: Deck is empty");
            return false;
        }
    }

    /// <summary>
    /// Draws multiple cards from the deck to the hand
    /// </summary>
    /// <param name="count">Number of cards to draw</param>
    /// <returns>Number of cards actually drawn</returns>
    public int DrawCards(int count)
    {
        // Check if we're on the server
        if (!FishNet.InstanceFinder.IsServerStarted)
        {
            Debug.LogWarning("DrawCards can only be called on the server");
            return 0;
        }
        
        int cardsDrawn = 0;
        
        // Draw cards while we have space and cards available
        for (int i = 0; i < count; i++)
        {
            if (DrawCard())
            {
                cardsDrawn++;
            }
            else
            {
                // If we couldn't draw a card, stop trying
                break;
            }
        }
        
        return cardsDrawn;
    }

    /// <summary>
    /// Moves a card from hand to discard pile
    /// </summary>
    /// <param name="cardId">ID of the card to move</param>
    /// <returns>True if the card was moved</returns>
    public bool MoveCardToDiscard(int cardId)
    {
        // Check if we're on the server
        if (!FishNet.InstanceFinder.IsServerStarted)
        {
            Debug.LogWarning("MoveCardToDiscard can only be called on the server");
            return false;
        }
        
        // Find and remove the card from hand
        GameObject cardObj = combatHand.RemoveCardById(cardId);
        
        if (cardObj != null)
        {
            // Add the card to the discard pile
            combatDiscard.AddCard(cardObj);
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Moves a specific card object from hand to discard pile
    /// </summary>
    /// <param name="cardObj">Card GameObject to move</param>
    /// <returns>True if the card was moved</returns>
    public bool MoveCardToDiscard(GameObject cardObj)
    {
        // Check if we're on the server
        if (!FishNet.InstanceFinder.IsServerStarted)
        {
            Debug.LogWarning("MoveCardToDiscard can only be called on the server");
            return false;
        }
        
        // Remove the card from hand
        if (combatHand.RemoveCard(cardObj))
        {
            // Add the card to the discard pile
            combatDiscard.AddCard(cardObj);
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Discards the entire hand
    /// </summary>
    public void DiscardHand()
    {
        // Check if we're on the server
        if (!FishNet.InstanceFinder.IsServerStarted)
        {
            Debug.LogWarning("DiscardHand can only be called on the server");
            return;
        }
        
        // Get all cards from hand
        List<GameObject> discardedCards = combatHand.DiscardHand();
        
        // Move all cards to discard pile
        foreach (GameObject cardObj in discardedCards)
        {
            combatDiscard.AddCard(cardObj);
        }
        
        Debug.Log($"Discarded {discardedCards.Count} cards from {parentEntity.name}'s hand");
    }

    /// <summary>
    /// Shuffles all cards from discard pile back into deck
    /// </summary>
    public void ShuffleDiscardIntoDeck()
    {
        // Check if we're on the server
        if (!FishNet.InstanceFinder.IsServerStarted)
        {
            Debug.LogWarning("ShuffleDiscardIntoDeck can only be called on the server");
            return;
        }
        
        // Get all cards from discard
        List<GameObject> cardsToShuffle = combatDiscard.GetAllCards();
        int cardCount = cardsToShuffle.Count;
        
        // Clear discard pile
        combatDiscard.ClearDiscard();
        
        // Add cards to deck and shuffle
        combatDeck.AddCardsToDeck(cardsToShuffle);
        
        Debug.Log($"Shuffled {cardCount} cards from discard into {parentEntity.name}'s deck");
    }
    
    /// <summary>
    /// Reshuffles discard pile into deck
    /// </summary>
    private void ReshuffleDiscardIntoDeck()
    {
        if (parentEntity == null || !parentEntity.IsServerStarted || combatDiscard == null || combatDeck == null) return;
        
        // Get all cards from discard
        List<GameObject> cardsToShuffle = combatDiscard.GetAllCards();
        
        // If there are no cards in discard, return
        if (cardsToShuffle.Count == 0)
        {
            Debug.Log($"{GetEntityName()} has no cards in discard pile to reshuffle.");
            return;
        }
        
        // Clear discard pile
        combatDiscard.ClearDiscard();
        
        // Add cards to deck
        combatDeck.AddCardsToDeck(cardsToShuffle);
        
        // Shuffle the deck
        combatDeck.ShuffleDeck();
        
        Debug.Log($"Reshuffled {cardsToShuffle.Count} cards from discard into {GetEntityName()}'s deck");
    }

    // Server-side logic for drawing initial cards
    public void DrawInitialCardsForEntity(int drawAmount)
    {
        if (parentEntity == null || !FishNet.InstanceFinder.IsServerStarted) return;

        string entityName = GetEntityName();
        
        ShuffleDeck(); // Shuffle before drawing
        
        Debug.Log($"HandManager: Drawing {drawAmount} cards for {entityName}.");

        // Check hand capacity before drawing
        int availableSpace = combatHand != null ? combatHand.GetAvailableSpace() : 0;
        int actualDrawAmount = Mathf.Min(drawAmount, availableSpace);
        
        if (actualDrawAmount < drawAmount)
        {
            Debug.LogWarning($"HandManager: {entityName} can only draw {actualDrawAmount} of {drawAmount} cards due to hand size limit.");
        }
        
        // Draw cards directly from deck - this uses the existing card GameObjects already created in the deck
        // and moves them to the hand rather than creating new ones
        int cardsDrawn = 0;
        for (int i = 0; i < actualDrawAmount; i++)
        {
            bool success = DrawOneCard();
            if (success)
            {
                cardsDrawn++;
            }
            else
            {
                Debug.LogWarning($"HandManager: {entityName} failed to draw card {i+1} of {actualDrawAmount}. Only drew {cardsDrawn} cards.");
                break;
            }
        }
        
        // Log the cards in hand after drawing
        Debug.Log($"After drawing, {entityName} has {combatHand.GetCardCount()} cards in hand");
    }

    // Server-side logic for drawing one card
    // Returns true if a card was successfully drawn
    public bool DrawOneCard()
    {
        if (parentEntity == null || !FishNet.InstanceFinder.IsServerStarted) return false;
        if (combatHand == null)
        {
            Debug.LogError("HandManager: Cannot draw card - CombatHand component not found.");
            return false;
        }

        // Check if hand is full before attempting to draw
        if (combatHand.IsFull())
        {
            Debug.Log($"{GetEntityName()} hand is full. Cannot draw more cards.");
            return false;
        }

        string entityName = GetEntityName();

        // Check if deck is empty
        if (combatDeck.GetDeckSize() == 0)
        {
            // Reshuffle discard into deck if deck is empty
            if (combatDiscard.GetCardCount() > 0)
            {
                Debug.Log($"{entityName} deck is empty. Reshuffling discard pile into deck.");
                ReshuffleDiscardIntoDeck();
                
                // Check if reshuffle was successful
                if (combatDeck.GetDeckSize() == 0)
                {
                    Debug.LogWarning($"{entityName} deck is still empty after reshuffling. Cannot draw a card.");
                    return false;
                }
                
                Debug.Log($"{entityName} successfully reshuffled discard. Continuing with draw.");
            }
            else
            {
                Debug.Log($"{entityName} has no cards in deck or discard to draw.");
                return false; // No cards left anywhere
            }
        }

        // Get existing card GameObject from deck - these were created during combat setup
        // and are stored in the deck's deckCards list
        List<GameObject> drawnCards = combatDeck.DrawCards(1);
        if (drawnCards.Count == 0)
        {
            Debug.LogWarning($"{entityName} failed to draw a card from deck (size: {combatDeck.GetDeckSize()}).");
            return false;
        }
        
        // Get the first card object from the list
        GameObject cardObj = drawnCards[0];
        
        // Add the existing GameObject to CombatHand - no new card is created here
        bool added = combatHand.AddCard(cardObj);
        if (!added)
        {
            // If adding to hand failed, put card back in deck 
            // Using AddCardToDeck to maintain the same card object
            combatDeck.AddCardToDeck(cardObj);
            Debug.LogWarning($"{entityName} failed to add card to hand. Card returned to deck.");
            return false;
        }
        
        // Get card ID for logging
        Card cardComponent = cardObj.GetComponent<Card>();
        int cardId = cardComponent != null ? cardComponent.CardId : -1;

        Debug.Log($"{entityName} drew card ID: {cardId}. Hand size: {combatHand.GetCardCount()}");
        return true;
    }

    // Server-side logic to shuffle the deck
    private void ShuffleDeck()
    {
        if (parentEntity == null || !parentEntity.IsServerStarted || combatDeck == null) return;
        
        combatDeck.ShuffleDeck();
    }

    // Get entity name for logging
    private string GetEntityName()
    {
        NetworkPlayer player = parentEntity as NetworkPlayer;
        NetworkPet pet = parentEntity as NetworkPet;
        
        if (player != null)
        {
            return player.PlayerName.Value;
        }
        else if (pet != null)
        {
            return pet.PetName.Value;
        }
        
        return "Unknown Entity";
    }

    // Card GameObjects parenting/movement in the scene hierarchy:
    // This is typically handled on the client-side by CombatCanvasManager.RenderHand() in response to SyncList changes.
    // The server dictates the *state* (which card IDs are in which list).
    // Clients then update their visuals. For example, when a card ID appears in `playerHandCardIds`,
    // the client instantiates a visual representation of that card and parents it to `playerHandDisplayArea`.
    // When an ID moves from `playerHandCardIds` to `discardPileCardIds`, the client moves the corresponding GameObject.
} 