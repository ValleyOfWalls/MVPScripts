using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Assuming CardData and CardUI scripts exist
// using YourNamespace; // Uncomment if your CardData/CardUI are in a namespace

public class DeckViewController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Transform cardContentArea; // The "Content" child of the ScrollView
    [SerializeField] private GameObject cardPrefab; // The Card Template prefab

    public GameObject cardEntryPrefab;
    public Transform contentPanel;

    private List<GameObject> spawnedCardEntries = new List<GameObject>();

    private void Awake()
    {
        // Ensure the panel starts inactive
        // if (gameObject.activeSelf)
        // {
        //     Debug.Log("DeckViewController.Awake: Setting inactive...");
        //     gameObject.SetActive(false);
        // }

        // Add listener to the close button
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HideDeck);
        }
        else
        {
            Debug.LogError("DeckViewController: Close Button reference is not set!");
        }

        if (cardPrefab == null)
        {
             Debug.LogError("DeckViewController: Card Prefab reference is not set!");
        }
    }

    /// <summary>
    /// Populates and shows the deck viewer panel.
    /// </summary>
    /// <param name="title">Title for the panel (e.g., "Player Deck").</param>
    /// <param name="cards">List of CardData objects to display.</param>
    // TODO: Replace 'CardData' with your actual card data structure/class if different.
    public void ShowDeck(string title, List<CardData> cards)
    {
        if (titleText != null) 
        {
            titleText.text = title;
        }
        else
        {
             Debug.LogError("DeckViewController: Title Text reference is not set!");
        }

        if (cardContentArea != null && cardPrefab != null)
        {
            // Clear existing cards
            foreach (Transform child in cardContentArea)
            {
                Destroy(child.gameObject);
            }

            // Instantiate and populate new cards
            if (cards != null)
            {
                foreach (CardData cardData in cards)
                {
                    if (cardData == null) continue; // Skip null card data entries

                    GameObject cardInstance = Instantiate(cardPrefab, cardContentArea);
                    cardInstance.name = $"CardView_{cardData.cardName}"; // Give it a unique name prefix

                    // --- Corrected Card Population (Direct Element Finding) ---
                    Transform headerPanel = cardInstance.transform.Find("HeaderPanel");
                    Transform descPanel = cardInstance.transform.Find("DescPanel");

                    TextMeshProUGUI nameText = headerPanel?.Find("CardNameText")?.GetComponent<TextMeshProUGUI>();
                    TextMeshProUGUI costText = headerPanel?.Find("CostText")?.GetComponent<TextMeshProUGUI>();
                    TextMeshProUGUI descText = descPanel?.Find("CardDescText")?.GetComponent<TextMeshProUGUI>();

                    if (nameText != null) 
                        nameText.text = cardData.cardName ?? "Error";
                    else 
                        Debug.LogWarning($"Could not find HeaderPanel/CardNameText on Card Prefab instance for {cardData?.cardName}");

                    if (costText != null) 
                        costText.text = cardData.cost.ToString();
                    else 
                        Debug.LogWarning($"Could not find HeaderPanel/CostText on Card Prefab instance for {cardData?.cardName}");

                    if (descText != null) 
                        descText.text = cardData.description ?? "Error";
                    else 
                        Debug.LogWarning($"Could not find DescPanel/CardDescText on Card Prefab instance for {cardData?.cardName}");

                    // Disable drag handler and raycasts for cards in the viewer
                    CardDragHandler dragHandler = cardInstance.GetComponent<CardDragHandler>();
                    if (dragHandler != null)
                    {
                        dragHandler.enabled = false; 
                    }
                    CanvasGroup canvasGroup = cardInstance.GetComponent<CanvasGroup>();
                    if(canvasGroup != null) 
                    {
                        canvasGroup.blocksRaycasts = false; // Prevent blocking scroll view interaction
                    }
                    // --- END Corrected Card Population ---
                }
            }
            else
            {
                Debug.LogWarning("ShowDeck called with a null card list.");
            }
        }
         else
        {
             Debug.LogError("DeckViewController: Card Content Area or Card Prefab reference is not set!");
        }

        // Show the panel
        Debug.Log("DeckViewController.ShowDeck: Setting active...");
        gameObject.SetActive(true);
        Debug.Log("DeckViewController.ShowDeck: SetActive(true) finished.");
    }

    /// <summary>
    /// Hides the deck viewer panel.
    /// </summary>
    public void HideDeck()
    {
        gameObject.SetActive(false);
    }
} 