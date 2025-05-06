# Project Scripts Summary

This document summarizes the C# scripts created for the project, outlining their purpose, key components, and interactions.

## UI and Scene Management

### 1. `StartScreenManager.cs`
- **Purpose**: Manages the initial start screen UI.
- **Attached to**: `StartScreenCanvas` GameObject.
- **Key Features**:
    - Reference to a "Start" button.
    - On start button press:
        - Calls `CustomNetworkManager.InitiateSteamConnectionAndLobby()` to attempt Steam lobby hosting.
        - Activates the `LobbyCanvas`.
        - Deactivates the `StartScreenCanvas`.
- **Dependencies**: `CustomNetworkManager`.

### 2. `LobbyManagerScript.cs`
- **Purpose**: Manages the player lobby UI and logic.
- **Attached to**: `LobbyCanvas` GameObject.
- **Inherits**: `NetworkBehaviour`.
- **Key Features**:
    - References "Ready" and "Start Game" buttons.
    - Displays a list of connected players and their ready status (`playerListText`).
    - `Awake()`: Attempts to find `NetworkManager` (should be `CustomNetworkManager`).
        - TODO: Needs explicit call to connect/host via `CustomNetworkManager` if not already handled by `StartScreenManager`.
    - `OnReadyButtonPressed()`: Client calls `CmdTogglePlayerReadyState`.
    - `CmdTogglePlayerReadyState` (ServerRpc): Toggles the calling player's ready status.
    - `CheckAllPlayersReady()` (Server): Enables the "Start Game" button if all players are ready and there's more than one player.
    - `OnStartButtonPressed()` (Server-side check): If conditions met, calls `RpcStartGame`.
    - `RpcStartGame` (ObserversRpc): Deactivates `LobbyCanvas` and activates `CombatCanvas`.
    - Handles adding player names to UI and updating across clients.
    - `HandlePlayerDisconnect()`: Called by `CustomNetworkManager` to update player list and ready states.
- **Dependencies**: `CustomNetworkManager`, `CombatCanvas`.

### 3. `CombatCanvasManager.cs`
- **Purpose**: Manages the UI elements specifically for the combat scene on each client.
- **Attached to**: `CombatCanvas` GameObject (not networked itself).
- **Key Features**:
    - References UI elements for:
        - Local player's name, health, energy, hand, deck, discard.
        - Opponent pet's name, health, hand (visuals).
        - "End Turn" button.
    - `SetupCombatUI()`: Called by `CombatSetup` (via RPC) to initialize the UI for the local player.
        - Finds the local `NetworkPlayer` and their assigned `NetworkPet` via `FightManager`.
        - Subscribes to `SyncVar` and `SyncList` changes on `localPlayer` and `opponentPetForLocalPlayer` to update UI.
    - `RenderHand()`: Clears and re-instantiates card visuals in the hand display area based on `SyncList<int> cardIds`.
        - Instantiates `cardGamePrefab` (from `CombatSetup`).
        - Adds click listeners to player's hand cards to call `CombatManager.CmdPlayerRequestsPlayCard()`.
    - `SetEndTurnButtonInteractable()`: Controls interactability of the end turn button.
- **Dependencies**: `FightManager`, `CombatManager`, `NetworkPlayer`, `NetworkPet`, `CombatSetup` (for `cardGamePrefab`).

## Networking and Core Logic

### 4. `CustomNetworkManager.cs`
- **Purpose**: Central networking hub, Steamworks integration, and entity management.
- **Inherits**: `FishNet.Managing.NetworkManager`.
- **Singleton**: Yes (`Instance`).
- **Key Features**:
    - **Steamworks Integration**:
        - Initializes `SteamAPI` on `Awake()`.
        - Handles Steam Callbacks for lobby creation (`OnLobbyCreated`), join requests (`OnGameLobbyJoinRequested`), and lobby entry (`OnLobbyEntered`).
        - `HostLobby()`: Creates a Steam lobby.
        - `JoinLobby(CSteamID)`: Joins an existing Steam lobby.
        - `InitiateSteamConnectionAndLobby()`: Called by `StartScreenManager` to host a lobby.
        - Uses `Tugboat` transport, sets client address to SteamID for P2P.
        - Uses randomized port for local testing.
        - **Note**: Requires `steam_appid.txt` in project root (e.g., with `480`) for editor testing.
    - **Entity Tracking**:
        - `[SerializeField] public List<NetworkObject> networkPlayers`
        - `[SerializeField] public List<NetworkObject> networkPets`
    - **Prefabs**:
        - `[SerializeField] public NetworkObject playerPrefab;`
        - `[SerializeField] public NetworkObject petPrefab;`
    - **Player Spawning**:
        - Instantiates `PlayerSpawner`.
        - `ServerManager_OnRemoteConnectionState`: When a client connects, calls `playerSpawner.SpawnPlayerForConnection(conn)`.
        - Handles client disconnections: `RemoveNetworkedEntitiesForConnection`, notifies `LobbyManagerScript`.
    - Registers/Unregisters `NetworkPlayer` and `NetworkPet` instances.
- **Dependencies**: `Steamworks.NET`, `Tugboat` (FishNet Transport), `PlayerSpawner`, `LobbyManagerScript`.

### 5. `PlayerSpawner.cs`
- **Purpose**: (Non-MonoBehaviour class) Handles the server-side spawning of `NetworkPlayer` and associated `NetworkPet` prefabs.
- **Instantiated by**: `CustomNetworkManager`.
- **Key Features**:
    - Constructor takes `CustomNetworkManager` instance.
        - Retrieves `networkPlayerPrefab` and `networkPetPrefab` from `CustomNetworkManager`.
    - `SpawnPlayerForConnection(NetworkConnection conn)` (Server-side):
        - Spawns the `networkPlayerPrefab` using `ServerManager.Spawn(prefab, conn)`.
        - Registers the new player instance with `CustomNetworkManager`.
        - Spawns the `networkPetPrefab` for the same connection.
        - Registers the new pet instance.
        - TODO: Link pet to player (e.g., `netPet.SetOwnerPlayer(netPlayer)`).
    - `DespawnEntitiesForConnection(NetworkConnection conn)`: Despawns player and associated pet.
- **Dependencies**: `CustomNetworkManager`, `NetworkPlayer` (prefab), `NetworkPet` (prefab).

### 6. `GameManager.cs`
- **Purpose**: Networked singleton to store and sync global game rules.
- **Inherits**: `NetworkBehaviour`.
- **Singleton**: Yes (`Instance`).
- **Key Features**:
    - Serializable and Synced Variables (`SyncVar`):
        - `PlayerDrawAmount`
        - `PetDrawAmount`
        - `PlayerMaxEnergy`
        - `PetMaxEnergy`
        - `PlayerMaxHealth`
        - `PetMaxHealth`
    - These values are used by `NetworkPlayer`, `NetworkPet`, and `CombatManager`.
- **Dependencies**: None explicit, but provides data for many other scripts.

## Combat System

### 7. `CombatSetup.cs`
- **Purpose**: Server-side script to initialize the combat phase once the `CombatCanvas` is active.
- **Attached to**: `CombatCanvas` GameObject.
- **Inherits**: `NetworkBehaviour`.
- **Key Features**:
    - `OnStartServer()`: Finds necessary managers (`CustomNetworkManager`, `FightManager`, `CombatManager`, `GameManager`).
    - `InitializeCombat()` (Server-side):
        - Calls `AssignFights()`.
        - Calls `RpcTriggerCombatCanvasManagerSetup()` to tell clients to set up their local combat UI.
        - Calls `combatManager.StartCombat()`.
    - `AssignFights()` (Server-side):
        - Retrieves lists of `NetworkPlayer`s and `NetworkPet`s from `CustomNetworkManager`.
        - Assigns each player to a unique pet (simple 1-to-1 logic for now).
        - Calls `fightManager.AddFightAssignment(player, opponentPet)`.
    - `[SerializeField] private GameObject cardGamePrefab`: Prefab for visual card representation in scene, used by `CombatCanvasManager` and potentially this script for spawning.
    - `SpawnCardObject()`: Placeholder method for spawning card GameObjects (currently simplified, real implementation would involve a Card Database).
- **Dependencies**: `CustomNetworkManager`, `FightManager`, `CombatManager`, `CombatCanvasManager`, `GameManager`.

### 8. `FightManager.cs`
- **Purpose**: Networked singleton to track and manage fight assignments between players and pets.
- **Inherits**: `NetworkBehaviour`.
- **Singleton**: Yes (`Instance`).
- **Key Features**:
    - `FightAssignmentData` struct: Stores `PlayerObjectId`, `PetObjectId`. (Note: `PlayerConnection` in struct might be problematic for `SyncList` serialization, ObjectIDs are safer).
    - `[SyncObject] private readonly SyncList<FightAssignmentData> fightAssignments`: Synced list of active fights.
    - Server-side dictionaries (`playerToPetMap`, `petToPlayerMap`) for quick lookups (rebuilt from `SyncList` on change).
    - `AddFightAssignment(NetworkPlayer player, NetworkPet pet)` (Server): Adds a new fight pairing.
    - `GetOpponentForPlayer(NetworkPlayer player)`: Returns the `NetworkPet` assigned to the given player.
    - `GetOpponentForPet(NetworkPet pet)`: Returns the `NetworkPlayer` assigned to the given pet.
- **Dependencies**: `NetworkPlayer`, `NetworkPet`.

### 9. `CombatManager.cs`
- **Purpose**: Networked singleton that orchestrates the entire combat flow, including rounds, turns, and actions.
- **Inherits**: `NetworkBehaviour`.
- **Singleton**: Yes (`Instance`).
- **Key Features**:
    - `currentRound` (`SyncVar`): Tracks the current combat round.
    - `FightTurnState` struct (server-side): Tracks `playerConnection`, `currentTurn` (`PlayerTurn`, `PetTurn`), `playerObjId`, `petObjId` for each ongoing fight.
    - Instantiates `HandManager` and `EffectManager`.
    - `StartCombat()` (Server): Called by `CombatSetup`. Initializes fight states and starts the first round.
    - `StartNewRound()` (Server):
        - Increments `currentRound`.
        - Replenishes energy for all players and pets in active fights.
        - Calls `handManager.DrawInitialCardsForEntity()` for players and pets.
        - Sets turn to `PlayerTurn` for each fight and notifies clients via `TargetRpcNotifyTurnChanged`.
    - `CmdEndPlayerTurn(NetworkConnection conn)` (ServerRpc):
        - Called by client when "End Turn" button is pressed.
        - Player discards hand (`handManager.DiscardHand`).
        - Transitions turn to `PetTurn` for that fight.
        - Starts `ProcessPetTurn()` coroutine.
    - `ProcessPetTurn()` (Server Coroutine):
        - Basic AI: Pet plays affordable cards from hand against its opponent player.
        - Uses `effectManager.ApplyEffect()` and `handManager.MoveCardToDiscard()`.
        - After pet's actions, pet discards hand.
        - Checks for fight end conditions or proceeds to next round if all pet turns are done.
    - `CmdPlayerRequestsPlayCard(NetworkConnection conn, int cardId)` (ServerRpc):
        - Validates turn, card presence in hand, and energy cost.
        - Applies card cost.
        - Calls `effectManager.ApplyEffect()`.
        - Calls `handManager.MoveCardToDiscard()`.
        - Checks for fight end conditions.
    - `GetCardDataFromId(int cardId)`: **Placeholder function**, needs implementation with a Card Database.
    - `IsPlayerTurn(NetworkPlayer player)`: Checks if it's the given player's turn in their fight.
    - Various RPCs to notify clients about round start, turn changes, cards played, messages, and fight end.
- **Dependencies**: `FightManager`, `HandManager`, `EffectManager`, `GameManager`, `CustomNetworkManager`, `CombatCanvasManager`, `NetworkPlayer`, `NetworkPet`, `Card`.

### 10. `HandManager.cs`
- **Purpose**: (Non-MonoBehaviour class) Handles all logic related to card drawing, discarding, and moving cards between deck, hand, and discard piles for entities.
- **Instantiated by**: `CombatManager`.
- **Key Features** (all server-side logic acting on `SyncList<int>` card ID lists in `NetworkPlayer`/`NetworkPet`):
    - `DrawInitialCardsForEntity(NetworkBehaviour entity, int drawAmount)`: Shuffles deck and draws specified cards.
    - `DrawOneCard(NetworkBehaviour entity)`: Draws a single card. Handles empty deck by reshuffling discard pile.
    - `DiscardHand(NetworkBehaviour entity)`: Moves all cards from hand to discard pile.
    - `MoveCardToDiscard(NetworkBehaviour entity, int cardId)`: Moves a specific card from hand to discard.
    - `ShuffleDeck(NetworkBehaviour entity)`: Shuffles the entity's deck (`currentDeckCardIds`).
    - `ReshuffleDiscardIntoDeck(NetworkBehaviour entity)`: Moves all cards from discard to deck and shuffles.
- **Dependencies**: `CombatManager`, `NetworkPlayer`, `NetworkPet`.

### 11. `EffectManager.cs`
- **Purpose**: (Non-MonoBehaviour class) Responsible for applying the effects of played cards.
- **Instantiated by**: `CombatManager`.
- **Key Features**:
    - `ApplyEffect(NetworkBehaviour caster, NetworkBehaviour target, Card cardData)` (Server-side):
        - Takes caster, target, and `Card` data.
        - Switches on `cardData.EffectType`:
            - `Damage`: Calls `TakeDamage()` on target.
            - `Heal`: Calls `Heal()` on target.
            - `DrawCard`: Calls `handManager.DrawOneCard()` for the caster. (Requires `CombatManager` to expose `HandManager` or this method be on `CombatManager`).
            - `BuffStats`, `DebuffStats`, `ApplyStatus`: Placeholders with basic logging; requires more detailed implementation on `NetworkPlayer`/`NetworkPet` and `Card` data.
- **Dependencies**: `CombatManager` (potentially for `HandManager` access), `NetworkPlayer`, `NetworkPet`, `Card`.

## Game Entities & Data

### 12. `NetworkPlayer.cs`
- **Purpose**: Represents a player in the game. Attached to the NetworkPlayer prefab.
- **Inherits**: `NetworkBehaviour`.
- **Key Features**:
    - **Synced Data (`SyncVar`)**:
        - `PlayerName`
        - `MaxHealth`, `CurrentHealth`
        - `MaxEnergy`, `CurrentEnergy`
        - `CurrentStatuses` (string, simple implementation)
    - **Card Management (`SyncList<int>`)**:
        - `currentDeckCardIds`
        - `playerHandCardIds`
        - `discardPileCardIds`
    - **Serialized Fields**:
        - `List<Card> StarterDeckPrefabs`: List of `Card` prefabs defining the initial deck.
        - `Transform PlayerHandTransform`, `DeckTransform`, `DiscardTransform`: For client-side visual organization of card GameObjects.
    - `OnStartServer()`: Initializes health/energy from `GameManager`, calls `InitializeDeck()`.
    - `InitializeDeck()` (Server): Populates `currentDeckCardIds` from `StarterDeckPrefabs` (using `GetInstanceID()` as a placeholder for real card IDs).
    - `CmdSetPlayerName()` (ServerRpc): Allows client to set its name (e.g., from Steam).
    - Methods: `TakeDamage()`, `Heal()`, `ChangeEnergy()`, `ReplenishEnergy()`.
- **Dependencies**: `GameManager`, `Card` (prefabs).

### 13. `NetworkPet.cs`
- **Purpose**: Represents a player-controlled or AI pet. Attached to the NetworkPet prefab.
- **Inherits**: `NetworkBehaviour`.
- **Key Features**:
    - Similar structure to `NetworkPlayer` for stats and card management.
    - **Synced Data (`SyncVar`)**:
        - `OwnerPlayerObjectId` (to link to a `NetworkPlayer`)
        - `PetName`
        - `MaxHealth`, `CurrentHealth`
        - `MaxEnergy`, `CurrentEnergy`
        - `CurrentStatuses`
    - **Card Management (`SyncList<int>`)**:
        - `currentDeckCardIds`
        - `playerHandCardIds`
        - `discardPileCardIds`
    - **Serialized Fields**:
        - `List<Card> StarterDeckPrefabs`
        - `Transform PetHandTransform`, `DeckTransform`, `DiscardTransform`.
    - `OnStartServer()`: Initializes stats from `GameManager`, calls `InitializeDeck()`.
    - `InitializeDeck()` (Server): Similar to `NetworkPlayer`.
    - `SetOwnerPlayer(NetworkPlayer ownerPlayer)` (Server): Sets the `OwnerPlayerObjectId`.
    - `GetOwnerPlayer()`: Retrieves the owner `NetworkPlayer` object.
    - Methods: `TakeDamage()`, `Heal()`, `ChangeEnergy()`, `ReplenishEnergy()`.
- **Dependencies**: `GameManager`, `Card` (prefabs), `NetworkPlayer`.

### 14. `Card.cs`
- **Purpose**: Defines the properties and effects of a single card. Attached to Card prefabs.
- **Inherits**: `NetworkBehaviour` (currently, though `MonoBehaviour` might be sufficient if card GameObjects aren't independently networked).
- **Key Features**:
    - `CardEffectType` enum (Damage, Heal, DrawCard, etc.).
    - **Serialized Fields (Card Definition)**:
        - `_cardId` (int, for unique identification)
        - `_cardName` (string)
        - `_description` (string)
        - `_cardArtwork` (Sprite)
        - `_effectType` (`CardEffectType`)
        - `_amount` (int, for effect magnitude)
        - `_energyCost` (int)
    - Public accessors for card properties.
    - `InitializeCard()`: Method to set card properties (useful if creating card data programmatically).
    - Runtime properties `OwningPlayer`, `OwningPet` (not serialized, set on instantiated card GameObjects).
- **Dependencies**: None.

### 15. `Deck.cs`
- **Purpose**: A data container script, attached to a "Deck Prefab," representing a list of predefined `Card` prefabs.
- **Inherits**: `MonoBehaviour`.
- **Key Features**:
    - `[SerializeField] private List<Card> cardsInDeck`: Assign `Card` prefabs in the Inspector to define a deck composition.
    - `DeckName` (string).
    - `GetCardDefinitions()`: Returns the list of `Card`s.
    - Referenced by `NetworkPlayer` and `NetworkPet` in their `StarterDeckPrefabs` field (though this should ideally be a reference to the `Deck` prefab itself, and then the player/pet would get the `cardsInDeck` from it).
- **Dependencies**: `Card` (prefabs).

---
This summary should provide a good overview for future reference. 