using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Configuration for rarity distribution in different contexts.
/// Defines how often each rarity appears in draft packs, starter decks, etc.
/// </summary>
[CreateAssetMenu(fileName = "RarityDistributionConfig", menuName = "Card System/Rarity Distribution Config")]
public class RarityDistributionConfig : ScriptableObject
{
    [Header("Draft Pack Distribution")]
    [SerializeField, Tooltip("Percentage chance for Common cards in draft packs")]
    [Range(0f, 100f)]
    public float draftCommonPercentage = 75f;
    
    [SerializeField, Tooltip("Percentage chance for Uncommon cards in draft packs")]
    [Range(0f, 100f)]
    public float draftUncommonPercentage = 20f;
    
    [SerializeField, Tooltip("Percentage chance for Rare cards in draft packs")]
    [Range(0f, 100f)]
    public float draftRarePercentage = 5f;
    
    [Header("Starter Deck Composition")]
    [SerializeField, Tooltip("Number of Common cards in starter decks")]
    public int starterDeckCommons = 6;
    
    [SerializeField, Tooltip("Number of Uncommon cards in starter decks")]
    public int starterDeckUncommons = 3;
    
    [SerializeField, Tooltip("Number of Rare cards in starter decks")]
    public int starterDeckRares = 1;
    
    [Header("Validation Settings")]
    [SerializeField, Tooltip("Ensure draft percentages always add up to 100%")]
    public bool normalizeDraftPercentages = true;
    
    /// <summary>
    /// Get total cards in a starter deck
    /// </summary>
    public int GetStarterDeckSize()
    {
        return starterDeckCommons + starterDeckUncommons + starterDeckRares;
    }
    
    /// <summary>
    /// Validate configuration in the Unity Inspector
    /// </summary>
    void OnValidate()
    {
        if (normalizeDraftPercentages)
        {
            float total = draftCommonPercentage + draftUncommonPercentage + draftRarePercentage;
            if (Mathf.Abs(total - 100f) > 0.1f)
            {
                Debug.LogWarning($"Draft percentages don't add to 100% (currently {total:F1}%). " +
                               "They will be normalized at runtime.");
            }
        }
    }

    public bool ValidateDistribution()
    {
        float total = draftCommonPercentage + draftUncommonPercentage + draftRarePercentage;
        return Mathf.Approximately(total, 100f);
    }
    
    /// <summary>
    /// Get a random rarity for draft packs based on the configured distribution
    /// </summary>
    public CardRarity GetRandomDraftRarity()
    {
        float random = UnityEngine.Random.Range(0f, 100f);
        
        if (random < draftCommonPercentage)
        {
            return CardRarity.Common;
        }
        else if (random < draftCommonPercentage + draftUncommonPercentage)
        {
            return CardRarity.Uncommon;
        }
        else
        {
            return CardRarity.Rare;
        }
    }
    
    /// <summary>
    /// Get normalized percentages that add up to 100%
    /// </summary>
    public (float common, float uncommon, float rare) GetNormalizedDraftPercentages()
    {
        float total = draftCommonPercentage + draftUncommonPercentage + draftRarePercentage;
        
        if (total <= 0f)
        {
            // Fallback to equal distribution
            return (33.33f, 33.33f, 33.34f);
        }
        
        float normalizer = 100f / total;
        return (
            draftCommonPercentage * normalizer,
            draftUncommonPercentage * normalizer,
            draftRarePercentage * normalizer
        );
    }
    
    /// <summary>
    /// Get starter deck composition as a list of rarities
    /// </summary>
    public List<CardRarity> GetStarterDeckComposition()
    {
        var composition = new List<CardRarity>();
        
        // Add commons
        for (int i = 0; i < starterDeckCommons; i++)
        {
            composition.Add(CardRarity.Common);
        }
        
        // Add uncommons
        for (int i = 0; i < starterDeckUncommons; i++)
        {
            composition.Add(CardRarity.Uncommon);
        }
        
        // Add rares
        for (int i = 0; i < starterDeckRares; i++)
        {
            composition.Add(CardRarity.Rare);
        }
        
        return composition;
    }
    
    /// <summary>
    /// Get the number of cards of a specific rarity needed for a starter deck
    /// </summary>
    public int GetStarterDeckCount(CardRarity rarity)
    {
        return rarity switch
        {
            CardRarity.Common => starterDeckCommons,
            CardRarity.Uncommon => starterDeckUncommons,
            CardRarity.Rare => starterDeckRares,
            _ => 0
        };
    }
} 