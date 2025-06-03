using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Represents a drop zone where cards can be dropped to target entities.
/// Attach to: GameObjects with NetworkEntity or NetworkEntityUI components.
/// </summary>
public class DropZone : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private NetworkEntity targetEntity;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject highlightOverlay;
    [SerializeField] private Color validTargetColor = Color.green;
    [SerializeField] private Color invalidTargetColor = Color.red;
    [SerializeField] private float highlightAlpha = 0.3f;
    
    [Header("Auto Setup")]
    [SerializeField] private bool autoSetupCollider = true;
    [SerializeField] private bool autoCreateHighlight = true;
    
    [Header("Debug")]
    [SerializeField] private bool debugLogEnabled = true;
    
    // Components
    private Collider2D dropCollider;
    private Image highlightImage;
    private bool isHighlighted = false;
    
    private void Awake()
    {
        SetupDropZone();
    }
    
    private void SetupDropZone()
    {
        // Try to find target entity
        if (targetEntity == null)
        {
            targetEntity = GetComponent<NetworkEntity>();
            
            // If still null, try to get it from NetworkEntityUI
            if (targetEntity == null)
            {
                NetworkEntityUI entityUI = GetComponent<NetworkEntityUI>();
                if (entityUI != null)
                {
                    // The NetworkEntityUI should be on the same GameObject as NetworkEntity
                    targetEntity = entityUI.GetComponent<NetworkEntity>();
                }
            }
            
            // For OwnPetVisualDisplay, we need to get the entity differently
            if (targetEntity == null)
            {
                OwnPetVisualDisplay petDisplay = GetComponent<OwnPetVisualDisplay>();
                if (petDisplay != null)
                {
                    targetEntity = petDisplay.GetCurrentPet();
                }
            }
        }
        
        // Setup collider for drop detection
        if (autoSetupCollider)
        {
            SetupCollider();
        }
        
        // Setup highlight overlay
        if (autoCreateHighlight)
        {
            SetupHighlightOverlay();
        }
        
        if (targetEntity != null)
        {
            LogDebug($"Drop zone setup for entity: {targetEntity.EntityName.Value}");
        }
        else
        {
            LogDebug("Drop zone setup but no target entity found");
        }
    }
    
    private void SetupCollider()
    {
        dropCollider = GetComponent<Collider2D>();
        
        if (dropCollider == null)
        {
            // Try to find a child with a collider (like an Image)
            Image image = GetComponentInChildren<Image>();
            if (image != null)
            {
                LogDebug($"Found Image component on child: {image.gameObject.name}");
                
                dropCollider = image.GetComponent<Collider2D>();
                if (dropCollider == null)
                {
                    // Add collider to the image GameObject (this is where raycast detection should happen)
                    dropCollider = image.gameObject.AddComponent<BoxCollider2D>();
                    LogDebug($"Added BoxCollider2D to Image child: {image.gameObject.name}");
                }
                
                // Ensure the Image GameObject can receive raycasts
                image.raycastTarget = true;
                LogDebug($"Set raycastTarget=true on Image: {image.gameObject.name}");
            }
            else
            {
                // Add collider to this GameObject
                dropCollider = gameObject.AddComponent<BoxCollider2D>();
                LogDebug("Added BoxCollider2D to main GameObject");
            }
        }
        
        // Make sure it's set up for UI interaction
        if (dropCollider != null)
        {
            dropCollider.isTrigger = true;
            
            // For UI elements, we need a GraphicRaycaster on the Canvas
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null)
                {
                    raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
                    LogDebug("Added GraphicRaycaster to Canvas");
                }
                
                LogDebug($"Canvas found: {canvas.name}, GraphicRaycaster present: {raycaster != null}");
            }
            else
            {
                LogDebug("Warning: No Canvas found in parents - UI raycasting may not work");
            }
            
            LogDebug($"Collider setup complete on: {dropCollider.gameObject.name}, isTrigger: {dropCollider.isTrigger}");
        }
    }
    
    private void SetupHighlightOverlay()
    {
        LogDebug($"SetupHighlightOverlay called - autoCreateHighlight: {autoCreateHighlight}, existing highlightOverlay: {(highlightOverlay != null ? "exists" : "null")}");
        
        if (highlightOverlay == null)
        {
            // Find the child Image GameObject to attach the highlight to
            Image targetImage = GetComponentInChildren<Image>();
            Transform parentForHighlight = targetImage != null ? targetImage.transform : transform;
            
            LogDebug($"Creating highlight overlay on: {parentForHighlight.name} (hasImage: {targetImage != null})");
            
            // Create a highlight overlay
            GameObject overlay = new GameObject("DropZone_Highlight");
            overlay.transform.SetParent(parentForHighlight, false);
            
            // Add Image component
            highlightImage = overlay.AddComponent<Image>();
            highlightImage.color = new Color(validTargetColor.r, validTargetColor.g, validTargetColor.b, 0f);
            
            // Make it cover the entire area
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            
            // Disable raycast blocking so it doesn't interfere with drop detection
            highlightImage.raycastTarget = false;
            
            highlightOverlay = overlay;
            overlay.SetActive(false);
            
            LogDebug($"Created highlight overlay: {overlay.name}, parent: {parentForHighlight.name}, raycastTarget: {highlightImage.raycastTarget}");
        }
        else
        {
            highlightImage = highlightOverlay.GetComponent<Image>();
            LogDebug($"Using existing highlight overlay: {highlightOverlay.name}");
        }
        
        // Verify the setup
        if (highlightOverlay != null && highlightImage != null)
        {
            LogDebug($"Highlight setup complete - overlay exists: {highlightOverlay != null}, image exists: {highlightImage != null}, initial color: {highlightImage.color}");
        }
        else
        {
            LogDebug("Warning: Highlight setup incomplete!");
        }
    }
    
    /// <summary>
    /// Called when a card starts being dragged over this drop zone
    /// </summary>
    /// <param name="cardDragDrop">The card being dragged</param>
    /// <param name="isValidTarget">Whether the card can target this entity</param>
    public void OnCardDragEnter(CardDragDrop cardDragDrop, bool isValidTarget)
    {
        ShowHighlight(isValidTarget);
        
        LogDebug($"Card drag enter - valid target: {isValidTarget}");
    }
    
    /// <summary>
    /// Called when a card stops being dragged over this drop zone
    /// </summary>
    public void OnCardDragExit()
    {
        HideHighlight();
        
        LogDebug("Card drag exit");
    }
    
    /// <summary>
    /// Shows the highlight overlay with appropriate color
    /// </summary>
    /// <param name="isValidTarget">Whether to show valid or invalid color</param>
    private void ShowHighlight(bool isValidTarget)
    {
        LogDebug($"ShowHighlight called - isValidTarget: {isValidTarget}, highlightOverlay: {(highlightOverlay != null ? "exists" : "null")}, highlightImage: {(highlightImage != null ? "exists" : "null")}");
        
        if (highlightOverlay == null || highlightImage == null) 
        {
            LogDebug("Cannot show highlight - missing overlay or image components");
            return;
        }
        
        Color targetColor = isValidTarget ? validTargetColor : invalidTargetColor;
        highlightImage.color = new Color(targetColor.r, targetColor.g, targetColor.b, highlightAlpha);
        
        highlightOverlay.SetActive(true);
        isHighlighted = true;
        
        LogDebug($"Highlight shown - color: {targetColor}, alpha: {highlightAlpha}, overlay active: {highlightOverlay.activeInHierarchy}");
    }
    
    /// <summary>
    /// Hides the highlight overlay
    /// </summary>
    private void HideHighlight()
    {
        LogDebug($"HideHighlight called - highlightOverlay: {(highlightOverlay != null ? "exists" : "null")}");
        
        if (highlightOverlay != null)
        {
            highlightOverlay.SetActive(false);
            LogDebug($"Highlight hidden - overlay active: {highlightOverlay.activeInHierarchy}");
        }
        
        isHighlighted = false;
    }
    
    /// <summary>
    /// Gets the target entity for this drop zone
    /// </summary>
    /// <returns>The NetworkEntity that cards dropped here will target</returns>
    public NetworkEntity GetTargetEntity()
    {
        // For OwnPetVisualDisplay, always get the current pet dynamically
        OwnPetVisualDisplay petDisplay = GetComponent<OwnPetVisualDisplay>();
        if (petDisplay != null)
        {
            return petDisplay.GetCurrentPet();
        }
        
        // For other cases, use the cached targetEntity
        return targetEntity;
    }
    
    /// <summary>
    /// Manually sets the target entity for this drop zone
    /// </summary>
    /// <param name="entity">The NetworkEntity to target</param>
    public void SetTargetEntity(NetworkEntity entity)
    {
        targetEntity = entity;
        LogDebug($"Target entity set to: {entity?.EntityName.Value ?? "null"}");
    }
    
    /// <summary>
    /// Checks if this drop zone is currently highlighted
    /// </summary>
    /// <returns>True if highlighted</returns>
    public bool IsHighlighted()
    {
        return isHighlighted;
    }
    
    private void LogDebug(string message)
    {
        if (debugLogEnabled)
        {
            Debug.Log($"[DropZone] {gameObject.name}: {message}");
        }
    }
    
    /// <summary>
    /// Debug method to help track raycast detection
    /// </summary>
    public void DebugRaycastInfo()
    {
        LogDebug("=== DropZone Raycast Debug Info ===");
        LogDebug($"GameObject: {gameObject.name}");
        LogDebug($"Has DropZone: {GetComponent<DropZone>() != null}");
        LogDebug($"DropCollider: {(dropCollider != null ? $"{dropCollider.gameObject.name} (enabled: {dropCollider.enabled})" : "null")}");
        
        Image childImage = GetComponentInChildren<Image>();
        if (childImage != null)
        {
            LogDebug($"Child Image: {childImage.gameObject.name} (raycastTarget: {childImage.raycastTarget})");
            BoxCollider2D imageCollider = childImage.GetComponent<BoxCollider2D>();
            LogDebug($"Image Collider: {(imageCollider != null ? $"present (enabled: {imageCollider.enabled})" : "missing")}");
        }
        else
        {
            LogDebug("No child Image found");
        }
        
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            LogDebug($"Canvas: {canvas.name}, GraphicRaycaster: {(raycaster != null ? "present" : "missing")}");
        }
        else
        {
            LogDebug("No parent Canvas found");
        }
        
        LogDebug($"Target Entity: {(GetTargetEntity()?.EntityName.Value ?? "null")}");
        LogDebug("=== End Debug Info ===");
    }
    
    private void OnDestroy()
    {
        // Clean up any created highlight overlay
        if (highlightOverlay != null && highlightOverlay.transform.parent == transform)
        {
            if (Application.isPlaying)
            {
                Destroy(highlightOverlay);
            }
            else
            {
                DestroyImmediate(highlightOverlay);
            }
        }
    }
} 