using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Managing.Object;

/// <summary>
/// Controls the flow of the drafting phase
/// </summary>
public class DraftManager : NetworkBehaviour
{
    // Singleton instance
    public static DraftManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private CombatSetup combatSetup;

    // Player draft data
    private Dictionary<int, Queue<int>> playerDraftPackQueues = new Dictionary<int, Queue<int>>();
    private Dictionary<int, int> draftPackOwners = new Dictionary<int, int>();
    
    // Draft completion tracking
    private readonly SyncVar<int> playersCompletedDraft = new SyncVar<int>();
    private int totalPlayers = 0;
    
    // Client-side references
    private NetworkPlayer localPlayer;
    private DraftCanvasManager draftCanvas;

    #region Lifecycle Methods
    
    private void Awake()
    {
        InitializeSingleton();
    }
    
    private void InitializeSingleton()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (IsServerInitialized)
        {
            InitializeDraftPacks();
        }
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        FindLocalObjects();
    }
    
    private void FindLocalObjects()
    {
        if (localPlayer == null)
        {
            localPlayer = FindLocalPlayer();
        }
        
        if (draftCanvas == null)
        {
            draftCanvas = FindFirstObjectByType<DraftCanvasManager>();
        }
    }
    
    private NetworkPlayer FindLocalPlayer()
    {
        NetworkPlayer[] players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        foreach (NetworkPlayer player in players)
        {
            if (player.IsOwner)
            {
                return player;
            }
        }
        return null;
    }
    
    private void InitializeDraftPacks()
    {
        DraftPack[] packs = Object.FindObjectsByType<DraftPack>(FindObjectsSortMode.None);
        foreach (DraftPack pack in packs)
        {
            // Initialize pack logic
        }
    }
    
    #endregion

    #region Server Methods
    
    /// <summary>
    /// Assigns an initial draft pack to a player
    /// Called by DraftPackSetup when packs are created
    /// </summary>
    [Server]
    public void AssignDraftPack(int playerObjectId, int draftPackObjectId)
    {
        if (!IsServerInitialized) return;
        
        // Add pack to player's queue
        if (!playerDraftPackQueues.TryGetValue(playerObjectId, out Queue<int> packsQueue))
        {
            packsQueue = new Queue<int>();
            playerDraftPackQueues[playerObjectId] = packsQueue;
        }
        
        packsQueue.Enqueue(draftPackObjectId);
        
        // Register the pack's owner
        draftPackOwners[draftPackObjectId] = playerObjectId;
        
        // Notify the player about their new pack
        NotifyPlayerOfNewPack(playerObjectId, draftPackObjectId);
    }
    
    private void NotifyPlayerOfNewPack(int playerObjectId, int draftPackObjectId)
    {
        if (!NetworkManager.ServerManager.Objects.Spawned.TryGetValue(playerObjectId, out NetworkObject playerObj))
            return;
            
        NetworkPlayer player = playerObj.GetComponent<NetworkPlayer>();
        if (player != null)
        {
            TargetReceiveNewDraftPack(player.Owner, draftPackObjectId);
        }
    }

    /// <summary>
    /// Handles a player selecting a card from a draft pack
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void CmdSelectCardFromPack(int playerObjectId, int draftPackObjectId, int cardId)
    {
        if (!IsServerInitialized) return;
        
        // Validate player and pack
        if (!ValidateSelectionRequest(playerObjectId, draftPackObjectId))
            return;
            
        // Process card selection
        ProcessCardSelection(playerObjectId, draftPackObjectId, cardId);
    }
    
    private bool ValidateSelectionRequest(int playerObjectId, int draftPackObjectId)
    {
        if (!NetworkManager.ServerManager.Objects.Spawned.TryGetValue(playerObjectId, out NetworkObject _))
            return false;
            
        if (!NetworkManager.ServerManager.Objects.Spawned.TryGetValue(draftPackObjectId, out NetworkObject _))
            return false;
            
        // Verify the player owns this pack
        if (!draftPackOwners.TryGetValue(draftPackObjectId, out int ownerId) || ownerId != playerObjectId)
        {
            Debug.LogWarning($"Player {playerObjectId} tried to select a card from pack {draftPackObjectId} they don't own.");
            return false;
        }
        
        return true;
    }
    
    private void ProcessCardSelection(int playerObjectId, int draftPackObjectId, int cardId)
    {
        // Get the player
        NetworkObject playerObj = NetworkManager.ServerManager.Objects.Spawned[playerObjectId];
        NetworkPlayer player = playerObj.GetComponent<NetworkPlayer>();
        
        // Get the draft pack
        NetworkObject packObj = NetworkManager.ServerManager.Objects.Spawned[draftPackObjectId];
        DraftPack draftPack = packObj.GetComponent<DraftPack>();
        
        // Add the card to the player's deck
        DeckManager deckManager = player.GetComponent<DeckManager>();
        if (deckManager != null)
        {
            deckManager.AddCardToDeck(cardId);
        }
        
        // Remove the card from the pack
        draftPack.RemoveCard(cardId);
        
        // Handle pack after card selection
        HandlePackAfterSelection(playerObjectId, draftPackObjectId, draftPack);
    }
    
    private void HandlePackAfterSelection(int playerObjectId, int draftPackObjectId, DraftPack draftPack)
    {
        // Check if the pack is now empty
        if (draftPack.IsEmpty())
        {
            HandleEmptyPack(playerObjectId, draftPackObjectId);
        }
        else
        {
            // Pass the pack to the next player
            PassPackToNextPlayer(draftPackObjectId);
        }
    }
    
    private void HandleEmptyPack(int playerObjectId, int draftPackObjectId)
    {
        // Remove the pack from the player's queue
        if (playerDraftPackQueues.TryGetValue(playerObjectId, out Queue<int> packsQueue))
        {
            if (packsQueue.Count > 0 && packsQueue.Peek() == draftPackObjectId)
            {
                packsQueue.Dequeue();
            }
        }
        
        // Clean up pack tracking
        draftPackOwners.Remove(draftPackObjectId);
        
        // Give the player their next pack, if any
        GivePlayerNextPack(playerObjectId);
    }

    /// <summary>
    /// Passes a draft pack to the next player
    /// </summary>
    [Server]
    private void PassPackToNextPlayer(int draftPackObjectId)
    {
        if (!draftPackOwners.TryGetValue(draftPackObjectId, out int currentOwnerId))
        {
            Debug.LogWarning($"Cannot pass pack {draftPackObjectId} - no current owner found.");
            return;
        }
        
        // Get all players in a predictable order
        List<NetworkPlayer> orderedPlayers = GetOrderedPlayers();
        
        // Find the current owner's index
        int currentOwnerIndex = FindPlayerIndexById(orderedPlayers, currentOwnerId);
        if (currentOwnerIndex == -1)
        {
            Debug.LogWarning($"Cannot find player with ID {currentOwnerId} to pass pack {draftPackObjectId}.");
            return;
        }
        
        // Calculate the next player's index (circular)
        int nextPlayerIndex = (currentOwnerIndex + 1) % orderedPlayers.Count;
        NetworkPlayer nextPlayer = orderedPlayers[nextPlayerIndex];
        
        // Update the draft pack owner
        draftPackOwners[draftPackObjectId] = nextPlayer.ObjectId;
        
        // Add the pack to the next player's queue
        EnsurePlayerHasQueue(nextPlayer.ObjectId);
        playerDraftPackQueues[nextPlayer.ObjectId].Enqueue(draftPackObjectId);
        
        // Notify the next player about their new pack
        TargetReceiveNewDraftPack(nextPlayer.Owner, draftPackObjectId);
    }
    
    private List<NetworkPlayer> GetOrderedPlayers()
    {
        List<NetworkPlayer> players = new List<NetworkPlayer>(Object.FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None));
        players.Sort((a, b) => a.ObjectId.CompareTo(b.ObjectId));
        return players;
    }
    
    private int FindPlayerIndexById(List<NetworkPlayer> players, int playerId)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].ObjectId == playerId)
            {
                return i;
            }
        }
        return -1;
    }
    
    private void EnsurePlayerHasQueue(int playerObjectId)
    {
        if (!playerDraftPackQueues.ContainsKey(playerObjectId))
        {
            playerDraftPackQueues[playerObjectId] = new Queue<int>();
        }
    }

    /// <summary>
    /// Gives a player their next queued draft pack
    /// </summary>
    [Server]
    private void GivePlayerNextPack(int playerObjectId)
    {
        if (!playerDraftPackQueues.TryGetValue(playerObjectId, out Queue<int> packsQueue) || packsQueue.Count == 0)
        {
            // Player has no more packs - they've completed drafting
            MarkPlayerCompletedDraft(playerObjectId);
            return;
        }
        
        int nextPackId = packsQueue.Peek();
        
        // Notify the player about their next pack
        if (NetworkManager.ServerManager.Objects.Spawned.TryGetValue(playerObjectId, out NetworkObject playerObj))
        {
            NetworkPlayer player = playerObj.GetComponent<NetworkPlayer>();
            if (player != null)
            {
                TargetReceiveNewDraftPack(player.Owner, nextPackId);
            }
        }
    }

    /// <summary>
    /// Marks a player as having completed the draft
    /// </summary>
    [Server]
    private void MarkPlayerCompletedDraft(int playerObjectId)
    {
        playersCompletedDraft.Value++;
        
        // Check if all players have completed drafting
        if (playersCompletedDraft.Value >= totalPlayers)
        {
            // All players completed - proceed to combat
            RpcDraftCompleted();
        }
    }
    
    [TargetRpc]
    private void TargetReceiveNewDraftPack(FishNet.Connection.NetworkConnection conn, int draftPackObjectId)
    {
        // Find the local player and DraftCanvasManager
        FindLocalObjects();
        
        // Update the UI with the new pack
        if (draftCanvas != null)
        {
            // Get the draft pack from the network
            if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue(draftPackObjectId, out NetworkObject packObj))
            {
                DraftPack draftPack = packObj.GetComponent<DraftPack>();
                if (draftPack != null && draftPack.IsSpawned)
                {
                    // Assuming DraftPack has a method to show itself
                    draftPack.ShowPackForPlayer();
                }
            }
        }
    }
    
    [ObserversRpc]
    private void RpcDraftCompleted()
    {
        // Find the GamePhaseManager to transition to Combat phase
        GamePhaseManager phaseManager = FindFirstObjectByType<GamePhaseManager>();
        if (phaseManager != null)
        {
            phaseManager.SetCombatPhase();
        }
        
        // Trigger combat setup
        if (combatSetup == null)
        {
            combatSetup = FindFirstObjectByType<CombatSetup>();
        }
        
        if (combatSetup != null && IsServerStarted)
        {
            combatSetup.InitializeCombat();
        }
    }
    
    #endregion
} 