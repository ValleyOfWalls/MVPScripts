# Attack Effects System Setup Guide

This guide explains how to set up and use the new attack effects system for your 3D MVP game.

## System Overview

The attack effects system consists of three main components:

1. **AttackEffectSource.cs** - Attach to character/pet prefabs to define effect origin points
2. **AttackEffectManager.cs** - Scene singleton that manages particle effects
3. **Enhanced HandleCardPlay.cs** - Triggers effects during combat
4. **Enhanced NetworkEntityAnimator.cs** - Handles attack animations

## Step 1: Setup AttackEffectSource on Characters/Pets

### Add Component to Prefabs
1. Open your character and pet prefabs
2. Add the `AttackEffectSource` component alongside `CharacterData` or `PetData`
3. The component will automatically discover visual components and attempt to find effect points

### Automatic Discovery
The component will search for transforms with these names:
- **Melee**: "MeleePoint", "WeaponPoint", "RightHand", "Hand_R"
- **Ranged**: "RangedPoint", "BowPoint", "LeftHand", "Hand_L"  
- **Magic**: "MagicPoint", "StaffPoint", "Chest", "Head"
- **Default**: "AttackPoint", "EffectPoint", "Center"

### Manual Setup (Optional)
1. Create empty GameObjects as children of your model
2. Position them where you want effects to originate (weapon tips, hands, etc.)
3. Name them according to the patterns above
4. Use the "Discover Effect Points" context menu to refresh

### Visual Debugging
- Enable "Show Gizmos" to see effect points in the Scene view
- Different colors represent different attack types:
  - Red = Melee
  - Blue = Ranged  
  - Magenta = Magic
  - Yellow = Default

## Step 2: Create Particle Effect Prefabs

Create particle system prefabs for each attack type:

### Basic Particle System Setup
1. Create empty GameObject
2. Add Particle System component
3. Configure for your desired effect:
   - Melee: Sharp, quick bursts
   - Ranged: Projectile-like with trails
   - Magic: Mystical, glowing effects
   - Default: General attack effect

### Recommended Settings
```
Shape: Cone or Sphere
Start Lifetime: 0.5-2 seconds
Start Speed: 2-10
Start Size: 0.1-0.5
Start Color: Match attack type theme
Emission Rate: 20-100
```

### Effect Movement
The AttackEffectManager will handle:
- Positioning the effect at source point
- Moving it toward the target
- Rotating to face movement direction
- Cleanup when complete

## Step 3: Setup AttackEffectManager in Scene

### Create Manager GameObject
1. Create empty GameObject named "AttackEffectManager"
2. Add `AttackEffectManager` component
3. Add `NetworkObject` component (required for FishNet)

### Assign Effect Prefabs
1. Drag your particle prefabs to the appropriate slots:
   - Melee Effect Prefab
   - Ranged Effect Prefab
   - Magic Effect Prefab
   - Default Effect Prefab

### Configure Settings
- **Pool Size**: Number of each effect to pre-instantiate (default: 20)
- **Default Effect Duration**: How long effects take to travel (default: 2s)
- **Speed Curve**: Animation curve for effect movement timing

## Step 4: Update Animator Controllers

### Add Animation Triggers
Your Animator Controllers need these triggers:
- `Idle` (existing)
- `Attack` (new)
- `TakeDamage` (new)
- `Defeated` (new)

### Create Animation States
1. **Attack State**: Quick attack animation
2. **Take Damage State**: Brief damage reaction
3. **Defeated State**: Defeat/death animation

### Transitions
- Any State → Attack (via Attack trigger)
- Any State → TakeDamage (via TakeDamage trigger)
- Attack → Idle (exit time or condition)
- TakeDamage → Idle (exit time or condition)

## Step 5: Configure Card Effects

### Setting Up Card Visual Effects

#### Option 1: Custom Prefabs Per Card (Recommended)
1. Open your card data assets
2. Expand the "Visual Effects" section in each `CardEffect`
3. Choose animation behavior and assign custom prefabs:
   - **Custom Effect Prefab**: Drag your specific effect prefab here
   - **Instant Effect Prefab**: For instant effects (OnSourceOnly/InstantOnTarget)
   - **Fallback Attack Type**: Used if custom prefab isn't found

#### Option 2: Use AttackEffectManager Defaults
1. Leave custom prefabs empty
2. Effects will use the AttackEffectManager's assigned prefabs based on attack type
3. Good for prototyping or when you want consistent effects across cards

### Example Configurations

**Basic Attack Card with Custom Effect:**
```
Effect 1 (Damage):
- Animation Behavior: ProjectileFromSource
- Custom Effect Prefab: MyFireballEffect
- Fallback Attack Type: Magic
- Custom Duration: 1.5
- Animation Delay: 0
```

**Multi-Effect Card (Damage + Stun):**
```
Effect 1 (Damage):
- Animation Behavior: ProjectileFromSource
- Custom Effect Prefab: MySlashEffect
- Fallback Attack Type: Melee
- Animation Delay: 0

Effect 2 (Apply Stun):
- Animation Behavior: InstantOnTarget
- Instant Effect Prefab: MyStunBurstEffect
- Animation Delay: 1.0 (after damage hits)
```

**Self-Buff Card:**
```
Effect 1 (Apply Strength):
- Animation Behavior: OnSourceOnly
- Instant Effect Prefab: MyStrengthAuraEffect
- Animation Delay: 0
```

**Using AttackEffectManager Defaults:**
```
Effect 1 (Damage):
- Animation Behavior: Auto
- Custom Effect Prefab: (leave empty)
- Fallback Attack Type: Default
```

## Step 6: Test the System

### In-Editor Testing
1. Use the AttackEffectManager's "Test Effect" context menu
2. Set test source and target transforms
3. Choose attack type to test

### Runtime Testing
1. Play combat scene
2. Play attack cards
3. Observe:
   - Source entity plays attack animation
   - Particle effect travels from source to target
   - Target entity plays damage animation

## Customization Options

### Card Effect Animation Behaviors
Each `CardEffect` now has visual animation settings:

**Animation Behavior Options:**
- **Auto**: Automatically determines behavior based on effect type
- **None**: No visual effect (silent)
- **InstantOnTarget**: Effect appears instantly on target
- **ProjectileFromSource**: Effect travels from source to target
- **OnSourceOnly**: Effect plays on source entity only
- **AreaEffect**: Area effect for multiple targets
- **BeamToTarget**: Continuous beam effect

**Additional Settings:**
- **Override Attack Type**: Force specific attack type (Melee/Ranged/Magic/Default)
- **Custom Duration**: Override default animation duration
- **Animation Delay**: Delay before effect starts (useful for sequencing)

### Auto-Detection Fallback
When using "Auto" behavior, the system determines attack type:
- Words like "spell", "magic", "bolt" → Magic
- Words like "shot", "arrow", "projectile" → Ranged  
- Words like "strike", "slash", "punch" → Melee
- Everything else → Default

### Custom Effect Points
Create child GameObjects on your models for precise effect positioning:
```
Character/
├── Model/
│   ├── RightHand/
│   │   └── WeaponPoint (for melee attacks)
│   ├── LeftHand/
│   │   └── BowPoint (for ranged attacks)
│   └── Chest/
│       └── MagicPoint (for magic attacks)
```

### Performance Optimization
- Adjust pool sizes based on expected concurrent effects
- Use simpler particle systems for mobile platforms
- Consider LOD system for distant effects

## Troubleshooting

### No Effects Playing
1. Check AttackEffectManager is in scene with NetworkObject
2. Verify effect prefabs are assigned
3. Ensure AttackEffectSource components are on characters/pets
4. Check console for error messages

### Effects Not Moving
1. Verify effect duration > 0
2. Check source and target positions are different
3. Ensure speed curve has proper values (0 to 1)

### Animation Issues
1. Check Animator Controller has required triggers
2. Verify NetworkEntityAnimator references correct animator
3. Check animation trigger names match script settings

### Network Issues
1. AttackEffectManager must be a NetworkObject
2. Effects only trigger on server
3. Check FishNet setup is correct

## Advanced Features

### Custom Effect Types
Extend `AttackEffectSource.AttackType` enum and add corresponding logic in:
- `AttackEffectManager.effectPrefabs` dictionary
- `HandleCardPlay.DetermineAttackType()` method

### Timing Synchronization
Adjust delay in `HandleCardPlay.TriggerDamageAnimationDelayed()` to match your effect travel time.

### Sound Integration
Add AudioSource components to effect prefabs for impact sounds.

## Files Modified/Created

- ✅ `AttackEffectSource.cs` (new)
- ✅ `AttackEffectManager.cs` (new)  
- ✅ `ReadOnlyAttribute.cs` (new)
- ✅ `HandleCardPlay.cs` (enhanced)
- ✅ `NetworkEntityAnimator.cs` (enhanced)

The system is now ready for use! Remember to assign the effect prefabs and add the manager to your combat scenes. 