using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Combat
{
    public static class CardEffectProcessor
    {
        // Dictionary to track cards played for effects that scale with similar cards
        private static Dictionary<string, int> cardsPlayedByName = new Dictionary<string, int>();
        
        // Track turn count for turn-scaling effects
        private static int currentTurnCount = 0;
        
        // List of active status effects
        private static Dictionary<ICombatant, List<StatusEffect>> activeStatusEffects = 
            new Dictionary<ICombatant, List<StatusEffect>>();
        
        // Track damage done by cards for cards that grow stronger with use
        private static Dictionary<string, int> cardUsageTracker = new Dictionary<string, int>();
        
        /// <summary>
        /// Reset all trackers at the start of combat
        /// </summary>
        public static void ResetAllTrackers()
        {
            cardsPlayedByName.Clear();
            currentTurnCount = 0;
            activeStatusEffects.Clear();
            cardUsageTracker.Clear();
        }
        
        /// <summary>
        /// Called when a new turn starts
        /// </summary>
        public static void IncrementTurnCount()
        {
            currentTurnCount++;
            ProcessStatusEffectsForNewTurn();
        }
        
        /// <summary>
        /// Apply all effects of a card to a target
        /// </summary>
        public static void ApplyCardEffects(CardData cardData, ICombatant source, ICombatant target)
        {
            if (cardData == null || source == null || target == null)
            {
                Debug.LogError("Invalid parameters for ApplyCardEffects");
                return;
            }
            
            // Track card usage for various effects
            TrackCardUsage(cardData.cardName);
            
            // Apply each effect in the card
            foreach (CardEffect effect in cardData.cardEffects)
            {
                // Skip effects that don't match the target type
                if (!IsValidTarget(effect.targetType, source, target))
                {
                    continue;
                }
                
                // Calculate final effect value based on modifiers
                int finalValue = CalculateEffectValue(effect, cardData, source, target);
                
                // Apply the effect
                ApplySingleEffect(effect.effectType, finalValue, source, target, cardData);
            }
        }
        
        /// <summary>
        /// Apply a single effect to a target
        /// </summary>
        private static void ApplySingleEffect(EffectType effectType, int value, ICombatant source, ICombatant target, CardData cardData)
        {
            switch (effectType)
            {
                case EffectType.Damage:
                    ApplyDamage(value, source, target);
                    break;
                    
                case EffectType.Block:
                    target.AddBlock(value);
                    break;
                    
                case EffectType.Heal:
                    target.Heal(value);
                    break;
                    
                case EffectType.DrawCard:
                    target.DrawCards(value);
                    break;
                    
                case EffectType.AddEnergy:
                    AddEnergy(value, target);
                    break;
                    
                case EffectType.ApplyBreak:
                    ApplyStatusEffect(target, StatusEffectType.Break, value);
                    break;
                    
                case EffectType.ApplyWeak:
                    ApplyStatusEffect(target, StatusEffectType.Weak, value);
                    break;
                    
                case EffectType.ApplyDoT:
                    ApplyStatusEffect(target, StatusEffectType.DoT, value);
                    break;
                    
                case EffectType.ApplyCritical:
                    ApplyStatusEffect(target, StatusEffectType.Critical, value);
                    break;
                    
                case EffectType.ApplyThorns:
                    ApplyStatusEffect(target, StatusEffectType.Thorns, value);
                    break;
                    
                default:
                    Debug.LogWarning($"Effect type {effectType} not implemented");
                    break;
            }
        }
        
        /// <summary>
        /// Calculate the final effect value based on various modifiers
        /// </summary>
        private static int CalculateEffectValue(CardEffect effect, CardData cardData, ICombatant source, ICombatant target)
        {
            float baseValue = effect.effectValue;
            float multiplier = 1.0f;
            
            // Check if card gets stronger with each use
            if (cardData.hasScalingWithUse && effect.effectType == EffectType.Damage)
            {
                int usageCount = GetCardUsageCount(cardData.cardName);
                multiplier += 0.2f * usageCount; // 20% increase per use
            }
            
            // Check if card scales with turn count
            if (cardData.hasScalingWithTurns)
            {
                multiplier += 0.1f * currentTurnCount; // 10% increase per turn
            }
            
            // Check if card is stronger at low health
            if (cardData.hasScalingWithLowHealth && source is CombatPet sourcePet)
            {
                float healthPercentage = (float)sourcePet.CurrentHealth / sourcePet.MaxHealth;
                if (healthPercentage < 0.5f) // Below 50% health
                {
                    multiplier += 0.5f; // 50% boost when below half health
                }
            }
            
            // Check for similar cards scaling
            if (cardData.hasScalingWithSimilarCards)
            {
                string baseName = GetBaseCardName(cardData.cardName);
                int similarCards = CountSimilarCards(baseName);
                multiplier += 0.15f * similarCards; // 15% increase per similar card
            }
            
            // Check if target has Break status (increases damage taken)
            if (effect.effectType == EffectType.Damage && HasStatusEffect(target, StatusEffectType.Break))
            {
                multiplier += 0.5f; // 50% more damage to broken targets
            }
            
            // Check if source has Weak status (reduces damage dealt)
            if (effect.effectType == EffectType.Damage && HasStatusEffect(source, StatusEffectType.Weak))
            {
                multiplier -= 0.25f; // 25% less damage when weakened
            }
            
            // Check if source has Critical status (chance for extra damage)
            if (effect.effectType == EffectType.Damage && HasStatusEffect(source, StatusEffectType.Critical))
            {
                // 25% chance for critical hit
                if (Random.value < 0.25f)
                {
                    multiplier += 1.0f; // Double damage on crit
                }
            }
            
            // Calculate final value
            int finalValue = Mathf.RoundToInt(baseValue * multiplier);
            return Mathf.Max(finalValue, 1); // Minimum value of 1
        }
        
        /// <summary>
        /// Apply damage to a target, accounting for thorns effect
        /// </summary>
        private static void ApplyDamage(int damage, ICombatant source, ICombatant target)
        {
            // Apply damage to target
            target.TakeDamage(damage);
            
            // Check for thorns effect on target
            if (HasStatusEffect(target, StatusEffectType.Thorns))
            {
                int thornsValue = GetStatusEffectValue(target, StatusEffectType.Thorns);
                source.TakeDamage(thornsValue);
            }
        }
        
        /// <summary>
        /// Add energy to a target (only works for CombatPlayer)
        /// </summary>
        private static void AddEnergy(int amount, ICombatant target)
        {
            if (target is CombatPlayer player)
            {
                player.SyncEnergy.Value += amount;
            }
        }
        
        /// <summary>
        /// Apply a status effect to a target
        /// </summary>
        private static void ApplyStatusEffect(ICombatant target, StatusEffectType effectType, int value)
        {
            // Initialize list if needed
            if (!activeStatusEffects.ContainsKey(target))
            {
                activeStatusEffects[target] = new List<StatusEffect>();
            }
            
            // Check if effect already exists
            StatusEffect existingEffect = activeStatusEffects[target]
                .FirstOrDefault(e => e.Type == effectType);
                
            if (existingEffect != null)
            {
                // Update existing effect
                existingEffect.Value += value;
                existingEffect.Duration = Mathf.Max(existingEffect.Duration, 2); // Reset duration
            }
            else
            {
                // Add new effect
                activeStatusEffects[target].Add(new StatusEffect 
                {
                    Type = effectType,
                    Value = value,
                    Duration = 2 // Default duration of 2 turns
                });
            }
            
            // Visual feedback
            if (target is MonoBehaviour targetMono)
            {
                Debug.Log($"Applied {effectType} ({value}) to {targetMono.name}");
            }
        }
        
        /// <summary>
        /// Process status effects at the start of a new turn
        /// </summary>
        private static void ProcessStatusEffectsForNewTurn()
        {
            foreach (var kvp in activeStatusEffects.ToList())
            {
                ICombatant target = kvp.Key;
                List<StatusEffect> effects = kvp.Value;
                
                // Apply DoT effects
                StatusEffect dotEffect = effects.FirstOrDefault(e => e.Type == StatusEffectType.DoT);
                if (dotEffect != null)
                {
                    target.TakeDamage(dotEffect.Value);
                }
                
                // Reduce duration of all effects
                for (int i = effects.Count - 1; i >= 0; i--)
                {
                    effects[i].Duration--;
                    if (effects[i].Duration <= 0)
                    {
                        effects.RemoveAt(i);
                    }
                }
                
                // Remove empty lists
                if (effects.Count == 0)
                {
                    activeStatusEffects.Remove(target);
                }
            }
        }
        
        /// <summary>
        /// Check if a target has a specific status effect
        /// </summary>
        private static bool HasStatusEffect(ICombatant target, StatusEffectType effectType)
        {
            if (!activeStatusEffects.ContainsKey(target))
            {
                return false;
            }
            
            return activeStatusEffects[target].Any(e => e.Type == effectType);
        }
        
        /// <summary>
        /// Get the value of a status effect on a target
        /// </summary>
        private static int GetStatusEffectValue(ICombatant target, StatusEffectType effectType)
        {
            if (!activeStatusEffects.ContainsKey(target))
            {
                return 0;
            }
            
            StatusEffect effect = activeStatusEffects[target]
                .FirstOrDefault(e => e.Type == effectType);
                
            return effect != null ? effect.Value : 0;
        }
        
        /// <summary>
        /// Track a card being played
        /// </summary>
        private static void TrackCardUsage(string cardName)
        {
            // Track cards played by name
            if (cardsPlayedByName.ContainsKey(cardName))
            {
                cardsPlayedByName[cardName]++;
            }
            else
            {
                cardsPlayedByName[cardName] = 1;
            }
            
            // Track card usage for scaling effects
            string baseCardName = GetBaseCardName(cardName);
            if (cardUsageTracker.ContainsKey(baseCardName))
            {
                cardUsageTracker[baseCardName]++;
            }
            else
            {
                cardUsageTracker[baseCardName] = 1;
            }
        }
        
        /// <summary>
        /// Get number of times a card has been used
        /// </summary>
        private static int GetCardUsageCount(string cardName)
        {
            string baseCardName = GetBaseCardName(cardName);
            return cardUsageTracker.ContainsKey(baseCardName) ? cardUsageTracker[baseCardName] : 0;
        }
        
        /// <summary>
        /// Count the number of similar cards played
        /// </summary>
        private static int CountSimilarCards(string baseCardName)
        {
            int count = 0;
            foreach (var cardName in cardsPlayedByName.Keys)
            {
                if (GetBaseCardName(cardName) == baseCardName)
                {
                    count += cardsPlayedByName[cardName];
                }
            }
            return count;
        }
        
        /// <summary>
        /// Get the base name of a card (without "+", etc.)
        /// </summary>
        private static string GetBaseCardName(string cardName)
        {
            // Remove any "+" or upgrader suffixes
            return cardName.Split('+')[0].Trim();
        }
        
        /// <summary>
        /// Check if a target is valid for an effect
        /// </summary>
        private static bool IsValidTarget(TargetType targetType, ICombatant source, ICombatant target)
        {
            switch (targetType)
            {
                case TargetType.Self:
                    return source == target;
                    
                case TargetType.SingleEnemy:
                    return IsEnemy(source, target);
                    
                case TargetType.AllEnemies:
                    return IsEnemy(source, target);
                    
                case TargetType.SingleAlly:
                    return !IsEnemy(source, target) && source != target;
                    
                case TargetType.AllAllies:
                    return !IsEnemy(source, target) && source != target;
                    
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Check if two combatants are enemies
        /// </summary>
        private static bool IsEnemy(ICombatant source, ICombatant target)
        {
            if (source == target) return false;
            
            // If one is a player and one is a pet, check ownership
            if (source is CombatPlayer sourcePlayer && target is CombatPet targetPet)
            {
                return targetPet != sourcePlayer.NetworkPlayer.CombatPet;
            }
            else if (source is CombatPet sourcePet && target is CombatPlayer targetPlayer)
            {
                return sourcePet != targetPlayer.NetworkPlayer.CombatPet;
            }
            
            // If both same type, they're enemies
            return true;
        }
    }
    
    /// <summary>
    /// Represents an active status effect on a combatant
    /// </summary>
    public class StatusEffect
    {
        public StatusEffectType Type { get; set; }
        public int Value { get; set; }
        public int Duration { get; set; }
    }
    
    /// <summary>
    /// Types of status effects
    /// </summary>
    public enum StatusEffectType
    {
        Break,      // Take more damage
        Weak,       // Deal less damage
        DoT,        // Damage over time
        Critical,   // Chance for critical hits
        Thorns      // Return damage when attacked
    }
} 