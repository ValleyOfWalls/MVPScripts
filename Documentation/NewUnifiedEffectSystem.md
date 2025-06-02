# New Unified Card Effect System

## 🎯 Overview

The card system has been redesigned to eliminate redundancy and confusion. **All card effects now live in a single, unified list** instead of being scattered across multiple systems.

## ❌ What We Fixed

### 1. **Main Effect + Additional Effects Redundancy**
**Before:** Confusing dual system
```csharp
[SerializeField] private CardEffectType _effectType;  // Main effect
[SerializeField] private int _amount;
[SerializeField] private List<MultiEffect> _additionalEffects;  // More effects
```

**After:** Unified system
```csharp
[SerializeField] private List<CardEffect> _effects;  // ALL effects in one place!
```

### 2. **"Condition Met" Field Confusion**
**Before:** Had an editable `conditionMet` field that made no sense
```csharp
public bool conditionMet;  // Why is this in the designer?!
```

**After:** Computed at runtime where it belongs
```csharp
// No conditionMet field - computed dynamically in CheckCondition()
```

### 3. **Scaling Nested in Conditionals**
**Before:** Scaling buried inside conditional effects
```csharp
public class ConditionalEffect {
    public bool useScaling;           // Confusing!
    public ScalingEffect scalingEffect;  // Why here?
}
```

**After:** Each effect has optional scaling directly
```csharp
public class CardEffect {
    public ScalingType scalingType = ScalingType.None;  // Clear and optional
}
```

## 🧩 New CardEffect Structure

Every effect is now self-contained with everything it needs:

```csharp
public class CardEffect
{
    // === BASIC EFFECT ===
    public CardEffectType effectType = CardEffectType.Damage;
    public int amount = 3;
    public int duration = 3;  // Only shows for status effects
    public CardTargetType targetType = CardTargetType.Opponent;
    public ElementalType elementalType = ElementalType.None;
    
    // === OPTIONAL CONDITION ===
    public ConditionalType conditionType = ConditionalType.None;
    public int conditionValue = 0;  // Only shows if condition set
    
    // === OPTIONAL ALTERNATIVE ===
    public bool hasAlternativeEffect = false;  // Only shows if condition set
    public CardEffectType alternativeEffectType;  // Only shows if alternative enabled
    public int alternativeEffectAmount;
    
    // === OPTIONAL SCALING ===
    public ScalingType scalingType = ScalingType.None;
    public float scalingMultiplier = 1.0f;  // Only shows if scaling set
    public int maxScaling = 10;
}
```

## 📝 Usage Examples

### Simple Damage Card
```csharp
card.AddEffect(CardEffectType.Damage, 4, CardTargetType.Opponent);
```

### Multi-Effect Card (Damage + Heal)
```csharp
card.AddEffect(CardEffectType.Damage, 4, CardTargetType.Opponent);
card.AddEffect(CardEffectType.Heal, 2, CardTargetType.Self);
```

### Conditional Effect (Heal more if low health)
```csharp
var effect = new CardEffect
{
    effectType = CardEffectType.Heal,
    amount = 3,
    targetType = CardTargetType.Self,
    conditionType = ConditionalType.IfSourceHealthBelow,
    conditionValue = 50,
    hasAlternativeEffect = true,
    alternativeEffectType = CardEffectType.Heal,
    alternativeEffectAmount = 6  // Heal 6 instead of 3 if below 50% health
};
```

### Scaling Effect (Damage grows with combo)
```csharp
var effect = new CardEffect
{
    effectType = CardEffectType.Damage,
    amount = 2,  // Base damage
    targetType = CardTargetType.Opponent,
    scalingType = ScalingType.ComboCount,
    scalingMultiplier = 1.5f,  // +1.5 damage per combo
    maxScaling = 10  // Cap at 10 damage
};
```

### Complex Effect (Conditional + Scaling)
```csharp
var effect = new CardEffect
{
    effectType = CardEffectType.Damage,
    amount = 1,
    targetType = CardTargetType.AllEnemies,
    conditionType = ConditionalType.IfComboCount,
    conditionValue = 3,  // Only if combo >= 3
    scalingType = ScalingType.ComboCount,
    scalingMultiplier = 2.0f,  // +2 damage per combo when triggered
    maxScaling = 15
};
```

## 🎨 Inspector Benefits

### Smart Field Visibility
- **Duration** only shows for status effects
- **Condition fields** only show when condition type is set
- **Alternative fields** only show when alternative is enabled  
- **Scaling fields** only show when scaling type is set

### No More Manual Sizing
- Property drawers use Unity's automatic sizing
- No more manual height calculations
- Clean, professional appearance

## 🔄 Backward Compatibility

All legacy properties still work through conversion methods:

```csharp
// Legacy properties automatically convert from new system
public CardEffectType EffectType => HasEffects ? _effects[0].effectType : CardEffectType.Damage;
public int Amount => HasEffects ? _effects[0].amount : 0;
public bool HasConditionalEffect => _effects.Any(e => e.conditionType != ConditionalType.None);
```

## 🚀 Key Benefits

1. **📋 Single Source of Truth** - All effects in one list
2. **🧠 Brain-Friendly** - Intuitive flow: Effect → Condition → Scaling  
3. **🎯 No Redundancy** - Each concept exists in exactly one place
4. **⚡ Better Performance** - No duplicate state management
5. **🔧 Easy to Extend** - Adding new features is straightforward
6. **🎨 Clean UI** - Conditional fields hide complexity
7. **🔄 Backward Compatible** - Existing code continues to work

## 🛠️ Migration Guide

### For Existing Cards
- No migration needed! Legacy properties provide seamless compatibility
- Cards continue to work exactly as before

### For New Development  
- Use `card.AddEffect()` for simple effects
- Create `CardEffect` objects directly for complex effects
- Combine multiple effects in the `_effects` list

### For Advanced Use Cases
- Conditional effects: Set `conditionType` and `conditionValue`
- Scaling effects: Set `scalingType` and `scalingMultiplier`
- Alternative effects: Enable `hasAlternativeEffect` and configure alternatives

The system evolved from a confusing multi-level hierarchy to a clean, intuitive design where every effect is self-contained! 

## 📝 Enhanced Conditional Effects

### How Conditionals Work

Conditional effects check game state and modify behavior based on the result. Each conditional has:

1. **Condition Type**: What to check (e.g., `IfSourceHealthAbove`)
2. **Condition Value**: The threshold to compare against (e.g., `50` for "above 50 health")
3. **Main Effect**: The primary effect to execute
4. **Alternative Effect** (optional): What to do if condition fails or succeeds

### Alternative Effect Logic

The new `AlternativeEffectLogic` enum controls how alternative effects interact with main effects:

#### Replace Logic (OR behavior)
- **If condition is met**: Execute main effect ONLY
- **If condition fails**: Execute alternative effect INSTEAD of main effect
- **Use case**: Either/or scenarios

```csharp
// Example: "Deal 8 damage if combo >= 3, otherwise draw 1 card"
effect.alternativeLogic = AlternativeEffectLogic.Replace;
effect.conditionType = ConditionalType.IfComboCount;
effect.conditionValue = 3;
effect.effectType = CardEffectType.Damage;
effect.amount = 8;
effect.alternativeEffectType = CardEffectType.DrawCard;
effect.alternativeEffectAmount = 1;
```

#### Additional Logic (AND behavior)
- **If condition is met**: Execute main effect AND alternative effect
- **If condition fails**: Execute main effect AND alternative effect
- **Use case**: Bonus effects, always-execute-with-conditional-bonus

```csharp
// Example: "Heal 3, heal 5 more if below 25% health"
effect.alternativeLogic = AlternativeEffectLogic.Additional;
effect.conditionType = ConditionalType.IfSourceHealthBelow;
effect.conditionValue = 25;
effect.effectType = CardEffectType.Heal;
effect.amount = 3;
effect.alternativeEffectType = CardEffectType.Heal;
effect.alternativeEffectAmount = 5;
// Total healing: 3 if above 25% health, 8 if below 25% health
```

### Available Conditions and Their Values

| Condition Type | conditionValue Meaning | Example |
|---|---|---|
| `IfSourceHealthAbove` | Health threshold | `50` = if source health > 50 |
| `IfSourceHealthBelow` | Health threshold | `25` = if source health < 25 |
| `IfTargetHealthAbove` | Health threshold | `75` = if target health > 75 |
| `IfTargetHealthBelow` | Health threshold | `30` = if target health < 30 |
| `IfCardsInHand` | Minimum cards | `3` = if 3 or more cards in hand |
| `IfEnergyRemaining` | Minimum energy | `2` = if 2 or more energy remaining |
| `IfComboCount` | Minimum combo | `5` = if combo count >= 5 |
| `IfPerfectionStreak` | Minimum streak | `3` = if perfect streak >= 3 |

### Usage Examples

### Simple Damage Card
```csharp
card.AddEffect(CardEffectType.Damage, 4, CardTargetType.Opponent);
```

### Multi-Effect Card (Damage + Heal)
```csharp
card.AddEffect(CardEffectType.Damage, 4, CardTargetType.Opponent);
card.AddEffect(CardEffectType.Heal, 2, CardTargetType.Self);
```

### Conditional Effect - OR Logic (Execute finishing move OR basic attack)
```csharp
var effect = new CardEffect
{
    effectType = CardEffectType.Damage,
    amount = 12,  // Big damage if condition met
    targetType = CardTargetType.Opponent,
    conditionType = ConditionalType.IfTargetHealthBelow,
    conditionValue = 30,  // If target below 30% health
    hasAlternativeEffect = true,
    alternativeLogic = AlternativeEffectLogic.Replace,  // OR logic
    alternativeEffectType = CardEffectType.Damage,
    alternativeEffectAmount = 4  // Basic damage if condition not met
};
// Result: Deal 12 damage to low-health enemies, 4 damage otherwise
```

### Conditional Effect - AND Logic (Always heal with bonus)
```csharp
var effect = new CardEffect
{
    effectType = CardEffectType.Heal,
    amount = 3,  // Base healing
    targetType = CardTargetType.Self,
    conditionType = ConditionalType.IfSourceHealthBelow,
    conditionValue = 50,  // If below 50% health
    hasAlternativeEffect = true,
    alternativeLogic = AlternativeEffectLogic.Additional,  // AND logic
    alternativeEffectType = CardEffectType.Heal,
    alternativeEffectAmount = 6  // Bonus healing
};
// Result: Always heal 3, heal 9 total if below 50% health
```

### Helper Methods for Easy Setup

```csharp
// OR Logic (either/or)
cardData.AddConditionalEffectOR(
    CardEffectType.Damage, 8, CardTargetType.Opponent,    // Main effect
    ConditionalType.IfComboCount, 3,                      // Condition
    CardEffectType.DrawCard, 1);                          // Alternative

// AND Logic (bonus effect)
cardData.AddConditionalEffectAND(
    CardEffectType.Heal, 3, CardTargetType.Self,          // Main effect (always)
    ConditionalType.IfSourceHealthBelow, 25,              // Condition
    CardEffectType.Heal, 5);                              // Bonus effect
```

### Scaling Effect (Damage grows with combo)
```csharp
var effect = new CardEffect
{
    effectType = CardEffectType.Damage,
    amount = 2,  // Base damage
    targetType = CardTargetType.Opponent,
    scalingType = ScalingType.ComboCount,
    scalingMultiplier = 1.5f,  // +1.5 damage per combo
    maxScaling = 10  // Cap at 10 damage
};
``` 