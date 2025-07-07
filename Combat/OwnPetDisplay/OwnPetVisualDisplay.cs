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
        currentPet = pet;
        
        if (pet != null)
        {
            UpdatePetVisuals();
            LogDebug($"Pet visual display set to: {pet.EntityName.Value}");
        }
        else
        {
            ClearPetVisuals();
            LogDebug("Pet visual display cleared");
        }
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
        if (petImage == null) return;
        
        // Try to get actual pet portrait first
        Sprite petPortrait = GetEntityPortrait(currentPet);
        if (petPortrait != null)
        {
            // Use actual pet portrait
            petImage.sprite = petPortrait;
            petImage.color = Color.white; // Reset color to show sprite normally
            petImage.gameObject.SetActive(true);
            LogDebug($"Pet portrait loaded for: {currentPet?.EntityName.Value ?? "null"}");
        }
        else
        {
            // Fall back to placeholder
            SetupPlaceholderImage();
            LogDebug($"Using placeholder image for: {currentPet?.EntityName.Value ?? "null"}");
        }
    }
    
    /// <summary>
    /// Gets the portrait sprite for the given pet entity
    /// Uses the same approach as EntitySelectionController and existing UI managers
    /// </summary>
    private Sprite GetEntityPortrait(NetworkEntity petEntity)
    {
        if (petEntity == null || petEntity.EntityType != EntityType.Pet) 
            return null;
        
        // Method 1: Try to get from PetData through selection system
        Sprite portraitFromData = GetPortraitFromPetData(petEntity);
        if (portraitFromData != null)
        {
            LogDebug($"Found portrait from PetData for {petEntity.EntityName.Value}");
            return portraitFromData;
        }
        
        // Method 2: Try to get from SpriteRenderer (legacy approach)
        SpriteRenderer spriteRenderer = petEntity.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            LogDebug($"Found portrait from SpriteRenderer for {petEntity.EntityName.Value}");
            return spriteRenderer.sprite;
        }
        
        // Method 3: Try to get from Image component (UI approach)
        Image imageComponent = petEntity.GetComponentInChildren<Image>();
        if (imageComponent != null && imageComponent.sprite != null)
        {
            LogDebug($"Found portrait from Image component for {petEntity.EntityName.Value}");
            return imageComponent.sprite;
        }
        
        LogDebug($"No portrait found for pet {petEntity.EntityName.Value}");
        return null;
    }
    
    /// <summary>
    /// Gets the portrait sprite from PetData through the selection system
    /// </summary>
    private Sprite GetPortraitFromPetData(NetworkEntity petEntity)
    {
        if (petEntity == null) return null;
        
        // Try to get PetData through CharacterSelectionManager (similar to EntityModelManager)
        CharacterSelectionManager selectionManager = FindFirstObjectByType<CharacterSelectionManager>();
        if (selectionManager == null)
        {
            LogDebug("CharacterSelectionManager not found, cannot get PetData");
            return null;
        }
        
        // Check if the pet has selection data
        int petIndex = petEntity.SelectedPetIndex.Value;
        if (petIndex < 0)
        {
            LogDebug($"Pet {petEntity.EntityName.Value} has no selection index");
            return null;
        }
        
        // Get available pets and find the one at the selected index
        var availablePets = selectionManager.GetAvailablePets();
        if (petIndex >= 0 && petIndex < availablePets.Count)
        {
            PetData petData = availablePets[petIndex];
            if (petData != null && petData.PetPortrait != null)
            {
                LogDebug($"Found PetData portrait for {petEntity.EntityName.Value} at index {petIndex}");
                return petData.PetPortrait;
            }
        }
        
        LogDebug($"No PetData found for pet {petEntity.EntityName.Value} at index {petIndex}");
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