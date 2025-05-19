using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using System.Linq;

/// <summary>
/// Sets up draft packs for the draft phase
/// </summary>
public class DraftPackSetup : NetworkBehaviour
{
    [Header("Draft Pack Settings")]
    [SerializeField] private int cardsPerPack = 10;
    [SerializeField] private GameObject draftPackPrefab;
    [SerializeField] private Transform draftPackSpawnPoint;

    [Header("References")]
    private CardDatabase cardDatabase;

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Get card database
        cardDatabase = CardDatabase.Instance;
        
        if (cardDatabase == null)
        {
            Debug.LogError("DraftPackSetup: CardDatabase instance not found!");
            return;
        }
    }

    /// <summary>
    /// Creates and populates draft packs for all players
    /// Called from DraftSetup
    /// </summary>
    [Server]
    public void SetupDraftPacks()
    {
        if (!IsServerInitialized) return;
        
        // Get all network entities
        NetworkEntity[] players = Object.FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        
        // Create one pack per player
        for (int i = 0; i < players.Length; i++)
        {
            NetworkEntity player = players[i];
            
            // Spawn a draft pack network object
            GameObject packObj = Instantiate(draftPackPrefab, draftPackSpawnPoint);
            NetworkObject networkObj = packObj.GetComponent<NetworkObject>();
            
            if (networkObj != null)
            {
                // Spawn the network object
                FishNet.InstanceFinder.NetworkManager.ServerManager.Spawn(packObj);
                
                // Get the DraftPack component
                DraftPack draftPack = packObj.GetComponent<DraftPack>();
                if (draftPack != null)
                {
                    // Get random cards for this pack
                    List<int> cardIds = GetRandomDraftableCards(cardsPerPack);
                    
                    // Initialize the pack with these cards
                    draftPack.InitializePack(cardIds);
                    
                    // Assign the pack to the current player
                    DraftManager.Instance.AssignDraftPack(player.ObjectId, networkObj.ObjectId);
                }
            }
        }
    }

    /// <summary>
    /// Gets a list of random draftable card IDs
    /// </summary>
    /// <param name="count">Number of cards to get</param>
    /// <returns>List of random card IDs</returns>
    private List<int> GetRandomDraftableCards(int count)
    {
        // Get random cards and extract their IDs
        List<CardData> randomCards = cardDatabase.GetRandomCards(count);
        return randomCards.Select(card => card.CardId).ToList();
    }
} 