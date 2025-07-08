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

    [Tooltip("Enable this flag to skip the fight preview interstitial screen and go straight from character select to combat.")]
    public bool skipFightPreview = false;
    
    [Tooltip("Enable this flag to skip the fight conclusion interstitial screen and go straight from combat to draft.")]
    public bool skipFightConclusion = false;

    [Tooltip("Enable this flag to display current FPS alongside host/client status text.")]
    public bool showFPS = false;

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
    
    [Tooltip("Drag the CharacterSelectionManager here.")]
    public CharacterSelectionManager characterSelectionManager;
    
    [Tooltip("Drag the GamePhaseManager here.")]
    public GamePhaseManager gamePhaseManager;

    [Header("UI References")]
    [Tooltip("Text element to display host/client status. Will be enabled when combat setup is complete.")]
    public TextMeshProUGUI hostClientStatusText;

    private bool startButtonClicked = false;
    private bool readyToStartGame = false;
    private bool combatSetupComplete = false;
    private bool characterSelectionComplete = false;

    // FPS tracking
    private float currentFPS = 0f;
    private Coroutine fpsUpdateCoroutine;

    void Start()
    {
        // Initially hide the host/client status text
        if (hostClientStatusText != null)
        {
            hostClientStatusText.gameObject.SetActive(false);
        }
        
        // Find missing components if not assigned
        if (combatSetup == null)
        {
            combatSetup = FindFirstObjectByType<CombatSetup>();
        }
        
        if (characterSelectionManager == null)
        {
            characterSelectionManager = FindFirstObjectByType<CharacterSelectionManager>();
        }
        
        if (gamePhaseManager == null)
        {
            gamePhaseManager = GamePhaseManager.Instance;
        }
        
        if (enableAutoTesting)
        {
            StartCoroutine(WaitForStartScreenReadiness());
        }
        
        // Start monitoring combat setup regardless of auto testing
        if (enableHostClientDisplay)
        {
            StartCoroutine(MonitorCombatSetup());
        }
    }

    private IEnumerator MonitorCombatSetup()
    {
        // Wait until CombatSetup is available
        yield return new WaitUntil(() => combatSetup != null);
        
        // Wait for setup completion without verbose logging
        while (!combatSetup.IsSetupComplete)
        {
            yield return null;
        }
        
        combatSetupComplete = true;
        DisplayHostClientStatus();
    }

    private void DisplayHostClientStatus()
    {
        if (!enableHostClientDisplay || hostClientStatusText == null)
        {
            return;
        }
        
        string statusText = GetNetworkStatusText();
        
        // Update the display text
        UpdateStatusDisplay(statusText);
        
        hostClientStatusText.gameObject.SetActive(true);
        
        // Start FPS updating if enabled and not already running
        if (showFPS && fpsUpdateCoroutine == null)
        {
            fpsUpdateCoroutine = StartCoroutine(UpdateFPSDisplay());
        }
    }

    private string GetNetworkStatusText()
    {
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
        
        return statusText;
    }

    private void UpdateStatusDisplay(string statusText)
    {
        if (hostClientStatusText == null) return;
        
        // Append FPS if enabled
        string displayText = $"Status: {statusText}";
        if (showFPS)
        {
            displayText += $" | FPS: {currentFPS:F1}";
        }
        
        hostClientStatusText.text = displayText;
    }

    private void LogFrameRateSettings()
    {
        // Simplified frame rate logging - only when needed
        if (OfflineGameManager.Instance != null)
        {
            OfflineGameManager.Instance.LogCurrentSettings();
        }
    }

    private IEnumerator WaitForStartScreenReadiness()
    {
        // Wait for Steam initialization
        if (steamNetworkIntegration != null)
        {
            yield return new WaitUntil(() => steamNetworkIntegration.IsSteamInitialized);
        }
        else
        {
            Debug.LogWarning("AutoTestRunner: SteamNetworkIntegration reference not set, skipping Steam initialization check.");
        }
        
        // Wait until the start button is available and interactable
        yield return new WaitUntil(() => startScreenStartButton != null && startScreenStartButton.gameObject.activeInHierarchy && startScreenStartButton.interactable);
        
        // Click the start button
        startScreenStartButton.onClick.Invoke();
        startButtonClicked = true;
        
        // Start monitoring lobby conditions
        StartCoroutine(WaitForLobbyReadiness());
    }

    private IEnumerator WaitForLobbyReadiness()
    {
        // Wait until lobby manager is ready
        yield return new WaitUntil(() => lobbyManager != null && lobbyManager.gameObject.activeInHierarchy);
        
        // Subscribe to lobby events
        lobbyManager.OnPlayersReadyStateChanged += CheckLobbyConditions;
        
        // Wait until conditions are met to start the game
        yield return new WaitUntil(() => readyToStartGame);
        
        // Only the host should click the start button
        if (steamNetworkIntegration != null && steamNetworkIntegration.IsUserSteamHost && lobbyStartGameButton != null && lobbyStartGameButton.interactable)
        {
            lobbyStartGameButton.onClick.Invoke();
        }
        else
        {

        }
        
        // Start monitoring character selection phase
        StartCoroutine(WaitForCharacterSelectionPhase());
    }

    private IEnumerator WaitForCharacterSelectionPhase()
    {
        
        
        // Wait for character selection phase to start
        yield return new WaitUntil(() => gamePhaseManager != null && gamePhaseManager.GetCurrentPhase() == GamePhaseManager.GamePhase.CharacterSelection);
        
        
        // Wait for character selection manager to be ready
        yield return new WaitUntil(() => characterSelectionManager != null);
        
        
        // Wait a bit for all players to connect to character selection
        yield return new WaitForSeconds(1.0f);
        
        // Try to subscribe to character selection events
        try
        {
            characterSelectionManager.OnPlayersReadyStateChanged += CheckCharacterSelectionConditions;
            
        }
        catch (System.Exception)
        {
            // Using polling method instead
            StartCoroutine(PollCharacterSelectionConditions());
        }
        

        
        // Wait until all players are ready in character selection
        yield return new WaitUntil(() => characterSelectionComplete);
        
        /* Debug.Log("AutoTestRunner: Character selection phase completed, waiting for combat transition..."); */
    }

    private IEnumerator PollCharacterSelectionConditions()
    {
        while (!characterSelectionComplete)
        {
            yield return new WaitForSeconds(1.0f);
            CheckCharacterSelectionConditions();
        }
    }

    private void CheckLobbyConditions()
    {
        if (lobbyManager == null) return;
        
        int playerCount = lobbyManager.GetConnectedPlayerCount();
        bool allPlayersReady = lobbyManager.AreAllPlayersReady();
        
        /* Debug.Log($"AutoTestRunner: Checking lobby conditions - Players: {playerCount}, All Ready: {allPlayersReady}"); */
        
        // Conditions: At least 2 players and all players are ready
        if (playerCount >= 2 && allPlayersReady)
        {
            readyToStartGame = true;
            Debug.Log("AutoTestRunner: Lobby conditions met! Two or more players are present and all are ready.");
        }
    }

    private void CheckCharacterSelectionConditions()
    {
        if (characterSelectionManager == null) return;
        
        int playerCount = characterSelectionManager.GetConnectedPlayerCount();
        
        /* Debug.Log($"AutoTestRunner: Checking character selection conditions - Players: {playerCount}"); */
        
        // For character selection, we need at least 2 players and the UI manager handles auto-selection/ready
        // We'll assume character selection is complete when we have enough players
        // The CharacterSelectionUIManager automatically selects and readies players when enableAutoTesting is true
        if (playerCount >= 2)
        {
            characterSelectionComplete = true;
            Debug.Log("AutoTestRunner: Character selection conditions met! Players have been auto-selected and readied.");
        }
    }

    private IEnumerator UpdateFPSDisplay()
    {
        while (showFPS && hostClientStatusText != null && hostClientStatusText.gameObject.activeInHierarchy)
        {
            // Calculate FPS
            currentFPS = 1.0f / Time.deltaTime;
            
            // Update only the display text directly, don't call DisplayHostClientStatus to avoid recursion
            if (enableHostClientDisplay)
            {
                string statusText = GetNetworkStatusText();
                UpdateStatusDisplay(statusText);
            }
            
            // Update every 0.5 seconds to avoid too frequent updates
            yield return new WaitForSeconds(0.5f);
        }
        
        fpsUpdateCoroutine = null;
    }

    private void OnDestroy()
    {
        // Stop FPS update coroutine
        if (fpsUpdateCoroutine != null)
        {
            StopCoroutine(fpsUpdateCoroutine);
            fpsUpdateCoroutine = null;
        }
        
        // Unsubscribe from events
        if (lobbyManager != null)
        {
            lobbyManager.OnPlayersReadyStateChanged -= CheckLobbyConditions;
        }
        
        if (characterSelectionManager != null)
        {
            // Use try-catch to handle event unsubscription safely
            try
            {
                characterSelectionManager.OnPlayersReadyStateChanged -= CheckCharacterSelectionConditions;
            }
            catch (System.Exception)
            {
                /* Debug.Log($"AutoTestRunner: Could not unsubscribe from character selection events: {e.Message}"); */
            }
        }
    }
    
    /// <summary>
    /// Public getter to check if fight preview should be skipped
    /// </summary>
    public bool ShouldSkipFightPreview => skipFightPreview;
    
    /// <summary>
    /// Public getter to check if fight conclusion should be skipped
    /// </summary>
    public bool ShouldSkipFightConclusion => skipFightConclusion;
} 