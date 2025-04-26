using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq;

public class LobbyManager
{
    private GameManager gameManager;
    
    // Player list management
    private List<GameObject> playerListEntries = new List<GameObject>();
    
    // Constants
    public const string PLAYER_READY_PROPERTY = "IsReady";
    
    private string localPetName = "MyPet";
    
    public LobbyManager(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public void Initialize()
    {
        // Clear any existing player list entries
        playerListEntries.Clear();
    }
    
    public void UpdatePlayerList(GameObject playerListPanel, GameObject playerEntryTemplate)
    {
        if (playerListPanel == null || playerEntryTemplate == null) return;
        
        foreach (GameObject entry in playerListEntries) 
            Object.Destroy(entry);
        
        playerListEntries.Clear();

        // --- ADDED: Log current player list state ---
        // Debug.Log($"UpdatePlayerList called. Players in PhotonNetwork.PlayerList ({PhotonNetwork.PlayerList.Length}): {string.Join(", ", PhotonNetwork.PlayerList.Select(p => p.NickName))}"); // Log original list
        // --- END ADDED ---

        // --- MODIFIED: Filter for active players only --- 
        var activePlayers = PhotonNetwork.PlayerList.Where(p => !p.IsInactive).ToList();
        Debug.Log($"UpdatePlayerList called. Active Players ({activePlayers.Count}): {string.Join(", ", activePlayers.Select(p => p.NickName))}");
        // --- END MODIFIED ---

        // --- MODIFIED: Iterate through active players --- 
        foreach (Player player in activePlayers.OrderBy(p => p.ActorNumber))
        {
            GameObject newEntry = Object.Instantiate(playerEntryTemplate, playerListPanel.transform);
            TMPro.TextMeshProUGUI textComponent = newEntry.GetComponent<TMPro.TextMeshProUGUI>();
            string readyStatus = "";
            if (player.CustomProperties.TryGetValue(PLAYER_READY_PROPERTY, out object isReadyStatus))
                readyStatus = (bool)isReadyStatus ? " <color=green>(Ready)</color>" : "";
            
            textComponent.text = $"{player.NickName}{(player.IsMasterClient ? " (Host)" : "")}{readyStatus}";
            newEntry.SetActive(true);
            playerListEntries.Add(newEntry);
        }
    }
    
    public void SetPlayerReady(bool isReady)
    {
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable {{ PLAYER_READY_PROPERTY, isReady }};
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }
    
    public bool CheckAllPlayersReady()
    {
        // --- MODIFIED: Filter for active players and check count/readiness --- 
        var activePlayers = PhotonNetwork.PlayerList.Where(p => !p.IsInactive).ToList();
        
        return activePlayers.Count >= 2 && // Need at least 2 *active* players
               activePlayers.All(p => 
                   p.CustomProperties.TryGetValue(PLAYER_READY_PROPERTY, out object isReady) && (bool)isReady);
        // --- END MODIFIED ---
    }
    
    #region Pet Name Methods
    
    public void SetLocalPetName(string name)
    {
        localPetName = name;
    }
    
    public string GetLocalPetName()
    {
        return localPetName;
    }
    
    #endregion
}