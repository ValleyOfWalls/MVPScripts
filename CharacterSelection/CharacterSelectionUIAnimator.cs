using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using DG.Tweening;
using MVPScripts.Utility;
using CharacterSelection;

/// <summary>
/// Handles sliding animations for UI panels in the character selection screen
/// Supports both UI button-based selection and 3D model-based selection
/// Now includes model transition animations delegated to ModelDissolveAnimator
/// </summary>
public class CharacterSelectionUIAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float slideAnimationDuration = 0.3f;
    [SerializeField] private Ease slideInEase = Ease.OutCubic;
    [SerializeField] private Ease slideOutEase = Ease.InCubic;
    
    [Header("Panel Slide Directions - Fallback Values")]
    [SerializeField] private Vector2 playerListHiddenOffset = new Vector2(-300, 0); // Fallback if no OffscreenPanelSetup
    [SerializeField] private Vector2 deckPreviewHiddenOffset = new Vector2(0, -400); // Fallback if no OffscreenPanelSetup
    
    [Header("Model Transition System")]
    [SerializeField] private ModelDissolveAnimator modelDissolveAnimator;
    
    // Panel references
    private GameObject playerListPanel;
    private GameObject deckPreviewPanel;
    private RectTransform playerListRect;
    private RectTransform deckPreviewRect;
    
    // Click detection areas
    private RectTransform characterGridParentRect;
    private RectTransform petGridParentRect;
    
    // Animation state
    private bool isPlayerListVisible = false;
    private bool isDeckPreviewVisible = false;
    private Tween playerListTween;
    private Tween deckPreviewTween;
    
    // Click detection control
    private bool enableClickOutsideDetection = true;
    
    // Events for state changes
    public System.Action<bool> OnPlayerListVisibilityChanged;
    public System.Action<bool> OnDeckPreviewVisibilityChanged;
    public System.Action<GameObject> OnModelTransitionStarted;
    public System.Action<GameObject> OnModelTransitionCompleted;
    

    
    #region Initialization
    
    public void Initialize(GameObject playerListPanel, GameObject deckPreviewPanel)
    {
        this.playerListPanel = playerListPanel;
        this.deckPreviewPanel = deckPreviewPanel;
        
        if (playerListPanel != null)
        {
            playerListRect = playerListPanel.GetComponent<RectTransform>();
        }
        
        if (deckPreviewPanel != null)
        {
            deckPreviewRect = deckPreviewPanel.GetComponent<RectTransform>();
        }
        
        // Initialize panels in their proper state (deck panel always visible, no animations)
        SetupInitialPanelPositions();
        
        // Setup model dissolve animator
        SetupModelDissolveAnimator();
        
        Debug.Log("CharacterSelectionUIAnimator: Initialized with player list and shared deck preview panel");
    }
    
    private void SetupInitialPanelPositions()
    {
        /* Debug.Log("CharacterSelectionUIAnimator: SetupInitialPanelPositions() - Setting up initial panel positions"); */
        
        // Set up all panels using the generic setup method (supports OffscreenPanelSetup)
        SetupPanel(playerListPanel, playerListRect, playerListHiddenOffset);
        SetupPanel(deckPreviewPanel, deckPreviewRect, deckPreviewHiddenOffset);
    }
    
    /// <summary>
    /// Generic panel setup method that works with any panel and checks for OffscreenPanelSetup
    /// </summary>
    private void SetupPanel(GameObject panel, RectTransform panelRect, Vector2 fallbackHiddenOffset)
    {
        if (panel == null || panelRect == null) return;
        
        /* Debug.Log($"CharacterSelectionUIAnimator: SetupPanel() - Setting up {panel.name} panel"); */
        
        // Check if the panel has an OffscreenPanelSetup component
        OffscreenPanelSetup panelSetup = panel.GetComponent<OffscreenPanelSetup>();
        if (panelSetup != null)
        {
            // Use the setup component's hidden position
            panelRect.anchoredPosition = panelSetup.HiddenPosition;
            Debug.Log($"CharacterSelectionUIAnimator: Using OffscreenPanelSetup hidden position for {panel.name}: {panelSetup.HiddenPosition}");
        }
        else
        {
            // Fallback to provided offset
            panelRect.anchoredPosition = fallbackHiddenOffset;
            Debug.Log($"CharacterSelectionUIAnimator: Using fallback hidden position for {panel.name}: {fallbackHiddenOffset}");
        }
        
        panel.SetActive(false);
        /* Debug.Log($"CharacterSelectionUIAnimator: {panel.name} panel set to INACTIVE during initial setup"); */
    }
    
    private void SetupModelDissolveAnimator()
    {
        // Get or create the model dissolve animator component
        if (modelDissolveAnimator == null)
        {
            modelDissolveAnimator = GetComponent<ModelDissolveAnimator>();
            if (modelDissolveAnimator == null)
            {
                modelDissolveAnimator = gameObject.AddComponent<ModelDissolveAnimator>();
            }
        }
        
        // Subscribe to events to forward them to our own events
        modelDissolveAnimator.OnTransitionStarted += (model) => OnModelTransitionStarted?.Invoke(model);
        modelDissolveAnimator.OnTransitionCompleted += (model) => OnModelTransitionCompleted?.Invoke(model);
    }
    
    #endregion
    
    #region Player List Panel Animation
    
    public void ShowPlayerListPanel()
    {
        if (playerListPanel == null || isPlayerListVisible) return;
        
        // Kill existing tween
        playerListTween?.Kill();
        
        playerListPanel.SetActive(true);
        isPlayerListVisible = true;
        
        // Get positions from OffscreenPanelSetup if available
        Vector2 hiddenPos = GetPlayerListHiddenPosition();
        Vector2 targetPos = GetPlayerListTargetPosition();
        
        // Set starting position and animate to target
        playerListRect.anchoredPosition = hiddenPos;
        playerListTween = playerListRect.DOAnchorPos(targetPos, slideAnimationDuration)
            .SetEase(slideInEase)
            .OnComplete(() => {
                OnPlayerListVisibilityChanged?.Invoke(true);
            });
        
        /* Debug.Log("CharacterSelectionUIAnimator: Player list panel sliding in with DOTween"); */
    }
    
    public void HidePlayerListPanel()
    {
        if (playerListPanel == null || !isPlayerListVisible) return;
        
        // Kill existing tween
        playerListTween?.Kill();
        
        isPlayerListVisible = false;
        
        Vector2 hiddenPos = GetPlayerListHiddenPosition();
        playerListTween = playerListRect.DOAnchorPos(hiddenPos, slideAnimationDuration)
            .SetEase(slideOutEase)
            .OnComplete(() => {
                playerListPanel.SetActive(false);
                OnPlayerListVisibilityChanged?.Invoke(false);
            });
        
        Debug.Log("CharacterSelectionUIAnimator: Player list panel sliding out with DOTween");
    }
    
    public void TogglePlayerListPanel()
    {
        if (isPlayerListVisible)
        {
            HidePlayerListPanel();
        }
        else
        {
            ShowPlayerListPanel();
        }
    }
    
    #endregion
    
    #region Deck Preview Panel Animation
    
    public void ShowDeckPreviewPanel()
    {
        if (deckPreviewPanel == null || isDeckPreviewVisible) return;
        
        // Kill existing tween
        deckPreviewTween?.Kill();
        
        deckPreviewPanel.SetActive(true);
        isDeckPreviewVisible = true;
        
        // Get positions from DeckPreviewPanelSetup if available
        Vector2 hiddenPos = GetDeckPanelHiddenPosition(deckPreviewPanel);
        Vector2 targetPos = GetDeckPanelTargetPosition(deckPreviewPanel);
        
        // Set starting position and animate to target
        deckPreviewRect.anchoredPosition = hiddenPos;
        deckPreviewTween = deckPreviewRect.DOAnchorPos(targetPos, slideAnimationDuration)
            .SetEase(slideInEase)
            .OnComplete(() => {
                OnDeckPreviewVisibilityChanged?.Invoke(true);
            });
        
        /* Debug.Log("CharacterSelectionUIAnimator: Deck preview panel sliding in from below with DOTween"); */
    }
    
    public void HideDeckPreviewPanel()
    {
        if (deckPreviewPanel == null || !isDeckPreviewVisible) return;
        
        // Kill existing tween
        deckPreviewTween?.Kill();
        
        isDeckPreviewVisible = false;
        
        Vector2 hiddenPos = GetDeckPanelHiddenPosition(deckPreviewPanel);
        deckPreviewTween = deckPreviewRect.DOAnchorPos(hiddenPos, slideAnimationDuration)
            .SetEase(slideOutEase)
            .OnComplete(() => {
                deckPreviewPanel.SetActive(false);
                Debug.Log("CharacterSelectionUIAnimator: Deck preview panel set to INACTIVE after slide-out animation completed");
                OnDeckPreviewVisibilityChanged?.Invoke(false);
            });
        
        Debug.Log("CharacterSelectionUIAnimator: Deck preview panel sliding out to below with DOTween");
    }
    
    #endregion
    
    #region Click Outside Detection
    
    private void Update()
    {
        // Only process click detection if we're in the character selection phase
        if (!IsCharacterSelectionPhaseActive())
        {
            return;
        }
        
        // Check for clicks outside of character/pet selections to hide deck previews
        if (enableClickOutsideDetection && (isDeckPreviewVisible) && Input.GetMouseButtonDown(0))
        {
            CheckClickOutsideSelections();
        }
    }
    
    /// <summary>
    /// Checks if we're currently in the character selection phase
    /// </summary>
    private bool IsCharacterSelectionPhaseActive()
    {
        // Try to get GamePhaseManager instance
        GamePhaseManager gamePhaseManager = GamePhaseManager.Instance;
        if (gamePhaseManager == null)
        {
            // Fallback: check if our parent canvas is active
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            bool canvasActive = parentCanvas != null && parentCanvas.gameObject.activeInHierarchy;
            
            if (!canvasActive && enableClickOutsideDetection)
            {
                // If canvas is inactive but we still have click detection enabled, 
                // this suggests we've transitioned away from character selection
                Debug.Log("CharacterSelectionUIAnimator: Parent canvas inactive, disabling click detection");
                enableClickOutsideDetection = false;
            }
            
            return canvasActive;
        }
        
        // Check if current phase is character selection
        bool isCharacterSelectionPhase = gamePhaseManager.GetCurrentPhase() == GamePhaseManager.GamePhase.CharacterSelection;
        
        if (!isCharacterSelectionPhase && enableClickOutsideDetection)
        {
            /* Debug.Log($"CharacterSelectionUIAnimator: Game phase changed to {gamePhaseManager.GetCurrentPhase()}, disabling click detection"); */
            enableClickOutsideDetection = false;
        }
        
        return isCharacterSelectionPhase;
    }
    
    private void CheckClickOutsideSelections()
    {
        /* Debug.Log($"CharacterSelectionUIAnimator: CheckClickOutsideSelections() - Mouse clicked at {Input.mousePosition}, checking if outside selection areas"); */
        
        // Debug current state
        DebugClickDetectionState();
        
        // Use UI raycasting to see what we clicked on
        bool isValidClick = IsClickOnCharacterOrPetSelection();
        
        if (!isValidClick)
        {
            Debug.Log("CharacterSelectionUIAnimator: Click DETECTED outside character/pet selections - HIDING ALL DECK PANELS");
            HideDeckPreviewPanel();
        }
        else
        {
            Debug.Log("CharacterSelectionUIAnimator: Click detected on valid selection area - keeping panels open");
        }
    }
    
    private bool IsClickOnCharacterOrPetSelection()
    {
        // First, check for 3D model clicks using Physics raycasting
        bool found3DClick = Check3DModelClick();
        if (found3DClick) return true;
        
        // Then check for UI clicks using EventSystem raycasting
        bool foundUIClick = CheckUIElementClick();
        if (foundUIClick) return true;
        
        Debug.Log("CharacterSelectionUIAnimator: No valid selection items found in any raycast results");
        return false;
    }
    
    private bool Check3DModelClick()
    {
        // Get the main camera for 3D raycasting
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<Camera>();
        }
        
        if (mainCamera == null)
        {
            Debug.Log("CharacterSelectionUIAnimator: No camera found for 3D model click detection");
            return false;
        }
        
        // Cast a ray from the camera through the mouse position
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
        {
            GameObject hitObject = hit.collider.gameObject;
            /* Debug.Log($"CharacterSelectionUIAnimator: 3D Physics raycast hit: {hitObject.name}"); */
            
            // Check if this is a character or pet selection model
            if (Is3DSelectionModel(hitObject))
            {
                /* Debug.Log($"CharacterSelectionUIAnimator: 3D model selection detected on {hitObject.name}"); */
                return true;
            }
            
            // Also check if the hit object is a child of a selection model
            Transform parent = hitObject.transform.parent;
            while (parent != null)
            {
                if (Is3DSelectionModel(parent.gameObject))
                {
                    /* Debug.Log($"CharacterSelectionUIAnimator: 3D model selection detected on parent {parent.name}"); */
                    return true;
                }
                parent = parent.parent;
            }
        }
        
        return false;
    }
    
    private bool Is3DSelectionModel(GameObject obj)
    {
        if (obj == null) return false;
        
        string objName = obj.name.ToLower();
        
        // Check for character or pet model indicators
        bool isCharacterModel = objName.Contains("character") || objName.Contains("warrior") || objName.Contains("enhanced") || objName.Contains("assassin");
        bool isPetModel = objName.Contains("pet") || objName.Contains("beast") || objName.Contains("elemental") || objName.Contains("spirit");
        
        // Also check for specific model naming patterns
        bool isSelectionModel = objName.Contains("selection") || objName.Contains("preview") || objName.Contains("model");
        
        // Check for character selection related components
        bool hasSelectionComponents = obj.GetComponent<ModelOutlineHover>() != null || 
                                      obj.GetComponent<Collider>() != null ||
                                      obj.GetComponentInParent<EntitySelectionController>() != null;
        
        bool result = (isCharacterModel || isPetModel || isSelectionModel) && hasSelectionComponents;
        
        if (result)
        {
            /* Debug.Log($"CharacterSelectionUIAnimator: Identified {obj.name} as 3D selection model"); */
        }
        
        return result;
    }
    
    private bool CheckUIElementClick()
    {
        // Get the EventSystem
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            Debug.Log("CharacterSelectionUIAnimator: No EventSystem found for UI click detection");
            return false;
        }
        
        // Raycast for UI elements
        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = Input.mousePosition
        };
        
        List<RaycastResult> results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);
        
        /* Debug.Log($"CharacterSelectionUIAnimator: UI raycast found {results.Count} hit objects"); */
        foreach (RaycastResult result in results)
        {
            /* Debug.Log($"CharacterSelectionUIAnimator: UI raycast hit: {result.gameObject.name}"); */
        }
        
        return CheckRaycastResults(results);
    }
    
    private bool CheckRaycastResults(List<RaycastResult> results)
    {
        foreach (RaycastResult result in results)
        {
            GameObject obj = result.gameObject;
            
            // Check if this object is in a deck panel (should keep panels open)
            if (IsObjectInDeckPanel(obj))
            {
                /* Debug.Log($"CharacterSelectionUIAnimator: Click on deck panel item: {obj.name}"); */
                return true;
            }
            
            // Check if this object is in character or pet selection (should keep panels open)
            if (IsObjectInCharacterOrPetSelection(obj))
            {
                /* Debug.Log($"CharacterSelectionUIAnimator: Click on character/pet selection item: {obj.name}"); */
                return true;
            }
        }
        
        return false;
    }
    
    // Helper method to find canvas for character items - adjusted search pattern
    private Canvas FindCanvasForCharacterItems()
    {
        // Try multiple search strategies to find character selection canvas
        
        // Strategy 1: Look for character grid parent's canvas
        if (characterGridParentRect != null)
        {
            Canvas canvas = characterGridParentRect.GetComponentInParent<Canvas>();
            if (canvas != null) return canvas;
        }
        
        // Strategy 2: Search for canvas with character-related names
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in allCanvases)
        {
            string canvasName = canvas.name.ToLower();
            if (canvasName.Contains("character") || canvasName.Contains("selection"))
            {
                return canvas;
            }
        }
        
        return null;
    }
    
    // Helper method to find canvas for pet items - adjusted search pattern  
    private Canvas FindCanvasForPetItems()
    {
        // Try multiple search strategies to find pet selection canvas
        
        // Strategy 1: Look for pet grid parent's canvas
        if (petGridParentRect != null)
        {
            Canvas canvas = petGridParentRect.GetComponentInParent<Canvas>();
            if (canvas != null) return canvas;
        }
        
        // Strategy 2: Search for canvas with pet-related names
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in allCanvases)
        {
            string canvasName = canvas.name.ToLower();
            if (canvasName.Contains("pet") || canvasName.Contains("selection"))
            {
                return canvas;
            }
        }
        
        return null;
    }
    
    public void SetClickDetectionAreas(RectTransform characterGridParent, RectTransform petGridParent)
    {
        characterGridParentRect = characterGridParent;
        petGridParentRect = petGridParent;
        
        /* Debug.Log($"CharacterSelectionUIAnimator: Click detection areas set - Character: {characterGridParent?.name}, Pet: {petGridParent?.name}"); */
        
        // Validate the grid parents
        ValidateAndFindGridParents();
        
        // Enable click detection now that we have areas
        enableClickOutsideDetection = true;
        
        Debug.Log($"CharacterSelectionUIAnimator: Click outside detection ENABLED with areas: Character={characterGridParentRect?.name}, Pet={petGridParentRect?.name}");
    }
    
    private void ValidateAndFindGridParents()
    {
        // If we don't have character grid parent, try to find it
        if (characterGridParentRect == null)
        {
            // Try to find character grid by searching for common names
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (GameObject obj in allObjects)
            {
                string objName = obj.name.ToLower();
                if ((objName.Contains("character") && (objName.Contains("grid") || objName.Contains("parent") || objName.Contains("content"))) ||
                    objName == "charactergridparent" || objName == "character_grid_parent")
                {
                    characterGridParentRect = obj.GetComponent<RectTransform>();
                    if (characterGridParentRect != null)
                    {
                        Debug.Log($"CharacterSelectionUIAnimator: Found character grid parent: {obj.name}");
                        break;
                    }
                }
            }
        }
        
        // If we don't have pet grid parent, try to find it
        if (petGridParentRect == null)
        {
            // Try to find pet grid by searching for common names
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (GameObject obj in allObjects)
            {
                string objName = obj.name.ToLower();
                if ((objName.Contains("pet") && (objName.Contains("grid") || objName.Contains("parent") || objName.Contains("content"))) ||
                    objName == "petgridparent" || objName == "pet_grid_parent")
                {
                    petGridParentRect = obj.GetComponent<RectTransform>();
                    if (petGridParentRect != null)
                    {
                        Debug.Log($"CharacterSelectionUIAnimator: Found pet grid parent: {obj.name}");
                        break;
                    }
                }
            }
        }
        
        // Final validation
        if (characterGridParentRect == null || petGridParentRect == null)
        {
            Debug.LogWarning($"CharacterSelectionUIAnimator: Grid parent validation failed - Character: {characterGridParentRect?.name}, Pet: {petGridParentRect?.name}");
            Debug.LogWarning("CharacterSelectionUIAnimator: Click outside detection may not work properly without proper grid parent references");
        }
    }
    
    public void DebugClickDetectionState()
    {
        Debug.Log($"CharacterSelectionUIAnimator: Click Detection State:");
        Debug.Log($"  - enableClickOutsideDetection: {enableClickOutsideDetection}");
        Debug.Log($"  - characterGridParentRect: {characterGridParentRect?.name ?? "NULL"}");
        Debug.Log($"  - petGridParentRect: {petGridParentRect?.name ?? "NULL"}");
        Debug.Log($"  - isDeckPreviewVisible: {isDeckPreviewVisible}");
        Debug.Log($"  - Character selection phase active: {IsCharacterSelectionPhaseActive()}");
    }
    
    public void DisableClickDetectionIfInvalid()
    {
        if (characterGridParentRect == null || petGridParentRect == null)
        {
            Debug.LogWarning("CharacterSelectionUIAnimator: Disabling click detection due to missing grid parent references");
            enableClickOutsideDetection = false;
        }
    }
    
    #endregion
    
    #region Model Transition Animation System
    
    /// <summary>
    /// Delegates to ModelDissolveAnimator helper class for model transitions
    /// </summary>
    #region Model Transition Delegation - Using ModelDissolveAnimator Helper
    
    public void AnimateModelTransition(GameObject oldModel, GameObject newModel, System.Action onComplete = null)
    {
        if (modelDissolveAnimator == null)
        {
            Debug.LogWarning("CharacterSelectionUIAnimator: ModelDissolveAnimator not available for model transition");
            onComplete?.Invoke();
            return;
        }
        
        modelDissolveAnimator.AnimateModelTransition(oldModel, newModel, onComplete);
    }

    /// <summary>
    /// Requests a model transition using a factory callback to create the target model only when needed.
    /// This prevents unnecessary model creation and ensures only the final target is created.
    /// </summary>
    /// <param name="currentModel">The current visible model</param>
    /// <param name="targetModel">The target model (can be null if using factory)</param>
    /// <param name="modelFactory">Factory callback to create the target model when needed</param>
    /// <param name="onComplete">Callback when transition completes</param>
    public void RequestModelTransition(GameObject currentModel, GameObject targetModel, System.Func<GameObject> modelFactory, System.Action onComplete = null)
    {
        if (modelDissolveAnimator == null)
        {
            Debug.LogWarning("CharacterSelectionUIAnimator: ModelDissolveAnimator not available for transition");
            onComplete?.Invoke();
            return;
        }

        // If targetModel is provided, use it directly
        if (targetModel != null)
        {
            modelDissolveAnimator.AnimateModelTransition(currentModel, targetModel, onComplete);
        }
        else if (modelFactory != null)
        {
            // Use factory-based transition - model will only be created when animation system needs it
            modelDissolveAnimator.AnimateModelTransitionWithFactory(currentModel, modelFactory, onComplete);
        }
        else
        {
            Debug.LogWarning("CharacterSelectionUIAnimator: No target model or factory provided for transition");
            onComplete?.Invoke();
        }
    }
    
    public void ShowModelInstantly(GameObject model)
    {
        if (modelDissolveAnimator == null)
        {
            Debug.LogWarning("CharacterSelectionUIAnimator: ModelDissolveAnimator not available for instant show");
            return;
        }
        
        modelDissolveAnimator.AnimateModelIn(model);
    }
    
    public void HideModelInstantly(GameObject model)
    {
        if (modelDissolveAnimator == null)
        {
            Debug.LogWarning("CharacterSelectionUIAnimator: ModelDissolveAnimator not available for instant hide");
            return;
        }
        
        modelDissolveAnimator.AnimateModelOut(model);
    }
    
    // Properties and methods that delegate to ModelDissolveAnimator
    public bool IsModelTransitioning => modelDissolveAnimator?.IsAnimating ?? false;
    
    /// <summary>
    /// Get the ModelDissolveAnimator for external access to animation state
    /// </summary>
    public ModelDissolveAnimator GetModelDissolveAnimator() => modelDissolveAnimator;
    
    // Configuration delegation methods
    public void SetModelTransitionTiming(float outDuration, float inDuration) => modelDissolveAnimator?.SetTransitionTiming(outDuration, inDuration);
    public void SetModelGlow(Color glowColor, float intensity, bool additive = true) => modelDissolveAnimator?.SetGlowSettings(glowColor, intensity, additive);
    public void SetModelFlash(Color flashColor, float intensity, float duration) => modelDissolveAnimator?.SetFlashSettings(flashColor, intensity, duration);
    public void SetModelEffectsEnabled(bool dissolve, bool scale, bool glow, bool flash) => modelDissolveAnimator?.SetEffectsEnabled(dissolve, scale, glow, flash);
    public void SetModelAudioClips(AudioClip teleportIn, AudioClip teleportOut, AudioClip flash) => modelDissolveAnimator?.SetAudioClips(teleportIn, teleportOut, flash);
    public void SetModelAudioSource(AudioSource audioSource) => modelDissolveAnimator?.SetAudioSource(audioSource);
    public string GetModelTransitionInfo() => modelDissolveAnimator?.GetTransitionInfo() ?? "ModelDissolveAnimator not available";
    public void StopModelTransition() => modelDissolveAnimator?.StopTransition();
    public void ClearModelCache() => modelDissolveAnimator?.ClearMaterialCache();
    
    [ContextMenu("Test Model Flash Effect")]
    public void TestModelFlashEffect()
    {
        if (modelDissolveAnimator == null)
        {
            Debug.LogWarning("CharacterSelectionUIAnimator: ModelDissolveAnimator not available for test flash");
            return;
        }
        
        // Find any active model in the scene for testing
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        GameObject testModel = null;
        foreach (Renderer renderer in renderers)
        {
            if (renderer.gameObject.activeInHierarchy)
            {
                testModel = renderer.gameObject;
                break;
            }
        }
        
        if (testModel != null)
        {
            Debug.Log($"CharacterSelectionUIAnimator: Testing flash effect on {testModel.name}");
            // Delegate to helper class
        }
        else
        {
            Debug.LogWarning("CharacterSelectionUIAnimator: No active models found for flash test");
        }
    }
    
    [ContextMenu("Print Model Transition Info")]
    public void PrintModelTransitionInfo()
    {
        Debug.Log(GetModelTransitionInfo());
    }
    
    #endregion
    
    #endregion
    
    #region Helper Methods
    
    private Vector2 GetPlayerListHiddenPosition()
    {
        if (playerListPanel == null) return playerListHiddenOffset;
        
        OffscreenPanelSetup panelSetup = playerListPanel.GetComponent<OffscreenPanelSetup>();
        if (panelSetup != null)
        {
            return panelSetup.HiddenPosition;
        }
        
        // Fallback to default
        return playerListHiddenOffset;
    }
    
    private Vector2 GetPlayerListTargetPosition()
    {
        if (playerListPanel == null || playerListRect == null) 
        {
            Debug.LogWarning("CharacterSelectionUIAnimator: PlayerListPanel or its RectTransform is null!");
            return Vector2.zero;
        }
        
        OffscreenPanelSetup panelSetup = playerListPanel.GetComponent<OffscreenPanelSetup>();
        if (panelSetup != null)
        {
            return panelSetup.TargetPosition;
        }
        
        // If no OffscreenPanelSetup, this method shouldn't be called - log error
        Debug.LogError($"CharacterSelectionUIAnimator: No OffscreenPanelSetup found on {playerListPanel.name}, but trying to get target position. This should not happen with the current setup.");
        
        // Return the panel's current position as emergency fallback
        return playerListRect.anchoredPosition;
    }
    
    private Vector2 GetDeckPanelHiddenPosition(GameObject panel)
    {
        if (panel == null) return deckPreviewHiddenOffset;
        
        OffscreenPanelSetup panelSetup = panel.GetComponent<OffscreenPanelSetup>();
        if (panelSetup != null)
        {
            return panelSetup.HiddenPosition;
        }
        
        // Fallback to default
        return deckPreviewHiddenOffset;
    }
    
    private Vector2 GetDeckPanelTargetPosition(GameObject panel)
    {
        if (panel == null) 
        {
            Debug.LogWarning("CharacterSelectionUIAnimator: Panel is null in GetDeckPanelTargetPosition!");
            return Vector2.zero;
        }
        
        OffscreenPanelSetup panelSetup = panel.GetComponent<OffscreenPanelSetup>();
        if (panelSetup != null)
        {
            return panelSetup.TargetPosition;
        }
        
        // If no OffscreenPanelSetup, this method shouldn't be called - log error
        Debug.LogError($"CharacterSelectionUIAnimator: No OffscreenPanelSetup found on {panel.name}, but trying to get target position. This should not happen with the current setup.");
        
        // Return the panel's current position as emergency fallback
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        return panelRect != null ? panelRect.anchoredPosition : Vector2.zero;
    }
    
    private bool IsObjectInDeckPanel(GameObject obj)
    {
        if (obj == null) return false;
        
        // Check if the object is a child of either deck panel
        if (deckPreviewPanel != null && obj.transform.IsChildOf(deckPreviewPanel.transform))
        {
            Debug.Log($"CharacterSelectionUIAnimator: Object {obj.name} is child of deck preview panel");
            return true;
        }
            
        return false;
    }
    
    private bool IsObjectInCharacterOrPetSelection(GameObject obj)
    {
        if (obj == null) 
        {
            Debug.Log($"CharacterSelectionUIAnimator: IsObjectInCharacterOrPetSelection - obj is null");
            return false;
        }
        
        /* Debug.Log($"CharacterSelectionUIAnimator: IsObjectInCharacterOrPetSelection checking: {obj.name}"); */
        
        // Method 1: Check if object is a child of character or pet grid parents
        if (characterGridParentRect != null && obj.transform.IsChildOf(characterGridParentRect.transform))
        {
            Debug.Log($"CharacterSelectionUIAnimator: Object {obj.name} is child of character grid parent ({characterGridParentRect.name})");
            return true;
        }
            
        if (petGridParentRect != null && obj.transform.IsChildOf(petGridParentRect.transform))
        {
            Debug.Log($"CharacterSelectionUIAnimator: Object {obj.name} is child of pet grid parent ({petGridParentRect.name})");
            return true;
        }
        
        // Method 2: Check by traversing up the hierarchy for selection items
        Transform currentTransform = obj.transform;
        int hierarchyDepth = 0;
        while (currentTransform != null && hierarchyDepth < 10) // Prevent infinite loops
        {
            string objName = currentTransform.name.ToLower();
            /* Debug.Log($"CharacterSelectionUIAnimator: Checking hierarchy level {hierarchyDepth}: {currentTransform.name}"); */
            
            // Look for selection item indicators (expanded patterns)
            if ((objName.Contains("character") && (objName.Contains("item") || objName.Contains("selection"))) ||
                (objName.Contains("pet") && (objName.Contains("item") || objName.Contains("selection"))) ||
                objName.Contains("selectionitem") ||
                objName.StartsWith("character") ||
                objName.StartsWith("pet"))
            {
                /* Debug.Log($"CharacterSelectionUIAnimator: Found selection item name pattern: {currentTransform.name}"); */
                // Check if this or a parent has a Button component
                if (currentTransform.GetComponent<UnityEngine.UI.Button>() != null)
                {
                    /* Debug.Log($"CharacterSelectionUIAnimator: Found selection button: {currentTransform.name}"); */
                    return true;
                }
            }
            
            // Check if we're in a known character/pet grid area by checking parent names
            if (currentTransform.parent != null)
            {
                string parentName = currentTransform.parent.name.ToLower();
                if (parentName.Contains("character") && (parentName.Contains("grid") || parentName.Contains("parent") || parentName.Contains("content")) ||
                    parentName.Contains("pet") && (parentName.Contains("grid") || parentName.Contains("parent") || parentName.Contains("content")))
                {
                    /* Debug.Log($"CharacterSelectionUIAnimator: Found selection area by parent name: {currentTransform.parent.name}"); */
                    return true;
                }
            }
            
            currentTransform = currentTransform.parent;
            hierarchyDepth++;
        }
        
        // Method 3: Alternative check by name patterns (fallback)
        string rootObjName = obj.name.ToLower();
        if (rootObjName.Contains("character") || rootObjName.Contains("pet"))
        {
            /* Debug.Log($"CharacterSelectionUIAnimator: Checking root object name pattern: {obj.name}"); */
            // Only consider it a selection item if it has a Button component or is clickable
            if (obj.GetComponent<UnityEngine.UI.Button>() != null)
            {
                /* Debug.Log($"CharacterSelectionUIAnimator: Found selection item by root name pattern with button: {obj.name}"); */
                return true;
            }
            
            if (obj.GetComponentInParent<UnityEngine.UI.Button>() != null)
            {
                /* Debug.Log($"CharacterSelectionUIAnimator: Found selection item by root name pattern with parent button: {obj.name}"); */
                return true;
            }
        }
        
        /* Debug.Log($"CharacterSelectionUIAnimator: Object {obj.name} is NOT a selection item"); */
        return false;
    }
    
    #endregion
    
    #region Cleanup
    
    private void OnDestroy()
    {
        // Kill all tweens to prevent memory leaks
        playerListTween?.Kill();
        deckPreviewTween?.Kill();
        
        // Unsubscribe from model dissolve animator events
        if (modelDissolveAnimator != null)
        {
            modelDissolveAnimator.OnTransitionStarted -= (model) => OnModelTransitionStarted?.Invoke(model);
            modelDissolveAnimator.OnTransitionCompleted -= (model) => OnModelTransitionCompleted?.Invoke(model);
        }
        
        // Clear all events
        OnPlayerListVisibilityChanged = null;
        OnDeckPreviewVisibilityChanged = null;
        OnModelTransitionStarted = null;
        OnModelTransitionCompleted = null;
        
        Debug.Log("CharacterSelectionUIAnimator: Destroyed and cleaned up");
    }
    
    private void OnDisable()
    {
        // Kill all active tweens when disabled
        playerListTween?.Kill();
        deckPreviewTween?.Kill();
        
        // Force hide all panels immediately when disabled
        if (playerListPanel != null && isPlayerListVisible)
        {
            playerListPanel.SetActive(false);
            isPlayerListVisible = false;
        }
        
        if (deckPreviewPanel != null && isDeckPreviewVisible)
        {
            deckPreviewPanel.SetActive(false);
            isDeckPreviewVisible = false;
        }
        
        Debug.Log("CharacterSelectionUIAnimator: Disabled and force-closed all panels");
    }
    
    private void OnValidate()
    {
        // Validate animation settings
        if (slideAnimationDuration <= 0f)
        {
            slideAnimationDuration = 0.3f;
            Debug.LogWarning("CharacterSelectionUIAnimator: slideAnimationDuration must be positive, reset to 0.3f");
        }
        
        // Validate the model dissolve animator reference
        if (modelDissolveAnimator == null)
        {
            modelDissolveAnimator = GetComponent<ModelDissolveAnimator>();
        }
    }
    
    #endregion
    
    #region Public Properties
    
    public bool IsPlayerListVisible => isPlayerListVisible;
    public bool IsDeckPreviewVisible => isDeckPreviewVisible;
    public bool IsAnyDeckVisible => isDeckPreviewVisible;
    
    // Control methods for external systems
    public void SetClickOutsideDetectionEnabled(bool enabled)
    {
        enableClickOutsideDetection = enabled;
        Debug.Log($"CharacterSelectionUIAnimator: Click outside detection set to {enabled}");
    }
    
    public void ForceKeepDeckPanelsOpen(bool keepOpen)
    {
        enableClickOutsideDetection = !keepOpen;
        Debug.Log($"CharacterSelectionUIAnimator: Force keep deck panels open: {keepOpen} (click detection: {enableClickOutsideDetection})");
    }
    
    public bool IsClickOutsideDetectionEnabled => enableClickOutsideDetection;
    
    #endregion
}