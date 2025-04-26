// Add this in a separate file or at the top of the appropriate scripts
public enum StatusEffectType
{
    None,
    Weak,   // Target deals less damage
    Break,  // Target takes more damage
    Thorns,
    Regeneration, // Added: Heals over time
    Strength, // Added: Deals more damage
    // Add more status effects here as needed
}