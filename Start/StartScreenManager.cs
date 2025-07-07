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
        RegisterStartScreenCanvas();
    }
    
    /// <summary>
    /// Initialize the game, show the start screen, and check for Steam
    /// </summary>
    public void InitializeGame()
    {
        ValidateReferences();
        CheckSteamAvailability();
        ShowStartScreen();
    }
    
    private void ValidateReferences()
    {
        if (startScreenUIManager == null)
        {
            Debug.LogError("StartScreenManager: StartScreenUIManager reference is null.");
            return;
        }
        
        if (gamePhaseManager == null)
        {
            gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
            RegisterStartScreenCanvas();
        }
    }
    
    private void RegisterStartScreenCanvas()
    {
        if (gamePhaseManager != null && startScreenCanvas != null)
        {
            gamePhaseManager.SetStartScreenCanvas(startScreenCanvas);
        }
    }
    
    private void ShowStartScreen()
    {
        if (gamePhaseManager != null)
        {
            gamePhaseManager.SetStartPhase();
        }
        else
        {
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
            TransitionToCharacterSelection();
        }
        else
        {
            startScreenUIManager.ShowErrorMessage("Steam is not available. Please start Steam and try again.");
        }
    }
    
    /// <summary>
    /// Transition from start screen directly to character selection (skipping lobby)
    /// </summary>
    private void TransitionToCharacterSelection()
    {
        // Always transition the UI first (this will hide start screen)
        startScreenUIManager.TransitionToCharacterSelectionScreen();
        
        // Set the game phase to character selection (skipping lobby)
        if (gamePhaseManager != null)
        {
            gamePhaseManager.SetCharacterSelectionPhase();
    
        }
        else
        {
            Debug.LogError("StartScreenManager: GamePhaseManager not found, cannot transition phase");
        }
    }
} 