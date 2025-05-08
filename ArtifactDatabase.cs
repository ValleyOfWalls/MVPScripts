using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton database for managing artifacts
/// </summary>
public class ArtifactDatabase : MonoBehaviour
{
    // Singleton instance
    public static ArtifactDatabase Instance { get; private set; }
    
    [Header("Artifact Data")]
    [SerializeField] private List<ArtifactData> artifactCollection = new List<ArtifactData>();
    
    // Dictionary for quick lookup by ID
    private Dictionary<int, ArtifactData> artifactLookup = new Dictionary<int, ArtifactData>();

    private void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeDatabase();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Initializes the artifact lookup dictionary
    /// </summary>
    private void InitializeDatabase()
    {
        // Clear existing lookup
        artifactLookup.Clear();
        
        // Add each artifact to the lookup dictionary
        foreach (ArtifactData artifact in artifactCollection)
        {
            if (artifact != null)
            {
                if (!artifactLookup.ContainsKey(artifact.Id))
                {
                    artifactLookup.Add(artifact.Id, artifact);
                }
                else
                {
                    Debug.LogWarning($"Duplicate artifact ID found: {artifact.Id}. Skipping duplicate.");
                }
            }
        }
        
        Debug.Log($"ArtifactDatabase initialized with {artifactLookup.Count} artifacts");
    }

    /// <summary>
    /// Gets an artifact by its ID
    /// </summary>
    /// <param name="artifactId">ID of the artifact to retrieve</param>
    /// <returns>ArtifactData, or null if not found</returns>
    public ArtifactData GetArtifactById(int artifactId)
    {
        if (artifactLookup.TryGetValue(artifactId, out ArtifactData data))
        {
            return data;
        }
        
        Debug.LogWarning($"Artifact with ID {artifactId} not found in database");
        return null;
    }

    /// <summary>
    /// Gets a list of random artifact IDs
    /// </summary>
    /// <param name="count">Number of random artifacts to get</param>
    /// <returns>List of random artifact IDs</returns>
    public List<int> GetRandomArtifactIds(int count)
    {
        List<int> result = new List<int>();
        List<int> availableIds = new List<int>(artifactLookup.Keys);
        
        // Limit count to available artifacts
        count = Mathf.Min(count, availableIds.Count);
        
        // Get random IDs
        for (int i = 0; i < count; i++)
        {
            if (availableIds.Count == 0) break;
            
            int randomIndex = Random.Range(0, availableIds.Count);
            int selectedId = availableIds[randomIndex];
            result.Add(selectedId);
            availableIds.RemoveAt(randomIndex);
        }
        
        return result;
    }

    /// <summary>
    /// Gets all artifacts in the database
    /// </summary>
    /// <returns>Dictionary of all artifacts</returns>
    public Dictionary<int, ArtifactData> GetAllArtifacts()
    {
        return new Dictionary<int, ArtifactData>(artifactLookup);
    }
} 