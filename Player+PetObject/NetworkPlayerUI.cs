using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Handles the UI visualization for NetworkPlayer
/// Attach to: The same GameObject as NetworkPlayer
/// </summary>
public class NetworkPlayerUI : MonoBehaviour
{
    // Static instance for easy access
    public static NetworkPlayerUI Instance { get; private set; }
    
    [Header("References")]
    [SerializeField] private NetworkPlayer player;
    [SerializeField] private NetworkEntityDeck entityDeck;
    [SerializeField] private CombatDiscard combatDiscard;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("UI Elements")]
    [SerializeField] private Transform playerHandTransform;
    [SerializeField] private Transform deckTransform;
    [SerializeField] private Transform discardTransform;
    
    [Header("Player Stats UI")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI energyText;
    [SerializeField] private TextMeshProUGUI currencyText;
    [SerializeField] private Image healthBar;
    
    [Header("Card System UI")]
    [SerializeField] private TextMeshProUGUI deckCountText;
    [SerializeField] private TextMeshProUGUI discardCountText;
    
    private void Awake()
    {
        // Set static instance
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("More than one NetworkPlayerUI instance exists. This may cause issues.");
        }
        
        if (player == null) player = GetComponent<NetworkPlayer>();
        if (entityDeck == null) entityDeck = GetComponent<NetworkEntityDeck>();
        if (combatDiscard == null) combatDiscard = GetComponent<CombatDiscard>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        
        // Add CanvasGroup if not already present
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        if (player == null)
        {
            Debug.LogError("NetworkPlayerUI: Cannot find NetworkPlayer component.");
        }
        
        if (entityDeck == null)
        {
            Debug.LogError("NetworkPlayerUI: Cannot find NetworkEntityDeck component.");
        }
        
        if (combatDiscard == null)
        {
            Debug.LogError("NetworkPlayerUI: Cannot find CombatDiscard component.");
        }
        
        // Default to hidden until the game state determines visibility
        SetVisible(false);
    }
    
    private void OnEnable()
    {
        if (entityDeck != null)
        {
            entityDeck.OnDeckChanged += UpdateDeckDisplay;
        }
        
        if (combatDiscard != null)
        {
            combatDiscard.OnDiscardChanged += UpdateDiscardDisplay;
        }
        
        if (player != null)
        {
            // Subscribe to player stat changes
            player.OnCurrencyChanged += UpdateCurrencyDisplay;
            player.PlayerName.OnChange += OnPlayerNameChanged;
            player.CurrentHealth.OnChange += OnCurrentHealthChanged;
            player.MaxHealth.OnChange += OnMaxHealthChanged;
            player.CurrentEnergy.OnChange += OnCurrentEnergyChanged;
            player.MaxEnergy.OnChange += OnMaxEnergyChanged;
        }
    }
    
    private void OnDisable()
    {
        if (entityDeck != null)
        {
            entityDeck.OnDeckChanged -= UpdateDeckDisplay;
        }
        
        if (combatDiscard != null)
        {
            combatDiscard.OnDiscardChanged -= UpdateDiscardDisplay;
        }
        
        if (player != null)
        {
            // Unsubscribe from player stat changes
            player.OnCurrencyChanged -= UpdateCurrencyDisplay;
            player.PlayerName.OnChange -= OnPlayerNameChanged;
            player.CurrentHealth.OnChange -= OnCurrentHealthChanged;
            player.MaxHealth.OnChange -= OnMaxHealthChanged;
            player.CurrentEnergy.OnChange -= OnCurrentEnergyChanged;
            player.MaxEnergy.OnChange -= OnMaxEnergyChanged;
        }
    }
    
    private void Start()
    {
        if (player != null)
        {
            // Initial UI setup
            UpdatePlayerUI();
        }
        
        // Initialize card system UI
        UpdateDeckDisplay();
        UpdateDiscardDisplay();
    }
    
    /// <summary>
    /// Set the visibility of this UI
    /// </summary>
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
    
    /// <summary>
    /// Updates the player's stats UI
    /// </summary>
    public void UpdatePlayerUI()
    {
        if (player == null) return;
        
        if (playerNameText != null) playerNameText.text = player.PlayerName.Value;
        UpdateHealthUI();
        UpdateEnergyUI();
        if (currencyText != null) currencyText.text = $"{player.Currency.Value}";
    }
    
    /// <summary>
    /// Handles player name changes
    /// </summary>
    private void OnPlayerNameChanged(string prev, string next, bool asServer)
    {
        if (playerNameText != null)
        {
            playerNameText.text = next;
        }
    }
    
    /// <summary>
    /// Handles current health changes
    /// </summary>
    private void OnCurrentHealthChanged(int prev, int next, bool asServer)
    {
        UpdateHealthUI();
    }
    
    /// <summary>
    /// Handles max health changes
    /// </summary>
    private void OnMaxHealthChanged(int prev, int next, bool asServer)
    {
        UpdateHealthUI();
    }
    
    /// <summary>
    /// Updates the health UI elements
    /// </summary>
    private void UpdateHealthUI()
    {
        if (player == null) return;
        
        int current = player.CurrentHealth.Value;
        int max = player.MaxHealth.Value;
        
        if (healthText != null)
        {
            healthText.text = $"{current}/{max}";
        }
        
        if (healthBar != null)
        {
            healthBar.fillAmount = max > 0 ? (float)current / max : 0;
        }
    }
    
    /// <summary>
    /// Handles current energy changes
    /// </summary>
    private void OnCurrentEnergyChanged(int prev, int next, bool asServer)
    {
        UpdateEnergyUI();
    }
    
    /// <summary>
    /// Handles max energy changes
    /// </summary>
    private void OnMaxEnergyChanged(int prev, int next, bool asServer)
    {
        UpdateEnergyUI();
    }
    
    /// <summary>
    /// Updates the energy UI elements
    /// </summary>
    private void UpdateEnergyUI()
    {
        if (player == null) return;
        
        int current = player.CurrentEnergy.Value;
        int max = player.MaxEnergy.Value;
        
        if (energyText != null)
        {
            energyText.text = $"{current}/{max}";
        }
    }
    
    /// <summary>
    /// Updates the deck display with the current cards
    /// </summary>
    private void UpdateDeckDisplay()
    {
        if (deckCountText != null && player != null)
        {
            int deckCount = 0;
            CombatDeck combatDeck = GetComponent<CombatDeck>();
            if (combatDeck != null)
            {
                deckCount = combatDeck.GetDeckSize();
            }
            deckCountText.text = $"Deck: {deckCount}";
        }
    }
    
    /// <summary>
    /// Updates the discard pile display
    /// </summary>
    private void UpdateDiscardDisplay()
    {
        if (discardCountText != null && combatDiscard != null)
        {
            discardCountText.text = $"Discard: {combatDiscard.GetCardCount()}";
        }
    }
    
    /// <summary>
    /// Updates the currency display
    /// </summary>
    private void UpdateCurrencyDisplay(int newCurrency)
    {
        if (currencyText != null)
        {
            currencyText.text = newCurrency.ToString();
        }
    }
    
    /// <summary>
    /// Gets the player hand transform for external access
    /// </summary>
    public Transform GetPlayerHandTransform()
    {
        return playerHandTransform;
    }
    
    /// <summary>
    /// Gets the deck transform for external access
    /// </summary>
    public Transform GetDeckTransform()
    {
        return deckTransform;
    }
    
    /// <summary>
    /// Gets the discard transform for external access
    /// </summary>
    public Transform GetDiscardTransform()
    {
        return discardTransform;
    }
} 