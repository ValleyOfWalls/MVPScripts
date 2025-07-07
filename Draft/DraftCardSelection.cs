using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;

/// <summary>
/// Handles card selection during the draft phase.
/// Attach to: Card prefabs to enable draft selection functionality.
/// </summary>
public class DraftCardSelection : MonoBehaviour
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
        draftManager = FindFirstObjectByType<DraftManager>();
        gamePhaseManager = GamePhaseManager.Instance;
        
        ValidateComponents();
        SetupButtonListener();
    }
    
    private void ValidateComponents()
    {
        if (cardButton == null)
            Debug.LogError($"DraftCardSelection on {gameObject.name}: Missing Button component");
        if (card == null)
            Debug.LogError($"DraftCardSelection on {gameObject.name}: Missing Card component");
    }
    
    private void SetupButtonListener()
    {
        if (cardButton != null)
        {
            cardButton.onClick.AddListener(OnCardClicked);
        }
    }
    
    /// <summary>
    /// Called when the card is clicked during draft phase
    /// </summary>
    public void OnCardClicked()
    {
        // Check if we're in the draft phase
        if (gamePhaseManager == null || gamePhaseManager.GetCurrentPhase() != GamePhaseManager.GamePhase.Draft)
        {
            Debug.Log($"DraftCardSelection: Card {gameObject.name} clicked but not in draft phase");
            return;
        }
        
        // Check if this card is draftable
        if (card == null || !card.IsDraftable)
        {
            Debug.Log($"DraftCardSelection: Card {gameObject.name} is not draftable");
            return;
        }
        
        // Check if the local player owns the pack containing this card
        if (!IsCardSelectableByLocalPlayer())
        {
            Debug.Log($"DraftCardSelection: Card {gameObject.name} is not selectable by local player");
            return;
        }
        
        /* Debug.Log($"DraftCardSelection: Card {gameObject.name} selected for drafting"); */
        
        // Notify the draft manager about the card selection
        if (draftManager != null)
        {
            draftManager.OnCardClicked(gameObject);
        }
        else
        {
            Debug.LogError("DraftCardSelection: DraftManager not found!");
        }
    }
    
    /// <summary>
    /// Checks if this card can be selected by the local player
    /// </summary>
    private bool IsCardSelectableByLocalPlayer()
    {
        if (draftManager == null)
        {
            Debug.Log($"DraftCardSelection: IsCardSelectableByLocalPlayer - draftManager is null");
            return false;
        }
        
        // Get the local player
        NetworkEntity localPlayer = GetLocalPlayer();
        if (localPlayer == null)
        {
            Debug.Log($"DraftCardSelection: IsCardSelectableByLocalPlayer - localPlayer is null");
            return false;
        }
        
        /* Debug.Log($"DraftCardSelection: IsCardSelectableByLocalPlayer - localPlayer found: {localPlayer.EntityName.Value}"); */
        
        // Check if the local player has a pack available and this card is in it
        bool isSelectable = draftManager.IsCardSelectableByPlayer(gameObject, localPlayer);
        Debug.Log($"DraftCardSelection: IsCardSelectableByLocalPlayer - draftManager.IsCardSelectableByPlayer returned: {isSelectable}");
        
        return isSelectable;
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