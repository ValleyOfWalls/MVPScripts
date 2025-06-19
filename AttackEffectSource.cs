using UnityEngine;

/// <summary>
/// Component that defines where attack effects should originate from on character/pet models.
/// Attach to: Root of character/pet prefabs alongside CharacterData or PetData.
/// </summary>
public class AttackEffectSource : MonoBehaviour
{
    [Header("Effect Origin Points")]
    [SerializeField] private Transform meleeAttackPoint;
    [SerializeField] private Transform rangedAttackPoint;
    [SerializeField] private Transform magicAttackPoint;
    [SerializeField] private Transform defaultAttackPoint;
    
    [Header("Auto-Discovery Settings")]
    [SerializeField] private bool autoDiscoverPoints = true;
    [SerializeField] private string[] meleePointNames = { "MeleePoint", "WeaponPoint", "RightHand", "Hand_R" };
    [SerializeField] private string[] rangedPointNames = { "RangedPoint", "BowPoint", "LeftHand", "Hand_L" };
    [SerializeField] private string[] magicPointNames = { "MagicPoint", "StaffPoint", "Chest", "Head" };
    [SerializeField] private string[] defaultPointNames = { "AttackPoint", "EffectPoint", "Center" };
    
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
    [SerializeField] private Color meleeGizmoColor = Color.red;
    [SerializeField] private Color rangedGizmoColor = Color.blue;
    [SerializeField] private Color magicGizmoColor = Color.magenta;
    [SerializeField] private Color defaultGizmoColor = Color.yellow;
    
    private bool hasDiscovered = false;
    
    // Public accessors for effect points
    public Transform MeleeAttackPoint => GetEffectPoint(AttackType.Melee);
    public Transform RangedAttackPoint => GetEffectPoint(AttackType.Ranged);
    public Transform MagicAttackPoint => GetEffectPoint(AttackType.Magic);
    public Transform DefaultAttackPoint => GetEffectPoint(AttackType.Default);
    
    public enum AttackType
    {
        Melee,
        Ranged,
        Magic,
        Default
    }
    
    private void Awake()
    {
        DiscoverEffectPoints();
    }
    
    private void Start()
    {
        // Ensure discovery happens even if Awake was missed
        if (!hasDiscovered)
        {
            DiscoverEffectPoints();
        }
    }
    
    private void OnValidate()
    {
        // Discover points in editor when values change
        #if UNITY_EDITOR
        if (!Application.isPlaying && autoDiscoverPoints)
        {
            DiscoverEffectPoints();
        }
        #endif
    }
    
    /// <summary>
    /// Automatically discovers effect points in the model hierarchy
    /// </summary>
    [ContextMenu("Discover Effect Points")]
    public void DiscoverEffectPoints()
    {
        // Find the model root (prioritize components that might indicate the model)
        DiscoverModelRoot();
        
        if (autoDiscoverPoints && discoveredModelRoot != null)
        {
            // Try to find specific effect points by name
            meleeAttackPoint = FindPointByNames(meleePointNames);
            rangedAttackPoint = FindPointByNames(rangedPointNames);
            magicAttackPoint = FindPointByNames(magicPointNames);
            defaultAttackPoint = FindPointByNames(defaultPointNames);
        }
        
        // Calculate model bounds for fallback positioning
        CalculateModelBounds();
        
        hasDiscovered = true;
        
        Debug.Log($"AttackEffectSource: Discovered effect points for {gameObject.name}:" +
                  $"\n- Model Root: {(discoveredModelRoot != null ? discoveredModelRoot.name : "None")}" +
                  $"\n- Melee Point: {(meleeAttackPoint != null ? meleeAttackPoint.name : "Auto-Generated")}" +
                  $"\n- Ranged Point: {(rangedAttackPoint != null ? rangedAttackPoint.name : "Auto-Generated")}" +
                  $"\n- Magic Point: {(magicAttackPoint != null ? magicAttackPoint.name : "Auto-Generated")}" +
                  $"\n- Default Point: {(defaultAttackPoint != null ? defaultAttackPoint.name : "Auto-Generated")}" +
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
    /// Gets the effect point for a specific attack type
    /// </summary>
    public Transform GetEffectPoint(AttackType attackType)
    {
        if (!hasDiscovered) DiscoverEffectPoints();
        
        Transform point = null;
        
        switch (attackType)
        {
            case AttackType.Melee:
                point = meleeAttackPoint;
                break;
            case AttackType.Ranged:
                point = rangedAttackPoint;
                break;
            case AttackType.Magic:
                point = magicAttackPoint;
                break;
            case AttackType.Default:
                point = defaultAttackPoint;
                break;
        }
        
        // If no specific point found, return fallback position
        if (point == null)
        {
            return GetFallbackPoint();
        }
        
        return point;
    }
    
    /// <summary>
    /// Gets the world position for an attack type
    /// </summary>
    public Vector3 GetEffectPosition(AttackType attackType)
    {
        Transform point = GetEffectPoint(attackType);
        return point != null ? point.position : GetFallbackPosition();
    }
    
    /// <summary>
    /// Gets a fallback transform when no specific point is available
    /// </summary>
    private Transform GetFallbackPoint()
    {
        // Create a temporary point if needed
        if (defaultAttackPoint == null && discoveredModelRoot != null)
        {
            return discoveredModelRoot;
        }
        
        return defaultAttackPoint != null ? defaultAttackPoint : transform;
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
        GameObject effectPoint = new GameObject(pointName);
        effectPoint.transform.SetParent(discoveredModelRoot != null ? discoveredModelRoot : transform);
        effectPoint.transform.localPosition = localPosition;
        
        return effectPoint;
    }
    
    /// <summary>
    /// Validates that effect points are properly configured
    /// </summary>
    public bool IsValid()
    {
        if (!hasDiscovered) DiscoverEffectPoints();
        
        // At minimum, we need a fallback position
        return discoveredModelRoot != null || transform != null;
    }
    
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        if (!hasDiscovered && Application.isPlaying)
        {
            DiscoverEffectPoints();
        }
        
        // Draw effect points
        DrawEffectPointGizmo(AttackType.Melee, meleeGizmoColor);
        DrawEffectPointGizmo(AttackType.Ranged, rangedGizmoColor);
        DrawEffectPointGizmo(AttackType.Magic, magicGizmoColor);
        DrawEffectPointGizmo(AttackType.Default, defaultGizmoColor);
        
        // Draw model bounds
        if (useModelBounds && modelBounds.size != Vector3.zero)
        {
            Gizmos.color = Color.white * 0.3f;
            Gizmos.DrawWireCube(modelBounds.center, modelBounds.size);
        }
    }
    
    private void DrawEffectPointGizmo(AttackType attackType, Color color)
    {
        Vector3 position = GetEffectPosition(attackType);
        
        Gizmos.color = color;
        Gizmos.DrawWireSphere(position, gizmoSize);
        
        // Draw a small cross to indicate direction
        Gizmos.DrawLine(position - Vector3.right * gizmoSize * 0.5f, position + Vector3.right * gizmoSize * 0.5f);
        Gizmos.DrawLine(position - Vector3.up * gizmoSize * 0.5f, position + Vector3.up * gizmoSize * 0.5f);
        Gizmos.DrawLine(position - Vector3.forward * gizmoSize * 0.5f, position + Vector3.forward * gizmoSize * 0.5f);
    }
} 