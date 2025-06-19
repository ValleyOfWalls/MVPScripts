using UnityEngine;
using DG.Tweening;
using System.Collections;

/// <summary>
/// Handles all animations for individual cards including draw, play, failed play, and hover animations.
/// Attach to: Card prefabs alongside other card components.
/// Now controlled by UIHoverDetector for hover events.
/// </summary>
public class CardAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float drawAnimationDuration = 0.6f;
    [SerializeField] private float playAnimationDuration = 0.4f;
    [SerializeField] private float failedPlayAnimationDuration = 0.8f;
    [SerializeField] private float dissolveAnimationDuration = 0.3f;
    
    [Header("Draw Animation")]
    [SerializeField] private Vector3 drawStartOffset = new Vector3(0, -200f, 0);
    [SerializeField] private Ease drawEase = Ease.OutBack;
    [SerializeField] private float drawScaleOvershoot = 1.1f;
    
    [Header("Play Animation")]
    [SerializeField] private Vector3 playTargetOffset = new Vector3(0, 100f, 0);
    [SerializeField] private Ease playEase = Ease.InBack;
    [SerializeField] private float playFadeOutAlpha = 0f;
    
    [Header("Failed Play Animation")]
    [SerializeField] private float dissolveOutDuration = 0.25f;
    [SerializeField] private float dissolveInDuration = 0.35f;
    [SerializeField] private float dissolveInDelay = 0.1f;
    [SerializeField] private Ease dissolveEase = Ease.InOutQuad;
    
    [Header("Hover Animation")]
    [SerializeField] private float hoverScaleMultiplier = 1.15f;
    [SerializeField] private float hoverAnimationDuration = 0.2f;
    [SerializeField] private Ease hoverEase = Ease.OutCubic;
    [SerializeField] private bool enableHoverEffects = true;
    
    [Header("Hand Type Modifiers")]
    [SerializeField] private bool isPlayerHand = true;
    [SerializeField] private float petHandSpeedMultiplier = 0.7f;
    [SerializeField] private float petHandAlphaMultiplier = 0.8f;
    
    [Header("Debug")]
    [SerializeField] private bool debugLogEnabled = true; // Temporarily enabled for debugging
    
    // Components
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Card card;
    private HandAnimator handAnimator;
    
    // Animation state
    private bool isAnimating = false;
    private bool isHovered = false;
    private Vector3 originalPosition;
    private Vector3 originalScale;
    private float originalAlpha;
    private Sequence currentSequence;
    private Tween hoverTween;
    
    // Failed play state
    private Vector3 dropPosition;
    private bool shouldReturnToHand = false;
    
    public bool IsAnimating => isAnimating;
    public bool IsHovered => isHovered;
    
    private void Awake()
    {
        // Get components
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        card = GetComponent<Card>();
        
        // Find HandAnimator in parent hierarchy
        handAnimator = GetComponentInParent<HandAnimator>();
        
        // Determine if this is a player or pet hand
        DetermineHandType();
        
        ValidateComponents();
    }
    
    private void Start()
    {
        // Store original state once the object is fully initialized
        StoreOriginalState();
        LogDebug($"Initial state stored in Start for {gameObject.name} - pos: {originalPosition}, scale: {originalScale}, alpha: {originalAlpha}");
    }
    
    private void DetermineHandType()
    {
        // Try to determine hand type from parent hierarchy
        Transform current = transform.parent;
        while (current != null)
        {
            if (current.name.ToLower().Contains("player"))
            {
                isPlayerHand = true;
                break;
            }
            else if (current.name.ToLower().Contains("pet") || current.name.ToLower().Contains("opponent"))
            {
                isPlayerHand = false;
                break;
            }
            current = current.parent;
        }
        
        LogDebug($"Determined hand type: {(isPlayerHand ? "Player" : "Pet")} for card {gameObject.name}");
    }
    
    private void ValidateComponents()
    {
        if (rectTransform == null)
            Debug.LogError($"CardAnimator on {gameObject.name}: Missing RectTransform component!");
        
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            LogDebug($"Added missing CanvasGroup to {gameObject.name}");
        }
        
        if (handAnimator == null)
            LogDebug($"No HandAnimator found in parent hierarchy for {gameObject.name}");
    }
    
    #region Public Animation Methods
    
    /// <summary>
    /// Animates card drawing into hand from below screen
    /// </summary>
    public void AnimateDrawToHand(Vector3 targetPosition, System.Action onComplete = null)
    {
        if (isAnimating)
        {
            LogDebug($"Cannot animate draw - already animating: {gameObject.name}");
            return;
        }
        
        LogDebug($"Starting draw animation for {gameObject.name} to position {targetPosition}");
        
        StoreOriginalState();
        
        // Clear any hover state before major animation
        if (isHovered)
        {
            LogDebug($"Clearing hover state before draw animation for {gameObject.name}");
            isHovered = false;
            if (hoverTween != null)
            {
                hoverTween.Kill();
                hoverTween = null;
            }
        }
        
        isAnimating = true;
        
        // Set starting position (below screen)
        Vector3 startPosition = targetPosition + drawStartOffset;
        rectTransform.localPosition = startPosition;
        rectTransform.localScale = Vector3.zero;
        canvasGroup.alpha = 0f;
        
        // Create animation sequence
        currentSequence = DOTween.Sequence();
        
        float duration = GetAdjustedDuration(drawAnimationDuration);
        
        // Animate position, scale, and alpha simultaneously
        currentSequence.Append(rectTransform.DOLocalMove(targetPosition, duration).SetEase(drawEase));
        currentSequence.Join(rectTransform.DOScale(drawScaleOvershoot, duration * 0.6f).SetEase(Ease.OutBack));
        currentSequence.Join(canvasGroup.DOFade(GetAdjustedAlpha(1f), duration * 0.4f));
        
        // Scale back to normal size
        currentSequence.Append(rectTransform.DOScale(originalScale, duration * 0.4f).SetEase(Ease.InOutQuad));
        
        // Complete callback
        currentSequence.OnComplete(() => {
            isAnimating = false;
            
            // Notify HandAnimator that this card is done animating
            if (handAnimator != null)
            {
                handAnimator.RemoveAnimatingCard(this);
            }
            
            onComplete?.Invoke();
            LogDebug($"Draw animation completed for {gameObject.name}");
        });
    }
    
    /// <summary>
    /// Animates card playing successfully
    /// </summary>
    public void AnimatePlaySuccess(Vector3? targetPosition = null, System.Action onComplete = null)
    {
        if (isAnimating)
        {
            LogDebug($"Cannot animate play success - already animating: {gameObject.name}");
            return;
        }
        
        LogDebug($"Starting play success animation for {gameObject.name}");
        
        // Clear any hover state before major animation
        if (isHovered)
        {
            LogDebug($"Clearing hover state before play success animation for {gameObject.name}");
            isHovered = false;
            if (hoverTween != null)
            {
                hoverTween.Kill();
                hoverTween = null;
            }
        }
        
        isAnimating = true;
        Vector3 target = targetPosition ?? (rectTransform.localPosition + playTargetOffset);
        
        currentSequence = DOTween.Sequence();
        float duration = GetAdjustedDuration(playAnimationDuration);
        
        // Animate up and fade out
        currentSequence.Append(rectTransform.DOLocalMove(target, duration).SetEase(playEase));
        currentSequence.Join(rectTransform.DOScale(originalScale * 1.2f, duration));
        currentSequence.Join(canvasGroup.DOFade(playFadeOutAlpha, duration));
        
        currentSequence.OnComplete(() => {
            isAnimating = false;
            
            // Notify HandAnimator that this card is done animating
            if (handAnimator != null)
            {
                handAnimator.RemoveAnimatingCard(this);
            }
            
            onComplete?.Invoke();
            LogDebug($"Play success animation completed for {gameObject.name}");
        });
    }
    
    /// <summary>
    /// Animates failed play: dissolve out at drop position, dissolve back in at hand
    /// </summary>
    public void AnimatePlayFailed(Vector3 failPosition, Vector3 handPosition, System.Action onComplete = null)
    {
        if (isAnimating)
        {
            LogDebug($"Cannot animate play failed - already animating: {gameObject.name}");
            return;
        }
        
        LogDebug($"Starting play failed animation for {gameObject.name} from {failPosition} to {handPosition}");
        
        // Ensure we have current state stored
        StoreOriginalState();
        
        // Clear any hover state before major animation
        if (isHovered)
        {
            LogDebug($"Clearing hover state before play failed animation for {gameObject.name}");
            isHovered = false;
            if (hoverTween != null)
            {
                hoverTween.Kill();
                hoverTween = null;
            }
        }
        
        isAnimating = true;
        dropPosition = failPosition;
        shouldReturnToHand = true;
        
        // Store current alpha and ensure canvas group is available
        float currentAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        
        LogDebug($"Stored state - originalAlpha: {originalAlpha}, currentAlpha: {currentAlpha}, originalScale: {originalScale}");
        
        currentSequence = DOTween.Sequence();
        
        // Phase 1: Dissolve out at drop position
        float dissolveOutDur = GetAdjustedDuration(dissolveOutDuration);
        currentSequence.Append(canvasGroup.DOFade(0f, dissolveOutDur).SetEase(dissolveEase));
        currentSequence.Join(rectTransform.DOScale(originalScale * 0.8f, dissolveOutDur).SetEase(dissolveEase));
        
        // Phase 2: Instantly move to hand position (invisible)
        currentSequence.AppendCallback(() => {
            rectTransform.localPosition = handPosition;
            rectTransform.localScale = originalScale * 0.8f;
            LogDebug($"Moved to hand position: {handPosition}, scale: {originalScale * 0.8f}");
        });
        
        // Phase 3: Dissolve in at hand position
        currentSequence.AppendInterval(dissolveInDelay);
        float dissolveInDur = GetAdjustedDuration(dissolveInDuration);
        float targetAlpha = GetAdjustedAlpha(Mathf.Max(originalAlpha, 1f)); // Ensure we fade to at least 1.0
        
        LogDebug($"Starting dissolve in - duration: {dissolveInDur}, targetAlpha: {targetAlpha}, targetScale: {originalScale}");
        
        currentSequence.Append(canvasGroup.DOFade(targetAlpha, dissolveInDur).SetEase(dissolveEase));
        currentSequence.Join(rectTransform.DOScale(originalScale, dissolveInDur).SetEase(dissolveEase));
        
        currentSequence.OnComplete(() => {
            isAnimating = false;
            shouldReturnToHand = false;
            
            // Ensure final state is correct
            if (canvasGroup != null)
            {
                canvasGroup.alpha = targetAlpha;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            
            if (rectTransform != null)
            {
                rectTransform.localPosition = handPosition;
                rectTransform.localScale = originalScale;
            }
            
            LogDebug($"Final state set - alpha: {canvasGroup?.alpha}, position: {rectTransform?.localPosition}, scale: {rectTransform?.localScale}");
            
            // Notify HandAnimator that this card is done animating
            if (handAnimator != null)
            {
                handAnimator.RemoveAnimatingCard(this);
            }
            
            onComplete?.Invoke();
            LogDebug($"Play failed animation completed for {gameObject.name}");
        });
    }
    
    /// <summary>
    /// Animates failed play with complete layout data: dissolve out at drop position, dissolve back in with proper layout
    /// </summary>
    public void AnimatePlayFailedComplete(Vector3 failPosition, Vector3 handPosition, Vector3 handScale, Quaternion handRotation, System.Action onComplete = null)
    {
        if (isAnimating)
        {
            LogDebug($"Cannot animate complete play failed - already animating: {gameObject.name}");
            return;
        }
        
        LogDebug($"Starting complete play failed animation for {gameObject.name} from {failPosition} to pos: {handPosition}, scale: {handScale}, rot: {handRotation.eulerAngles}");
        
        // Ensure we have current state stored
        StoreOriginalState();
        
        isAnimating = true;
        dropPosition = failPosition;
        shouldReturnToHand = true;
        
        // Store current alpha and ensure canvas group is available
        float currentAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        
        LogDebug($"Stored state - originalAlpha: {originalAlpha}, currentAlpha: {currentAlpha}, originalScale: {originalScale}");
        
        currentSequence = DOTween.Sequence();
        
        // Phase 1: Dissolve out at drop position
        float dissolveOutDur = GetAdjustedDuration(dissolveOutDuration);
        currentSequence.Append(canvasGroup.DOFade(0f, dissolveOutDur).SetEase(dissolveEase));
        currentSequence.Join(rectTransform.DOScale(originalScale * 0.8f, dissolveOutDur).SetEase(dissolveEase));
        
        // Phase 2: Instantly set complete layout (invisible) and trigger other cards to reposition
        currentSequence.AppendCallback(() => {
            rectTransform.localPosition = handPosition;
            rectTransform.localScale = handScale * 0.8f; // Start slightly smaller
            rectTransform.localRotation = handRotation;
            LogDebug($"Set complete layout - pos: {handPosition}, scale: {handScale * 0.8f}, rot: {handRotation.eulerAngles}");
            
            // Trigger layout update immediately when card is back in hand position
            // This allows other cards to start repositioning while this card dissolves in
            Transform handTransform = transform.parent;
            if (handTransform != null)
            {
                var handLayoutManager = handTransform.GetComponent<HandLayoutManager>();
                if (handLayoutManager != null)
                {
                    LogDebug($"Triggering early layout update for other cards while {gameObject.name} dissolves in");
                    handLayoutManager.UpdateLayout();
                }
            }
        });
        
        // Phase 3: Dissolve in with complete layout
        currentSequence.AppendInterval(dissolveInDelay);
        float dissolveInDur = GetAdjustedDuration(dissolveInDuration);
        float targetAlpha = GetAdjustedAlpha(Mathf.Max(originalAlpha, 1f)); // Ensure we fade to at least 1.0
        
        LogDebug($"Starting dissolve in - duration: {dissolveInDur}, targetAlpha: {targetAlpha}, targetScale: {handScale}");
        
        currentSequence.Append(canvasGroup.DOFade(targetAlpha, dissolveInDur).SetEase(dissolveEase));
        currentSequence.Join(rectTransform.DOScale(handScale, dissolveInDur).SetEase(dissolveEase));
        
        currentSequence.OnComplete(() => {
            isAnimating = false;
            shouldReturnToHand = false;
            
            // Ensure final state is correct with complete layout
            if (canvasGroup != null)
            {
                canvasGroup.alpha = targetAlpha;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            
            if (rectTransform != null)
            {
                rectTransform.localPosition = handPosition;
                rectTransform.localScale = handScale;
                rectTransform.localRotation = handRotation;
            }
            
            LogDebug($"Final complete state set - alpha: {canvasGroup?.alpha}, position: {rectTransform?.localPosition}, scale: {rectTransform?.localScale}, rotation: {rectTransform?.localRotation.eulerAngles}");
            
            // Notify HandAnimator that this card is done animating
            if (handAnimator != null)
            {
                handAnimator.RemoveAnimatingCard(this);
            }
            
            onComplete?.Invoke();
            LogDebug($"Complete play failed animation finished for {gameObject.name}");
        });
    }
    
    /// <summary>
    /// Simple dissolve out animation
    /// </summary>
    public void AnimateDissolveOut(System.Action onComplete = null)
    {
        if (isAnimating)
        {
            LogDebug($"Cannot animate dissolve out - already animating: {gameObject.name}");
            return;
        }
        
        LogDebug($"Starting dissolve out animation for {gameObject.name}");
        
        isAnimating = true;
        
        currentSequence = DOTween.Sequence();
        float duration = GetAdjustedDuration(dissolveAnimationDuration);
        
        currentSequence.Append(canvasGroup.DOFade(0f, duration).SetEase(dissolveEase));
        currentSequence.Join(rectTransform.DOScale(originalScale * 0.8f, duration).SetEase(dissolveEase));
        
        currentSequence.OnComplete(() => {
            isAnimating = false;
            onComplete?.Invoke();
            LogDebug($"Dissolve out animation completed for {gameObject.name}");
        });
    }
    
    /// <summary>
    /// Simple dissolve in animation
    /// </summary>
    public void AnimateDissolveIn(System.Action onComplete = null)
    {
        if (isAnimating)
        {
            LogDebug($"Cannot animate dissolve in - already animating: {gameObject.name}");
            return;
        }
        
        LogDebug($"Starting dissolve in animation for {gameObject.name}");
        
        isAnimating = true;
        
        // Set starting state
        canvasGroup.alpha = 0f;
        rectTransform.localScale = originalScale * 0.8f;
        
        currentSequence = DOTween.Sequence();
        float duration = GetAdjustedDuration(dissolveAnimationDuration);
        
        currentSequence.Append(canvasGroup.DOFade(GetAdjustedAlpha(originalAlpha), duration).SetEase(dissolveEase));
        currentSequence.Join(rectTransform.DOScale(originalScale, duration).SetEase(dissolveEase));
        
        currentSequence.OnComplete(() => {
            isAnimating = false;
            onComplete?.Invoke();
            LogDebug($"Dissolve in animation completed for {gameObject.name}");
        });
    }
    
    #endregion
    
    #region Animation Utilities
    
    /// <summary>
    /// Stops any current animation and resets to original state
    /// </summary>
    public void StopAnimation(bool resetToOriginal = true)
    {
        if (currentSequence != null)
        {
            currentSequence.Kill();
            currentSequence = null;
        }
        
        // Also stop hover animations
        if (hoverTween != null)
        {
            hoverTween.Kill();
            hoverTween = null;
        }
        
        isAnimating = false;
        isHovered = false;
        
        if (resetToOriginal)
        {
            RestoreOriginalState();
        }
        
        LogDebug($"Animation stopped for {gameObject.name} - major and hover animations cleared");
    }
    
    /// <summary>
    /// Stores the original state of the card for restoration
    /// </summary>
    public void StoreOriginalState()
    {
        if (rectTransform != null)
        {
            originalPosition = rectTransform.localPosition;
            originalScale = rectTransform.localScale;
        }
        
        if (canvasGroup != null)
        {
            originalAlpha = canvasGroup.alpha;
        }
        else
        {
            // Fallback to 1.0 if no canvas group
            originalAlpha = 1f;
        }
        
        // Ensure we have valid values
        if (originalAlpha <= 0.1f)
        {
            originalAlpha = 1f;
            LogDebug($"Corrected originalAlpha from near-zero to 1.0 for {gameObject.name}");
        }
        
        LogDebug($"Stored original state for {gameObject.name}: pos={originalPosition}, scale={originalScale}, alpha={originalAlpha}");
    }
    
    /// <summary>
    /// Restores the card to its original state
    /// </summary>
    public void RestoreOriginalState()
    {
        if (rectTransform != null)
        {
            rectTransform.localPosition = originalPosition;
            rectTransform.localScale = originalScale;
        }
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = originalAlpha;
        }
        
        LogDebug($"Restored original state for {gameObject.name}");
    }
    
    /// <summary>
    /// Gets animation duration adjusted for hand type
    /// </summary>
    private float GetAdjustedDuration(float baseDuration)
    {
        return isPlayerHand ? baseDuration : baseDuration * petHandSpeedMultiplier;
    }
    
    /// <summary>
    /// Gets alpha value adjusted for hand type
    /// </summary>
    private float GetAdjustedAlpha(float baseAlpha)
    {
        return isPlayerHand ? baseAlpha : baseAlpha * petHandAlphaMultiplier;
    }
    
    #endregion
    
    #region Integration Methods
    
    /// <summary>
    /// Called by CardDragDrop when a drag fails - starts the failed play animation
    /// </summary>
    public void OnDragFailed(Vector3 dropWorldPosition, Vector3 handReturnPosition)
    {
        LogDebug($"OnDragFailed called for {gameObject.name} - drop: {dropWorldPosition}, hand: {handReturnPosition}");
        
        // Convert world position to local position if needed
        Vector3 localDropPosition = transform.parent.InverseTransformPoint(dropWorldPosition);
        Vector3 localHandPosition = transform.parent.InverseTransformPoint(handReturnPosition);
        
        LogDebug($"Converted to local coordinates - drop: {localDropPosition}, hand: {localHandPosition}");
        
        AnimatePlayFailed(localDropPosition, localHandPosition);
    }
    
    /// <summary>
    /// Called by HandAnimator when card should be drawn
    /// </summary>
    public void OnDrawToHand(Vector3 targetPosition, System.Action onComplete = null)
    {
        AnimateDrawToHand(targetPosition, onComplete);
    }
    
    /// <summary>
    /// Called when card is successfully played
    /// </summary>
    public void OnPlaySuccess(Vector3? targetPosition = null, System.Action onComplete = null)
    {
        AnimatePlaySuccess(targetPosition, onComplete);
    }
    
    #endregion
    
    #region Unity Events
    
    private void OnDisable()
    {
        StopAnimation(false);
    }
    
    private void OnDestroy()
    {
        if (currentSequence != null)
        {
            currentSequence.Kill();
        }
    }
    
    #endregion
    
    #region Debug
    
    private void LogDebug(string message)
    {
        if (debugLogEnabled)
        {
            Debug.Log($"[CardAnimator] {message}");
        }
    }
    
    /// <summary>
    /// Test animation in editor
    /// </summary>
    [ContextMenu("Test Draw Animation")]
    public void TestDrawAnimation()
    {
        if (Application.isPlaying)
        {
            StoreOriginalState();
            AnimateDrawToHand(originalPosition);
        }
    }
    
    [ContextMenu("Test Failed Play Animation")]
    public void TestFailedPlayAnimation()
    {
        if (Application.isPlaying)
        {
            StoreOriginalState();
            Vector3 failPos = originalPosition + new Vector3(100, 50, 0);
            AnimatePlayFailed(failPos, originalPosition);
        }
    }
    
    [ContextMenu("Test Hover Enter")]
    public void TestHoverEnter()
    {
        if (Application.isPlaying)
        {
            StoreOriginalState();
            AnimateHoverEnter();
        }
    }
    
    [ContextMenu("Test Hover Exit")]
    public void TestHoverExit()
    {
        if (Application.isPlaying)
        {
            AnimateHoverExit();
        }
    }
    
        #endregion
    
    #region Hover Animation Methods
    
    /// <summary>
    /// Animates card scaling up on hover (called by UIHoverDetector for self-owned cards)
    /// </summary>
    public void AnimateHoverEnter()
    {
        LogDebug($"AnimateHoverEnter called for {gameObject.name} - enableHoverEffects: {enableHoverEffects}, isAnimating: {isAnimating}");
        
        if (!enableHoverEffects)
        {
            LogDebug($"Hover enter blocked for {gameObject.name} - hover effects disabled");
            return;
        }
        
        if (isAnimating)
        {
            LogDebug($"Hover enter blocked for {gameObject.name} - currently animating major animation");
            return;
        }
        
        // Ensure we have valid original scale
        if (originalScale == Vector3.zero)
        {
            LogDebug($"Original scale is zero for {gameObject.name} - storing current state");
            StoreOriginalState();
        }
        
        LogDebug($"Starting hover enter animation for {gameObject.name} - current scale: {rectTransform.localScale}, original scale: {originalScale}, target scale: {originalScale * hoverScaleMultiplier}");
        
        isHovered = true;
        
        // Kill any existing hover tween
        if (hoverTween != null)
        {
            hoverTween.Kill();
        }
        
        // Scale up smoothly
        Vector3 targetScale = originalScale * hoverScaleMultiplier;
        hoverTween = rectTransform.DOScale(targetScale, hoverAnimationDuration)
            .SetEase(hoverEase)
            .OnComplete(() => {
                LogDebug($"Hover enter animation completed for {gameObject.name} - final scale: {rectTransform.localScale}");
            });
    }
    
    /// <summary>
    /// Animates card scaling back to normal on hover exit (called by UIHoverDetector for self-owned cards)
    /// </summary>
    public void AnimateHoverExit()
    {
        LogDebug($"AnimateHoverExit called for {gameObject.name} - enableHoverEffects: {enableHoverEffects}, isAnimating: {isAnimating}");
        
        if (!enableHoverEffects)
        {
            LogDebug($"Hover exit blocked for {gameObject.name} - hover effects disabled");
            return;
        }
        
        if (isAnimating)
        {
            LogDebug($"Hover exit blocked for {gameObject.name} - currently animating major animation");
            return;
        }
        
        // Ensure we have valid original scale
        if (originalScale == Vector3.zero)
        {
            LogDebug($"Original scale is zero for {gameObject.name} - storing current state");
            StoreOriginalState();
        }
        
        LogDebug($"Starting hover exit animation for {gameObject.name} - current scale: {rectTransform.localScale}, original scale: {originalScale}");
        
        isHovered = false;
        
        // Kill any existing hover tween
        if (hoverTween != null)
        {
            hoverTween.Kill();
        }
        
        // Scale back to original size
        hoverTween = rectTransform.DOScale(originalScale, hoverAnimationDuration)
            .SetEase(hoverEase)
            .OnComplete(() => {
                LogDebug($"Hover exit animation completed for {gameObject.name} - final scale: {rectTransform.localScale}");
            });
    }
    
    #endregion
} 