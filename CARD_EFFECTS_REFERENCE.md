# Card Effects Reference

This document provides a comprehensive overview of all card effects supported by the CardData system.

## Core Effects

### **Damage**
- **Description**: Deal direct damage to targets
- **Targeting**: Opponents, self, or allies
- **Scaling**: ✅ Supported
- **Notes**: Primary offensive mechanic

### **Heal**
- **Description**: Restore health to targets
- **Targeting**: Self or allies primarily
- **Scaling**: ✅ Supported
- **Notes**: Primary healing mechanic

## Defensive Status Effects

### **Apply Shield**
- **Description**: Absorbs incoming damage before it affects health
- **Duration**: Lasts until consumed or manually removed
- **Targeting**: Self or allies
- **Scaling**: ✅ Supported
- **Behavior**: Stacks with existing Shield effects
- **Notes**: Removed at start of entity's turn if not consumed

### **Apply Thorns**
- **Description**: Reflects damage back to attackers
- **Duration**: 1 turn (until start of next turn)
- **Targeting**: Self or allies
- **Scaling**: ✅ Supported
- **Behavior**: Stacks with existing Thorns effects
- **Notes**: Reflects full thorns amount regardless of damage taken

## Positive Status Effects

### **Apply Strength**
- **Description**: Increases damage dealt by the entity
- **Duration**: Permanent until removed
- **Targeting**: Self or allies
- **Scaling**: ✅ Supported
- **Behavior**: Stacks with existing Strength effects
- **Notes**: Tracked in EntityTracker for damage calculations

### **Apply Salve**
- **Description**: Provides healing over time or damage reduction
- **Duration**: Ticks down by potency each turn
- **Targeting**: Self or allies
- **Scaling**: ✅ Supported
- **Behavior**: Stacks with existing Salve effects

### **Raise Critical Chance**
- **Description**: Increases critical hit chance by specified percentage
- **Targeting**: Self or allies
- **Scaling**: ❌ Not supported (percentage scaling would be weird)
- **Notes**: Percentage-based effect

## Negative Status Effects (Debuffs)

### **Apply Weak**
- **Description**: Reduces damage dealt by the affected entity
- **Duration**: Duration-based (counts down each turn)
- **Targeting**: Opponents primarily
- **Scaling**: ✅ Supported
- **Behavior**: Stacks duration with existing Weak effects
- **Notes**: Uses OnlineGameManager.WeakStatusModifier for damage reduction

### **Apply Break**
- **Description**: Increases damage taken by the affected entity
- **Duration**: Duration-based (counts down each turn)
- **Targeting**: Opponents primarily
- **Scaling**: ✅ Supported
- **Behavior**: Stacks duration with existing Break effects
- **Notes**: Increases damage taken by 50% (BreakStatusModifier)

### **Apply Burn**
- **Description**: Deals damage over time at end of each turn
- **Duration**: Ticks down by potency each turn
- **Targeting**: Opponents primarily
- **Scaling**: ✅ Supported
- **Behavior**: Stacks with existing Burn effects
- **Notes**: Damage applied at end of turn, then potency reduces by 1

### **Apply Curse**
- **Description**: Reduces damage output (acts like negative Strength)
- **Duration**: Ticks down by potency each turn
- **Targeting**: Opponents primarily
- **Scaling**: ✅ Supported
- **Behavior**: Stacks with existing Curse effects
- **Notes**: Subtracts potency from damage dealt

### **Apply Stun**
- **Description**: Makes next X cards played fizzle (no effects)
- **Duration**: Per-card based, not turn-based
- **Targeting**: Opponents primarily
- **Scaling**: ❌ Not supported (duration doesn't scale well)
- **Behavior**: Sets fizzle card count in EntityTracker
- **Notes**: Modern implementation uses card fizzling instead of turn skipping

## Stance Effects

### **Enter Stance**
- **Description**: Changes entity to specified stance
- **Available Stances**: 
  - Warrior (defensive)
  - Mystic (magical)
  - Assassin (offensive)
  - Focused (concentration)
- **Targeting**: Self primarily
- **Scaling**: ❌ Not supported (binary state)
- **Notes**: Stance changes are tracked in EntityTracker

### **Exit Stance**
- **Description**: Returns entity to neutral stance
- **Targeting**: Self
- **Scaling**: ❌ Not supported
- **Notes**: Can't exit stance multiple times

## Advanced Mechanics

### **Conditional Effects**
All effects can be made conditional using:
- **Condition Types**: Health thresholds, card counts, combo states, stance checks, etc.
- **Alternative Logic**: Replace main effect OR add bonus effect
- **Flexible Targeting**: Conditions can change based on game state

### **Scaling Effects**
Many effects support scaling based on:
- **Zero-cost cards** played this turn/fight
- **Cards played** this turn/fight
- **Damage dealt** this turn/fight
- **Current/Missing health**
- **Combo count**
- **Hand size**

### **Stance Exit Integration**
- Any effect can also exit the current stance when triggered
- Uses `shouldExitStance` boolean flag
- Processed after main effect resolves

## Effect Combinations

### **Multi-Effect Cards**
- Cards can have multiple effects that trigger together
- Each effect can have different targeting
- Conditional effects can create complex decision trees

### **Effect Interactions**
- **Shield + Thorns**: Shield absorbs damage, Thorns still reflects
- **Strength + Curse**: Net damage modification calculated
- **Burn + Salve**: Damage and healing can coexist
- **Weak + Break**: Damage reduction and amplification stack

## Removed Effects

The following effects have been removed from the system:
- ~~**Draw Cards**~~ - No longer fits game flow
- ~~**Restore Energy**~~ - Energy restoration removed
- ~~**Discard Random Cards**~~ - Card manipulation removed
- ~~**Apply Elemental Status**~~ - Elemental system removed
- ~~**Apply Limit Break**~~ - Removed for simplification

## Technical Notes

### **Status Effect Processing**
- **End of Turn**: Burn damage, potency reductions
- **Start of Turn**: Shield/Thorns removal
- **Damage Calculation**: Strength/Curse/Weak/Break modifiers applied
- **Card Play**: Fizzle checks for stunned entities

### **Effect Stacking**
- **Potency Stacking**: Shield, Thorns, Strength, Salve, Burn, Curse
- **Duration Stacking**: Weak, Break
- **Binary Effects**: Stances (replace previous state)

### **Validation**
- Cards validate effect configurations on creation
- Prevents invalid combinations (e.g., negative amounts for positive effects)
- Ensures proper targeting for effect types

## Usage Examples

### **Basic Damage Card**
```csharp
cardData.SetupBasicDamageCard("Fire Bolt", 6, 2, CardTargetType.Opponent);
```

### **Conditional Healing**
```csharp
cardData.AddConditionalEffectOR(
    CardEffectType.Heal, 8, CardTargetType.Self,
    ConditionalType.IfSourceHealthBelow, 50,
    CardEffectType.Heal, 4
);
```

### **Scaling Damage**
```csharp
var effect = new CardEffect {
    effectType = CardEffectType.Damage,
    amount = 3,
    scalingType = ScalingType.ComboCount,
    scalingMultiplier = 2.0f,
    maxScaling = 15
};
```

This system provides a flexible foundation for creating diverse and interesting card effects while maintaining clear mechanical interactions. 