using UnityEngine;
using UnityEngine.AI;
using System.Collections;
////
public class TravelerMover : MonoBehaviour
{
    private Vector3 destination;
    private float speed;
    private TravelerSpawner spawner;
    private bool isMoving = false;
    private bool destinationSet = false; // Flag to track if destination has been set
    
    // NavMesh components
    private NavMeshAgent navAgent;
    
    // Optional: For more natural movement
    [SerializeField] private bool useNavMesh = true;
    [SerializeField] private bool useSmoothing = true;
    [SerializeField] private float rotationSpeed = 90f; // Degrees per second
    
    private void Awake()
    {
        // Get or add NavMeshAgent component
        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent == null)
        {
            navAgent = gameObject.AddComponent<NavMeshAgent>();
        }
    }
    
    public void Initialize(Vector3 targetDestination, float moveSpeed, TravelerSpawner travelerSpawner)
    {
        Debug.Log($"TravelerMover: Initialize called for {gameObject.name}");
        
        destination = targetDestination;
        speed = moveSpeed;
        spawner = travelerSpawner;
        isMoving = true;
        destinationSet = false; // Reset flag
        
        Debug.Log($"TravelerMover: useNavMesh={useNavMesh}, navAgent null={navAgent == null}");
        
        if (useNavMesh && navAgent != null)
        {
            Debug.Log("TravelerMover: Configuring NavMesh agent...");
            
            // Configure NavMesh agent
            navAgent.enabled = true;
            navAgent.speed = speed;
            navAgent.acceleration = speed * 2f; // Quick acceleration
            navAgent.angularSpeed = 180f; // Fast turning
            navAgent.stoppingDistance = 0.1f;
            navAgent.autoBraking = true;
            
            Debug.Log($"TravelerMover: Agent configured. Starting SetDestinationNextFrame coroutine...");
            // Wait a frame to ensure agent is properly placed, then set destination
            StartCoroutine(SetDestinationNextFrame(destination));
        }
        else
        {
            // Face the destination for non-NavMesh movement
            if (useSmoothing)
            {
                Vector3 direction = (destination - transform.position).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = targetRotation;
                }
            }
        }
    }
    
    private void Update()
    {
        if (!isMoving) return;
        
        if (useNavMesh && navAgent != null && navAgent.enabled)
        {
            CheckNavMeshDestination();
        }
        else
        {
            MoveTraveler();
            CheckIfReachedDestination();
        }
    }
    
    private void MoveTraveler()
    {
        if (useSmoothing)
        {
            // Smooth movement with rotation
            Vector3 direction = (destination - transform.position).normalized;
            
            // Rotate towards movement direction
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, 
                    targetRotation, 
                    rotationSpeed * Time.deltaTime
                );
            }
            
            // Move towards destination
            transform.position = Vector3.MoveTowards(
                transform.position, 
                destination, 
                speed * Time.deltaTime
            );
        }
        else
        {
            // Simple linear movement
            transform.position = Vector3.MoveTowards(
                transform.position, 
                destination, 
                speed * Time.deltaTime
            );
        }
    }
    
    private void CheckNavMeshDestination()
    {
        // Don't check until destination has been set
        if (!destinationSet)
        {
            Debug.Log($"TravelerMover: Destination not set yet for {gameObject.name}, skipping check");
            return;
        }
        
        Debug.Log($"TravelerMover: Checking destination for {gameObject.name} - pathPending={navAgent.pathPending}, remainingDistance={navAgent.remainingDistance}, hasPath={navAgent.hasPath}, velocity={navAgent.velocity.sqrMagnitude}");
        
        // Check if NavMesh agent has reached its destination
        if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
        {
            // If agent has stopped or is very close, return to pool
            if (!navAgent.hasPath || navAgent.velocity.sqrMagnitude < 0.1f)
            {
                Debug.Log($"TravelerMover: {gameObject.name} reached destination or got stuck, returning to pool");
                isMoving = false;
                navAgent.enabled = false; // Disable for pool reuse
                spawner.ReturnTravelerToPool(gameObject);
            }
        }
    }
    
    private void CheckIfReachedDestination()
    {
        float distanceToDestination = Vector3.Distance(transform.position, destination);
        
        // If close enough to destination, return to pool
        if (distanceToDestination < 0.1f)
        {
            isMoving = false;
            spawner.ReturnTravelerToPool(gameObject);
        }
    }
    
    private IEnumerator SetDestinationNextFrame(Vector3 targetDestination)
    {
        Debug.Log($"TravelerMover: SetDestinationNextFrame started for {gameObject.name}");
        
        // Wait a frame to ensure NavMeshAgent is fully initialized
        yield return null;
        
        Debug.Log($"TravelerMover: After wait - agent enabled={navAgent.enabled}, isOnNavMesh={navAgent.isOnNavMesh}");
        
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            Debug.Log($"TravelerMover: Setting destination {targetDestination} for {gameObject.name}");
            navAgent.SetDestination(targetDestination);
            destinationSet = true; // Mark destination as set
            Debug.Log($"TravelerMover: Destination set successfully");
        }
        else
        {
            Debug.LogWarning($"NavMeshAgent on {gameObject.name} is not ready. Falling back to simple movement.");
            // Fallback to simple movement
            useNavMesh = false;
            destinationSet = true; // Even in fallback mode, mark as "set"
        }
    }
    
    // Optional: Visualize destination in Scene view while selected
    private void OnDrawGizmosSelected()
    {
        if (isMoving)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, destination);
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(destination, 0.2f);
        }
    }
} 