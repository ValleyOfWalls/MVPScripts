using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

namespace Combat
{
    // Card types similar to Slay the Spire
    public enum CardType
    {
        Attack,
        Skill,
        Power
    }
    
    public class Card : MonoBehaviour
    {
        [Header("Card Data")]
        [SerializeField] private CardData cardData;
        
        [Header("UI Elements")]
        [SerializeField] private Image cardImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Image cardTypeIcon;
        
        // Properties
        public CardData Data => cardData;
        public string CardName => cardData != null ? cardData.cardName : "NO_DATA";
        public string Description => cardData != null ? cardData.description : "NO_DATA";
        public int ManaCost => cardData != null ? cardData.manaCost : 0;
        public CardType Type => cardData != null ? cardData.cardType : default;
        public int BaseValue => cardData != null ? cardData.baseValue : 0;
        
        // Card state
        private bool isPlayable = true;
        private bool isDragging = false;
        private Vector3 originalPosition;
        private Vector3 originalScale;
        private PlayerHand owningHand;
        private ICombatant owner;
        
        private Canvas cardCanvas;
        private CanvasGroup cardCanvasGroup;
        
        private void Awake()
        {
            // Store original transform values
            originalPosition = transform.position;
            originalScale = transform.localScale;
            
            // Get or add required components
            cardCanvas = GetComponent<Canvas>();
            if (cardCanvas == null)
            {
                cardCanvas = gameObject.AddComponent<Canvas>();
                cardCanvas.overrideSorting = true;
                cardCanvas.sortingOrder = 100;
            }
            
            cardCanvasGroup = GetComponent<CanvasGroup>();
            if (cardCanvasGroup == null)
            {
                cardCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            
            Debug.Log($"Card Awake - Name: {CardName}, Image: {(cardImage != null ? "Found" : "Missing")}, NameText: {(nameText != null ? "Found" : "Missing")}");
        }
        
        private void Start()
        {
            UpdateCardUI();
            
            // Ensure card is visible
            if (cardCanvasGroup != null)
            {
                cardCanvasGroup.alpha = 1f;
                cardCanvasGroup.interactable = true;
                cardCanvasGroup.blocksRaycasts = true;
            }
        }

        private void UpdateCardUI()
        {
            // Debug.Log($"Updating UI for card: {CardName}");
            
            // Set up card UI
            if (nameText != null) nameText.text = CardName;
            if (descriptionText != null) descriptionText.text = Description;
            if (costText != null) costText.text = ManaCost.ToString();
            
            // Update card image if available
            if (cardImage != null)
            {
                // Load sprite from path if CardData exists
                if (cardData != null && !string.IsNullOrEmpty(cardData.cardArtworkPath))
                {
                    cardImage.sprite = Resources.Load<Sprite>(cardData.cardArtworkPath);
                    if (cardImage.sprite == null) 
                    {
                        Debug.LogWarning($"Card {CardName}: Could not load artwork from path: {cardData.cardArtworkPath}");
                        CreatePlaceholderCardBackground(); // Fallback placeholder
                    }
                }
                else if (cardImage.sprite == null) // If no CardData or path, or loading failed
                {
                    CreatePlaceholderCardBackground();
                }
                
                // Ensure image is displayed properly
                cardImage.enabled = true;
                cardImage.preserveAspect = true;
                
                // Set full opacity for image
                Color imgColor = cardImage.color;
                imgColor.a = 1f;
                cardImage.color = imgColor;
            }
            else
            {
                Debug.LogError($"Card {CardName} has no Image component assigned to cardImage");
            }
            
            // Set card type icon
            UpdateCardTypeIcon();
        }
        
        private void CreatePlaceholderCardBackground()
        {
            Debug.LogWarning($"No sprite assigned to Card {CardName} - creating placeholder");
            
            // Create a placeholder texture
            Texture2D texture = new Texture2D(200, 300);
            Color[] colors = new Color[200 * 300];
            
            // Fill with color based on card type
            Color fillColor;
            switch (Type)
            {
                case CardType.Attack:
                    fillColor = new Color(0.8f, 0.2f, 0.2f, 1.0f); // Red for attack
                    break;
                case CardType.Skill:
                    fillColor = new Color(0.2f, 0.4f, 0.8f, 1.0f); // Blue for skill
                    break;
                case CardType.Power:
                    fillColor = new Color(0.6f, 0.2f, 0.8f, 1.0f); // Purple for power
                    break;
                default:
                    fillColor = new Color(0.5f, 0.5f, 0.5f, 1.0f); // Gray default
                    break;
            }
            
            // Fill with type color
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = fillColor;
            }
            
            // Add a border
            for (int x = 0; x < 200; x++)
            {
                for (int y = 0; y < 300; y++)
                {
                    if (x < 5 || x > 194 || y < 5 || y > 294)
                    {
                        colors[y * 200 + x] = new Color(0.2f, 0.2f, 0.2f, 1.0f);
                    }
                }
            }
            
            texture.SetPixels(colors);
            texture.Apply();
            
            // Create sprite from texture
            Sprite placeholder = Sprite.Create(texture, new Rect(0, 0, 200, 300), new Vector2(0.5f, 0.5f), 100f);
            cardImage.sprite = placeholder;
            
            Debug.Log("Created placeholder sprite for card background");
        }
        
        public void Initialize(CardData data, PlayerHand hand, ICombatant cardOwner)
        {
            if (data == null)
            {
                Debug.LogError("Attempted to initialize Card with null CardData!");
                return;
            }
            
            cardData = data;
            owningHand = hand;
            owner = cardOwner;
            
            Debug.Log($"Initializing card for PlayerHand with data: {cardData.cardName}");
            
            // Ensure the card is visible and potentially interactable
            if (cardCanvasGroup != null)
            {
                cardCanvasGroup.alpha = 1f;
                cardCanvasGroup.interactable = true; // Player hands are interactable
                cardCanvasGroup.blocksRaycasts = true;
            }
            
            UpdateCardUI();
        }

        public void Initialize(CardData data, PetHand hand, ICombatant cardOwner)
        {
            if (data == null)
            {
                Debug.LogError("Attempted to initialize Card with null CardData!");
                return;
            }
            
            cardData = data;
            // Note: owningHand field is PlayerHand type, cannot directly assign PetHand
            // If common functionality is needed, consider an interface or base class for hands
            // For now, we store the owner but not the specific PetHand reference in owningHand
            owningHand = null; 
            owner = cardOwner;
            
            Debug.Log($"Initializing card for PetHand with data: {cardData.cardName}");
            
            // Ensure the card is visible but not interactable
            if (cardCanvasGroup != null)
            {
                cardCanvasGroup.alpha = 1f;
                cardCanvasGroup.interactable = false; // Pet hands are not interactable
                cardCanvasGroup.blocksRaycasts = false;
            }
            
            UpdateCardUI();
        }
        
        private void UpdateCardTypeIcon()
        {            
            if (cardTypeIcon != null)
            {
                // Load icon from path if CardData exists
                if (cardData != null && !string.IsNullOrEmpty(cardData.typeIconPath))
                {
                    cardTypeIcon.sprite = Resources.Load<Sprite>(cardData.typeIconPath);
                    if (cardTypeIcon.sprite != null)
                    {
                        cardTypeIcon.color = Color.white; // Reset color if using sprite
                    }
                    else
                    {
                         Debug.LogWarning($"Card {CardName}: Could not load type icon from path: {cardData.typeIconPath}");
                         SetTypeIconColorFallback(); // Fallback to color
                    }
                }
                else
                {
                    SetTypeIconColorFallback(); // Fallback to color if no path
                }
                
                // Ensure icon is enabled
                cardTypeIcon.enabled = true;
            }
        }

        // Helper method for fallback icon color
        private void SetTypeIconColorFallback()
        {
             if (cardTypeIcon == null) return;
             cardTypeIcon.sprite = null; // Ensure no sprite is assigned
             switch (Type)
             {
                 case CardType.Attack:
                     cardTypeIcon.color = Color.red;
                     break;
                 case CardType.Skill:
                     cardTypeIcon.color = Color.blue;
                     break;
                 case CardType.Power:
                     cardTypeIcon.color = Color.magenta;
                     break;
                 default:
                     cardTypeIcon.color = Color.gray;
                     break;
             }
        }
        
        public void SetPlayable(bool playable)
        {
            isPlayable = playable;
            
            // Visual update
            if (cardImage != null)
            {
                Color color = cardImage.color;
                color.a = playable ? 1f : 0.5f;
                cardImage.color = color;
            }
            
            // Update canvas group interactable state
            if (cardCanvasGroup != null)
            {
                cardCanvasGroup.interactable = playable;
            }
        }
        
        // Called when the card is clicked
        public void OnCardClick()
        {
            // If card is not playable, don't do anything
            if (!isPlayable) return;
            
            Debug.Log($"Card clicked: {CardName}");
            
            // Highlight on click
            transform.DOScale(originalScale * 1.1f, 0.2f);
        }
        
        // Called when the card starts being dragged
        public void OnBeginDrag()
        {
            if (!isPlayable) return;
            
            isDragging = true;
            
            Debug.Log($"Begin dragging card: {CardName}");
            
            // Store original position
            originalPosition = transform.position;
            
            // Visual feedback
            transform.DOScale(originalScale * 1.2f, 0.2f);
            
            // Bring card to front during drag
            if (cardCanvas != null)
                cardCanvas.sortingOrder += 10;
        }
        
        // Called while the card is being dragged
        public void OnDrag(Vector3 position)
        {
            if (!isDragging) return;
            
            // Follow the cursor/touch position
            transform.position = position;
        }
        
        // Called when the card is released after dragging
        public void OnEndDrag(bool validTarget, ICombatant target = null)
        {
            if (!isDragging) return;
            
            isDragging = false;
            
            Debug.Log($"End dragging card: {CardName}, Valid target: {validTarget}");
            
            // Reset sorting order
            if (cardCanvas != null)
                cardCanvas.sortingOrder -= 10;
            
            if (validTarget)
            {
                // Card was played successfully
                PlayCard(target);
            }
            else
            {
                // Card was not played, return to hand
                ReturnToHand();
            }
        }
        
        // Play the card
        public void PlayCard(ICombatant target = null)
        {
            Debug.Log($"Playing card: {CardName}, Target: {(target != null ? "Found" : "None")}");
            
            // Apply card effects
            if (cardData != null && cardData.cardEffects != null)
            {
                ApplyCardEffects(target);
            }
            
            // Notify the owning hand that this card was played
            if (owningHand != null)
            {
                owningHand.OnCardPlayed(this);
            }
            
            // Animate card being played
            transform.DOMove(new Vector3(Screen.width / 2, Screen.height / 2, 0), 0.3f)
                .SetEase(Ease.OutQuint);
            
            transform.DOScale(originalScale * 1.5f, 0.3f)
                .SetEase(Ease.OutQuint);
            
            // Fade out and destroy
            if (cardCanvasGroup != null)
            {
                cardCanvasGroup.DOFade(0, 0.3f)
                    .SetDelay(0.3f)
                    .OnComplete(() => {
                        Destroy(gameObject);
                    });
            }
            else
            {
                // If no canvas group, destroy after delay
                Destroy(gameObject, 0.6f);
            }
        }

        private void ApplyCardEffects(ICombatant target)
        {
            if (owner == null) return;
            
            // Process each effect
            foreach (CardEffect effect in cardData.cardEffects)
            {
                switch (effect.targetType)
                {
                    case TargetType.Self:
                        ApplyEffect(effect, owner);
                        break;
                    case TargetType.SingleEnemy:
                        if (target != null)
                            ApplyEffect(effect, target);
                        break;
                    case TargetType.AllEnemies:
                        foreach (ICombatant enemy in CombatManager.Instance.GetEnemies())
                            ApplyEffect(effect, enemy);
                        break;
                    case TargetType.SingleAlly:
                        if (target != null && !target.IsEnemy())
                            ApplyEffect(effect, target);
                        break;
                    case TargetType.AllAllies:
                        foreach (ICombatant ally in CombatManager.Instance.GetAllies())
                            ApplyEffect(effect, ally);
                        break;
                }
            }
        }
        
        private void ApplyEffect(CardEffect effect, ICombatant target)
        {
            if (target == null) return;
            
            Debug.Log($"Applying effect: {effect.effectType} with value {effect.effectValue} to target");
            
            switch (effect.effectType)
            {
                case EffectType.Damage:
                    target.TakeDamage(effect.effectValue);
                    break;
                case EffectType.Block:
                    target.AddBlock(effect.effectValue);
                    break;
                case EffectType.Heal:
                    target.Heal(effect.effectValue);
                    break;
                case EffectType.DrawCard:
                    target.DrawCards(effect.effectValue);
                    break;
                case EffectType.ApplyBuff:
                    target.ApplyBuff(effect.effectValue);
                    break;
                case EffectType.ApplyDebuff:
                    target.ApplyDebuff(effect.effectValue);
                    break;
            }
        }
        
        public void ReturnToHand()
        {
            Debug.Log($"Returning card to hand: {CardName}");
            
            // Return to original position and scale
            transform.DOMove(originalPosition, 0.3f)
                .SetEase(Ease.OutQuint);
            
            transform.DOScale(originalScale, 0.3f)
                .SetEase(Ease.OutQuint);
        }
        
        public void OnPointerEnter()
        {
            if (isDragging) return;
            
            // Zoom effect when hovering
            transform.DOScale(originalScale * 1.2f, 0.2f);
            
            // Bring to front
            if (cardCanvas != null)
                cardCanvas.sortingOrder += 5;
        }
        
        public void OnPointerExit()
        {
            if (isDragging) return;
            
            // Return to normal size when not hovering
            transform.DOScale(originalScale, 0.2f);
            
            // Return to normal sorting order
            if (cardCanvas != null)
                cardCanvas.sortingOrder -= 5;
        }
    }
}