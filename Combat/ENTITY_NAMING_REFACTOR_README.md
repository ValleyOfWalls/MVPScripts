# Entity Naming Refactor: Generic Combat System

## Overview

This document describes the completed refactoring of entity naming from the old confusing system (player/opponent/ally/opponent pet ally) to a clearer left/right positioning-based system (leftfighter/leftally/rightfighter/rightally). The system now uses generic, flexible methods instead of overspecialized ones.

## New Naming Convention

### Entity Types in Combat

1. **Left Fighter** (formerly "Player")
   - Positioned on the left side of the fight
   - Typically the human player
   - Entity Type: `EntityType.Player`

2. **Left Ally** (formerly "Ally" or "Player's Pet")
   - The pet that fights alongside the Left Fighter
   - Owned by the Left Fighter
   - Entity Type: `EntityType.Pet`

3. **Right Fighter** (formerly "Opponent Pet")
   - Positioned on the right side of the fight
   - Typically an opponent's pet
   - Entity Type: `EntityType.Pet`

4. **Right Ally** (formerly "Opponent Pet Ally" or "Opponent Player")
   - The player that owns the Right Fighter
   - Entity Type: `EntityType.Player`

## FightManager API - Generic Methods

The FightManager now provides simple, generic methods that work for any entity:

### Core Generic Methods

- `GetOpponent(NetworkEntity fighter)` - Gets the opponent for any fighter
- `GetAlly(NetworkEntity fighter)` - Gets the ally for any fighter  
- `GetFight(NetworkEntity entity)` - Gets the fight assignment for any entity
- `GetFightEntities(NetworkEntity entity)` - Gets all entities in a fight [leftfighter, leftally, rightfighter, rightally]

### Properties (Simplified Naming)

- `ClientLeftFighter` - The left fighter in the local client's fight
- `ClientRightFighter` - The right fighter in the local client's fight
- `ViewedLeftFighter` - The left fighter in the currently viewed fight
- `ViewedRightFighter` - The right fighter in the currently viewed fight

### Specialized Methods (Removed)

The following overspecialized methods have been removed in favor of the generic ones:

- ~~`GetOpponentForPlayer()`~~ → Use `GetOpponent()`
- ~~`GetOpponentForPet()`~~ → Use `GetOpponent()`
- ~~`GetFightForPlayer()`~~ → Use `GetFight()`
- ~~`GetFightForPet()`~~ → Use `GetFight()`
- ~~`GetLeftAllyForLeftFighter()`~~ → Use `GetAlly()`
- ~~`GetRightAllyForRightFighter()`~~ → Use `GetAlly()`

## Dual Ally Display System

### New Display Controllers

- `FighterAllyDisplayController` - Universal controller for both left and right ally displays
- Supports `FighterSide.Left` and `FighterSide.Right` configuration
- Replaces the old `OwnPetViewController` with a more flexible system

### CombatCanvasManager Updates

- Added support for dual ally displays (left and right)
- New properties: `GetLeftAllyDisplayController()`, `GetRightAllyDisplayController()`
- Backward compatibility maintained with existing `GetOwnPetViewController()`

## Benefits of Generic Approach

1. **Flexibility** - Methods work with any entity type, reducing code duplication
2. **Simplicity** - Fewer methods to remember and maintain
3. **Consistency** - Single API for all fighter/ally relationships
4. **Maintainability** - Easier to extend and modify behavior
5. **Clean Code** - No more overspecialized edge case methods

## Usage Examples

### Get opponent for any fighter
```csharp
NetworkEntity opponent = fightManager.GetOpponent(anyFighter);
```

### Get ally for any fighter
```csharp
NetworkEntity ally = fightManager.GetAlly(anyFighter);
```

### Get all entities in a fight
```csharp
List<NetworkEntity> allEntities = fightManager.GetFightEntities(anyEntity);
// Returns: [leftfighter, leftally, rightfighter, rightally]
```

## Files Updated

- `Combat/FightManager.cs` - Core refactoring and generic methods
- `Combat/CombatManager.cs` - Updated to use new methods
- `Combat/CombatCanvasManager.cs` - Dual ally display support
- `Combat/OwnPetDisplay/FighterAllyDisplayController.cs` - New universal controller
- `CardObject/CardEffectResolver.cs` - Updated to use generic methods
- `CardObject/SourceAndTargetIdentifier.cs` - Updated to use generic methods
- `CardObject/CardDragDrop.cs` - Updated to use generic methods
- `Testing/CombatTestManager.cs` - Updated to use generic methods
- All other files that referenced the old specialized methods 