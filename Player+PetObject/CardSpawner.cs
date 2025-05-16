using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Handles spawning and despawning of card GameObjects based on changes to HandManager.
/// Attach to: NetworkEntity prefabs alongside HandManager.
/// </summary>
public class CardSpawner : NetworkBehaviour
{
    [Header("Card Prefab")]
    [SerializeField] private GameObject cardPrefab;

    [Header("Optional Settings")]
    [SerializeField] private Vector3 defaultSpawnOffset = new Vector3(0, 0, -0.1f);

    private NetworkEntity _spawnerNetworkEntity;

    private void Awake()
    {
        _spawnerNetworkEntity = GetComponent<NetworkEntity>();
        if (_spawnerNetworkEntity == null)
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
    /// Spawns a card with the given card data
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
            Debug.LogError($"CardSpawner on {gameObject.name}: Cannot spawn card - Spawner's NetworkEntity is missing.");
            return null;
        }

        // Log the network entity we're about to use as the owner
        Debug.Log($"CardSpawner on {gameObject.name}: PREPARING TO SPAWN CARD with Owner NetworkEntity: {_spawnerNetworkEntity.name} " +
                 $"(EntityName: {_spawnerNetworkEntity.EntityName.Value}, ObjectId: {_spawnerNetworkEntity.GetComponent<NetworkObject>().ObjectId}, " +
                 $"ClientId: {_spawnerNetworkEntity.Owner?.ClientId ?? -1})");

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

        // Spawn the card on the network, giving ownership to the client that owns the spawner's entity
        NetworkObject networkObject = cardObject.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            // Spawn first so the NetworkObject has a valid ObjectId
            FishNet.InstanceFinder.ServerManager.Spawn(networkObject, _spawnerNetworkEntity.Owner);
            Debug.Log($"CardSpawner on {gameObject.name}: NETWORK SPAWNED card {cardData.CardName} (ID: {cardData.CardId}) " +
                     $"for network owner {_spawnerNetworkEntity.Owner?.ClientId ?? -1}");
            
            // IMPORTANT: Set owner AFTER spawning so ObjectId is valid and SyncVars work correctly
            Debug.Log($"CardSpawner on {gameObject.name}: SETTING OWNER ENTITY on Card {cardObject.name} (Card Component ID: {card.GetInstanceID()})");
            Debug.Log($"CardSpawner on {gameObject.name}: - Setting to NetworkEntity: {_spawnerNetworkEntity.name} (Component ID: {_spawnerNetworkEntity.GetInstanceID()})");
            
            // Set the logical owner of the card AFTER network spawn
            card.SetOwnerEntity(_spawnerNetworkEntity);
            
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