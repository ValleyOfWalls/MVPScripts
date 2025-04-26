using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections; // Added for Coroutines
using System.Collections.Generic;
using System.Linq;

public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public CardData cardData { get; set; } // Set by GameManager when instantiated
    public int cardHandIndex { get; set; } = -1; // Track position in hand list to differentiate identical cards
    public bool isDiscarding { get; set; } = false; // ADDED: Flag for discard animation
    public bool isDragging { get; private set; } = false; // ADDED: Flag for dragging state

    // --- ADDED: Hover effect fields ---
    public Vector3 originalPosition { get; set; }
    public Quaternion originalRotation { get; set; }
    public Vector3 originalScale { get; set; }
    private static readonly float hoverOffsetY = 30f;
    private static readonly float neighborOffsetX = 40f;
    private static readonly float hoverScaleFactor = 1.1f;
    private int originalSiblingIndex = -1; // Initialize to -1
    private bool isHovering = false;
    // --- END ADDED ---

    // --- ADDED: Animation fields ---
    private static readonly float hoverAnimDuration = 0.15f; // Duration for hover animation
    private Coroutine animationCoroutine = null; // Single coroutine for self-animation
    // --- END ADDED ---

    // --- ADDED: Target State Fields ---
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetScale;
    private Vector3 currentNeighborOffset = Vector3.zero; // Offset applied by neighbors
    // --- END ADDED ---

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector3 startPosition; // Screen position at drag start
    private Transform originalParent;
    private Canvas rootCanvas;
    private GameManager gameManager;
    private Vector2 dragOffset; // Offset from card pivot to mouse click point - SCREEN SPACE
    private Vector3 worldDragOffset; // ADDED: Offset in world space

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            Debug.LogWarning("CardDragHandler requires a CanvasGroup component on the card prefab root. Adding one.");
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        rootCanvas = GetComponentInParent<Canvas>();
        gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("CardDragHandler could not find GameManager in the scene!");
        }

        // Ensure originalScale is initialized if Awake runs before first CombatManager layout
        originalScale = rectTransform.localScale;

        // --- ADDED: Initialize target state to current state ---
        targetPosition = rectTransform.localPosition;
        targetRotation = rectTransform.localRotation;
        targetScale = rectTransform.localScale;
        currentNeighborOffset = Vector3.zero;
        // --- END ADDED ---

        isHovering = false;
        isDiscarding = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isDiscarding) return; // Don't allow dragging during discard animation
        
        Debug.Log($"CardDragHandler.OnBeginDrag on {cardData.cardName}");
        startPosition = rectTransform.position; // Store initial screen position
        
        // --- ADDED: Ensure originalParent is valid before starting drag ---
        if (originalParent == null || !originalParent.gameObject.activeInHierarchy)
        {
            // Find the PlayerHandPanel
            Transform handPanel = transform.parent;
            if (handPanel && handPanel.name == "PlayerHandPanel")
            {
                originalParent = handPanel;
                Debug.Log($"OnBeginDrag: Using current parent {originalParent.name} as originalParent");
            }
            else
            {
                var playerHandPanel = GameObject.Find("PlayerHandPanel");
                if (playerHandPanel != null)
                {
                    originalParent = playerHandPanel.transform;
                    Debug.Log($"OnBeginDrag: Found hand panel: {originalParent.name}");
                }
                else
                {
                    Debug.LogError($"OnBeginDrag: Could not find PlayerHandPanel! Using fallback.");
                    // Use canvas as fallback
                    originalParent = rootCanvas.transform;
                }
            }
        }
        else
        {
            Debug.Log($"OnBeginDrag: Using cached originalParent: {originalParent.name}");
        }
        // --- END ADDED ---
        
        originalSiblingIndex = transform.GetSiblingIndex(); // Remember layer order

        // Reparent to canvas root so it renders above everything
        transform.SetParent(rootCanvas.transform, true);
        transform.SetAsLastSibling(); // Ensure it's on top while dragging
        
        // Set dragging flag
        isDragging = true;
        
        // --- ADDED: Reset rotation to upright for drag ---
        rectTransform.localRotation = Quaternion.identity;
        // --- END ADDED ---

        canvasGroup.blocksRaycasts = false; // Prevent it blocking raycasts for drop zones

        // --- ADDED: Notify Hover Manager that dragging started ---
        // This is important so neighbours don't immediately try to push the dragged card
        HandPanelHoverManager hoverManager = originalParent?.GetComponent<HandPanelHoverManager>();
        hoverManager?.OnPointerExit(eventData); // Simulate pointer leaving panel to reset hover state
        // --- END ADDED ---
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rectTransform == null) return;
        
        // --- MODIFIED: Use World Space Offset --- 
        Vector3 currentWorldPoint = Vector3.zero;
        Camera eventCamera = rootCanvas.worldCamera ?? Camera.main; // Use canvas camera or fallback
        
        if (eventCamera != null)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(rootCanvas.transform as RectTransform, eventData.position, eventCamera, out currentWorldPoint);
             rectTransform.position = currentWorldPoint + worldDragOffset; // Set world position
        }
        // --- END MODIFIED ---

        // --- OLD SCREEN SPACE OFFSET METHOD ---
        /*
        // Calculate the target screen position
        Vector2 targetScreenPoint = eventData.position + dragOffset;
        
        // Convert target screen position to local point in the canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootCanvas.transform as RectTransform, targetScreenPoint, rootCanvas.worldCamera, out Vector2 localPoint);
        
        // Set the local position
        rectTransform.localPosition = localPoint;
        */
        // --- END OLD METHOD ---
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        //Debug.Log($"CardDragHandler.OnEndDrag fired. Pointer entered: {(eventData.pointerEnter != null ? eventData.pointerEnter.name : "null")}");

        canvasGroup.blocksRaycasts = true;
        isDragging = false; // Reset dragging flag
        
        // --- ADDED: Check for enemy test mode ---
        if (gameManager != null && gameManager.IsDebugEnemyTestMode())
        {
            // In enemy test mode, this card will be played by the opponent pet
            Debug.Log($"[ENEMY TEST MODE] Opponent pet will play card: {cardData.cardName}");
            
            // Start the simulation coroutine and snap card back to original position
            gameManager.StartCoroutine(SimulateOpponentPetCardPlay(cardData, gameObject));
            
            // Snap card back to original position after triggering effect
            transform.SetParent(originalParent, true);
            StopCoroutineIfRunning(ref animationCoroutine);
            rectTransform.localPosition = originalPosition;
            rectTransform.localRotation = originalRotation;
            rectTransform.localScale = originalScale;
            targetPosition = originalPosition;
            targetRotation = originalRotation;
            targetScale = originalScale;
            currentNeighborOffset = Vector3.zero;
            
            // Force reset of hover states for all cards
            HandPanelHoverManager hoverManager = originalParent?.GetComponent<HandPanelHoverManager>();
            hoverManager?.ResetAllHovers();
            
            // Restore original card description
            if (gameManager.GetCardPreviewCalculator() != null)
            {
                gameManager.GetCardPreviewCalculator().RestoreOriginalDescription(gameObject);
            }
            
            return; // Exit early since we've handled the card in enemy test mode
        }
        // --- END ADDED ---
        
        // --- ADDED: Ensure originalParent still exists ---
        if (originalParent == null || !originalParent.gameObject.activeInHierarchy)
        {
            Debug.LogError($"Card {cardData.cardName} has invalid originalParent! Finding PlayerHandPanel again.", this.gameObject);
            // Try to find the hand panel in the scene
            var handPanel = GameObject.Find("PlayerHandPanel");
            if (handPanel != null)
            {
                originalParent = handPanel.transform;
                Debug.Log($"Found hand panel: {originalParent.name}");
            }
            else 
            {
                Debug.LogError("Could not find PlayerHandPanel! Card will be lost.", this.gameObject);
                // Find the canvas as a last resort
                var canvas = FindObjectOfType<Canvas>();
                if (canvas != null)
                {
                    originalParent = canvas.transform;
                    Debug.Log($"Fallback to canvas: {originalParent.name}");
                }
            }
        }
        // --- END ADDED ---
        
        // --- MODIFIED: Save current parent for reference ---
        Transform currentParent = transform.parent;
        // --- END MODIFIED ---
        
        // --- MODIFIED: Reparenting is handled differently now based on play success ---
        // transform.SetParent(originalParent, true); // Return to hand panel first

        bool playedSuccessfully = false;
        if (eventData.pointerEnter != null && eventData.pointerEnter.TryGetComponent<CardDropZone>(out CardDropZone dropZone))
        {
            if (gameManager != null)
            {
                // --- MODIFIED: Pass the dragged GameObject (eventData.pointerDrag) to AttemptPlayCard ---
                playedSuccessfully = gameManager.AttemptPlayCard(cardData, dropZone.targetType, eventData.pointerDrag);
            }
        }

        // --- MODIFIED: Handle reparenting based on play success ---
        if (!playedSuccessfully)
        {
            // Card was NOT played, return it to the hand panel
            transform.SetParent(originalParent, true); // Return to hand panel

            // --- MODIFIED: Additional logging and robustness checks --- 
            Debug.Log($"Card {cardData.cardName} was not played. Snapping back to original position/rotation/scale.", this.gameObject);
            
            // Ensure we have valid original transform values
            if (originalPosition == Vector3.zero && originalScale == Vector3.zero)
            {
                Debug.LogWarning($"Card {cardData.cardName} has invalid original transform values. Resetting to defaults.", this.gameObject);
                originalPosition = new Vector3(0, -100, 0); // Default position in panel
                originalRotation = Quaternion.identity;
                originalScale = Vector3.one * 2f; // Default scale for cards in this game
            }
            
            // --- END MODIFIED ---

            // --- MODIFIED: Snap back instantly, ensure correct sibling index ---
            StopCoroutineIfRunning(ref animationCoroutine); // Stop self-animation
            rectTransform.localPosition = originalPosition;
            rectTransform.localRotation = originalRotation;
            rectTransform.localScale = originalScale;

            // --- ADDED: Debug log for snap back values ---
            Debug.Log($"Card {cardData.cardName} snapping back. Original Pos: {originalPosition}, Rot: {originalRotation.eulerAngles}, Scale: {originalScale}. Current Pos: {rectTransform.localPosition}, Rot: {rectTransform.localRotation.eulerAngles}, Scale: {rectTransform.localScale}", this.gameObject);
            // --- END ADDED ---

            // Reset target state as well
            targetPosition = originalPosition;
            targetRotation = originalRotation;
            targetScale = originalScale;
            currentNeighborOffset = Vector3.zero;
            
            // --- REMOVED Sibling Index Restoration from Drag End --- 
            /*
            if(originalParent != null && originalSiblingIndex >= 0 && originalSiblingIndex < originalParent.childCount)
            {
                transform.SetSiblingIndex(originalSiblingIndex);
            }
            else
            {
                // If index is invalid, maybe just set as last for safety?
                if(originalParent != null) transform.SetAsLastSibling();
            }
            */
            // --- END REMOVED ---
            
            // --- ADDED: Force reset of hover states for all cards ---
            HandPanelHoverManager hoverManager = originalParent?.GetComponent<HandPanelHoverManager>();
            hoverManager?.ResetAllHovers();
            // --- END ADDED ---
        }
        else
        {
            // Card WAS played successfully.
            // The AttemptPlayCard -> DiscardCard -> TriggerDiscardAnimation path will now handle
            // animating this specific GameObject (eventData.pointerDrag).
            // We do NOT reparent it back to the hand panel here, as the animation will handle its lifecycle.
            Debug.Log($"Card {cardData.cardName} played successfully. Discard animation should handle this GameObject.", this.gameObject);
        }
        // If played successfully, GameManager's UpdateHandUI will handle removal/re-layout

        // --- ADDED: Restore original card description ---
        if (gameManager != null && gameManager.GetCardPreviewCalculator() != null)
        {
            gameManager.GetCardPreviewCalculator().RestoreOriginalDescription(gameObject);
        }
        // --- END ADDED ---
    }

    // --- ADDED: Public Hover Methods (called by HandPanelHoverManager) ---
    public void EnterHoverState()
    {
        // --- ADDED: Add null check for originalParent ---
        if (transform.parent == null)
        {
            Debug.LogWarning($"EnterHoverState called on {name} with null parent! Skipping hover.", this.gameObject);
            return;
        }
        // --- END ADDED ---
        
        if (canvasGroup.blocksRaycasts == false || isHovering) return;
        //Debug.Log($"[{Time.frameCount}] EnterHoverState START: {name}");
        isHovering = true;

        // Store index only if not already stored (might happen if quickly re-hovering)
        if(originalSiblingIndex < 0) 
        {
            originalSiblingIndex = transform.GetSiblingIndex();
           // Debug.Log($"[{Time.frameCount}] EnterHoverState: Stored originalSiblingIndex = {originalSiblingIndex}");
        }

        // --- MODIFIED: Use Animation --- 
        currentNeighborOffset = Vector3.zero; // Reset any neighbor offset
        
        // Calculate target state
        targetPosition = originalPosition + Vector3.up * hoverOffsetY;
        targetRotation = originalRotation; // Keep original rotation
        targetScale = originalScale * hoverScaleFactor;

        // Start animation towards target & bring to front
        transform.SetAsLastSibling(); // Bring to front immediately
        UpdateAnimationTargetAndStart(); // Start animation
        
        // Start neighbor animations
        AffectNeighbors(neighborOffsetX, true);
    }

    public void ExitHoverState()
    {
        // --- ADDED: Add null check for originalParent ---
        if (transform.parent == null)
        {
            Debug.LogWarning($"ExitHoverState called on {name} with null parent! Skipping hover exit.", this.gameObject);
            return;
        }
        // --- END ADDED ---
        
        if (!isHovering || canvasGroup.blocksRaycasts == false) return;
        //Debug.Log($"[{Time.frameCount}] ExitHoverState START: {name}");
        isHovering = false;

        // --- MODIFIED: Use Animation --- 
        currentNeighborOffset = Vector3.zero; // Reset any neighbor offset

        // Target state is the original state
        targetPosition = originalPosition;
        targetRotation = originalRotation;
        targetScale = originalScale;

        // Start animation back towards target & restore layer order
        UpdateAnimationTargetAndStart(); // Just animate, layer order restored at end of coroutine

        // Start neighbor animations back to original
        AffectNeighbors(0f, false);
        // --- END MODIFIED ---
        
        // originalSiblingIndex = -1; // <-- MOVE THIS LINE
    }
    
    // --- ADDED: Helper to manage animation start/stop ---
    private void UpdateAnimationTargetAndStart()
    {
        //Debug.Log($"[{Time.frameCount}] UpdateAnimationTargetAndStart: Stopping existing coroutine for {name}");
        // Stop existing animation
        StopCoroutineIfRunning(ref animationCoroutine);
        // Start new one
        //Debug.Log($"[{Time.frameCount}] UpdateAnimationTargetAndStart: Starting AnimateTransformCoroutine for {name}. TargetPos={targetPosition}, TargetScale={targetScale}");
        animationCoroutine = StartCoroutine(AnimateTransformCoroutine());
    }
    
    // --- MODIFIED: Coroutine animates towards target fields ---
    private IEnumerator AnimateTransformCoroutine()
    {
        float elapsedTime = 0f;
        Vector3 startPosition = rectTransform.localPosition;
        Quaternion startRotation = rectTransform.localRotation;
        Vector3 startScale = rectTransform.localScale;

        while (elapsedTime < hoverAnimDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / hoverAnimDuration);
            // Smoothstep for nicer easing (optional)
            t = t * t * (3f - 2f * t);

            rectTransform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
            rectTransform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            rectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        // Ensure final state is set precisely
        rectTransform.localPosition = targetPosition;
        rectTransform.localRotation = targetRotation;
        rectTransform.localScale = targetScale;

        // --- MODIFIED: Layering Handling at End of Animation ---
        // Layering is now handled by HandPanelHoverManager.ResortSiblingIndices()
        // Coroutine just ensures the final transform state is set.
        
        animationCoroutine = null; // Mark as finished
        // --- END MODIFIED ---
    }
    
    // --- MODIFIED: AffectNeighbors calls ApplyNeighborOffset ---
    private void AffectNeighbors(float horizontalOffset, bool enteringHover)
    {
        // --- ADDED: Check parent ---
        if (transform.parent == null)
        {
            Debug.LogWarning($"AffectNeighbors on {name} - Parent is null, cannot find neighbors!");
            return;
        }
        // --- END ADDED ---
        
        CardDragHandler leftNeighbor, rightNeighbor;
        FindNeighbors(out leftNeighbor, out rightNeighbor);

        // Animate Left Neighbor
        if (leftNeighbor != null)
        {
            // Apply offset (positive offset means move left)
            leftNeighbor.ApplyNeighborOffset(Vector3.left * horizontalOffset);
        }

        // Animate Right Neighbor
        if (rightNeighbor != null)
        {
             // Apply offset (positive offset means move right)
             rightNeighbor.ApplyNeighborOffset(Vector3.right * horizontalOffset);
        }
    }
    
    // --- ADDED: Method for neighbors to apply offset ---
    public void ApplyNeighborOffset(Vector3 offset)
    {
        // --- MODIFIED: Add check for isDragging --- 
        // Don't apply if currently hovering/being dragged or discarding
        if (isHovering || isDragging || isDiscarding || canvasGroup.blocksRaycasts == false) return; 
        // --- END MODIFIED ---
        
        // --- ADDED: Check parent ---
        if (transform.parent == null)
        {
            Debug.LogWarning($"ApplyNeighborOffset on {name} - Parent is null, cannot apply offset!");
            return;
        }
        // --- END ADDED ---
        
        currentNeighborOffset = offset;
        // Recalculate target position based on original + offset
        targetPosition = originalPosition + currentNeighborOffset;
        // Rotation and Scale remain original when offset by neighbor
        targetRotation = originalRotation;
        targetScale = originalScale;

        // Start animation towards the new target position
        UpdateAnimationTargetAndStart(); // Don't bring neighbor to front
    }
    
    // --- MODIFIED: FindNeighbors based on original X position --- 
    private void FindNeighbors(out CardDragHandler leftNeighbor, out CardDragHandler rightNeighbor)
    {   
        leftNeighbor = null;
        rightNeighbor = null;
        Transform parent = transform.parent;

        if (parent == null) 
        {   
            Debug.LogWarning($"FindNeighbors ({name}): Parent is null!");
            return;
        }

        // 1. Get all active CardDragHandler siblings
        List<CardDragHandler> siblings = parent.GetComponentsInChildren<CardDragHandler>(false) // false = don't include inactive
                                              .Where(h => h != null && h.gameObject.activeSelf && h.gameObject.name != "CardTemplate")
                                              .ToList();

        if (siblings.Count <= 1) return; // No neighbors if only one card

        // 2. Sort siblings based on their calculated original X position
        siblings.Sort((a, b) => a.originalPosition.x.CompareTo(b.originalPosition.x));

        // 3. Find the index of this card in the sorted list
        int mySortedIndex = -1;
        for (int i = 0; i < siblings.Count; i++)
        {
            if (siblings[i] == this)
            {
                mySortedIndex = i;
                break;
            }
        }

        if (mySortedIndex == -1)
        {
            Debug.LogError($"FindNeighbors ({name}): Could not find self in sorted sibling list!");
            return;
        }

        // Debug.Log($"FindNeighbors ({name}): Sorted Index={mySortedIndex}, Count={siblings.Count}");

        // 4. Identify neighbors from the sorted list
        if (mySortedIndex > 0)
        {
            leftNeighbor = siblings[mySortedIndex - 1];
            // Debug.Log($"  - Found Left Neighbor: {leftNeighbor?.name} at sorted index {mySortedIndex - 1}");
        }

        if (mySortedIndex < siblings.Count - 1)
        {
            rightNeighbor = siblings[mySortedIndex + 1];
            // Debug.Log($"  - Found Right Neighbor: {rightNeighbor?.name} at sorted index {mySortedIndex + 1}");
        }
    }
    
    // --- ADDED: Helper to stop coroutine ---
    private void StopCoroutineIfRunning(ref Coroutine coroutineRef)
    {
        // Check if the coroutine is still running on this MonoBehaviour
        if (coroutineRef != null && this != null && this.enabled)
        {
            StopCoroutine(coroutineRef);
            coroutineRef = null;
        }
    }
    
    // --- MODIFIED: Method to reset card state instantly ---
    public void ResetToOriginalStateInstantly(bool affectNeighbors = true)
    {
        // --- ADDED: Check parent for neighbor operations ---
        bool hasValidParent = transform.parent != null;
        if (!hasValidParent)
        {
            Debug.LogWarning($"ResetToOriginalStateInstantly on {name} - Parent is null, skipping neighbor operations!", this.gameObject);
        }
        // --- END ADDED ---
        
        // Debug.Log($"ResetInstant: {name}");
        StopCoroutineIfRunning(ref animationCoroutine); // Stop self-animation
        isHovering = false; // Ensure hover state is off

        // Restore original transform values
        if (rectTransform != null && originalScale != Vector3.zero) {
             rectTransform.localPosition = originalPosition;
             rectTransform.localRotation = originalRotation;
             rectTransform.localScale = originalScale;
        }

        // Reset target state
        targetPosition = originalPosition;
        targetRotation = originalRotation;
        targetScale = originalScale;
        currentNeighborOffset = Vector3.zero;

        // Optionally tell neighbours to also reset (e.g., called from HoverManager exit)
        if (affectNeighbors && hasValidParent) {  // MODIFIED: Check hasValidParent
             // Find neighbors first before potentially resetting them
             CardDragHandler leftNeighbor, rightNeighbor;
             FindNeighbors(out leftNeighbor, out rightNeighbor);
             if(leftNeighbor != null) leftNeighbor.ApplyNeighborOffset(Vector3.zero);
             if(rightNeighbor != null) rightNeighbor.ApplyNeighborOffset(Vector3.zero);
        }
        originalSiblingIndex = -1; // Reset stored index
    }
    
    // --- ADDED: OnTransformParentChanged to track parent change ---
    private void OnTransformParentChanged()
    {
        // Keep track of parent changes to help debugging
        Debug.Log($"Card {name} parent changed: {transform.parent?.name ?? "null"}", this.gameObject);
    }

    private void OnDestroy()
    {
        // Reset all flags for safety
        isDragging = false;
        isDiscarding = false;
        isHovering = false;
    }

    // --- ADDED: Explicit state reset method ---
    public void ResetState()
    {
        isHovering = false;
        isDragging = false; // Ensure this is false
        isDiscarding = false; // Ensure this is false
        StopCoroutineIfRunning(ref animationCoroutine);
        // Reset target state to match original state just set
        if (rectTransform != null) {
            targetPosition = rectTransform.localPosition;
            targetRotation = rectTransform.localRotation;
            targetScale = rectTransform.localScale;
        } else {
            targetPosition = originalPosition;
            targetRotation = originalRotation;
            targetScale = originalScale;
        }
        currentNeighborOffset = Vector3.zero;
        originalSiblingIndex = -1;
        // Make sure raycasts are enabled unless explicitly dragging/discarding
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        // Debug.Log($"[{Time.frameCount}] ResetState called for {name}");
    }
    // --- END ADDED ---

    // --- ADDED: Implement OnPointerClick for enemy test mode ---
    public void OnPointerClick(PointerEventData eventData)
    {
        // Only process click if we're in enemy test mode and not dragging/discarding
        if (gameManager != null && gameManager.IsDebugEnemyTestMode() && !isDragging && !isDiscarding)
        {
            Debug.Log($"[ENEMY TEST MODE] Card clicked: {cardData.cardName}");
            // Use the proper opponent pet card play path for consistency with normal gameplay
            gameManager.StartCoroutine(SimulateOpponentPetCardPlay(cardData, gameObject));
        }
    }
    
    // Helper method to simulate opponent pet playing a card
    private IEnumerator SimulateOpponentPetCardPlay(CardData card, GameObject cardGO)
    {
        if (gameManager == null) yield break;
        
        // Use the same visualization coroutine that normal opponent pet turns use
        CombatUIManager uiManager = gameManager.GetCombatUIManager();
        if (uiManager != null)
        {
            // This handles visual effects and animation
            yield return uiManager.VisualizeOpponentPetCardPlay(card);
        }
        
        // After visualization, process the effect (this is what happens in normal gameplay)
        gameManager.GetCardManager().ProcessOpponentPetCardEffect(card, cardGO);
        
        // Update UI after processing
        gameManager.UpdateHealthUI();
    }
} 