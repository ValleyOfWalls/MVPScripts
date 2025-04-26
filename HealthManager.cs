using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq; // Added for Sum()
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class HealthManager
{
    private GameManager gameManager;
    
    // Health data
    private int localPlayerHealth;
    private int localPetHealth;
    private int opponentPetHealth;
    private int localPlayerBlock = 0;
    private int localPetBlock = 0;
    private int opponentPetBlock = 0;
    
    // DoT tracking
    private int localPlayerDotTurns = 0;
    private int localPlayerDotDamage = 0;
    private int localPetDotTurns = 0;
    private int localPetDotDamage = 0;
    private int opponentPetDotTurns = 0;
    private int opponentPetDotDamage = 0;
    
    // --- ADDED: HoT Tracking ---
    private int localPlayerHotTurns = 0;
    private int localPlayerHotAmount = 0;
    private int localPetHotTurns = 0;
    private int localPetHotAmount = 0;
    private int opponentPetHotTurns = 0;
    private int opponentPetHotAmount = 0;
    // --- END ADDED ---
    
    // --- MODIFIED: Crit tracking using list of buffs ---
    private struct CritBuff
    {
        public int amount;
        public int turns;
    }
    private List<CritBuff> localPlayerCritBuffs = new List<CritBuff>();
    private List<CritBuff> localPetCritBuffs = new List<CritBuff>();
    private List<CritBuff> opponentPetCritBuffs = new List<CritBuff>();
    // --- END MODIFIED ---
    
    // --- ADDED: Opponent Player Simulation Data ---
    private int opponentPlayerHealth = 0; // Initial value, will be set by properties
    private int opponentPlayerBlock = 0;
    private int opponentPlayerDotTurns = 0;
    private int opponentPlayerDotDamage = 0;
    private int opponentPlayerHotTurns = 0;
    private int opponentPlayerHotAmount = 0;
    private int opponentPlayerCritChance = CombatCalculator.BASE_CRIT_CHANCE; // Start with base
    // --- END ADDED ---
    
    public enum DamageSource
    {
        OpponentPetAttack, // Normal attack from opponent pet
        OpponentPetThorns, // Damage coming FROM opponent pet's thorns
        PlayerSelfAttack,  // Player hitting themselves with a card/effect
        PlayerSelfThorns,  // Player hitting themselves with their own thorns (recursive prevention)
        PlayerComboEffect, // Added: Damage from a player's combo effect
        DiscardEffect,     // Added: Damage from a discard effect
        Other // e.g., DoT effects, environment?
    }
    
    public HealthManager(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public void Initialize(int startingPlayerHealth, int startingPetHealth)
    {
        this.localPlayerHealth = startingPlayerHealth;
        this.localPetHealth = startingPetHealth;
        
        // Reset DoT
        localPlayerDotTurns = 0;
        localPlayerDotDamage = 0;
        localPetDotTurns = 0;
        localPetDotDamage = 0;
        opponentPetDotTurns = 0;
        opponentPetDotDamage = 0;
        
        // Reset block
        localPlayerBlock = 0;
        localPetBlock = 0;
        opponentPetBlock = 0;
        
        // --- ADDED: Reset HoT ---
        localPlayerHotTurns = 0;
        localPlayerHotAmount = 0;
        localPetHotTurns = 0;
        localPetHotAmount = 0;
        opponentPetHotTurns = 0;
        opponentPetHotAmount = 0;
        // --- END ADDED ---
        
        // --- MODIFIED: Reset crit buffs ---
        localPlayerCritBuffs.Clear();
        localPetCritBuffs.Clear();
        opponentPetCritBuffs.Clear();
        // --- END MODIFIED ---
        
        // --- ADDED: Reset Opponent Player Sim ---
        opponentPlayerHealth = startingPlayerHealth; // Assume symmetric start for now
        opponentPlayerBlock = 0;
        opponentPlayerDotTurns = 0;
        opponentPlayerDotDamage = 0;
        opponentPlayerHotTurns = 0;
        opponentPlayerHotAmount = 0;
        opponentPlayerCritChance = CombatCalculator.BASE_CRIT_CHANCE;
        // --- END ADDED ---

        // --- ADDED: Set Initial Player Properties ---
        UpdatePlayerStatProperties();
        // --- END ADDED ---
    }
    
    public void InitializeCombatState(int startingPetHealth, Player opponentPlayer)
    {
        // Reset block
        localPlayerBlock = 0;
        localPetBlock = 0;
        opponentPetBlock = 0;
        
        // --- ADDED: Reset HoT at start of combat ---
        localPlayerHotTurns = 0;
        localPlayerHotAmount = 0;
        localPetHotTurns = 0;
        localPetHotAmount = 0;
        opponentPetHotTurns = 0;
        opponentPetHotAmount = 0;
        // --- END ADDED ---
        
        // --- ADDED: Reset crit buffs at start of combat ---
        localPlayerCritBuffs.Clear();
        localPetCritBuffs.Clear();
        opponentPetCritBuffs.Clear();
        // --- END ADDED ---
        
        // --- ADDED: Reset Opponent Player Sim ---
        // Health will be set by properties shortly, but initialize others
        opponentPlayerBlock = 0;
        opponentPlayerDotTurns = 0;
        opponentPlayerDotDamage = 0;
        opponentPlayerHotTurns = 0;
        opponentPlayerHotAmount = 0;
        opponentPlayerCritChance = CombatCalculator.BASE_CRIT_CHANCE;
        // --- END ADDED ---
        
        // Get opponent's base pet HP
        if (opponentPlayer != null && opponentPlayer.CustomProperties.TryGetValue(PlayerManager.PLAYER_BASE_PET_HP_PROP, out object oppBasePetHP))
        {
            try { opponentPetHealth = (int)oppBasePetHP; }
            catch { Debug.LogError($"Failed to cast {PlayerManager.PLAYER_BASE_PET_HP_PROP} for player {opponentPlayer.NickName}, using default {startingPetHealth}"); opponentPetHealth = startingPetHealth; }
        }
        else
        {
            opponentPetHealth = startingPetHealth; // Default if property not found
        }
        Debug.Log($"Set initial opponent pet health to {opponentPetHealth}");

        // --- ADDED: Update Player Properties at Combat Start ---
        UpdatePlayerStatProperties();
        // --- END ADDED ---
    }
    
    #region Damage Methods
    
    public void DamageLocalPlayer(int amount, bool updateUIImmediate = true, DamageSource source = DamageSource.OpponentPetAttack)
    {
        if (amount <= 0) return;
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        StatusEffectManager statusManager = playerManager.GetStatusEffectManager();
        
        bool isPlayerWeak = statusManager.IsPlayerWeak();
        bool isPlayerBroken = statusManager.IsPlayerBroken();
        
        Debug.Log($"DEBUG DamageLocalPlayer: Initial amount={amount}, Source={source}, isBroken={isPlayerBroken}, isWeak={isPlayerWeak}");
        
        CombatCalculator calculator = gameManager.GetCombatCalculator();
        
        // Determine attacker stats based on source
        int attackerStrength = 0;
        int attackerCritChance = 0;
        bool attackerIsWeak = false; 

        if (source == DamageSource.PlayerSelfAttack)
        {
            // Player is attacking themselves, use their strength and crit chance
            attackerStrength = playerManager.GetPlayerStrength();
            attackerCritChance = playerManager.GetPlayerEffectiveCritChance();
            attackerIsWeak = isPlayerWeak; // Use player's weak status
        }
        // --- ADDED: Handle OpponentPetAttack Source --- 
        else if (source == DamageSource.OpponentPetAttack)
        {
            // Opponent Pet is attacking, use their strength and crit chance
            attackerStrength = playerManager.GetOpponentPetStrength();
            attackerCritChance = playerManager.GetOpponentPetEffectiveCritChance(); 
            // Use opponent pet's weak status (which should be synced/available)
            attackerIsWeak = playerManager.GetStatusEffectManager().IsOpponentPetWeak(); 
            Debug.Log($"Opponent Pet Attacking Player. Strength: {attackerStrength}, Crit: {attackerCritChance}, Weak: {attackerIsWeak}");
        }
        // --- END ADDED ---
        // Thorns damage (OpponentPetThorns, PlayerSelfThorns) doesn't use strength/crit.
        
        CombatCalculator.DamageResult result = calculator.CalculateDamage(
            amount,
            attackerStrength, 
            localPlayerBlock,
            attackerIsWeak, 
            isPlayerBroken,
            attackerCritChance
        );
        
        localPlayerBlock -= result.BlockConsumed;
        if (localPlayerBlock < 0) localPlayerBlock = 0;
        
        Debug.Log($"DamageLocalPlayer: Incoming={amount}, Source={source}, BlockConsumed={result.BlockConsumed}, DamageAfterBlock={result.DamageAfterBlock}");
        
        if (result.DamageAfterBlock > 0)
        {
            localPlayerHealth -= result.DamageAfterBlock;
            if (localPlayerHealth < 0) localPlayerHealth = 0;
        }
        
        if (updateUIImmediate)
        {
            gameManager.UpdateHealthUI();
        }
        
        UpdatePlayerStatProperties();

        if (localPlayerHealth <= 0)
        {
            gameManager.GetPlayerManager().GetCombatStateManager().HandleCombatLoss();
        }
        
        // --- Thorns Logic --- 
        int playerThornsValue = gameManager.GetPlayerManager().GetPlayerThorns();

        // 1. Should Player reflect thorns back to the attacker?
        if (playerThornsValue > 0 && source != DamageSource.PlayerSelfThorns && source != DamageSource.OpponentPetThorns)
        {
            Debug.Log($"Player has {playerThornsValue} Thorns! Reflecting damage back to Opponent Pet.");
            DamageOpponentPet(playerThornsValue, DamageSource.PlayerSelfThorns); 
        }

        // 2. Should Player take damage from their own thorns (due to self-attack)?
        if (playerThornsValue > 0 && source == DamageSource.PlayerSelfAttack)
        {
            Debug.Log($"Player attacked themselves while having Thorns {playerThornsValue}! Applying self-thorns damage.");
            DamageLocalPlayer(playerThornsValue, false, DamageSource.PlayerSelfThorns); // Recurse safely
        }
        
        // If source is OpponentPetThorns or PlayerSelfThorns, no further thorns action

        // Add damage numbers if damage was dealt
        if (result.DamageAfterBlock > 0)
        {
            DamageNumberManager damageManager = gameManager.GetDamageNumberManager();
            if (damageManager != null)
            {
                GameObject playerObject = gameManager.GetCombatUIManager().GetPlayerUIArea();
                damageManager.ShowDamageNumber(result.DamageAfterBlock, playerObject, result.IsCritical);
            }
        }
    }
    
    /// <summary>
    /// Applies damage to the opponent's pet based on the local simulation state and checks for win condition.
    /// This method performs the actual health/block modification and UI update.
    /// It does NOT send any network notifications.
    /// </summary>
    /// <param name="amount">The base damage amount</param>
    /// <param name="source">The original source of the damage</param>
    public CombatCalculator.DamageResult ApplyDamageToOpponentPetLocally(int amount, DamageSource source)
    {
        if (amount <= 0) 
        {
            Debug.LogWarning("ApplyDamageToOpponentPetLocally called with amount <= 0.");
            // Return a default/zero result if no damage is processed
            return new CombatCalculator.DamageResult { DamageAfterBlock = 0, BlockConsumed = 0, IsCritical = false, DamageBeforeBlock = 0 };
        }

        PlayerManager playerManager = gameManager.GetPlayerManager();
        StatusEffectManager statusManager = playerManager.GetStatusEffectManager();
        CombatCalculator calculator = gameManager.GetCombatCalculator();
        
        // Get required statuses
        bool isPlayerWeak = statusManager.IsPlayerWeak(); 
        bool isOpponentPetBroken = statusManager.IsOpponentPetBroken();
        int playerCritChance = playerManager.GetPlayerEffectiveCritChance();
        int playerStrength = playerManager.GetPlayerStrength();
        
        // Calculate damage results
        CombatCalculator.DamageResult result = calculator.CalculateDamage(
            amount,                 // Raw damage
            playerStrength,         // Attacker strength
            opponentPetBlock,       // Target block
            isPlayerWeak,           // Attacker is weak
            isOpponentPetBroken,    // Target is broken
            playerCritChance        // Attacker crit chance
        );
        
        // Apply results
        opponentPetBlock -= result.BlockConsumed;
        if (opponentPetBlock < 0) opponentPetBlock = 0;

        Debug.Log($"ApplyDamageToOpponentPetLocally: Incoming={amount}, Source={source}, Est. Block={result.BlockConsumed}, RemainingDamage={result.DamageAfterBlock}");
        
        if (result.DamageAfterBlock > 0)
        {
            opponentPetHealth -= result.DamageAfterBlock;
            if (opponentPetHealth < 0) opponentPetHealth = 0;
            
            // Log critical hit if it happened
            if (result.IsCritical)
            {
                Debug.LogWarning($"Player CRITICAL HIT! (Chance: {playerCritChance}%)");
            }
        }

        gameManager.UpdateHealthUI(); // Update both health and block display
        
        // Sync the opponent pet block reduction to the opponent
        if (result.BlockConsumed > 0 && PhotonNetwork.InRoom)
        {
            Player opponentPlayer = playerManager.GetOpponentPlayer();
            if (opponentPlayer != null)
            {
                // Notify opponent their pet's block was reduced
                gameManager.GetPhotonView().RPC("RpcUpdateOpponentPetBlock", opponentPlayer, opponentPetBlock);
            }
        }
        
        // Check for win condition after applying damage
        if (opponentPetHealth <= 0)
        {
            gameManager.GetPlayerManager().GetCombatStateManager().HandleCombatWin();
        }
        
        // Apply Thorns Damage Back
        int opponentPetThorns = gameManager.GetPlayerManager().GetOpponentPetThorns();
        if (opponentPetThorns > 0 && source != DamageSource.PlayerSelfThorns)
        {
            Debug.Log($"Opponent Pet has {opponentPetThorns} Thorns! Dealing damage back to Player (Source was {source}).");
            DamageLocalPlayer(opponentPetThorns, false, DamageSource.OpponentPetThorns);
        }
        else if (opponentPetThorns > 0 && source == DamageSource.PlayerSelfThorns)
        {
            Debug.Log($"Opponent Pet has {opponentPetThorns} Thorns, but skipping reflection because source was PlayerSelfThorns.");
        }

        // Trigger damage number popup if damage was dealt
        if (result.DamageAfterBlock > 0)
        {
            DamageNumberManager damageManager = gameManager.GetDamageNumberManager();
            if (damageManager != null)
            {
                GameObject opponentPetObject = gameManager.GetCombatUIManager().GetOpponentPetUIArea();
                damageManager.ShowDamageNumber(result.DamageAfterBlock, opponentPetObject, result.IsCritical);
            }
        }

        return result;
    }

    /// <summary>
    /// Processes damage dealt by the local player to the opponent's pet.
    /// Applies the damage locally and notifies the opponent via RPC.
    /// </summary>
    /// <param name="amount">Damage amount</param>
    /// <param name="source">The original source of the damage (e.g., card attack, thorns)</param>
    public void DamageOpponentPet(int amount, DamageSource source = DamageSource.PlayerSelfAttack)
    {
        if (amount <= 0) return;
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        Player opponentPlayer = playerManager.GetOpponentPlayer();
        
        // Deal damage locally first, passing the source
        CombatCalculator.DamageResult result = ApplyDamageToOpponentPetLocally(amount, source);
        
        if (opponentPlayer != null)
        {
            // Send RPC to opponent that their pet took damage
            Debug.Log($"Sending RPC: RpcTakePetDamage({result.DamageAfterBlock}) to {opponentPlayer.NickName}");
            
            // We send only the FINAL damage amount after local calculations,
            // since the opponent cannot apply further defense calculations
            gameManager.GetPhotonView().RPC("RpcTakePetDamage", opponentPlayer, result.DamageAfterBlock);
        }
        else
        {
            Debug.LogWarning("DamageOpponentPet: Cannot send RPC, opponentPlayer is null. Only local simulation updated.");
        }
    }
    
    // --- ADDED: Method to calculate and apply damage to the LOCAL pet ---
    /// <summary>
    /// Applies damage to the local pet based on the local simulation state.
    /// This handles block, status effects (Weak, Break), Strength, and Crit.
    /// It updates health/block locally and syncs the pet's health property.
    /// </summary>
    public CombatCalculator.DamageResult ApplyDamageToLocalPetLocally(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("ApplyDamageToLocalPetLocally called with amount <= 0.");
            return new CombatCalculator.DamageResult { DamageAfterBlock = 0, BlockConsumed = 0, IsCritical = false, DamageBeforeBlock = 0 };
        }

        PlayerManager playerManager = gameManager.GetPlayerManager();
        StatusEffectManager statusManager = playerManager.GetStatusEffectManager();
        CombatCalculator calculator = gameManager.GetCombatCalculator();

        // Get required statuses for the interaction (Player attacking Local Pet)
        bool isPlayerWeak = statusManager.IsPlayerWeak();           // Attacker (Player) status
        bool isLocalPetBroken = statusManager.IsLocalPetBroken();     // Target (Local Pet) status
        int playerCritChance = playerManager.GetPlayerEffectiveCritChance(); // Attacker (Player) crit
        int playerStrength = playerManager.GetPlayerStrength();      // Attacker (Player) strength

        // Calculate damage results
        CombatCalculator.DamageResult result = calculator.CalculateDamage(
            amount,                 // Raw damage
            playerStrength,         // Attacker strength
            localPetBlock,          // Target block
            isPlayerWeak,           // Attacker is weak
            isLocalPetBroken,       // Target is broken
            playerCritChance        // Attacker crit chance
        );

        // Apply results
        localPetBlock -= result.BlockConsumed;
        if (localPetBlock < 0) localPetBlock = 0;

        Debug.Log($"ApplyDamageToLocalPetLocally: Incoming={amount}, IsBroken={isLocalPetBroken}, BlockConsumed={result.BlockConsumed}, RemainingDamage={result.DamageAfterBlock}, IsCrit={result.IsCritical}");

        if (result.DamageAfterBlock > 0)
        {
            localPetHealth -= result.DamageAfterBlock;
            if (localPetHealth < 0) localPetHealth = 0;

            // Log critical hit if it happened
            if (result.IsCritical)
            {
                Debug.LogWarning($"Player CRITICAL HIT against own Pet! (Chance: {playerCritChance}%)");
            }
        }

        gameManager.UpdateHealthUI(); // Update both health and block display

        // Update pet health property for network sync (since local pet health changed)
        UpdatePlayerStatProperties(CombatStateManager.PLAYER_COMBAT_PET_HP_PROP);
        
        // Also sync the pet block since we reduced it
        if (PhotonNetwork.InRoom)
        {
            gameManager.GetPhotonView().RPC("RpcUpdateLocalPetBlock", RpcTarget.Others, localPetBlock);
        }

        // Apply thorns for self-damage if pet has thorns
        int localPetThorns = playerManager.GetLocalPetThorns();
        if (localPetThorns > 0)
        {
            Debug.Log($"Local Pet has {localPetThorns} Thorns! Dealing damage back to Player.");
            // Player attacked their own Pet, so reflect thorns damage
            DamageLocalPlayer(localPetThorns, false, DamageSource.PlayerSelfThorns);
        }

        // Trigger damage number popup if damage was dealt
        if (result.DamageAfterBlock > 0)
        {
            DamageNumberManager damageManager = gameManager.GetDamageNumberManager();
            if (damageManager != null)
            {
                GameObject ownPetObject = gameManager.GetCombatUIManager().GetOwnPetUIArea();
                damageManager.ShowDamageNumber(result.DamageAfterBlock, ownPetObject, result.IsCritical);
            }
        }

        return result;
    }
    // --- END ADDED ---

    public void DamageLocalPet(int finalDamageAmount, bool updateUIImmediate = true)
    {
        if (finalDamageAmount <= 0) return;
        Debug.Log($"Damaging local pet for {finalDamageAmount}");
        
        // Directly apply the final calculated damage amount
        localPetHealth = Mathf.Max(0, localPetHealth - finalDamageAmount);
        
        // Notify our opponent who's fighting our pet that our pet took damage
        // so they can update their UI representing our pet
        Player opponentPlayer = gameManager.GetPlayerManager().GetOpponentPlayer();
        if (opponentPlayer != null)
        {
            gameManager.GetPhotonView().RPC("RpcNotifyOpponentOfLocalPetHealthChange", opponentPlayer, localPetHealth);
        }
        
        if (updateUIImmediate)
        {
            gameManager.UpdateHealthUI();
        }
    }
    
    #endregion
    
    #region Block Methods
    
    public void AddBlockToLocalPlayer(int amount)
    {
        if (amount <= 0) return;
        localPlayerBlock += amount;
        Debug.Log($"Added {amount} block to Local Player. New total: {localPlayerBlock}");
        gameManager.UpdateHealthUI(); // Update block display
        
        // --- ADDED: Update Property ---
        UpdatePlayerStatProperties(CombatStateManager.PLAYER_COMBAT_PLAYER_BLOCK_PROP);
        // --- END ADDED ---
    }

    public void AddBlockToLocalPet(int amount)
    {
        if (amount <= 0) return;
        localPetBlock += amount;
        Debug.Log($"Added {amount} block to Local Pet. New total: {localPetBlock}");
        gameManager.UpdateHealthUI(); // Update block display

        // --- ADDED: Network Sync for Local Pet Block ---
        if (PhotonNetwork.InRoom)
        {
            gameManager.GetPhotonView().RPC("RpcUpdateLocalPetBlock", RpcTarget.Others, localPetBlock); // Send the new total block
            // Debug.Log($"Sent RpcUpdateLocalPetBlock({localPetBlock}) to others.");
        }
        // --- END ADDED ---
    }
    
    public void AddBlockToOpponentPet(int amount, bool updateUIImmediate = true)
    {
        if (amount <= 0) return;
        
        // Update local simulation immediately
        opponentPetBlock += amount;
        Debug.Log($"Added {amount} block to Opponent Pet (local sim). New total: {opponentPetBlock}");
        if (updateUIImmediate)
        {
            gameManager.UpdateHealthUI(); // Update block display
        }

        // --- ADDED: Network Sync for Opponent Pet Block ---
        Player opponentPlayer = gameManager.GetPlayerManager().GetOpponentPlayer();
        if (PhotonNetwork.InRoom && opponentPlayer != null)
        {
            // Send RPC to the specific opponent, telling them to add block to *their* pet
            gameManager.GetPhotonView().RPC("RpcAddBlockToLocalPet", opponentPlayer, amount); 
            // Debug.Log($"Sent RpcAddBlockToLocalPet({amount}) to {opponentPlayer.NickName}.");
        }
        else if (opponentPlayer == null)
        {
            Debug.LogWarning("AddBlockToOpponentPet: Cannot send RPC, opponentPlayer is null.");
        }
        // --- END ADDED ---
    }

    // --- ADDED: Direct Setter for Opponent Pet Block ---
    public void SetOpponentPetBlock(int amount)
    {
        opponentPetBlock = Mathf.Max(0, amount); // Ensure block doesn't go below 0
        Debug.Log($"Set Opponent Pet block (from network update) to {opponentPetBlock}");
        gameManager.UpdateHealthUI(); // Update block display
    }
    // --- END ADDED ---

    // --- ADDED: Direct Setter for Local Pet Block ---
    public void SetLocalPetBlock(int amount)
    {
        localPetBlock = Mathf.Max(0, amount); // Ensure block doesn't go below 0
        Debug.Log($"Set Local Pet block (from network update) to {localPetBlock}");
        gameManager.UpdateHealthUI(); // Update block display
    }
    // --- END ADDED ---

    public void ResetAllBlock()
    {
        Debug.Log($"Resetting block. Player: {localPlayerBlock} -> 0, Pet: {localPetBlock} -> 0");
        bool playerBlockChanged = localPlayerBlock != 0; // Check if property needs update
        localPlayerBlock = 0;
        localPetBlock = 0;
        gameManager.UpdateHealthUI(); // Update block display
        
        // --- ADDED: Update Property if needed ---
        if (playerBlockChanged && PhotonNetwork.InRoom)
        {
            UpdatePlayerStatProperties(CombatStateManager.PLAYER_COMBAT_PLAYER_BLOCK_PROP);
        }
        // --- END ADDED ---
    }
    
    // --- ADDED: Reset only player block ---
    public void ResetPlayerBlockOnly()
    {
        if (localPlayerBlock == 0) return; // No change needed
        
        Debug.Log($"Resetting Player Block Only: {localPlayerBlock} -> 0");
        localPlayerBlock = 0;
        gameManager.UpdateHealthUI(); // Update block display
        
        // --- ADDED: Update Property ---
        UpdatePlayerStatProperties(CombatStateManager.PLAYER_COMBAT_PLAYER_BLOCK_PROP);
        // --- END ADDED ---
    }
    // --- END ADDED ---
    
    // --- ADDED: Reset only opponent pet block ---
    public void ResetOpponentPetBlockOnly()
    {
        Debug.Log($"Resetting Opponent Pet Block (Local Sim): {opponentPetBlock} -> 0");
        opponentPetBlock = 0;
         // Don't call UpdateHealthUI here, it will be called by the turn logic

       // --- ADDED: Notify the actual owner to reset their pet's block --- 
       Player opponentPlayer = gameManager.GetPlayerManager()?.GetOpponentPlayer();
       if (PhotonNetwork.InRoom && opponentPlayer != null)
       {
           Debug.Log($"Sending RpcResetMyPetBlock to opponent {opponentPlayer.NickName}.");
           gameManager.GetPhotonView()?.RPC("RpcResetMyPetBlock", opponentPlayer);
       }
       else if (opponentPlayer == null)
       {
           Debug.LogWarning("ResetOpponentPetBlockOnly: Cannot send RPC, opponentPlayer is null.");
       }
       // --- END ADDED ---
    }
    // --- END ADDED ---
    
    // --- ADDED: Reset only local pet block (called via RPC) ---
    public void ResetLocalPetBlockOnly()
    {
        Debug.Log($"Resetting Local Pet Block (RPC Triggered): {localPetBlock} -> 0");
        localPetBlock = 0;
        gameManager.UpdateHealthUI(); // Update UI immediately on the owner's client
    }
    // --- END ADDED ---
    
    #endregion
    
    #region Healing Methods
    
    public void HealLocalPlayer(int amount)
    {
        if (amount <= 0) return;
        int effectiveMaxHP = GetEffectivePlayerMaxHealth();
        int previousHealth = localPlayerHealth; // Store previous health for comparison

        // Calculate how much healing can actually be applied (don't exceed max HP)
        int actualHealing = Mathf.Min(amount, effectiveMaxHP - localPlayerHealth);

        if (actualHealing <= 0) return; // No healing needed if already at max HP

        // Apply the calculated healing
        localPlayerHealth += actualHealing;

        // Log, update UI, and update properties only if health actually changed
        Debug.Log($"Healed Local Player by {actualHealing} (requested {amount}). New health: {localPlayerHealth} / {effectiveMaxHP}");
        gameManager.UpdateHealthUI();
        UpdatePlayerStatProperties(CombatStateManager.PLAYER_COMBAT_PLAYER_HP_PROP);

        // Show healing number popup for the actual amount healed
        DamageNumberManager damageManager = gameManager.GetDamageNumberManager();
        if (damageManager != null)
        {
            GameObject playerObject = gameManager.GetCombatUIManager().GetPlayerUIArea();
            damageManager.ShowHealNumber(actualHealing, playerObject);
        }
    }

    public void HealLocalPet(int amount)
    {
        if (amount <= 0) return;
        int effectiveMaxHP = GetEffectivePetMaxHealth();
        int previousHealth = localPetHealth; // Store previous health for comparison

        // Calculate how much healing can actually be applied (don't exceed max HP)
        int actualHealing = Mathf.Min(amount, effectiveMaxHP - localPetHealth);

        if (actualHealing <= 0) return; // No healing needed if already at max HP

        // Apply the calculated healing
        localPetHealth += actualHealing;

        Debug.Log($"Healed Local Pet by {actualHealing} (requested {amount}). New health: {localPetHealth} / {effectiveMaxHP}");
        gameManager.UpdateHealthUI();

        // --- Update pet health property for network sync ---
        if (PhotonNetwork.InRoom)
        {
            Hashtable petProps = new Hashtable();
            petProps.Add(CombatStateManager.PLAYER_COMBAT_PET_HP_PROP, localPetHealth);
            PhotonNetwork.LocalPlayer.SetCustomProperties(petProps);
            Debug.Log($"Updated local pet health property to {localPetHealth}");

            Player opponentPlayer = gameManager.GetPlayerManager().GetOpponentPlayer();
            if (opponentPlayer != null)
            {
                Debug.Log($"Sending RpcNotifyOpponentOfLocalPetHealthChange({localPetHealth}) to opponent {opponentPlayer.NickName} after local pet was healed.");
                gameManager.GetPhotonView().RPC("RpcNotifyOpponentOfLocalPetHealthChange", opponentPlayer, localPetHealth);
            }
        }
        // --- END Update ---

        // Show healing number popup for the actual amount healed
        DamageNumberManager damageManager = gameManager.GetDamageNumberManager();
        if (damageManager != null)
        {
            GameObject ownPetObject = gameManager.GetCombatUIManager().GetOwnPetUIArea();
            damageManager.ShowHealNumber(actualHealing, ownPetObject);
        }
    }
    
    public void HealOpponentPet(int amount)
    {
        if (amount <= 0) return;
        
        // Calculate how much healing to apply (cap to max health)
        int effectiveMaxHealth = GetEffectiveOpponentPetMaxHealth();
        int actualHealingAmount = Mathf.Min(amount, effectiveMaxHealth - opponentPetHealth);
        
        // Apply healing
        opponentPetHealth = Mathf.Min(effectiveMaxHealth, opponentPetHealth + actualHealingAmount);
        
        // Notify the opponent that their pet was healed
        Player opponentPlayer = gameManager.GetPlayerManager().GetOpponentPlayer();
        if (opponentPlayer != null)
        {
            Debug.Log($"Sending RPC: RpcHealMyPet({actualHealingAmount}) to {opponentPlayer.NickName}");
            gameManager.GetPhotonView().RPC("RpcHealMyPet", opponentPlayer, actualHealingAmount);
        }
        else
        {
            Debug.LogWarning("HealOpponentPet: Cannot send RPC, opponentPlayer is null.");
        }
        
        // Update UI
        gameManager.UpdateHealthUI();
        
        // Show healing number on this client
        if (actualHealingAmount > 0)
        {
            DamageNumberManager damageManager = gameManager.GetDamageNumberManager();
            if (damageManager != null)
            {
                GameObject opponentPetObject = gameManager.GetCombatUIManager().GetOpponentPetUIArea();
                damageManager.ShowHealNumber(actualHealingAmount, opponentPetObject);
            }
        }
    }
    
    #endregion
    
    #region DoT Methods
    
    // --- New generic DoT application method ---
    private void ApplyDoTEffect(ref int targetTurns, ref int targetDamage, int damage, int duration, string targetName, params string[] propertiesToUpdate)
    {
        if (damage <= 0 || duration <= 0) return;
        
        targetTurns += duration;
        targetDamage = damage;
        Debug.Log($"Applied DoT to {targetName}: {damage} damage for {duration} turns. Total Turns: {targetTurns}");
        
        if (propertiesToUpdate.Length > 0)
        {
            UpdatePlayerStatProperties(propertiesToUpdate);
        }
    }
    
    public void ApplyDotLocalPlayer(int damage, int duration)
    {
        ApplyDoTEffect(
            ref localPlayerDotTurns, 
            ref localPlayerDotDamage, 
            damage, 
            duration, 
            "Player",
            CombatStateManager.PLAYER_COMBAT_PLAYER_DOT_TURNS_PROP,
            CombatStateManager.PLAYER_COMBAT_PLAYER_DOT_DMG_PROP
        );
    }
    
    public void ApplyDotLocalPet(int damage, int duration)
    {
        ApplyDoTEffect(
            ref localPetDotTurns, 
            ref localPetDotDamage, 
            damage, 
            duration, 
            "Local Pet",
            CombatStateManager.PLAYER_COMBAT_PET_DOT_TURNS_PROP,
            CombatStateManager.PLAYER_COMBAT_PET_DOT_DMG_PROP
        );

        // Notify others
        if (PhotonNetwork.InRoom)
        {
            gameManager.GetPhotonView()?.RPC("RpcApplyDoTToMyPet", RpcTarget.Others, damage, duration);
        }
    }
    
    public void ApplyDotOpponentPet(int damage, int duration, bool originatedFromRPC = false)
    {
        ApplyDoTEffect(
            ref opponentPetDotTurns, 
            ref opponentPetDotDamage, 
            damage, 
            duration, 
            "Opponent Pet (local sim)"
        );

        // Notify the actual owner
        Player opponentPlayer = gameManager.GetPlayerManager()?.GetOpponentPlayer();
        if (PhotonNetwork.InRoom && opponentPlayer != null && !originatedFromRPC)
        {
            gameManager.GetPhotonView()?.RPC("RpcApplyDoTToMyPet", opponentPlayer, damage, duration);
        }
        else if (originatedFromRPC)
        {
            Debug.Log("ApplyDotOpponentPet called from RPC, skipping send.");
        }
    }
    
    // --- Process DoT Effects ---
    private void ProcessDoTEffect(ref int turns, ref int damage, Action<int> damageAction, string targetName, params string[] propertiesToUpdate)
    {
        if (turns <= 0 || damage <= 0) return;
        
        Debug.Log($"{targetName} DoT ticking for {damage} damage.");
        damageAction(damage);
        
        turns--; // Decrement AFTER applying damage
        if (turns == 0) damage = 0; // Clear damage if duration ends
        
        if (propertiesToUpdate.Length > 0)
        {
            UpdatePlayerStatProperties(propertiesToUpdate);
        }
    }
    
    public void ProcessPlayerDotEffect()
    {
        ProcessDoTEffect(
            ref localPlayerDotTurns,
            ref localPlayerDotDamage,
            (damage) => DamageLocalPlayer(damage),
            "Player",
            CombatStateManager.PLAYER_COMBAT_PLAYER_DOT_TURNS_PROP,
            CombatStateManager.PLAYER_COMBAT_PLAYER_DOT_DMG_PROP
        );
    }
    
    public void ProcessLocalPetDotEffect()
    {
        ProcessDoTEffect(
            ref localPetDotTurns,
            ref localPetDotDamage,
            (damage) => DamageLocalPet(damage),
            "Local Pet"
        );
    }
    
    public void ProcessOpponentPetDotEffect()
    {
        // Use the generic processor to apply damage locally and decrement turns
        ProcessDoTEffect(
            ref opponentPetDotTurns,
            ref opponentPetDotDamage,
            (damage) => DamageOpponentPet(damage, DamageSource.Other), // Apply damage locally, source is DoT
            "Opponent Pet (local sim)"
            // No properties to update here, as opponent health is synced separately
        );
        /*
        if (opponentPetDotTurns > 0 && opponentPetDotDamage > 0)
        {
            Debug.Log($"Opponent Pet DoT duration ticking (local sim). Turns remaining: {opponentPetDotTurns - 1}");
            // Actual damage is handled by the owner's client. We only decrement turns locally.
            opponentPetDotTurns--; 
            if (opponentPetDotTurns == 0) opponentPetDotDamage = 0; 
        }
        */
    }
    
    public int GetEffectivePlayerMaxHealth()
    {
        // Return base health directly
        return gameManager.GetStartingPlayerHealth();
    }
    
    public int GetEffectivePetMaxHealth()
    {
        // Return base health directly
        return gameManager.GetStartingPetHealth();
    }
    
    public int GetEffectiveOpponentPetMaxHealth()
    {
        // Return base opponent pet health - Assuming symmetric starting health for now
        // TODO: Revisit if opponent base health can differ significantly and needs specific tracking
        Player opponentPlayer = gameManager.GetPlayerManager()?.GetOpponentPlayer();
        if (opponentPlayer != null && opponentPlayer.CustomProperties.TryGetValue(PlayerManager.PLAYER_BASE_PET_HP_PROP, out object oppBasePetHP))
        {
            try { return (int)oppBasePetHP; } 
            catch { /* Fall through to default */ }
        }
        return gameManager.GetStartingPetHealth(); // Fallback to default starting pet health
    }
    
    #endregion
    
    #region Crit Methods
    
    // Generic method to apply crit chance buff
    private void ApplyCritBuff(List<CritBuff> buffList, int amount, int duration, string targetName, 
                              Player targetPlayer = null, bool originatedFromRPC = false, params string[] propertiesToUpdate)
    {
        if (amount <= 0 || duration <= 0) return;
        
        buffList.Add(new CritBuff { amount = amount, turns = duration });
        Debug.Log($"Applied Crit Chance Buff to {targetName}: +{amount}% for {duration} turns.");
        
        // Update property if specified
        if (propertiesToUpdate.Length > 0)
        {
            UpdatePlayerStatProperties(propertiesToUpdate);
        }
        
        // Send RPC to target player if specified and not from RPC
        if (targetPlayer != null && !originatedFromRPC && PhotonNetwork.InRoom)
        {
            gameManager.GetPhotonView()?.RPC("RpcApplyCritBuffToMyPet", targetPlayer, amount, duration);
        }
    }
    
    public void ApplyCritChanceBuffPlayer(int amount, int duration)
    {
        ApplyCritBuff(
            localPlayerCritBuffs, 
            amount, 
            duration, 
            "Player", 
            null, 
            false,
            CombatStateManager.PLAYER_COMBAT_PLAYER_CRIT_PROP
        );
    }
    
    public void ApplyCritChanceBuffPet(int amount, int duration)
    {
        ApplyCritBuff(
            localPetCritBuffs, 
            amount, 
            duration, 
            "Pet", 
            null, 
            false,
            CombatStateManager.PLAYER_COMBAT_PET_CRIT_PROP
        );
        
        // Notify others
        if (PhotonNetwork.InRoom)
        {
            gameManager.GetPhotonView()?.RPC("RpcApplyCritBuffToMyPet", RpcTarget.Others, amount, duration);
        }
    }
    
    public void ApplyCritChanceBuffOpponentPet(int amount, int duration, bool originatedFromRPC = false)
    {
        Player opponentPlayer = gameManager.GetPlayerManager()?.GetOpponentPlayer();
        
        ApplyCritBuff(
            opponentPetCritBuffs, 
            amount, 
            duration, 
            "Opponent Pet (local sim)", 
            opponentPlayer, 
            originatedFromRPC
        );
    }
    
    // Generic method to decrement crit buff durations
    private bool DecrementCritBuffs(List<CritBuff> buffList, string targetName, params string[] propertiesToUpdate)
    {
        bool changed = false;
        
        for (int i = buffList.Count - 1; i >= 0; i--)
        {
            CritBuff buff = buffList[i];
            buff.turns--;
            
            if (buff.turns <= 0)
            {
                buffList.RemoveAt(i);
                Debug.Log($"{targetName} Crit Buff ({buff.amount}%) expired.");
                changed = true;
            }
            else
            {
                buffList[i] = buff; // Update turns
            }
        }
        
        // Update properties if changed and properties specified
        if (changed && propertiesToUpdate.Length > 0)
        {
            UpdatePlayerStatProperties(propertiesToUpdate);
        }
        
        return changed;
    }
    
    public void DecrementPlayerCritBuffDurations()
    {
        DecrementCritBuffs(
            localPlayerCritBuffs, 
            "Player", 
            CombatStateManager.PLAYER_COMBAT_PLAYER_CRIT_PROP
        );
    }
    
    public void DecrementLocalPetCritBuffDurations()
    {
        DecrementCritBuffs(
            localPetCritBuffs, 
            "Local Pet", 
            CombatStateManager.PLAYER_COMBAT_PET_CRIT_PROP
        );
    }
    
    public void DecrementOpponentPetCritBuffDurations()
    {
        DecrementCritBuffs(
            opponentPetCritBuffs, 
            "Opponent Pet"
        );
    }
    
    // Generic method to calculate effective crit chance
    private int GetEffectiveCritChance(List<CritBuff> buffList)
    {
        int sumBonus = buffList.Sum(buff => buff.amount);
        return Mathf.Max(0, CombatCalculator.BASE_CRIT_CHANCE + sumBonus); // Ensure non-negative
    }
    
    public int GetPlayerEffectiveCritChance()
    {
        return GetEffectiveCritChance(localPlayerCritBuffs);
    }
    
    public int GetPetEffectiveCritChance()
    {
        return GetEffectiveCritChance(localPetCritBuffs);
    }
    
    public int GetOpponentPetEffectiveCritChance()
    {
        return GetEffectiveCritChance(opponentPetCritBuffs);
    }
    
    public int GetBaseCritChance()
    {
        return CombatCalculator.BASE_CRIT_CHANCE;
    }
    
    #endregion
    
    #region Getters and Setters
    
    public int GetLocalPlayerHealth() => localPlayerHealth;
    public int GetLocalPetHealth() => localPetHealth;
    public int GetOpponentPetHealth() => opponentPetHealth;
    
    public int GetLocalPlayerBlock() => localPlayerBlock;
    public int GetLocalPetBlock() => localPetBlock;
    public int GetOpponentPetBlock() => opponentPetBlock;
    
    public int GetPlayerDotTurns() => localPlayerDotTurns;
    public int GetPlayerDotDamage() => localPlayerDotDamage;
    public int GetLocalPetDotTurns() => localPetDotTurns;
    public int GetLocalPetDotDamage() => localPetDotDamage;
    public int GetOpponentPetDotTurns() => opponentPetDotTurns;
    public int GetOpponentPetDotDamage() => opponentPetDotDamage;
    
    // --- ADDED: HoT Getters ---
    public int GetPlayerHotTurns() => localPlayerHotTurns;
    public int GetPlayerHotAmount() => localPlayerHotAmount;
    public int GetLocalPetHotTurns() => localPetHotTurns;
    public int GetLocalPetHotAmount() => localPetHotAmount;
    public int GetOpponentPetHotTurns() => opponentPetHotTurns;
    public int GetOpponentPetHotAmount() => opponentPetHotAmount;
    // --- END ADDED ---
    
    // --- ADDED: Opponent Player Getters ---
    public int GetOpponentPlayerHealth() => opponentPlayerHealth;
    public int GetOpponentPlayerBlock() => opponentPlayerBlock;
    public int GetOpponentPlayerDotTurns() => opponentPlayerDotTurns;
    public int GetOpponentPlayerDotDamage() => opponentPlayerDotDamage;
    public int GetOpponentPlayerHotTurns() => opponentPlayerHotTurns;
    public int GetOpponentPlayerHotAmount() => opponentPlayerHotAmount;
    public int GetOpponentPlayerCritChance() => opponentPlayerCritChance;
    // --- END ADDED ---

    // --- ADDED: Opponent Player Setters (Called from GameManager.OnPlayerPropertiesUpdate) ---
    public void SetOpponentPlayerHealth(int value) { opponentPlayerHealth = value; gameManager.UpdateHealthUI(); } // Update UI when opponent stats change
    public void SetOpponentPlayerBlock(int value) { opponentPlayerBlock = value; gameManager.UpdateHealthUI(); }
    public void SetOpponentPlayerDot(int turns, int damage) { opponentPlayerDotTurns = turns; opponentPlayerDotDamage = damage; gameManager.UpdateHealthUI(); }
    public void SetOpponentPlayerHot(int turns, int amount) { opponentPlayerHotTurns = turns; opponentPlayerHotAmount = amount; gameManager.UpdateHealthUI(); }
    public void SetOpponentPlayerCritChance(int value) { opponentPlayerCritChance = value; gameManager.UpdateHealthUI(); }
    // --- END ADDED ---
    
    #endregion
    
    // --- RE-ADDED: HoT Methods --- 
    #region HoT Methods
    
    // --- New generic HoT application method ---
    private void ApplyHoTEffect(ref int targetTurns, ref int targetAmount, int amount, int duration, string targetName, params string[] propertiesToUpdate)
    {
        if (amount <= 0 || duration <= 0) return;
        
        targetTurns += duration;
        targetAmount = amount; // Overwrite amount with latest application
        Debug.Log($"Applied HoT to {targetName}: {amount} healing for {duration} turns. Total Turns: {targetTurns}");
        gameManager.UpdateHealthUI(); // Update UI to show status
        
        if (propertiesToUpdate.Length > 0)
        {
            UpdatePlayerStatProperties(propertiesToUpdate);
        }
    }
    
    public void ApplyHotLocalPlayer(int amount, int duration)
    {
        ApplyHoTEffect(
            ref localPlayerHotTurns, 
            ref localPlayerHotAmount, 
            amount, 
            duration, 
            "Player",
            CombatStateManager.PLAYER_COMBAT_PLAYER_HOT_TURNS_PROP,
            CombatStateManager.PLAYER_COMBAT_PLAYER_HOT_AMT_PROP
        );
    }
    
    public void ApplyHotLocalPet(int amount, int duration)
    {
        ApplyHoTEffect(
            ref localPetHotTurns, 
            ref localPetHotAmount, 
            amount, 
            duration, 
            "Local Pet",
            CombatStateManager.PLAYER_COMBAT_PET_HOT_TURNS_PROP,
            CombatStateManager.PLAYER_COMBAT_PET_HOT_AMT_PROP
        );

        // Notify others
        if (PhotonNetwork.InRoom)
        {
            gameManager.GetPhotonView()?.RPC("RpcApplyHoTToMyPet", RpcTarget.Others, amount, duration);
        }
    }
    
    public void ApplyHotOpponentPet(int amount, int duration, bool originatedFromRPC = false)
    {
        ApplyHoTEffect(
            ref opponentPetHotTurns, 
            ref opponentPetHotAmount, 
            amount, 
            duration, 
            "Opponent Pet (local sim)"
        );

        // Notify the actual owner
        if (!originatedFromRPC)
        {
            Player opponentPlayer = gameManager.GetPlayerManager()?.GetOpponentPlayer();
            if (PhotonNetwork.InRoom && opponentPlayer != null)
            {
                gameManager.GetPhotonView()?.RPC("RpcApplyHoTToMyPet", opponentPlayer, amount, duration);
            }
        }
    }
    
    // --- Process HoT Effects ---
    private void ProcessHoTEffect(ref int turns, ref int amount, Action<int> healAction, string targetName, params string[] propertiesToUpdate)
    {
        if (turns <= 0 || amount <= 0) return;
        
        Debug.Log($"{targetName} HoT ticking for {amount} healing.");
        healAction(amount);
        
        turns--; // Decrement AFTER applying heal
        if (turns == 0) amount = 0; // Clear amount if duration ends
        
        if (propertiesToUpdate.Length > 0)
        {
            UpdatePlayerStatProperties(propertiesToUpdate);
        }
        
        gameManager.UpdateHealthUI(); // Update UI after heal and decrement
    }
    
    public void ProcessPlayerHotEffect()
    {
        ProcessHoTEffect(
            ref localPlayerHotTurns,
            ref localPlayerHotAmount,
            (amount) => HealLocalPlayer(amount),
            "Player",
            CombatStateManager.PLAYER_COMBAT_PLAYER_HOT_TURNS_PROP,
            CombatStateManager.PLAYER_COMBAT_PLAYER_HOT_AMT_PROP
        );
    }
    
    public void ProcessLocalPetHotEffect()
    {
        ProcessHoTEffect(
            ref localPetHotTurns,
            ref localPetHotAmount,
            (amount) => HealLocalPet(amount),
            "Local Pet",
            CombatStateManager.PLAYER_COMBAT_PET_HOT_TURNS_PROP,
            CombatStateManager.PLAYER_COMBAT_PET_HOT_AMT_PROP
        );
    }
    
    public void ProcessOpponentPetHotEffect()
    {
        if (opponentPetHotTurns <= 0 || opponentPetHotAmount <= 0) return;
        
        Debug.Log($"Opponent Pet HoT ticking for {opponentPetHotAmount} healing.");
        HealOpponentPet(opponentPetHotAmount);
        
        opponentPetHotTurns--; // Decrement AFTER applying heal
        if (opponentPetHotTurns == 0) opponentPetHotAmount = 0; // Clear amount if duration ends
        
        gameManager.UpdateHealthUI(); // Update UI after heal and decrement
    }
    
    #endregion
    // --- END RE-ADDED ---

    // --- ADDED: Helper to update player properties ---
    /// <summary>
    /// Updates specific Photon Custom Properties for the local player's combat stats.
    /// If no specific keys are provided, updates all relevant stats.
    /// </summary>
    /// <param name="propertyKeysToUpdate">Optional list of specific property keys (from CombatStateManager) to update.</param>
    public void UpdatePlayerStatProperties(params string[] propertyKeysToUpdate)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;

        Hashtable propsToSet = new Hashtable();
        List<string> keys = propertyKeysToUpdate.ToList();
        bool updateAll = keys.Count == 0; // If no specific keys, update all
        
        // Get Status Effect Manager for pet status
        StatusEffectManager statusManager = gameManager.GetPlayerManager()?.GetStatusEffectManager();

        // --- Player Properties ---
        if (updateAll || keys.Contains(CombatStateManager.PLAYER_COMBAT_PLAYER_HP_PROP))
            propsToSet[CombatStateManager.PLAYER_COMBAT_PLAYER_HP_PROP] = localPlayerHealth;
        if (updateAll || keys.Contains(CombatStateManager.PLAYER_COMBAT_PLAYER_BLOCK_PROP))
            propsToSet[CombatStateManager.PLAYER_COMBAT_PLAYER_BLOCK_PROP] = localPlayerBlock;
        if (updateAll || keys.Contains(CombatStateManager.PLAYER_COMBAT_PLAYER_DOT_TURNS_PROP))
            propsToSet[CombatStateManager.PLAYER_COMBAT_PLAYER_DOT_TURNS_PROP] = localPlayerDotTurns;
        if (updateAll || keys.Contains(CombatStateManager.PLAYER_COMBAT_PLAYER_DOT_DMG_PROP))
            propsToSet[CombatStateManager.PLAYER_COMBAT_PLAYER_DOT_DMG_PROP] = localPlayerDotDamage;
        if (updateAll || keys.Contains(CombatStateManager.PLAYER_COMBAT_PLAYER_HOT_TURNS_PROP))
            propsToSet[CombatStateManager.PLAYER_COMBAT_PLAYER_HOT_TURNS_PROP] = localPlayerHotTurns;
        if (updateAll || keys.Contains(CombatStateManager.PLAYER_COMBAT_PLAYER_HOT_AMT_PROP))
            propsToSet[CombatStateManager.PLAYER_COMBAT_PLAYER_HOT_AMT_PROP] = localPlayerHotAmount;
        if (updateAll || keys.Contains(CombatStateManager.PLAYER_COMBAT_PLAYER_CRIT_PROP))
            propsToSet[CombatStateManager.PLAYER_COMBAT_PLAYER_CRIT_PROP] = GetPlayerEffectiveCritChance();
        // Player Energy is handled separately by EnergyManager
        // Player Weak/Break/Thorns/Strength are handled by StatusEffectManager

        // --- Local Pet Properties ---
        if (updateAll || keys.Contains(CombatStateManager.PLAYER_COMBAT_PET_HP_PROP))
            propsToSet[CombatStateManager.PLAYER_COMBAT_PET_HP_PROP] = localPetHealth;
        if (updateAll || keys.Contains(CombatStateManager.PLAYER_COMBAT_PET_DOT_TURNS_PROP))
            propsToSet[CombatStateManager.PLAYER_COMBAT_PET_DOT_TURNS_PROP] = localPetDotTurns;
        if (updateAll || keys.Contains(CombatStateManager.PLAYER_COMBAT_PET_DOT_DMG_PROP))
            propsToSet[CombatStateManager.PLAYER_COMBAT_PET_DOT_DMG_PROP] = localPetDotDamage;
        if (updateAll || keys.Contains(CombatStateManager.PLAYER_COMBAT_PET_HOT_TURNS_PROP))
            propsToSet[CombatStateManager.PLAYER_COMBAT_PET_HOT_TURNS_PROP] = localPetHotTurns;
        if (updateAll || keys.Contains(CombatStateManager.PLAYER_COMBAT_PET_HOT_AMT_PROP))
            propsToSet[CombatStateManager.PLAYER_COMBAT_PET_HOT_AMT_PROP] = localPetHotAmount;
        if (updateAll || keys.Contains(CombatStateManager.PLAYER_COMBAT_PET_CRIT_PROP))
            propsToSet[CombatStateManager.PLAYER_COMBAT_PET_CRIT_PROP] = GetPetEffectiveCritChance();
            
        // Fetch Pet status effects from StatusEffectManager
        if (statusManager != null) 
        { 
            if (updateAll || keys.Contains(CombatStateManager.PLAYER_COMBAT_PET_WEAK_PROP))
                propsToSet[CombatStateManager.PLAYER_COMBAT_PET_WEAK_PROP] = statusManager.GetLocalPetWeakTurns();
            if (updateAll || keys.Contains(CombatStateManager.PLAYER_COMBAT_PET_BREAK_PROP))
                propsToSet[CombatStateManager.PLAYER_COMBAT_PET_BREAK_PROP] = statusManager.GetLocalPetBreakTurns();
            if (updateAll || keys.Contains(CombatStateManager.PLAYER_COMBAT_PET_THORNS_PROP))
                propsToSet[CombatStateManager.PLAYER_COMBAT_PET_THORNS_PROP] = statusManager.GetLocalPetThorns();
            if (updateAll || keys.Contains(CombatStateManager.PLAYER_COMBAT_PET_STRENGTH_PROP))
                propsToSet[CombatStateManager.PLAYER_COMBAT_PET_STRENGTH_PROP] = statusManager.GetLocalPetStrength();
        }

        if (propsToSet.Count > 0)
        {
            PhotonNetwork.LocalPlayer.SetCustomProperties(propsToSet);
            // Debug.Log($"Updated player properties: {string.Join(", ", propsToSet.Keys)}"); // Optional detailed log
        }
    }
    // --- END ADDED ---

    // Add method to directly set opponent pet health for synchronization
    public void SetOpponentPetHealth(int newHealth)
    {
        opponentPetHealth = newHealth;
        Debug.Log($"Set opponent pet health to {newHealth} from network sync");
    }

    // --- ADDED: Methods to reset all effects ---
    public void ResetAllDoTEffects()
    {
        // Reset player DoT
        localPlayerDotTurns = 0;
        localPlayerDotDamage = 0;
        
        // Reset local pet DoT
        localPetDotTurns = 0;
        localPetDotDamage = 0;
        
        // Reset opponent pet DoT
        opponentPetDotTurns = 0;
        opponentPetDotDamage = 0;
        
        // Reset opponent player DoT
        opponentPlayerDotTurns = 0;
        opponentPlayerDotDamage = 0;
        
        // Update UI & properties
        UpdatePlayerStatProperties();
        gameManager.UpdateHealthUI();
    }
    
    public void ResetAllHoTEffects()
    {
        // Reset player HoT
        localPlayerHotTurns = 0;
        localPlayerHotAmount = 0;
        
        // Reset local pet HoT 
        localPetHotTurns = 0;
        localPetHotAmount = 0;
        
        // Reset opponent pet HoT
        opponentPetHotTurns = 0;
        opponentPetHotAmount = 0;
        
        // Reset opponent player HoT
        opponentPlayerHotTurns = 0;
        opponentPlayerHotAmount = 0;
        
        // Update UI & properties
        UpdatePlayerStatProperties();
        gameManager.UpdateHealthUI();
    }
    
    public void ResetAllCritBuffs()
    {
        // Clear all crit buffs
        localPlayerCritBuffs.Clear();
        localPetCritBuffs.Clear();
        opponentPetCritBuffs.Clear();
        
        // Update crit properties
        UpdatePlayerStatProperties(
            CombatStateManager.PLAYER_COMBAT_PLAYER_CRIT_PROP,
            CombatStateManager.PLAYER_COMBAT_PET_CRIT_PROP
        );
        
        Debug.Log("Reset all crit buffs");
    }
    // --- END ADDED ---
}