using UnityEngine;
using FishNet;
using FishNet.Managing;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System;

/// <summary>
/// Manages the character selection phase functionality, player selections, ready states, and transition logic.
/// Attach to: A dedicated CharacterSelectionManager GameObject with NetworkObject component.
/// </summary>
public class CharacterSelectionManager : NetworkBehaviour
{
    [Header("UI Manager")]
    [SerializeField] private CharacterSelectionUIManager uiManager;
    
    [Header("Available Options")]
    [SerializeField] private List<GameObject> availableCharacterPrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> availablePetPrefabs = new List<GameObject>();
    
    // Cached data components from prefabs - these are RUNTIME COPIES, not the original prefabs
    private List<CharacterData> availableCharacters = new List<CharacterData>();
    private List<PetData> availablePets = new List<PetData>();
    
    // References to original prefab data (read-only, never modified)
    private List<CharacterData> originalCharacterData = new List<CharacterData>();
    private List<PetData> originalPetData = new List<PetData>();
    
    [Header("Phase Management")]
    [SerializeField] private GamePhaseManager gamePhaseManager;
    [SerializeField] private PlayerSpawner playerSpawner;
    
    // Network data structures
    private readonly SyncDictionary<NetworkConnection, PlayerSelection> playerSelections = new SyncDictionary<NetworkConnection, PlayerSelection>();
    private readonly SyncDictionary<NetworkConnection, bool> playerReadyStates = new SyncDictionary<NetworkConnection, bool>();
    private readonly SyncDictionary<NetworkConnection, string> playerDisplayNames = new SyncDictionary<NetworkConnection, string>();
    
    private List<NetworkConnection> connectedPlayers = new List<NetworkConnection>();
    
    // Events
    public event Action OnPlayerSelectionsChanged;
    public event Action OnPlayersReadyStateChanged;

    private void Awake()
    {
        FindRequiredComponents();
        ExtractDataFromPrefabs();
        ValidateConfiguration();
    }
    
    private void FindRequiredComponents()
    {
        if (gamePhaseManager == null)
            gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
        if (playerSpawner == null)
            playerSpawner = FindFirstObjectByType<PlayerSpawner>();
        if (uiManager == null)
            uiManager = GetComponent<CharacterSelectionUIManager>();
            
        if (gamePhaseManager == null) Debug.LogError("CharacterSelectionManager: GamePhaseManager not found in scene.");
        if (playerSpawner == null) Debug.LogError("CharacterSelectionManager: PlayerSpawner not found in scene.");
        if (uiManager == null) Debug.LogError("CharacterSelectionManager: CharacterSelectionUIManager not found on GameObject.");
    }
    
    /// <summary>
    /// Extracts CharacterData and PetData components from the assigned prefabs and creates runtime copies
    /// </summary>
    private void ExtractDataFromPrefabs()
    {
        // Clear existing data
        availableCharacters.Clear();
        availablePets.Clear();
        originalCharacterData.Clear();
        originalPetData.Clear();
        
        // Extract character data from prefabs and create runtime copies
        foreach (GameObject prefab in availableCharacterPrefabs)
        {
            if (prefab == null) continue;
            
            CharacterData originalCharacterData = prefab.GetComponent<CharacterData>();
            if (originalCharacterData != null)
            {
                // Store reference to original (never modify this)
                this.originalCharacterData.Add(originalCharacterData);
                
                // Create runtime copy by instantiating the actual prefab (preserves all visual components)
                GameObject runtimeInstance = Instantiate(prefab);
                runtimeInstance.name = $"RuntimeCopy_{originalCharacterData.CharacterName}";
                
                // Temporarily activate to ensure component discovery works properly
                runtimeInstance.SetActive(true);
                
                // Get the CharacterData component from the spawned instance
                CharacterData runtimeCopy = runtimeInstance.GetComponent<CharacterData>();
                
                // Force visual component discovery to ensure all visual data is cached properly
                runtimeCopy.DiscoverVisualComponents();
                
                // Now make it inactive and move it to this GameObject as parent to keep it organized
                runtimeInstance.SetActive(false);
                runtimeInstance.transform.SetParent(this.transform);
                
                availableCharacters.Add(runtimeCopy);
                Debug.Log($"CharacterSelectionManager: Created runtime instance of {originalCharacterData.CharacterName} with all visual components");
                
                // Verify visual components were discovered properly
                if (runtimeCopy.CharacterMesh != null && runtimeCopy.CharacterMaterial != null)
                {
                    Debug.Log($"CharacterSelectionManager: ✓ Visual components discovered - Mesh: {runtimeCopy.CharacterMesh.name}, Material: {runtimeCopy.CharacterMaterial.name}");
                }
                else
                {
                    Debug.LogWarning($"CharacterSelectionManager: ⚠ Missing visual components - Mesh: {runtimeCopy.CharacterMesh?.name ?? "null"}, Material: {runtimeCopy.CharacterMaterial?.name ?? "null"}");
                }
            }
            else
            {
                Debug.LogError($"CharacterSelectionManager: Character prefab {prefab.name} is missing CharacterData component!");
            }
        }
        
        // Extract pet data from prefabs and create runtime copies
        foreach (GameObject prefab in availablePetPrefabs)
        {
            if (prefab == null) continue;
            
            PetData originalPetData = prefab.GetComponent<PetData>();
            if (originalPetData != null)
            {
                // Store reference to original (never modify this)
                this.originalPetData.Add(originalPetData);
                
                // Create runtime copy by instantiating the actual prefab (preserves all visual components)
                GameObject runtimeInstance = Instantiate(prefab);
                runtimeInstance.name = $"RuntimeCopy_{originalPetData.PetName}";
                
                // Temporarily activate to ensure component discovery works properly
                runtimeInstance.SetActive(true);
                
                // Get the PetData component from the spawned instance
                PetData runtimeCopy = runtimeInstance.GetComponent<PetData>();
                
                // Force visual component discovery to ensure all visual data is cached properly
                runtimeCopy.DiscoverVisualComponents();
                
                // Now make it inactive and move it to this GameObject as parent to keep it organized
                runtimeInstance.SetActive(false);
                runtimeInstance.transform.SetParent(this.transform);
                
                availablePets.Add(runtimeCopy);
                Debug.Log($"CharacterSelectionManager: Created runtime instance of {originalPetData.PetName} with all visual components");
                
                // Verify visual components were discovered properly
                if (runtimeCopy.PetMesh != null && runtimeCopy.PetMaterial != null)
                {
                    Debug.Log($"CharacterSelectionManager: ✓ Visual components discovered - Mesh: {runtimeCopy.PetMesh.name}, Material: {runtimeCopy.PetMaterial.name}");
                }
                else
                {
                    Debug.LogWarning($"CharacterSelectionManager: ⚠ Missing visual components - Mesh: {runtimeCopy.PetMesh?.name ?? "null"}, Material: {runtimeCopy.PetMaterial?.name ?? "null"}");
                }
            }
            else
            {
                Debug.LogError($"CharacterSelectionManager: Pet prefab {prefab.name} is missing PetData component!");
            }
        }
    }
    
    /// <summary>
    /// Copy all fields from source CharacterData to target CharacterData using reflection
    /// </summary>
    private void CopyCharacterDataFields(CharacterData source, CharacterData target)
    {
        var fields = typeof(CharacterData).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        foreach (var field in fields)
        {
            try
            {
                // Skip visual component fields since they should remain from the instantiated prefab
                if (field.Name.Contains("discovered") || field.Name.Contains("cached"))
                {
                    continue;
                }
                
                var value = field.GetValue(source);
                field.SetValue(target, value);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to copy field {field.Name}: {e.Message}");
            }
        }
        
        // Re-trigger component discovery to ensure visual components are properly cached
        target.DiscoverVisualComponents();
    }
    
    /// <summary>
    /// Copy all fields from source PetData to target PetData using reflection  
    /// </summary>
    private void CopyPetDataFields(PetData source, PetData target)
    {
        var fields = typeof(PetData).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        foreach (var field in fields)
        {
            try
            {
                // Skip visual component fields since they should remain from the instantiated prefab
                if (field.Name.Contains("discovered") || field.Name.Contains("cached"))
                {
                    continue;
                }
                
                var value = field.GetValue(source);
                field.SetValue(target, value);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to copy field {field.Name}: {e.Message}");
            }
        }
        
        // Re-trigger component discovery to ensure visual components are properly cached
        target.DiscoverVisualComponents();
    }
    
    /// <summary>
    /// Reset runtime copies back to original prefab data (useful when randomization is disabled)
    /// </summary>
    public void ResetToOriginalData()
    {
        Debug.Log("CharacterSelectionManager: Resetting runtime data copies to original prefab data");
        
        // Recreate runtime copies from original data
        for (int i = 0; i < originalCharacterData.Count && i < availableCharacters.Count; i++)
        {
            // Temporarily activate to ensure proper component access
            bool wasActive = availableCharacters[i].gameObject.activeSelf;
            availableCharacters[i].gameObject.SetActive(true);
            
            CopyCharacterDataFields(originalCharacterData[i], availableCharacters[i]);
            
            // Restore previous active state
            availableCharacters[i].gameObject.SetActive(wasActive);
            
            Debug.Log($"CharacterSelectionManager: Reset {availableCharacters[i].CharacterName} to original data");
        }
        
        for (int i = 0; i < originalPetData.Count && i < availablePets.Count; i++)
        {
            // Temporarily activate to ensure proper component access
            bool wasActive = availablePets[i].gameObject.activeSelf;
            availablePets[i].gameObject.SetActive(true);
            
            CopyPetDataFields(originalPetData[i], availablePets[i]);
            
            // Restore previous active state
            availablePets[i].gameObject.SetActive(wasActive);
            
            Debug.Log($"CharacterSelectionManager: Reset {availablePets[i].PetName} to original data");
        }
    }
    
    /// <summary>
    /// Clean up runtime copy GameObjects to prevent memory leaks
    /// </summary>
    private void CleanupRuntimeCopies()
    {
        Debug.Log("CharacterSelectionManager: Cleaning up runtime instance GameObjects");
        
        // Destroy instantiated character prefab copies
        foreach (var characterData in availableCharacters)
        {
            if (characterData != null && characterData.gameObject != null)
            {
                Debug.Log($"CharacterSelectionManager: Destroying runtime instance for {characterData.CharacterName}");
                DestroyImmediate(characterData.gameObject);
            }
        }
        
        // Destroy instantiated pet prefab copies
        foreach (var petData in availablePets)
        {
            if (petData != null && petData.gameObject != null)
            {
                Debug.Log($"CharacterSelectionManager: Destroying runtime instance for {petData.PetName}");
                DestroyImmediate(petData.gameObject);
            }
        }
        
        availableCharacters.Clear();
        availablePets.Clear();
    }
    
    private void OnDestroy()
    {
        // Clean up runtime copies when this component is destroyed
        CleanupRuntimeCopies();
    }

    private void ValidateConfiguration()
    {
        if (availableCharacterPrefabs.Count == 0)
            Debug.LogWarning("CharacterSelectionManager: No character prefabs configured. Please assign character prefabs with CharacterData components.");
        if (availablePetPrefabs.Count == 0)
            Debug.LogWarning("CharacterSelectionManager: No pet prefabs configured. Please assign pet prefabs with PetData components.");
            
        if (availableCharacters.Count == 0)
            Debug.LogWarning("CharacterSelectionManager: No valid character data found. Check that prefabs have CharacterData components.");
        if (availablePets.Count == 0)
            Debug.LogWarning("CharacterSelectionManager: No valid pet data found. Check that prefabs have PetData components.");
            
        // Validate each character/pet data
        foreach (var character in availableCharacters)
        {
            if (character == null || !character.IsValid())
                Debug.LogError($"CharacterSelectionManager: Invalid character data found: {character?.name}");
        }
        
        foreach (var pet in availablePets)
        {
            if (pet == null || !pet.IsValid())
                Debug.LogError($"CharacterSelectionManager: Invalid pet data found: {pet?.name}");
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Register for player connections
        if (InstanceFinder.ServerManager != null)
        {
            InstanceFinder.ServerManager.OnRemoteConnectionState += OnServerConnectionState;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Ensure we have the game phase manager reference
        if (gamePhaseManager == null)
        {
            gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
        }
        
        // Check if we're joining during character selection phase
        if (gamePhaseManager != null && gamePhaseManager.GetCurrentPhase() == GamePhaseManager.GamePhase.CharacterSelection)
        {
            Debug.Log("CharacterSelectionManager: Joining during character selection phase, initializing client");
            InitializeForClient();
        }
        else
        {
            Debug.Log("CharacterSelectionManager: OnStartClient called, waiting for character selection phase to begin");
            // The CharacterSelectionSetup will call InitializeForClient when appropriate, or the TargetRpc will handle it
        }
    }
    
    /// <summary>
    /// Initialize the character selection manager for a client when entering character selection phase
    /// </summary>
    public void InitializeForClient()
    {
        // Initialize local player
        string initialPlayerName = GetInitialPlayerName();
        CmdServerAddPlayer(LocalConnection, initialPlayerName);
        
        // Set up UI
        if (uiManager != null)
        {
            uiManager.Initialize(this, availableCharacters, availablePets);
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        
        if (InstanceFinder.ServerManager != null)
        {
            InstanceFinder.ServerManager.OnRemoteConnectionState -= OnServerConnectionState;
        }
    }

    private string GetInitialPlayerName()
    {
        string name = (SteamNetworkIntegration.Instance != null && SteamNetworkIntegration.Instance.IsSteamInitialized) 
            ? SteamNetworkIntegration.Instance.GetPlayerName() 
            : "Player";
        
        return name + " (" + LocalConnection.ClientId + ")";
    }

    private void OnServerConnectionState(NetworkConnection conn, FishNet.Transporting.RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Stopped)
        {
            ServerHandlePlayerDisconnect(conn);
        }
    }

    #region Server Methods
    
    [ServerRpc(RequireOwnership = false)]
    private void CmdServerAddPlayer(NetworkConnection conn, string playerName)
    {
        ServerAddPlayerLogic(conn, playerName);
    }

    [Server]
    private void ServerAddPlayerLogic(NetworkConnection conn, string playerName)
    {
        if (!connectedPlayers.Contains(conn))
        {
            connectedPlayers.Add(conn);
            playerReadyStates[conn] = false;
            playerDisplayNames[conn] = playerName;
            
            // Initialize empty selection
            playerSelections[conn] = new PlayerSelection
            {
                characterIndex = -1,
                petIndex = -1,
                customPlayerName = "",
                customPetName = "",
                hasSelection = false
            };
            
            // Ensure selection objects are spawned when any player joins
            EnsureSelectionObjectsSpawned();
            
            BroadcastPlayerUpdates();
            /* Debug.Log($"CharacterSelectionManager: Added player {playerName} to character selection"); */
        }
    }
    
    /// <summary>
    /// Server method to directly add a player (called by LobbyManager for subsequent players)
    /// </summary>
    [Server]
    public void ServerAddPlayerDirectly(NetworkConnection conn, string playerName)
    {
        /* Debug.Log($"CharacterSelectionManager: ServerAddPlayerDirectly called for {playerName}"); */
        ServerAddPlayerLogic(conn, playerName);
        
        // Ensure selection objects are spawned on server for all clients
        EnsureSelectionObjectsSpawned();
        
        // Ensure the joining client gets their UI initialized
        TargetRpcInitializeJoiningClient(conn);
    }
    
    /// <summary>
    /// Ensures selection objects are spawned on the server
    /// </summary>
    [Server]
    private void EnsureSelectionObjectsSpawned()
    {
        // Check if selection objects already exist
        SelectionNetworkObject[] existingObjects = FindObjectsByType<SelectionNetworkObject>(FindObjectsSortMode.None);
        if (existingObjects.Length > 0)
        {
            Debug.Log($"CharacterSelectionManager: Selection objects already spawned ({existingObjects.Length} found)");
            return;
        }
        
        // Find the UI manager and trigger object spawning
        if (uiManager != null)
        {
            Debug.Log("CharacterSelectionManager: Triggering server to spawn selection objects for new client");
            uiManager.ServerEnsureSelectionObjectsSpawned();
        }
        else
        {
            Debug.LogWarning("CharacterSelectionManager: UI manager not found, cannot spawn selection objects");
        }
    }

    /// <summary>
    /// Target RPC to initialize a client that's joining during character selection
    /// </summary>
    [TargetRpc]
    private void TargetRpcInitializeJoiningClient(NetworkConnection conn)
    {
        /* Debug.Log("CharacterSelectionManager: TargetRpcInitializeJoiningClient called"); */
        
        // If the client hasn't been initialized yet, do it now
        if (uiManager == null || !uiManager.gameObject.activeInHierarchy)
        {
            InitializeForClient();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdUpdatePlayerSelection(NetworkConnection conn, int characterIndex, int petIndex, string customPlayerName, string customPetName)
    {
        if (conn == null) return;
        
        // Validate indices
        if (characterIndex < 0 || characterIndex >= availableCharacters.Count)
        {
            Debug.LogError($"CharacterSelectionManager: Invalid character index {characterIndex}");
            return;
        }
        
        if (petIndex < 0 || petIndex >= availablePets.Count)
        {
            Debug.LogError($"CharacterSelectionManager: Invalid pet index {petIndex}");
            return;
        }
        
        // Update selection
        PlayerSelection selection = new PlayerSelection
        {
            characterIndex = characterIndex,
            petIndex = petIndex,
            customPlayerName = SanitizeName(customPlayerName),
            customPetName = SanitizeName(customPetName),
            hasSelection = true
        };
        
        playerSelections[conn] = selection;
        
        // Player is no longer ready when they change selection
        if (playerReadyStates.ContainsKey(conn))
        {
            playerReadyStates[conn] = false;
        }
        
        BroadcastPlayerUpdates();
        /* Debug.Log($"CharacterSelectionManager: Updated selection for player {playerDisplayNames[conn]}"); */
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdTogglePlayerReadyState(NetworkConnection conn)
    {
        /* Debug.Log($"CharacterSelectionManager: CmdTogglePlayerReadyState called for connection {conn?.ClientId} at {Time.time:F3}"); */
        
        if (conn == null)
        {
            Debug.LogWarning("CharacterSelectionManager: CmdTogglePlayerReadyState called with null connection");
            return;
        }

        if (!connectedPlayers.Contains(conn))
        {
            Debug.LogWarning($"CharacterSelectionManager: Player {conn.ClientId} not in connected players list");
            return;
        }

        // Player can only be ready if they have made a selection
        if (!playerSelections.ContainsKey(conn) || !playerSelections[conn].hasSelection)
        {
            Debug.LogWarning($"CharacterSelectionManager: Player {playerDisplayNames[conn]} tried to ready without selection");
            return;
        }

        bool currentState = playerReadyStates.ContainsKey(conn) ? playerReadyStates[conn] : false;
        playerReadyStates[conn] = !currentState;
        
        BroadcastPlayerUpdates();
        CheckAllPlayersReady();
        
        /* Debug.Log($"CharacterSelectionManager: Player {playerDisplayNames[conn]} ({conn.ClientId}) ready state: {playerReadyStates[conn]}"); */
    }

    [Server]
    private void BroadcastPlayerUpdates()
    {
        // Create lists for broadcasting
        List<PlayerSelectionInfo> selectionInfos = new List<PlayerSelectionInfo>();
        
        foreach (NetworkConnection conn in connectedPlayers)
        {
            PlayerSelectionInfo info = new PlayerSelectionInfo
            {
                playerName = playerDisplayNames.ContainsKey(conn) ? playerDisplayNames[conn] : "Unknown",
                hasSelection = playerSelections.ContainsKey(conn) ? playerSelections[conn].hasSelection : false,
                isReady = playerReadyStates.ContainsKey(conn) ? playerReadyStates[conn] : false,
                characterName = "",
                petName = "",
                characterIndex = -1,
                petIndex = -1
            };
            
            if (info.hasSelection && playerSelections.ContainsKey(conn))
            {
                var selection = playerSelections[conn];
                
                // Set indices for Mario Kart style display
                info.characterIndex = selection.characterIndex;
                info.petIndex = selection.petIndex;
                
                if (selection.characterIndex >= 0 && selection.characterIndex < availableCharacters.Count)
                {
                    info.characterName = !string.IsNullOrEmpty(selection.customPlayerName) 
                        ? selection.customPlayerName 
                        : availableCharacters[selection.characterIndex].CharacterName;
                }
                if (selection.petIndex >= 0 && selection.petIndex < availablePets.Count)
                {
                    info.petName = !string.IsNullOrEmpty(selection.customPetName) 
                        ? selection.customPetName 
                        : availablePets[selection.petIndex].PetName;
                }
            }
            
            selectionInfos.Add(info);
        }
        
        RpcUpdatePlayerSelections(selectionInfos);
    }

    [Server]
    private void CheckAllPlayersReady()
    {
        if (connectedPlayers.Count < 2)
        {
            // Need at least 2 players - this enforces the minimum lobby requirement
            Debug.Log($"CharacterSelectionManager: Not enough players ({connectedPlayers.Count}/2 minimum)");
            return;
        }
        
        bool allReady = true;
        bool allHaveSelections = true;
        
        foreach (NetworkConnection conn in connectedPlayers)
        {
            // Check if player is ready
            if (!playerReadyStates.ContainsKey(conn) || !playerReadyStates[conn])
            {
                allReady = false;
            }
            
            // Check if player has valid selection
            if (!playerSelections.ContainsKey(conn) || !playerSelections[conn].hasSelection)
            {
                allHaveSelections = false;
            }
        }
        
        /* Debug.Log($"CharacterSelectionManager: Ready check - Players: {connectedPlayers.Count}, All Ready: {allReady}, All Have Selections: {allHaveSelections}"); */
        
        if (allReady && allHaveSelections)
        {
            /* Debug.Log("CharacterSelectionManager: All players ready with selections, starting entity spawning and combat transition"); */
            StartCoroutine(TransitionToCombat());
        }
    }

    [Server]
    private System.Collections.IEnumerator TransitionToCombat()
    {
        // Notify clients about transition start
        RpcNotifyTransitionStart();
        
        // Spawn entities based on selections
        if (playerSpawner != null)
        {
            // Spawn entities for each player based on their selections
            foreach (var kvp in playerSelections)
            {
                NetworkConnection conn = kvp.Key;
                PlayerSelection selection = kvp.Value;
                
                if (!selection.hasSelection) continue;
                
                // Get character and pet data
                CharacterData characterData = GetCharacterDataByIndex(selection.characterIndex);
                PetData petData = GetPetDataByIndex(selection.petIndex);
                
                if (characterData == null || petData == null) continue;
                
                // Spawn entities for this player
                SpawnedEntitiesData spawnedData = playerSpawner.SpawnPlayerWithSelection(
                    conn, 
                    characterData, 
                    petData, 
                    selection.customPlayerName, 
                    selection.customPetName
                );
                
                if (spawnedData != null)
                {
                    /* Debug.Log($"CharacterSelectionManager: Successfully spawned entities for player {conn.ClientId}"); */
                }
                else
                {
                    Debug.LogError($"CharacterSelectionManager: Failed to spawn entities for player {conn.ClientId}");
                }
            }
        }
        else
        {
            Debug.LogError("CharacterSelectionManager: PlayerSpawner not found, cannot spawn entities");
            yield break;
        }
        
        // Show loading screen immediately before any cleanup or phase changes
        LoadingScreenManager loadingScreenManager = FindFirstObjectByType<LoadingScreenManager>();
        if (loadingScreenManager != null)
        {
            loadingScreenManager.RpcShowLoadingScreenForCombatTransition();
            Debug.Log("CharacterSelectionManager: Loading screen activated before combat transition");
        }
        else
        {
            Debug.LogWarning("CharacterSelectionManager: LoadingScreenManager not found, proceeding without loading screen");
        }
        
        // Small delay to ensure loading screen is visible before starting cleanup
        yield return new WaitForSeconds(0.1f);
        
        // Mark lobby as in-progress (no longer discoverable by new players)
        if (SteamNetworkIntegration.Instance != null)
        {
            SteamNetworkIntegration.Instance.MarkLobbyAsInProgress();
        }
        
        // Transition to combat phase
        if (gamePhaseManager != null)
        {
            gamePhaseManager.SetCombatPhase();
            
            // Use RPC method as primary route (since it works reliably)
            RpcUpdateGamePhaseToCombat();
            Debug.Log("CharacterSelectionManager: Sent combat phase change to all clients via RPC");
        }
        
        // Clean up character selection phase
        CharacterSelectionSetup characterSelectionSetup = FindFirstObjectByType<CharacterSelectionSetup>();
        if (characterSelectionSetup != null)
        {
            characterSelectionSetup.CleanupCharacterSelection();
            /* Debug.Log("CharacterSelectionManager: Character selection cleanup initiated"); */
        }
        else
        {
            Debug.LogWarning("CharacterSelectionManager: CharacterSelectionSetup not found for cleanup");
        }
        
        // Initialize combat
        yield return new WaitForSeconds(0.5f); // Allow time for phase transition
        
        CombatSetup combatSetup = FindFirstObjectByType<CombatSetup>();
        if (combatSetup != null)
        {
            combatSetup.InitializeCombat();
        }
        else
        {
            Debug.LogError("CharacterSelectionManager: CombatSetup not found, cannot initialize combat");
        }
    }

    [Server]
    public void ServerHandlePlayerDisconnect(NetworkConnection conn)
    {
        if (connectedPlayers.Contains(conn))
        {
            connectedPlayers.Remove(conn);
            playerSelections.Remove(conn);
            playerReadyStates.Remove(conn);
            playerDisplayNames.Remove(conn);
            
            BroadcastPlayerUpdates();
            
            Debug.Log($"CharacterSelectionManager: Player disconnected during character selection");
        }
    }

    private string SanitizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";
            
        // Trim and limit to 20 characters
        name = name.Trim();
        if (name.Length > 20)
            name = name.Substring(0, 20);
            
        return name;
    }

    #endregion

    #region Client RPCs

    [ObserversRpc]
    private void RpcUpdatePlayerSelections(List<PlayerSelectionInfo> selectionInfos)
    {
        if (uiManager != null)
        {
            uiManager.UpdateOtherPlayersSelections(selectionInfos);
        }
        
        OnPlayerSelectionsChanged?.Invoke();
    }

    [ObserversRpc]
    private void RpcNotifyTransitionStart()
    {
        if (uiManager != null)
        {
            uiManager.ShowTransitionMessage("All players ready! Spawning entities and starting combat...");
            
            // Start cleanup after a short delay to allow the transition message to be visible
            StartCoroutine(CleanupCharacterSelectionAfterDelay());
        }
    }
    
    /// <summary>
    /// Cleans up character selection UI and models after a short delay during combat transition
    /// </summary>
    private System.Collections.IEnumerator CleanupCharacterSelectionAfterDelay()
    {
        // Wait a moment to let players see the transition message
        yield return new WaitForSeconds(1.0f);
        
        if (uiManager != null)
        {
            // Hide character selection UI and clean up models
            uiManager.HideCharacterSelectionUI();
            Debug.Log("CharacterSelectionManager: Character selection UI hidden and models cleaned up");
        }
    }

    [ObserversRpc]
    private void RpcUpdateGamePhaseToCombat()
    {
        if (gamePhaseManager != null)
        {
            gamePhaseManager.SetCombatPhase();
            Debug.Log("CharacterSelectionManager: Game phase updated to Combat on client via RPC");
        }
    }

    #endregion

    #region Helper Methods

    private CharacterData GetCharacterDataByIndex(int index)
    {
        if (index < 0 || index >= availableCharacters.Count)
            return null;
        return availableCharacters[index];
    }

    private PetData GetPetDataByIndex(int index)
    {
        if (index < 0 || index >= availablePets.Count)
            return null;
        return availablePets[index];
    }

    #endregion

    #region Public API for UI

    public List<CharacterData> GetAvailableCharacters() => availableCharacters;
    public List<PetData> GetAvailablePets() => availablePets;
    
    public List<GameObject> GetAvailableCharacterPrefabs() => availableCharacterPrefabs;
    public List<GameObject> GetAvailablePetPrefabs() => availablePetPrefabs;
    
    public GameObject GetCharacterPrefabByIndex(int index)
    {
        if (index >= 0 && index < availableCharacterPrefabs.Count)
            return availableCharacterPrefabs[index];
        return null;
    }

    public GameObject GetPetPrefabByIndex(int index)
    {
        if (index >= 0 && index < availablePetPrefabs.Count)
            return availablePetPrefabs[index];
        return null;
    }
    
    /// <summary>
    /// Get the runtime instance GameObject for a character by index (with modified data if randomization is enabled)
    /// </summary>
    public GameObject GetCharacterRuntimeInstanceByIndex(int index)
    {
        if (index >= 0 && index < availableCharacters.Count && availableCharacters[index] != null)
            return availableCharacters[index].gameObject;
        return null;
    }
    
    /// <summary>
    /// Get the runtime instance GameObject for a pet by index (with modified data if randomization is enabled)
    /// </summary>
    public GameObject GetPetRuntimeInstanceByIndex(int index)
    {
        if (index >= 0 && index < availablePets.Count && availablePets[index] != null)
            return availablePets[index].gameObject;
        return null;
    }
    
    /// <summary>
    /// Get the appropriate prefab/instance for spawning in combat - returns runtime instance if available, otherwise original prefab
    /// </summary>
    public GameObject GetCharacterForSpawning(int index)
    {
        // Prefer runtime instance (which has modified data) over original prefab
        GameObject runtimeInstance = GetCharacterRuntimeInstanceByIndex(index);
        if (runtimeInstance != null)
        {
            Debug.Log($"CharacterSelectionManager: Using runtime instance for character {index}");
            return runtimeInstance;
        }
        
        Debug.Log($"CharacterSelectionManager: Using original prefab for character {index}");
        return GetCharacterPrefabByIndex(index);
    }
    
    /// <summary>
    /// Get the appropriate prefab/instance for spawning in combat - returns runtime instance if available, otherwise original prefab
    /// </summary>
    public GameObject GetPetForSpawning(int index)
    {
        // Prefer runtime instance (which has modified data) over original prefab
        GameObject runtimeInstance = GetPetRuntimeInstanceByIndex(index);
        if (runtimeInstance != null)
        {
            Debug.Log($"CharacterSelectionManager: Using runtime instance for pet {index}");
            return runtimeInstance;
        }
        
        Debug.Log($"CharacterSelectionManager: Using original prefab for pet {index}");
        return GetPetPrefabByIndex(index);
    }
    
    public int GetConnectedPlayerCount() => connectedPlayers.Count;
    
    public void RequestSelectionUpdate(int characterIndex, int petIndex, string customPlayerName, string customPetName)
    {
        CmdUpdatePlayerSelection(LocalConnection, characterIndex, petIndex, customPlayerName, customPetName);
    }
    
    public void RequestReadyToggle()
    {
        CmdTogglePlayerReadyState(LocalConnection);
    }

    #endregion
}

/// <summary>
/// Represents a player's character and pet selection
/// </summary>
[System.Serializable]
public struct PlayerSelection
{
    public int characterIndex;
    public int petIndex;
    public string customPlayerName;
    public string customPetName;
    public bool hasSelection;
}

/// <summary>
/// Information about a player's selection for broadcasting to clients
/// </summary>
[System.Serializable]
public struct PlayerSelectionInfo
{
    public string playerName;
    public string characterName;
    public string petName;
    public int characterIndex;
    public int petIndex;
    public bool hasSelection;
    public bool isReady;
} 