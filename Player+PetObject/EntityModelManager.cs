using UnityEngine;
using FishNet.Object;
using System.Collections;
using MVPScripts.Utility;
using CharacterSelection;

/// <summary>
/// Handles runtime model swapping for NetworkEntity objects based on character selection data.
/// Attach to: NetworkEntity prefabs that need dynamic model loading
/// </summary>
public class EntityModelManager : NetworkBehaviour
{
    [Header("Model Setup")]
    [SerializeField] private Transform modelParent; // Where to instantiate the new model
    [SerializeField] private Transform placeholderModel; // The placeholder model to replace
    [SerializeField] private bool autoFindPlaceholder = true; // Automatically find placeholder if not set
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    // References
    private NetworkEntity networkEntity;
    private NetworkEntityUI entityUI;
    private NetworkEntityAnimator entityAnimator;
    private Transform currentModel;
    private bool isModelSetupComplete = false;
    
    // Character selection data references
    private CharacterSelectionManager characterSelectionManager;
    
    public bool IsModelSetupComplete => isModelSetupComplete;
    public Transform CurrentModel => currentModel;
    
    private void Awake()
    {
        // Get required components
        networkEntity = GetComponent<NetworkEntity>();
        entityUI = GetComponent<NetworkEntityUI>();
        entityAnimator = GetComponent<NetworkEntityAnimator>();
        
        // Auto-find placeholder if not set
        if (autoFindPlaceholder && placeholderModel == null)
        {
            placeholderModel = FindPlaceholderModel();
        }
        
        // Set model parent if not specified
        if (modelParent == null)
        {
            modelParent = transform; // Use the NetworkEntity's transform as parent
        }
    }
    
    private Transform FindPlaceholderModel()
    {
        // Look for the existing model referenced in NetworkEntityUI
        if (entityUI != null)
        {
            Transform existingModel = entityUI.GetEntityModel();
            if (existingModel != null)
            {
                return existingModel;
            }
        }
        
        // Look for any child object that might be a model
        foreach (Transform child in transform)
        {
            if (child.name.ToLower().Contains("model") || 
                child.GetComponent<Renderer>() != null ||
                child.GetComponentInChildren<Renderer>() != null)
            {
                return child;
            }
        }
        
        return null;
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        
        if (showDebugLogs)
            Debug.Log($"EntityModelManager: Server started for {networkEntity?.EntityName.Value}");
        
        // Subscribe to selection data changes
        if (networkEntity != null)
        {
            networkEntity.SelectedCharacterIndex.OnChange += OnCharacterSelectionChanged;
            networkEntity.SelectedPetIndex.OnChange += OnPetSelectionChanged;
            networkEntity.CharacterPrefabPath.OnChange += OnCharacterPrefabPathChanged;
            networkEntity.PetPrefabPath.OnChange += OnPetPrefabPathChanged;
        }
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        if (showDebugLogs)
            Debug.Log($"EntityModelManager: Client started for {networkEntity?.EntityName.Value}");
        
        // Subscribe to selection data changes
        if (networkEntity != null)
        {
            networkEntity.SelectedCharacterIndex.OnChange += OnCharacterSelectionChanged;
            networkEntity.SelectedPetIndex.OnChange += OnPetSelectionChanged;
            networkEntity.CharacterPrefabPath.OnChange += OnCharacterPrefabPathChanged;
            networkEntity.PetPrefabPath.OnChange += OnPetPrefabPathChanged;
        }
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Unsubscribe from selection data changes
        if (networkEntity != null)
        {
            networkEntity.SelectedCharacterIndex.OnChange -= OnCharacterSelectionChanged;
            networkEntity.SelectedPetIndex.OnChange -= OnPetSelectionChanged;
            networkEntity.CharacterPrefabPath.OnChange -= OnCharacterPrefabPathChanged;
            networkEntity.PetPrefabPath.OnChange -= OnPetPrefabPathChanged;
        }
    }
    
    public override void OnStopServer()
    {
        base.OnStopServer();
        
        // Unsubscribe from selection data changes
        if (networkEntity != null)
        {
            networkEntity.SelectedCharacterIndex.OnChange -= OnCharacterSelectionChanged;
            networkEntity.SelectedPetIndex.OnChange -= OnPetSelectionChanged;
            networkEntity.CharacterPrefabPath.OnChange -= OnCharacterPrefabPathChanged;
            networkEntity.PetPrefabPath.OnChange -= OnPetPrefabPathChanged;
        }
    }
    
    private void OnCharacterSelectionChanged(int prev, int next, bool asServer)
    {
        if (networkEntity?.EntityType == EntityType.Player && next >= 0)
        {
            if (showDebugLogs)
                Debug.Log($"EntityModelManager: Character selection changed to index {next} for {networkEntity.EntityName.Value}");
            
            CheckAndApplyModelChange();
        }
    }
    
    private void OnPetSelectionChanged(int prev, int next, bool asServer)
    {
        if (networkEntity?.EntityType == EntityType.Pet && next >= 0)
        {
            if (showDebugLogs)
                Debug.Log($"EntityModelManager: Pet selection changed to index {next} for {networkEntity.EntityName.Value}");
            
            CheckAndApplyModelChange();
        }
    }
    
    private void OnCharacterPrefabPathChanged(string prev, string next, bool asServer)
    {
        if (networkEntity?.EntityType == EntityType.Player && !string.IsNullOrEmpty(next))
        {
            if (showDebugLogs)
                Debug.Log($"EntityModelManager: Character prefab path changed to {next} for {networkEntity.EntityName.Value}");
            
            CheckAndApplyModelChange();
        }
    }
    
    private void OnPetPrefabPathChanged(string prev, string next, bool asServer)
    {
        if (networkEntity?.EntityType == EntityType.Pet && !string.IsNullOrEmpty(next))
        {
            if (showDebugLogs)
                Debug.Log($"EntityModelManager: Pet prefab path changed to {next} for {networkEntity.EntityName.Value}");
            
            CheckAndApplyModelChange();
        }
    }
    
    private void CheckAndApplyModelChange()
    {
        if (networkEntity == null || isModelSetupComplete)
            return;
            
        if (networkEntity.HasValidSelection())
        {
            if (showDebugLogs)
                Debug.Log($"EntityModelManager: Applying model change for {networkEntity.EntityName.Value}");
            
            StartCoroutine(ApplyModelChangeCoroutine());
        }
    }
    
    private IEnumerator ApplyModelChangeCoroutine()
    {
        // Wait a frame to ensure all SyncVar updates are processed
        yield return null;
        
        if (networkEntity.EntityType == EntityType.Player)
        {
            yield return StartCoroutine(SetCharacterModelCoroutine(networkEntity.CharacterPrefabPath.Value, networkEntity.SelectedCharacterIndex.Value));
        }
        else if (networkEntity.EntityType == EntityType.Pet)
        {
            yield return StartCoroutine(SetPetModelCoroutine(networkEntity.PetPrefabPath.Value, networkEntity.SelectedPetIndex.Value));
        }
    }
    
    /// <summary>
    /// Sets the character model for this entity
    /// </summary>
    private IEnumerator SetCharacterModelCoroutine(string prefabPath, int characterIndex)
    {
        if (showDebugLogs)
            Debug.Log($"EntityModelManager: Setting character model from path: {prefabPath}");
        
        // Find the character selection manager to get character data
        ComponentResolver.FindComponent(ref characterSelectionManager, gameObject);
        
        if (characterSelectionManager == null)
        {
            Debug.LogError($"EntityModelManager: CharacterSelectionManager not found, cannot load character model");
            yield break;
        }
        
        // Get the character data
        var availableCharacters = characterSelectionManager.GetAvailableCharacters();
        if (characterIndex < 0 || characterIndex >= availableCharacters.Count)
        {
            Debug.LogError($"EntityModelManager: Invalid character index {characterIndex}, available count: {availableCharacters.Count}");
            yield break;
        }
        
        var characterData = availableCharacters[characterIndex];
        if (characterData == null)
        {
            Debug.LogError($"EntityModelManager: CharacterData is null for index {characterIndex}");
            yield break;
        }
        
        // Load the character model from the prefab
        yield return StartCoroutine(LoadModelFromPrefab(characterData.gameObject, "Character"));
    }
    
    /// <summary>
    /// Sets the pet model for this entity
    /// </summary>
    private IEnumerator SetPetModelCoroutine(string prefabPath, int petIndex)
    {
        if (showDebugLogs)
            Debug.Log($"EntityModelManager: Setting pet model from path: {prefabPath}");
        
        // Find the character selection manager to get pet data
        ComponentResolver.FindComponent(ref characterSelectionManager, gameObject);
        
        if (characterSelectionManager == null)
        {
            Debug.LogError($"EntityModelManager: CharacterSelectionManager not found, cannot load pet model");
            yield break;
        }
        
        // Get the pet data
        var availablePets = characterSelectionManager.GetAvailablePets();
        if (petIndex < 0 || petIndex >= availablePets.Count)
        {
            Debug.LogError($"EntityModelManager: Invalid pet index {petIndex}, available count: {availablePets.Count}");
            yield break;
        }
        
        var petData = availablePets[petIndex];
        if (petData == null)
        {
            Debug.LogError($"EntityModelManager: PetData is null for index {petIndex}");
            yield break;
        }
        
        // Load the pet model from the prefab
        yield return StartCoroutine(LoadModelFromPrefab(petData.gameObject, "Pet"));
    }
    
    /// <summary>
    /// Loads a model from a prefab and sets it up
    /// </summary>
    private IEnumerator LoadModelFromPrefab(GameObject prefab, string modelType)
    {
        if (prefab == null)
        {
            Debug.LogError($"EntityModelManager: {modelType} prefab is null");
            yield break;
        }
        
        if (showDebugLogs)
            Debug.Log($"EntityModelManager: Loading {modelType} model from prefab: {prefab.name}");
        
        // Instantiate the model
        GameObject modelInstance = Instantiate(prefab, modelParent.position, modelParent.rotation, modelParent);
        
        if (modelInstance == null)
        {
            Debug.LogError($"EntityModelManager: Failed to instantiate {modelType} model");
            yield break;
        }
        
        // Remove the data components from the instantiated model (we only want the visual parts)
        var characterData = modelInstance.GetComponent<CharacterData>();
        var petData = modelInstance.GetComponent<PetData>();
        var entitySelectionController = modelInstance.GetComponent<EntitySelectionController>();
        var modelOutlineHover = modelInstance.GetComponent<ModelOutlineHover>();
        
        if (characterData != null) DestroyImmediate(characterData);
        if (petData != null) DestroyImmediate(petData);
        if (entitySelectionController != null) DestroyImmediate(entitySelectionController);
        if (modelOutlineHover != null) DestroyImmediate(modelOutlineHover);
        
        // Clean up the old model
        CleanupOldModel();
        
        // Set the new model as current
        currentModel = modelInstance.transform;
        
        // Update component references
        UpdateComponentReferences(currentModel);
        
        isModelSetupComplete = true;
        
        if (showDebugLogs)
            Debug.Log($"EntityModelManager: Successfully loaded {modelType} model: {prefab.name} for {networkEntity?.EntityName.Value}");
        
        yield return null;
    }
    
    /// <summary>
    /// Updates references in other components to point to the new model
    /// </summary>
    private void UpdateComponentReferences(Transform newModel)
    {
        if (newModel == null) return;
        
        // Update NetworkEntityUI reference
        if (entityUI != null)
        {
            entityUI.SetEntityModel(newModel);
            if (showDebugLogs)
                Debug.Log($"EntityModelManager: Updated NetworkEntityUI model reference");
        }
        
        // Update NetworkEntityAnimator references
        if (entityAnimator != null)
        {
            Animator modelAnimator = newModel.GetComponent<Animator>();
            if (modelAnimator == null)
            {
                modelAnimator = newModel.GetComponentInChildren<Animator>();
            }
            
            entityAnimator.SetModelReferences(newModel, modelAnimator);
            if (showDebugLogs)
                Debug.Log($"EntityModelManager: Updated NetworkEntityAnimator references - Animator: {(modelAnimator != null ? modelAnimator.name : "None")}");
        }
    }
    
    /// <summary>
    /// Cleans up the old placeholder model
    /// </summary>
    private void CleanupOldModel()
    {
        if (placeholderModel != null && placeholderModel != currentModel)
        {
            if (showDebugLogs)
                Debug.Log($"EntityModelManager: Cleaning up old placeholder model: {placeholderModel.name}");
            
            if (Application.isPlaying)
                Destroy(placeholderModel.gameObject);
            else
                DestroyImmediate(placeholderModel.gameObject);
                
            placeholderModel = null;
        }
        
        if (currentModel != null && currentModel != placeholderModel)
        {
            if (showDebugLogs)
                Debug.Log($"EntityModelManager: Cleaning up previous current model: {currentModel.name}");
            
            if (Application.isPlaying)
                Destroy(currentModel.gameObject);
            else
                DestroyImmediate(currentModel.gameObject);
        }
    }
    
    /// <summary>
    /// Force applies the model change if selection data is already available
    /// </summary>
    [ContextMenu("Force Apply Model Change")]
    public void ForceApplyModelChange()
    {
        if (networkEntity != null && networkEntity.HasValidSelection())
        {
            isModelSetupComplete = false;
            CheckAndApplyModelChange();
        }
        else
        {
            Debug.LogWarning("EntityModelManager: Cannot force apply model change - no valid selection data");
        }
    }
    
    /// <summary>
    /// Resets the model setup state for testing
    /// </summary>
    [ContextMenu("Reset Model Setup")]
    public void ResetModelSetup()
    {
        isModelSetupComplete = false;
        if (showDebugLogs)
            Debug.Log("EntityModelManager: Model setup state reset");
    }
} 