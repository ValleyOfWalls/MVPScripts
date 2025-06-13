using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using FishNet.Object;

/// <summary>
/// Handles the UI visualization for player entities
/// Attach to: The same GameObject as NetworkEntity (Player type)
/// </summary>
public class NetworkPlayerUI : MonoBehaviour
{
    // Singleton pattern for easy access
    public static NetworkPlayerUI Instance { get; private set; }

    [SerializeField] private NetworkEntity entity;

    [Header("UI References")]
    [SerializeField] private Transform playerHandTransform;
    [SerializeField] private Transform deckTransform;
    [SerializeField] private Transform discardTransform;

    private NetworkEntityDeck entityDeck;
    private HandManager handManager;
    private NetworkObject networkObject;

    private void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("More than one NetworkPlayerUI instance exists. This may cause issues.");
            Destroy(gameObject);
            return;
        }

        // Get required components
        if (entity == null) entity = GetComponent<NetworkEntity>();
        entityDeck = GetComponent<NetworkEntityDeck>();
        handManager = GetComponent<HandManager>();
        networkObject = GetComponent<NetworkObject>();

        ValidateComponents();
    }
    
    private void Start()
    {
        // Log transform paths for debugging
        LogTransformPaths();
    }

    private void LogTransformPaths()
    {
        string objId = networkObject != null ? networkObject.ObjectId.ToString() : "no NetworkObject";
        
        if (deckTransform != null)
        {
            string path = GetTransformPath(deckTransform);
    
        }
        else
        {
            Debug.LogError($"NetworkPlayerUI (ID: {objId}) - deckTransform is null");
        }

        if (playerHandTransform != null)
        {
            string path = GetTransformPath(playerHandTransform);
    
        }

        if (discardTransform != null)
        {
            string path = GetTransformPath(discardTransform);
    
        }
    }

    private string GetTransformPath(Transform transform)
    {
        if (transform == null) return "null";
        
        string path = transform.name;
        Transform parent = transform.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }

    private void ValidateComponents()
    {
        if (entity == null || entity.EntityType != EntityType.Player)
        {
            Debug.LogError("NetworkPlayerUI: Cannot find NetworkEntity component or entity is not a player.");
            return;
        }

        if (entityDeck == null)
        {
            Debug.LogError("NetworkPlayerUI: Cannot find NetworkEntityDeck component.");
            return;
        }

        if (handManager == null)
        {
            Debug.LogError("NetworkPlayerUI: Cannot find HandManager component.");
            return;
        }
        
        if (networkObject == null)
        {
            Debug.LogError("NetworkPlayerUI: Cannot find NetworkObject component.");
        }
    }

    // Public getters for transforms
    public Transform GetPlayerHandTransform() => playerHandTransform;
    public Transform GetDeckTransform() 
    {
        if (deckTransform == null)
        {
            Debug.LogError($"NetworkPlayerUI on {gameObject.name}: deckTransform is null");
            // Create a fallback transform if needed
            GameObject fallbackObj = new GameObject("FallbackDeckTransform");
            fallbackObj.transform.SetParent(transform);
            fallbackObj.transform.localPosition = Vector3.zero;
            deckTransform = fallbackObj.transform;
    
        }
        return deckTransform;
    }
    public Transform GetDiscardTransform() => discardTransform;
} 