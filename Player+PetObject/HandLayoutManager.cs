using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using DG.Tweening;

/// <summary>
/// Manages the layout of cards in a hand using custom arc/fan positioning.
/// Replaces the horizontal layout group with more sophisticated card arrangement.
/// Attach to: Hand transform GameObject (the parent of card objects).
/// 
/// CENTRALIZED POSITIONING SYSTEM:
/// This class now serves as the central hub for ALL card positioning during combat.
/// Other systems (HandManager, CardParenter, etc.) delegate positioning to this class.
/// 
/// NETWORK ARCHITECTURE WITH NETWORKTRANSFORM:
/// - Only the OWNING CLIENT positions their cards (checked via NetworkEntity.IsOwner)
/// - NetworkTransform automatically syncs positions to other clients
/// - No RPCs needed for card positioning - NetworkTransform handles all sync
/// - Prevents conflicts: each client only touches cards they own
/// 
/// Key Methods for External Systems:
/// - OnCardAddedToHand(): Call when cards are moved TO hand (unified handling)
/// - OnCardRemovedFromHand(): Call when cards are removed FROM hand
/// - OnCardMovedToNonHandLocation(): Static method for deck/discard positioning
/// - HandleCombatCardPositioning(): For combat-specific card positioning
/// - HandleDragPositioning(): For drag/drop operations
/// 
/// OPTIMIZATION NOTES:
/// - Implements debouncing to prevent duplicate layout updates during initialization
/// - Skips layout updates when hand is empty to avoid unnecessary calculations
/// - Defers initial layout until Start() to prevent OnEnable/Start duplication
/// - Only processes layout updates after initialization is complete
/// - External systems should use RequestLayoutUpdate() for safe layout requests
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
    [SerializeField] private Vector3 baseCardScale = new Vector3(1f, 1f, 1f); // Base scale for cards
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
    [SerializeField] private bool debugLogEnabled = true;
    [SerializeField] private bool showGizmos = false;
    
    // Internal state
    private List<RectTransform> cardTransforms = new List<RectTransform>();
    private Dictionary<RectTransform, CardLayoutData> cardLayoutData = new Dictionary<RectTransform, CardLayoutData>();
    private RectTransform rectTransform;
    private Coroutine layoutCoroutine;
    
    // State tracking
    private bool isInitialized = false;
    
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
        // Mark as initialized and only update layout if we have cards
        isInitialized = true;
        
        // Defer initial layout update - only update if hand has cards
        RefreshCardList();
        
        if (cardTransforms.Count > 0)
        {
            Debug.Log($"[LAYOUT_DEBUG] Start: {cardTransforms.Count} cards found, initializing layout on {gameObject.name}");
            UpdateLayout();
        }
        else
        {
            LogDebug("Skipping initial layout update - hand is empty");
        }
    }
    
    private void OnEnable()
    {
        // Skip layout update on OnEnable - let Start() handle initialization
        // This prevents duplicate calls during spawning
        if (isInitialized)
        {
            // Only update if we're already initialized and have cards
            RefreshCardList();
            if (cardTransforms.Count > 0)
            {
                UpdateLayout();
            }
        }
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
            // Only if we have cards to layout
            if (cardTransforms.Count > 0)
            {
                ForceUpdateLayout();
            }
            
            LogDebug($"Completed layout restoration for {cardTransform.name}");
        }
    }
    
    /// <summary>
    /// Updates the layout of all cards in the hand - THE ONLY LAYOUT UPDATE METHOD
    /// Works with NetworkTransform: Only the owning client positions cards, NetworkTransform syncs to others
    /// </summary>
    public void UpdateLayout()
    {
        // Check ownership to prevent conflicts with NetworkTransform
        // Only the card owner should position cards, NetworkTransform handles sync
        if (!ShouldProcessLayoutUpdate())
        {
            LogDebug("Skipping UpdateLayout - not owned by local client (NetworkTransform will sync positions from owner)");
            return;
        }

        if (!isInitialized)
        {
            LogDebug("Skipping UpdateLayout - not yet initialized");
            return;
        }

        // Stop any existing animation
        if (layoutCoroutine != null)
        {
            StopCoroutine(layoutCoroutine);
            layoutCoroutine = null;
        }

        // Refresh card list
        RefreshCardList();

        // Skip layout if hand is empty
        if (cardTransforms.Count == 0)
        {
            LogDebug("Skipping layout update - hand is empty");
            return;
        }

        Debug.Log($"[LAYOUT_DEBUG] UpdateLayout: {cardTransforms.Count} cards, {(enableLayoutAnimation ? "animated" : "immediate")} on {gameObject.name}");

        // Use animation if enabled and in play mode
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
        // Check ownership to prevent duplicate updates on host/client machines
        if (!ShouldProcessLayoutUpdate())
        {
            LogDebug("Skipping UpdateLayoutExcludingCard - not owned by local client");
            return;
        }
        
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
        Debug.Log($"[LAYOUT_DEBUG] RefreshCardList called on {gameObject.name} - {transform.childCount} children");
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
                    Debug.Log($"[LAYOUT_DEBUG] Added card to layout: {child.name} (active: {child.gameObject.activeInHierarchy}) on {gameObject.name}");
                    LogDebug($"Added card to layout: {child.name} (active: {child.gameObject.activeInHierarchy})");
                }
                else
                {
                    Debug.Log($"[LAYOUT_DEBUG] Child {child.name} has RectTransform but no Card component - skipping on {gameObject.name}");
                }
            }
            else
            {
                Debug.Log($"[LAYOUT_DEBUG] Child {child.name} is not a RectTransform - skipping on {gameObject.name}");
            }
        }
        
        // Sort by sibling index to maintain consistent order
        cardTransforms = cardTransforms.OrderBy(c => c.GetSiblingIndex()).ToList();
        
        Debug.Log($"[LAYOUT_DEBUG] Refreshed card list: {cardTransforms.Count} cards found on {gameObject.name}");
        LogDebug($"Refreshed card list: {cardTransforms.Count} cards found");
    }
    
    /// <summary>
    /// Removes a card from layout tracking
    /// </summary>
    private void RemoveCardFromLayout(RectTransform cardTransform)
    {
        // Use enhanced removal method that handles visual state cleanup
        RemoveCardFromLayoutWithVisualState(cardTransform);
        
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
        if (cardCount == 0) 
        {
            LogDebug("No cards to calculate layout for");
            return;
        }
        
        // Validate rectTransform exists
        if (rectTransform == null)
        {
            Debug.LogError($"[LAYOUT_DEBUG] rectTransform is null - cannot calculate layout on {gameObject.name}");
            return;
        }
        
        // Calculate dynamic spacing
        float dynamicSpacing = CalculateDynamicSpacing(cardCount);
        
        // Calculate scale based on card count
        Vector3 cardScale = CalculateCardScale(cardCount);
        
        // Calculate arc parameters
        float totalArcWidth = (cardCount - 1) * dynamicSpacing;
        float arcAngle = Mathf.Min(maxArcAngle, totalArcWidth / arcRadius * Mathf.Rad2Deg);
        
        Vector2 handCenter = (Vector2)rectTransform.localPosition + handPivotOffset;
        
        Debug.Log($"[LAYOUT_DEBUG] Layout calc: {cardCount} cards, spacing={dynamicSpacing:F0}, scale={cardScale:F2}, arc={arcAngle:F1}° on {gameObject.name}");
        
        // Validate that we have reasonable values
        if (arcRadius <= 0)
        {
            Debug.LogError($"[LAYOUT_DEBUG] Invalid arcRadius: {arcRadius} on {gameObject.name}");
            arcRadius = 800f; // Reset to default
        }
        
        for (int i = 0; i < cardCount; i++)
        {
            RectTransform cardRect = cardsToLayout[i];
            if (cardRect == null)
            {
                Debug.LogWarning($"[LAYOUT_DEBUG] Null card at index {i} - skipping on {gameObject.name}");
                continue;
            }
            
            // Calculate position along arc
            float normalizedPosition = cardCount > 1 ? (float)i / (cardCount - 1) : 0.5f;
            float angleFromCenter = (normalizedPosition - 0.5f) * arcAngle * Mathf.Deg2Rad;
            
            // Calculate arc position (upward fan)
            Vector2 arcPosition = new Vector2(
                Mathf.Sin(angleFromCenter) * arcRadius,
                Mathf.Cos(angleFromCenter) * arcRadius - arcRadius
            );
            
            Vector3 finalPosition = new Vector3(
                handCenter.x + arcPosition.x,
                handCenter.y + arcPosition.y,
                i * cardZSpacing
            );
            
            // SAFEGUARD: Clamp positions to reasonable screen bounds  
            finalPosition.x = Mathf.Clamp(finalPosition.x, -1000f, 1000f);
            finalPosition.y = Mathf.Clamp(finalPosition.y, -500f, 500f);
            
            // Calculate rotation to follow arc (negative to point outward)
            float cardRotation = -angleFromCenter * cardRotationFactor * Mathf.Rad2Deg;
            Quaternion finalRotation = Quaternion.Euler(0, 0, cardRotation);
            
            Debug.Log($"[LAYOUT_DEBUG] Card {i} ({cardRect.name}): pos={finalPosition}, scale={cardScale}, rotation={cardRotation:F1}° on {gameObject.name}");
            
            // Store layout data
            cardLayoutData[cardRect] = new CardLayoutData(finalPosition, cardScale, finalRotation, cardRect.GetSiblingIndex());
        }
    }
    
    /// <summary>
    /// Calculates the appropriate spacing between cards based on count
    /// </summary>
    private float CalculateDynamicSpacing(int cardCount)
    {
        // VALIDATE AND FIX SPACING VALUES
        ValidateLayoutSettings();
        
        if (cardCount <= 1) 
        {
            Debug.Log($"[LAYOUT_DEBUG] CalculateDynamicSpacing: {cardCount} card(s), returning base spacing {cardSpacing} on {gameObject.name}");
            return cardSpacing;
        }
        
        // Linear interpolation between min and max spacing based on card count
        // More cards = less spacing
        float normalizedCardCount = Mathf.Clamp01((float)(cardCount - 2) / 8f); // Normalize 2-10 cards to 0-1
        float dynamicSpacing = Mathf.Lerp(maxCardSpacing, minCardSpacing, normalizedCardCount);
        
        Debug.Log($"[LAYOUT_DEBUG] CalculateDynamicSpacing: {cardCount} cards, normalized={normalizedCardCount:F2}, spacing={dynamicSpacing:F1} (range: {minCardSpacing}-{maxCardSpacing}) on {gameObject.name}");
        return dynamicSpacing;
    }
    
    /// <summary>
    /// Validates and fixes unreasonable layout settings that cause off-screen positioning
    /// </summary>
    private void ValidateLayoutSettings()
    {
        bool changed = false;
        
        // Fix extreme spacing values
        if (cardSpacing > 200f || cardSpacing < 50f)
        {
            Debug.LogWarning($"[LAYOUT_DEBUG] Fixing extreme cardSpacing: {cardSpacing} -> 120 on {gameObject.name}");
            cardSpacing = 120f;
            changed = true;
        }
        
        if (maxCardSpacing > 200f || maxCardSpacing < 80f)
        {
            Debug.LogWarning($"[LAYOUT_DEBUG] Fixing extreme maxCardSpacing: {maxCardSpacing} -> 150 on {gameObject.name}");
            maxCardSpacing = 150f;
            changed = true;
        }
        
        if (minCardSpacing > 120f || minCardSpacing < 50f)
        {
            Debug.LogWarning($"[LAYOUT_DEBUG] Fixing extreme minCardSpacing: {minCardSpacing} -> 80 on {gameObject.name}");
            minCardSpacing = 80f;
            changed = true;
        }
        
        // Fix extreme arc radius
        if (arcRadius > 1000f || arcRadius < 400f)
        {
            Debug.LogWarning($"[LAYOUT_DEBUG] Fixing extreme arcRadius: {arcRadius} -> 800 on {gameObject.name}");
            arcRadius = 800f;
            changed = true;
        }
        
        // Fix extreme arc angle
        if (maxArcAngle > 60f || maxArcAngle < 20f)
        {
            Debug.LogWarning($"[LAYOUT_DEBUG] Fixing extreme maxArcAngle: {maxArcAngle} -> 45 on {gameObject.name}");
            maxArcAngle = 45f;
            changed = true;
        }
        
        if (changed)
        {
            Debug.Log($"[LAYOUT_DEBUG] Layout settings validated and fixed on {gameObject.name}");
        }
    }
    
    /// <summary>
    /// Calculates card scale based on the number of cards
    /// </summary>
    private Vector3 CalculateCardScale(int cardCount)
    {
        if (cardCount <= maxCardsForFullScale)
        {
            Debug.Log($"[LAYOUT_DEBUG] CalculateCardScale: {cardCount} cards <= {maxCardsForFullScale}, using base scale {baseCardScale} on {gameObject.name}");
            return baseCardScale;
        }
        
        // Calculate scale reduction for too many cards
        float scaleReduction = (cardCount - maxCardsForFullScale) * scaleVariation;
        float finalScale = Mathf.Max(0.6f, 1f - scaleReduction); // Minimum scale of 0.6
        Vector3 cardScale = baseCardScale * finalScale;
        
        Debug.Log($"[LAYOUT_DEBUG] CalculateCardScale: {cardCount} cards > {maxCardsForFullScale}, scale reduction={scaleReduction:F2}, final scale={cardScale} on {gameObject.name}");
        return cardScale;
    }
    
    /// <summary>
    /// Applies layout immediately without animation
    /// </summary>
    private void ApplyLayoutImmediate()
    {
        Debug.Log($"[LAYOUT_DEBUG] ApplyLayoutImmediate called for {cardTransforms.Count} cards on {gameObject.name}");
        ApplyLayoutImmediateForCards(cardTransforms);
    }
    
    /// <summary>
    /// Applies layout immediately for specific cards
    /// </summary>
    private void ApplyLayoutImmediateForCards(List<RectTransform> cardsToLayout)
    {
        CalculateCardLayoutData(cardsToLayout);
        
        int appliedCount = 0;
        foreach (var cardRect in cardsToLayout)
        {
            if (cardLayoutData.ContainsKey(cardRect))
            {
                var data = cardLayoutData[cardRect];
                
                // Apply calculated layout directly - no safety checks
                
                // Apply the calculated layout
                Vector3 oldPos = cardRect.localPosition;
                Vector3 oldScale = cardRect.localScale;
                
                cardRect.localPosition = data.targetPosition;
                cardRect.localScale = data.targetScale;
                cardRect.localRotation = data.targetRotation;
                
                Debug.Log($"[LAYOUT_DEBUG] ✓ APPLIED: {cardRect.name} pos({oldPos:F0} -> {data.targetPosition:F0}) scale({oldScale:F2} -> {data.targetScale:F2}) on {gameObject.name}");
                appliedCount++;
                
                LogDebug($"Applied layout to {cardRect.name}: pos={data.targetPosition}, scale={data.targetScale}");
            }
            else
            {
                Debug.LogWarning($"[LAYOUT_DEBUG] No layout data found for card {cardRect.name} on {gameObject.name}");
            }
        }
        
        Debug.Log($"[LAYOUT_DEBUG] Layout application complete: {appliedCount}/{cardsToLayout.Count} cards positioned on {gameObject.name}");
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
        Debug.Log($"[LAYOUT_DEBUG] 🎬 Starting animated layout for {cardsToLayout.Count} cards on {gameObject.name}");
        
        CalculateCardLayoutData(cardsToLayout);
        
        if (cardsToLayout.Count == 0)
        {
            layoutCoroutine = null;
            yield break;
        }
        
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
        
        Debug.Log($"[LAYOUT_DEBUG] DOTween animation starting - duration: {layoutAnimationDuration}s on {gameObject.name}");
        
        // Create DOTween sequence for all cards
        Sequence layoutSequence = DOTween.Sequence();
        
        foreach (var cardRect in cardsToLayout)
        {
            if (cardRect != null && cardLayoutData.ContainsKey(cardRect))
            {
                var data = cardLayoutData[cardRect];
                
                // Add simultaneous animations for position, scale, and rotation
                layoutSequence.Join(cardRect.DOLocalMove(data.targetPosition, layoutAnimationDuration).SetEase(layoutAnimationCurve));
                layoutSequence.Join(cardRect.DOScale(data.targetScale, layoutAnimationDuration).SetEase(layoutAnimationCurve));
                layoutSequence.Join(cardRect.DOLocalRotateQuaternion(data.targetRotation, layoutAnimationDuration).SetEase(layoutAnimationCurve));
            }
        }
        
        // Wait for the sequence to complete
        yield return layoutSequence.WaitForCompletion();
        
        // Ensure final positions are exact and LOG the application
        Debug.Log($"[LAYOUT_DEBUG] 🎯 Animation complete - applying final positions on {gameObject.name}");
        foreach (var cardRect in cardsToLayout)
        {
            if (cardRect != null && cardLayoutData.ContainsKey(cardRect))
            {
                var data = cardLayoutData[cardRect];
                Vector3 oldPos = cardRect.localPosition;
                
                cardRect.localPosition = data.targetPosition;
                cardRect.localScale = data.targetScale;
                cardRect.localRotation = data.targetRotation;
                
                Debug.Log($"[LAYOUT_DEBUG] ✓ ANIMATED: {cardRect.name} pos({oldPos:F0} -> {data.targetPosition:F0}) on {gameObject.name}");
            }
        }
        
        layoutCoroutine = null;
        
        // Force canvas update to ensure visual changes are applied
        Canvas.ForceUpdateCanvases();
        Debug.Log($"[LAYOUT_DEBUG] Animation layout complete - canvas updated on {gameObject.name}");
    }
    
    /// <summary>
    /// Forces an immediate layout update without debouncing
    /// Used when we need to ensure layout is updated immediately
    /// </summary>
    public void ForceUpdateLayout()
    {
        // Check ownership to prevent duplicate updates on host/client machines
        if (!ShouldProcessLayoutUpdate())
        {
            LogDebug("Skipping ForceUpdateLayout - not owned by local client");
            return;
        }
        
        // Stop any running layout animation
        if (layoutCoroutine != null)
        {
            StopCoroutine(layoutCoroutine);
        }
        
        // Refresh card list to ensure we have the latest state
        RefreshCardList();
        
        // Skip layout if hand is empty
        if (cardTransforms.Count == 0)
        {
            LogDebug("Skipping force layout update - hand is empty");
            return;
        }
        
        // Apply layout immediately regardless of animation settings
        ApplyLayoutImmediate();
        
        LogDebug("Forced immediate layout update completed");
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
        
        // Apply layout to the specific card only if we have cards
        if (cardTransforms.Count > 0 && cardLayoutData.ContainsKey(cardTransform))
        {
            var data = cardLayoutData[cardTransform];
            cardTransform.localPosition = data.targetPosition;
            cardTransform.localScale = data.targetScale;
            cardTransform.localRotation = data.targetRotation;
            
            LogDebug($"Manually positioned card {cardTransform.name} at {data.targetPosition}");
        }
        else if (cardTransforms.Count == 0)
        {
            LogDebug("Skipping manual card positioning - hand is empty");
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
        targetScale = Vector3.zero;
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
    /// Called when children change (cards added/removed) - SIMPLE EVENT HANDLING
    /// </summary>
    private void OnTransformChildrenChanged()
    {
        Debug.Log($"[LAYOUT_DEBUG] OnTransformChildrenChanged called on {gameObject.name} - current child count: {transform.childCount}");
        
        // Only trigger layout updates if we're initialized
        if (!isInitialized)
        {
            LogDebug("Skipping OnTransformChildrenChanged - not yet initialized");
            return;
        }
        
        // Simple: just update the layout
        UpdateLayout();
    }
    
    /// <summary>
    /// Checks if this HandLayoutManager should process layout updates
    /// Prevents duplicate updates on host/client machines
    /// </summary>
    private bool ShouldProcessLayoutUpdate()
    {
        // Get NetworkEntity to check ownership - aligns with NetworkTransform ownership
        NetworkEntity networkEntity = GetComponentInParent<NetworkEntity>();
        
        if (networkEntity == null)
        {
            // No network entity found - this is likely a local/test scenario
            Debug.Log($"[LAYOUT_DEBUG] ShouldProcessLayoutUpdate: No NetworkEntity found - allowing layout update (local/test scenario) on {gameObject.name}");
            LogDebug("No NetworkEntity found - allowing layout update (local/test scenario)");
            return true;
        }
        
        // Check if this is owned by the local client - this aligns with NetworkTransform
        // Only the owning client should position cards, NetworkTransform will sync to others
        bool isOwner = networkEntity.IsOwner;
        
        Debug.Log($"[LAYOUT_DEBUG] ShouldProcessLayoutUpdate: NetworkEntity found, IsOwner={isOwner} for entity {networkEntity.EntityName.Value} on {gameObject.name}");
        LogDebug($"NetworkEntity ownership check: IsOwner={isOwner} for entity {networkEntity.EntityName.Value}");
        return isOwner;
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
      //  Debug.Log("[HandLayoutManager] Debug logging enabled");
    }
    
    /// <summary>
    /// Disables debug logging
    /// </summary>
    [ContextMenu("Disable Debug Logging")]
    public void DisableDebugLogging()
    {
        debugLogEnabled = false;
       // Debug.Log("[HandLayoutManager] Debug logging disabled");
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
                Gizmos.DrawWireCube(worldPos, new Vector3(20f, 20f, 20f));
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
    
    #region True Centralization - Complete Positioning/Scaling API
    
    /// <summary>
    /// Animation states that affect positioning and scaling
    /// </summary>
    public enum CardAnimationState
    {
        Normal,          // Default hand layout
        Drawing,         // Card being drawn to hand
        Playing,         // Card being played/removed
        PlayFailed,      // Card failed to play, returning to hand
        Dragging,        // Card being dragged
        Hovering,        // Card being hovered
        Dissolving       // Card dissolving in/out
    }
    
    /// <summary>
    /// Comprehensive data for card positioning and visual state
    /// </summary>
    private class CardVisualState
    {
        public Vector3 position;
        public Vector3 scale;
        public Quaternion rotation;
        public float alpha;
        public CardAnimationState animationState;
        public bool isAnimating;
        public System.DateTime lastStateChange;
        
        public CardVisualState()
        {
            position = Vector3.zero;
            scale = Vector3.zero; // Will be set to proper scale when needed
            rotation = Quaternion.identity;
            alpha = 1.0f;
            animationState = CardAnimationState.Normal;
            isAnimating = false;
            lastStateChange = System.DateTime.Now;
        }
        
        public void UpdateState(CardAnimationState newState)
        {
            animationState = newState;
            lastStateChange = System.DateTime.Now;
        }
    }
    
    // Tracking for all card visual states
    private Dictionary<RectTransform, CardVisualState> cardVisualStates = new Dictionary<RectTransform, CardVisualState>();
    
    /// <summary>
    /// CENTRALIZED METHOD: Sets card to drawing animation state
    /// Replaces CardAnimator.AnimateDrawToHand positioning logic
    /// </summary>
    public void SetCardDrawingState(GameObject cardObject, Vector3 targetPosition, Vector3 targetScale, System.Action onComplete = null)
    {
        RectTransform cardTransform = cardObject.transform as RectTransform;
        if (cardTransform == null) return;
        
        var visualState = GetOrCreateVisualState(cardTransform);
        visualState.UpdateState(CardAnimationState.Drawing);
        visualState.isAnimating = true;
        
        LogDebug($"CENTRALIZED: Setting card {cardObject.name} to drawing state - pos: {targetPosition}, scale: {targetScale}");
        
        // Apply drawing state immediately
        cardTransform.localPosition = targetPosition;
        cardTransform.localScale = targetScale;
        
        // Trigger completion callback if provided
        onComplete?.Invoke();
        
        // Return to normal state after drawing completes
        StartCoroutine(ReturnToNormalStateAfterDelay(cardTransform, 0.1f));
    }
    
    /// <summary>
    /// CENTRALIZED METHOD: Sets card to playing animation state
    /// Replaces CardAnimator.AnimatePlaySuccess positioning logic
    /// </summary>
    public void SetCardPlayingState(GameObject cardObject, Vector3? targetPosition = null, System.Action onComplete = null)
    {
        RectTransform cardTransform = cardObject.transform as RectTransform;
        if (cardTransform == null) return;
        
        var visualState = GetOrCreateVisualState(cardTransform);
        visualState.UpdateState(CardAnimationState.Playing);
        visualState.isAnimating = true;
        
        LogDebug($"CENTRALIZED: Setting card {cardObject.name} to playing state");
        
        // Apply play state (typically involves removal from hand layout)
        OnCardRemovedFromHand(cardObject);
        
        if (targetPosition.HasValue)
        {
            cardTransform.localPosition = targetPosition.Value;
        }
        
        onComplete?.Invoke();
    }
    
    /// <summary>
    /// CENTRALIZED METHOD: Sets card to failed play animation state
    /// Replaces CardAnimator.AnimatePlayFailed positioning logic
    /// </summary>
    public void SetCardPlayFailedState(GameObject cardObject, Vector3 handPosition, Vector3 handScale, Quaternion handRotation, System.Action onComplete = null)
    {
        RectTransform cardTransform = cardObject.transform as RectTransform;
        if (cardTransform == null) return;
        
        var visualState = GetOrCreateVisualState(cardTransform);
        visualState.UpdateState(CardAnimationState.PlayFailed);
        visualState.isAnimating = true;
        
        LogDebug($"CENTRALIZED: Setting card {cardObject.name} to play failed state - returning to hand");
        
        // Return card to hand with provided layout data
        cardTransform.localPosition = handPosition;
        cardTransform.localScale = handScale;
        cardTransform.localRotation = handRotation;
        
        // Add back to hand layout
        OnCardAddedToHand(cardObject);
        
        onComplete?.Invoke();
        
        // Return to normal state after animation completes
        StartCoroutine(ReturnToNormalStateAfterDelay(cardTransform, 0.5f));
    }
    
    /// <summary>
    /// CENTRALIZED METHOD: Sets card to dragging state
    /// Replaces CardDragDrop scaling logic
    /// </summary>
    public void SetCardDraggingState(GameObject cardObject, float dragScale = 1.2f, float dragAlpha = 0.8f)
    {
        RectTransform cardTransform = cardObject.transform as RectTransform;
        if (cardTransform == null) return;
        
        var visualState = GetOrCreateVisualState(cardTransform);
        visualState.UpdateState(CardAnimationState.Dragging);
        visualState.isAnimating = false; // Dragging is a static state, not animating
        
        LogDebug($"CENTRALIZED: Setting card {cardObject.name} to dragging state - scale: {dragScale}, alpha: {dragAlpha}");
        
        // Store original scale before applying drag scale
        if (cardLayoutData.TryGetValue(cardTransform, out var layoutData))
        {
            cardTransform.localScale = layoutData.targetScale * dragScale;
        }
        else
        {
                            cardTransform.localScale = baseCardScale * dragScale;
        }
        
        // Apply drag alpha
        CanvasGroup canvasGroup = cardObject.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = dragAlpha;
        }
        
        visualState.scale = cardTransform.localScale;
        visualState.alpha = dragAlpha;
    }
    
    /// <summary>
    /// CENTRALIZED METHOD: Sets card to hover state
    /// Replaces CardAnimator hover effects
    /// </summary>
    public void SetCardHoverState(GameObject cardObject, bool isHovering, float hoverScaleMultiplier = 1.1f)
    {
        RectTransform cardTransform = cardObject.transform as RectTransform;
        if (cardTransform == null) return;
        
        var visualState = GetOrCreateVisualState(cardTransform);
        visualState.UpdateState(isHovering ? CardAnimationState.Hovering : CardAnimationState.Normal);
        
        LogDebug($"CENTRALIZED: Setting card {cardObject.name} hover state: {isHovering}");
        
        if (isHovering)
        {
            // Apply hover scale
            if (cardLayoutData.TryGetValue(cardTransform, out var layoutData))
            {
                cardTransform.localScale = layoutData.targetScale * hoverScaleMultiplier;
            }
            else
            {
                cardTransform.localScale = baseCardScale * hoverScaleMultiplier;
            }
        }
        else
        {
            // Return to normal layout scale
            ReturnCardToNormalState(cardTransform);
        }
        
        visualState.scale = cardTransform.localScale;
    }
    
    /// <summary>
    /// CENTRALIZED METHOD: Restores card to normal hand layout state
    /// Replaces scattered state restoration logic
    /// </summary>
    public void ReturnCardToNormalState(RectTransform cardTransform)
    {
        if (cardTransform == null) return;
        
        var visualState = GetOrCreateVisualState(cardTransform);
        visualState.UpdateState(CardAnimationState.Normal);
        visualState.isAnimating = false;
        
        LogDebug($"CENTRALIZED: Returning card {cardTransform.name} to normal state");
        
        // Apply normal layout data if available
        if (cardLayoutData.TryGetValue(cardTransform, out var layoutData))
        {
            cardTransform.localPosition = layoutData.targetPosition;
            cardTransform.localScale = layoutData.targetScale;
            cardTransform.localRotation = layoutData.targetRotation;
        }
        else
        {
            // Force layout update to calculate correct position
            UpdateLayout();
        }
        
        // Reset alpha to normal
        CanvasGroup canvasGroup = cardTransform.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1.0f;
        }
        
        visualState.position = cardTransform.localPosition;
        visualState.scale = cardTransform.localScale;
        visualState.rotation = cardTransform.localRotation;
        visualState.alpha = 1.0f;
    }
    
    /// <summary>
    /// CENTRALIZED METHOD: Sets card transform directly (for external state restoration)
    /// Replaces Card.cs state restoration logic
    /// </summary>
    public void SetCardTransformState(GameObject cardObject, Vector3 position, Vector3 scale, Quaternion rotation, float alpha = 1.0f)
    {
        RectTransform cardTransform = cardObject.transform as RectTransform;
        if (cardTransform == null) return;
        
        var visualState = GetOrCreateVisualState(cardTransform);
        
        LogDebug($"CENTRALIZED: Setting card {cardObject.name} transform state - pos: {position}, scale: {scale}, rot: {rotation.eulerAngles}, alpha: {alpha}");
        
        // Apply transform state
        cardTransform.localPosition = position;
        cardTransform.localScale = scale;
        cardTransform.localRotation = rotation;
        
        // Apply alpha
        CanvasGroup canvasGroup = cardObject.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }
        
        // Update visual state tracking
        visualState.position = position;
        visualState.scale = scale;
        visualState.rotation = rotation;
        visualState.alpha = alpha;
        visualState.UpdateState(CardAnimationState.Normal); // Assume external state setting is for normal state
    }
    
    /// <summary>
    /// CENTRALIZED METHOD: Gets current card animation state
    /// </summary>
    public CardAnimationState GetCardAnimationState(RectTransform cardTransform)
    {
        if (cardVisualStates.TryGetValue(cardTransform, out var visualState))
        {
            return visualState.animationState;
        }
        return CardAnimationState.Normal;
    }
    
    /// <summary>
    /// CENTRALIZED METHOD: Checks if card is currently animating
    /// </summary>
    public bool IsCardAnimating(RectTransform cardTransform)
    {
        if (cardVisualStates.TryGetValue(cardTransform, out var visualState))
        {
            return visualState.isAnimating;
        }
        return false;
    }
    
    /// <summary>
    /// Helper to get or create visual state for a card
    /// </summary>
    private CardVisualState GetOrCreateVisualState(RectTransform cardTransform)
    {
        if (!cardVisualStates.TryGetValue(cardTransform, out var visualState))
        {
            visualState = new CardVisualState();
            cardVisualStates[cardTransform] = visualState;
        }
        return visualState;
    }
    
    /// <summary>
    /// Coroutine to return card to normal state after a delay
    /// </summary>
    private System.Collections.IEnumerator ReturnToNormalStateAfterDelay(RectTransform cardTransform, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (cardTransform != null)
        {
            ReturnCardToNormalState(cardTransform);
        }
    }
    
    /// <summary>
    /// Enhanced RemoveCardFromLayout to also clean up visual state
    /// </summary>
    private void RemoveCardFromLayoutWithVisualState(RectTransform cardTransform)
    {
        // Remove from layout data
        if (cardLayoutData.ContainsKey(cardTransform))
        {
            cardLayoutData.Remove(cardTransform);
        }
        
        // Remove from visual state tracking
        if (cardVisualStates.ContainsKey(cardTransform))
        {
            cardVisualStates.Remove(cardTransform);
        }
        
        // Remove from card list
        if (cardTransforms.Contains(cardTransform))
        {
            cardTransforms.Remove(cardTransform);
        }
        
        LogDebug($"CENTRALIZED: Removed card {cardTransform.name} from all tracking");
    }
    
    #endregion
    
    #region Centralized Card Management
    
    /// <summary>
    /// Handles when a card is added to hand - SIMPLE AND DIRECT
    /// </summary>
    public void OnCardAddedToHand(GameObject cardObject)
    {
        if (cardObject == null) return;

        RectTransform cardTransform = cardObject.transform as RectTransform;
        if (cardTransform == null) return;

        LogDebug($"OnCardAddedToHand called for {cardObject.name}");

        // Ensure the card is properly parented to this hand
        if (cardTransform.parent != transform)
        {
            cardTransform.SetParent(transform, false);
            LogDebug($"Reparented {cardObject.name} to hand");
        }

        // Reset card visual state for new hand position
        ResetCardVisualState(cardObject);

        // Simple: just update the layout when ready
        if (isInitialized)
        {
            Debug.Log($"[LAYOUT_DEBUG] OnCardAddedToHand: {cardObject.name} -> updating layout on {gameObject.name}");
            UpdateLayout();
        }
        else
        {
            LogDebug($"HandLayoutManager not yet initialized - layout will be handled when Start() completes");
        }
    }
    
    /// <summary>
    /// Handles when a card is removed from hand - centralizes removal logic
    /// </summary>
    public void OnCardRemovedFromHand(GameObject cardObject)
    {
        if (cardObject == null) return;
        
        RectTransform cardTransform = cardObject.transform as RectTransform;
        if (cardTransform == null) return;
        
        Debug.Log($"[LAYOUT_DEBUG] OnCardRemovedFromHand called for {cardObject.name} on {gameObject.name}");
        LogDebug($"OnCardRemovedFromHand called for {cardObject.name}");
        
        // Remove from our tracking
        RemoveCardFromLayout(cardTransform);
        Debug.Log($"[LAYOUT_DEBUG] Removed {cardObject.name} from layout tracking on {gameObject.name}");
    }
    
    /// <summary>
    /// Handles when a card is moved to a non-hand location (deck/discard) - provides consistent positioning
    /// NOTE: This method only handles parenting and basic transform reset for NON-HAND locations
    /// For hand locations, use OnCardAddedToHand() instead
    /// </summary>
    public static void OnCardMovedToNonHandLocation(GameObject cardObject, Transform targetTransform)
    {
        if (cardObject == null || targetTransform == null) return;
        
        Debug.Log($"HandLayoutManager CENTRALIZED: Moving card {cardObject.name} to non-hand location {targetTransform.name}");
        
        // Set parent and reset position for non-hand locations (deck/discard/etc)
        cardObject.transform.SetParent(targetTransform, false);
        cardObject.transform.localPosition = Vector3.zero;
        cardObject.transform.localRotation = Quaternion.identity;
        // Note: Scale should be set by the target location's requirements, not assumed
    }
    
    /// <summary>
    /// Centralized method for handling card position during combat card play/return
    /// Replaces the scattered logic in HandManager's EnsureHandLayoutAfterDelay methods
    /// </summary>
    public void HandleCombatCardPositioning(GameObject cardObject, bool wasReturned = false)
    {
        if (cardObject == null) return;
        
        RectTransform cardTransform = cardObject.transform as RectTransform;
        if (cardTransform == null) return;
        
        LogDebug($"HandleCombatCardPositioning called for {cardObject.name} (returned: {wasReturned})");
        
        if (wasReturned)
        {
            // Card was returned to hand (e.g., from TestCombat mode)
            OnCardAddedToHand(cardObject);
        }
        else
        {
            // Card was played from hand
            OnCardRemovedFromHand(cardObject);
        }
    }
    
    /// <summary>
    /// Handles positioning for cards during drag operations in combat
    /// Centralizes the drag positioning logic
    /// </summary>
    public void HandleDragPositioning(RectTransform cardTransform, bool isDragStart, bool cardWasPlayed = false)
    {
        if (cardTransform == null) return;
        
        if (isDragStart)
        {
            OnCardDragStart(cardTransform);
        }
        else
        {
            OnCardDragEnd(cardTransform, cardWasPlayed);
        }
    }
    
    /// <summary>
    /// Resets card visual state when it's moved to hand
    /// Centralizes the visual reset logic from HandManager
    /// </summary>
    private void ResetCardVisualState(GameObject cardObject)
    {
        if (cardObject == null) return;
        
        // Reset card visual state for hand position
        CanvasGroup cardCanvasGroup = cardObject.GetComponent<CanvasGroup>();
        if (cardCanvasGroup != null)
        {
            cardCanvasGroup.alpha = 1.0f; // Reset alpha from previous animations
        }
        
        // Reset CardAnimator state if present
        CardAnimator cardAnimator = cardObject.GetComponent<CardAnimator>();
        if (cardAnimator != null)
        {
            cardAnimator.StoreOriginalState(); // Store fresh state with alpha = 1.0
            // Refresh layout manager reference in case card was moved between hands
            cardAnimator.RefreshLayoutManager();
        }
        
        LogDebug($"Reset visual state for {cardObject.name}");
    }
    
    // Note: EnsureLayoutAfterCardAdded coroutine removed - unified layout handling now uses RequestLayoutUpdate()
    
    /// <summary>
    /// Gets the HandLayoutManager instance from a transform, if it exists
    /// Utility method for other systems to check if they should delegate positioning
    /// </summary>
    public static HandLayoutManager GetHandLayoutManager(Transform handTransform)
    {
        if (handTransform == null) return null;
        return handTransform.GetComponent<HandLayoutManager>();
    }
    
    /// <summary>
    /// Checks if a transform has a HandLayoutManager and returns positioning delegate info
    /// Helps other systems determine how to handle positioning
    /// </summary>
    public static bool ShouldDelegatePositioning(Transform targetTransform, out HandLayoutManager layoutManager)
    {
        layoutManager = null;
        if (targetTransform == null) return false;
        
        layoutManager = targetTransform.GetComponent<HandLayoutManager>();
        return layoutManager != null;
    }
    
    #endregion
} 