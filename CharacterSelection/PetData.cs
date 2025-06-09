using UnityEngine;

/// <summary>
/// ScriptableObject that defines a pet type with their unique starter deck and appearance.
/// Create via: Right-click in Project → Create → Game Data → Pet Data
/// </summary>
[CreateAssetMenu(fileName = "New Pet", menuName = "Game Data/Pet Data", order = 2)]
public class PetData : ScriptableObject
{
    [Header("Basic Info")]
    [SerializeField] private string petName = "Unknown Pet";
    [SerializeField] private Sprite petPortrait;
    [SerializeField, TextArea(3, 6)] private string petDescription = "A mysterious companion...";
    
    [Header("Visual Configuration")]
    [SerializeField] private Material petMaterial;
    [SerializeField] private Mesh petMesh;
    [SerializeField] private Color petTint = Color.white;
    [SerializeField] private RuntimeAnimatorController petAnimatorController;
    
    [Header("Gameplay")]
    [SerializeField] private DeckData starterDeck;
    
    [Header("Base Stats")]
    [SerializeField] private int baseHealth = 50;
    [SerializeField] private int baseEnergy = 2;
    
    // Public accessors
    public string PetName => petName;
    public Sprite PetPortrait => petPortrait;
    public string PetDescription => petDescription;
    public Material PetMaterial => petMaterial;
    public Mesh PetMesh => petMesh;
    public Color PetTint => petTint;
    public RuntimeAnimatorController PetAnimatorController => petAnimatorController;
    public DeckData StarterDeck => starterDeck;
    public int BaseHealth => baseHealth;
    public int BaseEnergy => baseEnergy;

    /// <summary>
    /// Validates that this pet data is properly configured
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(petName)) return false;
        if (petPortrait == null) return false;
        if (string.IsNullOrEmpty(petDescription)) return false;
        if (petMaterial == null) return false;
        if (petMesh == null) return false;
        if (starterDeck == null) return false;
        if (baseHealth <= 0) return false;
        if (baseEnergy <= 0) return false;
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