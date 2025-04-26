using UnityEngine;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;

public class CombatTurnManager
{
    private GameManager gameManager;
    private CombatUIManager uiManager;
    private int startingPlayerEnergy;
    private int startingPetEnergy;
    private int currentTurnNumber = 0;
    private int currentCombatTurn = 0;
    
    public CombatTurnManager(GameManager gameManager, CombatUIManager uiManager, int startingPlayerEnergy, int startingPetEnergy)
    {
        this.gameManager = gameManager;
        this.uiManager = uiManager;
        this.startingPlayerEnergy = startingPlayerEnergy;
        this.startingPetEnergy = startingPetEnergy;
    }
    
    public void StartTurn()
    {
        Debug.Log("Starting Player Turn");
        PlayerManager playerManager = gameManager.GetPlayerManager();

        // Initialize turn number if it's the first turn
        if (currentCombatTurn == 0) 
        {
            currentCombatTurn = 1;
        }
        
        // Reset combo counter at the start of the turn
        playerManager.ResetComboCount();

        // Process turn start effects (like DoT) and decrement buffs/debuffs for the PLAYER ONLY
        playerManager.ProcessPlayerTurnStartEffects();
        
        // Check for deaths after DoT
        if (playerManager.IsCombatEndedForLocalPlayer()) return; // End turn early if player/pet died

        // Reset energy
        playerManager.SetCurrentEnergy(startingPlayerEnergy);
        uiManager.UpdateEnergyUI();

        // Reset Opponent Pet Energy at Player Turn Start
        gameManager.GetCardManager().GetPetDeckManager().SetOpponentPetEnergy(startingPetEnergy);
        Debug.Log($"Opponent Pet Energy RESET to {startingPetEnergy} at start of player turn.");
        uiManager.UpdateHealthUI(); // Ensure opponent energy UI updates
        
        // Also notify the actual owner via RPC to update their property
        Player opponentPlayer = playerManager.GetOpponentPlayer();
        if (PhotonNetwork.InRoom && opponentPlayer != null)
        {
            Debug.Log($"Sending RpcResetMyPetEnergyProp({startingPetEnergy}) to opponent {opponentPlayer.NickName}.");
            gameManager.GetPhotonView()?.RPC("RpcResetMyPetEnergyProp", opponentPlayer, startingPetEnergy);
        }
        else if (opponentPlayer == null)
        {
            Debug.LogWarning("StartTurn: Cannot send RpcResetMyPetEnergyProp, opponentPlayer is null.");
        }

        // Increment Turn and Publish Status
        currentTurnNumber++;
        PublishCombatStatus();

        // Draw Opponent Pet Hand
        Debug.Log("CombatTurnManager.StartTurn: Drawing opponent pet's hand.");
        int petCardsToDraw = 5; // Assuming same draw count as player
        gameManager.GetCardManager().GetPetDeckManager().DrawOpponentPetHand(petCardsToDraw);
        // UI is updated within DrawOpponentPetHand

        // Draw player cards
        gameManager.GetCardManager().DrawHand();
        uiManager.UpdateHandUI();
        uiManager.UpdateDeckCountUI();

        // Make cards playable and enable end turn button
        uiManager.SetEndTurnButtonInteractable(true);
        
        uiManager.UpdateHealthUI(); // Update all health-related UI
        
        // Update Other Fights UI
        uiManager.UpdateOtherFightsUI();
    }
    
    public IEnumerator EndTurn()
    {
        Debug.Log("Ending Player Turn");
        uiManager.SetEndTurnButtonInteractable(false); // Prevent double clicks

        // --- INCREMENT COMBAT TURN --- 
        currentCombatTurn++;
        // --- END INCREMENT ---

        // Process End-of-Turn Hand Effects (Temp Upgrades, etc.)
        gameManager.GetCardManager().ProcessEndOfTurnHandEffects();

        // Discard Hand
        gameManager.GetCardManager().DiscardHand();
        uiManager.UpdateHandUI(); // Clear hand display
        uiManager.UpdateDeckCountUI();

        // Reset Opponent Pet Block before their action phase
        Debug.Log("Resetting Opponent Pet Block before their action phase.");
        gameManager.GetPlayerManager().GetHealthManager().ResetOpponentPetBlockOnly();
        uiManager.UpdateHealthUI(); // Update UI immediately to show zero block

        // --- ADDED: Decrement Player Crit Buffs at end of player turn ---
        gameManager.GetPlayerManager()?.GetHealthManager()?.DecrementPlayerCritBuffDurations();
        // --- ADDED: Decrement Player Strength at end of player turn ---
        gameManager.GetPlayerManager()?.GetStatusEffectManager()?.DecrementPlayerStrengthAtTurnEnd();
        uiManager.UpdateHealthUI(); // Update UI after decrementing player buffs
        // --- END ADDED ---

        // Opponent Pet Acts (with visualization)
        Debug.Log("Starting Opponent Pet Action Phase...");
        IEnumerator petTurnEnumerator = gameManager.GetCardManager().ExecuteOpponentPetTurn(startingPetEnergy);
        while (petTurnEnumerator.MoveNext()) {
            object currentYield = petTurnEnumerator.Current;
            Debug.Log($"[CombatTurnManager.EndTurn Loop] Yielded object Type: {(currentYield?.GetType()?.Name ?? "null")}");
            
            if (currentYield is CardData cardPlayed) {
                // 1. Visualize
                Debug.Log($"CombatTurnManager starting visualization for yielded card: {cardPlayed.cardName}");
                yield return gameManager.StartCoroutine(uiManager.VisualizeOpponentPetCardPlay(cardPlayed));
                Debug.Log($"CombatTurnManager finished visualization for yielded card: {cardPlayed.cardName}");

                // Notify Pet Owner about card play for energy decrement
                Player opponentPetOwner = gameManager.GetPlayerManager().GetOpponentPlayer();
                if (PhotonNetwork.InRoom && opponentPetOwner != null)
                {
                    Debug.Log($"Sending RpcOpponentPlayedCard(Cost={cardPlayed.cost}) to owner {opponentPetOwner.NickName}.");
                    gameManager.GetPhotonView()?.RPC("RpcOpponentPlayedCard", opponentPetOwner, cardPlayed.cost);
                }
                else if (opponentPetOwner == null) {
                    Debug.LogWarning("EndTurn: Cannot send RpcOpponentPlayedCard, opponentPetOwner is null.");
                }

                // 2. Apply Effects AFTER animation
                Debug.Log($"CombatTurnManager applying effects for: {cardPlayed.cardName}");
                gameManager.GetCardManager().ProcessOpponentPetCardEffect(cardPlayed);
                
                // 3. Update UI AFTER effects
                uiManager.UpdateHealthUI(); // Reflect health/block changes
            }
            else if (currentYield != null)
            {
                 // Handle other potential yields (delays, etc.) if needed
                 yield return currentYield;
            }
            else
            {
                yield return null; // Yield null if the enumerator yields null
            }
        }
        Debug.Log("...Opponent Pet Action Phase Finished");

        // --- ADDED: Decrement Opponent Pet Strength after their turn ---
        gameManager.GetPlayerManager()?.GetStatusEffectManager()?.DecrementOpponentPetStrengthAtTurnEnd();
        // --- END ADDED ---

        uiManager.UpdateHealthUI(); // Update UI after all actions

        // Reset player block AFTER opponent acts
        gameManager.GetPlayerManager().GetHealthManager().ResetPlayerBlockOnly();

        // Start Next Player Turn (if combat hasn't ended)
        if (!gameManager.GetPlayerManager().IsCombatEndedForLocalPlayer())
        {
            StartTurn();
        }
    }
    
    private void PublishCombatStatus()
    {
        if (!PhotonNetwork.InRoom) return; // Safety check

        int opponentPetHealth = gameManager.GetPlayerManager().GetOpponentPetHealth(); 
        int playerHealth = gameManager.GetPlayerManager().GetLocalPlayerHealth();
        
        ExitGames.Client.Photon.Hashtable combatProps = new ExitGames.Client.Photon.Hashtable
        {
            { CombatStateManager.PLAYER_COMBAT_OPP_PET_HP_PROP, opponentPetHealth },
            { CombatStateManager.PLAYER_COMBAT_TURN_PROP, currentTurnNumber },
            { CombatStateManager.PLAYER_COMBAT_PLAYER_HP_PROP, playerHealth }
        };
        
        PhotonNetwork.LocalPlayer.SetCustomProperties(combatProps);
    }
    
    public bool IsPlayerTurn()
    {
        return !gameManager.GetPlayerManager().IsCombatEndedForLocalPlayer();
    }
    
    public int GetCurrentTurnNumber()
    {
        return currentTurnNumber;
    }

    public int GetCurrentCombatTurn()
    {
        return currentCombatTurn;
    }
} 