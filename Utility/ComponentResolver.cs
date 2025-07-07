using UnityEngine;
using System;
using System.Collections.Generic;

namespace MVPScripts.Utility
{
    /// <summary>
    /// Utility class to consolidate component resolution patterns used across manager classes.
    /// Handles finding, validating, and resolving component references with standardized error handling.
    /// </summary>
    public static class ComponentResolver
    {
        /// <summary>
        /// Finds a component if the reference is null, with optional validation
        /// </summary>
        /// <typeparam name="T">Component type to find</typeparam>
        /// <param name="field">Reference field to populate if null</param>
        /// <param name="context">GameObject context for error messages</param>
        /// <param name="required">Whether this component is required (affects error vs warning)</param>
        /// <param name="searchGlobally">If true, uses FindFirstObjectByType, otherwise GetComponent</param>
        /// <returns>The found component or existing reference</returns>
        public static T FindComponent<T>(ref T field, GameObject context = null, bool required = true, bool searchGlobally = true) where T : Component
        {
            if (field != null) return field;
            
            string contextName = context?.name ?? "Unknown";
            
            if (searchGlobally)
            {
                field = UnityEngine.Object.FindFirstObjectByType<T>();
            }
            else if (context != null)
            {
                field = context.GetComponent<T>();
            }
            
            if (field == null)
            {
                string message = $"ComponentResolver: Could not find {typeof(T).Name} for {contextName}";
                if (required)
                    Debug.LogError(message);
                else
                    Debug.LogWarning(message);
            }
            else
            {
                Debug.Log($"ComponentResolver: Found {typeof(T).Name} for {contextName}");
            }
            
            return field;
        }
        
        /// <summary>
        /// Finds a component using a singleton instance property if available, falls back to scene search
        /// </summary>
        /// <typeparam name="T">Component type to find</typeparam>
        /// <param name="field">Reference field to populate if null</param>
        /// <param name="singletonGetter">Function to get singleton instance (e.g., () => GameManager.Instance)</param>
        /// <param name="context">GameObject context for error messages</param>
        /// <param name="required">Whether this component is required</param>
        /// <returns>The found component or existing reference</returns>
        public static T FindComponentWithSingleton<T>(ref T field, Func<T> singletonGetter, GameObject context = null, bool required = true) where T : Component
        {
            if (field != null) return field;
            
            string contextName = context?.name ?? "Unknown";
            
            // Try singleton first
            if (singletonGetter != null)
            {
                try
                {
                    field = singletonGetter();
                    if (field != null)
                    {
                        Debug.Log($"ComponentResolver: Found {typeof(T).Name} via singleton for {contextName}");
                        return field;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"ComponentResolver: Singleton getter failed for {typeof(T).Name}: {e.Message}");
                }
            }
            
            // Fall back to scene search
            return FindComponent(ref field, context, required, true);
        }
        
        /// <summary>
        /// Validates that a component reference is not null
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <param name="component">Component to validate</param>
        /// <param name="componentName">Name for error messages (defaults to type name)</param>
        /// <param name="context">GameObject context for error messages</param>
        /// <param name="required">Whether this component is required</param>
        /// <returns>True if component is valid (not null)</returns>
        public static bool ValidateComponent<T>(T component, string componentName = null, GameObject context = null, bool required = true) where T : Component
        {
            if (component != null) return true;
            
            string name = componentName ?? typeof(T).Name;
            string contextName = context?.name ?? "Unknown";
            string message = $"ComponentResolver: Missing {name} on {contextName}";
            
            if (required)
                Debug.LogError(message);
            else
                Debug.LogWarning(message);
                
            return false;
        }
        
        /// <summary>
        /// Validates that a GameObject reference is not null
        /// </summary>
        /// <param name="gameObject">GameObject to validate</param>
        /// <param name="objectName">Name for error messages</param>
        /// <param name="context">GameObject context for error messages</param>
        /// <param name="required">Whether this GameObject is required</param>
        /// <returns>True if GameObject is valid (not null)</returns>
        public static bool ValidateGameObject(GameObject gameObject, string objectName, GameObject context = null, bool required = true)
        {
            if (gameObject != null) return true;
            
            string contextName = context?.name ?? "Unknown";
            string message = $"ComponentResolver: Missing {objectName} on {contextName}";
            
            if (required)
                Debug.LogError(message);
            else
                Debug.LogWarning(message);
                
            return false;
        }
        
        /// <summary>
        /// Resolves multiple components at once with validation
        /// </summary>
        /// <param name="context">GameObject context for error messages</param>
        /// <param name="resolutions">Array of resolution actions</param>
        /// <returns>True if all required components were resolved successfully</returns>
        public static bool ResolveMultipleComponents(GameObject context, params ComponentResolution[] resolutions)
        {
            bool allResolved = true;
            string contextName = context?.name ?? "Unknown";
            
            foreach (var resolution in resolutions)
            {
                try
                {
                    resolution.ResolveAction?.Invoke();
                    
                    bool isValid = resolution.ValidateAction?.Invoke() ?? true;
                    if (!isValid && resolution.Required)
                    {
                        allResolved = false;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"ComponentResolver: Failed to resolve {resolution.ComponentName} for {contextName}: {e.Message}");
                    if (resolution.Required)
                        allResolved = false;
                }
            }
            
            return allResolved;
        }
        
        /// <summary>
        /// Data structure for component resolution configuration
        /// </summary>
        public class ComponentResolution
        {
            public string ComponentName { get; set; }
            public Action ResolveAction { get; set; }
            public Func<bool> ValidateAction { get; set; }
            public bool Required { get; set; } = true;
            
            public ComponentResolution(string name, Action resolve, Func<bool> validate = null, bool required = true)
            {
                ComponentName = name;
                ResolveAction = resolve;
                ValidateAction = validate;
                Required = required;
            }
        }
        
        /// <summary>
        /// Helper method to find a component on the same GameObject with auto-assignment
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <param name="field">Reference field to populate</param>
        /// <param name="context">GameObject to search on</param>
        /// <param name="required">Whether this component is required</param>
        /// <returns>The found component</returns>
        public static T FindComponentOnSameObject<T>(ref T field, GameObject context, bool required = true) where T : Component
        {
            return FindComponent(ref field, context, required, false);
        }
        
        /// <summary>
        /// Helper method for common pattern of finding a component in children
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <param name="field">Reference field to populate</param>
        /// <param name="context">GameObject to search in</param>
        /// <param name="required">Whether this component is required</param>
        /// <returns>The found component</returns>
        public static T FindComponentInChildren<T>(ref T field, GameObject context, bool required = true) where T : Component
        {
            if (field != null) return field;
            
            string contextName = context?.name ?? "Unknown";
            
            if (context != null)
            {
                field = context.GetComponentInChildren<T>();
            }
            
            if (field == null)
            {
                string message = $"ComponentResolver: Could not find {typeof(T).Name} in children of {contextName}";
                if (required)
                    Debug.LogError(message);
                else
                    Debug.LogWarning(message);
            }
            else
            {
                Debug.Log($"ComponentResolver: Found {typeof(T).Name} in children of {contextName}");
            }
            
            return field;
        }
        
        /// <summary>
        /// Finds a component globally using FindFirstObjectByType (for cases where no GameObject context is available)
        /// </summary>
        /// <typeparam name="T">Component type to find</typeparam>
        /// <returns>The found component or null</returns>
        public static T FindComponentGlobally<T>() where T : Component
        {
            return UnityEngine.Object.FindFirstObjectByType<T>();
        }
    }
} 