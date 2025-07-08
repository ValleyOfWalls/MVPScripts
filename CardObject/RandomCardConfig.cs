using UnityEngine;

/// <summary>
/// Master configuration for randomized card generation system.
/// This ScriptableObject holds references to all other configuration objects
/// needed for the procedural card generation system.
/// </summary>
[CreateAssetMenu(fileName = "RandomCardConfig", menuName = "Card System/Random Card Config")]
public class RandomCardConfig : ScriptableObject
{
    [Header("Point Budget System")]
    [SerializeField, Tooltip("Configuration for point budgets per rarity tier")]
    public CardPointBudgetConfig pointBudgetConfig;
    
    [Header("Effect Costs")]
    [SerializeField, Tooltip("Configuration for effect point costs and modifiers")]
    public EffectPointCostConfig effectCostConfig;
    
    [Header("Rarity Distribution")]
    [SerializeField, Tooltip("Configuration for rarity distribution in draft packs")]
    public RarityDistributionConfig rarityDistributionConfig;
    
    [Header("Upgrade System")]
    [SerializeField, Tooltip("Configuration for upgrade condition point costs")]
    public UpgradeConditionCostConfig upgradeCostConfig;
    
    [Header("Generation Settings")]
    [SerializeField, Tooltip("Minimum number of effects per card")]
    public int minEffectsPerCard = 1;
    
    [SerializeField, Tooltip("Maximum number of effects per card")]
    public int maxEffectsPerCard = 3;
    
    [SerializeField, Tooltip("Allow cards to have no upgrade conditions (upgrade immediately)")]
    public bool allowNoUpgradeCondition = true;
    
    [SerializeField, Tooltip("Percentage chance for a card to have no upgrade condition")]
    [Range(0f, 1f)]
    public float noUpgradeConditionChance = 0.1f;
} 