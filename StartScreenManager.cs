using UnityEngine;
using UnityEngine.UI;

public class StartScreenManager : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private GameObject lobbyCanvas;
    [SerializeField] private GameObject startScreenCanvas;

    private SteamAndLobbyHandler steamAndLobbyHandler;

    void Start()
    {
        // Try to find the instance. Ensure SteamAndLobbyHandler's Awake runs first or it's already in scene.
        steamAndLobbyHandler = SteamAndLobbyHandler.Instance;
        if (steamAndLobbyHandler == null)
        {
            Debug.LogError("StartScreenManager: SteamAndLobbyHandler instance not found. Steam lobby functionality may not work.");
        }

        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartButtonPressed);
        }
        else
        {
            Debug.LogError("Start Button is not assigned in the Inspector.");
        }

        if (lobbyCanvas == null)
        {
            Debug.LogError("Lobby Canvas is not assigned in the Inspector.");
        }

        if (startScreenCanvas == null)
        {
            Debug.LogError("Start Screen Canvas is not assigned in the Inspector.");
        }
    }

    void OnStartButtonPressed()
    {
        if (steamAndLobbyHandler != null)
        {
            Debug.Log("Start button pressed, attempting to initiate Steam connection and lobby...");
            steamAndLobbyHandler.InitiateSteamConnectionAndLobby();
        }
        else
        {
            Debug.LogError("Cannot initiate Steam lobby: SteamAndLobbyHandler instance is null.");
        }

        if (lobbyCanvas != null)
        {
            lobbyCanvas.SetActive(true);
        }

        if (startScreenCanvas != null)
        {
            startScreenCanvas.SetActive(false);
        }
    }
} 