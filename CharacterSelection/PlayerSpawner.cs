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
    [SerializeField] private NetworkObject playerStatsUIPrefab;
    [SerializeField] private NetworkObject petStatsUIPrefab;

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
            playerStatsUIPrefab = steamNetworkIntegration.NetworkEntityPlayerStatsUIPrefab?.GetComponent<NetworkObject>();
            petStatsUIPrefab = steamNetworkIntegration.NetworkEntityPetStatsUIPrefab?.GetComponent<NetworkObject>();
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

        if (playerStatsUIPrefab != null)
        {
            var playerStatsUIEntity = playerStatsUIPrefab.GetComponent<NetworkEntity>();
            if (playerStatsUIEntity == null)
                Debug.LogError("PlayerSpawner: Player stats UI prefab is missing NetworkEntity component");
            else if (playerStatsUIEntity.EntityType != EntityType.PlayerStatsUI)
                Debug.LogError("PlayerSpawner: Player stats UI prefab's NetworkEntity type is not set to PlayerStatsUI");
        }
        else
            Debug.LogError("PlayerSpawner: Player stats UI prefab is not assigned");

        if (petStatsUIPrefab != null)
        {
            var petStatsUIEntity = petStatsUIPrefab.GetComponent<NetworkEntity>();
            if (petStatsUIEntity == null)
                Debug.LogError("PlayerSpawner: Pet stats UI prefab is missing NetworkEntity component");
            else if (petStatsUIEntity.EntityType != EntityType.PetStatsUI)
                Debug.LogError("PlayerSpawner: Pet stats UI prefab's NetworkEntity type is not set to PetStatsUI");
        }
        else
            Debug.LogError("PlayerSpawner: Pet stats UI prefab is not assigned");
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
        
        // Set character selection data for model loading
        SetCharacterSelectionData(playerEntity, characterData, playerName);

        // Spawn player hand entity
        NetworkEntity playerHandEntity = SpawnEntity(playerHandPrefab, conn);
        if (playerHandEntity == null)
        {
            Debug.LogError($"PlayerSpawner: Failed to spawn player hand entity for client {conn.ClientId}");
            return null;
        }

        // Spawn player stats UI entity
        NetworkEntity playerStatsUIEntity = SpawnEntity(playerStatsUIPrefab, conn);
        if (playerStatsUIEntity == null)
        {
            Debug.LogError($"PlayerSpawner: Failed to spawn player stats UI entity for client {conn.ClientId}");
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
        
        // Set pet selection data for model loading
        SetPetSelectionData(petEntity, petData, petName);

        // Spawn pet hand entity
        NetworkEntity petHandEntity = SpawnEntity(petHandPrefab, conn);
        if (petHandEntity == null)
        {
            Debug.LogError($"PlayerSpawner: Failed to spawn pet hand entity for client {conn.ClientId}");
            return null;
        }

        // Spawn pet stats UI entity
        NetworkEntity petStatsUIEntity = SpawnEntity(petStatsUIPrefab, conn);
        if (petStatsUIEntity == null)
        {
            Debug.LogError($"PlayerSpawner: Failed to spawn pet stats UI entity for client {conn.ClientId}");
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

        // Set up stats UI relationships
        SetupStatsUIRelationships(playerEntity, playerStatsUIEntity, petEntity, petStatsUIEntity);

        // Set up starting decks
        SetupStartingDecks(playerEntity, playerHandEntity, petEntity, petHandEntity, characterData, petData);

        // Return spawned entities data
        SpawnedEntitiesData entitiesData = new SpawnedEntitiesData
        {
            playerEntity = playerEntity,
            petEntity = petEntity,
            playerHandEntity = playerHandEntity,
            petHandEntity = petHandEntity,
            playerStatsUIEntity = playerStatsUIEntity,
            petStatsUIEntity = petStatsUIEntity,
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

    /// <summary>
    /// Sets character selection data on the player entity for dynamic model loading
    /// </summary>
    private void SetCharacterSelectionData(NetworkEntity playerEntity, CharacterData characterData, string playerName)
    {
        if (playerEntity == null || characterData == null) return;
        
        // Find the character index in the selection manager
        CharacterSelectionManager selectionManager = FindFirstObjectByType<CharacterSelectionManager>();
        if (selectionManager == null)
        {
            Debug.LogError("PlayerSpawner: CharacterSelectionManager not found, cannot set character selection data");
            return;
        }
        
        var availableCharacters = selectionManager.GetAvailableCharacters();
        int characterIndex = -1;
        
        for (int i = 0; i < availableCharacters.Count; i++)
        {
            if (availableCharacters[i] == characterData)
            {
                characterIndex = i;
                break;
            }
        }
        
        if (characterIndex == -1)
        {
            Debug.LogError($"PlayerSpawner: Character '{characterData.CharacterName}' not found in available characters");
            return;
        }
        
        // Get the prefab path from the character data's game object
        string prefabPath = characterData.gameObject.name; // This will be used to identify the prefab
        
        // Set the selection data
        playerEntity.SetCharacterSelection(characterIndex, prefabPath);
        
        Debug.Log($"PlayerSpawner: Set character selection data for '{playerName}' - Index: {characterIndex}, Prefab: {prefabPath}");
    }
    
    /// <summary>
    /// Sets pet selection data on the pet entity for dynamic model loading
    /// </summary>
    private void SetPetSelectionData(NetworkEntity petEntity, PetData petData, string petName)
    {
        if (petEntity == null || petData == null) return;
        
        // Find the pet index in the selection manager
        CharacterSelectionManager selectionManager = FindFirstObjectByType<CharacterSelectionManager>();
        if (selectionManager == null)
        {
            Debug.LogError("PlayerSpawner: CharacterSelectionManager not found, cannot set pet selection data");
            return;
        }
        
        var availablePets = selectionManager.GetAvailablePets();
        int petIndex = -1;
        
        for (int i = 0; i < availablePets.Count; i++)
        {
            if (availablePets[i] == petData)
            {
                petIndex = i;
                break;
            }
        }
        
        if (petIndex == -1)
        {
            Debug.LogError($"PlayerSpawner: Pet '{petData.PetName}' not found in available pets");
            return;
        }
        
        // Get the prefab path from the pet data's game object
        string prefabPath = petData.gameObject.name; // This will be used to identify the prefab
        
        // Set the selection data
        petEntity.SetPetSelection(petIndex, prefabPath);
        
        Debug.Log($"PlayerSpawner: Set pet selection data for '{petName}' - Index: {petIndex}, Prefab: {prefabPath}");
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

    /// <summary>
    /// Sets up the relationships between main entities and their stats UI entities
    /// </summary>
    private void SetupStatsUIRelationships(NetworkEntity player, NetworkEntity playerStatsUI, NetworkEntity pet, NetworkEntity petStatsUI)
    {
        if (player == null || playerStatsUI == null || pet == null || petStatsUI == null)
        {
            Debug.LogError("SetupStatsUIRelationships: One or more entities are null");
            return;
        }

        if (player.EntityType != EntityType.Player || playerStatsUI.EntityType != EntityType.PlayerStatsUI || 
            pet.EntityType != EntityType.Pet || petStatsUI.EntityType != EntityType.PetStatsUI)
        {
            Debug.LogError("SetupStatsUIRelationships: Invalid entity types provided");
            return;
        }

        // Get the RelationshipManager components for main entities
        var playerRelationship = player.GetComponent<RelationshipManager>();
        var petRelationship = pet.GetComponent<RelationshipManager>();

        // Get the RelationshipManager components for stats UI entities
        var playerStatsUIRelationship = playerStatsUI.GetComponent<RelationshipManager>();
        var petStatsUIRelationship = petStatsUI.GetComponent<RelationshipManager>();

        if (playerRelationship != null && petRelationship != null && 
            playerStatsUIRelationship != null && petStatsUIRelationship != null)
        {
            // Link main entities to their stats UI
            playerRelationship.SetStatsUI(playerStatsUI);
            petRelationship.SetStatsUI(petStatsUI);
            
            // Link stats UI back to their main entities (use ally relationship)
            playerStatsUIRelationship.SetAlly(player);
            petStatsUIRelationship.SetAlly(pet);
            
            // Set stats UI entity names to reflect their purpose
            playerStatsUI.EntityName.Value = $"Player Stats UI ({player.Owner?.ClientId ?? -1})";
            petStatsUI.EntityName.Value = $"Pet Stats UI ({player.Owner?.ClientId ?? -1})";
            
            Debug.Log($"Set up stats UI relationships - Player (ID: {player.ObjectId}) -> Stats UI (ID: {playerStatsUI.ObjectId}), Pet (ID: {pet.ObjectId}) -> Stats UI (ID: {petStatsUI.ObjectId})");
        }
        else
        {
            Debug.LogError("SetupStatsUIRelationships: Missing RelationshipManager components");
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
    public NetworkEntity playerStatsUIEntity;
    public NetworkEntity petStatsUIEntity;
    public CharacterData characterData;
    public PetData petData;
} 