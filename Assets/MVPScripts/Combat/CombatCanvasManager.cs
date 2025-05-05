// ... existing code ...
    {
        NetworkObject targetPlayerNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(targetClientId);
        CombatPlayer targetPlayer = targetPlayerNetworkObject.GetComponent<CombatPlayer>();
        if (targetPlayer != null && targetPlayer.IsOwner && targetPlayer.IsServerInitialized)
        {
            targetPlayer.CmdDiscardCardServerRpc(cardInstanceID, targetPlayerId);
        }
// ... existing code ...