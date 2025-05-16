using UnityEngine;
using UnityEngine.UI; // For Image
using TMPro; // For TextMeshProUGUI
using FishNet.Object;
using FishNet.Object.Synchronizing;

public enum CardEffectType
{
    Damage,
    Heal,
    DrawCard,
    BuffStats,
    DebuffStats,
    ApplyStatus
    // Add more as needed
}

/// <summary>
/// Represents a card in the game, handling visual display and interaction.
/// Attach to: Card prefabs that will be instantiated for visual representation in the UI.
/// </summary>
public class Card : NetworkBehaviour
{
    [Header("Card Data")]
    private readonly SyncVar<int> _cardId = new SyncVar<int>();
    public int CardId => _cardId.Value;
    
    [Header("Card Properties")]
    private readonly SyncVar<bool> _isPurchasable = new SyncVar<bool>();
    public bool IsPurchasable => _isPurchasable.Value;
    
    private readonly SyncVar<bool> _isDraftable = new SyncVar<bool>();
    public bool IsDraftable => _isDraftable.Value;

    private readonly SyncVar<int> _purchaseCost = new SyncVar<int>();
    public int PurchaseCost => _purchaseCost.Value;
    
    [Header("Card State")]
    private readonly SyncVar<CardLocation> _currentContainer = new SyncVar<CardLocation>();
    public CardLocation CurrentContainer => _currentContainer.Value;
    
    [Header("UI References (Assign in Prefab)")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image artworkImage;
    [SerializeField] private TextMeshProUGUI energyCostText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Image cardImage; // Will handle both card artwork and background color

    // Local reference to card data (not synced over network)
    [SerializeField]
    protected CardData cardData;

    // Public quick accessors (optional, can just use cardData.PropertyName)
    public string CardName => cardData != null ? cardData.CardName : "No Data";
    public CardData CardData => cardData;

    /// <summary>
    /// Initializes the card with the provided card data
    /// </summary>
    /// <param name="data">The CardData ScriptableObject containing the card's information</param>
    public void Initialize(CardData data)
    {
        if (data == null)
        {
            Debug.LogError($"Card {gameObject.name}: Attempted to initialize with null CardData!");
            return;
        }

        cardData = data;
        _cardId.Value = data.CardId;

        // Update UI elements if they exist
        if (nameText != null) nameText.text = data.CardName;
        if (descriptionText != null) descriptionText.text = data.Description;
        if (energyCostText != null) energyCostText.text = data.EnergyCost.ToString();
        if (artworkImage != null && data.CardArtwork != null) artworkImage.sprite = data.CardArtwork;

        // Hide cost text by default (only shown in shop)
        if (costText != null) costText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Sets whether the card is purchasable and its cost
    /// </summary>
    public void SetPurchasable(bool purchasable, int cost = 0)
    {
        _isPurchasable.Value = purchasable;
        _purchaseCost.Value = cost;

        // Show/hide and update cost text if it exists
        if (costText != null)
        {
            costText.gameObject.SetActive(purchasable);
            if (purchasable) costText.text = cost.ToString();
        }
    }

    /// <summary>
    /// Sets whether the card is draftable
    /// </summary>
    public void SetDraftable(bool draftable)
    {
        _isDraftable.Value = draftable;
    }

    /// <summary>
    /// Sets the current container location of the card
    /// </summary>
    public void SetCurrentContainer(CardLocation location)
    {
        _currentContainer.Value = location;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // If we have a card ID but no card data, try to load it from the database
        if (_cardId.Value != 0 && cardData == null && CardDatabase.Instance != null)
        {
            cardData = CardDatabase.Instance.GetCardById(_cardId.Value);
            if (cardData != null)
            {
                Initialize(cardData);
            }
        }
    }
}