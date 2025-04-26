using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;

public class HandPanelHoverManager : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
{
    [Tooltip("The Camera rendering the UI Canvas this panel belongs to.")]
    public Camera uiCamera;

    private List<CardDragHandler> cardsInHand = new List<CardDragHandler>();
    private CardDragHandler currentlyHoveredCard = null;
    private RectTransform panelRectTransform;

    void Awake()
    {
        panelRectTransform = GetComponent<RectTransform>();
    }

    // Called by CombatUIManager whenever the hand UI is updated
    public void UpdateCardReferences(List<GameObject> cardObjects)
    {
        cardsInHand.Clear();
        foreach (GameObject cardGO in cardObjects)
        {
            if (cardGO != null && cardGO.activeSelf)
            {
                CardDragHandler handler = cardGO.GetComponent<CardDragHandler>();
                if (handler != null)
                {
                    if (handler.transform.parent == transform && !handler.isDragging && !handler.isDiscarding)
                    {
                        cardsInHand.Add(handler);
                    }
                }
                else
                {
                     Debug.LogWarning($"[HandPanelHoverManager] Card GameObject {cardGO.name} is missing CardDragHandler.");
                }
            }
        }
        // Reset hovered card reference if the previously hovered card is no longer in the list
        if (currentlyHoveredCard != null && (!cardsInHand.Contains(currentlyHoveredCard) || !currentlyHoveredCard.gameObject.activeInHierarchy))
        {
            currentlyHoveredCard = null;
        }
        // Debug.Log($"[HandPanelHoverManager] Updated card references from list. Count: {cardsInHand.Count}");
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (cardsInHand.Count == 0) return;

        CardDragHandler closestCard = FindClosestCard(eventData.position);

        if (closestCard != currentlyHoveredCard)
        {
            if (currentlyHoveredCard != null)
            {
                currentlyHoveredCard.ExitHoverState();
                // Debug.Log($"[HandPanelHoverManager] Exited hover on {currentlyHoveredCard.name}");
            }

            currentlyHoveredCard = closestCard;

            if (currentlyHoveredCard != null)
            {
                currentlyHoveredCard.EnterHoverState();
                // Debug.Log($"[HandPanelHoverManager] Entered hover on {currentlyHoveredCard.name}");
            }
        }
        
        // --- ADDED: Resort sibling indices after hover state change ---
        ResortSiblingIndices();
        // --- END ADDED ---
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // When mouse leaves the panel area, stop hovering the current card
        CardDragHandler cardToExit = currentlyHoveredCard; // Store reference
        currentlyHoveredCard = null; // Clear reference *before* calling ExitHoverState

        if (cardToExit != null) 
        {
            cardToExit.ExitHoverState(); // Call exit on the stored reference
            // Debug.Log($"[HandPanelHoverManager] Exited hover on {cardToExit.name} (Panel Exit)"); // Use cardToExit here
        }
        
        // --- ADDED: Resort sibling indices after hover state change ---
        ResortSiblingIndices();
        // --- END ADDED ---
    }

    private CardDragHandler FindClosestCard(Vector2 screenPosition)
    {
        // --- ADDED: Camera check ---
        if (uiCamera == null)
        {
            Debug.LogError("[HandPanelHoverManager] uiCamera is not assigned!");
            return null;
        }
        // --- END ADDED ---

        CardDragHandler hoveredCard = null;
        float minDepth = float.MaxValue; // Use depth (or sibling index) for tie-breaking if needed

        // Iterate in reverse to prioritize cards rendered on top (higher sibling index)
        for (int i = cardsInHand.Count - 1; i >= 0; i--)
        {
            CardDragHandler card = cardsInHand[i];

            // --- MODIFIED: Additional checks for valid cards ---
            if (card == null || !card.gameObject.activeSelf) continue;
            if (card.transform.parent != transform) continue; // Skip cards not parented to this panel (being dragged)
            if (card.isDragging || card.isDiscarding) continue; // Skip cards being dragged or discarded
            // --- END MODIFIED ---

            RectTransform cardRect = card.GetComponent<RectTransform>();
            if (cardRect == null) continue; // Skip if no RectTransform

            // --- NEW LOGIC: Check if pointer is within the card's rectangle ---
            if (RectTransformUtility.RectangleContainsScreenPoint(cardRect, screenPosition, uiCamera))
            {
                 // Simple approach: Return the first card found (topmost due to reverse iteration)
                 // If overlap is complex, you might need additional logic (e.g., check distance only among contained cards)
                 hoveredCard = card;
                 break; // Found the topmost card under the pointer

                // --- Alternative: If you want the absolute closest among overlapping cards (less common for UI) ---
                /*
                float distanceSqr = (screenPosition - (Vector2)uiCamera.WorldToScreenPoint(cardRect.position)).sqrMagnitude;
                if (distanceSqr < minDistanceSqr) {
                    minDistanceSqr = distanceSqr;
                    hoveredCard = card;
                }
                */
            }
        }

        // --- REMOVED: Distance check logic ---
        /*
        CardDragHandler closest = null;
        float minDistanceSqr = float.MaxValue;

        foreach (CardDragHandler card in cardsInHand)
        {
            // ... existing checks ...

            Vector2 cardScreenPosition = RectTransformUtility.WorldToScreenPoint(uiCamera, card.transform.position);
            float distanceSqr = (screenPosition - cardScreenPosition).sqrMagnitude;

            if (distanceSqr < minDistanceSqr)
            {
                minDistanceSqr = distanceSqr;
                closest = card;
            }
        }

        float maxDistanceThreshold = 150f;
        float maxDistanceSqr = maxDistanceThreshold * maxDistanceThreshold;

        if (minDistanceSqr > maxDistanceSqr)
        {
            return null;
        }
        return closest;
        */
        // --- END REMOVED ---

        return hoveredCard; // Return the card found within bounds (or null if none)
    }

    // --- ADDED: Function to resort sibling indices ---
    public void ResortSiblingIndices()
    {
        // Use the cached cardsInHand list which is updated by UpdateCardReferences
        if (cardsInHand.Count <= 1) return; // No sorting needed for 0 or 1 card

        // Create a temporary list to sort, preserving the original order of cardsInHand if needed elsewhere
        List<CardDragHandler> sortedCards = new List<CardDragHandler>();
        
        // --- MODIFIED: Only include cards that are children of this panel & add logging ---
        //Debug.Log($"[ResortSiblingIndices] Starting. Total cardsInHand: {cardsInHand.Count}");
        foreach (CardDragHandler card in cardsInHand)
        {
            if (card != null && card.transform != null)
            {
                bool isChild = card.transform.parent == transform;
                bool isDragging = card.isDragging;
                bool isDiscarding = card.isDiscarding;
                // --- Add check for card being active in hierarchy ---
                bool isActive = card.gameObject.activeInHierarchy;
                if (isChild && isActive && !isDragging && !isDiscarding)
                {
                    sortedCards.Add(card);
                    // Debug.Log($"[ResortSiblingIndices] Including: {card.name} (Active: {isActive}, Parent: {card.transform.parent?.name}, Drag:{isDragging}, Discard:{isDiscarding})");
                }
                else
                {
                    Debug.LogWarning($"[ResortSiblingIndices] EXCLUDING: {card.name} (Active: {isActive}, Parent: {card.transform.parent?.name ?? "null"}, Drag:{isDragging}, Discard:{isDiscarding})", card.gameObject);
                }
            }
             else {
                 Debug.LogWarning($"[ResortSiblingIndices] Skipping null card/transform in cardsInHand list.");
            }
        }
        
        if (sortedCards.Count <= 1) {
            //Debug.Log($"[ResortSiblingIndices] No sorting needed after filtering. Count: {sortedCards.Count}");
            // --- ADDED: Ensure single card is on top --- 
            if (sortedCards.Count == 1 && sortedCards[0] != null)
            {
                sortedCards[0].transform.SetAsLastSibling();
            }
            // --- END ADDED ---
            return; // No sorting needed if we filtered too many
        }
        // --- END MODIFIED ---

        // 2. Sort cards based on their original X position (left to right)
        sortedCards.Sort((a, b) => {
            // Null checks for safety
            if (a == null && b == null) return 0;
            if (a == null) return -1;
            if (b == null) return 1;
            return a.originalPosition.x.CompareTo(b.originalPosition.x);
        });

        // 3. Set sibling indices based on sorted order
        for (int i = 0; i < sortedCards.Count; i++)
        {
            if (sortedCards[i] != null && sortedCards[i].transform != null) // Ensure handler and transform are valid
            { 
                // Set the sibling index. Lower index = rendered first (behind)
                // Higher index = rendered later (in front)
                sortedCards[i].transform.SetSiblingIndex(i);
            }
        }

        // 4. Ensure the currently hovered card is always rendered last (on top)
        if (currentlyHoveredCard != null && currentlyHoveredCard.gameObject.activeSelf)
        {
            // Check if it's still in the list (it should be if UpdateCardReferences ran correctly)
            if (cardsInHand.Contains(currentlyHoveredCard))
            {
                currentlyHoveredCard.transform.SetAsLastSibling();
            }
        }
        //Debug.Log($"[ResortSiblingIndices] Finished. Sorted {sortedCards.Count} cards.");
    }
    // --- END ADDED ---

    // --- ADDED: Getter for CombatManager --- 
    public CardDragHandler GetCurrentlyHoveredCard()
    {
        return currentlyHoveredCard;
    }
    // --- END ADDED ---
    
    // --- ADDED: Method to reset all cards to their base state --- 
    public void ResetAllHovers()
    {
        // Clear the currently hovered card immediately to prevent ExitHover being called twice
        currentlyHoveredCard = null; 
        
        // Tell each card to exit its hover state (which animates it back)
        foreach (CardDragHandler card in cardsInHand)
        {
            if (card != null && card.gameObject.activeSelf && card.transform.parent == transform && !card.isDragging && !card.isDiscarding)
            {
                card.ExitHoverState();
            }
        }
        
        // Resort sibling indices to put them back in default order
        ResortSiblingIndices(); 
        //Debug.Log("[HandPanelHoverManager] ResetAllHovers called.");
    }
    // --- END ADDED ---
} 