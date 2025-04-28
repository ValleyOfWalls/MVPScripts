using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;
using FishNet;

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance { get; private set; }

    [Header("Lobby Screen")]
    [SerializeField] private GameObject lobbyCanvas;
    [SerializeField] private TextMeshProUGUI lobbyTitleText;
    [SerializeField] private Transform playerListPanel;
    [SerializeField] private TextMeshProUGUI playerEntryTemplate;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveButton;

    // State tracking
    private bool isLocalPlayerReady = false;
    private List<PlayerInfo> currentPlayers = new List<PlayerInfo>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Another LobbyUIManager instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Hide player entry template
        if (playerEntryTemplate != null)
            playerEntryTemplate.gameObject.SetActive(false);
        else
            Debug.LogError("PlayerEntryTemplate is not assigned in the LobbyUIManager Inspector!");
    }

    private void Start()
    {
        // Setup button listeners
        if (readyButton != null)
            readyButton.onClick.AddListener(OnReadyButtonClicked);
        else 
            Debug.LogError("ReadyButton is not assigned in the LobbyUIManager Inspector!");

        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(OnStartGameButtonClicked);
            startGameButton.gameObject.SetActive(false); // Hide start button initially
        }
        else 
            Debug.LogError("StartGameButton is not assigned in the LobbyUIManager Inspector!");

        if (leaveButton != null)
            leaveButton.onClick.AddListener(OnLeaveButtonClicked);
         else 
            Debug.LogError("LeaveButton is not assigned in the LobbyUIManager Inspector!");
    }

    public void ShowLobbyCanvas()
    {
        if (lobbyCanvas != null)
            lobbyCanvas.SetActive(true);

        // Reset ready state
        isLocalPlayerReady = false;
        UpdateReadyButtonState(isLocalPlayerReady);
        
        // Clear player list
        ClearPlayerList();
    }

    public void HideLobbyCanvas()
    {
        if (lobbyCanvas != null)
            lobbyCanvas.SetActive(false);
    }

    private void OnReadyButtonClicked()
    {
        // Get the local NetworkPlayer and toggle its readiness via command
        LobbyPlayerInfo localPlayer = GetLocalLobbyPlayer();
        if (localPlayer != null)
        {
            Debug.Log("Toggling ready state via LobbyPlayerInfo");
            localPlayer.ToggleReady();
            // Update the button text immediately for responsiveness
            // The actual state sync comes from the server via SyncVar callback
            isLocalPlayerReady = !isLocalPlayerReady;
            UpdateReadyButtonState(isLocalPlayerReady);
        }
        else
        {
            Debug.LogError("Could not find local LobbyPlayerInfo to toggle ready status.");
            
            // Check if player has been spawned yet
            if (NetworkPlayer.Players.Count == 0)
            {
                Debug.LogWarning("No NetworkPlayers exist yet. Player may not be properly spawned.");
            }
            
            // Provide visual feedback that the action failed
            if (readyButton != null)
            {
                // Visual indication that the button press was registered but failed
                AnimateInvalidButtonInput(readyButton);
            }
        }
    }

    public void UpdateReadyButtonState(bool isReady)
    {
         isLocalPlayerReady = isReady; // Update internal state
         if (readyButton != null)
         {
            TextMeshProUGUI buttonText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = isLocalPlayerReady ? "Unready" : "Ready";
            }
            // Optionally change button color
            Image buttonImage = readyButton.GetComponent<Image>();
             if (buttonImage != null)
             {
                 buttonImage.color = isLocalPlayerReady ? Color.red : Color.green;
             }
         }
    }

    private void OnStartGameButtonClicked()
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.StartGame();
        else
            Debug.LogError("LobbyManager instance is null when trying to start game.");
    }

    private void OnLeaveButtonClicked()
    {
        // Leave Steam lobby first, which will trigger GameManager.LeaveGame
        if (SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.IsInLobby)
        {
             Debug.Log("Leaving Steam Lobby...");
            SteamLobbyManager.Instance.LeaveLobby();
        }
        else if (GameManager.Instance != null)
        {
            // If not in a Steam lobby (e.g., direct connection), leave via GameManager
             Debug.Log("Not in Steam Lobby, leaving via GameManager...");
             GameManager.Instance.LeaveGame();
        }
        else
        {
            Debug.LogError("Cannot leave: SteamLobbyManager and GameManager instances are null.");
        }
    }

    public void UpdatePlayerList(List<PlayerInfo> players)
    {
        currentPlayers = players;
        
        if (playerListPanel == null || playerEntryTemplate == null)
        {
            Debug.LogError("PlayerListPanel or PlayerEntryTemplate is null in UpdatePlayerList!");
            return;
        }
        
        // Clear existing entries
        ClearPlayerList();
        
        // Create new entries
        foreach (var player in players)
        { 
            if (player == null) continue; // Skip null entries just in case

            TextMeshProUGUI playerEntry = Instantiate(playerEntryTemplate, playerListPanel);
            playerEntry.gameObject.SetActive(true);
            
            string readyStatus = player.IsReady ? "<color=green>✓ Ready</color>" : "<color=yellow>... Not Ready</color>";
            playerEntry.text = $"{player.PlayerName} - {readyStatus}";
            
            // Animate new entry
            playerEntry.transform.localScale = Vector3.zero;
            playerEntry.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
        }
    }
    
    private void ClearPlayerList()
    {
        if (playerListPanel == null || playerEntryTemplate == null) return;

        // Remove all player entries except the template
        foreach (Transform child in playerListPanel)
        {
            if (child.gameObject != playerEntryTemplate.gameObject)
            {
                // Kill any active DOTween tweens on this object before destroying
                DOTween.Kill(child, false); // Kill tweens targeting this transform (false = don't fire complete callback)
                Destroy(child.gameObject);
            }
        }
    }
    
    public void SetStartGameButtonActive(bool active)
    {
        if (startGameButton != null)
        {
            bool shouldBeVisible = active;
            
            if (startGameButton.gameObject.activeSelf != shouldBeVisible)
            {
                startGameButton.gameObject.SetActive(shouldBeVisible);
                // Animate button appearance/disappearance
                if (shouldBeVisible)
                {
                    startGameButton.transform.localScale = Vector3.zero;
                    startGameButton.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutElastic);
                }
                else
                {
                    // Optional: Animate disappearance
                    startGameButton.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack);
                }
            }
        }
        else
        {
            Debug.LogError("StartGameButton is null in SetStartGameButtonActive!");
        }
    }
    
    private void AnimateInvalidButtonInput(Button button)
    {
        if (button == null) return;
        
        // Shake animation for invalid input
        button.transform.DOShakePosition(0.5f, new Vector3(10, 0, 0), 10, 50);
        
        // Change button color to red briefly
        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage != null)
        {
            Color originalColor = buttonImage.color;
            buttonImage.DOColor(Color.red, 0.2f).OnComplete(() => {
                buttonImage.DOColor(originalColor, 0.2f);
            });
        }
    }

    // Helper to get the local NetworkPlayer with LobbyPlayerInfo component
    private LobbyPlayerInfo GetLocalLobbyPlayer()
    {
        Debug.Log($"Looking for local LobbyPlayerInfo. Player count: {NetworkPlayer.Players.Count}");
        
        foreach (NetworkPlayer np in NetworkPlayer.Players)
        {
            if (np != null && np.IsOwner)
            {
                LobbyPlayerInfo lobbyPlayer = np.GetComponent<LobbyPlayerInfo>();
                if (lobbyPlayer != null)
                {
                    Debug.Log($"Found local LobbyPlayerInfo on {np.name}");
                    return lobbyPlayer;
                }
            }
        }
        
        // Fallback attempt - try to find using ClientId
        if (InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Connection.IsValid)
        {
            int localClientId = InstanceFinder.ClientManager.Connection.ClientId;
            foreach (NetworkPlayer np in NetworkPlayer.Players)
            {
                if (np != null && np.Owner.ClientId == localClientId)
                {
                    LobbyPlayerInfo lobbyPlayer = np.GetComponent<LobbyPlayerInfo>();
                    if (lobbyPlayer != null)
                    {
                        return lobbyPlayer;
                    }
                }
            }
        }
        
        return null;
    }
}