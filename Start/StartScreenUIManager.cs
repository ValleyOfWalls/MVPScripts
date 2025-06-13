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
    [SerializeField] private StartScreenManager startScreenManager;
    
    // UI elements
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorMessageText;
    [SerializeField] private Button errorCloseButton;
    [SerializeField] private TextMeshProUGUI steamStatusText;

    void Awake()
    {
        InitializeUIComponents();
        SetInitialUIState();
        
        // No longer need to subscribe to LobbyManager availability since we skip lobby phase
    }
    
    void OnDestroy()
    {
        // No longer need to unsubscribe from LobbyManager events
    }

    private void InitializeUIComponents()
    {
        if (errorPanel != null && errorCloseButton != null)
        {
            errorCloseButton.onClick.AddListener(() => errorPanel.SetActive(false));
        }
        
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartButtonClicked);
        }
    }
    
    private void SetInitialUIState()
    {
        if (errorPanel != null) errorPanel.SetActive(false);
        if (startScreenCanvas != null) startScreenCanvas.SetActive(false);
    }

    /// <summary>
    /// Shows the start screen UI
    /// </summary>
    public void ShowStartScreen()
    {
        if (startScreenCanvas != null)
        {
            startScreenCanvas.SetActive(true);
        }
    }
    
    /// <summary>
    /// Updates UI elements based on Steam availability
    /// </summary>
    public void UpdateSteamAvailabilityStatus(bool isSteamAvailable, bool allowOfflinePlay)
    {
        UpdateSteamStatusText(isSteamAvailable, allowOfflinePlay);
        UpdateStartButtonState(isSteamAvailable, allowOfflinePlay);
    }
    
    private void UpdateSteamStatusText(bool isSteamAvailable, bool allowOfflinePlay)
    {
        if (steamStatusText == null) return;
        
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
    
    private void UpdateStartButtonState(bool isSteamAvailable, bool allowOfflinePlay)
    {
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
    }

    /// <summary>
    /// Transitions from start screen to character selection screen (skipping lobby)
    /// </summary>
    public void TransitionToCharacterSelectionScreen()
    {
        if (startScreenCanvas != null)
        {
            startScreenCanvas.SetActive(false);
        }


        
        // The character selection setup will handle showing the character selection UI
        // when the phase transitions occur
    }

    /// <summary>
    /// Transitions from start screen to lobby screen
    /// </summary>
    public void TransitionToLobbyScreen()
    {
        // DEPRECATED: Now transitions directly to character selection
        TransitionToCharacterSelectionScreen();
    }
    
    /// <summary>
    /// Called when LobbyManager becomes available via the static event
    /// </summary>
    private void OnLobbyManagerBecameAvailable(LobbyManager lobbyManager)
    {
        // DEPRECATED: No longer used since we skip lobby phase

    }

    /// <summary>
    /// Called when the start button is clicked
    /// </summary>
    private void OnStartButtonClicked()
    {
        if (startScreenManager != null)
        {
            startScreenManager.OnStartGameRequested();
        }
    }
} 