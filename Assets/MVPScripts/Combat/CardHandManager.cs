using Unity.Netcode;

public class CardHandManager : NetworkBehaviour
{
    [ServerRpc(RequireOwnership = false)]
    public void CmdUpdateCardsInHandServerRpc(string[] cardIDs, ServerRpcParams rpcParams = default)
    {
        if (!IsServerInitialized)
            return;
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            // ... existing code ...
        };
    }
} 