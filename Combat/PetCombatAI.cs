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

        ValidateComponents();
    }

    private void ValidateComponents()
    {
        if (petEntity == null)
            Debug.LogError($"PetCombatAI on {gameObject.name}: Missing NetworkEntity component");
    }

    /// <summary>
    /// Executes the pet's turn in combat
    /// </summary>
    [Server]
    public IEnumerator TakeTurn()
    {
        if (!IsServerInitialized) yield break;


        hasFinishedTurn = false;

        // Small delay before starting
        yield return new WaitForSeconds(delayBeforeFirstAction);

        // Get all cards in hand
        HandManager handManager = GetHandManager();
        if (handManager == null)
        {
            Debug.LogError($"PetCombatAI: Cannot find hand manager for {petEntity.EntityName.Value}");
            hasFinishedTurn = true;
            yield break;
        }

        Transform handTransform = handManager.GetHandTransform();
        if (handTransform == null)
        {
            Debug.LogError($"PetCombatAI: Cannot find hand transform for {petEntity.EntityName.Value}");
            hasFinishedTurn = true;
            yield break;
        }

        List<GameObject> cardsInHand = GetCardsInHand(handTransform);


        // If no cards in hand, end turn
        if (cardsInHand.Count == 0)
        {

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

                continue;
            }

            // Play the card


            // Prepare card for play by setting up source and target
            SourceAndTargetIdentifier sourceTarget = cardObject.GetComponent<SourceAndTargetIdentifier>();
            if (sourceTarget != null)
            {
                // FIXED: Determine the correct target based on the card's actual target type
                // instead of always forcing opponent target
                NetworkEntity correctTarget = DetermineTargetForCard(card.CardData, petEntity, opponentEntity);
                if (correctTarget != null)
                {
                    sourceTarget.ForceUpdateSourceAndTarget(petEntity, correctTarget);
                    // FIXED: Use the actual effective target type instead of legacy TargetType property
                    CardTargetType effectiveTargetType = card.CardData.GetEffectiveTargetType();

                }
                else
                {
                    // FIXED: Use the actual effective target type instead of legacy TargetType property
                    CardTargetType effectiveTargetType = card.CardData.GetEffectiveTargetType();
                    Debug.LogWarning($"PetCombatAI: Could not determine valid target for card {card.CardData.CardName} with target type {effectiveTargetType}");
                    continue;
                }
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
        
        // FIXED: Use Effects list instead of legacy EffectType and Amount properties
        int totalDamage = 0;
        int totalHealing = 0;
        bool hasStun = false;
        bool hasBuff = false;
        bool hasDebuff = false;
        bool hasDrawCard = false;
        
        if (card.CardData.HasEffects)
        {
            foreach (var effect in card.CardData.Effects)
            {
                switch (effect.effectType)
                {
                    case CardEffectType.Damage:
                        totalDamage += effect.amount;
                        break;
                    case CardEffectType.Heal:
                        totalHealing += effect.amount;
                        break;
                    case CardEffectType.ApplyStun:
                        hasStun = true;
                        break;
                    case CardEffectType.ApplyStrength:
                        hasBuff = true;
                        break;
                    case CardEffectType.ApplyCurse:
                        hasDebuff = true;
                        break;
                    case CardEffectType.DrawCard:
                        hasDrawCard = true;
                        break;
                }
            }
        }
        else
        {
            // Fallback: check if card has effects but we didn't process them above
            if (card.CardData.HasEffects && card.CardData.Effects.Count > 0)
            {
                var firstEffect = card.CardData.Effects[0];
                switch (firstEffect.effectType)
                {
                    case CardEffectType.Damage:
                        priority += firstEffect.amount;
                        break;
                    case CardEffectType.Heal:
                        priority += firstEffect.amount * 2; // Healing is valuable
                        break;
                    default:
                        priority += 10; // Base priority for other effects
                        break;
                }
            }
        }
        
        // Calculate priority based on primary effect types
        if (totalDamage > 0)
        {
            // Higher priority when opponent is low
            priority = 50 + totalDamage;
            if (opponentHealthPercent < 0.3f)
                priority += 30;
            else if (opponentHealthPercent < 0.5f)
                priority += 20;
        }
        else if (totalHealing > 0)
        {
            // Higher priority when pet is low on health
            priority = 30 + totalHealing;
            if (petHealthPercent < 0.3f)
                priority += 40;
            else if (petHealthPercent < 0.5f)
                priority += 20;
            else
                priority -= 20; // Lower priority if health is high
        }
        else if (hasBuff)
        {
            // Buffs are good early in the fight
            priority = 40;
        }
        else if (hasDebuff)
        {
            // Status effects are good early in the fight
            priority = 45;
        }
        else if (hasDrawCard)
        {
            // Draw cards are good when we have low cards and energy to play them
            priority = 35 + (petEntity.CurrentEnergy.Value * 5);
        }
        else if (hasStun)
        {
            return CalculateStunScore(opponent);
        }
        else
        {
            priority = 30;
        }
        
        // Adjust for energy efficiency using total effective amount
        int totalEffectAmount = totalDamage + totalHealing;
        if (card.CardData.EnergyCost > 0 && totalEffectAmount > 0)
        {
            priority = priority * totalEffectAmount / (card.CardData.EnergyCost * 10);
        }
        
        return priority;
    }

    /// <summary>
    /// Calculates the priority score for stun effects
    /// </summary>
    private int CalculateStunScore(NetworkEntity opponent)
    {
        // Stun is very valuable - prevents opponent from acting
        int baseScore = 80;
        
        // Higher priority if opponent has more energy (more they lose by being stunned)
        baseScore += opponent.CurrentEnergy.Value * 5;
        
        // Higher priority if opponent has cards in hand
        HandManager opponentHand = GetOpponentHandManager(opponent);
        if (opponentHand != null)
        {
            int cardsInHand = opponentHand.GetCardsInHand().Count;
            baseScore += cardsInHand * 3;
        }
        
        return baseScore;
    }

    /// <summary>
    /// Gets the HandManager from the opponent entity
    /// </summary>
    private HandManager GetOpponentHandManager(NetworkEntity opponent)
    {
        if (opponent == null) return null;
        
        var relationshipManager = opponent.GetComponent<RelationshipManager>();
        if (relationshipManager?.HandEntity == null) return null;
        
        var handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
        if (handEntity == null) return null;
        
        return handEntity.GetComponent<HandManager>();
    }

    /// <summary>
    /// Gets the HandManager from the pet's hand entity
    /// </summary>
    private HandManager GetHandManager()
    {
        // Find the hand entity through RelationshipManager
        var relationshipManager = petEntity.GetComponent<RelationshipManager>();
        if (relationshipManager == null)
        {
            Debug.LogError($"PetCombatAI: No RelationshipManager found on pet {petEntity.EntityName.Value}");
            return null;
        }

        if (relationshipManager.HandEntity == null)
        {
            Debug.LogError($"PetCombatAI: No hand entity found for pet {petEntity.EntityName.Value}");
            return null;
        }

        var handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
        if (handEntity == null)
        {
            Debug.LogError($"PetCombatAI: Hand entity is not a valid NetworkEntity for pet {petEntity.EntityName.Value}");
            return null;
        }

        var handManager = handEntity.GetComponent<HandManager>();
        if (handManager == null)
        {
            Debug.LogError($"PetCombatAI: No HandManager found on hand entity for pet {petEntity.EntityName.Value}");
            return null;
        }

        return handManager;
    }

    /// <summary>
    /// Determines the correct target for a card based on its target type
    /// </summary>
    private NetworkEntity DetermineTargetForCard(CardData cardData, NetworkEntity petEntity, NetworkEntity opponentEntity)
    {
        if (cardData == null) return null;
        
        CardTargetType targetType = cardData.GetEffectiveTargetType();
        
        switch (targetType)
        {
            case CardTargetType.Self:
                Debug.Log($"PetCombatAI: Card {cardData.CardName} targets Self - returning pet entity");
                return petEntity;
                
            case CardTargetType.Opponent:
                Debug.Log($"PetCombatAI: Card {cardData.CardName} targets Opponent - returning opponent entity");
                return opponentEntity;
                
            case CardTargetType.Ally:
                // Get the pet's ally (usually the player)
                NetworkEntity ally = GetAllyForEntity(petEntity);
                if (ally != null)
                {
                    Debug.Log($"PetCombatAI: Card {cardData.CardName} targets Ally - returning {ally.EntityName.Value}");
                    return ally;
                }
                else
                {
                    Debug.LogWarning($"PetCombatAI: Card {cardData.CardName} targets Ally but no ally found");
                    return null;
                }
                
            case CardTargetType.Random:
                // For random, pick from all available targets
                List<NetworkEntity> allTargets = GetAllPossibleTargets(petEntity, opponentEntity);
                if (allTargets.Count > 0)
                {
                    int randomIndex = Random.Range(0, allTargets.Count);
                    NetworkEntity randomTarget = allTargets[randomIndex];
                    Debug.Log($"PetCombatAI: Card {cardData.CardName} targets Random - selected {randomTarget.EntityName.Value}");
                    return randomTarget;
                }
                break;
                
            case CardTargetType.All:
            case CardTargetType.AllAllies:
            case CardTargetType.AllEnemies:
                // For multi-target cards, return the primary target (usually self for allies, opponent for enemies)
                if (targetType == CardTargetType.AllEnemies)
                {
                    Debug.Log($"PetCombatAI: Card {cardData.CardName} targets All Enemies - returning opponent as primary");
                    return opponentEntity;
                }
                else
                {
                    Debug.Log($"PetCombatAI: Card {cardData.CardName} targets All/AllAllies - returning self as primary");
                    return petEntity;
                }
                
            default:
                Debug.LogWarning($"PetCombatAI: Unhandled target type {targetType} for card {cardData.CardName}, defaulting to opponent");
                return opponentEntity;
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets the ally entity for the given entity
    /// </summary>
    private NetworkEntity GetAllyForEntity(NetworkEntity entity)
    {
        if (entity == null) return null;

        RelationshipManager relationshipManager = entity.GetComponent<RelationshipManager>();
        if (relationshipManager?.AllyEntity != null)
        {
            return relationshipManager.AllyEntity.GetComponent<NetworkEntity>();
        }

        return null;
    }
    
    /// <summary>
    /// Gets all possible target entities for random targeting
    /// </summary>
    private List<NetworkEntity> GetAllPossibleTargets(NetworkEntity petEntity, NetworkEntity opponentEntity)
    {
        List<NetworkEntity> targets = new List<NetworkEntity>();
        
        // Add self
        if (petEntity != null)
            targets.Add(petEntity);
            
        // Add ally
        NetworkEntity ally = GetAllyForEntity(petEntity);
        if (ally != null)
            targets.Add(ally);
            
        // Add opponent
        if (opponentEntity != null)
            targets.Add(opponentEntity);
            
        return targets;
    }
} 