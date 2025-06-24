using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections;
using System.Collections.Generic;
using Steamworks;

public enum EntityType
{
    Player,
    Pet,
    PlayerHand,
    PetHand,
    PlayerStatsUI,
    PetStatsUI
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

    [Header("Ownership Info (Inspector Display)")]
    [SerializeField] private int inspectorOwnerClientId = -1;

    // Basic entity info
    public readonly SyncVar<string> EntityName = new SyncVar<string>();
    public readonly SyncVar<uint> OwnerEntityId = new SyncVar<uint>(); // For pets, this is their owner's ObjectId
    
    // Character Selection Data (synced from character selection phase)
    public readonly SyncVar<int> SelectedCharacterIndex = new SyncVar<int>(-1);
    public readonly SyncVar<int> SelectedPetIndex = new SyncVar<int>(-1);
    public readonly SyncVar<string> CharacterPrefabPath = new SyncVar<string>("");
    public readonly SyncVar<string> PetPrefabPath = new SyncVar<string>("");

    // Stats
    [Header("Health & Energy")]
    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private int _maxEnergy = 100; // Fixed: was 3 in prefab, should be 100
    [SerializeField] private int _currentHealth = 100;
    [SerializeField] private int _currentEnergy = 100;

    public readonly SyncVar<int> MaxHealth = new SyncVar<int>();
    public readonly SyncVar<int> MaxEnergy = new SyncVar<int>();
    public readonly SyncVar<int> CurrentHealth = new SyncVar<int>();
    public readonly SyncVar<int> CurrentEnergy = new SyncVar<int>();
    public readonly SyncVar<string> CurrentStatuses = new SyncVar<string>();

    // Currency (only for players)
    [Header("Currency")]
    [SerializeField] private int _currency = 20;
    public readonly SyncVar<int> Currency = new SyncVar<int>();
    public event System.Action<int> OnCurrencyChanged;

    private GameManager gameManager;
    private RelationshipManager relationshipManager;
    private NetworkEntityAnimator entityAnimator;

    private void Awake()
    {
        relationshipManager = GetComponent<RelationshipManager>();
        if (relationshipManager == null)
        {
            relationshipManager = gameObject.AddComponent<RelationshipManager>();
            Debug.Log($"Added RelationshipManager to {entityType} {gameObject.name}");
        }
        
        entityAnimator = GetComponent<NetworkEntityAnimator>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        gameManager = FindFirstObjectByType<GameManager>();

        // Update inspector owner ID
        inspectorOwnerClientId = Owner?.ClientId ?? -1;

        // Log the serialized field values BEFORE using them
        Debug.Log($"SYNC_DEBUG: OnStartServer - {entityType} BEFORE SyncVar assignment - _maxHealth: {_maxHealth}, _maxEnergy: {_maxEnergy}, _currentHealth: {_currentHealth}, _currentEnergy: {_currentEnergy}");
        
        // DON'T initialize SyncVars here for Player/Pet entities - let PlayerSpawner handle it
        // This prevents premature sync of default prefab values before character/pet data is applied
        if (entityType != EntityType.Player && entityType != EntityType.Pet)
        {
            // Only initialize for UI entities and hands
            MaxHealth.Value = _maxHealth;
            MaxEnergy.Value = _maxEnergy;
            CurrentHealth.Value = _currentHealth;
            CurrentEnergy.Value = _currentEnergy;
            
            Debug.Log($"SYNC_DEBUG: OnStartServer - {entityType} initialized with default values");
        }
        else
        {
            Debug.Log($"SYNC_DEBUG: OnStartServer - {entityType} waiting for PlayerSpawner to apply character/pet data before sync");
        }

        // Set name based on type and owner (but don't override if already set)
        SetEntityName();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Update inspector owner ID
        inspectorOwnerClientId = Owner?.ClientId ?? -1;

        // Add more detailed ownership debugging
        bool isLocalConnection = Owner == FishNet.InstanceFinder.ClientManager.Connection;
        bool isHostConnection = FishNet.InstanceFinder.IsHostStarted && Owner != null && Owner.ClientId == 0;

        Debug.Log($"SYNC_DEBUG: OnStartClient - Type: {entityType}, Name: '{EntityName.Value}', " +
                  $"ID: {ObjectId}, IsOwner: {IsOwner}, HasOwner: {Owner != null}, " +
                  $"OwnerId: {(Owner != null ? Owner.ClientId : -1)}, " +
                  $"IsLocalConnection: {isLocalConnection}, IsHostConnection: {isHostConnection}");

        Debug.Log($"SYNC_DEBUG: OnStartClient Stats - MaxHealth={MaxHealth.Value}, MaxEnergy={MaxEnergy.Value}, " +
                  $"CurrentHealth={CurrentHealth.Value}, CurrentEnergy={CurrentEnergy.Value}, Currency={Currency.Value}");

        // Subscribe to SyncVar changes for debugging
        EntityName.OnChange += OnEntityNameChanged;
        MaxHealth.OnChange += OnMaxHealthChanged;
        MaxEnergy.OnChange += OnMaxEnergyChanged;
        CurrentEnergy.OnChange += OnCurrentEnergyChanged;

        // Register with EntityVisibilityManager
        RegisterWithVisibilityManager();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Unsubscribe from events
        EntityName.OnChange -= OnEntityNameChanged;
        MaxHealth.OnChange -= OnMaxHealthChanged;
        MaxEnergy.OnChange -= OnMaxEnergyChanged;
        CurrentEnergy.OnChange -= OnCurrentEnergyChanged;
        
        UnregisterFromVisibilityManager();
    }

    private void OnEntityNameChanged(string prev, string next, bool asServer)
    {
        Debug.Log($"SYNC_DEBUG: Name changed for {entityType} (ID: {ObjectId}) from '{prev}' to '{next}' (asServer: {asServer})");
    }

    private void OnMaxHealthChanged(int prev, int next, bool asServer)
    {
        Debug.Log($"SYNC_DEBUG: MaxHealth changed for {EntityName.Value} from {prev} to {next} (asServer: {asServer})");
    }

    private void OnMaxEnergyChanged(int prev, int next, bool asServer)
    {
        Debug.Log($"SYNC_DEBUG: MaxEnergy changed for {EntityName.Value} from {prev} to {next} (asServer: {asServer})");
        
        // Check if any UI controllers are listening to this entity's changes
        if (prev != next && !asServer)
        {
            Debug.Log($"SYNC_DEBUG: UI_CHECK - MaxEnergy changed for {EntityName.Value}, checking for UI controllers...");
            CheckUIControllerLinks();
        }
    }

    private void OnCurrentEnergyChanged(int prev, int next, bool asServer)
    {
        Debug.Log($"SYNC_DEBUG: CurrentEnergy changed for {EntityName.Value} from {prev} to {next} (asServer: {asServer})");
        
        // If this is a significant change (not just 0->0 or 3->3), log it as important
        if (prev != next && (prev == 0 || next == 100 || prev == 3))
        {
            Debug.Log($"SYNC_DEBUG: IMPORTANT - CurrentEnergy sync for {EntityName.Value}: {prev} -> {next}");
            
            // Log the actual SyncVar values after the change
            Debug.Log($"SYNC_DEBUG: VERIFY - {EntityName.Value} SyncVar values after change: MaxEnergy={MaxEnergy.Value}, CurrentEnergy={CurrentEnergy.Value}, MaxHealth={MaxHealth.Value}");
        }
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
            // Only set generic name if no name has been set yet
            // ApplyCharacterDataToEntity will set the proper player name later
            if (string.IsNullOrEmpty(EntityName.Value))
            {
                if (Owner != null)
                    EntityName.Value = $"Player ({Owner.ClientId})";
                else
                    EntityName.Value = "Player (No Owner)";
                Debug.Log($"SYNC_DEBUG: Set temporary player name to '{EntityName.Value}'");
            }
            else
            {
                Debug.Log($"SYNC_DEBUG: Player name already set to '{EntityName.Value}', not overriding");
            }
        }
        else // Pet
        {
            // Only set generic name if no name has been set yet
            // ApplyPetDataToEntity will set the proper pet name later
            if (string.IsNullOrEmpty(EntityName.Value))
            {
                int clientId = relationshipManager?.OwnerClientId ?? -1;
                EntityName.Value = $"Pet ({clientId})";
                Debug.Log($"SYNC_DEBUG: Set temporary pet name to '{EntityName.Value}'");
            }
            else
            {
                Debug.Log($"SYNC_DEBUG: Pet name already set to '{EntityName.Value}', not overriding");
            }
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
        // Don't override the pet's actual name - it should have been set properly during ApplyPetDataToEntity
        Debug.Log($"NetworkEntity: Set owner entity for pet '{EntityName.Value}' to player (Client ID: {ownerEntity.Owner?.ClientId ?? -1})");
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
        _currency = Currency.Value;
        OnCurrencyChanged?.Invoke(Currency.Value);
    }

    [Server]
    public void DeductCurrency(int amount)
    {
        if (entityType != EntityType.Player) return;
        Currency.Value -= amount;
        _currency = Currency.Value;
        OnCurrencyChanged?.Invoke(Currency.Value);
    }

    [Server]
    public void SetCurrency(int amount)
    {
        if (entityType != EntityType.Player) return;
        Currency.Value = amount;
        _currency = Currency.Value;
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

    #region Character Selection Data

    /// <summary>
    /// Sets the selected character data for this entity (Server only)
    /// </summary>
    [Server]
    public void SetCharacterSelection(int characterIndex, string characterPrefabPath)
    {
        if (entityType != EntityType.Player)
        {
            Debug.LogWarning($"SetCharacterSelection called on {entityType} entity - should only be called on Player entities");
            return;
        }
        
        SelectedCharacterIndex.Value = characterIndex;
        CharacterPrefabPath.Value = characterPrefabPath;
        
        Debug.Log($"NetworkEntity: Set character selection for {EntityName.Value} - Index: {characterIndex}, Path: {characterPrefabPath}");
    }

    /// <summary>
    /// Sets the selected pet data for this entity (Server only)
    /// </summary>
    [Server]
    public void SetPetSelection(int petIndex, string petPrefabPath)
    {
        if (entityType != EntityType.Pet)
        {
            Debug.LogWarning($"SetPetSelection called on {entityType} entity - should only be called on Pet entities");
            return;
        }
        
        SelectedPetIndex.Value = petIndex;
        PetPrefabPath.Value = petPrefabPath;
        
        Debug.Log($"NetworkEntity: Set pet selection for {EntityName.Value} - Index: {petIndex}, Path: {petPrefabPath}");
    }

    /// <summary>
    /// Gets the prefab path for this entity's selection
    /// </summary>
    public string GetSelectedPrefabPath()
    {
        return entityType == EntityType.Player ? CharacterPrefabPath.Value : PetPrefabPath.Value;
    }

    /// <summary>
    /// Gets the selection index for this entity
    /// </summary>
    public int GetSelectedIndex()
    {
        return entityType == EntityType.Player ? SelectedCharacterIndex.Value : SelectedPetIndex.Value;
    }

    /// <summary>
    /// Returns true if this entity has valid selection data
    /// </summary>
    public bool HasValidSelection()
    {
        if (entityType == EntityType.Player)
        {
            return SelectedCharacterIndex.Value >= 0 && !string.IsNullOrEmpty(CharacterPrefabPath.Value);
        }
        else if (entityType == EntityType.Pet)
        {
            return SelectedPetIndex.Value >= 0 && !string.IsNullOrEmpty(PetPrefabPath.Value);
        }
        return false;
    }

    #endregion

    #region Manual Stats Update

    /// <summary>
    /// Manually updates the SyncVars from the current serialized field values
    /// Called by PlayerSpawner after applying character/pet data via reflection
    /// </summary>
    [Server]
    public void RefreshStatsFromSerializedFields()
    {
        Debug.Log($"SYNC_DEBUG: RefreshStatsFromSerializedFields called for {entityType} '{EntityName.Value}' - BEFORE refresh: MaxHealth={MaxHealth.Value}, MaxEnergy={MaxEnergy.Value}, _maxHealth={_maxHealth}, _maxEnergy={_maxEnergy}");
        
        // Update SyncVars from current serialized field values
        MaxHealth.Value = _maxHealth;
        MaxEnergy.Value = _maxEnergy;
        CurrentHealth.Value = _currentHealth;
        CurrentEnergy.Value = _currentEnergy;
        
        // Ensure current values match max values
        CurrentHealth.Value = MaxHealth.Value;
        CurrentEnergy.Value = MaxEnergy.Value;
        
        // Update currency for players
        if (entityType == EntityType.Player)
        {
            Currency.Value = _currency;
            Debug.Log($"SYNC_DEBUG: Set currency for player '{EntityName.Value}' to {Currency.Value}");
        }
        
        // Sync serialized fields back
        _maxHealth = MaxHealth.Value;
        _maxEnergy = MaxEnergy.Value;
        _currentHealth = CurrentHealth.Value;
        _currentEnergy = CurrentEnergy.Value;
        
        Debug.Log($"SYNC_DEBUG: RefreshStatsFromSerializedFields completed for {entityType} '{EntityName.Value}' - AFTER refresh: MaxHealth={MaxHealth.Value}, MaxEnergy={MaxEnergy.Value}, CurrentHealth={CurrentHealth.Value}, CurrentEnergy={CurrentEnergy.Value}");
        
        // CRITICAL: Force sync to all clients immediately after data is applied
        // This ensures clients get the updated values even if they connected before PlayerSpawner ran
        StartCoroutine(ForceResyncToClientsDelayed());
    }
    
    /// <summary>
    /// Forces a resync of all SyncVars to clients with a small delay
    /// Used after PlayerSpawner applies character/pet data to ensure clients get updated values
    /// </summary>
    [Server]
    private System.Collections.IEnumerator ForceResyncToClientsDelayed()
    {
        // Wait one frame to ensure the SyncVar changes are processed
        yield return null;
        
        Debug.Log($"SYNC_DEBUG: ForceResyncToClients (delayed) called for {entityType} '{EntityName.Value}'");
        
        // Store current values
        string currentName = EntityName.Value;
        int currentMaxHealth = MaxHealth.Value;
        int currentMaxEnergy = MaxEnergy.Value;
        int currentHealth = CurrentHealth.Value;
        int currentEnergy = CurrentEnergy.Value;
        int currentCurrency = Currency.Value;
        
        Debug.Log($"SYNC_DEBUG: Current values before force sync - Name: '{currentName}', MaxEnergy: {currentMaxEnergy}, CurrentEnergy: {currentEnergy}");
        
        // Temporarily set to different values to force dirty
        EntityName.Value = currentName + "_temp";
        MaxHealth.Value = currentMaxHealth + 1;
        MaxEnergy.Value = currentMaxEnergy + 1;
        CurrentHealth.Value = currentHealth + 1;
        CurrentEnergy.Value = currentEnergy + 1;
        
        if (entityType == EntityType.Player)
        {
            Currency.Value = currentCurrency + 1;
        }
        
        Debug.Log($"SYNC_DEBUG: Set temporary values - Name: '{EntityName.Value}', MaxEnergy: {MaxEnergy.Value}");
        
        // Wait another frame to ensure the temporary values are sent
        yield return null;
        
        // Set back to correct values - this forces network update
        EntityName.Value = currentName;
        MaxHealth.Value = currentMaxHealth;
        MaxEnergy.Value = currentMaxEnergy;
        CurrentHealth.Value = currentHealth;
        CurrentEnergy.Value = currentEnergy;
        
        if (entityType == EntityType.Player)
        {
            Currency.Value = currentCurrency;
        }
        
        Debug.Log($"SYNC_DEBUG: Force resync completed for {entityType} '{EntityName.Value}' - final values: MaxEnergy={MaxEnergy.Value}, CurrentEnergy={CurrentEnergy.Value}");
    }
    
    /// <summary>
    /// Checks if any EntityStatsUIController instances are properly linked to this entity
    /// </summary>
    private void CheckUIControllerLinks()
    {
        EntityStatsUIController[] allControllers = FindObjectsOfType<EntityStatsUIController>(true);
        Debug.Log($"SYNC_DEBUG: UI_CHECK - Found {allControllers.Length} EntityStatsUIController instances");
        
        int linkedCount = 0;
        foreach (var controller in allControllers)
        {
            if (controller.IsLinked())
            {
                var linkedEntity = controller.GetLinkedEntity();
                if (linkedEntity == this)
                {
                    linkedCount++;
                    Debug.Log($"SYNC_DEBUG: UI_CHECK - Controller '{controller.gameObject.name}' is linked to this entity ({EntityName.Value})");
                }
                else if (linkedEntity != null)
                {
                    Debug.Log($"SYNC_DEBUG: UI_CHECK - Controller '{controller.gameObject.name}' is linked to different entity ({linkedEntity.EntityName.Value})");
                }
            }
            else
            {
                Debug.Log($"SYNC_DEBUG: UI_CHECK - Controller '{controller.gameObject.name}' is NOT linked to any entity");
            }
        }
        
        if (linkedCount == 0)
        {
            Debug.LogWarning($"SYNC_DEBUG: UI_CHECK - WARNING: No UI controllers are linked to {EntityName.Value}! UI won't update!");
            Debug.Log($"SYNC_DEBUG: UI_CHECK - Attempting to force reconnect UI controllers...");
            
            // Try to force reconnect UI controllers
            EntityStatsUIController.ForceReconnectAllStatsUIControllers();
        }
        else
        {
            Debug.Log($"SYNC_DEBUG: UI_CHECK - {linkedCount} UI controllers are properly linked to {EntityName.Value}");
        }
    }

    #endregion

    #region Animation

    /// <summary>
    /// Gets the NetworkEntityAnimator component for this entity
    /// </summary>
    public NetworkEntityAnimator GetAnimator()
    {
        return entityAnimator;
    }

    /// <summary>
    /// Triggers idle animation if animator is available
    /// </summary>
    [Server]
    public void TriggerIdleAnimation()
    {
        if (entityAnimator != null)
        {
            entityAnimator.StartIdleAnimation();
        }
    }

    /// <summary>
    /// Legacy method - now triggers idle animation for backwards compatibility
    /// </summary>
    [Server]
    public void TriggerSpawnAnimation()
    {
        if (entityAnimator != null)
        {
            entityAnimator.StartIdleAnimation();
        }
    }

    #endregion
} 