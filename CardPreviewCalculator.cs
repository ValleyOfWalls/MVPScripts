using UnityEngine;
using TMPro;
using System.Text.RegularExpressions;

/// <summary>
/// Class responsible for calculating and updating card description previews
/// when cards are hovered over targets.
/// </summary>
public class CardPreviewCalculator
{
    private static readonly Color previewColor = new Color(0.2f, 0.6f, 1f); // Light blue color
    private static readonly string originalDescKey = "OriginalDescription";

    private GameManager gameManager;
    private CombatCalculator combatCalculator;

    public CardPreviewCalculator(GameManager gameManager)
    {
        this.gameManager = gameManager;
        this.combatCalculator = gameManager.GetCombatCalculator();
    }

    /// <summary>
    /// Updates a card's description with the calculated damage preview when hovering over a target.
    /// </summary>
    /// <param name="cardGO">The card GameObject containing the description text</param>
    /// <param name="card">The CardData for the card</param>
    /// <param name="targetType">The target being hovered over</param>
    /// <returns>True if preview was applied, false otherwise</returns>
    public bool UpdateCardPreviewForTarget(GameObject cardGO, CardData card, CardDropZone.TargetType targetType)
    {
        if (cardGO == null || card == null) return false;

        // Find the description text component
        Transform descPanel = cardGO.transform.Find("DescPanel");
        TextMeshProUGUI descText = descPanel?.Find("CardDescText")?.GetComponent<TextMeshProUGUI>();
        if (descText == null) return false;

        // Store original description if not already stored
        if (!cardGO.TryGetComponent<CardPreviewState>(out var previewState))
        {
            previewState = cardGO.AddComponent<CardPreviewState>();
            previewState.originalDescription = descText.text;
        }

        // If card does damage, calculate the new damage amount
        if (DoesDealDamage(card))
        {
            int baseDamage = ExtractBaseDamageValue(card.description);
            if (baseDamage <= 0) return false; // No damage value found

            int calculatedDamage = CalculateDamagePreview(baseDamage, card, targetType);
            
            // Only update if the calculated damage is different from base damage
            if (calculatedDamage != baseDamage)
            {
                // Replace the damage value with colorized calculated value
                string newDescription = ReplaceDamageValue(
                    previewState.originalDescription, 
                    baseDamage.ToString(), 
                    $"<color=#{ColorUtility.ToHtmlStringRGB(previewColor)}>{calculatedDamage}</color>"
                );

                descText.text = newDescription;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Restores the card's original description.
    /// </summary>
    /// <param name="cardGO">The card GameObject containing the description text</param>
    /// <returns>True if description was restored, false otherwise</returns>
    public bool RestoreOriginalDescription(GameObject cardGO)
    {
        if (cardGO == null) return false;

        // Find the description text component
        Transform descPanel = cardGO.transform.Find("DescPanel");
        TextMeshProUGUI descText = descPanel?.Find("CardDescText")?.GetComponent<TextMeshProUGUI>();
        if (descText == null) return false;

        // Restore original description if available
        if (cardGO.TryGetComponent<CardPreviewState>(out var previewState))
        {
            descText.text = previewState.originalDescription;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Calculates the preview damage for a card against a specific target.
    /// </summary>
    private int CalculateDamagePreview(int baseDamage, CardData card, CardDropZone.TargetType targetType)
    {
        // Get managers
        PlayerManager playerManager = gameManager.GetPlayerManager();
        StatusEffectManager statusManager = playerManager.GetStatusEffectManager();
        HealthManager healthManager = gameManager.GetPlayerManager().GetHealthManager();

        // Gather all relevant status effects
        bool isPlayerWeak = statusManager.IsPlayerWeak();
        int playerStrength = playerManager.GetPlayerStrength();
        int playerCritChance = playerManager.GetPlayerEffectiveCritChance();
        
        int targetBlock = 0;
        bool isTargetBroken = false;

        // Set target-specific values
        switch (targetType)
        {
            case CardDropZone.TargetType.PlayerSelf:
                // (For cards a player would use on themselves - weird edge case)
                targetBlock = playerManager.GetLocalPlayerBlock();
                isTargetBroken = statusManager.IsPlayerBroken();
                Debug.Log($"Preview - PlayerSelf target: Block={targetBlock}, IsBroken={isTargetBroken}");
                break;

            case CardDropZone.TargetType.EnemyPet:
                targetBlock = playerManager.GetOpponentPetBlock();
                isTargetBroken = statusManager.IsOpponentPetBroken();
                Debug.Log($"Preview - EnemyPet target: Block={targetBlock}, IsBroken={isTargetBroken}");
                break;

            case CardDropZone.TargetType.OwnPet:
                targetBlock = playerManager.GetLocalPetBlock();
                isTargetBroken = statusManager.IsLocalPetBroken();
                Debug.Log($"Preview - OwnPet target: Block={targetBlock}, IsBroken={isTargetBroken}");
                break;
        }

        // Debug the status effects being applied to calculation
        Debug.Log($"Preview damage calculation inputs: BaseDmg={baseDamage}, Strength={playerStrength}, " +
                 $"IsWeak={isPlayerWeak}, TargetBroken={isTargetBroken}, CritChance={playerCritChance}");

        // Calculate result using the same formula as in combat
        CombatCalculator.DamageResult result = combatCalculator.CalculateDamage(
            baseDamage,       // Raw base damage from card
            playerStrength,   // Attacker strength
            0,                // Target block (setting to 0 for preview calculation)
            isPlayerWeak,     // Attacker is weak
            isTargetBroken,   // Target is broken
            playerCritChance  // Attacker crit chance
        );

        // Log the results of the calculation
        Debug.Log($"Preview damage results: DmgBeforeBlock={result.DamageBeforeBlock}, " +
                 $"DamageAfterBlock={result.DamageAfterBlock}, " +
                 $"IsCritical={result.IsCritical}");

        // For preview, use damageAfterBlock with 0 block, which will correctly
        // account for break status and weakness in the same order as actual combat
        return result.DamageAfterBlock;
    }

    /// <summary>
    /// Determines if a card deals damage based on its description or effects.
    /// </summary>
    private bool DoesDealDamage(CardData card)
    {
        // Check if description contains damage numbers
        if (Regex.IsMatch(card.description, @"Deal\s+\d+\s+damage"))
            return true;

        // Remove effect type checking until we have the proper structure
        // We'll rely on the description pattern match only
        /*
        foreach (var effect in card.effects)
        {
            if (effect.effectType == EffectType.Damage ||
                effect.effectType == EffectType.DamageMulti)
                return true;
        }
        */
        
        return false;
    }

    /// <summary>
    /// Extracts the base damage value from a card description.
    /// </summary>
    private int ExtractBaseDamageValue(string description)
    {
        // Try to match "Deal X damage" pattern
        Match match = Regex.Match(description, @"Deal\s+(\d+)\s+damage");
        if (match.Success && match.Groups.Count > 1)
        {
            if (int.TryParse(match.Groups[1].Value, out int damage))
            {
                return damage;
            }
        }

        return 0; // Default if no value found
    }

    /// <summary>
    /// Replaces a damage value in the description with a colorized preview value.
    /// </summary>
    private string ReplaceDamageValue(string description, string oldValue, string newValue)
    {
        return Regex.Replace(
            description,
            $"Deal\\s+{oldValue}\\s+damage",
            $"Deal {newValue} damage", 
            RegexOptions.IgnoreCase
        );
    }
}

/// <summary>
/// Simple component to store the original card description state.
/// </summary>
public class CardPreviewState : MonoBehaviour
{
    public string originalDescription;
} 