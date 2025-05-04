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
using System;

namespace Combat
{
    // Helper struct for Inspector visualization of combat assignments
    [System.Serializable]
    public struct CombatAssignmentInfo
    {
        public CombatPlayer Player;
        public CombatPet AssignedOpponentPet;
        public NetworkPlayer PlayerNetworkIdentity;
    }

    public class CombatManager : NetworkBehaviour
    {
        #region Singleton and References
        public static CombatManager Instance { get; private set; }
        
        [Header("Prefabs")]
        [SerializeField] private GameObject combatPlayerPrefab;
        [SerializeField] private GameObject combatPetPrefab;
        [SerializeField] private GameObject playerHandPrefab;
        [SerializeField] private GameObject petHandPrefab;
        
        [Header("References")]
        [SerializeField] private GameObject combatCanvas;
        
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
        
        // List for Inspector visibility
        [SerializeField] private List<CombatAssignmentInfo> inspectorCombatAssignments = new List<CombatAssignmentInfo>();
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[FALLBACK] Multiple CombatManagers detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            ValidatePrefabs();
        }
        
        private void Start()
        {
            // Verify combat canvas is set up properly
            if (combatCanvas != null)
            {
                if (!combatCanvas.activeInHierarchy)
                {
                    Debug.LogWarning("[FALLBACK] Combat canvas is inactive in hierarchy");
                }
                
                // Check for canvas group for animations
                CanvasGroup canvasGroup = combatCanvas.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    Debug.LogWarning("[FALLBACK] Combat canvas has no CanvasGroup component, adding one");
                    canvasGroup = combatCanvas.AddComponent<CanvasGroup>();
                }
            }
            else
            {
                Debug.LogError("[FALLBACK] No combat canvas assigned");
            }
        }
        
        private void ValidatePrefabs()
        {
            if (combatPlayerPrefab == null)
                Debug.LogError("[FALLBACK] No combatPlayerPrefab assigned");
            
            if (combatPetPrefab == null)
                Debug.LogError("[FALLBACK] No combatPetPrefab assigned");
            
            if (playerHandPrefab == null)
                Debug.LogError("[FALLBACK] No playerHandPrefab assigned");
            
            if (petHandPrefab == null)
                Debug.LogError("[FALLBACK] No petHandPrefab assigned");
        }
        #endregion

        #region Public Accessors
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
        #endregion

        #region Combat Initialization
        [Server]
        public void StartCombatForPlayers(List<NetworkPlayer> players)
        {
            if (combatStarted) 
            {
                Debug.LogWarning("[FALLBACK] Combat already started, ignoring duplicate call");
                return;
            }
            
            // Validate inputs
            if (players == null || players.Count < 2)
            {
                Debug.LogError($"[FALLBACK] Cannot start combat with less than 2 players. Players: {(players != null ? players.Count : 0)}");
                return;
            }
            
            playersInCombat.Clear(); // Clear previous list if any
            playersInCombat.AddRange(players);
            activeCombats.Clear(); // Clear previous combat data
            combatStarted = true;
            
            // --- Dictionaries to hold temporary references during setup ---
            Dictionary<NetworkPlayer, GameObject> combatPlayerObjs = new Dictionary<NetworkPlayer, GameObject>();
            Dictionary<NetworkPlayer, GameObject> playerHandObjs = new Dictionary<NetworkPlayer, GameObject>();
            Dictionary<NetworkPlayer, GameObject> combatPetObjs = new Dictionary<NetworkPlayer, GameObject>();
            Dictionary<NetworkPlayer, GameObject> petHandObjs = new Dictionary<NetworkPlayer, GameObject>();
            
            Dictionary<NetworkPlayer, CombatPlayer> combatPlayers = new Dictionary<NetworkPlayer, CombatPlayer>();
            Dictionary<NetworkPlayer, Combat.PlayerHand> playerHands = new Dictionary<NetworkPlayer, Combat.PlayerHand>();
            Dictionary<NetworkPlayer, CombatPet> combatPets = new Dictionary<NetworkPlayer, CombatPet>();
            Dictionary<NetworkPlayer, PetHand> petHands = new Dictionary<NetworkPlayer, PetHand>();
            Dictionary<NetworkPlayer, RuntimeDeck> playerDecks = new Dictionary<NetworkPlayer, RuntimeDeck>();
            Dictionary<NetworkPlayer, RuntimeDeck> petDecks = new Dictionary<NetworkPlayer, RuntimeDeck>();

            // --- Step 1: Instantiate all prefabs ---
            Debug.Log("[FALLBACK] Phase 1: Instantiating all prefabs...");
            foreach (NetworkPlayer player in players)
            {
                // --- Instantiate CombatPet ---
                Pet persistentPet = player.playerPet.Value;
                if (persistentPet == null) {
                    Debug.LogError($"[FALLBACK] Player {player.GetSteamName()} has no persistent pet! Cannot instantiate CombatPet.");
                    continue;
                }
                if (combatPetPrefab == null) {
                    Debug.LogError($"[FALLBACK] combatPetPrefab is null! Cannot instantiate CombatPet for {player.GetSteamName()}.");
                    continue;
                }
                GameObject combatPetObj = Instantiate(combatPetPrefab); 
                CombatPet combatPet = combatPetObj.GetComponent<CombatPet>();
                if (combatPet == null) {
                    Debug.LogError($"[FALLBACK] combatPetPrefab is missing CombatPet component! Cannot instantiate for {player.GetSteamName()}.");
                    Destroy(combatPetObj);
                    continue;
                }
                combatPetObj.name = $"CombatPet_{persistentPet.PetName}_{player.GetSteamName()}";
                combatPetObjs[player] = combatPetObj;
                combatPets[player] = combatPet;

                // --- Instantiate PetHand ---
                 if (petHandPrefab == null) {
                    Debug.LogError($"[FALLBACK] petHandPrefab is null! Cannot instantiate PetHand for {player.GetSteamName()}.");
                }
                else
                {
                    GameObject petHandObj = Instantiate(petHandPrefab);
                    PetHand petHand = petHandObj.GetComponent<PetHand>();
                    if (petHand == null) {
                        Debug.LogError($"[FALLBACK] petHandPrefab is missing PetHand component! Cannot instantiate for {player.GetSteamName()}.");
                        Destroy(petHandObj);
                    }
                    else 
                    {
                        petHandObj.name = $"PetHand_{player.GetSteamName()}";
                        petHandObjs[player] = petHandObj;
                        petHands[player] = petHand;
                    }
                }

                // --- Instantiate CombatPlayer ---
                 if (combatPlayerPrefab == null) {
                    Debug.LogError($"[FALLBACK] combatPlayerPrefab is null! Cannot instantiate CombatPlayer for {player.GetSteamName()}.");
                    continue;
                }
                GameObject combatPlayerObj = Instantiate(combatPlayerPrefab);
                CombatPlayer combatPlayer = combatPlayerObj.GetComponent<CombatPlayer>();
                 if (combatPlayer == null) {
                    Debug.LogError($"[FALLBACK] combatPlayerPrefab is missing CombatPlayer component! Cannot instantiate for {player.GetSteamName()}.");
                    Destroy(combatPlayerObj);
                    continue;
                }
                combatPlayerObj.name = $"CombatPlayer_{player.GetSteamName()}";
                combatPlayerObjs[player] = combatPlayerObj;
                combatPlayers[player] = combatPlayer;

                // --- Instantiate PlayerHand ---
                 if (playerHandPrefab == null) {
                    Debug.LogError($"[FALLBACK] playerHandPrefab is null! Cannot instantiate PlayerHand for {player.GetSteamName()}.");
                    }
                    else
                    {
                    GameObject playerHandObj = Instantiate(playerHandPrefab);
                    Combat.PlayerHand playerHand = playerHandObj.GetComponent<Combat.PlayerHand>();
                     if (playerHand == null) {
                        Debug.LogError($"[FALLBACK] playerHandPrefab is missing PlayerHand component! Cannot instantiate for {player.GetSteamName()}.");
                        Destroy(playerHandObj);
                }
                else
                {
                        playerHandObj.name = $"PlayerHand_{player.GetSteamName()}";
                        playerHandObjs[player] = playerHandObj;
                        playerHands[player] = playerHand;
                    }
                }
                
                // --- Create Runtime Decks (Can happen during instantiation phase) ---
                playerDecks[player] = CreatePlayerRuntimeDeck(player);
            }

            // --- Step 2: Spawn NetworkObjects ---
            Debug.Log("[FALLBACK] Phase 2: Spawning Network Objects...");
            foreach (NetworkPlayer player in players)
            {
                // Check if object exists before spawning
                if (combatPetObjs.TryGetValue(player, out GameObject cpObj)) Spawn(cpObj); else Debug.LogError($"[FALLBACK] Missing CombatPet GameObject for {player.GetSteamName()} during spawn.");
                if (petHandObjs.TryGetValue(player, out GameObject phObj)) Spawn(phObj, player.Owner); else Debug.LogWarning($"[FALLBACK] Missing PetHand GameObject for {player.GetSteamName()} during spawn.");
                if (combatPlayerObjs.TryGetValue(player, out GameObject cplObj)) Spawn(cplObj, player.Owner); else Debug.LogError($"[FALLBACK] Missing CombatPlayer GameObject for {player.GetSteamName()} during spawn.");
                if (playerHandObjs.TryGetValue(player, out GameObject plhObj)) Spawn(plhObj, player.Owner); else Debug.LogWarning($"[FALLBACK] Missing PlayerHand GameObject for {player.GetSteamName()} during spawn.");
            }

            // --- Step 3: Reparent NetworkObjects ---
            Debug.Log("[FALLBACK] Phase 3: Reparenting Network Objects...");
            foreach (NetworkPlayer player in players)
            {
                Pet persistentPet = player.playerPet.Value;
                NetworkObject playerNob = player.GetComponent<NetworkObject>();
                NetworkObject petNob = persistentPet?.GetComponent<NetworkObject>();

                if (playerNob == null) {
                     Debug.LogError($"[FALLBACK] Player {player.GetSteamName()} missing NetworkObject for parenting!");
                     continue;
                }
                 if (petNob == null) {
                     Debug.LogError($"[FALLBACK] Persistent Pet for {player.GetSteamName()} missing NetworkObject for parenting!");
                }
                
                // Reparent CombatPet (Child of Persistent Pet)
                 if (combatPets.TryGetValue(player, out CombatPet combatPet) && petNob != null) {
                     combatPet.RpcSetParent(petNob);
                 } else if (petNob == null) {
                     Debug.LogWarning($"[FALLBACK] Cannot reparent CombatPet for {player.GetSteamName()} - Persistent Pet NetworkObject missing.");
                 }

                // Reparent PetHand (Child of Persistent Pet)
                 if (petHands.TryGetValue(player, out PetHand petHand) && petNob != null) {
                     petHand.RpcSetParent(petNob);
                 } else if (petHands.ContainsKey(player) && petNob == null) {
                     Debug.LogWarning($"[FALLBACK] Cannot reparent PetHand for {player.GetSteamName()} - Persistent Pet NetworkObject missing.");
                 }

                // Reparent CombatPlayer (Child of NetworkPlayer)
                 if (combatPlayers.TryGetValue(player, out CombatPlayer combatPlayer)) {
                     combatPlayer.RpcSetParent(playerNob);
                 }

                // Reparent PlayerHand (Child of NetworkPlayer)
                 if (playerHands.TryGetValue(player, out Combat.PlayerHand playerHand)) {
                     playerHand.RpcSetParent(playerNob);
                 }
            }
            
            // --- Step 4: Assign Opponent Pets ---
            Debug.Log("[FALLBACK] Phase 4: Assigning Opponent Pets...");
            Dictionary<NetworkPlayer, NetworkPlayer> opponentAssignments = new Dictionary<NetworkPlayer, NetworkPlayer>();
            List<NetworkPlayer> shuffledPlayers = new List<NetworkPlayer>(players);
            ShuffleList(shuffledPlayers); // Ensure random assignment
            
            for (int i = 0; i < shuffledPlayers.Count; i++)
            {
                NetworkPlayer currentPlayer = shuffledPlayers[i];
                NetworkPlayer opponentPlayer = shuffledPlayers[(i + 1) % shuffledPlayers.Count]; // Next player in shuffled list wraps around
                opponentAssignments[currentPlayer] = opponentPlayer;
            }

            // --- Step 5: Link References & Initialize ---
            Debug.Log("[FALLBACK] Phase 5: Linking References and Initializing Components...");
            foreach (NetworkPlayer player in players)
            {
                // Get own components
                CombatPlayer ownCombatPlayer = combatPlayers.ContainsKey(player) ? combatPlayers[player] : null;
                Combat.PlayerHand ownPlayerHand = playerHands.ContainsKey(player) ? playerHands[player] : null;
                CombatPet ownCombatPet = combatPets.ContainsKey(player) ? combatPets[player] : null;
                PetHand ownPetHand = petHands.ContainsKey(player) ? petHands[player] : null;
                Pet persistentPet = player.playerPet.Value;
                
                // Get opponent components using the assignment map
                NetworkPlayer opponentPlayer = opponentAssignments[player];
                CombatPet opponentCombatPet = combatPets.ContainsKey(opponentPlayer) ? combatPets[opponentPlayer] : null;
                PetHand opponentPetHand = petHands.ContainsKey(opponentPlayer) ? petHands[opponentPlayer] : null;

                 // --- Create Pet Runtime Deck (Now that CombatPet exists) ---
                 if (ownCombatPet != null)
                 {
                    // Initialize CombatPet first (needs referencePet)
                     ownCombatPet.Initialize(persistentPet); 
                     // Now create the deck
                     ownCombatPet.CreateRuntimeDeck(); 
                     petDecks[player] = ownCombatPet.PetDeck;

                    // Assign PetHand to CombatPet
                     if (ownPetHand != null) {
                         ownCombatPet.AssignHand(ownPetHand);
                     }
                 }
                 else {
                     Debug.LogError($"[FALLBACK] Cannot initialize or create deck for {player.GetSteamName()}'s pet - CombatPet instance is missing.");
                 }

                 // --- Initialize Hands ---
                 if (ownPlayerHand != null && ownCombatPlayer != null) {
                     ownPlayerHand.Initialize(player, ownCombatPlayer); 
                 } else {
                     Debug.LogWarning($"[FALLBACK] Could not initialize PlayerHand for {player.GetSteamName()} - PlayerHand or CombatPlayer missing.");
                 }
                 
                 if (ownPetHand != null && ownCombatPet != null) {
                     ownPetHand.Initialize(ownCombatPet);
                     ownPetHand.DrawInitialHand(3); // Draw initial pet hand
                 } else {
                      Debug.LogWarning($"[FALLBACK] Could not initialize PetHand for {player.GetSteamName()} - PetHand or CombatPet missing.");
                 }

                 // --- Initialize CombatPlayer ---
                 RuntimeDeck playerDeck = playerDecks.ContainsKey(player) ? playerDecks[player] : null;
                 if (ownCombatPlayer != null) {
                     ownCombatPlayer.Initialize(player, ownCombatPet, opponentCombatPet, ownPlayerHand, playerDeck);
                 } else {
                      Debug.LogError($"[FALLBACK] Could not initialize CombatPlayer for {player.GetSteamName()} - CombatPlayer instance missing.");
                 }

                // --- Store references in CombatData ---
                if (!activeCombats.ContainsKey(player)) {
                    activeCombats[player] = new CombatData();
                }
                // Populate CombatData
                activeCombats[player].CombatPlayer = ownCombatPlayer;
                activeCombats[player].PlayerPet = ownCombatPet;
                activeCombats[player].OpponentPet = opponentCombatPet;
                activeCombats[player].OpponentPlayer = opponentPlayer; 
                activeCombats[player].PlayerHand = ownPlayerHand;
                activeCombats[player].PetHand = ownPetHand;
                activeCombats[player].TurnCompleted = false;
                activeCombats[player].CombatComplete = false;

                // --- Link references on NetworkPlayer itself ---
                player.SetCombatReferences(
                    ownCombatPlayer,
                    ownPlayerHand,
                    ownCombatPet,
                    ownPetHand,
                    opponentPlayer,
                    opponentCombatPet,
                    opponentPetHand
                );

                // Also set the synced opponent reference for the client UI to find
                player.SyncedOpponentPlayer.Value = opponentPlayer;
            }

            // --- Populate Inspector List ---
            PopulateInspectorAssignments();

            // --- Step 6: Show Combat Canvas ---
            RpcShowCombatCanvas();
            
            // --- Step 7: Start First Turns ---
            foreach (NetworkPlayer player in players)
            {
                if (activeCombats.TryGetValue(player, out CombatData combatData) && combatData.CombatPlayer != null)
                {
                    combatData.CombatPlayer.StartTurn();
                }
                else
                {
                    Debug.LogError($"[FALLBACK] No combat data or CombatPlayer found for {player.GetSteamName()} when starting turn.");
                }
            }
            
            // Log the final combat state
            LogCombatState();
        }

        // Helper method to update the inspector list based on activeCombats
        [Server]
        private void PopulateInspectorAssignments()
        {
            inspectorCombatAssignments.Clear();
            foreach (var kvp in activeCombats)
            {
                if (kvp.Key != null && kvp.Value != null && kvp.Value.CombatPlayer != null && kvp.Value.OpponentPet != null)
                {
                    inspectorCombatAssignments.Add(new CombatAssignmentInfo 
                    { 
                        Player = kvp.Value.CombatPlayer, 
                        AssignedOpponentPet = kvp.Value.OpponentPet,
                        PlayerNetworkIdentity = kvp.Key
                    });
                }
                else {
                    Debug.LogWarning("[FALLBACK] Skipping null entry when populating inspector assignments.");
                }
            }
        }

        // Helper to create player runtime deck
        [Server]
        private RuntimeDeck CreatePlayerRuntimeDeck(NetworkPlayer player)
        {
            RuntimeDeck deck = null;
            if (DeckManager.Instance != null)
            {
                deck = new RuntimeDeck(player.GetSteamName() + "'s Combat Deck", DeckType.PlayerDeck);
                if (player.persistentDeckCardIDs.Count > 0)
                {
                    foreach (string cardID in player.persistentDeckCardIDs)
                    {
                        CardData cardData = DeckManager.Instance.FindCardByName(cardID);
                        if (cardData != null) deck.AddCard(cardData);
                        else Debug.LogWarning($"[FALLBACK] Could not find CardData for ID {cardID}");
                    }
                    deck.Shuffle();
                }
                else
                {
                    Debug.LogWarning($"[FALLBACK] Player {player.GetSteamName()} has no persistent deck. Using starter deck.");
                    deck = DeckManager.Instance.GetPlayerStarterDeck();
                    if (deck == null) {
                         Debug.LogError($"[FALLBACK] Player starter deck is null in DeckManager!");
                         deck = new RuntimeDeck(player.GetSteamName() + "'s Empty Deck", DeckType.PlayerDeck);
                    }
                }
            }
            else
            {
                Debug.LogError("[FALLBACK] DeckManager is null, cannot create player runtime deck.");
                deck = new RuntimeDeck(player.GetSteamName() + "'s Empty Deck", DeckType.PlayerDeck);
            }
            return deck;
        }
        
        [Server]
        private void LogCombatState()
        {
            Debug.Log("[FALLBACK] === Combat State After Initialization ===");
            foreach (var kvp in activeCombats)
            {
                NetworkPlayer player = kvp.Key;
                CombatData data = kvp.Value;
                
                Debug.Log($"[FALLBACK] Player: {player.GetSteamName()}, Opponent: {data.OpponentPlayer.GetSteamName()}, " +
                          $"Pet HP: {data.PlayerPet.CurrentHealth}/{data.PlayerPet.MaxHealth}, " +
                          $"Opponent Pet HP: {data.OpponentPet.CurrentHealth}/{data.OpponentPet.MaxHealth}");
            }
        }
        #endregion

        #region Turn Management
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
                Debug.LogError("[FALLBACK] Could not find NetworkPlayer for CombatPlayer that ended turn");
                return;
            }
            
            // Mark this player's turn as completed
            if (activeCombats.TryGetValue(networkPlayer, out CombatData combatData))
            {
                combatData.TurnCompleted = true;
                
                // Now start the pet's turn
                CombatPet opponentPet = combatData.OpponentPet;
                if (opponentPet != null)
                {
                    opponentPet.StartTurn();
                }
                else
                {
                    Debug.LogError($"[FALLBACK] Cannot start pet turn - opponent pet for {networkPlayer.GetSteamName()} is null");
                }
            }
        }
        
        // Called when a pet ends their turn
        [Server]
        public void PetEndedTurn(CombatPet pet)
        {
            // Find the player combat data that has this pet as the opponent
            NetworkPlayer petOwner = null;
            NetworkPlayer opponentPlayer = null;
            
            foreach (var kvp in activeCombats)
            {
                if (kvp.Value.OpponentPet == pet)
                {
                    opponentPlayer = kvp.Key;
                    petOwner = kvp.Value.OpponentPlayer;
                    break;
                }
            }
            
            if (opponentPlayer == null || petOwner == null)
            {
                Debug.LogError("[FALLBACK] Could not find players for PetEndedTurn");
                return;
            }
            
            // Give the turn back to the player
            if (activeCombats.TryGetValue(opponentPlayer, out CombatData combatData))
            {
                // Reset turn completed flag for next round
                combatData.TurnCompleted = false;
                
                // Draw new cards for the player
                CombatPlayer playerCombat = combatData.CombatPlayer;
                if (playerCombat != null)
                {
                    // Draw cards for the player
                    playerCombat.DrawCards(3);
                    
                    // Start the player's turn using the new public method
                    playerCombat.SetTurn(true);
                    
                    // Also send RPC for more reliable turn updates
                    playerCombat.RpcNotifyTurnChanged(true);
                }
                else
                {
                    Debug.LogError($"[FALLBACK] Cannot start player turn - combatPlayer for {opponentPlayer.GetSteamName()} is null");
                }
            }
        }
        #endregion

        #region Combat Resolution
        // Complete a player's combat (win or lose)
        [Server]
        public void CompleteCombat(NetworkPlayer player, bool victory)
        {
            if (!activeCombats.ContainsKey(player))
            {
                Debug.LogError($"[FALLBACK] Tried to complete combat for player {player.GetSteamName()}, but no active combat found");
                return;
            }
            
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
                
                // Check if all combats have ended
                CheckCombatEndState();
            }
            else
            {
                Debug.LogError($"[FALLBACK] No combat data found for {player.GetSteamName()} when marking complete");
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
        public void HandlePetDefeat(CombatPet combatPet)
        {
            // Find the combat data for this pet
            foreach (var kvp in activeCombats)
            {
                if (kvp.Value.PlayerPet == combatPet)
                {
                    // Player's pet was defeated, they lost
                    RpcShowCombatResult(kvp.Key.Owner, false);
                    kvp.Value.CombatComplete = true;
                }
                else if (kvp.Value.OpponentPet == combatPet)
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
                Debug.Log("[FALLBACK] All combats complete, ending combat phase");
                RpcEndCombat();
                
                // Reset combat state
                combatStarted = false;
                
                // Clear references on NetworkPlayers involved
                foreach(NetworkPlayer p in playersInCombat)
                {
                    if (p != null) {
                        p.ClearCombatReferences();
                        p.SyncedOpponentPlayer.Value = null; // Clear synced opponent too
                    }
                }
                
                activeCombats.Clear();
                playersInCombat.Clear();
                inspectorCombatAssignments.Clear(); // Clear the inspector list too
                
                // On server, notify LobbyManager that combat is over
                if (LobbyManager.Instance != null)
                {
                    LobbyManager.Instance.OnCombatEnded();
                }
            }
        }
        #endregion

        #region Helper Methods
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
            // --- Option 1: Use NetworkPlayer reference (if it's reliable) ---
             if (player != null && player.OpponentCombatPet != null) {
                 return player.OpponentCombatPet;
             }

            // --- Option 2: Fallback to activeCombats dictionary (existing method) ---
            if (activeCombats.TryGetValue(player, out CombatData combatData))
            {
                return combatData.OpponentPet;
            }
            
            Debug.LogWarning($"[FALLBACK] Could not find opponent pet for {player?.GetSteamName()} using any method.");
            return null;
        }
        
        // Fisher-Yates shuffle algorithm
        public static void ShuffleList<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = UnityEngine.Random.Range(0, n + 1);
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
            else
            {
                Debug.LogError("[FALLBACK] Cannot show combat canvas - reference is null");
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
            else
            {
                Debug.LogError("[FALLBACK] Cannot hide combat canvas - reference is null");
            }
        }
        
        [TargetRpc]
        private void RpcShowOpponentAttacking(NetworkConnection conn, int damage)
        {
            // Implement client-side animation showing opponent attacking
        }
        
        [TargetRpc]
        private void RpcShowCombatResult(NetworkConnection conn, bool victory)
        {
            string result = victory ? "Victory" : "Defeat";
            
            // Find the combat canvas manager
            CombatCanvasManager canvasManager = FindFirstObjectByType<CombatCanvasManager>();
            if (canvasManager != null)
            {
                canvasManager.ShowCombatResult(victory);
            }
            else
            {
                Debug.LogError("[FALLBACK] Cannot show combat result - CombatCanvasManager not found");
            }
        }
        
        [ObserversRpc]
        private void RpcEndCombat()
        {
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
        public Combat.PlayerHand PlayerHand;
        public bool TurnCompleted;
        public bool CombatComplete;
        public PetHand PetHand;
    }
} 