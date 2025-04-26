using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Collections;

public class CardManager
{
    private GameManager gameManager;
    
    // Card data and configuration
    private List<CardData> starterDeck = new List<CardData>();
    private List<CardData> starterPetDeck = new List<CardData>();
    private List<CardData> allPlayerCardPool = new List<CardData>();
    private List<CardData> allPetCardPool = new List<CardData>();
    private int cardsToDraw = 5;
    
    // Manager instances
    private DeckManager deckManager;
    private PetDeckManager petDeckManager;
    private CardEffectService cardEffectService;
    private CardModificationService cardModificationService;
    private DraftService draftService;
    
    // --- ADDED: Play Count Tracking ---
    private Dictionary<string, int> cardPlayCountsThisCombat = new Dictionary<string, int>();
    private Dictionary<CardData, string> cardInstanceIds = new Dictionary<CardData, string>();
    private int nextCardId = 0;
    // --- END ADDED ---
    
    // Constants (maintaining the original API for other classes)
    // Draft constants
    public const string DRAFT_PLAYER_QUEUES_PROP = "DraftQueues";
    public const string DRAFT_PICKS_MADE_PROP = "DraftPicks";
    public const string DRAFT_ORDER_PROP = "DraftOrder";
    public const string PLAYER_PET_DECK_PROP = "PetDeck";
    
    public CardManager(GameManager gameManager, List<CardData> starterDeck, List<CardData> starterPetDeck,
                      List<CardData> allPlayerCardPool, List<CardData> allPetCardPool, int cardsToDraw)
    {
        this.gameManager = gameManager;
        this.starterDeck = new List<CardData>(starterDeck);
        this.starterPetDeck = new List<CardData>(starterPetDeck);
        this.allPlayerCardPool = new List<CardData>(allPlayerCardPool);
        this.allPetCardPool = new List<CardData>(allPetCardPool);
        this.cardsToDraw = cardsToDraw;
        
        // Initialize sub-managers
        this.deckManager = new DeckManager(gameManager, starterDeck, cardsToDraw);
        this.petDeckManager = new PetDeckManager(gameManager, starterPetDeck, allPetCardPool);
        this.cardEffectService = new CardEffectService(gameManager);
        this.cardModificationService = new CardModificationService(gameManager);
        this.draftService = new DraftService(gameManager, allPlayerCardPool, allPetCardPool);
    }
    
    public void InitializeAndSyncLocalPetDeck()
    {
        petDeckManager.InitializeAndSyncLocalPetDeck();
    }
    
    public void InitializeDecks()
    {
        deckManager.InitializeDecks();
        petDeckManager.ShuffleLocalPetDeck();
        ResetCombatCardPlayCounts(); // Reset play counts when decks are initialized for combat
        
        // Generate unique IDs for all cards in the deck
        foreach (CardData card in deckManager.GetDeck())
        {
            AssignUniqueIdToCard(card);
        }
        Debug.Log($"Assigned unique IDs to {cardInstanceIds.Count} cards in deck");
    }
    
    // --- ADDED: Reset Play Counts ---
    public void ResetCombatCardPlayCounts()
    {
        cardPlayCountsThisCombat.Clear();
        cardInstanceIds.Clear();
        nextCardId = 0;
        Debug.Log("Combat card play counts reset.");
    }
    // --- END ADDED ---
    
    // --- ADDED: Assign Unique ID to Card Instance ---
    private string AssignUniqueIdToCard(CardData card, GameObject cardGameObject = null)
    {
        if (card == null) return null;
        
        // If we have a GameObject, use its instance ID for uniqueness
        if (cardGameObject != null)
        {
            int cardInstanceId = cardGameObject.GetInstanceID();
            string gameObjectKey = $"{card.cardName}_go_{cardInstanceId}";
            
            // Check if we already have an ID for this specific GameObject
            if (!string.IsNullOrEmpty(cardGameObject.name) && 
                cardGameObject.name.Contains("_instance_"))
            {
                // The card GameObject already has an instance ID in its name, extract and reuse it
                string[] nameParts = cardGameObject.name.Split('_');
                if (nameParts.Length >= 3)
                {
                    string existingInstanceId = $"{card.cardName}_instance_{nameParts[nameParts.Length - 1]}";
                    Debug.Log($"Reusing existing instance ID from GameObject: {existingInstanceId}");
                    return existingInstanceId;
                }
            }
            
            // In Debug Card Reuse Mode, we should use the GameObject's instance ID directly
            // rather than looking up CardData in our dictionary, since the same CardData might
            // be reused for different cards with the same name
            bool debugCardReuseMode = gameManager.IsDebugCardReuseMode();
            if (debugCardReuseMode)
            {
                string instanceId = $"{card.cardName}_instance_{cardInstanceId}";
                Debug.Log($"Debug Card Reuse Mode: Created unique GameObject-based ID: {instanceId}");
                return instanceId;
            }
            
            // For normal mode, continue with CardData-based mapping
            if (cardInstanceIds.TryGetValue(card, out string existingId))
            {
                Debug.Log($"Found existing instance ID for {gameObjectKey}: {existingId}");
                return existingId;
            }
            
            string newInstanceId = $"{card.cardName}_instance_{nextCardId++}";
            cardInstanceIds[card] = newInstanceId;
            Debug.Log($"Created new unique instance ID for card: {newInstanceId} using GameObject ID: {cardInstanceId}");
            return newInstanceId;
        }
        else
        {
            // Fallback to using CardData reference if no GameObject provided
            if (cardInstanceIds.TryGetValue(card, out string existingId))
            {
                return existingId;
            }
            
            string instanceId = $"{card.cardName}_instance_{nextCardId++}";
            cardInstanceIds[card] = instanceId;
            Debug.Log($"Created new unique instance ID for card: {instanceId} (no GameObject provided)");
            return instanceId;
        }
    }
    // --- END ADDED ---
    
    public void DrawHand()
    {
        // Before drawing, ensure no lingering temp upgrades from previous states
        cardModificationService.RevertExpiredHandUpgrades(true); 
        deckManager.DrawHand();
        
        // Assign unique IDs to all drawn cards
        foreach (CardData card in deckManager.GetHand())
        {
            AssignUniqueIdToCard(card, null); // Pass null for GameObject as we don't have access to it here
        }
    }
    
    public void DrawCard()
    {
        deckManager.DrawCard();
        
        // Assign unique ID to the newly drawn card
        List<CardData> hand = deckManager.GetHand();
        if (hand.Count > 0)
        {
            AssignUniqueIdToCard(hand[hand.Count - 1], null); // Pass null for GameObject
        }
    }
    
    public void DiscardHand()
    {
        List<CardData> cardsToDiscard = new List<CardData>(deckManager.GetHand());

        // Trigger discard animations *before* actually discarding
        CombatUIManager combatUIManager = gameManager.GetCombatUIManager();
        if (combatUIManager != null)
        {
            foreach(CardData card in cardsToDiscard)
            {
                combatUIManager.TriggerDiscardAnimation(card);
            }
        }
        else
        {
            Debug.LogWarning("CombatUIManager not found, cannot trigger discard animations.");
        }

        deckManager.DiscardHand();
        
        // Process discard triggers for each card (after they are moved to discard pile)
        foreach(CardData card in cardsToDiscard)
        {
            // Remove any temporary upgrade tracking
            // This is now handled by CardModificationService during end of turn processing
            
            // Remove cost modifier tracking
            gameManager.GetPlayerManager().RemoveCostModifierForCard(card);
            
            // Process discard trigger
            HandleDiscardTrigger(card, CardDropZone.TargetType.PlayerSelf);
        }
    }
    
    public void HandleDiscardTrigger(CardData card, CardDropZone.TargetType targetType = CardDropZone.TargetType.PlayerSelf)
    {
        cardEffectService.HandleDiscardTrigger(card, targetType);
    }
    
    // --- MODIFIED: Accept optional GameObject --- 
    public bool AttemptPlayCard(CardData cardData, CardDropZone.TargetType targetType, GameObject cardGO = null)
    {
        Debug.Log($"CardManager.AttemptPlayCard called: Card '{cardData.cardName}', TargetType '{targetType}'");
        
        // Basic checks
        if (cardData == null)
        {
            Debug.LogError("AttemptPlayCard: cardData is null!");
            return false;
        }
        
        // Energy Check with Modifier
        PlayerManager playerManager = gameManager.GetPlayerManager();
        int cardSpecificModifier = playerManager.GetLocalHandCostModifier(cardData);
        int effectiveCost = Mathf.Max(0, cardData.cost + cardSpecificModifier);
        
        if (effectiveCost > playerManager.GetCurrentEnergy())
        {
            Debug.LogWarning($"AttemptPlayCard: Cannot play card '{cardData.cardName}' - Effective Cost ({effectiveCost}) exceeds current energy ({playerManager.GetCurrentEnergy()}).");
            return false; // Not enough energy
        }
        
        // --- ADDED: Check for debug card reuse mode ---
        bool debugCardReuseMode = gameManager.IsDebugCardReuseMode();
        bool removed = false;
        
        if (!debugCardReuseMode)
        {
            // Normal behavior - Attempt to remove the card from the hand list
            // --- MODIFIED: Pass cardGO to DiscardCard ---
            removed = deckManager.DiscardCard(cardData, cardGO); 
            Debug.Log($"Attempting to remove card '{cardData.cardName}' from hand. Result: {removed}");
        }
        else
        {
            // Debug mode - Skip removing card from hand
            Debug.Log($"Debug Card Reuse Mode: Card '{cardData.cardName}' will NOT be discarded when played");
            removed = true; // Pretend the card was successfully removed
        }
        // --- END ADDED ---
        
        if (removed)
        {
            // Remove cost modifier tracking for played card
            playerManager.RemoveCostModifierForCard(cardData);
            
            // --- ADDED: Only update UI if not in debug reuse mode ---
            if (!debugCardReuseMode)
            {
                gameManager.UpdateHandUI();
                gameManager.UpdateDeckCountUI();
            }
            // --- END ADDED ---
            
            // Consume energy (using effective cost)
            // --- MODIFIED: Skip energy consumption in debug mode ---
            if (!debugCardReuseMode)
            {
                playerManager.ConsumeEnergy(effectiveCost);
                Debug.Log($"Played card '{cardData.cardName}'. Energy remaining: {playerManager.GetCurrentEnergy()}");
            }
            else
            {
                Debug.Log($"Debug Card Reuse Mode: Energy NOT consumed for '{cardData.cardName}'");
            }
            // --- END MODIFIED ---
            
            // --- MODIFIED: Get play count BEFORE processing effects --- 
            int previousPlays = GetCardPlayCountThisCombat(cardData, cardGO);
            // --- ADDED: Get copy count BEFORE processing effects ---
            int totalCopies = GetTotalCardCopies(cardData);
            
            // Apply card effects via CardEffectService
            // --- MODIFIED: Pass totalCopies --- 
            bool effectsProcessed = cardEffectService.ProcessCardEffect(cardData, targetType, previousPlays, totalCopies);

            // Update health/status UI AFTER effects are processed
            if (effectsProcessed)
            {
                gameManager.UpdateHealthUI(); // This will also update status effects like combo
                
                // Increment the play count for this card
                IncrementCardPlayCount(cardData, cardGO);
            }
            
            Debug.Log($"Successfully processed effects for card '{cardData.cardName}' on target '{targetType}'.");
            return effectsProcessed;
        }
        else
        {
            Debug.LogWarning($"AttemptPlayCard: Card '{cardData.cardName}' NOT found in hand list for removal (reference equality failed?).");
            return false;
        }
    }
    
    /// <summary>
    /// Get the number of times this card has been played in this combat.
    /// </summary>
    public int GetCardPlayCountThisCombat(CardData card, GameObject cardGameObject = null)
    {
        if (card == null) return 0;
        
        // Get the instance ID for this card
        string instanceId = AssignUniqueIdToCard(card, cardGameObject);
        
        // Get the play count for this instance
        if (cardPlayCountsThisCombat.TryGetValue(instanceId, out int playCount))
        {
            Debug.Log($"Play count for {instanceId}: {playCount}");
            return playCount;
        }
        
        // If not played yet, return 0
        Debug.Log($"Play count for {instanceId}: 0 (never played)");
        return 0;
    }
    
    // --- ADDED: Get Total Card Copies ---
    public int GetTotalCardCopies(CardData cardData)
    {
        if (cardData == null)
        {
            Debug.LogWarning($"GetTotalCardCopies: Invalid CardData. Returning 0.");
            return 0;
        }

        string nameToCount;
        
        if (!string.IsNullOrEmpty(cardData.cardFamilyName))
        {
            // Use cardFamilyName if available
            nameToCount = cardData.cardFamilyName;
        }
        else
        {
            // Fall back to cardName if cardFamilyName is missing
            nameToCount = cardData.cardName;
            Debug.Log($"GetTotalCardCopies: Using cardName '{cardData.cardName}' instead of missing cardFamilyName");
        }
        
        List<CardData> allCards = deckManager.GetAllOwnedPlayerCards(); // Includes deck, hand, discard
        int count = allCards.Count(card => card != null && 
            (!string.IsNullOrEmpty(card.cardFamilyName) ? card.cardFamilyName == nameToCount : card.cardName == nameToCount));
        
        // Subtract 1 to exclude the card itself from the count
        int adjustedCount = Mathf.Max(0, count - 1);
        Debug.Log($"GetTotalCardCopies for '{nameToCount}': Found {count} copies, adjusted to {adjustedCount} (excluding self).");
        return adjustedCount;
    }
    // --- END ADDED ---
    
    public void ProcessOpponentPetCardEffect(CardData cardToPlay, GameObject cardGameObject = null)
    {
        if (cardToPlay == null) return;
        
        // Get the play count BEFORE processing the effect
        int previousPlays = GetCardPlayCountThisCombat(cardToPlay, cardGameObject);
        
        // Count total copies - look for cards with same card family name or card name
        // in all opponent pet cards (deck, hand, discard)
        List<CardData> allOpponentPetCards = new List<CardData>();
        allOpponentPetCards.AddRange(petDeckManager.GetOpponentPetDeck());
        allOpponentPetCards.AddRange(petDeckManager.GetOpponentPetHand());
        allOpponentPetCards.AddRange(petDeckManager.GetOpponentPetDiscard());
        
        int totalCopies = allOpponentPetCards.Count(c => 
            (!string.IsNullOrEmpty(c.cardFamilyName) && !string.IsNullOrEmpty(cardToPlay.cardFamilyName)) ? 
            c.cardFamilyName == cardToPlay.cardFamilyName : c.cardName == cardToPlay.cardName);
        
        // Subtract 1 to exclude the card itself from the count
        int adjustedTotalCopies = Mathf.Max(0, totalCopies - 1);
        
        // Process the card effect with the play count
        cardEffectService.ProcessOpponentPetCardEffect(cardToPlay, previousPlays, adjustedTotalCopies);
        
        // Increment the play count AFTER processing the effect
        IncrementCardPlayCount(cardToPlay, cardGameObject);
        
        Debug.Log($"Processed opponent pet card '{cardToPlay.cardName}' - previous plays: {previousPlays}, total copies: {totalCopies}, adjusted copies: {adjustedTotalCopies} (excluding self)");
    }
    
    public void ProcessEndOfTurnHandEffects()
    {
        cardModificationService.ProcessEndOfTurnHandEffects();
    }
    
    public IEnumerator ExecuteOpponentPetTurn(int startingPetEnergy)
    {
        Debug.Log("CardManager forwarding Opponent Pet Turn execution.");
        IEnumerator petTurnEnumerator = petDeckManager.ExecuteOpponentPetTurn(startingPetEnergy);
        while (petTurnEnumerator.MoveNext()) {
            // Explicitly yield whatever the PetDeckManager yields (should be CardData or null)
            yield return petTurnEnumerator.Current;
        }
        Debug.Log("CardManager finished forwarding Opponent Pet Turn execution.");
    }
    
    public void InitializeOpponentPetDeck(List<string> cardNames)
    {
        petDeckManager.InitializeOpponentPetDeck(cardNames);
        
        // Assign unique IDs to all cards in the opponent pet deck
        foreach (CardData card in petDeckManager.GetOpponentPetDeck())
        {
            AssignUniqueIdToOpponentPetCard(card);
        }
        Debug.Log($"Assigned unique IDs to {petDeckManager.GetOpponentPetDeck().Count} cards in opponent pet deck");
    }
    
    // ----- Draft Methods -----
    
    public void InitializeDraftState(int optionsPerDraft)
    {
        draftService.InitializeDraftState(optionsPerDraft);
    }
    
    public void HandlePlayerDraftPick(int optionId)
    {
        draftService.HandlePlayerDraftPick(optionId);
    }
    
    public void UpdateLocalDraftStateFromRoomProps()
    {
        draftService.UpdateLocalDraftStateFromRoomProps();
    }
    
    public void UpdateLocalDraftPicksFromRoomProps()
    {
        draftService.UpdateLocalDraftPicksFromRoomProps();
    }
    
    public void UpdateLocalDraftOrderFromRoomProps()
    {
        draftService.UpdateLocalDraftOrderFromRoomProps();
    }
    
    // ----- Getters -----
    
    // Core hand/deck accessors
    public List<CardData> GetDeck() => deckManager.GetDeck();
    public List<CardData> GetHand() => deckManager.GetHand();
    public List<CardData> GetDiscardPile() => deckManager.GetDiscardPile();
    public List<CardData> GetLocalPetDeck() => petDeckManager.GetLocalPetDeck();
    public List<CardData> GetOpponentPetDeck() => petDeckManager.GetOpponentPetDeck();
    public int GetDeckCount() => deckManager.GetDeckCount();
    public int GetDiscardCount() => deckManager.GetDiscardCount();
    public int GetHandCount() => deckManager.GetHandCount();
    
    // Deck view helpers
    public List<CardData> GetAllOwnedPlayerCards() => deckManager.GetAllOwnedPlayerCards();
    public List<CardData> GetAllOwnedPetCards() => petDeckManager.GetAllOwnedPetCards();
    
    // Draft state access
    public bool IsWaitingForLocalPick() => draftService.IsWaitingForLocalPick();
    public void SetWaitingForLocalPick(bool value) => draftService.SetWaitingForLocalPick(value);
    public Dictionary<int, DraftOption> GetLocalActiveOptionMap() => draftService.GetLocalActiveOptionMap();
    public List<SerializableDraftOption> GetLocalCurrentPack() => draftService.GetLocalCurrentPack();
    
    // Card modification checks
    public bool IsCardTemporarilyUpgraded(CardData card) => cardModificationService.IsCardTemporarilyUpgraded(card);
    
    // Manager accessors
    public DeckManager GetDeckManager() => deckManager;
    public PetDeckManager GetPetDeckManager() => petDeckManager;
    public CardEffectService GetCardEffectService() => cardEffectService;
    public CardModificationService GetCardModificationService() => cardModificationService;
    public DraftService GetDraftService() => draftService;
    
    // --- ADDED: Methods to handle cards added to deck during gameplay ---
    public void AddCardToPlayerDeck(CardData card)
    {
        if (card == null) return;
        
        // Assign a unique ID before adding to deck
        AssignUniqueIdToCard(card, null); // Pass null for GameObject
        
        // Then add to deck
        deckManager.AddCardToPlayerDeck(card);
    }
    
    // --- ADDED: Wrapper for any operation that creates or copies cards ---
    public CardData PrepareCardInstance(CardData cardData)
    {
        if (cardData == null) return null;
        
        // Assign a unique ID to the card instance
        AssignUniqueIdToCard(cardData, null); // Pass null for GameObject
        return cardData;
    }

    /// <summary>
    /// Increment the play count for a card.
    /// </summary>
    private void IncrementCardPlayCount(CardData card, GameObject cardGameObject = null)
    {
        if (card == null) return;
        
        string instanceId = AssignUniqueIdToCard(card, cardGameObject);
        
        // Increment the play count
        if (cardPlayCountsThisCombat.ContainsKey(instanceId))
        {
            cardPlayCountsThisCombat[instanceId]++;
        }
        else
        {
            cardPlayCountsThisCombat[instanceId] = 1;
        }
        
        Debug.Log($"Incremented play count for {instanceId} to {cardPlayCountsThisCombat[instanceId]}");
    }

    // --- ADDED: Method to assign unique IDs to opponent pet cards ---
    public string AssignUniqueIdToOpponentPetCard(CardData card)
    {
        if (card == null) return null;
        
        // Use the same AssignUniqueIdToCard method to ensure consistent ID generation
        string instanceId = AssignUniqueIdToCard(card, null);
        Debug.Log($"Assigned unique ID to opponent pet card: {instanceId}");
        return instanceId;
    }
}