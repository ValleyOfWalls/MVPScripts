using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using System.Collections;

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
    
    // Add a queue to track played card instances waiting for confirmation
    private HashSet<string> pendingCardRemovals = new HashSet<string>();
    
    // Reference to owning network entity
    private NetworkBehaviour owningEntity;
    
    // Reference to RelationshipManager for client ID checking
    private RelationshipManager relationshipManager;
    
    // Cache local client ID for quicker comparisons
    private int localClientId = -1;
    
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
        
        // Determine if we're attached to a player or pet
        owningEntity = GetComponent<NetworkPlayer>() ?? (NetworkBehaviour)GetComponent<NetworkPet>();
        if (owningEntity == null)
        {
            Debug.LogError("CardSpawner must be attached to either NetworkPlayer or NetworkPet");
        }
        
        // Get relationship manager
        relationshipManager = GetComponent<RelationshipManager>();
        if (relationshipManager == null)
        {
            relationshipManager = gameObject.AddComponent<RelationshipManager>();
            Debug.Log($"Added RelationshipManager to {gameObject.name} for CardSpawner");
        }
        
        // Cache local client ID
        if (IsClientInitialized && FishNet.InstanceFinder.ClientManager != null)
        {
            localClientId = FishNet.InstanceFinder.ClientManager.Connection.ClientId;
            Debug.Log($"CardSpawner local client ID: {localClientId}");
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
    /// Called specifically when a hand is discarded to ensure visual updates
    /// </summary>
    public void HandleHandDiscarded()
    {
        if (!IsClientInitialized) return; // Only clients handle visual representation
        
        Debug.Log($"HandleHandDiscarded called for {(localPlayer != null ? localPlayer.PlayerName.Value : localPet?.PetName.Value)}");
        
        // Force clear all visual cards
        ClearAllCards();
    }
    
    /// <summary>
    /// Updates the visual representation of cards in hand
    /// </summary>
    private void UpdateCardDisplay()
    {
        if (combatHand == null || handTransform == null) return;

        List<int> currentHandCards = combatHand.GetAllCards();
        
        // If hand is empty, clear all visual cards
        if (currentHandCards.Count == 0)
        {
            Debug.Log($"Hand is now empty, clearing all visual cards for {(localPlayer != null ? localPlayer.PlayerName.Value : localPet?.PetName.Value)}");
            ClearAllCards();
            return;
        }
        
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
        
        // Remove cards that shouldn't be in the hand anymore
        foreach (var spawnedEntry in currentlySpawnedCounts)
        {
            int cardId = spawnedEntry.Key;
            int numSpawned = spawnedEntry.Value;
            int numDesired = 0;
            desiredCardCounts.TryGetValue(cardId, out numDesired);
            
            if (numSpawned > numDesired)
            {
                // Remove excess cards
                int numToRemove = numSpawned - numDesired;
                for (int i = 0; i < numToRemove; i++)
                {
                    RemoveCardFromDisplay(cardId);
                }
            }
        }
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
        
        // Add to pending removals set
        pendingCardRemovals.Add(instanceId);
        
        // Temporarily disable the card's button to prevent double-clicks
        if (spawnedCardInstances.TryGetValue(instanceId, out GameObject cardObject))
        {
            Button cardButton = cardObject.GetComponent<Button>();
            if (cardButton != null)
            {
                cardButton.interactable = false;
            }
            
            // Apply a visual effect to show it's being played
            // Use a simple scale down effect
            cardObject.transform.localScale = cardObject.transform.localScale * 0.9f;
        }
        
        // Send request to server with the specific instance ID
        combatManager.CmdPlayerRequestsPlayCard(localPlayer.ObjectId, cardId, instanceId);
    }
    
    /// <summary>
    /// Called when the server confirms a card has been played but doesn't provide an instance ID
    /// </summary>
    public void OnServerConfirmCardPlayed(int cardId)
    {
        Debug.Log($"Server confirmed card played (no instance ID): ID {cardId}");
        
        // Find and remove the first instance of this card type, but only if it belongs to local client
        if (BelongsToLocalClient())
        {
            // Find any pending removal for this card ID first
            string pendingInstanceId = pendingCardRemovals
                .FirstOrDefault(id => ExtractCardIdFromInstanceId(id) == cardId);
                
            if (!string.IsNullOrEmpty(pendingInstanceId))
            {
                Debug.Log($"Found pending removal for card ID {cardId}: {pendingInstanceId}");
                OnServerConfirmCardPlayed(cardId, pendingInstanceId);
                return;
            }
            
            // If no pending removal, try to find the first instance
            string firstInstanceId = FindFirstInstanceOfCard(cardId);
            
            if (!string.IsNullOrEmpty(firstInstanceId))
            {
                Debug.Log($"OnServerConfirmCardPlayed: Using specific instance ID {firstInstanceId} instead of generic removal");
                OnServerConfirmCardPlayed(cardId, firstInstanceId);
            }
            else
            {
                Debug.LogWarning($"OnServerConfirmCardPlayed: Could not find any instance of card ID {cardId} to remove");
            }
        }
        else
        {
            Debug.Log($"Ignoring card play notification for card ID {cardId} since it does not belong to local client");
        }
    }
    
    /// <summary>
    /// Called when the server confirms a card has been played with a specific instance ID
    /// </summary>
    public void OnServerConfirmCardPlayed(int cardId, string instanceId)
    {
        Debug.Log($"Server confirmed card played: ID {cardId}, Instance {instanceId}");
        
        // First, check if we have the exact instance ID match
        if (!string.IsNullOrEmpty(instanceId) && spawnedCardInstances.TryGetValue(instanceId, out GameObject specificCardObject))
        {
            // Remove the instance from the pending set if it was there
            pendingCardRemovals.Remove(instanceId);
            
            // Remove the specific card instance
            RemoveCardInstance(instanceId);
            Debug.Log($"Removed specific card instance {instanceId} from display");
            return; // Exit early - we found and removed the exact card instance
        }
        
        // If we couldn't find the exact instance - e.g., if this is a notification from the server for another client's action
        // Then we need to carefully handle this to avoid removing wrong cards
        
        // Check if we have any pending removals for this card ID that the local player initiated
        string pendingInstanceId = pendingCardRemovals
            .FirstOrDefault(id => ExtractCardIdFromInstanceId(id) == cardId);
        
        if (!string.IsNullOrEmpty(pendingInstanceId))
        {
            Debug.Log($"Found pending removal for card ID {cardId}: {pendingInstanceId}");
            RemoveCardInstance(pendingInstanceId);
            pendingCardRemovals.Remove(pendingInstanceId);
        }
        else if (BelongsToLocalClient())
        {
            // We only want to do this fallback for cards belonging to the local client
            // This avoids accidentally removing other players' cards when the server notifies all clients
            
            Debug.Log($"OnServerConfirmCardPlayed: Local client needs to remove a card with ID {cardId}, but no instance ID match was found.");
            
            // Only remove the first instance of this card type - specifically checking if this is owned by local client
            string firstInstanceId = FindFirstInstanceOfCard(cardId);
            
            if (!string.IsNullOrEmpty(firstInstanceId))
            {
                Debug.Log($"OnServerConfirmCardPlayed: Fallback - using first found instance ID {firstInstanceId} for card ID {cardId}");
                RemoveCardInstance(firstInstanceId);
            }
            else
            {
                Debug.LogWarning($"OnServerConfirmCardPlayed: No valid instance found for card ID {cardId}, cannot remove card");
            }
        }
        else
        {
            Debug.Log($"Ignoring card play notification for card ID {cardId} since it does not belong to local client");
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
    /// Removes all cards from the display with visual fading
    /// </summary>
    private void ClearAllCards()
    {
        Debug.Log($"ClearAllCards called for {(localPlayer != null ? localPlayer.PlayerName.Value : localPet?.PetName.Value)}");
        
        // Make a copy of the cards to avoid collection modification issues
        Dictionary<string, GameObject> cardsCopy = new Dictionary<string, GameObject>(spawnedCardInstances);
        
        foreach (var cardEntry in cardsCopy)
        {
            string instanceId = cardEntry.Key;
            GameObject cardObject = cardEntry.Value;
            
            if (cardObject != null)
            {
                // Add visual fade out animation
                StartCoroutine(AnimateCardDiscard(cardObject, () => {
                    if (cardObject != null) Destroy(cardObject);
                }));
            }
        }
        
        // Clear all tracking collections
        spawnedCardsByType.Clear();
        spawnedCardInstances.Clear();
        cardInstanceCounter.Clear();
        pendingCardRemovals.Clear();
    }
    
    /// <summary>
    /// Animate a card being discarded with a fade out effect
    /// </summary>
    private System.Collections.IEnumerator AnimateCardDiscard(GameObject cardObject, System.Action onComplete)
    {
        if (cardObject == null) 
        {
            if (onComplete != null) onComplete();
            yield break;
        }
        
        // Try to get the card's canvas group for fading
        CanvasGroup canvasGroup = cardObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = cardObject.AddComponent<CanvasGroup>();
        }
        
        // Initial values
        float duration = 0.5f;
        float elapsedTime = 0f;
        float startAlpha = canvasGroup.alpha;
        Vector3 startScale = cardObject.transform.localScale;
        Vector3 targetScale = startScale * 0.8f;
        
        // Animate fade out and scale down
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            
            // Apply fading
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            
            // Apply scaling
            cardObject.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            
            yield return null;
        }
        
        // Ensure final state
        canvasGroup.alpha = 0f;
        
        // Complete the animation
        if (onComplete != null) onComplete();
    }
    
    /// <summary>
    /// Called when a card is played to remove it from display
    /// </summary>
    public void OnCardPlayed(int cardId)
    {
        RemoveCardFromDisplay(cardId);
    }

    /// <summary>
    /// Checks if this CardSpawner belongs to the local client
    /// </summary>
    public bool BelongsToLocalClient()
    {
        if (IsServerInitialized && !IsClientInitialized)
        {
            // On dedicated server, we need to handle everything
            Debug.Log($"BelongsToLocalClient: Dedicated server handles all entities");
            return true;
        }
        
        // Host handles all entities
        if (FishNet.InstanceFinder.IsHostStarted)
        {
            Debug.Log($"BelongsToLocalClient: Host handles all entities");
            return true;
        }
        
        // Check if entity is owned by this client directly
        if (owningEntity != null && owningEntity.IsOwner)
        {
            Debug.Log($"BelongsToLocalClient: Entity is directly owned by this client");
            return true;
        }
        
        // Check RelationshipManager client ID - main fix
        if (relationshipManager != null)
        {
            if (relationshipManager.OwnerClientId == localClientId)
            {
                Debug.Log($"BelongsToLocalClient: Entity belongs to this client via RelationshipManager (OwnerClientId: {relationshipManager.OwnerClientId}, LocalClientId: {localClientId})");
                return true;
            }
            else
            {
                Debug.Log($"BelongsToLocalClient: Entity does NOT belong to this client (RM OwnerClientId: {relationshipManager.OwnerClientId}, LocalClientId: {localClientId})");
            }
        }
        
        // Fall back to the basic Owner check
        if (owningEntity != null && owningEntity.Owner != null)
        {
            bool result = owningEntity.Owner.ClientId == localClientId;
            Debug.Log($"BelongsToLocalClient: Basic owner check result: {result} (Owner ClientId: {owningEntity.Owner.ClientId}, LocalClientId: {localClientId})");
            return result;
        }
        
        // If we're on client-only mode, this might be a special case
        if (IsClientInitialized && !IsServerInitialized)
        {
            // If localPlayer is set, this is the local player's CardSpawner
            if (localPlayer != null && localPlayer.IsOwner)
            {
                Debug.Log($"BelongsToLocalClient: Special client-only case for local player: {localPlayer.PlayerName.Value}");
                return true;
            }
        }
        
        Debug.Log($"BelongsToLocalClient: Entity ownership check failed - Entity does not belong to local client");
        return false;
    }

    /// <summary>
    /// Creates a card game object at the specified position
    /// </summary>
    public GameObject SpawnCard(int cardId, Transform parentTransform, bool addToHand = true)
    {
        // Check if we should handle this card
        if (!BelongsToLocalClient() && !FishNet.InstanceFinder.IsHostStarted)
        {
            Debug.Log($"SpawnCard: Ignored card {cardId} because it doesn't belong to local client (Owner ClientID: {(relationshipManager != null ? relationshipManager.OwnerClientId : -1)}, Local ClientID: {localClientId})");
            return null;
        }
        
        if (cardPrefab == null)
        {
            Debug.LogError("Card prefab not assigned to CardSpawner");
            return null;
        }

        // Make sure parent transform exists
        Transform parent = parentTransform ? parentTransform : cardPrefabParent;
        if (parent == null)
        {
            Debug.LogWarning("No parent transform specified for card. Using default parent.");
            parent = transform;
        }

        // Get card data
        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null)
        {
            Debug.LogError($"Card with ID {cardId} not found in database");
            return null;
        }

        // Create an instance ID for this card
        string instanceId = System.Guid.NewGuid().ToString().Substring(0, 8);
        string fullInstanceId = $"{cardId}_{instanceId}";

        // Instantiate the card prefab
        GameObject cardObj = Instantiate(cardPrefab, parent);
        
        // Position the card in the UI using a layout group or manual positioning
        // This will depend on your UI setup

        // Set up the card's visuals and data
        Card cardComponent = cardObj.GetComponent<Card>();
        if (cardComponent != null)
        {
            // Initialize the card with its data
            cardComponent.Initialize(cardData);
            
            // Store the instance ID
            cardObj.name = $"Card_{cardData.CardName}_{fullInstanceId}";
            
            // Add click handler if appropriate
            if (owningEntity != null && owningEntity.IsOwner)
            {
                // Add any needed click handlers or drag handlers
                // These would use your existing components
            }
        }

        // Store card in dictionary for later access
        spawnedCardInstances[fullInstanceId] = cardObj;
        
        Debug.Log($"Spawned card {cardData.CardName} (ID: {cardId}, Instance: {fullInstanceId}) for {(owningEntity is NetworkPlayer ? "player" : "pet")}");

        return cardObj;
    }
} 