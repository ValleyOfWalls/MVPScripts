using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles card selection during draft phase
/// </summary>
public class DraftSelectionManager : MonoBehaviour
{
    // Reference to the draft pack this card belongs to
    private DraftPack parentPack;
    
    // Reference to the card being managed
    private Card managedCard;
    
    // Button to handle selection
    private Button selectionButton;

    private void Awake()
    {
        // Get the card component
        managedCard = GetComponent<Card>();
        
        // Get the selection button
        selectionButton = GetComponent<Button>();
        
        if (managedCard == null)
        {
            Debug.LogError("DraftSelectionManager requires a Card component on the same GameObject.");
        }
        
        if (selectionButton == null)
        {
            Debug.LogError("DraftSelectionManager requires a Button component on the same GameObject.");
        }
        else
        {
            // Add click listener
            selectionButton.onClick.AddListener(OnCardClicked);
        }
    }

    /// <summary>
    /// Sets the parent draft pack reference
    /// </summary>
    /// <param name="pack">The draft pack this card belongs to</param>
    public void SetDraftPack(DraftPack pack)
    {
        parentPack = pack;
    }

    /// <summary>
    /// Called when the card is clicked
    /// </summary>
    private void OnCardClicked()
    {
        if (parentPack == null)
        {
            Debug.LogError("DraftSelectionManager: No parent pack assigned.");
            return;
        }
        
        if (managedCard == null)
        {
            Debug.LogError("DraftSelectionManager: No card component found.");
            return;
        }
        
        // Notify the parent pack that this card was selected
        parentPack.OnCardSelected(managedCard.CardId);
    }

    private void OnDestroy()
    {
        // Clean up event listeners
        if (selectionButton != null)
        {
            selectionButton.onClick.RemoveListener(OnCardClicked);
        }
    }
} 