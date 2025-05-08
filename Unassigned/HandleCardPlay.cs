using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;

/// <summary>
/// Handles the logic for playing cards during combat, including validation and effect application.
/// Attach to: Both NetworkPlayer and NetworkPet prefabs alongside HandManager.
/// </summary>
// Script to handle playing cards from NetworkPlayer and NetworkPet prefabs
public class HandleCardPlay : MonoBehaviour
{
    private NetworkBehaviour parentEntity; // Reference to the NetworkPlayer or NetworkPet this is attached to
    private HandManager handManager;       // Reference to the HandManager component on this entity
    private CombatHand combatHand;         // Reference to the CombatHand component
    private CardSpawner cardSpawner;       // Reference to the CardSpawner component
    private FightManager fightManager;     // Reference to the scene's FightManager
    
    private void Awake()
    {
        // Get the parent NetworkBehaviour (either NetworkPlayer or NetworkPet)
        parentEntity = GetComponent<NetworkPlayer>() as NetworkBehaviour;
        if (parentEntity == null)
        {
            parentEntity = GetComponent<NetworkPet>() as NetworkBehaviour;
        }

        if (parentEntity == null)
        {
            Debug.LogError("HandleCardPlay: Not attached to a NetworkPlayer or NetworkPet. This component must be attached to one of these.");
            return;
        }

        // Get the HandManager component on this entity
        handManager = GetComponent<HandManager>();
        if (handManager == null)
        {
            Debug.LogError("HandleCardPlay: HandManager component not found on the same GameObject.");
        }
        
        // Get the CombatHand component
        combatHand = GetComponent<CombatHand>();
        if (combatHand == null)
        {
            Debug.LogError("HandleCardPlay: CombatHand component not found on the same GameObject.");
        }
        
        // Get the CardSpawner component
        cardSpawner = GetComponent<CardSpawner>();
        if (cardSpawner == null)
        {
            Debug.LogError("HandleCardPlay: CardSpawner component not found on the same GameObject.");
        }
    }

    private void Start()
    {
        // Find the FightManager in the scene
        fightManager = FightManager.Instance;
        if (fightManager == null)
        {
            Debug.LogError("HandleCardPlay: FightManager not found in the scene.");
        }
    }

    // Method to play a card from this entity
    public void PlayCard(int cardId)
    {
        if (parentEntity == null || !parentEntity.IsServerStarted || handManager == null || fightManager == null || combatHand == null)
        {
            Debug.LogError("PlayCard: Required components are missing or not initialized.");
            return;
        }

        // Get the player or pet
        NetworkPlayer player = parentEntity as NetworkPlayer;
        NetworkPet pet = parentEntity as NetworkPet;
        
        if (player == null && pet == null)
        {
            Debug.LogError("PlayCard: Parent entity is neither a NetworkPlayer nor a NetworkPet.");
            return;
        }

        // Check if the card is in the entity's hand
        bool cardInHand = combatHand.HasCard(cardId);
        string entityName = player != null ? player.PlayerName.Value : pet.PetName.Value;

        if (!cardInHand)
        {
            Debug.LogWarning($"{entityName} tried to play card ID {cardId}, but it's not in their hand.");
            return;
        }

        // Get the card data
        CardData cardData = GetCardDataFromId(cardId);
        if (cardData == null)
        {
            Debug.LogError($"Card data not found for ID: {cardId}");
            return;
        }

        // Check if the entity has enough energy
        if (player != null && player.CurrentEnergy.Value < cardData.EnergyCost)
        {
            Debug.LogWarning($"{entityName} doesn't have enough energy to play card {cardData.CardName}.");
            return;
        }
        else if (pet != null && pet.CurrentEnergy.Value < cardData.EnergyCost)
        {
            Debug.LogWarning($"{entityName} doesn't have enough energy to play card {cardData.CardName}.");
            return;
        }

        // Get the target from FightManager
        NetworkBehaviour target = null;
        if (player != null)
        {
            NetworkPet targetPet = fightManager.GetOpponentForPlayer(player);
            if (targetPet != null)
            {
                target = targetPet;
                // Deduct energy cost
                player.ChangeEnergy(-cardData.EnergyCost);
            }
        }
        else if (pet != null)
        {
            NetworkPlayer targetPlayer = fightManager.GetOpponentForPet(pet);
            if (targetPlayer != null)
            {
                target = targetPlayer;
                // Deduct energy cost
                pet.ChangeEnergy(-cardData.EnergyCost);
            }
        }

        if (target == null)
        {
            Debug.LogError($"No target found for {entityName} in FightManager.");
            return;
        }

        // Apply the effect using the target's EffectManager
        EffectManager targetEffectManager = target.GetComponent<EffectManager>();
        if (targetEffectManager != null)
        {
            // Apply the effect to the target
            targetEffectManager.ApplyEffect(parentEntity, cardData);
            
            // Move the card from hand to discard
            handManager.MoveCardToDiscard(cardId);
            
            // Notify CardSpawner on client side (via observers RPC in network behavior)
            if (parentEntity is NetworkBehaviour netBehavior)
            {
                // The parent entity will notify clients that the card has been played
                if (netBehavior is NetworkPlayer networkPlayer)
                {
                    networkPlayer.NotifyCardPlayed(cardId);
                }
                else if (netBehavior is NetworkPet networkPet)
                {
                    networkPet.NotifyCardPlayed(cardId);
                }
            }
            
            Debug.Log($"{entityName} played card {cardData.CardName} on {target.name}");
        }
        else
        {
            Debug.LogError($"EffectManager component not found on target {target.name}");
        }
    }

    // Helper method to get card data from card ID (placeholder - replace with your card database)
    private CardData GetCardDataFromId(int cardId)
    {
        // This is a placeholder. In a real implementation, you would:
        // 1. Get this from a CardDatabase singleton
        // 2. Or from a more robust system that manages card data

        // For now, let's create a simple placeholder card
        if (CardDatabase.Instance != null)
        {
            return CardDatabase.Instance.GetCardById(cardId);
        }
        
        Debug.LogError("CardDatabase.Instance is null. Cannot retrieve card data.");
        return null;
    }
} 