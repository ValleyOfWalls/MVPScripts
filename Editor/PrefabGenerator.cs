using UnityEngine;
using UnityEditor;
using System.IO;
using FishNet.Object;
using System.Collections.Generic;

#if UNITY_EDITOR
public class PrefabGenerator : MonoBehaviour
{
    [Header("Required Prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject networkManagerPrefab;
    [SerializeField] private GameObject uiManagerPrefab;
    [SerializeField] private GameObject gameManagerPrefab;
    
    [Header("Generation Settings")]
    [SerializeField] private string prefabOutputPath = "Assets/Prefabs";
    [SerializeField] private bool autoGenerateMissingPrefabs = true;
    
    private void OnValidate()
    {
        if (autoGenerateMissingPrefabs && Application.isEditor && !Application.isPlaying)
        {
            GenerateMissingPrefabs();
        }
    }
    
    [ContextMenu("Generate All Prefabs")]
    public void GenerateAllPrefabs()
    {
        // Create output directory if it doesn't exist
        if (!Directory.Exists(prefabOutputPath))
        {
            Directory.CreateDirectory(prefabOutputPath);
        }
        
        // Generate each prefab
        GeneratePlayerPrefab();
        GenerateNetworkManagerPrefab();
        GenerateUIManagerPrefab();
        GenerateGameManagerPrefab();
        
        Debug.Log("All prefabs generated successfully!");
    }
    
    [ContextMenu("Generate Missing Prefabs")]
    public void GenerateMissingPrefabs()
    {
        // Create output directory if it doesn't exist
        if (!Directory.Exists(prefabOutputPath))
        {
            Directory.CreateDirectory(prefabOutputPath);
        }
        
        // Check and generate each prefab if missing
        if (playerPrefab == null)
            GeneratePlayerPrefab();
            
        if (networkManagerPrefab == null)
            GenerateNetworkManagerPrefab();
            
        if (uiManagerPrefab == null)
            GenerateUIManagerPrefab();
            
        if (gameManagerPrefab == null)
            GenerateGameManagerPrefab();
            
        Debug.Log("Missing prefabs generated successfully!");
    }
    
    private void GeneratePlayerPrefab()
    {
        // Create player GameObject
        GameObject player = new GameObject("PlayerPrefab");
        
        // Add required components
        player.AddComponent<NetworkObject>();
        Player playerComponent = player.AddComponent<Player>();
        
        // Create visual representation
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(player.transform, false);
        visual.transform.localScale = new Vector3(1f, 2f, 1f);
        visual.transform.localPosition = new Vector3(0, 1, 0);
        
        // Store reference to renderer
        playerComponent.visualRenderer = visual.GetComponent<MeshRenderer>();
        
        // Save as prefab
        string prefabPath = $"{prefabOutputPath}/PlayerPrefab.prefab";
        playerPrefab = SavePrefab(player, prefabPath);
        
        // Clean up
        DestroyImmediate(player);
    }
    
    private void GenerateNetworkManagerPrefab()
    {
        // Create NetworkManager GameObject
        GameObject networkManagerObj = new GameObject("NetworkManager");
        
        // Add FishNet's NetworkManager component
        FishNet.Managing.NetworkManager networkManager = networkManagerObj.AddComponent<FishNet.Managing.NetworkManager>();
        
        // Add Tugboat transport
        FishNet.Transporting.Tugboat.Tugboat tugboat = networkManagerObj.AddComponent<FishNet.Transporting.Tugboat.Tugboat>();
        networkManager.TransportManager.Transport = tugboat;
        
        // Configure transport
        tugboat.SetClientAddress("127.0.0.1");
        tugboat.SetPort(7777);
        
        // Save as prefab
        string prefabPath = $"{prefabOutputPath}/NetworkManager.prefab";
        networkManagerPrefab = SavePrefab(networkManagerObj, prefabPath);
        
        // Clean up
        DestroyImmediate(networkManagerObj);
    }
    
    private void GenerateUIManagerPrefab()
    {
        // Create UIManager GameObject
        GameObject uiManagerObj = new GameObject("UIManager");
        
        // Add UIManager component
        uiManagerObj.AddComponent<UIManager>();
        
        // Save as prefab
        string prefabPath = $"{prefabOutputPath}/UIManager.prefab";
        uiManagerPrefab = SavePrefab(uiManagerObj, prefabPath);
        
        // Clean up
        DestroyImmediate(uiManagerObj);
    }
    
    private void GenerateGameManagerPrefab()
    {
        // Create GameManager GameObject
        GameObject gameManagerObj = new GameObject("GameManager");
        
        // Add NetworkObject and GameManager components
        gameManagerObj.AddComponent<NetworkObject>();
        gameManagerObj.AddComponent<GameManager>();
        
        // Save as prefab
        string prefabPath = $"{prefabOutputPath}/GameManager.prefab";
        gameManagerPrefab = SavePrefab(gameManagerObj, prefabPath);
        
        // Clean up
        DestroyImmediate(gameManagerObj);
    }
    
    private GameObject SavePrefab(GameObject obj, string path)
    {
#if UNITY_EDITOR
        // Create containing folder if it doesn't exist
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // Save the prefab and return reference
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, path);
        return prefab;
#else
        return null;
#endif
    }
}
#endif 