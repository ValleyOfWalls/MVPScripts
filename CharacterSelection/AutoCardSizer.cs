using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Component that automatically scales cards to fit their container.
/// Add this to the parent object containing your card grid.
/// Works best when combined with a GridLayoutGroup.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class AutoCardSizer : MonoBehaviour
{
    [Header("Card Sizing Options")]
    [SerializeField] private bool autoSize = true;
    [SerializeField] private int preferredColumns = 4;
    [SerializeField] private int preferredRows = 2;
    [SerializeField] private float aspectRatio = 0.7f; // Width/Height ratio for cards (typical card ratio)
    [SerializeField] private Vector2 padding = new Vector2(10f, 10f);
    [SerializeField] private Vector2 spacing = new Vector2(5f, 5f);
    
    [Header("Size Constraints")]
    [SerializeField] private Vector2 minCardSize = new Vector2(80f, 114f);
    [SerializeField] private Vector2 maxCardSize = new Vector2(200f, 286f);
    
    private RectTransform rectTransform;
    private GridLayoutGroup gridLayoutGroup;
    private int lastChildCount = -1;
    private Vector2 lastContainerSize;
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        
        // Get or add GridLayoutGroup
        gridLayoutGroup = GetComponent<GridLayoutGroup>();
        if (gridLayoutGroup == null)
        {
            gridLayoutGroup = gameObject.AddComponent<GridLayoutGroup>();
        }
        
        // Setup initial grid properties
        gridLayoutGroup.spacing = spacing;
        gridLayoutGroup.padding.left = (int)padding.x;
        gridLayoutGroup.padding.right = (int)padding.x;
        gridLayoutGroup.padding.top = (int)padding.y;
        gridLayoutGroup.padding.bottom = (int)padding.y;
        gridLayoutGroup.childAlignment = TextAnchor.UpperCenter;
    }
    
    private void Update()
    {
        if (!autoSize) return;
        
        // Check if we need to recalculate
        int currentChildCount = transform.childCount;
        Vector2 currentSize = rectTransform.rect.size;
        
        if (currentChildCount != lastChildCount || currentSize != lastContainerSize)
        {
            RecalculateCardSizes();
            lastChildCount = currentChildCount;
            lastContainerSize = currentSize;
        }
    }
    
    /// <summary>
    /// Manually trigger recalculation of card sizes
    /// </summary>
    public void RecalculateCardSizes()
    {
        if (!autoSize || gridLayoutGroup == null) return;
        
        int childCount = 0;
        // Count only active children
        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i).gameObject.activeInHierarchy)
            {
                childCount++;
            }
        }
        
        if (childCount == 0) return;
        
        Vector2 containerSize = rectTransform.rect.size;
        Vector2 availableSize = containerSize - new Vector2(padding.x * 2, padding.y * 2);
        
        // Calculate optimal grid dimensions
        int columns, rows;
        CalculateOptimalGrid(childCount, out columns, out rows);
        
        // Calculate card size based on available space
        float cardWidth = (availableSize.x - (spacing.x * (columns - 1))) / columns;
        float cardHeight = (availableSize.y - (spacing.y * (rows - 1))) / rows;
        
        // Maintain aspect ratio and apply constraints
        Vector2 cardSize = CalculateConstrainedCardSize(cardWidth, cardHeight);
        
        // Apply to grid layout
        gridLayoutGroup.cellSize = cardSize;
        gridLayoutGroup.constraintCount = columns;
        gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        
        Debug.Log($"AutoCardSizer: Resized {childCount} cards to {cardSize} in {columns}x{rows} grid");
    }
    
    private void CalculateOptimalGrid(int itemCount, out int columns, out int rows)
    {
        if (itemCount <= preferredColumns)
        {
            // If we have fewer items than preferred columns, use single row
            columns = itemCount;
            rows = 1;
        }
        else
        {
            // Try to fit in preferred grid first
            columns = preferredColumns;
            rows = Mathf.CeilToInt((float)itemCount / columns);
            
            // If too many rows, try to balance
            if (rows > preferredRows)
            {
                // Calculate more balanced grid
                float aspectRatioContainer = rectTransform.rect.width / rectTransform.rect.height;
                float targetRatio = aspectRatio / aspectRatioContainer;
                
                columns = Mathf.RoundToInt(Mathf.Sqrt(itemCount * targetRatio));
                columns = Mathf.Max(1, columns);
                rows = Mathf.CeilToInt((float)itemCount / columns);
            }
        }
        
        // Ensure we don't exceed reasonable limits
        columns = Mathf.Clamp(columns, 1, itemCount);
        rows = Mathf.Max(1, rows);
    }
    
    private Vector2 CalculateConstrainedCardSize(float targetWidth, float targetHeight)
    {
        // Start with target dimensions
        Vector2 cardSize = new Vector2(targetWidth, targetHeight);
        
        // Apply aspect ratio constraint
        if (aspectRatio > 0)
        {
            // Determine which dimension is the limiting factor
            float widthFromHeight = targetHeight * aspectRatio;
            float heightFromWidth = targetWidth / aspectRatio;
            
            if (widthFromHeight <= targetWidth)
            {
                // Height is limiting factor
                cardSize = new Vector2(widthFromHeight, targetHeight);
            }
            else
            {
                // Width is limiting factor
                cardSize = new Vector2(targetWidth, heightFromWidth);
            }
        }
        
        // Apply size constraints
        cardSize.x = Mathf.Clamp(cardSize.x, minCardSize.x, maxCardSize.x);
        cardSize.y = Mathf.Clamp(cardSize.y, minCardSize.y, maxCardSize.y);
        
        // Re-apply aspect ratio after clamping if needed
        if (aspectRatio > 0)
        {
            float clampedAspectRatio = cardSize.x / cardSize.y;
            if (Mathf.Abs(clampedAspectRatio - aspectRatio) > 0.1f)
            {
                // Adjust to maintain aspect ratio within constraints
                if (cardSize.x == maxCardSize.x || cardSize.x == minCardSize.x)
                {
                    cardSize.y = cardSize.x / aspectRatio;
                    cardSize.y = Mathf.Clamp(cardSize.y, minCardSize.y, maxCardSize.y);
                }
                else if (cardSize.y == maxCardSize.y || cardSize.y == minCardSize.y)
                {
                    cardSize.x = cardSize.y * aspectRatio;
                    cardSize.x = Mathf.Clamp(cardSize.x, minCardSize.x, maxCardSize.x);
                }
            }
        }
        
        return cardSize;
    }
    
    /// <summary>
    /// Enable or disable auto-sizing
    /// </summary>
    public void SetAutoSize(bool enabled)
    {
        autoSize = enabled;
        if (enabled)
        {
            RecalculateCardSizes();
        }
    }
    
    /// <summary>
    /// Set preferred grid dimensions
    /// </summary>
    public void SetPreferredGrid(int columns, int rows)
    {
        preferredColumns = columns;
        preferredRows = rows;
        if (autoSize)
        {
            RecalculateCardSizes();
        }
    }
    
    /// <summary>
    /// Set the aspect ratio for cards
    /// </summary>
    public void SetAspectRatio(float ratio)
    {
        aspectRatio = ratio;
        if (autoSize)
        {
            RecalculateCardSizes();
        }
    }
} 