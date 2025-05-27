using UnityEngine;

/// <summary>
/// Interface for components that display pet information in the OwnPetView system.
/// Follows the Interface Segregation Principle by defining a contract for pet display components.
/// </summary>
public interface IOwnPetDisplay
{
    /// <summary>
    /// Sets the pet to be displayed by this component
    /// </summary>
    /// <param name="pet">The NetworkEntity pet to display</param>
    void SetPet(NetworkEntity pet);
    
    /// <summary>
    /// Gets the currently displayed pet
    /// </summary>
    /// <returns>The NetworkEntity pet currently being displayed</returns>
    NetworkEntity GetCurrentPet();
}

/// <summary>
/// Interface for components that can update their display when pet data changes
/// </summary>
public interface IUpdatablePetDisplay : IOwnPetDisplay
{
    /// <summary>
    /// Updates the display to reflect current pet data
    /// </summary>
    void UpdateDisplay();
}

/// <summary>
/// Interface for components that handle card interactions with pets
/// </summary>
public interface IPetCardInteraction
{
    /// <summary>
    /// Sets the target pet for card interactions
    /// </summary>
    /// <param name="pet">The NetworkEntity pet that can receive card interactions</param>
    void SetTargetPet(NetworkEntity pet);
    
    /// <summary>
    /// Gets the current target pet for card interactions
    /// </summary>
    /// <returns>The NetworkEntity pet that is the current target</returns>
    NetworkEntity GetTargetPet();
} 