using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages the fight conclusion interstitial flow after all fights complete and before draft transition.
/// Coordinates the conclusion display and transition to draft phase using FightConclusionAnimator.
/// Attach to: A NetworkObject in the scene that manages the fight conclusion timing.
/// </summary>
public class FightConclusionManager : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] private FightConclusionUIManager fightConclusionUIManager;
    [SerializeField] private FightConclusionAnimator fightConclusionAnimator;
    [SerializeField] private DraftSetup draftSetup;
    [SerializeField] private CombatCanvasManager combatCanvasManager;
    [SerializeField] private GamePhaseManager gamePhaseManager;
    [SerializeField] private AutoTestRunner autoTestRunner;

    [Header("Timing Settings")]
    [SerializeField] private float additionalDelayBeforeDraft = 0.5f;

    // Track conclusion state
    private readonly SyncVar<bool> isConclusionActive = new SyncVar<bool>(false);
    private Dictionary<NetworkEntity, bool> fightResults = new Dictionary<NetworkEntity, bool>();
    
    // Serializable structure for sending fight results via RPC
    [System.Serializable]
    private struct FightResultData
    {
        public int playerObjectId;
        public bool playerWon;
        
        public FightResultData(int playerId, bool won)
        {
            playerObjectId = playerId;
            playerWon = won;
        }
    }

    // Animation tracking
    private Coroutine conclusionTimingCoroutine;

    public bool IsConclusionActive => isConclusionActive.Value;
    public float ConclusionDuration => fightConclusionAnimator != null ? fightConclusionAnimator.TotalDuration : 5f;

    #region Lifecycle

    private void Awake()
    {
        FindRequiredComponents();
        SetupUIManagerEvents();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        isConclusionActive.OnChange += OnConclusionActiveChanged;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        isConclusionActive.OnChange += OnConclusionActiveChanged;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        isConclusionActive.OnChange -= OnConclusionActiveChanged;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        isConclusionActive.OnChange -= OnConclusionActiveChanged;
    }

    private void OnDestroy()
    {
        CleanupUIManagerEvents();
    }

    #endregion

    #region Component Resolution

    private void FindRequiredComponents()
    {
        if (fightConclusionUIManager == null)
        {
            fightConclusionUIManager = FindFirstObjectByType<FightConclusionUIManager>();
        }

        if (fightConclusionAnimator == null)
        {
            fightConclusionAnimator = FindFirstObjectByType<FightConclusionAnimator>();
            
            // If not found globally, try to get from UI manager
            if (fightConclusionAnimator == null && fightConclusionUIManager != null)
            {
                fightConclusionAnimator = fightConclusionUIManager.GetAnimator();
            }
        }

        if (draftSetup == null)
        {
            draftSetup = FindFirstObjectByType<DraftSetup>();
        }

        if (combatCanvasManager == null)
        {
            combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
        }

        if (gamePhaseManager == null)
        {
            gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
        }

        if (autoTestRunner == null)
        {
            autoTestRunner = FindFirstObjectByType<AutoTestRunner>();
        }
    }

    private void SetupUIManagerEvents()
    {
        if (fightConclusionUIManager != null)
        {
            fightConclusionUIManager.OnConclusionStarted += HandleConclusionStarted;
            fightConclusionUIManager.OnConclusionCompleted += HandleConclusionCompleted;
        }
    }

    private void CleanupUIManagerEvents()
    {
        if (fightConclusionUIManager != null)
        {
            fightConclusionUIManager.OnConclusionStarted -= HandleConclusionStarted;
            fightConclusionUIManager.OnConclusionCompleted -= HandleConclusionCompleted;
        }
    }

    #endregion

    #region Server Methods

    /// <summary>
    /// Starts the fight conclusion sequence (called by CombatManager after all fights complete)
    /// </summary>
    [Server]
    public void ShowFightConclusion(Dictionary<NetworkEntity, bool> completedFightResults)
    {
        if (!IsServerStarted)
        {
            Debug.LogError("FightConclusionManager: Cannot show conclusion - server not started");
            return;
        }

        if (isConclusionActive.Value)
        {
            Debug.LogWarning("FightConclusionManager: Conclusion already active, skipping");
            return;
        }

        // Check if conclusion should be skipped
        if (autoTestRunner != null && autoTestRunner.ShouldSkipFightConclusion)
        {
            Debug.Log("FightConclusionManager: Skipping fight conclusion, starting draft immediately");
            // Store the fight results for potential debugging/logging
            fightResults = new Dictionary<NetworkEntity, bool>(completedFightResults);
            StartDraftPhase();
            return;
        }

        Debug.Log("FightConclusionManager: Starting fight conclusion sequence");

        // Store the fight results
        fightResults = new Dictionary<NetworkEntity, bool>(completedFightResults);

        // Convert fight results to serializable format for RPC
        FightResultData[] fightResultsArray = completedFightResults.Select(kvp => 
            new FightResultData(kvp.Key.ObjectId, kvp.Value)).ToArray();

        // Set conclusion as active
        isConclusionActive.Value = true;

        // Trigger conclusion on all clients with fight results data
        RpcShowFightConclusion(fightResultsArray);

        // Start the conclusion timing coroutine
        StartConclusionTimingCoroutine();
    }

    /// <summary>
    /// Starts the conclusion timing coroutine with proper duration from animator
    /// </summary>
    [Server]
    private void StartConclusionTimingCoroutine()
    {
        if (conclusionTimingCoroutine != null)
        {
            StopCoroutine(conclusionTimingCoroutine);
        }
        
        conclusionTimingCoroutine = StartCoroutine(ConclusionTimingCoroutine());
    }

    /// <summary>
    /// Server coroutine that handles the conclusion timing and transition to draft
    /// </summary>
    [Server]
    private IEnumerator ConclusionTimingCoroutine()
    {
        float conclusionDuration = ConclusionDuration;
        Debug.Log($"FightConclusionManager: Conclusion timing started - will last {conclusionDuration} seconds");

        // Wait for the conclusion duration (including all animations)
        yield return new WaitForSeconds(conclusionDuration);

        Debug.Log("FightConclusionManager: Conclusion duration complete, transitioning to draft");

        // End the conclusion
        isConclusionActive.Value = false;

        // Notify clients to hide conclusion (though it should already be hidden by animation)
        RpcHideFightConclusion();

        // Wait a bit for clean transition
        yield return new WaitForSeconds(additionalDelayBeforeDraft);

        // Start draft phase
        StartDraftPhase();
        
        conclusionTimingCoroutine = null;
    }

    /// <summary>
    /// Starts the draft phase after conclusion completion
    /// </summary>
    [Server]
    private void StartDraftPhase()
    {
        Debug.Log("FightConclusionManager: Starting draft phase");

        if (draftSetup != null)
        {
            draftSetup.InitializeDraft();
            Debug.Log("FightConclusionManager: Draft phase started successfully");
        }
        else
        {
            Debug.LogError("FightConclusionManager: Cannot start draft - DraftSetup reference is missing");
        }
    }

    #endregion

    #region Client Methods

    /// <summary>
    /// Client-side method to handle conclusion start
    /// </summary>
    private void HandleConclusionStart()
    {
        Debug.Log("FightConclusionManager: Handling conclusion start on client");

        if (fightConclusionUIManager != null)
        {
            fightConclusionUIManager.ShowFightConclusion();
            Debug.Log("FightConclusionManager: Fight conclusion UI shown");
        }
        else
        {
            Debug.LogError("FightConclusionManager: FightConclusionUIManager reference is missing on client");
        }
    }

    /// <summary>
    /// Client-side method to handle conclusion end
    /// </summary>
    private void HandleConclusionEnd()
    {
        Debug.Log("FightConclusionManager: Handling conclusion end on client");

        if (fightConclusionUIManager != null)
        {
            fightConclusionUIManager.HideFightConclusion();
            Debug.Log("FightConclusionManager: Fight conclusion UI hidden");
        }
    }

    #endregion

    #region Event Handlers

    private void OnConclusionActiveChanged(bool prev, bool next, bool asServer)
    {
        Debug.Log($"FightConclusionManager: Conclusion active changed from {prev} to {next} on {(asServer ? "Server" : "Client")}");

        if (!asServer) // Client-side handling
        {
            if (next) // Conclusion started
            {
                HandleConclusionStart();
            }
            else // Conclusion ended
            {
                HandleConclusionEnd();
            }
        }
    }

    /// <summary>
    /// Called when UI manager reports conclusion has started
    /// </summary>
    private void HandleConclusionStarted()
    {
        Debug.Log("FightConclusionManager: UI Manager reported conclusion started");
    }

    /// <summary>
    /// Called when UI manager reports conclusion animation is complete
    /// </summary>
    private void HandleConclusionCompleted()
    {
        Debug.Log("FightConclusionManager: UI Manager reported conclusion completed");
        
        // On clients, we can use this to ensure UI is properly hidden
        // The server timing should handle the draft transition
    }

    #endregion

    #region RPCs

    /// <summary>
    /// RPC to show fight conclusion on all clients with fight results data
    /// </summary>
    [ObserversRpc]
    private void RpcShowFightConclusion(FightResultData[] fightResultsArray)
    {
        Debug.Log("FightConclusionManager: RpcShowFightConclusion called on client");
        
        // Convert the array back to a dictionary with NetworkEntity references
        fightResults.Clear();
        foreach (var resultData in fightResultsArray)
        {
            // Find the NetworkEntity by ObjectId
            if (NetworkManager.ClientManager.Objects.Spawned.TryGetValue(resultData.playerObjectId, out NetworkObject networkObject))
            {
                NetworkEntity playerEntity = networkObject.GetComponent<NetworkEntity>();
                if (playerEntity != null)
                {
                    fightResults[playerEntity] = resultData.playerWon;
                }
            }
        }
        
        Debug.Log($"FightConclusionManager: Received {fightResults.Count} fight results on client");
        
        // The actual showing is handled by the SyncVar change callback
        // This RPC provides the necessary fight results data for the UI
        
        // Ensure combat canvas remains available during conclusion for proper transition
        if (combatCanvasManager != null)
        {
            Debug.Log("FightConclusionManager: Combat canvas will be available during conclusion display");
        }
    }

    /// <summary>
    /// RPC to hide fight conclusion on all clients
    /// </summary>
    [ObserversRpc]
    private void RpcHideFightConclusion()
    {
        Debug.Log("FightConclusionManager: RpcHideFightConclusion called on client");
        
        // The actual hiding should already be handled by the animation system
        // This RPC is mainly for cleanup or fallback scenarios
        
        // Prepare for draft transition
        Debug.Log("FightConclusionManager: Preparing for draft transition");
    }

    /// <summary>
    /// RPC to force stop animations on all clients
    /// </summary>
    [ObserversRpc]
    private void RpcForceStopAnimation()
    {
        if (fightConclusionUIManager != null)
        {
            fightConclusionUIManager.HideFightConclusion();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Gets the current conclusion duration setting from the animator
    /// </summary>
    public float GetConclusionDuration()
    {
        return ConclusionDuration;
    }

    /// <summary>
    /// Sets animation timing on the animator (if available)
    /// </summary>
    public void SetAnimationTiming(float fadeInDuration, float displayDuration, float fadeOutDuration)
    {
        if (fightConclusionAnimator != null)
        {
            fightConclusionAnimator.SetAnimationTiming(fadeInDuration, displayDuration, fadeOutDuration);
            Debug.Log($"FightConclusionManager: Updated animation timing - Total duration now: {ConclusionDuration}");
        }
        else
        {
            Debug.LogWarning("FightConclusionManager: No FightConclusionAnimator found, cannot set timing");
        }
    }

    /// <summary>
    /// Sets the additional delay before draft starts
    /// </summary>
    public void SetAdditionalDelayBeforeDraft(float delay)
    {
        additionalDelayBeforeDraft = Mathf.Max(0f, delay);
        Debug.Log($"FightConclusionManager: Additional delay before draft set to {additionalDelayBeforeDraft} seconds");
    }

    /// <summary>
    /// Emergency method to force end conclusion (for debugging/edge cases)
    /// </summary>
    [Server]
    public void ForceEndConclusion()
    {
        if (isConclusionActive.Value)
        {
            Debug.Log("FightConclusionManager: Force ending conclusion");
            
            // Stop the timing coroutine
            if (conclusionTimingCoroutine != null)
            {
                StopCoroutine(conclusionTimingCoroutine);
                conclusionTimingCoroutine = null;
            }
            
            // Stop animations on clients
            RpcForceStopAnimation();
            
            // End conclusion state
            isConclusionActive.Value = false;
            RpcHideFightConclusion();
            
            // Start draft immediately
            StartDraftPhase();
        }
    }

    /// <summary>
    /// Gets the fight results data for UI display
    /// </summary>
    public Dictionary<NetworkEntity, bool> GetFightResults()
    {
        return new Dictionary<NetworkEntity, bool>(fightResults);
    }

    /// <summary>
    /// Validates that all required components are properly assigned
    /// </summary>
    public bool ValidateSetup()
    {
        bool isValid = true;
        
        if (fightConclusionUIManager == null)
        {
            Debug.LogError("FightConclusionManager: FightConclusionUIManager is not assigned");
            isValid = false;
        }
        
        if (fightConclusionAnimator == null)
        {
            Debug.LogWarning("FightConclusionManager: FightConclusionAnimator is not assigned - animations may not work");
        }
        
        if (draftSetup == null)
        {
            Debug.LogError("FightConclusionManager: DraftSetup is not assigned");
            isValid = false;
        }
        
        return isValid;
    }

    #endregion
} 