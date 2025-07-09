using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Main procedural card generator that creates randomized cards using the point budget system.
/// This system respects point budgets and creates balanced cards with appropriate effects.
/// </summary>
public class ProceduralCardGenerator
{
    private RandomCardConfig config;
    private CardPointBudgetManager budgetManager;
    
    // Card naming components for mystical procedural generation (shorter names)
    private readonly string[] damageWords = { 
        "Detonation", "Reckoning", "Undoing", "Convergence", "Shear", "Gambit", "Petition",
        "Chronoshear", "Burnished Fetters", "Symbiotic Reckoning", "Strike", "Blast", "Surge"
    };
    
    private readonly string[] healWords = { 
        "Mycelial Memory", "Celestial Drift", "Sporebound Oath", "Restoration", "Renewal",
        "Blessing", "Mending", "Recovery", "Vitality", "Regeneration"
    };
    
    private readonly string[] statusWords = { 
        "Ash Psalm", "Pendulum Gambit", "Oathroot Binding", "Unquenched Forgefire",
        "Hex", "Curse", "Blessing", "Ward", "Enchantment", "Aura"
    };
    
    private readonly string[] powerWords = { 
        "Choral", "Umbral", "Stellar", "Verdant", "Temporal", "Burnished", "Thorncoven", "Pale",
        "Void-Script", "Ember-Wrought", "Blood-Debt", "Star-Nursery", "Ink-Bound", "Smoke-Wreath"
    };
    
    private readonly string[] mysticalSuffixes = { 
        "of the Ember Choir", "of Undoing", "in Ascension", "Convergence", "Implodes", "Gambit",
        "of Dread", "of Barbed Favor", "Memory", "Drift", "Reckoning", "Over Cinders"
    };
    
    public ProceduralCardGenerator(RandomCardConfig config)
    {
        this.config = config;
        this.budgetManager = new CardPointBudgetManager(config);
        
        // Validate configuration on initialization
        ValidateConfiguration();
    }
    
    /// <summary>
    /// Validate that all required configurations are properly loaded and log the status
    /// </summary>
    private void ValidateConfiguration()
    {
        Debug.Log("=== PROCEDURAL CARD GENERATOR CONFIGURATION VALIDATION ===");
        
        bool allConfigsValid = true;
        
        // Check main config
        if (config == null)
        {
            Debug.LogError("[CONFIG] ❌ RandomCardConfig is NULL! Using fallback values for all systems.");
            allConfigsValid = false;
        }
        else
        {
            Debug.Log("[CONFIG] ✅ RandomCardConfig loaded successfully");
            
            // Check individual config components
            allConfigsValid &= ValidatePointBudgetConfig();
            allConfigsValid &= ValidateEffectCostConfig();
            allConfigsValid &= ValidateRarityDistributionConfig();
            allConfigsValid &= ValidateUpgradeCostConfig();
            
            // Log generation settings
            Debug.Log($"[CONFIG] Generation Settings: {config.minEffectsPerCard}-{config.maxEffectsPerCard} effects per card");
            Debug.Log($"[CONFIG] Upgrade Settings: Allow no upgrade: {config.allowNoUpgradeCondition}, Chance: {config.noUpgradeConditionChance:F1}%");
        }
        
        if (allConfigsValid)
        {
            Debug.Log("[CONFIG] 🎉 ALL CONFIGURATIONS LOADED SUCCESSFULLY - Using configured values");
        }
        else
        {
            Debug.LogWarning("[CONFIG] ⚠️ SOME CONFIGURATIONS MISSING - Will use fallback values where needed");
        }
        
        // Test budget calculation to verify actual values being used
        TestBudgetCalculation();
        
        Debug.Log("=== END CONFIGURATION VALIDATION ===");
    }
    
    private bool ValidatePointBudgetConfig()
    {
        if (config.pointBudgetConfig == null)
        {
            Debug.LogError("[CONFIG] ❌ CardPointBudgetConfig is NULL! Energy costs will use fallback values (12-35)");
            return false;
        }
        
        Debug.Log("[CONFIG] ✅ CardPointBudgetConfig loaded successfully");
        Debug.Log($"[CONFIG] Point Budgets - Common: {config.pointBudgetConfig.commonPointBudget}, " +
                  $"Uncommon: {config.pointBudgetConfig.uncommonPointBudget}, " +
                  $"Rare: {config.pointBudgetConfig.rarePointBudget}");
        Debug.Log($"[CONFIG] Energy Cost Settings - Budget %: {config.pointBudgetConfig.energyCostBudgetPercentage:F1}%, " +
                  $"Points per Energy: {config.pointBudgetConfig.pointsPerEnergyCost:F1}");
        
        return true;
    }
    
    private bool ValidateEffectCostConfig()
    {
        if (config.effectCostConfig == null)
        {
            Debug.LogError("[CONFIG] ❌ EffectPointCostConfig is NULL! Effect costs will use 1:1 ratio fallback");
            return false;
        }
        
        Debug.Log("[CONFIG] ✅ EffectPointCostConfig loaded successfully");
        Debug.Log($"[CONFIG] Key Effect Costs - Damage: {config.effectCostConfig.damagePointCost:F1}, " +
                  $"Heal: {config.effectCostConfig.healPointCost:F1}, " +
                  $"Energy Restore: {config.effectCostConfig.restoreEnergyPointCost:F1}");
        
        return true;
    }
    
    private bool ValidateRarityDistributionConfig()
    {
        if (config.rarityDistributionConfig == null)
        {
            Debug.LogError("[CONFIG] ❌ RarityDistributionConfig is NULL! Starter decks and draft packs will use basic randomization");
            return false;
        }
        
        Debug.Log("[CONFIG] ✅ RarityDistributionConfig loaded successfully");
        Debug.Log($"[CONFIG] Draft Distribution - Common: {config.rarityDistributionConfig.draftCommonPercentage:F1}%, " +
                  $"Uncommon: {config.rarityDistributionConfig.draftUncommonPercentage:F1}%, " +
                  $"Rare: {config.rarityDistributionConfig.draftRarePercentage:F1}%");
        Debug.Log($"[CONFIG] Starter Deck - Commons: {config.rarityDistributionConfig.starterDeckCommons}, " +
                  $"Uncommons: {config.rarityDistributionConfig.starterDeckUncommons}, " +
                  $"Rares: {config.rarityDistributionConfig.starterDeckRares}");
        
        return true;
    }
    
    private bool ValidateUpgradeCostConfig()
    {
        if (config.upgradeCostConfig == null)
        {
            Debug.LogError("[CONFIG] ❌ UpgradeConditionCostConfig is NULL! Upgrade conditions will use minimal fallback costs");
            return false;
        }
        
        Debug.Log("[CONFIG] ✅ UpgradeConditionCostConfig loaded successfully");
        Debug.Log($"[CONFIG] Upgrade Costs - Easy: {config.upgradeCostConfig.easyConditionCost:F1}, " +
                  $"Medium: {config.upgradeCostConfig.mediumConditionCost:F1}, " +
                  $"Hard: {config.upgradeCostConfig.hardConditionCost:F1}");
        
        return true;
    }
    
    private void TestBudgetCalculation()
    {
        Debug.Log("[CONFIG] Testing actual budget calculations:");
        
        foreach (CardRarity rarity in new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare })
        {
            var budget = budgetManager.CalculateBudget(rarity);
            Debug.Log($"[CONFIG] {rarity} Card Budget - Total: {budget.totalBudget}, " +
                      $"Energy Cost: {budget.energyCost}, " +
                      $"Effect Budget: {budget.effectBudget:F1}");
        }
    }
    
    /// <summary>
    /// Generate a random card of the specified rarity
    /// </summary>
    public CardData GenerateRandomCard(CardRarity rarity)
    {
        // Calculate budget for this card
        var budget = budgetManager.CalculateBudget(rarity);
        
        // Determine if this card should have an upgrade condition
        bool hasUpgrade = ShouldHaveUpgradeCondition();
        
        if (hasUpgrade)
        {
            var (conditionType, requiredValue) = GenerateRandomUpgradeCondition(rarity);
            budget = budgetManager.ApplyUpgradeCost(budget, conditionType, requiredValue);
        }
        
        // Generate effects that fit within the budget
        var proposedEffects = budgetManager.GenerateAffordableEffects(budget);
        
        if (proposedEffects.Count == 0)
        {
            Debug.LogWarning($"ProceduralCardGenerator: Failed to generate any effects for {rarity} card!");
            proposedEffects = CreateFallbackEffect(budget);
        }
        
        // Create the actual CardData object
        var cardData = CreateCardDataFromProposal(budget, proposedEffects, hasUpgrade);
        
        // Generate a procedural name and description
        GenerateCardNameAndDescription(cardData, proposedEffects);
        
        return cardData;
    }
    
    /// <summary>
    /// Generate multiple cards of specified rarities (useful for starter decks)
    /// </summary>
    public List<CardData> GenerateCards(List<CardRarity> rarities)
    {
        var cards = new List<CardData>();
        
        foreach (var rarity in rarities)
        {
            cards.Add(GenerateRandomCard(rarity));
        }
        
        return cards;
    }

    /// <summary>
    /// Generate a starter deck that checks OfflineGameManager for themed vs random preference
    /// </summary>
    public List<CardData> GenerateStarterDeck(string characterClass, int maxUniqueCards = 10)
    {
        bool useThemed = true; // Default to themed
        
        // Check OfflineGameManager if available
        if (OfflineGameManager.Instance != null)
        {
            useThemed = OfflineGameManager.Instance.UseThemedStarterDecks;
            Debug.Log($"[PROCGEN] OfflineGameManager found - using themed decks: {useThemed}");
        }
        else
        {
            Debug.LogWarning("[PROCGEN] OfflineGameManager not available - defaulting to themed decks");
        }
        
        if (useThemed)
        {
            return GenerateThematicStarterDeck(characterClass, maxUniqueCards);
        }
        else
        {
            return GenerateRandomStarterDeck(maxUniqueCards);
        }
    }

    /// <summary>
    /// Generate a completely random starter deck without thematic bias
    /// </summary>
    private List<CardData> GenerateRandomStarterDeck(int maxUniqueCards = 10)
    {
        if (config?.rarityDistributionConfig == null)
        {
            Debug.LogError("ProceduralCardGenerator: Missing rarity distribution configuration!");
            return new List<CardData>();
        }
        
        var cards = new List<CardData>();
        var distConfig = config.rarityDistributionConfig;
        
        // Calculate total cards needed for starter deck
        int totalCardsNeeded = distConfig.starterDeckCommons + distConfig.starterDeckUncommons + distConfig.starterDeckRares;
        
        // Limit unique cards to the maximum specified
        int uniqueCardsToGenerate = Mathf.Min(maxUniqueCards, totalCardsNeeded);
        
        Debug.Log($"[PROCGEN] Generating random starter deck: {uniqueCardsToGenerate} unique cards out of {totalCardsNeeded} total");
        
        // Generate unique cards with balanced rarity distribution
        for (int i = 0; i < uniqueCardsToGenerate; i++)
        {
            CardRarity rarity = DetermineRarityForUniqueCard(i, uniqueCardsToGenerate, distConfig);
            cards.Add(GenerateRandomCard(rarity));
        }
        
        // Fill remaining slots with duplicates of existing cards
        int duplicatesNeeded = totalCardsNeeded - uniqueCardsToGenerate;
        if (duplicatesNeeded > 0 && cards.Count > 0)
        {
            Debug.Log($"[PROCGEN] Adding {duplicatesNeeded} duplicates to reach target deck size of {totalCardsNeeded}");
            
            for (int i = 0; i < duplicatesNeeded; i++)
            {
                var cardToDuplicate = cards[UnityEngine.Random.Range(0, cards.Count)];
                cards.Add(DuplicateCard(cardToDuplicate));
            }
        }
        
        return cards;
    }
    
    /// <summary>
    /// Generate a thematic starter deck for a character class with a limited number of unique cards
    /// </summary>
    public List<CardData> GenerateThematicStarterDeck(string characterClass, int maxUniqueCards = 10)
    {
        if (config?.rarityDistributionConfig == null)
        {
            Debug.LogError("ProceduralCardGenerator: Missing rarity distribution configuration!");
            return new List<CardData>();
        }
        
        var cards = new List<CardData>();
        var distConfig = config.rarityDistributionConfig;
        
        // Calculate total cards needed for starter deck
        int totalCardsNeeded = distConfig.starterDeckCommons + distConfig.starterDeckUncommons + distConfig.starterDeckRares;
        
        // Limit unique cards to the maximum specified
        int uniqueCardsToGenerate = Mathf.Min(maxUniqueCards, totalCardsNeeded);
        
        Debug.Log($"[PROCGEN] Generating starter deck for {characterClass}: {uniqueCardsToGenerate} unique cards out of {totalCardsNeeded} total");
        
        // Generate unique cards with balanced rarity distribution
        for (int i = 0; i < uniqueCardsToGenerate; i++)
        {
            CardRarity rarity = DetermineRarityForUniqueCard(i, uniqueCardsToGenerate, distConfig);
            cards.Add(GenerateThematicCard(rarity, characterClass));
        }
        
        // Fill remaining slots with duplicates of existing cards
        int duplicatesNeeded = totalCardsNeeded - uniqueCardsToGenerate;
        if (duplicatesNeeded > 0 && cards.Count > 0)
        {
            Debug.Log($"[PROCGEN] Adding {duplicatesNeeded} duplicates to reach target deck size of {totalCardsNeeded}");
            
            for (int i = 0; i < duplicatesNeeded; i++)
            {
                var cardToDuplicate = cards[UnityEngine.Random.Range(0, cards.Count)];
                cards.Add(DuplicateCard(cardToDuplicate));
            }
        }
        
        return cards;
    }
    
    /// <summary>
    /// Generate a thematic card that fits a character class archetype
    /// </summary>
    private CardData GenerateThematicCard(CardRarity rarity, string characterClass)
    {
        var card = GenerateRandomCard(rarity);
        
        // Apply thematic modifications based on character class
        ApplyThematicModifications(card, characterClass);
        
        return card;
    }
    
    /// <summary>
    /// Apply thematic modifications to make cards fit character archetypes
    /// </summary>
    private void ApplyThematicModifications(CardData card, string characterClass)
    {
        switch (characterClass.ToLower())
        {
            case "warrior":
                BiasTowardWarriorTheme(card);
                break;
            case "mystic":
                BiasTowardMysticTheme(card);
                break;
            case "assassin":
                BiasTowardAssassinTheme(card);
                break;
        }
    }
    
    /// <summary>
    /// Bias card toward warrior theme (damage, shields, defense)
    /// </summary>
    private void BiasTowardWarriorTheme(CardData card)
    {
        // Modify effects to be more warrior-like
        var effects = card.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            if (effect.effectType == CardEffectType.Heal && UnityEngine.Random.value < 0.5f)
            {
                // Convert some heals to shields for warriors
                effect.effectType = CardEffectType.ApplyShield;
                effect.targetType = CardTargetType.Self;
            }
        }
        
        // Increase chance of stance effects for warriors
        if (UnityEngine.Random.value < 0.3f)
        {
            // Add stance effects with warrior bias
            if (!card.ChangesStance && effects.Count < 3)
            {
                var warriorStances = new[] { StanceType.Defensive, StanceType.Guardian, StanceType.Berserker, StanceType.Aggressive };
                StanceType stanceToEnter = warriorStances[UnityEngine.Random.Range(0, warriorStances.Length)];
                card.ChangeStance(stanceToEnter);
            }
        }
    }
    
    /// <summary>
    /// Bias card toward mystic theme (elemental effects, energy, spell-like)
    /// </summary>
    private void BiasTowardMysticTheme(CardData card)
    {
        var effects = card.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            
            // Add elemental typing to damage effects
            // Elemental system removed - no elemental typing for damage
        }
        
        // Mystics favor mystical effects (elemental system removed)
        
        // Mystics can enter mystical stances
        if (UnityEngine.Random.value < 0.25f && !card.ChangesStance)
        {
            var mysticStances = new[] { StanceType.Mystic, StanceType.Focused, StanceType.LimitBreak };
            StanceType stanceToEnter = mysticStances[UnityEngine.Random.Range(0, mysticStances.Length)];
            card.ChangeStance(stanceToEnter);
        }
    }
    
    /// <summary>
    /// Bias card toward assassin theme (high damage, low cost, conditions)
    /// </summary>
    private void BiasTowardAssassinTheme(CardData card)
    {
        var effects = card.Effects;
        
        // Assassins favor conditional effects and combos
        for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            if (effect.conditionType == ConditionalType.None && UnityEngine.Random.value < 0.4f)
            {
                // Add conditions to effects
                var conditions = new[] 
                { 
                    ConditionalType.IfTargetHealthBelow, 
                    ConditionalType.IfComboCount,
                    ConditionalType.IfSourceHealthBelow 
                };
                effect.conditionType = conditions[UnityEngine.Random.Range(0, conditions.Length)];
                effect.conditionValue = UnityEngine.Random.Range(2, 6);
            }
        }
        
        // Assassins can enter aggressive or focused stances
        if (UnityEngine.Random.value < 0.2f && !card.ChangesStance)
        {
            var assassinStances = new[] { StanceType.Aggressive, StanceType.Berserker, StanceType.Focused };
            StanceType stanceToEnter = assassinStances[UnityEngine.Random.Range(0, assassinStances.Length)];
            card.ChangeStance(stanceToEnter);
        }
    }
    
    /// <summary>
    /// Determine if a card should have an upgrade condition
    /// </summary>
    private bool ShouldHaveUpgradeCondition()
    {
        if (config == null || !config.allowNoUpgradeCondition)
            return true;
        
        return UnityEngine.Random.value > config.noUpgradeConditionChance;
    }
    
    /// <summary>
    /// Generate a random upgrade condition appropriate for the rarity
    /// </summary>
    private (UpgradeConditionType conditionType, int requiredValue) GenerateRandomUpgradeCondition(CardRarity rarity)
    {
        // Get appropriate condition types based on rarity
        var conditionTypes = GetUpgradeConditionsForRarity(rarity);
        var conditionType = conditionTypes[UnityEngine.Random.Range(0, conditionTypes.Count)];
        
        // Generate appropriate required value
        int requiredValue = GenerateRequiredValueForCondition(conditionType, rarity);
        
        return (conditionType, requiredValue);
    }
    
    /// <summary>
    /// Get upgrade conditions appropriate for each rarity level
    /// </summary>
    private List<UpgradeConditionType> GetUpgradeConditionsForRarity(CardRarity rarity)
    {
        return rarity switch
        {
            CardRarity.Common => new List<UpgradeConditionType>
            {
                // Simple tracking conditions
                UpgradeConditionType.TimesPlayedThisFight,
                UpgradeConditionType.CopiesInDeck,
                UpgradeConditionType.CopiesInHand,
                UpgradeConditionType.DamageDealtThisFight,
                UpgradeConditionType.HealingGivenThisFight,
                UpgradeConditionType.PlayedAtLowHealth,
                UpgradeConditionType.HeldAtTurnEnd,
                UpgradeConditionType.DrawnOften
            },
            CardRarity.Uncommon => new List<UpgradeConditionType>
            {
                // Tactical conditions
                UpgradeConditionType.ComboCountReached,
                UpgradeConditionType.PlayedInStance,
                UpgradeConditionType.PlayedMultipleTimesInTurn,
                UpgradeConditionType.ZeroCostCardsThisTurn,
                UpgradeConditionType.DamageDealtInSingleTurn,
                UpgradeConditionType.PlayedWithCombo,
                UpgradeConditionType.ComboUseBackToBack,
                UpgradeConditionType.FinalCardInHand,
                UpgradeConditionType.OnlyCardPlayedThisTurn,
                UpgradeConditionType.PlayedAtHighHealth,
                UpgradeConditionType.BattleLengthOver
            },
            CardRarity.Rare => new List<UpgradeConditionType>
            {
                // Advanced conditions
                UpgradeConditionType.PerfectionStreakAchieved,
                UpgradeConditionType.WonFightUsingCard,
                UpgradeConditionType.PlayedAsFinisher,
                UpgradeConditionType.PerfectTurnPlayed,
                UpgradeConditionType.SurvivedFightWithCard,
                UpgradeConditionType.DefeatedOpponentWithCard,
                UpgradeConditionType.SurvivedStatusEffect,
                UpgradeConditionType.AllCardsCostLowEnough,
                UpgradeConditionType.DeckSizeBelow,
                UpgradeConditionType.FamiliarNameInDeck,
                UpgradeConditionType.OnlyCardTypeInDeck
            },
            _ => new List<UpgradeConditionType> { UpgradeConditionType.TimesPlayedThisFight }
        };
    }
    
    /// <summary>
    /// Generate required value for a specific upgrade condition
    /// </summary>
    private int GenerateRequiredValueForCondition(UpgradeConditionType conditionType, CardRarity rarity)
    {
        return conditionType switch
        {
            UpgradeConditionType.TimesPlayedThisFight => rarity == CardRarity.Common ? 
                UnityEngine.Random.Range(2, 4) : UnityEngine.Random.Range(3, 6),
            UpgradeConditionType.DamageDealtThisFight => UnityEngine.Random.Range(10, 25),
            UpgradeConditionType.DamageDealtInSingleTurn => UnityEngine.Random.Range(8, 15),
            UpgradeConditionType.ComboCountReached => UnityEngine.Random.Range(3, 6),
            UpgradeConditionType.CopiesInDeck => UnityEngine.Random.Range(2, 4),
            UpgradeConditionType.HealingGivenThisFight => UnityEngine.Random.Range(8, 20),
            _ => UnityEngine.Random.Range(1, 4)
        };
    }
    
    /// <summary>
    /// Create fallback effect when budget generation fails
    /// </summary>
    private List<ProposedCardEffect> CreateFallbackEffect(CardBudgetBreakdown budget)
    {
        return new List<ProposedCardEffect>
        {
            new ProposedCardEffect
            {
                effectType = CardEffectType.Damage,
                amount = 3,
                targetType = CardTargetType.Opponent,
                conditionalType = ConditionalType.None,

            }
        };
    }
    
    /// <summary>
    /// Create actual CardData object from generation proposal
    /// </summary>
    private CardData CreateCardDataFromProposal(CardBudgetBreakdown budget, List<ProposedCardEffect> proposedEffects, bool hasUpgrade)
    {
        var cardData = ScriptableObject.CreateInstance<CardData>();
        
        // Set basic properties
        cardData.SetCardId(GenerateUniqueCardId());
        cardData.SetRarity(budget.rarity);
        cardData.SetEnergyCost(budget.energyCost);
        cardData.SetCardCategory(CardCategory.Draftable);
        cardData.SetInitiative(GenerateInitiative(budget.rarity));
        cardData.SetCardType(DetermineCardType(proposedEffects));
        
        // Convert proposed effects to actual CardEffect objects
        cardData.SetEffects(ConvertProposedEffects(proposedEffects));
        
        // Set up upgrade system if needed
        if (hasUpgrade)
        {
            var (conditionType, requiredValue) = GenerateRandomUpgradeCondition(budget.rarity);
            bool upgradeAllCopies = UnityEngine.Random.value < 0.3f; // 30% chance
            cardData.SetUpgradeProperties(true, conditionType, requiredValue, UpgradeComparisonType.GreaterThanOrEqual);
        }
        
        // Set tracking flags (always enabled for generated cards to support upgrade conditions)
        SetTrackingFlags(cardData);
        
        // Randomly add combo mechanics
        ApplyComboMechanics(cardData, budget.rarity);
        
        return cardData;
    }
    
    /// <summary>
    /// Set tracking flags for a generated card
    /// </summary>
    private void SetTrackingFlags(CardData cardData)
    {
        // The tracking flags (_trackPlayCount and _trackDamageHealing) are private fields
        // in CardData and are enabled by default. Since generated cards need tracking
        // for upgrade conditions and scaling, we rely on CardData's default behavior
        // which enables tracking by default for all cards.
        
        // Note: If tracking needs to be explicitly controlled, CardData would need
        // public setter methods for these properties.
    }
    
    /// <summary>
    /// Apply combo mechanics to a card based on rarity and type
    /// </summary>
    private void ApplyComboMechanics(CardData cardData, CardRarity rarity)
    {
        // Higher rarity cards are more likely to have combo mechanics
        float comboChance = rarity switch
        {
            CardRarity.Common => 0.15f,     // 15% chance
            CardRarity.Uncommon => 0.25f,   // 25% chance  
            CardRarity.Rare => 0.40f,       // 40% chance
            _ => 0.15f
        };
        
        if (UnityEngine.Random.value < comboChance)
        {
            // Decide between building combo or requiring combo
            if (UnityEngine.Random.value < 0.7f) // 70% chance to build combo
            {
                cardData.MakeComboCard();
            }
            else // 30% chance to require combo
            {
                int requiredCombo = rarity switch
                {
                    CardRarity.Common => UnityEngine.Random.Range(1, 3),
                    CardRarity.Uncommon => UnityEngine.Random.Range(2, 4),
                    CardRarity.Rare => UnityEngine.Random.Range(3, 6),
                    _ => 1
                };
                cardData.RequireCombo(requiredCombo);
            }
        }
    }
    
    /// <summary>
    /// Generate a unique card ID
    /// </summary>
    private int GenerateUniqueCardId()
    {
        // Generate based on timestamp and random to ensure uniqueness
        return System.DateTime.Now.GetHashCode() + UnityEngine.Random.Range(1000, 9999);
    }
    
    /// <summary>
    /// Generate initiative based on rarity (rarer cards can have higher initiative)
    /// </summary>
    private int GenerateInitiative(CardRarity rarity)
    {
        return rarity switch
        {
            CardRarity.Common => UnityEngine.Random.Range(0, 3),
            CardRarity.Uncommon => UnityEngine.Random.Range(1, 5),
            CardRarity.Rare => UnityEngine.Random.Range(2, 7),
            _ => 0
        };
    }
    
    /// <summary>
    /// Determine card type based on the effects it has
    /// </summary>
    private CardType DetermineCardType(List<ProposedCardEffect> effects)
    {
        if (effects.Any(e => e.effectType == CardEffectType.Damage))
            return CardType.Attack;
        
        if (effects.Any(e => e.effectType == CardEffectType.Heal || e.effectType == CardEffectType.ApplyShield))
            return CardType.Skill;
        
        if (effects.Any(e => e.effectType == CardEffectType.DrawCard))
            return CardType.Spell;
        
        if (effects.Any(e => e.effectType == CardEffectType.EnterStance || e.effectType == CardEffectType.ExitStance))
            return CardType.Stance;
        
        return CardType.Skill; // Default
    }
    
    /// <summary>
    /// Convert proposed effects to actual CardEffect objects
    /// </summary>
    private List<CardEffect> ConvertProposedEffects(List<ProposedCardEffect> proposedEffects)
    {
        var effects = new List<CardEffect>();
        
        foreach (var proposed in proposedEffects)
        {
            var effect = new CardEffect
            {
                effectType = proposed.effectType,
                amount = proposed.amount,
                targetType = proposed.targetType,
                duration = proposed.duration,
                // elementalType removed - elemental system removed
                conditionType = proposed.conditionalType
            };
            
            // Set appropriate condition values if conditional
            if (proposed.conditionalType != ConditionalType.None)
            {
                effect.conditionValue = GenerateConditionValue(proposed.conditionalType);
                
                // 25% chance for conditional effects to have alternative effects
                if (UnityEngine.Random.value < 0.25f)
                {
                    ApplyAlternativeEffect(effect);
                }
                
                // Special handling for stance-based conditionals to add stance exit
                if (effect.conditionType == ConditionalType.IfInStance && UnityEngine.Random.value < 0.5f)
                {
                    effect.shouldExitStance = true;
                }
            }
            
            // 20% chance for effects to have scaling (only for scalable effects)
            if (UnityEngine.Random.value < 0.20f && CanEffectScale(effect.effectType))
            {
                ApplyScalingEffect(effect);
            }
            
            // Set appropriate animation behavior
            effect.animationBehavior = GetAnimationBehavior(effect.effectType);
            
            effects.Add(effect);
        }
        
        return effects;
    }
    
    /// <summary>
    /// Apply alternative effect to a conditional effect
    /// </summary>
    private void ApplyAlternativeEffect(CardEffect effect)
    {
        effect.hasAlternativeEffect = true;
        
        // Randomly choose logic type (favor Replace for balance)
        effect.alternativeLogic = UnityEngine.Random.value < 0.7f ? 
            AlternativeEffectLogic.Replace : AlternativeEffectLogic.Additional;
        
        // Generate a reasonable alternative effect
        var alternativeEffects = new[]
        {
            CardEffectType.DrawCard,
            CardEffectType.ApplyShield,
            CardEffectType.Heal
        };
        
        effect.alternativeEffectType = alternativeEffects[UnityEngine.Random.Range(0, alternativeEffects.Length)];
        effect.alternativeEffectAmount = UnityEngine.Random.Range(1, 4);
    }
    
    /// <summary>
    /// Check if an effect type can logically scale
    /// </summary>
    private bool CanEffectScale(CardEffectType effectType)
    {
        return effectType switch
        {
            // Effects that make sense to scale
            CardEffectType.Damage => true,
            CardEffectType.Heal => true,
            // DrawCard removed per requirements
            CardEffectType.ApplyShield => true,
            CardEffectType.ApplyThorns => true,
            CardEffectType.ApplyStrength => true,
            CardEffectType.ApplySalve => true,
            CardEffectType.ApplyWeak => true,
            CardEffectType.ApplyBreak => true,
            CardEffectType.ApplyBurn => true,
            CardEffectType.ApplyCurse => true,
            
            // Effects that don't make sense to scale
            CardEffectType.ExitStance => false,          // Can't exit stance multiple times
            CardEffectType.EnterStance => false,         // Entering stance is binary
            CardEffectType.ApplyStun => false,           // Stun duration doesn't scale well

            CardEffectType.RaiseCriticalChance => false, // Percentage scaling would be weird
            
            _ => false // Default to no scaling for unknown effects
        };
    }
    
    /// <summary>
    /// Apply scaling effect to make the effect scale with game state
    /// </summary>
    private void ApplyScalingEffect(CardEffect effect)
    {
        // Choose appropriate scaling types based on effect type
        var scalingTypes = effect.effectType switch
        {
            CardEffectType.Damage => new[] 
            { 
                ScalingType.ComboCount, 
                ScalingType.DamageDealtThisTurn, 
                ScalingType.MissingHealth,
                ScalingType.CardsPlayedThisTurn
            },
            CardEffectType.Heal => new[] 
            { 
                ScalingType.CurrentHealth, 
                ScalingType.HandSize, 
                ScalingType.CardsPlayedThisTurn
            },
            // DrawCard removed per requirements

            _ => new[] { ScalingType.ComboCount, ScalingType.CardsPlayedThisTurn }
        };
        
        effect.scalingType = scalingTypes[UnityEngine.Random.Range(0, scalingTypes.Length)];
        effect.scalingMultiplier = UnityEngine.Random.Range(0.5f, 2.0f);
        effect.maxScaling = UnityEngine.Random.Range(5, 15);
    }
    
    /// <summary>
    /// Get appropriate animation behavior for effect type
    /// </summary>
    private EffectAnimationBehavior GetAnimationBehavior(CardEffectType effectType)
    {
        return effectType switch
        {
            CardEffectType.Damage => UnityEngine.Random.value < 0.7f ? 
                EffectAnimationBehavior.ProjectileFromSource : EffectAnimationBehavior.InstantOnTarget,
            CardEffectType.Heal => EffectAnimationBehavior.OnSourceOnly,

            
            // DrawCard removed per requirements

            CardEffectType.EnterStance => EffectAnimationBehavior.OnSourceOnly,
            CardEffectType.ExitStance => EffectAnimationBehavior.OnSourceOnly,
            CardEffectType.ApplyStun => EffectAnimationBehavior.InstantOnTarget,
            _ => EffectAnimationBehavior.Auto
        };
    }
    
    /// <summary>
    /// Generate appropriate condition value for a conditional type
    /// </summary>
    private int GenerateConditionValue(ConditionalType conditionType)
    {
        return conditionType switch
        {
            // Health-based conditions (multiples of 5)
            ConditionalType.IfTargetHealthBelow => GetRandomMultipleOf5(25, 60),
            ConditionalType.IfTargetHealthAbove => GetRandomMultipleOf5(50, 80),
            ConditionalType.IfSourceHealthBelow => GetRandomMultipleOf5(25, 50),
            ConditionalType.IfSourceHealthAbove => GetRandomMultipleOf5(60, 90),
            
            // Card count conditions
            ConditionalType.IfCardsInHand => UnityEngine.Random.Range(1, 4),
            ConditionalType.IfCardsInDeck => UnityEngine.Random.Range(5, 15),
            ConditionalType.IfCardsInDiscard => UnityEngine.Random.Range(3, 8),
            
            // Combat performance conditions
            ConditionalType.IfTimesPlayedThisFight => UnityEngine.Random.Range(2, 4),
            ConditionalType.IfDamageTakenThisFight => UnityEngine.Random.Range(10, 30),
            ConditionalType.IfDamageTakenLastRound => UnityEngine.Random.Range(5, 15),
            ConditionalType.IfHealingReceivedThisFight => UnityEngine.Random.Range(5, 20),
            ConditionalType.IfHealingReceivedLastRound => UnityEngine.Random.Range(3, 10),
            ConditionalType.IfPerfectionStreak => UnityEngine.Random.Range(2, 4),
            
            // Tactical conditions
            ConditionalType.IfComboCount => UnityEngine.Random.Range(2, 5),
            ConditionalType.IfZeroCostCardsThisTurn => UnityEngine.Random.Range(1, 3),
            ConditionalType.IfZeroCostCardsThisFight => UnityEngine.Random.Range(3, 6),
            // IfEnergyRemaining removed per requirements
            
            // Special conditions (stance and card type will need special handling)
            ConditionalType.IfInStance => 1, // Binary condition
            ConditionalType.IfLastCardType => 1, // Binary condition
            
            _ => UnityEngine.Random.Range(1, 5)
        };
    }
    
    /// <summary>
    /// Generate procedural name and description for the card
    /// </summary>
    private void GenerateCardNameAndDescription(CardData cardData, List<ProposedCardEffect> effects)
    {
        string name = GenerateCardName(effects, cardData.Rarity);
        
        // Convert proposed effects to actual effects first to get proper condition values
        var actualEffects = ConvertProposedEffects(effects);
        string description = GenerateCardDescription(actualEffects);
        
        cardData.SetCardName(name);
        cardData.SetDescription(description);
    }
    
    /// <summary>
    /// Generate a mystical card name based on comprehensive effect analysis
    /// </summary>
    private string GenerateCardName(List<ProposedCardEffect> effects, CardRarity rarity)
    {
        // Analyze the card's complete effect profile
        var profile = AnalyzeCardProfile(effects);
        
        // Generate name based on card profile
        string baseName = GenerateProfileBasedName(profile, rarity);
        
        // Apply final naming enhancements
        return ApplyNamingEnhancements(baseName, profile, rarity);
    }

    /// <summary>
    /// Analyze all effects to create a comprehensive card profile
    /// </summary>
    private CardProfile AnalyzeCardProfile(List<ProposedCardEffect> effects)
    {
        var profile = new CardProfile();
        
        // Categorize effects
        foreach (var effect in effects)
        {
            switch (effect.effectType)
            {
                case CardEffectType.Damage:
                    profile.damageEffects.Add(effect);
                    profile.totalDamage += effect.amount;
                    break;
                case CardEffectType.Heal:
                    profile.healingEffects.Add(effect);
                    profile.totalHealing += effect.amount;
                    break;
                case CardEffectType.DrawCard:

                case CardEffectType.ApplyShield:
                case CardEffectType.ApplyThorns:
                case CardEffectType.ApplyWeak:
                case CardEffectType.ApplyBreak:
                case CardEffectType.ApplyBurn:
                case CardEffectType.ApplySalve:
                case CardEffectType.ApplyStun:
                case CardEffectType.ApplyStrength:
                case CardEffectType.ApplyCurse:
                case CardEffectType.RaiseCriticalChance:


                case CardEffectType.EnterStance:
                case CardEffectType.ExitStance:
                    profile.statusEffects.Add(effect);
                    break;

            }
            
            // Track elemental types
            // Elemental system removed - no elemental types to track
            
            // Track conditional effects
            if (effect.conditionalType != ConditionalType.None)
            {
                profile.conditionalEffects.Add(effect);
            }
            
            // Track targets
            if (!profile.targetTypes.Contains(effect.targetType))
            {
                profile.targetTypes.Add(effect.targetType);
            }
        }
        
        // Determine primary archetype
        profile.primaryArchetype = DeterminePrimaryArchetype(profile);
        
        // Analyze effect combinations
        profile.effectCombination = AnalyzeEffectCombination(profile);
        
        return profile;
    }

    /// <summary>
    /// Determine the primary archetype of the card
    /// </summary>
    private CardArchetype DeterminePrimaryArchetype(CardProfile profile)
    {
        int damageCount = profile.damageEffects.Count;
        int healingCount = profile.healingEffects.Count;
        int statusCount = profile.statusEffects.Count;
        int utilityCount = profile.utilityEffects.Count;
        
        // Multi-effect archetypes
        if (damageCount > 0 && healingCount > 0) return CardArchetype.LifeDrain;
        if (damageCount > 0 && statusCount > 0) return CardArchetype.BattleMage;
        if (healingCount > 0 && statusCount > 0) return CardArchetype.SupportHealer;
        if (damageCount > 0 && utilityCount > 0) return CardArchetype.TacticalStrike;
        if (statusCount > 1) return CardArchetype.StatusWeaver;
        if (profile.conditionalEffects.Count > 0) return CardArchetype.ConditionalCaster;
        
        // Single-effect archetypes
        if (damageCount > 0) return CardArchetype.PureDamage;
        if (healingCount > 0) return CardArchetype.PureHealing;
        if (statusCount > 0) return CardArchetype.PureStatus;
        if (utilityCount > 0) return CardArchetype.PureUtility;
        
        return CardArchetype.PureDamage; // Fallback
    }

    /// <summary>
    /// Analyze how effects combine together
    /// </summary>
    private EffectCombination AnalyzeEffectCombination(CardProfile profile)
    {
        // Multi-target combinations
        if (profile.targetTypes.Contains(CardTargetType.Self) && 
            (profile.targetTypes.Contains(CardTargetType.Opponent) || profile.targetTypes.Contains(CardTargetType.Random)))
            return EffectCombination.SelfAndEnemy;
        
        // Conditional combinations
        if (profile.conditionalEffects.Count > 0)
        {
            if (profile.conditionalEffects.Any(e => e.conditionalType == ConditionalType.IfTargetHealthBelow))
                return EffectCombination.ExecutionTheme;
            if (profile.conditionalEffects.Any(e => e.conditionalType == ConditionalType.IfSourceHealthBelow))
                return EffectCombination.DesperationTheme;
            if (profile.conditionalEffects.Any(e => e.conditionalType == ConditionalType.IfComboCount))
                return EffectCombination.ComboTheme;
            return EffectCombination.ConditionalTheme;
        }
        
        // Elemental combinations
        if (profile.elementalTypes.Count > 1) return EffectCombination.MultiElemental;
        if (profile.elementalTypes.Count == 1) return EffectCombination.SingleElemental;
        
        // Effect quantity combinations
        if (profile.damageEffects.Count + profile.healingEffects.Count + profile.statusEffects.Count + profile.utilityEffects.Count > 2)
            return EffectCombination.MultiEffect;
        
        return EffectCombination.SimpleEffect;
    }

    /// <summary>
    /// Generate name based on the analyzed card profile
    /// </summary>
    private string GenerateProfileBasedName(CardProfile profile, CardRarity rarity)
    {
        // Get archetype-specific name components
        var nameComponents = GetArchetypeNameComponents(profile.primaryArchetype);
        
        // Apply combination-specific modifiers
        nameComponents = ApplyCombinationModifiers(nameComponents, profile.effectCombination);
        
        // Apply elemental theming
        if (profile.elementalTypes.Count > 0)
        {
            nameComponents = ApplyElementalTheming(nameComponents, profile.elementalTypes);
        }
        
        // Construct the name
        string baseName = ConstructNameFromComponents(nameComponents, rarity);
        
        return baseName;
    }

    /// <summary>
    /// Get name components specific to card archetype
    /// </summary>
    private NameComponents GetArchetypeNameComponents(CardArchetype archetype)
    {
        return archetype switch
        {
            CardArchetype.PureDamage => new NameComponents
            {
                primaryWords = new[] { "Annihilation", "Devastation", "Sundering", "Reckoning", "Strike", "Blast" },
                modifiers = new[] { "Brutal", "Savage", "Merciless", "Crushing", "Rending" }
            },
            CardArchetype.PureHealing => new NameComponents
            {
                primaryWords = new[] { "Restoration", "Benediction", "Renewal", "Revival", "Mending", "Recovery" },
                modifiers = new[] { "Divine", "Sacred", "Blessed", "Pure", "Radiant" }
            },
            CardArchetype.LifeDrain => new NameComponents
            {
                primaryWords = new[] { "Siphoning", "Leech", "Drain", "Vampirism", "Absorption" },
                modifiers = new[] { "Parasitic", "Draining", "Consuming", "Hungering", "Thirsting" }
            },
            CardArchetype.BattleMage => new NameComponents
            {
                primaryWords = new[] { "Spellblade", "Enchantment", "Sorcery", "Wizardry", "Arcanum" },
                modifiers = new[] { "Spellwoven", "Enchanted", "Arcane", "Mystical", "Magical" }
            },
            CardArchetype.StatusWeaver => new NameComponents
            {
                primaryWords = new[] { "Hexweaving", "Affliction", "Condition", "Manipulation", "Weaving" },
                modifiers = new[] { "Twisted", "Layered", "Complex", "Intricate", "Woven" }
            },
            CardArchetype.ConditionalCaster => new NameComponents
            {
                primaryWords = new[] { "Contingency", "Response", "Reaction", "Adaptation", "Trigger" },
                modifiers = new[] { "Adaptive", "Responsive", "Situational", "Triggered", "Conditional" }
            },
            CardArchetype.TacticalStrike => new NameComponents
            {
                primaryWords = new[] { "Assault", "Strike", "Attack", "Maneuver", "Technique" },
                modifiers = new[] { "Calculated", "Strategic", "Tactical", "Measured", "Precise" }
            },
            CardArchetype.SupportHealer => new NameComponents
            {
                primaryWords = new[] { "Care", "Protection", "Warding", "Shielding", "Blessing" },
                modifiers = new[] { "Protective", "Warding", "Shielding", "Guardian", "Defensive" }
            },
            _ => new NameComponents
            {
                primaryWords = new[] { "Mysterious Effect", "Unknown Power", "Arcane Force", "Hidden Strength", "Secret Art" },
                modifiers = new[] { "Mysterious", "Unknown", "Hidden", "Secret", "Veiled" }
            }
        };
    }

    /// <summary>
    /// Apply combination-specific modifiers to name components
    /// </summary>
    private NameComponents ApplyCombinationModifiers(NameComponents components, EffectCombination combination)
    {
        var modifiedComponents = new NameComponents
        {
            primaryWords = components.primaryWords,
            modifiers = components.modifiers
        };
        
        switch (combination)
        {
            case EffectCombination.MultiEffect:
                modifiedComponents.suffixes = new[] { "of Many Effects", "of Cascading Power", "of Layered Magic", "of Complex Weaving" };
                break;
            case EffectCombination.ExecutionTheme:
                modifiedComponents.suffixes = new[] { "of Final Judgment", "of the Killing Blow", "of Merciful End", "of Swift Conclusion" };
                break;
            case EffectCombination.DesperationTheme:
                modifiedComponents.suffixes = new[] { "of Last Resort", "of Desperate Hour", "of Final Stand", "of Breaking Point" };
                break;
            case EffectCombination.ComboTheme:
                modifiedComponents.suffixes = new[] { "of Flowing Strikes", "of Chain Reaction", "of Cascading Blows", "of Linked Power" };
                break;
            case EffectCombination.SelfAndEnemy:
                modifiedComponents.suffixes = new[] { "of Dual Purpose", "of Twin Effects", "of Balanced Force", "of Mirrored Power" };
                break;
            case EffectCombination.MultiElemental:
                modifiedComponents.suffixes = new[] { "of Elemental Fusion", "of Primal Convergence", "of Mixed Elements", "of Chaotic Forces" };
                break;
        }
        
        return modifiedComponents;
    }

    /// <summary>
    /// Apply elemental theming to name components (deprecated - elemental system removed)
    /// </summary>
    private NameComponents ApplyElementalTheming(NameComponents components, List<object> elementalTypes)
    {
        // Elemental system removed - return components unchanged
        return components;
    }

    /// <summary>
    /// Construct the final name from components (limited to 2 words, 3 on rare occasions)
    /// </summary>
    private string ConstructNameFromComponents(NameComponents components, CardRarity rarity)
    {
        string primaryWord = components.primaryWords[UnityEngine.Random.Range(0, components.primaryWords.Length)];
        
        // Count current words in primary word
        int currentWordCount = primaryWord.Split(' ').Length;
        
        // Simple name for common cards or if primary word is already multi-word
        if ((rarity == CardRarity.Common && UnityEngine.Random.value < 0.6f) || currentWordCount >= 2)
        {
            return primaryWord;
        }
        
        // Add modifiers for higher rarity or random chance (but respect word limit)
        if (rarity != CardRarity.Common || UnityEngine.Random.value < 0.4f)
        {
            if (UnityEngine.Random.value < 0.7f && components.modifiers.Length > 0 && currentWordCount < 2)
            {
                string modifier = components.modifiers[UnityEngine.Random.Range(0, components.modifiers.Length)];
                primaryWord = $"{modifier} {primaryWord}";
                currentWordCount++;
            }
            
            // Add suffixes very rarely and only for rare cards (3 word limit)
            if (rarity == CardRarity.Rare && UnityEngine.Random.value < 0.1f && // Only 10% chance for rare cards
                components.suffixes != null && components.suffixes.Length > 0 && currentWordCount < 3)
            {
                // Use shorter suffixes to avoid overly long names
                string[] shortSuffixes = { "of Dread", "of Power", "of Light", "of Shadow", "of Fire", "of Ice" };
                string suffix = shortSuffixes[UnityEngine.Random.Range(0, shortSuffixes.Length)];
                primaryWord = $"{primaryWord} {suffix}";
            }
        }
        
        return primaryWord;
    }

    /// <summary>
    /// Apply final naming enhancements based on profile (respecting word limits)
    /// </summary>
    private string ApplyNamingEnhancements(string baseName, CardProfile profile, CardRarity rarity)
    {
        // Count current words in base name
        int currentWordCount = baseName.Split(' ').Length;
        
        // Don't enhance if already at word limit
        if (currentWordCount >= 2) return baseName;
        
        // Add mystical enhancement for complex cards or rare cards
        bool shouldEnhance = rarity == CardRarity.Rare || 
                            profile.effectCombination != EffectCombination.SimpleEffect ||
                            profile.conditionalEffects.Count > 0 ||
                            profile.elementalTypes.Count > 1;
        
        if (shouldEnhance && UnityEngine.Random.value < 0.3f) // Reduced chance to keep names shorter
        {
            // Only add single word enhancements
            if (currentWordCount == 1)
            {
                if (UnityEngine.Random.value < 0.5f)
                {
                    // Add a simple prefix
                    string[] shortPrefixes = { "Dark", "Bright", "Swift", "Hidden", "Ancient", "Mystic" };
                    string prefix = shortPrefixes[UnityEngine.Random.Range(0, shortPrefixes.Length)];
                    baseName = $"{prefix} {baseName}";
                }
                else
                {
                    // Add a simple suffix (only for rare cards and very rarely)
                    if (rarity == CardRarity.Rare && UnityEngine.Random.value < 0.1f)
                    {
                        string[] shortSuffixes = { "Strike", "Force", "Power", "Art", "Skill", "Talent" };
                        string suffix = shortSuffixes[UnityEngine.Random.Range(0, shortSuffixes.Length)];
                        baseName = $"{baseName} {suffix}";
                    }
                }
            }
        }
        
        return baseName;
    }
    
    /// <summary>
    /// Generate a procedural description for the card
    /// </summary>
    private string GenerateCardDescription(List<CardEffect> effects)
    {
        var descriptions = new List<string>();
        
        foreach (var effect in effects)
        {
            string effectDesc = effect.effectType switch
            {
                // Core effects
                CardEffectType.Damage => GenerateDamageDescription(effect),
                CardEffectType.Heal => GenerateHealDescription(effect),
                // DrawCard removed per requirements
                // Note: RestoreEnergy removed from generation per requirements
                
                // Defensive buffs
                CardEffectType.ApplyShield => GenerateStatusDescription(effect, "Shield"),
                CardEffectType.ApplyThorns => GenerateStatusDescription(effect, "Thorns"),
                
                // Positive status effects
                CardEffectType.ApplyStrength => GenerateStatusDescription(effect, "Strength"),
                CardEffectType.ApplySalve => GenerateStatusDescription(effect, "Salve"),
                CardEffectType.RaiseCriticalChance => $"Increase critical chance by {effect.amount}%",

                
                // Negative status effects
                CardEffectType.ApplyWeak => GenerateStatusDescription(effect, "Weak", isNegative: true),
                CardEffectType.ApplyBreak => GenerateStatusDescription(effect, "Break", isNegative: true),
                CardEffectType.ApplyBurn => GenerateStatusDescription(effect, "Burn", isNegative: true),
                CardEffectType.ApplyStun => GenerateStunDescription(effect),
                CardEffectType.ApplyCurse => GenerateStatusDescription(effect, "Curse", isNegative: true),
                
                // Card manipulation

                
                // Elemental effects

                
                // Stance effects
                CardEffectType.EnterStance => "Enter combat stance",
                CardEffectType.ExitStance => "Exit current stance",
                
                // Fallback for any missing effects
                _ => effect.effectType.ToString().Replace("Apply", "Apply ")
            };
            
            // Add stance exit modifier if this effect should exit stance
            if (effect.shouldExitStance)
            {
                effectDesc += " and exit current stance";
            }
            
            // Add scaling information in parentheses within the same sentence
            if (effect.scalingType != ScalingType.None)
            {
                string scalingDesc = GetScalingDescription(effect);
                effectDesc = $"{effectDesc} ({scalingDesc})";
            }
            
            // Add conditional clause if present with specific condition
            if (effect.conditionType != ConditionalType.None)
            {
                string conditionDesc = GetConditionDescription(effect.conditionType, effect);
                
                // Add alternative effect if present
                if (effect.hasAlternativeEffect)
                {
                    string altDesc = GetAlternativeEffectDescription(effect);
                    if (effect.alternativeLogic == AlternativeEffectLogic.Replace)
                    {
                        effectDesc = $"{conditionDesc}: {effectDesc}, otherwise {altDesc}";
                    }
                    else // Additional
                    {
                        effectDesc = $"{conditionDesc}: {effectDesc} and {altDesc}";
                    }
                }
                else
                {
                    effectDesc = $"{conditionDesc}: {effectDesc}";
                }
            }
            
            descriptions.Add(effectDesc);
        }
        
        return string.Join(". ", descriptions) + ".";
    }
    
    /// <summary>
    /// Generate description for damage effects with proper targeting
    /// </summary>
    private string GenerateDamageDescription(CardEffect effect)
    {
        string base_desc = $"Deal {effect.amount} damage";
        string targetDesc = GetTargetDescription(effect.targetType);
        
        // Always specify target for damage (damage to self would be unusual and should be explicit)
        if (!string.IsNullOrEmpty(targetDesc) && effect.targetType != CardTargetType.Opponent)
        {
            base_desc += $" {targetDesc}";
        }
        
        return base_desc;
    }
    
    /// <summary>
    /// Generate description for heal effects with proper targeting and wording
    /// </summary>
    private string GenerateHealDescription(CardEffect effect)
    {
        string base_desc;
        
        if (effect.targetType == CardTargetType.Self)
        {
            base_desc = $"Heal {effect.amount} health";
        }
        else if (effect.targetType == CardTargetType.Opponent)
        {
            base_desc = $"Heal enemy for {effect.amount}";
        }
        else if (effect.targetType == CardTargetType.Ally)
        {
            base_desc = $"Heal ally for {effect.amount}";
        }
        else
        {
            // Fallback for other target types
            base_desc = $"Heal {effect.amount} health";
            string targetDesc = GetTargetDescription(effect.targetType);
            if (!string.IsNullOrEmpty(targetDesc))
            {
                base_desc += $" {targetDesc}";
            }
        }
        
        return base_desc;
    }
    
    // DrawCard effects removed per requirements
    
    /// <summary>
    /// Generate description for status effects with proper targeting and wording
    /// </summary>
    private string GenerateStatusDescription(CardEffect effect, string statusName, bool isNegative = false)
    {
        string base_desc;
        
        if (isNegative)
        {
            if (effect.targetType == CardTargetType.Self)
            {
                // Use "Suffer" for negative effects on self
                base_desc = $"Suffer {effect.amount} {statusName}";
            }
            else
            {
                base_desc = $"Apply {effect.amount} {statusName}";
                string targetDesc = GetTargetDescription(effect.targetType);
                
                // Always specify target for negative effects when not targeting enemies
                if (!string.IsNullOrEmpty(targetDesc) && effect.targetType != CardTargetType.Opponent)
                {
                    base_desc += $" {targetDesc}";
                }
                else if (effect.targetType == CardTargetType.Opponent)
                {
                    base_desc += " to enemy";
                }
            }
        }
        else
        {
            if (effect.targetType == CardTargetType.Self)
            {
                // Use "Gain" for positive effects on self
                base_desc = $"Gain {effect.amount} {statusName}";
            }
            else
            {
                base_desc = $"Grant {effect.amount} {statusName}";
                string targetDesc = GetTargetDescription(effect.targetType);
                
                // Always specify target for buffs when not targeting self
                if (!string.IsNullOrEmpty(targetDesc) && effect.targetType != CardTargetType.Self)
                {
                    base_desc += $" {targetDesc}";
                }
            }
        }
        
        return base_desc;
    }
    
    /// <summary>
    /// Generate description for stun effects with proper targeting
    /// </summary>
    private string GenerateStunDescription(CardEffect effect)
    {
        string base_desc;
        
        if (effect.targetType == CardTargetType.Self)
        {
            // Use "Suffer" for stunning self (negative effect)
            base_desc = effect.amount == 1 
                ? "Your next card fizzles" 
                : $"Your next {effect.amount} cards fizzle";
        }
        else if (effect.targetType == CardTargetType.Opponent)
        {
            base_desc = effect.amount == 1 
                ? "Enemy's next card fizzles" 
                : $"Enemy's next {effect.amount} cards fizzle";
        }
        else
        {
            // For ally targeting, be explicit
            if (effect.targetType == CardTargetType.Ally)
            {
                base_desc = effect.amount == 1 
                    ? "Ally's next card fizzles" 
                    : $"Ally's next {effect.amount} cards fizzle";
            }
            else
            {
                base_desc = effect.amount == 1 
                    ? "Next card fizzles" 
                    : $"Next {effect.amount} cards fizzle";
            }
        }
        
        return base_desc;
    }
    
    /// <summary>
    /// Get a human-readable description of the target type
    /// </summary>
    private string GetTargetDescription(CardTargetType targetType)
    {
        return targetType switch
        {
            CardTargetType.Self => "to self",
            CardTargetType.Ally => "to ally", 
            CardTargetType.Opponent => "to enemy",
            // Random targeting removed per requirements
            _ => ""
        };
    }
    
    /// <summary>
    /// Get a human-readable description of the conditional type
    /// </summary>
    private string GetConditionDescription(ConditionalType conditionalType, CardEffect actualEffect)
    {
        // Use the actual condition value from the effect
        int conditionValue = actualEffect.conditionValue;
        
        return conditionalType switch
        {
            // Health-based conditions - convert percentage to actual health values
            ConditionalType.IfTargetHealthBelow => $"If target health is below {ConvertHealthPercentToValue(conditionValue)}",
            ConditionalType.IfTargetHealthAbove => $"If target health is above {ConvertHealthPercentToValue(conditionValue)}",
            ConditionalType.IfSourceHealthBelow => $"If your health is below {ConvertHealthPercentToValue(conditionValue)}",
            ConditionalType.IfSourceHealthAbove => $"If your health is above {ConvertHealthPercentToValue(conditionValue)}",
            
            // Card count conditions
            ConditionalType.IfCardsInHand => $"If you have {conditionValue}+ cards in hand",
            ConditionalType.IfCardsInDeck => $"If deck has {conditionValue}+ cards",
            ConditionalType.IfCardsInDiscard => $"If discard has {conditionValue}+ cards",
            
            // Combat performance conditions
            ConditionalType.IfTimesPlayedThisFight => $"If played {conditionValue}+ times this fight",
            ConditionalType.IfDamageTakenThisFight => $"If taken {conditionValue}+ damage this fight",
            ConditionalType.IfDamageTakenLastRound => $"If took {conditionValue}+ damage last round",
            ConditionalType.IfHealingReceivedThisFight => $"If healed for {conditionValue} or more this fight",
            ConditionalType.IfHealingReceivedLastRound => $"If healed for {conditionValue} or more last round",
            ConditionalType.IfPerfectionStreak => $"If {conditionValue}+ perfect turns",
            
            // Tactical conditions
            ConditionalType.IfComboCount => $"If this is your {GetOrdinalNumber(conditionValue)} card played this turn",
            ConditionalType.IfZeroCostCardsThisTurn => $"If played {conditionValue}+ zero-cost cards this turn",
            ConditionalType.IfZeroCostCardsThisFight => $"If played {conditionValue}+ zero-cost cards this fight",
            // IfEnergyRemaining removed per requirements
            
            // Special conditions
            ConditionalType.IfInStance => "If in combat stance",
            ConditionalType.IfLastCardType => "If last card was same type",
            
            _ => "If condition met"
        };
    }
    
    /// <summary>
    /// Convert health percentage values to actual health numbers for clearer descriptions
    /// </summary>
    private int ConvertHealthPercentToValue(int percentage)
    {
        // Assume a standard base health of 100 for consistent descriptions
        // This provides concrete numbers that are easier to understand than percentages
        const int standardBaseHealth = 100;
        return Mathf.RoundToInt(standardBaseHealth * percentage / 100f);
    }
    
    /// <summary>
    /// Get a random multiple of 5 within the specified range
    /// </summary>
    private int GetRandomMultipleOf5(int min, int max)
    {
        // Round min up to nearest multiple of 5
        int minMultiple = ((min + 4) / 5) * 5;
        // Round max down to nearest multiple of 5
        int maxMultiple = (max / 5) * 5;
        
        if (minMultiple > maxMultiple) return minMultiple;
        
        // Generate random multiple of 5 in range
        int numSteps = (maxMultiple - minMultiple) / 5 + 1;
        int randomStep = UnityEngine.Random.Range(0, numSteps);
        return minMultiple + (randomStep * 5);
    }
    
    /// <summary>
    /// Convert numbers to ordinal format (1st, 2nd, 3rd, 4th, etc.)
    /// </summary>
    private string GetOrdinalNumber(int number)
    {
        if (number <= 0) return number.ToString();
        
        switch (number % 100)
        {
            case 11:
            case 12:
            case 13:
                return number + "th";
        }
        
        switch (number % 10)
        {
            case 1:
                return number + "st";
            case 2:
                return number + "nd";
            case 3:
                return number + "rd";
            default:
                return number + "th";
        }
    }
    
    /// <summary>
    /// Get human-readable description of scaling with proper formatting
    /// </summary>
    private string GetScalingDescription(CardEffect effect)
    {
        int scalingValue = effect.scalingMultiplier > 0 ? Mathf.RoundToInt(effect.scalingMultiplier) : 1;
        
        return effect.scalingType switch
        {
            ScalingType.ComboCount => $"+{scalingValue} for each combo count",
            ScalingType.HandSize => $"+{scalingValue} for each card in hand",
            ScalingType.CurrentHealth => $"+{scalingValue} for each current health",
            ScalingType.MissingHealth => $"+{scalingValue} for each missing health",
            ScalingType.CardsPlayedThisTurn => $"+{scalingValue} for each card played this turn",
            ScalingType.CardsPlayedThisFight => $"+{scalingValue} for each card played this fight",
            ScalingType.DamageDealtThisTurn => $"+{scalingValue} for each damage dealt this turn",
            ScalingType.DamageDealtThisFight => $"+{scalingValue} for each damage dealt this fight",
            ScalingType.ZeroCostCardsThisTurn => $"+{scalingValue} for each zero-cost card this turn",
            ScalingType.ZeroCostCardsThisFight => $"+{scalingValue} for each zero-cost card this fight",
            _ => $"+{scalingValue} per game state"
        };
    }
    
    /// <summary>
    /// Get description for alternative effect
    /// </summary>
    private string GetAlternativeEffectDescription(CardEffect effect)
    {
        string altDesc = effect.alternativeEffectType switch
        {
            CardEffectType.Damage => $"deal {effect.alternativeEffectAmount} damage",
            CardEffectType.Heal => $"heal {effect.alternativeEffectAmount} health",
            CardEffectType.DrawCard => $"draw {effect.alternativeEffectAmount} card(s)",

            CardEffectType.ApplyShield => $"gain {effect.alternativeEffectAmount} shield",
            _ => effect.alternativeEffectType.ToString().ToLower()
        };
        
        return altDesc;
    }
    
    /// <summary>
    /// Create a duplicate of a card for deck repetition
    /// </summary>
    private CardData DuplicateCard(CardData original)
    {
        var duplicate = ScriptableObject.CreateInstance<CardData>();
        
        // Copy all properties using setter methods
        duplicate.SetCardName(original.CardName);
        duplicate.SetDescription(original.Description);
        duplicate.SetRarity(original.Rarity);
        duplicate.SetEnergyCost(original.EnergyCost);
        duplicate.SetCardType(original.CardType);
        duplicate.SetInitiative(original.Initiative);
        duplicate.SetEffects(new List<CardEffect>(original.Effects));
        
        // Copy upgrade properties if they exist
        if (original.CanUpgrade)
        {
            duplicate.SetUpgradeProperties(original.CanUpgrade, original.UpgradeConditionType, 
                original.UpgradeRequiredValue, original.UpgradeComparisonType);
        }
        
        // Generate new unique ID
        duplicate.SetCardId(GenerateUniqueCardId());
        
        return duplicate;
    }

    /// <summary>
    /// Determine the rarity for a unique card based on its position and target distribution
    /// </summary>
    private CardRarity DetermineRarityForUniqueCard(int cardIndex, int totalUniqueCards, RarityDistributionConfig distConfig)
    {
        // Calculate how many of each rarity we want in our unique cards
        float totalOriginalCards = distConfig.starterDeckCommons + distConfig.starterDeckUncommons + distConfig.starterDeckRares;
        
        int targetCommons = Mathf.RoundToInt((distConfig.starterDeckCommons / totalOriginalCards) * totalUniqueCards);
        int targetUncommons = Mathf.RoundToInt((distConfig.starterDeckUncommons / totalOriginalCards) * totalUniqueCards);
        int targetRares = totalUniqueCards - targetCommons - targetUncommons; // Remainder goes to rares
        
        // Ensure we have at least some distribution
        if (targetCommons == 0 && totalUniqueCards > 2) targetCommons = totalUniqueCards - 2;
        if (targetRares == 0 && totalUniqueCards > 1) targetRares = 1;
        if (targetUncommons == 0 && totalUniqueCards > targetCommons + targetRares) targetUncommons = totalUniqueCards - targetCommons - targetRares;
        
        // Distribute cards based on index
        if (cardIndex < targetCommons)
            return CardRarity.Common;
        else if (cardIndex < targetCommons + targetUncommons)
            return CardRarity.Uncommon;
        else
            return CardRarity.Rare;
    }
} 

/// <summary>
/// Enumeration of card archetypes for naming
/// </summary>
public enum CardArchetype
{
    PureDamage,
    PureHealing,
    PureStatus,
    PureUtility,
    LifeDrain,
    BattleMage,
    StatusWeaver,
    ConditionalCaster,
    TacticalStrike,
    SupportHealer
}

/// <summary>
/// Enumeration of effect combinations for naming
/// </summary>
public enum EffectCombination
{
    SimpleEffect,
    MultiEffect,
    ExecutionTheme,
    DesperationTheme,
    ComboTheme,
    ConditionalTheme,
    SelfAndEnemy,
    MultiElemental,
    SingleElemental
}

/// <summary>
/// Profile of a card's effects for naming analysis
/// </summary>
public class CardProfile
{
    public List<ProposedCardEffect> damageEffects = new List<ProposedCardEffect>();
    public List<ProposedCardEffect> healingEffects = new List<ProposedCardEffect>();
    public List<ProposedCardEffect> statusEffects = new List<ProposedCardEffect>();
    public List<ProposedCardEffect> utilityEffects = new List<ProposedCardEffect>();
    public List<ProposedCardEffect> conditionalEffects = new List<ProposedCardEffect>();
    
    public List<object> elementalTypes = new List<object>();
    public List<CardTargetType> targetTypes = new List<CardTargetType>();
    
    public int totalDamage = 0;
    public int totalHealing = 0;
    
    public CardArchetype primaryArchetype;
    public EffectCombination effectCombination;
}

/// <summary>
/// Name components for constructing card names
/// </summary>
public class NameComponents
{
    public string[] primaryWords = new string[0];
    public string[] modifiers = new string[0];
    public string[] suffixes = null;
} 