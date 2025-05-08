using UnityEngine;

/// <summary>
/// Orchestrates the game startup sequence and handles game-level initialization.
/// Attach to: A dedicated StartManager GameObject.
/// </summary>
public class StartScreenManager : MonoBehaviour
{
    [SerializeField] private StartScreenUIManager uiManager;
    [SerializeField] private bool allowOfflinePlay = true; // Add option for offline play
    
    private SteamNetworkIntegration steamNetworkIntegration;
    private bool isSteamAvailable = false;

    void Awake()
    {
        if (uiManager == null)
        {
            Debug.LogError("StartScreenManager: StartScreenUIManager reference is not assigned in the Inspector.");
        }
    }

    void Start()
    {
        // Try to find the SteamNetworkIntegration instance
        steamNetworkIntegration = SteamNetworkIntegration.Instance;
        if (steamNetworkIntegration == null)
        {
            Debug.LogError("StartScreenManager: SteamNetworkIntegration instance not found. Steam lobby functionality may not work.");
            isSteamAvailable = false;
        }
        else
        {
            isSteamAvailable = steamNetworkIntegration.IsSteamInitialized;
            if (!isSteamAvailable)
            {
                Debug.LogWarning("StartScreenManager: Steam is not initialized. Using offline mode if allowed.");
            }
        }
        
        // Update UI to reflect Steam availability
        if (uiManager != null)
        {
            uiManager.UpdateSteamAvailabilityStatus(isSteamAvailable, allowOfflinePlay);
        }
    }

    /// <summary>
    /// Called by GameStartScript to begin the game initialization process
    /// </summary>
    public void InitializeGame()
    {
        Debug.Log("StartScreenManager: Initializing game...");
        
        // Initialize any game systems here before showing UI
        
        // Trigger the UI manager to show the start screen
        if (uiManager != null)
        {
            uiManager.ShowStartScreen();
        }
    }

    /// <summary>
    /// Called by StartScreenUIManager when the start button is pressed
    /// </summary>
    public void OnStartGameRequested()
    {
        if (isSteamAvailable && steamNetworkIntegration != null)
        {
            Debug.Log("StartScreenManager: Start button pressed, attempting to host or join a Steam lobby...");
            steamNetworkIntegration.RequestLobbiesList();
        }
        else if (allowOfflinePlay)
        {
            Debug.Log("StartScreenManager: Start button pressed, starting in offline mode...");
            // Handle offline mode - perhaps load a different scene or start in single player
            // For example: SceneManager.LoadScene("OfflineGameScene");
        }
        else
        {
            Debug.LogError("StartScreenManager: Cannot initiate game: Steam unavailable and offline play not allowed.");
            if (uiManager != null)
            {
                uiManager.ShowErrorMessage("Steam is not available. Please make sure Steam is running and try again.");
                return; // Don't proceed to lobby
            }
        }

        // Notify UI manager to transition to the lobby screen
        if (uiManager != null)
        {
            uiManager.TransitionToLobbyScreen();
        }
    }
} 