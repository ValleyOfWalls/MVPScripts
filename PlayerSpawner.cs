using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing.Server;
using FishNet.Managing;
using System.Collections.Generic;

// This class is not a MonoBehaviour, it's managed by SteamNetworkIntegration
public class PlayerSpawner
{
    private SteamNetworkIntegration steamNetworkIntegrationInstance;
    private NetworkObject networkPlayerPrefab;
    private NetworkObject networkPetPrefab;
    private NetworkManager fishNetManager; // Store reference to FishNet's NetworkManager

    public PlayerSpawner(SteamNetworkIntegration handler)
    {
        steamNetworkIntegrationInstance = handler;
        fishNetManager = FishNet.InstanceFinder.NetworkManager; // Get the main NetworkManager instance

        if (steamNetworkIntegrationInstance != null) 
        {
            networkPlayerPrefab = steamNetworkIntegrationInstance.NetworkPlayerPrefab?.GetComponent<NetworkObject>();
            networkPetPrefab = steamNetworkIntegrationInstance.NetworkPetPrefab?.GetComponent<NetworkObject>();
        }
        else
        {
            Debug.LogError("PlayerSpawner: SteamNetworkIntegration instance is null during construction.");
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
            Debug.LogError("PlayerSpawner: FishNet NetworkManager instance not found.");
        }
    }

    public void SpawnPlayerForConnection(NetworkConnection conn)
    {
        if (steamNetworkIntegrationInstance == null || !steamNetworkIntegrationInstance.IsUserSteamHost)
        {
            Debug.LogError("PlayerSpawner can only be used by the host.");
            return;
        }
        if (fishNetManager == null)
        {
            Debug.LogError("PlayerSpawner: FishNet NetworkManager is null. Cannot spawn.");
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

        GameObject playerGameObjectInstance = Object.Instantiate(networkPlayerPrefab.gameObject);
        fishNetManager.ServerManager.Spawn(playerGameObjectInstance, conn);
        NetworkObject playerInstance = playerGameObjectInstance.GetComponent<NetworkObject>();

        if (playerInstance != null)
        {
            Debug.Log($"Spawned NetworkPlayer for client {conn.ClientId}");
            NetworkPlayer netPlayer = playerInstance.GetComponent<NetworkPlayer>();
            if(netPlayer != null) {
                netPlayer.PlayerName.Value = "Player " + conn.ClientId;
            }

            GameObject petGameObjectInstance = Object.Instantiate(networkPetPrefab.gameObject);
            fishNetManager.ServerManager.Spawn(petGameObjectInstance, conn);
            NetworkObject petInstance = petGameObjectInstance.GetComponent<NetworkObject>();

            if (petInstance != null)
            {
                Debug.Log($"Spawned NetworkPet for client {conn.ClientId}");
                NetworkPet netPet = petInstance.GetComponent<NetworkPet>();
                if(netPet != null && netPlayer != null)
                {
                    netPet.SetOwnerPlayer(netPlayer);
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
        if (steamNetworkIntegrationInstance == null || !steamNetworkIntegrationInstance.IsUserSteamHost || fishNetManager == null)
        {
            return;
        }
    }
} 