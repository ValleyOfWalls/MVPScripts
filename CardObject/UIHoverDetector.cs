using UnityEngine;
using UnityEngine.EventSystems;
using FishNet.Object;

/// <summary>
/// Centralized hover detection for card objects. Determines if card is self-owned and
/// delegates hover events to appropriate components (CardAnimator and SourceAndTargetIdentifier).
/// Attach to: Card prefabs alongside other card components.
/// </summary>
public class UIHoverDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Debug")]
    [SerializeField] private bool debugLogEnabled = true;
    
    // Component references
    private Card card;
    private CardAnimator cardAnimator;
    private SourceAndTargetIdentifier sourceAndTargetIdentifier;
    private NetworkBehaviour networkBehaviour;
    private HandAnimator handAnimator;
    
    // State tracking
    private bool isSelfOwned = false;
    private bool isInitialized = false;
    
    private void Awake()
    {
        InitializeComponents();
    }
    
    private void Start()
    {
        // Double-check initialization in Start in case components weren't ready in Awake
        if (!isInitialized)
        {
            InitializeComponents();
        }
        
        DetermineOwnership();
    }
    
    private void InitializeComponents()
    {
        // Get required components
        card = GetComponent<Card>();
        cardAnimator = GetComponent<CardAnimator>();
        sourceAndTargetIdentifier = GetComponent<SourceAndTargetIdentifier>();
        networkBehaviour = GetComponent<NetworkBehaviour>();
        
        // NOTE: Don't cache HandAnimator here since card might not be in proper parent hierarchy yet
        // Will find it dynamically during hover
        
        ValidateComponents();
        isInitialized = true;
        
        LogDebug($"UIHoverDetector initialized on {gameObject.name}");
    }
    
    private void ValidateComponents()
    {
        if (card == null)
        {
            Debug.LogWarning($"UIHoverDetector on {gameObject.name}: Missing Card component - ownership detection may not work properly");
        }
        
        if (cardAnimator == null)
        {
            LogDebug($"UIHoverDetector on {gameObject.name}: No CardAnimator found - hover animations will be skipped");
        }
        
        if (sourceAndTargetIdentifier == null)
        {
            LogDebug($"UIHoverDetector on {gameObject.name}: No SourceAndTargetIdentifier found - targeting will be skipped");
        }
        
        if (networkBehaviour == null)
        {
            Debug.LogWarning($"UIHoverDetector on {gameObject.name}: Missing NetworkBehaviour component - ownership detection may not work properly");
        }
        
        // NOTE: HandAnimator validation removed since we find it dynamically during hover
    }
    
    /// <summary>
    /// Determines if this card is owned by the local player
    /// </summary>
    private void DetermineOwnership()
    {
        isSelfOwned = false;
        
        // Method 1: Check via NetworkBehaviour.IsOwner
        if (networkBehaviour != null && networkBehaviour.IsOwner)
        {
            isSelfOwned = true;
            LogDebug($"Ownership determined via NetworkBehaviour.IsOwner: {gameObject.name} is self-owned");
            return;
        }
        
        // Method 2: Check via Card.CanBePlayedByLocalPlayer()
        if (card != null && card.CanBePlayedByLocalPlayer())
        {
            isSelfOwned = true;
            LogDebug($"Ownership determined via Card.CanBePlayedByLocalPlayer: {gameObject.name} is self-owned");
            return;
        }
        
        // Method 3: Check hand type by looking at parent hierarchy
        Transform current = transform.parent;
        while (current != null)
        {
            string parentName = current.name.ToLower();
            if (parentName.Contains("player") && !parentName.Contains("opponent"))
            {
                isSelfOwned = true;
                LogDebug($"Ownership determined via parent hierarchy: {gameObject.name} is in player hand ({current.name})");
                return;
            }
            else if (parentName.Contains("pet") || parentName.Contains("opponent"))
            {
                isSelfOwned = false;
                LogDebug($"Ownership determined via parent hierarchy: {gameObject.name} is in opponent/pet hand ({current.name})");
                return;
            }
            current = current.parent;
        }
        
        LogDebug($"Could not determine clear ownership for {gameObject.name} - defaulting to not self-owned");
    }
    
    #region IPointerEnterHandler, IPointerExitHandler
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        LogDebug($"OnPointerEnter called for {gameObject.name} - mouse entered card area");
        
        // Re-check ownership in case it changed
        DetermineOwnership();
        
        LogDebug($"Processing hover enter for {gameObject.name} - isSelfOwned: {isSelfOwned}");
        
        // Always handle card layering (bring to front) regardless of ownership
        // This allows enemy cards to pop to top when hovered
        HandAnimator currentHandAnimator = FindHandAnimator();
        if (currentHandAnimator != null && cardAnimator != null)
        {
            LogDebug($"Delegating hover enter to HandAnimator for card layering - {gameObject.name} (owner: {isSelfOwned})");
            currentHandAnimator.BringCardToFront(cardAnimator);
        }
        
        // Ensure CardAnimator has up-to-date HandLayoutManager reference for all cards
        if (cardAnimator != null)
        {
            cardAnimator.RefreshLayoutManager();
        }
        
        // Always handle card animations regardless of ownership
        // This allows enemy cards to animate on hover
        if (cardAnimator != null)
        {
            LogDebug($"Delegating hover enter to CardAnimator for {gameObject.name} (owner: {isSelfOwned})");
            cardAnimator.AnimateHoverEnter();
        }
        
        // Only process targeting/damage preview for self-owned cards
        if (!isSelfOwned)
        {
            LogDebug($"Skipping targeting effects for {gameObject.name} - not self-owned (but handled layering and animation)");
            return;
        }
        
        // Delegate to SourceAndTargetIdentifier for targeting/damage preview (self-owned cards only)
        if (sourceAndTargetIdentifier != null)
        {
            LogDebug($"Delegating hover enter to SourceAndTargetIdentifier for {gameObject.name}");
            sourceAndTargetIdentifier.HandlePointerEnter();
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        LogDebug($"OnPointerExit called for {gameObject.name} - mouse left card area");
        
        LogDebug($"Processing hover exit for {gameObject.name} - isSelfOwned: {isSelfOwned}");
        
        // Always handle card layering (restore position) regardless of ownership
        // This restores enemy cards to original position when hover ends
        HandAnimator currentHandAnimator = FindHandAnimator();
        if (currentHandAnimator != null && cardAnimator != null)
        {
            LogDebug($"Delegating hover exit to HandAnimator for card layering - {gameObject.name} (owner: {isSelfOwned})");
            currentHandAnimator.RestoreCardPosition(cardAnimator);
        }
        
        // Always handle card animations regardless of ownership
        // This allows enemy cards to animate on hover exit
        if (cardAnimator != null)
        {
            LogDebug($"Delegating hover exit to CardAnimator for {gameObject.name} (owner: {isSelfOwned})");
            cardAnimator.AnimateHoverExit();
        }
        
        // Only process targeting/damage preview for self-owned cards
        if (!isSelfOwned)
        {
            LogDebug($"Skipping targeting effects for {gameObject.name} - not self-owned (but handled layering and animation)");
            return;
        }
        
        // Delegate to SourceAndTargetIdentifier for targeting/damage preview (self-owned cards only)
        if (sourceAndTargetIdentifier != null)
        {
            LogDebug($"Delegating hover exit to SourceAndTargetIdentifier for {gameObject.name}");
            sourceAndTargetIdentifier.HandlePointerExit();
        }
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Forces a re-check of ownership status
    /// </summary>
    public void RefreshOwnership()
    {
        DetermineOwnership();
        LogDebug($"Ownership refreshed for {gameObject.name} - isSelfOwned: {isSelfOwned}");
    }
    
    /// <summary>
    /// Gets the current ownership status
    /// </summary>
    public bool IsSelfOwned => isSelfOwned;
    
    /// <summary>
    /// Dynamically finds HandAnimator in parent hierarchy at hover time
    /// </summary>
    private HandAnimator FindHandAnimator()
    {
        HandAnimator foundHandAnimator = GetComponentInParent<HandAnimator>();
        
        if (foundHandAnimator != null)
        {
            LogDebug($"Found HandAnimator for {gameObject.name} at: {foundHandAnimator.gameObject.name}");
        }
        else
        {
            LogDebug($"No HandAnimator found in parent hierarchy for {gameObject.name} at hover time");
        }
        
        return foundHandAnimator;
    }
    
    #endregion
    
    #region Debug
    
    private void LogDebug(string message)
    {
        if (debugLogEnabled)
        {
            Debug.Log($"[UIHoverDetector] {message}");
        }
    }
    
    /// <summary>
    /// Test hover enter in editor
    /// </summary>
    [ContextMenu("Test Hover Enter")]
    public void TestHoverEnter()
    {
        if (Application.isPlaying)
        {
            OnPointerEnter(null);
        }
    }
    
    [ContextMenu("Test Hover Exit")]
    public void TestHoverExit()
    {
        if (Application.isPlaying)
        {
            OnPointerExit(null);
        }
    }
    
    [ContextMenu("Refresh Ownership")]
    public void TestRefreshOwnership()
    {
        RefreshOwnership();
    }
    
    [ContextMenu("Test Card Layering")]
    public void TestCardLayering()
    {
        if (Application.isPlaying && cardAnimator != null)
        {
            LogDebug($"Testing card layering for {gameObject.name}");
            LogDebug($"Current sibling index: {transform.GetSiblingIndex()}");
            
            HandAnimator currentHandAnimator = FindHandAnimator();
            if (currentHandAnimator != null)
            {
                currentHandAnimator.BringCardToFront(cardAnimator);
                LogDebug($"After bring to front: {transform.GetSiblingIndex()}");
            }
            else
            {
                LogDebug("No HandAnimator found for testing");
            }
        }
    }
    
    #endregion
} 