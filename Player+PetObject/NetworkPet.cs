using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using FishNet.Managing;

/// <summary>
/// Represents a pet entity in the networked game with health, energy, cards, and other game-related stats.
/// Attach to: The NetworkPetPrefab GameObject that is instantiated for each connected client's pet.
/// </summary>
public class NetworkPet : NetworkBehaviour
{
    // Link to the owner (could be a NetworkPlayer instance or the connection)
    // If a pet is always tied to a specific NetworkPlayer, you might sync the NetworkPlayer's ObjectId
    public readonly SyncVar<uint> OwnerPlayerObjectId = new SyncVar<uint>();

    public readonly SyncVar<string> PetName = new SyncVar<string>();

    [Header("Health & Energy (Editable in Inspector for Debugging)")]
    [SerializeField] private int _maxHealth = 50;
    [SerializeField] private int _maxEnergy = 2;
    [SerializeField] private int _currentHealth = 50;
    [SerializeField] private int _currentEnergy = 2;

    // SyncVars backed by serialized fields for easier debugging
    public readonly SyncVar<int> MaxHealth = new SyncVar<int>();
    public readonly SyncVar<int> MaxEnergy = new SyncVar<int>();
    public readonly SyncVar<int> CurrentHealth = new SyncVar<int>();
    public readonly SyncVar<int> CurrentEnergy = new SyncVar<int>();

    public readonly SyncVar<string> CurrentStatuses = new SyncVar<string>();

    public readonly SyncList<int> currentDeckCardIds = new SyncList<int>();
    public readonly SyncList<int> playerHandCardIds = new SyncList<int>(); // "PlayerHand" for pet refers to its own hand
    public readonly SyncList<int> discardPileCardIds = new SyncList<int>();

    private GameManager gameManager;
    
    // Extension properties for backward compatibility
    public Transform PetHandTransform => GetComponent<NetworkPetUI>()?.GetPetHandTransform();
    public Transform DeckTransform => GetComponent<NetworkPetUI>()?.GetDeckTransform();
    public Transform DiscardTransform => GetComponent<NetworkPetUI>()?.GetDiscardTransform();

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Use serialized fields as initial values
        MaxHealth.Value = _maxHealth;
        MaxEnergy.Value = _maxEnergy;
        CurrentHealth.Value = _currentHealth;
        CurrentEnergy.Value = _currentEnergy;
        
        // Override with GameManager values if available
        gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
        {
            MaxHealth.Value = gameManager.PetMaxHealth.Value;
            MaxEnergy.Value = gameManager.PetMaxEnergy.Value;
            CurrentHealth.Value = MaxHealth.Value;
            CurrentEnergy.Value = MaxEnergy.Value;
        }
        else Debug.LogWarning("GameManager not found in NetworkPet.OnStartServer. Using serialized field values.");
        
        // Set defaults if GameManager didn't override and serialized fields are zero
        if(MaxHealth.Value == 0) MaxHealth.Value = 50; 
        if(MaxEnergy.Value == 0) MaxEnergy.Value = 2;
        if(PetName.Value == null) PetName.Value = "DefaultPetName";
        
        // Synchronize serialized fields with SyncVars
        _maxHealth = MaxHealth.Value;
        _maxEnergy = MaxEnergy.Value;
        _currentHealth = CurrentHealth.Value;
        _currentEnergy = CurrentEnergy.Value;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Register with EntityVisibilityManager if available
        GamePhaseManager gamePhaseManager = GamePhaseManager.Instance;
        if (gamePhaseManager != null)
        {
            EntityVisibilityManager entityVisManager = gamePhaseManager.GetComponent<EntityVisibilityManager>();
            if (entityVisManager != null)
            {
                entityVisManager.RegisterPet(this);
            }
            else
            {
                Debug.LogWarning($"NetworkPet: EntityVisibilityManager not found on GamePhaseManager for {PetName.Value}");
            }
        }
        else
        {
            // Try to find EntityVisibilityManager directly as fallback
            EntityVisibilityManager entityVisManager = FindFirstObjectByType<EntityVisibilityManager>();
            if (entityVisManager != null)
            {
                entityVisManager.RegisterPet(this);
            }
            else
            {
                Debug.LogWarning($"NetworkPet: Neither GamePhaseManager nor EntityVisibilityManager found for {PetName.Value}");
            }
        }
        
        // Client-side initialization for pet visuals or UI linked to this pet
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
                entityVisManager.UnregisterPet(this);
            }
        }
        else
        {
            // Try to find EntityVisibilityManager directly as fallback
            EntityVisibilityManager entityVisManager = FindFirstObjectByType<EntityVisibilityManager>();
            if (entityVisManager != null)
            {
                entityVisManager.UnregisterPet(this);
            }
        }
    }

    [Server]
    public void SetOwnerPlayer(NetworkPlayer ownerPlayer)
    {
        if (ownerPlayer != null)
        {
            OwnerPlayerObjectId.Value = (uint) ownerPlayer.ObjectId;
        }
        else
        {
            OwnerPlayerObjectId.Value = 0U;
        }
    }

    public NetworkPlayer GetOwnerPlayer(NetworkManager networkManager = null)
    {
        if (OwnerPlayerObjectId.Value == 0U) return null;
        NetworkManager nm = networkManager ?? FishNet.InstanceFinder.NetworkManager;
        if (nm == null) return null;
        
        NetworkObject playerNob = null;
        // Try to get from ServerManager if server is running, otherwise from ClientManager
        if (nm.IsServerStarted && nm.ServerManager != null && nm.ServerManager.Objects != null)
        {
            nm.ServerManager.Objects.Spawned.TryGetValue((int)OwnerPlayerObjectId.Value, out playerNob);
        }
        else if (nm.IsClientStarted && nm.ClientManager != null && nm.ClientManager.Objects != null)
        {
            nm.ClientManager.Objects.Spawned.TryGetValue((int)OwnerPlayerObjectId.Value, out playerNob);
        }

        if (playerNob != null)
        {
            return playerNob.GetComponent<NetworkPlayer>();
        }
        return null;
    }

    [Server]
    public void TakeDamage(int amount)
    {
        CurrentHealth.Value -= amount;
        if (CurrentHealth.Value <= 0)
        {
            CurrentHealth.Value = 0;
            Debug.Log($"Pet {PetName.Value} has been defeated.");
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
    /// Notifies clients that a card has been played by this pet
    /// </summary>
    [ObserversRpc]
    public void NotifyCardPlayed(int cardId)
    {
        if (!IsClientInitialized) return;
        
        Debug.Log($"Pet NotifyCardPlayed received on client for card ID {cardId}, IsOwner: {IsOwner}, IsLocalPlayer: {IsClientInitialized && IsOwner}");
        
        // On client side, get the CardSpawner and tell it to remove the card
        CardSpawner cardSpawner = GetComponent<CardSpawner>();
        if (cardSpawner != null)
        {
            // Only remove cards from pets that belong to this client or in situations where this client needs to visualize a pet's move
            if (IsOwner || IsClientOnlyInitialized || FishNet.InstanceFinder.IsHostStarted)
            {
                // Use the new method for server-confirmed card removal
                cardSpawner.OnServerConfirmCardPlayed(cardId);
                Debug.Log($"Server confirmed card {cardId} played by pet - removing from display");
            }
            else
            {
                Debug.Log($"Ignoring pet card removal for non-owner client. Card: {cardId}, Pet: {PetName.Value}, ObjectId: {ObjectId}");
            }
        }
        else
        {
            Debug.LogError("NetworkPet.NotifyCardPlayed: CardSpawner component not found.");
        }
    }

    /// <summary>
    /// Notifies clients that a card has been played by this pet
    /// </summary>
    [ObserversRpc]
    public void NotifyCardPlayed(int cardId, string cardInstanceId)
    {
        if (!IsClientInitialized) return;
        
        Debug.Log($"Pet.NotifyCardPlayed received on client for card ID {cardId}, Instance ID {cardInstanceId}, IsOwner: {IsOwner}");
        
        // On client side, get the CardSpawner and tell it to remove the card
        CardSpawner cardSpawner = GetComponent<CardSpawner>();
        if (cardSpawner != null)
        {
            // Remove from the pet's hand
            cardSpawner.OnServerConfirmCardPlayed(cardId, cardInstanceId);
            Debug.Log($"Server confirmed pet card {cardId} (Instance: {cardInstanceId}) played - removing from display");
        }
        else
        {
            Debug.LogError("NetworkPet.NotifyCardPlayed: CardSpawner component not found.");
        }
    }
} 