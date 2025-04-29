using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Connection;
using DG.Tweening;
using System.Linq;
using System.Reflection;

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
        [SerializeField] private Transform playerHandArea;
        [SerializeField] private Transform petHandArea;
        [SerializeField] private PlayerHand playerHand;
        [SerializeField] private PlayerHand petHand;
        
        // List of active combats
        private readonly Dictionary<NetworkPlayer, CombatData> activeCombats = new Dictionary<NetworkPlayer, CombatData>();
        private readonly List<NetworkPlayer> playersInCombat = new List<NetworkPlayer>();
        private bool combatStarted = false;
        private readonly Dictionary<NetworkPlayer, Transform> playerCombatRoots = new Dictionary<NetworkPlayer, Transform>(); // Added parent storage
        
        // Runtime decks
        private RuntimeDeck playerDeck;
        private RuntimeDeck petDeck;
        
        // Lists of combatants
        private List<ICombatant> allies = new List<ICombatant>();
        private List<ICombatant> enemies = new List<ICombatant>();
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Multiple CombatManagers detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            Debug.Log("[CombatManager] Awake called");
            ValidatePrefabs();
        }
        
        private void Start()
        {
            Debug.Log("[CombatManager] Start called");
            
            // Verify combat canvas is set up properly
            if (combatCanvas != null)
            {
                Debug.Log($"[CombatManager] Combat canvas found: {combatCanvas.name}");
                if (!combatCanvas.activeInHierarchy)
                {
                    Debug.LogWarning("[CombatManager] Combat canvas is inactive in hierarchy");
                }
                
                // Check for canvas group for animations
                CanvasGroup canvasGroup = combatCanvas.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    Debug.LogWarning("[CombatManager] Combat canvas has no CanvasGroup component, adding one");
                    canvasGroup = combatCanvas.AddComponent<CanvasGroup>();
                }
            }
            else
            {
                Debug.LogError("[CombatManager] No combat canvas assigned");
            }
        }
        
        private void ValidatePrefabs()
        {
            if (combatPlayerPrefab == null)
                Debug.LogError("[CombatManager] No combatPlayerPrefab assigned");
            
            if (petPrefab == null)
                Debug.LogError("[CombatManager] No petPrefab assigned");
            
            if (playerHandPrefab == null)
                Debug.LogError("[CombatManager] No playerHandPrefab assigned");
            
            if (combatRootPrefab == null)
                Debug.LogError("[CombatManager] No combatRootPrefab assigned");
            
            Debug.Log($"[CombatManager] Prefabs validated - Player: {(combatPlayerPrefab != null ? "Valid" : "Missing")}, " +
                      $"Pet: {(petPrefab != null ? "Valid" : "Missing")}, " +
                      $"Hand: {(playerHandPrefab != null ? "Valid" : "Missing")}, " +
                      $"Root: {(combatRootPrefab != null ? "Valid" : "Missing")}");
        }
        
        // --- Public Accessors ---
        public List<ICombatant> GetEnemies()
        {
            if (enemies == null)
                enemies = new List<ICombatant>();
            return enemies;
        }
        
        public List<ICombatant> GetAllies()
        {
            if (allies == null)
                allies = new List<ICombatant>();
            return allies;
        }
        
        // --- Public Combat Methods ---
        
        [Server]
        public void StartCombatForPlayers(List<NetworkPlayer> players)
        {
            if (combatStarted) 
            {
                Debug.LogWarning("[CombatManager] Combat already started, ignoring duplicate call");
                return;
            }
            
            // Validate inputs
            if (players == null || players.Count < 2)
            {
                Debug.LogError($"[CombatManager] Cannot start combat with less than 2 players. Players: {(players != null ? players.Count : 0)}");
                return;
            }
            
            Debug.Log($"[CombatManager] Starting combat for {players.Count} players");
            playersInCombat.AddRange(players);
            combatStarted = true;
            
            // --- Step 1: Create Combat Roots for players with clear naming ---
            foreach (NetworkPlayer player in players)
            {
                // Create a combat root for this player with more descriptive naming
                GameObject rootObj = Instantiate(combatRootPrefab);
                string playerName = player.GetSteamName();
                rootObj.name = $"Player_{playerName}_Root";
                Spawn(rootObj);
                
                // Set as parent for this player's combat objects
                playerCombatRoots[player] = rootObj.transform;
                
                Debug.Log($"[CombatManager] Created combat root for {playerName}");
            }
            
            // --- Step 2: Spawn player pets with clear naming ---
            Dictionary<NetworkPlayer, Pet> allPlayerPets = new Dictionary<NetworkPlayer, Pet>();
            foreach (NetworkPlayer player in players)
            {
                Transform rootTransform = playerCombatRoots[player];
                string playerName = player.GetSteamName();
                
                // Create a Pet container under the player root
                GameObject petContainer = new GameObject($"Pet_Container_{playerName}");
                petContainer.transform.SetParent(rootTransform, false);
                
                GameObject petObj = Instantiate(petPrefab, petContainer.transform);
                petObj.name = $"Pet_{playerName}";
                
                Pet pet = petObj.GetComponent<Pet>();
                if (pet != null)
                {
                    // Spawn and initialize the pet
                    Spawn(petObj);
                    pet.Initialize(player);
                    allPlayerPets[player] = pet;
                    
                    Debug.Log($"[CombatManager] Created pet for {playerName} under proper container");
                }
                else
                {
                    Debug.LogError($"[CombatManager] Pet prefab missing Pet component for player {playerName}");
                }
            }
            
            // --- Step 2a: Assign pets to opponents --- 
            Dictionary<NetworkPlayer, NetworkPlayer> petAssignments = new Dictionary<NetworkPlayer, NetworkPlayer>();
            // Make a copy of the player list and shuffle it to randomize pet assignments
            List<NetworkPlayer> shuffledPlayers = new List<NetworkPlayer>(players);
            ShuffleList(shuffledPlayers);
            
            Debug.Log("[CombatManager] Assigning pets to opponents:");
            for (int i = 0; i < shuffledPlayers.Count; i++)
            {
                NetworkPlayer player = shuffledPlayers[i];
                NetworkPlayer opponentOwner = shuffledPlayers[(i + 1) % shuffledPlayers.Count];
                petAssignments[player] = opponentOwner;
                
                Debug.Log($"  - {player.GetSteamName()} will fight against {opponentOwner.GetSteamName()}'s pet");
            }
            
            // --- Step 3: Create CombatPlayers, PlayerHands, and link Pets with clear naming --- 
            foreach (NetworkPlayer player in players)
            {
                string playerName = player.GetSteamName();
                NetworkPlayer opponentPetOwner = petAssignments[player];
                
                // Retrieve existing pets
                Pet playerPet = allPlayerPets[player];
                Pet opponentPet = allPlayerPets[opponentPetOwner];
                
                Debug.Log($"[CombatManager] Setting up combat for {playerName} - Pet: {(playerPet != null ? "Found" : "Missing")}, OpponentPet: {(opponentPet != null ? "Found" : "Missing")}");
                
                // Retrieve the player's combat root
                Transform playerRoot = playerCombatRoots[player];
                
                // Create a CombatPlayer container under the player root
                GameObject playerContainer = new GameObject($"Player_Container_{playerName}");
                playerContainer.transform.SetParent(playerRoot, false);
                
                // Create combat player under the player container
                GameObject combatPlayerObj = Instantiate(combatPlayerPrefab, playerContainer.transform);
                combatPlayerObj.name = $"CombatPlayer_{playerName}";
                CombatPlayer combatPlayer = combatPlayerObj.GetComponent<CombatPlayer>();
                if (combatPlayer == null)
                {
                    Debug.LogError($"[CombatManager] CombatPlayer prefab missing CombatPlayer component for {playerName}");
                    continue;
                }
                
                Spawn(combatPlayerObj, player.Owner); // Assign ownership
                
                // Create a Hand container under the player root
                GameObject handContainer = new GameObject($"Hand_Container_{playerName}");
                handContainer.transform.SetParent(playerRoot, false);
                
                // Create player hand under the hand container
                GameObject playerHandObj = Instantiate(playerHandPrefab, handContainer.transform);
                playerHandObj.name = $"PlayerHand_{playerName}";
                PlayerHand playerHand = playerHandObj.GetComponent<PlayerHand>();
                if (playerHand == null)
                {
                    Debug.LogError($"[CombatManager] PlayerHand prefab missing PlayerHand component for {playerName}");
                    continue;
                }
                
                Spawn(playerHandObj, player.Owner); // Assign ownership to match combatPlayer
                
                // Initialize CombatPlayer with correct Pet references
                combatPlayer.Initialize(player, playerPet, opponentPet, playerHand);
                
                // Initialize PlayerHand with the CombatPlayer reference
                playerHand.Initialize(player, combatPlayer);
                
                // Store combat data
                activeCombats[player] = new CombatData
                {
                    CombatPlayer = combatPlayer,
                    PlayerPet = playerPet,
                    OpponentPet = opponentPet,
                    OpponentPlayer = opponentPetOwner,
                    PlayerHand = playerHand,
                    TurnCompleted = false
                    // CombatComplete defaults to false
                };
                
                Debug.Log($"[CombatManager] Combat setup complete for {playerName} with proper hierarchy organization");
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
                    Debug.Log($"[CombatManager] Started turn for {player.GetSteamName()}");
                }
                else
                {
                    Debug.LogError($"[CombatManager] No combat data found for {player.GetSteamName()} when starting turn");
                }
            }
            
            // Log the combat state after initialization
            LogCombatState();
        }
        
        [Server]
        private void LogCombatState()
        {
            Debug.Log("[Combat] === Combat State After Initialization ===");
            Debug.Log($"[Combat] Total players in combat: {playersInCombat.Count}");
            Debug.Log($"[Combat] Total active combats: {activeCombats.Count}");
            
            foreach (var kvp in activeCombats)
            {
                NetworkPlayer player = kvp.Key;
                CombatData data = kvp.Value;
                
                Debug.Log($"[Combat] Player: {player.GetSteamName()}");
                Debug.Log($"[Combat] - Opponent: {data.OpponentPlayer.GetSteamName()}");
                Debug.Log($"[Combat] - Player Pet HP: {data.PlayerPet.CurrentHealth}/{data.PlayerPet.MaxHealth}");
                Debug.Log($"[Combat] - Opponent Pet HP: {data.OpponentPet.CurrentHealth}/{data.OpponentPet.MaxHealth}");
                Debug.Log($"[Combat] - Is Turn Active: {data.CombatPlayer.IsMyTurn}");
                Debug.Log($"[Combat] - Energy: {data.CombatPlayer.CurrentEnergy}/{data.CombatPlayer.MaxEnergy}");
            }
            
            Debug.Log("[Combat] === End Combat State ===");
        }
        
        // Position all combat objects correctly
        [Server]
        private void PositionCombatObjects()
        {
            // This would normally use spawn points but we'll just use RPC to position them
            RpcPositionCombatObjects();
        }
        
        // Called when a player ends their turn
        [Server]
        public void PlayerEndedTurn(CombatPlayer player)
        {
            // Find the NetworkPlayer that owns this CombatPlayer
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
                Debug.LogError("[CombatManager] Could not find NetworkPlayer for CombatPlayer that ended turn");
                return;
            }
            
            Debug.Log($"[CombatManager] Player {networkPlayer.GetSteamName()} ended their turn");
            
            // Mark this player's turn as completed
            if (activeCombats.TryGetValue(networkPlayer, out CombatData combatData))
            {
                combatData.TurnCompleted = true;
                
                // Find the opponent and start their turn
                NetworkPlayer opponentPlayer = combatData.OpponentPlayer;
                if (activeCombats.TryGetValue(opponentPlayer, out CombatData opponentData))
                {
                    opponentData.CombatPlayer.StartTurn();
                    Debug.Log($"[CombatManager] Started turn for opponent {opponentPlayer.GetSteamName()}");
                }
                else
                {
                    Debug.LogError($"[CombatManager] No combat data found for opponent {opponentPlayer.GetSteamName()}");
                }
            }
        }
        
        // Complete a player's combat (win or lose)
        [Server]
        public void CompleteCombat(NetworkPlayer player, bool victory)
        {
            if (!activeCombats.ContainsKey(player))
            {
                Debug.LogError($"[CombatManager] Tried to complete combat for player {player.GetSteamName()}, but no active combat found");
                return;
            }
            
            Debug.Log($"[CombatManager] Completing combat for player {player.GetSteamName()}, Victory: {victory}");
            
            // Show the result to the player
            RpcShowCombatResult(player.Owner, victory);
            
            // Mark as complete
            activeCombats[player].CombatComplete = true;
            
            // Check if all combats have ended
            CheckCombatEndState();
        }
        
        // Mark a combat as complete
        [Server]
        public void MarkCombatComplete(NetworkPlayer player, bool win)
        {
            if (activeCombats.TryGetValue(player, out CombatData combatData))
            {
                combatData.CombatComplete = true;
                
                // Show the result UI to this player
                RpcShowCombatResult(player.Owner, win);
                
                Debug.Log($"[CombatManager] Marked combat as complete for {player.GetSteamName()}, Win: {win}");
                
                // Check if all combats have ended
                CheckCombatEndState();
            }
            else
            {
                Debug.LogError($"[CombatManager] No combat data found for {player.GetSteamName()} when marking complete");
            }
        }
        
        // Process combat damage and check for defeat
        [Server]
        public void ProcessCombatDamage(NetworkPlayer player, int damage)
        {
            if (!activeCombats.TryGetValue(player, out CombatData combatData))
                return;
            
            // Apply damage to player's pet
            combatData.PlayerPet.TakeDamage(damage);
            
            Debug.Log($"[CombatManager] Player {player.GetSteamName()}'s pet took {damage} damage. " +
                      $"Health: {combatData.PlayerPet.CurrentHealth}/{combatData.PlayerPet.MaxHealth}");
            
            // Check if player's pet is defeated
            if (combatData.PlayerPet.IsDefeated())
            {
                // Player lost the combat
                RpcShowCombatResult(player.Owner, false);
                // Mark combat as complete for this player
                combatData.CombatComplete = true; 
                
                Debug.Log($"[CombatManager] Player {player.GetSteamName()}'s pet was defeated");
                
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
            Debug.Log($"[CombatManager] Pet defeat registered");
            
            // Find the combat data for this pet
            foreach (var kvp in activeCombats)
            {
                if (kvp.Value.PlayerPet == pet)
                {
                    // Player's pet was defeated, they lost
                    Debug.Log($"[CombatManager] Player {kvp.Key.GetSteamName()}'s pet was defeated (player lost)");
                    RpcShowCombatResult(kvp.Key.Owner, false);
                    kvp.Value.CombatComplete = true;
                }
                else if (kvp.Value.OpponentPet == pet)
                {
                    // Opponent's pet was defeated, player won
                    Debug.Log($"[CombatManager] Opponent's pet was defeated, player {kvp.Key.GetSteamName()} won");
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
            // If all players have completed their combat, end the overall combat
            bool allComplete = true;
            foreach (var kvp in activeCombats)
            {
                if (!kvp.Value.CombatComplete)
                {
                    allComplete = false;
                    break;
                }
            }
            
            if (allComplete)
            {
                Debug.Log("[CombatManager] All combats complete, ending combat phase");
                RpcEndCombat();
                
                // Reset combat state
                combatStarted = false;
                activeCombats.Clear();
                playersInCombat.Clear();
                playerCombatRoots.Clear();
                
                // On server, notify LobbyManager that combat is over
                // This would typically be done via a more formal event system
                if (LobbyManager.Instance != null)
                {
                    LobbyManager.Instance.OnCombatEnded();
                }
            }
        }
        
        // Get a player's pet
        public Pet GetPet(NetworkPlayer player)
        {
            if (activeCombats.TryGetValue(player, out CombatData combatData))
            {
                return combatData.PlayerPet;
            }
            return null;
        }
        
        // Get a player's opponent's pet
        public Pet GetOpponentPet(NetworkPlayer player)
        {
            if (activeCombats.TryGetValue(player, out CombatData combatData))
            {
                return combatData.OpponentPet;
            }
            return null;
        }
        
        // --- Utility Methods ---
        
        #region Utility
        
        // Fisher-Yates shuffle algorithm
        public static void ShuffleList<T>(List<T> list)
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
            Debug.Log("[CombatManager] RpcShowCombatCanvas received");
            
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
                
                Debug.Log("[CombatManager] Combat canvas activated");
            }
            else
            {
                Debug.LogError("[CombatManager] Cannot show combat canvas - reference is null");
            }
        }
        
        [ObserversRpc]
        private void RpcHideCombatCanvas()
        {
            Debug.Log("[CombatManager] RpcHideCombatCanvas received");
            
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
                
                Debug.Log("[CombatManager] Combat canvas hidden");
            }
            else
            {
                Debug.LogError("[CombatManager] Cannot hide combat canvas - reference is null");
            }
        }
        
        [ObserversRpc]
        private void RpcPositionCombatObjects()
        {
            Debug.Log("[CombatManager] RpcPositionCombatObjects called - setting up combat objects on client");
            
            // Find local player
            NetworkPlayer localNetworkPlayer = null;
            foreach (NetworkPlayer player in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
            {
                if (player.IsOwner)
                {
                    localNetworkPlayer = player;
                    Debug.Log($"[CombatManager] Found local player: {player.GetSteamName()}");
                    break;
                }
            }
            
            if (localNetworkPlayer == null)
            {
                Debug.LogError("[CombatManager] Could not find local NetworkPlayer!");
                return;
            }
            
            // Find combat players
            CombatPlayer[] combatPlayers = FindObjectsByType<CombatPlayer>(FindObjectsSortMode.None);
            if (combatPlayers.Length != 2)
            {
                Debug.LogError($"[CombatManager] Expected exactly 2 CombatPlayers, found {combatPlayers.Length}");
                return;
            }
            
            // Get all pets in the scene
            Pet[] allPets = FindObjectsByType<Pet>(FindObjectsSortMode.None);
            if (allPets.Length != 2)
            {
                Debug.LogError($"[CombatManager] Expected exactly 2 Pets, found {allPets.Length}");
                return;
            }
            
            Debug.Log($"[CombatManager] Found {allPets.Length} pets");
            
            // Get player hands
            PlayerHand[] playerHands = FindObjectsByType<PlayerHand>(FindObjectsSortMode.None);
            Debug.Log($"[CombatManager] Found {playerHands.Length} player hands");
            
            // Find local player's pet and opponent's pet
            Pet localPlayerPet = null;
            Pet opponentPet = null;
            
            // First try to find pet by direct ownership
            foreach (Pet pet in allPets)
            {
                if (pet.PlayerOwner == localNetworkPlayer)
                {
                    localPlayerPet = pet;
                    Debug.Log($"[CombatManager] Found local player's pet by direct ownership: {pet.name}");
                }
                else if (pet.PlayerOwner != null && pet.PlayerOwner != localNetworkPlayer)
                {
                    opponentPet = pet;
                    Debug.Log($"[CombatManager] Found opponent's pet by direct ownership: {pet.name}");
                }
            }
            
            // If we couldn't find both pets by ownership, use other methods
            if (localPlayerPet == null || opponentPet == null)
            {
                Debug.Log("[CombatManager] Couldn't identify both pets by ownership, trying alternative methods");
                
                // If we have at least identified one pet, assign the other as the remaining one
                if (localPlayerPet != null && opponentPet == null)
                {
                    foreach (Pet pet in allPets)
                    {
                        if (pet != localPlayerPet)
                        {
                            opponentPet = pet;
                            Debug.Log($"[CombatManager] Assigned remaining pet as opponent: {pet.name}");
                            break;
                        }
                    }
                }
                else if (localPlayerPet == null && opponentPet != null)
                {
                    foreach (Pet pet in allPets)
                    {
                        if (pet != opponentPet)
                        {
                            localPlayerPet = pet;
                            Debug.Log($"[CombatManager] Assigned remaining pet as local player's: {pet.name}");
                            break;
                        }
                    }
                }
                // If we couldn't identify either pet by ownership, just take the first two and assign them
                else if (localPlayerPet == null && opponentPet == null && allPets.Length >= 2)
                {
                    // Just assign the first pet we find to local player and second to opponent
                    localPlayerPet = allPets[0];
                    opponentPet = allPets[1];
                    Debug.Log($"[CombatManager] Assigned pets arbitrarily: Local={localPlayerPet.name}, Opponent={opponentPet.name}");
                }
            }
            
            // Verify we don't have the same pet assigned to both roles
            if (localPlayerPet == opponentPet)
            {
                Debug.LogError("[CombatManager] CRITICAL ERROR: Same pet assigned to both local player and opponent!");
                if (allPets.Length >= 2)
                {
                    // Try to reassign one of them
                    for (int i = 0; i < allPets.Length; i++)
                    {
                        if (allPets[i] != localPlayerPet)
                        {
                            opponentPet = allPets[i];
                            Debug.Log($"[CombatManager] Fixed duplicate pet assignment, new opponent pet: {opponentPet.name}");
                            break;
                        }
                    }
                }
            }
            
            // Find the player hand that belongs to the local player
            PlayerHand localPlayerHand = null;
            foreach (PlayerHand hand in playerHands)
            {
                if (hand.Owner != null && hand.Owner.ClientId == localNetworkPlayer.Owner.ClientId)
                {
                    localPlayerHand = hand;
                    Debug.Log($"[CombatManager] Found local player's hand: {hand.name}");
                    break;
                }
            }
            
            if (localPlayerHand == null && playerHands.Length > 0)
            {
                // If we couldn't find the hand by owner, just use the first one
                localPlayerHand = playerHands[0];
                Debug.Log($"[CombatManager] Using first available hand as local player's: {localPlayerHand.name}");
            }
            
            // Now assign these objects to the CombatPlayer components
            CombatPlayer localCombatPlayer = null;
            CombatPlayer opponentCombatPlayer = null;
            
            // Find which CombatPlayer is the local one
            foreach (CombatPlayer combatPlayer in combatPlayers)
            {
                if (combatPlayer.NetworkPlayer == localNetworkPlayer)
                {
                    localCombatPlayer = combatPlayer;
                    Debug.Log($"[CombatManager] Found local combat player: {combatPlayer.name}");
                }
                else
                {
                    opponentCombatPlayer = combatPlayer;
                    Debug.Log($"[CombatManager] Found opponent combat player: {combatPlayer.name}");
                }
            }
            
            // If we couldn't identify the local combat player by NetworkPlayer, try another approach
            if (localCombatPlayer == null)
            {
                foreach (CombatPlayer combatPlayer in combatPlayers)
                {
                    if (combatPlayer.IsOwner)
                    {
                        localCombatPlayer = combatPlayer;
                        Debug.Log($"[CombatManager] Found local combat player by IsOwner: {combatPlayer.name}");
                        
                        // The other one must be the opponent
                        foreach (CombatPlayer cp in combatPlayers)
                        {
                            if (cp != localCombatPlayer)
                            {
                                opponentCombatPlayer = cp;
                                Debug.Log($"[CombatManager] Found opponent combat player (by elimination): {cp.name}");
                                break;
                            }
                        }
                        break;
                    }
                }
            }
            
            // If we still couldn't identify both, just take the first two
            if (localCombatPlayer == null || opponentCombatPlayer == null)
            {
                Debug.LogWarning("[CombatManager] Could not identify local and opponent combat players properly, assigning arbitrarily");
                if (combatPlayers.Length >= 2)
                {
                    localCombatPlayer = combatPlayers[0];
                    opponentCombatPlayer = combatPlayers[1];
                }
            }
            
            if (localCombatPlayer != null && opponentCombatPlayer != null)
            {
                Debug.Log("[CombatManager] Setting private fields via reflection...");
                // Set up the local combat player with references
                SetPrivateFieldValue(localCombatPlayer, "playerPet", localPlayerPet);
                SetPrivateFieldValue(localCombatPlayer, "opponentPet", opponentPet);
                SetPrivateFieldValue(localCombatPlayer, "playerHand", localPlayerHand);
                
                // Set up the opponent combat player with references (reversed pets)
                SetPrivateFieldValue(opponentCombatPlayer, "playerPet", opponentPet);
                SetPrivateFieldValue(opponentCombatPlayer, "opponentPet", localPlayerPet);
                SetPrivateFieldValue(opponentCombatPlayer, "playerHand", null); // Opponent doesn't need a hand on this client
                
                // Log the final assignments for debugging
                Debug.Log($"[CombatManager] Final assignments for local combat player ({localCombatPlayer.name}):" +
                         $"\n  - Player Pet: {(localPlayerPet != null ? localPlayerPet.name : "null")}" +
                         $"\n  - Opponent Pet: {(opponentPet != null ? opponentPet.name : "null")}" +
                         $"\n  - Player Hand: {(localPlayerHand != null ? localPlayerHand.name : "null")}");
                
                Debug.Log($"[CombatManager] Final assignments for opponent combat player ({opponentCombatPlayer.name}):" +
                         $"\n  - Player Pet: {(opponentPet != null ? opponentPet.name : "null")}" +
                         $"\n  - Opponent Pet: {(localPlayerPet != null ? localPlayerPet.name : "null")}" +
                         $"\n  - Player Hand: null");
                
                // Final verification check
                if (localPlayerPet == opponentPet && localPlayerPet != null)
                {
                    Debug.LogError("[CombatManager] CRITICAL ERROR: After assignment, the same pet is still assigned to both player and opponent!");
                }
            }
            else
            {
                Debug.LogError("[CombatManager] Failed to set up combat players - could not find both local and opponent combat players");
            }
            
            // Log pet details separately for clarity and to fix syntax issues
            var petDetails = allPets.Select(p => {
                string ownerInfo = p.PlayerOwner != null ? $" (Owner: {p.PlayerOwner.GetSteamName()})" : " (No owner)";
                return p.name + ownerInfo;
            });
            Debug.Log($"[CombatManager] Pet details: {string.Join(", ", petDetails)}");
        }
        
        [TargetRpc]
        private void RpcShowOpponentAttacking(NetworkConnection conn, int damage)
        {
            Debug.Log($"[CombatManager] RpcShowOpponentAttacking received with damage: {damage}");
            // Implement client-side animation showing opponent attacking
        }
        
        [TargetRpc]
        private void RpcShowCombatResult(NetworkConnection conn, bool victory)
        {
            string result = victory ? "Victory" : "Defeat";
            Debug.Log($"[CombatManager] RpcShowCombatResult received: {result}");
            
            /* // Temporarily commented out due to missing CombatCanvasManager.cs
            // Find the combat canvas manager
            CombatCanvasManager canvasManager = FindFirstObjectByType<CombatCanvasManager>();
            if (canvasManager != null)
            {
                canvasManager.ShowCombatResult(victory);
                Debug.Log($"[CombatManager] Showing combat result UI: {result}");
            }
            else
            {
                Debug.LogError("[CombatManager] Cannot show combat result - CombatCanvasManager not found");
            }
            */
        }
        
        [ObserversRpc]
        private void RpcEndCombat()
        {
            Debug.Log("[CombatManager] RpcEndCombat received");
            
            // Hide the combat canvas
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
            
            // Destroy all combat-related objects
            // This happens automatically when we return to the lobby scene
        }
        
        #endregion
        
        private void SetPrivateFieldValue(object obj, string fieldName, object value)
        {
            try
            {
                FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null)
                {
                    field.SetValue(obj, value);
                    Debug.Log($"[CombatManager] Successfully set {fieldName} on {obj.GetType().Name}");
                }
                else
                {
                    Debug.LogError($"[CombatManager] Could not find field '{fieldName}' on {obj.GetType().Name}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CombatManager] Error setting field {fieldName}: {e.Message}");
            }
        }
    }
    
    // Helper data structure to track combat state for each player
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