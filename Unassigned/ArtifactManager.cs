using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// Manages artifact operations and effects for a network entity
/// </summary>
public class ArtifactManager : NetworkBehaviour
{
    // Reference to the entity's artifact collection
    private NetworkEntityArtifacts entityArtifacts;
    
    // Cache of artifact objects
    private Dictionary<int, Artifact> artifactObjects = new Dictionary<int, Artifact>();
    
    [SerializeField] private Transform artifactDisplayContainer;
    [SerializeField] private GameObject artifactPrefab;

    private void Awake()
    {
        // Get the network entity artifacts component
        entityArtifacts = GetComponent<NetworkEntityArtifacts>();
        
        if (entityArtifacts == null)
        {
            Debug.LogError("ArtifactManager requires a NetworkEntityArtifacts component on the same GameObject.");
            return;
        }
    }

    private void OnEnable()
    {
        if (entityArtifacts != null)
        {
            // Subscribe to artifact collection changes
            entityArtifacts.OnArtifactsChanged += UpdateArtifactDisplay;
        }
    }

    private void OnDisable()
    {
        if (entityArtifacts != null)
        {
            // Unsubscribe from artifact collection changes
            entityArtifacts.OnArtifactsChanged -= UpdateArtifactDisplay;
        }
    }

    /// <summary>
    /// Adds an artifact to the entity's collection
    /// </summary>
    /// <param name="artifactId">ID of the artifact to add</param>
    public void AddArtifact(int artifactId)
    {
        if (entityArtifacts.AddArtifact(artifactId))
        {
            Debug.Log($"Added artifact {artifactId} to {gameObject.name}");
        }
    }

    /// <summary>
    /// Updates the visual display of artifacts
    /// </summary>
    private void UpdateArtifactDisplay()
    {
        if (artifactDisplayContainer == null) return;

        // Clear existing display
        foreach (Transform child in artifactDisplayContainer)
        {
            Destroy(child.gameObject);
        }
        artifactObjects.Clear();

        // Get all artifact IDs
        List<int> artifactIds = entityArtifacts.GetAllArtifactIds();

        // Create displays for each artifact
        foreach (int id in artifactIds)
        {
            ArtifactData data = entityArtifacts.GetArtifactData(id);
            if (data == null) continue;

            // Instantiate artifact object
            GameObject artifactObj = Instantiate(artifactPrefab, artifactDisplayContainer);
            Artifact artifact = artifactObj.GetComponent<Artifact>();
            
            if (artifact != null)
            {
                artifact.Initialize(data);
                artifact.SetupForInventory(GetComponent<NetworkBehaviour>());
                artifactObjects[id] = artifact;
            }
        }
    }

    /// <summary>
    /// Triggers artifact effects for a specific trigger type
    /// </summary>
    /// <param name="triggerType">The type of trigger to activate</param>
    public void TriggerArtifactEffects(ArtifactTriggerType triggerType)
    {
        // Get all artifact IDs
        List<int> artifactIds = entityArtifacts.GetAllArtifactIds();

        // Trigger effects for each artifact
        foreach (int id in artifactIds)
        {
            // Get or create the artifact object
            Artifact artifact;
            if (!artifactObjects.TryGetValue(id, out artifact))
            {
                ArtifactData data = entityArtifacts.GetArtifactData(id);
                if (data == null) continue;

                GameObject artifactObj = new GameObject($"Artifact_{id}");
                artifact = artifactObj.AddComponent<Artifact>();
                artifact.Initialize(data);
                artifact.SetupForInventory(GetComponent<NetworkBehaviour>());
                artifactObjects[id] = artifact;
            }

            // Apply the effect
            artifact.ApplyEffect(triggerType);
        }
    }

    /// <summary>
    /// Gets the total effect magnitude for a specific effect type
    /// Used by other systems to query artifact bonuses
    /// </summary>
    /// <param name="effectType">The type of effect to calculate</param>
    /// <returns>The total magnitude of the effect from all artifacts</returns>
    public int GetTotalEffectMagnitude(ArtifactEffectType effectType)
    {
        int total = 0;
        List<int> artifactIds = entityArtifacts.GetAllArtifactIds();

        foreach (int id in artifactIds)
        {
            ArtifactData data = entityArtifacts.GetArtifactData(id);
            if (data != null && data.EffectType == effectType)
            {
                total += data.EffectMagnitude;
            }
        }

        return total;
    }
} 