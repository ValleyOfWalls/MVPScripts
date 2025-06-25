using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using FishNet.Object;

/// <summary>
/// Handles deck preview functionality for character selection screen.
/// This controller manages the display and hiding of deck preview panels and their content.
/// Attach to: Same GameObject as CharacterSelectionUIManager
/// </summary>
public class DeckPreviewController : MonoBehaviour
{
    [Header("Deck Preview Panel References")]
    [SerializeField] private GameObject characterDeckPanel;
    [SerializeField] private ScrollRect characterDeckScrollView;
    [SerializeField] private Transform characterDeckGridParent;
    [SerializeField] private TextMeshProUGUI characterDeckTitle;
    
    [SerializeField] private GameObject petDeckPanel;
    [SerializeField] private ScrollRect petDeckScrollView;
    [SerializeField] private Transform petDeckGridParent;
    [SerializeField] private TextMeshProUGUI petDeckTitle;
    
    [Header("Deck Preview Settings")]
    [SerializeField] private GameObject deckCardPrefab;
    
    // Dependencies
    private CharacterSelectionUIAnimator uiAnimator;
    
    // Current deck content
    private List<GameObject> characterDeckItems = new List<GameObject>();
    private List<GameObject> petDeckItems = new List<GameObject>();
    
    // Available data
    private List<CharacterData> availableCharacters;
    private List<PetData> availablePets;
    
    // Current selections
    private int currentCharacterIndex = -1;
    private int currentPetIndex = -1;
    
    // State
    private bool isPlayerReady = false;
    
    #region Initialization
    
    public void Initialize(CharacterSelectionUIAnimator animator, List<CharacterData> characters, List<PetData> pets)
    {
        uiAnimator = animator;
        availableCharacters = characters;
        availablePets = pets;
        
        Debug.Log("DeckPreviewController: Initialized with UI animator and data");
    }
    
    #endregion
    
    #region Deck Preview Management
    
    /// <summary>
    /// Shows the character deck preview for the specified character index
    /// </summary>
    public void ShowCharacterDeck(int characterIndex, bool isReady = false)
    {
        /* Debug.Log($"DeckPreviewController: ShowCharacterDeck({characterIndex}) called - isReady: {isReady}"); */
        
        if (isReady)
        {
            Debug.Log("DeckPreviewController: Player is ready, NOT showing character deck");
            return;
        }
        
        currentCharacterIndex = characterIndex;
        
        // Check if this is a selection change that needs animation
        bool isSelectionChange = currentCharacterIndex != characterIndex && currentCharacterIndex >= 0;
        
        if (isSelectionChange)
        {
            Debug.Log("DeckPreviewController: Character selection changed - refreshing deck with animation");
            RefreshCharacterDeckWithAnimation();
        }
        else
        {
            /* Debug.Log("DeckPreviewController: Showing character deck normally"); */
            ShowCharacterDeckInternal();
        }
    }
    
    /// <summary>
    /// Shows the pet deck preview for the specified pet index
    /// </summary>
    public void ShowPetDeck(int petIndex, bool isReady = false)
    {
        /* Debug.Log($"DeckPreviewController: ShowPetDeck({petIndex}) called - isReady: {isReady}"); */
        
        if (isReady)
        {
            Debug.Log("DeckPreviewController: Player is ready, NOT showing pet deck");
            return;
        }
        
        currentPetIndex = petIndex;
        
        // Check if this is a selection change that needs animation
        bool isSelectionChange = currentPetIndex != petIndex && currentPetIndex >= 0;
        
        if (isSelectionChange)
        {
            Debug.Log("DeckPreviewController: Pet selection changed - refreshing deck with animation");
            RefreshPetDeckWithAnimation();
        }
        else
        {
            /* Debug.Log("DeckPreviewController: Showing pet deck normally"); */
            ShowPetDeckInternal();
        }
    }
    
    /// <summary>
    /// Hides all deck preview panels
    /// </summary>
    public void HideAllDeckPreviews()
    {
        /* Debug.Log("DeckPreviewController: HideAllDeckPreviews() called"); */
        
        ClearAllDeckPreviews();
        
        if (uiAnimator != null)
        {
            uiAnimator.HideAllDeckPanels();
        }
    }
    
    /// <summary>
    /// Updates the ready state and manages deck visibility accordingly
    /// </summary>
    public void SetPlayerReadyState(bool ready)
    {
        isPlayerReady = ready;
        
        if (ready)
        {
            Debug.Log("DeckPreviewController: Player became ready - hiding all deck panels");
            HideAllDeckPreviews();
        }
        else
        {
            Debug.Log("DeckPreviewController: Player became not ready - showing individual deck previews if selections exist");
            ShowIndividualDeckPreviews();
        }
    }
    
    /// <summary>
    /// Shows individual deck previews based on current selections (if player is not ready)
    /// </summary>
    public void ShowIndividualDeckPreviews()
    {
        if (isPlayerReady)
        {
            Debug.Log("DeckPreviewController: Player is ready - not showing individual deck previews");
            return;
        }
        
        // Show both decks if both are selected (allow simultaneous preview)
        if (currentCharacterIndex >= 0)
        {
            Debug.Log($"DeckPreviewController: Showing character deck for index {currentCharacterIndex}");
            ShowCharacterDeckInternal();
        }
        
        if (currentPetIndex >= 0)
        {
            Debug.Log($"DeckPreviewController: Showing pet deck for index {currentPetIndex}");
            ShowPetDeckInternal();
        }
    }
    
    #endregion
    
    #region Internal Deck Display Methods
    
    private void ShowCharacterDeckInternal()
    {
        /* Debug.Log($"DeckPreviewController: ShowCharacterDeckInternal() called for character index {currentCharacterIndex}"); */
        
        // Clear existing character deck preview
        ClearCharacterDeckPreview();
        
        if (currentCharacterIndex < 0 || currentCharacterIndex >= availableCharacters.Count)
        {
            Debug.Log($"DeckPreviewController: Invalid character index {currentCharacterIndex}, cannot show deck");
            return;
        }
            
        CharacterData character = availableCharacters[currentCharacterIndex];
        DeckData deckToShow = character.StarterDeck;
        string deckTitle = $"Character Deck: {character.CharacterName}";
        
        // Update character deck title
        if (characterDeckTitle != null)
        {
            characterDeckTitle.text = deckTitle;
            /* Debug.Log($"DeckPreviewController: Set character deck title to: {deckTitle}"); */
        }
        
        // Create character deck preview items
        if (deckToShow != null && deckToShow.CardsInDeck != null)
        {
            /* Debug.Log($"DeckPreviewController: Creating {deckToShow.CardsInDeck.Count} character card previews"); */
            foreach (CardData card in deckToShow.CardsInDeck)
            {
                if (card != null)
                {
                    GameObject previewItem = CreateDeckPreviewItem(card, characterDeckGridParent);
                    if (previewItem != null)
                    {
                        characterDeckItems.Add(previewItem);
                        /* Debug.Log($"DeckPreviewController: Created character card preview for {card.CardName}"); */
                    }
                }
            }
            /* Debug.Log($"DeckPreviewController: Total character deck items created: {characterDeckItems.Count}"); */
        }
        
        // Show the character deck panel with animation
        if (uiAnimator != null)
        {
            /* Debug.Log("DeckPreviewController: Calling uiAnimator.ShowCharacterDeckPanel()"); */
            uiAnimator.ShowCharacterDeckPanel();
        }
        else
        {
            Debug.LogError("DeckPreviewController: uiAnimator is null, cannot show character deck panel");
        }
    }
    
    private void ShowPetDeckInternal()
    {
        /* Debug.Log($"DeckPreviewController: ShowPetDeckInternal() called for pet index {currentPetIndex}"); */
        
        // Clear existing pet deck preview
        ClearPetDeckPreview();
        
        if (currentPetIndex < 0 || currentPetIndex >= availablePets.Count)
        {
            Debug.Log($"DeckPreviewController: Invalid pet index {currentPetIndex}, cannot show deck");
            return;
        }
            
        PetData pet = availablePets[currentPetIndex];
        DeckData deckToShow = pet.StarterDeck;
        string deckTitle = $"Pet Deck: {pet.PetName}";
        
        // Update pet deck title
        if (petDeckTitle != null)
        {
            petDeckTitle.text = deckTitle;
            /* Debug.Log($"DeckPreviewController: Set pet deck title to: {deckTitle}"); */
        }
        
        // Create pet deck preview items
        if (deckToShow != null && deckToShow.CardsInDeck != null)
        {
            /* Debug.Log($"DeckPreviewController: Creating {deckToShow.CardsInDeck.Count} pet card previews"); */
            foreach (CardData card in deckToShow.CardsInDeck)
            {
                if (card != null)
                {
                    GameObject previewItem = CreateDeckPreviewItem(card, petDeckGridParent);
                    if (previewItem != null)
                    {
                        petDeckItems.Add(previewItem);
                        /* Debug.Log($"DeckPreviewController: Created pet card preview for {card.CardName}"); */
                    }
                }
            }
            /* Debug.Log($"DeckPreviewController: Total pet deck items created: {petDeckItems.Count}"); */
        }
        
        // Show the pet deck panel with animation
        if (uiAnimator != null)
        {
            /* Debug.Log("DeckPreviewController: Calling uiAnimator.ShowPetDeckPanel()"); */
            uiAnimator.ShowPetDeckPanel();
        }
        else
        {
            Debug.LogError("DeckPreviewController: uiAnimator is null, cannot show pet deck panel");
        }
    }
    
    #endregion
    
    #region Deck Refresh Animation
    
    private void RefreshCharacterDeckWithAnimation()
    {
        if (uiAnimator == null) 
        {
            Debug.LogWarning("DeckPreviewController: No UI animator available for deck refresh animation");
            ShowCharacterDeckInternal();
            return;
        }
        
        // Hide the current character deck panel
        uiAnimator.HideCharacterDeckPanel();
        
        // Wait a brief moment for hide animation, then update content and show
        StartCoroutine(RefreshCharacterDeckCoroutine());
    }
    
    private void RefreshPetDeckWithAnimation()
    {
        if (uiAnimator == null) 
        {
            Debug.LogWarning("DeckPreviewController: No UI animator available for deck refresh animation");
            ShowPetDeckInternal();
            return;
        }
        
        // Hide the current pet deck panel
        uiAnimator.HidePetDeckPanel();
        
        // Wait a brief moment for hide animation, then update content and show
        StartCoroutine(RefreshPetDeckCoroutine());
    }
    
    private IEnumerator RefreshCharacterDeckCoroutine()
    {
        // Wait for hide animation to complete (adjust timing as needed)
        yield return new WaitForSeconds(0.3f);
        
        // Update the deck content while hidden
        PopulateCharacterDeckContent();
        
        // Show the deck panel with new content
        if (uiAnimator != null)
        {
            uiAnimator.ShowCharacterDeckPanel();
        }
    }
    
    private IEnumerator RefreshPetDeckCoroutine()
    {
        // Wait for hide animation to complete (adjust timing as needed)
        yield return new WaitForSeconds(0.3f);
        
        // Update the deck content while hidden
        PopulatePetDeckContent();
        
        // Show the deck panel with new content
        if (uiAnimator != null)
        {
            uiAnimator.ShowPetDeckPanel();
        }
    }
    
    private void PopulateCharacterDeckContent()
    {
        /* Debug.Log($"DeckPreviewController: PopulateCharacterDeckContent() called for character index {currentCharacterIndex}"); */
        
        // Clear existing character deck preview
        ClearCharacterDeckPreview();
        
        if (currentCharacterIndex < 0 || currentCharacterIndex >= availableCharacters.Count)
        {
            Debug.Log($"DeckPreviewController: Invalid character index {currentCharacterIndex}, cannot populate deck");
            return;
        }
            
        CharacterData character = availableCharacters[currentCharacterIndex];
        DeckData deckToShow = character.StarterDeck;
        string deckTitle = $"Character Deck: {character.CharacterName}";
        
        // Update character deck title
        if (characterDeckTitle != null)
        {
            characterDeckTitle.text = deckTitle;
            /* Debug.Log($"DeckPreviewController: Set character deck title to: {deckTitle}"); */
        }
        
        // Create character deck preview items
        if (deckToShow != null && deckToShow.CardsInDeck != null)
        {
            /* Debug.Log($"DeckPreviewController: Creating {deckToShow.CardsInDeck.Count} character card previews"); */
            foreach (CardData card in deckToShow.CardsInDeck)
            {
                if (card != null)
                {
                    GameObject previewItem = CreateDeckPreviewItem(card, characterDeckGridParent);
                    if (previewItem != null)
                    {
                        characterDeckItems.Add(previewItem);
                        /* Debug.Log($"DeckPreviewController: Created character card preview for {card.CardName}"); */
                    }
                }
            }
            /* Debug.Log($"DeckPreviewController: Total character deck items created: {characterDeckItems.Count}"); */
        }
    }
    
    private void PopulatePetDeckContent()
    {
        /* Debug.Log($"DeckPreviewController: PopulatePetDeckContent() called for pet index {currentPetIndex}"); */
        
        // Clear existing pet deck preview
        ClearPetDeckPreview();
        
        if (currentPetIndex < 0 || currentPetIndex >= availablePets.Count)
        {
            Debug.Log($"DeckPreviewController: Invalid pet index {currentPetIndex}, cannot populate deck");
            return;
        }
            
        PetData pet = availablePets[currentPetIndex];
        DeckData deckToShow = pet.StarterDeck;
        string deckTitle = $"Pet Deck: {pet.PetName}";
        
        // Update pet deck title
        if (petDeckTitle != null)
        {
            petDeckTitle.text = deckTitle;
            /* Debug.Log($"DeckPreviewController: Set pet deck title to: {deckTitle}"); */
        }
        
        // Create pet deck preview items
        if (deckToShow != null && deckToShow.CardsInDeck != null)
        {
            /* Debug.Log($"DeckPreviewController: Creating {deckToShow.CardsInDeck.Count} pet card previews"); */
            foreach (CardData card in deckToShow.CardsInDeck)
            {
                if (card != null)
                {
                    GameObject previewItem = CreateDeckPreviewItem(card, petDeckGridParent);
                    if (previewItem != null)
                    {
                        petDeckItems.Add(previewItem);
                        /* Debug.Log($"DeckPreviewController: Created pet card preview for {card.CardName}"); */
                    }
                }
            }
            /* Debug.Log($"DeckPreviewController: Total pet deck items created: {petDeckItems.Count}"); */
        }
    }
    
    #endregion
    
    #region Deck Item Creation
    
    private GameObject CreateDeckPreviewItem(CardData cardData, Transform parentTransform)
    {
        // Create basic preview item if no prefab
        if (deckCardPrefab == null)
        {
            return CreateBasicDeckPreviewItem(cardData, parentTransform);
        }
        
        GameObject item = Instantiate(deckCardPrefab, parentTransform);
        
        // IMPORTANT: Remove NetworkObject to prevent network/visibility management interference
        NetworkObject networkObject = item.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            DestroyImmediate(networkObject);
            Debug.Log($"DeckPreviewController: Removed NetworkObject from preview card {cardData.CardName}");
        }
        
        // IMPORTANT: Also remove NetworkTransform to prevent NetworkTransform errors
        FishNet.Component.Transforming.NetworkTransform networkTransform = item.GetComponent<FishNet.Component.Transforming.NetworkTransform>();
        if (networkTransform != null)
        {
            DestroyImmediate(networkTransform);
            Debug.Log($"DeckPreviewController: Removed NetworkTransform from preview card {cardData.CardName}");
        }
        
        // Initialize the Card component with the CardData
        Card cardComponent = item.GetComponent<Card>();
        if (cardComponent != null)
        {
            cardComponent.Initialize(cardData);
            /* Debug.Log($"DeckPreviewController: Initialized card preview for {cardData.CardName}"); */
        }
        else
        {
            Debug.LogWarning($"DeckPreviewController: deckCardPrefab missing Card component for {cardData.CardName}");
            
            // Fallback: Try to set name text manually if no Card component
            TextMeshProUGUI nameText = item.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = cardData.CardName;
            }
        }
        
        // Make sure the card is active and visible for deck preview
        item.SetActive(true);
        
        // Force override any visibility management
        StartCoroutine(ForceCardVisibilityNextFrame(item, cardData.CardName));
        
        // For deck preview, we might want to disable interaction components to prevent dragging/clicking
        CardDragDrop dragDrop = item.GetComponent<CardDragDrop>();
        if (dragDrop != null)
        {
            dragDrop.enabled = false;
            /* Debug.Log($"  Disabled CardDragDrop component"); */
        }
        
        // Disable any colliders to prevent unwanted interactions
        Collider2D cardCollider = item.GetComponent<Collider2D>();
        if (cardCollider != null)
        {
            cardCollider.enabled = false;
            /* Debug.Log($"  Disabled Collider2D component"); */
        }
        
        /* Debug.Log($"DeckPreviewController: Created preview card {cardData.CardName}, active: {item.activeInHierarchy}"); */
        
        return item;
    }
    
    /// <summary>
    /// Coroutine to force card visibility on the next frame, after any visibility management has run
    /// </summary>
    private System.Collections.IEnumerator ForceCardVisibilityNextFrame(GameObject card, string cardName)
    {
        yield return null; // Wait one frame
        
        if (card != null && !card.activeInHierarchy)
        {
            Debug.LogWarning($"DeckPreviewController: Card {cardName} was disabled by another system, forcing it back on");
            card.SetActive(true);
        }
        
        // Check again after a short delay
        yield return new WaitForSeconds(0.1f);
        
        if (card != null && !card.activeInHierarchy)
        {
            Debug.LogWarning($"DeckPreviewController: Card {cardName} disabled again, forcing it back on (second attempt)");
            card.SetActive(true);
        }
    }

    private GameObject CreateBasicDeckPreviewItem(CardData cardData, Transform parentTransform)
    {
        GameObject item = new GameObject(cardData.CardName + "_Preview");
        item.transform.SetParent(parentTransform, false);
        
        RectTransform itemRect = item.AddComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(100f, 140f);
        
        Image itemImage = item.AddComponent<Image>();
        itemImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        
        // Create card name text
        GameObject nameGO = new GameObject("CardName");
        nameGO.transform.SetParent(item.transform, false);
        
        RectTransform nameRect = nameGO.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0);
        nameRect.anchorMax = new Vector2(1, 0.3f);
        nameRect.sizeDelta = Vector2.zero;
        nameRect.anchoredPosition = Vector2.zero;
        
        TextMeshProUGUI nameText = nameGO.AddComponent<TextMeshProUGUI>();
        nameText.text = cardData.CardName;
        nameText.fontSize = 10;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.overflowMode = TextOverflowModes.Ellipsis;
        
        return item;
    }
    
    #endregion
    
    #region Cleanup Methods
    
    private void ClearCharacterDeckPreview()
    {
        foreach (GameObject item in characterDeckItems)
        {
            if (item != null) Destroy(item);
        }
        characterDeckItems.Clear();
    }
    
    private void ClearPetDeckPreview()
    {
        foreach (GameObject item in petDeckItems)
        {
            if (item != null) Destroy(item);
        }
        petDeckItems.Clear();
    }
    
    /// <summary>
    /// Clears all deck preview cards from both character and pet deck panels
    /// </summary>
    public void ClearAllDeckPreviews()
    {
        ClearCharacterDeckPreview();
        ClearPetDeckPreview();
    }
    
    #endregion
    
    #region Public Interface
    
    /// <summary>
    /// Updates the current character selection index
    /// </summary>
    public void SetCurrentCharacterIndex(int index)
    {
        currentCharacterIndex = index;
    }
    
    /// <summary>
    /// Updates the current pet selection index
    /// </summary>
    public void SetCurrentPetIndex(int index)
    {
        currentPetIndex = index;
    }
    
    /// <summary>
    /// Gets the current character selection index
    /// </summary>
    public int GetCurrentCharacterIndex()
    {
        return currentCharacterIndex;
    }
    
    /// <summary>
    /// Gets the current pet selection index
    /// </summary>
    public int GetCurrentPetIndex()
    {
        return currentPetIndex;
    }
    
    /// <summary>
    /// Checks if any deck previews are currently visible
    /// </summary>
    public bool HasVisibleDeckPreviews()
    {
        return (uiAnimator != null && uiAnimator.IsAnyDeckVisible);
    }
    
    /// <summary>
    /// Gets the character deck panel GameObject for external initialization
    /// </summary>
    public GameObject GetCharacterDeckPanel()
    {
        return characterDeckPanel;
    }
    
    /// <summary>
    /// Gets the pet deck panel GameObject for external initialization
    /// </summary>
    public GameObject GetPetDeckPanel()
    {
        return petDeckPanel;
    }
    
    #endregion
} 