using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Object;
using System.Collections.Generic;
using System;

/// <summary>
/// Controls the UI display of entity stats (health, energy, effects, etc.)
/// This is attached to the separate stats UI prefab that gets linked to main entities
/// Attach to: EntityStatsUI prefab
/// 
/// DEBUG: Added extensive logging with [STATS_UI_LINK] prefix to track linking issues.
/// Look for these logs to see which stats UI is linking to which entity.
/// </summary>
public class EntityStatsUIController : NetworkBehaviour
{
    [Header("Required Components")]
    [SerializeField] private NetworkEntity statsEntity; // The stats UI entity itself
    [SerializeField] private RelationshipManager relationshipManager; // Links back to main entity
    
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI energyText;
    [SerializeField] private Image healthBar;
    [SerializeField] private Image energyBar;
    
    [Header("Status Effects UI")]
    [SerializeField] private TextMeshProUGUI effectsText;
    [SerializeField] private Transform effectsContainer; // Optional: for individual effect icons later
    
    [Header("Player-Only UI")]
    [SerializeField] private TextMeshProUGUI currencyText;
    [SerializeField] private GameObject currencyContainer; // Hide for pets
    
    [Header("Card System UI")]
    [SerializeField] private TextMeshProUGUI deckCountText;
    [SerializeField] private TextMeshProUGUI discardCountText;
    
    [Header("Damage Preview UI")]
    [SerializeField] private TextMeshProUGUI damagePreviewText;
    [SerializeField] private GameObject damagePreviewContainer;
    
    // Reference to the main entity this UI represents
    private NetworkEntity linkedEntity;
    private LifeHandler linkedLifeHandler;
    private EnergyHandler linkedEnergyHandler;
    private EffectHandler linkedEffectHandler;
    private NetworkEntityDeck linkedEntityDeck;
    private EntityTracker linkedEntityTracker;
    
    // Track current values to avoid unnecessary updates
    private int lastHealth = -1;
    private int lastMaxHealth = -1;
    private int lastEnergy = -1;
    private int lastMaxEnergy = -1;
    private int lastCurrency = -1;
    private string lastEffectsText = "";
    private int lastComboCount = -1;

    private void Awake()
    {
        // Get required components
        if (statsEntity == null) statsEntity = GetComponent<NetworkEntity>();
        if (relationshipManager == null) relationshipManager = GetComponent<RelationshipManager>();
        
        // Validate stats entity type
        if (statsEntity != null && statsEntity.EntityType != EntityType.PlayerStatsUI && statsEntity.EntityType != EntityType.PetStatsUI)
        {
            Debug.LogError($"EntityStatsUIController: Stats entity {statsEntity.name} has wrong EntityType: {statsEntity.EntityType}. Expected PlayerStatsUI or PetStatsUI.");
        }
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Try to link to main entity if not already linked
        TryLinkToMainEntity();
    }
    
    /// <summary>
    /// Links this stats UI to its corresponding main entity
    /// Called during entity spawning or when relationships are established
    /// </summary>
    public void LinkToEntity(NetworkEntity mainEntity)
    {
        if (mainEntity == null)
        {
            Debug.LogError("EntityStatsUIController: Cannot link to null entity");
            return;
        }
        
        Debug.Log($"[STATS_UI_LINK] Attempting to link {(statsEntity != null ? statsEntity.EntityName.Value : "unknown")} to {mainEntity.EntityName.Value} (ID: {mainEntity.ObjectId}, Owner: {mainEntity.Owner?.ClientId ?? -1})");
        
        // Unsubscribe from previous entity if any
        UnlinkFromCurrentEntity();
        
        linkedEntity = mainEntity;
        
        // Get handler components from the main entity
        linkedLifeHandler = linkedEntity.GetComponent<LifeHandler>();
        linkedEnergyHandler = linkedEntity.GetComponent<EnergyHandler>();
        linkedEffectHandler = linkedEntity.GetComponent<EffectHandler>();
        linkedEntityDeck = linkedEntity.GetComponent<NetworkEntityDeck>();
        linkedEntityTracker = linkedEntity.GetComponent<EntityTracker>();
        
        Debug.Log($"[STATS_UI_LINK] Component linking results for {mainEntity.EntityName.Value} - LifeHandler: {linkedLifeHandler != null}, EnergyHandler: {linkedEnergyHandler != null}, EffectHandler: {linkedEffectHandler != null}, EntityDeck: {linkedEntityDeck != null}, EntityTracker: {linkedEntityTracker != null}");
        
        // Subscribe to stat changes
        SubscribeToStatChanges();
        
        // Hide currency UI for pets
        if (currencyContainer != null)
        {
            currencyContainer.SetActive(linkedEntity.EntityType == EntityType.Player);
        }
        
        // Initial UI update
        RefreshAllStats();
        
        Debug.Log($"[STATS_UI_LINK] Successfully linked {(statsEntity != null ? statsEntity.EntityName.Value : "unknown")} to {linkedEntity.EntityName.Value}");
    }
    
    /// <summary>
    /// Attempts to find and link to the main entity via RelationshipManager
    /// </summary>
    public void TryLinkToMainEntity()
    {
        if (relationshipManager == null)
        {
            Debug.LogError("EntityStatsUIController: Missing RelationshipManager");
            return;
        }
        
        Debug.Log($"[STATS_UI_LINK] TryLinkToMainEntity called for {(statsEntity != null ? statsEntity.EntityName.Value : "unknown stats UI")}");
        
        // The ally entity should be set to the main entity that owns this stats UI
        if (relationshipManager.AllyEntity != null)
        {
            NetworkEntity mainEntity = relationshipManager.AllyEntity.GetComponent<NetworkEntity>();
            if (mainEntity != null)
            {
                Debug.Log($"[STATS_UI_LINK] Found ally entity: {mainEntity.EntityName.Value} (ID: {mainEntity.ObjectId})");
                LinkToEntity(mainEntity);
            }
            else
            {
                Debug.LogError($"[STATS_UI_LINK] AllyEntity exists but has no NetworkEntity component!");
            }
        }
        else
        {
            Debug.LogWarning($"[STATS_UI_LINK] No ally entity set for {(statsEntity != null ? statsEntity.EntityName.Value : "unknown stats UI")} - cannot link");
        }
    }
    
    /// <summary>
    /// Subscribes to stat change events from the linked entity
    /// </summary>
    private void SubscribeToStatChanges()
    {
        if (linkedEntity == null) return;
        
        // Subscribe to basic stat changes
        linkedEntity.EntityName.OnChange += OnEntityNameChanged;
        linkedEntity.CurrentHealth.OnChange += OnCurrentHealthChanged;
        linkedEntity.MaxHealth.OnChange += OnMaxHealthChanged;
        linkedEntity.CurrentEnergy.OnChange += OnCurrentEnergyChanged;
        linkedEntity.MaxEnergy.OnChange += OnMaxEnergyChanged;
        
        // Subscribe to currency changes for players
        if (linkedEntity.EntityType == EntityType.Player)
        {
            linkedEntity.OnCurrencyChanged += UpdateCurrencyDisplay;
        }
        
        // Subscribe to handler events
        if (linkedLifeHandler != null)
        {
            linkedLifeHandler.OnDamageTaken += OnDamageTaken;
            linkedLifeHandler.OnHealingReceived += OnHealingReceived;
        }
        
        if (linkedEnergyHandler != null)
        {
            linkedEnergyHandler.OnEnergySpent += OnEnergySpent;
            linkedEnergyHandler.OnEnergyGained += OnEnergyGained;
        }
        
        if (linkedEffectHandler != null)
        {
            linkedEffectHandler.OnEffectsChanged += UpdateEffectsDisplay;
            /* Debug.Log($"EntityStatsUIController: Subscribed to OnEffectsChanged for {linkedEntity.EntityName.Value}"); */
        }
        else
        {
            /* Debug.Log($"EntityStatsUIController: linkedEffectHandler is null for {linkedEntity.EntityName.Value}, cannot subscribe to effect changes"); */
        }
        
        if (linkedEntityDeck != null)
        {
            linkedEntityDeck.OnDeckChanged += UpdateDeckDisplay;
        }
        
        if (linkedEntityTracker != null)
        {
            linkedEntityTracker.OnComboChanged += OnComboChanged;
            linkedEntityTracker.OnStanceChanged += OnStanceChanged;
        }
    }
    
    /// <summary>
    /// Unsubscribes from stat change events
    /// </summary>
    private void UnsubscribeFromStatChanges()
    {
        if (linkedEntity == null) return;
        
        // Unsubscribe from basic stat changes
        linkedEntity.EntityName.OnChange -= OnEntityNameChanged;
        linkedEntity.CurrentHealth.OnChange -= OnCurrentHealthChanged;
        linkedEntity.MaxHealth.OnChange -= OnMaxHealthChanged;
        linkedEntity.CurrentEnergy.OnChange -= OnCurrentEnergyChanged;
        linkedEntity.MaxEnergy.OnChange -= OnMaxEnergyChanged;
        
        // Unsubscribe from currency changes for players
        if (linkedEntity.EntityType == EntityType.Player)
        {
            linkedEntity.OnCurrencyChanged -= UpdateCurrencyDisplay;
        }
        
        // Unsubscribe from handler events
        if (linkedLifeHandler != null)
        {
            linkedLifeHandler.OnDamageTaken -= OnDamageTaken;
            linkedLifeHandler.OnHealingReceived -= OnHealingReceived;
        }
        
        if (linkedEnergyHandler != null)
        {
            linkedEnergyHandler.OnEnergySpent -= OnEnergySpent;
            linkedEnergyHandler.OnEnergyGained -= OnEnergyGained;
        }
        
        if (linkedEffectHandler != null)
        {
            linkedEffectHandler.OnEffectsChanged -= UpdateEffectsDisplay;
        }
        
        if (linkedEntityDeck != null)
        {
            linkedEntityDeck.OnDeckChanged -= UpdateDeckDisplay;
        }
        
        if (linkedEntityTracker != null)
        {
            linkedEntityTracker.OnComboChanged -= OnComboChanged;
            linkedEntityTracker.OnStanceChanged -= OnStanceChanged;
        }
    }
    
    /// <summary>
    /// Unlinks from the current entity and cleans up subscriptions
    /// </summary>
    private void UnlinkFromCurrentEntity()
    {
        if (linkedEntity != null)
        {
            UnsubscribeFromStatChanges();
            linkedEntity = null;
            linkedLifeHandler = null;
            linkedEnergyHandler = null;
            linkedEffectHandler = null;
            linkedEntityDeck = null;
            linkedEntityTracker = null;
        }
    }
    
    /// <summary>
    /// Refreshes all UI elements with current stat values
    /// </summary>
    public void RefreshAllStats()
    {
        if (linkedEntity == null) return;
        
        UpdateEntityNameUI();
        UpdateHealthUI();
        UpdateEnergyUI();
        UpdateCurrencyDisplay(linkedEntity.Currency.Value);
        UpdateEffectsDisplay();
        UpdateDeckDisplay();
    }
    
    #region Stat Change Handlers
    
    private void OnEntityNameChanged(string prev, string next, bool asServer)
    {
        UpdateEntityNameUI();
    }
    
    private void OnCurrentHealthChanged(int prev, int next, bool asServer)
    {
        UpdateHealthUI();
    }
    
    private void OnMaxHealthChanged(int prev, int next, bool asServer)
    {
        UpdateHealthUI();
    }
    
    private void OnCurrentEnergyChanged(int prev, int next, bool asServer)
    {
        UpdateEnergyUI();
    }
    
    private void OnMaxEnergyChanged(int prev, int next, bool asServer)
    {
        UpdateEnergyUI();
    }
    
    private void OnDamageTaken(int amount, NetworkEntity source)
    {
        // Could add damage animation or effects here
        UpdateHealthUI();
    }
    
    private void OnHealingReceived(int amount, NetworkEntity source)
    {
        // Could add healing animation or effects here
        UpdateHealthUI();
    }
    
    private void OnEnergySpent(int amount, NetworkEntity source)
    {
        // Could add energy spending animation or effects here
        UpdateEnergyUI();
    }
    
    private void OnEnergyGained(int amount, NetworkEntity source)
    {
        // Could add energy gaining animation or effects here
        UpdateEnergyUI();
    }
    
    private void OnComboChanged(int newComboCount)
    {
        UpdateEffectsDisplay();
    }
    
    private void OnStanceChanged(StanceType oldStance, StanceType newStance)
    {
        Debug.Log($"EntityStatsUIController: OnStanceChanged called for {(linkedEntity != null ? linkedEntity.EntityName.Value : "unknown entity")} - Old: {oldStance}, New: {newStance}");
        UpdateEffectsDisplay();
    }
    
    #endregion
    
    #region UI Update Methods
    
    private void UpdateEntityNameUI()
    {
        if (nameText != null && linkedEntity != null)
        {
            nameText.text = linkedEntity.EntityName.Value;
        }
    }
    
    private void UpdateHealthUI()
    {
        if (linkedEntity == null) return;
        
        int currentHealth = linkedEntity.CurrentHealth.Value;
        int maxHealth = linkedEntity.MaxHealth.Value;
        
        // Avoid unnecessary updates
        if (currentHealth == lastHealth && maxHealth == lastMaxHealth) return;
        
        lastHealth = currentHealth;
        lastMaxHealth = maxHealth;
        
        // Update health text
        if (healthText != null)
        {
            healthText.text = $"{currentHealth}/{maxHealth}";
        }
        
        // Update health bar
        if (healthBar != null && maxHealth > 0)
        {
            healthBar.fillAmount = (float)currentHealth / maxHealth;
        }
    }
    
    private void UpdateEnergyUI()
    {
        if (linkedEntity == null) return;
        
        int currentEnergy = linkedEntity.CurrentEnergy.Value;
        int maxEnergy = linkedEntity.MaxEnergy.Value;
        
        // Avoid unnecessary updates
        if (currentEnergy == lastEnergy && maxEnergy == lastMaxEnergy) return;
        
        lastEnergy = currentEnergy;
        lastMaxEnergy = maxEnergy;
        
        // Update energy text
        if (energyText != null)
        {
            energyText.text = $"{currentEnergy}/{maxEnergy}";
        }
        
        // Update energy bar
        if (energyBar != null && maxEnergy > 0)
        {
            energyBar.fillAmount = (float)currentEnergy / maxEnergy;
        }
    }
    
    private void UpdateCurrencyDisplay(int newCurrency)
    {
        // Avoid unnecessary updates
        if (newCurrency == lastCurrency) return;
        
        lastCurrency = newCurrency;
        
        if (currencyText != null)
        {
            currencyText.text = $"{newCurrency} Gold";
        }
    }
    
    private void UpdateEffectsDisplay()
    {
        if (linkedEffectHandler == null) 
        {
            Debug.Log($"EntityStatsUIController: UpdateEffectsDisplay called but linkedEffectHandler is null for {(linkedEntity != null ? linkedEntity.EntityName.Value : "unknown entity")}");
            return;
        }
        
        var activeEffects = linkedEffectHandler.GetAllEffects();
        
        Debug.Log($"EntityStatsUIController: UpdateEffectsDisplay called for {linkedEntity.EntityName.Value}, found {activeEffects.Count} effects");
        
        // Build effects text
        string effectsDisplayText = "";
        List<string> effectStrings = new List<string>();
        
        // Add current stance if not None
        if (linkedEntityTracker != null)
        {
            StanceType currentStance = linkedEntityTracker.CurrentStance;
            Debug.Log($"EntityStatsUIController: UpdateEffectsDisplay for {linkedEntity.EntityName.Value} - Current stance: {currentStance}");
            if (currentStance != StanceType.None)
            {
                effectStrings.Add($"Stance: {currentStance}");
                Debug.Log($"EntityStatsUIController: Added stance '{currentStance}' to effect list for {linkedEntity.EntityName.Value}");
            }
            else
            {
                Debug.Log($"EntityStatsUIController: Stance is None for {linkedEntity.EntityName.Value}, not adding to effect list");
            }
        }
        else
        {
            Debug.Log($"EntityStatsUIController: No linkedEntityTracker found for {linkedEntity.EntityName.Value}");
        }
        
        // Add combo count if greater than 0
        if (linkedEntityTracker != null && linkedEntityTracker.ComboCount > 0)
        {
            effectStrings.Add($"Combo ({linkedEntityTracker.ComboCount})");
        }
        
        if (activeEffects.Count > 0)
        {
            foreach (var effect in activeEffects)
            {
                string effectStr;
                
                // Format based on effect type mechanics (matching EffectHandler.GetActiveEffects logic)
                switch (effect.EffectName)
                {
                    case "Strength":
                    case "Curse":
                    case "Burn":
                    case "Salve":
                        // Always show potency for damage modifiers and DoT/HoT effects
                        effectStr = $"{effect.EffectName} ({effect.Potency})";
                        break;
                        
                    case "Break":
                    case "Weak":
                        // Show duration only (they don't have meaningful potency)
                        effectStr = effect.RemainingDuration > 1 ? 
                            $"{effect.EffectName} ({effect.RemainingDuration})" : 
                            effect.EffectName;
                        break;
                        
                    case "Thorns":
                        // Always show potency for Thorns since the amount is important
                        effectStr = $"{effect.EffectName} ({effect.Potency})";
                        break;
                        
                    case "Shield":
                        // Always show potency for Shield since the amount is important
                        effectStr = $"{effect.EffectName} ({effect.Potency})";
                        break;
                        
                    default:
                        // For other effects, show potency if > 0, otherwise show duration if > 1
                        if (effect.Potency > 1)
                        {
                            effectStr = $"{effect.EffectName} ({effect.Potency})";
                        }
                        else if (effect.RemainingDuration > 1)
                        {
                            effectStr = $"{effect.EffectName} ({effect.RemainingDuration})";
                        }
                        else
                        {
                            effectStr = effect.EffectName;
                        }
                        break;
                }
                
                effectStrings.Add(effectStr);
                
                /* Debug.Log($"EntityStatsUIController: Effect - {effect.EffectName}, Potency: {effect.Potency}, Duration: {effect.RemainingDuration}"); */
            }
        }
        
        if (effectStrings.Count > 0)
        {
            effectsDisplayText = string.Join(", ", effectStrings);
        }
        else
        {
            effectsDisplayText = "None";
        }
        
        /* Debug.Log($"EntityStatsUIController: Effects display text for {linkedEntity.EntityName.Value}: '{effectsDisplayText}'"); */
        
        // Avoid unnecessary updates
        if (effectsDisplayText == lastEffectsText) 
        {
            /* Debug.Log($"EntityStatsUIController: Effects display text unchanged for {linkedEntity.EntityName.Value}, skipping UI update"); */
            return;
        }
        
        lastEffectsText = effectsDisplayText;
        
        if (effectsText != null)
        {
            effectsText.text = effectsDisplayText;
            Debug.Log($"EntityStatsUIController: Updated effects text UI for {linkedEntity.EntityName.Value} to: '{effectsDisplayText}'");
        }
        else
        {
            Debug.Log($"EntityStatsUIController: effectsText UI component is null for {linkedEntity.EntityName.Value}");
        }
    }
    
    private void UpdateDeckDisplay()
    {
        if (linkedEntityDeck == null) return;
        
        if (deckCountText != null)
        {
            deckCountText.text = $"Deck: {linkedEntityDeck.GetTotalCardCount()}";
        }
        
        if (discardCountText != null)
        {
            // Note: NetworkEntityDeck doesn't track discard pile separately
            // This would need to be handled by a separate discard pile system
            discardCountText.text = "Discard: 0";
        }
    }
    
    #endregion
    
    #region Damage Preview
    
    /// <summary>
    /// Shows a damage/healing preview on the UI
    /// </summary>
    public void ShowDamagePreview(int amount, bool isDamage)
    {
        if (damagePreviewContainer == null || damagePreviewText == null) return;
        
        damagePreviewText.text = isDamage ? $"-{amount}" : $"+{amount}";
        damagePreviewText.color = isDamage ? Color.red : Color.green;
        damagePreviewContainer.SetActive(true);
    }
    
    /// <summary>
    /// Hides the damage preview
    /// </summary>
    public void HideDamagePreview()
    {
        if (damagePreviewContainer != null)
        {
            damagePreviewContainer.SetActive(false);
        }
    }
    
    #endregion
    
    #region Visibility Control
    
    /// <summary>
    /// Controls the visibility of this stats UI panel
    /// Called by EntityVisibilityManager during combat visibility updates
    /// </summary>
    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
        
        // Log visibility changes for debugging
        if (statsEntity != null)
        {
            Debug.Log($"EntityStatsUIController: {statsEntity.EntityName.Value} visibility set to {visible}");
        }
    }
    
    /// <summary>
    /// Gets the current visibility state of this stats UI panel
    /// </summary>
    public bool IsVisible()
    {
        return gameObject.activeInHierarchy;
    }
    
    #endregion
    
    #region Public Properties
    
    /// <summary>
    /// Gets the linked main entity
    /// </summary>
    public NetworkEntity GetLinkedEntity()
    {
        return linkedEntity;
    }
    
    /// <summary>
    /// Checks if this stats UI is linked to a main entity
    /// </summary>
    public bool IsLinked()
    {
        return linkedEntity != null;
    }
    
    /// <summary>
    /// Manually triggers an effects display update for debugging
    /// </summary>
    public void ForceUpdateEffectsDisplay()
    {
        Debug.Log($"EntityStatsUIController: ForceUpdateEffectsDisplay called for {(linkedEntity != null ? linkedEntity.EntityName.Value : "no linked entity")}");
        UpdateEffectsDisplay();
    }
    
    #endregion
    
    private void OnDestroy()
    {
        UnlinkFromCurrentEntity();
    }

    /// <summary>
    /// Static method to manually link all unlinked EntityStatsUIController instances to their correct entities
    /// </summary>
    public static void ForceReconnectAllStatsUIControllers()
    {
        /* Debug.Log("=== FORCE RECONNECTING ALL STATS UI CONTROLLERS ==="); */
        
        // Find all EntityStatsUIController instances
        EntityStatsUIController[] allControllers = FindObjectsOfType<EntityStatsUIController>(true);
        /* Debug.Log($"Found {allControllers.Length} EntityStatsUIController instances (including inactive)"); */
        
        // Find all main entities (Player and Pet types)
        NetworkEntity[] allEntities = FindObjectsOfType<NetworkEntity>(true);
        var mainEntities = new List<NetworkEntity>();
        
        foreach (var entity in allEntities)
        {
            if (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet)
            {
                mainEntities.Add(entity);
                Debug.Log($"Found main entity: {entity.EntityName.Value} (ID: {entity.ObjectId}, Type: {entity.EntityType})");
            }
        }
        
        /* Debug.Log($"Found {mainEntities.Count} main entities"); */
        
        // Try to link unlinked controllers
        foreach (var controller in allControllers)
        {
            if (!controller.IsLinked())
            {
                NetworkEntity statsEntity = controller.GetComponent<NetworkEntity>();
                if (statsEntity == null) 
                {
                    Debug.LogError($"Controller on {controller.gameObject.name} has no NetworkEntity component!");
                    continue;
                }
                
                /* Debug.Log($"Attempting to link unlinked controller on {controller.gameObject.name} (Type: {statsEntity.EntityType})"); */
                
                // Find matching main entity based on entity type
                EntityType targetMainEntityType = statsEntity.EntityType == EntityType.PlayerStatsUI ? EntityType.Player : EntityType.Pet;
                
                NetworkEntity bestMatch = null;
                
                // First, try to find a main entity that doesn't have a stats UI linked yet
                foreach (var mainEntity in mainEntities)
                {
                    if (mainEntity.EntityType == targetMainEntityType)
                    {
                        RelationshipManager mainEntityRM = mainEntity.GetComponent<RelationshipManager>();
                        if (mainEntityRM != null)
                        {
                            // Check if this main entity already has a working stats UI link
                            bool hasWorkingStatsUI = false;
                            if (mainEntityRM.StatsUIEntity != null)
                            {
                                NetworkEntity existingStatsUI = mainEntityRM.StatsUIEntity.GetComponent<NetworkEntity>();
                                if (existingStatsUI != null)
                                {
                                    EntityStatsUIController existingController = existingStatsUI.GetComponent<EntityStatsUIController>();
                                    if (existingController != null && existingController.IsLinked())
                                    {
                                        hasWorkingStatsUI = true;
                                    }
                                }
                            }
                            
                            if (!hasWorkingStatsUI)
                            {
                                bestMatch = mainEntity;
                                /* Debug.Log($"Found potential match: {mainEntity.EntityName.Value} (no working stats UI)"); */
                                break;
                            }
                        }
                    }
                }
                
                // If no unlinked entity found, just pick the first matching type
                if (bestMatch == null)
                {
                    foreach (var mainEntity in mainEntities)
                    {
                        if (mainEntity.EntityType == targetMainEntityType)
                        {
                            bestMatch = mainEntity;
                            /* Debug.Log($"Using fallback match: {mainEntity.EntityName.Value}"); */
                            break;
                        }
                    }
                }
                
                if (bestMatch != null)
                {
                    /* Debug.Log($"Linking {controller.gameObject.name} to {bestMatch.EntityName.Value}"); */
                    
                    // Set up the relationship both ways
                    RelationshipManager statsEntityRM = controller.GetComponent<RelationshipManager>();
                    RelationshipManager mainEntityRM = bestMatch.GetComponent<RelationshipManager>();
                    
                    if (statsEntityRM != null && mainEntityRM != null)
                    {
                        /* Debug.Log($"Setting up relationships: StatsUI -> Main, Main -> StatsUI"); */
                        statsEntityRM.SetAlly(bestMatch);
                        mainEntityRM.SetStatsUI(statsEntity);
                    }
                    else
                    {
                        Debug.LogError($"Missing RelationshipManager - StatsEntity: {statsEntityRM != null}, MainEntity: {mainEntityRM != null}");
                    }
                    
                    // Force the controller to link
                    /* Debug.Log($"Calling LinkToEntity..."); */
                    controller.LinkToEntity(bestMatch);
                    
                    /* Debug.Log($"Link complete - IsLinked: {controller.IsLinked()}"); */
                }
                else
                {
                    Debug.LogWarning($"No suitable main entity found for controller type {targetMainEntityType}");
                }
            }
            else
            {
                /* Debug.Log($"Controller on {controller.gameObject.name} is already linked to {controller.GetLinkedEntity().EntityName.Value}"); */
            }
        }
        
        /* Debug.Log("=== END FORCE RECONNECT ==="); */
    }

    /// <summary>
    /// Debug method to show all relationship statuses in Inspector-friendly format
    /// Call this during runtime to see what's linked and what's not
    /// </summary>
    public static void DebugAllRelationshipStatuses()
    {
        /* Debug.Log("=== RELATIONSHIP STATUS DEBUG ==="); */
        
        // Find all entities
        NetworkEntity[] allEntities = FindObjectsOfType<NetworkEntity>(true);
        var playerEntities = new List<NetworkEntity>();
        var petEntities = new List<NetworkEntity>();
        var statsUIEntities = new List<NetworkEntity>();
        
        foreach (var entity in allEntities)
        {
            switch (entity.EntityType)
            {
                case EntityType.Player:
                    playerEntities.Add(entity);
                    break;
                case EntityType.Pet:
                    petEntities.Add(entity);
                    break;
                case EntityType.PlayerStatsUI:
                case EntityType.PetStatsUI:
                    statsUIEntities.Add(entity);
                    break;
            }
        }
        
        /* Debug.Log($"Found: {playerEntities.Count} Players, {petEntities.Count} Pets, {statsUIEntities.Count} Stats UIs"); */
        
        // Check Player relationships
        /* Debug.Log("--- PLAYER ENTITIES ---"); */
        foreach (var player in playerEntities)
        {
            RelationshipManager rm = player.GetComponent<RelationshipManager>();
            if (rm != null)
            {
                string statsUIStatus = rm.StatsUIEntity != null ? 
                    $"LINKED to {rm.StatsUIEntity.GetComponent<NetworkEntity>()?.EntityName.Value ?? "unknown"}" : 
                    "NOT LINKED";
                string allyStatus = rm.AllyEntity != null ? 
                    $"LINKED to {rm.AllyEntity.GetComponent<NetworkEntity>()?.EntityName.Value ?? "unknown"}" : 
                    "NOT LINKED";
                
                /* Debug.Log($"  {player.EntityName.Value} (ID: {player.ObjectId}):"); */
                /* Debug.Log($"    StatsUI: {statsUIStatus}"); */
                /* Debug.Log($"    Ally: {allyStatus}"); */
            }
            else
            {
                Debug.LogError($"  {player.EntityName.Value}: NO RelationshipManager!");
            }
        }
        
        // Check Pet relationships
        /* Debug.Log("--- PET ENTITIES ---"); */
        foreach (var pet in petEntities)
        {
            RelationshipManager rm = pet.GetComponent<RelationshipManager>();
            if (rm != null)
            {
                string statsUIStatus = rm.StatsUIEntity != null ? 
                    $"LINKED to {rm.StatsUIEntity.GetComponent<NetworkEntity>()?.EntityName.Value ?? "unknown"}" : 
                    "NOT LINKED";
                string allyStatus = rm.AllyEntity != null ? 
                    $"LINKED to {rm.AllyEntity.GetComponent<NetworkEntity>()?.EntityName.Value ?? "unknown"}" : 
                    "NOT LINKED";
                
                /* Debug.Log($"  {pet.EntityName.Value} (ID: {pet.ObjectId}):"); */
                /* Debug.Log($"    StatsUI: {statsUIStatus}"); */
                /* Debug.Log($"    Ally: {allyStatus}"); */
            }
            else
            {
                Debug.LogError($"  {pet.EntityName.Value}: NO RelationshipManager!");
            }
        }
        
        // Check Stats UI relationships
        /* Debug.Log("--- STATS UI ENTITIES ---"); */
        foreach (var statsUI in statsUIEntities)
        {
            RelationshipManager rm = statsUI.GetComponent<RelationshipManager>();
            EntityStatsUIController controller = statsUI.GetComponent<EntityStatsUIController>();
            
            if (rm != null && controller != null)
            {
                string allyStatus = rm.AllyEntity != null ? 
                    $"LINKED to {rm.AllyEntity.GetComponent<NetworkEntity>()?.EntityName.Value ?? "unknown"}" : 
                    "NOT LINKED";
                string controllerStatus = controller.IsLinked() ? 
                    $"LINKED to {controller.GetLinkedEntity().EntityName.Value}" : 
                    "NOT LINKED";
                
                /* Debug.Log($"  {statsUI.EntityName.Value} (ID: {statsUI.ObjectId}, Type: {statsUI.EntityType}):"); */
                /* Debug.Log($"    RelationshipManager Ally: {allyStatus}"); */
                /* Debug.Log($"    Controller Link: {controllerStatus}"); */
                /* Debug.Log($"    GameObject Active: {statsUI.gameObject.activeInHierarchy}"); */
            }
            else
            {
                Debug.LogError($"  {statsUI.EntityName.Value}: Missing components - RM: {rm != null}, Controller: {controller != null}");
            }
        }
        
        /* Debug.Log("=== END RELATIONSHIP DEBUG ==="); */
    }
} 