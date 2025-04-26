using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Newtonsoft.Json;
using System.Linq; // Added for Find

public class GameStateManager
{
    private GameManager gameManager;
    
    // Canvas GameObject references
    private GameObject startScreenInstance;
    private GameObject lobbyInstance;
    private GameObject combatInstance;
    private GameObject draftInstance;
    
    // Lobby UI References
    private GameObject playerListPanel;
    private GameObject playerEntryTemplate;
    private Button readyButton;
    private Button leaveButton;
    private Button startGameButton;
    
    // Start Screen References
    private Button connectButton;
    private TMP_InputField playerNameInput;
    private TMP_InputField petNameInput;
    
    // Combat UI References
    private TextMeshProUGUI scoreText;
    
    // Draft Screen References
    private GameObject draftOptionsPanel;
    private TextMeshProUGUI draftTurnText;
    private GameObject draftOptionButtonTemplate;
    // --- ADDED: Draft Deck View Button References ---
    private Button viewDraftPlayerDeckButton;
    private Button viewDraftPetDeckButton;
    private DeckViewController deckViewController; // Reference to the controller
    
    // Game state
    private GameState currentState = GameState.Connecting;
    private bool userInitiatedConnection = false;
    private bool isWaitingToStartCombatRPC = false;
    private bool wasInRoom = false; // ADDED: Track if we were in a room before disconnect
    
    // --- ADDED: State for Draft Deck View Toggle ---
    private enum DeckViewType { None, Player, Pet } // Simpler enum for draft screen
    private DeckViewType currentDraftDeckViewType = DeckViewType.None;
    // --- END ADDED ---
    
    // Constructor for dependency injection
    public GameStateManager(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public GameState GetCurrentState()
    {
        return currentState;
    }
    
    public void SetCurrentState(GameState state)
    {
        currentState = state;
    }
    
    public void Initialize()
    {
        currentState = GameState.Connecting;
        
        // Find and setup canvases that now exist in the scene
        FindAndSetupCanvases();
        
        // Ensure only StartScreenCanvas is enabled initially
        if (startScreenInstance != null) startScreenInstance.SetActive(true);
        if (lobbyInstance != null) lobbyInstance.SetActive(false);
        if (combatInstance != null) combatInstance.SetActive(false);
        if (draftInstance != null) draftInstance.SetActive(false);
    }
    
    private void FindAndSetupCanvases()
    {
        // Find all canvases in the scene, including inactive ones
        Canvas[] allCanvases = Object.FindObjectsOfType<Canvas>(true);
        
        // Find canvases by name
        foreach (Canvas canvas in allCanvases)
        {
            if (canvas.gameObject.name == "StartScreenCanvas")
                startScreenInstance = canvas.gameObject;
            else if (canvas.gameObject.name == "LobbyCanvas")
                lobbyInstance = canvas.gameObject;
            else if (canvas.gameObject.name == "CombatCanvas")
                combatInstance = canvas.gameObject;
            else if (canvas.gameObject.name == "DraftCanvas")
                draftInstance = canvas.gameObject;
        }
        
        // Set up Start Screen
        if (startScreenInstance != null)
        {
            Transform centerPanel = startScreenInstance.transform.Find("CenterPanel");
            if (centerPanel != null)
            {
                connectButton = centerPanel.Find("ConnectButton")?.GetComponent<Button>();
                playerNameInput = centerPanel.Find("PlayerNameInput")?.GetComponent<TMP_InputField>();
                petNameInput = centerPanel.Find("PetNameInput")?.GetComponent<TMP_InputField>();
                
                if (connectButton != null)
                {
                    connectButton.onClick.AddListener(gameManager.ConnectToPhoton);
                }
                else Debug.LogError("ConnectButton not found within CenterPanel in StartScreenCanvas!");
                
                if (playerNameInput == null) Debug.LogError("PlayerNameInput not found within CenterPanel in StartScreenCanvas!");
                else playerNameInput.text = "Player_" + Random.Range(1000, 9999);
                
                if (petNameInput == null) Debug.LogError("PetNameInput not found within CenterPanel in StartScreenCanvas!");
                else petNameInput.text = "Buddy_" + Random.Range(100, 999);
            }
            else
            {
                Debug.LogError("CenterPanel not found in StartScreenCanvas!");
                connectButton = null;
                playerNameInput = null;
                petNameInput = null;
            }
        }
        else Debug.LogError("StartScreenCanvas not found in the scene!");
        
        // Set up Lobby canvas
        if (lobbyInstance != null)
        {
            // Find Lobby elements
            Transform panelTransform = lobbyInstance.transform.Find("PlayerListPanel");
            playerListPanel = panelTransform?.gameObject;
            playerEntryTemplate = panelTransform?.Find("PlayerEntryTemplate")?.gameObject;
            readyButton = lobbyInstance.transform.Find("ReadyButton")?.GetComponent<Button>();
            leaveButton = lobbyInstance.transform.Find("LeaveButton")?.GetComponent<Button>();
            startGameButton = lobbyInstance.transform.Find("StartGameButton")?.GetComponent<Button>();
            
            // Assign listeners
            readyButton?.onClick.AddListener(gameManager.ToggleReadyStatus);
            leaveButton?.onClick.AddListener(gameManager.LeaveRoom);
            startGameButton?.onClick.AddListener(gameManager.StartGame);
            
            if (playerListPanel == null || playerEntryTemplate == null || readyButton == null || leaveButton == null || startGameButton == null)
                Debug.LogError("One or more Lobby UI elements not found in LobbyCanvas!");
            
            playerEntryTemplate?.SetActive(false);
        }
        else Debug.LogError("LobbyCanvas not found in the scene!");
        
        // Set up Combat canvas - just find it but don't initialize references yet
        if (combatInstance != null)
        {
            // Find ScoreText
            Transform topArea = combatInstance.transform.Find("TopArea");
            if (topArea != null)
            {
                scoreText = topArea.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
            }
        }
        else Debug.LogError("CombatCanvas not found in the scene!");
        
        // Set up Draft canvas
        if (draftInstance != null)
        {
            // Find Draft UI Elements
            draftOptionsPanel = draftInstance.transform.Find("DraftOptionsPanel")?.gameObject;
            draftTurnText = draftInstance.transform.Find("DraftTurnText")?.GetComponent<TextMeshProUGUI>();
            draftOptionButtonTemplate = draftOptionsPanel?.transform.Find("OptionButtonTemplate")?.gameObject;
            
            // Find Draft Deck View Buttons & Controller
            Transform deckButtonsPanel = draftInstance.transform.Find("DeckButtonsPanel");
            viewDraftPlayerDeckButton = deckButtonsPanel?.Find("ViewPlayerDeckButton")?.GetComponent<Button>();
            viewDraftPetDeckButton = deckButtonsPanel?.Find("ViewPetDeckButton")?.GetComponent<Button>();
            
            // Logging for button discovery
            if (viewDraftPlayerDeckButton != null)
            {
                Debug.Log("Successfully found ViewPlayerDeckButton.");
            }
            else
            {
                Debug.LogError("ViewPlayerDeckButton component NOT found on DeckButtonsPanel/ViewPlayerDeckButton object!");
            }

            // Find or instantiate DeckViewController
            GameObject deckViewerPanelInstance = draftInstance.transform.Find("DeckViewerPanel")?.gameObject;
            if (deckViewerPanelInstance == null && gameManager.GetDeckViewerPanelPrefab() != null) 
            {
                Debug.Log("DeckViewerPanel not found, attempting to instantiate from prefab.");
                deckViewerPanelInstance = Object.Instantiate(gameManager.GetDeckViewerPanelPrefab(), draftInstance.transform);
                deckViewerPanelInstance.name = "DeckViewerPanel"; 
            }
            
            if (deckViewerPanelInstance != null)
            {
                deckViewController = deckViewerPanelInstance.GetComponent<DeckViewController>();
                if (deckViewController == null) 
                {
                    Debug.LogError("DeckViewController component not found on DeckViewerPanel!");
                }
                else
                {
                    // Set up DeckViewerPanel (initially hidden)
                    deckViewerPanelInstance.SetActive(false);
                    Debug.Log("DeckViewerPanel found and initialized.");
                    
                    // Add button handlers
                    if (viewDraftPlayerDeckButton != null)
                    {
                        viewDraftPlayerDeckButton.onClick.RemoveAllListeners();
                        viewDraftPlayerDeckButton.onClick.AddListener(ShowDraftPlayerDeck);
                        Debug.Log("ViewPlayerDeckButton listener added.");
                    }
                    
                    if (viewDraftPetDeckButton != null)
                    {
                        viewDraftPetDeckButton.onClick.RemoveAllListeners();
                        viewDraftPetDeckButton.onClick.AddListener(ShowDraftPetDeck);
                        Debug.Log("ViewPetDeckButton listener added.");
                    }
                }
            }
            else
            {
                Debug.LogError("Failed to find or create DeckViewerPanel!");
            }
            
            if (draftOptionsPanel == null || draftTurnText == null || draftOptionButtonTemplate == null)
            {
                Debug.LogError("One or more Draft UI elements not found in DraftCanvas!");
            }
            
            if (draftOptionButtonTemplate != null)
            {
                draftOptionButtonTemplate.SetActive(false); // Hide template
            }
        }
        else Debug.LogError("DraftCanvas not found in the scene!");
    }
    
    public void ShowStartScreen()
    {
        if (startScreenInstance != null)
        {
            startScreenInstance.SetActive(true);
            if (connectButton != null) connectButton.interactable = true;
        }
    }
    
    public void HideStartScreen()
    {
        if (startScreenInstance != null)
            startScreenInstance.SetActive(false);
    }
    
    public void ShowLobbyScreen()
    {
        if (lobbyInstance != null)
        {
            lobbyInstance.SetActive(true);
            gameManager.UpdateLobbyControls();
        }
        else
        {
            Debug.LogError("Cannot show lobby screen - LobbyCanvas not found in scene!");
        }
    }
    
    public void HideLobbyScreen()
    {
        if (lobbyInstance != null)
            lobbyInstance.SetActive(false);
    }
    
    public void ShowCombatScreen()
    {
        if (combatInstance != null)
        {
            // Initialize combat screen references before showing it
            gameManager.InitializeCombatScreenReferences(combatInstance);
            combatInstance.SetActive(true);
        }
        else
        {
            Debug.LogError("Cannot show combat screen - CombatCanvas not found in scene!");
        }
    }
    
    public void HideCombatScreen()
    {
        Debug.Log("Hiding Combat Screen");
        if (combatInstance != null) combatInstance.SetActive(false);
    }
    
    public void ShowDraftScreen()
    {
        Debug.Log("Showing Draft Screen");
        if (draftInstance != null)
        {
            draftInstance.SetActive(true);
        }
        else
        {
            Debug.LogError("Cannot show draft screen - DraftCanvas not found in scene!");
        }
    }
    
    public void HideDraftScreen()
    {
        Debug.Log("Hiding Draft Screen");
        if (draftInstance != null) draftInstance.SetActive(false);
    }
    
    public void TransitionToLobby()
    {
        SetCurrentState(GameState.Lobby);
        HideStartScreen();
        HideCombatScreen();
        HideDraftScreen();
        ShowLobbyScreen();
        wasInRoom = true; // Set flag when entering lobby
    }
    
    public void TransitionToCombat()
    {
        SetCurrentState(GameState.Combat);
        HideLobbyScreen();
        HideDraftScreen();
        ShowCombatScreen();
        wasInRoom = true; // Set flag when starting combat
    }
    
    public void TransitionToDraft()
    {
        SetCurrentState(GameState.Drafting);
        HideCombatScreen();
        HideLobbyScreen(); 
        ShowDraftScreen();
        wasInRoom = true; // Set flag when starting draft
    }
    
    public void TransitionToGameOver()
    {
        // You might want to set wasInRoom = false here depending on desired flow
        // For now, assume we stay "in room" technically until explicitly leaving or disconnected
        SetCurrentState(GameState.GameOver);
        HideCombatScreen();
        HideDraftScreen();
        HideLobbyScreen();
        // Show a game over screen here...
        Debug.Log("GAME OVER - Transitioning back to Start Screen for now");
        ShowStartScreen(); // Placeholder
    }
    
    public void HandleFullDisconnect()
    {
        Debug.Log("HandleFullDisconnect: Resetting state and showing Start Screen.");
        wasInRoom = false; // Clear flag on full disconnect
        HideLobbyScreen();
        HideCombatScreen();
        HideDraftScreen();
        // TODO: Hide Reconnecting UI if it exists
        ShowStartScreen();
        SetCurrentState(GameState.Connecting); // Return to initial state
    }
    
    public bool IsUserInitiatedConnection()
    {
        return userInitiatedConnection;
    }
    
    public void SetUserInitiatedConnection(bool value)
    {
        userInitiatedConnection = value;
    }
    
    public void SetWaitingToStartCombatRPC(bool value)
    {
        isWaitingToStartCombatRPC = value;
    }
    
    public bool IsWaitingToStartCombatRPC()
    {
        return isWaitingToStartCombatRPC;
    }
    
    public TMP_InputField GetPlayerNameInput()
    {
        return playerNameInput;
    }
    
    public TMP_InputField GetPetNameInput()
    {
        return petNameInput;
    }
    
    public Button GetConnectButton()
    {
        return connectButton;
    }
    
    public GameObject GetPlayerListPanel()
    {
        return playerListPanel;
    }
    
    public GameObject GetPlayerEntryTemplate()
    {
        return playerEntryTemplate;
    }
    
    public GameObject GetDraftOptionsPanel()
    {
        return draftOptionsPanel;
    }
    
    public TextMeshProUGUI GetDraftTurnText()
    {
        return draftTurnText;
    }
    
    public GameObject GetDraftOptionButtonTemplate()
    {
        return draftOptionButtonTemplate;
    }
    
    public GameObject GetLobbyInstance()
    {
        return lobbyInstance;
    }
    
    public Button GetStartGameButton()
    {
        return startGameButton;
    }
    
    public GameObject GetCombatInstance()
    {
        return combatInstance;
    }
    
    public TextMeshProUGUI GetScoreText()
    {
        return scoreText;
    }
    
    public void UpdateDraftUI(List<SerializableDraftOption> localCurrentPack, Dictionary<int, DraftOption> localActiveOptionMap, bool isWaitingForLocalPick)
    {
        if (currentState != GameState.Drafting || draftInstance == null || !draftInstance.activeSelf)
        {
            return;
        }
        
        if (draftOptionsPanel == null || draftTurnText == null || draftOptionButtonTemplate == null)
        {
            Debug.LogError("Cannot update Draft UI - references are missing.");
            return;
        }
        
        // Clear previous option buttons first
        foreach (Transform child in draftOptionsPanel.transform)
        {
            if (child.gameObject != draftOptionButtonTemplate) // Don't destroy template
            {
                Object.Destroy(child.gameObject);
            }
        }
        
        // Determine UI state based on whether the local player has a pack
        if (localCurrentPack.Count > 0)
        {
            draftTurnText.text = "Your Turn to Pick!";
            // Populate buttons from the local pack
            foreach (var serializableOption in localCurrentPack)
            {
                if (localActiveOptionMap.TryGetValue(serializableOption.OptionId, out DraftOption optionData))
                {
                    GameObject optionButtonGO = Object.Instantiate(draftOptionButtonTemplate, draftOptionsPanel.transform);
                    optionButtonGO.SetActive(true);
                    
                    TextMeshProUGUI buttonText = optionButtonGO.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText != null) buttonText.text = optionData.Description;
                    
                    Button optionButton = optionButtonGO.GetComponent<Button>();
                    if (optionButton != null)
                    {
                        int optionId = optionData.OptionId;
                        optionButton.onClick.RemoveAllListeners();
                        optionButton.onClick.AddListener(() => gameManager.HandleOptionSelected(optionId));
                        optionButton.interactable = true;
                    }
                }
                else
                {
                    Debug.LogWarning($"Option ID {serializableOption.OptionId} found in local pack list but not in active map. UI cannot display.");
                }
            }
        }
        else
        {
            // Local player doesn't have a pack right now
            draftTurnText.text = "Waiting for next pack...";
            // Buttons are already cleared
        }
    }

    // --- ADDED: Deck Viewing Methods for Draft Screen ---
    public void ShowDraftPlayerDeck()
    {
        Debug.Log("--- ShowDraftPlayerDeck method called! ---");
        if (deckViewController == null) 
        {
            Debug.LogError("DeckViewController is null in ShowDraftPlayerDeck");
            return;
        }
        
        // Toggle Logic
        if (deckViewController.gameObject.activeSelf && currentDraftDeckViewType == DeckViewType.Player)
        {
            deckViewController.HideDeck();
            currentDraftDeckViewType = DeckViewType.None;
            return;
        }
        
        // Show Logic (or switch view)
        CardManager cardManager = gameManager.GetCardManager();
        if (cardManager == null) 
        {
             Debug.LogError("CardManager is null in ShowDraftPlayerDeck");
            return;
        }

        List<CardData> playerDeck = cardManager.GetAllOwnedPlayerCards(); 
        if (playerDeck != null && playerDeck.Count > 0) 
        {
             Debug.Log("GameStateManager.ShowDraftPlayerDeck: Calling deckViewController.ShowDeck...");
             deckViewController.ShowDeck("My Deck", playerDeck);
             currentDraftDeckViewType = DeckViewType.Player; // Update state
             Debug.Log("GameStateManager.ShowDraftPlayerDeck: deckViewController.ShowDeck call finished.");
        }
        else
        {
            Debug.LogWarning("Player deck is null or empty, cannot show.");
            // Optionally hide the deck view if it was showing the other deck
            if (deckViewController.gameObject.activeSelf) {
                deckViewController.HideDeck();
                currentDraftDeckViewType = DeckViewType.None;
            }
        }
    }

    public void ShowDraftPetDeck()
    {
         if (deckViewController == null) 
        {
            Debug.LogError("DeckViewController is null in ShowDraftPetDeck");
            return;
        }
        
        // Toggle Logic
        if (deckViewController.gameObject.activeSelf && currentDraftDeckViewType == DeckViewType.Pet)
        {
            deckViewController.HideDeck();
            currentDraftDeckViewType = DeckViewType.None;
            return;
        }

        // Show Logic (or switch view)
        CardManager cardManager = gameManager.GetCardManager();
        if (cardManager == null) 
        {
            Debug.LogError("CardManager is null in ShowDraftPetDeck");
            return;
        }
        
        List<CardData> petDeck = cardManager.GetAllOwnedPetCards(); 
        if (petDeck != null && petDeck.Count > 0) 
        {
            deckViewController.ShowDeck("Pet Deck", petDeck);
            currentDraftDeckViewType = DeckViewType.Pet; // Update state
        }
         else
        {
            Debug.LogWarning("Pet deck is null or empty, cannot show.");
            // Optionally hide the deck view if it was showing the other deck
            if (deckViewController.gameObject.activeSelf) {
                deckViewController.HideDeck();
                currentDraftDeckViewType = DeckViewType.None;
            }
        }
    }
    // --- END ADDED SECTION ---

    // --- ADDED: Getter for wasInRoom state ---
    public bool WasInRoomWhenDisconnected()
    {
        return wasInRoom;
    }
    // --- END ADDED ---

    // --- ADDED: Setter for wasInRoom state ---
    public void SetWasInRoom(bool value)
    {
        wasInRoom = value;
    }
    // --- END ADDED ---

    // --- ADDED: Method to display reconnection attempt UI ---
    public void ShowReconnectingUI()
    {
        // TODO: Implement actual UI (e.g., enable a Panel with a Text message)
        Debug.LogWarning("ShowReconnectingUI: Displaying 'Attempting to Reconnect...' message.");
        // Example: reconnectingText.gameObject.SetActive(true);
        if (startScreenInstance != null && connectButton != null) connectButton.interactable = false; // Disable connect button
    }
    
    // --- ADDED: Method to hide reconnection attempt UI ---
    public void HideReconnectingUI()
    {
        // TODO: Implement actual UI hiding
        Debug.Log("HideReconnectingUI: Hiding reconnection message.");
        // Example: reconnectingText.gameObject.SetActive(false);
        // Re-enable connect button ONLY if we failed and are back at start screen
        if (currentState == GameState.Connecting && startScreenInstance != null && connectButton != null)
        {
            connectButton.interactable = true;
        }
    }
    // --- END ADDED ---
}