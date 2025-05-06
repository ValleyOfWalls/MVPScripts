using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing.Server;
using FishNet.Managing;
using System.Collections.Generic;

// This class is not a MonoBehaviour, it's managed by SteamAndLobbyHandler
public class PlayerSpawner
{
    private SteamAndLobbyHandler steamAndLobbyHandlerInstance;
    private NetworkObject networkPlayerPrefab;
    private NetworkObject networkPetPrefab;
    private NetworkManager fishNetManager; // Store reference to FishNet's NetworkManager

    public PlayerSpawner(SteamAndLobbyHandler handler)
    {
        steamAndLobbyHandlerInstance = handler;
        fishNetManager = FishNet.InstanceFinder.NetworkManager; // Get the main NetworkManager instance

        if (steamAndLobbyHandlerInstance != null) 
        {
            networkPlayerPrefab = steamAndLobbyHandlerInstance.playerPrefab; 
            networkPetPrefab = steamAndLobbyHandlerInstance.petPrefab;
        }
        else
        {
            Debug.LogError("PlayerSpawner: SteamAndLobbyHandler instance is null during construction.");
        }

        if (networkPlayerPrefab == null)
        {
            Debug.LogWarning("PlayerSpawner: NetworkPlayer prefab is not assigned on SteamAndLobbyHandler.");
        }
        if (networkPetPrefab == null)
        {
            Debug.LogWarning("PlayerSpawner: NetworkPet prefab is not assigned on SteamAndLobbyHandler.");
        }
        if (fishNetManager == null)
        {
            Debug.LogError("PlayerSpawner: FishNet NetworkManager instance not found.");
        }
    }

    public void SpawnPlayerForConnection(NetworkConnection conn)
    {
        if (steamAndLobbyHandlerInstance == null || !steamAndLobbyHandlerInstance.IsServerStarted)
        {
            Debug.LogError("PlayerSpawner can only be used by the server-side of SteamAndLobbyHandler.");
            return;
        }
        if (fishNetManager == null)
        {
            Debug.LogError("PlayerSpawner: FishNet NetworkManager is null. Cannot spawn.");
            return;
        }

        if (networkPlayerPrefab == null) 
        {
            Debug.LogError("NetworkPlayer prefab is not assigned in PlayerSpawner. Ensure it is set on the SteamAndLobbyHandler and assigned in its Inspector.");
            return; 
        }
        if (networkPetPrefab == null) 
        {
            Debug.LogError("NetworkPet prefab is not assigned in PlayerSpawner. Ensure it is set on the SteamAndLobbyHandler and assigned in its Inspector.");
            return; 
        }

        GameObject playerGameObjectInstance = Object.Instantiate(networkPlayerPrefab.gameObject);
        fishNetManager.ServerManager.Spawn(playerGameObjectInstance, conn);
        NetworkObject playerInstance = playerGameObjectInstance.GetComponent<NetworkObject>();

        if (playerInstance != null)
        {
            Debug.Log($"Spawned NetworkPlayer for client {conn.ClientId}");
            steamAndLobbyHandlerInstance.RegisterNetworkPlayer(playerInstance);
            NetworkPlayer netPlayer = playerInstance.GetComponent<NetworkPlayer>();
            if(netPlayer != null) {
                // netPlayer.PlayerName = SteamFriends.GetFriendPersonaName(Steamworks.SteamUser.GetSteamIDForConnection(conn)); // This needs Steamworks to be set up for connection user data
                // For now, can set from ClientId or a default
                netPlayer.CmdSetPlayerName("Player " + conn.ClientId); 
            }

            GameObject petGameObjectInstance = Object.Instantiate(networkPetPrefab.gameObject);
            fishNetManager.ServerManager.Spawn(petGameObjectInstance, conn);
            NetworkObject petInstance = petGameObjectInstance.GetComponent<NetworkObject>();

            if (petInstance != null)
            {
                Debug.Log($"Spawned NetworkPet for client {conn.ClientId}");
                steamAndLobbyHandlerInstance.RegisterNetworkPet(petInstance);
                NetworkPet netPet = petInstance.GetComponent<NetworkPet>();
                if(netPet != null && netPlayer != null)
                {
                    netPet.SetOwnerPlayer(netPlayer); // Link pet to player
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
        if (steamAndLobbyHandlerInstance == null || !steamAndLobbyHandlerInstance.IsServerStarted || fishNetManager == null)
        {
            return;
        }

        // FishNet typically handles despawning objects owned by the connection.
        // We just need to unregister them from our handler's lists.
        NetworkObject playerToUnregister = null;
        foreach (var playerObj in steamAndLobbyHandlerInstance.networkPlayers)
        {
            if (playerObj.Owner == conn)
            {
                playerToUnregister = playerObj;
                break;
            }
        }
        if (playerToUnregister != null) steamAndLobbyHandlerInstance.UnregisterNetworkPlayer(playerToUnregister);
        
        // Similarly for pets, if they are directly owned by the connection.
        // If pets are owned by the player object, FishNet handles their despawn when player is despawned.
        List<NetworkObject> petsToUnregister = new List<NetworkObject>();
        foreach (var petObj in steamAndLobbyHandlerInstance.networkPets)
        {
            if (petObj.Owner == conn) 
            {
                petsToUnregister.Add(petObj);
            }
        }
        foreach (var petObj in petsToUnregister)
        {
             steamAndLobbyHandlerInstance.UnregisterNetworkPet(petObj);
        }
        // Actual despawning is usually handled by FishNet automatically when a client disconnects
        // for objects spawned with that client as the owner.
    }
} 