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
- Upgrade Required Value: 3
- Upgrade Comparison Type: GreaterThanOrEqual
- Upgrade All Copies: ✗ false (single copy upgrade)
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
3. Done! The manager automatically detects all upgradeable cards
4. Check inspector to see how many upgradeable cards were found

### 3. How It Works

```
Turn 1: Player plays Strike (1st time) → No upgrade yet
Turn 3: Player plays Strike (2nd time) → No upgrade yet  
Turn 5: Player plays Strike (3rd time) → UPGRADE!
  ↳ One Strike card in deck becomes Strike+ immediately
  ↳ Player now has both Strike and Strike+ cards
```

## Available Condition Types

### Play-Based Conditions
- **TimesPlayedThisFight**: Upgrade after playing X times this fight
- **TimesPlayedAcrossFights**: Upgrade after playing X times total (persistent)
- **PlayedMultipleTimesInTurn**: Upgrade after playing X times in one turn
- **ComboUseBackToBack**: Upgrade after playing back-to-back with same card type
- **OnlyCardPlayedThisTurn**: Upgrade after being the only card played X turns

### Card Interaction Conditions (Per-Fight)
- **DrawnOften**: Upgrade after being drawn X times this fight
- **HeldAtTurnEnd**: Upgrade after being in hand at turn end X times this fight
- **DiscardedManually**: Upgrade after being manually discarded X times this fight
- **FinalCardInHand**: Upgrade after being the last card in hand X times this fight
- **ComboUseBackToBack**: Upgrade after playing back-to-back with same card type X times this fight
- **OnlyCardPlayedThisTurn**: Upgrade after being the only card played X turns this fight

### Card Interaction Conditions (Lifetime)
- **DrawnOftenLifetime**: Upgrade after being drawn X times across all fights
- **HeldAtTurnEndLifetime**: Upgrade after being in hand at turn end X times lifetime
- **DiscardedManuallyLifetime**: Upgrade after being manually discarded X times lifetime
- **FinalCardInHandLifetime**: Upgrade after being the last card in hand X times lifetime
- **ComboUseBackToBackLifetime**: Upgrade after playing back-to-back X times lifetime
- **OnlyCardPlayedInTurnLifetime**: Upgrade after being the only card played X turns lifetime

### Combat Performance Conditions
- **DamageDealtThisFight**: Upgrade after dealing X damage this fight
- **DamageDealtInSingleTurn**: Upgrade after dealing X damage in one turn
- **DamageTakenThisFight**: Upgrade after taking X damage this fight
- **HealingGivenThisFight**: Upgrade after healing X health this fight
- **HealingReceivedThisFight**: Upgrade after receiving X healing this fight
- **LostFightWithCard**: Upgrade after losing a fight (Lose to Win condition)

### Health-Based Conditions
- **PlayedAtLowHealth**: Upgrade when played at 25% health or below
- **PlayedAtHighHealth**: Upgrade when played at 75% health or above
- **PlayedAtHalfHealth**: Upgrade when played at 50% health or below

### Combo & Tactical Conditions
- **ComboCountReached**: Upgrade when combo count reaches X
- **PlayedWithCombo**: Upgrade when played with active combo
- **PlayedInStance**: Upgrade when played in any stance
- **PlayedAsFinisher**: Upgrade when played as a finisher card
- **ZeroCostCardsThisTurn**: Upgrade after playing X zero-cost cards this turn
- **ZeroCostCardsThisFight**: Upgrade after playing X zero-cost cards this fight
- **PerfectTurnPlayed**: Upgrade after a perfect turn (no damage + all energy used)

### Deck Composition Conditions
- **CopiesInDeck**: Upgrade based on number of copies in deck
- **CopiesInHand**: Upgrade based on number of copies in hand  
- **CopiesInDiscard**: Upgrade based on number of copies in discard
- **AllCopiesPlayedFromHand**: Upgrade when all copies played from hand
- **FamiliarNameInDeck**: Upgrade when X cards share keywords/title fragments
- **OnlyCardTypeInDeck**: Upgrade when it's the only card of its type in deck
- **AllCardsCostLowEnough**: Upgrade when all cards cost 1 or less
- **DeckSizeBelow**: Upgrade when deck has fewer than X cards

### Advanced Conditions (Per-Fight)
- **SurvivedStatusEffect**: Upgrade after surviving X status effects this fight
- **BattleLengthOver**: Upgrade after battle lasts longer than X turns
- **PerfectionStreakAchieved**: Upgrade after X turns without taking damage

### Advanced Conditions (Lifetime)
- **TotalFightsWon**: Upgrade after winning X fights total
- **TotalFightsLost**: Upgrade after losing X fights total
- **TotalBattleTurns**: Upgrade after participating in X total battle turns
- **TotalPerfectTurns**: Upgrade after achieving X perfect turns lifetime
- **TotalStatusEffectsSurvived**: Upgrade after surviving X different status effects lifetime
- **WonFightUsingCard**: Upgrade after winning X fights with this card

## Comparison Types

- **GreaterThanOrEqual**: Most common (≥)
- **Equal**: Exact match (=)
- **GreaterThan**: Strictly greater (>)
- **LessThanOrEqual**: At most (≤)
- **LessThan**: Strictly less (<)

## Creative Examples

### Repetition Mastery
```
Card: "Practice Swing" → "Perfect Swing"
Condition: TimesPlayedThisFight ≥ 5
Result: Upgrades after playing 5 times in one fight
```

### Combo Specialist
```
Card: "Chain Lightning" → "Storm Chain"
Condition: ComboUseBackToBack ≥ 3
Result: Upgrades after 3 back-to-back plays with other Lightning spells
```

### Lucky Draw
```
Card: "Fortune Card" → "Blessed Fortune"
Condition: DrawnOften ≥ 4
Result: Upgrades after being drawn 4 times in one fight
```

### Survival Instinct
```
Card: "Desperate Strike" → "Survivor's Fury"
Condition: PlayedAtLowHealth ≥ 1
Result: Upgrades when played once while at low health
```

### Deck Synergy
```
Card: "Venom Dart" → "Toxic Barrage"
Condition: FamiliarNameInDeck ≥ 3
Result: Upgrades when 3+ cards contain "Venom" in their name
```

### Perfect Execution
```
Card: "Focus" → "Perfect Focus"
Condition: PerfectTurnPlayed ≥ 1
Result: Upgrades after one perfect turn (no damage taken + all energy used)
```

### Lose to Win
```
Card: "Revenge" → "Sweet Revenge"
Condition: LostFightWithCard ≥ 1
Result: Upgrades after losing a fight with this card in deck
```

### Solo Performance
```
Card: "Lone Wolf" → "Alpha Wolf"
Condition: OnlyCardTypeInDeck ≥ 1
Result: Upgrades when it's the only Attack card in deck
```

### Battle Endurance
```
Card: "Patience" → "Eternal Patience"
Condition: BattleLengthOver ≥ 15
Result: Upgrades after battle lasts more than 15 turns
```

### Final Stand
```
Card: "Last Hope" → "Miracle"
Condition: FinalCardInHand ≥ 3
Result: Upgrades after being the last card in hand 3 times
```

### Lifetime Mastery
```
Card: "Veteran Strike" → "Master Strike"
Condition: TimesPlayedLifetime ≥ 50
Result: Upgrades after being played 50 times across all fights
```

### Career Survivor
```
Card: "Resilience" → "Legendary Resilience"
Condition: TotalFightsWon ≥ 10
Result: Upgrades after winning 10 fights total
```

### Persistence Pays Off
```
Card: "Stubborn Defense" → "Unyielding Defense"
Condition: HeldAtTurnEndLifetime ≥ 20
Result: Upgrades after being held at turn end 20 times lifetime
```

## Key Benefits

✅ **Simple Setup**: Just configure fields directly on CardData
✅ **No Extra Files**: No separate upgrade ScriptableObjects needed
✅ **Automatic Detection**: Manager finds upgradeable cards automatically
✅ **Rich Conditions**: 40+ different upgrade triggers available (per-fight + lifetime)
✅ **Immediate Feedback**: Upgrades happen instantly when conditions are met
✅ **Flexible Targeting**: Choose single copy or all copies per card

## Tips for Designers

1. **Start Simple**: Use `TimesPlayedThisFight` for your first upgrades
2. **Match Theme**: Choose conditions that fit the card's flavor
3. **Test Thresholds**: Playtest to find engaging but achievable values
4. **Mix Conditions**: Use different upgrade paths for variety
5. **Single Copy Default**: `UpgradeAllCopies` defaults to false for balance
6. **Per-Fight vs Lifetime**: Choose conditions that reset each fight vs persist forever
7. **Lifetime for Mastery**: Use lifetime conditions for long-term progression
8. **Per-Fight for Tactics**: Use per-fight conditions for dynamic gameplay

## Limitations

- **Single Condition**: Each card can only have one upgrade condition
- **No Complex Logic**: Can't combine multiple conditions with AND/OR
- **Immediate Only**: Upgrades always happen immediately (no end-of-turn timing)

For most upgrade use cases, this system provides plenty of flexibility while staying simple to configure! 