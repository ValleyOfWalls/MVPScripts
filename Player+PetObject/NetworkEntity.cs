using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using Steamworks;

public enum EntityType
{
    Player,
    Pet
}

/// <summary>
/// Represents a networked entity (player or pet) with health, energy, cards, and other game-related stats.
/// Attach to: Both Player and Pet prefabs that are instantiated for connected clients.
/// </summary>
public class NetworkEntity : NetworkBehaviour
{
    [Header("Entity Type")]
    [SerializeField] private EntityType entityType;
    public EntityType EntityType => entityType;

    // Basic entity info
    public readonly SyncVar<string> EntityName = new SyncVar<string>();
    public readonly SyncVar<uint> OwnerEntityId = new SyncVar<uint>(); // For pets, this is their owner's ObjectId

    // Stats
    [Header("Health & Energy")]
    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private int _maxEnergy = 3;
    [SerializeField] private int _currentHealth = 100;
    [SerializeField] private int _currentEnergy = 3;

    public readonly SyncVar<int> MaxHealth = new SyncVar<int>();
    public readonly SyncVar<int> MaxEnergy = new SyncVar<int>();
    public readonly SyncVar<int> CurrentHealth = new SyncVar<int>();
    public readonly SyncVar<int> CurrentEnergy = new SyncVar<int>();
    public readonly SyncVar<string> CurrentStatuses = new SyncVar<string>();

    // Currency (only for players)
    public readonly SyncVar<int> Currency = new SyncVar<int>();
    public event System.Action<int> OnCurrencyChanged;

    private GameManager gameManager;
    private RelationshipManager relationshipManager;

    private void Awake()
    {
        relationshipManager = GetComponent<RelationshipManager>();
        if (relationshipManager == null)
        {
            relationshipManager = gameObject.AddComponent<RelationshipManager>();
            Debug.Log($"Added RelationshipManager to {entityType} {gameObject.name}");
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        gameManager = FindFirstObjectByType<GameManager>();

        // Use serialized fields as initial values
        MaxHealth.Value = _maxHealth;
        MaxEnergy.Value = _maxEnergy;
        CurrentHealth.Value = _currentHealth;
        CurrentEnergy.Value = _currentEnergy;

        // Override with GameManager values if available
        if (gameManager != null)
        {
            if (entityType == EntityType.Player)
            {
                MaxHealth.Value = gameManager.PlayerMaxHealth.Value;
                MaxEnergy.Value = gameManager.PlayerMaxEnergy.Value;
            }
            else
            {
                MaxHealth.Value = gameManager.PetMaxHealth.Value;
                MaxEnergy.Value = gameManager.PetMaxEnergy.Value;
            }
            CurrentHealth.Value = MaxHealth.Value;
            CurrentEnergy.Value = MaxEnergy.Value;
        }

        // Set defaults if needed
        if (MaxHealth.Value == 0) MaxHealth.Value = entityType == EntityType.Player ? 100 : 50;
        if (MaxEnergy.Value == 0) MaxEnergy.Value = entityType == EntityType.Player ? 3 : 2;

        // Set name based on type and owner
        SetEntityName();

        // Sync serialized fields
        _maxHealth = MaxHealth.Value;
        _maxEnergy = MaxEnergy.Value;
        _currentHealth = CurrentHealth.Value;
        _currentEnergy = CurrentEnergy.Value;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Add more detailed ownership debugging
        bool isLocalConnection = Owner == FishNet.InstanceFinder.ClientManager.Connection;
        bool isHostConnection = FishNet.InstanceFinder.IsHostStarted && Owner != null && Owner.ClientId == 0;

        Debug.Log($"NetworkEntity OnStartClient - Type: {entityType}, Name: {EntityName.Value}, " +
                  $"ID: {ObjectId}, IsOwner: {IsOwner}, HasOwner: {Owner != null}, " +
                  $"OwnerId: {(Owner != null ? Owner.ClientId : -1)}, " +
                  $"IsLocalConnection: {isLocalConnection}, IsHostConnection: {isHostConnection}");

        // Register with EntityVisibilityManager
        RegisterWithVisibilityManager();
    }

    private void RegisterWithVisibilityManager()
    {
        GamePhaseManager gamePhaseManager = GamePhaseManager.Instance;
        EntityVisibilityManager entityVisManager = null;

        if (gamePhaseManager != null)
        {
            entityVisManager = gamePhaseManager.GetComponent<EntityVisibilityManager>();
        }
        else
        {
            entityVisManager = FindFirstObjectByType<EntityVisibilityManager>();
        }

        if (entityVisManager != null)
        {
            entityVisManager.RegisterEntity(this);
        }
        else
        {
            Debug.LogWarning($"NetworkEntity: Could not find EntityVisibilityManager for {EntityName.Value}");
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        UnregisterFromVisibilityManager();
    }

    private void UnregisterFromVisibilityManager()
    {
        EntityVisibilityManager entityVisManager = FindFirstObjectByType<EntityVisibilityManager>();
        if (entityVisManager != null)
        {
            entityVisManager.UnregisterEntity(this);
        }
    }

    [Server]
    private void SetEntityName()
    {
        if (entityType == EntityType.Player)
        {
            if (Owner != null)
                EntityName.Value = $"Player ({Owner.ClientId})";
            else
                EntityName.Value = "Player (No Owner)";
        }
        else // Pet
        {
            int clientId = relationshipManager?.OwnerClientId ?? -1;
            EntityName.Value = $"Pet ({clientId})";
        }
    }

    [Server]
    public void SetOwnerEntity(NetworkEntity ownerEntity)
    {
        if (entityType != EntityType.Pet)
        {
            Debug.LogError("Cannot set owner entity on a non-pet entity");
            return;
        }

        OwnerEntityId.Value = (uint)ownerEntity.ObjectId;
        // Update the name to reflect ownership
        EntityName.Value = $"Pet ({ownerEntity.Owner?.ClientId ?? -1})";
    }

    public NetworkEntity GetOwnerEntity()
    {
        if (entityType != EntityType.Pet || OwnerEntityId.Value == 0)
            return null;

        NetworkObject ownerObj = null;
        var nm = FishNet.InstanceFinder.NetworkManager;

        if (nm.IsServerStarted)
            nm.ServerManager.Objects.Spawned.TryGetValue((int)OwnerEntityId.Value, out ownerObj);
        else if (nm.IsClientStarted)
            nm.ClientManager.Objects.Spawned.TryGetValue((int)OwnerEntityId.Value, out ownerObj);

        return ownerObj?.GetComponent<NetworkEntity>();
    }

    #region State Changes

    [Server]
    public void TakeDamage(int amount)
    {
        CurrentHealth.Value -= amount;
        if (CurrentHealth.Value <= 0)
        {
            CurrentHealth.Value = 0;
            Debug.Log($"{entityType} {EntityName.Value} has been defeated.");
        }
        _currentHealth = CurrentHealth.Value;
    }

    [Server]
    public void Heal(int amount)
    {
        CurrentHealth.Value += amount;
        if (CurrentHealth.Value > MaxHealth.Value) CurrentHealth.Value = MaxHealth.Value;
        _currentHealth = CurrentHealth.Value;
    }

    [Server]
    public void ChangeEnergy(int amount)
    {
        CurrentEnergy.Value += amount;
        if (CurrentEnergy.Value < 0) CurrentEnergy.Value = 0;
        if (CurrentEnergy.Value > MaxEnergy.Value) CurrentEnergy.Value = MaxEnergy.Value;
        _currentEnergy = CurrentEnergy.Value;
    }

    [Server]
    public void ReplenishEnergy()
    {
        CurrentEnergy.Value = MaxEnergy.Value;
        _currentEnergy = CurrentEnergy.Value;
    }

    [Server]
    public void IncreaseMaxHealth(int amount)
    {
        MaxHealth.Value += amount;
        CurrentHealth.Value += amount;
        _maxHealth = MaxHealth.Value;
        _currentHealth = CurrentHealth.Value;
    }

    [Server]
    public void IncreaseMaxEnergy(int amount)
    {
        MaxEnergy.Value += amount;
        CurrentEnergy.Value += amount;
        _maxEnergy = MaxEnergy.Value;
        _currentEnergy = CurrentEnergy.Value;
    }

    #endregion

    #region Currency (Player Only)

    [Server]
    public void AddCurrency(int amount)
    {
        if (entityType != EntityType.Player) return;
        Currency.Value += amount;
        OnCurrencyChanged?.Invoke(Currency.Value);
    }

    [Server]
    public void DeductCurrency(int amount)
    {
        if (entityType != EntityType.Player) return;
        Currency.Value -= amount;
        OnCurrencyChanged?.Invoke(Currency.Value);
    }

    [Server]
    public void SetCurrency(int amount)
    {
        if (entityType != EntityType.Player) return;
        Currency.Value = amount;
        OnCurrencyChanged?.Invoke(Currency.Value);
    }

    #endregion

    #region Card Notifications

    [ObserversRpc]
    public void NotifyCardPlayed(int cardId, string cardInstanceId)
    {
        if (!IsSpawned) return;
        Debug.Log($"{entityType} NotifyCardPlayed received on client for card ID {cardId}, Instance ID {cardInstanceId}, IsOwner: {IsOwner}");
    }

    #endregion

    #region Name Management

    [ServerRpc(RequireOwnership = false)]
    public void CmdSetEntityName(string name, NetworkConnection sender = null)
    {
        if (entityType != EntityType.Player) return;
        EntityName.Value = name;
    }

    #endregion
} 