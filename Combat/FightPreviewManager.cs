using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages the fight preview interstitial flow between combat setup and combat start.
/// Coordinates the preview display and transition to actual combat using FightPreviewAnimator.
/// Attach to: A NetworkObject in the scene that manages the fight preview timing.
/// </summary>
public class FightPreviewManager : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] private FightPreviewUIManager fightPreviewUIManager;
    [SerializeField] private FightPreviewAnimator fightPreviewAnimator;
    [SerializeField] private CombatManager combatManager;
    [SerializeField] private CombatCanvasManager combatCanvasManager;
    [SerializeField] private GamePhaseManager gamePhaseManager;
    [SerializeField] private AutoTestRunner autoTestRunner;

    [Header("Timing Settings")]
    [SerializeField] private float additionalDelayBeforeCombat = 0.5f;

    // Track preview state
    private readonly SyncVar<bool> isPreviewActive = new SyncVar<bool>(false);
    private readonly SyncDictionary<NetworkConnection, bool> clientsReadyForPreview = new SyncDictionary<NetworkConnection, bool>();
    private bool allClientsReadyForPreview = false;

    // Animation tracking
    private Coroutine previewTimingCoroutine;

    public bool IsPreviewActive => isPreviewActive.Value;
    public float PreviewDuration => fightPreviewAnimator != null ? fightPreviewAnimator.TotalDuration : 3f;

    #region Lifecycle

    private void Awake()
    {
        FindRequiredComponents();
        SetupUIManagerEvents();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        isPreviewActive.OnChange += OnPreviewActiveChanged;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        isPreviewActive.OnChange += OnPreviewActiveChanged;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        isPreviewActive.OnChange -= OnPreviewActiveChanged;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        isPreviewActive.OnChange -= OnPreviewActiveChanged;
    }

    private void OnDestroy()
    {
        CleanupUIManagerEvents();
    }

    #endregion

    #region Component Resolution

    private void FindRequiredComponents()
    {
        if (fightPreviewUIManager == null)
        {
            fightPreviewUIManager = FindFirstObjectByType<FightPreviewUIManager>();
        }

        if (fightPreviewAnimator == null)
        {
            fightPreviewAnimator = FindFirstObjectByType<FightPreviewAnimator>();
            
            // If not found globally, try to get from UI manager
            if (fightPreviewAnimator == null && fightPreviewUIManager != null)
            {
                fightPreviewAnimator = fightPreviewUIManager.GetAnimator();
            }
        }

        if (combatManager == null)
        {
            combatManager = FindFirstObjectByType<CombatManager>();
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
        if (fightPreviewUIManager != null)
        {
            fightPreviewUIManager.OnPreviewStarted += HandlePreviewStarted;
            fightPreviewUIManager.OnPreviewCompleted += HandlePreviewCompleted;
        }
    }

    private void CleanupUIManagerEvents()
    {
        if (fightPreviewUIManager != null)
        {
            fightPreviewUIManager.OnPreviewStarted -= HandlePreviewStarted;
            fightPreviewUIManager.OnPreviewCompleted -= HandlePreviewCompleted;
        }
    }

    #endregion

    #region Server Methods

    /// <summary>
    /// Starts the fight preview sequence (called by CombatSetup)
    /// </summary>
    [Server]
    public void StartFightPreview()
    {
        if (!IsServerStarted)
        {
            Debug.LogError("FightPreviewManager: Cannot start preview - server not started");
            return;
        }

        if (isPreviewActive.Value)
        {
            Debug.LogWarning("FightPreviewManager: Preview already active, skipping");
            return;
        }

        // Check if preview should be skipped
        if (autoTestRunner != null && autoTestRunner.ShouldSkipFightPreview)
        {
            Debug.Log("FightPreviewManager: Skipping fight preview, starting combat immediately");
            StartActualCombat();
            return;
        }

        Debug.Log("FightPreviewManager: Starting fight preview sequence");

        // Reset client ready states
        clientsReadyForPreview.Clear();
        allClientsReadyForPreview = false;

        // Set preview as active
        isPreviewActive.Value = true;

        // Trigger preview on all clients
        RpcShowFightPreview();

        // Start the preview timing coroutine using animator duration
        StartPreviewTimingCoroutine();
    }

    /// <summary>
    /// Starts the preview timing coroutine with proper duration from animator
    /// </summary>
    [Server]
    private void StartPreviewTimingCoroutine()
    {
        if (previewTimingCoroutine != null)
        {
            StopCoroutine(previewTimingCoroutine);
        }
        
        previewTimingCoroutine = StartCoroutine(PreviewTimingCoroutine());
    }

    /// <summary>
    /// Server coroutine that handles the preview timing and transition to combat
    /// </summary>
    [Server]
    private IEnumerator PreviewTimingCoroutine()
    {
        float previewDuration = PreviewDuration;
        Debug.Log($"FightPreviewManager: Preview timing started - will last {previewDuration} seconds");

        // Wait for the preview duration (including all animations)
        yield return new WaitForSeconds(previewDuration);

        Debug.Log("FightPreviewManager: Preview duration complete, transitioning to combat");

        // End the preview
        isPreviewActive.Value = false;

        // Notify clients to hide preview (though it should already be hidden by animation)
        RpcHideFightPreview();

        // Wait a bit for clean transition
        yield return new WaitForSeconds(additionalDelayBeforeCombat);

        // Start actual combat
        StartActualCombat();
        
        previewTimingCoroutine = null;
    }

    /// <summary>
    /// Starts the actual combat after preview completion
    /// </summary>
    [Server]
    private void StartActualCombat()
    {
        Debug.Log("FightPreviewManager: Starting actual combat");

        if (combatManager != null)
        {
            combatManager.StartCombat();
            Debug.Log("FightPreviewManager: Combat started successfully");
        }
        else
        {
            Debug.LogError("FightPreviewManager: Cannot start combat - CombatManager reference is missing");
        }
    }

    #endregion

    #region Client Methods

    /// <summary>
    /// Client-side method to handle preview start
    /// </summary>
    private void HandlePreviewStart()
    {
        Debug.Log("FightPreviewManager: Handling preview start on client");

        if (fightPreviewUIManager != null)
        {
            fightPreviewUIManager.ShowFightPreview();
            Debug.Log("FightPreviewManager: Fight preview UI shown");
        }
        else
        {
            Debug.LogError("FightPreviewManager: FightPreviewUIManager reference is missing on client");
        }
    }

    /// <summary>
    /// Client-side method to handle preview end
    /// </summary>
    private void HandlePreviewEnd()
    {
        Debug.Log("FightPreviewManager: Handling preview end on client");

        if (fightPreviewUIManager != null)
        {
            fightPreviewUIManager.HideFightPreview();
            Debug.Log("FightPreviewManager: Fight preview UI hidden");
        }
    }

    #endregion

    #region Event Handlers

    private void OnPreviewActiveChanged(bool prev, bool next, bool asServer)
    {
        Debug.Log($"FightPreviewManager: Preview active changed from {prev} to {next} on {(asServer ? "Server" : "Client")}");

        if (!asServer) // Client-side handling
        {
            if (next) // Preview started
            {
                HandlePreviewStart();
            }
            else // Preview ended
            {
                HandlePreviewEnd();
            }
        }
    }

    /// <summary>
    /// Called when UI manager reports preview has started
    /// </summary>
    private void HandlePreviewStarted()
    {
        Debug.Log("FightPreviewManager: UI Manager reported preview started");
    }

    /// <summary>
    /// Called when UI manager reports preview animation is complete
    /// </summary>
    private void HandlePreviewCompleted()
    {
        Debug.Log("FightPreviewManager: UI Manager reported preview completed");
        
        // On clients, we can use this to ensure UI is properly hidden
        // The server timing should handle the combat transition
    }

    #endregion

    #region RPCs

    /// <summary>
    /// RPC to show fight preview on all clients
    /// </summary>
    [ObserversRpc]
    private void RpcShowFightPreview()
    {
        Debug.Log("FightPreviewManager: RpcShowFightPreview called on client");
        
        // The actual showing is handled by the SyncVar change callback
        // This RPC can be used for additional client-side setup if needed
        
        // Ensure combat canvas is hidden during preview
        if (combatCanvasManager != null)
        {
            // Don't hide completely, just ensure preview takes precedence
            Debug.Log("FightPreviewManager: Combat canvas will remain ready but preview will show on top");
        }
    }

    /// <summary>
    /// RPC to hide fight preview on all clients
    /// </summary>
    [ObserversRpc]
    private void RpcHideFightPreview()
    {
        Debug.Log("FightPreviewManager: RpcHideFightPreview called on client");
        
        // The actual hiding should already be handled by the animation system
        // This RPC is mainly for cleanup or fallback scenarios
        
        // Ensure combat canvas is ready to be shown
        if (combatCanvasManager != null)
        {
            Debug.Log("FightPreviewManager: Preparing combat canvas for display");
        }
    }

    /// <summary>
    /// RPC to force stop animations on all clients
    /// </summary>
    [ObserversRpc]
    private void RpcForceStopAnimation()
    {
        if (fightPreviewUIManager != null)
        {
            fightPreviewUIManager.HideFightPreview();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Gets the current preview duration setting from the animator
    /// </summary>
    public float GetPreviewDuration()
    {
        return PreviewDuration;
    }

    /// <summary>
    /// Sets animation timing on the animator (if available)
    /// </summary>
    public void SetAnimationTiming(float fadeInDuration, float displayDuration, float fadeOutDuration)
    {
        if (fightPreviewAnimator != null)
        {
            fightPreviewAnimator.SetAnimationTiming(fadeInDuration, displayDuration, fadeOutDuration);
            Debug.Log($"FightPreviewManager: Updated animation timing - Total duration now: {PreviewDuration}");
        }
        else
        {
            Debug.LogWarning("FightPreviewManager: No FightPreviewAnimator found, cannot set timing");
        }
    }

    /// <summary>
    /// Sets the additional delay before combat starts
    /// </summary>
    public void SetAdditionalDelayBeforeCombat(float delay)
    {
        additionalDelayBeforeCombat = Mathf.Max(0f, delay);
        Debug.Log($"FightPreviewManager: Additional delay before combat set to {additionalDelayBeforeCombat} seconds");
    }

    /// <summary>
    /// Emergency method to force end preview (for debugging/edge cases)
    /// </summary>
    [Server]
    public void ForceEndPreview()
    {
        if (isPreviewActive.Value)
        {
            Debug.Log("FightPreviewManager: Force ending preview");
            
            // Stop the timing coroutine
            if (previewTimingCoroutine != null)
            {
                StopCoroutine(previewTimingCoroutine);
                previewTimingCoroutine = null;
            }
            
            // Stop animations on clients
            RpcForceStopAnimation();
            
            // End preview state
            isPreviewActive.Value = false;
            RpcHideFightPreview();
            
            // Start combat immediately
            StartActualCombat();
        }
    }

    /// <summary>
    /// Validates that all required components are properly assigned
    /// </summary>
    public bool ValidateSetup()
    {
        bool isValid = true;
        
        if (fightPreviewUIManager == null)
        {
            Debug.LogError("FightPreviewManager: FightPreviewUIManager is not assigned");
            isValid = false;
        }
        
        if (fightPreviewAnimator == null)
        {
            Debug.LogWarning("FightPreviewManager: FightPreviewAnimator is not assigned - animations may not work");
        }
        
        if (combatManager == null)
        {
            Debug.LogError("FightPreviewManager: CombatManager is not assigned");
            isValid = false;
        }
        
        return isValid;
    }

    #endregion
} 