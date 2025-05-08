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
    }

    // Server-side logic for drawing initial cards
    public void DrawInitialCardsForEntity(int drawAmount)
    {
        if (parentEntity == null || !parentEntity.IsServerStarted) return;

        string entityName = "Unknown Entity";
        
        if (parentEntity is NetworkPlayer player)
        {
            entityName = player.PlayerName.Value;
            ShuffleDeck(); // Shuffle before drawing
        }
        else if (parentEntity is NetworkPet pet)
        {
            entityName = pet.PetName.Value;
            ShuffleDeck(); // Shuffle before drawing
        }

        Debug.Log($"HandManager: Drawing {drawAmount} cards for {entityName}.");
        for (int i = 0; i < drawAmount; i++)
        {
            DrawOneCard();
        }
    }

    // Server-side logic for drawing one card
    public void DrawOneCard()
    {
        if (parentEntity == null || !parentEntity.IsServerStarted) return;

        SyncList<int> deckIds = null;
        SyncList<int> handIds = null;
        SyncList<int> discardIds = null;
        Transform handTransform = null; 
        string entityName = "Unknown Entity";

        NetworkPlayer player = parentEntity as NetworkPlayer;
        NetworkPet pet = parentEntity as NetworkPet;

        if (player != null)
        {
            deckIds = player.currentDeckCardIds;
            handIds = player.playerHandCardIds;
            discardIds = player.discardPileCardIds;
            handTransform = player.PlayerHandTransform;
            entityName = player.PlayerName.Value;
        }
        else if (pet != null)
        {
            deckIds = pet.currentDeckCardIds;
            handIds = pet.playerHandCardIds;
            discardIds = pet.discardPileCardIds;
            handTransform = pet.PetHandTransform;
            entityName = pet.PetName.Value;
        }
        else return;

        if (deckIds.Count == 0)
        {
            // Reshuffle discard into deck if deck is empty
            if (discardIds.Count == 0)
            {
                Debug.Log($"{entityName} has no cards in deck or discard to draw.");
                return; // No cards left anywhere
            }
            ReshuffleDiscardIntoDeck(); 
            if (deckIds.Count == 0) { // Still no cards (discard was empty too)
                 Debug.Log($"{entityName} deck is still empty after attempting reshuffle.");
                 return;
            }
        }

        int cardIdToDraw = deckIds[0]; // Take from the top (assuming shuffled)
        deckIds.RemoveAt(0);
        handIds.Add(cardIdToDraw);

        Debug.Log($"{entityName} drew card ID: {cardIdToDraw}. Hand size: {handIds.Count}");

        // Physical card movement is primarily a client-side visual concern based on SyncList changes.
        // The server updates the SyncLists, and clients react to these changes in CombatCanvasManager.RenderHand().
        // If specific parenting is required on the server for some logic, it could be done here,
        // but card GameObjects are often not directly manipulated by the server unless they are NetworkObjects themselves.
        // The prompt mentioned: "spawned objects should physically move in the scene hierarchy... between deck, hand, and discard parent gameobjects."
        // This implies clients will handle the visual representation and parenting based on the SyncList data.
        // If actual NetworkObject cards were being moved, server would change their parent and FishNet would sync that.
    }

    // Server-side logic for discarding entire hand
    public void DiscardHand()
    {
        if (parentEntity == null || !parentEntity.IsServerStarted) return;

        SyncList<int> handIds = null;
        SyncList<int> discardIds = null;
        string entityName = "Unknown Entity";

        NetworkPlayer player = parentEntity as NetworkPlayer;
        NetworkPet pet = parentEntity as NetworkPet;

        if (player != null)
        {
            handIds = player.playerHandCardIds;
            discardIds = player.discardPileCardIds;
            entityName = player.PlayerName.Value;
        }
        else if (pet != null)
        {
            handIds = pet.playerHandCardIds;
            discardIds = pet.discardPileCardIds;
            entityName = pet.PetName.Value;
        }
        else return;

        if (handIds.Count == 0)
        {
            Debug.Log($"{entityName} has no cards in hand to discard.");
            return;
        }

        // Make a copy because we'll be modifying the collection
        List<int> cardsToDiscard = new List<int>(handIds);
        foreach (int cardId in cardsToDiscard)
        {
            handIds.Remove(cardId);
            discardIds.Add(cardId);
        }

        Debug.Log($"{entityName} discarded {cardsToDiscard.Count} cards.");
    }

    // Server-side logic for moving a specific card from hand to discard
    public void MoveCardToDiscard(int cardId)
    {
        if (parentEntity == null || !parentEntity.IsServerStarted) return;

        SyncList<int> handIds = null;
        SyncList<int> discardIds = null;
        string entityName = "Unknown Entity";

        NetworkPlayer player = parentEntity as NetworkPlayer;
        NetworkPet pet = parentEntity as NetworkPet;

        if (player != null)
        {
            handIds = player.playerHandCardIds;
            discardIds = player.discardPileCardIds;
            entityName = player.PlayerName.Value;
        }
        else if (pet != null)
        {
            handIds = pet.playerHandCardIds;
            discardIds = pet.discardPileCardIds;
            entityName = pet.PetName.Value;
        }
        else return;

        if (handIds.Contains(cardId))
        {
            handIds.Remove(cardId);
            discardIds.Add(cardId);
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
        if (parentEntity == null || !parentEntity.IsServerStarted) return;

        SyncList<int> deckIds = null;
        string entityName = "Unknown Entity";

        NetworkPlayer player = parentEntity as NetworkPlayer;
        NetworkPet pet = parentEntity as NetworkPet;

        if (player != null)
        {
            deckIds = player.currentDeckCardIds;
            entityName = player.PlayerName.Value;
        }
        else if (pet != null)
        {
            deckIds = pet.currentDeckCardIds;
            entityName = pet.PetName.Value;
        }
        else return;

        if (deckIds.Count <= 1) return; // No need to shuffle 0 or 1 card

        // Fisher-Yates shuffle
        List<int> tempDeck = new List<int>(deckIds);
        for (int i = tempDeck.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            int temp = tempDeck[i];
            tempDeck[i] = tempDeck[j];
            tempDeck[j] = temp;
        }

        // Clear and repopulate the SyncList
        deckIds.Clear();
        foreach (int cardId in tempDeck)
        {
            deckIds.Add(cardId);
        }

        Debug.Log($"{entityName}'s deck shuffled. Deck size: {deckIds.Count}");
    }

    // Server-side logic to move cards from discard to deck and shuffle
    private void ReshuffleDiscardIntoDeck()
    {
        if (parentEntity == null || !parentEntity.IsServerStarted) return;

        SyncList<int> deckIds = null;
        SyncList<int> discardIds = null;
        string entityName = "Unknown Entity";

        NetworkPlayer player = parentEntity as NetworkPlayer;
        NetworkPet pet = parentEntity as NetworkPet;

        if (player != null)
        {
            deckIds = player.currentDeckCardIds;
            discardIds = player.discardPileCardIds;
            entityName = player.PlayerName.Value;
        }
        else if (pet != null)
        {
            deckIds = pet.currentDeckCardIds;
            discardIds = pet.discardPileCardIds;
            entityName = pet.PetName.Value;
        }
        else return;

        if (discardIds.Count == 0)
        {
            Debug.Log($"{entityName} has no cards in discard to reshuffle.");
            return;
        }

        // Move all cards from discard to deck
        foreach (int cardId in discardIds.ToList())
        {
            deckIds.Add(cardId);
        }
        discardIds.Clear();

        // Shuffle the deck
        ShuffleDeck();

        Debug.Log($"{entityName}'s discard reshuffled into deck. New deck size: {deckIds.Count}");
    }

    // Card GameObjects parenting/movement in the scene hierarchy:
    // This is typically handled on the client-side by CombatCanvasManager.RenderHand() in response to SyncList changes.
    // The server dictates the *state* (which card IDs are in which list).
    // Clients then update their visuals. For example, when a card ID appears in `playerHandCardIds`,
    // the client instantiates a visual representation of that card and parents it to `playerHandDisplayArea`.
    // When an ID moves from `playerHandCardIds` to `discardPileCardIds`, the client moves the corresponding GameObject.
} 