using UnityEngine;
using FishNet;

/// <summary>
/// Manages the initial game setup and start screen functionality.
/// Attach to: A networked object in the scene that manages initialization flow.
/// </summary>
public class StartScreenManager : MonoBehaviour
{
    [SerializeField] private StartScreenUIManager startScreenUIManager;
    [SerializeField] private GameObject startScreenCanvas;
    
    // Reference to the GamePhaseManager
    private GamePhaseManager gamePhaseManager;
    
    // Server status flags
    private bool isSteamAvailable = false;
    private bool allowOfflinePlay = true;
    
    public void SetGamePhaseManager(GamePhaseManager phaseManager)
    {
        gamePhaseManager = phaseManager;
        
        // Register the start screen canvas with the GamePhaseManager
        if (gamePhaseManager != null && startScreenCanvas != null)
        {
            gamePhaseManager.SetStartScreenCanvas(startScreenCanvas);
        }
    }
    
    /// <summary>
    /// Initialize the game, show the start screen, and check for Steam
    /// </summary>
    public void InitializeGame()
    {
        // Check if we have required references
        if (startScreenUIManager == null)
        {
            Debug.LogError("StartScreenManager: StartScreenUIManager reference is null.");
            return;
        }
        
        if (gamePhaseManager == null)
        {
            gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
            
            if (gamePhaseManager == null)
            {
                Debug.LogError("StartScreenManager: GamePhaseManager not found in the scene!");
            }
            else if (startScreenCanvas != null)
            {
                gamePhaseManager.SetStartScreenCanvas(startScreenCanvas);
            }
        }
        
        // Check for Steam availability
        CheckSteamAvailability();
        
        // Show start screen
        if (gamePhaseManager != null)
        {
            gamePhaseManager.SetStartPhase();
        }
        else
        {
            // Fall back to direct canvas activation if GamePhaseManager isn't available
            startScreenUIManager.ShowStartScreen();
        }
    }
    
    /// <summary>
    /// Check if Steam is available
    /// </summary>
    private void CheckSteamAvailability()
    {
        // In a real implementation, this would check SteamNetworkIntegration
        // For now, we'll just assume Steam is available
        isSteamAvailable = true;
        
        // Update UI based on Steam availability
        startScreenUIManager.UpdateSteamAvailabilityStatus(isSteamAvailable, allowOfflinePlay);
    }
    
    /// <summary>
    /// Called when the player clicks the Start Game button
    /// </summary>
    public void OnStartGameRequested()
    {
        if (isSteamAvailable || allowOfflinePlay)
        {
            // Begin transition to lobby
            TransitionToLobby();
        }
        else
        {
            startScreenUIManager.ShowErrorMessage("Steam is not available. Please start Steam and try again.");
        }
    }
    
    /// <summary>
    /// Transition from start screen to lobby
    /// </summary>
    private void TransitionToLobby()
    {
        if (gamePhaseManager != null)
        {
            // Tell the GamePhaseManager to set the Lobby phase
            gamePhaseManager.SetLobbyPhase();
        }
        else
        {
            // Fall back to direct UI transition if GamePhaseManager isn't available
            startScreenUIManager.TransitionToLobbyScreen();
        }
    }
} 