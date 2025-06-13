using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Reflection;

/// <summary>
/// Automatically scales child objects in any Layout Group (Grid, Horizontal, or Vertical) to fit appropriately 
/// within the parent rect while maintaining proportions. Attach this to the GameObject with a Layout Group component.
/// </summary>
public class AutoScaleGridContent : MonoBehaviour
{
    [Header("Auto-Derived Settings (Read-Only)")]
    [SerializeField, ReadOnly] private float maxCellWidth = 100f;
    [SerializeField, ReadOnly] private float maxCellHeight = 140f;
    [SerializeField, ReadOnly] private float minCellWidth = 50f;
    [SerializeField, ReadOnly] private float minCellHeight = 70f;
    [SerializeField, ReadOnly] private int maxColumns = 6;
    [SerializeField, ReadOnly] private int preferredColumns = 4;
    
    [Header("Optional Overrides")]
    [SerializeField] private bool useCustomSettings = false;
    [SerializeField] private float customMaxWidth = 100f;
    [SerializeField] private float customMaxHeight = 140f;
    [SerializeField] private float customMinScale = 0.5f; // Minimum scale factor
    [SerializeField] private int customMaxColumns = 6;
    [SerializeField] private float paddingPercent = 0.1f; // 10% padding
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    private RectTransform rectTransform;
    private LayoutGroup layoutGroup;
    private GridLayoutGroup gridLayoutGroup;
    private HorizontalLayoutGroup horizontalLayoutGroup;
    private VerticalLayoutGroup verticalLayoutGroup;
    private LayoutGroupType layoutType;
    private int lastChildCount = 0;
    private Vector2 lastRectSize;
    private bool isInitialized = false;
    private bool settingsDerived = false;
    
    // References for auto-detection
    private CharacterSelectionUIManager uiManager;
    private Vector2 originalCardSize = Vector2.zero;
    
    // Layout group type enum
    private enum LayoutGroupType
    {
        None,
        Grid,
        Horizontal,
        Vertical
    }
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        Initialize();
    }
    
    private void Start()
    {
        // Force initial scaling after one frame to ensure everything is set up
        StartCoroutine(DelayedInitialScale());
    }
    
    private void Update()
    {
        // Check for changes in child count or rect size
        CheckForChanges();
    }
    
    #endregion
    
    #region Initialization
    
    private void Initialize()
    {
        rectTransform = GetComponent<RectTransform>();
        
        // Try to find any layout group type
        gridLayoutGroup = GetComponent<GridLayoutGroup>();
        horizontalLayoutGroup = GetComponent<HorizontalLayoutGroup>();
        verticalLayoutGroup = GetComponent<VerticalLayoutGroup>();
        
        // Determine layout type
        if (gridLayoutGroup != null)
        {
            layoutGroup = gridLayoutGroup;
            layoutType = LayoutGroupType.Grid;
        }
        else if (horizontalLayoutGroup != null)
        {
            layoutGroup = horizontalLayoutGroup;
            layoutType = LayoutGroupType.Horizontal;
        }
        else if (verticalLayoutGroup != null)
        {
            layoutGroup = verticalLayoutGroup;
            layoutType = LayoutGroupType.Vertical;
        }
        else
        {
            layoutType = LayoutGroupType.None;
        }
        
        if (rectTransform == null)
        {
            Debug.LogError($"AutoScaleGridContent: RectTransform component not found on {gameObject.name}!");
            return;
        }
        
        if (layoutType == LayoutGroupType.None)
        {
            Debug.LogError($"AutoScaleGridContent: No LayoutGroup component found on {gameObject.name}! Requires GridLayoutGroup, HorizontalLayoutGroup, or VerticalLayoutGroup.");
            return;
        }
        

        
        // Find UI manager for intelligent settings detection
        uiManager = FindFirstObjectByType<CharacterSelectionUIManager>();
        
        // Derive intelligent settings
        DeriveIntelligentSettings();
        
        // Store initial values
        lastChildCount = transform.childCount;
        lastRectSize = rectTransform.rect.size;
        isInitialized = true;
        

    }
    
    private IEnumerator DelayedInitialScale()
    {
        yield return null; // Wait one frame
        yield return null; // Wait another frame to be safe
        
        if (transform.childCount > 0)
        {
            RecalculateScale();
        }
    }
    
    private void DeriveIntelligentSettings()
    {
        if (useCustomSettings)
        {
            // Use custom overrides
            maxCellWidth = customMaxWidth;
            maxCellHeight = customMaxHeight;
            minCellWidth = maxCellWidth * customMinScale;
            minCellHeight = maxCellHeight * customMinScale;
            maxColumns = customMaxColumns;
            settingsDerived = true;
            

            return;
        }
        
        // Get parent size for calculations
        Vector2 parentSize = rectTransform.rect.size;
        
        // Try to get card prefab size from UI manager
        originalCardSize = DetectCardPrefabSize();
        
        if (originalCardSize != Vector2.zero)
        {
            // Use detected card size as basis
            maxCellWidth = originalCardSize.x;
            maxCellHeight = originalCardSize.y;
        }
        else
        {
            // Fallback: Use existing GridLayoutGroup cell size if available
            if (layoutType == LayoutGroupType.Grid && gridLayoutGroup.cellSize != Vector2.zero)
            {
                maxCellWidth = gridLayoutGroup.cellSize.x;
                maxCellHeight = gridLayoutGroup.cellSize.y;
            }
            else
            {
                // Final fallback: Use reasonable defaults based on parent size
                if (layoutType == LayoutGroupType.Horizontal)
                {
                    // For horizontal layout, height is constrained by parent, width is flexible
                    maxCellHeight = Mathf.Min(parentSize.y * 0.8f, 150f); // 80% of height
                    maxCellWidth = maxCellHeight * 0.7f; // Card-like aspect ratio (height > width)
                }
                else if (layoutType == LayoutGroupType.Vertical)
                {
                    // For vertical layout, width is constrained by parent, height is flexible
                    maxCellWidth = Mathf.Min(parentSize.x * 0.8f, 120f); // 80% of width
                    maxCellHeight = maxCellWidth * 1.4f; // Card-like aspect ratio (height > width)
                }
                else
                {
                    // Grid layout fallback
                    maxCellWidth = Mathf.Min(parentSize.x * 0.25f, 120f); // 25% of width or 120px max
                    maxCellHeight = maxCellWidth * 1.4f; // Card-like aspect ratio
                }
            }
        }
        
        // Calculate minimum sizes (50% of max by default)
        minCellWidth = maxCellWidth * 0.5f;
        minCellHeight = maxCellHeight * 0.5f;
        
        // Calculate optimal column count based on layout type and parent size
        if (parentSize.x > 0 && parentSize.y > 0)
        {
            float spacingX = 0f;
            float spacingY = 0f;
            float paddingX = 0f;
            float paddingY = 0f;
            
            // Get spacing and padding based on layout type
            if (layoutType == LayoutGroupType.Grid)
            {
                spacingX = gridLayoutGroup.spacing.x;
                spacingY = gridLayoutGroup.spacing.y;
                paddingX = gridLayoutGroup.padding.left + gridLayoutGroup.padding.right;
                paddingY = gridLayoutGroup.padding.top + gridLayoutGroup.padding.bottom;
            }
            else if (layoutType == LayoutGroupType.Horizontal)
            {
                spacingX = horizontalLayoutGroup.spacing;
                paddingX = horizontalLayoutGroup.padding.left + horizontalLayoutGroup.padding.right;
                paddingY = horizontalLayoutGroup.padding.top + horizontalLayoutGroup.padding.bottom;
            }
            else if (layoutType == LayoutGroupType.Vertical)
            {
                spacingY = verticalLayoutGroup.spacing;
                paddingX = verticalLayoutGroup.padding.left + verticalLayoutGroup.padding.right;
                paddingY = verticalLayoutGroup.padding.top + verticalLayoutGroup.padding.bottom;
            }
            
            float availableWidth = parentSize.x - paddingX;
            float availableHeight = parentSize.y - paddingY;
            
            if (layoutType == LayoutGroupType.Horizontal)
            {
                // For horizontal layout, calculate how many cards can fit horizontally
                int maxPossibleColumns = Mathf.FloorToInt((availableWidth + spacingX) / (maxCellWidth + spacingX));
                maxColumns = Mathf.Clamp(maxPossibleColumns, 1, 20); // Higher limit for horizontal
                preferredColumns = maxColumns; // Use all available space
            }
            else if (layoutType == LayoutGroupType.Vertical)
            {
                // For vertical layout, it's essentially 1 column, many rows
                maxColumns = 1;
                preferredColumns = 1;
            }
            else
            {
                // Grid layout
                int maxPossibleColumns = Mathf.FloorToInt((availableWidth + spacingX) / (maxCellWidth + spacingX));
                maxColumns = Mathf.Clamp(maxPossibleColumns, 1, 8);
                preferredColumns = Mathf.Max(1, Mathf.RoundToInt(maxColumns * 0.75f));
            }
        }
        else
        {
            // Fallback based on layout type
            if (layoutType == LayoutGroupType.Horizontal)
            {
                maxColumns = 10;
                preferredColumns = 8;
            }
            else if (layoutType == LayoutGroupType.Vertical)
            {
                maxColumns = 1;
                preferredColumns = 1;
            }
            else
            {
                maxColumns = 6;
                preferredColumns = 4;
            }
        }
        
        settingsDerived = true;
        
        if (enableDebugLogs)
        {
            Debug.Log($"AutoScaleGridContent: Derived settings - Max: {maxCellWidth}x{maxCellHeight}, " +
                     $"Min: {minCellWidth}x{minCellHeight}, Columns: {preferredColumns}/{maxColumns}");
        }
    }
    
    private Vector2 DetectCardPrefabSize()
    {
        // Try to get card prefab from UI manager
        if (uiManager != null)
        {
            // Use reflection to access the deckCardPrefab field
            var field = typeof(CharacterSelectionUIManager).GetField("deckCardPrefab", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                GameObject deckCardPrefab = field.GetValue(uiManager) as GameObject;
                if (deckCardPrefab != null)
                {
                    RectTransform prefabRect = deckCardPrefab.GetComponent<RectTransform>();
                    if (prefabRect != null)
                    {
                        if (enableDebugLogs)
                        {
                            Debug.Log($"AutoScaleGridContent: Detected card prefab size: {prefabRect.rect.size}");
                        }
                        return prefabRect.rect.size;
                    }
                }
            }
        }
        
        // Try to detect from first child if available
        if (transform.childCount > 0)
        {
            RectTransform childRect = transform.GetChild(0).GetComponent<RectTransform>();
            if (childRect != null)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"AutoScaleGridContent: Detected size from first child: {childRect.rect.size}");
                }
                return childRect.rect.size;
            }
        }
        
        return Vector2.zero;
    }
    
    #endregion
    
    #region Change Detection
    
    private void CheckForChanges()
    {
        if (!isInitialized) return;
        
        int currentChildCount = transform.childCount;
        Vector2 currentRectSize = rectTransform.rect.size;
        
        // Check if child count changed
        if (currentChildCount != lastChildCount)
        {
            if (enableDebugLogs)
            {
    
            }
            
            lastChildCount = currentChildCount;
            
            // Re-derive settings if we now have children and didn't detect card size before
            if (currentChildCount > 0 && originalCardSize == Vector2.zero && !useCustomSettings)
            {
                DeriveIntelligentSettings();
            }
            
            StartCoroutine(DelayedRecalculate());
        }
        
        // Check if rect size changed significantly
        if (Vector2.Distance(currentRectSize, lastRectSize) > 1f)
        {
            if (enableDebugLogs)
            {
    
            }
            
            lastRectSize = currentRectSize;
            StartCoroutine(DelayedRecalculate());
        }
    }
    
    private IEnumerator DelayedRecalculate()
    {
        // Wait a frame to ensure all layout updates are complete
        yield return null;
        RecalculateScale();
    }
    
    #endregion
    
    #region Scaling Logic
    
    private void RecalculateScale()
    {
        if (!isInitialized || transform.childCount == 0) return;
        
        Vector2 availableSize = rectTransform.rect.size;
        int childCount = transform.childCount;
        

        
        if (availableSize.x <= 0 || availableSize.y <= 0)
        {

            return;
        }
        
        if (layoutType == LayoutGroupType.Grid)
        {
            RecalculateGridLayout(availableSize, childCount);
        }
        else if (layoutType == LayoutGroupType.Horizontal)
        {
            RecalculateHorizontalLayout(availableSize, childCount);
        }
        else if (layoutType == LayoutGroupType.Vertical)
        {
            RecalculateVerticalLayout(availableSize, childCount);
        }
        
        // Force layout rebuild
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }
    
    private void RecalculateGridLayout(Vector2 availableSize, int childCount)
    {
        // Calculate optimal grid dimensions and cell size
        GridDimensions gridDims = CalculateOptimalGridDimensions(childCount, availableSize);
        
        // Ensure cell size never exceeds available space
        Vector2 maxAllowedSize = GetMaxAllowedCellSize(availableSize, childCount);
        gridDims.cellSize.x = Mathf.Min(gridDims.cellSize.x, maxAllowedSize.x);
        gridDims.cellSize.y = Mathf.Min(gridDims.cellSize.y, maxAllowedSize.y);
        
        // Apply the calculated cell size to the grid layout
        gridLayoutGroup.cellSize = gridDims.cellSize;
        
        if (enableDebugLogs)
        {
            Debug.Log($"AutoScaleGridContent: Final cell size: {gridDims.cellSize} (max allowed: {maxAllowedSize}) in {gridDims.columns}x{gridDims.rows} grid");
            Debug.Log($"AutoScaleGridContent: Grid spacing: {gridLayoutGroup.spacing}, Padding: L{gridLayoutGroup.padding.left} R{gridLayoutGroup.padding.right} T{gridLayoutGroup.padding.top} B{gridLayoutGroup.padding.bottom}");
        }
    }
    
    private void RecalculateHorizontalLayout(Vector2 availableSize, int childCount)
    {
        // For horizontal layout, scale children directly
        Vector2 targetSize = CalculateTargetSizeForHorizontalLayout(availableSize, childCount);
        ScaleChildrenDirectly(targetSize);
        
        if (enableDebugLogs)
        {
            Debug.Log($"AutoScaleGridContent: Horizontal layout - Target child size: {targetSize}");
            Debug.Log($"AutoScaleGridContent: Horizontal spacing: {horizontalLayoutGroup.spacing}, Padding: L{horizontalLayoutGroup.padding.left} R{horizontalLayoutGroup.padding.right} T{horizontalLayoutGroup.padding.top} B{horizontalLayoutGroup.padding.bottom}");
        }
    }
    
    private void RecalculateVerticalLayout(Vector2 availableSize, int childCount)
    {
        // For vertical layout, scale children directly
        Vector2 targetSize = CalculateTargetSizeForVerticalLayout(availableSize, childCount);
        ScaleChildrenDirectly(targetSize);
        
        if (enableDebugLogs)
        {
            Debug.Log($"AutoScaleGridContent: Vertical layout - Target child size: {targetSize}");
            Debug.Log($"AutoScaleGridContent: Vertical spacing: {verticalLayoutGroup.spacing}, Padding: L{verticalLayoutGroup.padding.left} R{verticalLayoutGroup.padding.right} T{verticalLayoutGroup.padding.top} B{verticalLayoutGroup.padding.bottom}");
        }
    }
    
    private Vector2 GetMaxAllowedCellSize(Vector2 availableSize, int itemCount)
    {
        // Apply padding to get usable space
        Vector2 usableSize = availableSize * (1f - paddingPercent);
        
        // Account for grid padding
        float paddingX = gridLayoutGroup.padding.left + gridLayoutGroup.padding.right;
        float paddingY = gridLayoutGroup.padding.top + gridLayoutGroup.padding.bottom;
        usableSize.x -= paddingX;
        usableSize.y -= paddingY;
        
        // For safety, ensure we have at least 1 column and calculate max cell size for that
        Vector2 spacing = gridLayoutGroup.spacing;
        
        // Single column scenario (worst case)
        float maxCellWidth = usableSize.x;
        int rows = itemCount;
        float maxCellHeight = (usableSize.y - (spacing.y * (rows - 1))) / rows;
        
        // Ensure positive values
        maxCellWidth = Mathf.Max(maxCellWidth, 10f);
        maxCellHeight = Mathf.Max(maxCellHeight, 10f);
        
        return new Vector2(maxCellWidth, maxCellHeight);
    }
    
    private Vector2 CalculateTargetSizeForHorizontalLayout(Vector2 availableSize, int childCount)
    {
        // Apply padding to get usable space
        Vector2 usableSize = availableSize * (1f - paddingPercent);
        
        // Account for horizontal layout padding
        float paddingX = horizontalLayoutGroup.padding.left + horizontalLayoutGroup.padding.right;
        float paddingY = horizontalLayoutGroup.padding.top + horizontalLayoutGroup.padding.bottom;
        usableSize.x -= paddingX;
        usableSize.y -= paddingY;
        
        // Account for spacing between children
        float totalSpacing = horizontalLayoutGroup.spacing * Mathf.Max(0, childCount - 1);
        float availableWidthForChildren = usableSize.x - totalSpacing;
        
        // Calculate width per child
        float targetWidth = availableWidthForChildren / childCount;
        
        // Height should use most of available height
        float targetHeight = usableSize.y;
        
        // Maintain aspect ratio if we have original card size
        if (originalCardSize != Vector2.zero)
        {
            float aspectRatio = originalCardSize.x / originalCardSize.y;
            
            // Try width-constrained scaling first
            float heightFromWidth = targetWidth / aspectRatio;
            if (heightFromWidth <= targetHeight)
            {
                targetHeight = heightFromWidth;
            }
            else
            {
                // Height-constrained scaling
                targetWidth = targetHeight * aspectRatio;
            }
        }
        
        // Ensure minimum and maximum sizes
        targetWidth = Mathf.Clamp(targetWidth, minCellWidth, maxCellWidth);
        targetHeight = Mathf.Clamp(targetHeight, minCellHeight, maxCellHeight);
        
        return new Vector2(targetWidth, targetHeight);
    }
    
    private Vector2 CalculateTargetSizeForVerticalLayout(Vector2 availableSize, int childCount)
    {
        // Apply padding to get usable space
        Vector2 usableSize = availableSize * (1f - paddingPercent);
        
        // Account for vertical layout padding
        float paddingX = verticalLayoutGroup.padding.left + verticalLayoutGroup.padding.right;
        float paddingY = verticalLayoutGroup.padding.top + verticalLayoutGroup.padding.bottom;
        usableSize.x -= paddingX;
        usableSize.y -= paddingY;
        
        // Account for spacing between children
        float totalSpacing = verticalLayoutGroup.spacing * Mathf.Max(0, childCount - 1);
        float availableHeightForChildren = usableSize.y - totalSpacing;
        
        // Calculate height per child
        float targetHeight = availableHeightForChildren / childCount;
        
        // Width should use most of available width
        float targetWidth = usableSize.x;
        
        // Maintain aspect ratio if we have original card size
        if (originalCardSize != Vector2.zero)
        {
            float aspectRatio = originalCardSize.x / originalCardSize.y;
            
            // Try height-constrained scaling first
            float widthFromHeight = targetHeight * aspectRatio;
            if (widthFromHeight <= targetWidth)
            {
                targetWidth = widthFromHeight;
            }
            else
            {
                // Width-constrained scaling
                targetHeight = targetWidth / aspectRatio;
            }
        }
        
        // Ensure minimum and maximum sizes
        targetWidth = Mathf.Clamp(targetWidth, minCellWidth, maxCellWidth);
        targetHeight = Mathf.Clamp(targetHeight, minCellHeight, maxCellHeight);
        
        return new Vector2(targetWidth, targetHeight);
    }
    
    private void ScaleChildrenDirectly(Vector2 targetSize)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            RectTransform childRect = child.GetComponent<RectTransform>();
            
            if (childRect != null)
            {
                // Get the original size of the prefab
                Vector2 originalSize = originalCardSize;
                if (originalSize == Vector2.zero)
                {
                    // Fallback: use current rect size as original
                    originalSize = childRect.rect.size;
                }
                
                // Calculate scale factors
                float scaleX = targetSize.x / originalSize.x;
                float scaleY = targetSize.y / originalSize.y;
                
                // Use the smaller scale factor to maintain aspect ratio
                float uniformScale = Mathf.Min(scaleX, scaleY);
                
                // Apply the scale to the entire prefab
                child.localScale = new Vector3(uniformScale, uniformScale, 1f);
                
                if (enableDebugLogs && i == 0) // Log only for first child to avoid spam
                {
                    Debug.Log($"AutoScaleGridContent: Scaled child {child.name} from {originalSize} to scale {uniformScale} (target was {targetSize})");
                }
            }
        }
    }
    
    private GridDimensions CalculateOptimalGridDimensions(int itemCount, Vector2 availableSize)
    {
        // Apply padding to get usable space
        Vector2 usableSize = availableSize * (1f - paddingPercent);
        
        // Account for layout padding (only for grid layout)
        float paddingX = 0f;
        float paddingY = 0f;
        Vector2 spacing = Vector2.zero;
        
        if (layoutType == LayoutGroupType.Grid)
        {
            paddingX = gridLayoutGroup.padding.left + gridLayoutGroup.padding.right;
            paddingY = gridLayoutGroup.padding.top + gridLayoutGroup.padding.bottom;
            spacing = gridLayoutGroup.spacing;
        }
        
        usableSize.x -= paddingX;
        usableSize.y -= paddingY;
        
        GridDimensions bestDims = new GridDimensions();
        float bestScore = float.MinValue;
        
        if (enableDebugLogs)
        {
            Debug.Log($"AutoScaleGridContent: Usable size after padding: {usableSize}, Spacing: {spacing}");
        }
        
        // Try different column counts to find the best fit
        for (int cols = 1; cols <= Mathf.Min(maxColumns, itemCount); cols++)
        {
            int rows = Mathf.CeilToInt((float)itemCount / cols);
            
            // Calculate available space per cell
            float availableCellWidth = (usableSize.x - (spacing.x * Mathf.Max(0, cols - 1))) / cols;
            float availableCellHeight = (usableSize.y - (spacing.y * Mathf.Max(0, rows - 1))) / rows;
            
            // Skip if negative space
            if (availableCellWidth <= 0 || availableCellHeight <= 0) continue;
            
            // Determine cell size while maintaining aspect ratio
            Vector2 cellSize = CalculateCellSizeWithAspectRatio(availableCellWidth, availableCellHeight);
            
            // Skip if cell size is below minimum
            if (cellSize.x < minCellWidth || cellSize.y < minCellHeight) continue;
            
            // Calculate a score for this configuration
            float score = CalculateLayoutScore(cols, rows, cellSize, itemCount);
            
            if (enableDebugLogs)
            {
                Debug.Log($"AutoScaleGridContent: Testing {cols}x{rows} grid - Available per cell: {availableCellWidth}x{availableCellHeight}, Final cell: {cellSize}, Score: {score}");
            }
            
            if (score > bestScore)
            {
                bestScore = score;
                bestDims = new GridDimensions
                {
                    columns = cols,
                    rows = rows,
                    cellSize = cellSize
                };
            }
        }
        
        // Fallback if no valid configuration found
        if (bestDims.cellSize == Vector2.zero)
        {
            bestDims = CreateFallbackDimensions(itemCount, usableSize);
            if (enableDebugLogs)
            {
                Debug.Log($"AutoScaleGridContent: Using fallback dimensions: {bestDims.cellSize}");
            }
        }
        
        return bestDims;
    }
    
    private Vector2 CalculateCellSizeWithAspectRatio(float availableWidth, float availableHeight)
    {
        // Use the original max cell size as the target aspect ratio
        float targetAspectRatio = maxCellWidth / maxCellHeight;
        
        // Calculate cell size based on available space and target aspect ratio
        float cellWidth, cellHeight;
        
        if (availableWidth / availableHeight > targetAspectRatio)
        {
            // Height is the limiting factor
            cellHeight = Mathf.Min(availableHeight, maxCellHeight);
            cellWidth = cellHeight * targetAspectRatio;
        }
        else
        {
            // Width is the limiting factor
            cellWidth = Mathf.Min(availableWidth, maxCellWidth);
            cellHeight = cellWidth / targetAspectRatio;
        }
        
        // Ensure we don't exceed maximum sizes
        cellWidth = Mathf.Min(cellWidth, maxCellWidth);
        cellHeight = Mathf.Min(cellHeight, maxCellHeight);
        
        // Ensure we meet minimum sizes
        cellWidth = Mathf.Max(cellWidth, minCellWidth);
        cellHeight = Mathf.Max(cellHeight, minCellHeight);
        
        return new Vector2(cellWidth, cellHeight);
    }
    
    private float CalculateLayoutScore(int columns, int rows, Vector2 cellSize, int itemCount)
    {
        float score = 0f;
        
        // Prefer configurations closer to preferred column count
        float columnScore = 1f - Mathf.Abs(columns - preferredColumns) / (float)maxColumns;
        score += columnScore * 0.3f;
        
        // Prefer larger cell sizes (better visibility)
        float sizeScore = (cellSize.x * cellSize.y) / (maxCellWidth * maxCellHeight);
        score += sizeScore * 0.5f;
        
        // Prefer fewer empty cells
        int totalCells = columns * rows;
        int emptyCells = totalCells - itemCount;
        float efficiencyScore = 1f - (emptyCells / (float)totalCells);
        score += efficiencyScore * 0.2f;
        
        return score;
    }
    
    private GridDimensions CreateFallbackDimensions(int itemCount, Vector2 usableSize)
    {
        // Simple fallback: try to fit in a square-ish grid with minimum cell size
        int cols = Mathf.CeilToInt(Mathf.Sqrt(itemCount));
        int rows = Mathf.CeilToInt((float)itemCount / cols);
        
        Vector2 spacing = gridLayoutGroup.spacing;
        float cellWidth = (usableSize.x - (spacing.x * Mathf.Max(0, cols - 1))) / cols;
        float cellHeight = (usableSize.y - (spacing.y * Mathf.Max(0, rows - 1))) / rows;
        
        // Ensure cell sizes are positive and reasonable
        cellWidth = Mathf.Max(10f, cellWidth);  // Minimum 10px width
        cellHeight = Mathf.Max(10f, cellHeight); // Minimum 10px height
        
        // Don't exceed original max sizes in fallback
        cellWidth = Mathf.Min(cellWidth, maxCellWidth);
        cellHeight = Mathf.Min(cellHeight, maxCellHeight);
        
        return new GridDimensions
        {
            columns = cols,
            rows = rows,
            cellSize = new Vector2(cellWidth, cellHeight)
        };
    }
    
    #endregion
    
    #region Public Interface
    
    /// <summary>
    /// Force recalculation of grid scaling
    /// </summary>
    public void ForceRecalculate()
    {
        if (isInitialized)
        {
            RecalculateScale();
        }
    }
    
    /// <summary>
    /// Force re-derivation of intelligent settings and recalculate
    /// </summary>
    public void RederiveSettings()
    {
        if (isInitialized)
        {
            DeriveIntelligentSettings();
            RecalculateScale();
        }
    }
    
    /// <summary>
    /// Enable custom settings mode with specific parameters
    /// </summary>
    public void EnableCustomSettings(float maxWidth, float maxHeight, float minScale = 0.5f, int maxCols = 6)
    {
        useCustomSettings = true;
        customMaxWidth = maxWidth;
        customMaxHeight = maxHeight;
        customMinScale = minScale;
        customMaxColumns = maxCols;
        
        DeriveIntelligentSettings();
        ForceRecalculate();
    }
    
    /// <summary>
    /// Disable custom settings and return to intelligent auto-detection
    /// </summary>
    public void DisableCustomSettings()
    {
        useCustomSettings = false;
        originalCardSize = Vector2.zero; // Force re-detection
        RederiveSettings();
    }
    
    #endregion
    
    #region Helper Classes
    
    private struct GridDimensions
    {
        public int columns;
        public int rows;
        public Vector2 cellSize;
    }
    
    #endregion
} 