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
using System; // Added for Action
using System.Diagnostics; // Added for Conditional attribute

namespace Combat
{
    // Helper struct for Inspector visualization of combat assignments
    [System.Serializable]
    public struct CombatAssignmentInfo
    {
        public CombatPlayer Player;
        public CombatPet AssignedOpponentPet;
        public NetworkPlayer PlayerNetworkIdentity; // Optional: Add NetworkPlayer for easier identification
    }

    public class CombatManager : NetworkBehaviour
    {
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
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                UnityEngine.Debug.LogError("Multiple CombatManagers detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // UnityEngine.Debug.Log("[CombatManager] Awake called");
            ValidatePrefabs();
        }
        
        private void Start()
        {
            UnityEngine.Debug.Log("[CombatManager] Start called");
            
            // Verify combat canvas is set up properly
            if (combatCanvas != null)
            {
                UnityEngine.Debug.Log($"[CombatManager] Combat canvas found: {combatCanvas.name}");
                if (!combatCanvas.activeInHierarchy)
                {
                    UnityEngine.Debug.LogWarning("[CombatManager] Combat canvas is inactive in hierarchy");
                }
                
                // Check for canvas group for animations
                CanvasGroup canvasGroup = combatCanvas.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    UnityEngine.Debug.LogWarning("[CombatManager] Combat canvas has no CanvasGroup component, adding one");
                    canvasGroup = combatCanvas.AddComponent<CanvasGroup>();
                }
            }
            else
            {
                UnityEngine.Debug.LogError("[CombatManager] No combat canvas assigned");
            }
        }
        
        private void ValidatePrefabs()
        {
            if (combatPlayerPrefab == null)
                UnityEngine.Debug.LogError("[CombatManager] No combatPlayerPrefab assigned");
            
            if (combatPetPrefab == null)
                UnityEngine.Debug.LogError("[CombatManager] No combatPetPrefab assigned");
            
            if (playerHandPrefab == null)
                UnityEngine.Debug.LogError("[CombatManager] No playerHandPrefab assigned");
            
            if (petHandPrefab == null)
                UnityEngine.Debug.LogError("[CombatManager] No petHandPrefab assigned");
            
            // UnityEngine.Debug.Log($"[CombatManager] Prefabs validated - Player: {(combatPlayerPrefab != null ? "Valid" : "Missing")}, " +
            //           $"CombatPet: {(combatPetPrefab != null ? "Valid" : "Missing")}, " +
            //           $"Hand: {(playerHandPrefab != null ? "Valid" : "Missing")}, " +
            //           $"PetHand: {(petHandPrefab != null ? "Valid" : "Missing")}");
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
                UnityEngine.Debug.LogWarning("[CombatManager] Combat already started, ignoring duplicate call");
                return;
            }
            
            // Validate inputs
            if (players == null || players.Count < 2)
            {
                UnityEngine.Debug.LogError($"[CombatManager] Cannot start combat with less than 2 players. Players: {(players != null ? players.Count : 0)}");
                return;
            }
            
            UnityEngine.Debug.Log($"[CombatManager] Starting combat for {players.Count} players");
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
            UnityEngine.Debug.Log("[CombatManager] Phase 1: Instantiating all prefabs...");
            foreach (NetworkPlayer player in players)
            {
                // --- Instantiate CombatPet ---
                Pet persistentPet = player.playerPet.Value;
                if (persistentPet == null) {
                    UnityEngine.Debug.LogError($"[CombatManager] Player {player.GetSteamName()} has no persistent pet! Cannot instantiate CombatPet.");
                    continue;
                }
                if (combatPetPrefab == null) {
                    UnityEngine.Debug.LogError($"[CombatManager] combatPetPrefab is null! Cannot instantiate CombatPet for {player.GetSteamName()}.");
                    continue;
                }
                GameObject combatPetObj = Instantiate(combatPetPrefab); 
                CombatPet combatPet = combatPetObj.GetComponent<CombatPet>();
                if (combatPet == null) {
                    UnityEngine.Debug.LogError($"[CombatManager] combatPetPrefab is missing CombatPet component! Cannot instantiate for {player.GetSteamName()}.");
                    Destroy(combatPetObj);
                    continue;
                }
                combatPetObj.name = $"CombatPet_{persistentPet.PetName}_{player.GetSteamName()}";
                combatPetObjs[player] = combatPetObj;
                combatPets[player] = combatPet;

                // --- Instantiate PetHand ---
                 if (petHandPrefab == null) {
                    UnityEngine.Debug.LogError($"[CombatManager] petHandPrefab is null! Cannot instantiate PetHand for {player.GetSteamName()}.");
                    // continue; // Decide if we proceed without pet hand
                }
                else
                {
                    GameObject petHandObj = Instantiate(petHandPrefab);
                    PetHand petHand = petHandObj.GetComponent<PetHand>();
                    if (petHand == null) {
                        UnityEngine.Debug.LogError($"[CombatManager] petHandPrefab is missing PetHand component! Cannot instantiate for {player.GetSteamName()}.");
                        Destroy(petHandObj);
                        // continue; 
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
                    UnityEngine.Debug.LogError($"[CombatManager] combatPlayerPrefab is null! Cannot instantiate CombatPlayer for {player.GetSteamName()}.");
                    continue;
                }
                GameObject combatPlayerObj = Instantiate(combatPlayerPrefab);
                CombatPlayer combatPlayer = combatPlayerObj.GetComponent<CombatPlayer>();
                 if (combatPlayer == null) {
                    UnityEngine.Debug.LogError($"[CombatManager] combatPlayerPrefab is missing CombatPlayer component! Cannot instantiate for {player.GetSteamName()}.");
                    Destroy(combatPlayerObj);
                    continue;
                }
                combatPlayerObj.name = $"CombatPlayer_{player.GetSteamName()}";
                combatPlayerObjs[player] = combatPlayerObj;
                combatPlayers[player] = combatPlayer;

                // --- Instantiate PlayerHand ---
                 if (playerHandPrefab == null) {
                    UnityEngine.Debug.LogError($"[CombatManager] playerHandPrefab is null! Cannot instantiate PlayerHand for {player.GetSteamName()}.");
                    // continue; // Decide if we proceed without player hand
                    }
                    else
                    {
                    GameObject playerHandObj = Instantiate(playerHandPrefab);
                    Combat.PlayerHand playerHand = playerHandObj.GetComponent<Combat.PlayerHand>();
                     if (playerHand == null) {
                        UnityEngine.Debug.LogError($"[CombatManager] playerHandPrefab is missing PlayerHand component! Cannot instantiate for {player.GetSteamName()}.");
                        Destroy(playerHandObj);
                        // continue;
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
                
                // We need the CombatPet to create its deck, but CombatPet Initialize isn't called yet.
                // We'll create the pet deck during the Reference Linking phase after CombatPet is initialized.
                 // petDecks[player] = CreatePetRuntimeDeck(combatPet); // Moved later
            }
            UnityEngine.Debug.Log("[CombatManager] Phase 1: Instantiation Complete.");


            // --- Step 2: Spawn NetworkObjects ---
            UnityEngine.Debug.Log("[CombatManager] Phase 2: Spawning Network Objects...");
            foreach (NetworkPlayer player in players)
            {
                // Check if object exists before spawning
                if (combatPetObjs.TryGetValue(player, out GameObject cpObj)) Spawn(cpObj); else UnityEngine.Debug.LogError($"Missing CombatPet GameObject for {player.GetSteamName()} during spawn.");
                if (petHandObjs.TryGetValue(player, out GameObject phObj)) Spawn(phObj, player.Owner); else UnityEngine.Debug.LogWarning($"Missing PetHand GameObject for {player.GetSteamName()} during spawn."); // Warn because optional
                if (combatPlayerObjs.TryGetValue(player, out GameObject cplObj)) Spawn(cplObj, player.Owner); else UnityEngine.Debug.LogError($"Missing CombatPlayer GameObject for {player.GetSteamName()} during spawn.");
                if (playerHandObjs.TryGetValue(player, out GameObject plhObj)) Spawn(plhObj, player.Owner); else UnityEngine.Debug.LogWarning($"Missing PlayerHand GameObject for {player.GetSteamName()} during spawn."); // Warn because optional
            }
            UnityEngine.Debug.Log("[CombatManager] Phase 2: Spawning Complete.");


            // --- Step 3: Reparent NetworkObjects ---
            // Requires NetworkObjects to be spawned first.
            // We might need a small delay or yield here if parenting fails immediately after spawn.
            // Consider using a coroutine or delayed callback if RpcSetParent doesn't work reliably immediately.
            // For now, assume immediate parenting works.
            UnityEngine.Debug.Log("[CombatManager] Phase 3: Reparenting Network Objects...");
            foreach (NetworkPlayer player in players)
            {
                Pet persistentPet = player.playerPet.Value;
                NetworkObject playerNob = player.GetComponent<NetworkObject>();
                NetworkObject petNob = persistentPet?.GetComponent<NetworkObject>();

                if (playerNob == null) {
                     UnityEngine.Debug.LogError($"Player {player.GetSteamName()} missing NetworkObject for parenting!");
                     continue;
                }
                 if (petNob == null) {
                     UnityEngine.Debug.LogError($"Persistent Pet for {player.GetSteamName()} missing NetworkObject for parenting!");
                     // continue; // Can't parent pet things if petNob is null
                }
                
                // Reparent CombatPet (Child of Persistent Pet)
                 if (combatPets.TryGetValue(player, out CombatPet combatPet) && petNob != null) {
                     combatPet.RpcSetParent(petNob);
                     UnityEngine.Debug.Log($"Reparenting CombatPet for {player.GetSteamName()} under {persistentPet.name}");
                 } else if (petNob == null) {
                     UnityEngine.Debug.LogWarning($"Cannot reparent CombatPet for {player.GetSteamName()} - Persistent Pet NetworkObject missing.");
                 }

                // Reparent PetHand (Child of Persistent Pet)
                 if (petHands.TryGetValue(player, out PetHand petHand) && petNob != null) {
                     petHand.RpcSetParent(petNob);
                     UnityEngine.Debug.Log($"Reparenting PetHand for {player.GetSteamName()} under {persistentPet.name}");
                 } else if (petHands.ContainsKey(player) && petNob == null) { // Only warn if pethand exists but petNob is null
                     UnityEngine.Debug.LogWarning($"Cannot reparent PetHand for {player.GetSteamName()} - Persistent Pet NetworkObject missing.");
                 }

                // Reparent CombatPlayer (Child of NetworkPlayer)
                 if (combatPlayers.TryGetValue(player, out CombatPlayer combatPlayer)) {
                     combatPlayer.RpcSetParent(playerNob);
                     UnityEngine.Debug.Log($"Reparenting CombatPlayer for {player.GetSteamName()} under {player.name}");
                 }

                // Reparent PlayerHand (Child of NetworkPlayer)
                 if (playerHands.TryGetValue(player, out Combat.PlayerHand playerHand)) {
                     playerHand.RpcSetParent(playerNob);
                     UnityEngine.Debug.Log($"Reparenting PlayerHand for {player.GetSteamName()} under {player.name}");
                 }
            }
             UnityEngine.Debug.Log("[CombatManager] Phase 3: Reparenting Complete. (Note: May require delay if issues occur)");
            
            // --- Step 4: Assign Opponent Pets ---
            UnityEngine.Debug.Log("[CombatManager] Phase 4: Assigning Opponent Pets...");
            Dictionary<NetworkPlayer, NetworkPlayer> opponentAssignments = new Dictionary<NetworkPlayer, NetworkPlayer>();
            List<NetworkPlayer> shuffledPlayers = new List<NetworkPlayer>(players); // Use the actual players in combat
            ShuffleList(shuffledPlayers); // Ensure random assignment
            
            for (int i = 0; i < shuffledPlayers.Count; i++)
            {
                NetworkPlayer currentPlayer = shuffledPlayers[i];
                NetworkPlayer opponentPlayer = shuffledPlayers[(i + 1) % shuffledPlayers.Count]; // Next player in shuffled list wraps around
                opponentAssignments[currentPlayer] = opponentPlayer;
                UnityEngine.Debug.Log($"  - {currentPlayer.GetSteamName()} will fight against {opponentPlayer.GetSteamName()}'s pet.");
            }
             UnityEngine.Debug.Log("[CombatManager] Phase 4: Opponent Assignment Complete.");


            // --- Step 5: Link References & Initialize ---
            // Now that everything is spawned and (hopefully) reparented, link everything up.
             UnityEngine.Debug.Log("[CombatManager] Phase 5: Linking References and Initializing Components...");
            foreach (NetworkPlayer player in players)
            {
                // Get own components (handle potential missing components from instantiation phase)
                CombatPlayer ownCombatPlayer = combatPlayers.ContainsKey(player) ? combatPlayers[player] : null;
                Combat.PlayerHand ownPlayerHand = playerHands.ContainsKey(player) ? playerHands[player] : null;
                CombatPet ownCombatPet = combatPets.ContainsKey(player) ? combatPets[player] : null;
                PetHand ownPetHand = petHands.ContainsKey(player) ? petHands[player] : null;
                Pet persistentPet = player.playerPet.Value; // Already checked non-null during instantiation
                
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
                     petDecks[player] = ownCombatPet.PetDeck; // Store deck reference if needed elsewhere
                     UnityEngine.Debug.Log($"Created runtime deck for {player.GetSteamName()}'s pet.");

                    // Assign PetHand to CombatPet (Logical link)
                     if (ownPetHand != null) {
                         ownCombatPet.AssignHand(ownPetHand); // This method exists on CombatPet
                     }
                 }
                 else {
                     UnityEngine.Debug.LogError($"Cannot initialize or create deck for {player.GetSteamName()}'s pet - CombatPet instance is missing.");
                 }


                 // --- Initialize Hands ---
                 if (ownPlayerHand != null && ownCombatPlayer != null) {
                    // PlayerHand Initialize might need CombatPlayer reference
                     ownPlayerHand.Initialize(player, ownCombatPlayer); 
                     UnityEngine.Debug.Log($"Initialized PlayerHand for {player.GetSteamName()}.");
                 } else {
                     UnityEngine.Debug.LogWarning($"Could not initialize PlayerHand for {player.GetSteamName()} - PlayerHand or CombatPlayer missing.");
                 }
                 
                 if (ownPetHand != null && ownCombatPet != null) {
                     ownPetHand.Initialize(ownCombatPet); // Initialize PetHand with CombatPet reference
                     ownPetHand.DrawInitialHand(3); // Draw initial pet hand
                     UnityEngine.Debug.Log($"Initialized and drew initial PetHand for {player.GetSteamName()}.");
                 } else {
                      UnityEngine.Debug.LogWarning($"Could not initialize PetHand for {player.GetSteamName()} - PetHand or CombatPet missing.");
                 }

                 // --- Initialize CombatPlayer ---
                 RuntimeDeck playerDeck = playerDecks.ContainsKey(player) ? playerDecks[player] : null;
                 if (ownCombatPlayer != null) {
                     ownCombatPlayer.Initialize(player, ownCombatPet, opponentCombatPet, ownPlayerHand, playerDeck);
                     UnityEngine.Debug.Log($"Initialized CombatPlayer for {player.GetSteamName()}.");
                 } else {
                      UnityEngine.Debug.LogError($"Could not initialize CombatPlayer for {player.GetSteamName()} - CombatPlayer instance missing.");
                 }

                // --- Store references in CombatData ---
                // We still use CombatData for turn management, even with references on NetworkPlayer
                if (!activeCombats.ContainsKey(player)) {
                    activeCombats[player] = new CombatData();
                }
                // Populate CombatData (use the retrieved components)
                activeCombats[player].CombatPlayer = ownCombatPlayer;
                activeCombats[player].PlayerPet = ownCombatPet; // Renamed from PlayerPet for clarity if needed, but using existing name
                activeCombats[player].OpponentPet = opponentCombatPet;
                activeCombats[player].OpponentPlayer = opponentPlayer; 
                activeCombats[player].PlayerHand = ownPlayerHand; // Add player hand if not present
                activeCombats[player].PetHand = ownPetHand; // Add pet hand if needed
                activeCombats[player].TurnCompleted = false;
                activeCombats[player].CombatComplete = false;
                 UnityEngine.Debug.Log($"Populated CombatData for {player.GetSteamName()}.");


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
            UnityEngine.Debug.Log("[CombatManager] Phase 5: Linking and Initialization Complete.");

            // --- Populate Inspector List ---
            PopulateInspectorAssignments();

            // --- Step 6: Show Combat Canvas ---
            UnityEngine.Debug.Log("[CombatManager] Phase 6: Showing Combat Canvas...");
            RpcShowCombatCanvas();
            UnityEngine.Debug.Log("[CombatManager] Phase 6: Canvas Shown.");
            
            // --- Step 7: Start First Turns ---
            UnityEngine.Debug.Log("[CombatManager] Phase 7: Starting First Turns...");
            foreach (NetworkPlayer player in players) // Iterate through original player list for turn start
            {
                if (activeCombats.TryGetValue(player, out CombatData combatData) && combatData.CombatPlayer != null)
                {
                    combatData.CombatPlayer.StartTurn(); // StartTurn likely handles drawing the initial player hand now
                    UnityEngine.Debug.Log($"[CombatManager] Started turn for {player.GetSteamName()}.");
                }
                else
                {
                    UnityEngine.Debug.LogError($"[CombatManager] No combat data or CombatPlayer found for {player.GetSteamName()} when starting turn.");
                }
            }
            UnityEngine.Debug.Log("[CombatManager] Phase 7: First Turns Started.");
            
            // Log the final combat state
            LogCombatState();
            UnityEngine.Debug.Log("[CombatManager] Combat Setup Complete.");
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
                        PlayerNetworkIdentity = kvp.Key // Add the NetworkPlayer reference
                    });
                }
                else {
                    UnityEngine.Debug.LogWarning("[CombatManager] Skipping null entry when populating inspector assignments.");
                }
            }
            UnityEngine.Debug.Log($"[CombatManager] Updated Inspector assignment list with {inspectorCombatAssignments.Count} entries.");
        }

        // Helper to create player runtime deck (extracted logic)
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
                        else UnityEngine.Debug.LogWarning($"[CombatManager] Could not find CardData for ID {cardID}");
                    }
                    deck.Shuffle();
                    UnityEngine.Debug.Log($"[CombatManager] Created runtime deck with {deck.DrawPileCount} cards for {player.GetSteamName()} from persistent data.");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[CombatManager] Player {player.GetSteamName()} has no persistent deck. Using starter deck.");
                    deck = DeckManager.Instance.GetPlayerStarterDeck(); // Returns a shuffled deck
                    if (deck == null) { // Handle case where starter deck is also missing
                         UnityEngine.Debug.LogError($"[CombatManager] Player starter deck is null in DeckManager!");
                         deck = new RuntimeDeck(player.GetSteamName() + "'s Empty Deck", DeckType.PlayerDeck);
                    }
                }
            }
            else
            {
                UnityEngine.Debug.LogError("[CombatManager] DeckManager is null, cannot create player runtime deck.");
                deck = new RuntimeDeck(player.GetSteamName() + "'s Empty Deck", DeckType.PlayerDeck);
            }
            return deck;
        }
        
        // Helper to create pet runtime deck (extracted logic) - Now called after CombatPet Initialize
        // [Server] // No longer needed here, called within the main loop
        // private RuntimeDeck CreatePetRuntimeDeck(CombatPet combatPet)
        // {
        //     if (combatPet == null || combatPet.ReferencePet == null) {
        //          UnityEngine.Debug.LogError("[CombatManager] Cannot create pet deck - CombatPet or ReferencePet is null.");
        //          return new RuntimeDeck("Empty Pet Deck", DeckType.PetDeck);
        //     }
             
        //     RuntimeDeck deck = null;
        //     if (DeckManager.Instance != null) {
        //         deck = combatPet.ReferencePet.CreateRuntimeDeck(); // Assuming Pet has this method
        //         if(deck == null || deck.TotalCardCount == 0) { // Fallback if persistent pet deck is empty
        //             UnityEngine.Debug.LogWarning($"[CombatManager] Persistent pet deck for {combatPet.name} is empty or null. Using random starter pet deck.");
        //             deck = DeckManager.Instance.GetRandomPetStarterDeck();
        //         }
        //         deck.Shuffle(); // Ensure it's shuffled
        //         UnityEngine.Debug.Log($"[CombatManager] Created runtime deck for {combatPet.name} with {deck.DrawPileCount} cards.");
        //     } else {
        //         UnityEngine.Debug.LogError("[CombatManager] DeckManager is null, cannot create pet runtime deck.");
        //         deck = new RuntimeDeck("Empty Pet Deck", DeckType.PetDeck);
        //     }
        //     return deck;
        // }
        
        [Server]
        private void LogCombatState()
        {
            UnityEngine.Debug.Log("[Combat] === Combat State After Initialization ===");
            UnityEngine.Debug.Log($"[Combat] Total players in combat: {playersInCombat.Count}");
            UnityEngine.Debug.Log($"[Combat] Total active combats: {activeCombats.Count}");
            
            foreach (var kvp in activeCombats)
            {
                NetworkPlayer player = kvp.Key;
                CombatData data = kvp.Value;
                
                UnityEngine.Debug.Log($"[Combat] Player: {player.GetSteamName()}");
                UnityEngine.Debug.Log($"[Combat] - Opponent: {data.OpponentPlayer.GetSteamName()}");
                UnityEngine.Debug.Log($"[Combat] - Player Pet HP: {data.PlayerPet.CurrentHealth}/{data.PlayerPet.MaxHealth}");
                UnityEngine.Debug.Log($"[Combat] - Opponent Pet HP: {data.OpponentPet.CurrentHealth}/{data.OpponentPet.MaxHealth}");
                UnityEngine.Debug.Log($"[Combat] - Is Turn Active: {data.CombatPlayer.IsMyTurn}");
                UnityEngine.Debug.Log($"[Combat] - Energy: {data.CombatPlayer.CurrentEnergy}/{data.CombatPlayer.MaxEnergy}");
            }
            
            UnityEngine.Debug.Log("[Combat] === End Combat State ===");
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
                UnityEngine.Debug.LogError("[CombatManager] Could not find NetworkPlayer for CombatPlayer that ended turn");
                return;
            }
            
            UnityEngine.Debug.Log($"[CombatManager] Player {networkPlayer.GetSteamName()} ended their turn");
            
            // Mark this player's turn as completed
            if (activeCombats.TryGetValue(networkPlayer, out CombatData combatData))
            {
                combatData.TurnCompleted = true;
                
                // Find the opponent and start their turn
                NetworkPlayer opponentPlayer = combatData.OpponentPlayer;
                if (activeCombats.TryGetValue(opponentPlayer, out CombatData opponentData))
                {
                    opponentData.CombatPlayer.StartTurn();
                    UnityEngine.Debug.Log($"[CombatManager] Started turn for opponent {opponentPlayer.GetSteamName()}");
                }
                else
                {
                    UnityEngine.Debug.LogError($"[CombatManager] No combat data found for opponent {opponentPlayer.GetSteamName()}");
                }
            }
        }
        
        // Complete a player's combat (win or lose)
        [Server]
        public void CompleteCombat(NetworkPlayer player, bool victory)
        {
            if (!activeCombats.ContainsKey(player))
            {
                UnityEngine.Debug.LogError($"[CombatManager] Tried to complete combat for player {player.GetSteamName()}, but no active combat found");
                return;
            }
            
            UnityEngine.Debug.Log($"[CombatManager] Completing combat for player {player.GetSteamName()}, Victory: {victory}");
            
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
                
                UnityEngine.Debug.Log($"[CombatManager] Marked combat as complete for {player.GetSteamName()}, Win: {win}");
                
                // Check if all combats have ended
                CheckCombatEndState();
            }
            else
            {
                UnityEngine.Debug.LogError($"[CombatManager] No combat data found for {player.GetSteamName()} when marking complete");
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
            
            UnityEngine.Debug.Log($"[CombatManager] Player {player.GetSteamName()}'s pet took {damage} damage. " +
                      $"Health: {combatData.PlayerPet.CurrentHealth}/{combatData.PlayerPet.MaxHealth}");
            
            // Check if player's pet is defeated
            if (combatData.PlayerPet.IsDefeated())
            {
                // Player lost the combat
                RpcShowCombatResult(player.Owner, false);
                // Mark combat as complete for this player
                combatData.CombatComplete = true; 
                
                UnityEngine.Debug.Log($"[CombatManager] Player {player.GetSteamName()}'s pet was defeated");
                
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
            UnityEngine.Debug.Log($"[CombatManager] Combat pet defeat registered");
            
            // Find the combat data for this pet
            foreach (var kvp in activeCombats)
            {
                if (kvp.Value.PlayerPet == combatPet)
                {
                    // Player's pet was defeated, they lost
                    UnityEngine.Debug.Log($"[CombatManager] Player {kvp.Key.GetSteamName()}'s pet was defeated (player lost)");
                    RpcShowCombatResult(kvp.Key.Owner, false);
                    kvp.Value.CombatComplete = true;
                }
                else if (kvp.Value.OpponentPet == combatPet)
                {
                    // Opponent's pet was defeated, player won
                    UnityEngine.Debug.Log($"[CombatManager] Opponent's pet was defeated, player {kvp.Key.GetSteamName()} won");
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
                UnityEngine.Debug.Log("[CombatManager] All combats complete, ending combat phase");
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
            // --- Option 1: Use NetworkPlayer reference (if it's reliable) ---
             if (player != null && player.OpponentCombatPet != null) {
                 return player.OpponentCombatPet;
             }

            // --- Option 2: Fallback to activeCombats dictionary (existing method) ---
            if (activeCombats.TryGetValue(player, out CombatData combatData))
            {
                return combatData.OpponentPet;
            }
            
            // --- Option 3: Look up opponent via assignment and then their pet ---
            // This requires opponentAssignments to be stored if needed outside StartCombat
            // if (opponentAssignments.TryGetValue(player, out NetworkPlayer opponent) && 
            //     combatPets.TryGetValue(opponent, out CombatPet oppPet)) {
            //     return oppPet;
            // }

            UnityEngine.Debug.LogWarning($"[CombatManager] Could not find opponent pet for {player?.GetSteamName()} using any method.");
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
                int k = UnityEngine.Random.Range(0, n + 1); // Explicitly use UnityEngine.Random
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
            UnityEngine.Debug.Log("[CombatManager] RpcShowCombatCanvas received");
            
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
                
                UnityEngine.Debug.Log("[CombatManager] Combat canvas activated");
            }
            else
            {
                UnityEngine.Debug.LogError("[CombatManager] Cannot show combat canvas - reference is null");
            }
        }
        
        [ObserversRpc]
        private void RpcHideCombatCanvas()
        {
            UnityEngine.Debug.Log("[CombatManager] RpcHideCombatCanvas received");
            
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
                
                UnityEngine.Debug.Log("[CombatManager] Combat canvas hidden");
            }
            else
            {
                UnityEngine.Debug.LogError("[CombatManager] Cannot hide combat canvas - reference is null");
            }
        }
        
        [TargetRpc]
        private void RpcShowOpponentAttacking(NetworkConnection conn, int damage)
        {
            UnityEngine.Debug.Log($"[CombatManager] RpcShowOpponentAttacking received with damage: {damage}");
            // Implement client-side animation showing opponent attacking
        }
        
        [TargetRpc]
        private void RpcShowCombatResult(NetworkConnection conn, bool victory)
        {
            string result = victory ? "Victory" : "Defeat";
            UnityEngine.Debug.Log($"[CombatManager] RpcShowCombatResult received: {result}");
            
            // Find the combat canvas manager
            CombatCanvasManager canvasManager = FindFirstObjectByType<CombatCanvasManager>(); // <--- Find the manager
            if (canvasManager != null)
            {
                canvasManager.ShowCombatResult(victory); // <--- Call the method
                UnityEngine.Debug.Log($"[CombatManager] Showing combat result UI: {result}");
            }
            else
            {
                UnityEngine.Debug.LogError("[CombatManager] Cannot show combat result - CombatCanvasManager not found");
            }
        }
        
        [ObserversRpc]
        private void RpcEndCombat()
        {
            UnityEngine.Debug.Log("[CombatManager] RpcEndCombat received");
            
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