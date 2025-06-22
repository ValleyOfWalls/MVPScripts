using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System.Linq;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages card upgrades by monitoring conditions and executing upgrades both in-fight and persistently.
/// Attach to: A persistent GameObject in the scene (singleton).
/// </summary>
public class CardUpgradeManager : NetworkBehaviour
{
    [Header("Upgrade Configuration")]
    [Tooltip("Automatically detects upgradeable cards from all CardData assets")]
    [SerializeField, ReadOnly] private int detectedUpgradeableCards = 0;
    
    [Header("Debug Info (Read Only)")]
    [SerializeField, ReadOnly] private List<string> pendingUpgrades = new List<string>();
    [SerializeField, ReadOnly] private List<string> completedUpgrades = new List<string>();
    
    // Singleton instance
    public static CardUpgradeManager Instance { get; private set; }
    
    // Network synchronized data for persistent tracking
    private readonly SyncDictionary<int, int> persistentPlayCounts = new SyncDictionary<int, int>(); // cardId -> play count across fights
    private readonly SyncDictionary<int, int> persistentWinCounts = new SyncDictionary<int, int>(); // cardId -> wins with card
    
    // Per-fight tracking
    private Dictionary<int, HashSet<int>> upgradedCardsThisFight = new Dictionary<int, HashSet<int>>(); // entityId -> set of upgraded card IDs
    private Dictionary<int, Dictionary<int, int>> turnPlayCounts = new Dictionary<int, Dictionary<int, int>>(); // entityId -> cardId -> count this turn
    private Dictionary<int, Dictionary<int, int>> consecutiveTurnCounts = new Dictionary<int, Dictionary<int, int>>(); // entityId -> cardId -> consecutive turns
    
    // Immediate upgrades only
    private List<QueuedUpgrade> immediateUpgrades = new List<QueuedUpgrade>();
    
    // Events
    public event Action<CardData, CardData, NetworkEntity> OnCardUpgraded; // baseCard, upgradedCard, entity
    
    private void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
            // Note: DontDestroyOnLoad is not used with NetworkBehaviour
            // The NetworkManager handles persistence for networked objects
        }
        else
        {
            Debug.LogWarning("CardUpgradeManager: Multiple instances detected. This should be a singleton.");
        }
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Validate all upgrade data
        ValidateUpgradeData();
        
        // Subscribe to game events
        SubscribeToGameEvents();
    }
    
    public override void OnStopServer()
    {
        base.OnStopServer();
        UnsubscribeFromGameEvents();
    }
    
    /// <summary>
    /// Validates all upgrade data configurations
    /// </summary>
    private void ValidateUpgradeData()
    {
        var availableUpgrades = GetUpgradeableCards();
        
        int validCount = availableUpgrades.Count;
        Debug.Log($"CardUpgradeManager: Loaded {validCount} cards with upgrade conditions");
        
        // Log each upgradeable card for debugging
        foreach (var card in availableUpgrades)
        {
            Debug.Log($"  - {card.CardName} → {card.UpgradedVersion.CardName} (Condition: {card.UpgradeConditionType} {card.UpgradeComparisonType} {card.UpgradeRequiredValue})");
        }
    }
    
    /// <summary>
    /// Gets all cards that have upgrade conditions configured by searching all CardData assets
    /// </summary>
    private List<CardData> GetUpgradeableCards()
    {
        #if UNITY_EDITOR
        // In editor, find all CardData assets that can upgrade
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:CardData");
        List<CardData> upgradeableCards = new List<CardData>();
        
        foreach (string guid in guids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            CardData cardData = UnityEditor.AssetDatabase.LoadAssetAtPath<CardData>(assetPath);
            
            if (cardData != null && cardData.CanUpgrade && cardData.UpgradedVersion != null)
            {
                upgradeableCards.Add(cardData);
            }
        }
        
        detectedUpgradeableCards = upgradeableCards.Count;
        return upgradeableCards;
        #else
        // In builds, we need a different approach since we can't use AssetDatabase
        // For now, return empty list - this would need to be populated differently in builds
        Debug.LogWarning("CardUpgradeManager: Automatic card detection only works in editor. In builds, upgradeable cards need to be detected differently.");
        return new List<CardData>();
        #endif
    }
    
    // Removed timing-specific methods since only immediate upgrades are supported
    
    /// <summary>
    /// Subscribe to relevant game events for tracking
    /// </summary>
    private void SubscribeToGameEvents()
    {
        // We'll subscribe to events from EntityTracker and CardTracker
        // This will be called when those components trigger events
    }
    
    /// <summary>
    /// Unsubscribe from game events
    /// </summary>
    private void UnsubscribeFromGameEvents()
    {
        // Cleanup event subscriptions
    }
    
    /// <summary>
    /// Called when a card is played - main entry point for upgrade checking
    /// </summary>
    [Server]
    public void OnCardPlayed(Card card, NetworkEntity entity)
    {
        if (!IsServerInitialized || card?.CardData == null || entity == null) return;
        
        int cardId = card.CardData.CardId;
        int entityId = entity.ObjectId;
        
        // Update persistent tracking
        UpdatePersistentTracking(cardId);
        
        // Update per-turn tracking
        UpdateTurnTracking(entityId, cardId);
        
        // Check for upgrades
        CheckUpgradeConditions(card, entity);
        
        // Process immediate upgrades
        ProcessImmediateUpgrades();
    }
    
    /// <summary>
    /// Called at the end of each turn
    /// </summary>
    [Server]
    public void OnTurnEnd(NetworkEntity entity)
    {
        if (!IsServerInitialized || entity == null) return;
        
        int entityId = entity.ObjectId;
        
        // Reset turn tracking
        if (turnPlayCounts.ContainsKey(entityId))
        {
            turnPlayCounts[entityId].Clear();
        }
    }
    
    /// <summary>
    /// Called at the end of a fight
    /// </summary>
    [Server]
    public void OnFightEnd(bool victory, List<NetworkEntity> participatingEntities)
    {
        if (!IsServerInitialized) return;
        
        // Update persistent win tracking if victory
        if (victory)
        {
            UpdatePersistentWinTracking(participatingEntities);
        }
        
        // Reset fight tracking
        ResetFightTracking();
    }
    
    /// <summary>
    /// Updates persistent play count tracking
    /// </summary>
    private void UpdatePersistentTracking(int cardId)
    {
        if (persistentPlayCounts.ContainsKey(cardId))
        {
            persistentPlayCounts[cardId]++;
        }
        else
        {
            persistentPlayCounts[cardId] = 1;
        }
    }
    
    /// <summary>
    /// Updates per-turn play count tracking
    /// </summary>
    private void UpdateTurnTracking(int entityId, int cardId)
    {
        if (!turnPlayCounts.ContainsKey(entityId))
        {
            turnPlayCounts[entityId] = new Dictionary<int, int>();
        }
        
        if (turnPlayCounts[entityId].ContainsKey(cardId))
        {
            turnPlayCounts[entityId][cardId]++;
        }
        else
        {
            turnPlayCounts[entityId][cardId] = 1;
        }
    }
    
    /// <summary>
    /// Updates persistent win tracking
    /// </summary>
    private void UpdatePersistentWinTracking(List<NetworkEntity> entities)
    {
        foreach (var entity in entities)
        {
            var entityDeck = entity.GetComponent<NetworkEntityDeck>();
            if (entityDeck != null)
            {
                var cardIds = entityDeck.GetAllCardIds();
                foreach (int cardId in cardIds)
                {
                    if (persistentWinCounts.ContainsKey(cardId))
                    {
                        persistentWinCounts[cardId]++;
                    }
                    else
                    {
                        persistentWinCounts[cardId] = 1;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Checks upgrade conditions for a played card
    /// </summary>
    private void CheckUpgradeConditions(Card card, NetworkEntity entity)
    {
        CardData cardData = card.CardData;
        int cardId = cardData.CardId;
        
        // Check if this card can upgrade
        if (!cardData.CanUpgrade || cardData.UpgradedVersion == null)
            return;
            
        // Skip if already upgraded (each card can only upgrade once)
        if (HasBeenUpgraded(entity.ObjectId, cardId))
            return;
        
        // Check if upgrade condition is met
        if (EvaluateUpgradeCondition(cardData, card, entity))
        {
            QueueUpgrade(cardData, card, entity);
        }
    }
    
    /// <summary>
    /// Evaluates whether the single upgrade condition is met
    /// </summary>
    private bool EvaluateUpgradeCondition(CardData cardData, Card card, NetworkEntity entity)
    {
        // Get the current value for this condition type
        int currentValue = GetConditionValue(cardData.UpgradeConditionType, card, entity);
        
        // Compare using the specified comparison type
        return CompareValues(currentValue, cardData.UpgradeRequiredValue, cardData.UpgradeComparisonType);
    }
    
    // Removed EvaluateSingleCondition - now using simplified EvaluateUpgradeCondition
    
    /// <summary>
    /// Gets the current value for a specific condition type
    /// </summary>
    private int GetConditionValue(UpgradeConditionType conditionType, Card card, NetworkEntity entity)
    {
        int cardId = card.CardData.CardId;
        int entityId = entity.ObjectId;
        
        // Get trackers
        var cardTracker = card.GetComponent<CardTracker>();
        var entityTracker = entity.GetComponent<EntityTracker>();
        
        switch (conditionType)
        {
            case UpgradeConditionType.TimesPlayedThisFight:
                return cardTracker?.TimesPlayedThisFight ?? 0;
                
            case UpgradeConditionType.TimesPlayedAcrossFights:
                return persistentPlayCounts.ContainsKey(cardId) ? persistentPlayCounts[cardId] : 0;
                
            case UpgradeConditionType.CopiesInDeck:
                return cardTracker?.TrackingData.cardsWithSameNameInDeck ?? 0;
                
            case UpgradeConditionType.CopiesInHand:
                return cardTracker?.TrackingData.cardsWithSameNameInHand ?? 0;
                
            case UpgradeConditionType.CopiesInDiscard:
                return cardTracker?.TrackingData.cardsWithSameNameInDiscard ?? 0;
                
            case UpgradeConditionType.DamageDealtThisFight:
                return entityTracker?.TrackingData.damageDealtThisFight ?? 0;
                
            case UpgradeConditionType.DamageDealtInSingleTurn:
                return entityTracker?.TrackingData.damageDealtLastRound ?? 0;
                
            case UpgradeConditionType.DamageTakenThisFight:
                return entityTracker?.TrackingData.damageTakenThisFight ?? 0;
                
            case UpgradeConditionType.HealingGivenThisFight:
                return entityTracker?.TrackingData.healingGivenThisFight ?? 0;
                
            case UpgradeConditionType.HealingReceivedThisFight:
                return entityTracker?.TrackingData.healingReceivedThisFight ?? 0;
                
            case UpgradeConditionType.PerfectionStreakAchieved:
                return entityTracker?.PerfectionStreak ?? 0;
                
            case UpgradeConditionType.ComboCountReached:
                return entityTracker?.ComboCount ?? 0;
                
            case UpgradeConditionType.ZeroCostCardsThisTurn:
                return entityTracker?.TrackingData.zeroCostCardsThisTurn ?? 0;
                
            case UpgradeConditionType.ZeroCostCardsThisFight:
                return entityTracker?.TrackingData.zeroCostCardsThisFight ?? 0;
                
            case UpgradeConditionType.PlayedMultipleTimesInTurn:
                return turnPlayCounts.ContainsKey(entityId) && turnPlayCounts[entityId].ContainsKey(cardId) 
                    ? turnPlayCounts[entityId][cardId] : 0;
                
            case UpgradeConditionType.WonFightUsingCard:
                return persistentWinCounts.ContainsKey(cardId) ? persistentWinCounts[cardId] : 0;
                
            case UpgradeConditionType.AllCopiesPlayedFromHand:
                return cardTracker?.AllCopiesPlayedFromHand() == true ? 1 : 0;
                
            case UpgradeConditionType.PlayedWithCombo:
                return cardTracker?.WasPlayedWithCombo() == true ? 1 : 0;
                
            case UpgradeConditionType.PlayedInStance:
                // For this condition, we need to check if the card was played in any specific stance
                // In the simplified system, we'll just check if played in current stance
                var currentStance = entityTracker?.CurrentStance ?? StanceType.None;
                return currentStance != StanceType.None ? 1 : 0;
                
            case UpgradeConditionType.PlayedAsFinisher:
                // Check if card was played when combo count was high enough and card builds combo
                bool isFinisher = card.CardData.RequiresCombo && entityTracker?.ComboCount >= card.CardData.RequiredComboAmount;
                return isFinisher ? 1 : 0;
                
            case UpgradeConditionType.PlayedAtLowHealth:
                float healthPercent = (float)entity.CurrentHealth.Value / entity.MaxHealth.Value * 100f;
                return healthPercent <= 25f ? 1 : 0; // Consider low health as 25% or below
                
            case UpgradeConditionType.PlayedAtHighHealth:
                float healthPercentHigh = (float)entity.CurrentHealth.Value / entity.MaxHealth.Value * 100f;
                return healthPercentHigh >= 75f ? 1 : 0; // Consider high health as 75% or above
                
            // Add more condition types as needed
            default:
                Debug.LogWarning($"CardUpgradeManager: Unhandled condition type {conditionType}");
                return 0;
        }
    }
    
    // Removed CheckContextualRequirements - simplified upgrade system doesn't use contextual requirements
    
    /// <summary>
    /// Compares two values using the specified comparison type
    /// </summary>
    private bool CompareValues(int currentValue, int requiredValue, UpgradeComparisonType comparisonType)
    {
        switch (comparisonType)
        {
            case UpgradeComparisonType.GreaterThanOrEqual:
                return currentValue >= requiredValue;
            case UpgradeComparisonType.Equal:
                return currentValue == requiredValue;
            case UpgradeComparisonType.LessThanOrEqual:
                return currentValue <= requiredValue;
            case UpgradeComparisonType.GreaterThan:
                return currentValue > requiredValue;
            case UpgradeComparisonType.LessThan:
                return currentValue < requiredValue;
            default:
                return false;
        }
    }
    
    /// <summary>
    /// Queues an immediate upgrade
    /// </summary>
    private void QueueUpgrade(CardData cardData, Card card, NetworkEntity entity)
    {
        var queuedUpgrade = new QueuedUpgrade
        {
            baseCardData = cardData,
            upgradedCardData = cardData.UpgradedVersion,
            card = card,
            entity = entity,
            timestamp = Time.time
        };
        
        // Queue for immediate processing (only timing type supported)
        immediateUpgrades.Add(queuedUpgrade);
        
        Debug.Log($"CardUpgradeManager: Queued immediate upgrade for {cardData.CardName} -> {cardData.UpgradedVersion.CardName}");
    }
    
    /// <summary>
    /// Processes immediate upgrades
    /// </summary>
    private void ProcessImmediateUpgrades()
    {
        foreach (var upgrade in immediateUpgrades)
        {
            ExecuteUpgrade(upgrade);
        }
        immediateUpgrades.Clear();
    }
    
    // Removed timing-specific upgrade processing methods since only immediate upgrades are supported
    
    /// <summary>
    /// Executes a queued upgrade
    /// </summary>
    private void ExecuteUpgrade(QueuedUpgrade upgrade)
    {
        if (upgrade.card?.CardData == null || upgrade.entity == null || upgrade.baseCardData == null || upgrade.upgradedCardData == null)
        {
            Debug.LogError("CardUpgradeManager: Cannot execute upgrade - missing card data or entity");
            return;
        }
        
        int cardId = upgrade.card.CardData.CardId;
        int entityId = upgrade.entity.ObjectId;
        
        // Mark as upgraded
        if (!upgradedCardsThisFight.ContainsKey(entityId))
        {
            upgradedCardsThisFight[entityId] = new HashSet<int>();
        }
        upgradedCardsThisFight[entityId].Add(cardId);
        
        // Execute in-fight upgrade
        ExecuteInFightUpgrade(upgrade);
        
        // Execute persistent upgrade
        ExecutePersistentUpgrade(upgrade);
        
        // Trigger event
        OnCardUpgraded?.Invoke(upgrade.baseCardData, upgrade.upgradedCardData, upgrade.entity);
        
        Debug.Log($"CardUpgradeManager: Executed upgrade {upgrade.baseCardData.CardName} -> {upgrade.upgradedCardData.CardName} for {upgrade.entity.EntityName.Value}");
    }
    
    /// <summary>
    /// Executes in-fight card upgrade (updates cards in hand/play)
    /// </summary>
    private void ExecuteInFightUpgrade(QueuedUpgrade upgrade)
    {
        // This would integrate with HandManager to replace card instances
        // For now, we'll just log the action
        Debug.Log($"CardUpgradeManager: In-fight upgrade executed for {upgrade.card.CardData.CardName}");
    }
    
    /// <summary>
    /// Executes persistent deck upgrade
    /// </summary>
    private void ExecutePersistentUpgrade(QueuedUpgrade upgrade)
    {
        var entityDeck = upgrade.entity.GetComponent<NetworkEntityDeck>();
        
        if (entityDeck == null)
        {
            Debug.LogError("CardUpgradeManager: Cannot execute persistent upgrade - missing NetworkEntityDeck component");
            return;
        }
        
        int baseCardId = upgrade.baseCardData.CardId;
        int upgradedCardId = upgrade.upgradedCardData.CardId;
        
        if (upgrade.baseCardData.UpgradeAllCopies)
        {
            // Replace all copies of the base card with the upgraded version
            int replacedCount = entityDeck.ReplaceCard(baseCardId, upgradedCardId);
            Debug.Log($"CardUpgradeManager: Replaced {replacedCount} copies of {upgrade.baseCardData.CardName} with {upgrade.upgradedCardData.CardName}");
        }
        else
        {
            // Replace only one copy
            bool replaced = entityDeck.ReplaceSingleCard(baseCardId, upgradedCardId);
            if (replaced)
            {
                Debug.Log($"CardUpgradeManager: Replaced one copy of {upgrade.baseCardData.CardName} with {upgrade.upgradedCardData.CardName}");
            }
            else
            {
                Debug.LogWarning($"CardUpgradeManager: Failed to replace {upgrade.baseCardData.CardName} - card not found in deck");
            }
        }
    }
    
    /// <summary>
    /// Checks if a card has been upgraded this fight
    /// </summary>
    private bool HasBeenUpgraded(int entityId, int cardId)
    {
        return upgradedCardsThisFight.ContainsKey(entityId) && upgradedCardsThisFight[entityId].Contains(cardId);
    }
    
    /// <summary>
    /// Resets fight-specific tracking data
    /// </summary>
    private void ResetFightTracking()
    {
        upgradedCardsThisFight.Clear();
        turnPlayCounts.Clear();
        consecutiveTurnCounts.Clear();
        immediateUpgrades.Clear();
    }
    
    /// <summary>
    /// Gets persistent play count for a card
    /// </summary>
    public int GetPersistentPlayCount(int cardId)
    {
        return persistentPlayCounts.ContainsKey(cardId) ? persistentPlayCounts[cardId] : 0;
    }
    
    /// <summary>
    /// Gets persistent win count for a card
    /// </summary>
    public int GetPersistentWinCount(int cardId)
    {
        return persistentWinCounts.ContainsKey(cardId) ? persistentWinCounts[cardId] : 0;
    }
    
    /// <summary>
    /// Gets a list of all cards that have available upgrades
    /// </summary>
    public List<int> GetCardsWithAvailableUpgrades()
    {
        var upgradeableCards = GetUpgradeableCards();
        return upgradeableCards.Select(card => card.CardId).ToList();
    }
    
    /// <summary>
    /// Checks if a specific card has available upgrades
    /// </summary>
    public bool HasAvailableUpgrades(int cardId)
    {
        var upgradeableCards = GetUpgradeableCards();
        return upgradeableCards.Any(card => card.CardId == cardId);
    }
    
    /// <summary>
    /// Gets upgrade progress for a card (for UI display)
    /// </summary>
    public string GetUpgradeProgress(int cardId, NetworkEntity entity)
    {
        var upgradeableCards = GetUpgradeableCards();
        var relevantCard = upgradeableCards.FirstOrDefault(card => card.CardId == cardId);
        
        if (relevantCard == null)
        {
            return "No upgrades available";
        }
        
        // For progress tracking, we don't actually need a Card instance
        // We can get the condition value directly from the tracking systems
        int currentValue = GetConditionValueForProgress(relevantCard.UpgradeConditionType, relevantCard.CardId, entity);
        
        return $"{currentValue}/{relevantCard.UpgradeRequiredValue} ({relevantCard.UpgradeConditionType})";
    }
    
    /// <summary>
    /// Gets condition value for progress tracking without requiring a Card instance
    /// </summary>
    private int GetConditionValueForProgress(UpgradeConditionType conditionType, int cardId, NetworkEntity entity)
    {
        int entityId = entity.ObjectId;
        var entityTracker = entity.GetComponent<EntityTracker>();
        
        switch (conditionType)
        {
            case UpgradeConditionType.TimesPlayedThisFight:
                // We can't easily track this without a CardTracker, so return 0 for progress
                return 0;
                
            case UpgradeConditionType.TimesPlayedAcrossFights:
                return persistentPlayCounts.ContainsKey(cardId) ? persistentPlayCounts[cardId] : 0;
                
            case UpgradeConditionType.DamageDealtThisFight:
                return entityTracker?.TrackingData.damageDealtThisFight ?? 0;
                
            case UpgradeConditionType.DamageDealtInSingleTurn:
                return entityTracker?.TrackingData.damageDealtLastRound ?? 0;
                
            case UpgradeConditionType.DamageTakenThisFight:
                return entityTracker?.TrackingData.damageTakenThisFight ?? 0;
                
            case UpgradeConditionType.HealingGivenThisFight:
                return entityTracker?.TrackingData.healingGivenThisFight ?? 0;
                
            case UpgradeConditionType.HealingReceivedThisFight:
                return entityTracker?.TrackingData.healingReceivedThisFight ?? 0;
                
            case UpgradeConditionType.WonFightUsingCard:
                return persistentWinCounts.ContainsKey(cardId) ? persistentWinCounts[cardId] : 0;
                
            case UpgradeConditionType.ZeroCostCardsThisTurn:
                return turnPlayCounts.ContainsKey(entityId) ? 
                    turnPlayCounts[entityId].Where(kvp => kvp.Key == 0).Sum(kvp => kvp.Value) : 0; // Simplified check
                
            case UpgradeConditionType.ComboCountReached:
                return entityTracker?.ComboCount ?? 0;
                
            // For other condition types, return 0 as they require more complex tracking
            default:
                return 0;
        }
    }
}

/// <summary>
/// Data structure for queued upgrades
/// </summary>
[System.Serializable]
public class QueuedUpgrade
{
    public CardData baseCardData;
    public CardData upgradedCardData;
    public Card card;
    public NetworkEntity entity;
    public float timestamp;
} 