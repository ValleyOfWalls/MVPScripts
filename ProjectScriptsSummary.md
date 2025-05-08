# Project Scripts Summary

This document summarizes the C# scripts created for the project, outlining their purpose, key components, and interactions.

## UI and Scene Management

### 1. `StartScreenManager.cs`
- **Purpose**: Manages the initial start screen UI.
- **Attached to**: `StartScreenCanvas` GameObject.
- **Key Features**:
    - Reference to a "Start" button.
    - On start button press:
        - Calls `SteamNetworkIntegration.Instance.RequestLobbiesList()` to attempt Steam lobby searching/joining/hosting.
        - Activates the `LobbyCanvas`.
        - Deactivates the `StartScreenCanvas`.
- **Dependencies**: `SteamNetworkIntegration`.

### 2. `LobbyManagerScript.cs`
- **Purpose**: Manages the player lobby UI and logic (ready status, player list, starting game).
- **Attached to**: `LobbyCanvas` GameObject.
- **Inherits**: `NetworkBehaviour`.
- **Key Features**:
    - References "Ready" and "Start Game" buttons, and `playerListText` TMP component.
    - Uses Dictionaries (`playerReadyStates`, `playerDisplayNames`) keyed by `NetworkConnection` to track player state on the server.
    - `OnStartClient()`: Client calls `CmdServerAddPlayer` to register itself with the server, attempting to use Steam name.
    - `CmdServerAddPlayer` (ServerRpc): Adds player to server lists (`connectedPlayers`, `playerReadyStates`, `playerDisplayNames`).
    - `OnReadyButtonPressed()`: Client calls `CmdTogglePlayerReadyState`.
    - `CmdTogglePlayerReadyState` (ServerRpc): Toggles the calling player's ready status in `playerReadyStates`.
    - `BroadcastFullPlayerList` (Server): Constructs display names with ready status and sends to all clients via `TargetRpcUpdatePlayerListUI`.
    - `TargetRpcUpdatePlayerListUI` (TargetRpc): Updates the `playerListText` UI on the client.
    - `CheckAllPlayersReady()` (Server): Enables the server's "Start Game" button interactable state if all connected players are ready. Calls `RpcUpdateStartButtonState`.
    - `RpcUpdateStartButtonState` (ObserversRpc): Updates the interactable state of the "Start Game" button on all clients based on server's decision.
    - `OnStartButtonPressed()` (Server-side check): If conditions met, calls `RpcStartGame`.
    - `RpcStartGame` (ObserversRpc): Deactivates `LobbyCanvas` and activates `CombatCanvas`.
    - `ServerHandlePlayerDisconnect()` (Server): Removes player data, updates lists, called by `SteamNetworkIntegration`.
    - `PrepareUIForLobbyJoin()`: Local UI setup when joining.
- **Dependencies**: `FishNet.NetworkManager`, `SteamNetworkIntegration` (for names), `CombatCanvas`.

### 3. `CombatCanvasManager.cs`
- **Purpose**: Manages the UI elements specifically for the combat scene on each client.
- **Attached to**: `CombatCanvas` GameObject (not networked itself).
- **Key Features**:
    - References UI elements for player/pet info, hand, deck, discard, end turn button.
    - `SetupCombatUI()`: Called locally (potentially triggered by `CombatSetup` RPC) to initialize the UI for the local player.
        - Finds local `NetworkPlayer` and opponent `NetworkPet` via `FightManager`.
        - Subscribes to `SyncVar`/`SyncList` changes on entities for UI updates.
    - `RenderHand()`: Instantiates/updates card visuals in hand.
    - `SetEndTurnButtonInteractable()`: Controls end turn button.
- **Dependencies**: `FightManager`, `CombatManager`, `NetworkPlayer`, `NetworkPet`, `CombatSetup` (for `cardGamePrefab`).

### 4. `DraftCanvasManager.cs`
- **Purpose**: Manages the UI elements for the draft phase.
- **Attached to**: `DraftCanvas` GameObject.
- **Key Features**:
    - References UI elements for draft packs, card shop, artifact shop, and player deck.
    - Called by `DraftSetup` to initialize the draft UI.
    - Displays draft packs, card shop, and artifact shop areas.
    - Handles UI updates when cards are drafted or purchased.
- **Dependencies**: `DraftSetup`, `DraftManager`, `NetworkPlayer`.

## Networking and Core Logic

### 5. `SteamNetworkIntegration.cs` (Replaces `SteamManager` and `SteamLobbyManager`)
- **Purpose**: Central Steamworks integration hub (Initialization, Lobby Management) and bridge to FishNet connection logic.
- **Inherits**: `MonoBehaviour`.
- **Singleton**: Yes (`Instance`).
- **Key Features**:
    - **Steamworks Integration**:
        - Initializes `SteamAPI` on `Awake()`.
        - Handles Steam Callbacks (`LobbyCreated_t`, `LobbyEnter_t`, `LobbyMatchList_t`, `LobbyDataUpdate_t`, etc.).
        - `RequestLobbiesList()`: Searches for lobbies; joins first found or calls `CreateLobby()`.
        - `CreateLobby()`: Creates a public Steam lobby, sets metadata.
        - `JoinLobby(CSteamID)`: Joins an existing Steam lobby.
        - `LeaveLobby()`: Leaves current Steam lobby and stops FishNet connection.
    - **FishNet Integration**:
        - Caches `NetworkManager` reference.
        - Gets reference to `PlayerSpawner` component on the same GameObject.
        - Starts FishNet host (server+client) in `OnLobbyCreatedCallback`.
        - Starts FishNet client in `OnLobbyEnteredCallback` (for joining clients).
        - Calls `PlayerSpawner.SpawnPlayerForConnection(conn)` for new clients.
        - Calls `LobbyManagerScript.ServerHandlePlayerDisconnect()` when a remote client disconnects.
    - **Prefabs**:
        - `[SerializeField] public GameObject NetworkPlayerPrefab;`
        - `[SerializeField] public GameObject NetworkPetPrefab;`
    - Provides player/friend name retrieval (`GetPlayerName`, `GetFriendName`).
- **Dependencies**: `Steamworks.NET`, `FishNet.NetworkManager`, `PlayerSpawner`, `LobbyManagerScript` (to notify on disconnect).

### 6. `PlayerSpawner.cs`
- **Purpose**: Handles the server-side spawning logic for `NetworkPlayer` and `NetworkPet`.
- **Inherits**: `MonoBehaviour`.
- **Attached to**: Same GameObject as `SteamNetworkIntegration`.
- **Key Features**:
    - `Awake()`: Gets references to `SteamNetworkIntegration` component and `NetworkManager`.
    - `SpawnPlayerForConnection(NetworkConnection conn)` (Server-side):
        - Checks if `NetworkManager.ServerManager.Started`.
        - Spawns the `NetworkPlayerPrefab` using `ServerManager.Spawn(prefab, conn)`. Logs OwnerId.
        - Attempts to set player name using Steam name for host, placeholder for clients.
        - Spawns the `NetworkPetPrefab` for the same connection. Logs OwnerId.
        - Calls `netPet.SetOwnerPlayer(netPlayer)`.
    - `DespawnEntitiesForConnection(NetworkConnection conn)`: Placeholder, notes FishNet handles despawn.
- **Dependencies**: `SteamNetworkIntegration` (for prefabs, names), `FishNet.NetworkManager`, `NetworkPlayer` (script), `NetworkPet` (script).

### 7. `GameManager.cs`
- **Purpose**: Networked singleton to store and sync global game rules.
- **Inherits**: `NetworkBehaviour`.
- **Singleton**: Yes (`Instance`).
- **Key Features**:
    - `SyncVar`s for: `PlayerDrawAmount`, `PetDrawAmount`, `PlayerMaxEnergy`, `PetMaxEnergy`, `PlayerMaxHealth`, `PetMaxHealth`.
- **Dependencies**: Provides data for `NetworkPlayer`, `NetworkPet`, `CombatManager`.

## Combat System

### 8. `CombatSetup.cs`
- **Purpose**: Server-side script to initialize the combat phase.
- **Attached to**: `CombatCanvas` GameObject (or similar scene setup object).
- **Inherits**: `NetworkBehaviour`.
- **Key Features**:
    - `OnStartServer()`: Finds necessary managers (`FightManager`, `CombatManager`, `GameManager`).
    - `InitializeCombat()` (Server-side):
        - Calls `AssignFights()`.
        - Calls `RpcTriggerCombatCanvasManagerSetup()` to trigger client UI setup.
        - Calls `combatManager.StartCombat()`.
    - `AssignFights()` (Server-side):
        - Retrieves lists of active `NetworkPlayer`s and `NetworkPet`s from `FishNet.NetworkManager.ServerManager.Objects`.
        - Assigns fights (player vs. pet) and calls `fightManager.AddFightAssignment()`.
    - `[SerializeField] public GameObject cardGamePrefab`: Prefab for visual card representation.
    - `SpawnCardObject()`: Placeholder method.
- **Dependencies**: `FishNet.NetworkManager`, `FightManager`, `CombatManager`, `CombatCanvasManager`, `GameManager`.

### 9. `FightManager.cs`
- **Purpose**: Networked singleton to track and manage fight assignments.
- **Inherits**: `NetworkBehaviour`.
- **Singleton**: Yes (`Instance`).
- **Key Features**:
    - `FightAssignmentData` struct (using ObjectIds).
    - `[SyncObject] private readonly SyncList<FightAssignmentData> fightAssignments`.
    - Server-side dictionaries for lookups.
    - `AddFightAssignment(NetworkPlayer player, NetworkPet pet)` (Server).
    - `GetOpponentForPlayer(NetworkPlayer player)`, `GetOpponentForPet(NetworkPet pet)`.
- **Dependencies**: `NetworkPlayer`, `NetworkPet`.

### 10. `CombatManager.cs`
- **Purpose**: Networked singleton orchestrating combat flow (rounds, turns, actions).
- **Inherits**: `NetworkBehaviour`.
- **Singleton**: Yes (`Instance`).
- **Key Features**:
    - `currentRound` (`SyncVar`).
    - Server-side `FightTurnState` struct/list.
    - `StartCombat()` (Server): Initializes fight states, starts round 1.
    - `StartNewRound()` (Server): Increments round, replenishes energy, calls `HandManager.DrawInitialCardsForEntity` on player and pet components, sets turns.
    - `CmdEndPlayerTurn` (ServerRpc): Handles player ending turn, calls `player.GetComponent<HandManager>().DiscardHand()`, starts `ProcessPetTurn`.
    - `ProcessPetTurn()` (Server Coroutine): Uses `pet.GetComponent<HandleCardPlay>().PlayCard(cardId)` to play cards. Checks for fight end.
    - `CmdPlayerRequestsPlayCard` (ServerRpc): Delegates card play to `player.GetComponent<HandleCardPlay>().PlayCard(cardId)`. Checks for fight end.
    - RPCs for notifying clients.
    - Triggers `DraftSetup.InitializeDraft()` when all combats are complete.
- **Dependencies**: `FightManager`, `GameManager`, `SteamNetworkIntegration`, `CombatCanvasManager`, `NetworkPlayer`, `NetworkPet`, `HandManager`, `HandleCardPlay`, `Card`, `CardDatabase`, `DraftSetup`.

### 11. `HandManager.cs`
- **Purpose**: Handles card draw, discard, shuffle logic.
- **Inherits**: `MonoBehaviour`.
- **Attached to**: `NetworkPlayer` and `NetworkPet` prefabs.
- **Key Features**: 
    - `DrawInitialCardsForEntity(int drawAmount)`: Draws initial cards.
    - `DrawOneCard()`: Draws one card from deck.
    - `DiscardHand()`: Discards all cards in hand.
    - `MoveCardToDiscard(int cardId)`: Moves specific card to discard.
    - `ShuffleDeck()`: Shuffles deck.
    - `ReshuffleDiscardIntoDeck()`: Moves cards from discard to deck and shuffles.
    - Now interacts with `CombatDeck`, `CombatHand`, and `CombatDiscard` components.
- **Dependencies**: `NetworkPlayer`, `NetworkPet`, `CombatDeck`, `CombatHand`, `CombatDiscard`.

### 12. `EffectManager.cs`
- **Purpose**: Applies card effects to entity targets.
- **Inherits**: `MonoBehaviour`.
- **Attached to**: `NetworkPlayer` and `NetworkPet` prefabs.
- **Key Features**: 
    - `ApplyEffect(NetworkBehaviour target, CardData cardData)`: Applies effects based on `cardData.EffectType` (Damage, Heal, DrawCard, etc.).
    - Switches on `cardData.EffectType` to call appropriate methods.
    - For DrawCard effects, finds target's HandManager component.
- **Dependencies**: `NetworkPlayer`, `NetworkPet`, `HandManager`, `Card`.

### 13. `HandleCardPlay.cs`
- **Purpose**: Manages card playing logic for entities.
- **Inherits**: `MonoBehaviour`.
- **Attached to**: `NetworkPlayer` and `NetworkPet` prefabs.
- **Key Features**: 
    - `PlayCard(int cardId)`: Validates card play (in hand, enough energy), gets target from FightManager, calls target's EffectManager.
    - Checks card is in hand and entity has sufficient energy.
    - Gets correct target from FightManager (player→pet or pet→player).
    - Applies energy cost to caster.
    - Uses target's EffectManager to apply card effects.
    - Calls HandManager to move played card to discard.
- **Dependencies**: `FightManager`, `HandManager`, `EffectManager`, `NetworkPlayer`, `NetworkPet`, `Card`, `CardDatabase`.

### 14. `CombatDeckSetup.cs`
- **Purpose**: Prepares entity's combat deck for a new combat.
- **Inherits**: `MonoBehaviour`.
- **Attached to**: `NetworkPlayer` and `NetworkPet` prefabs at root.
- **Key Features**:
    - Called by `CombatSetup`.
    - Copies cards from `NetworkEntityDeck` to `CombatDeck`.
    - Initializes decks for combat.
- **Dependencies**: `NetworkEntityDeck`, `CombatDeck`.

### 15. `CombatDeck.cs`
- **Purpose**: Manages the entity's deck of cards during combat.
- **Inherits**: `MonoBehaviour`.
- **Attached to**: `NetworkPlayer` and `NetworkPet` prefabs at root.
- **Key Features**:
    - Stores list of cards in current combat deck.
    - Accessed by `HandManager` for drawing cards.
- **Dependencies**: `Card`, `HandManager`.

### 16. `CombatDiscard.cs`
- **Purpose**: Manages the entity's discard pile during combat.
- **Inherits**: `MonoBehaviour`.
- **Attached to**: `NetworkPlayer` and `NetworkPet` prefabs at root.
- **Key Features**:
    - Stores list of cards in current combat discard pile.
    - Accessed by `HandManager` for discarding and reshuffling.
- **Dependencies**: `Card`, `HandManager`.

### 17. `CombatHand.cs`
- **Purpose**: Manages the entity's hand during combat.
- **Inherits**: `MonoBehaviour`.
- **Attached to**: `NetworkPlayer` and `NetworkPet` prefabs at root.
- **Key Features**:
    - Stores list of cards in current combat hand.
    - References a `combathand` GameObject in hierarchy for card visuals.
    - Accessed by `HandManager` for hand management.
- **Dependencies**: `Card`, `HandManager`.

## Draft System

### 18. `DraftSetup.cs`
- **Purpose**: Initializes the draft phase after combat.
- **Attached to**: `DraftManager` GameObject in the scene.
- **Inherits**: `NetworkBehaviour`.
- **Key Features**:
    - `InitializeDraft()` (Server-side):
        - Called by `CombatManager` when all combats are complete.
        - Deactivates `CombatCanvas`.
        - Activates `DraftCanvas`.
        - Sets up visual areas on draft canvas.
        - Calls `DraftPackSetup.SetupDraftPacks()`.
        - Initializes card shop and artifact shop.
- **Dependencies**: `DraftCanvasManager`, `DraftPackSetup`, `CardShopManager`, `ArtifactShopManager`.

### 19. `DraftManager.cs`
- **Purpose**: Controls the flow of the drafting phase.
- **Attached to**: `DraftManager` GameObject.
- **Inherits**: `NetworkBehaviour`.
- **Singleton**: Yes (`Instance`).
- **Key Features**:
    - Manages draft packs distribution to players.
    - Handles pack passing logic when a card is selected.
    - Queues packs for players.
    - Tracks draft completion status.
    - Triggers next combat when draft is complete.
- **Dependencies**: `DraftPack`, `NetworkPlayer`, `CombatSetup`.

### 20. `DraftPack.cs`
- **Purpose**: Contains a list of draftable cards.
- **Attached to**: `DraftPack` prefab.
- **Inherits**: `NetworkBehaviour`.
- **Key Features**:
    - Stores list of cards available in the pack.
    - Handles removal of selected cards.
    - Manages visual representation of cards in pack.
- **Dependencies**: `Card`.

### 21. `DraftPackSetup.cs`
- **Purpose**: Creates and populates draft packs.
- **Attached to**: Component triggered by `DraftSetup`.
- **Inherits**: `NetworkBehaviour`.
- **Key Features**:
    - `SetupDraftPacks()` (Server-side):
        - Creates draft packs (equal to number of players).
        - Populates each pack with random draftable cards.
        - Distributes initial packs to players.
- **Dependencies**: `DraftPack`, `DraftManager`, `NetworkPlayer`, `CardDatabase`.

### 22. `DraftSelectionManager.cs`
- **Purpose**: Handles card selection during draft.
- **Attached to**: Card prefabs in draft.
- **Inherits**: `MonoBehaviour`.
- **Key Features**:
    - Triggers when player selects a card from a draft pack.
    - Adds selected card to player's `NetworkEntityDeck`.
    - Notifies `DraftManager` to pass the pack to the next player.
- **Dependencies**: `DraftManager`, `DeckManager`, `NetworkPlayer`.

### 23. `CardShopManager.cs`
- **Purpose**: Manages card shop in draft phase.
- **Attached to**: `CardShop` prefab.
- **Inherits**: `NetworkBehaviour`.
- **Key Features**:
    - Populates shop with random purchasable cards.
    - Handles card purchase transactions.
    - Updates available cards when purchases are made.
- **Dependencies**: `Card`, `CardDatabase`, `NetworkPlayer`.

### 24. `CardSelectionManager.cs`
- **Purpose**: Handles card selection from card shop.
- **Attached to**: Card prefabs in shop.
- **Inherits**: `MonoBehaviour`.
- **Key Features**:
    - Triggers when player selects a card from the shop.
    - Verifies player has enough currency.
    - Adds selected card to player's `NetworkEntityDeck`.
    - Deducts cost from player's currency.
- **Dependencies**: `CardShopManager`, `DeckManager`, `NetworkPlayer`.

### 25. `ArtifactShopManager.cs`
- **Purpose**: Manages artifact shop in draft phase.
- **Attached to**: `ArtifactShop` prefab.
- **Inherits**: `NetworkBehaviour`.
- **Key Features**:
    - Populates shop with random purchasable artifacts.
    - Handles artifact purchase transactions.
    - Updates available artifacts when purchases are made.
- **Dependencies**: `Artifact`, `NetworkPlayer`.

### 26. `ArtifactSelectionManager.cs`
- **Purpose**: Handles artifact selection from artifact shop.
- **Attached to**: Artifact prefabs in shop.
- **Inherits**: `MonoBehaviour`.
- **Key Features**:
    - Triggers when player selects an artifact from the shop.
    - Verifies player has enough currency.
    - Adds selected artifact to player's `NetworkEntityArtifacts`.
    - Deducts cost from player's currency.
- **Dependencies**: `ArtifactShopManager`, `ArtifactManager`, `NetworkPlayer`.

## Game Entities & Data

### 27. `NetworkPlayer.cs`
- **Purpose**: Represents a player. Attached to NetworkPlayer prefab.
- **Inherits**: `NetworkBehaviour`.
- **Key Features**: `SyncVar`s (`PlayerName`, Health, Energy, Statuses), Methods for taking damage, healing, managing energy. Now uses new deck and artifact systems.
- **Dependencies**: `GameManager`, `Card` (prefabs), `HandManager`, `EffectManager`, `HandleCardPlay`, `NetworkEntityDeck`, `NetworkEntityArtifacts`, `RelationshipManager`, `DeckManager`, `ArtifactManager`.

### 28. `NetworkPet.cs`
- **Purpose**: Represents a pet. Attached to NetworkPet prefab.
- **Inherits**: `NetworkBehaviour`.
- **Key Features**: Similar to `NetworkPlayer`. Includes `OwnerPlayerObjectId` (`SyncVar`), `SetOwnerPlayer()`. Now uses new deck and artifact systems.
- **Dependencies**: `GameManager`, `Card` (prefabs), `NetworkPlayer`, `HandManager`, `EffectManager`, `HandleCardPlay`, `NetworkEntityDeck`, `NetworkEntityArtifacts`, `RelationshipManager`, `DeckManager`, `ArtifactManager`.

### 29. `EntityDeckSetup.cs`
- **Purpose**: Initializes entity's starting deck.
- **Inherits**: `MonoBehaviour`.
- **Attached to**: `NetworkPlayer` and `NetworkPet` prefabs at root.
- **Key Features**:
    - Called after `PlayerSpawner` creates entities.
    - Populates `NetworkEntityDeck` with starter deck cards.
- **Dependencies**: `NetworkEntityDeck`, `Card`.

### 30. `NetworkEntityDeck.cs`
- **Purpose**: Maintains entity's collected cards across combats.
- **Inherits**: `NetworkBehaviour`.
- **Attached to**: `NetworkPlayer` and `NetworkPet` prefabs at root.
- **Key Features**:
    - `SyncList<int>` of card IDs the entity owns.
    - Provides methods to add/remove cards.
    - Persists between combat and draft phases.
- **Dependencies**: `Card`, `CardDatabase`.

### 31. `NetworkEntityArtifacts.cs`
- **Purpose**: Maintains entity's collected artifacts.
- **Inherits**: `NetworkBehaviour`.
- **Attached to**: `NetworkPlayer` and `NetworkPet` prefabs at root.
- **Key Features**:
    - `SyncList<int>` of artifact IDs the entity owns.
    - Provides methods to add/remove artifacts.
    - Persists between combat and draft phases.
- **Dependencies**: `Artifact`.

### 32. `RelationshipManager.cs`
- **Purpose**: Manages entity relationships (allies, enemies).
- **Inherits**: `MonoBehaviour`.
- **Attached to**: `NetworkPlayer` and `NetworkPet` prefabs at root.
- **Key Features**:
    - References to allied entities.
    - Set after `PlayerSpawner` creates entities.
    - Provides methods to access allied entities.
- **Dependencies**: `NetworkPlayer`, `NetworkPet`.

### 33. `DeckManager.cs`
- **Purpose**: Manages operations on entity's card collection.
- **Inherits**: `MonoBehaviour`.
- **Attached to**: `NetworkPlayer` and `NetworkPet` prefabs at root.
- **Key Features**:
    - Interacts with `NetworkEntityDeck`.
    - Provides methods to add cards from draft/shop.
    - Handles deck modification during draft phase.
- **Dependencies**: `NetworkEntityDeck`, `Card`.

### 34. `ArtifactManager.cs`
- **Purpose**: Manages operations on entity's artifact collection.
- **Inherits**: `MonoBehaviour`.
- **Attached to**: `NetworkPlayer` and `NetworkPet` prefabs at root.
- **Key Features**:
    - Interacts with `NetworkEntityArtifacts`.
    - Provides methods to add artifacts from draft/shop.
    - Handles artifact effects during combat.
- **Dependencies**: `NetworkEntityArtifacts`, `Artifact`.

### 35. `Card.cs`
- **Purpose**: Defines card properties/effects. Attached to Card prefabs (or used as ScriptableObjects).
- **Inherits**: `MonoBehaviour` or `ScriptableObject`. (If `NetworkBehaviour`, reconsider if needed).
- **Key Features**: `CardEffectType` enum, Serialized fields for ID, Name, Description, Artwork, Effect Type, Amount, Cost. Public accessors.
- **Dependencies**: None.

### 36. `Artifact.cs`
- **Purpose**: Represents an artifact item with special effects.
- **Inherits**: `MonoBehaviour`.
- **Attached to**: Artifact prefabs.
- **Key Features**:
    - References `ArtifactData`.
    - Visualizes artifact in UI.
    - Handles selection in shop.
- **Dependencies**: `ArtifactData`.

### 37. `ArtifactData.cs`
- **Purpose**: Defines artifact properties and effects.
- **Inherits**: `ScriptableObject`.
- **Key Features**:
    - ID, Name, Description, Icon.
    - Effect type and parameters.
    - Similar structure to `Card`/`CardData`.
- **Dependencies**: None.

### 38. `CardDatabase.cs` (Assumed/Required)
- **Purpose**: Singleton (likely `ScriptableObject` or `MonoBehaviour`) to provide access to `CardData` (e.g., `Card` scriptable objects or prefabs) based on a card ID.
- **Key Features**: `GetCardById(int cardId)` method. Loaded at runtime.
- **Dependencies**: `Card`.

---
# Duplicate Functionality Analysis

After reviewing the codebase and implementing the requested changes, the following areas of duplicate or overlapping functionality were identified:

## 1. Card Display and Rendering Logic

Current duplication:
- **CombatCanvasManager.RenderHand()** - Contains logic to create and display card visuals
- **DraftCanvasManager** - Has similar logic for rendering cards in draft packs
- **CardShopManager** - Contains yet another implementation of card rendering

**Recommendation**: Create a unified `CardRenderer` or `CardDisplayManager` class that handles all card visualization. This class would take a list of card IDs, a parent transform, and optional callback for card interaction, then handle all the instantiation and setup.

## 2. Player/Pet Ownership and References

Current duplication:
- Multiple scripts looking up player/pet references in various ways
- Different approaches to finding the local player across UI managers

**Recommendation**: Create a `GameEntityRegistry` singleton that maintains references to all NetworkPlayer and NetworkPet instances. This would provide a standardized way to get entities by ID, get the local player, or find entities by ownership.

## 3. Deck Management

This has been largely addressed in our refactoring, but some areas might still have overlap:
- `NetworkEntityDeck` - Persistent deck storage
- `CombatDeck` - Combat-specific deck management 
- `DeckManager` - User interface for deck modification

These scripts all manage aspects of card collections. Consider whether all three components are necessary or if some functionality could be consolidated.

## 4. Card Selection Logic

Even with our unified `CardSelectionManager`, there might still be duplication:
- Draft pack card selection
- Card shop purchase logic
- Both have similar flows for validating and executing selection

**Recommendation**: Further refine the `CardSelectionManager` to use a strategy pattern where different selection types (draft, shop) are defined by interfaces but share common validation and execution patterns.

## 5. Network Connection Handling

Current duplication:
- Multiple scripts handling network connection events
- Redundant connection state monitoring

**Recommendation**: Create a `NetworkSessionManager` that provides a single interface for monitoring connection state and handling disconnections.

## 6. Combat Turn Management

Current duplication:
- `CombatManager` contains turn state management
- Individual entity scripts also track turn state

**Recommendation**: Move all turn state management into `CombatManager` and have entity scripts reference it rather than maintaining their own state.

## 7. Card Effect Application

Current duplication:
- Some effect logic in `HandleCardPlay`
- Some in `EffectManager`
- Potential for duplication in combat resolution code

**Recommendation**: Centralize all effect application in `EffectManager` and have other scripts delegate to it.

## 8. UI Management

Current duplication:
- Multiple canvas managers with similar patterns
- Redundant setup and teardown logic

**Recommendation**: Create a base `UICanvasManager` class that handles common functionality like finding references and setting up UI, then have specific managers inherit from it.

## 9. Game State Management

Current duplication:
- State transitions handled in multiple places
- Combat, draft, and lobby state managed separately

**Recommendation**: Create a central `GameStateManager` that coordinates all state transitions and ensures consistent state across the application. 