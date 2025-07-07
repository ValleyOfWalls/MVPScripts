using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages the UI elements for the fight conclusion screen that shows results after all fights complete.
/// Displays local player's result prominently and summarizes all other fight results.
/// Now focuses solely on UI management, with animations handled by FightConclusionAnimator.
/// Attach to: A NetworkObject that contains the fight conclusion UI elements.
/// </summary>
public class FightConclusionUIManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject fightConclusionCanvas;
    [SerializeField] private TextMeshProUGUI localPlayerResultText;
    [SerializeField] private TextMeshProUGUI localPlayerNameText;
    [SerializeField] private TextMeshProUGUI localOpponentNameText;
    [SerializeField] private Image localPlayerImage;
    [SerializeField] private Image localOpponentImage;
    [SerializeField] private GameObject localResultPanel;

    [Header("Summary UI")]
    [SerializeField] private TextMeshProUGUI summaryTitleText;
    [SerializeField] private Transform otherResultsContainer;
    [SerializeField] private GameObject otherResultEntryPrefab;
    [SerializeField] private ScrollRect otherResultsScrollRect;

    [Header("Animation Component")]
    [SerializeField] private FightConclusionAnimator animator;

    private CanvasGroup canvasGroup;
    private FightManager fightManager;
    private FightConclusionManager fightConclusionManager;

    // Events for external systems
    public event System.Action OnConclusionStarted;
    public event System.Action OnConclusionCompleted;

    #region Lifecycle

    private void Awake()
    {
        FindRequiredComponents();
        InitializeCanvas();
        SetupAnimationEvents();
    }

    private void OnDestroy()
    {
        CleanupAnimationEvents();
    }

    #endregion

    #region Initialization

    private void FindRequiredComponents()
    {
        if (fightManager == null)
        {
            fightManager = FindFirstObjectByType<FightManager>();
        }

        if (fightConclusionManager == null)
        {
            fightConclusionManager = FindFirstObjectByType<FightConclusionManager>();
        }

        // Get or add CanvasGroup for animations
        if (fightConclusionCanvas != null)
        {
            canvasGroup = fightConclusionCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = fightConclusionCanvas.AddComponent<CanvasGroup>();
            }
        }

        // Find animator if not assigned
        if (animator == null)
        {
            animator = GetComponent<FightConclusionAnimator>();
            if (animator == null)
            {
                animator = FindFirstObjectByType<FightConclusionAnimator>();
            }
        }
    }

    private void InitializeCanvas()
    {
        // Initially hide the canvas
        if (fightConclusionCanvas != null)
        {
            fightConclusionCanvas.SetActive(false);
        }

        // Initialize summary title
        if (summaryTitleText != null)
        {
            summaryTitleText.text = "All Fight Results";
        }

        // Set initial canvas group state
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    private void SetupAnimationEvents()
    {
        if (animator != null)
        {
            animator.OnAnimationStarted += HandleAnimationStarted;
            animator.OnDisplayPhaseStarted += HandleDisplayPhaseStarted;
            animator.OnAnimationCompleted += HandleAnimationCompleted;
        }
    }

    private void CleanupAnimationEvents()
    {
        if (animator != null)
        {
            animator.OnAnimationStarted -= HandleAnimationStarted;
            animator.OnDisplayPhaseStarted -= HandleDisplayPhaseStarted;
            animator.OnAnimationCompleted -= HandleAnimationCompleted;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Shows the fight conclusion with all fight results
    /// </summary>
    public void ShowFightConclusion()
    {
        if (!IsClientStarted)
        {
            Debug.LogWarning("FightConclusionUIManager: Cannot show conclusion - client not started");
            return;
        }

        // Get fight results data
        var fightResults = GetFightResultsData();
        if (fightResults == null || fightResults.Count == 0)
        {
            Debug.LogError("FightConclusionUIManager: No fight results data available");
            return;
        }

        // Update UI with fight results
        UpdateConclusionUI(fightResults);

        // Show canvas and start animation
        if (fightConclusionCanvas != null)
        {
            fightConclusionCanvas.SetActive(true);
        }

        StartConclusionAnimation();
    }

    /// <summary>
    /// Hides the fight conclusion immediately (for emergency cleanup)
    /// </summary>
    public void HideFightConclusion()
    {
        if (animator != null && animator.IsAnimating)
        {
            animator.StopCurrentAnimation();
        }

        if (animator != null && canvasGroup != null)
        {
            animator.HideImmediately(canvasGroup, GetAnimationTransform());
        }
        else if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        if (fightConclusionCanvas != null)
        {
            fightConclusionCanvas.SetActive(false);
        }
    }

    /// <summary>
    /// Public method to check if the conclusion is currently showing
    /// </summary>
    public bool IsConclusionShowing()
    {
        return fightConclusionCanvas != null && fightConclusionCanvas.activeSelf && 
               (animator == null || animator.IsAnimating || canvasGroup.alpha > 0f);
    }

    /// <summary>
    /// Gets the total duration of the conclusion (including animations)
    /// </summary>
    public float GetConclusionDuration()
    {
        return animator != null ? animator.TotalDuration : 5f;
    }

    #endregion

    #region UI Management

    /// <summary>
    /// Updates the UI with all fight results
    /// </summary>
    private void UpdateConclusionUI(Dictionary<NetworkEntity, bool> fightResults)
    {
        // Update local player result
        UpdateLocalPlayerResult(fightResults);

        // Update other results summary
        UpdateOtherResultsSummary(fightResults);
    }

    /// <summary>
    /// Updates the local player's result display
    /// </summary>
    private void UpdateLocalPlayerResult(Dictionary<NetworkEntity, bool> fightResults)
    {
        var localResult = GetLocalPlayerResult(fightResults);
        if (!localResult.HasValue)
        {
            Debug.LogWarning("FightConclusionUIManager: Could not find local player result");
            return;
        }

        var (player, opponent, playerWon) = localResult.Value;

        // Update result text
        if (localPlayerResultText != null)
        {
            localPlayerResultText.text = playerWon ? "VICTORY!" : "DEFEAT";
            localPlayerResultText.color = playerWon ? Color.green : Color.red;
        }

        // Update player name
        if (localPlayerNameText != null)
        {
            localPlayerNameText.text = player.EntityName.Value;
        }

        // Update opponent name
        if (localOpponentNameText != null)
        {
            localOpponentNameText.text = opponent.EntityName.Value;
        }

        // Update images
        UpdateLocalPlayerImages(player, opponent);

        /* Debug.Log($"FightConclusionUIManager: Local player result - {player.EntityName.Value} vs {opponent.EntityName.Value}: {(playerWon ? "Won" : "Lost")}"); */
    }

    /// <summary>
    /// Updates the local player and opponent images
    /// </summary>
    private void UpdateLocalPlayerImages(NetworkEntity player, NetworkEntity opponent)
    {
        if (localPlayerImage != null)
        {
            Sprite playerSprite = GetEntitySprite(player);
            if (playerSprite != null)
            {
                localPlayerImage.sprite = playerSprite;
                localPlayerImage.gameObject.SetActive(true);
            }
            else
            {
                localPlayerImage.gameObject.SetActive(false);
            }
        }

        if (localOpponentImage != null)
        {
            Sprite opponentSprite = GetEntitySprite(opponent);
            if (opponentSprite != null)
            {
                localOpponentImage.sprite = opponentSprite;
                localOpponentImage.gameObject.SetActive(true);
            }
            else
            {
                localOpponentImage.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Updates the summary of other players' results
    /// </summary>
    private void UpdateOtherResultsSummary(Dictionary<NetworkEntity, bool> fightResults)
    {
        if (otherResultsContainer == null || otherResultEntryPrefab == null)
        {
            Debug.LogWarning("FightConclusionUIManager: Other results container or prefab not assigned");
            return;
        }

        // Clear existing entries
        foreach (Transform child in otherResultsContainer)
        {
            Destroy(child.gameObject);
        }

        // Get local player to exclude from summary
        NetworkEntity localPlayer = GetLocalPlayer();

        // Create entries for other players
        foreach (var kvp in fightResults)
        {
            NetworkEntity player = kvp.Key;
            bool playerWon = kvp.Value;

            // Skip local player - they have their own dedicated display
            if (localPlayer != null && player == localPlayer)
                continue;

            // Get opponent for this player
            NetworkEntity opponent = fightManager.GetOpponentForPlayer(player);
            if (opponent == null)
                continue;

            // Create result entry
            CreateOtherResultEntry(player, opponent, playerWon);
        }

        /* Debug.Log($"FightConclusionUIManager: Created {otherResultsContainer.childCount} other result entries"); */
    }

    /// <summary>
    /// Creates a result entry for another player's fight
    /// </summary>
    private void CreateOtherResultEntry(NetworkEntity player, NetworkEntity opponent, bool playerWon)
    {
        GameObject entry = Instantiate(otherResultEntryPrefab, otherResultsContainer);

        // Find UI elements in the entry (assuming they follow naming conventions)
        TextMeshProUGUI playerNameText = entry.transform.Find("PlayerName")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI opponentNameText = entry.transform.Find("OpponentName")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI resultText = entry.transform.Find("Result")?.GetComponent<TextMeshProUGUI>();
        Image playerImage = entry.transform.Find("PlayerImage")?.GetComponent<Image>();
        Image opponentImage = entry.transform.Find("OpponentImage")?.GetComponent<Image>();

        // Update text elements
        if (playerNameText != null)
        {
            playerNameText.text = player.EntityName.Value;
        }

        if (opponentNameText != null)
        {
            opponentNameText.text = opponent.EntityName.Value;
        }

        if (resultText != null)
        {
            resultText.text = playerWon ? "Won" : "Lost";
            resultText.color = playerWon ? Color.green : Color.red;
        }

        // Update images
        if (playerImage != null)
        {
            Sprite playerSprite = GetEntitySprite(player);
            if (playerSprite != null)
            {
                playerImage.sprite = playerSprite;
            }
        }

        if (opponentImage != null)
        {
            Sprite opponentSprite = GetEntitySprite(opponent);
            if (opponentSprite != null)
            {
                opponentImage.sprite = opponentSprite;
            }
        }
    }

    #endregion

    #region Animation Integration

    /// <summary>
    /// Starts the conclusion animation using the FightConclusionAnimator
    /// </summary>
    private void StartConclusionAnimation()
    {
        if (animator == null)
        {
            Debug.Log("FightConclusionUIManager: No FightConclusionAnimator assigned, skipping conclusion animation");
            return;
        }

        if (canvasGroup == null)
        {
            Debug.LogError("FightConclusionUIManager: No CanvasGroup found, cannot animate conclusion");
            return;
        }

        // Start the animation
        animator.StartConclusionAnimation(canvasGroup, GetAnimationTransform());
    }

    /// <summary>
    /// Gets the transform to use for scale animations (if enabled)
    /// </summary>
    private Transform GetAnimationTransform()
    {
        // Return the local result panel transform for scale animations
        return localResultPanel != null ? localResultPanel.transform : fightConclusionCanvas.transform;
    }

    #endregion

    #region Animation Event Handlers

    private void HandleAnimationStarted()
    {
        Debug.Log("FightConclusionUIManager: Conclusion animation started");
        OnConclusionStarted?.Invoke();
    }

    private void HandleDisplayPhaseStarted()
    {
        Debug.Log("FightConclusionUIManager: Conclusion display phase started");
    }

    private void HandleAnimationCompleted()
    {
        /* Debug.Log("FightConclusionUIManager: Conclusion animation completed"); */
        
        // Hide the canvas
        if (fightConclusionCanvas != null)
        {
            fightConclusionCanvas.SetActive(false);
        }
        
        OnConclusionCompleted?.Invoke();
    }

    #endregion

    #region Data Retrieval

    /// <summary>
    /// Gets all fight results from the FightConclusionManager
    /// </summary>
    private Dictionary<NetworkEntity, bool> GetFightResultsData()
    {
        if (fightConclusionManager != null)
        {
            return fightConclusionManager.GetFightResults();
        }
        
        Debug.LogError("FightConclusionUIManager: FightConclusionManager reference is missing");
        return null;
    }

    /// <summary>
    /// Gets the local player's fight result
    /// </summary>
    private (NetworkEntity player, NetworkEntity opponent, bool playerWon)? GetLocalPlayerResult(Dictionary<NetworkEntity, bool> fightResults)
    {
        NetworkEntity localPlayer = GetLocalPlayer();
        if (localPlayer == null) return null;

        if (fightResults.TryGetValue(localPlayer, out bool playerWon))
        {
            NetworkEntity opponent = fightManager.GetOpponentForPlayer(localPlayer);
            if (opponent != null)
            {
                return (localPlayer, opponent, playerWon);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the local player entity
    /// </summary>
    private NetworkEntity GetLocalPlayer()
    {
        if (fightManager != null && fightManager.ClientCombatPlayer != null)
        {
            return fightManager.ClientCombatPlayer;
        }

        // Fallback: search for local player
        return FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None)
            .FirstOrDefault(e => e.EntityType == EntityType.Player && e.IsOwner);
    }

    /// <summary>
    /// Gets the sprite for an entity (implement based on your entity sprite system)
    /// </summary>
    private Sprite GetEntitySprite(NetworkEntity entity)
    {
        // TODO: Implement based on your entity sprite system
        // This might involve getting a sprite from the entity's SpriteRenderer,
        // or from a UI component, or from a data system
        
        // For now, try to get from SpriteRenderer
        SpriteRenderer spriteRenderer = entity.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            return spriteRenderer.sprite;
        }

        // Could also try getting from Image component
        Image imageComponent = entity.GetComponentInChildren<Image>();
        if (imageComponent != null)
        {
            return imageComponent.sprite;
        }

        Debug.LogWarning($"FightConclusionUIManager: Could not find sprite for entity {entity.EntityName.Value}");
        return null;
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Sets a custom animator (useful for testing or runtime changes)
    /// </summary>
    public void SetAnimator(FightConclusionAnimator newAnimator)
    {
        if (animator != null)
        {
            CleanupAnimationEvents();
        }

        animator = newAnimator;

        if (animator != null)
        {
            SetupAnimationEvents();
        }
    }

    /// <summary>
    /// Gets the current animator reference
    /// </summary>
    public FightConclusionAnimator GetAnimator()
    {
        return animator;
    }

    #endregion
} 