using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Object;
using FishNet.Managing;
using Steamworks;
using UnityEngine.EventSystems;

public class GameSetup : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject networkSetupPrefab;
    [SerializeField] private GameObject gameManagerPrefab;
    [SerializeField] private GameObject uiManagerPrefab;
    [SerializeField] private GameObject eventSystemPrefab;
    [SerializeField] private GameObject petPrefab;
    
    private NetworkSetup networkSetup;
    private GameManager gameManager;
    private UIManager uiManager;
    
    private void Awake()
    {
        // Debug.Log("GameSetup: Ensuring core components exist...");

        // Make sure we have an EventSystem
        EnsureEventSystem();

        // Ensure core managers exist (they should be in the scene)
        EnsureManager<NetworkManager>(); // Assuming NetworkManager is part of NetworkSetup or standalone
        EnsureManager<GameManager>();
        EnsureManager<UIManager>();

        // Assign Pet Prefab to GameManager
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null && petPrefab != null)
        {
            gm.SetPetPrefab(petPrefab);
            // Debug.Log("GameSetup: Assigned Pet Prefab to GameManager.");
        }
        else if (gm == null)
        {
            Debug.LogError("GameSetup: GameManager not found, cannot assign Pet Prefab!");
        }
        else if (petPrefab == null)
        {
            Debug.LogWarning("GameSetup: Pet Prefab is not assigned in the inspector!");
        }

        // Find and mark managers as DontDestroyOnLoad if they are root objects
        // This assumes the managers themselves don't already do this
        SetupPersistence<NetworkManager>();
        SetupPersistence<GameManager>();
        SetupPersistence<UIManager>();

        // GameSetup itself probably doesn't need to persist
        // Destroy(gameObject); // Optional: Destroy GameSetup after it runs
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            // Debug.Log("GameSetup: No EventSystem found, creating one...");
            if (eventSystemPrefab != null)
            {
                Instantiate(eventSystemPrefab); // Prefab likely handles DontDestroyOnLoad
            }
            else
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(eventSystem);
            }
        }
         else
        {
             // Debug.Log("GameSetup: EventSystem already exists.");
        }
    }

    private void EnsureManager<T>() where T : MonoBehaviour
    {
        if (FindFirstObjectByType<T>() == null)
        { 
            Debug.LogError($"GameSetup: Critical Manager of type {typeof(T).Name} not found in the scene! Ensure it is placed in the scene hierarchy.");
        }
         else
        {
             // Debug.Log($"GameSetup: Manager {typeof(T).Name} found.");
        }
    }

     private void SetupPersistence<T>() where T: MonoBehaviour
     {
         T manager = FindFirstObjectByType<T>();
         if (manager != null && manager.transform.parent == null)
         { 
              // Check if DontDestroyOnLoad is already applied by the manager itself
              // Note: There's no direct public API to check if an object is marked DontDestroyOnLoad.
              // We rely on the manager's own Awake to handle it or apply it here cautiously.
              // A common pattern is for singletons to handle their own persistence.
              // If managers handle their own DontDestroyOnLoad, this call might be redundant or log a warning.
             // Debug.Log($"GameSetup: Ensuring {typeof(T).Name} is marked DontDestroyOnLoad (if it's a root object).");
             DontDestroyOnLoad(manager.gameObject); 
         }
     }
} 