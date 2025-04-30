using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using FishNet.Object;
using TMPro;
using UnityEngine.SceneManagement;

namespace Combat
{
    /// <summary>
    /// Manages the Combat Scene Canvas to display either the local player's fight or other networked fights.
    /// Handles cycling through active fights when requested by the player.
    /// </summary>
    public class CombatSceneCanvas : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI battleInfoText;
        [SerializeField] private Button nextBattleButton;
        [SerializeField] private Transform playerPetContainer;
        [SerializeField] private Transform opponentPetContainer;
        [SerializeField] private Transform playerHandArea;
        
        [Header("Settings")]
        [SerializeField] private float transitionSpeed = 0.5f;
        
        // Local player references
        private NetworkPlayer localNetworkPlayer;
        private CombatPlayer localCombatPlayer;
        private CombatPet localPlayerPet;
        private CombatPet localOpponentPet;
        private PlayerHand localPlayerHand;
        
        // Fight tracking
        private int currentFightIndex = 0;
        private List<NetworkPlayer> playersInCombat = new List<NetworkPlayer>();
        
        // Current combat data
        private Dictionary<NetworkPlayer, CombatData> activeCombats = new Dictionary<NetworkPlayer, CombatData>();

        private void Awake()
        {
            // Setup button listener
            if (nextBattleButton != null)
            {
                nextBattleButton.onClick.AddListener(CycleToNextFight);
            }
            else
            {
                Debug.LogError("[CombatSceneCanvas] Next Battle Button reference is missing!");
            }
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
        }
        
        public override void OnStopClient()
        {
            base.OnStopClient();
        }

        // This method is called by CombatManager's RpcNotifyCombatReady
        public void HandleCombatReady()
        {
             Debug.Log("[CombatSceneCanvas] HandleCombatReady triggered by CombatManager RPC.");
             
             // Now it's safe to find the local player and request data
             FindLocalPlayer();
             
             // Request data only if we own this canvas instance (or this specific client)
             // This prevents every client calling the ServerRpc N times
             if (IsOwner) 
             {
                 RequestCombatData();
                 Debug.Log("[CombatSceneCanvas] Requesting combat data after HandleCombatReady.");
             }
        }

        private void FindLocalPlayer()
        {
            // Ensure Player list is accessible
            if (NetworkPlayer.Players == null)
            {
                 Debug.LogError("[CombatSceneCanvas] NetworkPlayer.Players list is null!");
                 return;
            }
            
            // Find the local player's NetworkPlayer
            foreach (NetworkPlayer player in NetworkPlayer.Players)
            {
                if (player.IsOwner)
                {
                    localNetworkPlayer = player;
                    Debug.Log($"[CombatSceneCanvas] Found local NetworkPlayer: {player.GetSteamName()}");
                    break;
                }
            }
            
            if (localNetworkPlayer == null)
            {
                Debug.LogError("[CombatSceneCanvas] Could not find local NetworkPlayer!");
                return;
            }
        }
        
        [ServerRpc(RequireOwnership = true)] // Require ownership is safer - only the owner client should request data
        private void RequestCombatData()
        {
            if (CombatManager.Instance == null)
            {
                Debug.LogError("[CombatSceneCanvas] CombatManager.Instance is null when requesting combat data!");
                return;
            }
            
            // Get active combats from the CombatManager
            activeCombats = new Dictionary<NetworkPlayer, CombatData>();
            playersInCombat = new List<NetworkPlayer>();
            
            foreach (var kvp in CombatManager.Instance.GetActiveCombats())
            {
                activeCombats[kvp.Key] = kvp.Value;
                playersInCombat.Add(kvp.Key);
            }
            
            // Tell clients about all active combats
            RpcUpdateCombatData(playersInCombat.ToArray(), activeCombats);
            
            // Set the initial view to the local player's combat
            SetViewToLocalFight();
        }
        
        [ObserversRpc]
        private void RpcUpdateCombatData(NetworkPlayer[] players, Dictionary<NetworkPlayer, CombatData> combats)
        {
            // Store combat data
            playersInCombat = new List<NetworkPlayer>(players);
            activeCombats = combats;
            
            // Set the initial view to the local player's combat
            SetViewToLocalFight();
        }
        
        private void SetViewToLocalFight()
        {
            if (localNetworkPlayer == null)
            {
                Debug.LogError("[CombatSceneCanvas] Local NetworkPlayer is null when trying to set initial view!");
                return;
            }
            
            // Find the local player's index in the list
            currentFightIndex = playersInCombat.IndexOf(localNetworkPlayer);
            if (currentFightIndex == -1)
            {
                Debug.LogError($"[CombatSceneCanvas] Local player {localNetworkPlayer.GetSteamName()} not found in playersInCombat list!");
                currentFightIndex = 0;
            }
            
            // Update the view
            UpdateCurrentFightView();
        }
        
        private void CycleToNextFight()
        {
            if (playersInCombat.Count <= 1)
            {
                Debug.Log("[CombatSceneCanvas] Only one combat available, not cycling.");
                return;
            }
            
            // Move to the next fight
            currentFightIndex = (currentFightIndex + 1) % playersInCombat.Count;
            
            // Update the view
            UpdateCurrentFightView();
        }
        
        private void UpdateCurrentFightView()
        {
            if (playersInCombat.Count == 0)
            {
                Debug.LogError("[CombatSceneCanvas] No players in combat when updating view!");
                return;
            }
            
            // Get the current player whose fight we're viewing
            NetworkPlayer currentPlayer = playersInCombat[currentFightIndex];
            
            // Check if we have combat data for this player
            if (!activeCombats.TryGetValue(currentPlayer, out CombatData combatData))
            {
                Debug.LogError($"[CombatSceneCanvas] No combat data found for player {currentPlayer.GetSteamName()}");
                return;
            }
            
            // Update battle info text
            if (battleInfoText != null)
            {
                string playerName = currentPlayer.GetSteamName();
                string opponentName = combatData.OpponentPlayer != null ? 
                    combatData.OpponentPlayer.GetSteamName() : "Unknown";
                
                battleInfoText.text = $"{playerName} vs {opponentName}'s Pet";
            }
            
            // Show/hide combat entities based on which fight we're viewing
            foreach (NetworkPlayer player in playersInCombat)
            {
                if (!activeCombats.TryGetValue(player, out CombatData playerCombatData))
                {
                    continue;
                }
                
                bool isCurrentFight = (player == currentPlayer);
                
                // Show/hide the player's combat entities
                SetCombatEntityVisibility(playerCombatData.CombatPlayer, isCurrentFight);
                SetCombatEntityVisibility(playerCombatData.PlayerPet, isCurrentFight);
                SetCombatEntityVisibility(playerCombatData.OpponentPet, isCurrentFight);
                SetCombatEntityVisibility(playerCombatData.PlayerHand, isCurrentFight);
            }
            
            // Position currently visible entities in the correct containers
            PositionVisibleEntities();
            
            Debug.Log($"[CombatSceneCanvas] Updated view to show {currentPlayer.GetSteamName()}'s fight");
        }
        
        private void SetCombatEntityVisibility(Component entity, bool isVisible)
        {
            if (entity == null)
            {
                return;
            }
            
            // Handle different entity types
            if (entity is CombatPlayer combatPlayer)
            {
                // Toggle visibility of CombatPlayer
                var renderer = combatPlayer.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = isVisible;
                }
            }
            else if (entity is CombatPet combatPet)
            {
                // Toggle visibility of CombatPet
                var renderer = combatPet.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = isVisible;
                }
                
                // Also handle any animator
                var animator = combatPet.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.enabled = isVisible;
                }
            }
            else if (entity is PlayerHand playerHand)
            {
                // Toggle visibility of PlayerHand (which might be a Canvas)
                var canvas = playerHand.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.enabled = isVisible;
                }
                else
                {
                    // Or it might just be a GameObject
                    playerHand.gameObject.SetActive(isVisible);
                }
            }
        }
        
        private void PositionVisibleEntities()
        {
            if (currentFightIndex < 0 || currentFightIndex >= playersInCombat.Count)
            {
                Debug.LogError("[CombatSceneCanvas] Invalid currentFightIndex when positioning entities");
                return;
            }
            
            NetworkPlayer currentPlayer = playersInCombat[currentFightIndex];
            if (!activeCombats.TryGetValue(currentPlayer, out CombatData combatData))
            {
                return;
            }
            
            // Position player pet in the player pet container
            if (combatData.PlayerPet != null && playerPetContainer != null)
            {
                PositionEntityInContainer(combatData.PlayerPet.transform, playerPetContainer);
            }
            
            // Position opponent pet in the opponent pet container
            if (combatData.OpponentPet != null && opponentPetContainer != null)
            {
                PositionEntityInContainer(combatData.OpponentPet.transform, opponentPetContainer);
            }
            
            // Position player hand in the player hand area
            if (combatData.PlayerHand != null && playerHandArea != null)
            {
                PositionEntityInContainer(combatData.PlayerHand.transform, playerHandArea);
            }
        }
        
        private void PositionEntityInContainer(Transform entityTransform, Transform container)
        {
            // Save the original parent
            Transform originalParent = entityTransform.parent;
            
            // Get network object component to use RpcSetParent if available
            NetworkObject netObj = entityTransform.GetComponent<NetworkObject>();
            
            if (netObj != null && netObj.IsSpawned)
            {
                // Use the CombatPlayer's RpcSetParent method if the entity is a CombatPlayer
                CombatPlayer combatPlayer = entityTransform.GetComponent<CombatPlayer>();
                if (combatPlayer != null)
                {
                    combatPlayer.RpcSetParent(container.GetComponent<NetworkObject>());
                }
                else
                {
                    // For non-CombatPlayer entities, set parent directly (local visual only)
                    entityTransform.SetParent(container, false);
                    entityTransform.localPosition = Vector3.zero;
                }
            }
            else
            {
                // No NetworkObject, set parent directly (local visual only)
                entityTransform.SetParent(container, false);
                entityTransform.localPosition = Vector3.zero;
            }
        }
        
        // Helper method to get current combat data
        public CombatData GetCurrentCombatData()
        {
            if (currentFightIndex < 0 || currentFightIndex >= playersInCombat.Count)
            {
                return null;
            }
            
            NetworkPlayer currentPlayer = playersInCombat[currentFightIndex];
            if (activeCombats.TryGetValue(currentPlayer, out CombatData data))
            {
                return data;
            }
            
            return null;
        }
    }
} 