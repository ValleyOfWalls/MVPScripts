# Card Upgrade System (DEPRECATED)

⚠️ **This documentation is for the old complex upgrade system. The new simplified system is documented in `SimpleCardUpgradeGuide.md`**

## Overview

This was the old Card Upgrade System that used separate CardUpgradeData files and a database. It has been replaced with a much simpler system that configures upgrades directly in CardData assets.

## Core Components

### 1. CardUpgradeData (ScriptableObject)
Defines the upgrade conditions and target for each card.

**Key Properties:**
- `BaseCard`: The card that can be upgraded
- `UpgradedCard`: The card it upgrades to
- `UpgradeTiming`: When the upgrade occurs (Immediate only)
- `UpgradeConditions`: List of conditions that must be met
- `ConditionLogic`: Whether ALL conditions must be met (AND) or ANY condition (OR)

### 2. CardUpgradeManager (NetworkBehaviour)
Singleton that monitors all upgrade conditions and executes upgrades.

**Key Methods:**
- `OnCardPlayed()`: Called when a card is played (triggers immediate upgrades)
- `OnTurnEnd()`: Called at the end of each turn (resets turn tracking)
- `OnFightEnd()`: Called when a fight ends (resets fight tracking)
- `HasAvailableUpgrades()`: Check if a card has upgrades
- `GetUpgradeProgress()`: Get progress toward upgrade for UI

### 3. CardUpgradeDatabase (MonoBehaviour)
Central repository for all immediate upgrade configurations.

**Key Methods:**
- `GetUpgradesForCard()`: Get all upgrades for a specific card
- `GetAllUpgrades()`: Get all immediate upgrades
- `CreatePlayCountUpgrade()`: Helper to create simple play count upgrades
- `CreatePerformanceUpgrade()`: Helper to create performance-based upgrades

## Upgrade Condition Types

### Card-Specific Tracking
- `TimesPlayedThisFight`: How many times played in current fight
- `TimesPlayedAcrossFights`: Total times played across all fights
- `CopiesInDeck/Hand/Discard`: Number of copies in each zone
- `AllCopiesPlayedFromHand`: All copies from hand have been played

### Combat Performance
- `DamageDealtThisFight/InSingleTurn`: Damage tracking
- `DamageTakenThisFight`: Damage taken tracking
- `HealingGiven/ReceivedThisFight`: Healing tracking
- `PerfectionStreakAchieved`: Turns without taking damage

### Tactical Conditions
- `ComboCountReached`: Combo count threshold
- `PlayedWithCombo`: Card was played with active combo
- `PlayedInStance`: Card was played in specific stance
- `PlayedAsFinisher`: Card was played as a finisher
- `ZeroCostCardsThisTurn/Fight`: Zero-cost card tracking

### Health-Based
- `PlayedAtLowHealth`: Card played when health < 25%
- `PlayedAtHighHealth`: Card played when health > 75%

### Victory Conditions
- `WonFightUsingCard`: Fights won with this card in deck
- `SurvivedFightWithCard`: Fights survived with this card

## Setup Instructions

### 1. Create Upgrade Database
1. Create an empty GameObject in your scene
2. Add the `CardUpgradeDatabase` component to it
3. Name it "CardUpgradeDatabase"
4. Configure immediate upgrade data in the inspector
5. Use the context menu "Auto-Populate Upgrade Database" to automatically find all CardUpgradeData assets

### 2. Create Upgrade Data
1. Right-click in Project → Create → Card Game → Card Upgrade Data
2. Configure the base card, upgraded card, and conditions
3. Add to the database or use the helper methods

### 3. Setup CardUpgradeManager
1. Create an empty GameObject in your scene
2. Add CardUpgradeManager component
3. Set "Use Database" to true (recommended)
4. The manager will automatically load upgrades from the database

### 4. Integration Points
The system automatically integrates with:
- `CardTracker`: Notifies when cards are played
- `EntityTracker`: Notifies on turn end
- `NetworkEntityDeck`: Handles persistent deck upgrades

## Example Upgrade Configurations

### Simple Play Count Upgrade
```csharp
// Lightning Bolt upgrades to Chain Lightning after being played 3 times
var upgrade = CardUpgradeDatabase.Instance.CreatePlayCountUpgrade(
    lightningBolt, chainLightning, 3, thisFightOnly: true);
```

### Performance-Based Upgrade
```csharp
// Healing Potion upgrades to Greater Healing after healing 100 HP total
var upgrade = CardUpgradeDatabase.Instance.CreatePerformanceUpgrade(
    healingPotion, greaterHealing, 
    UpgradeConditionType.HealingGivenThisFight, 100);
```

### Complex Conditional Upgrade
```csharp
// Create upgrade data manually for complex conditions
var upgradeData = CreateInstance<CardUpgradeData>();

// Basic Strike upgrades to Power Strike when:
// - Played 5 times this fight AND dealt 50+ damage this fight
upgradeData.BaseCard = basicStrike;
upgradeData.UpgradedCard = powerStrike;
upgradeData.ConditionLogic = UpgradeConditionLogic.AND;

upgradeData.UpgradeConditions.Add(new CardUpgradeCondition
{
    conditionType = UpgradeConditionType.TimesPlayedThisFight,
    requiredValue = 5,
    comparisonType = UpgradeComparisonType.GreaterThanOrEqual
});

upgradeData.UpgradeConditions.Add(new CardUpgradeCondition
{
    conditionType = UpgradeConditionType.DamageDealtThisFight,
    requiredValue = 50,
    comparisonType = UpgradeComparisonType.GreaterThanOrEqual
});
```

## Upgrade Timing

### Immediate Only
- Upgrades happen as soon as conditions are met during card play
- Only timing type supported for simplicity
- Provides immediate feedback to players

## Contextual Requirements

Upgrade conditions can have additional requirements:

### Stance Requirement
```csharp
condition.requiresSpecificStance = true;
condition.requiredStance = StanceType.Aggressive;
```

### Health Range Requirement
```csharp
condition.requiresHealthRange = true;
condition.minHealthPercent = 0;
condition.maxHealthPercent = 25; // Only when health is 0-25%
```

### Single Turn Requirement
```csharp
condition.mustOccurInSingleTurn = true; // Condition must be met within one turn
```

## Debugging and Monitoring

### Debug Information
The CardUpgradeManager shows debug info in the inspector:
- Pending upgrades
- Completed upgrades
- Persistent tracking data

### Upgrade Progress
Use `GetUpgradeProgress()` to show players their progress toward upgrades:
```csharp
string progress = CardUpgradeManager.Instance.GetUpgradeProgress(cardId, entity);
// Returns: "3/5 (TimesPlayedThisFight)"
```

### Validation
The database includes validation tools:
- Right-click database → "Validate All Upgrades"
- Right-click database → "Clean Invalid Upgrades"

## Best Practices

1. **Use the Database**: Store upgrades in CardUpgradeDatabase rather than manual lists
2. **Validate Early**: Use the validation tools to catch configuration errors
3. **Test Conditions**: Verify upgrade conditions work as expected in play
4. **Consider Timing**: Choose appropriate upgrade timing for the desired effect
5. **Balance Carefully**: Upgrades should feel rewarding but not overpowered
6. **Document Upgrades**: Use clear naming and descriptions for upgrade data

## Extending the System

### Adding New Condition Types
1. Add new enum value to `UpgradeConditionType`
2. Add case to `GetConditionValue()` in CardUpgradeManager
3. Add any needed tracking to CardTracker or EntityTracker

### Custom Upgrade Logic
The system is designed to be extensible. You can:
- Add new timing types
- Add new comparison types
- Add custom validation logic
- Add custom upgrade effects

## Performance Considerations

- Upgrade checking only happens when cards are played
- Persistent tracking uses network-synchronized dictionaries
- Database lookups are cached where possible
- Invalid upgrades are filtered out during validation

## Troubleshooting

### Upgrades Not Triggering
1. Check that CardUpgradeManager is in the scene
2. Verify upgrade conditions are properly configured
3. Ensure CardTracker is calling the upgrade manager
4. Check debug logs for condition evaluation

### Database Not Loading
1. Ensure database is named "CardUpgradeDatabase"
2. Place database in Resources folder
3. Check for null reference errors in console

### Persistent Tracking Issues
1. Verify CardUpgradeManager has network authority
2. Check that persistent tracking is enabled for relevant conditions
3. Ensure proper server/client synchronization 