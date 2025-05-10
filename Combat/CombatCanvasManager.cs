using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using TMPro;
using System.Linq;
using System.Collections;

/// <summary>
/// Manages the UI elements for the combat phase, including turn indicators, and notifications.
/// Attach to: The CombatCanvas GameObject that contains all combat UI elements.
/// </summary>
public class CombatCanvasManager : MonoBehaviour
{
    [Header("Controls")]
    [SerializeField] private Button endTurnButton;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI turnIndicatorText;
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
        // Simplified version that just logs to console
        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null) return;

        string casterName = caster is NetworkPlayer player ? player.PlayerName.Value : ((NetworkPet)caster).PetName.Value;
        string targetName = target is NetworkPlayer targetPlayer ? targetPlayer.PlayerName.Value : ((NetworkPet)target).PetName.Value;
        
        Debug.Log($"Card played: {casterName} played {cardData.CardName} on {targetName}");
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
        // Simplified version that just logs to console
        Debug.Log($"Combat notification: {message}");
    }

    public void SetupCombatUI()
    {
        // Find managers if not already assigned
        if (fightManager == null) fightManager = FindFirstObjectByType<FightManager>();
        if (combatManager == null) combatManager = FindFirstObjectByType<CombatManager>();

        if (fightManager == null) Debug.LogError("FightManager not found by CombatCanvasManager.");
        if (combatManager == null) Debug.LogError("CombatManager not found by CombatCanvasManager.");

        // Try multiple approaches to find the local player
        FindLocalPlayer();

        if (localPlayer == null)
        {
            LogLocalPlayerError();
            return;
        }

        // Find opponent
        opponentPetForLocalPlayer = fightManager.GetOpponentForPlayer(localPlayer);

        if (opponentPetForLocalPlayer == null)
        {
            // Try to wait and retry if opponent isn't available yet
            StartCoroutine(RetryFindOpponent());
        }
        else
        {
            // Complete UI setup since we found opponent
            CompleteUISetup();
        }
    }
    
    private void FindLocalPlayer()
    {
        var localConnection = FishNet.InstanceFinder.ClientManager.Connection;
        
        // For host, use special detection method first
        if (FishNet.InstanceFinder.IsHostStarted)
        {
            localPlayer = FindHostPlayerForLocalConnection();
        }
        
        // If still no player, try standard approaches
        if (localPlayer == null)
        {
            // 1. First try the standard approach
            localPlayer = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None)
                .FirstOrDefault(p => p.Owner == localConnection && p.IsOwner);
                
            // 2. If that fails, just check for IsOwner
            if (localPlayer == null)
            {
                localPlayer = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None)
                    .FirstOrDefault(p => p.IsOwner);
            }
            
            // 3. If that fails and we're the host, try a more lenient approach
            if (localPlayer == null && FishNet.InstanceFinder.IsHostStarted)
            {
                // Try by connection clientID
                if (localConnection != null)
                {
                    localPlayer = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None)
                        .FirstOrDefault(p => p.Owner != null && p.Owner.ClientId == localConnection.ClientId);
                }
                
                // Last resort: if host and still no player, use first NetworkPlayer found
                if (localPlayer == null && FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None).Length > 0)
                {
                    localPlayer = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None).First();
                }
            }
        }
    }
    
    private void LogLocalPlayerError()
    {
        Debug.LogError("Local NetworkPlayer not found for UI setup.");
        // Additional debug info to help troubleshoot
        int playerCount = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None).Length;
        Debug.LogError($"Found {playerCount} NetworkPlayer objects in the scene but none matched local connection.");
        
        var localConnection = FishNet.InstanceFinder.ClientManager.Connection;
        if (localConnection != null)
            Debug.LogError($"Local connection ClientId: {localConnection.ClientId}, IsOwner not set properly?");
        else
            Debug.LogError("Local connection is null!");
    }
    
    // Retry finding the opponent with a delay to allow network sync
    private IEnumerator RetryFindOpponent()
    {
        int maxAttempts = 5;
        int attempts = 0;
        
        while (opponentPetForLocalPlayer == null && attempts < maxAttempts)
        {
            yield return new WaitForSeconds(0.5f);
            
            if (fightManager != null && localPlayer != null)
            {
                opponentPetForLocalPlayer = fightManager.GetOpponentForPlayer(localPlayer);
                
                if (opponentPetForLocalPlayer != null)
                {
                    CompleteUISetup();
                    yield break;
                }
            }
            
            attempts++;
        }
        
        // Even if we didn't find an opponent, we should still set up as much of the UI as we can
        CompleteUISetup();
    }
    
    // Finalize UI setup
    private void CompleteUISetup()
    {
        InitializeButtonListeners();
    }

    private void InitializeButtonListeners()
    {
        SetupEndTurnButton();
    }

    /// <summary>
    /// Sets up the end turn button to correctly end the local player's turn.
    /// </summary>
    private void SetupEndTurnButton()
    {
        if (endTurnButton != null && combatManager != null && localPlayer != null)
        {
            endTurnButton.onClick.RemoveAllListeners();
            endTurnButton.onClick.AddListener(() => {
                Debug.Log($"End Turn button clicked for local player: {localPlayer.PlayerName.Value} with ObjectId: {localPlayer.ObjectId}");
                
                // Use the new method that explicitly sends the player's Object ID
                combatManager.CmdEndTurnForPlayer((int)localPlayer.ObjectId);
            });
            
            Debug.Log($"End turn button setup complete for player {localPlayer.PlayerName.Value} with ObjectId {localPlayer.ObjectId}");
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
    
    private NetworkPlayer FindHostPlayerForLocalConnection()
    {
        // This method specifically helps in host mode
        var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.IsOwner || player.IsHostInitialized)
            {
                return player;
            }
        }
        return null;
    }
} 