using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using MVPScripts.Utility;

/// <summary>
/// Handles card purchasing from the shop during the draft phase.
/// Attach to: Shop card prefabs to enable purchase functionality.
/// </summary>
public class ShopCardSelection : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button cardButton;
    [SerializeField] private Card card;
    
    private DraftManager draftManager;
    private GamePhaseManager gamePhaseManager;
    
    private void Awake()
    {
        // Get required components
        if (cardButton == null) cardButton = GetComponent<Button>();
        if (card == null) card = GetComponent<Card>();
        
        // Find managers
        ComponentResolver.FindComponent(ref draftManager, gameObject);
        gamePhaseManager = GamePhaseManager.Instance;
        
        ValidateComponents();
        SetupButtonListener();
    }
    
    private void ValidateComponents()
    {
        if (cardButton == null)
            Debug.LogError($"ShopCardSelection on {gameObject.name}: Missing Button component");
        if (card == null)
            Debug.LogError($"ShopCardSelection on {gameObject.name}: Missing Card component");
    }
    
    private void SetupButtonListener()
    {
        if (cardButton != null)
        {
            cardButton.onClick.AddListener(OnCardClicked);
        }
    }
    
    /// <summary>
    /// Called when the shop card is clicked
    /// </summary>
    public void OnCardClicked()
    {
        // Check if we're in the draft phase
        if (gamePhaseManager == null || gamePhaseManager.GetCurrentPhase() != GamePhaseManager.GamePhase.Draft)
        {
            Debug.Log($"ShopCardSelection: Card {gameObject.name} clicked but not in draft phase");
            return;
        }
        
        // Check if this card is purchasable
        if (card == null || !card.IsPurchasable)
        {
            Debug.Log($"ShopCardSelection: Card {gameObject.name} is not purchasable");
            return;
        }
        
        // Check if the local player can afford this card
        if (!CanLocalPlayerAffordCard())
        {
            Debug.Log($"ShopCardSelection: Local player cannot afford card {gameObject.name}");
            return;
        }
        
        /* Debug.Log($"ShopCardSelection: Card {gameObject.name} selected for purchase"); */
        
        // Notify the draft manager about the card purchase attempt
        if (draftManager != null)
        {
            draftManager.OnShopCardClicked(gameObject);
        }
        else
        {
            Debug.LogError("ShopCardSelection: DraftManager not found!");
        }
    }
    
    /// <summary>
    /// Checks if the local player can afford this card
    /// </summary>
    private bool CanLocalPlayerAffordCard()
    {
        if (card == null)
        {
            Debug.Log($"ShopCardSelection: CanLocalPlayerAffordCard - card is null");
            return false;
        }
        
        // Get the local player
        NetworkEntity localPlayer = GetLocalPlayer();
        if (localPlayer == null)
        {
            Debug.Log($"ShopCardSelection: CanLocalPlayerAffordCard - localPlayer is null");
            return false;
        }
        
        // Check if player is actually a player (not a pet)
        if (localPlayer.EntityType != EntityType.Player)
        {
            Debug.Log($"ShopCardSelection: CanLocalPlayerAffordCard - localPlayer is not a player entity");
            return false;
        }
        
        /* Debug.Log($"ShopCardSelection: CanLocalPlayerAffordCard - localPlayer found: {localPlayer.EntityName.Value}"); */
        /* Debug.Log($"ShopCardSelection: Player currency: {localPlayer.Currency.Value}, Card cost: {card.PurchaseCost}"); */
        
        // Check if player has enough currency
        bool canAfford = localPlayer.Currency.Value >= card.PurchaseCost;
        /* Debug.Log($"ShopCardSelection: CanLocalPlayerAffordCard - can afford: {canAfford}"); */
        
        return canAfford;
    }
    
    /// <summary>
    /// Gets the local player entity
    /// </summary>
    private NetworkEntity GetLocalPlayer()
    {
        NetworkEntity[] entities = FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        foreach (NetworkEntity entity in entities)
        {
            if (entity.EntityType == EntityType.Player && entity.IsOwner)
            {
                return entity;
            }
        }
        return null;
    }
    
    private void OnDestroy()
    {
        if (cardButton != null)
        {
            cardButton.onClick.RemoveListener(OnCardClicked);
        }
    }
} 