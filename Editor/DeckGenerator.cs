using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Editor script to generate themed starter decks for characters and pets
/// </summary>
public class DeckGenerator : EditorWindow
{
    private const string CARDS_FOLDER = "Assets/Generated/Cards/";
    private const string DECKS_FOLDER = "Assets/Generated/Decks/";
    
    [MenuItem("Tools/Generate Starter Decks")]
    public static void ShowWindow()
    {
        GetWindow<DeckGenerator>("Deck Generator");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Starter Deck Generator", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        GUILayout.Label("This will generate:");
        GUILayout.Label("• 3 Character Decks (Warrior, Mage, Cleric)");
        GUILayout.Label("• 3 Pet Decks (Beast, Elemental, Guardian)");
        GUILayout.Label("• Each deck: 4 Attack + 4 Defense + 2 Special cards");
        GUILayout.Label("• 60 additional draft cards (10 per theme)");
        GUILayout.Label("• Upgraded versions of all cards");
        GUILayout.Space(10);
        
        if (GUILayout.Button("Generate All Cards + Upgrades", GUILayout.Height(35)))
        {
            GenerateAllDecks();
            GenerateAllDraftCards();
            GenerateAllUpgradedCards();
        }
        
        GUILayout.Space(5);
        
        if (GUILayout.Button("Generate Only Starter Decks", GUILayout.Height(25)))
        {
            GenerateAllDecks();
        }
        
        if (GUILayout.Button("Generate Only Draft Cards", GUILayout.Height(25)))
        {
            GenerateAllDraftCards();
        }
        
        if (GUILayout.Button("Generate Only Upgraded Cards", GUILayout.Height(25)))
        {
            GenerateAllUpgradedCards();
        }
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Clear Generated Assets", GUILayout.Height(20)))
        {
            ClearGeneratedAssets();
        }
    }
    
    private void GenerateAllDecks()
    {
        // Ensure directories exist
        Directory.CreateDirectory(CARDS_FOLDER);
        Directory.CreateDirectory(DECKS_FOLDER);
        
        // Generate shared basic cards
        var basicAttack = CreateBasicAttack();
        var basicDefend = CreateBasicDefend();
        
        // Character Decks
        GenerateWarriorDeck(basicAttack, basicDefend);
        GenerateMageDeck(basicAttack, basicDefend);
        GenerateClericDeck(basicAttack, basicDefend);
        
        // Pet Decks
        GenerateBeastDeck(basicAttack, basicDefend);
        GenerateElementalDeck(basicAttack, basicDefend);
        GenerateGuardianDeck(basicAttack, basicDefend);
        
        // Force save all created assets
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // Force dirty all CardData assets to ensure effects are saved
        string[] cardAssets = AssetDatabase.FindAssets("t:CardData", new[] { CARDS_FOLDER });
        foreach (string guid in cardAssets)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (card != null)
            {
                EditorUtility.SetDirty(card);
            }
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("✅ All starter decks generated successfully!");
    }
    
    private void GenerateAllDraftCards()
    {
        // Ensure directories exist
        Directory.CreateDirectory(CARDS_FOLDER + "Draft/");
        
        // Generate draft cards for each theme
        GenerateWarriorDraftCards();
        GenerateMageDraftCards();
        GenerateClericDraftCards();
        GenerateBeastDraftCards();
        GenerateElementalDraftCards();
        GenerateGuardianDraftCards();
        
        // Force save all created assets
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // Force dirty all CardData assets to ensure effects are saved
        string[] cardAssets = AssetDatabase.FindAssets("t:CardData", new[] { CARDS_FOLDER });
        foreach (string guid in cardAssets)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (card != null)
            {
                EditorUtility.SetDirty(card);
                Debug.Log($"Marked dirty: {card.CardName} (asset: {card.name})");
            }
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("✅ All draft cards generated successfully! (60 cards total)");
    }
    
    #region Basic Shared Cards
    
    private CardData CreateBasicAttack()
    {
        var card = CreateCardAsset("BasicAttack", "Basic Attack", "Deal 6 damage to target opponent.");
        card.SetupBasicDamageCard("Basic Attack", 6, 1, CardTargetType.Opponent);
        SaveCardAsset(card);
        return card;
    }
    
    private CardData CreateBasicDefend()
    {
        var card = CreateCardAsset("BasicDefend", "Basic Defend", "Gain 5 Shield.");
        
        // Set card type to Skill for defense cards
        var cardTypeField = typeof(CardData).GetField("_cardType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        cardTypeField?.SetValue(card, CardType.Skill);
        
        card.AddEffect(CardEffectType.ApplyShield, 5, CardTargetType.Self);
        SaveCardAsset(card);
        return card;
    }
    
    #endregion
    
    #region Character Decks
    
    private void GenerateWarriorDeck(CardData basicAttack, CardData basicDefend)
    {
        var cards = new List<CardData>();
        
        // 4 Basic Attack, 4 Basic Defend
        for (int i = 0; i < 4; i++) cards.Add(basicAttack);
        for (int i = 0; i < 4; i++) cards.Add(basicDefend);
        
        // Special Card 1: Combo Strike
        var comboStrike = CreateCardAsset("WarriorComboStrike", "Combo Strike", "Deal 4 damage. Builds combo for powerful finishers.");
        comboStrike.SetupBasicDamageCard("Combo Strike", 4, 1);
        comboStrike.MakeComboCard();
        SaveCardAsset(comboStrike);
        cards.Add(comboStrike);
        
        // Special Card 2: Devastating Blow (Finisher)
        var devastatingBlow = CreateCardAsset("WarriorDevastatingBlow", "Devastating Blow", "Deal 15 damage. Requires combo to play.");
        devastatingBlow.SetupBasicDamageCard("Devastating Blow", 15, 2);
        devastatingBlow.RequireCombo(1);
        SaveCardAsset(devastatingBlow);
        cards.Add(devastatingBlow);
        
        CreateDeckAsset("WarriorStarterDeck", "Warrior Starter Deck", cards);
        Debug.Log("🗡️ Warrior deck created: Combo-focused melee combat");
    }
    
    private void GenerateMageDeck(CardData basicAttack, CardData basicDefend)
    {
        var cards = new List<CardData>();
        
        // 4 Basic Attack, 4 Basic Defend
        for (int i = 0; i < 4; i++) cards.Add(basicAttack);
        for (int i = 0; i < 4; i++) cards.Add(basicDefend);
        
        // Special Card 1: Mana Surge
        var manaSurge = CreateCardAsset("MageManaSurge", "Mana Surge", "Restore 2 energy and draw 1 card.");
        SetCardType(manaSurge, CardType.Skill, 1);
        manaSurge.AddEffect(CardEffectType.RestoreEnergy, 2, CardTargetType.Self);
        manaSurge.AddEffect(CardEffectType.DrawCard, 1, CardTargetType.Self);
        SaveCardAsset(manaSurge);
        cards.Add(manaSurge);
        
        // Special Card 2: Conditional Fireball
        var fireball = CreateCardAsset("MageFireball", "Conditional Fireball", "Deal 8 damage. If target health is below 25, deal 12 damage instead.");
        SetCardType(fireball, CardType.Spell, 2);
        fireball.AddConditionalEffectOR(CardEffectType.Damage, 8, CardTargetType.Opponent,
                                      ConditionalType.IfTargetHealthBelow, 25, 
                                      CardEffectType.Damage, 12);
        SaveCardAsset(fireball);
        cards.Add(fireball);
        
        CreateDeckAsset("MageStarterDeck", "Mage Starter Deck", cards);
        Debug.Log("🔮 Mage deck created: Energy manipulation and conditional spells");
    }
    
    private void GenerateClericDeck(CardData basicAttack, CardData basicDefend)
    {
        var cards = new List<CardData>();
        
        // 4 Basic Attack, 4 Basic Defend
        for (int i = 0; i < 4; i++) cards.Add(basicAttack);
        for (int i = 0; i < 4; i++) cards.Add(basicDefend);
        
        // Special Card 1: Healing Light
        var healingLight = CreateCardAsset("ClericHealingLight", "Healing Light", "Heal 8 health and gain 2 Strength.");
        healingLight.SetupBasicHealCard("Healing Light", 8, 2);
        healingLight.AddEffect(CardEffectType.ApplyStrength, 2, CardTargetType.Self);
        SaveCardAsset(healingLight);
        cards.Add(healingLight);
        
        // Special Card 2: Divine Protection
        var divineProtection = CreateCardAsset("ClericDivineProtection", "Divine Protection", "Gain 8 Shield and apply Weak to opponent for 2 turns.");
        SetCardType(divineProtection, CardType.Skill, 2);
        divineProtection.AddEffect(CardEffectType.ApplyShield, 8, CardTargetType.Self);
        divineProtection.AddEffect(CardEffectType.ApplyWeak, 0, CardTargetType.Opponent, 2);
        SaveCardAsset(divineProtection);
        cards.Add(divineProtection);
        
        CreateDeckAsset("ClericStarterDeck", "Cleric Starter Deck", cards);
        Debug.Log("⛪ Cleric deck created: Healing and support magic");
    }
    
    #endregion
    
    #region Pet Decks
    
    private void GenerateBeastDeck(CardData basicAttack, CardData basicDefend)
    {
        var cards = new List<CardData>();
        
        // 4 Basic Attack, 4 Basic Defend
        for (int i = 0; i < 4; i++) cards.Add(basicAttack);
        for (int i = 0; i < 4; i++) cards.Add(basicDefend);
        
        // Special Card 1: Primal Rage
        var primalRage = CreateCardAsset("BeastPrimalRage", "Primal Rage", "Gain 3 Strength and 3 Thorns until your next turn.");
        SetCardType(primalRage, CardType.Skill, 2);
        primalRage.AddEffect(CardEffectType.ApplyStrength, 3, CardTargetType.Self);
        primalRage.AddEffect(CardEffectType.ApplyThorns, 3, CardTargetType.Self);
        SaveCardAsset(primalRage);
        cards.Add(primalRage);
        
        // Special Card 2: Scaling Claw
        var scalingClaw = CreateCardAsset("BeastScalingClaw", "Scaling Claw", "Deal damage equal to 3 + cards played this fight.");
        scalingClaw.SetupBasicDamageCard("Scaling Claw", 3, 2);
        // Add scaling effect via helper method
        AddScalingToCard(scalingClaw, ScalingType.CardsPlayedThisFight, 1.0f, 15);
        SaveCardAsset(scalingClaw);
        cards.Add(scalingClaw);
        
        CreateDeckAsset("BeastStarterDeck", "Beast Starter Deck", cards);
        Debug.Log("🐺 Beast deck created: Strength stacking and aggressive tactics");
    }
    
    private void GenerateElementalDeck(CardData basicAttack, CardData basicDefend)
    {
        var cards = new List<CardData>();
        
        // 4 Basic Attack, 4 Basic Defend
        for (int i = 0; i < 4; i++) cards.Add(basicAttack);
        for (int i = 0; i < 4; i++) cards.Add(basicDefend);
        
        // Special Card 1: Elemental Burst
        var elementalBurst = CreateCardAsset("ElementalBurst", "Elemental Burst", "Deal 5 damage and apply Break for 2 turns.");
        elementalBurst.SetupBasicDamageCard("Elemental Burst", 5, 2);
        elementalBurst.AddEffect(CardEffectType.ApplyBreak, 0, CardTargetType.Opponent, 2);
        SaveCardAsset(elementalBurst);
        cards.Add(elementalBurst);
        
        // Special Card 2: Status Storm
        var statusStorm = CreateCardAsset("ElementalStatusStorm", "Status Storm", "Apply Stun for 1 turn. If opponent has low health, also deal 7 damage.");
        SetCardType(statusStorm, CardType.Spell, 2);
        statusStorm.AddEffect(CardEffectType.ApplyStun, 0, CardTargetType.Opponent, 1);
        statusStorm.AddConditionalEffectAND(CardEffectType.ApplyStun, 0, CardTargetType.Opponent,
                                          ConditionalType.IfTargetHealthBelow, 30, // More reasonable condition
                                          CardEffectType.Damage, 7);
        SaveCardAsset(statusStorm);
        cards.Add(statusStorm);
        
        CreateDeckAsset("ElementalStarterDeck", "Elemental Starter Deck", cards);
        Debug.Log("⚡ Elemental deck created: Status effects and elemental magic");
    }
    
    private void GenerateGuardianDeck(CardData basicAttack, CardData basicDefend)
    {
        var cards = new List<CardData>();
        
        // 4 Basic Attack, 4 Basic Defend
        for (int i = 0; i < 4; i++) cards.Add(basicAttack);
        for (int i = 0; i < 4; i++) cards.Add(basicDefend);
        
        // Special Card 1: Regeneration
        var regeneration = CreateCardAsset("GuardianRegeneration", "Regeneration", "Heal 3 health over time for 3 turns.");
        SetCardType(regeneration, CardType.Skill, 1);
        regeneration.AddEffect(CardEffectType.ApplyHealOverTime, 3, CardTargetType.Self, 3);
        SaveCardAsset(regeneration);
        cards.Add(regeneration);
        
        // Special Card 2: Guardian's Resolve
        var guardianResolve = CreateCardAsset("GuardianResolve", "Guardian's Resolve", "Gain 12 Shield. If health is below 50%, gain 6 additional Shield.");
        SetCardType(guardianResolve, CardType.Skill, 2);
        guardianResolve.AddConditionalEffectAND(CardEffectType.ApplyShield, 12, CardTargetType.Self,
                                              ConditionalType.IfSourceHealthBelow, 50,
                                              CardEffectType.ApplyShield, 6);
        SaveCardAsset(guardianResolve);
        cards.Add(guardianResolve);
        
        CreateDeckAsset("GuardianStarterDeck", "Guardian Starter Deck", cards);
        Debug.Log("🛡️ Guardian deck created: Defensive tactics and healing over time");
    }
    
    #endregion
    
    #region Utility Methods
    
    private CardData CreateCardAsset(string fileName, string cardName, string description)
    {
        var card = ScriptableObject.CreateInstance<CardData>();
        
        // Use reflection to set private fields since we can't access them directly
        var cardIdField = typeof(CardData).GetField("_cardId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cardNameField = typeof(CardData).GetField("_cardName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var descriptionField = typeof(CardData).GetField("_description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cardTypeField = typeof(CardData).GetField("_cardType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var energyCostField = typeof(CardData).GetField("_energyCost", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var effectsField = typeof(CardData).GetField("_effects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        cardIdField?.SetValue(card, fileName.GetHashCode());
        cardNameField?.SetValue(card, cardName);
        descriptionField?.SetValue(card, description);
        cardTypeField?.SetValue(card, CardType.Attack);
        energyCostField?.SetValue(card, 1);
        
        // Initialize the effects list
        effectsField?.SetValue(card, new System.Collections.Generic.List<CardEffect>());
        
        // Set the card category based on file path
        CardCategory category = DetermineCardCategory(fileName);
        card.SetCardCategory(category);
        
        string assetPath = CARDS_FOLDER + fileName + ".asset";
        
        // Ensure directory exists for nested paths
        string directoryPath = System.IO.Path.GetDirectoryName(assetPath);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
        
        AssetDatabase.CreateAsset(card, assetPath);
        
        // Set the asset name to match the filename (without path and extension)
        string assetFileName = System.IO.Path.GetFileNameWithoutExtension(fileName);
        card.name = assetFileName;
        
        return card;
    }
    
    /// <summary>
    /// Determines the card category based on the file path
    /// </summary>
    private CardCategory DetermineCardCategory(string fileName)
    {
        string fileNameLower = fileName.ToLower();
        
        if (fileNameLower.Contains("upgraded/") || fileNameLower.Contains("_upgraded"))
        {
            return CardCategory.Upgraded;
        }
        else if (fileNameLower.Contains("draft/"))
        {
            return CardCategory.Draftable;
        }
        else if (fileNameLower.Contains("basicattack") || fileNameLower.Contains("basicdefend") || 
                 fileNameLower.Contains("starter/"))
        {
            return CardCategory.Starter;
        }
        else
        {
            // Default to draftable for other cards (like special starter deck cards)
            return CardCategory.Draftable;
        }
    }
    
    private void SaveCardAsset(CardData card)
    {
        // Mark the asset as dirty so changes are saved
        EditorUtility.SetDirty(card);
    }
    
    private CardData CreateAndSetupCard(string fileName, string cardName, string description, System.Action<CardData> setupAction)
    {
        var card = CreateCardAsset(fileName, cardName, description);
        setupAction?.Invoke(card);
        SaveCardAsset(card);
        return card;
    }
    
    private void AddScalingToCard(CardData card, ScalingType scalingType, float multiplier, int maxScaling)
    {
        // Get the effects field via reflection
        var effectsField = typeof(CardData).GetField("_effects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var effects = effectsField?.GetValue(card) as System.Collections.Generic.List<CardEffect>;
        
        if (effects != null && effects.Count > 0)
        {
            // Modify the first effect to include scaling
            var firstEffect = effects[0];
            firstEffect.scalingType = scalingType;
            firstEffect.scalingMultiplier = multiplier;
            firstEffect.maxScaling = maxScaling;
        }
    }
    
    private void SetCardType(CardData card, CardType cardType, int energyCost)
    {
        var cardTypeField = typeof(CardData).GetField("_cardType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var energyCostField = typeof(CardData).GetField("_energyCost", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        cardTypeField?.SetValue(card, cardType);
        energyCostField?.SetValue(card, energyCost);
    }
    
    private DeckData CreateDeckAsset(string fileName, string deckName, List<CardData> cards)
    {
        var deck = ScriptableObject.CreateInstance<DeckData>();
        
        // Use reflection to set private fields
        var deckNameField = typeof(DeckData).GetField("_deckName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cardsField = typeof(DeckData).GetField("_cardsInDeck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        deckNameField?.SetValue(deck, deckName);
        cardsField?.SetValue(deck, cards);
        
        string assetPath = DECKS_FOLDER + fileName + ".asset";
        AssetDatabase.CreateAsset(deck, assetPath);
        
        return deck;
    }
    
    private void ClearGeneratedAssets()
    {
        if (Directory.Exists("Assets/Generated"))
        {
            AssetDatabase.DeleteAsset("Assets/Generated");
            AssetDatabase.Refresh();
            Debug.Log("🗑️ Cleared all generated assets");
        }
    }
    
    #endregion
    
    #region Draft Cards Generation
    
    private void GenerateWarriorDraftCards()
    {
        // Warrior Draft Cards - Combo and Heavy Damage Focus
        
        // Card 1: Berserker Strike
        var berserkerStrike = CreateCardAsset("Draft/WarriorBerserkerStrike", "Berserker Strike", "Deal 8 damage. If you have combo, deal 4 additional damage.");
        SetCardType(berserkerStrike, CardType.Attack, 2);
        berserkerStrike.AddConditionalEffectAND(CardEffectType.Damage, 8, CardTargetType.Opponent,
                                               ConditionalType.IfComboCount, 1, CardEffectType.Damage, 4);
        
        // Card 2: Combo Mastery
        var comboMastery = CreateCardAsset("Draft/WarriorComboMastery", "Combo Mastery", "Build combo and draw 1 card. Costs 0 if you have no combo.");
        SetCardType(comboMastery, CardType.Combo, 1);
        comboMastery.MakeComboCard();
        comboMastery.AddEffect(CardEffectType.DrawCard, 1, CardTargetType.Self);
        
        // Card 3: Execute
        var execute = CreateCardAsset("Draft/WarriorExecute", "Execute", "Deal 20 damage. Can only be played if opponent has less than 25 health.");
        SetCardType(execute, CardType.Finisher, 3);
        execute.AddConditionalEffectOR(CardEffectType.Damage, 0, CardTargetType.Opponent,
                                     ConditionalType.IfTargetHealthAbove, 25, CardEffectType.Damage, 20);
        
        // Card 4: Rage
        var rage = CreateCardAsset("Draft/WarriorRage", "Rage", "Gain 4 Strength. Take 3 damage.");
        SetCardType(rage, CardType.Skill, 1);
        rage.AddEffect(CardEffectType.ApplyStrength, 4, CardTargetType.Self);
        rage.AddEffect(CardEffectType.Damage, 3, CardTargetType.Self);
        
        // Card 5: Cleave
        var cleave = CreateCardAsset("Draft/WarriorCleave", "Cleave", "Deal 6 damage to opponent and 3 damage to all other enemies.");
        SetCardType(cleave, CardType.Attack, 2);
        cleave.AddEffect(CardEffectType.Damage, 6, CardTargetType.Opponent);
        cleave.AddEffect(CardEffectType.Damage, 3, CardTargetType.AllEnemies);
        
        // Card 6: Battle Frenzy
        var battleFrenzy = CreateCardAsset("Draft/WarriorBattleFrenzy", "Battle Frenzy", "Deal damage equal to 2 + damage dealt this fight.");
        SetCardType(battleFrenzy, CardType.Attack, 1);
        battleFrenzy.SetupBasicDamageCard("Battle Frenzy", 2, 1);
        AddScalingToCard(battleFrenzy, ScalingType.DamageDealtThisFight, 0.2f, 12);
        
        // Card 7: Intimidate
        var intimidate = CreateCardAsset("Draft/WarriorIntimidate", "Intimidate", "Apply Weak for 3 turns. Build combo.");
        SetCardType(intimidate, CardType.Skill, 1);
        intimidate.AddEffect(CardEffectType.ApplyWeak, 0, CardTargetType.Opponent, 3);
        intimidate.MakeComboCard();
        
        // Card 8: Perfect Strike
        var perfectStrike = CreateCardAsset("Draft/WarriorPerfectStrike", "Perfect Strike", "Deal 12 damage. Requires combo. Exhausts.");
        SetCardType(perfectStrike, CardType.Finisher, 2);
        perfectStrike.RequireCombo(1);
        perfectStrike.SetupBasicDamageCard("Perfect Strike", 12, 2);
        
        // Card 9: Warrior's Resolve
        var warriorsResolve = CreateCardAsset("Draft/WarriorResolve", "Warrior's Resolve", "Gain 8 Shield and 2 Strength.");
        SetCardType(warriorsResolve, CardType.Skill, 2);
        warriorsResolve.AddEffect(CardEffectType.ApplyShield, 8, CardTargetType.Self);
        warriorsResolve.AddEffect(CardEffectType.ApplyStrength, 2, CardTargetType.Self);
        
        // Card 10: Whirlwind
        var whirlwind = CreateCardAsset("Draft/WarriorWhirlwind", "Whirlwind", "Deal 4 damage to all enemies. Damage increases by 1 for each card played this turn.");
        SetCardType(whirlwind, CardType.Attack, 2);
        whirlwind.SetupBasicDamageCard("Whirlwind", 4, 2, CardTargetType.AllEnemies);
        AddScalingToCard(whirlwind, ScalingType.CardsPlayedThisTurn, 1.0f, 10);
        
        Debug.Log("🗡️ Generated 10 Warrior draft cards");
    }
    
    private void GenerateMageDraftCards()
    {
        // Mage Draft Cards - Spell Power and Energy Manipulation
        
        // Card 1: Arcane Orb
        var arcaneOrb = CreateCardAsset("Draft/MageArcaneOrb", "Arcane Orb", "Deal 5 damage. Draw 1 card if target dies.");
        SetCardType(arcaneOrb, CardType.Spell, 1);
        arcaneOrb.AddConditionalEffectAND(CardEffectType.Damage, 5, CardTargetType.Opponent,
                                        ConditionalType.IfTargetHealthBelow, 6, CardEffectType.DrawCard, 1);
        
        // Card 2: Mana Burn
        var manaBurn = CreateCardAsset("Draft/MageManaBurn", "Mana Burn", "Restore 3 energy. Apply Weak for 1 turn.");
        SetCardType(manaBurn, CardType.Spell, 0);
        manaBurn.AddEffect(CardEffectType.RestoreEnergy, 3, CardTargetType.Self);
        manaBurn.AddEffect(CardEffectType.ApplyWeak, 0, CardTargetType.Opponent, 1);
        
        // Card 3: Chain Lightning
        var chainLightning = CreateCardAsset("Draft/MageChainLightning", "Chain Lightning", "Deal 7 damage. If opponent is Weak, deal 3 damage to all enemies.");
        SetCardType(chainLightning, CardType.Spell, 2);
        chainLightning.AddEffect(CardEffectType.Damage, 7, CardTargetType.Opponent);
        chainLightning.AddConditionalEffectAND(CardEffectType.Damage, 7, CardTargetType.Opponent,
                                             ConditionalType.IfTargetHealthBelow, 100, CardEffectType.Damage, 3);
        
        // Card 4: Frost Armor
        var frostArmor = CreateCardAsset("Draft/MageFrostArmor", "Frost Armor", "Gain 6 Shield. Next attacker gets Weak for 2 turns.");
        SetCardType(frostArmor, CardType.Skill, 2);
        frostArmor.AddEffect(CardEffectType.ApplyShield, 6, CardTargetType.Self);
        frostArmor.AddEffect(CardEffectType.ApplyThorns, 2, CardTargetType.Self);
        
        // Card 5: Meteor
        var meteor = CreateCardAsset("Draft/MageMeteor", "Meteor", "Deal 15 damage to all enemies. Costs 1 less for each spell played this turn.");
        SetCardType(meteor, CardType.Spell, 4);
        meteor.SetupBasicDamageCard("Meteor", 15, 4, CardTargetType.AllEnemies);
        
        // Card 6: Spell Echo
        var spellEcho = CreateCardAsset("Draft/MageSpellEcho", "Spell Echo", "The next spell you play this turn triggers twice.");
        SetCardType(spellEcho, CardType.Skill, 1);
        spellEcho.AddEffect(CardEffectType.RaiseCriticalChance, 100, CardTargetType.Self, 1);
        
        // Card 7: Dispel
        var dispel = CreateCardAsset("Draft/MageDispel", "Dispel", "Remove all debuffs from self. Deal 3 damage for each removed.");
        SetCardType(dispel, CardType.Spell, 1);
        dispel.SetupBasicDamageCard("Dispel", 3, 1);
        AddScalingToCard(dispel, ScalingType.HandSize, 1.0f, 15);
        
        // Card 8: Time Warp
        var timeWarp = CreateCardAsset("Draft/MageTimeWarp", "Time Warp", "Draw 3 cards. They cost 0 this turn.");
        SetCardType(timeWarp, CardType.Spell, 3);
        timeWarp.AddEffect(CardEffectType.DrawCard, 3, CardTargetType.Self);
        
        // Card 9: Elemental Shield
        var elementalShield = CreateCardAsset("Draft/MageElementalShield", "Elemental Shield", "Gain Shield equal to 3 + cards in hand.");
        SetCardType(elementalShield, CardType.Skill, 1);
        elementalShield.AddEffect(CardEffectType.ApplyShield, 3, CardTargetType.Self);
        AddScalingToCard(elementalShield, ScalingType.HandSize, 1.0f, 12);
        
        // Card 10: Arcane Mastery
        var arcaneMastery = CreateCardAsset("Draft/MageArcaneMastery", "Arcane Mastery", "Permanently gain: 'Spells deal 1 additional damage this fight.'");
        SetCardType(arcaneMastery, CardType.Skill, 2);
        arcaneMastery.AddEffect(CardEffectType.ApplyStrength, 1, CardTargetType.Self);
        
        Debug.Log("🔮 Generated 10 Mage draft cards");
    }
    
    private void GenerateClericDraftCards()
    {
        // Cleric Draft Cards - Healing and Holy Magic
        
        // Card 1: Greater Heal
        var greaterHeal = CreateCardAsset("Draft/ClericGreaterHeal", "Greater Heal", "Heal 12 health. Gain 1 Strength if at full health after healing.");
        SetCardType(greaterHeal, CardType.Skill, 2);
        greaterHeal.SetupBasicHealCard("Greater Heal", 12, 2);
        greaterHeal.AddConditionalEffectAND(CardEffectType.Heal, 12, CardTargetType.Self,
                                          ConditionalType.IfSourceHealthAbove, 95, CardEffectType.ApplyStrength, 1);
        
        // Card 2: Blessed Strike
        var blessedStrike = CreateCardAsset("Draft/ClericBlessedStrike", "Blessed Strike", "Deal 6 damage. Heal 3 health. Build combo if opponent is Weak.");
        SetCardType(blessedStrike, CardType.Attack, 2);
        blessedStrike.SetupBasicDamageCard("Blessed Strike", 6, 2);
        blessedStrike.AddEffect(CardEffectType.Heal, 3, CardTargetType.Self);
        
        // Card 3: Divine Wrath
        var divineWrath = CreateCardAsset("Draft/ClericDivineWrath", "Divine Wrath", "Deal damage equal to health you've healed this fight (max 20).");
        SetCardType(divineWrath, CardType.Spell, 3);
        divineWrath.SetupBasicDamageCard("Divine Wrath", 5, 3);
        AddScalingToCard(divineWrath, ScalingType.DamageDealtThisFight, 0.3f, 20);
        
        // Card 4: Sanctuary
        var sanctuary = CreateCardAsset("Draft/ClericSanctuary", "Sanctuary", "Gain 10 Shield. Heal 2 health at start of next 3 turns.");
        SetCardType(sanctuary, CardType.Skill, 2);
        sanctuary.AddEffect(CardEffectType.ApplyShield, 10, CardTargetType.Self);
        sanctuary.AddEffect(CardEffectType.ApplyHealOverTime, 2, CardTargetType.Self, 3);
        
        // Card 5: Purify
        var purify = CreateCardAsset("Draft/ClericPurify", "Purify", "Remove all debuffs from all allies. Heal 5 for each removed.");
        SetCardType(purify, CardType.Skill, 2);
        purify.AddEffect(CardEffectType.Heal, 5, CardTargetType.AllAllies);
        
        // Card 6: Guardian Angel
        var guardianAngel = CreateCardAsset("Draft/ClericGuardianAngel", "Guardian Angel", "If you would take fatal damage this turn, instead heal to 1 and gain 20 Shield.");
        SetCardType(guardianAngel, CardType.Skill, 3);
        guardianAngel.AddConditionalEffectOR(CardEffectType.ApplyShield, 0, CardTargetType.Self,
                                           ConditionalType.IfSourceHealthBelow, 5, CardEffectType.ApplyShield, 20);
        
        // Card 7: Consecration
        var consecration = CreateCardAsset("Draft/ClericConsecration", "Consecration", "All enemies take 4 damage. All allies heal 4 health.");
        SetCardType(consecration, CardType.Spell, 3);
        consecration.AddEffect(CardEffectType.Damage, 4, CardTargetType.AllEnemies);
        consecration.AddEffect(CardEffectType.Heal, 4, CardTargetType.AllAllies);
        
        // Card 8: Smite
        var smite = CreateCardAsset("Draft/ClericSmite", "Smite", "Deal 8 damage. If target has debuffs, deal double damage.");
        SetCardType(smite, CardType.Attack, 2);
        smite.AddConditionalEffectOR(CardEffectType.Damage, 8, CardTargetType.Opponent,
                                   ConditionalType.IfTargetHealthBelow, 100, CardEffectType.Damage, 16);
        
        // Card 9: Blessing of Might
        var blessingOfMight = CreateCardAsset("Draft/ClericBlessingOfMight", "Blessing of Might", "All allies gain 2 Strength and 5 Shield.");
        SetCardType(blessingOfMight, CardType.Skill, 3);
        blessingOfMight.AddEffect(CardEffectType.ApplyStrength, 2, CardTargetType.AllAllies);
        blessingOfMight.AddEffect(CardEffectType.ApplyShield, 5, CardTargetType.AllAllies);
        
        // Card 10: Resurrection
        var resurrection = CreateCardAsset("Draft/ClericResurrection", "Resurrection", "Heal to full health. Costs 1 less for each 10 health missing.");
        SetCardType(resurrection, CardType.Skill, 5);
        resurrection.SetupBasicHealCard("Resurrection", 50, 5);
        AddScalingToCard(resurrection, ScalingType.MissingHealth, 2.0f, 50);
        
        Debug.Log("⛪ Generated 10 Cleric draft cards");
    }
    
    private void GenerateBeastDraftCards()
    {
        // Beast Draft Cards - Primal Power and Scaling
        
        // Card 1: Alpha Strike
        var alphaStrike = CreateCardAsset("Draft/BeastAlphaStrike", "Alpha Strike", "Deal 10 damage. If this kills the target, gain 3 Strength permanently.");
        SetCardType(alphaStrike, CardType.Attack, 3);
        alphaStrike.AddConditionalEffectAND(CardEffectType.Damage, 10, CardTargetType.Opponent,
                                          ConditionalType.IfTargetHealthBelow, 11, CardEffectType.ApplyStrength, 3);
        
        // Card 2: Pack Hunt
        var packHunt = CreateCardAsset("Draft/BeastPackHunt", "Pack Hunt", "Deal damage equal to 3 + your Strength to all enemies.");
        SetCardType(packHunt, CardType.Attack, 2);
        packHunt.SetupBasicDamageCard("Pack Hunt", 3, 2, CardTargetType.AllEnemies);
        AddScalingToCard(packHunt, ScalingType.DamageDealtThisFight, 0.1f, 15);
        
        // Card 3: Territorial Roar
        var territorialRoar = CreateCardAsset("Draft/BeastTerritorialRoar", "Territorial Roar", "Apply Weak to all enemies for 2 turns. Gain 2 Strength.");
        SetCardType(territorialRoar, CardType.Skill, 2);
        territorialRoar.AddEffect(CardEffectType.ApplyWeak, 0, CardTargetType.AllEnemies, 2);
        territorialRoar.AddEffect(CardEffectType.ApplyStrength, 2, CardTargetType.Self);
        
        // Card 4: Feral Instincts
        var feralInstincts = CreateCardAsset("Draft/BeastFeralInstincts", "Feral Instincts", "Gain 2 Strength and 4 Thorns. Take 2 damage.");
        SetCardType(feralInstincts, CardType.Skill, 1);
        feralInstincts.AddEffect(CardEffectType.ApplyStrength, 2, CardTargetType.Self);
        feralInstincts.AddEffect(CardEffectType.ApplyThorns, 4, CardTargetType.Self);
        feralInstincts.AddEffect(CardEffectType.Damage, 2, CardTargetType.Self);
        
        // Card 5: Blood Frenzy
        var bloodFrenzy = CreateCardAsset("Draft/BeastBloodFrenzy", "Blood Frenzy", "Deal 6 damage. Heal 3 health. Damage increases by 2 for each enemy killed this fight.");
        SetCardType(bloodFrenzy, CardType.Attack, 2);
        bloodFrenzy.SetupBasicDamageCard("Blood Frenzy", 6, 2);
        bloodFrenzy.AddEffect(CardEffectType.Heal, 3, CardTargetType.Self);
        AddScalingToCard(bloodFrenzy, ScalingType.CardsPlayedThisFight, 0.1f, 16);
        
        // Card 6: Apex Predator
        var apexPredator = CreateCardAsset("Draft/BeastApexPredator", "Apex Predator", "Deal 12 damage. Gain 1 Strength for each 5 damage dealt.");
        SetCardType(apexPredator, CardType.Attack, 3);
        apexPredator.SetupBasicDamageCard("Apex Predator", 12, 3);
        apexPredator.AddConditionalEffectAND(CardEffectType.Damage, 12, CardTargetType.Opponent,
                                           ConditionalType.IfTargetHealthAbove, 0, CardEffectType.ApplyStrength, 2);
        
        // Card 7: Primal Armor
        var primalArmor = CreateCardAsset("Draft/BeastPrimalArmor", "Primal Armor", "Gain Shield equal to 5 + your Strength. Gain 6 Thorns.");
        SetCardType(primalArmor, CardType.Skill, 2);
        primalArmor.AddEffect(CardEffectType.ApplyShield, 5, CardTargetType.Self);
        primalArmor.AddEffect(CardEffectType.ApplyThorns, 6, CardTargetType.Self);
        AddScalingToCard(primalArmor, ScalingType.DamageDealtThisFight, 0.2f, 15);
        
        // Card 8: Savage Charge
        var savageCharge = CreateCardAsset("Draft/BeastSavageCharge", "Savage Charge", "Deal 8 damage. Apply Break for 3 turns. Take 3 damage.");
        SetCardType(savageCharge, CardType.Attack, 2);
        savageCharge.SetupBasicDamageCard("Savage Charge", 8, 2);
        savageCharge.AddEffect(CardEffectType.ApplyBreak, 0, CardTargetType.Opponent, 3);
        savageCharge.AddEffect(CardEffectType.Damage, 3, CardTargetType.Self);
        
        // Card 9: Endless Hunger
        var endlessHunger = CreateCardAsset("Draft/BeastEndlessHunger", "Endless Hunger", "Gain 1 Strength. This effect doubles each time played this fight.");
        SetCardType(endlessHunger, CardType.Skill, 1);
        endlessHunger.AddEffect(CardEffectType.ApplyStrength, 1, CardTargetType.Self);
        AddScalingToCard(endlessHunger, ScalingType.CardsPlayedThisFight, 0.5f, 8);
        
        // Card 10: King of Beasts
        var kingOfBeasts = CreateCardAsset("Draft/BeastKingOfBeasts", "King of Beasts", "Permanently gain: 'All attacks deal +2 damage and heal 1 health this fight.'");
        SetCardType(kingOfBeasts, CardType.Skill, 4);
        kingOfBeasts.AddEffect(CardEffectType.ApplyStrength, 2, CardTargetType.Self);
        
        Debug.Log("🐺 Generated 10 Beast draft cards");
    }
    
    private void GenerateElementalDraftCards()
    {
        // Elemental Draft Cards - Status Effects and Elemental Magic
        
        // Card 1: Lightning Bolt
        var lightningBolt = CreateCardAsset("Draft/ElementalLightningBolt", "Lightning Bolt", "Deal 9 damage. If target is Weak, also Stun for 1 turn.");
        SetCardType(lightningBolt, CardType.Spell, 2);
        lightningBolt.AddConditionalEffectAND(CardEffectType.Damage, 9, CardTargetType.Opponent,
                                            ConditionalType.IfTargetHealthBelow, 100, CardEffectType.ApplyStun, 1);
        
        // Card 2: Toxic Cloud
        var toxicCloud = CreateCardAsset("Draft/ElementalToxicCloud", "Toxic Cloud", "Apply 5 damage over time for 3 turns to all enemies.");
        SetCardType(toxicCloud, CardType.Spell, 3);
        toxicCloud.AddEffect(CardEffectType.ApplyDamageOverTime, 5, CardTargetType.AllEnemies, 3);
        
        // Card 3: Elemental Fusion
        var elementalFusion = CreateCardAsset("Draft/ElementalFusion", "Elemental Fusion", "Deal 6 damage. Apply random status effect for 2 turns.");
        SetCardType(elementalFusion, CardType.Spell, 2);
        elementalFusion.SetupBasicDamageCard("Elemental Fusion", 6, 2);
        elementalFusion.AddEffect(CardEffectType.ApplyWeak, 0, CardTargetType.Opponent, 2);
        
        // Card 4: Storm Shield
        var stormShield = CreateCardAsset("Draft/ElementalStormShield", "Storm Shield", "Gain 8 Shield. Next attacker takes 5 damage and gets Stunned.");
        SetCardType(stormShield, CardType.Skill, 2);
        stormShield.AddEffect(CardEffectType.ApplyShield, 8, CardTargetType.Self);
        stormShield.AddEffect(CardEffectType.ApplyThorns, 5, CardTargetType.Self);
        
        // Card 5: Chaos Magic
        var chaosMagic = CreateCardAsset("Draft/ElementalChaosMagic", "Chaos Magic", "Deal random damage between 3-15. Apply random debuff for 1-3 turns.");
        SetCardType(chaosMagic, CardType.Spell, 2);
        chaosMagic.SetupBasicDamageCard("Chaos Magic", 9, 2); // Average damage
        chaosMagic.AddEffect(CardEffectType.ApplyBreak, 0, CardTargetType.Opponent, 2);
        
        // Card 6: Elemental Mastery
        var elementalMastery = CreateCardAsset("Draft/ElementalMastery", "Elemental Mastery", "Status effects you apply last 1 additional turn this fight.");
        SetCardType(elementalMastery, CardType.Skill, 2);
        elementalMastery.AddEffect(CardEffectType.ApplyStrength, 1, CardTargetType.Self); // Simulating mastery
        
        // Card 7: Inferno
        var inferno = CreateCardAsset("Draft/ElementalInferno", "Inferno", "Deal 4 damage to all enemies. Apply 2 damage over time for 4 turns.");
        SetCardType(inferno, CardType.Spell, 3);
        inferno.AddEffect(CardEffectType.Damage, 4, CardTargetType.AllEnemies);
        inferno.AddEffect(CardEffectType.ApplyDamageOverTime, 2, CardTargetType.AllEnemies, 4);
        
        // Card 8: Dispelling Wind
        var dispellingWind = CreateCardAsset("Draft/ElementalDispellingWind", "Dispelling Wind", "Remove all status effects from all characters. Deal 3 damage for each removed.");
        SetCardType(dispellingWind, CardType.Spell, 2);
        dispellingWind.SetupBasicDamageCard("Dispelling Wind", 6, 2, CardTargetType.AllEnemies);
        
        // Card 9: Elemental Rebirth
        var elementalRebirth = CreateCardAsset("Draft/ElementalRebirth", "Elemental Rebirth", "If you have 3+ debuffs, remove them all and gain 15 Shield.");
        SetCardType(elementalRebirth, CardType.Skill, 1);
        elementalRebirth.AddConditionalEffectOR(CardEffectType.ApplyShield, 0, CardTargetType.Self,
                                              ConditionalType.IfSourceHealthBelow, 50, CardEffectType.ApplyShield, 15);
        
        // Card 10: Avatar of Elements
        var avatarOfElements = CreateCardAsset("Draft/ElementalAvatar", "Avatar of Elements", "Transform: Gain immunity to status effects and +5 damage this fight.");
        SetCardType(avatarOfElements, CardType.Skill, 4);
        avatarOfElements.AddEffect(CardEffectType.ApplyStrength, 5, CardTargetType.Self);
        
        Debug.Log("⚡ Generated 10 Elemental draft cards");
    }
    
    private void GenerateGuardianDraftCards()
    {
        // Guardian Draft Cards - Defensive Mastery and Protection
        
        // Card 1: Fortress
        var fortress = CreateCardAsset("Draft/GuardianFortress", "Fortress", "Gain 15 Shield. Heal 2 health per turn for 5 turns.");
        SetCardType(fortress, CardType.Skill, 3);
        fortress.AddEffect(CardEffectType.ApplyShield, 15, CardTargetType.Self);
        fortress.AddEffect(CardEffectType.ApplyHealOverTime, 2, CardTargetType.Self, 5);
        
        // Card 2: Protective Barrier
        var protectiveBarrier = CreateCardAsset("Draft/GuardianProtectiveBarrier", "Protective Barrier", "All allies gain 8 Shield. You gain additional Shield equal to allies protected.");
        SetCardType(protectiveBarrier, CardType.Skill, 2);
        protectiveBarrier.AddEffect(CardEffectType.ApplyShield, 8, CardTargetType.AllAllies);
        protectiveBarrier.AddEffect(CardEffectType.ApplyShield, 5, CardTargetType.Self);
        
        // Card 3: Counter Attack
        var counterAttack = CreateCardAsset("Draft/GuardianCounterAttack", "Counter Attack", "Gain 6 Shield. Deal 8 damage to next attacker.");
        SetCardType(counterAttack, CardType.Skill, 2);
        counterAttack.AddEffect(CardEffectType.ApplyShield, 6, CardTargetType.Self);
        counterAttack.AddEffect(CardEffectType.ApplyThorns, 8, CardTargetType.Self);
        
        // Card 4: Life Link
        var lifeLink = CreateCardAsset("Draft/GuardianLifeLink", "Life Link", "Heal 6 health. All allies heal 3 health.");
        SetCardType(lifeLink, CardType.Skill, 2);
        lifeLink.SetupBasicHealCard("Life Link", 6, 2);
        lifeLink.AddEffect(CardEffectType.Heal, 3, CardTargetType.AllAllies);
        
        // Card 5: Stalwart Defense
        var stalwartDefense = CreateCardAsset("Draft/GuardianStalwartDefense", "Stalwart Defense", "Gain Shield equal to 8 + damage taken this turn.");
        SetCardType(stalwartDefense, CardType.Skill, 1);
        stalwartDefense.AddEffect(CardEffectType.ApplyShield, 8, CardTargetType.Self);
        AddScalingToCard(stalwartDefense, ScalingType.DamageDealtThisTurn, 1.0f, 20);
        
        // Card 6: Guardian's Oath
        var guardiansOath = CreateCardAsset("Draft/GuardianOath", "Guardian's Oath", "Permanently gain: 'Heal 1 health whenever you gain Shield.'");
        SetCardType(guardiansOath, CardType.Skill, 2);
        guardiansOath.AddEffect(CardEffectType.ApplyHealOverTime, 1, CardTargetType.Self, 99);
        
        // Card 7: Shield Wall
        var shieldWall = CreateCardAsset("Draft/GuardianShieldWall", "Shield Wall", "Gain 20 Shield. Cannot attack next turn.");
        SetCardType(shieldWall, CardType.Skill, 2);
        shieldWall.AddEffect(CardEffectType.ApplyShield, 20, CardTargetType.Self);
        
        // Card 8: Retribution
        var retribution = CreateCardAsset("Draft/GuardianRetribution", "Retribution", "Deal damage to all enemies equal to Shield lost this turn.");
        SetCardType(retribution, CardType.Attack, 2);
        retribution.SetupBasicDamageCard("Retribution", 5, 2, CardTargetType.AllEnemies);
        AddScalingToCard(retribution, ScalingType.DamageDealtThisTurn, 0.5f, 15);
        
        // Card 9: Healing Sanctuary
        var healingSanctuary = CreateCardAsset("Draft/GuardianHealingSanctuary", "Healing Sanctuary", "All characters heal 5 health. You heal additional health equal to your Shield.");
        SetCardType(healingSanctuary, CardType.Skill, 3);
        healingSanctuary.AddEffect(CardEffectType.Heal, 5, CardTargetType.Everyone);
        healingSanctuary.AddEffect(CardEffectType.Heal, 5, CardTargetType.Self);
        
        // Card 10: Immortal Guardian
        var immortalGuardian = CreateCardAsset("Draft/GuardianImmortal", "Immortal Guardian", "Transform: Cannot die this fight (minimum 1 health). Gain 10 Shield per turn.");
        SetCardType(immortalGuardian, CardType.Skill, 5);
        immortalGuardian.AddEffect(CardEffectType.ApplyHealOverTime, 5, CardTargetType.Self, 10);
        immortalGuardian.AddEffect(CardEffectType.ApplyShield, 10, CardTargetType.Self);
        
        Debug.Log("🛡️ Generated 10 Guardian draft cards");
    }
    
    #endregion
    
    #region Upgraded Cards Generation
    
    private void GenerateAllUpgradedCards()
    {
        // Ensure directories exist
        Directory.CreateDirectory(CARDS_FOLDER + "Upgraded/");
        
        Debug.Log("🔧 Starting upgrade generation...");
        
        // Find all existing cards to upgrade
        string[] cardAssets = AssetDatabase.FindAssets("t:CardData", new[] { CARDS_FOLDER });
        List<CardData> cardsToUpgrade = new List<CardData>();
        
        foreach (string guid in cardAssets)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            // Skip already upgraded cards
            if (path.Contains("/Upgraded/")) continue;
            
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (card != null)
            {
                cardsToUpgrade.Add(card);
                Debug.Log($"Found card to upgrade: {card.CardName} (asset: {card.name})");
            }
        }
        
        Debug.Log($"Found {cardsToUpgrade.Count} cards to upgrade");
        
        // Generate upgraded versions
        foreach (CardData originalCard in cardsToUpgrade)
        {
            GenerateUpgradedCard(originalCard);
        }
        
        // Force save all created assets
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // Force dirty all CardData assets to ensure links are saved
        string[] allCardAssets = AssetDatabase.FindAssets("t:CardData", new[] { CARDS_FOLDER });
        foreach (string guid in allCardAssets)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (card != null)
            {
                EditorUtility.SetDirty(card);
            }
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log($"✅ Generated {cardsToUpgrade.Count} upgraded cards!");
    }
    
    private void GenerateUpgradedCard(CardData originalCard)
    {
        string originalName = originalCard.CardName;
        
        // Clean up the filename - remove "Draft/" prefix and any path separators
        string cleanFileName = originalCard.name.Replace("Draft/", "").Replace("/", "_");
        string upgradedFileName = "Upgraded/" + cleanFileName + "_Upgraded";
        string upgradedName = originalName + "+";
        
        Debug.Log($"Creating upgrade: {originalName} → {upgradedName} (file: {upgradedFileName})");
        
        // Create the upgraded card
        var upgradedCard = CreateCardAsset(upgradedFileName, upgradedName, GetUpgradedDescription(originalCard));
        
        // Copy basic properties
        SetCardType(upgradedCard, originalCard.CardType, GetUpgradedEnergyCost(originalCard));
        
        // Copy and upgrade effects
        CopyAndUpgradeEffects(originalCard, upgradedCard);
        
        // Copy combo properties if they exist
        if (originalCard.BuildsCombo)
        {
            upgradedCard.MakeComboCard();
        }
        
        if (originalCard.RequiresCombo)
        {
            upgradedCard.RequireCombo(originalCard.RequiredComboAmount);
        }
        
        SaveCardAsset(upgradedCard);
        
        // Link the original card to its upgraded version
        LinkCardToUpgrade(originalCard, upgradedCard);
        
        Debug.Log($"🔧 Upgraded: {originalName} → {upgradedName}");
    }
    
    private void LinkCardToUpgrade(CardData originalCard, CardData upgradedCard)
    {
        // Use reflection to set the upgraded version field
        var upgradedVersionField = typeof(CardData).GetField("_upgradedVersion", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        upgradedVersionField?.SetValue(originalCard, upgradedCard);
        
        EditorUtility.SetDirty(originalCard);
    }
    
    private string GetUpgradedDescription(CardData originalCard)
    {
        string baseDescription = originalCard.Description;
        
        // Add upgrade indicators to description
        if (baseDescription.Contains("Deal ") && baseDescription.Contains(" damage"))
        {
            // Upgrade damage descriptions
            return baseDescription.Replace("Deal ", "Deal ").Replace(" damage", " damage") + " [UPGRADED]";
        }
        else if (baseDescription.Contains("Gain ") && baseDescription.Contains(" Shield"))
        {
            // Upgrade shield descriptions
            return baseDescription + " [UPGRADED]";
        }
        else if (baseDescription.Contains("Heal "))
        {
            // Upgrade healing descriptions
            return baseDescription + " [UPGRADED]";
        }
        
        return baseDescription + " [UPGRADED]";
    }
    
    private int GetUpgradedEnergyCost(CardData originalCard)
    {
        // Most upgrades reduce cost by 1 (minimum 0)
        int originalCost = originalCard.EnergyCost;
        
        // Don't reduce 0-cost cards or 1-cost cards (keep them playable)
        if (originalCost <= 1) return originalCost;
        
        return originalCost - 1;
    }
    
    private void CopyAndUpgradeEffects(CardData originalCard, CardData upgradedCard)
    {
        if (!originalCard.HasEffects) return;
        
        foreach (var originalEffect in originalCard.Effects)
        {
            var upgradedEffect = new CardEffect
            {
                effectType = originalEffect.effectType,
                amount = GetUpgradedEffectAmount(originalEffect),
                targetType = originalEffect.targetType,
                duration = GetUpgradedDuration(originalEffect),
                elementalType = originalEffect.elementalType,
                
                // Copy conditional effects
                conditionType = originalEffect.conditionType,
                conditionValue = originalEffect.conditionValue,
                alternativeEffectType = originalEffect.alternativeEffectType,
                alternativeEffectAmount = GetUpgradedEffectAmount(originalEffect, true),
                
                // Copy scaling
                scalingType = originalEffect.scalingType,
                scalingMultiplier = originalEffect.scalingMultiplier * 1.2f, // 20% better scaling
                maxScaling = originalEffect.maxScaling + 5,
                
                // Copy animation settings
                animationBehavior = originalEffect.animationBehavior,
                animationDelay = originalEffect.animationDelay
            };
            
            // Add the upgraded effect
            var effectsField = typeof(CardData).GetField("_effects", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var effects = effectsField?.GetValue(upgradedCard) as System.Collections.Generic.List<CardEffect>;
            
            if (effects != null)
            {
                effects.Add(upgradedEffect);
            }
        }
    }
    
    private int GetUpgradedEffectAmount(CardEffect originalEffect, bool isAlternative = false)
    {
        int originalAmount = isAlternative ? originalEffect.alternativeEffectAmount : originalEffect.amount;
        
        switch (originalEffect.effectType)
        {
            case CardEffectType.Damage:
                // Damage gets +2-4 boost depending on original amount
                if (originalAmount <= 5) return originalAmount + 2;
                if (originalAmount <= 10) return originalAmount + 3;
                return originalAmount + 4;
                
            case CardEffectType.Heal:
                // Healing gets +2-3 boost
                if (originalAmount <= 8) return originalAmount + 2;
                return originalAmount + 3;
                
            case CardEffectType.ApplyShield:
                // Shield gets +2-3 boost
                if (originalAmount <= 8) return originalAmount + 2;
                return originalAmount + 3;
                
            case CardEffectType.ApplyStrength:
            case CardEffectType.ApplyThorns:
                // Status effects get +1 boost
                return originalAmount + 1;
                
            case CardEffectType.RestoreEnergy:
            case CardEffectType.DrawCard:
                // Utility effects get +1 boost
                return originalAmount + 1;
                
            default:
                // Default: +1 for most effects
                return originalAmount + 1;
        }
    }
    
    private int GetUpgradedDuration(CardEffect originalEffect)
    {
        // Duration effects get +1 turn (if they have duration)
        if (originalEffect.duration > 0)
        {
            return originalEffect.duration + 1;
        }
        
        return originalEffect.duration;
    }
    
    #endregion
} 