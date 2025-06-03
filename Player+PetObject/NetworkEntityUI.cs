using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Handles the UI visualization for NetworkEntity
/// Attach to: The same GameObject as NetworkEntity
/// </summary>
public class NetworkEntityUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkEntity entity;
    [SerializeField] private NetworkEntityDeck entityDeck;
    [SerializeField] private HandManager handManager;
    [SerializeField] private EffectHandler effectHandler;
    [SerializeField] private EntityTracker entityTracker;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("UI Elements")]
    [SerializeField] private Transform handTransform;
    [SerializeField] private Transform deckTransform;
    [SerializeField] private Transform discardTransform;

    [Header("Entity Stats UI")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI energyText;
    [SerializeField] private Image healthBar;
    [SerializeField] private Image entityImage; // Main visual representation of the entity

    [Header("Damage Preview UI")]
    [SerializeField] private TextMeshProUGUI damagePreviewText;
    [SerializeField] private GameObject damagePreviewContainer;

    [Header("Status Effects UI")]
    [SerializeField] private TextMeshProUGUI effectsText;
    [SerializeField] private Transform effectsContainer; // Optional: for individual effect icons later

    [Header("Player-Only UI")]
    [SerializeField] private TextMeshProUGUI currencyText;

    [Header("Card System UI")]
    [SerializeField] private TextMeshProUGUI deckCountText;
    [SerializeField] private TextMeshProUGUI discardCountText;

    private void Awake()
    {
        // Get required components
        if (entity == null) entity = GetComponent<NetworkEntity>();
        if (entityDeck == null) entityDeck = GetComponent<NetworkEntityDeck>();
        if (handManager == null) handManager = GetComponent<HandManager>();
        if (effectHandler == null) effectHandler = GetComponent<EffectHandler>();
        if (entityTracker == null) entityTracker = GetComponent<EntityTracker>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        // Add CanvasGroup if not present
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        ValidateComponents();

        // Default to hidden until game state determines visibility
        SetVisible(false);
    }

    private void ValidateComponents()
    {
        if (entity == null)
        {
            Debug.LogError($"NetworkEntityUI on {gameObject.name}: Missing NetworkEntity component");
            return;
        }

        // Validate components based on entity type
        if (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet)
        {
            // Main entities should have NetworkEntityDeck but not HandManager
            if (entityDeck == null)
                Debug.LogError($"NetworkEntityUI on {gameObject.name}: Missing NetworkEntityDeck component");
            // HandManager should be on the Hand entity, not the main entity
        }
        else if (entity.EntityType == EntityType.PlayerHand || entity.EntityType == EntityType.PetHand)
        {
            // Hand entities should have HandManager but not NetworkEntityDeck
            if (handManager == null)
                Debug.LogError($"NetworkEntityUI on {gameObject.name}: Missing HandManager component");
            // NetworkEntityDeck should be on the main entity, not the hand entity
        }
    }

    private void OnEnable()
    {
        if (entityDeck != null)
        {
            entityDeck.OnDeckChanged += UpdateDeckDisplay;
        }

        if (effectHandler != null)
        {
            effectHandler.OnEffectsChanged += UpdateEffectsDisplay;
        }

        if (entityTracker != null)
        {
            entityTracker.OnStanceChanged += OnStanceChanged;
        }

        if (entity != null)
        {
            // Subscribe to entity stat changes
            entity.EntityName.OnChange += OnEntityNameChanged;
            entity.CurrentHealth.OnChange += OnCurrentHealthChanged;
            entity.MaxHealth.OnChange += OnMaxHealthChanged;
            entity.CurrentEnergy.OnChange += OnCurrentEnergyChanged;
            entity.MaxEnergy.OnChange += OnMaxEnergyChanged;

            // Subscribe to currency changes for players
            if (entity.EntityType == EntityType.Player)
            {
                entity.OnCurrencyChanged += UpdateCurrencyDisplay;
            }
        }
    }

    private void OnDisable()
    {
        if (entityDeck != null)
        {
            entityDeck.OnDeckChanged -= UpdateDeckDisplay;
        }

        if (effectHandler != null)
        {
            effectHandler.OnEffectsChanged -= UpdateEffectsDisplay;
        }

        if (entityTracker != null)
        {
            entityTracker.OnStanceChanged -= OnStanceChanged;
        }

        if (entity != null)
        {
            // Unsubscribe from entity stat changes
            entity.EntityName.OnChange -= OnEntityNameChanged;
            entity.CurrentHealth.OnChange -= OnCurrentHealthChanged;
            entity.MaxHealth.OnChange -= OnMaxHealthChanged;
            entity.CurrentEnergy.OnChange -= OnCurrentEnergyChanged;
            entity.MaxEnergy.OnChange -= OnMaxEnergyChanged;

            // Unsubscribe from currency changes for players
            if (entity.EntityType == EntityType.Player)
            {
                entity.OnCurrencyChanged -= UpdateCurrencyDisplay;
            }
        }
    }

    private void Start()
    {
        if (entity != null)
        {
            UpdateEntityUI();
        }

        UpdateDeckDisplay();
        UpdateDiscardDisplay();
        UpdateEffectsDisplay();
    }

    public void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1.0f : 0.0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }

    public void UpdateEntityUI()
    {
        if (entity == null) return;

        if (nameText != null) nameText.text = entity.EntityName.Value;
        UpdateHealthUI();
        UpdateEnergyUI();
        if (entity.EntityType == EntityType.Player)
        {
            UpdateCurrencyDisplay(entity.Currency.Value);
        }
    }

    private void OnEntityNameChanged(string prev, string next, bool asServer)
    {
        if (nameText != null)
        {
            nameText.text = next;
        }
    }

    private void OnCurrentHealthChanged(int prev, int next, bool asServer)
    {
        UpdateHealthUI();
    }

    private void OnMaxHealthChanged(int prev, int next, bool asServer)
    {
        UpdateHealthUI();
    }

    /// <summary>
    /// Updates the entity health UI
    /// </summary>
    public void UpdateHealthUI()
    {
        if (entity == null) return;

        int current = entity.CurrentHealth.Value;
        int max = entity.MaxHealth.Value;

        if (healthText != null)
        {
            healthText.text = $"{current}/{max}";
        }

        if (healthBar != null)
        {
            healthBar.fillAmount = max > 0 ? (float)current / max : 0;
        }
    }

    private void OnCurrentEnergyChanged(int prev, int next, bool asServer)
    {
        UpdateEnergyUI();
    }

    private void OnMaxEnergyChanged(int prev, int next, bool asServer)
    {
        UpdateEnergyUI();
    }

    /// <summary>
    /// Updates the entity energy UI
    /// </summary>
    public void UpdateEnergyUI()
    {
        if (entity == null) return;

        int current = entity.CurrentEnergy.Value;
        int max = entity.MaxEnergy.Value;

        if (energyText != null)
        {
            energyText.text = $"{current}/{max}";
        }
    }

    private void UpdateDeckDisplay()
    {
        if (deckCountText != null)
        {
            deckCountText.text = "0";
        }
    }

    private void UpdateDiscardDisplay()
    {
        if (discardCountText != null)
        {
            discardCountText.text = "0";
        }
    }

    private void UpdateCurrencyDisplay(int newCurrency)
    {
        if (currencyText != null && entity.EntityType == EntityType.Player)
        {
            currencyText.text = newCurrency.ToString();
        }
    }

    private void UpdateEffectsDisplay()
    {
        if (effectsText == null) return;
        
        List<string> displayItems = new List<string>();
        
        // Add current stance if not None
        if (entityTracker != null && entityTracker.CurrentStance != StanceType.None)
        {
            int duration = entityTracker.StanceDuration;
            string stanceDisplay = duration == 0 ? 
                $"Stance: {entityTracker.CurrentStance}" : 
                $"Stance: {entityTracker.CurrentStance} ({duration}/2)";
            displayItems.Add(stanceDisplay);
        }
        
        // Add status effects
        if (effectHandler != null)
        {
            List<string> activeEffects = effectHandler.GetActiveEffects();
            displayItems.AddRange(activeEffects);
        }
        
        // Display everything or hide if empty
        if (displayItems.Count == 0)
        {
            effectsText.text = ""; // Hide when no effects or stance
        }
        else
        {
            effectsText.text = string.Join(", ", displayItems);
        }
    }

    private void OnStanceChanged(StanceType prev, StanceType next)
    {
        UpdateEffectsDisplay();
    }

    // Public getters for transforms
    public Transform GetHandTransform() 
    {
        // Only Hand entities should have hand transforms
        if (entity != null && (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet))
        {
            // Main entities don't have hand transforms - they're on the Hand entity
            Debug.LogWarning($"NetworkEntityUI on {gameObject.name}: Main entity {entity.EntityType} should not have hand transform. Use RelationshipManager to find Hand entity.");
            return null;
        }

        if (handTransform == null)
        {
            Debug.LogError($"NetworkEntityUI on {gameObject.name}: handTransform is null");
            return null;
        }
        return handTransform;
    }

    public Transform GetDeckTransform() 
    {
        // Only Hand entities should have deck transforms
        if (entity != null && (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet))
        {
            // Main entities don't have deck transforms - they're on the Hand entity
            Debug.LogWarning($"NetworkEntityUI on {gameObject.name}: Main entity {entity.EntityType} should not have deck transform. Use RelationshipManager to find Hand entity.");
            return null;
        }

        if (deckTransform == null)
        {
            Debug.LogError($"NetworkEntityUI on {gameObject.name}: deckTransform is null");
            return null;
        }
        return deckTransform;
    }

    public Transform GetDiscardTransform() 
    {
        // Only Hand entities should have discard transforms
        if (entity != null && (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet))
        {
            // Main entities don't have discard transforms - they're on the Hand entity
            Debug.LogWarning($"NetworkEntityUI on {gameObject.name}: Main entity {entity.EntityType} should not have discard transform. Use RelationshipManager to find Hand entity.");
            return null;
        }

        if (discardTransform == null)
        {
            Debug.LogError($"NetworkEntityUI on {gameObject.name}: discardTransform is null");
            return null;
        }
        return discardTransform;
    }

    /// <summary>
    /// Shows a damage/heal preview over this entity
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

        Debug.Log($"NetworkEntityUI: Showing {(isDamage ? "damage" : "heal")} preview of {amount} on {entity?.EntityName.Value} over entity image");
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

        Debug.Log($"NetworkEntityUI: Hiding damage preview on {entity?.EntityName.Value}");
    }

    /// <summary>
    /// Gets the main visual image for this entity (for UI positioning purposes)
    /// </summary>
    public Image GetEntityImage()
    {
        return entityImage;
    }
} 