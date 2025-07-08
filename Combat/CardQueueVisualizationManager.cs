using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using FishNet.Object;
using FishNet.Connection;
using DG.Tweening;

/// <summary>
/// Manages the visualization of the card queue during combat.
/// Creates and animates tiles showing the order cards will execute.
/// Attach to: A GameObject in the combat scene that handles queue visualization
/// </summary>
public class CardQueueVisualizationManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform queueContainer;
    [SerializeField] private GameObject queueTilePrefab;
    [SerializeField] private CanvasGroup queueVisualizationPanel;
    
    [Header("Layout Settings")]
    [SerializeField] private float tileSpacing = 80f;
    [SerializeField] private bool centerAlignment = true;
    
    [Header("Animation Settings")]
    [SerializeField] private float staggerDelay = 0.15f;
    [SerializeField] private float panelFadeInDuration = 0.3f;
    [SerializeField] private float panelFadeOutDuration = 0.5f;
    [SerializeField] private float panelDelayBeforeFadeOut = 1f;
    
    [Header("Color Settings")]
    [SerializeField] private Color playerCardColor = new Color(0.3f, 0.7f, 1f, 1f); // Light blue
    [SerializeField] private Color petCardColor = new Color(0.9f, 0.6f, 0.2f, 1f); // Orange
    [SerializeField] private Color enemyCardColor = new Color(1f, 0.4f, 0.4f, 1f); // Light red
    [SerializeField] private Color neutralCardColor = new Color(0.8f, 0.8f, 0.8f, 1f); // Gray
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // Private fields
    private List<CardQueueTile> activeTiles = new List<CardQueueTile>();
    private Dictionary<string, CardQueueTile> cardKeyToTileMap = new Dictionary<string, CardQueueTile>();
    private Dictionary<int, string> executionOrderToKeyMap = new Dictionary<int, string>();
    private bool isVisualizationActive = false;
    private Coroutine currentVisualizationCoroutine;
    
    // Events
    public System.Action OnVisualizationStarted;
    public System.Action OnVisualizationCompleted;
    
    #region Initialization
    
    private void Awake()
    {
        // Auto-find components if not assigned
        if (queueContainer == null)
        {
            queueContainer = transform.Find("QueueContainer");
            if (queueContainer == null)
            {
                Debug.LogWarning("CardQueueVisualizationManager: QueueContainer not found, creating one");
                GameObject container = new GameObject("QueueContainer");
                container.transform.SetParent(transform);
                queueContainer = container.transform;
            }
        }
        
        if (queueVisualizationPanel == null)
        {
            queueVisualizationPanel = GetComponent<CanvasGroup>();
        }
        
        // Validate required components
        if (queueTilePrefab == null)
        {
            Debug.LogError("CardQueueVisualizationManager: Queue tile prefab is not assigned!");
        }
        
        // Initialize panel as hidden
        if (queueVisualizationPanel != null)
        {
            queueVisualizationPanel.alpha = 0f;
            queueVisualizationPanel.interactable = false;
            queueVisualizationPanel.blocksRaycasts = false;
        }
        
        LogDebug("CardQueueVisualizationManager: Initialized");
    }
    
    private void Start()
    {
        // Subscribe to combat card queue events
        if (CombatCardQueue.Instance != null)
        {
            SubscribeToQueueEvents();
        }
        else
        {
            LogDebug("CombatCardQueue.Instance not available at Start, will try to subscribe later");
        }
    }
    
    private void SubscribeToQueueEvents()
    {
        CombatCardQueue.OnCardExecutionStarted += OnCardExecutionStarted;
        CombatCardQueue.OnCardExecutionCompleted += OnCardExecutionCompleted;
        CombatCardQueue.OnQueueVisualizationReady += OnQueueVisualizationReady;
        CombatCardQueue.OnCardExecuted += OnCardExecuted;
        LogDebug("Subscribed to all queue events");
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Shows the queue visualization with the provided card execution order
    /// </summary>
    public void ShowQueueVisualization(List<QueuedCardPlay> sortedCardPlays)
    {
        LogDebug($"ShowQueueVisualization called - Active: {isVisualizationActive}, Cards: {sortedCardPlays?.Count ?? 0}");
        LogDebug($"Network Check - IsClient: {IsClientInitialized}, IsServer: {IsServerInitialized}, IsOwner: {IsOwner}");
        
        if (isVisualizationActive)
        {
            LogDebug("Queue visualization already active, clearing current visualization first");
            ClearVisualization();
        }
        
        if (sortedCardPlays == null || sortedCardPlays.Count == 0)
        {
            LogDebug("No cards to visualize in queue");
            return;
        }
        
        LogDebug($"Starting queue visualization with {sortedCardPlays.Count} cards");
        
        if (currentVisualizationCoroutine != null)
        {
            StopCoroutine(currentVisualizationCoroutine);
        }
        
        currentVisualizationCoroutine = StartCoroutine(ShowQueueVisualizationCoroutine(sortedCardPlays));
    }

    /// <summary>
    /// RPC method to show visualization on all clients
    /// </summary>
    [ObserversRpc]
    public void RpcShowQueueVisualization(QueuedCardPlay[] sortedCardPlays)
    {
        LogDebug($"RpcShowQueueVisualization received on client - Cards: {sortedCardPlays?.Length ?? 0}");
        ShowQueueVisualization(sortedCardPlays?.ToList() ?? new List<QueuedCardPlay>());
    }
    
    /// <summary>
    /// Removes the tile for a specific card that has finished executing by execution order
    /// </summary>
    public void RemoveCardByExecutionOrder(int executionIndex)
    {
        if (!isVisualizationActive)
        {
            LogDebug($"Visualization not active, cannot remove card at index: {executionIndex}");
            return;
        }
        
        if (executionOrderToKeyMap.TryGetValue(executionIndex, out string uniqueKey))
        {
            if (cardKeyToTileMap.TryGetValue(uniqueKey, out CardQueueTile tile))
            {
                LogDebug($"Removing tile at execution index {executionIndex} with key '{uniqueKey}'");
                
                // Animate tile out
                tile.AnimateOut(() => {
                    // Clean up tile after animation
                    if (tile != null)
                    {
                        activeTiles.Remove(tile);
                        cardKeyToTileMap.Remove(uniqueKey);
                        executionOrderToKeyMap.Remove(executionIndex);
                        Destroy(tile.gameObject);
                        LogDebug($"Removed tile for execution index {executionIndex}. Remaining tiles: {activeTiles.Count}");
                    }
                    
                    // Check if all tiles are removed
                    if (activeTiles.Count == 0)
                    {
                        LogDebug("All tiles removed, completing visualization");
                        CompleteVisualization();
                    }
                });
            }
            else
            {
                LogDebug($"No tile found for key: {uniqueKey}");
            }
        }
        else
        {
            LogDebug($"No execution order mapping found for index: {executionIndex}");
        }
    }

    /// <summary>
    /// RPC method to remove card by execution order on all clients
    /// </summary>
    [ObserversRpc]
    public void RpcRemoveCardByExecutionOrder(int executionIndex)
    {
        LogDebug($"RpcRemoveCardByExecutionOrder received - Index: {executionIndex}");
        RemoveCardByExecutionOrder(executionIndex);
    }
    
    /// <summary>
    /// Removes the tile for a specific card that has finished executing (legacy method)
    /// </summary>
    public void RemoveCardFromVisualization(string cardName)
    {
        LogDebug($"Legacy removal method called for: {cardName} - using execution order instead");
        // This method is kept for compatibility but we now use execution order
    }
    
    /// <summary>
    /// Clears the entire visualization immediately
    /// </summary>
    public void ClearVisualization()
    {
        LogDebug("Clearing queue visualization");
        
        isVisualizationActive = false;
        
        if (currentVisualizationCoroutine != null)
        {
            StopCoroutine(currentVisualizationCoroutine);
            currentVisualizationCoroutine = null;
        }
        
        // Destroy all active tiles
        foreach (CardQueueTile tile in activeTiles)
        {
            if (tile != null)
            {
                Destroy(tile.gameObject);
            }
        }
        
        activeTiles.Clear();
        cardKeyToTileMap.Clear();
        executionOrderToKeyMap.Clear();
        
        // Hide panel immediately
        if (queueVisualizationPanel != null)
        {
            queueVisualizationPanel.alpha = 0f;
            queueVisualizationPanel.interactable = false;
            queueVisualizationPanel.blocksRaycasts = false;
        }
        
        LogDebug("Queue visualization cleared");
    }

    /// <summary>
    /// RPC method to clear visualization on all clients
    /// </summary>
    [ObserversRpc]
    public void RpcClearVisualization()
    {
        LogDebug("RpcClearVisualization received");
        ClearVisualization();
    }
    
    #endregion
    
    #region Private Implementation
    
    private IEnumerator ShowQueueVisualizationCoroutine(List<QueuedCardPlay> sortedCardPlays)
    {
        // Validation
        string validationResult = ValidateVisualizationSetup();
        if (!string.IsNullOrEmpty(validationResult))
        {
            LogDebug($"Validation failed: {validationResult}");
            yield break;
        }
        
        isVisualizationActive = true;
        OnVisualizationStarted?.Invoke();
        
        // Show the panel with fade-in
        if (queueVisualizationPanel != null)
        {
            queueVisualizationPanel.alpha = 0f;
            queueVisualizationPanel.interactable = true;
            queueVisualizationPanel.blocksRaycasts = true;
            queueVisualizationPanel.DOFade(1f, panelFadeInDuration);
        }
        
        // Create tiles for all cards
        CreateTiles(sortedCardPlays);
        
        // Wait a brief moment for tiles to be created
        yield return new WaitForSeconds(0.1f);
        
        // Animate tiles in with stagger
        yield return StartCoroutine(AnimateTilesIn());
        
        LogDebug("Queue visualization setup complete, waiting for card execution");
    }
    
    private void CreateTiles(List<QueuedCardPlay> sortedCardPlays)
    {
        for (int i = 0; i < sortedCardPlays.Count; i++)
        {
            QueuedCardPlay cardPlay = sortedCardPlays[i];
            string cardName = cardPlay.cardData?.CardName ?? "Unknown Card";
            
            // Create unique key for this card based on execution order
            string uniqueKey = $"{cardName}_{i}";
            
            // Instantiate tile
            GameObject tileObject = Instantiate(queueTilePrefab, queueContainer);
            CardQueueTile tile = tileObject.GetComponent<CardQueueTile>();
            
            if (tile != null)
            {
                // Position the tile BEFORE initializing to ensure originalPosition is correct
                PositionTile(tile, i, sortedCardPlays.Count);
                LogDebug($"Positioned tile {i + 1} at {tile.RectTransform.localPosition} - vertical stack");
                
                // Get entity color based on type
                Color entityColor = GetEntityColor(cardPlay.sourceEntityId);
                
                // Initialize the tile with card data and color
                tile.Initialize(cardName, i + 1, entityColor);
                
                // Store tile references
                activeTiles.Add(tile);
                cardKeyToTileMap[uniqueKey] = tile;
                executionOrderToKeyMap[i] = uniqueKey;
                
                LogDebug($"Created tile {i + 1} for '{cardName}' with key '{uniqueKey}'");
            }
            else
            {
                Debug.LogError($"CardQueueVisualizationManager: CardQueueTile component missing on prefab for {cardName}");
                Destroy(tileObject);
            }
        }
    }

    /// <summary>
    /// Gets the appropriate color for an entity based on its type and relationship to the local player
    /// </summary>
    private Color GetEntityColor(int entityId)
    {
        // Try to find the entity by ID
        NetworkEntity networkEntity = FindEntityById(entityId);
        if (networkEntity != null)
        {
            // Check if this is the local player's entity
            if (networkEntity.IsOwner)
            {
                // Check if it's a pet or player by looking at EntityType or component presence
                if (networkEntity.EntityType == EntityType.Pet || 
                    networkEntity.GetComponent<NetworkPetUI>() != null)
                {
                    return petCardColor;
                }
                else if (networkEntity.EntityType == EntityType.Player ||
                         networkEntity.GetComponent<NetworkPlayerUI>() != null)
                {
                    return playerCardColor;
                }
            }
            else
            {
                // Not owned by local player - could be enemy or other player
                return enemyCardColor;
            }
        }
        
        // Default to neutral color if we can't determine the relationship
        LogDebug($"Could not determine entity type for ID {entityId}, using neutral color");
        return neutralCardColor;
    }

    /// <summary>
    /// Finds a NetworkEntity by its object ID
    /// </summary>
    private NetworkEntity FindEntityById(int entityId)
    {
        if (entityId == 0) return null;
        
        FishNet.Object.NetworkObject netObj = null;
        
        if (IsServerInitialized)
        {
            FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(entityId, out netObj);
        }
        else if (IsClientInitialized)
        {
            FishNet.InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(entityId, out netObj);
        }
        
        return netObj?.GetComponent<NetworkEntity>();
    }
    
    private void PositionTile(CardQueueTile tile, int index, int totalTiles)
    {
        if (tile?.RectTransform == null) return;
        
        // Simple vertical stacking (centered)
        float yPosition = (totalTiles - 1) * tileSpacing * 0.5f - index * tileSpacing;
        Vector3 position = new Vector3(0f, yPosition, 0f);
        
        tile.RectTransform.localPosition = position;
    }
    
    private IEnumerator AnimateTilesIn()
    {
        LogDebug($"Starting staggered tile animations with {staggerDelay}s delay");
        
        foreach (CardQueueTile tile in activeTiles)
        {
            if (tile != null)
            {
                tile.AnimateIn();
                yield return new WaitForSeconds(staggerDelay);
            }
        }
        
        LogDebug("All tiles animated in");
    }
    
    private void CompleteVisualization()
    {
        if (currentVisualizationCoroutine != null)
        {
            StopCoroutine(currentVisualizationCoroutine);
            currentVisualizationCoroutine = null;
        }
        
        StartCoroutine(CompleteVisualizationCoroutine());
    }
    
    private IEnumerator CompleteVisualizationCoroutine()
    {
        LogDebug("Starting visualization completion sequence");
        
        // Wait before fading out
        yield return new WaitForSeconds(panelDelayBeforeFadeOut);
        
        // Fade out panel
        if (queueVisualizationPanel != null)
        {
            queueVisualizationPanel.DOFade(0f, panelFadeOutDuration).OnComplete(() => {
                queueVisualizationPanel.interactable = false;
                queueVisualizationPanel.blocksRaycasts = false;
            });
        }
        
        yield return new WaitForSeconds(panelFadeOutDuration);
        
        isVisualizationActive = false;
        OnVisualizationCompleted?.Invoke();
        
        LogDebug("Queue visualization completed");
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnCardExecutionStarted()
    {
        LogDebug("Card execution started - visualization should be ready");
    }
    
    private void OnCardExecutionCompleted()
    {
        LogDebug("Card execution completed");
        
        // Check if there are still tiles that haven't been removed
        if (activeTiles.Count > 0)
        {
            LogDebug($"Warning: {activeTiles.Count} tiles still active after execution completed");
        }
        
        // Clear any remaining visualization
        ClearVisualization();
    }
    
    private void OnQueueVisualizationReady(List<QueuedCardPlay> sortedCardPlays)
    {
        // Validate UI setup before proceeding
        string uiCheck = ValidateVisualizationSetup();
        LogDebug($"UI Setup Check - {(string.IsNullOrEmpty(uiCheck) ? "All components OK" : uiCheck)}");
        LogDebug($"Network Check - IsClient: {IsClientInitialized}, IsServer: {IsServerInitialized}, IsOwner: {IsOwner}");
        
        // Show visualization on this client
        ShowQueueVisualization(sortedCardPlays);
        
        // If this is the server, also send RPC to all clients
        if (IsServerInitialized)
        {
            LogDebug("Server sending RPC to all clients for queue visualization");
            RpcShowQueueVisualization(sortedCardPlays.ToArray());
        }
    }
    
    private void OnCardExecuted(string cardName, int executionIndex)
    {
        LogDebug($"Card executed: {cardName} (execution index: {executionIndex})");
        
        // Remove the tile by execution order
        RemoveCardByExecutionOrder(executionIndex);
        
        // If this is the server, also send RPC to all clients
        if (IsServerInitialized)
        {
            LogDebug($"Server sending RPC to remove card at index {executionIndex}");
            RpcRemoveCardByExecutionOrder(executionIndex);
        }
    }
    
    #endregion
    
    #region Utility and Validation
    
    public bool IsVisualizationActive => isVisualizationActive;
    public int ActiveTileCount => activeTiles.Count;
    
    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[CardQueueVisualization] {message}");
        }
    }
    
    private string ValidateVisualizationSetup()
    {
        if (queueContainer == null) return "Container: Missing";
        if (queueTilePrefab == null) return "Prefab: Missing";
        if (queueVisualizationPanel == null) return "Panel: Missing";
        return string.Empty;
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (CombatCardQueue.Instance != null)
        {
            CombatCardQueue.OnCardExecutionStarted -= OnCardExecutionStarted;
            CombatCardQueue.OnCardExecutionCompleted -= OnCardExecutionCompleted;
            CombatCardQueue.OnQueueVisualizationReady -= OnQueueVisualizationReady;
            CombatCardQueue.OnCardExecuted -= OnCardExecuted;
        }
        
        // Stop any running animations
        if (currentVisualizationCoroutine != null)
        {
            StopCoroutine(currentVisualizationCoroutine);
        }
        
        // Clean up DOTween sequences
        DOTween.Kill(this);
    }
    
    protected override void OnValidate()
    {
        base.OnValidate();
        
        // Ensure positive values
        tileSpacing = Mathf.Max(0f, tileSpacing);
        staggerDelay = Mathf.Max(0f, staggerDelay);
        panelFadeInDuration = Mathf.Max(0f, panelFadeInDuration);
        panelFadeOutDuration = Mathf.Max(0f, panelFadeOutDuration);
        panelDelayBeforeFadeOut = Mathf.Max(0f, panelDelayBeforeFadeOut);
    }
    
    #endregion
} 