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
        public int baseValue;
        public string cardArtworkPath;
        public string typeIconPath;

        [Header("Effects")]
        public CardEffect[] cardEffects;
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
        ApplyBuff,
        ApplyDebuff
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