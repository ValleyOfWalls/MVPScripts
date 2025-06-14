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
    [SerializeField] private NetworkObject playerHandPrefab;
    [SerializeField] private NetworkObject petHandPrefab;

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
            playerHandPrefab = steamNetworkIntegration.NetworkEntityPlayerHandPrefab?.GetComponent<NetworkObject>();
            petHandPrefab = steamNetworkIntegration.NetworkEntityPetHandPrefab?.GetComponent<NetworkObject>();
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

        if (playerHandPrefab != null)
        {
            var playerHandEntity = playerHandPrefab.GetComponent<NetworkEntity>();
            if (playerHandEntity == null)
                Debug.LogError("PlayerSpawner: Player hand prefab is missing NetworkEntity component");
            else if (playerHandEntity.EntityType != EntityType.PlayerHand)
                Debug.LogError("PlayerSpawner: Player hand prefab's NetworkEntity type is not set to PlayerHand");
        }
        else
            Debug.LogError("PlayerSpawner: Player hand prefab is not assigned");

        if (petHandPrefab != null)
        {
            var petHandEntity = petHandPrefab.GetComponent<NetworkEntity>();
            if (petHandEntity == null)
                Debug.LogError("PlayerSpawner: Pet hand prefab is missing NetworkEntity component");
            else if (petHandEntity.EntityType != EntityType.PetHand)
                Debug.LogError("PlayerSpawner: Pet hand prefab's NetworkEntity type is not set to PetHand");
        }
        else
            Debug.LogError("PlayerSpawner: Pet hand prefab is not assigned");
    }

    public void SpawnPlayerForConnection(NetworkConnection conn)
    {
        // Entity spawning is now handled during character selection phase
        // This method is kept for backwards compatibility but no longer spawns entities
        Debug.Log($"PlayerSpawner: Connection registered for client {conn.ClientId}. Entities will be spawned during character selection phase.");
    }

    /// <summary>
    /// Spawns entities for a player based on their character selection data
    /// </summary>
    public SpawnedEntitiesData SpawnPlayerWithSelection(NetworkConnection conn, CharacterData characterData, PetData petData, string playerName, string petName)
    {
        if (characterData == null || petData == null)
        {
            Debug.LogError($"PlayerSpawner: Cannot spawn entities for client {conn.ClientId} - characterData or petData is null");
            return null;
        }

        Debug.Log($"PlayerSpawner: Spawning entities for client {conn.ClientId} with character '{characterData.CharacterName}' and pet '{petData.PetName}'");

        // Use generic prefabs - character/pet data provides visual configuration instead of separate prefabs
        NetworkObject characterPrefabToSpawn = playerPrefab;
        NetworkObject petPrefabToSpawn = petPrefab;

        // Spawn player entity with character data
        NetworkEntity playerEntity = SpawnEntity(characterPrefabToSpawn, conn);
        if (playerEntity == null)
        {
            Debug.LogError($"PlayerSpawner: Failed to spawn player entity for client {conn.ClientId}");
            return null;
        }

        // Apply character data to player entity
        ApplyCharacterDataToEntity(playerEntity, characterData, playerName);

        // Spawn player hand entity
        NetworkEntity playerHandEntity = SpawnEntity(playerHandPrefab, conn);
        if (playerHandEntity == null)
        {
            Debug.LogError($"PlayerSpawner: Failed to spawn player hand entity for client {conn.ClientId}");
            return null;
        }

        // Spawn pet entity with pet data
        NetworkEntity petEntity = SpawnEntity(petPrefabToSpawn, conn);
        if (petEntity == null)
        {
            Debug.LogError($"PlayerSpawner: Failed to spawn pet entity for client {conn.ClientId}");
            return null;
        }

        // Apply pet data to pet entity
        ApplyPetDataToEntity(petEntity, petData, petName);

        // Spawn pet hand entity
        NetworkEntity petHandEntity = SpawnEntity(petHandPrefab, conn);
        if (petHandEntity == null)
        {
            Debug.LogError($"PlayerSpawner: Failed to spawn pet hand entity for client {conn.ClientId}");
            return null;
        }

        // Set up pet-player relationship
        if (petEntity != null && playerEntity != null)
        {
            petEntity.SetOwnerEntity(playerEntity);
            SetupPlayerPetRelationship(playerEntity, petEntity);
            Debug.Log($"Connected pet (ID: {petEntity.ObjectId}) to player (ID: {playerEntity.ObjectId})");
        }

        // Set up hand relationships
        SetupHandRelationships(playerEntity, playerHandEntity, petEntity, petHandEntity);

        // Set up starting decks
        SetupStartingDecks(playerEntity, playerHandEntity, petEntity, petHandEntity, characterData, petData);

        // Return spawned entities data
        SpawnedEntitiesData entitiesData = new SpawnedEntitiesData
        {
            playerEntity = playerEntity,
            petEntity = petEntity,
            playerHandEntity = playerHandEntity,
            petHandEntity = petHandEntity,
            characterData = characterData,
            petData = petData
        };

        Debug.Log($"PlayerSpawner: Successfully spawned and configured all entities for client {conn.ClientId}");
        return entitiesData;
    }

    /// <summary>
    /// Applies character data to a spawned player entity
    /// </summary>
    private void ApplyCharacterDataToEntity(NetworkEntity playerEntity, CharacterData characterData, string customName)
    {
        if (playerEntity == null || characterData == null) return;

        // Set entity name
        string entityName = !string.IsNullOrEmpty(customName) ? customName : characterData.CharacterName;
        playerEntity.EntityName.Value = entityName;

        // Apply base stats using reflection to set NetworkEntity's private fields
        SetEntityBaseStats(playerEntity.gameObject, characterData.BaseHealth, characterData.BaseEnergy, characterData.StartingCurrency);

        // Apply visual configuration
        ConfigureEntityVisuals(playerEntity.gameObject, characterData.CharacterMaterial, characterData.CharacterMesh, 
                              characterData.CharacterTint, characterData.CharacterAnimatorController);

        // Set up deck with character's starter deck
        EntityDeckSetup deckSetup = playerEntity.GetComponent<EntityDeckSetup>();
        if (deckSetup != null && characterData.StarterDeck != null)
        {
            deckSetup.SetRuntimeDeckData(characterData.StarterDeck);
            Debug.Log($"PlayerSpawner: Set player deck to '{characterData.StarterDeck.DeckName}' with {characterData.StarterDeck.CardsInDeck?.Count ?? 0} cards");
        }

        Debug.Log($"PlayerSpawner: Applied character data '{characterData.CharacterName}' to player entity");
    }

    /// <summary>
    /// Applies pet data to a spawned pet entity
    /// </summary>
    private void ApplyPetDataToEntity(NetworkEntity petEntity, PetData petData, string customName)
    {
        if (petEntity == null || petData == null) return;

        // Set entity name
        string entityName = !string.IsNullOrEmpty(customName) ? customName : petData.PetName;
        petEntity.EntityName.Value = entityName;

        // Apply base stats using reflection to set NetworkEntity's private fields
        SetEntityBaseStats(petEntity.gameObject, petData.BaseHealth, petData.BaseEnergy, 0); // Pets don't have currency

        // Apply visual configuration
        ConfigureEntityVisuals(petEntity.gameObject, petData.PetMaterial, petData.PetMesh, 
                              petData.PetTint, petData.PetAnimatorController);

        // Set up deck with pet's starter deck
        EntityDeckSetup deckSetup = petEntity.GetComponent<EntityDeckSetup>();
        if (deckSetup != null && petData.StarterDeck != null)
        {
            deckSetup.SetRuntimeDeckData(petData.StarterDeck);
            Debug.Log($"PlayerSpawner: Set pet deck to '{petData.StarterDeck.DeckName}' with {petData.StarterDeck.CardsInDeck?.Count ?? 0} cards");
        }

        Debug.Log($"PlayerSpawner: Applied pet data '{petData.PetName}' to pet entity");
    }

    /// <summary>
    /// Sets up starting decks for the spawned entities
    /// NOTE: This is now handled in ApplyCharacterDataToEntity and ApplyPetDataToEntity via EntityDeckSetup
    /// </summary>
    private void SetupStartingDecks(NetworkEntity playerEntity, NetworkEntity playerHandEntity, NetworkEntity petEntity, NetworkEntity petHandEntity, CharacterData characterData, PetData petData)
    {
        Debug.Log($"PlayerSpawner: Starting deck setup handled by EntityDeckSetup components on main entities");
    }

    /// <summary>
    /// Configures the visual appearance of an entity
    /// </summary>
    private void ConfigureEntityVisuals(GameObject entityObj, Material material, Mesh mesh, Color tint, RuntimeAnimatorController animatorController)
    {
        // Configure mesh renderer
        MeshRenderer meshRenderer = entityObj.GetComponent<MeshRenderer>();
        MeshFilter meshFilter = entityObj.GetComponent<MeshFilter>();
        
        if (meshRenderer != null && material != null)
        {
            meshRenderer.material = material;
            meshRenderer.material.color = tint;
        }
        
        if (meshFilter != null && mesh != null)
        {
            meshFilter.mesh = mesh;
        }

        // Configure animator
        Animator animator = entityObj.GetComponent<Animator>();
        if (animator != null && animatorController != null)
        {
            animator.runtimeAnimatorController = animatorController;
        }

        Debug.Log($"PlayerSpawner: Configured visual appearance for {entityObj.name}");
    }

    /// <summary>
    /// Sets the base stats for an entity (will be applied during NetworkEntity initialization)
    /// </summary>
    private void SetEntityBaseStats(GameObject entityObj, int baseHealth, int baseEnergy, int startingCurrency)
    {
        // Use reflection to set private serialized fields that NetworkEntity reads during initialization
        NetworkEntity networkEntity = entityObj.GetComponent<NetworkEntity>();
        if (networkEntity != null)
        {
            // Set the serialized fields directly
            var type = typeof(NetworkEntity);
            
            var healthField = type.GetField("_maxHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (healthField != null) healthField.SetValue(networkEntity, baseHealth);
            
            var energyField = type.GetField("_maxEnergy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (energyField != null) energyField.SetValue(networkEntity, baseEnergy);
            
            var currencyField = type.GetField("_currency", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (currencyField != null) currencyField.SetValue(networkEntity, startingCurrency);
            
            // Also set current values
            var currentHealthField = type.GetField("_currentHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (currentHealthField != null) currentHealthField.SetValue(networkEntity, baseHealth);
            
            var currentEnergyField = type.GetField("_currentEnergy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (currentEnergyField != null) currentEnergyField.SetValue(networkEntity, baseEnergy);

            Debug.Log($"PlayerSpawner: Set base stats - Health: {baseHealth}, Energy: {baseEnergy}, Currency: {startingCurrency}");
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

    private void SetupHandRelationships(NetworkEntity player, NetworkEntity playerHand, NetworkEntity pet, NetworkEntity petHand)
    {
        if (player == null || playerHand == null || pet == null || petHand == null) return;
        if (player.EntityType != EntityType.Player || playerHand.EntityType != EntityType.PlayerHand || pet.EntityType != EntityType.Pet || petHand.EntityType != EntityType.PetHand)
        {
            Debug.LogError("SetupHandRelationships: Invalid entity types provided");
            return;
        }

        // Get the RelationshipManager components
        var playerRelationship = player.GetComponent<RelationshipManager>();
        var petRelationship = pet.GetComponent<RelationshipManager>();

        if (playerRelationship != null && petRelationship != null)
        {
            // Set up hand relationships
            playerRelationship.SetHand(playerHand);
            petRelationship.SetHand(petHand);
            
            Debug.Log($"Set up hand relationships - Player (ID: {player.ObjectId}) -> Hand (ID: {playerHand.ObjectId}), Pet (ID: {pet.ObjectId}) -> Hand (ID: {petHand.ObjectId})");
        }
        else
        {
            Debug.LogError("SetupHandRelationships: Missing RelationshipManager components");
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

/// <summary>
/// Data structure to hold information about spawned entities
/// </summary>
[System.Serializable]
public class SpawnedEntitiesData
{
    public NetworkEntity playerEntity;
    public NetworkEntity petEntity;
    public NetworkEntity playerHandEntity;
    public NetworkEntity petHandEntity;
    public CharacterData characterData;
    public PetData petData;
} 