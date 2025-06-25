using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles the display of pet health and energy stats.
/// Attach to: OwnPetViewPrefab GameObject
/// </summary>
public class OwnPetStatsDisplay : MonoBehaviour, IUpdatablePetDisplay
{
    [Header("Health UI")]
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Image healthBar; // Legacy fallback
    [SerializeField] private Image healthBarBackground; // Legacy fallback
    
    [Header("Energy UI")]
    [SerializeField] private TextMeshProUGUI energyText;
    [SerializeField] private Image energyBar; // Legacy fallback
    [SerializeField] private Image energyBarBackground; // Legacy fallback
    
    [Header("Enhanced Bar Controller")]
    [SerializeField] private HealthEnergyBarController barController;
    
    [Header("Visual Settings")]
    [SerializeField] private Color healthBarColor = Color.red;
    [SerializeField] private Color energyBarColor = Color.blue;
    [SerializeField] private Color barBackgroundColor = Color.gray;
    
    [Header("Debug")]
    [SerializeField] private bool debugLogEnabled = true;
    
    // Current pet being displayed
    private NetworkEntity currentPet;
    
    private void Awake()
    {
        ValidateComponents();
        SetupBarColors();
        
        // Get bar controller if not assigned
        if (barController == null)
            barController = GetComponent<HealthEnergyBarController>();
    }
    
    private void ValidateComponents()
    {
        // Try to find components if not assigned
        if (healthText == null)
        {
            healthText = transform.Find("HealthText")?.GetComponent<TextMeshProUGUI>();
        }
        
        if (energyText == null)
        {
            energyText = transform.Find("EnergyText")?.GetComponent<TextMeshProUGUI>();
        }
        
        if (healthBar == null)
        {
            healthBar = transform.Find("HealthBar")?.GetComponent<Image>();
        }
        
        if (energyBar == null)
        {
            energyBar = transform.Find("EnergyBar")?.GetComponent<Image>();
        }
        
        if (healthBarBackground == null)
        {
            healthBarBackground = transform.Find("HealthBarBackground")?.GetComponent<Image>();
        }
        
        if (energyBarBackground == null)
        {
            energyBarBackground = transform.Find("EnergyBarBackground")?.GetComponent<Image>();
        }
        
        // Log warnings for missing components
        if (healthText == null) LogError("Health Text component not found!");
        if (energyText == null) LogError("Energy Text component not found!");
        if (healthBar == null) LogError("Health Bar component not found!");
        if (energyBar == null) LogError("Energy Bar component not found!");
    }
    
    private void SetupBarColors()
    {
        if (healthBar != null)
            healthBar.color = healthBarColor;
        if (energyBar != null)
            energyBar.color = energyBarColor;
        if (healthBarBackground != null)
            healthBarBackground.color = barBackgroundColor;
        if (energyBarBackground != null)
            energyBarBackground.color = barBackgroundColor;
    }
    
    /// <summary>
    /// Sets the pet whose stats should be displayed
    /// </summary>
    /// <param name="pet">The NetworkEntity pet to display stats for</param>
    public void SetPet(NetworkEntity pet)
    {
        currentPet = pet;
        
        if (pet != null)
        {
            UpdateAllStats();
            LogDebug($"Pet stats display set to: {pet.EntityName.Value}");
        }
        else
        {
            ClearAllStats();
            LogDebug("Pet stats display cleared");
        }
    }
    
    /// <summary>
    /// Updates the health display (text and bar)
    /// </summary>
    public void UpdateHealthDisplay()
    {
        if (currentPet == null) return;
        
        int currentHealth = currentPet.CurrentHealth.Value;
        int maxHealth = currentPet.MaxHealth.Value;
        
        // Use enhanced bar controller if available, otherwise fallback to legacy
        if (barController != null)
        {
            barController.UpdateHealthBar(currentHealth, maxHealth);
        }
        else
        {
            // Legacy fallback - Update health text
            if (healthText != null)
            {
                healthText.text = $"{currentHealth}/{maxHealth}";
            }
            
            // Legacy fallback - Update health bar
            if (healthBar != null)
            {
                float healthPercentage = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
                healthBar.fillAmount = healthPercentage;
            }
        }
        
        LogDebug($"Health updated: {currentHealth}/{maxHealth}");
    }
    
    /// <summary>
    /// Updates the energy display (text and bar)
    /// </summary>
    public void UpdateEnergyDisplay()
    {
        if (currentPet == null) return;
        
        int currentEnergy = currentPet.CurrentEnergy.Value;
        int maxEnergy = currentPet.MaxEnergy.Value;
        
        // Use enhanced bar controller if available, otherwise fallback to legacy
        if (barController != null)
        {
            barController.UpdateEnergyBar(currentEnergy, maxEnergy);
        }
        else
        {
            // Legacy fallback - Update energy text
            if (energyText != null)
            {
                energyText.text = $"{currentEnergy}/{maxEnergy}";
            }
            
            // Legacy fallback - Update energy bar
            if (energyBar != null)
            {
                float energyPercentage = maxEnergy > 0 ? (float)currentEnergy / maxEnergy : 0f;
                energyBar.fillAmount = energyPercentage;
            }
        }
        
        LogDebug($"Energy updated: {currentEnergy}/{maxEnergy}");
    }
    
    /// <summary>
    /// Updates both health and energy displays
    /// </summary>
    public void UpdateAllStats()
    {
        UpdateHealthDisplay();
        UpdateEnergyDisplay();
    }
    
    /// <summary>
    /// Updates the display to reflect current pet data (implements IUpdatablePetDisplay)
    /// </summary>
    public void UpdateDisplay()
    {
        UpdateAllStats();
    }
    
    /// <summary>
    /// Clears all stat displays
    /// </summary>
    private void ClearAllStats()
    {
        // Clear health display
        if (healthText != null)
            healthText.text = "0/0";
        if (healthBar != null)
            healthBar.fillAmount = 0f;
        
        // Clear energy display
        if (energyText != null)
            energyText.text = "0/0";
        if (energyBar != null)
            energyBar.fillAmount = 0f;
    }
    
    /// <summary>
    /// Gets the current pet being displayed
    /// </summary>
    public NetworkEntity GetCurrentPet()
    {
        return currentPet;
    }
    
    /// <summary>
    /// Sets the color of the health bar
    /// </summary>
    /// <param name="color">The color to use for the health bar</param>
    public void SetHealthBarColor(Color color)
    {
        healthBarColor = color;
        if (healthBar != null)
            healthBar.color = color;
    }
    
    /// <summary>
    /// Sets the color of the energy bar
    /// </summary>
    /// <param name="color">The color to use for the energy bar</param>
    public void SetEnergyBarColor(Color color)
    {
        energyBarColor = color;
        if (energyBar != null)
            energyBar.color = color;
    }
    
    /// <summary>
    /// Sets the color of both bar backgrounds
    /// </summary>
    /// <param name="color">The color to use for the bar backgrounds</param>
    public void SetBarBackgroundColor(Color color)
    {
        barBackgroundColor = color;
        if (healthBarBackground != null)
            healthBarBackground.color = color;
        if (energyBarBackground != null)
            energyBarBackground.color = color;
    }
    
    private void LogDebug(string message)
    {
        if (debugLogEnabled)
        {
            Debug.Log($"[OwnPetStatsDisplay] {message}");
        }
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[OwnPetStatsDisplay] {message}");
    }
} 