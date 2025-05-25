using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Object;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Manages the UI elements for the draft phase, including pack display and card selection.
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
    
    [Header("Card Selection UI")]
    [SerializeField] private Transform cardSelectionArea;
    [SerializeField] private Button selectForPlayerButton;
    [SerializeField] private Button selectForPetButton;
    [SerializeField] private TextMeshProUGUI selectionPromptText;
    
    private NetworkEntity localPlayer;
    private DraftManager draftManager;
    private GameObject currentSelectedCard;
    
    public Transform DraftPackContainer => draftPackContainer;
    public Transform CardSelectionArea => cardSelectionArea;
    
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
        
        Debug.Log($"DraftCanvasManager: Initialized for player {localPlayer.EntityName.Value}");
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
    
    public void ShowCardSelectionUI(GameObject selectedCard)
    {
        currentSelectedCard = selectedCard;
        
        if (selectForPlayerButton != null) selectForPlayerButton.gameObject.SetActive(true);
        if (selectForPetButton != null) selectForPetButton.gameObject.SetActive(true);
        
        if (selectionPromptText != null)
        {
            Card cardComponent = selectedCard.GetComponent<Card>();
            string cardName = cardComponent?.CardData?.CardName ?? "Unknown Card";
            selectionPromptText.text = $"Add '{cardName}' to which deck?";
        }
        
        Debug.Log($"DraftCanvasManager: Showing card selection UI for {selectedCard.name}");
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
        Debug.Log("DraftCanvasManager: Hidden card selection UI");
    }
    
    private void SelectCardForEntity(EntityType entityType)
    {
        if (currentSelectedCard == null || draftManager == null || localPlayer == null)
        {
            Debug.LogError("DraftCanvasManager: Cannot select card - missing references");
            return;
        }
        
        Card cardComponent = currentSelectedCard.GetComponent<Card>();
        if (cardComponent == null || cardComponent.CardData == null)
        {
            Debug.LogError("DraftCanvasManager: Selected card has no Card component or CardData");
            return;
        }
        
        Debug.Log($"DraftCanvasManager: Selecting card {cardComponent.CardData.CardName} for {entityType}");
        
        // Call the draft manager to handle the selection
        draftManager.SelectCard(currentSelectedCard, entityType);
        
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
        DraftPackSetup draftPackSetup = FindFirstObjectByType<DraftPackSetup>();
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
    
    private void OnDestroy()
    {
        if (selectForPlayerButton != null) selectForPlayerButton.onClick.RemoveAllListeners();
        if (selectForPetButton != null) selectForPetButton.onClick.RemoveAllListeners();
        if (continueButton != null) continueButton.onClick.RemoveAllListeners();
    }
} 