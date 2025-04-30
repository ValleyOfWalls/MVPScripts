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
    private readonly SyncVar<string> _syncedPlayerName = new SyncVar<string>(""); // Synced name for hierarchy
    
    // Persistent deck - synced across the network
    [Header("Player Deck")]
    public readonly SyncList<string> persistentDeckCardIDs = new SyncList<string>();
    private bool deckInitialized = false;

    // Pet reference - synced across the network
    [Header("Pet")]
    public readonly SyncVar<Pet> playerPet = new SyncVar<Pet>();
    private readonly SyncVar<string> _syncedPetName = new SyncVar<string>(""); // Synced name for pet hierarchy
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
        
        // Register SyncVar callbacks
        _syncedPlayerName.OnChange += OnPlayerNameChanged;
        _syncedPetName.OnChange += OnPetNameChanged;

        // Apply initial names if already set
        OnPlayerNameChanged(string.Empty, _syncedPlayerName.Value, false);
        OnPetNameChanged(string.Empty, _syncedPetName.Value, false);

        // Update UI if owner
        if (IsOwner && LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UpdatePlayerList();
            LobbyManager.Instance.UpdateLobbyControls();
        }
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();

        // Unregister SyncVar callbacks
        _syncedPlayerName.OnChange -= OnPlayerNameChanged;
        _syncedPetName.OnChange -= OnPetNameChanged;
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
                Debug.Log($"[NetworkPlayer] Created persistent pet for {GetSteamName()} (will be renamed later)");
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

        // RENAME the NetworkPlayer GameObject for clarity in hierarchy (Server Only + SyncVar)
        string uniquePlayerName = string.IsNullOrEmpty(steamName) ? $"Player_{Owner.ClientId}" : steamName;
        uniquePlayerName = GetUniqueGameObjectName(uniquePlayerName, Owner.ClientId);
        gameObject.name = uniquePlayerName; // Set name on server immediately
        _syncedPlayerName.Value = uniquePlayerName; // Sync name to clients
        Debug.Log($"Set NetworkPlayer name to: {uniquePlayerName} (Synced)");

        // RENAME the Pet GameObject now that we have the name (Server Only + SyncVar)
        if (playerPet.Value != null)
        {
            string uniquePetName = string.IsNullOrEmpty(steamName) ? "Player's Pet" : $"{steamName}'s Pet";
            uniquePetName = GetUniqueGameObjectName(uniquePetName, Owner.ClientId); 
            playerPet.Value.gameObject.name = uniquePetName; // Set name on server immediately
            _syncedPetName.Value = uniquePetName; // Sync name to clients
            Debug.Log($"Set Pet object name to: {uniquePetName} (Synced)");
        }
        else
        {
            Debug.LogWarning($"SetSteamInfo: Could not rename pet for {steamName}, playerPet SyncVar is null.");
        }
    }
    
    // Helper to ensure unique GameObject names (simple approach)
    private string GetUniqueGameObjectName(string baseName, int uniqueId)
    {
        int count = 0;
        string finalName = baseName;
        // Check against all GameObjects in the scene (can be slow in large scenes)
        // A more optimized approach might track NetworkPlayer names specifically
        while (GameObject.Find(finalName) != null && GameObject.Find(finalName) != gameObject) // Check if name exists AND it's not this object
        {
            count++;
            finalName = $"{baseName}_{uniqueId}"; // Use ClientId for uniqueness on first collision
            if (count > 1) // Add counter for subsequent collisions
            {
                 finalName = $"{baseName}_{uniqueId}_{count}";
            }
            if (count > 10) { // Safety break
                 Debug.LogError($"Could not find unique name for {baseName} after {count} tries!");
                 return $"{baseName}_{System.Guid.NewGuid()}"; // Use GUID as last resort
            }
        }
        return finalName;
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
    // Called on CLIENT when synced name changes
    private void OnPlayerNameChanged(string prevName, string newName, bool asServer)
    {
        if (asServer) return;
        if (!string.IsNullOrEmpty(newName))
        {
             gameObject.name = newName;
             // Optional: Add a small log for debugging client-side rename
             // Debug.Log($"[Client] NetworkPlayer GameObject renamed to: {newName}");
        }
    }

    // Called on CLIENT when synced pet name changes
    private void OnPetNameChanged(string prevName, string newName, bool asServer)
    {
         if (asServer) return;
         if (!string.IsNullOrEmpty(newName) && playerPet.Value != null)
         {
             playerPet.Value.gameObject.name = newName;
             // Optional: Add a small log for debugging client-side rename
             // Debug.Log($"[Client] Pet GameObject renamed to: {newName}");
         }
    }

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