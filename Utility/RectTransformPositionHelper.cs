using UnityEngine;
using UnityEditor;

/// <summary>
/// Utility script to detect exact distance of a RectTransform's center from the top and left edges of the canvas.
/// Attach this to any GameObject with a RectTransform to see its position values in the inspector.
/// Works in editor mode for easy positioning.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class RectTransformPositionHelper : MonoBehaviour
{
    [Header("Position Information")]
    [SerializeField, ReadOnly] private float distanceFromLeft = 0f;
    [SerializeField, ReadOnly] private float distanceFromTop = 0f;
    [SerializeField, ReadOnly] private float distanceFromRight = 0f;
    [SerializeField, ReadOnly] private float distanceFromBottom = 0f;
    
    [Header("Canvas Information")]
    [SerializeField, ReadOnly] private Vector2 canvasSize = Vector2.zero;
    [SerializeField, ReadOnly] private string canvasName = "";
    
    [Header("Anchored Position")]
    [SerializeField, ReadOnly] private Vector2 anchoredPosition = Vector2.zero;
    [SerializeField, ReadOnly] private Vector2 anchorMin = Vector2.zero;
    [SerializeField, ReadOnly] private Vector2 anchorMax = Vector2.zero;
    
    [Header("World Position")]
    [SerializeField, ReadOnly] private Vector3 worldPosition = Vector3.zero;
    [SerializeField, ReadOnly] private Vector2 localPosition = Vector2.zero;
    
    [Header("Options")]
    [SerializeField] private bool autoUpdate = true;
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color gizmoColor = Color.yellow;
    
    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private RectTransform canvasRectTransform;
    
    void Start()
    {
        Initialize();
    }
    
    void Update()
    {
        if (autoUpdate)
        {
            UpdatePositionInfo();
        }
    }
    
    void OnValidate()
    {
        // Update when values change in inspector
        if (Application.isPlaying || !Application.isPlaying) // Works in both edit and play mode
        {
            Initialize();
            UpdatePositionInfo();
        }
    }
    
    private void Initialize()
    {
        rectTransform = GetComponent<RectTransform>();
        FindParentCanvas();
    }
    
    private void FindParentCanvas()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            canvasRectTransform = parentCanvas.GetComponent<RectTransform>();
            canvasName = parentCanvas.name;
            if (canvasRectTransform != null)
            {
                canvasSize = canvasRectTransform.sizeDelta;
            }
        }
        else
        {
            canvasName = "No Canvas Found";
            canvasSize = Vector2.zero;
        }
    }
    
    private void UpdatePositionInfo()
    {
        if (rectTransform == null) return;
        
        // Update basic transform info
        anchoredPosition = rectTransform.anchoredPosition;
        anchorMin = rectTransform.anchorMin;
        anchorMax = rectTransform.anchorMax;
        worldPosition = rectTransform.position;
        localPosition = rectTransform.localPosition;
        
        if (parentCanvas == null || canvasRectTransform == null)
        {
            FindParentCanvas();
            if (parentCanvas == null) return;
        }
        
        // Update canvas size in case it changed
        canvasSize = canvasRectTransform.sizeDelta;
        
        // Calculate distances from canvas edges
        CalculateDistancesFromCanvasEdges();
    }
    
    private void CalculateDistancesFromCanvasEdges()
    {
        // Get the world corners of the canvas
        Vector3[] canvasCorners = new Vector3[4];
        canvasRectTransform.GetWorldCorners(canvasCorners);
        
        // Canvas corners: [0] = bottom-left, [1] = top-left, [2] = top-right, [3] = bottom-right
        Vector3 canvasTopLeft = canvasCorners[1];
        Vector3 canvasTopRight = canvasCorners[2];
        Vector3 canvasBottomLeft = canvasCorners[0];
        Vector3 canvasBottomRight = canvasCorners[3];
        
        // Get the center position of our RectTransform in world space
        Vector3 centerWorldPos = rectTransform.position;
        
        // Convert to canvas local space for more accurate calculations
        Vector2 canvasLocalPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRectTransform, 
            RectTransformUtility.WorldToScreenPoint(parentCanvas.worldCamera, centerWorldPos), 
            parentCanvas.worldCamera, 
            out canvasLocalPos
        );
        
        // Canvas local coordinates have (0,0) at center, so we need to adjust
        float canvasWidth = canvasSize.x;
        float canvasHeight = canvasSize.y;
        
        // Convert from center-based coordinates to top-left based coordinates
        float fromLeft = canvasLocalPos.x + (canvasWidth * 0.5f);
        float fromTop = (canvasHeight * 0.5f) - canvasLocalPos.y;
        float fromRight = canvasWidth - fromLeft;
        float fromBottom = canvasHeight - fromTop;
        
        // Update the values
        distanceFromLeft = fromLeft;
        distanceFromTop = fromTop;
        distanceFromRight = fromRight;
        distanceFromBottom = fromBottom;
    }
    
    // Method to manually update - useful for editor scripts
    [ContextMenu("Update Position Info")]
    public void ManualUpdate()
    {
        Initialize();
        UpdatePositionInfo();
    }
    
    // Method to copy position values to clipboard (editor only)
    [ContextMenu("Copy Position to Clipboard")]
    public void CopyPositionToClipboard()
    {
        string posInfo = $"Distance from Left: {distanceFromLeft:F2}\n" +
                        $"Distance from Top: {distanceFromTop:F2}\n" +
                        $"Distance from Right: {distanceFromRight:F2}\n" +
                        $"Distance from Bottom: {distanceFromBottom:F2}\n" +
                        $"Anchored Position: ({anchoredPosition.x:F2}, {anchoredPosition.y:F2})\n" +
                        $"Canvas Size: ({canvasSize.x:F2}, {canvasSize.y:F2})";
        
        GUIUtility.systemCopyBuffer = posInfo;
        Debug.Log("Position info copied to clipboard:\n" + posInfo);
    }
    
    // Method to set another RectTransform to match this position
    public void ApplyPositionToRectTransform(RectTransform targetRect)
    {
        if (targetRect == null) return;
        
        // Set the target to use the same anchor setup and position
        targetRect.anchorMin = anchorMin;
        targetRect.anchorMax = anchorMax;
        targetRect.anchoredPosition = anchoredPosition;
        
        Debug.Log($"Applied position to {targetRect.name}: AnchoredPos({anchoredPosition.x:F2}, {anchoredPosition.y:F2})");
    }
    
    void OnDrawGizmos()
    {
        if (!showGizmos || rectTransform == null) return;
        
        Gizmos.color = gizmoColor;
        
        // Draw a cross at the center
        Vector3 pos = transform.position;
        float size = 20f;
        
        Gizmos.DrawLine(pos + Vector3.left * size, pos + Vector3.right * size);
        Gizmos.DrawLine(pos + Vector3.up * size, pos + Vector3.down * size);
        
        // Draw distance lines if we have canvas info
        if (parentCanvas != null && canvasRectTransform != null)
        {
            Vector3[] canvasCorners = new Vector3[4];
            canvasRectTransform.GetWorldCorners(canvasCorners);
            
            Vector3 canvasTopLeft = canvasCorners[1];
            Vector3 canvasTopRight = canvasCorners[2];
            Vector3 canvasBottomLeft = canvasCorners[0];
            
            // Draw distance lines
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.5f);
            
            // Line to top edge
            Vector3 topPoint = new Vector3(pos.x, canvasTopLeft.y, pos.z);
            Gizmos.DrawLine(pos, topPoint);
            
            // Line to left edge
            Vector3 leftPoint = new Vector3(canvasTopLeft.x, pos.y, pos.z);
            Gizmos.DrawLine(pos, leftPoint);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (rectTransform == null) return;
        
        // Draw more detailed info when selected
        Gizmos.color = Color.red;
        Vector3 pos = transform.position;
        
        // Draw a larger cross
        float size = 50f;
        Gizmos.DrawLine(pos + Vector3.left * size, pos + Vector3.right * size);
        Gizmos.DrawLine(pos + Vector3.up * size, pos + Vector3.down * size);
        
        // Draw rect bounds
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        
        Gizmos.color = Color.cyan;
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
        }
    }
} 