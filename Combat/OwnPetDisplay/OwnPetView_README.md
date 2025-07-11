# OwnPetView System

The OwnPetView system provides a way to display the currently viewed player's pet in the Combat UI, showing its health, energy, and allowing card interactions.

## Components Overview

### Core Components

1. **OwnPetViewController** - Main controller that coordinates all pet display functionality
2. **OwnPetVisualDisplay** - Handles pet image and name display
3. **OwnPetStatsDisplay** - Manages health and energy UI elements
4. **OwnPetStatusEffectsDisplay** - Shows current status effects affecting the pet
5. **OwnPetCardDropZone** - Handles card drop interactions (currently stubbed)

### Interfaces

- **IOwnPetDisplay** - Basic contract for pet display components
- **IUpdatablePetDisplay** - For components that can update their display
- **IPetCardInteraction** - For components that handle card interactions

## Setup Instructions

### 1. Create the OwnPetViewPrefab

1. Create a new GameObject called "OwnPetViewPrefab"
2. Add a `NetworkObject` component to make it network-aware
3. **Important**: Do NOT add a `NetworkEntity` component - this is a UI element, not a game entity
4. Add the following components to it:
   - `OwnPetViewController`
   - `OwnPetVisualDisplay`
   - `OwnPetStatsDisplay`
   - `OwnPetStatusEffectsDisplay`
   - `OwnPetCardDropZone`

### 2. Set up the UI Structure

Create child GameObjects for the UI elements:

```
OwnPetViewPrefab
├── PetImage (Image component)
├── PetNameText (TextMeshProUGUI component)
├── HealthText (TextMeshProUGUI component)
├── HealthBar (Image component with Image Type: Filled)
├── HealthBarBackground (Image component)
├── EnergyText (TextMeshProUGUI component)
├── EnergyBar (Image component with Image Type: Filled)
├── EnergyBarBackground (Image component)
└── StatusEffectsText (TextMeshProUGUI component)
```

### 3. Configure Component References

#### OwnPetVisualDisplay
- Assign `PetImage` to the `petImage` field
- Assign `PetNameText` to the `petNameText` field

#### OwnPetStatsDisplay
- Assign `HealthText` to the `healthText` field
- Assign `HealthBar` to the `healthBar` field
- Assign `HealthBarBackground` to the `healthBarBackground` field
- Assign `EnergyText` to the `energyText` field
- Assign `EnergyBar` to the `energyBar` field
- Assign `EnergyBarBackground` to the `energyBarBackground` field

#### OwnPetStatusEffectsDisplay
- Assign `StatusEffectsText` to the `statusEffectsText` field

#### OwnPetViewController
- Assign the `OwnPetVisualDisplay` component to the `visualDisplay` field
- Assign the `OwnPetStatsDisplay` component to the `statsDisplay` field
- Assign the `OwnPetStatusEffectsDisplay` component to the `statusEffectsDisplay` field
- Assign the `OwnPetCardDropZone` component to the `cardDropZone` field

### 4. Integrate with CombatCanvasManager

1. In your Combat Canvas, create a Transform to hold the OwnPetViewPrefab (this will be the container)
2. In the `CombatCanvasManager`:
   - Assign the container Transform to the `ownPetViewContainer` field
   - Assign the OwnPetViewPrefab to the `ownPetViewPrefab` field
   - The `ownPetViewController` field will be automatically found at runtime after prefab instantiation

**Instantiation Process**: 
- The prefab is automatically spawned as a NetworkObject during `CombatCanvasManager.SetupOwnPetView()` 
- Server spawns the NetworkObject at scene root first to avoid parenting conflicts
- After spawning, the server moves it to the correct parent container
- Server uses RPC to notify clients about the correct parent hierarchy
- Clients automatically receive the spawned object and move it to the correct parent
- Falls back to regular instantiation if the prefab doesn't have a NetworkObject component

**Network Parenting Solutions**:
- Spawns at root first to avoid NetworkObject parent-child conflicts
- Uses post-spawn parenting to place in correct UI hierarchy
- Client-side coroutine with retry logic to handle timing issues
- RPC communication ensures consistent hierarchy across all clients

## How It Works

### Automatic Ally Detection

The system automatically detects which ally to display based on:
1. The currently viewed fighter (from `FightManager.ViewedLeftFighter`)
2. The fighter's relationship to their ally (via `RelationshipManager.AllyEntity`)

Note: In the current implementation, this displays the left fighter's ally, which in typical player-vs-pet scenarios would be the player's pet. In player-vs-player scenarios, this would be the left player's ally entity.

### Real-time Updates

The system subscribes to the ally's NetworkEntity SyncVars:
- `CurrentHealth` and `MaxHealth` for health display
- `CurrentEnergy` and `MaxEnergy` for energy display
- `EntityName` for name display

### Status Effects Synchronization

The status effects display automatically syncs with the ally's `EffectHandler` component:
- Subscribes to `OnEffectsChanged` events for real-time updates
- Displays effect name, potency, and remaining duration
- Configurable formatting and display limits
- Shows "No active effects" when ally has no status effects

### Spectating Support

When spectating different fights, the system automatically updates to show the currently viewed fighter's ally.

## Customization

### Visual Appearance

- Modify colors in the `OwnPetStatsDisplay` component
- Change the placeholder color in `OwnPetVisualDisplay`
- Customize highlight colors in `OwnPetCardDropZone`
- Configure status effects formatting in `OwnPetStatusEffectsDisplay`

### Status Effects Display

- Toggle display of effect potency and duration
- Customize text formatting for different effect display modes
- Set maximum number of effects to show
- Change "no effects" message text

### Pet Images

Currently uses a white placeholder. To add actual pet images:
1. Modify the `UpdatePetImage()` method in `OwnPetVisualDisplay`
2. Implement pet type identification
3. Load appropriate sprites based on pet data

### Card Drop Functionality

The card drop system is currently stubbed. To implement:
1. Complete the `HandleCardDrop()` method in `OwnPetCardDropZone`
2. Add card validation logic in `CanAcceptCard()`
3. Implement networking for card effects

## Debug Features

All components include debug logging that can be enabled/disabled via inspector checkboxes:
- `debugLogEnabled` in each component
- Logs pet changes, stat updates, and interaction events

## Integration Points

### CombatCanvasManager
- Calls `SetupOwnPetView()` during combat initialization
- Calls `OnViewedCombatChanged()` when spectating changes
- Provides `GetOwnPetViewController()` for external access

### FightManager
- Notifies `CombatCanvasManager` when viewed fight changes
- Provides `ViewedCombatPlayer` for pet detection

### EntityVisibilityManager
- Works alongside the visibility system
- Respects current game state and fight assignments

## Future Enhancements

1. **Pet Animations** - Add animation support to `OwnPetVisualDisplay`
2. **Status Effects** - Display active status effects on the pet
3. **Card Targeting** - Complete the card drop functionality
4. **Multiple Pet Support** - Extend for games with multiple pets per player
5. **Pet Portraits** - Replace placeholder with actual pet artwork system 

## Troubleshooting

### Prefab Gets Disabled After Spawning

**Symptoms**: The OwnPetView prefab spawns but immediately gets disabled
**Likely Causes**:
1. **NetworkEntity component**: If the prefab has a `NetworkEntity` component, it will register with `EntityVisibilityManager` and get controlled by game state visibility rules
2. **Parent NetworkObserver conflicts**: The parent canvas has a NetworkObserver that might conflict with child NetworkObjects
3. **Game state transitions**: EntityVisibilityManager might be hiding entities during state changes

**Solutions**:
1. Remove `NetworkEntity` component from the prefab (it's a UI element, not a game entity)
2. Check the debug logs for EntityVisibilityManager activity
3. Ensure the prefab is spawned at root first, then parented
4. Verify the parent container is active and visible

### Client Hierarchy Issues

**Symptoms**: On clients, the prefab appears at scene root instead of under the correct parent
**Solutions**:
1. The system uses RPC and coroutine retry logic to handle this
2. Check network connectivity and RPC delivery
3. Verify the container Transform is properly assigned

### Debug Logging

Enable debug logging in all components to track:
- When the prefab gets spawned/instantiated
- When it gets enabled/disabled
- EntityVisibilityManager registration and visibility changes
- Network spawning and parenting operations 