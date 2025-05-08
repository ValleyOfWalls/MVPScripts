using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using TMPro;
using System.Linq;
using FishNet.Object.Synchronizing;

/// <summary>
/// Manages the UI elements for the combat phase, including card display, turn indicators, and notifications.
/// Attach to: The CombatCanvas GameObject that contains all combat UI elements.
/// </summary>
public class CombatCanvasManager : MonoBehaviour
{
    // UI Element References (assign in Inspector for the CombatCanvas prefab)
    // [Header("Player Area")]
    // [SerializeField] private Transform playerHandDisplayArea; // Removed
    // [SerializeField] private Transform playerDeckDisplayArea; // Removed
    // [SerializeField] private Transform playerDiscardDisplayArea; // Removed

    // [Header("Opponent Pet Area")]
    // [SerializeField] private Transform opponentPetHandDisplayArea; // Removed

    [Header("Controls")]
    [SerializeField] private Button endTurnButton;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI turnIndicatorText;
    [SerializeField] private GameObject cardPlayedEffectPrefab;
    [SerializeField] private GameObject notificationPrefab;
    [SerializeField] private Transform effectsContainer;
    [SerializeField] private GameObject fightEndedPanel;
    [SerializeField] private TextMeshProUGUI fightEndedText;
    
    // Card prefab for visual representation - moved from CombatSetup
    [SerializeField] private GameObject cardPrefab;

    private NetworkPlayer localPlayer;
    private NetworkPet opponentPetForLocalPlayer;

    private FightManager fightManager;
    private CombatManager combatManager;

    public void Initialize(CombatManager manager, NetworkPlayer player)
    {
        combatManager = manager;
        localPlayer = player;
    }

    public void UpdateTurnIndicator(string currentTurnEntityName)
    {
        if (turnIndicatorText != null)
        {
            turnIndicatorText.text = $"{currentTurnEntityName}'s Turn";
        }
    }

    public void ShowCardPlayedEffect(int cardId, NetworkBehaviour caster, NetworkBehaviour target)
    {
        if (cardPlayedEffectPrefab == null || effectsContainer == null) return;

        // Get card data
        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null) return;

        // Instantiate effect
        GameObject effectObj = Instantiate(cardPlayedEffectPrefab, effectsContainer);
        CardPlayedEffect effect = effectObj.GetComponent<CardPlayedEffect>();
        
        if (effect != null)
        {
            string casterName = caster is NetworkPlayer player ? player.PlayerName.Value : ((NetworkPet)caster).PetName.Value;
            string targetName = target is NetworkPlayer targetPlayer ? targetPlayer.PlayerName.Value : ((NetworkPet)target).PetName.Value;
            
            effect.Initialize(cardData, casterName, targetName);
        }
    }

    public void ShowFightEndedUI(NetworkPlayer player, NetworkPet pet, bool petWon)
    {
        if (fightEndedPanel == null || fightEndedText == null) return;

        string winnerName = petWon ? pet.PetName.Value : player.PlayerName.Value;
        fightEndedText.text = $"{winnerName} has won the fight!";
        fightEndedPanel.SetActive(true);
    }

    public void ShowNotificationMessage(string message)
    {
        if (notificationPrefab == null || effectsContainer == null) return;

        GameObject notificationObj = Instantiate(notificationPrefab, effectsContainer);
        NotificationMessage notification = notificationObj.GetComponent<NotificationMessage>();
        
        if (notification != null)
        {
            notification.Initialize(message);
        }
    }

    public void SetupCombatUI()
    {
        Debug.Log("CombatCanvasManager: SetupCombatUI called.");

        fightManager = FindFirstObjectByType<FightManager>();
        combatManager = FindFirstObjectByType<CombatManager>();

        if (fightManager == null) Debug.LogError("FightManager not found by CombatCanvasManager.");
        if (combatManager == null) Debug.LogError("CombatManager not found by CombatCanvasManager.");

        var localConnection = FishNet.InstanceFinder.ClientManager.Connection;
        localPlayer = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None).FirstOrDefault(p => p.Owner == localConnection && p.IsOwner);

        if (localPlayer == null)
        {
            Debug.LogError("Local NetworkPlayer not found for UI setup.");
            if(FishNet.InstanceFinder.IsHostStarted && FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None).Length > 0){
                localPlayer = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None).FirstOrDefault(p => p.IsOwner); 
                if(localPlayer != null) Debug.LogWarning("Local player not found by specific Owner, defaulting to first owned player as Host.");
                else Debug.LogError("Host check for local player also failed to find an owned NetworkPlayer.");
            }
            if (localPlayer == null) return;
        }

        Debug.Log($"Setting up UI for local player: {localPlayer.PlayerName.Value}"); 

        opponentPetForLocalPlayer = fightManager.GetOpponentForPlayer(localPlayer);

        if (opponentPetForLocalPlayer == null)
        {
            Debug.LogWarning($"No opponent pet found for player {localPlayer.PlayerName.Value} in FightManager. Opponent UI might be incomplete.");
        }

        InitializeButtonListeners();
        SubscribeToCardEvents();
        
        // Hand rendering is now handled by CardSpawner component
        // No need to manually call RenderHand anymore
    }

    private void InitializeButtonListeners()
    {
        if (endTurnButton != null && combatManager != null && localPlayer != null)
        {
            endTurnButton.onClick.AddListener(() => combatManager.CmdEndPlayerTurn());
        }
        else
        {
            if(endTurnButton == null) Debug.LogError("End Turn Button not assigned in CombatCanvasManager.");
            if(combatManager == null) Debug.LogError("CombatManager not assigned in CombatCanvasManager (for end turn).");
            if(localPlayer == null) Debug.LogError("LocalPlayer not found, cannot assign end turn button action properly.");
        }
    }

    private void SubscribeToCardEvents()
    {
        if (localPlayer != null)
        {
            // This is no longer needed as CardSpawner handles rendering
            // Keeping the subscription for compatibility but it doesn't need to do anything
            localPlayer.playerHandCardIds.OnChange += OnPlayerHandChanged;
        }
    }

    private void OnDestroy()
    {
        if (endTurnButton != null) endTurnButton.onClick.RemoveAllListeners();
        UnsubscribeFromCardEvents();
    }

    private void UnsubscribeFromCardEvents()
    {
        if (localPlayer != null)
        {
            localPlayer.playerHandCardIds.OnChange -= OnPlayerHandChanged;
        }
    }

    private void OnPlayerHandChanged(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
    {
        // Hand rendering is now handled by CardSpawner
        // This method is kept for backward compatibility
        Debug.Log($"Player hand changed. Operation: {op}, Index: {index}, NewItem ID: {newItem}. CardSpawner will update the display.");
    }

    // This method is no longer used since CardSpawner handles rendering
    // Keeping it for reference but with a warning
    private void RenderHand(SyncList<int> cardIds, Transform handArea, bool isPlayerHand)
    {
        Debug.LogWarning("RenderHand is deprecated. Card rendering is now handled by CardSpawner component.");
        
        // Method implementation removed as it's no longer needed
    }

    private void OnPlayerCardClicked(CardData cardDataInstance)
    {
        if (localPlayer == null || combatManager == null)
        {
            Debug.LogError("Cannot play card: LocalPlayer or CombatManager is null.");
            return;
        }

        if (cardDataInstance != null)
        {
            Debug.Log($"Player clicked card: {cardDataInstance.CardName}");
            combatManager.CmdPlayerRequestsPlayCard(cardDataInstance.CardId);
        }
    }

    public void SetEndTurnButtonInteractable(bool interactable)
    {
        if (endTurnButton != null)
        {
            endTurnButton.interactable = interactable;
        }
    }
} 