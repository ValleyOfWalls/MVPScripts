# Combat Math Testing System

## Overview
This system provides comprehensive testing for combat damage calculations with status effects. It validates the interaction between different effects like Weak, Break, Strength, Curse, and Armor.

## Usage

### Editor Window (Primary Method)
1. Open Unity and enter Play Mode
2. Go to `Tools > Test Combat > Combat Math Test Window`
3. Click "Run Basic Combat Math Tests (13 tests)" for standard validation
4. Click "Run Extended Combat Math Tests (35+ tests)" for comprehensive validation
5. View results in the window and check console for detailed logs

## Test Categories

### Basic Tests (13 tests)
- **Individual Effects**: Weak, Break, Strength, Curse, Armor
- **Common Combinations**: Weak+Break, Strength+Break, Curse+Weak, Armor+Break
- **Edge Cases**: Zero damage, high armor vs low damage, extreme values

### Extended Tests (35+ tests)
- **Complex Combinations**: Triple effects, all effects combined, extreme values
- **Boundary Conditions**: Exact equality scenarios, maximum/minimum damage
- **Rounding Behavior**: Fractional results and banker's rounding validation
- **Effect Stacking**: Different potency values for each effect type

## Damage Calculation Order

The system applies effects in this specific order:

### Source Modifiers (Attacker)
1. **Curse Effects**: Flat damage reduction (e.g., -3 damage)
2. **Strength Effects**: Flat damage bonus (e.g., +5 damage)  
3. **Damage Clamping**: Ensure damage ≥ 0 after flat modifiers
4. **Weak Effects**: Percentage reduction (×0.75 for Weak)

### Target Modifiers (Defender)
1. **Break Effects**: Percentage increase to incoming damage (×1.5 for Break)
2. **Armor Effects**: Flat damage reduction (minimum 1 damage)

### Final Step
- **Rounding**: Unity's banker's rounding (0.5 rounds to nearest even)

## Expected Calculations

### Individual Effects
- **Weak**: 10 × 0.75 = 7.5 → 8 (banker's rounding)
- **Break**: 10 × 1.5 = 15
- **Strength +5**: (10 + 5) = 15
- **Curse -3**: (10 - 3) = 7
- **Armor 4**: max(1, 10 - 4) = 6

### Complex Combinations
- **Strength + Curse + Weak**: (20 - 4 + 8) × 0.75 = 24 × 0.75 = 18
- **All Effects**: (20 + 6) × 0.75 × 1.5 - 3 = 19.5 × 1.5 - 3 = 26.25 → 26

### Edge Cases
- **Extreme Curse**: max(0, 5 - 10) = 0 (damage cannot go negative)
- **High Armor**: max(1, 5 - 10) = 1 (damage cannot go below 1 from armor)

## Banker's Rounding Examples
- 7.5 → 8 (rounds to nearest even)
- 8.5 → 8 (rounds to nearest even)  
- 22.5 → 22 (rounds to nearest even)
- 23.5 → 24 (rounds to nearest even)

## Troubleshooting

### Common Issues
1. **Tests only work in Play Mode** - Enter Play Mode before running tests
2. **No entities found** - Ensure you're in a scene with NetworkEntity objects that have EffectHandler components
3. **Unexpected results** - Check console logs for detailed calculation steps

### Debug Information
- All tests log their results to the Unity Console
- Failed tests show expected vs actual values
- Detailed calculation steps are logged for debugging

### Status Effect Requirements
- **Weak/Break**: Managed by EffectHandler, temporary effects
- **Strength**: Synced with EntityTracker.StrengthStacks
- **Curse**: Managed by EffectHandler as damage modification
- **Armor**: Managed by EffectHandler, temporary effect

## Test Results Interpretation

### Success Indicators
- All tests show "PASS" status
- Console shows "X/X tests passed, 0 failed"
- No failure reasons listed

### Failure Analysis
- Check "Failure" field in test results
- Compare expected vs actual damage values
- Review console logs for calculation steps
- Verify effect application order

## Integration Notes

This testing system integrates with:
- **DamageCalculator**: Core damage calculation logic
- **EffectHandler**: Status effect management
- **EntityTracker**: Strength stack tracking
- **GameManager**: Weak effect modifier value

The tests validate the entire damage calculation pipeline to ensure combat math works correctly across all scenarios. 