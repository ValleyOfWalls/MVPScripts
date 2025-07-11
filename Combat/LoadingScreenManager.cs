using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Manages the loading screen between Character Selection and Combat phases.
/// Shows random loading tips and transitions to combat when setup is complete.
/// Attach to: A NetworkObject with a Canvas component for the loading screen.
/// </summary>
public class LoadingScreenManager : NetworkBehaviour
{
    [Header("Loading Screen Canvas")]
    [SerializeField] private Canvas loadingCanvas;
    [SerializeField] private GameObject loadingScreenPanel;
    
    [Header("Loading Tip Images")]
    [SerializeField] private Image loadingTipImage;
    [SerializeField] private Sprite[] loadingTipSprites;
    
    [Header("Loading Text")]
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private string[] loadingMessages = {
        "Preparing for battle...",
        "Setting up combat decks...",
        "Assigning fights...",
        "Loading combat arena...",
        "Initializing battle systems..."
    };
    
    [Header("Animation")]
    [SerializeField] private bool animateLoadingText = true;
    [SerializeField] private float textAnimationSpeed = 0.5f;
    
    [Header("Canvas Sorting")]
    [SerializeField] private int loadingCanvasSortOrder = 1000; // High value to appear on top
    
    [Header("References")]
    [SerializeField] private CombatSetup combatSetup;
    [SerializeField] private GamePhaseManager gamePhaseManager;
    [SerializeField] private CombatCanvasManager combatCanvasManager;
    
    private bool isLoadingScreenActive = false;
    private Coroutine loadingTextAnimation;
    private int currentMessageIndex = 0;
    
    // Track completion states
    private bool isDeckSetupComplete = false;
    private bool isModelPositioningComplete = false;
    
    private void Awake()
    {
        ResolveReferences();
        SetupCanvas();
        
        // Initially hide the loading screen
        if (loadingScreenPanel != null)
        {
            loadingScreenPanel.SetActive(false);
        }
    }
    
    private void ResolveReferences()
    {
        if (combatSetup == null)
            combatSetup = FindFirstObjectByType<CombatSetup>();
            
        if (gamePhaseManager == null)
            gamePhaseManager = FindFirstObjectByType<GamePhaseManager>();
            
        if (combatCanvasManager == null)
            combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
            
        if (loadingCanvas == null)
            loadingCanvas = GetComponent<Canvas>();
    }
    
    private void SetupCanvas()
    {
        if (loadingCanvas != null)
        {
            // Set high sort order to appear on top of other canvases
            loadingCanvas.sortingOrder = loadingCanvasSortOrder;
            
            // Ensure it renders on top
            loadingCanvas.overrideSorting = true;
        }
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Subscribe to game phase changes
        if (gamePhaseManager != null)
        {
            gamePhaseManager.OnPhaseChanged += OnGamePhaseChanged;
        }
        
        // Combat setup completion is now handled via OnDeckSetupComplete callback
        // Model positioning completion is handled via OnModelPositioningComplete callback
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        
        if (gamePhaseManager != null)
        {
            gamePhaseManager.OnPhaseChanged -= OnGamePhaseChanged;
        }
    }
    
    private void OnGamePhaseChanged(GamePhaseManager.GamePhase newPhase)
    {
        /* Debug.Log($"LoadingScreenManager: Game phase changed to {newPhase}"); */
        
        // Show loading screen when transitioning to combat phase
        if (newPhase == GamePhaseManager.GamePhase.Combat && !isLoadingScreenActive)
        {
            ShowLoadingScreen();
        }
    }
    
    /// <summary>
    /// Shows the loading screen with a random loading tip
    /// </summary>
    public void ShowLoadingScreen()
    {
        if (isLoadingScreenActive) return;
        
        /* Debug.Log("LoadingScreenManager: Showing loading screen"); */
        isLoadingScreenActive = true;
        
        // Reset completion states for new combat round
        ResetCompletionStates();
        
        // Activate the loading screen panel
        if (loadingScreenPanel != null)
        {
            loadingScreenPanel.SetActive(true);
        }
        
        // Set random loading tip image
        SetRandomLoadingTip();
        
        // Start loading text animation
        StartLoadingTextAnimation();
        
        Debug.Log("LoadingScreenManager: Loading screen displayed");
    }
    
    /// <summary>
    /// Shows the loading screen immediately for combat transition, called before phase change
    /// </summary>
    [ObserversRpc]
    public void RpcShowLoadingScreenForCombatTransition()
    {
        Debug.Log("LoadingScreenManager: Showing loading screen for combat transition");
        ShowLoadingScreen();
    }
    
    /// <summary>
    /// Hides the loading screen and transitions to combat
    /// </summary>
    public void HideLoadingScreen()
    {
        if (!isLoadingScreenActive) return;
        
        /* Debug.Log("LoadingScreenManager: Hiding loading screen"); */
        isLoadingScreenActive = false;
        
        // Stop loading text animation
        StopLoadingTextAnimation();
        
        // Deactivate the loading screen panel
        if (loadingScreenPanel != null)
        {
            loadingScreenPanel.SetActive(false);
        }
        
        Debug.Log("LoadingScreenManager: Loading screen hidden, transitioning to combat");
    }
    
    private void SetRandomLoadingTip()
    {
        if (loadingTipImage != null && loadingTipSprites != null && loadingTipSprites.Length > 0)
        {
            int randomIndex = Random.Range(0, loadingTipSprites.Length);
            loadingTipImage.sprite = loadingTipSprites[randomIndex];
            Debug.Log($"LoadingScreenManager: Set loading tip image {randomIndex + 1} of {loadingTipSprites.Length}");
        }
        else
        {
            Debug.LogWarning("LoadingScreenManager: No loading tip sprites configured or image reference missing");
        }
    }
    
    private void StartLoadingTextAnimation()
    {
        if (loadingText != null && animateLoadingText && loadingMessages.Length > 0)
        {
            if (loadingTextAnimation != null)
            {
                StopCoroutine(loadingTextAnimation);
            }
            
            loadingTextAnimation = StartCoroutine(AnimateLoadingText());
        }
        else if (loadingText != null && loadingMessages.Length > 0)
        {
            // Just set a random message if not animating
            int randomIndex = Random.Range(0, loadingMessages.Length);
            loadingText.text = loadingMessages[randomIndex];
        }
    }
    
    private void StopLoadingTextAnimation()
    {
        if (loadingTextAnimation != null)
        {
            StopCoroutine(loadingTextAnimation);
            loadingTextAnimation = null;
        }
    }
    
    private IEnumerator AnimateLoadingText()
    {
        currentMessageIndex = 0;
        
        while (isLoadingScreenActive)
        {
            if (loadingMessages.Length > 0)
            {
                loadingText.text = loadingMessages[currentMessageIndex];
                currentMessageIndex = (currentMessageIndex + 1) % loadingMessages.Length;
            }
            
            yield return new WaitForSeconds(textAnimationSpeed);
        }
    }
    

    
    /// <summary>
    /// Force show loading screen (for testing or manual control)
    /// </summary>
    [ContextMenu("Show Loading Screen")]
    public void ForceShowLoadingScreen()
    {
        ShowLoadingScreen();
    }
    
    /// <summary>
    /// Force hide loading screen (for testing or manual control)
    /// </summary>
    [ContextMenu("Hide Loading Screen")]
    public void ForceHideLoadingScreen()
    {
        HideLoadingScreen();
    }
    
    /// <summary>
    /// Add a custom loading message at runtime
    /// </summary>
    public void AddLoadingMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        
        List<string> messageList = new List<string>(loadingMessages);
        messageList.Add(message);
        loadingMessages = messageList.ToArray();
    }
    
    /// <summary>
    /// Add a custom loading tip sprite at runtime
    /// </summary>
    public void AddLoadingTipSprite(Sprite sprite)
    {
        if (sprite == null) return;
        
        List<Sprite> spriteList = new List<Sprite>(loadingTipSprites);
        spriteList.Add(sprite);
        loadingTipSprites = spriteList.ToArray();
    }
    
    /// <summary>
    /// Called by CombatCanvasManager when model positioning is complete
    /// </summary>
    public void OnModelPositioningComplete()
    {
        isModelPositioningComplete = true;
        Debug.Log("LoadingScreenManager: Model positioning completed");
        
        // Check if we can hide the loading screen now
        CheckCanHideLoadingScreen();
    }
    
    /// <summary>
    /// Called by CombatSetup when deck setup is complete
    /// </summary>
    public void OnDeckSetupComplete()
    {
        isDeckSetupComplete = true;
        Debug.Log("LoadingScreenManager: Deck setup completed");
        
        // Check if we can hide the loading screen now
        CheckCanHideLoadingScreen();
    }
    
    /// <summary>
    /// Checks if both deck setup and model positioning are complete, then hides loading screen
    /// </summary>
    private void CheckCanHideLoadingScreen()
    {
        if (isDeckSetupComplete && isModelPositioningComplete && isLoadingScreenActive)
        {
            Debug.Log("LoadingScreenManager: Both deck setup and model positioning complete, hiding loading screen");
            HideLoadingScreen();
        }
        else
        {
            Debug.Log($"LoadingScreenManager: Waiting for completion - DeckSetup: {isDeckSetupComplete}, ModelPositioning: {isModelPositioningComplete}, LoadingActive: {isLoadingScreenActive}");
        }
    }
    
    /// <summary>
    /// Resets completion states for new combat round
    /// </summary>
    public void ResetCompletionStates()
    {
        isDeckSetupComplete = false;
        isModelPositioningComplete = false;
        Debug.Log("LoadingScreenManager: Reset completion states for new combat round");
    }
    
    // Public getters for external systems
    public bool IsLoadingScreenActive => isLoadingScreenActive;
    public Canvas LoadingCanvas => loadingCanvas;
} 