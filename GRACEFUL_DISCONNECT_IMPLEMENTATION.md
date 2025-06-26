# Graceful Disconnect and Return to Start Screen Implementation

## Overview
This implementation adds proper graceful disconnection functionality to allow players to leave the character selection/lobby screen and return cleanly to the start screen.

## Problem Solved
Previously, when players tried to leave the character selection/lobby screen, the game would hang because:
1. Network objects weren't properly cleaned up
2. No proper transition back to start screen phase
3. Steam lobby disconnection wasn't handled comprehensively
4. UI state wasn't properly reset
5. GamePhaseManager (NetworkObject) was being called after disconnection
6. PhaseNetworker wasn't working reliably

## Implementation Details

### Key Changes Made

#### 1. SteamNetworkIntegration.cs - Main Disconnect Logic
- **New Method**: `DisconnectAndReturnToStart()`
  - Resets GamePhaseManager to Start phase BEFORE disconnecting (since it's a NetworkObject)
  - Handles complete disconnection process using coroutine for proper timing
  - Ensures both Steam lobby and FishNet disconnection
  - Cleans up character selection models
  - Does NOT call NetworkObjects after disconnection

- **Enhanced Method**: `LeaveLobby()`
  - Added better logging for debugging
  - More robust error handling
  - Clear status messages for each step

- **New Event**: `OnDisconnectedAndReturnedToStart`
  - Allows other systems to be notified when disconnect process completes

- **Lobby Status System**: Prevents new players from joining in-progress games
  - `MarkLobbyAsOpen()`: Marks lobby as available for new players (character selection)
  - `MarkLobbyAsInProgress()`: Marks lobby as unavailable during draft/combat phases
  - `MarkLobbyAsClosed()`: Completely closes lobby when game ends or players disconnect
  - **Filtered Discovery**: Only shows open lobbies in `RequestLobbiesList()`

#### 2. CharacterSelectionUIManager.cs - UI Integration
- **Updated Method**: `LeaveGame()`
  - Now uses `SteamNetworkIntegration.Instance.DisconnectAndReturnToStart()`
  - Includes fallback logic if SteamNetworkIntegration is unavailable
  - Better error handling and logging

#### 3. LobbyManager.cs - Server-Side Support
- **New Method**: `RequestLeaveLobby()`
  - Public method for handling leave requests
  - Routes to SteamNetworkIntegration for consistent handling

#### 4. Phase Management System - RPC as Primary
- **Updated CharacterSelectionSetup.cs**: Uses RPC method as primary for phase changes
- **Updated CombatSetup.cs**: Uses RPC method as primary for phase changes  
- **Updated CharacterSelectionManager.cs**: Uses RPC method as primary for phase changes
- **Removed PhaseNetworker dependency**: Since RPC fallback works reliably, made it the primary method

## How It Works

### Step-by-Step Process
1. **User Clicks Leave Button** (in Character Selection UI)
2. **CharacterSelectionUIManager.LeaveGame()** is called
3. **SteamNetworkIntegration.DisconnectAndReturnToStart()** is invoked
4. **Phase Reset** - GamePhaseManager is reset to Start phase while still connected
5. **Steam Lobby Leave** - Calls `LeaveLobby()` to disconnect from Steam
6. **Network Cleanup** - Stops FishNet client and server connections
7. **Wait for Network Cleanup** - Brief delay to ensure proper disconnection
8. **Model Cleanup** - Character selection models are properly cleaned up
9. **Event Notification** - `OnDisconnectedAndReturnedToStart` event is fired
10. **Reconnection Ready** - Start screen is ready, can rejoin games normally

### Network Object Safety
- **GamePhaseManager Reset**: Called BEFORE disconnection since it's a NetworkObject
- **No Post-Disconnect NetworkObject Calls**: All NetworkObject interaction happens before disconnection
- **UI Transition**: Handled through local GamePhaseManager state, not after disconnection

### RPC Method as Primary
- **Reliable Communication**: RPC methods work consistently across all scenarios
- **No PhaseNetworker Dependency**: Eliminates the "PhaseNetworker not found" error
- **Direct Messaging**: Uses ObserversRpc for immediate phase synchronization

## Usage

### For Players
1. Join a lobby/character selection screen
2. Click the "Leave Game" button in the character selection UI
3. The game will smoothly disconnect and return to the start screen
4. All network objects and UI state will be properly cleaned up
5. **Can rejoin games immediately** - No hanging or broken state

### For Developers

#### Calling Disconnect Programmatically
```csharp
// From any script
if (SteamNetworkIntegration.Instance != null)
{
    SteamNetworkIntegration.Instance.DisconnectAndReturnToStart();
}
```

#### Listening for Disconnect Completion
```csharp
// Subscribe to the event
SteamNetworkIntegration.Instance.OnDisconnectedAndReturnedToStart += OnDisconnectedComplete;

private void OnDisconnectedComplete()
{
    Debug.Log("Player has successfully returned to start screen");
    // Add any additional logic needed after disconnect
}
```

## Testing

### Manual Testing Steps
1. **Start the game** - Should show start screen
2. **Join/Create lobby** - Transition to character selection
3. **Make some selections** - Choose character and pet
4. **Click Leave Game button** - Should see smooth transition back to start
5. **Verify clean state** - Start screen should be fully functional
6. **Click Start Game again** - Should work normally, no hanging
7. **Repeat process** - Should work consistently multiple times

### Expected Behavior
- **No hanging** - Game should never freeze during disconnect or reconnection
- **Clean UI** - Start screen should appear properly after leaving
- **No memory leaks** - Character selection models should be cleaned up
- **Consistent state** - Can rejoin games after leaving without issues
- **Working reconnection** - Start Game button works normally after leaving
- **No RPC errors** - Should see "RPC" in logs instead of "PhaseNetworker not found"

### Debug Logging
The implementation includes comprehensive logging:
- Steam lobby disconnection steps
- Network connection status changes
- UI cleanup and refresh steps
- Error conditions and fallbacks
- Phase transition confirmation

Look for log messages starting with "SteamNetworkIntegration:" to track the disconnect process.

## Network Object Considerations

### Why Network Objects Aren't Destroyed
As specified in the requirements, network objects in this game are designed to persist and shouldn't be destroyed as part of cleanup. The implementation:
- Doesn't call Destroy() on network objects
- Uses cleanup methods that preserve network object integrity
- Relies on FishNet's built-in connection management
- Allows network objects to handle their own lifecycle
- **Calls NetworkObjects only while still connected**

### GamePhaseManager Handling
Since GamePhaseManager is a NetworkObject:
- **Phase reset happens BEFORE disconnection** while still connected
- **No calls to GamePhaseManager after disconnection**
- **Local UI state handles the transition properly**
- **Start Game button works because phase is properly reset**

### Character Selection Model Cleanup
The character selection models are cleaned up through:
- `CharacterSelectionUIManager.CleanupSelectionModels()`
- Uses `SelectionNetworkObject.CleanupSelectionObject()` for network objects
- Regular GameObject cleanup for non-network objects
- Proper disposal of EntitySelectionController references

## Error Handling

### Fallback Mechanisms
- If SteamNetworkIntegration.Instance is null, uses direct FishNet disconnection
- If GamePhaseManager is not found, continues with disconnect process
- Comprehensive logging for troubleshooting edge cases
- RPC methods are now primary, eliminating PhaseNetworker dependency

### Common Issues and Solutions
1. **Hanging during disconnect**: Check network manager status in logs
2. **Start Game not working after disconnect**: Verify GamePhaseManager is reset to Start phase before disconnection
3. **Memory leaks**: Ensure CleanupSelectionModels() is being called
4. **PhaseNetworker errors**: Fixed by using RPC as primary method
5. **Reconnection issues**: Check Steam lobby status and network object cleanup

## Lobby Status Management

### Automatic Status Updates
The system automatically manages lobby availability to prevent new players from joining inappropriate game states:

#### Status Transitions
- **Open** → Character Selection Phase: New players can join and select characters/pets
- **In-Progress** → Draft/Combat Phases: No new players allowed (game already started)
- **Closed** → Game End/Disconnect: Lobby permanently unavailable

#### Implementation Points
- **Character Selection Entry**: `GamePhaseManager.ExecutePhaseSpecificLogic()` marks lobby as open
- **Draft Phase Entry**: Automatically marked as in-progress to prevent mid-draft joins
- **Combat Transition**: `CharacterSelectionManager.TransitionToCombat()` marks as in-progress
- **Player Disconnect**: `SteamNetworkIntegration.DisconnectAndReturnToStart()` marks as closed
- **Lobby Discovery**: `RequestLobbiesList()` only returns open lobbies

### Benefits
- **No Mid-Game Joins**: Players can't accidentally join games already in progress
- **Cleaner Matchmaking**: Only shows lobbies that are actually available for new players
- **Automatic Management**: No manual intervention required, status updates automatically
- **Host Control**: Only lobby hosts can update status, preventing conflicts

## Future Enhancements

### Potential Improvements
1. **Progress Indicator** - Show disconnect progress to users
2. **Cancellation** - Allow users to cancel disconnect process
3. **Auto-reconnect** - Option to automatically rejoin previous lobby
4. **Batch Cleanup** - More efficient cleanup for large numbers of objects
5. **Analytics** - Track disconnect success rates and common failure points
6. **Lobby Browser UI** - Show lobby status and player counts in a browseable interface 