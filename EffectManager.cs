using UnityEngine;
using FishNet.Object; // For NetworkBehaviour type checking

// Not a MonoBehaviour. Instantiated by CombatManager.
public class EffectManager
{
    public EffectManager()
    {
        // Constructor if needed
    }

    // Server-side method to apply a card's effect
    public void ApplyEffect(NetworkBehaviour caster, NetworkBehaviour target, Card cardData)
    {
        if (caster == null || target == null || cardData == null)
        {
            Debug.LogError("ApplyEffect: Caster, Target, or CardData is null.");
            return;
        }

        // Ensure this runs on the server where state changes are authoritative
        if (!caster.IsServerStarted)
        {
            Debug.LogWarning("ApplyEffect called on client. Effects should be applied on the server.");
            return;
        }

        Debug.Log($"EffectManager: Applying '{cardData.CardName}' (Effect: {cardData.EffectType}, Amount: {cardData.Amount}) from {caster.name} to {target.name}");

        NetworkPlayer targetPlayer = target as NetworkPlayer;
        NetworkPet targetPet = target as NetworkPet;
        // Caster could also be player or pet
        // NetworkPlayer casterPlayer = caster as NetworkPlayer;
        // NetworkPet casterPet = caster as NetworkPet;

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
                // The caster draws cards. CombatManager will need a reference to HandManager.
                // This shows a dependency that CombatManager might need to expose HandManager or call it.
                HandManager handManager = CombatManager.Instance.GetComponent<CombatManager>()?.handManager; // Assuming CombatManager has public handManager
                if (handManager != null) {
                    for(int i = 0; i < cardData.Amount; i++) {
                         handManager.DrawOneCard(caster);
                    }
                } else {
                     Debug.LogError("HandManager instance not found on CombatManager for DrawCard effect.");
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
                string statusToApply = cardData.name; // Or a specific status property on the card
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