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
            Debug.LogError($"NetworkEntityUI on {gameObject.name}: Missing NetworkEntity component");
        if (entityDeck == null)
            Debug.LogError($"NetworkEntityUI on {gameObject.name}: Missing NetworkEntityDeck component");
        if (handManager == null)
            Debug.LogError($"NetworkEntityUI on {gameObject.name}: Missing HandManager component");
    }

    private void OnEnable()
    {
        if (entityDeck != null)
        {
            entityDeck.OnDeckChanged += UpdateDeckDisplay;
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

    private void UpdateHealthUI()
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

    private void UpdateEnergyUI()
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

    // Public getters for transforms
    public Transform GetHandTransform() 
    {
        if (handTransform == null)
        {
            Debug.LogError($"NetworkEntityUI on {gameObject.name}: handTransform is null");
            // Create a fallback transform if needed
            GameObject fallbackObj = new GameObject("FallbackHandTransform");
            fallbackObj.transform.SetParent(transform);
            fallbackObj.transform.localPosition = Vector3.zero;
            handTransform = fallbackObj.transform;
            Debug.Log($"Created fallback handTransform for {gameObject.name}");
        }
        return handTransform;
    }

    public Transform GetDeckTransform() 
    {
        if (deckTransform == null)
        {
            Debug.LogError($"NetworkEntityUI on {gameObject.name}: deckTransform is null");
            // Create a fallback transform if needed
            GameObject fallbackObj = new GameObject("FallbackDeckTransform");
            fallbackObj.transform.SetParent(transform);
            fallbackObj.transform.localPosition = Vector3.zero;
            deckTransform = fallbackObj.transform;
            Debug.Log($"Created fallback deckTransform for {gameObject.name}");
        }
        return deckTransform;
    }

    public Transform GetDiscardTransform() 
    {
        if (discardTransform == null)
        {
            Debug.LogError($"NetworkEntityUI on {gameObject.name}: discardTransform is null");
            // Create a fallback transform if needed
            GameObject fallbackObj = new GameObject("FallbackDiscardTransform");
            fallbackObj.transform.SetParent(transform);
            fallbackObj.transform.localPosition = Vector3.zero;
            discardTransform = fallbackObj.transform;
            Debug.Log($"Created fallback discardTransform for {gameObject.name}");
        }
        return discardTransform;
    }
} 