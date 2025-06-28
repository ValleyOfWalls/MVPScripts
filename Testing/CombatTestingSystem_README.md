# Combat Testing System

A comprehensive automated testing framework for validating all card effects, mechanics, and interactions in the combat system.

## Overview

This system provides:
- **Automated card testing** covering every possible mechanic
- **State capture and restoration** for consistent test conditions
- **Before/after state comparison** to validate effects
- **Perspective testing** from both player and opponent viewpoints
- **Manageable logging** for analysis
- **UI integration** for easy test execution

## Components

### 1. TestCardGenerator (Editor Tool)
- **Location**: `Editor/TestCardGenerator.cs`
- **Purpose**: Generates comprehensive test cards covering all mechanics
- **Access**: `Tools > Card Generator > Test Card Generator`

### 2. CombatTestManager
- **Location**: `Testing/CombatTestManager.cs`
- **Purpose**: Orchestrates test execution, state management, and validation
- **Attach to**: A GameObject in the combat scene

### 3. EntityStateCapture
- **Location**: `Testing/EntityStateCapture.cs`
- **Purpose**: Captures and restores entity states (health, energy, effects)
- **Usage**: Static utility class, no setup required

### 4. TestLogger
- **Location**: `Testing/TestLogger.cs`
- **Purpose**: Manages test output with size limits for analysis
- **Usage**: Static utility class, no setup required

### 5. UI Integration
- **Location**: Modified `Combat/CombatCanvasManager.cs`
- **Purpose**: Adds test buttons to combat UI

## Setup Instructions

### 1. Generate Test Cards
1. Open Unity Editor
2. Go to `Tools > Card Generator > Test Card Generator`
3. Configure generation options:
   - ✅ Basic Effects (Damage, Heal, etc.)
   - ✅ Status Effects (Break, Burn, Shield, etc.)
   - ✅ Conditional Effects (If health below X, etc.)
   - ✅ Scaling Effects (Damage scales with combo, etc.)
   - ✅ Combo Effects (Build/require combo)
   - ✅ Stance Effects (Enter different stances)
   - ✅ Targeting Variations (Multi-target, random, etc.)
4. Click "Generate All Test Cards"
5. Test cards will be created in `Assets/MVPScripts/Testing/GeneratedTestCards/`

### 2. Add CombatTestManager to Scene
1. In your combat scene, create an empty GameObject
2. Name it "CombatTestManager"
3. Add the `CombatTestManager` component
4. Configure settings:
   - **Enable Test Mode**: Check this to enable testing
   - **Card Play Delay**: Time to wait for effects to process (1.0s recommended)
   - **State Reset Delay**: Time between state reset and next test (0.5s recommended)

### 3. Add Test Buttons to Combat UI
The UI has been automatically integrated. You need to:
1. Open your Combat Canvas prefab/scene
2. Find the CombatCanvasManager component
3. Assign the new button fields in the "Testing Controls" section:
   - **Test Player Perspective Button**: Button to test from player's perspective
   - **Test Opponent Perspective Button**: Button to test from opponent pet's perspective
   - **Stop Tests Button**: Button to halt running tests
   - **Generate Test Cards Button**: Button to open card generator (Editor only)

### 4. Create Test Buttons (If Needed)
If you don't have test buttons yet:
1. Add 4 new UI Buttons to your combat canvas
2. Position them appropriately (suggest bottom-right corner)
3. Label them:
   - "Test Player"
   - "Test Opponent"
   - "Stop Tests"
   - "Generate Cards" (will be hidden in builds)
4. Assign these buttons to the CombatCanvasManager fields

## Usage

### Running Tests

1. **Start Combat**: Enter a combat scenario with all entities present
2. **Choose Perspective**: Click either "Test Player" or "Test Opponent" button
3. **Watch Execution**: Tests run automatically, showing progress in console
4. **View Results**: Check console for detailed logs and summaries

### Test Flow (Per Card)

1. **Capture Default State**: Records health, energy, and effects of all entities
2. **Clear Hands**: Removes existing cards to avoid interference
3. **Generate Test Card**: Creates card in caster's hand with sufficient energy
4. **Determine Target**: Finds appropriate target based on card type
5. **Capture Before State**: Records entity states before card play
6. **Play Card**: Executes card effect through normal game systems
7. **Wait for Processing**: Allows time for effects to complete
8. **Capture After State**: Records entity states after effects
9. **Compare States**: Logs all changes (health, energy, effects)
10. **Validate Result**: Checks if expected effect occurred
11. **Reset to Default**: Restores all entities to starting state
12. **Repeat**: Continues with next test card

### Reading Test Results

Test logs include:
```
=== Player: Test Basic Damage ===
  Captured before states
  Target determined: Opponent Pet
  [SUCCESS] Player played 'Test Basic Damage' on Opponent Pet
  Player: No changes
  Opponent Pet: Health: 100 → 95
  Local Player Pet: No changes
  Opponent Player: No changes
  Result: PASS

Test Summary: 45/50 passed, 5 failed
```

### Log Output Management

- **Size Limits**: Logs are automatically capped to prevent overwhelming output
- **Summary View**: `TestLogger.GetSummary()` shows pass/fail counts
- **Failed Tests Only**: `TestLogger.GetFailedTests()` shows only failures
- **Full Results**: `TestLogger.GetAllResults()` shows complete test log

## Advanced Features

### Custom Test Cards
Add custom test cards by:
1. Creating CardData assets manually
2. Adding them to CombatTestManager's "Test Cards" list
3. Or calling `testManager.AddTestCard(cardData)` in code

### Test Validation
The system validates effects by:
- **Damage**: Target health decreases
- **Heal**: Target health increases  
- **Energy**: Target energy changes appropriately
- **Status Effects**: Effects are added to target
- **Custom**: Override `ValidateCardEffect()` for complex validation

### Debugging Failed Tests
When tests fail:
1. Check the detailed logs for the specific card
2. Look at before/after state comparisons
3. Verify card data is correctly configured
4. Check if the card effect is properly implemented

## Troubleshooting

### Common Issues

**Test Manager Not Found**
- Ensure CombatTestManager is added to scene
- Check that it's a NetworkBehaviour on a NetworkObject

**No Test Cards Generated**
- Verify TestCardGenerator ran without errors
- Check `Assets/MVPScripts/Testing/GeneratedTestCards/` folder
- Ensure CardData template is available

**Tests Not Running**
- Confirm "Enable Test Mode" is checked
- Verify all combat entities are present
- Check console for initialization errors

**State Reset Issues**
- Increase state reset delay
- Verify EntityStateCapture is working correctly
- Check that all entities have required components

### Performance Considerations

- Tests run sequentially to avoid interference
- Large test suites may take several minutes
- Consider running smaller subsets for quick validation
- Use async execution to avoid blocking the main thread

## Extension Points

### Adding New Card Effects
1. Generate test cards using TestCardGenerator
2. Update `ValidateCardEffect()` for specific validation
3. Add new effect types to test generation

### Custom Test Scenarios
1. Create specific test cards for edge cases
2. Override test flow in `RunSingleCardTest()`
3. Add custom validation logic

### Integration with CI/CD
1. Use command line arguments to trigger tests
2. Export results to files for automated analysis
3. Set up automated test runs on builds

## File Structure
```
Testing/
├── CombatTestManager.cs           # Main test orchestrator
├── EntityStateCapture.cs          # State capture/restore utility
├── TestLogger.cs                  # Log management
├── CombatTestingSystem_README.md  # This guide
└── GeneratedTestCards/            # Auto-generated test cards
    ├── TEST_Basic_Damage.asset
    ├── TEST_Status_Burn.asset
    └── ... (more test cards)

Editor/
└── TestCardGenerator.cs           # Test card generation tool
```

This system provides comprehensive coverage of your card game's mechanics while maintaining manageable output for analysis and debugging. 