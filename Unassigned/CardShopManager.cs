using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Linq;

/// <summary>
/// Manages the card shop in the draft phase
/// </summary>
public class CardShopManager : NetworkBehaviour
{
    [Header("Shop Settings")]
    [SerializeField] private int numberOfCardsToShow = 3;
    [SerializeField] private Transform cardShopContainer;
    [SerializeField] private GameObject cardPrefab;

    [Header("References")]
    private CardDatabase cardDatabase;

    // Synced list of card IDs currently available in the shop
    private readonly SyncList<int> availableCardIds = new SyncList<int>();

    // Dictionary of spawned card objects, keyed by their IDs
    private Dictionary<int, GameObject> spawnedCards = new Dictionary<int, GameObject>();

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Get card database
        cardDatabase = CardDatabase.Instance;
        
        if (cardDatabase == null)
        {
            Debug.LogError("CardShopManager: CardDatabase instance not found!");
            return;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Register for shop inventory changes
        availableCardIds.OnChange += HandleShopInventoryChanged;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Unregister from shop inventory changes
        availableCardIds.OnChange -= HandleShopInventoryChanged;
    }

    /// <summary>
    /// Initializes the card shop with random cards
    /// Called from DraftSetup
    /// </summary>
    [Server]
    public void SetupShop()
    {
        if (!IsServerInitialized) return;
        
        // Clear current shop inventory
        availableCardIds.Clear();
        
        // Get random cards from the database
        List<int> randomCardIds = GetRandomCardIds(numberOfCardsToShow);
        
        // Add them to the available cards
        foreach (int cardId in randomCardIds)
        {
            availableCardIds.Add(cardId);
        }
    }

    /// <summary>
    /// Gets random card IDs from the card database
    /// </summary>
    private List<int> GetRandomCardIds(int count)
    {
        List<CardData> randomCards = cardDatabase.GetRandomCards(count);
        return randomCards.Select(card => card.CardId).ToList();
    }

    /// <summary>
    /// Updates the shop display based on available cards
    /// </summary>
    private void UpdateShopDisplay()
    {
        if (cardShopContainer == null) return;
        
        // Clear current display
        foreach (var spawnedCard in spawnedCards.Values)
        {
            Destroy(spawnedCard);
        }
        spawnedCards.Clear();
        
        // Create displays for each available card
        foreach (int cardId in availableCardIds)
        {
            CardData cardData = cardDatabase.GetCardById(cardId);
            if (cardData == null) continue;
            
            // Instantiate card object
            GameObject cardObj = Instantiate(cardPrefab, cardShopContainer);
            Card card = cardObj.GetComponent<Card>();
            CardSelectionManager selectionManager = cardObj.GetComponent<CardSelectionManager>();
            
            if (card != null)
            {
                // Initialize the card with data
                card.Initialize(cardData);
                
                if (selectionManager != null)
                {
                    selectionManager.SetSelectionMode(false); // false = shop mode
                }
                
                spawnedCards[cardId] = cardObj;
            }
        }
    }

    /// <summary>
    /// Called when a player attempts to purchase a card
    /// </summary>
    /// <param name="cardComponent">The card component to purchase</param>
    public void AttemptPurchaseCard(Card cardComponent)
    {
        if (cardComponent == null || cardComponent.CardData == null) return;
        
        // Find local player
        NetworkEntity localPlayer = null;
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
            Debug.LogError("CardShopManager: Could not find local player!");
            return;
        }
        
        // Check if player has enough currency
        if (localPlayer.Currency.Value < cardComponent.CardData.EnergyCost)
        {
            // Display insufficient funds message
            Debug.Log("Not enough currency to purchase card!");
            return;
        }
        
        // Request purchase from server
        CmdPurchaseCard(localPlayer.ObjectId, cardComponent.CardId);
    }

    /// <summary>
    /// Server RPC to purchase a card
    /// </summary>
    /// <param name="playerObjectId">Object ID of the purchasing player</param>
    /// <param name="cardId">ID of the card to purchase</param>
    [ServerRpc(RequireOwnership = false)]
    private void CmdPurchaseCard(int playerObjectId, int cardId)
    {
        // Get player by object ID
        NetworkObject playerObj = null;
        if (NetworkManager.ServerManager.Objects.Spawned.TryGetValue(playerObjectId, out playerObj))
        {
            NetworkEntity player = playerObj.GetComponent<NetworkEntity>();
            if (player == null) return;
            
            // Get card data
            CardData cardData = CardDatabase.Instance.GetCardById(cardId);
            if (cardData == null) return;
            
            // Check if card is available
            bool cardAvailable = false;
            foreach (int id in availableCardIds)
            {
                if (id == cardId)
                {
                    cardAvailable = true;
                    break;
                }
            }
            
            if (!cardAvailable)
            {
                Debug.LogWarning($"CardShopManager: Card {cardId} is not available for purchase.");
                return;
            }
            
            // Check if player has enough currency
            if (player.Currency.Value < cardData.EnergyCost)
            {
                Debug.LogWarning($"CardShopManager: Entity {player.EntityName} does not have enough currency.");
                return;
            }
            
            // Deduct cost
            player.DeductCurrency(cardData.EnergyCost);
            
            // Add card to player's deck
            DeckManager deckManager = player.GetComponent<DeckManager>();
            if (deckManager != null)
            {
                deckManager.AddCardToDeck(cardId);
                
                // Remove card from shop
                for (int i = 0; i < availableCardIds.Count; i++)
                {
                    if (availableCardIds[i] == cardId)
                    {
                        availableCardIds.RemoveAt(i);
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handles changes to the shop inventory
    /// </summary>
    private void HandleShopInventoryChanged(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
    {
        UpdateShopDisplay();
    }

    /// <summary>
    /// Spawns a card in the shop
    /// </summary>
    private void SpawnShopCard(CardData cardData)
    {
        if (cardPrefab == null || cardShopContainer == null) return;

        GameObject cardObj = Instantiate(cardPrefab, cardShopContainer);
        Card cardComponent = cardObj.GetComponent<Card>();
        CardSelectionManager selectionManager = cardObj.GetComponent<CardSelectionManager>();
        
        if (cardComponent != null)
        {
            cardComponent.Initialize(cardData);
            cardComponent.SetPurchasable(true, cardData.EnergyCost * 10); // Base cost on energy cost
        }
        
        if (selectionManager != null)
        {
            selectionManager.SetSelectionMode(false); // false = shop mode
        }
        
        spawnedCards.Add(cardData.CardId, cardObj);
    }

    /// <summary>
    /// Calculates the purchase cost for a card
    /// </summary>
    /// <param name="cardData">The card data to calculate cost for</param>
    /// <returns>The calculated cost in currency</returns>
    private int CalculateCardCost(CardData cardData)
    {
        if (cardData == null) return 50; // Default cost

        // Base cost on energy cost multiplied by 10
        int baseCost = cardData.EnergyCost * 10;

        // Add additional cost based on effect type
        switch (cardData.EffectType)
        {
            case CardEffectType.Damage:
            case CardEffectType.Heal:
                baseCost += cardData.Amount * 5; // More powerful effects cost more
                break;
            case CardEffectType.DrawCard:
                baseCost += cardData.Amount * 15; // Card draw is valuable
                break;
            case CardEffectType.BuffStats:
            case CardEffectType.DebuffStats:
                baseCost += cardData.Amount * 8; // Stat effects are moderately valuable
                break;
            case CardEffectType.ApplyStatus:
                baseCost += 20; // Status effects have a flat additional cost
                break;
        }

        // Ensure minimum cost of 10
        return Mathf.Max(10, baseCost);
    }
} 