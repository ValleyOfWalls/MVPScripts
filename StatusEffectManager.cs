using UnityEngine;
using System.Collections.Generic;

// This is a simple placeholder for the StatusEffectManager class
// It will be expanded in the future for gameplay
public class StatusEffectManager
{
    // Dictionaries to store active status effects
    private Dictionary<StatusEffectType, int> playerEffects = new Dictionary<StatusEffectType, int>();
    private Dictionary<StatusEffectType, int> petEffects = new Dictionary<StatusEffectType, int>();
    private Dictionary<StatusEffectType, int> enemyEffects = new Dictionary<StatusEffectType, int>();
    private Dictionary<StatusEffectType, int> enemyPetEffects = new Dictionary<StatusEffectType, int>();
    
    public StatusEffectManager()
    {
        // Initialize dictionaries with default values
        foreach (StatusEffectType type in System.Enum.GetValues(typeof(StatusEffectType)))
        {
            playerEffects[type] = 0;
            petEffects[type] = 0;
            enemyEffects[type] = 0;
            enemyPetEffects[type] = 0;
        }
    }
    
    // Methods to reset effects
    public void ResetAllPlayerEffects()
    {
        foreach (StatusEffectType type in System.Enum.GetValues(typeof(StatusEffectType)))
        {
            playerEffects[type] = 0;
        }
        
        Debug.Log("Reset all player status effects");
    }
    
    public void ResetAllLocalPetEffects()
    {
        foreach (StatusEffectType type in System.Enum.GetValues(typeof(StatusEffectType)))
        {
            petEffects[type] = 0;
        }
        
        Debug.Log("Reset all local pet status effects");
    }
}

// Status effect types
public enum StatusEffectType
{
    Strength,
    Dexterity,
    Block,
    Vulnerable,
    Weak,
    Poison,
    Burn,
    Stun
} 