# Deck Generator - Starter Deck Creation Tool

This Unity Editor script automatically generates **6 themed starter decks** (3 character decks + 3 pet decks) for your card game, following the "4 Attack + 4 Defense + 2 Special" formula similar to Slay the Spire.

## How to Use

1. Open Unity Editor
2. Go to **Tools → Generate Starter Decks** 
3. Choose your generation option:
   - **"Generate All Cards + Upgrades"**: Creates starter decks + 60 draft cards + upgraded versions
   - **"Generate Only Starter Decks"**: Just the 6 starter decks
   - **"Generate Only Draft Cards"**: Just the 60 draft cards
   - **"Generate Only Upgraded Cards"**: Creates upgraded versions of existing cards
4. Find generated assets in `Assets/Generated/Cards/` and `Assets/Generated/Decks/`
5. **Auto-populate CardDatabase**: Right-click your CardDatabase component → **"Auto-Populate Card Database"**

## Generated Deck Themes

### 🗡️ Character Decks

#### **Warrior Starter Deck** - Combo Combat
- **Theme**: Build combo points and unleash devastating finishers
- **Special Cards**:
  - **Combo Strike**: Deal 4 damage, builds combo for future plays
  - **Devastating Blow**: Deal 15 damage, requires combo to play (finisher)
- **Strategy**: Chain combo cards to enable powerful finishing moves

#### **🔮 Mage Starter Deck** - Spell Mastery
- **Theme**: Energy manipulation and conditional spell casting
- **Special Cards**:
  - **Mana Surge**: Restore 2 energy and draw 1 card (resource management)
  - **Conditional Fireball**: Deal 8 damage, or 12 if target is below 25 health
- **Strategy**: Manage resources efficiently and time spells for maximum impact

#### **⛪ Cleric Starter Deck** - Divine Support
- **Theme**: Healing and protective magic with strength enhancement
- **Special Cards**:
  - **Healing Light**: Heal 8 health and gain 2 Strength (hybrid heal/buff)
  - **Divine Protection**: Gain 8 Shield and apply Weak to opponent for 2 turns
- **Strategy**: Sustain through fights while weakening enemies and growing stronger

### 🐾 Pet Decks

#### **🐺 Beast Starter Deck** - Primal Aggression
- **Theme**: Strength stacking and aggressive scaling tactics
- **Special Cards**:
  - **Primal Rage**: Gain 3 Strength and 3 Thorns (buff + retaliation)
  - **Scaling Claw**: Deal damage that scales with cards played this fight
- **Strategy**: Build strength over time and punish attackers with thorns

#### **⚡ Elemental Starter Deck** - Status Control
- **Theme**: Status effects and conditional damage amplification
- **Special Cards**:
  - **Elemental Burst**: Deal 5 damage and apply Break for 2 turns
  - **Status Storm**: Stun for 1 turn, deal bonus damage if target has low health
- **Strategy**: Control the battlefield with debuffs and capitalize on weakened enemies

#### **🛡️ Guardian Starter Deck** - Defensive Mastery
- **Theme**: Defensive tactics with healing over time and adaptive shielding
- **Special Cards**:
  - **Regeneration**: Heal 3 health over time for 3 turns (sustained recovery)
  - **Guardian's Resolve**: Gain 12 Shield, +6 more if health is below 50%
- **Strategy**: Outlast opponents through superior defense and gradual healing

## Shared Basic Cards

All decks include the same foundation:
- **4x Basic Attack**: Deal 6 damage (1 energy, Attack type)
- **4x Basic Defend**: Gain 5 Shield (1 energy, Skill type)

## Technical Features

The generator demonstrates various CardData system capabilities:

### 🔧 Card Mechanics Used
- **Combo System**: Build/consume combo points (Warrior)
- **Conditional Effects**: Different outcomes based on game state (Mage, Elemental)
- **Status Effects**: Buffs, debuffs, and damage over time (All decks)
- **Scaling Effects**: Cards that grow stronger during the fight (Beast)
- **Multi-Target Effects**: Cards with multiple simultaneous effects (Cleric, Guardian)

### 🎯 Card Types Showcased
- **Attack**: Direct damage dealing
- **Skill**: Utility and defensive abilities  
- **Combo**: Cards that build combo points
- **Finisher**: High-power cards requiring combo
- **Spell**: Magical effects with conditions

### 📊 Status Effects Included
- **Strength**: Increases damage output
- **Shield**: Blocks incoming damage
- **Thorns**: Reflects damage to attackers
- **Weak**: Reduces enemy damage
- **Break**: Reduces enemy defense
- **Stun**: Prevents enemy actions
- **Heal Over Time**: Gradual health restoration

## Draft Card Pool (60 Additional Cards)

Each theme gets **10 advanced draft cards** that expand on their mechanics:

### 🗡️ **Warrior Draft Cards** - Advanced Combat
- **Berserker Strike**, **Execute**, **Whirlwind** (area damage)
- **Rage**, **Battle Frenzy** (strength stacking)
- **Combo Mastery**, **Perfect Strike** (combo synergy)
- **Intimidate**, **Warrior's Resolve**, **Cleave** (tactical options)

### 🔮 **Mage Draft Cards** - Spell Mastery  
- **Arcane Orb**, **Chain Lightning**, **Meteor** (spell variety)
- **Mana Burn**, **Time Warp**, **Spell Echo** (resource manipulation)
- **Frost Armor**, **Elemental Shield** (magical defense)
- **Dispel**, **Arcane Mastery** (spell enhancement)

### ⛪ **Cleric Draft Cards** - Divine Magic
- **Greater Heal**, **Resurrection**, **Sanctuary** (enhanced healing)
- **Blessed Strike**, **Smite**, **Divine Wrath** (holy damage)
- **Guardian Angel**, **Consecration** (protection magic)
- **Purify**, **Blessing of Might** (ally support)

### 🐺 **Beast Draft Cards** - Primal Evolution
- **Alpha Strike**, **Apex Predator**, **King of Beasts** (apex predator)
- **Pack Hunt**, **Blood Frenzy**, **Savage Charge** (hunting tactics)
- **Territorial Roar**, **Feral Instincts** (intimidation)
- **Primal Armor**, **Endless Hunger** (evolution)

### ⚡ **Elemental Draft Cards** - Elemental Mastery
- **Lightning Bolt**, **Inferno**, **Toxic Cloud** (elemental damage)
- **Storm Shield**, **Elemental Fusion** (elemental defense)
- **Chaos Magic**, **Dispelling Wind** (chaotic effects)
- **Elemental Mastery**, **Avatar of Elements** (transformation)

### 🛡️ **Guardian Draft Cards** - Ultimate Defense
- **Fortress**, **Shield Wall**, **Stalwart Defense** (impenetrable defense)
- **Protective Barrier**, **Life Link** (ally protection)
- **Counter Attack**, **Retribution** (defensive offense)
- **Guardian's Oath**, **Healing Sanctuary**, **Immortal Guardian** (transcendence)

## Card Upgrade System

Every generated card automatically gets an **upgraded version** with enhanced stats and effects:

### 🔧 **Upgrade Benefits**
- **Reduced Energy Cost**: Most cards cost 1 less energy (minimum 0)
- **Enhanced Effects**: 
  - Damage: +2 to +4 bonus damage
  - Healing: +2 to +3 bonus healing  
  - Shield: +2 to +3 bonus shield
  - Status Effects: +1 potency and +1 duration
  - Scaling: 20% better scaling multipliers
- **Improved Descriptions**: Clear "[UPGRADED]" indicators
- **Linked References**: Original cards automatically link to their upgraded versions

### 📈 **Upgrade Examples**
- **Basic Attack** (6 damage, 1 energy) → **Basic Attack+** (8 damage, 1 energy)
- **Devastating Blow** (15 damage, 2 energy) → **Devastating Blow+** (19 damage, 1 energy)
- **Healing Light** (8 heal + 2 strength, 2 energy) → **Healing Light+** (10 heal + 3 strength, 1 energy)
- **Primal Rage** (3 strength + 3 thorns, 2 energy) → **Primal Rage+** (4 strength + 4 thorns, 1 energy)

## File Output

Generated assets:
```
Assets/Generated/
├── Cards/
│   ├── BasicAttack.asset
│   ├── BasicDefend.asset
│   ├── [12 starter deck special cards]
│   ├── Draft/
│   │   ├── [60 advanced draft cards]
│   │   ├── WarriorBerserkerStrike.asset
│   │   ├── MageArcaneOrb.asset
│   │   ├── ClericGreaterHeal.asset
│   │   ├── BeastAlphaStrike.asset
│   │   ├── ElementalLightningBolt.asset
│   │   ├── GuardianFortress.asset
│   │   └── [54 more draft cards...]
│   └── Upgraded/
│       ├── [All cards with + versions]
│       ├── BasicAttack_Upgraded.asset
│       ├── BasicDefend_Upgraded.asset
│       ├── WarriorComboStrike_Upgraded.asset
│       └── [75+ more upgraded cards...]
└── Decks/
    ├── WarriorStarterDeck.asset
    ├── MageStarterDeck.asset
    ├── ClericStarterDeck.asset
    ├── BeastStarterDeck.asset
    ├── ElementalStarterDeck.asset
    └── GuardianStarterDeck.asset
```

## Integration with Character/Pet Systems

The generated DeckData assets can be directly assigned to:
- **CharacterData.StarterDeck** for character selection
- **PetData.StarterDeck** for pet selection

This provides immediate gameplay variety and demonstrates the full range of your card system's capabilities!

## 🗃️ CardDatabase Auto-Population

The **CardDatabase** component now includes editor tools for managing your card collection:

### **Auto-Populate Card Database**
- **Right-click** the CardDatabase component in Inspector
- Select **"Auto-Populate Card Database"**
- Automatically finds and adds **ALL** CardData assets in your project
- Assigns sequential IDs and validates each card
- **Perfect for after generating new cards!**

### **Validate Card Database** 
- **Right-click** the CardDatabase component
- Select **"Validate Card Database"**
- Checks for missing names, descriptions, negative costs, and missing effects
- Reports validation results with detailed issue list

### **Generate Card Summary**
- **Right-click** the CardDatabase component  
- Select **"Generate Card Summary"**
- Shows complete breakdown of your card collection:
  - Cards by type (Attack, Skill, Spell, etc.)
  - Cards by energy cost (0, 1, 2, 3+ energy)
  - Special mechanics count (Combo, Finisher, Conditional, Scaling)

### **Usage Workflow**
1. Generate cards with DeckGenerator
2. Auto-populate CardDatabase to include all new cards
3. Validate database to ensure card quality
4. Generate summary to review your collection
5. Use `CardDatabase.Instance.GetRandomCardsWithDuplicates(count)` for draft packs!

---

*Generated decks provide balanced starting points while showcasing different strategic approaches and mechanical depth.* 