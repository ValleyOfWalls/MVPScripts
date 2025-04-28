// This class can be placed at the bottom of LobbyManager.cs or in its own file
[System.Serializable]
public class PlayerInfo
{
    public int ConnectionId;
    public string PlayerName;
    public bool IsReady;
}