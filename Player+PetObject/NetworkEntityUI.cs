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
        
        if (effectHandler == null)
        {
            effectsText.text = ""; // Hide effects text if no effect handler
            return;
        }
        
        List<string> activeEffects = effectHandler.GetActiveEffects();
        
        if (activeEffects.Count == 0)
        {
            effectsText.text = ""; // Hide when no effects
        }
        else
        {
            effectsText.text = "Effects: " + string.Join(", ", activeEffects);
        }
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
} 