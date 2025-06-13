using UnityEngine;
using FishNet.Object;
using System.Collections;

/// <summary>
/// Coordinates the transition to character selection phase and initializes the character selection system.
/// Attach to: A NetworkObject in the scene that manages the character selection setup process.
/// </summary>
public class CharacterSelectionSetup : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterSelectionManager characterSelectionManager;
    [SerializeField] private CharacterSelectionUIManager characterSelectionUIManager;
    [SerializeField] private GamePhaseManager gamePhaseManager;
    [SerializeField] private EntityVisibilityManager entityVisibilityManager;
    
    [Header("Character Selection Canvas")]
    [SerializeField] private GameObject characterSelectionCanvas;
    
    private bool isSetupComplete = false;
    
    public bool IsSetupComplete => isSetupComplete;
    
    private void Awake()
    {
        ResolveReferences();
        RegisterCharacterSelectionCanvasWithPhaseManager();
    }
    
    private void ResolveReferences()
    {
        if (characterSelectionManager == null) characterSelectionManager = FindFirstObjectByType<CharacterSelectionManager>();
        if (characterSelectionUIManager == null) characterSelectionUIManager = FindFirstObjectByType<CharacterSelectionUIManager>();
        if (gamePhaseManager == null) gamePhaseManager = GamePhaseManager.Instance;
        if (entityVisibilityManager == null) entityVisibilityManager = FindFirstObjectByType<EntityVisibilityManager>();
        
        // Log what we found for debugging
        Debug.Log($"CharacterSelectionSetup.ResolveReferences: CharacterSelectionManager = {(characterSelectionManager != null ? "Found" : "NULL")}");
        Debug.Log($"CharacterSelectionSetup.ResolveReferences: CharacterSelectionUIManager = {(characterSelectionUIManager != null ? "Found" : "NULL")}");
        Debug.Log($"CharacterSelectionSetup.ResolveReferences: CharacterSelectionCanvas GameObject = {(characterSelectionCanvas != null ? "Found" : "NULL")}");
    }
    
    /// <summary>
    /// Public method to re-resolve references if needed
    /// </summary>
    public void RefreshReferences()
    {
        ResolveReferences();
        RegisterCharacterSelectionCanvasWithPhaseManager();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Re-resolve references when client starts to ensure we have all components
        ResolveReferences();
        Debug.Log("CharacterSelectionSetup: Client started, references resolved");
    }
    
    private void RegisterCharacterSelectionCanvasWithPhaseManager()
    {
        if (gamePhaseManager != null && characterSelectionCanvas != null)
        {
            gamePhaseManager.SetCharacterSelectionCanvas(characterSelectionCanvas);
        }
    }
    
    /// <summary>
    /// Initiates the character selection phase setup directly from start screen
    /// </summary>
    [Server]
    public void InitializeCharacterSelection()
    {
        if (!IsServerInitialized)
        {
            Debug.LogError("CharacterSelectionSetup: Cannot initialize character selection - server not initialized");
            return;
        }
        
        if (isSetupComplete)
        {
            Debug.LogWarning("CharacterSelectionSetup: Character selection setup already completed");
            return;
        }
        
        Debug.Log("CharacterSelectionSetup: Starting combined lobby/character selection phase initialization...");
        
        StartCoroutine(SetupCombinedCharacterSelectionPhase());
    }
    
    /// <summary>
    /// Coroutine to handle the combined lobby/character selection setup process
    /// </summary>
    [Server]
    private IEnumerator SetupCombinedCharacterSelectionPhase()
    {
        // Step 1: Hide any existing lobby UI (if any)
        Debug.Log("CharacterSelectionSetup: Step 1 - Ensuring lobby UI is hidden");
        HideLobbyUI();
        
        // Step 2: Hide all existing network entities (if any)
        Debug.Log("CharacterSelectionSetup: Step 2 - Hiding existing network entities");
        HideAllNetworkEntities();
        
        // Small delay to ensure UI transitions are smooth
        yield return new WaitForSeconds(0.1f);
        
        // Step 3: Update game phase to CharacterSelection (this now includes lobby functionality)
        Debug.Log("CharacterSelectionSetup: Step 3 - Updating game phase to CharacterSelection (with lobby functionality)");
        UpdateGamePhase();
        
        // Step 4: Enable character selection canvas
        Debug.Log("CharacterSelectionSetup: Step 4 - Enabling character selection canvas");
        EnableCharacterSelectionCanvas();
        
        // Small delay to ensure UI is ready
        yield return new WaitForSeconds(0.2f);
        
        // Step 5: Initialize character selection manager (now handles lobby functionality too)
        Debug.Log("CharacterSelectionSetup: Step 5 - Initializing character selection manager with lobby integration");
        InitializeCharacterSelectionManager();
        
        isSetupComplete = true;
        Debug.Log("CharacterSelectionSetup: Combined lobby/character selection phase initialization complete!");
    }
    
    /// <summary>
    /// Hides the lobby UI
    /// </summary>
    [Server]
    private void HideLobbyUI()
    {
        // Notify clients to hide lobby UI
        RpcHideLobbyUI();
    }
    
    /// <summary>
    /// Hides all network entities during character selection phase
    /// </summary>
    [Server]
    private void HideAllNetworkEntities()
    {
        if (entityVisibilityManager != null)
        {
            // Set the game state to CharacterSelection which will hide all entities
            entityVisibilityManager.SetGameState(EntityVisibilityManager.GameState.CharacterSelection);
            Debug.Log("CharacterSelectionSetup: All network entities hidden via EntityVisibilityManager");
        }
        else
        {
            Debug.LogError("CharacterSelectionSetup: EntityVisibilityManager not found, cannot hide network entities");
        }
        
        // Also notify clients to hide entities
        RpcHideAllNetworkEntities();
    }
    
    /// <summary>
    /// Updates the game phase to CharacterSelection
    /// </summary>
    [Server]
    private void UpdateGamePhase()
    {
        if (gamePhaseManager != null)
        {
            gamePhaseManager.SetCharacterSelectionPhase();
            Debug.Log("CharacterSelectionSetup: Game phase updated to CharacterSelection on server");
            
            // Also notify clients to update their game phase using PhaseNetworker
            PhaseNetworker phaseNetworker = gamePhaseManager.GetComponent<PhaseNetworker>();
            if (phaseNetworker != null)
            {
                phaseNetworker.SendPhaseChangeToClients((int)GamePhaseManager.GamePhase.CharacterSelection);
                Debug.Log("CharacterSelectionSetup: Sent character selection phase change to all clients via PhaseNetworker");
            }
            else
            {
                Debug.LogWarning("CharacterSelectionSetup: PhaseNetworker not found, using fallback RPC method");
                RpcUpdateGamePhase();
            }
        }
        else
        {
            Debug.LogError("CharacterSelectionSetup: GamePhaseManager not found, cannot update game phase");
        }
    }
    
    /// <summary>
    /// Enables the character selection canvas
    /// </summary>
    [Server]
    private void EnableCharacterSelectionCanvas()
    {
        // Notify clients to enable character selection canvas
        RpcEnableCharacterSelectionCanvas();
    }
    
    /// <summary>
    /// Initializes the character selection manager
    /// </summary>
    [Server]
    private void InitializeCharacterSelectionManager()
    {
        if (characterSelectionManager != null)
        {
            // The CharacterSelectionManager handles its own initialization when players connect
            Debug.Log("CharacterSelectionSetup: CharacterSelectionManager found and ready");
        }
        else
        {
            Debug.LogError("CharacterSelectionSetup: CharacterSelectionManager not found, character selection will not work properly");
        }
    }
    
    #region Client RPCs
    
    [ObserversRpc]
    private void RpcHideLobbyUI()
    {
        Debug.Log("CharacterSelectionSetup: RpcHideLobbyUI called on client");
        
        if (characterSelectionUIManager != null)
        {
            characterSelectionUIManager.HideLobbyUI();
            Debug.Log("CharacterSelectionSetup: Lobby UI hidden on client");
        }
        else
        {
            Debug.LogWarning("CharacterSelectionSetup: CharacterSelectionUIManager not found on client, cannot hide lobby UI");
        }
    }
    
    [ObserversRpc]
    private void RpcHideAllNetworkEntities()
    {
        Debug.Log("CharacterSelectionSetup: RpcHideAllNetworkEntities called on client");
        
        if (entityVisibilityManager != null)
        {
            entityVisibilityManager.SetGameState(EntityVisibilityManager.GameState.CharacterSelection);
            Debug.Log("CharacterSelectionSetup: All network entities hidden on client");
        }
        else
        {
            Debug.LogWarning("CharacterSelectionSetup: EntityVisibilityManager not found on client, cannot hide network entities");
        }
    }
    
    [ObserversRpc]
    private void RpcUpdateGamePhase()
    {
        Debug.Log("CharacterSelectionSetup: RpcUpdateGamePhase called on client");
        
        if (gamePhaseManager != null)
        {
            gamePhaseManager.SetCharacterSelectionPhase();
            Debug.Log("CharacterSelectionSetup: Game phase updated to CharacterSelection on client");
        }
        else
        {
            // Try to find GamePhaseManager if reference is missing
            gamePhaseManager = GamePhaseManager.Instance;
            if (gamePhaseManager != null)
            {
                gamePhaseManager.SetCharacterSelectionPhase();
                Debug.Log("CharacterSelectionSetup: Game phase updated to CharacterSelection on client via Instance");
            }
            else
            {
                Debug.LogError("CharacterSelectionSetup: GamePhaseManager reference is missing and Instance is null");
            }
        }
    }
    
    [ObserversRpc]
    private void RpcEnableCharacterSelectionCanvas()
    {
        Debug.Log("CharacterSelectionSetup: RpcEnableCharacterSelectionCanvas called on client");
        
        if (characterSelectionCanvas != null)
        {
            characterSelectionCanvas.SetActive(true);
            Debug.Log("CharacterSelectionSetup: Character selection canvas enabled on client");
        }
        else
        {
            Debug.LogError("CharacterSelectionSetup: Character selection canvas reference is missing on client");
        }
        
        // Initialize character selection manager for this client
        if (characterSelectionManager != null)
        {
            characterSelectionManager.InitializeForClient();
            Debug.Log("CharacterSelectionSetup: CharacterSelectionManager initialized for client");
        }
        else
        {
            Debug.LogWarning("CharacterSelectionSetup: CharacterSelectionManager not found on client");
        }
    }
    
    #endregion
    
    /// <summary>
    /// Resets the setup state to allow re-initialization
    /// </summary>
    [Server]
    public void ResetSetup()
    {
        isSetupComplete = false;
        Debug.Log("CharacterSelectionSetup: Setup state reset");
    }
} 