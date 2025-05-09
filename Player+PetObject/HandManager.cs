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

    private void Awake()
    {
        // Get the parent NetworkBehaviour (either NetworkPlayer or NetworkPet)
        parentEntity = GetComponent<NetworkPlayer>() as NetworkBehaviour;
        if (parentEntity == null)
        {
            parentEntity = GetComponent<NetworkPet>() as NetworkBehaviour;
        }

        if (parentEntity == null)
        {
            Debug.LogError("HandManager: Not attached to a NetworkPlayer or NetworkPet. This component must be attached to one of these.");
        }
        
        // Get the required components
        combatHand = GetComponent<CombatHand>();
        if (combatHand == null)
        {
            Debug.LogError("HandManager: CombatHand component not found. This component must be attached to the same GameObject.");
        }
        
        combatDeck = GetComponent<CombatDeck>();
        if (combatDeck == null)
        {
            Debug.LogError("HandManager: CombatDeck component not found. This component must be attached to the same GameObject.");
        }
        
        combatDiscard = GetComponent<CombatDiscard>();
        if (combatDiscard == null)
        {
            Debug.LogError("HandManager: CombatDiscard component not found. This component must be attached to the same GameObject.");
        }
    }

    // Server-side logic for drawing initial cards
    public void DrawInitialCardsForEntity(int drawAmount)
    {
        if (parentEntity == null || !parentEntity.IsServerStarted) return;

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
        
        for (int i = 0; i < actualDrawAmount; i++)
        {
            DrawOneCard();
        }
    }

    // Server-side logic for drawing one card
    public void DrawOneCard()
    {
        if (parentEntity == null || !parentEntity.IsServerStarted) return;
        if (combatHand == null)
        {
            Debug.LogError("HandManager: Cannot draw card - CombatHand component not found.");
            return;
        }

        // Check if hand is full before attempting to draw
        if (combatHand.IsFull())
        {
            Debug.Log($"{GetEntityName()} hand is full. Cannot draw more cards.");
            return;
        }

        string entityName = GetEntityName();

        // Check if deck is empty
        if (combatDeck.GetDeckSize() == 0)
        {
            // Reshuffle discard into deck if deck is empty
            if (combatDiscard.GetCardCount() > 0)
            {
                ReshuffleDiscardIntoDeck(); 
                if (combatDeck.GetDeckSize() == 0) { // Still no cards (reshuffle failed)
                     Debug.Log($"{entityName} deck is still empty after attempting reshuffle.");
                     return;
                }
            }
            else
            {
                Debug.Log($"{entityName} has no cards in deck or discard to draw.");
                return; // No cards left anywhere
            }
        }

        // Draw one card from deck
        List<int> drawnCards = combatDeck.DrawCards(1);
        if (drawnCards.Count == 0)
        {
            Debug.LogWarning($"{entityName} failed to draw a card from deck.");
            return;
        }
        
        int cardIdToDraw = drawnCards[0];
        
        // Add to CombatHand
        bool added = combatHand.AddCard(cardIdToDraw);
        if (!added)
        {
            // If adding to hand failed, put card back in deck
            combatDeck.AddCard(cardIdToDraw);
            Debug.LogWarning($"{entityName} failed to add card ID: {cardIdToDraw} to hand. Card returned to deck.");
            return;
        }

        Debug.Log($"{entityName} drew card ID: {cardIdToDraw}. Hand size: {combatHand.GetCardCount()}");
    }

    // Server-side logic for discarding entire hand
    public void DiscardHand()
    {
        if (parentEntity == null || !parentEntity.IsServerStarted) return;
        if (combatHand == null || combatDiscard == null)
        {
            Debug.LogError("HandManager: Cannot discard hand - CombatHand or CombatDiscard component not found.");
            return;
        }

        string entityName = GetEntityName();

        if (combatHand.IsEmpty())
        {
            Debug.Log($"{entityName} has no cards in hand to discard.");
            return;
        }

        // Get all cards from hand and discard them
        List<int> cardsToDiscard = combatHand.GetAllCards();
        foreach (int cardId in cardsToDiscard)
        {
            combatDiscard.AddCard(cardId);
        }
        
        // Clear the hand
        combatHand.DiscardHand();

        Debug.Log($"{entityName} discarded {cardsToDiscard.Count} cards.");
    }

    // Server-side logic for moving a specific card from hand to discard
    public void MoveCardToDiscard(int cardId)
    {
        if (parentEntity == null || !parentEntity.IsServerStarted) return;
        if (combatHand == null || combatDiscard == null)
        {
            Debug.LogError("HandManager: Cannot move card to discard - CombatHand or CombatDiscard component not found.");
            return;
        }

        string entityName = GetEntityName();

        if (combatHand.HasCard(cardId))
        {
            // Remove from hand
            combatHand.RemoveCard(cardId);
            
            // Add to discard pile
            combatDiscard.AddCard(cardId);
            
            Debug.Log($"{entityName} moved card ID {cardId} from hand to discard.");
        }
        else
        {
            Debug.LogWarning($"{entityName} tried to move card ID {cardId} to discard, but it was not in hand.");
        }
    }

    // Server-side logic to shuffle the deck
    private void ShuffleDeck()
    {
        if (parentEntity == null || !parentEntity.IsServerStarted || combatDeck == null) return;
        
        combatDeck.ShuffleDeck();
    }

    // Server-side logic to move cards from discard to deck and shuffle
    private void ReshuffleDiscardIntoDeck()
    {
        if (parentEntity == null || !parentEntity.IsServerStarted) return;
        if (combatDeck == null || combatDiscard == null)
        {
            Debug.LogError($"HandManager: Cannot reshuffle discard for {GetEntityName()} - CombatDeck or CombatDiscard component not found.");
            return;
        }

        string entityName = GetEntityName();

        if (combatDiscard.GetCardCount() == 0)
        {
            Debug.Log($"{entityName} has no cards in discard to reshuffle.");
            return;
        }

        // Get all cards from discard
        List<int> discardCards = combatDiscard.GetAllCards();
        if (discardCards.Count == 0)
        {
            Debug.LogWarning($"Failed to get discard cards for {entityName}.");
            return;
        }
        
        // Add to deck and shuffle
        combatDeck.AddCardsToDeck(discardCards);
        
        // Clear the discard pile
        combatDiscard.ClearDiscard();

        Debug.Log($"{entityName}'s discard reshuffled into deck. New deck size: {combatDeck.GetDeckSize()}");
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