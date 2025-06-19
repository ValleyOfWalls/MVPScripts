using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

/// <summary>
/// Manages the layout of cards in a hand using custom arc/fan positioning.
/// Replaces the horizontal layout group with more sophisticated card arrangement.
/// Attach to: Hand transform GameObject (the parent of card objects).
/// </summary>
public class HandLayoutManager : MonoBehaviour
{
    [Header("Real-time Preview")]
    [SerializeField] private bool enableRealtimePreview = true; // Enable real-time preview in editor
    [SerializeField] private bool previewInEditMode = true; // Preview even when not playing
    
    [Header("Arc Layout Settings")]
    [SerializeField] private float arcRadius = 800f; // Radius of the arc
    [SerializeField] private float maxArcAngle = 45f; // Maximum arc angle in degrees
    [SerializeField] private float cardSpacing = 120f; // Base spacing between cards
    [SerializeField] private float maxCardSpacing = 150f; // Maximum spacing between cards
    [SerializeField] private float minCardSpacing = 80f; // Minimum spacing between cards
    
    [Header("Card Scaling")]
    [SerializeField] private Vector3 baseCardScale = Vector3.one; // Base scale for cards
    [SerializeField] private float scaleVariation = 0.1f; // How much cards scale down when hand is full
    [SerializeField] private int maxCardsForFullScale = 5; // Number of cards before scaling starts
    
    [Header("Positioning")]
    [SerializeField] private Vector2 handPivotOffset = Vector2.zero; // Offset from transform position
    [SerializeField] private float cardRotationFactor = 1f; // How much cards rotate to follow arc
    [SerializeField] private float cardZSpacing = -0.1f; // Z spacing between cards (negative brings cards forward)
    
    [Header("Animation")]
    [SerializeField] private float layoutAnimationDuration = 0.3f;
    [SerializeField] private AnimationCurve layoutAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool enableLayoutAnimation = true;
    
    [Header("Debug")]
    [SerializeField] private bool debugLogEnabled = false;
    [SerializeField] private bool showGizmos = false;
    
    // Internal state
    private List<RectTransform> cardTransforms = new List<RectTransform>();
    private Dictionary<RectTransform, CardLayoutData> cardLayoutData = new Dictionary<RectTransform, CardLayoutData>();
    private RectTransform rectTransform;
    private bool layoutUpdatePending = false;
    private Coroutine layoutCoroutine;
    
    // Drag state tracking
    private RectTransform draggedCard;
    private CardLayoutData draggedCardOriginalData;
    
    /// <summary>
    /// Data structure to store card layout information
    /// </summary>
    private class CardLayoutData
    {
        public Vector3 targetPosition;
        public Vector3 targetScale;
        public Quaternion targetRotation;
        public int originalSiblingIndex;
        
        public CardLayoutData(Vector3 position, Vector3 scale, Quaternion rotation, int siblingIndex)
        {
            targetPosition = position;
            targetScale = scale;
            targetRotation = rotation;
            originalSiblingIndex = siblingIndex;
        }
    }
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError($"HandLayoutManager on {gameObject.name}: RectTransform component required!");
        }
    }
    
    private void Start()
    {
        // Initial layout update
        UpdateLayout();
    }
    
    private void OnEnable()
    {
        UpdateLayout();
    }
    
    /// <summary>
    /// Called when a card starts being dragged
    /// </summary>
    public void OnCardDragStart(RectTransform cardTransform)
    {
        if (cardTransform == null) return;
        
        LogDebug($"Card drag started: {cardTransform.name}");
        
        // Ensure we have current layout data before drag starts
        if (!cardLayoutData.ContainsKey(cardTransform))
        {
            // Calculate layout to get current position data
            RefreshCardList();
            CalculateCardLayoutData(cardTransforms);
        }
        
        // Store the dragged card and its original data
        draggedCard = cardTransform;
        if (cardLayoutData.ContainsKey(cardTransform))
        {
            draggedCardOriginalData = new CardLayoutData(
                cardLayoutData[cardTransform].targetPosition,
                cardLayoutData[cardTransform].targetScale,
                cardLayoutData[cardTransform].targetRotation,
                cardTransform.GetSiblingIndex()
            );
            LogDebug($"Stored original position for {cardTransform.name}: {draggedCardOriginalData.targetPosition}");
        }
        else
        {
            LogDebug($"Warning: No layout data found for {cardTransform.name}, using current transform data");
            draggedCardOriginalData = new CardLayoutData(
                cardTransform.localPosition,
                cardTransform.localScale,
                cardTransform.localRotation,
                cardTransform.GetSiblingIndex()
            );
        }
        
        // Update layout for remaining cards (excluding the dragged card)
        UpdateLayoutExcludingCard(cardTransform);
    }
    
    /// <summary>
    /// Called when a card drag ends
    /// </summary>
    public void OnCardDragEnd(RectTransform cardTransform, bool cardWasPlayed)
    {
        if (cardTransform == null) return;
        
        LogDebug($"Card drag ended: {cardTransform.name}, played: {cardWasPlayed}");
        
        if (cardWasPlayed)
        {
            // Card was played, remove it from our tracking
            RemoveCardFromLayout(cardTransform);
        }
        else
        {
            // Card returned to hand, restore it to its original position
            LogDebug($"Restoring {cardTransform.name} to hand layout");
            
            // Ensure the card is properly parented and active before layout update
            if (cardTransform.parent != transform)
            {
                cardTransform.SetParent(transform, false);
                LogDebug($"Reparented {cardTransform.name} back to hand");
            }
            
            // Restore the card to its original sibling index to maintain order
            if (draggedCardOriginalData != null)
            {
                cardTransform.SetSiblingIndex(draggedCardOriginalData.originalSiblingIndex);
                LogDebug($"Restored sibling index for {cardTransform.name} to {draggedCardOriginalData.originalSiblingIndex}");
                
                // Immediately restore the card to its exact original position
                cardTransform.localPosition = draggedCardOriginalData.targetPosition;
                cardTransform.localScale = draggedCardOriginalData.targetScale;
                cardTransform.localRotation = draggedCardOriginalData.targetRotation;
                
                LogDebug($"Immediately restored {cardTransform.name} to original position: {draggedCardOriginalData.targetPosition}");
            }
            
            // Clear drag state
            draggedCard = null;
            draggedCardOriginalData = null;
            
            // Refresh card list to ensure this card is included
            RefreshCardList();
            
            // Force immediate layout update to ensure all cards are properly positioned
            ForceUpdateLayout();
            
            LogDebug($"Completed layout restoration for {cardTransform.name}");
        }
    }
    
    /// <summary>
    /// Updates the layout of all cards in the hand
    /// </summary>
    public void UpdateLayout()
    {
        if (layoutCoroutine != null)
        {
            StopCoroutine(layoutCoroutine);
        }
        
        // Refresh card list
        RefreshCardList();
        
        if (enableLayoutAnimation && Application.isPlaying)
        {
            layoutCoroutine = StartCoroutine(AnimateLayoutUpdate());
        }
        else
        {
            ApplyLayoutImmediate();
        }
    }
    
    /// <summary>
    /// Updates layout excluding a specific card (used during drag)
    /// </summary>
    private void UpdateLayoutExcludingCard(RectTransform excludedCard)
    {
        if (layoutCoroutine != null)
        {
            StopCoroutine(layoutCoroutine);
        }
        
        List<RectTransform> cardsToLayout = cardTransforms.Where(c => c != excludedCard && c != null).ToList();
        
        if (enableLayoutAnimation && Application.isPlaying)
        {
            layoutCoroutine = StartCoroutine(AnimateLayoutUpdateForCards(cardsToLayout));
        }
        else
        {
            ApplyLayoutImmediateForCards(cardsToLayout);
        }
    }
    
    /// <summary>
    /// Refreshes the list of card transforms from children
    /// </summary>
    private void RefreshCardList()
    {
        cardTransforms.Clear();
        
        // Get all child RectTransforms that have Card components
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            RectTransform childRect = child as RectTransform;
            
            // Include all cards with Card component (don't require activeInHierarchy for layout calculation)
            if (childRect != null)
            {
                Card cardComponent = child.GetComponent<Card>();
                if (cardComponent != null)
                {
                    cardTransforms.Add(childRect);
                    LogDebug($"Added card to layout: {child.name} (active: {child.gameObject.activeInHierarchy})");
                }
            }
        }
        
        // Sort by sibling index to maintain consistent order
        cardTransforms = cardTransforms.OrderBy(c => c.GetSiblingIndex()).ToList();
        
        LogDebug($"Refreshed card list: {cardTransforms.Count} cards found");
    }
    
    /// <summary>
    /// Removes a card from layout tracking
    /// </summary>
    private void RemoveCardFromLayout(RectTransform cardTransform)
    {
        if (cardTransforms.Contains(cardTransform))
        {
            cardTransforms.Remove(cardTransform);
            LogDebug($"Removed card from layout: {cardTransform.name}");
        }
        
        if (cardLayoutData.ContainsKey(cardTransform))
        {
            cardLayoutData.Remove(cardTransform);
        }
        
        // Update layout for remaining cards
        UpdateLayout();
    }
    
    /// <summary>
    /// Calculates layout data for all cards
    /// </summary>
    private void CalculateCardLayoutData(List<RectTransform> cardsToLayout)
    {
        cardLayoutData.Clear();
        
        int cardCount = cardsToLayout.Count;
        if (cardCount == 0) return;
        
        // Calculate dynamic spacing
        float dynamicSpacing = CalculateDynamicSpacing(cardCount);
        
        // Calculate scale based on card count
        Vector3 cardScale = CalculateCardScale(cardCount);
        
        // Calculate arc parameters
        float totalArcWidth = (cardCount - 1) * dynamicSpacing;
        float arcAngle = Mathf.Min(maxArcAngle, totalArcWidth / arcRadius * Mathf.Rad2Deg);
        
        Vector2 handCenter = (Vector2)rectTransform.localPosition + handPivotOffset;
        
        for (int i = 0; i < cardCount; i++)
        {
            RectTransform cardRect = cardsToLayout[i];
            
            // Calculate position along arc
            float normalizedPosition = cardCount > 1 ? (float)i / (cardCount - 1) : 0.5f;
            float angle = Mathf.Lerp(-arcAngle * 0.5f, arcAngle * 0.5f, normalizedPosition);
            
            // Convert angle to radians
            float angleRad = angle * Mathf.Deg2Rad;
            
            // Calculate position on arc (inverted Y to create frown instead of smile)
            Vector2 arcPosition = new Vector2(
                Mathf.Sin(angleRad) * arcRadius,
                Mathf.Cos(angleRad) * arcRadius - arcRadius
            );
            
            // Calculate z position for proper card stacking (earlier cards go behind)
            float zPosition = i * cardZSpacing;
            
            Vector3 finalPosition = new Vector3(
                handCenter.x + arcPosition.x,
                handCenter.y + arcPosition.y,
                zPosition
            );
            
            // Calculate rotation (cards follow the arc, negative angle to point outward)
            Quaternion cardRotation = Quaternion.Euler(0, 0, -angle * cardRotationFactor);
            
            // Store layout data
            cardLayoutData[cardRect] = new CardLayoutData(
                finalPosition,
                cardScale,
                cardRotation,
                cardRect.GetSiblingIndex()
            );
        }
        
        LogDebug($"Calculated layout for {cardCount} cards - spacing: {dynamicSpacing:F1}, scale: {cardScale}, arc angle: {arcAngle:F1}°");
    }
    
    /// <summary>
    /// Calculates dynamic spacing based on card count
    /// </summary>
    private float CalculateDynamicSpacing(int cardCount)
    {
        if (cardCount <= 1) return cardSpacing;
        
        // Use cardSpacing as the baseline, but clamp it between min and max
        float baseSpacing = Mathf.Clamp(cardSpacing, minCardSpacing, maxCardSpacing);
        
        // Reduce spacing as more cards are added, starting from the base spacing
        float spacingReduction = Mathf.Clamp01((float)(cardCount - 1) / 10f);
        return Mathf.Lerp(baseSpacing, minCardSpacing, spacingReduction);
    }
    
    /// <summary>
    /// Calculates card scale based on card count
    /// </summary>
    private Vector3 CalculateCardScale(int cardCount)
    {
        if (cardCount <= maxCardsForFullScale)
        {
            return baseCardScale;
        }
        
        float scaleReduction = (cardCount - maxCardsForFullScale) * scaleVariation;
        float finalScale = Mathf.Max(0.5f, baseCardScale.x - scaleReduction);
        
        return Vector3.one * finalScale;
    }
    
    /// <summary>
    /// Applies layout immediately without animation
    /// </summary>
    private void ApplyLayoutImmediate()
    {
        ApplyLayoutImmediateForCards(cardTransforms);
    }
    
    /// <summary>
    /// Applies layout immediately for specific cards
    /// </summary>
    private void ApplyLayoutImmediateForCards(List<RectTransform> cardsToLayout)
    {
        CalculateCardLayoutData(cardsToLayout);
        
        foreach (var cardRect in cardsToLayout)
        {
            if (cardLayoutData.ContainsKey(cardRect))
            {
                var data = cardLayoutData[cardRect];
                cardRect.localPosition = data.targetPosition;
                cardRect.localScale = data.targetScale;
                cardRect.localRotation = data.targetRotation;
            }
        }
    }
    
    /// <summary>
    /// Animates layout update
    /// </summary>
    private IEnumerator AnimateLayoutUpdate()
    {
        yield return AnimateLayoutUpdateForCards(cardTransforms);
    }
    
    /// <summary>
    /// Animates layout update for specific cards
    /// </summary>
    private IEnumerator AnimateLayoutUpdateForCards(List<RectTransform> cardsToLayout)
    {
        CalculateCardLayoutData(cardsToLayout);
        
        // Store starting positions/scales/rotations
        Dictionary<RectTransform, Vector3> startPositions = new Dictionary<RectTransform, Vector3>();
        Dictionary<RectTransform, Vector3> startScales = new Dictionary<RectTransform, Vector3>();
        Dictionary<RectTransform, Quaternion> startRotations = new Dictionary<RectTransform, Quaternion>();
        
        foreach (var cardRect in cardsToLayout)
        {
            if (cardRect != null)
            {
                startPositions[cardRect] = cardRect.localPosition;
                startScales[cardRect] = cardRect.localScale;
                startRotations[cardRect] = cardRect.localRotation;
            }
        }
        
        float elapsed = 0f;
        
        while (elapsed < layoutAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / layoutAnimationDuration;
            float curveValue = layoutAnimationCurve.Evaluate(t);
            
            foreach (var cardRect in cardsToLayout)
            {
                if (cardRect != null && cardLayoutData.ContainsKey(cardRect))
                {
                    var data = cardLayoutData[cardRect];
                    
                    cardRect.localPosition = Vector3.Lerp(startPositions[cardRect], data.targetPosition, curveValue);
                    cardRect.localScale = Vector3.Lerp(startScales[cardRect], data.targetScale, curveValue);
                    cardRect.localRotation = Quaternion.Lerp(startRotations[cardRect], data.targetRotation, curveValue);
                }
            }
            
            yield return null;
        }
        
        // Ensure final positions are exact
        foreach (var cardRect in cardsToLayout)
        {
            if (cardRect != null && cardLayoutData.ContainsKey(cardRect))
            {
                var data = cardLayoutData[cardRect];
                cardRect.localPosition = data.targetPosition;
                cardRect.localScale = data.targetScale;
                cardRect.localRotation = data.targetRotation;
            }
        }
        
        layoutCoroutine = null;
    }
    
    /// <summary>
    /// Force updates layout immediately (useful for external calls)
    /// </summary>
    public void ForceUpdateLayout()
    {
        if (layoutCoroutine != null)
        {
            StopCoroutine(layoutCoroutine);
            layoutCoroutine = null;
        }
        
        // Refresh card list before applying layout
        RefreshCardList();
        ApplyLayoutImmediate();
        
        LogDebug($"Forced layout update complete for {cardTransforms.Count} cards");
    }
    
    /// <summary>
    /// Gets the index a card should have based on its current position
    /// </summary>
    public int GetInsertionIndex(Vector2 worldPosition)
    {
        if (cardTransforms.Count == 0) return 0;
        
        // Convert world position to local position
        Vector2 localPos = rectTransform.InverseTransformPoint(worldPosition);
        
        // Find the best insertion point based on x position
        for (int i = 0; i < cardTransforms.Count; i++)
        {
            if (localPos.x < cardTransforms[i].localPosition.x)
            {
                return i;
            }
        }
        
        return cardTransforms.Count;
    }
    
    /// <summary>
    /// Manually positions a specific card in the layout (useful for debugging)
    /// </summary>
    public void PositionCard(RectTransform cardTransform)
    {
        if (cardTransform == null) return;
        
        // Ensure the card is in our tracking list
        if (!cardTransforms.Contains(cardTransform))
        {
            RefreshCardList();
        }
        
        // Recalculate layout for all cards
        CalculateCardLayoutData(cardTransforms);
        
        // Apply layout to the specific card
        if (cardLayoutData.ContainsKey(cardTransform))
        {
            var data = cardLayoutData[cardTransform];
            cardTransform.localPosition = data.targetPosition;
            cardTransform.localScale = data.targetScale;
            cardTransform.localRotation = data.targetRotation;
            
            LogDebug($"Manually positioned card {cardTransform.name} at {data.targetPosition}");
        }
    }
    
    /// <summary>
    /// Gets the target position for a card without moving it (useful for animations)
    /// </summary>
    public Vector3 GetCardTargetPosition(RectTransform cardTransform)
    {
        if (cardTransform == null) return Vector3.zero;
        
        // Ensure the card is in our tracking list
        if (!cardTransforms.Contains(cardTransform))
        {
            RefreshCardList();
        }
        
        // Recalculate layout for all cards
        CalculateCardLayoutData(cardTransforms);
        
        // Return the target position without applying it
        if (cardLayoutData.ContainsKey(cardTransform))
        {
            return cardLayoutData[cardTransform].targetPosition;
        }
        
        return Vector3.zero;
    }
    
    /// <summary>
    /// Gets the complete layout data for a card without moving it
    /// </summary>
    public bool GetCardLayoutData(RectTransform cardTransform, out Vector3 targetPosition, out Vector3 targetScale, out Quaternion targetRotation)
    {
        targetPosition = Vector3.zero;
        targetScale = Vector3.one;
        targetRotation = Quaternion.identity;
        
        if (cardTransform == null) return false;
        
        // Ensure the card is in our tracking list
        if (!cardTransforms.Contains(cardTransform))
        {
            RefreshCardList();
        }
        
        // Recalculate layout for all cards
        CalculateCardLayoutData(cardTransforms);
        
        // Return the layout data without applying it
        if (cardLayoutData.ContainsKey(cardTransform))
        {
            var data = cardLayoutData[cardTransform];
            targetPosition = data.targetPosition;
            targetScale = data.targetScale;
            targetRotation = data.targetRotation;
            return true;
        }
        
        return false;
    }
    
    #region External Event Handlers
    
    /// <summary>
    /// Called when children change (cards added/removed)
    /// </summary>
    private void OnTransformChildrenChanged()
    {
        // Delay update slightly to ensure all changes are processed
        if (!layoutUpdatePending)
        {
            layoutUpdatePending = true;
            StartCoroutine(DelayedLayoutUpdate());
        }
    }
    
    private IEnumerator DelayedLayoutUpdate()
    {
        yield return null; // Wait one frame
        layoutUpdatePending = false;
        UpdateLayout();
    }
    
    #endregion
    
    #region Debug and Gizmos
    
    private void LogDebug(string message)
    {
        if (debugLogEnabled)
        {
            Debug.Log($"[HandLayoutManager] {message}");
        }
    }
    
    /// <summary>
    /// Enables debug logging temporarily for troubleshooting
    /// </summary>
    [ContextMenu("Enable Debug Logging")]
    public void EnableDebugLogging()
    {
        debugLogEnabled = true;
        Debug.Log("[HandLayoutManager] Debug logging enabled");
    }
    
    /// <summary>
    /// Disables debug logging
    /// </summary>
    [ContextMenu("Disable Debug Logging")]
    public void DisableDebugLogging()
    {
        debugLogEnabled = false;
        Debug.Log("[HandLayoutManager] Debug logging disabled");
    }
    
    private void OnDrawGizmos()
    {
        if (!showGizmos || rectTransform == null) return;
        
        // Draw arc visualization
        Vector3 worldCenter = transform.TransformPoint((Vector2)rectTransform.localPosition + handPivotOffset);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(worldCenter, 5f);
        
        // Draw arc
        int arcSegments = 20;
        float arcAngle = maxArcAngle;
        
        for (int i = 0; i < arcSegments; i++)
        {
            float angle1 = Mathf.Lerp(-arcAngle * 0.5f, arcAngle * 0.5f, (float)i / arcSegments) * Mathf.Deg2Rad;
            float angle2 = Mathf.Lerp(-arcAngle * 0.5f, arcAngle * 0.5f, (float)(i + 1) / arcSegments) * Mathf.Deg2Rad;
            
            Vector3 point1 = worldCenter + new Vector3(Mathf.Sin(angle1) * arcRadius, Mathf.Cos(angle1) * arcRadius - arcRadius, 0);
            Vector3 point2 = worldCenter + new Vector3(Mathf.Sin(angle2) * arcRadius, Mathf.Cos(angle2) * arcRadius - arcRadius, 0);
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(point1, point2);
        }
        
        // Draw card positions
        if (Application.isPlaying && cardLayoutData != null)
        {
            Gizmos.color = Color.green;
            foreach (var data in cardLayoutData.Values)
            {
                Vector3 worldPos = transform.TransformPoint(data.targetPosition);
                Gizmos.DrawWireCube(worldPos, Vector3.one * 20f);
            }
        }
    }
    
    #endregion
    
    #region Editor Real-time Preview
    
#if UNITY_EDITOR
    /// <summary>
    /// Called when values change in the inspector - enables real-time preview
    /// </summary>
    private void OnValidate()
    {
        if (!enableRealtimePreview) return;
        
        // Ensure we have a rect transform
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
        
        if (rectTransform == null) return;
        
        // Update layout in edit mode or play mode
        if (previewInEditMode || Application.isPlaying)
        {
            // Use delayed call to avoid issues with inspector updates
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && gameObject != null)
                {
                    PreviewLayout();
                }
            };
        }
    }
    
    /// <summary>
    /// Preview layout without animation (for editor use)
    /// </summary>
    private void PreviewLayout()
    {
        if (!enableRealtimePreview) return;
        
        // Refresh card list
        RefreshCardList();
        
        // Apply layout immediately (no animation in editor)
        ApplyLayoutImmediate();
        
        // Mark scene as dirty so changes are visible
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
    
    /// <summary>
    /// Context menu to manually trigger preview update
    /// </summary>
    [UnityEditor.MenuItem("CONTEXT/HandLayoutManager/Update Preview")]
    private static void UpdatePreviewContextMenu(UnityEditor.MenuCommand command)
    {
        HandLayoutManager manager = (HandLayoutManager)command.context;
        if (manager != null)
        {
            manager.PreviewLayout();
        }
    }
    
    /// <summary>
    /// Context menu to toggle real-time preview
    /// </summary>
    [UnityEditor.MenuItem("CONTEXT/HandLayoutManager/Toggle Real-time Preview")]
    private static void ToggleRealtimePreviewContextMenu(UnityEditor.MenuCommand command)
    {
        HandLayoutManager manager = (HandLayoutManager)command.context;
        if (manager != null)
        {
            manager.enableRealtimePreview = !manager.enableRealtimePreview;
            Debug.Log($"HandLayoutManager real-time preview: {(manager.enableRealtimePreview ? "Enabled" : "Disabled")}");
            
            if (manager.enableRealtimePreview)
            {
                manager.PreviewLayout();
            }
        }
    }
#endif
    
    #endregion
} 