using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(fileName = "NewCard", menuName = "Cards/Card Data")]
    public class CardData : ScriptableObject
    {
        [Header("Card Data")]
        public string cardName;
        public string description;
        public int manaCost;
        public CardType cardType;
        public string cardArtworkPath;
        public string typeIconPath;

        [Header("Effects")]
        public CardEffect[] cardEffects;
        
        [Header("Scaling Properties")]
        public bool hasScalingWithUse = false;          // Gets stronger with each use
        public bool hasScalingWithTurns = false;        // Gets stronger as turns pass
        public bool hasScalingWithLowHealth = false;    // Gets stronger when health is low
        public bool hasScalingWithSimilarCards = false; // Gets stronger based on similar cards
    }

    [System.Serializable]
    public class CardEffect
    {
        public EffectType effectType;
        public int effectValue;
        public TargetType targetType;
    }

    public enum EffectType
    {
        Damage,
        Block,
        Heal,
        DrawCard,
        AddEnergy,       // New: Add energy to a player
        ApplyBreak,      // New: Target takes more damage
        ApplyWeak,       // New: Target deals less damage
        ApplyDoT,        // New: Target takes damage over time
        ApplyCritical,   // New: Gives chance for critical hits
        ApplyThorns,     // New: Damage attacker when hit
        ApplyBuff,       // Still support generic buffs
        ApplyDebuff      // Still support generic debuffs
    }

    public enum TargetType
    {
        Self,
        SingleEnemy,
        AllEnemies,
        SingleAlly,
        AllAllies
    }
} 