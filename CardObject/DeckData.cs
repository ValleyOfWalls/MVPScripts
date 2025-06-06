using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New DeckData", menuName = "Card Game/Deck Data")]
public class DeckData : ScriptableObject
{
    [SerializeField]
    private string _deckName = "New Deck";

    [SerializeField]
    private List<CardData> _cardsInDeck = new List<CardData>();

    public string DeckName => _deckName;
    public IReadOnlyList<CardData> CardsInDeck => _cardsInDeck.AsReadOnly(); // Provide read-only access
} 