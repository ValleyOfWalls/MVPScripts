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
    
    // Events
    public event Action<int, NetworkEntity> OnDamageTaken;
    public event Action<int, NetworkEntity> OnHealingReceived;
    public event Action<NetworkEntity> OnDeath;
    
    // SFX References
    [Header("Audio")]
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private AudioClip healingSound;
    [SerializeField] private AudioClip deathSound;
    
    // VFX References
    [Header("Visual Effects")]
    [SerializeField] private GameObject damageEffectPrefab;
    [SerializeField] private GameObject healEffectPrefab;
    [SerializeField] private GameObject deathEffectPrefab;
    
    private AudioSource audioSource;
    
    private void Awake()
    {
        // Get required references
        if (entity == null) entity = GetComponent<NetworkEntity>();
        
        // Set up audio source if needed
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && (damageSound != null || healingSound != null || deathSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
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
        
        Debug.Log($"LifeHandler: {entity.EntityName.Value} taking {amount} damage from {(source != null ? source.EntityName.Value : "unknown")}");
        
        // Cap damage to prevent negative health
        int cappedDamage = Mathf.Min(amount, entity.CurrentHealth.Value);
        
        // Apply damage to the entity
        entity.CurrentHealth.Value -= cappedDamage;
        
        // Trigger damage VFX and SFX
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
        
        Debug.Log($"LifeHandler: {entity.EntityName.Value} receiving {amount} healing from {(source != null ? source.EntityName.Value : "unknown")}");
        
        // Cap healing to prevent exceeding max health
        int cappedHealing = Mathf.Min(amount, entity.MaxHealth.Value - entity.CurrentHealth.Value);
        
        if (cappedHealing <= 0) return; // Already at max health
        
        // Apply healing to the entity
        entity.CurrentHealth.Value += cappedHealing;
        
        // Trigger healing VFX and SFX
        RpcOnHealingReceived(cappedHealing, source != null ? source.ObjectId : 0);
    }
    
    /// <summary>
    /// Handles death logic when health reaches 0
    /// </summary>
    [Server]
    private void HandleDeath(NetworkEntity killer)
    {
        Debug.Log($"LifeHandler: {entity.EntityName.Value} has died");
        
        // Ensure health is exactly 0
        entity.CurrentHealth.Value = 0;
        
        // Trigger death VFX and SFX
        RpcOnDeath(killer != null ? killer.ObjectId : 0);
        
        // TODO: Implement additional death logic (e.g., removing from combat, awarding XP, etc.)
        // For now, just notify the combat manager if it exists
        CombatManager combatManager = FindFirstObjectByType<CombatManager>();
        if (combatManager != null)
        {
            // Call appropriate method on CombatManager to handle entity death
            // combatManager.HandleEntityDeath(entity, killer);
        }
    }
    
    [ObserversRpc]
    private void RpcOnDamageTaken(int amount, int sourceId)
    {
        // Find source entity on the client
        NetworkEntity sourceEntity = FindEntityById(sourceId);
        
        // Invoke the event
        OnDamageTaken?.Invoke(amount, sourceEntity);
        
        // Play damage sound
        if (audioSource != null && damageSound != null)
        {
            audioSource.clip = damageSound;
            audioSource.Play();
        }
        
        // Spawn damage effect
        if (damageEffectPrefab != null)
        {
            GameObject effect = Instantiate(damageEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 2f); // Clean up after 2 seconds
        }
        
        // Display floating damage number or other UI feedback
        // This would typically be handled by a UI manager
        Debug.Log($"Client: {entity.EntityName.Value} took {amount} damage");
    }
    
    [ObserversRpc]
    private void RpcOnHealingReceived(int amount, int sourceId)
    {
        // Find source entity on the client
        NetworkEntity sourceEntity = FindEntityById(sourceId);
        
        // Invoke the event
        OnHealingReceived?.Invoke(amount, sourceEntity);
        
        // Play healing sound
        if (audioSource != null && healingSound != null)
        {
            audioSource.clip = healingSound;
            audioSource.Play();
        }
        
        // Spawn healing effect
        if (healEffectPrefab != null)
        {
            GameObject effect = Instantiate(healEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 2f); // Clean up after 2 seconds
        }
        
        // Display floating healing number or other UI feedback
        Debug.Log($"Client: {entity.EntityName.Value} healed for {amount}");
    }
    
    [ObserversRpc]
    private void RpcOnDeath(int killerId)
    {
        // Find killer entity on the client
        NetworkEntity killerEntity = FindEntityById(killerId);
        
        // Invoke the event
        OnDeath?.Invoke(killerEntity);
        
        // Play death sound
        if (audioSource != null && deathSound != null)
        {
            audioSource.clip = deathSound;
            audioSource.Play();
        }
        
        // Spawn death effect
        if (deathEffectPrefab != null)
        {
            GameObject effect = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 3f); // Clean up after 3 seconds
        }
        
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