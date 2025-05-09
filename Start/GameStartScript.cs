using UnityEngine;

/// <summary>
/// Initial entry point for the game. This script should be attached to a GameObject present at game start.
/// It initializes the GamePhaseManager which then orchestrates the overall game flow.
/// </summary>
public class GameStartScript : MonoBehaviour
{
    [SerializeField] private StartScreenManager startScreenManager;
    [SerializeField] private GamePhaseManager gamePhaseManager;

    void Start()
    {
        // Initialize GamePhaseManager first - this is our primary flow controller
        if (gamePhaseManager == null)
        {
            gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
            if (gamePhaseManager == null)
            {
                Debug.LogError("GameStartScript: GamePhaseManager not found in the scene. Phase transitions won't work correctly.");
            }
        }
        
        // Initialize StartScreenManager if available - kept for backwards compatibility
        if (startScreenManager != null)
        {
            // Set GamePhaseManager reference and initialize
            startScreenManager.SetGamePhaseManager(gamePhaseManager);
            startScreenManager.InitializeGame();
        }
        else if (gamePhaseManager != null)
        {
            // If no StartScreenManager but we have GamePhaseManager, directly set start phase
            Debug.LogWarning("GameStartScript: No StartScreenManager found, directly setting Start phase via GamePhaseManager");
            gamePhaseManager.SetStartPhase();
        }
        else
        {
            Debug.LogError("GameStartScript: Neither StartScreenManager nor GamePhaseManager available. Game initialization failed.");
        }
    }
} 