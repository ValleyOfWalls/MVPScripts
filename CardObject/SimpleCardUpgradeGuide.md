# Simple Card Upgrade System Guide

## Overview

The simplified card upgrade system allows cards to upgrade immediately when conditions are met. All upgrade configuration is done directly in the CardData asset - no separate upgrade files needed!

## Quick Setup Example: Strike → Strike+

### 1. Create Your Cards

**Strike.asset (Base Card)**
```
Basic Info:
- Card Name: "Strike"
- Card Id: 1001
- Energy Cost: 1
- Effects: Deal 6 damage

Upgrade Conditions:
- Can Upgrade: ✓ true
- Upgraded Version: StrikePlus.asset (drag and drop)
- Upgrade Condition Type: TimesPlayedThisFight
- Upgrade Required Value: 5
- Upgrade Comparison Type: GreaterThanOrEqual
- Upgrade All Copies: ✓ true
```

**StrikePlus.asset (Upgraded Card)**
```
Basic Info:
- Card Name: "Strike+"
- Card Id: 1002  
- Energy Cost: 1
- Effects: Deal 9 damage

Upgrade Conditions:
- Can Upgrade: ✗ false (upgraded cards don't upgrade further)
```

### 2. Setup CardUpgradeManager

1. Create empty GameObject named "CardUpgradeManager"
2. Add `CardUpgradeManager` component
3. In "Upgradeable Cards" list, drag your Strike.asset
4. Done! The manager will automatically detect and monitor Strike for upgrades

### 3. How It Works

```
Turn 1: Player plays Strike (1st time) → No upgrade yet
Turn 3: Player plays Strike (2nd time) → No upgrade yet  
Turn 5: Player plays Strike (3rd time) → No upgrade yet
Turn 7: Player plays Strike (4th time) → No upgrade yet
Turn 9: Player plays Strike (5th time) → UPGRADE!
  ↳ All Strike cards in deck become Strike+ immediately
  ↳ Player now has Strike+ cards for rest of fight and future fights
```

## Available Condition Types

### Card Usage
- `TimesPlayedThisFight`: Upgrade after playing X times this fight
- `TimesPlayedAcrossFights`: Upgrade after playing X times total (persistent)
- `CopiesInDeck/Hand/Discard`: Upgrade based on copies in different zones

### Combat Performance  
- `DamageDealtThisFight`: Upgrade after dealing X damage this fight
- `DamageDealtInSingleTurn`: Upgrade after dealing X damage in one turn
- `HealingGivenThisFight`: Upgrade after healing X health this fight

### Tactical Conditions
- `ComboCountReached`: Upgrade when combo reaches X
- `PlayedWithCombo`: Upgrade when played with active combo
- `ZeroCostCardsThisTurn`: Upgrade after playing X zero-cost cards this turn

### Victory Conditions
- `WonFightUsingCard`: Upgrade after winning X fights with this card in deck
- `SurvivedFightWithCard`: Upgrade after surviving X fights with this card

## Comparison Types

- `GreaterThanOrEqual`: Most common (≥)
- `Equal`: Exact match (=)
- `GreaterThan`: Strictly greater (>)
- `LessThanOrEqual`: At most (≤)
- `LessThan`: Strictly less (<)

## More Examples

### Healing Potion → Greater Healing
```
Condition: HealingGivenThisFight ≥ 50
Result: Upgrades after healing 50+ HP total this fight
```

### Fireball → Mega Fireball  
```
Condition: DamageDealtInSingleTurn ≥ 25
Result: Upgrades after dealing 25+ damage in a single turn
```

### Shield → Tower Shield
```
Condition: SurvivedFightWithCard ≥ 3  
Result: Upgrades after surviving 3 fights with Shield in deck
```

## Key Benefits

✅ **Simple Setup**: Just configure fields directly on CardData
✅ **No Extra Files**: No separate upgrade ScriptableObjects needed
✅ **Visual Inspector**: See all upgrade info right in the card asset
✅ **Automatic Detection**: Manager finds upgradeable cards automatically
✅ **Immediate Feedback**: Upgrades happen instantly when conditions are met

## Migration from Old System

If you had the complex upgrade system before:

1. **Delete**: All CardUpgradeData.asset files
2. **Delete**: CardUpgradeDatabase GameObject  
3. **Update**: Set upgrade fields directly on your CardData assets
4. **Add Cards**: Drag CardData assets to CardUpgradeManager's "Upgradeable Cards" list

## Limitations

- **Single Condition**: Each card can only have one upgrade condition
- **No Complex Logic**: Can't combine multiple conditions with AND/OR
- **Immediate Only**: Upgrades always happen immediately (no end-of-turn timing)

For 90% of upgrade use cases, this simplified system is much easier to work with! 