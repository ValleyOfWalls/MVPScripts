using UnityEngine;
using TMPro;

public class NotificationMessage : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private float displayDuration = 2f;
    [SerializeField] private Animator animator;

    public void Initialize(string message)
    {
        if (messageText != null)
            messageText.text = message;

        // Start fade out coroutine
        Destroy(gameObject, displayDuration);
    }
} 