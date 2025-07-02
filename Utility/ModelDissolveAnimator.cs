using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MVPScripts.Utility
{
    /// <summary>
    /// Reusable model dissolve animation system inspired by WeaponTeleportEffect
    /// Handles dissolve, scale, glow, and flash effects for 3D model transitions
    /// Can be used anywhere in the project that needs model transition animations
    /// </summary>
    public class ModelDissolveAnimator : MonoBehaviour
    {
        [Header("Transition Effects")]
        [SerializeField] private float transitionDuration = 1.0f;
        [SerializeField] private float outDuration = 0.5f;
        [SerializeField] private float inDuration = 0.5f;
        [SerializeField] private bool useDissolveEffect = true;
        [SerializeField] private bool useScaleEffect = false;
        [SerializeField] private bool useGlowEffect = true;
        [SerializeField] private bool useFlashEffect = true;
        [SerializeField] private bool useAlphaFallback = true;
        
        [Header("Dissolve Effect")]
        [SerializeField] private string dissolvePropertyName = "_DissolveAmount";
        [SerializeField] private float dissolveStart = 1f; // Fully dissolved
        [SerializeField] private float dissolveEnd = 0f;   // Fully visible
        [SerializeField] private Shader dissolveShader;
        [SerializeField] private Texture2D dissolveNoiseTexture;
        
        [Header("Scale Effect")]
        [SerializeField] private Vector3 modelStartScale = new Vector3(0.1f, 0.1f, 0.1f);
        [SerializeField] private AnimationCurve modelScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        [Header("Glow Effect")]
        [SerializeField] private string emissionPropertyName = "_EmissionColor";
        [SerializeField] private Color glowColor = new Color(0f, 1f, 1f, 1f); // Cyan tech color
        [SerializeField] private float glowIntensity = 2f;
        [SerializeField] private AnimationCurve glowCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool additiveGlow = true;
        
        [Header("Flash Effect")]
        [SerializeField] private float flashDuration = 0.15f;
        [SerializeField] private Color flashColor = new Color(0f, 0.8f, 1f, 1f);
        [SerializeField] private float flashIntensity = 5f;
        [SerializeField] private AnimationCurve flashCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        
        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip teleportInSound;
        [SerializeField] private AudioClip teleportOutSound;
        [SerializeField] private AudioClip flashSound;
        [SerializeField] private float audioVolume = 0.5f;
        
        // Animation state tracking - NEVER interrupt running animations
        private Coroutine currentAnimationCoroutine = null;
        private GameObject currentlyAnimatingModel = null;
        private GameObject currentModel = null; // Track the currently visible model
        
        // Animation queue manager - handles all queueing and optimization
        private AnimationQueueManager<TransitionRequest> queueManager;
        
        // Factory protection (but no immediate cleanup)
        private bool isFactoryCallInProgress = false;
        private readonly object factoryLock = new object();
        
        // Material caching for effects
        private Dictionary<GameObject, ModelMaterialCache> materialCache = new Dictionary<GameObject, ModelMaterialCache>();
        
        // Dissolve shader and texture management
        private bool hasDissolveShader = false;
        private Texture2D defaultNoiseTexture;
        
        // Events
        public System.Action<GameObject> OnTransitionStarted;
        public System.Action<GameObject> OnTransitionCompleted;
        
        #region Data Classes
        
        private enum TransitionType
        {
            ModelTransition, // From one model to another
            ModelIn,         // Only animate in
            ModelOut         // Only animate out
        }
        
        private class TransitionRequest
        {
            public TransitionType type;
            public GameObject oldModel;
            public GameObject newModel;
            public System.Func<GameObject> modelFactory; // Factory to create new model when needed
            public System.Action onComplete;
            public System.Guid transitionId; // Unique ID for cancellation tracking
            
            private TransitionRequest(TransitionType type, GameObject oldModel, GameObject newModel, System.Func<GameObject> modelFactory, System.Action onComplete, System.Guid transitionId)
            {
                this.type = type;
                this.oldModel = oldModel;
                this.newModel = newModel;
                this.modelFactory = modelFactory;
                this.onComplete = onComplete;
                this.transitionId = transitionId;
            }
            
            // Static factory methods to avoid constructor ambiguity
            public static TransitionRequest WithModel(TransitionType type, GameObject oldModel, GameObject newModel, System.Action onComplete = null)
            {
                return new TransitionRequest(type, oldModel, newModel, null, onComplete, System.Guid.NewGuid());
            }
            
            public static TransitionRequest WithFactory(TransitionType type, GameObject oldModel, System.Func<GameObject> modelFactory, System.Action onComplete = null)
            {
                return new TransitionRequest(type, oldModel, null, modelFactory, onComplete, System.Guid.NewGuid());
            }
            
            public static TransitionRequest ModelOutOnly(GameObject oldModel, System.Action onComplete = null)
            {
                return new TransitionRequest(TransitionType.ModelOut, oldModel, null, null, onComplete, System.Guid.NewGuid());
            }
        }
        
        [System.Serializable]
        private class ModelMaterialCache
        {
            public Material[] originalMaterials;
            public Material[] effectMaterials;
            public Vector3 originalScale;
            public Dictionary<Material, MaterialPropertyData> originalProperties;
            
            public ModelMaterialCache()
            {
                originalProperties = new Dictionary<Material, MaterialPropertyData>();
            }
        }
        
        [System.Serializable]
        private class MaterialPropertyData
        {
            public Color originalEmission = Color.black;
            public Color originalBaseColor = Color.white;
            public float originalAlpha = 1f;
            public float originalDissolveAmount = 0f;
            public bool hasEmission = false;
            public bool hasDissolveProperty = false;
            public string dissolvePropertyName = "";
            public string[] originalKeywords;
        }
        
        #endregion
        
        #region Initialization
        
        private void Awake()
        {
            InitializeDissolveShader();
            CreateDefaultNoiseTexture();
            InitializeQueueManager();
        }
        
        private void InitializeQueueManager()
        {
            // Create and configure the animation queue manager
            queueManager = new AnimationQueueManager<TransitionRequest>(this);
            
            // Set up delegates for customization
            queueManager.ExecuteRequest = ExecuteAnimationRespectingRequest;
            queueManager.OptimizeQueue = OptimizeTransitionRequests;
            queueManager.RequiresFadeOut = RequiresFadeOutForRequest;
            queueManager.CreateFadeOutRequest = CreateFadeOutRequest;
            
            // Set up event handlers
            queueManager.OnAnimationStateChanged = OnAnimationStateChanged;
            queueManager.OnQueueProcessingStarted = (count) => Debug.Log($"ModelDissolveAnimator: Queue processing started with {count} requests");
            queueManager.OnQueueProcessingCompleted = () => Debug.Log("ModelDissolveAnimator: Queue processing completed");
            
            Debug.Log("ModelDissolveAnimator: Queue manager initialized");
        }
        
        private void InitializeDissolveShader()
        {
            // Try to find the dissolve shader if not assigned
            if (dissolveShader == null)
            {
                dissolveShader = Shader.Find("Custom/URP/WeaponDissolve");
            }
            
            hasDissolveShader = dissolveShader != null;
            
            if (hasDissolveShader)
            {
                Debug.Log("ModelDissolveAnimator: Found Custom/URP/WeaponDissolve shader");
            }
            else
            {
                Debug.LogWarning("ModelDissolveAnimator: Custom/URP/WeaponDissolve shader not found, will use alpha fallback");
            }
        }
        
        private void CreateDefaultNoiseTexture()
        {
            // Use assigned texture if available
            if (dissolveNoiseTexture != null)
            {
                Debug.Log("ModelDissolveAnimator: Using assigned dissolve noise texture");
                return;
            }
            
            // Create a simple noise texture programmatically (same as WeaponTeleportEffect)
            int size = 256;
            defaultNoiseTexture = new Texture2D(size, size, TextureFormat.R8, false);
            defaultNoiseTexture.name = "ModelDissolve_NoiseTexture";
            
            Color[] pixels = new Color[size * size];
            
            // Generate Perlin noise
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float noiseValue = Mathf.PerlinNoise(x * 0.1f, y * 0.1f);
                    pixels[y * size + x] = new Color(noiseValue, noiseValue, noiseValue, 1f);
                }
            }
            
            defaultNoiseTexture.SetPixels(pixels);
            defaultNoiseTexture.Apply();
            
            Debug.Log("ModelDissolveAnimator: Generated default noise texture");
        }
        
        #endregion
        
        #region Public Interface
        
        /// <summary>
        /// Animate a transition between two models (old model dissolves out, new model dissolves in)
        /// Uses smart queueing to avoid unnecessary intermediate animations
        /// </summary>
        public void AnimateModelTransition(GameObject oldModel, GameObject newModel, System.Action onComplete = null)
        {
            var request = TransitionRequest.WithModel(TransitionType.ModelTransition, oldModel, newModel, onComplete);
            QueueTransition(request);
        }
        
        /// <summary>
        /// Animate a transition using a factory callback to create the new model only when needed
        /// This prevents unnecessary model creation during rapid selection changes
        /// </summary>
        public void AnimateModelTransitionWithFactory(GameObject oldModel, System.Func<GameObject> modelFactory, System.Action onComplete = null)
        {
            var request = TransitionRequest.WithFactory(TransitionType.ModelTransition, oldModel, modelFactory, onComplete);
            QueueTransition(request);
        }
        
        /// <summary>
        /// Animate only the "in" portion (model appears with effects)
        /// </summary>
        public void AnimateModelIn(GameObject model, System.Action onComplete = null)
        {
            var request = TransitionRequest.WithModel(TransitionType.ModelIn, null, model, onComplete);
            QueueTransition(request);
        }
        
        /// <summary>
        /// Animate only the "out" portion (model disappears with effects)
        /// </summary>
        public void AnimateModelOut(GameObject model, System.Action onComplete = null)
        {
            var request = TransitionRequest.ModelOutOnly(model, onComplete);
            QueueTransition(request);
        }
        
        /// <summary>
        /// Show a model instantly without animation
        /// </summary>
        public void ShowModelInstantly(GameObject model)
        {
            if (model == null) return;
            
            model.SetActive(true);
            SetModelDissolveAmount(model, dissolveEnd); // Fully visible
            model.transform.localScale = GetOriginalScale(model);
            SetModelEmissionColor(model, Color.black); // No glow
        }
        
        /// <summary>
        /// Hide a model instantly without animation
        /// </summary>
        public void HideModelInstantly(GameObject model)
        {
            if (model == null) return;
            
            SetModelDissolveAmount(model, dissolveStart); // Fully dissolved
            model.transform.localScale = modelStartScale;
            model.SetActive(false);
        }
        
        /// <summary>
        /// Stop queued transitions but NEVER interrupt running animations
        /// </summary>
        public void StopTransition()
        {
            Debug.Log($"ModelDissolveAnimator: StopTransition called - Animation state: {queueManager?.CurrentAnimationState}");
            
            if (queueManager?.IsAnimating == true)
            {
                Debug.Log("ModelDissolveAnimator: CANNOT stop transition - animation is running and must complete");
                // Clear queue but don't interrupt running animation
                queueManager.ClearQueue();
                Debug.Log("ModelDissolveAnimator: Cleared queue but preserved running animation");
                return;
            }
            
            // Only stop if no animation is running
            queueManager?.StopAndClear();
            Debug.Log("ModelDissolveAnimator: Stopped transition - no animation was running");
        }
        
        /// <summary>
        /// Animation-respecting queue system - NEVER interrupts running animations
        /// </summary>
        private void QueueTransition(TransitionRequest request)
        {
            Debug.Log($"ModelDissolveAnimator: New transition request - Old: {request.oldModel?.name ?? "None"}, New: {(request.newModel?.name ?? (request.modelFactory != null ? "Factory-Created" : "None"))}");
            Debug.Log($"ModelDissolveAnimator: Current animation state: {queueManager?.CurrentAnimationState}");
            
            lock (factoryLock)
            {
                // Queue the request using the queue manager
                queueManager?.QueueRequest(request);
            }
        }
        
        #region Queue Manager Delegate Methods
        
        /// <summary>
        /// Optimize transition requests by removing redundant intermediate transitions
        /// </summary>
        private List<TransitionRequest> OptimizeTransitionRequests(List<TransitionRequest> requests)
        {
            if (requests.Count <= 1) return requests;
            
            Debug.Log($"ModelDissolveAnimator: Optimizing {requests.Count} requests");
            
            // Find the final target (what the user ultimately wants)
            GameObject finalTarget = null;
            System.Func<GameObject> finalFactory = null;
            System.Action finalCallback = null;
            
            for (int i = requests.Count - 1; i >= 0; i--)
            {
                if (requests[i].newModel != null || requests[i].modelFactory != null)
                {
                    finalTarget = requests[i].newModel;
                    finalFactory = requests[i].modelFactory;
                    finalCallback = requests[i].onComplete;
                    break;
                }
            }
            
            var optimizedRequests = new List<TransitionRequest>();
            
            // Build optimized sequence based on current animation state
            if (queueManager?.CurrentAnimationState == AnimationQueueManager<TransitionRequest>.AnimationState.Idle)
            {
                // No animation running - can start optimal sequence immediately
                if (currentModel != null && (finalTarget == null || currentModel != finalTarget))
                {
                    // Need to fade out current, then fade in new
                    optimizedRequests.Add(TransitionRequest.ModelOutOnly(currentModel));
                    
                    var inRequest = finalFactory != null 
                        ? TransitionRequest.WithFactory(TransitionType.ModelIn, null, finalFactory, finalCallback)
                        : TransitionRequest.WithModel(TransitionType.ModelIn, null, finalTarget, finalCallback);
                    optimizedRequests.Add(inRequest);
                    
                    Debug.Log($"ModelDissolveAnimator: Optimized to: OUT {currentModel.name} → IN {finalTarget?.name ?? "Factory-Created"}");
                }
                else if (finalTarget != null || finalFactory != null)
                {
                    // No current model or same target - just fade in
                    var inRequest = finalFactory != null 
                        ? TransitionRequest.WithFactory(TransitionType.ModelIn, null, finalFactory, finalCallback)
                        : TransitionRequest.WithModel(TransitionType.ModelIn, null, finalTarget, finalCallback);
                    optimizedRequests.Add(inRequest);
                    
                    Debug.Log($"ModelDissolveAnimator: Optimized to: Direct IN {finalTarget?.name ?? "Factory-Created"}");
                }
            }
            else
            {
                // Animation is running - must wait for completion, then add optimal next steps
                Debug.Log($"ModelDissolveAnimator: Animation {queueManager?.CurrentAnimationState} is running - will optimize after completion");
                
                // Just keep the final request for now
                if (finalFactory != null)
                {
                    optimizedRequests.Add(TransitionRequest.WithFactory(TransitionType.ModelTransition, currentModel, finalFactory, finalCallback));
                }
                else if (finalTarget != null)
                {
                    optimizedRequests.Add(TransitionRequest.WithModel(TransitionType.ModelTransition, currentModel, finalTarget, finalCallback));
                }
            }
            
            Debug.Log($"ModelDissolveAnimator: Queue optimized from {requests.Count} to {optimizedRequests.Count} requests");
            return optimizedRequests;
        }
        
        /// <summary>
        /// Check if a request requires a fade out for proper animation flow
        /// </summary>
        private bool RequiresFadeOutForRequest(TransitionRequest request)
        {
            // If we have a current model and the next request needs a different model, we need fade out
            return currentModel != null && 
                   (request.newModel != currentModel || request.modelFactory != null);
        }
        
        /// <summary>
        /// Create a fade out request for proper animation flow
        /// </summary>
        private TransitionRequest CreateFadeOutRequest()
        {
            return currentModel != null ? TransitionRequest.ModelOutOnly(currentModel) : null;
        }
        
        /// <summary>
        /// Handle animation state changes from the queue manager
        /// </summary>
        private void OnAnimationStateChanged(AnimationQueueManager<TransitionRequest>.AnimationState newState)
        {
            Debug.Log($"ModelDissolveAnimator: Animation state changed to {newState}");
        }
        
        #endregion
        

        

        

        
        /// <summary>
        /// Executes animation request while properly tracking animation state
        /// </summary>
        private IEnumerator ExecuteAnimationRespectingRequest(TransitionRequest request)
        {
            if (request == null) 
            {
                Debug.LogWarning("ModelDissolveAnimator: Null transition request, skipping");
                yield break;
            }
            
            Debug.Log($"ModelDissolveAnimator: ANIMATION EXECUTE - Starting {request.type}");
            
            // Pre-create model if using factory to avoid race conditions
            GameObject targetModel = request.newModel;
            if (targetModel == null && request.modelFactory != null)
            {
                // Use lock to ensure only one factory call happens at a time
                lock (factoryLock)
                {
                    if (!isFactoryCallInProgress)
                    {
                        isFactoryCallInProgress = true;
                        Debug.Log("ModelDissolveAnimator: ANIMATION EXECUTE - Calling factory under lock protection");
                        
                        try
                        {
                            targetModel = request.modelFactory();
                            Debug.Log($"ModelDissolveAnimator: ANIMATION EXECUTE - Factory created: {targetModel?.name ?? "null"}");
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError($"ModelDissolveAnimator: Factory failed: {e.Message}");
                            targetModel = null;
                        }
                        finally
                        {
                            isFactoryCallInProgress = false;
                        }
                    }
                    else
                    {
                        Debug.LogWarning("ModelDissolveAnimator: ANIMATION EXECUTE - Factory call already in progress, skipping");
                        yield break;
                    }
                }
            }
            
            // Execute the appropriate transition type with state tracking
            switch (request.type)
            {
                case TransitionType.ModelTransition:
                    if (targetModel != null)
                    {
                        yield return StartCoroutine(AnimationStateAwareTransition(request.oldModel, targetModel, request.onComplete));
                    }
                    else
                    {
                        Debug.LogWarning("ModelDissolveAnimator: ModelTransition has null target model, skipping");
                    }
                    break;
                    
                case TransitionType.ModelIn:
                    if (targetModel != null)
                    {
                        yield return StartCoroutine(AnimationStateAwareModelIn(targetModel, request.onComplete));
                    }
                    else
                    {
                        Debug.LogWarning("ModelDissolveAnimator: ModelIn has null target model, skipping");
                    }
                    break;
                    
                case TransitionType.ModelOut:
                    if (request.oldModel != null)
                    {
                        yield return StartCoroutine(AnimationStateAwareModelOut(request.oldModel, request.onComplete));
                    }
                    else
                    {
                        Debug.LogWarning("ModelDissolveAnimator: ModelOut request has null oldModel, skipping");
                    }
                    break;
                    
                default:
                    Debug.LogWarning($"ModelDissolveAnimator: Unknown transition type: {request.type}");
                    break;
            }
            
            Debug.Log($"ModelDissolveAnimator: ANIMATION EXECUTE - Completed {request.type}");
        }
        
        /// <summary>
        /// Model transition with proper animation state tracking
        /// </summary>
        private IEnumerator AnimationStateAwareTransition(GameObject oldModel, GameObject newModel, System.Action onComplete)
        {
            OnTransitionStarted?.Invoke(newModel);
            
            // Phase 1: Animate out old model (if exists)
            if (oldModel != null)
            {
                queueManager?.SetAnimationState(AnimationQueueManager<TransitionRequest>.AnimationState.FadingOut);
                currentlyAnimatingModel = oldModel;
                Debug.Log($"ModelDissolveAnimator: Starting FADE OUT animation for {oldModel.name}");
                
                SetupModelForTransition(oldModel);
                yield return StartCoroutine(AnimateModelOutCoroutine(oldModel));
                
                CleanupModelTransition(oldModel);
                Debug.Log($"ModelDissolveAnimator: Completed FADE OUT animation for {oldModel.name}");
            }
            
            // Phase 2: Animate in new model (if exists)
            if (newModel != null)
            {
                queueManager?.SetAnimationState(AnimationQueueManager<TransitionRequest>.AnimationState.FadingIn);
                currentlyAnimatingModel = newModel;
                currentModel = newModel; // Update tracking
                Debug.Log($"ModelDissolveAnimator: Starting FADE IN animation for {newModel.name}");
                
                SetupModelForTransition(newModel);
                yield return StartCoroutine(AnimateModelInCoroutine(newModel));
                
                Debug.Log($"ModelDissolveAnimator: Completed FADE IN animation for {newModel.name}");
            }
            
            // Reset animation state
            queueManager?.SetAnimationState(AnimationQueueManager<TransitionRequest>.AnimationState.Idle);
            currentlyAnimatingModel = null;
            
            OnTransitionCompleted?.Invoke(newModel);
            onComplete?.Invoke();
            
            Debug.Log("ModelDissolveAnimator: Full transition completed - state reset to Idle");
        }
        
        /// <summary>
        /// Model in only with proper animation state tracking
        /// </summary>
        private IEnumerator AnimationStateAwareModelIn(GameObject model, System.Action onComplete)
        {
            queueManager?.SetAnimationState(AnimationQueueManager<TransitionRequest>.AnimationState.FadingIn);
            currentlyAnimatingModel = model;
            currentModel = model; // Update tracking
            Debug.Log($"ModelDissolveAnimator: Starting FADE IN ONLY animation for {model.name}");
            
            OnTransitionStarted?.Invoke(model);
            
            SetupModelForTransition(model);
            yield return StartCoroutine(AnimateModelInCoroutine(model));
            
            // Reset animation state
            queueManager?.SetAnimationState(AnimationQueueManager<TransitionRequest>.AnimationState.Idle);
            currentlyAnimatingModel = null;
            
            OnTransitionCompleted?.Invoke(model);
            onComplete?.Invoke();
            
            Debug.Log($"ModelDissolveAnimator: Completed FADE IN ONLY animation for {model.name} - state reset to Idle");
        }
        
        /// <summary>
        /// Model out only with proper animation state tracking
        /// </summary>
        private IEnumerator AnimationStateAwareModelOut(GameObject model, System.Action onComplete)
        {
            queueManager?.SetAnimationState(AnimationQueueManager<TransitionRequest>.AnimationState.FadingOut);
            currentlyAnimatingModel = model;
            Debug.Log($"ModelDissolveAnimator: Starting FADE OUT ONLY animation for {model.name}");
            
            OnTransitionStarted?.Invoke(model);
            
            SetupModelForTransition(model);
            yield return StartCoroutine(AnimateModelOutCoroutine(model));
            CleanupModelTransition(model);
            
            // Clear current model if we just faded it out
            if (currentModel == model)
            {
                currentModel = null;
            }
            
            // Reset animation state
            queueManager?.SetAnimationState(AnimationQueueManager<TransitionRequest>.AnimationState.Idle);
            currentlyAnimatingModel = null;
            
            OnTransitionCompleted?.Invoke(model);
            onComplete?.Invoke();
            
            Debug.Log($"ModelDissolveAnimator: Completed FADE OUT ONLY animation for {model.name} - state reset to Idle");
        }
        
        /// <summary>
        /// Executes a single transition request (lazy factory approach - factory called when animation starts)
        /// </summary>
        private IEnumerator ExecuteTransitionRequest(TransitionRequest request)
        {
            if (request == null) 
            {
                Debug.LogWarning("ModelDissolveAnimator: Null transition request, skipping");
                yield break;
            }
            
            // Execute the appropriate transition type using the animation-state-aware methods
            switch (request.type)
            {
                case TransitionType.ModelTransition:
                    // Use factory to create new model if needed
                    GameObject newModel = request.newModel;
                    if (newModel == null && request.modelFactory != null)
                    {
                        Debug.Log("ModelDissolveAnimator: Creating model using factory for transition");
                        newModel = request.modelFactory();
                        currentModel = newModel; // Update tracking
                    }
                    yield return StartCoroutine(AnimationStateAwareTransition(request.oldModel, newModel, request.onComplete));
                    break;
                    
                case TransitionType.ModelIn:
                    // Use factory to create new model if needed
                    GameObject modelToAnimateIn = request.newModel;
                    if (modelToAnimateIn == null && request.modelFactory != null)
                    {
                        Debug.Log("ModelDissolveAnimator: Creating model using factory for ModelIn");
                        modelToAnimateIn = request.modelFactory();
                        currentModel = modelToAnimateIn; // Update tracking
                    }
                    yield return StartCoroutine(AnimationStateAwareModelIn(modelToAnimateIn, request.onComplete));
                    break;
                    
                case TransitionType.ModelOut:
                    if (request.oldModel != null)
                    {
                        yield return StartCoroutine(AnimationStateAwareModelOut(request.oldModel, request.onComplete));
                    }
                    else
                    {
                        Debug.LogWarning("ModelDissolveAnimator: ModelOut request has null oldModel, skipping");
                        request.onComplete?.Invoke();
                    }
                    break;
                    
                default:
                    Debug.LogWarning($"ModelDissolveAnimator: Unknown transition type: {request.type}");
                    request.onComplete?.Invoke();
                    break;
            }
        }
        
        /// <summary>
        /// Clear all cached materials and restore originals
        /// </summary>
        public void ClearMaterialCache()
        {
            foreach (var kvp in materialCache)
            {
                RestoreOriginalMaterials(kvp.Key);
            }
            materialCache.Clear();
        }
        
        /// <summary>
        /// Manually restore original materials for a specific model (for debugging)
        /// </summary>
        public void ForceRestoreOriginalMaterials(GameObject model)
        {
            RestoreOriginalMaterials(model);
            Debug.Log($"ModelDissolveAnimator: Force restored original materials for {model?.name ?? "null"}");
        }
        
        #endregion
        
        #region Configuration Methods
        
        public void SetTransitionTiming(float outDuration, float inDuration)
        {
            this.outDuration = outDuration;
            this.inDuration = inDuration;
            this.transitionDuration = outDuration + inDuration;
        }
        
        public void SetGlowSettings(Color glowColor, float intensity, bool additive = true)
        {
            this.glowColor = glowColor;
            this.glowIntensity = intensity;
            this.additiveGlow = additive;
        }
        
        public void SetFlashSettings(Color flashColor, float intensity, float duration)
        {
            this.flashColor = flashColor;
            this.flashIntensity = intensity;
            this.flashDuration = duration;
        }
        
        public void SetEffectsEnabled(bool dissolve, bool scale, bool glow, bool flash)
        {
            this.useDissolveEffect = dissolve;
            this.useScaleEffect = scale;
            this.useGlowEffect = glow;
            this.useFlashEffect = flash;
        }
        
        public void SetAudioClips(AudioClip teleportIn, AudioClip teleportOut, AudioClip flash)
        {
            this.teleportInSound = teleportIn;
            this.teleportOutSound = teleportOut;
            this.flashSound = flash;
        }
        
        public void SetAudioSource(AudioSource audioSource)
        {
            this.audioSource = audioSource;
        }
        
        public void SetBatchDelay(float delayTime)
        {
            // Legacy method - batch delay is no longer used in animation-respecting system
            Debug.Log("ModelDissolveAnimator: SetBatchDelay called but no longer used in animation-respecting system");
        }
        
        #endregion
        
        #region Properties
        
        public bool IsTransitioning => queueManager?.IsAnimating == true;
        
        public string GetTransitionInfo()
        {
            return $"ModelDissolveAnimator Info:\n" +
                   $"- Transition Duration: {transitionDuration}s (Out: {outDuration}s, In: {inDuration}s)\n" +
                   $"- Effects: Dissolve={useDissolveEffect}, Scale={useScaleEffect}, Glow={useGlowEffect}, Flash={useFlashEffect}\n" +
                   $"- Glow: Color={glowColor}, Intensity={glowIntensity}, Additive={additiveGlow}\n" +
                   $"- Flash: Color={flashColor}, Intensity={flashIntensity}, Duration={flashDuration}s\n" +
                   $"- Audio: Source={audioSource != null}, Volume={audioVolume}\n" +
                   $"- Cached Models: {materialCache.Count}";
        }
        
        #endregion
        
        // Removed old Core Animation Logic methods that used deprecated variables
        
        #region Model Setup and Cleanup
        
        private void SetupModelForTransition(GameObject model)
        {
            if (model == null) return;
            
            // Debug.Log($"ModelDissolveAnimator: Setting up model for transition: {model.name}");
            
            // Find all renderers in the model
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) 
            {
                Debug.LogWarning($"ModelDissolveAnimator: No renderers found on {model.name}");
                return;
            }
            
            // If already in cache, clean up first
            if (materialCache.ContainsKey(model))
            {
                // Debug.Log($"ModelDissolveAnimator: Model {model.name} already in cache, cleaning up first");
                CleanupModelTransition(model);
            }
            
            // Cache original materials and properties
            var cache = new ModelMaterialCache();
            cache.originalScale = model.transform.localScale;
            
            List<Material> originalMats = new List<Material>();
            List<Material> effectMats = new List<Material>();
            
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                
                Material[] rendererMaterials = renderer.materials;
                Material[] rendererEffectMaterials = new Material[rendererMaterials.Length];
                
                for (int i = 0; i < rendererMaterials.Length; i++)
                {
                    Material originalMat = rendererMaterials[i];
                    if (originalMat == null)
                    {
                        Debug.LogWarning($"ModelDissolveAnimator: Null material found on {renderer.name} at index {i}");
                        continue;
                    }
                    
                    originalMats.Add(originalMat);
                    
                    // Cache original properties
                    CacheOriginalMaterialProperties(originalMat, cache);
                    
                    // Create effect material
                    Material effectMat = CreateEffectMaterial(originalMat);
                    if (effectMat != null)
                    {
                        effectMats.Add(effectMat);
                        rendererEffectMaterials[i] = effectMat;
                    }
                    else
                    {
                        // Fallback to original material if effect creation failed
                        rendererEffectMaterials[i] = originalMat;
                    }
                }
                
                // Apply effect materials to renderer
                try
                {
                    renderer.materials = rendererEffectMaterials;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"ModelDissolveAnimator: Failed to apply materials to {renderer.name}: {e.Message}");
                }
            }
            
            cache.originalMaterials = originalMats.ToArray();
            cache.effectMaterials = effectMats.ToArray();
            
            // Store cache
            materialCache[model] = cache;
            
            // Setup audio source if needed
            SetupAudioSource(model);
            
            // Debug.Log($"ModelDissolveAnimator: Setup complete for {model.name} - {originalMats.Count} materials cached");
        }
        
        private void SetupAudioSource(GameObject model)
        {
            if (audioSource == null)
            {
                audioSource = model.GetComponent<AudioSource>();
                if (audioSource == null && model.transform.parent != null)
                {
                    audioSource = model.transform.parent.GetComponent<AudioSource>();
                }
                if (audioSource == null)
                {
                    audioSource = gameObject.GetComponent<AudioSource>();
                    if (audioSource == null)
                    {
                        audioSource = gameObject.AddComponent<AudioSource>();
                        audioSource.playOnAwake = false;
                        audioSource.volume = audioVolume;
                    }
                }
            }
        }
        
        private void CacheOriginalMaterialProperties(Material material, ModelMaterialCache cache)
        {
            if (material == null || cache == null) return;
            
            try
            {
                var propData = new MaterialPropertyData();
                
                // Cache emission
                if (!string.IsNullOrEmpty(emissionPropertyName) && material.HasProperty(emissionPropertyName))
                {
                    propData.originalEmission = material.GetColor(emissionPropertyName);
                    propData.hasEmission = propData.originalEmission.maxColorComponent > 0.01f;
                }
                
                // Cache base color/alpha
                if (material.HasProperty("_BaseColor"))
                {
                    propData.originalBaseColor = material.GetColor("_BaseColor");
                    propData.originalAlpha = propData.originalBaseColor.a;
                }
                else if (material.HasProperty("_Color"))
                {
                    propData.originalBaseColor = material.GetColor("_Color");
                    propData.originalAlpha = propData.originalBaseColor.a;
                }
                
                // Cache dissolve property
                string[] dissolveProperties = { "_DissolveAmount", "_Cutoff", "_Alpha", "_Dissolve", "_Fade" };
                foreach (string propName in dissolveProperties)
                {
                    if (material.HasProperty(propName))
                    {
                        propData.hasDissolveProperty = true;
                        propData.dissolvePropertyName = propName;
                        propData.originalDissolveAmount = material.GetFloat(propName);
                        break;
                    }
                }
                
                // Cache keywords
                propData.originalKeywords = material.shaderKeywords;
                
                cache.originalProperties[material] = propData;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ModelDissolveAnimator: Failed to cache properties for material {material.name}: {e.Message}");
            }
        }
        
        private Material CreateEffectMaterial(Material originalMaterial)
        {
            if (originalMaterial == null)
            {
                Debug.LogWarning("ModelDissolveAnimator: Cannot create effect material from null original material");
                return null;
            }
            
            try
            {
                Material effectMaterial;
                
                if (hasDissolveShader && dissolveShader != null)
                {
                    // Create material with dissolve shader (same as WeaponTeleportEffect)
                    effectMaterial = new Material(dissolveShader);
                    
                    // Copy properties from original material
                    CopyMaterialProperties(originalMaterial, effectMaterial);
                    
                    // Debug.Log($"ModelDissolveAnimator: Created material with dissolve shader for {originalMaterial.name}");
                }
                else
                {
                    // Fallback: use original material with alpha transparency
                    effectMaterial = new Material(originalMaterial);
                    
                    // Set up for transparency if using alpha fallback
                    if (useAlphaFallback)
                    {
                        SetupTransparentMaterial(effectMaterial);
                    }
                    
                    // Debug.Log($"ModelDissolveAnimator: Using alpha transparency fallback for {originalMaterial.name}");
                }
                
                // Set up dissolve properties
                SetupDissolveProperties(effectMaterial);
                
                return effectMaterial;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ModelDissolveAnimator: Failed to create effect material from {originalMaterial.name}: {e.Message}");
                return null;
            }
        }
        
        private void CopyMaterialProperties(Material source, Material destination)
        {
            // Copy only essential properties to avoid type conflicts
            try
            {
                // Copy main texture
                if (source.HasProperty("_BaseMap") && destination.HasProperty("_BaseMap"))
                {
                    destination.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                }
                else if (source.HasProperty("_MainTex") && destination.HasProperty("_MainTex"))
                {
                    destination.SetTexture("_MainTex", source.GetTexture("_MainTex"));
                }
                
                // Copy main color
                if (source.HasProperty("_BaseColor") && destination.HasProperty("_BaseColor"))
                {
                    destination.SetColor("_BaseColor", source.GetColor("_BaseColor"));
                }
                else if (source.HasProperty("_Color") && destination.HasProperty("_Color"))
                {
                    destination.SetColor("_Color", source.GetColor("_Color"));
                }
                
                // Copy metallic value only (avoid texture property check that causes errors)
                if (source.HasProperty("_Metallic") && destination.HasProperty("_Metallic"))
                {
                    try
                    {
                        destination.SetFloat("_Metallic", source.GetFloat("_Metallic"));
                    }
                    catch
                    {
                        // Skip if not a float property
                    }
                }
                
                if (source.HasProperty("_Smoothness") && destination.HasProperty("_Smoothness"))
                {
                    destination.SetFloat("_Smoothness", source.GetFloat("_Smoothness"));
                }
                
                // Debug.Log($"ModelDissolveAnimator: Copied essential properties from {source.name} to dissolve material");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"ModelDissolveAnimator: Failed to copy some properties from {source.name}: {e.Message}");
            }
        }
        
        private void SetupDissolveProperties(Material material)
        {
            if (material == null) return;
            
            // Set dissolve texture (use assigned texture or generated one)
            if (material.HasProperty("_DissolveTexture"))
            {
                Texture2D noiseTexture = dissolveNoiseTexture != null ? dissolveNoiseTexture : defaultNoiseTexture;
                material.SetTexture("_DissolveTexture", noiseTexture);
            }
            
            // Set initial dissolve amount (fully visible)
            if (!string.IsNullOrEmpty(dissolvePropertyName) && material.HasProperty(dissolvePropertyName))
            {
                material.SetFloat(dissolvePropertyName, dissolveEnd);
            }
            
            // Set initial emission
            if (!string.IsNullOrEmpty(emissionPropertyName) && material.HasProperty(emissionPropertyName))
            {
                material.SetColor(emissionPropertyName, Color.black);
                material.EnableKeyword("_EMISSION");
            }
        }
        
        private void SetupTransparentMaterial(Material material)
        {
            // Try to set up transparency for URP materials
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1); // Transparent
            }
            
            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0); // Alpha blend
            }
            
            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 0); // Disable alpha clipping
            }
            
            // Set render queue for transparency
            material.renderQueue = 3000;
            
            // Enable transparency keywords
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        
        private void RestoreOriginalMaterials(GameObject model)
        {
            if (model == null || !materialCache.ContainsKey(model)) return;
            
            try
            {
                var cache = materialCache[model];
                if (cache == null) return;
                
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
                
                int materialIndex = 0;
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null) continue;
                    
                    Material[] currentMaterials = renderer.materials;
                    Material[] restoredMaterials = new Material[currentMaterials.Length];
                    
                    for (int i = 0; i < restoredMaterials.Length; i++)
                    {
                        if (materialIndex < cache.originalMaterials.Length && cache.originalMaterials[materialIndex] != null)
                        {
                            restoredMaterials[i] = cache.originalMaterials[materialIndex];
                        }
                        else
                        {
                            // Fallback to current material if original not available
                            restoredMaterials[i] = currentMaterials[i];
                        }
                        materialIndex++;
                    }
                    
                    try
                    {
                        renderer.materials = restoredMaterials;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"ModelDissolveAnimator: Failed to restore materials on {renderer.name}: {e.Message}");
                    }
                }
                
                // Restore original scale
                model.transform.localScale = cache.originalScale;
                
                // Restore original material properties
                RestoreOriginalMaterialProperties(cache);
                
                // Destroy effect materials
                if (cache.effectMaterials != null)
                {
                    foreach (Material effectMat in cache.effectMaterials)
                    {
                        if (effectMat != null)
                        {
                            DestroyImmediate(effectMat);
                        }
                    }
                }
                
                // Debug.Log($"ModelDissolveAnimator: Restored original materials for {model.name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ModelDissolveAnimator: Failed to restore materials for {model.name}: {e.Message}");
            }
        }
        
        private void RestoreOriginalMaterialProperties(ModelMaterialCache cache)
        {
            if (cache?.originalProperties == null) return;
            
            foreach (var kvp in cache.originalProperties)
            {
                Material material = kvp.Key;
                MaterialPropertyData propData = kvp.Value;
                
                if (material == null) continue;
                
                try
                {
                    // Restore emission
                    if (!string.IsNullOrEmpty(emissionPropertyName) && material.HasProperty(emissionPropertyName))
                    {
                        material.SetColor(emissionPropertyName, propData.originalEmission);
                        
                        // Restore emission keyword state
                        if (propData.hasEmission)
                        {
                            material.EnableKeyword("_EMISSION");
                        }
                        else
                        {
                            material.DisableKeyword("_EMISSION");
                        }
                    }
                    
                    // Restore base color/alpha
                    if (material.HasProperty("_BaseColor"))
                    {
                        material.SetColor("_BaseColor", propData.originalBaseColor);
                    }
                    else if (material.HasProperty("_Color"))
                    {
                        material.SetColor("_Color", propData.originalBaseColor);
                    }
                    
                    // Restore dissolve property
                    if (propData.hasDissolveProperty && !string.IsNullOrEmpty(propData.dissolvePropertyName))
                    {
                        if (material.HasProperty(propData.dissolvePropertyName))
                        {
                            material.SetFloat(propData.dissolvePropertyName, propData.originalDissolveAmount);
                        }
                    }
                    
                    // Restore keywords
                    if (propData.originalKeywords != null)
                    {
                        material.shaderKeywords = propData.originalKeywords;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"ModelDissolveAnimator: Error restoring properties for material {material.name}: {e.Message}");
                }
            }
            
            // Debug.Log($"ModelDissolveAnimator: Restored original material properties for {cache.originalProperties.Count} materials");
        }

        private void CleanupModelTransition(GameObject model)
        {
            if (model == null) return;
            
            // Clean up cached materials but don't restore them (model might be destroyed)
            if (materialCache.ContainsKey(model))
            {
                var cache = materialCache[model];
                
                // Destroy effect materials to prevent memory leaks
                foreach (Material effectMat in cache.effectMaterials)
                {
                    if (effectMat != null)
                    {
                        DestroyImmediate(effectMat);
                    }
                }
                
                materialCache.Remove(model);
            }
        }
        
        /// <summary>
        /// Force stop transition and clean up all cached materials
        /// </summary>
        private void ForceStopTransition()
        {
            // Stop current animation coroutine if running
            if (currentAnimationCoroutine != null)
            {
                StopCoroutine(currentAnimationCoroutine);
                currentAnimationCoroutine = null;
            }
            
            // Reset animation state
            queueManager?.SetAnimationState(AnimationQueueManager<TransitionRequest>.AnimationState.Idle);
            currentlyAnimatingModel = null;
            
            // Clean up all cached materials
            var modelsToCleanup = new List<GameObject>(materialCache.Keys);
            foreach (GameObject model in modelsToCleanup)
            {
                if (model != null)
                {
                    RestoreOriginalMaterials(model);
                }
                CleanupModelTransition(model);
            }
            materialCache.Clear();
            
            Debug.Log("ModelDissolveAnimator: Force stopped transition and cleaned up cache");
        }
        
        #endregion
        
        #region Animation Coroutines
        
        private IEnumerator AnimateModelOutCoroutine(GameObject model)
        {
            if (model == null)
            {
                Debug.LogWarning("ModelDissolveAnimator: Cannot animate null model out");
                yield break;
            }
            
            // Debug.Log($"ModelDissolveAnimator: Animating model out: {model.name}");
            
            // Play teleport out sound
            PlaySound(teleportOutSound);
            
            float elapsedTime = 0f;
            Vector3 originalScale = GetOriginalScale(model);
            
            while (elapsedTime < outDuration && model != null)
            {
                float progress = elapsedTime / outDuration;
                
                // Dissolve effect
                if (useDissolveEffect)
                {
                    float dissolveAmount = Mathf.Lerp(dissolveEnd, dissolveStart, progress);
                    SetModelDissolveAmount(model, dissolveAmount);
                }
                
                // Scale effect
                if (useScaleEffect && model.transform != null)
                {
                    float scaleFactor = modelScaleCurve.Evaluate(1f - progress);
                    model.transform.localScale = Vector3.Lerp(modelStartScale, originalScale, scaleFactor);
                }
                
                // Glow effect
                if (useGlowEffect)
                {
                    float glowFactor = glowCurve.Evaluate(1f - progress);
                    Color currentGlow = glowColor * (glowIntensity * glowFactor);
                    SetModelEmissionColor(model, currentGlow);
                }
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // Ensure final values (if model still exists)
            if (model != null)
            {
                if (useDissolveEffect) SetModelDissolveAmount(model, dissolveStart);
                if (useScaleEffect && model.transform != null) model.transform.localScale = modelStartScale;
                if (useGlowEffect) SetModelEmissionColor(model, Color.black);
                
                // Hide the model
                model.SetActive(false);
            }
        }
        
        private IEnumerator AnimateModelInCoroutine(GameObject model)
        {
            if (model == null)
            {
                Debug.LogWarning("ModelDissolveAnimator: Cannot animate null model in");
                yield break;
            }
            
            // Debug.Log($"ModelDissolveAnimator: Animating model in: {model.name}");
            
            // Ensure model is active and renderers are enabled
            model.SetActive(true);
            EnableModelRenderers(model);
            
            // Set initial state
            Vector3 originalScale = GetOriginalScale(model);
            if (useScaleEffect && model.transform != null) model.transform.localScale = modelStartScale;
            if (useDissolveEffect) SetModelDissolveAmount(model, dissolveStart);
            
            // Play teleport in sound
            PlaySound(teleportInSound);
            
            float elapsedTime = 0f;
            
            while (elapsedTime < inDuration && model != null)
            {
                float progress = elapsedTime / inDuration;
                
                // Dissolve effect
                if (useDissolveEffect)
                {
                    float dissolveAmount = Mathf.Lerp(dissolveStart, dissolveEnd, progress);
                    SetModelDissolveAmount(model, dissolveAmount);
                }
                
                // Scale effect
                if (useScaleEffect && model.transform != null)
                {
                    float scaleFactor = modelScaleCurve.Evaluate(progress);
                    model.transform.localScale = Vector3.Lerp(modelStartScale, originalScale, scaleFactor);
                }
                
                // Glow effect
                if (useGlowEffect)
                {
                    float glowFactor = glowCurve.Evaluate(progress);
                    Color currentGlow = glowColor * (glowIntensity * glowFactor);
                    SetModelEmissionColor(model, currentGlow);
                }
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // Ensure final values (if model still exists)
            if (model != null)
            {
                // Restore original materials BEFORE final effects to prevent black/red mess
                RestoreOriginalMaterials(model);
                
                if (useScaleEffect && model.transform != null) model.transform.localScale = originalScale;
                if (useGlowEffect) SetModelEmissionColor(model, Color.black);
                
                // Flash effect (now on original materials)
                if (useFlashEffect)
                {
                    yield return StartCoroutine(PlayFlashEffect(model));
                }
            }
        }
        
        private IEnumerator PlayFlashEffect(GameObject model)
        {
            // Debug.Log($"ModelDissolveAnimator: Playing flash effect on: {model.name}");
            
            // Play flash sound
            PlaySound(flashSound);
            
            float elapsedTime = 0f;
            
            while (elapsedTime < flashDuration)
            {
                float progress = elapsedTime / flashDuration;
                float flashFactor = flashCurve.Evaluate(progress);
                
                Color currentFlash = flashColor * (flashIntensity * flashFactor);
                SetModelEmissionColor(model, currentFlash);
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // Return to no glow
            SetModelEmissionColor(model, Color.black);
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Enables all renderers in a model (needed when model was prepared with disabled renderers)
        /// </summary>
        private void EnableModelRenderers(GameObject model)
        {
            if (model == null) return;
            
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
            
            // Debug.Log($"ModelDissolveAnimator: Enabled {renderers.Length} renderers for {model.name}");
        }
        
        private Vector3 GetOriginalScale(GameObject model)
        {
            if (materialCache.ContainsKey(model))
            {
                return materialCache[model].originalScale;
            }
            return model.transform.localScale;
        }
        
        private void SetModelDissolveAmount(GameObject model, float amount)
        {
            if (!useDissolveEffect || model == null) return;
            
            try
            {
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
                int totalMaterials = 0;
                int dissolvePropertiesFound = 0;
                int alphaFallbacksUsed = 0;
                
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null) continue;
                    
                    Material[] materials = renderer.materials;
                    if (materials == null) continue;
                    
                    foreach (Material material in materials)
                    {
                        if (material == null) continue;
                        totalMaterials++;
                        
                        if (hasDissolveShader && !string.IsNullOrEmpty(dissolvePropertyName) && material.HasProperty(dissolvePropertyName))
                        {
                            // Use dissolve shader
                            material.SetFloat(dissolvePropertyName, amount);
                            dissolvePropertiesFound++;
                        }
                        else if (useAlphaFallback)
                        {
                            // Use alpha transparency fallback (invert: 0 = invisible, 1 = visible)
                            float alpha = 1f - amount;
                            SetMaterialAlpha(material, alpha);
                            alphaFallbacksUsed++;
                        }
                        else
                        {
                            Debug.LogWarning($"ModelDissolveAnimator: No dissolve property found on material {material.name}, and alpha fallback is disabled");
                        }
                    }
                }
                
                // Debug.Log($"ModelDissolveAnimator: Dissolve amount {amount} applied to {model.name} - {totalMaterials} materials total, {dissolvePropertiesFound} dissolve properties, {alphaFallbacksUsed} alpha fallbacks");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ModelDissolveAnimator: Error setting dissolve amount on {model.name}: {e.Message}");
            }
        }
        
        private void SetMaterialAlpha(Material material, float alpha)
        {
            if (material == null) return;
            
            try
            {
                if (material.HasProperty("_BaseColor"))
                {
                    Color color = material.GetColor("_BaseColor");
                    color.a = alpha;
                    material.SetColor("_BaseColor", color);
                }
                else if (material.HasProperty("_Color"))
                {
                    Color color = material.GetColor("_Color");
                    color.a = alpha;
                    material.SetColor("_Color", color);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ModelDissolveAnimator: Error setting material alpha: {e.Message}");
            }
        }
        
        private void SetModelEmissionColor(GameObject model, Color color)
        {
            if ((!useGlowEffect && !useFlashEffect) || model == null) return;
            
            try
            {
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null) continue;
                    
                    Material[] materials = renderer.materials;
                    if (materials == null) continue;
                    
                    foreach (Material material in materials)
                    {
                        if (material == null || string.IsNullOrEmpty(emissionPropertyName) || !material.HasProperty(emissionPropertyName)) continue;
                        
                        try
                        {
                            if (additiveGlow && materialCache.ContainsKey(model))
                            {
                                // Find original material to get base emission
                                Material originalMaterial = FindOriginalMaterial(material, materialCache[model]);
                                if (originalMaterial != null && materialCache[model].originalProperties.ContainsKey(originalMaterial))
                                {
                                    Color baseEmission = materialCache[model].originalProperties[originalMaterial].originalEmission;
                                    material.SetColor(emissionPropertyName, baseEmission + color);
                                }
                                else
                                {
                                    material.SetColor(emissionPropertyName, color);
                                }
                            }
                            else
                            {
                                material.SetColor(emissionPropertyName, color);
                            }
                            
                            // Enable/disable emission keyword based on intensity
                            if (color.maxColorComponent > 0.01f)
                            {
                                material.EnableKeyword("_EMISSION");
                            }
                            else
                            {
                                material.DisableKeyword("_EMISSION");
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError($"ModelDissolveAnimator: Error setting emission on material {material.name}: {e.Message}");
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ModelDissolveAnimator: Error setting emission color on {model.name}: {e.Message}");
            }
        }
        
        private Material FindOriginalMaterial(Material effectMaterial, ModelMaterialCache cache)
        {
            for (int i = 0; i < cache.effectMaterials.Length; i++)
            {
                if (cache.effectMaterials[i] == effectMaterial && i < cache.originalMaterials.Length)
                {
                    return cache.originalMaterials[i];
                }
            }
            return null;
        }
        
        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, audioVolume);
            }
        }
        
        #endregion
        
        #region Cleanup
        
        private void OnDestroy()
        {
            StopTransition();
            ClearMaterialCache();
            
            // Clean up generated texture
            if (defaultNoiseTexture != null)
            {
                DestroyImmediate(defaultNoiseTexture);
                defaultNoiseTexture = null;
            }
        }
        
        private void OnDisable()
        {
            StopTransition();
        }
        
        #endregion
        
        #region Context Menu Testing
        
        [ContextMenu("Test Flash Effect")]
        public void TestFlashEffect()
        {
            GameObject testModel = GameObject.FindObjectOfType<Renderer>()?.gameObject;
            if (testModel != null)
            {
                Debug.Log("ModelDissolveAnimator: Testing flash effect");
                SetupModelForTransition(testModel);
                StartCoroutine(PlayFlashEffect(testModel));
            }
            else
            {
                Debug.LogWarning("ModelDissolveAnimator: No renderer found for testing");
            }
        }
        
        [ContextMenu("Print Info")]
        public void PrintInfo()
        {
            Debug.Log(GetTransitionInfo());
        }
        
        [ContextMenu("Force Restore All Original Materials")]
        public void ForceRestoreAllOriginalMaterials()
        {
            foreach (var kvp in materialCache)
            {
                RestoreOriginalMaterials(kvp.Key);
            }
            Debug.Log($"ModelDissolveAnimator: Force restored original materials for {materialCache.Count} cached models");
        }
        
        #endregion
    }
} 