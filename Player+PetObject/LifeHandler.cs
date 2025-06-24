using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;

/// <summary>
/// Handles life/health changes for entities including damage, healing, and death.
/// Attach to: Entity GameObjects along with NetworkEntity.
/// </summary>
public class LifeHandler : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkEntity entity;
    [SerializeField] private NetworkEntityUI entityUI;
    
    // Events
    public event Action<int, NetworkEntity> OnDamageTaken;
    public event Action<int, NetworkEntity> OnHealingReceived;
    public event Action<NetworkEntity> OnDeath;
    
    private void Awake()
    {
        // Get required references
        if (entity == null) entity = GetComponent<NetworkEntity>();
        if (entityUI == null) entityUI = GetComponent<NetworkEntityUI>();
    }
    
    /// <summary>
    /// Apply damage to this entity
    /// </summary>
    /// <param name="amount">Amount of damage to apply</param>
    /// <param name="source">Entity that caused the damage (can be null)</param>
    [Server]
    public void TakeDamage(int amount, NetworkEntity source)
    {
        if (!IsServerInitialized)
        {
            Debug.LogError($"LifeHandler on {gameObject.name}: TakeDamage called outside of server context");
            return;
        }
        
        if (amount <= 0) return; // Ignore non-positive damage
        
        /* Debug.Log($"LifeHandler: {entity.EntityName.Value} taking {amount} damage from {(source != null ? source.EntityName.Value : "unknown")}"); */
        
        // Process shield absorption first
        EffectHandler effectHandler = GetComponent<EffectHandler>();
        int finalDamage = amount;
        if (effectHandler != null)
        {
            finalDamage = effectHandler.ProcessShieldAbsorption(amount);
        }
        
        // Cap damage to prevent negative health
        int cappedDamage = Mathf.Min(finalDamage, entity.CurrentHealth.Value);
        
        // Apply damage to the entity
        entity.CurrentHealth.Value -= cappedDamage;
        
        // Process thorns reflection if damage was actually taken
        if (cappedDamage > 0 && source != null && effectHandler != null)
        {
            effectHandler.ProcessThornsReflection(source, cappedDamage);
        }
        
        // Notify clients
        RpcOnDamageTaken(cappedDamage, source != null ? source.ObjectId : 0);
        
        // Check for death
        if (entity.CurrentHealth.Value <= 0)
        {
            HandleDeath(source);
        }
    }
    
    /// <summary>
    /// Apply healing to this entity
    /// </summary>
    /// <param name="amount">Amount of healing to apply</param>
    /// <param name="source">Entity that caused the healing (can be null)</param>
    [Server]
    public void Heal(int amount, NetworkEntity source)
    {
        if (!IsServerInitialized)
        {
            Debug.LogError($"LifeHandler on {gameObject.name}: Heal called outside of server context");
            return;
        }
        
        if (amount <= 0) return; // Ignore non-positive healing
        
        /* Debug.Log($"LifeHandler: {entity.EntityName.Value} receiving {amount} healing from {(source != null ? source.EntityName.Value : "unknown")}"); */
        
        // Cap healing to prevent exceeding max health
        int cappedHealing = Mathf.Min(amount, entity.MaxHealth.Value - entity.CurrentHealth.Value);
        
        if (cappedHealing <= 0) return; // Already at max health
        
        // Apply healing to the entity
        entity.CurrentHealth.Value += cappedHealing;
        
        // Notify clients
        RpcOnHealingReceived(cappedHealing, source != null ? source.ObjectId : 0);
    }
    
    /// <summary>
    /// Handles death logic when health reaches 0
    /// </summary>
    [Server]
    private void HandleDeath(NetworkEntity killer)
    {
        /* Debug.Log($"LifeHandler: {entity.EntityName.Value} has died"); */
        
        // Ensure health is exactly 0
        entity.CurrentHealth.Value = 0;
        
        // Notify clients
        RpcOnDeath(killer != null ? killer.ObjectId : 0);
        
        // Notify the combat manager about the entity death
        CombatManager combatManager = FindFirstObjectByType<CombatManager>();
        if (combatManager != null)
        {
            combatManager.HandleEntityDeath(entity, killer);
        }
        else
        {
            Debug.LogWarning($"LifeHandler: CombatManager not found, cannot notify about death of {entity.EntityName.Value}");
        }
    }
    
    [ObserversRpc]
    private void RpcOnDamageTaken(int amount, int sourceId)
    {
        // Find source entity on the client
        NetworkEntity sourceEntity = FindEntityById(sourceId);
        
        // Invoke the event
        OnDamageTaken?.Invoke(amount, sourceEntity);
        
        // UI updates are now handled automatically by EntityStatsUIController
        // through events and SyncVar changes - no manual UI update needed
        
        Debug.Log($"Client: {entity.EntityName.Value} took {amount} damage");
    }
    
    [ObserversRpc]
    private void RpcOnHealingReceived(int amount, int sourceId)
    {
        // Find source entity on the client
        NetworkEntity sourceEntity = FindEntityById(sourceId);
        
        // Invoke the event
        OnHealingReceived?.Invoke(amount, sourceEntity);
        
        // UI updates are now handled automatically by EntityStatsUIController
        // through events and SyncVar changes - no manual UI update needed
        
        Debug.Log($"Client: {entity.EntityName.Value} healed for {amount}");
    }
    
    [ObserversRpc]
    private void RpcOnDeath(int killerId)
    {
        // Find killer entity on the client
        NetworkEntity killerEntity = FindEntityById(killerId);
        
        // Invoke the event
        OnDeath?.Invoke(killerEntity);
        
        // UI updates are now handled automatically by EntityStatsUIController
        // through events and SyncVar changes - no manual UI update needed
        
        Debug.Log($"Client: {entity.EntityName.Value} has died");
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