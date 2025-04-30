using UnityEngine;
using UnityEditor;
using System.IO; // Required for Path operations

namespace Combat.EditorScripts // Use a namespace to avoid conflicts
{
    public class CardDataGenerator
    {
        // Define the path where card assets will be saved
        private const string CardSavePath = "Assets/Data/Cards"; 

        [MenuItem("Assets/Create/Combat/Sample Card Assets")]
        public static void CreateSampleCardAssets()
        {
            // Ensure the target directory exists
            if (!Directory.Exists(CardSavePath))
            {
                Directory.CreateDirectory(CardSavePath);
                Debug.Log($"Created directory: {CardSavePath}");
            }

            // Create Strike Card
            CreateCardAsset(
                cardName: "Strike",
                description: "Deal 6 damage",
                manaCost: 1,
                cardType: CardType.Attack,
                baseValue: 6,
                effectType: EffectType.Damage,
                effectValue: 6,
                targetType: TargetType.SingleEnemy,
                artworkPath: "", // Optional: Set path like "Artwork/Cards/Strike"
                typeIconPath: "" // Optional: Set path like "UI/Icons/AttackIcon"
            );

            // Create Defend Card
            CreateCardAsset(
                cardName: "Defend",
                description: "Gain 5 block",
                manaCost: 1,
                cardType: CardType.Skill,
                baseValue: 5,
                effectType: EffectType.Block,
                effectValue: 5,
                targetType: TargetType.Self,
                artworkPath: "", // Optional: Set path like "Artwork/Cards/Defend"
                typeIconPath: "" // Optional: Set path like "UI/Icons/SkillIcon"
            );

            // Create Power Up Card
            CreateCardAsset(
                cardName: "Power Up",
                description: "Gain 1 energy next turn", // Placeholder description
                manaCost: 2, // Placeholder cost from original code
                cardType: CardType.Power,
                baseValue: 1, // Placeholder value, e.g., energy amount
                effectType: EffectType.DrawCard, // Placeholder effect from original code (using DrawCard as an example)
                effectValue: 1, // Placeholder effect value
                targetType: TargetType.Self, // Placeholder target
                artworkPath: "", // Optional: Set path like "Artwork/Cards/PowerUp"
                typeIconPath: "" // Optional: Set path like "UI/Icons/PowerIcon"
            );

            // Refresh the Asset Database to show the new files
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Sample CardData assets created successfully!");
        }

        private static void CreateCardAsset(string cardName, string description, int manaCost, CardType cardType, int baseValue, EffectType effectType, int effectValue, TargetType targetType, string artworkPath = "", string typeIconPath = "")
        {
            // Create an instance of CardData
            CardData cardData = ScriptableObject.CreateInstance<CardData>();

            // Populate the fields
            cardData.cardName = cardName;
            cardData.description = description;
            cardData.manaCost = manaCost;
            cardData.cardType = cardType;
            cardData.baseValue = baseValue;
            cardData.cardArtworkPath = artworkPath; // Assign optional paths
            cardData.typeIconPath = typeIconPath;  // Assign optional paths

            // Create the CardEffect
            CardEffect effect = new CardEffect
            {
                effectType = effectType,
                effectValue = effectValue,
                targetType = targetType
                // Add other effect properties if needed (delay, condition, etc.)
            };
            cardData.cardEffects = new CardEffect[] { effect }; // Assign the effect

            // Define the full path for the new asset file
            string assetPath = Path.Combine(CardSavePath, $"{cardName}.asset");
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath); // Avoid overwriting

            // Create the asset file
            AssetDatabase.CreateAsset(cardData, assetPath);
            Debug.Log($"Created CardData asset at: {assetPath}");
        }
    }
} 