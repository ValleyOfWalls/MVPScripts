using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Newtonsoft.Json;

public class PlayerManager
{
    private GameManager gameManager;
    
    // Sub-managers
    private HealthManager healthManager;
    private StatusEffectManager statusEffectManager;
    private EnergyManager energyManager;
    private CombatStateManager combatStateManager;
    private LobbyManager lobbyManager;
    
    // Constants (maintaining original API constants)
    public const string PLAYER_READY_PROPERTY = "IsReady";
    public const string COMBAT_FINISHED_PROPERTY = "CombatFinished";
    public const string PLAYER_SCORE_PROP = "PlayerScore";
    public const string PLAYER_BASE_PET_HP_PROP = "BasePetHP";
    public const string COMBAT_PAIRINGS_PROP = "CombatPairings";
    
    public PlayerManager(GameManager gameManager)
    {
        this.gameManager = gameManager;
        
        // Initialize sub-managers
        this.healthManager = new HealthManager(gameManager);
        this.statusEffectManager = new StatusEffectManager(gameManager);
        this.energyManager = new EnergyManager(gameManager);
        this.combatStateManager = new CombatStateManager(gameManager);
        this.lobbyManager = new LobbyManager(gameManager);
    }
    
    public void Initialize(int startingPlayerHealth, int startingPetHealth, int startingEnergy)
    {
        healthManager.Initialize(startingPlayerHealth, startingPetHealth);
        statusEffectManager.Initialize();
        energyManager.Initialize(startingEnergy);
        combatStateManager.Initialize();
        lobbyManager.Initialize();
    }
    
    #region Process Turn Start Methods
    
    public void ProcessPlayerTurnStartEffects()
    {
        // Apply DoT Damage FIRST
        healthManager.ProcessPlayerDotEffect();
        healthManager.ProcessPlayerHotEffect();
        
        // Then Decrement Status Effects & Cost Modifiers
        statusEffectManager.DecrementPlayerStatusEffects();
        energyManager.DecrementCostModifiers();
        healthManager.DecrementPlayerCritBuffDurations();
        
        Debug.Log("Processed Player Turn Start effects");
    }
    
    public void ProcessLocalPetTurnStartEffects()
    {
        // Apply DoT Damage FIRST
        healthManager.ProcessLocalPetDotEffect();
        healthManager.ProcessLocalPetHotEffect();
        
        // Then Decrement Status Effects
        statusEffectManager.DecrementLocalPetStatusEffects();
        healthManager.DecrementLocalPetCritBuffDurations();
        
        Debug.Log("Processed Local Pet Turn Start effects");
    }
    
    public void ProcessOpponentPetTurnStartEffects()
    {
        // Apply DoT Damage FIRST
        healthManager.ProcessOpponentPetDotEffect();
        healthManager.ProcessOpponentPetHotEffect();
        
        // Then Decrement Status Effects
        statusEffectManager.DecrementOpponentPetStatusEffects();
        // Decrement thorns for opponent pet on their turn
        statusEffectManager.DecrementOpponentPetThorns();
        healthManager.DecrementOpponentPetCritBuffDurations();
        
        Debug.Log("Processed Opponent Pet Turn Start effects");
    }
    
    #endregion
    
    #region Health & Damage Proxies
    
    public void DamageLocalPlayer(int amount, bool updateUIImmediate = true, HealthManager.DamageSource source = HealthManager.DamageSource.OpponentPetAttack) => healthManager.DamageLocalPlayer(amount, updateUIImmediate, source);
    public void DamageLocalPet(int amount) => healthManager.DamageLocalPet(amount);
    public void DamageOpponentPet(int amount, HealthManager.DamageSource source = HealthManager.DamageSource.PlayerSelfAttack) => healthManager.DamageOpponentPet(amount, source);
    
    public CombatCalculator.DamageResult ApplyDamageToLocalPetLocally(int amount) => healthManager.ApplyDamageToLocalPetLocally(amount);
    
    public void AddBlockToLocalPlayer(int amount) => healthManager.AddBlockToLocalPlayer(amount);
    public void AddBlockToLocalPet(int amount) => healthManager.AddBlockToLocalPet(amount);
    public void AddBlockToOpponentPet(int amount, bool updateUIImmediate = true) => healthManager.AddBlockToOpponentPet(amount, updateUIImmediate);
    public void ResetAllBlock() => healthManager.ResetAllBlock();
    
    public void HealLocalPlayer(int amount) => healthManager.HealLocalPlayer(amount);
    public void HealLocalPet(int amount) => healthManager.HealLocalPet(amount);
    public void HealOpponentPet(int amount) => healthManager.HealOpponentPet(amount);
    
    #endregion
    
    #region Status Effect Proxies
    
    public void ApplyStatusEffectLocalPlayer(StatusEffectType type, int duration) => statusEffectManager.ApplyStatusEffectLocalPlayer(type, duration);
    public void ApplyStatusEffectLocalPet(StatusEffectType type, int duration) => statusEffectManager.ApplyStatusEffectLocalPet(type, duration);
    public void ApplyStatusEffectOpponentPet(StatusEffectType type, int duration) => statusEffectManager.ApplyStatusEffectOpponentPet(type, duration);
    
    public void ApplyDotLocalPlayer(int damage, int duration) => healthManager.ApplyDotLocalPlayer(damage, duration);
    public void ApplyDotLocalPet(int damage, int duration) => healthManager.ApplyDotLocalPet(damage, duration);
    public void ApplyDotOpponentPet(int damage, int duration) => healthManager.ApplyDotOpponentPet(damage, duration);
    
    public void ApplyHotLocalPlayer(int amount, int duration) => healthManager.ApplyHotLocalPlayer(amount, duration);
    public void ApplyHotLocalPet(int amount, int duration) => healthManager.ApplyHotLocalPet(amount, duration);
    public void ApplyHotOpponentPet(int amount, int duration) => healthManager.ApplyHotOpponentPet(amount, duration);
    
    public void IncrementComboCount() => statusEffectManager.IncrementComboCount();
    public void ResetComboCount() => statusEffectManager.ResetComboCount();
    
    public void ApplyCritChanceBuffPlayer(int amount, int duration) => healthManager.ApplyCritChanceBuffPlayer(amount, duration);
    public void ApplyCritChanceBuffPet(int amount, int duration) => healthManager.ApplyCritChanceBuffPet(amount, duration);
    public void ApplyCritChanceBuffOpponentPet(int amount, int duration) => healthManager.ApplyCritChanceBuffOpponentPet(amount, duration);
    
    public void DecrementPlayerCritBuffDurations() => healthManager.DecrementPlayerCritBuffDurations();
    public void DecrementLocalPetCritBuffDurations() => healthManager.DecrementLocalPetCritBuffDurations();
    public void DecrementOpponentPetCritBuffDurations() => healthManager.DecrementOpponentPetCritBuffDurations();
    
    #endregion
    
    #region Energy Management Proxies
    
    public void ConsumeEnergy(int amount) => energyManager.ConsumeEnergy(amount);
    public void GainEnergy(int amount) => energyManager.GainEnergy(amount);
    
    public void ApplyCostModifierToOpponentHand(int amount, int duration, int cardCount) => 
        energyManager.ApplyCostModifierToOpponentHand(amount, duration, cardCount);
    
    public void ApplyCostModifierToLocalHand(int amount, int duration, int cardCount) => 
        energyManager.ApplyCostModifierToLocalHand(amount, duration, cardCount);
    
    public int GetLocalHandCostModifier(CardData card) => energyManager.GetLocalHandCostModifier(card);
    
    public void RemoveCostModifierForCard(CardData card) => energyManager.RemoveCostModifierForCard(card);
    
    #endregion
    
    #region Combat State Proxies
    
    public void InitializeCombatState(int opponentPetOwnerActorNum, int startingPlayerHealth, int startingPetHealth) =>
        combatStateManager.InitializeCombatState(opponentPetOwnerActorNum, startingPlayerHealth, startingPetHealth);
    
    public void SetPlayerCombatFinished(bool finished) => combatStateManager.SetPlayerCombatFinished(finished);
    
    public void CheckForAllPlayersFinishedCombat() => combatStateManager.CheckForAllPlayersFinishedCombat();
    
    public void ResetCombatFinishedFlags() => combatStateManager.ResetCombatFinishedFlags();
    
    public void PrepareNextCombatRound() => combatStateManager.PrepareNextCombatRound();
    
    public void UpdateScoreUI() => combatStateManager.UpdateScoreUI();
    
    public bool IsCombatEndedForLocalPlayer() => combatStateManager.IsCombatEndedForLocalPlayer();
    
    public void SetCombatEndedForLocalPlayer(bool value) => combatStateManager.SetCombatEndedForLocalPlayer(value);
    
    #endregion
    
    #region Lobby Management Proxies
    
    public void UpdatePlayerList(GameObject playerListPanel, GameObject playerEntryTemplate) => 
        lobbyManager.UpdatePlayerList(playerListPanel, playerEntryTemplate);
    
    public void SetPlayerReady(bool isReady) => lobbyManager.SetPlayerReady(isReady);
    
    public bool CheckAllPlayersReady() => lobbyManager.CheckAllPlayersReady();
    
    public void SetLocalPetName(string name) => lobbyManager.SetLocalPetName(name);
    
    public string GetLocalPetName() => lobbyManager.GetLocalPetName();
    
    #endregion
    
    #region Status Check Proxies
    
    public bool IsPlayerWeak() => statusEffectManager.IsPlayerWeak();
    public bool IsPlayerBroken() => statusEffectManager.IsPlayerBroken();
    public bool IsLocalPetWeak() => statusEffectManager.IsLocalPetWeak();
    public bool IsLocalPetBroken() => statusEffectManager.IsLocalPetBroken();
    public bool IsOpponentPetWeak() => statusEffectManager.IsOpponentPetWeak();
    public bool IsOpponentPetBroken() => statusEffectManager.IsOpponentPetBroken();
    
    #endregion
    
    #region Getters
    
    public int GetLocalPlayerHealth() => healthManager.GetLocalPlayerHealth();
    public int GetLocalPetHealth() => healthManager.GetLocalPetHealth();
    public int GetOpponentPetHealth() => healthManager.GetOpponentPetHealth();
    
    public int GetLocalPlayerBlock() => healthManager.GetLocalPlayerBlock();
    public int GetLocalPetBlock() => healthManager.GetLocalPetBlock();
    public int GetOpponentPetBlock() => healthManager.GetOpponentPetBlock();
    
    public int GetCurrentEnergy() => energyManager.GetCurrentEnergy();
    public void SetCurrentEnergy(int energy) => energyManager.SetCurrentEnergy(energy);
    public int GetStartingEnergy() => energyManager.GetStartingEnergy();
    
    public int GetCurrentComboCount() => statusEffectManager.GetCurrentComboCount();
    
    public int GetPlayerDotTurns() => healthManager.GetPlayerDotTurns();
    public int GetPlayerDotDamage() => healthManager.GetPlayerDotDamage();
    public int GetLocalPetDotTurns() => healthManager.GetLocalPetDotTurns();
    public int GetLocalPetDotDamage() => healthManager.GetLocalPetDotDamage();
    public int GetOpponentPetDotTurns() => healthManager.GetOpponentPetDotTurns();
    public int GetOpponentPetDotDamage() => healthManager.GetOpponentPetDotDamage();
    
    public int GetEffectivePlayerMaxHealth() => healthManager.GetEffectivePlayerMaxHealth();
    public int GetEffectivePetMaxHealth() => healthManager.GetEffectivePetMaxHealth();
    public int GetEffectiveOpponentPetMaxHealth() => healthManager.GetEffectiveOpponentPetMaxHealth();
    
    public Player GetOpponentPlayer() => combatStateManager.GetOpponentPlayer();
    public void SetOpponentPlayer(Player player) => combatStateManager.SetOpponentPlayer(player);
    
    public HealthManager GetHealthManager() => healthManager;
    public StatusEffectManager GetStatusEffectManager() => statusEffectManager;
    public EnergyManager GetEnergyManager() => energyManager;
    public CombatStateManager GetCombatStateManager() => combatStateManager;
    public LobbyManager GetLobbyManager() => lobbyManager;
    
    // Status Effect Turn Getters
    public int GetPlayerWeakTurns() => statusEffectManager.GetPlayerWeakTurns();
    public int GetPlayerBreakTurns() => statusEffectManager.GetPlayerBreakTurns();
    public int GetLocalPetWeakTurns() => statusEffectManager.GetLocalPetWeakTurns();
    public int GetLocalPetBreakTurns() => statusEffectManager.GetLocalPetBreakTurns();
    public int GetOpponentPetWeakTurns() => statusEffectManager.GetOpponentPetWeakTurns();
    public int GetOpponentPetBreakTurns() => statusEffectManager.GetOpponentPetBreakTurns();
    
    // Crit Chance Getters
    public int GetPlayerEffectiveCritChance() => healthManager.GetPlayerEffectiveCritChance();
    public int GetPetEffectiveCritChance() => healthManager.GetPetEffectiveCritChance();
    public int GetOpponentPetEffectiveCritChance() => healthManager.GetOpponentPetEffectiveCritChance();
    public int GetBaseCritChance() => healthManager.GetBaseCritChance();
    
    // Energy Getters for Pet Deck
    public int GetStartingPetEnergy() => gameManager.GetStartingPetEnergy();
    
    // --- ADDED: Thorns Proxies --- 
    public int GetPlayerThorns() => statusEffectManager.GetPlayerThorns();
    public int GetLocalPetThorns() => statusEffectManager.GetLocalPetThorns();
    public int GetOpponentPetThorns() => statusEffectManager.GetOpponentPetThorns();
    // --- END ADDED ---
    
    // --- ADDED: HoT Getters --- 
    public int GetPlayerHotTurns() => healthManager.GetPlayerHotTurns();
    public int GetPlayerHotAmount() => healthManager.GetPlayerHotAmount();
    public int GetLocalPetHotTurns() => healthManager.GetLocalPetHotTurns();
    public int GetLocalPetHotAmount() => healthManager.GetLocalPetHotAmount();
    public int GetOpponentPetHotTurns() => healthManager.GetOpponentPetHotTurns();
    public int GetOpponentPetHotAmount() => healthManager.GetOpponentPetHotAmount();
    // --- END ADDED ---
    
    // --- ADDED: Strength Getters --- 
    public int GetPlayerStrength() => statusEffectManager.GetPlayerStrength();
    public int GetLocalPetStrength() => statusEffectManager.GetLocalPetStrength();
    public int GetOpponentPetStrength() => statusEffectManager.GetOpponentPetStrength();
    // --- END ADDED ---
    
    #endregion
}