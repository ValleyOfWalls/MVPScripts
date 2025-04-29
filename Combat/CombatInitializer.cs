using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using System.Linq;

namespace Combat
{
    // This class initializes combat when the Start Game button is pressed
    public class CombatInitializer : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatManager combatManager;
        
        // Singleton instance for easy access
        public static CombatInitializer Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            
            // Find combat manager if not set
            if (combatManager == null)
                combatManager = FindFirstObjectByType<CombatManager>();
        }
        
        // This method is called by the LobbyManager when Start Game is pressed
        [Server]
        public void StartCombat()
        {
            Debug.Log("[Combat] === Combat Initialization Started ===");
            
            if (combatManager == null)
            {
                Debug.LogError("CombatManager not found!");
                return;
            }
            
            // Get all players from the NetworkPlayer class
            List<NetworkPlayer> players = new List<NetworkPlayer>(NetworkPlayer.Players);
            
            Debug.Log($"[Combat] Found {players.Count} players ready for combat");
            foreach (var player in players)
            {
                Debug.Log($"[Combat] - Player: {player.GetSteamName()} ready for combat");
            }
            
            if (players.Count < 2)
            {
                Debug.LogWarning("Not enough players to start combat (minimum 2 required)");
                return;
            }
            
            // Log combat systems being initialized
            Debug.Log("[Combat] Current deployed functionality:");
            Debug.Log("[Combat] - Player and Pet spawning");
            Debug.Log("[Combat] - Card drawing and hand management");
            Debug.Log("[Combat] - Turn-based combat system");
            Debug.Log("[Combat] - Energy system for card costs");
            Debug.Log("[Combat] - Combat UI with player stats");
            Debug.Log("[Combat] - Card playing mechanics");
            Debug.Log("[Combat] - Basic combat flow (player turn -> opponent pet turn)");
            
            // Start combat with all players
            combatManager.StartCombatForPlayers(players);
            
            Debug.Log("[Combat] === Combat Initialization Completed ===");
        }
        
        // Connect to the start game button in the lobby
        public void Initialize()
        {
            // Find the LobbyManager and hook into the StartGame method
            LobbyManager lobbyManager = FindFirstObjectByType<LobbyManager>();
            if (lobbyManager != null)
            {
                // Hook into the RpcStartGame by modifying GameManager
                ModifyGameManagerForCombat();
            }
            else
            {
                Debug.LogError("LobbyManager not found when initializing CombatInitializer");
            }
        }
        
        // Modify the GameManager to intercept the combat state change
        private void ModifyGameManagerForCombat()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                // The GameManager already handles the state change to Combat when 
                // LobbyManager.RpcStartGame() is called. We'll just make sure our combat 
                // is started when the game state changes to Combat.
                
                // We could use an event or hook into SetGameState, but for simplicity
                // we'll just check the state in Update
                enabled = true;
            }
            else
            {
                Debug.LogError("GameManager not found when initializing CombatInitializer");
            }
        }
        
        // Track when game state changes to Combat
        private bool combatStarted = false;
        
        private void Update()
        {
            if (combatStarted) return;
            
            // Check if the game state is Combat
            if (GameManager.Instance != null && 
                GameManager.Instance.CurrentGameState == GameState.Combat)
            {
                if (IsServerStarted) 
                {
                    // Start combat on server
                    StartCombat();
                }
                
                combatStarted = true;
                enabled = false; // No need to keep checking once combat has started
            }
        }

        // This method is called by the LobbyManager when Start Game is pressed
        [Server]
        public void StartCombatForPlayers()
        {
            Debug.Log("[Combat] === Combat Initialization Started (via direct call) ===");
            
            // Use the class field, ensure it's assigned in Awake
            if (this.combatManager == null)
            {
                Debug.LogError("CombatManager field is null in CombatInitializer!");
                // Attempt to find it again
                this.combatManager = FindFirstObjectByType<CombatManager>(); 
                if (this.combatManager == null)
                {
                    Debug.LogError("Could not find CombatManager!");
                    return;
                }
            }
            
            // Pass the list of players in the lobby to start combat
            List<NetworkPlayer> playersInLobby = NetworkPlayer.Players.Where(p => p != null).ToList();
            this.combatManager.StartCombatForPlayers(playersInLobby);
            
            Debug.Log("[Combat] === Combat Initialization Completed (via direct call) ===");
        }
    }
} 