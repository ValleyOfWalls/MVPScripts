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
    
    // UI elements
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorMessageText;
    [SerializeField] private Button errorCloseButton;
    [SerializeField] private TextMeshProUGUI steamStatusText;

    void Awake()
    {
        InitializeUIComponents();
        SetInitialUIState();
        
        // Subscribe to LobbyManager availability event
        LobbyManager.OnLobbyManagerAvailable += OnLobbyManagerBecameAvailable;
    }
    
    void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        LobbyManager.OnLobbyManagerAvailable -= OnLobbyManagerBecameAvailable;
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
    /// Transitions from start screen to lobby screen
    /// </summary>
    public void TransitionToLobbyScreen()
    {
        if (startScreenCanvas != null)
        {
            startScreenCanvas.SetActive(false);
        }

        // Try to prepare lobby UI immediately
        if (lobbyUIManager != null)
        {
            lobbyUIManager.PrepareUIForLobbyJoin();
            Debug.Log("StartScreenUIManager: Lobby UI prepared via assigned reference");
        }
        else
        {
            // If no assigned reference, try to find it in the scene
            LobbyUIManager foundLobbyUIManager = FindFirstObjectByType<LobbyUIManager>();
            if (foundLobbyUIManager != null)
            {
                foundLobbyUIManager.PrepareUIForLobbyJoin();
                Debug.Log("StartScreenUIManager: Lobby UI prepared via scene search");
            }
            else
            {
                Debug.Log("StartScreenUIManager: Could not find LobbyUIManager immediately. Waiting for LobbyManager to become available via event.");
                // The event subscription will handle this when LobbyManager becomes available
            }
        }
    }
    
    /// <summary>
    /// Called when LobbyManager becomes available via the static event
    /// </summary>
    private void OnLobbyManagerBecameAvailable(LobbyManager lobbyManager)
    {
        if (lobbyManager != null && lobbyManager.GetComponent<LobbyUIManager>() != null)
        {
            LobbyUIManager lobbyUI = lobbyManager.GetComponent<LobbyUIManager>();
            lobbyUI.PrepareUIForLobbyJoin();
            Debug.Log("StartScreenUIManager: Lobby UI prepared via LobbyManager availability event");
        }
        else
        {
            // Try to find it in the scene as a fallback
            LobbyUIManager foundLobbyUIManager = FindFirstObjectByType<LobbyUIManager>();
            if (foundLobbyUIManager != null)
            {
                foundLobbyUIManager.PrepareUIForLobbyJoin();
                Debug.Log("StartScreenUIManager: Lobby UI prepared via scene search after LobbyManager event");
            }
        }
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