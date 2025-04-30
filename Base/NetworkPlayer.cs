using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using Steamworks;
using Combat; // Add the Combat namespace for Deck and DeckManager access

public class NetworkPlayer : NetworkBehaviour
{
    // Static collection of all players
    public static readonly List<NetworkPlayer> Players = new List<NetworkPlayer>();
    
    [Header("Steam Identity")]
    private readonly SyncVar<string> _steamName = new SyncVar<string>("");
    private readonly SyncVar<ulong> _steamId = new SyncVar<ulong>(0);
    
    // Persistent deck - synced across the network
    [Header("Player Deck")]
    public readonly SyncList<string> persistentDeckCardIDs = new SyncList<string>();
    private bool deckInitialized = false;

    // Pet reference - synced across the network
    [Header("Pet")]
    public readonly SyncVar<Pet> playerPet = new SyncVar<Pet>();
    private bool petInitialized = false;
    
    // Properties
    public string SteamName => _steamName.Value;
    public ulong SteamID => _steamId.Value;
    
    private void Awake()
    {
        // Handle any non-network initialization
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();
        Players.Add(this); // Add server instance to list
        
        // Initialize player's deck if needed
        if (!deckInitialized)
        {
            InitializePlayerDeck();
        }
        
        // Initialize player's pet if needed
        if (!petInitialized)
        {
            InitializePlayerPet();
        }
        
        // If GameManager exists, update UI
        if (GameManager.Instance != null && LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdatePlayerList();
            LobbyManager.Instance.UpdateLobbyControls();
        }
    }
    
    public override void OnStopServer()
    {
        base.OnStopServer();
        Players.Remove(this); // Clean up when player leaves
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Update UI if owner
        if (IsOwner && LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdatePlayerList();
            LobbyManager.Instance.UpdateLobbyControls();
        }
    }
    
    [Server]
    private void InitializePlayerPet()
    {
        if (playerPet.Value != null || petInitialized)
            return;
            
        // Create a new pet for this player using the prefab from GameManager
        GameObject petObj = null;
        
        if (GameManager.Instance != null && GameManager.Instance.PetPrefab != null)
        {
            petObj = Instantiate(GameManager.Instance.PetPrefab);
            Debug.Log("[NetworkPlayer] Instantiating pet from GameManager.Instance.PetPrefab");
        }
        else
        {
            Debug.LogError("[NetworkPlayer] Cannot create pet: GameManager instance or PetPrefab is null!");
            // Optional: Fallback to Resources.Load if necessary
            // GameObject petPrefab = Resources.Load<GameObject>("Prefabs/Pet");
            // if (petPrefab != null) petObj = Instantiate(petPrefab);
            // else return;
            return; // Exit if prefab is not available
        }
        
        if (petObj != null)
        {
            Pet pet = petObj.GetComponent<Pet>();
            if (pet != null)
            {
                Spawn(petObj);
                pet.Initialize(this);
                playerPet.Value = pet;
                petInitialized = true;
                Debug.Log($"[NetworkPlayer] Created persistent pet for {GetSteamName()}");
            }
            else
            {
                Debug.LogError("[NetworkPlayer] Created pet object has no Pet component");
                Destroy(petObj);
            }
        }
    }
    
    // Steam ID and name methods
    [Server]
    public void SetSteamInfo(ulong steamId, string steamName)
    {
        _steamId.Value = steamId;
        _steamName.Value = steamName;
        Debug.Log($"Player {Owner.ClientId} set Steam info: {steamId} ({steamName})");
    }
    
    // Add a player to the list
    public static void AddPlayer(NetworkPlayer player)
    {
        if (!Players.Contains(player))
        {
            Players.Add(player);
        }
    }
    
    // Remove a player from the list
    public static void RemovePlayer(NetworkPlayer player)
    {
        Players.Remove(player);
    }
    
    // Get a formatted string with the player's name and ID
    public string GetDisplayName()
    {
        return $"{SteamName} (ID: {Owner.ClientId})";
    }
    
    #region Client and Host Methods
    public string GetSteamName()
    {
        return string.IsNullOrEmpty(SteamName) ? "Player" : SteamName;
    }
    #endregion
    
    #region Callbacks
    private void OnSteamNameChanged(string oldValue, string newValue, bool asServer)
    {
        Debug.Log($"OnSteamNameChanged: Player ID {SteamID} changed name from '{oldValue}' to '{newValue}'. IsServer: {asServer}");
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdatePlayerList();
        }
    }
    #endregion

    #region Player Deck Methods
    [Server]
    public void InitializePlayerDeck()
    {
        // Only initialize on the server and if not already initialized
        if (!IsServerInitialized || deckInitialized)
            return;
            
        // Check if DeckManager exists
        if (DeckManager.Instance == null)
        {
            Debug.LogError($"[NetworkPlayer] Cannot initialize deck for {GetSteamName()} - DeckManager.Instance is null!");
            return;
        }
        
        // Clear existing deck (if any)
        persistentDeckCardIDs.Clear();
        
        // Get the starter deck from DeckManager
        Deck starterDeck = DeckManager.Instance.GetStarterDeckTemplate();
        
        if (starterDeck == null)
        {
            Debug.LogError($"[NetworkPlayer] Failed to get starter deck template from DeckManager for {GetSteamName()}");
            return;
        }
        
        // Add each card from the starter deck to the persistent deck by name/ID
        foreach (CardData card in starterDeck.Cards)
        {
            if (card != null)
            {
                persistentDeckCardIDs.Add(card.cardName); // Using card name as ID
                Debug.Log($"[NetworkPlayer] Added card {card.cardName} to {GetSteamName()}'s persistent deck");
            }
        }
        
        deckInitialized = true;
        Debug.Log($"[NetworkPlayer] Initialized persistent deck for {GetSteamName()} with {persistentDeckCardIDs.Count} cards");
    }
    
    // Helper method to get the number of cards in the persistent deck
    public int GetPersistentDeckCount()
    {
        return persistentDeckCardIDs.Count;
    }
    #endregion

    // Combat-related methods can be added here as needed
}