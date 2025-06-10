using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using FishNet.Object;

/// <summary>
/// Manages the character selection UI interactions with Mario Kart-style shared selection grids.
/// </summary>
public class CharacterSelectionUIManager : MonoBehaviour
{
    [Header("UI References - Set by CharacterSelectionCanvasSetup")]
    [SerializeField] private GameObject characterSelectionCanvas;
    [SerializeField] private GameObject backgroundPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TMP_InputField playerNameInputField;
    [SerializeField] private Button readyButton;
    [SerializeField] private ScrollRect characterScrollView;
    [SerializeField] private Transform characterGridParent;
    [SerializeField] private ScrollRect petScrollView;
    [SerializeField] private Transform petGridParent;
    [SerializeField] private GameObject deckPreviewPanel;
    [SerializeField] private ScrollRect deckPreviewScrollView;
    [SerializeField] private Transform deckPreviewGridParent;
    [SerializeField] private TextMeshProUGUI deckPreviewTitle;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI readyCounterText;
    
    [Header("Selection Item Prefab")]
    [SerializeField] private GameObject selectionItemPrefab;
    [SerializeField] private GameObject deckCardPrefab;
    
    [Header("Styling")]
    [SerializeField] private Color selectedColor = new Color(0.3f, 0.6f, 1f, 1f);
    [SerializeField] private Color unselectedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color readyColor = Color.green;
    [SerializeField] private Color notReadyColor = Color.gray;
    
    // Player color system
    private static readonly Color[] PLAYER_COLORS = new Color[]
    {
        new Color(1f, 0.2f, 0.2f, 1f),     // Red
        new Color(0.2f, 0.5f, 1f, 1f),     // Blue  
        new Color(0.2f, 1f, 0.2f, 1f),     // Green
        new Color(1f, 1f, 0.2f, 1f),       // Yellow
        new Color(1f, 0.5f, 0.2f, 1f),     // Orange
        new Color(0.8f, 0.2f, 1f, 1f),     // Purple
        new Color(0.2f, 1f, 1f, 1f),       // Cyan
        new Color(1f, 0.2f, 0.8f, 1f)      // Pink
    };
    
    // Manager reference
    private CharacterSelectionManager selectionManager;
    
    // Current selections
    private int selectedCharacterIndex = -1;
    private int selectedPetIndex = -1;
    private string customPlayerName = "";
    private bool isShowingCharacterDeck = false; // Track which deck is being shown
    
    // Available options
    private List<CharacterData> availableCharacters;
    private List<PetData> availablePets;
    
    // UI element lists
    private List<GameObject> characterItems = new List<GameObject>();
    private List<GameObject> petItems = new List<GameObject>();
    private List<GameObject> deckPreviewItems = new List<GameObject>();
    
    // State
    private bool isReady = false;
    private bool hasValidSelection = false;
    
    // Player data for Mario Kart-style display
    private Dictionary<string, PlayerSelectionDisplayInfo> otherPlayersData = new Dictionary<string, PlayerSelectionDisplayInfo>();
    private Color myPlayerColor = Color.white;
    private string myPlayerID = "";

    #region Initialization

    public void Initialize(CharacterSelectionManager manager, List<CharacterData> characters, List<PetData> pets)
    {
        selectionManager = manager;
        availableCharacters = characters;
        availablePets = pets;
        
        // Assign player color and ID
        myPlayerID = GetPlayerID();
        myPlayerColor = GetPlayerColor(myPlayerID);
        
        SetupUI();
        CreateSelectionItems();
        MakeDefaultSelections();
        UpdateReadyButtonState();
        
        Debug.Log($"CharacterSelectionUIManager: Initialized - Player ID: {myPlayerID}, Color: {myPlayerColor}");
    }
    
    private string GetPlayerID()
    {
        // Use connection ID or steam ID as unique identifier
        return FishNet.InstanceFinder.ClientManager?.Connection?.ClientId.ToString() ?? System.Guid.NewGuid().ToString();
    }
    
    private Color GetPlayerColor(string playerID)
    {
        // Assign color based on player ID hash for consistency
        int colorIndex = Mathf.Abs(playerID.GetHashCode()) % PLAYER_COLORS.Length;
        return PLAYER_COLORS[colorIndex];
    }
    
    private void SetupUI()
    {
        // Enable the canvas
        if (characterSelectionCanvas != null)
        {
            characterSelectionCanvas.SetActive(true);
        }
        
        // Set up input field
        if (playerNameInputField != null)
        {
            playerNameInputField.onValueChanged.AddListener(OnPlayerNameChanged);
        }
        
        // Set up ready button
        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyButtonClicked);
            readyButton.interactable = false;
        }
        
        // Initialize status text
        if (statusText != null)
        {
            statusText.text = "Select a character and pet to continue...";
        }
        
        // Initialize ready counter
        UpdateReadyCounter(0, 1); // Start with 0/1 until we know player count
    }

    private void CreateSelectionItems()
    {
        CreateCharacterItems();
        CreatePetItems();
    }

    private void CreateCharacterItems()
    {
        // Clear existing items
        foreach (GameObject item in characterItems)
        {
            if (item != null) Destroy(item);
        }
        characterItems.Clear();
        
        // Create character selection items
        for (int i = 0; i < availableCharacters.Count; i++)
        {
            CharacterData character = availableCharacters[i];
            if (character == null) continue;
            
            GameObject item = CreateSelectionItem(characterGridParent, character.CharacterName, character.CharacterPortrait, character.CharacterDescription, true, i);
            if (item != null)
            {
                int index = i; // Capture for closure
                Button button = item.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => OnCharacterSelected(index));
                }
                characterItems.Add(item);
            }
        }
    }

    private void CreatePetItems()
    {
        // Clear existing items
        foreach (GameObject item in petItems)
        {
            if (item != null) Destroy(item);
        }
        petItems.Clear();
        
        // Create pet selection items
        for (int i = 0; i < availablePets.Count; i++)
        {
            PetData pet = availablePets[i];
            if (pet == null) continue;
            
            GameObject item = CreateSelectionItem(petGridParent, pet.PetName, pet.PetPortrait, pet.PetDescription, false, i);
            if (item != null)
            {
                int index = i; // Capture for closure
                Button button = item.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => OnPetSelected(index));
                }
                petItems.Add(item);
            }
        }
    }

    private GameObject CreateSelectionItem(Transform parent, string itemName, Sprite portrait, string description, bool isCharacter, int index)
    {
        // Since prefab is provided, we should always use it
        if (selectionItemPrefab == null)
        {
            Debug.LogError("CharacterSelectionUIManager: Selection Item Prefab is not assigned!");
            return null;
        }
        
        GameObject item = Instantiate(selectionItemPrefab, parent);
        
        // Set up the item data using the prefab structure
        // Find the name text component
        TextMeshProUGUI nameText = item.GetComponentInChildren<TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text = itemName;
        }
        
        // Find the portrait image component specifically (not the root image)
        // Look for an Image component that's likely the portrait (not the background)
        Image[] images = item.GetComponentsInChildren<Image>();
        Image portraitImage = null;
        
        // Try to find portrait by name or component order
        foreach (Image img in images)
        {
            // Look for portrait-specific naming or the second image (assuming first is background)
            if (img.gameObject.name.ToLower().Contains("portrait") || 
                img.gameObject.name.ToLower().Contains("image") ||
                img != item.GetComponent<Image>()) // Not the root image
            {
                portraitImage = img;
                break;
            }
        }
        
        // If no specific portrait found, use the second image component (skip root)
        if (portraitImage == null && images.Length > 1)
        {
            portraitImage = images[1]; // Assume index 0 is root background
        }
        
        // Set the portrait sprite
        if (portraitImage != null && portrait != null)
        {
            portraitImage.sprite = portrait;
        }
        else if (portrait != null)
        {
            Debug.LogWarning($"CharacterSelectionUIManager: Could not find portrait Image component in prefab for {itemName}");
        }
        
        return item;
    }

    private void MakeDefaultSelections()
    {
        // Auto-select first character and first pet if available
        if (availableCharacters.Count > 0)
        {
            OnCharacterSelected(0);
            Debug.Log("CharacterSelectionUIManager: Auto-selected first character for default selection");
        }
        
        if (availablePets.Count > 0)
        {
            OnPetSelected(0);
            Debug.Log("CharacterSelectionUIManager: Auto-selected first pet for default selection");
        }
        
        // Check if auto-test runner wants us to auto-ready
        AutoTestRunner autoTestRunner = FindFirstObjectByType<AutoTestRunner>();
        if (autoTestRunner != null && autoTestRunner.enableAutoTesting)
        {
            // Auto-ready after a short delay to ensure UI is set up
            StartCoroutine(AutoReadyAfterDelay());
        }
    }
    
    private System.Collections.IEnumerator AutoReadyAfterDelay()
    {
        yield return new WaitForSeconds(0.5f); // Allow UI to update
        
        if (hasValidSelection)
        {
            Debug.Log("CharacterSelectionUIManager: Auto-ready triggered by AutoTestRunner");
            OnReadyButtonClicked();
        }
    }

    #endregion

    #region Selection Handling

    private void OnCharacterSelected(int characterIndex)
    {
        if (characterIndex < 0 || characterIndex >= availableCharacters.Count) return;
        
        // Update selection
        selectedCharacterIndex = characterIndex;
        isShowingCharacterDeck = true;
        
        // Update visual selection
        UpdateMySelectionVisuals();
        
        // Show character deck preview
        ShowIndividualDeckPreview();
        
        // Update ready state
        UpdateSelectionState();
        
        Debug.Log($"CharacterSelectionUIManager: Selected character {availableCharacters[characterIndex].CharacterName}");
    }

    private void OnPetSelected(int petIndex)
    {
        if (petIndex < 0 || petIndex >= availablePets.Count) return;
        
        // Update selection
        selectedPetIndex = petIndex;
        isShowingCharacterDeck = false;
        
        // Update visual selection
        UpdateMySelectionVisuals();
        
        // Show pet deck preview
        ShowIndividualDeckPreview();
        
        // Update ready state
        UpdateSelectionState();
        
        Debug.Log($"CharacterSelectionUIManager: Selected pet {availablePets[petIndex].PetName}");
    }

    private void UpdateMySelectionVisuals()
    {
        // Update character selection border
        for (int i = 0; i < characterItems.Count; i++)
        {
            if (characterItems[i] != null)
            {
                PlayerSelectionIndicator indicator = characterItems[i].GetComponentInChildren<PlayerSelectionIndicator>();
                if (indicator != null)
                {
                    if (i == selectedCharacterIndex)
                    {
                        indicator.AddPlayerSelection(myPlayerID, myPlayerColor);
                    }
                    else
                    {
                        indicator.RemovePlayerSelection(myPlayerID);
                    }
                }
            }
        }
        
        // Update pet selection border
        for (int i = 0; i < petItems.Count; i++)
        {
            if (petItems[i] != null)
            {
                PlayerSelectionIndicator indicator = petItems[i].GetComponentInChildren<PlayerSelectionIndicator>();
                if (indicator != null)
                {
                    if (i == selectedPetIndex)
                    {
                        indicator.AddPlayerSelection(myPlayerID, myPlayerColor);
                    }
                    else
                    {
                        indicator.RemovePlayerSelection(myPlayerID);
                    }
                }
            }
        }
    }

    private void UpdateSelectionState()
    {
        hasValidSelection = selectedCharacterIndex >= 0 && selectedPetIndex >= 0;
        
        // Update status text
        if (statusText != null)
        {
            if (hasValidSelection)
            {
                string charName = !string.IsNullOrEmpty(customPlayerName) ? customPlayerName : availableCharacters[selectedCharacterIndex].CharacterName;
                string petName = availablePets[selectedPetIndex].PetName;
                statusText.text = $"Selected: {charName} & {petName}";
            }
            else if (selectedCharacterIndex >= 0)
            {
                statusText.text = "Now select a pet...";
            }
            else if (selectedPetIndex >= 0)
            {
                statusText.text = "Now select a character...";
            }
            else
            {
                statusText.text = "Select a character and pet to continue...";
            }
        }
        
        // Update ready button
        UpdateReadyButtonState();
        
        // Send selection to server if valid
        if (hasValidSelection)
        {
            if (selectionManager != null)
            {
                selectionManager.RequestSelectionUpdate(selectedCharacterIndex, selectedPetIndex, customPlayerName, "");
            }
        }
    }

    #endregion

    #region Deck Preview

    private void ShowIndividualDeckPreview()
    {
        // Clear existing preview
        ClearDeckPreview();
        
        if (isReady)
        {
            // Hide deck preview when ready
            if (deckPreviewPanel != null)
                deckPreviewPanel.SetActive(false);
            return;
        }
        
        // Show deck preview panel and ensure parents are active
        if (deckPreviewPanel != null)
        {
            deckPreviewPanel.SetActive(true);
            Debug.Log($"CharacterSelectionUIManager: Activated deck preview panel");
        }
        else
        {
            Debug.LogWarning("CharacterSelectionUIManager: deckPreviewPanel is null!");
        }
        
        // Ensure scroll view is active
        if (deckPreviewScrollView != null)
        {
            deckPreviewScrollView.gameObject.SetActive(true);
            Debug.Log($"CharacterSelectionUIManager: Activated deck preview scroll view");
            
            // Check scroll view properties
            Debug.Log($"CharacterSelectionUIManager: ScrollView enabled: {deckPreviewScrollView.enabled}");
            Debug.Log($"CharacterSelectionUIManager: ScrollView viewport: {deckPreviewScrollView.viewport?.name ?? "null"}");
            Debug.Log($"CharacterSelectionUIManager: ScrollView content: {deckPreviewScrollView.content?.name ?? "null"}");
        }
        else
        {
            Debug.LogWarning("CharacterSelectionUIManager: deckPreviewScrollView is null!");
        }
        
        // Ensure grid parent is active
        if (deckPreviewGridParent != null)
        {
            deckPreviewGridParent.gameObject.SetActive(true);
            Debug.Log($"CharacterSelectionUIManager: Activated deck preview grid parent");
        }
        
        DeckData deckToShow = null;
        string deckTitle = "";
        
        if (isShowingCharacterDeck && selectedCharacterIndex >= 0)
        {
            // Show character deck
            deckToShow = availableCharacters[selectedCharacterIndex].StarterDeck;
            deckTitle = $"Character Deck: {availableCharacters[selectedCharacterIndex].CharacterName}";
        }
        else if (!isShowingCharacterDeck && selectedPetIndex >= 0)
        {
            // Show pet deck
            deckToShow = availablePets[selectedPetIndex].StarterDeck;
            deckTitle = $"Pet Deck: {availablePets[selectedPetIndex].PetName}";
        }
        
        // Update preview title
        if (deckPreviewTitle != null)
        {
            deckPreviewTitle.text = deckTitle;
            Debug.Log($"CharacterSelectionUIManager: Set deck title to: {deckTitle}");
        }
        
        // Create preview items
        if (deckToShow != null && deckToShow.CardsInDeck != null)
        {
            Debug.Log($"CharacterSelectionUIManager: Creating {deckToShow.CardsInDeck.Count} card previews");
            foreach (CardData card in deckToShow.CardsInDeck)
            {
                if (card != null)
                {
                    GameObject previewItem = CreateDeckPreviewItem(card);
                    if (previewItem != null)
                    {
                        deckPreviewItems.Add(previewItem);
                        Debug.Log($"CharacterSelectionUIManager: Created card preview for {card.CardName}, active: {previewItem.activeInHierarchy}");
                    }
                }
            }
            Debug.Log($"CharacterSelectionUIManager: Total deck preview items created: {deckPreviewItems.Count}");
        }
        else
        {
            Debug.LogWarning($"CharacterSelectionUIManager: No deck data to show - deckToShow: {deckToShow != null}, cards: {deckToShow?.CardsInDeck?.Count ?? 0}");
        }
    }

    private void ClearDeckPreview()
    {
        foreach (GameObject item in deckPreviewItems)
        {
            if (item != null) Destroy(item);
        }
        deckPreviewItems.Clear();
    }

    private GameObject CreateDeckPreviewItem(CardData cardData)
    {
        // Create basic preview item if no prefab
        if (deckCardPrefab == null)
        {
            return CreateBasicDeckPreviewItem(cardData);
        }
        
        GameObject item = Instantiate(deckCardPrefab, deckPreviewGridParent);
        
        // IMPORTANT: Remove NetworkObject to prevent network/visibility management interference
        NetworkObject networkObject = item.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            DestroyImmediate(networkObject);
            Debug.Log($"CharacterSelectionUIManager: Removed NetworkObject from preview card {cardData.CardName}");
        }
        
        // Initialize the Card component with the CardData
        Card cardComponent = item.GetComponent<Card>();
        if (cardComponent != null)
        {
            cardComponent.Initialize(cardData);
            Debug.Log($"CharacterSelectionUIManager: Initialized card preview for {cardData.CardName}");
        }
        else
        {
            Debug.LogWarning($"CharacterSelectionUIManager: deckCardPrefab missing Card component for {cardData.CardName}");
            
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
            Debug.Log($"  Disabled CardDragDrop component");
        }
        
        // Disable any colliders to prevent unwanted interactions
        Collider2D cardCollider = item.GetComponent<Collider2D>();
        if (cardCollider != null)
        {
            cardCollider.enabled = false;
            Debug.Log($"  Disabled Collider2D component");
        }
        
        Debug.Log($"CharacterSelectionUIManager: Created preview card {cardData.CardName}, active: {item.activeInHierarchy}");
        
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
            Debug.LogWarning($"CharacterSelectionUIManager: Card {cardName} was disabled by another system, forcing it back on");
            card.SetActive(true);
        }
        
        // Check again after a short delay
        yield return new WaitForSeconds(0.1f);
        
        if (card != null && !card.activeInHierarchy)
        {
            Debug.LogWarning($"CharacterSelectionUIManager: Card {cardName} disabled again, forcing it back on (second attempt)");
            card.SetActive(true);
        }
    }

    private GameObject CreateBasicDeckPreviewItem(CardData cardData)
    {
        GameObject item = new GameObject(cardData.CardName + "_Preview");
        item.transform.SetParent(deckPreviewGridParent, false);
        
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

    #region UI Event Handlers

    private void OnPlayerNameChanged(string newName)
    {
        customPlayerName = newName;
        UpdateSelectionState();
    }

    private void OnReadyButtonClicked()
    {
        if (!hasValidSelection) return;
        
        isReady = !isReady;
        
        // Hide/show deck preview based on ready state
        if (isReady)
        {
            ClearDeckPreview();
            if (deckPreviewPanel != null)
                deckPreviewPanel.SetActive(false);
        }
        else
        {
            ShowIndividualDeckPreview();
        }
        
        if (selectionManager != null)
        {
            selectionManager.RequestReadyToggle();
        }
    }

    private void UpdateReadyButtonState()
    {
        if (readyButton != null)
        {
            readyButton.interactable = hasValidSelection;
            
            Image buttonImage = readyButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = isReady ? readyColor : (hasValidSelection ? selectedColor : notReadyColor);
            }
            
            TextMeshProUGUI buttonText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = isReady ? "Not Ready" : "Ready";
            }
        }
    }

    #endregion

    #region Mario Kart Style Updates

    public void UpdateOtherPlayersSelections(List<PlayerSelectionInfo> playerInfos)
    {
        // Update ready counter
        int readyCount = playerInfos.Count(p => p.isReady);
        int totalCount = playerInfos.Count;
        UpdateReadyCounter(readyCount, totalCount);
        
        // Clear previous player data
        ClearAllPlayerSelections();
        
        // Update visual indicators for each player
        foreach (PlayerSelectionInfo info in playerInfos)
        {
            if (info.playerName == myPlayerID) continue; // Skip self
            
            Color playerColor = GetPlayerColor(info.playerName);
            
            // Show character selection
            if (info.hasSelection && info.characterIndex >= 0 && info.characterIndex < characterItems.Count)
            {
                PlayerSelectionIndicator indicator = characterItems[info.characterIndex].GetComponentInChildren<PlayerSelectionIndicator>();
                if (indicator != null)
                {
                    indicator.AddPlayerSelection(info.playerName, playerColor);
                }
            }
            
            // Show pet selection
            if (info.hasSelection && info.petIndex >= 0 && info.petIndex < petItems.Count)
            {
                PlayerSelectionIndicator indicator = petItems[info.petIndex].GetComponentInChildren<PlayerSelectionIndicator>();
                if (indicator != null)
                {
                    indicator.AddPlayerSelection(info.playerName, playerColor);
                }
            }
        }
    }

    private void ClearAllPlayerSelections()
    {
        // Clear character selections
        foreach (GameObject item in characterItems)
        {
            if (item != null)
            {
                PlayerSelectionIndicator indicator = item.GetComponentInChildren<PlayerSelectionIndicator>();
                if (indicator != null)
                {
                    indicator.ClearAllExcept(myPlayerID);
                }
            }
        }
        
        // Clear pet selections
        foreach (GameObject item in petItems)
        {
            if (item != null)
            {
                PlayerSelectionIndicator indicator = item.GetComponentInChildren<PlayerSelectionIndicator>();
                if (indicator != null)
                {
                    indicator.ClearAllExcept(myPlayerID);
                }
            }
        }
    }

    private void UpdateReadyCounter(int readyCount, int totalCount)
    {
        if (readyCounterText != null)
        {
            readyCounterText.text = $"{readyCount}/{totalCount} Ready";
            readyCounterText.color = (readyCount == totalCount) ? readyColor : Color.white;
        }
    }

    #endregion

    #region External Interface

    public void ShowTransitionMessage(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        
        // Disable interactions during transition
        if (readyButton != null)
        {
            readyButton.interactable = false;
        }
    }

    public void HideCharacterSelectionUI()
    {
        if (characterSelectionCanvas != null)
        {
            characterSelectionCanvas.SetActive(false);
        }
    }

    #endregion
}

/// <summary>
/// Data structure for player selection display info
/// </summary>
[System.Serializable]
public class PlayerSelectionDisplayInfo
{
    public string playerName;
    public int characterIndex;
    public int petIndex;
    public bool hasSelection;
    public bool isReady;
    public Color playerColor;
} 