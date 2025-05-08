using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the UI elements and interactions for the start screen.
/// Attach to: A UIManager GameObject that manages all UI elements.
/// </summary>
public class StartScreenUIManager : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private GameObject startScreenCanvas;
    [SerializeField] private GameObject lobbyCanvas;
    [SerializeField] private StartScreenManager startScreenManager;

    void Awake()
    {
        // Validate required references
        if (startButton == null)
        {
            Debug.LogError("StartScreenUIManager: Start Button is not assigned in the Inspector.");
        }
        else
        {
            startButton.onClick.AddListener(OnStartButtonClicked);
        }

        if (startScreenCanvas == null)
        {
            Debug.LogError("StartScreenUIManager: Start Screen Canvas is not assigned in the Inspector.");
        }

        if (lobbyCanvas == null)
        {
            Debug.LogError("StartScreenUIManager: Lobby Canvas is not assigned in the Inspector.");
        }

        if (startScreenManager == null)
        {
            Debug.LogError("StartScreenUIManager: StartScreenManager reference is not assigned in the Inspector.");
        }

        // Ensure start screen is not visible by default
        if (startScreenCanvas != null)
        {
            startScreenCanvas.SetActive(false);
        }

        // Ensure lobby canvas is not visible by default
        if (lobbyCanvas != null)
        {
            lobbyCanvas.SetActive(false);
        }
    }

    /// <summary>
    /// Shows the start screen UI
    /// </summary>
    public void ShowStartScreen()
    {
        if (startScreenCanvas != null)
        {
            startScreenCanvas.SetActive(true);
            Debug.Log("StartScreenUIManager: Start screen displayed");
        }
    }

    /// <summary>
    /// Transitions from start screen to lobby screen
    /// </summary>
    public void TransitionToLobbyScreen()
    {
        if (startScreenCanvas != null)
        {
            startScreenCanvas.SetActive(false);
        }

        if (lobbyCanvas != null)
        {
            lobbyCanvas.SetActive(true);
            Debug.Log("StartScreenUIManager: Transitioned to lobby screen");
        }
    }

    /// <summary>
    /// Called when the start button is clicked
    /// </summary>
    private void OnStartButtonClicked()
    {
        Debug.Log("StartScreenUIManager: Start button clicked");
        
        if (startScreenManager != null)
        {
            startScreenManager.OnStartGameRequested();
        }
    }
} 