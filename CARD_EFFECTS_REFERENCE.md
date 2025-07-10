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
- **Description**: Reflects damage back to attackers, then clears
- **Duration**: Until triggered by taking damage
- **Targeting**: Self or allies
- **Scaling**: ✅ Supported
- **Behavior**: Stacks with existing Thorns effects
- **Notes**: Reflects full thorns amount when damage is taken, then thorns are completely consumed/cleared

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
  - Aggressive (bonus damage, reduced defense)
  - Defensive (bonus defense, reduced damage)
  - Focused (energy and draw bonuses)
  - Berserker (high damage and speed, health cost)
  - Guardian (shield and thorns bonuses)
  - Mystic (enhanced magical effects)
- **Targeting**: Self primarily
- **Scaling**: ❌ Not supported (binary state)
- **Notes**: Stance changes are tracked in EntityTracker

### **Exit Stance**
- **Description**: Returns entity to neutral stance
- **Targeting**: Self
- **Scaling**: ❌ Not supported
- **Integration**: Can be triggered automatically by conditional effects
- **Notes**: Now only used via shouldExitStance flag on conditional effects

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

## Advanced Mechanics

### **Perfection Streak**
- **Description**: Tracks consecutive turns without taking damage
- **Usage**: Used as conditional trigger (IfPerfectionStreak)
- **Duration**: Resets when any damage is taken
- **Tracking**: Each turn survived without damage adds +1 to streak
- **Notes**: Different from Perfect Turns - only requires avoiding damage

### **Perfect Turns**
- **Description**: Special turns where entity uses ALL energy AND takes no damage
- **Tracking**: Separate from perfection streak
- **Conditions**: Must spend all available energy in turn AND take 0 damage
- **Usage**: Used in upgrade conditions (PerfectTurnPlayed, TotalPerfectTurns)
- **Notes**: Much rarer than perfection streak due to dual requirements

### **Combo System**
- **Description**: Sequential card play mechanic that builds combo count
- **Usage**: Some cards require combo count to play (finishers)
- **Reset**: Combo resets when cards are played out of sequence
- **Conditions**: Can be used as conditional trigger (IfComboCount)
- **Upgrade Conditions**: ComboCountReached, PlayedWithCombo, PlayedAsFinisher
- **Notes**: Essential for "finishing move" mechanics

## Complex Tactical Effects

These advanced effects provide MTG-style strategic depth and interaction.

### **Redirect Next Attack**
- **Description**: Forces the next damage targeting you to hit a different target instead
- **Targeting**: Self or allies (entity that will have attacks redirected)
- **Scaling**: ❌ Not supported (targeting is not scalable)
- **Behavior**: Single-use effect that is consumed when triggered
- **Redirect Options**:
  - **1**: Redirect to ally
  - **2**: Redirect back to attacker
  - **3**: Redirect to opponent
- **Notes**: Checks for redirect during damage processing and automatically redirects the attack

### **Amplify**
- **Description**: Increases the potency of the next card effect by the specified amount
- **Targeting**: Self or allies (entity whose next effect will be amplified)
- **Scaling**: ✅ Supported (amplification amount can scale)
- **Behavior**: Single-use effect that is consumed when the next effect is played
- **Notes**: Applied before scaling calculations, works with any effect that has an `amount` value

### **Siphon**
- **Description**: Steals one beneficial status effect from the target and transfers it to the caster
- **Targeting**: Opponents primarily (entity to steal from)
- **Scaling**: ❌ Not supported (effect transfer is binary)
- **Behavior**: Transfers first found beneficial effect (Strength, Shield, Salve, CriticalUp, Thorns)
- **Notes**: Only transfers one effect per use, removes from target and adds to source with same potency/duration

### **Revenge**
- **Description**: Conditional effect that triggers bonus effects if you took damage this turn
- **Implementation**: Uses `IfDamageTakenThisTurn` conditional type
- **Targeting**: Flexible (depends on the revenge effect)
- **Scaling**: ✅ Supported (through conditional effect scaling)
- **Usage Example**:
  ```csharp
  // Card deals 4 damage, but 8 damage if you took damage this turn
  effect.effectType = CardEffectType.Damage;
  effect.amount = 4;
  effect.conditionType = ConditionalType.IfDamageTakenThisTurn;
  effect.conditionValue = 1;
  effect.hasAlternativeEffect = true;
  effect.alternativeEffectType = CardEffectType.Damage;
  effect.alternativeEffectAmount = 8;
  effect.alternativeLogic = AlternativeEffectLogic.Replace;
  ```
- **Notes**: Implemented through conditional system rather than direct effect

### **Corrupt**
- **Description**: Converts the target's first beneficial status effect into a harmful equivalent
- **Targeting**: Opponents (entity to corrupt)
- **Scaling**: ❌ Not supported (conversion is binary)
- **Behavior**: Converts first found beneficial effect using corruption mapping
- **Corruption Mappings**:
  - **Shield** → **Burn**
  - **Strength** → **Curse**
  - **Salve** → **Burn**
  - **CriticalUp** → **Weak**
  - **Thorns** → **Break**
- **Notes**: Preserves potency and duration of the original effect

### **Mimic**
- **Description**: Copies the last card effect that was used against the caster
- **Targeting**: Opponents (target to use the mimicked effect on)
- **Scaling**: ❌ Not supported (copies exact effect)
- **Behavior**: Retrieves and replicates the last hostile effect used against the caster
- **Tracking**: EntityTracker automatically records effects used against entities
- **Notes**: Only tracks hostile effects (source != target), applies exact same effect type, amount, and duration

### **Health Swap**
- **Description**: Swaps current health totals between the caster and target
- **Targeting**: Allies primarily (though can target opponents strategically)
- **Scaling**: ❌ Not supported (health values are absolute)
- **Behavior**: Instantly exchanges current health values between two entities
- **Strategic Uses**:
  - Save a low-health ally by giving them your higher health
  - Tactical positioning when you have more health than maximum damage threats
- **Notes**: Does not affect maximum health, only current health values

### **Complex Effect Interactions**
- **Redirect + Amplify**: Redirected attacks can trigger amplified effects on the new target
- **Siphon + Corrupt**: Can steal an effect, then corrupt the remaining effects
- **Mimic + Revenge**: Revenge effects can be mimicked if used against you
- **Health Swap + Conditional Effects**: Health-based conditionals recalculate after swap

## Removed Effects

The following effects have been removed from the system:
- ~~**Draw Cards**~~ - Card manipulation removed
- ~~**Restore Energy**~~ - Energy restoration removed
- ~~**Persistent Effects**~~ - Complex persistence removed
- ~~**Zone Effects**~~ - Multi-target effects removed
- ~~**Limit Break Stance**~~ - Limit break system removed
- ~~**Discard Random Cards**~~ - Card manipulation removed
- ~~**Apply Elemental Status**~~ - Elemental system removed

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