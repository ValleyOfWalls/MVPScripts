using UnityEngine;
// using TMPro; // Likely not needed here if CardEffectType is defined elsewhere

// Ensure CardEffectType enum is defined, e.g., in its own file or a shared namespace.
// Example:
/*
public enum CardEffectType 
{ 
    Damage, 
    Heal, 
    DrawCard, 
    BuffStats, 
    DebuffStats, 
    ApplyStatus 
}
*/

// Define card target types
public enum CardTargetType
{
    Self,       // Target the caster (player or pet)
    Opponent,   // Target the opponent (enemy player or pet)
    Ally,       // Target your ally (player targets pet, pet targets player)
    Random      // Target is chosen randomly
}

[CreateAssetMenu(fileName = "New CardData", menuName = "Card Game/Card Data")]
public class CardData : ScriptableObject
{
    [Header("Card Identification")]
    [SerializeField] private int _cardId = 0; // Add serialized ID field for consistent IDs across clients
    [SerializeField] private string _cardName = "New Card";
    [TextArea(3, 5)]
    [SerializeField] private string _description = "Card Description";

    [Header("Visuals")]
    [SerializeField] private Sprite _cardArtwork;

    [Header("Gameplay")]
    [SerializeField] private CardEffectType _effectType = CardEffectType.Damage; // Make sure CardEffectType is defined
    [SerializeField] private CardTargetType _targetType = CardTargetType.Opponent; // Default to targeting opponent
    [SerializeField] private int _amount = 1; // Potency of the effect (e.g., damage amount, cards to draw)
    [SerializeField] private int _energyCost = 1;

    // Public accessors
    public int CardId => _cardId; // Use the serialized ID instead of GetInstanceID()
    public string CardName => _cardName;
    public string Description => _description;
    public Sprite CardArtwork => _cardArtwork;
    public CardEffectType EffectType => _effectType;
    public CardTargetType TargetType => _targetType;
    public int Amount => _amount;
    public int EnergyCost => _energyCost;
} 