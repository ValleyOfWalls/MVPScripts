using UnityEngine;

/// <summary>
/// Simple data component to store hover-related information for cards.
/// Automatically added by HandAnimator when needed.
/// </summary>
public class CardHoverData : MonoBehaviour
{
    [Header("Stored Hover Data")]
    [SerializeField] private int originalSortingOrder = 0;
    [SerializeField] private bool hasStoredSortingOrder = false;
    
    public int OriginalSortingOrder => originalSortingOrder;
    public bool HasStoredSortingOrder => hasStoredSortingOrder;
    
    /// <summary>
    /// Stores the original Canvas sorting order for later restoration
    /// </summary>
    public void StoreOriginalSortingOrder(int sortingOrder)
    {
        if (!hasStoredSortingOrder) // Only store if we haven't already stored one
        {
            originalSortingOrder = sortingOrder;
            hasStoredSortingOrder = true;
        }
    }
    
    /// <summary>
    /// Clears the stored sorting order
    /// </summary>
    public void ClearStoredSortingOrder()
    {
        originalSortingOrder = 0;
        hasStoredSortingOrder = false;
    }
    
    /// <summary>
    /// Debug method to show current state
    /// </summary>
    [ContextMenu("Debug Hover Data")]
    public void DebugHoverData()
    {
        Canvas canvas = GetComponent<Canvas>();
        int currentSortingOrder = canvas != null ? canvas.sortingOrder : 0;
        Debug.Log($"CardHoverData on {gameObject.name}: OriginalSortingOrder={originalSortingOrder}, HasStored={hasStoredSortingOrder}, CurrentSortingOrder={currentSortingOrder}");
    }
} 