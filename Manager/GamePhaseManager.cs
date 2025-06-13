using UnityEngine;
using FishNet;
using FishNet.Object;

/// <summary>
/// Manages game phase transitions (Start, Lobby, Draft, Combat) and handles related UI visibility.
/// Attach to: A persistent GameObject in the scene that will manage game phases.
/// Dependencies: Requires EntityVisibilityManager on the same GameObject.
/// </summary>
[RequireComponent(typeof(EntityVisibilityManager))]
public class GamePhaseManager : MonoBehaviour
{
    public static GamePhaseManager Instance { get; private set; }
    
    // Game phase enumeration
    public enum GamePhase
    {
        Start,
        Lobby,
        CharacterSelection,
        Draft,
        Combat
    }
    
    [Header("UI Canvas References")]
    [SerializeField] private GameObject startScreenCanvas;
    [SerializeField] private GameObject lobbyCanvas;
    [SerializeField] private GameObject characterSelectionCanvas;
    [SerializeField] private GameObject draftCanvas;
    [SerializeField] private GameObject combatCanvas;
    
    [Header("Current Phase")]
    [SerializeField] private GamePhase currentPhase = GamePhase.Start;
    
    // Event for phase changes
    public event System.Action<GamePhase> OnPhaseChanged;
    
    // Reference to the EntityVisibilityManager on the same GameObject
    private EntityVisibilityManager entityVisibilityManager;
    
    private void Awake()
    {
        InitializeSingleton();
        entityVisibilityManager = GetComponent<EntityVisibilityManager>() ?? gameObject.AddComponent<EntityVisibilityManager>();
    }
    
    private void InitializeSingleton()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        UpdateUIForCurrentPhase();
    }
    
    #region Public Phase Setters
    
    public void SetStartPhase() => SetPhase(GamePhase.Start);
    
    public void SetLobbyPhase() => SetPhase(GamePhase.Lobby);
    
    public void SetCharacterSelectionPhase() => SetPhase(GamePhase.CharacterSelection);
    
    public void SetDraftPhase() => SetPhase(GamePhase.Draft);
    
    public void SetCombatPhase() => SetPhase(GamePhase.Combat);
    
    #endregion
    
    /// <summary>
    /// Sets the game phase and executes phase-specific logic
    /// </summary>
    private void SetPhase(GamePhase newPhase)
    {
        if (currentPhase == newPhase) return;
        
        currentPhase = newPhase;
        
        // Update local UI for this client
        UpdatePhaseLocally(newPhase);
        
        // Execute phase-specific logic
        ExecutePhaseSpecificLogic(newPhase);
    }
    
    /// <summary>
    /// Executes specific logic based on the new game phase
    /// </summary>
    private void ExecutePhaseSpecificLogic(GamePhase newPhase)
    {
        if (newPhase == GamePhase.CharacterSelection && SteamNetworkIntegration.Instance != null)
        {
            // Start Steam lobby process when entering character selection (which now includes lobby functionality)
            SteamNetworkIntegration.Instance.RequestLobbiesList();
            Debug.Log("GamePhaseManager: Started Steam lobby process for character selection phase");
        }
    }
    
    /// <summary>
    /// Called by a NetworkBehaviour when a networked phase change is received
    /// </summary>
    public void OnNetworkedPhaseChangeReceived(int phaseInt)
    {
        GamePhase receivedPhase = (GamePhase)phaseInt;
        if (currentPhase != receivedPhase)
        {
            UpdatePhaseLocally(receivedPhase);
        }
    }
    
    /// <summary>
    /// Update the phase locally without networking it
    /// </summary>
    private void UpdatePhaseLocally(GamePhase newPhase)
    {
        currentPhase = newPhase;
        
        UpdateUIForCurrentPhase();
        UpdateEntityVisibility();
        
        // Invoke event for other systems to react
        OnPhaseChanged?.Invoke(newPhase);
    }
    
    /// <summary>
    /// Update UI canvases for the current phase
    /// </summary>
    private void UpdateUIForCurrentPhase()
    {
        if (startScreenCanvas != null) startScreenCanvas.SetActive(currentPhase == GamePhase.Start);
        if (lobbyCanvas != null) lobbyCanvas.SetActive(currentPhase == GamePhase.Lobby);
        if (characterSelectionCanvas != null) characterSelectionCanvas.SetActive(currentPhase == GamePhase.CharacterSelection);
        if (draftCanvas != null) draftCanvas.SetActive(currentPhase == GamePhase.Draft);
        if (combatCanvas != null) combatCanvas.SetActive(currentPhase == GamePhase.Combat);
    }
    
    /// <summary>
    /// Update entity visibility based on the current phase
    /// </summary>
    private void UpdateEntityVisibility()
    {
        if (entityVisibilityManager == null) return;
        
        switch (currentPhase)
        {
            case GamePhase.Combat:
                entityVisibilityManager.SetGameState(EntityVisibilityManager.GameState.Combat);
                break;
            case GamePhase.Lobby:
                entityVisibilityManager.SetGameState(EntityVisibilityManager.GameState.Lobby);
                break;
            case GamePhase.CharacterSelection:
                entityVisibilityManager.SetGameState(EntityVisibilityManager.GameState.CharacterSelection);
                break;
            case GamePhase.Draft:
                entityVisibilityManager.SetGameState(EntityVisibilityManager.GameState.Draft);
                break;
            default:
                entityVisibilityManager.SetGameState(EntityVisibilityManager.GameState.Start);
                break;
        }
    }
    
    /// <summary>
    /// Get the current game phase
    /// </summary>
    public GamePhase GetCurrentPhase() => currentPhase;
    
    #region Canvas Setting Methods
    
    public void SetStartScreenCanvas(GameObject canvas)
    {
        if (canvas != null) 
        {
            startScreenCanvas = canvas;
            UpdateUIForCurrentPhase();
        }
    }
    
    public void SetLobbyCanvas(GameObject canvas)
    {
        if (canvas != null) 
        {
            lobbyCanvas = canvas;
            UpdateUIForCurrentPhase();
        }
    }
    
    public void SetCharacterSelectionCanvas(GameObject canvas)
    {
        if (canvas != null) 
        {
            characterSelectionCanvas = canvas;
            UpdateUIForCurrentPhase();
        }
    }
    
    public void SetDraftCanvas(GameObject canvas)
    {
        if (canvas != null) 
        {
            draftCanvas = canvas;
            UpdateUIForCurrentPhase();
        }
    }
    
    public void SetCombatCanvas(GameObject canvas)
    {
        if (canvas != null) 
        {
            combatCanvas = canvas;
            UpdateUIForCurrentPhase();
        }
    }
    
    #endregion
}

/// <summary>
/// Handles the networking of phase changes across clients
/// </summary>
public class PhaseNetworker : NetworkBehaviour
{
    private GamePhaseManager phaseManager;
    
    private void Awake()
    {
        phaseManager = GetComponentInParent<GamePhaseManager>();
    }
    
    [Server]
    public void SendPhaseChangeToClients(int phaseInt)
    {
        RpcPhaseChanged(phaseInt);
    }
    
    [ObserversRpc]
    private void RpcPhaseChanged(int phaseInt)
    {
        phaseManager?.OnNetworkedPhaseChangeReceived(phaseInt);
    }
} 