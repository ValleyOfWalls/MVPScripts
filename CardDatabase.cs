using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CardDatabase : MonoBehaviour
{
    public static CardDatabase Instance { get; private set; }

    [SerializeField]
    private List<CardData> allCardDataList = new List<CardData>(); // Assign all CardData SOs here in Inspector

    // OR, load from Resources:
    // [SerializeField] private string cardDataPathInResources = "CardData"; 

    private Dictionary<int, CardData> cardDataById = new Dictionary<int, CardData>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Make it persistent
            InitializeDatabase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeDatabase()
    {
        cardDataById.Clear();

        // Option 1: If using the list assigned in Inspector
        foreach (CardData data in allCardDataList)
        {
            if (data != null)
            {
                if (!cardDataById.ContainsKey(data.CardId))
                {
                    cardDataById.Add(data.CardId, data);
                }
                else
                {
                    Debug.LogWarning($"CardDatabase: Duplicate CardId {data.CardId} found for '{data.CardName}' and '{cardDataById[data.CardId].CardName}'. Ignoring duplicate.");
                }
            }
        }
        Debug.Log($"CardDatabase initialized with {cardDataById.Count} cards from Inspector list.");

        // Option 2: If loading from Resources folder
        /*
        CardData[] loadedCards = Resources.LoadAll<CardData>(cardDataPathInResources);
        foreach (CardData data in loadedCards)
        {
            if (data != null)
            {
                if (!cardDataById.ContainsKey(data.CardId))
                {
                    cardDataById.Add(data.CardId, data);
                }
                else
                {
                    Debug.LogWarning($"CardDatabase: Duplicate CardId {data.CardId} found for '{data.CardName}' and '{cardDataById[data.CardId].CardName}'. Ignoring duplicate.");
                }
            }
        }
        Debug.Log($"CardDatabase initialized with {cardDataById.Count} cards from Resources folder: Assets/Resources/{cardDataPathInResources}");
        */

        if (cardDataById.Count == 0)
        {
            Debug.LogWarning("CardDatabase is empty. Make sure to assign CardData ScriptableObjects to the list in the Inspector or place them in the Resources folder if using that loading method.");
        }
    }

    public CardData GetCardById(int cardId)
    {
        if (cardDataById.TryGetValue(cardId, out CardData data))
        {
            return data;
        }
        Debug.LogWarning($"CardDatabase: Card with ID {cardId} not found.");
        return null;
    }

    public List<CardData> GetAllCards()
    {
        return new List<CardData>(cardDataById.Values);
    }
} 