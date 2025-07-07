using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;

/// <summary>
/// Integration class for combat math testing via Editor Window
/// Provides methods to test specific damage scenarios with status effect combinations
/// </summary>
public static class CombatMathIntegration
{
    /// <summary>
    /// Test a specific damage scenario with given status effects
    /// </summary>
    public static CombatMathTestResult TestDamageScenario(NetworkEntity source, NetworkEntity target, 
        int baseDamage, List<StatusEffectData> sourceEffects, List<StatusEffectData> targetEffects, 
        int expectedDamage, string scenarioName = "Custom Scenario")
    {
        var damageCalculator = UnityEngine.Object.FindFirstObjectByType<DamageCalculator>();
        if (damageCalculator == null)
        {
            return new CombatMathTestResult
            {
                testName = scenarioName,
                passed = false,
                failureReason = "DamageCalculator not found"
            };
        }
        
        // Clear existing effects
        ClearEntityEffects(source);
        ClearEntityEffects(target);
        
        // Apply source effects
        foreach (var effect in sourceEffects)
        {
            ApplyStatusEffect(source, effect, source);
        }
        
        // Apply target effects
        foreach (var effect in targetEffects)
        {
            ApplyStatusEffect(target, effect, source);
        }
        
        // Calculate actual damage
        int actualDamage = damageCalculator.CalculateDamage(source, target, baseDamage);
        
        // Create result
        var result = new CombatMathTestResult
        {
            testName = scenarioName,
            baseDamage = baseDamage,
            expectedDamage = expectedDamage,
            actualDamage = actualDamage,
            passed = actualDamage == expectedDamage,
            sourceEffects = string.Join(", ", sourceEffects.Select(e => $"{e.effectName}({e.potency})")),
            targetEffects = string.Join(", ", targetEffects.Select(e => $"{e.effectName}({e.potency})")),
            failureReason = actualDamage != expectedDamage ? $"Expected {expectedDamage}, got {actualDamage}" : "",
            timestamp = System.DateTime.Now
        };
        
        // Log result to console for visibility
        string status = result.passed ? "PASS" : "FAIL";
        Debug.Log($"[COMBAT MATH] {scenarioName}: {status} - Base:{baseDamage}, Expected:{expectedDamage}, Actual:{actualDamage}");
        if (!string.IsNullOrEmpty(result.sourceEffects))
            Debug.Log($"  Source Effects: {result.sourceEffects}");
        if (!string.IsNullOrEmpty(result.targetEffects))
            Debug.Log($"  Target Effects: {result.targetEffects}");
        if (!result.passed)
            Debug.LogWarning($"  FAILURE: {result.failureReason}");
        
        return result;
    }
    
    /// <summary>
    /// Run the standard comprehensive test suite (used by Editor Window)
    /// </summary>
    public static List<CombatMathTestResult> RunComprehensiveStatusEffectTests(NetworkEntity source, NetworkEntity target)
    {
        var results = new List<CombatMathTestResult>();
        
        // Test individual effects
        results.AddRange(TestIndividualEffects(source, target));
        
        // Test common combinations
        results.AddRange(TestCommonCombinations(source, target));
        
        // Test edge cases
        results.AddRange(TestEdgeCases(source, target));
        
        // Log summary
        int passed = results.Count(r => r.passed);
        int failed = results.Count - passed;
        Debug.Log($"[COMBAT MATH SUMMARY] {passed}/{results.Count} tests passed, {failed} failed");
        
        if (failed > 0)
        {
            Debug.LogWarning($"[COMBAT MATH FAILURES]:");
            foreach (var failure in results.Where(r => !r.passed))
            {
                Debug.LogWarning($"  - {failure.testName}: {failure.failureReason}");
            }
        }
        
        Debug.Log($"Completed {results.Count} basic combat math tests");
        
        return results;
    }
    
    /// <summary>
    /// Run extended test suite with additional complex scenarios
    /// </summary>
    public static List<CombatMathTestResult> RunExtendedStatusEffectTests(NetworkEntity source, NetworkEntity target)
    {
        var results = new List<CombatMathTestResult>();
        
        // Include all basic tests
        results.AddRange(RunComprehensiveStatusEffectTests(source, target));
        
        // Add extended tests
        results.AddRange(TestComplexCombinations(source, target));
        results.AddRange(TestBoundaryConditions(source, target));
        results.AddRange(TestRoundingBehavior(source, target));
        results.AddRange(TestEffectStacking(source, target));
        
        // Log extended summary
        int basicTestCount = TestIndividualEffects(source, target).Count + 
                            TestCommonCombinations(source, target).Count + 
                            TestEdgeCases(source, target).Count;
        int extendedTestCount = results.Count - basicTestCount;
        int passed = results.Count(r => r.passed);
        int failed = results.Count - passed;
        
        Debug.Log($"[EXTENDED COMBAT MATH SUMMARY] {passed}/{results.Count} total tests passed ({basicTestCount} basic + {extendedTestCount} extended)");
        
        if (failed > 0)
        {
            Debug.LogWarning($"[EXTENDED COMBAT MATH FAILURES]:");
            foreach (var failure in results.Where(r => !r.passed))
            {
                Debug.LogWarning($"  - {failure.testName}: {failure.failureReason}");
            }
        }
        
        Debug.Log($"Completed {results.Count} extended combat math tests");
        
        return results;
    }
    
    private static List<CombatMathTestResult> TestIndividualEffects(NetworkEntity source, NetworkEntity target)
    {
        var results = new List<CombatMathTestResult>();
        int baseDamage = 10;
        
        // Weak effect (reduces damage by 25%)
        results.Add(TestDamageScenario(source, target, baseDamage,
            new List<StatusEffectData> { new StatusEffectData { effectName = "Weak", potency = 1, duration = 3 } },
            new List<StatusEffectData>(),
            8, "Weak Source"));
        
        // Break effect (increases damage by 50%)
        results.Add(TestDamageScenario(source, target, baseDamage,
            new List<StatusEffectData>(),
            new List<StatusEffectData> { new StatusEffectData { effectName = "Break", potency = 1, duration = 3 } },
            15, "Break Target"));
        
        // Strength effect (adds flat damage)
        results.Add(TestDamageScenario(source, target, baseDamage,
            new List<StatusEffectData> { new StatusEffectData { effectName = "Strength", potency = 5, duration = 999 } },
            new List<StatusEffectData>(),
            15, "Strength +5"));
        
        // Curse effect (reduces flat damage)
        results.Add(TestDamageScenario(source, target, baseDamage,
            new List<StatusEffectData> { new StatusEffectData { effectName = "Curse", potency = 3, duration = 999 } },
            new List<StatusEffectData>(),
            7, "Curse -3"));
        
        // Armor effect (reduces incoming damage)
        results.Add(TestDamageScenario(source, target, baseDamage,
            new List<StatusEffectData>(),
            new List<StatusEffectData> { new StatusEffectData { effectName = "Armor", potency = 4, duration = 3 } },
            6, "Armor 4"));
        
        return results;
    }
    
    private static List<CombatMathTestResult> TestCommonCombinations(NetworkEntity source, NetworkEntity target)
    {
        var results = new List<CombatMathTestResult>();
        int baseDamage = 10;
        
        // Weak source + Break target
        results.Add(TestDamageScenario(source, target, baseDamage,
            new List<StatusEffectData> { new StatusEffectData { effectName = "Weak", potency = 1, duration = 3 } },
            new List<StatusEffectData> { new StatusEffectData { effectName = "Break", potency = 1, duration = 3 } },
            11, "Weak + Break"));
        
        // Strength + Break
        results.Add(TestDamageScenario(source, target, baseDamage,
            new List<StatusEffectData> { new StatusEffectData { effectName = "Strength", potency = 5, duration = 999 } },
            new List<StatusEffectData> { new StatusEffectData { effectName = "Break", potency = 1, duration = 3 } },
            22, "Strength + Break"));
        
        // Curse + Weak (double debuff on source)
        results.Add(TestDamageScenario(source, target, baseDamage,
            new List<StatusEffectData> { 
                new StatusEffectData { effectName = "Curse", potency = 3, duration = 999 },
                new StatusEffectData { effectName = "Weak", potency = 1, duration = 3 }
            },
            new List<StatusEffectData>(),
            5, "Curse + Weak"));
        
        // Armor + Break (defense vs vulnerability)
        results.Add(TestDamageScenario(source, target, baseDamage,
            new List<StatusEffectData>(),
            new List<StatusEffectData> { 
                new StatusEffectData { effectName = "Armor", potency = 4, duration = 3 },
                new StatusEffectData { effectName = "Break", potency = 1, duration = 3 }
            },
            11, "Armor + Break"));
        
        return results;
    }
    
    private static List<CombatMathTestResult> TestEdgeCases(NetworkEntity source, NetworkEntity target)
    {
        var results = new List<CombatMathTestResult>();
        
        // Zero damage
        results.Add(TestDamageScenario(source, target, 0,
            new List<StatusEffectData>(),
            new List<StatusEffectData>(),
            0, "Zero Damage"));
        
        // High armor vs low damage (should not go below 1)
        results.Add(TestDamageScenario(source, target, 5,
            new List<StatusEffectData>(),
            new List<StatusEffectData> { new StatusEffectData { effectName = "Armor", potency = 10, duration = 3 } },
            1, "High Armor vs Low Damage"));
        
        // Multiple strength stacks
        results.Add(TestDamageScenario(source, target, 5,
            new List<StatusEffectData> { new StatusEffectData { effectName = "Strength", potency = 15, duration = 999 } },
            new List<StatusEffectData>(),
            20, "High Strength Stacks"));
        
        // Extreme curse (should not go below 0)
        results.Add(TestDamageScenario(source, target, 5,
            new List<StatusEffectData> { new StatusEffectData { effectName = "Curse", potency = 10, duration = 999 } },
            new List<StatusEffectData>(),
            0, "Extreme Curse (5 damage - 10 curse = 0)"));
        
        return results;
    }
    
    private static List<CombatMathTestResult> TestComplexCombinations(NetworkEntity source, NetworkEntity target)
    {
        var results = new List<CombatMathTestResult>();
        int baseDamage = 20;
        
        // Triple source effects: Strength + Curse + Weak
        // (20 - 4 + 8) × 0.75 = 24 × 0.75 = 18
        results.Add(TestDamageScenario(source, target, baseDamage,
            new List<StatusEffectData> { 
                new StatusEffectData { effectName = "Strength", potency = 8, duration = 999 },
                new StatusEffectData { effectName = "Curse", potency = 4, duration = 999 },
                new StatusEffectData { effectName = "Weak", potency = 1, duration = 3 }
            },
            new List<StatusEffectData>(),
            18, "Strength + Curse + Weak"));
        
        // All effects combined: Source (Strength + Weak) + Target (Armor + Break)
        // (20 + 6) × 0.75 × 1.5 - 3 = 26 × 0.75 × 1.5 - 3 = 19.5 × 1.5 - 3 = 29.25 - 3 = 26.25 → 26
        results.Add(TestDamageScenario(source, target, baseDamage,
            new List<StatusEffectData> { 
                new StatusEffectData { effectName = "Strength", potency = 6, duration = 999 },
                new StatusEffectData { effectName = "Weak", potency = 1, duration = 3 }
            },
            new List<StatusEffectData> { 
                new StatusEffectData { effectName = "Armor", potency = 3, duration = 3 },
                new StatusEffectData { effectName = "Break", potency = 1, duration = 3 }
            },
            26, "All Effects Combined"));
        
        // Massive Strength vs Massive Armor
        // (10 + 50) - 40 = 60 - 40 = 20
        results.Add(TestDamageScenario(source, target, 10,
            new List<StatusEffectData> { new StatusEffectData { effectName = "Strength", potency = 50, duration = 999 } },
            new List<StatusEffectData> { new StatusEffectData { effectName = "Armor", potency = 40, duration = 3 } },
            20, "Massive Strength vs Massive Armor"));
        
        return results;
    }
    
    private static List<CombatMathTestResult> TestBoundaryConditions(NetworkEntity source, NetworkEntity target)
    {
        var results = new List<CombatMathTestResult>();
        
        // Damage exactly equals armor (should be 1)
        results.Add(TestDamageScenario(source, target, 8,
            new List<StatusEffectData>(),
            new List<StatusEffectData> { new StatusEffectData { effectName = "Armor", potency = 8, duration = 3 } },
            1, "Damage Equals Armor"));
        
        // Curse exactly equals damage (should be 0)
        results.Add(TestDamageScenario(source, target, 12,
            new List<StatusEffectData> { new StatusEffectData { effectName = "Curse", potency = 12, duration = 999 } },
            new List<StatusEffectData>(),
            0, "Curse Equals Damage"));
        
        // Maximum possible damage with all positive effects
        // (1 + 100) × 1.5 = 101 × 1.5 = 151.5 → 152
        results.Add(TestDamageScenario(source, target, 1,
            new List<StatusEffectData> { new StatusEffectData { effectName = "Strength", potency = 100, duration = 999 } },
            new List<StatusEffectData> { new StatusEffectData { effectName = "Break", potency = 1, duration = 3 } },
            152, "Maximum Damage Boost"));
        
        // Minimum possible damage with all negative effects
        // (1 - 100) × 0.75 = max(0, -99) × 0.75 = 0
        results.Add(TestDamageScenario(source, target, 1,
            new List<StatusEffectData> { 
                new StatusEffectData { effectName = "Curse", potency = 100, duration = 999 },
                new StatusEffectData { effectName = "Weak", potency = 1, duration = 3 }
            },
            new List<StatusEffectData>(),
            0, "Minimum Damage (All Debuffs)"));
        
        return results;
    }
    
    private static List<CombatMathTestResult> TestRoundingBehavior(NetworkEntity source, NetworkEntity target)
    {
        var results = new List<CombatMathTestResult>();
        
        // Test various .5 rounding scenarios
        
        // 21 × 0.75 = 15.75 → 16
        results.Add(TestDamageScenario(source, target, 21,
            new List<StatusEffectData> { new StatusEffectData { effectName = "Weak", potency = 1, duration = 3 } },
            new List<StatusEffectData>(),
            16, "Rounding: 15.75 → 16"));
        
        // 19 × 0.75 = 14.25 → 14
        results.Add(TestDamageScenario(source, target, 19,
            new List<StatusEffectData> { new StatusEffectData { effectName = "Weak", potency = 1, duration = 3 } },
            new List<StatusEffectData>(),
            14, "Rounding: 14.25 → 14"));
        
        // 15 × 1.5 = 22.5 → 22 (banker's rounding)
        results.Add(TestDamageScenario(source, target, 15,
            new List<StatusEffectData>(),
            new List<StatusEffectData> { new StatusEffectData { effectName = "Break", potency = 1, duration = 3 } },
            22, "Rounding: 22.5 → 22 (banker's)"));
        
        // 17 × 1.5 = 25.5 → 26 (banker's rounding)
        results.Add(TestDamageScenario(source, target, 17,
            new List<StatusEffectData>(),
            new List<StatusEffectData> { new StatusEffectData { effectName = "Break", potency = 1, duration = 3 } },
            26, "Rounding: 25.5 → 26 (banker's)"));
        
        return results;
    }
    
    private static List<CombatMathTestResult> TestEffectStacking(NetworkEntity source, NetworkEntity target)
    {
        var results = new List<CombatMathTestResult>();
        
        // Test different Strength values
        results.Add(TestDamageScenario(source, target, 10,
            new List<StatusEffectData> { new StatusEffectData { effectName = "Strength", potency = 1, duration = 999 } },
            new List<StatusEffectData>(),
            11, "Strength +1"));
        
        results.Add(TestDamageScenario(source, target, 10,
            new List<StatusEffectData> { new StatusEffectData { effectName = "Strength", potency = 25, duration = 999 } },
            new List<StatusEffectData>(),
            35, "Strength +25"));
        
        // Test different Curse values
        results.Add(TestDamageScenario(source, target, 10,
            new List<StatusEffectData> { new StatusEffectData { effectName = "Curse", potency = 1, duration = 999 } },
            new List<StatusEffectData>(),
            9, "Curse -1"));
        
        results.Add(TestDamageScenario(source, target, 10,
            new List<StatusEffectData> { new StatusEffectData { effectName = "Curse", potency = 9, duration = 999 } },
            new List<StatusEffectData>(),
            1, "Curse -9"));
        
        // Test different Armor values
        results.Add(TestDamageScenario(source, target, 10,
            new List<StatusEffectData>(),
            new List<StatusEffectData> { new StatusEffectData { effectName = "Armor", potency = 1, duration = 3 } },
            9, "Armor 1"));
        
        results.Add(TestDamageScenario(source, target, 10,
            new List<StatusEffectData>(),
            new List<StatusEffectData> { new StatusEffectData { effectName = "Armor", potency = 9, duration = 3 } },
            1, "Armor 9"));
        
        return results;
    }
    
    private static void ApplyStatusEffect(NetworkEntity entity, StatusEffectData effectData, NetworkEntity source)
    {
        var effectHandler = entity.GetComponent<EffectHandler>();
        if (effectHandler != null && effectHandler.IsServerInitialized)
        {
            effectHandler.AddEffect(effectData.effectName, effectData.potency, effectData.duration, source);
        }
    }
    
    private static void ClearEntityEffects(NetworkEntity entity)
    {
        var effectHandler = entity.GetComponent<EffectHandler>();
        if (effectHandler != null && effectHandler.IsServerInitialized)
        {
            effectHandler.ClearAllEffects();
        }
        
        // Also clear EntityTracker strength stacks
        var entityTracker = entity.GetComponent<EntityTracker>();
        if (entityTracker != null && entityTracker.IsServerInitialized)
        {
            entityTracker.RemoveStrength(entityTracker.StrengthStacks);
        }
    }
}

/// <summary>
/// Status effect data structure for testing
/// </summary>
[System.Serializable]
public class StatusEffectData
{
    public string effectName;
    public int potency;
    public int duration;
}

/// <summary>
/// Result of a combat math test
/// </summary>
[System.Serializable]
public class CombatMathTestResult
{
    public string testName;
    public int baseDamage;
    public int expectedDamage;
    public int actualDamage;
    public bool passed;
    public string sourceEffects;
    public string targetEffects;
    public string failureReason;
    public System.DateTime timestamp;
} 