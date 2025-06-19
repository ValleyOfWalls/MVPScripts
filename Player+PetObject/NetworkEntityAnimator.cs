using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// Handles animations for NetworkEntity 3D models - simplified version that just handles idle animations
/// Attach to: The same GameObject as NetworkEntity (the parent, not the 3D model itself)
/// </summary>
public class NetworkEntityAnimator : NetworkBehaviour
{
    [Header("Animation References")]
    [SerializeField] private Animator modelAnimator;
    [SerializeField] private Transform modelTransform; // Reference to the 3D model transform
    
    [Header("Animation Settings")]
    [SerializeField] private string idleAnimationTrigger = "Idle";
    [SerializeField] private string attackAnimationTrigger = "Attack";
    [SerializeField] private string takeDamageAnimationTrigger = "TakeDamage";
    [SerializeField] private string defeatedAnimationTrigger = "Defeated";
    
    // Networked animation state
    private readonly SyncVar<AnimationState> currentAnimationState = new SyncVar<AnimationState>(AnimationState.None);
    
    // References
    private NetworkEntity networkEntity;
    private NetworkEntityUI entityUI;
    
    // Animation state tracking
    private bool hasStartedIdleAnimation = false;
    private bool isAnimationInitialized = false;

    public enum AnimationState
    {
        None,
        Idle,
        Moving,
        Attacking,
        TakingDamage,
        Defeated
    }

    public AnimationState CurrentState => currentAnimationState.Value;
    public bool IsIdleAnimationStarted => hasStartedIdleAnimation;

    private void Awake()
    {
        // Get required components
        networkEntity = GetComponent<NetworkEntity>();
        entityUI = GetComponent<NetworkEntityUI>();
        
        // If modelTransform isn't set, try to get it from NetworkEntityUI
        if (modelTransform == null && entityUI != null)
        {
            modelTransform = entityUI.GetEntityModel();
        }
        
        // Find the Animator on the model
        if (modelTransform != null && modelAnimator == null)
        {
            modelAnimator = modelTransform.GetComponent<Animator>();
            if (modelAnimator == null)
            {
                modelAnimator = modelTransform.GetComponentInChildren<Animator>();
            }
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        currentAnimationState.OnChange += OnAnimationStateChanged;
        InitializeAnimation();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        currentAnimationState.OnChange += OnAnimationStateChanged;
        InitializeAnimation();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        currentAnimationState.OnChange -= OnAnimationStateChanged;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        currentAnimationState.OnChange -= OnAnimationStateChanged;
    }

    private void InitializeAnimation()
    {
        if (isAnimationInitialized) return;
        
        ValidateAnimationSetup();
        
        if (modelAnimator != null)
        {
            isAnimationInitialized = true;
    
        }
    }

    private void ValidateAnimationSetup()
    {
        if (modelAnimator == null)
        {
            Debug.LogError($"NetworkEntityAnimator on {gameObject.name}: No Animator found on model. Please add an Animator component to the 3D model.");
            return;
        }

        if (modelAnimator.runtimeAnimatorController == null)
        {
            Debug.LogError($"NetworkEntityAnimator on {gameObject.name}: No Animator Controller assigned to the Animator.");
            return;
        }

        // Validate animation parameters exist
        bool hasIdleTrigger = HasAnimatorParameter(idleAnimationTrigger, AnimatorControllerParameterType.Trigger);
        bool hasAttackTrigger = HasAnimatorParameter(attackAnimationTrigger, AnimatorControllerParameterType.Trigger);
        bool hasTakeDamageTrigger = HasAnimatorParameter(takeDamageAnimationTrigger, AnimatorControllerParameterType.Trigger);
        bool hasDefeatedTrigger = HasAnimatorParameter(defeatedAnimationTrigger, AnimatorControllerParameterType.Trigger);

        if (!hasIdleTrigger)
        {
            Debug.LogWarning($"NetworkEntityAnimator on {gameObject.name}: Idle trigger '{idleAnimationTrigger}' not found in Animator Controller. Idle will play as entry state.");
        }
        
        if (!hasAttackTrigger)
        {
            Debug.LogWarning($"NetworkEntityAnimator on {gameObject.name}: Attack trigger '{attackAnimationTrigger}' not found in Animator Controller.");
        }
        
        if (!hasTakeDamageTrigger)
        {
            Debug.LogWarning($"NetworkEntityAnimator on {gameObject.name}: Take damage trigger '{takeDamageAnimationTrigger}' not found in Animator Controller.");
        }
        
        if (!hasDefeatedTrigger)
        {
            Debug.LogWarning($"NetworkEntityAnimator on {gameObject.name}: Defeated trigger '{defeatedAnimationTrigger}' not found in Animator Controller.");
        }


    }

    private bool HasAnimatorParameter(string paramName, AnimatorControllerParameterType paramType)
    {
        if (modelAnimator == null || modelAnimator.runtimeAnimatorController == null)
            return false;

        foreach (AnimatorControllerParameter param in modelAnimator.parameters)
        {
            if (param.name == paramName && param.type == paramType)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Starts the idle animation when the entity becomes visible
    /// </summary>
    [Server]
    public void StartIdleAnimation()
    {
        if (!IsServerStarted || hasStartedIdleAnimation || !isAnimationInitialized)
        {

            return;
        }


        
        currentAnimationState.Value = AnimationState.Idle;
        hasStartedIdleAnimation = true;
        
        // Ensure the animator is enabled
        if (modelAnimator != null)
        {
            modelAnimator.enabled = true;
        }
    }

    /// <summary>
    /// Plays the idle animation (can be called multiple times)
    /// </summary>
    [Server]
    public void PlayIdleAnimation()
    {
        if (!IsServerStarted || !isAnimationInitialized) return;
        
        if (currentAnimationState.Value != AnimationState.Idle)
        {
    
            currentAnimationState.Value = AnimationState.Idle;
        }
    }

    /// <summary>
    /// ServerRpc to start idle animation from client
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void ServerStartIdleAnimation()
    {
        StartIdleAnimation();
    }

    /// <summary>
    /// Sets new model references (called by EntityModelManager)
    /// </summary>
    public void SetModelReferences(Transform newModelTransform, Animator newModelAnimator)
    {
        // Clear old references
        modelTransform = newModelTransform;
        modelAnimator = newModelAnimator;
        
        // Reset animation state
        isAnimationInitialized = false;
        hasStartedIdleAnimation = false;
        
        // Re-initialize with new model
        if (IsServerStarted || IsClientStarted)
        {
            InitializeAnimation();
        }
        
        Debug.Log($"NetworkEntityAnimator: Set new model references - Transform: {(newModelTransform != null ? newModelTransform.name : "None")}, Animator: {(newModelAnimator != null ? newModelAnimator.name : "None")}");
    }

    /// <summary>
    /// Gets the current model transform
    /// </summary>
    public Transform GetModelTransform()
    {
        return modelTransform;
    }

    /// <summary>
    /// Gets the current model animator
    /// </summary>
    public Animator GetModelAnimator()
    {
        return modelAnimator;
    }

    private void OnAnimationStateChanged(AnimationState prev, AnimationState next, bool asServer)
    {

        
        if (modelAnimator == null || !isAnimationInitialized)
        {
            Debug.LogWarning($"NetworkEntityAnimator: Cannot play animation - Animator not ready for {networkEntity?.EntityName.Value}");
            return;
        }

        // Play the appropriate animation based on state
        switch (next)
        {
            case AnimationState.Idle:
                // Try to trigger idle animation, but if no trigger exists, it should already be playing as entry state
                if (HasAnimatorParameter(idleAnimationTrigger, AnimatorControllerParameterType.Trigger))
                {
                    modelAnimator.SetTrigger(idleAnimationTrigger);
                    Debug.Log($"NetworkEntityAnimator: Triggered idle animation for {networkEntity?.EntityName.Value}");
                }
                else
                {
                    Debug.Log($"NetworkEntityAnimator: Idle animation playing automatically (entry state) for {networkEntity?.EntityName.Value}");
                }
                break;
                
            case AnimationState.Attacking:
                if (HasAnimatorParameter(attackAnimationTrigger, AnimatorControllerParameterType.Trigger))
                {
                    modelAnimator.SetTrigger(attackAnimationTrigger);
                    Debug.Log($"NetworkEntityAnimator: Triggered attack animation for {networkEntity?.EntityName.Value}");
                    
                    // Auto-return to idle after attack animation
                    StartCoroutine(ReturnToIdleAfterDelay(1f));
                }
                break;
                
            case AnimationState.TakingDamage:
                if (HasAnimatorParameter(takeDamageAnimationTrigger, AnimatorControllerParameterType.Trigger))
                {
                    modelAnimator.SetTrigger(takeDamageAnimationTrigger);
                    Debug.Log($"NetworkEntityAnimator: Triggered take damage animation for {networkEntity?.EntityName.Value}");
                    
                    // Auto-return to idle after damage animation
                    StartCoroutine(ReturnToIdleAfterDelay(0.5f));
                }
                break;
                
            case AnimationState.Defeated:
                if (HasAnimatorParameter(defeatedAnimationTrigger, AnimatorControllerParameterType.Trigger))
                {
                    modelAnimator.SetTrigger(defeatedAnimationTrigger);
                    Debug.Log($"NetworkEntityAnimator: Triggered defeated animation for {networkEntity?.EntityName.Value}");
                }
                break;
        }
    }

    /// <summary>
    /// Called when the entity becomes visible during combat setup
    /// </summary>
    public void OnEntityBecameVisible()
    {
        if (!hasStartedIdleAnimation && IsServerStarted)
        {
            Debug.Log($"NetworkEntityAnimator: Entity became visible, starting idle animation for {networkEntity?.EntityName.Value}");
            StartIdleAnimation();
        }
        else if (hasStartedIdleAnimation && IsServerStarted)
        {
            Debug.Log($"NetworkEntityAnimator: Entity became visible, ensuring idle animation is playing for {networkEntity?.EntityName.Value}");
            PlayIdleAnimation();
        }
    }

    /// <summary>
    /// Animation control methods for combat
    /// </summary>
    [Server]
    public void PlayAttackAnimation()
    {
        Debug.Log($"NetworkEntityAnimator: PlayAttackAnimation called for {networkEntity?.EntityName.Value} - IsServer: {IsServerStarted}, IsInitialized: {isAnimationInitialized}");
        
        if (!IsServerStarted || !isAnimationInitialized) 
        {
            Debug.LogWarning($"NetworkEntityAnimator: Cannot play attack animation - IsServer: {IsServerStarted}, IsInitialized: {isAnimationInitialized}");
            return;
        }
        
        Debug.Log($"NetworkEntityAnimator: Setting animation state to Attacking for {networkEntity?.EntityName.Value}");
        currentAnimationState.Value = AnimationState.Attacking;
    }

    [Server]
    public void PlayTakeDamageAnimation()
    {
        Debug.Log($"NetworkEntityAnimator: PlayTakeDamageAnimation called for {networkEntity?.EntityName.Value} - IsServer: {IsServerStarted}, IsInitialized: {isAnimationInitialized}");
        
        if (!IsServerStarted || !isAnimationInitialized) 
        {
            Debug.LogWarning($"NetworkEntityAnimator: Cannot play take damage animation - IsServer: {IsServerStarted}, IsInitialized: {isAnimationInitialized}");
            return;
        }
        
        Debug.Log($"NetworkEntityAnimator: Setting animation state to TakingDamage for {networkEntity?.EntityName.Value}");
        currentAnimationState.Value = AnimationState.TakingDamage;
    }

    [Server]
    public void PlayDefeatedAnimation()
    {
        if (!IsServerStarted || !isAnimationInitialized) return;
        currentAnimationState.Value = AnimationState.Defeated;
    }
    
    /// <summary>
    /// Coroutine to return to idle state after a delay
    /// </summary>
    private System.Collections.IEnumerator ReturnToIdleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (IsServerStarted && currentAnimationState.Value != AnimationState.Defeated)
        {
            PlayIdleAnimation();
        }
    }

    #region Inspector Helpers

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    [ContextMenu("Test Start Idle Animation")]
    private void TestStartIdleAnimation()
    {
        if (Application.isPlaying && IsServerStarted)
        {
            hasStartedIdleAnimation = false; // Reset for testing
            StartIdleAnimation();
        }
    }

    [ContextMenu("Test Play Idle Animation")]
    private void TestPlayIdleAnimation()
    {
        if (Application.isPlaying && IsServerStarted)
        {
            PlayIdleAnimation();
        }
    }

    [ContextMenu("Validate Animation Setup")]
    private void TestValidateSetup()
    {
        ValidateAnimationSetup();
    }

    #endregion

    #region Legacy Methods (for backwards compatibility)

    /// <summary>
    /// Legacy method - now calls StartIdleAnimation for backwards compatibility
    /// </summary>
    [Server]
    public void PlayEntranceSequence()
    {
        StartIdleAnimation();
    }

    /// <summary>
    /// Legacy method - now calls StartIdleAnimation for backwards compatibility
    /// </summary>
    [Server]
    public void PlaySpawnAnimation()
    {
        StartIdleAnimation();
    }

    /// <summary>
    /// Legacy method - now calls StartIdleAnimation for backwards compatibility
    /// </summary>
    [Server]
    public void TriggerSpawnAnimation()
    {
        StartIdleAnimation();
    }

    #endregion
} 