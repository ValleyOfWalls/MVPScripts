using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet;

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
            Debug.Log($"[DeckManager] Searching for CardData with name: '{cardName}'");
            
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
            if (hand == null)
            {
                Debug.LogError("CreateCardObject called with null hand (parent NetworkBehaviour)!");
                return null;
            }
            if (hand.NetworkObject == null)
            {
                 Debug.LogError($"CreateCardObject called with hand '{hand.name}' that has no NetworkObject!");
                 return null;
            }
            
            // Only the server should create and spawn network objects
            if (hand.IsServer)
            {
                Debug.Log($"[Server] Creating card object for {cardData.cardName} to be parented under {hand.name} ({hand.NetworkObject.ObjectId})");
                
                // Instantiate the card - initially WITHOUT a parent to avoid potential scaling issues
                GameObject cardObj = Instantiate(cardPrefab);
                
                // Get the Card component
                Card card = cardObj.GetComponent<Card>();
                
                if (card != null)
                {
                    // Initialize the card's data on the Server
                    if (hand is PlayerHand playerHand)
                    {
                        card.ServerInitialize(cardData, owner, playerHand, null);
                    }
                    else if (hand is PetHand petHand)
                    {
                        card.ServerInitialize(cardData, owner, null, petHand);
                    }
                    else
                    {
                        Debug.LogError($"CreateCardObject called with unknown hand type: {hand.GetType().Name}");
                         Destroy(cardObj);
                         return null;
                    }
                    
                    // Set the GameObject name via SyncVar AFTER getting component
                    card.SetCardObjectName(cardData.cardName);
                    
                    // Spawn the networked card - Owner should be the hand's owner
                    InstanceFinder.ServerManager.Spawn(cardObj, hand.Owner);
                    
                    // Set parent AFTER spawning using the RPC
                    card.RpcSetParent(hand.NetworkObject);

                    Debug.Log($"[Server] Card {cardData.cardName} spawned (ID: {card.NetworkObject.ObjectId}), name set, and RpcSetParent called for {hand.name} ({hand.NetworkObject.ObjectId}).");
                    
                    return cardObj;
                }
                else
                {
                    Debug.LogError("Card component not found on card prefab!");
                    Destroy(cardObj);
                    return null;
                }
            }
            else
            {
                Debug.LogWarning("CreateCardObject called from client. Only the server should create and spawn networked cards.");
                return null;
            }
        }
    }
} 