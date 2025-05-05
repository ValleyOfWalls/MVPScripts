using Unity.Netcode;

public class PlayerHand : NetworkBehaviour
{
    /// <summary>
    /// Client request server to remove a card from hand
    /// </summary>
    /// <param name="cardHandIndex"></param>
    [ServerRpc(RequireOwnership = false)]
    public new void CmdRemoveCardFromHand(int cardHandIndex, string cardID)
    {
        // ... existing code ...
    }

    [ServerRpc(RequireOwnership = false)]
    public void CmdSpawnCardsServerRpc(string[] cardIDs, ServerRpcParams rpcParams = default)
    {
        if (IsServerInitialized)
        {
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                // ... existing code ...
            };
        }
    }
} 