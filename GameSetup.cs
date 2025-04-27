using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Object;
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
    
    private NetworkSetup networkSetup;
    private GameManager gameManager;
    private UIManager uiManager;
    
    private void Awake()
    {
        // Make sure we have an EventSystem
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            if (eventSystemPrefab != null)
            {
                Instantiate(eventSystemPrefab);
            }
            else
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                 DontDestroyOnLoad(eventSystem);
            }
        }
        
        // Create NetworkSetup if it doesn't exist
        if (FindFirstObjectByType<NetworkSetup>() == null)
        {
            if (networkSetupPrefab != null)
            {
                networkSetup = Instantiate(networkSetupPrefab).GetComponent<NetworkSetup>();
            }
            else
            {
                GameObject netSetupObj = new GameObject("NetworkSetup");
                networkSetup = netSetupObj.AddComponent<NetworkSetup>();
            }
        }
        else
        {
            networkSetup = FindFirstObjectByType<NetworkSetup>();
        }
        
        // Create GameManager if it doesn't exist
        if (FindFirstObjectByType<GameManager>() == null)
        {
            if (gameManagerPrefab != null)
            {
                gameManager = Instantiate(gameManagerPrefab).GetComponent<GameManager>();
                 DontDestroyOnLoad(gameManager.gameObject);
            }
            else
            {
                GameObject gmObj = new GameObject("GameManager");
                gameManager = gmObj.AddComponent<GameManager>();
                 DontDestroyOnLoad(gmObj);
            }
        }
        else
        {
            gameManager = FindFirstObjectByType<GameManager>();
            
            if (gameManager.transform.parent == null)
                DontDestroyOnLoad(gameManager.gameObject); 
        }
        
        // Create UIManager if it doesn't exist
        if (FindFirstObjectByType<UIManager>() == null)
        {
            if (uiManagerPrefab != null)
            {
                uiManager = Instantiate(uiManagerPrefab).GetComponent<UIManager>();
                 DontDestroyOnLoad(uiManager.gameObject);
            }
            else
            {
                GameObject uiObj = new GameObject("UIManager");
                uiManager = uiObj.AddComponent<UIManager>();
                 DontDestroyOnLoad(uiObj);
            }
        }
        else
        {
            uiManager = FindFirstObjectByType<UIManager>();
            
            if (uiManager.transform.parent == null)
                DontDestroyOnLoad(uiManager.gameObject); 
        }
        
        // Generate Player Prefab if needed
        if (playerPrefab == null)
        {
            Debug.LogError("PlayerPrefab (NetworkPlayer) is not assigned in GameSetup Inspector!");
        }
        
        // GameSetup itself doesn't strictly need to persist if its job is done in Awake
        // DontDestroyOnLoad(gameObject);
    }
} 