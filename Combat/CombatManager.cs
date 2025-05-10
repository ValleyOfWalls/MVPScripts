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

    private struct FightTurnState
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
        
        // Start the first round of combat
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
    public void StartNewRound()
    {
        currentRound.Value++;

        Debug.Log($"Starting round {currentRound.Value}");
        
        // Create a local copy of the fight states to avoid collection modification issues
        List<FightTurnState> fightStatesCopy = new List<FightTurnState>(fightTurnStates);

        // For all entities in all fights, replenish energy and draw cards
        foreach (var fightState in fightStatesCopy)
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
                        
                        // Notify client about turn change
                        if (player.Owner != null)
                        {
                            try
                            {
                                // Send ObserversRpcs instead of TargetRpcs
                                RpcUpdateTurnState(fightState.playerObjId, fightState.petObjId, CombatTurn.PlayerTurn, player.Owner);
                                RpcEnableEndTurnButton(true, player.Owner);
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
        }
    }

    [Server]
    private IEnumerator ProcessPetTurn(NetworkPet pet, NetworkPlayer player, FightTurnState fightState)
    {
        Debug.Log($"Pet {pet.PetName.Value}'s turn against {player.PlayerName.Value}.");
        
        // Wait a moment before starting pet actions for better player experience
        yield return new WaitForSeconds(1.5f);
        
        // Get the pet's cards in hand - use a try/catch for better error handling
        List<int> cardsToPlay = new List<int>();
        try 
        {
            // Get a CombatHand reference first to see actual hand state
            CombatHand petHand = pet.GetComponent<CombatHand>();
            if (petHand != null)
            {
                cardsToPlay = petHand.GetAllCards();
                Debug.Log($"Pet {pet.PetName.Value} has {cardsToPlay.Count} cards in hand (via CombatHand)");
            }
            else
            {
                // Fall back to SyncList if CombatHand not available
                cardsToPlay = new List<int>(pet.playerHandCardIds);
                Debug.Log($"Pet {pet.PetName.Value} has {cardsToPlay.Count} cards in hand (via SyncList)");
            }
            
            // Log each card ID for debugging
            if (cardsToPlay.Count > 0)
            {
                Debug.Log($"Pet {pet.PetName.Value}'s cards in hand: {string.Join(", ", cardsToPlay)}");
            }
            else 
            {
                Debug.LogWarning($"Pet {pet.PetName.Value} has no cards in hand at the start of its turn!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error getting pet's cards: {e.Message}\n{e.StackTrace}");
            cardsToPlay = new List<int>();
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
        
        // Log actual card count in SyncList for debugging
        Debug.Log($"Pet {pet.PetName.Value}'s playerHandCardIds.Count is {pet.playerHandCardIds.Count}");

        // If no cards to play, check if we need to reset state
        if (cardsToPlay.Count == 0 && pet.playerHandCardIds.Count > 0)
        {
            // There's a synchronization issue - the sync list has cards but our local copy doesn't
            Debug.LogWarning($"Synchronization issue detected: SyncList has {pet.playerHandCardIds.Count} cards but local copy has 0. Attempting to fix.");
            cardsToPlay = new List<int>(pet.playerHandCardIds);
        }
        // If the sync list is empty but CombatHand has cards, use those instead
        else if (pet.playerHandCardIds.Count == 0 && cardsToPlay.Count > 0)
        {
            Debug.LogWarning($"Detected mismatch: CombatHand has {cardsToPlay.Count} cards but SyncList is empty. Using CombatHand as source of truth.");
            
            // Manually update the pet's SyncList with the CombatHand's contents
            // This ensures cards can be played even if synchronization failed earlier
            pet.playerHandCardIds.Clear();
            foreach (int cardId in cardsToPlay)
            {
                pet.playerHandCardIds.Add(cardId);
            }
            
            Debug.Log($"Updated pet's playerHandCardIds with {cardsToPlay.Count} cards from CombatHand");
        }

        // Make a copy of the cards to play to avoid collection modification issues during iteration
        List<int> cardsCopy = new List<int>(cardsToPlay);
        
        // Attempt to play each card
        foreach (int cardId in cardsCopy)
        {
            // Double check that card is still in the pet's hand
            bool cardInHand = pet.playerHandCardIds.Contains(cardId);
            Debug.Log($"Checking if pet still has card {cardId} in hand: {cardInHand}");
            
            // Fall back to CombatHand if SyncList doesn't have the card
            if (!cardInHand)
            {
                CombatHand petHand = pet.GetComponent<CombatHand>();
                cardInHand = petHand != null && petHand.HasCard(cardId);
                
                if (cardInHand)
                {
                    Debug.LogWarning($"Card {cardId} not found in pet's SyncList but found in CombatHand. Using it anyway.");
                    
                    // Add to SyncList to allow play
                    pet.playerHandCardIds.Add(cardId);
                }
            }
            
            if (cardInHand)
            {
                CardData cardData = GetCardDataFromId(cardId); // This needs a Card Database
                if (cardData == null)
                {
                    Debug.LogError($"Failed to get card data for ID {cardId}");
                    continue;
                }
                
                Debug.Log($"Pet {pet.PetName.Value} considering card {cardData.CardName} (ID: {cardId}, Cost: {cardData.EnergyCost}, Pet Energy: {pet.CurrentEnergy.Value})");
                
                if (cardData != null && pet.CurrentEnergy.Value >= cardData.EnergyCost)
                {
                    // Use the pet's PlayCard method to properly handle card targeting based on target type
                    if (pet.TryPlayCard(cardId, out string cardInstanceId, out NetworkBehaviour target))
                    {
                        // Card was successfully played
                        Debug.Log($"Pet {pet.PetName.Value} played card ID: {cardId} on target: {target.name} (Instance ID: {cardInstanceId})");
                        
                        playedAtLeastOneCard = true;
                        cardsPlayed++;
                        
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
        }

        // Log final play status
        if (!playedAtLeastOneCard)
        {
            // Get current cards in hand for final validation
            List<int> currentCards = pet.GetComponent<CombatHand>()?.GetAllCards() ?? new List<int>();
            
            Debug.Log($"Pet {pet.PetName.Value} didn't play any cards this turn (played: {cardsPlayed}, " +
                      $"cards at start: {cardsToPlay.Count}, current cards: {currentCards.Count}, " +
                      $"cards in SyncList: {pet.playerHandCardIds.Count})");
            
            // Wait a moment to show the pet's "thinking" even if no cards played
            yield return new WaitForSeconds(1.0f);
        }

        Debug.Log($"Pet {pet.PetName.Value} ends its turn.");
        
        // Get final card count before discard for debugging
        int finalCardCount = pet.playerHandCardIds.Count;
        Debug.Log($"Pet {pet.PetName.Value} has {finalCardCount} cards in hand before discarding");
        
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
                RpcNotifyHandDiscarded((uint)pet.ObjectId, petTurnOwnerConn);
            }
            else
            {
                Debug.LogWarning($"Player owner connection is null, sending broadcast notification for pet hand discard");
                RpcNotifyHandDiscarded((uint)pet.ObjectId, null);
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

                // Start a new round - this will replenish energy and draw cards
                StartNewRound();
                
                // Get the client connection that owns this player
                NetworkConnection turnEndOwnerConn = player.Owner;
                
                // Log connection details to help with debugging
                Debug.Log($"CONNECTIONS (Pet Turn End): Player {player.PlayerName.Value} owner ClientId: " +
                         $"{(turnEndOwnerConn != null ? turnEndOwnerConn.ClientId.ToString() : "null")}");
                
                // Ensure we have a valid connection to send to
                NetworkConnection targetConn = turnEndOwnerConn ?? player.Owner;
                
                Debug.Log($"Sending RPC notifications specifically to connection with ClientId: {targetConn.ClientId}");

                // Notify clients about the turn change
                RpcUpdateTurnState(fightState.playerObjId, fightState.petObjId, CombatTurn.PlayerTurn, targetConn);
                RpcEnableEndTurnButton(true, targetConn);

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
        RpcNotifyFightEnded(fightState.playerObjId, fightState.petObjId, player.CurrentHealth.Value <= 0, fightEndOwnerConn);

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
        // Find the requesting player
        NetworkPlayer requestingPlayer = FindRequestingPlayer(playerIdArg);        
        if (requestingPlayer == null)
        {
            Debug.LogError("Could not determine player for card play request.");
            return;
        }

        // Find the player's fight state
        FightTurnState? fightStateNullable = FindFightStateForPlayer(requestingPlayer);        
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
            RpcNotifyMessage("Error: Target not found.", requestingPlayer.Owner);
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
    /// Find the requesting player based on playerIdArg and connection info
    /// </summary>
    private NetworkPlayer FindRequestingPlayer(int playerIdArg)
    {
        NetworkPlayer requestingPlayer = null;
        
        // Try to get the player by object ID
        if (NetworkManager.ServerManager.Objects.Spawned.TryGetValue(playerIdArg, out NetworkObject playerObj))
        {
            requestingPlayer = playerObj.GetComponent<NetworkPlayer>();
            return requestingPlayer;
        }
        
        // Fallback to finding through connection
        var conn = Owner;
        if (conn != null)
        {
            var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                if (player.Owner != null && player.Owner.ClientId == conn.ClientId)
                {
                    return player;
                }
            }
        }
        
        // Last resort fallback
        var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.IsOwner)
            {
                return player;
            }
        }
        
        // If all else fails, use first player
        if (players.Length > 0)
        {
            Debug.LogWarning("Could not determine requesting player. Using first available player as fallback.");
            return players[0];
        }
        
        return null;
    }
    
    /// <summary>
    /// Find the fight state for a player
    /// </summary>
    private FightTurnState? FindFightStateForPlayer(NetworkPlayer player)
    {
        if (player == null) return null;
        
        // Try to find by exact player object ID
        FightTurnState? fightState = fightTurnStates.Find(state => state.playerObjId == player.ObjectId);
        
        // If not found and we have any fight states, use the first one as fallback
        if (!fightState.HasValue && fightTurnStates.Count > 0)
        {
            Debug.LogWarning($"Using first available fight state as fallback");
            return fightTurnStates[0];
        }
        
        return fightState;
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
            RpcNotifyMessage(message, playerConnection);
        }
    }
    
    /// <summary>
    /// Notify clients that a card was played
    /// </summary>
    public void NotifyCardPlayed(uint playerObjId, uint targetObjId, int cardId, string cardInstanceId, NetworkConnection playerConnection)
    {
        if (playerConnection != null)
        {
            RpcNotifyCardPlayed(playerObjId, targetObjId, cardId, cardInstanceId, playerConnection);
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

    [ObserversRpc]
    private void RpcUpdateTurnState(uint playerObjId, uint petObjId, CombatTurn newTurnState, NetworkConnection playerConnection)
    {
        Debug.Log($"RpcUpdateTurnState received - PlayerID: {playerObjId}, PetID: {petObjId}, " +
                 $"Turn: {newTurnState}, TargetClientId: {(playerConnection != null ? playerConnection.ClientId.ToString() : "null")}, " +
                 $"LocalClientId: {NetworkManager.ClientManager.Connection.ClientId}");
                 
        // Find the local player that belongs to this client
        NetworkPlayer localPlayer = null;
        foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
        {
            if (player.IsOwner)
            {
                localPlayer = player;
                Debug.Log($"RpcUpdateTurnState found local owned player: {player.PlayerName.Value} (ID: {player.ObjectId})");
                break;
            }
        }
        
        // Get local player's pet (opponent)
        NetworkPet localPlayerPet = null;
        if (localPlayer != null && FightManager.Instance != null)
        {
            localPlayerPet = FightManager.Instance.GetOpponentForPlayer(localPlayer);
            if (localPlayerPet != null)
            {
                Debug.Log($"RpcUpdateTurnState found local player's pet: {localPlayerPet.PetName.Value} (ID: {localPlayerPet.ObjectId})");
            }
        }

        // Determine if this update is for the local player's fight
        bool shouldProcess = false;
        
        // Host processes all updates
        if (NetworkManager.IsHostStarted)
        {
            Debug.Log($"Host detected in RpcUpdateTurnState. Processing all turn updates.");
            shouldProcess = true;
        }
        // Client only needs updates for their own fight
        else if (localPlayer != null)
        {
            // Check if this update is specifically for the local player's fight
            if (localPlayer.ObjectId == playerObjId)
            {
                Debug.Log($"This turn update is for local player's fight. Processing.");
                shouldProcess = true;
            }
            // Check if this update is for the local player's pet opponent
            else if (localPlayerPet != null && localPlayerPet.ObjectId == petObjId)
            {
                Debug.Log($"This turn update involves local player's pet opponent. Processing.");
                shouldProcess = true;
            }
            // Also process if it's coming from the client's connection
            else if (NetworkManager.ClientManager.Connection.ClientId == playerConnection.ClientId)
            {
                Debug.Log($"This turn update is from local client's connection. Processing.");
                shouldProcess = true;
            }
            else
            {
                Debug.Log($"Turn update not for local fight: PlayerID {playerObjId}, PetID {petObjId}, Local Player ID: {localPlayer.ObjectId}, Local Pet ID: {(localPlayerPet != null ? localPlayerPet.ObjectId : -1)}");
            }
        }
        
        if (!shouldProcess)
        {
            Debug.Log($"RpcUpdateTurnState skipped - not relevant for this client ({NetworkManager.ClientManager.Connection.ClientId})");
            return;
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
        }
        
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)petObjId, out NetworkObject petObj))
        {
            foundPet = petObj.GetComponent<NetworkPet>();
            Debug.Log($"RpcUpdateTurnState found pet: {foundPet.PetName.Value}");
        }
        else
        {
            Debug.LogError($"RpcUpdateTurnState: Could not find pet object with ID {petObjId}");
        }

        if (foundPlayer != null && foundPet != null)
        {
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
        else
        {
            Debug.LogError($"RpcUpdateTurnState: Cannot process turn update - missing player or pet reference");
        }
    }

    [ObserversRpc]
    private void RpcEnableEndTurnButton(bool enabled, NetworkConnection playerConnection)
    {
        Debug.Log($"RpcEnableEndTurnButton received - Enabled: {enabled}, TargetClientId: {(playerConnection != null ? playerConnection.ClientId.ToString() : "null")}, LocalClientId: {NetworkManager.ClientManager.Connection.ClientId}");
        
        // Find the local player that belongs to this client
        NetworkPlayer localPlayer = null;
        foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
        {
            if (player.IsOwner)
            {
                localPlayer = player;
                Debug.Log($"RpcEnableEndTurnButton found local owned player: {player.PlayerName.Value} (ID: {player.ObjectId})");
                break;
            }
        }
        
        // Determine if this update is for the local player's UI
        bool shouldProcess = false;
        
        // Host processes all updates
        if (NetworkManager.IsHostStarted)
        {
            Debug.Log($"Host detected in RpcEnableEndTurnButton. Processing update.");
            shouldProcess = true;
        }
        // Client only needs updates for their own player
        else if (localPlayer != null)
        {
            // Check if the client connection matches
            if (NetworkManager.ClientManager.Connection.ClientId == playerConnection.ClientId)
            {
                Debug.Log($"This button update is for local client's connection. Processing.");
                shouldProcess = true;
            }
            // Check if the connection ID matches the local player's owner
            else if (localPlayer.Owner != null && playerConnection != null && localPlayer.Owner.ClientId == playerConnection.ClientId)
            {
                Debug.Log($"This button update is for local player's owner. Processing.");
                shouldProcess = true;
            }
            else
            {
                Debug.Log($"Button update not for local player - Local Player ClientId: {(localPlayer.Owner != null ? localPlayer.Owner.ClientId.ToString() : "null")}, Update for ClientId: {(playerConnection != null ? playerConnection.ClientId.ToString() : "null")}");
            }
        }
        
        if (!shouldProcess)
        {
            Debug.Log($"RpcEnableEndTurnButton skipped - not relevant for this client ({NetworkManager.ClientManager.Connection.ClientId})");
            return;
        }
        
        if (combatCanvasManager != null)
        {
            combatCanvasManager.SetEndTurnButtonInteractable(enabled);
        }
        else
        {
            Debug.LogWarning("RpcEnableEndTurnButton: combatCanvasManager is null");
        }
    }

    [ObserversRpc]
    private void RpcNotifyCardPlayed(uint casterObjId, uint targetObjId, int cardId, NetworkConnection playerConnection)
    {
        // Raw network values received
        Debug.Log($"RPC ANALYSIS - RpcNotifyCardPlayed raw values received directly from network:" +
                  $"\n  CasterID: {casterObjId}" +
                  $"\n  TargetID: {targetObjId}" +
                  $"\n  CardID: {cardId}" +
                  $"\n  TargetClientId: {(playerConnection != null ? playerConnection.ClientId.ToString() : "-1")}" +
                  $"\n  Received by CombatManager ID: {ObjectId}" +
                  $"\n  IsServer: {IsServerInitialized}, IsClient: {IsClientInitialized}, IsOwner: {IsOwner}");
        
        Debug.Log($"RpcNotifyCardPlayed received - CasterID: {casterObjId}, TargetID: {targetObjId}, CardID: {cardId}, TargetClientId: {(playerConnection != null ? playerConnection.ClientId.ToString() : "-1")}");
        
        // Special handling for host - host needs to process all card plays
        bool shouldProcess = false;
        
        if (NetworkManager.IsHostStarted)
        {
            // Host should process all card plays
            Debug.Log($"Host detected in RpcNotifyCardPlayed. Processing card play notification.");
            shouldProcess = true;
        }
        else
        {
            // For non-host clients, check if they're involved in this fight
            // Get the local player and check if it's involved in this card play
            NetworkPlayer thisClientPlayer = null;
            foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
            {
                if (player.IsOwner)
                {
                    thisClientPlayer = player;
                    Debug.Log($"Found local client player: {player.PlayerName.Value}, ID: {player.ObjectId}");
                    break;
                }
            }
            
            // If we're running on a client, this must be for this client
            if (NetworkManager.IsClientOnlyStarted)
            {
                shouldProcess = true;
                Debug.Log($"Client-only detected in RpcNotifyCardPlayed. Processing card play notification for client.");
            }
            // Otherwise check if this is the player's connection or if the local player is involved
            else if (thisClientPlayer != null)
            {
                shouldProcess = (NetworkManager.ClientManager.Connection.ClientId == playerConnection.ClientId) ||
                               (thisClientPlayer.ObjectId == casterObjId || thisClientPlayer.ObjectId == targetObjId);
                
                Debug.Log($"Client player check: Connection match: {NetworkManager.ClientManager.Connection.ClientId == playerConnection.ClientId}, " +
                          $"Is caster: {thisClientPlayer.ObjectId == casterObjId}, Is target: {thisClientPlayer.ObjectId == targetObjId}");
            }
            
            if (shouldProcess)
            {
                Debug.Log($"Client will process card play notification - local player involved");
            }
        }
        
        if (!shouldProcess)
        {
            Debug.Log($"RpcNotifyCardPlayed skipped - not for this client. Client: {NetworkManager.ClientManager.Connection.ClientId}, Target: {(playerConnection != null ? playerConnection.ClientId.ToString() : "null")}");
            return;
        }
        
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
            
            // If we can't find the caster, try to use the local player as a fallback for client side visualization
            if (NetworkManager.IsClientOnlyStarted)
            {
                foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
                {
                    if (player.IsOwner)
                    {
                        caster = player;
                        Debug.Log($"Using local player as caster fallback: {player.PlayerName.Value}, ID: {player.ObjectId}");
                        break;
                    }
                }
            }
        }
        
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)targetObjId, out NetworkObject targetObj))
        {
            target = targetObj.GetComponent<NetworkBehaviour>();
            Debug.Log($"Successfully found target object with ID {targetObjId}");
        }
        else
        {
            Debug.LogError($"Failed to find target object with ID {targetObjId}");
        }

        if (caster != null && target != null)
        {
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
        else
        {
            Debug.LogError($"RpcNotifyCardPlayed: Missing caster or target references");
        }
    }

    [ObserversRpc]
    private void RpcNotifyCardPlayed(uint casterObjId, uint targetObjId, int cardId, string cardInstanceId, NetworkConnection playerConnection)
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
        
        Debug.Log($"RpcNotifyCardPlayed received - CasterID: {casterObjId}, TargetID: {targetObjId}, CardID: {cardId}, InstanceID: {cardInstanceId}, TargetClientId: {(playerConnection != null ? playerConnection.ClientId.ToString() : "-1")}");
        
        // Special handling for host - host needs to process all card plays
        bool shouldProcess = false;
        
        if (NetworkManager.IsHostStarted)
        {
            // Host should process all card plays
            Debug.Log($"Host detected in RpcNotifyCardPlayed. Processing card play notification.");
            shouldProcess = true;
        }
        else
        {
            // For non-host clients, check if they're involved in this fight
            // Get the local player and check if it's involved in this card play
            NetworkPlayer thisClientPlayer = null;
            foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
            {
                if (player.IsOwner)
                {
                    thisClientPlayer = player;
                    Debug.Log($"Found local client player: {player.PlayerName.Value}, ID: {player.ObjectId}");
                    break;
                }
            }
            
            // If we're running on a client, this must be for this client
            if (NetworkManager.IsClientOnlyStarted)
            {
                shouldProcess = true;
                Debug.Log($"Client-only detected in RpcNotifyCardPlayed. Processing card play notification for client.");
            }
            // Otherwise check if this is the player's connection or if the local player is involved
            else if (thisClientPlayer != null)
            {
                shouldProcess = (NetworkManager.ClientManager.Connection.ClientId == playerConnection.ClientId) ||
                               (thisClientPlayer.ObjectId == casterObjId || thisClientPlayer.ObjectId == targetObjId);
                
                Debug.Log($"Client player check: Connection match: {NetworkManager.ClientManager.Connection.ClientId == playerConnection.ClientId}, " +
                          $"Is caster: {thisClientPlayer.ObjectId == casterObjId}, Is target: {thisClientPlayer.ObjectId == targetObjId}");
            }
            
            if (shouldProcess)
            {
                Debug.Log($"Client will process card play notification - local player involved");
            }
        }
        
        if (!shouldProcess)
        {
            Debug.Log($"RpcNotifyCardPlayed skipped - not for this client. Client: {NetworkManager.ClientManager.Connection.ClientId}, Target: {(playerConnection != null ? playerConnection.ClientId.ToString() : "null")}");
            return;
        }
        
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
            
            // If we can't find the caster, try to use the local player as a fallback for client side visualization
            if (NetworkManager.IsClientOnlyStarted)
            {
                foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
                {
                    if (player.IsOwner)
                    {
                        caster = player;
                        Debug.Log($"Using local player as caster fallback: {player.PlayerName.Value}, ID: {player.ObjectId}");
                        break;
                    }
                }
            }
        }
        
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)targetObjId, out NetworkObject targetObj))
        {
            target = targetObj.GetComponent<NetworkBehaviour>();
            Debug.Log($"Successfully found target object with ID {targetObjId}");
        }
        else
        {
            Debug.LogError($"Failed to find target object with ID {targetObjId}");
        }

        if (caster != null && target != null)
        {
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
        else
        {
            Debug.LogError($"RpcNotifyCardPlayed: Missing caster or target references");
        }
    }

    [ObserversRpc]
    private void RpcNotifyFightEnded(uint playerObjId, uint petObjId, bool petWon, NetworkConnection playerConnection)
    {
        Debug.Log($"RpcNotifyFightEnded received - PlayerID: {playerObjId}, PetID: {petObjId}, PetWon: {petWon}, TargetClientId: {playerConnection.ClientId}");
        
        // Special handling for host - host needs to process all fight endings
        bool shouldProcess = false;
        
        if (NetworkManager.IsHostStarted)
        {
            // Host should process all fight endings
            Debug.Log($"Host detected in RpcNotifyFightEnded. Processing fight ended notification.");
            shouldProcess = true;
        }
        else
        {
            // For non-host clients, check if they're involved in this fight
            // Get the local player
            NetworkPlayer localPlayer = null;
            foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
            {
                if (player.IsOwner)
                {
                    localPlayer = player;
                    break;
                }
            }
            
            // Process if this is the player's connection or if the local player is involved
            shouldProcess = (NetworkManager.ClientManager.Connection.ClientId == playerConnection.ClientId) ||
                           (localPlayer != null && localPlayer.ObjectId == playerObjId);
            
            if (shouldProcess)
            {
                Debug.Log($"Client will process fight ended notification - local player involved");
            }
        }
        
        if (!shouldProcess)
        {
            Debug.Log($"RpcNotifyFightEnded skipped - not for this client");
            return;
        }
        
        // Rename to avoid variable naming conflicts
        NetworkPlayer foundPlayer = null;
        NetworkPet foundPet = null;
        
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)playerObjId, out NetworkObject playerObj))
        {
            foundPlayer = playerObj.GetComponent<NetworkPlayer>();
        }
        
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)petObjId, out NetworkObject petObj))
        {
            foundPet = petObj.GetComponent<NetworkPet>();
        }

        if (foundPlayer != null && foundPet != null)
        {
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
        else
        {
            Debug.LogError($"RpcNotifyFightEnded: Missing player or pet references");
        }
    }

    [ObserversRpc]
    private void RpcNotifyMessage(string message, NetworkConnection playerConnection)
    {
        Debug.Log($"RpcNotifyMessage received - Message: {message}, TargetClientId: {playerConnection.ClientId}");
        
        // Special handling for host - host needs to process all messages
        bool shouldProcess = false;
        
        if (NetworkManager.IsHostStarted)
        {
            // Host should process all messages
            Debug.Log($"Host detected in RpcNotifyMessage. Processing message.");
            shouldProcess = true;
        }
        else
        {
            // For non-host clients, check if this message is for them
            // Get the local player
            NetworkPlayer clientPlayer = null;
            foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
            {
                if (player.IsOwner)
                {
                    clientPlayer = player;
                    break;
                }
            }
            
            // Process if this is the player's connection or if local player matches
            shouldProcess = (NetworkManager.ClientManager.Connection.ClientId == playerConnection.ClientId) ||
                           (clientPlayer != null && clientPlayer.Owner != null && 
                            clientPlayer.Owner.ClientId == playerConnection.ClientId);
            
            if (shouldProcess)
            {
                Debug.Log($"Client will process notification message - local player involved");
            }
        }
        
        if (!shouldProcess)
        {
            Debug.Log($"RpcNotifyMessage skipped - not for this client");
            return;
        }
        
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
    [ObserversRpc]
    private void RpcNotifyHandDiscarded(uint entityObjId, NetworkConnection targetConnection = null)
    {
        Debug.Log($"RpcNotifyHandDiscarded received for entity ID: {entityObjId}, TargetConnection: {(targetConnection != null ? targetConnection.ClientId.ToString() : "broadcast")}");
        
        // Find the local player that belongs to this client
        NetworkPlayer localPlayer = null;
        foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
        {
            if (player.IsOwner)
            {
                localPlayer = player;
                Debug.Log($"RpcNotifyHandDiscarded found local owned player: {player.PlayerName.Value} (ID: {player.ObjectId})");
                break;
            }
        }
        
        // Get local player's pet (opponent)
        NetworkPet localPlayerPet = null;
        if (localPlayer != null && FightManager.Instance != null)
        {
            localPlayerPet = FightManager.Instance.GetOpponentForPlayer(localPlayer);
            if (localPlayerPet != null)
            {
                Debug.Log($"RpcNotifyHandDiscarded found local player's pet: {localPlayerPet.PetName.Value} (ID: {localPlayerPet.ObjectId})");
            }
        }
        
        // Default to not processing
        bool shouldProcess = false;
        
        // Check if we're the target connection (if specified)
        if (targetConnection != null && NetworkManager.ClientManager.Connection.ClientId == targetConnection.ClientId)
        {
            Debug.Log($"This client is the specific target of this notification. Will process.");
            shouldProcess = true;
        }
        // Check if host (host processes all hand discards)
        else if (NetworkManager.IsHostStarted)
        {
            Debug.Log($"Host detected - will process hand discard for entity {entityObjId}");
            shouldProcess = true;
        }
        // Non-host client strictly checks if this entity is part of their own fight
        else if (localPlayer != null)
        {
            // Only process if this is the local player
            if (localPlayer.ObjectId == entityObjId)
            {
                Debug.Log($"Will process hand discard - it's for the local player (ID: {localPlayer.ObjectId})");
                shouldProcess = true;
            }
            // Or if it's the local player's pet opponent
            else if (localPlayerPet != null && localPlayerPet.ObjectId == entityObjId)
            {
                Debug.Log($"Will process hand discard - it's for the local player's pet opponent (ID: {localPlayerPet.ObjectId})");
                shouldProcess = true;
            }
            else
            {
                Debug.Log($"SKIPPING hand discard - Entity ID {entityObjId} does NOT match local player (ID: {localPlayer.ObjectId}) or their pet (ID: {(localPlayerPet != null ? localPlayerPet.ObjectId : -1)})");
                shouldProcess = false;
            }
        }
        
        if (!shouldProcess)
        {
            Debug.Log($"FINAL DECISION: Skipping hand discard processing for entity {entityObjId} - not relevant to this client");
            return;
        }
        
        Debug.Log($"FINAL DECISION: Processing hand discard processing for entity {entityObjId} - it belongs to this client's fight");
        
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
        
        // First try to find by direct owner connection
        foreach (var player in allPlayers)
        {
            if (player.Owner != null && player.Owner.ClientId == clientId)
            {
                Debug.Log($"Found player by connection ClientId match: {player.PlayerName.Value} with ClientId {clientId}");
                return player;
            }
        }
        
        // Then try to find by RelationshipManager ClientId
        foreach (var player in allPlayers)
        {
            var relationshipManager = player.GetComponent<RelationshipManager>();
            if (relationshipManager != null && relationshipManager.OwnerClientId == clientId)
            {
                Debug.Log($"Found player by RelationshipManager ClientId: {player.PlayerName.Value} with ClientId {clientId}");
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
    /// Finds the local player for the client calling the method
    /// </summary>
    private NetworkPlayer FindLocalPlayer()
    {
        var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            // Check for ownership
            if (player.IsOwner)
            {
                Debug.Log($"Found local player {player.PlayerName.Value} based on IsOwner");
                return player;
            }
            
            // Also check RelationshipManager
            var rm = player.GetComponent<RelationshipManager>();
            if (rm != null && rm.IsOwnedByLocalPlayer)
            {
                Debug.Log($"Found local player {player.PlayerName.Value} based on RelationshipManager.IsOwnedByLocalPlayer");
                return player;
            }
        }
        
        // As a fallback, for host check for server ownership
        if (NetworkManager.IsHostStarted)
        {
            foreach (var player in allPlayers)
            {
                var rm = player.GetComponent<RelationshipManager>();
                if (rm != null && rm.IsOwnedByServer)
                {
                    Debug.Log($"Found host's player {player.PlayerName.Value} based on IsOwnedByServer");
                    return player;
                }
            }
        }
        
        Debug.LogWarning("Could not find local player");
        return null;
    }

    /// <summary>
    /// Called by the client to end their turn. This RPC sends the player's ObjectId to avoid
    /// connection ID issues on the server.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void CmdEndTurnForPlayer(int playerObjectId)
    {
        Debug.Log($"CmdEndTurnForPlayer received for PlayerObjectId: {playerObjectId}");
        
        // Find the player object directly by ID
        NetworkPlayer targetPlayer = null;
        if (NetworkManager.ServerManager.Objects.Spawned.TryGetValue(playerObjectId, out NetworkObject playerObj))
        {
            targetPlayer = playerObj.GetComponent<NetworkPlayer>();
            Debug.Log($"Found target player by ObjectId: {targetPlayer.PlayerName.Value} (ID: {playerObjectId})");
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
        
        // Log details about which player's turn we're ending to help with debugging
        Debug.Log($"Ending turn for player {fightPlayer.PlayerName.Value} (ID: {fightPlayer.ObjectId}, Owner ClientId: {(fightPlayer.Owner != null ? fightPlayer.Owner.ClientId.ToString() : "null")})");
        
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
            
            // Get player's owner connection (should be the same as targetConnection calculated earlier)
            NetworkConnection petTurnOwnerConn = fightPlayer.Owner;
            
            // Notify clients to clear player's hand display
            Debug.Log($"Sending player hand discard notification to connection {(petTurnOwnerConn != null ? petTurnOwnerConn.ClientId.ToString() : "null")}");
            RpcNotifyHandDiscarded((uint)fightPlayer.ObjectId, petTurnOwnerConn);
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
        
        // Log connection details to help with debugging
        Debug.Log($"CONNECTIONS: Player owner ClientId: {(turnEndOwnerConn != null ? turnEndOwnerConn.ClientId.ToString() : "null")}");
        
        // Ensure we have a valid connection to send to (fallback to the command sender if needed)
        NetworkConnection targetConn = turnEndOwnerConn ?? Owner;
        
        Debug.Log($"Sending RPC notifications specifically to connection with ClientId: {targetConn.ClientId}");

        // Notify clients about the turn state change
        RpcUpdateTurnState(fightState.playerObjId, fightState.petObjId, CombatTurn.PetTurn, targetConn);
        
        // Notify clients to disable end turn button
        RpcEnableEndTurnButton(false, targetConn);
        
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
} 