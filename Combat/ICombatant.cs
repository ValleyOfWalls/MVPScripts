using UnityEngine;

namespace Combat
{
    public interface ICombatant
    {
        void TakeDamage(int amount);
        void AddBlock(int amount);
        void Heal(int amount);
        void DrawCards(int amount);
        void ApplyBuff(int buffId);
        void ApplyDebuff(int debuffId);
        bool IsEnemy();
    }
} 