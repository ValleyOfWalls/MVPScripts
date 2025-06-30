# Card Upgrade Animation Setup Guide

This guide walks you through setting up the card upgrade animation system that uses actual Card prefab instances to show the transition from base card to upgraded card.

## Overview

The upgrade animation system consists of:
- **CardUpgradeAnimator**: Manages the animation queue and creates temporary card instances
- **InCombatCardReplacer**: Handles replacing cards in combat zones during fights  
- **CardUpgradeManager**: Coordinates upgrade detection and triggers animations

The system uses actual Card prefab instances for animations, eliminating the need for separate UI prefabs.

## Setup Steps

### 1. CardUpgradeAnimator Setup

The CardUpgradeAnimator automatically finds Card prefabs through the CardSpawner system, so no prefab setup is required.

1. Add `CardUpgradeAnimator` component to a persistent GameObject (like GameManager)
2. Configure animation settings:
   - **Animation Timing**: Control duration of each animation phase
   - **Animation Curves**: Customize easing curves for smooth transitions
   - **Visual Effects**: Enable screen shake and transition effects
   - **Animation Layout**: Position and scale of upgrade display cards
   - **Audio**: Assign sound effects for upgrade events

### 2. CardUpgradeManager Integration

The CardUpgradeManager automatically triggers animations when upgrades occur:

1. Add `CardUpgradeManager` component to a persistent NetworkBehaviour GameObject
2. The system will automatically detect CardSpawners and use their Card prefabs for animations

### 3. CardSpawner Requirements

Ensure all entities that can upgrade cards have CardSpawner components:
- Player hand entities need CardSpawner with Card prefab assigned
- Pet hand entities need CardSpawner with Card prefab assigned

The Card prefab should have all standard Card components (Card, NetworkObject, UI elements).

## Component Details

### CardUpgradeAnimator

**Location**: `CardObject/Upgrade/CardUpgradeAnimator.cs`

**Key Features**:
- **Automatic Card Discovery**: Finds Card prefabs through entity CardSpawners
- **Local Instance Creation**: Creates non-networked copies for animation purposes
- **Component Cleanup**: Removes NetworkObject, drag/drop, and collider components from animation instances
- **Animation Phases**: Appear → Display Base → Transition → Display Upgraded → Fade Out
- **Visual Effects**: Screen shake, scale effects, and smooth transitions

**Configuration**:
- **Animation Layout**: Position, scale, and separation of cards during animation
- **Visual Effects**: Screen shake intensity and transition effects
- **Audio**: Sound effects for different animation phases

### InCombatCardReplacer  

**Location**: `CardObject/Upgrade/InCombatCardReplacer.cs`

**Purpose**: Replaces actual card instances in combat zones (hand, deck, discard)

**Key Features**:
- Finds all card instances with specific IDs across all combat zones
- Supports upgrading single cards or all copies
- Maintains card positions and states during replacement
- Validates replacements before executing

### CardUpgradeManager

**Location**: `CardObject/CardUpgradeManager.cs`  

**Purpose**: Detects upgrade conditions and coordinates the upgrade process

**Key Features**:
- Monitors gameplay events for upgrade condition triggers
- Queues animations through CardUpgradeAnimator
- Handles both in-combat and persistent deck upgrades
- Tracks upgrade progress and completion

## Animation Flow

1. **Trigger**: CardUpgradeManager detects met upgrade condition
2. **Queue**: Animation queued in CardUpgradeAnimator
3. **Setup**: Temporary Card instances created from CardSpawner prefabs
4. **Animate**: Base card appears → transition effect → upgraded card revealed
5. **Execute**: InCombatCardReplacer updates actual game cards
6. **Cleanup**: Temporary animation instances destroyed

## Troubleshooting

### Animation Not Playing
- Check that CardUpgradeAnimator.Instance is available
- Verify that CardSpawner exists on the entity or its hand entity
- Ensure Card prefab is assigned in CardSpawner

### Cards Not Upgrading in Combat
- Verify InCombatCardReplacer.Instance is available
- Check that card instances exist in expected locations (hand/deck/discard)
- Confirm CardDatabase has the upgraded card data

### Visual Issues
- Check Animation Layout settings in CardUpgradeAnimator
- Verify Card prefab has proper UI components (nameText, artworkImage, etc.)
- Ensure camera is properly positioned to see upgrade display area

### Network Issues
- Animation uses local-only instances, so no network setup required
- Actual card replacements are handled by existing network systems
- Only the upgrade trigger needs to be networked (handled by CardUpgradeManager)

## Customization

### Animation Timing
Adjust durations in CardUpgradeAnimator:
- `appearDuration`: How long base card takes to appear
- `displayBaseDuration`: How long to show base card
- `transitionDuration`: Duration of card transition effect  
- `displayUpgradedDuration`: How long to show upgraded card
- `fadeOutDuration`: How long cards take to fade out

### Visual Effects
Customize in CardUpgradeAnimator:
- `upgradeDisplayPosition`: World position for upgrade display
- `cardScale`: Size of cards during animation
- `cardSeparation`: Distance between base and upgraded cards
- `enableScreenShake`: Toggle camera shake effect
- `shakeIntensity`: Strength of camera shake

### Audio
Assign audio clips in CardUpgradeAnimator:
- `upgradeStartSound`: Played when base card appears
- `transitionSound`: Played during card transition  
- `upgradeCompleteSound`: Played when upgraded card appears

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