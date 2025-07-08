using UnityEngine;
using FishNet.Object;
using FishNet.Managing;

/// <summary>
/// Spawns network managers when server starts.
/// Attach this to the NetworkManager GameObject or any persistent object.
/// </summary>
public class NetworkManagerSpawner : MonoBehaviour
{
    [Header("Network Manager Prefabs")]
    [SerializeField, Tooltip("OnlineGameManager prefab - must be a NetworkObject")]
    private GameObject onlineGameManagerPrefab;
    
    [SerializeField, Tooltip("NetworkCardDatabase prefab - must be a NetworkObject")]
    private GameObject networkCardDatabasePrefab;
    
    [Header("References")]
    private NetworkManager fishNetManager;
    
    // Track spawned objects
    private NetworkObject spawnedOnlineGameManager;
    private NetworkObject spawnedNetworkCardDatabase;

    private void Awake()
    {
        Debug.Log("[NETSPAWN] NetworkManagerSpawner.Awake() called");
        
        // Find the NetworkManager
        fishNetManager = FindFirstObjectByType<NetworkManager>();
        if (fishNetManager == null)
        {
            Debug.LogError("[NETSPAWN] ERROR: No NetworkManager found in scene!");
            return;
        }

        Debug.Log("[NETSPAWN] Found NetworkManager, subscribing to server events");
        
        // Subscribe to server events
        fishNetManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (fishNetManager != null && fishNetManager.ServerManager != null)
        {
            fishNetManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        }
    }

    /// <summary>
    /// Called when server connection state changes
    /// </summary>
    private void OnServerConnectionState(FishNet.Transporting.ServerConnectionStateArgs args)
    {
        Debug.Log($"[NETSPAWN] OnServerConnectionState called - State: {args.ConnectionState}");
        
        if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Started)
        {
            Debug.Log("[NETSPAWN] Server started, spawning network managers...");
            SpawnNetworkManagers();
        }
        else if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Stopped)
        {
            Debug.Log("[NETSPAWN] Server stopped, cleaning up network managers...");
            CleanupNetworkManagers();
        }
    }

    /// <summary>
    /// Spawn the network manager objects
    /// </summary>
    private void SpawnNetworkManagers()
    {
        Debug.Log($"[NETSPAWN] SpawnNetworkManagers() called - IsServerStarted: {fishNetManager.IsServerStarted}");
        
        if (!fishNetManager.IsServerStarted)
        {
            Debug.LogWarning("[NETSPAWN] WARNING: Server not started, cannot spawn network managers");
            return;
        }

        Debug.Log($"[NETSPAWN] Checking prefab assignments - OnlineGameManager: {(onlineGameManagerPrefab != null ? "Assigned" : "NULL")}, NetworkCardDatabase: {(networkCardDatabasePrefab != null ? "Assigned" : "NULL")}");

        // Spawn OnlineGameManager
        if (onlineGameManagerPrefab != null)
        {
            Debug.Log("[NETSPAWN] Instantiating OnlineGameManager prefab...");
            GameObject onlineManagerObj = Instantiate(onlineGameManagerPrefab);
            spawnedOnlineGameManager = onlineManagerObj.GetComponent<NetworkObject>();
            
            if (spawnedOnlineGameManager != null)
            {
                Debug.Log("[NETSPAWN] OnlineGameManager NetworkObject found, spawning...");
                fishNetManager.ServerManager.Spawn(spawnedOnlineGameManager);
                Debug.Log("[NETSPAWN] SUCCESS: OnlineGameManager spawned successfully!");
            }
            else
            {
                Debug.LogError("[NETSPAWN] ERROR: OnlineGameManager prefab missing NetworkObject component!");
                Destroy(onlineManagerObj);
            }
        }
        else
        {
            Debug.LogError("[NETSPAWN] ERROR: OnlineGameManager prefab not assigned!");
        }

        // Spawn NetworkCardDatabase
        if (networkCardDatabasePrefab != null)
        {
            Debug.Log("[NETSPAWN] Instantiating NetworkCardDatabase prefab...");
            GameObject cardDbObj = Instantiate(networkCardDatabasePrefab);
            spawnedNetworkCardDatabase = cardDbObj.GetComponent<NetworkObject>();
            
            if (spawnedNetworkCardDatabase != null)
            {
                Debug.Log("[NETSPAWN] NetworkCardDatabase NetworkObject found, spawning...");
                fishNetManager.ServerManager.Spawn(spawnedNetworkCardDatabase);
                Debug.Log("[NETSPAWN] SUCCESS: NetworkCardDatabase spawned successfully!");
            }
            else
            {
                Debug.LogError("[NETSPAWN] ERROR: NetworkCardDatabase prefab missing NetworkObject component!");
                Destroy(cardDbObj);
            }
        }
        else
        {
            Debug.LogError("[NETSPAWN] ERROR: NetworkCardDatabase prefab not assigned!");
        }
        
        Debug.Log("[NETSPAWN] SpawnNetworkManagers() completed");
    }

    /// <summary>
    /// Clean up spawned network managers
    /// </summary>
    private void CleanupNetworkManagers()
    {
        if (spawnedOnlineGameManager != null)
        {
            if (fishNetManager.IsServerStarted)
            {
                fishNetManager.ServerManager.Despawn(spawnedOnlineGameManager);
            }
            spawnedOnlineGameManager = null;
        }

        if (spawnedNetworkCardDatabase != null)
        {
            if (fishNetManager.IsServerStarted)
            {
                fishNetManager.ServerManager.Despawn(spawnedNetworkCardDatabase);
            }
            spawnedNetworkCardDatabase = null;
        }
    }

    // Debug methods for testing
    [ContextMenu("Force Spawn Network Managers")]
    public void ForceSpawnNetworkManagers()
    {
        if (Application.isPlaying)
        {
            SpawnNetworkManagers();
        }
    }

    [ContextMenu("Force Cleanup Network Managers")]
    public void ForceCleanupNetworkManagers()
    {
        if (Application.isPlaying)
        {
            CleanupNetworkManagers();
        }
    }
} 