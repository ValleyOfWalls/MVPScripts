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
    // This list is server-authoritative and not directly synced. RPCs will inform clients of turn changes.
    private List<FightTurnState> fightTurnStates = new List<FightTurnState>();

    [SerializeField] private FightManager fightManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private DraftSetup draftSetup;
    
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
        if (draftSetup == null) draftSetup = FindFirstObjectByType<DraftSetup>();
        
        steamNetworkIntegration = SteamNetworkIntegration.Instance;

        if (fightManager == null) Debug.LogError("FightManager not found by CombatManager.");
        if (gameManager == null) Debug.LogError("GameManager not found by CombatManager.");
        if (steamNetworkIntegration == null) Debug.LogError("SteamNetworkIntegration not found by CombatManager.");
        if (draftSetup == null) Debug.LogError("DraftSetup not found by CombatManager.");
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

        // For all entities in all fights, replenish energy and draw cards
        foreach (var fightState in fightTurnStates)
        {
            // Get player and pet
            NetworkPlayer player = GetNetworkObjectComponent<NetworkPlayer>(fightState.playerObjId);
            NetworkPet pet = GetNetworkObjectComponent<NetworkPet>(fightState.petObjId);

            if (player != null && pet != null)
            {
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

                // Set turn to player
                var updatedFightState = fightState;
                updatedFightState.currentTurn = CombatTurn.PlayerTurn;
                int index = fightTurnStates.IndexOf(fightState);
                fightTurnStates[index] = updatedFightState;

                // Notify only the clients involved in this fight about whose turn it is
                TargetRpcUpdateTurnState(player.Owner, (uint)player.ObjectId, (uint)pet.ObjectId, CombatTurn.PlayerTurn);

                // If on a client who is the one controlling this player, enable End Turn button.
                TargetRpcEnableEndTurnButton(player.Owner, true);

                Debug.Log($"Player {player.PlayerName.Value}'s turn against {pet.PetName.Value} has started.");
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdEndPlayerTurn()
    {
        NetworkConnection conn = Owner;

        // Find the fight state for this connection
        FightTurnState? fightStateNullable = fightTurnStates.Find(state => state.playerConnection == conn);
        if (!fightStateNullable.HasValue)
        {
            Debug.LogError($"No fight state found for connection {conn.ClientId}");
            return;
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
        TargetRpcUpdateTurnState(conn, fightState.playerObjId, fightState.petObjId, CombatTurn.PetTurn);
        TargetRpcEnableEndTurnButton(conn, false);

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
        
        // Get the HandleCardPlay component for the pet
        HandleCardPlay petCardHandler = pet.GetComponent<HandleCardPlay>();
        if (petCardHandler == null)
        {
            Debug.LogError($"HandleCardPlay component not found on pet {pet.PetName.Value}");
            yield break;
        }
        
        // Simple AI: Play all cards in hand one by one if energy allows
        // Make a copy because playing a card modifies the hand
        List<int> cardsToPlay = new List<int>(pet.playerHandCardIds);

        foreach (int cardId in cardsToPlay)
        {
            if (pet.playerHandCardIds.Contains(cardId)) // Card might have been removed by another effect
            {
                CardData cardData = GetCardDataFromId(cardId); // This needs a Card Database
                if (cardData != null && pet.CurrentEnergy.Value >= cardData.EnergyCost)
                {
                    Debug.Log($"Pet {pet.PetName.Value} playing card ID: {cardId} (Cost: {cardData.EnergyCost}) on Player {player.PlayerName.Value}");
                    
                    // Use the HandleCardPlay component to play the card
                    petCardHandler.PlayCard(cardId);
                    
                    // Only notify the player involved in this fight
                    TargetRpcNotifyCardPlayed(player.Owner, (uint)pet.ObjectId, (uint)player.ObjectId, cardId);
                    yield return new WaitForSeconds(1f); // Simulate thinking/action time
                }
            }
        }

        Debug.Log($"Pet {pet.PetName.Value} ends its turn.");
        
        // Discard pet's hand using its HandManager
        HandManager petHandManager = pet.GetComponent<HandManager>();
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
            TargetRpcUpdateTurnState(player.Owner, fightState.playerObjId, fightState.petObjId, CombatTurn.PlayerTurn);
            TargetRpcEnableEndTurnButton(player.Owner, true);

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
        TargetRpcNotifyFightEnded(player.Owner, fightState.playerObjId, fightState.petObjId, player.CurrentHealth.Value <= 0);

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
        
        // Transition to draft phase
        if (draftSetup != null)
        {
            draftSetup.InitializeDraft();
        }
        else
        {
            Debug.LogError("Cannot transition to draft phase: DraftSetup reference is null");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdPlayerRequestsPlayCard(int cardId)
    {
        NetworkConnection conn = Owner;

        // Find the fight state for this connection
        FightTurnState? fightStateNullable = fightTurnStates.Find(state => state.playerConnection == conn);
        if (!fightStateNullable.HasValue)
        {
            Debug.LogError($"No fight state found for connection {conn.ClientId}");
            TargetRpcNotifyMessage(conn, "Error: No active fight found for you.");
            return;
        }

        FightTurnState fightState = fightStateNullable.Value;
        NetworkPlayer player = GetNetworkObjectComponent<NetworkPlayer>(fightState.playerObjId);
        if (player == null)
        {
            Debug.LogError($"Player not found for ObjectId: {fightState.playerObjId} in CmdPlayerRequestsPlayCard");
            TargetRpcNotifyMessage(conn, "Error: Player entity not found.");
            return;
        }

        // Check if it's the player's turn
        if (fightState.currentTurn != CombatTurn.PlayerTurn)
        {
            TargetRpcNotifyMessage(conn, "Not your turn!");
            Debug.LogWarning($"Player {player.PlayerName.Value} tried to play card, but it's not their turn (state: {fightState.currentTurn}).");
            return;
        }

        // Get the player's HandleCardPlay component
        HandleCardPlay playerCardHandler = player.GetComponent<HandleCardPlay>();
        if (playerCardHandler == null)
        {
            Debug.LogError($"HandleCardPlay component not found on player {player.PlayerName.Value}");
            TargetRpcNotifyMessage(conn, "Error: Card play handler not found.");
            return;
        }

        // Check if player has enough energy for the card
        CardData cardData = GetCardDataFromId(cardId);
        if (cardData != null && player.CurrentEnergy.Value < cardData.EnergyCost)
        {
            TargetRpcNotifyMessage(conn, $"Not enough energy! Need {cardData.EnergyCost} energy.");
            return;
        }

        // Use the HandleCardPlay component to play the card
        playerCardHandler.PlayCard(cardId);

        // Get the pet to notify the client about the card being played
        NetworkPet targetPet = GetNetworkObjectComponent<NetworkPet>(fightState.petObjId);
        if (targetPet != null)
        {
            // Only notify the client involved in this fight
            TargetRpcNotifyCardPlayed(player.Owner, (uint)player.ObjectId, (uint)targetPet.ObjectId, cardId);
        }

        // Check for game over after card play
        if (targetPet != null && (targetPet.CurrentHealth.Value <= 0 || player.CurrentHealth.Value <= 0))
        {
            HandleFightEnd(player, targetPet, fightState);
        }
    }

    // Helper method to get entity from ObjectId
    private T GetNetworkObjectComponent<T>(uint objectId) where T : NetworkBehaviour
    {
        // First check cache for faster lookups
        if (typeof(T) == typeof(NetworkPlayer))
        {
            if (playerCache.TryGetValue(objectId, out NetworkPlayer player))
                return player as T;
        }
        else if (typeof(T) == typeof(NetworkPet))
        {
            if (petCache.TryGetValue(objectId, out NetworkPet pet))
                return pet as T;
        }
        
        // If not in cache, look up in NetworkManager
        if (NetworkManager.ServerManager.Objects.Spawned.TryGetValue((int)objectId, out NetworkObject netObj) && netObj != null)
        {
            T component = netObj.GetComponent<T>();
            
            // Cache the result for future lookups
            if (component is NetworkPlayer playerComponent)
                playerCache[objectId] = playerComponent;
            else if (component is NetworkPet petComponent)
                petCache[objectId] = petComponent;
                
            return component;
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
    private void TargetRpcUpdateTurnState(NetworkConnection conn, uint playerObjId, uint petObjId, CombatTurn newTurnState)
    {
        NetworkPlayer player = null;
        NetworkPet pet = null;
        
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)playerObjId, out NetworkObject playerObj))
        {
            player = playerObj.GetComponent<NetworkPlayer>();
        }
        
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)petObjId, out NetworkObject petObj))
        {
            pet = petObj.GetComponent<NetworkPet>();
        }

        if (player != null && pet != null)
        {
            Debug.Log($"Turn update: {(newTurnState == CombatTurn.PlayerTurn ? player.PlayerName.Value : pet.PetName.Value)}'s turn");

            // Update UI accordingly
            if (combatCanvasManager != null)
            {
                combatCanvasManager.UpdateTurnIndicator(newTurnState == CombatTurn.PlayerTurn ? player.PlayerName.Value : pet.PetName.Value);
            }
        }
    }

    [TargetRpc]
    private void TargetRpcEnableEndTurnButton(NetworkConnection conn, bool enabled)
    {
        if (combatCanvasManager != null)
        {
            combatCanvasManager.SetEndTurnButtonInteractable(enabled);
        }
    }

    [TargetRpc]
    private void TargetRpcNotifyCardPlayed(NetworkConnection conn, uint casterObjId, uint targetObjId, int cardId)
    {
        NetworkBehaviour caster = null;
        NetworkBehaviour target = null;
        
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)casterObjId, out NetworkObject casterObj))
        {
            caster = casterObj.GetComponent<NetworkBehaviour>();
        }
        
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)targetObjId, out NetworkObject targetObj))
        {
            target = targetObj.GetComponent<NetworkBehaviour>();
        }

        if (caster != null && target != null)
        {
            string casterName = caster is NetworkPlayer player ? player.PlayerName.Value : ((NetworkPet)caster).PetName.Value;
            string targetName = target is NetworkPlayer targetPlayer ? targetPlayer.PlayerName.Value : ((NetworkPet)target).PetName.Value;

            Debug.Log($"Card played: {casterName} played card ID {cardId} on {targetName}");

            // Local client UI updates for card being played
            if (combatCanvasManager != null)
            {
                combatCanvasManager.ShowCardPlayedEffect(cardId, caster, target);
            }
        }
    }

    [TargetRpc]
    private void TargetRpcNotifyFightEnded(NetworkConnection conn, uint playerObjId, uint petObjId, bool petWon)
    {
        NetworkPlayer player = null;
        NetworkPet pet = null;
        
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)playerObjId, out NetworkObject playerObj))
        {
            player = playerObj.GetComponent<NetworkPlayer>();
        }
        
        if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue((int)petObjId, out NetworkObject petObj))
        {
            pet = petObj.GetComponent<NetworkPet>();
        }

        if (player != null && pet != null)
        {
            string winnerName = petWon ? pet.PetName.Value : player.PlayerName.Value;
            Debug.Log($"Fight ended between {player.PlayerName.Value} and {pet.PetName.Value}. Winner: {winnerName}");

            // Update local client UI
            if (combatCanvasManager != null)
            {
                combatCanvasManager.ShowFightEndedUI(player, pet, petWon);
            }
        }
    }

    [TargetRpc]
    private void TargetRpcNotifyMessage(NetworkConnection conn, string message)
    {
        Debug.Log($"Combat notification: {message}");
        if (combatCanvasManager != null)
        {
            combatCanvasManager.ShowNotificationMessage(message);
        }
    }

    #endregion

    // Helper method to check if it's a player's turn
    public bool IsPlayerTurn(NetworkPlayer player)
    {
        if (player == null) return false;
        
        // Get the player's fight state
        FightTurnState? fightState = fightTurnStates.Find(state => state.playerObjId == player.ObjectId);
        if (!fightState.HasValue) return false;
        
        // Check if it's the player's turn
        return fightState.Value.currentTurn == CombatTurn.PlayerTurn;
    }
} 