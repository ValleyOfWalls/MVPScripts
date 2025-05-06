using UnityEngine;
using System.Collections.Generic;

// This script can be attached to a Prefab that represents a pre-defined deck.
// NetworkPlayer and NetworkPet can then have a reference to one of these Deck prefabs
// to define their starter cards.
public class Deck : MonoBehaviour
{
    [SerializeField]
    private string deckName = "New Deck";

    [SerializeField]
    private List<Card> cardsInDeck = new List<Card>(); // Assign Card prefabs here in the Inspector

    public string DeckName => deckName;
    public List<Card> CardsInDeck => new List<Card>(cardsInDeck); // Return a copy to prevent external modification

    // Helper method to get card definitions
    public List<Card> GetCardDefinitions()
    {
        return new List<Card>(cardsInDeck);
    }

    // You might add methods here for managing the deck definition if needed,
    // e.g., AddCard, RemoveCard, though it's often easier to do this via the Inspector
    // if these are primarily prefab assets.
} 