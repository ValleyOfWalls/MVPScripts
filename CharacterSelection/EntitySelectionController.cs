using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controller for individual character/pet selection prefabs.
/// Handles click interactions via UI buttons or 3D models with colliders, deck preview triggering, and visual selection state.
/// Attach to: Root of character/pet selection item prefabs.
/// </summary>
public class EntitySelectionController : MonoBehaviour
{
    [Header("Selection Type")]
    [SerializeField] private EntityType entityType = EntityType.Character;
    
    [Header("Selection Mode")]
    [SerializeField] private SelectionMode selectionMode = SelectionMode.UI_Button;
    
    [Header("Data References")]
    [SerializeField] private CharacterData characterDataReference;
    [SerializeField] private PetData petDataReference;
    
    [Header("UI Components (Button Mode)")]
    [SerializeField] private Button selectionButton;
    [SerializeField] private PlayerSelectionIndicator selectionIndicator;
    
    [Header("3D Model Components (Model Mode)")]
    [SerializeField] private GameObject model3D;
    [SerializeField] private Collider modelCollider;
    [SerializeField] private MeshRenderer modelRenderer;
    [SerializeField] private MeshFilter modelMeshFilter;
    [SerializeField] private bool useRaycastDetection = true; // Use raycasting vs OnMouseDown
    
    [Header("3D Model Settings")]
    [SerializeField] private LayerMask raycastLayerMask = -1;
    [SerializeField] private float maxRaycastDistance = 100f;
    [SerializeField] private Camera raycastCamera; // If null, will use Camera.main
    
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
    private bool model3DCreated = false; // Track if we created the 3D model dynamically
    
    public enum EntityType
    {
        Character,
        Pet
    }
    
    public enum SelectionMode
    {
        UI_Button,
        Model_3D,
        Both // Support both input methods
    }
    
    #region Initialization
    
    private void Awake()
    {
        // Find required components on this prefab
        FindRequiredComponents();
    }
    
    private void FindRequiredComponents()
    {
        // Find UI components
        if (selectionButton == null)
            selectionButton = GetComponent<Button>();
        
        if (selectionIndicator == null)
            selectionIndicator = GetComponentInChildren<PlayerSelectionIndicator>();
        
        // Find 3D model components
        if (model3D == null)
            model3D = transform.Find("Model3D")?.gameObject;
        
        if (model3D != null)
        {
            if (modelCollider == null)
                modelCollider = model3D.GetComponent<Collider>();
            if (modelRenderer == null)
                modelRenderer = model3D.GetComponent<MeshRenderer>();
            if (modelMeshFilter == null)
                modelMeshFilter = model3D.GetComponent<MeshFilter>();
        }
        
        // Set up input listeners based on selection mode
        SetupInputListeners();
    }
    
    private void SetupInputListeners()
    {
        // Set up button listener if using UI button mode
        if ((selectionMode == SelectionMode.UI_Button || selectionMode == SelectionMode.Both) && selectionButton != null)
        {
            selectionButton.onClick.RemoveAllListeners();
            selectionButton.onClick.AddListener(OnSelectionClicked);
        }
        
        // Set up 3D model listeners if using 3D model mode
        if ((selectionMode == SelectionMode.Model_3D || selectionMode == SelectionMode.Both) && model3D != null)
        {
            // If not using raycast detection, set up OnMouseDown detection
            if (!useRaycastDetection)
            {
                // Ensure the model has a collider for OnMouseDown to work
                if (modelCollider == null)
                {
                    Debug.LogWarning($"EntitySelectionController: 3D model on {gameObject.name} needs a Collider for OnMouseDown detection");
                }
            }
        }
        
        // Validate setup
        ValidateSetup();
    }
    
    private void ValidateSetup()
    {
        bool hasValidInput = false;
        
        if (selectionMode == SelectionMode.UI_Button || selectionMode == SelectionMode.Both)
        {
            if (selectionButton != null)
            {
                hasValidInput = true;
            }
            else
            {
                Debug.LogError($"EntitySelectionController: UI_Button mode selected but no Button component found on {gameObject.name}");
            }
        }
        
        if (selectionMode == SelectionMode.Model_3D || selectionMode == SelectionMode.Both)
        {
            if (model3D != null && modelCollider != null)
            {
                hasValidInput = true;
            }
            else
            {
                Debug.LogError($"EntitySelectionController: Model_3D mode selected but 3D model or collider not found on {gameObject.name}");
            }
        }
        
        if (!hasValidInput)
        {
            Debug.LogError($"EntitySelectionController: No valid input method configured on {gameObject.name}");
        }
    }
    
    /// <summary>
    /// Creates a 3D model from character/pet data if one doesn't exist
    /// Uses the auto-discovered visual components from the data
    /// </summary>
    private void Create3DModelFromData()
    {
        if (model3D != null) return; // Already have a model
        
        Mesh meshToUse = null;
        Material materialToUse = null;
        
        // Get mesh and material from the auto-discovered components
        if (entityType == EntityType.Character && characterData != null)
        {
            meshToUse = characterData.CharacterMesh;
            materialToUse = characterData.CharacterMaterial;
        }
        else if (entityType == EntityType.Pet && petData != null)
        {
            meshToUse = petData.PetMesh;
            materialToUse = petData.PetMaterial;
        }
        
        if (meshToUse != null)
        {
            // Create the 3D model GameObject
            model3D = new GameObject("Model3D");
            model3D.transform.SetParent(transform, false);
            
            // Add MeshFilter and MeshRenderer
            modelMeshFilter = model3D.AddComponent<MeshFilter>();
            modelMeshFilter.mesh = meshToUse;
            
            modelRenderer = model3D.AddComponent<MeshRenderer>();
            if (materialToUse != null)
            {
                modelRenderer.material = materialToUse;
            }
            
            // Add a collider for interaction
            MeshCollider meshCollider = model3D.AddComponent<MeshCollider>();
            meshCollider.convex = true; // Allow for trigger and rigidbody interactions
            modelCollider = meshCollider;
            
            model3DCreated = true;
            
            Debug.Log($"EntitySelectionController: Created 3D model for {GetDisplayName()} from auto-discovered data");
        }
        else
        {
            Debug.LogWarning($"EntitySelectionController: Cannot create 3D model - no mesh data available for {GetDisplayName()}. Make sure your prefab has a MeshFilter component in its hierarchy.");
        }
    }
    
    /// <summary>
    /// Initialize this controller with character data
    /// </summary>
    public void InitializeWithCharacter(CharacterData data, int index, CharacterSelectionUIManager uiMgr, DeckPreviewController deckController, CharacterSelectionManager selMgr)
    {
        // Use runtime data if provided, otherwise try to find it on this GameObject
        if (data != null)
        {
            characterData = data;
        }
        else
        {
            // Try to find CharacterData on this GameObject (prefab-based approach)
            characterData = GetComponent<CharacterData>();
            if (characterData == null)
            {
                characterData = characterDataReference; // Final fallback
            }
        }
        
        selectionIndex = index;
        entityType = EntityType.Character;
        
        // Update serialized reference if runtime data was provided
        if (characterData != null && characterDataReference != characterData)
        {
            characterDataReference = characterData;
        }
        
        SetDependencies(uiMgr, deckController, selMgr);
        
        // Create 3D model if needed and in 3D mode (only if we don't already have a model)
        if ((selectionMode == SelectionMode.Model_3D || selectionMode == SelectionMode.Both) && model3D == null)
        {
            Create3DModelFromData();
        }
        
        ValidateInitialization();
        isInitialized = true;
        
        Debug.Log($"EntitySelectionController: Initialized as Character controller for {GetDisplayName()} at index {index}");
    }
    
    /// <summary>
    /// Initialize this controller with pet data
    /// </summary>
    public void InitializeWithPet(PetData data, int index, CharacterSelectionUIManager uiMgr, DeckPreviewController deckController, CharacterSelectionManager selMgr)
    {
        // Use runtime data if provided, otherwise try to find it on this GameObject
        if (data != null)
        {
            petData = data;
        }
        else
        {
            // Try to find PetData on this GameObject (prefab-based approach)
            petData = GetComponent<PetData>();
            if (petData == null)
            {
                petData = petDataReference; // Final fallback
            }
        }
        
        selectionIndex = index;
        entityType = EntityType.Pet;
        
        // Update serialized reference if runtime data was provided
        if (petData != null && petDataReference != petData)
        {
            petDataReference = petData;
        }
        
        SetDependencies(uiMgr, deckController, selMgr);
        
        // Create 3D model if needed and in 3D mode (only if we don't already have a model)
        if ((selectionMode == SelectionMode.Model_3D || selectionMode == SelectionMode.Both) && model3D == null)
        {
            Create3DModelFromData();
        }
        
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
    /// Initialize using only the serialized references or components on this GameObject (useful for testing or standalone usage)
    /// </summary>
    public void InitializeFromSerializedReferences(int index, CharacterSelectionUIManager uiMgr, DeckPreviewController deckController, CharacterSelectionManager selMgr)
    {
        selectionIndex = index;
        
        if (entityType == EntityType.Character)
        {
            // Try to get CharacterData from this GameObject first, then fall back to serialized reference
            characterData = GetComponent<CharacterData>() ?? characterDataReference;
        }
        else
        {
            // Try to get PetData from this GameObject first, then fall back to serialized reference
            petData = GetComponent<PetData>() ?? petDataReference;
        }
        
        SetDependencies(uiMgr, deckController, selMgr);
        
        // Create 3D model if needed and in 3D mode (only if we don't already have a model)
        if ((selectionMode == SelectionMode.Model_3D || selectionMode == SelectionMode.Both) && model3D == null)
        {
            Create3DModelFromData();
        }
        
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
    
    #region Unity Update and Input Detection
    
    private void Update()
    {
        // Handle raycast-based click detection for 3D models
        if ((selectionMode == SelectionMode.Model_3D || selectionMode == SelectionMode.Both) && useRaycastDetection)
        {
            HandleRaycastInput();
        }
    }
    
    private void HandleRaycastInput()
    {
        if (!Input.GetMouseButtonDown(0)) return; // Only check on left mouse button down
        
        Camera cameraToUse = raycastCamera != null ? raycastCamera : Camera.main;
        if (cameraToUse == null) return;
        
        Ray ray = cameraToUse.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, raycastLayerMask))
        {
            // Check if we hit our model or any of its children
            if (IsHitOurModel(hit.collider))
            {
                OnModel3DClicked();
            }
        }
    }
    
    private bool IsHitOurModel(Collider hitCollider)
    {
        if (model3D == null) return false;
        
        // Check if the hit collider is our model collider
        if (hitCollider == modelCollider) return true;
        
        // Check if the hit collider is a child of our model
        Transform current = hitCollider.transform;
        while (current != null)
        {
            if (current.gameObject == model3D) return true;
            current = current.parent;
        }
        
        return false;
    }
    
    // Unity's OnMouseDown method for non-raycast detection
    private void OnMouseDown()
    {
        // Only respond if we're using OnMouseDown detection and this is a 3D model
        if ((selectionMode == SelectionMode.Model_3D || selectionMode == SelectionMode.Both) && !useRaycastDetection)
        {
            OnModel3DClicked();
        }
    }
    
    private void OnModel3DClicked()
    {
        if (!isInitialized)
        {
            Debug.LogWarning($"EntitySelectionController: 3D model click ignored - controller not initialized on {gameObject.name}");
            return;
        }
        
        Debug.Log($"EntitySelectionController: 3D model clicked - Type: {entityType}, Index: {selectionIndex}");
        
        // Call the same selection handling logic as button clicks
        HandleSelection();
    }
    
    #endregion
    
    #region Selection Handling
    
    private void OnSelectionClicked()
    {
        if (!isInitialized)
        {
            Debug.LogWarning($"EntitySelectionController: Button click ignored - controller not initialized on {gameObject.name}");
            return;
        }
        
        Debug.Log($"EntitySelectionController: Button clicked - Type: {entityType}, Index: {selectionIndex}");
        
        // Call the common selection handling logic
        HandleSelection();
    }
    
    /// <summary>
    /// Common selection handling method called by both button clicks and 3D model clicks
    /// </summary>
    private void HandleSelection()
    {
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
    
    /// <summary>
    /// Set the selection mode and refresh input listeners
    /// </summary>
    public void SetSelectionMode(SelectionMode mode)
    {
        selectionMode = mode;
        SetupInputListeners();
        
        // Create or remove 3D model as needed
        if ((mode == SelectionMode.Model_3D || mode == SelectionMode.Both) && model3D == null)
        {
            Create3DModelFromData();
        }
    }
    
    /// <summary>
    /// Get the current selection mode
    /// </summary>
    public SelectionMode GetSelectionMode()
    {
        return selectionMode;
    }
    
    /// <summary>
    /// Get the 3D model GameObject (if any)
    /// </summary>
    public GameObject GetModel3D()
    {
        return model3D;
    }
    
    /// <summary>
    /// Set the 3D model GameObject manually
    /// </summary>
    public void SetModel3D(GameObject modelObject)
    {
        model3D = modelObject;
        
        if (model3D != null)
        {
            // Update component references
            modelCollider = model3D.GetComponent<Collider>();
            modelRenderer = model3D.GetComponent<MeshRenderer>();
            modelMeshFilter = model3D.GetComponent<MeshFilter>();
            
            // Set as child if not already
            if (model3D.transform.parent != transform)
            {
                model3D.transform.SetParent(transform, false);
            }
        }
        
        SetupInputListeners();
    }
    
    /// <summary>
    /// Refresh the 3D model from current character/pet data
    /// </summary>
    public void RefreshModel3DFromData()
    {
        if (model3DCreated && model3D != null)
        {
            // Destroy existing created model
            if (Application.isPlaying)
                Destroy(model3D);
            else
                DestroyImmediate(model3D);
                
            model3D = null;
            model3DCreated = false;
        }
        
        Create3DModelFromData();
    }
    
    /// <summary>
    /// Check if this controller is using 3D model input
    /// </summary>
    public bool IsUsing3DModel()
    {
        return selectionMode == SelectionMode.Model_3D || selectionMode == SelectionMode.Both;
    }
    
    /// <summary>
    /// Check if this controller is using button input
    /// </summary>
    public bool IsUsingButton()
    {
        return selectionMode == SelectionMode.UI_Button || selectionMode == SelectionMode.Both;
    }
    
    /// <summary>
    /// Set the camera used for raycast detection
    /// </summary>
    public void SetRaycastCamera(Camera camera)
    {
        raycastCamera = camera;
    }
    
    /// <summary>
    /// Get the current raycast camera
    /// </summary>
    public Camera GetRaycastCamera()
    {
        return raycastCamera;
    }
    
    private void OnDestroy()
    {
        // Clean up dynamically created 3D model
        if (model3DCreated && model3D != null)
        {
            if (Application.isPlaying)
                Destroy(model3D);
            else
                DestroyImmediate(model3D);
        }
    }
    
    #endregion
} 