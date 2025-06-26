using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace CardUpgrade
{
    /// <summary>
    /// Manages the UI elements of the card upgrade display
    /// Attach to: CardUpgradeDisplay.prefab root GameObject
    /// </summary>
    public class CardUpgradeUIController : MonoBehaviour
    {
        [Header("Card Display Elements")]
        [SerializeField] private Transform cardDisplayArea;
        [SerializeField] private Image cardFrame;
        [SerializeField] private Image cardArtwork;
        [SerializeField] private TextMeshProUGUI cardName;
        [SerializeField] private TextMeshProUGUI cardCost;
        [SerializeField] private TextMeshProUGUI cardDescription;
        
        [Header("Stats Display")]
        [SerializeField] private Transform statsContainer;
        [SerializeField] private TextMeshProUGUI damageText;
        [SerializeField] private TextMeshProUGUI shieldText;
        [SerializeField] private TextMeshProUGUI energyText;
        [SerializeField] private TextMeshProUGUI otherStatsText;
        
        [Header("Effects Display")]
        [SerializeField] private Transform effectsContainer;
        [SerializeField] private TextMeshProUGUI effectsListText;
        
        [Header("Transition Elements")]
        [SerializeField] private Image transitionOverlay;
        [SerializeField] private Material transitionMaterial;
        
        [Header("Animation Settings")]
        [SerializeField] private AnimationCurve wipeProgressCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        
        private void Awake()
        {
            // Get components
            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = GetComponent<RectTransform>();
            
            // Ensure we have a canvas group for fading
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            
            // Setup transition overlay
            SetupTransitionOverlay();
            
            // Initialize display
            ResetDisplay();
        }
        
        /// <summary>
        /// Setup the transition overlay for wipe effects
        /// </summary>
        private void SetupTransitionOverlay()
        {
            if (transitionOverlay != null)
            {
                // Ensure overlay covers the entire display
                RectTransform overlayRect = transitionOverlay.GetComponent<RectTransform>();
                if (overlayRect != null)
                {
                    overlayRect.anchorMin = Vector2.zero;
                    overlayRect.anchorMax = Vector2.one;
                    overlayRect.offsetMin = Vector2.zero;
                    overlayRect.offsetMax = Vector2.zero;
                }
                
                // Set initial state (invisible)
                transitionOverlay.color = new Color(1f, 1f, 1f, 0f);
                
                // Apply transition material if available
                if (transitionMaterial != null)
                {
                    transitionOverlay.material = transitionMaterial;
                }
            }
        }
        
        /// <summary>
        /// Display a card's data in the UI
        /// </summary>
        public void DisplayCard(CardData cardData)
        {
            if (cardData == null)
            {
                Debug.LogError("CardUpgradeUIController: Cannot display null card data");
                return;
            }
            
            // Set card artwork
            SetCardArtwork(cardData.CardArtwork);
            
            // Update card text
            UpdateCardText(cardData.CardName, cardData.Description, cardData.EnergyCost);
            
            // Update card effects
            UpdateCardEffects(cardData.Effects);
            
            // Update stats (if this card has damage/shield/etc.)
            UpdateCardStats(cardData);
            
            Debug.Log($"CardUpgradeUIController: Displayed card {cardData.CardName}");
        }
        
        /// <summary>
        /// Set the card artwork
        /// </summary>
        public void SetCardArtwork(Sprite artwork)
        {
            if (cardArtwork != null)
            {
                cardArtwork.sprite = artwork;
                cardArtwork.gameObject.SetActive(artwork != null);
            }
        }
        
        /// <summary>
        /// Update card text elements
        /// </summary>
        public void UpdateCardText(string name, string description, int cost)
        {
            if (cardName != null)
            {
                cardName.text = name;
            }
            
            if (cardDescription != null)
            {
                cardDescription.text = description;
            }
            
            if (cardCost != null)
            {
                cardCost.text = cost.ToString();
            }
        }
        
        /// <summary>
        /// Update card effects display
        /// </summary>
        public void UpdateCardEffects(List<CardEffect> effects)
        {
            if (effectsListText == null) return;
            
            if (effects == null || effects.Count == 0)
            {
                effectsListText.text = "No special effects";
                return;
            }
            
            System.Text.StringBuilder effectsText = new System.Text.StringBuilder();
            
            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                string effectDescription = GetEffectDescription(effect);
                
                effectsText.Append(effectDescription);
                
                if (i < effects.Count - 1)
                {
                    effectsText.Append("\n");
                }
            }
            
            effectsListText.text = effectsText.ToString();
        }
        
        /// <summary>
        /// Update card stats display
        /// </summary>
        private void UpdateCardStats(CardData cardData)
        {
            // Find damage effects
            int totalDamage = 0;
            int totalShield = 0;
            
            if (cardData.Effects != null)
            {
                foreach (var effect in cardData.Effects)
                {
                    switch (effect.effectType)
                    {
                        case CardEffectType.Damage:
                            totalDamage += effect.amount;
                            break;
                        case CardEffectType.ApplyShield:
                            totalShield += effect.amount;
                            break;
                    }
                }
            }
            
            // Update damage display
            if (damageText != null)
            {
                if (totalDamage > 0)
                {
                    damageText.text = $"Damage: {totalDamage}";
                    damageText.gameObject.SetActive(true);
                }
                else
                {
                    damageText.gameObject.SetActive(false);
                }
            }
            
            // Update shield display
            if (shieldText != null)
            {
                if (totalShield > 0)
                {
                    shieldText.text = $"Shield: {totalShield}";
                    shieldText.gameObject.SetActive(true);
                }
                else
                {
                    shieldText.gameObject.SetActive(false);
                }
            }
            
            // Update energy display
            if (energyText != null)
            {
                energyText.text = $"Energy: {cardData.EnergyCost}";
            }
        }
        
        /// <summary>
        /// Get a human-readable description of an effect
        /// </summary>
        private string GetEffectDescription(CardEffect effect)
        {
            switch (effect.effectType)
            {
                case CardEffectType.Damage:
                    return $"Deal {effect.amount} damage";
                case CardEffectType.Heal:
                    return $"Heal {effect.amount} health";
                case CardEffectType.DrawCard:
                    return $"Draw {effect.amount} card(s)";
                case CardEffectType.RestoreEnergy:
                    return $"Restore {effect.amount} energy";
                case CardEffectType.ApplyShield:
                    return $"Gain {effect.amount} shield";
                case CardEffectType.ApplyWeak:
                    return $"Apply Weak for {effect.duration} turn(s)";
                case CardEffectType.ApplyBreak:
                    return $"Apply Break for {effect.duration} turn(s)";
                case CardEffectType.ApplyBurn:
                    return $"Apply {effect.amount} Burn";
                case CardEffectType.ApplySalve:
                    return $"Apply {effect.amount} Salve";
                case CardEffectType.ApplyThorns:
                    return $"Apply {effect.amount} Thorns";
                case CardEffectType.ApplyStun:
                    return $"Stun for {effect.duration} turn(s)";
                case CardEffectType.ApplyStrength:
                    return $"Gain {effect.amount} Strength";
                case CardEffectType.ApplyCurse:
                    return $"Apply {effect.amount} Curse";
                case CardEffectType.RaiseCriticalChance:
                    return $"Increase critical chance by {effect.amount}%";
                case CardEffectType.DiscardRandomCards:
                    return $"Discard {effect.amount} random card(s)";
                default:
                    return effect.effectType.ToString();
            }
        }
        
        /// <summary>
        /// Play the wipe transition effect
        /// </summary>
        public IEnumerator PlayWipeTransition(float duration)
        {
            if (transitionOverlay == null)
            {
                Debug.LogWarning("CardUpgradeUIController: No transition overlay assigned, skipping wipe effect");
                yield return new WaitForSecondsRealtime(duration);
                yield break;
            }
            
            float elapsed = 0f;
            Color overlayColor = Color.white;
            
            // Phase 1: Wipe in (cover the card)
            float wipeInDuration = duration * 0.4f;
            while (elapsed < wipeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = elapsed / wipeInDuration;
                float curveProgress = wipeProgressCurve.Evaluate(progress);
                
                // Update transition overlay alpha and potentially material properties
                overlayColor.a = curveProgress;
                transitionOverlay.color = overlayColor;
                
                // If using a shader material, update the wipe progress
                if (transitionMaterial != null)
                {
                    transitionMaterial.SetFloat("_WipeProgress", curveProgress);
                }
                
                yield return null;
            }
            
            // Ensure fully covered
            overlayColor.a = 1f;
            transitionOverlay.color = overlayColor;
            if (transitionMaterial != null)
            {
                transitionMaterial.SetFloat("_WipeProgress", 1f);
            }
            
            // Phase 2: Hold (brief pause for card data change)
            float holdDuration = duration * 0.2f;
            yield return new WaitForSecondsRealtime(holdDuration);
            
            // Phase 3: Wipe out (reveal the new card)
            float wipeOutDuration = duration * 0.4f;
            elapsed = 0f;
            
            while (elapsed < wipeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = elapsed / wipeOutDuration;
                float curveProgress = wipeProgressCurve.Evaluate(1f - progress); // Reverse
                
                // Update transition overlay alpha
                overlayColor.a = curveProgress;
                transitionOverlay.color = overlayColor;
                
                // If using a shader material, update the wipe progress
                if (transitionMaterial != null)
                {
                    transitionMaterial.SetFloat("_WipeProgress", curveProgress);
                }
                
                yield return null;
            }
            
            // Ensure fully revealed
            overlayColor.a = 0f;
            transitionOverlay.color = overlayColor;
            if (transitionMaterial != null)
            {
                transitionMaterial.SetFloat("_WipeProgress", 0f);
            }
        }
        
        /// <summary>
        /// Reset the display to its initial state
        /// </summary>
        public void ResetDisplay()
        {
            // Clear all text elements
            if (cardName != null) cardName.text = "";
            if (cardDescription != null) cardDescription.text = "";
            if (cardCost != null) cardCost.text = "";
            if (effectsListText != null) effectsListText.text = "";
            if (damageText != null) damageText.text = "";
            if (shieldText != null) shieldText.text = "";
            if (energyText != null) energyText.text = "";
            if (otherStatsText != null) otherStatsText.text = "";
            
            // Clear artwork
            if (cardArtwork != null)
            {
                cardArtwork.sprite = null;
            }
            
            // Reset transition overlay
            if (transitionOverlay != null)
            {
                transitionOverlay.color = new Color(1f, 1f, 1f, 0f);
                if (transitionMaterial != null)
                {
                    transitionMaterial.SetFloat("_WipeProgress", 0f);
                }
            }
            
            // Reset scale and alpha
            transform.localScale = Vector3.zero;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }
        
        /// <summary>
        /// Validate that all required UI elements are assigned
        /// </summary>
        private void ValidateUIElements()
        {
            if (cardDisplayArea == null) Debug.LogWarning("CardUpgradeUIController: cardDisplayArea not assigned!");
            if (transitionOverlay == null) Debug.LogWarning("CardUpgradeUIController: transitionOverlay not assigned!");
        }
        
        #if UNITY_EDITOR
        private void OnValidate()
        {
            // Only validate the most critical elements to avoid spam during prefab generation
            ValidateUIElements();
        }
        #endif
    }
} 