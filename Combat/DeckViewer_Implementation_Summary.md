# Deck Viewer System Implementation Summary

## Overview
The deck viewing system allows players to view the full deck contents of any entity in the currently viewed combat fight. This includes:
- The player's own deck
- The opponent pet's deck  
- The allied pet's deck

The system works seamlessly with combat spectating, automatically updating to show decks relevant to the currently viewed fight.

## Key Components

### 1. DeckViewerManager.cs
**Location**: `Combat/DeckViewerManager.cs`
**Purpose**: Main controller for deck viewing functionality

**Key Features**:
- Spawns temporary cards for viewing deck contents
- Displays cards in a scrollable grid layout
- Manages UI state and button interactions
- Integrates with spectating system
- Automatically updates when viewed fight changes

**Public Methods**:
- `ViewDeck(DeckType)` - Opens deck view for specified type
- `CloseDeckView()` - Closes deck view and cleans up cards
- `UpdateButtonStates()` - Updates button states based on available entities
- `IsDeckViewOpen` - Property to check if deck view is currently open

### 2. Updated CombatCanvasManager.cs
**Changes Made**:
- Added `DeckViewerManager` reference field
- Added `SetupDeckViewer()` method in initialization
- Updated `OnViewedCombatChanged()` to refresh deck viewer
- Added `GetDeckViewerManager()` public accessor

### 3. Updated CombatSpectatorManager.cs
**Changes Made**:
- Added `DeckViewerManager` reference
- Updates deck viewer when switching between fights
- Closes open deck views when changing fights
- Ensures button states are correct for each viewed fight

### 4. Enhanced CardSpawner.cs
**Changes Made**:
- Added `SpawnUnownedCard(CardData, Vector3)` overload for custom positioning
- Updated `SpawnCardInternal` to support custom spawn positions
- Better support for temporary card spawning

### 5. Enhanced FightManager.cs
**Changes Made**:
- Added `GetViewedFightEntities()` helper method
- Added `GetFightEntities(FightAssignmentData)` helper method
- Better support for querying fight participants

## System Flow

### Normal Deck Viewing
1. Player clicks deck view button (My/Opponent/Ally)
2. `DeckViewerManager.ViewDeck()` is called
3. System uses `FightManager` to get current viewed combat entities
4. Appropriate entity's `NetworkEntityDeck` is queried for card IDs
5. `CardSpawner.SpawnUnownedCard()` creates temporary view cards
6. Cards are positioned in grid layout and made non-interactive
7. Deck view panel opens with scrollable card display

### Spectating Integration
1. Player starts spectating via `CombatSpectatorManager`
2. When switching fights, `SwitchToFight()` is called
3. Any open deck view is automatically closed
4. `FightManager.SetViewedFight()` updates viewed entities
5. `DeckViewerManager.UpdateButtonStates()` refreshes button availability
6. Button text updates to show names of entities in new viewed fight

### Cleanup Process
1. When closing deck view or switching fights
2. `CleanupSpawnedCards()` despawns all temporary cards
3. Uses appropriate `CardSpawner.DespawnCard()` for proper cleanup
4. Resets internal state variables

## Integration Points

### With NetworkEntityDeck
- Reads persistent card collections via `GetAllCardIds()`
- Supports any entity with a deck component
- Works with dynamic deck changes

### With CardSpawner
- Uses entity-specific spawners for proper ownership context
- Spawns cards as unowned/temporary for viewing
- Handles proper network despawning

### With RelationshipManager
- Finds allied pets through relationship system
- Supports complex entity relationships
- Updates when relationships change

### With FightManager
- Gets current viewed combat participants
- Supports spectating different fights
- Updates when viewed fight changes

### With EntityVisibilityManager
- Cards respect visibility rules
- Proper client filtering of viewed cards
- Integrates with existing entity systems

## UI Structure Required

```
CombatCanvas
├── DeckViewerManager (script)
├── DeckViewButtons Panel
│   ├── ViewMyDeckButton
│   ├── ViewOpponentDeckButton  
│   └── ViewAllyDeckButton
└── DeckViewPanel (initially inactive)
    ├── DeckViewHeader
    │   ├── DeckViewTitle (TextMeshPro)
    │   └── CloseDeckViewButton
    └── DeckViewScrollRect
        └── DeckViewContainer (Transform)
```

## Configuration Options

### DeckViewerManager Settings
- `cardSpacing`: Grid spacing between cards (default: 1.2, -1.5, 0)
- `cardScale`: Size scale for view cards (default: 0.8)
- `cardsPerRow`: Cards per row in grid (default: 6)
- `debugLogEnabled`: Enable debug logging

### Card Display
- Cards are spawned as non-interactive
- Buttons and colliders are disabled
- Cards are properly scaled and positioned
- Automatic scroll rect content sizing

## Network Considerations

### Server Authority
- Only server can spawn deck view cards
- Proper network spawning/despawning
- Temporary cards don't affect game state

### Client Synchronization
- UI updates happen on all clients
- Button states sync with viewed combat
- Proper cleanup on network disconnect

### Performance
- Cards spawn with small delays to prevent frame drops
- Automatic cleanup prevents memory leaks
- Efficient entity lookups via cached references

## Future Enhancements

### Possible Additions
- Card search/filter functionality
- Deck statistics display
- Card hover previews
- Animated transitions
- Sound effects
- Keyboard shortcuts

### Visual Improvements
- Background blur when deck view open
- Fade in/out animations
- Card flip animations
- Better visual feedback for view-only cards

## Testing Checklist

- [ ] Create UI hierarchy as specified
- [ ] Assign all DeckViewerManager references
- [ ] Test with multiple players and pets
- [ ] Verify spectating updates deck viewer
- [ ] Test deck view for all entity types
- [ ] Verify proper cleanup on fight changes
- [ ] Test with empty decks
- [ ] Test network disconnection scenarios
- [ ] Verify button state updates
- [ ] Test scroll functionality with large decks

## Notes

- System is designed to be modular and extensible
- Works with existing card and entity systems
- Minimal impact on existing combat functionality
- Respects all existing network and visibility rules
- Fully integrated with spectating system 