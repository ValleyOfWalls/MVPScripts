using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using MVPScripts.Utility;

/// <summary>
/// Handles the creation and setup of the shop during the drafting phase.
/// Attach to: A NetworkObject in the scene that coordinates shop creation.
/// </summary>
public class ShopSetup : NetworkBehaviour
{
    [Header("Shop Configuration")]
    [SerializeField] private GameObject shopPackPrefab;
    [SerializeField] private Transform shopContainer;
    
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private DraftCanvasManager draftCanvasManager;
    
    private ShopPack spawnedShop = null;
    
    public ShopPack SpawnedShop => spawnedShop;
    
    private void Awake()
    {
        ComponentResolver.FindComponentWithSingleton(ref gameManager, () => GameManager.Instance, gameObject);
        ComponentResolver.FindComponent(ref draftCanvasManager, gameObject);
        
        if (shopPackPrefab == null)
        {
            Debug.LogError("ShopSetup: Shop pack prefab is not assigned!");
        }
        else
        {
            // Validate the prefab has required components
            ValidateShopPackPrefab();
        }
    }
    
    private void ValidateShopPackPrefab()
    {
        if (shopPackPrefab.GetComponent<ShopPack>() == null)
            Debug.LogError("ShopSetup: Shop pack prefab is missing ShopPack component!");
        if (shopPackPrefab.GetComponent<NetworkObject>() == null)
            Debug.LogError("ShopSetup: Shop pack prefab is missing NetworkObject component!");
        if (shopPackPrefab.GetComponent<CardSpawner>() == null)
            Debug.LogError("ShopSetup: Shop pack prefab is missing CardSpawner component!");
    }
    
    /// <summary>
    /// Mirrors transform properties from DraftCanvasManager's ShopContainer to a ShopPack's CardContainer
    /// </summary>
    private void MirrorTransformProperties(Transform sourceContainer, Transform targetContainer)
    {
        if (sourceContainer == null || targetContainer == null)
        {
            Debug.LogWarning("ShopSetup: Cannot mirror transform properties - source or target is null");
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
            
            /* Debug.Log($"ShopSetup: Mirrored RectTransform properties from {sourceContainer.name} to {targetContainer.name}"); */
            /* Debug.Log($"  - Size Delta: {sourceRect.sizeDelta}"); */
            /* Debug.Log($"  - Anchored Position: {sourceRect.anchoredPosition}"); */
            /* Debug.Log($"  - Anchor Min/Max: {sourceRect.anchorMin}/{sourceRect.anchorMax}"); */
        }
        else
        {
            /* Debug.Log($"ShopSetup: Mirrored basic Transform properties from {sourceContainer.name} to {targetContainer.name}"); */
        }
    }
    
    /// <summary>
    /// Syncs transform mirroring to all clients
    /// </summary>
    [ObserversRpc]
    private void ObserversMirrorCardContainerTransform(int shopNetObjId)
    {
        /* Debug.Log($"ShopSetup.ObserversMirrorCardContainerTransform called on {(IsServerInitialized ? "Server" : "Client")} - Shop NOB ID: {shopNetObjId}"); */
        
        // Find the DraftCanvasManager if we don't have it
        ComponentResolver.FindComponent(ref draftCanvasManager, gameObject);
        
        if (draftCanvasManager == null || draftCanvasManager.ShopContainer == null)
        {
            Debug.LogError("ShopSetup: Cannot mirror transform - DraftCanvasManager or ShopContainer not found");
            return;
        }
        
        // Find the shop NetworkObject
        NetworkObject shopNetObj = null;
        bool foundShop = false;
        
        if (FishNet.InstanceFinder.NetworkManager.IsClientStarted)
        {
            foundShop = FishNet.InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(shopNetObjId, out shopNetObj);
        }
        else if (FishNet.InstanceFinder.NetworkManager.IsServerStarted)
        {
            foundShop = FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(shopNetObjId, out shopNetObj);
        }
        
        if (!foundShop || shopNetObj == null)
        {
            Debug.LogError($"ShopSetup: Failed to find shop NetworkObject with ID {shopNetObjId}");
            return;
        }
        
        // Get the ShopPack component and its CardContainer
        ShopPack shopPack = shopNetObj.GetComponent<ShopPack>();
        if (shopPack == null || shopPack.CardContainer == null)
        {
            Debug.LogError("ShopSetup: Shop has no ShopPack component or CardContainer");
            return;
        }
        
        // Mirror the transform properties
        MirrorTransformProperties(draftCanvasManager.ShopContainer, shopPack.CardContainer);
    }
    
    /// <summary>
    /// Creates the shop for all players to access
    /// </summary>
    [Server]
    public void CreateShop()
    {
        if (!IsServerInitialized)
        {
            Debug.LogError("ShopSetup: Cannot create shop - server not initialized");
            return;
        }
        
        if (gameManager == null)
        {
            Debug.LogError("ShopSetup: Cannot create shop - GameManager not found");
            return;
        }
        
        if (spawnedShop != null)
        {
            Debug.LogWarning("ShopSetup: Shop already exists, clearing existing shop first");
            ClearExistingShop();
        }
        
        int shopSize = gameManager.ShopSize.Value;
        int minCost = gameManager.ShopMinCardCost.Value;
        int maxCost = gameManager.ShopMaxCardCost.Value;
        
        /* Debug.Log($"ShopSetup: Creating shop with {shopSize} cards, cost range: {minCost}-{maxCost}"); */
        
        CreateShopInstance(shopSize, minCost, maxCost);
        
        /* Debug.Log($"ShopSetup: Successfully created shop"); */
    }
    
    /// <summary>
    /// Creates a single shop instance
    /// </summary>
    [Server]
    private void CreateShopInstance(int shopSize, int minCost, int maxCost)
    {
        if (shopPackPrefab == null)
        {
            Debug.LogError("ShopSetup: Cannot create shop - prefab is null");
            return;
        }
        
        /* Debug.Log($"ShopSetup: Creating shop instance"); */
        
        // Instantiate the shop
        GameObject shopObject = Instantiate(shopPackPrefab);
        
        // Set position and parent
        if (shopContainer != null)
        {
            shopObject.transform.SetParent(shopContainer);
        }
        shopObject.transform.localPosition = Vector3.zero;
        shopObject.name = "GameShop";
        
        // Get the ShopPack component
        ShopPack shopPack = shopObject.GetComponent<ShopPack>();
        if (shopPack == null)
        {
            Debug.LogError("ShopSetup: Instantiated shop has no ShopPack component");
            Destroy(shopObject);
            return;
        }
        
        // Spawn the shop on the network
        NetworkObject shopNetObj = shopObject.GetComponent<NetworkObject>();
        if (shopNetObj != null)
        {
            FishNet.InstanceFinder.ServerManager.Spawn(shopNetObj);
            /* Debug.Log($"ShopSetup: Spawned shop on network"); */
            
            // Sync the shop parenting to all clients
            if (shopContainer != null)
            {
                ObserversSyncShopParenting(shopNetObj.ObjectId, this.NetworkObject.ObjectId);
            }
        }
        else
        {
            Debug.LogError("ShopSetup: Shop prefab has no NetworkObject component");
            Destroy(shopObject);
            return;
        }
        
        // Initialize the shop with cards using GameManager values
        shopPack.InitializeShop(shopSize, minCost, maxCost);
        
        // Mirror the transform properties from DraftCanvasManager's ShopContainer to this shop's CardContainer
        if (draftCanvasManager != null && draftCanvasManager.ShopContainer != null && shopPack.CardContainer != null)
        {
            // Mirror on server first
            MirrorTransformProperties(draftCanvasManager.ShopContainer, shopPack.CardContainer);
            
            // Sync the mirroring to all clients
            ObserversMirrorCardContainerTransform(shopNetObj.ObjectId);
        }
        else
        {
            Debug.LogWarning("ShopSetup: Cannot mirror transform properties - missing DraftCanvasManager, ShopContainer, or CardContainer");
        }
        
        // Store reference to the spawned shop
        spawnedShop = shopPack;
        
        // Subscribe to shop events
        spawnedShop.OnCardPurchased += OnCardPurchased;
        spawnedShop.OnShopRefreshed += OnShopRefreshed;
        
        /* Debug.Log($"ShopSetup: Successfully created and initialized shop"); */
    }
    
    /// <summary>
    /// Clears the existing shop
    /// </summary>
    [Server]
    private void ClearExistingShop()
    {
        /* Debug.Log($"ShopSetup: Clearing existing shop"); */
        
        if (spawnedShop != null)
        {
            // Unsubscribe from events
            spawnedShop.OnCardPurchased -= OnCardPurchased;
            spawnedShop.OnShopRefreshed -= OnShopRefreshed;
            
            // Despawn the shop
            NetworkObject shopNetObj = spawnedShop.GetComponent<NetworkObject>();
            if (shopNetObj != null && shopNetObj.IsSpawned)
            {
                FishNet.InstanceFinder.ServerManager.Despawn(shopNetObj);
            }
            
            spawnedShop = null;
        }
    }
    
    /// <summary>
    /// Called when a card is purchased from the shop
    /// </summary>
    private void OnCardPurchased(ShopPack shop, GameObject purchasedCard, NetworkEntity buyer)
    {
        Debug.Log($"ShopSetup: Card {purchasedCard.name} purchased by {buyer.EntityName.Value}");
        
        // The shop handles removing the card and deducting currency
        // Additional logic can be added here if needed
    }
    
    /// <summary>
    /// Called when the shop is refreshed
    /// </summary>
    private void OnShopRefreshed(ShopPack shop)
    {
        Debug.Log($"ShopSetup: Shop refreshed");
        
        // Additional logic for shop refresh can be added here if needed
    }
    
    /// <summary>
    /// Gets the current shop instance
    /// </summary>
    public ShopPack GetShop()
    {
        return spawnedShop;
    }
    
    /// <summary>
    /// Checks if the shop is available and has cards
    /// </summary>
    public bool IsShopAvailable()
    {
        return spawnedShop != null && !spawnedShop.IsEmpty;
    }
    
    /// <summary>
    /// Syncs shop parenting to all clients
    /// </summary>
    [ObserversRpc]
    private void ObserversSyncShopParenting(int shopNetObjId, int setupNetObjId)
    {
        /* Debug.Log($"ShopSetup.ObserversSyncShopParenting called on {(IsServerInitialized ? "Server" : "Client")} - Shop NOB ID: {shopNetObjId}, Setup NOB ID: {setupNetObjId}"); */
        
        NetworkObject shopNetObj = null;
        bool foundShop = false;
        
        if (FishNet.InstanceFinder.NetworkManager.IsClientStarted)
        {
            foundShop = FishNet.InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(shopNetObjId, out shopNetObj);
        }
        else if (FishNet.InstanceFinder.NetworkManager.IsServerStarted)
        {
            foundShop = FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(shopNetObjId, out shopNetObj);
        }
        
        if (!foundShop || shopNetObj == null)
        {
            Debug.LogError($"ShopSetup: Failed to find shop NetworkObject with ID {shopNetObjId} on {(IsServerInitialized ? "Server" : "Client")}.");
            return;
        }

        GameObject shopObject = shopNetObj.gameObject;
        shopObject.name = "GameShop";

        // Ensure this ShopSetup instance matches the intended setup
        if (this.NetworkObject.ObjectId != setupNetObjId)
        {
            Debug.LogError($"ShopSetup.ObserversSyncShopParenting on {gameObject.name} (Setup NOB ID: {this.NetworkObject.ObjectId}): Received setupNetObjId {setupNetObjId} which does not match this setup. Shop will not be parented here.");
            return;
        }

        if (shopContainer == null)
        {
            Debug.LogError($"ShopSetup on {gameObject.name} (Setup NOB ID: {this.NetworkObject.ObjectId}): shopContainer is null on client. Shop (NOB ID: {shopNetObjId}) cannot be parented to container.");
            return;
        }

        /* Debug.Log($"ShopSetup on {gameObject.name} (Client): Parenting shop (NOB ID: {shopNetObjId}) to shopContainer: {shopContainer.name}"); */
        shopObject.transform.SetParent(shopContainer, false); // worldPositionStays = false to correctly apply local transforms
        shopObject.transform.localPosition = Vector3.zero;
        shopObject.transform.localRotation = Quaternion.identity;
        shopObject.transform.localScale = Vector3.one;
        
        // Shop should be visible
        shopObject.SetActive(true);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Re-resolve references when client starts to ensure we have all components
        if (draftCanvasManager == null) ComponentResolver.FindComponent(ref draftCanvasManager, gameObject);
        Debug.Log("ShopSetup: Client started, references resolved");
    }
    
    /// <summary>
    /// Public method to refresh transform mirroring for the shop
    /// </summary>
    public void RefreshTransformMirroring()
    {
        if (draftCanvasManager == null) ComponentResolver.FindComponent(ref draftCanvasManager, gameObject);
        
        if (draftCanvasManager == null || draftCanvasManager.ShopContainer == null)
        {
            Debug.LogWarning("ShopSetup: Cannot refresh transform mirroring - DraftCanvasManager or ShopContainer not found");
            return;
        }
        
        if (spawnedShop != null && spawnedShop.CardContainer != null)
        {
            MirrorTransformProperties(draftCanvasManager.ShopContainer, spawnedShop.CardContainer);
            
            // If we're on server, sync to clients
            if (IsServerInitialized)
            {
                NetworkObject shopNetObj = spawnedShop.GetComponent<NetworkObject>();
                if (shopNetObj != null)
                {
                    ObserversMirrorCardContainerTransform(shopNetObj.ObjectId);
                }
            }
        }
        
        /* Debug.Log($"ShopSetup: Refreshed transform mirroring for shop"); */
    }
} 