using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Connection;
using DG.Tweening;
using System.Linq;
using System.Reflection;
using FishNet.Managing.Scened;
using FishNet;

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
            Dictionary<NetworkPlayer, NetworkObject> playerRootObjects = new Dictionary<NetworkPlayer, NetworkObject>(); // Store NetworkObject
            foreach (NetworkPlayer player in players)
            {
                GameObject rootObj = Instantiate(combatRootPrefab);
                // Don't rename on server yet, clients will do this
                Spawn(rootObj); 
                playerRootObjects[player] = rootObj.GetComponent<NetworkObject>(); // Store the NetworkObject
                playerCombatRoots[player] = rootObj.transform; // Keep local transform reference if needed
                Debug.Log($"[CombatManager] Spawned combat root for Player {player.Owner.ClientId}");
            }
            
            // --- Step 2: Spawn player pets ---
            Dictionary<NetworkPlayer, CombatPet> playerCombatPets = new Dictionary<NetworkPlayer, CombatPet>();
            Dictionary<NetworkPlayer, NetworkObject> playerPetObjects = new Dictionary<NetworkPlayer, NetworkObject>(); // Store NetworkObject
            foreach (NetworkPlayer player in players)
            {
                // Get the persistent pet reference
                Pet persistentPet = player.playerPet.Value;
                if (persistentPet == null)
                {
                    Debug.LogError($"[CombatManager] Player {player.GetSteamName()} has no persistent pet!");
                    continue;
                }
                
                // Create a combat pet for this player's pet
                GameObject combatPetObj = Instantiate(petPrefab); 
                CombatPet combatPet = combatPetObj.GetComponent<CombatPet>();
                if (combatPet == null)
                {
                    // Try adding the component if it's not there
                    combatPet = combatPetObj.AddComponent<CombatPet>();
                    if (combatPet == null)
                    {
                        Debug.LogError($"[CombatManager] Could not add CombatPet component to prefab for player {player.Owner.ClientId}");
                        Destroy(combatPetObj);
                        continue;
                    }
                }
                
                Spawn(combatPetObj); // Spawn the combat pet
                playerCombatPets[player] = combatPet;
                playerPetObjects[player] = combatPetObj.GetComponent<NetworkObject>();
                Debug.Log($"[CombatManager] Spawned combat pet for Player {player.Owner.ClientId}");
            }
            
            // --- Step 2a: Assign pets to opponents --- 
            Dictionary<NetworkPlayer, NetworkPlayer> petAssignments = new Dictionary<NetworkPlayer, NetworkPlayer>();
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
            
            // --- Step 3: Create CombatPlayers and PlayerHands --- 
            Dictionary<NetworkPlayer, NetworkObject> playerCombatPlayerObjects = new Dictionary<NetworkPlayer, NetworkObject>();
            Dictionary<NetworkPlayer, NetworkObject> playerHandObjects = new Dictionary<NetworkPlayer, NetworkObject>();

            foreach (NetworkPlayer player in players)
            {
                // Get the NetworkPlayer who owns the pet this player will fight
                NetworkPlayer opponentPetOwner = petAssignments[player];
                
                // Get the CombatPet instances using the owners
                CombatPet playerPet = playerCombatPets.ContainsKey(player) ? playerCombatPets[player] : null;
                CombatPet opponentPet = playerCombatPets.ContainsKey(opponentPetOwner) ? playerCombatPets[opponentPetOwner] : null;

                if (playerPet == null) {
                    Debug.LogError($"[CombatManager] Failed to find PlayerPet for {player.GetSteamName()} during CombatPlayer setup.");
                    continue; // Skip this player if their pet wasn't created properly
                }
                if (opponentPet == null) {
                    Debug.LogError($"[CombatManager] Failed to find OpponentPet (owned by {opponentPetOwner.GetSteamName()}) for {player.GetSteamName()} during CombatPlayer setup.");
                    continue; // Skip this player if the opponent pet wasn't created properly
                }
                
                // --- Step 3a: Create Runtime Deck from player's persistent deck --- 
                RuntimeDeck runtimePlayerDeck = null;
                if (DeckManager.Instance != null)
                {
                    // Create a new empty runtime deck
                    runtimePlayerDeck = new RuntimeDeck(player.GetSteamName() + "'s Combat Deck", DeckType.PlayerDeck);
                    
                    // Check if player has an initialized deck
                    if (player.persistentDeckCardIDs.Count > 0)
                    {
                        Debug.Log($"[CombatManager] Creating runtime deck from {player.GetSteamName()}'s persistent deck ({player.persistentDeckCardIDs.Count} cards)");
                        
                        // Populate deck from persistent deck IDs
                        foreach (string cardID in player.persistentDeckCardIDs)
                        {
                            CardData cardData = DeckManager.Instance.FindCardByName(cardID);
                            if (cardData != null)
                            {
                                runtimePlayerDeck.AddCard(cardData);
                                Debug.Log($"[CombatManager] Added card {cardID} to runtime deck");
                            }
                            else
                            {
                                Debug.LogWarning($"[CombatManager] Could not find CardData for {cardID}");
                            }
                        }
                        
                        // Shuffle the newly created deck
                        runtimePlayerDeck.Shuffle();
                        Debug.Log($"[CombatManager] Shuffled runtime deck with {runtimePlayerDeck.DrawPileCount} cards for {player.GetSteamName()}");
                    }
                    else
                    {
                        // Fallback: If player doesn't have cards in persistent deck, use starter deck
                        Debug.LogWarning($"[CombatManager] Player {player.GetSteamName()} has no persistent deck. Using starter deck.");
                        runtimePlayerDeck = DeckManager.Instance.GetPlayerStarterDeck(); // Ensure a valid deck
                        if (runtimePlayerDeck != null) runtimePlayerDeck.Shuffle();
                    }
                }
                else
                {
                    Debug.LogError("[CombatManager] DeckManager is null, cannot create runtime deck.");
                    // Create an empty deck to prevent null reference errors
                    runtimePlayerDeck = new RuntimeDeck(player.GetSteamName() + "'s Empty Deck", DeckType.PlayerDeck);
                }

                // --- Step 3b: Create CombatPlayer FIRST --- 
                GameObject combatPlayerObj = Instantiate(combatPlayerPrefab); 
                CombatPlayer combatPlayer = combatPlayerObj.GetComponent<CombatPlayer>();
                Spawn(combatPlayerObj, player.Owner); // Assign ownership
                playerCombatPlayerObjects[player] = combatPlayerObj.GetComponent<NetworkObject>();
                Debug.Log($"[CombatManager] Spawned CombatPlayer for {player.GetSteamName()}");

                // --- Step 3c: Create Player Hand SECOND --- 
                GameObject playerHandObj = Instantiate(playerHandPrefab); 
                PlayerHand hand = playerHandObj.GetComponent<PlayerHand>();
                Spawn(playerHandObj, player.Owner); // Assign ownership
                playerHandObjects[player] = playerHandObj.GetComponent<NetworkObject>();
                Debug.Log($"[CombatManager] Spawned PlayerHand for {player.GetSteamName()}");

                // --- Step 3d: Initialize PlayerHand THIRD (needs combatPlayer) ---
                hand.Initialize(player, combatPlayer);

                // --- Step 3e: Initialize CombatPlayer FOURTH (needs hand) ---
                combatPlayer.Initialize(player, playerPet, opponentPet, hand, runtimePlayerDeck);
                
                // --- Step 3f: Store references in CombatData ---
                if (!activeCombats.ContainsKey(player))
                {
                    activeCombats[player] = new CombatData();
                }
                activeCombats[player].CombatPlayer = combatPlayer;
                activeCombats[player].PlayerPet = playerPet;
                activeCombats[player].OpponentPet = opponentPet;
                activeCombats[player].OpponentPlayer = opponentPetOwner; // Store the opponent player for later
                activeCombats[player].PlayerHand = hand;
                activeCombats[player].TurnCompleted = false;
                activeCombats[player].CombatComplete = false;
                
                Debug.Log($"[CombatManager] Populated CombatData for {player.GetSteamName()}: PlayerPet={playerPet.name}, OpponentPet={opponentPet.name}");
            }
            
            // Position all objects on clients (This might need adjustment or removal if hierarchy setup handles positions)
            // RpcPositionCombatObjects(); // Let's comment this out for now and see if hierarchy setup is enough
            
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
        // Commented out as potentially redundant after hierarchy RPC
        // [Server]
        // private void PositionCombatObjects()
        // {
        //     // This would normally use spawn points but we'll just use RPC to position them
        //     RpcPositionCombatObjects();
        // }
        
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
        public void HandlePetDefeat(CombatPet combatPet)
        {
            Debug.Log($"[CombatManager] Combat pet defeat registered");
            
            // Find the combat data for this pet
            foreach (var kvp in activeCombats)
            {
                if (kvp.Value.PlayerPet == combatPet)
                {
                    // Player's pet was defeated, they lost
                    Debug.Log($"[CombatManager] Player {kvp.Key.GetSteamName()}'s pet was defeated (player lost)");
                    RpcShowCombatResult(kvp.Key.Owner, false);
                    kvp.Value.CombatComplete = true;
                }
                else if (kvp.Value.OpponentPet == combatPet)
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
        public CombatPet GetPet(NetworkPlayer player)
        {
            if (activeCombats.TryGetValue(player, out CombatData combatData))
            {
                return combatData.PlayerPet;
            }
            return null;
        }
        
        // Get a player's opponent's pet
        public CombatPet GetOpponentPet(NetworkPlayer player)
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
            
            // Declare opponentPlayerHand here to ensure it's in scope for later use
            PlayerHand opponentPlayerHand = null; 

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
                Debug.Log("[CombatManager] Setting private fields via reflection for RpcPositionCombatObjects...");
                // Set up the local combat player with references
                SetPrivateFieldValue(localCombatPlayer, "playerPet", localPlayerPet);
                SetPrivateFieldValue(localCombatPlayer, "opponentPet", opponentPet);
                SetPrivateFieldValue(localCombatPlayer, "playerHand", localPlayerHand);
                
                // Find the OPPONENT player hand - Note: opponentPlayerHand is already declared in the outer scope
                // PlayerHand opponentPlayerHand = null; // REMOVE this redeclaration
                if (opponentCombatPlayer != null && opponentCombatPlayer.Owner != null)
                {
                    foreach (PlayerHand hand in playerHands)
                    {
                        // Find the hand owned by the opponent combat player's connection
                        if (hand.Owner != null && hand.Owner.ClientId == opponentCombatPlayer.Owner.ClientId && hand != localPlayerHand)
                        {
                            opponentPlayerHand = hand;
                            Debug.Log($"[CombatManager] Found opponent player's hand: {hand.name} (Owner: {hand.Owner.ClientId})");
                            break;
                        }
                    }
                }
                else if (opponentPet != null && opponentPet.PlayerOwner != null && opponentPet.PlayerOwner.Owner != null)
                {
                     // Fallback: Find hand based on opponent pet's owner if combat player owner wasn't ready
                    foreach (PlayerHand hand in playerHands)
                    {
                        if (hand.Owner != null && hand.Owner.ClientId == opponentPet.PlayerOwner.Owner.ClientId && hand != localPlayerHand)
                        {
                             opponentPlayerHand = hand;
                             Debug.Log($"[CombatManager] Found opponent player's hand via Pet Owner: {hand.name} (Owner: {hand.Owner.ClientId})");
                             break;
                        }
                    }
                }

                if (opponentPlayerHand == null)
                {
                    Debug.LogWarning("[CombatManager] Could not reliably find opponent's PlayerHand.");
                    // Attempting less reliable fallback: find the hand that isn't the local one
                    if (playerHands.Length == 2)
                    {
                        opponentPlayerHand = (playerHands[0] == localPlayerHand) ? playerHands[1] : playerHands[0];
                        Debug.LogWarning($"[CombatManager] Fallback: Assigned opponent hand to {opponentPlayerHand?.name}");
                    }
                }
                
                // Set up the opponent combat player with references (reversed pets)
                SetPrivateFieldValue(opponentCombatPlayer, "playerPet", opponentPet);
                SetPrivateFieldValue(opponentCombatPlayer, "opponentPet", localPlayerPet);
                SetPrivateFieldValue(opponentCombatPlayer, "playerHand", opponentPlayerHand); // Assign found opponent hand
                
                // Log the final assignments for debugging
                Debug.Log($"[CombatManager] Final assignments for local combat player ({localCombatPlayer.name}):" +
                         $"\n  - Player Pet: {(localPlayerPet != null ? localPlayerPet.name : "null")}" +
                         $"\n  - Opponent Pet: {(opponentPet != null ? opponentPet.name : "null")}" +
                         $"\n  - Player Hand: {(localPlayerHand != null ? localPlayerHand.name : "null")}");
                
                Debug.Log($"[CombatManager] Final assignments for opponent combat player ({opponentCombatPlayer.name}):" +
                         $"\n  - Player Pet: {(opponentPet != null ? opponentPet.name : "null")}" +
                         $"\n  - Opponent Pet: {(localPlayerPet != null ? localPlayerPet.name : "null")}" +
                         $"\n  - Player Hand: {(opponentPlayerHand != null ? opponentPlayerHand.name : "null")}"); // Log assigned opponent hand
                
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

            Debug.Log($"[CombatManager-Diag] All PlayerHands Found:");
            foreach (var hand in playerHands)
            {
                Debug.Log($"  - Hand: {hand.name}, Owner ClientId: {hand.Owner?.ClientId ?? -1}");
            }
            Debug.Log($"[CombatManager-Diag] Identified Local PlayerHand: {(localPlayerHand != null ? localPlayerHand.name : "null")}, Owner ClientId: {localPlayerHand?.Owner?.ClientId ?? -1}");
            Debug.Log($"[CombatManager-Diag] Identified Opponent PlayerHand: {(opponentPlayerHand != null ? opponentPlayerHand.name : "null")}, Owner ClientId: {opponentPlayerHand?.Owner?.ClientId ?? -1}"); // This log should now work

            Debug.Log("[CombatManager-Diag] All CombatPlayers Found:");
            foreach (var cp in combatPlayers)
            {
                Debug.Log($"  - CombatPlayer: {cp.name}, NetworkPlayer: {cp.NetworkPlayer?.GetSteamName() ?? "null"}, IsOwner: {cp.IsOwner}");
            }
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
        
        [ObserversRpc]
        private void RpcSetupCombatHierarchy(string playerName, int rootObjId, int petObjId, int combatPlayerObjId, int handObjId)
        {
            Debug.Log($"[Client] Received RpcSetupCombatHierarchy for player {playerName}");

            // Find the NetworkObjects using their IDs - CORRECTED API Call
            NetworkObject rootNob = null;
            NetworkObject petNob = null;
            NetworkObject combatPlayerNob = null;
            NetworkObject handNob = null;

            if (InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Objects != null)
            {
                InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(rootObjId, out rootNob);
                InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(petObjId, out petNob);
                InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(combatPlayerObjId, out combatPlayerNob);
                InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(handObjId, out handNob);
            }
            else
            {
                 Debug.LogError($"[Client] ClientManager or ClientManager.Objects is null. Cannot find NetworkObjects for player {playerName}.");
                 return;
            }

            if (rootNob == null || petNob == null || combatPlayerNob == null || handNob == null)
            {
                 Debug.LogError($"[Client] Failed to find one or more NetworkObjects for player {playerName}. " +
                               $"Root: {(rootNob != null)}, Pet: {(petNob != null)}, Player: {(combatPlayerNob != null)}, Hand: {(handNob != null)}");
                 return;
            }

            // Rename the objects
            rootNob.gameObject.name = $"Player_{playerName}_Root";
            petNob.gameObject.name = $"Pet_{playerName}";
            combatPlayerNob.gameObject.name = $"CombatPlayer_{playerName}";
            handNob.gameObject.name = $"PlayerHand_{playerName}";
            
            Debug.Log($"[Client] Renamed objects for player {playerName}");

            // Set the hierarchy (parenting)
            // Note: Creating intermediate containers like before might be desirable for organization.
            // Let's keep it simple first and parent directly to the root.
            Transform rootTransform = rootNob.transform;
            petNob.transform.SetParent(rootTransform, false); // Use worldPositionStays = false
            combatPlayerNob.transform.SetParent(rootTransform, false);
            handNob.transform.SetParent(rootTransform, false);

            Debug.Log($"[Client] Set hierarchy for player {playerName}: Parented Pet, CombatPlayer, Hand under Root");

            // Optional: Add containers dynamically if needed for scene organization
            // GameObject petContainer = new GameObject($"Pet_Container_{playerName}");
            // petContainer.transform.SetParent(rootTransform, false);
            // petNob.transform.SetParent(petContainer.transform, false);
            // Similar for Player and Hand containers...
            
            // Refresh CombatPlayer references if necessary (might be needed if Initialize relies on hierarchy/names)
            CombatPlayer combatPlayer = combatPlayerNob.GetComponent<CombatPlayer>();
            if (combatPlayer != null)
            {
                // Example: Re-run parts of initialization or specific update methods if they failed before hierarchy was set
                // combatPlayer.FindCombatReferences(); // If such a method exists and is safe to call again
                 Debug.Log($"[Client] Found CombatPlayer component for {playerName}. Consider if re-initialization/reference update is needed.");
            }
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
        public CombatPet PlayerPet;
        public CombatPet OpponentPet;
        public NetworkPlayer OpponentPlayer;
        public PlayerHand PlayerHand;
        public bool TurnCompleted;
        public bool CombatComplete;
    }
} 