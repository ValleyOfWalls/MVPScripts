using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the UI elements and interactions for the start screen.
/// Attach to: A UIManager GameObject that manages all UI elements.
/// </summary>
public class StartScreenUIManager : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private GameObject startScreenCanvas;
    [SerializeField] private LobbyUIManager lobbyUIManager;
    [SerializeField] private StartScreenManager startScreenManager;
    
    // New UI elements
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorMessageText;
    [SerializeField] private Button errorCloseButton;
    [SerializeField] private TextMeshProUGUI steamStatusText;

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

        if (lobbyUIManager == null)
        {
            Debug.LogError("StartScreenUIManager: LobbyUIManager reference is not assigned in the Inspector.");
        }

        if (startScreenManager == null)
        {
            Debug.LogError("StartScreenUIManager: StartScreenManager reference is not assigned in the Inspector.");
        }
        
        // Initialize error panel
        if (errorPanel != null)
        {
            errorPanel.SetActive(false);
            
            if (errorCloseButton != null)
            {
                errorCloseButton.onClick.AddListener(() => errorPanel.SetActive(false));
            }
        }

        // Ensure start screen is not visible by default
        if (startScreenCanvas != null)
        {
            startScreenCanvas.SetActive(false);
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
    /// Updates UI elements based on Steam availability
    /// </summary>
    public void UpdateSteamAvailabilityStatus(bool isSteamAvailable, bool allowOfflinePlay)
    {
        // Update Steam status text if available
        if (steamStatusText != null)
        {
            if (isSteamAvailable)
            {
                steamStatusText.text = "Steam: Connected";
                steamStatusText.color = Color.green;
            }
            else
            {
                steamStatusText.text = "Steam: Offline" + (allowOfflinePlay ? " (Offline Play Enabled)" : "");
                steamStatusText.color = allowOfflinePlay ? Color.yellow : Color.red;
            }
        }
        
        // Update start button interactability if needed
        if (startButton != null)
        {
            startButton.interactable = isSteamAvailable || allowOfflinePlay;
        }
    }
    
    /// <summary>
    /// Shows an error message to the user
    /// </summary>
    public void ShowErrorMessage(string message)
    {
        if (errorPanel != null && errorMessageText != null)
        {
            errorMessageText.text = message;
            errorPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("StartScreenUIManager: Cannot show error message - UI elements missing");
            Debug.LogError("Error: " + message);
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

        if (lobbyUIManager != null)
        {
            lobbyUIManager.PrepareUIForLobbyJoin();
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