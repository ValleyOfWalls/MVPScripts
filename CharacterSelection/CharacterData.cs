using UnityEngine;

/// <summary>
/// MonoBehaviour component that defines a character archetype with their unique starter deck and appearance.
/// Attach to: Root of character prefabs used in character selection.
/// </summary>
public class CharacterData : MonoBehaviour
{
    [Header("Basic Info")]
    [SerializeField] private string characterName = "Unknown Character";
    [SerializeField] private Sprite characterPortrait;
    [SerializeField, TextArea(3, 6)] private string characterDescription = "A mysterious character...";
    
    [Header("Gameplay")]
    [SerializeField] private DeckData starterDeck;
    
    [Header("Base Stats")]
    [SerializeField] private int baseHealth = 100;
    [SerializeField] private int baseEnergy = 3;
    [SerializeField] private int startingCurrency = 20;
    
    [Header("Auto-Discovered Visual Components (Read-Only)")]
    [SerializeField, ReadOnly] private MeshRenderer discoveredMeshRenderer;
    [SerializeField, ReadOnly] private SkinnedMeshRenderer discoveredSkinnedMeshRenderer;
    [SerializeField, ReadOnly] private MeshFilter discoveredMeshFilter;
    [SerializeField, ReadOnly] private Collider discoveredCollider;
    [SerializeField, ReadOnly] private Animator discoveredAnimator;
    
    // Cached data
    private Mesh cachedMesh;
    private Material cachedMaterial;
    private RuntimeAnimatorController cachedAnimatorController;
    private Color cachedTint = Color.white;
    private bool hasDiscovered = false;
    
    // Public accessors
    public string CharacterName => characterName;
    public Sprite CharacterPortrait => characterPortrait;
    public string CharacterDescription => characterDescription;
    public DeckData StarterDeck => starterDeck;
    public int BaseHealth => baseHealth;
    public int BaseEnergy => baseEnergy;
    public int StartingCurrency => startingCurrency;
    
    // Auto-discovered visual properties
    public Mesh CharacterMesh => GetDiscoveredMesh();
    public Material CharacterMaterial => GetDiscoveredMaterial();
    public MeshRenderer CharacterMeshRenderer => GetDiscoveredMeshRenderer();
    public SkinnedMeshRenderer CharacterSkinnedMeshRenderer => GetDiscoveredSkinnedMeshRenderer();
    public Collider CharacterCollider => GetDiscoveredCollider();
    public Color CharacterTint => GetDiscoveredTint();
    public RuntimeAnimatorController CharacterAnimatorController => GetDiscoveredAnimatorController();

    private void Awake()
    {
        DiscoverVisualComponents();
    }
    
    private void Start()
    {
        // Ensure discovery happens even if Awake was missed
        if (!hasDiscovered)
        {
            DiscoverVisualComponents();
        }
    }
    
    private void OnValidate()
    {
        // Discover components in editor when values change
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DiscoverVisualComponents();
        }
        #endif
    }
    
    /// <summary>
    /// Automatically discovers visual components in the prefab hierarchy
    /// </summary>
    [ContextMenu("Discover Visual Components")]
    public void DiscoverVisualComponents()
    {
        // Search in children first, then self
        discoveredMeshRenderer = GetComponentInChildren<MeshRenderer>();
        discoveredSkinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        discoveredMeshFilter = GetComponentInChildren<MeshFilter>();
        discoveredCollider = GetComponentInChildren<Collider>();
        discoveredAnimator = GetComponentInChildren<Animator>();
        
        // Cache the mesh and material - prioritize SkinnedMeshRenderer for animated characters
        if (discoveredSkinnedMeshRenderer != null)
        {
            cachedMesh = discoveredSkinnedMeshRenderer.sharedMesh;
            cachedMaterial = discoveredSkinnedMeshRenderer.sharedMaterial;
        }
        else if (discoveredMeshFilter != null)
        {
            cachedMesh = discoveredMeshFilter.sharedMesh;
            cachedMaterial = discoveredMeshRenderer?.sharedMaterial;
        }
        else
        {
            cachedMesh = null;
            cachedMaterial = discoveredMeshRenderer?.sharedMaterial;
        }
        
        cachedAnimatorController = discoveredAnimator?.runtimeAnimatorController;
        
        // Auto-create and fit BoxCollider if SkinnedMeshRenderer exists
        SetupBoxColliderForSkinnedMesh();
        
        // Get tint from material if available
        if (cachedMaterial != null)
        {
            // Try common material color properties
            if (cachedMaterial.HasProperty("_Color"))
                cachedTint = cachedMaterial.GetColor("_Color");
            else if (cachedMaterial.HasProperty("_BaseColor"))
                cachedTint = cachedMaterial.GetColor("_BaseColor");
            else if (cachedMaterial.HasProperty("_MainColor"))
                cachedTint = cachedMaterial.GetColor("_MainColor");
            else
                cachedTint = Color.white;
        }
        else
        {
            cachedTint = Color.white;
        }
        
        hasDiscovered = true;
        
        Debug.Log($"CharacterData: Discovered components for {characterName}:" +
                  $"\n- MeshRenderer: {(discoveredMeshRenderer != null ? discoveredMeshRenderer.name : "None")}" +
                  $"\n- SkinnedMeshRenderer: {(discoveredSkinnedMeshRenderer != null ? discoveredSkinnedMeshRenderer.name : "None")}" +
                  $"\n- MeshFilter: {(discoveredMeshFilter != null ? discoveredMeshFilter.name : "None")}" +
                  $"\n- Collider: {(discoveredCollider != null ? discoveredCollider.name : "None")}" +
                  $"\n- Animator: {(discoveredAnimator != null ? discoveredAnimator.name : "None")}" +
                  $"\n- Mesh: {(cachedMesh != null ? cachedMesh.name : "None")}" +
                  $"\n- Material: {(cachedMaterial != null ? cachedMaterial.name : "None")}" +
                  $"\n- Tint: {cachedTint}" +
                  $"\n- AnimatorController: {(cachedAnimatorController != null ? cachedAnimatorController.name : "None")}");
    }
    
    private Mesh GetDiscoveredMesh()
    {
        if (!hasDiscovered) DiscoverVisualComponents();
        return cachedMesh;
    }
    
    private Material GetDiscoveredMaterial()
    {
        if (!hasDiscovered) DiscoverVisualComponents();
        return cachedMaterial;
    }
    
    private MeshRenderer GetDiscoveredMeshRenderer()
    {
        if (!hasDiscovered) DiscoverVisualComponents();
        return discoveredMeshRenderer;
    }
    
    private Collider GetDiscoveredCollider()
    {
        if (!hasDiscovered) DiscoverVisualComponents();
        return discoveredCollider;
    }
    
    private Color GetDiscoveredTint()
    {
        if (!hasDiscovered) DiscoverVisualComponents();
        return cachedTint;
    }
    
    private RuntimeAnimatorController GetDiscoveredAnimatorController()
    {
        if (!hasDiscovered) DiscoverVisualComponents();
        return cachedAnimatorController;
    }
    
    private SkinnedMeshRenderer GetDiscoveredSkinnedMeshRenderer()
    {
        if (!hasDiscovered) DiscoverVisualComponents();
        return discoveredSkinnedMeshRenderer;
    }
    
    /// <summary>
    /// Auto-creates and fits a BoxCollider based on SkinnedMeshRenderer bounds
    /// </summary>
    private void SetupBoxColliderForSkinnedMesh()
    {
        if (discoveredSkinnedMeshRenderer == null) return;
        
        // Check if we already have a collider
        if (discoveredCollider != null) 
        {
            // If it's a BoxCollider, update its bounds to match the SkinnedMeshRenderer
            if (discoveredCollider is BoxCollider boxCollider)
            {
                FitBoxColliderToSkinnedMesh(boxCollider, discoveredSkinnedMeshRenderer);
                Debug.Log($"CharacterData: Updated existing BoxCollider bounds for {characterName}");
            }
            return;
        }
        
        // No collider exists, create a new BoxCollider
        BoxCollider newBoxCollider = gameObject.AddComponent<BoxCollider>();
        FitBoxColliderToSkinnedMesh(newBoxCollider, discoveredSkinnedMeshRenderer);
        
        // Update our discovered collider reference
        discoveredCollider = newBoxCollider;
        
        /* Debug.Log($"CharacterData: Created and fitted new BoxCollider for {characterName}"); */
    }
    
    /// <summary>
    /// Fits a BoxCollider to match the bounds of a SkinnedMeshRenderer
    /// </summary>
    private void FitBoxColliderToSkinnedMesh(BoxCollider boxCollider, SkinnedMeshRenderer skinnedMeshRenderer)
    {
        if (skinnedMeshRenderer.sharedMesh == null) return;
        
        // Get the bounds of the mesh in local space
        Bounds meshBounds = skinnedMeshRenderer.sharedMesh.bounds;
        
        // Transform the bounds to account for the SkinnedMeshRenderer's local transform
        Transform smrTransform = skinnedMeshRenderer.transform;
        Transform colliderTransform = boxCollider.transform;
        
        // Calculate relative transform
        Vector3 relativePosition = colliderTransform.InverseTransformPoint(smrTransform.TransformPoint(meshBounds.center));
        Vector3 relativeScale = new Vector3(
            meshBounds.size.x * smrTransform.lossyScale.x / colliderTransform.lossyScale.x,
            meshBounds.size.y * smrTransform.lossyScale.y / colliderTransform.lossyScale.y,
            meshBounds.size.z * smrTransform.lossyScale.z / colliderTransform.lossyScale.z
        );
        
        // Apply to BoxCollider
        boxCollider.center = relativePosition;
        boxCollider.size = relativeScale;
        
        // Make it a trigger for selection purposes
        boxCollider.isTrigger = true;
    }
    
    /// <summary>
    /// Validates that this character data is properly configured
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(characterName)) return false;
        if (characterPortrait == null) return false;
        if (starterDeck == null) return false;
        if (baseHealth <= 0) return false;
        if (baseEnergy <= 0) return false;
        
        // Ensure visual components are discovered
        if (!hasDiscovered) DiscoverVisualComponents();
        
        // Validate that we found essential visual components
        if (discoveredMeshRenderer == null)
        {
            Debug.LogWarning($"CharacterData: No MeshRenderer found in prefab hierarchy for {characterName}");
        }
        if (discoveredCollider == null)
        {
            Debug.LogWarning($"CharacterData: No Collider found in prefab hierarchy for {characterName} - needed for 3D selection");
        }
        
        return true;
    }
}

/// <summary>
/// Defines the general archetype/role of a character
/// </summary>
public enum CharacterArchetype
{
    Aggressive,    // High damage, lower health
    Defensive,     // High health, defensive abilities
    Balanced,      // Balanced stats and abilities
    Support,       // Focuses on helping pets and allies
    Controller     // Focuses on battlefield control
} 