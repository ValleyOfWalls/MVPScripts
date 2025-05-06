using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using Steamworks; // For potentially linking to Steam ID

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

    // StarterDeck should be a list of Card ScriptableObjects or identifiers
    // For simplicity, let's assume it's a list of Card script references if Card is a MonoBehaviour on a prefab
    // Or better, use CardData (struct/class) if Card is not a MonoBehaviour itself.
    [SerializeField] public List<Card> StarterDeckPrefabs = new List<Card>(); // Assign Card prefabs in Inspector

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
        InitializeDeck();
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

    [Server]
    private void InitializeDeck()
    {
        currentDeckCardIds.Clear();
        playerHandCardIds.Clear();
        discardPileCardIds.Clear();

        if (StarterDeckPrefabs != null)
        {
            foreach (Card cardPrefab in StarterDeckPrefabs)
            {
                if (cardPrefab != null)
                {
                    // Assuming Card script has a unique ID or we use its instance ID (if it were a ScriptableObject)
                    // For simplicity, let's assume Card has an ID field we can use or we generate one.
                    // This needs a proper Card Data system.
                    // For now, let's placeholder with a simple hash or a predefined ID on the Card script.
                    currentDeckCardIds.Add(cardPrefab.GetInstanceID()); // Placeholder - use a proper card ID system
                }
            }
        }
        Debug.Log($"Player {PlayerName.Value} (ID: {ObjectId}) deck initialized with {currentDeckCardIds.Count} cards.");
        // ShuffleDeck(); // Implement shuffling logic here
    }

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

    // Further methods for drawing cards, playing cards, etc., will be managed by CombatManager/HandManager
    // but might involve ServerRpc calls to this script to modify its state (e.g., card lists).
} 