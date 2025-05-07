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

// This script would be on a Card Prefab.
// For networking, you typically don't network individual cards unless they have complex, independent behaviors.
// More often, you network card IDs and the server validates plays, then RPCs results.
// However, if cards are physicalized and need to be seen moving by all, they can be NetworkObjects.
// For simplicity and following the request, let's make it a NetworkBehaviour for now,
// though a non-NetworkBehaviour ScriptableObject or simple class for CardData is often preferred for the definition,
// and then a separate NetworkObject for the in-game representation if needed.

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

    [Header("UI References (Assign in Prefab)")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image artworkImage;
    [SerializeField] private TextMeshProUGUI energyCostText;

    public CardData cardData { get; private set; }

    // Public quick accessors (optional, can just use cardData.PropertyName)
    public int CardId => cardData != null ? cardData.CardId : -1;
    public string CardName => cardData != null ? cardData.CardName : "No Data";
    public int EnergyCost => cardData != null ? cardData.EnergyCost : 0;
    public CardEffectType EffectType => cardData != null ? cardData.EffectType : default(CardEffectType);
    public int Amount => cardData != null ? cardData.Amount : 0;

    public void Initialize(CardData data)
    {
        cardData = data;
        if (cardData == null)
        {
            Debug.LogError("Cannot initialize card with null CardData.", this.gameObject);
            // Optionally set UI to indicate an error or missing data
            if (nameText != null) nameText.text = "Error";
            if (descriptionText != null) descriptionText.text = "No Card Data Loaded";
            if (energyCostText != null) energyCostText.text = "X";
            return;
        }

        if (nameText != null) nameText.text = cardData.CardName;
        if (descriptionText != null) descriptionText.text = cardData.Description;
        if (artworkImage != null) artworkImage.sprite = cardData.CardArtwork; // Make sure sprite is assigned in CardData SO
        if (energyCostText != null) energyCostText.text = cardData.EnergyCost.ToString();
        
        this.gameObject.name = $"Card_{cardData.CardName}_{cardData.CardId}";
    }

    // For in-game spawned cards, you might have a different script or NetworkObject representation.
    // This current script is more like the definition/template for a card.
    // If an actual GameObject card is moved around and needs its state synced, it might have a simpler NetworkedCard script
    // that holds a reference to a CardData (ScriptableObject or this Card script as a template).

    // Example: If this Card script itself is on the spawned NetworkObject representing the card in hand/play:
    // public override void OnStartServer() { base.OnStartServer(); /* Potentially sync some state if needed */ }
    // public override void OnStartClient() { base.OnStartClient(); /* Update UI based on synced state */ }
} 