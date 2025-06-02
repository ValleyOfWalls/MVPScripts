using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Simple builder for creating common card types without getting overwhelmed by options.
/// Use this for 90% of your card creation needs.
/// </summary>
[System.Serializable]
public class SimpleCardBuilder
{
    [Header("═══ CARD TEMPLATES ═══")]
    [Space(5)]
    
    [Header("Quick Actions")]
    [SerializeField] private string cardName = "New Card";
    [SerializeField] private int energyCost = 2;
    [SerializeField] private string description = "";
    
    // Public property to access cardName from editor window
    public string CardName => cardName;
    
    [Space(10)]
    [Header("═══ COMMON CARD TYPES ═══")]
    
    [Header("Basic Attack Cards")]
    public bool createBasicAttack;
    [SerializeField] private int attackDamage = 4;
    
    [Header("Basic Defense Cards")]
    public bool createBasicDefense;
    [SerializeField] private int shieldAmount = 6;
    
    [Header("Healing Cards")]
    public bool createHealCard;
    [SerializeField] private int healAmount = 5;
    
    [Header("Status Effect Cards")]
    public bool createStatusCard;
    [SerializeField] private StatusCardType statusType = StatusCardType.Weak;
    [SerializeField] private int statusPotency = 2;
    [SerializeField] private int statusDuration = 3;
    
    [Header("Utility Cards")]
    public bool createUtilityCard;
    [SerializeField] private UtilityCardType utilityType = UtilityCardType.DrawCards;
    [SerializeField] private int utilityAmount = 2;
    
    [Space(10)]
    [Header("═══ ADVANCED VARIANTS ═══")]
    
    [Header("Combo System")]
    [SerializeField] private bool makeComboCard = false;
    [SerializeField] private bool makeFinisher = false;
    
    [ConditionalField("makeFinisher", true, true)]
    [SerializeField] private int requiredComboAmount = 1;
    
    [Header("Elemental")]
    [SerializeField] private ElementalType elementalType = ElementalType.None;
    
    [Header("Scaling")]
    [SerializeField] private bool addScaling = false;
    [SerializeField] private ScalingCardType scalingType = ScalingCardType.ScaleWithZeroCost;
    
    [Header("Multiple Effects")]
    [SerializeField] private bool addSecondaryEffect = false;
    [SerializeField] private SecondaryEffectType secondaryEffect = SecondaryEffectType.HealSelf;
    [SerializeField] private int secondaryAmount = 2;

    public enum StatusCardType
    {
        Weak,
        Break, 
        Stun,
        Thorns,
        Poison
    }
    
    public enum UtilityCardType
    {
        DrawCards,
        RestoreEnergy,
        DiscardEnemyCards
    }
    
    public enum ScalingCardType
    {
        ScaleWithZeroCost,
        ScaleWithCombo,
        ScaleWithCardsPlayed,
        ScaleWithDamageDealt
    }
    
    public enum SecondaryEffectType
    {
        HealSelf,
        DrawCard,
        GainEnergy,
        DamageAll
    }

    /// <summary>
    /// Creates a card based on the current settings
    /// </summary>
    public CardData BuildCard()
    {
        CardData newCard = ScriptableObject.CreateInstance<CardData>();
        
        // Basic setup
        newCard.SetPrivateField("_cardName", cardName);
        newCard.SetPrivateField("_energyCost", energyCost);
        newCard.SetPrivateField("_description", string.IsNullOrEmpty(description) ? GenerateDescription() : description);
        
        // Apply elemental type
        if (elementalType != ElementalType.None)
        {
            newCard.SetPrivateField("_elementalType", elementalType);
        }
        
        // Build specific card type
        if (createBasicAttack)
        {
            newCard.SetupBasicDamageCard(cardName, attackDamage, energyCost);
        }
        else if (createBasicDefense)
        {
            SetupShieldCard(newCard);
        }
        else if (createHealCard)
        {
            newCard.SetupBasicHealCard(cardName, healAmount, energyCost);
        }
        else if (createStatusCard)
        {
            SetupStatusEffectCard(newCard);
        }
        else if (createUtilityCard)
        {
            SetupUtilityCard(newCard);
        }
        else
        {
            // Default to basic attack
            newCard.SetupBasicDamageCard(cardName, 3, energyCost);
        }
        
        // Apply advanced features
        if (makeComboCard)
        {
            newCard.MakeComboCard();
        }
        
        if (makeFinisher)
        {
            newCard.RequireCombo(requiredComboAmount);
        }
        
        if (addScaling)
        {
            ApplyScaling(newCard);
        }
        
        if (addSecondaryEffect)
        {
            ApplySecondaryEffect(newCard);
        }
        
        return newCard;
    }
    
    private void SetupShieldCard(CardData card)
    {
        card.SetupStatusCard(cardName, CardEffectType.ApplyShield, shieldAmount, 0, energyCost);
    }
    
    private void SetupStatusEffectCard(CardData card)
    {
        CardEffectType effectType = ConvertStatusType(statusType);
        card.SetupStatusCard(cardName, effectType, statusPotency, statusDuration, energyCost);
    }
    
    private void SetupUtilityCard(CardData card)
    {
        switch (utilityType)
        {
            case UtilityCardType.DrawCards:
                card.SetPrivateField("_cardType", CardType.Skill);
                card.SetPrivateField("_effectType", CardEffectType.DrawCard);
                card.SetPrivateField("_targetType", CardTargetType.Self);
                card.SetPrivateField("_amount", utilityAmount);
                break;
                
            case UtilityCardType.RestoreEnergy:
                card.SetPrivateField("_cardType", CardType.Skill);
                card.SetPrivateField("_effectType", CardEffectType.RestoreEnergy);
                card.SetPrivateField("_targetType", CardTargetType.Self);
                card.SetPrivateField("_amount", utilityAmount);
                break;
                
            case UtilityCardType.DiscardEnemyCards:
                card.SetPrivateField("_cardType", CardType.Skill);
                card.SetPrivateField("_effectType", CardEffectType.DiscardRandomCards);
                card.SetPrivateField("_targetType", CardTargetType.Opponent);
                card.SetPrivateField("_amount", utilityAmount);
                break;
        }
    }
    
    private void ApplyScaling(CardData card)
    {
        ScalingType scaling;
        float multiplier;
        
        switch (scalingType)
        {
            case ScalingCardType.ScaleWithZeroCost:
                scaling = ScalingType.ZeroCostCardsThisTurn;
                multiplier = 1.0f;
                break;
            case ScalingCardType.ScaleWithCombo:
                scaling = ScalingType.ComboCount;
                multiplier = 1.5f;
                break;
            case ScalingCardType.ScaleWithCardsPlayed:
                scaling = ScalingType.CardsPlayedThisTurn;
                multiplier = 0.5f;
                break;
            case ScalingCardType.ScaleWithDamageDealt:
                scaling = ScalingType.DamageDealtThisFight;
                multiplier = 0.1f;
                break;
            default:
                scaling = ScalingType.None;
                multiplier = 1.0f;
                break;
        }
        
        if (scaling != ScalingType.None)
        {
            card.AddScaling(scaling, multiplier);
        }
    }
    
    private void ApplySecondaryEffect(CardData card)
    {
        switch (secondaryEffect)
        {
            case SecondaryEffectType.HealSelf:
                card.AddAdditionalEffect(CardEffectType.Heal, secondaryAmount, CardTargetType.Self);
                break;
            case SecondaryEffectType.DrawCard:
                card.AddAdditionalEffect(CardEffectType.DrawCard, secondaryAmount, CardTargetType.Self);
                break;
            case SecondaryEffectType.GainEnergy:
                card.AddAdditionalEffect(CardEffectType.RestoreEnergy, secondaryAmount, CardTargetType.Self);
                break;
            case SecondaryEffectType.DamageAll:
                card.AddAdditionalEffect(CardEffectType.Damage, secondaryAmount, CardTargetType.AllEnemies);
                break;
        }
    }
    
    private CardEffectType ConvertStatusType(StatusCardType statusType)
    {
        switch (statusType)
        {
            case StatusCardType.Weak:
                return CardEffectType.ApplyWeak;
            case StatusCardType.Break:
                return CardEffectType.ApplyBreak;
            case StatusCardType.Stun:
                return CardEffectType.ApplyStun;
            case StatusCardType.Thorns:
                return CardEffectType.ApplyThorns;
            case StatusCardType.Poison:
                return CardEffectType.ApplyDamageOverTime;
            default:
                return CardEffectType.ApplyWeak;
        }
    }
    
    private string GenerateDescription()
    {
        if (createBasicAttack)
            return $"Deal {attackDamage} damage";
        if (createBasicDefense)
            return $"Gain {shieldAmount} Shield";
        if (createHealCard)
            return $"Heal {healAmount} health";
        if (createStatusCard)
            return $"Apply {statusType} {statusPotency} for {statusDuration} turns";
        if (createUtilityCard)
            return $"{utilityType} {utilityAmount}";
            
        return "Basic card effect";
    }
}

/// <summary>
/// Custom property drawer to make the builder more user-friendly
/// </summary>
[CustomPropertyDrawer(typeof(SimpleCardBuilder))]
public class SimpleCardBuilderDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        
        // Create a foldout for the builder
        property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), 
            property.isExpanded, "Simple Card Builder", true);
        
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            
            // Draw the properties
            EditorGUI.PropertyField(position, property, true);
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUI.EndProperty();
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property.isExpanded)
        {
            return EditorGUI.GetPropertyHeight(property, true);
        }
        return EditorGUIUtility.singleLineHeight;
    }
}

/// <summary>
/// Editor window for the simple card builder
/// </summary>
public class SimpleCardBuilderWindow : EditorWindow
{
    private SimpleCardBuilder builder = new SimpleCardBuilder();
    private Vector2 scrollPosition;
    
    [MenuItem("Tools/Card Builder/Simple Card Builder")]
    public static void ShowWindow()
    {
        SimpleCardBuilderWindow window = GetWindow<SimpleCardBuilderWindow>("Simple Card Builder");
        window.minSize = new Vector2(400, 600);
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Simple Card Builder", EditorStyles.boldLabel);
        GUILayout.Label("Create cards quickly without getting overwhelmed by options", EditorStyles.helpBox);
        
        EditorGUILayout.Space();
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        // Serialize the builder for the inspector
        SerializedObject serializedBuilder = new SerializedObject(this);
        SerializedProperty builderProperty = serializedBuilder.FindProperty("builder");
        
        EditorGUILayout.PropertyField(builderProperty, true);
        serializedBuilder.ApplyModifiedProperties();
        
        EditorGUILayout.EndScrollView();
        
        EditorGUILayout.Space();
        
        // Build button
        if (GUILayout.Button("Build Card", GUILayout.Height(30)))
        {
            BuildAndSaveCard();
        }
    }
    
    private void BuildAndSaveCard()
    {
        CardData newCard = builder.BuildCard();
        
        // Save the card as an asset
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Card",
            builder.CardName + ".asset",
            "asset",
            "Choose where to save the card");
            
        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(newCard, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // Select the new card
            Selection.activeObject = newCard;
            EditorGUIUtility.PingObject(newCard);
            
            Debug.Log($"Created card: {newCard.CardName} at {path}");
        }
    }
}

#endif

/// <summary>
/// Extension methods for CardData to access private fields
/// </summary>
public static class CardDataExtensions
{
    public static void SetPrivateField(this CardData cardData, string fieldName, object value)
    {
        var field = typeof(CardData).GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(cardData, value);
        }
        else
        {
            Debug.LogWarning($"Field {fieldName} not found on CardData");
        }
    }
} 