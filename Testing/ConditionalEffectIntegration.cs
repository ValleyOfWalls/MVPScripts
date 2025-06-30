using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Static utility class for programmatic conditional effect testing
/// Provides methods to test specific conditional scenarios and run comprehensive test suites
/// Similar to CombatMathIntegration but focused on conditional effects
/// </summary>
public static class ConditionalEffectIntegration
{
    /// <summary>
    /// Test a specific conditional effect scenario
    /// </summary>
    public static bool TestConditionalEffect(ConditionalType conditionType, int conditionValue, 
        int actualValue, bool useEntityValue = false)
    {
        Debug.Log($"[CONDITIONAL_INTEGRATION] Testing {conditionType}: {actualValue} vs {conditionValue}");
        
        bool result = EvaluateCondition(conditionType, conditionValue, actualValue);
        
        Debug.Log($"[CONDITIONAL_INTEGRATION] Result: {result}");
        return result;
    }
    
    /// <summary>
    /// Run comprehensive conditional effect test suite
    /// </summary>
    public static void RunConditionalEffectTests()
    {
        Debug.Log("[CONDITIONAL_INTEGRATION] ═══════════════════════════════════════");
        Debug.Log("[CONDITIONAL_INTEGRATION] STARTING CONDITIONAL EFFECT TESTS");
        Debug.Log("[CONDITIONAL_INTEGRATION] ═══════════════════════════════════════");
        
        int totalTests = 0;
        int passedTests = 0;
        
        // Test health-based conditions
        totalTests += TestHealthConditions(ref passedTests);
        
        // Test tracking-based conditions
        totalTests += TestTrackingConditions(ref passedTests);
        
        // Test tactical conditions
        totalTests += TestTacticalConditions(ref passedTests);
        
        // Test edge cases
        totalTests += TestConditionalEdgeCases(ref passedTests);
        
        // Report results
        float successRate = totalTests > 0 ? (float)passedTests / totalTests * 100f : 0f;
        
        Debug.Log("[CONDITIONAL_INTEGRATION] ═══════════════════════════════════════");
        Debug.Log($"[CONDITIONAL_INTEGRATION] CONDITIONAL EFFECT TEST RESULTS");
        Debug.Log($"[CONDITIONAL_INTEGRATION] Total Tests: {totalTests}");
        Debug.Log($"[CONDITIONAL_INTEGRATION] Passed: {passedTests}");
        Debug.Log($"[CONDITIONAL_INTEGRATION] Failed: {totalTests - passedTests}");
        Debug.Log($"[CONDITIONAL_INTEGRATION] Success Rate: {successRate:F1}%");
        Debug.Log("[CONDITIONAL_INTEGRATION] ═══════════════════════════════════════");
    }
    
    private static int TestHealthConditions(ref int passedTests)
    {
        Debug.Log("[CONDITIONAL_INTEGRATION] Testing health-based conditions...");
        
        var tests = new[]
        {
            new { name = "Source Health Below (true)", condition = ConditionalType.IfSourceHealthBelow, threshold = 50, actual = 30, expected = true },
            new { name = "Source Health Below (false)", condition = ConditionalType.IfSourceHealthBelow, threshold = 50, actual = 70, expected = false },
            new { name = "Source Health Above (true)", condition = ConditionalType.IfSourceHealthAbove, threshold = 50, actual = 80, expected = true },
            new { name = "Source Health Above (false)", condition = ConditionalType.IfSourceHealthAbove, threshold = 50, actual = 30, expected = false },
            new { name = "Target Health Below (true)", condition = ConditionalType.IfTargetHealthBelow, threshold = 40, actual = 25, expected = true },
            new { name = "Target Health Below (false)", condition = ConditionalType.IfTargetHealthBelow, threshold = 40, actual = 60, expected = false },
            new { name = "Target Health Above (true)", condition = ConditionalType.IfTargetHealthAbove, threshold = 60, actual = 80, expected = true },
            new { name = "Target Health Above (false)", condition = ConditionalType.IfTargetHealthAbove, threshold = 60, actual = 40, expected = false }
        };
        
        int testCount = 0;
        foreach (var test in tests)
        {
            testCount++;
            bool result = TestConditionalEffect(test.condition, test.threshold, test.actual);
            
            if (result == test.expected)
            {
                passedTests++;
                Debug.Log($"[CONDITIONAL_INTEGRATION] ✓ {test.name}: PASSED");
            }
            else
            {
                Debug.LogError($"[CONDITIONAL_INTEGRATION] ✗ {test.name}: FAILED - Expected {test.expected}, got {result}");
            }
        }
        
        return testCount;
    }
    
    private static int TestTrackingConditions(ref int passedTests)
    {
        Debug.Log("[CONDITIONAL_INTEGRATION] Testing tracking-based conditions...");
        
        var tests = new[]
        {
            new { name = "Damage Taken This Fight (true)", condition = ConditionalType.IfDamageTakenThisFight, threshold = 20, actual = 30, expected = true },
            new { name = "Damage Taken This Fight (false)", condition = ConditionalType.IfDamageTakenThisFight, threshold = 20, actual = 15, expected = false },
            new { name = "Damage Taken Last Round (true)", condition = ConditionalType.IfDamageTakenLastRound, threshold = 10, actual = 15, expected = true },
            new { name = "Healing Received This Fight (true)", condition = ConditionalType.IfHealingReceivedThisFight, threshold = 25, actual = 30, expected = true },
            new { name = "Healing Received Last Round (false)", condition = ConditionalType.IfHealingReceivedLastRound, threshold = 15, actual = 10, expected = false },
            new { name = "Times Played This Fight (true)", condition = ConditionalType.IfTimesPlayedThisFight, threshold = 3, actual = 4, expected = true },
            new { name = "Times Played This Fight (false)", condition = ConditionalType.IfTimesPlayedThisFight, threshold = 5, actual = 3, expected = false }
        };
        
        int testCount = 0;
        foreach (var test in tests)
        {
            testCount++;
            bool result = TestConditionalEffect(test.condition, test.threshold, test.actual);
            
            if (result == test.expected)
            {
                passedTests++;
                Debug.Log($"[CONDITIONAL_INTEGRATION] ✓ {test.name}: PASSED");
            }
            else
            {
                Debug.LogError($"[CONDITIONAL_INTEGRATION] ✗ {test.name}: FAILED - Expected {test.expected}, got {result}");
            }
        }
        
        return testCount;
    }
    
    private static int TestTacticalConditions(ref int passedTests)
    {
        Debug.Log("[CONDITIONAL_INTEGRATION] Testing tactical conditions...");
        
        var tests = new[]
        {
            new { name = "Combo Count (true)", condition = ConditionalType.IfComboCount, threshold = 3, actual = 5, expected = true },
            new { name = "Combo Count (false)", condition = ConditionalType.IfComboCount, threshold = 4, actual = 2, expected = false },
            new { name = "Perfection Streak (true)", condition = ConditionalType.IfPerfectionStreak, threshold = 2, actual = 3, expected = true },
            new { name = "Zero Cost Cards This Turn (true)", condition = ConditionalType.IfZeroCostCardsThisTurn, threshold = 1, actual = 2, expected = true },
            new { name = "Zero Cost Cards This Fight (false)", condition = ConditionalType.IfZeroCostCardsThisFight, threshold = 5, actual = 3, expected = false },
            new { name = "Energy Remaining (true)", condition = ConditionalType.IfEnergyRemaining, threshold = 2, actual = 4, expected = true },
            new { name = "In Stance (true)", condition = ConditionalType.IfInStance, threshold = (int)StanceType.Aggressive, actual = (int)StanceType.Aggressive, expected = true },
            new { name = "In Stance (false)", condition = ConditionalType.IfInStance, threshold = (int)StanceType.Defensive, actual = (int)StanceType.None, expected = false }
        };
        
        int testCount = 0;
        foreach (var test in tests)
        {
            testCount++;
            bool result = TestConditionalEffect(test.condition, test.threshold, test.actual);
            
            if (result == test.expected)
            {
                passedTests++;
                Debug.Log($"[CONDITIONAL_INTEGRATION] ✓ {test.name}: PASSED");
            }
            else
            {
                Debug.LogError($"[CONDITIONAL_INTEGRATION] ✗ {test.name}: FAILED - Expected {test.expected}, got {result}");
            }
        }
        
        return testCount;
    }
    
    private static int TestConditionalEdgeCases(ref int passedTests)
    {
        Debug.Log("[CONDITIONAL_INTEGRATION] Testing edge cases...");
        
        var tests = new[]
        {
            new { name = "Zero Threshold Equal", condition = ConditionalType.IfDamageTakenThisFight, threshold = 0, actual = 0, expected = true },
            new { name = "Zero Threshold Above", condition = ConditionalType.IfDamageTakenThisFight, threshold = 0, actual = 1, expected = true },
            new { name = "High Values", condition = ConditionalType.IfDamageTakenThisFight, threshold = 999, actual = 1000, expected = true },
            new { name = "Boundary Case Equal", condition = ConditionalType.IfComboCount, threshold = 50, actual = 50, expected = true },
            new { name = "Boundary Case Below", condition = ConditionalType.IfComboCount, threshold = 50, actual = 49, expected = false },
            new { name = "Cards In Hand Edge", condition = ConditionalType.IfCardsInHand, threshold = 7, actual = 7, expected = true }
        };
        
        int testCount = 0;
        foreach (var test in tests)
        {
            testCount++;
            bool result = TestConditionalEffect(test.condition, test.threshold, test.actual);
            
            if (result == test.expected)
            {
                passedTests++;
                Debug.Log($"[CONDITIONAL_INTEGRATION] ✓ {test.name}: PASSED");
            }
            else
            {
                Debug.LogError($"[CONDITIONAL_INTEGRATION] ✗ {test.name}: FAILED - Expected {test.expected}, got {result}");
            }
        }
        
        return testCount;
    }
    
    /// <summary>
    /// Test alternative effect logic scenarios
    /// </summary>
    public static void TestAlternativeEffectLogic()
    {
        Debug.Log("[CONDITIONAL_INTEGRATION] Testing alternative effect logic...");
        
        // Test Replace logic
        TestReplaceLogic(true);  // Condition met - use main effect
        TestReplaceLogic(false); // Condition not met - use alternative effect
        
        // Test Additional logic
        TestAdditionalLogic(true);  // Condition met - use both effects
        TestAdditionalLogic(false); // Condition not met - use only main effect
    }
    
    private static void TestReplaceLogic(bool conditionMet)
    {
        string scenario = conditionMet ? "Condition Met" : "Condition Not Met";
        string expectedEffect = conditionMet ? "Main Effect" : "Alternative Effect";
        
        Debug.Log($"[CONDITIONAL_INTEGRATION] Replace Logic - {scenario}: Should use {expectedEffect}");
    }
    
    private static void TestAdditionalLogic(bool conditionMet)
    {
        string scenario = conditionMet ? "Condition Met" : "Condition Not Met";
        string expectedEffect = conditionMet ? "Main + Alternative Effects" : "Main Effect Only";
        
        Debug.Log($"[CONDITIONAL_INTEGRATION] Additional Logic - {scenario}: Should use {expectedEffect}");
    }
    
    /// <summary>
    /// Test deck/hand/discard related conditions
    /// </summary>
    public static void TestCardLocationConditions()
    {
        Debug.Log("[CONDITIONAL_INTEGRATION] Testing card location conditions...");
        
        var locationTests = new[]
        {
            new { name = "Cards In Hand", condition = ConditionalType.IfCardsInHand, threshold = 5, actual = 6, expected = true },
            new { name = "Cards In Deck", condition = ConditionalType.IfCardsInDeck, threshold = 10, actual = 8, expected = false },
            new { name = "Cards In Discard", condition = ConditionalType.IfCardsInDiscard, threshold = 3, actual = 4, expected = true }
        };
        
        foreach (var test in locationTests)
        {
            bool result = TestConditionalEffect(test.condition, test.threshold, test.actual);
            Debug.Log($"[CONDITIONAL_INTEGRATION] {test.name}: {result} (expected: {test.expected})");
        }
    }
    
    private static bool EvaluateCondition(ConditionalType conditionType, int conditionValue, int actualValue)
    {
        switch (conditionType)
        {
            // Health conditions use comparison operators
            case ConditionalType.IfSourceHealthBelow:
            case ConditionalType.IfTargetHealthBelow:
                return actualValue < conditionValue;
                
            case ConditionalType.IfSourceHealthAbove:
            case ConditionalType.IfTargetHealthAbove:
                return actualValue > conditionValue;
                
            // Most tracking conditions use >= comparison
            case ConditionalType.IfDamageTakenThisFight:
            case ConditionalType.IfDamageTakenLastRound:
            case ConditionalType.IfHealingReceivedThisFight:
            case ConditionalType.IfHealingReceivedLastRound:
            case ConditionalType.IfTimesPlayedThisFight:
            case ConditionalType.IfComboCount:
            case ConditionalType.IfPerfectionStreak:
            case ConditionalType.IfZeroCostCardsThisTurn:
            case ConditionalType.IfZeroCostCardsThisFight:
            case ConditionalType.IfEnergyRemaining:
            case ConditionalType.IfCardsInHand:
            case ConditionalType.IfCardsInDeck:
            case ConditionalType.IfCardsInDiscard:
                return actualValue >= conditionValue;
                
            // Stance and card type use exact equality
            case ConditionalType.IfInStance:
            case ConditionalType.IfLastCardType:
                return actualValue == conditionValue;
                
            default:
                Debug.LogWarning($"[CONDITIONAL_INTEGRATION] Unknown condition type: {conditionType}");
                return false;
        }
    }
} 