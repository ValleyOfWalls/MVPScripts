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
        Draft,
        Combat
    }
    
    [Header("UI Canvas References")]
    [SerializeField] private GameObject startScreenCanvas;
    [SerializeField] private GameObject lobbyCanvas;
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
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        // Get reference to the EntityVisibilityManager
        entityVisibilityManager = GetComponent<EntityVisibilityManager>();
        if (entityVisibilityManager == null)
        {
            Debug.LogError("EntityVisibilityManager not found on GamePhaseManager GameObject. Adding one.");
            entityVisibilityManager = gameObject.AddComponent<EntityVisibilityManager>();
        }
    }
    
    private void Start()
    {
        // Initialize UI based on starting phase
        UpdateUIForCurrentPhase();
    }
    
    /// <summary>
    /// Set the game phase to Start
    /// </summary>
    public void SetStartPhase()
    {
        SetPhase(GamePhase.Start);
    }
    
    /// <summary>
    /// Set the game phase to Lobby
    /// </summary>
    public void SetLobbyPhase()
    {
        SetPhase(GamePhase.Lobby);
    }
    
    /// <summary>
    /// Set the game phase to Draft
    /// </summary>
    public void SetDraftPhase()
    {
        SetPhase(GamePhase.Draft);
    }
    
    /// <summary>
    /// Set the game phase to Combat
    /// </summary>
    public void SetCombatPhase()
    {
        SetPhase(GamePhase.Combat);
    }
    
    /// <summary>
    /// Sets the game phase and executes phase-specific logic
    /// </summary>
    private void SetPhase(GamePhase newPhase)
    {
        if (currentPhase == newPhase) return;
        
        GamePhase oldPhase = currentPhase;
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
        switch (newPhase)
        {
            case GamePhase.Lobby:
                // Handle transition to Lobby phase - connect to Steam Network
                if (SteamNetworkIntegration.Instance != null)
                {
                    SteamNetworkIntegration.Instance.RequestLobbiesList();
                }
                else
                {
                    Debug.LogError("GamePhaseManager: Cannot find SteamNetworkIntegration instance to initialize lobby");
                }
                break;
                
            case GamePhase.Draft:
            case GamePhase.Combat:
            case GamePhase.Start:
                // Phase-specific initialization logic if needed
                break;
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
        
        // Update UI visibility
        UpdateUIForCurrentPhase();
        
        // Update player/pet visibility
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
        if (draftCanvas != null) draftCanvas.SetActive(currentPhase == GamePhase.Draft);
        if (combatCanvas != null) combatCanvas.SetActive(currentPhase == GamePhase.Combat);
    }
    
    /// <summary>
    /// Update entity visibility based on the current phase
    /// </summary>
    private void UpdateEntityVisibility()
    {
        if (entityVisibilityManager == null) 
        {
            Debug.LogError("EntityVisibilityManager reference is null in GamePhaseManager");
            return;
        }
        
        switch (currentPhase)
        {
            case GamePhase.Lobby:
                entityVisibilityManager.SetLobbyState();
                break;
            case GamePhase.Combat:
                entityVisibilityManager.SetCombatState();
                break;
            default:
                // For other phases, use lobby visibility rules (entities hidden)
                entityVisibilityManager.SetLobbyState();
                break;
        }
    }
    
    /// <summary>
    /// Get the current game phase
    /// </summary>
    public GamePhase GetCurrentPhase()
    {
        return currentPhase;
    }
    
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
/// Helper class for networking phase changes via FishNet
/// </summary>
public class PhaseNetworker : NetworkBehaviour
{
    private GamePhaseManager phaseManager;
    
    private void Awake()
    {
        phaseManager = FindFirstObjectByType<GamePhaseManager>();
    }
    
    [Server]
    public void SendPhaseChangeToClients(int phaseInt)
    {
        RpcPhaseChanged(phaseInt);
    }
    
    [ObserversRpc]
    private void RpcPhaseChanged(int phaseInt)
    {
        if (phaseManager != null)
        {
            phaseManager.OnNetworkedPhaseChangeReceived(phaseInt);
        }
    }
} 