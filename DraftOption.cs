using UnityEngine;
using System;

public enum DraftOptionType
{
    AddPlayerCard,
    AddPetCard,
    UpgradePlayerCard, // Requires specifying which card
    UpgradePetCard,   // Requires specifying which card
    UpgradePlayerStat,
    UpgradePetStat
}

public enum StatType
{
    MaxHealth,
    StartingEnergy,
    // Add other stats as needed (e.g., Strength, Dexterity)
}

[Serializable] // Make it serializable for potential network transfer or saving
public class DraftOption
{
    public int OptionId { get; private set; } // Unique ID for this option instance in a draft pool
    public DraftOptionType Type;
    public string Description;

    // Specific data based on type
    public CardData CardToAdd;        // For AddPlayerCard, AddPetCard
    public CardData CardToUpgrade;    // For UpgradePlayerCard, UpgradePetCard (Could be index/ID instead)
    public StatType StatToUpgrade;    // For UpgradePlayerStat, UpgradePetStat
    public int StatIncreaseAmount; // For UpgradePlayerStat, UpgradePetStat

    // Constructor examples (can add more specific ones)
    public DraftOption(int id)
    {
        OptionId = id;
    }

    // Static factory methods for easier creation
    public static DraftOption CreateAddCardOption(int id, CardData card, bool forPet)
    {
        return new DraftOption(id)
        {
            Type = forPet ? DraftOptionType.AddPetCard : DraftOptionType.AddPlayerCard,
            CardToAdd = card,
            Description = $"Add {(forPet ? "Pet" : "Player")} Card: {card.cardName}"
        };
    }

    public static DraftOption CreateUpgradeStatOption(int id, StatType stat, int amount, bool forPet)
    {
        return new DraftOption(id)
        {
            Type = forPet ? DraftOptionType.UpgradePetStat : DraftOptionType.UpgradePlayerStat,
            StatToUpgrade = stat,
            StatIncreaseAmount = amount,
            Description = $"Upgrade {(forPet ? "Pet" : "Player")} Stat: +{amount} {stat}"
        };
    }
    
    public static DraftOption CreateUpgradeCardOption(int id, CardData cardToUpgrade, bool forPet)
    {
        if (cardToUpgrade == null || cardToUpgrade.upgradedVersion == null)
        {
            Debug.LogWarning($"Cannot create UpgradeCardOption for {cardToUpgrade?.cardName} - null card or no upgraded version defined.");
            return null; // Cannot create if no upgrade path
        }
        return new DraftOption(id)
        {
            Type = forPet ? DraftOptionType.UpgradePetCard : DraftOptionType.UpgradePlayerCard,
            CardToUpgrade = cardToUpgrade,
            Description = $"Upgrade {(forPet ? "Pet" : "Player")} Card: {cardToUpgrade.cardName} -> {cardToUpgrade.upgradedVersion.cardName}"
        };
    }

    // NOTE: For network synchronization, directly serializing this class with CardData might be tricky.
    // We might need to convert these options to a simpler format (e.g., using Card IDs/Names, enum indices)
    // before putting them into Photon Custom Properties.
} 