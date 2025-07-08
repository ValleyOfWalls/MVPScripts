using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages draft pack generation with configurable rarity distributions when randomization is enabled.
/// This replaces the random card selection in draft packs with rarity-aware selection.
/// </summary>
public class RandomizedDraftPackManager : MonoBehaviour
{
    [Header("Rarity Configuration")]
    [SerializeField, Tooltip("Configuration for rarity distributions")]
    private RarityDistributionConfig rarityDistributionConfig;
    
    [Header("Pack Configuration")]
    [SerializeField, Tooltip("Guaranteed minimum of each rarity per pack (0 = no guarantee)")]
    private int minCommonPerPack = 0;
    
    [SerializeField, Tooltip("Guaranteed minimum of each rarity per pack (0 = no guarantee)")]
    private int minUncommonPerPack = 0;
    
    [SerializeField, Tooltip("Guaranteed minimum of each rarity per pack (0 = no guarantee)")]
    private int minRarePerPack = 0;
    
    public static RandomizedDraftPackManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Generate cards for a draft pack using rarity distribution if randomization is enabled
    /// </summary>
    public List<CardData> GenerateDraftPackCards(int packSize)
    {
        // Check if randomization is enabled
        if (OnlineGameManager.Instance == null || !OnlineGameManager.Instance.RandomizationEnabled.Value)
        {
            // Use default behavior - just get random cards
            return CardDatabase.Instance.GetRandomCardsWithDuplicates(packSize);
        }
        
        if (rarityDistributionConfig == null)
        {
            Debug.LogWarning("RandomizedDraftPackManager: No rarity distribution config found, using default random selection");
            return CardDatabase.Instance.GetRandomCardsWithDuplicates(packSize);
        }
        
        return GenerateRarityBasedDraftPack(packSize);
    }
    
    /// <summary>
    /// Generate a draft pack with proper rarity distribution
    /// </summary>
    private List<CardData> GenerateRarityBasedDraftPack(int packSize)
    {
        var packCards = new List<CardData>();
        var draftableCards = CardDatabase.Instance.GetDraftableCards();
        
        if (draftableCards.Count == 0)
        {
            Debug.LogError("RandomizedDraftPackManager: No draftable cards available!");
            return packCards;
        }
        
        // Separate cards by rarity
        var cardsByRarity = SeparateCardsByRarity(draftableCards);
        
        // Start with guaranteed minimums
        packCards.AddRange(GetGuaranteedCards(cardsByRarity));
        
        // Fill remaining slots with random cards based on distribution
        int remainingSlots = packSize - packCards.Count;
        
        for (int i = 0; i < remainingSlots; i++)
        {
            var rarity = rarityDistributionConfig.GetRandomDraftRarity();
            var card = GetRandomCardOfRarity(cardsByRarity, rarity);
            
            if (card != null)
            {
                packCards.Add(card);
            }
            else
            {
                // Fallback to any available card if no cards of the selected rarity
                card = GetRandomCardOfAnyRarity(cardsByRarity);
                if (card != null)
                {
                    packCards.Add(card);
                }
            }
        }
        
        // Shuffle the pack so guaranteed cards aren't always first
        return ShuffleCards(packCards);
    }
    
    /// <summary>
    /// Separate draftable cards by their rarity
    /// </summary>
    private Dictionary<CardRarity, List<CardData>> SeparateCardsByRarity(List<CardData> cards)
    {
        var cardsByRarity = new Dictionary<CardRarity, List<CardData>>
        {
            [CardRarity.Common] = new List<CardData>(),
            [CardRarity.Uncommon] = new List<CardData>(),
            [CardRarity.Rare] = new List<CardData>()
        };
        
        foreach (var card in cards)
        {
            if (cardsByRarity.ContainsKey(card.Rarity))
            {
                cardsByRarity[card.Rarity].Add(card);
            }
            else
            {
                // Default to common if rarity is unknown
                cardsByRarity[CardRarity.Common].Add(card);
            }
        }
        
        return cardsByRarity;
    }
    
    /// <summary>
    /// Get guaranteed cards based on minimum requirements
    /// </summary>
    private List<CardData> GetGuaranteedCards(Dictionary<CardRarity, List<CardData>> cardsByRarity)
    {
        var guaranteedCards = new List<CardData>();
        
        // Add guaranteed commons
        for (int i = 0; i < minCommonPerPack; i++)
        {
            var card = GetRandomCardOfRarity(cardsByRarity, CardRarity.Common);
            if (card != null)
            {
                guaranteedCards.Add(card);
            }
        }
        
        // Add guaranteed uncommons
        for (int i = 0; i < minUncommonPerPack; i++)
        {
            var card = GetRandomCardOfRarity(cardsByRarity, CardRarity.Uncommon);
            if (card != null)
            {
                guaranteedCards.Add(card);
            }
        }
        
        // Add guaranteed rares
        for (int i = 0; i < minRarePerPack; i++)
        {
            var card = GetRandomCardOfRarity(cardsByRarity, CardRarity.Rare);
            if (card != null)
            {
                guaranteedCards.Add(card);
            }
        }
        
        return guaranteedCards;
    }
    
    /// <summary>
    /// Get a random card of a specific rarity (allows duplicates)
    /// </summary>
    private CardData GetRandomCardOfRarity(Dictionary<CardRarity, List<CardData>> cardsByRarity, CardRarity rarity)
    {
        if (!cardsByRarity.ContainsKey(rarity) || cardsByRarity[rarity].Count == 0)
        {
            return null;
        }
        
        var rarityCards = cardsByRarity[rarity];
        return rarityCards[UnityEngine.Random.Range(0, rarityCards.Count)];
    }
    
    /// <summary>
    /// Get a random card of any available rarity (fallback)
    /// </summary>
    private CardData GetRandomCardOfAnyRarity(Dictionary<CardRarity, List<CardData>> cardsByRarity)
    {
        var allCards = new List<CardData>();
        
        foreach (var rarityList in cardsByRarity.Values)
        {
            allCards.AddRange(rarityList);
        }
        
        if (allCards.Count == 0)
        {
            return null;
        }
        
        return allCards[UnityEngine.Random.Range(0, allCards.Count)];
    }
    
    /// <summary>
    /// Shuffle a list of cards
    /// </summary>
    private List<CardData> ShuffleCards(List<CardData> cards)
    {
        var shuffled = new List<CardData>(cards);
        
        for (int i = 0; i < shuffled.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, shuffled.Count);
            (shuffled[i], shuffled[randomIndex]) = (shuffled[randomIndex], shuffled[i]);
        }
        
        return shuffled;
    }
    
    /// <summary>
    /// Get rarity distribution statistics for a list of cards
    /// </summary>
    public RarityStatistics GetRarityStatistics(List<CardData> cards)
    {
        var stats = new RarityStatistics();
        
        foreach (var card in cards)
        {
            switch (card.Rarity)
            {
                case CardRarity.Common:
                    stats.commonCount++;
                    break;
                case CardRarity.Uncommon:
                    stats.uncommonCount++;
                    break;
                case CardRarity.Rare:
                    stats.rareCount++;
                    break;
            }
        }
        
        stats.totalCards = cards.Count;
        
        return stats;
    }
    
    /// <summary>
    /// Validate rarity distribution configuration
    /// </summary>
    [ContextMenu("Validate Rarity Configuration")]
    public void ValidateRarityConfiguration()
    {
        if (rarityDistributionConfig == null)
        {
            Debug.LogError("RandomizedDraftPackManager: No rarity distribution config assigned!");
            return;
        }
        
        var (common, uncommon, rare) = rarityDistributionConfig.GetNormalizedDraftPercentages();
        
        Debug.Log($"Rarity Distribution Configuration:");
        Debug.Log($"  Common: {common:F1}%");
        Debug.Log($"  Uncommon: {uncommon:F1}%");
        Debug.Log($"  Rare: {rare:F1}%");
        Debug.Log($"  Total: {common + uncommon + rare:F1}%");
        
        // Test pack generation
        Debug.Log("\nTesting pack generation...");
        var testPack = GenerateRarityBasedDraftPack(4);
        var stats = GetRarityStatistics(testPack);
        
        Debug.Log($"Test Pack Contents ({stats.totalCards} cards):");
        Debug.Log($"  Common: {stats.commonCount} ({stats.CommonPercentage:F1}%)");
        Debug.Log($"  Uncommon: {stats.uncommonCount} ({stats.UncommonPercentage:F1}%)");
        Debug.Log($"  Rare: {stats.rareCount} ({stats.RarePercentage:F1}%)");
    }
}

/// <summary>
/// Statistics about rarity distribution in a collection of cards
/// </summary>
[System.Serializable]
public class RarityStatistics
{
    public int commonCount = 0;
    public int uncommonCount = 0;
    public int rareCount = 0;
    public int totalCards = 0;
    
    public float CommonPercentage => totalCards > 0 ? (commonCount / (float)totalCards) * 100f : 0f;
    public float UncommonPercentage => totalCards > 0 ? (uncommonCount / (float)totalCards) * 100f : 0f;
    public float RarePercentage => totalCards > 0 ? (rareCount / (float)totalCards) * 100f : 0f;
} 