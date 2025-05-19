using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems; // Required for IPointerClickHandler
using UnityEngine.UI; // Required for Button

/// <summary>
/// Handles the logic for playing cards during combat
/// Attach to: Card prefabs only.
/// </summary>
public class HandleCardPlay : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Button cardButton;
    [SerializeField] private SourceAndTargetIdentifier sourceAndTargetIdentifier;
    [SerializeField] private Card card;
    [SerializeField] private CardEffectResolver cardEffectResolver;

    private CardData cardData;

    private void Awake()
    {
        // Get the button component
        if (cardButton == null) 
        {
            cardButton = GetComponent<Button>();
        }

        if (sourceAndTargetIdentifier == null)
        {
            sourceAndTargetIdentifier = GetComponent<SourceAndTargetIdentifier>();
        }

        if (card == null)
        {
            card = GetComponent<Card>();
        }
        
        if (cardEffectResolver == null)
        {
            cardEffectResolver = GetComponent<CardEffectResolver>();
        }

        ValidateComponents();
    }

    private void ValidateComponents()
    {
        if (cardButton == null)
            Debug.LogError($"HandleCardPlay on {gameObject.name}: Missing Button component");
        
        if (sourceAndTargetIdentifier == null)
            Debug.LogError($"HandleCardPlay on {gameObject.name}: Missing SourceAndTargetIdentifier component");
        
        if (card == null)
            Debug.LogError($"HandleCardPlay on {gameObject.name}: Missing Card component");
            
        if (cardEffectResolver == null)
            Debug.LogError($"HandleCardPlay on {gameObject.name}: Missing CardEffectResolver component");
    }

    public void Initialize(CardData data)
    {
        ValidateComponents();
        cardData = data;
        if (cardButton != null)
        {
            cardButton.onClick.AddListener(OnCardClicked);
        }
    }

    /// <summary>
    /// Called when the card is clicked
    /// </summary>
    public void OnCardClicked()
    {
        Debug.Log($"HandleCardPlay: OnCardClicked for card {gameObject.name}");
        
        // Validate basic conditions
        if (!IsOwner)
        {
            Debug.LogWarning($"HandleCardPlay: Cannot handle click, not network owner of card {gameObject.name}");
            return;
        }

        if (card == null || card.CurrentContainer != CardLocation.Hand)
        {
            Debug.LogWarning($"HandleCardPlay: Cannot handle click, card is not in hand. Card: {gameObject.name}, Location: {(card != null ? card.CurrentContainer.ToString() : "Card component missing")}");
            return;
        }

        // Check if player has enough energy
        NetworkEntity owner = card.OwnerEntity;
        if (owner != null && cardData != null && owner.CurrentEnergy.Value < cardData.EnergyCost)
        {
            Debug.LogWarning($"HandleCardPlay: Not enough energy to play card. Required: {cardData.EnergyCost}, Available: {owner.CurrentEnergy.Value}");
            // Consider showing a UI notification to the player
            return;
        }

        // Card is valid to be clicked - Update source and target references
        if (sourceAndTargetIdentifier != null)
        {
            Debug.Log($"HandleCardPlay: Triggering UpdateSourceAndTarget for card {gameObject.name}");
            sourceAndTargetIdentifier.UpdateSourceAndTarget();
            
            // Verify we have valid source and target
            if (sourceAndTargetIdentifier.SourceEntity == null || sourceAndTargetIdentifier.TargetEntity == null)
            {
                Debug.LogError($"HandleCardPlay: Missing source or target entity for card {gameObject.name}");
                return;
            }
            
            // Now that we have valid source and target, play the card
            PlayCard();
        }
        else
        {
            Debug.LogError($"HandleCardPlay: Missing SourceAndTargetIdentifier component for card {gameObject.name}");
        }
    }
    
    /// <summary>
    /// Plays the card by resolving its effect and handling energy cost
    /// </summary>
    private void PlayCard()
    {
        Debug.Log($"HandleCardPlay: Playing card {gameObject.name}");
        
        // Get owner entity (should be the source entity from SourceAndTargetIdentifier)
        NetworkEntity owner = sourceAndTargetIdentifier.SourceEntity;
        
        // Handle energy cost on server
        if (cardData != null)
        {
            CmdDeductEnergyCost(owner.ObjectId, cardData.EnergyCost);
        }
        
        // Resolve the card effect
        if (cardEffectResolver != null)
        {
            Debug.Log($"HandleCardPlay: Calling cardEffectResolver.ResolveCardEffect() for card {gameObject.name}");
            cardEffectResolver.ResolveCardEffect();
        }
        else
        {
            Debug.LogError($"HandleCardPlay: Missing CardEffectResolver component for card {gameObject.name}");
        }
        
        // Move the card to the discard pile
        CmdMoveCardToDiscard();
    }
    
    [ServerRpc]
    private void CmdDeductEnergyCost(int ownerEntityId, int energyCost)
    {
        // Find the entity by ID
        NetworkEntity entity = FindEntityById(ownerEntityId);
        if (entity == null)
        {
            Debug.LogError($"HandleCardPlay: Could not find entity with ID {ownerEntityId}");
            return;
        }
        
        // Deduct energy cost
        entity.ChangeEnergy(-energyCost);
        Debug.Log($"HandleCardPlay: Deducted {energyCost} energy from {entity.EntityName.Value}. New energy: {entity.CurrentEnergy.Value}");
    }
    
    [ServerRpc]
    private void CmdMoveCardToDiscard()
    {
        // Get the card's owner
        NetworkEntity owner = card.OwnerEntity;
        if (owner == null)
        {
            Debug.LogError($"HandleCardPlay: Card {gameObject.name} has no owner");
            return;
        }
        
        // Get the hand manager
        HandManager handManager = owner.GetComponent<HandManager>();
        if (handManager == null)
        {
            Debug.LogError($"HandleCardPlay: Owner {owner.EntityName.Value} has no HandManager component");
            return;
        }
        
        // Move the card from hand to discard pile
        handManager.DiscardCard(card.gameObject);
        Debug.Log($"HandleCardPlay: Moved card {gameObject.name} to discard pile");
    }
    
    private NetworkEntity FindEntityById(int entityId)
    {
        NetworkObject netObj = null;
        
        if (IsServerInitialized)
        {
            FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(entityId, out netObj);
        }
        else if (IsClientInitialized)
        {
            FishNet.InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(entityId, out netObj);
        }
        
        return netObj?.GetComponent<NetworkEntity>();
    }
} 