using UnityEngine;
using FishNet.Object; // For NetworkBehaviour type checking

/// <summary>
/// Applies card effects to entity targets during combat, such as damage, healing, and status effects.
/// Attach to: Both NetworkPlayer and NetworkPet prefabs to handle receiving effects from played cards.
/// </summary>
public class EffectManager : MonoBehaviour
{
    private NetworkBehaviour parentEntity; // Reference to the NetworkPlayer or NetworkPet this is attached to

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
            Debug.LogError("EffectManager: Not attached to a NetworkPlayer or NetworkPet. This component must be attached to one of these.");
        }
    }

    // Server-side method to apply a card's effect
    public void ApplyEffect(NetworkBehaviour target, CardData cardData)
    {
        if (parentEntity == null || target == null || cardData == null)
        {
            Debug.LogError("ApplyEffect: Parent Entity, Target, or CardData is null.");
            return;
        }

        // Ensure this runs on the server where state changes are authoritative
        if (!parentEntity.IsServerStarted)
        {
            Debug.LogWarning("ApplyEffect called on client. Effects should be applied on the server.");
            return;
        }

        Debug.Log($"EffectManager: Applying '{cardData.CardName}' (Effect: {cardData.EffectType}, Amount: {cardData.Amount}) from {parentEntity.name} to {target.name}");

        NetworkPlayer targetPlayer = target as NetworkPlayer;
        NetworkPet targetPet = target as NetworkPet;

        switch (cardData.EffectType)
        {
            case CardEffectType.Damage:
                if (targetPlayer != null) targetPlayer.TakeDamage(cardData.Amount);
                else if (targetPet != null) targetPet.TakeDamage(cardData.Amount);
                break;

            case CardEffectType.Heal:
                if (targetPlayer != null) targetPlayer.Heal(cardData.Amount);
                else if (targetPet != null) targetPet.Heal(cardData.Amount);
                break;

            case CardEffectType.DrawCard:
                // Find the target's HandManager component
                HandManager targetHandManager = target.GetComponent<HandManager>();
                if (targetHandManager != null) 
                {
                    for(int i = 0; i < cardData.Amount; i++) 
                    {
                        targetHandManager.DrawOneCard();
                    }
                } 
                else 
                {
                    Debug.LogError("HandManager component not found on target for DrawCard effect.");
                }
                break;

            case CardEffectType.BuffStats:
                // Example: Increase MaxHealth or temporarily buff attack/defense
                // This would require adding relevant fields and methods to NetworkPlayer/Pet
                // e.g., targetPlayer.ApplyBuff(StatType.MaxHealth, cardData.Amount, duration);
                Debug.Log($"BuffStats effect for {cardData.Amount} not fully implemented.");
                break;

            case CardEffectType.DebuffStats:
                Debug.Log($"DebuffStats effect for {cardData.Amount} not fully implemented.");
                break;

            case CardEffectType.ApplyStatus:
                // Example: targetPlayer.ApplyStatusEffect(StatusType.Poison, cardData.Amount); // Amount could be duration or strength
                // CurrentStatuses on NetworkPlayer/Pet is a simple string, would need parsing and management.
                string statusToApply = cardData.CardName; // Or a specific status property on the card
                if (targetPlayer != null) 
                {
                    if (string.IsNullOrEmpty(targetPlayer.CurrentStatuses.Value)) targetPlayer.CurrentStatuses.Value = statusToApply;
                    else targetPlayer.CurrentStatuses.Value += "," + statusToApply;
                }
                else if (targetPet != null) 
                {
                    if (string.IsNullOrEmpty(targetPet.CurrentStatuses.Value)) targetPet.CurrentStatuses.Value = statusToApply;
                    else targetPet.CurrentStatuses.Value += "," + statusToApply;
                }
                Debug.Log($"Applied status '{statusToApply}' effect. (Simplified)");
                break;

            default:
                Debug.LogWarning($"Unhandled effect type: {cardData.EffectType}");
                break;
        }

        // Changes to SyncVars on NetworkPlayer/Pet (like CurrentHealth) will automatically sync to clients.
        // CombatManager might send an RPC to notify clients that an effect was visually applied (e.g., for animations).
    }
} 