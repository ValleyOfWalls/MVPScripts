using UnityEngine;
using FishNet.Object;

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

public class Card : NetworkBehaviour // Or MonoBehaviour if cards are locally controlled based on synced IDs
{
    // If this Card script is on a prefab that is NOT a NetworkObject itself, 
    // but is instantiated by a NetworkObject (like NetworkPlayer/Pet who owns the card instance),
    // then this script doesn't need to be a NetworkBehaviour.
    // Let's assume card *definitions* are not NetworkObjects, but card *instances in hand/play* might be.
    // For server authoritative, the *state* of which cards are where is synced, not necessarily each card itself as a NB.
    // Given the request, making it a MonoBehaviour is safer for now. If they need to be spawned independently as networked entities, then NetworkBehaviour.
    // Let's go with MonoBehaviour and assume card instances are managed by their owners (Player/Pet).
    // If card instances need to be transferred or have their own networked state, then it should be a NetworkBehaviour.

    [Header("Card Info")]
    [SerializeField] private int _cardId; // Unique ID for this card type
    [SerializeField] private string _cardName = "Default Card";
    [TextArea]
    [SerializeField] private string _description = "Default card description.";
    [SerializeField] private Sprite _cardArtwork;

    [Header("Card Effect")]
    [SerializeField] private CardEffectType _effectType = CardEffectType.Damage;
    [SerializeField] private int _amount = 5; // e.g., damage amount, heal amount, cards to draw
    [SerializeField] private int _energyCost = 1;

    // Potentially add target type (self, enemy, all enemies, specific ally etc.)

    // --- Runtime Properties (if instantiated in scene) ---
    // These would NOT be serialized on the prefab asset, but set on instances.
    public NetworkPlayer OwningPlayer { get; set; } // If card is owned by a player
    public NetworkPet OwningPet { get; set; } // If card is owned by a pet

    // --- Accessors ---
    public int CardId => _cardId;
    public string CardName => _cardName;
    public string Description => _description;
    public Sprite CardArtwork => _cardArtwork;
    public CardEffectType EffectType => _effectType;
    public int Amount => _amount;
    public int EnergyCost => _energyCost;

    // If this is a prefab that gets instantiated, an Init method might be useful.
    public void InitializeCard(int id, string cardName, string description, CardEffectType effectType, int amount, int energyCost, Sprite artwork = null)
    {
        _cardId = id;
        _cardName = cardName;
        _description = description;
        _effectType = effectType;
        _amount = amount;
        _energyCost = energyCost;
        _cardArtwork = artwork;
    }

    // For in-game spawned cards, you might have a different script or NetworkObject representation.
    // This current script is more like the definition/template for a card.
    // If an actual GameObject card is moved around and needs its state synced, it might have a simpler NetworkedCard script
    // that holds a reference to a CardData (ScriptableObject or this Card script as a template).

    // Example: If this Card script itself is on the spawned NetworkObject representing the card in hand/play:
    // public override void OnStartServer() { base.OnStartServer(); /* Potentially sync some state if needed */ }
    // public override void OnStartClient() { base.OnStartClient(); /* Update UI based on synced state */ }
} 