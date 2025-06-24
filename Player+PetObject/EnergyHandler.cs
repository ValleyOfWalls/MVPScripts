using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;

/// <summary>
/// Handles energy changes for entities including consumption, regeneration, and max energy modifications.
/// Attach to: Entity GameObjects along with NetworkEntity.
/// </summary>
public class EnergyHandler : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkEntity entity;
    [SerializeField] private NetworkEntityUI entityUI;
    
    // Events
    public event Action<int, NetworkEntity> OnEnergySpent;
    public event Action<int, NetworkEntity> OnEnergyGained;
    public event Action<int> OnMaxEnergyChanged;
    
    private void Awake()
    {
        // Get required references
        if (entity == null) entity = GetComponent<NetworkEntity>();
        if (entityUI == null) entityUI = GetComponent<NetworkEntityUI>();
    }
    
    /// <summary>
    /// Spend energy for an action (like playing a card)
    /// </summary>
    /// <param name="amount">Amount of energy to spend</param>
    /// <param name="source">Entity that caused the energy spend (can be null)</param>
    [Server]
    public void SpendEnergy(int amount, NetworkEntity source)
    {
        if (!IsServerInitialized)
        {
            Debug.LogError($"EnergyHandler on {gameObject.name}: SpendEnergy called outside of server context");
            return;
        }
        
        if (amount <= 0) return; // Ignore non-positive amounts
        
        /* Debug.Log($"EnergyHandler: {entity.EntityName.Value} spending {amount} energy"); */
        
        // Cap energy spend to prevent negative energy
        int cappedAmount = Mathf.Min(amount, entity.CurrentEnergy.Value);
        
        // Apply energy change to the entity
        entity.CurrentEnergy.Value -= cappedAmount;
        
        // Notify clients
        RpcOnEnergySpent(cappedAmount, source != null ? source.ObjectId : 0);
    }
    
    /// <summary>
    /// Add energy to the entity
    /// </summary>
    /// <param name="amount">Amount of energy to add</param>
    /// <param name="source">Entity that caused the energy gain (can be null)</param>
    [Server]
    public void AddEnergy(int amount, NetworkEntity source)
    {
        if (!IsServerInitialized)
        {
            Debug.LogError($"EnergyHandler on {gameObject.name}: AddEnergy called outside of server context");
            return;
        }
        
        if (amount <= 0) return; // Ignore non-positive amounts
        
        /* Debug.Log($"EnergyHandler: {entity.EntityName.Value} gaining {amount} energy"); */
        
        // Cap energy gain to prevent exceeding max energy
        int cappedAmount = Mathf.Min(amount, entity.MaxEnergy.Value - entity.CurrentEnergy.Value);
        
        if (cappedAmount <= 0) return; // Already at max energy
        
        // Apply energy gain to the entity
        entity.CurrentEnergy.Value += cappedAmount;
        
        // Notify clients
        RpcOnEnergyGained(cappedAmount, source != null ? source.ObjectId : 0);
    }
    
    /// <summary>
    /// Set energy to maximum (usually at start of turn)
    /// </summary>
    [Server]
    public void ReplenishEnergy()
    {
        if (!IsServerInitialized) return;
        
        int amountToAdd = entity.MaxEnergy.Value - entity.CurrentEnergy.Value;
        
        if (amountToAdd <= 0) return; // Already at max energy
        
        /* Debug.Log($"EnergyHandler: {entity.EntityName.Value} replenishing to max energy {entity.MaxEnergy.Value}"); */
        
        // Set energy to max
        entity.CurrentEnergy.Value = entity.MaxEnergy.Value;
        
        // Notify clients
        RpcOnEnergyGained(amountToAdd, 0);
    }
    
    /// <summary>
    /// Increase the maximum energy
    /// </summary>
    /// <param name="amount">Amount to increase max energy by</param>
    [Server]
    public void IncreaseMaxEnergy(int amount)
    {
        if (!IsServerInitialized || amount <= 0) return;
        
        /* Debug.Log($"EnergyHandler: {entity.EntityName.Value} increasing max energy by {amount}"); */
        
        // Increase max energy
        entity.MaxEnergy.Value += amount;
        
        // Also increase current energy by the same amount
        entity.CurrentEnergy.Value += amount;
        
        // Notify clients
        RpcOnMaxEnergyChanged(entity.MaxEnergy.Value);
    }
    
    /// <summary>
    /// Decrease the maximum energy
    /// </summary>
    /// <param name="amount">Amount to decrease max energy by</param>
    [Server]
    public void DecreaseMaxEnergy(int amount)
    {
        if (!IsServerInitialized || amount <= 0) return;
        
        /* Debug.Log($"EnergyHandler: {entity.EntityName.Value} decreasing max energy by {amount}"); */
        
        // Decrease max energy, but ensure it doesn't go below 0
        entity.MaxEnergy.Value = Mathf.Max(0, entity.MaxEnergy.Value - amount);
        
        // Cap current energy if it now exceeds max
        if (entity.CurrentEnergy.Value > entity.MaxEnergy.Value)
        {
            entity.CurrentEnergy.Value = entity.MaxEnergy.Value;
        }
        
        // Notify clients
        RpcOnMaxEnergyChanged(entity.MaxEnergy.Value);
    }
    
    [ObserversRpc]
    private void RpcOnEnergySpent(int amount, int sourceId)
    {
        // Find source entity on the client
        NetworkEntity sourceEntity = FindEntityById(sourceId);
        
        // Invoke the event
        OnEnergySpent?.Invoke(amount, sourceEntity);
        
        // UI updates are now handled automatically by EntityStatsUIController
        // through events and SyncVar changes - no manual UI update needed
        
        Debug.Log($"Client: {entity.EntityName.Value} spent {amount} energy");
    }
    
    [ObserversRpc]
    private void RpcOnEnergyGained(int amount, int sourceId)
    {
        // Find source entity on the client
        NetworkEntity sourceEntity = FindEntityById(sourceId);
        
        // Invoke the event
        OnEnergyGained?.Invoke(amount, sourceEntity);
        
        // UI updates are now handled automatically by EntityStatsUIController
        // through events and SyncVar changes - no manual UI update needed
        
        Debug.Log($"Client: {entity.EntityName.Value} gained {amount} energy");
    }
    
    [ObserversRpc]
    private void RpcOnMaxEnergyChanged(int newMaxEnergy)
    {
        // Invoke the event
        OnMaxEnergyChanged?.Invoke(newMaxEnergy);
        
        // UI updates are now handled automatically by EntityStatsUIController
        // through events and SyncVar changes - no manual UI update needed
        
        Debug.Log($"Client: {entity.EntityName.Value} max energy changed to {newMaxEnergy}");
    }
    
    /// <summary>
    /// Finds a NetworkEntity by its object ID
    /// </summary>
    private NetworkEntity FindEntityById(int entityId)
    {
        if (entityId == 0) return null;
        
        NetworkObject netObj = null;
        
        if (IsServerInitialized)
        {
            FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(entityId, out netObj);
        }
        else if (IsClientInitialized)
        {
            FishNet.InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(entityId, out netObj);
        }
        
        return netObj?.GetComponent<NetworkEntity>();
    }
} 