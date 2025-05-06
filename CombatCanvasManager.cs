using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using FishNet.Connection;
using TMPro;
using System.Linq;
using FishNet.Object.Synchronizing;

public class CombatCanvasManager : MonoBehaviour
{
    // UI Element References (assign in Inspector for the CombatCanvas prefab)
    [Header("Player Area")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI playerHealthText;
    [SerializeField] private TextMeshProUGUI playerEnergyText;
    [SerializeField] private Transform playerHandDisplayArea; // Parent for player's card visuals
    [SerializeField] private Transform playerDeckDisplayArea; // Visual representation of deck
    [SerializeField] private Transform playerDiscardDisplayArea; // Visual representation of discard

    [Header("Opponent Pet Area")]
    [SerializeField] private TextMeshProUGUI opponentPetNameText;
    [SerializeField] private TextMeshProUGUI opponentPetHealthText;
    // Opponent hand is typically hidden or shows card backs only
    [SerializeField] private Transform opponentPetHandDisplayArea; // Parent for opponent pet's card visuals (backs)

    [Header("Controls")]
    [SerializeField] private Button endTurnButton;

    private NetworkPlayer localPlayer;
    private NetworkPet opponentPetForLocalPlayer;

    private FightManager fightManager;
    private CombatManager combatManager;

    // Called by an RPC from CombatSetup (RpcTriggerCombatCanvasManagerSetup)
    public void SetupCombatUI()
    {
        Debug.Log("CombatCanvasManager: SetupCombatUI called.");

        fightManager = FindFirstObjectByType<FightManager>();
        combatManager = FindFirstObjectByType<CombatManager>(); // For end turn button

        if (fightManager == null)
        {
            Debug.LogError("FightManager not found by CombatCanvasManager.");
            return;
        }
        if (combatManager == null)
        {
            Debug.LogError("CombatManager not found by CombatCanvasManager.");
        }

        // Find the local player's NetworkObject
        // This requires knowing which NetworkPlayer is associated with the local client.
        // CustomNetworkManager or a PlayerIdentity system would typically provide this.
        var localConnection = FishNet.InstanceFinder.ClientManager.Connection;
        localPlayer = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None).FirstOrDefault(p => p.Owner == localConnection && p.IsOwner);

        if (localPlayer == null)
        {
            Debug.LogError("Local NetworkPlayer not found for UI setup.");
            if(FishNet.InstanceFinder.IsHostStarted && FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None).Length == 1){
                localPlayer = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None).First();
                Debug.LogWarning("Local player not found by Owner, defaulting to first player as Host.");
            }
            if (localPlayer == null) return;
        }

        Debug.Log($"Setting up UI for local player: {localPlayer.PlayerName}");

        // Find the opponent pet for the local player using FightManager
        opponentPetForLocalPlayer = fightManager.GetOpponentForPlayer(localPlayer);

        if (opponentPetForLocalPlayer == null)
        {
            Debug.LogWarning($"No opponent pet found for player {localPlayer.PlayerName} in FightManager. UI might be incomplete.");
        }

        InitializeUIReferences();
        SubscribeToEvents();

        if (endTurnButton != null && combatManager != null)
        {
            endTurnButton.onClick.AddListener(() => combatManager.CmdEndPlayerTurn(localPlayer.Owner));
        }
        else
        {
            Debug.LogError("End Turn Button or CombatManager not assigned in CombatCanvasManager.");
        }

        // Initial UI update
        UpdatePlayerUI(localPlayer);
        if (opponentPetForLocalPlayer != null) UpdateOpponentPetUI(opponentPetForLocalPlayer);
    }

    private void InitializeUIReferences()
    {
        // Ensure all UI Text and Transform references are assigned in the inspector.
        if (playerNameText == null) Debug.LogError("PlayerNameText not assigned.");
        if (playerHealthText == null) Debug.LogError("PlayerHealthText not assigned.");
        if (playerEnergyText == null) Debug.LogError("PlayerEnergyText not assigned.");
        if (playerHandDisplayArea == null) Debug.LogError("PlayerHandDisplayArea not assigned.");
        // ... and so on for other UI elements
    }

    private void SubscribeToEvents()
    {
        if (localPlayer != null)
        {
            // FishNet SyncVars automatically update, but if you need callbacks for changes:
            // localPlayer.CurrentHealth.OnChange += OnPlayerHealthChanged; // Error: int doesn't have OnChange
            // localPlayer.CurrentEnergy.OnChange += OnPlayerEnergyChanged; // Error: int doesn't have OnChange
            // localPlayer.PlayerName.OnChange += OnPlayerNameChanged;     // Error: string doesn't have OnChange
            // For SyncLists like playerHandCardIds, you'd subscribe to OnChange event of the SyncList itself.
            localPlayer.playerHandCardIds.OnChange += OnPlayerHandChanged;
        }

        if (opponentPetForLocalPlayer != null)
        {
            // opponentPetForLocalPlayer.CurrentHealth.OnChange += OnOpponentPetHealthChanged; // Error
            // opponentPetForLocalPlayer.PetName.OnChange += OnOpponentPetNameChanged;         // Error
            // opponentPetForLocalPlayer.playerHandCardIds.OnChange += OnOpponentHandChanged; // If displaying card backs
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        if (endTurnButton != null) endTurnButton.onClick.RemoveAllListeners();
    }

    private void UnsubscribeFromEvents()
    {
        if (localPlayer != null)
        {
            // localPlayer.CurrentHealth.OnChange -= OnPlayerHealthChanged;
            // localPlayer.CurrentEnergy.OnChange -= OnPlayerEnergyChanged;
            // localPlayer.PlayerName.OnChange -= OnPlayerNameChanged;
            localPlayer.playerHandCardIds.OnChange -= OnPlayerHandChanged;
        }
        if (opponentPetForLocalPlayer != null)
        { 
            // opponentPetForLocalPlayer.CurrentHealth.OnChange -= OnOpponentPetHealthChanged;
            // opponentPetForLocalPlayer.PetName.OnChange -= OnOpponentPetNameChanged;
            // opponentPetForLocalPlayer.playerHandCardIds.OnChange -= OnOpponentHandChanged;
        }
    }

    // --- UI Update Methods (called by event handlers or initial setup) ---
    private void UpdatePlayerUI(NetworkPlayer player) // This will now be called more frequently or manually
    {
        if (player == null) return;
        if (playerNameText != null) playerNameText.text = player.PlayerName.Value;
        if (playerHealthText != null) playerHealthText.text = $"Health: {player.CurrentHealth.Value}/{player.MaxHealth.Value}";
        if (playerEnergyText != null) playerEnergyText.text = $"Energy: {player.CurrentEnergy.Value}/{player.MaxEnergy.Value}";
        // Hand display update will be handled by OnPlayerHandChanged
    }

    private void UpdateOpponentPetUI(NetworkPet pet) // This will now be called more frequently or manually
    {
        if (pet == null) return;
        if (opponentPetNameText != null) opponentPetNameText.text = pet.PetName.Value;
        if (opponentPetHealthText != null) opponentPetHealthText.text = $"Health: {pet.CurrentHealth.Value}/{pet.MaxHealth.Value}";
    }

    // --- Event Handlers for SyncVar/SyncList Changes ---
    // Remove direct event handlers for primitive/string SyncVars for now
    // private void OnPlayerNameChanged(string prev, string next, bool asServer){ if (playerNameText != null) playerNameText.text = next; }
    // private void OnPlayerHealthChanged(int prev, int next, bool asServer) { if (playerHealthText != null) playerHealthText.text = $"Health: {next}/{localPlayer.MaxHealth}"; }
    // private void OnPlayerEnergyChanged(int prev, int next, bool asServer) { if (playerEnergyText != null) playerEnergyText.text = $"Energy: {next}/{localPlayer.MaxEnergy}"; }
    
    private void OnPlayerHandChanged(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
    {
        // This is where you would update the visual representation of the player's hand.
        // 'newItem' is the card ID (or whatever you store in playerHandCardIds).
        // You'd instantiate/destroy/update card GameObjects in playerHandDisplayArea.
        // This requires a Card Database or a way to get Card data from an ID.
        Debug.Log($"Player hand changed. Operation: {op}, Index: {index}, NewItem ID: {newItem}. Re-rendering hand...");
        RenderHand(localPlayer.playerHandCardIds, playerHandDisplayArea, true);
    }

    // private void OnOpponentPetNameChanged(string prev, string next, bool asServer){ if (opponentPetNameText != null) opponentPetNameText.text = next; }
    // private void OnOpponentPetHealthChanged(int prev, int next, bool asServer) { if (opponentPetHealthText != null) opponentPetHealthText.text = $"Health: {next}/{opponentPetForLocalPlayer.MaxHealth}"; }
    
    // You will likely need an Update() method in CombatCanvasManager to periodically call UpdatePlayerUI and UpdateOpponentPetUI
    // or call them after specific game events if SyncVar<T>.OnChange is not used for these fields.
    void Update() {
        if (localPlayer != null) UpdatePlayerUI(localPlayer);
        if (opponentPetForLocalPlayer != null) UpdateOpponentPetUI(opponentPetForLocalPlayer);
    }

    private void RenderHand(SyncList<int> cardIds, Transform handArea, bool isPlayerHand)
    {
        if (handArea == null) return;

        // Clear existing cards (simple approach)
        foreach (Transform child in handArea)
        {
            Destroy(child.gameObject);
        }

        // Get card prefab from CombatSetup or a ResourceManager
        CombatSetup combatSetup = FindFirstObjectByType<CombatSetup>();
        if (combatSetup == null || combatSetup.cardGamePrefab == null)
        {
            Debug.LogError("Cannot render hand: CombatSetup or cardGamePrefab not found.");
            return;
        }

        foreach (int cardId in cardIds)
        {
            // TODO: Get actual CardData from cardId using a CardDatabase or similar system.
            // For now, we'll just instantiate the generic cardGamePrefab and maybe set a text with ID.
            GameObject cardVisualInstance = Instantiate(combatSetup.cardGamePrefab, handArea);
            Card cardComponent = cardVisualInstance.GetComponent<Card>();
            if (cardComponent != null)
            {
                // This is where you would use cardId to fetch full card data (name, description, artwork etc.)
                // from a central CardData repository (e.g., ScriptableObject assets) and apply it to the card visuals.
                // cardComponent.InitializeFromCardData(CardDatabase.GetCard(cardId));
                // For now, a placeholder:
                cardComponent.name = $"Card_{cardId}";
                TextMeshProUGUI cardText = cardVisualInstance.GetComponentInChildren<TextMeshProUGUI>();
                if(cardText) cardText.text = $"ID: {cardId}"; // Simple display of ID
            }
            else
            {
                 TextMeshProUGUI cardText = cardVisualInstance.GetComponentInChildren<TextMeshProUGUI>();
                 if(cardText) cardText.text = $"ID: {cardId} (No Card Script)"; 
            }

            if (isPlayerHand)
            {
                // Add Button component or similar for click interaction if cards are playable by clicking
                Button cardButton = cardVisualInstance.GetComponent<Button>();
                if (cardButton == null) cardButton = cardVisualInstance.AddComponent<Button>();
                cardButton.onClick.AddListener(() => OnPlayerCardClicked(cardId, cardComponent));
            }
            else
            {
                // Opponent's cards - might just show card backs, no interaction
                // Potentially disable interaction or change appearance
            }
        }
    }

    private void OnPlayerCardClicked(int cardId, Card cardReference)
    {
        if (localPlayer == null || combatManager == null)
        {
            Debug.LogError("Cannot play card: LocalPlayer or CombatManager is null.");
            return;
        }

        // Check if it's the local player's turn (CombatManager should track this)
        if (!combatManager.IsPlayerTurn(localPlayer))
        {
            Debug.Log("Not your turn!");
            return;
        }

        // Check for energy cost
        // CardData cardData = CardDatabase.GetCard(cardId); // Get full card data
        // For now, using the cardReference if it has cost.
        if (cardReference == null) {
            Debug.LogError($"Card reference for ID {cardId} is null.");
            return;
        }

        if (localPlayer.CurrentEnergy.Value < cardReference.EnergyCost)
        {
            Debug.Log("Not enough energy to play this card!");
            return;
        }
        
        Debug.Log($"Player clicked on card with ID: {cardId}. Requesting to play.");
        // Tell CombatManager the player wants to play this card.
        // The target will be determined by FightManager via CombatManager.
        combatManager.CmdPlayerRequestsPlayCard(localPlayer.Owner, cardId);
    }

    public void SetEndTurnButtonInteractable(bool interactable)
    {
        if (endTurnButton != null) endTurnButton.interactable = interactable;
    }
} 