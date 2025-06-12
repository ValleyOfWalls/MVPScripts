# Loading Screen Setup Guide

## Overview
The `LoadingScreenManager` creates a loading screen that appears between Character Selection and Combat phases, displaying random loading tips and transitioning automatically when combat setup completes.

## GameObject Setup

### 1. Create Loading Screen GameObject
1. Create a new GameObject named "LoadingScreen"
2. Add a `Canvas` component
3. Add a `NetworkObject` component
4. Add the `LoadingScreenManager` script

### 2. Canvas Configuration
- **Render Mode**: Screen Space - Overlay
- **Sort Order**: 1000 (or higher to appear on top)
- **Override Sorting**: Enabled

### 3. UI Hierarchy Structure
```
LoadingScreen (Canvas + NetworkObject + LoadingScreenManager)
└── LoadingScreenPanel (GameObject)
    ├── BackgroundImage (Image) - Full screen background
    ├── LoadingTipImage (Image) - For random tip images
    └── LoadingText (TextMeshPro) - For animated loading messages
```

### 4. Component References
In the `LoadingScreenManager` script, assign:
- **Loading Canvas**: The Canvas component
- **Loading Screen Panel**: The main panel GameObject
- **Loading Tip Image**: The Image component for tips
- **Loading Tip Sprites**: Array of tip images (drag multiple sprites)
- **Loading Text**: The TextMeshPro component
- **Combat Setup**: Reference to CombatSetup in scene
- **Game Phase Manager**: Reference to GamePhaseManager in scene

## Loading Tip Images Setup

### Creating Tip Images
1. Create full-screen images (1920x1080 recommended)
2. Import as Sprites in Unity
3. Add gameplay tips, combat strategies, or lore
4. Assign to the `loadingTipSprites` array in the inspector

### Example Loading Tips
- Combat strategy guides
- Card combination hints
- Gameplay mechanics explanations
- Lore and world-building content
- Character backstories

## Integration with Game Flow

### Automatic Triggering
The loading screen automatically shows when:
1. Game phase transitions to Combat
2. CombatSetup begins initialization
3. Loading screen monitors setup completion
4. Hides when combat is ready

### Manual Control (for testing)
Use the context menu options:
- Right-click script → "Show Loading Screen"
- Right-click script → "Hide Loading Screen"

## Customization Options

### Loading Messages
Edit the `loadingMessages` array to customize text:
```csharp
"Preparing for battle...",
"Setting up combat decks...",
"Assigning fights...",
"Loading combat arena...",
"Initializing battle systems..."
```

### Animation Settings
- **Animate Loading Text**: Enable/disable text cycling
- **Text Animation Speed**: Time between message changes (seconds)

### Canvas Sorting
- **Loading Canvas Sort Order**: Higher values appear on top (default: 1000)

## Runtime Customization

### Add Messages Dynamically
```csharp
LoadingScreenManager loadingScreen = FindFirstObjectByType<LoadingScreenManager>();
loadingScreen.AddLoadingMessage("New loading tip!");
```

### Add Tip Images Dynamically
```csharp
LoadingScreenManager loadingScreen = FindFirstObjectByType<LoadingScreenManager>();
loadingScreen.AddLoadingTipSprite(newTipSprite);
```

## Troubleshooting

### Loading Screen Not Appearing
1. Check Canvas sort order is high enough
2. Verify GamePhaseManager reference is assigned
3. Ensure loading screen panel is initially inactive
4. Check phase transition is triggering correctly

### Tips Not Displaying
1. Verify `loadingTipSprites` array has sprites assigned
2. Check `loadingTipImage` component reference
3. Ensure sprites are imported as Sprite type

### Text Not Animating
1. Check `animateLoadingText` is enabled
2. Verify `loadingText` TextMeshPro reference
3. Ensure `loadingMessages` array has content

### Transition Not Working
1. Verify `CombatSetup` reference is assigned
2. Check combat setup completion monitoring
3. Ensure NetworkObject is properly spawned 