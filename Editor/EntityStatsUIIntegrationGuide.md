# Entity Stats UI Integration Guide

## Overview
This guide explains how the entity stats UI prefabs are automatically spawned and linked with their corresponding main entities during the character selection phase.

## What Was Implemented

### 1. SteamNetworkIntegration Updates
Added new prefab references:
- `NetworkEntityPlayerStatsUIPrefab` - Player stats UI prefab 
- `NetworkEntityPetStatsUIPrefab` - Pet stats UI prefab

These should be assigned in the Unity Inspector on the SteamNetworkIntegration GameObject.

### 2. PlayerSpawner Updates
The PlayerSpawner now:
- Validates stats UI prefabs during startup
- Spawns stats UI entities alongside main entities
- Links stats UI entities to their main entities using RelationshipManager
- Includes stats UI entities in SpawnedEntitiesData

### 3. Automatic Relationship Setup
When entities are spawned, the following relationships are automatically established:

#### Main Entity → Stats UI
- `player.RelationshipManager.SetStatsUI(playerStatsUI)`
- `pet.RelationshipManager.SetStatsUI(petStatsUI)`

#### Stats UI → Main Entity  
- `playerStatsUI.RelationshipManager.SetAlly(player)`
- `petStatsUI.RelationshipManager.SetAlly(pet)`

## Setup Instructions

### 1. Configure Prefabs in Unity Inspector
1. Open the scene containing your SteamNetworkIntegration GameObject
2. Select the SteamNetworkIntegration GameObject
3. In the Inspector, assign the stats UI prefabs:
   - Set `Network Entity Player Stats UI Prefab` to your PlayerStatsUI prefab
   - Set `Network Entity Pet Stats UI Prefab` to your PetStatsUI prefab

### 2. Ensure Prefabs Are Properly Configured
Make sure your stats UI prefabs have:
- NetworkObject component
- NetworkEntity component with correct EntityType (PlayerStatsUI/PetStatsUI)
- RelationshipManager component
- EntityStatsUIController component
- NetworkEntityUI component (if needed)

### 3. Set Up Combat Canvas Positioning
Configure the CombatCanvasManager with positioning transforms:
```csharp
public Transform playerStatsUIPositionTransform;
public Transform opponentPetStatsUIPositionTransform;
```

## How It Works

### Spawning Flow
1. Client selects character and pet
2. PlayerSpawner.SpawnPlayerWithSelection() is called
3. Main entities (player, pet) are spawned
4. Hand entities (player hand, pet hand) are spawned  
5. **Stats UI entities (player stats UI, pet stats UI) are spawned**
6. Relationships are established between all entities
7. All entities are returned in SpawnedEntitiesData

### Entity Ownership
- Stats UI entities have the same owner as their main entities
- This ensures proper networking and visibility permissions

### Automatic Updates
- EntityStatsUIController automatically subscribes to stat changes from main entity
- Stats UI updates in real-time when health, energy, effects, etc. change
- No manual update calls needed

## Networking Integration
- Stats UI entities are full NetworkObjects with proper FishNet integration
- Visibility is controlled by EntityVisibilityManager 
- Stats UI follows same visibility rules as hands (always visible when owner is in viewed fight)
- UI positioning is handled by CombatCanvasManager

## Debugging
The PlayerSpawner includes comprehensive logging:
- Entity spawning success/failure
- Relationship setup confirmation
- Entity ID tracking for debugging

Check the console for messages like:
```
Set up stats UI relationships - Player (ID: 123) -> Stats UI (ID: 456), Pet (ID: 789) -> Stats UI (ID: 101)
```

## Testing
1. Start the game with multiple clients
2. Go through character selection
3. Check that stats UI appears for each player's entities
4. Verify stats update when entities take damage, gain energy, etc.
5. Check that stats UI visibility follows combat viewing rules 