using FishNet.Object;

namespace Combat
{
    // Interface to represent common properties/methods of PlayerHand and PetHand if needed
    public interface IHand
    {
        NetworkObject NetworkObject { get; } // Example common property
        // Add other common members if necessary
        bool IsOwner { get; } // Useful for checks
        void ArrangeCardsInHand(); // Ensure both hands have this method accessible
    }
} 