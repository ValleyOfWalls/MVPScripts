using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;

/// <summary>
/// Editor window for testing combat math scenarios in play mode
/// Access via Tools > Test Combat > Combat Math Test Window
/// </summary>
public class CombatMathTestWindow : EditorWindow
{
    private List<CombatMathTestResult> lastTestResults = new List<CombatMathTestResult>();
    private Vector2 scrollPosition;
    private bool showFailuresOnly = false;
    
    [MenuItem("Tools/Test Combat/Combat Math Test Window")]
    public static void ShowWindow()
    {
        GetWindow<CombatMathTestWindow>("Combat Math Tests");
    }
    
    void OnGUI()
    {
        EditorGUILayout.LabelField("Combat Math Testing", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to run combat math tests", MessageType.Info);
            return;
        }
        
        // Test buttons
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Run Basic Combat Math Tests (13 tests)", GUILayout.Height(30)))
        {
            RunBasicTests();
        }
        
        if (GUILayout.Button("Run Extended Combat Math Tests (35+ tests)", GUILayout.Height(30)))
        {
            RunExtendedTests();
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // Results display options
        if (lastTestResults.Count > 0)
        {
            EditorGUILayout.BeginHorizontal();
            showFailuresOnly = EditorGUILayout.Toggle("Show Failures Only", showFailuresOnly);
            
            int passedCount = lastTestResults.Count(r => r.passed);
            int failedCount = lastTestResults.Count - passedCount;
            
            EditorGUILayout.LabelField($"Results: {passedCount}/{lastTestResults.Count} passed", 
                failedCount > 0 ? EditorStyles.boldLabel : EditorStyles.label);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Results scroll view
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));
            
            var resultsToShow = showFailuresOnly ? 
                lastTestResults.Where(r => !r.passed).ToList() : 
                lastTestResults;
            
            foreach (var result in resultsToShow)
            {
                DrawTestResult(result);
            }
            
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.HelpBox("No test results yet. Click a test button to run tests.", MessageType.Info);
        }
    }
    
    private void RunBasicTests()
    {
        var entities = FindTestEntities();
        if (entities.source == null || entities.target == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find NetworkEntity objects for testing. Make sure you're in a combat scene with entities present.", "OK");
            return;
        }
        
        Debug.Log("[COMBAT MATH] Starting basic combat math tests...");
        lastTestResults = CombatMathIntegration.RunComprehensiveStatusEffectTests(entities.source, entities.target);
        
        int passed = lastTestResults.Count(r => r.passed);
        int failed = lastTestResults.Count - passed;
        
        string message = $"Basic Tests Complete!\n\nPassed: {passed}\nFailed: {failed}\n\nCheck console for detailed results.";
        EditorUtility.DisplayDialog("Test Results", message, "OK");
        
        Repaint();
    }
    
    private void RunExtendedTests()
    {
        var entities = FindTestEntities();
        if (entities.source == null || entities.target == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find NetworkEntity objects for testing. Make sure you're in a combat scene with entities present.", "OK");
            return;
        }
        
        Debug.Log("[COMBAT MATH] Starting extended combat math tests...");
        lastTestResults = CombatMathIntegration.RunExtendedStatusEffectTests(entities.source, entities.target);
        
        int passed = lastTestResults.Count(r => r.passed);
        int failed = lastTestResults.Count - passed;
        
        string message = $"Extended Tests Complete!\n\nPassed: {passed}\nFailed: {failed}\n\nCheck console for detailed results.";
        EditorUtility.DisplayDialog("Test Results", message, "OK");
        
        Repaint();
    }
    
    private void DrawTestResult(CombatMathTestResult result)
    {
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = result.passed ? Color.green : Color.red;
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = originalColor;
        
        // Test name and status
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(result.testName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(result.passed ? "PASS" : "FAIL", 
            result.passed ? EditorStyles.label : EditorStyles.boldLabel, 
            GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
        
        // Damage values
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Base: {result.baseDamage}", GUILayout.Width(80));
        EditorGUILayout.LabelField($"Expected: {result.expectedDamage}", GUILayout.Width(100));
        EditorGUILayout.LabelField($"Actual: {result.actualDamage}", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();
        
        // Effects
        if (!string.IsNullOrEmpty(result.sourceEffects))
        {
            EditorGUILayout.LabelField($"Source Effects: {result.sourceEffects}", EditorStyles.miniLabel);
        }
        if (!string.IsNullOrEmpty(result.targetEffects))
        {
            EditorGUILayout.LabelField($"Target Effects: {result.targetEffects}", EditorStyles.miniLabel);
        }
        
        // Failure reason
        if (!result.passed && !string.IsNullOrEmpty(result.failureReason))
        {
            EditorGUILayout.LabelField($"Failure: {result.failureReason}", EditorStyles.miniLabel);
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }
    
    private (NetworkEntity source, NetworkEntity target) FindTestEntities()
    {
        var entities = UnityEngine.Object.FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        
        NetworkEntity source = null;
        NetworkEntity target = null;
        
        foreach (var entity in entities)
        {
            if (entity.GetComponent<EffectHandler>() != null)
            {
                if (source == null)
                    source = entity;
                else if (target == null)
                {
                    target = entity;
                    break;
                }
            }
        }
        
        return (source, target);
    }
} 