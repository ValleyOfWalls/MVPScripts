using UnityEngine;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Connection;
using DG.Tweening;
using System.Linq;

namespace Combat
{
    public class CombatManager : NetworkBehaviour
    {
        public static CombatManager Instance { get; private set; }
        
        [Header("Prefabs")]
        [SerializeField] private GameObject combatPlayerPrefab;
        [SerializeField] private GameObject petPrefab;
        [SerializeField] private GameObject playerHandPrefab;
        [SerializeField] private GameObject combatRootPrefab;
        
        [Header("References")]
        [SerializeField] private Transform petSpawnPointsParent;
        [SerializeField] private Transform playerSpawnPointsParent;
        [SerializeField] private GameObject combatCanvas;
        
        // List of active combats
        private readonly Dictionary<NetworkPlayer, CombatData> activeCombats = new Dictionary<NetworkPlayer, CombatData>();
        private readonly List<NetworkPlayer> playersInCombat = new List<NetworkPlayer>();
        private bool combatStarted = false;
        private readonly Dictionary<NetworkPlayer, Transform> playerCombatRoots = new Dictionary<NetworkPlayer, Transform>(); // Added parent storage
        
        // Awake is called when the script instance is being loaded
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
        }
        
        // When combat manager starts on server
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Initialize any server-side components
            activeCombats.Clear();
            playersInCombat.Clear();
            combatStarted = false;
        }
        
        // Start combat for all players
        [Server]
        public void StartCombat(List<NetworkPlayer> players)
        {
            if (combatStarted)
            {
                Debug.LogWarning("Combat already started!");
                return;
            }
            
            if (players.Count < 2)
            {
                Debug.LogWarning("Not enough players to start combat!");
                return;
            }
            
            Debug.Log($"Starting combat with {players.Count} players");
            
            // Set state
            combatStarted = true;
            playersInCombat.AddRange(players);
            playerCombatRoots.Clear(); // Clear roots before combat
            
            // --- Step 1: Create Parents and Spawn all Pets --- 
            Dictionary<NetworkPlayer, Pet> allPlayerPets = new Dictionary<NetworkPlayer, Pet>();
            foreach (NetworkPlayer player in players)
            {
                // Instantiate the CombatRootPrefab
                GameObject playerRootObj = Instantiate(combatRootPrefab);
                // Rename for clarity in hierarchy
                playerRootObj.name = $"Player_{player.Owner.ClientId}_CombatRoot"; 
                // NetworkObject is already on the prefab, just spawn it with ownership
                Spawn(playerRootObj, player.Owner);
                
                playerCombatRoots[player] = playerRootObj.transform; // Store the parent transform
                
                // Instantiate pet under the player's root
                GameObject playerPetObj = Instantiate(petPrefab, playerCombatRoots[player]);
                Pet playerPet = playerPetObj.GetComponent<Pet>();
                Spawn(playerPetObj, player.Owner); // Spawn pet with ownership
                playerPet.Initialize(player);
                allPlayerPets[player] = playerPet;
                Debug.Log($"Created and spawned pet for {player.GetSteamName()} under spawned root {playerRootObj.name}"); // Updated log
            }
            
            // --- Step 2: Create Pet Matchups --- 
            List<NetworkPlayer> shuffledPlayers = new List<NetworkPlayer>(players);
            ShuffleList(shuffledPlayers);
            Dictionary<NetworkPlayer, NetworkPlayer> petAssignments = new Dictionary<NetworkPlayer, NetworkPlayer>();
            for (int i = 0; i < shuffledPlayers.Count; i++)
            {
                NetworkPlayer player = shuffledPlayers[i];
                NetworkPlayer opponentOwner = shuffledPlayers[(i + 1) % shuffledPlayers.Count];
                petAssignments[player] = opponentOwner;
            }
            
            // --- Step 3: Create CombatPlayers, PlayerHands, and link Pets --- 
            foreach (NetworkPlayer player in players)
            {
                NetworkPlayer opponentPetOwner = petAssignments[player];
                
                // Retrieve existing pets
                Pet playerPet = allPlayerPets[player];
                Pet opponentPet = allPlayerPets[opponentPetOwner];
                
                // Retrieve the player's combat root
                Transform playerRoot = playerCombatRoots[player];
                
                // Create combat player under the player's root
                GameObject combatPlayerObj = Instantiate(combatPlayerPrefab, playerRoot);
                CombatPlayer combatPlayer = combatPlayerObj.GetComponent<CombatPlayer>();
                Spawn(combatPlayerObj, player.Owner); // Assign ownership
                
                // Create player hand under the player's root
                GameObject playerHandObj = Instantiate(playerHandPrefab, playerRoot);
                PlayerHand playerHand = playerHandObj.GetComponent<PlayerHand>();
                Spawn(playerHandObj, player.Owner); // Assign ownership
                
                // Initialize CombatPlayer with correct Pet references
                combatPlayer.Initialize(player, playerPet, opponentPet, this);
                
                // Initialize PlayerHand (already done in CombatPlayer.Initialize if needed)
                // playerHand.Initialize(player, combatPlayer); 
                
                // Store combat data
                activeCombats[player] = new CombatData
                {
                    CombatPlayer = combatPlayer,
                    PlayerPet = playerPet, // Use retrieved pet
                    OpponentPet = opponentPet, // Use retrieved pet
                    OpponentPlayer = opponentPetOwner,
                    PlayerHand = playerHand,
                    TurnCompleted = false
                    // CombatComplete defaults to false
                };
            }
            
            // Position all objects on clients
            PositionCombatObjects();
            
            // Show the combat UI on all clients
            RpcShowCombatCanvas();
            
            // Start the combat for all players
            foreach (NetworkPlayer player in players)
            {
                // Start the player's turn
                if (activeCombats.TryGetValue(player, out CombatData combatData))
                {
                    combatData.CombatPlayer.StartTurn();
                }
            }
        }
        
        // Position all combat objects correctly
        [Server]
        private void PositionCombatObjects()
        {
            // This would normally use spawn points but we'll just use RPC to position them
            RpcPositionCombatObjects(activeCombats.Keys.ToArray());
        }
        
        // Called when a player ends their turn
        [Server]
        public void PlayerEndedTurn(CombatPlayer player)
        {
            // Find the player that owns this combat player
            NetworkPlayer networkPlayer = null;
            foreach (var kvp in activeCombats)
            {
                if (kvp.Value.CombatPlayer == player)
                {
                    networkPlayer = kvp.Key;
                    break;
                }
            }
            
            if (networkPlayer == null)
            {
                Debug.LogError("Could not find network player for combat player");
                return;
            }
            
            // Mark turn as completed
            activeCombats[networkPlayer].TurnCompleted = true;
            
            // Start opponent pet's turn (Slay the Spire style)
            PerformOpponentPetTurn(networkPlayer);
        }
        
        // Perform the opponent pet's turn
        [Server]
        private void PerformOpponentPetTurn(NetworkPlayer player)
        {
            if (!activeCombats.TryGetValue(player, out CombatData combatData))
                return;
            
            // Get the opponent pet
            Pet opponentPet = combatData.OpponentPet;
            Pet playerPet = combatData.PlayerPet;
            
            if (opponentPet.IsDefeated() || playerPet.IsDefeated())
            {
                // Combat already ended for this player
                CheckCombatEndState();
                return;
            }
            
            // Simple AI for opponent pet - just attack the player's pet
            int attackDamage = Random.Range(5, 16);
            
            // Notify the player that opponent pet is attacking
            RpcShowOpponentAttacking(player.Owner, attackDamage);
            
            // Apply the damage after a delay using DOVirtual.DelayedCall
            DOVirtual.DelayedCall(1.5f, () => 
            {
                // Ensure the combat context is still valid before executing
                if (activeCombats.ContainsKey(player))
                {
                    CompleteOpponentAttack(player, attackDamage);
                }
            });
        }
        
        // Complete the opponent's attack after animation
        [Server]
        private void CompleteOpponentAttack(NetworkPlayer player, int damage)
        {
            if (!activeCombats.TryGetValue(player, out CombatData combatData))
                return;
            
            // Apply damage to player's pet
            combatData.PlayerPet.TakeDamage(damage);
            
            // Check if player's pet is defeated
            if (combatData.PlayerPet.IsDefeated())
            {
                // Player lost the combat
                RpcShowCombatResult(player.Owner, false);
                // Mark combat as complete for this player
                combatData.CombatComplete = true; 
                
                // Check if all combats have ended
                CheckCombatEndState();
            }
            else
            {
                // Start a new turn for the player
                combatData.TurnCompleted = false;
                combatData.CombatPlayer.StartTurn();
            }
        }
        
        // Handle a pet being defeated
        [Server]
        public void HandlePetDefeat(Pet pet)
        {
            // Find the combat data for this pet
            foreach (var kvp in activeCombats)
            {
                if (kvp.Value.PlayerPet == pet)
                {
                    // Player's pet was defeated, they lost
                    RpcShowCombatResult(kvp.Key.Owner, false);
                    kvp.Value.CombatComplete = true;
                }
                else if (kvp.Value.OpponentPet == pet)
                {
                    // Opponent's pet was defeated, player won
                    RpcShowCombatResult(kvp.Key.Owner, true);
                    kvp.Value.CombatComplete = true;
                }
            }
            
            // Check if all combats have ended
            CheckCombatEndState();
        }
        
        // Check if all combats have ended
        [Server]
        private void CheckCombatEndState()
        {
            bool allComplete = true;
            
            foreach (var kvp in activeCombats)
            {
                if (!kvp.Value.CombatComplete)
                {
                    // Still have active combats
                    allComplete = false;
                    break;
                }
            }
            
            if (allComplete)
            {
                // All combats have ended, end the combat phase
                EndCombatPhase();
            }
        }
        
        // End the combat phase
        [Server]
        private void EndCombatPhase()
        {
            Debug.Log("All combats have completed, ending combat phase");
            
            // Clean up combat objects
            foreach (var kvp in activeCombats)
            {
                if (kvp.Value.CombatPlayer != null)
                    Despawn(kvp.Value.CombatPlayer.gameObject);
                
                if (kvp.Value.PlayerPet != null)
                    Despawn(kvp.Value.PlayerPet.gameObject);
                
                if (kvp.Value.OpponentPet != null)
                    Despawn(kvp.Value.OpponentPet.gameObject);
                
                if (kvp.Value.PlayerHand != null)
                    Despawn(kvp.Value.PlayerHand.gameObject);
            }
            
            // Clear combat data
            activeCombats.Clear();
            playersInCombat.Clear();
            combatStarted = false;
            
            // Hide combat UI for all clients
            RpcHideCombatCanvas();
            
            // Return to lobby or game flow state
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Lobby);
            }
        }
        
        #region Utility Methods
        
        // Shuffle a list using Fisher-Yates algorithm
        private void ShuffleList<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Range(0, n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
        
        #endregion
        
        #region RPCs
        
        [ObserversRpc]
        private void RpcShowCombatCanvas()
        {
            if (combatCanvas != null)
            {
                combatCanvas.SetActive(true);
                
                // Add animation to show the canvas
                CanvasGroup canvasGroup = combatCanvas.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0;
                    canvasGroup.DOFade(1, 0.5f);
                }
            }
        }
        
        [ObserversRpc]
        private void RpcHideCombatCanvas()
        {
            if (combatCanvas != null)
            {
                // Add animation to hide the canvas
                CanvasGroup canvasGroup = combatCanvas.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.DOFade(0, 0.5f).OnComplete(() => 
                    {
                        combatCanvas.SetActive(false);
                    });
                }
                else
                {
                    combatCanvas.SetActive(false);
                }
            }
        }
        
        [ObserversRpc]
        private void RpcPositionCombatObjects(NetworkPlayer[] players)
        {
            // This would normally position objects for each player based on their client's view
            // The server has already spawned everything, we just need visual positioning
            
            // In a real implementation, you'd position based on client-specific data
            // using TargetRPCs for each player
        }
        
        [TargetRpc]
        private void RpcShowOpponentAttacking(NetworkConnection conn, int damage)
        {
            // This would show an animation or effect for the opponent attacking
            Debug.Log($"Opponent is attacking for {damage} damage!");
        }
        
        [TargetRpc]
        private void RpcShowCombatResult(NetworkConnection conn, bool victory)
        {
            // This would show the combat result on the client
            string resultMessage = victory ? "Victory!" : "Defeat!";
            Debug.Log(resultMessage);
            
            // In a real implementation, you'd show a UI element with the result
            // and potentially some animations
        }
        
        #endregion
    }
    
    // Class to hold combat-related data for a player
    public class CombatData
    {
        public CombatPlayer CombatPlayer;
        public Pet PlayerPet;
        public Pet OpponentPet;
        public NetworkPlayer OpponentPlayer;
        public PlayerHand PlayerHand;
        
        public bool TurnCompleted;
        public bool CombatComplete;
    }
} 