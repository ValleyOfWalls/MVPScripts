using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using FishNet.Managing;

public enum CombatTurn
{
    None,
    PlayerTurn,
    PetTurn
}

/// <summary>
/// Orchestrates the combat flow including turns, rounds, card playing, and fight progression.
/// Attach to: A persistent NetworkObject in the scene that manages combat gameplay.
/// </summary>
public class CombatManager : NetworkBehaviour
{
    public static CombatManager Instance { get; private set; }

    public readonly SyncVar<int> currentRound = new SyncVar<int>(0);

    public struct FightTurnState
    {
        public NetworkConnection playerConnection;
        public CombatTurn currentTurn;
        public uint playerObjId;
        public uint petObjId;
    }
    
    // Client-side structure to track the current turn for the local player's fight
    private struct ClientFightTurnState
    {
        public CombatTurn currentTurn;
        public uint playerObjId;
        public uint petObjId;
    }
    
    // Client-side cache of the local player's fight turn state
    private ClientFightTurnState clientFightTurnState;
    
    // Client-side flag to track if combat has been initialized
    private bool clientCombatInitialized = false;
    
    // This list is server-authoritative and not directly synced. RPCs will inform clients of turn changes.
    private List<FightTurnState> fightTurnStates = new List<FightTurnState>();

    [SerializeField] private FightManager fightManager;
    [SerializeField] private GameManager gameManager;
    
    // Reference to GamePhaseManager
    private GamePhaseManager gamePhaseManager;
    
    private SteamNetworkIntegration steamNetworkIntegration;
    private CombatCanvasManager combatCanvasManager; // For local UI updates like end turn button
    
    // Cache for network objects to avoid repeated lookups
    private Dictionary<uint, NetworkPlayer> playerCache = new Dictionary<uint, NetworkPlayer>();
    private Dictionary<uint, NetworkPet> petCache = new Dictionary<uint, NetworkPet>();
    
    // Track active fights for game completion detection
    private int activeFightCount = 0;

    private void Awake()
    { 
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Find required components if not set in inspector
        FindRequiredComponents();

        if (fightManager == null) Debug.LogError("FightManager not found by CombatManager.");
        if (gameManager == null) Debug.LogError("GameManager not found by CombatManager.");
        if (steamNetworkIntegration == null) Debug.LogError("SteamNetworkIntegration not found by CombatManager.");
        if (gamePhaseManager == null) Debug.LogError("GamePhaseManager not found by CombatManager.");
    }

    private void FindRequiredComponents()
    {
        if (fightManager == null) fightManager = FightManager.Instance;
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (gamePhaseManager == null) gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
        if (steamNetworkIntegration == null) steamNetworkIntegration = SteamNetworkIntegration.Instance;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // Client might need references for local predictions or UI updates, but server is authoritative.
        combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
    }

    [Server]
    public void StartCombat()
    {
        if (fightManager == null)
        {
            Debug.LogError("CombatManager.StartCombat: FightManager instance is null. Cannot start combat.");
            return;
        }

        // Fight assignments and combat decks should already be setup by CombatSetup
        InitializeFightTurnStates();
        
        // Start the first round of combat for all fights (pass default to use all fights)
        StartNewRound();
    }

    [Server]
    private void InitializeFightTurnStates()
    {
        var fightAssignments = fightManager.GetAllFightAssignments();
        foreach (var fightAssignment in fightAssignments)
        {
            NetworkPlayer player = GetNetworkObjectComponent<NetworkPlayer>(fightAssignment.PlayerObjectId);
            NetworkPet pet = GetNetworkObjectComponent<NetworkPet>(fightAssignment.PetObjectId);

            if (player != null && pet != null)
            {
                // Create a new fight turn state
                FightTurnState fightState = new FightTurnState
                {
                    playerConnection = player.Owner,
                    currentTurn = CombatTurn.None, // Will be set in StartNewRound
                    playerObjId = (uint)player.ObjectId,
                    petObjId = (uint)pet.ObjectId
                };

                fightTurnStates.Add(fightState);
            }
            else
            {
                Debug.LogError($"Failed to initialize fight state. Player or pet not found for IDs: {fightAssignment.PlayerObjectId}, {fightAssignment.PetObjectId}");
            }
        }
        
        // Track how many fights are active for completion detection
        activeFightCount = fightTurnStates.Count;
    }

    [Server]
    public void StartNewRound(FightTurnState fightToUpdate = default)
    {
        currentRound.Value++;

        Debug.Log($"Starting round {currentRound.Value}");
        
        // Create a collection of fights to process
        List<FightTurnState> fightsToProcess = new List<FightTurnState>();
        
        // If a specific fight was provided, only process that one
        if (fightToUpdate.playerObjId != 0 && fightToUpdate.petObjId != 0)
        {
            Debug.Log($"Starting new round for specific fight: Player {fightToUpdate.playerObjId} vs Pet {fightToUpdate.petObjId}");
            fightsToProcess.Add(fightToUpdate);
        }
        // Otherwise, process all fights (for initial setup only)
        else
        {
            Debug.Log("Starting new round for all fights (initial setup)");
            fightsToProcess = new List<FightTurnState>(fightTurnStates);
        }

        // Process only the specified fights
        foreach (var fightState in fightsToProcess)
        {
            // Get player and pet
            NetworkPlayer player = GetNetworkObjectComponent<NetworkPlayer>(fightState.playerObjId);
            NetworkPet pet = GetNetworkObjectComponent<NetworkPet>(fightState.petObjId);

            if (player != null && pet != null)
            {
                try
                {
                    // Replenish energy
                    player.CurrentEnergy.Value = gameManager.PlayerMaxEnergy.Value;
                    pet.CurrentEnergy.Value = gameManager.PetMaxEnergy.Value;

                    // Draw cards using their HandManager components
                    HandManager playerHandManager = player.GetComponent<HandManager>();
                    HandManager petHandManager = pet.GetComponent<HandManager>();
                    
                    if (playerHandManager != null)
                    {
                        // Get the player's current hand size
                        CombatHand playerHand = player.GetComponent<CombatHand>();
                        int currentPlayerHandSize = playerHand != null ? playerHand.GetCardCount() : 0;
                        
                        // For the first round, we use PlayerDrawAmount as the initial draw
                        // For subsequent rounds, we draw enough to reach PlayerTargetHandSize
                        int playerDrawAmount;
                        
                        if (currentRound.Value == 1)
                        {
                            // First round: Draw the initial hand size
                            playerDrawAmount = gameManager != null ? gameManager.PlayerDrawAmount.Value : 5;
                            Debug.Log($"First round: Player {player.PlayerName.Value} drawing initial hand of {playerDrawAmount} cards");
                        }
                        else
                        {
                            // Subsequent rounds: Draw to reach target hand size
                            int targetHandSize = gameManager != null ? gameManager.PlayerTargetHandSize.Value : 3;
                            playerDrawAmount = Mathf.Max(0, targetHandSize - currentPlayerHandSize);
                            
                            if (playerDrawAmount > 0)
                            {
                                Debug.Log($"Round {currentRound.Value}: Player {player.PlayerName.Value} drawing {playerDrawAmount} cards to reach target hand size of {targetHandSize}");
                            }
                            else
                            {
                                Debug.Log($"Round {currentRound.Value}: Player {player.PlayerName.Value} already has {currentPlayerHandSize} cards, target is {targetHandSize}, no cards drawn");
                            }
                        }
                        
                        // Draw the calculated number of cards
                        if (playerDrawAmount > 0)
                        {
                            playerHandManager.DrawInitialCardsForEntity(playerDrawAmount);
                        }
                    }
                    else
                    {
                        Debug.LogError($"HandManager component not found on player {player.PlayerName.Value}");
                    }

                    if (petHandManager != null)
                    {
                        // Get the pet's current hand size
                        CombatHand petHand = pet.GetComponent<CombatHand>();
                        int currentPetHandSize = petHand != null ? petHand.GetCardCount() : 0;
                        
                        // For the first round, we use PetDrawAmount as the initial draw
                        // For subsequent rounds, we draw enough to reach PetTargetHandSize
                        int petDrawAmount;
                        
                        if (currentRound.Value == 1)
                        {
                            // First round: Draw the initial hand size
                            petDrawAmount = gameManager != null ? gameManager.PetDrawAmount.Value : 3;
                            Debug.Log($"First round: Pet {pet.PetName.Value} drawing initial hand of {petDrawAmount} cards");
                        }
                        else
                        {
                            // Subsequent rounds: Draw to reach target hand size
                            int targetHandSize = gameManager != null ? gameManager.PetTargetHandSize.Value : 3; 
                            petDrawAmount = Mathf.Max(0, targetHandSize - currentPetHandSize);
                            
                            if (petDrawAmount > 0)
                            {
                                Debug.Log($"Round {currentRound.Value}: Pet {pet.PetName.Value} drawing {petDrawAmount} cards to reach target hand size of {targetHandSize}");
                            }
                            else
                            {
                                Debug.Log($"Round {currentRound.Value}: Pet {pet.PetName.Value} already has {currentPetHandSize} cards, target is {targetHandSize}, no cards drawn");
                            }
                        }
                        
                        // Draw the calculated number of cards
                        if (petDrawAmount > 0)
                        {
                            petHandManager.DrawInitialCardsForEntity(petDrawAmount);
                        }
                    }
                    else
                    {
                        Debug.LogError($"HandManager component not found on pet {pet.PetName.Value}");
                    }
                    
                    // Update turn state to Player's turn
                    int index = fightTurnStates.FindIndex(fs => 
                        fs.playerObjId == fightState.playerObjId && 
                        fs.petObjId == fightState.petObjId);
                    
                    if (index >= 0)
                    {
                        FightTurnState updatedState = fightTurnStates[index];
                        updatedState.currentTurn = CombatTurn.PlayerTurn;
                        fightTurnStates[index] = updatedState;
                        
                        // Notify ONLY the client involved in this fight about turn change
                        // First ensure we have a valid connection
                        if (player.Owner != null)
                        {
                            try
                            {
                                // Send targeted RPC only to this player's connection
                                RpcUpdateTurnState(player.Owner, fightState.playerObjId, fightState.petObjId, CombatTurn.PlayerTurn);
                                RpcEnableEndTurnButton(player.Owner, true);
                            }
                            catch (System.Exception e)
                            {
                                Debug.LogError($"Error notifying client of turn state: {e.Message}");
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error in StartNewRound processing fight: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"Player or pet null in StartNewRound. PlayerID: {fightState.playerObjId}, PetID: {fightState.petObjId}");
            }
        }
    }

    [Server]
    private IEnumerator ProcessPetTurn(NetworkPet pet, NetworkPlayer player, FightTurnState fightState)
    {
        Debug.Log($"Pet {pet.PetName.Value}'s turn against {player.PlayerName.Value}.");
        
        // Wait a moment before starting pet actions for better player experience
        yield return new WaitForSeconds(1.5f);
        
        // Get the pet's cards in hand
        List<GameObject> cardsToPlay = new List<GameObject>();
        try 
        {
            // Get cards from CombatHand
            CombatHand petHand = pet.GetComponent<CombatHand>();
            if (petHand != null)
            {
                cardsToPlay = petHand.GetAllCardObjects();
                Debug.Log($"Pet {pet.PetName.Value} has {cardsToPlay.Count} cards in hand (via CombatHand)");
            }
            else
            {
                Debug.LogError($"CombatHand component not found on pet {pet.PetName.Value}");
                yield break;
            }
            
            // Log cards for debugging
            if (cardsToPlay.Count > 0)
            {
                string cardNames = string.Join(", ", cardsToPlay.Select(c => {
                    Card cardComponent = c.GetComponent<Card>();
                    return cardComponent != null ? cardComponent.CardName : "Unknown";
                }));
                Debug.Log($"Pet {pet.PetName.Value}'s cards in hand: {cardNames}");
            }
            else 
            {
                Debug.LogWarning($"Pet {pet.PetName.Value} has no cards in hand at the start of its turn!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error getting pet's cards: {e.Message}\n{e.StackTrace}");
            cardsToPlay = new List<GameObject>();
        }
        
        // Get the HandManager to handle moving cards to discard pile
        HandManager petHandManager = pet.GetComponent<HandManager>();
        if (petHandManager == null)
        {
            Debug.LogError($"HandManager component not found on pet {pet.PetName.Value}");
            yield break;
        }
        
        // Track if any cards were played
        bool playedAtLeastOneCard = false;
        int cardsPlayed = 0;
        
        // Make a copy of the cards to play to avoid collection modification issues during iteration
        List<GameObject> cardsCopy = new List<GameObject>(cardsToPlay);
        
        // Attempt to play each card
        foreach (GameObject cardObj in cardsCopy)
        {
            if (cardObj == null) continue;
            
            Card cardComponent = cardObj.GetComponent<Card>();
            if (cardComponent == null) continue;
            
            int cardId = cardComponent.CardId;
            
            CardData cardData = GetCardDataFromId(cardId);
            if (cardData == null)
            {
                Debug.LogError($"Failed to get card data for ID {cardId}");
                continue;
            }
            
            Debug.Log($"Pet {pet.PetName.Value} considering card {cardData.CardName} (ID: {cardId}, Cost: {cardData.EnergyCost}, Pet Energy: {pet.CurrentEnergy.Value})");
            
            if (cardData != null && pet.CurrentEnergy.Value >= cardData.EnergyCost)
            {
                // Use the pet's TryPlayCard method to properly handle card targeting based on target type
                if (pet.TryPlayCard(cardId, out string cardInstanceId, out NetworkBehaviour target))
                {
                    // Card was successfully played
                    Debug.Log($"Pet {pet.PetName.Value} played card ID: {cardId} on target: {target.name} (Instance ID: {cardInstanceId})");
                    
                    playedAtLeastOneCard = true;
                    cardsPlayed++;
                    
                    // Note: We don't move the card to discard here, the pet.TryPlayCard method already does this
                    
                    // Longer pause between card plays for better visualization
                    yield return new WaitForSeconds(2.5f);
                    
                    // Check game over condition after each card play
                    if (player.CurrentHealth.Value <= 0 || pet.CurrentHealth.Value <= 0)
                    {
                        HandleFightEnd(player, pet, fightState);
                        yield break;
                    }
                }
                else
                {
                    Debug.LogWarning($"Pet {pet.PetName.Value} failed to play card ID: {cardId}");
                }
            }
            else
            {
                Debug.Log($"Pet {pet.PetName.Value} doesn't have enough energy ({pet.CurrentEnergy.Value}) to play card ID: {cardId} (Cost: {cardData.EnergyCost})");
            }
        }

        // Log final play status
        if (!playedAtLeastOneCard)
        {
            // Get current cards in hand for final validation
            List<GameObject> currentCards = pet.GetComponent<CombatHand>()?.GetAllCardObjects() ?? new List<GameObject>();
            
            Debug.Log($"Pet {pet.PetName.Value} didn't play any cards this turn (played: {cardsPlayed}, " +
                      $"cards at start: {cardsToPlay.Count}, current cards: {currentCards.Count})");
            
            // Wait a moment to show the pet's "thinking" even if no cards played
            yield return new WaitForSeconds(1.0f);
        }

        Debug.Log($"Pet {pet.PetName.Value} ends its turn.");
        
        // Discard pet's hand using its HandManager
        if (petHandManager != null)
        {
            petHandManager.DiscardHand();
            
            // Get the client connection that owns this player
            NetworkConnection petTurnOwnerConn = player.Owner;
            
            // Log connection details to help with debugging
            Debug.Log($"CONNECTIONS (Pet Turn): Player {player.PlayerName.Value} owner ClientId: " +
                     $"{(petTurnOwnerConn != null ? petTurnOwnerConn.ClientId.ToString() : "null")}");
            
            // Notify clients to clear pet's hand display - send specifically to the player's owner
            if (petTurnOwnerConn != null)
            {
                Debug.Log($"Sending pet hand discard notification specifically to player owner connection {petTurnOwnerConn.ClientId}");
                RpcNotifyHandDiscarded(petTurnOwnerConn, (uint)pet.ObjectId);
            }
            else
            {
                Debug.LogWarning($"Player owner connection is null, sending broadcast notification for pet hand discard");
                RpcNotifyHandDiscarded(null, (uint)pet.ObjectId);
            }
            
            // Wait for discard animation to complete
            yield return new WaitForSeconds(1.5f);
        }
        else
        {
            Debug.LogError($"HandManager component not found on pet {pet.PetName.Value}");
        }

        // Check for game over conditions for this fight
        if (player.CurrentHealth.Value <= 0 || pet.CurrentHealth.Value <= 0)
        {
            HandleFightEnd(player, pet, fightState);
        }
        else
        {
            // Check if we should start a new round
            if (currentRound.Value > 0) // Make sure we've started combat
            {
                // Update turn state - back to player's turn
                var updatedFightState = fightState;
                updatedFightState.currentTurn = CombatTurn.PlayerTurn;
                int index = fightTurnStates.IndexOf(fightState);
                fightTurnStates[index] = updatedFightState;

                // Start a new round - this will replenish energy and draw cards, but only for this specific fight
                StartNewRound(updatedFightState);
                
                // Get the client connection that owns this player
                NetworkConnection turnEndOwnerConn = player.Owner;
                
                // Log connection details to help with debugging
                Debug.Log($"CONNECTIONS (Pet Turn End): Player {player.PlayerName.Value} owner ClientId: " +
                         $"{(turnEndOwnerConn != null ? turnEndOwnerConn.ClientId.ToString() : "null")}");
                
                // Ensure we have a valid connection to send to
                NetworkConnection targetConn = turnEndOwnerConn ?? Owner;
                
                Debug.Log($"Sending RPC notifications specifically to connection with ClientId: {targetConn.ClientId}");

                // Notify clients about the turn change
                RpcUpdateTurnState(targetConn, fightState.playerObjId, fightState.petObjId, CombatTurn.PlayerTurn);
                RpcEnableEndTurnButton(targetConn, true);

                Debug.Log($"Player {player.PlayerName.Value}'s turn has started again in a new round.");
            }
            else
            {
                Debug.LogError("Combat not properly initialized - round counter is 0 or negative");
            }
        }
    }

    [Server]
    private void HandleFightEnd(NetworkPlayer player, NetworkPet pet, FightTurnState fightState)
    {
        string winnerName = player.CurrentHealth.Value <= 0 ? pet.PetName.Value : player.PlayerName.Value;
        Debug.Log($"Fight between {player.PlayerName.Value} and {pet.PetName.Value} has ended. Winner: {winnerName}");

        // Remove from active fights
        fightTurnStates.Remove(fightState);
        activeFightCount--;

        // Get the client connection that owns the player
        NetworkConnection fightEndOwnerConn = player.Owner;
        
        // Log connection details to help with debugging
        Debug.Log($"CONNECTIONS (Fight End): Player {player.PlayerName.Value} owner ClientId: " +
                 $"{(fightEndOwnerConn != null ? fightEndOwnerConn.ClientId.ToString() : "null")}");
        
        // Notify only the clients involved in this fight
        Debug.Log($"Sending fight end notification to connection {(fightEndOwnerConn != null ? fightEndOwnerConn.ClientId.ToString() : "null")}");
        RpcNotifyFightEnded(fightEndOwnerConn, fightState.playerObjId, fightState.petObjId, player.CurrentHealth.Value <= 0);

        // If all fights are done, transition to draft phase
        if (activeFightCount <= 0)
        {
            // All fights are done
            Debug.Log("All fights have concluded. Transitioning to draft phase.");
            
            // Wait a short time before transitioning
            StartCoroutine(TransitionToDraftPhase());
        }
    }
    
    [Server]
    private IEnumerator TransitionToDraftPhase()
    {
        // Allow some time for final fight end visuals
        yield return new WaitForSeconds(3f);
        
        // Transition to draft phase using GamePhaseManager instead of directly calling DraftSetup
        if (gamePhaseManager != null)
        {
            Debug.Log("CombatManager: Transitioning to Draft phase via GamePhaseManager");
            gamePhaseManager.SetDraftPhase();
        }
        else
        {
            Debug.LogError("Cannot transition to draft phase: GamePhaseManager reference is null");
            
            // Fallback - try to find DraftSetup directly if GamePhaseManager isn't available
            DraftSetup draftSetup = FindFirstObjectByType<DraftSetup>();
            if (draftSetup != null)
            {
                Debug.LogWarning("Using fallback method to initialize draft phase via DraftSetup directly");
                draftSetup.InitializeDraft();
            }
            else
            {
                Debug.LogError("Both GamePhaseManager and DraftSetup are null - cannot transition to draft phase");
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlayerRequestsPlayCard(int playerIdArg, int cardId, string cardInstanceId)
    {
        // Find the requesting player directly by ID
        NetworkPlayer requestingPlayer = null;
        if (NetworkManager.ServerManager.Objects.Spawned.TryGetValue(playerIdArg, out NetworkObject playerObj))
        {
            requestingPlayer = playerObj.GetComponent<NetworkPlayer>();
            Debug.Log($"Found requesting player: {requestingPlayer.PlayerName.Value} (ID: {playerIdArg})");
        }
        
        if (requestingPlayer == null)
        {
            Debug.LogError("Could not determine player for card play request.");
            return;
        }

        // Find the player's fight state
        FightTurnState? fightStateNullable = fightTurnStates.Find(state => state.playerObjId == requestingPlayer.ObjectId);
        
        if (!fightStateNullable.HasValue)
        {
            Debug.LogError($"No fight state found for player {requestingPlayer.PlayerName.Value} (ID: {requestingPlayer.ObjectId})");
            return;
        }
        
        FightTurnState fightState = fightStateNullable.Value;
        
        // Get target pet from fight state
        NetworkPet targetPet = GetNetworkObjectComponent<NetworkPet>(fightState.petObjId);
        if (targetPet == null)
        {
            Debug.LogError($"Target pet not found for ObjectId: {fightState.petObjId}");
            RpcNotifyMessage(requestingPlayer.Owner, "Error: Target not found.");
            return;
        }

        // Create HandleCardPlay or use existing one
        HandleCardPlay cardPlayHandler = FindOrCreateCardPlayHandler(requestingPlayer);
        if (cardPlayHandler == null)
        {
            Debug.LogError("Failed to create HandleCardPlay instance");
            return;
        }

        // Handle the card play request
        bool success = cardPlayHandler.HandleCardPlayRequest(requestingPlayer, cardId, cardInstanceId);
        
        // Check for game over after card play
        if (success && (targetPet.CurrentHealth.Value <= 0 || requestingPlayer.CurrentHealth.Value <= 0))
        {
            HandleFightEnd(requestingPlayer, targetPet, fightState);
        }
    }
    
    /// <summary>
    /// Find or create a HandleCardPlay instance for a player
    /// </summary>
    private HandleCardPlay FindOrCreateCardPlayHandler(NetworkPlayer player)
    {
        if (player == null) return null;
        
        // Check if player already has HandleCardPlay component
        HandleCardPlay handler = player.GetComponent<HandleCardPlay>();
        
        // If not, create one
        if (handler == null)
        {
            handler = player.gameObject.AddComponent<HandleCardPlay>();
            handler.SetOwnerEntity(player.gameObject);
        }
        
        return handler;
    }

    /// <summary>
    /// Send a notification message to a specific player
    /// </summary>
    public void SendNotificationToPlayer(string message, NetworkConnection playerConnection)
    {
        if (playerConnection != null)
        {
            RpcNotifyMessage(playerConnection, message);
        }
    }
    
    /// <summary>
    /// Notify clients that a card was played
    /// </summary>
    public void NotifyCardPlayed(uint playerObjId, uint targetObjId, int cardId, string cardInstanceId, NetworkConnection playerConnection)
    {
        if (playerConnection != null)
        {
            RpcNotifyCardPlayed(playerConnection, playerObjId, targetObjId, cardId, cardInstanceId);
        }
    }

    // Helper method to get entity from ObjectId
    private T GetNetworkObjectComponent<T>(uint objectId) where T : NetworkBehaviour
    {
        Debug.Log($"GetNetworkObjectComponent called for ObjectID: {objectId}, Type: {typeof(T).Name}, IsServer: {IsServerInitialized}, IsClient: {IsClientInitialized}");
        
        // First check cache for faster lookups
        if (typeof(T) == typeof(NetworkPlayer))
        {
            if (playerCache.TryGetValue(objectId, out NetworkPlayer cachedPlayer))
            {
                Debug.Log($"Found cached player for ID {objectId}: {cachedPlayer.PlayerName.Value}");
                return cachedPlayer as T;
            }
        }
        else if (typeof(T) == typeof(NetworkPet))
        {
            if (petCache.TryGetValue(objectId, out NetworkPet pet))
            {
                Debug.Log($"Found cached pet for ID {objectId}: {pet.PetName.Value}");
                return pet as T;
            }
        }
        
        // If not in cache, look up in appropriate NetworkManager
        NetworkObject netObj = null;
        
        // Try server objects first if we're on the server
        if (IsServerInitialized && NetworkManager.IsServerStarted && NetworkManager.ServerManager != null)
        {
            bool found = NetworkManager.ServerManager.Objects.Spawned.TryGetValue((int)objectId, out netObj);
            Debug.Log($"Server lookup for ID {objectId}: {(found ? "Found" : "Not found")}");
        }
        
        // If not found on server or we're client-only, try client objects
        if (netObj == null && IsClientInitialized && NetworkManager.IsClientStarted && NetworkManager.ClientManager != null)
        {
            bool found = NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)objectId, out netObj);
            Debug.Log($"Client lookup for ID {objectId}: {(found ? "Found" : "Not found")}");
        }
        
        if (netObj != null)
        {
            T component = netObj.GetComponent<T>();
            
            if (component != null)
            {
                // Display found component info
                string entityName = component is NetworkPlayer player ? player.PlayerName.Value : 
                                   (component is NetworkPet pet ? pet.PetName.Value : "Unknown");
                Debug.Log($"Found component of type {typeof(T).Name} for ID {objectId}: {entityName}");
                
                // Cache the result for future lookups
                if (component is NetworkPlayer playerComponent)
                    playerCache[objectId] = playerComponent;
                else if (component is NetworkPet petComponent)
                    petCache[objectId] = petComponent;
                    
                return component;
            }
            else
            {
                Debug.LogError($"Network object found for ID {objectId} but does not have component of type {typeof(T).Name}");
            }
        }
        else
        {
            Debug.LogError($"Could not find any network object with ID {objectId} on {(IsServerInitialized ? "server" : "client")}");
        }
        
        return null;
    }

    // Get CardData from CardId
    private CardData GetCardDataFromId(int cardId)
    {
        // This should use a proper card database in your actual implementation
        if (CardDatabase.Instance != null)
        {
            return CardDatabase.Instance.GetCardById(cardId);
        }
        
        Debug.LogError($"Card database not found or card ID {cardId} not in database");
        return null;
    }

    #region RPCs and client notifications

    [TargetRpc]
    private void RpcUpdateTurnState(NetworkConnection playerConnection, uint playerObjId, uint petObjId, CombatTurn newTurnState)
    {
        Debug.Log($"RpcUpdateTurnState received - PlayerID: {playerObjId}, PetID: {petObjId}, " +
                 $"Turn: {newTurnState}, TargetClientId: {(playerConnection != null ? playerConnection.ClientId.ToString() : "null")}, " +
                 $"LocalClientId: {NetworkManager.ClientManager.Connection.ClientId}");
                 
        // Find the local player that belongs to this client using RelationshipManager for consistent approach
        NetworkPlayer localPlayer = FindLocalPlayerUsingRelationshipManager();
        if (localPlayer == null)
        {
            Debug.Log($"RpcUpdateTurnState: No local player found using RelationshipManager");
            return;
        }

        Debug.Log($"RpcUpdateTurnState found local player: {localPlayer.PlayerName.Value} (ID: {localPlayer.ObjectId})");
        
        // Get local player's pet (opponent)
        NetworkPet localPlayerPet = null;
        if (FightManager.Instance != null)
        {
            localPlayerPet = FightManager.Instance.GetOpponentForPlayer(localPlayer);
            if (localPlayerPet != null)
            {
                Debug.Log($"RpcUpdateTurnState found local player's pet: {localPlayerPet.PetName.Value} (ID: {localPlayerPet.ObjectId})");
            }
        }

        // Find the player and pet objects by their IDs
        NetworkPlayer foundPlayer = null;
        NetworkPet foundPet = null;
        
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)playerObjId, out NetworkObject playerObj))
        {
            foundPlayer = playerObj.GetComponent<NetworkPlayer>();
            Debug.Log($"RpcUpdateTurnState found player: {foundPlayer.PlayerName.Value}");
        }
        else
        {
            Debug.LogError($"RpcUpdateTurnState: Could not find player object with ID {playerObjId}");
            return;
        }
        
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)petObjId, out NetworkObject petObj))
        {
            foundPet = petObj.GetComponent<NetworkPet>();
            Debug.Log($"RpcUpdateTurnState found pet: {foundPet.PetName.Value}");
        }
        else
        {
            Debug.LogError($"RpcUpdateTurnState: Could not find pet object with ID {petObjId}");
            return;
        }

        // Store the current turn state in a client-side cache for this player's fight
        clientFightTurnState = new ClientFightTurnState 
        {
            playerObjId = playerObjId,
            petObjId = petObjId,
            currentTurn = newTurnState
        };
        
        // Mark that combat has been initialized for this client
        clientCombatInitialized = true;
        
        Debug.Log($"Client received turn update: {(newTurnState == CombatTurn.PlayerTurn ? foundPlayer.PlayerName.Value : foundPet.PetName.Value)}'s turn. Combat initialized.");

        // Update UI accordingly
        if (combatCanvasManager != null)
        {
            combatCanvasManager.UpdateTurnIndicator(newTurnState == CombatTurn.PlayerTurn ? foundPlayer.PlayerName.Value : foundPet.PetName.Value);
        }
        else
        {
            Debug.LogWarning("Could not update UI - combatCanvasManager is null");
        }
    }

    [TargetRpc]
    private void RpcEnableEndTurnButton(NetworkConnection playerConnection, bool enabled)
    {
        Debug.Log($"RpcEnableEndTurnButton received - Enabled: {enabled}, TargetClientId: {(playerConnection != null ? playerConnection.ClientId.ToString() : "null")}, LocalClientId: {NetworkManager.ClientManager.Connection.ClientId}");
        
        if (combatCanvasManager != null)
        {
            combatCanvasManager.SetEndTurnButtonInteractable(enabled);
        }
        else
        {
            Debug.LogWarning("RpcEnableEndTurnButton: combatCanvasManager is null");
        }
    }

    [TargetRpc]
    private void RpcNotifyCardPlayed(NetworkConnection playerConnection, uint casterObjId, uint targetObjId, int cardId)
    {
        // Raw network values received
        Debug.Log($"RPC ANALYSIS - RpcNotifyCardPlayed raw values received directly from network:" +
                  $"\n  CasterID: {casterObjId}" +
                  $"\n  TargetID: {targetObjId}" +
                  $"\n  CardID: {cardId}" +
                  $"\n  TargetClientId: {(playerConnection != null ? playerConnection.ClientId.ToString() : "-1")}" +
                  $"\n  Received by CombatManager ID: {ObjectId}" +
                  $"\n  IsServer: {IsServerInitialized}, IsClient: {IsClientInitialized}, IsOwner: {IsOwner}");
        
        NetworkBehaviour caster = null;
        NetworkBehaviour target = null;
        
        // Properly retrieve caster object by ObjectId
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)casterObjId, out NetworkObject casterObj))
        {
            caster = casterObj.GetComponent<NetworkBehaviour>();
            Debug.Log($"Successfully found caster object with ID {casterObjId}");
        }
        else
        {
            Debug.LogError($"Failed to find caster object with ID {casterObjId}");
            return;
        }
        
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)targetObjId, out NetworkObject targetObj))
        {
            target = targetObj.GetComponent<NetworkBehaviour>();
            Debug.Log($"Successfully found target object with ID {targetObjId}");
        }
        else
        {
            Debug.LogError($"Failed to find target object with ID {targetObjId}");
            return;
        }

        // Fix variable naming conflict by using different variable names
        string casterName = caster is NetworkPlayer playerCaster ? playerCaster.PlayerName.Value : ((NetworkPet)caster).PetName.Value;
        string targetName = target is NetworkPlayer targetPlayer ? targetPlayer.PlayerName.Value : ((NetworkPet)target).PetName.Value;

        // Ensure we show the right player ID
        int casterId = caster is NetworkPlayer playerCaster2 ? playerCaster2.ObjectId : -1;
        
        Debug.Log($"Card played: {casterName} (ID: {casterId}) played card ID {cardId} on {targetName}");

        // Local client UI updates for card being played
        if (combatCanvasManager != null)
        {
            combatCanvasManager.ShowCardPlayedEffect(cardId, caster, target);
        }
        else
        {
            Debug.LogWarning("RpcNotifyCardPlayed: combatCanvasManager is null");
        }
        
        // Only notify the caster object that played the card
        if (caster is NetworkPlayer playerCasterObj)
        {
            playerCasterObj.NotifyCardPlayed(cardId);
        }
        else if (caster is NetworkPet petCasterObj)
        {
            petCasterObj.NotifyCardPlayed(cardId);
        }
    }

    [TargetRpc]
    private void RpcNotifyCardPlayed(NetworkConnection playerConnection, uint casterObjId, uint targetObjId, int cardId, string cardInstanceId)
    {
        // Raw network values received
        Debug.Log($"RPC ANALYSIS - RpcNotifyCardPlayed raw values received directly from network:" +
                  $"\n  CasterID: {casterObjId}" +
                  $"\n  TargetID: {targetObjId}" +
                  $"\n  CardID: {cardId}" +
                  $"\n  InstanceID: {cardInstanceId}" +
                  $"\n  TargetClientId: {(playerConnection != null ? playerConnection.ClientId.ToString() : "-1")}" +
                  $"\n  Received by CombatManager ID: {ObjectId}" +
                  $"\n  IsServer: {IsServerInitialized}, IsClient: {IsClientInitialized}, IsOwner: {IsOwner}");
        
        NetworkBehaviour caster = null;
        NetworkBehaviour target = null;
        
        // Properly retrieve caster object by ObjectId
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)casterObjId, out NetworkObject casterObj))
        {
            caster = casterObj.GetComponent<NetworkBehaviour>();
            Debug.Log($"Successfully found caster object with ID {casterObjId}");
        }
        else
        {
            Debug.LogError($"Failed to find caster object with ID {casterObjId}");
            return;
        }
        
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)targetObjId, out NetworkObject targetObj))
        {
            target = targetObj.GetComponent<NetworkBehaviour>();
            Debug.Log($"Successfully found target object with ID {targetObjId}");
        }
        else
        {
            Debug.LogError($"Failed to find target object with ID {targetObjId}");
            return;
        }

        // Fix variable naming conflict by using different variable names
        string casterName = caster is NetworkPlayer playerCaster ? playerCaster.PlayerName.Value : ((NetworkPet)caster).PetName.Value;
        string targetName = target is NetworkPlayer targetPlayer ? targetPlayer.PlayerName.Value : ((NetworkPet)target).PetName.Value;

        // Ensure we show the right player ID
        int casterId = caster is NetworkPlayer playerCaster2 ? playerCaster2.ObjectId : -1;
        
        Debug.Log($"Card played: {casterName} (ID: {casterId}) played card ID {cardId}, Instance {cardInstanceId} on {targetName}");

        // Local client UI updates for card being played
        if (combatCanvasManager != null)
        {
            combatCanvasManager.ShowCardPlayedEffect(cardId, caster, target);
        }
        else
        {
            Debug.LogWarning("RpcNotifyCardPlayed: combatCanvasManager is null");
        }
        
        // Only notify the caster object that played the card
        if (caster is NetworkPlayer playerCasterObj)
        {
            playerCasterObj.NotifyCardPlayed(cardId, cardInstanceId);
        }
        else if (caster is NetworkPet petCasterObj)
        {
            petCasterObj.NotifyCardPlayed(cardId, cardInstanceId);
        }
    }

    [TargetRpc]
    private void RpcNotifyFightEnded(NetworkConnection playerConnection, uint playerObjId, uint petObjId, bool petWon)
    {
        Debug.Log($"RpcNotifyFightEnded received - PlayerID: {playerObjId}, PetID: {petObjId}, PetWon: {petWon}");
        
        // Find player and pet objects by their IDs
        NetworkPlayer foundPlayer = null;
        NetworkPet foundPet = null;
        
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)playerObjId, out NetworkObject playerObj))
        {
            foundPlayer = playerObj.GetComponent<NetworkPlayer>();
        }
        else
        {
            Debug.LogError($"RpcNotifyFightEnded: Could not find player object with ID {playerObjId}");
            return;
        }
        
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)petObjId, out NetworkObject petObj))
        {
            foundPet = petObj.GetComponent<NetworkPet>();
        }
        else
        {
            Debug.LogError($"RpcNotifyFightEnded: Could not find pet object with ID {petObjId}");
            return;
        }

        string winnerName = petWon ? foundPet.PetName.Value : foundPlayer.PlayerName.Value;
        Debug.Log($"Fight ended between {foundPlayer.PlayerName.Value} and {foundPet.PetName.Value}. Winner: {winnerName}");

        // Update local client UI
        if (combatCanvasManager != null)
        {
            combatCanvasManager.ShowFightEndedUI(foundPlayer, foundPet, petWon);
        }
        else
        {
            Debug.LogWarning("RpcNotifyFightEnded: combatCanvasManager is null");
        }
    }

    [TargetRpc]
    private void RpcNotifyMessage(NetworkConnection playerConnection, string message)
    {
        Debug.Log($"Combat notification: {message}");
        if (combatCanvasManager != null)
        {
            combatCanvasManager.ShowNotificationMessage(message);
        }
        else
        {
            Debug.LogWarning("RpcNotifyMessage: combatCanvasManager is null");
        }
    }

    /// <summary>
    /// Notifies clients to clear the visual hand display for an entity
    /// </summary>
    [TargetRpc]
    private void RpcNotifyHandDiscarded(NetworkConnection targetConnection, uint entityObjId)
    {
        Debug.Log($"RpcNotifyHandDiscarded received for entity ID: {entityObjId}");
        
        // Find the entity (player or pet)
        NetworkBehaviour entity = null;
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)entityObjId, out NetworkObject netObj))
        {
            entity = netObj.GetComponent<NetworkBehaviour>();
        }
        
        if (entity != null)
        {
            Debug.Log($"Processing hand discard for entity {entityObjId}");
            
            // Get the CardSpawner component and clear all cards
            CardSpawner cardSpawner = entity.GetComponent<CardSpawner>();
            if (cardSpawner != null)
            {
                Debug.Log($"Notifying CardSpawner to clear all cards for entity {entityObjId}");
                cardSpawner.HandleHandDiscarded();
            }
            else
            {
                Debug.LogWarning($"CardSpawner component not found on entity {entityObjId}");
            }
        }
        else
        {
            Debug.LogError($"Entity with ID {entityObjId} not found in client objects");
        }
    }

    #endregion

    // Helper method to check if it's a player's turn
    public bool IsPlayerTurn(NetworkPlayer player)
    {
        if (player == null) return false;
        
        // On the server, use the server-authoritative list
        if (IsServerInitialized)
        {
            // Get the player's fight state
            FightTurnState? fightState = fightTurnStates.Find(state => state.playerObjId == player.ObjectId);
            if (!fightState.HasValue) return false;
            
            // Check if it's the player's turn
            return fightState.Value.currentTurn == CombatTurn.PlayerTurn;
        }
        // On the client, use the client-side cached turn state
        else
        {
            // For better debugging, log what we know about the state
            Debug.Log($"Client IsPlayerTurn check for Player {player.PlayerName.Value} (ID: {player.ObjectId}), " +
                      $"Current client state: PlayerID: {clientFightTurnState.playerObjId}, " +
                      $"Turn: {clientFightTurnState.currentTurn}");
                      
            // Check if this player is in the client's cached fight
            if (clientFightTurnState.playerObjId == player.ObjectId)
            {
                return clientFightTurnState.currentTurn == CombatTurn.PlayerTurn;
            }
            
            // If we don't have a match, it's probably not initialized yet
            return false;
        }
    }
    
    // Helper method to check if combat has been properly initialized
    public bool IsCombatInitialized()
    {
        // On server, check if we have fight states and the round has started
        if (IsServerInitialized)
        {
            return fightTurnStates != null && fightTurnStates.Count > 0 && currentRound.Value > 0;
        }
        // On client, use the client combat initialized flag
        else
        {
            if (!clientCombatInitialized)
            {
                Debug.Log("Client combat not yet initialized. Waiting for server to send turn state updates.");
            }
            return clientCombatInitialized;
        }
    }

    /// <summary>
    /// Finds a player by client ID using RelationshipManager
    /// </summary>
    private NetworkPlayer FindPlayerByClientId(int clientId)
    {
        // Get all players in the scene
        var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        
        // First try to find by RelationshipManager ClientId
        foreach (var player in allPlayers)
        {
            var relationshipManager = player.GetComponent<RelationshipManager>();
            if (relationshipManager != null && relationshipManager.OwnerClientId == clientId)
            {
                Debug.Log($"Found player by RelationshipManager ClientId: {player.PlayerName.Value} with ClientId {clientId}");
                return player;
            }
        }
        
        // Fall back to direct owner connection
        foreach (var player in allPlayers)
        {
            if (player.Owner != null && player.Owner.ClientId == clientId)
            {
                Debug.Log($"Found player by connection ClientId match: {player.PlayerName.Value} with ClientId {clientId}");
                return player;
            }
        }
        
        // If client ID is 0 or -1 and we're the host, find the host player as fallback
        if ((clientId == 0 || clientId == -1) && NetworkManager.IsHostStarted)
        {
            foreach (var player in allPlayers)
            {
                var relationshipManager = player.GetComponent<RelationshipManager>();
                if (relationshipManager != null && relationshipManager.IsOwnedByServer)
                {
                    Debug.Log($"Found host player as fallback: {player.PlayerName.Value}");
                    return player;
                }
            }
        }
        
        // If we couldn't find a player, return null
        Debug.LogWarning($"No player found with ClientId {clientId}");
        return null;
    }

    /// <summary>
    /// Called by the client to end their turn. This RPC sends the player's ObjectId to avoid
    /// connection ID issues on the server.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void CmdEndTurnForPlayer(int playerObjectId)
    {
        // Find the player object directly by ID
        NetworkPlayer targetPlayer = null;
        if (NetworkManager.ServerManager.Objects.Spawned.TryGetValue(playerObjectId, out NetworkObject playerObj))
        {
            targetPlayer = playerObj.GetComponent<NetworkPlayer>();
            Debug.Log($"Found target player: {targetPlayer.PlayerName.Value} (ID: {playerObjectId})");
        }
        
        if (targetPlayer == null)
        {
            Debug.LogError($"Could not find player with ObjectId {playerObjectId}");
            return;
        }
        
        // Once we have the player, find the fight state
        FightTurnState? fightStateNullable = fightTurnStates.Find(state => state.playerObjId == targetPlayer.ObjectId);
        
        if (!fightStateNullable.HasValue)
        {
            Debug.LogError($"No fight state found for player {targetPlayer.PlayerName.Value} (ID: {targetPlayer.ObjectId})");
            return;
        }
        
        FightTurnState fightState = fightStateNullable.Value;
        
        // Double-check that we're using the right player
        NetworkPlayer fightPlayer = GetNetworkObjectComponent<NetworkPlayer>(fightState.playerObjId);
        if (fightPlayer == null)
        {
            Debug.LogError($"Player not found for ObjectId: {fightState.playerObjId}");
            return;
        }
        
        // Log details about which player's turn we're ending
        Debug.Log($"Ending turn for player {fightPlayer.PlayerName.Value} (ID: {fightPlayer.ObjectId})");
        
        if (fightState.currentTurn != CombatTurn.PlayerTurn)
        {
            Debug.LogWarning($"Player tried to end turn, but it's not their turn (state: {fightState.currentTurn})");
            return;
        }

        // Discard player's hand using their HandManager component
        HandManager playerHandManager = fightPlayer.GetComponent<HandManager>();
        if (playerHandManager != null)
        {
            playerHandManager.DiscardHand();
            
            // Get player's owner connection
            NetworkConnection petTurnOwnerConn = fightPlayer.Owner;
            
            // Notify clients to clear player's hand display
            Debug.Log($"Sending player hand discard notification to connection {(petTurnOwnerConn != null ? petTurnOwnerConn.ClientId.ToString() : "null")}");
            RpcNotifyHandDiscarded(petTurnOwnerConn, (uint)fightPlayer.ObjectId);
        }
        else
        {
            Debug.LogError($"HandManager component not found on player {fightPlayer.PlayerName.Value}");
        }

        // Update turn state
        var updatedFightState = fightState;
        updatedFightState.currentTurn = CombatTurn.PetTurn;
        int index = fightTurnStates.IndexOf(fightState);
        fightTurnStates[index] = updatedFightState;

        // Get the client connection that owns this player
        NetworkConnection turnEndOwnerConn = fightPlayer.Owner;
        
        // Ensure we have a valid connection to send to
        NetworkConnection targetConn = turnEndOwnerConn ?? Owner;
        
        Debug.Log($"Sending RPC notifications to connection with ClientId: {targetConn.ClientId}");

        // Notify clients about the turn state change
        RpcUpdateTurnState(targetConn, fightState.playerObjId, fightState.petObjId, CombatTurn.PetTurn);
        
        // Notify clients to disable end turn button
        RpcEnableEndTurnButton(targetConn, false);
        
        // Start pet turn process
        NetworkPet pet = GetNetworkObjectComponent<NetworkPet>(fightState.petObjId);
        if (pet != null)
        {
            StartCoroutine(ProcessPetTurn(pet, fightPlayer, updatedFightState));
        }
        else
        {
            Debug.LogError($"Pet not found for ObjectId: {fightState.petObjId}");
        }
    }

    /// <summary>
    /// Finds the NetworkPlayer that is owned by the local player using RelationshipManager
    /// </summary>
    public NetworkPlayer FindLocalPlayerUsingRelationshipManager()
    {
        var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            // Use RelationshipManager to check for local player ownership
            RelationshipManager relationshipManager = player.GetComponent<RelationshipManager>();
            if (relationshipManager != null && relationshipManager.IsOwnedByLocalPlayer)
            {
                Debug.Log($"Found local player {player.PlayerName.Value} using RelationshipManager");
                return player;
            }
        }
        
        // Fall back to checking for IsOwner if RelationshipManager approach fails
        foreach (var player in allPlayers)
        {
            if (player.IsOwner)
            {
                Debug.Log($"Found local player {player.PlayerName.Value} using IsOwner");
                return player;
            }
        }
        
        Debug.LogWarning("Could not find local player");
        return null;
    }
    
    /// <summary>
    /// Ends the turn for the local player by finding them via RelationshipManager
    /// </summary>
    public void EndTurnForLocalPlayer()
    {
        NetworkPlayer localPlayer = FindLocalPlayerUsingRelationshipManager();
        if (localPlayer == null)
        {
            Debug.LogError("Cannot end turn - local player not found");
            return;
        }
        
        Debug.Log($"Ending turn for local player {localPlayer.PlayerName.Value} (ID: {localPlayer.ObjectId})");
        CmdEndTurnForPlayer(localPlayer.ObjectId);
    }
} 