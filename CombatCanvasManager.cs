using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using TMPro;
using System.Linq;
using FishNet.Object.Synchronizing;

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

    private NetworkPlayer localPlayer;
    private NetworkPet opponentPetForLocalPlayer;

    private FightManager fightManager;
    private CombatManager combatManager;

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
        
        if (localPlayer != null && localPlayer.PlayerHandTransform != null && localPlayer.playerHandCardIds.Count > 0) {
             RenderHand(localPlayer.playerHandCardIds, localPlayer.PlayerHandTransform, true);
        }
        else if (localPlayer != null && localPlayer.PlayerHandTransform == null)
        {
            Debug.LogError("PlayerHandTransform is null on localPlayer. Cannot perform initial hand render.");
        }
    }

    private void InitializeButtonListeners()
    {
        if (endTurnButton != null && combatManager != null && localPlayer != null)
        {
            endTurnButton.onClick.AddListener(() => combatManager.CmdEndPlayerTurn(localPlayer.Owner));
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
        if (localPlayer == null) return; 
        if (localPlayer.PlayerHandTransform == null)
        {
            Debug.LogError("PlayerHandTransform is null on localPlayer. Cannot render hand on change.");
            return;
        }
        Debug.Log($"Player hand changed. Operation: {op}, Index: {index}, NewItem ID: {newItem}. Re-rendering hand...");
        RenderHand(localPlayer.playerHandCardIds, localPlayer.PlayerHandTransform, true);
    }

    private void RenderHand(SyncList<int> cardIds, Transform handArea, bool isPlayerHand)
    {
        if (handArea == null) 
        {
            Debug.LogError("RenderHand called with a null handArea. Cannot render cards.");
            return;
        }

        foreach (Transform child in handArea)
        {
            Destroy(child.gameObject);
        }

        CombatSetup combatSetup = FindFirstObjectByType<CombatSetup>();
        if (combatSetup == null || combatSetup.cardGamePrefab == null)
        {
            Debug.LogError("Cannot render hand: CombatSetup or cardGamePrefab not found.");
            return;
        }
        if (CardDatabase.Instance == null) {
            Debug.LogError("Cannot render hand: CardDatabase.Instance is null.");
            return;
        }

        foreach (int cardId in cardIds)
        {
            CardData cardDataInstance = CardDatabase.Instance.GetCardById(cardId);
            if (cardDataInstance == null)
            {
                Debug.LogWarning($"RenderHand: Could not find CardData for ID {cardId}. Skipping card visual.");
                continue;
            }

            GameObject cardVisualInstance = Instantiate(combatSetup.cardGamePrefab, handArea);
            Card cardComponent = cardVisualInstance.GetComponent<Card>();
            if (cardComponent != null)
            {
                cardComponent.Initialize(cardDataInstance);
            }
            else
            {
                 Debug.LogError($"RenderHand: Card prefab is missing Card script component for card ID {cardId}.");
                 TextMeshProUGUI cardText = cardVisualInstance.GetComponentInChildren<TextMeshProUGUI>();
                 if(cardText) cardText.text = $"ID: {cardId} (Error: Missing Card Script)"; 
            }

            if (isPlayerHand)
            {
                Button cardButton = cardVisualInstance.GetComponent<Button>();
                if (cardButton == null) cardButton = cardVisualInstance.AddComponent<Button>();
                cardButton.onClick.AddListener(() => OnPlayerCardClicked(cardDataInstance));
            }
            // else for opponent hand, if needed
        }
    }

    private void OnPlayerCardClicked(CardData cardDataInstance)
    {
        if (localPlayer == null || combatManager == null)
        {
            Debug.LogError("Cannot play card: LocalPlayer or CombatManager is null.");
            return;
        }

        if (cardDataInstance == null) {
            Debug.LogError("OnPlayerCardClicked: cardDataInstance is null.");
            return;
        }

        if (!combatManager.IsPlayerTurn(localPlayer))
        {
            Debug.Log("Not your turn!");
            return;
        }

        if (localPlayer.CurrentEnergy.Value < cardDataInstance.EnergyCost)
        {
            Debug.Log("Not enough energy to play this card!");
            return;
        }
        
        Debug.Log($"Player clicked on card '{cardDataInstance.CardName}' (ID: {cardDataInstance.CardId}). Requesting to play.");
        combatManager.CmdPlayerRequestsPlayCard(localPlayer.Owner, cardDataInstance.CardId);
    }

    public void SetEndTurnButtonInteractable(bool interactable)
    {
        if (endTurnButton != null) endTurnButton.interactable = interactable;
    }
} 