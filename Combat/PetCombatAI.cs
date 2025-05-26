using UnityEngine;
using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Handles AI behavior for pets during combat
/// Attach to: NetworkEntity prefabs of type Pet
/// </summary>
public class PetCombatAI : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkEntity petEntity;
    [SerializeField] private HandManager handManager;

    [Header("AI Settings")]
    [SerializeField] private float delayBetweenActions = 1.0f;
    [SerializeField] private float delayBeforeFirstAction = 0.5f;

    // Track turn state
    private bool hasFinishedTurn = false;
    public bool HasFinishedTurn => hasFinishedTurn;

    private void Awake()
    {
        // Get required components
        if (petEntity == null) petEntity = GetComponent<NetworkEntity>();
        if (handManager == null) handManager = GetComponent<HandManager>();

        ValidateComponents();
    }

    private void ValidateComponents()
    {
        if (petEntity == null)
            Debug.LogError($"PetCombatAI on {gameObject.name}: Missing NetworkEntity component");
        if (handManager == null)
            Debug.LogError($"PetCombatAI on {gameObject.name}: Missing HandManager component");
    }

    /// <summary>
    /// Executes the pet's turn in combat
    /// </summary>
    [Server]
    public IEnumerator TakeTurn()
    {
        if (!IsServerInitialized) yield break;

        Debug.Log($"PetCombatAI: {petEntity.EntityName.Value} starting turn");
        hasFinishedTurn = false;

        // Small delay before starting
        yield return new WaitForSeconds(delayBeforeFirstAction);

        // Get all cards in hand
        Transform handTransform = handManager.GetHandTransform();
        if (handTransform == null)
        {
            Debug.LogError($"PetCombatAI: Cannot find hand transform for {petEntity.EntityName.Value}");
            hasFinishedTurn = true;
            yield break;
        }

        List<GameObject> cardsInHand = GetCardsInHand(handTransform);
        Debug.Log($"PetCombatAI: {petEntity.EntityName.Value} has {cardsInHand.Count} cards in hand");

        // If no cards in hand, end turn
        if (cardsInHand.Count == 0)
        {
            Debug.Log($"PetCombatAI: {petEntity.EntityName.Value} has no cards to play");
            hasFinishedTurn = true;
            yield break;
        }

        // Get opponent (target for most cards)
        NetworkEntity opponentEntity = GetOpponentEntity();
        if (opponentEntity == null)
        {
            Debug.LogError($"PetCombatAI: Cannot find opponent for {petEntity.EntityName.Value}");
            hasFinishedTurn = true;
            yield break;
        }

        // Sort cards by priority for AI to play
        List<GameObject> sortedCards = GetSortedCardsByPriority(cardsInHand, opponentEntity);

        // Play cards until out of energy or cards
        int remainingEnergy = petEntity.CurrentEnergy.Value;
        Debug.Log($"PetCombatAI: {petEntity.EntityName.Value} starting with {remainingEnergy} energy");

        foreach (GameObject cardObject in sortedCards)
        {
            Card card = cardObject.GetComponent<Card>();
            if (card == null || card.CardData == null)
            {
                Debug.LogError($"PetCombatAI: Invalid card in hand for {petEntity.EntityName.Value}");
                continue;
            }

            // Check if we have enough energy to play this card
            if (card.CardData.EnergyCost > remainingEnergy)
            {
                Debug.Log($"PetCombatAI: Not enough energy to play {card.CardData.CardName}. Cost: {card.CardData.EnergyCost}, Available: {remainingEnergy}");
                continue;
            }

            // Play the card
            Debug.Log($"PetCombatAI: {petEntity.EntityName.Value} playing card {card.CardData.CardName}");

            // Prepare card for play by setting up source and target
            SourceAndTargetIdentifier sourceTarget = cardObject.GetComponent<SourceAndTargetIdentifier>();
            if (sourceTarget != null)
            {
                // Force update source and target instead of relying on hover
                sourceTarget.ForceUpdateSourceAndTarget(petEntity, opponentEntity);
            }
            else
            {
                Debug.LogError($"PetCombatAI: Card {card.CardData.CardName} missing SourceAndTargetIdentifier");
                continue;
            }

            // Trigger the card play
            HandleCardPlay cardPlayHandler = cardObject.GetComponent<HandleCardPlay>();
            if (cardPlayHandler != null)
            {
                // Call the ServerPlayCard method
                cardPlayHandler.ServerPlayCard();
                
                // Update remaining energy
                remainingEnergy -= card.CardData.EnergyCost;
                Debug.Log($"PetCombatAI: After playing card, {petEntity.EntityName.Value} has {remainingEnergy} energy left");
                
                // Add delay between card plays
                yield return new WaitForSeconds(delayBetweenActions);
            }
            else
            {
                Debug.LogError($"PetCombatAI: Card {card.CardData.CardName} missing HandleCardPlay");
                continue;
            }
        }

        // Final delay before ending turn
        yield return new WaitForSeconds(delayBetweenActions);
        
        Debug.Log($"PetCombatAI: {petEntity.EntityName.Value} finished turn");
        hasFinishedTurn = true;
    }

    /// <summary>
    /// Resets the turn state when a new round begins
    /// </summary>
    [Server]
    public void ResetTurnState()
    {
        if (!IsServerInitialized) return;
        hasFinishedTurn = false;
    }

    /// <summary>
    /// Gets all cards in the pet's hand
    /// Note: Uses logical state (transform hierarchy) rather than visual state (activeSelf)
    /// since AI should be able to play cards regardless of client visibility filtering
    /// </summary>
    private List<GameObject> GetCardsInHand(Transform handTransform)
    {
        List<GameObject> cards = new List<GameObject>();
        for (int i = 0; i < handTransform.childCount; i++)
        {
            Transform child = handTransform.GetChild(i);
            if (child != null && child.gameObject != null)
            {
                cards.Add(child.gameObject);
            }
        }
        return cards;
    }

    /// <summary>
    /// Gets the opponent entity for this pet (usually the player)
    /// </summary>
    private NetworkEntity GetOpponentEntity()
    {
        // Find FightManager to get the opponent
        FightManager fightManager = FindFirstObjectByType<FightManager>();
        if (fightManager != null)
        {
            return fightManager.GetOpponentForPet(petEntity);
        }
        return null;
    }

    /// <summary>
    /// Sorts cards by priority for the AI to play
    /// </summary>
    private List<GameObject> GetSortedCardsByPriority(List<GameObject> cardsInHand, NetworkEntity opponent)
    {
        // Simple priority system:
        // 1. High damage cards when opponent has low health
        // 2. Healing cards when pet has low health
        // 3. Buff/status effect cards early in fight
        // 4. Default to damage cards in order of damage/energy efficiency

        List<GameObject> sortedCards = new List<GameObject>(cardsInHand);
        
        // Sort by custom priority
        sortedCards.Sort((a, b) => {
            Card cardA = a.GetComponent<Card>();
            Card cardB = b.GetComponent<Card>();
            
            if (cardA == null || cardB == null || cardA.CardData == null || cardB.CardData == null)
                return 0;
                
            return GetCardPriority(cardB, opponent) - GetCardPriority(cardA, opponent);
        });
        
        return sortedCards;
    }

    /// <summary>
    /// Calculates priority score for a card based on current game state
    /// </summary>
    private int GetCardPriority(Card card, NetworkEntity opponent)
    {
        if (card == null || card.CardData == null)
            return 0;
            
        int priority = 0;
        float opponentHealthPercent = (float)opponent.CurrentHealth.Value / opponent.MaxHealth.Value;
        float petHealthPercent = (float)petEntity.CurrentHealth.Value / petEntity.MaxHealth.Value;
        
        // Base priority on card type
        switch (card.CardData.EffectType)
        {
            case CardEffectType.Damage:
                // Higher priority when opponent is low
                priority = 50 + card.CardData.Amount;
                if (opponentHealthPercent < 0.3f)
                    priority += 30;
                else if (opponentHealthPercent < 0.5f)
                    priority += 20;
                break;
                
            case CardEffectType.Heal:
                // Higher priority when pet is low on health
                priority = 30 + card.CardData.Amount;
                if (petHealthPercent < 0.3f)
                    priority += 40;
                else if (petHealthPercent < 0.5f)
                    priority += 20;
                else
                    priority -= 20; // Lower priority if health is high
                break;
                
            case CardEffectType.BuffStats:
                // Buffs are good early in the fight
                priority = 40;
                break;
                
            case CardEffectType.DebuffStats:
            case CardEffectType.ApplyStatus:
                // Status effects are good early in the fight
                priority = 45;
                break;
                
            case CardEffectType.DrawCard:
                // Draw cards are good when we have low cards and energy to play them
                priority = 35 + (petEntity.CurrentEnergy.Value * 5);
                break;
                
            default:
                priority = 30;
                break;
        }
        
        // Adjust for energy efficiency
        if (card.CardData.EnergyCost > 0)
        {
            priority = priority * card.CardData.Amount / (card.CardData.EnergyCost * 10);
        }
        
        return priority;
    }
} 