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
    [Header("Health & Energy (Editable in Inspector for Debugging)")]
    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private int _maxEnergy = 3;
    [SerializeField] private int _currentHealth = 100;
    [SerializeField] private int _currentEnergy = 3;

    // SyncVars backed by serialized fields for easier debugging
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

    // CurrentDeck and PlayerHand will manage instantiated Card objects.
    [Header("Card Management")]
    [Tooltip("Card management is now handled by CombatDeck, CombatHand, and CombatDiscard components")]
    
    private GameManager gameManager;
    
    // Reference to RelationshipManager for client ID tracking
    private RelationshipManager relationshipManager;
    
    // Extension properties for backward compatibility
    public Transform PlayerHandTransform => GetComponent<NetworkPlayerUI>()?.GetPlayerHandTransform();
    public Transform DeckTransform => GetComponent<NetworkPlayerUI>()?.GetDeckTransform();
    public Transform DiscardTransform => GetComponent<NetworkPlayerUI>()?.GetDiscardTransform();
    
    // Quick access to client ID
    public int OwnerClientId 
    { 
        get 
        {
            if (relationshipManager != null)
                return relationshipManager.OwnerClientId;
                
            if (Owner != null)
                return Owner.ClientId;
                
            return -1;
        }
    }
    
    // Quick access to server ownership status
    public bool IsOwnedByServer
    {
        get
        {
            if (relationshipManager != null)
                return relationshipManager.IsOwnedByServer;
                
            if (Owner != null)
                return Owner.IsHost || Owner.ClientId == 0;
                
            return false;
        }
    }
    
    private void Awake()
    {
        relationshipManager = GetComponent<RelationshipManager>();
        if (relationshipManager == null)
        {
            relationshipManager = gameObject.AddComponent<RelationshipManager>();
            Debug.Log($"Added RelationshipManager to player {gameObject.name}");
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        GameManager gameManager = FindFirstObjectByType<GameManager>(); // Updated FindObjectOfType
        
        // Use serialized fields as initial values
        MaxHealth.Value = _maxHealth;
        MaxEnergy.Value = _maxEnergy;
        CurrentHealth.Value = _currentHealth;
        CurrentEnergy.Value = _currentEnergy;
        
        // Override with GameManager values if available
        if (gameManager != null)
        {
            MaxHealth.Value = gameManager.PlayerMaxHealth.Value;
            MaxEnergy.Value = gameManager.PlayerMaxEnergy.Value;
            CurrentHealth.Value = MaxHealth.Value;
            CurrentEnergy.Value = MaxEnergy.Value;
        }
        else Debug.LogWarning("GameManager not found in NetworkPlayer.OnStartServer. Using serialized field values.");
        
        // Set defaults if GameManager didn't override and serialized fields are zero
        if(MaxHealth.Value == 0) MaxHealth.Value = 100;
        if(MaxEnergy.Value == 0) MaxEnergy.Value = 3;
        if(PlayerName.Value == null) PlayerName.Value = "DefaultPlayerName";
        
        // Synchronize serialized fields with SyncVars
        _maxHealth = MaxHealth.Value;
        _maxEnergy = MaxEnergy.Value;
        _currentHealth = CurrentHealth.Value;
        _currentEnergy = CurrentEnergy.Value;
        
        // Update player name with client ID
        if (Owner != null)
        {
            PlayerName.Value = $"Player ({Owner.ClientId})";
        }
        else
        {
            PlayerName.Value = $"Player (No Owner)";
        }
    }

    public override void OnStartClient()
    { 
        base.OnStartClient();
        
        // Add more detailed ownership debugging, especially for host
        bool isLocalConnection = Owner == FishNet.InstanceFinder.ClientManager.Connection;
        bool isHostConnection = FishNet.InstanceFinder.IsHostStarted && Owner != null && Owner.ClientId == 0;
        
        Debug.Log($"NetworkPlayer OnStartClient - Name: {PlayerName.Value}, ID: {ObjectId}, IsOwner: {IsOwner}, " +
                  $"HasOwner: {Owner != null}, OwnerId: {(Owner != null ? Owner.ClientId : -1)}, " + 
                  $"IsLocalConnection: {isLocalConnection}, IsHostConnection: {isHostConnection}");
        
        // Register with EntityVisibilityManager if available
        GamePhaseManager gamePhaseManager = GamePhaseManager.Instance;
        if (gamePhaseManager != null)
        {
            EntityVisibilityManager entityVisManager = gamePhaseManager.GetComponent<EntityVisibilityManager>();
            if (entityVisManager != null)
            {
                entityVisManager.RegisterPlayer(this);
            }
            else
            {
                Debug.LogWarning($"NetworkPlayer: EntityVisibilityManager not found on GamePhaseManager for {PlayerName.Value}");
            }
        }
        else
        {
            // Try to find EntityVisibilityManager directly as fallback
            EntityVisibilityManager entityVisManager = FindFirstObjectByType<EntityVisibilityManager>();
            if (entityVisManager != null)
            {
                entityVisManager.RegisterPlayer(this);
            }
            else
            {
                Debug.LogWarning($"NetworkPlayer: Neither GamePhaseManager nor EntityVisibilityManager found for {PlayerName.Value}");
            }
        }
        
        // Client-side initialization if needed, often for UI or visual setup based on SyncVar values
        // Special handling for host
        if (IsOwner || (FishNet.InstanceFinder.IsHostStarted && isHostConnection)) 
        {
            Debug.Log($"NetworkPlayer: Identified as owned by local client - Name: {PlayerName.Value}, ID: {ObjectId}");
            
            // You can add host-specific logic here if needed
            // For example, if player name comes from Steam
            // CmdSetPlayerName(SteamFriends.GetPersonaName());
        }
        // Cards are typically handled by server commands and RPCs to clients for visual updates.
        // The SyncLists for card IDs will automatically update on clients.
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Unregister from EntityVisibilityManager when despawned
        GamePhaseManager gamePhaseManager = GamePhaseManager.Instance;
        if (gamePhaseManager != null)
        {
            EntityVisibilityManager entityVisManager = gamePhaseManager.GetComponent<EntityVisibilityManager>();
            if (entityVisManager != null)
            {
                entityVisManager.UnregisterPlayer(this);
            }
        }
        else
        {
            // Try to find EntityVisibilityManager directly as fallback
            EntityVisibilityManager entityVisManager = FindFirstObjectByType<EntityVisibilityManager>();
            if (entityVisManager != null)
            {
                entityVisManager.UnregisterPlayer(this);
            }
        }
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
        // Update serialized field
        _currentHealth = CurrentHealth.Value;
    }

    [Server]
    public void Heal(int amount)
    {
        CurrentHealth.Value += amount;
        if (CurrentHealth.Value > MaxHealth.Value) CurrentHealth.Value = MaxHealth.Value;
        // Update serialized field
        _currentHealth = CurrentHealth.Value;
    }

    [Server]
    public void ChangeEnergy(int amount)
    {
        CurrentEnergy.Value += amount;
        if (CurrentEnergy.Value < 0) CurrentEnergy.Value = 0;
        if (CurrentEnergy.Value > MaxEnergy.Value) CurrentEnergy.Value = MaxEnergy.Value;
        // Update serialized field
        _currentEnergy = CurrentEnergy.Value;
    }

    [Server]
    public void ReplenishEnergy(){
        CurrentEnergy.Value = MaxEnergy.Value;
        // Update serialized field
        _currentEnergy = CurrentEnergy.Value;
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
        // Update serialized fields
        _maxHealth = MaxHealth.Value;
        _currentHealth = CurrentHealth.Value;
    }

    [Server]
    public void IncreaseMaxEnergy(int amount)
    {
        MaxEnergy.Value += amount;
        CurrentEnergy.Value += amount;
        // Update serialized fields
        _maxEnergy = MaxEnergy.Value;
        _currentEnergy = CurrentEnergy.Value;
    }
    
    /// <summary>
    /// Notifies clients that a card has been played by this player (with specific instance ID)
    /// </summary>
    [ObserversRpc]
    public void NotifyCardPlayed(int cardId, string cardInstanceId)
    {
        if (!IsClientInitialized) return;
        
        Debug.Log($"NotifyCardPlayed received on client for card ID {cardId}, Instance ID {cardInstanceId}, IsOwner: {IsOwner}, IsLocalPlayer: {IsClientInitialized && IsOwner}");
        
        // Check if this object is still valid before proceeding (to prevent "server not active" warnings)
        if (!IsSpawned) 
        {
            Debug.LogWarning($"NotifyCardPlayed: NetworkPlayer object is no longer spawned. Ignoring card removal for card {cardId}, instance {cardInstanceId}");
            return;
        }
        
        // On client side, get the CardSpawner and tell it to remove the card
        CardSpawner cardSpawner = GetComponent<CardSpawner>();
        if (cardSpawner != null)
        {
            // Only remove cards from the local player's hand if this is the owner's player object
            // This ensures cards are only removed from the player who actually played them
            bool shouldProcessOnThisClient = IsOwner || IsClientOnlyInitialized || 
                                            (FishNet.InstanceFinder.IsHostStarted && ObjectId == GetComponent<NetworkObject>().ObjectId);
            
            if (shouldProcessOnThisClient)
            {
                // Catch any exceptions that might occur due to timing issues with despawning
                try
                {
                    // Use a more controlled approach to handle player card removal
                    if (!string.IsNullOrEmpty(cardInstanceId))
                    {
                        // Get all cards of this type for logging purposes
                        int sameIdCardCount = cardSpawner.GetCardCountForId(cardId);
                        Debug.Log($"Player {PlayerName.Value} has {sameIdCardCount} cards with ID {cardId} before removal");
                        
                        // Use the new method for server-confirmed card removal with instance ID
                        cardSpawner.OnServerConfirmCardPlayed(cardId, cardInstanceId);
                        Debug.Log($"Server confirmed card {cardId} (Instance: {cardInstanceId}) played - precisely removed from display");
                    }
                    else
                    {
                        // Generate a temporary instance ID for this notification to make it unique
                        string tempInstanceId = $"player_played_{cardId}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
                        Debug.Log($"No instance ID provided, using temp ID {tempInstanceId} to ensure only one card is removed");
                        
                        // Make sure to remove exactly one card
                        cardSpawner.RemoveOneCardOfType(cardId, tempInstanceId);
                        Debug.Log($"Server confirmed card {cardId} played - removing exactly one card instance");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Exception when removing card {cardId} (Instance: {cardInstanceId}): {ex.Message}");
                }
            }
            else
            {
                Debug.Log($"Ignoring card removal for non-owner player. Card: {cardId}, Instance: {cardInstanceId}, Player: {PlayerName.Value}, ObjectId: {ObjectId}");
            }
        }
        else
        {
            Debug.LogError("NetworkPlayer.NotifyCardPlayed: CardSpawner component not found.");
        }
    }

    /// <summary>
    /// Legacy version of NotifyCardPlayed - forwards to the instance-aware version with a generated instance ID
    /// </summary>
    [ObserversRpc]
    public void NotifyCardPlayed(int cardId)
    {
        if (!IsClientInitialized) return;
        
        Debug.Log($"NotifyCardPlayed (legacy) received on client for card ID {cardId}, forwarding to instance-aware version");
        
        // Generate a unique instance ID for backward compatibility
        string generatedInstanceId = $"player_{cardId}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
        
        // Call the instance-aware version
        NotifyCardPlayed(cardId, generatedInstanceId);
    }

    // Further methods for drawing cards, playing cards, etc., will be managed by CombatManager/HandManager
    // but might involve ServerRpc calls to this script to modify its state (e.g., card lists).
} 