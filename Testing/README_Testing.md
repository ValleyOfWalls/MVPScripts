# Card System Testing Tools

This directory contains comprehensive testing tools for the Enhanced Card System. These tools allow you to create test cards for every mechanic and test them thoroughly in the Unity Editor.

## Quick Start

1. **Create Test Cards**: Go to `Tools > Card Factory > Create All Test Cards` in the Unity Editor
2. **Add TestCombat to Scene**: Create an empty GameObject and add the `TestCombat` component
3. **Open Card Spawner**: Go to `Tools > Test Combat > Open Card Spawner Window`
4. **Start Testing**: Play the game and use the spawner to add cards to your hand

## CardFactory

The `CardFactory` class generates comprehensive test cards for every feature in the enhanced card system.

### Features Tested

#### Basic Effects
- **Test_Damage_Basic**: Simple damage card
- **Test_Heal_Basic**: Simple healing card  
- **Test_Draw_Cards**: Card drawing
- **Test_Energy_Restore**: Energy restoration
- **Test_Zero_Cost**: Zero-cost card for scaling tests

#### Status Effects
- **Test_Break_Status**: Reduce target armor
- **Test_Weak_Status**: Reduce target damage
- **Test_DOT**: Damage over time (poison)
- **Test_HOT**: Healing over time (regeneration)
- **Test_Crit_Boost**: Increase critical chance
- **Test_Thorns**: Reflect damage back to attacker
- **Test_Shield**: Absorb incoming damage
- **Test_Stun**: Skip target's next turn
- **Test_Limit_Break**: Enter enhanced state
- **Test_Strength**: Permanent damage increase
- **Test_Discard_Random**: Force random card discard

#### Target Variations
- **Test_Target_Self**: Self-targeting
- **Test_Target_Opponent**: Opponent targeting
- **Test_Target_Ally**: Ally targeting
- **Test_Target_Random**: Random targeting
- **Test_Target_All**: All entities
- **Test_Target_AllAllies**: All allies
- **Test_Target_AllEnemies**: All enemies
- **Test_Flexible_Targeting**: Multiple target options

#### Zone Effects (Global)
- **Test_Zone_Damage_All**: Damage everyone globally
- **Test_Zone_Heal_All**: Heal all players globally
- **Test_Zone_Energy_All**: Restore energy to all pets
- **Test_Zone_Draw_All**: All entities draw cards

#### Scaling Effects
- **Test_Scale_ZeroCost_Turn**: Scales with zero-cost cards this turn
- **Test_Scale_ZeroCost_Fight**: Scales with zero-cost cards this fight
- **Test_Scale_CardsPlayed_Turn**: Scales with cards played this turn
- **Test_Scale_CardsPlayed_Fight**: Scales with total cards played
- **Test_Scale_Damage_Dealt**: Scales with damage dealt
- **Test_Scale_Combo**: Scales with combo count
- **Test_Scale_Missing_Health**: Scales with missing health

#### Conditional Effects
- **Test_Conditional_Health**: Execute effect if target health below threshold
- **Test_Conditional_Hand**: Bonus effect if enough cards in hand
- **Test_Conditional_Perfection**: Bonus for perfect damage streak
- **Test_Conditional_Combo**: Effect if combo count meets requirement
- **Test_Conditional_Energy**: Bonus if enough energy remaining

#### Multi-Effects
- **Test_Multi_Damage_Heal**: Damage enemy and heal self
- **Test_Multi_Damage_Status**: Damage and apply status
- **Test_Multi_Triple**: Three different effects
- **Test_Multi_Area**: Different effects on different targets

#### Stance System
- **Test_Stance_Aggressive**: +2 damage, -1 defense
- **Test_Stance_Defensive**: +2 defense, +2 shield
- **Test_Stance_Focused**: +1 energy, +1 draw
- **Test_Stance_Guardian**: +3 shield, +1 thorns
- **Test_Stance_Mystic**: Enhances elemental effects
- **Test_Stance_Exit**: Exit current stance

#### Persistent Effects
- **Test_Persistent_Damage_Aura**: Deal damage each turn
- **Test_Persistent_Heal_Aura**: Heal each turn for limited duration
- **Test_Persistent_Energy_Regen**: Restore energy each turn
- **Test_Persistent_Draw_Bonus**: Draw extra card each turn
- **Test_Persistent_Stance_Dependent**: Effect only while in specific stance

#### Card Sequencing
- **Test_Attack_Basic**: Basic attack card
- **Test_Combo_Builder**: Builds combo count
- **Test_Finisher_Combo**: Requires combo to play
- **Test_Skill_Basic**: Basic skill card
- **Test_Spell_Basic**: Basic spell card
- **Test_Counter_Attack**: Requires attack played immediately before
- **Test_Reaction_Skill**: Requires any skill played this turn

#### Elemental Effects
- **Test_Fire_Damage**: Fire damage with burn status
- **Test_Ice_Damage**: Ice damage with slow status
- **Test_Lightning_Damage**: Lightning with chain effect to all enemies
- **Test_Void_Damage**: Void damage with corruption

#### Complex Combinations
- **Test_Complex_Scaling_Conditional**: Scaling + conditional + multi-effect
- **Test_Complex_Zone_Persistent**: Zone + persistent + stance
- **Test_Complex_Elemental_Scaling**: Elemental + scaling + multi-target

### Using CardFactory

```csharp
// In Unity Editor menu:
Tools > Card Factory > Create All Test Cards    // Creates all test cards
Tools > Card Factory > Clear Test Cards         // Removes all test cards
```

Test cards are created in `Assets/CardData/TestCards/` and automatically added to the project.

## TestCombat

The `TestCombat` class provides runtime testing utilities and debug commands.

### Features

#### Menu Commands
- **Open Card Spawner Window**: Browse and spawn any card from the database
- **Toggle Return To Hand Mode**: Cards return to hand instead of discarding
- **Spawn Random Test Cards**: Add random test cards to player hand
- **Clear Player Hand**: Remove all cards from player hand
- **Reset All Entity Trackers**: Reset combat statistics
- **Log All Combat State**: Comprehensive state debugging

#### Return to Hand Mode
When enabled, played cards will:
1. Execute their effects normally
2. Return to hand after 0.5 seconds instead of discarding

Perfect for testing card effects repeatedly without running out of cards.

#### Card Spawner Window
- **Search Filter**: Find cards by name or description
- **Card Details**: Shows cost, type, effects, and features
- **Individual Spawn**: Click "Spawn" to add specific cards
- **Bulk Operations**: Spawn multiple random test cards or clear hand

### Using TestCombat

1. **Setup**: Add `TestCombat` component to a GameObject in your scene
2. **Enable Return Mode**: `Tools > Test Combat > Toggle Return To Hand Mode`
3. **Open Spawner**: `Tools > Test Combat > Open Card Spawner Window`
4. **Play and Test**: Use the spawner to add cards and test mechanics

### Integration

The system integrates with your existing card system:
- `HandleCardPlay` checks for return-to-hand mode
- Effects still process normally in test mode
- Network synchronization works in multiplayer testing
- All tracking systems remain functional

## Testing Workflow

### Basic Testing
1. Create test cards with CardFactory
2. Add TestCombat component to scene
3. Start play mode
4. Open Card Spawner window
5. Spawn cards and test functionality

### Specific Mechanic Testing
1. Search for specific test cards (e.g., "Test_Scale" for scaling)
2. Spawn the relevant cards
3. Enable return-to-hand mode for repeated testing
4. Observe effects and verify behavior

### Network Testing
1. Start multiplayer session
2. Use TestCombat on each client
3. Test zone effects, targeting, and synchronization
4. Use combat state logging for debugging

### Performance Testing
1. Spawn many complex cards
2. Test with multiple persistent effects
3. Monitor frame rate and network traffic
4. Use entity tracker reset for clean states

## Advanced Features

### Custom Test Cards
You can create additional test cards by extending the CardFactory:

```csharp
private static void CreateCustomTestCards()
{
    // Create your custom test scenarios
    var customCard = CreateTestCard("Test_Custom", "Description", CardEffectType.Damage, 5, CardTargetType.Opponent);
    // Add custom configuration...
}
```

### Integration with CI/CD
The CardFactory can be called from scripts for automated testing:

```csharp
#if UNITY_EDITOR
[UnityTest]
public IEnumerator TestAllCardMechanics()
{
    CardFactory.CreateAllTestCards();
    // Run automated tests...
    CardFactory.ClearTestCards();
}
#endif
```

## Troubleshooting

### Common Issues

**Cards not spawning**:
- Ensure CardDatabase is initialized
- Check that player entity exists and is owned
- Verify HandManager and CardSpawner components

**Return-to-hand not working**:
- Confirm TestCombat component is in scene
- Check that mode is enabled in TestCombat settings
- Ensure cards are being played normally first

**Network synchronization issues**:
- Test with single player first
- Verify all entities have proper network ownership
- Check server/client logs for errors

**Missing test cards**:
- Run CardFactory again
- Check `Assets/CardData/TestCards/` directory
- Refresh CardDatabase if needed

### Debug Commands

Use these methods for debugging:
- `TestCombat.Instance.LogCombatState()`: Complete state dump
- EntityTracker debug properties: Real-time tracking data
- Card component debug fields: Current container and ownership

## Performance Considerations

- Test cards are lightweight ScriptableObjects
- Return-to-hand mode uses minimal overhead
- Card spawner window caches data for performance
- Complex test cards may impact frame rate with many instances

## Future Extensions

The testing system is designed for easy extension:
- Add new test card categories in CardFactory
- Extend TestCombat with additional debug commands
- Create specialized testing scenarios
- Integrate with automated testing frameworks

This comprehensive testing suite ensures all card mechanics work correctly and provides tools for ongoing development and debugging. 