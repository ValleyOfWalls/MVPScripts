using UnityEngine;
using System.Collections.Generic;

public class PetPrefabManager : MonoBehaviour
{
    public static PetPrefabManager Instance { get; private set; }
    
    [SerializeField] private GameObject defaultPetPrefab;
    [SerializeField] private List<GameObject> petPrefabs = new List<GameObject>();
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple PetPrefabManager instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        Debug.Log($"PetPrefabManager initialized with {petPrefabs.Count} pet prefabs");
        
        // Ensure default pet prefab is set
        if (defaultPetPrefab == null && petPrefabs.Count > 0)
        {
            defaultPetPrefab = petPrefabs[0];
            Debug.LogWarning("No default pet prefab assigned, using first prefab in list as default");
        }
    }
    
    public GameObject GetDefaultPetPrefab()
    {
        if (defaultPetPrefab == null)
        {
            Debug.LogError("No default pet prefab available!");
            return null;
        }
        return defaultPetPrefab;
    }
    
    public GameObject GetPetPrefabByIndex(int index)
    {
        if (petPrefabs.Count == 0)
        {
            Debug.LogWarning("No pet prefabs available, returning default");
            return defaultPetPrefab;
        }
            
        int safeIndex = index % petPrefabs.Count;
        return petPrefabs[safeIndex];
    }
    
    public int GetPetPrefabCount()
    {
        return petPrefabs.Count;
    }
    
    // Get a pet prefab based on a string identifier (could be expanded for named pets)
    public GameObject GetPetPrefabByName(string petName)
    {
        foreach (GameObject petPrefab in petPrefabs)
        {
            if (petPrefab.name.ToLower() == petName.ToLower())
            {
                return petPrefab;
            }
        }
        
        Debug.LogWarning($"No pet prefab found with name {petName}, returning default");
        return defaultPetPrefab;
    }
} 