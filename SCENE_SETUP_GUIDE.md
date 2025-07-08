# Scene Setup Guide: New Network Architecture

This guide explains how to set up scenes to work with the new separated network architecture.

## Overview

The old `GameManager` has been split into three main components:

1. **OfflineGameManager** (MonoBehaviour): Local settings and configuration
2. **OnlineGameManager** (NetworkBehaviour): Network-synchronized game state  
3. **NetworkCardDatabase** (NetworkBehaviour): Synchronized procedural cards

## Required Objects in Scene Hierarchy

### 1. Game Management Objects

#### A. OfflineGameManager (Scene Object)
- **Type**: MonoBehaviour (always exists locally)
- **Purpose**: Local settings like randomization flag, graphics, audio
- **Setup**: Create GameObject in scene, add `OfflineGameManager.cs` component
- **Settings**:
  - Set `randomizationEnabled` checkbox as desired
  - Configure frame rate, audio levels, etc.

#### B. OnlineGameManager Prefab
- **Type**: NetworkBehaviour (spawned by host)
- **Purpose**: Network-synchronized game state
- **Setup**: Create prefab with `OnlineGameManager.cs` and `NetworkObject` components
- **Settings**:
  - Configure all game balance values (damage modifiers, hand sizes, etc.)
  - Set draft/shop pack sizes and card counts
  - **Important**: Add to FishNet's "Network Prefabs" list

#### C. NetworkCardDatabase Prefab
- **Type**: NetworkBehaviour (spawned by host) 
- **Purpose**: Synchronizes procedural cards from host to clients
- **Setup**: Create prefab with `NetworkCardDatabase.cs` and `NetworkObject` components
- **Settings**:
  - Leave default settings
  - **Important**: Add to FishNet's "Network Prefabs" list

#### D. NetworkManagerSpawner (Scene Object)
- **Type**: MonoBehaviour (spawns network objects)
- **Purpose**: Spawns OnlineGameManager and NetworkCardDatabase when server starts
- **Setup**: Attach to NetworkManager GameObject or create separate persistent object
- **Settings**:
  - Assign OnlineGameManager prefab
  - Assign NetworkCardDatabase prefab

### 2. Card System Objects

Update existing card objects:

#### RandomizedCardDatabaseManager
- **Now**: NetworkBehaviour (was MonoBehaviour)
- **Purpose**: Generates cards on server, syncs via NetworkCardDatabase
- **Dependencies**: Requires NetworkCardDatabase to be spawned first

### 3. Combat System Objects

Update existing combat objects:

#### DamageCalculator
- **Component**: `DamageCalculator.cs` 
- **Changes**: Now accesses `OnlineGameManager.Instance` directly
- **No serialized references needed**: Gets settings from OnlineGameManager

### 4. Draft System Objects

Update existing draft objects:

#### DraftPackSetup, DraftSetup, DraftManager, etc.
- **Field Type Change**: `OnlineGameManager gameManager` (was `GameManager`)
- **Initialization**: Gets `OnlineGameManager.Instance` in Awake()

## Network Object Setup Checklist

### In FishNet's NetworkManager:

1. **Network Prefabs** tab:
   - Add `OnlineGameManager` prefab
   - Add `NetworkCardDatabase` prefab

2. **Spawnable Prefabs** tab:
   - Ensure all player/pet prefabs are registered
   - Ensure card prefabs are registered

### Spawning Order (Server Only):

The spawning happens automatically, but for reference:

1. **OfflineGameManager**: Always exists (MonoBehaviour)
2. **OnlineGameManager**: Spawned by `OnStartServer()` 
3. **NetworkCardDatabase**: Spawned by OnlineGameManager
4. **RandomizedCardDatabaseManager**: Reads from NetworkCardDatabase

## How Network Spawning Works

### Server (Host) Side:
1. **Server starts** → `NetworkManagerSpawner` detects server start
2. **Spawns OnlineGameManager** → Becomes available as `OnlineGameManager.Instance`
3. **Spawns NetworkCardDatabase** → Becomes available as `NetworkCardDatabase.Instance`
4. **OnlineGameManager reads OfflineGameManager** → Copies randomization settings
5. **Cards generate if randomization enabled** → RandomizedCardDatabaseManager creates cards
6. **NetworkCardDatabase syncs cards** → Sends cards to all clients

### Client Side:
1. **Client connects** → FishNet automatically sends spawned NetworkObjects to client
2. **Client receives OnlineGameManager** → `OnlineGameManager.Instance` becomes available
3. **Client receives NetworkCardDatabase** → `NetworkCardDatabase.Instance` becomes available  
4. **Client receives card sync** → Cards applied to local CardDatabase
5. **Client systems work normally** → Access managers via `.Instance` properties

### Key Points:
- ✅ **Automatic**: Clients automatically receive spawned NetworkObjects
- ✅ **Timing**: OnlineGameManager available before character selection
- ✅ **Singleton Pattern**: Instance properties work on both host and clients
- ✅ **Card Sync**: All clients get identical procedural cards

## Scene Transition Flow

### 1. Main Menu → Character Selection
```
OfflineGameManager loads → 
NetworkManagerSpawner spawns managers →
OnlineGameManager reads offline settings →
NetworkCardDatabase spawns →
Cards generate & sync →
Character selection shows randomized decks
```

### 2. Character Selection → Draft
```
Player selections complete →
OnlineGameManager settings applied →
Draft packs use OnlineGameManager values
```

### 3. Draft → Combat
```
Draft complete →
Combat systems access OnlineGameManager →
DamageCalculator uses network settings
```

## Common Setup Issues

### ❌ "OnlineGameManager.Instance is null"
**Cause**: OnlineGameManager not spawned or not in Network Prefabs list
**Fix**: 
1. Add OnlineGameManager to Network Prefabs
2. Ensure it spawns on server start
3. Check for null before accessing

### ❌ "Randomized cards not showing"
**Cause**: NetworkCardDatabase not synchronized  
**Fix**:
1. Ensure NetworkCardDatabase is in Network Prefabs
2. Check RandomizedCardDatabaseManager is NetworkBehaviour
3. Verify cards sync before character selection

### ❌ "Compilation errors after update"
**Cause**: Old GameManager references
**Fix**:
1. Replace `GameManager` with `OnlineGameManager` or `OfflineGameManager`
2. Remove `.Value` from properties that are no longer SyncVars
3. Update field declarations in serialized fields

## Testing Checklist

### Single Player:
- [ ] OfflineGameManager settings work
- [ ] OnlineGameManager spawns correctly  
- [ ] Randomized cards generate if enabled
- [ ] Combat damage calculation works

### Multiplayer:
- [ ] Host generates cards once
- [ ] Clients receive synced cards
- [ ] All players see identical card sets
- [ ] Game settings synchronized across clients

## Step-by-Step Setup Instructions

### Step 1: Create OfflineGameManager (Scene Object)
1. In your scene, create empty GameObject named "OfflineGameManager"
2. Add `OfflineGameManager.cs` component
3. Configure settings:
   - Check "Enable Randomized Cards" if desired
   - Set frame rate and VSync preferences
   - Adjust audio levels

### Step 2: Create OnlineGameManager Prefab
1. Create empty GameObject named "OnlineGameManager"
2. Add these components:
   - `OnlineGameManager.cs`
   - `NetworkObject` (FishNet component)
3. Configure game balance settings
4. Save as prefab in Project window
5. **Important**: Add this prefab to FishNet NetworkManager → Network Prefabs list

### Step 3: Create NetworkCardDatabase Prefab
1. Create empty GameObject named "NetworkCardDatabase"
2. Add these components:
   - `NetworkCardDatabase.cs`
   - `NetworkObject` (FishNet component)
3. Leave default settings
4. Save as prefab in Project window
5. **Important**: Add this prefab to FishNet NetworkManager → Network Prefabs list

### Step 4: Setup NetworkManagerSpawner
1. Find your NetworkManager GameObject in the scene
2. Add `NetworkManagerSpawner.cs` component to it
3. Assign prefab references:
   - **Online Game Manager Prefab**: Drag OnlineGameManager prefab here
   - **Network Card Database Prefab**: Drag NetworkCardDatabase prefab here

### Step 5: Update RandomizedCardDatabaseManager
1. Find RandomizedCardDatabaseManager in your scene
2. Ensure it has these components:
   - `RandomizedCardDatabaseManager.cs`
   - `NetworkObject` (FishNet component)
3. Add to Network Prefabs list if not already there

## Migration Steps

If migrating an existing scene:

1. **Remove old GameManager**:
   - Delete GameObject with old GameManager component

2. **Add new managers using steps above**:
   - Follow Step 1-5 above for complete setup

3. **Update references**:
   - Find all scripts with `GameManager` fields
   - Change types to appropriate new manager
   - Update GetComponent/FindObjectOfType calls

4. **Test thoroughly**:
   - Verify all systems work in single player
   - Test multiplayer synchronization
   - Check card randomization works correctly

## Advanced Configuration

### Custom Card Generation:
- Modify `ProceduralCardGenerator.cs` 
- Update rarity distributions in config files
- Test with different pack sizes

### Network Performance:
- Adjust batch sizes in `NetworkCardDatabase.cs`
- Monitor network traffic during card sync
- Consider compression for large card sets

### Debugging:
- Enable debug logs with `[NETDB]` prefix for card sync
- Use `[OFFLINE]` prefix for OfflineGameManager logs  
- Check `[ONLINE]` prefix for OnlineGameManager logs

---

**Important**: Always test multiplayer scenarios when making changes. The new architecture is designed to handle network timing issues that were problematic with the old unified GameManager. 