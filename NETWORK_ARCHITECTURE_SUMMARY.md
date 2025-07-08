# Network Architecture Refactor Summary

## Overview

The GameManager has been split into three components to provide better separation of concerns and resolve network timing issues:

1. **OfflineGameManager** - Local settings (MonoBehaviour)
2. **OnlineGameManager** - Network-synchronized game state (NetworkBehaviour) 
3. **GameManager** - Backward compatibility bridge (MonoBehaviour)
4. **NetworkCardDatabase** - Card synchronization system (NetworkBehaviour)

## Architecture Components

### OfflineGameManager (MonoBehaviour)
- **Purpose**: Local/offline settings that don't require network synchronization
- **Lifecycle**: Available immediately on scene load
- **Responsibilities**:
  - Randomization preferences (local setting)
  - Frame rate and VSync settings
  - Audio volume settings (master, music, SFX)
  - Graphics settings
  - Other local user preferences

### OnlineGameManager (NetworkBehaviour)
- **Purpose**: Network-synchronized game state and rules
- **Lifecycle**: Spawned by host, synchronized to clients when they join
- **Responsibilities**:
  - Game session ID
  - Player/Pet draw amounts and hand targets
  - Health and energy max values
  - Damage modifiers (crit chance, weak/break multipliers)
  - Draft and shop settings
  - Network-synchronized randomization flag

### GameManager (MonoBehaviour) 
- **Purpose**: Backward compatibility bridge
- **Lifecycle**: Available immediately, delegates to appropriate managers
- **Responsibilities**:
  - Maintains existing API for backward compatibility
  - Routes requests to OfflineGameManager or OnlineGameManager
  - Provides SyncVar-like proxy objects for seamless integration

### NetworkCardDatabase (NetworkBehaviour)
- **Purpose**: Synchronizes procedurally generated cards from host to all clients
- **Lifecycle**: Spawned by host, receives cards from server
- **Responsibilities**:
  - Batched card synchronization (50 cards per batch)
  - Progress tracking and events
  - Converting CardData to network-serializable format
  - Applying synced cards to local CardDatabase

## Card Synchronization Flow

### Host/Server Side:
1. `OfflineGameManager` loads with randomization setting
2. `OnlineGameManager` spawns and reads setting from `OfflineGameManager`
3. `RandomizedCardDatabaseManager` (as NetworkBehaviour) starts on server
4. If randomization enabled, generates cards using `ProceduralCardGenerator`
5. Cards are converted to `NetworkCardData` format
6. `NetworkCardDatabase` syncs cards to all clients in batches
7. Server applies cards to local `CardDatabase` and updates starter deck references

### Client Side:
1. `NetworkCardDatabase` receives batched card data from server
2. Cards are converted back to `CardData` format
3. Local `CardDatabase` is updated with synced cards
4. Starter deck references in `CharacterData`/`PetData` are updated
5. Client notifies completion and triggers any dependent systems

## Key Benefits

### Separation of Concerns
- Local settings (graphics, audio) separate from network state
- No network dependency for offline functionality
- Clear ownership of different types of settings

### Network Timing Resolution
- OfflineGameManager available immediately (no network wait)
- OnlineGameManager properly synchronized across all clients
- No more timing issues with SyncVar initialization

### Backward Compatibility
- Existing code continues to work without changes
- `GameManager.Instance.PlayerDrawAmount.Value` still works
- `GameManager.Instance.RandomizedCardsEnabled.Value` still works
- SyncVar-like proxy objects maintain existing API

### Robust Card Synchronization
- Host generates cards once, all clients receive identical cards
- Batched synchronization prevents network message size issues
- Progress tracking and error handling
- Automatic fallbacks for network issues

## Usage Examples

### Accessing Local Settings
```csharp
// Frame rate setting (available immediately)
bool vsyncEnabled = OfflineGameManager.Instance.EnableVSync;
int maxFPS = OfflineGameManager.Instance.MaxFrameRate;

// Randomization preference (local setting)
bool randomizationEnabled = OfflineGameManager.Instance.EnableRandomizedCards;
```

### Accessing Network State
```csharp
// Player draw amounts (network synchronized)
int playerDraw = OnlineGameManager.Instance.PlayerDrawAmount.Value;
int petDraw = OnlineGameManager.Instance.PetDrawAmount.Value;

// Network randomization state (after sync)
bool networkRandomization = OnlineGameManager.Instance.RandomizationEnabled.Value;
```

### Backward Compatible Access
```csharp
// These continue to work exactly as before
int drawAmount = GameManager.Instance.PlayerDrawAmount.Value;
bool randomEnabled = GameManager.Instance.RandomizedCardsEnabled.Value;
```

### Card Synchronization Events
```csharp
// Subscribe to card sync progress
NetworkCardDatabase.Instance.OnSyncProgress += (progress) => {
    Debug.Log($"Card sync progress: {progress:P0}");
};

// Subscribe to sync completion
NetworkCardDatabase.Instance.OnCardsSynced += () => {
    Debug.Log("All cards synced from host!");
};
```

## File Structure

```
Manager/
├── OfflineGameManager.cs     // Local settings (new)
├── OnlineGameManager.cs      // Network state (new) 
└── GameManager.cs            // Compatibility bridge (refactored)

CardObject/
├── NetworkCardDatabase.cs           // Card sync system (new)
├── NetworkCardData.cs              // Network serialization (new)
└── RandomizedCardDatabaseManager.cs // Updated for network architecture
```

## Setup Requirements

### Scene Setup
1. Create `OfflineGameManager` GameObject in scene (persistent)
2. Create `OnlineGameManager` as NetworkObject (spawned by host)  
3. Create `NetworkCardDatabase` as NetworkObject (spawned by host)
4. Keep existing `GameManager` for backward compatibility
5. Ensure `RandomizedCardDatabaseManager` is a NetworkBehaviour

### Inspector Configuration
- Configure randomization and graphics settings in `OfflineGameManager`
- Configure game rules and balance in `OnlineGameManager`
- `GameManager` requires no configuration (just compatibility bridge)

## Migration Notes

### What Changed
- `GameManager` is no longer a `NetworkBehaviour`
- Settings moved to appropriate specialized managers
- Card synchronization now uses dedicated network system
- Randomization timing moved to proper network lifecycle

### What Stayed the Same
- All existing `GameManager.Instance` calls continue to work
- SyncVar access patterns unchanged (`GameManager.Instance.PlayerDrawAmount.Value`)
- No changes required to calling code
- Same functionality, better architecture

## Troubleshooting

### Common Issues
1. **"OfflineGameManager not found"** - Ensure OfflineGameManager GameObject exists in scene
2. **"OnlineGameManager not found"** - Ensure OnlineGameManager is spawned as NetworkObject by host
3. **Cards not syncing** - Check NetworkCardDatabase is spawned and randomization is enabled
4. **Timing issues** - Use proper managers (Offline for immediate access, Online for network state)

### Debug Commands
- `OfflineGameManager.Instance.LogCurrentSettings()` - Show local settings
- `OnlineGameManager.Instance.LogGameSettings()` - Show network state  
- `GameManager.Instance.LogCurrentDisplaySettings()` - Compatibility bridge test 