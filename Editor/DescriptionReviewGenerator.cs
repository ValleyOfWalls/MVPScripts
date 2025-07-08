using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Editor script to generate and review random card descriptions
/// </summary>
public class DescriptionReviewGenerator : EditorWindow
{
    private Vector2 scrollPosition;
    private List<string> generatedDescriptions = new List<string>();
    private List<string> generatedNames = new List<string>();
    private List<CardRarity> generatedRarities = new List<CardRarity>();
    private bool isGenerating = false;
    
    [MenuItem("Tools/Card System/Description Review Generator")]
    public static void ShowWindow()
    {
        GetWindow<DescriptionReviewGenerator>("Description Review Generator");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Random Card Description Generator", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        GUILayout.Label("This tool generates 100 random card descriptions using the same system", EditorStyles.helpBox);
        GUILayout.Label("as the actual card generation logic. Review these to identify areas for improvement.", EditorStyles.helpBox);
        GUILayout.Space(10);
        
        EditorGUI.BeginDisabledGroup(isGenerating);
        if (GUILayout.Button("Generate 100 Random Card Descriptions", GUILayout.Height(30)))
        {
            GenerateDescriptions();
        }
        EditorGUI.EndDisabledGroup();
        
        if (isGenerating)
        {
            GUILayout.Label("Generating descriptions...", EditorStyles.centeredGreyMiniLabel);
        }
        
        GUILayout.Space(10);
        
        if (generatedDescriptions.Count > 0)
        {
            GUILayout.Label($"Generated {generatedDescriptions.Count} Descriptions:", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Copy All to Clipboard"))
            {
                CopyAllToClipboard();
            }
            
            GUILayout.Space(5);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            for (int i = 0; i < generatedDescriptions.Count; i++)
            {
                DrawDescriptionCard(i);
            }
            
            EditorGUILayout.EndScrollView();
        }
    }
    
    private void DrawDescriptionCard(int index)
    {
        EditorGUILayout.BeginVertical("box");
        
        // Header with card number, name, and rarity
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"#{index + 1}", GUILayout.Width(30));
        
        // Color the rarity
        Color originalColor = GUI.color;
        GUI.color = GetRarityColor(generatedRarities[index]);
        GUILayout.Label($"[{generatedRarities[index]}]", EditorStyles.boldLabel, GUILayout.Width(80));
        GUI.color = originalColor;
        
        GUILayout.Label(generatedNames[index], EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        
        if (GUILayout.Button("Copy", GUILayout.Width(50)))
        {
            GUIUtility.systemCopyBuffer = $"{generatedNames[index]} ({generatedRarities[index]})\n{generatedDescriptions[index]}";
        }
        
        EditorGUILayout.EndHorizontal();
        
        // Description text
        EditorGUILayout.SelectableLabel(generatedDescriptions[index], EditorStyles.wordWrappedLabel, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2));
        
        EditorGUILayout.EndVertical();
        GUILayout.Space(2);
    }
    
    private Color GetRarityColor(CardRarity rarity)
    {
        return rarity switch
        {
            CardRarity.Common => Color.white,
            CardRarity.Uncommon => Color.green,
            CardRarity.Rare => Color.cyan,
            _ => Color.white
        };
    }
    
    private void GenerateDescriptions()
    {
        isGenerating = true;
        generatedDescriptions.Clear();
        generatedNames.Clear();
        generatedRarities.Clear();
        
        try
        {
            // Find the config assets
            RandomCardConfig config = AssetDatabase.LoadAssetAtPath<RandomCardConfig>("Assets/MVPScripts/RandomCardConfig.asset");
            if (config == null)
            {
                EditorUtility.DisplayDialog("Error", "Could not find RandomCardConfig.asset", "OK");
                isGenerating = false;
                return;
            }
            
            // Create the procedural generator
            ProceduralCardGenerator generator = new ProceduralCardGenerator(config);
            
            // Generate cards across different rarities (weighted like the actual system)
            List<CardRarity> raritiesToGenerate = new List<CardRarity>();
            
            // Distribute rarities: ~60% Common, ~30% Uncommon, ~10% Rare
            for (int i = 0; i < 60; i++) raritiesToGenerate.Add(CardRarity.Common);
            for (int i = 0; i < 30; i++) raritiesToGenerate.Add(CardRarity.Uncommon);
            for (int i = 0; i < 10; i++) raritiesToGenerate.Add(CardRarity.Rare);
            
            // Shuffle the list for variety
            for (int i = 0; i < raritiesToGenerate.Count; i++)
            {
                var temp = raritiesToGenerate[i];
                int randomIndex = Random.Range(i, raritiesToGenerate.Count);
                raritiesToGenerate[i] = raritiesToGenerate[randomIndex];
                raritiesToGenerate[randomIndex] = temp;
            }
            
            // Generate cards and extract descriptions
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    CardRarity rarity = raritiesToGenerate[i];
                    CardData card = generator.GenerateRandomCard(rarity);
                    
                    if (card != null)
                    {
                        generatedNames.Add(card.CardName ?? "Unnamed Card");
                        generatedDescriptions.Add(card.Description ?? "No description");
                        generatedRarities.Add(card.Rarity);
                    }
                    else
                    {
                        generatedNames.Add("Failed Generation");
                        generatedDescriptions.Add("Card generation failed");
                        generatedRarities.Add(rarity);
                    }
                    
                    // Show progress
                    EditorUtility.DisplayProgressBar("Generating Descriptions", $"Generated {i + 1}/100 cards", (float)(i + 1) / 100f);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error generating card #{i + 1}: {e.Message}");
                    generatedNames.Add($"Error Card #{i + 1}");
                    generatedDescriptions.Add($"Generation error: {e.Message}");
                    generatedRarities.Add(CardRarity.Common);
                }
            }
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to generate descriptions: {e.Message}", "OK");
            Debug.LogError($"Description generation failed: {e}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            isGenerating = false;
            Repaint();
        }
    }
    
    private void CopyAllToClipboard()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== 100 Generated Card Descriptions ===");
        sb.AppendLine();
        
        for (int i = 0; i < generatedDescriptions.Count; i++)
        {
            sb.AppendLine($"#{i + 1} - {generatedNames[i]} ({generatedRarities[i]})");
            sb.AppendLine(generatedDescriptions[i]);
            sb.AppendLine();
        }
        
        GUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log("All descriptions copied to clipboard!");
    }
} 