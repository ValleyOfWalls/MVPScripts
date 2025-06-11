using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Handles drag and drop functionality for cards during combat.
/// Attach to: Card prefabs alongside Card, HandleCardPlay, and other card components.
/// </summary>
public class CardDragDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Drag Settings")]
    [SerializeField] private float dragThreshold = 15f; // Minimum distance to start drag (increased for more reliable detection)
    [SerializeField] private Canvas dragCanvas; // Canvas to parent card to during drag
    [SerializeField] private int dragSortingOrder = 1000; // Sorting order during drag
    
    [Header("Visual Feedback")]
    [SerializeField] private float dragScale = 1.1f; // Scale factor during drag
    [SerializeField] private float dragAlpha = 0.8f; // Alpha during drag
    
    [Header("Debug")]
    [SerializeField] private bool debugLogEnabled = false;
    
    // Components
    private Card card;
    private HandleCardPlay handleCardPlay;
    private SourceAndTargetIdentifier sourceAndTargetIdentifier;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas originalCanvas;
    
    // Drag state
    private bool isDragging = false;
    private bool isDragStarted = false;
    private Vector2 dragStartPosition;
    private Vector2 pointerDownPosition;
    private Transform originalParent;
    private int originalSortingOrder;
    private Vector3 originalScale;
    private float originalAlpha;
    private DropZone currentHoverZone;
    private DropZone validDropZone;
    
    // Click detection
    private bool pointerDown = false;
    private float pointerDownTime;
    private const float maxClickTime = 0.5f; // Max time for a click vs drag (increased)
    private bool eventHandled = false; // Prevent double handling
    
    private void Awake()
    {
        // Get required components
        card = GetComponent<Card>();
        handleCardPlay = GetComponent<HandleCardPlay>();
        sourceAndTargetIdentifier = GetComponent<SourceAndTargetIdentifier>();
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        
        // Add CanvasGroup if missing
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        // Find drag canvas with improved logic
        if (dragCanvas == null)
        {
            dragCanvas = FindSuitableDragCanvas();
        }
        
        ValidateComponents();
    }
    
    /// <summary>
    /// Finds a suitable canvas for dragging with improved client/host compatibility
    /// </summary>
    private Canvas FindSuitableDragCanvas()
    {
        Canvas foundCanvas = null;
        
        // Strategy 1: Try to find the canvas that contains the card UI
        Canvas currentCanvas = GetComponentInParent<Canvas>();
        if (currentCanvas != null)
        {
            // Check if this canvas is suitable for dragging (has GraphicRaycaster)
            GraphicRaycaster raycaster = currentCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                foundCanvas = currentCanvas;
            }
            else
            {
                // Try the root canvas in the hierarchy
                Canvas rootCanvas = currentCanvas;
                while (rootCanvas.transform.parent != null)
                {
                    Canvas parentCanvas = rootCanvas.transform.parent.GetComponentInParent<Canvas>();
                    if (parentCanvas != null)
                        rootCanvas = parentCanvas;
                    else
                        break;
                }
                
                if (rootCanvas != currentCanvas && rootCanvas.GetComponent<GraphicRaycaster>() != null)
                {
                    foundCanvas = rootCanvas;
                }
            }
        }
        
        // Strategy 2: Find main UI canvas by common names
        if (foundCanvas == null)
        {
            string[] commonCanvasNames = { "Canvas", "MainCanvas", "UICanvas", "GameCanvas" };
            foreach (string canvasName in commonCanvasNames)
            {
                GameObject canvasObj = GameObject.Find(canvasName);
                if (canvasObj != null)
                {
                    Canvas canvas = canvasObj.GetComponent<Canvas>();
                    if (canvas != null && canvas.GetComponent<GraphicRaycaster>() != null)
                    {
                        foundCanvas = canvas;
                        break;
                    }
                }
            }
        }
        
        // Strategy 3: Find any suitable canvas with GraphicRaycaster
        if (foundCanvas == null)
        {
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (Canvas canvas in allCanvases)
            {
                // Look for a canvas with GraphicRaycaster that's in world space or screen space overlay
                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null && (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.renderMode == RenderMode.WorldSpace))
                {
                    foundCanvas = canvas;
                    break;
                }
            }
        }
        
        // Strategy 4: Use current parent canvas as fallback even without GraphicRaycaster
        if (foundCanvas == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                foundCanvas = parentCanvas;
            }
        }
        
        if (foundCanvas == null)
        {
            Debug.LogError("Could not find any suitable drag canvas!");
        }
        
        return foundCanvas;
    }
    
    private void ValidateComponents()
    {
        if (card == null)
            Debug.LogError($"CardDragDrop on {gameObject.name}: Missing Card component!");
        
        if (handleCardPlay == null)
            Debug.LogError($"CardDragDrop on {gameObject.name}: Missing HandleCardPlay component!");
        
        if (rectTransform == null)
            Debug.LogError($"CardDragDrop on {gameObject.name}: Missing RectTransform component!");
        
        // Ensure proper UI event setup
        EnsureUIEventSetup();
    }
    
    /// <summary>
    /// Ensures the card is properly set up for UI events and disables conflicting systems
    /// </summary>
    private void EnsureUIEventSetup()
    {
        // Keep mouse collider enabled for hover effects (damage previews), but ensure UI events take priority
        BoxCollider2D mouseCollider = GetComponent<BoxCollider2D>();
        if (mouseCollider != null)
        {
            mouseCollider.enabled = true; // Keep enabled for OnMouseEnter/Exit damage previews
        }
        
        // Ensure we're on a Canvas that can receive UI events
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            Debug.LogWarning("No parent Canvas found - UI events may not work properly");
        }
        else
        {
            // Ensure the canvas has a GraphicRaycaster
            GraphicRaycaster raycaster = parentCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = parentCanvas.gameObject.AddComponent<GraphicRaycaster>();
                LogDebug("Added GraphicRaycaster to parent Canvas");
            }
        }
        
        // Ensure CanvasGroup is properly configured
        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            LogDebug("Configured CanvasGroup for UI interactions");
        }
        
        LogDebug("UI event setup complete");
    }
    
    /// <summary>
    /// Checks if the card can be dragged based on game state
    /// </summary>
    private bool CanDragCard()
    {
        // Don't allow dragging in draft phase
        GamePhaseManager gamePhaseManager = GamePhaseManager.Instance;
        if (gamePhaseManager != null && gamePhaseManager.GetCurrentPhase() == GamePhaseManager.GamePhase.Draft)
        {
            LogDebug("Cannot drag card - in draft phase");
            return false;
        }
        
        // Check if card is in hand
        if (card.CurrentContainer != CardLocation.Hand)
        {
            LogDebug($"Cannot drag card - not in hand (current: {card.CurrentContainer})");
            return false;
        }
        
        // Check if card can be played by local player
        if (!card.CanBePlayedByLocalPlayer())
        {
            LogDebug("Cannot drag card - cannot be played by local player");
            return false;
        }
        
        // Update source and target before checking play requirements (like Card.OnMouseDown does)
        if (sourceAndTargetIdentifier != null)
        {
            LogDebug("Updating source and target before drag validation");
            sourceAndTargetIdentifier.UpdateSourceAndTarget();
        }
        
        // Check if card meets basic play requirements (energy, stun, etc.)
        if (handleCardPlay != null && !handleCardPlay.CanCardBePlayed())
        {
            LogDebug($"Cannot drag card - play requirements not met: {handleCardPlay.GetPlayBlockReason()}");
            return false;
        }
        
        return true;
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventHandled)
        {
            LogDebug("Pointer down ignored - event already handled");
            return;
        }
        
        pointerDown = true;
        pointerDownTime = Time.time;
        pointerDownPosition = eventData.position;
        dragStartPosition = eventData.position;
        isDragStarted = false;
        eventHandled = false;
        
        LogDebug($"Pointer down detected at {eventData.position}");
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pointerDown)
        {
            LogDebug("Pointer up ignored - no matching pointer down");
            return;
        }
        
        if (!isDragStarted && !eventHandled)
        {
            // This was a click, not a drag
            float clickDuration = Time.time - pointerDownTime;
            Vector2 totalMovement = eventData.position - pointerDownPosition;
            
            LogDebug($"Checking click: duration={clickDuration:F2}s, movement={totalMovement.magnitude:F1}px, threshold={dragThreshold}");
            
            if (clickDuration <= maxClickTime && totalMovement.magnitude < dragThreshold)
            {
                LogDebug("Click detected - handling as card click");
                eventHandled = true;
                HandleCardClick();
            }
            else
            {
                LogDebug($"Not a click - duration too long ({clickDuration:F2}s > {maxClickTime:F2}s) or movement too large ({totalMovement.magnitude:F1}px > {dragThreshold}px)");
            }
        }
        
        // Reset state
        pointerDown = false;
        isDragStarted = false;
        
        // Reset eventHandled after a frame to allow for new interactions
        StartCoroutine(ResetEventHandledAfterFrame());
    }
    
    private System.Collections.IEnumerator ResetEventHandledAfterFrame()
    {
        yield return null; // Wait one frame
        eventHandled = false;
    }
    
    private void HandleCardClick()
    {
        LogDebug("Handling card click - calling card.OnMouseDown()");
        
        // Directly call the card play logic instead of OnMouseDown to avoid recursion
        if (card != null)
        {
            // We'll implement the card play logic here similar to Card.OnMouseDown but without conflicts
            TryPlayCard();
        }
    }
    
    private void TryPlayCard()
    {
        // Check the current game phase to determine container requirements
        GamePhaseManager gamePhaseManager = GamePhaseManager.Instance;
        bool isDraftPhase = gamePhaseManager != null && gamePhaseManager.GetCurrentPhase() == GamePhaseManager.GamePhase.Draft;
        
        LogDebug($"TryPlayCard - isDraftPhase: {isDraftPhase}, IsDraftable: {card.IsDraftable}, CurrentContainer: {card.CurrentContainer}");
        
        // First check if card is in the correct container
        if (!isDraftPhase && card.CurrentContainer != CardLocation.Hand) 
        {
            LogDebug($"Cannot play - not in hand (current: {card.CurrentContainer})");
            return;
        }
        else if (isDraftPhase && !card.IsDraftable)
        {
            LogDebug("Cannot select in draft - card is not draftable");
            return;
        }
        else if (isDraftPhase && card.IsDraftable)
        {
            LogDebug("Draft phase detected - delegating to DraftCardSelection");
            
            // For draft cards, delegate to the DraftCardSelection component
            DraftCardSelection draftSelection = GetComponent<DraftCardSelection>();
            if (draftSelection != null)
            {
                draftSelection.OnCardClicked();
            }
            else
            {
                Debug.LogError("Draft card but no DraftCardSelection component found!");
            }
            return;
        }
        
        // Check if card can be played by local player
        if (!card.CanBePlayedByLocalPlayer())
        {
            LogDebug("Cannot play - card cannot be played by local player");
            return;
        }
        
        // Update source and target before checking play requirements (like Card.OnMouseDown does)
        if (sourceAndTargetIdentifier != null)
        {
            LogDebug("Updating source and target before play validation");
            sourceAndTargetIdentifier.UpdateSourceAndTarget();
        }
        
        // Check if card meets basic play requirements
        if (handleCardPlay != null && !handleCardPlay.CanCardBePlayed())
        {
            LogDebug($"Cannot play - requirements not met: {handleCardPlay.GetPlayBlockReason()}");
            return;
        }
        
        // Update source and target before playing
        if (sourceAndTargetIdentifier != null)
        {
            sourceAndTargetIdentifier.UpdateSourceAndTarget();
        }
        
        // Play the card
        if (handleCardPlay != null)
        {
            LogDebug("Playing card via click");
            handleCardPlay.OnCardPlayAttempt();
            
            // Notify SourceAndTargetIdentifier that card was played for cleanup
            if (sourceAndTargetIdentifier != null)
            {
                sourceAndTargetIdentifier.OnCardPlayedOrDiscarded();
            }
        }
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        LogDebug($"OnBeginDrag called - eventHandled: {eventHandled}, isDragStarted: {isDragStarted}");
        
        // Check if event was already handled or if this is too soon after pointer down
        if (eventHandled)
        {
            LogDebug("Begin drag cancelled - event already handled");
            eventData.pointerDrag = null;
            return;
        }
        
        if (!CanDragCard())
        {
            LogDebug("Begin drag cancelled - cannot drag card");
            eventData.pointerDrag = null;
            return;
        }
        
        Vector2 dragDistance = eventData.position - dragStartPosition;
        LogDebug($"Begin drag check: distance={dragDistance.magnitude:F1}px, threshold={dragThreshold}px");
        
        if (dragDistance.magnitude < dragThreshold)
        {
            LogDebug("Begin drag cancelled - below threshold distance");
            return; // Don't start drag until threshold is met
        }
        
        // Mark that we've started dragging to prevent click events
        isDragStarted = true;
        isDragging = true;
        eventHandled = true; // Prevent click handling
        
        LogDebug("Begin drag - starting drag operation");
        
        // Notify SourceAndTargetIdentifier that drag is starting to keep damage previews visible
        if (sourceAndTargetIdentifier != null)
        {
            sourceAndTargetIdentifier.OnDragStart();
        }
        
        // Check if we need to refresh drag canvas (can be null on clients or after scene changes)
        if (dragCanvas == null)
        {
            LogDebug("Drag canvas is null - attempting to find suitable canvas");
            dragCanvas = FindSuitableDragCanvas();
        }
        
        // Store original state
        originalParent = transform.parent;
        originalScale = transform.localScale;
        originalAlpha = canvasGroup.alpha;
        
        // Get original canvas and sorting order
        originalCanvas = GetComponentInParent<Canvas>();
        if (originalCanvas != null)
        {
            originalSortingOrder = originalCanvas.sortingOrder;
        }
        
        // UPDATED: Don't reparent - just use Canvas component for proper rendering order
        LogDebug("Maintaining original parent during drag - using Canvas component for rendering order");
        
        // Add Canvas component to card for higher sorting order
        Canvas cardCanvas = GetComponent<Canvas>();
        if (cardCanvas == null)
        {
            cardCanvas = gameObject.AddComponent<Canvas>();
        }
        cardCanvas.overrideSorting = true;
        cardCanvas.sortingOrder = dragSortingOrder;
        
        LogDebug($"Added Canvas component with sorting order: {dragSortingOrder}, staying in parent: {originalParent.name}");
        
        // Apply drag visual effects
        transform.localScale = originalScale * dragScale;
        canvasGroup.alpha = dragAlpha;
        canvasGroup.blocksRaycasts = false; // Allow raycasts to pass through to drop zones
        
        LogDebug($"Applied drag visuals - scale: {transform.localScale}, alpha: {canvasGroup.alpha}");
        
        // Update source and target for targeting validation
        if (sourceAndTargetIdentifier != null)
        {
            sourceAndTargetIdentifier.UpdateSourceAndTarget();
        }
        
        LogDebug("Drag start complete - ready for dragging");
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) 
        {
            LogDebug("OnDrag called but isDragging is false - checking if we should start drag");
            // Check if we should start dragging now
            Vector2 dragDistance = eventData.position - dragStartPosition;
            if (dragDistance.magnitude >= dragThreshold && !isDragStarted)
            {
                LogDebug($"Drag threshold met during OnDrag - attempting to start drag (distance: {dragDistance.magnitude:F1}px)");
                OnBeginDrag(eventData);
            }
            return;
        }
        
        // Move card to follow pointer
        if (rectTransform != null)
        {
            Vector2 localPoint;
            
            // Use the appropriate parent transform for screen-to-local conversion
            Transform referenceTransform = originalParent != null ? originalParent : transform.parent;
            RectTransform referenceRect = referenceTransform as RectTransform;
            
            if (referenceRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                referenceRect, 
                eventData.position, 
                eventData.pressEventCamera, 
                out localPoint))
            {
                rectTransform.localPosition = localPoint;
            }
            else
            {
                // Fallback: try to use dragCanvas if available
                if (dragCanvas != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragCanvas.transform as RectTransform, 
                    eventData.position, 
                    eventData.pressEventCamera, 
                    out localPoint))
                {
                    // Convert from drag canvas local space to our parent's local space
                    Vector3 worldPoint = dragCanvas.transform.TransformPoint(localPoint);
                    localPoint = referenceRect.InverseTransformPoint(worldPoint);
                    rectTransform.localPosition = localPoint;
                }
            }
        }
        
        // Check for drop zones under the pointer
        CheckForDropZones(eventData);
    }
    
    private void CheckForDropZones(PointerEventData eventData)
    {
        // First try UI raycasting for backwards compatibility
        var raycastResults = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, raycastResults);
        
        LogDebug($"UI Raycast found {raycastResults.Count} UI elements at {eventData.position}");
        
        DropZone newHoverZone = null;
        
        // Check UI elements first
        for (int i = 0; i < raycastResults.Count; i++)
        {
            var result = raycastResults[i];
            LogDebug($"  UI Hit {i}: {result.gameObject.name} (parent: {(result.gameObject.transform.parent?.name ?? "none")})");
            
            // Check for DropZone on this GameObject
            DropZone dropZone = result.gameObject.GetComponent<DropZone>();
            if (dropZone != null)
            {
                newHoverZone = dropZone;
                LogDebug($"  --> Found DropZone on UI element {result.gameObject.name}!");
                break;
            }
            else
            {
                // Also check parent for DropZone (in case we hit the child Image)
                DropZone parentDropZone = result.gameObject.GetComponentInParent<DropZone>();
                if (parentDropZone != null)
                {
                    newHoverZone = parentDropZone;
                    LogDebug($"  --> Found DropZone on parent {parentDropZone.gameObject.name} of {result.gameObject.name}!");
                    break;
                }
            }
        }
        
        // If no UI DropZone found, try 3D physics raycasting
        if (newHoverZone == null)
        {
            newHoverZone = CheckFor3DDropZones(eventData);
        }
        
        // Handle zone changes
        if (newHoverZone != currentHoverZone)
        {
            // Exit previous zone
            if (currentHoverZone != null)
            {
                currentHoverZone.OnCardDragExit();
                LogDebug($"Exited drop zone: {currentHoverZone.gameObject.name}");
            }
            
            // Enter new zone
            currentHoverZone = newHoverZone;
            if (currentHoverZone != null)
            {
                NetworkEntity targetEntity = currentHoverZone.GetTargetEntity();
                LogDebug($"Found DropZone: {currentHoverZone.gameObject.name}");
                LogDebug($"Target entity from DropZone: {targetEntity?.EntityName.Value ?? "null"}");
                
                bool canTarget = targetEntity != null && CanTargetEntity(targetEntity);
                LogDebug($"Can target entity: {canTarget}");
                
                if (!canTarget && targetEntity != null)
                {
                    // Debug why targeting failed
                    var validTargetTypes = card?.CardData?.GetValidTargetTypes();
                    CardTargetType entityTargetType = GetEntityTargetType(targetEntity);
                    LogDebug($"Card valid target types: {(validTargetTypes != null ? string.Join(", ", validTargetTypes) : "null")}");
                    LogDebug($"Entity target type: {entityTargetType}");
                }
                
                currentHoverZone.OnCardDragEnter(this, canTarget);
                LogDebug($"Entered drop zone: {currentHoverZone.gameObject.name}, canTarget: {canTarget}");
                
                // Set as valid drop zone if we can target it
                validDropZone = canTarget ? currentHoverZone : null;
                LogDebug($"Valid drop zone set to: {(validDropZone != null ? validDropZone.gameObject.name : "null")}");
            }
            else
            {
                validDropZone = null;
                LogDebug("No drop zone under cursor");
            }
        }
    }
    
    /// <summary>
    /// Checks for 3D colliders with DropZone components using physics raycasting
    /// </summary>
    private DropZone CheckFor3DDropZones(PointerEventData eventData)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            // Try to find any camera if main camera is not available
            camera = FindObjectOfType<Camera>();
        }
        
        if (camera == null)
        {
            LogDebug("No camera found for 3D raycasting");
            return null;
        }
        
        // Convert screen position to world ray
        Ray ray = camera.ScreenPointToRay(eventData.position);
        
        // Perform 3D raycast
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);
        
        LogDebug($"3D Raycast found {hits.Length} 3D colliders at {eventData.position}");
        
        // Sort hits by distance (closest first)
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        
        // Check each hit for DropZone component
        foreach (var hit in hits)
        {
            LogDebug($"  3D Hit: {hit.collider.gameObject.name} at distance {hit.distance:F2}");
            LogDebug($"    Collider type: {hit.collider.GetType().Name}");
            LogDebug($"    GameObject layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
            LogDebug($"    GameObject tag: {hit.collider.gameObject.tag}");
            
            // List all components on the hit GameObject for debugging
            Component[] components = hit.collider.GetComponents<Component>();
            LogDebug($"    Components on {hit.collider.gameObject.name}: {string.Join(", ", System.Array.ConvertAll(components, c => c.GetType().Name))}");
            
            // Check for DropZone on the hit GameObject
            DropZone dropZone = hit.collider.GetComponent<DropZone>();
            if (dropZone != null)
            {
                LogDebug($"  --> Found DropZone component on 3D object {hit.collider.gameObject.name}!");
                LogDebug($"      DropZone target entity: {dropZone.GetTargetEntity()?.EntityName.Value ?? "null"}");
                return dropZone;
            }
            else
            {
                LogDebug($"    No DropZone component on {hit.collider.gameObject.name}");
            }
            
            // Also check parent for DropZone
            DropZone parentDropZone = hit.collider.GetComponentInParent<DropZone>();
            if (parentDropZone != null)
            {
                LogDebug($"  --> Found DropZone on parent {parentDropZone.gameObject.name} of 3D object {hit.collider.gameObject.name}!");
                LogDebug($"      Parent DropZone target entity: {parentDropZone.GetTargetEntity()?.EntityName.Value ?? "null"}");
                return parentDropZone;
            }
            else
            {
                LogDebug($"    No DropZone component in parents of {hit.collider.gameObject.name}");
            }
            
            // Also check children for DropZone (in case the collider is a child of the DropZone)
            DropZone childDropZone = hit.collider.GetComponentInChildren<DropZone>();
            if (childDropZone != null)
            {
                LogDebug($"  --> Found DropZone on child {childDropZone.gameObject.name} of 3D object {hit.collider.gameObject.name}!");
                LogDebug($"      Child DropZone target entity: {childDropZone.GetTargetEntity()?.EntityName.Value ?? "null"}");
                return childDropZone;
            }
            else
            {
                LogDebug($"    No DropZone component in children of {hit.collider.gameObject.name}");
            }
        }
        
        if (hits.Length == 0)
        {
            LogDebug("No 3D colliders hit by raycast");
        }
        else
        {
            LogDebug("3D colliders found but no DropZone components detected");
        }
        
        return null;
    }
    
    private bool CanTargetEntity(NetworkEntity targetEntity)
    {
        if (card?.CardData == null || targetEntity == null)
            return false;
        
        // Get the card's target types
        var validTargetTypes = card.CardData.GetValidTargetTypes();
        if (validTargetTypes.Count == 0)
            return false;
        
        // Determine what type this entity represents
        CardTargetType entityTargetType = GetEntityTargetType(targetEntity);
        
        // Check if any of the card's valid targets match this entity
        return validTargetTypes.Contains(entityTargetType);
    }
    
    private CardTargetType GetEntityTargetType(NetworkEntity entity)
    {
        if (entity == null) return CardTargetType.Opponent;
        
        // Get the source entity (card owner)
        NetworkEntity sourceEntity = sourceAndTargetIdentifier?.SourceEntity;
        if (sourceEntity == null) return CardTargetType.Opponent;
        
        // Check if entity is self
        if (entity == sourceEntity)
        {
            return CardTargetType.Self;
        }
        
        // Use FightManager to determine fight relationships
        FightManager fightManager = FightManager.Instance;
        if (fightManager == null) return CardTargetType.Opponent;
        
        // Check if entity is the source's opponent in the current fight
        NetworkEntity sourceOpponent = null;
        if (sourceEntity.EntityType == EntityType.Player)
        {
            sourceOpponent = fightManager.GetOpponentForPlayer(sourceEntity);
        }
        else if (sourceEntity.EntityType == EntityType.Pet)
        {
            sourceOpponent = fightManager.GetOpponentForPet(sourceEntity);
        }
        
        if (sourceOpponent == entity)
        {
            return CardTargetType.Opponent;
        }
        
        // Check if entity is an ally using RelationshipManager
        RelationshipManager sourceRelationship = sourceEntity.GetComponent<RelationshipManager>();
        if (sourceRelationship?.AllyEntity != null)
        {
            NetworkEntity allyEntity = sourceRelationship.AllyEntity.GetComponent<NetworkEntity>();
            if (allyEntity == entity)
            {
                return CardTargetType.Ally;
            }
        }
        
        // If it's neither self, opponent, nor ally, default to opponent
        // (This handles cases where the entity might be in a different fight or not part of the current combat)
        return CardTargetType.Opponent;
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) 
        {
            LogDebug("End drag ignored - not currently dragging");
            return;
        }
        
        LogDebug("End drag - processing drop");
        
        bool cardWasPlayed = false;
        
        // Check if we're over a valid drop zone
        if (validDropZone != null)
        {
            NetworkEntity targetEntity = validDropZone.GetTargetEntity();
            if (targetEntity != null && CanTargetEntity(targetEntity))
            {
                LogDebug($"Valid drop detected - playing card on target: {targetEntity.EntityName.Value}");
                
                // Override the target for this play
                if (sourceAndTargetIdentifier != null)
                {
                    sourceAndTargetIdentifier.SetOverrideTarget(targetEntity);
                }
                
                // Play the card
                if (handleCardPlay != null)
                {
                    handleCardPlay.OnCardPlayAttempt();
                    cardWasPlayed = true;
                    
                    // Notify SourceAndTargetIdentifier that card was played for cleanup
                    if (sourceAndTargetIdentifier != null)
                    {
                        sourceAndTargetIdentifier.OnCardPlayedOrDiscarded();
                    }
                }
                
                // Clear override target
                if (sourceAndTargetIdentifier != null)
                {
                    sourceAndTargetIdentifier.ClearOverrideTarget();
                }
            }
            else
            {
                LogDebug("Invalid drop - cannot target this entity");
            }
        }
        else
        {
            LogDebug("No valid drop zone - card will return to hand");
        }
        
        // Reset visual state and position
        ResetCardState(cardWasPlayed);
        
        // Clean up hover effects
        if (currentHoverZone != null)
        {
            currentHoverZone.OnCardDragExit();
            currentHoverZone = null;
        }
        
        // Reset drag state
        isDragging = false;
        isDragStarted = false;
        validDropZone = null;
        
        // Notify SourceAndTargetIdentifier that drag is ending
        if (sourceAndTargetIdentifier != null)
        {
            sourceAndTargetIdentifier.OnDragEnd();
        }
        
        LogDebug($"End drag complete - card was played: {cardWasPlayed}");
    }
    
    private void ResetCardState(bool cardWasPlayed)
    {
        LogDebug("Resetting card state after drag");
        
        // Always clean up drag visual state first, regardless of whether card was played
        // Reset scale and alpha
        transform.localScale = originalScale;
        canvasGroup.alpha = originalAlpha;
        canvasGroup.blocksRaycasts = true; // Make sure raycasts are enabled again
        
        // Reset canvas settings
        Canvas cardCanvas = GetComponent<Canvas>();
        if (cardCanvas != null)
        {
            if (originalCanvas != null)
            {
                cardCanvas.overrideSorting = false;
                LogDebug("Reset canvas sorting override");
            }
            else
            {
                // If there was no original canvas, remove the one we added
                if (Application.isPlaying)
                {
                    Destroy(cardCanvas);
                }
                else
                {
                    DestroyImmediate(cardCanvas);
                }
                LogDebug("Removed temporary canvas component");
            }
        }
        
        if (cardWasPlayed)
        {
            // Card was played - let the card system handle parent/position cleanup
            // Since we maintained original parent during drag, no special handling needed
            LogDebug("Card was played - visual drag state cleaned up, staying in original parent");
            return;
        }
        
        // Since we maintained the original parent during drag, no reparenting needed
        // Just reset position and visual state
        LogDebug("Card not played - resetting position within original parent (no reparenting needed)");
        
        LogDebug($"Reset visual state - scale: {originalScale}, alpha: {originalAlpha}");
        
        // Reset position (the layout group should handle this)
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = Vector2.zero;
            LogDebug("Reset anchored position to zero");
        }
        
        // Ensure the card is interactable again
        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            LogDebug("Ensured CanvasGroup is interactable and blocks raycasts");
        }
        
        // Force a layout rebuild to ensure proper positioning
        if (rectTransform != null)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
        
        // Re-enable UI event system to ensure card remains interactable
        EnsureUIEventSetup();
        
        LogDebug("Card state reset complete - returned to hand");
    }
    
    /// <summary>
    /// Called when the component is enabled to ensure UI events work
    /// </summary>
    private void OnEnable()
    {
        // Delay the setup slightly to ensure all components are ready
        if (Application.isPlaying)
        {
            StartCoroutine(DelayedUISetup());
        }
    }
    
    /// <summary>
    /// Ensures UI setup after a brief delay
    /// </summary>
    private System.Collections.IEnumerator DelayedUISetup()
    {
        yield return null; // Wait one frame
        if (this != null && gameObject.activeInHierarchy)
        {
            EnsureUIEventSetup();
        }
    }
    
    private void LogDebug(string message)
    {
        // Verbose logging disabled for performance
    }
} 