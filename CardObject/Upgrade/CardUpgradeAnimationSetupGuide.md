# Card Upgrade Animation System Setup Guide

## Overview

This system provides dramatic upgrade animations when cards meet their upgrade criteria in combat. When a card upgrades, it will:

1. Show a large representation of the base card in the center of the screen
2. Play a bombastic entrance animation
3. Perform a wipe transition to the upgraded version
4. Fade out after displaying the upgraded card
5. Replace all instances of the card in combat zones (hand, deck, discard)
6. Update the persistent deck with the upgraded version

## Setup Instructions

### 1. Generate the Upgrade Display Prefab

First, you need to create the CardUpgradeDisplay prefab:

1. In Unity, go to **Tools > Card Upgrade > Generate Upgrade Display Prefab**
2. Click **"Generate CardUpgradeDisplay Prefab"**
3. The prefab will be created at `Assets/MVPScripts/CardObject/Upgrade/CardUpgradeDisplay.prefab`

### 2. Setup the Singleton Components

Add these components to a singleton GameObject in your scene (like GameManager):

#### CardUpgradeAnimator
- **Attach to**: A singleton GameObject (like GameManager or create a dedicated UpgradeManager)
- **Purpose**: Manages upgrade animations, effects, and queuing
- **Configuration**:
  - **Card Upgrade Display Prefab**: Assign the generated CardUpgradeDisplay.prefab
  - **Animation Timing**: Adjust durations for appear, display, transition, etc.
  - **Audio**: Assign sound effects for upgrade start, transition, and complete
  - **Visual Effects**: Enable/disable screen shake, set intensity

#### InCombatCardReplacer
- **Attach to**: Same singleton GameObject as CardUpgradeAnimator
- **Purpose**: Handles replacing cards in all combat zones during a fight
- **No configuration needed**: Works automatically

### 3. Verify Integration

The system automatically integrates with the existing `CardUpgradeManager`. When a card meets its upgrade criteria:

1. `CardUpgradeManager` detects the upgrade condition
2. Queues the upgrade animation via `CardUpgradeAnimator`
3. Shows the animation sequence
4. Replaces cards in combat via `InCombatCardReplacer`
5. Updates the persistent deck via `NetworkEntityDeck`

## Component Descriptions

### CardUpgradeAnimator

**Location**: `CardObject/Upgrade/CardUpgradeAnimator.cs`

**Key Features**:
- **Animation Queuing**: Multiple upgrades are queued and played sequentially
- **Timing Control**: Configurable durations for each animation phase
- **Visual Effects**: Screen shake, scale animations, fade effects
- **Audio Integration**: Sound effects for different animation phases
- **Game Pause**: Optionally pause the game during upgrade animations

**Inspector Settings**:
- **Animation Timing**: Control how long each phase lasts
- **Animation Curves**: Customize the feel of scale and fade animations
- **Visual Effects**: Toggle screen shake and adjust intensity
- **Audio**: Assign sound clips for different events
- **Prefab Reference**: Must be assigned to the CardUpgradeDisplay prefab

### CardUpgradeUIController

**Location**: `CardObject/Upgrade/CardUpgradeUIController.cs`

**Purpose**: Manages the UI elements of the upgrade display
- Displays card information (name, description, artwork, effects)
- Handles the wipe transition effect
- Updates card visuals based on card data

**Attachment**: Automatically added to the CardUpgradeDisplay prefab

### InCombatCardReplacer

**Location**: `CardObject/Upgrade/InCombatCardReplacer.cs`

**Purpose**: Replaces cards in all combat zones (hand, deck, discard pile)
- Finds all instances of the base card
- Updates them with the upgraded version
- Preserves card state and position
- Supports upgrading all copies or just one

**Key Methods**:
- `ReplaceCardInAllZones()`: Main entry point for card replacement
- `FindAllCardInstances()`: Locates cards across all zones
- `UpdateCardInstance()`: Updates individual card data

## Animation Sequence

When a card upgrade is triggered:

1. **Queueing**: Upgrade is added to the animation queue
2. **Appear** (0.5s): Card scales in from zero with fade
3. **Display Base** (1.0s): Shows the original card
4. **Transition** (0.8s): Wipe effect covers and reveals upgraded card
5. **Display Upgraded** (1.5s): Shows the new card with completion sound
6. **Fade Out** (0.5s): Card fades away (only if this is the last queued animation)
7. **Card Replacement**: Updates all card instances in combat and persistent deck

## Advanced Configuration

### Custom Animation Curves

The `CardUpgradeAnimator` uses animation curves for smooth transitions:
- **Scale In Curve**: Controls how the card scales during appearance
- **Fade In Curve**: Controls alpha fade during appearance
- **Shake Intensity Curve**: Controls screen shake intensity over time

### Audio Integration

Assign audio clips for:
- **Upgrade Start Sound**: Played when animation begins
- **Transition Sound**: Played during the wipe effect
- **Upgrade Complete Sound**: Played when showing the upgraded card

### Visual Customization

The `CardUpgradeUIController` automatically formats cards based on their properties:
- Card background color changes based on card type
- Stats are displayed only if relevant (damage, shield, etc.)
- Effects are listed in human-readable format

## Troubleshooting

### Animation Not Playing
- Ensure `CardUpgradeAnimator.Instance` is not null
- Check that the CardUpgradeDisplay prefab is assigned
- Verify the singleton is properly initialized

### Cards Not Replacing in Combat
- Ensure `InCombatCardReplacer.Instance` is not null
- Check that the entity has a `RelationshipManager` with a valid `HandEntity`
- Verify the `HandManager` has proper transform references

### UI Elements Missing
- Regenerate the CardUpgradeDisplay prefab using the editor tool
- Check that all UI references in `CardUpgradeUIController` are assigned
- Ensure TextMeshPro is imported in the project

### Performance Issues
- Reduce animation durations if needed
- Disable screen shake for lower-end devices
- Consider reducing the number of simultaneous upgrades

## Example Usage

The system works automatically with existing upgrade conditions, but you can also trigger upgrades manually:

```csharp
// Get the card data
CardData baseCard = CardDatabase.Instance.GetCardById(oldCardId);
CardData upgradedCard = CardDatabase.Instance.GetCardById(newCardId);

// Queue the upgrade animation
CardUpgradeAnimator.Instance.QueueUpgradeAnimation(
    baseCard, 
    upgradedCard, 
    playerEntity, 
    upgradeAllCopies: true,
    onComplete: (base, upgraded, entity, all) => {
        Debug.Log($"Upgrade complete: {base.CardName} -> {upgraded.CardName}");
    }
);
```

## Testing

To test the system:

1. Set up a card with simple upgrade conditions (e.g., play it once)
2. Start a combat encounter
3. Play the card to trigger the upgrade
4. Verify the animation plays and cards are replaced properly

The system includes extensive debug logging to help track the upgrade process. 