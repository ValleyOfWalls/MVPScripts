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
    /// Internal method that handles the actual card spawning logic
    /// </summary>
    /// <param name="cardData">The data for the card to spawn</param>
    /// <param name="ownerEntity">The NetworkEntity that will own this card, or null for unowned cards</param>
    /// <returns>The spawned card GameObject, or null if spawn failed</returns>
    [Server]
    private GameObject SpawnCardInternal(CardData cardData, NetworkEntity ownerEntity)
    {
        // Instantiate the card prefab
        GameObject cardObject = Instantiate(cardPrefab);
        
        // Set the card's position with offset to prevent z-fighting
        cardObject.transform.position = transform.position + defaultSpawnOffset;
        
        // Initialize the card with data before spawning
        Card card = cardObject.GetComponent<Card>();
        if (card != null)
        {
            // Initialize the card with its data first
            card.Initialize(cardData);
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
                Debug.Log($"CardSpawner on {gameObject.name}: PREPARING TO SPAWN OWNED CARD with Owner NetworkEntity: {ownerEntity.name} " +
                         $"(EntityName: {ownerEntity.EntityName.Value}, ObjectId: {ownerEntity.GetComponent<NetworkObject>().ObjectId}, " +
                         $"ClientId: {ownerEntity.Owner?.ClientId ?? -1})");
                
                // Spawn with specific owner
                FishNet.InstanceFinder.ServerManager.Spawn(networkObject, ownerEntity.Owner);
                Debug.Log($"CardSpawner on {gameObject.name}: NETWORK SPAWNED owned card {cardData.CardName} (ID: {cardData.CardId}) " +
                         $"for network owner {ownerEntity.Owner?.ClientId ?? -1}");
                
                // IMPORTANT: Set owner AFTER spawning so ObjectId is valid and SyncVars work correctly
                Debug.Log($"CardSpawner on {gameObject.name}: SETTING OWNER ENTITY on Card {cardObject.name} (Card Component ID: {card.GetInstanceID()})");
                Debug.Log($"CardSpawner on {gameObject.name}: - Setting to NetworkEntity: {ownerEntity.name} (Component ID: {ownerEntity.GetInstanceID()})");
                
                // Set the logical owner of the card AFTER network spawn
                card.SetOwnerEntity(ownerEntity);
                
                // Verify the owner entity was set correctly
                Debug.Log($"CardSpawner on {gameObject.name}: - VERIFICATION - Card.ownerEntity is {(card.OwnerEntity != null ? "SET" : "NULL")}");
                if (card.OwnerEntity != null)
                {
                    Debug.Log($"CardSpawner on {gameObject.name}: - Card.ownerEntity = {card.OwnerEntity.name} (ID: {card.OwnerEntity.GetInstanceID()})");
                }
                
                // One final verification after network spawn and owner setting
                Debug.Log($"CardSpawner on {gameObject.name}: FINAL VERIFICATION - " +
                         $"Card.ownerEntity is {(card.OwnerEntity != null ? $"SET to {card.OwnerEntity.name}" : "NULL")}");
            }
            else
            {
                // Spawn without specific owner (server-owned, unowned card for draft)
                FishNet.InstanceFinder.ServerManager.Spawn(networkObject);
                Debug.Log($"CardSpawner on {gameObject.name}: NETWORK SPAWNED unowned card {cardData.CardName} (ID: {cardData.CardId}) for draft");
                
                // Don't set an owner entity for draft cards - they remain unowned until picked
                Debug.Log($"CardSpawner on {gameObject.name}: Card {cardObject.name} spawned as unowned (for draft)");
            }
        }
        else
        {
            Debug.LogError($"CardSpawner on {gameObject.name}: Card prefab is missing NetworkObject component");
            Destroy(cardObject);
            return null;
        }

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