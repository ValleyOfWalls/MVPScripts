using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Handles the UI visualization for NetworkPet
/// Attach to: The same GameObject as NetworkPet
/// </summary>
public class NetworkPetUI : MonoBehaviour
{
    // Static instance for easy access
    public static NetworkPetUI Instance { get; private set; }
    
    [Header("References")]
    [SerializeField] private NetworkPet pet;
    [SerializeField] private NetworkEntityDeck entityDeck;
    [SerializeField] private CombatDiscard combatDiscard;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("UI Elements")]
    [SerializeField] private Transform petHandTransform;
    [SerializeField] private Transform deckTransform;
    [SerializeField] private Transform discardTransform;
    
    [Header("Pet Stats UI")]
    [SerializeField] private TextMeshProUGUI petNameText;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI energyText;
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
            Debug.LogWarning("More than one NetworkPetUI instance exists. This may cause issues.");
        }
        
        if (pet == null) pet = GetComponent<NetworkPet>();
        if (entityDeck == null) entityDeck = GetComponent<NetworkEntityDeck>();
        if (combatDiscard == null) combatDiscard = GetComponent<CombatDiscard>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        
        // Add CanvasGroup if not already present
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        if (pet == null)
        {
            Debug.LogError("NetworkPetUI: Cannot find NetworkPet component.");
        }
        
        if (entityDeck == null)
        {
            Debug.LogError("NetworkPetUI: Cannot find NetworkEntityDeck component.");
        }
        
        if (combatDiscard == null)
        {
            Debug.LogError("NetworkPetUI: Cannot find CombatDiscard component.");
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
        
        if (pet != null)
        {
            // Subscribe to pet stat changes
            pet.PetName.OnChange += OnPetNameChanged;
            pet.CurrentHealth.OnChange += OnCurrentHealthChanged;
            pet.MaxHealth.OnChange += OnMaxHealthChanged;
            pet.CurrentEnergy.OnChange += OnCurrentEnergyChanged;
            pet.MaxEnergy.OnChange += OnMaxEnergyChanged;
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
        
        if (pet != null)
        {
            // Unsubscribe from pet stat changes
            pet.PetName.OnChange -= OnPetNameChanged;
            pet.CurrentHealth.OnChange -= OnCurrentHealthChanged;
            pet.MaxHealth.OnChange -= OnMaxHealthChanged;
            pet.CurrentEnergy.OnChange -= OnCurrentEnergyChanged;
            pet.MaxEnergy.OnChange -= OnMaxEnergyChanged;
        }
    }
    
    private void Start()
    {
        if (pet != null)
        {
            // Initial UI setup
            UpdatePetUI();
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
    /// Updates the pet's stats UI
    /// </summary>
    public void UpdatePetUI()
    {
        if (pet == null) return;
        
        if (petNameText != null) petNameText.text = pet.PetName.Value;
        UpdateHealthUI();
        UpdateEnergyUI();
    }
    
    /// <summary>
    /// Handles pet name changes
    /// </summary>
    private void OnPetNameChanged(string prev, string next, bool asServer)
    {
        if (petNameText != null)
        {
            petNameText.text = next;
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
        if (pet == null) return;
        
        int current = pet.CurrentHealth.Value;
        int max = pet.MaxHealth.Value;
        
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
        if (pet == null) return;
        
        int current = pet.CurrentEnergy.Value;
        int max = pet.MaxEnergy.Value;
        
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
        if (deckCountText != null && pet != null)
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
    /// Updates the pet's name display in the UI
    /// </summary>
    public void UpdateNameDisplay()
    {
        if (pet == null || petNameText == null) return;
        
        // Update the pet name text
        petNameText.text = pet.PetName.Value;
        
        // Log the name update
        Debug.Log($"UI updated for pet name: {pet.PetName.Value}");
    }
    
    /// <summary>
    /// Gets the pet hand transform for external access
    /// </summary>
    public Transform GetPetHandTransform()
    {
        return petHandTransform;
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