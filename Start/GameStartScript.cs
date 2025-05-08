using UnityEngine;

/// <summary>
/// Initial entry point for the game. This script should be attached to a GameObject present at game start.
/// It triggers the StartScreenManager which then orchestrates the UI flow.
/// </summary>
public class GameStartScript : MonoBehaviour
{
    [SerializeField] private StartScreenManager startScreenManager;

    void Start()
    {
        if (startScreenManager != null)
        {
            startScreenManager.InitializeGame();
        }
        else
        {
            Debug.LogError("GameStartScript: StartScreenManager reference is not assigned in the Inspector.");
        }
    }
} 