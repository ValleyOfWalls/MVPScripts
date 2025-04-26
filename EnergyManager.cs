using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime; // Add this for Player type
using ExitGames.Client.Photon; // Added for Hashtable

public class EnergyManager
{
    private GameManager gameManager;
    
    private int currentEnergy;
    private int startingEnergy;
    
    // --- ADDED: Opponent Player Simulation Data ---
    private int opponentPlayerEnergy = 0; // Initial value, will be set by properties
    // --- END ADDED ---
    
    // Card Cost Modification
    private struct CostModifierInfo
    {
        public int amount;
        public int durationTurns;
    }
    
    private Dictionary<CardData, CostModifierInfo> cardCostModifiers = new Dictionary<CardData, CostModifierInfo>();
    
    // Structure to hold cost modification details
    private struct CostModifier
    {
        public int amount; // The change in cost (+/-)
        public int duration; // Turns remaining
        public int cardCount; // How many cards it applies to (-1 for all)
        public bool appliedToOpponent; // Was this modifier sent *to* the opponent?
        public List<CardData> affectedCards; // Specific cards affected (if count > 0)
    }
    // List to track active cost modifiers
    private List<CostModifier> activeCostModifiers = new List<CostModifier>();
    
    public EnergyManager(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public void Initialize(int startingEnergy)
    {
        this.startingEnergy = startingEnergy;
        this.currentEnergy = startingEnergy;
        
        // Reset cost modifiers
        cardCostModifiers.Clear();
        
        // --- ADDED: Reset Opponent Player Sim ---
        this.opponentPlayerEnergy = startingEnergy; // Assume symmetric start
        // --- END ADDED ---
        
        // --- ADDED: Set Initial Property ---
        UpdatePlayerEnergyProperty();
        // --- END ADDED ---
    }
    
    #region Energy Methods
    
    public void ConsumeEnergy(int amount)
    {
        if (amount > 0)
        {
            currentEnergy -= amount;
            if (currentEnergy < 0) currentEnergy = 0; // Prevent negative energy
            Debug.Log($"Consumed {amount} energy. Remaining: {currentEnergy}");
            gameManager.UpdateEnergyUI();
            
            // --- ADDED: Update Property ---
            UpdatePlayerEnergyProperty();
            // --- END ADDED ---
        }
    }
    
    public void GainEnergy(int amount)
    {
        if (amount > 0)
        {
            currentEnergy += amount;
            // Optional: Add a cap if needed
            // if (currentEnergy > startingEnergy * 2) currentEnergy = startingEnergy * 2;
            Debug.Log($"Gained {amount} energy. New total: {currentEnergy}");
            gameManager.UpdateEnergyUI();
            
            // --- ADDED: Update Property ---
            UpdatePlayerEnergyProperty();
            // --- END ADDED ---
        }
    }
    
    #endregion
    
    #region Cost Modifier Methods
    
    public void ApplyCostModifierToOpponentHand(int amount, int duration, int cardCount)
    {
        if (duration <= 0) return;
        Debug.Log($"Sending Cost Modifier to Opponent: Amount={amount}, Duration={duration}, Count={cardCount}");

        // Send RPC to opponent
        Player opponentPlayer = gameManager.GetPlayerManager()?.GetOpponentPlayer();
        if (PhotonNetwork.InRoom && opponentPlayer != null)
        {
            gameManager.GetPhotonView()?.RPC("RpcApplyHandCostModifier", opponentPlayer, amount, duration, cardCount);
        }
    }
    
    public void ApplyCostModifierToLocalHand(int amount, int duration, int cardCount)
    {
        if (duration <= 0) return;
        Debug.Log($"Received Cost Modifier: Amount={amount}, Duration={duration}, Count={cardCount}");

        activeCostModifiers.Add(new CostModifier 
        { 
            amount = amount, 
            duration = duration, 
            cardCount = cardCount, 
            appliedToOpponent = false, // This affects *our* hand
            affectedCards = new List<CardData>()
        });
        
        // Trigger a hand UI update to reflect potential cost changes
        gameManager.UpdateHandUI();
    }
    
    public int GetLocalHandCostModifier(CardData card)
    {
        int totalModifier = 0;
        foreach (var modifier in activeCostModifiers)
        {
            // Check if modifier applies to all cards OR this specific card is affected
            if (modifier.cardCount == -1 || (modifier.affectedCards != null && modifier.affectedCards.Contains(card)))
            {
                 totalModifier += modifier.amount;
            }
        }
        return totalModifier;
    }
    
    public void RemoveCostModifierForCard(CardData card)
    { 
        if (card == null) return;
        
        bool handNeedsUpdate = false;
        for (int i = activeCostModifiers.Count - 1; i >= 0; i--)
        {
            CostModifier mod = activeCostModifiers[i];
            if (mod.affectedCards != null && mod.affectedCards.Contains(card))
            {
                mod.affectedCards.Remove(card);
                 // If the modifier had a specific count and we just removed the last affected card, 
                 // we could potentially remove the modifier itself, but the duration check handles expiration.
                activeCostModifiers[i] = mod; // Update the list
                handNeedsUpdate = true; // Cost might change immediately
                Debug.Log($"Removed card {card.cardName} from cost modifier effect (Amount: {mod.amount}).");
            }
        }
        
        if (handNeedsUpdate)
        {
            gameManager.UpdateHandUI();
        }
    }
    
    public void DecrementCostModifiers()
    {
        bool handNeedsUpdate = false;
        // Iterate backwards to allow removal
        for (int i = activeCostModifiers.Count - 1; i >= 0; i--)
        {
            CostModifier modifier = activeCostModifiers[i];
            modifier.duration--;

            if (modifier.duration <= 0)
            {
                Debug.Log($"Cost Modifier expired: Amount={modifier.amount}, Count={modifier.cardCount}");
                activeCostModifiers.RemoveAt(i);
                handNeedsUpdate = true; // Costs will change
            }
            else
            {
                activeCostModifiers[i] = modifier; // Update duration in the list
            }
        }
        
        if (handNeedsUpdate)
        {
             // Update hand UI to reflect expired modifiers
             gameManager.UpdateHandUI();
        }
    }
    
    #endregion
    
    #region Getters and Setters
    
    public int GetCurrentEnergy() => currentEnergy;
    public void SetCurrentEnergy(int energy) => currentEnergy = energy;
    public int GetStartingEnergy() => startingEnergy;
    public void SetStartingEnergy(int energy) => startingEnergy = energy;
    
    // --- ADDED: Opponent Player Getters/Setters ---
    public int GetOpponentPlayerEnergy() => opponentPlayerEnergy;
    public void SetOpponentPlayerEnergy(int value) 
    {
        opponentPlayerEnergy = value;
        // Optionally update UI elements that might show opponent energy, if any
        // gameManager.UpdateHealthUI(); // Or a more specific UI update if needed
    }
    // --- END ADDED ---
    
    #endregion
    
    // --- ADDED: Helper to update player energy property ---
    private void UpdatePlayerEnergyProperty()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;

        Hashtable propsToSet = new Hashtable
        {
            { CombatStateManager.PLAYER_COMBAT_ENERGY_PROP, currentEnergy }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(propsToSet);
        // Debug.Log($"Updated player energy property to {currentEnergy}");
    }
    // --- END ADDED ---
}