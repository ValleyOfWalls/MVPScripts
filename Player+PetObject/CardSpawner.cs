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
    [SerializeField] private Transform cardPrefabParent;
    
    // Internal references
    private CombatHand combatHand;
    private Dictionary<int, List<GameObject>> spawnedCardsByType = new Dictionary<int, List<GameObject>>();
    private Dictionary<string, GameObject> spawnedCardInstances = new Dictionary<string, GameObject>();
    private Dictionary<int, int> cardInstanceCounter = new Dictionary<int, int>();
    
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
        
        if (localPlayer == null && localPet == null)
        {
            Debug.LogError("CardSpawner must be attached to either a NetworkPlayer or NetworkPet");
        }
        
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
            Debug.LogError("CardSpawner: Hand transform reference is null");
        }
    }
    
    private void Start()
    {
        // Find combat manager
        combatManager = FindFirstObjectByType<CombatManager>();
        if (combatManager == null)
        {
            Debug.LogWarning("CardSpawner: CombatManager not found in scene");
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
        
        // Create a dictionary to track which cards we've already counted in this update
        Dictionary<int, int> cardCounts = new Dictionary<int, int>();
        
        // Count how many of each card ID we have in the hand
        foreach (int cardId in currentHandCards)
        {
            if (cardCounts.ContainsKey(cardId))
            {
                cardCounts[cardId]++;
            }
            else
            {
                cardCounts[cardId] = 1;
            }
        }
        
        // Remove cards that are no longer in hand
        List<string> instancesToRemove = new List<string>();
        foreach (var instanceEntry in spawnedCardInstances)
        {
            string instanceId = instanceEntry.Key;
            int cardId = ExtractCardIdFromInstanceId(instanceId);
            
            // Check if this card ID is still in the hand in sufficient quantities
            if (!cardCounts.TryGetValue(cardId, out int count) || count <= 0)
            {
                // This card or this instance of the card is no longer needed
                RemoveCardInstance(instanceId);
                instancesToRemove.Add(instanceId);
            }
            else
            {
                // Decrease the count for this card ID since we've accounted for one instance
                cardCounts[cardId]--;
            }
        }
        
        // Actually remove the instances from our tracking dictionary
        foreach (string instanceId in instancesToRemove)
        {
            spawnedCardInstances.Remove(instanceId);
        }
        
        // Add new cards for any remaining counts
        foreach (var cardCount in cardCounts)
        {
            int cardId = cardCount.Key;
            int count = cardCount.Value;
            
            // We need to spawn 'count' more instances of this card ID
            for (int i = 0; i < count; i++)
            {
                AddCardToDisplay(cardId);
            }
        }
        
        // No need to arrange cards - we're using a horizontal layout group instead
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
        
        // Generate a unique instance ID for this card
        if (!cardInstanceCounter.ContainsKey(cardId))
        {
            cardInstanceCounter[cardId] = 0;
        }
        int instanceNum = cardInstanceCounter[cardId]++;
        string instanceId = $"{cardId}_{instanceNum}";
        
        // Instantiate the card
        GameObject cardObject = Instantiate(cardPrefab, handTransform);
        
        // Ensure the card is active when spawned
        cardObject.SetActive(true);

        // Make sure all child objects are also active
        foreach (Transform child in cardObject.transform)
        {
            child.gameObject.SetActive(true);
        }

        Debug.Log($"Card {cardData.CardName} (ID: {cardId}) instantiated with active state: {cardObject.activeSelf}");
        
        Card cardComponent = cardObject.GetComponent<Card>();
        
        if (cardComponent != null)
        {
            // Initialize the card
            cardComponent.Initialize(cardData);
            
            // Store reference to the card in both tracking structures
            if (!spawnedCardsByType.ContainsKey(cardId))
            {
                spawnedCardsByType[cardId] = new List<GameObject>();
            }
            spawnedCardsByType[cardId].Add(cardObject);
            spawnedCardInstances[instanceId] = cardObject;
            
            // Store the instance ID on the card object for later retrieval
            cardObject.name = $"Card_{cardData.CardName}_{instanceId}";
            
            // Get the HandleCardPlay component and set its parent entity
            HandleCardPlay handleCardPlay = cardObject.GetComponent<HandleCardPlay>();
            if (handleCardPlay != null)
            {
                if (localPlayer != null)
                {
                    handleCardPlay.SetOwnerEntity(localPlayer.gameObject);
                }
                else if (localPet != null)
                {
                    handleCardPlay.SetOwnerEntity(localPet.gameObject);
                }
                else
                {
                    Debug.LogError("CardSpawner: Cannot set owner entity for HandleCardPlay - no NetworkPlayer or NetworkPet found.");
                }
            }
            
            // If this is a player card (not AI pet), add click event
            if (localPlayer != null && (localPlayer.IsOwner || FishNet.InstanceFinder.IsHostStarted || IsClientOnlyInitialized))
            {
                // Log for debugging who can click
                Debug.Log($"Adding click handler to card {cardData.CardName} for {localPlayer.PlayerName.Value}. " +
                          $"IsOwner: {localPlayer.IsOwner}, IsHost: {FishNet.InstanceFinder.IsHostStarted}, IsClientOnly: {IsClientOnlyInitialized}");
                
                // Add or get Button component for click handling
                Button cardButton = cardObject.GetComponent<Button>();
                if (cardButton == null) cardButton = cardObject.AddComponent<Button>();
                
                // Add click event - pass the instance ID to identify exactly which card was clicked
                cardButton.onClick.AddListener(() => OnCardClicked(cardId, instanceId, cardData));
            }
            
            // Log a message to verify card was added
            Debug.Log($"Added card {cardData.CardName} (ID: {cardId}, Instance: {instanceId}) to display for {(localPlayer != null ? localPlayer.PlayerName.Value : localPet.PetName.Value)}");
        }
        else
        {
            Debug.LogError("CardSpawner: Card prefab does not have a Card component");
            Destroy(cardObject);
        }
    }
    
    /// <summary>
    /// Extract the card ID from an instance ID
    /// </summary>
    private int ExtractCardIdFromInstanceId(string instanceId)
    {
        string[] parts = instanceId.Split('_');
        if (parts.Length >= 1 && int.TryParse(parts[0], out int cardId))
        {
            return cardId;
        }
        return -1;
    }
    
    /// <summary>
    /// Handle card click event
    /// </summary>
    private void OnCardClicked(int cardId, string instanceId, CardData cardData)
    {
        if (combatManager == null)
        {
            Debug.LogError("CardSpawner: Cannot play card - CombatManager not found");
            return;
        }
        
        // Log detailed information about the card and the entities involved
        Debug.Log($"DETAILED CARD INFO - Card: {cardData.CardName} (ID: {cardId}, Instance: {instanceId}) " +
                 $"owned by Player: {(localPlayer != null ? localPlayer.PlayerName.Value : "none")} (ID: {(localPlayer != null ? localPlayer.ObjectId : -1)}), " +
                 $"Combat Manager ID: {combatManager.ObjectId}, " +
                 $"Local Pet: {(localPet != null ? localPet.PetName.Value : "none")} (ID: {(localPet != null ? localPet.ObjectId : -1)})");
        
        // Special handling for host - allow interactions even if IsOwner is false
        bool canPlayCard = false;
        if (localPlayer != null)
        {
            // Simplified check - if we're on the client side, we should allow card play
            // for cards displayed in the player's hand
            if (localPlayer.IsOwner || IsClientOnlyInitialized)
            {
                canPlayCard = true;
                Debug.Log($"Client {(localPlayer.IsOwner ? "owner" : "non-owner")} clicked on card {cardData.CardName}. Allowing card play for {localPlayer.PlayerName.Value}");
            }
            else if (FishNet.InstanceFinder.IsHostStarted)
            {
                // If this is the host, allow them to play cards
                canPlayCard = true;
                Debug.Log($"Host click detected on card {cardData.CardName}. Allowing card play for host player: {localPlayer.PlayerName.Value}");
            }
        }
        
        if (!canPlayCard)
        {
            Debug.LogWarning($"CardSpawner: Cannot play card - Not authorized. Player: {(localPlayer != null ? localPlayer.PlayerName.Value : "null")}, " +
                           $"IsOwner: {(localPlayer != null ? localPlayer.IsOwner.ToString() : "n/a")}, " +
                           $"IsHost: {FishNet.InstanceFinder.IsHostStarted}, " +
                           $"IsClientOnly: {IsClientOnlyInitialized}");
            return;
        }

        // Debug information about the combat state
        Debug.Log($"OnCardClicked: Player {localPlayer.PlayerName.Value} (ID: {localPlayer.ObjectId}) attempting to play card {cardData.CardName} (ID: {cardId})");
        
        // Check if combat has been properly initialized
        if (!combatManager.IsCombatInitialized())
        {
            Debug.LogWarning("Cannot play card: Combat has not been properly initialized yet");
            return;
        }
        
        // Check if it's the player's turn before sending the command
        if (!combatManager.IsPlayerTurn(localPlayer))
        {
            Debug.LogWarning($"Cannot play card: It's not {localPlayer.PlayerName.Value}'s turn");
            return;
        }
        
        Debug.Log($"Player clicked on card '{cardData.CardName}' (ID: {cardId}, Instance: {instanceId}). Requesting to play.");
        
        // Store the instance ID for later removal after server confirmation
        // We'll remove it when we get the NotifyCardPlayed callback from the server
        combatManager.CmdPlayerRequestsPlayCard(localPlayer.ObjectId, cardId, instanceId);
    }
    
    /// <summary>
    /// Called when the server confirms a card has been played
    /// This would be called from a notification method after server processing
    /// </summary>
    public void OnServerConfirmCardPlayed(int cardId)
    {
        // Find and remove the first instance of this card type
        RemoveCardFromDisplay(cardId);
    }
    
    /// <summary>
    /// Called when the server confirms a card has been played
    /// This would be called from a notification method after server processing
    /// </summary>
    public void OnServerConfirmCardPlayed(int cardId, string instanceId)
    {
        // If we have an instance ID, use that to remove the specific card
        if (!string.IsNullOrEmpty(instanceId) && spawnedCardInstances.ContainsKey(instanceId))
        {
            RemoveCardInstance(instanceId);
            spawnedCardInstances.Remove(instanceId);
            Debug.Log($"Removed specific card instance {instanceId} from display");
        }
        else
        {
            // Fallback to removing by card ID if we don't have a valid instance ID
            RemoveCardFromDisplay(cardId);
        }
    }
    
    /// <summary>
    /// Removes a specific card instance from the display
    /// </summary>
    private void RemoveCardInstance(string instanceId)
    {
        if (spawnedCardInstances.TryGetValue(instanceId, out GameObject cardObject))
        {
            int cardId = ExtractCardIdFromInstanceId(instanceId);
            
            // Remove click event listener if this is a player card
            Button cardButton = cardObject.GetComponent<Button>();
            if (cardButton != null)
            {
                cardButton.onClick.RemoveAllListeners();
            }
            
            // Remove from type-based dictionary
            if (spawnedCardsByType.TryGetValue(cardId, out List<GameObject> cardObjectList))
            {
                cardObjectList.Remove(cardObject);
                if (cardObjectList.Count == 0)
                {
                    spawnedCardsByType.Remove(cardId);
                }
            }
            
            // Destroy the card object
            Destroy(cardObject);
            
            Debug.Log($"Removed card instance {instanceId} from display");
        }
    }
    
    /// <summary>
    /// Removes a card from the visual display - finds the first instance of the card type
    /// </summary>
    private void RemoveCardFromDisplay(int cardId)
    {
        if (spawnedCardsByType.TryGetValue(cardId, out List<GameObject> cardObjects) && cardObjects.Count > 0)
        {
            // Find the corresponding instance ID
            string instanceIdToRemove = null;
            foreach (var entry in spawnedCardInstances)
            {
                if (entry.Value == cardObjects[0] && ExtractCardIdFromInstanceId(entry.Key) == cardId)
                {
                    instanceIdToRemove = entry.Key;
                    break;
                }
            }
            
            if (instanceIdToRemove != null)
            {
                RemoveCardInstance(instanceIdToRemove);
                spawnedCardInstances.Remove(instanceIdToRemove);
            }
        }
    }
    
    /// <summary>
    /// Removes all cards from the display
    /// </summary>
    private void ClearAllCards()
    {
        foreach (var cardList in spawnedCardsByType.Values)
        {
            foreach (var cardObject in cardList)
            {
                if (cardObject != null)
                {
                    Destroy(cardObject);
                }
            }
        }
        
        spawnedCardsByType.Clear();
        spawnedCardInstances.Clear();
        cardInstanceCounter.Clear();
    }
    
    /// <summary>
    /// Called when a card is played to remove it from display
    /// </summary>
    public void OnCardPlayed(int cardId)
    {
        RemoveCardFromDisplay(cardId);
    }
} 