using UnityEngine;
using System.Collections.Generic;
using Photon.Realtime;
using Photon.Pun;
using System.Collections;

public class CardEffectService
{
    private GameManager gameManager;
    private CombatTurnManager _combatTurnManager;
    private Transform effectsCanvasTransform; // Added: Reference to the effects canvas
    private Camera effectsCanvasCamera; // Added: Camera assigned to the effects canvas
    private float effectsCanvasPlaneDistance; // Added: Plane distance for position calculation
    
    private CombatTurnManager CombatTurnManagerInstance
    {
        get
        {
            if (_combatTurnManager == null)
            {
                _combatTurnManager = gameManager.GetCombatManager()?.GetCombatTurnManager();
                if (_combatTurnManager == null) {
                     Debug.LogError("CardEffectService failed to get CombatTurnManager on demand!");
                }
            }
            return _combatTurnManager;
        }
    }
    
    public CardEffectService(GameManager gameManager)
    {
        this.gameManager = gameManager;
        
        // Find the Effects Canvas and its camera
        GameObject effectsCanvasGO = GameObject.Find("EffectsCanvas"); // Find by name (adjust if needed)
        if (effectsCanvasGO != null)
        {
            Canvas canvas = effectsCanvasGO.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                effectsCanvasTransform = effectsCanvasGO.transform;
                effectsCanvasCamera = canvas.worldCamera; 
                effectsCanvasPlaneDistance = canvas.planeDistance;
                if (effectsCanvasCamera == null)
                {
                     Debug.LogError("EffectsCanvas found, but it has no Render Camera assigned!");
                }
            }
            else
            {
                 Debug.LogError("Found EffectsCanvas, but it's not set to Screen Space - Camera mode!");
            }
        }
        else
        {
             Debug.LogError("CardEffectService could not find the EffectsCanvas GameObject! Make sure it exists in the scene and is named correctly.");
        }
    }
    
    public bool ProcessCardEffect(CardData cardData, CardDropZone.TargetType targetType, int previousPlaysThisCombat, int totalCopies)
    {
        PlayerManager playerManager = gameManager.GetPlayerManager();
        int currentTurn = CombatTurnManagerInstance?.GetCurrentCombatTurn() ?? 1; // Get current turn, default to 1 if manager missing
        CombatCalculator calculator = gameManager.GetCombatCalculator(); // Get the combat calculator
        
        // --- Check Player Health Status for Threshold --- 
        bool isBelowHealthThreshold = CheckHealthThreshold(cardData);
        
        // --- Play Target Visual Effect --- 
        if (cardData.targetEffectPrefab != null) 
        {
            PlayTargetEffectPrefab(targetType, cardData.targetEffectPrefab);
        }
        
        // Apply target-specific effects
        ApplyTargetSpecificEffects(cardData, targetType, currentTurn, calculator, previousPlaysThisCombat, totalCopies, isBelowHealthThreshold);
        
        // Apply target-independent effects
        ApplyCardDrawEffect(cardData, targetType);
        ApplyCardDiscardEffect(cardData, targetType);
        
        // Combo Check
        if (cardData.isComboStarter)
        {
            playerManager.IncrementComboCount();
            if (playerManager.GetCurrentComboCount() >= cardData.comboTriggerValue)
            {
                Debug.Log($"COMBO TRIGGERED! ({playerManager.GetCurrentComboCount()}/{cardData.comboTriggerValue}) Applying effect: {cardData.comboEffectType}");
                ApplyComboEffect(cardData, targetType);
            }
        }
        
        // Apply Cost Modifier Effect
        ApplyCostModifierEffect(cardData);
        
        // Apply Crit Chance Buff Effect
        ApplyCritChanceBuffEffect(cardData, targetType);
        
        // Apply Temporary Hand Upgrade Effect
        if (cardData.upgradeHandCardCount > 0)
        {
            gameManager.GetCardManager().GetCardModificationService().ApplyTemporaryHandUpgrade(
                cardData.upgradeHandCardCount, 
                cardData.upgradeHandCardDuration, 
                cardData.upgradeHandCardTargetRule);
        }
        
        return true;
    }
    
    private bool CheckHealthThreshold(CardData cardData)
    {
        if (cardData.healthThresholdPercent <= 0) return false;
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        int currentHP = playerManager.GetLocalPlayerHealth();
        int maxHP = playerManager.GetEffectivePlayerMaxHealth();
        bool isBelowThreshold = gameManager.GetCombatCalculator().IsEntityBelowHealthThreshold(cardData, currentHP, maxHP);
        
        if (isBelowThreshold)
        {
            Debug.Log($"Player health ({currentHP}/{maxHP}) is below threshold ({cardData.healthThresholdPercent}%). Applying enhanced effect.");
        }
        
        return isBelowThreshold;
    }
    
    private void ApplyTargetSpecificEffects(CardData cardData, CardDropZone.TargetType targetType, int currentTurn, 
        CombatCalculator calculator, int previousPlaysThisCombat, int totalCopies, bool isBelowHealthThreshold)
    {
        PlayerManager playerManager = gameManager.GetPlayerManager();
        
        switch (targetType)
        {
            case CardDropZone.TargetType.EnemyPet:
                ApplyDamageEffect(cardData, targetType, currentTurn, calculator, previousPlaysThisCombat, totalCopies, isBelowHealthThreshold);
                ApplyBlockEffect(cardData, targetType, currentTurn, calculator, previousPlaysThisCombat, totalCopies, isBelowHealthThreshold);
                ApplyHealingEffect(cardData, targetType);
                ApplyStatusEffect(cardData, targetType);
                ApplyDoTEffect(cardData, targetType);
                ApplyHoTEffect(cardData, targetType);
                ApplyEnergyGainEffect(cardData, targetType);
                break;
                
            case CardDropZone.TargetType.PlayerSelf:
                ApplyDamageEffect(cardData, targetType, currentTurn, calculator, previousPlaysThisCombat, totalCopies, isBelowHealthThreshold);
                ApplyBlockEffect(cardData, targetType, currentTurn, calculator, previousPlaysThisCombat, totalCopies, isBelowHealthThreshold);
                ApplyHealingEffect(cardData, targetType);
                ApplyStatusEffect(cardData, targetType);
                ApplyDoTEffect(cardData, targetType);
                ApplyHoTEffect(cardData, targetType);
                ApplyEnergyGainEffect(cardData, targetType);
                break;
                
            case CardDropZone.TargetType.OwnPet:
                ApplyDamageEffect(cardData, targetType, currentTurn, calculator, previousPlaysThisCombat, totalCopies, isBelowHealthThreshold);
                ApplyBlockEffect(cardData, targetType, currentTurn, calculator, previousPlaysThisCombat, totalCopies, isBelowHealthThreshold);
                ApplyHealingEffect(cardData, targetType);
                ApplyStatusEffect(cardData, targetType);
                ApplyDoTEffect(cardData, targetType);
                ApplyHoTEffect(cardData, targetType);
                ApplyEnergyGainEffect(cardData, targetType);
                break;
        }
    }
    
    private void ApplyDamageEffect(CardData cardData, CardDropZone.TargetType targetType, int currentTurn, 
        CombatCalculator calculator, int previousPlaysThisCombat, int totalCopies, bool isBelowHealthThreshold)
    {
        if (cardData.damage <= 0 && cardData.damageScalingPerTurn <= 0 && 
            cardData.damageScalingPerPlay <= 0 && cardData.damageScalingPerCopy <= 0) return;
            
        PlayerManager playerManager = gameManager.GetPlayerManager();
        int playerStrength = playerManager.GetPlayerStrength();
        int totalDamage = calculator.CalculateCardDamageWithScaling(
            cardData, 
            currentTurn, 
            previousPlaysThisCombat, 
            totalCopies, 
            isBelowHealthThreshold, 
            targetType == CardDropZone.TargetType.OwnPet ? playerStrength : 0 // Include strength only for own pet
        );
        
        if (totalDamage <= 0) return;
        
        switch (targetType)
        {
            case CardDropZone.TargetType.EnemyPet:
                playerManager.DamageOpponentPet(totalDamage, HealthManager.DamageSource.PlayerSelfAttack);
                Debug.Log($"Dealt {totalDamage} damage to Opponent Pet. Base: {cardData.damage}, Strength: {playerStrength}");
                break;
                
            case CardDropZone.TargetType.PlayerSelf:
                playerManager.DamageLocalPlayer(totalDamage, true, HealthManager.DamageSource.PlayerSelfAttack);
                Debug.Log($"Applied {totalDamage} damage to PlayerSelf. Base: {cardData.damage}, Strength: {playerStrength}");
                break;
                
            case CardDropZone.TargetType.OwnPet:
                playerManager.ApplyDamageToLocalPetLocally(totalDamage);
                Debug.Log($"Applied {totalDamage} damage to OwnPet. Base: {cardData.damage}, Strength: {playerStrength}");
                break;
        }
    }
    
    private void ApplyBlockEffect(CardData cardData, CardDropZone.TargetType targetType, int currentTurn, 
        CombatCalculator calculator, int previousPlaysThisCombat, int totalCopies, bool isBelowHealthThreshold)
    {
        if (cardData.block <= 0 && cardData.blockScalingPerTurn <= 0 && 
            cardData.blockScalingPerPlay <= 0 && cardData.blockScalingPerCopy <= 0) return;
            
        PlayerManager playerManager = gameManager.GetPlayerManager();
        int totalBlock = calculator.CalculateCardBlockWithScaling(
            cardData, 
            currentTurn, 
            previousPlaysThisCombat, 
            totalCopies, 
            isBelowHealthThreshold
        );
        
        if (totalBlock <= 0) return;
        
        switch (targetType)
        {
            case CardDropZone.TargetType.EnemyPet:
                playerManager.AddBlockToOpponentPet(totalBlock);
                Debug.Log($"Applied {totalBlock} block to Enemy Pet. Base: {cardData.block}");
                break;
                
            case CardDropZone.TargetType.PlayerSelf:
                playerManager.AddBlockToLocalPlayer(totalBlock);
                Debug.Log($"Applied {totalBlock} block to PlayerSelf. Base: {cardData.block}");
                break;
                
            case CardDropZone.TargetType.OwnPet:
                playerManager.AddBlockToLocalPet(totalBlock);
                Debug.Log($"Applied {totalBlock} block to OwnPet. Base: {cardData.block}");
                break;
        }
    }
    
    private void ApplyHealingEffect(CardData cardData, CardDropZone.TargetType targetType)
    {
        if (cardData.healingAmount <= 0) return;
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        
        switch (targetType)
        {
            case CardDropZone.TargetType.EnemyPet:
                playerManager.HealOpponentPet(cardData.healingAmount);
                break;
                
            case CardDropZone.TargetType.PlayerSelf:
                playerManager.HealLocalPlayer(cardData.healingAmount);
                break;
                
            case CardDropZone.TargetType.OwnPet:
                playerManager.HealLocalPet(cardData.healingAmount);
                int finalPetHealth = playerManager.GetLocalPetHealth(); // Get health AFTER healing
                Debug.Log($"Healed OwnPet by {cardData.healingAmount}. New health: {finalPetHealth}");
                break;
        }
    }
    
    private void ApplyStatusEffect(CardData cardData, CardDropZone.TargetType targetType)
    {
        if (cardData.statusToApply == StatusEffectType.None) return;
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        int valueToApply = 0;
        switch (cardData.statusToApply) {
            case StatusEffectType.Thorns: valueToApply = cardData.thornsAmount; break;
            case StatusEffectType.Strength: valueToApply = cardData.strengthAmount; break;
            default: valueToApply = cardData.statusDuration; break; // Weak, Break use duration
        }

        if (valueToApply <= 0) {
            Debug.LogWarning($"Card {cardData.cardName} has {cardData.statusToApply} status but its corresponding amount (thornsAmount/strengthAmount/statusDuration) is 0.");
            return;
        }
        
        switch (targetType)
        {
            case CardDropZone.TargetType.EnemyPet:
                playerManager.ApplyStatusEffectOpponentPet(cardData.statusToApply, valueToApply);
                break;
                
            case CardDropZone.TargetType.PlayerSelf:
                playerManager.ApplyStatusEffectLocalPlayer(cardData.statusToApply, valueToApply);
                break;
                
            case CardDropZone.TargetType.OwnPet:
                playerManager.ApplyStatusEffectLocalPet(cardData.statusToApply, valueToApply);
                break;
        }
    }
    
    private void ApplyDoTEffect(CardData cardData, CardDropZone.TargetType targetType)
    {
        if (cardData.dotDamageAmount <= 0 || cardData.dotDuration <= 0) return;
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        
        switch (targetType)
        {
            case CardDropZone.TargetType.EnemyPet:
                playerManager.ApplyDotOpponentPet(cardData.dotDamageAmount, cardData.dotDuration);
                break;
                
            case CardDropZone.TargetType.PlayerSelf:
                playerManager.ApplyDotLocalPlayer(cardData.dotDamageAmount, cardData.dotDuration);
                break;
                
            case CardDropZone.TargetType.OwnPet:
                playerManager.ApplyDotLocalPet(cardData.dotDamageAmount, cardData.dotDuration);
                break;
        }
    }
    
    private void ApplyHoTEffect(CardData cardData, CardDropZone.TargetType targetType)
    {
        if (cardData.hotAmount <= 0 || cardData.hotDuration <= 0) return;
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        
        switch (targetType)
        {
            case CardDropZone.TargetType.EnemyPet:
                playerManager.ApplyHotOpponentPet(cardData.hotAmount, cardData.hotDuration);
                break;
                
            case CardDropZone.TargetType.PlayerSelf:
                playerManager.ApplyHotLocalPlayer(cardData.hotAmount, cardData.hotDuration);
                break;
                
            case CardDropZone.TargetType.OwnPet:
                playerManager.ApplyHotLocalPet(cardData.hotAmount, cardData.hotDuration);
                break;
        }
    }
    
    private void ApplyEnergyGainEffect(CardData cardData, CardDropZone.TargetType targetType)
    {
        if (cardData.energyGain <= 0) return;
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        
        switch (targetType)
        {
            case CardDropZone.TargetType.EnemyPet:
                // Update local simulation and notify owner
                gameManager.GetCardManager().GetPetDeckManager().AddEnergyToOpponentPet(cardData.energyGain);
                Debug.Log($"Applied {cardData.energyGain} energy to Opponent Pet (local sim).");
                break;
                
            case CardDropZone.TargetType.PlayerSelf:
                playerManager.GainEnergy(cardData.energyGain);
                break;
                
            case CardDropZone.TargetType.OwnPet:
                Debug.Log($"Applying {cardData.energyGain} energy to Own Pet.");
                // Update the custom property directly, triggering UI update via OnPlayerPropertiesUpdate
                int currentPetEnergy = gameManager.GetStartingPetEnergy(); // Start with base
                if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP, out object energyObj))
                {
                    try { currentPetEnergy = (int)energyObj; } catch {}
                }
                int newPetEnergy = currentPetEnergy + cardData.energyGain;
                ExitGames.Client.Photon.Hashtable petEnergyProp = new ExitGames.Client.Photon.Hashtable
                {
                    { CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP, newPetEnergy }
                };
                PhotonNetwork.LocalPlayer.SetCustomProperties(petEnergyProp);
                break;
        }
    }
    
    private void ApplyCardDrawEffect(CardData cardData, CardDropZone.TargetType targetType)
    {
        if (cardData.drawAmount <= 0) return;
        
        switch (targetType)
        {
            case CardDropZone.TargetType.PlayerSelf:
                // Draw cards for the player
                Debug.Log($"Drawing {cardData.drawAmount} cards for player.");
                DeckManager deckManager = gameManager.GetCardManager().GetDeckManager();
                for(int d=0; d < cardData.drawAmount; d++)
                {
                    deckManager.DrawCard();
                }
                gameManager.UpdateHandUI();
                gameManager.UpdateDeckCountUI();
                break;
                
            case CardDropZone.TargetType.OwnPet:
                // Draw cards for own pet
                Debug.Log($"Drawing {cardData.drawAmount} cards for own pet.");
                
                // Send RPC to the opponent who's simulating our own pet
                Player opponentPlayer = gameManager.GetPlayerManager().GetOpponentPlayer();
                if (opponentPlayer != null)
                {
                    Debug.Log($"Sending RpcDrawCardsForMyPet({cardData.drawAmount}) to opponent {opponentPlayer.NickName}.");
                    gameManager.GetPhotonView().RPC("RpcDrawCardsForMyPet", opponentPlayer, cardData.drawAmount);
                }
                else
                {
                    Debug.LogWarning("Could not send RpcDrawCardsForMyPet, opponent player is null.");
                }
                break;
                
            case CardDropZone.TargetType.EnemyPet:
                // Draw cards for enemy pet
                Debug.Log($"Drawing {cardData.drawAmount} cards for opponent's pet.");
                PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
                for(int d=0; d < cardData.drawAmount; d++)
                {
                    petDeckManager.DrawOpponentPetCard();
                }
                // Update opponent pet hand UI
                gameManager.GetCombatUIManager()?.UpdateOpponentPetHandUI();
                break;
        }
    }
    
    private void ApplyCardDiscardEffect(CardData cardData, CardDropZone.TargetType targetType)
    {
        if (cardData.discardRandomAmount <= 0) return;
        
        switch (targetType)
        {
            case CardDropZone.TargetType.PlayerSelf:
                // Discard random cards from player's hand
                Debug.Log($"Discarding {cardData.discardRandomAmount} random cards from player's hand.");
                List<CardData> discarded = gameManager.GetCardManager().GetDeckManager().DiscardRandomCards(cardData.discardRandomAmount, targetType);
                // Animation is triggered within DiscardRandomCards, but we need to update UI after.
                if (discarded.Count > 0)
                {
                    gameManager.UpdateHandUI(); // Update hand if any cards were actually discarded
                    gameManager.UpdateDeckCountUI();
                }
                break;
                
            case CardDropZone.TargetType.OwnPet:
                // Discard random cards from own pet's hand
                Debug.Log($"Discarding {cardData.discardRandomAmount} random cards from own pet's hand.");
                
                // Send RPC to the opponent who's simulating our own pet
                Player opponentPlayer = gameManager.GetPlayerManager().GetOpponentPlayer();
                if (opponentPlayer != null)
                {
                    Debug.Log($"Sending RpcDiscardRandomCardsFromMyPet({cardData.discardRandomAmount}) to opponent {opponentPlayer.NickName}.");
                    gameManager.GetPhotonView().RPC("RpcDiscardRandomCardsFromMyPet", opponentPlayer, cardData.discardRandomAmount);
                }
                else
                {
                    Debug.LogWarning("Could not send RpcDiscardRandomCardsFromMyPet, opponent player is null.");
                }
                break;
                
            case CardDropZone.TargetType.EnemyPet:
                // Discard random cards from enemy pet's hand
                Debug.Log($"Discarding {cardData.discardRandomAmount} random cards from opponent pet's hand.");
                PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
                petDeckManager.DiscardRandomOpponentPetCards(cardData.discardRandomAmount);
                // Update opponent pet hand UI
                gameManager.GetCombatUIManager()?.UpdateOpponentPetHandUI();
                break;
        }
    }
    
    // MODIFIED: Use costModificationTargetRole
    private void ApplyCostModifierEffect(CardData cardData)
    {
        if (cardData.costChangeAmount == 0) return; // Check only amount, role determines target
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        
        if (cardData.costModificationTargetRole == CostModificationTargetRole.Target) // Target means opponent's hand
        {
            playerManager.ApplyCostModifierToOpponentHand(cardData.costChangeAmount, cardData.costChangeDuration, cardData.costChangeCardCount);
            Debug.Log($"Cost Mod Effect: Applying {cardData.costChangeAmount} cost change to Opponent Hand for {cardData.costChangeDuration} turns (Count: {cardData.costChangeCardCount}).");
        }
        else // Self means player's own hand
        {
            playerManager.ApplyCostModifierToLocalHand(cardData.costChangeAmount, cardData.costChangeDuration, cardData.costChangeCardCount);
            Debug.Log($"Cost Mod Effect: Applying {cardData.costChangeAmount} cost change to Local Hand for {cardData.costChangeDuration} turns (Count: {cardData.costChangeCardCount}).");
        }
    }
    
    private void ApplyCritChanceBuffEffect(CardData cardData, CardDropZone.TargetType targetType)
    {
        if (cardData.critChanceBuffAmount <= 0) return;
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        CritBuffTargetRule rule = cardData.critBuffRule;
        Debug.Log($"Applying Crit Buff: Amount={cardData.critChanceBuffAmount}, Duration={cardData.critChanceBuffDuration}, Rule={rule}, DropTarget={targetType}"); 
        
        if (rule == CritBuffTargetRule.Self)
        {
            // Apply to the player who played the card
            playerManager.ApplyCritChanceBuffPlayer(cardData.critChanceBuffAmount, cardData.critChanceBuffDuration);
        }
        else if (rule == CritBuffTargetRule.Target)
        {
            // Apply based on where the card was dropped (targetType)
            switch (targetType)
            {
                case CardDropZone.TargetType.PlayerSelf:
                    playerManager.ApplyCritChanceBuffPlayer(cardData.critChanceBuffAmount, cardData.critChanceBuffDuration);
                    break;
                    
                case CardDropZone.TargetType.OwnPet:
                    playerManager.ApplyCritChanceBuffPet(cardData.critChanceBuffAmount, cardData.critChanceBuffDuration);
                    break;
                    
                case CardDropZone.TargetType.EnemyPet:
                    // Note: HealthManager handles sending RPC if needed
                    playerManager.ApplyCritChanceBuffOpponentPet(cardData.critChanceBuffAmount, cardData.critChanceBuffDuration);
                    break;
                    
                default:
                    // Log a warning if the drop target isn't one of the direct entities
                    Debug.LogWarning($"CritBuffRule is Target, but drop target was {targetType}. Crit buff not applied.");
                    break;
            }
        }
    }
    
    public void ProcessOpponentPetCardEffect(CardData cardToPlay, int previousPlays = 0, int totalCopies = 0)
    {
        if (cardToPlay == null) {
            Debug.LogError("ProcessOpponentPetCardEffect: cardToPlay is null!");
            return;
        }
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        CombatCalculator calculator = gameManager.GetCombatCalculator();
        // Get the current turn from the proper source
        int currentTurn = CombatTurnManagerInstance?.GetCurrentCombatTurn() ?? 1;
        
        // Determine target based on card primary target field
        OpponentPetTargetType target = cardToPlay.primaryTargetWhenPlayedByPet;
        
        // Log scaling info
        Debug.Log($"Processing opponent pet card '{cardToPlay.cardName}' with previous plays: {previousPlays}, total copies: {totalCopies}");
        
        // Apply all effects in a structured way
        ApplyOpponentPetDamageEffect(cardToPlay, target, currentTurn, calculator, previousPlays, totalCopies);
        ApplyOpponentPetBlockEffect(cardToPlay, target, currentTurn, calculator, previousPlays, totalCopies);
        ApplyOpponentPetHealingEffect(cardToPlay, target);
        ApplyOpponentPetStatusEffect(cardToPlay, target);
        ApplyOpponentPetDoTEffect(cardToPlay, target);
        ApplyOpponentPetHoTEffect(cardToPlay, target);
        ApplyOpponentPetEnergyGainEffect(cardToPlay);
        ApplyOpponentPetDrawEffect(cardToPlay);
        ApplyOpponentPetDiscardEffect(cardToPlay);
        ApplyOpponentPetCritBuffEffect(cardToPlay, target);
        
        // Update UI after all effects have been applied
        gameManager.UpdateHealthUI();
        // Update opponent pet hand UI
        gameManager.GetCombatUIManager()?.UpdateOpponentPetHandUI();
    }
    
    private void ApplyOpponentPetDamageEffect(CardData cardToPlay, OpponentPetTargetType target, int currentTurn, CombatCalculator calculator, int previousPlays, int totalCopies)
    {
        if (cardToPlay.damage <= 0) return;
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        StatusEffectManager statusManager = playerManager.GetStatusEffectManager();
        int strengthBonus = playerManager.GetOpponentPetStrength();
        
        // Calculate damage without block interaction
        int actualDamage = calculator.CalculateCardDamageWithScaling(
            cardToPlay,
            currentTurn,
            previousPlays,
            totalCopies,
            false, // Low health threshold not used for opponent pet
            0 // Don't include strength here - it will be applied in damage calculations
        );
        
        if (actualDamage <= 0) return;
        
        switch (target)
        {
            case OpponentPetTargetType.Player:
                playerManager.DamageLocalPlayer(actualDamage);
                Debug.Log($"Opponent Pet dealt {actualDamage} damage to Player. (Base: {cardToPlay.damage}, StrB: {strengthBonus}). Player Health: {playerManager.GetLocalPlayerHealth()}, Block: {playerManager.GetLocalPlayerBlock()}");
                break;
                
            case OpponentPetTargetType.Self:
                playerManager.DamageOpponentPet(actualDamage);
                Debug.Log($"Opponent Pet dealt {actualDamage} damage to ITSELF. (Base: {cardToPlay.damage}, StrB: {strengthBonus}). Pet Health: {playerManager.GetOpponentPetHealth()}, Block: {playerManager.GetOpponentPetBlock()}");
                break;
        }
    }
    
    private void ApplyOpponentPetBlockEffect(CardData cardToPlay, OpponentPetTargetType target, int currentTurn, CombatCalculator calculator, int previousPlays, int totalCopies)
    {
        if (cardToPlay.block <= 0) return;
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        int totalBlock = calculator.CalculateCardBlockWithScaling(
            cardToPlay,
            currentTurn,
            previousPlays,
            totalCopies,
            false // Low health threshold not used for opponent pet
        );
        
        if (totalBlock <= 0) return;
        
        switch (target)
        {
            case OpponentPetTargetType.Self:
                playerManager.AddBlockToOpponentPet(totalBlock);
                Debug.Log($"Opponent Pet gained {totalBlock} block. Est. Block: {playerManager.GetOpponentPetBlock()}");
                break;
                
            case OpponentPetTargetType.Player:
                playerManager.AddBlockToLocalPlayer(totalBlock);
                Debug.LogWarning($"Opponent Pet card {cardToPlay.cardName} applied block to Player? Block: {totalBlock}");
                break;
        }
    }
    
    private void ApplyOpponentPetHealingEffect(CardData cardToPlay, OpponentPetTargetType target)
    {
        if (cardToPlay.healingAmount <= 0) return;
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        
        switch (target)
        {
            case OpponentPetTargetType.Self:
                playerManager.HealOpponentPet(cardToPlay.healingAmount);
                Debug.Log($"Opponent Pet healed itself for {cardToPlay.healingAmount}. New health: {playerManager.GetOpponentPetHealth()}");
                break;
                
            case OpponentPetTargetType.Player:
                // Unusual case but handle it for completeness
                playerManager.HealLocalPlayer(cardToPlay.healingAmount);
                Debug.LogWarning($"Opponent Pet card {cardToPlay.cardName} healed Player for {cardToPlay.healingAmount}. New health: {playerManager.GetLocalPlayerHealth()}");
                break;
        }
    }
    
    private void ApplyOpponentPetStatusEffect(CardData cardToPlay, OpponentPetTargetType target)
    {
        if (cardToPlay.statusToApply == StatusEffectType.None) return;
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        int valueToApply = 0;
        
        switch (cardToPlay.statusToApply) {
            case StatusEffectType.Thorns: valueToApply = cardToPlay.thornsAmount; break;
            case StatusEffectType.Strength: valueToApply = cardToPlay.strengthAmount; break;
            default: valueToApply = cardToPlay.statusDuration; break; // Weak, Break use duration
        }
        
        if (valueToApply <= 0) {
            Debug.LogWarning($"Card {cardToPlay.cardName} has {cardToPlay.statusToApply} status but its corresponding amount (thornsAmount/strengthAmount/statusDuration) is 0.");
            return;
        }
        
        switch (target)
        {
            case OpponentPetTargetType.Self:
                playerManager.ApplyStatusEffectOpponentPet(cardToPlay.statusToApply, valueToApply);
                Debug.Log($"Opponent Pet applied {cardToPlay.statusToApply} to itself for {valueToApply}.");
                break;
                
            case OpponentPetTargetType.Player:
                playerManager.ApplyStatusEffectLocalPlayer(cardToPlay.statusToApply, valueToApply);
                Debug.Log($"Opponent Pet applied {cardToPlay.statusToApply} to Player for {valueToApply}.");
                break;
        }
    }
    
    private void ApplyOpponentPetDoTEffect(CardData cardToPlay, OpponentPetTargetType target)
    {
        if (cardToPlay.dotDamageAmount <= 0 || cardToPlay.dotDuration <= 0) return;
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        
        switch (target)
        {
            case OpponentPetTargetType.Player:
                playerManager.ApplyDotLocalPlayer(cardToPlay.dotDamageAmount, cardToPlay.dotDuration);
                Debug.Log($"Opponent Pet applied DoT to Player: {cardToPlay.dotDamageAmount} damage for {cardToPlay.dotDuration} turns.");
                break;
                
            case OpponentPetTargetType.Self:
                playerManager.ApplyDotOpponentPet(cardToPlay.dotDamageAmount, cardToPlay.dotDuration);
                Debug.Log($"Opponent Pet applied DoT to itself: {cardToPlay.dotDamageAmount} damage for {cardToPlay.dotDuration} turns.");
                break;
        }
    }
    
    private void ApplyOpponentPetHoTEffect(CardData cardToPlay, OpponentPetTargetType target)
    {
        if (cardToPlay.hotAmount <= 0 || cardToPlay.hotDuration <= 0) return;
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        
        switch (target)
        {
            case OpponentPetTargetType.Self:
                playerManager.ApplyHotOpponentPet(cardToPlay.hotAmount, cardToPlay.hotDuration);
                Debug.Log($"Opponent Pet applied HoT to itself: {cardToPlay.hotAmount} healing for {cardToPlay.hotDuration} turns.");
                break;
                
            case OpponentPetTargetType.Player:
                playerManager.ApplyHotLocalPlayer(cardToPlay.hotAmount, cardToPlay.hotDuration);
                Debug.Log($"Opponent Pet applied HoT to Player: {cardToPlay.hotAmount} healing for {cardToPlay.hotDuration} turns.");
                break;
        }
    }
    
    private void ApplyOpponentPetEnergyGainEffect(CardData cardToPlay)
    {
        if (cardToPlay.energyGain <= 0) return;
        
        // Update local simulation and notify owner
        gameManager.GetCardManager().GetPetDeckManager().AddEnergyToOpponentPet(cardToPlay.energyGain);
        Debug.Log($"Applied {cardToPlay.energyGain} energy to Opponent Pet (local sim).");
    }
    
    private void ApplyOpponentPetDrawEffect(CardData cardToPlay)
    {
        if (cardToPlay.drawAmount <= 0) return;
        
        Debug.Log($"Opponent Pet draws {cardToPlay.drawAmount} cards (simulated).");
        PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
        for(int d=0; d < cardToPlay.drawAmount; d++)
        {
            petDeckManager.DrawOpponentPetCard();
        }
    }
    
    private void ApplyOpponentPetDiscardEffect(CardData cardToPlay)
    {
        if (cardToPlay.discardRandomAmount <= 0) return;
        
        Debug.Log($"Opponent Pet discards {cardToPlay.discardRandomAmount} random cards (simulated).");
        gameManager.GetCardManager().GetPetDeckManager().DiscardRandomOpponentPetCards(cardToPlay.discardRandomAmount);
    }
    
    private void ApplyOpponentPetCritBuffEffect(CardData cardToPlay, OpponentPetTargetType target)
    {
        if (cardToPlay.critChanceBuffAmount <= 0) return;
        
        PlayerManager playerManager = gameManager.GetPlayerManager();
        CritBuffTargetRule rule = cardToPlay.critBuffRule;
        Debug.Log($"Opponent Pet applying Crit Buff: Amount={cardToPlay.critChanceBuffAmount}, Duration={cardToPlay.critChanceBuffDuration}, Rule={rule}");
        
        if (rule == CritBuffTargetRule.Self)
        {
            // Apply buff to the opponent pet itself
            playerManager.ApplyCritChanceBuffOpponentPet(cardToPlay.critChanceBuffAmount, cardToPlay.critChanceBuffDuration);
        }
        else if (rule == CritBuffTargetRule.Target)
        {
            // Apply based on the pet's primary target for this card
            switch (target)
            {
                case OpponentPetTargetType.Player:
                    playerManager.ApplyCritChanceBuffPlayer(cardToPlay.critChanceBuffAmount, cardToPlay.critChanceBuffDuration);
                    Debug.Log($"Opponent Pet applied Crit Buff to Player: +{cardToPlay.critChanceBuffAmount}% for {cardToPlay.critChanceBuffDuration} turns.");
                    break;
                    
                case OpponentPetTargetType.Self:
                    playerManager.ApplyCritChanceBuffOpponentPet(cardToPlay.critChanceBuffAmount, cardToPlay.critChanceBuffDuration);
                    break;
            }
        }
    }
    
    // REVERTED: Back to original logic using target-specific types and default targetType param
    public void HandleDiscardTrigger(CardData card, CardDropZone.TargetType targetType = CardDropZone.TargetType.PlayerSelf) // Added default back
    {
        // Only process if card has a discard effect
        if (card.discardEffectType == DiscardEffectType.None) return;

        PlayerManager playerManager = gameManager.GetPlayerManager();
        int value = card.discardEffectValue;
        if (value <= 0) return; // Check value after playerManager is obtained
        
        // Use the original switch logic based on the specific DiscardEffectType
        switch (card.discardEffectType)
        {
            case DiscardEffectType.DealDamageToOpponentPet:
                // This effect always targets the opponent pet regardless of drop target
                // Use the specific DiscardEffect source
                playerManager.DamageOpponentPet(value, HealthManager.DamageSource.DiscardEffect); // Use correct source
                Debug.Log($"Discard Effect: Dealt {value} damage to Opponent Pet.");
                break;
            
            case DiscardEffectType.GainBlockPlayer:
                // Effect targets Player (or Pet if targetType is OwnPet)
                if (targetType == CardDropZone.TargetType.PlayerSelf)
                {
                    playerManager.AddBlockToLocalPlayer(value);
                    Debug.Log($"Discard Effect: Gained {value} block for player.");
                }
                else if (targetType == CardDropZone.TargetType.OwnPet)
                {
                    playerManager.AddBlockToLocalPet(value);
                    Debug.Log($"Discard Effect: Gained {value} block for own pet.");
                }
                // Removed EnemyPet case as it wasn't implemented before
                break;
            
            case DiscardEffectType.GainBlockPet:
                // This effect always targets the player's own pet
                playerManager.AddBlockToLocalPet(value);
                 Debug.Log($"Discard Effect: Gained {value} block for own pet.");
                break;
            
            case DiscardEffectType.DrawCard:
                // Apply draw based on target (who discarded)
                if (targetType == CardDropZone.TargetType.PlayerSelf)
                {
                    Debug.Log($"Discard Effect: Drawing {value} cards for player.");
                    DeckManager deckManager = gameManager.GetCardManager().GetDeckManager();
                    for(int i=0; i < value; i++) deckManager.DrawCard();
                    gameManager.UpdateHandUI();
                    gameManager.UpdateDeckCountUI();
                }
                else if (targetType == CardDropZone.TargetType.OwnPet)
                {
                    // Needs implementation: Tell opponent (who simulates our pet) to draw cards
                    Player opponentPlayer = playerManager.GetOpponentPlayer();
                    if (opponentPlayer != null) {
                         Debug.Log($"Discard Effect: Requesting OwnPet draw {value} cards (sending RPC to {opponentPlayer.NickName}).");
                        gameManager.GetPhotonView().RPC("RpcDrawCardsForMyPet", opponentPlayer, value);
                    } else {
                        Debug.LogWarning("Discard Effect: Could not send RpcDrawCardsForMyPet, opponent player is null.");
                    }
                }
                else if (targetType == CardDropZone.TargetType.EnemyPet)
                {
                    Debug.Log($"Discard Effect: Drawing {value} cards for opponent's pet.");
                    PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
                    for(int i=0; i < value; i++) petDeckManager.DrawOpponentPetCard();
                    gameManager.GetCombatUIManager()?.UpdateOpponentPetHandUI();
                }
                break;
            
            case DiscardEffectType.GainEnergy:
                // Apply energy gain based on target (who discarded)
                if (targetType == CardDropZone.TargetType.PlayerSelf)
                {
                    playerManager.GainEnergy(value);
                    Debug.Log($"Discard Effect: Gained {value} energy for player.");
                }
                else if (targetType == CardDropZone.TargetType.OwnPet)
                {
                     // Update Own Pet's energy custom property
                    int currentPetEnergy = gameManager.GetStartingPetEnergy();
                    if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP, out object energyObj)) {
                        try { currentPetEnergy = (int)energyObj; } catch {}
                    }
                    int newPetEnergy = currentPetEnergy + value;
                    ExitGames.Client.Photon.Hashtable petEnergyProp = new ExitGames.Client.Photon.Hashtable { { CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP, newPetEnergy } };
                    PhotonNetwork.LocalPlayer.SetCustomProperties(petEnergyProp);
                    Debug.Log($"Discard Effect: Gained {value} energy for Own Pet (updated property).");
                }
                else if (targetType == CardDropZone.TargetType.EnemyPet)
                {
                    PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
                    petDeckManager.AddEnergyToOpponentPet(value);
                    Debug.Log($"Discard Effect: Added {value} energy to opponent's pet.");
                }
                break;
        }
    }
    
    private void ApplyComboEffect(CardData triggeringCard, CardDropZone.TargetType originalTargetType)
    {
        PlayerManager playerManager = gameManager.GetPlayerManager();
        int value = triggeringCard.comboEffectValue;
        if (value <= 0) return; // No effect if value is zero or less

        // Determine the effective target based on ComboTargetRole
        CardDropZone.TargetType effectiveTarget;
        if (triggeringCard.comboTargetRole == ComboTargetRole.Self)
        {
            effectiveTarget = CardDropZone.TargetType.PlayerSelf; // Combo always targets the player when role is Self
        }
        else // ComboTargetRole.Target
        {
            effectiveTarget = originalTargetType; // Combo targets whatever the card was played on
        }

        Debug.Log($"Combo targeting role: {triggeringCard.comboTargetRole}, Original target: {originalTargetType}, Effective target: {effectiveTarget}");

        switch (triggeringCard.comboEffectType)
        {
            case ComboEffectType.DealDamage:
                // Apply damage based on effective target
                if (effectiveTarget == CardDropZone.TargetType.EnemyPet) {
                    // Use PlayerComboEffect source
                    playerManager.DamageOpponentPet(value, HealthManager.DamageSource.PlayerComboEffect); 
                    Debug.Log($"Combo Effect: Dealt {value} damage to Opponent Pet.");
                }
                else if (effectiveTarget == CardDropZone.TargetType.PlayerSelf)
                {
                    // Use PlayerComboEffect source
                    playerManager.DamageLocalPlayer(value, true, HealthManager.DamageSource.PlayerComboEffect);
                    Debug.Log($"Combo Effect: Dealt {value} damage to PlayerSelf.");
                }
                else if (effectiveTarget == CardDropZone.TargetType.OwnPet)
                {
                    // ApplyDamageToLocalPetLocally doesn't take a source, but maybe it should?
                    // For now, just call it. Consider adding source if needed for thorns etc.
                    playerManager.ApplyDamageToLocalPetLocally(value); 
                    Debug.Log($"Combo Effect: Dealt {value} damage to OwnPet.");
                }
                else {
                     Debug.LogWarning($"Combo Effect: DealDamage targeted {effectiveTarget}, which is not implemented or invalid for this effect.");
                }
                break;

            case ComboEffectType.GainBlock:
                // Apply block based on effective target
                if (effectiveTarget == CardDropZone.TargetType.PlayerSelf) {
                    playerManager.AddBlockToLocalPlayer(value);
                     Debug.Log($"Combo Effect: Gained {value} block for PlayerSelf.");
                }
                else if (effectiveTarget == CardDropZone.TargetType.OwnPet) {
                    playerManager.AddBlockToLocalPet(value);
                    Debug.Log($"Combo Effect: Gained {value} block for OwnPet.");
                }
                else if (effectiveTarget == CardDropZone.TargetType.EnemyPet) {
                     playerManager.AddBlockToOpponentPet(value);
                     Debug.Log($"Combo Effect: Gained {value} block for EnemyPet.");
                }
                else {
                     Debug.LogWarning($"Combo Effect: GainBlock targeted {effectiveTarget}, which is not implemented or invalid for this effect.");
                }
                break;

            case ComboEffectType.DrawCard:
                // DrawCard implicitly targets the player, regardless of comboTargetRole or originalTargetType
                DeckManager deckManager = gameManager.GetCardManager().GetDeckManager();
                for(int i=0; i < value; i++) deckManager.DrawCard();
                gameManager.UpdateHandUI();
                gameManager.UpdateDeckCountUI();
                Debug.Log($"Combo Effect: Drew {value} cards.");
                break;

            case ComboEffectType.GainEnergy:
                // GainEnergy implicitly targets the player, regardless of comboTargetRole or originalTargetType
                playerManager.GainEnergy(value);
                 Debug.Log($"Combo Effect: Gained {value} energy.");
                break;

            case ComboEffectType.None:
            default:
                 Debug.LogWarning($"Combo Effect: Triggered with type {triggeringCard.comboEffectType}, but no action defined.");
                break;
        }
    }

    // --- MODIFIED: Helper method to visualize target effects --- 
    private void PlayTargetEffectPrefab(CardDropZone.TargetType targetType, GameObject effectPrefab)
    {
        if (effectPrefab == null || gameManager == null || effectsCanvasTransform == null || effectsCanvasCamera == null) 
        {
            Debug.LogError("Cannot play target effect: Missing dependencies (prefab, gameManager, effectsCanvas, or effectsCamera).");
            return; // Missing dependencies
        }

        // Find target transform based on type (UI element)
        Transform targetTransform = null;
        CombatUIManager uiManager = gameManager.GetCombatUIManager();
        if (uiManager == null) return; // Need UI Manager for positions

        switch (targetType)
        {
            case CardDropZone.TargetType.EnemyPet:
                // Find the opponent pet's UI element (e.g., health bar or representation)
                // This requires access to UI elements, ideally via CombatUIManager
                GameObject oppPetUI = uiManager.GetOpponentPetUIArea(); // Assuming such a getter exists or can be added
                if (oppPetUI != null) targetTransform = oppPetUI.transform;
                break;
            case CardDropZone.TargetType.PlayerSelf:
                GameObject playerUI = uiManager.GetPlayerUIArea(); // Assuming such a getter exists
                if (playerUI != null) targetTransform = playerUI.transform;
                break;
            case CardDropZone.TargetType.OwnPet:
                 GameObject ownPetUI = uiManager.GetOwnPetUIArea(); // Assuming such a getter exists
                if (ownPetUI != null) targetTransform = ownPetUI.transform;
                break;
        }

        if (targetTransform != null)
        {
            // Calculate screen position of the UI target
            // For Overlay canvas, the transform.position is often close enough to screen pos,
            // but using WorldToScreenPoint is safer if anchors/pivots are complex.
            // We need the *Combat UI* canvas's camera (or null for overlay) here.
            // --- MODIFIED: Get canvas from root instance ---
            GameObject uiRoot = uiManager?.GetCombatRootInstance();
            Canvas uiCanvas = uiRoot?.GetComponentInParent<Canvas>();
            // --- END MODIFIED ---
            Camera uiCamera = (uiCanvas?.renderMode == RenderMode.ScreenSpaceOverlay) ? null : uiCanvas?.worldCamera;
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, targetTransform.position);

            // Convert screen position to world position on the Effects Canvas plane
            Vector3 worldPos = effectsCanvasCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, effectsCanvasPlaneDistance));
            
            // Instantiate under the *Effects Canvas* and set world position
            gameManager.StartCoroutine(InstantiateAndDestroyEffect(effectPrefab, worldPos, effectsCanvasTransform)); 
        }
        else
        {
            Debug.LogWarning($"PlayTargetEffectPrefab: Could not find target transform for {targetType}");
        }
    }

    private IEnumerator InstantiateAndDestroyEffect(GameObject prefab, Vector3 worldPosition, Transform parentTransform)
    {
        // Create a temporary GameObject by instantiating the prefab under the correct parent (Effects Canvas)
        GameObject effectGO = Object.Instantiate(prefab, parentTransform); 
        effectGO.transform.position = worldPosition; // Set world position
        
        // --- ADDED: Attempt to get Animator and animation length --- 
        Animator animator = effectGO.GetComponent<Animator>();
        float duration = 1.0f; // Default duration if animator/clip not found
        
        if (animator != null && animator.runtimeAnimatorController != null && animator.runtimeAnimatorController.animationClips.Length > 0)
        {
            // Assuming the first animation clip is the one we want the length of
            duration = animator.runtimeAnimatorController.animationClips[0].length;
            Debug.Log($"Target Effect: Found animation clip, duration: {duration}s");
        }
        else
        {
             Debug.LogWarning("Target Effect: Animator or Animation Clip not found on prefab. Using default duration.");
        }
        // --- END ADDED ---

        // Wait for the determined duration
        yield return new WaitForSeconds(duration);

        // Cleanup - Destroy the instantiated effect object
        if (effectGO != null) // Check if it wasn't destroyed elsewhere
        {
            Object.Destroy(effectGO);
        }
        // yield return null; // Keep the coroutine running for one frame to ensure instantiation happens
    }
    // --- END MODIFIED ---
}