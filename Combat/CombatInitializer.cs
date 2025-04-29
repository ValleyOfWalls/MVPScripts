using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;

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
            if (combatManager == null)
            {
                Debug.LogError("CombatManager not found!");
                return;
            }
            
            // Get all players from the NetworkPlayer class
            List<NetworkPlayer> players = new List<NetworkPlayer>(NetworkPlayer.Players);
            
            if (players.Count < 2)
            {
                Debug.LogWarning("Not enough players to start combat (minimum 2 required)");
                return;
            }
            
            // Start combat with all players
            combatManager.StartCombat(players);
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
    }
} 