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
    
    // Track current values to avoid unnecessary updates
    private int lastHealth = -1;
    private int lastMaxHealth = -1;
    private int lastEnergy = -1;
    private int lastMaxEnergy = -1;
    private int lastCurrency = -1;
    private string lastEffectsText = "";

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
        
        // Unsubscribe from previous entity if any
        UnlinkFromCurrentEntity();
        
        linkedEntity = mainEntity;
        
        // Get handler components from the main entity
        linkedLifeHandler = linkedEntity.GetComponent<LifeHandler>();
        linkedEnergyHandler = linkedEntity.GetComponent<EnergyHandler>();
        linkedEffectHandler = linkedEntity.GetComponent<EffectHandler>();
        linkedEntityDeck = linkedEntity.GetComponent<NetworkEntityDeck>();
        
        // Subscribe to stat changes
        SubscribeToStatChanges();
        
        // Hide currency UI for pets
        if (currencyContainer != null)
        {
            currencyContainer.SetActive(linkedEntity.EntityType == EntityType.Player);
        }
        
        // Initial UI update
        RefreshAllStats();
        
        Debug.Log($"EntityStatsUIController: Successfully linked {statsEntity.EntityName.Value} to {linkedEntity.EntityName.Value}");
    }
    
    /// <summary>
    /// Attempts to find and link to the main entity via RelationshipManager
    /// </summary>
    private void TryLinkToMainEntity()
    {
        if (relationshipManager == null)
        {
            Debug.LogError("EntityStatsUIController: Missing RelationshipManager");
            return;
        }
        
        // The ally entity should be set to the main entity that owns this stats UI
        if (relationshipManager.AllyEntity != null)
        {
            NetworkEntity mainEntity = relationshipManager.AllyEntity.GetComponent<NetworkEntity>();
            if (mainEntity != null)
            {
                LinkToEntity(mainEntity);
            }
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
        }
        
        if (linkedEntityDeck != null)
        {
            linkedEntityDeck.OnDeckChanged += UpdateDeckDisplay;
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
        if (linkedEffectHandler == null) return;
        
        var activeEffects = linkedEffectHandler.GetAllEffects();
        
        // Build effects text
        string effectsDisplayText = "";
        if (activeEffects.Count > 0)
        {
            List<string> effectStrings = new List<string>();
            foreach (var effect in activeEffects)
            {
                string effectStr = effect.EffectName;
                if (effect.RemainingDuration > 0)
                    effectStr += $" ({effect.RemainingDuration})";
                if (effect.Potency > 1)
                    effectStr += $" x{effect.Potency}";
                effectStrings.Add(effectStr);
            }
            effectsDisplayText = string.Join(", ", effectStrings);
        }
        else
        {
            effectsDisplayText = "None";
        }
        
        // Avoid unnecessary updates
        if (effectsDisplayText == lastEffectsText) return;
        
        lastEffectsText = effectsDisplayText;
        
        if (effectsText != null)
        {
            effectsText.text = effectsDisplayText;
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
    
    #endregion
    
    private void OnDestroy()
    {
        UnlinkFromCurrentEntity();
    }
} 