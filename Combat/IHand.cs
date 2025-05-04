using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

namespace Combat
{
    /// <summary>
    /// Interface defining common functionality for card hands (player and pet hands)
    /// </summary>
    public interface IHand
    {
        /// <summary>
        /// The current number of cards in the hand
        /// </summary>
        int HandSize { get; }
        
        /// <summary>
        /// Arrange cards visually in the hand
        /// </summary>
        void ArrangeCardsInHand();
        
        /// <summary>
        /// Server RPC to draw the initial hand of cards
        /// </summary>
        /// <param name="count">Number of cards to draw</param>
        void DrawInitialHand(int count);
        
        /// <summary>
        /// Server-side method to draw cards into the hand
        /// </summary>
        /// <param name="count">Number of cards to draw</param>
        void DrawCards(int count);
        
        /// <summary>
        /// Set the deck this hand draws from
        /// </summary>
        /// <param name="deck">The runtime deck to use</param>
        void SetDeck(RuntimeDeck deck);
        
        /// <summary>
        /// Discard all cards in hand (server-side)
        /// </summary>
        void ServerDiscardHand();
        
        /// <summary>
        /// RPC to set the parent of this hand
        /// </summary>
        /// <param name="parentNetworkObject">The parent NetworkObject</param>
        void RpcSetParent(NetworkObject parentNetworkObject);
    }
} 