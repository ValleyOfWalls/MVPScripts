using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Represents a single card tile in the queue visualization.
/// Shows card name and animates in/out during queue execution.
/// Attach to: Card queue tile UI prefabs
/// </summary>
public class CardQueueTile : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI cardNameText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform rectTransform;
    
    [Header("Animation Settings")]
    [SerializeField] private float animateInDuration = 0.4f;
    [SerializeField] private float animateOutDuration = 0.3f;
    [SerializeField] private Vector3 animateInStartOffset = new Vector3(0, -100f, 0);
    [SerializeField] private Vector3 animateOutEndOffset = new Vector3(0, 100f, 0);
    [SerializeField] private Ease animateInEase = Ease.OutBack;
    [SerializeField] private Ease animateOutEase = Ease.InBack;
    
    [Header("Visual Settings")]
    [SerializeField] private Color defaultBackgroundColor = Color.white;
    [SerializeField] private Color highlightBackgroundColor = Color.yellow;
    [SerializeField] private float highlightDuration = 0.2f;
    
    // Private fields
    private Vector3 originalPosition;
    private bool isAnimating = false;
    private Sequence currentAnimation;
    private string associatedCardName;
    
    // Events
    public System.Action<CardQueueTile> OnAnimateInComplete;
    public System.Action<CardQueueTile> OnAnimateOutComplete;
    
    #region Initialization
    
    private void Awake()
    {
        // Auto-find components if not assigned
        if (cardNameText == null)
            cardNameText = GetComponentInChildren<TextMeshProUGUI>();
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
        
        // Validate required components
        if (rectTransform == null)
        {
            Debug.LogError($"CardQueueTile: RectTransform is required on {gameObject.name}");
        }
    }
    
    /// <summary>
    /// Initializes the tile with card data and optional color
    /// </summary>
    public void Initialize(string cardName, int executionOrder = -1, Color? backgroundColor = null)
    {
        associatedCardName = cardName;
        
        if (cardNameText != null)
        {
            string displayText = executionOrder >= 0 ? $"{executionOrder}. {cardName}" : cardName;
            cardNameText.text = displayText;
        }
        else
        {
            Debug.LogWarning($"CardQueueTile: cardNameText is null for '{cardName}'");
        }
        
        if (backgroundImage != null)
        {
            // Use provided color or default
            Color colorToUse = backgroundColor ?? defaultBackgroundColor;
            backgroundImage.color = colorToUse;
            
            // Store the color as the new default for this tile
            defaultBackgroundColor = colorToUse;
        }
        else
        {
            Debug.LogWarning($"CardQueueTile: backgroundImage is null for '{cardName}'");
        }
        
        // Set initial state (hidden)
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
        else
        {
            Debug.LogWarning($"CardQueueTile: canvasGroup is null for '{cardName}'");
        }
        
        originalPosition = rectTransform.localPosition;
        
        Debug.Log($"CardQueueTile: Initialized tile for '{cardName}' with execution order {executionOrder} and color {(backgroundColor?.ToString() ?? "default")}");
    }
    
    #endregion
    
    #region Animation Methods
    
    /// <summary>
    /// Animates the tile sliding in from the specified start position
    /// </summary>
    public void AnimateIn(System.Action onComplete = null)
    {
        if (isAnimating)
        {
            Debug.LogWarning($"CardQueueTile: Already animating tile for '{associatedCardName}'");
            return;
        }
        
        Debug.Log($"CardQueueTile: Animating in tile for '{associatedCardName}'");
        
        isAnimating = true;
        
        // Set starting state
        Vector3 startPosition = originalPosition + animateInStartOffset;
        rectTransform.localPosition = startPosition;
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
        
        // Create animation sequence
        currentAnimation = DOTween.Sequence();
        
        // Animate position and alpha simultaneously
        currentAnimation.Append(rectTransform.DOLocalMove(originalPosition, animateInDuration).SetEase(animateInEase));
        
        if (canvasGroup != null)
        {
            currentAnimation.Join(canvasGroup.DOFade(1f, animateInDuration).SetEase(Ease.OutQuad));
        }
        
        currentAnimation.OnComplete(() => {
            isAnimating = false;
            currentAnimation = null;
            OnAnimateInComplete?.Invoke(this);
            onComplete?.Invoke();
            Debug.Log($"CardQueueTile: Animate in completed for '{associatedCardName}'");
        });
    }
    
    /// <summary>
    /// Animates the tile sliding out to the specified end position
    /// </summary>
    public void AnimateOut(System.Action onComplete = null)
    {
        if (isAnimating)
        {
            Debug.LogWarning($"CardQueueTile: Already animating tile for '{associatedCardName}'");
            return;
        }
        
        Debug.Log($"CardQueueTile: Animating out tile for '{associatedCardName}'");
        
        isAnimating = true;
        
        // Calculate end position
        Vector3 endPosition = originalPosition + animateOutEndOffset;
        
        // Create animation sequence
        currentAnimation = DOTween.Sequence();
        
        // Optional highlight before animating out
        if (backgroundImage != null)
        {
            currentAnimation.Append(backgroundImage.DOColor(highlightBackgroundColor, highlightDuration * 0.5f));
            currentAnimation.Append(backgroundImage.DOColor(defaultBackgroundColor, highlightDuration * 0.5f));
        }
        
        // Animate position and alpha simultaneously
        currentAnimation.Append(rectTransform.DOLocalMove(endPosition, animateOutDuration).SetEase(animateOutEase));
        
        if (canvasGroup != null)
        {
            currentAnimation.Join(canvasGroup.DOFade(0f, animateOutDuration).SetEase(Ease.InQuad));
        }
        
        currentAnimation.OnComplete(() => {
            isAnimating = false;
            currentAnimation = null;
            OnAnimateOutComplete?.Invoke(this);
            onComplete?.Invoke();
            Debug.Log($"CardQueueTile: Animate out completed for '{associatedCardName}'");
        });
    }
    
    /// <summary>
    /// Immediately shows the tile without animation
    /// </summary>
    public void ShowImmediately()
    {
        StopCurrentAnimation();
        
        rectTransform.localPosition = originalPosition;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
        if (backgroundImage != null)
        {
            backgroundImage.color = defaultBackgroundColor;
        }
    }
    
    /// <summary>
    /// Immediately hides the tile without animation
    /// </summary>
    public void HideImmediately()
    {
        StopCurrentAnimation();
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }
    
    /// <summary>
    /// Stops any current animation
    /// </summary>
    public void StopCurrentAnimation()
    {
        if (currentAnimation != null && currentAnimation.IsActive())
        {
            currentAnimation.Kill();
            currentAnimation = null;
        }
        isAnimating = false;
    }
    
    #endregion
    
    #region Properties
    
    public string CardName => associatedCardName;
    public bool IsAnimating => isAnimating;
    public RectTransform RectTransform => rectTransform;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void OnDestroy()
    {
        StopCurrentAnimation();
        
        // Clear events
        OnAnimateInComplete = null;
        OnAnimateOutComplete = null;
    }
    
    private void OnValidate()
    {
        // Validate animation settings
        animateInDuration = Mathf.Max(0.1f, animateInDuration);
        animateOutDuration = Mathf.Max(0.1f, animateOutDuration);
        highlightDuration = Mathf.Max(0.1f, highlightDuration);
    }
    
    #endregion
} 