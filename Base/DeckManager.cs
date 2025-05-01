using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

namespace Combat
{
    [CreateAssetMenu(fileName = "DeckManager", menuName = "Cards/Deck Manager")]
    public class DeckManager : ScriptableObject
    {
        [Header("Starter Decks")]
        [SerializeField] private Deck playerStarterDeck;
        [SerializeField] private List<Deck> petStarterDecks = new List<Deck>();
        
        [Header("Card Prefab")]
        [SerializeField] private GameObject cardPrefab;
        
        // Singleton instance
        private static DeckManager _instance;
        public static DeckManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<DeckManager>("DeckManager");
                    if (_instance == null)
                    {
                        Debug.LogError("DeckManager not found in Resources folder!");
                    }
                }
                return _instance;
            }
        }
        
        // Get the starter deck template (the actual ScriptableObject, not a runtime instance)
        public Deck GetStarterDeckTemplate()
        {
            if (playerStarterDeck == null)
            {
                Debug.LogError("Player starter deck not assigned in DeckManager!");
            }
            return playerStarterDeck;
        }
        
        // Get a pet starter deck template by index
        public Deck GetPetStarterDeckTemplate(int petIndex)
        {
            if (petStarterDecks.Count == 0)
            {
                Debug.LogError("No pet starter decks assigned in DeckManager!");
                return null;
            }
            
            // Use modulo to ensure valid index
            int safeIndex = petIndex % petStarterDecks.Count;
            
            if (petStarterDecks[safeIndex] == null)
            {
                Debug.LogError($"Pet starter deck at index {safeIndex} is null!");
                return null;
            }
            
            return petStarterDecks[safeIndex];
        }
        
        // Find a CardData by its name/ID
        public CardData FindCardByName(string cardName)
        {
            // First check the player starter deck
            if (playerStarterDeck != null)
            {
                foreach (CardData card in playerStarterDeck.Cards)
                {
                    if (card != null && card.cardName == cardName)
                    {
                        return card;
                    }
                }
            }
            
            // If not found, check all pet starter decks
            foreach (Deck petDeck in petStarterDecks)
            {
                if (petDeck != null)
                {
                    foreach (CardData card in petDeck.Cards)
                    {
                        if (card != null && card.cardName == cardName)
                        {
                            return card;
                        }
                    }
                }
            }
            
            // Card not found
            Debug.LogWarning($"Could not find CardData with name: {cardName}");
            return null;
        }
        
        // Get a starter deck for a player
        public RuntimeDeck GetPlayerStarterDeck()
        {
            if (playerStarterDeck == null)
            {
                Debug.LogError("Player starter deck not assigned!");
                return new RuntimeDeck("Empty Deck", DeckType.PlayerDeck);
            }
            
            RuntimeDeck deck = playerStarterDeck.CreateRuntimeDeck();
            deck.Shuffle();
            return deck;
        }
        
        // Get a starter deck for a pet by index
        public RuntimeDeck GetPetStarterDeck(int petIndex)
        {
            if (petStarterDecks.Count == 0)
            {
                Debug.LogError("No pet starter decks assigned!");
                return new RuntimeDeck("Empty Deck", DeckType.PetDeck);
            }
            
            // Use modulo to ensure valid index
            int safeIndex = petIndex % petStarterDecks.Count;
            
            RuntimeDeck deck = petStarterDecks[safeIndex].CreateRuntimeDeck();
            deck.Shuffle();
            return deck;
        }
        
        // Get a random pet starter deck
        public RuntimeDeck GetRandomPetStarterDeck()
        {
            if (petStarterDecks.Count == 0)
            {
                Debug.LogError("No pet starter decks assigned!");
                return new RuntimeDeck("Empty Deck", DeckType.PetDeck);
            }
            
            int randomIndex = Random.Range(0, petStarterDecks.Count);
            RuntimeDeck deck = petStarterDecks[randomIndex].CreateRuntimeDeck();
            deck.Shuffle();
            return deck;
        }
        
        // Create a card GameObject from card data
        public GameObject CreateCardObject(CardData cardData, Transform parent, NetworkBehaviour hand, ICombatant owner)
        {
            if (cardPrefab == null)
            {
                Debug.LogError("Card prefab not assigned!");
                return null;
            }
            if (cardData == null)
            {
                 Debug.LogError("CreateCardObject called with null CardData!");
                 return null;
            }
            
            GameObject cardObj = Instantiate(cardPrefab, parent);
            Card card = cardObj.GetComponent<Card>();
            
            if (card != null)
            {
                // Initialize the card first (this sets the internal data and UI text)
                if (hand is PlayerHand playerHand)
                {
                    card.Initialize(cardData, playerHand, owner);
                }
                else if (hand is PetHand petHand)
                {
                    card.Initialize(cardData, petHand, owner);
                }
                else
                {
                    Debug.LogError("CreateCardObject called with unknown hand type!");
                    // Optionally initialize with null hand if appropriate
                    // card.Initialize(cardData, null, owner);
                }

                // RENAME the GameObject based on the card's name (which should be set in UI by Initialize)
                if (!string.IsNullOrEmpty(card.CardName) && card.CardName != "NO_DATA")
                {
                     cardObj.name = card.CardName;
                }
                else
                {
                    // Fallback name if CardName isn't set correctly
                    cardObj.name = $"Card_{cardData.cardName}_UninitializedName";
                    Debug.LogWarning($"Could not get valid CardName from Card component after Initialize. Using fallback name: {cardObj.name}");
                }
            }
            else
            {
                Debug.LogError("Card component not found on card prefab!");
            }
            
            return cardObj;
        }
    }
} 