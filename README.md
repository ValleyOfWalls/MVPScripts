# Multiplayer Game Lobby System

This is a multiplayer lobby system created using Fishnet, Steamworks.Net, and DOTween.

## Setup Instructions

1. **Scene Setup**
   - Create a new scene or use an existing one
   - Add a `GameSetup` object by right-clicking in the hierarchy and selecting Create Empty, then add the `GameSetup.cs` script to it
   - The GameSetup script will handle creating all necessary managers and UI components at runtime

2. **Required Assets**
   - Make sure you have the following assets imported:
     - FishNet (https://assetstore.unity.com/packages/tools/network/fish-net-networking-evolved-207815)
     - Steamworks.NET (https://github.com/rlabrecque/Steamworks.NET)
     - DOTween (https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676)
     - TextMeshPro (Available through Package Manager)

3. **Generating Prefabs (Optional)**
   - You can add the `PrefabGenerator` script to any GameObject in your scene in the editor
   - Use the context menu (right-click the component) to generate all necessary prefabs
   - Alternatively, check the "Auto Generate Missing Prefabs" box to automatically create any missing prefabs

4. **Testing Locally**
   - You can test locally by building a standalone version of the game and running two instances
   - One instance will host, and the other will join
   - For Steam integration, you'll need to set up a Steam App ID and follow Steamworks setup instructions

## System Overview

### Core Scripts

1. **GameSetup.cs**
   - Main entry point for setting up the game
   - Creates all necessary managers and UI components if they don't exist

2. **NetworkSetup.cs**
   - Handles the setup of network-related components
   - Creates FishNet's NetworkManager with appropriate transport

3. **GameManager.cs**
   - Manages game state and transitions
   - Handles player connections, ready status, and game start

4. **UIManager.cs**
   - Manages UI elements and their interactions
   - Updates player list and handles button clicks

5. **Player.cs**
   - Represents a player in the game
   - Manages player properties like name and ready status

6. **PrefabGenerator.cs**
   - Editor tool to generate all necessary prefabs
   - Only runs in the Unity Editor, not at runtime

### Game Flow

1. Players start at the Start Screen, where they enter their name
2. On connecting, players enter the Lobby where they can see other players and ready up
3. Once all players are ready, the host can start the game
4. When the game starts, all players transition to the Combat screen

## Customization

- **UI**: Modify the UI elements by editing the canvas prefabs or the CreateCanvas methods in NetworkSetup.cs
- **Player Visuals**: Change the player appearance by modifying the Player prefab or the CreatePlayerPrefab method
- **Network Configuration**: Adjust network settings in the NetworkSetup.cs file

## Troubleshooting

- If players cannot connect, check firewall settings and port forwarding
- Make sure the NetworkManager prefab has the correct transport settings
- Ensure all required plugins are properly imported and initialized 