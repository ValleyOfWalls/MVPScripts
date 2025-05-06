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
                Debug.Log($"[DIAGNOSTIC] DrawCard: Draw pile is empty for deck {deckName}. Discard pile has {discardPile.Count} cards.");
                
                if (discardPile.Count == 0)
                {
                    Debug.LogWarning($"[DIAGNOSTIC] No cards left in either draw or discard pile for deck {deckName}!");
                    return null; // No cards left
                }
                
                // Use the Reshuffle method instead of duplicating logic
                Reshuffle();
                
                // Double-check that reshuffling actually worked
                if (drawPile.Count == 0)
                {
                    Debug.LogError($"[DIAGNOSTIC] Reshuffle failed! Draw pile is still empty for deck {deckName}");
                    return null;
                }
            }
            
            // Draw the top card
            CardData drawnCard = drawPile[0];
            drawPile.RemoveAt(0);
            
            Debug.Log($"[DIAGNOSTIC] Drew card {drawnCard.cardName} from deck {deckName}. Remaining: {drawPile.Count} in draw pile, {discardPile.Count} in discard.");
            
            return drawnCard;
        }

        // Discard a card
        public void DiscardCard(CardData card)
        {
            if (card == null)
            {
                Debug.LogWarning($"[DIAGNOSTIC] Attempted to discard null card to deck {deckName}");
                return;
            }
            
            discardPile.Add(card);
            Debug.Log($"[DIAGNOSTIC] Discarded card {card.cardName} to {deckName}. Discard pile now has {discardPile.Count} cards.");
        }

        // Discard all cards in hand
        public void DiscardHand(List<CardData> hand)
        {
            if (hand == null || hand.Count == 0)
            {
                Debug.Log($"[DIAGNOSTIC] No cards to discard from hand to deck {deckName}");
                return;
            }
            
            int validCards = 0;
            foreach (CardData card in hand)
            {
                if (card != null)
                {
                    discardPile.Add(card);
                    validCards++;
                }
            }
            
            Debug.Log($"[DIAGNOSTIC] Discarded {validCards} cards from hand to deck {deckName}. Discard pile now has {discardPile.Count} cards.");
            hand.Clear();
        }

        // Check if discard pile should be reshuffled into draw pile
        public bool NeedsReshuffle()
        {
            bool needs = drawPile.Count == 0 && discardPile.Count > 0;
            if (needs)
            {
                Debug.Log($"[DIAGNOSTIC] Deck {deckName} needs reshuffling. Draw pile: {drawPile.Count}, Discard pile: {discardPile.Count}");
            }
            return needs;
        }

        // Reshuffle the discard pile into the draw pile
        public void Reshuffle()
        {
            Debug.Log($"[DIAGNOSTIC] Attempting to reshuffle deck {deckName}. Draw pile: {drawPile.Count}, Discard pile: {discardPile.Count}");
            
            if (discardPile.Count == 0)
            {
                Debug.LogWarning($"[DIAGNOSTIC] Cannot reshuffle: Discard pile is empty for deck {deckName}");
                return;
            }

            // Move all cards from discard to draw pile
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            
            // Shuffle the draw pile
            Shuffle();
            
            Debug.Log($"[DIAGNOSTIC] Reshuffled {drawPile.Count} cards from discard pile into draw pile for deck {deckName}");
        }
    }
} 