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
    public NetworkCardData networkCardData; // Using NetworkCardData for proper network serialization
    
    public QueuedCardPlay(int sourceId, int[] targetIds, GameObject card, CardData data)
    {
        sourceEntityId = sourceId;
        targetEntityIds = targetIds;
        cardObject = card;
        networkCardData = NetworkCardData.FromCardData(data); // Convert to network-safe format
    }
    
    // Helper property to get CardData when needed (creates temporary instance)
    public CardData CardData => networkCardData.ToCardData();
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
        
        // AI_COMBAT_DEBUG: Debug card name at queue entry point
        string cardName = cardData?.CardName ?? "NULL_CARDDATA";
        Debug.Log($"AI_COMBAT_DEBUG: QUEUEING CARD - Entity {sourceEntityId} queuing card '{cardName}' (GameObject: '{cardObject?.name ?? "NULL_GAMEOBJECT"}')");
        
        // Initialize queue for this entity if it doesn't exist
        if (!entityCardQueues.ContainsKey(sourceEntityId))
        {
            entityCardQueues[sourceEntityId] = new List<QueuedCardPlay>();
            Debug.Log($"AI_COMBAT_DEBUG: Created new card queue for entity {sourceEntityId}");
        }
        
        // Create queued card play
        QueuedCardPlay queuedPlay = new QueuedCardPlay(sourceEntityId, targetEntityIds, cardObject, cardData);
        entityCardQueues[sourceEntityId].Add(queuedPlay);
        
        // AI_COMBAT_DEBUG: Debug the queued card play data
        Debug.Log($"AI_COMBAT_DEBUG: CARD QUEUED SUCCESSFULLY - Entity {sourceEntityId} now has {entityCardQueues[sourceEntityId].Count} cards in queue");
        
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
            Initiative = play.CardData?.Initiative ?? 0,
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
        
        if (queuedPlays.Any(q => q.CardData?.Initiative > 0))
        {
            Debug.Log($"CombatCardQueue: Sorted cards by initiative for entity {entityId}:");
            foreach (var play in queuedPlays)
            {
                int initiative = play.CardData?.Initiative ?? 0;
                Debug.Log($"  - {play.CardData?.CardName} (Initiative: {initiative})");
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
                    Debug.Log($"CombatCardQueue: Executing queued card play: {queuedPlay.CardData.CardName}");
                    
                    // Execute the card play directly (bypass queuing)
                    cardPlayHandler.ExecuteCardPlayDirectly(queuedPlay.sourceEntityId, queuedPlay.targetEntityIds);
                    
                    // EVENT-DRIVEN: Wait for card animation completion instead of fixed delay
                    yield return StartCoroutine(WaitForCardExecutionComplete(queuedPlay.cardObject));
                }
                else
                {
                    Debug.LogError($"CombatCardQueue: HandleCardPlay component missing on queued card {queuedPlay.CardData.CardName}");
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
                string cardName = queue[i].CardData != null ? queue[i].CardData.CardName : "Unknown";
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
        
        Debug.Log($"AI_COMBAT_DEBUG: CHECKING QUEUES - Found {entityCardQueues.Count} entities with potential queues");
        
        foreach (var entityQueue in entityCardQueues)
        {
            int entityId = entityQueue.Key;
            List<QueuedCardPlay> queuedPlays = entityQueue.Value;
            
            Debug.Log($"AI_COMBAT_DEBUG: Entity {entityId} has {queuedPlays.Count} cards in queue");
            
            if (queuedPlays.Count > 0)
            {
                Debug.Log($"AI_COMBAT_DEBUG: Found {queuedPlays.Count} queued cards for entity {entityId}");
                foreach (var play in queuedPlays)
                {
                    Debug.Log($"AI_COMBAT_DEBUG: - Card: {play.CardData?.CardName ?? "NULL"}");
                }
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
            Initiative = play.CardData?.Initiative ?? 0,
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
            int initiative = play.CardData?.Initiative ?? 0;
            Debug.Log($"  {i + 1}. Entity {play.sourceEntityId}: {play.CardData?.CardName} (Initiative: {initiative})");
        }
        
        // Notify visualization system about the queue order
        OnQueueVisualizationReady?.Invoke(allQueuedPlays);
        RpcNotifyQueueVisualizationReady(allQueuedPlays);
        
        // QUEUENAME: Debug queue order before visualization
        Debug.Log($"QUEUENAME: About to send visualization RPC with {allQueuedPlays.Count} cards:");
        for (int i = 0; i < allQueuedPlays.Count; i++)
        {
            var play = allQueuedPlays[i];
            Debug.Log($"QUEUENAME: Queue index {i} - CardData.CardName='{play.CardData?.CardName ?? "NULL"}', GameObject.name='{play.cardObject?.name ?? "NULL"}'");
        }
        
        // Wait for visualization setup to complete
        float visualizationSetupDelay = 1f + (allQueuedPlays.Count * 0.15f); // Base delay + stagger time
        Debug.Log($"CombatCardQueue: Waiting {visualizationSetupDelay:F1}s for visualization setup to complete");
        yield return new WaitForSeconds(visualizationSetupDelay);
        
        // Execute each queued card play in global initiative order
        for (int i = 0; i < allQueuedPlays.Count; i++)
        {
            QueuedCardPlay queuedPlay = allQueuedPlays[i];
            
            Debug.Log($"AI_COMBAT_DEBUG: Processing card {i + 1}/{allQueuedPlays.Count} - Entity {queuedPlay.sourceEntityId}, Card: {queuedPlay.CardData?.CardName ?? "NULL"}");
            
            // Check if this card's fight is still active before executing
            if (!ShouldExecuteCard(queuedPlay))
            {
                Debug.Log($"AI_COMBAT_DEBUG: SKIPPING card - fight has ended for entity {queuedPlay.sourceEntityId}");
                Debug.Log($"CombatCardQueue: Skipping card {i + 1}/{allQueuedPlays.Count}: {queuedPlay.CardData.CardName} from entity {queuedPlay.sourceEntityId} - fight has ended");
                
                // Still notify visualization system that this card was "executed" (skipped)
                OnCardExecuted?.Invoke(queuedPlay.CardData.CardName, i);
                RpcNotifyCardExecuted(queuedPlay.CardData.CardName, i);
                
                continue;
            }
            
            Debug.Log($"AI_COMBAT_DEBUG: Card fight check passed - proceeding with execution");
            
            if (queuedPlay.cardObject != null)
            {
                Debug.Log($"AI_COMBAT_DEBUG: Card object found: {queuedPlay.cardObject.name} (ID: {queuedPlay.cardObject.GetInstanceID()})");
                
                HandleCardPlay cardPlayHandler = queuedPlay.cardObject.GetComponent<HandleCardPlay>();
                if (cardPlayHandler != null)
                {
                    Debug.Log($"AI_COMBAT_DEBUG: HandleCardPlay component found on {queuedPlay.cardObject.name}");
                    Debug.Log($"CombatCardQueue: Executing card {i + 1}/{allQueuedPlays.Count}: {queuedPlay.CardData.CardName} from entity {queuedPlay.sourceEntityId}");
                    
                    // Debug target entities
                    if (queuedPlay.targetEntityIds != null && queuedPlay.targetEntityIds.Length > 0)
                    {
                        Debug.Log($"AI_COMBAT_DEBUG: Executing with {queuedPlay.targetEntityIds.Length} targets: [{string.Join(", ", queuedPlay.targetEntityIds)}]");
                    }
                    else
                    {
                        Debug.Log($"AI_COMBAT_DEBUG: Executing with NO targets (self-target or no-target card)");
                    }
                    
                    // Execute the card play directly (bypass queuing)
                    Debug.Log($"AI_COMBAT_DEBUG: Calling ExecuteCardPlayDirectly...");
                    
                    bool executionSucceeded = false;
                    try
                    {
                        cardPlayHandler.ExecuteCardPlayDirectly(queuedPlay.sourceEntityId, queuedPlay.targetEntityIds);
                        executionSucceeded = true;
                        Debug.Log($"AI_COMBAT_DEBUG: ExecuteCardPlayDirectly completed successfully");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"AI_COMBAT_DEBUG: EXCEPTION during ExecuteCardPlayDirectly for {queuedPlay.CardData.CardName}: {ex.Message}");
                        Debug.LogError($"AI_COMBAT_DEBUG: Exception stack trace: {ex.StackTrace}");
                        executionSucceeded = false;
                    }
                    
                    if (executionSucceeded)
                    {
                        // EVENT-DRIVEN: Wait for card animation completion instead of fixed delay
                        yield return StartCoroutine(WaitForCardExecutionComplete(queuedPlay.cardObject));
                        
                        // Notify visualization system that this card finished executing
                        OnCardExecuted?.Invoke(queuedPlay.CardData.CardName, i);
                        RpcNotifyCardExecuted(queuedPlay.CardData.CardName, i);
                        
                        Debug.Log($"AI_COMBAT_DEBUG: Card execution fully completed for {queuedPlay.CardData.CardName}");
                        
                        // Add delay between card executions (except after the last card)
                        if (i < allQueuedPlays.Count - 1 && delayBetweenCardExecutions > 0f)
                        {
                            Debug.Log($"CombatCardQueue: Waiting {delayBetweenCardExecutions}s before next card execution");
                            yield return new WaitForSeconds(delayBetweenCardExecutions);
                        }
                    }
                    else
                    {
                        Debug.LogError($"AI_COMBAT_DEBUG: Card execution FAILED for {queuedPlay.CardData.CardName} - skipping animation and continuing");
                    }
                }
                else
                {
                    Debug.LogError($"AI_COMBAT_DEBUG: MISSING HandleCardPlay component on card object {queuedPlay.cardObject.name}!");
                    Debug.LogError($"CombatCardQueue: HandleCardPlay component missing on queued card {queuedPlay.CardData.CardName}");
                }
            }
            else
            {
                Debug.LogError($"AI_COMBAT_DEBUG: CARD OBJECT IS NULL for entity {queuedPlay.sourceEntityId}, card {queuedPlay.CardData?.CardName ?? "NULL"}!");
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
        
        // QUEUENAME: Debug received RPC data
        Debug.Log($"QUEUENAME: RpcNotifyQueueVisualizationReady received {sortedPlays?.Count ?? 0} cards:");
        if (sortedPlays != null)
        {
            for (int i = 0; i < sortedPlays.Count; i++)
            {
                var play = sortedPlays[i];
                Debug.Log($"QUEUENAME: RPC index {i} - CardData.CardName='{play.CardData?.CardName ?? "NULL"}'");
            }
        }
        
        OnQueueVisualizationReady?.Invoke(sortedPlays);
    }

    /// <summary>
    /// Notifies all clients that a specific card finished executing
    /// </summary>
    [ObserversRpc]
    private void RpcNotifyCardExecuted(string cardName, int executionIndex)
    {
        // QUEUENAME: Debug RPC card execution notification
        Debug.Log($"QUEUENAME: RpcNotifyCardExecuted received - cardName='{cardName}', executionIndex={executionIndex}");
        
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

    /// <summary>
    /// Removes all queued cards for entities involved in a specific fight
    /// Called when a fight ends during card execution
    /// </summary>
    [Server]
    public void RemoveCardsForEndedFight(int playerEntityId, int petEntityId)
    {
        if (!IsServerInitialized) return;
        
        int removedCount = 0;
        
        // Remove cards for the player
        if (entityCardQueues.ContainsKey(playerEntityId))
        {
            int playerCardCount = entityCardQueues[playerEntityId].Count;
            removedCount += playerCardCount;
            entityCardQueues[playerEntityId].Clear();
            Debug.Log($"CombatCardQueue: Removed {playerCardCount} queued cards for player entity {playerEntityId}");
        }
        
        // Remove cards for the pet
        if (entityCardQueues.ContainsKey(petEntityId))
        {
            int petCardCount = entityCardQueues[petEntityId].Count;
            removedCount += petCardCount;
            entityCardQueues[petEntityId].Clear();
            Debug.Log($"CombatCardQueue: Removed {petCardCount} queued cards for pet entity {petEntityId}");
        }
        
        if (removedCount > 0)
        {
            Debug.Log($"CombatCardQueue: Removed {removedCount} total queued cards for ended fight (player: {playerEntityId}, pet: {petEntityId})");
        }
    }

    /// <summary>
    /// Checks if a card should still be executed based on whether its associated fight is still active
    /// </summary>
    [Server]
    public bool ShouldExecuteCard(QueuedCardPlay queuedPlay)
    {
        if (!IsServerInitialized) return false;
        
        // Find the CombatManager to check if the fight is still active
        CombatManager combatManager = FindFirstObjectByType<CombatManager>();
        if (combatManager == null)
        {
            Debug.LogWarning("CombatCardQueue: CombatManager not found, assuming card should execute");
            return true;
        }
        
        // Check if the source entity's fight is still active
        return combatManager.IsFightActive(queuedPlay.sourceEntityId);
    }

    #region Unity Lifecycle
    
    protected override void OnValidate()
    {
        base.OnValidate();
        
        // Ensure delay is non-negative
        delayBetweenCardExecutions = Mathf.Max(0f, delayBetweenCardExecutions);
    }
    
    #endregion
} 