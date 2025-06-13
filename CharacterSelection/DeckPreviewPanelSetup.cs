using UnityEngine;

/// <summary>
/// Automatically sets up deck preview panels to start below the canvas and remember their target position.
/// Attach this to deck preview panels that are positioned where you want them to end up when animated in.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DeckPreviewPanelSetup : MonoBehaviour
{
    [Header("Setup Options")]
    [SerializeField] private bool autoSetupOnAwake = true;
    [SerializeField] private float belowCanvasOffset = -400f; // How far below canvas to position
    
    [Header("Stored Positions (Read-Only)")]
    [SerializeField, ReadOnly] private Vector2 targetPosition;
    [SerializeField, ReadOnly] private Vector2 hiddenPosition;
    [SerializeField, ReadOnly] private Vector2 canvasSize;
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
        
        Debug.Log($"DeckPreviewPanelSetup: Setup complete for {gameObject.name} - Target: {targetPosition}, Hidden: {hiddenPosition}");
    }
    
    private void InitializeComponents()
    {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        
        if (parentCanvas != null)
        {
            canvasRectTransform = parentCanvas.GetComponent<RectTransform>();
            canvasSize = canvasRectTransform.sizeDelta;
        }
        else
        {
            Debug.LogError($"DeckPreviewPanelSetup: No parent Canvas found for {gameObject.name}");
            canvasSize = Vector2.zero;
        }
    }
    
    private void StoreCurrentPositionAsTarget()
    {
        // Store the current anchored position as the target (where we want to animate to)
        targetPosition = rectTransform.anchoredPosition;
        Debug.Log($"DeckPreviewPanelSetup: Stored target position for {gameObject.name}: {targetPosition}");
    }
    
    private void CalculateHiddenPosition()
    {
        // Calculate the hidden position (below the canvas)
        hiddenPosition = new Vector2(targetPosition.x, belowCanvasOffset);
        Debug.Log($"DeckPreviewPanelSetup: Calculated hidden position for {gameObject.name}: {hiddenPosition}");
    }
    
    private void ApplyHiddenPosition()
    {
        // Move the panel to the hidden position
        rectTransform.anchoredPosition = hiddenPosition;
        Debug.Log($"DeckPreviewPanelSetup: Moved {gameObject.name} to hidden position: {hiddenPosition}");
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
        Debug.Log($"DeckPreviewPanelSetup: Moved {gameObject.name} to target position: {targetPosition}");
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
        Debug.Log($"DeckPreviewPanelSetup: Moved {gameObject.name} to hidden position: {hiddenPosition}");
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
        
        Debug.Log($"DeckPreviewPanelSetup: Reset setup for {gameObject.name}");
    }
    
    /// <summary>
    /// Update the below canvas offset and recalculate positions
    /// </summary>
    public void UpdateBelowCanvasOffset(float newOffset)
    {
        belowCanvasOffset = newOffset;
        CalculateHiddenPosition();
        
        // If we're currently in hidden position, move to new hidden position
        if (Vector2.Distance(rectTransform.anchoredPosition, hiddenPosition) > Vector2.Distance(rectTransform.anchoredPosition, targetPosition))
        {
            ApplyHiddenPosition();
        }
        
        Debug.Log($"DeckPreviewPanelSetup: Updated below canvas offset to {belowCanvasOffset} for {gameObject.name}");
    }
    
    // Validation in editor
    void OnValidate()
    {
        if (Application.isPlaying) return;
        
        // Recalculate hidden position if offset changes
        if (isSetup && targetPosition != Vector2.zero)
        {
            Vector2 newHiddenPosition = new Vector2(targetPosition.x, belowCanvasOffset);
            if (newHiddenPosition != hiddenPosition)
            {
                hiddenPosition = newHiddenPosition;
                Debug.Log($"DeckPreviewPanelSetup: Updated hidden position for {gameObject.name}: {hiddenPosition}");
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