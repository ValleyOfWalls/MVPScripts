using UnityEngine;
using FishNet.Object;
using System.Collections;

/// <summary>
/// Simplified animation controller that directly triggers animations on 3D models
/// Network synchronization is handled by FishNet's NetworkAnimator component on the model
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
    
    // References
    private NetworkEntity networkEntity;
    private bool isAnimationInitialized = false;
    
    // Animation state management
    private bool isPlayingAttackAnimation = false;
    private bool isPlayingDamageAnimation = false;
    private Coroutine attackReturnToIdleCoroutine;
    private Coroutine damageReturnToIdleCoroutine;
    
    // Cooldown system for rapid animation triggers
    private float lastAttackTime = 0f;
    private float lastDamageTime = 0f;
    private const float ANIMATION_COOLDOWN = 1.0f; // Increased cooldown to prevent rapid triggers

    private void Awake()
    {
        // Get references
        networkEntity = GetComponent<NetworkEntity>();
        
        if (networkEntity == null)
        {
            Debug.LogError($"NetworkEntityAnimator on {gameObject.name}: No NetworkEntity component found. Please add a NetworkEntity component.");
        }
    }

    private void OnDestroy()
    {
        // Clean up coroutines
        if (attackReturnToIdleCoroutine != null)
        {
            StopCoroutine(attackReturnToIdleCoroutine);
            attackReturnToIdleCoroutine = null;
        }
        
        if (damageReturnToIdleCoroutine != null)
        {
            StopCoroutine(damageReturnToIdleCoroutine);
            damageReturnToIdleCoroutine = null;
        }
    }

    public override void OnStartServer()
    {
        InitializeAnimation();
    }

    public override void OnStartClient()
    {
        InitializeAnimation();
    }

    private void InitializeAnimation()
    {
        if (isAnimationInitialized) return;

        ValidateAnimationSetup();
        isAnimationInitialized = true;
        
        Debug.Log($"NetworkEntityAnimator: Initialized for {networkEntity?.EntityName.Value}");
    }

    private void ValidateAnimationSetup()
    {
        if (modelAnimator == null)
        {
            Debug.LogError($"NetworkEntityAnimator on {gameObject.name}: No Animator found on model. Please add an Animator component to the 3D model and assign it to modelAnimator field.");
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
            Debug.LogWarning($"NetworkEntityAnimator on {gameObject.name}: Idle trigger '{idleAnimationTrigger}' not found in Animator Controller. Idle should play as entry state.");
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

        Debug.Log($"NetworkEntityAnimator: Animation setup validated for {networkEntity?.EntityName.Value}");
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
    /// Animation control methods for combat - these directly trigger animations
    /// Network synchronization is handled by FishNet's NetworkAnimator on the model
    /// </summary>
    public void PlayAttackAnimation()
    {
        Debug.Log($"NetworkEntityAnimator: PlayAttackAnimation called for {networkEntity?.EntityName.Value}");
        
        if (!isAnimationInitialized || modelAnimator == null) 
        {
            Debug.LogWarning($"NetworkEntityAnimator: Cannot play attack animation - not initialized or missing animator");
            return;
        }
        
        // Cooldown protection for rapid card plays
        if (Time.time - lastAttackTime < ANIMATION_COOLDOWN)
        {
            Debug.LogWarning($"NetworkEntityAnimator: Attack animation on cooldown for {networkEntity?.EntityName.Value} (last played {Time.time - lastAttackTime:F2}s ago)");
            return;
        }
        
        // Additional protection: don't trigger if already playing attack animation
        if (isPlayingAttackAnimation)
        {
            Debug.LogWarning($"NetworkEntityAnimator: Attack animation already playing for {networkEntity?.EntityName.Value}, ignoring new request");
            return;
        }
        
        if (HasAnimatorParameter(attackAnimationTrigger, AnimatorControllerParameterType.Trigger))
        {
            Debug.Log($"NetworkEntityAnimator: Triggering attack animation '{attackAnimationTrigger}' for {networkEntity?.EntityName.Value}");
            
            // Cancel any existing return-to-idle coroutine
            if (attackReturnToIdleCoroutine != null)
            {
                StopCoroutine(attackReturnToIdleCoroutine);
                attackReturnToIdleCoroutine = null;
            }
            
            // Update state tracking
            isPlayingAttackAnimation = true;
            lastAttackTime = Time.time;
            
            // Trigger the animation
            modelAnimator.SetTrigger(attackAnimationTrigger);
            
            // Log current state for debugging
            LogAnimatorState("Attack");
            
            // Start coroutine to return to idle after animation duration
            attackReturnToIdleCoroutine = StartCoroutine(ReturnToIdleAfterAttack());
        }
        else
        {
            Debug.LogWarning($"NetworkEntityAnimator: Attack trigger '{attackAnimationTrigger}' not found for {networkEntity?.EntityName.Value}");
        }
    }

    public void PlayTakeDamageAnimation()
    {
        Debug.Log($"NetworkEntityAnimator: PlayTakeDamageAnimation called for {networkEntity?.EntityName.Value}");
        
        if (!isAnimationInitialized || modelAnimator == null) 
        {
            Debug.LogWarning($"NetworkEntityAnimator: Cannot play take damage animation - not initialized or missing animator");
            return;
        }
        
        // Cooldown protection for rapid damage animations
        if (Time.time - lastDamageTime < ANIMATION_COOLDOWN)
        {
            Debug.LogWarning($"NetworkEntityAnimator: Damage animation on cooldown for {networkEntity?.EntityName.Value} (last played {Time.time - lastDamageTime:F2}s ago)");
            return;
        }
        
        // Additional protection: don't trigger if already playing damage animation
        if (isPlayingDamageAnimation)
        {
            Debug.LogWarning($"NetworkEntityAnimator: Damage animation already playing for {networkEntity?.EntityName.Value}, ignoring new request");
            return;
        }
        
        if (HasAnimatorParameter(takeDamageAnimationTrigger, AnimatorControllerParameterType.Trigger))
        {
            Debug.Log($"NetworkEntityAnimator: Triggering take damage animation '{takeDamageAnimationTrigger}' for {networkEntity?.EntityName.Value}");
            
            // Cancel any existing return-to-idle coroutine
            if (damageReturnToIdleCoroutine != null)
            {
                StopCoroutine(damageReturnToIdleCoroutine);
                damageReturnToIdleCoroutine = null;
            }
            
            // Update state tracking
            isPlayingDamageAnimation = true;
            lastDamageTime = Time.time;
            
            // Trigger the animation
            modelAnimator.SetTrigger(takeDamageAnimationTrigger);
            
            // Log current state for debugging
            LogAnimatorState("TakeDamage");
            
            // Start coroutine to return to idle after animation duration
            damageReturnToIdleCoroutine = StartCoroutine(ReturnToIdleAfterDamage());
        }
        else
        {
            Debug.LogWarning($"NetworkEntityAnimator: Take damage trigger '{takeDamageAnimationTrigger}' not found for {networkEntity?.EntityName.Value}");
        }
    }

    public void PlayIdleAnimation()
    {
        Debug.Log($"NetworkEntityAnimator: PlayIdleAnimation called for {networkEntity?.EntityName.Value}");
        
        if (!isAnimationInitialized || modelAnimator == null) 
        {
            Debug.LogWarning($"NetworkEntityAnimator: Cannot play idle animation - not initialized or missing animator");
            return;
        }
        
        if (HasAnimatorParameter(idleAnimationTrigger, AnimatorControllerParameterType.Trigger))
        {
            Debug.Log($"NetworkEntityAnimator: Triggering idle animation '{idleAnimationTrigger}' for {networkEntity?.EntityName.Value}");
            modelAnimator.SetTrigger(idleAnimationTrigger);
        }
        else
        {
            Debug.Log($"NetworkEntityAnimator: Idle animation should play automatically (entry state) for {networkEntity?.EntityName.Value}");
        }
    }

    public void PlayDefeatedAnimation()
    {
        Debug.Log($"NetworkEntityAnimator: PlayDefeatedAnimation called for {networkEntity?.EntityName.Value}");
        
        if (!isAnimationInitialized || modelAnimator == null) 
        {
            Debug.LogWarning($"NetworkEntityAnimator: Cannot play defeated animation - not initialized or missing animator");
            return;
        }
        
        if (HasAnimatorParameter(defeatedAnimationTrigger, AnimatorControllerParameterType.Trigger))
        {
            Debug.Log($"NetworkEntityAnimator: Triggering defeated animation '{defeatedAnimationTrigger}' for {networkEntity?.EntityName.Value}");
            modelAnimator.SetTrigger(defeatedAnimationTrigger);
        }
        else
        {
            Debug.LogWarning($"NetworkEntityAnimator: Defeated trigger '{defeatedAnimationTrigger}' not found for {networkEntity?.EntityName.Value}");
        }
    }

    /// <summary>
    /// Logs current animator state information for debugging
    /// </summary>
    private void LogAnimatorState(string context)
    {
        if (modelAnimator == null || modelAnimator.layerCount == 0) return;
        
        AnimatorStateInfo stateInfo = modelAnimator.GetCurrentAnimatorStateInfo(0);
        
        // Try to get the actual state name for better debugging
        string stateName = "Unknown";
        AnimatorClipInfo[] clipInfos = modelAnimator.GetCurrentAnimatorClipInfo(0);
        if (clipInfos.Length > 0)
        {
            stateName = clipInfos[0].clip.name;
        }
        
        Debug.Log($"NetworkEntityAnimator [{context}]: Current clip: '{stateName}', State Hash: {stateInfo.fullPathHash}, NormalizedTime: {stateInfo.normalizedTime:F2}");
        Debug.Log($"NetworkEntityAnimator [{context}]: Length: {stateInfo.length}s, Speed: {stateInfo.speed}, Loop: {stateInfo.loop}");
        
        // Check for transition issues
        if (modelAnimator.IsInTransition(0))
        {
            AnimatorTransitionInfo transitionInfo = modelAnimator.GetAnimatorTransitionInfo(0);
            Debug.Log($"NetworkEntityAnimator [{context}]: In transition - Duration: {transitionInfo.duration}s, Progress: {transitionInfo.normalizedTime:F2}");
        }
        
        // Warn about potential issues
        if (stateInfo.normalizedTime > 5f)
        {
            Debug.LogWarning($"NetworkEntityAnimator [{context}]: High normalizedTime ({stateInfo.normalizedTime:F2}) suggests animation might not be transitioning properly");
        }
    }

    /// <summary>
    /// Sets new model references (called by EntityModelManager)
    /// </summary>
    public void SetModelReferences(Transform newModelTransform, Animator newModelAnimator)
    {
        modelTransform = newModelTransform;
        modelAnimator = newModelAnimator;
        
        // Reset animation state
        isAnimationInitialized = false;
        
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

    /// <summary>
    /// Called when the entity becomes visible during combat setup (backwards compatibility)
    /// </summary>
    public void OnEntityBecameVisible()
    {
        Debug.Log($"NetworkEntityAnimator: Entity became visible, playing idle animation for {networkEntity?.EntityName.Value}");
        PlayIdleAnimation();
    }

    /// <summary>
    /// Starts idle animation (backwards compatibility method)
    /// </summary>
    public void StartIdleAnimation()
    {
        Debug.Log($"NetworkEntityAnimator: StartIdleAnimation called for {networkEntity?.EntityName.Value}");
        PlayIdleAnimation();
    }

    /// <summary>
    /// Coroutines for automatic return to idle
    /// </summary>
    private IEnumerator ReturnToIdleAfterAttack()
    {
        // Wait for the attack animation to complete
        // We'll wait a bit longer than the expected animation duration to ensure it completes
        yield return new WaitForSeconds(1.2f);
        
        if (isPlayingAttackAnimation)
        {
            Debug.Log($"NetworkEntityAnimator: Attack animation completed, returning to idle for {networkEntity?.EntityName.Value}");
            isPlayingAttackAnimation = false;
            PlayIdleAnimation();
        }
        
        attackReturnToIdleCoroutine = null;
    }
    
    private IEnumerator ReturnToIdleAfterDamage()
    {
        // Wait for the damage animation to complete
        // Damage animations are typically shorter
        yield return new WaitForSeconds(0.8f);
        
        if (isPlayingDamageAnimation)
        {
            Debug.Log($"NetworkEntityAnimator: Damage animation completed, returning to idle for {networkEntity?.EntityName.Value}");
            isPlayingDamageAnimation = false;
            PlayIdleAnimation();
        }
        
        damageReturnToIdleCoroutine = null;
    }

    #region Debug Methods

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    [ContextMenu("Test Attack Animation")]
    private void TestAttackAnimation()
    {
        if (Application.isPlaying)
        {
            PlayAttackAnimation();
        }
    }

    [ContextMenu("Test Take Damage Animation")]
    private void TestTakeDamageAnimation()
    {
        if (Application.isPlaying)
        {
            PlayTakeDamageAnimation();
        }
    }

    [ContextMenu("Test Idle Animation")]
    private void TestIdleAnimation()
    {
        if (Application.isPlaying)
        {
            PlayIdleAnimation();
        }
    }

    [ContextMenu("Diagnose Animator Setup")]
    public void DiagnoseAnimatorSetup()
    {
        Debug.Log($"=== Animator Diagnosis for {networkEntity?.EntityName.Value} ===");
        
        if (modelAnimator == null)
        {
            Debug.LogError("ModelAnimator is null - please assign the Animator component from your 3D model");
            return;
        }
        
        if (modelAnimator.runtimeAnimatorController == null)
        {
            Debug.LogError("RuntimeAnimatorController is null - please assign an Animator Controller");
            return;
        }
        
        Debug.Log($"Animator enabled: {modelAnimator.enabled}");
        Debug.Log($"GameObject active: {modelAnimator.gameObject.activeInHierarchy}");
        Debug.Log($"Controller: {modelAnimator.runtimeAnimatorController.name}");
        Debug.Log($"Layer count: {modelAnimator.layerCount}");
        
        // Check parameters
        Debug.Log($"Parameters ({modelAnimator.parameters.Length}):");
        foreach (var param in modelAnimator.parameters)
        {
            Debug.Log($"  - {param.name} ({param.type})");
        }
        
        // Check specific triggers
        bool hasIdle = HasAnimatorParameter(idleAnimationTrigger, AnimatorControllerParameterType.Trigger);
        bool hasAttack = HasAnimatorParameter(attackAnimationTrigger, AnimatorControllerParameterType.Trigger);
        bool hasTakeDamage = HasAnimatorParameter(takeDamageAnimationTrigger, AnimatorControllerParameterType.Trigger);
        bool hasDefeated = HasAnimatorParameter(defeatedAnimationTrigger, AnimatorControllerParameterType.Trigger);
        
        Debug.Log($"Required Triggers:");
        Debug.Log($"  - {idleAnimationTrigger}: {hasIdle}");
        Debug.Log($"  - {attackAnimationTrigger}: {hasAttack}");
        Debug.Log($"  - {takeDamageAnimationTrigger}: {hasTakeDamage}");
        Debug.Log($"  - {defeatedAnimationTrigger}: {hasDefeated}");
        
        // Current state info
        LogAnimatorState("Diagnosis");
        
        Debug.Log($"=== SETUP INSTRUCTIONS ===");
        Debug.Log($"1. Add FishNet's NetworkAnimator component to your 3D model GameObject");
        Debug.Log($"2. Ensure your Animator Controller has proper transitions from Idle to Attack/TakeDamage states");
        Debug.Log($"3. Verify transition conditions use the correct trigger names");
        Debug.Log($"4. Test animations using the context menu options above");
        Debug.Log($"========================");
    }

    #endregion
} 