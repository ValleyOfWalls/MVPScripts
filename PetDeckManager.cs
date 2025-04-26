using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Collections;

public class PetDeckManager
{
    private GameManager gameManager;
    
    // Pet deck data
    private List<CardData> localPetDeck = new List<CardData>();
    private List<CardData> starterPetDeck = new List<CardData>();
    
    // Opponent pet state (for local simulation)
    private int opponentPetEnergy;
    private List<CardData> opponentPetDeck = new List<CardData>();
    private List<CardData> opponentPetHand = new List<CardData>();
    private List<CardData> opponentPetDiscard = new List<CardData>();
    
    // Card pool
    private List<CardData> allPetCardPool = new List<CardData>();
    
    public const string PLAYER_PET_DECK_PROP = "PetDeck";
    
    public PetDeckManager(GameManager gameManager, List<CardData> starterPetDeck, List<CardData> allPetCardPool)
    {
        this.gameManager = gameManager;
        this.starterPetDeck = new List<CardData>(starterPetDeck ?? new List<CardData>());
        this.allPetCardPool = new List<CardData>(allPetCardPool ?? new List<CardData>());
    }
    
    public void InitializeAndSyncLocalPetDeck()
    {
        if (localPetDeck == null || localPetDeck.Count == 0)
        {
            localPetDeck = new List<CardData>(starterPetDeck);
            Debug.Log($"InitializeAndSyncLocalPetDeck: Initialized localPetDeck from starter. Count: {localPetDeck.Count}");
            // Sync this initial state immediately
            SyncLocalPetDeckToCustomProperties();
        }
        else
        {
            Debug.Log("InitializeAndSyncLocalPetDeck: localPetDeck already initialized.");
        }
    }
    
    public void ShuffleLocalPetDeck()
    {
        System.Random rng = new System.Random();
        int n = localPetDeck.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            CardData value = localPetDeck[k];
            localPetDeck[k] = localPetDeck[n];
            localPetDeck[n] = value;
        }
        Debug.Log("Local Pet Deck shuffled.");
    }
    
    // Opponent Pet Deck Methods
    public void InitializeOpponentPetDeck(List<string> cardNames)
    {
        Debug.Log($"[PetDeckManager.InitializeOpponentPetDeck] STARTING - Reconstructing opponent pet deck.");
        opponentPetDeck.Clear(); // Clear previous round's deck

        if (cardNames == null || cardNames.Count == 0)
        {
            // No synced data found (first round? property missing?), use starter deck
            Debug.Log("InitializeOpponentPetDeck: No card names provided, using starter pet deck.");
            opponentPetDeck = new List<CardData>(starterPetDeck);
        }
        else
        {
            // Reconstruct the deck from the provided names
            Debug.Log($"InitializeOpponentPetDeck: Reconstructing deck from {cardNames.Count} names.");
            foreach (string cardName in cardNames)
            {
                CardData cardData = FindCardDataByName(cardName);
                if (cardData != null)
                {
                    opponentPetDeck.Add(cardData);
                }
                else
                {
                    Debug.LogWarning($"InitializeOpponentPetDeck: Could not find CardData for card name '{cardName}' while reconstructing opponent deck.");
                }
            }
        }

        // Ensure hand/discard are clear and shuffle the newly constructed deck
        opponentPetHand.Clear();
        opponentPetDiscard.Clear();
        ShuffleOpponentPetDeck(); // Shuffle the reconstructed or starter deck

        Debug.Log($"Opponent Pet Deck initialized with {opponentPetDeck.Count} cards. Hand ({opponentPetHand.Count}) and Discard ({opponentPetDiscard.Count}) cleared.");
    }
    
    public void ShuffleOpponentPetDeck()
    {
        System.Random rng = new System.Random();
        int n = opponentPetDeck.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            CardData value = opponentPetDeck[k];
            opponentPetDeck[k] = opponentPetDeck[n];
            opponentPetDeck[n] = value;
        }
        Debug.Log("Opponent Pet Deck shuffled.");
    }
    
    public void DrawOpponentPetHand(int amountToDraw)
    {
        opponentPetHand.Clear(); // Clear previous hand
        for (int i = 0; i < amountToDraw; i++)
        {
            DrawOpponentPetCard();
        }
        Debug.Log($"Opponent Pet drew {opponentPetHand.Count} cards.");
        
        // --- ADDED: Update Opponent Hand UI ---
        gameManager.GetCombatUIManager()?.UpdateOpponentPetHandUI();
        // --- END ADDED ---
    }
    
    public void DrawOpponentPetCard()
    {
        if (opponentPetDeck.Count == 0)
        {
            if (opponentPetDiscard.Count == 0)
            {
                Debug.Log("Opponent Pet has no cards left to draw!");
                return; // Out of cards
            }
            ReshuffleOpponentPetDiscardPile();
        }
        
        if (opponentPetDeck.Count > 0) // Check again after potential reshuffle
        {
            CardData drawnCard = opponentPetDeck[0];
            opponentPetDeck.RemoveAt(0);
            opponentPetHand.Add(drawnCard);
            
            // Assign a unique ID to the card
            gameManager.GetCardManager().AssignUniqueIdToOpponentPetCard(drawnCard);
        }
    }
    
    public void DiscardOpponentPetHand()
    {
        List<CardData> cardsToDiscard = new List<CardData>(opponentPetHand); // Create a copy
        opponentPetHand.Clear();
        foreach(CardData card in cardsToDiscard)
        {
            opponentPetDiscard.Add(card);
            
            // Re-assign unique ID when card moves to discard pile
            gameManager.GetCardManager().AssignUniqueIdToOpponentPetCard(card);
        }
        // --- ADDED: Update Opponent Hand UI ---
        gameManager.GetCombatUIManager()?.UpdateOpponentPetHandUI();
        // --- END ADDED ---
    }
    
    public void DiscardRandomOpponentPetCards(int amount)
    {
        System.Random rng = new System.Random();
        int cardsToDiscardCount = Mathf.Min(amount, opponentPetHand.Count); 
        
        for (int i = 0; i < cardsToDiscardCount; i++)
        {
            if (opponentPetHand.Count == 0) break; 
            
            int randomIndex = rng.Next(opponentPetHand.Count);
            CardData cardToDiscard = opponentPetHand[randomIndex];
            opponentPetHand.RemoveAt(randomIndex);
            opponentPetDiscard.Add(cardToDiscard);
            
            // Re-assign unique ID when card moves to discard pile
            // This is important for cards that might have been drawn without getting a unique ID
            gameManager.GetCardManager().AssignUniqueIdToOpponentPetCard(cardToDiscard);
            
            Debug.Log($"Randomly discarded opponent pet card: {cardToDiscard.cardName}");
        }
    }
    
    public void ReshuffleOpponentPetDiscardPile()
    {
        Debug.Log($"[PetDeckManager.ReshuffleOpponentPetDiscardPile] STARTING - Reshuffling {opponentPetDiscard.Count} cards into deck (current size: {opponentPetDeck.Count})");
        opponentPetDeck.AddRange(opponentPetDiscard);
        opponentPetDiscard.Clear();
        ShuffleOpponentPetDeck();
        Debug.Log($"[PetDeckManager.ReshuffleOpponentPetDiscardPile] END - Deck size: {opponentPetDeck.Count}, Discard size: {opponentPetDiscard.Count}");
    }

    // MODIFIED: Returns IEnumerator to allow yielding
    public IEnumerator ExecuteOpponentPetTurn(int startingPetEnergy) // Parameter is now fallback
    {
        Debug.Log("---> Starting Opponent Pet Turn <--- (Fallback Energy: " + startingPetEnergy + ")");

        // NOTE: Opponent Pet Energy is now reset at the START of the *player's* turn in CombatManager.StartTurn

        Player opponentPetOwner = gameManager.GetPlayerManager().GetOpponentPlayer(); // The player whose pet we are simulating

        // Determine starting energy based on Owner's Property 
        // NOTE: We now reset energy above. Reading from property is removed to ensure reset.
        // If energy carry-over is desired later, this logic would need to be revisited.
        
        // Process Turn Start Effects (handled by PlayerManager)
        gameManager.GetPlayerManager().ProcessOpponentPetTurnStartEffects();
        
        // Determine how many cards the pet should draw
        // int petCardsToDraw = 5; // MODIFIED: Draw 5 cards, like the player
        // DrawOpponentPetHand(petCardsToDraw); // MOVED to CombatManager.StartTurn
        
        // Simple AI: Play cards until out of energy or no playable cards left
        bool cardPlayedThisLoop;
        do
        {
            cardPlayedThisLoop = false;
            CardData cardToPlay = null;
            int cardIndex = -1;
            
            // --- MODIFIED AI: Find ALL playable cards and choose randomly ---
            List<(CardData card, int index)> playableCards = new List<(CardData, int)>();
            for(int i = 0; i < opponentPetHand.Count; i++)
            {
                if (opponentPetHand[i].cost <= opponentPetEnergy)
                {
                    playableCards.Add((opponentPetHand[i], i));
                }
            }
            
            // If any playable cards were found
            if (playableCards.Count > 0)
            {
                // Choose one randomly
                System.Random rng = new System.Random();
                int randomIndex = rng.Next(playableCards.Count);
                (cardToPlay, cardIndex) = playableCards[randomIndex];

                // Yield the card for visualization FIRST
                Debug.Log($"Opponent Pet wants to play card: {cardToPlay.cardName} (Cost: {cardToPlay.cost}). Yielding for visualization/effect.");
                yield return cardToPlay;
                
                // --- Code resumes AFTER CombatManager finishes visualizing and applying effects ---
                Debug.Log($"Resuming Opponent Pet turn after yielding card: {cardToPlay.cardName}");

                // NOW deduct energy and update hand/discard
                opponentPetEnergy -= cardToPlay.cost;
                opponentPetHand.Remove(cardToPlay);
                opponentPetDiscard.Add(cardToPlay);
                
                // Ensure the card has a unique ID when played and when added to discard
                gameManager.GetCardManager().AssignUniqueIdToOpponentPetCard(cardToPlay);
                
                cardPlayedThisLoop = true; // Indicate a card was processed, loop again
                
                // --- ADDED: Update Opponent Hand UI ---
                gameManager.GetCombatUIManager()?.UpdateOpponentPetHandUI();
                // --- END ADDED ---
                
                Debug.Log($"Opponent Pet energy remaining after playing {cardToPlay.cardName}: {opponentPetEnergy}");
                gameManager.UpdateHealthUI(); // Update UI to show energy change *after* card play completes
            }
            
        } while (cardPlayedThisLoop && opponentPetEnergy > 0); // Continue if a card was played and energy remains
        
        Debug.Log("Opponent Pet finished playing cards.");
        DiscardOpponentPetHand();
        
        // --- ADDED: Decrement buffs and update UI at end of turn ---
        gameManager.GetPlayerManager()?.GetHealthManager()?.DecrementOpponentPetCritBuffDurations();
        // Decrement other buffs here if needed (e.g., DoT, HoT turns are usually handled at turn START)
        gameManager.UpdateHealthUI(); // Update UI after buffs are decremented
        // --- END ADDED ---
        
        Debug.Log("---> Ending Opponent Pet Turn <---");
        // Update UI called within DiscardOpponentPetHand
        yield break; // Ensure coroutine yields null at the end
    }

    private void SyncLocalPetDeckToCustomProperties()
    {
        // Convert List<CardData> to List<string> (card names)
        List<string> petDeckCardNames = localPetDeck.Select(card => card.cardName).ToList();

        // Serialize the list of names
        string petDeckJson = JsonConvert.SerializeObject(petDeckCardNames);

        // Set the custom property for the local player
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable { { PLAYER_PET_DECK_PROP, petDeckJson } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        Debug.Log($"Synced local pet deck to Player Properties. {petDeckCardNames.Count} cards. JSON: {petDeckJson}");
    }

    public void AddCardToPetDeck(CardData card)
    {
        if (card != null)
        {
            localPetDeck.Add(card);
            SyncLocalPetDeckToCustomProperties();
            Debug.Log($"Added {card.cardName} to pet deck. Current count: {localPetDeck.Count}");
        }
    }

    public bool UpgradePetCard(CardData cardToUpgrade, CardData upgradedVersion)
    {
        int index = localPetDeck.FindIndex(card => card == cardToUpgrade);
        if (index == -1)
        {
            // Fallback by name matching
            index = localPetDeck.FindIndex(card => card.cardName == cardToUpgrade.cardName && card.upgradedVersion != null);
        }
        
        if (index != -1 && upgradedVersion != null)
        {
            localPetDeck[index] = upgradedVersion;
            SyncLocalPetDeckToCustomProperties();
            return true;
        }
        return false;
    }

    public CardData FindCardDataByName(string cardName)
    {
        // First check pet card pools
        CardData found = allPetCardPool.FirstOrDefault(c => c.cardName == cardName);
        if (found == null)
        {
            found = starterPetDeck.FirstOrDefault(c => c.cardName == cardName);
        }
        
        // Check upgraded versions
        if (found == null)
        {
            foreach (var card in allPetCardPool.Concat(starterPetDeck))
            {
                if (card.upgradedVersion != null && card.upgradedVersion.cardName == cardName)
                {
                    return card.upgradedVersion;
                }
            }
        }
        
        return found;
    }

    // Getters
    public List<CardData> GetLocalPetDeck() => localPetDeck;
    public List<CardData> GetOpponentPetDeck() => opponentPetDeck;
    public List<CardData> GetOpponentPetHand() => opponentPetHand;
    public List<CardData> GetOpponentPetDiscard() => opponentPetDiscard;
    public List<CardData> GetAllOwnedPetCards() => new List<CardData>(localPetDeck);
    public int GetOpponentPetEnergy() => opponentPetEnergy;
    public void SetOpponentPetEnergy(int energy) => opponentPetEnergy = energy;

    // --- ADDED: Method to add energy to opponent pet and notify owner --- 
    public void AddEnergyToOpponentPet(int amount)
    {
        if (amount <= 0) return;
        
        // Update local simulation immediately
        opponentPetEnergy += amount;
        Debug.Log($"Added {amount} energy to Opponent Pet (local sim). New total: {opponentPetEnergy}");
        gameManager.UpdateHealthUI(); // ADDED: Update UI immediately

        // Notify the actual owner of the pet
        Player opponentPetOwner = gameManager.GetPlayerManager().GetOpponentPlayer(); // The player whose pet we are simulating
        if (PhotonNetwork.InRoom && opponentPetOwner != null)
        {
            // Send RPC to the specific opponent, telling them to add energy to *their* pet
            gameManager.GetPhotonView().RPC("RpcAddEnergyToLocalPet", opponentPetOwner, amount); 
            // Debug.Log($"Sent RpcAddEnergyToLocalPet({amount}) to {opponentPetOwner.NickName}.");
        }
        else if (opponentPetOwner == null)
        {
            Debug.LogWarning("AddEnergyToOpponentPet: Cannot send RPC, opponentPetOwner is null.");
        }
    }
    // --- END ADDED ---
}