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
        [SerializeField] private string cardName;
        [SerializeField] private string description;
        [SerializeField] private int manaCost;
        [SerializeField] private CardType cardType;
        [SerializeField] private int baseValue; // For attacks: damage, for skills: block, etc.
        
        [Header("UI Elements")]
        [SerializeField] private Image cardImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Image cardTypeIcon;
        
        // Properties
        public string CardName => cardName;
        public string Description => description;
        public int ManaCost => manaCost;
        public CardType Type => cardType;
        public int BaseValue => baseValue;
        
        // Card state
        private bool isPlayable = true;
        private bool isDragging = false;
        private Vector3 originalPosition;
        private Vector3 originalScale;
        private PlayerHand owningHand;
        
        private void Awake()
        {
            // Store original transform values
            originalPosition = transform.position;
            originalScale = transform.localScale;
        }
        
        private void Start()
        {
            // Set up card UI
            if (nameText != null) nameText.text = cardName;
            if (descriptionText != null) descriptionText.text = description;
            if (costText != null) costText.text = manaCost.ToString();
            
            // Set card type icon (needs to be implemented)
            UpdateCardTypeIcon();
        }
        
        public void Initialize(string name, string desc, int cost, CardType type, int value, PlayerHand hand)
        {
            cardName = name;
            description = desc;
            manaCost = cost;
            cardType = type;
            baseValue = value;
            owningHand = hand;
            
            // Update UI
            if (nameText != null) nameText.text = cardName;
            if (descriptionText != null) descriptionText.text = description;
            if (costText != null) costText.text = manaCost.ToString();
            
            UpdateCardTypeIcon();
        }
        
        private void UpdateCardTypeIcon()
        {
            if (cardTypeIcon != null)
            {
                // In a real implementation, you would set the icon based on cardType
                // cardTypeIcon.sprite = GetSpriteForCardType(cardType);
                
                switch (cardType)
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
                }
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
        }
        
        // Called when the card is clicked
        public void OnCardClick()
        {
            // If card is not playable, don't do anything
            if (!isPlayable) return;
            
            // Highlight on click
            transform.DOScale(originalScale * 1.1f, 0.2f);
        }
        
        // Called when the card starts being dragged
        public void OnBeginDrag()
        {
            if (!isPlayable) return;
            
            isDragging = true;
            
            // Store original position
            originalPosition = transform.position;
            
            // Visual feedback
            transform.DOScale(originalScale * 1.2f, 0.2f);
            
            // Bring card to front during drag
            GetComponent<Canvas>().sortingOrder += 10;
        }
        
        // Called while the card is being dragged
        public void OnDrag(Vector3 position)
        {
            if (!isDragging) return;
            
            // Follow the cursor/touch position
            transform.position = position;
        }
        
        // Called when the card is released after dragging
        public void OnEndDrag(bool validTarget)
        {
            if (!isDragging) return;
            
            isDragging = false;
            
            // Reset sorting order
            GetComponent<Canvas>().sortingOrder -= 10;
            
            if (validTarget)
            {
                // Card was played successfully
                PlayCard();
            }
            else
            {
                // Card was not played, return to hand
                ReturnToHand();
            }
        }
        
        // Play the card
        public void PlayCard()
        {
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
            GetComponent<CanvasGroup>().DOFade(0, 0.3f)
                .SetDelay(0.3f)
                .OnComplete(() => {
                    Destroy(gameObject);
                });
        }
        
        // Return the card to its position in the hand
        public void ReturnToHand()
        {
            // Animate returning to original position
            transform.DOMove(originalPosition, 0.3f)
                .SetEase(Ease.OutBack);
            
            transform.DOScale(originalScale, 0.3f)
                .SetEase(Ease.OutBack);
        }
        
        // Card hover effects
        public void OnPointerEnter()
        {
            if (isDragging) return;
            
            // Enlarge the card slightly
            transform.DOScale(originalScale * 1.1f, 0.2f);
            
            // Lift it up a bit
            transform.DOLocalMoveY(transform.localPosition.y + 30f, 0.2f);
        }
        
        public void OnPointerExit()
        {
            if (isDragging) return;
            
            // Return to original scale and position
            transform.DOScale(originalScale, 0.2f);
            transform.DOLocalMoveY(transform.localPosition.y - 30f, 0.2f);
        }
    }
}