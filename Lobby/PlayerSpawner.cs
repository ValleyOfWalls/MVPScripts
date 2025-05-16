using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing.Server;
using FishNet.Managing;
using System.Collections.Generic;

/// <summary>
/// Handles the spawning of NetworkEntity prefabs (players and pets) for connected clients.
/// Attach to: The same GameObject as SteamNetworkIntegration to handle entity spawning when connections are established.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [Header("Entity Prefabs")]
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private NetworkObject petPrefab;

    private NetworkManager fishNetManager;
    private SteamNetworkIntegration steamNetworkIntegration;

    private void Awake()
    {
        steamNetworkIntegration = GetComponent<SteamNetworkIntegration>();
        fishNetManager = FishNet.InstanceFinder.NetworkManager;

        if (steamNetworkIntegration != null) 
        {
            playerPrefab = steamNetworkIntegration.NetworkEntityPlayerPrefab?.GetComponent<NetworkObject>();
            petPrefab = steamNetworkIntegration.NetworkEntityPetPrefab?.GetComponent<NetworkObject>();
        }

        ValidatePrefabs();
    }

    private void ValidatePrefabs()
    {
        if (playerPrefab != null)
        {
            var playerEntity = playerPrefab.GetComponent<NetworkEntity>();
            if (playerEntity == null)
                Debug.LogError("PlayerSpawner: Player prefab is missing NetworkEntity component");
            else if (playerEntity.EntityType != EntityType.Player)
                Debug.LogError("PlayerSpawner: Player prefab's NetworkEntity type is not set to Player");
        }
        else
            Debug.LogError("PlayerSpawner: Player prefab is not assigned");

        if (petPrefab != null)
        {
            var petEntity = petPrefab.GetComponent<NetworkEntity>();
            if (petEntity == null)
                Debug.LogError("PlayerSpawner: Pet prefab is missing NetworkEntity component");
            else if (petEntity.EntityType != EntityType.Pet)
                Debug.LogError("PlayerSpawner: Pet prefab's NetworkEntity type is not set to Pet");
        }
        else
            Debug.LogError("PlayerSpawner: Pet prefab is not assigned");
    }

    public void SpawnPlayerForConnection(NetworkConnection conn)
    {
        // Log connection info for debugging
        bool isHostConnection = fishNetManager.IsHostStarted && conn.ClientId == 0;
        Debug.Log($"PlayerSpawner: Attempting to spawn entities for client {conn.ClientId}. IsHost: {isHostConnection}");

        // Spawn player entity
        NetworkEntity playerEntity = SpawnEntity(playerPrefab, conn);
        if (playerEntity == null)
        {
            Debug.LogError($"PlayerSpawner: Failed to spawn player entity for client {conn.ClientId}");
            return;
        }

        // Set player name
        SetEntityName(playerEntity, conn);

        // Spawn pet entity
        NetworkEntity petEntity = SpawnEntity(petPrefab, conn);
        if (petEntity == null)
        {
            Debug.LogError($"PlayerSpawner: Failed to spawn pet entity for client {conn.ClientId}");
            return;
        }

        // Set up pet-player relationship
        if (petEntity != null && playerEntity != null)
        {
            petEntity.SetOwnerEntity(playerEntity);
            SetupPlayerPetRelationship(playerEntity, petEntity);
            Debug.Log($"Connected pet (ID: {petEntity.ObjectId}) to player (ID: {playerEntity.ObjectId})");
        }
    }

    private NetworkEntity SpawnEntity(NetworkObject prefab, NetworkConnection conn)
    {
        if (prefab == null) return null;

        GameObject instance = Object.Instantiate(prefab.gameObject);
        fishNetManager.ServerManager.Spawn(instance, conn);

        NetworkEntity entity = instance.GetComponent<NetworkEntity>();
        if (entity != null)
        {
            Debug.Log($"Spawned {entity.EntityType} (ID: {entity.ObjectId}, OwnerId: {entity.Owner?.ClientId}) for client {conn.ClientId}");
            return entity;
        }

        Debug.LogError($"PlayerSpawner: Spawned instance missing NetworkEntity component");
        return null;
    }

    private void SetEntityName(NetworkEntity entity, NetworkConnection conn)
    {
        if (entity == null || entity.EntityType != EntityType.Player) return;

        string entityName;
        if (steamNetworkIntegration != null && steamNetworkIntegration.IsSteamInitialized)
        {
            string steamName = "Player";
            if (conn == fishNetManager.ClientManager.Connection)
            {
                steamName = steamNetworkIntegration.GetPlayerName();
            }
            entityName = $"{steamName} ({conn.ClientId})";
        }
        else
        {
            entityName = $"Player {conn.ClientId}";
        }

        entity.EntityName.Value = entityName;
    }

    private void SetupPlayerPetRelationship(NetworkEntity player, NetworkEntity pet)
    {
        if (player == null || pet == null) return;
        if (player.EntityType != EntityType.Player || pet.EntityType != EntityType.Pet)
        {
            Debug.LogError("SetupPlayerPetRelationship: Invalid entity types provided");
            return;
        }

        // Get the RelationshipManager components
        var playerRelationship = player.GetComponent<RelationshipManager>();
        var petRelationship = pet.GetComponent<RelationshipManager>();

        if (playerRelationship != null && petRelationship != null)
        {
            // Set up the relationship
            playerRelationship.SetAlly(pet);
            petRelationship.SetAlly(player);
        }
        else
        {
            Debug.LogError("SetupPlayerPetRelationship: Missing RelationshipManager components");
        }
    }

    public void DespawnEntitiesForConnection(NetworkConnection conn)
    {
        if (fishNetManager == null || !fishNetManager.ServerManager.Started)
        {
            Debug.LogWarning("PlayerSpawner: Cannot despawn. FishNet ServerManager is not started or NetworkManager is null.");
            return;
        }
        // FishNet handles despawning objects owned by the connection automatically
        Debug.Log($"PlayerSpawner: DespawnEntitiesForConnection called for {conn.ClientId}. FishNet will handle owned object despawn.");
    }
} 