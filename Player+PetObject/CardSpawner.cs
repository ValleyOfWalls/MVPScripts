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

    private void Awake()
    {
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

        // Instantiate the card prefab
        GameObject cardObject = Instantiate(cardPrefab);
        
        // Set the card's position with offset to prevent z-fighting
        cardObject.transform.position = transform.position + defaultSpawnOffset;
        
        // Get the Card component and initialize it
        Card card = cardObject.GetComponent<Card>();
        if (card != null)
        {
            // Initialize the card with its data
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
            FishNet.InstanceFinder.ServerManager.Spawn(networkObject);
            Debug.Log($"CardSpawner on {gameObject.name}: Successfully spawned card {cardData.CardName} (ID: {cardData.CardId})");
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