# Dynamic Model Swapping System

This system allows NetworkEntity objects to dynamically swap their visual models at runtime based on character selection data. This enables using generic player/pet prefabs that can display different character appearances without requiring separate prefabs for each character.

## System Architecture

### Core Components

#### 1. NetworkEntity (Extended)
- **Selection Data Storage**: Added synced fields for character/pet selection indices and prefab paths
- **Selection Methods**: `SetCharacterSelection()`, `SetPetSelection()`, `HasValidSelection()`
- **Data Access**: `GetSelectedPrefabPath()`, `GetSelectedIndex()`

#### 2. EntityModelManager (New)
- **Purpose**: Handles runtime model swapping for NetworkEntity objects
- **Attachment**: Added to NetworkEntity prefabs that need dynamic model loading
- **Key Features**:
  - Automatic detection of placeholder models
  - Component reference updates (NetworkEntityUI, NetworkEntityAnimator)
  - Network-aware model loading (server + client synchronization)
  - Coroutine-based loading with proper timing

#### 3. NetworkEntityUI (Enhanced)
- **Model References**: Existing `GetEntityModel()` and `SetEntityModel()` methods
- **Integration**: EntityModelManager calls `SetEntityModel()` when models change

#### 4. NetworkEntityAnimator (Enhanced)
- **Dynamic References**: New `SetModelReferences()` method for updating model and animator references
- **Reinitialization**: Properly handles animation setup when models change

#### 5. PlayerSpawner (Updated)
- **Selection Data**: New methods `SetCharacterSelectionData()` and `SetPetSelectionData()`
- **Integration**: Calls selection data methods after spawning entities

## Data Flow

1. **Character Selection Phase**: Player selects character/pet in CharacterSelectionManager
2. **Entity Spawning**: PlayerSpawner creates generic NetworkEntity prefabs
3. **Selection Data**: PlayerSpawner sets character/pet selection data on NetworkEntity
4. **Model Loading**: EntityModelManager detects selection data changes and loads appropriate models
5. **Component Updates**: EntityModelManager updates NetworkEntityUI and NetworkEntityAnimator references
6. **Cleanup**: Old placeholder models are destroyed

## Key Features

### Network Synchronization
- All selection data is synced via SyncVars
- Model changes occur on both server and clients
- Proper event subscription/unsubscription for network lifecycle

### Automatic Setup
- EntityModelManager auto-detects placeholder models if not explicitly set
- Automatic component discovery and reference updates
- Editor tool for adding EntityModelManager to existing prefabs

### Performance Optimization
- Models are only loaded when selection data is complete
- Coroutine-based loading prevents frame drops
- One-time setup flag prevents unnecessary reloading

### Error Handling
- Validates selection data before attempting model loads
- Graceful fallbacks for missing components
- Comprehensive debug logging

## Setup Instructions

### Automatic Setup (Recommended)
1. Open **MVPTools > Setup Entity Model Managers** in Unity
2. Click "Add EntityModelManager to All NetworkEntity Prefabs"
3. The tool will automatically add EntityModelManager to all Player and Pet prefabs

### Manual Setup
1. Add `EntityModelManager` component to NetworkEntity prefabs (Player and Pet types only)
2. Configure `modelParent` if needed (defaults to the NetworkEntity transform)
3. Set `placeholderModel` if not using auto-detection
4. Configure debug settings as needed

## Usage

### For Existing Code
- No changes required to existing character selection logic
- NetworkEntity prefabs automatically gain dynamic model capability
- PlayerSpawner handles setting selection data during entity spawning

### For New Features
- Access selection data via `NetworkEntity.GetSelectedIndex()` and `GetSelectedPrefabPath()`
- Check if entity has valid selection via `NetworkEntity.HasValidSelection()`
- Force model updates via `EntityModelManager.ForceApplyModelChange()`

## Implementation Benefits

1. **Scalability**: Single generic prefab per entity type instead of prefab per character
2. **Memory Efficiency**: Only loads selected models, not all possible models
3. **Network Efficiency**: Selection data syncs once, models load locally
4. **Maintainability**: Changes to character data automatically reflect in game
5. **Flexibility**: Easy to extend for additional entity types or model variations

## Technical Notes

### Component Dependencies
- EntityModelManager requires NetworkEntity, NetworkEntityUI (optional), NetworkEntityAnimator (optional)
- Automatically finds and updates component references when models change
- Uses reflection-free approach for better performance

### Model Loading Process
- Instantiates model prefabs and removes data components (CharacterData, PetData, etc.)
- Updates mesh renderers, animators, and other visual components
- Maintains proper parent-child relationships for UI positioning

### Editor Integration
- Custom editor tool for automatic setup
- Context menu options for testing and debugging
- Validation and error reporting during setup

## Files Modified/Created

### Core System
- `Player+PetObject/EntityModelManager.cs` (New)
- `Player+PetObject/NetworkEntity.cs` (Extended)
- `Player+PetObject/NetworkEntityUI.cs` (Enhanced)
- `Player+PetObject/NetworkEntityAnimator.cs` (Enhanced)
- `CharacterSelection/PlayerSpawner.cs` (Updated)

### Editor Tools
- `Editor/EntityModelManagerSetup.cs` (New)

### Documentation
- `Documentation/DynamicModelSystem.md` (This file) 