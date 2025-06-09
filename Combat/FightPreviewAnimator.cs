using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// Handles all animation logic for the fight preview system.
/// Separates animation concerns from UI management following Single Responsibility Principle.
/// Attach to: Same GameObject as FightPreviewUIManager or reference from it.
/// </summary>
public class FightPreviewAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float displayDuration = 3f;
    
    [Header("Animation Curves")]
    [SerializeField] private AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    
    [Header("Optional Effects")]
    [SerializeField] private bool useScaleAnimation = false;
    [SerializeField] private Vector3 startScale = Vector3.one * 0.8f;
    [SerializeField] private Vector3 endScale = Vector3.one;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // Events for animation lifecycle
    public event Action OnAnimationStarted;
    public event Action OnDisplayPhaseStarted;
    public event Action OnAnimationCompleted;

    // Current animation state
    private bool isAnimating = false;
    private Coroutine currentAnimationCoroutine;

    public bool IsAnimating => isAnimating;
    public float TotalDuration => fadeInDuration + displayDuration + fadeOutDuration;

    #region Public API

    /// <summary>
    /// Starts the complete preview animation sequence
    /// </summary>
    /// <param name="canvasGroup">The CanvasGroup to animate</param>
    /// <param name="targetTransform">Optional transform for scale animation</param>
    /// <returns>Coroutine reference for external control</returns>
    public Coroutine StartPreviewAnimation(CanvasGroup canvasGroup, Transform targetTransform = null)
    {
        if (canvasGroup == null)
        {
            Debug.LogError("FightPreviewAnimator: CanvasGroup is null, cannot start animation");
            return null;
        }

        if (isAnimating)
        {
            Debug.LogWarning("FightPreviewAnimator: Animation already in progress, stopping current animation");
            StopCurrentAnimation();
        }

        currentAnimationCoroutine = StartCoroutine(PreviewAnimationSequence(canvasGroup, targetTransform));
        return currentAnimationCoroutine;
    }

    /// <summary>
    /// Stops the current animation and resets state
    /// </summary>
    public void StopCurrentAnimation()
    {
        if (currentAnimationCoroutine != null)
        {
            StopCoroutine(currentAnimationCoroutine);
            currentAnimationCoroutine = null;
        }
        
        isAnimating = false;
    }

    /// <summary>
    /// Immediately shows the canvas (skips fade in)
    /// </summary>
    public void ShowImmediately(CanvasGroup canvasGroup, Transform targetTransform = null)
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = 1f;
        
        if (useScaleAnimation && targetTransform != null)
        {
            targetTransform.localScale = endScale;
        }
    }

    /// <summary>
    /// Immediately hides the canvas (skips fade out)
    /// </summary>
    public void HideImmediately(CanvasGroup canvasGroup, Transform targetTransform = null)
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = 0f;
        
        if (useScaleAnimation && targetTransform != null)
        {
            targetTransform.localScale = startScale;
        }
    }

    #endregion

    #region Animation Coroutines

    /// <summary>
    /// Main animation sequence coroutine
    /// </summary>
    private IEnumerator PreviewAnimationSequence(CanvasGroup canvasGroup, Transform targetTransform)
    {
        isAnimating = true;
        OnAnimationStarted?.Invoke();

        // Ensure we start from the correct state
        canvasGroup.alpha = 0f;
        if (useScaleAnimation && targetTransform != null)
        {
            targetTransform.localScale = startScale;
        }

        // Phase 1: Fade In
        yield return StartCoroutine(FadeInAnimation(canvasGroup, targetTransform));

        // Phase 2: Display
        OnDisplayPhaseStarted?.Invoke();
        yield return new WaitForSeconds(displayDuration);

        // Phase 3: Fade Out
        yield return StartCoroutine(FadeOutAnimation(canvasGroup, targetTransform));

        // Animation complete
        isAnimating = false;
        currentAnimationCoroutine = null;
        OnAnimationCompleted?.Invoke();
    }

    /// <summary>
    /// Handles the fade-in animation
    /// </summary>
    private IEnumerator FadeInAnimation(CanvasGroup canvasGroup, Transform targetTransform)
    {
        yield return StartCoroutine(AnimateCanvasGroup(
            canvasGroup, 
            targetTransform,
            0f, 1f, 
            startScale, endScale,
            fadeInDuration, 
            fadeInCurve, 
            scaleCurve
        ));
    }

    /// <summary>
    /// Handles the fade-out animation
    /// </summary>
    private IEnumerator FadeOutAnimation(CanvasGroup canvasGroup, Transform targetTransform)
    {
        yield return StartCoroutine(AnimateCanvasGroup(
            canvasGroup, 
            targetTransform,
            1f, 0f, 
            endScale, startScale,
            fadeOutDuration, 
            fadeOutCurve, 
            scaleCurve
        ));
    }

    /// <summary>
    /// Generic method to animate CanvasGroup alpha and optional scale
    /// </summary>
    private IEnumerator AnimateCanvasGroup(
        CanvasGroup canvasGroup, 
        Transform targetTransform,
        float startAlpha, float endAlpha,
        Vector3 startScaleValue, Vector3 endScaleValue,
        float duration, 
        AnimationCurve alphaCurve, 
        AnimationCurve scaleAnimationCurve)
    {
        if (duration <= 0f)
        {
            canvasGroup.alpha = endAlpha;
            if (useScaleAnimation && targetTransform != null)
            {
                targetTransform.localScale = endScaleValue;
            }
            yield break;
        }

        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / duration;
            
            // Apply alpha animation
            float curveValue = alphaCurve.Evaluate(normalizedTime);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, curveValue);
            
            // Apply scale animation if enabled
            if (useScaleAnimation && targetTransform != null)
            {
                float scaleCurveValue = scaleAnimationCurve.Evaluate(normalizedTime);
                targetTransform.localScale = Vector3.Lerp(startScaleValue, endScaleValue, scaleCurveValue);
            }
            
            yield return null;
        }

        // Ensure final values are set
        canvasGroup.alpha = endAlpha;
        if (useScaleAnimation && targetTransform != null)
        {
            targetTransform.localScale = endScaleValue;
        }
    }

    #endregion

    #region Configuration Methods

    /// <summary>
    /// Updates animation timing settings
    /// </summary>
    public void SetAnimationTiming(float newFadeInDuration, float newDisplayDuration, float newFadeOutDuration)
    {
        if (isAnimating)
        {
            Debug.LogWarning("FightPreviewAnimator: Cannot change timing while animation is in progress");
            return;
        }

        fadeInDuration = Mathf.Max(0f, newFadeInDuration);
        displayDuration = Mathf.Max(0f, newDisplayDuration);
        fadeOutDuration = Mathf.Max(0f, newFadeOutDuration);
    }

    /// <summary>
    /// Enables or disables scale animation
    /// </summary>
    public void SetScaleAnimationEnabled(bool enabled)
    {
        useScaleAnimation = enabled;
    }

    /// <summary>
    /// Sets custom animation curves
    /// </summary>
    public void SetAnimationCurves(AnimationCurve newFadeInCurve, AnimationCurve newFadeOutCurve, AnimationCurve newScaleCurve = null)
    {
        if (newFadeInCurve != null) fadeInCurve = newFadeInCurve;
        if (newFadeOutCurve != null) fadeOutCurve = newFadeOutCurve;
        if (newScaleCurve != null) scaleCurve = newScaleCurve;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Gets the current animation progress (0-1)
    /// </summary>
    public float GetAnimationProgress()
    {
        if (!isAnimating) return 0f;
        
        // This is a simplified progress calculation
        // Could be enhanced to track exact phase and progress
        return 0.5f; // Placeholder - would need more complex tracking for exact progress
    }

    /// <summary>
    /// Validates that the animator is properly configured
    /// </summary>
    public bool ValidateConfiguration()
    {
        bool isValid = true;
        
        if (fadeInDuration < 0f)
        {
            Debug.LogError("FightPreviewAnimator: Fade in duration cannot be negative");
            isValid = false;
        }
        
        if (fadeOutDuration < 0f)
        {
            Debug.LogError("FightPreviewAnimator: Fade out duration cannot be negative");
            isValid = false;
        }
        
        if (displayDuration < 0f)
        {
            Debug.LogError("FightPreviewAnimator: Display duration cannot be negative");
            isValid = false;
        }
        
        if (fadeInCurve == null || fadeOutCurve == null)
        {
            Debug.LogError("FightPreviewAnimator: Animation curves cannot be null");
            isValid = false;
        }
        
        return isValid;
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ValidateConfiguration();
    }

    private void OnDestroy()
    {
        StopCurrentAnimation();
    }

    #endregion
} 