using UnityEngine;

public class CombatManager : MonoBehaviour
{
    // Find the Canvas Manager in the scene
    CombatCanvasManager canvasManager = FindFirstObjectByType<CombatCanvasManager>();

    if (canvasManager != null)
    {
        // Call the method on the Canvas Manager to update the pet's status text
    }
} 