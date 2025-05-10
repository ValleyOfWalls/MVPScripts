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
        
        // Count how many of each card ID we *should* have based on the CombatHand
        Dictionary<int, int> desiredCardCounts = new Dictionary<int, int>();
        foreach (int cardId in currentHandCards)
        {
            if (desiredCardCounts.ContainsKey(cardId))
            {
                desiredCardCounts[cardId]++;
            }
            else
            {
                desiredCardCounts[cardId] = 1;
            }
        }
        
        // Count how many of each card ID are *currently spawned*
        Dictionary<int, int> currentlySpawnedCounts = new Dictionary<int, int>();
        foreach (var instanceEntry in spawnedCardInstances)
        {
            int cardId = ExtractCardIdFromInstanceId(instanceEntry.Key);
            if (currentlySpawnedCounts.ContainsKey(cardId))
            {
                currentlySpawnedCounts[cardId]++;
            }
            else
            {
                currentlySpawnedCounts[cardId] = 1;
            }
        }

        // Add new cards if the desired count is greater than currently spawned count
        foreach (var desiredEntry in desiredCardCounts)
        {
            int cardId = desiredEntry.Key;
            int numDesired = desiredEntry.Value;
            int numCurrentlySpawned = 0;
            currentlySpawnedCounts.TryGetValue(cardId, out numCurrentlySpawned);

            if (numDesired > numCurrentlySpawned)
            {
                for (int i = 0; i < (numDesired - numCurrentlySpawned); i++)
                {
                    AddCardToDisplay(cardId);
                }
            }
        }
        
        // NO REMOVAL LOGIC HERE. Removals are handled by OnServerConfirmCardPlayed via RPC.
        // The old logic that iterated spawnedCardInstances and removed them if their
        // count in a temporary 'cardCounts' dictionary went to zero is removed.
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
        // THIS IS THE PROBLEMATIC METHOD - it doesn't guarantee removing the right instance
        // Instead, we should generate an instance ID and use it in this case too
        string firstInstanceId = FindFirstInstanceOfCard(cardId);
        
        if (!string.IsNullOrEmpty(firstInstanceId))
        {
            Debug.Log($"OnServerConfirmCardPlayed: Using specific instance ID {firstInstanceId} instead of generic removal");
            OnServerConfirmCardPlayed(cardId, firstInstanceId);
        }
        else
        {
            Debug.LogWarning($"OnServerConfirmCardPlayed: Could not find any instance of card ID {cardId} to remove");
            RemoveCardFromDisplay(cardId); // Fallback to the old method
        }
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
            string firstInstanceId = FindFirstInstanceOfCard(cardId);
            
            if (!string.IsNullOrEmpty(firstInstanceId))
            {
                Debug.Log($"OnServerConfirmCardPlayed: Fallback - using first found instance ID {firstInstanceId} instead of generic removal");
                RemoveCardInstance(firstInstanceId);
                spawnedCardInstances.Remove(firstInstanceId);
            }
            else
            {
                Debug.LogWarning($"OnServerConfirmCardPlayed: No valid instance found for card ID {cardId}, falling back to legacy method");
                RemoveCardFromDisplay(cardId);
            }
        }
    }
    
    /// <summary>
    /// Helper method to find the first instance ID of a card with the given card ID
    /// </summary>
    private string FindFirstInstanceOfCard(int cardId)
    {
        foreach (var entry in spawnedCardInstances)
        {
            if (ExtractCardIdFromInstanceId(entry.Key) == cardId)
            {
                return entry.Key;
            }
        }
        return null;
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
            
            // Remove from type-based dictionary - make sure we're removing only this specific card object
            if (spawnedCardsByType.TryGetValue(cardId, out List<GameObject> cardObjectList))
            {
                // Make sure we're only removing the exact card instance, not any card with the same ID
                if (cardObjectList.Contains(cardObject))
                {
                    cardObjectList.Remove(cardObject);
                    
                    // Clean up the list if it's now empty
                    if (cardObjectList.Count == 0)
                    {
                        spawnedCardsByType.Remove(cardId);
                    }
                }
                else
                {
                    Debug.LogWarning($"RemoveCardInstance: Card object for instance {instanceId} was not found in spawnedCardsByType list for card ID {cardId}");
                }
            }
            
            // Destroy the card object
            Destroy(cardObject);
            
            Debug.Log($"Removed card instance {instanceId} from display");
        }
        else
        {
            Debug.LogWarning($"RemoveCardInstance: Card instance {instanceId} not found in spawnedCardInstances dictionary");
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
                
                // Important: Log which specific card was removed to help with debugging
                Debug.Log($"RemoveCardFromDisplay: Removed specific card instance {instanceIdToRemove} from display");
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