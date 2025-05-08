using UnityEngine;

/// <summary>
/// Orchestrates the game startup sequence and handles game-level initialization.
/// Attach to: A dedicated StartManager GameObject.
/// </summary>
public class StartScreenManager : MonoBehaviour
{
    [SerializeField] private StartScreenUIManager uiManager;
    
    private SteamNetworkIntegration steamNetworkIntegration;

    void Awake()
    {
        // Try to find the SteamNetworkIntegration instance
        steamNetworkIntegration = SteamNetworkIntegration.Instance;
        if (steamNetworkIntegration == null)
        {
            Debug.LogError("StartScreenManager: SteamNetworkIntegration instance not found. Steam lobby functionality may not work.");
        }

        if (uiManager == null)
        {
            Debug.LogError("StartScreenManager: StartScreenUIManager reference is not assigned in the Inspector.");
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
        if (steamNetworkIntegration != null)
        {
            Debug.Log("StartScreenManager: Start button pressed, attempting to host or join a Steam lobby...");
            steamNetworkIntegration.RequestLobbiesList();
        }
        else
        {
            Debug.LogError("StartScreenManager: Cannot initiate Steam lobby: SteamNetworkIntegration instance is null.");
        }

        // Notify UI manager to transition to the lobby screen
        if (uiManager != null)
        {
            uiManager.TransitionToLobbyScreen();
        }
    }
} 