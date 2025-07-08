using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages spatial positioning of multiple fight arenas, placing each fight in a different 3D location.
/// Handles arena assignment, spatial calculations, and camera positioning for viewing different arenas.
/// Host player uses arena 0 (original position), non-host players get assigned to numbered arenas.
/// </summary>
public class ArenaManager : NetworkBehaviour
{
    public static ArenaManager Instance { get; private set; }
    
    [Header("Arena Configuration")]
    [SerializeField] private float arenaSpacing = 50f;
    [SerializeField] private int arenasPerRow = 2;
    
    [Header("Reference Positions (Captured at Start)")]
    [SerializeField] private Vector3 referenceCameraPosition;
    [SerializeField] private Vector3 referenceCameraRotation;
    [SerializeField] private Vector3 referencePlayerPosition;
    [SerializeField] private Vector3 referenceOpponentPetPosition;
    [SerializeField] private bool referencePositionsCaptured = false;
    
    [Header("Camera Configuration")]
    [SerializeField] private float cameraTransitionDuration = 1f;
    [SerializeField] private AnimationCurve cameraTransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Arena Layout")]
    [SerializeField] private bool showArenaGizmos = true;
    [SerializeField] private Color arenaGizmoColor = Color.green;
    [SerializeField] private Vector3 arenaGizmoSize = new Vector3(20, 1, 20);
    
    [Header("Debug")]
    [SerializeField] private bool debugLogEnabled = true;
    
    // Arena data structures
    private Dictionary<int, Vector3> arenaPositions = new Dictionary<int, Vector3>();
    private Dictionary<int, Vector3> arenaCameraPositions = new Dictionary<int, Vector3>();
    private Dictionary<uint, int> playerToArenaMap = new Dictionary<uint, int>();
    private Dictionary<int, List<uint>> arenaToPlayersMap = new Dictionary<int, List<uint>>();
    
    // Components
    private FightManager fightManager;
    private CombatCanvasManager combatCanvasManager;
    private Camera mainCamera;
    private CameraManager cameraManager;
    
    // Arena assignment tracking
    private int nextArenaIndex = 0;
    private bool isInitialized = false;
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        FindRequiredComponents();
    }
    
    private void Start()
    {
        if (Instance == this)
        {
            InitializeArenaSystem();
        }
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    
    #endregion
    
    #region Initialization
    
    private void FindRequiredComponents()
    {
        if (fightManager == null)
            fightManager = FindFirstObjectByType<FightManager>();
            
        if (combatCanvasManager == null)
            combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
            
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
                mainCamera = FindFirstObjectByType<Camera>();
        }
        
        if (cameraManager == null)
            cameraManager = FindFirstObjectByType<CameraManager>();
            
        LogDebug($"ArenaManager components found - FightManager: {fightManager != null}, CombatCanvasManager: {combatCanvasManager != null}, MainCamera: {mainCamera != null}, CameraManager: {cameraManager != null}");
    }
    
    private void InitializeArenaSystem()
    {
        LogDebug("Initializing arena system");
        
        // Capture reference positions from the current scene setup
        CaptureReferencePositions();
        
        // Generate arena positions based on captured references
        GenerateArenaPositions();
        
        // Subscribe to fight manager events
        if (fightManager != null)
        {
            fightManager.OnFightsChanged += OnFightsChanged;
        }
        
        isInitialized = true;
        LogDebug("Arena system initialized successfully");
    }
    
    private void CaptureReferencePositions()
    {
        LogDebug("Capturing reference positions from scene");
        Debug.Log("[ARENA_CAPTURE] Starting reference position capture");
        
        // Capture camera position and rotation
        if (mainCamera != null)
        {
            referenceCameraPosition = mainCamera.transform.position;
            referenceCameraRotation = mainCamera.transform.eulerAngles;
            LogDebug($"Captured camera reference - Position: {referenceCameraPosition}, Rotation: {referenceCameraRotation}");
            Debug.Log($"[ARENA_CAPTURE] Camera reference captured - Position: {referenceCameraPosition}, Rotation: {referenceCameraRotation}");
        }
        else
        {
            LogDebug("Warning: No main camera found, using default positions");
            Debug.Log("[ARENA_CAPTURE] WARNING: No main camera found, using default positions");
            referenceCameraPosition = new Vector3(0, 8, -12);
            referenceCameraRotation = new Vector3(20, 0, 0);
        }
        
        // Capture player and pet position references from CombatCanvasManager
        if (combatCanvasManager != null)
        {
            // Use reflection or direct access to get position transforms
            var playerPosField = typeof(CombatCanvasManager).GetField("playerPositionTransform", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var petPosField = typeof(CombatCanvasManager).GetField("opponentPetPositionTransform", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
            if (playerPosField != null && petPosField != null)
            {
                var playerTransform = playerPosField.GetValue(combatCanvasManager) as Transform;
                var petTransform = petPosField.GetValue(combatCanvasManager) as Transform;
                
                if (playerTransform != null && petTransform != null)
                {
                    referencePlayerPosition = playerTransform.position;
                    referenceOpponentPetPosition = petTransform.position;
                    LogDebug($"Captured entity references - Player: {referencePlayerPosition}, Pet: {referenceOpponentPetPosition}");
                    Debug.Log($"[ARENA_CAPTURE] Entity references captured - Player: {referencePlayerPosition}, Pet: {referenceOpponentPetPosition}");
                }
                else
                {
                    LogDebug("Warning: Transform references not found, using default positions");
                    Debug.Log("[ARENA_CAPTURE] WARNING: Transform references not found, using default positions");
                    referencePlayerPosition = new Vector3(-2, 0, 0);
                    referenceOpponentPetPosition = new Vector3(2, 0, 0);
                }
            }
            else
            {
                LogDebug("Warning: Could not access position transforms, using default positions");
                referencePlayerPosition = new Vector3(-2, 0, 0);
                referenceOpponentPetPosition = new Vector3(2, 0, 0);
            }
        }
        else
        {
            LogDebug("Warning: CombatCanvasManager not found, using default positions");
            referencePlayerPosition = new Vector3(-2, 0, 0);
            referenceOpponentPetPosition = new Vector3(2, 0, 0);
        }
        
        referencePositionsCaptured = true;
        LogDebug("Reference positions captured successfully");
    }
    
    private void GenerateArenaPositions()
    {
        if (!referencePositionsCaptured)
        {
            LogDebug("Warning: Reference positions not captured, cannot generate arena positions");
            return;
        }
        
        LogDebug("Generating arena positions based on captured references");
        
        // Calculate positions for up to 8 arenas (should be enough for most scenarios)
        for (int i = 0; i < 8; i++)
        {
            Vector3 arenaBasePosition = CalculateArenaBasePosition(i);
            Vector3 cameraPosition = CalculateArenaCameraPosition(i);
            
            arenaPositions[i] = arenaBasePosition;
            arenaCameraPositions[i] = cameraPosition;
            
            LogDebug($"Arena {i}: Base Position = {arenaBasePosition}, Camera = {cameraPosition}");
        }
    }
    
    private Vector3 CalculateArenaBasePosition(int arenaIndex)
    {
        if (arenaIndex == 0)
        {
            // Arena 0 uses the original center point between player and pet positions
            return (referencePlayerPosition + referenceOpponentPetPosition) * 0.5f;
        }
        
        // Calculate grid position for non-host arenas relative to reference center
        Vector3 referenceCenter = (referencePlayerPosition + referenceOpponentPetPosition) * 0.5f;
        
        int row = (arenaIndex - 1) / arenasPerRow;
        int col = (arenaIndex - 1) % arenasPerRow;
        
        Vector3 gridOffset = new Vector3(
            col * arenaSpacing - ((arenasPerRow - 1) * arenaSpacing * 0.5f),
            0,
            (row + 1) * arenaSpacing
        );
        
        return referenceCenter + gridOffset;
    }
    
    private Vector3 CalculateArenaCameraPosition(int arenaIndex)
    {
        if (arenaIndex == 0)
        {
            // Arena 0 uses the exact captured camera position
            return referenceCameraPosition;
        }
        
        // Calculate relative camera offset from reference center to reference camera
        Vector3 referenceCenter = (referencePlayerPosition + referenceOpponentPetPosition) * 0.5f;
        Vector3 cameraOffset = referenceCameraPosition - referenceCenter;
        
        // Apply this same offset to other arena centers
        Vector3 arenaCenter = CalculateArenaBasePosition(arenaIndex);
        return arenaCenter + cameraOffset;
    }
    
    #endregion
    
    #region Arena Assignment
    
    public void AssignFightsToArenas()
    {
        if (!isInitialized)
        {
            LogDebug("Arena system not initialized, cannot assign fights");
            return;
        }
        
        if (fightManager == null)
        {
            LogDebug("FightManager not found, cannot assign fights");
            return;
        }
        
        var allFights = fightManager.GetAllFightAssignments();
        if (allFights.Count == 0)
        {
            LogDebug("No fights to assign to arenas");
            return;
        }
        
        LogDebug($"Assigning {allFights.Count} fights to arenas");
        
        // Clear existing assignments
        playerToArenaMap.Clear();
        arenaToPlayersMap.Clear();
        nextArenaIndex = 0;
        
        // Assign each fight to an arena
        foreach (var fight in allFights)
        {
            AssignFightToArena(fight);
        }
        
        LogDebug($"Fight assignment complete. Total arenas used: {nextArenaIndex}");
    }
    
    private void AssignFightToArena(FightAssignmentData fight)
    {
        int arenaIndex = nextArenaIndex++;
        
        // Map player to arena
        playerToArenaMap[fight.PlayerObjectId] = arenaIndex;
        
        // Map arena to players
        if (!arenaToPlayersMap.ContainsKey(arenaIndex))
        {
            arenaToPlayersMap[arenaIndex] = new List<uint>();
        }
        arenaToPlayersMap[arenaIndex].Add(fight.PlayerObjectId);
        
        LogDebug($"Assigned fight (Player {fight.PlayerObjectId} vs Pet {fight.PetObjectId}) to arena {arenaIndex}");
        Debug.Log($"[ARENA_ASSIGN] Player {fight.PlayerObjectId} assigned to arena {arenaIndex} (vs Pet {fight.PetObjectId})");
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Gets the arena index for a specific player
    /// </summary>
    public int GetArenaForPlayer(uint playerObjectId)
    {
        return playerToArenaMap.TryGetValue(playerObjectId, out int arenaIndex) ? arenaIndex : -1;
    }
    
    /// <summary>
    /// Gets the arena position for a specific arena index
    /// </summary>
    public Vector3 GetArenaPosition(int arenaIndex)
    {
        bool found = arenaPositions.TryGetValue(arenaIndex, out Vector3 position);
        Debug.Log($"[ARENA_CAMERA] GetArenaPosition({arenaIndex}) - Found: {found}, Position: {position}");
        return found ? position : Vector3.zero;
    }
    
    /// <summary>
    /// Gets the camera position for a specific arena index
    /// </summary>
    public Vector3 GetArenaCameraPosition(int arenaIndex)
    {
        bool found = arenaCameraPositions.TryGetValue(arenaIndex, out Vector3 position);
        Debug.Log($"[ARENA_CAMERA] GetArenaCameraPosition({arenaIndex}) - Found: {found}, Position: {position}");
        return found ? position : Vector3.zero;
    }
    
    /// <summary>
    /// Gets the camera rotation for arena viewing
    /// </summary>
    public Vector3 GetArenaCameraRotation()
    {
        return referenceCameraRotation;
    }
    
    /// <summary>
    /// Gets the arena index for the currently viewed fight
    /// </summary>
    public int GetCurrentViewedArena()
    {
        if (fightManager == null || fightManager.ViewedCombatPlayer == null)
            return -1;
            
        return GetArenaForPlayer((uint)fightManager.ViewedCombatPlayer.ObjectId);
    }
    
    /// <summary>
    /// Calculates the world position for an entity within a specific arena
    /// </summary>
    public Vector3 CalculateEntityPositionInArena(int arenaIndex, Vector3 relativePosition)
    {
        if (arenaIndex == 0)
        {
            // For arena 0, use the exact reference positions
            Vector3 referenceCenter = (referencePlayerPosition + referenceOpponentPetPosition) * 0.5f;
            return referenceCenter + relativePosition;
        }
        
        // For other arenas, apply the same relative offset from their arena center
        Vector3 arenaCenter = GetArenaPosition(arenaIndex);
        return arenaCenter + relativePosition;
    }
    
    /// <summary>
    /// Gets the player position for a specific arena
    /// </summary>
    public Vector3 GetPlayerPositionInArena(int arenaIndex)
    {
        if (arenaIndex == 0)
        {
            return referencePlayerPosition;
        }
        
        Vector3 arenaCenter = GetArenaPosition(arenaIndex);
        Vector3 referenceCenter = (referencePlayerPosition + referenceOpponentPetPosition) * 0.5f;
        Vector3 playerOffset = referencePlayerPosition - referenceCenter;
        return arenaCenter + playerOffset;
    }
    
    /// <summary>
    /// Gets the opponent pet position for a specific arena
    /// </summary>
    public Vector3 GetOpponentPetPositionInArena(int arenaIndex)
    {
        if (arenaIndex == 0)
        {
            return referenceOpponentPetPosition;
        }
        
        Vector3 arenaCenter = GetArenaPosition(arenaIndex);
        Vector3 referenceCenter = (referencePlayerPosition + referenceOpponentPetPosition) * 0.5f;
        Vector3 petOffset = referenceOpponentPetPosition - referenceCenter;
        return arenaCenter + petOffset;
    }
    
    /// <summary>
    /// Gets all arena positions for debugging
    /// </summary>
    public Dictionary<int, Vector3> GetAllArenaPositions()
    {
        return new Dictionary<int, Vector3>(arenaPositions);
    }
    
    /// <summary>
    /// Gets all camera positions for debugging
    /// </summary>
    public Dictionary<int, Vector3> GetAllCameraPositions()
    {
        return new Dictionary<int, Vector3>(arenaCameraPositions);
    }
    
    /// <summary>
    /// Checks if the arena system is initialized
    /// </summary>
    public bool IsInitialized => isInitialized;
    
    /// <summary>
    /// Gets the reference player position captured at startup
    /// </summary>
    public Vector3 ReferencePlayerPosition => referencePlayerPosition;
    
    /// <summary>
    /// Gets the reference opponent pet position captured at startup
    /// </summary>
    public Vector3 ReferenceOpponentPetPosition => referenceOpponentPetPosition;
    
    /// <summary>
    /// Gets the reference camera position captured at startup
    /// </summary>
    public Vector3 ReferenceCameraPosition => referenceCameraPosition;
    
    /// <summary>
    /// Gets the reference camera rotation captured at startup
    /// </summary>
    public Vector3 ReferenceCameraRotation => referenceCameraRotation;
    
    /// <summary>
    /// Checks if reference positions have been captured
    /// </summary>
    public bool ReferencePositionsCaptured => referencePositionsCaptured;
    
    #endregion
    
    #region Event Handlers
    
    private void OnFightsChanged(bool hasFights)
    {
        if (hasFights)
        {
            LogDebug("Fights changed, reassigning arenas");
            AssignFightsToArenas();
        }
        else
        {
            LogDebug("No fights active, clearing arena assignments");
            ClearArenaAssignments();
        }
    }
    
    private void ClearArenaAssignments()
    {
        playerToArenaMap.Clear();
        arenaToPlayersMap.Clear();
        nextArenaIndex = 0;
    }
    
    #endregion
    
    #region Debug and Gizmos
    
    private void OnDrawGizmos()
    {
        if (!showArenaGizmos || !Application.isPlaying)
            return;
            
        Gizmos.color = arenaGizmoColor;
        
        foreach (var kvp in arenaPositions)
        {
            Vector3 position = kvp.Value;
            Gizmos.DrawWireCube(position, arenaGizmoSize);
            
            // Draw arena number
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(position + Vector3.up * 2, $"Arena {kvp.Key}");
            #endif
        }
        
        // Draw camera positions
        Gizmos.color = Color.blue;
        foreach (var kvp in arenaCameraPositions)
        {
            Vector3 position = kvp.Value;
            Gizmos.DrawWireSphere(position, 1f);
            
            // Draw camera direction
            Vector3 arenaPos = arenaPositions[kvp.Key];
            Gizmos.DrawLine(position, arenaPos);
        }
    }
    
    [ContextMenu("Debug Arena Assignments")]
    public void DebugArenaAssignments()
    {
        LogDebug("=== Arena Assignments Debug ===");
        LogDebug($"Total arenas: {arenaPositions.Count}");
        LogDebug($"Active arena assignments: {playerToArenaMap.Count}");
        
        foreach (var kvp in playerToArenaMap)
        {
            LogDebug($"Player {kvp.Key} -> Arena {kvp.Value} at {GetArenaPosition(kvp.Value)}");
        }
        
        foreach (var kvp in arenaToPlayersMap)
        {
            LogDebug($"Arena {kvp.Key} contains players: {string.Join(", ", kvp.Value)}");
        }
    }
    
    [ContextMenu("Regenerate Arena Positions")]
    public void RegenerateArenaPositions()
    {
        GenerateArenaPositions();
        LogDebug("Arena positions regenerated");
    }
    
    private void LogDebug(string message)
    {
        if (debugLogEnabled)
        {
            Debug.Log($"[ArenaManager] {message}");
        }
    }
    
    #endregion
} 