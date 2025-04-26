using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CardModificationService
{
    private GameManager gameManager;
    
    // Tracking for Temporary Upgrades
    // Key: The *instance* of the upgraded card currently in hand
    // Value: Info about its original state and duration
    private Dictionary<CardData, TempUpgradeInfo> tempUpgradedCardsInHand = new Dictionary<CardData, TempUpgradeInfo>();
    
    private struct TempUpgradeInfo
    {
        public CardData originalCard;
        public int durationTurns; // 0 = combat long, >0 = turns remaining
    }
    
    public CardModificationService(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public void ApplyTemporaryHandUpgrade(int cardCount, int duration, UpgradeTargetRule targetRule)
    {
        DeckManager deckManager = gameManager.GetCardManager().GetDeckManager();
        List<CardData> hand = deckManager.GetHand();
        
        if (cardCount <= 0 || hand.Count == 0) return;

        Debug.Log($"Attempting to apply temporary hand upgrade: {cardCount} cards for {duration} turns, targeting {targetRule}");

        // 1. Identify potential candidates in hand (not already temp upgraded, has upgrade path)
        List<CardData> candidates = hand.Where(card => 
            card.upgradedVersion != null && 
            !tempUpgradedCardsInHand.ContainsKey(card) && // Exclude cards already temp upgraded
            !tempUpgradedCardsInHand.Any(kvp => kvp.Value.originalCard == card) // Exclude cards that *are* temp upgrades of something else
        ).ToList();

        if (candidates.Count == 0) 
        {
            Debug.Log("No valid cards in hand to temporarily upgrade.");
            return;
        }

        // 2. Select target cards based on rule
        List<CardData> cardsToUpgrade = new List<CardData>();
        System.Random rng = new System.Random();

        switch (targetRule)
        {
            case UpgradeTargetRule.Random:
                cardsToUpgrade = candidates.OrderBy(x => rng.Next()).Take(Mathf.Min(cardCount, candidates.Count)).ToList();
                break;
            case UpgradeTargetRule.Cheapest:
                cardsToUpgrade = candidates.OrderBy(card => card.cost).Take(Mathf.Min(cardCount, candidates.Count)).ToList();
                break;
            case UpgradeTargetRule.MostExpensive:
                cardsToUpgrade = candidates.OrderByDescending(card => card.cost).Take(Mathf.Min(cardCount, candidates.Count)).ToList();
                break;
        }

        // 3. Perform the upgrade and track
        foreach (CardData originalCard in cardsToUpgrade)
        {
            int handIndex = hand.IndexOf(originalCard);
            if (handIndex != -1)
            {
                CardData upgradedCard = originalCard.upgradedVersion;
                hand[handIndex] = upgradedCard;
                
                // Track it - Key is the *new* upgraded card instance in hand
                TempUpgradeInfo info = new TempUpgradeInfo { originalCard = originalCard, durationTurns = duration };
                tempUpgradedCardsInHand[upgradedCard] = info;

                Debug.Log($"Temporarily upgraded hand card '{originalCard.cardName}' to '{upgradedCard.cardName}' at index {handIndex}. Duration: {duration} turns.");
            }
            else
            {
                Debug.LogWarning($"Could not find card '{originalCard.cardName}' in hand to upgrade (was it removed concurrently?)");
            }
        }

        // 4. Update UI
        if (cardsToUpgrade.Count > 0)
        {
            gameManager.UpdateHandUI();
        }
    }
    
    public void ProcessEndOfTurnHandEffects()
    {
        RevertExpiredHandUpgrades(false); // Decrement durations and revert if needed
    }

    public void RevertExpiredHandUpgrades(bool forceRevertAll)
    {
        if (tempUpgradedCardsInHand.Count == 0) return;

        Debug.Log($"Processing end-of-turn hand upgrades. Force Revert All: {forceRevertAll}");
        List<CardData> upgradedCardsToRemove = new List<CardData>();
        List<KeyValuePair<int, CardData>> cardsToRevertInHand = new List<KeyValuePair<int, CardData>>();
        
        DeckManager deckManager = gameManager.GetCardManager().GetDeckManager();
        List<CardData> hand = deckManager.GetHand();

        // Create a temporary list of keys to iterate over, allowing modification of the dictionary
        List<CardData> currentUpgradedKeys = new List<CardData>(tempUpgradedCardsInHand.Keys);

        foreach (CardData upgradedCard in currentUpgradedKeys)
        {
            if (tempUpgradedCardsInHand.TryGetValue(upgradedCard, out TempUpgradeInfo info))
            {
                bool revert = forceRevertAll;
                if (!forceRevertAll && info.durationTurns > 0) // Only decrement if duration is turn-based
                {
                    info.durationTurns--;
                    tempUpgradedCardsInHand[upgradedCard] = info; // Update duration in dictionary
                    Debug.Log($"Decremented temp upgrade duration for {upgradedCard.cardName}. Turns remaining: {info.durationTurns}");
                    if (info.durationTurns == 0)
                    {
                        revert = true;
                    }
                }
                else if (!forceRevertAll && info.durationTurns == 0) // Combat-long effect, don't revert unless forced
                {
                    // Do nothing, combat-long effect persists until forced revert
                }
                else if (info.durationTurns < 0) // Should not happen, but handle defensively
                {
                    Debug.LogWarning($"Temp upgrade info for {upgradedCard.cardName} had negative duration ({info.durationTurns}), forcing revert.");
                    revert = true; 
                }

                if (revert)
                {
                    // Find the card in the actual hand list to replace it
                    int handIndex = hand.IndexOf(upgradedCard);
                    if (handIndex != -1)
                    {
                        Debug.Log($"Reverting temporary upgrade: '{upgradedCard.cardName}' back to '{info.originalCard.cardName}' in hand.");
                        // Schedule the revert to avoid modifying 'hand' while potentially iterating elsewhere
                        cardsToRevertInHand.Add(new KeyValuePair<int, CardData>(handIndex, info.originalCard));
                        upgradedCardsToRemove.Add(upgradedCard); // Mark for removal from tracking dict
                    }
                    else
                    {
                        // Card is no longer in hand (discarded?), just remove from tracking
                        Debug.Log($"Temp upgraded card '{upgradedCard.cardName}' not found in hand during revert check (likely discarded). Removing tracking.");
                        upgradedCardsToRemove.Add(upgradedCard);
                    }

                    // Remove cost modifier for the *upgraded* card instance being removed/reverted
                    gameManager.GetPlayerManager().RemoveCostModifierForCard(upgradedCard);
                }
            }
        }

        // Perform the scheduled reverts in the hand
        foreach (var revertPair in cardsToRevertInHand)
        {
            if (revertPair.Key >= 0 && revertPair.Key < hand.Count) // Double check index validity
            {
                hand[revertPair.Key] = revertPair.Value; // Revert card in hand
            }
        }

        // Remove expired entries from the tracking dictionary
        foreach (CardData keyToRemove in upgradedCardsToRemove)
        {
            tempUpgradedCardsInHand.Remove(keyToRemove);
        }

        // Update UI if any reverts happened
        if (cardsToRevertInHand.Count > 0)
        {
            gameManager.UpdateHandUI();
        }
    }
    
    public bool IsCardTemporarilyUpgraded(CardData cardInstanceInHand)
    {
        return tempUpgradedCardsInHand.ContainsKey(cardInstanceInHand);
    }
}