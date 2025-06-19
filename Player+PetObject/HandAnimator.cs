using UnityEngine;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages hand-level animations and coordinates with individual CardAnimator components.
/// Handles draw sequences, hand reorganization, and animation coordination.
/// Attach to: Hand transform GameObject (same as HandLayoutManager).
/// </summary>
public class HandAnimator : MonoBehaviour
{
    [Header("Hand Animation Settings")]
    [SerializeField] private float drawSequenceDelay = 0.1f; // Delay between cards when drawing multiple
    [SerializeField] private float layoutUpdateDelay = 0.15f; // Delay before updating layout after animations
    [SerializeField] private bool animateLayoutChanges = true;
    [SerializeField] private float handShakeIntensity = 2f;
    [SerializeField] private float handShakeDuration = 0.3f;
    
    [Header("Hand Type Settings")]
    [SerializeField] private bool isPlayerHand = true;
    [SerializeField] private Vector3 drawSourceOffset = new Vector3(0, -300f, 0); // Where cards come from
    
    [Header("Animation Coordination")]
    [SerializeField] private bool waitForAnimationsToComplete = true;
    [SerializeField] private float maxAnimationWaitTime = 5f;
    
    [Header("Debug")]
    [SerializeField] private bool debugLogEnabled = false;
    
    // Components
    private HandLayoutManager handLayoutManager;
    private RectTransform rectTransform;
    
    // Animation state
    private List<CardAnimator> animatingCards = new List<CardAnimator>();
    private bool isHandAnimating = false;
    private Coroutine currentDrawSequence;
    private Coroutine layoutUpdateCoroutine;
    
    // Events
    public System.Action<int> OnDrawSequenceStarted; // Parameter: number of cards
    public System.Action OnDrawSequenceCompleted;
    public System.Action OnHandLayoutAnimationStarted;
    public System.Action OnHandLayoutAnimationCompleted;
    
    public bool IsAnimating => isHandAnimating || animatingCards.Count > 0;
    
    private void Awake()
    {
        // Get components
        handLayoutManager = GetComponent<HandLayoutManager>();
        rectTransform = GetComponent<RectTransform>();
        
        // Determine hand type
        DetermineHandType();
        
        ValidateComponents();
    }
    
    private void Start()
    {
        // Subscribe to layout manager events if available
        if (handLayoutManager != null)
        {
            // We'll add integration here after modifying HandLayoutManager
        }
    }
    
    private void DetermineHandType()
    {
        // Similar logic to CardAnimator
        Transform current = transform;
        while (current != null)
        {
            string name = current.name.ToLower();
            if (name.Contains("player"))
            {
                isPlayerHand = true;
                break;
            }
            else if (name.Contains("pet") || name.Contains("opponent"))
            {
                isPlayerHand = false;
                break;
            }
            current = current.parent;
        }
        
        LogDebug($"Determined hand type: {(isPlayerHand ? "Player" : "Pet")} for hand {gameObject.name}");
    }
    
    private void ValidateComponents()
    {
        if (rectTransform == null)
            Debug.LogError($"HandAnimator on {gameObject.name}: Missing RectTransform component!");
        
        if (handLayoutManager == null)
            Debug.LogWarning($"HandAnimator on {gameObject.name}: No HandLayoutManager found - some features may not work");
    }
    
    #region Public Animation Methods
    
    /// <summary>
    /// Animates drawing multiple cards in sequence
    /// </summary>
    public void AnimateDrawSequence(List<CardAnimator> cards, System.Action onComplete = null)
    {
        if (cards == null || cards.Count == 0)
        {
            LogDebug("Cannot animate draw sequence - no cards provided");
            onComplete?.Invoke();
            return;
        }
        
        if (currentDrawSequence != null)
        {
            LogDebug("Draw sequence already in progress - stopping previous sequence");
            StopCoroutine(currentDrawSequence);
        }
        
        LogDebug($"Starting draw sequence for {cards.Count} cards");
        
        currentDrawSequence = StartCoroutine(DrawSequenceCoroutine(cards, onComplete));
    }
    
    /// <summary>
    /// Animates a single card being drawn to hand
    /// </summary>
    public void AnimateDrawCard(CardAnimator cardAnimator, Vector3? targetPosition = null, System.Action onComplete = null)
    {
        if (cardAnimator == null)
        {
            LogDebug("Cannot animate draw - CardAnimator is null");
            onComplete?.Invoke();
            return;
        }
        
        LogDebug($"Animating single card draw: {cardAnimator.gameObject.name}");
        
        // Get target position from layout manager or use provided position
        Vector3 target = targetPosition ?? GetCardTargetPosition(cardAnimator);
        
        // Track this card as animating
        AddAnimatingCard(cardAnimator);
        
        // Start the draw animation
        cardAnimator.AnimateDrawToHand(target, () => {
            RemoveAnimatingCard(cardAnimator);
            
            // Update layout after card is drawn
            ScheduleLayoutUpdate();
            
            onComplete?.Invoke();
        });
    }
    
    /// <summary>
    /// Animates the hand shaking (e.g., when cards can't be played)
    /// </summary>
    public void AnimateHandShake(System.Action onComplete = null)
    {
        if (isHandAnimating)
        {
            LogDebug("Cannot shake hand - already animating");
            onComplete?.Invoke();
            return;
        }
        
        LogDebug("Starting hand shake animation");
        
        isHandAnimating = true;
        Vector3 originalPosition = rectTransform.localPosition;
        
        // Create shake sequence
        Sequence shakeSequence = DOTween.Sequence();
        
        for (int i = 0; i < 6; i++)
        {
            Vector3 shakeOffset = new Vector3(
                Random.Range(-handShakeIntensity, handShakeIntensity),
                Random.Range(-handShakeIntensity * 0.5f, handShakeIntensity * 0.5f),
                0
            );
            
            shakeSequence.Append(rectTransform.DOLocalMove(originalPosition + shakeOffset, handShakeDuration / 12f));
        }
        
        // Return to original position
        shakeSequence.Append(rectTransform.DOLocalMove(originalPosition, handShakeDuration / 6f));
        
        shakeSequence.OnComplete(() => {
            isHandAnimating = false;
            onComplete?.Invoke();
            LogDebug("Hand shake animation completed");
        });
    }
    
    /// <summary>
    /// Triggers layout update with optional animation
    /// </summary>
    public void UpdateLayoutAnimated(System.Action onComplete = null)
    {
        if (handLayoutManager == null)
        {
            LogDebug("Cannot update layout - no HandLayoutManager found");
            onComplete?.Invoke();
            return;
        }
        
        LogDebug("Updating hand layout with animation");
        
        OnHandLayoutAnimationStarted?.Invoke();
        
        if (animateLayoutChanges)
        {
            handLayoutManager.UpdateLayout();
            
            // Wait a bit for animation to complete
            StartCoroutine(WaitForLayoutAnimation(onComplete));
        }
        else
        {
            handLayoutManager.ForceUpdateLayout();
            OnHandLayoutAnimationCompleted?.Invoke();
            onComplete?.Invoke();
        }
    }
    
    #endregion
    
    #region Animation Coordination
    
    /// <summary>
    /// Adds a card to the list of currently animating cards
    /// </summary>
    public void AddAnimatingCard(CardAnimator cardAnimator)
    {
        if (!animatingCards.Contains(cardAnimator))
        {
            animatingCards.Add(cardAnimator);
            LogDebug($"Added {cardAnimator.gameObject.name} to animating cards list (total: {animatingCards.Count})");
        }
    }
    
    /// <summary>
    /// Removes a card from the list of currently animating cards
    /// </summary>
    public void RemoveAnimatingCard(CardAnimator cardAnimator)
    {
        if (animatingCards.Remove(cardAnimator))
        {
            LogDebug($"Removed {cardAnimator.gameObject.name} from animating cards list (total: {animatingCards.Count})");
        }
    }
    
    /// <summary>
    /// Waits for all cards to finish animating
    /// </summary>
    public IEnumerator WaitForAllAnimations()
    {
        float waitStartTime = Time.time;
        
        while (animatingCards.Count > 0 && (Time.time - waitStartTime) < maxAnimationWaitTime)
        {
            // Remove any null or non-animating cards
            animatingCards.RemoveAll(card => card == null || !card.IsAnimating);
            yield return null;
        }
        
        if (Time.time - waitStartTime >= maxAnimationWaitTime)
        {
            LogDebug($"Animation wait timeout reached - forcing completion");
            animatingCards.Clear();
        }
        
        LogDebug("All card animations completed");
    }
    
    #endregion
    
    #region Private Methods
    
    /// <summary>
    /// Coroutine for drawing multiple cards in sequence
    /// </summary>
    private IEnumerator DrawSequenceCoroutine(List<CardAnimator> cards, System.Action onComplete)
    {
        OnDrawSequenceStarted?.Invoke(cards.Count);
        isHandAnimating = true;
        
        LogDebug($"Starting draw sequence for {cards.Count} cards with delay {drawSequenceDelay}s");
        
        // Draw each card with a delay
        for (int i = 0; i < cards.Count; i++)
        {
            CardAnimator card = cards[i];
            if (card != null)
            {
                Vector3 targetPosition = GetCardTargetPosition(card);
                
                AddAnimatingCard(card);
                
                card.AnimateDrawToHand(targetPosition, () => {
                    RemoveAnimatingCard(card);
                });
                
                // Wait for delay before next card (except for last card)
                if (i < cards.Count - 1)
                {
                    yield return new WaitForSeconds(drawSequenceDelay);
                }
            }
        }
        
        // Wait for all cards to finish their individual animations
        if (waitForAnimationsToComplete)
        {
            yield return StartCoroutine(WaitForAllAnimations());
        }
        
        // Update layout after all cards are drawn
        ScheduleLayoutUpdate();
        
        isHandAnimating = false;
        currentDrawSequence = null;
        
        OnDrawSequenceCompleted?.Invoke();
        onComplete?.Invoke();
        
        LogDebug("Draw sequence completed");
    }
    
    /// <summary>
    /// Gets the target position for a card from the layout manager
    /// </summary>
    private Vector3 GetCardTargetPosition(CardAnimator cardAnimator)
    {
        if (handLayoutManager != null && cardAnimator != null)
        {
            // Use the HandLayoutManager's GetCardTargetPosition method
            RectTransform cardRect = cardAnimator.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                Vector3 targetPosition = handLayoutManager.GetCardTargetPosition(cardRect);
                LogDebug($"Got target position for {cardAnimator.gameObject.name}: {targetPosition}");
                return targetPosition;
            }
        }
        
        // Fallback: just use local zero position
        LogDebug($"Using fallback position for {cardAnimator?.gameObject.name ?? "null"}");
        return Vector3.zero;
    }
    
    /// <summary>
    /// Schedules a layout update after a brief delay
    /// </summary>
    private void ScheduleLayoutUpdate()
    {
        if (layoutUpdateCoroutine != null)
        {
            StopCoroutine(layoutUpdateCoroutine);
        }
        
        layoutUpdateCoroutine = StartCoroutine(DelayedLayoutUpdate());
    }
    
    /// <summary>
    /// Performs layout update after delay
    /// </summary>
    private IEnumerator DelayedLayoutUpdate()
    {
        yield return new WaitForSeconds(layoutUpdateDelay);
        
        if (handLayoutManager != null)
        {
            LogDebug("Performing delayed layout update");
            handLayoutManager.UpdateLayout();
        }
        
        layoutUpdateCoroutine = null;
    }
    
    /// <summary>
    /// Waits for layout animation to complete
    /// </summary>
    private IEnumerator WaitForLayoutAnimation(System.Action onComplete)
    {
        // Wait for layout animation to complete
        // This is a simplified version - we'll improve it when integrating with HandLayoutManager
        yield return new WaitForSeconds(0.5f);
        
        OnHandLayoutAnimationCompleted?.Invoke();
        onComplete?.Invoke();
    }
    
    #endregion
    
    #region Card Layering Methods
    
    /// <summary>
    /// Brings a card to the front using Canvas sorting order (doesn't affect layout)
    /// </summary>
    public void BringCardToFront(CardAnimator cardAnimator)
    {
        if (cardAnimator == null)
        {
            LogDebug("Cannot bring card to front - CardAnimator is null");
            return;
        }
        
        // Get or add Canvas component to the card
        Canvas cardCanvas = cardAnimator.GetComponent<Canvas>();
        if (cardCanvas == null)
        {
            cardCanvas = cardAnimator.gameObject.AddComponent<Canvas>();
        }
        
        // Store the original sorting order if not already stored
        CardHoverData hoverData = cardAnimator.GetComponent<CardHoverData>();
        if (hoverData == null)
        {
            hoverData = cardAnimator.gameObject.AddComponent<CardHoverData>();
        }
        
        if (!hoverData.HasStoredSortingOrder)
        {
            hoverData.StoreOriginalSortingOrder(cardCanvas.sortingOrder);
            LogDebug($"Stored original sorting order {hoverData.OriginalSortingOrder} for {cardAnimator.gameObject.name}");
        }
        
        // Set high sorting order to bring to front (without affecting layout)
        cardCanvas.overrideSorting = true;
        cardCanvas.sortingOrder = 1000; // High value to ensure it's on top
        
        LogDebug($"Brought {cardAnimator.gameObject.name} to front using Canvas sorting order");
    }
    
    /// <summary>
    /// Restores a card's Canvas sorting order to its original value
    /// </summary>
    public void RestoreCardPosition(CardAnimator cardAnimator)
    {
        if (cardAnimator == null)
        {
            LogDebug("Cannot restore card position - CardAnimator is null");
            return;
        }
        
        Canvas cardCanvas = cardAnimator.GetComponent<Canvas>();
        if (cardCanvas == null)
        {
            LogDebug($"Cannot restore card position - no Canvas on {cardAnimator.gameObject.name}");
            return;
        }
        
        CardHoverData hoverData = cardAnimator.GetComponent<CardHoverData>();
        if (hoverData == null || !hoverData.HasStoredSortingOrder)
        {
            LogDebug($"Cannot restore card position - no stored sorting order for {cardAnimator.gameObject.name}");
            return;
        }
        
        // Restore to original sorting order
        cardCanvas.sortingOrder = hoverData.OriginalSortingOrder;
        cardCanvas.overrideSorting = hoverData.OriginalSortingOrder != 0; // Only override if non-zero
        
        LogDebug($"Restored {cardAnimator.gameObject.name} to sorting order {hoverData.OriginalSortingOrder}");
        
        // Clear the stored sorting order
        hoverData.ClearStoredSortingOrder();
    }
    
    #endregion
    
    #region Integration Methods
    
    /// <summary>
    /// Called when a card is added to the hand (e.g., drawn from deck)
    /// </summary>
    public void OnCardAddedToHand(CardAnimator cardAnimator)
    {
        LogDebug($"Card added to hand: {cardAnimator.gameObject.name}");
        AnimateDrawCard(cardAnimator);
    }
    
    /// <summary>
    /// Called when multiple cards are added to hand (e.g., initial draw)
    /// </summary>
    public void OnCardsAddedToHand(List<CardAnimator> cards)
    {
        LogDebug($"Multiple cards added to hand: {cards.Count}");
        AnimateDrawSequence(cards);
    }
    
    /// <summary>
    /// Called when a card is successfully played
    /// </summary>
    public void OnCardPlayed(CardAnimator cardAnimator)
    {
        LogDebug($"Card played successfully: {cardAnimator.gameObject.name}");
        
        // Remove from animating list if present
        RemoveAnimatingCard(cardAnimator);
        
        // Trigger layout update after a brief delay
        ScheduleLayoutUpdate();
    }
    
    /// <summary>
    /// Called when a card play fails
    /// </summary>
    public void OnCardPlayFailed(CardAnimator cardAnimator, Vector3 dropPosition)
    {
        LogDebug($"Card play failed: {cardAnimator.gameObject.name}");
        
        Vector3 handReturnPosition = GetCardTargetPosition(cardAnimator);
        
        AddAnimatingCard(cardAnimator);
        
        cardAnimator.OnDragFailed(dropPosition, handReturnPosition);
        
        // The card animator will call RemoveAnimatingCard when its animation completes
        // via the completion callback
    }
    
    /// <summary>
    /// Gets the complete layout data for a card (position, scale, rotation)
    /// </summary>
    public bool GetCardCompleteLayoutData(CardAnimator cardAnimator, out Vector3 targetPosition, out Vector3 targetScale, out Quaternion targetRotation)
    {
        targetPosition = Vector3.zero;
        targetScale = Vector3.one;
        targetRotation = Quaternion.identity;
        
        if (handLayoutManager != null && cardAnimator != null)
        {
            RectTransform cardRect = cardAnimator.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                return handLayoutManager.GetCardLayoutData(cardRect, out targetPosition, out targetScale, out targetRotation);
            }
        }
        
        return false;
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Gets all CardAnimator components in child objects
    /// </summary>
    public List<CardAnimator> GetAllCardAnimators()
    {
        return GetComponentsInChildren<CardAnimator>().ToList();
    }
    
    /// <summary>
    /// Stops all current animations
    /// </summary>
    public void StopAllAnimations()
    {
        LogDebug("Stopping all hand animations");
        
        // Stop draw sequence
        if (currentDrawSequence != null)
        {
            StopCoroutine(currentDrawSequence);
            currentDrawSequence = null;
        }
        
        // Stop layout update
        if (layoutUpdateCoroutine != null)
        {
            StopCoroutine(layoutUpdateCoroutine);
            layoutUpdateCoroutine = null;
        }
        
        // Stop all card animations
        foreach (CardAnimator card in animatingCards.ToList())
        {
            if (card != null)
            {
                card.StopAnimation();
            }
        }
        
        animatingCards.Clear();
        isHandAnimating = false;
    }
    
    #endregion
    
    #region Unity Events
    
    private void OnDisable()
    {
        StopAllAnimations();
    }
    
    private void OnDestroy()
    {
        StopAllAnimations();
        DOTween.Kill(this);
    }
    
    #endregion
    
    #region Debug
    
    private void LogDebug(string message)
    {
        if (debugLogEnabled)
        {
            Debug.Log($"[HandAnimator] {message}");
        }
    }
    
    /// <summary>
    /// Test draw sequence in editor
    /// </summary>
    [ContextMenu("Test Draw Sequence")]
    public void TestDrawSequence()
    {
        if (Application.isPlaying)
        {
            List<CardAnimator> cards = GetAllCardAnimators();
            if (cards.Count > 0)
            {
                AnimateDrawSequence(cards);
            }
            else
            {
                LogDebug("No cards found for test draw sequence");
            }
        }
    }
    
    [ContextMenu("Test Hand Shake")]
    public void TestHandShake()
    {
        if (Application.isPlaying)
        {
            AnimateHandShake();
        }
    }
    
    #endregion
} 