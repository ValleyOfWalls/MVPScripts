using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using TMPro;
using System.Linq;
using FishNet.Object.Synchronizing;
using System.Collections;

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

        // More robust player detection
        var localConnection = FishNet.InstanceFinder.ClientManager.Connection;
        
        // For host, use special detection method first
        if (FishNet.InstanceFinder.IsHostStarted)
        {
            localPlayer = FindHostPlayerForLocalConnection();
            if (localPlayer != null)
            {
                Debug.Log($"Host-specific player detection found: {localPlayer.PlayerName.Value}");
            }
        }
        
        // If we still don't have a player (or not host), try regular methods
        if (localPlayer == null)
        {
            // Try multiple approaches to find the local player
            // 1. First try the standard approach
            localPlayer = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None)
                .FirstOrDefault(p => p.Owner == localConnection && p.IsOwner);
                
            // 2. If that fails, just check for IsOwner
            if (localPlayer == null)
            {
                localPlayer = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None)
                    .FirstOrDefault(p => p.IsOwner);
                
                if (localPlayer != null)
                    Debug.Log($"Found local player by IsOwner property: {localPlayer.PlayerName.Value}");
            }
            
            // 3. If that fails and we're the host, try a more lenient approach
            if (localPlayer == null && FishNet.InstanceFinder.IsHostStarted)
            {
                // Try by connection clientID
                if (localConnection != null)
                {
                    localPlayer = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None)
                        .FirstOrDefault(p => p.Owner != null && p.Owner.ClientId == localConnection.ClientId);
                    
                    if (localPlayer != null)
                        Debug.Log($"Found local player by ClientId match: {localPlayer.PlayerName.Value}");
                }
                
                // Last resort: if host and still no player, use first NetworkPlayer found
                if (localPlayer == null && FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None).Length > 0)
                {
                    localPlayer = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None).First();
                    Debug.LogWarning($"Using first available NetworkPlayer as local player for Host: {localPlayer.PlayerName.Value}");
                }
            }
        }

        if (localPlayer == null)
        {
            Debug.LogError("Local NetworkPlayer not found for UI setup.");
            // Additional debug info to help troubleshoot
            int playerCount = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None).Length;
            Debug.LogError($"Found {playerCount} NetworkPlayer objects in the scene but none matched local connection.");
            
            if (localConnection != null)
                Debug.LogError($"Local connection ClientId: {localConnection.ClientId}, IsOwner not set properly?");
            else
                Debug.LogError("Local connection is null!");
                
            return;
        }

        Debug.Log($"Setting up UI for local player: {localPlayer.PlayerName.Value}"); 

        opponentPetForLocalPlayer = fightManager.GetOpponentForPlayer(localPlayer);

        if (opponentPetForLocalPlayer == null)
        {
            Debug.LogWarning($"No opponent pet found for player {localPlayer.PlayerName.Value} in FightManager. Opponent UI might be incomplete.");
            
            // Try to wait and retry if opponent isn't available yet
            StartCoroutine(RetryFindOpponent());
        }
        else
        {
            // Complete UI setup since we found opponent
            CompleteUISetup();
        }
    }
    
    // Retry finding the opponent with a delay to allow network sync
    private IEnumerator RetryFindOpponent()
    {
        int maxAttempts = 5;
        int attempts = 0;
        
        while (opponentPetForLocalPlayer == null && attempts < maxAttempts)
        {
            Debug.Log($"Attempt {attempts+1}: Waiting for opponent pet assignment in FightManager...");
            yield return new WaitForSeconds(0.5f);
            
            if (fightManager != null && localPlayer != null)
            {
                opponentPetForLocalPlayer = fightManager.GetOpponentForPlayer(localPlayer);
                
                if (opponentPetForLocalPlayer != null)
                {
                    Debug.Log($"Found opponent pet on retry: {opponentPetForLocalPlayer.PetName.Value}");
                    CompleteUISetup();
                    yield break;
                }
            }
            
            attempts++;
        }
        
        // Even if we didn't find an opponent, we should still set up as much of the UI as we can
        Debug.LogWarning("Could not find opponent pet after multiple attempts. Setting up UI with limited data.");
        CompleteUISetup();
    }
    
    // Finalize UI setup
    private void CompleteUISetup()
    {
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

    // Add a new method to specifically handle host player detection 
    private NetworkPlayer FindHostPlayerForLocalConnection()
    {
        var localConnection = FishNet.InstanceFinder.ClientManager.Connection;
        
        // Check if we're running as host
        if (FishNet.InstanceFinder.IsHostStarted)
        {
            Debug.Log("Running as host, using special host player detection logic");
            
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            
            // Log all players for debugging
            foreach (var player in players)
            {
                Debug.Log($"Player in scene: {player.PlayerName.Value}, ID: {player.ObjectId}, " +
                         $"OwnerId: {(player.Owner != null ? player.Owner.ClientId : -1)}, IsOwner: {player.IsOwner}");
            }
            
            // For host, first check by ClientId 0 which is the host's connection
            var hostPlayer = players.FirstOrDefault(p => p.Owner != null && p.Owner.ClientId == 0);
            
            if (hostPlayer != null)
            {
                Debug.Log($"Found host player by ClientId 0: {hostPlayer.PlayerName.Value}");
                return hostPlayer;
            }
            
            // If that fails, try other methods
            hostPlayer = players.FirstOrDefault(p => p.IsOwner);
            if (hostPlayer != null)
            {
                Debug.Log($"Found host player by IsOwner: {hostPlayer.PlayerName.Value}");
                return hostPlayer;
            }
            
            // Last resort: take the player with no owner (server owned) or the first player if all else fails
            hostPlayer = players.FirstOrDefault(p => p.Owner == null) ?? (players.Length > 0 ? players[0] : null);
            if (hostPlayer != null)
            {
                Debug.Log($"Using fallback for host player: {hostPlayer.PlayerName.Value}");
                return hostPlayer;
            }
        }
        
        return null;
    }
} 