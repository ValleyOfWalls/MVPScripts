using UnityEngine;
using MVPScripts.Utility;

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
        InitializeGamePhaseManager();
        InitializeStartScreenManager();
    }
    
    private void InitializeGamePhaseManager()
    {
        if (gamePhaseManager != null) return;
        
        ComponentResolver.FindComponentWithSingleton(ref gamePhaseManager, () => GamePhaseManager.Instance, gameObject);
        if (gamePhaseManager == null)
        {
            Debug.LogError("GamePhaseManager not found in the scene. Phase transitions won't work correctly.");
        }
    }
    
    private void InitializeStartScreenManager()
    {
        if (startScreenManager != null)
        {
            startScreenManager.SetGamePhaseManager(gamePhaseManager);
            startScreenManager.InitializeGame();
        }
        else if (gamePhaseManager != null)
        {
            // If no StartScreenManager but we have GamePhaseManager, directly set start phase
            gamePhaseManager.SetStartPhase();
        }
    }
} 