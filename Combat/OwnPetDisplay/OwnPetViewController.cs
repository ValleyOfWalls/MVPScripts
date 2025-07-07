using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using System.Collections;

/// <summary>
/// Main controller for the OwnPetView functionality.
/// Manages the display of the currently viewed player's pet in the combat UI.
/// Attach to: OwnPetViewPrefab GameObject
/// </summary>
public class OwnPetViewController : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private OwnPetVisualDisplay visualDisplay;
    [SerializeField] private OwnPetStatsDisplay statsDisplay;
    [SerializeField] private OwnPetStatusEffectsDisplay statusEffectsDisplay;
    [SerializeField] private OwnPetCardDropZone cardDropZone;
    
    [Header("Debug")]
    [SerializeField] private bool debugLogEnabled = true;
    
    // Current pet being displayed
    private NetworkEntity currentDisplayedPet;
    
    // Managers
    private FightManager fightManager;
    private EntityVisibilityManager entityVisibilityManager;
    
    private void Awake()
    {
        ValidateComponents();
        
        // Check if this GameObject has a NetworkEntity component
        NetworkEntity networkEntity = GetComponent<NetworkEntity>();
        if (networkEntity != null)
        {
            LogDebug($"OwnPetViewController has NetworkEntity component - this may cause visibility conflicts with EntityVisibilityManager!");
            LogDebug($"NetworkEntity Type: {networkEntity.EntityType}, Name: {networkEntity.EntityName.Value}");
        }
        else
        {
            LogDebug("OwnPetViewController does not have NetworkEntity component - good for UI elements");
        }
    }
    
    private void Start()
    {
        FindManagers();
        
        // Check if this view has already been set up (e.g., by RPC)
        // If we already have a displayed pet, don't interfere with the setup
        if (currentDisplayedPet != null)
        {
            LogDebug("Start() called but pet already displayed - skipping initial setup to avoid interference");
            return;
        }
        
        SetupInitialState();
    }
    
    private void OnEnable()
    {
        LogDebug("OwnPetViewController enabled");
        SubscribeToEvents();
    }
    
    private void OnDisable()
    {
        LogDebug("OwnPetViewController disabled - this might be caused by EntityVisibilityManager or other systems");
        UnsubscribeFromEvents();
    }
    
    private void ValidateComponents()
    {
        if (visualDisplay == null)
            visualDisplay = GetComponent<OwnPetVisualDisplay>();
        if (statsDisplay == null)
            statsDisplay = GetComponent<OwnPetStatsDisplay>();
        if (statusEffectsDisplay == null)
            statusEffectsDisplay = GetComponent<OwnPetStatusEffectsDisplay>();
        if (cardDropZone == null)
            cardDropZone = GetComponent<OwnPetCardDropZone>();
            
        if (visualDisplay == null)
            LogError("OwnPetVisualDisplay component not found!");
        if (statsDisplay == null)
            LogError("OwnPetStatsDisplay component not found!");
        if (statusEffectsDisplay == null)
            LogError("OwnPetStatusEffectsDisplay component not found!");
        if (cardDropZone == null)
            LogError("OwnPetCardDropZone component not found!");
    }
    
    private void FindManagers()
    {
        if (fightManager == null)
        {
            fightManager = FindFirstObjectByType<FightManager>();
            if (fightManager == null)
            {
                LogDebug("FightManager not found in scene - will retry later");
            }
            else
            {
                LogDebug($"Found FightManager: {fightManager.name}");
            }
        }
        
        if (entityVisibilityManager == null)
        {
            entityVisibilityManager = FindFirstObjectByType<EntityVisibilityManager>();
            if (entityVisibilityManager == null)
            {
                LogDebug("EntityVisibilityManager not found in scene");
            }
            else
            {
                LogDebug($"Found EntityVisibilityManager: {entityVisibilityManager.name}");
            }
        }
    }
    
    private void SetupInitialState()
    {
        // If the view is already active and has a pet, don't hide it
        if (gameObject.activeInHierarchy && currentDisplayedPet != null)
        {
            LogDebug("SetupInitialState called but view already active with pet - skipping hide");
            return;
        }
        
        // Initially hide the view until we have a pet to display
        SetViewVisible(false);
        
        // Try to find and display the current viewed player's pet
        UpdateDisplayedPet();
    }
    
    private void SubscribeToEvents()
    {
        if (fightManager != null)
        {
            fightManager.OnFightsChanged += OnFightsChanged;
        }
    }
    
    private void UnsubscribeFromEvents()
    {
        if (fightManager != null)
        {
            fightManager.OnFightsChanged -= OnFightsChanged;
        }
        
        // Unsubscribe from current pet events
        UnsubscribeFromPetEvents();
    }
    
    private void OnFightsChanged(bool hasFights)
    {
        LogDebug($"Fights changed, hasFights: {hasFights}");
        UpdateDisplayedPet();
    }
    
    /// <summary>
    /// Updates which pet is currently being displayed based on the viewed combat player
    /// </summary>
    public void UpdateDisplayedPet()
    {
        // Try to find managers if not available
        if (fightManager == null)
        {
            FindManagers();
        }
        
        if (fightManager == null)
        {
            LogDebug("FightManager not available, will retry in a moment");
            // Instead of immediately hiding, start a retry coroutine
            StartCoroutine(RetryFindingFightManager());
            return;
        }
        
        // Get the currently viewed player
        NetworkEntity viewedPlayer = fightManager.ViewedCombatPlayer;
        if (viewedPlayer == null)
        {
            LogDebug("No viewed combat player found - this is normal during combat setup");
            // Don't hide immediately, the fight might not be set up yet
            StartCoroutine(RetryFindingViewedPlayer());
            return;
        }
        
        // Find the viewed player's own pet through RelationshipManager
        NetworkEntity viewedPlayersPet = GetPlayerOwnPet(viewedPlayer);
        if (viewedPlayersPet == null)
        {
            LogDebug($"No pet found for viewed player {viewedPlayer.EntityName.Value}");
            SetViewVisible(false);
            return;
        }
        
        // Update the displayed pet if it changed
        if (currentDisplayedPet != viewedPlayersPet)
        {
            SetDisplayedPet(viewedPlayersPet);
        }
    }
    
    /// <summary>
    /// Retry finding the FightManager with a delay
    /// </summary>
    private IEnumerator RetryFindingFightManager()
    {
        int retryCount = 0;
        int maxRetries = 10;
        
        while (retryCount < maxRetries && fightManager == null)
        {
            yield return new WaitForSeconds(0.5f);
            FindManagers();
            retryCount++;
            
            if (fightManager != null)
            {
                LogDebug($"Found FightManager on retry {retryCount}");
                UpdateDisplayedPet(); // Try again now that we have FightManager
                yield break;
            }
        }
        
        if (fightManager == null)
        {
            LogDebug($"Failed to find FightManager after {maxRetries} retries - hiding view");
            SetViewVisible(false);
        }
    }
    
    /// <summary>
    /// Retry finding the viewed player with a delay
    /// </summary>
    private IEnumerator RetryFindingViewedPlayer()
    {
        int retryCount = 0;
        int maxRetries = 10;
        
        while (retryCount < maxRetries)
        {
            yield return new WaitForSeconds(0.3f);
            
            if (fightManager != null && fightManager.ViewedCombatPlayer != null)
            {
                LogDebug($"Found viewed combat player on retry {retryCount}");
                UpdateDisplayedPet(); // Try again now that we have a viewed player
                yield break;
            }
            
            retryCount++;
        }
        
        LogDebug($"No viewed combat player found after {maxRetries} retries - this might be normal if no fights are active");
        SetViewVisible(false);
    }
    
    /// <summary>
    /// Gets the player's own pet through RelationshipManager
    /// </summary>
    private NetworkEntity GetPlayerOwnPet(NetworkEntity player)
    {
        if (player == null || player.EntityType != EntityType.Player)
            return null;
            
        RelationshipManager relationshipManager = player.GetComponent<RelationshipManager>();
        if (relationshipManager == null)
        {
            LogDebug($"No RelationshipManager found on player {player.EntityName.Value}");
            return null;
        }
        
        NetworkBehaviour allyEntity = relationshipManager.AllyEntity;
        if (allyEntity == null)
        {
            LogDebug($"No ally entity found for player {player.EntityName.Value}");
            return null;
        }
        
        NetworkEntity petEntity = allyEntity.GetComponent<NetworkEntity>();
        if (petEntity == null || petEntity.EntityType != EntityType.Pet)
        {
            LogDebug($"Ally entity is not a valid pet for player {player.EntityName.Value}");
            return null;
        }
        
        return petEntity;
    }
    
    /// <summary>
    /// Sets the pet to be displayed and updates all components
    /// </summary>
    private void SetDisplayedPet(NetworkEntity pet)
    {
        LogDebug($"Setting displayed pet to: {(pet != null ? pet.EntityName.Value : "null")}");
        
        // Unsubscribe from previous pet events
        UnsubscribeFromPetEvents();
        
        // Update current pet reference
        currentDisplayedPet = pet;
        
        if (pet != null)
        {
            // Update all display components
            if (visualDisplay != null)
                visualDisplay.SetPet(pet);
            if (statsDisplay != null)
                statsDisplay.SetPet(pet);
            if (statusEffectsDisplay != null)
                statusEffectsDisplay.SetPet(pet);
            if (cardDropZone != null)
                cardDropZone.SetTargetPet(pet);
            
            // Subscribe to new pet events
            SubscribeToPetEvents();
            
            // Show the view
            SetViewVisible(true);
        }
        else
        {
            // Hide the view
            SetViewVisible(false);
        }
    }
    
    private void SubscribeToPetEvents()
    {
        if (currentDisplayedPet == null) return;
        
        // Subscribe to health and energy changes
        currentDisplayedPet.CurrentHealth.OnChange += OnPetHealthChanged;
        currentDisplayedPet.MaxHealth.OnChange += OnPetMaxHealthChanged;
        currentDisplayedPet.CurrentEnergy.OnChange += OnPetEnergyChanged;
        currentDisplayedPet.MaxEnergy.OnChange += OnPetMaxEnergyChanged;
        currentDisplayedPet.EntityName.OnChange += OnPetNameChanged;
    }
    
    private void UnsubscribeFromPetEvents()
    {
        if (currentDisplayedPet == null) return;
        
        // Unsubscribe from health and energy changes
        currentDisplayedPet.CurrentHealth.OnChange -= OnPetHealthChanged;
        currentDisplayedPet.MaxHealth.OnChange -= OnPetMaxHealthChanged;
        currentDisplayedPet.CurrentEnergy.OnChange -= OnPetEnergyChanged;
        currentDisplayedPet.MaxEnergy.OnChange -= OnPetMaxEnergyChanged;
        currentDisplayedPet.EntityName.OnChange -= OnPetNameChanged;
    }
    
    private void OnPetHealthChanged(int prev, int next, bool asServer)
    {
        if (statsDisplay != null)
            statsDisplay.UpdateHealthDisplay();
    }
    
    private void OnPetMaxHealthChanged(int prev, int next, bool asServer)
    {
        if (statsDisplay != null)
            statsDisplay.UpdateHealthDisplay();
    }
    
    private void OnPetEnergyChanged(int prev, int next, bool asServer)
    {
        if (statsDisplay != null)
            statsDisplay.UpdateEnergyDisplay();
    }
    
    private void OnPetMaxEnergyChanged(int prev, int next, bool asServer)
    {
        if (statsDisplay != null)
            statsDisplay.UpdateEnergyDisplay();
    }
    
    private void OnPetNameChanged(string prev, string next, bool asServer)
    {
        if (visualDisplay != null)
            visualDisplay.UpdatePetName();
    }
    
    /// <summary>
    /// Shows or hides the entire pet view
    /// </summary>
    private void SetViewVisible(bool visible)
    {
        gameObject.SetActive(visible);
        LogDebug($"Pet view visibility set to: {visible}");
    }
    
    /// <summary>
    /// Public method to force refresh the displayed pet (called by CombatCanvasManager)
    /// </summary>
    public void RefreshDisplayedPet()
    {
        UpdateDisplayedPet();
        
        // Force refresh of pet portraits in case pet data has changed
        RefreshPetPortraits();
    }
    
    /// <summary>
    /// Forces a refresh of pet portraits in all display components
    /// </summary>
    public void RefreshPetPortraits()
    {
        if (currentDisplayedPet == null) return;
        
        // Refresh portrait in stats display
        if (statsDisplay != null)
        {
            statsDisplay.UpdatePortraitDisplay();
        }
        
        // Refresh portrait in visual display by updating pet image
        if (visualDisplay != null)
        {
            // The visual display doesn't have a direct UpdatePortrait method,
            // so we trigger a full visual update which includes the portrait
            visualDisplay.SetPet(currentDisplayedPet);
        }
        
        LogDebug("Pet portraits refreshed");
    }
    
    /// <summary>
    /// Gets the currently displayed pet (for external access)
    /// </summary>
    public NetworkEntity GetCurrentDisplayedPet()
    {
        return currentDisplayedPet;
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
    
    private void LogDebug(string message)
    {
        if (debugLogEnabled)
        {
            Debug.Log($"[OwnPetViewController] {message}");
        }
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[OwnPetViewController] {message}");
    }
} 