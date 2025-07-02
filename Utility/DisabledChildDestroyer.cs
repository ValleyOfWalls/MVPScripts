using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MVPScripts.Utility
{
    /// <summary>
    /// Helper component that tracks a GameObject's active state and notifies when it changes.
    /// This is automatically added to child objects that need monitoring.
    /// </summary>
    public class GameObjectStateTracker : MonoBehaviour
    {
        public System.Action<GameObject, bool> OnActiveStateChanged;
        private bool lastActiveState;
        private bool isInitialized = false;
        
        private void Start()
        {
            lastActiveState = gameObject.activeInHierarchy;
            isInitialized = true;
        }
        
        private void OnEnable()
        {
            if (isInitialized && !lastActiveState)
            {
                lastActiveState = true;
                OnActiveStateChanged?.Invoke(gameObject, true);
            }
        }
        
        private void OnDisable()
        {
            if (isInitialized && lastActiveState)
            {
                lastActiveState = false;
                OnActiveStateChanged?.Invoke(gameObject, false);
            }
        }
    }

    /// <summary>
    /// Monitors child GameObjects and destroys them after a delay when they become disabled.
    /// Uses an event-driven approach instead of Update() for better performance.
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
        
        [Header("Advanced Settings")]
        [SerializeField] private bool autoAddTrackersToNewChildren = true;
        [SerializeField] private bool useTransformChildEvents = true;
        
        // Track objects scheduled for destruction
        private Dictionary<GameObject, Coroutine> scheduledDestruction = new Dictionary<GameObject, Coroutine>();
        
        // Track monitored child objects
        private HashSet<GameObject> monitoredChildren = new HashSet<GameObject>();
        
        private void Start()
        {
            // Set up monitoring for existing children
            SetupMonitoringForExistingChildren();
            
            if (enableDebugLogging)
            {
                Debug.Log($"DisabledChildDestroyer: Initialized on {gameObject.name} - monitoring {monitoredChildren.Count} children");
            }
        }
        
        private void OnTransformChildrenChanged()
        {
            if (useTransformChildEvents && autoAddTrackersToNewChildren)
            {
                // Check for new children that need monitoring
                SetupMonitoringForNewChildren();
                
                // Clean up monitoring for removed children
                CleanupRemovedChildrenMonitoring();
            }
        }
        
        /// <summary>
        /// Set up monitoring for all existing children
        /// </summary>
        private void SetupMonitoringForExistingChildren()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child != null)
                {
                    SetupMonitoringForChild(child);
                }
            }
        }
        
        /// <summary>
        /// Set up monitoring for newly added children
        /// </summary>
        private void SetupMonitoringForNewChildren()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child != null && !monitoredChildren.Contains(child))
                {
                    SetupMonitoringForChild(child);
                    
                    if (enableDebugLogging)
                    {
                        Debug.Log($"DisabledChildDestroyer: Started monitoring new child: {child.name}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Clean up monitoring for children that were removed from the hierarchy
        /// </summary>
        private void CleanupRemovedChildrenMonitoring()
        {
            var childrenToRemove = new List<GameObject>();
            
            foreach (GameObject child in monitoredChildren)
            {
                if (child == null || child.transform.parent != transform)
                {
                    childrenToRemove.Add(child);
                }
            }
            
            foreach (GameObject child in childrenToRemove)
            {
                RemoveMonitoringForChild(child);
            }
        }
        
        /// <summary>
        /// Set up monitoring for a specific child
        /// </summary>
        private void SetupMonitoringForChild(GameObject child)
        {
            if (monitoredChildren.Contains(child))
                return;
                
            // Add the state tracker component if it doesn't exist
            GameObjectStateTracker tracker = child.GetComponent<GameObjectStateTracker>();
            if (tracker == null)
            {
                tracker = child.AddComponent<GameObjectStateTracker>();
            }
            
            // Subscribe to state change events
            tracker.OnActiveStateChanged += OnChildActiveStateChanged;
            
            // Add to monitored set
            monitoredChildren.Add(child);
        }
        
        /// <summary>
        /// Remove monitoring for a specific child
        /// </summary>
        private void RemoveMonitoringForChild(GameObject child)
        {
            if (child != null)
            {
                GameObjectStateTracker tracker = child.GetComponent<GameObjectStateTracker>();
                if (tracker != null)
                {
                    tracker.OnActiveStateChanged -= OnChildActiveStateChanged;
                }
                
                // Cancel any scheduled destruction
                if (scheduledDestruction.ContainsKey(child))
                {
                    if (scheduledDestruction[child] != null)
                    {
                        StopCoroutine(scheduledDestruction[child]);
                    }
                    scheduledDestruction.Remove(child);
                }
            }
            
            monitoredChildren.Remove(child);
        }
        
        /// <summary>
        /// Called when a monitored child's active state changes
        /// </summary>
        private void OnChildActiveStateChanged(GameObject child, bool isActive)
        {
            if (child == null)
                return;
                
            if (!isActive)
            {
                // Child was disabled
                OnChildDisabled(child);
            }
            else
            {
                // Child was enabled
                OnChildEnabled(child);
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
                if (scheduledDestruction[child] != null)
                {
                    StopCoroutine(scheduledDestruction[child]);
                }
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
                
                if (scheduledDestruction[child] != null)
                {
                    StopCoroutine(scheduledDestruction[child]);
                }
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
                RemoveMonitoringForChild(child);
                
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
                if (child != null && scheduledDestruction.ContainsKey(child))
                {
                    scheduledDestruction.Remove(child);
                }
            }
        }
        
        /// <summary>
        /// Manually add monitoring for a specific child (useful for runtime-created objects)
        /// </summary>
        public void AddMonitoringForChild(GameObject child)
        {
            if (child != null && child.transform.parent == transform)
            {
                SetupMonitoringForChild(child);
                
                if (enableDebugLogging)
                {
                    Debug.Log($"DisabledChildDestroyer: Manually added monitoring for child: {child.name}");
                }
            }
        }
        
        /// <summary>
        /// Manually remove monitoring for a specific child (public interface)
        /// </summary>
        public void RemoveMonitoringForChildManually(GameObject child)
        {
            RemoveMonitoringForChild(child);
            
            if (enableDebugLogging)
            {
                Debug.Log($"DisabledChildDestroyer: Manually removed monitoring for child: {child?.name ?? "null"}");
            }
        }
        
        /// <summary>
        /// Manually trigger cleanup of all currently disabled children
        /// </summary>
        [ContextMenu("Cleanup All Disabled Children")]
        public void CleanupAllDisabledChildren()
        {
            int cleanedUp = 0;
            var childrenToCleanup = new List<GameObject>();
            
            // Collect children to cleanup
            foreach (GameObject child in monitoredChildren)
            {
                if (child != null && !child.activeInHierarchy && ShouldDestroyChild(child))
                {
                    childrenToCleanup.Add(child);
                }
            }
            
            // Cleanup collected children
            foreach (GameObject child in childrenToCleanup)
            {
                if (enableDebugLogging)
                {
                    Debug.Log($"DisabledChildDestroyer: Manual cleanup - destroying {child.name}");
                }
                
                // Cancel any scheduled destruction
                if (scheduledDestruction.ContainsKey(child))
                {
                    if (scheduledDestruction[child] != null)
                    {
                        StopCoroutine(scheduledDestruction[child]);
                    }
                    scheduledDestruction.Remove(child);
                }
                
                RemoveMonitoringForChild(child);
                Destroy(child);
                cleanedUp++;
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
            int totalChildren = monitoredChildren.Count;
            int activeChildren = 0;
            int scheduledForDestruction = scheduledDestruction.Count;
            
            foreach (GameObject child in monitoredChildren)
            {
                if (child != null && child.activeInHierarchy)
                {
                    activeChildren++;
                }
            }
            
            return $"DisabledChildDestroyer Info:\n" +
                   $"- Total monitored children: {totalChildren}\n" +
                   $"- Active children: {activeChildren}\n" +
                   $"- Scheduled for destruction: {scheduledForDestruction}\n" +
                   $"- Destroy delay: {destroyDelay} seconds\n" +
                   $"- Name filtering: {(onlyDestroyWithNameContains ? "Enabled" : "Disabled")}\n" +
                   $"- Auto-add trackers: {autoAddTrackersToNewChildren}\n" +
                   $"- Use transform events: {useTransformChildEvents}";
        }
        
        [ContextMenu("Print Tracking Info")]
        public void PrintTrackingInfo()
        {
            Debug.Log(GetTrackingInfo());
        }
        
        private void OnDestroy()
        {
            // Cancel all scheduled destructions and clean up monitoring
            CancelAllScheduledDestructions();
            
            // Clean up all monitoring
            var childrenToCleanup = new List<GameObject>(monitoredChildren);
            foreach (GameObject child in childrenToCleanup)
            {
                RemoveMonitoringForChild(child);
            }
        }
        
        private void OnDisable()
        {
            // Cancel all scheduled destructions when this component is disabled
            CancelAllScheduledDestructions();
        }
    }
} 