using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Manages the UI elements and interactions for the lobby screen.
/// Attach to: The UIManager GameObject that manages all UI elements.
/// </summary>
public class LobbyUIManager : MonoBehaviour
{
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startButton;
    [SerializeField] private GameObject lobbyCanvas;
    [SerializeField] private TextMeshProUGUI playerListText;
    
    [SerializeField] private LobbyManager lobbyManager;
    [SerializeField] private GamePhaseManager gamePhaseManager;
    [SerializeField] private CombatSetup combatSetup;

    private void Awake()
    {
        // Validate required references
        if (readyButton == null)
        {
            Debug.LogError("LobbyUIManager: Ready Button is not assigned in the Inspector.");
        }
        else
        {
            readyButton.onClick.AddListener(OnReadyButtonClicked);
        }

        if (startButton == null)
        {
            Debug.LogError("LobbyUIManager: Start Button is not assigned in the Inspector.");
        }
        else
        {
            startButton.onClick.AddListener(OnStartButtonClicked);
            startButton.interactable = false; // Disabled until conditions are met
        }

        if (lobbyCanvas == null) Debug.LogError("LobbyUIManager: Lobby Canvas is not assigned in the Inspector.");
        if (playerListText == null) Debug.LogError("LobbyUIManager: Player List Text is not assigned in the Inspector.");
        if (lobbyManager == null) Debug.LogError("LobbyUIManager: LobbyManager reference is not assigned in the Inspector.");
        
        // Find the GamePhaseManager if not set in inspector
        if (gamePhaseManager == null)
        {
            gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
            if (gamePhaseManager == null)
            {
                Debug.LogWarning("LobbyUIManager: GamePhaseManager not found. Phase transitions may not work correctly.");
            }
        }
        
        // Find the CombatSetup if not set in inspector
        if (combatSetup == null)
        {
            combatSetup = FindFirstObjectByType<CombatSetup>();
            if (combatSetup == null)
            {
                Debug.LogWarning("LobbyUIManager: CombatSetup not found. Combat initialization may not work correctly.");
            }
        }
    }

    private void Start()
    {
        // Ensure lobby canvas is not initially visible
        if (lobbyCanvas != null)
        {
            lobbyCanvas.SetActive(false);
        }
    }

    /// <summary>
    /// Called when the ready button is clicked
    /// </summary>
    private void OnReadyButtonClicked()
    {
        if (lobbyManager != null)
        {
            lobbyManager.CmdTogglePlayerReadyState();
        }
    }

    /// <summary>
    /// Called when the start button is clicked
    /// </summary>
    private void OnStartButtonClicked()
    {
        if (lobbyManager != null)
        {
            lobbyManager.RequestStartGame();
        }
        else
        {
            Debug.LogError("LobbyUIManager: LobbyManager reference is null. Cannot start game.");
        }
    }

    /// <summary>
    /// Updates the player list displayed in the UI
    /// </summary>
    public void UpdatePlayerListUI(List<string> displayNamesToShow)
    {
        if (playerListText != null)
        {
            playerListText.text = "Players in Lobby:\n" + string.Join("\n", displayNamesToShow);
        }
    }

    /// <summary>
    /// Sets the interactable state of the start button
    /// </summary>
    public void SetStartButtonInteractable(bool interactable)
    {
        if (startButton != null)
        {
            startButton.interactable = interactable;
        }
    }

    /// <summary>
    /// Hides the lobby UI
    /// </summary>
    public void HideLobbyUI()
    {
        if (lobbyCanvas != null)
        {
            lobbyCanvas.SetActive(false);
        }
    }

    /// <summary>
    /// Shows the lobby UI
    /// </summary>
    public void ShowLobbyUI()
    {
        if (lobbyCanvas != null)
        {
            lobbyCanvas.SetActive(true);
        }
    }

    /// <summary>
    /// Prepares the UI for joining a lobby
    /// </summary>
    public void PrepareUIForLobbyJoin()
    {
        ShowLobbyUI();
        
        // Reset any UI elements as needed
        if (playerListText != null)
        {
            playerListText.text = "Players in Lobby:\n(Waiting for server data...)";
        }
        
        if (readyButton != null)
        {
            readyButton.interactable = true;
        }
        
        if (startButton != null)
        {
            startButton.interactable = false;
        }
    }
} 