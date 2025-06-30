using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Static utility class for programmatic upgrade condition testing
/// Provides methods to test specific upgrade scenarios and run comprehensive test suites
/// Similar to CombatMathIntegration but focused on upgrade conditions
/// </summary>
public static class UpgradeConditionIntegration
{
    /// <summary>
    /// Test a specific upgrade condition scenario
    /// </summary>
    public static bool TestUpgradeCondition(UpgradeConditionType conditionType, int requiredValue, 
        UpgradeComparisonType comparisonType, int actualValue)
    {
        Debug.Log($"[UPGRADE_INTEGRATION] Testing {conditionType}: {actualValue} {comparisonType} {requiredValue}");
        
        bool result = CompareValues(actualValue, requiredValue, comparisonType);
        
        Debug.Log($"[UPGRADE_INTEGRATION] Result: {result}");
        return result;
    }
    
    /// <summary>
    /// Run comprehensive upgrade condition test suite
    /// </summary>
    public static void RunUpgradeConditionTests()
    {
        Debug.Log("[UPGRADE_INTEGRATION] ═══════════════════════════════════════");
        Debug.Log("[UPGRADE_INTEGRATION] STARTING UPGRADE CONDITION TESTS");
        Debug.Log("[UPGRADE_INTEGRATION] ═══════════════════════════════════════");
        
        int totalTests = 0;
        int passedTests = 0;
        
        // Test basic comparison types
        totalTests += TestComparisonTypes(ref passedTests);
        
        // Test specific upgrade conditions
        totalTests += TestSpecificConditions(ref passedTests);
        
        // Test edge cases
        totalTests += TestEdgeCases(ref passedTests);
        
        // Report results
        float successRate = totalTests > 0 ? (float)passedTests / totalTests * 100f : 0f;
        
        Debug.Log("[UPGRADE_INTEGRATION] ═══════════════════════════════════════");
        Debug.Log($"[UPGRADE_INTEGRATION] UPGRADE CONDITION TEST RESULTS");
        Debug.Log($"[UPGRADE_INTEGRATION] Total Tests: {totalTests}");
        Debug.Log($"[UPGRADE_INTEGRATION] Passed: {passedTests}");
        Debug.Log($"[UPGRADE_INTEGRATION] Failed: {totalTests - passedTests}");
        Debug.Log($"[UPGRADE_INTEGRATION] Success Rate: {successRate:F1}%");
        Debug.Log("[UPGRADE_INTEGRATION] ═══════════════════════════════════════");
    }
    
    private static int TestComparisonTypes(ref int passedTests)
    {
        Debug.Log("[UPGRADE_INTEGRATION] Testing comparison types...");
        
        var tests = new[]
        {
            new { name = "GreaterThanOrEqual (true)", actual = 10, required = 10, comparison = UpgradeComparisonType.GreaterThanOrEqual, expected = true },
            new { name = "GreaterThanOrEqual (false)", actual = 9, required = 10, comparison = UpgradeComparisonType.GreaterThanOrEqual, expected = false },
            new { name = "Equal (true)", actual = 5, required = 5, comparison = UpgradeComparisonType.Equal, expected = true },
            new { name = "Equal (false)", actual = 4, required = 5, comparison = UpgradeComparisonType.Equal, expected = false },
            new { name = "LessThanOrEqual (true)", actual = 3, required = 5, comparison = UpgradeComparisonType.LessThanOrEqual, expected = true },
            new { name = "LessThanOrEqual (false)", actual = 6, required = 5, comparison = UpgradeComparisonType.LessThanOrEqual, expected = false },
            new { name = "GreaterThan (true)", actual = 8, required = 7, comparison = UpgradeComparisonType.GreaterThan, expected = true },
            new { name = "GreaterThan (false)", actual = 7, required = 7, comparison = UpgradeComparisonType.GreaterThan, expected = false },
            new { name = "LessThan (true)", actual = 3, required = 5, comparison = UpgradeComparisonType.LessThan, expected = true },
            new { name = "LessThan (false)", actual = 5, required = 5, comparison = UpgradeComparisonType.LessThan, expected = false }
        };
        
        int testCount = 0;
        foreach (var test in tests)
        {
            testCount++;
            bool result = CompareValues(test.actual, test.required, test.comparison);
            
            if (result == test.expected)
            {
                passedTests++;
                Debug.Log($"[UPGRADE_INTEGRATION] ✓ {test.name}: PASSED");
            }
            else
            {
                Debug.LogError($"[UPGRADE_INTEGRATION] ✗ {test.name}: FAILED - Expected {test.expected}, got {result}");
            }
        }
        
        return testCount;
    }
    
    private static int TestSpecificConditions(ref int passedTests)
    {
        Debug.Log("[UPGRADE_INTEGRATION] Testing specific upgrade conditions...");
        
        var tests = new[]
        {
            new { name = "Times Played This Fight", condition = UpgradeConditionType.TimesPlayedThisFight, value = 3, required = 3, expected = true },
            new { name = "Damage Dealt This Fight", condition = UpgradeConditionType.DamageDealtThisFight, value = 100, required = 75, expected = true },
            new { name = "Damage Taken This Fight", condition = UpgradeConditionType.DamageTakenThisFight, value = 50, required = 60, expected = false },
            new { name = "Healing Given This Fight", condition = UpgradeConditionType.HealingGivenThisFight, value = 30, required = 25, expected = true },
            new { name = "Combo Count Reached", condition = UpgradeConditionType.ComboCountReached, value = 5, required = 4, expected = true },
            new { name = "Zero Cost Cards This Turn", condition = UpgradeConditionType.ZeroCostCardsThisTurn, value = 2, required = 3, expected = false },
            new { name = "Zero Cost Cards This Fight", condition = UpgradeConditionType.ZeroCostCardsThisFight, value = 4, required = 3, expected = true }
        };
        
        int testCount = 0;
        foreach (var test in tests)
        {
            testCount++;
            bool result = TestUpgradeCondition(test.condition, test.required, UpgradeComparisonType.GreaterThanOrEqual, test.value);
            
            if (result == test.expected)
            {
                passedTests++;
                Debug.Log($"[UPGRADE_INTEGRATION] ✓ {test.name}: PASSED");
            }
            else
            {
                Debug.LogError($"[UPGRADE_INTEGRATION] ✗ {test.name}: FAILED - Expected {test.expected}, got {result}");
            }
        }
        
        return testCount;
    }
    
    private static int TestEdgeCases(ref int passedTests)
    {
        Debug.Log("[UPGRADE_INTEGRATION] Testing edge cases...");
        
        var tests = new[]
        {
            new { name = "Zero Value Equal", actual = 0, required = 0, comparison = UpgradeComparisonType.Equal, expected = true },
            new { name = "Negative Value Less Than", actual = -5, required = 0, comparison = UpgradeComparisonType.LessThan, expected = true },
            new { name = "Large Value Greater Than", actual = 999, required = 100, comparison = UpgradeComparisonType.GreaterThan, expected = true },
            new { name = "Boundary Case Equal", actual = 50, required = 50, comparison = UpgradeComparisonType.Equal, expected = true },
            new { name = "Boundary Case Greater", actual = 51, required = 50, comparison = UpgradeComparisonType.GreaterThan, expected = true },
            new { name = "Boundary Case Less", actual = 49, required = 50, comparison = UpgradeComparisonType.LessThan, expected = true }
        };
        
        int testCount = 0;
        foreach (var test in tests)
        {
            testCount++;
            bool result = CompareValues(test.actual, test.required, test.comparison);
            
            if (result == test.expected)
            {
                passedTests++;
                Debug.Log($"[UPGRADE_INTEGRATION] ✓ {test.name}: PASSED");
            }
            else
            {
                Debug.LogError($"[UPGRADE_INTEGRATION] ✗ {test.name}: FAILED - Expected {test.expected}, got {result}");
            }
        }
        
        return testCount;
    }
    
    /// <summary>
    /// Test health-based upgrade conditions
    /// </summary>
    public static void TestHealthBasedConditions()
    {
        Debug.Log("[UPGRADE_INTEGRATION] Testing health-based upgrade conditions...");
        
        // Test low health condition (25% threshold)
        bool lowHealthTest = TestHealthCondition(20, 100, UpgradeConditionType.PlayedAtLowHealth);
        Debug.Log($"[UPGRADE_INTEGRATION] Low Health (20/100): {lowHealthTest}");
        
        // Test high health condition (75% threshold)
        bool highHealthTest = TestHealthCondition(80, 100, UpgradeConditionType.PlayedAtHighHealth);
        Debug.Log($"[UPGRADE_INTEGRATION] High Health (80/100): {highHealthTest}");
        
        // Test half health condition (50% threshold)
        bool halfHealthTest = TestHealthCondition(40, 100, UpgradeConditionType.PlayedAtHalfHealth);
        Debug.Log($"[UPGRADE_INTEGRATION] Half Health (40/100): {halfHealthTest}");
    }
    
    private static bool TestHealthCondition(int currentHealth, int maxHealth, UpgradeConditionType conditionType)
    {
        float healthPercent = (float)currentHealth / maxHealth * 100f;
        
        switch (conditionType)
        {
            case UpgradeConditionType.PlayedAtLowHealth:
                return healthPercent <= 25f;
            case UpgradeConditionType.PlayedAtHighHealth:
                return healthPercent >= 75f;
            case UpgradeConditionType.PlayedAtHalfHealth:
                return healthPercent <= 50f;
            default:
                return false;
        }
    }
    
    /// <summary>
    /// Test stance-based upgrade conditions
    /// </summary>
    public static void TestStanceBasedConditions()
    {
        Debug.Log("[UPGRADE_INTEGRATION] Testing stance-based upgrade conditions...");
        
        var stanceTests = new[]
        {
            new { stance = StanceType.Aggressive, expected = true },
            new { stance = StanceType.Defensive, expected = true },
            new { stance = StanceType.None, expected = false }
        };
        
        foreach (var test in stanceTests)
        {
            bool result = test.stance != StanceType.None;
            Debug.Log($"[UPGRADE_INTEGRATION] Stance {test.stance}: {result} (expected: {test.expected})");
        }
    }
    
    private static bool CompareValues(int currentValue, int requiredValue, UpgradeComparisonType comparisonType)
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
} 