using UnityEngine;
using System.Collections.Generic;

public enum DiscardEffectType
{
    None,
    DealDamageToOpponentPet,
    GainBlockPlayer, 
    GainBlockPet,
    DrawCard,
    GainEnergy
}

public enum ComboEffectType
{
    None,
    DealDamage,        // Renamed from DealDamageToOpponentPet
    GainBlock,         // Renamed from GainBlockPlayer/GainBlockPet
    DrawCard,          // Target is implicitly Player
    GainEnergy         // Target is implicitly Player
    // Add more complex combo effects later
}

public enum UpgradeTargetRule
{
    Random,
    Cheapest,
    MostExpensive
}

public enum OpponentPetTargetType
{
    Player, // The player the pet is fighting
    Self    // The pet itself
}

public enum CritBuffTargetRule
{
    Target, // Apply buff to the entity the card was dropped on
    Self    // Apply buff to the entity playing the card (Player or Opponent Pet)
}

// ADDED: Define the role of the combo effect target
public enum ComboTargetRole
{
    Self,   // The entity playing the card (Player)
    Target  // The entity the card was played on (e.g., EnemyPet, PlayerSelf, OwnPet)
}
// END ADDED

// ADDED: Define role for cost modification target
public enum CostModificationTargetRole
{
    Self,   // The hand of the entity triggering the cost modification (e.g., player playing the card)
    Target  // The hand of the opponent of the entity triggering the modification
}
// END ADDED

[CreateAssetMenu(fileName = "New Card", menuName = "Cards/Card Data")]
public class CardData : ScriptableObject
{
    public string cardName = "New Card";
    public string cardFamilyName = ""; // Added: Base name for grouping base/upgraded cards
    public int cost = 1;
    [TextArea(3, 10)] public string description = "Card Effect Description";
    
    [Header("Card Targeting")]
    [Tooltip("When played by an opponent pet, who is the primary target? Player = the player fighting the pet. Self = the pet itself.")]
    public OpponentPetTargetType primaryTargetWhenPlayedByPet = OpponentPetTargetType.Player; // Default to targeting the player

    [Header("Gameplay Effects")]
    public int damage = 0; // Amount of damage this card deals (0 if none)
    public int block = 0; // Amount of block this card provides (0 if none)
    public int energyGain = 0; // Amount of energy this card provides (0 if none)
    public int drawAmount = 0; // How many cards to draw when played (0 if none)
    public int discardRandomAmount = 0; // How many random cards to discard from hand when played (0 if none)
    public int healingAmount = 0; // Amount of health to restore (0 if none)
    // Add other effects like buffs, debuffs, etc.

    [Header("Turn Scaling Effect")]
    public float damageScalingPerTurn = 0f; // Bonus damage added per combat turn passed
    public float blockScalingPerTurn = 0f; // Bonus block added per combat turn passed

    [Header("Play Count Scaling Effect")]
    public float damageScalingPerPlay = 0f; // Bonus damage per previous play this combat
    public float blockScalingPerPlay = 0f; // Bonus block per previous play this combat

    [Header("Low Health Enhancement")]
    [Range(0, 100)] public int healthThresholdPercent = 0; // HP % below which effect is enhanced (0 = disabled)
    public float damageMultiplierBelowThreshold = 1.0f; // Multiplier for damage when below threshold
    public float blockMultiplierBelowThreshold = 1.0f; // Multiplier for block when below threshold

    [Header("Copy Scaling Effect")]
    public float damageScalingPerCopy = 0f; // Bonus damage per copy in deck+hand+discard
    public float blockScalingPerCopy = 0f; // Bonus block per copy in deck+hand+discard

    [Header("Status Effect Application")]
    public StatusEffectType statusToApply = StatusEffectType.None;
    public int statusDuration = 0; // Turns the status lasts (Used as Amount for Thorns/Strength)
    public int thornsAmount = 0; // Amount of Thorns to apply (0 if none)
    public int strengthAmount = 0; // Amount of Strength to apply (0 if none)

    [Header("Damage Over Time (DoT)")]
    public int dotDamageAmount = 0; // Damage per turn
    public int dotDuration = 0; // Turns the DoT lasts

    [Header("Heal Over Time (HoT)")]
    public int hotAmount = 0; // Healing per turn
    public int hotDuration = 0; // Turns the HoT lasts

    [Header("Combo Effect")]
    public bool isComboStarter = false; // Does this card contribute to/start a combo?
    public int comboTriggerValue = 3; // How many combo cards needed to trigger the effect
    public ComboEffectType comboEffectType = ComboEffectType.None;
    public int comboEffectValue = 0; // Value for the combo effect (e.g., damage, block)
    // ADDED: Determine if combo hits Self (player) or the card's original Target
    [Tooltip("Determines if the combo effect applies to Self (the player) or the Target the card was played on.")]
    public ComboTargetRole comboTargetRole = ComboTargetRole.Target; // Default to hitting the original target

    [Header("Cost Modification Effect")]
    // ADDED: Use target role for cost modification
    [Tooltip("Determines whose hand costs are modified: Self (player triggering) or Target (their opponent).")]
    public CostModificationTargetRole costModificationTargetRole = CostModificationTargetRole.Self; // Default to modifying own hand
    public int costChangeAmount = 0; // e.g., +1 or -1
    public int costChangeDuration = 1; // Turns the change lasts (e.g., 1 for opponent's next turn)
    public int costChangeCardCount = 0; // How many cards targeted (0 = all, >0 = random count)

    [Header("Critical Chance Buff Effect")]
    [Tooltip("Should the crit buff apply to the target it was dropped on (Target), or the entity playing the card (Self)?")]
    public CritBuffTargetRule critBuffRule = CritBuffTargetRule.Self; // Default to Self.
    public int critChanceBuffAmount = 0; // Percentage bonus (e.g., 20 for +20%)
    public int critChanceBuffDuration = 0; // Duration in turns (0 = end of combat)

    [Header("Temporary Card Upgrade Effect")]
    public int upgradeHandCardCount = 0; // How many cards in hand to upgrade
    public int upgradeHandCardDuration = 1; // Duration (0 = rest of combat, 1 = this turn only)
    public UpgradeTargetRule upgradeHandCardTargetRule = UpgradeTargetRule.Random;

    [Header("Discard Trigger Effect")]
    public DiscardEffectType discardEffectType = DiscardEffectType.None;
    public int discardEffectValue = 0; // Value associated with the discard effect (e.g., damage amount, block amount)

    [Header("Upgrade Info")]
    public CardData upgradedVersion = null; // Link to the ScriptableObject representing the upgraded version of this card

    public Sprite cardArt;          // Image displayed on the card itself
    public GameObject targetEffectPrefab; // Prefab with animation to play on target
} 