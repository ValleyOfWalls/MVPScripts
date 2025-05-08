using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CardPlayedEffect : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI cardNameText;
    [SerializeField] private TextMeshProUGUI effectText;
    [SerializeField] private float displayDuration = 2f;
    [SerializeField] private Animator animator;

    public void Initialize(CardData cardData, string casterName, string targetName)
    {
        if (cardNameText != null)
            cardNameText.text = cardData.CardName;

        if (effectText != null)
            effectText.text = $"{casterName} played {cardData.CardName} on {targetName}";

        // Start fade out coroutine
        Destroy(gameObject, displayDuration);
    }
} 