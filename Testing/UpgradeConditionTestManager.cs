using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;

/// <summary>
/// Automated testing manager for card upgrade conditions
/// Tests all upgrade condition types to ensure they work correctly
/// Similar to CombatMathTestManager but focused on upgrade logic
/// </summary>
public class UpgradeConditionTestManager : NetworkBehaviour
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
    private List<UpgradeTestScenario> testScenarios;
    private CardUpgradeManager upgradeManager;
    private EntityTracker testEntityTracker;
    private NetworkEntity testEntity;
    
    private void Start()
    {
        if (runTestsOnStart && IsServer)
        {
            StartCoroutine(RunAllTests());
        }
    }
    
    /// <summary>
    /// Run all upgrade condition tests
    /// </summary>
    public void RunTests()
    {
        if (IsServer)
        {
            StartCoroutine(RunAllTests());
        }
        else
        {
            Debug.LogWarning("UpgradeConditionTestManager: Tests can only be run on server");
        }
    }
    
    private IEnumerator RunAllTests()
    {
        TestLogger.StartTest("Upgrade Condition Test Suite");
        
        // Initialize test environment
        yield return StartCoroutine(InitializeTestEnvironment());
        
        // Setup test scenarios
        SetupTestScenarios();
        
        totalTests = 0;
        passedTests = 0;
        failedTests = 0;
        
        Debug.Log($"[UPGRADE_TEST] Starting {testScenarios.Count} upgrade condition test scenarios...");
        
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
        // Find or create upgrade manager
        upgradeManager = FindObjectOfType<CardUpgradeManager>();
        if (upgradeManager == null)
        {
            Debug.LogError("UpgradeConditionTestManager: CardUpgradeManager not found!");
            yield break;
        }
        
        // Find test entity (use first available NetworkEntity)
        testEntity = FindObjectOfType<NetworkEntity>();
        if (testEntity == null)
        {
            Debug.LogError("UpgradeConditionTestManager: No NetworkEntity found for testing!");
            yield break;
        }
        
        testEntityTracker = testEntity.GetComponent<EntityTracker>();
        if (testEntityTracker == null)
        {
            Debug.LogError("UpgradeConditionTestManager: Test entity has no EntityTracker!");
            yield break;
        }
        
        Debug.Log($"[UPGRADE_TEST] Test environment initialized with entity: {testEntity.EntityName.Value}");
        
        yield return new WaitForSeconds(0.1f);
    }
    
    private void SetupTestScenarios()
    {
        testScenarios = new List<UpgradeTestScenario>
        {
            // Card-specific tracking tests
            new UpgradeTestScenario
            {
                name = "Times Played This Fight",
                conditionType = UpgradeConditionType.TimesPlayedThisFight,
                requiredValue = 3,
                comparisonType = UpgradeComparisonType.GreaterThanOrEqual,
                setupAction = (entity, tracker, cardId) => {
                    // Simulate playing card 3 times
                    for (int i = 0; i < 3; i++)
                    {
                        upgradeManager.OnCardPlayed(CreateMockCard(cardId), entity);
                    }
                },
                expectedResult = true
            },
            
            new UpgradeTestScenario
            {
                name = "Times Played This Fight (Not Met)",
                conditionType = UpgradeConditionType.TimesPlayedThisFight,
                requiredValue = 5,
                comparisonType = UpgradeComparisonType.GreaterThanOrEqual,
                setupAction = (entity, tracker, cardId) => {
                    // Simulate playing card only 2 times
                    for (int i = 0; i < 2; i++)
                    {
                        upgradeManager.OnCardPlayed(CreateMockCard(cardId), entity);
                    }
                },
                expectedResult = false
            },
            
            // Combat performance tests
            new UpgradeTestScenario
            {
                name = "Damage Dealt This Fight",
                conditionType = UpgradeConditionType.DamageDealtThisFight,
                requiredValue = 50,
                comparisonType = UpgradeComparisonType.GreaterThanOrEqual,
                setupAction = (entity, tracker, cardId) => {
                    tracker.RecordDamageDealt(60);
                },
                expectedResult = true
            },
            
            new UpgradeTestScenario
            {
                name = "Damage Taken This Fight",
                conditionType = UpgradeConditionType.DamageTakenThisFight,
                requiredValue = 30,
                comparisonType = UpgradeComparisonType.GreaterThanOrEqual,
                setupAction = (entity, tracker, cardId) => {
                    tracker.RecordDamageTaken(35);
                },
                expectedResult = true
            },
            
            new UpgradeTestScenario
            {
                name = "Healing Given This Fight",
                conditionType = UpgradeConditionType.HealingGivenThisFight,
                requiredValue = 25,
                comparisonType = UpgradeComparisonType.GreaterThanOrEqual,
                setupAction = (entity, tracker, cardId) => {
                    tracker.RecordHealingGiven(30);
                },
                expectedResult = true
            },
            
            // Health-based tests
            new UpgradeTestScenario
            {
                name = "Played At Low Health",
                conditionType = UpgradeConditionType.PlayedAtLowHealth,
                requiredValue = 1,
                comparisonType = UpgradeComparisonType.GreaterThanOrEqual,
                setupAction = (entity, tracker, cardId) => {
                    // Set health to 20% (low health)
                    entity.CurrentHealth.Value = Mathf.RoundToInt(entity.MaxHealth.Value * 0.2f);
                },
                expectedResult = true
            },
            
            new UpgradeTestScenario
            {
                name = "Played At High Health",
                conditionType = UpgradeConditionType.PlayedAtHighHealth,
                requiredValue = 1,
                comparisonType = UpgradeComparisonType.GreaterThanOrEqual,
                setupAction = (entity, tracker, cardId) => {
                    // Set health to 80% (high health)
                    entity.CurrentHealth.Value = Mathf.RoundToInt(entity.MaxHealth.Value * 0.8f);
                },
                expectedResult = true
            },
            
            new UpgradeTestScenario
            {
                name = "Played At Half Health",
                conditionType = UpgradeConditionType.PlayedAtHalfHealth,
                requiredValue = 1,
                comparisonType = UpgradeComparisonType.GreaterThanOrEqual,
                setupAction = (entity, tracker, cardId) => {
                    // Set health to 40% (below half)
                    entity.CurrentHealth.Value = Mathf.RoundToInt(entity.MaxHealth.Value * 0.4f);
                },
                expectedResult = true
            },
            
            // Combo and tactical tests
            new UpgradeTestScenario
            {
                name = "Combo Count Reached",
                conditionType = UpgradeConditionType.ComboCountReached,
                requiredValue = 3,
                comparisonType = UpgradeComparisonType.GreaterThanOrEqual,
                setupAction = (entity, tracker, cardId) => {
                    tracker.SetComboCount(4);
                },
                expectedResult = true
            },
            
            new UpgradeTestScenario
            {
                name = "Zero Cost Cards This Turn",
                conditionType = UpgradeConditionType.ZeroCostCardsThisTurn,
                requiredValue = 2,
                comparisonType = UpgradeComparisonType.GreaterThanOrEqual,
                setupAction = (entity, tracker, cardId) => {
                    tracker.RecordCardPlayed(1, false, CardType.Attack, true);
                    tracker.RecordCardPlayed(2, false, CardType.Skill, true);
                },
                expectedResult = true
            },
            
            new UpgradeTestScenario
            {
                name = "Zero Cost Cards This Fight",
                conditionType = UpgradeConditionType.ZeroCostCardsThisFight,
                requiredValue = 3,
                comparisonType = UpgradeComparisonType.GreaterThanOrEqual,
                setupAction = (entity, tracker, cardId) => {
                    for (int i = 0; i < 4; i++)
                    {
                        tracker.RecordCardPlayed(i + 1, false, CardType.Attack, true);
                    }
                },
                expectedResult = true
            },
            
            // Stance tests
            new UpgradeTestScenario
            {
                name = "Played In Stance",
                conditionType = UpgradeConditionType.PlayedInStance,
                requiredValue = 1,
                comparisonType = UpgradeComparisonType.GreaterThanOrEqual,
                setupAction = (entity, tracker, cardId) => {
                    tracker.SetStance(StanceType.Aggressive);
                },
                expectedResult = true
            },
            
            // Comparison type tests
            new UpgradeTestScenario
            {
                name = "Equal Comparison Test",
                conditionType = UpgradeConditionType.DamageDealtThisFight,
                requiredValue = 42,
                comparisonType = UpgradeComparisonType.Equal,
                setupAction = (entity, tracker, cardId) => {
                    tracker.RecordDamageDealt(42); // Exactly 42
                },
                expectedResult = true
            },
            
            new UpgradeTestScenario
            {
                name = "Less Than Comparison Test",
                conditionType = UpgradeConditionType.DamageTakenThisFight,
                requiredValue = 20,
                comparisonType = UpgradeComparisonType.LessThan,
                setupAction = (entity, tracker, cardId) => {
                    tracker.RecordDamageTaken(15); // Less than 20
                },
                expectedResult = true
            },
            
            new UpgradeTestScenario
            {
                name = "Greater Than Comparison Test",
                conditionType = UpgradeConditionType.HealingGivenThisFight,
                requiredValue = 10,
                comparisonType = UpgradeComparisonType.GreaterThan,
                setupAction = (entity, tracker, cardId) => {
                    tracker.RecordHealingGiven(15); // Greater than 10
                },
                expectedResult = true
            }
        };
    }
    
    private IEnumerator RunTestScenario(UpgradeTestScenario scenario)
    {
        totalTests++;
        
        TestLogger.StartTest($"Upgrade Test: {scenario.name}");
        
        // Reset test environment
        yield return StartCoroutine(ResetTestEnvironment());
        
        // Create test card with upgrade condition
        int testCardId = 9999 + totalTests; // Unique ID for each test
        CardData testCard = CreateTestCard(testCardId, scenario.conditionType, scenario.requiredValue, scenario.comparisonType);
        Card mockCard = CreateMockCard(testCardId, testCard);
        
        // Setup test conditions
        scenario.setupAction?.Invoke(testEntity, testEntityTracker, testCardId);
        
        yield return new WaitForSeconds(0.1f); // Allow setup to complete
        
        // Test the upgrade condition evaluation
        bool actualResult = EvaluateUpgradeCondition(testCard, mockCard, testEntity);
        
        // Validate result
        bool testPassed = actualResult == scenario.expectedResult;
        
        if (testPassed)
        {
            passedTests++;
            TestLogger.LogEvent($"✓ PASS: Expected {scenario.expectedResult}, got {actualResult}");
            if (logDetailedResults)
            {
                Debug.Log($"[UPGRADE_TEST] ✓ {scenario.name}: PASSED");
            }
        }
        else
        {
            failedTests++;
            TestLogger.LogEvent($"✗ FAIL: Expected {scenario.expectedResult}, got {actualResult}");
            Debug.LogError($"[UPGRADE_TEST] ✗ {scenario.name}: FAILED - Expected {scenario.expectedResult}, got {actualResult}");
        }
        
        TestLogger.FinishTest();
    }
    
    private IEnumerator ResetTestEnvironment()
    {
        // Reset entity health to full
        testEntity.CurrentHealth.Value = testEntity.MaxHealth.Value;
        
        // Reset tracker data
        if (testEntityTracker != null)
        {
            testEntityTracker.ResetForNewFight();
        }
        
        yield return new WaitForSeconds(0.1f);
    }
    
    private CardData CreateTestCard(int cardId, UpgradeConditionType conditionType, int requiredValue, UpgradeComparisonType comparisonType)
    {
        var cardData = ScriptableObject.CreateInstance<CardData>();
        
        // Use reflection to set private fields (same pattern as existing test code)
        var cardIdField = typeof(CardData).GetField("_cardId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cardNameField = typeof(CardData).GetField("_cardName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var canUpgradeField = typeof(CardData).GetField("_canUpgrade", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var upgradeConditionTypeField = typeof(CardData).GetField("_upgradeConditionType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var upgradeRequiredValueField = typeof(CardData).GetField("_upgradeRequiredValue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var upgradeComparisonTypeField = typeof(CardData).GetField("_upgradeComparisonType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        cardIdField?.SetValue(cardData, cardId);
        cardNameField?.SetValue(cardData, $"Test Card {conditionType}");
        canUpgradeField?.SetValue(cardData, true);
        upgradeConditionTypeField?.SetValue(cardData, conditionType);
        upgradeRequiredValueField?.SetValue(cardData, requiredValue);
        upgradeComparisonTypeField?.SetValue(cardData, comparisonType);
        
        return cardData;
    }
    
    private Card CreateMockCard(int cardId, CardData cardData = null)
    {
        // Create a mock card GameObject
        GameObject mockCardObj = new GameObject($"MockCard_{cardId}");
        Card mockCard = mockCardObj.AddComponent<Card>();
        
        // Set card data
        if (cardData != null)
        {
            var cardDataField = typeof(Card).GetField("cardData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            cardDataField?.SetValue(mockCard, cardData);
        }
        
        // Add CardTracker component
        CardTracker cardTracker = mockCardObj.AddComponent<CardTracker>();
        
        return mockCard;
    }
    
    private bool EvaluateUpgradeCondition(CardData cardData, Card card, NetworkEntity entity)
    {
        // Use reflection to access the private EvaluateUpgradeCondition method from CardUpgradeManager
        var method = typeof(CardUpgradeManager).GetMethod("EvaluateUpgradeCondition", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (method != null)
        {
            return (bool)method.Invoke(upgradeManager, new object[] { cardData, card, entity });
        }
        
        // Fallback: implement basic evaluation logic
        return EvaluateUpgradeConditionFallback(cardData, card, entity);
    }
    
    private bool EvaluateUpgradeConditionFallback(CardData cardData, Card card, NetworkEntity entity)
    {
        // Simplified evaluation logic as fallback
        int currentValue = GetConditionValueFallback(cardData.UpgradeConditionType, entity);
        return CompareValues(currentValue, cardData.UpgradeRequiredValue, cardData.UpgradeComparisonType);
    }
    
    private int GetConditionValueFallback(UpgradeConditionType conditionType, NetworkEntity entity)
    {
        var entityTracker = entity.GetComponent<EntityTracker>();
        if (entityTracker == null) return 0;
        
        switch (conditionType)
        {
            case UpgradeConditionType.DamageDealtThisFight:
                return entityTracker.TrackingData.damageDealtThisFight;
            case UpgradeConditionType.DamageTakenThisFight:
                return entityTracker.TrackingData.damageTakenThisFight;
            case UpgradeConditionType.HealingGivenThisFight:
                return entityTracker.TrackingData.healingGivenThisFight;
            case UpgradeConditionType.ComboCountReached:
                return entityTracker.ComboCount;
            case UpgradeConditionType.ZeroCostCardsThisTurn:
                return entityTracker.TrackingData.zeroCostCardsThisTurn;
            case UpgradeConditionType.ZeroCostCardsThisFight:
                return entityTracker.TrackingData.zeroCostCardsThisFight;
            case UpgradeConditionType.PlayedAtLowHealth:
                float lowHealthPercent = (float)entity.CurrentHealth.Value / entity.MaxHealth.Value * 100f;
                return lowHealthPercent <= 25f ? 1 : 0;
            case UpgradeConditionType.PlayedAtHighHealth:
                float highHealthPercent = (float)entity.CurrentHealth.Value / entity.MaxHealth.Value * 100f;
                return highHealthPercent >= 75f ? 1 : 0;
            case UpgradeConditionType.PlayedAtHalfHealth:
                float halfHealthPercent = (float)entity.CurrentHealth.Value / entity.MaxHealth.Value * 100f;
                return halfHealthPercent <= 50f ? 1 : 0;
            case UpgradeConditionType.PlayedInStance:
                return entityTracker.CurrentStance != StanceType.None ? 1 : 0;
            default:
                return 0;
        }
    }
    
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
    
    private void ReportFinalResults()
    {
        float successRate = totalTests > 0 ? (float)passedTests / totalTests * 100f : 0f;
        
        Debug.Log($"[UPGRADE_TEST] ═══════════════════════════════════════");
        Debug.Log($"[UPGRADE_TEST] UPGRADE CONDITION TEST RESULTS");
        Debug.Log($"[UPGRADE_TEST] ═══════════════════════════════════════");
        Debug.Log($"[UPGRADE_TEST] Total Tests: {totalTests}");
        Debug.Log($"[UPGRADE_TEST] Passed: {passedTests}");
        Debug.Log($"[UPGRADE_TEST] Failed: {failedTests}");
        Debug.Log($"[UPGRADE_TEST] Success Rate: {successRate:F1}%");
        Debug.Log($"[UPGRADE_TEST] ═══════════════════════════════════════");
        
        if (failedTests > 0)
        {
            Debug.LogError($"[UPGRADE_TEST] {failedTests} upgrade condition tests FAILED! Check logs above for details.");
        }
        else
        {
            Debug.Log($"[UPGRADE_TEST] All upgrade condition tests PASSED! ✓");
        }
    }
}

/// <summary>
/// Data structure for upgrade test scenarios
/// </summary>
[System.Serializable]
public class UpgradeTestScenario
{
    public string name;
    public UpgradeConditionType conditionType;
    public int requiredValue;
    public UpgradeComparisonType comparisonType;
    public System.Action<NetworkEntity, EntityTracker, int> setupAction;
    public bool expectedResult;
} 