using UnityEngine;
using System.Collections;
using FishNet.Object;

/// <summary>
/// Manages camera positioning and movement for viewing different arena fights.
/// Handles smooth transitions between arenas and maintains consistent viewing angles.
/// Integrates with ArenaManager to provide camera control for spectating multiple fights.
/// </summary>
public class CameraManager : NetworkBehaviour
{
    public static CameraManager Instance { get; private set; }
    
    [Header("Camera Configuration")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool useMainCameraIfNotSet = true;
    [SerializeField] private float defaultTransitionDuration = 1f;
    [SerializeField] private AnimationCurve defaultTransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Movement Settings")]
    [SerializeField] private bool enableSmoothing = true;
    [SerializeField] private float smoothingSpeed = 5f;
    [SerializeField] private float positionThreshold = 0.1f;
    [SerializeField] private float rotationThreshold = 1f;
    
    [Header("Arena Integration")]
    [SerializeField] private bool autoFindArenaManager = true;
    [SerializeField] private ArenaManager arenaManager;
    
    [Header("Debug")]
    [SerializeField] private bool debugLogEnabled = true;
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color targetPositionGizmoColor = Color.red;
    
    // Camera state
    private Vector3 currentTargetPosition;
    private Vector3 currentTargetRotation;
    private bool isTransitioning = false;
    private Coroutine currentTransitionCoroutine;
    
    // Components
    private FightManager fightManager;
    
    // Camera transition data
    private struct CameraTransitionData
    {
        public Vector3 startPosition;
        public Vector3 startRotation;
        public Vector3 targetPosition;
        public Vector3 targetRotation;
        public float duration;
        public AnimationCurve curve;
        public System.Action onComplete;
    }
    
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
        
        InitializeCamera();
        FindRequiredComponents();
    }
    
    private void Start()
    {
        if (Instance == this)
        {
            InitializeCameraManager();
        }
    }
    
    private void Update()
    {
        if (Instance == this && enableSmoothing && !isTransitioning)
        {
            UpdateCameraSmoothing();
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
    
    private void InitializeCamera()
    {
        if (targetCamera == null && useMainCameraIfNotSet)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindFirstObjectByType<Camera>();
            }
        }
        
        if (targetCamera != null)
        {
            currentTargetPosition = targetCamera.transform.position;
            currentTargetRotation = targetCamera.transform.eulerAngles;
            LogDebug($"Camera initialized: {targetCamera.name}");
        }
        else
        {
            LogDebug("No camera found for CameraManager");
        }
    }
    
    private void FindRequiredComponents()
    {
        if (autoFindArenaManager && arenaManager == null)
        {
            arenaManager = FindFirstObjectByType<ArenaManager>();
        }
        
        if (fightManager == null)
        {
            fightManager = FindFirstObjectByType<FightManager>();
        }
        
        LogDebug($"CameraManager components found - ArenaManager: {arenaManager != null}, FightManager: {fightManager != null}");
    }
    
    private void InitializeCameraManager()
    {
        LogDebug("CameraManager initialized successfully");
        
        // Capture current camera position but don't move it
        // The user's camera position is preserved as the reference for arena 0
        if (targetCamera != null)
        {
            currentTargetPosition = targetCamera.transform.position;
            currentTargetRotation = targetCamera.transform.eulerAngles;
            LogDebug($"Camera position preserved at {currentTargetPosition}, rotation {currentTargetRotation}");
        }
    }
    
    #endregion
    
    #region Camera Movement
    
    /// <summary>
    /// Moves the camera to view a specific arena
    /// </summary>
    public void MoveCameraToArena(int arenaIndex, bool animated = true, float duration = -1, System.Action onComplete = null)
    {
        Debug.Log($"[ARENA_CAMERA] MoveCameraToArena called - arenaIndex: {arenaIndex}, animated: {animated}");
        
        if (arenaManager == null)
        {
            LogDebug("ArenaManager not found, cannot move camera to arena");
            Debug.Log("[ARENA_CAMERA] ERROR: ArenaManager not found, cannot move camera to arena");
            return;
        }
        
        if (targetCamera == null)
        {
            LogDebug("No target camera set, cannot move camera");
            Debug.Log("[ARENA_CAMERA] ERROR: No target camera set, cannot move camera");
            return;
        }
        
        Vector3 targetPosition = arenaManager.GetArenaCameraPosition(arenaIndex);
        Vector3 targetRotation = arenaManager.GetArenaCameraRotation();
        
        Debug.Log($"[ARENA_CAMERA] Target position for arena {arenaIndex}: {targetPosition}");
        Debug.Log($"[ARENA_CAMERA] Target rotation for arena {arenaIndex}: {targetRotation}");
        Debug.Log($"[ARENA_CAMERA] Current camera position: {targetCamera.transform.position}");
        
        if (targetPosition == Vector3.zero)
        {
            LogDebug($"Invalid arena position for arena {arenaIndex}");
            Debug.Log($"[ARENA_CAMERA] ERROR: Invalid arena position for arena {arenaIndex}");
            return;
        }
        
        LogDebug($"Moving camera to arena {arenaIndex} at {targetPosition}");
        Debug.Log($"[ARENA_CAMERA] Moving camera to arena {arenaIndex} at {targetPosition} (animated: {animated})");
        
        if (animated)
        {
            float transitionDuration = duration > 0 ? duration : defaultTransitionDuration;
            StartCameraTransition(targetPosition, targetRotation, transitionDuration, onComplete);
        }
        else
        {
            SetCameraPositionImmediate(targetPosition, targetRotation);
            onComplete?.Invoke();
        }
    }
    
    /// <summary>
    /// Moves the camera to view a specific player's arena
    /// </summary>
    public void MoveCameraToPlayerArena(uint playerObjectId, bool animated = true, float duration = -1, System.Action onComplete = null)
    {
        Debug.Log($"[ARENA_CAMERA] MoveCameraToPlayerArena called for player {playerObjectId}");
        
        if (arenaManager == null)
        {
            LogDebug("ArenaManager not found, cannot move camera to player arena");
            Debug.Log("[ARENA_CAMERA] ERROR: ArenaManager not found, cannot move camera to player arena");
            return;
        }
        
        int arenaIndex = arenaManager.GetArenaForPlayer(playerObjectId);
        Debug.Log($"[ARENA_CAMERA] Player {playerObjectId} is in arena {arenaIndex}");
        
        if (arenaIndex < 0)
        {
            LogDebug($"No arena found for player {playerObjectId}");
            Debug.Log($"[ARENA_CAMERA] ERROR: No arena found for player {playerObjectId}");
            return;
        }
        
        LogDebug($"Moving camera to player {playerObjectId}'s arena (arena {arenaIndex})");
        Debug.Log($"[ARENA_CAMERA] Moving camera to player {playerObjectId}'s arena (arena {arenaIndex})");
        MoveCameraToArena(arenaIndex, animated, duration, onComplete);
    }
    
    /// <summary>
    /// Moves the camera to view the currently viewed fight
    /// </summary>
    public void MoveCameraToCurrentViewedFight(bool animated = true, float duration = -1, System.Action onComplete = null)
    {
        Debug.Log("[ARENA_CAMERA] MoveCameraToCurrentViewedFight called");
        
        if (fightManager == null || fightManager.ViewedLeftFighter == null)
        {
            Debug.LogWarning($"[ARENA_CAMERA] Cannot move camera - FightManager: {fightManager != null}, ViewedPlayer: {fightManager?.ViewedLeftFighter != null}");
            return;
        }
        
        uint playerObjectId = (uint)fightManager.ViewedLeftFighter.ObjectId;
        Debug.Log($"[ARENA_CAMERA] Current viewed player: {fightManager.ViewedLeftFighter.EntityName.Value} (ID: {playerObjectId})");
        MoveCameraToPlayerArena(playerObjectId, animated, duration, onComplete);
    }
    
    /// <summary>
    /// Sets the camera position immediately without animation
    /// </summary>
    public void SetCameraPositionImmediate(Vector3 position, Vector3 rotation)
    {
        if (targetCamera == null)
            return;
            
        // Stop any ongoing transition
        if (currentTransitionCoroutine != null)
        {
            StopCoroutine(currentTransitionCoroutine);
            currentTransitionCoroutine = null;
            isTransitioning = false;
        }
        
        targetCamera.transform.position = position;
        targetCamera.transform.eulerAngles = rotation;
        currentTargetPosition = position;
        currentTargetRotation = rotation;
        
        LogDebug($"Camera position set immediately to {position}, rotation {rotation}");
    }
    
    /// <summary>
    /// Starts a smooth camera transition
    /// </summary>
    private void StartCameraTransition(Vector3 targetPosition, Vector3 targetRotation, float duration, System.Action onComplete)
    {
        if (currentTransitionCoroutine != null)
        {
            StopCoroutine(currentTransitionCoroutine);
        }
        
        CameraTransitionData transitionData = new CameraTransitionData
        {
            startPosition = targetCamera.transform.position,
            startRotation = targetCamera.transform.eulerAngles,
            targetPosition = targetPosition,
            targetRotation = targetRotation,
            duration = duration,
            curve = defaultTransitionCurve,
            onComplete = onComplete
        };
        
        currentTransitionCoroutine = StartCoroutine(CameraTransitionCoroutine(transitionData));
    }
    
    /// <summary>
    /// Coroutine for smooth camera transitions
    /// </summary>
    private IEnumerator CameraTransitionCoroutine(CameraTransitionData data)
    {
        isTransitioning = true;
        float elapsedTime = 0f;
        
        while (elapsedTime < data.duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / data.duration;
            float curveValue = data.curve.Evaluate(t);
            
            // Interpolate position
            Vector3 currentPosition = Vector3.Lerp(data.startPosition, data.targetPosition, curveValue);
            targetCamera.transform.position = currentPosition;
            
            // Interpolate rotation
            Vector3 currentRotation = Vector3.Lerp(data.startRotation, data.targetRotation, curveValue);
            targetCamera.transform.eulerAngles = currentRotation;
            
            yield return null;
        }
        
        // Ensure we end at the exact target position
        targetCamera.transform.position = data.targetPosition;
        targetCamera.transform.eulerAngles = data.targetRotation;
        
        currentTargetPosition = data.targetPosition;
        currentTargetRotation = data.targetRotation;
        
        isTransitioning = false;
        currentTransitionCoroutine = null;
        
        LogDebug($"Camera transition completed to {data.targetPosition}");
        data.onComplete?.Invoke();
    }
    
    /// <summary>
    /// Updates camera smoothing for non-transition movement
    /// </summary>
    private void UpdateCameraSmoothing()
    {
        if (targetCamera == null)
            return;
            
        Vector3 currentPosition = targetCamera.transform.position;
        Vector3 currentRotation = targetCamera.transform.eulerAngles;
        
        // Check if we need to smooth to target position
        float positionDistance = Vector3.Distance(currentPosition, currentTargetPosition);
        float rotationDistance = Vector3.Distance(currentRotation, currentTargetRotation);
        
        if (positionDistance > positionThreshold || rotationDistance > rotationThreshold)
        {
            // Smooth towards target
            Vector3 newPosition = Vector3.Lerp(currentPosition, currentTargetPosition, Time.deltaTime * smoothingSpeed);
            Vector3 newRotation = Vector3.Lerp(currentRotation, currentTargetRotation, Time.deltaTime * smoothingSpeed);
            
            targetCamera.transform.position = newPosition;
            targetCamera.transform.eulerAngles = newRotation;
        }
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Sets the target camera for the camera manager
    /// </summary>
    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
        if (camera != null)
        {
            currentTargetPosition = camera.transform.position;
            currentTargetRotation = camera.transform.eulerAngles;
        }
        LogDebug($"Target camera set to {(camera != null ? camera.name : "null")}");
    }
    
    /// <summary>
    /// Gets the current target camera
    /// </summary>
    public Camera GetTargetCamera()
    {
        return targetCamera;
    }
    
    /// <summary>
    /// Sets the arena manager reference
    /// </summary>
    public void SetArenaManager(ArenaManager manager)
    {
        arenaManager = manager;
        LogDebug($"Arena manager set to {(manager != null ? manager.name : "null")}");
    }
    
    /// <summary>
    /// Checks if the camera is currently transitioning
    /// </summary>
    public bool IsTransitioning => isTransitioning;
    
    /// <summary>
    /// Stops any current camera transition
    /// </summary>
    public void StopTransition()
    {
        if (currentTransitionCoroutine != null)
        {
            StopCoroutine(currentTransitionCoroutine);
            currentTransitionCoroutine = null;
            isTransitioning = false;
            LogDebug("Camera transition stopped");
        }
    }
    
    /// <summary>
    /// Updates the camera to follow the currently viewed fight
    /// </summary>
    public void UpdateCameraForCurrentFight()
    {
        MoveCameraToCurrentViewedFight();
    }
    
    #endregion
    
    #region Debug and Gizmos
    
    private void OnDrawGizmos()
    {
        if (!showGizmos)
            return;
            
        if (targetCamera != null)
        {
            // Draw current camera position
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(targetCamera.transform.position, 0.5f);
            
            // Draw target position
            Gizmos.color = targetPositionGizmoColor;
            Gizmos.DrawWireSphere(currentTargetPosition, 0.3f);
            
            // Draw line between current and target
            if (Vector3.Distance(targetCamera.transform.position, currentTargetPosition) > 0.1f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(targetCamera.transform.position, currentTargetPosition);
            }
        }
    }
    
    [ContextMenu("Move Camera to Arena 0")]
    public void DebugMoveCameraToArena0()
    {
        MoveCameraToArena(0, true);
    }
    
    [ContextMenu("Move Camera to Arena 1")]
    public void DebugMoveCameraToArena1()
    {
        MoveCameraToArena(1, true);
    }
    
    [ContextMenu("Move Camera to Current Viewed Fight")]
    public void DebugMoveCameraToCurrentFight()
    {
        MoveCameraToCurrentViewedFight(true);
    }
    
    [ContextMenu("Debug Camera State")]
    public void DebugCameraState()
    {
        LogDebug("=== Camera State Debug ===");
        LogDebug($"Target Camera: {(targetCamera != null ? targetCamera.name : "null")}");
        LogDebug($"Current Position: {(targetCamera != null ? targetCamera.transform.position : Vector3.zero)}");
        LogDebug($"Target Position: {currentTargetPosition}");
        LogDebug($"Current Rotation: {(targetCamera != null ? targetCamera.transform.eulerAngles : Vector3.zero)}");
        LogDebug($"Target Rotation: {currentTargetRotation}");
        LogDebug($"Is Transitioning: {isTransitioning}");
        LogDebug($"Arena Manager: {(arenaManager != null ? arenaManager.name : "null")}");
        LogDebug($"Fight Manager: {(fightManager != null ? fightManager.name : "null")}");
    }
    
    private void LogDebug(string message)
    {
        if (debugLogEnabled)
        {
            Debug.Log($"[CameraManager] {message}");
        }
    }
    
    #endregion
} 