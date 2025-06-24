using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// Handles sliding animations for UI panels in the character selection screen
/// Supports both UI button-based selection and 3D model-based selection
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
    
    // Panel references
    private GameObject playerListPanel;
    private GameObject characterDeckPanel;
    private GameObject petDeckPanel;
    private RectTransform playerListRect;
    private RectTransform characterDeckRect;
    private RectTransform petDeckRect;
    
    // Click detection areas
    private RectTransform characterGridParentRect;
    private RectTransform petGridParentRect;
    
    // Animation state
    private bool isPlayerListVisible = false;
    private bool isCharacterDeckVisible = false;
    private bool isPetDeckVisible = false;
    private Tween playerListTween;
    private Tween characterDeckTween;
    private Tween petDeckTween;
    
    // Click detection control
    private bool enableClickOutsideDetection = true;
    
    // Events for state changes
    public System.Action<bool> OnPlayerListVisibilityChanged;
    public System.Action<bool> OnCharacterDeckVisibilityChanged;
    public System.Action<bool> OnPetDeckVisibilityChanged;
    
    #region Initialization
    
    public void Initialize(GameObject playerListPanel, GameObject characterDeckPanel, GameObject petDeckPanel)
    {
        this.playerListPanel = playerListPanel;
        this.characterDeckPanel = characterDeckPanel;
        this.petDeckPanel = petDeckPanel;
        
        if (playerListPanel != null)
        {
            playerListRect = playerListPanel.GetComponent<RectTransform>();
        }
        
        if (characterDeckPanel != null)
        {
            characterDeckRect = characterDeckPanel.GetComponent<RectTransform>();
        }
        
        if (petDeckPanel != null)
        {
            petDeckRect = petDeckPanel.GetComponent<RectTransform>();
        }
        
        // Initialize panels in hidden state
        SetupInitialPanelPositions();
        
        /* Debug.Log("CharacterSelectionUIAnimator: Initialized with player list, character deck, and pet deck panels"); */
    }
    
    private void SetupInitialPanelPositions()
    {
        /* Debug.Log("CharacterSelectionUIAnimator: SetupInitialPanelPositions() - Setting up initial panel positions"); */
        
        // Set up all panels using the generic setup method (supports OffscreenPanelSetup)
        SetupPanel(playerListPanel, playerListRect, playerListHiddenOffset);
        SetupPanel(characterDeckPanel, characterDeckRect, deckPreviewHiddenOffset);
        SetupPanel(petDeckPanel, petDeckRect, deckPreviewHiddenOffset);
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
    
    #region Character Deck Panel Animation
    
    public void ShowCharacterDeckPanel()
    {
        /* Debug.Log($"CharacterSelectionUIAnimator: ShowCharacterDeckPanel() called - characterDeckPanel: {characterDeckPanel?.name}, isVisible: {isCharacterDeckVisible}"); */
        
        if (characterDeckPanel == null || isCharacterDeckVisible) return;
        
        // Kill existing tween
        characterDeckTween?.Kill();
        
        characterDeckPanel.SetActive(true);
        /* Debug.Log("CharacterSelectionUIAnimator: Character deck panel set to ACTIVE - starting slide-in animation"); */
        isCharacterDeckVisible = true;
        
        // Get positions from DeckPreviewPanelSetup if available
        Vector2 hiddenPos = GetDeckPanelHiddenPosition(characterDeckPanel);
        Vector2 targetPos = GetDeckPanelTargetPosition(characterDeckPanel);
        
        // Set starting position and animate to target
        characterDeckRect.anchoredPosition = hiddenPos;
        characterDeckTween = characterDeckRect.DOAnchorPos(targetPos, slideAnimationDuration)
            .SetEase(slideInEase)
            .OnComplete(() => {
                /* Debug.Log("CharacterSelectionUIAnimator: Character deck panel slide-in animation completed"); */
                /* Debug.Log($"CharacterSelectionUIAnimator: Character deck panel active state after animation: {characterDeckPanel.activeInHierarchy}"); */
                OnCharacterDeckVisibilityChanged?.Invoke(true);
            });
        
        /* Debug.Log("CharacterSelectionUIAnimator: Character deck panel sliding in from below with DOTween"); */
    }
    
    public void HideCharacterDeckPanel()
    {
        /* Debug.Log($"CharacterSelectionUIAnimator: HideCharacterDeckPanel() called - characterDeckPanel: {characterDeckPanel?.name}, isVisible: {isCharacterDeckVisible}"); */
        
        if (characterDeckPanel == null || !isCharacterDeckVisible) return;
        
        // Kill existing tween
        characterDeckTween?.Kill();
        
        isCharacterDeckVisible = false;
        
        Vector2 hiddenPos = GetDeckPanelHiddenPosition(characterDeckPanel);
        characterDeckTween = characterDeckRect.DOAnchorPos(hiddenPos, slideAnimationDuration)
            .SetEase(slideOutEase)
            .OnComplete(() => {
                characterDeckPanel.SetActive(false);
                Debug.Log("CharacterSelectionUIAnimator: Character deck panel set to INACTIVE after slide-out animation completed");
                OnCharacterDeckVisibilityChanged?.Invoke(false);
            });
        
        Debug.Log("CharacterSelectionUIAnimator: Character deck panel sliding out to below with DOTween");
    }
    
    #endregion
    
    #region Pet Deck Panel Animation
    
    public void ShowPetDeckPanel()
    {
        /* Debug.Log($"CharacterSelectionUIAnimator: ShowPetDeckPanel() called - petDeckPanel: {petDeckPanel?.name}, isVisible: {isPetDeckVisible}"); */
        
        if (petDeckPanel == null || isPetDeckVisible) return;
        
        // Kill existing tween
        petDeckTween?.Kill();
        
        petDeckPanel.SetActive(true);
        /* Debug.Log("CharacterSelectionUIAnimator: Pet deck panel set to ACTIVE - starting slide-in animation"); */
        isPetDeckVisible = true;
        
        // Get positions from DeckPreviewPanelSetup if available
        Vector2 hiddenPos = GetDeckPanelHiddenPosition(petDeckPanel);
        Vector2 targetPos = GetDeckPanelTargetPosition(petDeckPanel);
        
        // Set starting position and animate to target
        petDeckRect.anchoredPosition = hiddenPos;
        petDeckTween = petDeckRect.DOAnchorPos(targetPos, slideAnimationDuration)
            .SetEase(slideInEase)
            .OnComplete(() => {
                /* Debug.Log("CharacterSelectionUIAnimator: Pet deck panel slide-in animation completed"); */
                /* Debug.Log($"CharacterSelectionUIAnimator: Pet deck panel active state after animation: {petDeckPanel.activeInHierarchy}"); */
                OnPetDeckVisibilityChanged?.Invoke(true);
            });
        
        /* Debug.Log("CharacterSelectionUIAnimator: Pet deck panel sliding in from below with DOTween"); */
    }
    
    public void HidePetDeckPanel()
    {
        /* Debug.Log($"CharacterSelectionUIAnimator: HidePetDeckPanel() called - petDeckPanel: {petDeckPanel?.name}, isVisible: {isPetDeckVisible}"); */
        
        if (petDeckPanel == null || !isPetDeckVisible) return;
        
        // Kill existing tween
        petDeckTween?.Kill();
        
        isPetDeckVisible = false;
        
        Vector2 hiddenPos = GetDeckPanelHiddenPosition(petDeckPanel);
        petDeckTween = petDeckRect.DOAnchorPos(hiddenPos, slideAnimationDuration)
            .SetEase(slideOutEase)
            .OnComplete(() => {
                petDeckPanel.SetActive(false);
                Debug.Log("CharacterSelectionUIAnimator: Pet deck panel set to INACTIVE after slide-out animation completed");
                OnPetDeckVisibilityChanged?.Invoke(false);
            });
        
        Debug.Log("CharacterSelectionUIAnimator: Pet deck panel sliding out to below with DOTween");
    }
    
    public void HideAllDeckPanels()
    {
        Debug.Log("CharacterSelectionUIAnimator: HideAllDeckPanels() called - hiding both character and pet deck panels");
        HideCharacterDeckPanel();
        HidePetDeckPanel();
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
        if (enableClickOutsideDetection && (isCharacterDeckVisible || isPetDeckVisible) && Input.GetMouseButtonDown(0))
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
            HideAllDeckPanels();
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
            Transform current = hitObject.transform;
            int depth = 0;
            while (current != null && depth < 10) // Prevent infinite loops
            {
                if (Is3DSelectionModel(current.gameObject))
                {
                    /* Debug.Log($"CharacterSelectionUIAnimator: 3D model selection detected on parent {current.name}"); */
                    return true;
                }
                current = current.parent;
                depth++;
            }
        }
        
        /* Debug.Log("CharacterSelectionUIAnimator: No 3D model selection detected"); */
        return false;
    }
    
    private bool Is3DSelectionModel(GameObject obj)
    {
        if (obj == null) return false;
        
        // Check if this object has an EntitySelectionController component
        EntitySelectionController controller = obj.GetComponent<EntitySelectionController>();
        if (controller != null)
        {
            Debug.Log($"CharacterSelectionUIAnimator: Found EntitySelectionController on {obj.name}");
            return true;
        }
        
        // Check if this object is a child of something with an EntitySelectionController
        controller = obj.GetComponentInParent<EntitySelectionController>();
        if (controller != null)
        {
            Debug.Log($"CharacterSelectionUIAnimator: Found EntitySelectionController in parent hierarchy of {obj.name}");
            return true;
        }
        
        // Check if this object is a child of the character or pet grid parents
        if (characterGridParentRect != null && obj.transform.IsChildOf(characterGridParentRect.transform))
        {
            /* Debug.Log($"CharacterSelectionUIAnimator: Object {obj.name} is child of character grid parent"); */
            return true;
        }
        
        if (petGridParentRect != null && obj.transform.IsChildOf(petGridParentRect.transform))
        {
            /* Debug.Log($"CharacterSelectionUIAnimator: Object {obj.name} is child of pet grid parent"); */
            return true;
        }
        
        return false;
    }
    
    private bool CheckUIElementClick()
    {
        // Use EventSystem to raycast and see what UI elements we hit
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
        
        List<RaycastResult> results = new List<RaycastResult>();
        
        // Try both character and pet canvases to ensure we can detect clicks on both
        bool foundValidClick = false;
        
        // Try character canvas first
        Canvas characterCanvas = FindCanvasForCharacterItems();
        if (characterCanvas != null)
        {
            GraphicRaycaster raycaster = characterCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.Raycast(pointerData, results);
                /* Debug.Log($"CharacterSelectionUIAnimator: Using Character Canvas: {characterCanvas.name} - found {results.Count} hits"); */
                foundValidClick = CheckRaycastResults(results);
                if (foundValidClick) return true;
            }
        }
        
        // Try pet canvas if character canvas didn't find anything
        Canvas petCanvas = FindCanvasForPetItems();
        if (petCanvas != null && petCanvas != characterCanvas) // Don't duplicate if same canvas
        {
            results.Clear(); // Clear previous results
            GraphicRaycaster raycaster = petCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.Raycast(pointerData, results);
                /* Debug.Log($"CharacterSelectionUIAnimator: Using Pet Canvas: {petCanvas.name} - found {results.Count} hits"); */
                foundValidClick = CheckRaycastResults(results);
                if (foundValidClick) return true;
            }
        }
        
        /* Debug.Log("CharacterSelectionUIAnimator: No valid UI selection items found in canvas raycast results"); */
        return false;
    }
    
    private bool CheckRaycastResults(List<RaycastResult> results)
    {
        // Log all hit objects for debugging
        for (int i = 0; i < results.Count; i++)
        {
            GameObject hitObject = results[i].gameObject;
            Debug.Log($"CharacterSelectionUIAnimator: Raycast hit [{i}]: {hitObject.name} (layer: {hitObject.layer})");
        }
        
        // Check if any of the hit objects are character or pet selection items
        foreach (RaycastResult result in results)
        {
            GameObject hitObject = result.gameObject;
            
            // Check if we clicked on a deck preview panel (should keep open)
            if (IsObjectInDeckPanel(hitObject))
            {
                Debug.Log($"CharacterSelectionUIAnimator: Click on deck panel ({hitObject.name}) - keeping open");
                return true;
            }
            
            // Check if we clicked on a character or pet selection item
            if (IsObjectInCharacterOrPetSelection(hitObject))
            {
                /* Debug.Log($"CharacterSelectionUIAnimator: Click on selection item ({hitObject.name}) - keeping open"); */
                return true;
            }
            
            /* Debug.Log($"CharacterSelectionUIAnimator: Checked object: {hitObject.name} (not a selection item)"); */
        }
        
        return false;
    }
    
    private Canvas FindCanvasForCharacterItems()
    {
        if (characterGridParentRect != null && characterGridParentRect.childCount > 0)
        {
            // Get the first character selection item and find its Canvas
            Transform firstCharacterItem = characterGridParentRect.GetChild(0);
            Canvas itemCanvas = firstCharacterItem.GetComponent<Canvas>();
            if (itemCanvas != null && itemCanvas.GetComponent<GraphicRaycaster>() != null)
            {
                Debug.Log($"CharacterSelectionUIAnimator: Found Canvas from character item: {itemCanvas.name}");
                return itemCanvas;
            }
        }
        
        Debug.Log("CharacterSelectionUIAnimator: Could not find Canvas on character selection items");
        return null;
    }
    
    private Canvas FindCanvasForPetItems()
    {
        if (petGridParentRect != null && petGridParentRect.childCount > 0)
        {
            // Get the first pet selection item and find its Canvas
            Transform firstPetItem = petGridParentRect.GetChild(0);
            Canvas itemCanvas = firstPetItem.GetComponent<Canvas>();
            if (itemCanvas != null && itemCanvas.GetComponent<GraphicRaycaster>() != null)
            {
                Debug.Log($"CharacterSelectionUIAnimator: Found Canvas from pet item: {itemCanvas.name}");
                return itemCanvas;
            }
        }
        
        Debug.Log("CharacterSelectionUIAnimator: Could not find Canvas on pet selection items");
        return null;
    }
    
    /// <summary>
    /// Set up click outside detection with specific UI areas to check
    /// </summary>
    public void SetClickDetectionAreas(RectTransform characterGridParent, RectTransform petGridParent)
    {
        characterGridParentRect = characterGridParent;
        petGridParentRect = petGridParent;
        /* Debug.Log($"CharacterSelectionUIAnimator: Click detection areas set - Character: {characterGridParent?.name} (null: {characterGridParent == null}), Pet: {petGridParent?.name} (null: {petGridParent == null})"); */
        
        // Additional debugging info
        if (characterGridParent != null)
        {
            Debug.Log($"CharacterSelectionUIAnimator: Character grid parent active: {characterGridParent.gameObject.activeInHierarchy}, children: {characterGridParent.transform.childCount}");
        }
        if (petGridParent != null)
        {
            Debug.Log($"CharacterSelectionUIAnimator: Pet grid parent active: {petGridParent.gameObject.activeInHierarchy}, children: {petGridParent.transform.childCount}");
        }
        
        // If either is null, try to find them automatically
        if (characterGridParent == null || petGridParent == null)
        {
            Debug.LogWarning("CharacterSelectionUIAnimator: Some grid parent references are null, attempting to find them automatically...");
            ValidateAndFindGridParents();
        }
    }
    
    /// <summary>
    /// Try to automatically find the character and pet grid parents if they weren't set properly
    /// </summary>
    private void ValidateAndFindGridParents()
    {
        // Try to find grid parents by name if they're missing
        if (characterGridParentRect == null)
        {
            RectTransform[] gridParents = FindObjectsByType<RectTransform>(FindObjectsSortMode.None);
            foreach (RectTransform grid in gridParents)
            {
                string name = grid.name.ToLower();
                if (name.Contains("character") && (name.Contains("grid") || name.Contains("parent") || name.Contains("content")))
                {
                    characterGridParentRect = grid;
                    Debug.Log($"CharacterSelectionUIAnimator: Auto-found character grid parent: {grid.name}");
                    break;
                }
            }
        }
        
        if (petGridParentRect == null)
        {
            RectTransform[] gridParents = FindObjectsByType<RectTransform>(FindObjectsSortMode.None);
            foreach (RectTransform grid in gridParents)
            {
                string name = grid.name.ToLower();
                if (name.Contains("pet") && (name.Contains("grid") || name.Contains("parent") || name.Contains("content")))
                {
                    petGridParentRect = grid;
                    /* Debug.Log($"CharacterSelectionUIAnimator: Auto-found pet grid parent: {grid.name}"); */
                    break;
                }
            }
        }
        
        // Log final status
        /* Debug.Log($"CharacterSelectionUIAnimator: Final grid parent status - Character: {characterGridParentRect?.name ?? "NULL"}, Pet: {petGridParentRect?.name ?? "NULL"}"); */
        
        // Disable click detection if we still don't have valid setup
        DisableClickDetectionIfInvalid();
    }
    
    /// <summary>
    /// Debug method to check current click detection state
    /// </summary>
    public void DebugClickDetectionState()
    {
        /* Debug.Log($"CharacterSelectionUIAnimator: Click Detection State:"); */
        /* Debug.Log($"  - enableClickOutsideDetection: {enableClickOutsideDetection}"); */
        /* Debug.Log($"  - characterGridParentRect: {characterGridParentRect?.name ?? "NULL"}"); */
        /* Debug.Log($"  - petGridParentRect: {petGridParentRect?.name ?? "NULL"}"); */
        /* Debug.Log($"  - characterDeckPanel: {characterDeckPanel?.name ?? "NULL"} (active: {characterDeckPanel?.activeInHierarchy})"); */
        /* Debug.Log($"  - petDeckPanel: {petDeckPanel?.name ?? "NULL"} (active: {petDeckPanel?.activeInHierarchy})"); */
        
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindFirstObjectByType<Canvas>();
        }
        
        /* Debug.Log($"  - Canvas: {canvas?.name ?? "NULL"}"); */
        /* Debug.Log($"  - GraphicRaycaster: {canvas?.GetComponent<GraphicRaycaster>() != null}"); */
        
        // Check if we should disable click detection due to missing references
        bool hasValidSetup = characterGridParentRect != null || petGridParentRect != null;
        if (!hasValidSetup && enableClickOutsideDetection)
        {
            Debug.LogWarning("CharacterSelectionUIAnimator: Click detection is enabled but no valid grid parent references found. Consider disabling click detection or fixing the references.");
        }
    }
    
    /// <summary>
    /// Force disable click detection if the setup is invalid
    /// </summary>
    public void DisableClickDetectionIfInvalid()
    {
        bool hasValidSetup = characterGridParentRect != null || petGridParentRect != null;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindFirstObjectByType<Canvas>();
        }
        bool hasValidCanvas = canvas != null && canvas.GetComponent<GraphicRaycaster>() != null;
        
        if (!hasValidSetup || !hasValidCanvas)
        {
            Debug.LogWarning($"CharacterSelectionUIAnimator: Invalid setup detected (hasValidSetup: {hasValidSetup}, hasValidCanvas: {hasValidCanvas}). Disabling click detection to prevent issues.");
            enableClickOutsideDetection = false;
        }
    }
    
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
        if (characterDeckPanel != null && obj.transform.IsChildOf(characterDeckPanel.transform))
        {
            Debug.Log($"CharacterSelectionUIAnimator: Object {obj.name} is child of character deck panel");
            return true;
        }
            
        if (petDeckPanel != null && obj.transform.IsChildOf(petDeckPanel.transform))
        {
            Debug.Log($"CharacterSelectionUIAnimator: Object {obj.name} is child of pet deck panel");
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
            
            // PlayerSelectionIndicator functionality removed - skipping these checks
            
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
        characterDeckTween?.Kill();
        petDeckTween?.Kill();
    }
    
    private void OnDisable()
    {
        // Kill all tweens when disabled
        playerListTween?.Kill();
        characterDeckTween?.Kill();
        petDeckTween?.Kill();
    }
    
    #endregion
    
    #region Public Properties
    
    public bool IsPlayerListVisible => isPlayerListVisible;
    public bool IsCharacterDeckVisible => isCharacterDeckVisible;
    public bool IsPetDeckVisible => isPetDeckVisible;
    public bool IsAnyDeckVisible => isCharacterDeckVisible || isPetDeckVisible;
    
    /// <summary>
    /// Enable or disable click-outside detection. When disabled, panels will stay open until manually closed.
    /// </summary>
    public void SetClickOutsideDetectionEnabled(bool enabled)
    {
        enableClickOutsideDetection = enabled;
        Debug.Log($"CharacterSelectionUIAnimator: Click outside detection {(enabled ? "enabled" : "disabled")}");
    }
    
    /// <summary>
    /// Force both deck panels to stay open, regardless of click detection
    /// </summary>
    public void ForceKeepDeckPanelsOpen(bool keepOpen)
    {
        enableClickOutsideDetection = !keepOpen;
        Debug.Log($"CharacterSelectionUIAnimator: Force keep deck panels open: {keepOpen}");
    }
    
    /// <summary>
    /// Check if click detection is currently enabled
    /// </summary>
    public bool IsClickOutsideDetectionEnabled => enableClickOutsideDetection;
    
    #endregion
}