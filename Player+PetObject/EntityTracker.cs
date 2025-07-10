using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections.Generic;

/// <summary>
/// Enhanced tracker for entity combat statistics, stance, persistent effects, and advanced mechanics.
/// Attach to: Main entity prefabs (Player and Pet).
/// </summary>
public class EntityTracker : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkEntity entity;

    [Header("Debug Info (Read Only)")]
    [SerializeField, ReadOnly] private EntityTrackingData trackingData = new EntityTrackingData();

    // Network synchronized data
    private readonly SyncVar<bool> _isStunned = new SyncVar<bool>(); // Legacy - kept for backwards compatibility
    private readonly SyncVar<int> _fizzleCardCount = new SyncVar<int>(); // Number of cards that will fizzle

    private readonly SyncVar<int> _comboCount = new SyncVar<int>();
    private readonly SyncVar<int> _perfectionStreak = new SyncVar<int>();
    private readonly SyncVar<int> _strengthStacks = new SyncVar<int>();
    private readonly SyncVar<int> _currentStance = new SyncVar<int>(); // StanceType as int
    private readonly SyncVar<int> _stanceDuration = new SyncVar<int>(); // How many turns in current stance



    // Turn tracking
    private readonly SyncVar<int> _zeroCostCardsThisTurn = new SyncVar<int>();
    private readonly SyncVar<int> _zeroCostCardsThisFight = new SyncVar<int>();
    private readonly SyncVar<int> _cardsPlayedThisTurn = new SyncVar<int>();
    private readonly SyncVar<int> _cardsPlayedThisFight = new SyncVar<int>();
    private readonly SyncVar<int> _lastPlayedCardType = new SyncVar<int>(); // CardType as int
    
    // Complex effect tracking
    private readonly SyncVar<int> _redirectNextAttackTargetId = new SyncVar<int>(); // EntityId to redirect next attack to (0 = none)
    private readonly SyncVar<int> _amplifyNextEffectAmount = new SyncVar<int>(); // Amount to amplify next effect by (0 = none)
    private readonly SyncVar<string> _lastEffectUsedAgainstMe = new SyncVar<string>(); // Format: "effectType|amount|duration" for Mimic

    // Events for other systems to listen to
    public event Action<int> OnDamageDealt;
    public event Action<int> OnDamageTaken;
    public event Action<int> OnHealingGiven;
    public event Action<int> OnHealingReceived;
    public event Action<StanceType, StanceType> OnStanceChanged; // oldStance, newStance
    public event Action<int> OnComboChanged;
    public event Action<int> OnStrengthChanged;

    // Public accessors
    public bool IsStunned => _isStunned.Value; // Legacy - for backwards compatibility
    public int FizzleCardCount => _fizzleCardCount.Value; // Number of cards that will fizzle
    public bool HasFizzleEffect => _fizzleCardCount.Value > 0; // Convenience property

    public int ComboCount => _comboCount.Value;
    public int PerfectionStreak => _perfectionStreak.Value;
    public int StrengthStacks => _strengthStacks.Value;
    public StanceType CurrentStance => (StanceType)_currentStance.Value;
    public int StanceDuration => _stanceDuration.Value;
    public EntityTrackingData TrackingData => trackingData;
    
    // Complex effect accessors
    public int RedirectNextAttackTargetId => _redirectNextAttackTargetId.Value;
    public int AmplifyNextEffectAmount => _amplifyNextEffectAmount.Value;
    public string LastEffectUsedAgainstMe => _lastEffectUsedAgainstMe.Value;
    public bool HasRedirectNextAttack => _redirectNextAttackTargetId.Value != 0;
    public bool HasAmplifyNextEffect => _amplifyNextEffectAmount.Value > 0;

    private void Awake()
    {
        // Get required references
        if (entity == null) entity = GetComponent<NetworkEntity>();

        // Subscribe to sync var changes for debug display
        _isStunned.OnChange += (prev, next, asServer) => UpdateTrackingData();
        _fizzleCardCount.OnChange += (prev, next, asServer) => UpdateTrackingData();

        _comboCount.OnChange += (prev, next, asServer) => UpdateTrackingData();
        _perfectionStreak.OnChange += (prev, next, asServer) => UpdateTrackingData();
        _strengthStacks.OnChange += (prev, next, asServer) =>
        {
            /* Debug.Log($"EntityTracker: StrengthStacks changed for {entity.EntityName.Value} - Previous: {prev}, New: {next}, AsServer: {asServer}"); */
            trackingData.strengthStacks = next;
            OnStrengthChanged?.Invoke(next);
        };
        _currentStance.OnChange += (prev, next, asServer) => 
        {
            UpdateTrackingData();
            OnStanceChanged?.Invoke((StanceType)prev, (StanceType)next);
        };
        _zeroCostCardsThisTurn.OnChange += (prev, next, asServer) => UpdateTrackingData();
        _zeroCostCardsThisFight.OnChange += (prev, next, asServer) => UpdateTrackingData();
        _cardsPlayedThisTurn.OnChange += (prev, next, asServer) => UpdateTrackingData();
        _cardsPlayedThisFight.OnChange += (prev, next, asServer) => UpdateTrackingData();
        _lastPlayedCardType.OnChange += (prev, next, asServer) => UpdateTrackingData();
        _stanceDuration.OnChange += (prev, next, asServer) => UpdateTrackingData();
        
        // Complex effect change handlers
        _redirectNextAttackTargetId.OnChange += (prev, next, asServer) => UpdateTrackingData();
        _amplifyNextEffectAmount.OnChange += (prev, next, asServer) => UpdateTrackingData();
        _lastEffectUsedAgainstMe.OnChange += (prev, next, asServer) => UpdateTrackingData();
    }

    private void UpdateTrackingData()
    {
        trackingData.isStunned = _isStunned.Value;
        trackingData.fizzleCardCount = _fizzleCardCount.Value;

        trackingData.comboCount = _comboCount.Value;
        trackingData.perfectionStreak = _perfectionStreak.Value;
        trackingData.strengthStacks = _strengthStacks.Value;
        trackingData.currentStance = (StanceType)_currentStance.Value;
        trackingData.stanceDuration = _stanceDuration.Value;
        trackingData.zeroCostCardsThisTurn = _zeroCostCardsThisTurn.Value;
        trackingData.zeroCostCardsThisFight = _zeroCostCardsThisFight.Value;
        trackingData.cardsPlayedThisTurn = _cardsPlayedThisTurn.Value;
        trackingData.cardsPlayedThisFight = _cardsPlayedThisFight.Value;
        trackingData.lastPlayedCardType = (CardType)_lastPlayedCardType.Value;
        
        // Complex effect tracking
        trackingData.redirectNextAttackTargetId = _redirectNextAttackTargetId.Value;
        trackingData.amplifyNextEffectAmount = _amplifyNextEffectAmount.Value;
        trackingData.lastEffectUsedAgainstMe = _lastEffectUsedAgainstMe.Value;
    }

    /// <summary>
    /// Records that a card was played, updating tracking and combo systems
    /// </summary>
    [Server]
    public void RecordCardPlayed(int cardId, bool hasComboModifier, CardType cardType, bool isZeroCost)
    {
        if (!IsServerInitialized) return;

        // Update play counts
        _cardsPlayedThisTurn.Value++;
        _cardsPlayedThisFight.Value++;

        // Update zero-cost tracking
        if (isZeroCost)
        {
            _zeroCostCardsThisTurn.Value++;
            _zeroCostCardsThisFight.Value++;
        }

        // Update card type tracking for back-to-back detection
        trackingData.secondLastPlayedCardType = trackingData.lastPlayedCardType;
        trackingData.lastPlayedCardType = cardType;
        _lastPlayedCardType.Value = (int)cardType;

        // Update combo count
        if (hasComboModifier)
        {
            _comboCount.Value++;
        }
        else if (cardType != CardType.Combo)
        {
            // Non-combo cards reset the combo (including finishers)
            _comboCount.Value = 0;
        }

        OnComboChanged?.Invoke(_comboCount.Value);
        
        /* Debug.Log($"EntityTracker: {entity.EntityName.Value} played card {cardId}. Combo: {_comboCount.Value}, ZeroCost this turn: {_zeroCostCardsThisTurn.Value}"); */
    }

    /// <summary>
    /// Records damage dealt by this entity
    /// </summary>
    [Server]
    public void RecordDamageDealt(int amount)
    {
        if (!IsServerInitialized) return;

        trackingData.damageDealtThisFight += amount;
        trackingData.damageDealtLastRound += amount;
        
        OnDamageDealt?.Invoke(amount);
        
        Debug.Log($"EntityTracker: {entity.EntityName.Value} dealt {amount} damage. Total this fight: {trackingData.damageDealtThisFight}");
        
        // Notify upgrade manager for upgrade checking
        if (CardUpgradeManager.Instance != null)
        {
            // The upgrade manager is already subscribed to OnDamageDealt event, so no need for direct call
        }
    }

    /// <summary>
    /// Records damage taken by this entity
    /// </summary>
    [Server]
    public void RecordDamageTaken(int amount)
    {
        if (!IsServerInitialized) return;

        trackingData.damageTakenThisFight += amount;
        trackingData.damageTakenLastRound += amount;
        trackingData.tookDamageThisTurn = true;
        
        // Reset perfection streak if damage was taken
        if (amount > 0)
        {
            _perfectionStreak.Value = 0;
        }
        
        OnDamageTaken?.Invoke(amount);
        
        Debug.Log($"EntityTracker: {entity.EntityName.Value} took {amount} damage. Perfection streak reset.");
    }

    /// <summary>
    /// Records healing given by this entity
    /// </summary>
    [Server]
    public void RecordHealingGiven(int amount)
    {
        if (!IsServerInitialized) return;

        trackingData.healingGivenThisFight += amount;
        trackingData.healingGivenLastRound += amount;
        
        OnHealingGiven?.Invoke(amount);
        
        Debug.Log($"EntityTracker: {entity.EntityName.Value} gave {amount} healing. Total this fight: {trackingData.healingGivenThisFight}");
    }

    /// <summary>
    /// Records healing received by this entity
    /// </summary>
    [Server]
    public void RecordHealingReceived(int amount)
    {
        if (!IsServerInitialized) return;

        trackingData.healingReceivedThisFight += amount;
        trackingData.healingReceivedLastRound += amount;
        
        OnHealingReceived?.Invoke(amount);
        
        Debug.Log($"EntityTracker: {entity.EntityName.Value} received {amount} healing. Total this fight: {trackingData.healingReceivedThisFight}");
    }

        /// <summary>
    /// Sets the stunned state (legacy - use SetFizzleCardCount instead)
    /// </summary>
    [Server]
    public void SetStunned(bool stunned)
    {
        if (!IsServerInitialized) return;

        _isStunned.Value = stunned;
        Debug.Log($"EntityTracker: {entity.EntityName.Value} stun state set to {stunned}");
    }
    
    /// <summary>
    /// Sets the number of cards that will fizzle when played
    /// </summary>
    [Server]
    public void SetFizzleCardCount(int fizzleCount)
    {
        if (!IsServerInitialized) return;

        _fizzleCardCount.Value = Mathf.Max(0, fizzleCount);
        Debug.Log($"EntityTracker: {entity.EntityName.Value} fizzle count set to {_fizzleCardCount.Value}");
    }
    
    /// <summary>
    /// Adds to the fizzle card count
    /// </summary>
    [Server]
    public void AddFizzleCards(int additionalCards)
    {
        if (!IsServerInitialized) return;

        SetFizzleCardCount(_fizzleCardCount.Value + additionalCards);
    }
    
    /// <summary>
    /// Consumes one fizzle effect when a card is played
    /// Returns true if the card should fizzle
    /// </summary>
    [Server]
    public bool ConsumeAndCheckFizzle()
    {
        if (!IsServerInitialized) return false;

        if (_fizzleCardCount.Value > 0)
        {
            _fizzleCardCount.Value--;
            Debug.Log($"EntityTracker: {entity.EntityName.Value} card fizzled! Remaining fizzle count: {_fizzleCardCount.Value}");
            return true;
        }
        
        return false;
    }



    /// <summary>
    /// Sets the combo count directly (for testing purposes)
    /// </summary>
    [Server]
    public void SetComboCount(int comboCount)
    {
        if (!IsServerInitialized) return;
        
        _comboCount.Value = comboCount;
        OnComboChanged?.Invoke(_comboCount.Value);
        
        Debug.Log($"EntityTracker: {entity.EntityName.Value} combo count set to {comboCount}");
    }

    /// <summary>
    /// Sets the entity's combat stance
    /// </summary>
    [Server]
    public void SetStance(StanceType stance)
    {
        if (!IsServerInitialized) return;
        
        StanceType oldStance = (StanceType)_currentStance.Value;
        _currentStance.Value = (int)stance;
        
        // Reset stance duration when stance changes (including to None)
        if (oldStance != stance)
        {
            _stanceDuration.Value = 0;
            Debug.Log($"EntityTracker: {entity.EntityName.Value} stance changed from {oldStance} to {stance}, duration reset to 0");
        }
        
        // Handle stance-specific effects
        ApplyStanceEffects(stance, oldStance);
        
        Debug.Log($"EntityTracker: {entity.EntityName.Value} stance changed from {oldStance} to {stance}");
    }

    /// <summary>
    /// Applies or removes stance-specific effects
    /// </summary>
    private void ApplyStanceEffects(StanceType newStance, StanceType oldStance)
    {
        // Remove old stance effects (if any)
        // This would integrate with EffectHandler to remove stance-based status effects
        
        // Apply new stance effects
        switch (newStance)
        {
            case StanceType.Aggressive:
                // +damage, -defense would be handled by EffectHandler
                break;
            case StanceType.Defensive:
                // +defense, -damage would be handled by EffectHandler
                break;
            case StanceType.None:
                break;
        }
    }

    /// <summary>
    /// Adds strength stacks
    /// </summary>
    [Server]
    public void AddStrength(int amount)
    {
        if (!IsServerInitialized || amount <= 0) return;
        
        _strengthStacks.Value += amount;
        Debug.Log($"EntityTracker: {entity.EntityName.Value} gained {amount} strength. Total: {_strengthStacks.Value}");
    }

    /// <summary>
    /// Removes strength stacks
    /// </summary>
    [Server]
    public void RemoveStrength(int amount)
    {
        if (!IsServerInitialized || amount <= 0) return;
        
        _strengthStacks.Value = Mathf.Max(0, _strengthStacks.Value - amount);
        Debug.Log($"EntityTracker: {entity.EntityName.Value} lost {amount} strength. Total: {_strengthStacks.Value}");
    }



    /// <summary>
    /// Processes turn start effects
    /// </summary>
    [Server]
    public void OnTurnStart()
    {
        if (!IsServerInitialized) return;

        trackingData.currentTurnNumber++;
        trackingData.battleTurnCount++;
        
        // Reset turn-specific counters
        _zeroCostCardsThisTurn.Value = 0;
        _cardsPlayedThisTurn.Value = 0;
        trackingData.damageDealtLastRound = 0;
        trackingData.healingGivenLastRound = 0;
        trackingData.healingReceivedLastRound = 0;
        trackingData.damageTakenLastRound = 0;
        trackingData.usedAllEnergyThisTurn = false;
        
        // Update perfection streak if no damage was taken last turn
        if (!trackingData.tookDamageThisTurn)
        {
            _perfectionStreak.Value++;
        }
        trackingData.tookDamageThisTurn = false;

        // Process status effects at start of turn
        EffectHandler effectHandler = entity.GetComponent<EffectHandler>();
        if (effectHandler != null)
        {
            effectHandler.ProcessStartOfTurnEffects();
        }



        // Process stance turn-start effects
        ProcessStanceTurnEffects(true);
        
        /* Debug.Log($"EntityTracker: Turn start for {entity.EntityName.Value}. Turn: {trackingData.currentTurnNumber}, Battle Turn: {trackingData.battleTurnCount}, Perfection: {_perfectionStreak.Value}"); */
    }

    /// <summary>
    /// Processes turn end effects
    /// </summary>
    [Server]
    public void OnTurnEnd()
    {
        if (!IsServerInitialized) return;

        // Check for perfect turn (no damage taken + all energy used)
        if (!trackingData.tookDamageThisTurn && trackingData.usedAllEnergyThisTurn)
        {
            trackingData.hadPerfectTurnThisFight = true;
            Debug.Log($"EntityTracker: {entity.EntityName.Value} had a perfect turn!");
        }

        // Process status effects at end of turn
        EffectHandler effectHandler = entity.GetComponent<EffectHandler>();
        if (effectHandler != null)
        {
            effectHandler.ProcessEndOfTurnEffects();
        }

        // Process stance turn-end effects
        ProcessStanceTurnEffects(false);
        
        // Handle stance duration tracking and auto-clear
        StanceType currentStance = (StanceType)_currentStance.Value;
        if (currentStance != StanceType.None)
        {
            _stanceDuration.Value++;
            /* Debug.Log($"EntityTracker: {entity.EntityName.Value} has been in {currentStance} stance for {_stanceDuration.Value} turn(s)"); */
            
            // Clear stance if held for 2 consecutive turns
            if (_stanceDuration.Value >= 2)
            {
                /* Debug.Log($"EntityTracker: {entity.EntityName.Value} has held {currentStance} stance for 2 turns, clearing to None"); */
                SetStance(StanceType.None);
            }
        }
        
        // Notify the upgrade manager
        if (CardUpgradeManager.Instance != null)
        {
            CardUpgradeManager.Instance.OnTurnEnd(entity);
        }
        
        /* Debug.Log($"EntityTracker: Turn end for {entity.EntityName.Value}"); */
    }



    /// <summary>
    /// Processes stance-based turn effects
    /// </summary>
    private void ProcessStanceTurnEffects(bool isStartOfTurn)
    {
        StanceType currentStance = (StanceType)_currentStance.Value;
        
        switch (currentStance)
        {
            case StanceType.Aggressive:
                if (isStartOfTurn)
                {
                    // Aggressive stance effects
                    Debug.Log($"EntityTracker: Aggressive stance turn start effects for {entity.EntityName.Value}");
                }
                break;
            case StanceType.Defensive:
                if (isStartOfTurn)
                {
                    // Defensive stance effects
                    Debug.Log($"EntityTracker: Defensive stance turn start effects for {entity.EntityName.Value}");
                }
                break;
        }
    }

    /// <summary>
    /// Checks if a condition is met for conditional card effects
    /// </summary>
    public bool CheckCondition(ConditionalType conditionType, int conditionValue)
    {
        switch (conditionType)
        {
            case ConditionalType.IfDamageTakenThisFight:
                return trackingData.damageTakenThisFight >= conditionValue;
            case ConditionalType.IfDamageTakenLastRound:
                return trackingData.damageTakenLastRound >= conditionValue;
            case ConditionalType.IfDamageTakenThisTurn:
                return trackingData.tookDamageThisTurn && conditionValue == 1; // Simple boolean check
            case ConditionalType.IfHealingReceivedThisFight:
                return trackingData.healingReceivedThisFight >= conditionValue;
            case ConditionalType.IfHealingReceivedLastRound:
                return trackingData.healingReceivedLastRound >= conditionValue;
            case ConditionalType.IfPerfectionStreak:
                return _perfectionStreak.Value >= conditionValue;
            case ConditionalType.IfComboCount:
                return _comboCount.Value >= conditionValue;
            case ConditionalType.IfZeroCostCardsThisTurn:
                return _zeroCostCardsThisTurn.Value >= conditionValue;
            case ConditionalType.IfZeroCostCardsThisFight:
                return _zeroCostCardsThisFight.Value >= conditionValue;
            case ConditionalType.IfInStance:
                return _currentStance.Value == conditionValue; // conditionValue should be cast to StanceType
            case ConditionalType.IfLastCardType:
                return _lastPlayedCardType.Value == conditionValue; // conditionValue should be cast to CardType
            case ConditionalType.IfEnergyRemaining:
                return entity.CurrentEnergy.Value >= conditionValue;
            default:
                return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // COMPLEX EFFECT MANAGEMENT
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Sets redirect next attack effect - next damage targeting this entity will hit the specified target instead
    /// </summary>
    [Server]
    public void SetRedirectNextAttack(int targetEntityId)
    {
        if (!IsServerInitialized) return;
        _redirectNextAttackTargetId.Value = targetEntityId;
        Debug.Log($"EntityTracker: {entity.EntityName.Value} will redirect next attack to entity {targetEntityId}");
    }
    
    /// <summary>
    /// Clears redirect next attack effect (called after being used)
    /// </summary>
    [Server]
    public void ClearRedirectNextAttack()
    {
        if (!IsServerInitialized) return;
        _redirectNextAttackTargetId.Value = 0;
    }
    
    /// <summary>
    /// Sets amplify next effect - next card effect will have its potency increased by the specified amount
    /// </summary>
    [Server]
    public void SetAmplifyNextEffect(int amplifyAmount)
    {
        if (!IsServerInitialized) return;
        _amplifyNextEffectAmount.Value = amplifyAmount;
        Debug.Log($"EntityTracker: {entity.EntityName.Value} will amplify next effect by {amplifyAmount}");
    }
    
    /// <summary>
    /// Consumes and returns the amplify next effect amount
    /// </summary>
    [Server]
    public int ConsumeAmplifyNextEffect()
    {
        if (!IsServerInitialized) return 0;
        int amplifyAmount = _amplifyNextEffectAmount.Value;
        _amplifyNextEffectAmount.Value = 0;
        return amplifyAmount;
    }
    
    /// <summary>
    /// Records the last effect used against this entity (for Mimic)
    /// </summary>
    [Server]
    public void RecordEffectUsedAgainstMe(CardEffectType effectType, int amount, int duration)
    {
        if (!IsServerInitialized) return;
        string effectData = $"{effectType}|{amount}|{duration}";
        _lastEffectUsedAgainstMe.Value = effectData;
        Debug.Log($"EntityTracker: Recorded effect used against {entity.EntityName.Value}: {effectData}");
    }
    
    /// <summary>
    /// Gets the last effect used against this entity for Mimic
    /// </summary>
    public (CardEffectType effectType, int amount, int duration) GetLastEffectUsedAgainstMe()
    {
        string effectData = _lastEffectUsedAgainstMe.Value;
        if (string.IsNullOrEmpty(effectData)) 
            return (CardEffectType.Damage, 0, 0); // Default if no effect recorded
            
        string[] parts = effectData.Split('|');
        if (parts.Length >= 3)
        {
            if (System.Enum.TryParse<CardEffectType>(parts[0], out CardEffectType effectType) &&
                int.TryParse(parts[1], out int amount) &&
                int.TryParse(parts[2], out int duration))
            {
                return (effectType, amount, duration);
            }
        }
        
        return (CardEffectType.Damage, 0, 0); // Default if parsing fails
    }

    /// <summary>
    /// Gets the current tracking data for scaling calculations
    /// </summary>
    public EntityTrackingData GetTrackingDataForScaling()
    {
        UpdateTrackingData(); // Ensure latest data
        return trackingData;
    }

    /// <summary>
    /// Resets tracking data for a new fight
    /// </summary>
    [Server]
    public void ResetForNewFight()
    {
        if (!IsServerInitialized) return;

        // Reset fight-specific data (preserve lifetime data)
        trackingData.damageTakenThisFight = 0;
        trackingData.damageDealtThisFight = 0;
        trackingData.healingReceivedThisFight = 0;
        trackingData.healingGivenThisFight = 0;
        trackingData.currentTurnNumber = 0;
        trackingData.battleTurnCount = 0;
        trackingData.hadPerfectTurnThisFight = false;
        trackingData.usedAllEnergyThisTurn = false;
        trackingData.secondLastPlayedCardType = CardType.Attack;
        trackingData.wonLastFight = false;
        trackingData.lostLastFight = false;
        trackingData.survivedStatusEffects.Clear();
        // Note: totalFightsWon, totalFightsLost, totalBattleTurns, totalPerfectTurns, totalStatusEffectsSurvived are preserved
        
        _comboCount.Value = 0;
        _perfectionStreak.Value = 0;
        _zeroCostCardsThisFight.Value = 0;
        _cardsPlayedThisFight.Value = 0;
        _isStunned.Value = false;
        _fizzleCardCount.Value = 0;

        _strengthStacks.Value = 0;
        _currentStance.Value = (int)StanceType.None;
        _stanceDuration.Value = 0;
        
        // Reset complex effect states
        _redirectNextAttackTargetId.Value = 0;
        _amplifyNextEffectAmount.Value = 0;
        _lastEffectUsedAgainstMe.Value = "";
        
        // Reset turn data
        ResetTurnData();

        /* Debug.Log($"EntityTracker: Reset fight data for {entity.EntityName.Value}"); */
    }

    /// <summary>
    /// Resets turn-specific data
    /// </summary>
    [Server]
    public void ResetTurnData()
    {
        if (!IsServerInitialized) return;

        _zeroCostCardsThisTurn.Value = 0;
        _cardsPlayedThisTurn.Value = 0;
        trackingData.damageDealtLastRound = 0;
        trackingData.healingGivenLastRound = 0;
        trackingData.healingReceivedLastRound = 0;
        trackingData.damageTakenLastRound = 0;
        trackingData.tookDamageThisTurn = false;
    }

    /// <summary>
    /// Records energy usage for perfect turn tracking
    /// </summary>
    [Server]
    public void RecordEnergyUsage(int currentEnergy, int maxEnergy)
    {
        if (!IsServerInitialized) return;
        
        // Check if all energy was used (allowing for 0 remaining)
        trackingData.usedAllEnergyThisTurn = (currentEnergy == 0);
    }
    
    /// <summary>
    /// Records surviving a status effect
    /// </summary>
    [Server]
    public void RecordSurvivedStatusEffect(string statusEffectName)
    {
        if (!IsServerInitialized) return;
        
        if (!trackingData.survivedStatusEffects.Contains(statusEffectName))
        {
            trackingData.survivedStatusEffects.Add(statusEffectName);
            trackingData.totalStatusEffectsSurvived++;
            Debug.Log($"EntityTracker: {entity.EntityName.Value} survived status effect: {statusEffectName}. Total unique: {trackingData.totalStatusEffectsSurvived}");
        }
    }
    
    /// <summary>
    /// Records fight outcome for this entity
    /// </summary>
    [Server]
    public void RecordFightOutcome(bool victory)
    {
        if (!IsServerInitialized) return;
        
        trackingData.wonLastFight = victory;
        trackingData.lostLastFight = !victory;
        
        // Update lifetime tracking
        if (victory)
        {
            trackingData.totalFightsWon++;
        }
        else
        {
            trackingData.totalFightsLost++;
        }
        
        // Update lifetime battle turns
        trackingData.totalBattleTurns += trackingData.battleTurnCount;
        
        // Update lifetime perfect turns
        if (trackingData.hadPerfectTurnThisFight)
        {
            trackingData.totalPerfectTurns++;
        }
        
        // Notify upgrade manager for persistent tracking
        if (CardUpgradeManager.Instance != null)
        {
            CardUpgradeManager.Instance.UpdatePersistentEntityTracking(entity.ObjectId, victory, trackingData.battleTurnCount, trackingData.hadPerfectTurnThisFight ? 1 : 0, trackingData.survivedStatusEffects.Count);
        }
        
        /* Debug.Log($"EntityTracker: {entity.EntityName.Value} fight outcome: {(victory ? "Victory" : "Defeat")}. Lifetime: {trackingData.totalFightsWon}W/{trackingData.totalFightsLost}L"); */
    }

    /// <summary>
    /// Called when a fight ends - notifies upgrade manager
    /// </summary>
    [Server]
    public static void NotifyFightEnd(bool victory, List<NetworkEntity> participatingEntities)
    {
        // Record fight outcomes for all participating entities
        foreach (var entity in participatingEntities)
        {
            var tracker = entity.GetComponent<EntityTracker>();
            if (tracker != null)
            {
                tracker.RecordFightOutcome(victory);
            }
        }
        
        if (CardUpgradeManager.Instance != null)
        {
            CardUpgradeManager.Instance.OnFightEnd(victory, participatingEntities);
        }
        
        Debug.Log($"EntityTracker: Fight ended with victory={victory}, {participatingEntities.Count} participating entities");
    }
    

}

// ═══════════════════════════════════════════════════════════════
// ENTITY TRACKING STRUCTURES (moved from CardEnums.cs)
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Enhanced entity tracking data for damage, healing, and perfection streaks
/// </summary>
[System.Serializable]
public class EntityTrackingData
{
    [Header("Damage Tracking")]
    public int damageTakenThisFight;
    public int damageTakenLastRound;
    public int damageDealtThisFight;
    public int damageDealtLastRound;
    
    [Header("Healing Tracking")]
    public int healingReceivedThisFight;
    public int healingReceivedLastRound;
    public int healingGivenThisFight;
    public int healingGivenLastRound;
    
    [Header("Perfection Tracking")]
    public int perfectionStreak; // Turns without taking damage
    public int currentTurnNumber;
    public bool tookDamageThisTurn;
    public bool hadPerfectTurnThisFight; // No damage taken + all energy used
    
    [Header("Combat State")]
    public int comboCount;
    public bool isStunned; // Legacy - kept for compatibility
    public int fizzleCardCount; // Number of cards that will fizzle

    public StanceType currentStance;
    public int stanceDuration; // How many consecutive turns in current stance
    
    [Header("Turn Tracking")]
    public int zeroCostCardsThisTurn;
    public int zeroCostCardsThisFight;
    public int cardsPlayedThisTurn;
    public int cardsPlayedThisFight;
    public CardType lastPlayedCardType;
    public CardType secondLastPlayedCardType; // For back-to-back tracking
    public bool usedAllEnergyThisTurn;
    
    [Header("Strength")]
    public int strengthStacks;
    
    [Header("Battle Tracking - This Fight")]
    public int battleTurnCount; // Total turns in current battle
    public bool wonLastFight;
    public bool lostLastFight;
    public List<string> survivedStatusEffects = new List<string>(); // Status effects survived this fight
    
    [Header("Battle Tracking - Lifetime")]
    public int totalFightsWon; // Total fights won across all time
    public int totalFightsLost; // Total fights lost across all time
    public int totalBattleTurns; // Total turns across all battles
    public int totalPerfectTurns; // Total perfect turns achieved
    public int totalStatusEffectsSurvived; // Total unique status effects survived
    
    [Header("Complex Effect States")]
    public int redirectNextAttackTargetId; // EntityId to redirect next attack to (0 = none)
    public int amplifyNextEffectAmount; // Amount to amplify next effect by (0 = none)
    public string lastEffectUsedAgainstMe; // Format: "effectType|amount|duration" for Mimic
}

/// <summary>
/// Data structure for stance effects
/// </summary>
[System.Serializable]
public class StanceEffect
{
    [Header("Stance Configuration")]
    public StanceType stanceType;
    public bool overridePreviousStance;
    
    [Header("Stat Modifiers")]
    public int damageModifier;
    public int defenseModifier;
    public int energyModifier;
    public int drawModifier;
    public int healthModifier;
    
    [Header("Special Effects")]
    public bool grantsThorns;
    public int thornsAmount;
    public bool grantsShield;
    public int shieldAmount;
    public bool enhancesCritical;
    public int criticalBonus;
    
    [Header("Ongoing Effects")]
    public CardEffectType onTurnStartEffect;
    public int onTurnStartAmount;
    public CardEffectType onTurnEndEffect;
    public int onTurnEndAmount;
} 