using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using FishNet.Managing; // For NetworkManager
// using FishNet.Connection.Server; // Incorrect, remove
// using FishNet.Connection.Client; // Incorrect, remove

public enum CombatTurn
{
    None,
    PlayerTurn,
    PetTurn
}

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

    private FightManager fightManager;
    public HandManager handManager; // Made public
    private EffectManager effectManager; // To be created
    private GameManager gameManager;
    private SteamNetworkIntegration steamNetworkIntegration; // Changed
    private CombatCanvasManager combatCanvasManager; // For local UI updates like end turn button

    // A simple queue for players whose turn it is, assuming not all fights start/end turns simultaneously.
    // This might be overly simplistic if fights are fully independent and asynchronous.
    // For now, let's process turns for each fight individually.

    private void Awake()
    { 
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        fightManager = FightManager.Instance;
        handManager = new HandManager(this); // Assuming HandManager is not a MonoBehaviour
        effectManager = new EffectManager(); // Assuming EffectManager is not a MonoBehaviour
        gameManager = FindFirstObjectByType<GameManager>();
        steamNetworkIntegration = SteamNetworkIntegration.Instance; // Changed

        if (fightManager == null) Debug.LogError("FightManager not found by CombatManager.");
        if (gameManager == null) Debug.LogError("GameManager not found by CombatManager.");
        if (steamNetworkIntegration == null) Debug.LogError("SteamNetworkIntegration not found by CombatManager."); // Changed
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
        if (fightManager == null || gameManager == null) 
        {
            Debug.LogError("CombatManager cannot start combat. Missing FightManager or GameManager.");
            return;
        }
        Debug.Log("CombatManager: Starting Combat Process...");
        currentRound.Value = 0;
        fightTurnStates.Clear();

        List<FightAssignmentData> assignments = fightManager.GetAllFightAssignments();
        if (assignments.Count == 0)
        {
            Debug.LogWarning("No fight assignments. Combat cannot start.");
            return;
        }

        foreach (var assignment in assignments)
        {
            NetworkPlayer player = GetNetworkObjectComponent<NetworkPlayer>(assignment.PlayerObjectId);
            NetworkPet pet = GetNetworkObjectComponent<NetworkPet>(assignment.PetObjectId);

            if (player != null && pet != null)
            {
                fightTurnStates.Add(new FightTurnState 
                { 
                    playerConnection = player.Owner,
                    currentTurn = CombatTurn.None, // Will be set at round start
                    playerObjId = (uint) player.ObjectId,
                    petObjId = (uint) pet.ObjectId
                });
            }
        }
        
        StartNewRound();
    }

    [Server]
    private void StartNewRound()
    {
        currentRound.Value++;
        Debug.Log($"Starting Round {currentRound.Value}");
        RpcNotifyRoundStart(currentRound.Value);

        // For each active fight
        for (int i = 0; i < fightTurnStates.Count; i++)
        {
            FightTurnState fightState = fightTurnStates[i];
            NetworkPlayer player = GetNetworkObjectComponent<NetworkPlayer>(fightState.playerObjId);
            NetworkPet pet = GetNetworkObjectComponent<NetworkPet>(fightState.petObjId);

            if (player == null || pet == null)
            {
                Debug.LogWarning($"Player or Pet missing for fight, skipping round start for this fight. PlayerID: {fightState.playerObjId}, PetID: {fightState.petObjId}");
                continue;
            }

            // Replenish energy
            player.ReplenishEnergy();
            pet.ReplenishEnergy();

            // Draw cards
            handManager.DrawInitialCardsForEntity(player, gameManager.PlayerDrawAmount.Value);
            handManager.DrawInitialCardsForEntity(pet, gameManager.PetDrawAmount.Value);
            
            // Set turn to PlayerTurn for this fight
            fightState.currentTurn = CombatTurn.PlayerTurn;
            fightTurnStates[i] = fightState; // Update struct in list

            Debug.Log($"Round {currentRound.Value}: Player {player.PlayerName.Value}'s turn.");
            TargetRpcNotifyTurnChanged(player.Owner, CombatTurn.PlayerTurn, (uint)player.ObjectId, (uint)pet.ObjectId);
            if(combatCanvasManager != null && player.IsOwner) combatCanvasManager.SetEndTurnButtonInteractable(true);
        }
    }

    [ServerRpc(RequireOwnership = true)] // Only owning client can end their turn
    public void CmdEndPlayerTurn(NetworkConnection conn)
    {
        Debug.Log($"CmdEndPlayerTurn received from client {conn.ClientId}");
        int fightIndex = fightTurnStates.FindIndex(f => f.playerConnection == conn && f.currentTurn == CombatTurn.PlayerTurn);
        if (fightIndex == -1) 
        {
            Debug.LogWarning($"Player {conn.ClientId} tried to end turn, but it's not their turn or fight not found.");
            return;
        }

        FightTurnState fightState = fightTurnStates[fightIndex];
        NetworkPlayer player = GetNetworkObjectComponent<NetworkPlayer>(fightState.playerObjId);
        NetworkPet pet = GetNetworkObjectComponent<NetworkPet>(fightState.petObjId);

        if (player == null || pet == null) 
        {
            Debug.LogError("Player or Pet became null during turn end.");
            return;
        }

        Debug.Log($"Player {player.PlayerName.Value} ends turn.");
        handManager.DiscardHand(player); // Player discards remaining cards

        // Transition to Pet's turn for this specific fight
        fightState.currentTurn = CombatTurn.PetTurn;
        fightTurnStates[fightIndex] = fightState;
        TargetRpcNotifyTurnChanged(player.Owner, CombatTurn.PetTurn, (uint)player.ObjectId, (uint)pet.ObjectId);
        if(combatCanvasManager != null && player.IsOwner) combatCanvasManager.SetEndTurnButtonInteractable(false);

        // Start Pet's turn logic (e.g., AI plays cards)
        StartCoroutine(ProcessPetTurn(pet, player, fightState)); 
    }

    [Server]
    private IEnumerator ProcessPetTurn(NetworkPet pet, NetworkPlayer player, FightTurnState fightState)
    {
        Debug.Log($"Pet {pet.PetName.Value}'s turn against {player.PlayerName.Value}.");
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
                    // Server directly applies card effect
                    pet.ChangeEnergy(-cardData.EnergyCost);
                    effectManager.ApplyEffect(pet, player, cardData); // Pet is caster, Player is target
                    handManager.MoveCardToDiscard(pet, cardId); 
                    
                    RpcNotifyCardPlayed((uint)pet.ObjectId, (uint)player.ObjectId, cardId);
                    yield return new WaitForSeconds(1f); // Simulate thinking/action time
                }
            }
        }

        Debug.Log($"Pet {pet.PetName.Value} ends its turn.");
        handManager.DiscardHand(pet); // Pet discards remaining cards

        // Check for game over conditions for this fight
        if (player.CurrentHealth.Value <= 0 || pet.CurrentHealth.Value <= 0)
        {
            HandleFightEnd(player, pet, fightState);
        }
        else
        {
            // If this fight is over, it shouldn't proceed to next round start for this fight
            // For now, let's assume all fights progress round by round together.
            // This logic needs adjustment if fights are fully independent in their round progression.
            // For now, once all pet turns are done for the round, a new round begins for all.
            int fightIndex = fightTurnStates.FindIndex(f => f.playerObjId == fightState.playerObjId);
            if(fightIndex != -1) {
                FightTurnState updatedFightState = fightTurnStates[fightIndex];
                updatedFightState.currentTurn = CombatTurn.None; // Mark turn as ended for this fight in this round
                fightTurnStates[fightIndex] = updatedFightState;
            }

            // Check if all fights in current round have completed their turns
            if (fightTurnStates.All(f => 
                f.currentTurn == CombatTurn.None || 
                GetNetworkObjectComponent<NetworkPlayer>(f.playerObjId)?.CurrentHealth.Value <= 0 || 
                GetNetworkObjectComponent<NetworkPet>(f.petObjId)?.CurrentHealth.Value <=0 ))
            {
                Debug.Log("All pet turns for the round are complete. Starting new round.");
                StartNewRound();
            }
        }
    }
    
    [Server]
    private void HandleFightEnd(NetworkPlayer player, NetworkPet pet, FightTurnState fightState)
    {
        string winner = "";
        if (player.CurrentHealth.Value <= 0 && pet.CurrentHealth.Value <= 0) winner = "Draw!";
        else if (player.CurrentHealth.Value <= 0) winner = $"Pet {pet.PetName.Value} wins!";
        else if (pet.CurrentHealth.Value <= 0) winner = $"Player {player.PlayerName.Value} wins!";

        Debug.Log($"Fight between Player {player.PlayerName.Value} and Pet {pet.PetName.Value} ended. Result: {winner}");
        RpcNotifyFightEnd(player.Owner, (uint)player.ObjectId, (uint)pet.ObjectId, winner);

        // Remove from active fights or mark as completed
        // fightTurnStates.Remove(fightState); 
        // This might be complex if removing while iterating. Better to mark as inactive.
        int fightIndex = fightTurnStates.FindIndex(f => f.playerObjId == fightState.playerObjId);
        if(fightIndex != -1) {
            FightTurnState updatedFightState = fightTurnStates[fightIndex];
            updatedFightState.currentTurn = CombatTurn.None; // Effectively marks as ended for turn processing
            // Could add a bool IsFightOver to FightTurnState
            fightTurnStates[fightIndex] = updatedFightState;
        }
        // TODO: Check if all fights are over to end combat entirely.
    }

    [ServerRpc(RequireOwnership = true)]
    public void CmdPlayerRequestsPlayCard(NetworkConnection conn, int cardId)
    {
        // Find the fight this player is in based on their connection and if it's their turn.
        int fightIndex = fightTurnStates.FindIndex(f => f.playerConnection == conn && f.currentTurn == CombatTurn.PlayerTurn);
        if (fightIndex == -1)
        {
            NetworkPlayer tempPlayer = null;
            // Attempt to find the player associated with the connection for a better error message.
            // This loop is just for richer debugging; the core logic relies on fightTurnStates.
            foreach(var fs_debug in fightTurnStates)
            {
                if(fs_debug.playerConnection == conn) {
                    tempPlayer = GetNetworkObjectComponent<NetworkPlayer>(fs_debug.playerObjId);
                    break;
                }
            }
            string playerNameForError = tempPlayer != null ? tempPlayer.PlayerName.Value : "Player for connection " + conn.ClientId;
            Debug.LogWarning($"{playerNameForError} tried to play card, but their fight was not found in fightTurnStates or it's not their turn.");
            TargetRpcNotifyMessage(conn, "Cannot play card now (not your turn or fight not found).");
            return;
        }

        FightTurnState fightState = fightTurnStates[fightIndex];
        NetworkPlayer player = GetNetworkObjectComponent<NetworkPlayer>(fightState.playerObjId);

        if (player == null) 
        {
            Debug.LogError($"CmdPlayerRequestsPlayCard: Player NetworkObject could not be retrieved for an active fight state. ConnId: {conn.ClientId}, PlayerObjId: {fightState.playerObjId}. This should not happen if fightState is valid.");
            TargetRpcNotifyMessage(conn, "Error: Player data missing.");
            return;
        }
        
        // This check is somewhat redundant given the FindIndex query, but kept for explicit clarity.
        if (fightState.currentTurn != CombatTurn.PlayerTurn)
        {
            TargetRpcNotifyMessage(conn, "Not your turn!");
            Debug.LogWarning($"Player {player.PlayerName.Value} tried to play card, but it's not their turn (state: {fightState.currentTurn}).");
            return;
        }

        if (!player.playerHandCardIds.Contains(cardId))
        {
            TargetRpcNotifyMessage(conn, "Card not in hand!");
            Debug.LogWarning($"Player {player.PlayerName.Value} tried to play card ID {cardId}, but it's not in their hand.");
            return;
        }

        CardData cardData = GetCardDataFromId(cardId);
        if (cardData == null)
        {
            // GetCardDataFromId already logs an error.
            TargetRpcNotifyMessage(conn, "Error: Card data invalid.");
            return;
        }

        if (player.CurrentEnergy.Value < cardData.EnergyCost)
        {
            TargetRpcNotifyMessage(conn, "Not enough energy!");
            Debug.LogWarning($"Player {player.PlayerName.Value} tried to play card {cardData.CardName} (Cost: {cardData.EnergyCost}), but only has {player.CurrentEnergy.Value} energy.");
            return;
        }

        Debug.Log($"Player {player.PlayerName.Value} requests to play card: {cardData.CardName} (ID: {cardId})");

        player.ChangeEnergy(-cardData.EnergyCost);

        NetworkPet targetPet = GetNetworkObjectComponent<NetworkPet>(fightState.petObjId);
        if (targetPet == null)
        {
            Debug.LogError($"Target pet not found for player {player.PlayerName.Value}'s card play. PetObjId: {fightState.petObjId}");
            TargetRpcNotifyMessage(conn, "Error: Target pet not found.");
            return;
        }

        effectManager.ApplyEffect(player, targetPet, cardData);
        handManager.MoveCardToDiscard(player, cardId);

        RpcNotifyCardPlayed((uint)player.ObjectId, (uint)targetPet.ObjectId, cardId); // Cast ObjectIds

        if (targetPet.CurrentHealth.Value <= 0 || player.CurrentHealth.Value <= 0)
        {
            HandleFightEnd(player, targetPet, fightState);
        }
    }

    // Helper to get Card data from an ID - YOU NEED TO IMPLEMENT THIS
    // This might involve looking up a ScriptableObject or a prefab based on ID.
    private CardData GetCardDataFromId(int cardId)
    {
        // Or have a dedicated CardDatabase singleton.
        if (CardDatabase.Instance != null)
        { 
            CardData data = CardDatabase.Instance.GetCardById(cardId);
            if (data == null) {
                 Debug.LogError($"GetCardDataFromId: Card ID {cardId} not found in CardDatabase. Ensure CardDatabase is in scene and card ID is correct.");
            }
            return data;
        }
        Debug.LogError($"GetCardDataFromId: CardDatabase.Instance is null. Make sure CardDatabase is in the scene and initialized before combat.");
        return null; 
    }

    private T GetNetworkObjectComponent<T>(uint objectId) where T : NetworkBehaviour
    {
        if (objectId == 0U || FishNet.InstanceFinder.NetworkManager == null) 
            return null;

        NetworkManager fishNetManager = FishNet.InstanceFinder.NetworkManager;
        NetworkObject nob = null;

        if (fishNetManager.ServerManager != null && fishNetManager.ServerManager.Objects.Spawned.TryGetValue((int)objectId, out nob))
        {
            return nob.GetComponent<T>();
        }
        
        // Check on client side if not found on server (e.g. client calling this for UI)
        if (fishNetManager.ClientManager != null && fishNetManager.ClientManager.Objects.Spawned.TryGetValue((int)objectId, out nob))
        {
            return nob.GetComponent<T>();
        }

        // The error CS0165: Use of unassigned local variable 'nob' was because nob assignment was inside the if, 
        // and the final return nob?.GetComponent<T>(); could be hit if both failed.
        // The structure above fixes this by returning directly from inside the TryGetValue success blocks.
        // If neither finds it, nob remains null (or its initial value if you don't re-assign null in C#7+ out vars).

        Debug.LogWarning($"NetworkObject with ID {objectId} not found on server or client.");
        return null;
    }

    public bool IsPlayerTurn(NetworkPlayer player)
    {
        if (player == null) return false;
        int fightIndex = fightTurnStates.FindIndex(f => f.playerObjId == player.ObjectId);
        if (fightIndex != -1)
        {
            return fightTurnStates[fightIndex].currentTurn == CombatTurn.PlayerTurn;
        }
        return false;
    }

    // --- RPCs for client notification --- 
    [ObserversRpc]
    private void RpcNotifyRoundStart(int round)
    {
        Debug.Log($"Client notified: Round {round} starting.");
        // Client-side UI can update round display using the passed 'round' parameter
    }

    [TargetRpc]
    private void TargetRpcNotifyTurnChanged(NetworkConnection conn, CombatTurn turn, uint NPOwnerID, uint NPTargetID)
    {
        Debug.Log($"Client {conn.ClientId} notified: It is now {turn}. PlayerID {NPOwnerID}, PetID {NPTargetID}.");
        // Client-side UI can update based on whose turn it is (e.g., enable/disable End Turn button)
        if (combatCanvasManager == null) combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
        if (combatCanvasManager != null)
        {
            NetworkPlayer localPlayer = GetNetworkObjectComponent<NetworkPlayer>(NPOwnerID);
            if(localPlayer != null && localPlayer.IsOwner)
            {
                combatCanvasManager.SetEndTurnButtonInteractable(turn == CombatTurn.PlayerTurn);
            }
        }
    }

    [ObserversRpc]
    private void RpcNotifyCardPlayed(uint casterId, uint targetId, int cardId)
    {
        NetworkBehaviour caster = GetNetworkObjectComponent<NetworkBehaviour>(casterId);
        NetworkBehaviour target = GetNetworkObjectComponent<NetworkBehaviour>(targetId);
        CardData cardData = GetCardDataFromId(cardId); // Client also needs card data for visuals
        string cardName = cardData != null ? cardData.CardName : "Unknown Card";
        Debug.Log($"Client notified: Caster {casterId} played card {cardName} (ID: {cardId}) on Target {targetId}.");
        // Client-side: Play animations, show card effect visuals, update UI from SyncVar changes.
    }
    
    [TargetRpc]
    private void TargetRpcNotifyMessage(NetworkConnection conn, string message)
    {
        Debug.Log($"Message for client {conn.ClientId}: {message}");
        // TODO: Show this message on the player's UI
    }

    [TargetRpc]
    private void RpcNotifyFightEnd(NetworkConnection conn, uint playerObjId, uint petObjId, string result)
    {
        Debug.Log($"Client {conn.ClientId} notified: Fight Over! Player {playerObjId} vs Pet {petObjId}. Result: {result}");
        // TODO: Show fight result on UI, potentially offer return to lobby or next action.
    }
} 