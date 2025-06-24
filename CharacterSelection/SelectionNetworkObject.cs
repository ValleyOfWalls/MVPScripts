using UnityEngine;
using System.Collections;

/// <summary>
/// Marks a NetworkObject as being used for character selection display.
/// This helps identify and clean up selection objects when transitioning phases.
/// Also handles proper parenting for late-joining clients.
/// </summary>
public class SelectionNetworkObject : MonoBehaviour
{
    [Header("Selection Info")]
    public bool isCharacterSelectionObject = false;
    public int selectionIndex = -1;
    public bool isCharacter = true;
    
    [Header("Cleanup")]
    public bool shouldDespawnOnCleanup = true;
    
    [Header("Parenting")]
    private Transform desiredParent;
    private bool hasBeenParented = false;
    
    private void Start()
    {
        // Optional: Add any initialization logic here
        if (isCharacterSelectionObject)
        {
            /* Debug.Log($"SelectionNetworkObject: Marked {gameObject.name} as character selection object (Index: {selectionIndex}, IsCharacter: {isCharacter})"); */
            
            // For clients, immediately try to find and set the correct parent
            if (!FishNet.InstanceFinder.IsServerStarted)
            {
                // Try immediate parenting first
                TryImmediateParenting();
                
                // If that fails, use coroutine as fallback
                if (!hasBeenParented)
                {
                    StartCoroutine(TryFindAndSetParentOnClient());
                }
            }
        }
    }
    
    /// <summary>
    /// Tries to immediately find and set the parent without waiting
    /// </summary>
    private void TryImmediateParenting()
    {
        CharacterSelectionUIManager uiManager = FindObjectOfType<CharacterSelectionUIManager>();
        if (uiManager != null)
        {
            // Find the appropriate parent transform using reflection to access private fields
            Transform targetParent = null;
            
            try
            {
                var field = typeof(CharacterSelectionUIManager).GetField(
                    isCharacter ? "characterModelsParent" : "petModelsParent", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (field != null)
                {
                    targetParent = field.GetValue(uiManager) as Transform;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"SelectionNetworkObject: Could not access parent field via reflection: {e.Message}");
            }
            
            // If we found a parent, use it
            if (targetParent != null)
            {
                desiredParent = targetParent;
                SetParentNow();
                
                // Position the object correctly
                PositionObject(uiManager);
            }
        }
    }
    
    /// <summary>
    /// Sets the desired parent for this selection object
    /// </summary>
    public void SetDesiredParent(Transform parent)
    {
        desiredParent = parent;
        
        // If we haven't been parented yet and have a valid parent, do it now
        if (parent != null && !hasBeenParented)
        {
            SetParentNow();
        }
    }
    
    /// <summary>
    /// Attempts to find and set the correct parent on clients
    /// </summary>
    private IEnumerator TryFindAndSetParentOnClient()
    {
        // Wait for UI to be initialized
        float waitTime = 0f;
        const float maxWaitTime = 5f;
        
        while (waitTime < maxWaitTime && !hasBeenParented)
        {
            yield return new WaitForSeconds(0.2f);
            waitTime += 0.2f;
            
            // Try to find the UI manager
            CharacterSelectionUIManager uiManager = FindObjectOfType<CharacterSelectionUIManager>();
            if (uiManager != null)
            {
                // Find the appropriate parent transform
                Transform targetParent = null;
                
                if (isCharacter)
                {
                    // Look for character models parent
                    targetParent = uiManager.transform.Find("CharacterModelsParent");
                    if (targetParent == null)
                    {
                        // Fallback: look for any transform with "character" in the name
                        Transform[] children = uiManager.GetComponentsInChildren<Transform>();
                        foreach (Transform child in children)
                        {
                            if (child.name.ToLower().Contains("character") && child.name.ToLower().Contains("parent"))
                            {
                                targetParent = child;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    // Look for pet models parent
                    targetParent = uiManager.transform.Find("PetModelsParent");
                    if (targetParent == null)
                    {
                        // Fallback: look for any transform with "pet" in the name
                        Transform[] children = uiManager.GetComponentsInChildren<Transform>();
                        foreach (Transform child in children)
                        {
                            if (child.name.ToLower().Contains("pet") && child.name.ToLower().Contains("parent"))
                            {
                                targetParent = child;
                                break;
                            }
                        }
                    }
                }
                
                // If we found a parent, use it
                if (targetParent != null)
                {
                    desiredParent = targetParent;
                    SetParentNow();
                    
                    // Position the object correctly
                    PositionObject(uiManager);
                    break;
                }
            }
        }
        
        if (!hasBeenParented)
        {
            Debug.LogWarning($"SelectionNetworkObject: Could not find appropriate parent for {gameObject.name} after {maxWaitTime} seconds");
        }
    }
    
    /// <summary>
    /// Sets the parent immediately
    /// </summary>
    private void SetParentNow()
    {
        if (desiredParent != null && !hasBeenParented)
        {
            transform.SetParent(desiredParent, false);
            hasBeenParented = true;
            
            Debug.Log($"SelectionNetworkObject: Successfully parented {gameObject.name} to {desiredParent.name}");
        }
    }
    
    /// <summary>
    /// Positions the object correctly using the UI manager
    /// </summary>
    private void PositionObject(CharacterSelectionUIManager uiManager)
    {
        if (uiManager != null)
        {
            // Try to call the Position3DModel method via reflection
            try
            {
                var method = typeof(CharacterSelectionUIManager).GetMethod("Position3DModel", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    method.Invoke(uiManager, new object[] { gameObject, selectionIndex, isCharacter });
                    Debug.Log($"SelectionNetworkObject: Positioned {gameObject.name} at index {selectionIndex}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"SelectionNetworkObject: Could not position object {gameObject.name}: {e.Message}");
            }
        }
    }
    
    /// <summary>
    /// Cleans up this selection object
    /// </summary>
    public void CleanupSelectionObject()
    {
        if (shouldDespawnOnCleanup)
        {
            FishNet.Object.NetworkObject networkObject = GetComponent<FishNet.Object.NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                // Despawn through the network system
                if (FishNet.InstanceFinder.IsServerStarted)
                {
                    FishNet.InstanceFinder.ServerManager.Despawn(networkObject);
                }
            }
        }
        
        // Destroy the GameObject
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }
} 