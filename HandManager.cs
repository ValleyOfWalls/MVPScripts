using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System.Linq;

// Not a MonoBehaviour. Instantiated by CombatManager.
public class HandManager
{
    private CombatManager combatManagerInstance;
    private System.Random rng = new System.Random();

    public HandManager(CombatManager manager)
    {
        combatManagerInstance = manager;
    }

    // Server-side logic for drawing initial cards
    public void DrawInitialCardsForEntity(NetworkBehaviour entity, int drawAmount)
    {
        if (!combatManagerInstance.IsServerStarted) return;

        SyncList<int> deck = null;
        SyncList<int> hand = null;
        string entityName = "Unknown Entity";

        if (entity is NetworkPlayer player)
        {
            deck = player.currentDeckCardIds;
            hand = player.playerHandCardIds;
            entityName = player.PlayerName.Value;
            ShuffleDeck(player); // Shuffle before drawing
        }
        else if (entity is NetworkPet pet)
        {
            deck = pet.currentDeckCardIds;
            hand = pet.playerHandCardIds;
            entityName = pet.PetName.Value;
            ShuffleDeck(pet); // Shuffle before drawing
        }
        else
        {
            Debug.LogError("DrawInitialCardsForEntity: Entity is not a NetworkPlayer or NetworkPet.");
            return;
        }

        Debug.Log($"HandManager: Drawing {drawAmount} cards for {entityName}.");
        for (int i = 0; i < drawAmount; i++)
        {
            DrawOneCard(entity);
        }
    }

    // Server-side logic for drawing one card
    public void DrawOneCard(NetworkBehaviour entity)
    {
        if (!combatManagerInstance.IsServerStarted) return;

        SyncList<int> deckIds = null;
        SyncList<int> handIds = null;
        SyncList<int> discardIds = null;
        Transform handTransform = null; 
        string entityName = "Unknown Entity";

        NetworkPlayer player = entity as NetworkPlayer;
        NetworkPet pet = entity as NetworkPet;

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
            ReshuffleDiscardIntoDeck(entity);
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

    [Server]
    public void DiscardHand(NetworkBehaviour entity)
    {
        if (!combatManagerInstance.IsServerStarted) return;

        SyncList<int> handIds = null;
        SyncList<int> discardIds = null;
        string entityName = "Unknown Entity";

        NetworkPlayer player = entity as NetworkPlayer;
        NetworkPet pet = entity as NetworkPet;

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

        if (handIds.Count > 0) {
            Debug.Log($"{entityName} discarding hand. Cards: {handIds.Count}");
            // Add all cards from hand to discard, then clear hand
            foreach (int cardId in handIds)
            {
                discardIds.Add(cardId);
            }
            handIds.Clear();
        }
    }

    [Server]
    public void MoveCardToDiscard(NetworkBehaviour entity, int cardId)
    {
        if (!combatManagerInstance.IsServerStarted) return;

        SyncList<int> handIds = null;
        SyncList<int> discardIds = null;
        string entityName = "Unknown Entity";

        NetworkPlayer player = entity as NetworkPlayer;
        NetworkPet pet = entity as NetworkPet;

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

    [Server]
    private void ShuffleDeck(NetworkBehaviour entity)
    {
        SyncList<int> deckIds = null;
        string entityName = "Unknown Entity";

        if (entity is NetworkPlayer player) { deckIds = player.currentDeckCardIds; entityName = player.PlayerName.Value; }
        else if (entity is NetworkPet pet) { deckIds = pet.currentDeckCardIds; entityName = pet.PetName.Value; }
        else return;

        // Basic Fisher-Yates shuffle on the SyncList (server-side)
        // SyncList doesn't have a direct Sort or random access for shuffle easily.
        // Best to copy to a list, shuffle, clear SyncList, and add back.
        if (deckIds.Count > 1)
        {
            List<int> tempList = new List<int>(deckIds);
            int n = tempList.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                int value = tempList[k];
                tempList[k] = tempList[n];
                tempList[n] = value;
            }
            deckIds.Clear();
            foreach (int cardId in tempList)
            {
                deckIds.Add(cardId);
            }
            Debug.Log($"{entityName}'s deck shuffled. Cards: {deckIds.Count}");
        }
    }

    [Server]
    private void ReshuffleDiscardIntoDeck(NetworkBehaviour entity)
    {
        SyncList<int> deckIds = null;
        SyncList<int> discardIds = null;
        string entityName = "Unknown Entity";

        NetworkPlayer player = entity as NetworkPlayer;
        NetworkPet pet = entity as NetworkPet;

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

        if (discardIds.Count > 0)
        {
            Debug.Log($"Reshuffling {discardIds.Count} cards from {entityName}'s discard pile into deck.");
            foreach (int cardId in discardIds)
            {
                deckIds.Add(cardId);
            }
            discardIds.Clear();
            ShuffleDeck(entity);
        }
    }

    // Card GameObjects parenting/movement in the scene hierarchy:
    // This is typically handled on the client-side by CombatCanvasManager.RenderHand() in response to SyncList changes.
    // The server dictates the *state* (which card IDs are in which list).
    // Clients then update their visuals. For example, when a card ID appears in `playerHandCardIds`,
    // the client instantiates a visual representation of that card and parents it to `playerHandDisplayArea`.
    // When an ID moves from `playerHandCardIds` to `discardPileCardIds`, the client moves the corresponding GameObject.
} 