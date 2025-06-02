# Card Creation Guide

## Overview
This guide explains how to create cards using the enhanced card system. The system has been simplified to avoid redundancy while supporting powerful mechanics.

## Quick Start: Using Simple Card Builder

For 90% of cards, use the **Simple Card Builder** (Tools → Card Builder → Simple Card Builder):

### Basic Cards
- **Attack Cards**: Deal damage to enemies
- **Defense Cards**: Gain shield or defensive benefits  
- **Healing Cards**: Restore health to yourself or allies
- **Status Cards**: Apply status effects like Weak, Break, Stun
- **Utility Cards**: Draw cards, restore energy, manipulate opponent's hand

### Advanced Features
- **Combo Cards**: Build combo when played (required for Finishers)
- **Finisher Cards**: Can only be played when you have combo
- **Elemental Cards**: Fire, Ice, Lightning, Void effects
- **Scaling Cards**: Power increases based on game state
- **Multi-Effect Cards**: Do multiple things at once

## Manual Card Creation

### Core Fields

#### Basic Info
- **Card Name**: Display name
- **Description**: What the card does
- **Card Artwork**: Visual representation

#### Core Mechanics  
- **Card Type**: Attack, Skill, Spell, Combo, Finisher, Stance, etc.
- **Effect Type**: What the card does (Damage, Heal, DrawCard, etc.)
- **Energy Cost**: Cost to play the card
- **Amount**: Power/potency of the effect
- **Duration**: ⚠️ **Only appears for status effects that need it**

#### Targeting
- **Target Type**: Who the card affects
  - `Self`: You
  - `Opponent`: Enemy 
  - `Ally`: Your ally (player ↔ pet)
  - `AllEnemies`: All opponents globally
  - `AllAllies`: All your allies globally
  - `All`: Everyone globally
  - `AllPlayers`: All players globally  
  - `AllPets`: All pets globally

### Advanced Features

#### Additional Effects
Check "Has Additional Effects" to make the card do multiple things:
- Each additional effect can have its own target
- Mix damage with healing, status effects, etc.
- Example: "Deal 4 damage and heal 2"

#### Conditional Behavior  
Check "Has Conditional Behavior" for cards that change based on conditions:
- If target health below X
- If you have X cards in hand
- If you've taken damage this turn
- If you're in a specific stance

#### Scaling & Growth
Check "Scales With Game State" for cards that get stronger:
- Scale with zero-cost cards played
- Scale with total damage dealt
- Scale with combo count
- Set maximum scaling to prevent overpowered effects

#### Combat Integration
- **Elemental Type**: Fire, Ice, Lightning, Void for special interactions
- **Builds Combo**: This card contributes to combo meter
- **Requires Combo**: ⚠️ **Only playable when you have active combo** (simplified!)

#### Stance & Persistent Effects
- **Changes Stance**: Switch to Aggressive, Defensive, Focused, etc.
- **Creates Persistent Effects**: Effects that last the entire fight

## Key Improvements

### ✅ Fixed Issues
1. **Duration Field**: Only shows for status effects that actually use duration
2. **No Redundant Effect Types**: Clean separation between WHAT happens (effect type) and WHO it affects (target type)
3. **Simplified Combo System**: Just "Requires Combo" - no confusing card type requirements
4. **Fixed List Issues**: Additional effects, scaling, etc. now work properly with perfect spacing
5. **No Boolean Flags**: Lists auto-detect when they have content - no more dual state management

### ✅ What Was Removed
- Redundant zone effect types (use `Damage` + `AllEnemies` instead of `ZoneDamageAll`)
- Complex combo requirements (just needs active combo)
- Manual boolean flags (lists check their own content)
- Inspector spacing issues (custom property drawers ensure proper layout)

### ✅ Clean Effect Type System
**Effect Types** describe **WHAT** happens:
- `Damage` - Deal damage
- `Heal` - Restore health  
- `ApplyWeak` - Apply weak status
- `DrawCard` - Draw cards

**Target Types** describe **WHO** it affects:
- `Opponent` → single enemy
- `AllEnemies` → all opponents globally
- `AllAllies` → all allies globally
- `All` → everyone globally

**Example**: Instead of `ZoneDamageAll` effect type, use `Damage` effect + `AllEnemies` target. Much cleaner!

### ✅ Conditional Field Visibility
Fields only appear when relevant:
- Duration only shows for status effects
- Stance options only show if "Changes Stance" is checked
- Scaling options only show if "Scales With Game State" is checked
- Additional effects only show if "Has Additional Effects" is checked

## Common Card Patterns

### Simple Attack
```
Card Type: Attack
Effect Type: Damage  
Amount: 4
Energy Cost: 2
Target Type: Opponent
```

### Healing with Shield  
```
Card Type: Skill
Effect Type: Heal
Amount: 3
Target Type: Self
Has Additional Effects: ✓
  - Effect: ApplyShield, Amount: 2, Target: Self
```

### Area Damage
```
Card Type: Spell
Effect Type: Damage
Amount: 2  
Target Type: AllEnemies
Energy Cost: 3
```

### Combo Finisher
```
Card Type: Finisher
Effect Type: Damage
Amount: 8
Requires Combo: ✓
Energy Cost: 3
```

### Scaling Spell
```
Card Type: Spell
Effect Type: Damage
Amount: 2
Scales With Game State: ✓
  - Scaling Type: ZeroCostCardsThisFight
  - Multiplier: 1.5
  - Max Scaling: 12
```

## Testing Your Cards

Use the **Card Factory** testing system:
1. Open TestCombat scene
2. Press F1 for debug menu
3. Use "Spawn Card" to test your creations
4. Enable "Return to Hand" mode for rapid testing

The system includes 80+ example cards showing every mechanic in action.

## Quick Start - 3 Easy Ways to Create Cards

### 1. 🎯 Simple Card Builder (Recommended for Beginners)
**Use this for 90% of your cards**

1. Go to `Tools > Card Builder > Simple Card Builder`
2. Choose a card template (Attack, Defense, Heal, Status, Utility)
3. Set the basic values (name, cost, power)
4. Optionally add advanced features (combo, scaling, secondary effects)
5. Click "Build Card"

**Perfect for:** Basic cards, learning the system, rapid prototyping

### 2. 📝 CardData Inspector (For Custom Cards)
**Use when you need more control**

1. Right-click in Project → `Create > Card Game > Card Data`
2. Fill out the organized sections in the inspector
3. Use the new clean interface with helpful tooltips

**Perfect for:** Custom mechanics, complex cards, fine-tuning

### 3. 🧪 Test Card Factory (For Testing)
**Use for comprehensive testing**

1. Go to `Tools > Card Factory > Create All Test Cards`
2. Use the pre-made test cards for reference
3. Copy and modify existing test cards

**Perfect for:** Testing mechanics, learning by example

---

## Understanding Card Structure

The new `CardData` is organized into clear sections:

### 🏷️ **Basic Card Info**
- **Card ID**: Unique identifier
- **Card Name**: Display name
- **Description**: What the card does
- **Card Artwork**: Visual representation

### ⚙️ **Core Mechanics** 
- **Card Type**: Attack, Skill, Spell, Combo, Finisher, etc.
- **Effect Type**: Damage, Heal, Shield, Status effects, etc.
- **Target Type**: Who the card affects
- **Amount**: Power/potency of the effect
- **Energy Cost**: How much energy to play
- **Duration**: How long status effects last

### ✨ **Special Features**
Simple toggles for common enhancements:
- **Multiple Effects**: Card does several things
- **Conditional Effects**: Different effects based on conditions
- **Elemental Type**: Fire, Ice, Lightning, Void
- **Combo System**: Builds or requires combos

### 🔧 **Advanced Features**
For complex cards (usually hidden):
- **Card Sequencing**: Requires specific cards played first
- **Zone Effects**: Affects ALL players globally
- **Persistent Effects**: Last the entire fight
- **Stance System**: Changes combat stance
- **Scaling Effects**: Power increases based on game state

---

## Common Card Creation Workflows

### 🗡️ **Creating a Basic Attack Card**

**Simple Builder Method:**
1. Open Simple Card Builder
2. Check "Create Basic Attack"
3. Set damage and cost
4. Done!

**Manual Method:**
1. Create new CardData
2. Set Card Type = Attack
3. Set Effect Type = Damage
4. Set Target Type = Opponent
5. Set Amount = damage value
6. Set Energy Cost

### 🛡️ **Creating a Defense Card**

**Simple Builder:**
1. Check "Create Basic Defense"
2. Set shield amount and cost

**Manual:**
1. Card Type = Skill
2. Effect Type = Apply Shield
3. Target Type = Self
4. Amount = shield value

### 💚 **Creating a Heal Card**

**Simple Builder:**
1. Check "Create Heal Card"
2. Set healing amount

**Manual:**
1. Card Type = Skill
2. Effect Type = Heal
3. Target Type = Self (or Ally)
4. Amount = healing value

### 😵 **Creating a Status Effect Card**

**Simple Builder:**
1. Check "Create Status Card"
2. Choose status type (Weak, Stun, Poison, etc.)
3. Set potency and duration

**Manual:**
1. Card Type = Skill
2. Effect Type = Apply[StatusName] (e.g., ApplyWeak)
3. Target Type = Opponent (usually)
4. Amount = potency
5. Duration = turns to last

---

## Advanced Card Features

### 🔄 **Combo System**
- **Combo Cards**: Build combo count when played
- **Finishers**: Require combo to be played, consume combo

```
// Make a combo card
card.MakeComboCard();

// Make a finisher
card.MakeFinisher(CardType.Combo); // Requires combo card first
```

### 📈 **Scaling Effects**
Cards that get stronger based on game state:

```
// Scale with zero-cost cards played
card.AddScaling(ScalingType.ZeroCostCardsThisTurn, 1.0f, 10);

// Scale with combo count
card.AddScaling(ScalingType.ComboCount, 1.5f, 15);
```

### 🎭 **Multiple Effects**
Cards that do several things:

```
// Add a secondary heal effect
card.AddSecondaryEffect(CardEffectType.Heal, 2, CardTargetType.Self);

// Add card draw
card.AddSecondaryEffect(CardEffectType.DrawCard, 1, CardTargetType.Self);
```

### 🌪️ **Zone Effects**
Cards that affect ALL players/pets globally:
- Check "Has Zone Effect" 
- Configure who is affected (all players, all pets, etc.)

### ⚡ **Elemental Types**
- **Fire**: Burn damage over time
- **Ice**: Slow effects
- **Lightning**: Chain to multiple targets
- **Void**: Corruption effects

---

## Card Balancing Guidelines

### 💰 **Energy Cost Guidelines**
- **0 Energy**: Weak effects, often with drawbacks
- **1 Energy**: Basic effects (2-3 damage, small heals)
- **2 Energy**: Standard effects (3-4 damage, moderate effects)
- **3 Energy**: Strong effects (5-6 damage, powerful status)
- **4+ Energy**: Very powerful effects, finishers

### 🎯 **Target Guidelines**
- **Self**: Defensive effects, buffs, heals
- **Opponent**: Damage, debuffs, disruption
- **Ally**: Support effects, heals, buffs
- **All**: Powerful effects with higher cost
- **Random**: Chaotic effects, usually cheaper

### ⏱️ **Duration Guidelines**
- **0**: Instant effects (damage, heal, draw)
- **1-2**: Short status effects
- **3-4**: Standard status effects
- **5+**: Long-term effects (expensive)

---

## Testing Your Cards

### 🧪 **Using TestCombat System**
1. Add TestCombat component to scene
2. Use `Tools > Test Combat > Open Card Spawner Window`
3. Search for your card and spawn it
4. Enable "Return to Hand Mode" for repeated testing

### 🔍 **Card Validation**
The system automatically validates:
- Energy costs vs. effects
- Target requirements
- Sequence requirements (for advanced cards)
- Duration values

### 📊 **Debugging**
- Use tracking properties to monitor card performance
- Check EntityTracker for combat statistics
- Use TestCombat logging for detailed state info

---

## Common Mistakes & Solutions

### ❌ **Problem**: Card is too complex
**✅ Solution**: Use Simple Card Builder first, add complexity gradually

### ❌ **Problem**: Card doesn't work as expected
**✅ Solution**: Test with simpler version first, check target types

### ❌ **Problem**: Overwhelmed by options
**✅ Solution**: Hide advanced sections, focus on Core Mechanics only

### ❌ **Problem**: Card balance issues
**✅ Solution**: Follow energy cost guidelines, test extensively

---

## Card Creation Checklist

**Before Creating:**
- [ ] What is the card's main purpose?
- [ ] What energy cost makes sense?
- [ ] Who should it target?

**Basic Setup:**
- [ ] Card name and description
- [ ] Card type (Attack/Skill/Spell)
- [ ] Effect type and amount
- [ ] Energy cost and target

**Testing:**
- [ ] Spawn card in TestCombat
- [ ] Test different scenarios
- [ ] Verify targeting works
- [ ] Check energy cost feels fair

**Polish:**
- [ ] Add artwork if available
- [ ] Fine-tune description
- [ ] Add to appropriate card packs

---

## Quick Reference

### Most Common Effect Types
- `Damage` - Deal damage
- `Heal` - Restore health
- `ApplyShield` - Absorb damage
- `ApplyWeak` - Reduce damage dealt
- `ApplyBreak` - Reduce armor
- `ApplyStun` - Skip next turn
- `DrawCard` - Draw cards
- `RestoreEnergy` - Gain energy

### Most Common Target Types
- `Self` - The card player
- `Opponent` - Enemy player/pet
- `Ally` - Friendly player/pet
- `AllEnemies` - All opponents
- `All` - Everyone

### Quick Builder Templates
1. **Basic Attack**: 3 damage, 2 cost
2. **Basic Heal**: 4 healing, 2 cost  
3. **Basic Shield**: 6 shield, 2 cost
4. **Weak Status**: 2 weak for 3 turns, 1 cost
5. **Card Draw**: Draw 2 cards, 1 cost

Use these as starting points and modify as needed! 