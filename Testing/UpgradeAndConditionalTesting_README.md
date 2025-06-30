# Upgrade Condition & Conditional Effect Testing System

This document describes the comprehensive testing system for card upgrade conditions and conditional effects, following the same methodology as the combat math testing system.

## Overview

The testing system provides three testing approaches:
1. **Automated Testing**: Component-based testing with predefined scenarios
2. **Interactive Testing**: Editor window for custom scenarios during development  
3. **Programmatic Testing**: API for integration with other test systems

## System Components

### 1. UpgradeConditionTestManager.cs
**Purpose**: Automated testing manager for card upgrade conditions  
**Location**: `Testing/UpgradeConditionTestManager.cs`  
**Usage**: Attach to GameObject in combat scene, configure and run tests

**Features**:
- Tests all upgrade condition types (TimesPlayedThisFight, DamageDealtThisFight, etc.)
- Tests all comparison types (GreaterThanOrEqual, Equal, LessThan, etc.)
- Health-based conditions (PlayedAtLowHealth, PlayedAtHighHealth, PlayedAtHalfHealth)
- Tactical conditions (ComboCountReached, ZeroCostCardsThisTurn, PlayedInStance)
- Comprehensive logging with TestLogger integration

**Test Scenarios**: 15+ predefined scenarios covering:
- Card-specific tracking (times played, copies in hand/deck/discard)
- Combat performance (damage dealt/taken, healing given/received)
- Health-based triggers (low/high/half health thresholds)
- Combo and tactical gameplay (combo count, zero cost cards, stance)
- All comparison operator types

### 2. ConditionalEffectTestManager.cs
**Purpose**: Automated testing manager for conditional card effects  
**Location**: `Testing/ConditionalEffectTestManager.cs`  
**Usage**: Attach to GameObject in combat scene, configure and run tests

**Features**:
- Tests all conditional statement types (IfSourceHealthBelow, IfTargetHealthAbove, etc.)
- Tests alternative effect logic (Replace vs Additional)
- Health-based conditionals with dynamic entity state setup
- Tracking-based conditionals (damage taken, healing received, etc.)
- Tactical conditionals (combo count, perfection streak, stance)

**Test Scenarios**: 18+ predefined scenarios covering:
- Health conditions (source/target health above/below thresholds)
- Combat tracking (damage taken this fight/last round, healing received)
- Tactical gameplay (combo count, perfection streak, zero cost cards)
- Stance and energy conditions
- Alternative effect logic testing (Replace vs Additional)

### 3. UpgradeConditionIntegration.cs
**Purpose**: Static utility class for programmatic upgrade condition testing  
**Location**: `Testing/UpgradeConditionIntegration.cs`  
**Usage**: Call static methods from other scripts or console

**Key Methods**:
```csharp
// Test specific upgrade condition
bool TestUpgradeCondition(UpgradeConditionType conditionType, int requiredValue, 
    UpgradeComparisonType comparisonType, int actualValue)

// Run comprehensive test suite
void RunUpgradeConditionTests()

// Test health-based conditions
void TestHealthBasedConditions()

// Test stance-based conditions  
void TestStanceBasedConditions()
```

### 4. ConditionalEffectIntegration.cs
**Purpose**: Static utility class for programmatic conditional effect testing  
**Location**: `Testing/ConditionalEffectIntegration.cs`  
**Usage**: Call static methods from other scripts or console

**Key Methods**:
```csharp
// Test specific conditional effect
bool TestConditionalEffect(ConditionalType conditionType, int conditionValue, 
    int actualValue, bool useEntityValue = false)

// Run comprehensive test suite
void RunConditionalEffectTests()

// Test alternative effect logic
void TestAlternativeEffectLogic()

// Test card location conditions
void TestCardLocationConditions()
```

### 5. UpgradeAndConditionalTestWindow.cs
**Purpose**: Interactive Unity Editor window for real-time testing  
**Location**: `Editor/UpgradeAndConditionalTestWindow.cs`  
**Access**: `Tools > Test Combat > Upgrade & Conditional Test Window`

**Features**:
- Custom upgrade condition testing with sliders and dropdowns
- Custom conditional effect testing with configurable parameters
- Real-time test execution during Play Mode
- Visual pass/fail indicators with detailed results
- Integration with component-based test managers

## Usage Instructions

### Method 1: Automated Component Testing

1. **Setup**:
   - Enter Play Mode in combat scene
   - Add `UpgradeConditionTestManager` to a GameObject
   - Add `ConditionalEffectTestManager` to a GameObject
   - Configure test settings in inspector

2. **Run Tests**:
   - Enable "Run Tests On Start" for automatic testing
   - Or call `RunTests()` method manually
   - Monitor console for detailed results

3. **Results**:
   - Check inspector for pass/fail counts
   - Review console logs for detailed test information
   - Failed tests will show expected vs actual values

### Method 2: Interactive Editor Window

1. **Access**: `Tools > Test Combat > Upgrade & Conditional Test Window`

2. **Custom Testing**:
   - Select upgrade condition type and comparison operator
   - Set required and actual values
   - Click "Test Upgrade Condition" for immediate results
   - Repeat for conditional effects

3. **Predefined Tests**:
   - Click "Run All Upgrade Tests" for comprehensive upgrade testing
   - Click "Run All Conditional Tests" for comprehensive conditional testing
   - Click "Run Complete Test Suite" for everything

### Method 3: Programmatic API

```csharp
// Test specific upgrade conditions
bool result1 = UpgradeConditionIntegration.TestUpgradeCondition(
    UpgradeConditionType.TimesPlayedThisFight, 3, 
    UpgradeComparisonType.GreaterThanOrEqual, 5);

// Test specific conditional effects  
bool result2 = ConditionalEffectIntegration.TestConditionalEffect(
    ConditionalType.IfSourceHealthBelow, 50, 30);

// Run complete test suites
UpgradeConditionIntegration.RunUpgradeConditionTests();
ConditionalEffectIntegration.RunConditionalEffectTests();
```

## Upgrade Condition Types Tested

### Card-Specific Tracking
- `TimesPlayedThisFight` - How many times card was played this fight
- `TimesPlayedAcrossFights` - Persistent play count across fights
- `CopiesInDeck` - Number of copies in deck
- `CopiesInHand` - Number of copies in hand  
- `CopiesInDiscard` - Number of copies in discard pile

### Combat Performance
- `DamageDealtThisFight` - Total damage dealt this fight
- `DamageDealtInSingleTurn` - Damage dealt in one turn
- `DamageTakenThisFight` - Total damage taken this fight
- `HealingGivenThisFight` - Total healing given this fight
- `HealingReceivedThisFight` - Total healing received this fight

### Health-Based Conditions
- `PlayedAtLowHealth` - Played when health ≤ 25%
- `PlayedAtHighHealth` - Played when health ≥ 75%
- `PlayedAtHalfHealth` - Played when health ≤ 50%

### Tactical Conditions
- `ComboCountReached` - Combo count threshold reached
- `ZeroCostCardsThisTurn` - Zero cost cards played this turn
- `ZeroCostCardsThisFight` - Zero cost cards played this fight
- `PlayedInStance` - Played while in specific stance

### Comparison Types
- `GreaterThanOrEqual` - Current ≥ Required
- `Equal` - Current == Required
- `LessThanOrEqual` - Current ≤ Required
- `GreaterThan` - Current > Required
- `LessThan` - Current < Required

## Conditional Effect Types Tested

### Health Conditions
- `IfSourceHealthBelow` - Source health < threshold
- `IfSourceHealthAbove` - Source health > threshold
- `IfTargetHealthBelow` - Target health < threshold
- `IfTargetHealthAbove` - Target health > threshold

### Combat Tracking
- `IfDamageTakenThisFight` - Damage taken ≥ threshold
- `IfDamageTakenLastRound` - Last round damage ≥ threshold
- `IfHealingReceivedThisFight` - Healing received ≥ threshold
- `IfHealingReceivedLastRound` - Last round healing ≥ threshold
- `IfTimesPlayedThisFight` - Play count ≥ threshold

### Tactical Conditions
- `IfComboCount` - Combo count ≥ threshold
- `IfPerfectionStreak` - Perfection streak ≥ threshold
- `IfZeroCostCardsThisTurn` - Zero cost cards ≥ threshold
- `IfZeroCostCardsThisFight` - Total zero cost cards ≥ threshold
- `IfInStance` - Currently in specific stance
- `IfEnergyRemaining` - Energy remaining ≥ threshold

### Card Location Conditions
- `IfCardsInHand` - Hand size ≥ threshold
- `IfCardsInDeck` - Deck size ≥ threshold
- `IfCardsInDiscard` - Discard size ≥ threshold

### Alternative Effect Logic
- `Replace` - Use alternative effect instead of main effect when condition not met
- `Additional` - Use both main and alternative effects when condition met

## Expected Test Results

### Upgrade Conditions
- **Low Health (20/100)**: Should trigger `PlayedAtLowHealth` (≤25%)
- **High Health (80/100)**: Should trigger `PlayedAtHighHealth` (≥75%)  
- **Half Health (40/100)**: Should trigger `PlayedAtHalfHealth` (≤50%)
- **Damage Dealt (60 vs 50)**: Should pass `GreaterThanOrEqual` comparison
- **Times Played (3 vs 5)**: Should fail `GreaterThanOrEqual` comparison
- **Combo Count (4 vs 3)**: Should pass `GreaterThanOrEqual` comparison

### Conditional Effects
- **Source Health Below (30 vs 50)**: Should return `true` (30 < 50)
- **Target Health Above (80 vs 60)**: Should return `true` (80 > 60)
- **Damage Taken (30 vs 20)**: Should return `true` (30 ≥ 20)
- **Combo Count (5 vs 3)**: Should return `true` (5 ≥ 3)
- **In Stance (Aggressive vs Aggressive)**: Should return `true` (exact match)
- **Energy Remaining (4 vs 2)**: Should return `true` (4 ≥ 2)

## Troubleshooting

### Common Issues

1. **"Tests require Play Mode"**
   - Solution: Enter Play Mode before running tests
   - Reason: Tests need access to game systems and components

2. **"TestManager not found in scene"**
   - Solution: Add UpgradeConditionTestManager and/or ConditionalEffectTestManager to GameObjects
   - Reason: Component-based tests require managers in scene

3. **"No NetworkEntity found for testing"**
   - Solution: Ensure combat scene has NetworkEntity components
   - Reason: Tests need entities to simulate upgrade/conditional scenarios

4. **Tests showing unexpected results**
   - Check console for detailed logging
   - Verify entity state setup in test scenarios
   - Ensure EntityTracker components are present and functional

### Debug Information

Enable detailed logging by setting `logDetailedResults = true` in test managers. This provides:
- Step-by-step test execution
- Entity state before/after setup
- Condition evaluation details
- Expected vs actual value comparisons

## Integration with Existing Systems

This testing system integrates with:
- **CardUpgradeManager**: Tests actual upgrade condition evaluation logic
- **CardEffectResolver**: Tests actual conditional effect processing
- **EntityTracker**: Uses real tracking data for condition evaluation
- **TestLogger**: Consistent logging with combat math testing system

## Future Enhancements

Potential additions:
- Persistent upgrade condition testing (across multiple fights)
- Complex conditional combinations (AND/OR logic)
- Performance testing for large numbers of conditions
- Automated regression testing integration
- Visual effect validation for conditional triggers 