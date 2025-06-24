# CardGenerator Class

A comprehensive utility class for programmatically creating `CardData` objects for your card game. This class handles proper setup of card effects, upgrade linking, validation, and provides convenient methods for common card types.

## Features

- ✅ **Create Basic Cards**: Damage, Heal, Status Effect, Multi-Effect cards
- ✅ **Upgrade System**: Link base cards to upgraded versions with custom conditions
- ✅ **Validation**: Built-in card validation and debugging tools
- ✅ **Reflection-Based**: Works with private fields in `CardData` ScriptableObjects
- ✅ **Example Sets**: Pre-built starter card sets for testing

## Quick Start

### Creating the Example Cards (5 Damage + Upgrade)

```csharp
// Method 1: Use the built-in example generator
var (baseCard, upgradedCard) = CardGenerator.GenerateExampleDamageCards();

// This creates:
// - "Strike": 5 damage, 2 energy cost
// - "Heavy Strike": 7 damage + draw card, 2 energy cost  
// - Upgrade condition: Play Strike 3+ times this fight
```

### Creating Custom Cards

```csharp
// Create a basic damage card
CardData fireStrike = CardGenerator.CreateDamageCard(
    cardName: "Fire Strike", 
    damage: 8, 
    energyCost: 3, 
    target: CardTargetType.Opponent
);

// Create an upgraded version
CardData infernalStrike = CardGenerator.CreateUpgradedVersion(
    baseCard: fireStrike,
    upgradedName: "Infernal Strike",
    damageMultiplier: 1.5f,  // 8 -> 12 damage
    addBonusEffect: true,
    bonusEffectType: CardEffectType.ApplyBurn,
    bonusAmount: 3
);

// Link them with custom upgrade conditions
CardGenerator.LinkCardUpgrade(
    baseCard: fireStrike,
    upgradedCard: infernalStrike,
    conditionType: UpgradeConditionType.DamageDealtThisFight,
    requiredValue: 25
);
```

## Available Card Types

### Basic Cards
- **Damage Cards**: `CreateDamageCard(name, damage, cost, target)`
- **Heal Cards**: `CreateHealCard(name, healing, cost, target)`
- **Status Cards**: `CreateStatusCard(name, statusType, potency, duration, cost, target)`

### Advanced Cards
- **Multi-Effect Cards**: `CreateMultiEffectCard(name, cost, cardType, effects)`
- **Upgraded Versions**: `CreateUpgradedVersion(baseCard, upgradedName, options...)`

## Upgrade System

### Linking Cards
```csharp
CardGenerator.LinkCardUpgrade(
    baseCard: myCard,
    upgradedCard: myUpgradedCard,
    conditionType: UpgradeConditionType.TimesPlayedThisFight,
    requiredValue: 3,
    comparisonType: UpgradeComparisonType.GreaterThanOrEqual,
    upgradeAllCopies: false
);
```

### Available Upgrade Conditions
- `TimesPlayedThisFight` - Play the card X times in current fight
- `DamageDealtThisFight` - Deal X total damage this fight
- `HealingReceivedThisFight` - Receive X healing this fight
- `ComboCountReached` - Reach combo count of X
- `PlayedAtLowHealth` - Play while at low health
- Many more... (see `UpgradeConditionType` enum)

### Upgrade Options
- **Damage Multiplier**: Scale damage/healing effects
- **Cost Reduction**: Reduce energy cost of upgraded version
- **Bonus Effects**: Add additional effects to upgraded cards
- **All Copies**: Upgrade all copies when condition is met

## Card Effects System

### Simple Effects
```csharp
// Add a basic effect
card.AddEffect(CardEffectType.Damage, 5, CardTargetType.Opponent);
```

### Complex Effects
```csharp
CardEffect complexEffect = new CardEffect
{
    effectType = CardEffectType.Damage,
    amount = 8,
    targetType = CardTargetType.Opponent,
    conditionType = ConditionalType.IfTargetHealthBelow,
    conditionValue = 50,
    hasAlternativeEffect = true,
    alternativeEffectType = CardEffectType.Heal,
    alternativeEffectAmount = 3,
    alternativeLogic = AlternativeEffectLogic.Replace
};
```

## Validation & Debugging

### Card Validation
```csharp
bool isValid = CardGenerator.ValidateCard(myCard);
```

### Detailed Information
```csharp
CardGenerator.PrintCardInfo(myCard);
// Outputs:
// ═══ CARD INFO: Strike ═══
// ID: 3847
// Cost: 2 energy
// Type: Attack
// Category: Starter
// Description: Deal 5 damage to opponent.
// Effects (1):
//   1. Damage - Amount: 5, Target: Opponent, Duration: 0
// Upgrade: TimesPlayedThisFight GreaterThanOrEqual 3
// Upgrades to: Heavy Strike
```

## Pre-Built Card Sets

### Starter Set
```csharp
var starterCards = CardGenerator.GenerateStarterCardSet();
// Creates paired base/upgraded versions of:
// - Strike/Heavy Strike (damage cards)
// - Defend/Strong Defend (shield cards)  
// - Recover/Greater Recover (heal cards)
```

## Card Categories

- **Starter**: Basic cards for starter decks (`CardCategory.Starter`)
- **Draftable**: Cards available in draft packs (`CardCategory.Draftable`) 
- **Upgraded**: Enhanced versions not available in drafts (`CardCategory.Upgraded`)

## Integration with Existing Systems

The CardGenerator creates standard `CardData` ScriptableObjects that work seamlessly with:
- `CardEffectResolver` (processes card effects)
- `CardUpgradeManager` (handles upgrade conditions)
- `CardTracker` (tracks play statistics)
- `HandManager` (manages cards in hand)
- All existing card game systems

## Testing

Use the provided test scripts:
- `CardGeneratorTest.cs` - Comprehensive testing of all features
- `CardGeneratorExample.cs` - Simple usage examples

Both can be run via:
- Component context menu in inspector
- Automatic testing on scene start
- Manual method calls from other scripts

## Tips

1. **Always validate cards** after creation with `ValidateCard()`
2. **Use PrintCardInfo()** for debugging card setup
3. **Set proper categories** for card filtering in game systems  
4. **Test upgrade conditions** match your game's tracking systems
5. **Use the example generators** as templates for custom cards

---

*The CardGenerator is designed to work with your existing CardData architecture and provides a clean, code-based alternative to manually creating ScriptableObject assets.* 