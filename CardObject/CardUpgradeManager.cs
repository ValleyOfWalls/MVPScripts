using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using System.Linq;
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
    
    // Card-specific lifetime tracking
    private readonly SyncDictionary<int, int> persistentDrawCounts = new SyncDictionary<int, int>(); // cardId -> draws across fights
    private readonly SyncDictionary<int, int> persistentHeldAtTurnEndCounts = new SyncDictionary<int, int>(); // cardId -> held at turn end
    private readonly SyncDictionary<int, int> persistentDiscardCounts = new SyncDictionary<int, int>(); // cardId -> manual discards
    private readonly SyncDictionary<int, int> persistentFinalCardCounts = new SyncDictionary<int, int>(); // cardId -> times as final card
    private readonly SyncDictionary<int, int> persistentBackToBackCounts = new SyncDictionary<int, int>(); // cardId -> back-to-back plays
    private readonly SyncDictionary<int, int> persistentSoloPlayCounts = new SyncDictionary<int, int>(); // cardId -> solo plays
    
    // Entity-specific lifetime tracking
    private readonly SyncDictionary<int, int> persistentEntityFightsWon = new SyncDictionary<int, int>(); // entityId -> fights won
    private readonly SyncDictionary<int, int> persistentEntityFightsLost = new SyncDictionary<int, int>(); // entityId -> fights lost
    private readonly SyncDictionary<int, int> persistentEntityBattleTurns = new SyncDictionary<int, int>(); // entityId -> total battle turns
    private readonly SyncDictionary<int, int> persistentEntityPerfectTurns = new SyncDictionary<int, int>(); // entityId -> perfect turns
    private readonly SyncDictionary<int, int> persistentEntityStatusEffectsSurvived = new SyncDictionary<int, int>(); // entityId -> unique status effects survived
    
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
        
        Debug.Log("[CARD_UPGRADE] CardUpgradeManager started on server");
        
        // Validate all upgrade data
        ValidateUpgradeData();
        
        // Subscribe to game events
        SubscribeToGameEvents();
        
        // Subscribe to entity spawning events for automatic event subscription
        NetworkManager.ServerManager.OnRemoteConnectionState += OnClientConnectionStateChanged;
    }
    
    /// <summary>
    /// Called when client connection state changes - used to subscribe to new entities
    /// </summary>
    private void OnClientConnectionStateChanged(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            // Delay subscription to allow entities to fully spawn
            StartCoroutine(DelayedEntitySubscription());
        }
    }
    
    /// <summary>
    /// Delayed subscription to new entities after they spawn
    /// </summary>
    private System.Collections.IEnumerator DelayedEntitySubscription()
    {
        yield return new WaitForSeconds(1.0f); // Wait for entities to spawn
        
        // Find any new entities and subscribe to their events
        NetworkEntity[] allEntities = FindObjectsOfType<NetworkEntity>();
        foreach (var entity in allEntities)
        {
            SubscribeToEntityEvents(entity);
        }
        
        Debug.Log($"[CARD_UPGRADE] Subscribed to {allEntities.Length} entities after client connection");
    }
    
    public override void OnStopServer()
    {
        base.OnStopServer();
        UnsubscribeFromGameEvents();
        
        // Unsubscribe from connection events
        if (NetworkManager.ServerManager != null)
        {
            NetworkManager.ServerManager.OnRemoteConnectionState -= OnClientConnectionStateChanged;
        }
    }
    
    /// <summary>
    /// Validates all upgrade data configurations
    /// </summary>
    private void ValidateUpgradeData()
    {
        var availableUpgrades = GetUpgradeableCards();
        
        int validCount = availableUpgrades.Count;
        Debug.Log($"[CARD_UPGRADE] Loaded {validCount} cards with upgrade conditions");
        
        // Log each upgradeable card for debugging
        foreach (var card in availableUpgrades)
        {
            Debug.Log($"[CARD_UPGRADE]   - {card.CardName} → {card.UpgradedVersion.CardName} (Condition: {card.UpgradeConditionType} {card.UpgradeComparisonType} {card.UpgradeRequiredValue})");
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
        Debug.Log("[CARD_UPGRADE] Subscribing to game events for comprehensive upgrade checking");
        
        // Find all entities in the scene and subscribe to their events
        NetworkEntity[] allEntities = FindObjectsOfType<NetworkEntity>();
        foreach (var entity in allEntities)
        {
            SubscribeToEntityEvents(entity);
        }
    }
    
    /// <summary>
    /// Subscribe to a specific entity's events
    /// </summary>
    private void SubscribeToEntityEvents(NetworkEntity entity)
    {
        if (entity == null) return;
        
        // Subscribe to EntityTracker events
        EntityTracker entityTracker = entity.GetComponent<EntityTracker>();
        if (entityTracker != null)
        {
            entityTracker.OnDamageDealt += (amount) => OnDamageDealt(entity, amount);
            entityTracker.OnDamageTaken += (amount) => OnDamageTaken(entity, amount);
            entityTracker.OnHealingGiven += (amount) => OnHealingGiven(entity, amount);
            entityTracker.OnHealingReceived += (amount) => OnHealingReceived(entity, amount);
            entityTracker.OnStanceChanged += (oldStance, newStance) => OnStanceChanged(entity, oldStance, newStance);
            entityTracker.OnComboChanged += (comboCount) => OnComboChanged(entity, comboCount);
            entityTracker.OnStrengthChanged += (strengthStacks) => OnStrengthChanged(entity, strengthStacks);
        }
        
        // Subscribe to LifeHandler events for more detailed health tracking
        LifeHandler lifeHandler = entity.GetComponent<LifeHandler>();
        if (lifeHandler != null)
        {
            lifeHandler.OnDamageTaken += (amount, source) => OnDetailedDamageTaken(entity, amount, source);
            lifeHandler.OnHealingReceived += (amount, source) => OnDetailedHealingReceived(entity, amount, source);
        }
        
        // Subscribe to health changes for low/high health conditions
        entity.CurrentHealth.OnChange += (prev, next, asServer) => OnHealthChanged(entity, prev, next);
        
        Debug.Log($"[CARD_UPGRADE] Subscribed to events for entity: {entity.EntityName.Value}");
    }
    
    /// <summary>
    /// Unsubscribe from game events
    /// </summary>
    private void UnsubscribeFromGameEvents()
    {
        // Find all entities and unsubscribe from their events
        NetworkEntity[] allEntities = FindObjectsOfType<NetworkEntity>();
        foreach (var entity in allEntities)
        {
            UnsubscribeFromEntityEvents(entity);
        }
    }
    
    /// <summary>
    /// Unsubscribe from a specific entity's events
    /// </summary>
    private void UnsubscribeFromEntityEvents(NetworkEntity entity)
    {
        if (entity == null) return;
        
        // Unsubscribe from EntityTracker events
        EntityTracker entityTracker = entity.GetComponent<EntityTracker>();
        if (entityTracker != null)
        {
            entityTracker.OnDamageDealt -= (amount) => OnDamageDealt(entity, amount);
            entityTracker.OnDamageTaken -= (amount) => OnDamageTaken(entity, amount);
            entityTracker.OnHealingGiven -= (amount) => OnHealingGiven(entity, amount);
            entityTracker.OnHealingReceived -= (amount) => OnHealingReceived(entity, amount);
            entityTracker.OnStanceChanged -= (oldStance, newStance) => OnStanceChanged(entity, oldStance, newStance);
            entityTracker.OnComboChanged -= (comboCount) => OnComboChanged(entity, comboCount);
            entityTracker.OnStrengthChanged -= (strengthStacks) => OnStrengthChanged(entity, strengthStacks);
        }
        
        // Unsubscribe from LifeHandler events
        LifeHandler lifeHandler = entity.GetComponent<LifeHandler>();
        if (lifeHandler != null)
        {
            lifeHandler.OnDamageTaken -= (amount, source) => OnDetailedDamageTaken(entity, amount, source);
            lifeHandler.OnHealingReceived -= (amount, source) => OnDetailedHealingReceived(entity, amount, source);
        }
        
        // Unsubscribe from health changes
        entity.CurrentHealth.OnChange -= (prev, next, asServer) => OnHealthChanged(entity, prev, next);
    }
    
    // ═══════════════════════════════════════════════════════════════
    // EVENT HANDLERS FOR UPGRADE CHECKING
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Called when an entity deals damage - checks for damage-based upgrade conditions
    /// </summary>
    [Server]
    private void OnDamageDealt(NetworkEntity entity, int amount)
    {
        if (!IsServerInitialized || entity == null) return;
        
        Debug.Log($"[CARD_UPGRADE] OnDamageDealt: {entity.EntityName.Value} dealt {amount} damage");
        
        // Check upgrades for all cards that might be affected by damage dealt
        CheckAllCardsForUpgradeConditions(entity, new UpgradeConditionType[] {
            UpgradeConditionType.DamageDealtThisFight,
            UpgradeConditionType.DamageDealtInSingleTurn
        });
    }
    
    /// <summary>
    /// Called when an entity takes damage - checks for damage-based upgrade conditions
    /// </summary>
    [Server]
    private void OnDamageTaken(NetworkEntity entity, int amount)
    {
        if (!IsServerInitialized || entity == null) return;
        
        Debug.Log($"[CARD_UPGRADE] OnDamageTaken: {entity.EntityName.Value} took {amount} damage");
        
        // Check upgrades for all cards that might be affected by damage taken
        CheckAllCardsForUpgradeConditions(entity, new UpgradeConditionType[] {
            UpgradeConditionType.DamageTakenThisFight
        });
    }
    
    /// <summary>
    /// Called when an entity gives healing - checks for healing-based upgrade conditions
    /// </summary>
    [Server]
    private void OnHealingGiven(NetworkEntity entity, int amount)
    {
        if (!IsServerInitialized || entity == null) return;
        
        Debug.Log($"[CARD_UPGRADE] OnHealingGiven: {entity.EntityName.Value} gave {amount} healing");
        
        // Check upgrades for all cards that might be affected by healing given
        CheckAllCardsForUpgradeConditions(entity, new UpgradeConditionType[] {
            UpgradeConditionType.HealingGivenThisFight
        });
    }
    
    /// <summary>
    /// Called when an entity receives healing - checks for healing-based upgrade conditions
    /// </summary>
    [Server]
    private void OnHealingReceived(NetworkEntity entity, int amount)
    {
        if (!IsServerInitialized || entity == null) return;
        
        Debug.Log($"[CARD_UPGRADE] OnHealingReceived: {entity.EntityName.Value} received {amount} healing");
        
        // Check upgrades for all cards that might be affected by healing received
        CheckAllCardsForUpgradeConditions(entity, new UpgradeConditionType[] {
            UpgradeConditionType.HealingReceivedThisFight
        });
    }
    
    /// <summary>
    /// Called when an entity changes stance - checks for stance-based upgrade conditions
    /// </summary>
    [Server]
    private void OnStanceChanged(NetworkEntity entity, StanceType oldStance, StanceType newStance)
    {
        if (!IsServerInitialized || entity == null) return;
        
        Debug.Log($"[CARD_UPGRADE] OnStanceChanged: {entity.EntityName.Value} changed stance from {oldStance} to {newStance}");
        
        // Check upgrades for all cards that might be affected by stance changes
        CheckAllCardsForUpgradeConditions(entity, new UpgradeConditionType[] {
            UpgradeConditionType.PlayedInStance
        });
    }
    
    /// <summary>
    /// Called when an entity's combo count changes - checks for combo-based upgrade conditions
    /// </summary>
    [Server]
    private void OnComboChanged(NetworkEntity entity, int comboCount)
    {
        if (!IsServerInitialized || entity == null) return;
        
        Debug.Log($"[CARD_UPGRADE] OnComboChanged: {entity.EntityName.Value} has {comboCount} combo");
        
        // Check upgrades for all cards that might be affected by combo count
        CheckAllCardsForUpgradeConditions(entity, new UpgradeConditionType[] {
            UpgradeConditionType.ComboCountReached,
            UpgradeConditionType.PlayedWithCombo
        });
    }
    
    /// <summary>
    /// Called when an entity's strength changes - checks for strength-based upgrade conditions
    /// </summary>
    [Server]
    private void OnStrengthChanged(NetworkEntity entity, int strengthStacks)
    {
        if (!IsServerInitialized || entity == null) return;
        
        Debug.Log($"[CARD_UPGRADE] OnStrengthChanged: {entity.EntityName.Value} has {strengthStacks} strength stacks");
        
        // Could add strength-based upgrade conditions in the future
        // For now, this is just logged for potential future use
    }
    
    /// <summary>
    /// Called when an entity's health changes - checks for health-based upgrade conditions
    /// </summary>
    [Server]
    private void OnHealthChanged(NetworkEntity entity, int previousHealth, int newHealth)
    {
        if (!IsServerInitialized || entity == null) return;
        
        Debug.Log($"[CARD_UPGRADE] OnHealthChanged: {entity.EntityName.Value} health changed from {previousHealth} to {newHealth}");
        
        // Check if entity is now at low/high health and trigger relevant upgrades
        CheckAllCardsForUpgradeConditions(entity, new UpgradeConditionType[] {
            UpgradeConditionType.PlayedAtLowHealth,
            UpgradeConditionType.PlayedAtHighHealth,
            UpgradeConditionType.PlayedAtHalfHealth
        });
    }
    
    /// <summary>
    /// Called when an entity takes damage with source info - checks for detailed damage conditions
    /// </summary>
    [Server]
    private void OnDetailedDamageTaken(NetworkEntity entity, int amount, NetworkEntity source)
    {
        if (!IsServerInitialized || entity == null) return;
        
        Debug.Log($"[CARD_UPGRADE] OnDetailedDamageTaken: {entity.EntityName.Value} took {amount} damage from {(source != null ? source.EntityName.Value : "unknown")}");
        
        // This provides more detailed damage information for future upgrade conditions
    }
    
    /// <summary>
    /// Called when an entity receives healing with source info - checks for detailed healing conditions
    /// </summary>
    [Server]
    private void OnDetailedHealingReceived(NetworkEntity entity, int amount, NetworkEntity source)
    {
        if (!IsServerInitialized || entity == null) return;
        
        Debug.Log($"[CARD_UPGRADE] OnDetailedHealingReceived: {entity.EntityName.Value} received {amount} healing from {(source != null ? source.EntityName.Value : "unknown")}");
        
        // This provides more detailed healing information for future upgrade conditions
    }
    
    /// <summary>
    /// Called when a card is discarded - checks for discard-based upgrade conditions
    /// </summary>
    [Server]
    public void OnCardDiscarded(Card card, NetworkEntity entity, bool wasManualDiscard)
    {
        if (!IsServerInitialized || card?.CardData == null || entity == null) return;
        
        Debug.Log($"[CARD_UPGRADE] OnCardDiscarded: {card.CardData.CardName} discarded by {entity.EntityName.Value} (manual: {wasManualDiscard})");
        
        if (wasManualDiscard)
        {
            int cardId = card.CardData.CardId;
            UpdatePersistentDiscardCount(cardId);
            
            // Check upgrades for all cards that might be affected by manual discards
            CheckAllCardsForUpgradeConditions(entity, new UpgradeConditionType[] {
                UpgradeConditionType.DiscardedManually,
                UpgradeConditionType.DiscardedManuallyLifetime
            });
        }
    }
    
    /// <summary>
    /// Called when a card is held at turn end - checks for turn-end-based upgrade conditions
    /// </summary>
    [Server]
    public void OnCardHeldAtTurnEnd(Card card, NetworkEntity entity)
    {
        if (!IsServerInitialized || card?.CardData == null || entity == null) return;
        
        Debug.Log($"[CARD_UPGRADE] OnCardHeldAtTurnEnd: {card.CardData.CardName} held at turn end by {entity.EntityName.Value}");
        
        int cardId = card.CardData.CardId;
        UpdatePersistentHeldAtTurnEndCount(cardId);
        
        // Check upgrades for all cards that might be affected by being held at turn end
        CheckAllCardsForUpgradeConditions(entity, new UpgradeConditionType[] {
            UpgradeConditionType.HeldAtTurnEnd,
            UpgradeConditionType.HeldAtTurnEndLifetime
        });
    }
    
    /// <summary>
    /// Called when a card is the final card in hand - checks for final-card-based upgrade conditions
    /// </summary>
    [Server]
    public void OnCardIsFinalInHand(Card card, NetworkEntity entity)
    {
        if (!IsServerInitialized || card?.CardData == null || entity == null) return;
        
        Debug.Log($"[CARD_UPGRADE] OnCardIsFinalInHand: {card.CardData.CardName} is final card in hand for {entity.EntityName.Value}");
        
        int cardId = card.CardData.CardId;
        UpdatePersistentFinalCardCount(cardId);
        
        // Check upgrades for all cards that might be affected by being the final card
        CheckAllCardsForUpgradeConditions(entity, new UpgradeConditionType[] {
            UpgradeConditionType.FinalCardInHand,
            UpgradeConditionType.FinalCardInHandLifetime
        });
    }
    
    /// <summary>
    /// Called when a status effect is applied to an entity - checks for status-based upgrade conditions
    /// </summary>
    [Server]
    public void OnStatusEffectApplied(NetworkEntity entity, string statusEffectName)
    {
        if (!IsServerInitialized || entity == null) return;
        
        Debug.Log($"[CARD_UPGRADE] OnStatusEffectApplied: {statusEffectName} applied to {entity.EntityName.Value}");
        
        // Record that the entity survived this status effect
        EntityTracker entityTracker = entity.GetComponent<EntityTracker>();
        if (entityTracker != null)
        {
            entityTracker.RecordSurvivedStatusEffect(statusEffectName);
        }
        
        // Check upgrades for all cards that might be affected by status effects
        CheckAllCardsForUpgradeConditions(entity, new UpgradeConditionType[] {
            UpgradeConditionType.SurvivedStatusEffect,
            UpgradeConditionType.TotalStatusEffectsSurvived
        });
    }
    
    /// <summary>
    /// Called when an entity wins or loses a fight - checks for victory/defeat-based upgrade conditions
    /// </summary>
    [Server]
    public void OnFightOutcome(NetworkEntity entity, bool victory, List<Card> cardsUsedInFight)
    {
        if (!IsServerInitialized || entity == null) return;
        
        Debug.Log($"[CARD_UPGRADE] OnFightOutcome: {entity.EntityName.Value} {(victory ? "won" : "lost")} the fight");
        
        // Update persistent tracking for fight outcomes
        int entityId = entity.ObjectId;
        
        if (persistentEntityFightsWon.ContainsKey(entityId))
        {
            if (victory) persistentEntityFightsWon[entityId]++;
        }
        else
        {
            persistentEntityFightsWon[entityId] = victory ? 1 : 0;
        }
        
        if (persistentEntityFightsLost.ContainsKey(entityId))
        {
            if (!victory) persistentEntityFightsLost[entityId]++;
        }
        else
        {
            persistentEntityFightsLost[entityId] = victory ? 0 : 1;
        }
        
        // Check for fight outcome-based upgrades
        CheckAllCardsForUpgradeConditions(entity, new UpgradeConditionType[] {
            UpgradeConditionType.WonFightUsingCard,
            UpgradeConditionType.LostFightWithCard,
            UpgradeConditionType.SurvivedFightWithCard,
            UpgradeConditionType.TotalFightsWon,
            UpgradeConditionType.TotalFightsLost
        });
        
        // Check for specific cards that were used to win/lose the fight
        if (cardsUsedInFight != null)
        {
            foreach (var card in cardsUsedInFight)
            {
                if (card?.CardData != null)
                {
                    CheckUpgradeConditions(card, entity);
                }
            }
        }
    }
    
    /// <summary>
    /// Checks all cards for specific upgrade condition types
    /// </summary>
    [Server]
    private void CheckAllCardsForUpgradeConditions(NetworkEntity entity, UpgradeConditionType[] conditionTypes)
    {
        if (!IsServerInitialized || entity == null || conditionTypes == null) return;
        
        // Get all cards in hand for this entity
        CheckAllCardsInHandForUpgrades(entity);
        
        // Also check all cards in deck/discard that might have these conditions
        // This is important for persistent upgrade conditions
        var upgradeableCards = GetUpgradeableCards();
        foreach (var cardData in upgradeableCards)
        {
            foreach (var conditionType in conditionTypes)
            {
                if (cardData.UpgradeConditionType == conditionType)
                {
                    // Create a temporary card reference for evaluation
                    // In a real implementation, you might want to check all actual card instances
                    Debug.Log($"[CARD_UPGRADE] Checking condition {conditionType} for {cardData.CardName}");
                    
                    // This is a simplified check - in practice you'd want to evaluate against all instances
                    int currentValue = GetConditionValueForProgress(conditionType, cardData.CardId, entity);
                    bool conditionMet = CompareValues(currentValue, cardData.UpgradeRequiredValue, cardData.UpgradeComparisonType);
                    
                    if (conditionMet)
                    {
                        Debug.Log($"[CARD_UPGRADE] Condition met for {cardData.CardName}: {currentValue} {cardData.UpgradeComparisonType} {cardData.UpgradeRequiredValue}");
                        // Would need to find actual card instances to upgrade
                    }
                }
            }
        }
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
        
        Debug.Log($"[CARD_UPGRADE] OnCardPlayed called for {card.CardData.CardName} (ID: {cardId}) by {entity.EntityName.Value}");
        
        // Update persistent tracking
        UpdatePersistentTracking(cardId);
        
        // Update per-turn tracking
        UpdateTurnTracking(entityId, cardId);
        
        // Check for upgrades on the played card
        CheckUpgradeConditions(card, entity);
        
        // Check for play-specific upgrade conditions across all cards
        CheckAllCardsForUpgradeConditions(entity, new UpgradeConditionType[] {
            UpgradeConditionType.TimesPlayedThisFight,
            UpgradeConditionType.TimesPlayedAcrossFights,
            UpgradeConditionType.PlayedMultipleTimesInTurn,
            UpgradeConditionType.PlayedOnConsecutiveTurns,
            UpgradeConditionType.PlayedWithCombo,
            UpgradeConditionType.PlayedAsFinisher,
            UpgradeConditionType.PlayedAtLowHealth,
            UpgradeConditionType.PlayedAtHighHealth,
            UpgradeConditionType.PlayedAtHalfHealth,
            UpgradeConditionType.ZeroCostCardsThisTurn,
            UpgradeConditionType.ZeroCostCardsThisFight,
            UpgradeConditionType.ComboUseBackToBack,
            UpgradeConditionType.OnlyCardPlayedThisTurn
        });
        
        // Process immediate upgrades
        ProcessImmediateUpgrades();
    }
    
    /// <summary>
    /// Called when a card is drawn - checks for upgrades based on hand composition
    /// </summary>
    [Server]
    public void OnCardDrawn(Card card, NetworkEntity entity)
    {
        if (!IsServerInitialized || card?.CardData == null || entity == null) return;
        
        int cardId = card.CardData.CardId;
        
        Debug.Log($"[CARD_UPGRADE] OnCardDrawn called for {card.CardData.CardName} (ID: {cardId}) by {entity.EntityName.Value}");
        
        // Force update of card tracking data to reflect current hand state
        CardTracker cardTracker = card.GetComponent<CardTracker>();
        if (cardTracker != null)
        {
            cardTracker.UpdateTrackingData();
        }
        
        // Update persistent draw tracking
        UpdatePersistentDrawCount(cardId);
        
        // Check for upgrades for ALL cards in hand (not just the drawn card)
        // This is important because drawing one card might change the conditions for other cards
        CheckAllCardsInHandForUpgrades(entity);
        
        // Check for draw-specific upgrade conditions
        CheckAllCardsForUpgradeConditions(entity, new UpgradeConditionType[] {
            UpgradeConditionType.DrawnOften,
            UpgradeConditionType.DrawnOftenLifetime,
            UpgradeConditionType.CopiesInHand,
            UpgradeConditionType.CopiesInDeck,
            UpgradeConditionType.CopiesInDiscard
        });
        
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
        
        Debug.Log($"[CARD_UPGRADE] OnTurnEnd called for {entity.EntityName.Value}");
        
        // Check for cards held at turn end and update their tracking
        HandManager handManager = entity.GetComponent<HandManager>();
        if (handManager != null)
        {
            var cardsInHand = handManager.GetCardsInHand();
            foreach (var cardGameObject in cardsInHand)
            {
                if (cardGameObject != null)
                {
                    Card cardComponent = cardGameObject.GetComponent<Card>();
                    if (cardComponent?.CardData != null)
                    {
                        OnCardHeldAtTurnEnd(cardComponent, entity);
                    }
                }
            }
            
            // Check if any card is the final card in hand
            if (cardsInHand.Count == 1 && cardsInHand[0] != null)
            {
                Card finalCardComponent = cardsInHand[0].GetComponent<Card>();
                if (finalCardComponent?.CardData != null)
                {
                    OnCardIsFinalInHand(finalCardComponent, entity);
                }
            }
        }
        
        // Check for turn-end based upgrade conditions
        CheckAllCardsForUpgradeConditions(entity, new UpgradeConditionType[] {
            UpgradeConditionType.HeldAtTurnEnd,
            UpgradeConditionType.HeldAtTurnEndLifetime,
            UpgradeConditionType.FinalCardInHand,
            UpgradeConditionType.FinalCardInHandLifetime,
            UpgradeConditionType.OnlyCardPlayedThisTurn,
            UpgradeConditionType.OnlyCardPlayedInTurnLifetime,
            UpgradeConditionType.PlayedOnConsecutiveTurns,
            UpgradeConditionType.PlayedMultipleTimesInTurn
        });
        
        // Reset turn tracking
        if (turnPlayCounts.ContainsKey(entityId))
        {
            turnPlayCounts[entityId].Clear();
        }
        
        // Process any upgrades that might have been triggered
        ProcessImmediateUpgrades();
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
    /// Updates persistent card draw count
    /// </summary>
    [Server]
    public void UpdatePersistentDrawCount(int cardId)
    {
        if (!IsServerInitialized) return;
        
        if (persistentDrawCounts.ContainsKey(cardId))
        {
            persistentDrawCounts[cardId]++;
        }
        else
        {
            persistentDrawCounts[cardId] = 1;
        }
    }
    
    /// <summary>
    /// Updates persistent card held at turn end count
    /// </summary>
    [Server]
    public void UpdatePersistentHeldAtTurnEndCount(int cardId)
    {
        if (!IsServerInitialized) return;
        
        if (persistentHeldAtTurnEndCounts.ContainsKey(cardId))
        {
            persistentHeldAtTurnEndCounts[cardId]++;
        }
        else
        {
            persistentHeldAtTurnEndCounts[cardId] = 1;
        }
    }
    
    /// <summary>
    /// Updates persistent card discard count
    /// </summary>
    [Server]
    public void UpdatePersistentDiscardCount(int cardId)
    {
        if (!IsServerInitialized) return;
        
        if (persistentDiscardCounts.ContainsKey(cardId))
        {
            persistentDiscardCounts[cardId]++;
        }
        else
        {
            persistentDiscardCounts[cardId] = 1;
        }
    }
    
    /// <summary>
    /// Updates persistent final card count
    /// </summary>
    [Server]
    public void UpdatePersistentFinalCardCount(int cardId)
    {
        if (!IsServerInitialized) return;
        
        if (persistentFinalCardCounts.ContainsKey(cardId))
        {
            persistentFinalCardCounts[cardId]++;
        }
        else
        {
            persistentFinalCardCounts[cardId] = 1;
        }
    }
    
    /// <summary>
    /// Updates persistent back-to-back play count
    /// </summary>
    [Server]
    public void UpdatePersistentBackToBackCount(int cardId)
    {
        if (!IsServerInitialized) return;
        
        if (persistentBackToBackCounts.ContainsKey(cardId))
        {
            persistentBackToBackCounts[cardId]++;
        }
        else
        {
            persistentBackToBackCounts[cardId] = 1;
        }
    }
    
    /// <summary>
    /// Updates persistent solo play count
    /// </summary>
    [Server]
    public void UpdatePersistentSoloPlayCount(int cardId)
    {
        if (!IsServerInitialized) return;
        
        if (persistentSoloPlayCounts.ContainsKey(cardId))
        {
            persistentSoloPlayCounts[cardId]++;
        }
        else
        {
            persistentSoloPlayCounts[cardId] = 1;
        }
    }
    
    /// <summary>
    /// Updates persistent entity tracking data
    /// </summary>
    [Server]
    public void UpdatePersistentEntityTracking(int entityId, bool victory, int battleTurns, int perfectTurns, int statusEffectsSurvived)
    {
        if (!IsServerInitialized) return;
        
        // Update fights won/lost
        if (victory)
        {
            if (persistentEntityFightsWon.ContainsKey(entityId))
            {
                persistentEntityFightsWon[entityId]++;
            }
            else
            {
                persistentEntityFightsWon[entityId] = 1;
            }
        }
        else
        {
            if (persistentEntityFightsLost.ContainsKey(entityId))
            {
                persistentEntityFightsLost[entityId]++;
            }
            else
            {
                persistentEntityFightsLost[entityId] = 1;
            }
        }
        
        // Update battle turns
        if (persistentEntityBattleTurns.ContainsKey(entityId))
        {
            persistentEntityBattleTurns[entityId] += battleTurns;
        }
        else
        {
            persistentEntityBattleTurns[entityId] = battleTurns;
        }
        
        // Update perfect turns
        if (perfectTurns > 0)
        {
            if (persistentEntityPerfectTurns.ContainsKey(entityId))
            {
                persistentEntityPerfectTurns[entityId] += perfectTurns;
            }
            else
            {
                persistentEntityPerfectTurns[entityId] = perfectTurns;
            }
        }
        
        // Update status effects survived
        if (statusEffectsSurvived > 0)
        {
            if (persistentEntityStatusEffectsSurvived.ContainsKey(entityId))
            {
                persistentEntityStatusEffectsSurvived[entityId] += statusEffectsSurvived;
            }
            else
            {
                persistentEntityStatusEffectsSurvived[entityId] = statusEffectsSurvived;
            }
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
        
        Debug.Log($"[CARD_UPGRADE] Checking upgrade conditions for {cardData.CardName}");
        
        // Check if this card can upgrade
        if (!cardData.CanUpgrade || cardData.UpgradedVersion == null)
        {
            Debug.Log($"[CARD_UPGRADE] {cardData.CardName} cannot upgrade (CanUpgrade: {cardData.CanUpgrade}, HasUpgradedVersion: {cardData.UpgradedVersion != null})");
            return;
        }
            
        // Skip if already upgraded (each card can only upgrade once)
        if (HasBeenUpgraded(entity.ObjectId, cardId))
        {
            Debug.Log($"[CARD_UPGRADE] {cardData.CardName} already upgraded this fight");
            return;
        }
        
        Debug.Log($"[CARD_UPGRADE] {cardData.CardName} can upgrade to {cardData.UpgradedVersion.CardName}. Checking condition: {cardData.UpgradeConditionType} {cardData.UpgradeComparisonType} {cardData.UpgradeRequiredValue}");
        
        // Check if upgrade condition is met
        if (EvaluateUpgradeCondition(cardData, card, entity))
        {
            Debug.Log($"[CARD_UPGRADE] ✓ Upgrade condition met! Queueing upgrade for {cardData.CardName}");
            QueueUpgrade(cardData, card, entity);
        }
        else
        {
            Debug.Log($"[CARD_UPGRADE] ✗ Upgrade condition not met for {cardData.CardName}");
        }
    }
    
    /// <summary>
    /// Checks upgrade conditions for all cards in an entity's hand
    /// </summary>
    private void CheckAllCardsInHandForUpgrades(NetworkEntity entity)
    {
        Debug.Log($"[CARD_UPGRADE] Checking all cards in hand for upgrades for {entity.EntityName.Value}");
        
        NetworkEntity handEntity = null;
        NetworkEntity mainEntity = null;
        
        // Check if the entity is already a Hand entity
        if (entity.EntityType == EntityType.PlayerHand || entity.EntityType == EntityType.PetHand)
        {
            handEntity = entity;
            
            // Find the main entity (Player/Pet) that owns this hand
            var allEntities = FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
            foreach (var ent in allEntities)
            {
                if (ent.EntityType == EntityType.Player || ent.EntityType == EntityType.Pet)
                {
                    var relationshipManager = ent.GetComponent<RelationshipManager>();
                    if (relationshipManager?.HandEntity != null)
                    {
                        var handNetworkEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
                        if (handNetworkEntity != null && handNetworkEntity.ObjectId == handEntity.ObjectId)
                        {
                            mainEntity = ent;
                            break;
                        }
                    }
                }
            }
            
            Debug.Log($"[CARD_UPGRADE] Using hand entity directly: {handEntity.EntityName.Value}, main entity: {mainEntity?.EntityName.Value ?? "not found"}");
        }
        else
        {
            // Entity is a Player/Pet, find its Hand entity
            var relationshipManager = entity.GetComponent<RelationshipManager>();
            if (relationshipManager?.HandEntity == null)
            {
                Debug.Log($"[CARD_UPGRADE] No hand entity found for {entity.EntityName.Value}");
                return;
            }
            
            handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
            mainEntity = entity;
            
            Debug.Log($"[CARD_UPGRADE] Found hand entity: {handEntity?.EntityName.Value ?? "null"} for main entity: {mainEntity.EntityName.Value}");
        }
        
        if (handEntity == null)
        {
            Debug.Log($"[CARD_UPGRADE] No valid hand entity found for {entity.EntityName.Value}");
            return;
        }
        
        var handManager = handEntity.GetComponent<HandManager>();
        if (handManager == null)
        {
            Debug.Log($"[CARD_UPGRADE] No HandManager found on hand entity {handEntity.EntityName.Value}");
            return;
        }
        
        // Get all cards in hand
        var cardsInHand = handManager.GetCardsInHand();
        Debug.Log($"[CARD_UPGRADE] Found {cardsInHand.Count} cards in hand for {handEntity.EntityName.Value}");
        
        // Use main entity for upgrade checking if available, otherwise fall back to the passed entity
        var entityForUpgradeCheck = mainEntity ?? entity;
        Debug.Log($"[CARD_UPGRADE] Using entity for upgrade checks: {entityForUpgradeCheck.EntityName.Value}");
        
        // Check each card for upgrades
        foreach (var cardObj in cardsInHand)
        {
            var card = cardObj.GetComponent<Card>();
            if (card != null)
            {
                // Update tracking data before checking
                var cardTracker = card.GetComponent<CardTracker>();
                if (cardTracker != null)
                {
                    cardTracker.UpdateTrackingData();
                }
                
                CheckUpgradeConditions(card, entityForUpgradeCheck);
            }
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
        bool result = CompareValues(currentValue, cardData.UpgradeRequiredValue, cardData.UpgradeComparisonType);
        
        Debug.Log($"[CARD_UPGRADE] Current value for {cardData.UpgradeConditionType}: {currentValue}, Required: {cardData.UpgradeRequiredValue} ({cardData.UpgradeComparisonType}), Result: {result}");
        
        return result;
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
                
            case UpgradeConditionType.PlayedAtHalfHealth:
                float healthPercentHalf = (float)entity.CurrentHealth.Value / entity.MaxHealth.Value * 100f;
                return healthPercentHalf <= 50f ? 1 : 0; // Under 50% HP
                
            case UpgradeConditionType.LostFightWithCard:
                return entityTracker?.TrackingData.lostLastFight == true ? 1 : 0;
                
            case UpgradeConditionType.ComboUseBackToBack:
                return cardTracker?.TrackingData.timesPlayedBackToBack ?? 0;
                
            case UpgradeConditionType.DrawnOften:
                return cardTracker?.TrackingData.timesDrawnThisFight ?? 0;
                
            case UpgradeConditionType.HeldAtTurnEnd:
                return cardTracker?.TrackingData.timesHeldAtTurnEnd ?? 0;
                
            case UpgradeConditionType.DiscardedManually:
                return cardTracker?.TrackingData.timesDiscardedManually ?? 0;
                
            case UpgradeConditionType.FinalCardInHand:
                return cardTracker?.TrackingData.timesFinalCardInHand ?? 0;
                
            case UpgradeConditionType.FamiliarNameInDeck:
                return GetFamiliarNameCount(card.CardData, entity);
                
            case UpgradeConditionType.OnlyCardTypeInDeck:
                return IsOnlyCardTypeInDeck(card.CardData, entity) ? 1 : 0;
                
            case UpgradeConditionType.AllCardsCostLowEnough:
                return AllCardsCostLowEnough(entity) ? 1 : 0;
                
            case UpgradeConditionType.DeckSizeBelow:
                return GetDeckSize(entity);
                
            case UpgradeConditionType.SurvivedStatusEffect:
                return entityTracker?.TrackingData.survivedStatusEffects.Count ?? 0;
                
            case UpgradeConditionType.BattleLengthOver:
                return entityTracker?.TrackingData.battleTurnCount ?? 0;
                
            case UpgradeConditionType.PerfectTurnPlayed:
                return entityTracker?.TrackingData.hadPerfectTurnThisFight == true ? 1 : 0;
                
            case UpgradeConditionType.OnlyCardPlayedThisTurn:
                return cardTracker?.TrackingData.timesOnlyCardPlayedInTurn ?? 0;
                
            // Lifetime tracking conditions
            case UpgradeConditionType.DrawnOftenLifetime:
                return persistentDrawCounts.ContainsKey(cardId) ? persistentDrawCounts[cardId] : 0;
                
            case UpgradeConditionType.HeldAtTurnEndLifetime:
                return persistentHeldAtTurnEndCounts.ContainsKey(cardId) ? persistentHeldAtTurnEndCounts[cardId] : 0;
                
            case UpgradeConditionType.DiscardedManuallyLifetime:
                return persistentDiscardCounts.ContainsKey(cardId) ? persistentDiscardCounts[cardId] : 0;
                
            case UpgradeConditionType.FinalCardInHandLifetime:
                return persistentFinalCardCounts.ContainsKey(cardId) ? persistentFinalCardCounts[cardId] : 0;
                
            case UpgradeConditionType.ComboUseBackToBackLifetime:
                return persistentBackToBackCounts.ContainsKey(cardId) ? persistentBackToBackCounts[cardId] : 0;
                
            case UpgradeConditionType.OnlyCardPlayedInTurnLifetime:
                return persistentSoloPlayCounts.ContainsKey(cardId) ? persistentSoloPlayCounts[cardId] : 0;
                
            case UpgradeConditionType.TotalFightsWon:
                int entityIdForWins = entity.ObjectId;
                return persistentEntityFightsWon.ContainsKey(entityIdForWins) ? persistentEntityFightsWon[entityIdForWins] : 0;
                
            case UpgradeConditionType.TotalFightsLost:
                int entityIdForLosses = entity.ObjectId;
                return persistentEntityFightsLost.ContainsKey(entityIdForLosses) ? persistentEntityFightsLost[entityIdForLosses] : 0;
                
            case UpgradeConditionType.TotalBattleTurns:
                int entityIdForTurns = entity.ObjectId;
                return persistentEntityBattleTurns.ContainsKey(entityIdForTurns) ? persistentEntityBattleTurns[entityIdForTurns] : 0;
                
            case UpgradeConditionType.TotalPerfectTurns:
                int entityIdForPerfect = entity.ObjectId;
                return persistentEntityPerfectTurns.ContainsKey(entityIdForPerfect) ? persistentEntityPerfectTurns[entityIdForPerfect] : 0;
                
            case UpgradeConditionType.TotalStatusEffectsSurvived:
                int entityIdForStatus = entity.ObjectId;
                return persistentEntityStatusEffectsSurvived.ContainsKey(entityIdForStatus) ? persistentEntityStatusEffectsSurvived[entityIdForStatus] : 0;
                
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
        
        // Queue upgrade animation, which will handle both in-combat and persistent upgrades
        if (CardUpgrade.CardUpgradeAnimator.Instance != null)
        {
            CardUpgrade.CardUpgradeAnimator.Instance.QueueUpgradeAnimation(
                upgrade.baseCardData, 
                upgrade.upgradedCardData, 
                upgrade.entity, 
                upgrade.baseCardData.UpgradeAllCopies,
                OnUpgradeAnimationComplete
            );
        }
        else
        {
            Debug.LogError("CardUpgradeManager: CardUpgradeAnimator not found! Executing upgrade without animation.");
        
            // Fallback: Execute upgrades without animation
            ExecuteInFightUpgrade(upgrade);
        ExecutePersistentUpgrade(upgrade);
            OnCardUpgraded?.Invoke(upgrade.baseCardData, upgrade.upgradedCardData, upgrade.entity);
        }
        
        Debug.Log($"CardUpgradeManager: Queued upgrade animation {upgrade.baseCardData.CardName} -> {upgrade.upgradedCardData.CardName} for {upgrade.entity.EntityName.Value}");
    }
    
    /// <summary>
    /// Called when the upgrade animation completes
    /// </summary>
    private void OnUpgradeAnimationComplete(CardData baseCard, CardData upgradedCard, NetworkEntity entity, bool upgradeAllCopies)
    {
        // Execute the actual card replacements after animation
        ExecuteInFightUpgrade(baseCard, upgradedCard, entity, upgradeAllCopies);
        ExecutePersistentUpgrade(baseCard, upgradedCard, entity, upgradeAllCopies);
        
        // Trigger event
        OnCardUpgraded?.Invoke(baseCard, upgradedCard, entity);
        
        Debug.Log($"CardUpgradeManager: Completed upgrade {baseCard.CardName} -> {upgradedCard.CardName} for {entity.EntityName.Value}");
    }
    
    /// <summary>
    /// Executes in-fight card upgrade (updates cards in hand/play)
    /// </summary>
    private void ExecuteInFightUpgrade(QueuedUpgrade upgrade)
    {
        ExecuteInFightUpgrade(upgrade.baseCardData, upgrade.upgradedCardData, upgrade.entity, upgrade.baseCardData.UpgradeAllCopies);
    }
    
    /// <summary>
    /// Executes in-fight card upgrade with specified parameters
    /// </summary>
    private void ExecuteInFightUpgrade(CardData baseCard, CardData upgradedCard, NetworkEntity entity, bool upgradeAllCopies)
    {
        // Use the InCombatCardReplacer to replace cards in all combat zones
        if (CardUpgrade.InCombatCardReplacer.Instance != null)
        {
            CardUpgrade.InCombatCardReplacer.Instance.ReplaceCardInAllZones(
                baseCard.CardId, 
                upgradedCard.CardId, 
                entity, 
                upgradeAllCopies
            );
        }
        else
        {
            Debug.LogError("CardUpgradeManager: InCombatCardReplacer not found! In-combat upgrade failed.");
        }
    }
    
    /// <summary>
    /// Executes persistent deck upgrade
    /// </summary>
    private void ExecutePersistentUpgrade(QueuedUpgrade upgrade)
    {
        ExecutePersistentUpgrade(upgrade.baseCardData, upgrade.upgradedCardData, upgrade.entity, upgrade.baseCardData.UpgradeAllCopies);
    }
    
    /// <summary>
    /// Executes persistent deck upgrade with specified parameters
    /// </summary>
    private void ExecutePersistentUpgrade(CardData baseCard, CardData upgradedCard, NetworkEntity entity, bool upgradeAllCopies)
    {
        var entityDeck = entity.GetComponent<NetworkEntityDeck>();
        
        if (entityDeck == null)
        {
            Debug.LogError("CardUpgradeManager: Cannot execute persistent upgrade - missing NetworkEntityDeck component");
            return;
        }
        
        int baseCardId = baseCard.CardId;
        int upgradedCardId = upgradedCard.CardId;
        
        if (upgradeAllCopies)
        {
            // Replace all copies of the base card with the upgraded version
            int replacedCount = entityDeck.ReplaceCard(baseCardId, upgradedCardId);
            Debug.Log($"CardUpgradeManager: Replaced {replacedCount} copies of {baseCard.CardName} with {upgradedCard.CardName}");
        }
        else
        {
            // Replace only one copy
            bool replaced = entityDeck.ReplaceSingleCard(baseCardId, upgradedCardId);
            if (replaced)
            {
                Debug.Log($"CardUpgradeManager: Replaced one copy of {baseCard.CardName} with {upgradedCard.CardName}");
            }
            else
            {
                Debug.LogWarning($"CardUpgradeManager: Failed to replace {baseCard.CardName} - card not found in deck");
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
    /// Gets the number of cards in deck that share a keyword or title fragment with the given card
    /// </summary>
    private int GetFamiliarNameCount(CardData cardData, NetworkEntity entity)
    {
        var entityDeck = entity.GetComponent<NetworkEntityDeck>();
        if (entityDeck == null) return 0;
        
        string cardName = cardData.CardName;
        string[] keywords = cardName.Split(' '); // Simple keyword extraction
        
        var deckCardIds = entityDeck.GetAllCardIds();
        int familiarCount = 0;
        
        foreach (int deckCardId in deckCardIds)
        {
            CardData deckCard = CardDatabase.Instance?.GetCardById(deckCardId);
            if (deckCard == null || deckCard == cardData) continue;
            
            // Check if any keyword from the original card appears in this deck card's name
            foreach (string keyword in keywords)
            {
                if (keyword.Length > 2 && deckCard.CardName.Contains(keyword, System.StringComparison.OrdinalIgnoreCase))
                {
                    familiarCount++;
                    break; // Only count each card once
                }
            }
        }
        
        return familiarCount;
    }
    
    /// <summary>
    /// Checks if this card is the only one of its type in the deck
    /// </summary>
    private bool IsOnlyCardTypeInDeck(CardData cardData, NetworkEntity entity)
    {
        var entityDeck = entity.GetComponent<NetworkEntityDeck>();
        if (entityDeck == null) return false;
        
        var deckCardIds = entityDeck.GetAllCardIds();
        int sameTypeCount = 0;
        
        foreach (int deckCardId in deckCardIds)
        {
            CardData deckCard = CardDatabase.Instance?.GetCardById(deckCardId);
            if (deckCard != null && deckCard.CardType == cardData.CardType)
            {
                sameTypeCount++;
                if (sameTypeCount > 1) return false; // More than one of this type
            }
        }
        
        return sameTypeCount == 1; // Exactly one (this card)
    }
    
    /// <summary>
    /// Checks if all cards in deck cost 1 or less
    /// </summary>
    private bool AllCardsCostLowEnough(NetworkEntity entity)
    {
        var entityDeck = entity.GetComponent<NetworkEntityDeck>();
        if (entityDeck == null) return false;
        
        var deckCardIds = entityDeck.GetAllCardIds();
        
        foreach (int deckCardId in deckCardIds)
        {
            CardData deckCard = CardDatabase.Instance?.GetCardById(deckCardId);
            if (deckCard != null && deckCard.EnergyCost > 1)
            {
                return false; // Found a card that costs more than 1
            }
        }
        
        return true; // All cards cost 1 or less
    }
    
    /// <summary>
    /// Gets the total deck size
    /// </summary>
    private int GetDeckSize(NetworkEntity entity)
    {
        var entityDeck = entity.GetComponent<NetworkEntityDeck>();
        if (entityDeck == null) return 0;
        
        return entityDeck.GetAllCardIds().Count;
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
                
            case UpgradeConditionType.PlayedAtHalfHealth:
                float healthPercentHalf = (float)entity.CurrentHealth.Value / entity.MaxHealth.Value * 100f;
                return healthPercentHalf <= 50f ? 1 : 0;
                
            case UpgradeConditionType.LostFightWithCard:
                return entityTracker?.TrackingData.lostLastFight == true ? 1 : 0;
                
            case UpgradeConditionType.BattleLengthOver:
                return entityTracker?.TrackingData.battleTurnCount ?? 0;
                
            case UpgradeConditionType.PerfectTurnPlayed:
                return entityTracker?.TrackingData.hadPerfectTurnThisFight == true ? 1 : 0;
                
            case UpgradeConditionType.SurvivedStatusEffect:
                return entityTracker?.TrackingData.survivedStatusEffects.Count ?? 0;
                
            case UpgradeConditionType.DeckSizeBelow:
                return GetDeckSize(entity);
                
            case UpgradeConditionType.AllCardsCostLowEnough:
                return AllCardsCostLowEnough(entity) ? 1 : 0;
                
            // Lifetime tracking conditions for progress
            case UpgradeConditionType.DrawnOftenLifetime:
                return persistentDrawCounts.ContainsKey(cardId) ? persistentDrawCounts[cardId] : 0;
                
            case UpgradeConditionType.HeldAtTurnEndLifetime:
                return persistentHeldAtTurnEndCounts.ContainsKey(cardId) ? persistentHeldAtTurnEndCounts[cardId] : 0;
                
            case UpgradeConditionType.DiscardedManuallyLifetime:
                return persistentDiscardCounts.ContainsKey(cardId) ? persistentDiscardCounts[cardId] : 0;
                
            case UpgradeConditionType.FinalCardInHandLifetime:
                return persistentFinalCardCounts.ContainsKey(cardId) ? persistentFinalCardCounts[cardId] : 0;
                
            case UpgradeConditionType.ComboUseBackToBackLifetime:
                return persistentBackToBackCounts.ContainsKey(cardId) ? persistentBackToBackCounts[cardId] : 0;
                
            case UpgradeConditionType.OnlyCardPlayedInTurnLifetime:
                return persistentSoloPlayCounts.ContainsKey(cardId) ? persistentSoloPlayCounts[cardId] : 0;
                
            case UpgradeConditionType.TotalFightsWon:
                int entityIdForWins = entity.ObjectId;
                return persistentEntityFightsWon.ContainsKey(entityIdForWins) ? persistentEntityFightsWon[entityIdForWins] : 0;
                
            case UpgradeConditionType.TotalFightsLost:
                int entityIdForLosses = entity.ObjectId;
                return persistentEntityFightsLost.ContainsKey(entityIdForLosses) ? persistentEntityFightsLost[entityIdForLosses] : 0;
                
            case UpgradeConditionType.TotalBattleTurns:
                int entityIdForTurns = entity.ObjectId;
                return persistentEntityBattleTurns.ContainsKey(entityIdForTurns) ? persistentEntityBattleTurns[entityIdForTurns] : 0;
                
            case UpgradeConditionType.TotalPerfectTurns:
                int entityIdForPerfect = entity.ObjectId;
                return persistentEntityPerfectTurns.ContainsKey(entityIdForPerfect) ? persistentEntityPerfectTurns[entityIdForPerfect] : 0;
                
            case UpgradeConditionType.TotalStatusEffectsSurvived:
                int entityIdForStatus = entity.ObjectId;
                return persistentEntityStatusEffectsSurvived.ContainsKey(entityIdForStatus) ? persistentEntityStatusEffectsSurvived[entityIdForStatus] : 0;
                
            // For other condition types that require Card instance, return 0 for progress
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