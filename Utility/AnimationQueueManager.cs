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
        private MonoBehaviour owner; // The MonoBehaviour that owns this queue manager
        private Coroutine queueProcessingCoroutine = null;
        
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
                Debug.LogWarning("AnimationQueueManager: Cannot queue null request");
                return;
            }
            
            Debug.Log($"AnimationQueueManager: Queueing animation request - Current state: {CurrentAnimationState}, Queue size: {requestQueue.Count}");
            
            // Add request to queue
            requestQueue.Enqueue(request);
            
            // Start processing if not already running
            if (!isProcessingQueue && owner != null)
            {
                queueProcessingCoroutine = owner.StartCoroutine(ProcessQueueRespectingAnimations());
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
                Debug.Log($"AnimationQueueManager: Animation state changed: {CurrentAnimationState} → {newState}");
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
            isProcessingQueue = true;
            OnQueueProcessingStarted?.Invoke(requestQueue.Count);
            
            Debug.Log($"AnimationQueueManager: Starting queue processing with {requestQueue.Count} requests");
            
            while (requestQueue.Count > 0)
            {
                // Wait for current animation to complete before processing next request
                yield return new WaitUntil(() => CurrentAnimationState == AnimationState.Idle);
                
                // Check if queue was cleared while waiting
                if (requestQueue.Count == 0) break;
                
                // Optimize queue before processing next request
                OptimizeQueueRespectingAnimations();
                
                // Check again after optimization
                if (requestQueue.Count == 0) break;
                
                // Get next request
                TRequest nextRequest = requestQueue.Dequeue();
                Debug.Log($"AnimationQueueManager: Processing next request - Queue remaining: {requestQueue.Count}");
                
                // Check if we need to add a fade out for proper animation flow
                // This may return a fade out request instead of the original request
                TRequest requestToProcess = HandleAnimationFlowOptimization(nextRequest);
                
                // Execute the request (either original or fade out)
                if (ExecuteRequest != null)
                {
                    yield return owner.StartCoroutine(ExecuteRequest(requestToProcess));
                }
                else
                {
                    Debug.LogWarning("AnimationQueueManager: No ExecuteRequest delegate set, skipping request");
                }
            }
            
            isProcessingQueue = false;
            queueProcessingCoroutine = null;
            OnQueueProcessingCompleted?.Invoke();
            
            Debug.Log("AnimationQueueManager: Queue processing completed");
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
            // Only add fade out if we're idle and the request requires it
            if (CurrentAnimationState == AnimationState.Idle && 
                RequiresFadeOut != null && RequiresFadeOut(nextRequest) &&
                CreateFadeOutRequest != null)
            {
                Debug.Log("AnimationQueueManager: Adding fade out for proper animation flow");
                
                // Create fade out request
                TRequest fadeOutRequest = CreateFadeOutRequest();
                if (fadeOutRequest != null)
                {
                    // Put the original request back at the front of the queue
                    var remainingRequests = new List<TRequest>(requestQueue);
                    requestQueue.Clear();
                    
                    requestQueue.Enqueue(nextRequest); // Original request goes first
                    
                    foreach (var request in remainingRequests)
                    {
                        requestQueue.Enqueue(request);
                    }
                    
                    Debug.Log("AnimationQueueManager: Inserted fade out for proper animation flow");
                    
                    // Return the fade out request to be processed immediately
                    return fadeOutRequest;
                }
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
                   $"- Owner: {owner?.name ?? "null"}";
        }
        
        #endregion
    }
} 