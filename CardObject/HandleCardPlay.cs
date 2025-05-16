using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems; // Required for IPointerClickHandler
using UnityEngine.UI; // Required for Button

/// <summary>
/// Handles the logic for playing cards during combat
/// Attach to: Card prefabs only.
/// </summary>
public class HandleCardPlay : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Button cardButton;
    [SerializeField] private SourceAndTargetIdentifier sourceAndTargetIdentifier;
    [SerializeField] private Card card;

    private CardData cardData;

    private void Awake()
    {
        // Get the button component
        if (cardButton == null) 
        {
            cardButton = GetComponent<Button>();
        }

        if (sourceAndTargetIdentifier == null)
        {
            sourceAndTargetIdentifier = GetComponent<SourceAndTargetIdentifier>();
        }

        if (card == null)
        {
            card = GetComponent<Card>();
        }

        ValidateComponents();
    }

    private void ValidateComponents()
    {
        if (cardButton == null)
            Debug.LogError($"HandleCardPlay on {gameObject.name}: Missing Button component");
        
        if (sourceAndTargetIdentifier == null)
            Debug.LogError($"HandleCardPlay on {gameObject.name}: Missing SourceAndTargetIdentifier component");
        
        if (card == null)
            Debug.LogError($"HandleCardPlay on {gameObject.name}: Missing Card component");
    }

    public void Initialize(CardData data)
    {
        ValidateComponents();
        cardData = data;
        if (cardButton != null)
        {
            cardButton.onClick.AddListener(OnCardClicked);
        }
    }

    /// <summary>
    /// Called when the card is clicked
    /// </summary>
    public void OnCardClicked()
    {
        Debug.Log($"HandleCardPlay: OnCardClicked for card {gameObject.name}");
        
        // Validate basic conditions
        if (!IsOwner)
        {
            Debug.LogWarning($"HandleCardPlay: Cannot handle click, not network owner of card {gameObject.name}");
            return;
        }

        if (card == null || card.CurrentContainer != CardLocation.Hand)
        {
            Debug.LogWarning($"HandleCardPlay: Cannot handle click, card is not in hand. Card: {gameObject.name}, Location: {(card != null ? card.CurrentContainer.ToString() : "Card component missing")}");
            return;
        }

        // Card is valid to be clicked - Update source and target references
        if (sourceAndTargetIdentifier != null)
        {
            Debug.Log($"HandleCardPlay: Triggering UpdateSourceAndTarget for card {gameObject.name}");
            sourceAndTargetIdentifier.UpdateSourceAndTarget();
        }
        else
        {
            Debug.LogError($"HandleCardPlay: Missing SourceAndTargetIdentifier component for card {gameObject.name}");
        }
    }
} 