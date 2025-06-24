using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Handles the display of pet status effects.
/// Syncs with the pet's EffectHandler to show current active effects.
/// Attach to: OwnPetViewPrefab GameObject
/// </summary>
public class OwnPetStatusEffectsDisplay : MonoBehaviour, IUpdatablePetDisplay
{
    [Header("Status Effects UI")]
    [SerializeField] private TextMeshProUGUI statusEffectsText;
    [SerializeField] private GameObject statusEffectsContainer;
    
    [Header("Display Settings")]
    [SerializeField] private string noEffectsText = "No active effects";
    [SerializeField] private bool showEffectDuration = true;
    [SerializeField] private bool showEffectPotency = true;
    [SerializeField] private int maxEffectsToShow = 10;
    
    [Header("Text Formatting")]
    [SerializeField] private string effectFormat = "• {0}";
    [SerializeField] private string effectWithPotencyFormat = "• {0} ({1})";
    [SerializeField] private string effectWithDurationFormat = "• {0} - {1} turns";
    [SerializeField] private string effectFullFormat = "• {0} ({1}) - {2} turns";
    
    [Header("Debug")]
    [SerializeField] private bool debugLogEnabled = true;
    
    // Current pet being displayed
    private NetworkEntity currentPet;
    private EffectHandler currentEffectHandler;
    private EntityTracker currentEntityTracker;
    
    private void Awake()
    {
        ValidateComponents();
    }
    
    private void ValidateComponents()
    {
        if (statusEffectsText == null)
        {
            statusEffectsText = GetComponentInChildren<TextMeshProUGUI>();
            if (statusEffectsText == null)
                LogError("Status Effects Text component not found! Please assign it in the inspector.");
        }
        
        if (statusEffectsContainer == null)
        {
            statusEffectsContainer = statusEffectsText?.transform.parent?.gameObject;
        }
    }
    
    /// <summary>
    /// Sets the pet whose status effects should be displayed
    /// </summary>
    /// <param name="pet">The NetworkEntity pet to display effects for</param>
    public void SetPet(NetworkEntity pet)
    {
        // Unsubscribe from previous pet's effect handler
        UnsubscribeFromEffectHandler();
        
        currentPet = pet;
        currentEffectHandler = null;
        currentEntityTracker = null;
        
        if (pet != null)
        {
            // Get the pet's EffectHandler component
            currentEffectHandler = pet.GetComponent<EffectHandler>();
            currentEntityTracker = pet.GetComponent<EntityTracker>();
            
            if (currentEffectHandler != null)
            {
                // Subscribe to effect changes
                SubscribeToEffectHandler();
                
                // Update display immediately
                UpdateStatusEffectsDisplay();
                
                LogDebug($"Status effects display set to pet: {pet.EntityName.Value}");
            }
            else
            {
                LogDebug($"No EffectHandler found on pet: {pet.EntityName.Value}");
                ClearStatusEffectsDisplay();
            }
            
            // Show the container
            SetContainerVisible(true);
        }
        else
        {
            ClearStatusEffectsDisplay();
            SetContainerVisible(false);
            LogDebug("Status effects display cleared");
        }
    }
    
    /// <summary>
    /// Gets the current pet being displayed
    /// </summary>
    public NetworkEntity GetCurrentPet()
    {
        return currentPet;
    }
    
    /// <summary>
    /// Updates the display to reflect current pet data (implements IUpdatablePetDisplay)
    /// </summary>
    public void UpdateDisplay()
    {
        UpdateStatusEffectsDisplay();
    }
    
    /// <summary>
    /// Updates the status effects display text
    /// </summary>
    public void UpdateStatusEffectsDisplay()
    {
        if (statusEffectsText == null) return;
        
        if (currentEffectHandler == null)
        {
            statusEffectsText.text = noEffectsText;
            return;
        }
        
        // Get all active effects from the EffectHandler
        List<EffectHandler.StatusEffect> activeEffects = currentEffectHandler.GetAllEffects();
        
        // Build the effects text
        StringBuilder effectsText = new StringBuilder();
        int effectsShown = 0;
        
        // Add combo count if greater than 0
        if (currentEntityTracker != null && currentEntityTracker.ComboCount > 0)
        {
            string comboLine = string.Format(effectFormat, $"Combo ({currentEntityTracker.ComboCount})");
            effectsText.AppendLine(comboLine);
            effectsShown++;
        }
        
        if (activeEffects == null || activeEffects.Count == 0)
        {
            // If no effects but we have combo, show what we have
            if (effectsShown > 0)
            {
                statusEffectsText.text = effectsText.ToString().TrimEnd();
                LogDebug($"Updated status effects display: {effectsShown} effects shown (combo only)");
                return;
            }
            
            statusEffectsText.text = noEffectsText;
            LogDebug("No active effects to display");
            return;
        }
        
        foreach (var effect in activeEffects)
        {
            if (effectsShown >= maxEffectsToShow) break;
            
            string effectLine = FormatEffectText(effect);
            effectsText.AppendLine(effectLine);
            effectsShown++;
        }
        
        // Show truncation message if there are more effects
        if (activeEffects.Count > maxEffectsToShow)
        {
            effectsText.AppendLine($"... and {activeEffects.Count - maxEffectsToShow} more");
        }
        
        statusEffectsText.text = effectsText.ToString().TrimEnd();
        LogDebug($"Updated status effects display: {effectsShown} effects shown");
    }
    
    /// <summary>
    /// Formats a single effect for display
    /// </summary>
    private string FormatEffectText(EffectHandler.StatusEffect effect)
    {
        if (effect == null) return "";
        
        // Format based on effect type mechanics (matching EffectHandler.GetActiveEffects logic)
        string effectDisplay;
        
        switch (effect.EffectName)
        {
            case "Strength":
            case "Curse":
            case "Burn":
            case "Salve":
                // Always show potency for damage modifiers and DoT/HoT effects
                effectDisplay = string.Format(effectWithPotencyFormat, effect.EffectName, effect.Potency);
                break;
                
            case "Break":
            case "Weak":
                // Show duration only (they don't have meaningful potency)
                if (effect.RemainingDuration > 1)
                {
                    effectDisplay = string.Format(effectWithDurationFormat, effect.EffectName, effect.RemainingDuration);
                }
                else
                {
                    effectDisplay = string.Format(effectFormat, effect.EffectName);
                }
                break;
                
            case "Thorns":
                // Always show potency for Thorns since the amount is important
                effectDisplay = string.Format(effectWithPotencyFormat, effect.EffectName, effect.Potency);
                break;
                
            case "Shield":
                // Always show potency for Shield since the amount is important
                effectDisplay = string.Format(effectWithPotencyFormat, effect.EffectName, effect.Potency);
                break;
                
            default:
                // For other effects, use the display settings
                if (showEffectPotency && showEffectDuration)
                {
                    effectDisplay = string.Format(effectFullFormat, effect.EffectName, effect.Potency, effect.RemainingDuration);
                }
                else if (showEffectPotency && effect.Potency > 1)
                {
                    effectDisplay = string.Format(effectWithPotencyFormat, effect.EffectName, effect.Potency);
                }
                else if (showEffectDuration && effect.RemainingDuration > 1)
                {
                    effectDisplay = string.Format(effectWithDurationFormat, effect.EffectName, effect.RemainingDuration);
                }
                else
                {
                    effectDisplay = string.Format(effectFormat, effect.EffectName);
                }
                break;
        }
        
        return effectDisplay;
    }
    
    /// <summary>
    /// Clears the status effects display
    /// </summary>
    private void ClearStatusEffectsDisplay()
    {
        if (statusEffectsText != null)
        {
            statusEffectsText.text = noEffectsText;
        }
    }
    
    /// <summary>
    /// Shows or hides the status effects container
    /// </summary>
    private void SetContainerVisible(bool visible)
    {
        if (statusEffectsContainer != null)
        {
            statusEffectsContainer.SetActive(visible);
        }
    }
    
    /// <summary>
    /// Subscribes to the current effect handler's events
    /// </summary>
    private void SubscribeToEffectHandler()
    {
        if (currentEffectHandler != null)
        {
            currentEffectHandler.OnEffectsChanged += OnEffectsChanged;
            LogDebug("Subscribed to EffectHandler events");
        }
        
        if (currentEntityTracker != null)
        {
            currentEntityTracker.OnComboChanged += OnComboChanged;
            LogDebug("Subscribed to EntityTracker combo events");
        }
    }
    
    /// <summary>
    /// Unsubscribes from the current effect handler's events
    /// </summary>
    private void UnsubscribeFromEffectHandler()
    {
        if (currentEffectHandler != null)
        {
            currentEffectHandler.OnEffectsChanged -= OnEffectsChanged;
            LogDebug("Unsubscribed from EffectHandler events");
        }
        
        if (currentEntityTracker != null)
        {
            currentEntityTracker.OnComboChanged -= OnComboChanged;
            LogDebug("Unsubscribed from EntityTracker combo events");
        }
    }
    
    /// <summary>
    /// Called when the pet's effects change
    /// </summary>
    private void OnEffectsChanged()
    {
        UpdateStatusEffectsDisplay();
        LogDebug("Effects changed - updated display");
    }
    
    /// <summary>
    /// Called when the pet's combo count changes
    /// </summary>
    private void OnComboChanged(int newComboCount)
    {
        UpdateStatusEffectsDisplay();
        LogDebug($"Combo changed to {newComboCount} - updated display");
    }
    
    /// <summary>
    /// Sets the maximum number of effects to display
    /// </summary>
    public void SetMaxEffectsToShow(int maxEffects)
    {
        maxEffectsToShow = Mathf.Max(1, maxEffects);
        UpdateStatusEffectsDisplay();
    }
    
    /// <summary>
    /// Sets whether to show effect potency values
    /// </summary>
    public void SetShowEffectPotency(bool show)
    {
        showEffectPotency = show;
        UpdateStatusEffectsDisplay();
    }
    
    /// <summary>
    /// Sets whether to show effect duration
    /// </summary>
    public void SetShowEffectDuration(bool show)
    {
        showEffectDuration = show;
        UpdateStatusEffectsDisplay();
    }
    
    /// <summary>
    /// Sets the text to show when there are no active effects
    /// </summary>
    public void SetNoEffectsText(string text)
    {
        noEffectsText = text;
        if (currentEffectHandler == null || currentEffectHandler.GetAllEffects().Count == 0)
        {
            UpdateStatusEffectsDisplay();
        }
    }
    
    private void OnDestroy()
    {
        UnsubscribeFromEffectHandler();
    }
    
    private void LogDebug(string message)
    {
        if (debugLogEnabled)
        {
            Debug.Log($"[OwnPetStatusEffectsDisplay] {message}");
        }
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[OwnPetStatusEffectsDisplay] {message}");
    }
} 