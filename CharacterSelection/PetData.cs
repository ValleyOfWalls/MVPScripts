using UnityEngine;

/// <summary>
/// MonoBehaviour component that defines a pet type with their unique starter deck and appearance.
/// Attach to: Root of pet prefabs used in character selection.
/// </summary>
public class PetData : MonoBehaviour
{
    [Header("Basic Info")]
    [SerializeField] private string petName = "Unknown Pet";
    [SerializeField] private Sprite petPortrait;
    [SerializeField, TextArea(3, 6)] private string petDescription = "A mysterious companion...";
    
    [Header("Gameplay")]
    [SerializeField] private DeckData starterDeck;
    
    [Header("Base Stats")]
    [SerializeField] private int baseHealth = 50;
    [SerializeField] private int baseEnergy = 2;
    
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
    public string PetName => petName;
    public Sprite PetPortrait => petPortrait;
    public string PetDescription => petDescription;
    public DeckData StarterDeck => starterDeck;
    public int BaseHealth => baseHealth;
    public int BaseEnergy => baseEnergy;
    
    // Auto-discovered visual properties
    public Mesh PetMesh => GetDiscoveredMesh();
    public Material PetMaterial => GetDiscoveredMaterial();
    public MeshRenderer PetMeshRenderer => GetDiscoveredMeshRenderer();
    public SkinnedMeshRenderer PetSkinnedMeshRenderer => GetDiscoveredSkinnedMeshRenderer();
    public Collider PetCollider => GetDiscoveredCollider();
    public Color PetTint => GetDiscoveredTint();
    public RuntimeAnimatorController PetAnimatorController => GetDiscoveredAnimatorController();

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
        
        Debug.Log($"PetData: Discovered components for {petName}:" +
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
                Debug.Log($"PetData: Updated existing BoxCollider bounds for {petName}");
            }
            return;
        }
        
        // No collider exists, create a new BoxCollider
        BoxCollider newBoxCollider = gameObject.AddComponent<BoxCollider>();
        FitBoxColliderToSkinnedMesh(newBoxCollider, discoveredSkinnedMeshRenderer);
        
        // Update our discovered collider reference
        discoveredCollider = newBoxCollider;
        
        Debug.Log($"PetData: Created and fitted new BoxCollider for {petName}");
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
    /// Validates that this pet data is properly configured
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(petName)) return false;
        if (petPortrait == null) return false;
        if (string.IsNullOrEmpty(petDescription)) return false;
        if (starterDeck == null) return false;
        if (baseHealth <= 0) return false;
        if (baseEnergy <= 0) return false;
        
        // Ensure visual components are discovered
        if (!hasDiscovered) DiscoverVisualComponents();
        
        // Validate that we found essential visual components
        if (discoveredMeshRenderer == null)
        {
            Debug.LogWarning($"PetData: No MeshRenderer found in prefab hierarchy for {petName}");
        }
        if (discoveredCollider == null)
        {
            Debug.LogWarning($"PetData: No Collider found in prefab hierarchy for {petName} - needed for 3D selection");
        }
        
        return true;
    }
}

/// <summary>
/// Defines the general type/category of a pet
/// </summary>
public enum PetType
{
    Aggressive,    // High damage, fast attacks
    Tank,          // High health, defensive abilities
    Balanced,      // Balanced stats and abilities
    Support,       // Healing and buffs
    Magical,       // Energy manipulation and spells
    Swift          // Speed and agility focused
} 