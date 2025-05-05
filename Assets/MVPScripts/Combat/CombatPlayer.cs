using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CombatPlayer : NetworkBehaviour
{
    public new NetworkPlayer Owner;

    void Start()
    {
        NetworkPlayer[] players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        foreach (NetworkPlayer player in players)
        {
            if (player.OwnerClientId == OwnerClientId)
            {
                Owner = player;
                CombatManager.Instance.AddPlayer(this);
                Pet = FindFirstObjectByType<Pet>();
                Pet?.SetOwner(this);
                break;
            }
        }
    }

    public void DiscardCard(ulong cardInstanceID, ulong targetPlayerId)
    {
        NetworkLog.LogInfoServer($"Discarding card {cardInstanceID} for player {targetPlayerId}");
        Card cardToDiscard = FindObjectsByType<Card>(FindObjectsSortMode.None).ToList().Find(c => c.CardID == cardInstanceID);
        if (cardToDiscard != null)
        {
            CombatCanvasManager combatCanvas = FindFirstObjectByType<CombatCanvasManager>();
            if (combatCanvas != null)
            {
                combatCanvas.TargetDiscardCardClientRpc(cardInstanceID, targetPlayerId, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { targetPlayerId } } });
            }
        }
    }

    public void UpdateHandUI()
    {
        CombatCanvasManager canvasManager = FindFirstObjectByType<CombatCanvasManager>();
        if (canvasManager != null)
        {
            List<CardData> cardDataList = PlayerHand.CardsInHand.Select(card => card.CardData).ToList();
            // ... existing code ...
        }
    }
} 