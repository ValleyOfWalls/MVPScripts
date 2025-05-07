using UnityEngine;
using UnityEngine.UI;

public class StartScreenManager : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private GameObject lobbyCanvas;
    [SerializeField] private GameObject startScreenCanvas;

    private SteamNetworkIntegration steamNetworkIntegration;

    void Start()
    {
        // Try to find the instance. Ensure SteamNetworkIntegration's Awake runs first or it's already in scene.
        steamNetworkIntegration = SteamNetworkIntegration.Instance;
        if (steamNetworkIntegration == null)
        {
            Debug.LogError("StartScreenManager: SteamNetworkIntegration instance not found. Steam lobby functionality may not work.");
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
        if (steamNetworkIntegration != null)
        {
            Debug.Log("Start button pressed, attempting to host or join a Steam lobby...");
            steamNetworkIntegration.RequestLobbiesList();
        }
        else
        {
            Debug.LogError("Cannot initiate Steam lobby: SteamNetworkIntegration instance is null.");
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