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
        InitializeButtons();
        FindRequiredComponents();
    }
    
    private void InitializeButtons()
    {
        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyButtonClicked);
        }
        
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartButtonClicked);
            startButton.interactable = false;
        }
    }
    
    private void FindRequiredComponents()
    {
        if (gamePhaseManager == null)
        {
            gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
        }
        
        if (combatSetup == null)
        {
            combatSetup = FindFirstObjectByType<CombatSetup>();
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
        ResetLobbyUIState();
    }
    
    private void ResetLobbyUIState()
    {
        // Reset player list text
        if (playerListText != null)
        {
            playerListText.text = "Players in Lobby:\n(Waiting for server data...)";
        }
        
        // Reset button states
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