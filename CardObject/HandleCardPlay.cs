using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
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
        
        Debug.Log($"HandleCardPlay: Successfully set owner entity to {ownerObject.name}");
    }
    
    private void Awake()
    {
        // Find the FightManager in the scene
        fightManager = FightManager.Instance;
    }

    /// <summary>
    /// Internal method to determine the target for this card
    /// </summary>
    private bool TryGetTarget(out NetworkBehaviour target)
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
        
        Debug.LogError($"No target found for {parentEntity.name} in FightManager.");
        return false;
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
        
        // Find the target
        if (!TryGetTarget(out NetworkBehaviour target))
        {
            return false;
        }
        
        // Apply the effect using the target's EffectManager
        EffectManager targetEffectManager = target.GetComponent<EffectManager>();
        if (targetEffectManager != null)
        {
            // Apply the effect to the target
            targetEffectManager.ApplyEffect(parentEntity, cardData);
            
            string entityName = parentEntity is NetworkPlayer player ? player.PlayerName.Value : 
                               (parentEntity as NetworkPet).PetName.Value;
            
            Debug.Log($"{entityName} applied card effect {cardData.CardName} on {target.name}");
            return true;
        }
        else
        {
            Debug.LogError($"EffectManager component not found on target {target.name}");
            return false;
        }
    }
} 