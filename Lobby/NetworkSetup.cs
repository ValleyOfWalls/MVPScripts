using UnityEngine;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using Steamworks;
using FishNet.Object;
using UnityEngine.UI;
using System.Collections.Generic;
using FishNet;

public class NetworkSetup : MonoBehaviour
{
    [Header("Network Configuration")]
    [SerializeField] private ushort defaultPort = 7777;
    [SerializeField] private string defaultAddress = "127.0.0.1";
    [SerializeField] private bool useSteam = true;

    [Header("Required Prefabs")]
    [SerializeField] private NetworkManager networkManagerPrefab;
    [SerializeField] private SteamManager steamManagerPrefab;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject canvasPrefab;
    
    private NetworkManager _networkManager;
    private SteamManager _steamManager;
    
    private void Awake()
    {
        // Set application target frame rate
        Application.targetFrameRate = 60;
        
        // Generate required prefabs if they don't exist
        EnsurePrefabsExist();
    }
    
    private void EnsurePrefabsExist()
    {
        // Check and create NetworkManager using InstanceFinder
        if (InstanceFinder.NetworkManager == null)
        {
            if (networkManagerPrefab != null)
            {
                _networkManager = Instantiate(networkManagerPrefab);
            }
            else
            {
                _networkManager = CreateNetworkManager();
            }
        }
        else
        {
            _networkManager = InstanceFinder.NetworkManager;
        }
        
        // Check and create SteamManager using FindFirstObjectByType
        if (FindFirstObjectByType<SteamManager>() == null && useSteam)
        {
            if (steamManagerPrefab != null)
            {
                _steamManager = Instantiate(steamManagerPrefab);
            }
            else
            {
                _steamManager = CreateSteamManager();
            }
        }
        
        // Check and create Canvas using FindFirstObjectByType
        if (FindFirstObjectByType<Canvas>() == null)
        {
            if (canvasPrefab != null)
            {
                Instantiate(canvasPrefab);
            }
            else
            {
                CreateCanvasPrefabs();
            }
        }
        
        // Ensure PlayerPrefab (NetworkPlayer) is in Spawnable Prefabs
        if (_networkManager != null && playerPrefab != null)
        {
            NetworkObject nob = playerPrefab.GetComponent<NetworkObject>();
            if (nob != null)
            {
                // Register the prefab and force it to be added to the default scene
                _networkManager.SpawnablePrefabs.AddObject(nob, true);
                Debug.Log($"Player prefab {playerPrefab.name} added to NetworkManager spawnable prefabs");
            }
            else
            {
                Debug.LogError($"PlayerPrefab {playerPrefab.name} is missing NetworkObject component!");
            }
        }
        else if (_networkManager != null && playerPrefab == null)
        {
             Debug.LogWarning("PlayerPrefab is not assigned in NetworkSetup Inspector.");
        }
        
        // Create and Register a PlayerSpawner prefab
        GameObject spawnerPrefab = new GameObject("PlayerSpawnerPrefab");
        PlayerSpawner spawner = spawnerPrefab.AddComponent<PlayerSpawner>();
        NetworkObject spawnerNob = spawnerPrefab.AddComponent<NetworkObject>();
        
        // Set properties and register with NetworkManager
        if (playerPrefab != null)
        {
            spawner.playerPrefab = playerPrefab;
        }
        
        // Add PlayerSpawner to NetworkManager's spawnable prefabs
        if (_networkManager != null)
        {
            _networkManager.SpawnablePrefabs.AddObject(spawnerNob, true);
            Debug.Log($"PlayerSpawner prefab added to NetworkManager's spawnable prefabs");
        }
        
        // This is a prefab, so don't spawn it immediately - mark as a prefab so it won't run
        spawnerPrefab.SetActive(false);
    }
    
    private NetworkManager CreateNetworkManager()
    {
        // Create a new GameObject for NetworkManager
        GameObject networkManagerObj = new GameObject("NetworkManager");
        NetworkManager nm = networkManagerObj.AddComponent<NetworkManager>();
        
        // Add Tugboat transport
        Tugboat tugboat = networkManagerObj.AddComponent<Tugboat>();
        nm.TransportManager.Transport = tugboat;
        
        // Configure transport
        tugboat.SetClientAddress(defaultAddress);
        tugboat.SetPort(defaultPort);
        
        // Set as DontDestroyOnLoad
        DontDestroyOnLoad(networkManagerObj);
        
        // Add default NetworkObject prefabs to the NetworkManager
        if (playerPrefab != null)
        {   
            NetworkObject nob = playerPrefab.GetComponent<NetworkObject>();
            if (nob != null) {
                 // Attempt to add the prefab
                 nm.SpawnablePrefabs.AddObject(nob);
                 // Debug.Log($"Attempted to add {playerPrefab.name} to created NetworkManager Spawnable Prefabs.");
            }
            else
            {
                Debug.LogError($"Assigned PlayerPrefab {playerPrefab.name} is missing NetworkObject component!");
            }
        }
        
        Debug.Log("Created NetworkManager with Tugboat transport");
        return nm;
    }
    
    private SteamManager CreateSteamManager()
    {
        // Create a new GameObject for SteamManager
        GameObject steamManagerObj = new GameObject("SteamManager");
        SteamManager sm = steamManagerObj.AddComponent<SteamManager>();
        
        // Set as DontDestroyOnLoad
        DontDestroyOnLoad(steamManagerObj);
        
        Debug.Log("Created SteamManager");
        return sm;
    }
    
    private void CreateCanvasPrefabs()
    {
        // Create Start Screen Canvas
        CreateUICanvas("StartScreenCanvas", out Canvas startScreenCanvas);
        CreateStartScreenUI(startScreenCanvas.transform);
        
        // Create Lobby Canvas
        CreateUICanvas("LobbyCanvas", out Canvas lobbyCanvas);
        CreateLobbyUI(lobbyCanvas.transform);
        
        // Create Combat Canvas
        CreateUICanvas("CombatCanvas", out Canvas combatCanvas);
        
        // Hide both by default
        lobbyCanvas.gameObject.SetActive(false);
        combatCanvas.gameObject.SetActive(false);
        
        Debug.Log("Created UI canvas prefabs");
    }
    
    private void CreateUICanvas(string name, out Canvas canvas)
    {
        GameObject canvasObj = new GameObject(name);
        canvas = canvasObj.AddComponent<Canvas>();
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        // Set canvas properties
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        
        // Create background image
        GameObject bgObj = new GameObject("BG");
        bgObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image bgImage = bgObj.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 1.0f);
        bgImage.raycastTarget = true;
        
        // Set BG to stretch full screen
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
    }
    
    private void CreateStartScreenUI(Transform parent)
    {
        // Create center panel
        GameObject centerPanel = new GameObject("CenterPanel");
        centerPanel.transform.SetParent(parent, false);
        
        RectTransform centerRect = centerPanel.AddComponent<RectTransform>();
        centerRect.anchorMin = new Vector2(0.5f, 0.5f);
        centerRect.anchorMax = new Vector2(0.5f, 0.5f);
        centerRect.pivot = new Vector2(0.5f, 0.5f);
        centerRect.sizeDelta = new Vector2(400, 300);
        
        UnityEngine.UI.Image panelImage = centerPanel.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        
        UnityEngine.UI.VerticalLayoutGroup layout = centerPanel.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        
        centerPanel.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        
        // Title
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(centerPanel.transform, false);
        TMPro.TextMeshProUGUI titleText = titleObj.AddComponent<TMPro.TextMeshProUGUI>();
        titleText.text = "GAME LOBBY";
        titleText.fontSize = 36;
        titleText.alignment = TMPro.TextAlignmentOptions.Center;
        titleText.color = Color.white;
        
        UnityEngine.UI.LayoutElement titleLayout = titleObj.AddComponent<UnityEngine.UI.LayoutElement>();
        titleLayout.minHeight = 50;
        titleLayout.preferredHeight = 50;
        
        // Player Name Input
        GameObject inputObj = new GameObject("PlayerNameInput");
        inputObj.transform.SetParent(centerPanel.transform, false);
        
        UnityEngine.UI.Image inputBg = inputObj.AddComponent<UnityEngine.UI.Image>();
        inputBg.color = new Color(0.3f, 0.3f, 0.3f, 1.0f);
        
        TMPro.TMP_InputField nameInput = inputObj.AddComponent<TMPro.TMP_InputField>();
        
        GameObject textArea = new GameObject("Text Area");
        textArea.transform.SetParent(inputObj.transform, false);
        textArea.AddComponent<RectTransform>();
        textArea.AddComponent<UnityEngine.UI.RectMask2D>();
        
        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(textArea.transform, false);
        TMPro.TextMeshProUGUI placeholder = placeholderObj.AddComponent<TMPro.TextMeshProUGUI>();
        placeholder.text = "Enter Your Name...";
        placeholder.fontSize = 18;
        placeholder.alignment = TMPro.TextAlignmentOptions.Left;
        placeholder.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(textArea.transform, false);
        TMPro.TextMeshProUGUI inputText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        inputText.text = "";
        inputText.fontSize = 18;
        inputText.alignment = TMPro.TextAlignmentOptions.Left;
        inputText.color = Color.white;
        
        nameInput.textViewport = textArea.GetComponent<RectTransform>();
        nameInput.placeholder = placeholder;
        nameInput.textComponent = inputText;
        nameInput.characterLimit = 20;
        
        UnityEngine.UI.LayoutElement inputLayout = inputObj.AddComponent<UnityEngine.UI.LayoutElement>();
        inputLayout.minHeight = 40;
        inputLayout.preferredHeight = 40;
        
        // Connect Button
        GameObject buttonObj = new GameObject("ConnectButton");
        buttonObj.transform.SetParent(centerPanel.transform, false);
        
        UnityEngine.UI.Image buttonImage = buttonObj.AddComponent<UnityEngine.UI.Image>();
        buttonImage.color = new Color(0.2f, 0.6f, 0.2f, 1.0f);
        
        UnityEngine.UI.Button button = buttonObj.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = buttonImage;
        
        // Button text
        GameObject buttonTextObj = new GameObject("Text (TMP)");
        buttonTextObj.transform.SetParent(buttonObj.transform, false);
        TMPro.TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TMPro.TextMeshProUGUI>();
        buttonText.text = "CONNECT";
        buttonText.fontSize = 20;
        buttonText.alignment = TMPro.TextAlignmentOptions.Center;
        buttonText.color = Color.white;
        
        RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;
        
        UnityEngine.UI.LayoutElement buttonLayout = buttonObj.AddComponent<UnityEngine.UI.LayoutElement>();
        buttonLayout.minHeight = 50;
        buttonLayout.preferredHeight = 50;
        
        // Position text elements
        RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
        textAreaRect.anchorMin = new Vector2(0, 0);
        textAreaRect.anchorMax = new Vector2(1, 1);
        textAreaRect.offsetMin = new Vector2(10, 0);
        textAreaRect.offsetMax = new Vector2(-10, 0);
        
        RectTransform placeholderRect = placeholderObj.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }
    
    private void CreateLobbyUI(Transform parent)
    {
        // Create title text
        GameObject titleObj = new GameObject("LobbyTitleText");
        titleObj.transform.SetParent(parent, false);
        TMPro.TextMeshProUGUI titleText = titleObj.AddComponent<TMPro.TextMeshProUGUI>();
        titleText.text = "LOBBY";
        titleText.fontSize = 36;
        titleText.alignment = TMPro.TextAlignmentOptions.Center;
        titleText.color = Color.white;
        
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.9f);
        titleRect.anchorMax = new Vector2(0.5f, 0.9f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(400, 50);
        
        titleObj.AddComponent<UnityEngine.UI.ContentSizeFitter>().horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        
        // Player list panel
        GameObject listPanelObj = new GameObject("PlayerListPanel");
        listPanelObj.transform.SetParent(parent, false);
        
        RectTransform listRect = listPanelObj.AddComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.5f, 0.5f);
        listRect.anchorMax = new Vector2(0.5f, 0.5f);
        listRect.pivot = new Vector2(0.5f, 0.5f);
        listRect.sizeDelta = new Vector2(400, 300);
        
        UnityEngine.UI.Image listImage = listPanelObj.AddComponent<UnityEngine.UI.Image>();
        listImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        
        UnityEngine.UI.VerticalLayoutGroup layout = listPanelObj.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        
        listPanelObj.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        
        // Player entry template
        GameObject entryObj = new GameObject("PlayerEntryTemplate");
        entryObj.transform.SetParent(listPanelObj.transform, false);
        TMPro.TextMeshProUGUI entryText = entryObj.AddComponent<TMPro.TextMeshProUGUI>();
        entryText.text = "Player Name - Not Ready";
        entryText.fontSize = 24;
        entryText.alignment = TMPro.TextAlignmentOptions.Left;
        entryText.color = Color.white;
        
        entryObj.AddComponent<UnityEngine.UI.ContentSizeFitter>().horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        
        // Ready Button
        GameObject readyButtonObj = new GameObject("ReadyButton");
        readyButtonObj.transform.SetParent(parent, false);
        
        RectTransform readyRect = readyButtonObj.AddComponent<RectTransform>();
        readyRect.anchorMin = new Vector2(0.3f, 0.2f);
        readyRect.anchorMax = new Vector2(0.3f, 0.2f);
        readyRect.pivot = new Vector2(0.5f, 0.5f);
        readyRect.sizeDelta = new Vector2(150, 50);
        
        UnityEngine.UI.Image readyImage = readyButtonObj.AddComponent<UnityEngine.UI.Image>();
        readyImage.color = new Color(0.2f, 0.6f, 0.2f, 1.0f);
        
        UnityEngine.UI.Button readyButton = readyButtonObj.AddComponent<UnityEngine.UI.Button>();
        readyButton.targetGraphic = readyImage;
        
        // Ready button text
        GameObject readyTextObj = new GameObject("Text (TMP)");
        readyTextObj.transform.SetParent(readyButtonObj.transform, false);
        TMPro.TextMeshProUGUI readyText = readyTextObj.AddComponent<TMPro.TextMeshProUGUI>();
        readyText.text = "READY";
        readyText.fontSize = 20;
        readyText.alignment = TMPro.TextAlignmentOptions.Center;
        readyText.color = Color.white;
        
        RectTransform readyTextRect = readyTextObj.GetComponent<RectTransform>();
        readyTextRect.anchorMin = Vector2.zero;
        readyTextRect.anchorMax = Vector2.one;
        readyTextRect.offsetMin = Vector2.zero;
        readyTextRect.offsetMax = Vector2.zero;
        
        readyTextObj.AddComponent<UnityEngine.UI.ContentSizeFitter>().horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        
        // Start Game Button
        GameObject startButtonObj = new GameObject("StartGameButton");
        startButtonObj.transform.SetParent(parent, false);
        
        RectTransform startRect = startButtonObj.AddComponent<RectTransform>();
        startRect.anchorMin = new Vector2(0.5f, 0.2f);
        startRect.anchorMax = new Vector2(0.5f, 0.2f);
        startRect.pivot = new Vector2(0.5f, 0.5f);
        startRect.sizeDelta = new Vector2(200, 50);
        
        UnityEngine.UI.Image startImage = startButtonObj.AddComponent<UnityEngine.UI.Image>();
        startImage.color = new Color(0.6f, 0.2f, 0.2f, 1.0f);
        
        UnityEngine.UI.Button startButton = startButtonObj.AddComponent<UnityEngine.UI.Button>();
        startButton.targetGraphic = startImage;
        
        // Start button text
        GameObject startTextObj = new GameObject("Text (TMP)");
        startTextObj.transform.SetParent(startButtonObj.transform, false);
        TMPro.TextMeshProUGUI startText = startTextObj.AddComponent<TMPro.TextMeshProUGUI>();
        startText.text = "START GAME";
        startText.fontSize = 20;
        startText.alignment = TMPro.TextAlignmentOptions.Center;
        startText.color = Color.white;
        
        RectTransform startTextRect = startTextObj.GetComponent<RectTransform>();
        startTextRect.anchorMin = Vector2.zero;
        startTextRect.anchorMax = Vector2.one;
        startTextRect.offsetMin = Vector2.zero;
        startTextRect.offsetMax = Vector2.zero;
        
        startTextObj.AddComponent<UnityEngine.UI.ContentSizeFitter>().horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        
        // Leave Button
        GameObject leaveButtonObj = new GameObject("LeaveButton");
        leaveButtonObj.transform.SetParent(parent, false);
        
        RectTransform leaveRect = leaveButtonObj.AddComponent<RectTransform>();
        leaveRect.anchorMin = new Vector2(0.7f, 0.2f);
        leaveRect.anchorMax = new Vector2(0.7f, 0.2f);
        leaveRect.pivot = new Vector2(0.5f, 0.5f);
        leaveRect.sizeDelta = new Vector2(150, 50);
        
        UnityEngine.UI.Image leaveImage = leaveButtonObj.AddComponent<UnityEngine.UI.Image>();
        leaveImage.color = new Color(0.4f, 0.4f, 0.4f, 1.0f);
        
        UnityEngine.UI.Button leaveButton = leaveButtonObj.AddComponent<UnityEngine.UI.Button>();
        leaveButton.targetGraphic = leaveImage;
        
        // Leave button text
        GameObject leaveTextObj = new GameObject("Text (TMP)");
        leaveTextObj.transform.SetParent(leaveButtonObj.transform, false);
        TMPro.TextMeshProUGUI leaveText = leaveTextObj.AddComponent<TMPro.TextMeshProUGUI>();
        leaveText.text = "LEAVE";
        leaveText.fontSize = 20;
        leaveText.alignment = TMPro.TextAlignmentOptions.Center;
        leaveText.color = Color.white;
        
        RectTransform leaveTextRect = leaveTextObj.GetComponent<RectTransform>();
        leaveTextRect.anchorMin = Vector2.zero;
        leaveTextRect.anchorMax = Vector2.one;
        leaveTextRect.offsetMin = Vector2.zero;
        leaveTextRect.offsetMax = Vector2.zero;
        
        leaveTextObj.AddComponent<UnityEngine.UI.ContentSizeFitter>().horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
    }
} 