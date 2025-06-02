# Enhanced Card System Documentation

## Overview

The enhanced card system provides comprehensive support for complex card mechanics, tracking, and effects. This system follows SOLID principles with modular components that can be mixed and matched to create diverse card behaviors.

## Core Components

### 1. CardEnums.cs
Defines all enums and data structures for the enhanced card system:

- **CardEffectType**: Extended enum supporting 20+ effect types including status effects, elemental effects, and complex mechanics
- **CardTargetType**: Enhanced targeting including Self, Ally, All, AllAllies, AllEnemies
- **ElementalType**: Fire, Ice, Lightning, Void elemental effects
- **ConditionalType**: Conditions for conditional effects (health thresholds, tracking data, etc.)
- **ConditionalEffect**: Data structure for conditional card effects
- **MultiEffect**: Data structure for cards with multiple effects
- **CardTrackingData**: Tracking data for individual cards
- **EntityTrackingData**: Tracking data for entities (damage, healing, perfection, etc.)

### 2. CardData.cs (Enhanced)
The ScriptableObject that defines card properties:

#### New Properties:
- **Duration**: For status effects
- **ElementalType**: Elemental classification
- **HasComboModifier**: For combo tracking
- **UpgradedVersion**: Reference to upgraded card
- **HasMultipleEffects**: Enable multi-effect cards
- **MultiEffects**: List of additional effects
- **HasConditionalEffect**: Enable conditional effects
- **ConditionalEffect**: Conditional effect data
- **Advanced Targeting**: CanTargetSelf, CanTargetAlly, RequiresSpecificTarget
- **Tracking Flags**: TrackPlayCount, TrackDeckComposition, etc.

#### Methods:
- `GetValidTargetTypes()`: Returns all valid targets for the card
- `GetEffectiveTargetType()`: Gets the target type considering overrides
- `CanTarget(CardTargetType)`: Checks if card can target specific type

### 3. EntityTracker.cs
Tracks combat statistics and states for entities:

#### Tracked Data:
- **Damage**: Taken/dealt this fight and last round
- **Healing**: Received/given this fight and last round
- **Perfection**: Streak of turns without taking damage
- **Combo**: Current combo count
- **States**: Stunned, Limit Break
- **Card Play Counts**: Per card ID

#### Key Methods:
- `RecordDamageTaken(int)`: Records damage taken
- `RecordDamageDealt(int)`: Records damage dealt
- `RecordHealingReceived(int)`: Records healing received
- `RecordHealingGiven(int)`: Records healing given
- `RecordCardPlayed(int, bool)`: Records card play with combo tracking
- `StartNewTurn()`: Processes turn start (perfection tracking)
- `StartNewRound()`: Processes round start
- `ResetForNewFight()`: Resets all tracking for new fight
- `CheckCondition(ConditionalType, int)`: Checks conditional requirements

### 4. CardTracker.cs
Tracks card-specific data:

#### Tracked Data:
- **Play Count**: Times played this fight
- **Deck Composition**: Cards with same name in deck/hand/discard
- **Upgraded Version**: Reference tracking

#### Key Methods:
- `RecordCardPlayed()`: Records card play
- `CheckCondition(ConditionalType, int)`: Checks card-specific conditions
- `UpdateDeckComposition()`: Updates deck composition tracking

### 5. SourceAndTargetIdentifier.cs (Enhanced)
Enhanced targeting system:

#### New Features:
- **Multi-Target Support**: AllTargets list for cards affecting multiple entities
- **Enhanced Target Types**: Support for all new target types
- **Ally Targeting**: Finds ally entities through RelationshipManager
- **Random Targeting**: Selects random valid targets
- **Debug Information**: Comprehensive debug display

#### Key Methods:
- `GetTargetEntities(List<NetworkEntity>)`: Gets all valid targets
- `GetAllyForEntity(NetworkEntity)`: Finds ally entity
- `GetAllPossibleTargets()`: Gets all entities in fight
- `ForceUpdateSourceAndTargets(NetworkEntity, List<NetworkEntity>)`: For multi-target cards

### 6. CardEffectResolver.cs (Enhanced)
Comprehensive effect resolution system:

#### Supported Effects:
1. **Basic Effects**: Damage, Heal, DrawCard, RestoreEnergy
2. **Status Effects**: Break, Weak, DamageOverTime, HealOverTime
3. **Advanced Effects**: Thorns, Shield, CriticalUp, Stun, LimitBreak
4. **Elemental Effects**: Fire, Ice, Lightning, Void
5. **Card Manipulation**: DiscardRandomCards
6. **Multi-Effects**: Cards with multiple simultaneous effects
7. **Conditional Effects**: Effects based on game state

#### Key Methods:
- `ProcessCardEffects()`: Main effect processing
- `ProcessSingleEffect()`: Processes individual effects
- `ProcessConditionalEffect()`: Handles conditional logic
- `CheckCondition()`: Evaluates conditional requirements
- `GetTargetsForEffect()`: Gets appropriate targets per effect

### 7. EffectHandler.cs (Enhanced)
Enhanced status effect system:

#### New Status Effects:
- **Thorns**: Reflects damage to attackers
- **Shield**: Absorbs incoming damage
- **Elemental Effects**: Fire (DoT), Ice (slow), Lightning (energy drain), Void (anti-heal)
- **Stun**: Prevents actions
- **LimitBreak**: Enhanced abilities

#### Key Methods:
- `ProcessThornsReflection(NetworkEntity, int)`: Handles thorns damage
- `ProcessShieldAbsorption(int)`: Processes shield absorption
- `HandleEffectExpiration(string)`: Special logic for effect expiration
- `UpdateEffectPotency(string, int)`: Updates effect strength

## Card Mechanics Implementation

### 1. Break/Weak Status Effects
```csharp
// In CardData, set:
EffectType = CardEffectType.ApplyBreak; // or ApplyWeak
Amount = 1; // Potency
Duration = 3; // Turns
```

### 2. Damage/Heal Over Time
```csharp
// In CardData, set:
EffectType = CardEffectType.ApplyDamageOverTime; // or ApplyHealOverTime
Amount = 2; // Damage/heal per turn
Duration = 3; // Number of turns
```

### 3. Self/Ally Targeting
```csharp
// In CardData, set:
TargetType = CardTargetType.Self; // or Ally
CanTargetSelf = true;
CanTargetAlly = true;
```

### 4. Multi-Effect Cards
```csharp
// In CardData, set:
HasMultipleEffects = true;
// Add to MultiEffects list:
// Effect 1: Damage to opponent
// Effect 2: Heal to self
```

### 5. Conditional Effects
```csharp
// In CardData, set:
HasConditionalEffect = true;
ConditionalEffect.conditionType = ConditionalType.IfTargetHealthBelow;
ConditionalEffect.conditionValue = 50; // 50% health
ConditionalEffect.effectType = CardEffectType.Damage;
ConditionalEffect.effectAmount = 10; // Extra damage if condition met
```

### 6. Combo Cards
```csharp
// In CardData, set:
HasComboModifier = true;
// Combo count increases when played, resets when non-combo card played
```

### 7. Tracking-Based Effects
```csharp
// In CardData, set:
TrackPlayCount = true;
HasConditionalEffect = true;
ConditionalEffect.conditionType = ConditionalType.IfTimesPlayedThisFight;
ConditionalEffect.conditionValue = 3; // If played 3+ times
```

## Integration Points

### CombatManager Integration
- Initializes EntityTrackers for new fights
- Notifies trackers of turn/round changes
- Resets tracking data between fights

### LifeHandler Integration
- Processes shield absorption before damage
- Triggers thorns reflection after damage
- Records damage/healing in EntityTracker

### HandManager Integration
- CardTracker monitors deck composition
- Supports card discard effects
- Tracks cards in hand/deck/discard

## Debugging Features

### Inspector Display
All tracking components show read-only debug information:
- **EntityTracker**: Current stats, streaks, states
- **CardTracker**: Play counts, deck composition
- **SourceAndTargetIdentifier**: Current source/targets

### Logging
Comprehensive logging for:
- Effect applications and expirations
- Tracking data updates
- Conditional effect evaluations
- Target resolution

## Usage Examples

### Creating a Thorns Card
```csharp
// CardData settings:
EffectType = CardEffectType.ApplyThorns
TargetType = CardTargetType.Self
Amount = 3 // Reflects 3 damage
Duration = 5 // Lasts 5 turns
```

### Creating a Conditional Heal
```csharp
// CardData settings:
HasConditionalEffect = true
ConditionalEffect.conditionType = ConditionalType.IfSourceHealthBelow
ConditionalEffect.conditionValue = 25 // If below 25% health
ConditionalEffect.effectType = CardEffectType.Heal
ConditionalEffect.effectAmount = 15 // Heal for 15
ConditionalEffect.hasAlternativeEffect = true
ConditionalEffect.alternativeEffectType = CardEffectType.Heal
ConditionalEffect.alternativeEffectAmount = 5 // Otherwise heal for 5
```

### Creating a Multi-Target Card
```csharp
// CardData settings:
TargetType = CardTargetType.AllAllies
EffectType = CardEffectType.Heal
Amount = 5 // Heals all allies for 5
```

This enhanced card system provides the foundation for complex, strategic card gameplay while maintaining clean, modular code architecture. 