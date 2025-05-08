using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using Steamworks; // For potentially linking to Steam ID

/// <summary>
/// Represents a player entity in the networked game with health, energy, cards, and other game-related stats.
/// Attach to: The NetworkPlayerPrefab GameObject that is instantiated for each connected client.
/// </summary>
public class NetworkPlayer : NetworkBehaviour
{
    // Link to the owner connection (automatically handled by FishNet if spawned with ownership)
    // public NetworkConnection OwnerConnection => Owner; // FishNet provides Owner

    public readonly SyncVar<string> PlayerName = new SyncVar<string>();

    // Max stats can be read from GameManager or set at spawn
    public readonly SyncVar<int> MaxHealth = new SyncVar<int>();
    public readonly SyncVar<int> MaxEnergy = new SyncVar<int>();

    public readonly SyncVar<int> CurrentHealth = new SyncVar<int>();
    public readonly SyncVar<int> CurrentEnergy = new SyncVar<int>();

    // Example for statuses. You might want a more complex type or a list of status effect objects.
    public readonly SyncVar<string> CurrentStatuses = new SyncVar<string>();

    // Add Currency system
    public readonly SyncVar<int> Currency = new SyncVar<int>();
    
    // Event for currency changes
    public event System.Action<int> OnCurrencyChanged;

    // StarterDeck should be a list of Card ScriptableObjects or identifiers
    // For simplicity, let's assume it's a list of Card script references if Card is a MonoBehaviour on a prefab
    // Or better, use CardData (struct/class) if Card is not a MonoBehaviour itself.
    [SerializeField] public DeckData StarterDeckDefinition; // Assign DeckData SO in Inspector

    // CurrentDeck and PlayerHand will manage instantiated Card objects.
    public readonly SyncList<int> currentDeckCardIds = new SyncList<int>(); 
    public readonly SyncList<int> playerHandCardIds = new SyncList<int>();
    public readonly SyncList<int> discardPileCardIds = new SyncList<int>();

    // Reference to the physical hand object in the player's hierarchy (visual only, not for direct card list syncing)
    [SerializeField] public Transform PlayerHandTransform; // Assign in prefab inspector
    [SerializeField] public Transform DeckTransform; // Assign in prefab inspector
    [SerializeField] public Transform DiscardTransform; // Assign in prefab inspector

    private GameManager gameManager;

    public override void OnStartServer()
    {
        base.OnStartServer();
        GameManager gameManager = FindFirstObjectByType<GameManager>(); // Updated FindObjectOfType
        if (gameManager != null)
        {
            MaxHealth.Value = gameManager.PlayerMaxHealth.Value;
            MaxEnergy.Value = gameManager.PlayerMaxEnergy.Value;
        }
        else Debug.LogWarning("GameManager not found in NetworkPlayer.OnStartServer. Max stats will use defaults.");
        
        // Set defaults if GameManager didn't override
        if(MaxHealth.Value == 0) MaxHealth.Value = 100;
        if(MaxEnergy.Value == 0) MaxEnergy.Value = 3;
        if(PlayerName.Value == null) PlayerName.Value = "DefaultPlayerName";

        CurrentHealth.Value = MaxHealth.Value;
        CurrentEnergy.Value = MaxEnergy.Value;
        // InitializeDeck method removed - now handled by EntityDeckSetup
    }

    public override void OnStartClient()
    { 
        base.OnStartClient();
        // Client-side initialization if needed, often for UI or visual setup based on SyncVar values
        // For example, if player name comes from Steam
        if (IsOwner) // Only the owner client might fetch their specific Steam name
        {
            // CmdSetPlayerName(SteamFriends.GetPersonaName()); // Example RPC to set name from client
        }
        // Cards are typically handled by server commands and RPCs to clients for visual updates.
        // The SyncLists for card IDs will automatically update on clients.
    }

    // InitializeDeck method removed - now handled by EntityDeckSetup

    // Example method to set player name (could be called after Steam authentication)
    [ServerRpc(RequireOwnership = false)] // Owner client can call this, or server can set it directly
    public void CmdSetPlayerName(string name, NetworkConnection sender = null) // sender is automatically populated if client calls
    {
        PlayerName.Value = name;
        // If called from client RPC, sender will be the client's connection.
        // Can do validation here if needed.
    }

    [Server]
    public void TakeDamage(int amount)
    {
        CurrentHealth.Value -= amount;
        if (CurrentHealth.Value <= 0)
        {
            CurrentHealth.Value = 0;
            // Handle player death
            Debug.Log($"Player {PlayerName.Value} has been defeated.");
        }
    }

    [Server]
    public void Heal(int amount)
    {
        CurrentHealth.Value += amount;
        if (CurrentHealth.Value > MaxHealth.Value) CurrentHealth.Value = MaxHealth.Value;
    }

    [Server]
    public void ChangeEnergy(int amount)
    {
        CurrentEnergy.Value += amount;
        if (CurrentEnergy.Value < 0) CurrentEnergy.Value = 0;
        if (CurrentEnergy.Value > MaxEnergy.Value) CurrentEnergy.Value = MaxEnergy.Value;
    }

    [Server]
    public void ReplenishEnergy(){
        CurrentEnergy.Value = MaxEnergy.Value;
    }

    [Server]
    public void AddCurrency(int amount)
    {
        Currency.Value += amount;
        if (IsClientInitialized)
        {
            OnCurrencyChanged?.Invoke(Currency.Value);
        }
    }

    [Server]
    public void DeductCurrency(int amount)
    {
        Currency.Value = Mathf.Max(0, Currency.Value - amount);
        if (IsClientInitialized)
        {
            OnCurrencyChanged?.Invoke(Currency.Value);
        }
    }

    [Server]
    public void SetCurrency(int amount)
    {
        Currency.Value = Mathf.Max(0, amount);
        if (IsClientInitialized)
        {
            OnCurrencyChanged?.Invoke(Currency.Value);
        }
    }

    [Server]
    public void IncreaseMaxHealth(int amount)
    {
        MaxHealth.Value += amount;
        CurrentHealth.Value += amount;
    }

    [Server]
    public void IncreaseMaxEnergy(int amount)
    {
        MaxEnergy.Value += amount;
        CurrentEnergy.Value += amount;
    }

    // Further methods for drawing cards, playing cards, etc., will be managed by CombatManager/HandManager
    // but might involve ServerRpc calls to this script to modify its state (e.g., card lists).

    [ServerRpc(RequireOwnership = true)]
    public void CmdAddCardToDeck(int cardId)
    {
        NetworkEntityDeck entityDeck = GetComponent<NetworkEntityDeck>();
        if (entityDeck != null)
        {
            entityDeck.AddCard(cardId);
            Debug.Log($"Player {PlayerName.Value} added card ID {cardId} to their deck via ServerRpc.");
        }
        else
        {
            Debug.LogError($"Cannot add card to deck: NetworkEntityDeck component not found on player {PlayerName.Value}.");
        }
    }
    
    [ServerRpc(RequireOwnership = true)]
    public void CmdRemoveCardFromDeck(int cardId)
    {
        NetworkEntityDeck entityDeck = GetComponent<NetworkEntityDeck>();
        if (entityDeck != null)
        {
            bool removed = entityDeck.RemoveCard(cardId);
            Debug.Log($"Player {PlayerName.Value} {(removed ? "removed" : "failed to remove")} card ID {cardId} from their deck via ServerRpc.");
        }
        else
        {
            Debug.LogError($"Cannot remove card from deck: NetworkEntityDeck component not found on player {PlayerName.Value}.");
        }
    }
} 