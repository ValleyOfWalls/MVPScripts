using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.UI;  // Added for Button references

public class PhotonManager : MonoBehaviourPunCallbacks
{
    private GameManager gameManager;
    private new PhotonView photonView;  // Use 'new' to avoid warning
    
    // Add a constant for the pet name property
    public const string PET_NAME_PROPERTY = "PetName";
    private string pendingPetName;

    public void Initialize(GameManager gameManager)
    {
        this.gameManager = gameManager;
        photonView = gameManager.GetPhotonView();
    }

    // Connection Methods
    public void ConnectToPhoton(string playerName, string petName)
    {
        Debug.Log("Connecting to Photon...");
        
        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = "Player_" + Random.Range(1000, 9999);
        }
        PhotonNetwork.NickName = playerName;
        
        // Store the pet name to set as custom property when joined
        pendingPetName = petName;
        
        gameManager.GetGameStateManager().SetUserInitiatedConnection(true);
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("Already connected to Master, attempting to join/create room...");
            TryJoinGameRoom();
        }
        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public void TryJoinGameRoom()
    {
        Debug.Log("Attempting to JoinRoom(\"asd\")...");
        if (!PhotonNetwork.JoinRoom("asd"))
        {
            Debug.LogError("PhotonNetwork.JoinRoom failed immediately. Check connection and room name format.");
        }
    }

    private void CreateRoom()
    {
        Debug.Log("Creating room 'asd'...");
        RoomOptions roomOptions = new RoomOptions 
        {
            MaxPlayers = 4, 
            PlayerTtl = 60000 // Keep player slot open for 60 seconds (60000ms) after disconnect
        };
        PhotonNetwork.CreateRoom("asd", roomOptions, TypedLobby.Default);
    }

    public void LeaveRoom()
    {
        Debug.Log("Leave Button Clicked - Disconnecting from Photon...");
        PhotonNetwork.Disconnect();
    }

    // PUN Callbacks
    public override void OnConnectedToMaster()
    {
        Debug.Log($"Connected to Master Server. Player Nickname: {PhotonNetwork.NickName}");
        // Automatically join the default lobby after connecting.
        Debug.Log("Connected to Master. Attempting to join default lobby...");
        PhotonNetwork.JoinLobby(); 
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Default Lobby.");
        // Now that we are in the lobby, attempt to join or create the room if connection was user-initiated.
        if (gameManager.GetGameStateManager().IsUserInitiatedConnection())
        {
            Debug.Log("User initiated connection - attempting to join/create room from lobby...");
            TryJoinGameRoom();
        }
        else
        {
             Debug.Log("Joined lobby, but connection wasn't user-initiated. Waiting...");
        }
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined Room: {PhotonNetwork.CurrentRoom.Name}");
        
        gameManager.GetGameStateManager().HideReconnectingUI();
        
        // Set the pet name as a custom property
        if (!string.IsNullOrEmpty(pendingPetName))
        {
            ExitGames.Client.Photon.Hashtable petNameProp = new ExitGames.Client.Photon.Hashtable
            {
                { PET_NAME_PROPERTY, pendingPetName }
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(petNameProp);
            Debug.Log($"Set pet name custom property: {pendingPetName}");
        }
        
        gameManager.GetGameStateManager().SetUserInitiatedConnection(false);
        gameManager.GetGameStateManager().TransitionToLobby();
        gameManager.UpdatePlayerList();
        gameManager.GetPlayerManager().SetPlayerReady(false);
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Left Room");
        gameManager.GetGameStateManager().SetWasInRoom(false);
        gameManager.GetGameStateManager().SetUserInitiatedConnection(false);
        gameManager.GetGameStateManager().SetCurrentState(GameState.Connecting);
        gameManager.GetGameStateManager().ShowStartScreen();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Join Room Failed: ({returnCode}) {message}");
        gameManager.GetGameStateManager().HideReconnectingUI();

        if (returnCode == ErrorCode.GameDoesNotExist)
        {
            Debug.Log("OnJoinRoomFailed reported GameDoesNotExist. Attempting to create the room now.");
            CreateRoom();
        }
        else if (returnCode == ErrorCode.JoinFailedFoundInactiveJoiner)
        {
            Debug.Log("OnJoinRoomFailed reported JoinFailedFoundInactiveJoiner. Attempting to rejoin the room now.");
            if (!PhotonNetwork.RejoinRoom("asd"))
            {
                Debug.LogError("PhotonNetwork.RejoinRoom failed immediately after Join failed. Check connection?");
                gameManager.GetGameStateManager().SetUserInitiatedConnection(false);
                Button connectBtn = gameManager.GetGameStateManager().GetConnectButton();
                if (connectBtn != null) connectBtn.interactable = true;
            }
        }
        else
        {
            Debug.LogWarning($"JoinRoom failed with unhandled error code {returnCode}. Resetting flags and button.");
            gameManager.GetGameStateManager().SetUserInitiatedConnection(false);
            Button connectBtn = gameManager.GetGameStateManager().GetConnectButton();
            if (connectBtn != null) connectBtn.interactable = true;
        }
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Create Room Failed: ({returnCode}) {message}");
        gameManager.GetGameStateManager().SetUserInitiatedConnection(false);
        Button connectBtn = gameManager.GetGameStateManager().GetConnectButton();
        if (connectBtn != null) connectBtn.interactable = true;
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"Disconnected from Photon: {cause}");
        
        // Check if the disconnect cause is potentially recoverable and we were in a room
        if (ShouldAttemptReconnect(cause) && gameManager.GetGameStateManager().WasInRoomWhenDisconnected())
        {
            Debug.Log("Attempting to ReconnectAndRejoin...");
            gameManager.GetGameStateManager().ShowReconnectingUI(); // Show some UI indication
            if (!PhotonNetwork.ReconnectAndRejoin())
            {
                Debug.LogError("ReconnectAndRejoin failed immediately. Likely no previous room or connection issue.");
                gameManager.GetGameStateManager().HandleFullDisconnect();
            }
        }
        else
        {
            gameManager.GetGameStateManager().HandleFullDisconnect();
        }
    }
    
    // Helper function to check if reconnection should be attempted
    private bool ShouldAttemptReconnect(DisconnectCause cause)
    {
        switch (cause)
        {
            case DisconnectCause.Exception:
            case DisconnectCause.ServerTimeout:
            case DisconnectCause.ClientTimeout:
            case DisconnectCause.DisconnectByServerLogic:
            case DisconnectCause.AuthenticationTicketExpired:
            case DisconnectCause.DisconnectByServerReasonUnknown:
                return true;
            // Add other cases if needed, e.g., specific OpResponse errors
            default:
                return false; // Explicit leave, connection errors, etc.
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player {newPlayer.NickName} entered room.");
        if (gameManager.GetGameStateManager().GetLobbyInstance() != null && 
            gameManager.GetGameStateManager().GetLobbyInstance().activeSelf)
        {
            gameManager.UpdatePlayerList();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player {otherPlayer.NickName} left room.");
        if (gameManager.GetGameStateManager().GetLobbyInstance() != null && 
            gameManager.GetGameStateManager().GetLobbyInstance().activeSelf)
        {
            gameManager.UpdatePlayerList();
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        Debug.Log($"Player {targetPlayer.NickName} properties updated. CurrentState: {gameManager.GetGameStateManager().GetCurrentState()}");
        
        if (gameManager.GetGameStateManager().GetCurrentState() == GameState.Lobby && 
            changedProps.ContainsKey(PlayerManager.PLAYER_READY_PROPERTY))
        {
            if (gameManager.GetGameStateManager().GetLobbyInstance() != null && 
                gameManager.GetGameStateManager().GetLobbyInstance().activeSelf)
                gameManager.UpdatePlayerList();
        }
        else if (gameManager.GetGameStateManager().GetCurrentState() == GameState.Combat && 
                 changedProps.ContainsKey(PlayerManager.COMBAT_FINISHED_PROPERTY))
        {
            Debug.Log($"Property update for {PlayerManager.COMBAT_FINISHED_PROPERTY} received from {targetPlayer.NickName}");
            if (PhotonNetwork.IsMasterClient)
            {
                gameManager.GetPlayerManager().CheckForAllPlayersFinishedCombat();
            }
        }
        else if (changedProps.ContainsKey(PlayerManager.PLAYER_SCORE_PROP))
        {
            Debug.Log($"Score property updated for {targetPlayer.NickName}. Refreshing Score UI.");
            gameManager.GetPlayerManager().UpdateScoreUI();
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        Debug.Log($"Room properties updated. CurrentState: {gameManager.GetGameStateManager().GetCurrentState()}");

        // Master Client check: If waiting to send StartCombat RPC and pairings are now set
        if (PhotonNetwork.IsMasterClient && gameManager.GetGameStateManager().IsWaitingToStartCombatRPC() && 
            propertiesThatChanged.ContainsKey(PlayerManager.COMBAT_PAIRINGS_PROP))
        {
            Debug.Log("Master Client confirms pairings property set. Sending RpcStartCombat.");
            gameManager.GetGameStateManager().SetWaitingToStartCombatRPC(false);
            photonView.RPC("RpcStartCombat", RpcTarget.All);
        }

        if (gameManager.GetGameStateManager().GetCurrentState() == GameState.Drafting)
        {
            gameManager.GetDraftManager().HandleRoomPropertiesUpdate(propertiesThatChanged);
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"New Master Client: {newMasterClient.NickName}");
        if (gameManager.GetGameStateManager().GetLobbyInstance() != null && 
            gameManager.GetGameStateManager().GetLobbyInstance().activeSelf)
        {
            gameManager.UpdateLobbyControls();
        }
    }
    
    // RPC Wrappers
    public void SendRPCStartCombat()
    {
        photonView.RPC("RpcStartCombat", RpcTarget.All);
    }
    
    public void SendRPCStartDraft()
    {
        photonView.RPC("RpcStartDraft", RpcTarget.All);
    }
    
    public void SendRPCTakePetDamage(Player targetPlayer, int damageAmount)
    {
        photonView.RPC("RpcTakePetDamage", targetPlayer, damageAmount);
    }
    
    public void SendRPCPlayerPickedOption(int optionId)
    {
        photonView.RPC("RpcPlayerPickedOption", RpcTarget.MasterClient, optionId);
    }
}