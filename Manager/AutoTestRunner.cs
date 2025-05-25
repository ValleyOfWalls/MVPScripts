using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using FishNet.Managing;

public class AutoTestRunner : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Enable this flag to run the automated test sequence.")]
    public bool enableAutoTesting = false;
    
    [Tooltip("Enable this flag to show host/client status text when combat setup is complete.")]
    public bool enableHostClientDisplay = false;

    [Header("Button References")]
    [Tooltip("Drag the 'Start Button' GameObject from the initial start screen here.")]
    public Button startScreenStartButton;

    [Tooltip("Drag the 'Start Game Button' GameObject from the Lobby UI here.")]
    public Button lobbyStartGameButton;

    [Header("Component References")]
    [Tooltip("Drag the LobbyManager here.")]
    public LobbyManager lobbyManager;
    
    [Tooltip("Drag the StartScreenManager here.")]
    public StartScreenManager startScreenManager;
    
    [Tooltip("Drag the SteamNetworkIntegration object here.")]
    public SteamNetworkIntegration steamNetworkIntegration;
    
    [Tooltip("Drag the CombatSetup object here.")]
    public CombatSetup combatSetup;

    [Header("UI References")]
    [Tooltip("Text element to display host/client status. Will be enabled when combat setup is complete.")]
    public TextMeshProUGUI hostClientStatusText;

    private bool startButtonClicked = false;
    private bool readyToStartGame = false;
    private bool combatSetupComplete = false;

    void Start()
    {
        // Initially hide the host/client status text
        if (hostClientStatusText != null)
        {
            hostClientStatusText.gameObject.SetActive(false);
        }
        
        // Find CombatSetup if not assigned
        if (combatSetup == null)
        {
            combatSetup = FindFirstObjectByType<CombatSetup>();
        }
        
        if (enableAutoTesting)
        {
            Debug.Log("AutoTestRunner: Automated testing enabled. Starting sequence...");
            StartCoroutine(WaitForStartScreenReadiness());
        }
        else
        {
            Debug.Log("AutoTestRunner: Automated testing disabled.");
        }
        
        // Start monitoring combat setup regardless of auto testing
        if (enableHostClientDisplay)
        {
            StartCoroutine(MonitorCombatSetup());
        }
    }

    private IEnumerator MonitorCombatSetup()
    {
        Debug.Log("AutoTestRunner: Starting to monitor combat setup completion...");
        
        // Wait until CombatSetup is available
        yield return new WaitUntil(() => combatSetup != null);
        Debug.Log("AutoTestRunner: CombatSetup component found, now monitoring IsSetupComplete...");
        
        // Add periodic logging to track the setup status
        float checkInterval = 1.0f;
        float lastCheckTime = 0f;
        
        while (!combatSetup.IsSetupComplete)
        {
            if (Time.time - lastCheckTime >= checkInterval)
            {
                Debug.Log($"AutoTestRunner: Combat setup status - IsSetupComplete: {combatSetup.IsSetupComplete}, IsCombatActive: {combatSetup.IsCombatActive}");
                lastCheckTime = Time.time;
            }
            yield return null;
        }
        
        combatSetupComplete = true;
        Debug.Log("AutoTestRunner: Combat setup detected as complete! Displaying host/client status.");
        
        DisplayHostClientStatus();
    }

    private void DisplayHostClientStatus()
    {
        if (!enableHostClientDisplay || hostClientStatusText == null)
        {
            return;
        }
        
        string statusText = "Unknown";
        
        // Use FishNet's network state instead of Steam host status for more accurate detection
        if (FishNet.InstanceFinder.NetworkManager != null)
        {
            var networkManager = FishNet.InstanceFinder.NetworkManager;
            
            if (networkManager.IsServerStarted && networkManager.IsClientStarted)
            {
                statusText = "HOST"; // Both server and client are running (host mode)
            }
            else if (networkManager.IsClientStarted && !networkManager.IsServerStarted)
            {
                statusText = "CLIENT"; // Only client is running (client mode)
            }
            else if (networkManager.IsServerStarted && !networkManager.IsClientStarted)
            {
                statusText = "SERVER"; // Only server is running (dedicated server mode)
            }
            else
            {
                statusText = "DISCONNECTED"; // Neither server nor client is running
            }
        }
        else if (steamNetworkIntegration != null)
        {
            // Fallback to Steam host status if FishNet is not available
            if (steamNetworkIntegration.IsUserSteamHost)
            {
                statusText = "STEAM_HOST";
            }
            else
            {
                statusText = "STEAM_CLIENT";
            }
        }
        
        hostClientStatusText.text = $"Status: {statusText}";
        hostClientStatusText.gameObject.SetActive(true);
        
        Debug.Log($"AutoTestRunner: Displaying status - {statusText}");
    }

    private IEnumerator WaitForStartScreenReadiness()
    {
        Debug.Log("AutoTestRunner: Waiting for Steam to initialize and start button to be available...");
        
        // Wait for Steam initialization
        if (steamNetworkIntegration != null)
        {
            yield return new WaitUntil(() => steamNetworkIntegration.IsSteamInitialized);
            Debug.Log("AutoTestRunner: Steam initialized successfully.");
        }
        else
        {
            Debug.LogWarning("AutoTestRunner: SteamNetworkIntegration reference not set, skipping Steam initialization check.");
        }
        
        // Wait until the start button is available and interactable
        yield return new WaitUntil(() => startScreenStartButton != null && startScreenStartButton.gameObject.activeInHierarchy && startScreenStartButton.interactable);
        Debug.Log("AutoTestRunner: Start Screen Button is now available and interactable.");
        
        // Click the start button
        startScreenStartButton.onClick.Invoke();
        startButtonClicked = true;
        Debug.Log("AutoTestRunner: Start Screen Button clicked. Moving to lobby phase.");
        
        // Start monitoring lobby conditions
        StartCoroutine(WaitForLobbyReadiness());
    }

    private IEnumerator WaitForLobbyReadiness()
    {
        Debug.Log("AutoTestRunner: Waiting for lobby conditions to be met...");
        
        // Wait until lobby manager is ready
        yield return new WaitUntil(() => lobbyManager != null && lobbyManager.gameObject.activeInHierarchy);
        
        // Subscribe to lobby events
        lobbyManager.OnPlayersReadyStateChanged += CheckLobbyConditions;
        
        // Wait until conditions are met to start the game
        yield return new WaitUntil(() => readyToStartGame);
        
        // Only the host should click the start button
        if (steamNetworkIntegration != null && steamNetworkIntegration.IsUserSteamHost && lobbyStartGameButton != null && lobbyStartGameButton.interactable)
        {
            Debug.Log("AutoTestRunner: This client is the host and lobby conditions are met. Clicking Start Game button.");
            lobbyStartGameButton.onClick.Invoke();
        }
        else
        {
            Debug.Log("AutoTestRunner: This client is not the host or button is not interactable. Waiting for host to start the game.");
        }
        
        Debug.Log("AutoTestRunner: Automated test sequence completed.");
    }

    private void CheckLobbyConditions()
    {
        if (lobbyManager == null) return;
        
        int playerCount = lobbyManager.GetConnectedPlayerCount();
        bool allPlayersReady = lobbyManager.AreAllPlayersReady();
        
        Debug.Log($"AutoTestRunner: Checking lobby conditions - Players: {playerCount}, All Ready: {allPlayersReady}");
        
        // Conditions: At least 2 players and all players are ready
        if (playerCount >= 2 && allPlayersReady)
        {
            readyToStartGame = true;
            Debug.Log("AutoTestRunner: Lobby conditions met! Two or more players are present and all are ready.");
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (lobbyManager != null)
        {
            lobbyManager.OnPlayersReadyStateChanged -= CheckLobbyConditions;
        }
    }
} 