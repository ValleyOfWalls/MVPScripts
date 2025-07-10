using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Editor tool to validate that all card effects from the reference document
/// can be generated properly with descriptions by the ProceduralCardGenerator
/// </summary>
public class CardEffectGenerationValidator : EditorWindow
{
    private Vector2 scrollPosition;
    private List<CardEffectValidationResult> validationResults = new List<CardEffectValidationResult>();
    private bool isValidating = false;
    private bool showFailuresOnly = false;
    private bool showDetailedResults = true;
    
    private readonly CardEffectType[] allSupportedEffects = {
        // Core Effects
        CardEffectType.Damage,
        CardEffectType.Heal,
        
        // Defensive Status Effects
        CardEffectType.ApplyShield,
        CardEffectType.ApplyThorns,
        
        // Positive Status Effects
        CardEffectType.ApplyStrength,
        CardEffectType.ApplySalve,
        CardEffectType.RaiseCriticalChance,
        
        // Negative Status Effects
        CardEffectType.ApplyWeak,
        CardEffectType.ApplyBreak,
        CardEffectType.ApplyBurn,
        CardEffectType.ApplyCurse,
        CardEffectType.ApplyStun,
        
        // Stance Effects
        CardEffectType.EnterStance,
        CardEffectType.ExitStance
    };
    
    [MenuItem("Tools/Card System/Card Effect Generation Validator")]
    public static void ShowWindow()
    {
        GetWindow<CardEffectGenerationValidator>("Card Effect Validator");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Card Effect Generation Validator", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        GUILayout.Label("This tool validates that all card effects from the reference document", EditorStyles.helpBox);
        GUILayout.Label("can be generated properly with descriptions by the ProceduralCardGenerator.", EditorStyles.helpBox);
        GUILayout.Space(10);
        
        EditorGUI.BeginDisabledGroup(isValidating);
        if (GUILayout.Button("Validate All Card Effects (1000 generations per effect)", GUILayout.Height(30)))
        {
            ValidateAllCardEffects();
        }
        EditorGUI.EndDisabledGroup();
        
        if (isValidating)
        {
            GUILayout.Label("Validating card effects...", EditorStyles.centeredGreyMiniLabel);
        }
        
        GUILayout.Space(10);
        
        if (validationResults.Count > 0)
        {
            DrawValidationResults();
        }
    }
    
    private void DrawValidationResults()
    {
        GUILayout.Label("Validation Results:", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        showFailuresOnly = EditorGUILayout.Toggle("Show Failures Only", showFailuresOnly);
        showDetailedResults = EditorGUILayout.Toggle("Show Detailed Results", showDetailedResults);
        EditorGUILayout.EndHorizontal();
        
        if (GUILayout.Button("Copy Results to Clipboard"))
        {
            CopyResultsToClipboard();
        }
        
        GUILayout.Space(5);
        
        // Summary stats
        int passedCount = validationResults.Count(r => r.canGenerate);
        int failedCount = validationResults.Count - passedCount;
        
        Color originalColor = GUI.color;
        GUI.color = failedCount > 0 ? Color.red : Color.green;
        GUILayout.Label($"Results: {passedCount}/{validationResults.Count} effects can be generated", EditorStyles.boldLabel);
        GUI.color = originalColor;
        
        GUILayout.Space(10);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        var resultsToShow = showFailuresOnly ? 
            validationResults.Where(r => !r.canGenerate).ToList() : 
            validationResults;
        
        foreach (var result in resultsToShow)
        {
            DrawValidationResult(result);
        }
        
        EditorGUILayout.EndScrollView();
    }
    
    private void DrawValidationResult(CardEffectValidationResult result)
    {
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = result.canGenerate ? Color.green : Color.red;
        
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = originalColor;
        
        // Header
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(result.effectType.ToString(), EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label(result.canGenerate ? "✅ CAN GENERATE" : "❌ CANNOT GENERATE", 
            result.canGenerate ? EditorStyles.label : EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();
        
        // Stats
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"Generated: {result.generatedCount}/{result.attemptedCount}", GUILayout.Width(120));
        GUILayout.Label($"Success Rate: {(result.generatedCount / (float)result.attemptedCount * 100):F1}%", GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();
        
        // Sample descriptions
        if (showDetailedResults && result.sampleDescriptions.Count > 0)
        {
            GUILayout.Label("Sample Descriptions:", EditorStyles.miniLabel);
            foreach (var desc in result.sampleDescriptions.Take(3))
            {
                EditorGUILayout.SelectableLabel(desc, EditorStyles.wordWrappedMiniLabel, GUILayout.MinHeight(20));
            }
        }
        
        // Errors
        if (!string.IsNullOrEmpty(result.errorMessage))
        {
            GUILayout.Label($"Error: {result.errorMessage}", EditorStyles.miniLabel);
        }
        
        EditorGUILayout.EndVertical();
        GUILayout.Space(2);
    }
    
    private void ValidateAllCardEffects()
    {
        isValidating = true;
        validationResults.Clear();
        
        try
        {
            // Find the config assets
            RandomCardConfig config = AssetDatabase.LoadAssetAtPath<RandomCardConfig>("Assets/MVPScripts/RandomCardConfig.asset");
            if (config == null)
            {
                EditorUtility.DisplayDialog("Error", "Could not find RandomCardConfig.asset", "OK");
                isValidating = false;
                return;
            }
            
            // Create the procedural generator
            ProceduralCardGenerator generator = new ProceduralCardGenerator(config);
            
            for (int i = 0; i < allSupportedEffects.Length; i++)
            {
                var effectType = allSupportedEffects[i];
                
                EditorUtility.DisplayProgressBar("Validating Card Effects", 
                    $"Testing {effectType}...", (float)i / allSupportedEffects.Length);
                
                var result = ValidateCardEffect(generator, effectType);
                validationResults.Add(result);
            }
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Validation failed: {e.Message}", "OK");
            Debug.LogError($"Card effect validation failed: {e}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            isValidating = false;
            Repaint();
        }
    }
    
    private CardEffectValidationResult ValidateCardEffect(ProceduralCardGenerator generator, CardEffectType effectType)
    {
        var result = new CardEffectValidationResult
        {
            effectType = effectType,
            attemptedCount = 1000,
            generatedCount = 0,
            sampleDescriptions = new List<string>(),
            errorMessage = ""
        };
        
        // Generate 1000 cards and count how many contain this effect
        for (int i = 0; i < 1000; i++)
        {
            try
            {
                // Try all rarities to maximize chances
                CardRarity rarity = (CardRarity)(i % 3);
                CardData card = generator.GenerateRandomCard(rarity);
                
                if (card != null && card.Effects != null)
                {
                    bool hasEffect = card.Effects.Any(e => e.effectType == effectType);
                    
                    if (hasEffect)
                    {
                        result.generatedCount++;
                        
                        // Collect sample descriptions
                        if (result.sampleDescriptions.Count < 10)
                        {
                            string cardDesc = $"[{card.CardName}] {card.Description}";
                            if (!result.sampleDescriptions.Contains(cardDesc))
                            {
                                result.sampleDescriptions.Add(cardDesc);
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                result.errorMessage = $"Generation error: {e.Message}";
            }
        }
        
        result.canGenerate = result.generatedCount > 0;
        return result;
    }
    
    private void CopyResultsToClipboard()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Card Effect Generation Validation Results ===");
        sb.AppendLine();
        
        int passedCount = validationResults.Count(r => r.canGenerate);
        int failedCount = validationResults.Count - passedCount;
        
        sb.AppendLine($"Summary: {passedCount}/{validationResults.Count} effects can be generated");
        sb.AppendLine($"Passed: {passedCount}");
        sb.AppendLine($"Failed: {failedCount}");
        sb.AppendLine();
        
        foreach (var result in validationResults)
        {
            sb.AppendLine($"{result.effectType}: {(result.canGenerate ? "✅ CAN GENERATE" : "❌ CANNOT GENERATE")}");
            sb.AppendLine($"  Generated: {result.generatedCount}/{result.attemptedCount} ({(result.generatedCount / (float)result.attemptedCount * 100):F1}%)");
            
            if (!string.IsNullOrEmpty(result.errorMessage))
            {
                sb.AppendLine($"  Error: {result.errorMessage}");
            }
            
            if (result.sampleDescriptions.Count > 0)
            {
                sb.AppendLine("  Sample Descriptions:");
                foreach (var desc in result.sampleDescriptions.Take(2))
                {
                    sb.AppendLine($"    - {desc}");
                }
            }
            
            sb.AppendLine();
        }
        
        GUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log("Validation results copied to clipboard");
    }
}

/// <summary>
/// Result of validating a single card effect type
/// </summary>
[System.Serializable]
public class CardEffectValidationResult
{
    public CardEffectType effectType;
    public int attemptedCount;
    public int generatedCount;
    public bool canGenerate;
    public List<string> sampleDescriptions = new List<string>();
    public string errorMessage;
} 