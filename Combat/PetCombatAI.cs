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
    /// Queues cards for the pet during the shared turn phase
    /// </summary>
    [Server]
    public void QueueCardsForSharedTurn()
    {
        if (!IsServerInitialized) return;
        
        Debug.Log($"AI_COMBAT_DEBUG: {petEntity.EntityName.Value} (ID: {petEntity.ObjectId}) starting QueueCardsForSharedTurn");

        // Get all cards in hand
        Debug.Log($"AI_COMBAT_DEBUG: {petEntity.EntityName.Value} - Getting hand manager");
        HandManager handManager = GetHandManager();
        if (handManager == null)
        {
            Debug.LogError($"AI_COMBAT_DEBUG: Cannot find hand manager for {petEntity.EntityName.Value}");
            return;
        }

        Debug.Log($"AI_COMBAT_DEBUG: {petEntity.EntityName.Value} - Getting hand transform");
        Transform handTransform = handManager.GetHandTransform();
        if (handTransform == null)
        {
            Debug.LogError($"AI_COMBAT_DEBUG: Cannot find hand transform for {petEntity.EntityName.Value}");
            return;
        }

        Debug.Log($"AI_COMBAT_DEBUG: {petEntity.EntityName.Value} - Getting cards in hand");
        List<GameObject> cardsInHand = GetCardsInHand(handTransform);
        Debug.Log($"AI_COMBAT_DEBUG: {petEntity.EntityName.Value} - Found {cardsInHand.Count} cards in hand");

        // If no cards in hand, end turn
        if (cardsInHand.Count == 0)
        {
            Debug.Log($"AI_COMBAT_DEBUG: {petEntity.EntityName.Value} has no cards to queue - ending turn early");
            return;
        }

        // Get opponent (target for most cards)
        NetworkEntity opponentEntity = GetOpponentEntity();
        if (opponentEntity == null)
        {
            Debug.LogError($"AI_COMBAT_DEBUG: Cannot find opponent for {petEntity.EntityName.Value}");
            return;
        }
        
        Debug.Log($"AI_COMBAT_DEBUG: {petEntity.EntityName.Value} found opponent: {opponentEntity.EntityName.Value} (ID: {opponentEntity.ObjectId})");

        // Sort cards by priority for AI to play
        List<GameObject> sortedCards = GetSortedCardsByPriority(cardsInHand, opponentEntity);
        Debug.Log($"AI_COMBAT_DEBUG: {petEntity.EntityName.Value} sorted {sortedCards.Count} cards by priority");

        // Play cards until out of energy or cards
        int remainingEnergy = petEntity.CurrentEnergy.Value;
        Debug.Log($"AI_COMBAT_DEBUG: {petEntity.EntityName.Value} - Starting with {remainingEnergy} energy");

        int cardsQueued = 0;
        foreach (GameObject cardObject in sortedCards)
        {
            Card card = cardObject.GetComponent<Card>();
            if (card == null || card.CardData == null)
            {
                Debug.LogError($"AI_COMBAT_DEBUG: Invalid card in hand for {petEntity.EntityName.Value}");
                continue;
            }

            Debug.Log($"AI_COMBAT_DEBUG: {petEntity.EntityName.Value} - Evaluating card: {card.CardData.CardName} (cost: {card.CardData.EnergyCost})");

            // Check if we have enough energy to play this card
            if (card.CardData.EnergyCost > remainingEnergy)
            {
                Debug.Log($"AI_COMBAT_DEBUG: {petEntity.EntityName.Value} - Skipping {card.CardData.CardName} (cost: {card.CardData.EnergyCost}, remaining: {remainingEnergy})");
                continue;
            }
            
            Debug.Log($"AI_COMBAT_DEBUG: {petEntity.EntityName.Value} - Can afford {card.CardData.CardName} (cost: {card.CardData.EnergyCost}, remaining: {remainingEnergy})");

            // Queue the card play
            // Prepare card for play by setting up source and target
            SourceAndTargetIdentifier sourceTarget = cardObject.GetComponent<SourceAndTargetIdentifier>();
            if (sourceTarget != null)
            {
                // Determine the correct target based on the card's actual target type
                NetworkEntity correctTarget = DetermineTargetForCard(card.CardData, petEntity, opponentEntity);
                if (correctTarget != null)
                {
                    sourceTarget.ForceUpdateSourceAndTarget(petEntity, correctTarget);
                    Debug.Log($"AI_COMBAT_DEBUG: {petEntity.EntityName.Value} - Set target for {card.CardData.CardName}: {correctTarget.EntityName.Value}");
                }
                else
                {
                    CardTargetType effectiveTargetType = card.CardData.GetEffectiveTargetType();
                    Debug.LogWarning($"AI_COMBAT_DEBUG: Could not determine valid target for card {card.CardData.CardName} with target type {effectiveTargetType}");
                    continue;
                }
            }
            else
            {
                Debug.LogError($"AI_COMBAT_DEBUG: Card {card.CardData.CardName} missing SourceAndTargetIdentifier");
                continue;
            }

            // Queue the card play
            HandleCardPlay cardPlayHandler = cardObject.GetComponent<HandleCardPlay>();
            if (cardPlayHandler != null)
            {
                Debug.Log($"AI_COMBAT_DEBUG: Attempting to queue card {card.CardData.CardName}");
                
                // Get source and target information for the server call
                SourceAndTargetIdentifier sourceTargetId = cardObject.GetComponent<SourceAndTargetIdentifier>();
                if (sourceTargetId != null)
                {
                    // Update source and target on server side for AI
                    sourceTargetId.UpdateSourceAndTarget();
                    
                    var sourceEntity = sourceTargetId.SourceEntity;
                    var allTargets = sourceTargetId.AllTargets;
                    
                    int sourceId = sourceEntity != null ? sourceEntity.ObjectId : petEntity.ObjectId; // Fallback to pet entity
                    int[] targetIds = allTargets != null ? allTargets.Select(t => t != null ? t.ObjectId : 0).Where(id => id != 0).ToArray() : new int[0];
                    
                    Debug.Log($"AI_COMBAT_DEBUG: Calling ServerPlayCard (queuing) with sourceId: {sourceId}, targetIds: [{string.Join(", ", targetIds)}]");
                    
                    // Call the ServerPlayCard method with parameters (this will queue the card)
                    try
                    {
                        cardPlayHandler.ServerPlayCard(sourceId, targetIds);
                        Debug.Log($"AI_COMBAT_DEBUG: Successfully queued card {card.CardData.CardName}");
                        cardsQueued++;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"AI_COMBAT_DEBUG: Exception queuing card {card.CardData.CardName}: {ex.Message}");
                        continue;
                    }
                }
                else
                {
                    Debug.LogError($"AI_COMBAT_DEBUG: SourceAndTargetIdentifier not found on card {card.CardData.CardName}");
                    continue;
                }
                
                // Update remaining energy for AI decision making
                remainingEnergy -= card.CardData.EnergyCost;
                Debug.Log($"AI_COMBAT_DEBUG: {petEntity.EntityName.Value} - Energy after queuing {card.CardData.CardName}: {remainingEnergy}");
            }
            else
            {
                Debug.LogError($"AI_COMBAT_DEBUG: Card {card.CardData.CardName} missing HandleCardPlay");
                continue;
            }
        }

        Debug.Log($"AI_COMBAT_DEBUG: {petEntity.EntityName.Value} finished queuing cards for shared turn - {cardsQueued} cards queued, {remainingEnergy} energy remaining");
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
            return fightManager.GetOpponent(petEntity);
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
                    // DrawCard effect removed
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
                /* Debug.Log($"PetCombatAI: Card {cardData.CardName} targets Self - returning pet entity"); */
                return petEntity;
                
            case CardTargetType.Opponent:
                /* Debug.Log($"PetCombatAI: Card {cardData.CardName} targets Opponent - returning opponent entity"); */
                return opponentEntity;
                
            case CardTargetType.Ally:
                // Get the pet's ally (usually the player)
                NetworkEntity ally = GetAllyForEntity(petEntity);
                if (ally != null)
                {
                    /* Debug.Log($"PetCombatAI: Card {cardData.CardName} targets Ally - returning {ally.EntityName.Value}"); */
                    return ally;
                }
                else
                {
                    Debug.LogWarning($"PET_AI_DEBUG: Card {cardData.CardName} targets Ally but no ally found");
                    return null;
                }
                
            case CardTargetType.Random:
                // For random, pick from all available targets
                List<NetworkEntity> allTargets = GetAllPossibleTargets(petEntity, opponentEntity);
                if (allTargets.Count > 0)
                {
                    int randomIndex = Random.Range(0, allTargets.Count);
                    NetworkEntity randomTarget = allTargets[randomIndex];
                    /* Debug.Log($"PetCombatAI: Card {cardData.CardName} targets Random - selected {randomTarget.EntityName.Value}"); */
                    return randomTarget;
                }
                break;
                
            // AllEnemies targeting removed - cards now use single targets with "can also target" flags
                
            default:
                Debug.LogWarning($"PET_AI_DEBUG: Unhandled target type {targetType} for card {cardData.CardName}, defaulting to opponent");
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