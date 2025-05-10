using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;

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
                    // Player targets their pet
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
        if (player == null)
        {
            Debug.LogError("HandleCardPlay: Player cannot be null");
            return false;
        }
        
        // Set the owner entity to the player if not already set
        if (!isEntitySet || parentEntity != player)
        {
            SetOwnerEntity(player.gameObject);
        }
        
        // Validate that the card can be played
        var (canPlay, errorMessage) = ValidateCardPlay(cardId);
        if (!canPlay)
        {
            if (player.Owner != null && combatManager != null)
            {
                // Send notification to the player
                combatManager.SendNotificationToPlayer(errorMessage, player.Owner);
            }
            return false;
        }
        
        // Get the card data
        CardData cardData = GetCardData(cardId);
        if (cardData == null)
        {
            return false;
        }
        
        // Get the target based on card's targeting type
        if (!TryGetTarget(cardData, out NetworkBehaviour target))
        {
            return false;
        }
        
        // Get the HandManager
        HandManager handManager = player.GetComponent<HandManager>();
        if (handManager == null)
        {
            return false;
        }
        
        // Get the effect manager
        EffectManager targetEffectManager = target.GetComponent<EffectManager>();
        if (targetEffectManager == null)
        {
            return false;
        }
        
        // Deduct energy cost
        player.ChangeEnergy(-cardData.EnergyCost);
        
        // Apply the effect from the player to the target
        targetEffectManager.ApplyEffect(target, cardData);
        
        // Move the card from hand to discard
        handManager.MoveCardToDiscard(cardId);
        
        // Notify clients that the card was played
        if (combatManager != null)
        {
            combatManager.NotifyCardPlayed((uint)player.ObjectId, (uint)target.ObjectId, cardId, cardInstanceId, player.Owner);
        }
        
        return true;
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