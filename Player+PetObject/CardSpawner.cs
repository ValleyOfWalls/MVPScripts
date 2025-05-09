using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

/// <summary>
/// Handles spawning and despawning of card GameObjects based on changes to CombatHand.
/// Attach to: Both NetworkPlayer and NetworkPet prefabs alongside CombatHand.
/// </summary>
public class CardSpawner : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] public GameObject cardPrefab;
    
    // Internal references
    private CombatHand combatHand;
    private Dictionary<int, GameObject> spawnedCards = new Dictionary<int, GameObject>();
    
    // UI references
    private NetworkPlayerUI playerUI;
    private NetworkPetUI petUI;
    private Transform handTransform;
    
    // Combat references
    private CombatManager combatManager;
    private NetworkPlayer localPlayer;
    private NetworkPet localPet;
    
    private void Awake()
    {
        // Get the CombatHand component
        combatHand = GetComponent<CombatHand>();
        
        if (combatHand == null)
        {
            Debug.LogError("CardSpawner requires a CombatHand component on the same GameObject");
        }
        
        // Check if this is attached to a player or pet
        localPlayer = GetComponent<NetworkPlayer>();
        localPet = GetComponent<NetworkPet>();
        
        if (localPlayer != null)
        {
            playerUI = GetComponent<NetworkPlayerUI>();
            if (playerUI != null)
            {
                handTransform = playerUI.GetPlayerHandTransform();
            }
        }
        else if (localPet != null)
        {
            petUI = GetComponent<NetworkPetUI>();
            if (petUI != null)
            {
                handTransform = petUI.GetPetHandTransform();
            }
        }
        
        if (handTransform == null)
        {
            Debug.LogError("CardSpawner couldn't find a hand transform reference");
        }
    }
    
    private void Start()
    {
        // Find combat manager
        combatManager = FindFirstObjectByType<CombatManager>();
        if (combatManager == null)
        {
            Debug.LogWarning("CardSpawner couldn't find CombatManager. Card playing functionality may be limited.");
        }
        
        // Check if card prefab is assigned
        if (cardPrefab == null)
        {
            Debug.LogError("CardSpawner: Card prefab is not assigned! Cards cannot be displayed.");
        }
        
        // Initial display update if we're running on the client
        if (IsClientInitialized && handTransform != null)
        {
            UpdateCardDisplay();
        }
    }
    
    private void OnEnable()
    {
        if (combatHand != null)
        {
            combatHand.OnHandChanged += HandleHandChanged;
            
            // Ensure we update on enable for any existing cards
            if (IsClientInitialized && handTransform != null)
            {
                UpdateCardDisplay();
            }
        }
    }
    
    private void OnDisable()
    {
        if (combatHand != null)
        {
            combatHand.OnHandChanged -= HandleHandChanged;
        }
        
        // Clean up any spawned cards when disabled
        ClearAllCards();
    }
    
    /// <summary>
    /// Called when the hand changes (cards added or removed)
    /// </summary>
    private void HandleHandChanged()
    {
        if (!IsClientInitialized) return; // Only clients handle visual representation
        
        UpdateCardDisplay();
    }
    
    /// <summary>
    /// Updates the visual representation of cards in hand
    /// </summary>
    private void UpdateCardDisplay()
    {
        if (combatHand == null || handTransform == null) return;
        
        List<int> currentHandCards = combatHand.GetAllCards();
        
        // First, identify cards that are no longer in hand and need to be removed
        List<int> cardsToRemove = new List<int>();
        foreach (var cardId in spawnedCards.Keys)
        {
            if (!currentHandCards.Contains(cardId))
            {
                cardsToRemove.Add(cardId);
            }
        }
        
        // Remove cards that are no longer in hand
        foreach (var cardId in cardsToRemove)
        {
            RemoveCardFromDisplay(cardId);
        }
        
        // Add new cards that weren't previously displayed
        foreach (var cardId in currentHandCards)
        {
            if (!spawnedCards.ContainsKey(cardId))
            {
                AddCardToDisplay(cardId);
            }
        }
        
        // Arrange cards in hand
        ArrangeCardsInHand();
    }
    
    /// <summary>
    /// Adds a card to the visual display
    /// </summary>
    private void AddCardToDisplay(int cardId)
    {
        if (cardPrefab == null)
        {
            Debug.LogError("CardSpawner: Card prefab is not assigned");
            return;
        }
        
        // Get card data
        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null)
        {
            Debug.LogWarning($"CardSpawner: No card data found for ID {cardId}");
            return;
        }
        
        // Instantiate the card
        GameObject cardObject = Instantiate(cardPrefab, handTransform);
        Card cardComponent = cardObject.GetComponent<Card>();
        
        if (cardComponent != null)
        {
            // Initialize the card
            cardComponent.Initialize(cardData);
            
            // Store reference to the card
            spawnedCards[cardId] = cardObject;
            
            // If this is a player card (not AI pet), add click event
            if (localPlayer != null && localPlayer.IsOwner)
            {
                // Add or get Button component for click handling
                Button cardButton = cardObject.GetComponent<Button>();
                if (cardButton == null) cardButton = cardObject.AddComponent<Button>();
                
                // Add click event
                cardButton.onClick.AddListener(() => OnCardClicked(cardId, cardData));
            }
            
            // Log a message to verify card was added
            Debug.Log($"Added card {cardData.CardName} (ID: {cardId}) to display for {(localPlayer != null ? localPlayer.PlayerName.Value : localPet.PetName.Value)}");
        }
        else
        {
            Debug.LogError("CardSpawner: Card prefab does not have a Card component");
            Destroy(cardObject);
        }
    }
    
    /// <summary>
    /// Handle card click event
    /// </summary>
    private void OnCardClicked(int cardId, CardData cardData)
    {
        if (combatManager == null)
        {
            Debug.LogError("CardSpawner: Cannot play card - CombatManager not found");
            return;
        }
        
        if (localPlayer == null || !localPlayer.IsOwner)
        {
            Debug.LogWarning("CardSpawner: Cannot play card - Not the local player or not owner");
            return;
        }
        
        // Check if it's the player's turn before sending the command
        if (!combatManager.IsPlayerTurn(localPlayer))
        {
            Debug.LogWarning($"Cannot play card: It's not {localPlayer.PlayerName.Value}'s turn");
            return;
        }
        
        Debug.Log($"Player clicked on card '{cardData.CardName}' (ID: {cardId}). Requesting to play.");
        combatManager.CmdPlayerRequestsPlayCard(cardId);
    }
    
    /// <summary>
    /// Removes a card from the visual display
    /// </summary>
    private void RemoveCardFromDisplay(int cardId)
    {
        if (spawnedCards.TryGetValue(cardId, out GameObject cardObject))
        {
            // Remove click event listener if this is a player card
            Button cardButton = cardObject.GetComponent<Button>();
            if (cardButton != null)
            {
                cardButton.onClick.RemoveAllListeners();
            }
            
            // Remove from dictionary
            spawnedCards.Remove(cardId);
            
            // Destroy the card object
            Destroy(cardObject);
            
            Debug.Log($"Removed card ID {cardId} from display");
        }
    }
    
    /// <summary>
    /// Removes all cards from the display
    /// </summary>
    private void ClearAllCards()
    {
        foreach (var cardObject in spawnedCards.Values)
        {
            if (cardObject != null)
            {
                Destroy(cardObject);
            }
        }
        
        spawnedCards.Clear();
    }
    
    /// <summary>
    /// Arranges cards in a visually appealing layout
    /// </summary>
    private void ArrangeCardsInHand()
    {
        if (spawnedCards.Count == 0) return;
        
        int cardCount = spawnedCards.Count;
        float spacing = 100f; // Horizontal spacing between cards
        float arcHeight = 50f; // How much the cards arc upward in the middle
        float startX = -(spacing * (cardCount - 1)) / 2f; // Center the cards
        
        int index = 0;
        foreach (var cardObj in spawnedCards.Values)
        {
            if (cardObj != null)
            {
                // Calculate position in an arc
                float xPos = startX + (index * spacing);
                float yPos = -(Mathf.Pow(xPos / (spacing * 2), 2)) * arcHeight; // Parabolic arc
                
                // Set local position
                RectTransform rectTransform = cardObj.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.localPosition = new Vector3(xPos, yPos, 0);
                    
                    // Calculate rotation (cards fan out)
                    float rotationAngle = Mathf.Lerp(-10f, 10f, (float)index / (cardCount - 1));
                    rectTransform.localRotation = Quaternion.Euler(0, 0, rotationAngle);
                    
                    // Set z-order for proper layering
                    rectTransform.SetSiblingIndex(index);
                }
                
                index++;
            }
        }
    }
    
    /// <summary>
    /// Called when a card is played to remove it from display
    /// </summary>
    public void OnCardPlayed(int cardId)
    {
        RemoveCardFromDisplay(cardId);
        ArrangeCardsInHand();
    }
} 