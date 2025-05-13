using UnityEngine;
using UnityEngine.UI; // For Image
using TMPro; // For TextMeshProUGUI

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
public class Card : MonoBehaviour // Changed from NetworkBehaviour
{
    // If this Card script is on a prefab that is NOT a NetworkObject itself, 
    // but is instantiated by a NetworkObject (like NetworkPlayer/Pet who owns the card instance),
    // then this script doesn't need to be a NetworkBehaviour.
    // Let's assume card *definitions* are not NetworkObjects, but card *instances in hand/play* might be.
    // For server authoritative, the *state* of which cards are where is synced, not necessarily each card itself as a NB.
    // Given the request, making it a MonoBehaviour is safer for now. If they need to be spawned independently as networked entities, then NetworkBehaviour.
    // Let's go with MonoBehaviour and assume card instances are managed by their owners (Player/Pet).
    // If card instances need to be transferred or have their own networked state, then it should be a NetworkBehaviour.

    [Header("Card Data")]
    [SerializeField] private int cardId;
    public int CardId { get => cardId; }
    
    [Header("Card Properties")]
    [SerializeField] private bool isPurchasable = false; // Can be bought in card shop
    [SerializeField] private bool isDraftable = true;    // Can be picked in draft packs
    [SerializeField] private int purchaseCost = 50;      // Cost to purchase if isPurchasable is true
    
    [Header("Card State")]
    [SerializeField] private CardLocation currentContainer = CardLocation.Deck;
    
    [Header("UI References (Assign in Prefab)")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image artworkImage;
    [SerializeField] private TextMeshProUGUI energyCostText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Image cardImage;
    [SerializeField] private Image backgroundImage;

    public CardData cardData { get; private set; }

    // Public quick accessors (optional, can just use cardData.PropertyName)
    public string CardName => cardData != null ? cardData.CardName : "No Data";
    public int EnergyCost => cardData != null ? cardData.EnergyCost : 0;
    public CardEffectType EffectType => cardData != null ? cardData.EffectType : default(CardEffectType);
    public int Amount => cardData != null ? cardData.Amount : 0;
    public bool IsPurchasable => isPurchasable;
    public bool IsDraftable => isDraftable;
    public int PurchaseCost => purchaseCost;
    
    // Container tracking
    public CardLocation CurrentContainer => currentContainer;
    
    // Event for container changes
    public delegate void ContainerChanged(CardLocation oldContainer, CardLocation newContainer);
    public event ContainerChanged OnContainerChanged;

    public void Initialize(CardData data)
    {
        if (data == null)
        {
            Debug.LogError("Card.Initialize: CardData is null");
            return;
        }
        
        this.cardData = data;
        this.cardId = data.CardId;
        
        if (nameText != null) nameText.text = data.CardName;
        if (descriptionText != null) descriptionText.text = data.Description;
        if (artworkImage != null) artworkImage.sprite = data.CardArtwork; // Make sure sprite is assigned in CardData SO
        if (energyCostText != null) energyCostText.text = data.EnergyCost.ToString();
        if (costText != null) costText.text = data.EnergyCost.ToString();
        
        this.gameObject.name = $"Card_{data.CardName}_{data.CardId}";
        
        // Update card appearance based on effect type
        UpdateCardAppearance(data.EffectType);
        
        // Update card image if provided
        if (cardImage != null && data.CardArtwork != null)
        {
            cardImage.sprite = data.CardArtwork;
            cardImage.enabled = true;
        }
        else if (cardImage != null)
        {
            cardImage.enabled = false;
        }
    }

    /// <summary>
    /// Updates the card's visual appearance based on effect type
    /// </summary>
    /// <param name="effectType">The card's effect type</param>
    private void UpdateCardAppearance(CardEffectType effectType)
    {
        if (backgroundImage == null) return;
        
        // Different colors based on effect type
        Color cardColor = Color.white;
        
        switch (effectType)
        {
            case CardEffectType.Damage:
                cardColor = new Color(1.0f, 0.6f, 0.6f); // Red for damage
                break;
            case CardEffectType.Heal:
                cardColor = new Color(0.6f, 1.0f, 0.6f); // Green for healing
                break;
            case CardEffectType.DrawCard:
                cardColor = new Color(0.6f, 0.8f, 1.0f); // Blue for card draw
                break;
            case CardEffectType.BuffStats:
                cardColor = new Color(0.9f, 0.9f, 0.6f); // Yellow for buffs
                break;
            case CardEffectType.DebuffStats:
                cardColor = new Color(0.8f, 0.6f, 1.0f); // Purple for debuffs
                break;
            case CardEffectType.ApplyStatus:
                cardColor = new Color(1.0f, 0.8f, 0.6f); // Orange for status effects
                break;
            default:
                cardColor = Color.white;
                break;
        }
        
        backgroundImage.color = cardColor;
    }

    /// <summary>
    /// Set whether this card can be purchased and at what cost
    /// </summary>
    public void SetPurchasable(bool canPurchase, int cost = 50)
    {
        isPurchasable = canPurchase;
        if (canPurchase)
        {
            purchaseCost = cost;
        }
    }
    
    /// <summary>
    /// Set whether this card can be drafted
    /// </summary>
    public void SetDraftable(bool canDraft)
    {
        isDraftable = canDraft;
    }
    
    /// <summary>
    /// Set the current container of this card
    /// </summary>
    /// <param name="container">Destination container (Deck, Hand, Discard)</param>
    public void SetCurrentContainer(CardLocation container)
    {
        if (container == currentContainer) return;
        
        CardLocation oldContainer = currentContainer;
        currentContainer = container;
        
        // Invoke container change event
        OnContainerChanged?.Invoke(oldContainer, container);
        
        // Update card visual state based on container
        UpdateCardStateForContainer();
    }
    
    /// <summary>
    /// Updates the card's visual state based on its current container
    /// </summary>
    private void UpdateCardStateForContainer()
    {
        // Basic state management - will be expanded in CardSpawner for actual visual updates
        switch (currentContainer)
        {
            case CardLocation.Deck:
                // Cards in deck are disabled (not visible)
                gameObject.SetActive(false);
                break;
                
            case CardLocation.Hand:
                // Cards in hand are enabled (visible)
                gameObject.SetActive(true);
                break;
                
            case CardLocation.Discard:
                // Cards in discard are disabled (not visible)
                gameObject.SetActive(false);
                break;
        }
    }

    // For in-game spawned cards, you might have a different script or NetworkObject representation.
    // This current script is more like the definition/template for a card.
    // If an actual GameObject card is moved around and needs its state synced, it might have a simpler NetworkedCard script
    // that holds a reference to a CardData (ScriptableObject or this Card script as a template).

    // Example: If this Card script itself is on the spawned NetworkObject representing the card in hand/play:
    // public override void OnStartServer() { base.OnStartServer(); /* Potentially sync some state if needed */ }
    // public override void OnStartClient() { base.OnStartClient(); /* Update UI based on synced state */ }
} 