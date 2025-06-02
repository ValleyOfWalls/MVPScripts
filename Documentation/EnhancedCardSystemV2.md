# Enhanced Card System V2 - Complete Feature Guide

⚠️ **LEGACY DOCUMENTATION WARNING** ⚠️

**This documentation describes the legacy card system that has been significantly simplified.**

**For the current unified system documentation, see:** `NewUnifiedEffectSystem.md`

**Key Changes in Current System:**
- Persistent effects now use `PersistentFightEffect` structure with core `CardEffectType` values
- Removed redundant enum values like `PersistentDamageAura`, `PersistentHealingAura`, etc.
- Simplified effect system removes redundancy between enum values and data structures

---

## Overview

The Enhanced Card System V2 significantly expands the card game mechanics with advanced features including zone effects, card types, sequencing, scaling effects, persistent fight effects, stance system, and strength mechanics. This system follows SOLID principles and maintains full network synchronization.

## New Mechanics Summary

### 1. Card Types & Sequencing
- **11 Card Types**: Attack, Skill, Spell, Combo, Finisher, Stance, Artifact, Ritual, Counter, Reaction
- **Sequence Requirements**: Cards can require specific card types to be played before them
- **Combo/Finisher System**: Cards that build combos and cards that require combos

### 2. Zone Effects (Global Effects)
- **All Players/Pets**: Effects that affect ALL players or pets globally, not just local fight
- **Configurable Targeting**: Include/exclude caster, opponents, allies
- **Scaling**: Zone effects can scale based on various metrics

### 3. Persistent Local Fight Effects
- **Fight-Long Effects**: Effects that last the entire fight with configurable triggers
- **Turn-Based Triggers**: Effects that trigger every turn, every other turn, etc.
- **Stance Requirements**: Effects that only work while in specific stances

### 4. Enhanced Stance System
- **7 Stance Types**: None, Aggressive, Defensive, Focused, Berserker, Guardian, Mystic, LimitBreak
- **Stance Benefits**: Each stance provides unique stat modifiers and special effects
- **Stance Transitions**: Cards can force stance changes or require specific stances

### 5. Scaling Effects
- **10 Scaling Types**: Zero-cost cards, cards played, damage dealt, health states, combo count, hand size
- **Dynamic Scaling**: Card effects that grow stronger based on game state
- **Scaling Caps**: Maximum limits to prevent infinite scaling

### 6. Zero-Cost Card Tracking
- **Turn/Fight Tracking**: Separate tracking for zero-cost cards played this turn vs entire fight
- **Scaling Integration**: Many effects scale based on zero-cost card count
- **Strategic Depth**: Encourages zero-cost card strategies

### 7. Strength System
- **Damage Boost**: Strength stacks add damage to all attacks
- **Stack Management**: Add/remove strength with various effects
- **Network Synchronized**: Full multiplayer support for strength tracking

## File Structure

```
CardObject/
├── CardEnums.cs                 # All enums and data structures
├── CardData.cs                  # Enhanced card data with new properties
├── CardEffectResolver.cs        # Processes all card effects
├── CardTracker.cs               # Individual card tracking
├── Card.cs                      # Card visual component
└── HandleCardPlay.cs            # Card play validation and handling

Player+PetObject/
├── EntityTracker.cs             # Combat statistics and state tracking
└── HandManager.cs               # Enhanced with new utility methods

Utility/
└── ReadOnlyAttribute.cs         # Inspector debugging support

Documentation/
├── EnhancedCardSystem.md        # Original system documentation
└── EnhancedCardSystemV2.md      # This comprehensive guide
```

## Key Components

### CardEnums.cs
Contains all the new enums and data structures:

- **CardType**: 11 different card categories
- **StanceType**: 7 different stances with unique effects
- **ScalingType**: 10 different scaling metrics
- **ConditionalType**: Extended conditional checks
- **CardSequenceRequirement**: Data for card sequencing
- **ZoneEffect**: Global effect configuration
- **PersistentFightEffect**: Long-lasting effect data
- **StanceEffect**: Stance transition and benefit data
- **ScalingEffect**: Dynamic scaling configuration

### EntityTracker.cs (Enhanced)
Now tracks comprehensive combat data:

- **Stance Management**: Current stance, stance transitions
- **Zero-Cost Tracking**: Turn and fight-level zero-cost card counts
- **Strength System**: Strength stack management
- **Persistent Effects**: Fight-long effect storage
- **Turn Lifecycle**: Proper turn start/end processing
- **Sequencing**: Last played card type tracking

### CardEffectResolver.cs (Enhanced)
Processes all new effect types:

- **Zone Effects**: Global effects affecting all players/pets
- **Persistent Effects**: Fight-long effects with custom triggers
- **Stance Effects**: Stance changes and benefits
- **Scaling Effects**: Dynamic effects based on game state
- **Sequence Validation**: Checks card play requirements
- **Strength Integration**: Adds strength bonus to damage

### HandleCardPlay.cs (Enhanced)
Validates card play with new restrictions:

- **Stun Checking**: Prevents card play when stunned
- **Sequence Validation**: Ensures proper card order
- **Energy Validation**: Checks energy costs
- **Target Validation**: Ensures valid targets exist

## New Card Properties

### Basic Properties
```csharp
public CardType CardType                    // Type of card (Attack, Skill, etc.)
public bool IsFinisher                      // Requires combo to play
public bool IsZeroCost                      // Automatic property based on energy cost
```

### Sequencing Properties
```csharp
public CardSequenceRequirement SequenceRequirement  // What must be played before this card
public bool CanPlayWithSequence()                   // Validates sequence requirements
public string GetSequenceRequirementText()          // UI description
```

### Zone Effect Properties
```csharp
public bool HasZoneEffect                   // Card has global effects
public List<ZoneEffect> ZoneEffects         // List of zone effects
```

### Persistent Effect Properties
```csharp
public bool HasPersistentEffect            // Card creates lasting effects
public List<PersistentFightEffect> PersistentEffects  // Fight-long effects
```

### Stance Properties
```csharp
public bool AffectsStance                   // Card changes stance
public StanceEffect StanceEffect            // Stance change configuration
```

### Scaling Properties
```csharp
public bool HasScalingEffect               // Card has dynamic scaling
public List<ScalingEffect> ScalingEffects  // Scaling configurations
public int GetScalingAmount()              // Calculate scaled values
```

## Example Card Configurations

### 1. Combo Attack Card
```csharp
CardType: Attack
HasComboModifier: true
EffectType: Damage
Amount: 3
Description: "Deal 3 damage. Builds combo."
```

### 2. Finisher Card
```csharp
CardType: Finisher
IsFinisher: true
SequenceRequirement:
  - hasSequenceRequirement: true
  - requiredPreviousCardType: Combo
  - allowIfComboActive: true
EffectType: Damage
Amount: 8
Description: "Deal 8 damage. Requires combo."
```

### 3. Zone Healing Card
```csharp
CardType: Spell
HasZoneEffect: true
ZoneEffects:
  - effectType: ZoneHealAll
  - baseAmount: 2
  - affectAllPlayers: true
  - affectAllPets: true
Description: "Heal all players and pets for 2."
```

### 4. Scaling Damage Card
```csharp
CardType: Attack
HasScalingEffect: true
ScalingEffects:
  - scalingType: ZeroCostCardsThisTurn
  - baseAmount: 1
  - scalingMultiplier: 1.0
  - maxScaling: 10
Description: "Deal 1 damage + 1 per zero-cost card played this turn (max 10)."
```

### 5. Stance Change Card
```csharp
CardType: Stance
AffectsStance: true
StanceEffect:
  - stanceType: Aggressive
  - damageModifier: 2
  - defenseModifier: -1
  - grantsThorns: true
  - thornsAmount: 1
Description: "Enter Aggressive stance: +2 damage, -1 defense, gain 1 thorns."
```

### 6. Persistent Effect Card
```csharp
CardType: Artifact
HasPersistentEffect: true
PersistentEffects:
  - effectName: "Energy Regeneration"
  - effectType: PersistentEnergyRegen
  - potency: 1
  - triggerInterval: 0
  - lastEntireFight: true
Description: "Gain 1 energy at the start of each turn for the rest of the fight."
```

## Card Effect Types

### Zone Effects
- **ZoneDamageAll**: Damage all entities globally
- **ZoneHealAll**: Heal all entities globally
- **ZoneBuffAll**: Buff all entities globally
- **ZoneDebuffAll**: Debuff all entities globally
- **ZoneDrawAll**: All entities draw cards
- **ZoneEnergyAll**: All entities gain energy
- **ZoneStatusAll**: Apply status to all entities

### Persistent Effects
- **PersistentDamageAura**: Ongoing damage each turn
- **PersistentHealingAura**: Ongoing healing each turn
- **PersistentEnergyRegen**: Energy gain each turn
- **PersistentDrawBonus**: Extra card draw each turn
- **PersistentDamageReduction**: Reduced incoming damage
- **PersistentCritBonus**: Increased critical chance

### Stance Effects
- **EnterStance**: Change to specific stance
- **ExitStance**: Return to neutral stance

### Scaling Effects
- **ScaleByZeroCostCards**: Scale by zero-cost cards played
- **ScaleByCardsPlayed**: Scale by total cards played
- **ScaleByDamageDealt**: Scale by damage dealt
- **ScaleByHealth**: Scale by current/missing health

### Enhanced Status Effects
- **ApplyStrength**: Add damage-boosting stacks
- **ApplyStun**: Prevent card play next turn
- **ApplyLimitBreak**: Enhanced abilities stance

## Targeting System

### Basic Targeting
- **Self**: Target the caster
- **Opponent**: Target enemy
- **Ally**: Target ally (player ↔ pet)
- **Random**: Random valid target

### Zone Targeting
- **AllPlayers**: All players globally
- **AllPets**: All pets globally
- **Everyone**: All entities globally
- **All**: All entities in fight
- **AllAllies**: All allies in fight
- **AllEnemies**: All enemies in fight

## Scaling System

### Scaling Types
1. **ZeroCostCardsThisTurn**: Number of zero-cost cards played this turn
2. **ZeroCostCardsThisFight**: Total zero-cost cards played this fight
3. **CardsPlayedThisTurn**: Cards played this turn
4. **CardsPlayedThisFight**: Total cards played this fight
5. **DamageDealtThisTurn**: Damage dealt this turn
6. **DamageDealtThisFight**: Total damage dealt this fight
7. **CurrentHealth**: Current health amount
8. **MissingHealth**: Max health - current health
9. **ComboCount**: Current combo count
10. **HandSize**: Current hand size

### Scaling Formula
```csharp
finalAmount = baseAmount + (scalingValue * scalingMultiplier)
finalAmount = Min(finalAmount, maxScaling)
```

## Stance System

### Stance Types & Effects

#### 1. Aggressive
- **Modifiers**: +2 damage, -1 defense
- **Special**: May grant thorns
- **Use Case**: High-risk, high-reward combat

#### 2. Defensive
- **Modifiers**: +2 defense, -1 damage
- **Special**: May grant shield
- **Use Case**: Tanking and survival

#### 3. Focused
- **Modifiers**: +1 energy, +1 draw
- **Special**: Enhanced card economy
- **Use Case**: Setup and combo building

#### 4. Berserker
- **Modifiers**: +3 damage, +1 speed, -2 health
- **Special**: High damage at health cost
- **Use Case**: All-in aggressive strategies

#### 5. Guardian
- **Modifiers**: +1 defense
- **Special**: Grants shield and thorns
- **Use Case**: Protection and retaliation

#### 6. Mystic
- **Modifiers**: Enhanced elemental effects
- **Special**: Boosted elemental damage/status
- **Use Case**: Elemental strategies

#### 7. LimitBreak
- **Modifiers**: Enhanced all abilities
- **Special**: Temporary super mode
- **Use Case**: Finishing moves and comebacks

## Conditional System

### Enhanced Conditionals
- **IfZeroCostCardsThisTurn**: Based on zero-cost cards this turn
- **IfZeroCostCardsThisFight**: Based on zero-cost cards this fight
- **IfInStance**: Based on current stance
- **IfLastCardType**: Based on last played card type
- **IfEnergyRemaining**: Based on current energy

### Condition Evaluation
Cards can have different effects based on whether conditions are met, with optional alternative effects when conditions fail.

## Network Synchronization

All new mechanics are fully network synchronized:

- **SyncVars**: Stance, strength, combo count, zero-cost tracking
- **SyncLists**: Persistent effects
- **Server Authority**: All effect processing on server
- **Client Updates**: Real-time UI updates

## Integration with Existing Systems

### CombatManager Integration
- **EntityTracker Lifecycle**: Initialize and manage trackers
- **Turn Management**: Process stance and persistent effects
- **Fight Lifecycle**: Reset tracking between fights

### EffectHandler Integration
- **Status Effects**: Enhanced with strength, stun, stance effects
- **Effect Expiration**: Proper cleanup of temporary effects
- **Network Sync**: Status effect synchronization

### LifeHandler Integration
- **Strength Damage**: Automatic strength bonus application
- **Shield Absorption**: Damage reduction processing
- **Thorns Reflection**: Damage reflection on attackers

## Usage Examples

### Creating a Zero-Cost Scaling Card
1. Set `EnergyCost` to 0
2. Enable `HasScalingEffect`
3. Add scaling effect with `ScalingType.ZeroCostCardsThisTurn`
4. Set base amount and multiplier

### Creating a Combo Finisher
1. Set `CardType` to `Finisher`
2. Set `IsFinisher` to true
3. Configure sequence requirement for combo cards
4. Set powerful effect with high damage/impact

### Creating a Zone Effect Card
1. Enable `HasZoneEffect`
2. Configure zone effect targeting (all players, all pets, etc.)
3. Set effect type and amount
4. Optional: Add scaling for dynamic zone effects

### Creating a Persistent Aura
1. Enable `HasPersistentEffect`
2. Configure effect name, type, and potency
3. Set trigger interval (0 = every turn)
4. Choose duration (entire fight or limited turns)

### Creating a Stance Card
1. Set `CardType` to `Stance`
2. Enable `AffectsStance`
3. Configure stance type and modifiers
4. Optional: Add immediate benefits (thorns, shield)

## Performance Considerations

- **Efficient Tracking**: Only track what's needed for active cards
- **Network Optimization**: Batch updates where possible
- **Memory Management**: Clean up temporary effects properly
- **Scalability**: System supports hundreds of simultaneous effects

## Future Expansion Points

The system is designed for easy expansion:

- **New Card Types**: Add to `CardType` enum
- **New Scaling Types**: Add to `ScalingType` enum
- **New Stance Types**: Add to `StanceType` enum
- **New Effect Types**: Add to `CardEffectType` enum
- **New Conditionals**: Add to `ConditionalType` enum

## Debugging Features

- **ReadOnly Attributes**: All tracking data visible in inspector
- **Comprehensive Logging**: Detailed debug output for all effects
- **Validation Checks**: Extensive error checking and warnings
- **Inspector Integration**: Real-time data viewing during development

This enhanced card system provides a robust foundation for complex card game mechanics while maintaining clean architecture and full multiplayer support. 