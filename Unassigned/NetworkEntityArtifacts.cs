using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// Maintains a networked list of artifacts owned by an entity
/// </summary>
public class NetworkEntityArtifacts : NetworkBehaviour
{
    // Networked list of artifact IDs
    private readonly SyncList<int> artifactIds = new SyncList<int>();
    
    // Local cache of loaded artifact data
    private Dictionary<int, ArtifactData> artifactDataCache = new Dictionary<int, ArtifactData>();
    
    // Delegate for artifact collection changes
    public delegate void ArtifactCollectionChanged();
    public event ArtifactCollectionChanged OnArtifactsChanged;

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Register for changes to the artifact list
        artifactIds.OnChange += HandleArtifactListChanged;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Unregister from changes to the artifact list
        artifactIds.OnChange -= HandleArtifactListChanged;
    }

    /// <summary>
    /// Adds an artifact to the entity's collection
    /// </summary>
    /// <param name="artifactId">ID of the artifact to add</param>
    /// <returns>True if successfully added</returns>
    [Server]
    public bool AddArtifact(int artifactId)
    {
        if (!IsServerInitialized) return false;
        
        // Add the artifact ID to the collection
        artifactIds.Add(artifactId);
        return true;
    }

    /// <summary>
    /// Removes an artifact from the entity's collection
    /// </summary>
    /// <param name="artifactId">ID of the artifact to remove</param>
    /// <returns>True if successfully removed</returns>
    [Server]
    public bool RemoveArtifact(int artifactId)
    {
        if (!IsServerInitialized) return false;
        
        // Find and remove the artifact ID from the collection
        for (int i = 0; i < artifactIds.Count; i++)
        {
            if (artifactIds[i] == artifactId)
            {
                artifactIds.RemoveAt(i);
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Gets a list of all artifact IDs in the collection
    /// </summary>
    /// <returns>List of artifact IDs</returns>
    public List<int> GetAllArtifactIds()
    {
        List<int> result = new List<int>();
        
        foreach (int id in artifactIds)
        {
            result.Add(id);
        }
        
        return result;
    }

    /// <summary>
    /// Loads artifact data for a given ID
    /// </summary>
    /// <param name="artifactId">ID of the artifact to load</param>
    /// <returns>ArtifactData for the specified ID, or null if not found</returns>
    public ArtifactData GetArtifactData(int artifactId)
    {
        // Check if the data is already in cache
        if (artifactDataCache.TryGetValue(artifactId, out ArtifactData cachedData))
        {
            return cachedData;
        }
        
        // Otherwise load it from the ArtifactDatabase
        ArtifactData data = ArtifactDatabase.Instance.GetArtifactById(artifactId);
        
        // Cache the data if found
        if (data != null)
        {
            artifactDataCache[artifactId] = data;
        }
        
        return data;
    }

    /// <summary>
    /// Handles changes to the artifacts collection
    /// </summary>
    private void HandleArtifactListChanged(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
    {
        // Notify subscribers that the artifact collection has changed
        OnArtifactsChanged?.Invoke();
    }
} 