using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles card selection from various sources including card shops and draft packs.
/// Attach to: Individual card prefabs in shops and draft packs to enable selection.
/// </summary>
public class CardSelectionManager : MonoBehaviour
{
    [Header("Card References")]
    [SerializeField] private Card card;
    [SerializeField] private Button cardButton;
    
    [Header("Selection Mode")]
    [SerializeField] private bool isDraftSelection = false; // If false, it's a shop purchase
    
    private NetworkEntity localPlayer;
    private DeckManager deckManager;
    private CardShopManager cardShopManager;
    private DraftManager draftManager;
    private DraftPack draftPack;

    private void Awake()
    {
        // Ensure we have a card reference
        if (card == null)
        {
            card = GetComponent<Card>();
            if (card == null)
            {
                Debug.LogError("CardSelectionManager requires a Card component on this GameObject or assigned in the inspector.");
            }
        }
        
        // Set up button listener
        if (cardButton == null)
        {
            cardButton = GetComponent<Button>();
            if (cardButton == null)
            {
                Debug.LogWarning("CardSelectionManager: No Button component found. Adding one.");
                cardButton = gameObject.AddComponent<Button>();
            }
        }
        
        cardButton.onClick.AddListener(OnCardSelected);
    }

    private void Start()
    {
        // Find local player
        NetworkEntity[] players = Object.FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        foreach (NetworkEntity player in players)
        {
            if (player.IsOwner)
            {
                localPlayer = player;
                break;
            }
        }
        
        if (localPlayer == null)
        {
            Debug.LogError("CardSelectionManager: Could not find local player!");
            return;
        }
        
        // Get deck manager from local player
        deckManager = localPlayer.GetComponent<DeckManager>();
        if (deckManager == null)
        {
            Debug.LogError("CardSelectionManager: DeckManager component not found on local player!");
            return;
        }
        
        // Find managers based on selection mode
        if (isDraftSelection)
        {
            // Find the draft manager
            draftManager = FindFirstObjectByType<DraftManager>();
            if (draftManager == null)
            {
                Debug.LogError("CardSelectionManager: DraftManager not found in the scene!");
                return;
            }
            
            // Find the draft pack this card belongs to
            draftPack = GetComponentInParent<DraftPack>();
            if (draftPack == null)
            {
                Debug.LogError("CardSelectionManager: No parent DraftPack found for this card!");
                return;
            }
        }
        else
        {
            // Find the card shop manager
            cardShopManager = FindFirstObjectByType<CardShopManager>();
            if (cardShopManager == null)
            {
                Debug.LogError("CardSelectionManager: CardShopManager not found in the scene!");
                return;
            }
        }
    }

    private void OnDestroy()
    {
        if (cardButton != null)
        {
            cardButton.onClick.RemoveListener(OnCardSelected);
        }
    }

    /// <summary>
    /// Called when the card is selected
    /// </summary>
    private void OnCardSelected()
    {
        if (isDraftSelection)
        {
            HandleDraftSelection();
        }
        else
        {
            HandleShopPurchase();
        }
    }
    
    /// <summary>
    /// Handles selection from a draft pack
    /// </summary>
    private void HandleDraftSelection()
    {
        if (draftPack == null || draftManager == null || deckManager == null || card == null || !card.IsDraftable)
        {
            Debug.LogError("CardSelectionManager: Cannot process draft selection - missing components!");
            return;
        }
        
        // Add the card to the player's deck through the draft manager
        draftManager.CmdSelectCardFromPack(localPlayer.ObjectId, draftPack.ObjectId, card.CardId);
        
        Debug.Log($"Player drafted card: {card.CardName} (ID: {card.CardId})");
    }
    
    /// <summary>
    /// Handles purchase from the card shop
    /// </summary>
    private void HandleShopPurchase()
    {
        if (cardShopManager == null || deckManager == null || card == null || !card.IsPurchasable)
        {
            Debug.LogError("CardSelectionManager: Cannot process shop purchase - missing components!");
            return;
        }
        
        // Attempt to purchase the card
        cardShopManager.AttemptPurchaseCard(card);
        
        Debug.Log($"Player attempted to purchase card: {card.CardName} (ID: {card.CardId}, Cost: {card.PurchaseCost})");
    }
    
    /// <summary>
    /// Sets whether this card is in draft mode or shop mode
    /// </summary>
    public void SetSelectionMode(bool isDraft)
    {
        isDraftSelection = isDraft;
    }
} 