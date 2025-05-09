using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using FishNet.Managing; // For NetworkManager
using FishNet.Managing.Object;
// using FishNet.Connection.Server; // Incorrect, remove
// using FishNet.Connection.Client; // Incorrect, remove

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

    // Tracks whose turn it is for each fight. Key is Player ObjectId.
    // This needs to be more robust if fights are truly independent and can be in different turn states.
    // For simplicity, let's assume a global turn or manage turns per fight.
    // Let's manage current turn per player connection involved in a fight.
    // We need a way to map a connection to its current fight state.
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
    // Removed DraftSetup reference as GamePhaseManager will handle phase transitions
    
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
        if (fightManager == null) fightManager = FightManager.Instance;
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        // Find GamePhaseManager
        gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
        
        steamNetworkIntegration = SteamNetworkIntegration.Instance;

        if (fightManager == null) Debug.LogError("FightManager not found by CombatManager.");
        if (gameManager == null) Debug.LogError("GameManager not found by CombatManager.");
        if (steamNetworkIntegration == null) Debug.LogError("SteamNetworkIntegration not found by CombatManager.");
        if (gamePhaseManager == null) Debug.LogError("GamePhaseManager not found by CombatManager.");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // Client might need references for local predictions or UI updates, but server is authoritative.
        combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
        if (IsOwner) // Only if this client owns something or is the local player
        {
           // Client logic for receiving turn updates, etc.
        }
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
        // This method is called by CombatSetup after it completes initialization
        Debug.Log("CombatManager: Starting combat flow...");
        
        // Initialize turn states based on existing fight assignments
        InitializeFightTurnStates();

        // Log fight assignments for debugging
        var fightAssignments = fightManager.GetAllFightAssignments();
        Debug.Log($"Combat starting with {fightAssignments.Count} fight assignments. Current round: {currentRound.Value}");
        
        // Start the first round of combat
        StartNewRound();
        
        Debug.Log($"Combat initialization complete. Round {currentRound.Value} started.");
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
                Debug.Log($"Initialized fight state for player: {player.PlayerName.Value} (ID: {fightState.playerObjId}) vs pet: {pet.PetName.Value} (ID: {fightState.petObjId})");
            }
            else
            {
                Debug.LogError($"Failed to initialize fight state. Player or pet not found for IDs: {fightAssignment.PlayerObjectId}, {fightAssignment.PetObjectId}");
            }
        }
        
        // Track how many fights are active for completion detection
        activeFightCount = fightTurnStates.Count;
        Debug.Log($"Combat started with {activeFightCount} active fights");
    }

    [Server]
    public void StartNewRound()
    {
        currentRound.Value++;
        Debug.Log($"Starting round {currentRound.Value}");

        // Create a local copy of the fight states to avoid collection modification issues
        List<FightTurnState> fightStatesCopy = new List<FightTurnState>(fightTurnStates);
        Debug.Log($"Processing {fightStatesCopy.Count} fight states for round {currentRound.Value}");

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
                    // Log fight details for debugging
                    Debug.Log($"Processing fight: Player {player.PlayerName.Value} (ID: {fightState.playerObjId}, " +
                              $"Connection: {(player.Owner != null ? player.Owner.ClientId.ToString() : "null")}) " +
                              $"vs Pet {pet.PetName.Value} (ID: {fightState.petObjId})");
                    
                    // Replenish energy
                    player.CurrentEnergy.Value = gameManager.PlayerMaxEnergy.Value;
                    pet.CurrentEnergy.Value = gameManager.PetMaxEnergy.Value;

                    // Draw cards using their HandManager components
                    HandManager playerHandManager = player.GetComponent<HandManager>();
                    HandManager petHandManager = pet.GetComponent<HandManager>();

                    if (playerHandManager != null)
                    {
                        int playerDrawAmount = currentRound.Value == 1 ? gameManager.PlayerDrawAmount.Value : 1;
                        playerHandManager.DrawInitialCardsForEntity(playerDrawAmount);
                    }
                    else
                    {
                        Debug.LogError($"HandManager component not found on player {player.PlayerName.Value}");
                    }

                    if (petHandManager != null)
                    {
                        int petDrawAmount = currentRound.Value == 1 ? gameManager.PetDrawAmount.Value : 1;
                        petHandManager.DrawInitialCardsForEntity(petDrawAmount);
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
                                Debug.Log($"Sending turn state update to player {player.PlayerName.Value} (Connection: {player.Owner.ClientId})...");
                                
                                // Send ObserversRpcs instead of TargetRpcs
                                RpcUpdateTurnState(fightState.playerObjId, fightState.petObjId, CombatTurn.PlayerTurn, player.Owner);
                                RpcEnableEndTurnButton(true, player.Owner);
                                
                                Debug.Log($"Turn state update sent to player {player.PlayerName.Value}");
                            }
                            catch (System.Exception e)
                            {
                                Debug.LogWarning($"Failed to send turn state to player {player.PlayerName.Value}: {e.Message}");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"Player {player.PlayerName.Value} has no Owner connection");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Could not find fight state for player {player.PlayerName.Value} vs pet {pet.PetName.Value}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error in StartNewRound for player {player.PlayerName.Value} vs pet {pet.PetName.Value}: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"Player or pet reference is null in fight state. Player ID: {fightState.playerObjId}, Pet ID: {fightState.petObjId}");
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdEndPlayerTurn()
    {
        NetworkConnection conn = Owner;

        // Find the fight state for this connection
        FightTurnState? fightStateNullable = fightTurnStates.Find(state => 
            state.playerConnection != null && state.playerConnection.ClientId == conn.ClientId);
            
        if (!fightStateNullable.HasValue)
        {
            Debug.LogError($"No fight state found for connection {conn.ClientId}");
            
            // Special handling for host - try looking by player object instead
            if (NetworkManager.IsHostStarted)
            {
                Debug.Log("Host detected. Trying to find fight state by looking at all players...");
                
                // For host, try to find the fight by looking at all players
                foreach (var state in fightTurnStates)
                {
                    NetworkPlayer playerInState = GetNetworkObjectComponent<NetworkPlayer>(state.playerObjId);
                    if (playerInState != null && playerInState.Owner != null && playerInState.Owner.ClientId == conn.ClientId)
                    {
                        Debug.Log($"Found host fight state for player {playerInState.PlayerName.Value} with ObjectID {state.playerObjId}");
                        fightStateNullable = state;
                        break;
                    }
                }
                
                // If still not found, try a more brutal approach - try to match any player
                if (!fightStateNullable.HasValue && fightTurnStates.Count > 0)
                {
                    Debug.LogWarning("Host fight not found by ClientId. Using first available fight as fallback.");
                    fightStateNullable = fightTurnStates[0];
                }
            }
            
            // If still no fight state, give up
            if (!fightStateNullable.HasValue)
            {
                RpcNotifyMessage("Error: No active fight found for you.", conn);
                return;
            }
        }

        FightTurnState fightState = fightStateNullable.Value;
        if (fightState.currentTurn != CombatTurn.PlayerTurn)
        {
            Debug.LogWarning($"Player tried to end turn, but it's not their turn (state: {fightState.currentTurn})");
            return;
        }

        NetworkPlayer player = GetNetworkObjectComponent<NetworkPlayer>(fightState.playerObjId);
        if (player == null)
        {
            Debug.LogError($"Player not found for ObjectId: {fightState.playerObjId}");
            return;
        }

        // Discard player's hand using their HandManager component
        HandManager playerHandManager = player.GetComponent<HandManager>();
        if (playerHandManager != null)
        {
            playerHandManager.DiscardHand();
        }
        else
        {
            Debug.LogError($"HandManager component not found on player {player.PlayerName.Value}");
        }

        // Update turn state
        var updatedFightState = fightState;
        updatedFightState.currentTurn = CombatTurn.PetTurn;
        int index = fightTurnStates.IndexOf(fightState);
        fightTurnStates[index] = updatedFightState;

        // Notify only the clients involved in this fight
        RpcUpdateTurnState(fightState.playerObjId, fightState.petObjId, CombatTurn.PetTurn, conn);
        RpcEnableEndTurnButton(false, conn);

        // Start pet turn process
        NetworkPet pet = GetNetworkObjectComponent<NetworkPet>(fightState.petObjId);
        if (pet != null)
        {
            StartCoroutine(ProcessPetTurn(pet, player, updatedFightState));
        }
        else
        {
            Debug.LogError($"Pet not found for ObjectId: {fightState.petObjId}");
        }
    }

    [Server]
    private IEnumerator ProcessPetTurn(NetworkPet pet, NetworkPlayer player, FightTurnState fightState)
    {
        Debug.Log($"Pet {pet.PetName.Value}'s turn against {player.PlayerName.Value}.");
        
        // Simple AI: Play all cards in hand one by one if energy allows
        // Make a copy because playing a card modifies the hand
        List<int> cardsToPlay = new List<int>(pet.playerHandCardIds);
        
        // Get the HandManager to handle moving cards to discard pile
        HandManager petHandManager = pet.GetComponent<HandManager>();
        if (petHandManager == null)
        {
            Debug.LogError($"HandManager component not found on pet {pet.PetName.Value}");
            yield break;
        }
        
        // Get the player's effect manager for applying card effects
        EffectManager playerEffectManager = player.GetComponent<EffectManager>();
        if (playerEffectManager == null)
        {
            Debug.LogError($"EffectManager component not found on player {player.PlayerName.Value}");
            yield break;
        }

        foreach (int cardId in cardsToPlay)
        {
            if (pet.playerHandCardIds.Contains(cardId)) // Card might have been removed by another effect
            {
                CardData cardData = GetCardDataFromId(cardId); // This needs a Card Database
                if (cardData != null && pet.CurrentEnergy.Value >= cardData.EnergyCost)
                {
                    Debug.Log($"Pet {pet.PetName.Value} playing card ID: {cardId} (Cost: {cardData.EnergyCost}) on Player {player.PlayerName.Value}");
                    
                    // Deduct energy cost
                    pet.ChangeEnergy(-cardData.EnergyCost);
                    
                    // Apply the effect to the target
                    playerEffectManager.ApplyEffect(pet, cardData);
                    
                    // Move the card from hand to discard
                    petHandManager.MoveCardToDiscard(cardId);
                    
                    // Only notify the player involved in this fight
                    RpcNotifyCardPlayed((uint)pet.ObjectId, (uint)player.ObjectId, cardId, player.Owner);
                    yield return new WaitForSeconds(1f); // Simulate thinking/action time
                }
            }
        }

        Debug.Log($"Pet {pet.PetName.Value} ends its turn.");
        
        // Discard pet's hand using its HandManager
        if (petHandManager != null)
        {
            petHandManager.DiscardHand();
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
            // Update turn state - back to player's turn
            var updatedFightState = fightState;
            updatedFightState.currentTurn = CombatTurn.PlayerTurn;
            int index = fightTurnStates.IndexOf(fightState);
            fightTurnStates[index] = updatedFightState;

            // Notify clients
            RpcUpdateTurnState(fightState.playerObjId, fightState.petObjId, CombatTurn.PlayerTurn, player.Owner);
            RpcEnableEndTurnButton(true, player.Owner);

            Debug.Log($"Player {player.PlayerName.Value}'s turn has started again.");
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

        // Notify only the clients involved in this fight
        RpcNotifyFightEnded(fightState.playerObjId, fightState.petObjId, player.CurrentHealth.Value <= 0, player.Owner);

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
        // First log which CombatManager instance is receiving this call
        Debug.Log($"SERVER COMBAT MANAGER - CmdPlayerRequestsPlayCard received on Combat Manager with ID: {ObjectId}, " +
                  $"IsServer: {IsServerInitialized}, IsHost: {NetworkManager.IsHostStarted}, IsOwner: {IsOwner}, " + 
                  $"Owner: {(Owner != null ? Owner.ClientId.ToString() : "null")}");
        
        NetworkConnection conn = Owner;
        
        // Extensive debugging to figure out what's happening
        Debug.Log($"CmdPlayerRequestsPlayCard received for playerObjectId: {playerIdArg}, cardId: {cardId}, instanceId: {cardInstanceId}. " + 
                  $"Connection ClientId: {(conn != null ? conn.ClientId.ToString() : "null")}, " +
                  $"This object's Owner: {(Owner != null ? Owner.ClientId.ToString() : "null")}, " +
                  $"This object's ID: {ObjectId}");
        
        // Debug all network objects for the server
        Debug.Log("SERVER - All spawned network objects:");
        foreach (var kvp in NetworkManager.ServerManager.Objects.Spawned)
        {
            string typeName = kvp.Value.GetComponentInChildren<NetworkBehaviour>()?.GetType().Name ?? "Unknown";
            string objName = kvp.Value.name;
            Debug.Log($"  - Object ID: {kvp.Key}, Type: {typeName}, Name: {objName}");
        }
                
        // Get the actual player who is calling this command based on the playerObjectId
        NetworkPlayer requestingPlayer = null;
        
        // Get the player by object ID
        if (NetworkManager.ServerManager.Objects.Spawned.TryGetValue(playerIdArg, out NetworkObject playerObj))
        {
            requestingPlayer = playerObj.GetComponent<NetworkPlayer>();
            Debug.Log($"Found requesting player by object ID: {requestingPlayer.PlayerName.Value}, ID: {requestingPlayer.ObjectId}");
        }
        
        // Fallback to finding through connection if needed
        if (requestingPlayer == null && conn != null)
        {
            var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                if (player.Owner != null && player.Owner.ClientId == conn.ClientId)
                {
                    requestingPlayer = player;
                    Debug.Log($"Found requesting player through connection: {player.PlayerName.Value}, ID: {player.ObjectId}");
                    break;
                }
            }
        }
        
        // Last resort fallback
        if (requestingPlayer == null)
        {
            Debug.LogWarning($"Player not found through connection in CmdPlayerRequestsPlayCard. Using fallback method.");
            
            // Replace with a safer approach to get player
            // Try to identify the requesting player via other means
            var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                if (player.IsOwner)
                {
                    requestingPlayer = player;
                    Debug.Log($"Using fallback: Found requesting player {player.PlayerName.Value} with ObjectId {player.ObjectId}");
                    break;
                }
            }
            
            if (requestingPlayer == null && allPlayers.Length > 0)
            {
                Debug.LogWarning("Could not determine requesting player. Using first available player as fallback.");
                requestingPlayer = allPlayers[0];
            }
            
            if (requestingPlayer == null)
            {
                Debug.LogError("Could not determine player for card play request.");
                return;
            }
        }

        // Debug all fight states
        Debug.Log($"SERVER - All fight states:");
        foreach (var fs in fightTurnStates)
        {
            Debug.Log($"  - Fight state: PlayerID: {fs.playerObjId}, PetID: {fs.petObjId}, " +
                     $"Connection: {(fs.playerConnection != null ? fs.playerConnection.ClientId.ToString() : "null")}, " +
                     $"Turn: {fs.currentTurn}");
        }

        // Find the fight state for this player
        FightTurnState? fightStateNullable = fightTurnStates.Find(state => state.playerObjId == requestingPlayer.ObjectId);
        
        // Debug all fight states to help identify issues
        Debug.Log($"Looking for fight state with player ID {requestingPlayer.ObjectId} (Name: {requestingPlayer.PlayerName.Value})");
        
        if (!fightStateNullable.HasValue)
        {
            // This is a critical error - we couldn't find a fight state for this player
            Debug.LogError($"No fight state found for player {requestingPlayer.PlayerName.Value} (ID: {requestingPlayer.ObjectId})");
            
            // Try to get any available fight state as a fallback
            if (fightTurnStates.Count > 0)
            {
                Debug.LogWarning($"Using first available fight state as fallback");
                fightStateNullable = fightTurnStates[0];
            }
            else
            {
                Debug.LogError($"No fight states available. Cannot process card play request.");
                return;
            }
        }
        
        FightTurnState fightState = fightStateNullable.Value;
        
        // Double-check that the fight state matches the requesting player
        if (fightState.playerObjId != requestingPlayer.ObjectId)
        {
            Debug.LogWarning($"Fight state player ID ({fightState.playerObjId}) does not match requesting player ID ({requestingPlayer.ObjectId})");
            
            // Update the player reference to match the fight state
            NetworkPlayer playerFromFightState = GetNetworkObjectComponent<NetworkPlayer>(fightState.playerObjId);
            if (playerFromFightState != null)
            {
                Debug.Log($"Using player from fight state: {playerFromFightState.PlayerName.Value} with ID {playerFromFightState.ObjectId}");
                
                // CRITICAL: Make a local copy of the fight state and update it to use requesting player's ID
                fightState = new FightTurnState
                {
                    playerObjId = (uint)requestingPlayer.ObjectId,
                    petObjId = fightState.petObjId,
                    currentTurn = fightState.currentTurn,
                    playerConnection = requestingPlayer.Owner
                };
                
                Debug.Log($"CRITICAL: Modified fight state to use requesting player's ID: {requestingPlayer.ObjectId}");
            }
        }

        // Log all available players for debugging
        Debug.Log($"Available players in CmdPlayerRequestsPlayCard:");
        var allPlayersDebug = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        foreach (var p in allPlayersDebug)
        {
            Debug.Log($"  - Player {p.PlayerName.Value}, ID: {p.ObjectId}, Owner: {(p.Owner != null ? p.Owner.ClientId.ToString() : "null")}");
        }

        // Check if it's the player's turn
        if (fightState.currentTurn != CombatTurn.PlayerTurn)
        {
            RpcNotifyMessage("Not your turn!", conn);
            Debug.LogWarning($"Player {requestingPlayer.PlayerName.Value} tried to play card, but it's not their turn (state: {fightState.currentTurn}).");
            return;
        }

        // Check if player has enough energy for the card
        CardData cardData = GetCardDataFromId(cardId);
        if (cardData != null && requestingPlayer.CurrentEnergy.Value < cardData.EnergyCost)
        {
            RpcNotifyMessage($"Not enough energy! Need {cardData.EnergyCost} energy.", conn);
            return;
        }

        // Check if the card is in the player's hand
        CombatHand playerHand = requestingPlayer.GetComponent<CombatHand>();
        if (playerHand == null || !playerHand.HasCard(cardId))
        {
            RpcNotifyMessage("Card not found in your hand.", conn);
            return;
        }

        // Get the HandManager to handle moving the card to discard pile
        HandManager handManager = requestingPlayer.GetComponent<HandManager>();
        if (handManager == null)
        {
            Debug.LogError($"HandManager component not found on player {requestingPlayer.PlayerName.Value}");
            RpcNotifyMessage("Error: Cannot play card - missing HandManager component.", conn);
            return;
        }

        // Get the target pet
        NetworkPet targetPet = GetNetworkObjectComponent<NetworkPet>(fightState.petObjId);
        if (targetPet == null)
        {
            Debug.LogError($"Target pet not found for ObjectId: {fightState.petObjId}");
            RpcNotifyMessage("Error: Target not found.", conn);
            return;
        }

        // Get the target's effect manager
        EffectManager targetEffectManager = targetPet.GetComponent<EffectManager>();
        if (targetEffectManager == null)
        {
            Debug.LogError($"EffectManager component not found on target pet {targetPet.PetName.Value}");
            RpcNotifyMessage("Error: Cannot apply card effect - missing EffectManager on target.", conn);
            return;
        }

        Debug.Log($"Player {requestingPlayer.PlayerName.Value} (ID: {requestingPlayer.ObjectId}) playing card ID: {cardId}, Instance: {cardInstanceId} against pet ID: {fightState.petObjId}");

        // Deduct energy cost
        requestingPlayer.ChangeEnergy(-cardData.EnergyCost);
        
        // Get the player's effect manager
        EffectManager playerEffectManager = requestingPlayer.GetComponent<EffectManager>();
        if (playerEffectManager == null)
        {
            Debug.LogError($"EffectManager component not found on player {requestingPlayer.PlayerName.Value}");
            RpcNotifyMessage("Error: Cannot apply card effect - missing EffectManager on player.", conn);
            return;
        }
        
        // Apply the effect from the player to the target pet
        playerEffectManager.ApplyEffect(targetPet, cardData);
        
        // Move the card from hand to discard
        handManager.MoveCardToDiscard(cardId);

        // Ensure we're using the correct player and pet IDs from the fight state
        uint playerObjId = (uint)requestingPlayer.ObjectId;
        uint petObjectId = fightState.petObjId;
        
        // Critical debug info showing exact IDs we're about to send
        Debug.Log($"CRITICAL - About to send RpcNotifyCardPlayed with CasterID: {playerObjId}, TargetID: {petObjectId}, CardID: {cardId}, InstanceID: {cardInstanceId}");
        
        // IMPORTANT: Create a direct reference to the caster and target for verification
        NetworkPlayer directPlayerRef = requestingPlayer;
        NetworkPet directPetRef = targetPet;
        
        Debug.Log($"VERIFICATION - Direct references before RPC: " +
                  $"Player {directPlayerRef.PlayerName.Value} (ID: {directPlayerRef.ObjectId}) -> " +
                  $"Pet {directPetRef.PetName.Value} (ID: {directPetRef.ObjectId})");

        // Notify client that the card was played (this triggers the visual removal in CardSpawner)
        NetworkConnection playerConn = requestingPlayer.Owner;
        if (playerConn != null)
        {
            // Notify ALL clients about the card play, but use the verified player and pet IDs
            // We'll also pass the cardInstanceId now for precise card identification
            RpcNotifyCardPlayed(playerObjId, petObjectId, cardId, cardInstanceId, playerConn);
            Debug.Log($"Card played notification sent to client {playerConn.ClientId} (Player {requestingPlayer.PlayerName.Value}, ID: {requestingPlayer.ObjectId})");
        }
        else
        {
            Debug.LogError($"Could not notify card played - player {requestingPlayer.PlayerName.Value} has no owner connection");
            // Try to send notification anyway with verified IDs
            RpcNotifyCardPlayed(playerObjId, petObjectId, cardId, cardInstanceId, conn);
        }

        // Check for game over after card play
        if (targetPet != null && (targetPet.CurrentHealth.Value <= 0 || requestingPlayer.CurrentHealth.Value <= 0))
        {
            HandleFightEnd(requestingPlayer, targetPet, fightState);
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
                 $"Turn: {newTurnState}, TargetClientId: {playerConnection.ClientId}, " +
                 $"LocalClientId: {NetworkManager.ClientManager.Connection.ClientId}");
                 
        // Special handling for host - host needs to accept all turn updates
        bool shouldProcess = false;
        
        if (NetworkManager.IsHostStarted)
        {
            // Host should process all turn updates
            Debug.Log($"Host detected in RpcUpdateTurnState. Processing all turn updates.");
            shouldProcess = true;
        }
        else
        {
            // For non-host clients, check if they're involved in this fight
            // Get the local player and check if it's involved in this fight
            NetworkPlayer localPlayer = null;
            foreach (var player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
            {
                if (player.IsOwner)
                {
                    localPlayer = player;
                    break;
                }
            }
            
            // Process if this is the player's connection or if the local player is involved in this fight
            shouldProcess = (NetworkManager.ClientManager.Connection.ClientId == playerConnection.ClientId) ||
                           (localPlayer != null && localPlayer.ObjectId == playerObjId);
            
            if (shouldProcess)
            {
                Debug.Log($"Client will process turn update - local player involved");
            }
        }
        
        if (!shouldProcess)
        {
            Debug.Log($"RpcUpdateTurnState skipped - not for this client ({NetworkManager.ClientManager.Connection.ClientId})");
            return;
        }
        
        // Rename to avoid conflicts
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
        Debug.Log($"RpcEnableEndTurnButton received - Enabled: {enabled}, TargetClientId: {playerConnection.ClientId}");
        
        // Special handling for host - host needs to process all updates
        bool shouldProcess = false;
        
        if (NetworkManager.IsHostStarted)
        {
            // Host should process all updates
            Debug.Log($"Host detected in RpcEnableEndTurnButton. Processing update.");
            shouldProcess = true;
        }
        else
        {
            // For non-host clients, check if they're involved in this update
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
            
            // Process if this is the player's connection or if the local player is involved
            shouldProcess = (NetworkManager.ClientManager.Connection.ClientId == playerConnection.ClientId) ||
                           (clientPlayer != null && clientPlayer.Owner != null && 
                            clientPlayer.Owner.ClientId == playerConnection.ClientId);
            
            if (shouldProcess)
            {
                Debug.Log($"Client will process button update - local player involved");
            }
        }
        
        if (!shouldProcess)
        {
            Debug.Log($"RpcEnableEndTurnButton skipped - not for this client");
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
} 