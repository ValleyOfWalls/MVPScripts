using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Object;
using System.Collections;

/// <summary>
/// Manages the UI elements for the fight preview interstitial screen that shows before combat.
/// Displays player vs opponent pet matchup for a few seconds to build excitement.
/// Now focuses solely on UI management, with animations handled by FightPreviewAnimator.
/// Attach to: A NetworkObject that contains the fight preview UI elements.
/// </summary>
public class FightPreviewUIManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject fightPreviewCanvas;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI versusText;
    [SerializeField] private TextMeshProUGUI opponentPetNameText;
    [SerializeField] private Image playerImage;
    [SerializeField] private Image opponentPetImage;
    [SerializeField] private GameObject backgroundPanel;

    [Header("Animation Component")]
    [SerializeField] private FightPreviewAnimator animator;

    private CanvasGroup canvasGroup;
    private FightManager fightManager;

    // Events for external systems
    public event System.Action OnPreviewStarted;
    public event System.Action OnPreviewCompleted;

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

        // Get or add CanvasGroup for animations
        if (fightPreviewCanvas != null)
        {
            canvasGroup = fightPreviewCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = fightPreviewCanvas.AddComponent<CanvasGroup>();
            }
        }

        // Find animator if not assigned
        if (animator == null)
        {
            animator = GetComponent<FightPreviewAnimator>();
            if (animator == null)
            {
                animator = FindFirstObjectByType<FightPreviewAnimator>();
            }
        }
    }

    private void InitializeCanvas()
    {
        // Initially hide the canvas
        if (fightPreviewCanvas != null)
        {
            fightPreviewCanvas.SetActive(false);
        }

        // Initialize versus text
        if (versusText != null)
        {
            versusText.text = "VS";
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
    /// Shows the fight preview with the local player's fight information
    /// </summary>
    public void ShowFightPreview()
    {
        if (!IsClientStarted)
        {
            Debug.LogWarning("FightPreviewUIManager: Cannot show preview - client not started");
            return;
        }

        // Get local player's fight information
        var localFightData = GetLocalPlayerFightData();
        if (!localFightData.HasValue)
        {
            Debug.LogError("FightPreviewUIManager: Could not get local player fight data");
            return;
        }

        // Update UI with fight data
        UpdateFightPreviewUI(
            localFightData.Value.playerName, 
            localFightData.Value.opponentPetName, 
            localFightData.Value.playerImageSprite, 
            localFightData.Value.opponentPetImageSprite
        );

        // Show canvas and start animation
        if (fightPreviewCanvas != null)
        {
            fightPreviewCanvas.SetActive(true);
        }

        StartPreviewAnimation();
    }

    /// <summary>
    /// Hides the fight preview immediately (for emergency cleanup)
    /// </summary>
    public void HideFightPreview()
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

        if (fightPreviewCanvas != null)
        {
            fightPreviewCanvas.SetActive(false);
        }
    }

    /// <summary>
    /// Public method to check if the preview is currently showing
    /// </summary>
    public bool IsPreviewShowing()
    {
        return fightPreviewCanvas != null && fightPreviewCanvas.activeSelf && 
               (animator == null || animator.IsAnimating || canvasGroup.alpha > 0f);
    }

    /// <summary>
    /// Gets the total duration of the preview (including animations)
    /// </summary>
    public float GetPreviewDuration()
    {
        return animator != null ? animator.TotalDuration : 3f;
    }

    #endregion

    #region UI Management

    /// <summary>
    /// Updates the UI with fight information
    /// </summary>
    private void UpdateFightPreviewUI(string playerName, string opponentPetName, Sprite playerSprite, Sprite opponentPetSprite)
    {
        // Update text elements
        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        if (opponentPetNameText != null)
        {
            opponentPetNameText.text = opponentPetName;
        }

        // Update images
        UpdatePlayerImage(playerSprite);
        UpdateOpponentPetImage(opponentPetSprite);
    }

    private void UpdatePlayerImage(Sprite playerSprite)
    {
        if (playerImage != null)
        {
            if (playerSprite != null)
            {
                playerImage.sprite = playerSprite;
                playerImage.gameObject.SetActive(true);
            }
            else
            {
                playerImage.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateOpponentPetImage(Sprite opponentPetSprite)
    {
        if (opponentPetImage != null)
        {
            if (opponentPetSprite != null)
            {
                opponentPetImage.sprite = opponentPetSprite;
                opponentPetImage.gameObject.SetActive(true);
            }
            else
            {
                opponentPetImage.gameObject.SetActive(false);
            }
        }
    }

    #endregion

    #region Animation Integration

    /// <summary>
    /// Starts the preview animation using the FightPreviewAnimator
    /// </summary>
    private void StartPreviewAnimation()
    {
        if (animator == null)
        {
            Debug.LogError("FightPreviewUIManager: No FightPreviewAnimator found, cannot animate preview");
            // Fallback to showing without animation
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                StartCoroutine(FallbackDisplayCoroutine());
            }
            return;
        }

        if (canvasGroup == null)
        {
            Debug.LogError("FightPreviewUIManager: No CanvasGroup found, cannot animate preview");
            return;
        }

        // Start the animation
        animator.StartPreviewAnimation(canvasGroup, GetAnimationTransform());
    }

    /// <summary>
    /// Gets the transform to use for scale animations (if enabled)
    /// </summary>
    private Transform GetAnimationTransform()
    {
        // Return the background panel transform for scale animations
        return backgroundPanel != null ? backgroundPanel.transform : fightPreviewCanvas.transform;
    }

    /// <summary>
    /// Fallback display coroutine when animator is not available
    /// </summary>
    private IEnumerator FallbackDisplayCoroutine()
    {
        yield return new WaitForSeconds(3f);
        HideFightPreview();
    }

    #endregion

    #region Animation Event Handlers

    private void HandleAnimationStarted()
    {
        Debug.Log("FightPreviewUIManager: Preview animation started");
        OnPreviewStarted?.Invoke();
    }

    private void HandleDisplayPhaseStarted()
    {
        Debug.Log("FightPreviewUIManager: Preview display phase started");
    }

    private void HandleAnimationCompleted()
    {
        Debug.Log("FightPreviewUIManager: Preview animation completed");
        
        // Hide the canvas
        if (fightPreviewCanvas != null)
        {
            fightPreviewCanvas.SetActive(false);
        }
        
        OnPreviewCompleted?.Invoke();
    }

    #endregion

    #region Data Retrieval

    /// <summary>
    /// Gets the local player's fight data for display
    /// </summary>
    private (string playerName, string opponentPetName, Sprite playerImageSprite, Sprite opponentPetImageSprite)? GetLocalPlayerFightData()
    {
        if (fightManager == null)
        {
            Debug.LogError("FightPreviewUIManager: FightManager reference is missing");
            return null;
        }

        // Get local player and opponent from FightManager
        NetworkEntity localPlayer = fightManager.ClientCombatPlayer;
        NetworkEntity opponentPet = fightManager.ClientCombatOpponentPet;

        if (localPlayer == null || opponentPet == null)
        {
            Debug.LogError("FightPreviewUIManager: Could not find local player or opponent pet");
            return null;
        }

        // Get names
        string playerName = localPlayer.EntityName.Value;
        string opponentPetName = opponentPet.EntityName.Value;

        // Get sprites
        Sprite playerSprite = GetEntitySprite(localPlayer);
        Sprite opponentPetSprite = GetEntitySprite(opponentPet);

        return (playerName, opponentPetName, playerSprite, opponentPetSprite);
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

        Debug.LogWarning($"FightPreviewUIManager: Could not find sprite for entity {entity.EntityName.Value}");
        return null;
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Sets a custom animator (useful for testing or runtime changes)
    /// </summary>
    public void SetAnimator(FightPreviewAnimator newAnimator)
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
    public FightPreviewAnimator GetAnimator()
    {
        return animator;
    }

    #endregion
} 