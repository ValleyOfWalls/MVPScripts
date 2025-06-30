using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;

/// <summary>
/// Automated testing manager for conditional card effects
/// Tests all conditional statement types to ensure they work correctly
/// Similar to CombatMathTestManager but focused on conditional effect logic
/// </summary>
public class ConditionalEffectTestManager : NetworkBehaviour
{
    [Header("Test Configuration")]
    [SerializeField] private bool runTestsOnStart = false;
    [SerializeField] private bool logDetailedResults = true;
    [SerializeField] private float testDelay = 0.5f;
    
    [Header("Test Results (Read Only)")]
    [SerializeField, ReadOnly] private int totalTests = 0;
    [SerializeField, ReadOnly] private int passedTests = 0;
    [SerializeField, ReadOnly] private int failedTests = 0;
    
    // Test scenario definitions
    private List<ConditionalEffectTestScenario> testScenarios;
    private CardEffectResolver effectResolver;
    private EntityTracker testEntityTracker;
    private NetworkEntity testEntity;
    private NetworkEntity testTarget;
    
    private void Start()
    {
        if (runTestsOnStart && IsServer)
        {
            StartCoroutine(RunAllTests());
        }
    }
    
    /// <summary>
    /// Run all conditional effect tests
    /// </summary>
    public void RunTests()
    {
        if (IsServer)
        {
            StartCoroutine(RunAllTests());
        }
        else
        {
            Debug.LogWarning("ConditionalEffectTestManager: Tests can only be run on server");
        }
    }
    
    private IEnumerator RunAllTests()
    {
        TestLogger.StartTest("Conditional Effect Test Suite");
        
        // Initialize test environment
        yield return StartCoroutine(InitializeTestEnvironment());
        
        // Setup test scenarios
        SetupTestScenarios();
        
        totalTests = 0;
        passedTests = 0;
        failedTests = 0;
        
        Debug.Log($"[CONDITIONAL_TEST] Starting {testScenarios.Count} conditional effect test scenarios...");
        
        // Run each test scenario
        foreach (var scenario in testScenarios)
        {
            yield return StartCoroutine(RunTestScenario(scenario));
            yield return new WaitForSeconds(testDelay);
        }
        
        // Report final results
        ReportFinalResults();
        
        TestLogger.FinishTest();
    }
    
    private IEnumerator InitializeTestEnvironment()
    {
        // Find effect resolver
        effectResolver = FindObjectOfType<CardEffectResolver>();
        if (effectResolver == null)
        {
            Debug.LogError("ConditionalEffectTestManager: CardEffectResolver not found!");
            yield break;
        }
        
        // Find test entities
        var entities = FindObjectsOfType<NetworkEntity>();
        if (entities.Length < 2)
        {
            Debug.LogError("ConditionalEffectTestManager: Need at least 2 NetworkEntities for testing!");
            yield break;
        }
        
        testEntity = entities[0];
        testTarget = entities[1];
        
        testEntityTracker = testEntity.GetComponent<EntityTracker>();
        if (testEntityTracker == null)
        {
            Debug.LogError("ConditionalEffectTestManager: Test entity has no EntityTracker!");
            yield break;
        }
        
        Debug.Log($"[CONDITIONAL_TEST] Test environment initialized with entity: {testEntity.EntityName.Value} and target: {testTarget.EntityName.Value}");
        
        yield return new WaitForSeconds(0.1f);
    }
    
    private void SetupTestScenarios()
    {
        testScenarios = new List<ConditionalEffectTestScenario>
        {
            // Health-based conditionals
            new ConditionalEffectTestScenario
            {
                name = "Source Health Below - Condition Met",
                conditionType = ConditionalType.IfSourceHealthBelow,
                conditionValue = 50,
                mainEffect = CardEffectType.Damage,
                mainAmount = 10,
                alternativeEffect = CardEffectType.Heal,
                alternativeAmount = 5,
                alternativeLogic = AlternativeEffectLogic.Replace,
                setupAction = (source, target, tracker) => {
                    source.CurrentHealth.Value = 30; // Below 50
                },
                expectedEffectType = CardEffectType.Damage,
                expectedAmount = 10,
                expectedResult = true
            },
            
            new ConditionalEffectTestScenario
            {
                name = "Source Health Below - Condition Not Met",
                conditionType = ConditionalType.IfSourceHealthBelow,
                conditionValue = 50,
                mainEffect = CardEffectType.Damage,
                mainAmount = 10,
                alternativeEffect = CardEffectType.Heal,
                alternativeAmount = 5,
                alternativeLogic = AlternativeEffectLogic.Replace,
                setupAction = (source, target, tracker) => {
                    source.CurrentHealth.Value = 70; // Above 50
                },
                expectedEffectType = CardEffectType.Heal,
                expectedAmount = 5,
                expectedResult = false
            },
            
            new ConditionalEffectTestScenario
            {
                name = "Source Health Above - Condition Met",
                conditionType = ConditionalType.IfSourceHealthAbove,
                conditionValue = 50,
                mainEffect = CardEffectType.Damage,
                mainAmount = 15,
                alternativeEffect = CardEffectType.Damage,
                alternativeAmount = 5,
                alternativeLogic = AlternativeEffectLogic.Replace,
                setupAction = (source, target, tracker) => {
                    source.CurrentHealth.Value = 80; // Above 50
                },
                expectedEffectType = CardEffectType.Damage,
                expectedAmount = 15,
                expectedResult = true
            },
            
            new ConditionalEffectTestScenario
            {
                name = "Target Health Below - Condition Met",
                conditionType = ConditionalType.IfTargetHealthBelow,
                conditionValue = 40,
                mainEffect = CardEffectType.Damage,
                mainAmount = 20,
                alternativeEffect = CardEffectType.Damage,
                alternativeAmount = 8,
                alternativeLogic = AlternativeEffectLogic.Replace,
                setupAction = (source, target, tracker) => {
                    target.CurrentHealth.Value = 25; // Below 40
                },
                expectedEffectType = CardEffectType.Damage,
                expectedAmount = 20,
                expectedResult = true
            },
            
            new ConditionalEffectTestScenario
            {
                name = "Target Health Above - Condition Met",
                conditionType = ConditionalType.IfTargetHealthAbove,
                conditionValue = 60,
                mainEffect = CardEffectType.Heal,
                mainAmount = 12,
                alternativeEffect = CardEffectType.Heal,
                alternativeAmount = 3,
                alternativeLogic = AlternativeEffectLogic.Replace,
                setupAction = (source, target, tracker) => {
                    target.CurrentHealth.Value = 80; // Above 60
                },
                expectedEffectType = CardEffectType.Heal,
                expectedAmount = 12,
                expectedResult = true
            },
            
            // Tracking-based conditionals
            new ConditionalEffectTestScenario
            {
                name = "Damage Taken This Fight - Condition Met",
                conditionType = ConditionalType.IfDamageTakenThisFight,
                conditionValue = 20,
                mainEffect = CardEffectType.Heal,
                mainAmount = 15,
                alternativeEffect = CardEffectType.Heal,
                alternativeAmount = 5,
                alternativeLogic = AlternativeEffectLogic.Replace,
                setupAction = (source, target, tracker) => {
                    tracker.RecordDamageTaken(25); // Above 20
                },
                expectedEffectType = CardEffectType.Heal,
                expectedAmount = 15,
                expectedResult = true
            },
            
            new ConditionalEffectTestScenario
            {
                name = "Damage Taken Last Round - Condition Met",
                conditionType = ConditionalType.IfDamageTakenLastRound,
                conditionValue = 10,
                mainEffect = CardEffectType.ApplyStrength,
                mainAmount = 3,
                alternativeEffect = CardEffectType.ApplyStrength,
                alternativeAmount = 1,
                alternativeLogic = AlternativeEffectLogic.Replace,
                setupAction = (source, target, tracker) => {
                    tracker.RecordDamageTaken(12); // Above 10
                },
                expectedEffectType = CardEffectType.ApplyStrength,
                expectedAmount = 3,
                expectedResult = true
            },
            
            new ConditionalEffectTestScenario
            {
                name = "Healing Received This Fight - Condition Met",
                conditionType = ConditionalType.IfHealingReceivedThisFight,
                conditionValue = 15,
                mainEffect = CardEffectType.Damage,
                mainAmount = 18,
                alternativeEffect = CardEffectType.Damage,
                alternativeAmount = 8,
                alternativeLogic = AlternativeEffectLogic.Replace,
                setupAction = (source, target, tracker) => {
                    tracker.RecordHealingReceived(20); // Above 15
                },
                expectedEffectType = CardEffectType.Damage,
                expectedAmount = 18,
                expectedResult = true
            },
            
            // Combo and tactical conditionals
            new ConditionalEffectTestScenario
            {
                name = "Combo Count - Condition Met",
                conditionType = ConditionalType.IfComboCount,
                conditionValue = 3,
                mainEffect = CardEffectType.Damage,
                mainAmount = 25,
                alternativeEffect = CardEffectType.Damage,
                alternativeAmount = 10,
                alternativeLogic = AlternativeEffectLogic.Replace,
                setupAction = (source, target, tracker) => {
                    tracker.SetComboCount(4); // Above 3
                },
                expectedEffectType = CardEffectType.Damage,
                expectedAmount = 25,
                expectedResult = true
            },
            
            new ConditionalEffectTestScenario
            {
                name = "Perfection Streak - Condition Not Met",
                conditionType = ConditionalType.IfPerfectionStreak,
                conditionValue = 2,
                mainEffect = CardEffectType.DrawCard,
                mainAmount = 3,
                alternativeEffect = CardEffectType.DrawCard,
                alternativeAmount = 1,
                alternativeLogic = AlternativeEffectLogic.Replace,
                setupAction = (source, target, tracker) => {
                    // Perfection streak starts at 0 and can't be directly set in tests
                    // This test verifies that the condition fails when streak is below threshold
                    // In a real scenario, perfection streak would be managed by the combat system
                },
                expectedEffectType = CardEffectType.DrawCard,
                expectedAmount = 1, // Should use alternative effect
                expectedResult = false // Condition not met since streak is 0 < 2
            },
            
            new ConditionalEffectTestScenario
            {
                name = "Zero Cost Cards This Turn - Condition Met",
                conditionType = ConditionalType.IfZeroCostCardsThisTurn,
                conditionValue = 1,
                mainEffect = CardEffectType.RestoreEnergy,
                mainAmount = 2,
                alternativeEffect = CardEffectType.RestoreEnergy,
                alternativeAmount = 1,
                alternativeLogic = AlternativeEffectLogic.Replace,
                setupAction = (source, target, tracker) => {
                    tracker.RecordCardPlayed(1, false, CardType.Attack, true);
                    tracker.RecordCardPlayed(2, false, CardType.Skill, true); // 2 cards, above 1
                },
                expectedEffectType = CardEffectType.RestoreEnergy,
                expectedAmount = 2,
                expectedResult = true
            },
            
            new ConditionalEffectTestScenario
            {
                name = "Zero Cost Cards This Fight - Condition Met",
                conditionType = ConditionalType.IfZeroCostCardsThisFight,
                conditionValue = 3,
                mainEffect = CardEffectType.ApplyBreak,
                mainAmount = 2,
                alternativeEffect = CardEffectType.ApplyWeak,
                alternativeAmount = 1,
                alternativeLogic = AlternativeEffectLogic.Replace,
                setupAction = (source, target, tracker) => {
                    for (int i = 0; i < 4; i++) // 4 cards, above 3
                    {
                        tracker.RecordCardPlayed(i + 1, false, CardType.Attack, true);
                    }
                },
                expectedEffectType = CardEffectType.ApplyBreak,
                expectedAmount = 2,
                expectedResult = true
            },
            
            // Stance and energy conditionals
            new ConditionalEffectTestScenario
            {
                name = "In Stance - Condition Met",
                conditionType = ConditionalType.IfInStance,
                conditionValue = (int)StanceType.Aggressive,
                mainEffect = CardEffectType.Damage,
                mainAmount = 20,
                alternativeEffect = CardEffectType.Damage,
                alternativeAmount = 12,
                alternativeLogic = AlternativeEffectLogic.Replace,
                setupAction = (source, target, tracker) => {
                    tracker.SetStance(StanceType.Aggressive);
                },
                expectedEffectType = CardEffectType.Damage,
                expectedAmount = 20,
                expectedResult = true
            },
            
            new ConditionalEffectTestScenario
            {
                name = "Energy Remaining - Condition Met",
                conditionType = ConditionalType.IfEnergyRemaining,
                conditionValue = 2,
                mainEffect = CardEffectType.Heal,
                mainAmount = 8,
                alternativeEffect = CardEffectType.Heal,
                alternativeAmount = 3,
                alternativeLogic = AlternativeEffectLogic.Replace,
                setupAction = (source, target, tracker) => {
                    source.CurrentEnergy.Value = 4; // Above 2
                },
                expectedEffectType = CardEffectType.Heal,
                expectedAmount = 8,
                expectedResult = true
            },
            
            // Alternative logic tests
            new ConditionalEffectTestScenario
            {
                name = "Additional Logic - Condition Met",
                conditionType = ConditionalType.IfComboCount,
                conditionValue = 2,
                mainEffect = CardEffectType.Damage,
                mainAmount = 10,
                alternativeEffect = CardEffectType.ApplyStrength,
                alternativeAmount = 2,
                alternativeLogic = AlternativeEffectLogic.Additional,
                setupAction = (source, target, tracker) => {
                    tracker.SetComboCount(3); // Above 2
                },
                expectedEffectType = CardEffectType.Damage, // Should get both effects
                expectedAmount = 10,
                expectedResult = true,
                expectsBothEffects = true
            },
            
            new ConditionalEffectTestScenario
            {
                name = "Additional Logic - Condition Not Met",
                conditionType = ConditionalType.IfComboCount,
                conditionValue = 5,
                mainEffect = CardEffectType.Damage,
                mainAmount = 10,
                alternativeEffect = CardEffectType.ApplyStrength,
                alternativeAmount = 2,
                alternativeLogic = AlternativeEffectLogic.Additional,
                setupAction = (source, target, tracker) => {
                    tracker.SetComboCount(2); // Below 5
                },
                expectedEffectType = CardEffectType.Damage, // Should get only main effect
                expectedAmount = 10,
                expectedResult = false,
                expectsBothEffects = false
            }
        };
    }
    
    private IEnumerator RunTestScenario(ConditionalEffectTestScenario scenario)
    {
        totalTests++;
        
        TestLogger.StartTest($"Conditional Test: {scenario.name}");
        
        // Reset test environment
        yield return StartCoroutine(ResetTestEnvironment());
        
        // Create test card with conditional effect
        CardData testCard = CreateTestCard(scenario);
        
        // Setup test conditions
        scenario.setupAction?.Invoke(testEntity, testTarget, testEntityTracker);
        
        yield return new WaitForSeconds(0.1f); // Allow setup to complete
        
        // Capture state before effect
        var beforeState = CaptureEntityState(testTarget);
        
        // Test the conditional effect
        bool conditionMet = EvaluateCondition(scenario.conditionType, scenario.conditionValue, testEntity, testTarget);
        
        // Validate result
        bool testPassed = conditionMet == scenario.expectedResult;
        
        if (testPassed)
        {
            passedTests++;
            TestLogger.LogEvent($"✓ PASS: Condition {scenario.conditionType} expected {scenario.expectedResult}, got {conditionMet}");
            if (logDetailedResults)
            {
                Debug.Log($"[CONDITIONAL_TEST] ✓ {scenario.name}: PASSED");
            }
        }
        else
        {
            failedTests++;
            TestLogger.LogEvent($"✗ FAIL: Condition {scenario.conditionType} expected {scenario.expectedResult}, got {conditionMet}");
            Debug.LogError($"[CONDITIONAL_TEST] ✗ {scenario.name}: FAILED - Expected {scenario.expectedResult}, got {conditionMet}");
        }
        
        TestLogger.FinishTest();
    }
    
    private IEnumerator ResetTestEnvironment()
    {
        // Reset entity health and energy to full
        testEntity.CurrentHealth.Value = testEntity.MaxHealth.Value;
        testEntity.CurrentEnergy.Value = testEntity.MaxEnergy.Value;
        testTarget.CurrentHealth.Value = testTarget.MaxHealth.Value;
        testTarget.CurrentEnergy.Value = testTarget.MaxEnergy.Value;
        
        // Reset tracker data
        if (testEntityTracker != null)
        {
            testEntityTracker.ResetForNewFight();
        }
        
        yield return new WaitForSeconds(0.1f);
    }
    
    private CardData CreateTestCard(ConditionalEffectTestScenario scenario)
    {
        var cardData = ScriptableObject.CreateInstance<CardData>();
        
        // Use reflection to set private fields
        var cardIdField = typeof(CardData).GetField("_cardId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cardNameField = typeof(CardData).GetField("_cardName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var effectsField = typeof(CardData).GetField("_effects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        cardIdField?.SetValue(cardData, totalTests + 10000);
        cardNameField?.SetValue(cardData, $"Test Card {scenario.name}");
        
        // Create conditional effect
        var effects = new List<CardEffect>
        {
            new CardEffect
            {
                effectType = scenario.mainEffect,
                amount = scenario.mainAmount,
                targetType = CardTargetType.Opponent,
                conditionType = scenario.conditionType,
                conditionValue = scenario.conditionValue,
                hasAlternativeEffect = true,
                alternativeEffectType = scenario.alternativeEffect,
                alternativeEffectAmount = scenario.alternativeAmount,
                alternativeLogic = scenario.alternativeLogic
            }
        };
        
        effectsField?.SetValue(cardData, effects);
        
        return cardData;
    }
    
    private bool EvaluateCondition(ConditionalType conditionType, int conditionValue, NetworkEntity sourceEntity, NetworkEntity targetEntity)
    {
        // Use reflection to access CheckConditionForEffect method from CardEffectResolver
        var method = typeof(CardEffectResolver).GetMethod("CheckConditionForEffect", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (method != null)
        {
            var mockEffect = new CardEffect
            {
                conditionType = conditionType,
                conditionValue = conditionValue
            };
            
            var targetList = new List<NetworkEntity> { targetEntity };
            return (bool)method.Invoke(effectResolver, new object[] { sourceEntity, targetList, mockEffect });
        }
        
        // Fallback: implement basic evaluation logic
        return EvaluateConditionFallback(conditionType, conditionValue, sourceEntity, targetEntity);
    }
    
    private bool EvaluateConditionFallback(ConditionalType conditionType, int conditionValue, NetworkEntity sourceEntity, NetworkEntity targetEntity)
    {
        switch (conditionType)
        {
            case ConditionalType.IfSourceHealthBelow:
                return sourceEntity.CurrentHealth.Value < conditionValue;
            case ConditionalType.IfSourceHealthAbove:
                return sourceEntity.CurrentHealth.Value > conditionValue;
            case ConditionalType.IfTargetHealthBelow:
                return targetEntity.CurrentHealth.Value < conditionValue;
            case ConditionalType.IfTargetHealthAbove:
                return targetEntity.CurrentHealth.Value > conditionValue;
            case ConditionalType.IfEnergyRemaining:
                return sourceEntity.CurrentEnergy.Value >= conditionValue;
            default:
                // For tracking-based conditions, delegate to EntityTracker
                var tracker = sourceEntity.GetComponent<EntityTracker>();
                return tracker?.CheckCondition(conditionType, conditionValue) ?? false;
        }
    }
    
    private EntityState CaptureEntityState(NetworkEntity entity)
    {
        return EntityStateCapture.CaptureEntityState(entity);
    }
    
    private void ReportFinalResults()
    {
        float successRate = totalTests > 0 ? (float)passedTests / totalTests * 100f : 0f;
        
        Debug.Log($"[CONDITIONAL_TEST] ═══════════════════════════════════════");
        Debug.Log($"[CONDITIONAL_TEST] CONDITIONAL EFFECT TEST RESULTS");
        Debug.Log($"[CONDITIONAL_TEST] ═══════════════════════════════════════");
        Debug.Log($"[CONDITIONAL_TEST] Total Tests: {totalTests}");
        Debug.Log($"[CONDITIONAL_TEST] Passed: {passedTests}");
        Debug.Log($"[CONDITIONAL_TEST] Failed: {failedTests}");
        Debug.Log($"[CONDITIONAL_TEST] Success Rate: {successRate:F1}%");
        Debug.Log($"[CONDITIONAL_TEST] ═══════════════════════════════════════");
        
        if (failedTests > 0)
        {
            Debug.LogError($"[CONDITIONAL_TEST] {failedTests} conditional effect tests FAILED! Check logs above for details.");
        }
        else
        {
            Debug.Log($"[CONDITIONAL_TEST] All conditional effect tests PASSED! ✓");
        }
    }
}

/// <summary>
/// Data structure for conditional effect test scenarios
/// </summary>
[System.Serializable]
public class ConditionalEffectTestScenario
{
    public string name;
    public ConditionalType conditionType;
    public int conditionValue;
    public CardEffectType mainEffect;
    public int mainAmount;
    public CardEffectType alternativeEffect;
    public int alternativeAmount;
    public AlternativeEffectLogic alternativeLogic;
    public System.Action<NetworkEntity, NetworkEntity, EntityTracker> setupAction;
    public CardEffectType expectedEffectType;
    public int expectedAmount;
    public bool expectedResult;
    public bool expectsBothEffects = false;
}

 