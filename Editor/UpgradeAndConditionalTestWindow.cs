#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Interactive Unity Editor window for testing upgrade conditions and conditional effects
/// Similar to CombatMathTestWindow but focused on upgrade and conditional systems
/// </summary>
public class UpgradeAndConditionalTestWindow : EditorWindow
{
    [MenuItem("Tools/Test Combat/Upgrade & Conditional Test Window")]
    public static void ShowWindow()
    {
        GetWindow<UpgradeAndConditionalTestWindow>("Upgrade & Conditional Tests");
    }
    
    // Upgrade testing variables
    private UpgradeConditionType selectedUpgradeCondition = UpgradeConditionType.TimesPlayedThisFight;
    private UpgradeComparisonType selectedComparisonType = UpgradeComparisonType.GreaterThanOrEqual;
    private int upgradeRequiredValue = 3;
    private int upgradeActualValue = 5;
    
    // Conditional testing variables
    private ConditionalType selectedConditionalType = ConditionalType.IfSourceHealthBelow;
    private int conditionalThreshold = 50;
    private int conditionalActualValue = 30;
    
    // Test results
    private string lastUpgradeResult = "";
    private string lastConditionalResult = "";
    private bool showDetailedResults = true;
    
    // GUI state
    private Vector2 scrollPosition;
    private bool upgradeTestsFoldout = true;
    private bool conditionalTestsFoldout = true;
    private bool integrationTestsFoldout = true;
    
    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        EditorGUILayout.LabelField("Upgrade & Conditional Effect Testing", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // Show play mode requirement
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("These tests require Play Mode to access game systems.", MessageType.Info);
            EditorGUILayout.Space();
        }
        
        // Upgrade condition testing section
        upgradeTestsFoldout = EditorGUILayout.Foldout(upgradeTestsFoldout, "Upgrade Condition Testing", true);
        if (upgradeTestsFoldout)
        {
            DrawUpgradeTestingSection();
        }
        
        EditorGUILayout.Space();
        
        // Conditional effect testing section
        conditionalTestsFoldout = EditorGUILayout.Foldout(conditionalTestsFoldout, "Conditional Effect Testing", true);
        if (conditionalTestsFoldout)
        {
            DrawConditionalTestingSection();
        }
        
        EditorGUILayout.Space();
        
        // Integration testing section
        integrationTestsFoldout = EditorGUILayout.Foldout(integrationTestsFoldout, "Integration Testing", true);
        if (integrationTestsFoldout)
        {
            DrawIntegrationTestingSection();
        }
        
        EditorGUILayout.EndScrollView();
    }
    
    private void DrawUpgradeTestingSection()
    {
        EditorGUILayout.BeginVertical("box");
        
        EditorGUILayout.LabelField("Custom Upgrade Condition Test", EditorStyles.boldLabel);
        
        // Upgrade condition selection
        selectedUpgradeCondition = (UpgradeConditionType)EditorGUILayout.EnumPopup("Condition Type", selectedUpgradeCondition);
        selectedComparisonType = (UpgradeComparisonType)EditorGUILayout.EnumPopup("Comparison", selectedComparisonType);
        upgradeRequiredValue = EditorGUILayout.IntField("Required Value", upgradeRequiredValue);
        upgradeActualValue = EditorGUILayout.IntField("Actual Value", upgradeActualValue);
        
        EditorGUILayout.Space();
        
        // Test button
        GUI.enabled = Application.isPlaying;
        if (GUILayout.Button("Test Upgrade Condition"))
        {
            TestUpgradeCondition();
        }
        GUI.enabled = true;
        
        // Show result
        if (!string.IsNullOrEmpty(lastUpgradeResult))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Result:", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(lastUpgradeResult);
        }
        
        EditorGUILayout.Space();
        
        // Predefined upgrade tests
        EditorGUILayout.LabelField("Predefined Upgrade Tests", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Run All Upgrade Tests"))
        {
            RunAllUpgradeTests();
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawConditionalTestingSection()
    {
        EditorGUILayout.BeginVertical("box");
        
        EditorGUILayout.LabelField("Custom Conditional Effect Test", EditorStyles.boldLabel);
        
        // Conditional type selection
        selectedConditionalType = (ConditionalType)EditorGUILayout.EnumPopup("Condition Type", selectedConditionalType);
        conditionalThreshold = EditorGUILayout.IntField("Threshold Value", conditionalThreshold);
        conditionalActualValue = EditorGUILayout.IntField("Actual Value", conditionalActualValue);
        
        EditorGUILayout.Space();
        
        // Test button
        GUI.enabled = Application.isPlaying;
        if (GUILayout.Button("Test Conditional Effect"))
        {
            TestConditionalEffect();
        }
        GUI.enabled = true;
        
        // Show result
        if (!string.IsNullOrEmpty(lastConditionalResult))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Result:", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(lastConditionalResult);
        }
        
        EditorGUILayout.Space();
        
        // Predefined conditional tests
        EditorGUILayout.LabelField("Predefined Conditional Tests", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Run All Conditional Tests"))
        {
            RunAllConditionalTests();
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawIntegrationTestingSection()
    {
        EditorGUILayout.BeginVertical("box");
        
        EditorGUILayout.LabelField("Integration & Component Tests", EditorStyles.boldLabel);
        
        showDetailedResults = EditorGUILayout.Toggle("Show Detailed Results", showDetailedResults);
        
        EditorGUILayout.Space();
        
        // Component testing buttons
        GUI.enabled = Application.isPlaying;
        
        if (GUILayout.Button("Test Upgrade Condition Manager"))
        {
            TestUpgradeConditionManager();
        }
        
        if (GUILayout.Button("Test Conditional Effect Manager"))
        {
            TestConditionalEffectManager();
        }
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Run Complete Test Suite"))
        {
            RunCompleteTestSuite();
        }
        
        GUI.enabled = true;
        
        EditorGUILayout.EndVertical();
    }
    
    private void TestUpgradeCondition()
    {
        bool result = UpgradeConditionIntegration.TestUpgradeCondition(
            selectedUpgradeCondition, 
            upgradeRequiredValue, 
            selectedComparisonType, 
            upgradeActualValue
        );
        
        string comparisonSymbol = GetComparisonSymbol(selectedComparisonType);
        lastUpgradeResult = $"Condition: {selectedUpgradeCondition}\n" +
                           $"Test: {upgradeActualValue} {comparisonSymbol} {upgradeRequiredValue}\n" +
                           $"Result: {(result ? "✓ CONDITION MET" : "✗ CONDITION NOT MET")}";
        
        Debug.Log($"[UPGRADE_TEST_WINDOW] {lastUpgradeResult.Replace("\n", " | ")}");
    }
    
    private void TestConditionalEffect()
    {
        bool result = ConditionalEffectIntegration.TestConditionalEffect(
            selectedConditionalType, 
            conditionalThreshold, 
            conditionalActualValue
        );
        
        string comparisonDesc = GetConditionalComparisonDescription(selectedConditionalType);
        lastConditionalResult = $"Condition: {selectedConditionalType}\n" +
                               $"Test: {conditionalActualValue} {comparisonDesc} {conditionalThreshold}\n" +
                               $"Result: {(result ? "✓ CONDITION MET" : "✗ CONDITION NOT MET")}";
        
        Debug.Log($"[CONDITIONAL_TEST_WINDOW] {lastConditionalResult.Replace("\n", " | ")}");
    }
    
    private void RunAllUpgradeTests()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Test Error", "Tests require Play Mode to access game systems.", "OK");
            return;
        }
        
        Debug.Log("[UPGRADE_TEST_WINDOW] Running all upgrade condition tests...");
        UpgradeConditionIntegration.RunUpgradeConditionTests();
        
        lastUpgradeResult = "All upgrade condition tests completed. Check console for detailed results.";
    }
    
    private void RunAllConditionalTests()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Test Error", "Tests require Play Mode to access game systems.", "OK");
            return;
        }
        
        Debug.Log("[CONDITIONAL_TEST_WINDOW] Running all conditional effect tests...");
        ConditionalEffectIntegration.RunConditionalEffectTests();
        
        lastConditionalResult = "All conditional effect tests completed. Check console for detailed results.";
    }
    
    private void TestUpgradeConditionManager()
    {
        var upgradeManager = FindObjectOfType<UpgradeConditionTestManager>();
        if (upgradeManager != null)
        {
            upgradeManager.RunTests();
            Debug.Log("[UPGRADE_TEST_WINDOW] UpgradeConditionTestManager tests started.");
        }
        else
        {
            Debug.LogError("[UPGRADE_TEST_WINDOW] UpgradeConditionTestManager not found in scene!");
            EditorUtility.DisplayDialog("Test Error", "UpgradeConditionTestManager not found in scene!", "OK");
        }
    }
    
    private void TestConditionalEffectManager()
    {
        var conditionalManager = FindObjectOfType<ConditionalEffectTestManager>();
        if (conditionalManager != null)
        {
            conditionalManager.RunTests();
            Debug.Log("[CONDITIONAL_TEST_WINDOW] ConditionalEffectTestManager tests started.");
        }
        else
        {
            Debug.LogError("[CONDITIONAL_TEST_WINDOW] ConditionalEffectTestManager not found in scene!");
            EditorUtility.DisplayDialog("Test Error", "ConditionalEffectTestManager not found in scene!", "OK");
        }
    }
    
    private void RunCompleteTestSuite()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Test Error", "Tests require Play Mode to access game systems.", "OK");
            return;
        }
        
        Debug.Log("[TEST_WINDOW] ═══════════════════════════════════════");
        Debug.Log("[TEST_WINDOW] RUNNING COMPLETE TEST SUITE");
        Debug.Log("[TEST_WINDOW] ═══════════════════════════════════════");
        
        // Run integration tests
        UpgradeConditionIntegration.RunUpgradeConditionTests();
        ConditionalEffectIntegration.RunConditionalEffectTests();
        
        // Run component tests if available
        TestUpgradeConditionManager();
        TestConditionalEffectManager();
        
        Debug.Log("[TEST_WINDOW] ═══════════════════════════════════════");
        Debug.Log("[TEST_WINDOW] COMPLETE TEST SUITE FINISHED");
        Debug.Log("[TEST_WINDOW] ═══════════════════════════════════════");
        
        lastUpgradeResult = "Complete test suite finished. Check console for detailed results.";
        lastConditionalResult = "Complete test suite finished. Check console for detailed results.";
    }
    
    private string GetComparisonSymbol(UpgradeComparisonType comparisonType)
    {
        switch (comparisonType)
        {
            case UpgradeComparisonType.GreaterThanOrEqual: return ">=";
            case UpgradeComparisonType.Equal: return "==";
            case UpgradeComparisonType.LessThanOrEqual: return "<=";
            case UpgradeComparisonType.GreaterThan: return ">";
            case UpgradeComparisonType.LessThan: return "<";
            default: return "?";
        }
    }
    
    private string GetConditionalComparisonDescription(ConditionalType conditionType)
    {
        switch (conditionType)
        {
            case ConditionalType.IfSourceHealthBelow:
            case ConditionalType.IfTargetHealthBelow:
                return "<";
            case ConditionalType.IfSourceHealthAbove:
            case ConditionalType.IfTargetHealthAbove:
                return ">";
            case ConditionalType.IfInStance:
            case ConditionalType.IfLastCardType:
                return "==";
            default:
                return ">=";
        }
    }
}
#endif 