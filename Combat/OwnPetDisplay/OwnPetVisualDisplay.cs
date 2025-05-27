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
    
    private void SetupPlaceholderImage()
    {
        if (petImage != null)
        {
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
            
            LogDebug("Placeholder pet image setup complete");
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
        
        // For now, just ensure the placeholder is visible
        // In the future, this could load specific pet sprites based on pet type/ID
        petImage.gameObject.SetActive(true);
        
        // TODO: Implement actual pet image loading based on pet data
        // This could involve:
        // - Pet type/species identification
        // - Loading sprites from Resources or Addressables
        // - Handling different pet variations/skins
        
        LogDebug($"Pet image updated for: {currentPet?.EntityName.Value ?? "null"}");
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