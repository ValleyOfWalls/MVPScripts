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
    [SerializeField] private Vector3 playTargetOffset = new Vector3(0, 200, 0);
    [SerializeField] private Ease playEase = Ease.InBack;
    [SerializeField] private float playFadeOutAlpha = 0.3f;
    
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
    private HandLayoutManager handLayoutManager;
    
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
        
        // Find HandLayoutManager in parent hierarchy
        handLayoutManager = GetComponentInParent<HandLayoutManager>();
        
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
        
        if (handLayoutManager == null)
            LogDebug($"No HandLayoutManager found in parent hierarchy for {gameObject.name}");
    }
    
    #region Public Animation Methods
    
    /// <summary>
    /// Animates card drawing into hand from below screen
    /// Uses DOTween for smooth animation instead of immediate positioning
    /// </summary>
    public void AnimateDrawToHand(Vector3 targetPosition, System.Action onComplete = null)
    {
        if (isAnimating)
        {
            LogDebug($"Cannot animate draw - already animating: {gameObject.name}");
            return;
        }
        
        LogDebug($"Starting draw animation for {gameObject.name} to position {targetPosition}");
        
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
        
        // Store current position and set start position
        Vector3 startPosition = targetPosition + drawStartOffset;
        rectTransform.localPosition = startPosition;
        
        // Ensure card is visible at start
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
        
        // Get target scale from layout manager if available
        Vector3 targetScale = GetCurrentLayoutScale();
        
        // Create animation sequence
        currentSequence = DOTween.Sequence();
        float duration = GetAdjustedDuration(drawAnimationDuration);
        
        // Animate position with easing
        currentSequence.Append(rectTransform.DOLocalMove(targetPosition, duration).SetEase(drawEase));
        
        // Animate scale with slight overshoot effect
        if (drawScaleOvershoot > 1f)
        {
            Vector3 overshootScale = targetScale * drawScaleOvershoot;
            currentSequence.Join(rectTransform.DOScale(overshootScale, duration * 0.6f).SetEase(Ease.OutQuad));
            currentSequence.Append(rectTransform.DOScale(targetScale, duration * 0.4f).SetEase(Ease.InQuad));
        }
        else
        {
            currentSequence.Join(rectTransform.DOScale(targetScale, duration).SetEase(drawEase));
        }
        
        currentSequence.OnComplete(() => {
            isAnimating = false;
            
            // Notify HandAnimator that this card is done animating
            if (handAnimator != null)
            {
                handAnimator.RemoveAnimatingCard(this);
            }
            
            // Update layout manager about final position
            if (handLayoutManager != null)
            {
                handLayoutManager.OnCardAddedToHand(gameObject);
            }
            
            onComplete?.Invoke();
            LogDebug($"Draw animation completed for {gameObject.name}");
        });
    }
    
    /// <summary>
    /// Animates card playing successfully
    /// CENTRALIZED: Delegates all positioning/scaling to HandLayoutManager
    /// </summary>
    public void AnimatePlaySuccess(Vector3? targetPosition = null, System.Action onComplete = null)
    {
        if (isAnimating)
        {
            LogDebug($"Cannot animate play success - already animating: {gameObject.name}");
            return;
        }
        
        LogDebug($"CENTRALIZED: Starting play success animation for {gameObject.name} - delegating to HandLayoutManager");
        
        // Try to refresh missing component references before animating
        if (handAnimator == null)
        {
            handAnimator = GetComponentInParent<HandAnimator>();
            if (handAnimator != null)
            {
                LogDebug($"Refreshed HandAnimator reference for {gameObject.name}: Found");
            }
        }
        
        if (handLayoutManager == null)
        {
            handLayoutManager = GetComponentInParent<HandLayoutManager>();
            if (handLayoutManager != null)
            {
                LogDebug($"Refreshed HandLayoutManager reference for {gameObject.name}: Found");
            }
        }
        
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
        
        // CENTRALIZED: Delegate all positioning/scaling to HandLayoutManager
        if (handLayoutManager != null)
        {
            Vector3 target = targetPosition ?? (rectTransform.localPosition + playTargetOffset);
            handLayoutManager.SetCardPlayingState(gameObject, target, () => {
                isAnimating = false;
                
                // Notify HandAnimator that this card is done animating
                if (handAnimator != null)
                {
                    handAnimator.RemoveAnimatingCard(this);
                }
                
                onComplete?.Invoke();
                LogDebug($"CENTRALIZED: Play success animation completed for {gameObject.name}");
            });
        }
        else
        {
            Debug.LogError($"CardAnimator: No HandLayoutManager found for {gameObject.name} - cannot perform centralized play success animation");
            isAnimating = false;
            onComplete?.Invoke();
        }
    }
    
    /// <summary>
    /// Animates failed play: dissolve out at drop position, dissolve back in at hand
    /// CENTRALIZED: Delegates all positioning/scaling to HandLayoutManager
    /// </summary>
    public void AnimatePlayFailed(Vector3 failPosition, Vector3 handPosition, System.Action onComplete = null)
    {
        if (isAnimating)
        {
            LogDebug($"Cannot animate play failed - already animating: {gameObject.name}");
            return;
        }
        
        LogDebug($"CENTRALIZED: Starting play failed animation for {gameObject.name} - delegating to HandLayoutManager");
        
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
        
        // CENTRALIZED: Delegate all positioning/scaling to HandLayoutManager
        if (handLayoutManager != null)
        {
            Vector3 handScale = GetCurrentLayoutScale();
            handLayoutManager.SetCardPlayFailedState(gameObject, handPosition, handScale, Quaternion.identity, () => {
                isAnimating = false;
                shouldReturnToHand = false;
                
                // Notify HandAnimator that this card is done animating
                if (handAnimator != null)
                {
                    handAnimator.RemoveAnimatingCard(this);
                }
                
                onComplete?.Invoke();
                LogDebug($"CENTRALIZED: Play failed animation completed for {gameObject.name}");
            });
        }
        else
        {
            Debug.LogError($"CardAnimator: No HandLayoutManager found for {gameObject.name} - cannot perform centralized play failed animation");
            isAnimating = false;
            shouldReturnToHand = false;
            onComplete?.Invoke();
        }
    }
    
    /// <summary>
    /// Animates failed play with complete layout data: dissolve out at drop position, dissolve back in with proper layout
    /// CENTRALIZED: Delegates all positioning/scaling to HandLayoutManager
    /// </summary>
    public void AnimatePlayFailedComplete(Vector3 failPosition, Vector3 handPosition, Vector3 handScale, Quaternion handRotation, System.Action onComplete = null)
    {
        if (isAnimating)
        {
            LogDebug($"Cannot animate complete play failed - already animating: {gameObject.name}");
            return;
        }
        
        LogDebug($"CENTRALIZED: Starting complete play failed animation for {gameObject.name} - delegating to HandLayoutManager");
        
        isAnimating = true;
        dropPosition = failPosition;
        shouldReturnToHand = true;
        
        // CENTRALIZED: Delegate all positioning/scaling to HandLayoutManager
        if (handLayoutManager != null)
        {
            handLayoutManager.SetCardPlayFailedState(gameObject, handPosition, handScale, handRotation, () => {
                isAnimating = false;
                shouldReturnToHand = false;
                
                // Notify HandAnimator that this card is done animating
                if (handAnimator != null)
                {
                    handAnimator.RemoveAnimatingCard(this);
                }
                
                onComplete?.Invoke();
                LogDebug($"CENTRALIZED: Complete play failed animation finished for {gameObject.name}");
            });
        }
        else
        {
            Debug.LogError($"CardAnimator: No HandLayoutManager found for {gameObject.name} - cannot perform centralized complete play failed animation");
            isAnimating = false;
            shouldReturnToHand = false;
            onComplete?.Invoke();
        }
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
    /// <summary>
    /// Animates card appearing via dissolve in effect
    /// CENTRALIZED: Delegates all positioning/scaling to HandLayoutManager
    /// </summary>
    public void AnimateDissolveIn(System.Action onComplete = null)
    {
        if (isAnimating)
        {
            LogDebug($"Cannot animate dissolve in - already animating: {gameObject.name}");
            return;
        }
        
        LogDebug($"CENTRALIZED: Starting dissolve in animation for {gameObject.name} - delegating to HandLayoutManager");
        
        isAnimating = true;
        
        // CENTRALIZED: Delegate dissolve in effect to HandLayoutManager
        if (handLayoutManager != null)
        {
            handLayoutManager.SetCardTransformState(gameObject, originalPosition, originalScale, Quaternion.identity, GetAdjustedAlpha(originalAlpha));
            isAnimating = false;
            onComplete?.Invoke();
            LogDebug($"CENTRALIZED: Dissolve in animation completed for {gameObject.name}");
        }
        else
        {
            Debug.LogError($"CardAnimator: No HandLayoutManager found for {gameObject.name} - cannot perform centralized dissolve in animation");
            isAnimating = false;
            onComplete?.Invoke();
        }
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
    /// CENTRALIZED: Delegates all positioning/scaling to HandLayoutManager
    /// </summary>
    public void RestoreOriginalState()
    {
        LogDebug($"CENTRALIZED: Restoring original state for {gameObject.name} - delegating to HandLayoutManager");
        
        // CENTRALIZED: Delegate state restoration to HandLayoutManager
        if (handLayoutManager != null && rectTransform != null)
        {
            handLayoutManager.SetCardTransformState(gameObject, originalPosition, originalScale, Quaternion.identity, originalAlpha);
            LogDebug($"CENTRALIZED: Restored state via HandLayoutManager for {gameObject.name}");
        }
        else
        {
            Debug.LogError($"CardAnimator: No HandLayoutManager found for {gameObject.name} - cannot perform centralized state restoration");
        }
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
    
    /// <summary>
    /// Gets the current target scale from HandLayoutManager or falls back to stored original scale
    /// </summary>
    private Vector3 GetCurrentLayoutScale()
    {
        // If we have a HandLayoutManager, get the current target scale for this card
        if (handLayoutManager != null && rectTransform != null)
        {
            if (handLayoutManager.GetCardLayoutData(rectTransform, out Vector3 targetPosition, out Vector3 targetScale, out Quaternion targetRotation))
            {
                LogDebug($"Got current layout scale from HandLayoutManager for {gameObject.name}: {targetScale}");
                return targetScale;
            }
        }
        
        // Fallback to stored original scale
        LogDebug($"Using stored original scale for {gameObject.name}: {originalScale}");
        return originalScale;
    }
    
    /// <summary>
    /// Refreshes the HandLayoutManager reference and updates scale information
    /// Call this if the card is moved to a different hand or if layout manager is added/changed
    /// </summary>
    public void RefreshLayoutManager()
    {
        handLayoutManager = GetComponentInParent<HandLayoutManager>();
        LogDebug($"Refreshed HandLayoutManager reference for {gameObject.name}: {(handLayoutManager != null ? "Found" : "Not found")}");
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
    
    [ContextMenu("Test Layout Scale Integration")]
    public void TestLayoutScale()
    {
        if (Application.isPlaying)
        {
            LogDebug($"=== Layout Scale Test for {gameObject.name} ===");
            LogDebug($"Current transform scale: {rectTransform.localScale}");
            LogDebug($"Stored original scale: {originalScale}");
            
            Vector3 layoutScale = GetCurrentLayoutScale();
            LogDebug($"Current layout scale: {layoutScale}");
            
            if (handLayoutManager != null)
            {
                LogDebug($"HandLayoutManager found: {handLayoutManager.gameObject.name}");
            }
            else
            {
                LogDebug("No HandLayoutManager found");
            }
        }
    }
    
    [ContextMenu("Refresh Layout Manager")]
    public void TestRefreshLayoutManager()
    {
        RefreshLayoutManager();
    }
    
    #endregion
    
    #region Hover Animation Methods
    
    /// <summary>
    /// Animates card scaling up on hover (called by UIHoverDetector for self-owned cards)
    /// CENTRALIZED: Delegates all positioning/scaling to HandLayoutManager
    /// </summary>
    public void AnimateHoverEnter()
    {
        LogDebug($"CENTRALIZED: AnimateHoverEnter called for {gameObject.name} - delegating to HandLayoutManager");
        
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
        
        isHovered = true;
        
        // Kill any existing hover tween
        if (hoverTween != null)
        {
            hoverTween.Kill();
        }
        
        // CENTRALIZED: Delegate hover scaling to HandLayoutManager
        if (handLayoutManager != null)
        {
            handLayoutManager.SetCardHoverState(gameObject, true, hoverScaleMultiplier);
            LogDebug($"CENTRALIZED: Hover enter completed for {gameObject.name}");
        }
        else
        {
            Debug.LogError($"CardAnimator: No HandLayoutManager found for {gameObject.name} - cannot perform centralized hover enter animation");
        }
    }
    
    /// <summary>
    /// Animates card scaling back to normal on hover exit (called by UIHoverDetector for self-owned cards)
    /// CENTRALIZED: Delegates all positioning/scaling to HandLayoutManager
    /// </summary>
    public void AnimateHoverExit()
    {
        LogDebug($"CENTRALIZED: AnimateHoverExit called for {gameObject.name} - delegating to HandLayoutManager");
        
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
        
        isHovered = false;
        
        // Kill any existing hover tween
        if (hoverTween != null)
        {
            hoverTween.Kill();
        }
        
        // CENTRALIZED: Delegate hover scaling to HandLayoutManager
        if (handLayoutManager != null)
        {
            handLayoutManager.SetCardHoverState(gameObject, false);
            LogDebug($"CENTRALIZED: Hover exit completed for {gameObject.name}");
        }
        else
        {
            Debug.LogError($"CardAnimator: No HandLayoutManager found for {gameObject.name} - cannot perform centralized hover exit animation");
        }
    }
    
    #endregion
} 