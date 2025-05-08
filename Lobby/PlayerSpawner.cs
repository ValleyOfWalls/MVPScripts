using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing.Server;
using FishNet.Managing;
using System.Collections.Generic;

/// <summary>
/// Handles the spawning of NetworkPlayer and NetworkPet prefabs for connected clients.
/// Attach to: The same GameObject as SteamNetworkIntegration to handle player spawning when connections are established.
/// </summary>
// Convert to MonoBehaviour to be attached to a GameObject alongside SteamNetworkIntegration
public class PlayerSpawner : MonoBehaviour
{
    private NetworkObject networkPlayerPrefab;
    private NetworkObject networkPetPrefab;
    private NetworkManager fishNetManager;
    private SteamNetworkIntegration steamNetworkIntegration;

    private void Awake()
    {
        steamNetworkIntegration = GetComponent<SteamNetworkIntegration>();
        fishNetManager = FishNet.InstanceFinder.NetworkManager;

        if (steamNetworkIntegration != null) 
        {
            networkPlayerPrefab = steamNetworkIntegration.NetworkPlayerPrefab?.GetComponent<NetworkObject>();
            networkPetPrefab = steamNetworkIntegration.NetworkPetPrefab?.GetComponent<NetworkObject>();
        }
        else
        {
            Debug.LogError("PlayerSpawner: SteamNetworkIntegration component not found on the same GameObject.");
        }

        if (networkPlayerPrefab == null)
        {
            Debug.LogWarning("PlayerSpawner: NetworkPlayer prefab is not assigned on SteamNetworkIntegration or does not have a NetworkObject component.");
        }
        if (networkPetPrefab == null)
        {
            Debug.LogWarning("PlayerSpawner: NetworkPet prefab is not assigned on SteamNetworkIntegration or does not have a NetworkObject component.");
        }
        if (fishNetManager == null)
        {
            Debug.LogError("PlayerSpawner: FishNet NetworkManager instance not found. Spawning will fail.");
        }
    }

    public void SpawnPlayerForConnection(NetworkConnection conn)
    {
        if (fishNetManager == null || !fishNetManager.ServerManager.Started)
        {
            Debug.LogError("PlayerSpawner: Cannot spawn player. FishNet ServerManager is not started or NetworkManager is null.");
            return;
        }

        if (networkPlayerPrefab == null) 
        {
            Debug.LogError("NetworkPlayer prefab is not assigned in PlayerSpawner.");
            return; 
        }
        if (networkPetPrefab == null) 
        {
            Debug.LogError("NetworkPet prefab is not assigned in PlayerSpawner.");
            return; 
        }

        Debug.Log($"PlayerSpawner: Attempting to spawn NetworkPlayer for client {conn.ClientId}.");
        GameObject playerGameObjectInstance = Object.Instantiate(networkPlayerPrefab.gameObject);
        fishNetManager.ServerManager.Spawn(playerGameObjectInstance, conn);
        NetworkObject playerInstance = playerGameObjectInstance.GetComponent<NetworkObject>();

        if (playerInstance != null)
        {
            Debug.Log($"Spawned NetworkPlayer (ID: {playerInstance.ObjectId}, OwnerId: {playerInstance.OwnerId}) for client {conn.ClientId}");
            NetworkPlayer netPlayer = playerInstance.GetComponent<NetworkPlayer>();
            if(netPlayer != null) {
                // Set player name (e.g., from Steam or connection ID)
                if (steamNetworkIntegration != null && steamNetworkIntegration.IsSteamInitialized)
                {
                    // This requires a way to map NetworkConnection to CSteamID if you want specific Steam names for remote players.
                    // For the host, conn.FirstObjectId (if reliable after spawn) or a direct call to GetPlayerName() can be used.
                    // For now, using a generic name.
                    string steamName = "Player";
                    if (conn == fishNetManager.ClientManager.Connection) // Is this the host's own connection?
                    {
                         steamName = steamNetworkIntegration.GetPlayerName();
                    }
                    else
                    {
                        // For remote clients, you'd need a system to get their Steam name via their CSteamID
                        // which might involve sending it over the network or using Steam P2P auth data.
                        // CSteamID remoteUserSteamId = GetSteamIDForConnection(conn); // Placeholder for this logic
                        // steamName = steamNetworkIntegration.GetFriendName(remoteUserSteamId);
                    }
                    netPlayer.PlayerName.Value = $"{steamName} ({conn.ClientId})";
                }
                else
                {
                    netPlayer.PlayerName.Value = "Player " + conn.ClientId;
                }
            }

            Debug.Log($"PlayerSpawner: Attempting to spawn NetworkPet for client {conn.ClientId}.");
            GameObject petGameObjectInstance = Object.Instantiate(networkPetPrefab.gameObject);
            fishNetManager.ServerManager.Spawn(petGameObjectInstance, conn);
            NetworkObject petInstance = petGameObjectInstance.GetComponent<NetworkObject>();

            if (petInstance != null)
            {
                Debug.Log($"Spawned NetworkPet (ID: {petInstance.ObjectId}, OwnerId: {petInstance.OwnerId}) for client {conn.ClientId}");
                NetworkPet netPet = petInstance.GetComponent<NetworkPet>();
                if(netPet != null && netPlayer != null)
                {
                    netPet.SetOwnerPlayer(netPlayer);
                    
                    // Establish relationship between player and pet
                    RelationshipManager.SetupPlayerPetRelationship(netPlayer, netPet);
                }
            }
            else
            {
                Debug.LogError("Failed to spawn NetworkPet.");
            }
        }
        else
        {
            Debug.LogError("Failed to spawn NetworkPlayer.");
        }
    }

    public void DespawnEntitiesForConnection(NetworkConnection conn)
    {
        if (fishNetManager == null || !fishNetManager.ServerManager.Started)
        {
             Debug.LogWarning("PlayerSpawner: Cannot despawn. FishNet ServerManager is not started or NetworkManager is null.");
            return;
        }
        // FishNet usually handles despawning objects owned by the connection automatically.
        // If you have custom logic for unregistering, it would go here.
        Debug.Log($"PlayerSpawner: DespawnEntitiesForConnection called for {conn.ClientId}. FishNet should handle owned object despawn.");
    }
} 