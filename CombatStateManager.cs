using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Collections.Generic;
using System.Linq; // Add this for ToList() and OrderBy()
using Newtonsoft.Json;

public class CombatStateManager
{
    private GameManager gameManager;
    
    private bool combatEndedForLocalPlayer = false;
    private Player opponentPlayer;
    
    // Constants
    public const string COMBAT_FINISHED_PROPERTY = "CombatFinished";
    public const string PLAYER_SCORE_PROP = "PlayerScore";
    public const string PLAYER_BASE_PET_HP_PROP = "BasePetHP";
    public const string COMBAT_PAIRINGS_PROP = "CombatPairings";
    // --- ADDED: Constants for Other Fights UI ---
    public const string PLAYER_COMBAT_OPP_PET_HP_PROP = "combatOppPetHP";
    public const string PLAYER_COMBAT_TURN_PROP = "combatTurn";
    // --- ADDED: Constant for Player Health in Combat ---
    public const string PLAYER_COMBAT_PLAYER_HP_PROP = "combatPlayerHP";
    // --- ADDED: Constant for Pet Energy in Combat ---
    public const string PLAYER_COMBAT_PET_ENERGY_PROP = "PetEnergy";
    // --- END ADDED ---
    
    // --- ADDED: Player Stat Sync Properties ---
    public const string PLAYER_COMBAT_PLAYER_BLOCK_PROP = "combatPlayerBlock";
    public const string PLAYER_COMBAT_ENERGY_PROP = "combatEnergy";
    public const string PLAYER_COMBAT_PLAYER_WEAK_PROP = "combatPlayerWeak";
    public const string PLAYER_COMBAT_PLAYER_BREAK_PROP = "combatPlayerBreak";
    public const string PLAYER_COMBAT_PLAYER_THORNS_PROP = "combatPlayerThorns";
    public const string PLAYER_COMBAT_PLAYER_STRENGTH_PROP = "combatPlayerStrength";
    public const string PLAYER_COMBAT_PLAYER_DOT_TURNS_PROP = "combatPlayerDotTurns";
    public const string PLAYER_COMBAT_PLAYER_DOT_DMG_PROP = "combatPlayerDotDmg";
    public const string PLAYER_COMBAT_PLAYER_HOT_TURNS_PROP = "combatPlayerHotTurns";
    public const string PLAYER_COMBAT_PLAYER_HOT_AMT_PROP = "combatPlayerHotAmt";
    public const string PLAYER_COMBAT_PLAYER_CRIT_PROP = "combatPlayerCrit"; // Stores total effective crit %
    // --- END ADDED ---
    
    // Add the constant for pet health tracking
    public const string PLAYER_COMBAT_PET_HP_PROP = "combatPetHP";
    
    // --- ADDED: Pet Stat Sync Properties ---
    public const string PLAYER_COMBAT_PET_HOT_TURNS_PROP = "combatPetHotTurns";
    public const string PLAYER_COMBAT_PET_HOT_AMT_PROP = "combatPetHotAmt";
    // Add more pet-specific props here if needed (e.g., Block, Weak, Break, Strength, Crit)
    public const string PLAYER_COMBAT_PET_WEAK_PROP = "combatPetWeakTurns";
    public const string PLAYER_COMBAT_PET_BREAK_PROP = "combatPetBreakTurns";
    public const string PLAYER_COMBAT_PET_THORNS_PROP = "combatPetThornsAmt";
    public const string PLAYER_COMBAT_PET_STRENGTH_PROP = "combatPetStrengthAmt";
    public const string PLAYER_COMBAT_PET_DOT_TURNS_PROP = "combatPetDotTurns";
    public const string PLAYER_COMBAT_PET_DOT_DMG_PROP = "combatPetDotDmg";
    public const string PLAYER_COMBAT_PET_CRIT_PROP = "combatPetCrit"; // Stores total effective crit %
    // --- END ADDED ---
    
    public CombatStateManager(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public void Initialize()
    {
        combatEndedForLocalPlayer = false;
        opponentPlayer = null;
    }
    
    public void InitializeCombatState(int opponentPetOwnerActorNum, int startingPlayerHealth, int startingPetHealth)
    {
        Debug.Log($"Initializing Combat State for round. Fighting pet of ActorNum {opponentPetOwnerActorNum}");
        
        combatEndedForLocalPlayer = false;
        
        // Determine opponent player based on provided ActorNumber
        opponentPlayer = null; // Reset from previous round
        List<string> opponentPetCardNames = new List<string>();
        
        if (opponentPetOwnerActorNum > 0)
        {
            opponentPlayer = PhotonNetwork.CurrentRoom.GetPlayer(opponentPetOwnerActorNum);
        }
        
        if (opponentPlayer == null && opponentPetOwnerActorNum > 0)
        {
            Debug.LogError($"Could not find opponent player with ActorNum {opponentPetOwnerActorNum}!");
        }
        else if (opponentPlayer != null)
        {
            Debug.Log($"Found opponent: {opponentPlayer.NickName}");

            // Get opponent's pet deck card names
            if (opponentPlayer.CustomProperties.TryGetValue(CardManager.PLAYER_PET_DECK_PROP, out object oppPetDeckObj))
            {
                try
                {
                    string petDeckJson = oppPetDeckObj as string;
                    if (!string.IsNullOrEmpty(petDeckJson))
                    {
                        opponentPetCardNames = JsonConvert.DeserializeObject<List<string>>(petDeckJson) ?? new List<string>();
                        Debug.Log($"Retrieved {opponentPetCardNames.Count} pet card names for opponent {opponentPlayer.NickName}.");
                    }
                    else
                    {
                        Debug.LogWarning($"Opponent {opponentPlayer.NickName} has empty {CardManager.PLAYER_PET_DECK_PROP} property.");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to deserialize opponent pet deck JSON for player {opponentPlayer.NickName}: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"Opponent {opponentPlayer.NickName} has no {CardManager.PLAYER_PET_DECK_PROP} property. Using starter deck.");
            }
        }
        else
        {
            Debug.LogWarning("InitializeCombatState called with invalid or missing opponent ActorNum. Setting default opponent pet health.");
        }
        
        // Initialize health for this player via HealthManager
        gameManager.GetPlayerManager().GetHealthManager().InitializeCombatState(startingPetHealth, opponentPlayer);
        
        // Initialize local pet deck via CardManager
        gameManager.GetCardManager().InitializeOpponentPetDeck(opponentPetCardNames);
    }
    
    public void HandleCombatWin()
    {
        if (combatEndedForLocalPlayer) return;
        combatEndedForLocalPlayer = true;
        
        Debug.Log("COMBAT WIN! Player defeated the opponent's pet.");
        
        // Award point using Player Property
        int currentScore = 0;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PLAYER_SCORE_PROP, out object scoreVal))
        {
            currentScore = (int)scoreVal;
        }
        int newScore = currentScore + 1;
        Hashtable scoreUpdate = new Hashtable { { PLAYER_SCORE_PROP, newScore } };
        // Use Check-And-Swap for safety against race conditions
        Hashtable expectedScore = new Hashtable { { PLAYER_SCORE_PROP, currentScore } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(scoreUpdate, expectedScore);
        Debug.Log($"Attempted to set local player score to {newScore} (expected {currentScore}).");
        
        // Mark this player as finished with combat for this round
        SetPlayerCombatFinished(true);
        
        // Disable further actions
        gameManager.DisableEndTurnButton();
    }
    
    public void HandleCombatLoss()
    {
        if (combatEndedForLocalPlayer) return;
        combatEndedForLocalPlayer = true;
        
        Debug.Log("COMBAT LOSS! Player was defeated.");
        
        // Award point to opponent using Player Property
        if (opponentPlayer != null)
        {
            int currentOpponentScore = 0;
            if (opponentPlayer.CustomProperties.TryGetValue(PLAYER_SCORE_PROP, out object scoreVal))
            {
                currentOpponentScore = (int)scoreVal;
            }
            int newOpponentScore = currentOpponentScore + 1;
            Hashtable scoreUpdate = new Hashtable { { PLAYER_SCORE_PROP, newOpponentScore } };
            Hashtable expectedScore = new Hashtable { { PLAYER_SCORE_PROP, currentOpponentScore } };
            opponentPlayer.SetCustomProperties(scoreUpdate, expectedScore);
            Debug.Log($"Attempted to set opponent ({opponentPlayer.NickName}) score to {newOpponentScore} (expected {currentOpponentScore}).");
        }
        else
        {
            Debug.LogError("HandleCombatLoss: Cannot award point, opponentPlayer is null!");
        }
        
        // Mark this player as finished with combat for this round
        SetPlayerCombatFinished(true);
        
        // Disable further actions
        gameManager.DisableEndTurnButton();
    }
    
    public void SetPlayerCombatFinished(bool finished)
    {
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            { COMBAT_FINISHED_PROPERTY, finished }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        Debug.Log($"Set {COMBAT_FINISHED_PROPERTY} to {finished} for local player.");
        
        // Master client should check if all players are finished
        if (PhotonNetwork.IsMasterClient)
        {
            CheckForAllPlayersFinishedCombat();
        }
    }
    
    public void CheckForAllPlayersFinishedCombat()
    {
        if (!PhotonNetwork.IsMasterClient || gameManager.GetGameStateManager().GetCurrentState() != GameState.Combat)
        {
            return; // Only Master Client checks, and only during combat
        }
        
        Debug.Log("Master Client checking if all players finished combat...");
        
        bool allFinished = true;
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            object finishedStatus;
            if (p.CustomProperties.TryGetValue(COMBAT_FINISHED_PROPERTY, out finishedStatus))
            {
                if (!(bool)finishedStatus)
                {
                    allFinished = false;
                    Debug.Log($"Player {p.NickName} has not finished combat yet.");
                    break; // No need to check further
                }
            }
            else
            {
                // Player hasn't set the property yet
                allFinished = false;
                Debug.Log($"Player {p.NickName} property {COMBAT_FINISHED_PROPERTY} not found.");
                break;
            }
        }
        
        if (allFinished)
        {
            Debug.Log("All players have finished combat! Starting Draft Phase.");
            // Reset flags for next round before starting draft
            ResetCombatFinishedFlags();
            // Tell all clients to start the draft phase
            gameManager.GetPhotonView().RPC("RpcStartDraft", RpcTarget.All);
        }
        else
        {
            Debug.Log("Not all players finished combat yet.");
        }
    }
    
    public void ResetCombatFinishedFlags()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            { COMBAT_FINISHED_PROPERTY, false }
        };
        
        // Reset for all players
        foreach(Player p in PhotonNetwork.PlayerList)
        {
            p.SetCustomProperties(props);
        }
        Debug.Log("Reset CombatFinished flags for all players.");
    }
    
    public void PrepareNextCombatRound()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        Debug.Log("Master Client preparing next combat round...");
        
        List<Player> players = PhotonNetwork.PlayerList.ToList();
        // Shuffle players for randomized pairings
        System.Random rng = new System.Random();
        players = players.OrderBy(p => rng.Next()).ToList();
        
        Dictionary<int, int> pairings = new Dictionary<int, int>();
        if (players.Count < 2)
        {
            Debug.LogWarning("Cannot create pairings with less than 2 players.");
        }
        else
        {
            for (int i = 0; i < players.Count; i++)
            {
                Player currentPlayer = players[i];
                Player opponentPetOwner = players[(i + 1) % players.Count]; // Cyclical pairing
                pairings[currentPlayer.ActorNumber] = opponentPetOwner.ActorNumber;
                Debug.Log($"Pairing: {currentPlayer.NickName} vs {opponentPetOwner.NickName}'s Pet");
            }
        }
        
        // Store pairings in room properties
        string pairingsJson = JsonConvert.SerializeObject(pairings);
        Hashtable roomProps = new Hashtable
        {
            { COMBAT_PAIRINGS_PROP, pairingsJson }
        };
        
        // Reset combat finished flags BEFORE setting properties that trigger the next step
        ResetCombatFinishedFlags();
        
        // Set properties
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
        
        // Set flag to wait for OnRoomPropertiesUpdate confirmation
        gameManager.GetGameStateManager().SetWaitingToStartCombatRPC(true);
        Debug.Log($"Master Client set pairings property and isWaitingToStartCombatRPC = true. Waiting for confirmation.");
    }
    
    public void UpdateScoreUI()
    {
        TMPro.TextMeshProUGUI scoreText = gameManager.GetScoreText();
        if (scoreText == null) return;
        
        // Fetch local player's score
        int localScore = 0;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PLAYER_SCORE_PROP, out object localScoreVal))
        {
            try { localScore = (int)localScoreVal; } catch { /* Ignore cast error, keep 0 */ }
        }
        
        // Display only local score
        scoreText.text = $"Score: {localScore}";
    }
    
    #region Getters and Setters
    
    public bool IsCombatEndedForLocalPlayer() => combatEndedForLocalPlayer;
    public void SetCombatEndedForLocalPlayer(bool value) => combatEndedForLocalPlayer = value;
    public Player GetOpponentPlayer() => opponentPlayer;
    public void SetOpponentPlayer(Player player) => opponentPlayer = player;
    
    #endregion
}