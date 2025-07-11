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
    
    [Header("Portrait Display")]
    [SerializeField] private Image petPortraitImage;
    
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
    
    private void OnDestroy()
    {
        // Clean up subscriptions
        if (currentPet != null)
        {
            UnsubscribeFromPetUpdates(currentPet);
        }
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
        
        // Try to find portrait image if not assigned
        if (petPortraitImage == null)
        {
            petPortraitImage = transform.Find("PetPortraitImage")?.GetComponent<Image>();
        }
        
        // Log warnings for missing components
        if (healthText == null) LogError("Health Text component not found!");
        if (energyText == null) LogError("Energy Text component not found!");
        if (healthBar == null) LogError("Health Bar component not found!");
        if (energyBar == null) LogError("Energy Bar component not found!");
        if (petPortraitImage == null) LogError("Pet Portrait Image component not found!");
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
        // Unsubscribe from previous pet if any
        if (currentPet != null)
        {
            UnsubscribeFromPetUpdates(currentPet);
        }
        
        currentPet = pet;
        
        if (pet != null)
        {
            UpdateAllStats();
            SubscribeToPetUpdates(pet);
            LogDebug($"ALLY_SETUP: Pet stats display set to: {pet.EntityName.Value} - subscribed to network updates");
        }
        else
        {
            ClearAllStats();
            LogDebug("ALLY_SETUP: Pet stats display cleared");
        }
    }
    
    /// <summary>
    /// Subscribes to network updates from the pet entity
    /// </summary>
    private void SubscribeToPetUpdates(NetworkEntity pet)
    {
        if (pet == null) return;
        
        // Subscribe to health changes
        pet.CurrentHealth.OnChange += OnHealthChanged;
        pet.MaxHealth.OnChange += OnHealthChanged;
        
        // Subscribe to energy changes
        pet.CurrentEnergy.OnChange += OnEnergyChanged;
        pet.MaxEnergy.OnChange += OnEnergyChanged;
        
        // Subscribe to name changes
        pet.EntityName.OnChange += OnNameChanged;
        
        LogDebug($"ALLY_SETUP: Subscribed to network updates for {pet.EntityName.Value}");
    }
    
    /// <summary>
    /// Unsubscribes from network updates from the pet entity
    /// </summary>
    private void UnsubscribeFromPetUpdates(NetworkEntity pet)
    {
        if (pet == null) return;
        
        // Unsubscribe from health changes
        pet.CurrentHealth.OnChange -= OnHealthChanged;
        pet.MaxHealth.OnChange -= OnHealthChanged;
        
        // Unsubscribe from energy changes
        pet.CurrentEnergy.OnChange -= OnEnergyChanged;
        pet.MaxEnergy.OnChange -= OnEnergyChanged;
        
        // Unsubscribe from name changes
        pet.EntityName.OnChange -= OnNameChanged;
        
        LogDebug($"ALLY_SETUP: Unsubscribed from network updates for {pet.EntityName.Value}");
    }
    
    /// <summary>
    /// Called when pet health changes over the network
    /// </summary>
    private void OnHealthChanged(int prev, int next, bool asServer)
    {
        LogDebug($"ALLY_SETUP: Health changed from {prev} to {next} (asServer: {asServer})");
        UpdateHealthDisplay();
    }
    
    /// <summary>
    /// Called when pet energy changes over the network
    /// </summary>
    private void OnEnergyChanged(int prev, int next, bool asServer)
    {
        LogDebug($"ALLY_SETUP: Energy changed from {prev} to {next} (asServer: {asServer})");
        UpdateEnergyDisplay();
    }
    
    /// <summary>
    /// Called when pet name changes over the network
    /// </summary>
    private void OnNameChanged(string prev, string next, bool asServer)
    {
        LogDebug($"ALLY_SETUP: Name changed from {prev} to {next} (asServer: {asServer})");
        UpdatePortraitDisplay(); // Name change might affect portrait lookup
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
        UpdatePortraitDisplay();
    }
    
    /// <summary>
    /// Updates the pet portrait display
    /// </summary>
    public void UpdatePortraitDisplay()
    {
        if (currentPet == null || petPortraitImage == null) return;
        
        Sprite petPortrait = GetEntityPortrait(currentPet);
        if (petPortrait != null)
        {
            petPortraitImage.sprite = petPortrait;
            petPortraitImage.gameObject.SetActive(true);
            LogDebug($"Portrait updated for: {currentPet.EntityName.Value}");
        }
        else
        {
            // Hide portrait if none available
            petPortraitImage.gameObject.SetActive(false);
            LogDebug($"No portrait available for: {currentPet.EntityName.Value}");
        }
    }
    
    /// <summary>
    /// Gets the portrait sprite for the given entity (supports both Pet and Player entities)
    /// Uses the same approach as EntitySelectionController and existing UI managers
    /// </summary>
    private Sprite GetEntityPortrait(NetworkEntity entity)
    {
        if (entity == null) 
            return null;
        
        LogDebug($"PORTRAIT_DEBUG: Looking for portrait for {entity.EntityName.Value} (ID: {entity.ObjectId}, Type: {entity.EntityType})");
        
        // Method 1: Try to get from selection data (works for both Pet and Player entities)
        Sprite portraitFromData = GetPortraitFromSelectionData(entity);
        if (portraitFromData != null)
        {
            LogDebug($"PORTRAIT_DEBUG: Found portrait from selection data for {entity.EntityName.Value}");
            return portraitFromData;
        }
        
        // Method 2: Try to get from SpriteRenderer (legacy approach)
        SpriteRenderer spriteRenderer = entity.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            LogDebug($"PORTRAIT_DEBUG: Found portrait from SpriteRenderer for {entity.EntityName.Value}");
            return spriteRenderer.sprite;
        }
        
        // Method 3: Try to get from Image component (UI approach)
        Image imageComponent = entity.GetComponentInChildren<Image>();
        if (imageComponent != null && imageComponent.sprite != null)
        {
            LogDebug($"PORTRAIT_DEBUG: Found portrait from Image component for {entity.EntityName.Value}");
            return imageComponent.sprite;
        }
        
        LogDebug($"PORTRAIT_DEBUG: No portrait found for entity {entity.EntityName.Value}");
        return null;
    }
    
    /// <summary>
    /// Gets the portrait sprite from selection data (supports both Pet and Player entities)
    /// </summary>
    private Sprite GetPortraitFromSelectionData(NetworkEntity entity)
    {
        if (entity == null) return null;
        
        LogDebug($"PORTRAIT_DEBUG: Getting portrait from selection data for {entity.EntityName.Value} (Type: {entity.EntityType})");
        
        // Method 1: Try to get from CharacterSelectionManager (works for local player entities)
        CharacterSelectionManager selectionManager = FindFirstObjectByType<CharacterSelectionManager>();
        if (selectionManager != null)
        {
            Sprite portraitFromSelection = TryGetPortraitFromSelectionManager(entity, selectionManager);
            if (portraitFromSelection != null)
            {
                return portraitFromSelection;
            }
        }
        else
        {
            LogDebug("PORTRAIT_DEBUG: CharacterSelectionManager not found");
        }
        
        // Method 2: Try to find portrait by matching entity name with available data assets
        Sprite portraitFromAssets = TryGetPortraitFromDataAssets(entity);
        if (portraitFromAssets != null)
        {
            return portraitFromAssets;
        }
        
        LogDebug($"PORTRAIT_DEBUG: No selection data portrait found for entity {entity.EntityName.Value}");
        return null;
    }
    
    /// <summary>
    /// Tries to get portrait from CharacterSelectionManager (works for local player entities)
    /// </summary>
    private Sprite TryGetPortraitFromSelectionManager(NetworkEntity entity, CharacterSelectionManager selectionManager)
    {
        if (entity.EntityType == EntityType.Pet)
        {
            // Handle Pet entities
            int petIndex = entity.SelectedPetIndex.Value;
            LogDebug($"PORTRAIT_DEBUG: Pet {entity.EntityName.Value} has SelectedPetIndex: {petIndex}");
            
            if (petIndex < 0)
            {
                LogDebug($"PORTRAIT_DEBUG: Pet {entity.EntityName.Value} has invalid selection index ({petIndex})");
                return null;
            }
            
            // Get available pets and find the one at the selected index
            var availablePets = selectionManager.GetAvailablePets();
            LogDebug($"PORTRAIT_DEBUG: CharacterSelectionManager has {availablePets?.Count ?? 0} available pets");
            
            if (availablePets == null || availablePets.Count == 0)
            {
                LogDebug($"PORTRAIT_DEBUG: No available pets in CharacterSelectionManager");
                return null;
            }
            
            if (petIndex >= 0 && petIndex < availablePets.Count)
            {
                PetData petData = availablePets[petIndex];
                LogDebug($"PORTRAIT_DEBUG: Found PetData at index {petIndex}: {petData?.PetName ?? "null"}");
                
                if (petData != null && petData.PetPortrait != null)
                {
                    LogDebug($"PORTRAIT_DEBUG: Found PetData portrait from selection manager for {entity.EntityName.Value} at index {petIndex} - Portrait: {petData.PetPortrait.name}");
                    return petData.PetPortrait;
                }
                else
                {
                    LogDebug($"PORTRAIT_DEBUG: PetData at index {petIndex} is null or has no portrait - PetData: {petData?.PetName ?? "null"}, Portrait: {petData?.PetPortrait?.name ?? "null"}");
                }
            }
            else
            {
                LogDebug($"PORTRAIT_DEBUG: Pet index {petIndex} is out of range for available pets (count: {availablePets.Count})");
            }
        }
        else if (entity.EntityType == EntityType.Player)
        {
            // Handle Player entities (they might have character portraits)
            int characterIndex = entity.SelectedCharacterIndex.Value;
            LogDebug($"PORTRAIT_DEBUG: Player {entity.EntityName.Value} has SelectedCharacterIndex: {characterIndex}");
            
            if (characterIndex < 0)
            {
                LogDebug($"PORTRAIT_DEBUG: Player {entity.EntityName.Value} has invalid character selection index ({characterIndex})");
                return null;
            }
            
            // Get available characters and find the one at the selected index
            var availableCharacters = selectionManager.GetAvailableCharacters();
            LogDebug($"PORTRAIT_DEBUG: CharacterSelectionManager has {availableCharacters?.Count ?? 0} available characters");
            
            if (availableCharacters == null || availableCharacters.Count == 0)
            {
                LogDebug($"PORTRAIT_DEBUG: No available characters in CharacterSelectionManager");
                return null;
            }
            
            if (characterIndex >= 0 && characterIndex < availableCharacters.Count)
            {
                CharacterData characterData = availableCharacters[characterIndex];
                LogDebug($"PORTRAIT_DEBUG: Found CharacterData at index {characterIndex}: {characterData?.CharacterName ?? "null"}");
                
                if (characterData != null && characterData.CharacterPortrait != null)
                {
                    LogDebug($"PORTRAIT_DEBUG: Found CharacterData portrait from selection manager for {entity.EntityName.Value} at index {characterIndex} - Portrait: {characterData.CharacterPortrait.name}");
                    return characterData.CharacterPortrait;
                }
                else
                {
                    LogDebug($"PORTRAIT_DEBUG: CharacterData at index {characterIndex} is null or has no portrait - CharacterData: {characterData?.CharacterName ?? "null"}, Portrait: {characterData?.CharacterPortrait?.name ?? "null"}");
                }
            }
            else
            {
                LogDebug($"PORTRAIT_DEBUG: Character index {characterIndex} is out of range for available characters (count: {availableCharacters.Count})");
            }
        }
        else
        {
            LogDebug($"PORTRAIT_DEBUG: Entity type {entity.EntityType} not supported for selection manager lookup");
        }
        
        return null;
    }
    
    /// <summary>
    /// Tries to get portrait by searching all data assets in the scene and matching by name
    /// This works for opponent entities where we don't have selection manager data
    /// </summary>
    private Sprite TryGetPortraitFromDataAssets(NetworkEntity entity)
    {
        LogDebug($"PORTRAIT_DEBUG: Searching for data assets matching name: {entity.EntityName.Value} (Type: {entity.EntityType})");
        
        if (entity.EntityType == EntityType.Pet)
        {
            // Search PetData assets
            PetData[] allPetDataAssets = FindObjectsByType<PetData>(FindObjectsSortMode.None);
            LogDebug($"PORTRAIT_DEBUG: Found {allPetDataAssets.Length} PetData assets in scene");
            
            foreach (PetData petData in allPetDataAssets)
            {
                if (petData != null && petData.PetName == entity.EntityName.Value)
                {
                    LogDebug($"PORTRAIT_DEBUG: Found matching PetData asset: {petData.PetName}");
                    
                    if (petData.PetPortrait != null)
                    {
                        LogDebug($"PORTRAIT_DEBUG: Found portrait from PetData asset for {entity.EntityName.Value} - Portrait: {petData.PetPortrait.name}");
                        return petData.PetPortrait;
                    }
                    else
                    {
                        LogDebug($"PORTRAIT_DEBUG: Matching PetData asset has no portrait: {petData.PetName}");
                    }
                }
            }
            
            LogDebug($"PORTRAIT_DEBUG: No matching PetData asset found for {entity.EntityName.Value}");
        }
        else if (entity.EntityType == EntityType.Player)
        {
            // Search CharacterData assets
            CharacterData[] allCharacterDataAssets = FindObjectsByType<CharacterData>(FindObjectsSortMode.None);
            LogDebug($"PORTRAIT_DEBUG: Found {allCharacterDataAssets.Length} CharacterData assets in scene");
            
            foreach (CharacterData characterData in allCharacterDataAssets)
            {
                if (characterData != null && characterData.CharacterName == entity.EntityName.Value)
                {
                    LogDebug($"PORTRAIT_DEBUG: Found matching CharacterData asset: {characterData.CharacterName}");
                    
                    if (characterData.CharacterPortrait != null)
                    {
                        LogDebug($"PORTRAIT_DEBUG: Found portrait from CharacterData asset for {entity.EntityName.Value} - Portrait: {characterData.CharacterPortrait.name}");
                        return characterData.CharacterPortrait;
                    }
                    else
                    {
                        LogDebug($"PORTRAIT_DEBUG: Matching CharacterData asset has no portrait: {characterData.CharacterName}");
                    }
                }
            }
            
            LogDebug($"PORTRAIT_DEBUG: No matching CharacterData asset found for {entity.EntityName.Value}");
        }
        else
        {
            LogDebug($"PORTRAIT_DEBUG: Entity type {entity.EntityType} not supported for asset search");
        }
        
        return null;
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
        
        // Clear portrait display
        if (petPortraitImage != null)
            petPortraitImage.gameObject.SetActive(false);
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