using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using MVPScripts.Utility;

namespace CharacterSelection
{
    /// <summary>
    /// Manages visual formation of OTHER players' character/pet models behind local player
    /// Uses selectedModelSpawn as focal point, integrates with existing ModelDissolveAnimator
    /// Handles host+client case properly - no formation if no other players exist
    /// Does NOT modify materials - leaves all material effects to ModelDissolveAnimator
    /// </summary>
    public class OtherPlayersFormationManager : MonoBehaviour
    {
        [Header("Formation Configuration")]
        [SerializeField] private float baseDistance = 4f; // Distance from focal point to first row
        [SerializeField] private float rowSpacing = 3f; // Distance between rows
        [SerializeField] private float pairSpacing = 1.5f; // Space between character and pet in a pair
        [SerializeField] private float playerSpacing = 4f; // Space between different players in same row
        [SerializeField] private float arcAngle = 120f; // Total arc angle for formation (degrees)
        
        [Header("Model Management")]
        [SerializeField] private Transform formationRoot; // Parent for all other players' models
        
        // Integration references - set during initialization
        private Transform focalPoint; // Reference to selectedModelSpawn from CharacterSelectionUIManager
        private Dictionary<string, ModelDissolveAnimator> slotAnimators = new Dictionary<string, ModelDissolveAnimator>(); // One animator per formation slot
        private CharacterSelectionManager selectionManager;
        private string myPlayerID;
        
        // Formation data
        private Dictionary<string, OtherPlayerModels> otherPlayerModels = new Dictionary<string, OtherPlayerModels>();
        private List<FormationSlot> formationSlots = new List<FormationSlot>();
        private bool isInitialized = false;
        
        // State tracking for optimization
        private int lastPlayerCount = 0;
        private bool needsFormationRecalculation = false;
        
        // Events for debugging and integration
        public System.Action<string, GameObject> OnOtherPlayerModelSpawned;
        public System.Action<string, GameObject> OnOtherPlayerModelRemoved;
        public System.Action<int> OnFormationPlayerCountChanged;
        
        #region Data Structures
        
        [System.Serializable]
        public class OtherPlayerModels
        {
            public string playerID;
            public string playerName;
            public GameObject characterModel;
            public GameObject petModel;
            public int characterIndex = -1;
            public int petIndex = -1;
            public int formationSlotIndex = -1; // Which slot pair this player occupies
            public bool hasCharacter = false;
            public bool hasPet = false;
            public Color playerColor = Color.white;
            
            public OtherPlayerModels(string playerID, string playerName)
            {
                this.playerID = playerID;
                this.playerName = playerName;
            }
        }
        
        [System.Serializable]
        public class FormationSlot
        {
            public Vector3 characterPosition;
            public Vector3 petPosition;
            public Quaternion characterRotation;
            public Quaternion petRotation;
            public bool isOccupied = false;
            public string occupyingPlayerID = "";
            
            public void AssignToPlayer(string playerID)
            {
                isOccupied = true;
                occupyingPlayerID = playerID;
            }
            
            public void Clear()
            {
                isOccupied = false;
                occupyingPlayerID = "";
            }
        }
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initialize the formation manager with required dependencies
        /// Creates its own dedicated ModelDissolveAnimator for formation animations
        /// </summary>
        public void Initialize(Transform focalPoint, CharacterSelectionManager manager, string myPlayerID)
        {
            this.focalPoint = focalPoint;
            this.selectionManager = manager;
            this.myPlayerID = myPlayerID;
            
            // Create formation root if needed
            if (formationRoot == null)
            {
                GameObject rootObj = new GameObject("OtherPlayersFormation");
                rootObj.transform.SetParent(transform);
                formationRoot = rootObj.transform;
            }
            
            // Initialize slot animators container
            InitializeSlotAnimators();
            
            // Validate dependencies
            if (focalPoint == null)
            {
                Debug.LogError("OtherPlayersFormationManager: focalPoint is null! Cannot initialize formation.");
                return;
            }
            
            // Note: Slot animators are created on-demand when models are spawned
            
            if (selectionManager == null)
            {
                Debug.LogError("OtherPlayersFormationManager: selectionManager is null! Cannot create models.");
                return;
            }
            
            if (string.IsNullOrEmpty(myPlayerID))
            {
                Debug.LogError("OtherPlayersFormationManager: myPlayerID is null or empty! Cannot filter local player.");
                return;
            }
            
            isInitialized = true;
            Debug.Log($"OtherPlayersFormationManager: Initialized for player {myPlayerID} with focal point at {focalPoint.position} and per-slot animation system");
        }
        
        /// <summary>
        /// Initialize the slot animators container (animators created on-demand)
        /// </summary>
        private void InitializeSlotAnimators()
        {
            slotAnimators.Clear();
            Debug.Log("OtherPlayersFormationManager: Initialized per-slot animation system - each formation position gets its own animator");
        }
        
        /// <summary>
        /// Get or create a ModelDissolveAnimator for a specific formation slot
        /// This ensures each formation position has its own independent animation queue
        /// </summary>
        private ModelDissolveAnimator GetSlotAnimator(int slotIndex, bool isCharacter)
        {
            string slotKey = $"Slot_{slotIndex}_{(isCharacter ? "Character" : "Pet")}";
            
            if (!slotAnimators.ContainsKey(slotKey))
            {
                // Create a child GameObject to hold this slot's animator
                GameObject animatorHolder = new GameObject($"SlotAnimator_{slotIndex}_{(isCharacter ? "Char" : "Pet")}");
                animatorHolder.transform.SetParent(transform);
                
                // Add and configure the ModelDissolveAnimator component
                ModelDissolveAnimator slotAnimator = animatorHolder.AddComponent<ModelDissolveAnimator>();
                slotAnimators[slotKey] = slotAnimator;
                
                Debug.Log($"OtherPlayersFormationManager: Created dedicated animator for {slotKey} - independent animation queue");
            }
            
            return slotAnimators[slotKey];
        }
        
        #endregion
        
        #region Public Interface
        
        /// <summary>
        /// Main update method called when player selections change
        /// Handles host+client case - if no other players, no formation is created
        /// </summary>
        public void UpdateFormation(List<PlayerSelectionInfo> allPlayers, string currentMyPlayerID)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("OtherPlayersFormationManager: UpdateFormation called before initialization");
                return;
            }
            
            // Update player ID if it changed
            if (!string.IsNullOrEmpty(currentMyPlayerID))
            {
                myPlayerID = currentMyPlayerID;
            }
            
            // Filter out local player using more robust logic
            // Check both playerName directly and if playerName contains the player ID
            var otherPlayers = allPlayers.Where(p => 
                p.playerName != myPlayerID && 
                !p.playerName.Contains($"({myPlayerID})")
            ).ToList();
            
            Debug.Log($"OtherPlayersFormationManager: UpdateFormation called - Total: {allPlayers.Count}, Others: {otherPlayers.Count}, My ID: {myPlayerID}");
            
            // Host+client case: if no other players, clear formation and exit
            if (otherPlayers.Count == 0)
            {
                if (otherPlayerModels.Count > 0)
                {
                    Debug.Log("OtherPlayersFormationManager: No other players - clearing formation (host+client case)");
                    ClearAllFormationModels();
                }
                return;
            }
            
            // Handle player count changes (additions/removals)
            HandlePlayerChanges(otherPlayers);
            
            // Recalculate formation IMMEDIATELY if needed (before trying to create models)
            if (needsFormationRecalculation)
            {
                RecalculateFormation();
                needsFormationRecalculation = false;
                
                // IMPORTANT: If formation recalculation triggered repositioning animations,
                // delay new model creation to avoid visual artifacts from overlapping animations
                bool hasRepositioningAnimations = HasActiveRepositioningAnimations();
                if (hasRepositioningAnimations)
                {
                    Debug.Log("OtherPlayersFormationManager: Repositioning animations in progress - delaying new model creation");
                    StartCoroutine(DelayedUpdatePlayerModels(otherPlayers, 0.1f)); // Small delay to let repositioning start
                    return;
                }
            }
            
            // Update individual player selections (now that slot indices are assigned)
            foreach (var player in otherPlayers)
            {
                UpdatePlayerModels(player);
            }
        }
        
        /// <summary>
        /// Force cleanup of all formation models (called during scene transitions, etc.)
        /// </summary>
        public void CleanupFormation()
        {
            Debug.Log("OtherPlayersFormationManager: Cleaning up formation");
            
            // Stop any running animations across all slot animators
            foreach (var slotAnimator in slotAnimators.Values)
            {
                if (slotAnimator != null)
                {
                    slotAnimator.StopTransition();
                }
            }
            
            // Destroy all other player models
            ClearAllFormationModels();
            
            Debug.Log("OtherPlayersFormationManager: Formation cleanup completed");
        }
        
        #endregion
        
        #region Formation Management
        
        private void HandlePlayerChanges(List<PlayerSelectionInfo> otherPlayers)
        {
            var currentPlayerIDs = new HashSet<string>(otherPlayers.Select(p => p.playerName));
            var trackedPlayerIDs = new HashSet<string>(otherPlayerModels.Keys);
            
            // Handle player additions
            foreach (var player in otherPlayers)
            {
                if (!trackedPlayerIDs.Contains(player.playerName))
                {
                    Debug.Log($"OtherPlayersFormationManager: Adding new player to formation: {player.playerName}");
                    AddNewPlayer(player);
                    needsFormationRecalculation = true;
                }
            }
            
            // Handle player removals
            var playersToRemove = trackedPlayerIDs.Except(currentPlayerIDs).ToList();
            foreach (string playerID in playersToRemove)
            {
                Debug.Log($"OtherPlayersFormationManager: Removing player from formation: {playerID}");
                RemovePlayer(playerID);
                needsFormationRecalculation = true;
            }
            
            // Notify if player count changed
            if (otherPlayers.Count != lastPlayerCount)
            {
                lastPlayerCount = otherPlayers.Count;
                OnFormationPlayerCountChanged?.Invoke(lastPlayerCount);
            }
        }
        
        private void AddNewPlayer(PlayerSelectionInfo player)
        {
            var playerData = new OtherPlayerModels(player.playerName, player.playerName);
            // Don't set indices yet - leave them at -1 so UpdatePlayerModels will detect changes
            
            otherPlayerModels[player.playerName] = playerData;
            
            Debug.Log($"OtherPlayersFormationManager: Added player {player.playerName} - will create models for Character: {player.characterIndex}, Pet: {player.petIndex}");
        }
        
        private void RemovePlayer(string playerID)
        {
            if (otherPlayerModels.ContainsKey(playerID))
            {
                var playerData = otherPlayerModels[playerID];
                
                // Animate out and destroy models
                if (playerData.characterModel != null)
                {
                    AnimateModelOut(playerData.characterModel, () => Destroy(playerData.characterModel));
                    OnOtherPlayerModelRemoved?.Invoke(playerID, playerData.characterModel);
                }
                
                if (playerData.petModel != null)
                {
                    AnimateModelOut(playerData.petModel, () => Destroy(playerData.petModel));
                    OnOtherPlayerModelRemoved?.Invoke(playerID, playerData.petModel);
                }
                
                // Free up formation slot
                if (playerData.formationSlotIndex >= 0 && playerData.formationSlotIndex < formationSlots.Count)
                {
                    formationSlots[playerData.formationSlotIndex].Clear();
                }
                
                otherPlayerModels.Remove(playerID);
                Debug.Log($"OtherPlayersFormationManager: Removed player {playerID} from formation");
            }
        }
        
        private void UpdatePlayerModels(PlayerSelectionInfo player)
        {
            if (!otherPlayerModels.ContainsKey(player.playerName))
            {
                // This should have been handled in HandlePlayerChanges, but safety check
                Debug.LogWarning($"OtherPlayersFormationManager: UpdatePlayerModels called for unknown player: {player.playerName}");
                return;
            }
            
            var playerData = otherPlayerModels[player.playerName];
            
            Debug.Log($"OtherPlayersFormationManager: UpdatePlayerModels for {player.playerName} - Current: Char={playerData.characterIndex}, Pet={playerData.petIndex} | New: Char={player.characterIndex}, Pet={player.petIndex}");
            
            // Handle character selection change
            if (player.characterIndex != playerData.characterIndex)
            {
                Debug.Log($"OtherPlayersFormationManager: Character change for {player.playerName}: {playerData.characterIndex} -> {player.characterIndex}");
                HandleCharacterChange(playerData, player);
            }
            else
            {
                Debug.Log($"OtherPlayersFormationManager: No character change for {player.playerName} (both {player.characterIndex})");
            }
            
            // Handle pet selection change
            if (player.petIndex != playerData.petIndex)
            {
                Debug.Log($"OtherPlayersFormationManager: Pet change for {player.playerName}: {playerData.petIndex} -> {player.petIndex}");
                HandlePetChange(playerData, player);
            }
            else
            {
                Debug.Log($"OtherPlayersFormationManager: No pet change for {player.playerName} (both {player.petIndex})");
            }
        }
        
        #endregion
        
        #region Model Creation and Animation
        
        private void HandleCharacterChange(OtherPlayerModels playerData, PlayerSelectionInfo newInfo)
        {
            GameObject oldModel = playerData.characterModel;
            
            // Update tracking immediately
            playerData.characterIndex = newInfo.characterIndex;
            playerData.hasCharacter = newInfo.characterIndex >= 0;
            
            if (newInfo.characterIndex < 0)
            {
                // Character deselected - just animate out
                if (oldModel != null)
                {
                    AnimateModelOut(oldModel, () => {
                        Destroy(oldModel);
                        playerData.characterModel = null;
                    });
                }
                return;
            }
            
            // Character selected or changed - use slot-specific animation system
            ModelDissolveAnimator slotAnimator = GetSlotAnimator(playerData.formationSlotIndex, true); // true = character
            
            if (slotAnimator != null)
            {
                if (oldModel == null)
                {
                    // First spawn - just dissolve in the new model
                    GameObject newModel = CreateCharacterModelForOtherPlayer(newInfo.characterIndex, playerData.formationSlotIndex);
                    if (newModel != null)
                    {
                        PrepareModelForAnimation(newModel);
                        playerData.characterModel = newModel;
                        slotAnimator.AnimateModelIn(newModel, () => {
                            Debug.Log($"OtherPlayersFormationManager: Character spawn completed for {playerData.playerName} in slot {playerData.formationSlotIndex}");
                        });
                    }
                }
                else
                {
                    // Model change - use proper transition (don't pass oldModel to avoid double fade-out)
                    slotAnimator.AnimateModelTransitionWithFactory(
                        null, // Let the animation system handle the current model detection automatically
                        () => {
                            GameObject newModel = CreateCharacterModelForOtherPlayer(newInfo.characterIndex, playerData.formationSlotIndex);
                            if (newModel != null)
                            {
                                PrepareModelForAnimation(newModel);
                            }
                            return newModel;
                        },
                        () => {
                            // Animation completed callback
                            Debug.Log($"OtherPlayersFormationManager: Character transition completed for {playerData.playerName} in slot {playerData.formationSlotIndex}");
                            
                            // Update the reference to the new model
                            var newModel = FindLatestCharacterModel(playerData.formationSlotIndex);
                            if (newModel != null)
                            {
                                playerData.characterModel = newModel;
                            }
                            
                            // Cleanup old model reference
                            if (oldModel != null)
                            {
                                Destroy(oldModel);
                            }
                        }
                    );
                }
            }
            else
            {
                // Fallback without animation
                Debug.LogWarning($"OtherPlayersFormationManager: Could not get slot animator for character in slot {playerData.formationSlotIndex} - creating model without animation");
                if (oldModel != null)
                {
                    Destroy(oldModel);
                }
                
                playerData.characterModel = CreateCharacterModelForOtherPlayer(newInfo.characterIndex, playerData.formationSlotIndex);
            }
        }
        
        private void HandlePetChange(OtherPlayerModels playerData, PlayerSelectionInfo newInfo)
        {
            GameObject oldModel = playerData.petModel;
            
            // Update tracking immediately
            playerData.petIndex = newInfo.petIndex;
            playerData.hasPet = newInfo.petIndex >= 0;
            
            if (newInfo.petIndex < 0)
            {
                // Pet deselected - just animate out
                if (oldModel != null)
                {
                    AnimateModelOut(oldModel, () => {
                        Destroy(oldModel);
                        playerData.petModel = null;
                    });
                }
                return;
            }
            
            // Pet selected or changed - use slot-specific animation system
            ModelDissolveAnimator slotAnimator = GetSlotAnimator(playerData.formationSlotIndex, false); // false = pet
            
            if (slotAnimator != null)
            {
                if (oldModel == null)
                {
                    // First spawn - just dissolve in the new model
                    GameObject newModel = CreatePetModelForOtherPlayer(newInfo.petIndex, playerData.formationSlotIndex);
                    if (newModel != null)
                    {
                        PrepareModelForAnimation(newModel);
                        playerData.petModel = newModel;
                        slotAnimator.AnimateModelIn(newModel, () => {
                            Debug.Log($"OtherPlayersFormationManager: Pet spawn completed for {playerData.playerName} in slot {playerData.formationSlotIndex}");
                        });
                    }
                }
                else
                {
                    // Model change - use proper transition (don't pass oldModel to avoid double fade-out)
                    slotAnimator.AnimateModelTransitionWithFactory(
                        null, // Let the animation system handle the current model detection automatically
                        () => {
                            GameObject newModel = CreatePetModelForOtherPlayer(newInfo.petIndex, playerData.formationSlotIndex);
                            if (newModel != null)
                            {
                                PrepareModelForAnimation(newModel);
                            }
                            return newModel;
                        },
                        () => {
                            // Animation completed callback
                            Debug.Log($"OtherPlayersFormationManager: Pet transition completed for {playerData.playerName} in slot {playerData.formationSlotIndex}");
                            
                            // Update the reference to the new model
                            var newModel = FindLatestPetModel(playerData.formationSlotIndex);
                            if (newModel != null)
                            {
                                playerData.petModel = newModel;
                            }
                            
                            // Cleanup old model reference
                            if (oldModel != null)
                            {
                                Destroy(oldModel);
                            }
                        }
                    );
                }
            }
            else
            {
                // Fallback without animation
                Debug.LogWarning($"OtherPlayersFormationManager: Could not get slot animator for pet in slot {playerData.formationSlotIndex} - creating model without animation");
                if (oldModel != null)
                {
                    Destroy(oldModel);
                }
                
                playerData.petModel = CreatePetModelForOtherPlayer(newInfo.petIndex, playerData.formationSlotIndex);
            }
        }
        
        private GameObject CreateCharacterModelForOtherPlayer(int characterIndex, int slotIndex)
        {
            if (selectionManager == null || characterIndex < 0)
            {
                Debug.LogWarning($"OtherPlayersFormationManager: Cannot create character model - manager null or invalid index: {characterIndex}");
                return null;
            }
            
            if (slotIndex < 0 || slotIndex >= formationSlots.Count)
            {
                Debug.LogWarning($"OtherPlayersFormationManager: Invalid slot index for character: {slotIndex}");
                return null;
            }
            
            GameObject prefab = selectionManager.GetCharacterPrefabByIndex(characterIndex);
            if (prefab == null)
            {
                Debug.LogWarning($"OtherPlayersFormationManager: No character prefab found for index: {characterIndex}");
                return null;
            }
            
            FormationSlot slot = formationSlots[slotIndex];
            GameObject model = Instantiate(prefab, formationRoot);
            model.transform.position = slot.characterPosition;
            model.transform.rotation = slot.characterRotation;
            model.name = $"OtherPlayer_Character_{characterIndex}_{slotIndex}";
            
            // NO MATERIAL MODIFICATIONS - let ModelDissolveAnimator handle all effects
            
            OnOtherPlayerModelSpawned?.Invoke("character", model);
            Debug.Log($"OtherPlayersFormationManager: Created character model at slot {slotIndex}: {model.name}");
            
            return model;
        }
        
        private GameObject CreatePetModelForOtherPlayer(int petIndex, int slotIndex)
        {
            if (selectionManager == null || petIndex < 0)
            {
                Debug.LogWarning($"OtherPlayersFormationManager: Cannot create pet model - manager null or invalid index: {petIndex}");
                return null;
            }
            
            if (slotIndex < 0 || slotIndex >= formationSlots.Count)
            {
                Debug.LogWarning($"OtherPlayersFormationManager: Invalid slot index for pet: {slotIndex}");
                return null;
            }
            
            GameObject prefab = selectionManager.GetPetPrefabByIndex(petIndex);
            if (prefab == null)
            {
                Debug.LogWarning($"OtherPlayersFormationManager: No pet prefab found for index: {petIndex}");
                return null;
            }
            
            FormationSlot slot = formationSlots[slotIndex];
            GameObject model = Instantiate(prefab, formationRoot);
            model.transform.position = slot.petPosition;
            model.transform.rotation = slot.petRotation;
            model.name = $"OtherPlayer_Pet_{petIndex}_{slotIndex}";
            
            // NO MATERIAL MODIFICATIONS - let ModelDissolveAnimator handle all effects
            
            OnOtherPlayerModelSpawned?.Invoke("pet", model);
            Debug.Log($"OtherPlayersFormationManager: Created pet model at slot {slotIndex}: {model.name}");
            
            return model;
        }
        
        private GameObject FindLatestCharacterModel(int slotIndex)
        {
            // Find the most recently created character model for this slot
            string expectedName = $"OtherPlayer_Character_";
            Transform[] children = formationRoot.GetComponentsInChildren<Transform>();
            
            foreach (Transform child in children)
            {
                if (child.name.StartsWith(expectedName) && child.name.EndsWith($"_{slotIndex}"))
                {
                    return child.gameObject;
                }
            }
            return null;
        }
        
        private GameObject FindLatestPetModel(int slotIndex)
        {
            // Find the most recently created pet model for this slot
            string expectedName = $"OtherPlayer_Pet_";
            Transform[] children = formationRoot.GetComponentsInChildren<Transform>();
            
            foreach (Transform child in children)
            {
                if (child.name.StartsWith(expectedName) && child.name.EndsWith($"_{slotIndex}"))
                {
                    return child.gameObject;
                }
            }
            return null;
        }
        
        private void AnimateModelOut(GameObject model, System.Action onComplete)
        {
            if (model != null)
            {
                // Find which slot this model belongs to by parsing its name
                // Expected format: "OtherPlayer_Character_X_Y" or "OtherPlayer_Pet_X_Y" where Y is the slot index
                string[] nameParts = model.name.Split('_');
                if (nameParts.Length >= 4 && int.TryParse(nameParts[3], out int slotIndex))
                {
                    bool isCharacter = nameParts[1] == "Character";
                    ModelDissolveAnimator slotAnimator = GetSlotAnimator(slotIndex, isCharacter);
                    
                    if (slotAnimator != null)
                    {
                        slotAnimator.AnimateModelOut(model, onComplete);
                        return;
                    }
                }
                
                Debug.LogWarning($"OtherPlayersFormationManager: Could not determine slot for model {model.name} - skipping animation");
            }
            
            onComplete?.Invoke();
        }
        
        #endregion
        
        #region Formation Calculation
        
        private void RecalculateFormation()
        {
            if (focalPoint == null)
            {
                Debug.LogError("OtherPlayersFormationManager: Cannot recalculate formation - focalPoint is null");
                return;
            }
            
            int playerCount = otherPlayerModels.Count;
            if (playerCount == 0)
            {
                formationSlots.Clear();
                return;
            }
            
            Debug.Log($"OtherPlayersFormationManager: Recalculating formation for {playerCount} other players");
            
            // Check if any players have existing models that need to be faded out first
            var playersWithModels = otherPlayerModels.Values.Where(p => p.characterModel != null || p.petModel != null).ToList();
            if (playersWithModels.Count > 0)
            {
                Debug.Log($"OtherPlayersFormationManager: {playersWithModels.Count} players have existing models - starting coordinated fade out/in sequence");
                StartCoordinatedFormationRebuild(playerCount);
                return;
            }
            
            // No existing models, just calculate and assign normally
            CalculateFormationPositions(playerCount);
            AssignPlayersToSlots();
            Debug.Log($"OtherPlayersFormationManager: Formation recalculated - {formationSlots.Count} slots created");
        }
        
        private void CalculateFormationPositions(int totalOtherPlayers)
        {
            formationSlots.Clear();
            
            // Calculate tiered arc formation
            int playersPerRow = 2; // Start with 2 players per row
            int currentRow = 1; // Start from row 1 (row 0 is local player)
            int playersProcessed = 0;
            
            while (playersProcessed < totalOtherPlayers)
            {
                int playersInThisRow = Mathf.Min(playersPerRow, totalOtherPlayers - playersProcessed);
                CreateRowPositions(currentRow, playersInThisRow);
                
                playersProcessed += playersInThisRow;
                currentRow++;
                playersPerRow += 1; // Each row can fit one more player than the previous
            }
        }
        
        private void CreateRowPositions(int rowIndex, int playersInRow)
        {
            float rowDistance = baseDistance + (rowIndex * rowSpacing);
            float arcStep = playersInRow > 1 ? arcAngle / (playersInRow - 1) : 0f;
            
            for (int i = 0; i < playersInRow; i++)
            {
                // Calculate angle for this player (spread across arc)
                float angle = playersInRow > 1 ? (i * arcStep) - (arcAngle * 0.5f) : 0f;
                
                // Convert to world position relative to focal point (behind the main character)
                Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                Vector3 basePosition = focalPoint.position + (direction * rowDistance);
                
                // Create slot pair for this player (character + pet side by side)
                FormationSlot slot = new FormationSlot();
                slot.characterPosition = basePosition + (focalPoint.right * -pairSpacing * 0.5f);
                slot.petPosition = basePosition + (focalPoint.right * pairSpacing * 0.5f);
                
                // Both models face toward the focal point
                slot.characterRotation = Quaternion.LookRotation(focalPoint.position - slot.characterPosition);
                slot.petRotation = Quaternion.LookRotation(focalPoint.position - slot.petPosition);
                
                formationSlots.Add(slot);
            }
        }
        
        private void AssignPlayersToSlots()
        {
            var playerList = otherPlayerModels.Values.ToList();
            
            Debug.Log($"OtherPlayersFormationManager: AssignPlayersToSlots - {playerList.Count} players, {formationSlots.Count} slots");
            
            for (int i = 0; i < playerList.Count && i < formationSlots.Count; i++)
            {
                var player = playerList[i];
                var slot = formationSlots[i];
                
                Debug.Log($"OtherPlayersFormationManager: Assigning player {player.playerName} to slot {i}");
                
                // Assign new slot to player
                player.formationSlotIndex = i;
                slot.AssignToPlayer(player.playerID);
                
                // Update model positions (no animation needed here - handled by coordinated rebuild)
                UpdatePlayerModelPositions(player, slot);
            }
        }
        
        private void UpdatePlayerModelPositions(OtherPlayerModels player, FormationSlot slot)
        {
            if (player.characterModel != null)
            {
                player.characterModel.transform.position = slot.characterPosition;
                player.characterModel.transform.rotation = slot.characterRotation;
            }
            
            if (player.petModel != null)
            {
                player.petModel.transform.position = slot.petPosition;
                player.petModel.transform.rotation = slot.petRotation;
            }
        }
        
        /// <summary>
        /// Animates a player's models from their old slot positions to new slot positions
        /// using dissolve out/in animations when formation recalculates
        /// </summary>
        private void AnimatePlayerReposition(OtherPlayerModels player, FormationSlot newSlot, int oldSlotIndex)
        {
            // Animate character model reposition if it exists
            if (player.characterModel != null && player.characterIndex >= 0)
            {
                ModelDissolveAnimator oldCharAnimator = GetSlotAnimator(oldSlotIndex, true);
                ModelDissolveAnimator newCharAnimator = GetSlotAnimator(player.formationSlotIndex, true);
                
                if (oldCharAnimator != null && newCharAnimator != null)
                {
                    // Create factory for repositioned character model
                    System.Func<GameObject> charFactory = () => {
                        GameObject repositionedModel = CreateCharacterModelForOtherPlayer(player.characterIndex, player.formationSlotIndex);
                        return repositionedModel;
                    };
                    
                    // Animate transition from old slot to new slot
                    oldCharAnimator.AnimateModelTransitionWithFactory(
                        null, // Let system detect current model
                        charFactory,
                        () => {
                            // Update reference to new model
                            var newModel = FindLatestCharacterModel(player.formationSlotIndex);
                            if (newModel != null)
                            {
                                player.characterModel = newModel;
                            }
                            Debug.Log($"OtherPlayersFormationManager: Character reposition completed for {player.playerName} to slot {player.formationSlotIndex}");
                        }
                    );
                }
            }
            
            // Animate pet model reposition if it exists
            if (player.petModel != null && player.petIndex >= 0)
            {
                ModelDissolveAnimator oldPetAnimator = GetSlotAnimator(oldSlotIndex, false);
                ModelDissolveAnimator newPetAnimator = GetSlotAnimator(player.formationSlotIndex, false);
                
                if (oldPetAnimator != null && newPetAnimator != null)
                {
                    // Create factory for repositioned pet model
                    System.Func<GameObject> petFactory = () => {
                        GameObject repositionedModel = CreatePetModelForOtherPlayer(player.petIndex, player.formationSlotIndex);
                        return repositionedModel;
                    };
                    
                    // Animate transition from old slot to new slot
                    oldPetAnimator.AnimateModelTransitionWithFactory(
                        null, // Let system detect current model
                        petFactory,
                        () => {
                            // Update reference to new model
                            var newModel = FindLatestPetModel(player.formationSlotIndex);
                            if (newModel != null)
                            {
                                player.petModel = newModel;
                            }
                            Debug.Log($"OtherPlayersFormationManager: Pet reposition completed for {player.playerName} to slot {player.formationSlotIndex}");
                        }
                    );
                }
            }
        }
        
        /// <summary>
        /// Coordinated formation rebuild: fade out all existing models, then fade in all models at new positions
        /// </summary>
        private void StartCoordinatedFormationRebuild(int playerCount)
        {
            // Pre-calculate new formation positions
            CalculateFormationPositions(playerCount);
            
            // Collect all existing models that need to fade out
            var modelsToFadeOut = new List<GameObject>();
            var playerData = new List<(OtherPlayerModels player, int newSlotIndex)>();
            
            var playerList = otherPlayerModels.Values.ToList();
            for (int i = 0; i < playerList.Count && i < formationSlots.Count; i++)
            {
                var player = playerList[i];
                playerData.Add((player, i));
                
                if (player.characterModel != null)
                    modelsToFadeOut.Add(player.characterModel);
                if (player.petModel != null)
                    modelsToFadeOut.Add(player.petModel);
            }
            
            if (modelsToFadeOut.Count == 0)
            {
                // No models to fade out, just assign slots normally
                AssignPlayersToSlots();
                return;
            }
            
            Debug.Log($"OtherPlayersFormationManager: Starting coordinated rebuild - fading out {modelsToFadeOut.Count} existing models");
            
            // Start fade out for all existing models simultaneously
            StartCoroutine(CoordinatedFormationRebuildSequence(playerData, modelsToFadeOut));
        }
        
        /// <summary>
        /// Coroutine to handle the coordinated fade out → fade in sequence
        /// </summary>
        private System.Collections.IEnumerator CoordinatedFormationRebuildSequence(
            List<(OtherPlayerModels player, int newSlotIndex)> playerData, 
            List<GameObject> modelsToFadeOut)
        {
            // Phase 1: Fade out all existing models simultaneously
            var fadeOutTasks = new List<System.Action>();
            foreach (var model in modelsToFadeOut)
            {
                if (model != null)
                {
                    fadeOutTasks.Add(() => {
                        AnimateModelOut(model, () => {
                            // Model faded out successfully
                        });
                    });
                }
            }
            
            // Execute all fade outs
            foreach (var task in fadeOutTasks)
            {
                task();
            }
            
            // Wait for fade out animations to complete (typical fade duration)
            yield return new WaitForSeconds(1.0f);
            
            Debug.Log("OtherPlayersFormationManager: All models faded out - now assigning new slots and fading in");
            
            // Phase 2: Clear old model references and assign new slots
            foreach (var (player, newSlotIndex) in playerData)
            {
                // Clear old references (models should be destroyed by fade out)
                player.characterModel = null;
                player.petModel = null;
                
                // Assign new slot
                player.formationSlotIndex = newSlotIndex;
                if (newSlotIndex < formationSlots.Count)
                {
                    formationSlots[newSlotIndex].AssignToPlayer(player.playerID);
                }
            }
            
            Debug.Log("OtherPlayersFormationManager: Formation rebuild completed - slots reassigned, now forcing model recreation");
            
            // Phase 3: Force recreation of models for all players
            foreach (var (player, newSlotIndex) in playerData)
            {
                if (player.characterIndex >= 0)
                {
                    Debug.Log($"OtherPlayersFormationManager: Recreating character model for {player.playerName} at slot {newSlotIndex}");
                    ForceRecreateCharacterModel(player, newSlotIndex);
                }
                
                if (player.petIndex >= 0)
                {
                    Debug.Log($"OtherPlayersFormationManager: Recreating pet model for {player.playerName} at slot {newSlotIndex}");
                    ForceRecreatePetModel(player, newSlotIndex);
                }
            }
        }
        
        /// <summary>
        /// Force recreation of a character model after coordinated rebuild
        /// </summary>
        private void ForceRecreateCharacterModel(OtherPlayerModels player, int slotIndex)
        {
            ModelDissolveAnimator slotAnimator = GetSlotAnimator(slotIndex, true);
            if (slotAnimator != null)
            {
                GameObject newModel = CreateCharacterModelForOtherPlayer(player.characterIndex, slotIndex);
                if (newModel != null)
                {
                    // Prepare model for animation to prevent pop-in
                    PrepareModelForAnimation(newModel);
                    
                    player.characterModel = newModel;
                    slotAnimator.AnimateModelIn(newModel, () => {
                        Debug.Log($"OtherPlayersFormationManager: Character recreation completed for {player.playerName} in slot {slotIndex}");
                    });
                }
            }
            else
            {
                Debug.LogWarning($"OtherPlayersFormationManager: Could not get slot animator for character recreation in slot {slotIndex}");
            }
        }
        
        /// <summary>
        /// Force recreation of a pet model after coordinated rebuild
        /// </summary>
        private void ForceRecreatePetModel(OtherPlayerModels player, int slotIndex)
        {
            ModelDissolveAnimator slotAnimator = GetSlotAnimator(slotIndex, false);
            if (slotAnimator != null)
            {
                GameObject newModel = CreatePetModelForOtherPlayer(player.petIndex, slotIndex);
                if (newModel != null)
                {
                    // Prepare model for animation to prevent pop-in
                    PrepareModelForAnimation(newModel);
                    
                    player.petModel = newModel;
                    slotAnimator.AnimateModelIn(newModel, () => {
                        Debug.Log($"OtherPlayersFormationManager: Pet recreation completed for {player.playerName} in slot {slotIndex}");
                    });
                }
            }
            else
            {
                Debug.LogWarning($"OtherPlayersFormationManager: Could not get slot animator for pet recreation in slot {slotIndex}");
            }
        }
        
        /// <summary>
        /// Prepare a model for animation by disabling all renderers to prevent pop-in effect
        /// </summary>
        private void PrepareModelForAnimation(GameObject model)
        {
            if (model == null) return;
            
            Debug.Log($"OtherPlayersFormationManager: Preparing model for animation: {model.name}");
            
            // Make all renderers initially invisible so the animation can control the appearance
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }
            
            // Keep the GameObject active but invisible through renderer control
            model.SetActive(true);
            
            Debug.Log($"OtherPlayersFormationManager: Model {model.name} prepared for animation - {renderers.Length} renderers disabled, awaiting animation");
        }
        
        /// <summary>
        /// Check if any slot animators are currently running repositioning animations
        /// </summary>
        private bool HasActiveRepositioningAnimations()
        {
            foreach (var slotAnimator in slotAnimators.Values)
            {
                if (slotAnimator != null && slotAnimator.IsTransitioning)
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Coroutine to delay UpdatePlayerModels to avoid overlapping animations
        /// </summary>
        private System.Collections.IEnumerator DelayedUpdatePlayerModels(List<PlayerSelectionInfo> players, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            Debug.Log("OtherPlayersFormationManager: Delayed model updates starting after repositioning animations");
            
            // Update individual player selections
            foreach (var player in players)
            {
                UpdatePlayerModels(player);
            }
        }
        
        #endregion
        
        #region Cleanup
        
        private void ClearAllFormationModels()
        {
            foreach (var kvp in otherPlayerModels)
            {
                DestroyPlayerModels(kvp.Value);
            }
            
            otherPlayerModels.Clear();
            formationSlots.Clear();
            lastPlayerCount = 0;
            needsFormationRecalculation = false;
        }
        
        private void DestroyPlayerModels(OtherPlayerModels playerData)
        {
            if (playerData.characterModel != null)
            {
                Destroy(playerData.characterModel);
                playerData.characterModel = null;
            }
            
            if (playerData.petModel != null)
            {
                Destroy(playerData.petModel);
                playerData.petModel = null;
            }
        }
        
        private void OnDestroy()
        {
            CleanupFormation();
            
            // Clean up slot animators
            foreach (var kvp in slotAnimators)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            slotAnimators.Clear();
            
            // Clear events
            OnOtherPlayerModelSpawned = null;
            OnOtherPlayerModelRemoved = null;
            OnFormationPlayerCountChanged = null;
        }
        
        #endregion
        
        #region Debug and Utilities
        
        public string GetFormationInfo()
        {
            return $"OtherPlayersFormationManager Info:\n" +
                   $"- Initialized: {isInitialized}\n" +
                   $"- My Player ID: {myPlayerID}\n" +
                   $"- Other Players: {otherPlayerModels.Count}\n" +
                   $"- Formation Slots: {formationSlots.Count}\n" +
                   $"- Focal Point: {focalPoint?.position ?? Vector3.zero}\n" +
                   $"- Base Distance: {baseDistance}\n" +
                   $"- Arc Angle: {arcAngle}°";
        }
        
        [ContextMenu("Print Formation Info")]
        public void PrintFormationInfo()
        {
            Debug.Log(GetFormationInfo());
        }
        
        [ContextMenu("Force Recalculate Formation")]
        public void ForceRecalculateFormation()
        {
            if (isInitialized)
            {
                needsFormationRecalculation = true;
                RecalculateFormation();
                Debug.Log("OtherPlayersFormationManager: Forced formation recalculation");
            }
        }
        
        /// <summary>
        /// Get debug information about all slot animators to verify independence
        /// </summary>
        public string GetFormationAnimatorInfo()
        {
            if (slotAnimators.Count == 0)
            {
                return "Formation Slot Animators: None created yet";
            }
            
            var info = $"Formation Slot Animators ({slotAnimators.Count} total):\n";
            foreach (var kvp in slotAnimators)
            {
                var animator = kvp.Value;
                info += $"  - {kvp.Key}: {(animator != null ? $"Active (Animating: {animator.IsAnimating})" : "NULL")}\n";
            }
            info += $"Formation Manager: {gameObject.name}";
            
            return info;
        }
        
        [ContextMenu("Print Formation Animator Info")]
        public void PrintFormationAnimatorInfo()
        {
            Debug.Log(GetFormationAnimatorInfo());
        }
        
        #endregion
    }
} 