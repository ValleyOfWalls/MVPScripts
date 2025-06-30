using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Direction that the bar fills/empties
/// </summary>
public enum BarDirection
{
    LeftToRight,    // Bar fills from left to right (shrinks from right when losing value)
    RightToLeft     // Bar fills from right to left (shrinks from left when losing value)
}

/// <summary>
/// Controls health and energy bars using a mask-based system that supports both left-to-right and right-to-left directions.
/// Attach to: The parent GameObject containing the health/energy bar UI elements.
/// </summary>
public class HealthEnergyBarController : MonoBehaviour
{
    [Header("Health Bar Configuration")]
    [SerializeField] private RectTransform healthBarFill;
    [SerializeField] private Image healthBarFillImage;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private float healthBarWidth = 200f; // Full width of the health bar
    [SerializeField] private BarDirection healthBarDirection = BarDirection.LeftToRight;
    
    [Header("Energy Bar Configuration")]
    [SerializeField] private RectTransform energyBarFill;
    [SerializeField] private Image energyBarFillImage;
    [SerializeField] private TextMeshProUGUI energyText;
    [SerializeField] private float energyBarWidth = 200f; // Full width of the energy bar
    [SerializeField] private BarDirection energyBarDirection = BarDirection.LeftToRight;
    
    [Header("Animation Settings")]
    [SerializeField] private bool useAnimation = true;
    [SerializeField] private float animationSpeed = 2f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Color Settings")]
    [SerializeField] private Color healthBarColor = Color.red;
    [SerializeField] private Color energyBarColor = Color.blue;
    [SerializeField] private Color lowHealthColor = new Color(1f, 0.5f, 0f); // Orange
    [SerializeField] private Color criticalHealthColor = new Color(0.5f, 0f, 0f); // Dark red
    [SerializeField] private float lowHealthThreshold = 0.3f;
    [SerializeField] private float criticalHealthThreshold = 0.15f;
    
    [Header("Debug")]
    [SerializeField] private bool debugEnabled = false;
    
    // Current values for tracking changes
    private int currentHealth;
    private int maxHealth;
    private int currentEnergy;
    private int maxEnergy;
    
    // Animation tracking
    private float healthTargetWidth;
    private float energyTargetWidth;
    private float healthCurrentWidth;
    private float energyCurrentWidth;
    private bool isAnimatingHealth = false;
    private bool isAnimatingEnergy = false;
    
    private void Awake()
    {
        ValidateComponents();
        SetupInitialColors();
    }
    
    private void Start()
    {
        // Initialize bar widths
        if (healthBarFill != null)
        {
            healthBarWidth = healthBarFill.rect.width;
            healthCurrentWidth = healthBarWidth;
            healthTargetWidth = healthBarWidth;
        }
        
        if (energyBarFill != null)
        {
            energyBarWidth = energyBarFill.rect.width;
            energyCurrentWidth = energyBarWidth;
            energyTargetWidth = energyBarWidth;
        }
    }
    
    private void Update()
    {
        if (useAnimation)
        {
            AnimateHealthBar();
            AnimateEnergyBar();
        }
    }
    
    private void ValidateComponents()
    {
        // Auto-find components if not assigned
        if (healthBarFill == null)
        {
            Transform healthFillTransform = transform.Find("HealthBarContainer/HealthBarForeground/HealthBarFill");
            if (healthFillTransform != null)
            {
                healthBarFill = healthFillTransform.GetComponent<RectTransform>();
                healthBarFillImage = healthFillTransform.GetComponent<Image>();
            }
        }
        
        if (energyBarFill == null)
        {
            Transform energyFillTransform = transform.Find("EnergyBarContainer/EnergyBarForeground/EnergyBarFill");
            if (energyFillTransform != null)
            {
                energyBarFill = energyFillTransform.GetComponent<RectTransform>();
                energyBarFillImage = energyFillTransform.GetComponent<Image>();
            }
        }
        
        if (healthText == null)
        {
            healthText = transform.Find("HealthText")?.GetComponent<TextMeshProUGUI>();
        }
        
        if (energyText == null)
        {
            energyText = transform.Find("EnergyText")?.GetComponent<TextMeshProUGUI>();
        }
        
        // Log warnings for missing critical components
        if (healthBarFill == null) LogWarning("Health bar fill RectTransform not found!");
        if (energyBarFill == null) LogWarning("Energy bar fill RectTransform not found!");
    }
    
    private void SetupInitialColors()
    {
        if (healthBarFillImage != null)
            healthBarFillImage.color = healthBarColor;
        if (energyBarFillImage != null)
            energyBarFillImage.color = energyBarColor;
    }
    
    /// <summary>
    /// Updates the health bar display
    /// </summary>
    /// <param name="health">Current health value</param>
    /// <param name="maxHealthValue">Maximum health value</param>
    public void UpdateHealthBar(int health, int maxHealthValue)
    {
        currentHealth = health;
        maxHealth = maxHealthValue;
        
        // Update health text immediately
        if (healthText != null)
        {
            healthText.text = $"{health}/{maxHealthValue}";
        }
        
        // Calculate target width based on health percentage
        float healthPercentage = maxHealthValue > 0 ? (float)health / maxHealthValue : 0f;
        float newTargetWidth = healthBarWidth * healthPercentage;
        
        // Update color based on health percentage
        UpdateHealthBarColor(healthPercentage);
        
        // Always update target, even during animation
        healthTargetWidth = newTargetWidth;
        
        if (useAnimation)
        {
            isAnimatingHealth = true;
        }
        else
        {
            // Immediate update
            healthCurrentWidth = healthTargetWidth;
            UpdateHealthBarWidth(healthTargetWidth);
        }
        
        LogDebug($"Health updated: {health}/{maxHealthValue} ({healthPercentage:P1}) - Target width: {healthTargetWidth}");
    }
    
    /// <summary>
    /// Updates the energy bar display
    /// </summary>
    /// <param name="energy">Current energy value</param>
    /// <param name="maxEnergyValue">Maximum energy value</param>
    public void UpdateEnergyBar(int energy, int maxEnergyValue)
    {
        currentEnergy = energy;
        maxEnergy = maxEnergyValue;
        
        // Update energy text immediately
        if (energyText != null)
        {
            energyText.text = $"{energy}/{maxEnergyValue}";
        }
        
        // Calculate target width based on energy percentage
        float energyPercentage = maxEnergyValue > 0 ? (float)energy / maxEnergyValue : 0f;
        float newTargetWidth = energyBarWidth * energyPercentage;
        
        // Always update target, even during animation
        energyTargetWidth = newTargetWidth;
        
        if (useAnimation)
        {
            isAnimatingEnergy = true;
        }
        else
        {
            // Immediate update
            energyCurrentWidth = energyTargetWidth;
            UpdateEnergyBarWidth(energyTargetWidth);
        }
        
        LogDebug($"Energy updated: {energy}/{maxEnergyValue} ({energyPercentage:P1}) - Target width: {energyTargetWidth}");
    }
    
    /// <summary>
    /// Updates health bar color based on current health percentage
    /// </summary>
    private void UpdateHealthBarColor(float healthPercentage)
    {
        if (healthBarFillImage == null) return;
        
        Color targetColor;
        if (healthPercentage <= criticalHealthThreshold)
        {
            targetColor = criticalHealthColor;
        }
        else if (healthPercentage <= lowHealthThreshold)
        {
            // Lerp between normal and low health color
            float t = (healthPercentage - criticalHealthThreshold) / (lowHealthThreshold - criticalHealthThreshold);
            targetColor = Color.Lerp(criticalHealthColor, lowHealthColor, t);
        }
        else
        {
            // Lerp between low health and normal color
            float t = (healthPercentage - lowHealthThreshold) / (1f - lowHealthThreshold);
            targetColor = Color.Lerp(lowHealthColor, healthBarColor, t);
        }
        
        healthBarFillImage.color = targetColor;
    }
    
    /// <summary>
    /// Animates the health bar to the target width
    /// </summary>
    private void AnimateHealthBar()
    {
        if (!isAnimatingHealth || healthBarFill == null) return;
        
        float difference = Mathf.Abs(healthCurrentWidth - healthTargetWidth);
        if (difference < 0.1f)
        {
            // Animation complete
            healthCurrentWidth = healthTargetWidth;
            UpdateHealthBarWidth(healthCurrentWidth);
            isAnimatingHealth = false;
            return;
        }
        
        // Animate towards target
        float speed = animationSpeed * Time.deltaTime * healthBarWidth;
        float t = speed / difference;
        t = animationCurve.Evaluate(Mathf.Clamp01(t));
        
        healthCurrentWidth = Mathf.Lerp(healthCurrentWidth, healthTargetWidth, t);
        UpdateHealthBarWidth(healthCurrentWidth);
    }
    
    /// <summary>
    /// Animates the energy bar to the target width
    /// </summary>
    private void AnimateEnergyBar()
    {
        if (!isAnimatingEnergy || energyBarFill == null) return;
        
        float difference = Mathf.Abs(energyCurrentWidth - energyTargetWidth);
        if (difference < 0.1f)
        {
            // Animation complete
            energyCurrentWidth = energyTargetWidth;
            UpdateEnergyBarWidth(energyCurrentWidth);
            isAnimatingEnergy = false;
            return;
        }
        
        // Animate towards target
        float speed = animationSpeed * Time.deltaTime * energyBarWidth;
        float t = speed / difference;
        t = animationCurve.Evaluate(Mathf.Clamp01(t));
        
        energyCurrentWidth = Mathf.Lerp(energyCurrentWidth, energyTargetWidth, t);
        UpdateEnergyBarWidth(energyCurrentWidth);
    }
    
    /// <summary>
    /// Updates the visual width of the health bar fill image
    /// </summary>
    private void UpdateHealthBarWidth(float width)
    {
        if (healthBarFill == null) return;
        
        // Update the width of the fill image
        Vector2 sizeDelta = healthBarFill.sizeDelta;
        sizeDelta.x = width;
        healthBarFill.sizeDelta = sizeDelta;
        
        // Set anchor position based on bar direction
        Vector2 anchoredPosition = healthBarFill.anchoredPosition;
        if (healthBarDirection == BarDirection.LeftToRight)
        {
            // Anchor to left so it shrinks from the right
            anchoredPosition.x = 0;
        }
        else // RightToLeft
        {
            // Anchor to right so it shrinks from the left
            anchoredPosition.x = healthBarWidth - width;
        }
        healthBarFill.anchoredPosition = anchoredPosition;
    }
    
    /// <summary>
    /// Updates the visual width of the energy bar fill image
    /// </summary>
    private void UpdateEnergyBarWidth(float width)
    {
        if (energyBarFill == null) return;
        
        // Update the width of the fill image
        Vector2 sizeDelta = energyBarFill.sizeDelta;
        sizeDelta.x = width;
        energyBarFill.sizeDelta = sizeDelta;
        
        // Set anchor position based on bar direction
        Vector2 anchoredPosition = energyBarFill.anchoredPosition;
        if (energyBarDirection == BarDirection.LeftToRight)
        {
            // Anchor to left so it shrinks from the right
            anchoredPosition.x = 0;
        }
        else // RightToLeft
        {
            // Anchor to right so it shrinks from the left
            anchoredPosition.x = energyBarWidth - width;
        }
        energyBarFill.anchoredPosition = anchoredPosition;
    }
    
    /// <summary>
    /// Sets custom colors for the bars
    /// </summary>
    public void SetHealthBarColor(Color color)
    {
        healthBarColor = color;
        if (healthBarFillImage != null)
            healthBarFillImage.color = color;
    }
    
    public void SetEnergyBarColor(Color color)
    {
        energyBarColor = color;
        if (energyBarFillImage != null)
            energyBarFillImage.color = color;
    }
    
    /// <summary>
    /// Sets the direction for the health bar
    /// </summary>
    public void SetHealthBarDirection(BarDirection direction)
    {
        healthBarDirection = direction;
        // Force update to apply new direction
        UpdateHealthBarWidth(healthCurrentWidth);
    }
    
    /// <summary>
    /// Sets the direction for the energy bar
    /// </summary>
    public void SetEnergyBarDirection(BarDirection direction)
    {
        energyBarDirection = direction;
        // Force update to apply new direction
        UpdateEnergyBarWidth(energyCurrentWidth);
    }
    
    /// <summary>
    /// Gets the current health bar direction
    /// </summary>
    public BarDirection GetHealthBarDirection()
    {
        return healthBarDirection;
    }
    
    /// <summary>
    /// Gets the current energy bar direction
    /// </summary>
    public BarDirection GetEnergyBarDirection()
    {
        return energyBarDirection;
    }
    
    /// <summary>
    /// Gets current health values
    /// </summary>
    public (int current, int max) GetHealthValues()
    {
        return (currentHealth, maxHealth);
    }
    
    /// <summary>
    /// Gets current energy values
    /// </summary>
    public (int current, int max) GetEnergyValues()
    {
        return (currentEnergy, maxEnergy);
    }
    
    /// <summary>
    /// Forces immediate update without animation
    /// </summary>
    public void ForceImmediateUpdate()
    {
        bool wasAnimating = useAnimation;
        useAnimation = false;
        
        UpdateHealthBar(currentHealth, maxHealth);
        UpdateEnergyBar(currentEnergy, maxEnergy);
        
        useAnimation = wasAnimating;
    }
    
    private void LogDebug(string message)
    {
        if (debugEnabled)
        {
            Debug.Log($"[HealthEnergyBarController] {message}");
        }
    }
    
    private void LogWarning(string message)
    {
        Debug.LogWarning($"[HealthEnergyBarController] {message}");
    }
} 