using UnityEngine;
using System.Collections;
using System;
using DG.Tweening;

/// <summary>
/// Handles all animation logic for the fight conclusion system.
/// Separates animation concerns from UI management following Single Responsibility Principle.
/// Attach to: Same GameObject as FightConclusionUIManager or reference from it.
/// </summary>
public class FightConclusionAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float fadeInDuration = 0.8f;
    [SerializeField] private float fadeOutDuration = 0.8f;
    [SerializeField] private float displayDuration = 4f;
    
    [Header("DOTween Easing")]
    [SerializeField] private Ease fadeInEase = Ease.OutBack;
    [SerializeField] private Ease fadeOutEase = Ease.InBack;
    [SerializeField] private Ease scaleEase = Ease.OutBack;
    
    [Header("Optional Effects")]
    [SerializeField] private bool useScaleAnimation = true;
    [SerializeField] private Vector3 startScale = Vector3.one * 0.9f;
    [SerializeField] private Vector3 endScale = Vector3.one;
    [SerializeField] private bool useShakeEffect = true;
    [SerializeField] private Vector3 shakeStrength = Vector3.one * 20f;
    [SerializeField] private float shakeDuration = 0.5f;
    [SerializeField] private int shakeVibrato = 20;

    // Events for animation lifecycle
    public event Action OnAnimationStarted;
    public event Action OnDisplayPhaseStarted;
    public event Action OnAnimationCompleted;

    // Current animation state
    private bool isAnimating = false;
    private Sequence currentAnimationSequence;

    public bool IsAnimating => isAnimating;
    public float TotalDuration => fadeInDuration + displayDuration + fadeOutDuration;

    #region Public API

    /// <summary>
    /// Starts the complete conclusion animation sequence
    /// </summary>
    /// <param name="canvasGroup">The CanvasGroup to animate</param>
    /// <param name="targetTransform">Optional transform for scale animation</param>
    /// <returns>DOTween Sequence reference for external control</returns>
    public Sequence StartConclusionAnimation(CanvasGroup canvasGroup, Transform targetTransform = null)
    {
        if (canvasGroup == null)
        {
            Debug.LogError("FightConclusionAnimator: CanvasGroup is null, cannot start animation");
            return null;
        }

        if (isAnimating)
        {
            Debug.LogWarning("FightConclusionAnimator: Animation already in progress, stopping current animation");
            StopCurrentAnimation();
        }

        currentAnimationSequence = CreateConclusionAnimationSequence(canvasGroup, targetTransform);
        currentAnimationSequence.Play();
        return currentAnimationSequence;
    }

    /// <summary>
    /// Stops the current animation and resets state
    /// </summary>
    public void StopCurrentAnimation()
    {
        if (currentAnimationSequence != null && currentAnimationSequence.IsActive())
        {
            currentAnimationSequence.Kill();
            currentAnimationSequence = null;
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

    #region DOTween Animation Methods

    /// <summary>
    /// Creates the complete DOTween animation sequence for fight conclusion
    /// </summary>
    private Sequence CreateConclusionAnimationSequence(CanvasGroup canvasGroup, Transform targetTransform)
    {
        // Set initial state
        canvasGroup.alpha = 0f;
        if (useScaleAnimation && targetTransform != null)
        {
            targetTransform.localScale = startScale;
        }

        // Create the sequence
        Sequence sequence = DOTween.Sequence();

        // Animation start callback
        sequence.OnStart(() => {
            isAnimating = true;
            OnAnimationStarted?.Invoke();
        });

        // Phase 1: Dramatic Fade In with scale
        sequence.Append(canvasGroup.DOFade(1f, fadeInDuration).SetEase(fadeInEase));

        // Add scale animation simultaneously if enabled
        if (useScaleAnimation && targetTransform != null)
        {
            sequence.Join(targetTransform.DOScale(endScale, fadeInDuration).SetEase(scaleEase));
        }

        // Add shake effect for dramatic impact if enabled
        if (useShakeEffect && targetTransform != null)
        {
            sequence.Append(targetTransform.DOShakePosition(shakeDuration, shakeStrength, shakeVibrato, 90f, false, true));
        }

        // Phase 2: Display phase callback
        sequence.AppendCallback(() => OnDisplayPhaseStarted?.Invoke());

        // Phase 3: Extended display duration for conclusion
        sequence.AppendInterval(displayDuration);

        // Phase 4: Fade Out
        sequence.Append(canvasGroup.DOFade(0f, fadeOutDuration).SetEase(fadeOutEase));

        // Add scale out animation simultaneously if enabled
        if (useScaleAnimation && targetTransform != null)
        {
            sequence.Join(targetTransform.DOScale(startScale, fadeOutDuration).SetEase(scaleEase));
        }

        // Animation complete callback
        sequence.OnComplete(() => {
            isAnimating = false;
            currentAnimationSequence = null;
            OnAnimationCompleted?.Invoke();
        });

        return sequence;
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
            Debug.LogWarning("FightConclusionAnimator: Cannot change timing while animation is in progress");
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
    /// Sets custom DOTween easing curves
    /// </summary>
    public void SetAnimationEasing(Ease newFadeInEase, Ease newFadeOutEase, Ease newScaleEase)
    {
        fadeInEase = newFadeInEase;
        fadeOutEase = newFadeOutEase;
        scaleEase = newScaleEase;
    }

    /// <summary>
    /// Sets shake effect parameters
    /// </summary>
    public void SetShakeEffect(bool enabled, Vector3 strength, float duration, int vibrato)
    {
        useShakeEffect = enabled;
        shakeStrength = strength;
        shakeDuration = duration;
        shakeVibrato = vibrato;
    }

    /// <summary>
    /// Sets custom scale animation values
    /// </summary>
    public void SetScaleValues(Vector3 newStartScale, Vector3 newEndScale)
    {
        startScale = newStartScale;
        endScale = newEndScale;
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
            Debug.LogError("FightConclusionAnimator: Fade in duration cannot be negative");
            isValid = false;
        }
        
        if (fadeOutDuration < 0f)
        {
            Debug.LogError("FightConclusionAnimator: Fade out duration cannot be negative");
            isValid = false;
        }
        
        if (displayDuration < 0f)
        {
            Debug.LogError("FightConclusionAnimator: Display duration cannot be negative");
            isValid = false;
        }
        
        if (shakeDuration < 0f)
        {
            Debug.LogError("FightConclusionAnimator: Shake duration cannot be negative");
            isValid = false;
        }
        
        if (shakeVibrato < 1)
        {
            Debug.LogError("FightConclusionAnimator: Shake vibrato must be at least 1");
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