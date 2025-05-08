using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// Manages the artifact shop in the draft phase
/// </summary>
public class ArtifactShopManager : NetworkBehaviour
{
    [Header("Shop Settings")]
    [SerializeField] private int numberOfArtifactsToShow = 3;
    [SerializeField] private Transform artifactShopContainer;
    [SerializeField] private GameObject artifactPrefab;

    [Header("References")]
    private ArtifactDatabase artifactDatabase;

    // Synced list of artifact IDs currently available in the shop
    private readonly SyncList<int> availableArtifactIds = new SyncList<int>();

    // Dictionary of spawned artifact objects, keyed by their IDs
    private Dictionary<int, GameObject> spawnedArtifacts = new Dictionary<int, GameObject>();

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Get artifact database
        artifactDatabase = ArtifactDatabase.Instance;
        
        if (artifactDatabase == null)
        {
            Debug.LogError("ArtifactShopManager: ArtifactDatabase instance not found!");
            return;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Register for shop inventory changes
        availableArtifactIds.OnChange += HandleShopInventoryChanged;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Unregister from shop inventory changes
        availableArtifactIds.OnChange -= HandleShopInventoryChanged;
    }

    /// <summary>
    /// Initializes the artifact shop with random artifacts
    /// Called from DraftSetup
    /// </summary>
    [Server]
    public void SetupShop()
    {
        if (!IsServerInitialized) return;
        
        // Clear current shop inventory
        availableArtifactIds.Clear();
        
        // Get random artifacts from the database
        List<int> randomArtifactIds = artifactDatabase.GetRandomArtifactIds(numberOfArtifactsToShow);
        
        // Add them to the available artifacts
        foreach (int artifactId in randomArtifactIds)
        {
            availableArtifactIds.Add(artifactId);
        }
    }

    /// <summary>
    /// Updates the shop display based on available artifacts
    /// </summary>
    private void UpdateShopDisplay()
    {
        if (artifactShopContainer == null) return;
        
        // Clear current display
        foreach (var spawnedArtifact in spawnedArtifacts.Values)
        {
            Destroy(spawnedArtifact);
        }
        spawnedArtifacts.Clear();
        
        // Create displays for each available artifact
        foreach (int artifactId in availableArtifactIds)
        {
            if (artifactDatabase == null)
            {
                artifactDatabase = ArtifactDatabase.Instance;
                if (artifactDatabase == null)
                {
                    Debug.LogError("ArtifactShopManager: ArtifactDatabase instance not found!");
                    return;
                }
            }
            
            ArtifactData data = artifactDatabase.GetArtifactById(artifactId);
            if (data == null) continue;
            
            // Instantiate artifact object
            GameObject artifactObj = Instantiate(artifactPrefab, artifactShopContainer);
            Artifact artifact = artifactObj.GetComponent<Artifact>();
            ArtifactSelectionManager selectionManager = artifactObj.GetComponent<ArtifactSelectionManager>();
            
            if (artifact != null)
            {
                artifact.Initialize(data);
                artifact.SetupForShop(selectionManager);
                
                if (selectionManager != null)
                {
                    selectionManager.SetShopManager(this);
                }
                
                spawnedArtifacts[artifactId] = artifactObj;
            }
        }
    }

    /// <summary>
    /// Called when a player attempts to purchase an artifact
    /// </summary>
    /// <param name="artifact">The artifact to purchase</param>
    public void AttemptPurchaseArtifact(Artifact artifact)
    {
        if (artifact == null || artifact.ArtifactData == null) return;
        
        // Find local player
        NetworkPlayer localPlayer = null;
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
            Debug.LogError("ArtifactShopManager: Could not find local player!");
            return;
        }
        
        // Check if player has enough currency
        if (localPlayer.Currency.Value < artifact.ArtifactData.Cost)
        {
            Debug.Log("Not enough currency to purchase artifact!");
            return;
        }
        
        // Request purchase from server
        CmdPurchaseArtifact(localPlayer.ObjectId, artifact.Id);
    }

    /// <summary>
    /// Server RPC to purchase an artifact
    /// </summary>
    /// <param name="playerObjectId">Object ID of the purchasing player</param>
    /// <param name="artifactId">ID of the artifact to purchase</param>
    [ServerRpc(RequireOwnership = false)]
    private void CmdPurchaseArtifact(int playerObjectId, int artifactId)
    {
        // Get player by object ID
        NetworkObject playerObj = null;
        if (NetworkManager.ServerManager.Objects.Spawned.TryGetValue(playerObjectId, out playerObj))
        {
            NetworkPlayer player = playerObj.GetComponent<NetworkPlayer>();
            if (player == null) return;
            
            // Get artifact data
            ArtifactData artifactData = artifactDatabase.GetArtifactById(artifactId);
            if (artifactData == null) return;
            
            // Check if artifact is available
            bool artifactAvailable = false;
            foreach (int id in availableArtifactIds)
            {
                if (id == artifactId)
                {
                    artifactAvailable = true;
                    break;
                }
            }
            
            if (!artifactAvailable)
            {
                Debug.LogWarning($"ArtifactShopManager: Artifact {artifactId} is not available for purchase.");
                return;
            }
            
            // Check if player has enough currency
            if (player.Currency.Value < artifactData.Cost)
            {
                Debug.LogWarning($"ArtifactShopManager: Player {player.PlayerName} does not have enough currency.");
                return;
            }
            
            // Deduct cost
            player.DeductCurrency(artifactData.Cost);
            
            // Add artifact to player's collection
            ArtifactManager artifactManager = player.GetComponent<ArtifactManager>();
            if (artifactManager != null)
            {
                artifactManager.AddArtifact(artifactId);
                
                // Remove artifact from shop
                for (int i = 0; i < availableArtifactIds.Count; i++)
                {
                    if (availableArtifactIds[i] == artifactId)
                    {
                        availableArtifactIds.RemoveAt(i);
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handles changes to the shop inventory
    /// </summary>
    private void HandleShopInventoryChanged(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
    {
        UpdateShopDisplay();
    }
} 