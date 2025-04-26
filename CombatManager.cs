using UnityEngine;
using System.Collections;
using Photon.Pun;

public class CombatManager
{
    private GameManager gameManager;
    private int startingPlayerHealth;
    private int startingPetHealth;
    private int startingPlayerEnergy;
    private int startingPetEnergy;

    // Helper managers
    private CombatUIManager uiManager;
    private CombatTurnManager turnManager;
    private DeckViewManager deckViewManager;

    public void Initialize(GameManager gameManager, int startingPlayerHealth, int startingPetHealth, int startingPlayerEnergy, int startingPetEnergy, CombatUIManager uiManager = null)
    {
        this.gameManager = gameManager;
        this.startingPlayerHealth = startingPlayerHealth;
        this.startingPetHealth = startingPetHealth;
        this.startingPlayerEnergy = startingPlayerEnergy;
        this.startingPetEnergy = startingPetEnergy;
        
        // Use provided UIManager or create a new one if not provided
        this.uiManager = uiManager ?? new CombatUIManager(gameManager);
        turnManager = new CombatTurnManager(gameManager, this.uiManager, startingPlayerEnergy, startingPetEnergy);
        deckViewManager = new DeckViewManager(gameManager);
    }

    public void InitializeCombatScreenReferences(GameObject combatInstance)
    {
        if (combatInstance == null) return;

        // Initialize UI references in helper managers
        uiManager.InitializeUIReferences(combatInstance);
        deckViewManager.InitializeReferences(combatInstance);
        
        // Assign listeners for end turn button
        uiManager.GetEndTurnButton()?.onClick.RemoveAllListeners();
        uiManager.GetEndTurnButton()?.onClick.AddListener(() => gameManager.StartCoroutine(EndTurn()));
    }

    public void InitializeCombatState(int opponentPetOwnerActorNum)
    {
        gameManager.GetPlayerManager().InitializeCombatState(opponentPetOwnerActorNum, startingPlayerHealth, startingPetHealth);
        gameManager.GetCardManager().InitializeDecks();
        
        // Set opponent pet energy for local simulation
        gameManager.GetCardManager().GetPetDeckManager().SetOpponentPetEnergy(startingPetEnergy);
        
        // Setup Initial UI
        uiManager.InitializeUIState();
        uiManager.UpdateHealthUI();
        uiManager.UpdateDeckCountUI();
        
        // Start the first turn
        StartTurn();
        
        // Update other UI elements
        uiManager.UpdateOtherFightsUI();
        uiManager.UpdateHealthUI();
    }

    public void StartTurn()
    {
        turnManager.StartTurn();
    }

    public IEnumerator EndTurn()
    {
        return turnManager.EndTurn();
    }

    public void UpdateHealthUI()
    {
        uiManager.UpdateHealthUI();
    }

    public void UpdateHandUI()
    {
        uiManager.UpdateHandUI();
    }

    public void UpdateDeckCountUI()
    {
        uiManager.UpdateDeckCountUI();
    }

    public void UpdateEnergyUI()
    {
        uiManager.UpdateEnergyUI();
    }

    public bool IsPlayerTurn()
    {
        return turnManager.IsPlayerTurn();
    }

    public bool AttemptPlayCard(CardData cardData, CardDropZone.TargetType targetType, GameObject cardGO = null)
    {
        return gameManager.GetCardManager().AttemptPlayCard(cardData, targetType, cardGO);
    }

    public void DisableEndTurnButton()
    {
        uiManager.SetEndTurnButtonInteractable(false);
    }

    public GameObject GetPlayerHandPanel()
    {
        return uiManager.GetPlayerHandPanel();
    }

    public CombatUIManager GetCombatUIManager()
    {
        return uiManager;
    }

    public CombatTurnManager GetCombatTurnManager()
    {
        return turnManager;
    }

    public IEnumerator ExecuteOpponentPetTurn()
    {
        // Pass this to the card manager's coroutine and yield its result
        return gameManager.GetCardManager().ExecuteOpponentPetTurn(startingPetEnergy);
    }
}