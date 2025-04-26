using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon; // Required for Hashtable
using Newtonsoft.Json; // Using Newtonsoft.Json for easier serialization

// Helper class for serializing draft options via JSON for Photon Properties
[System.Serializable]
public class SerializableDraftOption
{
    public int OptionId;
    public DraftOptionType Type;
    public string Description;

    // Store identifiers instead of direct references
    public string CardName;        // For Add Card OR Card to UPGRADE
    public bool IsPetCard;
    public StatType StatToUpgrade; // For Upgrade Stat
    public int StatIncreaseAmount; // For Upgrade Stat
    public bool IsPetStat;

    // Convert from DraftOption to SerializableDraftOption
    public static SerializableDraftOption FromDraftOption(DraftOption option)
    {
        var serializable = new SerializableDraftOption
        {
            OptionId = option.OptionId,
            Type = option.Type,
            Description = option.Description,
            StatToUpgrade = option.StatToUpgrade,
            StatIncreaseAmount = option.StatIncreaseAmount,
            IsPetStat = (option.Type == DraftOptionType.UpgradePetStat)
        };

        if (option.Type == DraftOptionType.AddPlayerCard || option.Type == DraftOptionType.AddPetCard)
        {
            serializable.CardName = option.CardToAdd?.cardName;
            serializable.IsPetCard = (option.Type == DraftOptionType.AddPetCard);
        }
        else if (option.Type == DraftOptionType.UpgradePlayerCard || option.Type == DraftOptionType.UpgradePetCard)
        {
            serializable.CardName = option.CardToUpgrade?.cardName; // Store the NAME of the card to be upgraded
            serializable.IsPetCard = (option.Type == DraftOptionType.UpgradePetCard);
        }
        // TODO: Handle UpgradePlayerCard / UpgradePetCard serialization (e.g., store CardName of card to upgrade)

        return serializable;
    }

    // Convert from SerializableDraftOption back to DraftOption
    public DraftOption ToDraftOption(List<CardData> playerCardPool, List<CardData> petCardPool, List<CardData> playerDeck, List<CardData> petDeck)
    {
        DraftOption option = new DraftOption(this.OptionId)
        {
            Type = this.Type,
            Description = this.Description,
            StatToUpgrade = this.StatToUpgrade,
            StatIncreaseAmount = this.StatIncreaseAmount
        };

        if (this.Type == DraftOptionType.AddPlayerCard || this.Type == DraftOptionType.AddPetCard)
        {
            List<CardData> pool = this.IsPetCard ? petCardPool : playerCardPool;
            option.CardToAdd = pool?.FirstOrDefault(card => card.cardName == this.CardName);
            if (option.CardToAdd == null && !string.IsNullOrEmpty(this.CardName))
            {
                Debug.LogWarning($"Could not find card with name '{this.CardName}' in the corresponding pool during deserialization.");
                return null; // Indicate failure to deserialize fully
            }
        }
        else if (this.Type == DraftOptionType.UpgradePlayerCard || this.Type == DraftOptionType.UpgradePetCard)
        {
            // When deserializing an upgrade option, we need the ACTUAL CardData object to upgrade from the player's/pet's deck
            List<CardData> deckToCheck = this.IsPetCard ? petDeck : playerDeck;
            option.CardToUpgrade = deckToCheck?.FirstOrDefault(card => card.cardName == this.CardName && card.upgradedVersion != null);
            if (option.CardToUpgrade == null && !string.IsNullOrEmpty(this.CardName))
            {
                // This might happen if the card was somehow removed from the deck before deserialization, or never existed.
                // It's also possible the card existed but didn't have an upgrade path when the option was created.
                Debug.LogWarning($"Could not find upgradable card with name '{this.CardName}' in the corresponding deck during deserialization. Type: {this.Type}");
                // We *could* return null, but maybe the UI should just display the description and fail gracefully if selected?
                // For now, let's return the option but with CardToUpgrade being null.
                // The ApplyDraftChoice logic will need to handle this null case.
            }
            else if (option.CardToUpgrade != null)
            {
                // We found the card, reconstruct the description accurately using the found card and its upgrade
                 option.Description = $"Upgrade {(this.IsPetCard ? "Pet" : "Player")} Card: {option.CardToUpgrade.cardName} -> {option.CardToUpgrade.upgradedVersion.cardName}";
            }
        }
        // TODO: Handle UpgradePlayerCard / UpgradePetCard deserialization

        return option;
    }
} 