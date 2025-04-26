using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using ExitGames.Client.Photon;

public class GameManager : MonoBehaviourPunCallbacks
{
    // --- ADDED: Static instance for singleton access ---
    public static GameManager Instance { get; private set; }
    // --- END ADDED ---

    [Header("Settings")]
    [SerializeField] private int startingPlayerHealth = 50;
    [SerializeField] private int startingPetHealth = 30;
    [SerializeField] private int startingEnergy = 3;
    [SerializeField] private int startingPetEnergy = 3;
    [SerializeField] private int cardsToDraw = 5;
    [SerializeField] private List<CardData> starterDeck = new List<CardData>();
    [SerializeField] private List<CardData> starterPetDeck = new List<CardData>();
    [SerializeField] private int optionsPerDraft = 3;

    [Header("Card Pools for Draft/Rewards")]
    [SerializeField] private List<CardData> allPlayerCardPool = new List<CardData>();
    [SerializeField] private List<CardData> allPetCardPool = new List<CardData>();

    // --- ADDED: Win Condition ---
    [SerializeField] private int targetScoreToWin = 10; // Default score to win
    // --- END ADDED ---

    [Header("UI Prefabs")]
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private GameObject deckViewerPanelPrefab;

    // Manager instances
    private GameStateManager gameStateManager;
    private PlayerManager playerManager;
    private CardManager cardManager;
    private PhotonManager photonManager;
    private CombatManager combatManager;
    private CombatUIManager combatUIManager;
    private CombatCalculator combatCalculator;
    private DraftManager draftManager;
    // --- ADDED: Card Preview Calculator ---
    private CardPreviewCalculator cardPreviewCalculator;
    // --- END ADDED ---

    // PhotonView component reference
    private PhotonView photonViewComponent;

    // --- ADDED: Debug toggle ---
    private bool debugCardReuseMode = false;
    // --- ADDED: Debug toggle for enemy test mode ---
    private bool debugEnemyTestMode = false;

    private StatusEffectManager statusEffectManager;
    private CardModificationService cardModificationService;
    private HandPanelHoverManager handPanelHoverManager;
    private DamageNumberManager damageNumberManager;

    #region Unity Lifecycle Methods

    void Awake()
    {
        Debug.Log("GameManager Awake");
        
        // --- MODIFIED: Singleton setup ---
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Another GameManager instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this; 
        // --- END MODIFIED ---
        
        // Ensure only one GameManager instance exists (Kept original check just in case, but Instance check should handle it)
        // if (FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length > 1)
        // {
        //     Destroy(gameObject);
        //     return;
        // }
        DontDestroyOnLoad(gameObject);

        // Get PhotonView component
        photonViewComponent = GetComponent<PhotonView>();
        if (photonViewComponent == null)
        {
            Debug.LogError("GameManager requires a PhotonView component!");
            // Add it if missing? Or rely on prefab setup?
            // photonViewComponent = gameObject.AddComponent<PhotonView>();
            return; 
        }

        // Initialize the combat calculator first (used by other components)
        combatCalculator = new CombatCalculator(this);
        
        // Initialize all manager instances in proper order
        gameStateManager = new GameStateManager(this);
        playerManager = new PlayerManager(this);
        cardManager = new CardManager(this, starterDeck, starterPetDeck, allPlayerCardPool, allPetCardPool, cardsToDraw);
        photonManager = gameObject.AddComponent<PhotonManager>(); // Add as component
        combatManager = new CombatManager(); // Default constructor
        combatUIManager = new CombatUIManager(this); // Initialize with reference to this GameManager
        draftManager = new DraftManager(); // Default constructor
        // --- ADDED: Initialize Card Preview Calculator ---
        cardPreviewCalculator = new CardPreviewCalculator(this);
        // --- END ADDED ---

        // ** Initialize and sync local pet deck early **
        if (cardManager != null)
        {
            cardManager.InitializeAndSyncLocalPetDeck();
        }
        else
        {
            Debug.LogError("CardManager is null after instantiation in Awake!");
        }
    }

    void Start()
    {
        // Initialize manager classes that require it after Awake
        gameStateManager.Initialize();
        playerManager.Initialize(startingPlayerHealth, startingPetHealth, startingEnergy);
        photonManager.Initialize(this); // Assuming PhotonManager needs Initialize
        combatManager.Initialize(this, startingPlayerHealth, startingPetHealth, startingEnergy, startingPetEnergy, combatUIManager); // Pass our UIManager
        draftManager.Initialize(this, optionsPerDraft); // Pass necessary refs/config
    }

    // --- ADDED: Update method for keyboard shortcuts ---
    void Update()
    {
        // Only process debug commands in Combat state
        if (gameStateManager != null && gameStateManager.GetCurrentState() == GameState.Combat)
        {
            // Check for Alt key
            bool altKeyPressed = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            
            if (altKeyPressed)
            {
                // Toggle debug card reuse mode with Alt+R
                if (Input.GetKeyDown(KeyCode.R))
                {
                    debugCardReuseMode = !debugCardReuseMode;
                    Debug.Log($"Debug Card Reuse Mode: {(debugCardReuseMode ? "ENABLED" : "DISABLED")}");
                    
                    // Show on-screen notification
                    if (combatUIManager != null)
                    {
                        combatUIManager.ShowDebugModeStatus("Card Reuse Mode", debugCardReuseMode);
                    }
                }
                
                // Reset all status effects with Alt+S
                if (Input.GetKeyDown(KeyCode.S))
                {
                    ResetAllStatusEffects();
                    
                    // Show on-screen notification
                    if (combatUIManager != null)
                    {
                        string message = PhotonNetwork.InRoom ? 
                            "Status Effects Reset (All Players)" : 
                            "Status Effects Reset";
                        combatUIManager.ShowDebugModeStatus(message, true);
                    }
                }
                
                // Toggle enemy test mode with Alt+T
                if (Input.GetKeyDown(KeyCode.T))
                {
                    debugEnemyTestMode = !debugEnemyTestMode;
                    Debug.Log($"Debug Enemy Test Mode: {(debugEnemyTestMode ? "ENABLED" : "DISABLED")}");
                    
                    // Show on-screen notification
                    if (combatUIManager != null)
                    {
                        combatUIManager.ShowDebugModeStatus("Enemy Test Mode", debugEnemyTestMode);
                    }
                }
            }
        }
    }

    #endregion

    #region Photon Callbacks
    
    // These override methods forward to PhotonManager
    public override void OnConnectedToMaster() => photonManager.OnConnectedToMaster();
    public override void OnJoinedRoom() => photonManager.OnJoinedRoom();
    public override void OnLeftRoom() => photonManager.OnLeftRoom();
    public override void OnJoinRoomFailed(short returnCode, string message) => photonManager.OnJoinRoomFailed(returnCode, message);
    public override void OnCreateRoomFailed(short returnCode, string message) => photonManager.OnCreateRoomFailed(returnCode, message);
    public override void OnDisconnected(DisconnectCause cause) => photonManager.OnDisconnected(cause);
    public override void OnPlayerEnteredRoom(Player newPlayer) => photonManager.OnPlayerEnteredRoom(newPlayer);
    public override void OnPlayerLeftRoom(Player otherPlayer) => photonManager.OnPlayerLeftRoom(otherPlayer);
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        photonManager.OnPlayerPropertiesUpdate(targetPlayer, changedProps);
        
        // --- MODIFIED: Update local simulation based on ANY player property changes ---
        if (targetPlayer == null) return; // Safety check

        Player opponentPlayer = playerManager?.GetOpponentPlayer();
        bool isOpponent = opponentPlayer != null && targetPlayer == opponentPlayer;
        bool isSelf = targetPlayer == PhotonNetwork.LocalPlayer;

        // --- Pet Energy Update (Applies to Opponent Pet Sim or Local Pet Property) ---
        if (changedProps.ContainsKey(CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP))
        {
            try
            {
                int newPetEnergy = (int)changedProps[CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP];
                if (isOpponent)
                {
                    // Update our simulation of the opponent's pet
                    Debug.Log($"OnPlayerPropertiesUpdate: Opponent ({targetPlayer.NickName}) pet energy changed to {newPetEnergy}. Updating local simulation.");
                    cardManager?.GetPetDeckManager()?.SetOpponentPetEnergy(newPetEnergy);
                }
                // No action needed if isSelf, as the property was set locally.
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to cast pet energy property: {e.Message}");
            }
        }

        // --- Player Stat Updates (Apply to Opponent Player Simulation) ---
        if (isOpponent)
        {
            HealthManager hm = playerManager?.GetHealthManager();
            StatusEffectManager sem = playerManager?.GetStatusEffectManager();
            EnergyManager em = playerManager?.GetEnergyManager();

            if (hm != null) 
            {
                if (changedProps.TryGetValue(CombatStateManager.PLAYER_COMBAT_PLAYER_HP_PROP, out object hpVal)) 
                    hm.SetOpponentPlayerHealth((int)hpVal);
                if (changedProps.TryGetValue(CombatStateManager.PLAYER_COMBAT_PLAYER_BLOCK_PROP, out object blockVal)) 
                    hm.SetOpponentPlayerBlock((int)blockVal);
                if (changedProps.TryGetValue(CombatStateManager.PLAYER_COMBAT_PLAYER_DOT_TURNS_PROP, out object dotTurnsVal) && 
                    changedProps.TryGetValue(CombatStateManager.PLAYER_COMBAT_PLAYER_DOT_DMG_PROP, out object dotDmgVal)) 
                    hm.SetOpponentPlayerDot((int)dotTurnsVal, (int)dotDmgVal);
                if (changedProps.TryGetValue(CombatStateManager.PLAYER_COMBAT_PLAYER_HOT_TURNS_PROP, out object hotTurnsVal) && 
                    changedProps.TryGetValue(CombatStateManager.PLAYER_COMBAT_PLAYER_HOT_AMT_PROP, out object hotAmtVal)) 
                    hm.SetOpponentPlayerHot((int)hotTurnsVal, (int)hotAmtVal);
                if (changedProps.TryGetValue(CombatStateManager.PLAYER_COMBAT_PLAYER_CRIT_PROP, out object critVal)) 
                    hm.SetOpponentPlayerCritChance((int)critVal);
            }
            
            if (sem != null)
            {
                 if (changedProps.TryGetValue(CombatStateManager.PLAYER_COMBAT_PLAYER_WEAK_PROP, out object weakVal)) 
                    sem.SetOpponentPlayerWeakTurns((int)weakVal);
                 if (changedProps.TryGetValue(CombatStateManager.PLAYER_COMBAT_PLAYER_BREAK_PROP, out object breakVal)) 
                    sem.SetOpponentPlayerBreakTurns((int)breakVal);
                 if (changedProps.TryGetValue(CombatStateManager.PLAYER_COMBAT_PLAYER_THORNS_PROP, out object thornsVal)) 
                    sem.SetOpponentPlayerThorns((int)thornsVal);
                 if (changedProps.TryGetValue(CombatStateManager.PLAYER_COMBAT_PLAYER_STRENGTH_PROP, out object strengthVal)) 
                    sem.SetOpponentPlayerStrength((int)strengthVal);
            }
            
            if (em != null)
            {
                if (changedProps.TryGetValue(CombatStateManager.PLAYER_COMBAT_ENERGY_PROP, out object energyVal)) 
                    em.SetOpponentPlayerEnergy((int)energyVal);
            }
            
            Debug.Log($"Updated opponent ({targetPlayer.NickName}) simulated stats based on received properties.");
        }
        // --- END Player Stat Updates ---
        
        // Update Score UI and Other Fights UI regardless of who changed properties, if in relevant game states
        if (gameStateManager.GetCurrentState() == GameState.Combat || gameStateManager.GetCurrentState() == GameState.Drafting)
        {
            playerManager.GetCombatStateManager().UpdateScoreUI(); // Use CombatStateManager via PlayerManager
            if (combatUIManager != null)
            {
                combatUIManager.UpdateOtherFightsUI();
            }
            combatManager.UpdateHealthUI(); // Update main Health UI as well for property changes
        }

        // --- Pet Health Update ---
        if (changedProps.ContainsKey(CombatStateManager.PLAYER_COMBAT_PET_HP_PROP))
        {
            try
            {
                int newPetHealth = (int)changedProps[CombatStateManager.PLAYER_COMBAT_PET_HP_PROP];
                if (isOpponent)
                {
                    // This is the health of the opponent's pet (which could be the pet we are fighting)
                    Debug.Log($"OnPlayerPropertiesUpdate: Opponent pet health changed to {newPetHealth}. Updating local simulation.");
                    playerManager?.GetHealthManager()?.SetOpponentPetHealth(newPetHealth);
                    // Update UI immediately to reflect the change
                    UpdateHealthUI();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to cast pet health property: {e.Message}");
            }
        }
    }
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) => photonManager.OnRoomPropertiesUpdate(propertiesThatChanged);
    public override void OnMasterClientSwitched(Player newMasterClient) => photonManager.OnMasterClientSwitched(newMasterClient);
    
    #endregion

    #region Connection & Room Functions

    public void ConnectToPhoton()
    {
        string playerName = gameStateManager.GetPlayerNameInput()?.text;
        string petName = gameStateManager.GetPetNameInput()?.text;
        
        playerManager.SetLocalPetName(petName);
        photonManager.ConnectToPhoton(playerName, petName);
    }

    public void LeaveRoom()
    {
        photonManager.LeaveRoom();
    }

    #endregion

    #region Lobby Functions

    public void UpdatePlayerList()
    {
        playerManager.UpdatePlayerList(gameStateManager.GetPlayerListPanel(), gameStateManager.GetPlayerEntryTemplate());
        UpdateLobbyControls();
    }

    public void UpdateLobbyControls()
    {
        GameObject lobbyInstance = gameStateManager.GetLobbyInstance();
        if (lobbyInstance == null) return;
        
        bool allReady = playerManager.CheckAllPlayersReady();
        
        Button startGameButton = gameStateManager.GetStartGameButton();
        if (startGameButton != null)
        {
            // Allow button to be visible for all players in the lobby
            startGameButton.gameObject.SetActive(true); 
            // Allow button interaction if all players are ready, regardless of master client status
            startGameButton.interactable = allReady; 
        }
    }

    public void ToggleReadyStatus()
    {
        bool currentStatus = false;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PlayerManager.PLAYER_READY_PROPERTY, out object isReadyStatus))
            currentStatus = (bool)isReadyStatus;
        
        playerManager.SetPlayerReady(!currentStatus);
    }

    #endregion

    #region Game Start & Combat Functions

    // This method is called when the Start Game button is clicked
    public void StartGame()
    {
        // Perform local checks first to avoid unnecessary RPCs or actions
        if (!PhotonNetwork.CurrentRoom.IsOpen) 
        {
            Debug.LogWarning("StartGame clicked, but room is already closed.");
            return;
        }
        if (!playerManager.CheckAllPlayersReady())
        {
            Debug.LogWarning("StartGame clicked, but not enough active players are ready.");
            // Optional: Update UI to show not ready? Maybe disable button again briefly?
            // UpdateLobbyControls(); 
            return;
        }

        // If this client is the Master Client, start the game directly.
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Master Client initiating game start directly.");
            ExecuteStartGameSequence(); 
        }
        // If this client is NOT the Master Client, send an RPC to the Master Client to request the start.
        else
        {
            Debug.Log($"Non-Master Client {PhotonNetwork.LocalPlayer.NickName} sending RPC to Master Client to request game start.");
            photonViewComponent.RPC("RpcRequestStartGame", RpcTarget.MasterClient);
        }
    }

    // RPC called by non-Master Clients, executed only on the Master Client
    [PunRPC]
    private void RpcRequestStartGame(PhotonMessageInfo info)
    {
        Debug.Log($"Master Client received RpcRequestStartGame from {info.Sender.NickName}.");

        // Master Client performs final checks before starting
        if (!PhotonNetwork.CurrentRoom.IsOpen) 
        {
            Debug.LogWarning("RpcRequestStartGame received, but room is already closed.");
            return;
        }
        if (!playerManager.CheckAllPlayersReady())
        {
            Debug.LogWarning("RpcRequestStartGame received, but not enough active players are ready.");
            // Inform the requesting client? Might be overkill.
            return;
        }

        // Checks passed, execute the start sequence
        ExecuteStartGameSequence();
    }

    // Contains the actual game starting logic, should only be called by the Master Client
    private void ExecuteStartGameSequence()
    {
        Debug.Log("Master Client executing StartGame setup...");

        // Prevent others from joining
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;

        // Initialize scores for all players to 0
        Debug.Log("Initializing player scores...");
        Hashtable initialScoreProps = new Hashtable { { PlayerManager.PLAYER_SCORE_PROP, 0 } };
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            p.SetCustomProperties(initialScoreProps);
            Debug.Log($"Set initial score for {p.NickName}");
        }

        // Prepare combat round (initiates the process, likely sets room props and triggers RpcStartCombat eventually)
        playerManager.PrepareNextCombatRound();
    }

    [PunRPC]
    private void RpcStartCombat()
    {
        Debug.Log($"RPC: Starting Combat Setup for {PhotonNetwork.LocalPlayer.NickName}");
        gameStateManager.TransitionToCombat();

        // Fetch pairings from Room Properties
        int opponentPetOwnerActorNum = -1;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PlayerManager.COMBAT_PAIRINGS_PROP, out object pairingsObj))
        {
            try
            {
                string pairingsJson = pairingsObj as string;
                var pairingsDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<int, int>>(pairingsJson);
                if (pairingsDict != null && pairingsDict.TryGetValue(PhotonNetwork.LocalPlayer.ActorNumber, out int oppActorNum))
                {
                    opponentPetOwnerActorNum = oppActorNum;
                    Debug.Log($"Combat pairing found: Fighting pet of player {opponentPetOwnerActorNum}");
                }
                else
                {
                    Debug.LogWarning("Could not find local player's pairing in COMBAT_PAIRINGS_PROP.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to deserialize combat pairings: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"Room property {PlayerManager.COMBAT_PAIRINGS_PROP} not found.");
        }

        // Initialize combat state for this player
        combatManager.InitializeCombatState(opponentPetOwnerActorNum);
    }

    public void InitializeCombatScreenReferences(GameObject combatInstance)
    {
        combatManager.InitializeCombatScreenReferences(combatInstance);
    }

    #endregion

    #region Card Play Functions

    public bool IsPlayerTurn()
    {
        return combatManager.IsPlayerTurn();
    }

    public bool AttemptPlayCard(CardData cardData, CardDropZone.TargetType targetType, GameObject cardGO = null)
    {
        if (!IsPlayerTurn())
        {
            Debug.LogWarning("AttemptPlayCard: Cannot play card, not player's turn.");
            return false;
        }
        // Pass the optional GameObject along
        return combatManager.AttemptPlayCard(cardData, targetType, cardGO);
    }

    #endregion

    #region Draft Functions

    [PunRPC]
    private void RpcStartDraft()
    {
        draftManager.StartDraft();
    }

    public void HandleOptionSelected(int selectedOptionId)
    {
        draftManager.HandleOptionSelected(selectedOptionId);
    }

    [PunRPC]
    private void RpcPlayerPickedOption(int chosenOptionId, PhotonMessageInfo info)
    {
        draftManager.ProcessPlayerPickedOption(chosenOptionId, info.Sender);
    }

    #endregion

    #region RPCs and Network Functions

    [PunRPC]
    private void RpcTakePetDamage(int finalDamageAmount)
    {
        if (finalDamageAmount <= 0) return;

        Debug.Log($"RPC Received: My Pet taking {finalDamageAmount} final damage.");
        // Pass the final damage amount calculated by the sender
        playerManager.DamageLocalPet(finalDamageAmount); 
        
        // Show damage number on this client even though the damage was initiated by the opponent
        DamageNumberManager damageManager = GetDamageNumberManager();
        if (damageManager != null)
        {
            GameObject ownPetObject = GetCombatUIManager().GetOwnPetUIArea();
            damageManager.ShowDamageNumber(finalDamageAmount, ownPetObject, false);
        }
    }

    // --- ADDED: RPC for Hand Cost Modifier ---
    [PunRPC]
    private void RpcApplyHandCostModifier(int amount, int duration, int cardCount)
    {
        Debug.Log($"RPC Received: Apply Hand Cost Modifier Amount={amount}, Duration={duration}, Count={cardCount}");
        playerManager.ApplyCostModifierToLocalHand(amount, duration, cardCount);
        // The UI will update implicitly when hand is redrawn or cost is checked
    }
    // --- END ADDED ---

    // --- ADDED: RPC for Opponent's Pet taking damage (e.g., from player attacking their own pet) ---
    [PunRPC]
    private void RpcOpponentPetTookDamage(int damageAmount)
    {
        if (damageAmount <= 0) return;

        Debug.Log($"RPC Received: Opponent Pet (the one I'm fighting) taking {damageAmount} damage.");
        // Apply this damage to the pet we are fighting, WITHOUT triggering another RPC
        // Use DamageSource.Other as this isn't a direct attack from the opponent pet itself
        playerManager.GetHealthManager().ApplyDamageToOpponentPetLocally(damageAmount, HealthManager.DamageSource.Other);
    }
    // --- END ADDED ---

    // --- ADDED: RPC Receiver for Local Pet Block Update (Sent by owner to others) ---
    [PunRPC]
    private void RpcUpdateLocalPetBlock(int newTotalBlock, PhotonMessageInfo info)
    {
        // Only execute if the sender is not ourselves
        if (info.Sender != null && info.Sender != PhotonNetwork.LocalPlayer)
        {
            Debug.Log($"RPC: Received RpcUpdateLocalPetBlock({newTotalBlock}) from {info.Sender.NickName}. Updating opponent's view of our pet's block.");
            if (playerManager != null)
            {
                playerManager.GetHealthManager().SetOpponentPetBlock(newTotalBlock);
                // playerManager.UpdateOtherPlayerPetBlock(info.Sender.ActorNumber, newTotalBlock);
            }
        }
        else
        {
            Debug.LogWarning($"RPC: Received RpcUpdateLocalPetBlock from self or null sender. Ignoring.");
        }
    }

    // New RPC for updating opponent pet block
    [PunRPC]
    private void RpcUpdateOpponentPetBlock(int newTotalBlock, PhotonMessageInfo info)
    {
        // This RPC is received when we damage the opponent's pet and need to update their local pet's block
        if (info.Sender != null && info.Sender != PhotonNetwork.LocalPlayer)
        {
            Debug.Log($"RPC: Received RpcUpdateOpponentPetBlock({newTotalBlock}) from {info.Sender.NickName}. Updating our local pet's block.");
            if (playerManager != null)
            {
                // Set our local pet's block to the value provided by the opponent
                playerManager.GetHealthManager().SetLocalPetBlock(newTotalBlock);
            }
        }
        else
        {
            Debug.LogWarning($"RPC: Received RpcUpdateOpponentPetBlock from self or null sender. Ignoring.");
        }
    }

    // --- ADDED: RPC Receiver for Adding Block to Local Pet (Sent by opponent targeting our pet) ---
    [PunRPC]
    private void RpcAddBlockToLocalPet(int amount, PhotonMessageInfo info)
    {
        // This RPC is sent *to us* when an *opponent* plays a card that gives *our pet* block.
        if (info.Sender != null && info.Sender != PhotonNetwork.LocalPlayer)
        {
             Debug.Log($"RPC: Received RpcAddBlockToLocalPet({amount}) from {info.Sender.NickName}. Adding block to our local pet.");
             // Directly add the block amount to our local pet
             playerManager.AddBlockToLocalPet(amount);
        }
         else
        {
            Debug.LogWarning($"RPC: Received RpcAddBlockToLocalPet from self or null sender. Ignoring.");
        }
    }

    // --- ADDED: RPC Receiver for Adding Energy to Local Pet (Sent by opponent targeting our pet) ---
    [PunRPC]
    private void RpcAddEnergyToLocalPet(int amount, PhotonMessageInfo info)
    {
        // This RPC is sent *to us* when an *opponent* plays a card that gives *our pet* energy.
        if (info.Sender != null && info.Sender != PhotonNetwork.LocalPlayer)
        {
             Debug.Log($"RPC: Received RpcAddEnergyToLocalPet({amount}) from {info.Sender.NickName}. Adding energy to our local pet.");
             // How should local pet energy be tracked/added? 
             // Currently, pets don't consume energy controlled by the player.
             // For now, let's just log this. If pets need their own energy pool managed by the player,
             // we would call a method like playerManager.AddEnergyToLocalPet(amount) here.
             // Let's update the custom property directly, as that's what the UI reads.
             int currentPetEnergy = 0; 
             if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP, out object energyObj))
             {
                 try { currentPetEnergy = (int)energyObj; } catch {}
             }
             int newPetEnergy = currentPetEnergy + amount;
             ExitGames.Client.Photon.Hashtable petEnergyProp = new ExitGames.Client.Photon.Hashtable
             {
                 { CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP, newPetEnergy }
             };
             PhotonNetwork.LocalPlayer.SetCustomProperties(petEnergyProp);
        }
         else
        {
            Debug.LogWarning($"RPC: Received RpcAddEnergyToLocalPet from self or null sender. Ignoring.");
        }
    }

    // --- ADDED: RPC Receiver for Updating the Local Pet's Energy Property (Sent by simulator after pet's turn) ---
    [PunRPC]
    private void RpcUpdateMyPetEnergyProperty(int finalEnergy, PhotonMessageInfo info)
    {
        // This RPC is sent TO US (the pet owner) by the opponent who just finished SIMULATING our pet's turn.
        if (info.Sender != null && info.Sender != PhotonNetwork.LocalPlayer)
        {
            Debug.Log($"RPC: Received RpcUpdateMyPetEnergyProperty({finalEnergy}) from simulator {info.Sender.NickName}. Setting my pet's energy property.");
            ExitGames.Client.Photon.Hashtable petEnergyProp = new ExitGames.Client.Photon.Hashtable
            {
                { CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP, finalEnergy }
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(petEnergyProp);
            
            // UI will update via OnPlayerPropertiesUpdate callback
        }
         else
        {
            Debug.LogWarning($"RPC: Received RpcUpdateMyPetEnergyProperty from self or null sender. Ignoring.");
        }
    }

    // --- ADDED: RPC Receiver for Resetting Local Pet Block ---
    [PunRPC]
    private void RpcResetMyPetBlock(PhotonMessageInfo info)
    {
        // Only execute if the sender is the current opponent
        Player opponentPlayer = playerManager?.GetOpponentPlayer();
        if (info.Sender != null && opponentPlayer != null && info.Sender == opponentPlayer)
        {
            Debug.Log($"RPC: Received RpcResetMyPetBlock from opponent {info.Sender.NickName}. Resetting local pet block.");
            playerManager?.GetHealthManager()?.ResetLocalPetBlockOnly();
        }
        else
        {           
            Debug.LogWarning($"RPC: Received RpcResetMyPetBlock from unexpected sender ({info.Sender?.NickName ?? "null"}) or opponent is null. Ignoring.");
        }
    }

    // --- ADDED: RPC Receiver for Resetting Local Pet Energy Property ---
    [PunRPC]
    private void RpcResetMyPetEnergyProp(int initialEnergy, PhotonMessageInfo info)
    {
        // This is received by the pet owner at the start of the opponent's turn.
        // We only care if the sender is our current opponent.
        Player opponentPlayer = playerManager?.GetOpponentPlayer();
         if (info.Sender != null && opponentPlayer != null && info.Sender == opponentPlayer)
         {
             Debug.Log($"RPC: Received RpcResetMyPetEnergyProp({initialEnergy}) from opponent {info.Sender.NickName}. Setting my pet energy property.");
             ExitGames.Client.Photon.Hashtable petEnergyProp = new ExitGames.Client.Photon.Hashtable
             {
                 { CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP, initialEnergy }
             };
             PhotonNetwork.LocalPlayer.SetCustomProperties(petEnergyProp);
             // UI update happens via OnPlayerPropertiesUpdate
         }
         else
         {
              Debug.LogWarning($"RPC: Received RpcResetMyPetEnergyProp from unexpected sender ({info.Sender?.NickName ?? "null"}) or opponent is null. Ignoring.");
         }
    }

    // --- ADDED: RPC Receiver for Opponent Pet Playing a Card (for incremental energy update) ---
    [PunRPC]
    private void RpcOpponentPlayedCard(int cardCost, PhotonMessageInfo info)
    {
        // Received by the owner of the pet whose turn is being simulated.
        // Check if the sender is our current opponent.
        Player opponentPlayer = playerManager?.GetOpponentPlayer();
        if (info.Sender != null && opponentPlayer != null && info.Sender == opponentPlayer)
        {
            Debug.Log($"RPC: Received RpcOpponentPlayedCard(Cost={cardCost}) from opponent {info.Sender.NickName}. Decrementing my pet energy property.");
            int currentPetEnergy = startingPetEnergy; // Fallback
            if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP, out object energyObj))
            {
                try { currentPetEnergy = (int)energyObj; } catch {}
            }
            int newPetEnergy = Mathf.Max(0, currentPetEnergy - cardCost);
            ExitGames.Client.Photon.Hashtable petEnergyProp = new ExitGames.Client.Photon.Hashtable
            {
                { CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP, newPetEnergy }
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(petEnergyProp);
            // UI update happens via OnPlayerPropertiesUpdate
        }
        else
        {           
            Debug.LogWarning($"RPC: Received RpcOpponentPlayedCard from unexpected sender ({info.Sender?.NickName ?? "null"}) or opponent is null. Ignoring.");
        }
    }

    // --- ADDED: RPC Receiver for Applying DoT to Local Pet ---
    [PunRPC]
    private void RpcApplyDoTToMyPet(int damage, int duration, PhotonMessageInfo info)
    {
        if (info.Sender == null || info.Sender.IsLocal) return; // Ignore messages from self or invalid sender

        // This RPC means "The sender applied DoT to their pet"
        // Apply it to our local simulation of their pet (our opponentPet)
        Debug.Log($"RPC: Received RpcApplyDoTToMyPet(Dmg={damage}, Dur={duration}) from player {info.Sender.NickName}. Applying DoT to OPPONENT pet (local sim).");
        playerManager?.GetHealthManager()?.ApplyDotOpponentPet(damage, duration, originatedFromRPC: true);
    }

    // --- ADDED: RPC Receiver for Applying Crit Buff to Local Pet ---
    [PunRPC]
    private void RpcApplyCritBuffToMyPet(int amount, int duration, PhotonMessageInfo info)
    {
        if (info.Sender == null || info.Sender.IsLocal) return;

        // This RPC means "The sender applied a crit buff to their pet"
        // We need to apply it to our local simulation of their pet (our opponentPet)
        Debug.Log($"RPC: Received RpcApplyCritBuffToMyPet(Amt={amount}, Dur={duration}) from player {info.Sender.NickName}. Applying Crit Buff to OPPONENT pet (local sim).");
        playerManager?.GetHealthManager()?.ApplyCritChanceBuffOpponentPet(amount, duration, originatedFromRPC: true);
    }

    // --- ADDED: RPC Receiver for Applying Status Effect to Local Pet ---
    [PunRPC]
    private void RpcApplyStatusToMyPet(StatusEffectType type, int duration, PhotonMessageInfo info)
    {
        if (info.Sender == null || info.Sender.IsLocal) return;

        // This RPC means "The sender applied a status effect to their pet"
        // Apply it to our local simulation of their pet (our opponentPet)
        Debug.Log($"RPC: Received RpcApplyStatusToMyPet(Type={type}, Dur={duration}) from player {info.Sender.NickName}. Applying Status to OPPONENT pet (local sim).");
        playerManager?.GetStatusEffectManager()?.ApplyStatusEffectOpponentPet(type, duration, originatedFromRPC: true);
    }

    // --- ADDED: RPC Receiver for Applying HoT to Local Pet ---
    [PunRPC]
    private void RpcApplyHoTToMyPet(int amount, int duration, PhotonMessageInfo info)
    {
        if (info.Sender == null || info.Sender.IsLocal) return;

        // This RPC means "The sender applied HoT to their pet"
        // Apply it to our local simulation of their pet (our opponentPet)
        Debug.Log($"RPC: Received RpcApplyHoTToMyPet(Amt={amount}, Dur={duration}) from player {info.Sender.NickName}. Applying HoT to OPPONENT pet (local sim).");
        playerManager?.GetHealthManager()?.ApplyHotOpponentPet(amount, duration, originatedFromRPC: true);
    }

    // --- ADDED: RPC Receiver for Pet Health Change Notification ---
    [PunRPC]
    private void RpcNotifyOpponentOfLocalPetHealthChange(int newPetHealth, PhotonMessageInfo info)
    {
        if (info.Sender == null || info.Sender.IsLocal) return;

        Player opponentPlayer = playerManager?.GetOpponentPlayer();
        if (opponentPlayer != null && info.Sender == opponentPlayer)
        {
            // Store the previous health value to calculate the change
            int oldHealth = playerManager?.GetHealthManager()?.GetOpponentPetHealth() ?? 0;
            
            // This is a direct update from our opponent about their pet health
            Debug.Log($"RPC: Received RpcNotifyOpponentOfLocalPetHealthChange({newPetHealth}) from current opponent {info.Sender.NickName}. Updating opponent pet health in our local simulation.");
            playerManager?.GetHealthManager()?.SetOpponentPetHealth(newPetHealth);
            UpdateHealthUI(); // Update UI to reflect the change
            
            // Calculate the health change and show appropriate number
            int healthChange = newPetHealth - oldHealth;
            if (healthChange != 0)
            {
                DamageNumberManager damageManager = GetDamageNumberManager();
                if (damageManager != null)
                {
                    GameObject opponentPetObject = GetCombatUIManager().GetOpponentPetUIArea();
                    if (healthChange > 0)
                    {
                        // Health increased - show healing
                        damageManager.ShowHealNumber(healthChange, opponentPetObject);
                    }
                    else
                    {
                        // Health decreased - show damage
                        damageManager.ShowDamageNumber(-healthChange, opponentPetObject, false);
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning($"RPC: Received RpcNotifyOpponentOfLocalPetHealthChange from unexpected sender ({info.Sender?.NickName ?? "null"}) or opponent is null. Ignoring.");
        }
    }

    // --- ADDED: RPC Receiver for Healing Local Pet ---
    [PunRPC]
    private void RpcHealMyPet(int healAmount, PhotonMessageInfo info)
    {
        // This RPC is sent *to us* when an *opponent* plays a card that heals *our pet*
        if (info.Sender != null && info.Sender != PhotonNetwork.LocalPlayer)
        {
            Debug.Log($"RPC: Received RpcHealMyPet({healAmount}) from {info.Sender.NickName}. Healing our local pet.");
            // Directly heal our local pet
            playerManager?.HealLocalPet(healAmount);
            
            // Show healing number on this client
            DamageNumberManager damageManager = GetDamageNumberManager();
            if (damageManager != null)
            {
                GameObject ownPetObject = GetCombatUIManager().GetOwnPetUIArea();
                damageManager.ShowHealNumber(healAmount, ownPetObject);
            }
        }
        else
        {
            Debug.LogWarning($"RPC: Received RpcHealMyPet from self or null sender. Ignoring.");
        }
    }

    // --- ADDED: RPC Receiver for Drawing Cards for Local Pet ---
    [PunRPC]
    private void RpcDrawCardsForMyPet(int amount, PhotonMessageInfo info)
    {
        // This RPC is sent *to us* when an *opponent* plays a card that makes *our pet* draw cards
        if (info.Sender != null && info.Sender != PhotonNetwork.LocalPlayer)
        {
            Debug.Log($"RPC: Received RpcDrawCardsForMyPet({amount}) from {info.Sender.NickName}. Drawing cards for our local pet.");
            
            // Get the appropriate opponent player who's fighting our pet
            Player opponentPlayer = playerManager?.GetOpponentPlayer();
            
            // The opponent who sent this RPC should be the one fighting our pet
            if (opponentPlayer != null && info.Sender == opponentPlayer)
            {
                // For each player fighting this player's pet, there should be a local simulation
                // of this player's pet in their game. Draw cards for this simulation.
                PetDeckManager petDeckManager = cardManager?.GetPetDeckManager();
                if (petDeckManager != null)
                {
                    // Draw cards for our opponent pet (which is the sender's copy of our pet)
                    for (int i = 0; i < amount; i++)
                    {
                        petDeckManager.DrawOpponentPetCard();
                    }
                    // Update UI to show the opponent pet's new hand
                    combatUIManager?.UpdateOpponentPetHandUI();
                    Debug.Log($"Drew {amount} cards for local pet simulation in response to RPC.");
                }
                else
                {
                    Debug.LogWarning("Could not draw cards for pet: PetDeckManager is null.");
                }
            }
            else
            {
                Debug.LogWarning($"RPC: Received RpcDrawCardsForMyPet from player who is not our opponent. Ignoring.");
            }
        }
        else
        {
            Debug.LogWarning($"RPC: Received RpcDrawCardsForMyPet from self or null sender. Ignoring.");
        }
    }

    // --- ADDED: RPC Receiver for Discarding Random Cards from Local Pet ---
    [PunRPC]
    private void RpcDiscardRandomCardsFromMyPet(int amount, PhotonMessageInfo info)
    {
        // This RPC is sent *to us* when an *opponent* plays a card that makes *our pet* discard random cards
        if (info.Sender != null && info.Sender != PhotonNetwork.LocalPlayer)
        {
            Debug.Log($"RPC: Received RpcDiscardRandomCardsFromMyPet({amount}) from {info.Sender.NickName}. Discarding random cards from our local pet's hand.");
            
            // Get the appropriate opponent player who's fighting our pet
            Player opponentPlayer = playerManager?.GetOpponentPlayer();
            
            // The opponent who sent this RPC should be the one fighting our pet
            if (opponentPlayer != null && info.Sender == opponentPlayer)
            {
                // For each player fighting this player's pet, there should be a local simulation
                // of this player's pet in their game. Discard cards from this simulation.
                PetDeckManager petDeckManager = cardManager?.GetPetDeckManager();
                if (petDeckManager != null)
                {
                    // Discard random cards from our opponent pet's hand (which is the sender's copy of our pet)
                    petDeckManager.DiscardRandomOpponentPetCards(amount);
                    // Update UI to show the opponent pet's new hand
                    combatUIManager?.UpdateOpponentPetHandUI();
                    Debug.Log($"Discarded {amount} random cards from local pet simulation in response to RPC.");
                }
                else
                {
                    Debug.LogWarning("Could not discard cards from pet: PetDeckManager is null.");
                }
            }
            else
            {
                Debug.LogWarning($"RPC: Received RpcDiscardRandomCardsFromMyPet from player who is not our opponent. Ignoring.");
            }
        }
        else
        {
            Debug.LogWarning($"RPC: Received RpcDiscardRandomCardsFromMyPet from self or null sender. Ignoring.");
        }
    }

    public PhotonView GetPhotonView()
    {
        return photonViewComponent;
    }

    #endregion

    #region UI Update Proxy Methods
    
    // These methods proxy UI update calls to the combat manager
    public void UpdateHandUI()
    {
        combatManager.UpdateHandUI();
    }

    public void UpdateDeckCountUI()
    {
        combatManager.UpdateDeckCountUI();
    }

    public void UpdateHealthUI()
    {
        combatManager.UpdateHealthUI();
    }

    public void UpdateEnergyUI()
    {
        combatManager.UpdateEnergyUI();
    }
    
    #endregion

    #region Public Helpers for Manager Classes

    public void DisableEndTurnButton()
    {
        combatManager.DisableEndTurnButton();
    }

    public TextMeshProUGUI GetScoreText()
    {
        return gameStateManager.GetScoreText();
    }

    public GameObject GetPlayerHandPanel()
    {
        return combatManager.GetPlayerHandPanel();
    }

    public GameObject GetCardPrefab()
    {
        return cardPrefab;
    }

    public GameStateManager GetGameStateManager()
    {
        return gameStateManager;
    }

    public PlayerManager GetPlayerManager()
    {
        return playerManager;
    }

    public CardManager GetCardManager()
    {
        return cardManager;
    }
    
    public PhotonManager GetPhotonManager()
    {
        return photonManager;
    }
    
    public CombatManager GetCombatManager()
    {
        return combatManager;
    }
    
    public DraftManager GetDraftManager()
    {
        return draftManager;
    }

    // --- ADDED: Proxy for CombatTurnManager ---
    public CombatTurnManager GetCombatTurnManager()
    {
        return combatManager?.GetCombatTurnManager();
    }
    // --- END ADDED ---

    // --- ADDED: Getter for CombatUIManager ---
    public CombatUIManager GetCombatUIManager()
    {
        // Return the direct instance instead of going through CombatManager
        return combatUIManager;
    }
    // --- END ADDED ---

    public int GetStartingPlayerHealth()
    {
        return startingPlayerHealth;
    }

    public void SetStartingPlayerHealth(int value)
    {
        startingPlayerHealth = value;
    }

    public int GetStartingPetHealth()
    {
        return startingPetHealth;
    }

    public void SetStartingPetHealth(int value)
    {
        startingPetHealth = value;
    }

    public int GetStartingEnergy()
    {
        return startingEnergy;
    }

    public void SetStartingEnergy(int value)
    {
        startingEnergy = value;
    }

    // --- ADDED Getter/Setter for Pet Energy ---
    public int GetStartingPetEnergy()
    {
        return startingPetEnergy;
    }

    public void SetStartingPetEnergy(int value)
    {
        startingPetEnergy = value;
    }
    // --- END ADDED ---

    // --- ADDED: Getter for Target Score ---
    public int GetTargetScoreToWin()
    {
        return targetScoreToWin;
    }
    // --- END ADDED ---

    // --- ADDED Getter for Deck Viewer Prefab ---
    public GameObject GetDeckViewerPanelPrefab()
    {
        return deckViewerPanelPrefab;
    }
    // --- END ADDED Getter ---

    // Added: Getter for the CombatCalculator
    public CombatCalculator GetCombatCalculator()
    {
        return combatCalculator;
    }

    // --- ADDED: Getter for Card Preview Calculator ---
    public CardPreviewCalculator GetCardPreviewCalculator()
    {
        return cardPreviewCalculator;
    }
    // --- END ADDED ---

    // --- ADDED: Getter for debug mode ---
    public bool IsDebugCardReuseMode()
    {
        return debugCardReuseMode;
    }

    // --- ADDED: Getter for enemy test mode ---
    public bool IsDebugEnemyTestMode()
    {
        return debugEnemyTestMode;
    }

    // --- ADDED: Method to reset all status effects ---
    public void ResetAllStatusEffects(bool fromRPC = false)
    {
        if (playerManager == null) return;

        Debug.Log("Resetting all status effects for all entities");
        
        // Reset player status effects
        StatusEffectManager statusEffectManager = playerManager.GetStatusEffectManager();
        HealthManager healthManager = playerManager.GetHealthManager();
        
        if (statusEffectManager != null)
        {
            // Reset combo count
            statusEffectManager.ResetComboCount();
            
            // Reset player status effects
            statusEffectManager.ResetAllPlayerStatusEffects();
            statusEffectManager.ResetAllLocalPetStatusEffects();
            statusEffectManager.ResetAllOpponentPetStatusEffects();
        }
        
        if (healthManager != null)
        {
            // Reset DoT/HoT effects
            healthManager.ResetAllDoTEffects();
            healthManager.ResetAllHoTEffects();
            
            // Reset block
            healthManager.ResetAllBlock();
            
            // Reset crit buffs
            healthManager.ResetAllCritBuffs();
        }
        
        // Update UI
        UpdateHealthUI();
        
        // Synchronize across network if not already triggered by an RPC
        if (PhotonNetwork.InRoom && !fromRPC)
        {
            Debug.Log("Broadcasting status reset to all other players");
            photonViewComponent.RPC("RpcResetAllStatusEffects", RpcTarget.Others);
        }
    }
    
    // --- ADDED: RPC handler for status reset ---
    [PunRPC]
    private void RpcResetAllStatusEffects()
    {
        Debug.Log("Received RPC to reset all status effects");
        ResetAllStatusEffects(true); // Pass true to avoid infinite RPC loop
        
        // Show on-screen notification
        if (combatUIManager != null)
        {
            combatUIManager.ShowDebugModeStatus("Status Effects Reset (Network)", true);
        }
    }

    // Method to register DamageNumberManager with GameManager
    public void RegisterDamageNumberManager(DamageNumberManager manager)
    {
        this.damageNumberManager = manager;
        Debug.Log("DamageNumberManager registered with GameManager");
    }
    
    // Getter for DamageNumberManager
    public DamageNumberManager GetDamageNumberManager()
    {
        return damageNumberManager;
    }

    #endregion
}