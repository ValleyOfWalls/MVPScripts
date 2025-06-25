# Health & Energy Bar Setup Guide

This guide explains how to set up the new mask-based health and energy bars that shrink from right to left as values decrease.

## Overview

The new system uses a mask-based approach instead of the simple `fillAmount` method. This provides better visual control and allows for custom bar textures that shrink naturally from right to left.

## GameObject Hierarchy Setup

### For Each Bar (Health and Energy)

Create the following hierarchy in your UI:

```
YourStatsPanel
├── HealthBarContainer
│   ├── HealthBarBackground (Image - empty bar texture)
│   └── HealthBarForeground (Image + Mask)
│       └── HealthBarFill (Image - full bar texture)
├── EnergyBarContainer
│   ├── EnergyBarBackground (Image - empty bar texture)
│   └── EnergyBarForeground (Image + Mask)
│       └── EnergyBarFill (Image - full bar texture)
├── HealthText (TextMeshProUGUI)
└── EnergyText (TextMeshProUGUI)
```

## Component Configuration

### 1. HealthBarBackground / EnergyBarBackground
- **Component**: `Image`
- **Sprite**: Your empty/background bar texture
- **Image Type**: `Sliced` (if using 9-slice sprites)
- **Color**: Gray or your preferred background color

### 2. HealthBarForeground / EnergyBarForeground
- **Component**: `Image` (optional - can be transparent)
- **Component**: `Mask` ✓
- **Settings**: 
  - Check "Show Mask Graphic" if you want this visible
  - Leave unchecked for invisible mask
- **Rect Transform**: 
  - Anchors: Stretch both horizontally and vertically
  - Left: 0, Right: 0, Top: 0, Bottom: 0

### 3. HealthBarFill / EnergyBarFill
- **Component**: `Image`
- **Sprite**: Your full bar texture (the colorful fill)
- **Image Type**: `Sliced` (if using 9-slice sprites)
- **Color**: Red for health, Blue for energy (or your preferred colors)
- **Rect Transform**: 
  - Anchors: Left side anchored (0, 0.5, 0, 0.5)
  - Pivot: (0, 0.5) - Important! This makes it shrink from the right
  - Left: 0, Width: Full bar width (e.g., 200)

## Script Setup

### 1. Add the HealthEnergyBarController Component

1. Add the `HealthEnergyBarController` script to your stats panel GameObject
2. Configure the references in the inspector:

```
Health Bar Configuration:
├── Health Bar Fill: Drag HealthBarFill RectTransform here
├── Health Bar Fill Image: Drag HealthBarFill Image here
├── Health Text: Drag HealthText TextMeshProUGUI here
└── Health Bar Width: Set to your bar's full width (e.g., 200)

Energy Bar Configuration:
├── Energy Bar Fill: Drag EnergyBarFill RectTransform here
├── Energy Bar Fill Image: Drag EnergyBarFill Image here
├── Energy Text: Drag EnergyText TextMeshProUGUI here
└── Energy Bar Width: Set to your bar's full width (e.g., 200)
```

### 2. Update Existing Controllers

#### For EntityStatsUIController:
1. Add the `HealthEnergyBarController` component to the same GameObject
2. In the inspector, assign the `Bar Controller` field

#### For OwnPetStatsDisplay:
1. Add the `HealthEnergyBarController` component to the same GameObject
2. In the inspector, assign the `Bar Controller` field

## Configuration Options

### Animation Settings
- **Use Animation**: Enable smooth bar transitions
- **Animation Speed**: How fast bars animate (2.0 recommended)
- **Animation Curve**: Custom easing curve for animations

### Color Settings
- **Health Bar Color**: Normal health color (default: red)
- **Energy Bar Color**: Energy color (default: blue)
- **Low Health Color**: Color when health is low (default: orange)
- **Critical Health Color**: Color when health is critical (default: dark red)
- **Low Health Threshold**: Percentage for low health (0.3 = 30%)
- **Critical Health Threshold**: Percentage for critical health (0.15 = 15%)

## Auto-Setup Feature

The controller includes auto-discovery of components. If you follow the naming convention, it will automatically find:

- `HealthBarContainer/HealthBarForeground/HealthBarFill`
- `EnergyBarContainer/EnergyBarForeground/EnergyBarFill`
- `HealthText`
- `EnergyText`

## Important Notes

### Rect Transform Settings for Fill Images
The most critical part is setting up the HealthBarFill and EnergyBarFill RectTransforms correctly:

1. **Anchors**: 
   - Min: (0, 0) - Left bottom
   - Max: (0, 1) - Left top
   - This anchors to the left edge and stretches vertically
2. **Pivot**: Set to (0, 0.5) - This makes scaling happen from the left edge
3. **Position**: Set X to 0 to align with the left edge
4. **Size**: Set width to your full bar width, height to 0 (stretches to parent)

### Mask Component
The Mask component on the foreground object will clip the fill image, creating the shrinking effect. Make sure:
- The mask object completely contains the fill object
- The fill object is a child of the masked object

## Testing

1. **In Play Mode**: Damage/heal your character to see the health bar shrink/grow
2. **Use Energy**: Play cards or abilities to see the energy bar change
3. **Check Animation**: Verify smooth transitions between values
4. **Color Changes**: Test low health color transitions

## Troubleshooting

### Bar Not Shrinking
- Check that the fill RectTransform has the correct anchor/pivot settings
- Ensure the Mask component is properly configured
- Verify the fill image is a child of the masked object

### Animation Issues
- Check that `Use Animation` is enabled
- Adjust `Animation Speed` if too fast/slow
- Ensure the controller's Update method is being called

### Colors Not Changing
- Verify the Image components are assigned in the controller
- Check that the color threshold values are set correctly
- Ensure the controller is receiving health updates

### Auto-Discovery Not Working
- Check that your GameObject hierarchy matches the expected naming
- Manually assign components in the inspector if auto-discovery fails
- Enable debug logging to see what components are found

## Legacy Compatibility

The updated controllers maintain backward compatibility. If no `HealthEnergyBarController` is assigned, they will fall back to the original `fillAmount` method with the legacy Image components. 