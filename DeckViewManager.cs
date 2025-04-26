using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;

public class DeckViewManager
{
    private GameManager gameManager;
    private DeckViewController deckViewController;
    
    // Buttons
    private Button viewPlayerDeckButton;
    private Button viewPetDeckButton;
    private Button viewOppPetDeckButton;
    
    // State for Deck View Toggle
    private enum DeckViewType { None, Player, Pet, OpponentPet }
    private DeckViewType currentDeckViewType = DeckViewType.None;
    
    public DeckViewManager(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public void InitializeReferences(GameObject combatInstance)
    {
        if (combatInstance == null) return;
        
        // Find the buttons
        Transform playerArea = combatInstance.transform.Find("PlayerArea");
        if (playerArea == null)
        {
            Debug.LogError("Could not find PlayerArea in CombatCanvas!");
            return;
        }
        
        Transform bottomBar = playerArea.Find("BottomBar");
        viewPlayerDeckButton = bottomBar?.Find("ViewPlayerDeckButton")?.GetComponent<Button>();
        viewPetDeckButton = bottomBar?.Find("ViewPetDeckButton")?.GetComponent<Button>();
        viewOppPetDeckButton = bottomBar?.Find("ViewOppPetDeckButton")?.GetComponent<Button>();
        
        // Find DeckViewController
        GameObject deckViewerPanelInstance = combatInstance.transform.Find("DeckViewerPanel")?.gameObject;
        if (deckViewerPanelInstance == null && gameManager.GetDeckViewerPanelPrefab() != null)
        {
            // Instantiate it under the combat canvas if not found
            deckViewerPanelInstance = Object.Instantiate(gameManager.GetDeckViewerPanelPrefab(), combatInstance.transform);
            deckViewerPanelInstance.name = "DeckViewerPanel"; // Ensure consistent name
        }

        if (deckViewerPanelInstance != null)
        {
            deckViewController = deckViewerPanelInstance.GetComponent<DeckViewController>();
            if (deckViewController == null)
            {
                Debug.LogError("DeckViewController component not found on the DeckViewerPanel instance!");
            }
        }
        else
        {
            Debug.LogError("DeckViewerPanel instance could not be found or instantiated!");
        }
        
        // Assign button listeners
        viewPlayerDeckButton?.onClick.RemoveAllListeners();
        viewPetDeckButton?.onClick.RemoveAllListeners();
        viewOppPetDeckButton?.onClick.RemoveAllListeners();
        
        viewPlayerDeckButton?.onClick.AddListener(ShowPlayerDeck);
        viewPetDeckButton?.onClick.AddListener(ShowPetDeck);
        viewOppPetDeckButton?.onClick.AddListener(ShowOpponentPetDeck);
    }
    
    private void ShowPlayerDeck()
    {
        if (deckViewController == null) return;

        // Toggle Logic
        if (deckViewController.gameObject.activeSelf && currentDeckViewType == DeckViewType.Player)
        {
            deckViewController.HideDeck();
            currentDeckViewType = DeckViewType.None;
            return;
        }

        // Show Logic (or switch view)
        CardManager cardManager = gameManager.GetCardManager();
        if (cardManager == null) return;

        List<CardData> fullPlayerDeck = new List<CardData>(cardManager.GetDeck());
        fullPlayerDeck.AddRange(cardManager.GetDiscardPile());
        string title = $"{PhotonNetwork.LocalPlayer.NickName}'s Deck ({fullPlayerDeck.Count})";
        deckViewController.ShowDeck(title, fullPlayerDeck);
        currentDeckViewType = DeckViewType.Player;
    }

    private void ShowPetDeck()
    {
        if (deckViewController == null) return;

        // Toggle Logic
        if (deckViewController.gameObject.activeSelf && currentDeckViewType == DeckViewType.Pet)
        {
            deckViewController.HideDeck();
            currentDeckViewType = DeckViewType.None;
            return;
        }

        // Show Logic (or switch view)
        CardManager cardManager = gameManager.GetCardManager();
        if (cardManager == null) return;

        List<CardData> petDeck = cardManager.GetLocalPetDeck() ?? new List<CardData>();
        string title = $"{gameManager.GetPlayerManager().GetLocalPetName()}'s Deck ({petDeck.Count})";
        deckViewController.ShowDeck(title, petDeck);
        currentDeckViewType = DeckViewType.Pet;
    }

    private void ShowOpponentPetDeck()
    {
        if (deckViewController == null) return;

        // Toggle Logic
        if (deckViewController.gameObject.activeSelf && currentDeckViewType == DeckViewType.OpponentPet)
        {
            deckViewController.HideDeck();
            currentDeckViewType = DeckViewType.None;
            return;
        }

        // Show Logic (or switch view)
        Player opponent = gameManager.GetPlayerManager().GetOpponentPlayer();
        CardManager cardManager = gameManager.GetCardManager();
        if (opponent == null || cardManager == null) return;

        List<CardData> opponentPetDeck = cardManager.GetOpponentPetDeck() ?? new List<CardData>();
        
        // Get the opponent's pet name from custom properties
        string opponentPetName = null;
        if (opponent.CustomProperties.TryGetValue(PhotonManager.PET_NAME_PROPERTY, out object petNameObj))
        {
            opponentPetName = petNameObj as string;
        }
        
        // Use the custom pet name if available, otherwise fall back to the default format
        if (string.IsNullOrEmpty(opponentPetName))
        {
            opponentPetName = string.IsNullOrEmpty(opponent.NickName) ? "Opponent Pet" : $"{opponent.NickName}'s Pet";
        }
        
        string title = $"{opponentPetName} Deck ({opponentPetDeck.Count})";

        deckViewController.ShowDeck(title, opponentPetDeck);
        currentDeckViewType = DeckViewType.OpponentPet;
    }
} 