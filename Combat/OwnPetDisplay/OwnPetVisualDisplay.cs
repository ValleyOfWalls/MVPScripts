using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles the visual representation of the pet (image and name).
/// Attach to: OwnPetViewPrefab GameObject
/// </summary>
public class OwnPetVisualDisplay : MonoBehaviour, IOwnPetDisplay
{
    [Header("Visual Components")]
    [SerializeField] private Image petImage;
    [SerializeField] private TextMeshProUGUI petNameText;
    
    [Header("Damage Preview UI")]
    [SerializeField] private TextMeshProUGUI damagePreviewText;
    [SerializeField] private GameObject damagePreviewContainer;
    
    [Header("Placeholder Settings")]
    [SerializeField] private Color placeholderColor = Color.white;
    
    [Header("Debug")]
    [SerializeField] private bool debugLogEnabled = true;
    
    // Current pet being displayed
    private NetworkEntity currentPet;
    
    private void Awake()
    {
        ValidateComponents();
        SetupPlaceholderImage();
        
        // Add DropZone component for card drag and drop targeting
        DropZone dropZone = GetComponent<DropZone>();
        if (dropZone == null)
        {
            dropZone = gameObject.AddComponent<DropZone>();
            LogDebug("Added DropZone component for card targeting");
        }
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
        if (petImage == null)
        {
            petImage = GetComponentInChildren<Image>();
            if (petImage == null)
                LogError("Pet Image component not found! Please assign it in the inspector.");
        }
        
        if (petNameText == null)
        {
            petNameText = GetComponentInChildren<TextMeshProUGUI>();
            if (petNameText == null)
                LogError("Pet Name Text component not found! Please assign it in the inspector.");
        }
    }
    
    /// <summary>
    /// Sets the pet to be displayed
    /// </summary>
    /// <param name="pet">The NetworkEntity pet to display</param>
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
            UpdatePetVisuals();
            SubscribeToPetUpdates(pet);
            LogDebug($"ALLY_SETUP: Pet visual display set to: {pet.EntityName.Value} - subscribed to network updates");
        }
        else
        {
            ClearPetVisuals();
            LogDebug("ALLY_SETUP: Pet visual display cleared");
        }
    }
    
    /// <summary>
    /// Subscribes to network updates from the pet entity
    /// </summary>
    private void SubscribeToPetUpdates(NetworkEntity pet)
    {
        if (pet == null) return;
        
        // Subscribe to name changes (affects display)
        pet.EntityName.OnChange += OnNameChanged;
        
        LogDebug($"ALLY_SETUP: Visual display subscribed to network updates for {pet.EntityName.Value}");
    }
    
    /// <summary>
    /// Unsubscribes from network updates from the pet entity
    /// </summary>
    private void UnsubscribeFromPetUpdates(NetworkEntity pet)
    {
        if (pet == null) return;
        
        // Unsubscribe from name changes
        pet.EntityName.OnChange -= OnNameChanged;
        
        LogDebug($"ALLY_SETUP: Visual display unsubscribed from network updates for {pet.EntityName.Value}");
    }
    
    /// <summary>
    /// Called when pet name changes over the network
    /// </summary>
    private void OnNameChanged(string prev, string next, bool asServer)
    {
        LogDebug($"ALLY_SETUP: Pet name changed from {prev} to {next} (asServer: {asServer})");
        UpdatePetName();
        UpdatePetImage(); // Name change might affect portrait lookup
    }
    
    /// <summary>
    /// Updates the pet name display (called when pet name changes)
    /// </summary>
    public void UpdatePetName()
    {
        if (currentPet != null && petNameText != null)
        {
            petNameText.text = currentPet.EntityName.Value;
            LogDebug($"Pet name updated to: {currentPet.EntityName.Value}");
        }
    }
    
    /// <summary>
    /// Updates all pet visual elements
    /// </summary>
    private void UpdatePetVisuals()
    {
        if (currentPet == null) return;
        
        // Update pet name
        if (petNameText != null)
        {
            petNameText.text = currentPet.EntityName.Value;
        }
        
        // Update pet image (placeholder for now)
        UpdatePetImage();
    }
    
    /// <summary>
    /// Updates the pet image (currently shows placeholder)
    /// </summary>
    private void UpdatePetImage()
    {
        if (petImage == null) 
        {
            LogDebug($"PORTRAIT_DEBUG: petImage is null for {currentPet?.EntityName.Value ?? "null"}");
            return;
        }
        
        LogDebug($"PORTRAIT_DEBUG: UpdatePetImage() called for {currentPet?.EntityName.Value ?? "null"}");
        
        // Try to get actual pet portrait first
        Sprite petPortrait = GetEntityPortrait(currentPet);
        if (petPortrait != null)
        {
            // Use actual pet portrait
            petImage.sprite = petPortrait;
            petImage.color = Color.white; // Reset color to show sprite normally
            petImage.gameObject.SetActive(true);
            LogDebug($"PORTRAIT_DEBUG: Pet portrait loaded for: {currentPet?.EntityName.Value ?? "null"} - Sprite: {petPortrait.name}");
        }
        else
        {
            // Fall back to placeholder
            SetupPlaceholderImage();
            LogDebug($"PORTRAIT_DEBUG: Using placeholder image for: {currentPet?.EntityName.Value ?? "null"}");
        }
    }
    
    /// <summary>
    /// Gets the portrait sprite for the given entity (supports both Pet and Player entities)
    /// Uses the same approach as EntitySelectionController and existing UI managers
    /// </summary>
    private Sprite GetEntityPortrait(NetworkEntity entity)
    {
        if (entity == null) 
        {
            LogDebug($"PORTRAIT_DEBUG: Entity is null");
            return null;
        }
        
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
        else
        {
            LogDebug($"PORTRAIT_DEBUG: No SpriteRenderer found for {entity.EntityName.Value}");
        }
        
        // Method 3: Try to get from Image component (UI approach)
        Image imageComponent = entity.GetComponentInChildren<Image>();
        if (imageComponent != null && imageComponent.sprite != null)
        {
            LogDebug($"PORTRAIT_DEBUG: Found portrait from Image component for {entity.EntityName.Value}");
            return imageComponent.sprite;
        }
        else
        {
            LogDebug($"PORTRAIT_DEBUG: No Image component found for {entity.EntityName.Value}");
        }
        
        LogDebug($"PORTRAIT_DEBUG: No portrait found for entity {entity.EntityName.Value} - all methods failed");
        return null;
    }
    
    /// <summary>
    /// Gets the portrait sprite from selection data (supports both Pet and Player entities)
    /// </summary>
    private Sprite GetPortraitFromSelectionData(NetworkEntity entity)
    {
        if (entity == null) 
        {
            LogDebug("PORTRAIT_DEBUG: entity is null");
            return null;
        }
        
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
    /// Sets up the placeholder image (moved from UpdatePetImage for reusability)
    /// </summary>
    private void SetupPlaceholderImage()
    {
        if (petImage == null) return;
        
        // Create a simple white texture for placeholder
        Texture2D placeholderTexture = new Texture2D(1, 1);
        placeholderTexture.SetPixel(0, 0, placeholderColor);
        placeholderTexture.Apply();
        
        // Create sprite from texture
        Sprite placeholderSprite = Sprite.Create(
            placeholderTexture,
            new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f)
        );
        
        petImage.sprite = placeholderSprite;
        petImage.color = placeholderColor;
        petImage.gameObject.SetActive(true);
        
        LogDebug("Placeholder pet image setup complete");
    }
    
    /// <summary>
    /// Clears all pet visual elements
    /// </summary>
    private void ClearPetVisuals()
    {
        if (petNameText != null)
        {
            petNameText.text = "";
        }
        
        if (petImage != null)
        {
            petImage.gameObject.SetActive(false);
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
    /// Sets the placeholder color for the pet image
    /// </summary>
    /// <param name="color">The color to use for the placeholder</param>
    public void SetPlaceholderColor(Color color)
    {
        placeholderColor = color;
        if (petImage != null)
        {
            petImage.color = color;
        }
    }
    
    /// <summary>
    /// Shows a damage/heal preview over this pet
    /// </summary>
    /// <param name="amount">The damage (positive) or heal (negative) amount</param>
    /// <param name="isDamage">True for damage, false for healing</param>
    public void ShowDamagePreview(int amount, bool isDamage)
    {
        if (damagePreviewText == null) return;

        // Set text color based on damage or heal
        if (isDamage)
        {
            damagePreviewText.color = Color.red;
            damagePreviewText.text = $"-{amount}";
        }
        else
        {
            damagePreviewText.color = Color.green;
            damagePreviewText.text = $"+{amount}";
        }

        // Always keep the text component enabled, just show the container if it exists
        if (damagePreviewContainer != null)
            damagePreviewContainer.SetActive(true);

        LogDebug($"Showing {(isDamage ? "damage" : "heal")} preview of {amount} on {currentPet?.EntityName.Value} over pet image");
    }

    /// <summary>
    /// Hides the damage/heal preview
    /// </summary>
    public void HideDamagePreview()
    {
        if (damagePreviewText != null)
        {
            damagePreviewText.text = ""; // Clear text but keep component enabled
        }

        // Hide container if it exists, but keep text component itself enabled
        if (damagePreviewContainer != null)
            damagePreviewContainer.SetActive(false);

        LogDebug($"Hiding damage preview on {currentPet?.EntityName.Value}");
    }

    /// <summary>
    /// Gets the pet image component (for UI positioning purposes)
    /// </summary>
    public Image GetPetImage()
    {
        return petImage;
    }
    
    private void LogDebug(string message)
    {
        if (debugLogEnabled)
        {
            Debug.Log($"[OwnPetVisualDisplay] {message}");
        }
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[OwnPetVisualDisplay] {message}");
    }
} 