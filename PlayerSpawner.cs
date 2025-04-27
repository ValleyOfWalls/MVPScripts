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
        NetworkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
        
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
        NetworkManager.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState;
    }

    private void ServerManager_OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        Debug.Log($"ServerManager_OnRemoteConnectionState called: ConnectionState={args.ConnectionState}, ClientId={conn.ClientId}");
        
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            Debug.Log($"Player connected! Spawning player for connection {conn.ClientId}");
            
            if (!initialized)
            {
                Debug.Log("PlayerSpawner not initialized yet, attempting to find player prefab before spawning");
                FindPlayerPrefab();
                initialized = true;
            }
            
            SpawnPlayer(conn);
        }
    }

    private void SpawnPlayer(NetworkConnection conn)
    {
        Debug.Log($"Attempting to spawn player for connection {conn.ClientId}");
        
        // Try to set player prefab if null
        if (playerPrefab == null)
        {
            FindPlayerPrefab();
            
            if (playerPrefab == null)
            {
                Debug.LogError("Cannot spawn player: Failed to find player prefab through all fallback methods.");
                return;
            }
        }
        
        try
        {
            // Spawn the player prefab for the connection
            GameObject playerObj = Instantiate(playerPrefab);
            NetworkObject nob = playerObj.GetComponent<NetworkObject>();
            
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
                    Debug.LogError($"Error spawning player NetworkObject: {ex.Message}\n{ex.StackTrace}");
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
            Debug.LogError($"Error spawning player: {ex.Message}\n{ex.StackTrace}");
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
        
        // Try to find any existing NetworkObject prefabs in the scene
        // or registered prefabs that might be player prefabs
        if (InstanceFinder.NetworkManager != null)
        {
            Debug.Log("Checking for Player component in existing NetworkObjects");
            // Look for existing PlayerObjects in the scene as a last resort
            Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
            if (players != null && players.Length > 0)
            {
                foreach (Player player in players)
                {
                    if (player != null && player.gameObject != null && player.GetComponent<NetworkObject>() != null)
                    {
                        playerPrefab = player.gameObject;
                        Debug.Log($"Found player prefab from scene: {playerPrefab.name}");
                        return;
                    }
                }
            }
        }
        
        Debug.LogWarning("Could not find a player prefab in any location");
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
            Debug.LogWarning("PlayerSpawner does not have player prefab assigned. Will attempt to retrieve from GameManager.");
            FindPlayerPrefab();
            
            if (playerPrefab != null)
            {
                Debug.Log($"PlayerSpawner found and assigned player prefab: {playerPrefab.name}");
                initialized = true;
            }
        }
    }
} 