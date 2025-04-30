# Implemented Changes

## Pet Deck System
1. Added persistent decks for pets similar to players
   - Added `persistentDeckCardIDs` SyncList to `Pet.cs`
   - Added methods to initialize pet deck from templates
   - Added method to create a runtime deck for combat

2. Added ability for pets to draw cards from their deck in combat
   - Added `DrawCardFromDeck` method in `Pet.cs`
   - Implemented deck generation for pets in `CombatManager.cs`

3. Created a dedicated `PetHand.cs` class to manage and display pet cards
   - Similar to `PlayerHand` but with pet-specific logic
   - Cards are visible but not interactive
   - Synchronized across the network

## Network Card Visibility
1. Modified `PlayerHand.cs` to show cards for all players
   - All cards are now visible in the hierarchy for all players
   - Cards are only interactive for the owner player
   - Added proper network synchronization of cards using SyncLists

2. Added networking features:
   - Added `syncedCardIDs` to track card state across network
   - Added `RpcAddCardToObservers` to show cards to all players
   - Added `RpcRemoveCardFromHand` for synchronized card removal
   - Added proper callbacks to handle synced card changes

## Integration with Combat System
1. Updated `CombatManager.cs` to set up pet decks and hands
   - Creates runtime decks for pets during combat setup
   - Instantiates and initializes PetHand components
   - Draws initial hands for pets

2. Added proper initialization of pet decks from templates
   - Used `DeckManager` to get appropriate pet deck templates

## Additional Changes
1. Fixed edge cases and error handling
   - Added fallback to starter decks when needed
   - Added proper logging for debugging
   - Added null reference checks

## Usage
The changes maintain the existing architecture while extending it to support pets. Players and pets now both have their own decks and can draw cards during combat. All players can see everyone's cards in the hierarchy view, but can only interact with their own. 

# Pet and Combat Pet Architecture Changes

## Overview
We've restructured the pet system to follow the same pattern as players, with a clear separation between persistent pets (Pet) and combat-specific instances (CombatPet). This approach provides better consistency, cleaner code organization, and makes it easier to maintain persistent pet data while having specialized combat functionality.

## Key Changes

### 1. Persistent Pet
- Modified `Pet.cs` to focus purely on persistent data (stats, appearance, deck)
- Pet is now created when a player is initialized and persists throughout gameplay
- Pet stores the persistent deck cards in `persistentDeckCardIDs`
- Pet is attached to the player via `NetworkPlayer.playerPet` SyncVar

### 2. Combat-Specific Pet
- Created new `CombatPet.cs` for combat-specific functionality
- CombatPet references the persistent Pet via `referencePet`
- CombatPet implements the ICombatant interface with combat methods
- CombatPet creates its runtime deck from the persistent Pet's deck data
- CombatPet handles combat-specific functionality (taking damage, drawing cards, etc.)

### 3. Player Initialization
- Updated `NetworkPlayer.cs` to create and own a persistent Pet
- Added `InitializePlayerPet()` method that creates the Pet when the player is initialized
- Player stores a reference to its Pet using a SyncVar for network synchronization

### 4. Combat Management
- Updated `CombatManager.cs` to work with the new architecture
- CombatManager now creates CombatPets during combat setup
- CombatPets reference the persistent Pets
- CombatManager initializes CombatPets with references to their persistent counterparts
- Updated CombatData to store CombatPet references instead of Pet

### 5. Deck Management
- Persistent decks remain with the Pet
- Combat-specific runtime decks are created by CombatPet during initialization
- CombatPet now handles drawing cards from its runtime deck
- PetHand interacts with CombatPet instead of directly with Pet

### 6. Infrastructure
- Created `PetPrefabManager.cs` to handle pet prefab selection and instantiation
- PetPrefabManager helps NetworkPlayer create persistent Pets
- PetHand has been updated to work with CombatPet instead of Pet

## Benefits
1. **Clear Separation of Concerns**: Persistent data vs. combat functionality
2. **Consistent Architecture**: Follows the same pattern as Player/CombatPlayer
3. **Better Data Persistence**: Pet data persists between combat sessions
4. **Specialized Combat Logic**: Combat-specific code is contained in CombatPet
5. **Network Optimization**: Only combat-specific data needs to be synced during combat

## Usage
The new architecture maintains the existing deck system for pets but with a clearer separation:
1. Players are created with a persistent Pet
2. The Pet has a persistent deck that follows it
3. During combat, a temporary CombatPet is created with a runtime deck
4. The CombatPet draws from its runtime deck
5. All players can see cards in the pet's hand 

# Combat Scene Canvas Enhancements

## Multi-Fight View Functionality
1. Added `CombatSceneCanvas.cs` to manage visibility of networked combat entities
   - By default shows the local player's fight
   - Provides ability to cycle through viewing other active fights
   - Works with NetworkTransform and NetworkAnimation components
   - Automatically positions combat entities in the appropriate UI containers

2. Added fight cycling capabilities
   - Added a "View Next Battle" button to the UI
   - Button cycles through all active fights in the network
   - Preserves the network state of all entities (only changes visibility)
   - Updates battle info text to show current players being viewed

3. Modified `CombatManager.cs` to support visibility toggling
   - Added `GetActiveCombats()` method to expose fight data
   - Maintains a dictionary of all active combat data
   - Allows the canvas to determine which fights to show/hide

4. Enhanced `CombatSceneCanvasBuilder.cs`
   - Updated to create the "View Next Battle" button
   - Positions the button in the battle info panel
   - Automatically adds the CombatSceneCanvas component

## Usage
The CombatSceneCanvas manages the visibility of networked combat entities (players, pets, and cards) based on which fight the local player is viewing. By default, it shows the local player's fight, but players can press the "View Next Battle" button to cycle through and observe other active fights. 

All entities use NetworkTransform and NetworkAnimation components, but are only visible when the player cycles to their specific fight. This approach optimizes performance while allowing players to spectate all ongoing battles. 