using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Photon.Pun;

public class DamageNumberManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float displayDuration = 1.5f;
    [SerializeField] private float popupDelayBetweenNumbers = 0.1f;
    [SerializeField] private float initialScale = 0.5f;
    [SerializeField] private float popScale = 1.5f;
    [SerializeField] private float finalScale = 1.0f;
    [SerializeField] private float moveDistance = 100f;
    [SerializeField] private float fadeOutDuration = 0.3f;
    
    [Header("Text Styling")]
    [SerializeField] private int defaultFontSize = 36;
    [SerializeField] private int criticalFontSize = 42;
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private Color damageColor = Color.red;
    [SerializeField] private Color criticalColor = new Color(1f, 0.5f, 0f); // Orange
    [SerializeField] private Color healColor = Color.green;

    private Camera mainCamera;
    private Queue<DamagePopupData> pendingPopups = new Queue<DamagePopupData>();
    private bool isProcessingQueue = false;
    private Transform canvasTransform;

    private struct DamagePopupData
    {
        public int amount;
        public Vector3 position;
        public PopupType type;
        public GameObject targetObject;
    }

    public enum PopupType
    {
        Damage,
        Critical,
        Heal
    }

    private void Awake()
    {
        mainCamera = Camera.main;
        canvasTransform = transform;
    }

    private void Start()
    {
        // Register with GameManager
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.RegisterDamageNumberManager(this);
        }
        else
        {
            Debug.LogError("DamageNumberManager could not find GameManager!");
        }
    }

    public void ShowDamageNumber(int amount, GameObject targetObject, bool isCritical = false)
    {
        if (amount <= 0) return;

        DamagePopupData data = new DamagePopupData
        {
            amount = amount,
            position = GetPopupPosition(targetObject),
            type = isCritical ? PopupType.Critical : PopupType.Damage,
            targetObject = targetObject
        };

        pendingPopups.Enqueue(data);

        if (!isProcessingQueue)
        {
            StartCoroutine(ProcessPopupQueue());
        }
    }

    public void ShowHealNumber(int amount, GameObject targetObject)
    {
        if (amount <= 0) return;

        DamagePopupData data = new DamagePopupData
        {
            amount = amount,
            position = GetPopupPosition(targetObject),
            type = PopupType.Heal,
            targetObject = targetObject
        };

        pendingPopups.Enqueue(data);

        if (!isProcessingQueue)
        {
            StartCoroutine(ProcessPopupQueue());
        }
    }

    private Vector3 GetPopupPosition(GameObject targetObject)
    {
        // Get the screen position of the target
        Vector3 screenPos;
        
        if (targetObject != null)
        {
            RectTransform rectTransform = targetObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // For UI elements, get their position directly
                screenPos = RectTransformUtility.WorldToScreenPoint(mainCamera, rectTransform.position);
                
                // Add some offset based on the rect's size
                screenPos.y += rectTransform.rect.height * 0.5f;
            }
            else
            {
                // For world objects, use WorldToScreenPoint
                screenPos = mainCamera.WorldToScreenPoint(targetObject.transform.position);
            }
            
            // Add random horizontal offset to prevent perfect overlapping
            screenPos.x += Random.Range(-20f, 20f);
        }
        else
        {
            // Fallback to center of screen with some random offset
            screenPos = new Vector3(Screen.width * 0.5f + Random.Range(-50f, 50f), 
                                   Screen.height * 0.5f + Random.Range(-50f, 50f), 
                                   0);
        }
        
        return screenPos;
    }

    private IEnumerator ProcessPopupQueue()
    {
        isProcessingQueue = true;
        
        while (pendingPopups.Count > 0)
        {
            DamagePopupData data = pendingPopups.Dequeue();
            CreatePopup(data);
            
            // Brief delay between popups
            yield return new WaitForSeconds(popupDelayBetweenNumbers);
        }
        
        isProcessingQueue = false;
    }

    private void CreatePopup(DamagePopupData data)
    {
        // Create a GameObject for the popup
        GameObject popupObj = new GameObject("DamageNumber");
        popupObj.transform.SetParent(canvasTransform, false);
        
        // Add RectTransform component
        RectTransform rectTransform = popupObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(200, 50);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        
        // Add CanvasGroup for fading
        CanvasGroup canvasGroup = popupObj.AddComponent<CanvasGroup>();
        
        // Add TextMeshProUGUI component
        TextMeshProUGUI textComponent = popupObj.AddComponent<TextMeshProUGUI>();
        
        // Configure text style based on type
        Color textColor;
        int fontSize;
        string displayText;
        
        switch (data.type)
        {
            case PopupType.Critical:
                textColor = criticalColor;
                fontSize = criticalFontSize;
                displayText = data.amount.ToString() + "!";
                textComponent.fontStyle = FontStyles.Bold;
                break;
            case PopupType.Heal:
                textColor = healColor;
                fontSize = defaultFontSize;
                displayText = "+" + data.amount.ToString();
                break;
            case PopupType.Damage:
            default:
                textColor = damageColor;
                fontSize = defaultFontSize;
                displayText = data.amount.ToString();
                break;
        }
        
        // Apply text settings
        textComponent.text = displayText;
        textComponent.color = textColor;
        textComponent.fontSize = fontSize;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.enableWordWrapping = false;
        
        // Set font asset if provided
        if (fontAsset != null)
        {
            textComponent.font = fontAsset;
        }
        
        // Add outline - using material properties instead of enableOutline
        textComponent.outlineWidth = 0.2f;
        textComponent.outlineColor = Color.black;
        
        // Disable raycast target (don't block input)
        textComponent.raycastTarget = false;
        
        // Convert screen position to canvas position
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasTransform as RectTransform, 
            data.position, 
            mainCamera, 
            out Vector2 localPos))
        {
            rectTransform.localPosition = localPos;
        }
        
        // Animate the popup
        AnimatePopup(popupObj, rectTransform);
    }

    private void AnimatePopup(GameObject popupObj, RectTransform rectTransform)
    {
        // Set initial state
        rectTransform.localScale = Vector3.one * initialScale;
        CanvasGroup canvasGroup = popupObj.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        
        // Scale animation sequence
        Sequence scaleSequence = DOTween.Sequence();
        scaleSequence.Append(rectTransform.DOScale(popScale, 0.2f).SetEase(Ease.OutQuad));
        scaleSequence.Append(rectTransform.DOScale(finalScale, 0.1f).SetEase(Ease.InQuad));
        
        // Move up animation
        rectTransform.DOLocalMoveY(rectTransform.localPosition.y + moveDistance, displayDuration)
            .SetEase(Ease.OutQuad);
        
        // Fade out at the end
        canvasGroup.DOFade(0f, fadeOutDuration)
            .SetDelay(displayDuration - fadeOutDuration)
            .OnComplete(() => Destroy(popupObj));
    }
} 