# Fight Preview System Setup Guide

## Overview
The Fight Preview System displays a 3-second interstitial screen showing "Player Name vs Opponent Pet Name" with images before combat begins. The system now uses a clean separation of concerns with dedicated animation handling.

## Components Created
1. **FightPreviewUIManager.cs** - Handles UI display and data management
2. **FightPreviewAnimator.cs** - **NEW** - Dedicated animation system with curves and effects
3. **FightPreviewManager.cs** - Manages timing and flow control
4. Modified **CombatSetup.cs** - Integration with existing combat flow

## Architecture Overview

The system follows SOLID principles with clear separation of responsibilities:

```
FightPreviewManager (Flow Control)
├── FightPreviewUIManager (UI Management)
│   └── FightPreviewAnimator (Animation System)
└── CombatManager (Combat Start)
```

## Unity Setup Requirements

### 1. Create Fight Preview Canvas
Create a new Canvas in your scene with the following structure:

```
FightPreviewCanvas (GameObject with Canvas, CanvasScaler, GraphicRaycaster, CanvasGroup)
├── BackgroundPanel (Image - dark semi-transparent background)
└── FightInfoPanel (GameObject)
    ├── PlayerNameText (TextMeshPro - UGUI)
    ├── VersusText (TextMeshPro - UGUI) [displays "VS"]
    ├── OpponentPetNameText (TextMeshPro - UGUI)
    ├── PlayerImage (Image)
    └── OpponentPetImage (Image)
```

**Important**: The FightPreviewCanvas must have a `CanvasGroup` component for animations to work.

### 2. FightPreviewAnimator Setup
1. Create a GameObject called "FightPreviewAnimator" (or add to existing UI Manager)
2. Add the `FightPreviewAnimator` component
3. In the Inspector, configure:
   - **Fade In Duration**: 0.5 seconds (default)
   - **Fade Out Duration**: 0.5 seconds (default) 
   - **Display Duration**: 3.0 seconds (default)
   - **Use Scale Animation**: Optional scaling effect
   - **Animation Curves**: Customize fade curves for smoother animations

### 3. FightPreviewUIManager Setup
1. Create a GameObject called "FightPreviewUIManager"
2. Add the `FightPreviewUIManager` component
3. Add a `NetworkObject` component (required for FishNet)
4. In the Inspector, assign:
   - **Fight Preview Canvas**: The canvas created above
   - **Player Name Text**: Reference to PlayerNameText
   - **Versus Text**: Reference to VersusText
   - **Opponent Pet Name Text**: Reference to OpponentPetNameText
   - **Player Image**: Reference to PlayerImage
   - **Opponent Pet Image**: Reference to OpponentPetImage
   - **Background Panel**: Reference to BackgroundPanel
   - **Animator**: Reference to FightPreviewAnimator component

### 4. FightPreviewManager Setup
1. Create a GameObject called "FightPreviewManager"
2. Add the `FightPreviewManager` component
3. Add a `NetworkObject` component (required for FishNet)
4. In the Inspector, assign:
   - **Fight Preview UI Manager**: Reference to FightPreviewUIManager
   - **Fight Preview Animator**: Reference to FightPreviewAnimator
   - **Combat Manager**: Reference to your CombatManager
   - **Combat Canvas Manager**: Reference to your CombatCanvasManager
   - **Game Phase Manager**: Reference to your GamePhaseManager
   - **Additional Delay Before Combat**: Set to 0.5 (seconds)

### 5. CombatSetup Integration
In your CombatSetup component Inspector:
- **Fight Preview Manager**: Reference to your FightPreviewManager

## Animation Features

### Basic Animation
- **Fade In**: Smooth alpha transition from 0 to 1
- **Display**: Hold at full opacity
- **Fade Out**: Smooth alpha transition from 1 to 0

### Advanced Animation (Optional)
- **Scale Animation**: Elements can scale up/down during transitions
- **Custom Animation Curves**: Define exact easing for professional feel
- **Configurable Timing**: Adjust individual phase durations

### Animation Events
The system provides events for integration:
- `OnAnimationStarted`: When fade-in begins
- `OnDisplayPhaseStarted`: When full display begins  
- `OnAnimationCompleted`: When fade-out ends

## Flow Changes
The new flow with separated animation system:

### Old Flow:
```
LobbyManager.RpcStartGame() 
→ CombatSetup.InitializeCombat() 
→ CombatSetup.StartCombat() 
→ CombatManager.StartCombat()
```

### New Flow:
```
LobbyManager.RpcStartGame() 
→ CombatSetup.InitializeCombat() 
→ CombatSetup.StartCombat() 
→ FightPreviewManager.StartFightPreview() 
→ FightPreviewUIManager.ShowFightPreview()
→ FightPreviewAnimator.StartPreviewAnimation()
→ [Animation sequence: Fade In → Display → Fade Out] 
→ FightPreviewManager.StartActualCombat() 
→ CombatManager.StartCombat()
```

## Styling Recommendations

### Canvas Settings
- **Canvas Scaler**: Scale With Screen Size
- **Reference Resolution**: 1920x1080
- **Sort Order**: High value (e.g., 100) to appear on top
- **CanvasGroup**: Required for fade animations

### Animation Curves
For professional feel, consider these curve types:
- **Fade In**: EaseOut curve for snappy appearance
- **Fade Out**: EaseIn curve for smooth disappearance
- **Scale**: EaseOutBack for subtle bounce effect

### Text Styling
- **Player Name**: Large, bold font (e.g., 48pt)
- **Versus Text**: Medium font (e.g., 36pt), colored accent
- **Pet Name**: Large, bold font (e.g., 48pt)

### Layout Suggestions
```
[Player Image]    [Player Name]
                     VS
               [Pet Name]    [Pet Image]
```

Or horizontally:
```
[Player Image] [Player Name] VS [Pet Name] [Pet Image]
```

### Image Settings
- **Player/Pet Images**: 
  - Size: 128x128 or 256x256
  - Preserve Aspect: True
  - Image Type: Simple

## API Usage

### Runtime Animation Control
```csharp
// Get the animator
FightPreviewAnimator animator = fightPreviewManager.GetComponent<FightPreviewAnimator>();

// Customize timing
animator.SetAnimationTiming(0.3f, 2.5f, 0.7f); // fade in, display, fade out

// Enable scale animation
animator.SetScaleAnimationEnabled(true);

// Get total duration
float totalDuration = animator.TotalDuration;
```

### Event Handling
```csharp
// Subscribe to UI manager events
fightPreviewUIManager.OnPreviewStarted += () => Debug.Log("Preview started!");
fightPreviewUIManager.OnPreviewCompleted += () => Debug.Log("Preview done!");

// Subscribe to animator events
animator.OnAnimationStarted += () => Debug.Log("Animation began!");
animator.OnDisplayPhaseStarted += () => Debug.Log("Now showing preview!");
animator.OnAnimationCompleted += () => Debug.Log("Animation finished!");
```

## Entity Sprite Integration
The `FightPreviewUIManager.GetEntitySprite()` method currently looks for:
1. `SpriteRenderer` component on the entity
2. `Image` component on the entity

You may need to modify this method based on how your entities store their display sprites.

## Testing
1. Start a multiplayer session
2. Enter lobby and ready all players
3. Click start - you should see the fight preview with smooth animations
4. Combat should begin automatically after the preview

## Troubleshooting

### Common Issues
- **No animation**: Ensure CanvasGroup is present on FightPreviewCanvas
- **Jerky animation**: Check animation curves and frame rate
- **Timing issues**: Verify FightPreviewAnimator is properly assigned to FightPreviewManager

### Debug Tools
- Use `FightPreviewManager.ValidateSetup()` to check component references
- Check `FightPreviewAnimator.ValidateConfiguration()` for animation settings
- Monitor console for detailed logging during preview sequence

### Performance
- Animation system is optimized for mobile with minimal allocations
- Uses coroutines rather than Update() for better performance
- CanvasGroup animation is GPU-accelerated

## Advanced Customization

### Custom Animation Curves
Create custom AnimationCurve assets in Unity and assign them to the animator for unique effects.

### Multiple Animation Profiles
You can swap animators at runtime for different preview styles:
```csharp
FightPreviewAnimator alternateAnimator = GetAlternateAnimator();
fightPreviewUIManager.SetAnimator(alternateAnimator);
```

### Integration with Other Systems
The separated architecture makes it easy to integrate with:
- Audio systems (trigger sounds on animation events)
- Particle effects (spawn effects during display phase)
- Analytics (track preview completion rates) 