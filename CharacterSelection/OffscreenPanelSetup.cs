using UnityEngine;

public enum HideDirection
{
    Up,
    Down,
    Left,
    Right
}

/// <summary>
/// Automatically sets up UI panels to start off-screen and remember their target position.
/// Attach this to panels that are positioned where you want them to end up when animated in.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class OffscreenPanelSetup : MonoBehaviour
{
    [Header("Setup Options")]
    [SerializeField] private bool autoSetupOnAwake = true;
    [SerializeField] private HideDirection hideDirection = HideDirection.Down;
    [SerializeField] private bool autoCalculateOffset = true; // Automatically calculate offset based on panel size
    [SerializeField] private float manualOffscreenOffset = 400f; // Used when autoCalculateOffset is false
    [SerializeField] private float offsetBuffer = 50f; // Extra buffer distance when auto-calculating
    
    [Header("Stored Positions (Read-Only)")]
    [SerializeField, ReadOnly] private Vector2 targetPosition;
    [SerializeField, ReadOnly] private Vector2 hiddenPosition;
    [SerializeField, ReadOnly] private Vector2 canvasSize;
    [SerializeField, ReadOnly] private Vector2 panelSize;
    [SerializeField, ReadOnly] private float calculatedOffset;
    [SerializeField, ReadOnly] private bool isSetup = false;
    
    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private RectTransform canvasRectTransform;
    
    // Public properties for the animator to use
    public Vector2 TargetPosition => targetPosition;
    public Vector2 HiddenPosition => hiddenPosition;
    
    void Awake()
    {
        if (autoSetupOnAwake)
        {
            SetupPanelPositions();
        }
    }
    
    [ContextMenu("Setup Panel Positions")]
    public void SetupPanelPositions()
    {
        InitializeComponents();
        StoreCurrentPositionAsTarget();
        CalculateHiddenPosition();
        ApplyHiddenPosition();
        MarkAsSetup();
        
        Debug.Log($"OffscreenPanelSetup: Setup complete for {gameObject.name} - Target: {targetPosition}, Hidden: {hiddenPosition}");
    }
    
    private void InitializeComponents()
    {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        
        // Store panel size
        panelSize = rectTransform.sizeDelta;
        
        if (parentCanvas != null)
        {
            canvasRectTransform = parentCanvas.GetComponent<RectTransform>();
            canvasSize = canvasRectTransform.sizeDelta;
        }
        else
        {
            Debug.LogError($"OffscreenPanelSetup: No parent Canvas found for {gameObject.name}");
            canvasSize = Vector2.zero;
        }
    }
    
    private void StoreCurrentPositionAsTarget()
    {
        // Store the current anchored position as the target (where we want to animate to)
        targetPosition = rectTransform.anchoredPosition;
        Debug.Log($"OffscreenPanelSetup: Stored target position for {gameObject.name}: {targetPosition}");
    }
    
    private void CalculateHiddenPosition()
    {
        // Calculate or use manual offset
        float offsetToUse = autoCalculateOffset ? CalculateAutoOffset() : manualOffscreenOffset;
        calculatedOffset = offsetToUse;
        
        // Calculate the hidden position based on the selected direction
        switch (hideDirection)
        {
            case HideDirection.Up:
                hiddenPosition = new Vector2(targetPosition.x, canvasSize.y/2 + offsetToUse);
                break;
            case HideDirection.Down:
                hiddenPosition = new Vector2(targetPosition.x, -canvasSize.y/2 - offsetToUse);
                break;
            case HideDirection.Left:
                hiddenPosition = new Vector2(-canvasSize.x/2 - offsetToUse, targetPosition.y);
                break;
            case HideDirection.Right:
                hiddenPosition = new Vector2(canvasSize.x/2 + offsetToUse, targetPosition.y);
                break;
        }
        
        Debug.Log($"OffscreenPanelSetup: Calculated hidden position for {gameObject.name} ({hideDirection}) using {(autoCalculateOffset ? "auto" : "manual")} offset ({offsetToUse:F1}): {hiddenPosition}");
    }
    
    /// <summary>
    /// Automatically calculates the minimum offset needed to completely hide the panel off-screen
    /// </summary>
    private float CalculateAutoOffset()
    {
        float requiredOffset = 0f;
        
        switch (hideDirection)
        {
            case HideDirection.Up:
            case HideDirection.Down:
                // For vertical movement, use panel height plus buffer
                requiredOffset = (panelSize.y / 2f) + offsetBuffer;
                break;
                
            case HideDirection.Left:
            case HideDirection.Right:
                // For horizontal movement, use panel width plus buffer
                requiredOffset = (panelSize.x / 2f) + offsetBuffer;
                break;
        }
        
        // Ensure minimum offset for smooth animation
        float minimumOffset = 100f;
        requiredOffset = Mathf.Max(requiredOffset, minimumOffset);
        
        Debug.Log($"OffscreenPanelSetup: Auto-calculated offset for {gameObject.name} ({hideDirection}): {requiredOffset:F1} (panel size: {panelSize}, buffer: {offsetBuffer})");
        return requiredOffset;
    }
    
    private void ApplyHiddenPosition()
    {
        // Move the panel to the hidden position
        rectTransform.anchoredPosition = hiddenPosition;
        Debug.Log($"OffscreenPanelSetup: Moved {gameObject.name} to hidden position: {hiddenPosition}");
    }
    
    private void MarkAsSetup()
    {
        isSetup = true;
    }
    
    /// <summary>
    /// Move the panel to its target position (for testing or immediate showing)
    /// </summary>
    [ContextMenu("Move to Target Position")]
    public void MoveToTargetPosition()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
            
        rectTransform.anchoredPosition = targetPosition;
        Debug.Log($"OffscreenPanelSetup: Moved {gameObject.name} to target position: {targetPosition}");
    }
    
    /// <summary>
    /// Move the panel to its hidden position (for testing or immediate hiding)
    /// </summary>
    [ContextMenu("Move to Hidden Position")]
    public void MoveToHiddenPosition()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
            
        rectTransform.anchoredPosition = hiddenPosition;
        Debug.Log($"OffscreenPanelSetup: Moved {gameObject.name} to hidden position: {hiddenPosition}");
    }
    
    /// <summary>
    /// Reset the setup - useful if you want to reposition the target
    /// </summary>
    [ContextMenu("Reset Setup")]
    public void ResetSetup()
    {
        isSetup = false;
        
        // Move back to target position so you can reposition
        if (targetPosition != Vector2.zero)
        {
            MoveToTargetPosition();
        }
        
        Debug.Log($"OffscreenPanelSetup: Reset setup for {gameObject.name}");
    }
    
    /// <summary>
    /// Update the manual offscreen offset and recalculate positions (only applies when autoCalculateOffset is false)
    /// </summary>
    public void UpdateManualOffscreenOffset(float newOffset)
    {
        manualOffscreenOffset = newOffset;
        CalculateHiddenPosition();
        
        // If we're currently in hidden position, move to new hidden position
        if (Vector2.Distance(rectTransform.anchoredPosition, hiddenPosition) > Vector2.Distance(rectTransform.anchoredPosition, targetPosition))
        {
            ApplyHiddenPosition();
        }
        
        Debug.Log($"OffscreenPanelSetup: Updated manual offscreen offset to {manualOffscreenOffset} for {gameObject.name}");
    }
    
    /// <summary>
    /// Update the offset buffer used in auto-calculation and recalculate positions
    /// </summary>
    public void UpdateOffsetBuffer(float newBuffer)
    {
        offsetBuffer = newBuffer;
        
        if (autoCalculateOffset)
        {
            CalculateHiddenPosition();
            
            // If we're currently in hidden position, move to new hidden position
            if (Vector2.Distance(rectTransform.anchoredPosition, hiddenPosition) > Vector2.Distance(rectTransform.anchoredPosition, targetPosition))
            {
                ApplyHiddenPosition();
            }
        }
        
        Debug.Log($"OffscreenPanelSetup: Updated offset buffer to {offsetBuffer} for {gameObject.name}");
    }
    
    /// <summary>
    /// Toggle between automatic and manual offset calculation
    /// </summary>
    public void SetAutoCalculateOffset(bool useAutoCalculate)
    {
        autoCalculateOffset = useAutoCalculate;
        CalculateHiddenPosition();
        
        // If we're currently in hidden position, move to new hidden position
        if (Vector2.Distance(rectTransform.anchoredPosition, hiddenPosition) > Vector2.Distance(rectTransform.anchoredPosition, targetPosition))
        {
            ApplyHiddenPosition();
        }
        
        Debug.Log($"OffscreenPanelSetup: Set auto-calculate offset to {autoCalculateOffset} for {gameObject.name}");
    }
    
    /// <summary>
    /// Update the hide direction and recalculate positions
    /// </summary>
    public void UpdateHideDirection(HideDirection newDirection)
    {
        hideDirection = newDirection;
        CalculateHiddenPosition();
        
        // If we're currently in hidden position, move to new hidden position
        if (Vector2.Distance(rectTransform.anchoredPosition, hiddenPosition) > Vector2.Distance(rectTransform.anchoredPosition, targetPosition))
        {
            ApplyHiddenPosition();
        }
        
        Debug.Log($"OffscreenPanelSetup: Updated hide direction to {hideDirection} for {gameObject.name}");
    }
    
    // Validation in editor
    void OnValidate()
    {
        if (Application.isPlaying) return;
        
        // Recalculate hidden position if direction or offset changes
        if (isSetup && targetPosition != Vector2.zero)
        {
            Vector2 previousHiddenPosition = hiddenPosition;
            CalculateHiddenPosition();
            if (hiddenPosition != previousHiddenPosition)
            {
                Debug.Log($"OffscreenPanelSetup: Updated hidden position for {gameObject.name}: {hiddenPosition}");
            }
        }
    }
    
    // Visual indicators
    void OnDrawGizmos()
    {
        if (!isSetup || rectTransform == null) return;
        
        // Draw gizmos to show target and hidden positions
        Vector3 worldTargetPos = transform.TransformPoint(targetPosition);
        Vector3 worldHiddenPos = transform.TransformPoint(hiddenPosition);
        
        // Target position (green)
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(worldTargetPos, Vector3.one * 50f);
        
        // Hidden position (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(worldHiddenPos, Vector3.one * 50f);
        
        // Connection line
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(worldTargetPos, worldHiddenPos);
    }
    
    void OnDrawGizmosSelected()
    {
        if (!isSetup) return;
        
        // Draw labels when selected (this would need a custom editor to show text)
        OnDrawGizmos();
        
        // Draw current position indicator
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 75f);
    }
} 