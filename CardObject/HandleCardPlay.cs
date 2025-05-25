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
    
    // Flag to prevent double processing of the same card play
    private bool isProcessingCardPlay = false;

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
        
        // Check the current game phase to determine which handler to use
        GamePhaseManager gamePhaseManager = GamePhaseManager.Instance;
        if (gamePhaseManager != null)
        {
            GamePhaseManager.GamePhase currentPhase = gamePhaseManager.GetCurrentPhase();
            
            if (currentPhase == GamePhaseManager.GamePhase.Draft)
            {
                // In draft phase, delegate to DraftCardSelection if available
                DraftCardSelection draftSelection = GetComponent<DraftCardSelection>();
                if (draftSelection != null)
                {
                    Debug.Log($"HandleCardPlay: Delegating to DraftCardSelection for card {gameObject.name}");
                    draftSelection.OnCardClicked();
                    return;
                }
                else
                {
                    Debug.LogWarning($"HandleCardPlay: Card {gameObject.name} clicked in draft phase but no DraftCardSelection component found");
                    return;
                }
            }
            else if (currentPhase == GamePhaseManager.GamePhase.Combat)
            {
                // In combat phase, check if card is draftable (should not be playable in combat)
                if (card != null && card.IsDraftable)
                {
                    Debug.Log($"HandleCardPlay: Card {gameObject.name} is draftable and cannot be played in combat");
                    return;
                }
                
                // Continue with normal combat card play logic
                Debug.Log($"HandleCardPlay: Processing combat card play for {gameObject.name}");
            }
            else
            {
                Debug.Log($"HandleCardPlay: Card {gameObject.name} clicked in {currentPhase} phase - no action taken");
                return;
            }
        }
        else
        {
            Debug.LogWarning($"HandleCardPlay: GamePhaseManager not found, assuming combat phase for card {gameObject.name}");
        }
        
        // Prevent double processing
        if (isProcessingCardPlay)
        {
            Debug.Log($"HandleCardPlay: Card {gameObject.name} is already being processed. Ignoring this click.");
            return;
        }
        
        // Validate basic conditions for combat card play
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

        // Set the processing flag to prevent double processing
        isProcessingCardPlay = true;

        // Card is valid to be clicked - Update source and target references
        if (sourceAndTargetIdentifier != null)
        {
            Debug.Log($"HandleCardPlay: Triggering UpdateSourceAndTarget for card {gameObject.name}");
            sourceAndTargetIdentifier.UpdateSourceAndTarget();
            
            // Verify we have valid source and target
            if (sourceAndTargetIdentifier.SourceEntity == null || sourceAndTargetIdentifier.TargetEntity == null)
            {
                Debug.LogError($"HandleCardPlay: Missing source or target entity for card {gameObject.name}");
                isProcessingCardPlay = false; // Reset the flag if we can't play the card
                return;
            }
            
            // Now that we have valid source and target, play the card
            PlayCard();
        }
        else
        {
            Debug.LogError($"HandleCardPlay: Missing SourceAndTargetIdentifier component for card {gameObject.name}");
            isProcessingCardPlay = false; // Reset the flag if we can't play the card
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
        
        // Handle energy cost on server using EnergyHandler
        if (cardData != null)
        {
            CmdSpendEnergy(owner.ObjectId, cardData.EnergyCost);
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
        
        // Card play is complete - reset the flag but with a slight delay to ensure all RPCs have been sent
        Invoke("ResetProcessingFlag", 0.5f);
    }
    
    private void ResetProcessingFlag()
    {
        isProcessingCardPlay = false;
        Debug.Log($"HandleCardPlay: Processing completed for card {gameObject.name}");
    }
    
    [ServerRpc]
    private void CmdSpendEnergy(int ownerEntityId, int energyCost)
    {
        // Find the entity by ID
        NetworkEntity entity = FindEntityById(ownerEntityId);
        if (entity == null)
        {
            Debug.LogError($"HandleCardPlay: Could not find entity with ID {ownerEntityId}");
            return;
        }
        
        // Get the EnergyHandler and use it to spend energy
        EnergyHandler energyHandler = entity.GetComponent<EnergyHandler>();
        if (energyHandler != null)
        {
            energyHandler.SpendEnergy(energyCost, null); // No source entity for the energy spend
            Debug.Log($"HandleCardPlay: Deducted {energyCost} energy from {entity.EntityName.Value} via EnergyHandler");
        }
        else
        {
            Debug.LogError($"HandleCardPlay: Owner {entity.EntityName.Value} has no EnergyHandler component");
            
            // Fallback: directly update energy if no EnergyHandler is available
            entity.ChangeEnergy(-energyCost);
            Debug.Log($"HandleCardPlay: Fallback - Directly deducted {energyCost} energy from {entity.EntityName.Value}. New energy: {entity.CurrentEnergy.Value}");
        }
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

    /// <summary>
    /// Server-side method to play a card for AI-controlled entities
    /// </summary>
    [Server]
    public void ServerPlayCard()
    {
        if (!IsServerInitialized) 
        {
            Debug.LogError($"HandleCardPlay: Cannot call ServerPlayCard for card {gameObject.name} - server not initialized");
            return;
        }

        Debug.Log($"HandleCardPlay: ServerPlayCard called for card {gameObject.name}");
        
        // Prevent double processing
        if (isProcessingCardPlay)
        {
            Debug.Log($"HandleCardPlay: Card {gameObject.name} is already being processed. Ignoring this request.");
            return;
        }
        
        // Set the processing flag to prevent double processing
        isProcessingCardPlay = true;

        // Verify we have valid source and target from SourceAndTargetIdentifier
        if (sourceAndTargetIdentifier == null || 
            sourceAndTargetIdentifier.SourceEntity == null || 
            sourceAndTargetIdentifier.TargetEntity == null)
        {
            Debug.LogError($"HandleCardPlay: Missing source or target entity for card {gameObject.name}");
            isProcessingCardPlay = false; // Reset the flag
            return;
        }
        
        // Get owner entity (should be the source entity from SourceAndTargetIdentifier)
        NetworkEntity owner = sourceAndTargetIdentifier.SourceEntity;
        
        // Handle energy cost
        if (cardData != null)
        {
            // Direct energy cost handling for server-side play
            owner.ChangeEnergy(-cardData.EnergyCost);
            Debug.Log($"HandleCardPlay: Deducted {cardData.EnergyCost} energy from {owner.EntityName.Value}. New energy: {owner.CurrentEnergy.Value}");
        }
        
        // Resolve the card effect
        if (cardEffectResolver != null)
        {
            Debug.Log($"HandleCardPlay: Calling cardEffectResolver.ServerResolveCardEffect() for card {gameObject.name}");
            cardEffectResolver.ServerResolveCardEffect(sourceAndTargetIdentifier.SourceEntity, sourceAndTargetIdentifier.TargetEntity, card.CardData);
        }
        else
        {
            Debug.LogError($"HandleCardPlay: Missing CardEffectResolver component for card {gameObject.name}");
        }
        
        // Move the card to the discard pile
        HandManager handManager = owner.GetComponent<HandManager>();
        if (handManager != null)
        {
            handManager.DiscardCard(gameObject);
            Debug.Log($"HandleCardPlay: Moved card {gameObject.name} to discard pile");
        }
        else
        {
            Debug.LogError($"HandleCardPlay: Owner {owner.EntityName.Value} has no HandManager component");
        }
        
        // Reset the flag
        isProcessingCardPlay = false;
    }
} 