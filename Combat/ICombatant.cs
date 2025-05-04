using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Interface for entities that can participate in combat
    /// </summary>
    public interface ICombatant
    {
        #region Combat Status
        /// <summary>
        /// Whether this combatant is an enemy (from the player's perspective)
        /// </summary>
        bool IsEnemy();
        
        /// <summary>
        /// Whether this combatant is defeated
        /// </summary>
        bool IsDefeated();
        #endregion
        
        #region Combat Actions
        /// <summary>
        /// Take damage from an attack or effect
        /// </summary>
        /// <param name="amount">Amount of damage to take</param>
        void TakeDamage(int amount);
        
        /// <summary>
        /// Add block/defense against incoming damage
        /// </summary>
        /// <param name="amount">Amount of block to add</param>
        void AddBlock(int amount);
        
        /// <summary>
        /// Heal the combatant
        /// </summary>
        /// <param name="amount">Amount of health to restore</param>
        void Heal(int amount);
        
        /// <summary>
        /// Draw cards into the combatant's hand
        /// </summary>
        /// <param name="amount">Number of cards to draw</param>
        void DrawCards(int amount);
        
        /// <summary>
        /// Apply a positive status effect to the combatant
        /// </summary>
        /// <param name="buffId">ID of the buff to apply</param>
        void ApplyBuff(int buffId);
        
        /// <summary>
        /// Apply a negative status effect to the combatant
        /// </summary>
        /// <param name="debuffId">ID of the debuff to apply</param>
        void ApplyDebuff(int debuffId);
        #endregion
    }
} 