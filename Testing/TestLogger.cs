using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.Linq;

/// <summary>
/// Manages test logging with size limits for easier analysis
/// </summary>
public static class TestLogger
{
    private static List<string> testResults = new List<string>();
    private static StringBuilder currentTest = new StringBuilder();
    private static int maxLogEntries = 50; // Limit per log batch
    private static int maxCharactersPerEntry = 500; // Limit per log entry
    private static int currentLogBatch = 1;
    private static int totalTestsRun = 0;
    
    /// <summary>
    /// Start logging a new test
    /// </summary>
    public static void StartTest(string testName)
    {
        currentTest.Clear();
        currentTest.AppendLine($"=== {testName} ===");
    }
    
    /// <summary>
    /// Log an event during testing
    /// </summary>
    public static void LogEvent(string message)
    {
        if (message.Length > maxCharactersPerEntry)
        {
            message = message.Substring(0, maxCharactersPerEntry - 3) + "...";
        }
        currentTest.AppendLine($"  {message}");
    }
    
    /// <summary>
    /// Log before/after state comparison
    /// </summary>
    public static void LogStateComparison(string entityName, EntityState before, EntityState after)
    {
        var changes = new List<string>();
        
        // Check health changes
        if (before.currentHealth != after.currentHealth)
        {
            changes.Add($"Health: {before.currentHealth} → {after.currentHealth}");
        }
        
        // Check energy changes
        if (before.currentEnergy != after.currentEnergy)
        {
            changes.Add($"Energy: {before.currentEnergy} → {after.currentEnergy}");
        }
        
        // Check stance changes
        if (before.currentStance != after.currentStance)
        {
            changes.Add($"Stance: {before.currentStance} → {after.currentStance}");
        }
        
        // Check combo changes
        if (before.comboCount != after.comboCount)
        {
            changes.Add($"Combo: {before.comboCount} → {after.comboCount}");
        }
        
        // Check stun state changes
        if (before.isStunned != after.isStunned)
        {
            changes.Add($"Stunned: {before.isStunned} → {after.isStunned}");
        }
        
        // Check limit break changes
        if (before.isInLimitBreak != after.isInLimitBreak)
        {
            changes.Add($"LimitBreak: {before.isInLimitBreak} → {after.isInLimitBreak}");
        }
        
        // Check effect changes
        var beforeEffects = before.activeEffects.Select(e => $"{e.effectName}({e.potency})").OrderBy(x => x).ToList();
        var afterEffects = after.activeEffects.Select(e => $"{e.effectName}({e.potency})").OrderBy(x => x).ToList();
        
        if (!beforeEffects.SequenceEqual(afterEffects))
        {
            changes.Add($"Effects: [{string.Join(",", beforeEffects)}] → [{string.Join(",", afterEffects)}]");
        }
        
        if (changes.Count > 0)
        {
            LogEvent($"{entityName}: {string.Join(", ", changes)}");
        }
        else
        {
            LogEvent($"{entityName}: No changes");
        }
    }
    
    /// <summary>
    /// Log card play details
    /// </summary>
    public static void LogCardPlay(string casterName, string cardName, string targetName, bool wasSuccessful)
    {
        string status = wasSuccessful ? "SUCCESS" : "FAILED";
        LogEvent($"[{status}] {casterName} played '{cardName}' on {targetName}");
    }
    
    /// <summary>
    /// Log test result (pass/fail)
    /// </summary>
    public static void LogTestResult(bool passed, string reason = "")
    {
        string result = passed ? "PASS" : "FAIL";
        if (!string.IsNullOrEmpty(reason))
        {
            LogEvent($"Result: {result} - {reason}");
        }
        else
        {
            LogEvent($"Result: {result}");
        }
    }
    
    /// <summary>
    /// Finish current test and add to results
    /// </summary>
    public static void FinishTest()
    {
        if (testResults.Count >= maxLogEntries)
        {
            // Output current batch and start a new one
            OutputCurrentBatch();
            StartNewBatch();
        }
        
        testResults.Add(currentTest.ToString());
        totalTestsRun++;
        currentTest.Clear();
    }
    
    /// <summary>
    /// Get all test results formatted for output
    /// </summary>
    public static string GetAllResults()
    {
        var sb = new StringBuilder();
        
        if (currentLogBatch == 1)
        {
            // Single batch - show all results
            sb.AppendLine("=== COMBAT TEST RESULTS ===");
            sb.AppendLine($"Total tests: {testResults.Count}");
            sb.AppendLine();
        }
        else
        {
            // Multiple batches - show current batch
            sb.AppendLine($"=== COMBAT TEST RESULTS - BATCH {currentLogBatch} ===");
            sb.AppendLine($"Tests in this batch: {testResults.Count}");
            sb.AppendLine($"Total tests run so far: {totalTestsRun}");
            sb.AppendLine();
        }
        
        foreach (var result in testResults)
        {
            sb.AppendLine(result);
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Get summary of test results
    /// </summary>
    public static string GetSummary()
    {
        int batchTests = testResults.Count;
        int batchPassed = testResults.Count(r => r.Contains("Result: PASS"));
        int batchFailed = batchTests - batchPassed;
        
        if (currentLogBatch == 1)
        {
            return $"Test Summary: {batchPassed}/{batchTests} passed, {batchFailed} failed";
        }
        else
        {
            return $"Batch {currentLogBatch} Summary: {batchPassed}/{batchTests} passed, {batchFailed} failed (Total tests run: {totalTestsRun})";
        }
    }
    
    /// <summary>
    /// Clear all test results
    /// </summary>
    public static void ClearResults()
    {
        testResults.Clear();
        currentTest.Clear();
        currentLogBatch = 1;
        totalTestsRun = 0;
    }
    
    /// <summary>
    /// Get only failed tests for debugging
    /// </summary>
    public static string GetFailedTests()
    {
        var failedTests = testResults.Where(r => r.Contains("Result: FAIL")).ToList();
        
        if (failedTests.Count == 0)
        {
            return "No failed tests found.";
        }
        
        var sb = new StringBuilder();
        sb.AppendLine("=== FAILED TESTS ONLY ===");
        
        foreach (var failed in failedTests)
        {
            sb.AppendLine(failed);
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Set logging limits
    /// </summary>
    public static void SetLimits(int maxEntries, int maxCharsPerEntry)
    {
        maxLogEntries = maxEntries;
        maxCharactersPerEntry = maxCharsPerEntry;
    }
    
    /// <summary>
    /// Output current batch of results to console
    /// </summary>
    private static void OutputCurrentBatch()
    {
        var batchResults = GetAllResults();
        var batchSummary = GetSummary();
        
        UnityEngine.Debug.Log($"\n{batchSummary}");
        UnityEngine.Debug.Log(batchResults);
        
        // Also log batch completion
        UnityEngine.Debug.Log($"=== BATCH {currentLogBatch} COMPLETED - Starting next batch... ===\n");
    }
    
    /// <summary>
    /// Start a new batch of test results
    /// </summary>
    private static void StartNewBatch()
    {
        testResults.Clear();
        currentLogBatch++;
    }
    
    /// <summary>
    /// Get total number of tests run across all batches
    /// </summary>
    public static int GetTotalTestsRun()
    {
        return totalTestsRun;
    }
    
    /// <summary>
    /// Get current batch number
    /// </summary>
    public static int GetCurrentBatch()
    {
        return currentLogBatch;
    }
} 