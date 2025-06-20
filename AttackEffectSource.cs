using UnityEngine;

/// <summary>
/// Component that defines where effect animations should originate from on character/pet models.
/// Attach to: Root of character/pet prefabs alongside CharacterData or PetData.
/// </summary>
public class EffectAnimationSource : MonoBehaviour
{
    [Header("Effect Origin Point")]
    [SerializeField] private Transform effectPoint;
    
    [Header("Auto-Discovery Settings")]
    [SerializeField] private bool autoDiscoverPoint = true;
    [SerializeField] private string[] effectPointNames = { "EffectPoint", "AttackPoint", "MeleePoint", "WeaponPoint", "RightHand", "Hand_R", "Center" };
    
    [Header("Fallback Settings")]
    [SerializeField] private Vector3 fallbackOffset = Vector3.up;
    [SerializeField] private bool useModelBounds = true;
    
    [Header("Auto-Discovered Components (Read-Only)")]
    [SerializeField, ReadOnly] private Transform discoveredModelRoot;
    [SerializeField, ReadOnly] private Renderer discoveredRenderer;
    [SerializeField, ReadOnly] private Bounds modelBounds;
    
    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private float gizmoSize = 0.1f;
    [SerializeField] private Color effectGizmoColor = Color.yellow;
    
    private bool hasDiscovered = false;
    
    // Public accessor for effect point
    public Transform EffectPoint => GetEffectPoint();
    
    private void Awake()
    {
        DiscoverEffectPoint();
    }
    
    private void Start()
    {
        // Ensure discovery happens even if Awake was missed
        if (!hasDiscovered)
        {
            DiscoverEffectPoint();
        }
    }
    
    private void OnValidate()
    {
        // Discover points in editor when values change
        #if UNITY_EDITOR
        if (!Application.isPlaying && autoDiscoverPoint)
        {
            DiscoverEffectPoint();
        }
        #endif
    }
    
    /// <summary>
    /// Automatically discovers effect point in the model hierarchy
    /// </summary>
    [ContextMenu("Discover Effect Point")]
    public void DiscoverEffectPoint()
    {
        // Find the model root (prioritize components that might indicate the model)
        DiscoverModelRoot();
        
        if (autoDiscoverPoint && discoveredModelRoot != null)
        {
            // Try to find specific effect point by name
            effectPoint = FindPointByNames(effectPointNames);
        }
        
        // Calculate model bounds for fallback positioning
        CalculateModelBounds();
        
        hasDiscovered = true;
        
        Debug.Log($"EffectAnimationSource: Discovered effect point for {gameObject.name}:" +
                  $"\n- Model Root: {(discoveredModelRoot != null ? discoveredModelRoot.name : "None")}" +
                  $"\n- Effect Point: {(effectPoint != null ? effectPoint.name : "Auto-Generated")}" +
                  $"\n- Model Bounds: {modelBounds}");
    }
    
    private void DiscoverModelRoot()
    {
        // Try to get model from other components first
        var characterData = GetComponent<CharacterData>();
        var petData = GetComponent<PetData>();
        
        if (characterData != null)
        {
            var skinnedRenderer = characterData.CharacterSkinnedMeshRenderer;
            var meshRenderer = characterData.CharacterMeshRenderer;
            discoveredRenderer = skinnedRenderer != null ? skinnedRenderer : meshRenderer;
            discoveredModelRoot = discoveredRenderer?.transform;
        }
        else if (petData != null)
        {
            var skinnedRenderer = petData.PetSkinnedMeshRenderer;
            var meshRenderer = petData.PetMeshRenderer;
            discoveredRenderer = skinnedRenderer != null ? skinnedRenderer : meshRenderer;
            discoveredModelRoot = discoveredRenderer?.transform;
        }
        
        // Fallback: search for renderers in children
        if (discoveredModelRoot == null)
        {
            discoveredRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if (discoveredRenderer == null)
                discoveredRenderer = GetComponentInChildren<MeshRenderer>();
            
            discoveredModelRoot = discoveredRenderer?.transform;
        }
        
        // Final fallback: use this transform
        if (discoveredModelRoot == null)
        {
            discoveredModelRoot = transform;
        }
    }
    
    private Transform FindPointByNames(string[] pointNames)
    {
        if (discoveredModelRoot == null) return null;
        
        foreach (string pointName in pointNames)
        {
            // Search in the model hierarchy
            Transform found = FindChildByName(discoveredModelRoot, pointName);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
    
    private Transform FindChildByName(Transform parent, string name)
    {
        // Check direct children first
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name.Contains(name))
            {
                return child;
            }
        }
        
        // Recursive search in grandchildren
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildByName(parent.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
    
    private void CalculateModelBounds()
    {
        if (discoveredRenderer != null)
        {
            modelBounds = discoveredRenderer.bounds;
        }
        else
        {
            // Fallback: calculate bounds from all renderers
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                modelBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    modelBounds.Encapsulate(renderers[i].bounds);
                }
            }
            else
            {
                // Final fallback: use transform position
                modelBounds = new Bounds(transform.position, Vector3.one);
            }
        }
    }
    
    /// <summary>
    /// Gets the effect point transform
    /// </summary>
    public Transform GetEffectPoint()
    {
        if (!hasDiscovered) DiscoverEffectPoint();
        
        // If no specific point found, return fallback position
        if (effectPoint == null)
        {
            return GetFallbackPoint();
        }
        
        return effectPoint;
    }
    
    /// <summary>
    /// Gets the world position for effect animations
    /// </summary>
    public Vector3 GetEffectPosition()
    {
        Transform point = GetEffectPoint();
        return point != null ? point.position : GetFallbackPosition();
    }
    
    /// <summary>
    /// Gets a fallback transform when no specific point is available
    /// </summary>
    private Transform GetFallbackPoint()
    {
        // Create a temporary point if needed
        if (effectPoint == null && discoveredModelRoot != null)
        {
            return discoveredModelRoot;
        }
        
        return effectPoint != null ? effectPoint : transform;
    }
    
    /// <summary>
    /// Gets a fallback position when no specific point is available
    /// </summary>
    private Vector3 GetFallbackPosition()
    {
        if (useModelBounds && modelBounds.size != Vector3.zero)
        {
            // Use model center + offset
            return modelBounds.center + fallbackOffset;
        }
        
        // Use transform position + offset
        return transform.position + fallbackOffset;
    }
    
    /// <summary>
    /// Creates an effect point at a specific position
    /// </summary>
    [ContextMenu("Create Effect Point Here")]
    public GameObject CreateEffectPoint(string pointName, Vector3 localPosition)
    {
        GameObject effectPointGO = new GameObject(pointName);
        effectPointGO.transform.SetParent(discoveredModelRoot != null ? discoveredModelRoot : transform);
        effectPointGO.transform.localPosition = localPosition;
        
        return effectPointGO;
    }
    
    /// <summary>
    /// Validates that effect point is properly configured
    /// </summary>
    public bool IsValid()
    {
        if (!hasDiscovered) DiscoverEffectPoint();
        
        // At minimum, we need a fallback position
        return discoveredModelRoot != null || transform != null;
    }
    
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        if (!hasDiscovered && Application.isPlaying)
        {
            DiscoverEffectPoint();
        }
        
        // Draw effect point
        DrawEffectPointGizmo();
        
        // Draw model bounds
        if (useModelBounds && modelBounds.size != Vector3.zero)
        {
            Gizmos.color = Color.white * 0.3f;
            Gizmos.DrawWireCube(modelBounds.center, modelBounds.size);
        }
    }
    
    private void DrawEffectPointGizmo()
    {
        Vector3 position = GetEffectPosition();
        
        Gizmos.color = effectGizmoColor;
        Gizmos.DrawWireSphere(position, gizmoSize);
        
        // Draw a small cross to indicate direction
        Gizmos.DrawLine(position - Vector3.right * gizmoSize * 0.5f, position + Vector3.right * gizmoSize * 0.5f);
        Gizmos.DrawLine(position - Vector3.up * gizmoSize * 0.5f, position + Vector3.up * gizmoSize * 0.5f);
        Gizmos.DrawLine(position - Vector3.forward * gizmoSize * 0.5f, position + Vector3.forward * gizmoSize * 0.5f);
    }
} 