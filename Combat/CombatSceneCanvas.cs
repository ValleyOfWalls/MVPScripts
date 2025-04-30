using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using FishNet.Connection;
using System.Collections.Generic;
using TMPro;
using FishNet;

namespace Combat
{
    public class CombatSceneCanvas : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject canvasRoot;
        [SerializeField] private TextMeshProUGUI battleInfoText;
        [SerializeField] private Button nextBattleButton;
        [SerializeField] private GameObject battleViewContainer;

        // References to combatants
        private CombatPlayer localPlayer;
        private CombatPet opponentPet;
        
        // Combat ownership information
        private NetworkPlayer owningPlayer;
        private NetworkPlayer opponentOwner;
        
        // Reference to all combat canvases in the scene
        private static List<CombatSceneCanvas> allCombatCanvases = new List<CombatSceneCanvas>();
        
        // Currently viewed combat index (for cycling between combats)
        private static int currentlyViewedCombatIndex = -1;
        
        private void Awake()
        {
            if (!allCombatCanvases.Contains(this))
            {
                allCombatCanvases.Add(this);
            }
            
            if (nextBattleButton != null)
            {
                nextBattleButton.onClick.AddListener(OnNextBattleClicked);
            }
        }
        
        private void OnDestroy()
        {
            if (allCombatCanvases.Contains(this))
            {
                allCombatCanvases.Remove(this);
            }
            
            if (nextBattleButton != null)
            {
                nextBattleButton.onClick.RemoveListener(OnNextBattleClicked);
            }
        }
        
        public void Initialize(CombatPlayer player, CombatPet opponent, NetworkPlayer playerOwner, NetworkPlayer opponentOwner)
        {
            this.localPlayer = player;
            this.opponentPet = opponent;
            this.owningPlayer = playerOwner;
            this.opponentOwner = opponentOwner;
            
            if (battleInfoText != null)
            {
                battleInfoText.text = $"{playerOwner.GetSteamName()} vs {opponentOwner.GetSteamName()}'s Pet";
            }
            
            // Set active state based on if this is the local player's combat
            // Get the local NetworkPlayer instance correctly
            NetworkPlayer localNetworkPlayer = null;
            if (InstanceFinder.ClientManager.Connection != null && InstanceFinder.ClientManager.Connection.FirstObject != null)
            {
                localNetworkPlayer = InstanceFinder.ClientManager.Connection.FirstObject.GetComponent<NetworkPlayer>();
            }
            
            if (localNetworkPlayer != null)
            {
                SetActiveForPlayer(localNetworkPlayer);
            }
            else
            {
                Debug.LogWarning("[CombatSceneCanvas] Could not find local NetworkPlayer during Initialize.");
                // Optionally set a default state if local player isn't found immediately
                // canvasRoot?.SetActive(false); // Example: hide if local player unknown
            }
        }
        
        // Set active state based on if this is the specified player's combat
        // Renamed parameter from localPlayer to checkingPlayer to avoid conflict with the field
        public void SetActiveForPlayer(NetworkPlayer checkingPlayer)
        {
            bool isLocalPlayerCombat = (checkingPlayer == owningPlayer);
            
            if (canvasRoot != null)
            {
                // Show the canvas if it's the local player's combat OR if we are actively spectating
                canvasRoot.SetActive(isLocalPlayerCombat || currentlyViewedCombatIndex != -1);
            }
            
            // Enable/disable interactive elements based on ownership
            // The field 'this.localPlayer' refers to the CombatPlayer component associated with this canvas
            if (checkingPlayer == owningPlayer)
            {
                // This is the player's own combat, make it fully interactive
                SetInteractable(true);
            }
            else
            {
                // This is another player's combat, make it view-only
                SetInteractable(false);
            }
            
            // Also control visibility of player and pet GameObjects
            // Use the field 'this.localPlayer' (CombatPlayer) and 'opponentPet' (CombatPet)
            if (this.localPlayer != null && opponentPet != null)
            {
                // Activate the GameObjects if this canvas should be visible
                bool shouldBeActive = canvasRoot != null && canvasRoot.activeSelf;
                
                // Set player and pet active states and spectator mode
                this.localPlayer.SetActive(shouldBeActive);
                this.localPlayer.SetSpectatorMode(!isLocalPlayerCombat); // Spectator if NOT the local player's combat
                
                opponentPet.SetActive(shouldBeActive);
                opponentPet.SetSpectatorMode(!isLocalPlayerCombat); // Spectator if NOT the local player's combat
            }
        }
        
        // Set interactable state of controls
        private void SetInteractable(bool interactable)
        {
            // Set the interactable state for any interactive UI elements in the canvas itself
            Button[] canvasButtons = battleViewContainer.GetComponentsInChildren<Button>(true);
            foreach (Button button in canvasButtons)
            {
                if (button != nextBattleButton) // Don't disable the next battle button
                {
                    button.interactable = interactable;
                }
            }
            
            // Use the new spectator mode methods to handle player and pet interactivity
            if (localPlayer != null)
            {
                localPlayer.SetSpectatorMode(!interactable);
            }
            
            if (opponentPet != null)
            {
                opponentPet.SetSpectatorMode(!interactable);
            }
            
            // Make sure the next battle button is always enabled
            if (nextBattleButton != null)
            {
                nextBattleButton.interactable = true;
            }
        }
        
        // Event handler for next battle button
        private void OnNextBattleClicked()
        {
            CmdCycleToNextBattle();
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void CmdCycleToNextBattle()
        {
            // Use the client that sent this request to determine which battles to cycle through
            RpcCycleToNextBattle(Owner);
        }
        
        [TargetRpc]
        private void RpcCycleToNextBattle(NetworkConnection conn)
        {
            // Increment the index
            currentlyViewedCombatIndex++;
            
            // Wrap around if we've gone past the last combat
            if (currentlyViewedCombatIndex >= allCombatCanvases.Count)
            {
                currentlyViewedCombatIndex = -1; // -1 means show only the player's own combat
            }
            
            // Update all combat canvases
            UpdateCombatCanvasVisibility();
        }
        
        // Update visibility for all combat canvases
        private void UpdateCombatCanvasVisibility()
        {
            // Get the local NetworkPlayer instance correctly
            NetworkPlayer localNetworkPlayer = null;
            if (InstanceFinder.ClientManager.Connection != null && InstanceFinder.ClientManager.Connection.FirstObject != null)
            {
                localNetworkPlayer = InstanceFinder.ClientManager.Connection.FirstObject.GetComponent<NetworkPlayer>();
            }
            
            if (localNetworkPlayer == null)
            {
                Debug.LogError("[CombatSceneCanvas] Could not find local NetworkPlayer during UpdateCombatCanvasVisibility.");
                return; // Cannot update visibility without knowing the local player
            }
            
            for (int i = 0; i < allCombatCanvases.Count; i++)
            {
                CombatSceneCanvas canvas = allCombatCanvases[i];
                
                if (currentlyViewedCombatIndex == -1)
                {
                    // Default view: only show player's own combat
                    bool isOwnCombat = canvas.owningPlayer == localNetworkPlayer;
                    canvas.canvasRoot.SetActive(isOwnCombat);
                    canvas.SetInteractable(isOwnCombat);
                    // Also update the associated player/pet GameObject visibility
                    canvas.localPlayer?.SetActive(isOwnCombat);
                    canvas.opponentPet?.SetActive(isOwnCombat);
                    // Ensure spectator mode is set correctly (should be false if it's own combat)
                    canvas.localPlayer?.SetSpectatorMode(false);
                    canvas.opponentPet?.SetSpectatorMode(false);
                }
                else if (i == currentlyViewedCombatIndex)
                {
                    // Spectator view: Show the currently selected combat
                    canvas.canvasRoot.SetActive(true);
                    bool isOwnCombatWhileSpectating = canvas.owningPlayer == localNetworkPlayer;
                    canvas.SetInteractable(isOwnCombatWhileSpectating);
                    // Update associated player/pet GameObject visibility and spectator mode
                    canvas.localPlayer?.SetActive(true);
                    canvas.opponentPet?.SetActive(true);
                    canvas.localPlayer?.SetSpectatorMode(!isOwnCombatWhileSpectating);
                    canvas.opponentPet?.SetSpectatorMode(!isOwnCombatWhileSpectating);
                }
                else
                {
                    // Hide all other combats
                    canvas.canvasRoot.SetActive(false);
                    // Also hide the associated player/pet GameObjects
                    canvas.localPlayer?.SetActive(false);
                    canvas.opponentPet?.SetActive(false);
                }
            }
        }
        
        // Called when the game state changes
        public static void ResetViewIndex()
        {
            currentlyViewedCombatIndex = -1;
            
            // Update visibility on all canvases
            foreach (CombatSceneCanvas canvas in allCombatCanvases)
            {
                canvas.UpdateCombatCanvasVisibility();
            }
        }
    }
} 