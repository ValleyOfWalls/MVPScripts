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
 
    }

    public void SpawnPlayerForConnection(NetworkConnection conn)
    {
        
        // Log connection info for debugging
        bool isHostConnection = fishNetManager.IsHostStarted && conn.ClientId == 0;
        Debug.Log($"PlayerSpawner: Attempting to spawn NetworkPlayer for client {conn.ClientId}. IsHost: {isHostConnection}");
        
       
        // Create player instance
        GameObject playerGameObjectInstance = Object.Instantiate(networkPlayerPrefab.gameObject);
        
        // Spawn on server with the connection as the owner
        fishNetManager.ServerManager.Spawn(playerGameObjectInstance, conn);
        NetworkObject playerInstance = playerGameObjectInstance.GetComponent<NetworkObject>();

        if (playerInstance != null)
        {
            // Verify ownership assignment
            Debug.Log($"Spawned NetworkPlayer (ID: {playerInstance.ObjectId}, OwnerId: {playerInstance.OwnerId}, HasOwner: {playerInstance.Owner != null}) for client {conn.ClientId}");
            NetworkPlayer netPlayer = playerInstance.GetComponent<NetworkPlayer>();
            
            if(netPlayer != null) {
                // Set player name (e.g., from Steam or connection ID)
                if (steamNetworkIntegration != null && steamNetworkIntegration.IsSteamInitialized)
                {
                    // This requires a way to map NetworkConnection to CSteamID if you want specific Steam names for remote players.
                    // For now, using a generic name.
                    string steamName = "Player";
                    // Since we're no longer spawning for host, this check is less relevant but kept for safety
                    if (conn == fishNetManager.ClientManager.Connection)
                    {
                         steamName = steamNetworkIntegration.GetPlayerName();
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
                Debug.Log($"Spawned NetworkPet (ID: {petInstance.ObjectId}, OwnerId: {petInstance.OwnerId}, HasOwner: {petInstance.Owner != null}) for client {conn.ClientId}");
                NetworkPet netPet = petInstance.GetComponent<NetworkPet>();
                if(netPet != null && netPlayer != null)
                {
                    netPet.SetOwnerPlayer(netPlayer);
                    
                    // Establish relationship between player and pet
                    RelationshipManager.SetupPlayerPetRelationship(netPlayer, netPet);
                    
                    // Verify pet-player relationship
                    Debug.Log($"Connected pet (ID: {petInstance.ObjectId}) to player (ID: {playerInstance.ObjectId}). Pet.OwnerPlayerObjectId={netPet.OwnerPlayerObjectId.Value}");
                }
            }

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