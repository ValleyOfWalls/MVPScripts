using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using TMPro;
using System.Linq;
using FishNet.Object.Synchronizing;

/// <summary>
/// Manages the UI elements for the combat phase, including turn indicators, and notifications.
/// Attach to: The CombatCanvas GameObject that contains all combat UI elements.
/// </summary>
public class CombatCanvasManager : MonoBehaviour
{
    // Card rendering logic has been moved to CardSpawner

    [Header("Controls")]
    [SerializeField] private Button endTurnButton;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI turnIndicatorText;
    [SerializeField] private GameObject cardPlayedEffectPrefab;
    [SerializeField] private GameObject notificationPrefab;
    [SerializeField] private Transform effectsContainer;
    [SerializeField] private GameObject fightEndedPanel;
    [SerializeField] private TextMeshProUGUI fightEndedText;

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
        
        // Card rendering is now fully handled by CardSpawner component
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

    private void OnDestroy()
    {
        if (endTurnButton != null) endTurnButton.onClick.RemoveAllListeners();
    }

    public void SetEndTurnButtonInteractable(bool interactable)
    {
        if (endTurnButton != null)
        {
            endTurnButton.interactable = interactable;
        }
    }
} 