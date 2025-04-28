using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Start Screen")]
    [SerializeField] private GameObject startScreenCanvas;
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Button connectButton;

    [Header("Game UI")]
    [SerializeField] private GameObject combatCanvas;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Another UIManager instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Setup button listeners
        if (connectButton != null)
            connectButton.onClick.AddListener(OnConnectButtonClicked);
        else 
            Debug.LogError("ConnectButton is not assigned in the UIManager Inspector!");

        // Set default player name if available
        if (playerNameInput != null && PlayerPrefs.HasKey("PlayerName"))
        {
            playerNameInput.text = PlayerPrefs.GetString("PlayerName");
        }
    }

    public void OnGameStateChanged(GameState newState)
    {
        // Set up UI based on new state
        switch (newState)
        {
            case GameState.Lobby:
                ShowLobbyUI();
                break;
            case GameState.StartScreen:
                ShowStartScreenUI();
                break;
            case GameState.Combat:
                ShowCombatUI();
                break;
        }
    }

    private void ShowStartScreenUI()
    {
        if (startScreenCanvas != null)
            startScreenCanvas.SetActive(true);
        
        if (combatCanvas != null)
            combatCanvas.SetActive(false);
    }

    private void ShowLobbyUI()
    {
        if (startScreenCanvas != null)
            startScreenCanvas.SetActive(false);
        
        if (combatCanvas != null)
            combatCanvas.SetActive(false);
        
        // LobbyUIManager handles showing the lobby canvas
        if (LobbyUIManager.Instance != null)
            LobbyUIManager.Instance.ShowLobbyCanvas();
    }

    private void ShowCombatUI()
    {
        if (startScreenCanvas != null)
            startScreenCanvas.SetActive(false);
        
        if (combatCanvas != null)
            combatCanvas.SetActive(true);
        
        // Hide lobby canvas if it was active
        if (LobbyUIManager.Instance != null)
            LobbyUIManager.Instance.HideLobbyCanvas();
    }

    private void OnConnectButtonClicked()
    {
        if (playerNameInput == null || string.IsNullOrEmpty(playerNameInput.text))
        {
            Debug.LogWarning("Player name input is missing or empty.");
            if(playerNameInput != null) AnimateInvalidInput(playerNameInput);
            return;
        }

        string playerName = playerNameInput.text;
        
        // Save name for next time
        PlayerPrefs.SetString("PlayerName", playerName);
        
        // Use Steam Lobby Manager to Create/Join
        if (SteamLobbyManager.Instance != null)
        {
            Debug.Log("Attempting to find or create Steam Lobby...");
            SteamLobbyManager.Instance.RequestLobbiesList();
        }
        else
        {
            Debug.LogError("SteamLobbyManager instance not found. Cannot create/join lobby.");
        }
    }
    
    private void AnimateInvalidInput(TMP_InputField input)
    {
        // Shake animation for invalid input
        input.transform.DOShakePosition(0.5f, new Vector3(10, 0, 0), 10, 50);
        
        // Change border color to red briefly
        Image inputImage = input.GetComponent<Image>();
        if (inputImage != null)
        {
            Color originalColor = inputImage.color;
            inputImage.DOColor(Color.red, 0.2f).OnComplete(() => {
                inputImage.DOColor(originalColor, 0.2f);
            });
        }
    }
}