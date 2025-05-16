using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the UI elements for the draft phase, including draft packs, card shop, and artifact shop displays.
/// Attach to: The DraftCanvas GameObject that contains all draft phase UI elements.
/// </summary>
public class DraftCanvasManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform draftPacksContainer;
    [SerializeField] private Transform cardShopContainer;
    [SerializeField] private Transform artifactShopContainer;
    [SerializeField] private Transform playerDeckContainer;
    [SerializeField] private TextMeshProUGUI playerCurrencyText;
    [SerializeField] private Button returnToDeckButton;
    [SerializeField] private Button proceedToCombatButton;

    [Header("Player References")]
    private NetworkEntity localPlayer;

    private void Awake()
    {
        // Ensure canvas is disabled at start
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Sets up the draft canvas UI for the local player
    /// </summary>
    public void SetupDraftUI()
    {
        // Find the local player
        NetworkEntity[] players = Object.FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        foreach (NetworkEntity player in players)
        {
            if (player.IsOwner)
            {
                localPlayer = player;
                break;
            }
        }

        if (localPlayer == null)
        {
            Debug.LogError("DraftCanvasManager: Could not find local player!");
            return;
        }

        // Register events for currency updates
        localPlayer.OnCurrencyChanged += UpdateCurrencyDisplay;
        
        // Set initial currency display
        UpdateCurrencyDisplay(localPlayer.Currency.Value);

        // Setup button listeners
        returnToDeckButton.onClick.AddListener(ShowPlayerDeck);
        
        // Proceed button should be disabled until all players complete their draft
        proceedToCombatButton.interactable = false;
    }

    /// <summary>
    /// Updates the currency display UI
    /// </summary>
    private void UpdateCurrencyDisplay(int currencyAmount)
    {
        playerCurrencyText.text = $"Currency: {currencyAmount}";
    }

    /// <summary>
    /// Switches view to player's deck
    /// </summary>
    private void ShowPlayerDeck()
    {
        // Implementation to show the player's deck UI
        cardShopContainer.gameObject.SetActive(false);
        artifactShopContainer.gameObject.SetActive(false);
        draftPacksContainer.gameObject.SetActive(false);
        playerDeckContainer.gameObject.SetActive(true);
    }

    /// <summary>
    /// Called by DraftManager when all players are ready to proceed to combat
    /// </summary>
    public void EnableProceedButton()
    {
        proceedToCombatButton.interactable = true;
    }

    /// <summary>
    /// Cleans up any event subscriptions when the object is destroyed
    /// </summary>
    private void OnDestroy()
    {
        if (localPlayer != null)
        {
            localPlayer.OnCurrencyChanged -= UpdateCurrencyDisplay;
        }
    }
} 