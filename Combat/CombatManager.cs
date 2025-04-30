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
        [SerializeField] private GameObject combatPetPrefab;
        [SerializeField] private GameObject playerHandPrefab;
        [SerializeField] private GameObject petHandPrefab;
        [SerializeField] private GameObject combatSceneCanvasPrefab; // Prefab for combat scene canvas
        
        [Header("References")]
        // combatCanvas reference removed as we're using programmatically generated canvases
        
        // List of active combats
        private readonly Dictionary<NetworkPlayer, CombatData> activeCombats = new Dictionary<NetworkPlayer, CombatData>();
        private readonly List<NetworkPlayer> playersInCombat = new List<NetworkPlayer>();
        private bool combatStarted = false;
        
        // Runtime decks
        private RuntimeDeck playerDeck;
        private RuntimeDeck petDeck;
        
        // Lists of combatants
        private List<ICombatant> allies = new List<ICombatant>();
        private List<ICombatant> enemies = new List<ICombatant>();
        
        // List of combat scene canvases
        private readonly List<CombatSceneCanvas> combatSceneCanvases = new List<CombatSceneCanvas>();
        
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
            
            // We no longer need a direct reference to a combat canvas
            // since we're generating canvases programmatically using combatSceneCanvasPrefab
        }
        
        private void ValidatePrefabs()
        {
            if (combatPlayerPrefab == null)
                Debug.LogError("[CombatManager] No combatPlayerPrefab assigned");
            
            if (combatPetPrefab == null)
                Debug.LogError("[CombatManager] No combatPetPrefab assigned");
            
            if (playerHandPrefab == null)
                Debug.LogError("[CombatManager] No playerHandPrefab assigned");
            
            if (petHandPrefab == null)
                Debug.LogError("[CombatManager] No petHandPrefab assigned");
                
            if (combatSceneCanvasPrefab == null)
                Debug.LogError("[CombatManager] No combatSceneCanvasPrefab assigned");
            
            Debug.Log($"[CombatManager] Prefabs validated - Player: {(combatPlayerPrefab != null ? "Valid" : "Missing")}, " +
                      $"CombatPet: {(combatPetPrefab != null ? "Valid" : "Missing")}, " +
                      $"Hand: {(playerHandPrefab != null ? "Valid" : "Missing")}, " +
                      $"PetHand: {(petHandPrefab != null ? "Valid" : "Missing")}, " +
                      $"SceneCanvas: {(combatSceneCanvasPrefab != null ? "Valid" : "Missing")}");
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
                
                // Ensure CombatPet prefab is assigned
                if (combatPetPrefab == null)
                {
                    Debug.LogError($"[CombatManager] CombatPet Prefab is not assigned in the inspector for player {player.Owner.ClientId}");
                    continue;
                }

                // Create a combat pet for this player's persistent pet using the correct prefab
                GameObject combatPetObj = Instantiate(combatPetPrefab); 
                CombatPet combatPet = combatPetObj.GetComponent<CombatPet>();
                
                if (combatPet == null)
                {
                    Debug.LogError($"[CombatManager] CombatPet Prefab does not contain a CombatPet component for player {player.Owner.ClientId}");
                    Destroy(combatPetObj);
                    continue;
                }

                // Spawn the CombatPet (now correctly parented and initialized)
                Spawn(combatPetObj); // Spawn FIRST

                // Set parent AFTER spawning
                combatPetObj.transform.SetParent(persistentPet.transform, false);

                // Initialize AFTER spawning
                combatPet.Initialize(persistentPet);

                // Create Deck AFTER initializing
                combatPet.CreateRuntimeDeck();
                
                playerCombatPets[player] = combatPet;
                playerPetObjects[player] = combatPetObj.GetComponent<NetworkObject>();
                Debug.Log($"[CombatManager] Spawned combat pet for Player {player.Owner.ClientId} as child of {persistentPet.name}");

                // --- Spawn PetHand for the CombatPet ---
                if (petHandPrefab != null)
                {
                    GameObject petHandObj = Instantiate(petHandPrefab);
                    PetHand petHandInstance = petHandObj.GetComponent<PetHand>();
                    if (petHandInstance != null)
                    {
                        petHandObj.name = $"PetHand_{player.GetSteamName()}";
                        petHandObj.transform.SetParent(persistentPet.transform, false); // Parent to the persistent Pet
                        Spawn(petHandObj, player.Owner); // Spawn with player ownership (like PlayerHand)
                        combatPet.AssignHand(petHandInstance); // Link hand to the combatPet (logical link remains)
                        Debug.Log($"[CombatManager] Spawned PetHand for CombatPet of Player {player.Owner.ClientId}, parented to {persistentPet.name}");

                        // --- Draw Initial Pet Hand ---
                        petHandInstance.Initialize(combatPet); // Initialize hand with pet reference
                        petHandInstance.DrawInitialHand(3); // Draw initial cards (adjust count as needed)
                        Debug.Log($"[CombatManager] Drew initial hand for PetHand of Player {player.Owner.ClientId}");
                    }
                    else
                    {
                         Debug.LogError($"[CombatManager] petHandPrefab is missing the PetHand component for player {player.Owner.ClientId}!");
                         Destroy(petHandObj);
                    }
                }
                else
                {
                    Debug.LogError($"[CombatManager] petHandPrefab is not assigned in the inspector for player {player.Owner.ClientId}! Cannot spawn PetHand.");
                }
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
                // Get the player's transform to use as parent
                Transform playerTransform = player.transform; 

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
                combatPlayerObj.name = $"CombatPlayer_{player.GetSteamName()}"; // Name it on server for clarity
                combatPlayerObj.transform.SetParent(playerTransform, false); // Set parent BEFORE spawning
                Spawn(combatPlayerObj, player.Owner); // Assign ownership
                playerCombatPlayerObjects[player] = combatPlayerObj.GetComponent<NetworkObject>();
                Debug.Log($"[CombatManager] Spawned CombatPlayer for {player.GetSteamName()} under {playerTransform.name}");

                // --- Step 3c: Create Player Hand SECOND --- 
                GameObject playerHandObj = Instantiate(playerHandPrefab); 
                PlayerHand hand = playerHandObj.GetComponent<PlayerHand>();
                playerHandObj.name = $"PlayerHand_{player.GetSteamName()}"; // Name it on server
                playerHandObj.transform.SetParent(playerTransform, false); // Set parent BEFORE spawning
                Spawn(playerHandObj, player.Owner); // Assign ownership
                playerHandObjects[player] = playerHandObj.GetComponent<NetworkObject>();
                Debug.Log($"[CombatManager] Spawned PlayerHand for {player.GetSteamName()} under {playerTransform.name}");

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

                // --- Create a dedicated CombatSceneCanvas for this combat ---
                CreateCombatSceneCanvas(player, opponentPetOwner, playerCombatPets[player], playerCombatPets[opponentPetOwner]);
            }
            
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
        private void CreateCombatSceneCanvas(NetworkPlayer player, NetworkPlayer opponentPetOwner, CombatPet playerPet, CombatPet opponentPet)
        {
            if (combatSceneCanvasPrefab == null)
            {
                Debug.LogError("[CombatManager] Cannot create combat scene canvas: prefab is null");
                return;
            }
            
            // Create a new canvas for this specific combat
            GameObject canvasObj = Instantiate(combatSceneCanvasPrefab);
            canvasObj.name = $"CombatCanvas_{player.GetSteamName()}_vs_{opponentPetOwner.GetSteamName()}Pet";
            
            // Get the CombatSceneCanvas component
            CombatSceneCanvas sceneCanvas = canvasObj.GetComponent<CombatSceneCanvas>();
            if (sceneCanvas == null)
            {
                Debug.LogError("[CombatManager] CombatSceneCanvas component not found on prefab");
                Destroy(canvasObj);
                return;
            }
            
            // Spawn the canvas with network visibility
            Spawn(canvasObj);
            
            // Add to the list of canvases
            combatSceneCanvases.Add(sceneCanvas);
            
            // Get the CombatPlayer for this player (from activeCombats)
            CombatData combatData = null;
            if (activeCombats.ContainsKey(player))
            {
                combatData = activeCombats[player];
            }
            else
            {
                Debug.LogWarning($"[CombatManager] No CombatData found for player {player.GetSteamName()}");
            }
            
            // Initialize the canvas with combat references if data is available
            if (combatData != null)
            {
                sceneCanvas.Initialize(combatData.CombatPlayer, opponentPet, player, opponentPetOwner);
                Debug.Log($"[CombatManager] Created and initialized combat scene canvas for {player.GetSteamName()} vs {opponentPetOwner.GetSteamName()}'s pet");
            }
            else
            {
                Debug.LogError($"[CombatManager] Failed to initialize combat scene canvas: missing combat data for player {player.GetSteamName()}");
            }
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
            
            // Show all combat scene canvases
            foreach (CombatSceneCanvas canvas in combatSceneCanvases)
            {
                if (canvas != null)
                {
                    canvas.gameObject.SetActive(true);
                    
                    // Add animation to show the canvas
                    CanvasGroup canvasGroup = canvas.GetComponent<CanvasGroup>();
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = 0;
                        canvasGroup.DOFade(1, 0.5f);
                    }
                }
            }
            
            Debug.Log("[CombatManager] Combat canvases activated");
        }
        
        [ObserversRpc]
        private void RpcHideCombatCanvas()
        {
            Debug.Log("[CombatManager] RpcHideCombatCanvas received");
            
            // Hide all combat scene canvases
            foreach (CombatSceneCanvas canvas in combatSceneCanvases)
            {
                if (canvas != null)
                {
                    // Add animation to hide the canvas
                    CanvasGroup canvasGroup = canvas.GetComponent<CanvasGroup>();
                    if (canvasGroup != null)
                    {
                        canvasGroup.DOFade(0, 0.5f).OnComplete(() => 
                        {
                            canvas.gameObject.SetActive(false);
                        });
                    }
                    else
                    {
                        canvas.gameObject.SetActive(false);
                    }
                }
            }
            
            Debug.Log("[CombatManager] Combat canvases hidden");
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
            
            // Hide all combat scene canvases
            foreach (CombatSceneCanvas canvas in combatSceneCanvases)
            {
                if (canvas != null)
                {
                    // Add animation to hide the canvas
                    CanvasGroup canvasGroup = canvas.GetComponent<CanvasGroup>();
                    if (canvasGroup != null)
                    {
                        canvasGroup.DOFade(0, 0.5f).OnComplete(() => 
                        {
                            canvas.gameObject.SetActive(false);
                        });
                    }
                    else
                    {
                        canvas.gameObject.SetActive(false);
                    }
                }
            }
            
            // Reset the view index
            CombatSceneCanvas.ResetViewIndex();
        }
        
        #endregion
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