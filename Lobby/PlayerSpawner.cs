using UnityEngine;
using FishNet;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using System.Collections;

public class PlayerSpawner : NetworkBehaviour
{
    [SerializeField] public GameObject playerPrefab;
    
    private bool initialized = false;
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log($"PlayerSpawner.OnStartServer called, ID: {NetworkObject.ObjectId}");
        
        // Subscribe to connection events
        if (NetworkManager.ServerManager != null)
        {
            NetworkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
        }
        else
        {
            Debug.LogError("PlayerSpawner: ServerManager is null, cannot subscribe to connection events.");
        }
        
        // Ensure we have a player prefab
        if (playerPrefab == null)
        {
            StartCoroutine(InitializeWithDelay());
        }
        else
        {
            initialized = true;
        }
    }
    
    private IEnumerator InitializeWithDelay()
    {
        // Try to find player prefab with a small delay to ensure GameManager is fully initialized
        yield return new WaitForSeconds(0.2f);
        
        if (playerPrefab == null)
        {
            FindPlayerPrefab();
            
            if (playerPrefab != null)
            {
                Debug.Log($"PlayerSpawner initialized with prefab: {playerPrefab.name}");
                initialized = true;
            }
            else
            {
                Debug.LogError("PlayerSpawner failed to find a player prefab after delay");
            }
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        Debug.Log("PlayerSpawner.OnStopServer called");
        
        // Unsubscribe from connection events
        if (NetworkManager.ServerManager != null)
        {
            NetworkManager.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState;
        }

        // Unsubscribe from any lingering scene load events (safety measure)
        if (NetworkManager != null && NetworkManager.ServerManager != null)
        {
            foreach (NetworkConnection conn in NetworkManager.ServerManager.Clients.Values)
            {
                if (conn != null && conn.IsValid) // Check if connection is valid
                {
                    conn.OnLoadedStartScenes -= Connection_OnLoadedStartScenes;
                }
            }
        }
    }

    private void ServerManager_OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        Debug.Log($"ServerManager_OnRemoteConnectionState called: ConnectionState={args.ConnectionState}, ClientId={conn.ClientId}");
        
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            // Wait for the client to load the start scenes before spawning player
            Debug.Log($"Client connected: {conn.ClientId}. Waiting for scene load confirmation...");
            conn.OnLoadedStartScenes += Connection_OnLoadedStartScenes;
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            // Client disconnected, ensure we unsubscribe from the scene load event
            Debug.Log($"Client disconnected: {conn.ClientId}. Unsubscribing from scene load event.");
            conn.OnLoadedStartScenes -= Connection_OnLoadedStartScenes;
            // Player object cleanup is usually handled automatically by FishNet when owner disconnects
        }
    }

    private void Connection_OnLoadedStartScenes(NetworkConnection conn, bool asServer)
    {
        Debug.Log($"Connection {conn.ClientId} has loaded start scenes. Spawning player...");

        // Unsubscribe immediately to prevent multiple spawns for the same connection
        conn.OnLoadedStartScenes -= Connection_OnLoadedStartScenes;

        // Ensure initialization check before spawning
        if (!initialized)
        {
            Debug.LogWarning("PlayerSpawner not fully initialized, attempting late initialization before spawning.");
            FindPlayerPrefab(); // Attempt to find prefab if initialization was delayed
            if (playerPrefab != null) initialized = true;
        }

        if (initialized && playerPrefab != null)
        {
            SpawnPlayer(conn);
        }
        else
        {
            Debug.LogError($"Failed to spawn player for connection {conn.ClientId}: Spawner not initialized or prefab missing.");
        }
    }

    private void SpawnPlayer(NetworkConnection conn)
    {
        Debug.Log($"Attempting to spawn player for connection {conn.ClientId}");
        
        // Double check prefab just before instantiation
        if (playerPrefab == null)
        {
            Debug.LogError($"Cannot spawn player for connection {conn.ClientId}: playerPrefab is null!");
            return;
        }
        
        try
        {
            // Spawn the player prefab for the connection
            GameObject playerObj = Instantiate(playerPrefab);
            NetworkObject nob = playerObj.GetComponent<NetworkObject>();
            
            // Make sure the LobbyPlayerInfo component is attached to the player
            if (playerObj.GetComponent<LobbyPlayerInfo>() == null)
            {
                playerObj.AddComponent<LobbyPlayerInfo>();
                Debug.Log("Added LobbyPlayerInfo component to player prefab instance.");
            }
            
            if (nob != null)
            {
                // Check if connection is still valid before spawning
                if (!conn.IsActive)
                {
                    Debug.LogError($"Cannot spawn player: Connection {conn.ClientId} is no longer active");
                    Destroy(playerObj);
                    return;
                }
                
                try
                {
                    // Use the correct method to spawn player with owner
                    NetworkManager.ServerManager.Spawn(playerObj, conn);
                    Debug.Log($"Player successfully spawned for connection {conn.ClientId} with NetworkObject ID: {nob.ObjectId}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Error spawning player NetworkObject for Conn {conn.ClientId}: {ex.Message}\n{ex.StackTrace}");
                    Destroy(playerObj);
                }
            }
            else
            {
                Debug.LogError("Failed to spawn player: NetworkObject component not found on player prefab.");
                Destroy(playerObj);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error during player instantiation or spawning for Conn {conn.ClientId}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void FindPlayerPrefab()
    {
        Debug.Log("Attempting to find player prefab through fallback methods...");
        
        // Try to get from GameManager first
        if (GameManager.Instance != null && GameManager.Instance.playerPrefab != null)
        {
            playerPrefab = GameManager.Instance.playerPrefab;
            Debug.Log($"Found player prefab from GameManager: {playerPrefab.name}");
            return;
        }
        
        // Try to find player prefab in Resources folder
        GameObject resourcePrefab = Resources.Load<GameObject>("PlayerPrefab");
        if (resourcePrefab != null)
        {
            playerPrefab = resourcePrefab;
            Debug.Log($"Found player prefab in Resources: {playerPrefab.name}");
            return;
        }
        
        Debug.LogWarning("Could not find a player prefab via GameManager or Resources.");
    }

    // Get the player prefab reference from GameManager if possible
    private void Start()
    {
        if (playerPrefab != null)
        {
            Debug.Log($"PlayerSpawner has player prefab: {playerPrefab.name}");
            initialized = true;
        }
        else
        {
            Debug.LogWarning("PlayerSpawner does not have player prefab assigned. Will attempt to retrieve from GameManager or fallbacks.");
            FindPlayerPrefab();
            
            if (playerPrefab != null)
            {
                Debug.Log($"PlayerSpawner found and assigned player prefab: {playerPrefab.name}");
                initialized = true;
            }
            else
            {
                Debug.LogError("PlayerSpawner could not find a Player Prefab during Start/Initialization!");
            }
        }
    }
}