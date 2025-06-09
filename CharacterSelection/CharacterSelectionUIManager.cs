using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

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
        // Create basic item structure if no prefab
        if (selectionItemPrefab == null)
        {
            return CreateBasicSelectionItem(parent, itemName, portrait, description, isCharacter, index);
        }
        
        GameObject item = Instantiate(selectionItemPrefab, parent);
        
        // Set up the item data (assuming the prefab has the right structure)
        TextMeshProUGUI nameText = item.GetComponentInChildren<TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text = itemName;
        }
        
        Image portraitImage = item.GetComponentInChildren<Image>();
        if (portraitImage != null && portrait != null)
        {
            portraitImage.sprite = portrait;
        }
        
        // Add player indicators container
        AddPlayerIndicatorsToItem(item);
        
        return item;
    }

    private GameObject CreateBasicSelectionItem(Transform parent, string itemName, Sprite portrait, string description, bool isCharacter, int index)
    {
        // Create basic UI structure programmatically
        GameObject item = new GameObject(itemName + "_Item");
        item.transform.SetParent(parent, false);
        
        RectTransform itemRect = item.AddComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(200f, 250f);
        
        Image itemImage = item.AddComponent<Image>();
        itemImage.color = unselectedColor;
        
        Button itemButton = item.AddComponent<Button>();
        itemButton.targetGraphic = itemImage;
        
        // Create vertical layout
        VerticalLayoutGroup layout = item.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        
        // Create portrait
        if (portrait != null)
        {
            GameObject portraitGO = new GameObject("Portrait");
            portraitGO.transform.SetParent(item.transform, false);
            
            RectTransform portraitRect = portraitGO.AddComponent<RectTransform>();
            portraitRect.sizeDelta = new Vector2(120f, 120f);
            
            Image portraitImage = portraitGO.AddComponent<Image>();
            portraitImage.sprite = portrait;
            portraitImage.preserveAspect = true;
        }
        
        // Create name text
        GameObject nameGO = new GameObject("Name");
        nameGO.transform.SetParent(item.transform, false);
        
        RectTransform nameRect = nameGO.AddComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(0, 30f);
        
        TextMeshProUGUI nameText = nameGO.AddComponent<TextMeshProUGUI>();
        nameText.text = itemName;
        nameText.fontSize = 16;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.fontStyle = FontStyles.Bold;
        
        // Add player indicators container
        AddPlayerIndicatorsToItem(item);
        
        return item;
    }

    private void AddPlayerIndicatorsToItem(GameObject item)
    {
        // Create container for player selection indicators
        GameObject indicatorsContainer = new GameObject("PlayerIndicators");
        indicatorsContainer.transform.SetParent(item.transform, false);
        
        RectTransform indicatorsRect = indicatorsContainer.AddComponent<RectTransform>();
        indicatorsRect.anchorMin = new Vector2(0, 0);
        indicatorsRect.anchorMax = new Vector2(1, 1);
        indicatorsRect.sizeDelta = Vector2.zero;
        indicatorsRect.anchoredPosition = Vector2.zero;
        
        // Add the PlayerSelectionIndicator component
        PlayerSelectionIndicator indicator = indicatorsContainer.AddComponent<PlayerSelectionIndicator>();
        indicator.Initialize();
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
        
        // Show deck preview panel
        if (deckPreviewPanel != null)
            deckPreviewPanel.SetActive(true);
        
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
        }
        
        // Create preview items
        if (deckToShow != null && deckToShow.CardsInDeck != null)
        {
            foreach (CardData card in deckToShow.CardsInDeck)
            {
                if (card != null)
                {
                    GameObject previewItem = CreateDeckPreviewItem(card);
                    if (previewItem != null)
                    {
                        deckPreviewItems.Add(previewItem);
                    }
                }
            }
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
        
        // Set up the item data (would need to be adjusted based on actual prefab)
        TextMeshProUGUI nameText = item.GetComponentInChildren<TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text = cardData.CardName;
        }
        
        return item;
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