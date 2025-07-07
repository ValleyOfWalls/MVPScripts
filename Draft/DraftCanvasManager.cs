using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Object;
using MVPScripts.Utility;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Manages the UI elements for the draft phase, including pack display, shop, and card selection.
/// Attach to: The DraftCanvas GameObject that contains all draft UI elements.
/// </summary>
public class DraftCanvasManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject draftCanvas;
    [SerializeField] private Transform draftPackContainer;
    [SerializeField] private TextMeshProUGUI draftStatusText;
    [SerializeField] private Button continueButton;
    [SerializeField] private TextMeshProUGUI packInfoText;
    
    [Header("Shop UI")]
    [SerializeField] private Transform shopContainer;
    [SerializeField] private TextMeshProUGUI currencyText;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private TextMeshProUGUI shopTitleText;
    
    [Header("Card Selection UI")]
    [SerializeField] private Transform cardSelectionArea;
    [SerializeField] private Button selectForPlayerButton;
    [SerializeField] private Button selectForPetButton;
    [SerializeField] private TextMeshProUGUI selectionPromptText;
    
    private NetworkEntity localPlayer;
    private DraftManager draftManager;
    private GameObject currentSelectedCard;
    private bool isShopCard = false; // Track if selected card is from shop
    
    public Transform DraftPackContainer => draftPackContainer;
    public Transform CardSelectionArea => cardSelectionArea;
    public Transform ShopContainer => shopContainer;
    
    private void Awake()
    {
        if (draftCanvas == null) draftCanvas = gameObject;
        
        // Initially hide selection buttons
        if (selectForPlayerButton != null) selectForPlayerButton.gameObject.SetActive(false);
        if (selectForPetButton != null) selectForPetButton.gameObject.SetActive(false);
        
        // Setup button listeners
        SetupButtonListeners();
    }
    
    private void SetupButtonListeners()
    {
        if (selectForPlayerButton != null)
        {
            selectForPlayerButton.onClick.AddListener(() => SelectCardForEntity(EntityType.Player));
        }
        
        if (selectForPetButton != null)
        {
            selectForPetButton.onClick.AddListener(() => SelectCardForEntity(EntityType.Pet));
        }
        
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueButtonPressed);
            continueButton.gameObject.SetActive(false); // Initially hidden
        }
    }
    
    public void Initialize(DraftManager manager, NetworkEntity player)
    {
        draftManager = manager;
        localPlayer = player;
        
        // Setup currency display for the local player
        SetupCurrencyDisplay();
        
        // Setup shop UI
        SetupShopUI();
        
        Debug.Log($"DraftCanvasManager: Initialized for player {localPlayer.EntityName.Value}");
    }
    
    private void SetupCurrencyDisplay()
    {
        if (localPlayer != null && localPlayer.EntityType == EntityType.Player)
        {
            // Subscribe to currency changes
            localPlayer.OnCurrencyChanged += UpdateCurrencyDisplay;
            
            // Update initial display
            UpdateCurrencyDisplay(localPlayer.Currency.Value);
        }
    }
    
    private void SetupShopUI()
    {
        // Initialize shop panel state
        if (shopPanel != null)
        {
            shopPanel.SetActive(true); // Shop is visible during draft
        }
        
        if (shopTitleText != null)
        {
            shopTitleText.text = "Card Shop";
        }
    }
    
    private void UpdateCurrencyDisplay(int newCurrency)
    {
        if (currencyText != null)
        {
            currencyText.text = $"Gold: {newCurrency}";
        }
    }
    
    public void EnableDraftCanvas()
    {
        if (draftCanvas != null)
        {
            draftCanvas.SetActive(true);
            Debug.Log("DraftCanvasManager: Draft canvas enabled");
        }
    }
    
    public void DisableDraftCanvas()
    {
        if (draftCanvas != null)
        {
            draftCanvas.SetActive(false);
            Debug.Log("DraftCanvasManager: Draft canvas disabled");
        }
        
        // Clean up any active UI state
        CleanupDraftState();
    }
    
    /// <summary>
    /// Cleans up draft state when transitioning away from draft
    /// </summary>
    public void CleanupDraftState()
    {
        // Hide card selection UI
        HideCardSelectionUI();
        
        // Hide continue button
        HideContinueButton();
        
        // Clear status text
        if (draftStatusText != null)
        {
            draftStatusText.text = "";
        }
        
        // Clear pack info text
        if (packInfoText != null)
        {
            packInfoText.text = "";
        }
        
        // Hide shop panel
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
        
        // Unsubscribe from currency changes
        if (localPlayer != null)
        {
            localPlayer.OnCurrencyChanged -= UpdateCurrencyDisplay;
        }
        
        /* Debug.Log("DraftCanvasManager: Draft state cleaned up"); */
    }
    
    public void UpdateDraftStatus(string status)
    {
        if (draftStatusText != null)
        {
            draftStatusText.text = status;
        }
    }
    
    public void UpdatePackInfo(int currentPack, int totalPacks, int cardsRemaining)
    {
        if (packInfoText != null)
        {
            packInfoText.text = $"Pack {currentPack}/{totalPacks} - {cardsRemaining} cards remaining";
        }
    }
    
    /// <summary>
    /// Shows card selection UI for draft cards
    /// </summary>
    public void ShowCardSelectionUI(GameObject selectedCard)
    {
        ShowCardSelectionUI(selectedCard, false);
    }
    
    /// <summary>
    /// Shows card selection UI for either draft or shop cards
    /// </summary>
    public void ShowCardSelectionUI(GameObject selectedCard, bool isFromShop)
    {
        // For shop cards, verify the card is still available before showing UI
        if (isFromShop)
        {
            if (!IsShopCardStillAvailable(selectedCard))
            {
                Debug.Log($"DraftCanvasManager: Shop card {selectedCard.name} is no longer available, not showing selection UI");
                return;
            }
        }
        
        currentSelectedCard = selectedCard;
        isShopCard = isFromShop;
        
        if (selectForPlayerButton != null) selectForPlayerButton.gameObject.SetActive(true);
        if (selectForPetButton != null) selectForPetButton.gameObject.SetActive(true);
        
        if (selectionPromptText != null)
        {
            Card cardComponent = selectedCard.GetComponent<Card>();
            string cardName = cardComponent?.CardData?.CardName ?? "Unknown Card";
            
            if (isFromShop)
            {
                int cost = cardComponent?.PurchaseCost ?? 0;
                selectionPromptText.text = $"Purchase '{cardName}' for {cost} gold?\nAdd to which deck?";
            }
            else
            {
                selectionPromptText.text = $"Add '{cardName}' to which deck?";
            }
        }
        
        /* Debug.Log($"DraftCanvasManager: Showing card selection UI for {selectedCard.name} (from shop: {isFromShop})"); */
    }
    
    /// <summary>
    /// Checks if a shop card is still available for purchase
    /// </summary>
    private bool IsShopCardStillAvailable(GameObject cardObject)
    {
        if (cardObject == null) return false;
        
        // Check if the card still exists and is active
        if (!cardObject.activeInHierarchy) return false;
        
        // Check if the card is still marked as purchasable
        Card cardComponent = cardObject.GetComponent<Card>();
        if (cardComponent == null || !cardComponent.IsPurchasable) return false;
        
        // Find the shop and verify the card is still in it
        ShopPack shop = ComponentResolver.FindComponentGlobally<ShopPack>();
        if (shop == null || !shop.ContainsCard(cardObject))
        {
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Hides the selection UI if it's currently showing a specific card
    /// Call this when a card becomes unavailable
    /// </summary>
    public void HideSelectionUIForCard(GameObject unavailableCard)
    {
        if (currentSelectedCard == unavailableCard)
        {
            Debug.Log($"DraftCanvasManager: Hiding selection UI because card {unavailableCard.name} is no longer available");
            HideCardSelectionUI();
        }
    }
    
    /// <summary>
    /// Validates that the currently selected card is still available
    /// Called before processing a selection to prevent race conditions
    /// </summary>
    private bool ValidateCurrentSelection()
    {
        if (currentSelectedCard == null) return false;
        
        if (isShopCard)
        {
            return IsShopCardStillAvailable(currentSelectedCard);
        }
        
        // For draft cards, check if the card still exists and is draftable
        if (!currentSelectedCard.activeInHierarchy) return false;
        
        Card cardComponent = currentSelectedCard.GetComponent<Card>();
        return cardComponent != null && cardComponent.IsDraftable;
    }
    
    public void HideCardSelectionUI()
    {
        if (selectForPlayerButton != null) selectForPlayerButton.gameObject.SetActive(false);
        if (selectForPetButton != null) selectForPetButton.gameObject.SetActive(false);
        
        if (selectionPromptText != null)
        {
            selectionPromptText.text = "";
        }
        
        currentSelectedCard = null;
        isShopCard = false;
        Debug.Log("DraftCanvasManager: Hidden card selection UI");
    }
    
    private void SelectCardForEntity(EntityType entityType)
    {
        if (currentSelectedCard == null || draftManager == null || localPlayer == null)
        {
            Debug.LogError("DraftCanvasManager: Cannot select card - missing references");
            return;
        }
        
        // Validate that the selected card is still available
        if (!ValidateCurrentSelection())
        {
            Debug.LogWarning($"DraftCanvasManager: Selected card {currentSelectedCard.name} is no longer available");
            HideCardSelectionUI();
            return;
        }
        
        Card cardComponent = currentSelectedCard.GetComponent<Card>();
        if (cardComponent == null || cardComponent.CardData == null)
        {
            Debug.LogError("DraftCanvasManager: Selected card has no Card component or CardData");
            return;
        }
        
        /* Debug.Log($"DraftCanvasManager: Selecting card {cardComponent.CardData.CardName} for {entityType} (from shop: {isShopCard})"); */
        
        // Call the appropriate method based on whether this is a shop card or draft card
        if (isShopCard)
        {
            draftManager.PurchaseCard(currentSelectedCard, entityType);
        }
        else
        {
            draftManager.SelectCard(currentSelectedCard, entityType);
        }
        
        // Hide the selection UI
        HideCardSelectionUI();
    }
    
    public void ShowContinueButton()
    {
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(true);
            continueButton.interactable = true;
        }
    }
    
    public void HideContinueButton()
    {
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(false);
        }
    }
    
    private void OnContinueButtonPressed()
    {
        if (draftManager != null && localPlayer != null)
        {
            Debug.Log("DraftCanvasManager: Continue button pressed");
            draftManager.OnContinueButtonPressed(localPlayer.Owner);
        }
    }
    
    public void ShowDraftCompleteMessage()
    {
        UpdateDraftStatus("Draft Complete! Waiting for other players...");
        ShowContinueButton();
    }
    
    /// <summary>
    /// Triggers refresh of transform mirroring for all draft packs
    /// Call this if the DraftPackContainer transform properties change
    /// </summary>
    public void RefreshDraftPackTransforms()
    {
        DraftPackSetup draftPackSetup = ComponentResolver.FindComponentGlobally<DraftPackSetup>();
        if (draftPackSetup != null)
        {
            draftPackSetup.RefreshTransformMirroring();
            Debug.Log("DraftCanvasManager: Triggered refresh of draft pack transforms");
        }
        else
        {
            Debug.LogWarning("DraftCanvasManager: Could not find DraftPackSetup to refresh transforms");
        }
    }
    
    /// <summary>
    /// Triggers refresh of transform mirroring for the shop
    /// Call this if the ShopContainer transform properties change
    /// </summary>
    public void RefreshShopTransforms()
    {
        ShopSetup shopSetup = ComponentResolver.FindComponentGlobally<ShopSetup>();
        if (shopSetup != null)
        {
            shopSetup.RefreshTransformMirroring();
            Debug.Log("DraftCanvasManager: Triggered refresh of shop transforms");
        }
        else
        {
            Debug.LogWarning("DraftCanvasManager: Could not find ShopSetup to refresh transforms");
        }
    }
    
    private void OnDestroy()
    {
        if (selectForPlayerButton != null) selectForPlayerButton.onClick.RemoveAllListeners();
        if (selectForPetButton != null) selectForPetButton.onClick.RemoveAllListeners();
        if (continueButton != null) continueButton.onClick.RemoveAllListeners();
        
        // Unsubscribe from currency changes
        if (localPlayer != null)
        {
            localPlayer.OnCurrencyChanged -= UpdateCurrencyDisplay;
        }
    }
} 