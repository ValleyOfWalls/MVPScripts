using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles card drop interactions for the pet view.
/// This is currently stubbed and will be implemented when card drop functionality is added.
/// Attach to: OwnPetViewPrefab GameObject
/// </summary>
public class OwnPetCardDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IPetCardInteraction
{
    [Header("Drop Zone Settings")]
    [SerializeField] private bool acceptAllCards = true;
    [SerializeField] private LayerMask cardLayerMask = -1;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject dropHighlight;
    [SerializeField] private Color highlightColor = Color.green;
    
    [Header("Debug")]
    [SerializeField] private bool debugLogEnabled = true;
    
    // Current target pet
    private NetworkEntity targetPet;
    
    // Drop zone state
    private bool isHighlighted = false;
    private bool isDragOver = false;
    
    private void Awake()
    {
        SetupDropZone();
    }
    
    private void SetupDropZone()
    {
        // Ensure we have a collider for drop detection
        Collider2D collider = GetComponent<Collider2D>();
        if (collider == null)
        {
            // Add a BoxCollider2D if none exists
            BoxCollider2D boxCollider = gameObject.AddComponent<BoxCollider2D>();
            boxCollider.isTrigger = true;
            LogDebug("Added BoxCollider2D for drop zone functionality");
        }
        
        // Setup highlight object if not assigned
        if (dropHighlight == null)
        {
            dropHighlight = CreateDropHighlight();
        }
        
        // Initially hide highlight
        SetHighlightVisible(false);
    }
    
    private GameObject CreateDropHighlight()
    {
        // Create a simple highlight object
        GameObject highlight = new GameObject("DropHighlight");
        highlight.transform.SetParent(transform);
        highlight.transform.localPosition = Vector3.zero;
        highlight.transform.localScale = Vector3.one;
        
        // Add a visual component (could be an Image or SpriteRenderer)
        UnityEngine.UI.Image highlightImage = highlight.AddComponent<UnityEngine.UI.Image>();
        highlightImage.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.3f);
        
        LogDebug("Created drop highlight object");
        return highlight;
    }
    
    /// <summary>
    /// Sets the target pet for card drop interactions
    /// </summary>
    /// <param name="pet">The NetworkEntity pet that cards can be dropped on</param>
    public void SetTargetPet(NetworkEntity pet)
    {
        targetPet = pet;
        
        if (pet != null)
        {
            LogDebug($"Card drop zone target set to: {pet.EntityName.Value}");
        }
        else
        {
            LogDebug("Card drop zone target cleared");
        }
    }
    
    /// <summary>
    /// Called when a card is dropped on this zone
    /// </summary>
    /// <param name="eventData">The drop event data</param>
    public void OnDrop(PointerEventData eventData)
    {
        LogDebug("Card drop detected - functionality not yet implemented");
        
        // TODO: Implement card drop functionality
        // This should:
        // 1. Validate that the dropped object is a card
        // 2. Check if the card can be played on the target pet
        // 3. Execute the card effect on the target pet
        // 4. Handle networking for multiplayer synchronization
        
        GameObject droppedObject = eventData.pointerDrag;
        if (droppedObject != null)
        {
            LogDebug($"Dropped object: {droppedObject.name}");
            
            // Stub: Check if it's a card
            Card droppedCard = droppedObject.GetComponent<Card>();
            if (droppedCard != null)
            {
                HandleCardDrop(droppedCard);
            }
            else
            {
                LogDebug("Dropped object is not a card");
            }
        }
        
        // Hide highlight after drop
        SetHighlightVisible(false);
        isDragOver = false;
    }
    
    /// <summary>
    /// Called when a dragged object enters the drop zone
    /// </summary>
    /// <param name="eventData">The pointer event data</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null)
        {
            isDragOver = true;
            
            // Check if the dragged object is a valid card
            Card draggedCard = eventData.pointerDrag.GetComponent<Card>();
            if (draggedCard != null && CanAcceptCard(draggedCard))
            {
                SetHighlightVisible(true);
                LogDebug($"Valid card entered drop zone: {draggedCard.name}");
            }
        }
    }
    
    /// <summary>
    /// Called when a dragged object exits the drop zone
    /// </summary>
    /// <param name="eventData">The pointer event data</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        isDragOver = false;
        SetHighlightVisible(false);
        LogDebug("Object exited drop zone");
    }
    
    /// <summary>
    /// Handles the actual card drop logic (stubbed)
    /// </summary>
    /// <param name="card">The card that was dropped</param>
    private void HandleCardDrop(Card card)
    {
        if (targetPet == null)
        {
            LogDebug("Cannot handle card drop - no target pet set");
            return;
        }
        
        if (!CanAcceptCard(card))
        {
            LogDebug($"Card {card.name} cannot be played on target pet");
            return;
        }
        
        LogDebug($"STUB: Playing card {card.name} on pet {targetPet.EntityName.Value}");
        
        // TODO: Implement actual card playing logic
        // This should involve:
        // 1. Validating card requirements (energy cost, targeting rules, etc.)
        // 2. Sending a network request to play the card
        // 3. Applying card effects to the target pet
        // 4. Updating UI and game state
    }
    
    /// <summary>
    /// Checks if a card can be accepted by this drop zone (stubbed)
    /// </summary>
    /// <param name="card">The card to check</param>
    /// <returns>True if the card can be accepted</returns>
    private bool CanAcceptCard(Card card)
    {
        if (card == null) return false;
        
        // TODO: Implement actual card validation logic
        // This could check:
        // - Card type (support cards only?)
        // - Energy requirements
        // - Targeting restrictions
        // - Game state conditions
        
        // For now, accept all cards if acceptAllCards is true
        return acceptAllCards;
    }
    
    /// <summary>
    /// Shows or hides the drop highlight
    /// </summary>
    /// <param name="visible">Whether the highlight should be visible</param>
    private void SetHighlightVisible(bool visible)
    {
        if (dropHighlight != null && isHighlighted != visible)
        {
            dropHighlight.SetActive(visible);
            isHighlighted = visible;
            LogDebug($"Drop highlight visibility: {visible}");
        }
    }
    
    /// <summary>
    /// Gets the current target pet
    /// </summary>
    public NetworkEntity GetTargetPet()
    {
        return targetPet;
    }
    
    /// <summary>
    /// Sets whether this drop zone accepts all cards
    /// </summary>
    /// <param name="acceptAll">Whether to accept all cards</param>
    public void SetAcceptAllCards(bool acceptAll)
    {
        acceptAllCards = acceptAll;
        LogDebug($"Accept all cards set to: {acceptAll}");
    }
    
    /// <summary>
    /// Sets the highlight color for the drop zone
    /// </summary>
    /// <param name="color">The color to use for highlighting</param>
    public void SetHighlightColor(Color color)
    {
        highlightColor = color;
        
        if (dropHighlight != null)
        {
            UnityEngine.UI.Image highlightImage = dropHighlight.GetComponent<UnityEngine.UI.Image>();
            if (highlightImage != null)
            {
                highlightImage.color = new Color(color.r, color.g, color.b, 0.3f);
            }
        }
    }
    
    private void LogDebug(string message)
    {
        if (debugLogEnabled)
        {
            Debug.Log($"[OwnPetCardDropZone] {message}");
        }
    }
} 