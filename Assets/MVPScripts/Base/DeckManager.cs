using System.Collections.Generic;

public class DeckManager : MonoBehaviour
{
    private void SendCardsToPlayer(ulong clientId, List<string> cards)
    {
        if (!IsServerInitialized)
            return;

        // ... existing code ...
    }
} 