using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Handles the creation and setup of draft packs for the drafting phase.
/// Attach to: A NetworkObject in the scene that coordinates draft pack creation.
/// </summary>
public class DraftPackSetup : NetworkBehaviour
{
    [Header("Draft Pack Configuration")]
    [SerializeField] private GameObject draftPackPrefab;
    [SerializeField] private Transform draftPackContainer;
    
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private DraftCanvasManager draftCanvasManager;
    
    private List<DraftPack> spawnedPacks = new List<DraftPack>();
    
    public List<DraftPack> SpawnedPacks => spawnedPacks;
    
    private void Awake()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (draftCanvasManager == null) draftCanvasManager = FindFirstObjectByType<DraftCanvasManager>();
        
        if (draftPackPrefab == null)
        {
            Debug.LogError("DraftPackSetup: Draft pack prefab is not assigned!");
        }
        else
        {
            // Validate the prefab has required components
            ValidateDraftPackPrefab();
        }
    }
    
    private void ValidateDraftPackPrefab()
    {
        if (draftPackPrefab.GetComponent<DraftPack>() == null)
            Debug.LogError("DraftPackSetup: Draft pack prefab is missing DraftPack component!");
        if (draftPackPrefab.GetComponent<NetworkObject>() == null)
            Debug.LogError("DraftPackSetup: Draft pack prefab is missing NetworkObject component!");
        if (draftPackPrefab.GetComponent<CardSpawner>() == null)
            Debug.LogError("DraftPackSetup: Draft pack prefab is missing CardSpawner component!");
    }
    
    /// <summary>
    /// Mirrors transform properties from DraftCanvasManager's DraftPackContainer to a DraftPack's CardContainer
    /// </summary>
    private void MirrorTransformProperties(Transform sourceContainer, Transform targetContainer)
    {
        if (sourceContainer == null || targetContainer == null)
        {
            Debug.LogWarning("DraftPackSetup: Cannot mirror transform properties - source or target is null");
            return;
        }
        
        // Mirror basic transform properties
        targetContainer.localPosition = sourceContainer.localPosition;
        targetContainer.localRotation = sourceContainer.localRotation;
        targetContainer.localScale = sourceContainer.localScale;
        
        // Mirror RectTransform properties if both are RectTransforms
        RectTransform sourceRect = sourceContainer as RectTransform;
        RectTransform targetRect = targetContainer as RectTransform;
        
        if (sourceRect != null && targetRect != null)
        {
            targetRect.anchorMin = sourceRect.anchorMin;
            targetRect.anchorMax = sourceRect.anchorMax;
            targetRect.anchoredPosition = sourceRect.anchoredPosition;
            targetRect.sizeDelta = sourceRect.sizeDelta;
            targetRect.pivot = sourceRect.pivot;
            targetRect.offsetMin = sourceRect.offsetMin;
            targetRect.offsetMax = sourceRect.offsetMax;
            
            /* Debug.Log($"DraftPackSetup: Mirrored RectTransform properties from {sourceContainer.name} to {targetContainer.name}"); */
            /* Debug.Log($"  - Size Delta: {sourceRect.sizeDelta}"); */
            /* Debug.Log($"  - Anchored Position: {sourceRect.anchoredPosition}"); */
            /* Debug.Log($"  - Anchor Min/Max: {sourceRect.anchorMin}/{sourceRect.anchorMax}"); */
        }
        else
        {
            /* Debug.Log($"DraftPackSetup: Mirrored basic Transform properties from {sourceContainer.name} to {targetContainer.name}"); */
        }
    }
    
    /// <summary>
    /// Syncs transform mirroring to all clients
    /// </summary>
    [ObserversRpc]
    private void ObserversMirrorCardContainerTransform(int packNetObjId)
    {
        /* Debug.Log($"DraftPackSetup.ObserversMirrorCardContainerTransform called on {(IsServerInitialized ? "Server" : "Client")} - Pack NOB ID: {packNetObjId}"); */
        
        // Find the DraftCanvasManager if we don't have it
        if (draftCanvasManager == null)
        {
            draftCanvasManager = FindFirstObjectByType<DraftCanvasManager>();
        }
        
        if (draftCanvasManager == null || draftCanvasManager.DraftPackContainer == null)
        {
            Debug.LogError("DraftPackSetup: Cannot mirror transform - DraftCanvasManager or DraftPackContainer not found");
            return;
        }
        
        // Find the pack NetworkObject
        NetworkObject packNetObj = null;
        bool foundPack = false;
        
        if (FishNet.InstanceFinder.NetworkManager.IsClientStarted)
        {
            foundPack = FishNet.InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(packNetObjId, out packNetObj);
        }
        else if (FishNet.InstanceFinder.NetworkManager.IsServerStarted)
        {
            foundPack = FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(packNetObjId, out packNetObj);
        }
        
        if (!foundPack || packNetObj == null)
        {
            Debug.LogError($"DraftPackSetup: Failed to find pack NetworkObject with ID {packNetObjId}");
            return;
        }
        
        // Get the DraftPack component and its CardContainer
        DraftPack draftPack = packNetObj.GetComponent<DraftPack>();
        if (draftPack == null || draftPack.CardContainer == null)
        {
            Debug.LogError("DraftPackSetup: Pack has no DraftPack component or CardContainer");
            return;
        }
        
        // Mirror the transform properties
        MirrorTransformProperties(draftCanvasManager.DraftPackContainer, draftPack.CardContainer);
    }
    
    /// <summary>
    /// Creates draft packs for all players
    /// </summary>
    [Server]
    public void CreateDraftPacks()
    {
        if (!IsServerInitialized)
        {
            Debug.LogError("DraftPackSetup: Cannot create draft packs - server not initialized");
            return;
        }
        
        if (gameManager == null)
        {
            Debug.LogError("DraftPackSetup: Cannot create draft packs - GameManager not found");
            return;
        }
        
        // Get all player entities
        List<NetworkEntity> players = GetAllPlayerEntities();
        
        if (players.Count == 0)
        {
            Debug.LogError("DraftPackSetup: No players found to create draft packs for");
            return;
        }
        
        int packSize = gameManager.DraftPackSize.Value;
        /* Debug.Log($"DraftPackSetup: Creating {players.Count} draft packs with {packSize} cards each"); */
        
        // Clear any existing packs
        ClearExistingPacks();
        
        // Create one pack per player
        foreach (NetworkEntity player in players)
        {
            CreateDraftPackForPlayer(player, packSize);
        }
        
        /* Debug.Log($"DraftPackSetup: Successfully created {spawnedPacks.Count} draft packs"); */
    }
    
    /// <summary>
    /// Creates a single draft pack for a specific player
    /// </summary>
    [Server]
    private void CreateDraftPackForPlayer(NetworkEntity player, int packSize)
    {
        if (draftPackPrefab == null)
        {
            Debug.LogError("DraftPackSetup: Cannot create pack - prefab is null");
            return;
        }
        
        /* Debug.Log($"DraftPackSetup: Creating draft pack for player {player.EntityName.Value}"); */
        
        // Instantiate the pack
        GameObject packObject = Instantiate(draftPackPrefab);
        
        // Set position and parent
        if (draftPackContainer != null)
        {
            packObject.transform.SetParent(draftPackContainer);
        }
        packObject.transform.localPosition = Vector3.zero;
        
        // Get the DraftPack component
        DraftPack draftPack = packObject.GetComponent<DraftPack>();
        if (draftPack == null)
        {
            Debug.LogError("DraftPackSetup: Instantiated pack has no DraftPack component");
            Destroy(packObject);
            return;
        }
        
        // Spawn the pack on the network
        NetworkObject packNetObj = packObject.GetComponent<NetworkObject>();
        if (packNetObj != null)
        {
            FishNet.InstanceFinder.ServerManager.Spawn(packNetObj);
            /* Debug.Log($"DraftPackSetup: Spawned draft pack on network for player {player.EntityName.Value}"); */
            
            // Sync the pack parenting to all clients
            if (draftPackContainer != null)
            {
                ObserversSyncPackParenting(packNetObj.ObjectId, this.NetworkObject.ObjectId, $"DraftPack_{player.EntityName.Value}");
            }
        }
        else
        {
            Debug.LogError("DraftPackSetup: Pack prefab has no NetworkObject component");
            Destroy(packObject);
            return;
        }
        
        // Initialize the pack with cards
        draftPack.InitializePack(packSize, player);
        
        // Mirror the transform properties from DraftCanvasManager's DraftPackContainer to this pack's CardContainer
        if (draftCanvasManager != null && draftCanvasManager.DraftPackContainer != null && draftPack.CardContainer != null)
        {
            // Mirror on server first
            MirrorTransformProperties(draftCanvasManager.DraftPackContainer, draftPack.CardContainer);
            
            // Sync the mirroring to all clients
            ObserversMirrorCardContainerTransform(packNetObj.ObjectId);
        }
        else
        {
            Debug.LogWarning("DraftPackSetup: Cannot mirror transform properties - missing DraftCanvasManager, DraftPackContainer, or CardContainer");
        }
        
        // Add to our list of spawned packs
        spawnedPacks.Add(draftPack);
        
        // Subscribe to pack events
        draftPack.OnPackEmpty += OnPackEmpty;
        
        /* Debug.Log($"DraftPackSetup: Successfully created and initialized draft pack for player {player.EntityName.Value}"); */
    }
    
    /// <summary>
    /// Clears all existing draft packs
    /// </summary>
    [Server]
    private void ClearExistingPacks()
    {
        /* Debug.Log($"DraftPackSetup: Clearing {spawnedPacks.Count} existing packs"); */
        
        foreach (DraftPack pack in spawnedPacks)
        {
            if (pack != null)
            {
                // Unsubscribe from events
                pack.OnPackEmpty -= OnPackEmpty;
                
                // Despawn the pack
                NetworkObject packNetObj = pack.GetComponent<NetworkObject>();
                if (packNetObj != null && packNetObj.IsSpawned)
                {
                    FishNet.InstanceFinder.ServerManager.Despawn(packNetObj);
                }
            }
        }
        
        spawnedPacks.Clear();
    }
    
    /// <summary>
    /// Gets all player entities in the game
    /// </summary>
    private List<NetworkEntity> GetAllPlayerEntities()
    {
        return FishNet.InstanceFinder.NetworkManager.ServerManager.Objects.Spawned.Values
            .Select(nob => nob.GetComponent<NetworkEntity>())
            .Where(entity => entity != null && entity.EntityType == EntityType.Player)
            .ToList();
    }
    
    /// <summary>
    /// Called when a draft pack becomes empty
    /// </summary>
    private void OnPackEmpty(DraftPack emptyPack)
    {
        /* Debug.Log($"DraftPackSetup: Pack {emptyPack.name} is now empty"); */
        
        // Remove from our list
        spawnedPacks.Remove(emptyPack);
        
        // Unsubscribe from events
        emptyPack.OnPackEmpty -= OnPackEmpty;
        
        // Despawn the empty pack
        NetworkObject packNetObj = emptyPack.GetComponent<NetworkObject>();
        if (packNetObj != null && packNetObj.IsSpawned)
        {
            FishNet.InstanceFinder.ServerManager.Despawn(packNetObj);
        }
    }
    
    /// <summary>
    /// Gets the draft pack currently owned by a specific player
    /// </summary>
    public DraftPack GetPackOwnedBy(NetworkEntity player)
    {
        return spawnedPacks.FirstOrDefault(pack => pack.IsOwnedBy(player));
    }
    
    /// <summary>
    /// Gets all active draft packs
    /// </summary>
    public List<DraftPack> GetAllActivePacks()
    {
        return new List<DraftPack>(spawnedPacks);
    }
    
    /// <summary>
    /// Checks if all packs are empty (draft is complete)
    /// </summary>
    public bool AreAllPacksEmpty()
    {
        return spawnedPacks.Count == 0 || spawnedPacks.All(pack => pack.IsEmpty);
    }
    
    /// <summary>
    /// Gets the total number of cards remaining across all packs
    /// </summary>
    public int GetTotalCardsRemaining()
    {
        return spawnedPacks.Sum(pack => pack.CardCount);
    }
    
    /// <summary>
    /// Syncs draft pack parenting to all clients
    /// </summary>
    [ObserversRpc]
    private void ObserversSyncPackParenting(int packNetObjId, int setupNetObjId, string packName)
    {
        /* Debug.Log($"DraftPackSetup.ObserversSyncPackParenting called on {(IsServerInitialized ? "Server" : "Client")} - Pack NOB ID: {packNetObjId}, Setup NOB ID: {setupNetObjId}, Pack Name: {packName}"); */
        
        NetworkObject packNetObj = null;
        bool foundPack = false;
        
        if (FishNet.InstanceFinder.NetworkManager.IsClientStarted)
        {
            foundPack = FishNet.InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(packNetObjId, out packNetObj);
        }
        else if (FishNet.InstanceFinder.NetworkManager.IsServerStarted)
        {
            foundPack = FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(packNetObjId, out packNetObj);
        }
        
        if (!foundPack || packNetObj == null)
        {
            Debug.LogError($"DraftPackSetup: Failed to find pack NetworkObject with ID {packNetObjId} on {(IsServerInitialized ? "Server" : "Client")}.");
            return;
        }

        GameObject packObject = packNetObj.gameObject;
        packObject.name = packName;

        // Ensure this DraftPackSetup instance matches the intended setup
        if (this.NetworkObject.ObjectId != setupNetObjId)
        {
            Debug.LogError($"DraftPackSetup.ObserversSyncPackParenting on {gameObject.name} (Setup NOB ID: {this.NetworkObject.ObjectId}): Received setupNetObjId {setupNetObjId} which does not match this setup. Pack will not be parented here.");
            return;
        }

        if (draftPackContainer == null)
        {
            Debug.LogError($"DraftPackSetup on {gameObject.name} (Setup NOB ID: {this.NetworkObject.ObjectId}): draftPackContainer is null on client. Pack {packName} (NOB ID: {packNetObjId}) cannot be parented to container.");
            return;
        }

        /* Debug.Log($"DraftPackSetup on {gameObject.name} (Client): Parenting pack {packName} (NOB ID: {packNetObjId}) to draftPackContainer: {draftPackContainer.name}"); */
        packObject.transform.SetParent(draftPackContainer, false); // worldPositionStays = false to correctly apply local transforms
        packObject.transform.localPosition = Vector3.zero;
        packObject.transform.localRotation = Quaternion.identity;
        packObject.transform.localScale = Vector3.one;
        
        // Draft packs should be visible
        packObject.SetActive(true);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Re-resolve references when client starts to ensure we have all components
        if (draftCanvasManager == null) draftCanvasManager = FindFirstObjectByType<DraftCanvasManager>();
        Debug.Log("DraftPackSetup: Client started, references resolved");
    }
    
    /// <summary>
    /// Public method to refresh transform mirroring for all spawned packs
    /// </summary>
    public void RefreshTransformMirroring()
    {
        if (draftCanvasManager == null) draftCanvasManager = FindFirstObjectByType<DraftCanvasManager>();
        
        if (draftCanvasManager == null || draftCanvasManager.DraftPackContainer == null)
        {
            Debug.LogWarning("DraftPackSetup: Cannot refresh transform mirroring - DraftCanvasManager or DraftPackContainer not found");
            return;
        }
        
        foreach (DraftPack pack in spawnedPacks)
        {
            if (pack != null && pack.CardContainer != null)
            {
                MirrorTransformProperties(draftCanvasManager.DraftPackContainer, pack.CardContainer);
                
                // If we're on server, sync to clients
                if (IsServerInitialized)
                {
                    NetworkObject packNetObj = pack.GetComponent<NetworkObject>();
                    if (packNetObj != null)
                    {
                        ObserversMirrorCardContainerTransform(packNetObj.ObjectId);
                    }
                }
            }
        }
        
        /* Debug.Log($"DraftPackSetup: Refreshed transform mirroring for {spawnedPacks.Count} packs"); */
    }
} 