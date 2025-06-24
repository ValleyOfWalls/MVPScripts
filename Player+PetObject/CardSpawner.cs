using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Handles spawning and despawning of card GameObjects.
/// Can be used for both owned cards (player/pet decks) and unowned cards (draft packs).
/// 
/// Configuration:
/// - For Player/Pet decks: Attach to NetworkEntity prefabs, keep requiresNetworkEntity = true
/// - For Draft packs: Attach to DraftPack prefabs, set requiresNetworkEntity = false
/// 
/// Usage:
/// - SpawnCard(): Creates cards owned by the spawner's NetworkEntity (for player/pet decks)
/// - SpawnUnownedCard(): Creates unowned cards (for draft packs)
/// 
/// Attach to: NetworkEntity prefabs alongside HandManager, or DraftPack prefabs.
/// </summary>
public class CardSpawner : NetworkBehaviour
{
    [Header("Card Prefab")]
    [SerializeField] private GameObject cardPrefab;

    [Header("Optional Settings")]
    [SerializeField] private Vector3 defaultSpawnOffset = new Vector3(0, 0, -0.1f);
    
    [Header("Spawner Type")]
    [SerializeField] private bool requiresNetworkEntity = true;

    private NetworkEntity _spawnerNetworkEntity;

    private void Awake()
    {
        _spawnerNetworkEntity = GetComponent<NetworkEntity>();
        
        // Only require NetworkEntity if explicitly configured to do so
        if (requiresNetworkEntity && _spawnerNetworkEntity == null)
        {
            Debug.LogError($"CardSpawner on {gameObject.name}: Missing NetworkEntity component on the spawner itself!");
        }

        if (cardPrefab == null)
        {
            Debug.LogError($"CardSpawner on {gameObject.name}: Card prefab is not assigned!");
        }
        else
        {
            // Validate card prefab has required components
            if (cardPrefab.GetComponent<Card>() == null)
                Debug.LogError($"CardSpawner on {gameObject.name}: Card prefab is missing Card component!");
            if (cardPrefab.GetComponent<NetworkObject>() == null)
                Debug.LogError($"CardSpawner on {gameObject.name}: Card prefab is missing NetworkObject component!");
        }
    }

    /// <summary>
    /// Public accessor for the card prefab (for deck viewing and other local card creation)
    /// </summary>
    public GameObject CardPrefab => cardPrefab;

    /// <summary>
    /// Spawns a card with the given card data and assigns ownership to the spawner's NetworkEntity
    /// </summary>
    /// <param name="cardData">The data for the card to spawn</param>
    /// <returns>The spawned card GameObject, or null if spawn failed</returns>
    [Server]
    public GameObject SpawnCard(CardData cardData)
    {
        if (!IsServerInitialized || cardPrefab == null || cardData == null)
        {
            Debug.LogError($"CardSpawner on {gameObject.name}: Cannot spawn card - missing required components or data");
            return null;
        }

        if (_spawnerNetworkEntity == null)
        {
            Debug.LogError($"CardSpawner on {gameObject.name}: Cannot spawn owned card - Spawner's NetworkEntity is missing. Use SpawnUnownedCard for draft cards.");
            return null;
        }

        return SpawnCardInternal(cardData, _spawnerNetworkEntity);
    }
    
    /// <summary>
    /// Spawns an unowned card with the given card data (for draft packs)
    /// </summary>
    /// <param name="cardData">The data for the card to spawn</param>
    /// <returns>The spawned card GameObject, or null if spawn failed</returns>
    [Server]
    public GameObject SpawnUnownedCard(CardData cardData)
    {
        if (!IsServerInitialized || cardPrefab == null || cardData == null)
        {
            Debug.LogError($"CardSpawner on {gameObject.name}: Cannot spawn unowned card - missing required components or data");
            return null;
        }

        return SpawnCardInternal(cardData, null);
    }
    
    /// <summary>
    /// Spawns an unowned card with the given card data at a specific position (for deck viewing)
    /// </summary>
    /// <param name="cardData">The data for the card to spawn</param>
    /// <param name="spawnPosition">The world position where the card should be spawned</param>
    /// <returns>The spawned card GameObject, or null if spawn failed</returns>
    [Server]
    public GameObject SpawnUnownedCard(CardData cardData, Vector3 spawnPosition)
    {
        if (!IsServerInitialized || cardPrefab == null || cardData == null)
        {
            Debug.LogError($"CardSpawner on {gameObject.name}: Cannot spawn unowned card - missing required components or data");
            return null;
        }

        return SpawnCardInternal(cardData, null, spawnPosition);
    }
    
    /// <summary>
    /// Internal method that handles the actual card spawning logic
    /// </summary>
    /// <param name="cardData">The data for the card to spawn</param>
    /// <param name="ownerEntity">The NetworkEntity that will own this card, or null for unowned cards</param>
    /// <returns>The spawned card GameObject, or null if spawn failed</returns>
    [Server]
    private GameObject SpawnCardInternal(CardData cardData, NetworkEntity ownerEntity)
    {
        return SpawnCardInternal(cardData, ownerEntity, transform.position + defaultSpawnOffset);
    }
    
    /// <summary>
    /// Internal method that handles the actual card spawning logic with custom spawn position
    /// </summary>
    /// <param name="cardData">The data for the card to spawn</param>
    /// <param name="ownerEntity">The NetworkEntity that will own this card, or null for unowned cards</param>
    /// <param name="spawnPosition">The world position where the card should be spawned</param>
    /// <returns>The spawned card GameObject, or null if spawn failed</returns>
    [Server]
    private GameObject SpawnCardInternal(CardData cardData, NetworkEntity ownerEntity, Vector3 spawnPosition)
    {
        /* Debug.Log($"=== CardSpawner.SpawnCardInternal START ==="); */
        /* Debug.Log($"CardSpawner on: {gameObject.name}"); */
        /* Debug.Log($"Card to spawn: {cardData.CardName}"); */
        /* Debug.Log($"Owner Entity: {(ownerEntity != null ? ownerEntity.EntityName.Value : "null")}"); */
        /* Debug.Log($"Owner Entity ClientId: {(ownerEntity?.Owner?.ClientId ?? -1)}"); */
        /* Debug.Log($"Spawn Position: {spawnPosition}"); */
        
        // Instantiate the card prefab
        GameObject cardObject = Instantiate(cardPrefab);
        
        // Set the card's position
        cardObject.transform.position = spawnPosition;
        
        // Initialize the card with data before spawning
        Card card = cardObject.GetComponent<Card>();
        if (card != null)
        {
            // Initialize the card with its data first
            card.Initialize(cardData);
            /* Debug.Log($"Card {cardData.CardName}: Initialized with data"); */
        }
        else
        {
            Debug.LogError($"CardSpawner on {gameObject.name}: Card prefab is missing Card component");
            Destroy(cardObject);
            return null;
        }

        // Spawn the card on the network
        NetworkObject networkObject = cardObject.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            if (ownerEntity != null)
            {
                // Log the network entity we're about to use as the owner
                /* Debug.Log($"CardSpawner: PREPARING TO SPAWN OWNED CARD"); */
                /* Debug.Log($"- Target Owner Entity: {ownerEntity.EntityName.Value}"); */
                /* Debug.Log($"- Target Owner Entity GameObject: {ownerEntity.gameObject.name}"); */
                /* Debug.Log($"- Target Owner Entity ObjectId: {ownerEntity.GetComponent<NetworkObject>().ObjectId}"); */
                /* Debug.Log($"- Target Owner Entity ClientId: {ownerEntity.Owner?.ClientId ?? -1}"); */
                /* Debug.Log($"- Target Owner Entity IsOwner: {ownerEntity.IsOwner}"); */
                
                // Spawn with specific owner
                FishNet.InstanceFinder.ServerManager.Spawn(networkObject, ownerEntity.Owner);
                /* Debug.Log($"CardSpawner: NETWORK SPAWNED owned card {cardData.CardName} (ID: {cardData.CardId})"); */
                /* Debug.Log($"- Card NetworkObject Owner after spawn: {networkObject.Owner?.ClientId ?? -1}"); */
                /* Debug.Log($"- Card NetworkObject IsOwner after spawn: {networkObject.IsOwner}"); */
                /* Debug.Log($"- Card NetworkObject ObjectId after spawn: {networkObject.ObjectId}"); */
                
                // IMPORTANT: Set owner AFTER spawning so ObjectId is valid and SyncVars work correctly
                /* Debug.Log($"CardSpawner: SETTING OWNER ENTITY on Card {cardObject.name}"); */
                /* Debug.Log($"- Setting to NetworkEntity: {ownerEntity.EntityName.Value} (Component ID: {ownerEntity.GetInstanceID()})"); */
                
                // Set the logical owner of the card AFTER network spawn
                card.SetOwnerEntity(ownerEntity);
                
                // Verify the owner entity was set correctly
                /* Debug.Log($"CardSpawner: VERIFICATION AFTER SetOwnerEntity"); */
                /* Debug.Log($"- Card.OwnerEntity is {(card.OwnerEntity != null ? "SET" : "NULL")}"); */
                if (card.OwnerEntity != null)
                {
                    /* Debug.Log($"- Card.OwnerEntity = {card.OwnerEntity.EntityName.Value} (ID: {card.OwnerEntity.GetInstanceID()})"); */
                    /* Debug.Log($"- Card.OwnerEntity ClientId = {card.OwnerEntity.Owner?.ClientId ?? -1}"); */
                    /* Debug.Log($"- Card.OwnerEntity IsOwner = {card.OwnerEntity.IsOwner}"); */
                }
                
                // One final verification after network spawn and owner setting
                /* Debug.Log($"CardSpawner: FINAL VERIFICATION"); */
                /* Debug.Log($"- Card NetworkObject Owner ClientId: {networkObject.Owner?.ClientId ?? -1}"); */
                /* Debug.Log($"- Card.OwnerEntity: {(card.OwnerEntity != null ? card.OwnerEntity.EntityName.Value : "null")}"); */
                /* Debug.Log($"- Card.OwnerEntity ClientId: {(card.OwnerEntity?.Owner?.ClientId ?? -1)}"); */
            }
            else
            {
                // Spawn without specific owner (server-owned, unowned card for draft)
                FishNet.InstanceFinder.ServerManager.Spawn(networkObject);
                /* Debug.Log($"CardSpawner on {gameObject.name}: NETWORK SPAWNED unowned card {cardData.CardName} (ID: {cardData.CardId}) for draft"); */
                
                // Don't set an owner entity for draft cards - they remain unowned until picked
                /* Debug.Log($"CardSpawner on {gameObject.name}: Card {cardObject.name} spawned as unowned (for draft)"); */
            }
        }
        else
        {
            Debug.LogError($"CardSpawner on {gameObject.name}: Card prefab is missing NetworkObject component");
            Destroy(cardObject);
            return null;
        }

        /* Debug.Log($"=== CardSpawner.SpawnCardInternal END ==="); */
        return cardObject;
    }

    /// <summary>
    /// Despawns a card from the network
    /// </summary>
    [Server]
    public void DespawnCard(GameObject cardObject)
    {
        if (!IsServerInitialized || cardObject == null) return;

        NetworkObject networkObject = cardObject.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
        {
            FishNet.InstanceFinder.ServerManager.Despawn(networkObject);
            Debug.Log($"CardSpawner on {gameObject.name}: Despawned card {cardObject.name}");
        }
    }
} 