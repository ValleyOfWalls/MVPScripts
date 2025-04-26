using UnityEngine;
using System.Collections.Generic;
using Photon.Realtime;
using Photon.Pun;
using ExitGames.Client.Photon; // Added for Hashtable
using System.Linq; // Added for ToList()

public class StatusEffectManager
{
    private GameManager gameManager;
    
    // Status effect tracking (Local Player)
    private int localPlayerWeakTurns = 0;
    private int localPlayerBreakTurns = 0;
    private int localPlayerThorns = 0;
    private int localPlayerStrength = 0;

    // Status effect tracking (Local Pet)
    private int localPetWeakTurns = 0;
    private int localPetBreakTurns = 0;
    private int localPetThorns = 0;
    private int localPetStrength = 0;

    // Status effect tracking (Opponent Pet - Local Sim)
    private int opponentPetWeakTurns = 0;
    private int opponentPetBreakTurns = 0;
    private int opponentPetThorns = 0;
    private int opponentPetStrength = 0;
    
    // --- ADDED: Opponent Player Simulation Data ---
    private int opponentPlayerWeakTurns = 0;
    private int opponentPlayerBreakTurns = 0;
    private int opponentPlayerThorns = 0;
    private int opponentPlayerStrength = 0;
    // --- END ADDED ---

    // Combo tracking (Remains Local)
    private int currentComboCount = 0;
    
    public StatusEffectManager(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public void Initialize()
    {
        // Reset status effects
        localPlayerWeakTurns = 0;
        localPlayerBreakTurns = 0;
        localPetWeakTurns = 0;
        localPetBreakTurns = 0;
        opponentPetWeakTurns = 0;
        opponentPetBreakTurns = 0;
        localPlayerThorns = 0;
        localPetThorns = 0;
        opponentPetThorns = 0;
        localPlayerStrength = 0;
        localPetStrength = 0;
        opponentPetStrength = 0;
        
        // --- ADDED: Reset Opponent Player Sim ---
        opponentPlayerWeakTurns = 0;
        opponentPlayerBreakTurns = 0;
        opponentPlayerThorns = 0;
        opponentPlayerStrength = 0;
        // --- END ADDED ---
        
        // Reset combo
        currentComboCount = 0;

        // --- ADDED: Set Initial Player Properties ---
        UpdatePlayerStatusProperties();
        // --- END ADDED ---
    }
    
    #region Status Effect Methods
    
    public void ApplyStatusEffectLocalPlayer(StatusEffectType type, int duration)
    {
        if (duration <= 0) return;
        bool changed = false;
        switch(type)
        {
            case StatusEffectType.Weak:
                localPlayerWeakTurns += duration;
                Debug.Log($"Applied Weak to Player for {duration} turns. Total: {localPlayerWeakTurns}");
                changed = true;
                break;
            case StatusEffectType.Break:
                localPlayerBreakTurns += duration;
                Debug.Log($"Applied Break to Player for {duration} turns. Total: {localPlayerBreakTurns}");
                changed = true;
                break;
            case StatusEffectType.Thorns:
                localPlayerThorns += duration; // Duration parameter used as amount for Thorns
                Debug.Log($"Applied {duration} Thorns to Player. Total: {localPlayerThorns}");
                changed = true;
                break;
            case StatusEffectType.Strength:
                localPlayerStrength += duration; // Duration param used as amount
                Debug.Log($"Applied {duration} Strength to Player. Total: {localPlayerStrength}");
                changed = true;
                break;
        }
        
        // --- ADDED: Update Property if changed ---
        if (changed)
        {
            UpdatePlayerStatusProperties(type); // Update the specific status that changed
        }
        // --- END ADDED ---
    }
    
    public void ApplyStatusEffectLocalPet(StatusEffectType type, int duration)
    {
        if (duration <= 0) return;
        string propKeyToUpdate = null;
        
        switch(type)
        {
            case StatusEffectType.Weak:
                localPetWeakTurns += duration;
                propKeyToUpdate = CombatStateManager.PLAYER_COMBAT_PET_WEAK_PROP;
                Debug.Log($"Applied Weak to Local Pet for {duration} turns. Total: {localPetWeakTurns}");
                break;
            case StatusEffectType.Break:
                localPetBreakTurns += duration;
                propKeyToUpdate = CombatStateManager.PLAYER_COMBAT_PET_BREAK_PROP;
                Debug.Log($"Applied Break to Local Pet for {duration} turns. Total: {localPetBreakTurns}");
                break;
            case StatusEffectType.Thorns:
                localPetThorns += duration; // Duration parameter used as amount for Thorns
                propKeyToUpdate = CombatStateManager.PLAYER_COMBAT_PET_THORNS_PROP;
                Debug.Log($"Applied {duration} Thorns to Local Pet. Total: {localPetThorns}");
                break;
            case StatusEffectType.Strength:
                localPetStrength += duration; // Duration param used as amount
                propKeyToUpdate = CombatStateManager.PLAYER_COMBAT_PET_STRENGTH_PROP;
                Debug.Log($"Applied {duration} Strength to Local Pet. Total: {localPetStrength}");
                break;
        }
        
        // --- ADDED: Sync Property --- 
        if (!string.IsNullOrEmpty(propKeyToUpdate))
        {
            gameManager.GetPlayerManager()?.GetHealthManager()?.UpdatePlayerStatProperties(propKeyToUpdate);
        }
        // --- END ADDED ---

        // Notify others
        if (PhotonNetwork.InRoom)
        {
            gameManager.GetPhotonView()?.RPC("RpcApplyStatusToMyPet", RpcTarget.Others, type, duration);
        }
    }
    
    public void ApplyStatusEffectOpponentPet(StatusEffectType type, int duration, bool originatedFromRPC = false)
    {
        if (duration <= 0) return;
        switch(type)
        {
            case StatusEffectType.Weak:
                opponentPetWeakTurns += duration;
                Debug.Log($"Applied Weak to Opponent Pet for {duration} turns (local sim). Total: {opponentPetWeakTurns}");
                break;
            case StatusEffectType.Break:
                opponentPetBreakTurns += duration;
                Debug.Log($"Applied Break to Opponent Pet for {duration} turns (local sim). Total: {opponentPetBreakTurns}");
                break;
            // --- ADDED: Handle Thorns --- 
            case StatusEffectType.Thorns:
                opponentPetThorns += duration; // Duration parameter used as amount for Thorns
                Debug.Log($"Applied {duration} Thorns to Opponent Pet (local sim). Total: {opponentPetThorns}");
                break;
            // --- ADDED: Handle Strength --- 
            case StatusEffectType.Strength:
                opponentPetStrength += duration; // Duration param used as amount
                Debug.Log($"Applied {duration} Strength to Opponent Pet (local sim). Total: {opponentPetStrength}");
                break;
            // --- END ADDED ---
        }

        // --- MODIFIED: Log message to include Thorns --- 
        Debug.Log($"Applied Status {type} (Value/Dur: {duration}) to Opponent Pet (local sim)");

        // Notify the actual owner
        Player opponentPlayer = gameManager.GetPlayerManager()?.GetOpponentPlayer();
        if (PhotonNetwork.InRoom && opponentPlayer != null && !originatedFromRPC) // Check originatedFromRPC
        {
            gameManager.GetPhotonView()?.RPC("RpcApplyStatusToMyPet", opponentPlayer, type, duration);
        }
        else if (originatedFromRPC)
        {
            Debug.Log("ApplyStatusEffectOpponentPet called from RPC, skipping send.");
        }
    }
    
    public void DecrementPlayerStatusEffects()
    {
        bool weakChanged = localPlayerWeakTurns > 0;
        bool breakChanged = localPlayerBreakTurns > 0;
        bool thornsChanged = localPlayerThorns > 0; // Check if thorns existed before decrementing
        // bool strengthChanged = localPlayerStrength > 0; // Removed Strength check from here

        if (localPlayerWeakTurns > 0) localPlayerWeakTurns--;
        if (localPlayerBreakTurns > 0) localPlayerBreakTurns--;
        if (localPlayerThorns > 0) localPlayerThorns--; // Uncommented: Decrement Thorns
        // if (localPlayerStrength > 0) localPlayerStrength--; // Strength decays -> Moved to Turn End
        
        Debug.Log($"Decremented Player Status (Turn Start). Weak: {localPlayerWeakTurns}, Break: {localPlayerBreakTurns}, Thorns: {localPlayerThorns}"); // Removed Strength from log

        // --- ADDED: Update Properties if changed ---
        List<string> propsToUpdate = new List<string>();
        if (weakChanged && localPlayerWeakTurns == 0) propsToUpdate.Add(CombatStateManager.PLAYER_COMBAT_PLAYER_WEAK_PROP);
        if (breakChanged && localPlayerBreakTurns == 0) propsToUpdate.Add(CombatStateManager.PLAYER_COMBAT_PLAYER_BREAK_PROP);
        if (thornsChanged) propsToUpdate.Add(CombatStateManager.PLAYER_COMBAT_PLAYER_THORNS_PROP); // Uncommented: Add thorns prop if it changed
        // if (strengthChanged && localPlayerStrength == 0) propsToUpdate.Add(CombatStateManager.PLAYER_COMBAT_PLAYER_STRENGTH_PROP); // Removed Strength property update from here

        if (propsToUpdate.Count > 0)
        {
            // Only update properties whose values actually changed (or expired)
            UpdatePlayerStatusProperties(type: null, propertyKeysToUpdate: propsToUpdate.ToArray());
        }
        // --- END ADDED ---
    }
    
    public void DecrementLocalPetStatusEffects()
    {
        bool weakChanged = localPetWeakTurns > 0;
        bool breakChanged = localPetBreakTurns > 0;
        bool thornsChanged = localPetThorns > 0;
        bool strengthChanged = localPetStrength > 0;

        if (localPetWeakTurns > 0) localPetWeakTurns--;
        if (localPetBreakTurns > 0) localPetBreakTurns--;
        if (localPetThorns > 0) localPetThorns--; // Uncommented: Decrement Thorns
        if (localPetStrength > 0) localPetStrength--; // Decrement Strength

        Debug.Log($"Decremented Local Pet Status. Weak: {localPetWeakTurns}, Break: {localPetBreakTurns}, Thorns: {localPetThorns}, Strength: {localPetStrength}");

        // Update relevant pet properties if they changed (specifically if they expired or thorns decremented)
        List<string> propsToUpdate = new List<string>();
        if (weakChanged && localPetWeakTurns == 0) propsToUpdate.Add(CombatStateManager.PLAYER_COMBAT_PET_WEAK_PROP);
        if (breakChanged && localPetBreakTurns == 0) propsToUpdate.Add(CombatStateManager.PLAYER_COMBAT_PET_BREAK_PROP);
        if (thornsChanged) propsToUpdate.Add(CombatStateManager.PLAYER_COMBAT_PET_THORNS_PROP);
        if (strengthChanged && localPetStrength == 0) propsToUpdate.Add(CombatStateManager.PLAYER_COMBAT_PET_STRENGTH_PROP);

        if (propsToUpdate.Count > 0)
        {
            UpdatePetStatusProperties(propsToUpdate.ToArray()); // Call helper to update pet properties
        }
    }
    
    public void DecrementOpponentPetStatusEffects()
    {
        if (opponentPetWeakTurns > 0) opponentPetWeakTurns--;
        if (opponentPetBreakTurns > 0) opponentPetBreakTurns--;
        // if (opponentPetThorns > 0) opponentPetThorns--; // Decrement Thorns if needed
        // if (opponentPetStrength > 0) opponentPetStrength--; // Decrement Strength -> Moved to Turn End
        Debug.Log($"Decremented Opponent Pet Status (local sim - Turn Start). Weak: {opponentPetWeakTurns}, Break: {opponentPetBreakTurns}, Thorns: {opponentPetThorns}"); // Removed Strength from log
    }
    
    public void DecrementOpponentPetThorns()
    {
        if (opponentPetThorns > 0)
        {
            opponentPetThorns--;
            Debug.Log($"Decremented Opponent Pet Thorns (local sim). Thorns: {opponentPetThorns}");
        }
    }
    
    // --- ADDED: Strength Decrement at Turn End ---
    public void DecrementPlayerStrengthAtTurnEnd()
    {
        if (localPlayerStrength > 0)
        {
            localPlayerStrength--;
            Debug.Log($"Decremented Player Strength at Turn End. Remaining: {localPlayerStrength}");
            // Update property if strength reaches 0
            if (localPlayerStrength == 0)
            {
                UpdatePlayerStatusProperties(type: StatusEffectType.Strength); 
            }
        }
    }

    public void DecrementOpponentPetStrengthAtTurnEnd()
    {
        if (opponentPetStrength > 0)
        {
            opponentPetStrength--;
            Debug.Log($"Decremented Opponent Pet Strength at Turn End (local sim). Remaining: {opponentPetStrength}");
            // Note: Opponent pet strength is purely local simulation, no property update needed here.
        }
    }
    // --- END ADDED ---
    
    #endregion
    
    #region Combo Methods
    
    public void IncrementComboCount()
    {
        currentComboCount++;
        Debug.Log($"Combo count incremented to: {currentComboCount}");
    }

    public void ResetComboCount()
    {
        if (currentComboCount > 0)
        {
            Debug.Log($"Resetting combo count from {currentComboCount} to 0.");
            currentComboCount = 0;
        }
    }
    
    #endregion
    
    #region Status Check Methods
    
    public bool IsPlayerWeak() => localPlayerWeakTurns > 0;
    public bool IsPlayerBroken() => localPlayerBreakTurns > 0;
    public bool IsLocalPetWeak() => localPetWeakTurns > 0;
    public bool IsLocalPetBroken() => localPetBreakTurns > 0;
    public bool IsOpponentPetWeak() => opponentPetWeakTurns > 0;
    public bool IsOpponentPetBroken() => opponentPetBreakTurns > 0;
    
    // --- ADDED: Opponent Player Checks ---
    public bool IsOpponentPlayerWeak() => opponentPlayerWeakTurns > 0;
    public bool IsOpponentPlayerBroken() => opponentPlayerBreakTurns > 0;
    // --- END ADDED ---
    
    #endregion
    
    #region Getters
    
    public int GetCurrentComboCount() => currentComboCount;
    
    public int GetPlayerWeakTurns() => localPlayerWeakTurns;
    public int GetPlayerBreakTurns() => localPlayerBreakTurns;
    public int GetLocalPetWeakTurns() => localPetWeakTurns;
    public int GetLocalPetBreakTurns() => localPetBreakTurns;
    public int GetOpponentPetWeakTurns() => opponentPetWeakTurns;
    public int GetOpponentPetBreakTurns() => opponentPetBreakTurns;
    
    // --- ADDED: Thorns Getters --- 
    public int GetPlayerThorns() => localPlayerThorns;
    public int GetLocalPetThorns() => localPetThorns;
    public int GetOpponentPetThorns() => opponentPetThorns;
    // --- END ADDED ---
    
    // --- ADDED: Strength Getters ---
    public int GetPlayerStrength() => localPlayerStrength;
    public int GetLocalPetStrength() => localPetStrength;
    public int GetOpponentPetStrength() => opponentPetStrength;
    // --- END ADDED ---
    
    // --- ADDED: Opponent Player Getters ---
    public int GetOpponentPlayerWeakTurns() => opponentPlayerWeakTurns;
    public int GetOpponentPlayerBreakTurns() => opponentPlayerBreakTurns;
    public int GetOpponentPlayerThorns() => opponentPlayerThorns;
    public int GetOpponentPlayerStrength() => opponentPlayerStrength;
    // --- END ADDED ---
    
    #endregion

    // --- ADDED: Opponent Player Setters (Called from GameManager.OnPlayerPropertiesUpdate) ---
    public void SetOpponentPlayerWeakTurns(int value) { opponentPlayerWeakTurns = value; gameManager.UpdateHealthUI(); } // Update UI when opponent stats change
    public void SetOpponentPlayerBreakTurns(int value) { opponentPlayerBreakTurns = value; gameManager.UpdateHealthUI(); }
    public void SetOpponentPlayerThorns(int value) { opponentPlayerThorns = value; gameManager.UpdateHealthUI(); }
    public void SetOpponentPlayerStrength(int value) { opponentPlayerStrength = value; gameManager.UpdateHealthUI(); }
    // --- END ADDED ---

    // --- ADDED: Helper to update player status properties ---
    /// <summary>
    /// Updates specific Photon Custom Properties for the local player's combat status effects.
    /// Can update a single status based on type, specific keys, or all statuses.
    /// </summary>
    /// <param name="type">Optional StatusEffectType that changed.</param>
    /// <param name="propertyKeysToUpdate">Optional list of specific property keys (from CombatStateManager) to update.</param>
    public void UpdatePlayerStatusProperties(StatusEffectType? type = null, params string[] propertyKeysToUpdate)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;

        Hashtable propsToSet = new Hashtable();
        List<string> keys = propertyKeysToUpdate.Length > 0 ? propertyKeysToUpdate.ToList() : new List<string>();
        bool updateAll = keys.Count == 0 && type == null; // If no specific keys or type, update all

        // Determine which properties to include
        if (updateAll || type == StatusEffectType.Weak || keys.Contains(CombatStateManager.PLAYER_COMBAT_PLAYER_WEAK_PROP))
            propsToSet[CombatStateManager.PLAYER_COMBAT_PLAYER_WEAK_PROP] = localPlayerWeakTurns;
            
        if (updateAll || type == StatusEffectType.Break || keys.Contains(CombatStateManager.PLAYER_COMBAT_PLAYER_BREAK_PROP))
            propsToSet[CombatStateManager.PLAYER_COMBAT_PLAYER_BREAK_PROP] = localPlayerBreakTurns;
            
        if (updateAll || type == StatusEffectType.Thorns || keys.Contains(CombatStateManager.PLAYER_COMBAT_PLAYER_THORNS_PROP))
            propsToSet[CombatStateManager.PLAYER_COMBAT_PLAYER_THORNS_PROP] = localPlayerThorns;
            
        if (updateAll || type == StatusEffectType.Strength || keys.Contains(CombatStateManager.PLAYER_COMBAT_PLAYER_STRENGTH_PROP))
            propsToSet[CombatStateManager.PLAYER_COMBAT_PLAYER_STRENGTH_PROP] = localPlayerStrength;

        if (propsToSet.Count > 0)
        {
            PhotonNetwork.LocalPlayer.SetCustomProperties(propsToSet);
            // Debug.Log($"Updated player status properties: {string.Join(", ", propsToSet.Keys)}"); // Optional detailed log
        }
    }
    // --- END ADDED ---

    // --- ADDED: Methods to reset all status effects ---
    public void ResetAllPlayerStatusEffects()
    {
        localPlayerWeakTurns = 0;
        localPlayerBreakTurns = 0;
        localPlayerThorns = 0;
        localPlayerStrength = 0;
        
        // Update all player status properties
        UpdatePlayerStatusProperties(null);
        
        Debug.Log("Reset all player status effects to 0");
    }
    
    public void ResetAllLocalPetStatusEffects()
    {
        localPetWeakTurns = 0;
        localPetBreakTurns = 0;
        localPetThorns = 0;
        localPetStrength = 0;
        
        // Update pet status properties
        UpdatePetStatusProperties();
        
        Debug.Log("Reset all local pet status effects to 0");
    }
    
    public void ResetAllOpponentPetStatusEffects()
    {
        opponentPetWeakTurns = 0;
        opponentPetBreakTurns = 0;
        opponentPetThorns = 0;
        opponentPetStrength = 0;
        
        Debug.Log("Reset all opponent pet status effects to 0");
    }
    
    // Helper method to update all pet status properties
    private void UpdatePetStatusProperties(params string[] propertyKeysToUpdate)
    {
        List<string> petProps = new List<string>
        {
            CombatStateManager.PLAYER_COMBAT_PET_WEAK_PROP,
            CombatStateManager.PLAYER_COMBAT_PET_BREAK_PROP,
            CombatStateManager.PLAYER_COMBAT_PET_THORNS_PROP,
            CombatStateManager.PLAYER_COMBAT_PET_STRENGTH_PROP
        };
        
        foreach (string prop in petProps)
        {
            if (propertyKeysToUpdate.Contains(prop))
            {
                gameManager.GetPlayerManager()?.GetHealthManager()?.UpdatePlayerStatProperties(prop);
            }
        }
    }
    // --- END ADDED ---
}