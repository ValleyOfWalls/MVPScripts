using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using System;
using FishNet.Object;
using MVPScripts.Utility;

namespace CardUpgrade
{
    /// <summary>
    /// Queued upgrade animation data structure for in-hand transformations
    /// </summary>
    [System.Serializable]
    public class QueuedUpgradeAnimation
    {
        public Card cardToUpgrade;  // The actual card GameObject in hand
        public CardData baseCard;
        public CardData upgradedCard;
        public NetworkEntity entity;
        public bool upgradeAllCopies;
        public System.Action<CardData, CardData, NetworkEntity, bool> onComplete;
        public float queueTime;
    }

    /// <summary>
    /// Manages in-hand card upgrade animations with burn and replacement effects
    /// Attach to: Singleton GameObject (GameManager recommended)
    /// </summary>
    public class CardUpgradeAnimator : MonoBehaviour
    {
        [Header("Animation Timing")]
        [SerializeField] private float handLayoutWaitTime = 0.5f; // Wait for hand layout to settle
        [SerializeField] private float burnEffectDuration = 1.0f;
        [SerializeField] private float transformPauseDuration = 0.2f;
        [SerializeField] private float replaceEffectDuration = 0.8f;
        
        [Header("Visual Effects")]
        [SerializeField] private bool enableScreenShake = true;
        [SerializeField] private float shakeIntensity = 0.05f; // Reduced for subtlety
        [SerializeField] private Color burnColor = Color.red;
        [SerializeField] private Color replaceGlowColor = Color.yellow;
        
        [Header("Animation Curves")]
        [SerializeField] private AnimationCurve burnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve replaceCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve shakeIntensityCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        
        [Header("Audio")]
        [SerializeField] private AudioClip burnSound;
        [SerializeField] private AudioClip transformSound;
        [SerializeField] private AudioClip replaceSound;
        [SerializeField] private AudioSource audioSource;

        // Singleton
        public static CardUpgradeAnimator Instance { get; private set; }

        // Private fields
        private Queue<QueuedUpgradeAnimation> animationQueue = new Queue<QueuedUpgradeAnimation>();
        private bool isPlayingAnimation = false;
        private Camera mainCamera;

        private void Awake()
        {
            // Singleton setup
            if (Instance == null)
            {
                Instance = this;
                //DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            Debug.Log("[CARD_UPGRADE] CardUpgradeAnimator initialized for in-hand transformations");
            
            // Find main camera for screen shake
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                ComponentResolver.FindComponent(ref mainCamera, gameObject);
            }
            
            // Set up audio source if not assigned
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        /// <summary>
        /// Queue an in-hand upgrade animation
        /// </summary>
        public void QueueUpgradeAnimation(CardData baseCard, CardData upgradedCard, NetworkEntity entity, bool upgradeAllCopies, System.Action<CardData, CardData, NetworkEntity, bool> onComplete)
        {
            Debug.Log($"[CARD_UPGRADE] CardUpgradeAnimator.QueueUpgradeAnimation called: {baseCard?.CardName} -> {upgradedCard?.CardName}");
            
            if (baseCard == null || upgradedCard == null)
            {
                Debug.LogError("[CARD_UPGRADE] CardUpgradeAnimator: Cannot queue upgrade with null card data");
                onComplete?.Invoke(baseCard, upgradedCard, entity, upgradeAllCopies);
                return;
            }

            // Only animate for local player entities (prevent double animation on host/client)
            if (!entity.IsOwner)
            {
                Debug.Log($"[CARD_UPGRADE] Skipping animation for non-local entity {entity.name} (IsOwner: {entity.IsOwner})");
                onComplete?.Invoke(baseCard, upgradedCard, entity, upgradeAllCopies);
                return;
            }

            // Additional safety check - this should now be prevented by CardUpgradeManager
            foreach (var queuedItem in animationQueue)
            {
                if (queuedItem.baseCard.CardId == baseCard.CardId && queuedItem.entity == entity)
                {
                    Debug.LogWarning($"[CARD_UPGRADE] Duplicate upgrade animation detected for {baseCard.CardName} on {entity.name} - skipping (this should not happen anymore)");
                    onComplete?.Invoke(baseCard, upgradedCard, entity, upgradeAllCopies);
                    return;
                }
            }

            // Find the actual card GameObject in hand
            Card cardToUpgrade = FindCardInHand(baseCard, entity);
            if (cardToUpgrade == null)
            {
                Debug.LogError($"[CARD_UPGRADE] Could not find card {baseCard.CardName} in hand for entity {entity.name}");
                onComplete?.Invoke(baseCard, upgradedCard, entity, upgradeAllCopies);
                return;
            }

            var queuedAnimation = new QueuedUpgradeAnimation
            {
                cardToUpgrade = cardToUpgrade,
                baseCard = baseCard,
                upgradedCard = upgradedCard,
                entity = entity,
                upgradeAllCopies = upgradeAllCopies,
                onComplete = onComplete,
                queueTime = Time.time
            };

            animationQueue.Enqueue(queuedAnimation);
            Debug.Log($"[CARD_UPGRADE] CardUpgradeAnimator: Queued in-hand upgrade for {baseCard.CardName} → {upgradedCard.CardName} (Queue size: {animationQueue.Count})");

            // Start playing if not already playing
            if (!isPlayingAnimation)
            {
                Debug.Log($"[CARD_UPGRADE] CardUpgradeAnimator: Starting in-hand transformation coroutine");
                StartCoroutine(PlayNextUpgradeInQueue());
            }
            else
            {
                Debug.Log($"[CARD_UPGRADE] CardUpgradeAnimator: Animation already playing, added to queue");
            }
        }

        /// <summary>
        /// Find the card GameObject in the hand that matches the card data
        /// </summary>
        private Card FindCardInHand(CardData cardData, NetworkEntity entity)
        {
            Debug.Log($"[CARD_UPGRADE] Looking for card {cardData.CardName} in hand for entity {entity.name}");

            // Try to find hand entity through relationship manager
            RelationshipManager relationshipManager = entity.GetComponent<RelationshipManager>();
            NetworkEntity handEntity = relationshipManager?.HandEntity as NetworkEntity;
            
            if (handEntity == null)
            {
                Debug.LogWarning($"[CARD_UPGRADE] No hand entity found for {entity.name}");
                return null;
            }

            // Get HandManager from hand entity
            HandManager handManager = handEntity.GetComponent<HandManager>();
            if (handManager == null)
            {
                Debug.LogWarning($"[CARD_UPGRADE] No HandManager found on hand entity {handEntity.name}");
                return null;
            }

            // Search through cards in hand
            var cardsInHand = handManager.GetCardsInHand();
            foreach (var cardObj in cardsInHand)
            {
                Card card = cardObj.GetComponent<Card>();
                if (card != null && card.CardData != null && card.CardData.CardId == cardData.CardId)
                {
                    Debug.Log($"[CARD_UPGRADE] Found matching card {card.CardData.CardName} in hand");
                    return card;
                }
            }

            Debug.LogWarning($"[CARD_UPGRADE] Could not find card {cardData.CardName} (ID: {cardData.CardId}) in hand");
            return null;
        }

        /// <summary>
        /// Wait for hand layout animations to complete before starting upgrade
        /// </summary>
        private IEnumerator WaitForHandLayoutToSettle(NetworkEntity entity)
        {
            Debug.Log($"[CARD_UPGRADE] Waiting for hand layout to settle for entity {entity.name}");

            // Find hand entity
            RelationshipManager relationshipManager = entity.GetComponent<RelationshipManager>();
            NetworkEntity handEntity = relationshipManager?.HandEntity as NetworkEntity;
            
            if (handEntity == null)
            {
                Debug.LogWarning($"[CARD_UPGRADE] No hand entity found - using fixed wait time");
                yield return new WaitForSeconds(handLayoutWaitTime);
                yield break;
            }

            // Get HandManager from hand entity to find the hand transform
            HandManager handManager = handEntity.GetComponent<HandManager>();
            if (handManager == null)
            {
                Debug.LogWarning($"[CARD_UPGRADE] No HandManager found on {handEntity.name} - using fixed wait time");
                yield return new WaitForSeconds(handLayoutWaitTime);
                yield break;
            }

            // Get the hand transform where HandAnimator should be located
            Transform handTransform = handManager.GetHandTransform();
            if (handTransform == null)
            {
                Debug.LogWarning($"[CARD_UPGRADE] No hand transform found - using fixed wait time");
                yield return new WaitForSeconds(handLayoutWaitTime);
                yield break;
            }

            // Get HandAnimator from the hand transform (same object as HandLayoutManager)
            HandAnimator handAnimator = handTransform.GetComponent<HandAnimator>();
            if (handAnimator == null)
            {
                Debug.LogWarning($"[CARD_UPGRADE] No HandAnimator found on hand transform {handTransform.name} - using fixed wait time");
                yield return new WaitForSeconds(handLayoutWaitTime);
                yield break;
            }

            Debug.Log($"[CARD_UPGRADE] Found HandAnimator on {handAnimator.name}");

            // Wait for hand animations to complete, with a timeout
            float waitStartTime = Time.time;
            float maxWaitTime = handLayoutWaitTime * 4f; // Maximum wait time to prevent infinite loops

            while (handAnimator.IsAnimating && (Time.time - waitStartTime) < maxWaitTime)
            {
                yield return null;
            }

            if (handAnimator.IsAnimating)
            {
                Debug.LogWarning($"[CARD_UPGRADE] Hand animation timeout after {maxWaitTime}s - proceeding anyway");
            }
            else
            {
                Debug.Log($"[CARD_UPGRADE] Hand layout settled after {Time.time - waitStartTime:F2}s");
            }

            // Additional small delay to ensure everything is stable
            yield return new WaitForSeconds(0.1f);
        }

        /// <summary>
        /// Update hand layout and wait for it to complete so the card gets proper positioning
        /// </summary>
        private IEnumerator UpdateHandLayoutAndWait(NetworkEntity entity)
        {
            Debug.Log($"[CARD_UPGRADE] UpdateHandLayoutAndWait for entity {entity.name}");

            // Find hand entity
            RelationshipManager relationshipManager = entity.GetComponent<RelationshipManager>();
            NetworkEntity handEntity = relationshipManager?.HandEntity as NetworkEntity;
            
            if (handEntity == null)
            {
                Debug.LogWarning($"[CARD_UPGRADE] No hand entity found for layout update");
                yield break;
            }

            // Get HandManager to find hand transform
            HandManager handManager = handEntity.GetComponent<HandManager>();
            if (handManager == null)
            {
                Debug.LogWarning($"[CARD_UPGRADE] No HandManager found for layout update");
                yield break;
            }

            // Get the hand transform where HandLayoutManager should be
            Transform handTransform = handManager.GetHandTransform();
            if (handTransform == null)
            {
                Debug.LogWarning($"[CARD_UPGRADE] No hand transform found for layout update");
                yield break;
            }

            // Get HandLayoutManager and trigger layout update
            HandLayoutManager handLayoutManager = handTransform.GetComponent<HandLayoutManager>();
            if (handLayoutManager != null)
            {
                Debug.Log($"[CARD_UPGRADE] Triggering hand layout update");
                handLayoutManager.UpdateLayout();
                
                // Wait a bit for the layout to apply
                yield return new WaitForSeconds(0.2f);
            }
            else
            {
                Debug.LogWarning($"[CARD_UPGRADE] No HandLayoutManager found on {handTransform.name}");
            }
        }

        /// <summary>
        /// Play the next upgrade animation in the queue
        /// </summary>
        private IEnumerator PlayNextUpgradeInQueue()
        {
            Debug.Log($"[CARD_UPGRADE] PlayNextUpgradeInQueue started with {animationQueue.Count} items in queue");

            while (animationQueue.Count > 0)
            {
                isPlayingAnimation = true;
                var nextUpgrade = animationQueue.Dequeue();

                Debug.Log($"[CARD_UPGRADE] CardUpgradeAnimator: Starting in-hand transformation for {nextUpgrade.baseCard.CardName} → {nextUpgrade.upgradedCard.CardName}");

                yield return StartCoroutine(PlayInHandUpgradeAnimation(nextUpgrade));

                Debug.Log($"[CARD_UPGRADE] CardUpgradeAnimator: Completed in-hand transformation for {nextUpgrade.baseCard.CardName} → {nextUpgrade.upgradedCard.CardName}");

                // Complete callback
                nextUpgrade.onComplete?.Invoke(nextUpgrade.baseCard, nextUpgrade.upgradedCard, nextUpgrade.entity, nextUpgrade.upgradeAllCopies);
            }

            isPlayingAnimation = false;
            Debug.Log("[CARD_UPGRADE] CardUpgradeAnimator: Animation queue completed");
        }

        /// <summary>
        /// Play a single in-hand upgrade animation with burn and replace effects
        /// </summary>
        private IEnumerator PlayInHandUpgradeAnimation(QueuedUpgradeAnimation upgrade)
        {
            Debug.Log($"[CARD_UPGRADE] PlayInHandUpgradeAnimation started for {upgrade.baseCard?.CardName} -> {upgrade.upgradedCard?.CardName}");

            if (upgrade.cardToUpgrade == null)
            {
                Debug.LogError("[CARD_UPGRADE] Card to upgrade is null");
                yield break;
            }

            // Phase 1: Wait for hand layout animation to settle
            Debug.Log($"[CARD_UPGRADE] Phase 1: Waiting for hand layout to settle");
            yield return StartCoroutine(WaitForHandLayoutToSettle(upgrade.entity));

            // Phase 2: Burn away effect
            Debug.Log($"[CARD_UPGRADE] Phase 2: Starting burn away effect");
            PlayUpgradeAudio(burnSound);
            yield return StartCoroutine(AnimateBurnAway(upgrade.cardToUpgrade.gameObject));

            // Phase 3: Transform pause (card is invisible)
            Debug.Log($"[CARD_UPGRADE] Phase 3: Transform pause");
            PlayUpgradeAudio(transformSound);
            
            // Update the card data while it's invisible
            upgrade.cardToUpgrade.UpdateCardData(upgrade.upgradedCard, true);
            
            // Optional screen shake during transformation
            if (enableScreenShake)
            {
                StartCoroutine(PlayScreenShake(shakeIntensity, transformPauseDuration));
            }
            
            yield return new WaitForSeconds(transformPauseDuration);

            // Phase 4: Update hand layout to get proper positioning for the new card
            Debug.Log($"[CARD_UPGRADE] Phase 4: Updating hand layout for proper positioning");
            yield return StartCoroutine(UpdateHandLayoutAndWait(upgrade.entity));

            // Phase 5: Replace effect (new card appears with proper layout)
            Debug.Log($"[CARD_UPGRADE] Phase 5: Starting replace effect");
            PlayUpgradeAudio(replaceSound);
            yield return StartCoroutine(AnimateReplaceEffect(upgrade.cardToUpgrade.gameObject));

            Debug.Log($"[CARD_UPGRADE] PlayInHandUpgradeAnimation completed");
        }

        /// <summary>
        /// Animate the card burning away with visual effects
        /// </summary>
        private IEnumerator AnimateBurnAway(GameObject cardObject)
        {
            Debug.Log($"[CARD_UPGRADE] AnimateBurnAway started for {cardObject.name}");

            CanvasGroup canvasGroup = cardObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = cardObject.AddComponent<CanvasGroup>();
            }

            RectTransform rectTransform = cardObject.GetComponent<RectTransform>();
            
            // Get card image component for color effect
            var cardImages = cardObject.GetComponentsInChildren<UnityEngine.UI.Image>();
            Color[] originalColors = new Color[cardImages.Length];
            for (int i = 0; i < cardImages.Length; i++)
            {
                originalColors[i] = cardImages[i].color;
            }

            float elapsed = 0f;
            Vector3 originalScale = rectTransform.localScale;
            Quaternion originalRotation = rectTransform.localRotation;

            while (elapsed < burnEffectDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = elapsed / burnEffectDuration;
                float curveProgress = burnCurve.Evaluate(progress);

                // Fade out
                canvasGroup.alpha = 1f - curveProgress;

                // DON'T modify scale - let hand layout system handle positioning
                // rectTransform.localScale = Vector3.Lerp(originalScale, originalScale * 0.8f, curveProgress);

                // Color shift to burn color
                for (int i = 0; i < cardImages.Length; i++)
                {
                    cardImages[i].color = Color.Lerp(originalColors[i], burnColor, curveProgress * 0.6f);
                }

                // Add subtle rotation for dramatic effect (much smaller)
                float rotationAmount = Mathf.Sin(progress * Mathf.PI * 2) * 1f; // Reduced from 2f
                rectTransform.localRotation = originalRotation * Quaternion.Euler(0, 0, rotationAmount);

                yield return null;
            }

            // Ensure final values - restore original transform properties
            canvasGroup.alpha = 0f;
            rectTransform.localScale = originalScale; // Keep original scale
            rectTransform.localRotation = originalRotation; // Keep original rotation

            Debug.Log($"[CARD_UPGRADE] AnimateBurnAway completed for {cardObject.name}");
        }

        /// <summary>
        /// Animate the new card appearing with glow effect
        /// </summary>
        private IEnumerator AnimateReplaceEffect(GameObject cardObject)
        {
            Debug.Log($"[CARD_UPGRADE] AnimateReplaceEffect started for {cardObject.name}");

            CanvasGroup canvasGroup = cardObject.GetComponent<CanvasGroup>();
            RectTransform rectTransform = cardObject.GetComponent<RectTransform>();
            
            // Get card image component for glow effect
            var cardImages = cardObject.GetComponentsInChildren<UnityEngine.UI.Image>();
            Color[] finalColors = new Color[cardImages.Length];
            for (int i = 0; i < cardImages.Length; i++)
            {
                finalColors[i] = cardImages[i].color;
            }

            float elapsed = 0f;
            Vector3 targetScale = rectTransform.localScale; // Use the scale that HandLayoutManager set
            Quaternion targetRotation = rectTransform.localRotation; // Preserve current rotation

            // Start with card invisible but at correct size/position
            canvasGroup.alpha = 0f;
            // DON'T modify scale - trust the hand layout system
            // rectTransform.localScale = targetScale * 1.2f;

            while (elapsed < replaceEffectDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = elapsed / replaceEffectDuration;
                float curveProgress = replaceCurve.Evaluate(progress);

                // Fade in with a flash effect
                float flashEffect = 1f + (Mathf.Sin(progress * Mathf.PI * 8) * 0.1f); // Quick flash
                canvasGroup.alpha = curveProgress * flashEffect;

                // DON'T modify scale - let hand layout handle it
                // rectTransform.localScale = Vector3.Lerp(targetScale * 1.2f, targetScale, curveProgress);

                // Glow effect (add brightness then fade to normal)
                float glowIntensity = Mathf.Sin(progress * Mathf.PI) * 0.5f;
                for (int i = 0; i < cardImages.Length; i++)
                {
                    Color glowColor = Color.Lerp(finalColors[i], replaceGlowColor, glowIntensity);
                    cardImages[i].color = glowColor;
                }

                yield return null;
            }

            // Ensure final values - trust the existing layout
            canvasGroup.alpha = 1f;
            rectTransform.localScale = targetScale;      // Keep the layout-set scale
            rectTransform.localRotation = targetRotation; // Keep the layout-set rotation
            for (int i = 0; i < cardImages.Length; i++)
            {
                cardImages[i].color = finalColors[i];
            }

            Debug.Log($"[CARD_UPGRADE] AnimateReplaceEffect completed for {cardObject.name}");
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

            mainCamera.transform.position = originalPos;
        }

        /// <summary>
        /// Play upgrade audio
        /// </summary>
        private void PlayUpgradeAudio(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        // Public utility methods
        public int GetQueueSize()
        {
            return animationQueue.Count;
        }

        public bool IsPlayingAnimation()
        {
            return isPlayingAnimation;
        }

        public void ClearQueue()
        {
            animationQueue.Clear();
            Debug.Log("[CARD_UPGRADE] CardUpgradeAnimator: Animation queue cleared");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnValidate()
        {
            // Ensure positive values
            handLayoutWaitTime = Mathf.Max(0f, handLayoutWaitTime);
            burnEffectDuration = Mathf.Max(0.1f, burnEffectDuration);
            transformPauseDuration = Mathf.Max(0f, transformPauseDuration);
            replaceEffectDuration = Mathf.Max(0.1f, replaceEffectDuration);
            shakeIntensity = Mathf.Max(0f, shakeIntensity);
        }
    }
} 