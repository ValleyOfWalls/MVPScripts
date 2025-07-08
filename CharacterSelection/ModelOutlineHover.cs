using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CharacterSelection
{
    /// <summary>
    /// Adds a screen-space outline effect to models when hovering over them with the mouse.
    /// Creates an outline only around the visible silhouette from the camera's perspective.
    /// </summary>
    public class ModelOutlineHover : MonoBehaviour
    {
        [Header("Outline Settings")]
        [SerializeField] private Color outlineColor = Color.white;
        [SerializeField, Range(1f, 10f)] private float outlineWidth = 3f;
        [SerializeField] private bool enableOutlineOnStart = false;
        
        [Header("Auto-Discovery Settings")]
        [SerializeField] private bool searchInChildren = true;
        [SerializeField] private bool includeInactive = false;
        
        [Header("Rendering")]
        [SerializeField] private int outlineStencilRef = 1;
        [SerializeField] private LayerMask outlineLayer = -1;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;
        
        // Discovered components
        private Renderer[] renderers;
        private Material[] originalMaterials;
        private bool isOutlineActive = false;
        private bool isInitialized = false;
        
        // Outline materials and setup
        private Material stencilWriteMaterial;
        private Material outlinePostProcessMaterial;
        private Camera targetCamera;
        private CommandBuffer outlineCommandBuffer;
        
        // Shader property IDs
        private static readonly int OutlineColorProperty = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthProperty = Shader.PropertyToID("_OutlineWidth");
        private static readonly int MainTexProperty = Shader.PropertyToID("_MainTex");
        
        private void Start()
        {
            InitializeOutlineSystem();
            
            if (enableOutlineOnStart)
            {
                ShowOutline();
            }
        }
        
        /// <summary>
        /// Discovers and initializes the outline system for all renderers in the hierarchy
        /// </summary>
        [ContextMenu("Initialize Outline System")]
        public void InitializeOutlineSystem()
        {
            if (isInitialized)
            {
                CleanupOutlineSystem();
            }
            
            // Find all renderers in the hierarchy
            if (searchInChildren)
            {
                renderers = GetComponentsInChildren<Renderer>(includeInactive);
            }
            else
            {
                renderers = GetComponents<Renderer>();
            }
            
            if (renderers.Length == 0)
            {
                if (showDebugLogs)
                    Debug.LogWarning($"ModelOutlineHover: No renderers found on {gameObject.name}");
                return;
            }
            
            // Store original materials
            originalMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    originalMaterials[i] = renderers[i].sharedMaterial;
                }
            }
            
            // Create outline materials
            CreateOutlineMaterials();
            
            // Find or set target camera
            FindTargetCamera();
            
            isInitialized = true;
            
            if (showDebugLogs)
            {
                /* Debug.Log($"ModelOutlineHover: Initialized outline system for {renderers.Length} renderers on {gameObject.name}"); */
            }
        }
        
        /// <summary>
        /// Creates the materials needed for the outline effect
        /// </summary>
        private void CreateOutlineMaterials()
        {
            // Create stencil write material (renders to stencil buffer)
            CreateStencilWriteMaterial();
            
            // Create outline post-process material (creates the outline from stencil)
            CreateOutlinePostProcessMaterial();
        }
        
        /// <summary>
        /// Creates a material that writes to the stencil buffer
        /// </summary>
        private void CreateStencilWriteMaterial()
        {
            // Try to find a suitable unlit shader
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null)
                unlitShader = Shader.Find("URP/Unlit");
            if (unlitShader == null)
                unlitShader = Shader.Find("Unlit/Color");
            if (unlitShader == null)
                unlitShader = Shader.Find("Mobile/Unlit (Supports Lightmap)");
            
            if (unlitShader != null)
            {
                stencilWriteMaterial = new Material(unlitShader);
                
                // Configure for stencil writing
                stencilWriteMaterial.SetInt("_StencilComp", (int)CompareFunction.Always);
                stencilWriteMaterial.SetInt("_Stencil", outlineStencilRef);
                stencilWriteMaterial.SetInt("_StencilOp", (int)StencilOp.Replace);
                stencilWriteMaterial.SetInt("_StencilWriteMask", 255);
                stencilWriteMaterial.SetInt("_ColorMask", 0); // Don't write to color buffer
                
                // Set color to black (won't be visible anyway due to ColorMask)
                if (stencilWriteMaterial.HasProperty("_BaseColor"))
                    stencilWriteMaterial.SetColor("_BaseColor", Color.black);
                if (stencilWriteMaterial.HasProperty("_Color"))
                    stencilWriteMaterial.SetColor("_Color", Color.black);
            }
            else
            {
                if (showDebugLogs)
                    Debug.LogError($"ModelOutlineHover: Could not find suitable unlit shader for {gameObject.name}");
            }
        }
        
        /// <summary>
        /// Creates a material for the outline post-processing effect
        /// </summary>
        private void CreateOutlinePostProcessMaterial()
        {
            // For now, we'll use a simple approach with the camera's background
            // In a full implementation, you'd want a custom outline shader
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null)
                unlitShader = Shader.Find("Unlit/Color");
            
            if (unlitShader != null)
            {
                outlinePostProcessMaterial = new Material(unlitShader);
                outlinePostProcessMaterial.SetColor("_BaseColor", outlineColor);
                if (outlinePostProcessMaterial.HasProperty("_Color"))
                    outlinePostProcessMaterial.SetColor("_Color", outlineColor);
            }
        }
        
        /// <summary>
        /// Finds the target camera for outline rendering
        /// </summary>
        private void FindTargetCamera()
        {
            // Try to find the main camera or camera tagged "MainCamera"
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                GameObject cameraObj = GameObject.FindWithTag("MainCamera");
                if (cameraObj != null)
                    targetCamera = cameraObj.GetComponent<Camera>();
            }
            
            // If still null, find any camera in the scene
            if (targetCamera == null)
            {
                targetCamera = FindFirstObjectByType<Camera>();
            }
            
            if (targetCamera == null && showDebugLogs)
            {
                Debug.LogWarning($"ModelOutlineHover: No camera found for outline rendering on {gameObject.name}");
            }
        }
        
        /// <summary>
        /// Sets a specific camera as the target for outline rendering
        /// </summary>
        public void SetTargetCamera(Camera camera)
        {
            targetCamera = camera;
        }
        
        /// <summary>
        /// Shows the outline effect using a simpler approach
        /// </summary>
        public void ShowOutline()
        {
            if (!isInitialized)
                InitializeOutlineSystem();
            
            if (isOutlineActive || renderers == null)
                return;
            
            // For now, use a simple highlighting approach
            ApplyHighlightEffect();
            
            isOutlineActive = true;
            
            if (showDebugLogs)
                Debug.Log($"ModelOutlineHover: Showing outline on {gameObject.name}");
        }
        
        /// <summary>
        /// Hides the outline effect
        /// </summary>
        public void HideOutline()
        {
            if (!isOutlineActive || renderers == null)
                return;
            
            RemoveHighlightEffect();
            
            isOutlineActive = false;
            
            if (showDebugLogs)
                Debug.Log($"ModelOutlineHover: Hiding outline on {gameObject.name}");
        }
        
        /// <summary>
        /// Applies a highlight effect to make the model stand out
        /// </summary>
        private void ApplyHighlightEffect()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    // Create a brighter version of the original material
                    Material highlightMaterial = CreateHighlightMaterial(originalMaterials[i]);
                    if (highlightMaterial != null)
                    {
                        renderers[i].material = highlightMaterial;
                    }
                }
            }
        }
        
        /// <summary>
        /// Removes the highlight effect and restores original materials
        /// </summary>
        private void RemoveHighlightEffect()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && originalMaterials[i] != null)
                {
                    renderers[i].sharedMaterial = originalMaterials[i];
                }
            }
        }
        
        /// <summary>
        /// Creates a highlight material based on the original
        /// </summary>
        private Material CreateHighlightMaterial(Material originalMaterial)
        {
            if (originalMaterial == null)
                return null;
            
            Material highlightMaterial = new Material(originalMaterial);
            
            // Brighten the material
            if (highlightMaterial.HasProperty("_BaseColor"))
            {
                Color baseColor = highlightMaterial.GetColor("_BaseColor");
                Color highlightColor = Color.Lerp(baseColor, outlineColor, 0.3f);
                highlightColor = Color.Lerp(highlightColor, Color.white, 0.2f); // Brighten
                highlightMaterial.SetColor("_BaseColor", highlightColor);
            }
            else if (highlightMaterial.HasProperty("_Color"))
            {
                Color baseColor = highlightMaterial.GetColor("_Color");
                Color highlightColor = Color.Lerp(baseColor, outlineColor, 0.3f);
                highlightColor = Color.Lerp(highlightColor, Color.white, 0.2f); // Brighten
                highlightMaterial.SetColor("_Color", highlightColor);
            }
            
            // Increase emission if available
            if (highlightMaterial.HasProperty("_EmissionColor"))
            {
                highlightMaterial.SetColor("_EmissionColor", outlineColor * 0.1f);
                highlightMaterial.EnableKeyword("_EMISSION");
            }
            
            return highlightMaterial;
        }
        
        /// <summary>
        /// Updates the outline color
        /// </summary>
        public void SetOutlineColor(Color color)
        {
            outlineColor = color;
            
            if (outlinePostProcessMaterial != null)
            {
                outlinePostProcessMaterial.SetColor("_BaseColor", outlineColor);
                if (outlinePostProcessMaterial.HasProperty("_Color"))
                    outlinePostProcessMaterial.SetColor("_Color", outlineColor);
            }
            
            // If currently active, refresh the effect
            if (isOutlineActive)
            {
                RemoveHighlightEffect();
                ApplyHighlightEffect();
            }
            
            if (showDebugLogs)
                Debug.Log($"ModelOutlineHover: Updated outline color to {color} on {gameObject.name}");
        }
        
        /// <summary>
        /// Updates the outline width
        /// </summary>
        public void SetOutlineWidth(float width)
        {
            outlineWidth = Mathf.Clamp(width, 1f, 10f);
            
            if (showDebugLogs)
                Debug.Log($"ModelOutlineHover: Updated outline width to {width} on {gameObject.name}");
        }
        
        /// <summary>
        /// Mouse enter event - show outline
        /// </summary>
        private void OnMouseEnter()
        {
            ShowOutline();
        }
        
        /// <summary>
        /// Mouse exit event - hide outline
        /// </summary>
        private void OnMouseExit()
        {
            HideOutline();
        }
        
        /// <summary>
        /// Cleans up the outline system
        /// </summary>
        private void CleanupOutlineSystem()
        {
            // Remove any active effects first
            if (isOutlineActive)
                HideOutline();
            
            // Clean up materials
            if (stencilWriteMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(stencilWriteMaterial);
                else
                    DestroyImmediate(stencilWriteMaterial);
                stencilWriteMaterial = null;
            }
            
            if (outlinePostProcessMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(outlinePostProcessMaterial);
                else
                    DestroyImmediate(outlinePostProcessMaterial);
                outlinePostProcessMaterial = null;
            }
            
            // Clean up command buffer if it exists
            if (outlineCommandBuffer != null && targetCamera != null)
            {
                targetCamera.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, outlineCommandBuffer);
                outlineCommandBuffer = null;
            }
        }
        
        private void OnDestroy()
        {
            CleanupOutlineSystem();
        }
        
        private void OnDisable()
        {
            if (isOutlineActive)
                HideOutline();
        }
        
        #if UNITY_EDITOR
        private void OnValidate()
        {
            // Update outline properties in editor when values change
            if (isInitialized)
            {
                SetOutlineColor(outlineColor);
                SetOutlineWidth(outlineWidth);
            }
        }
        #endif
    }
} 