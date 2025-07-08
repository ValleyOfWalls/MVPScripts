using UnityEngine;
using System;

/// <summary>
/// Configuration for point budgets per card rarity tier.
/// Higher rarity cards get more points to spend on effects.
/// 
/// SCALING RATIONALE (Updated for 100 HP, ~7 round fights, 100 energy per turn):
/// - Common (35 points): 25-35 energy cost, 50-75 damage/healing potential
/// - Uncommon (55 points): 35-45 energy cost, 75-120 damage/healing potential  
/// - Rare (80 points): 45-55 energy cost, 120-180 damage/healing potential
/// 
/// With these values:
/// - Players can play 2-3 cards per turn (energy costs 25-55 per card)
/// - Fights last ~7 rounds (100 HP ÷ 15-20 average damage per turn)
/// - Energy restoration becomes meaningful and strategic
/// - Status effects and shields scale appropriately for longer fights
/// </summary>
[CreateAssetMenu(fileName = "CardPointBudgetConfig", menuName = "Card System/Point Budget Config")]
public class CardPointBudgetConfig : ScriptableObject
{
    [Header("Point Budgets by Rarity")]
    [SerializeField, Tooltip("Base point budget for Common cards")]
    public int commonPointBudget = 35;
    
    [SerializeField, Tooltip("Base point budget for Uncommon cards")]
    public int uncommonPointBudget = 55;
    
    [SerializeField, Tooltip("Base point budget for Rare cards")]
    public int rarePointBudget = 80;
    
    [Header("Budget Variation")]
    [SerializeField, Tooltip("Random variation in point budget (+/- this amount)")]
    [Range(0, 5)]
    public int budgetVariation = 3;
    
    [Header("Energy Cost Budget Allocation")]
    [SerializeField, Tooltip("Percentage of budget that goes to determining energy cost")]
    [Range(0f, 1f)]
    public float energyCostBudgetPercentage = 0.5f;
    
    [SerializeField, Tooltip("Points per energy cost (lower = more expensive cards)")]
    public float pointsPerEnergyCost = 0.5f;
    
    /// <summary>
    /// Get the point budget for a specific rarity, including random variation
    /// </summary>
    public int GetPointBudget(CardRarity rarity)
    {
        int baseBudget = rarity switch
        {
            CardRarity.Common => commonPointBudget,
            CardRarity.Uncommon => uncommonPointBudget,
            CardRarity.Rare => rarePointBudget,
            _ => commonPointBudget
        };
        
        // Add random variation
        int variation = UnityEngine.Random.Range(-budgetVariation, budgetVariation + 1);
        return Mathf.Max(1, baseBudget + variation);
    }
    
    /// <summary>
    /// Calculate energy cost based on point budget allocation
    /// </summary>
    public int CalculateEnergyCost(int totalBudget)
    {
        float energyBudget = totalBudget * energyCostBudgetPercentage;
        int energyCost = Mathf.RoundToInt(energyBudget / pointsPerEnergyCost);
        return Mathf.Max(0, energyCost);
    }
    
    /// <summary>
    /// Get remaining budget after energy cost allocation
    /// </summary>
    public int GetRemainingBudget(int totalBudget, int energyCost)
    {
        float usedBudget = energyCost * pointsPerEnergyCost;
        return Mathf.Max(0, totalBudget - Mathf.RoundToInt(usedBudget));
    }
} 