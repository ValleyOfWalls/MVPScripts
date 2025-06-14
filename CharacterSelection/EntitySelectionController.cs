using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controller for individual character/pet selection prefabs.
/// Handles click interactions, deck preview triggering, and visual selection state.
/// Attach to: Root of character/pet selection item prefabs.
/// </summary>
public class EntitySelectionController : MonoBehaviour
{
    [Header("Selection Type")]
    [SerializeField] private EntityType entityType = EntityType.Character;
    
    [Header("Data References")]
    [SerializeField] private CharacterData characterDataReference;
    [SerializeField] private PetData petDataReference;
    
    [Header("Visual Components")]
    [SerializeField] private Button selectionButton;
    [SerializeField] private PlayerSelectionIndicator selectionIndicator;
    
    // Runtime data (set by CharacterSelectionManager, but can fall back to serialized references)
    private CharacterData characterData;
    private PetData petData;
    private int selectionIndex = -1;
    
    // Dependencies (found at runtime)
    private CharacterSelectionUIManager uiManager;
    private DeckPreviewController deckPreviewController;
    private CharacterSelectionManager selectionManager;
    
    // State
    private bool isInitialized = false;
    
    public enum EntityType
    {
        Character,
        Pet
    }
    
    #region Initialization
    
    private void Awake()
    {
        // Find required components on this prefab
        FindRequiredComponents();
    }
    
    private void FindRequiredComponents()
    {
        if (selectionButton == null)
            selectionButton = GetComponent<Button>();
        
        if (selectionIndicator == null)
            selectionIndicator = GetComponentInChildren<PlayerSelectionIndicator>();
            
        // Set up button click listener
        if (selectionButton != null)
        {
            selectionButton.onClick.RemoveAllListeners();
            selectionButton.onClick.AddListener(OnSelectionClicked);
        }
        else
        {
            Debug.LogError($"EntitySelectionController: No Button component found on {gameObject.name}");
        }
    }
    
    /// <summary>
    /// Initialize this controller with character data
    /// </summary>
    public void InitializeWithCharacter(CharacterData data, int index, CharacterSelectionUIManager uiMgr, DeckPreviewController deckController, CharacterSelectionManager selMgr)
    {
        // Use runtime data if provided, otherwise fall back to serialized reference
        characterData = data ?? characterDataReference;
        selectionIndex = index;
        entityType = EntityType.Character;
        
        // Update serialized reference if runtime data was provided
        if (data != null && characterDataReference != data)
        {
            characterDataReference = data;
        }
        
        SetDependencies(uiMgr, deckController, selMgr);
        ValidateInitialization();
        isInitialized = true;
        
        Debug.Log($"EntitySelectionController: Initialized as Character controller for {GetDisplayName()} at index {index}");
    }
    
    /// <summary>
    /// Initialize this controller with pet data
    /// </summary>
    public void InitializeWithPet(PetData data, int index, CharacterSelectionUIManager uiMgr, DeckPreviewController deckController, CharacterSelectionManager selMgr)
    {
        // Use runtime data if provided, otherwise fall back to serialized reference
        petData = data ?? petDataReference;
        selectionIndex = index;
        entityType = EntityType.Pet;
        
        // Update serialized reference if runtime data was provided
        if (data != null && petDataReference != data)
        {
            petDataReference = data;
        }
        
        SetDependencies(uiMgr, deckController, selMgr);
        ValidateInitialization();
        isInitialized = true;
        
        Debug.Log($"EntitySelectionController: Initialized as Pet controller for {GetDisplayName()} at index {index}");
    }
    
    private void SetDependencies(CharacterSelectionUIManager uiMgr, DeckPreviewController deckController, CharacterSelectionManager selMgr)
    {
        uiManager = uiMgr;
        deckPreviewController = deckController;
        selectionManager = selMgr;
    }
    
    /// <summary>
    /// Initialize using only the serialized references (useful for testing or standalone usage)
    /// </summary>
    public void InitializeFromSerializedReferences(int index, CharacterSelectionUIManager uiMgr, DeckPreviewController deckController, CharacterSelectionManager selMgr)
    {
        selectionIndex = index;
        
        if (entityType == EntityType.Character)
        {
            characterData = characterDataReference;
        }
        else
        {
            petData = petDataReference;
        }
        
        SetDependencies(uiMgr, deckController, selMgr);
        ValidateInitialization();
        isInitialized = true;
        
        Debug.Log($"EntitySelectionController: Initialized from serialized references as {entityType} controller for {GetDisplayName()} at index {index}");
    }
    
    /// <summary>
    /// Validates that the controller has valid data for its entity type
    /// </summary>
    private void ValidateInitialization()
    {
        if (entityType == EntityType.Character)
        {
            if (characterData == null)
            {
                Debug.LogError($"EntitySelectionController: Character controller on {gameObject.name} has no CharacterData assigned!");
            }
            else if (!characterData.IsValid())
            {
                Debug.LogWarning($"EntitySelectionController: Character controller on {gameObject.name} has invalid CharacterData: {characterData.name}");
            }
        }
        else if (entityType == EntityType.Pet)
        {
            if (petData == null)
            {
                Debug.LogError($"EntitySelectionController: Pet controller on {gameObject.name} has no PetData assigned!");
            }
            else if (!petData.IsValid())
            {
                Debug.LogWarning($"EntitySelectionController: Pet controller on {gameObject.name} has invalid PetData: {petData.name}");
            }
        }
    }
    
    #endregion
    
    #region Selection Handling
    
    private void OnSelectionClicked()
    {
        if (!isInitialized)
        {
            Debug.LogWarning($"EntitySelectionController: Click ignored - controller not initialized on {gameObject.name}");
            return;
        }
        
        Debug.Log($"EntitySelectionController: Selection clicked - Type: {entityType}, Index: {selectionIndex}");
        
        if (entityType == EntityType.Character)
        {
            HandleCharacterSelection();
        }
        else
        {
            HandlePetSelection();
        }
    }
    
    private void HandleCharacterSelection()
    {
        if (characterData == null)
        {
            Debug.LogError("EntitySelectionController: Character data is null");
            return;
        }
        
        Debug.Log($"EntitySelectionController: Handling character selection for {characterData.CharacterName}");
        
        // Notify UI manager about selection change
        if (uiManager != null)
        {
            uiManager.OnCharacterSelectionChanged(selectionIndex);
        }
        
        // Trigger deck preview
        if (deckPreviewController != null)
        {
            bool isPlayerReady = uiManager?.IsPlayerReady ?? false;
            deckPreviewController.SetCurrentCharacterIndex(selectionIndex);
            deckPreviewController.ShowCharacterDeck(selectionIndex, isPlayerReady);
        }
        
        // Update selection with manager
        if (selectionManager != null && uiManager != null)
        {
            uiManager.UpdateSelectionFromController(EntityType.Character, selectionIndex);
        }
    }
    
    private void HandlePetSelection()
    {
        if (petData == null)
        {
            Debug.LogError("EntitySelectionController: Pet data is null");
            return;
        }
        
        Debug.Log($"EntitySelectionController: Handling pet selection for {petData.PetName}");
        
        // Notify UI manager about selection change
        if (uiManager != null)
        {
            uiManager.OnPetSelectionChanged(selectionIndex);
        }
        
        // Trigger deck preview
        if (deckPreviewController != null)
        {
            bool isPlayerReady = uiManager?.IsPlayerReady ?? false;
            deckPreviewController.SetCurrentPetIndex(selectionIndex);
            deckPreviewController.ShowPetDeck(selectionIndex, isPlayerReady);
        }
        
        // Update selection with manager
        if (selectionManager != null && uiManager != null)
        {
            uiManager.UpdateSelectionFromController(EntityType.Pet, selectionIndex);
        }
    }
    
    #endregion
    
    #region Visual Selection Management
    
    /// <summary>
    /// Add a player's selection indicator to this item
    /// </summary>
    public void AddPlayerSelection(string playerID, Color playerColor)
    {
        if (selectionIndicator != null)
        {
            selectionIndicator.AddPlayerSelection(playerID, playerColor);
        }
    }
    
    /// <summary>
    /// Remove a player's selection indicator from this item
    /// </summary>
    public void RemovePlayerSelection(string playerID)
    {
        if (selectionIndicator != null)
        {
            selectionIndicator.RemovePlayerSelection(playerID);
        }
    }
    
    /// <summary>
    /// Clear all player selection indicators except the specified one
    /// </summary>
    public void ClearAllPlayerSelectionsExcept(string playerID)
    {
        if (selectionIndicator != null)
        {
            selectionIndicator.ClearAllExcept(playerID);
        }
    }
    
    /// <summary>
    /// Clear all player selection indicators
    /// </summary>
    public void ClearAllPlayerSelections()
    {
        if (selectionIndicator != null)
        {
            selectionIndicator.ClearAll();
        }
    }
    
    #endregion
    
    #region Public Interface
    
    /// <summary>
    /// Get the associated deck data for this selection
    /// </summary>
    public DeckData GetAssociatedDeck()
    {
        if (entityType == EntityType.Character && characterData != null)
        {
            return characterData.StarterDeck;
        }
        else if (entityType == EntityType.Pet && petData != null)
        {
            return petData.StarterDeck;
        }
        
        return null;
    }
    
    /// <summary>
    /// Get the display name for this selection
    /// </summary>
    public string GetDisplayName()
    {
        if (entityType == EntityType.Character && characterData != null)
        {
            return characterData.CharacterName;
        }
        else if (entityType == EntityType.Pet && petData != null)
        {
            return petData.PetName;
        }
        
        return "Unknown";
    }
    
    /// <summary>
    /// Get the portrait sprite for this selection
    /// </summary>
    public Sprite GetPortraitSprite()
    {
        if (entityType == EntityType.Character && characterData != null)
        {
            return characterData.CharacterPortrait;
        }
        else if (entityType == EntityType.Pet && petData != null)
        {
            return petData.PetPortrait;
        }
        
        return null;
    }
    
    /// <summary>
    /// Get the description for this selection
    /// </summary>
    public string GetDescription()
    {
        if (entityType == EntityType.Character && characterData != null)
        {
            return characterData.CharacterDescription;
        }
        else if (entityType == EntityType.Pet && petData != null)
        {
            return petData.PetDescription;
        }
        
        return "";
    }
    
    /// <summary>
    /// Get the selection index
    /// </summary>
    public int GetSelectionIndex()
    {
        return selectionIndex;
    }
    
    /// <summary>
    /// Get the entity type
    /// </summary>
    public EntityType GetEntityType()
    {
        return entityType;
    }
    
    /// <summary>
    /// Check if this controller is properly initialized
    /// </summary>
    public bool IsInitialized()
    {
        return isInitialized;
    }
    
    /// <summary>
    /// Get the serialized character data reference (for inspector visibility)
    /// </summary>
    public CharacterData GetSerializedCharacterData()
    {
        return characterDataReference;
    }
    
    /// <summary>
    /// Get the serialized pet data reference (for inspector visibility)
    /// </summary>
    public PetData GetSerializedPetData()
    {
        return petDataReference;
    }
    
    /// <summary>
    /// Set the serialized character data reference (for editor scripting)
    /// </summary>
    public void SetSerializedCharacterData(CharacterData data)
    {
        characterDataReference = data;
        if (isInitialized && entityType == EntityType.Character)
        {
            characterData = data;
        }
    }
    
    /// <summary>
    /// Set the serialized pet data reference (for editor scripting)
    /// </summary>
    public void SetSerializedPetData(PetData data)
    {
        petDataReference = data;
        if (isInitialized && entityType == EntityType.Pet)
        {
            petData = data;
        }
    }
    
    #endregion
} 