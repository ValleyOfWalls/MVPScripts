using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using FishNet.Object;
using UnityEngine.EventSystems;
using MVPScripts.Utility;

#if DOTWEEN_ENABLED || !UNITY_EDITOR && !UNITY_STANDALONE
using DG.Tweening;
#endif

/// <summary>
/// Handles deck preview functionality for character selection screen.
/// This controller manages the display and hiding of deck preview panels and their content.
/// Now uses a card summary system with individual card animations and hover tooltips.
/// Uses a single shared preview area for both characters and pets.
/// Integrates with AnimationQueueManager for proper animation sequencing.
/// Attach to: Same GameObject as CharacterSelectionUIManager
/// 
/// CARD SUMMARY PREFAB REQUIREMENTS:
/// The cardSummaryPrefab should be a UI GameObject with:
/// - Root GameObject with Image component (for background and hover detection)
/// - Child GameObject with TextMeshProUGUI component (for displaying card name + count)
/// - RectTransform sized appropriately (recommended: full width, 30 height)
/// - Background Image should have raycastTarget = true for hover detection
/// - The TextMeshProUGUI will be automatically populated with "CardName" or "CardName x2" format
/// 
/// If no prefab is assigned, a basic one will be created automatically.
/// </summary>
public class DeckPreviewController : MonoBehaviour
{
    [Header("Shared Deck Preview Panel References")]
    [SerializeField] private GameObject deckPreviewPanel;
    [SerializeField] private ScrollRect deckPreviewScrollView;
    [SerializeField] private Transform deckPreviewGridParent;
    [SerializeField] private TextMeshProUGUI deckPreviewTitle;
    
    [Header("Deck Preview Settings - Card Summary System")]
    [SerializeField] private GameObject cardSummaryPrefab; // New simple prefab for card summaries
    [SerializeField] private GameObject deckCardPrefab; // Used for hover tooltips only
    
    [Header("Card Summary Animation Settings")]
    [SerializeField] private float cardAnimationDuration = 0.25f;
    [SerializeField] private float staggerDelayMin = 0.05f;
    [SerializeField] private float staggerDelayMax = 0.15f;
    [SerializeField] private float offscreenRightOffset = 300f; // How far offscreen to start
    [SerializeField] private float animationGapDuration = 0.2f; // Gap between animate out and animate in
    [SerializeField] private float layoutWaitTime = 0.1f; // Time to wait for layout group

#if DOTWEEN_ENABLED || !UNITY_EDITOR && !UNITY_STANDALONE
    [SerializeField] private Ease slideInEase = Ease.OutCubic;
    [SerializeField] private Ease slideOutEase = Ease.InCubic;
#endif
    
    [Header("Hover Tooltip Settings")]
    [SerializeField] private Vector2 tooltipOffset = new Vector2(-20f, 20f); // Bottom-right positioning
    [SerializeField] private float tooltipShowDelay = 0f; // No delay
    [SerializeField] private bool enableTooltipClamping = false; // Temporarily disabled for debugging
    [SerializeField] private bool enableMouseFollowing = true; // Follow mouse cursor
    
    // Dependencies
    private CharacterSelectionUIAnimator uiAnimator;
    
    // Current deck content - now using shared card summaries
    private List<CardSummaryItem> currentDeckSummaries = new List<CardSummaryItem>();
    
    // Animation management with AnimationQueueManager
    private AnimationQueueManager<DeckAnimationRequest> animationQueue;
    
    // Tooltip management
    private GameObject currentTooltipCard;
    private Coroutine tooltipDelayCoroutine;
    private Coroutine mouseFollowCoroutine;
    
    // Available data
    private List<CharacterData> availableCharacters;
    private List<PetData> availablePets;
    
    // Current selections
    private int currentCharacterIndex = -1;
    private int currentPetIndex = -1;
    private EntityType currentDisplayType = EntityType.None;
    
    // State
    private bool isPlayerReady = false;
    
    private enum EntityType
    {
        None,
        Character,
        Pet
    }
    
    /// <summary>
    /// Request type for deck animations
    /// </summary>
    private class DeckAnimationRequest
    {
        public EntityType entityType;
        public int entityIndex;
        public DeckData deckData;
        public string deckTitle;
        public bool isRefresh; // Whether this is a refresh (requires fade out first)
        
        public DeckAnimationRequest(EntityType type, int index, DeckData deck, string title, bool refresh = false)
        {
            entityType = type;
            entityIndex = index;
            deckData = deck;
            deckTitle = title;
            isRefresh = refresh;
        }
    }
    
    #region Initialization
    
    public void Initialize(CharacterSelectionUIAnimator animator, List<CharacterData> characters, List<PetData> pets)
    {
        uiAnimator = animator;
        availableCharacters = characters;
        availablePets = pets;
        
        // Ensure the shared deck preview panel is always visible (no animation)
        if (deckPreviewPanel != null)
        {
            deckPreviewPanel.SetActive(true);
        }
        
        // Initialize animation queue manager
        SetupAnimationQueue();
        
        Debug.Log("DeckPreviewController: Initialized with UI animator and data - using shared card summary system with AnimationQueueManager");
    }
    
    private void SetupAnimationQueue()
    {
        animationQueue = new AnimationQueueManager<DeckAnimationRequest>(this);
        
        // Set up delegates for animation queue
        animationQueue.ExecuteRequest = ExecuteDeckAnimationRequest;
        animationQueue.OptimizeQueue = OptimizeDeckAnimationQueue;
        animationQueue.RequiresFadeOut = (request) => request.isRefresh;
        animationQueue.CreateFadeOutRequest = () => new DeckAnimationRequest(EntityType.None, -1, null, "", false);
        
        // Set up events
        animationQueue.OnAnimationStateChanged += (state) => {
            Debug.Log($"DeckPreviewController: Animation queue state changed to {state}");
        };
        
        animationQueue.OnQueueProcessingStarted += (count) => {
            Debug.Log($"DeckPreviewController: Animation queue processing started with {count} requests");
        };
        
        animationQueue.OnQueueProcessingCompleted += () => {
            Debug.Log("DeckPreviewController: Animation queue processing completed");
        };
    }
    
    #endregion
    
    #region Deck Preview Management
    
    /// <summary>
    /// Shows the character deck preview for the specified character index
    /// </summary>
    public void ShowCharacterDeck(int characterIndex, bool isReady = false)
    {
        if (isReady)
        {
            Debug.Log("DeckPreviewController: Player is ready, clearing deck preview");
            ClearAllDeckPreviews();
            return;
        }
        
        if (characterIndex < 0 || characterIndex >= availableCharacters.Count)
        {
            Debug.Log($"DeckPreviewController: Invalid character index {characterIndex}, cannot show deck");
            return;
        }
        
        CharacterData character = availableCharacters[characterIndex];
        DeckData deckToShow = character.StarterDeck;
        string deckTitle = $"Character Deck: {character.CharacterName}";
        
        // Check if this is a selection change that needs animation
        bool isSelectionChange = (currentDisplayType != EntityType.Character || currentCharacterIndex != characterIndex) && currentDeckSummaries.Count > 0;
        
        currentCharacterIndex = characterIndex;
        currentDisplayType = EntityType.Character;
        
        // Queue the animation request
        var request = new DeckAnimationRequest(EntityType.Character, characterIndex, deckToShow, deckTitle, isSelectionChange);
        animationQueue.QueueRequest(request);
        
        Debug.Log($"DeckPreviewController: Queued character deck request for {character.CharacterName} (refresh: {isSelectionChange})");
    }
    
    /// <summary>
    /// Shows the pet deck preview for the specified pet index
    /// </summary>
    public void ShowPetDeck(int petIndex, bool isReady = false)
    {
        if (isReady)
        {
            Debug.Log("DeckPreviewController: Player is ready, clearing deck preview");
            ClearAllDeckPreviews();
            return;
        }
        
        if (petIndex < 0 || petIndex >= availablePets.Count)
        {
            Debug.Log($"DeckPreviewController: Invalid pet index {petIndex}, cannot show deck");
            return;
        }
        
        PetData pet = availablePets[petIndex];
        DeckData deckToShow = pet.StarterDeck;
        string deckTitle = $"Pet Deck: {pet.PetName}";
        
        // Check if this is a selection change that needs animation
        bool isSelectionChange = (currentDisplayType != EntityType.Pet || currentPetIndex != petIndex) && currentDeckSummaries.Count > 0;
        
        currentPetIndex = petIndex;
        currentDisplayType = EntityType.Pet;
        
        // Queue the animation request
        var request = new DeckAnimationRequest(EntityType.Pet, petIndex, deckToShow, deckTitle, isSelectionChange);
        animationQueue.QueueRequest(request);
        
        Debug.Log($"DeckPreviewController: Queued pet deck request for {pet.PetName} (refresh: {isSelectionChange})");
    }
    
    /// <summary>
    /// Forces a deck refresh when switching between character/pet tabs
    /// </summary>
    public void RefreshCurrentDeck()
    {
        if (currentDisplayType == EntityType.Character && currentCharacterIndex >= 0)
        {
            Debug.Log("DeckPreviewController: Refreshing current character deck due to tab switch");
            ShowCharacterDeck(currentCharacterIndex, isPlayerReady);
        }
        else if (currentDisplayType == EntityType.Pet && currentPetIndex >= 0)
        {
            Debug.Log("DeckPreviewController: Refreshing current pet deck due to tab switch");
            ShowPetDeck(currentPetIndex, isPlayerReady);
        }
    }
    
    /// <summary>
    /// Clears all deck preview content (no panel hiding)
    /// </summary>
    public void HideAllDeckPreviews()
    {
        ClearAllDeckPreviews();
        currentDisplayType = EntityType.None;
    }
    
    /// <summary>
    /// Updates the ready state and manages deck visibility accordingly
    /// </summary>
    public void SetPlayerReadyState(bool ready)
    {
        isPlayerReady = ready;
        
        if (ready)
        {
            Debug.Log("DeckPreviewController: Player became ready - clearing deck preview");
            ClearAllDeckPreviews();
            currentDisplayType = EntityType.None;
        }
        else
        {
            Debug.Log("DeckPreviewController: Player became not ready - showing deck preview if selections exist");
            ShowCurrentDeckPreview();
        }
    }
    
    /// <summary>
    /// Shows deck preview based on current selections (if player is not ready)
    /// </summary>
    public void ShowCurrentDeckPreview()
    {
        if (isPlayerReady)
        {
            Debug.Log("DeckPreviewController: Player is ready - not showing deck preview");
            return;
        }
        
        // Show current selection based on display type
        if (currentDisplayType == EntityType.Character && currentCharacterIndex >= 0)
        {
            Debug.Log($"DeckPreviewController: Showing character deck for index {currentCharacterIndex}");
            ShowCharacterDeck(currentCharacterIndex, false);
        }
        else if (currentDisplayType == EntityType.Pet && currentPetIndex >= 0)
        {
            Debug.Log($"DeckPreviewController: Showing pet deck for index {currentPetIndex}");
            ShowPetDeck(currentPetIndex, false);
        }
    }
    
    #endregion
    
    #region Animation Queue Implementation
    
    /// <summary>
    /// Execute a deck animation request through the queue system
    /// </summary>
    private IEnumerator ExecuteDeckAnimationRequest(DeckAnimationRequest request)
    {
        if (request.entityType == EntityType.None)
        {
            // This is a fade out request
            yield return StartCoroutine(AnimateCardsOut());
        }
        else
        {
            // This is a normal deck display request
            yield return StartCoroutine(ProcessDeckDisplayRequest(request));
        }
    }
    
    /// <summary>
    /// Optimize the deck animation queue to remove redundant requests
    /// </summary>
    private List<DeckAnimationRequest> OptimizeDeckAnimationQueue(List<DeckAnimationRequest> requests)
    {
        if (requests.Count <= 1) return requests;
        
        Debug.Log($"DeckPreviewController: Optimizing {requests.Count} deck animation requests");
        
        // Keep only the last request for each entity type
        var optimized = new List<DeckAnimationRequest>();
        
        // Find the last character request
        for (int i = requests.Count - 1; i >= 0; i--)
        {
            if (requests[i].entityType == EntityType.Character)
            {
                optimized.Add(requests[i]);
                break;
            }
        }
        
        // Find the last pet request
        for (int i = requests.Count - 1; i >= 0; i--)
        {
            if (requests[i].entityType == EntityType.Pet)
            {
                optimized.Add(requests[i]);
                break;
            }
        }
        
        // If we have both types, keep only the very last one
        if (optimized.Count > 1)
        {
            var lastOverallRequest = requests[requests.Count - 1];
            optimized.Clear();
            optimized.Add(lastOverallRequest);
        }
        
        Debug.Log($"DeckPreviewController: Optimized to {optimized.Count} deck animation requests");
        return optimized;
    }
    
    /// <summary>
    /// Process a deck display request with proper timing
    /// </summary>
    private IEnumerator ProcessDeckDisplayRequest(DeckAnimationRequest request)
    {
        // Update deck title
        if (deckPreviewTitle != null)
        {
            deckPreviewTitle.text = request.deckTitle;
        }
        
        // Create and animate card summaries
        yield return StartCoroutine(CreateAndAnimateCardSummaries(request.deckData, true));
    }
    
    /// <summary>
    /// Animate cards out only (for fade out requests)
    /// </summary>
    private IEnumerator AnimateCardsOut()
    {
        if (currentDeckSummaries.Count > 0)
        {
            yield return StartCoroutine(AnimateCardSummariesOut(currentDeckSummaries));
            yield return new WaitForSeconds(animationGapDuration);
        }
    }
    
    #endregion
    
    #region Internal Deck Display Methods
    
    // Removed ShowDeckInternal - now using animation queue system
    
    #endregion
    
    #region Card Summary System
    
    /// <summary>
    /// Data structure for card summary items
    /// </summary>
    private class CardSummaryItem
    {
        public GameObject summaryObject;
        public CardData cardData;
        public int count;
        public RectTransform rectTransform;
        public Vector3 targetPosition;
        public bool isAnimating;
        
        public CardSummaryItem(GameObject obj, CardData data, int cardCount)
        {
            summaryObject = obj;
            cardData = data;
            count = cardCount;
            rectTransform = obj.GetComponent<RectTransform>();
            isAnimating = false;
        }
    }
    
    /// <summary>
    /// Creates card summaries from deck data, grouping by card name and counting duplicates
    /// </summary>
    private List<CardSummaryData> CreateCardSummariesFromDeck(DeckData deckData)
    {
        if (deckData?.CardsInDeck == null)
        {
            Debug.LogWarning("DeckPreviewController: Deck data is null or has no cards");
            return new List<CardSummaryData>();
        }
        
        // Group cards by name and count them
        Dictionary<string, CardSummaryData> cardCounts = new Dictionary<string, CardSummaryData>();
        
        foreach (CardData card in deckData.CardsInDeck)
        {
            if (card == null) continue;
            
            string cardName = card.CardName;
            if (cardCounts.ContainsKey(cardName))
            {
                cardCounts[cardName].count++;
        }
        else
        {
                cardCounts[cardName] = new CardSummaryData
                {
                    cardData = card,
                    count = 1
                };
            }
        }
        
        // Convert to list and sort by card name for consistent ordering
        List<CardSummaryData> summaries = cardCounts.Values.ToList();
        summaries.Sort((a, b) => string.Compare(a.cardData.CardName, b.cardData.CardName));
        
        Debug.Log($"DeckPreviewController: Created {summaries.Count} unique card summaries from {deckData.CardsInDeck.Count} total cards");
        return summaries;
    }
    
    /// <summary>
    /// Data structure for card summary information
    /// </summary>
    private class CardSummaryData
    {
        public CardData cardData;
        public int count;
    }
    
    /// <summary>
    /// Creates and animates card summary items into view with proper layout group integration
    /// </summary>
    private IEnumerator CreateAndAnimateCardSummaries(DeckData deckData, bool animateIn)
    {
        // Step 1: Animate out existing cards if any (handled by animation queue)
        if (currentDeckSummaries.Count > 0 && animateIn)
        {
            Debug.Log("DeckPreviewController: Animating out existing cards");
            yield return StartCoroutine(AnimateCardSummariesOut(currentDeckSummaries));
            
            // Wait for gap between out and in animations
            yield return new WaitForSeconds(animationGapDuration);
        }
        else
        {
            ClearCardSummaries(currentDeckSummaries);
        }
        
        if (deckData?.CardsInDeck == null || deckPreviewGridParent == null) yield break;
        
        // Step 2: Create card summary data
        List<CardSummaryData> summaryData = CreateCardSummariesFromDeck(deckData);
        if (summaryData.Count == 0) yield break;
        
        // Step 3: Create summary GameObjects - initially invisible
        foreach (CardSummaryData data in summaryData)
        {
            GameObject summaryObj = CreateCardSummaryObject(data, deckPreviewGridParent);
            if (summaryObj != null)
            {
                CardSummaryItem item = new CardSummaryItem(summaryObj, data.cardData, data.count);
                currentDeckSummaries.Add(item);
                
                // Set up hover tooltip
                SetupCardSummaryHover(item);
                
                // Initially hide the card (make it transparent but don't disable)
                CanvasGroup canvasGroup = summaryObj.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = summaryObj.AddComponent<CanvasGroup>();
                }
                canvasGroup.alpha = 0f;
            }
        }
        
        // Step 4: Wait longer for layout group to position cards properly
        yield return null; // Let layout group start calculating
        yield return new WaitForSeconds(layoutWaitTime); // Give layout group time to complete
        
        // Step 5: Force layout update to ensure positions are correct
        if (deckPreviewGridParent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(deckPreviewGridParent as RectTransform);
        }
        yield return null; // Another frame after forced rebuild
        
        // Step 6: Capture target positions from layout group (now they should be correct)
        foreach (CardSummaryItem item in currentDeckSummaries)
        {
            if (item?.rectTransform != null)
            {
                item.targetPosition = item.rectTransform.localPosition;
                Debug.Log($"DeckPreviewController: Card '{item.cardData.CardName}' target position: {item.targetPosition}");
            }
        }
        
        // Step 7: Animate them in if requested
        if (animateIn && currentDeckSummaries.Count > 0)
        {
            yield return StartCoroutine(AnimateCardSummariesIn(currentDeckSummaries));
        }
        else
        {
            // If not animating, just make them visible
            foreach (CardSummaryItem item in currentDeckSummaries)
            {
                if (item?.summaryObject != null)
                {
                    CanvasGroup canvasGroup = item.summaryObject.GetComponent<CanvasGroup>();
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = 1f;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Creates a single card summary GameObject
    /// </summary>
    private GameObject CreateCardSummaryObject(CardSummaryData data, Transform parentTransform)
    {
        GameObject summaryObj;
        
        if (cardSummaryPrefab != null)
        {
            summaryObj = Instantiate(cardSummaryPrefab, parentTransform);
        }
        else
        {
            // Create basic summary object if no prefab
            summaryObj = CreateBasicCardSummary(data, parentTransform);
        }
        
        if (summaryObj == null) return null;
        
        // Set up the summary content
        SetupCardSummaryContent(summaryObj, data);
        
        return summaryObj;
    }
    
    /// <summary>
    /// Sets up the content of a card summary object
    /// </summary>
    private void SetupCardSummaryContent(GameObject summaryObj, CardSummaryData data)
    {
        // Find text component and set content
        TextMeshProUGUI nameText = summaryObj.GetComponentInChildren<TextMeshProUGUI>();
        if (nameText != null)
        {
            string displayText = data.count > 1 ? $"{data.cardData.CardName} x{data.count}" : data.cardData.CardName;
            nameText.text = displayText;
        }
        else
        {
            Debug.LogWarning($"DeckPreviewController: No TextMeshProUGUI found in card summary prefab for {data.cardData.CardName}");
        }
    }
    
    /// <summary>
    /// Creates a basic card summary object when no prefab is available
    /// </summary>
    private GameObject CreateBasicCardSummary(CardSummaryData data, Transform parentTransform)
    {
        GameObject summaryObj = new GameObject($"CardSummary_{data.cardData.CardName}");
        summaryObj.transform.SetParent(parentTransform, false);
        
        RectTransform rectTransform = summaryObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(0f, 30f); // Full width, 30 height
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        
        // Add background
        Image background = summaryObj.AddComponent<Image>();
        background.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        background.raycastTarget = true;
        
        // Add text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(summaryObj.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.fontSize = 14;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.verticalAlignment = VerticalAlignmentOptions.Middle;
        
        return summaryObj;
    }
    
    #endregion
    
    #region Card Summary Animation
    
    /// <summary>
    /// Animates card summaries sliding in from the right with stagger
    /// </summary>
    private IEnumerator AnimateCardSummariesIn(List<CardSummaryItem> summaries)
    {
        if (summaries.Count == 0) yield break;
        
        Debug.Log($"DeckPreviewController: Animating {summaries.Count} card summaries IN");
        
        // Calculate positions and set starting positions offscreen
        for (int i = 0; i < summaries.Count; i++)
        {
            CardSummaryItem item = summaries[i];
            if (item?.rectTransform == null) continue;
            
            // Target position should already be set from CreateAndAnimateCardSummaries
            // Only overwrite if it's not already set (fallback safety)
            if (item.targetPosition == Vector3.zero)
            {
                item.targetPosition = item.rectTransform.localPosition;
                Debug.LogWarning($"DeckPreviewController: Target position not set for {item.cardData.CardName}, using current position as fallback");
            }
            
            // Set starting position (offscreen right)
            Vector3 startPosition = item.targetPosition + Vector3.right * GetOffscreenRightDistance(item.rectTransform);
            item.rectTransform.localPosition = startPosition;
            
            // Make sure alpha is 1 since we're using position animation, not alpha
            CanvasGroup canvasGroup = item.summaryObject.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
            
            // Ensure object is active and visible
            item.summaryObject.SetActive(true);
            item.isAnimating = true;
        }
        
        // Wait one frame for layout
        yield return null;
        
        // Animate each card with stagger
        for (int i = 0; i < summaries.Count; i++)
        {
            CardSummaryItem item = summaries[i];
            if (item?.rectTransform == null) continue;
            
            // Start animation for this card
#if DOTWEEN_ENABLED || !UNITY_EDITOR && !UNITY_STANDALONE
            item.rectTransform.DOLocalMove(item.targetPosition, cardAnimationDuration)
                .SetEase(slideInEase)
                .OnComplete(() => {
                    item.isAnimating = false;
                });
#else
            StartCoroutine(AnimateCardMoveCoroutine(item, item.targetPosition, cardAnimationDuration, true));
#endif
            
            // Random stagger delay before next card
            if (i < summaries.Count - 1)
            {
                float delay = Random.Range(staggerDelayMin, staggerDelayMax);
                yield return new WaitForSeconds(delay);
            }
        }
        
        // Wait for the last card to finish
        yield return new WaitForSeconds(cardAnimationDuration);
        
        Debug.Log("DeckPreviewController: Card summaries IN animation completed");
    }
    
    /// <summary>
    /// Animates card summaries sliding out to the right with stagger
    /// </summary>
    private IEnumerator AnimateCardSummariesOut(List<CardSummaryItem> summaries)
    {
        if (summaries.Count == 0) yield break;
        
        Debug.Log($"DeckPreviewController: Animating {summaries.Count} card summaries OUT");
        
        // Animate each card out with stagger (reverse order for visual appeal)
        for (int i = summaries.Count - 1; i >= 0; i--)
        {
            CardSummaryItem item = summaries[i];
            if (item?.rectTransform == null) continue;
            
            item.isAnimating = true;
            
            // Calculate offscreen position
            Vector3 offscreenPosition = item.rectTransform.localPosition + Vector3.right * GetOffscreenRightDistance(item.rectTransform);
            
            // Start animation for this card
#if DOTWEEN_ENABLED || !UNITY_EDITOR && !UNITY_STANDALONE
            item.rectTransform.DOLocalMove(offscreenPosition, cardAnimationDuration)
                .SetEase(slideOutEase)
                .OnComplete(() => {
                    item.isAnimating = false;
                    if (item.summaryObject != null)
                    {
                        Destroy(item.summaryObject);
                    }
                });
#else
            StartCoroutine(AnimateCardMoveCoroutine(item, offscreenPosition, cardAnimationDuration, false));
#endif
            
            // Random stagger delay before next card
            if (i > 0)
            {
                float delay = Random.Range(staggerDelayMin, staggerDelayMax);
                yield return new WaitForSeconds(delay);
            }
        }
        
        // Wait for the last card to finish
        yield return new WaitForSeconds(cardAnimationDuration);
        
        // Clear the list
        summaries.Clear();
        
        Debug.Log("DeckPreviewController: Card summaries OUT animation completed");
    }
    
#if !DOTWEEN_ENABLED && (UNITY_EDITOR || UNITY_STANDALONE)
    /// <summary>
    /// Fallback coroutine-based animation when DOTween is not available
    /// </summary>
    private IEnumerator AnimateCardMoveCoroutine(CardSummaryItem item, Vector3 targetPosition, float duration, bool isAnimatingIn)
    {
        if (item?.rectTransform == null) yield break;
        
        Vector3 startPosition = item.rectTransform.localPosition;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            if (item?.rectTransform == null) yield break;
            
            float progress = elapsedTime / duration;
            
            // Apply easing curve (simplified cubic ease)
            float easedProgress = isAnimatingIn ? EaseOutCubic(progress) : EaseInCubic(progress);
            
            Vector3 currentPosition = Vector3.Lerp(startPosition, targetPosition, easedProgress);
            item.rectTransform.localPosition = currentPosition;
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Ensure final position
        if (item?.rectTransform != null)
        {
            item.rectTransform.localPosition = targetPosition;
        }
        
        // Mark as complete
        item.isAnimating = false;
        
        // Destroy if this was an out animation
        if (!isAnimatingIn && item?.summaryObject != null)
        {
            Destroy(item.summaryObject);
        }
    }
    
    /// <summary>
    /// Simplified easing functions for fallback animations
    /// </summary>
    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
    
    private float EaseInCubic(float t)
    {
        return t * t * t;
    }
#endif
    
    /// <summary>
    /// Calculates the distance needed to move a card offscreen to the right
    /// </summary>
    private float GetOffscreenRightDistance(RectTransform cardRect)
    {
        if (cardRect == null) return offscreenRightOffset;
        
        // Get the canvas bounds
        Canvas parentCanvas = cardRect.GetComponentInParent<Canvas>();
        if (parentCanvas == null) return offscreenRightOffset;
        
        RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();
        if (canvasRect == null) return offscreenRightOffset;
        
        // Calculate distance based on canvas width + card width + buffer
        float canvasWidth = canvasRect.rect.width;
        float cardWidth = cardRect.rect.width;
        float distance = (canvasWidth * 0.5f) + cardWidth + 50f; // 50f buffer
        
        return Mathf.Max(distance, offscreenRightOffset); // Use at least the minimum offset
    }
    
    #endregion
    
    #region Hover Tooltip System
    
    /// <summary>
    /// Sets up hover detection and tooltip for a card summary item
    /// </summary>
    private void SetupCardSummaryHover(CardSummaryItem item)
    {
        if (item?.summaryObject == null) 
        {
            Debug.LogWarning("DeckPreviewController: Cannot setup hover - item or summaryObject is null");
            return;
        }
            
        // Validate that the canvas has a GraphicRaycaster for hover detection
        ValidateCanvasForHoverDetection(item.summaryObject);
        
        // Ensure there's an Image component for raycast detection
        Image backgroundImage = item.summaryObject.GetComponent<Image>();
        if (backgroundImage == null)
        {
            Debug.LogWarning($"DeckPreviewController: No Image component found on {item.summaryObject.name}, adding one for hover detection");
            backgroundImage = item.summaryObject.AddComponent<Image>();
            backgroundImage.color = new Color(1f, 1f, 1f, 0.01f); // Nearly transparent but detects raycasts
        }
        
        // Ensure raycastTarget is enabled
        if (!backgroundImage.raycastTarget)
        {
            Debug.Log($"DeckPreviewController: Enabling raycastTarget on {item.summaryObject.name} for hover detection");
            backgroundImage.raycastTarget = true;
        }
        
        // Add or get hover detector component
        CardSummaryHoverDetector hoverDetector = item.summaryObject.GetComponent<CardSummaryHoverDetector>();
        if (hoverDetector == null)
        {
            Debug.Log($"DeckPreviewController: Adding CardSummaryHoverDetector to {item.summaryObject.name}");
            hoverDetector = item.summaryObject.AddComponent<CardSummaryHoverDetector>();
        }
        
        // Set up the hover detector
        hoverDetector.Initialize(item.cardData, this);
        Debug.Log($"DeckPreviewController: Hover detection setup completed for {item.cardData.CardName}");
    }
    
    /// <summary>
    /// Validates that the canvas has required components for hover detection
    /// </summary>
    private void ValidateCanvasForHoverDetection(GameObject uiObject)
    {
        Canvas canvas = uiObject.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("DeckPreviewController: No Canvas found in parent hierarchy - hover detection will not work!");
            return;
        }
        
        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            Debug.LogError($"DeckPreviewController: Canvas '{canvas.name}' is missing GraphicRaycaster component - adding it now for hover detection");
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
        else
        {
            Debug.Log($"DeckPreviewController: Canvas '{canvas.name}' has GraphicRaycaster - hover detection should work");
        }
        
        // Also check for EventSystem
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            Debug.LogError("DeckPreviewController: No EventSystem found in scene - hover detection will not work! Please add an EventSystem to the scene.");
        }
        else
        {
            Debug.Log("DeckPreviewController: EventSystem found - hover events should be processed");
        }
    }
    
    /// <summary>
    /// Shows a full card tooltip at the mouse position
    /// </summary>
    public void ShowCardTooltip(CardData cardData, Vector3 mousePosition)
    {
        // Validate the mouse position we received
        Vector3 currentMousePosition = Input.mousePosition;
        Debug.Log($"DeckPreviewController: ShowCardTooltip called for {cardData?.CardName ?? "null card"}");
        Debug.Log($"DeckPreviewController: Received mouse position: {mousePosition}");
        Debug.Log($"DeckPreviewController: Current Input.mousePosition: {currentMousePosition}");
        
        // Use the most current mouse position to be safe
        Vector3 mousePositionToUse = currentMousePosition;
        Debug.Log($"DeckPreviewController: Using current mouse position: {mousePositionToUse}");
        
        // Hide any existing tooltip
        HideCardTooltip();
        
        if (cardData == null)
        {
            Debug.LogWarning("DeckPreviewController: Cannot show tooltip - cardData is null");
            return;
        }
            
        // Show tooltip immediately if no delay, otherwise use delay
        if (tooltipShowDelay <= 0f)
        {
            Debug.Log($"DeckPreviewController: Creating tooltip immediately for {cardData.CardName}");
            CreateAndShowTooltipImmediate(cardData, mousePositionToUse);
        }
        else
        {
            Debug.Log($"DeckPreviewController: Starting tooltip delay of {tooltipShowDelay} seconds");
            tooltipDelayCoroutine = StartCoroutine(ShowTooltipAfterDelay(cardData, mousePositionToUse));
        }
    }
    
    /// <summary>
    /// Creates and shows tooltip immediately without delay
    /// </summary>
    private void CreateAndShowTooltipImmediate(CardData cardData, Vector3 mousePosition)
    {
        // Create tooltip card
        currentTooltipCard = CreateTooltipCard(cardData);
        if (currentTooltipCard != null)
        {
            Debug.Log($"DeckPreviewController: Tooltip created immediately, positioning at {mousePosition}");
            PositionTooltipCard(currentTooltipCard, mousePosition);
            
            // Start mouse following if enabled
            if (enableMouseFollowing)
            {
                Debug.Log("DeckPreviewController: Starting mouse following for tooltip");
                mouseFollowCoroutine = StartCoroutine(FollowMouseCoroutine());
            }
        }
        else
        {
            Debug.LogError("DeckPreviewController: Failed to create tooltip card immediately");
        }
    }
    
    /// <summary>
    /// Hides the current card tooltip
    /// </summary>
    public void HideCardTooltip()
    {
        Debug.Log("DeckPreviewController: HideCardTooltip called");
        
        // Stop delay coroutine
        if (tooltipDelayCoroutine != null)
        {
            Debug.Log("DeckPreviewController: Stopping tooltip delay coroutine");
            StopCoroutine(tooltipDelayCoroutine);
            tooltipDelayCoroutine = null;
        }
        
        // Stop mouse following coroutine
        if (mouseFollowCoroutine != null)
        {
            Debug.Log("DeckPreviewController: Stopping mouse following coroutine");
            StopCoroutine(mouseFollowCoroutine);
            mouseFollowCoroutine = null;
        }
        
        // Destroy existing tooltip
        if (currentTooltipCard != null)
        {
            Debug.Log("DeckPreviewController: Destroying existing tooltip card");
            Destroy(currentTooltipCard);
            currentTooltipCard = null;
        }
    }
    
    /// <summary>
    /// Shows tooltip after a delay
    /// </summary>
    private IEnumerator ShowTooltipAfterDelay(CardData cardData, Vector3 mousePosition)
    {
        Debug.Log($"DeckPreviewController: Waiting {tooltipShowDelay} seconds before showing tooltip for {cardData.CardName}");
        yield return new WaitForSeconds(tooltipShowDelay);

        Debug.Log($"DeckPreviewController: Delay complete, creating tooltip for {cardData.CardName}");

        // Create tooltip card
        currentTooltipCard = CreateTooltipCard(cardData);
        if (currentTooltipCard != null)
        {
            Debug.Log($"DeckPreviewController: Tooltip card created successfully, waiting for layout then positioning at {mousePosition}");
            
            // Wait a frame for the card to initialize its layout
            yield return null;
            
            PositionTooltipCard(currentTooltipCard, mousePosition);
            
            // Start mouse following if enabled
            if (enableMouseFollowing)
            {
                Debug.Log("DeckPreviewController: Starting mouse following for delayed tooltip");
                mouseFollowCoroutine = StartCoroutine(FollowMouseCoroutine());
            }
        }
        else
        {
            Debug.LogError("DeckPreviewController: Failed to create tooltip card");
        }

        tooltipDelayCoroutine = null;
    }
    
    /// <summary>
    /// Creates a full card object for tooltip display
    /// </summary>
    private GameObject CreateTooltipCard(CardData cardData)
    {
        if (deckCardPrefab == null)
        {
            Debug.LogWarning("DeckPreviewController: No deckCardPrefab set for tooltip display");
            return null;
        }

        Debug.Log($"DeckPreviewController: Creating tooltip card for {cardData.CardName}");

        // Create tooltip card using the existing card prefab system
        GameObject tooltipCard = Instantiate(deckCardPrefab);
        tooltipCard.name = $"TooltipCard_{cardData.CardName}";

        // IMMEDIATELY disable components that cause initialization spam BEFORE they can run
        DisableTooltipCardComponents(tooltipCard);

        // Remove/disable network components that might interfere
        NetworkObject networkObject = tooltipCard.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            Debug.Log("DeckPreviewController: Removing NetworkObject from tooltip card");
            DestroyImmediate(networkObject);
        }
        
        FishNet.Component.Transforming.NetworkTransform networkTransform = tooltipCard.GetComponent<FishNet.Component.Transforming.NetworkTransform>();
        if (networkTransform != null)
        {
            Debug.Log("DeckPreviewController: Removing NetworkTransform from tooltip card");
            DestroyImmediate(networkTransform);
        }
        
        // Initialize the card AFTER disabling components
        Card cardComponent = tooltipCard.GetComponent<Card>();
        if (cardComponent != null)
        {
            Debug.Log($"DeckPreviewController: Initializing Card component for {cardData.CardName}");
            cardComponent.Initialize(cardData);
        }
        else
        {
            Debug.LogWarning("DeckPreviewController: No Card component found on tooltip prefab");
        }

        // Set scale for tooltip size
        tooltipCard.transform.localScale = Vector3.one * 0.8f; // Slightly smaller than normal

        Debug.Log($"DeckPreviewController: Tooltip card creation completed for {cardData.CardName}");
        return tooltipCard;
    }

    /// <summary>
    /// Immediately disables components that cause unwanted initialization
    /// </summary>
    private void DisableTooltipCardComponents(GameObject tooltipCard)
    {
        // Disable gameplay-related components
        CardDragDrop dragDrop = tooltipCard.GetComponent<CardDragDrop>();
        if (dragDrop != null)
        {
            dragDrop.enabled = false;
        }
        
        Collider2D cardCollider = tooltipCard.GetComponent<Collider2D>();
        if (cardCollider != null)
        {
            cardCollider.enabled = false;
        }

        // Disable components that trigger unnecessary initialization
        SourceAndTargetIdentifier sourceTarget = tooltipCard.GetComponent<SourceAndTargetIdentifier>();
        if (sourceTarget != null)
        {
            sourceTarget.enabled = false;
        }

        CardAnimator cardAnimator = tooltipCard.GetComponent<CardAnimator>();
        if (cardAnimator != null)
        {
            cardAnimator.enabled = false;
        }

        UIHoverDetector uiHoverDetector = tooltipCard.GetComponent<UIHoverDetector>();
        if (uiHoverDetector != null)
        {
            uiHoverDetector.enabled = false;
        }

        // Disable any CardEffectResolver
        CardEffectResolver effectResolver = tooltipCard.GetComponent<CardEffectResolver>();
        if (effectResolver != null)
        {
            effectResolver.enabled = false;
        }

        Debug.Log($"DeckPreviewController: Disabled unwanted components on {tooltipCard.name}");
    }
    
    /// <summary>
    /// Positions the tooltip card near the mouse position
    /// </summary>
    private void PositionTooltipCard(GameObject tooltipCard, Vector3 mousePosition)
    {
        if (tooltipCard == null) 
        {
            Debug.LogError("DeckPreviewController: Cannot position tooltip - tooltipCard is null");
            return;
        }

        // Get canvas for proper positioning
        Canvas targetCanvas = GetTooltipCanvas();
        if (targetCanvas == null) 
        {
            Debug.LogError("DeckPreviewController: Cannot position tooltip - no canvas found");
            return;
        }

        Debug.Log($"DeckPreviewController: Positioning tooltip on canvas {targetCanvas.name} at mouse position {mousePosition}");

        // Set parent and ensure it's on top
        tooltipCard.transform.SetParent(targetCanvas.transform, false);
        tooltipCard.transform.SetAsLastSibling(); // Ensure it renders on top

        RectTransform tooltipRect = tooltipCard.GetComponent<RectTransform>();
        if (tooltipRect != null)
        {
            // Convert screen position to canvas position
            Vector2 canvasPosition;
            bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetCanvas.GetComponent<RectTransform>(),
                mousePosition,
                targetCanvas.worldCamera,
                out canvasPosition
            );

            if (!converted)
            {
                Debug.LogWarning("DeckPreviewController: Failed to convert mouse position to canvas coordinates");
                // Fallback to center position
                canvasPosition = Vector2.zero;
            }

            Debug.Log($"DeckPreviewController: Converted mouse position {mousePosition} to canvas position {canvasPosition}");

            // Apply offset to position tooltip relative to mouse
            // Negative X moves tooltip left (mouse at right edge), positive Y moves tooltip up (mouse at bottom edge)
            Vector2 offsetPosition = canvasPosition + tooltipOffset;
            
            Debug.Log($"DeckPreviewController: Applied offset {tooltipOffset} to get position {offsetPosition}");
            
            // Set initial position
            tooltipRect.localPosition = offsetPosition;

            // Optionally ensure tooltip stays within canvas bounds
            if (enableTooltipClamping)
            {
                Debug.Log("DeckPreviewController: Clamping enabled - applying canvas bounds");
                ClampTooltipToCanvas(tooltipRect, targetCanvas.GetComponent<RectTransform>());
            }
            else
            {
                Debug.Log("DeckPreviewController: Clamping disabled - using raw offset position");
            }

            Debug.Log($"DeckPreviewController: Final tooltip position: {tooltipRect.localPosition}");

            // Make sure tooltip is visible
            CanvasGroup canvasGroup = tooltipCard.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = tooltipCard.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false; // Prevent tooltip from intercepting mouse events
            canvasGroup.blocksRaycasts = false; // Allow mouse events to pass through

            // Force canvas to update
            Canvas.ForceUpdateCanvases();
            
            Debug.Log($"DeckPreviewController: Tooltip positioned and made visible for {tooltipCard.name}");
        }
        else
        {
            Debug.LogError("DeckPreviewController: Tooltip card has no RectTransform component");
        }
    }
    
    /// <summary>
    /// Gets the appropriate canvas for tooltip display
    /// </summary>
    private Canvas GetTooltipCanvas()
    {
        // Try to use the same canvas as the deck panels
        if (deckPreviewPanel != null)
        {
            Canvas canvas = deckPreviewPanel.GetComponentInParent<Canvas>();
            if (canvas != null) 
            {
                Debug.Log($"DeckPreviewController: Using deck preview canvas: {canvas.name}");
                return canvas;
            }
        }

        // Try to find canvas through the parent transform hierarchy
        if (deckPreviewGridParent != null)
        {
            Canvas gridCanvas = deckPreviewGridParent.GetComponentInParent<Canvas>();
            if (gridCanvas != null)
            {
                Debug.Log($"DeckPreviewController: Using grid parent canvas: {gridCanvas.name}");
                return gridCanvas;
            }
        }

        // Find all canvases and use the first UI canvas (not tooltip canvases)
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in allCanvases)
        {
            // Skip canvases that are clearly tooltip cards
            if (!canvas.name.Contains("TooltipCard") && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                Debug.Log($"DeckPreviewController: Using fallback UI canvas: {canvas.name}");
                return canvas;
            }
        }

        Debug.LogError("DeckPreviewController: No suitable canvas found in scene for tooltip display");
        return null;
    }
    
    /// <summary>
    /// Clamps tooltip position to stay within canvas bounds
    /// </summary>
    private void ClampTooltipToCanvas(RectTransform tooltipRect, RectTransform canvasRect)
    {
        if (tooltipRect == null || canvasRect == null) 
        {
            Debug.LogWarning("DeckPreviewController: Cannot clamp tooltip - missing RectTransform references");
            return;
        }

        Vector3 tooltipPosition = tooltipRect.localPosition;
        Vector2 tooltipSize = tooltipRect.rect.size;
        Vector2 canvasSize = canvasRect.rect.size;

        Debug.Log($"DeckPreviewController: Clamping tooltip - Original position: {tooltipPosition}, Tooltip size: {tooltipSize}, Canvas size: {canvasSize}");

        // Calculate canvas bounds (accounting for canvas anchor)
        float canvasHalfWidth = canvasSize.x * 0.5f;
        float canvasHalfHeight = canvasSize.y * 0.5f;
        float tooltipHalfWidth = tooltipSize.x * 0.5f;
        float tooltipHalfHeight = tooltipSize.y * 0.5f;

        // Calculate clamp bounds with some padding
        float padding = 10f;
        float minX = -canvasHalfWidth + tooltipHalfWidth + padding;
        float maxX = canvasHalfWidth - tooltipHalfWidth - padding;
        float minY = -canvasHalfHeight + tooltipHalfHeight + padding;
        float maxY = canvasHalfHeight - tooltipHalfHeight - padding;

        Debug.Log($"DeckPreviewController: Clamp bounds - MinX: {minX}, MaxX: {maxX}, MinY: {minY}, MaxY: {maxY}");

        // Only clamp if the bounds are valid
        if (minX < maxX && minY < maxY)
        {
            // Clamp X position
            tooltipPosition.x = Mathf.Clamp(tooltipPosition.x, minX, maxX);
            
            // Clamp Y position
            tooltipPosition.y = Mathf.Clamp(tooltipPosition.y, minY, maxY);
            
            Debug.Log($"DeckPreviewController: Clamped tooltip position: {tooltipPosition}");
        }
        else
        {
            Debug.LogWarning($"DeckPreviewController: Invalid clamp bounds - using original position. Canvas may be too small or tooltip too large.");
            Debug.LogWarning($"DeckPreviewController: Canvas size: {canvasSize}, Tooltip size: {tooltipSize}");
            // Don't modify position if bounds are invalid
        }

        tooltipRect.localPosition = tooltipPosition;
        Debug.Log($"DeckPreviewController: Final tooltip local position set to: {tooltipRect.localPosition}");
    }
    
    /// <summary>
    /// Coroutine that continuously updates tooltip position to follow mouse
    /// </summary>
    private IEnumerator FollowMouseCoroutine()
    {
        while (currentTooltipCard != null)
        {
            Vector3 currentMousePosition = Input.mousePosition;
            PositionTooltipCard(currentTooltipCard, currentMousePosition);
            
            // Update every frame for smooth following
            yield return null;
        }
        
        Debug.Log("DeckPreviewController: Mouse following stopped - tooltip no longer exists");
    }
    
    #endregion
    
    #region Cleanup Methods
    
    private void ClearCardSummaries(List<CardSummaryItem> summaries)
    {
        foreach (CardSummaryItem item in summaries)
        {
            if (item?.summaryObject != null)
            {
                Destroy(item.summaryObject);
            }
        }
        summaries.Clear();
    }
    
    /// <summary>
    /// Clears all deck preview cards from both character and pet deck panels
    /// </summary>
    public void ClearAllDeckPreviews()
    {
        // Stop any running animations through the queue
        if (animationQueue != null)
        {
            animationQueue.StopAndClear();
        }
        
        // Clear summaries
        ClearCardSummaries(currentDeckSummaries);
        
        // Hide tooltip
        HideCardTooltip();
    }
    
    #endregion
    
    #region Public Interface
    
    public void SetCurrentCharacterIndex(int index)
    {
        currentCharacterIndex = index;
    }
    
    public void SetCurrentPetIndex(int index)
    {
        currentPetIndex = index;
    }
    
    public int GetCurrentCharacterIndex()
    {
        return currentCharacterIndex;
    }
    
    public int GetCurrentPetIndex()
    {
        return currentPetIndex;
    }
    
    public bool HasVisibleDeckPreviews()
    {
        return currentDeckSummaries.Count > 0;
    }
    
    public GameObject GetDeckPreviewPanel()
    {
        return deckPreviewPanel;
    }
    
    /// <summary>
    /// Gets information about the current animation queue state for debugging
    /// </summary>
    public string GetAnimationQueueInfo()
    {
        return animationQueue?.GetQueueInfo() ?? "Animation queue not initialized";
    }
    
    #endregion
    
    #region Cleanup
    
    private void OnDestroy()
    {
        // Clean up animation queue
        if (animationQueue != null)
        {
            animationQueue.StopAndClear();
        }
        
        // Clean up tooltip coroutines
        if (tooltipDelayCoroutine != null)
        {
            StopCoroutine(tooltipDelayCoroutine);
        }
        
        if (mouseFollowCoroutine != null)
        {
            StopCoroutine(mouseFollowCoroutine);
        }
        
        // Clean up tooltip
        HideCardTooltip();
        
        // Clean up summaries
        ClearAllDeckPreviews();
    }
    
    #endregion
    }
    
    /// <summary>
/// Component for detecting hover events on card summary items
    /// </summary>
public class CardSummaryHoverDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CardData cardData;
    private DeckPreviewController deckController;
    
    public void Initialize(CardData data, DeckPreviewController controller)
    {
        cardData = data;
        deckController = controller;
        Debug.Log($"CardSummaryHoverDetector: Initialized for card {data?.CardName ?? "null"} on GameObject {gameObject.name}");
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log($"CardSummaryHoverDetector: OnPointerEnter triggered for {cardData?.CardName ?? "null card"} on {gameObject.name}");
        Debug.Log($"CardSummaryHoverDetector: eventData.position = {eventData.position}");
        
        if (cardData != null && deckController != null)
        {
            // Use actual mouse position instead of UI element position
            Vector3 actualMousePosition = Input.mousePosition;
            Vector3 eventMousePosition = eventData.position;
            
            Debug.Log($"CardSummaryHoverDetector: COMPARISON - Input.mousePosition: {actualMousePosition} vs eventData.position: {eventMousePosition}");
            Debug.Log($"CardSummaryHoverDetector: Using actual mouse position for tooltip");
            
            deckController.ShowCardTooltip(cardData, actualMousePosition);
        }
        else
        {
            Debug.LogWarning($"CardSummaryHoverDetector: Cannot show tooltip - cardData: {(cardData != null ? "valid" : "null")}, deckController: {(deckController != null ? "valid" : "null")}");
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log($"CardSummaryHoverDetector: OnPointerExit triggered for {cardData?.CardName ?? "null card"} on {gameObject.name}");
        
        if (deckController != null)
        {
            deckController.HideCardTooltip();
        }
        else
        {
            Debug.LogWarning("CardSummaryHoverDetector: Cannot hide tooltip - deckController is null");
        }
    }
} 