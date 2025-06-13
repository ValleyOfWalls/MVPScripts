using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using FishNet.Object;

/// <summary>
/// Handles the UI visualization for pet entities
/// Attach to: The same GameObject as NetworkEntity (Pet type)
/// </summary>
public class NetworkPetUI : MonoBehaviour
{
    // Singleton pattern for easy access
    public static NetworkPetUI Instance { get; private set; }

    [SerializeField] private NetworkEntity entity;

    [Header("UI References")]
    [SerializeField] private Transform petHandTransform;
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
            Debug.LogWarning("More than one NetworkPetUI instance exists. This may cause issues.");
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
            Debug.LogError($"NetworkPetUI (ID: {objId}) - deckTransform is null");
        }

        if (petHandTransform != null)
        {
            string path = GetTransformPath(petHandTransform);
    
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
        if (entity == null || entity.EntityType != EntityType.Pet)
        {
            Debug.LogError("NetworkPetUI: Cannot find NetworkEntity component or entity is not a pet.");
            return;
        }

        if (entityDeck == null)
        {
            Debug.LogError("NetworkPetUI: Cannot find NetworkEntityDeck component.");
            return;
        }

        if (handManager == null)
        {
            Debug.LogError("NetworkPetUI: Cannot find HandManager component.");
            return;
        }

        if (networkObject == null)
        {
            Debug.LogError("NetworkPetUI: Cannot find NetworkObject component.");
        }
    }

    // Public getters for transforms
    public Transform GetPetHandTransform() => petHandTransform;
    public Transform GetDeckTransform() 
    {
        if (deckTransform == null)
        {
            Debug.LogError($"NetworkPetUI on {gameObject.name}: deckTransform is null");
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