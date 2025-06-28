# Test Deck Usage Guide

## Overview
The CombatTestManager now supports both individual test cards and deck-based testing approaches. This guide explains how to use test decks for organized combat testing.

## Setup

### 1. Generate Test Cards
First, generate test cards using the Test Card Generator:
- In Unity Editor: `Tools > Card Generator > Test Card Generator`
- Click "Generate All Test Cards"

### 2. Generate Test Decks  
Generate organized test decks using the Test Deck Generator:
- In Unity Editor: `Tools > Card Generator > Test Deck Generator`
- Choose your options:
  - **Master Test Deck**: Contains all generated test cards
  - **Categorized Decks**: Separate decks by effect type (Damage, Healing, Buffs, etc.)
  - **Small Test Deck**: Core effects only for quick validation
- Click "Generate Test Decks"

Generated decks are saved to: `Assets/MVPScripts/Testing/GeneratedTestDecks/`

### 3. Configure CombatTestManager
In the CombatTestManager inspector:
- Enable "Use Test Deck" checkbox
- Assign your desired test deck to the "Test Deck" field
- The system will now use cards from the deck instead of individual cards

## Available Test Decks

### Master Test Deck
- Contains ALL generated test cards
- Best for comprehensive testing
- Tests every possible card effect and interaction

### Categorized Decks
Organized by effect type for focused testing:
- **Damage Test Deck**: Direct damage effects
- **Healing Test Deck**: Healing and restoration effects  
- **Resource Test Deck**: Energy and card draw effects
- **Buffs Test Deck**: Positive status effects (Shield, Thorns, etc.)
- **Debuffs Test Deck**: Negative status effects (Break, Weak, Stun)
- **Status Effects Test Deck**: DoT effects (Burn, Curse, Strength)
- **Elemental Test Deck**: Elemental status effects
- **Stance Test Deck**: Stance-related effects

### Small Test Deck
- Contains only core effects: Damage, Heal, Shield, Burn, Draw Card, Restore Energy
- Perfect for quick smoke tests or CI/CD pipelines
- Faster execution than full test suite

## Usage in Combat

### UI Buttons
The Combat Canvas includes these testing buttons:
- **Test Player Perspective**: Run tests from player's viewpoint
- **Test Opponent Perspective**: Run tests from opponent pet's viewpoint  
- **Stop Tests**: Cancel running tests
- **Generate Test Cards**: Open Test Card Generator (Editor only)
- **Generate Test Decks**: Open Test Deck Generator (Editor only)

### Test Execution Flow
1. CombatTestManager loads cards from assigned test deck
2. For each card in deck order:
   - Capture entity states before card play
   - Clear hands and spawn test card
   - Determine appropriate target based on card type
   - Play card through HandleCardPlay system
   - Wait for effect processing
   - Capture entity states after card play
   - Log state changes and validate effects
   - Reset to default state
   - Continue to next card

## Customizing Test Decks

### Manual Deck Creation
You can manually create test decks:
1. Create new DeckData asset: `Right-click > Create > Card System > Deck Data`
2. Name it with "TESTDECK_" prefix for organization
3. Add desired test cards to the deck
4. Assign to CombatTestManager

### Deck Filtering
Modify `TestDeckGenerator.cs` to create custom filtered decks:
- Add new categorization logic in `GetCardCategory()`
- Create specialized decks in `CreateCategorizedDecks()`
- Filter by card properties, costs, targeting, etc.

## Benefits of Deck-Based Testing

### Organization
- Logical grouping of related test cases
- Easy to create focused test suites
- Clear separation of test scenarios

### Execution Control
- Run specific subset of tests
- Deterministic test order based on deck composition
- Easy to reproduce specific test sequences

### Maintenance
- Update test scope by modifying deck contents
- No need to regenerate all test cards for subset testing
- Version control friendly (deck assets track changes)

### Performance  
- Small test decks for quick validation
- Large test decks for comprehensive coverage
- Choose appropriate scope for testing context

## Best Practices

### Test Deck Naming
- Use descriptive names: "Core_Combat_Effects", "Edge_Case_Tests"
- Include version or date for tracking: "Combat_Tests_v1.2"
- Use consistent prefixes: "TESTDECK_" for automated tools

### Test Organization
- Start with Small Test Deck for basic validation
- Use categorized decks for focused debugging  
- Run Master Test Deck for release validation
- Create custom decks for specific bug reproduction

### CI/CD Integration
- Use Small Test Deck for fast feedback in pull requests
- Use Master Test Deck in nightly builds
- Create specific regression test decks for known issues

## Troubleshooting

### "No cards in test deck"
- Ensure deck is properly assigned in CombatTestManager
- Check that deck contains cards (not empty)
- Verify "Use Test Deck" is enabled

### "Test deck not found"
- Regenerate test decks using Test Deck Generator
- Check deck exists in GeneratedTestDecks folder
- Ensure deck asset is not corrupted

### "Cards not playing correctly"
- Verify test cards were generated properly
- Check card effects are valid
- Ensure proper targeting setup in test manager 