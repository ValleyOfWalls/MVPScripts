using UnityEngine;
using System.Collections;
using System;
using DG.Tweening;

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
    
    [Header("DOTween Easing")]
    [SerializeField] private Ease fadeInEase = Ease.OutCubic;
    [SerializeField] private Ease fadeOutEase = Ease.InCubic;
    [SerializeField] private Ease scaleEase = Ease.OutBack;
    
    [Header("Optional Effects")]
    [SerializeField] private bool useScaleAnimation = false;
    [SerializeField] private Vector3 startScale = Vector3.one * 0.8f;
    [SerializeField] private Vector3 endScale = Vector3.one;
    [SerializeField] private bool usePunchEffect = true;
    [SerializeField] private Vector3 punchStrength = Vector3.one * 0.1f;
    [SerializeField] private float punchDuration = 0.3f;

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
    /// Starts the complete preview animation sequence
    /// </summary>
    /// <param name="canvasGroup">The CanvasGroup to animate</param>
    /// <param name="targetTransform">Optional transform for scale animation</param>
    /// <returns>DOTween Sequence reference for external control</returns>
    public Sequence StartPreviewAnimation(CanvasGroup canvasGroup, Transform targetTransform = null)
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

        currentAnimationSequence = CreatePreviewAnimationSequence(canvasGroup, targetTransform);
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
    /// Creates the complete DOTween animation sequence
    /// </summary>
    private Sequence CreatePreviewAnimationSequence(CanvasGroup canvasGroup, Transform targetTransform)
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

        // Phase 1: Fade In
        sequence.Append(canvasGroup.DOFade(1f, fadeInDuration).SetEase(fadeInEase));

        // Add scale animation simultaneously if enabled
        if (useScaleAnimation && targetTransform != null)
        {
            sequence.Join(targetTransform.DOScale(endScale, fadeInDuration).SetEase(scaleEase));
        }

        // Add punch effect if enabled and we have a transform
        if (usePunchEffect && targetTransform != null)
        {
            sequence.Append(targetTransform.DOPunchScale(punchStrength, punchDuration, 10, 1f));
        }

        // Phase 2: Display phase callback
        sequence.AppendCallback(() => OnDisplayPhaseStarted?.Invoke());

        // Phase 3: Display duration
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
    /// Sets custom DOTween easing curves
    /// </summary>
    public void SetAnimationEasing(Ease newFadeInEase, Ease newFadeOutEase, Ease newScaleEase)
    {
        fadeInEase = newFadeInEase;
        fadeOutEase = newFadeOutEase;
        scaleEase = newScaleEase;
    }

    /// <summary>
    /// Sets punch effect parameters
    /// </summary>
    public void SetPunchEffect(bool enabled, Vector3 strength, float duration)
    {
        usePunchEffect = enabled;
        punchStrength = strength;
        punchDuration = duration;
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
        
        if (punchDuration < 0f)
        {
            Debug.LogError("FightPreviewAnimator: Punch duration cannot be negative");
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