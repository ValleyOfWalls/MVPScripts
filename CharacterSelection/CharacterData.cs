using UnityEngine;

/// <summary>
/// ScriptableObject that defines a character archetype with their unique starter deck and appearance.
/// Create via: Right-click in Project → Create → Game Data → Character Data
/// </summary>
[CreateAssetMenu(fileName = "New Character", menuName = "Game Data/Character Data", order = 1)]
public class CharacterData : ScriptableObject
{
    [Header("Basic Info")]
    [SerializeField] private string characterName = "Unknown Character";
    [SerializeField] private Sprite characterPortrait;
    [SerializeField, TextArea(3, 6)] private string characterDescription = "A mysterious character...";
    
    [Header("Visual Configuration")]
    [SerializeField] private Material characterMaterial;
    [SerializeField] private Mesh characterMesh;
    [SerializeField] private Color characterTint = Color.white;
    [SerializeField] private RuntimeAnimatorController characterAnimatorController;
    
    [Header("Gameplay")]
    [SerializeField] private DeckData starterDeck;
    
    [Header("Base Stats")]
    [SerializeField] private int baseHealth = 100;
    [SerializeField] private int baseEnergy = 3;
    [SerializeField] private int startingCurrency = 20;
    
    // Public accessors
    public string CharacterName => characterName;
    public Sprite CharacterPortrait => characterPortrait;
    public string CharacterDescription => characterDescription;
    public Material CharacterMaterial => characterMaterial;
    public Mesh CharacterMesh => characterMesh;
    public Color CharacterTint => characterTint;
    public RuntimeAnimatorController CharacterAnimatorController => characterAnimatorController;
    public DeckData StarterDeck => starterDeck;
    public int BaseHealth => baseHealth;
    public int BaseEnergy => baseEnergy;
    public int StartingCurrency => startingCurrency;

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