using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MVPScripts.Utility
{
    /// <summary>
    /// Monitors child GameObjects and destroys them after a delay when they become disabled.
    /// Useful for cleaning up model instances that are disabled by animation systems.
    /// Attach this to a parent GameObject that contains models that should be cleaned up.
    /// </summary>
    public class DisabledChildDestroyer : MonoBehaviour
    {
        [Header("Cleanup Settings")]
        [SerializeField] private float destroyDelay = 5f;
        [SerializeField] private bool enableDebugLogging = true;
        
        [Header("Filter Settings")]
        [SerializeField] private bool onlyDestroyWithNameContains = false;
        [SerializeField] private string[] nameFilters = { "Selected", "Model", "Character", "Pet" };
        
        // Track objects scheduled for destruction
        private Dictionary<GameObject, Coroutine> scheduledDestruction = new Dictionary<GameObject, Coroutine>();
        
        // Track child objects and their previous active states
        private Dictionary<GameObject, bool> childActiveStates = new Dictionary<GameObject, bool>();
        
        private void Start()
        {
            // Initialize tracking for existing children
            InitializeChildTracking();
            
            if (enableDebugLogging)
            {
                Debug.Log($"DisabledChildDestroyer: Initialized on {gameObject.name} - monitoring {childActiveStates.Count} children");
            }
        }
        
        private void Update()
        {
            CheckChildrenForStateChanges();
        }
        
        /// <summary>
        /// Initialize tracking for all existing children
        /// </summary>
        private void InitializeChildTracking()
        {
            childActiveStates.Clear();
            
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child != null)
                {
                    childActiveStates[child] = child.activeInHierarchy;
                }
            }
        }
        
        /// <summary>
        /// Check all children for active state changes
        /// </summary>
        private void CheckChildrenForStateChanges()
        {
            // Create a copy of the keys to avoid modification during iteration
            var childrenToCheck = new List<GameObject>(childActiveStates.Keys);
            var childrenToRemove = new List<GameObject>();
            var stateUpdates = new Dictionary<GameObject, bool>();
            
            foreach (GameObject child in childrenToCheck)
            {
                // Check if child still exists
                if (child == null)
                {
                    childrenToRemove.Add(child);
                    continue;
                }
                
                bool previousState = childActiveStates[child];
                bool currentState = child.activeInHierarchy;
                
                // Detect state change from active to inactive
                if (previousState && !currentState)
                {
                    OnChildDisabled(child);
                }
                // Detect state change from inactive to active (cancel destruction if scheduled)
                else if (!previousState && currentState)
                {
                    OnChildEnabled(child);
                }
                
                // Store state update for later application
                stateUpdates[child] = currentState;
            }
            
            // Apply all state updates
            foreach (var kvp in stateUpdates)
            {
                if (childActiveStates.ContainsKey(kvp.Key))
                {
                    childActiveStates[kvp.Key] = kvp.Value;
                }
            }
            
            // Remove null references
            foreach (GameObject nullChild in childrenToRemove)
            {
                childActiveStates.Remove(nullChild);
                if (scheduledDestruction.ContainsKey(nullChild))
                {
                    scheduledDestruction.Remove(nullChild);
                }
            }
            
            // Check for new children
            CheckForNewChildren();
        }
        
        /// <summary>
        /// Check for newly added children and start tracking them
        /// </summary>
        private void CheckForNewChildren()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child != null && !childActiveStates.ContainsKey(child))
                {
                    childActiveStates[child] = child.activeInHierarchy;
                    
                    if (enableDebugLogging)
                    {
                        Debug.Log($"DisabledChildDestroyer: Started tracking new child: {child.name}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Called when a child becomes disabled
        /// </summary>
        private void OnChildDisabled(GameObject child)
        {
            if (!ShouldDestroyChild(child))
                return;
                
            if (enableDebugLogging)
            {
                Debug.Log($"DisabledChildDestroyer: Child disabled - {child.name} - scheduling destruction in {destroyDelay} seconds");
            }
            
            // Cancel any existing destruction coroutine for this child
            if (scheduledDestruction.ContainsKey(child))
            {
                StopCoroutine(scheduledDestruction[child]);
            }
            
            // Schedule destruction
            Coroutine destructionCoroutine = StartCoroutine(DestroyAfterDelay(child, destroyDelay));
            scheduledDestruction[child] = destructionCoroutine;
        }
        
        /// <summary>
        /// Called when a child becomes enabled (cancels scheduled destruction)
        /// </summary>
        private void OnChildEnabled(GameObject child)
        {
            if (scheduledDestruction.ContainsKey(child))
            {
                if (enableDebugLogging)
                {
                    Debug.Log($"DisabledChildDestroyer: Child re-enabled - {child.name} - cancelling scheduled destruction");
                }
                
                StopCoroutine(scheduledDestruction[child]);
                scheduledDestruction.Remove(child);
            }
        }
        
        /// <summary>
        /// Check if a child should be destroyed based on filter settings
        /// </summary>
        private bool ShouldDestroyChild(GameObject child)
        {
            if (!onlyDestroyWithNameContains)
                return true;
                
            if (nameFilters == null || nameFilters.Length == 0)
                return true;
                
            string childName = child.name.ToLower();
            
            foreach (string filter in nameFilters)
            {
                if (!string.IsNullOrEmpty(filter) && childName.Contains(filter.ToLower()))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Coroutine to destroy a child after a delay
        /// </summary>
        private IEnumerator DestroyAfterDelay(GameObject child, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            // Double-check that the child still exists and is still disabled
            if (child != null && !child.activeInHierarchy)
            {
                if (enableDebugLogging)
                {
                    Debug.Log($"DisabledChildDestroyer: Destroying disabled child: {child.name}");
                }
                
                // Remove from tracking
                childActiveStates.Remove(child);
                scheduledDestruction.Remove(child);
                
                // Destroy the child
                Destroy(child);
            }
            else
            {
                if (enableDebugLogging && child != null)
                {
                    Debug.Log($"DisabledChildDestroyer: Child {child.name} was re-enabled before destruction - cancelling");
                }
                
                // Remove from scheduled destruction if it was re-enabled
                if (child != null)
                {
                    scheduledDestruction.Remove(child);
                }
            }
        }
        
        /// <summary>
        /// Manually trigger cleanup of all currently disabled children
        /// </summary>
        [ContextMenu("Cleanup All Disabled Children")]
        public void CleanupAllDisabledChildren()
        {
            int cleanedUp = 0;
            
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child != null && !child.activeInHierarchy && ShouldDestroyChild(child))
                {
                    if (enableDebugLogging)
                    {
                        Debug.Log($"DisabledChildDestroyer: Manual cleanup - destroying {child.name}");
                    }
                    
                    // Cancel any scheduled destruction
                    if (scheduledDestruction.ContainsKey(child))
                    {
                        StopCoroutine(scheduledDestruction[child]);
                        scheduledDestruction.Remove(child);
                    }
                    
                    childActiveStates.Remove(child);
                    Destroy(child);
                    cleanedUp++;
                }
            }
            
            Debug.Log($"DisabledChildDestroyer: Manual cleanup completed - destroyed {cleanedUp} disabled children");
        }
        
        /// <summary>
        /// Cancel all scheduled destructions
        /// </summary>
        public void CancelAllScheduledDestructions()
        {
            foreach (var kvp in scheduledDestruction)
            {
                if (kvp.Value != null)
                {
                    StopCoroutine(kvp.Value);
                }
            }
            
            scheduledDestruction.Clear();
            
            if (enableDebugLogging)
            {
                Debug.Log($"DisabledChildDestroyer: Cancelled all scheduled destructions");
            }
        }
        
        /// <summary>
        /// Get info about currently tracked children and scheduled destructions
        /// </summary>
        public string GetTrackingInfo()
        {
            int totalChildren = childActiveStates.Count;
            int activeChildren = 0;
            int scheduledForDestruction = scheduledDestruction.Count;
            
            foreach (var kvp in childActiveStates)
            {
                if (kvp.Key != null && kvp.Value)
                {
                    activeChildren++;
                }
            }
            
            return $"DisabledChildDestroyer Info:\n" +
                   $"- Total tracked children: {totalChildren}\n" +
                   $"- Active children: {activeChildren}\n" +
                   $"- Scheduled for destruction: {scheduledForDestruction}\n" +
                   $"- Destroy delay: {destroyDelay} seconds\n" +
                   $"- Name filtering: {(onlyDestroyWithNameContains ? "Enabled" : "Disabled")}";
        }
        
        [ContextMenu("Print Tracking Info")]
        public void PrintTrackingInfo()
        {
            Debug.Log(GetTrackingInfo());
        }
        
        private void OnDestroy()
        {
            // Cancel all scheduled destructions when this component is destroyed
            CancelAllScheduledDestructions();
        }
        
        private void OnDisable()
        {
            // Cancel all scheduled destructions when this component is disabled
            CancelAllScheduledDestructions();
        }
    }
} 