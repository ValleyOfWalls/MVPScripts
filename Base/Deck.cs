using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Combat
{
    [CreateAssetMenu(fileName = "NewDeck", menuName = "Cards/Deck")]
    public class Deck : ScriptableObject
    {
        [SerializeField] private List<CardData> cards = new List<CardData>();
        [SerializeField] private string deckName;
        [SerializeField] private DeckType deckType;

        // Properties
        public string DeckName => deckName;
        public DeckType Type => deckType;
        public int CardCount => cards.Count;
        public List<CardData> Cards => cards;

        // Duplicate the deck data to create a runtime instance
        public RuntimeDeck CreateRuntimeDeck()
        {
            RuntimeDeck runtimeDeck = new RuntimeDeck(deckName, deckType);
            foreach (CardData card in cards)
            {
                runtimeDeck.AddCard(card);
            }
            return runtimeDeck;
        }
    }

    public enum DeckType
    {
        PlayerDeck,
        PetDeck
    }

    // Runtime deck instance that can be shuffled, drawn from, etc.
    [System.Serializable]
    public class RuntimeDeck
    {
        private List<CardData> drawPile = new List<CardData>();
        private List<CardData> discardPile = new List<CardData>();
        private string deckName;
        private DeckType deckType;

        public RuntimeDeck(string name, DeckType type)
        {
            deckName = name;
            deckType = type;
        }

        // Properties
        public string DeckName => deckName;
        public DeckType Type => deckType;
        public int DrawPileCount => drawPile.Count;
        public int DiscardPileCount => discardPile.Count;
        public int TotalCards => drawPile.Count + discardPile.Count;

        // Add a card to the draw pile
        public void AddCard(CardData card)
        {
            drawPile.Add(card);
        }

        // Shuffle the draw pile
        public void Shuffle()
        {
            // Fisher-Yates shuffle
            for (int i = drawPile.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                CardData temp = drawPile[i];
                drawPile[i] = drawPile[j];
                drawPile[j] = temp;
            }
        }

        // Draw a card from the draw pile
        public CardData DrawCard()
        {
            // If draw pile is empty, shuffle discard pile into draw pile
            if (drawPile.Count == 0)
            {
                if (discardPile.Count == 0)
                {
                    return null; // No cards left
                }
                
                // Move all cards from discard to draw pile
                drawPile.AddRange(discardPile);
                discardPile.Clear();
                
                // Shuffle the draw pile
                Shuffle();
            }
            
            // Draw the top card
            CardData drawnCard = drawPile[0];
            drawPile.RemoveAt(0);
            
            return drawnCard;
        }

        // Discard a card
        public void DiscardCard(CardData card)
        {
            discardPile.Add(card);
        }

        // Discard all cards in hand
        public void DiscardHand(List<CardData> hand)
        {
            discardPile.AddRange(hand);
            hand.Clear();
        }
    }
} 