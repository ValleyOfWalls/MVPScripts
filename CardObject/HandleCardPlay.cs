using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Handles the logic for playing cards during combat, including validation and effect application.
/// Attach to: Card prefabs only.
/// </summary>
public class HandleCardPlay : MonoBehaviour
{
    private NetworkBehaviour parentEntity; // Reference to the NetworkPlayer or NetworkPet this card belongs to
    private NetworkBehaviour targetEntity; // Reference to the target entity for the card effect
    private FightManager fightManager;     // Reference to the scene's FightManager
    private CombatManager combatManager;   // Reference to the scene's CombatManager
    private HandManager handManager;       // Reference to the HandManager of the parent entity
    
    private bool isEntitySet = false;      // Flag to track if the parent entity has been set
    
    /// <summary>
    /// Sets the parent entity (NetworkPlayer or NetworkPet) for this HandleCardPlay instance.
    /// This should be called when the card is instantiated and assigned to a player/pet.
    /// </summary>
    /// <param name="ownerObject">The GameObject of either NetworkPlayer or NetworkPet</param>
    public void SetOwnerEntity(GameObject ownerObject)
    {
        if (ownerObject == null)
        {
            Debug.LogError("HandleCardPlay: Attempted to set null object as owner entity.");
            return;
        }
        
        // Get the NetworkBehaviour component (either NetworkPlayer or NetworkPet)
        NetworkBehaviour entity = ownerObject.GetComponent<NetworkPlayer>() as NetworkBehaviour;
        if (entity == null)
        {
            entity = ownerObject.GetComponent<NetworkPet>() as NetworkBehaviour;
        }
        
        if (entity == null)
        {
            Debug.LogError("HandleCardPlay: Owner object does not have NetworkPlayer or NetworkPet component.");
            return;
        }
        
        // Set the parent entity
        parentEntity = entity;
        isEntitySet = true;
        
        // Find the FightManager in the scene if not already set
        if (fightManager == null)
        {
            fightManager = FightManager.Instance;
        }
        
        // Find the CombatManager in the scene if not already set
        if (combatManager == null)
        {
            combatManager = CombatManager.Instance;
        }
        
        // Get the HandManager from the parent entity
        handManager = entity.GetComponent<HandManager>();
        if (handManager == null)
        {
            Debug.LogWarning($"HandManager component not found on {entity.name}");
        }
    }
    
    private void Awake()
    {
        // Find the FightManager and CombatManager in the scene
        fightManager = FightManager.Instance;
        combatManager = CombatManager.Instance;
    }

    /// <summary>
    /// Internal method to determine the target for this card based on the card's target type
    /// </summary>
    private bool TryGetTarget(CardData cardData, out NetworkBehaviour target)
    {
        target = null;
        
        if (!isEntitySet || parentEntity == null)
        {
            Debug.LogError("HandleCardPlay: Cannot get target - owner entity not set.");
            return false;
        }
        
        if (fightManager == null)
        {
            Debug.LogError("HandleCardPlay: Cannot get target - FightManager not found.");
            return false;
        }
        
        if (cardData == null)
        {
            Debug.LogError("HandleCardPlay: Cannot get target - card data is null.");
            return false;
        }
        
        // Determine target based on the card's target type
        switch (cardData.TargetType)
        {
            case CardTargetType.Self:
                // Target self
                target = parentEntity;
                return true;
                
            case CardTargetType.Opponent:
                // Target opponent
                if (parentEntity is NetworkPlayer player)
                {
                    NetworkPet targetPet = fightManager.GetOpponentForPlayer(player);
                    if (targetPet != null)
                    {
                        target = targetPet;
                        return true;
                    }
                }
                else if (parentEntity is NetworkPet pet)
                {
                    NetworkPlayer targetPlayer = fightManager.GetOpponentForPet(pet);
                    if (targetPlayer != null)
                    {
                        target = targetPlayer;
                        return true;
                    }
                }
                break;
                
            case CardTargetType.Ally:
                // Target ally
                if (parentEntity is NetworkPlayer allyPlayer)
                {
                    // Player targets themselves
                    RelationshipManager relationshipManager = allyPlayer.GetComponent<RelationshipManager>();
                    if (relationshipManager != null && relationshipManager.Ally != null)
                    {
                        target = relationshipManager.Ally as NetworkBehaviour;
                        return true;
                    }
                }
                else if (parentEntity is NetworkPet allyPet)
                {
                    // Pet targets its owner player
                    NetworkPlayer owner = allyPet.GetOwnerPlayer();
                    if (owner != null)
                    {
                        target = owner;
                        return true;
                    }
                }
                break;
                
            case CardTargetType.Random:
                // Randomly choose between opponent and self
                if (Random.value < 0.5f)
                {
                    // Target self
                    target = parentEntity;
                    return true;
                }
                else
                {
                    // Target opponent (same as Opponent case)
                    if (parentEntity is NetworkPlayer randomPlayer)
                    {
                        NetworkPet targetPet = fightManager.GetOpponentForPlayer(randomPlayer);
                        if (targetPet != null)
                        {
                            target = targetPet;
                            return true;
                        }
                    }
                    else if (parentEntity is NetworkPet randomPet)
                    {
                        NetworkPlayer targetPlayer = fightManager.GetOpponentForPet(randomPet);
                        if (targetPlayer != null)
                        {
                            target = targetPlayer;
                            return true;
                        }
                    }
                }
                break;
        }
        
        Debug.LogError($"No target found for {parentEntity.name} using target type {cardData.TargetType}");
        return false;
    }

    /// <summary>
    /// Validates whether a card can be played by the parent entity
    /// </summary>
    /// <param name="cardId">The ID of the card to validate</param>
    /// <returns>A tuple with (canPlay, errorMessage)</returns>
    public (bool canPlay, string errorMessage) ValidateCardPlay(int cardId)
    {
        if (!isEntitySet || parentEntity == null)
        {
            return (false, "Owner entity not set.");
        }
        
        // Make sure the parent entity is a NetworkPlayer (pets use different logic)
        if (!(parentEntity is NetworkPlayer player))
        {
            return (false, "Only players can manually play cards.");
        }
        
        // Check if it's the player's turn
        if (combatManager != null && !combatManager.IsPlayerTurn(player))
        {
            return (false, "Not your turn!");
        }
        
        // Get card data
        CardData cardData = GetCardData(cardId);
        if (cardData == null)
        {
            return (false, "Card not found in database.");
        }
        
        // Check if player has enough energy for the card
        if (player.CurrentEnergy.Value < cardData.EnergyCost)
        {
            return (false, $"Not enough energy! Need {cardData.EnergyCost} energy.");
        }
        
        // Check if the card is in the player's hand
        CombatHand playerHand = player.GetComponent<CombatHand>();
        if (playerHand == null || !playerHand.HasCard(cardId))
        {
            return (false, "Card not found in your hand.");
        }
        
        // Try to get the target for this card
        if (!TryGetTarget(cardData, out NetworkBehaviour target))
        {
            return (false, "Target not found for card effect.");
        }
        
        // Validate that target has an EffectManager
        if (target.GetComponent<EffectManager>() == null)
        {
            return (false, "Target cannot receive card effects.");
        }
        
        return (true, string.Empty);
    }
    
    /// <summary>
    /// Handles the full card playing process, including validation, energy cost, and effect application
    /// </summary>
    /// <param name="player">The player playing the card</param>
    /// <param name="cardId">The ID of the card being played</param>
    /// <param name="cardInstanceId">The instance ID of the card</param>
    /// <returns>True if the card was successfully played</returns>
    public bool HandleCardPlayRequest(NetworkPlayer player, int cardId, string cardInstanceId)
    {
        if (!FishNet.InstanceFinder.IsServerStarted) return false;
        
        if (player == null)
        {
            Debug.LogError("HandleCardPlay: Player is null");
            return false;
        }
        
        // Check if player has the card in hand using CombatHand
        CombatHand combatHand = player.GetComponent<CombatHand>();
        if (combatHand == null || !combatHand.HasCard(cardId))
        {
            Debug.LogError($"Player {player.PlayerName.Value} does not have card ID {cardId} in hand");
            return false;
        }
        
        // Find the specific card object using its instance ID (if provided)
        GameObject cardObj = null;
        if (!string.IsNullOrEmpty(cardInstanceId))
        {
            // Try to find the specific card instance from the player's hand
            cardObj = FindCardObjectByInstanceId(combatHand, cardInstanceId, cardId);
        }
        
        // If we couldn't find by instance ID, fall back to finding by card ID
        if (cardObj == null)
        {
            cardObj = FindCardObjectById(combatHand, cardId);
            if (cardObj == null)
            {
                Debug.LogError($"Couldn't find card object for ID {cardId} in player's hand");
                return false;
            }
        }
        
        // Get the card data
        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null)
        {
            Debug.LogError($"Card data not found for ID {cardId}");
            return false;
        }
        
        // Check if the player has enough energy
        if (player.CurrentEnergy.Value < cardData.EnergyCost)
        {
            Debug.LogError($"Player {player.PlayerName.Value} doesn't have enough energy to play card {cardData.CardName}");
            return false;
        }
        
        // Find the correct target based on the card's target type
        NetworkBehaviour target = null;
        switch (cardData.TargetType)
        {
            case CardTargetType.Self:
                target = player;
                break;
                
            case CardTargetType.Opponent:
                // Find the player's opponent using FightManager
                if (FightManager.Instance != null)
                {
                    target = FightManager.Instance.GetOpponentForPlayer(player);
                }
                
                if (target == null)
                {
                    Debug.LogError($"No opponent found for player {player.PlayerName.Value}");
                    return false;
                }
                break;
                
            case CardTargetType.Ally:
                // For now, "Ally" for a player means they target themselves
                target = player;
                break;
                
            case CardTargetType.Random:
                // Randomly choose between self and opponent
                if (Random.value < 0.5f)
                {
                    target = player; // Self
                }
                else
                {
                    // Find opponent
                    if (FightManager.Instance != null)
                    {
                        target = FightManager.Instance.GetOpponentForPlayer(player);
                    }
                    
                    if (target == null)
                    {
                        Debug.LogError($"No opponent found for player {player.PlayerName.Value} for random targeting");
                        target = player; // Fall back to self
                    }
                }
                break;
                
            default:
                Debug.LogError($"Unsupported card target type: {cardData.TargetType}");
                return false;
        }
        
        // Make sure the target has an effect manager
        EffectManager targetEffectManager = target.GetComponent<EffectManager>();
        if (targetEffectManager == null)
        {
            Debug.LogError($"Target {target.name} doesn't have an EffectManager component");
            return false;
        }
        
        // Deduct energy cost
        player.ChangeEnergy(-cardData.EnergyCost);
        
        // Apply the card effect
        targetEffectManager.ApplyEffect(target, cardData);
        
        // Get the HandManager if we don't have it yet
        if (handManager == null)
        {
            handManager = player.GetComponent<HandManager>();
        }
        
        // Remove card from hand and put in discard pile
        if (handManager != null)
        {
            handManager.MoveCardToDiscard(cardObj);
        }
        else
        {
            // Try to remove the card using the hand component directly
            combatHand.RemoveCard(cardObj);
            CombatDiscard discard = player.GetComponent<CombatDiscard>();
            if (discard != null)
            {
                discard.AddCard(cardObj);
            }
            else
            {
                // Last resort, just destroy the card if we couldn't find the discard pile
                Destroy(cardObj);
            }
        }
        
        // Notify the player's clients that a card was played with the specific instance ID
        player.NotifyCardPlayed(cardId, cardInstanceId);
        
        // Notify the combat manager that a card was played
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.NotifyCardPlayed((uint)player.ObjectId, (uint)target.ObjectId, cardId, cardInstanceId, player.Owner);
        }
        
        Debug.Log($"Player {player.PlayerName.Value} successfully played card {cardData.CardName} on {target.name}");
        
        return true;
    }

    /// <summary>
    /// Finds a card GameObject by its instance ID, falling back to card ID if needed
    /// </summary>
    private GameObject FindCardObjectByInstanceId(CombatHand hand, string instanceId, int cardId)
    {
        if (hand == null || string.IsNullOrEmpty(instanceId)) return null;
        
        // Get all cards from hand
        List<GameObject> cards = hand.GetAllCardObjects();
        
        // First try to match by the cardObj name which should contain the instance ID
        foreach (GameObject cardObj in cards)
        {
            if (cardObj != null && cardObj.name.Contains(instanceId))
            {
                return cardObj;
            }
        }
        
        // If not found by instance ID, fall back to the card ID
        return FindCardObjectById(hand, cardId);
    }
    
    /// <summary>
    /// Finds a card GameObject by its card ID
    /// </summary>
    private GameObject FindCardObjectById(CombatHand hand, int cardId)
    {
        if (hand == null) return null;
        
        // Get all cards from hand
        List<GameObject> cards = hand.GetAllCardObjects();
        
        // Find card by ID
        foreach (GameObject cardObj in cards)
        {
            if (cardObj != null)
            {
                Card cardComponent = cardObj.GetComponent<Card>();
                if (cardComponent != null && cardComponent.CardId == cardId)
                {
                    return cardObj;
                }
            }
        }
        
        return null;
    }

    /// <summary>
    /// Play this card on the appropriate target.
    /// Note: Card management (moving to discard, visual updates) should be handled elsewhere.
    /// </summary>
    /// <param name="cardData">The data for the card being played</param>
    /// <returns>True if the card effect was successfully applied</returns>
    public bool ApplyCardEffect(CardData cardData)
    {
        if (!isEntitySet || parentEntity == null)
        {
            Debug.LogError("HandleCardPlay: Cannot play card - owner entity not set.");
            return false;
        }
        
        // Find the target based on the card's target type
        if (!TryGetTarget(cardData, out NetworkBehaviour target))
        {
            return false;
        }
        
        // Apply the effect using the target's EffectManager
        EffectManager targetEffectManager = target.GetComponent<EffectManager>();
        if (targetEffectManager != null)
        {
            // Apply the effect to the target
            targetEffectManager.ApplyEffect(target, cardData);
            return true;
        }
        else
        {
            Debug.LogError($"EffectManager component not found on target {target.name}");
            return false;
        }
    }
    
    /// <summary>
    /// Gets the card data from the card database
    /// </summary>
    private CardData GetCardData(int cardId)
    {
        if (CardDatabase.Instance != null)
        {
            return CardDatabase.Instance.GetCardById(cardId);
        }
        return null;
    }
} 