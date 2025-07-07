using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;

/// <summary>
/// Data structure to hold a queued card play
/// </summary>
[System.Serializable]
public struct QueuedCardPlay
{
    public int sourceEntityId;
    public int[] targetEntityIds;
    public GameObject cardObject;
    public CardData cardData;
    
    public QueuedCardPlay(int sourceId, int[] targetIds, GameObject card, CardData data)
    {
        sourceEntityId = sourceId;
        targetEntityIds = targetIds;
        cardObject = card;
        cardData = data;
    }
}

/// <summary>
/// Manages queued card plays for entities during combat turns.
/// Cards are queued instead of executed immediately, then all execute at turn end.
/// </summary>
public class CombatCardQueue : NetworkBehaviour
{
    [Header("Execution Timing")]
    [SerializeField] private float delayBetweenCardExecutions = 0.5f;
    
    // Queue of card plays for each entity
    private readonly Dictionary<int, List<QueuedCardPlay>> entityCardQueues = new Dictionary<int, List<QueuedCardPlay>>();
    
    // Singleton instance
    private static CombatCardQueue instance;
    public static CombatCardQueue Instance => instance;
    
    // Events for tracking execution completion
    public static event Action OnCardExecutionStarted;
    public static event Action OnCardExecutionCompleted;
    
    // Events for queue visualization
    public static event Action<List<QueuedCardPlay>> OnQueueVisualizationReady;
    public static event Action<string, int> OnCardExecuted; // Pass card name and execution index
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Queues a card play for execution later
    /// </summary>
    [Server]
    public void QueueCardPlay(int sourceEntityId, int[] targetEntityIds, GameObject cardObject, CardData cardData)
    {
        if (!IsServerInitialized)
        {
            Debug.LogError("CombatCardQueue: Cannot queue card play - server not initialized");
            return;
        }
        
        // Initialize queue for this entity if it doesn't exist
        if (!entityCardQueues.ContainsKey(sourceEntityId))
        {
            entityCardQueues[sourceEntityId] = new List<QueuedCardPlay>();
        }
        
        // Create queued card play
        QueuedCardPlay queuedPlay = new QueuedCardPlay(sourceEntityId, targetEntityIds, cardObject, cardData);
        entityCardQueues[sourceEntityId].Add(queuedPlay);
        
        Debug.Log($"CombatCardQueue: Queued card play for entity {sourceEntityId} - {cardData.CardName}. Queue size: {entityCardQueues[sourceEntityId].Count}");
    }
    
    /// <summary>
    /// Executes all queued card plays for an entity
    /// </summary>
    [Server]
    public IEnumerator ExecuteQueuedCardPlays(int entityId)
    {
        if (!IsServerInitialized)
        {
            Debug.LogError("CombatCardQueue: Cannot execute queued plays - server not initialized");
            yield break;
        }
        
        if (!entityCardQueues.ContainsKey(entityId) || entityCardQueues[entityId].Count == 0)
        {
            Debug.Log($"CombatCardQueue: No queued card plays for entity {entityId}");
            yield break;
        }
        
        List<QueuedCardPlay> queuedPlays = entityCardQueues[entityId];
        Debug.Log($"CombatCardQueue: Executing {queuedPlays.Count} queued card plays for entity {entityId}");
        
        // Sort queued plays by initiative (higher initiative executes first)
        // For ties, use random ordering
        var random = new System.Random();
        var playWithRandomTiebreaker = queuedPlays.Select(play => new {
            Play = play,
            Initiative = play.cardData?.Initiative ?? 0,
            RandomTiebreaker = random.Next()
        }).ToList();
        
        playWithRandomTiebreaker.Sort((a, b) => {
            // Sort in descending order (higher initiative first)
            if (a.Initiative != b.Initiative)
                return b.Initiative.CompareTo(a.Initiative);
            
            // If initiative is the same, use random tiebreaker
            return a.RandomTiebreaker.CompareTo(b.RandomTiebreaker);
        });
        
        // Extract the sorted plays
        queuedPlays = playWithRandomTiebreaker.Select(item => item.Play).ToList();
        
        if (queuedPlays.Any(q => q.cardData?.Initiative > 0))
        {
            Debug.Log($"CombatCardQueue: Sorted cards by initiative for entity {entityId}:");
            foreach (var play in queuedPlays)
            {
                int initiative = play.cardData?.Initiative ?? 0;
                Debug.Log($"  - {play.cardData?.CardName} (Initiative: {initiative})");
            }
        }
        
        // Execute each queued card play in initiative order
        foreach (QueuedCardPlay queuedPlay in queuedPlays)
        {
            if (queuedPlay.cardObject != null)
            {
                HandleCardPlay cardPlayHandler = queuedPlay.cardObject.GetComponent<HandleCardPlay>();
                if (cardPlayHandler != null)
                {
                    Debug.Log($"CombatCardQueue: Executing queued card play: {queuedPlay.cardData.CardName}");
                    
                    // Execute the card play directly (bypass queuing)
                    cardPlayHandler.ExecuteCardPlayDirectly(queuedPlay.sourceEntityId, queuedPlay.targetEntityIds);
                    
                    // EVENT-DRIVEN: Wait for card animation completion instead of fixed delay
                    yield return StartCoroutine(WaitForCardExecutionComplete(queuedPlay.cardObject));
                }
                else
                {
                    Debug.LogError($"CombatCardQueue: HandleCardPlay component missing on queued card {queuedPlay.cardData.CardName}");
                }
            }
        }
        
        // Clear the queue after execution
        entityCardQueues[entityId].Clear();
        Debug.Log($"CombatCardQueue: Cleared queue for entity {entityId}");
    }
    
    /// <summary>
    /// Gets the number of queued card plays for an entity
    /// </summary>
    public int GetQueuedCardCount(int entityId)
    {
        if (entityCardQueues.ContainsKey(entityId))
        {
            return entityCardQueues[entityId].Count;
        }
        return 0;
    }
    
    /// <summary>
    /// Gets all queued card plays for an entity
    /// </summary>
    public List<QueuedCardPlay> GetQueuedCardPlays(int entityId)
    {
        if (entityCardQueues.ContainsKey(entityId))
        {
            return new List<QueuedCardPlay>(entityCardQueues[entityId]);
        }
        return new List<QueuedCardPlay>();
    }
    
    /// <summary>
    /// Clears all queued card plays for an entity
    /// </summary>
    [Server]
    public void ClearQueuedCardPlays(int entityId)
    {
        if (!IsServerInitialized) return;
        
        if (entityCardQueues.ContainsKey(entityId))
        {
            entityCardQueues[entityId].Clear();
            Debug.Log($"CombatCardQueue: Cleared queued card plays for entity {entityId}");
        }
    }
    
    /// <summary>
    /// Removes a specific queued card play for an entity
    /// </summary>
    [Server]
    public bool RemoveQueuedCard(int entityId, GameObject cardObject)
    {
        if (!IsServerInitialized) return false;
        
        if (!entityCardQueues.ContainsKey(entityId))
        {
            Debug.LogWarning($"CombatCardQueue: No queue found for entity {entityId}");
            return false;
        }
        
        List<QueuedCardPlay> queue = entityCardQueues[entityId];
        
        // Find and remove the specific card
        for (int i = queue.Count - 1; i >= 0; i--)
        {
            if (queue[i].cardObject == cardObject)
            {
                string cardName = queue[i].cardData != null ? queue[i].cardData.CardName : "Unknown";
                queue.RemoveAt(i);
                Debug.Log($"CombatCardQueue: Removed queued card {cardName} for entity {entityId}. Queue size: {queue.Count}");
                return true;
            }
        }
        
        Debug.LogWarning($"CombatCardQueue: Card not found in queue for entity {entityId}");
        return false;
    }
    
    /// <summary>
    /// Executes all queued card plays from all entities in global initiative order
    /// </summary>
    [Server]
    public IEnumerator ExecuteAllQueuedCardPlaysInInitiativeOrder()
    {
        if (!IsServerInitialized)
        {
            Debug.LogError("CombatCardQueue: Cannot execute queued plays - server not initialized");
            yield break;
        }
        
        // Notify that card execution is starting
        OnCardExecutionStarted?.Invoke();
        RpcNotifyCardExecutionStarted();
        
        // Collect all queued card plays from all entities
        List<QueuedCardPlay> allQueuedPlays = new List<QueuedCardPlay>();
        
        foreach (var entityQueue in entityCardQueues)
        {
            int entityId = entityQueue.Key;
            List<QueuedCardPlay> queuedPlays = entityQueue.Value;
            
            if (queuedPlays.Count > 0)
            {
                Debug.Log($"CombatCardQueue: Found {queuedPlays.Count} queued cards for entity {entityId}");
                allQueuedPlays.AddRange(queuedPlays);
            }
        }
        
        if (allQueuedPlays.Count == 0)
        {
            Debug.Log("CombatCardQueue: No queued card plays found across all entities");
            // Still notify completion even if no cards to execute
            OnCardExecutionCompleted?.Invoke();
            RpcNotifyCardExecutionCompleted();
            yield break;
        }
        
        Debug.Log($"CombatCardQueue: Executing {allQueuedPlays.Count} total queued card plays in global initiative order");
        
        // Sort all card plays by initiative (higher initiative executes first)
        // For ties, we'll assign random values and sort by those
        var random = new System.Random();
        var playWithRandomTiebreaker = allQueuedPlays.Select(play => new {
            Play = play,
            Initiative = play.cardData?.Initiative ?? 0,
            RandomTiebreaker = random.Next()
        }).ToList();
        
        playWithRandomTiebreaker.Sort((a, b) => {
            // Sort in descending order (higher initiative first)
            if (a.Initiative != b.Initiative)
                return b.Initiative.CompareTo(a.Initiative);
            
            // If initiative is the same, use random tiebreaker
            return a.RandomTiebreaker.CompareTo(b.RandomTiebreaker);
        });
        
        // Extract the sorted plays
        allQueuedPlays = playWithRandomTiebreaker.Select(item => item.Play).ToList();
        
        // Log the execution order
        Debug.Log("CombatCardQueue: Global initiative execution order:");
        for (int i = 0; i < allQueuedPlays.Count; i++)
        {
            var play = allQueuedPlays[i];
            int initiative = play.cardData?.Initiative ?? 0;
            Debug.Log($"  {i + 1}. Entity {play.sourceEntityId}: {play.cardData?.CardName} (Initiative: {initiative})");
        }
        
        // Notify visualization system about the queue order
        OnQueueVisualizationReady?.Invoke(allQueuedPlays);
        RpcNotifyQueueVisualizationReady(allQueuedPlays);
        
        // Wait for visualization setup to complete
        float visualizationSetupDelay = 1f + (allQueuedPlays.Count * 0.15f); // Base delay + stagger time
        Debug.Log($"CombatCardQueue: Waiting {visualizationSetupDelay:F1}s for visualization setup to complete");
        yield return new WaitForSeconds(visualizationSetupDelay);
        
        // Execute each queued card play in global initiative order
        for (int i = 0; i < allQueuedPlays.Count; i++)
        {
            QueuedCardPlay queuedPlay = allQueuedPlays[i];
            
            if (queuedPlay.cardObject != null)
            {
                HandleCardPlay cardPlayHandler = queuedPlay.cardObject.GetComponent<HandleCardPlay>();
                if (cardPlayHandler != null)
                {
                    Debug.Log($"CombatCardQueue: Executing card {i + 1}/{allQueuedPlays.Count}: {queuedPlay.cardData.CardName} from entity {queuedPlay.sourceEntityId}");
                    
                    // Execute the card play directly (bypass queuing)
                    cardPlayHandler.ExecuteCardPlayDirectly(queuedPlay.sourceEntityId, queuedPlay.targetEntityIds);
                    
                    // EVENT-DRIVEN: Wait for card animation completion instead of fixed delay
                    yield return StartCoroutine(WaitForCardExecutionComplete(queuedPlay.cardObject));
                    
                    // Notify visualization system that this card finished executing
                    OnCardExecuted?.Invoke(queuedPlay.cardData.CardName, i);
                    RpcNotifyCardExecuted(queuedPlay.cardData.CardName, i);
                    
                    // Add delay between card executions (except after the last card)
                    if (i < allQueuedPlays.Count - 1 && delayBetweenCardExecutions > 0f)
                    {
                        Debug.Log($"CombatCardQueue: Waiting {delayBetweenCardExecutions}s before next card execution");
                        yield return new WaitForSeconds(delayBetweenCardExecutions);
                    }
                }
                else
                {
                    Debug.LogError($"CombatCardQueue: HandleCardPlay component missing on queued card {queuedPlay.cardData.CardName}");
                }
            }
        }
        
        // Clear all queues after execution
        foreach (var entityQueue in entityCardQueues)
        {
            entityQueue.Value.Clear();
        }
        
        Debug.Log("CombatCardQueue: Global initiative execution complete, all queues cleared");
        
        // Notify that card execution is completed
        OnCardExecutionCompleted?.Invoke();
        RpcNotifyCardExecutionCompleted();
    }
    
    /// <summary>
    /// Waits for a card's execution animations to complete
    /// EVENT-DRIVEN: Monitors actual card animation state instead of hardcoded delay
    /// </summary>
    private IEnumerator WaitForCardExecutionComplete(GameObject cardObject)
    {
        if (cardObject == null)
        {
            yield break;
        }
        
        CardAnimator cardAnimator = cardObject.GetComponent<CardAnimator>();
        float startTime = Time.time;
        const float maxWaitTime = 2f; // Safety fallback
        const float minWaitTime = 0.1f; // Minimum visual pause between cards
        
        // Ensure minimum visual pause
        yield return new WaitForSeconds(minWaitTime);
        
        // If card has animator, wait for animation completion
        if (cardAnimator != null)
        {
            while (cardAnimator.IsAnimating && Time.time - startTime < maxWaitTime)
            {
                yield return null; // Check every frame
            }
            
            float actualWaitTime = Time.time - startTime;
            if (cardAnimator.IsAnimating)
            {
                Debug.LogWarning($"CombatCardQueue: Card execution timeout after {actualWaitTime:F2}s for {cardObject.name}");
            }
            else
            {
                Debug.Log($"CombatCardQueue: Card execution completed after {actualWaitTime:F2}s for {cardObject.name}");
            }
        }
        else
        {
            // No animator, just use minimum wait
            Debug.Log($"CombatCardQueue: No CardAnimator found for {cardObject.name}, using minimum wait");
        }
    }
    
    /// <summary>
    /// Notifies all clients that card execution is starting
    /// </summary>
    [ObserversRpc]
    private void RpcNotifyCardExecutionStarted()
    {
        Debug.Log("CombatCardQueue (Client): Card execution started");
        OnCardExecutionStarted?.Invoke();
    }
    
    /// <summary>
    /// Notifies all clients that card execution is completed
    /// </summary>
    [ObserversRpc]
    private void RpcNotifyCardExecutionCompleted()
    {
        Debug.Log("CombatCardQueue (Client): Card execution completed");
        OnCardExecutionCompleted?.Invoke();
    }

    /// <summary>
    /// Notifies all clients about the queue visualization order
    /// </summary>
    [ObserversRpc]
    private void RpcNotifyQueueVisualizationReady(List<QueuedCardPlay> sortedPlays)
    {
        Debug.Log($"CombatCardQueue (Client): Queue visualization ready with {sortedPlays.Count} cards");
        OnQueueVisualizationReady?.Invoke(sortedPlays);
    }

    /// <summary>
    /// Notifies all clients that a specific card finished executing
    /// </summary>
    [ObserversRpc]
    private void RpcNotifyCardExecuted(string cardName, int executionIndex)
    {
        Debug.Log($"CombatCardQueue (Client): Card executed - {cardName} (index: {executionIndex})");
        OnCardExecuted?.Invoke(cardName, executionIndex);
    }

    /// <summary>
    /// Clears all queued card plays for all entities
    /// </summary>
    [Server]
    public void ClearAllQueuedCardPlays()
    {
        if (!IsServerInitialized) return;
        
        entityCardQueues.Clear();
        Debug.Log("CombatCardQueue: Cleared all queued card plays");
    }

    #region Unity Lifecycle
    
    private void OnValidate()
    {
        // Ensure delay is non-negative
        delayBetweenCardExecutions = Mathf.Max(0f, delayBetweenCardExecutions);
    }
    
    #endregion
} 