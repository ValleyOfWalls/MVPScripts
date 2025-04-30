# Combat Scene Canvas System

This document explains how to set up and use the combat scene canvas system, which allows each player to view their own combat by default and toggle between different combats.

## Overview

The combat scene canvas system creates a separate canvas for each combat matchup (player vs opponent pet). When combat starts, each player will only see their own combat, but they can use a button to cycle through and spectate other combats.

## Files

- `CombatSceneCanvas.cs`: The main class that manages the combat scene canvas
- `CombatSceneCanvasPrefab.cs`: A guide on how to set up the prefab in the Unity Editor
- `CombatManager.cs`: Modified to create and manage combat scene canvases
- `CombatPlayer.cs`: Added methods for spectator mode
- `CombatPet.cs`: Added methods for spectator mode

## Setup Instructions

### 1. Create the Combat Scene Canvas Prefab

1. Create a new GameObject in your scene and name it "CombatSceneCanvas"
2. Add the `CombatSceneCanvas.cs` script to this GameObject
3. Create the following hierarchy:

```
CombatSceneCanvas (GameObject with CombatSceneCanvas.cs component)
└── Canvas (Canvas component with CanvasScaler, GraphicRaycaster)
    ├── BattleInfoPanel
    │   ├── BattleInfoText (TextMeshProUGUI - shows "{player} vs {opponent}'s Pet")
    │   └── NextBattleButton (Button)
    │       └── ButtonText (TextMeshProUGUI - "View Next Battle")
    ├── BattleViewContainer (Contains combat view elements)
    │   ├── PlayerArea (For player cards, stats, etc.)
    │   ├── PetArea (For opponent pet)
    │   └── BattlefieldArea (Middle area where combat animations happen)
    └── EndTurnButton (Button - Only visible for player's own combat)
```

4. In the CombatSceneCanvas component inspector, assign:
   - `canvasRoot` = Canvas GameObject
   - `battleInfoText` = BattleInfoText component
   - `nextBattleButton` = NextBattleButton component
   - `battleViewContainer` = BattleViewContainer GameObject

5. Create a prefab from this GameObject
6. Remove the `CombatSceneCanvasPrefab.cs` script if it was added (it's only for reference)

### 2. Assign the Prefab to the Combat Manager

1. In the CombatManager inspector, assign the CombatSceneCanvas prefab to the `combatSceneCanvasPrefab` field

## How It Works

1. When combat starts, CombatManager creates a separate CombatSceneCanvas for each player
2. Each player initially only sees their own combat (their player and their opponent's pet)
3. Players can click the "View Next Battle" button to cycle through all active combats
4. When spectating other combats, the UI is view-only and cannot be interacted with
5. Players can cycle back to their own combat to continue playing

## Feature Implementation Notes

- Each combat scene canvas is networked and synchronized across all clients
- The currently viewed combat is tracked per-client, so each player can view different combats
- Combat objects (players, pets, hands) use a slightly transparent visual effect when in spectator mode
- All UI controls are disabled when spectating other players' combats
- When combat ends, all combat scene canvases are reset

## Customization

You can customize the appearance and behavior of the combat scene canvas:

- Modify the UI layout and design in the prefab
- Adjust the transparency and visual effects for spectator mode in `CombatPlayer.SetSpectatorMode()` and `CombatPet.SetSpectatorMode()`
- Add more information or controls to the battle info panel 