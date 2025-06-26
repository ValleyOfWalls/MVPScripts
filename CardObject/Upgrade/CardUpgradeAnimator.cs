using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using System;

namespace CardUpgrade
{
    /// <summary>
    /// Queued upgrade animation data structure
    /// </summary>
    [System.Serializable]
    public class QueuedUpgradeAnimation
    {
        public CardData baseCard;
        public CardData upgradedCard;
        public NetworkEntity entity;
        public bool upgradeAllCopies;
        public System.Action<CardData, CardData, NetworkEntity, bool> onComplete;
        public float queueTime;
    }

    /// <summary>
    /// Manages card upgrade animations, effects, and queuing
    /// Attach to: Singleton GameObject in scene (like GameManager or dedicated UpgradeManager)
    /// </summary>
    public class CardUpgradeAnimator : MonoBehaviour
    {
        [Header("Animation Timing")]
        [SerializeField] private float appearDuration = 0.5f;
        [SerializeField] private float displayBaseDuration = 1.0f;
        [SerializeField] private float transitionDuration = 0.8f;
        [SerializeField] private float displayUpgradedDuration = 1.5f;
        [SerializeField] private float fadeOutDuration = 0.5f;
        
        [Header("Animation Curves")]
        [SerializeField] private AnimationCurve scaleInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve shakeIntensityCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        
        [Header("Visual Effects")]
        [SerializeField] private Color transitionColor = Color.white;
        [SerializeField] private bool enableScreenShake = true;
        [SerializeField] private float shakeIntensity = 0.1f;
        [SerializeField] private bool pauseGameDuringUpgrade = true;
        
        [Header("Audio")]
        [SerializeField] private AudioClip upgradeStartSound;
        [SerializeField] private AudioClip transitionSound;
        [SerializeField] private AudioClip upgradeCompleteSound;
        [SerializeField] private AudioSource audioSource;
        
        [Header("Prefab Reference")]
        [SerializeField] private GameObject cardUpgradeDisplayPrefab;
        
        // Singleton
        public static CardUpgradeAnimator Instance { get; private set; }
        
        // Runtime state
        private Queue<QueuedUpgradeAnimation> animationQueue = new Queue<QueuedUpgradeAnimation>();
        private bool isPlayingAnimation = false;
        private GameObject currentUpgradeDisplay;
        private CardUpgradeUIController currentUIController;
        private Camera mainCamera;
        private Vector3 originalCameraPosition;
        private float originalTimeScale;
        
        private void Awake()
        {
            // Singleton setup
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void Initialize()
        {
            // Get main camera reference
            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                originalCameraPosition = mainCamera.transform.position;
            }
            
            // Setup audio source if not assigned
            if (audioSource == null)
            {
                audioSource = gameObject.GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            
            // Store original time scale
            originalTimeScale = Time.timeScale;
            
            Debug.Log("CardUpgradeAnimator initialized successfully");
        }
        
        /// <summary>
        /// Queue an upgrade animation to be played
        /// </summary>
        public void QueueUpgradeAnimation(CardData baseCard, CardData upgradedCard, NetworkEntity entity, bool upgradeAllCopies, System.Action<CardData, CardData, NetworkEntity, bool> onComplete)
        {
            if (baseCard == null || upgradedCard == null)
            {
                Debug.LogError("CardUpgradeAnimator: Cannot queue upgrade with null card data");
                onComplete?.Invoke(baseCard, upgradedCard, entity, upgradeAllCopies);
                return;
            }
            
            var queuedAnimation = new QueuedUpgradeAnimation
            {
                baseCard = baseCard,
                upgradedCard = upgradedCard,
                entity = entity,
                upgradeAllCopies = upgradeAllCopies,
                onComplete = onComplete,
                queueTime = Time.time
            };
            
            animationQueue.Enqueue(queuedAnimation);
            Debug.Log($"CardUpgradeAnimator: Queued upgrade animation for {baseCard.CardName} → {upgradedCard.CardName} (Queue size: {animationQueue.Count})");
            
            // Start playing if not already playing
            if (!isPlayingAnimation)
            {
                StartCoroutine(PlayNextUpgradeInQueue());
            }
        }
        
        /// <summary>
        /// Play the next upgrade animation in the queue
        /// </summary>
        private IEnumerator PlayNextUpgradeInQueue()
        {
            while (animationQueue.Count > 0)
            {
                isPlayingAnimation = true;
                var nextUpgrade = animationQueue.Dequeue();
                
                Debug.Log($"CardUpgradeAnimator: Starting animation for {nextUpgrade.baseCard.CardName} → {nextUpgrade.upgradedCard.CardName}");
                
                yield return StartCoroutine(PlayUpgradeAnimation(nextUpgrade));
                
                // Complete callback
                nextUpgrade.onComplete?.Invoke(nextUpgrade.baseCard, nextUpgrade.upgradedCard, nextUpgrade.entity, nextUpgrade.upgradeAllCopies);
            }
            
            isPlayingAnimation = false;
            Debug.Log("CardUpgradeAnimator: Animation queue completed");
        }
        
        /// <summary>
        /// Play a single upgrade animation
        /// </summary>
        private IEnumerator PlayUpgradeAnimation(QueuedUpgradeAnimation upgrade)
        {
            // Pause game if enabled
            if (pauseGameDuringUpgrade)
            {
                Time.timeScale = 0f;
            }
            
            // Create upgrade display if needed
            yield return StartCoroutine(EnsureUpgradeDisplay());
            
            if (currentUIController == null)
            {
                Debug.LogError("CardUpgradeAnimator: Failed to create upgrade display");
                yield break;
            }
            
            // Reset display for new animation
            currentUIController.ResetDisplay();
            
            // Phase 1: Appear with base card
            PlayUpgradeAudio(upgradeStartSound);
            currentUIController.DisplayCard(upgrade.baseCard);
            yield return StartCoroutine(AnimateAppear());
            
            // Phase 2: Display base card
            yield return new WaitForSecondsRealtime(displayBaseDuration);
            
            // Phase 3: Transition effect
            PlayUpgradeAudio(transitionSound);
            if (enableScreenShake)
            {
                StartCoroutine(PlayScreenShake(shakeIntensity, transitionDuration));
            }
            yield return StartCoroutine(currentUIController.PlayWipeTransition(transitionDuration));
            
            // Phase 4: Show upgraded card
            currentUIController.DisplayCard(upgrade.upgradedCard);
            PlayUpgradeAudio(upgradeCompleteSound);
            
            // Phase 5: Display upgraded card
            yield return new WaitForSecondsRealtime(displayUpgradedDuration);
            
            // Phase 6: Fade out (only if this is the last animation)
            if (animationQueue.Count == 0)
            {
                yield return StartCoroutine(AnimateFadeOut());
                DestroyUpgradeDisplay();
            }
            
            // Resume game if enabled
            if (pauseGameDuringUpgrade)
            {
                Time.timeScale = originalTimeScale;
            }
        }
        
        /// <summary>
        /// Ensure upgrade display exists and get UI controller
        /// </summary>
        private IEnumerator EnsureUpgradeDisplay()
        {
            if (currentUpgradeDisplay == null)
            {
                if (cardUpgradeDisplayPrefab == null)
                {
                    Debug.LogError("CardUpgradeAnimator: Card upgrade display prefab not assigned!");
                    yield break;
                }
                
                currentUpgradeDisplay = Instantiate(cardUpgradeDisplayPrefab);
                currentUIController = currentUpgradeDisplay.GetComponent<CardUpgradeUIController>();
                
                if (currentUIController == null)
                {
                    Debug.LogError("CardUpgradeAnimator: Upgrade display prefab missing CardUpgradeUIController component!");
                    DestroyUpgradeDisplay();
                    yield break;
                }
                
                Debug.Log("CardUpgradeAnimator: Created upgrade display");
            }
            
            yield return null;
        }
        
        /// <summary>
        /// Animate the upgrade display appearing
        /// </summary>
        private IEnumerator AnimateAppear()
        {
            if (currentUIController == null) yield break;
            
            float elapsed = 0f;
            Vector3 startScale = Vector3.zero;
            Vector3 endScale = Vector3.one;
            
            CanvasGroup canvasGroup = currentUpgradeDisplay.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            
            while (elapsed < appearDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = elapsed / appearDuration;
                
                // Scale animation
                float scaleProgress = scaleInCurve.Evaluate(progress);
                currentUpgradeDisplay.transform.localScale = Vector3.Lerp(startScale, endScale, scaleProgress);
                
                // Fade animation
                if (canvasGroup != null)
                {
                    float fadeProgress = fadeInCurve.Evaluate(progress);
                    canvasGroup.alpha = fadeProgress;
                }
                
                yield return null;
            }
            
            // Ensure final values
            currentUpgradeDisplay.transform.localScale = endScale;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }
        
        /// <summary>
        /// Animate the upgrade display fading out
        /// </summary>
        private IEnumerator AnimateFadeOut()
        {
            if (currentUIController == null) yield break;
            
            float elapsed = 0f;
            CanvasGroup canvasGroup = currentUpgradeDisplay.GetComponent<CanvasGroup>();
            float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
            
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = elapsed / fadeOutDuration;
                
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, progress);
                }
                
                yield return null;
            }
            
            // Ensure final value
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }
        
        /// <summary>
        /// Play screen shake effect
        /// </summary>
        private IEnumerator PlayScreenShake(float intensity, float duration)
        {
            if (!enableScreenShake || mainCamera == null) yield break;
            
            float elapsed = 0f;
            Vector3 originalPos = mainCamera.transform.position;
            
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = elapsed / duration;
                
                float currentIntensity = intensity * shakeIntensityCurve.Evaluate(progress);
                Vector3 randomOffset = new Vector3(
                    UnityEngine.Random.Range(-currentIntensity, currentIntensity),
                    UnityEngine.Random.Range(-currentIntensity, currentIntensity),
                    0f
                );
                
                mainCamera.transform.position = originalPos + randomOffset;
                
                yield return null;
            }
            
            // Reset camera position
            mainCamera.transform.position = originalPos;
        }
        
        /// <summary>
        /// Play upgrade audio clip
        /// </summary>
        private void PlayUpgradeAudio(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
        
        /// <summary>
        /// Destroy the current upgrade display
        /// </summary>
        private void DestroyUpgradeDisplay()
        {
            if (currentUpgradeDisplay != null)
            {
                Destroy(currentUpgradeDisplay);
                currentUpgradeDisplay = null;
                currentUIController = null;
                Debug.Log("CardUpgradeAnimator: Destroyed upgrade display");
            }
        }
        
        /// <summary>
        /// Get current queue size for debugging
        /// </summary>
        public int GetQueueSize()
        {
            return animationQueue.Count;
        }
        
        /// <summary>
        /// Check if currently playing animation
        /// </summary>
        public bool IsPlayingAnimation()
        {
            return isPlayingAnimation;
        }
        
        /// <summary>
        /// Clear all queued animations (emergency stop)
        /// </summary>
        public void ClearQueue()
        {
            animationQueue.Clear();
            isPlayingAnimation = false;
            DestroyUpgradeDisplay();
            
            // Restore time scale
            if (pauseGameDuringUpgrade)
            {
                Time.timeScale = originalTimeScale;
            }
            
            Debug.Log("CardUpgradeAnimator: Cleared animation queue and reset state");
        }
        
        private void OnDestroy()
        {
            // Clean up
            ClearQueue();
            
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        #if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure durations are positive
            appearDuration = Mathf.Max(0.1f, appearDuration);
            displayBaseDuration = Mathf.Max(0.1f, displayBaseDuration);
            transitionDuration = Mathf.Max(0.1f, transitionDuration);
            displayUpgradedDuration = Mathf.Max(0.1f, displayUpgradedDuration);
            fadeOutDuration = Mathf.Max(0.1f, fadeOutDuration);
            
            shakeIntensity = Mathf.Max(0f, shakeIntensity);
        }
        #endif
    }
} 