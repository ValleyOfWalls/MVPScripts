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

    // Dictionary mapping player ObjectIds to their draft packs queue
    private Dictionary<int, Queue<int>> playerDraftPackQueues = new Dictionary<int, Queue<int>>();
    
    // Dictionary mapping draft pack ObjectIds to their owners
    private Dictionary<int, int> draftPackOwners = new Dictionary<int, int>();
    
    // SyncVar to track number of players who have completed drafting
    private readonly SyncVar<int> playersCompletedDraft = new SyncVar<int>();
    
    // Total number of players in the draft
    private int totalPlayers = 0;

    private void Awake()
    {
        // Singleton setup
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
            // Find all draft packs using new API
            DraftPack[] packs = Object.FindObjectsByType<DraftPack>(FindObjectsSortMode.None);
            foreach (DraftPack pack in packs)
            {
                // Initialize pack
            }
        }
    }

    /// <summary>
    /// Assigns an initial draft pack to a player
    /// Called by DraftPackSetup when packs are created
    /// </summary>
    /// <param name="playerObjectId">The player's object ID</param>
    /// <param name="draftPackObjectId">The draft pack's object ID</param>
    [Server]
    public void AssignDraftPack(int playerObjectId, int draftPackObjectId)
    {
        if (!IsServerInitialized) return;
        
        // Add pack to player's queue
        if (playerDraftPackQueues.TryGetValue(playerObjectId, out Queue<int> packsQueue))
        {
            packsQueue.Enqueue(draftPackObjectId);
        }
        
        // Register the pack's owner
        draftPackOwners[draftPackObjectId] = playerObjectId;
        
        // Notify the player about their new pack
        NetworkObject playerObj = null;
        if (NetworkManager.ServerManager.Objects.Spawned.TryGetValue(playerObjectId, out playerObj))
        {
            NetworkPlayer player = playerObj.GetComponent<NetworkPlayer>();
            if (player != null)
            {
                TargetReceiveNewDraftPack(player.Owner, draftPackObjectId);
            }
        }
    }

    /// <summary>
    /// Handles a player selecting a card from a draft pack
    /// </summary>
    /// <param name="playerObjectId">The player's object ID</param>
    /// <param name="draftPackObjectId">The draft pack's object ID</param>
    /// <param name="cardId">The selected card's ID</param>
    [ServerRpc(RequireOwnership = false)]
    public void CmdSelectCardFromPack(int playerObjectId, int draftPackObjectId, int cardId)
    {
        if (!IsServerInitialized) return;
        
        // Get the player
        NetworkObject playerObj = null;
        if (NetworkManager.ServerManager.Objects.Spawned.TryGetValue(playerObjectId, out playerObj))
        {
            NetworkPlayer player = playerObj.GetComponent<NetworkPlayer>();
            if (player == null) return;
            
            // Get the draft pack
            NetworkObject packObj = null;
            if (NetworkManager.ServerManager.Objects.Spawned.TryGetValue(draftPackObjectId, out packObj))
            {
                DraftPack draftPack = packObj.GetComponent<DraftPack>();
                if (draftPack == null) return;
                
                // Verify the player owns this pack
                if (!draftPackOwners.TryGetValue(draftPackObjectId, out int ownerId) || ownerId != playerObjectId)
                {
                    Debug.LogWarning($"Player {playerObjectId} tried to select a card from pack {draftPackObjectId} they don't own.");
                    return;
                }
                
                // Add the card to the player's deck
                DeckManager deckManager = player.GetComponent<DeckManager>();
                if (deckManager != null)
                {
                    deckManager.AddCardToDeck(cardId);
                }
                
                // Remove the card from the pack
                draftPack.RemoveCard(cardId);
                
                // Check if the pack is now empty
                if (draftPack.IsEmpty())
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
                else
                {
                    // Pass the pack to the next player
                    PassPackToNextPlayer(draftPackObjectId);
                }
            }
        }
    }

    /// <summary>
    /// Passes a draft pack to the next player
    /// </summary>
    /// <param name="draftPackObjectId">The draft pack's object ID</param>
    [Server]
    private void PassPackToNextPlayer(int draftPackObjectId)
    {
        if (!draftPackOwners.TryGetValue(draftPackObjectId, out int currentOwnerId))
        {
            Debug.LogWarning($"Cannot pass pack {draftPackObjectId} - no current owner found.");
            return;
        }
        
        // Get all players in a predictable order
        List<NetworkPlayer> orderedPlayers = new List<NetworkPlayer>(Object.FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None));
        orderedPlayers.Sort((a, b) => a.ObjectId.CompareTo(b.ObjectId));
        
        // Find the current owner's index
        int currentOwnerIndex = -1;
        for (int i = 0; i < orderedPlayers.Count; i++)
        {
            if (orderedPlayers[i].ObjectId == currentOwnerId)
            {
                currentOwnerIndex = i;
                break;
            }
        }
        
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
        if (playerDraftPackQueues.TryGetValue(nextPlayer.ObjectId, out Queue<int> packsQueue))
        {
            packsQueue.Enqueue(draftPackObjectId);
        }
        
        // Notify the next player about their new pack
        TargetReceiveNewDraftPack(nextPlayer.Owner, draftPackObjectId);
    }

    /// <summary>
    /// Gives a player their next queued draft pack
    /// </summary>
    /// <param name="playerObjectId">The player's object ID</param>
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
        NetworkObject playerObj = NetworkManager.ServerManager.Objects.Spawned.TryGetValue(playerObjectId, out NetworkObject obj) ? obj : null;
        if (playerObj != null)
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
    /// <param name="playerObjectId">The player's object ID</param>
    [Server]
    private void MarkPlayerCompletedDraft(int playerObjectId)
    {
        playersCompletedDraft.Value++;
        
        // Check if all players have completed drafting
        if (playersCompletedDraft.Value >= totalPlayers)
        {
            // All players completed - proceed to combat
            RpcDraftCompleted();
            
            // Start the next combat phase
            if (combatSetup != null)
            {
                combatSetup.InitializeCombat();
            }
        }
    }

    /// <summary>
    /// Target RPC to tell a player they've received a new draft pack
    /// </summary>
    /// <param name="conn">The player's connection</param>
    /// <param name="draftPackObjectId">The draft pack's object ID</param>
    [TargetRpc]
    private void TargetReceiveNewDraftPack(FishNet.Connection.NetworkConnection conn, int draftPackObjectId)
    {
        // Get the draft pack
        NetworkObject packObj = NetworkManager.ClientManager.Objects.Spawned.TryGetValue(draftPackObjectId, out NetworkObject obj) ? obj : null;
        if (packObj == null) return;
        
        DraftPack draftPack = packObj.GetComponent<DraftPack>();
        if (draftPack == null) return;
        
        // Update pack UI
        draftPack.ShowPackForPlayer();
    }

    /// <summary>
    /// Notifies all clients that the draft phase has completed
    /// </summary>
    [ObserversRpc]
    private void RpcDraftCompleted()
    {
        // Find the draft canvas manager
        DraftCanvasManager draftCanvasManager = Object.FindFirstObjectByType<DraftCanvasManager>();
        if (draftCanvasManager != null)
        {
            // Enable the proceed button on the draft canvas
            draftCanvasManager.EnableProceedButton();
        }
    }

    private NetworkPlayer localPlayer;
    private DraftCanvasManager draftCanvas;

    private void FindLocalObjects()
    {
        // Update object finding methods
        NetworkPlayer[] players = Object.FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        foreach (NetworkPlayer player in players)
        {
            if (player.IsOwner)
            {
                localPlayer = player;
                break;
            }
        }

        if (localPlayer == null)
        {
            Debug.LogError("DraftManager: Could not find local player!");
            return;
        }

        // Update other FindObjectOfType calls similarly
        draftCanvas = Object.FindFirstObjectByType<DraftCanvasManager>();
        if (draftCanvas == null)
        {
            Debug.LogError("DraftManager: Could not find DraftCanvasManager!");
            return;
        }
    }
} 