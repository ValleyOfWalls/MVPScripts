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
    [SerializeField] private Button spectateButton;
    [SerializeField] private Button returnToOwnFightButton;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI turnIndicatorText;
    [SerializeField] private GameObject fightEndedPanel;
    [SerializeField] private TextMeshProUGUI fightEndedText;
    [SerializeField] private GameObject combatCanvas;

    private NetworkEntity localPlayer;
    private NetworkEntity opponentPetForLocalPlayer;

    private FightManager fightManager;
    private CombatManager combatManager;

    // Public properties for accessing UI elements
    public Button SpectateButton => spectateButton;
    public Button ReturnToOwnFightButton => returnToOwnFightButton;

    public void Initialize(CombatManager manager, NetworkEntity player)
    {
        combatManager = manager;
        localPlayer = player;
    }

    public void UpdateTurnUI(CombatTurn turn)
    {
        if (turnIndicatorText != null)
        {
            string turnText = turn switch
            {
                CombatTurn.PlayerTurn => "Player's Turn",
                CombatTurn.PetTurn => "Pet's Turn",
                _ => "Waiting..."
            };
            turnIndicatorText.text = turnText;
        }

        // Enable/disable end turn button based on whose turn it is
        if (endTurnButton != null)
        {
            bool isLocalPlayerTurn = turn == CombatTurn.PlayerTurn && localPlayer != null && localPlayer.IsOwner;
            endTurnButton.interactable = isLocalPlayerTurn;
        }
    }

    public void ShowCardPlayedEffect(int cardId, NetworkBehaviour caster, NetworkBehaviour target)
    {
        // Simplified version that just logs to console
        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null) return;

        string casterName = caster.GetComponent<NetworkEntity>()?.EntityName.Value ?? "Unknown";
        string targetName = target.GetComponent<NetworkEntity>()?.EntityName.Value ?? "Unknown";
        
        Debug.Log($"Card played: {casterName} played {cardData.CardName} on {targetName}");
    }

    public void ShowFightEndedPanel(NetworkEntity player, NetworkEntity pet, bool petWon)
    {
        if (fightEndedPanel == null || fightEndedText == null) return;

        string winnerName = petWon ? pet.EntityName.Value : player.EntityName.Value;
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
        // Try to find local player through various means
        localPlayer = FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None)
            .FirstOrDefault(p => p.EntityType == EntityType.Player && p.IsOwner);

        if (localPlayer == null)
        {
            Debug.LogWarning("Could not find local player through direct search.");
        }
    }

    private void LogLocalPlayerError()
    {
        Debug.LogError("CombatCanvasManager: Could not find local player. UI setup failed.");
    }

    private IEnumerator RetryFindOpponent()
    {
        int retryCount = 0;
        const int maxRetries = 5;
        const float retryDelay = 0.5f;

        while (opponentPetForLocalPlayer == null && retryCount < maxRetries)
        {
            yield return new WaitForSeconds(retryDelay);
            opponentPetForLocalPlayer = fightManager.GetOpponentForPlayer(localPlayer);
            retryCount++;
        }

        if (opponentPetForLocalPlayer != null)
        {
            CompleteUISetup();
        }
        else
        {
            Debug.LogError("Failed to find opponent pet after retries.");
        }
    }

    private void CompleteUISetup()
    {
        // Set up UI elements based on the local player and their opponent
        if (localPlayer != null && opponentPetForLocalPlayer != null)
        {
            Debug.Log($"Setting up combat UI for {localPlayer.EntityName.Value} vs {opponentPetForLocalPlayer.EntityName.Value}");
            
            // Initialize button listeners
            InitializeButtonListeners();
            
            // Additional UI setup code here
        }
    }

    private void InitializeButtonListeners()
    {
        Debug.Log("CombatCanvasManager: Initializing button listeners");
        SetupEndTurnButton();
    }

    /// <summary>
    /// Sets up the end turn button to correctly end the local player's turn.
    /// </summary>
    private void SetupEndTurnButton()
    {
        if (endTurnButton != null)
        {
            Debug.Log("CombatCanvasManager: Setting up end turn button");
            endTurnButton.onClick.RemoveAllListeners();
            endTurnButton.onClick.AddListener(() => {
                Debug.Log("CombatCanvasManager: End turn button clicked");
                if (combatManager != null && localPlayer != null)
                {
                    Debug.Log($"CombatCanvasManager: Sending end turn request for player {localPlayer.EntityName.Value}");
                    // Call the server method to end the turn
                    combatManager.OnEndTurnButtonPressed(localPlayer.Owner);
                }
                else
                {
                    Debug.LogError($"Cannot end turn: Missing {(combatManager == null ? "CombatManager" : "local player")} reference");
                }
            });
            
            Debug.Log("CombatCanvasManager: End turn button setup complete");
        }
        else
        {
            Debug.LogError("End Turn Button not assigned in CombatCanvasManager");
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
    
    /// <summary>
    /// Disables the end turn button when the local player's fight is over
    /// </summary>
    public void DisableEndTurnButton()
    {
        if (endTurnButton != null)
        {
            endTurnButton.gameObject.SetActive(false);
            Debug.Log("CombatCanvasManager: End turn button disabled - fight is over");
        }
    }
    
    /// <summary>
    /// Enables the end turn button (used when starting a new fight)
    /// </summary>
    public void EnableEndTurnButton()
    {
        if (endTurnButton != null)
        {
            endTurnButton.gameObject.SetActive(true);
            Debug.Log("CombatCanvasManager: End turn button enabled");
        }
    }
    
    /// <summary>
    /// Disables the entire combat canvas (used during draft transition)
    /// </summary>
    public void DisableCombatCanvas()
    {
        if (combatCanvas != null)
        {
            combatCanvas.SetActive(false);
            Debug.Log("CombatCanvasManager: Combat canvas GameObject disabled");
        }
        else
        {
            Debug.LogError("CombatCanvasManager: Combat canvas reference is not assigned");
        }
    }
    
    /// <summary>
    /// Enables the entire combat canvas (used when returning to combat)
    /// </summary>
    public void EnableCombatCanvas()
    {
        if (combatCanvas != null)
        {
            combatCanvas.SetActive(true);
            Debug.Log("CombatCanvasManager: Combat canvas GameObject enabled");
        }
        else
        {
            Debug.LogError("CombatCanvasManager: Combat canvas reference is not assigned");
        }
    }
    
    /// <summary>
    /// Called when the local player's fight ends to update UI accordingly
    /// </summary>
    public void OnLocalFightEnded()
    {
        DisableEndTurnButton();
        Debug.Log("CombatCanvasManager: Local fight ended - UI updated");
    }
} 