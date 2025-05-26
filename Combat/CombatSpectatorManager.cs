using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;

/// <summary>
/// Manages spectating functionality during combat, allowing players to view other ongoing fights.
/// Attach to: The CombatManager GameObject to handle fight spectating.
/// </summary>
public class CombatSpectatorManager : MonoBehaviour
{
    [Header("Debug Options")]
    [SerializeField] private bool debugLogEnabled = true;
    
    // Manager references
    private FightManager fightManager;
    private EntityVisibilityManager entityVisibilityManager;
    private CombatCanvasManager combatCanvasManager;
    
    // UI References (obtained from CombatCanvasManager)
    private Button spectateButton;
    private Button returnToOwnFightButton;
    
    // Spectating state
    private bool isSpectating = false;
    private int currentSpectatedFightIndex = -1;
    private List<FightAssignmentData> availableFights = new List<FightAssignmentData>();
    
    // Local player's original fight data
    private FightAssignmentData? localPlayerFight;
    private NetworkConnection localConnection;
    
    #region Lifecycle Methods
    
    private void Awake()
    {
        FindManagerReferences();
    }
    
    private void Start()
    {
        SetupSpectatingUI();
    }
    
    private void OnEnable()
    {
        // Subscribe to fight changes
        if (fightManager != null)
        {
            fightManager.OnFightsChanged += OnFightsChanged;
        }
    }
    
    private void OnDisable()
    {
        // Unsubscribe from fight changes
        if (fightManager != null)
        {
            fightManager.OnFightsChanged -= OnFightsChanged;
        }
    }
    
    #endregion
    
    #region Initialization
    
    private void FindManagerReferences()
    {
        // Find FightManager
        if (fightManager == null)
        {
            fightManager = FindFirstObjectByType<FightManager>();
        }
        
        // Find EntityVisibilityManager
        if (entityVisibilityManager == null)
        {
            entityVisibilityManager = FindFirstObjectByType<EntityVisibilityManager>();
        }
        
        // Find CombatCanvasManager
        if (combatCanvasManager == null)
        {
            combatCanvasManager = FindFirstObjectByType<CombatCanvasManager>();
        }
        
        // Get button references from CombatCanvasManager
        if (combatCanvasManager != null)
        {
            spectateButton = combatCanvasManager.SpectateButton;
            returnToOwnFightButton = combatCanvasManager.ReturnToOwnFightButton;
        }
        
        LogDebug($"Manager references found - FightManager: {fightManager != null}, EntityVisibilityManager: {entityVisibilityManager != null}, CombatCanvasManager: {combatCanvasManager != null}");
        LogDebug($"Button references found - SpectateButton: {spectateButton != null}, ReturnToOwnFightButton: {returnToOwnFightButton != null}");
    }
    
    private void SetupSpectatingUI()
    {
        if (spectateButton != null)
        {
            spectateButton.onClick.RemoveAllListeners();
            spectateButton.onClick.AddListener(OnSpectateButtonClicked);
            LogDebug("Spectate button listener added");
        }
        else
        {
            LogDebug("Spectate button not assigned");
        }
        
        if (returnToOwnFightButton != null)
        {
            returnToOwnFightButton.onClick.RemoveAllListeners();
            returnToOwnFightButton.onClick.AddListener(OnReturnToOwnFightClicked);
            returnToOwnFightButton.gameObject.SetActive(false); // Initially hidden
            LogDebug("Return to own fight button listener added");
        }
        else
        {
            LogDebug("Return to own fight button not assigned");
        }
    }
    
    private bool CacheLocalPlayerFight()
    {
        // Get local connection
        var networkManager = FishNet.InstanceFinder.NetworkManager;
        if (networkManager != null && networkManager.IsClientStarted)
        {
            localConnection = networkManager.ClientManager.Connection;
        }
        
        if (localConnection == null || fightManager == null)
        {
            LogDebug("Cannot cache local player fight - missing connection or fight manager");
            return false;
        }
        
        // Cache the local player's fight
        localPlayerFight = fightManager.GetFightForConnection(localConnection);
        
        if (localPlayerFight.HasValue)
        {
            LogDebug($"Cached local player fight: Player {localPlayerFight.Value.PlayerObjectId} vs Pet {localPlayerFight.Value.PetObjectId}");
            return true;
        }
        else
        {
            LogDebug("No fight found for local player");
            return false;
        }
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnFightsChanged(bool hasFights)
    {
        UpdateAvailableFights();
        UpdateSpectateButtonState();
    }
    
    private void OnSpectateButtonClicked()
    {
        LogDebug("Spectate button clicked");
        
        if (!isSpectating)
        {
            StartSpectating();
        }
        else
        {
            SpectateNextFight();
        }
    }
    
    private void OnReturnToOwnFightClicked()
    {
        LogDebug("Return to own fight button clicked");
        ReturnToOwnFight();
    }
    
    #endregion
    
    #region Spectating Logic
    
    private void UpdateAvailableFights()
    {
        if (fightManager == null)
        {
            availableFights.Clear();
            return;
        }
        
        // Get all fight assignments
        var allFights = fightManager.GetAllFightAssignments();
        
        // Ensure we have the local player fight cached if we're spectating
        if (isSpectating && !localPlayerFight.HasValue)
        {
            CacheLocalPlayerFight();
        }
        
        // Filter out the local player's fight if we have one
        availableFights.Clear();
        foreach (var fight in allFights)
        {
            if (!localPlayerFight.HasValue || 
                fight.PlayerObjectId != localPlayerFight.Value.PlayerObjectId)
            {
                availableFights.Add(fight);
            }
        }
        
        // If we're spectating and there are no other fights available, we should exit spectating mode
        if (isSpectating && availableFights.Count == 0)
        {
            LogDebug("No other fights available while spectating - exiting spectating mode");
            ExitSpectatingMode();
        }
        
        LogDebug($"Updated available fights: {availableFights.Count} fights available for spectating");
    }
    
    private void UpdateSpectateButtonState()
    {
        if (spectateButton == null) return;
        
        // Enable spectate button if there are other fights to watch
        bool canSpectate = availableFights.Count > 0;
        spectateButton.interactable = canSpectate;
        
        // Update button text based on state
        var buttonText = spectateButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (buttonText != null)
        {
            if (!isSpectating)
            {
                buttonText.text = canSpectate ? "Spectate Other Fights" : "No Other Fights";
            }
            else
            {
                buttonText.text = availableFights.Count > 1 ? "Next Fight" : "Spectating";
            }
        }
        
        LogDebug($"Spectate button state updated - interactable: {canSpectate}, isSpectating: {isSpectating}");
    }
    
    private void StartSpectating()
    {
        if (availableFights.Count == 0)
        {
            LogDebug("No fights available to spectate");
            return;
        }
        
        // Cache the local player's fight before we start spectating
        if (!CacheLocalPlayerFight())
        {
            LogDebug("Cannot start spectating - failed to cache local player fight");
            return;
        }
        
        isSpectating = true;
        currentSpectatedFightIndex = 0;
        
        // Show the return button
        if (returnToOwnFightButton != null)
        {
            returnToOwnFightButton.gameObject.SetActive(true);
        }
        
        // Disable end turn button while spectating
        if (combatCanvasManager != null)
        {
            combatCanvasManager.SetEndTurnButtonInteractable(false);
        }
        
        // Switch to the first available fight
        SwitchToFight(availableFights[currentSpectatedFightIndex]);
        
        UpdateSpectateButtonState();
        
        LogDebug($"Started spectating fight {currentSpectatedFightIndex + 1} of {availableFights.Count}");
    }
    
    private void SpectateNextFight()
    {
        if (availableFights.Count <= 1)
        {
            LogDebug("Only one fight available, cannot cycle to next");
            return;
        }
        
        // Move to next fight (cycle back to 0 if at end)
        currentSpectatedFightIndex = (currentSpectatedFightIndex + 1) % availableFights.Count;
        
        // Switch to the new fight
        SwitchToFight(availableFights[currentSpectatedFightIndex]);
        
        LogDebug($"Switched to spectating fight {currentSpectatedFightIndex + 1} of {availableFights.Count}");
    }
    
    private void ReturnToOwnFight()
    {
        // Try to use cached fight first, but if not available, try to find it again
        if (!localPlayerFight.HasValue)
        {
            LogDebug("No cached local player fight, attempting to find it now");
            if (!CacheLocalPlayerFight())
            {
                LogDebug("Cannot return to own fight - unable to find local player fight");
                return;
            }
        }
        
        // Switch back to local player's fight first
        SwitchToFight(localPlayerFight.Value);
        
        // Then exit spectating mode (this will be called automatically by SwitchToFight if we're viewing our own fight)
        // But we call it here too in case there are any edge cases
        if (isSpectating)
        {
            ExitSpectatingMode();
        }
        
        LogDebug("Returned to own fight");
    }
    
    private void ExitSpectatingMode()
    {
        if (!isSpectating) return;
        
        isSpectating = false;
        currentSpectatedFightIndex = -1;
        
        // Hide the return button
        if (returnToOwnFightButton != null)
        {
            returnToOwnFightButton.gameObject.SetActive(false);
        }
        
        // Re-enable end turn button
        if (combatCanvasManager != null)
        {
            combatCanvasManager.SetEndTurnButtonInteractable(true);
        }
        
        UpdateSpectateButtonState();
        
        LogDebug("Exited spectating mode");
    }
    
    private void SwitchToFight(FightAssignmentData fight)
    {
        if (fightManager == null)
        {
            LogDebug("Cannot switch fight - FightManager is null");
            return;
        }
        
        // Get the player entity for this fight
        var playerEntity = GetNetworkObjectComponent<NetworkEntity>(fight.PlayerObjectId);
        if (playerEntity == null)
        {
            LogDebug($"Cannot find player entity with ID {fight.PlayerObjectId}");
            return;
        }
        
        // Use FightManager's SetViewedFight method to update the viewed combat references
        bool success = fightManager.SetViewedFight(playerEntity);
        
        if (success)
        {
            // Update entity visibility to show the new fight
            if (entityVisibilityManager != null)
            {
                entityVisibilityManager.UpdateAllEntitiesVisibility();
            }
            
            // Check if we've cycled back to the local player's own fight
            if (isSpectating && localPlayerFight.HasValue && 
                fight.PlayerObjectId == localPlayerFight.Value.PlayerObjectId)
            {
                LogDebug("Cycled back to local player's own fight - automatically exiting spectating mode");
                ExitSpectatingMode();
            }
            
            LogDebug($"Successfully switched to fight: Player {fight.PlayerObjectId} vs Pet {fight.PetObjectId}");
        }
        else
        {
            LogDebug($"Failed to switch to fight: Player {fight.PlayerObjectId} vs Pet {fight.PetObjectId}");
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    // Helper function to get a component from a NetworkObject ID
    private T GetNetworkObjectComponent<T>(uint objectId) where T : FishNet.Object.NetworkBehaviour
    {
        FishNet.Object.NetworkObject nob = null;
        var networkManager = FishNet.InstanceFinder.NetworkManager;
        
        if (networkManager != null)
        {
            if (networkManager.IsServerStarted)
            {
                networkManager.ServerManager.Objects.Spawned.TryGetValue((int)objectId, out nob);
            }
            else if (networkManager.IsClientStarted)
            {
                networkManager.ClientManager.Objects.Spawned.TryGetValue((int)objectId, out nob);
            }
        }
        
        return nob != null ? nob.GetComponent<T>() : null;
    }
    
    private void LogDebug(string message)
    {
        if (debugLogEnabled)
        {
            Debug.Log($"[CombatSpectatorManager] {message}");
        }
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Manually refresh the available fights (useful when fights change)
    /// </summary>
    public void RefreshAvailableFights()
    {
        UpdateAvailableFights();
        UpdateSpectateButtonState();
    }
    
    /// <summary>
    /// Check if currently spectating another fight
    /// </summary>
    public bool IsSpectating => isSpectating;
    
    /// <summary>
    /// Get the number of available fights to spectate
    /// </summary>
    public int AvailableFightsCount => availableFights.Count;
    
    /// <summary>
    /// Force return to own fight (useful for external systems)
    /// </summary>
    public void ForceReturnToOwnFight()
    {
        if (isSpectating)
        {
            ReturnToOwnFight();
        }
    }
    
    #endregion
} 