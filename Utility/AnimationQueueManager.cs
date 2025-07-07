using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MVPScripts.Utility
{
    /// <summary>
    /// Generic animation queue manager that respects running animations and optimizes transitions.
    /// Can be used by any component that needs to queue and manage animations without interrupting them.
    /// </summary>
    /// <typeparam name="TRequest">The type of animation request to handle</typeparam>
    public class AnimationQueueManager<TRequest> where TRequest : class
    {
        #region Animation State Management
        
        /// <summary>
        /// Current state of the animation system
        /// </summary>
        public enum AnimationState
        {
            Idle,           // No animation running - can start immediately
            FadingOut,      // Currently fading out - CANNOT interrupt
            FadingIn        // Currently fading in - CANNOT interrupt  
        }
        
        /// <summary>
        /// Current animation state - publicly readable
        /// </summary>
        public AnimationState CurrentAnimationState { get; private set; } = AnimationState.Idle;
        
        /// <summary>
        /// Whether an animation is currently running
        /// </summary>
        public bool IsAnimating => CurrentAnimationState != AnimationState.Idle;
        
        #endregion
        
        #region Queue Management
        
        private Queue<TRequest> requestQueue = new Queue<TRequest>();
        private bool isProcessingQueue = false;
        private bool isStartingProcessing = false; // New flag to prevent reentrant processing start
        private MonoBehaviour owner; // The MonoBehaviour that owns this queue manager
        private Coroutine queueProcessingCoroutine = null;
        private string instanceId; // Unique identifier for this instance
        
        /// <summary>
        /// Number of requests currently in the queue
        /// </summary>
        public int QueueCount => requestQueue.Count;
        
        #endregion
        
        #region Delegates for Customization
        
        /// <summary>
        /// Delegate to execute a single animation request
        /// </summary>
        public System.Func<TRequest, IEnumerator> ExecuteRequest { get; set; }
        
        /// <summary>
        /// Delegate to optimize the queue (remove redundant requests)
        /// </summary>
        public System.Func<List<TRequest>, List<TRequest>> OptimizeQueue { get; set; }
        
        /// <summary>
        /// Delegate to check if a fade out is needed for proper animation flow
        /// </summary>
        public System.Func<TRequest, bool> RequiresFadeOut { get; set; }
        
        /// <summary>
        /// Delegate to create a fade out request for proper animation flow
        /// </summary>
        public System.Func<TRequest> CreateFadeOutRequest { get; set; }
        
        /// <summary>
        /// Delegate to create a modified version of a request that doesn't require fade out
        /// </summary>
        public System.Func<TRequest, TRequest> CreateModifiedRequestWithoutFadeOut { get; set; }
        
        /// <summary>
        /// Delegate called when animation state changes
        /// </summary>
        public System.Action<AnimationState> OnAnimationStateChanged { get; set; }
        
        /// <summary>
        /// Delegate called when queue processing starts
        /// </summary>
        public System.Action<int> OnQueueProcessingStarted { get; set; }
        
        /// <summary>
        /// Delegate called when queue processing completes
        /// </summary>
        public System.Action OnQueueProcessingCompleted { get; set; }
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initialize the animation queue manager
        /// </summary>
        /// <param name="owner">The MonoBehaviour that will run the coroutines</param>
        public AnimationQueueManager(MonoBehaviour owner)
        {
            this.owner = owner;
            this.instanceId = $"{owner?.name ?? "Unknown"}_{System.DateTime.Now.Ticks}"; // Generate a unique ID with owner name
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Queue an animation request
        /// </summary>
        /// <param name="request">The animation request to queue</param>
        public void QueueRequest(TRequest request)
        {
            if (request == null)
            {
                Debug.LogWarning($"AnimationQueueManager[{instanceId}]: Cannot queue null request");
                return;
            }
            
            Debug.Log($"AnimationQueueManager[{instanceId}]: QueueRequest called - Current state: {CurrentAnimationState}, Queue size: {requestQueue.Count}, Processing: {isProcessingQueue}, Starting: {isStartingProcessing}");
            
            // Add request to queue
            requestQueue.Enqueue(request);
            
            // Start processing if not already running and not in the process of starting
            if (!isProcessingQueue && !isStartingProcessing && owner != null)
            {
                Debug.Log($"AnimationQueueManager[{instanceId}]: Starting new queue processing coroutine");
                isStartingProcessing = true;
                queueProcessingCoroutine = owner.StartCoroutine(ProcessQueueRespectingAnimations());
            }
            else if (isProcessingQueue)
            {
                Debug.Log($"AnimationQueueManager[{instanceId}]: Queue processing already in progress, request added to existing queue");
            }
            else if (isStartingProcessing)
            {
                Debug.Log($"AnimationQueueManager[{instanceId}]: Queue processing start already in progress, request added to queue");
            }
        }
        
        /// <summary>
        /// Clear all queued requests (does not stop current animation)
        /// </summary>
        public void ClearQueue()
        {
            Debug.Log($"AnimationQueueManager: Clearing queue with {requestQueue.Count} requests");
            requestQueue.Clear();
        }
        
        /// <summary>
        /// Stop all processing and clear queue
        /// </summary>
        public void StopAndClear()
        {
            Debug.Log("AnimationQueueManager: Stopping all processing and clearing queue");
            
            // Stop queue processing coroutine
            if (queueProcessingCoroutine != null && owner != null)
            {
                owner.StopCoroutine(queueProcessingCoroutine);
                queueProcessingCoroutine = null;
            }
            
            // Clear queue and reset state
            requestQueue.Clear();
            isProcessingQueue = false;
            isStartingProcessing = false;
            SetAnimationState(AnimationState.Idle);
        }
        
        /// <summary>
        /// Manually set the animation state (use with caution)
        /// </summary>
        /// <param name="newState">The new animation state</param>
        public void SetAnimationState(AnimationState newState)
        {
            if (CurrentAnimationState != newState)
            {
                Debug.Log($"AnimationQueueManager[{instanceId}]: Animation state changed: {CurrentAnimationState} → {newState}");
                CurrentAnimationState = newState;
                OnAnimationStateChanged?.Invoke(newState);
            }
        }
        
        #endregion
        
        #region Queue Processing
        
        /// <summary>
        /// Process the animation queue while respecting running animations
        /// </summary>
        private IEnumerator ProcessQueueRespectingAnimations()
        {
            // Clear the starting flag since we're now actually processing
            isStartingProcessing = false;
            
            // CRITICAL SAFETY CHECK: Don't start processing if queue is already empty
            if (requestQueue.Count == 0)
            {
                Debug.Log($"AnimationQueueManager[{instanceId}]: ProcessQueueRespectingAnimations called with empty queue, exiting immediately");
                isProcessingQueue = false;
                queueProcessingCoroutine = null;
                yield break;
            }
            
            isProcessingQueue = true;
            OnQueueProcessingStarted?.Invoke(requestQueue.Count);
            
            Debug.Log($"AnimationQueueManager[{instanceId}]: Starting queue processing with {requestQueue.Count} requests");
            
            while (requestQueue.Count > 0)
            {
                // Wait for current animation to complete before processing next request
                yield return new WaitUntil(() => CurrentAnimationState == AnimationState.Idle);
                
                // Check if queue was cleared while waiting
                if (requestQueue.Count == 0) 
                {
                    Debug.Log($"AnimationQueueManager[{instanceId}]: Queue became empty while waiting, stopping processing");
                    break;
                }
                
                // Optimize queue before processing next request
                OptimizeQueueRespectingAnimations();
                
                // Check again after optimization - CRITICAL SAFETY CHECK
                if (requestQueue.Count == 0) 
                {
                    Debug.Log($"AnimationQueueManager[{instanceId}]: Queue became empty after optimization, stopping processing");
                    break;
                }
                
                // Get next request
                TRequest nextRequest = requestQueue.Dequeue();
                Debug.Log($"AnimationQueueManager[{instanceId}]: Processing next request - Queue remaining: {requestQueue.Count}");
                
                // SAFETY CHECK: Ensure we have a valid request
                if (nextRequest == null)
                {
                    Debug.LogWarning($"AnimationQueueManager[{instanceId}]: Dequeued null request, skipping");
                    continue;
                }
                
                // Check if we need to add a fade out for proper animation flow
                // This may return a fade out request instead of the original request
                TRequest requestToProcess = HandleAnimationFlowOptimization(nextRequest);
                
                // SAFETY CHECK: Ensure we still have a valid request after optimization
                if (requestToProcess == null)
                {
                    Debug.LogWarning($"AnimationQueueManager[{instanceId}]: Request became null after flow optimization, skipping");
                    continue;
                }
                
                // Execute the request (either original or fade out)
                if (ExecuteRequest != null)
                {
                    Debug.Log($"AnimationQueueManager[{instanceId}]: Executing request, queue size before execution: {requestQueue.Count}");
                    yield return owner.StartCoroutine(ExecuteRequest(requestToProcess));
                    Debug.Log($"AnimationQueueManager[{instanceId}]: Request execution completed, queue size: {requestQueue.Count}");
                }
                else
                {
                    Debug.LogWarning($"AnimationQueueManager[{instanceId}]: No ExecuteRequest delegate set, skipping request");
                }
                
                // SAFETY CHECK: Prevent infinite loops by checking if we're in a bad state
                if (requestQueue.Count == 0 && CurrentAnimationState == AnimationState.Idle)
                {
                    Debug.Log($"AnimationQueueManager[{instanceId}]: Queue empty and state idle after processing, exiting loop");
                    break;
                }
            }
            
            isProcessingQueue = false;
            queueProcessingCoroutine = null;
            OnQueueProcessingCompleted?.Invoke();
            
            Debug.Log($"AnimationQueueManager[{instanceId}]: Queue processing completed");
        }
        
        /// <summary>
        /// Optimize the queue by removing redundant requests while respecting animations
        /// </summary>
        private void OptimizeQueueRespectingAnimations()
        {
            if (requestQueue.Count <= 1) return;
            
            Debug.Log($"AnimationQueueManager: Optimizing queue with {requestQueue.Count} requests");
            
            // Convert queue to list for optimization
            var requests = new List<TRequest>(requestQueue);
            requestQueue.Clear();
            
            // Apply custom optimization if provided
            if (OptimizeQueue != null)
            {
                requests = OptimizeQueue(requests);
            }
            
            // Rebuild queue with optimized requests
            foreach (var request in requests)
            {
                requestQueue.Enqueue(request);
            }
            
            Debug.Log($"AnimationQueueManager: Queue optimized to {requestQueue.Count} requests");
        }
        
        /// <summary>
        /// Check if we need to add a fade out before processing the next request
        /// Returns the actual request that should be processed next
        /// </summary>
        private TRequest HandleAnimationFlowOptimization(TRequest nextRequest)
        {
            // CRITICAL SAFETY CHECK: Don't process flow optimization if we don't have a valid request
            if (nextRequest == null)
            {
                Debug.LogWarning($"AnimationQueueManager[{instanceId}]: HandleAnimationFlowOptimization called with null request, returning null");
                return null;
            }
            
            Debug.Log($"AnimationQueueManager[{instanceId}]: HandleAnimationFlowOptimization - Processing request, RequiresFadeOut: {RequiresFadeOut?.Invoke(nextRequest)}, Current queue size: {requestQueue.Count}");
            
            // Only add fade out if we're idle and the request requires it
            if (CurrentAnimationState == AnimationState.Idle && 
                RequiresFadeOut != null && RequiresFadeOut(nextRequest) &&
                CreateFadeOutRequest != null)
            {
                // Safety check: Don't add a fade out if the queue already has a fade out as the last item
                if (requestQueue.Count > 0)
                {
                    var queueArray = requestQueue.ToArray();
                    var lastQueuedRequest = queueArray[queueArray.Length - 1];
                    if (RequiresFadeOut != null && !RequiresFadeOut(lastQueuedRequest))
                    {
                        // Last request is a fade out (doesn't require fade out), so don't add another
                        Debug.Log($"AnimationQueueManager[{instanceId}]: Skipping fade out - last queued request is already a fade out");
                        return nextRequest;
                    }
                }
                
                Debug.Log($"AnimationQueueManager[{instanceId}]: Adding fade out for proper animation flow");
                
                // Create fade out request
                TRequest fadeOutRequest = CreateFadeOutRequest();
                if (fadeOutRequest != null)
                {
                    Debug.Log($"AnimationQueueManager[{instanceId}]: Created fade out request, current queue size before modification: {requestQueue.Count}");
                    
                    // Put the original request back at the front of the queue, but mark it as no longer needing fade out
                    var remainingRequests = new List<TRequest>(requestQueue);
                    requestQueue.Clear();
                    
                    // CRITICAL FIX: Create a modified version of the original request that doesn't require fade out
                    // This prevents infinite loops where the same request keeps requiring fade out
                    TRequest modifiedOriginalRequest = CreateModifiedRequestWithoutFadeOut?.Invoke(nextRequest);
                    if (modifiedOriginalRequest != null)
                    {
                        requestQueue.Enqueue(modifiedOriginalRequest); // Modified request goes first
                        Debug.Log($"AnimationQueueManager[{instanceId}]: Modified original request to not require fade out");
                    }
                    else
                    {
                        requestQueue.Enqueue(nextRequest); // Fallback to original request
                        Debug.LogWarning($"AnimationQueueManager[{instanceId}]: Could not modify original request, using original (may cause infinite loop)");
                    }
                    
                    foreach (var request in remainingRequests)
                    {
                        requestQueue.Enqueue(request);
                    }
                    
                    Debug.Log($"AnimationQueueManager[{instanceId}]: Inserted fade out for proper animation flow, new queue size: {requestQueue.Count}");
                    
                    // Return the fade out request to be processed immediately
                    return fadeOutRequest;
                }
                else
                {
                    Debug.LogWarning($"AnimationQueueManager[{instanceId}]: CreateFadeOutRequest returned null, cannot add fade out");
                }
            }
            else
            {
                Debug.Log($"AnimationQueueManager[{instanceId}]: No fade out needed - State: {CurrentAnimationState}, RequiresFadeOut: {RequiresFadeOut?.Invoke(nextRequest)}, HasCreateFadeOut: {CreateFadeOutRequest != null}");
            }
            
            // No fade out needed, return the original request
            return nextRequest;
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Get information about the current queue state
        /// </summary>
        /// <returns>Debug information string</returns>
        public string GetQueueInfo()
        {
            return $"AnimationQueueManager Info:\n" +
                   $"- Animation State: {CurrentAnimationState}\n" +
                   $"- Queue Count: {requestQueue.Count}\n" +
                   $"- Processing: {isProcessingQueue}\n" +
                   $"- Owner: {owner?.name ?? "null"}\n" +
                   $"- Instance ID: {instanceId}";
        }
        
        #endregion
    }
} 