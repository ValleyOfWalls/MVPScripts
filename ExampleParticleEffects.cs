using UnityEngine;

/// <summary>
/// Helper script to create basic particle effects for the attack system.
/// Attach to any GameObject and use the context menu options to generate effect prefabs.
/// </summary>
public class ExampleParticleEffects : MonoBehaviour
{
    [Header("Generation Settings")]
    [SerializeField] private string prefabSavePath = "Assets/Prefabs/Effects/";
    
    [Header("Effect Colors")]
    [SerializeField] private Color meleeColor = Color.red;
    [SerializeField] private Color rangedColor = Color.blue;
    [SerializeField] private Color magicColor = Color.magenta;
    [SerializeField] private Color defaultColor = Color.yellow;
    
    /// <summary>
    /// Creates all effect prefabs at once
    /// </summary>
    [ContextMenu("Generate All Effect Prefabs")]
    public void GenerateAllEffectPrefabs()
    {
        GenerateMeleeEffect();
        GenerateRangedEffect();
        GenerateMagicEffect();
        GenerateDefaultEffect();
        
        Debug.Log("ExampleParticleEffects: Generated all attack effect prefabs");
    }
    
    /// <summary>
    /// Creates a melee attack effect (burst of particles)
    /// </summary>
    [ContextMenu("Generate Melee Effect")]
    public void GenerateMeleeEffect()
    {
        GameObject effect = CreateBaseEffect("MeleeAttackEffect");
        ParticleSystem ps = effect.GetComponent<ParticleSystem>();
        
        var main = ps.main;
        main.startLifetime = 0.5f;
        main.startSpeed = 8f;
        main.startSize = 0.2f;
        main.startColor = meleeColor;
        main.maxParticles = 50;
        
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0.0f, 30),
            new ParticleSystem.Burst(0.1f, 20)
        });
        
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        
        SavePrefab(effect, "MeleeAttackEffect");
    }
    
    /// <summary>
    /// Creates a ranged attack effect (projectile trail)
    /// </summary>
    [ContextMenu("Generate Ranged Effect")]
    public void GenerateRangedEffect()
    {
        GameObject effect = CreateBaseEffect("RangedAttackEffect");
        ParticleSystem ps = effect.GetComponent<ParticleSystem>();
        
        var main = ps.main;
        main.startLifetime = 1f;
        main.startSpeed = 5f;
        main.startSize = 0.1f;
        main.startColor = rangedColor;
        main.maxParticles = 100;
        
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.05f;
        
        var emission = ps.emission;
        emission.rateOverTime = 80;
        
        // Add trail for projectile effect
        var trails = ps.trails;
        trails.enabled = true;
        trails.ratio = 1f;
        trails.lifetime = 0.3f;
        trails.minVertexDistance = 0.1f; // Fixed property name
        
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(10f);
        
        SavePrefab(effect, "RangedAttackEffect");
    }
    
    /// <summary>
    /// Creates a magic attack effect (mystical sparkles)
    /// </summary>
    [ContextMenu("Generate Magic Effect")]
    public void GenerateMagicEffect()
    {
        GameObject effect = CreateBaseEffect("MagicAttackEffect");
        ParticleSystem ps = effect.GetComponent<ParticleSystem>();
        
        var main = ps.main;
        main.startLifetime = 1.5f;
        main.startSpeed = 3f;
        main.startSize = 0.15f;
        main.startColor = magicColor;
        main.maxParticles = 150;
        
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;
        
        var emission = ps.emission;
        emission.rateOverTime = 60;
        
        // Add size over lifetime for mystical effect
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.5f);
        sizeCurve.AddKey(0.5f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
        
        // Add color over lifetime for fade effect
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(magicColor, 0f), new GradientColorKey(magicColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = colorGradient;
        
        SavePrefab(effect, "MagicAttackEffect");
    }
    
    /// <summary>
    /// Creates a default attack effect (simple burst)
    /// </summary>
    [ContextMenu("Generate Default Effect")]
    public void GenerateDefaultEffect()
    {
        GameObject effect = CreateBaseEffect("DefaultAttackEffect");
        ParticleSystem ps = effect.GetComponent<ParticleSystem>();
        
        var main = ps.main;
        main.startLifetime = 1f;
        main.startSpeed = 5f;
        main.startSize = 0.1f;
        main.startColor = defaultColor;
        main.maxParticles = 75;
        
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        
        var emission = ps.emission;
        emission.rateOverTime = 50;
        
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        
        SavePrefab(effect, "DefaultAttackEffect");
    }
    
    /// <summary>
    /// Creates a base effect GameObject with ParticleSystem
    /// </summary>
    private GameObject CreateBaseEffect(string name)
    {
        GameObject effect = new GameObject(name);
        ParticleSystem ps = effect.AddComponent<ParticleSystem>();
        
        // Common settings for all effects
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        
        // Set default material to sprites/default if available
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetDefaultSpriteMaterial();
        
        return effect;
    }
    
    /// <summary>
    /// Gets the default sprites material or creates a basic one
    /// </summary>
    private Material GetDefaultSpriteMaterial()
    {
        // Try to find the default sprites material
        Material defaultMaterial = Resources.Load<Material>("Sprites/Default");
        
        if (defaultMaterial == null)
        {
            // Fallback to default sprite material
            defaultMaterial = Resources.GetBuiltinResource<Material>("Sprites-Default.mat");
        }
        
        if (defaultMaterial == null)
        {
            // Create a basic unlit material as last resort
            defaultMaterial = new Material(Shader.Find("Sprites/Default"));
        }
        
        return defaultMaterial;
    }
    
    /// <summary>
    /// Saves the effect as a prefab (Editor only)
    /// </summary>
    private void SavePrefab(GameObject effect, string name)
    {
        #if UNITY_EDITOR
        // Ensure the directory exists
        if (!System.IO.Directory.Exists(prefabSavePath))
        {
            System.IO.Directory.CreateDirectory(prefabSavePath);
        }
        
        string prefabPath = prefabSavePath + name + ".prefab";
        
        // Save as prefab
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(effect, prefabPath);
        
        Debug.Log($"ExampleParticleEffects: Saved {name} prefab to {prefabPath}");
        
        // Clean up the scene object
        DestroyImmediate(effect);
        #else
        Debug.LogWarning("ExampleParticleEffects: Prefab saving only works in editor");
        #endif
    }
    
    /// <summary>
    /// Creates a test scene with example effects
    /// </summary>
    [ContextMenu("Create Test Scene Setup")]
    public void CreateTestSceneSetup()
    {
        // Create test objects
        GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.name = "TestSource";
        source.transform.position = Vector3.left * 2f;
        
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        target.name = "TestTarget";
        target.transform.position = Vector3.right * 2f;
        
        // Create manager
        GameObject manager = new GameObject("AttackEffectManager");
        AttackEffectManager effectManager = manager.AddComponent<AttackEffectManager>();
        
        // Add NetworkObject if FishNet is available
        var networkObjectType = System.Type.GetType("FishNet.Object.NetworkObject");
        if (networkObjectType != null)
        {
            manager.AddComponent(networkObjectType);
        }
        
        Debug.Log("ExampleParticleEffects: Created test scene setup. Assign effect prefabs to AttackEffectManager.");
    }
} 