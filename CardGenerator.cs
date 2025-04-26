using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class CardGenerator : MonoBehaviour
{
    private const string CardSavePath = "Assets/Cards";
    private const string TestCardPrefix = "Test_";
    
    // Card sets for testing
    private static List<CardData> damageTestCards = new List<CardData>();
    private static List<CardData> blockTestCards = new List<CardData>();
    private static List<CardData> statusEffectTestCards = new List<CardData>();
    private static List<CardData> dotHotTestCards = new List<CardData>();
    private static List<CardData> comboTestCards = new List<CardData>();
    private static List<CardData> discardEffectTestCards = new List<CardData>();
    private static List<CardData> costModTestCards = new List<CardData>();
    private static List<CardData> critBuffTestCards = new List<CardData>();
    private static List<CardData> drawDiscardTestCards = new List<CardData>();
    private static List<CardData> upgradeTestCards = new List<CardData>();
    private static List<CardData> energyTestCards = new List<CardData>();
    private static List<CardData> scalingTestCards = new List<CardData>();
    private static List<CardData> petTargetingTestCards = new List<CardData>();
    private static List<CardData> healingTestCards = new List<CardData>();
    
    [MenuItem("Tools/Test Cards/Generate Test Card Assets")]
    public static void GenerateCardAssets()
    {
        // Clear all existing card data lists
        ClearCardLists();
        
        // Ensure the directory exists
        if (!Directory.Exists(CardSavePath))
        {
            Directory.CreateDirectory(CardSavePath);
        }

        // Delete existing test cards
        DeleteExistingTestCards();

        // Generate all new test cards
        GenerateDamageTestCards();
        GenerateBlockTestCards();
        GenerateStatusEffectTestCards();
        GenerateDotHotTestCards();
        GenerateComboTestCards();
        GenerateDiscardEffectCards();
        GenerateCostModifierCards();
        GenerateCritBuffCards();
        GenerateDrawDiscardCards();
        GenerateUpgradeCards();
        GenerateEnergyCards();
        GenerateScalingTestCards();
        GeneratePetTargetingCards();
        GenerateHealingCards();

        // Refresh the asset database to show the new cards
        AssetDatabase.Refresh();
        Debug.Log("Test card generation complete!");
    }
    
    private static void ClearCardLists()
    {
        damageTestCards.Clear();
        blockTestCards.Clear();
        statusEffectTestCards.Clear();
        dotHotTestCards.Clear();
        comboTestCards.Clear();
        discardEffectTestCards.Clear();
        costModTestCards.Clear();
        critBuffTestCards.Clear();
        drawDiscardTestCards.Clear();
        upgradeTestCards.Clear();
        energyTestCards.Clear();
        scalingTestCards.Clear();
        petTargetingTestCards.Clear();
        healingTestCards.Clear();
    }
    
    private static void DeleteExistingTestCards()
    {
        if (!Directory.Exists(CardSavePath)) return;
        
        string[] cardGuids = AssetDatabase.FindAssets("t:CardData", new[] { CardSavePath });
        foreach (string guid in cardGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains(TestCardPrefix))
            {
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"Deleted test card: {path}");
            }
        }
    }
    
    #region Card Generation Methods
    
    private static void GenerateDamageTestCards()
    {
        // Basic damage test cards
        CardData smallDamage = CreateTestCard(
            "Small_Damage", 
            "Deal 5 damage.", 
            1, 
            card => { card.damage = 5; }
        );
        damageTestCards.Add(smallDamage);
        
        CardData mediumDamage = CreateTestCard(
            "Medium_Damage", 
            "Deal 10 damage.", 
            2, 
            card => { card.damage = 10; }
        );
        damageTestCards.Add(mediumDamage);
        
        CardData largeDamage = CreateTestCard(
            "Large_Damage", 
            "Deal 20 damage.", 
            3, 
            card => { card.damage = 20; }
        );
        damageTestCards.Add(largeDamage);
        
        // Critical damage test
        CardData criticalDamage = CreateTestCard(
            "Critical_Hit", 
            "Deal 10 damage with 100% crit chance.", 
            2, 
            card => { 
            card.damage = 10;
                card.critChanceBuffAmount = 100;
                card.critChanceBuffDuration = 1;
                card.critBuffRule = CritBuffTargetRule.Self;
            }
        );
        damageTestCards.Add(criticalDamage);
        
        Debug.Log("Generated damage test cards");
    }
    
    private static void GenerateBlockTestCards()
    {
        // Basic block test cards
        CardData smallBlock = CreateTestCard(
            "Small_Block", 
            "Gain 5 block.", 
            1, 
            card => { card.block = 5; }
        );
        blockTestCards.Add(smallBlock);
        
        CardData mediumBlock = CreateTestCard(
            "Medium_Block", 
            "Gain 10 block.", 
            2, 
            card => { card.block = 10; }
        );
        blockTestCards.Add(mediumBlock);
        
        CardData largeBlock = CreateTestCard(
            "Large_Block", 
            "Gain 20 block.", 
            3, 
            card => { card.block = 20; }
        );
        blockTestCards.Add(largeBlock);
        
        // Damage + Block combination
        CardData damageAndBlock = CreateTestCard(
            "Damage_And_Block", 
            "Deal 5 damage and gain 5 block.", 
            2, 
            card => { 
                card.damage = 5;
                card.block = 5;
            }
        );
        blockTestCards.Add(damageAndBlock);
        
        Debug.Log("Generated block test cards");
    }
    
    private static void GenerateStatusEffectTestCards()
    {
        // Weak status
        CardData weak = CreateTestCard(
            "Apply_Weak", 
            "Apply 2 Weak to target.", 
            1, 
            card => { 
            card.statusToApply = StatusEffectType.Weak;
            card.statusDuration = 2;
            }
        );
        statusEffectTestCards.Add(weak);
        
        // Break status
        CardData breakCard = CreateTestCard(
            "Apply_Break", 
            "Apply 2 Break to target.", 
            1, 
            card => { 
            card.statusToApply = StatusEffectType.Break;
            card.statusDuration = 2;
            }
        );
        statusEffectTestCards.Add(breakCard);
        
        // Thorns status
        CardData thorns = CreateTestCard(
            "Apply_Thorns", 
            "Apply 3 Thorns to target.", 
            1, 
            card => { 
                card.statusToApply = StatusEffectType.Thorns;
                card.thornsAmount = 3;
            }
        );
        statusEffectTestCards.Add(thorns);
        
        // Strength status
        CardData strength = CreateTestCard(
            "Apply_Strength", 
            "Apply 2 Strength to target.", 
            1, 
            card => { 
                card.statusToApply = StatusEffectType.Strength;
                card.strengthAmount = 2;
            }
        );
        statusEffectTestCards.Add(strength);
        
        Debug.Log("Generated status effect test cards");
    }
    
    private static void GenerateDotHotTestCards()
    {
        // Damage over time
        CardData dot = CreateTestCard(
            "Apply_DoT", 
            "Apply 3 damage per turn for 3 turns.", 
            2, 
            card => { 
            card.dotDamageAmount = 3;
            card.dotDuration = 3;
            }
        );
        dotHotTestCards.Add(dot);
        
        // Heal over time
        CardData hot = CreateTestCard(
            "Apply_HoT", 
            "Apply 3 healing per turn for 3 turns.", 
            2, 
            card => { 
                card.hotAmount = 3;
                card.hotDuration = 3;
            }
        );
        dotHotTestCards.Add(hot);
        
        // Both DoT and HoT
        CardData dotAndHot = CreateTestCard(
            "DoT_And_HoT", 
            "Apply 2 damage and 2 healing per turn for 3 turns.", 
            3, 
            card => { 
                card.dotDamageAmount = 2;
                card.dotDuration = 3;
                card.hotAmount = 2;
                card.hotDuration = 3;
            }
        );
        dotHotTestCards.Add(dotAndHot);
        
        Debug.Log("Generated DoT/HoT test cards");
    }
    
    private static void GenerateComboTestCards()
    {
        // Combo damage starter
        CardData comboStarter = CreateTestCard(
            "Combo_Starter_Damage", 
            "Deal 3 damage. Combo: After 2 combo cards, deal 10 damage.", 
            1, 
            card => { 
                card.damage = 3;
            card.isComboStarter = true;
                card.comboTriggerValue = 2;
            card.comboEffectType = ComboEffectType.DealDamage;
            card.comboEffectValue = 10;
            card.comboTargetRole = ComboTargetRole.Target;
            }
        );
        comboTestCards.Add(comboStarter);
        
        // Combo block starter
        CardData comboBlockStarter = CreateTestCard(
            "Combo_Starter_Block", 
            "Gain 3 block. Combo: After 2 combo cards, gain 10 block.", 
            1, 
            card => { 
                card.block = 3;
            card.isComboStarter = true;
                card.comboTriggerValue = 2;
            card.comboEffectType = ComboEffectType.GainBlock;
            card.comboEffectValue = 10;
            card.comboTargetRole = ComboTargetRole.Self;
            }
        );
        comboTestCards.Add(comboBlockStarter);
        
        // Combo draw starter
        CardData comboDrawStarter = CreateTestCard(
            "Combo_Starter_Draw", 
            "Draw 1 card. Combo: After 2 combo cards, draw 3 cards.", 
            1, 
            card => { 
            card.drawAmount = 1;
            card.isComboStarter = true;
                card.comboTriggerValue = 2;
            card.comboEffectType = ComboEffectType.DrawCard;
            card.comboEffectValue = 3;
            }
        );
        comboTestCards.Add(comboDrawStarter);
        
        // Combo energy starter
        CardData comboEnergyStarter = CreateTestCard(
            "Combo_Starter_Energy", 
            "Gain 1 energy. Combo: After 2 combo cards, gain 3 energy.", 
            0, 
            card => { 
                card.energyGain = 1;
            card.isComboStarter = true;
                card.comboTriggerValue = 2;
                card.comboEffectType = ComboEffectType.GainEnergy;
                card.comboEffectValue = 3;
            }
        );
        comboTestCards.Add(comboEnergyStarter);
        
        Debug.Log("Generated combo test cards");
    }

    private static void GenerateDiscardEffectCards()
    {
        // Discard effect - Deal damage
        CardData discardDamage = CreateTestCard(
            "Discard_Damage", 
            "Deal 3 damage. When discarded, deal 5 damage.", 
            1, 
            card => { 
            card.damage = 3;
            card.discardEffectType = DiscardEffectType.DealDamageToOpponentPet;
            card.discardEffectValue = 5;
            }
        );
        discardEffectTestCards.Add(discardDamage);
        
        // Discard effect - Gain block for player
        CardData discardBlock = CreateTestCard(
            "Discard_Block_Player", 
            "Gain 3 block. When discarded, gain 5 block.", 
            1, 
            card => { 
                card.block = 3;
            card.discardEffectType = DiscardEffectType.GainBlockPlayer;
            card.discardEffectValue = 5;
            }
        );
        discardEffectTestCards.Add(discardBlock);
        
        // Discard effect - Gain block for pet
        CardData discardPetBlock = CreateTestCard(
            "Discard_Block_Pet", 
            "Gain 3 block. When discarded, give your pet 5 block.", 
            1, 
            card => { 
                card.block = 3;
                card.discardEffectType = DiscardEffectType.GainBlockPet;
                card.discardEffectValue = 5;
            }
        );
        discardEffectTestCards.Add(discardPetBlock);
        
        // Discard effect - Draw cards
        CardData discardDraw = CreateTestCard(
            "Discard_Draw", 
            "Draw 1 card. When discarded, draw 2 cards.", 
            1, 
            card => { 
            card.drawAmount = 1;
            card.discardEffectType = DiscardEffectType.DrawCard;
                card.discardEffectValue = 2;
            }
        );
        discardEffectTestCards.Add(discardDraw);
        
        // Discard effect - Gain energy
        CardData discardEnergy = CreateTestCard(
            "Discard_Energy", 
            "Gain 1 energy. When discarded, gain 2 energy.", 
            0, 
            card => { 
                card.energyGain = 1;
                card.discardEffectType = DiscardEffectType.GainEnergy;
                card.discardEffectValue = 2;
            }
        );
        discardEffectTestCards.Add(discardEnergy);
        
        Debug.Log("Generated discard effect test cards");
    }
    
    private static void GenerateCostModifierCards()
    {
        // Reduce player card costs
        CardData reduceCost = CreateTestCard(
            "Reduce_Player_Cost", 
            "Your cards cost 1 less this turn.", 
            2, 
            card => { 
            card.costModificationTargetRole = CostModificationTargetRole.Self;
            card.costChangeAmount = -1;
            card.costChangeDuration = 1;
            card.costChangeCardCount = 0; // All cards
            }
        );
        costModTestCards.Add(reduceCost);
        
        // Increase opponent card costs
        CardData increaseCost = CreateTestCard(
            "Increase_Opponent_Cost", 
            "Enemy cards cost 1 more next turn.", 
            2, 
            card => { 
            card.costModificationTargetRole = CostModificationTargetRole.Target;
            card.costChangeAmount = 1;
            card.costChangeDuration = 1;
            card.costChangeCardCount = 0; // All cards
            }
        );
        costModTestCards.Add(increaseCost);
        
        // Reduce specific number of player cards
        CardData reduceSpecificCards = CreateTestCard(
            "Reduce_Specific_Cards", 
            "2 random cards in your hand cost 1 less this turn.", 
            1, 
            card => { 
                card.costModificationTargetRole = CostModificationTargetRole.Self;
                card.costChangeAmount = -1;
                card.costChangeDuration = 1;
                card.costChangeCardCount = 2; // 2 random cards
            }
        );
        costModTestCards.Add(reduceSpecificCards);
        
        Debug.Log("Generated cost modifier test cards");
    }
    
    private static void GenerateCritBuffCards()
    {
        // Player crit buff
        CardData playerCritBuff = CreateTestCard(
            "Player_Crit_Buff", 
            "Gain +25% crit chance for 2 turns.", 
            1, 
            card => { 
                card.critChanceBuffAmount = 25;
                card.critChanceBuffDuration = 2;
                card.critBuffRule = CritBuffTargetRule.Self;
            }
        );
        critBuffTestCards.Add(playerCritBuff);
        
        // Target-based crit buff
        CardData targetCritBuff = CreateTestCard(
            "Target_Crit_Buff", 
            "Give target +25% crit chance for 2 turns.", 
            1, 
            card => { 
                card.critChanceBuffAmount = 25;
                card.critChanceBuffDuration = 2;
                card.critBuffRule = CritBuffTargetRule.Target;
            }
        );
        critBuffTestCards.Add(targetCritBuff);
        
        // Damage with crit buff
        CardData damageCritBuff = CreateTestCard(
            "Damage_With_Crit", 
            "Deal 5 damage and gain +50% crit chance this turn.", 
            2, 
            card => { 
                card.damage = 5;
                card.critChanceBuffAmount = 50;
                card.critChanceBuffDuration = 1;
                card.critBuffRule = CritBuffTargetRule.Self;
            }
        );
        critBuffTestCards.Add(damageCritBuff);
        
        Debug.Log("Generated crit buff test cards");
    }
    
    private static void GenerateDrawDiscardCards()
    {
        // Draw cards
        CardData draw = CreateTestCard(
            "Draw_Cards", 
            "Draw 3 cards.", 
            1, 
            card => { card.drawAmount = 3; }
        );
        drawDiscardTestCards.Add(draw);
        
        // Discard opponent cards
        CardData discardOpponent = CreateTestCard(
            "Discard_Opponent", 
            "Opponent discards 2 random cards.", 
            2, 
            card => { card.discardRandomAmount = 2; }
        );
        drawDiscardTestCards.Add(discardOpponent);
        
        // Draw for player's pet
        CardData drawPet = CreateTestCard(
            "Draw_For_Pet", 
            "Your pet draws 2 cards.", 
            1, 
            card => { card.drawAmount = 2; }
        );
        drawDiscardTestCards.Add(drawPet);
        
        Debug.Log("Generated draw/discard test cards");
    }
    
    private static void GenerateUpgradeCards()
    {
        // Upgrade random cards
        CardData upgradeRandom = CreateTestCard(
            "Upgrade_Random", 
            "Upgrade 2 random cards in hand for this turn.", 
            1, 
            card => { 
            card.upgradeHandCardCount = 2;
            card.upgradeHandCardDuration = 1;
            card.upgradeHandCardTargetRule = UpgradeTargetRule.Random;
            }
        );
        upgradeTestCards.Add(upgradeRandom);
        
        // Upgrade cheapest cards
        CardData upgradeCheapest = CreateTestCard(
            "Upgrade_Cheapest", 
            "Upgrade the 2 cheapest cards in hand for this turn.", 
            1, 
            card => { 
                card.upgradeHandCardCount = 2;
            card.upgradeHandCardDuration = 1;
                card.upgradeHandCardTargetRule = UpgradeTargetRule.Cheapest;
            }
        );
        upgradeTestCards.Add(upgradeCheapest);
        
        // Upgrade most expensive cards
        CardData upgradeMostExpensive = CreateTestCard(
            "Upgrade_Expensive", 
            "Upgrade the 2 most expensive cards in hand for this turn.", 
            1, 
            card => { 
            card.upgradeHandCardCount = 2;
                card.upgradeHandCardDuration = 1;
            card.upgradeHandCardTargetRule = UpgradeTargetRule.MostExpensive;
            }
        );
        upgradeTestCards.Add(upgradeMostExpensive);
        
        Debug.Log("Generated upgrade test cards");
    }
    
    private static void GenerateEnergyCards()
    {
        // Gain energy
        CardData gainEnergy = CreateTestCard(
            "Gain_Energy", 
            "Gain 2 energy.", 
            0, 
            card => { card.energyGain = 2; }
        );
        energyTestCards.Add(gainEnergy);
        
        // Energy and card draw
        CardData energyAndDraw = CreateTestCard(
            "Energy_And_Draw", 
            "Gain 1 energy and draw 2 cards.", 
            1, 
            card => { 
                card.energyGain = 1;
                card.drawAmount = 2;
            }
        );
        energyTestCards.Add(energyAndDraw);
        
        Debug.Log("Generated energy test cards");
    }
    
    private static void GenerateScalingTestCards()
    {
        // Turn scaling damage
        CardData turnScalingDamage = CreateTestCard(
            "Turn_Scaling_Damage", 
            "Deal 5 damage. Deals +2 damage for each turn that passes.", 
            1, 
            card => { 
                card.damage = 5;
                card.damageScalingPerTurn = 2f;
            }
        );
        scalingTestCards.Add(turnScalingDamage);
        
        // Turn scaling block
        CardData turnScalingBlock = CreateTestCard(
            "Turn_Scaling_Block", 
            "Gain 5 block. Gains +2 block for each turn that passes.", 
            1, 
            card => { 
            card.block = 5;
                card.blockScalingPerTurn = 2f;
            }
        );
        scalingTestCards.Add(turnScalingBlock);
        
        // Play count scaling damage
        CardData playScalingDamage = CreateTestCard(
            "Play_Scaling_Damage", 
            "Deal 5 damage. Deals +3 damage for each time played this combat.", 
            1, 
            card => { 
            card.damage = 5;
                card.damageScalingPerPlay = 3f;
            }
        );
        scalingTestCards.Add(playScalingDamage);
        
        // Copy scaling damage
        CardData copyScalingDamage = CreateTestCard(
            "Copy_Scaling_Damage", 
            "Deal 5 damage. Deals +2 damage for each copy in your deck, hand, and discard.", 
            1, 
            card => { 
            card.damage = 5;
                card.damageScalingPerCopy = 2f;
            }
        );
        scalingTestCards.Add(copyScalingDamage);
        
        // Low health threshold damage
        CardData lowHealthDamage = CreateTestCard(
            "Low_Health_Damage", 
            "Deal 8 damage. If your health is below 50%, deal 16 damage instead.", 
            2, 
            card => { 
                card.damage = 8;
                card.healthThresholdPercent = 50;
                card.damageMultiplierBelowThreshold = 2.0f;
            }
        );
        scalingTestCards.Add(lowHealthDamage);
        
        Debug.Log("Generated scaling test cards");
    }
    
    private static void GeneratePetTargetingCards()
    {
        // Card targeting player when played by pet
        CardData petTargetsPlayer = CreateTestCard(
            "Pet_Targets_Player", 
            "Deal 5 damage. When played by a pet, targets the player.", 
            1, 
            card => { 
                card.damage = 5;
                card.primaryTargetWhenPlayedByPet = OpponentPetTargetType.Player;
            }
        );
        petTargetingTestCards.Add(petTargetsPlayer);
        
        // Card targeting self (pet) when played by pet
        CardData petTargetsSelf = CreateTestCard(
            "Pet_Targets_Self", 
            "Gain 5 block. When played by a pet, targets itself.", 
            1, 
            card => { 
                card.block = 5;
                card.primaryTargetWhenPlayedByPet = OpponentPetTargetType.Self;
            }
        );
        petTargetingTestCards.Add(petTargetsSelf);
        
        // Mixed effect card with pet targeting
        CardData mixedPetTargeting = CreateTestCard(
            "Mixed_Pet_Targeting", 
            "Deal 5 damage and gain 5 block. When played by a pet, deals damage to player and gives itself block.", 
            2, 
            card => { 
                card.damage = 5;
                card.block = 5;
                card.primaryTargetWhenPlayedByPet = OpponentPetTargetType.Player; // For damage effect - the block will go to the pet
            }
        );
        petTargetingTestCards.Add(mixedPetTargeting);
        
        Debug.Log("Generated pet targeting test cards");
    }
    
    private static void GenerateHealingCards()
    {
        // Basic healing
        CardData smallHeal = CreateTestCard(
            "Small_Heal", 
            "Heal 5 health.", 
            1, 
            card => { card.healingAmount = 5; }
        );
        healingTestCards.Add(smallHeal);
        
        // Large healing
        CardData largeHeal = CreateTestCard(
            "Large_Heal", 
            "Heal 15 health.", 
            3, 
            card => { card.healingAmount = 15; }
        );
        healingTestCards.Add(largeHeal);
        
        // Damage and heal
        CardData damageAndHeal = CreateTestCard(
            "Damage_And_Heal", 
            "Deal 5 damage and heal 5 health.", 
            2, 
            card => { 
                card.damage = 5;
                card.healingAmount = 5;
            }
        );
        healingTestCards.Add(damageAndHeal);
        
        Debug.Log("Generated healing test cards");
    }
    
    #endregion
    
    #region Quick Setup Methods
    
    [MenuItem("Tools/Test Cards/Setup Player Deck/Damage Test")]
    public static void SetupPlayerDamageTestDeck()
    {
        SetupTestDeck(damageTestCards, true);
    }
    
    [MenuItem("Tools/Test Cards/Setup Player Deck/Block Test")]
    public static void SetupPlayerBlockTestDeck()
    {
        SetupTestDeck(blockTestCards, true);
    }
    
    [MenuItem("Tools/Test Cards/Setup Player Deck/Status Effects Test")]
    public static void SetupPlayerStatusEffectsTestDeck()
    {
        SetupTestDeck(statusEffectTestCards, true);
    }
    
    [MenuItem("Tools/Test Cards/Setup Player Deck/DoT & HoT Test")]
    public static void SetupPlayerDotHotTestDeck()
    {
        SetupTestDeck(dotHotTestCards, true);
    }
    
    [MenuItem("Tools/Test Cards/Setup Player Deck/Combo Test")]
    public static void SetupPlayerComboTestDeck()
    {
        SetupTestDeck(comboTestCards, true);
    }
    
    [MenuItem("Tools/Test Cards/Setup Player Deck/Discard Effects Test")]
    public static void SetupPlayerDiscardEffectsTestDeck()
    {
        SetupTestDeck(discardEffectTestCards, true);
    }
    
    [MenuItem("Tools/Test Cards/Setup Player Deck/Cost Modification Test")]
    public static void SetupPlayerCostModTestDeck()
    {
        SetupTestDeck(costModTestCards, true);
    }
    
    [MenuItem("Tools/Test Cards/Setup Player Deck/Critical Buff Test")]
    public static void SetupPlayerCritBuffTestDeck()
    {
        SetupTestDeck(critBuffTestCards, true);
    }
    
    [MenuItem("Tools/Test Cards/Setup Player Deck/Draw & Discard Test")]
    public static void SetupPlayerDrawDiscardTestDeck()
    {
        SetupTestDeck(drawDiscardTestCards, true);
    }
    
    [MenuItem("Tools/Test Cards/Setup Player Deck/Card Upgrade Test")]
    public static void SetupPlayerUpgradeTestDeck()
    {
        SetupTestDeck(upgradeTestCards, true);
    }
    
    [MenuItem("Tools/Test Cards/Setup Player Deck/Energy Test")]
    public static void SetupPlayerEnergyTestDeck()
    {
        SetupTestDeck(energyTestCards, true);
    }
    
    [MenuItem("Tools/Test Cards/Setup Player Deck/Scaling Test")]
    public static void SetupPlayerScalingTestDeck()
    {
        SetupTestDeck(scalingTestCards, true);
    }
    
    [MenuItem("Tools/Test Cards/Setup Player Deck/Pet Targeting Test")]
    public static void SetupPlayerPetTargetingTestDeck()
    {
        SetupTestDeck(petTargetingTestCards, true);
    }
    
    [MenuItem("Tools/Test Cards/Setup Player Deck/Healing Test")]
    public static void SetupPlayerHealingTestDeck()
    {
        SetupTestDeck(healingTestCards, true);
    }
    
    [MenuItem("Tools/Test Cards/Setup Pet Deck/Damage Test")]
    public static void SetupPetDamageTestDeck()
    {
        SetupTestDeck(damageTestCards, false);
    }
    
    [MenuItem("Tools/Test Cards/Setup Pet Deck/Block Test")]
    public static void SetupPetBlockTestDeck()
    {
        SetupTestDeck(blockTestCards, false);
    }
    
    [MenuItem("Tools/Test Cards/Setup Pet Deck/Status Effects Test")]
    public static void SetupPetStatusEffectsTestDeck()
    {
        SetupTestDeck(statusEffectTestCards, false);
    }
    
    [MenuItem("Tools/Test Cards/Setup Pet Deck/DoT & HoT Test")]
    public static void SetupPetDotHotTestDeck()
    {
        SetupTestDeck(dotHotTestCards, false);
    }
    
    [MenuItem("Tools/Test Cards/Setup Pet Deck/Pet Targeting Test")]
    public static void SetupPetPetTargetingTestDeck()
    {
        SetupTestDeck(petTargetingTestCards, false);
    }
    
    [MenuItem("Tools/Test Cards/Setup Pet Deck/Healing Test")]
    public static void SetupPetHealingTestDeck()
    {
        SetupTestDeck(healingTestCards, false);
    }
    
    [MenuItem("Tools/Test Cards/Setup Both Decks/Damage vs. Block Test")]
    public static void SetupDamageVsBlockTest()
    {
        // Player gets damage cards, pet gets block cards
        SetupTestDeck(damageTestCards, true);
        SetupTestDeck(blockTestCards, false);
    }
    
    [MenuItem("Tools/Test Cards/Setup Both Decks/Block vs. Damage Test")]
    public static void SetupBlockVsDamageTest()
    {
        // Player gets block cards, pet gets damage cards
        SetupTestDeck(blockTestCards, true);
        SetupTestDeck(damageTestCards, false);
    }
    
    [MenuItem("Tools/Test Cards/Setup Both Decks/Status vs. Healing Test")]
    public static void SetupStatusVsHealingTest()
    {
        // Player gets status effect cards, pet gets healing cards
        SetupTestDeck(statusEffectTestCards, true);
        SetupTestDeck(healingTestCards, false);
    }
    
    private static void SetupTestDeck(List<CardData> cardList, bool isPlayerDeck)
    {
        if (cardList == null || cardList.Count == 0)
        {
            Debug.LogWarning("Cannot setup test deck: card list is empty or null. Generate test cards first.");
            return;
        }
        
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("Cannot setup test deck: GameManager not found in scene.");
            return;
        }
        
        // Find or create test cards if they don't exist
        if (!AssetDatabase.IsValidFolder(CardSavePath))
        {
            GenerateCardAssets();
        }
        
        if (isPlayerDeck)
        {
            // Get the starterDeck field via SerializedObject
            SerializedObject serializedObject = new SerializedObject(gameManager);
            SerializedProperty starterDeckProperty = serializedObject.FindProperty("starterDeck");
            starterDeckProperty.ClearArray();
            
            for (int i = 0; i < cardList.Count; i++)
            {
                starterDeckProperty.InsertArrayElementAtIndex(i);
                SerializedProperty element = starterDeckProperty.GetArrayElementAtIndex(i);
                element.objectReferenceValue = cardList[i];
            }
            
            serializedObject.ApplyModifiedProperties();
            Debug.Log($"Player deck set up with {cardList.Count} test cards.");
        }
        else
        {
            // Get the starterPetDeck field via SerializedObject
            SerializedObject serializedObject = new SerializedObject(gameManager);
            SerializedProperty starterPetDeckProperty = serializedObject.FindProperty("starterPetDeck");
            starterPetDeckProperty.ClearArray();
            
            for (int i = 0; i < cardList.Count; i++)
            {
                starterPetDeckProperty.InsertArrayElementAtIndex(i);
                SerializedProperty element = starterPetDeckProperty.GetArrayElementAtIndex(i);
                element.objectReferenceValue = cardList[i];
            }
            
            serializedObject.ApplyModifiedProperties();
            Debug.Log($"Pet deck set up with {cardList.Count} test cards.");
        }
    }
    
    #endregion
    
    private static CardData CreateTestCard(string cardName, string description, int cost, System.Action<CardData> setup)
    {
        string fullCardName = TestCardPrefix + cardName;
        string assetPath = $"{CardSavePath}/{fullCardName}.asset";
        
        // Create the card as a ScriptableObject asset
        CardData card = ScriptableObject.CreateInstance<CardData>();
        card.cardName = fullCardName;
        card.description = description;
        card.cost = cost;
        
        // Use the setup action to configure the card
        setup(card);
        
        // Save the card asset
        AssetDatabase.CreateAsset(card, assetPath);
        
        return card;
    }
} 