using UnityEngine;
using FishNet.Object; // For NetworkBehaviour type checking

/// <summary>
/// Applies card effects to entity targets during combat, such as damage, healing, and status effects.
/// Attach to: NetworkEntity prefabs to handle receiving effects from played cards.
/// </summary>
public class EffectManager : MonoBehaviour
{
    private NetworkBehaviour parentEntity; // Reference to the NetworkEntity this is attached to

    private void Awake()
    {
        // Get the parent NetworkBehaviour (NetworkEntity)
        parentEntity = GetComponent<NetworkEntity>() as NetworkBehaviour;

        if (parentEntity == null)
        {
            Debug.LogError("EffectManager: Not attached to a NetworkEntity. This component must be attached to one.");
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

        NetworkEntity targetEntity = target as NetworkEntity;

        switch (cardData.EffectType)
        {
            case CardEffectType.Damage:
                if (targetEntity != null) targetEntity.TakeDamage(cardData.Amount);
                break;

            case CardEffectType.Heal:
                if (targetEntity != null) targetEntity.Heal(cardData.Amount);
                break;

            case CardEffectType.BuffStats:
                // Example: Increase MaxHealth or temporarily buff attack/defense
                // This would require adding relevant fields and methods to NetworkEntity
                // e.g., targetEntity.ApplyBuff(StatType.MaxHealth, cardData.Amount, duration);
                Debug.Log($"BuffStats effect for {cardData.Amount} not fully implemented.");
                break;

            case CardEffectType.DebuffStats:
                Debug.Log($"DebuffStats effect for {cardData.Amount} not fully implemented.");
                break;

            case CardEffectType.ApplyStatus:
                // Example: targetEntity.ApplyStatusEffect(StatusType.Poison, cardData.Amount); // Amount could be duration or strength
                // CurrentStatuses on NetworkEntity is a simple string, would need parsing and management.
                string statusToApply = cardData.CardName; // Or a specific status property on the card
                if (targetEntity != null) 
                {
                    if (string.IsNullOrEmpty(targetEntity.CurrentStatuses.Value)) targetEntity.CurrentStatuses.Value = statusToApply;
                    else targetEntity.CurrentStatuses.Value += "," + statusToApply;
                }
                Debug.Log($"Applied status '{statusToApply}' effect. (Simplified)");
                break;

            default:
                Debug.LogWarning($"Unhandled effect type: {cardData.EffectType}");
                break;
        }

        // Changes to SyncVars on NetworkEntity (like CurrentHealth) will automatically sync to clients.
        // CombatManager might send an RPC to notify clients that an effect was visually applied (e.g., for animations).
    }
} 