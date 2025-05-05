using UnityEngine;
using UnityEngine.EventSystems;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using Combat;

public class CardDragHandler : NetworkBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Drag Settings")]
    [SerializeField] private float dragElevation = 30f;
    [SerializeField] private float dragScale = 1.2f;
    [SerializeField] private float hoveredScale = 1.1f;
    [SerializeField] private float dragSpeed = 10f;
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Color validTargetColor = Color.green;
    [SerializeField] private Color invalidTargetColor = Color.red;

    // Cache references
    private Card card;
    private Vector3 originalPosition;
    private Vector3 originalScale;
    private Transform originalParent;
    private Camera mainCamera;
    private bool isDragging = false;
    private bool isHovering = false;
    
    // Cached owner reference
    private NetworkPlayer ownerPlayer;

    // Targets
    private ICombatant currentTarget = null;
    private Transform currentHighlightedTransform = null;

    // Sync state
    private readonly SyncVar<bool> isBeingDragged = new SyncVar<bool>();

    private void Awake()
    {
        // Get required components
        card = GetComponent<Card>();
        
        if (canvas == null)
            canvas = GetComponent<Canvas>();
        
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
        }
        
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Get main camera
        mainCamera = Camera.main;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Set up event handlers if needed
        isBeingDragged.OnChange += OnDragStateChanged;
        
        // Make sure we have initial values
        originalScale = transform.localScale;
        
        // Try to get the owner from Player Hand
        FindOwnerPlayer();
    }
    
    private void FindOwnerPlayer()
    {
        // Try to find player hand parent to determine owner
        PlayerHand playerHand = GetComponentInParent<PlayerHand>();
        if (playerHand != null)
        {
            // Get the owner from the player hand
            ownerPlayer = playerHand.GetComponentInParent<NetworkPlayer>();
        }
    }

    private void OnDragStateChanged(bool previousValue, bool newValue, bool asServer)
    {
        // Update visual state if someone else is dragging this card
        if (!isDragging && newValue)
        {
            // Another player is dragging this card
            canvasGroup.alpha = 0.6f;
        }
        else if (!isDragging && !newValue)
        {
            // No one is dragging this card
            canvasGroup.alpha = 1.0f;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Don't hover while dragging
        if (isDragging || isBeingDragged.Value)
            return;
            
        isHovering = true;
        
        // Scale up slightly on hover
        transform.localScale = originalScale * hoveredScale;
        
        // Inform the card - create a new PointerEventData if needed
        if (card != null)
            card.OnPointerEnter(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Reset hover state
        if (isHovering && !isDragging)
        {
            isHovering = false;
            transform.localScale = originalScale;
            
            // Inform the card
            if (card != null)
                card.OnPointerExit(eventData);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Only allow dragging if this is your card and it's playable
        if (!IsOwner)
            return;
            
        // Store original values
        originalPosition = transform.position;
        originalScale = transform.localScale;
        originalParent = transform.parent;
        
        // Make sure we have owner reference
        if (ownerPlayer == null)
        {
            FindOwnerPlayer();
        }
        
        // Elevate the card and scale it up
        Vector3 elevatedPosition = originalPosition;
        elevatedPosition.z -= dragElevation; // Move toward camera
        transform.position = elevatedPosition;
        transform.localScale = originalScale * dragScale;
        
        // Set the sorting order higher
        canvas.sortingOrder = 200;
        
        // Make card semi-transparent to see what's beneath
        canvasGroup.alpha = 0.8f;
        canvasGroup.blocksRaycasts = false;
        
        // Set drag state
        isDragging = true;
        
        // Sync with server
        if (IsOwner)
        {
            SetDragStateServerRpc(true);
        }
        
        // Inform the card - pass the eventData
        if (card != null)
            card.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || !IsOwner)
            return;

        // Convert mouse position to world position
        Vector3 mousePosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = originalPosition.z - dragElevation; // Keep consistent z-depth
        
        // Smoothly move to the new position
        transform.position = Vector3.Lerp(transform.position, mousePosition, Time.deltaTime * dragSpeed);
        
        // Check for targets under the card
        CheckForTargets();
        
        // Inform the card - pass the eventData
        if (card != null)
            card.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging || !IsOwner)
            return;
            
        isDragging = false;
        canvasGroup.blocksRaycasts = true;
        canvas.sortingOrder = 100;
        
        // Sync with server
        if (IsOwner)
        {
            SetDragStateServerRpc(false);
        }
        
        bool validTarget = (currentTarget != null);
        
        // Inform the card with proper parameters
        if (card != null)
            card.OnEndDrag(eventData);
        
        // If we have a valid target, play the card on that target
        if (validTarget && card != null)
        {
            // Instead of calling PlayCard, use ReturnToHand if card wasn't played
            // Card targeting is now handled by CardTargetingSystem
        }
        else
        {
            // Return to original position/parent
            ReturnToHand();
        }
        
        // Remove any highlighting
        RemoveTargetHighlight();
        
        // Reset target
        currentTarget = null;
    }

    private void CheckForTargets()
    {
        // Reset current target
        ICombatant previousTarget = currentTarget;
        currentTarget = null;
        
        // Convert mouse position to ray
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit2D[] hits = Physics2D.RaycastAll(ray.origin, ray.direction, 100f);
        
        // Check for valid targets (NetworkPlayer or Pet)
        foreach (RaycastHit2D hit in hits)
        {
            // Try to get NetworkPlayer
            NetworkPlayer networkPlayer = hit.collider.GetComponent<NetworkPlayer>();
            if (networkPlayer != null)
            {
                // Check if this is the opponent (only allow playing cards on opponent, not yourself)
                if (ownerPlayer != null && networkPlayer != ownerPlayer)
                {
                    currentTarget = networkPlayer.CombatPlayer;
                    // Highlight target
                    HighlightTarget(hit.transform, true);
                    return;
                }
            }
            
            // Try to get Pet
            Pet pet = hit.collider.GetComponent<Pet>();
            if (pet != null)
            {
                // Check if this is an opponent's pet
                if (ownerPlayer != null && pet.PlayerOwner != ownerPlayer)
                {
                    // Find the combat pet reference
                    CombatPet combatPet = null;
                    
                    // Try to get from the NetworkPlayer's opponent reference
                    if (ownerPlayer != null && ownerPlayer.OpponentCombatPet != null)
                    {
                        combatPet = ownerPlayer.OpponentCombatPet;
                        currentTarget = combatPet;
                        // Highlight target
                        HighlightTarget(hit.transform, true);
                        return;
                    }
                }
            }
            
            // Check for CombatPlayer directly
            CombatPlayer combatPlayer = hit.collider.GetComponent<CombatPlayer>();
            if (combatPlayer != null)
            {
                // Make sure it's not our own combat player
                if (ownerPlayer != null && combatPlayer != ownerPlayer.CombatPlayer)
                {
                    currentTarget = combatPlayer;
                    // Highlight target
                    HighlightTarget(hit.transform, true);
                    return;
                }
            }
            
            // Check for CombatPet directly
            CombatPet directCombatPet = hit.collider.GetComponent<CombatPet>();
            if (directCombatPet != null)
            {
                // Make sure it's not our own combat pet
                if (ownerPlayer != null && directCombatPet != ownerPlayer.CombatPet)
                {
                    currentTarget = directCombatPet;
                    // Highlight target
                    HighlightTarget(hit.transform, true);
                    return;
                }
            }
        }
        
        // No valid target found, remove any highlight
        if (previousTarget != null)
        {
            RemoveTargetHighlight();
        }
    }

    private void HighlightTarget(Transform targetTransform, bool isValid)
    {
        // Remove previous highlight if it's a different transform
        if (currentHighlightedTransform != null && currentHighlightedTransform != targetTransform)
        {
            RemoveTargetHighlight();
        }
        
        // Set the new highlighted transform
        currentHighlightedTransform = targetTransform;
        
        // Get or add outline component
        Outline outline = targetTransform.GetComponent<Outline>();
        if (outline == null)
        {
            outline = targetTransform.gameObject.AddComponent<Outline>();
        }
        
        // Set color based on validity
        outline.SetColor(isValid ? validTargetColor : invalidTargetColor);
        
        // Enable the outline
        outline.enabled = true;
    }
    
    private void RemoveTargetHighlight()
    {
        // Remove highlight from previously highlighted object
        if (currentHighlightedTransform != null)
        {
            Outline outline = currentHighlightedTransform.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }
            currentHighlightedTransform = null;
        }
    }

    private void ReturnToHand()
    {
        // Return the card to its original position and parent
        transform.SetParent(originalParent);
        
        // Make sure we explicitly reset the Z position to prevent cards from disappearing behind the canvas
        Vector3 returnPosition = originalPosition;
        
        // Reset local position and scale
        transform.position = returnPosition;
        transform.localScale = originalScale;
        canvasGroup.alpha = 1.0f;
        
        // Reset the Z position explicitly to ensure card visibility by getting the PlayerHand component
        PlayerHand playerHand = GetComponentInParent<PlayerHand>();
        if (playerHand != null)
        {
            playerHand.ArrangeCardsInHand();
        }
        
        // Inform the card
        card.ReturnToHand();
    }

    [ServerRpc]
    private void SetDragStateServerRpc(bool isDragging)
    {
        // Update the synced state
        isBeingDragged.Value = isDragging;
    }
    
    [ServerRpc]
    private void PlayCardServerRpc(NetworkObject targetNetworkObject)
    {
        // This method should be updated to use the CardTargetingSystem instead
        
        // For backward compatibility, just call ReturnToHand
        // As the targeting and card playing is now handled by CardTargetingSystem
        ReturnToHand();
    }
} 