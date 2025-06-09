# Character Selection System Implementation

## Overview
A complete character selection phase has been implemented between the Lobby and Combat phases, allowing players to choose characters and pets with unique starter decks before battle begins. **Entity spawning now only occurs during character selection phase.**

## Key Changes Made

### Entity Spawning Flow
- ✅ **Removed entity spawning from lobby connection** - `PlayerSpawner.SpawnPlayerForConnection()` no longer spawns entities
- ✅ **Entities only spawn during character selection** - Using `PlayerSpawner.SpawnPlayerWithSelection()` based on player choices
- ✅ **No duplicate entities** - Clean single-point spawning system

### Default Selection & Auto-Testing
- ✅ **Default character/pet selection** - First character and pet automatically selected when entering character selection
- ✅ **AutoTestRunner integration** - Handles character selection phase automation when `enableAutoTesting = true`
- ✅ **Full automation** - Gets from game start to combat automatically for both host and clients

## Implemented Classes

### 1. CharacterData.cs & PetData.cs (ScriptableObjects)
- **CharacterData**: Defines character archetypes with stats, appearance, starter decks, and prefabs
- **PetData**: Defines pet types with similar structure to characters
- Both include validation methods and customization options

### 2. CharacterSelectionCanvasSetup.cs (UI Builder)
- Programmatically creates the character selection UI following existing patterns
- Builds grids for character/pet selection, deck preview panel, player status display
- Auto-assigns UI elements to CharacterSelectionUIManager via reflection

### 3. CharacterSelectionUIManager.cs (UI Controller)
- Handles all UI interactions: character/pet selection, custom naming, ready states
- Real-time deck preview showing combined character + pet cards
- **NEW**: `MakeDefaultSelections()` - Auto-selects first character/pet on initialization
- **NEW**: AutoTestRunner integration - Auto-ready when testing enabled
- Displays other players' selections and ready status
- Validates selections and enforces 20-character name limit

### 4. CharacterSelectionManager.cs (Network Manager)
- Server-authoritative networking with SyncDictionaries for selections and ready states
- Real-time broadcasting of player selection updates
- Server validation and name sanitization
- **UPDATED**: Uses PlayerSpawner instead of EntitySpawner
- Transition logic when all players are ready

### 5. CharacterSelectionSetup.cs (Phase Coordinator)
- Manages phase transition from Lobby to CharacterSelection
- Coordinates with GamePhaseManager and EntityVisibilityManager
- Handles UI showing/hiding and entity visibility
- Uses RPCs for synchronized client updates

### 6. Updated PlayerSpawner.cs (Entity Spawning)
- **MAJOR CHANGE**: `SpawnPlayerForConnection()` no longer spawns entities - just logs connection
- Extended with `SpawnPlayerWithSelection()` method for character-based spawning
- Sets up starter decks, entity stats, appearance, and relationships
- Returns `SpawnedEntitiesData` for tracking spawned entities

### 7. Updated AutoTestRunner.cs (Test Automation)
- **NEW**: Character selection phase monitoring
- **NEW**: Automatic progression through character selection
- **NEW**: CharacterSelectionManager and GamePhaseManager references
- **NEW**: `WaitForCharacterSelectionPhase()` and `CheckCharacterSelectionConditions()`
- Works seamlessly with CharacterSelectionUIManager's auto-selection feature

## Integration Points

### GamePhaseManager.cs
- Added `CharacterSelection` to GamePhase enum
- Added `SetCharacterSelectionPhase()` method
- Canvas management for character selection UI

### EntityVisibilityManager.cs
- Added `CharacterSelection` to GameState enum
- `UpdateVisibilityForCharacterSelection()` hides entities during selection

### LobbyManager.cs
- Transitions to CharacterSelection instead of Combat
- Calls CharacterSelectionSetup for phase initialization

## Data Flow

### Normal Game Flow:
1. **Connection**: Players connect to lobby → **NO entities spawned**
2. **Lobby**: Players ready up → Transition to character selection
3. **Character Selection**: Auto-select first character/pet → Players can change → Ready up
4. **Entity Spawning**: Server spawns entities based on selections → Transition to combat

### AutoTestRunner Flow (when enabled):
1. **Start Screen**: Auto-click start button
2. **Lobby**: Auto-ready all players → Auto-click start game (host only)
3. **Character Selection**: Auto-select defaults → Auto-ready
4. **Combat**: Entities spawned → Combat begins
5. **All automated end-to-end**

## Key Features

- ✅ **Single-point entity spawning** - Only during character selection
- ✅ **Default selections** - First character/pet auto-selected
- ✅ **Full test automation** - AutoTestRunner handles entire flow
- ✅ **Real-time selection viewing** for all players
- ✅ **Custom character/pet naming** with validation
- ✅ **Deck preview** showing combined character + pet cards
- ✅ **Server-authoritative** selection validation
- ✅ **Seamless integration** with existing game flow
- ✅ **SOLID principles** with clear separation of concerns

## Architecture Benefits

- **Single Responsibility**: Each class has a focused purpose
- **Open/Closed**: Easy to add new characters/pets via ScriptableObjects
- **Interface Segregation**: UI, networking, and spawning are separated
- **Dependency Inversion**: Uses existing systems (GamePhaseManager, EntityVisibilityManager)
- **Clean Entity Management**: No duplicate entities, single spawn point
- **Full Test Coverage**: Complete automation for testing scenarios

## Testing

With `AutoTestRunner.enableAutoTesting = true`:
1. Game automatically progresses from start screen → lobby → character selection → combat
2. Both host and clients automatically make selections and ready up
3. Entities spawn only once during character selection with selected character/pet data
4. No manual intervention required for testing

The system is now fully functional with clean entity management and complete test automation!

## Next Steps

The character selection system is fully functional and ready for testing. Future enhancements could include:

- Character/pet ability previews
- More detailed deck inspection
- Ban/pick phases for competitive play
- Character/pet unlocking systems
- Cosmetic customization options 