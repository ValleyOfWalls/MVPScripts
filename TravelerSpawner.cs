using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class TravelerSpawner : MonoBehaviour
{
    [Header("Prefab Settings")]
    [SerializeField] private GameObject travelerPrefab;
    
    [Header("Spawn Settings")]
    [SerializeField] private float spawnRate = 2f; // Travelers per second
    [SerializeField] private int maxTravelers = 50;
    
    [Header("Spawn Volume (Bottom of Screen)")]
    [SerializeField] private Vector3 spawnCenter = new Vector3(0, 0, -5);
    [SerializeField] private Vector3 spawnSize = new Vector3(10, 0, 2);
    
    [Header("Destination Volume (Top of Screen)")]
    [SerializeField] private Vector3 destinationCenter = new Vector3(0, 0, 5);
    [SerializeField] private Vector3 destinationSize = new Vector3(8, 0, 2);
    
    [Header("Movement Settings")]
    [SerializeField] private float minSpeed = 1f;
    [SerializeField] private float maxSpeed = 3f;
    [SerializeField] private float pathVariation = 0.5f; // How much travelers deviate from straight path
    
    // Object Pooling
    private Queue<GameObject> travelerPool = new Queue<GameObject>();
    private List<GameObject> activeTravelers = new List<GameObject>();
    
    private void Start()
    {
        /* Debug.Log("TravelerSpawner: Starting initialization..."); */
        
        // Check if prefab is assigned
        if (travelerPrefab == null)
        {
            Debug.LogError("TravelerSpawner: No traveler prefab assigned! Please assign a prefab in the inspector.");
            return;
        }
        
        /* Debug.Log($"TravelerSpawner: Creating pool of {maxTravelers} travelers..."); */
        
        // Pre-populate the pool
        for (int i = 0; i < maxTravelers; i++)
        {
            GameObject traveler = Instantiate(travelerPrefab);
            traveler.SetActive(false);
            
            // Add TravelerMover component if it doesn't exist
            if (traveler.GetComponent<TravelerMover>() == null)
            {
                traveler.AddComponent<TravelerMover>();
            }
            
            travelerPool.Enqueue(traveler);
        }
        
        /* Debug.Log($"TravelerSpawner: Pool created with {travelerPool.Count} travelers. Starting spawn coroutine..."); */
        StartCoroutine(SpawnTravelers());
    }
    
    private IEnumerator SpawnTravelers()
    {
        /* Debug.Log("TravelerSpawner: Spawn coroutine started!"); */
        
        while (true)
        {
            /* Debug.Log($"TravelerSpawner: Active: {activeTravelers.Count}, Pool: {travelerPool.Count}, Max: {maxTravelers}"); */
            
            if (activeTravelers.Count < maxTravelers && travelerPool.Count > 0)
            {
                Debug.Log("TravelerSpawner: Attempting to spawn traveler...");
                SpawnTraveler();
            }
            else
            {
                Debug.Log("TravelerSpawner: Skipping spawn - conditions not met");
            }
            
            yield return new WaitForSeconds(1f / spawnRate);
        }
    }
    
    private void SpawnTraveler()
    {
        /* Debug.Log("TravelerSpawner: SpawnTraveler() called"); */
        
        if (travelerPool.Count == 0)
        {
            Debug.LogWarning("TravelerSpawner: Pool is empty!");
            return;
        }
        
        GameObject traveler = travelerPool.Dequeue();
        /* Debug.Log($"TravelerSpawner: Got traveler from pool: {traveler.name}"); */
        
        // Random spawn position within spawn volume
        Vector3 spawnPos = GetRandomPositionInVolume(spawnCenter, spawnSize);
        
        // Find a valid position on the NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(spawnPos, out hit, 5f, NavMesh.AllAreas))
        {
            traveler.transform.position = hit.position;
        }
        else
        {
            // Fallback: try the spawn center if random position fails
            if (NavMesh.SamplePosition(spawnCenter, out hit, 5f, NavMesh.AllAreas))
            {
                traveler.transform.position = hit.position;
            }
            else
            {
                // Last resort: use original position and log warning
                traveler.transform.position = spawnPos;
                Debug.LogWarning("No NavMesh found near spawn area. Make sure NavMesh covers spawn points.");
            }
        }
        
        // Random destination within destination volume
        Vector3 destination = GetRandomPositionInVolume(destinationCenter, destinationSize);
        
        // Add some path variation (slight curve)
        Vector3 pathVariationOffset = new Vector3(
            Random.Range(-pathVariation, pathVariation),
            0,
            0
        );
        destination += pathVariationOffset;
        
        // Find a valid destination on the NavMesh
        if (NavMesh.SamplePosition(destination, out hit, 5f, NavMesh.AllAreas))
        {
            destination = hit.position;
        }
        else
        {
            // Fallback: try destination center if random destination fails
            if (NavMesh.SamplePosition(destinationCenter, out hit, 5f, NavMesh.AllAreas))
            {
                destination = hit.position;
            }
            else
            {
                Debug.LogWarning("No NavMesh found near destination area. Make sure NavMesh covers destination points.");
            }
        }
        
        // Random speed
        float speed = Random.Range(minSpeed, maxSpeed);
        
        // Random rotation for variety
        traveler.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
        
        // Activate the traveler first so NavMeshAgent becomes active
        /* Debug.Log($"TravelerSpawner: Activating traveler at position {traveler.transform.position}"); */
        traveler.SetActive(true);
        activeTravelers.Add(traveler);
        
        // Setup the traveler AFTER activation
        TravelerMover mover = traveler.GetComponent<TravelerMover>();
        
        if (mover == null)
        {
            Debug.LogError($"TravelerSpawner: No TravelerMover component found on {traveler.name}!");
            traveler.SetActive(false);
            activeTravelers.Remove(traveler);
            travelerPool.Enqueue(traveler);
            return;
        }
        
        /* Debug.Log($"TravelerSpawner: Found TravelerMover component. Initializing traveler with destination {destination} and speed {speed}"); */
        
        try
        {
            mover.Initialize(destination, speed, this);
            /* Debug.Log($"TravelerSpawner: Initialize completed successfully!"); */
        }
        catch (System.Exception e)
        {
            Debug.LogError($"TravelerSpawner: Error during Initialize: {e.Message}");
            Debug.LogError($"TravelerSpawner: Stack trace: {e.StackTrace}");
        }
        
        /* Debug.Log($"TravelerSpawner: Traveler spawned successfully! Active count: {activeTravelers.Count}"); */
    }
    
    private Vector3 GetRandomPositionInVolume(Vector3 center, Vector3 size)
    {
        Vector3 randomOffset = new Vector3(
            Random.Range(-size.x / 2, size.x / 2),
            0, // No Y variation - keep all travelers at ground level
            Random.Range(-size.z / 2, size.z / 2)
        );
        
        return center + randomOffset;
    }
    
    public void ReturnTravelerToPool(GameObject traveler)
    {
        /* Debug.Log($"TravelerSpawner: ReturnTravelerToPool called for {traveler.name}"); */
        
        if (activeTravelers.Contains(traveler))
        {
            Debug.Log($"TravelerSpawner: Returning {traveler.name} to pool. Active count before: {activeTravelers.Count}");
            activeTravelers.Remove(traveler);
            traveler.SetActive(false);
            travelerPool.Enqueue(traveler);
            Debug.Log($"TravelerSpawner: Active count after: {activeTravelers.Count}");
        }
        else
        {
            Debug.LogWarning($"TravelerSpawner: Tried to return {traveler.name} but it wasn't in active list!");
        }
    }
    
    // Visualize spawn and destination areas in Scene view
    private void OnDrawGizmosSelected()
    {
        // Draw spawn volume (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(spawnCenter, spawnSize);
        
        // Draw destination volume (green)
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(destinationCenter, destinationSize);
        
        // Draw path indicators
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(spawnCenter, destinationCenter);
    }
} 