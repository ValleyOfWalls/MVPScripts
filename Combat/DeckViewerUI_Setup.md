# Deck Viewer UI Setup Guide

## Overview
This guide explains how to set up the UI elements for the DeckViewerManager in Unity.

## Required UI Structure

### 1. Combat Canvas Hierarchy
```
CombatCanvas
├── CombatCanvasManager (script)
├── DeckViewerManager (script) 
├── ExistingCombatUI
└── DeckViewerUI
    ├── DeckViewButtons (Panel)
    │   ├── ViewMyDeckButton (Button)
    │   ├── ViewOpponentDeckButton (Button)
    │   └── ViewAllyDeckButton (Button)
    └── DeckViewPanel (Panel) [Initially Inactive]
        ├── DeckViewHeader (Panel)
        │   ├── DeckViewTitle (TextMeshPro)
        │   └── CloseDeckViewButton (Button)
        └── DeckViewScrollRect (ScrollRect)
            └── DeckViewContainer (Transform)
```

### 2. Component Assignments

#### DeckViewerManager Script Fields:
- **deckViewPanel**: Assign the "DeckViewPanel" GameObject
- **deckViewContainer**: Assign the "DeckViewContainer" Transform
- **closeDeckViewButton**: Assign the "CloseDeckViewButton" Button
- **deckViewTitle**: Assign the "DeckViewTitle" TextMeshPro
- **deckViewScrollRect**: Assign the "DeckViewScrollRect" ScrollRect
- **viewMyDeckButton**: Assign the "ViewMyDeckButton" Button
- **viewOpponentDeckButton**: Assign the "ViewOpponentDeckButton" Button
- **viewAllyDeckButton**: Assign the "ViewAllyDeckButton" Button

#### CombatCanvasManager Script Fields:
- **deckViewerManager**: Assign the DeckViewerManager component

### 3. UI Element Details

#### Deck View Buttons Panel
- Position: Bottom or side of combat UI
- Layout: Horizontal or vertical button group
- Button Text Examples:
  - "View My Deck"
  - "View Opponent Deck"  
  - "View Ally Deck"

#### Deck View Panel
- Position: Center of screen (overlay)
- Size: Large enough to display card grid
- Background: Semi-transparent dark panel
- Initially: SetActive(false)

#### Deck View Container
- Parent: Content of ScrollRect
- Layout: Manual positioning (handled by script)
- Size: Will be adjusted dynamically based on card count

#### Close Button
- Position: Top-right corner of deck view panel
- Text: "X" or "Close"

### 4. Recommended UI Settings

#### Deck View Panel
- Canvas Group Component (for fade in/out animations)
- Alpha: 0.9
- Blocks Raycasts: true
- Interactable: true

#### ScrollRect Settings
- Content: DeckViewContainer
- Horizontal: false
- Vertical: true
- Movement Type: Elastic
- Scrollbar Visibility: Auto Hide

#### Button Settings
- Transition: Color Tint
- Interactable: true (will be controlled by script)
- Navigation: Automatic

### 5. Optional Enhancements

#### Card Display Settings (in DeckViewerManager)
- **cardSpacing**: (1.2, -1.5, 0) for grid layout
- **cardScale**: 0.8 for smaller display cards
- **cardsPerRow**: 6 for good visibility

#### Visual Effects
- Add fade in/out animations using DOTween
- Add card hover effects (disabled in view mode)
- Add background blur when deck view is open

### 6. Testing Setup

1. Create the UI hierarchy as described above
2. Assign all references in the DeckViewerManager
3. Ensure entities have NetworkEntityDeck components
4. Test in play mode with multiple entities
5. Verify spectating updates deck viewer correctly

### 7. Integration Points

The DeckViewerManager integrates with:
- **FightManager**: Gets current viewed combat entities
- **NetworkEntityDeck**: Reads card collections
- **CardSpawner**: Spawns cards for viewing
- **CombatCanvasManager**: Updates when viewed combat changes
- **CombatSpectatorManager**: Closes deck view when switching fights

## Notes

- Cards spawned for deck viewing are temporary and unowned
- Card interactions are disabled in view mode
- Deck view automatically closes when switching fights
- Button states update based on available entities in viewed combat
- Works with spectating - shows decks for currently viewed fight 