using UnityEngine;
using UnityEngine.EventSystems;

// Attach this script to UI elements that act as drop targets for cards.
public class CardDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    // Define the types of targets a card can be dropped on
    public enum TargetType
    {
        OwnPet,
        EnemyPet,
        PlayerSelf, // If cards can target the player directly
        // Add other target types as needed
    }

    [Tooltip("Specify what this zone represents (e.g., the opponent's pet area)")]
    public TargetType targetType;

    // Optional: Reference to an image/graphic for highlighting
    [SerializeField] public UnityEngine.UI.Image highlightGraphic;

    void Start()
    {
        // Ensure the highlight is off initially
        if (highlightGraphic != null) highlightGraphic.enabled = false;
    }

    // Called when a draggable object (like a card) is dropped onto this zone
    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log($"Card dropped on {gameObject.name} representing {targetType}");
        
        // The CardDragHandler's OnEndDrag currently handles the logic 
        // of checking eventData.pointerEnter and calling GameManager.
        // So, this OnDrop might not need to do much unless we change that logic.
        
        // Reset highlight on drop
        if (highlightGraphic != null) highlightGraphic.enabled = false;
        
        // Example: If CardDragHandler wasn't doing the work, you'd do it here:
        // CardDragHandler draggedCard = eventData.pointerDrag.GetComponent<CardDragHandler>();
        // if (draggedCard != null)
        // {
        //     GameManager gameManager = FindObjectOfType<GameManager>();
        //     if (gameManager != null)
        //     {
        //         gameManager.AttemptPlayCard(draggedCard.cardData, targetType);
        //     }
        // }
    }

    // Called when a draggable object enters the bounds of this zone
    public void OnPointerEnter(PointerEventData eventData)
    {
        // Check if the object being dragged is a card
        GameObject hoveringCard = eventData.pointerDrag;
        
        if (hoveringCard != null && hoveringCard.GetComponent<CardDragHandler>() != null)
        {
            Debug.Log($"Card entered drop zone: {gameObject.name}");
            // Show highlight (optional)
            if (highlightGraphic != null) 
            {
                highlightGraphic.enabled = true;
            }
            else
            {
                Debug.LogWarning($"HighlightGraphic is null on {gameObject.name}");
            }
            
            // --- ADDED: Preview damage calculation when hovering over a target ---
            CardDragHandler cardHandler = hoveringCard.GetComponent<CardDragHandler>();
            if (cardHandler != null && cardHandler.cardData != null)
            {
                GameManager gameManager = GameManager.Instance;
                if (gameManager != null && gameManager.GetCardPreviewCalculator() != null)
                {
                    // Update the card's text with preview damage
                    gameManager.GetCardPreviewCalculator().UpdateCardPreviewForTarget(
                        hoveringCard, 
                        cardHandler.cardData, 
                        targetType
                    );
                }
            }
            // --- END ADDED ---
        }
    }

    // Called when a draggable object exits the bounds of this zone
    public void OnPointerExit(PointerEventData eventData)
    {
         // Check if the object that *was* being dragged is a card 
         // (eventData.pointerDrag might be null if drop happened elsewhere)
        GameObject exitingCard = eventData.pointerDrag;
        
        if (exitingCard != null && exitingCard.GetComponent<CardDragHandler>() != null)
        {
            Debug.Log($"Card exited drop zone: {gameObject.name}");
            // Hide highlight (optional)
            if (highlightGraphic != null) 
            {
                highlightGraphic.enabled = false;
            }
             else
            {
                Debug.LogWarning($"HighlightGraphic is null on {gameObject.name}");
            }
            
            // --- ADDED: Restore original card description ---
            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.GetCardPreviewCalculator() != null)
            {
                gameManager.GetCardPreviewCalculator().RestoreOriginalDescription(exitingCard);
            }
            // --- END ADDED ---
        }
         // Also hide if the pointer exits for any reason while highlight is on
         else if (highlightGraphic != null && highlightGraphic.enabled)
         {
             highlightGraphic.enabled = false;
         }
    }
} 