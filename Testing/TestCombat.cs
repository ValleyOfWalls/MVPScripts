using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;

#if UNITY_EDITOR

/// <summary>
/// Testing utilities for the combat system with debug commands and card spawning tools.
/// </summary>
public class TestCombat : MonoBehaviour
{
    [Header("Test Mode Settings")]
    [SerializeField] private bool enableReturnToHandMode = false;
    [SerializeField] private bool showDebugLogs = true;
    
    private static TestCombat instance;
    public static TestCombat Instance => instance;
    
    // Card return mode tracking
    private Dictionary<GameObject, CardLocation> originalCardLocations = new Dictionary<GameObject, CardLocation>();
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    #region Menu Commands
    
    [MenuItem("Tools/Test Combat/Open Card Spawner Window")]
    public static void OpenCardSpawnerWindow()
    {
        CardSpawnerWindow.ShowWindow();
    }
    
    [MenuItem("Tools/Test Combat/Toggle Return To Hand Mode")]
    public static void ToggleReturnToHandMode()
    {
        if (Instance != null)
        {
            Instance.enableReturnToHandMode = !Instance.enableReturnToHandMode;
            Debug.Log($"Return to Hand Mode: {(Instance.enableReturnToHandMode ? "ENABLED" : "DISABLED")}");
        }
        else
        {
            Debug.LogWarning("TestCombat instance not found in scene. Add TestCombat component to a GameObject.");
        }
    }
    
    [MenuItem("Tools/Test Combat/Spawn Random Test Cards in Player Hand")]
    public static void SpawnRandomTestCards()
    {
        NetworkEntity localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            Instance?.SpawnRandomCardsInHand(localPlayer, 3);
        }
        else
        {
            Debug.LogWarning("No local player found");
        }
    }
    
    [MenuItem("Tools/Test Combat/Clear Player Hand")]
    public static void ClearPlayerHand()
    {
        NetworkEntity localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            Instance?.ClearEntityHand(localPlayer);
        }
        else
        {
            Debug.LogWarning("No local player found");
        }
    }
    
    [MenuItem("Tools/Test Combat/Reset All Entity Trackers")]
    public static void ResetAllEntityTrackers()
    {
        EntityTracker[] trackers = FindObjectsByType<EntityTracker>(FindObjectsSortMode.None);
        foreach (var tracker in trackers)
        {
            if (tracker.IsServerInitialized)
            {
                tracker.ResetForNewFight();
                Debug.Log($"Reset EntityTracker for {tracker.gameObject.name}");
            }
        }
    }
    
    [MenuItem("Tools/Test Combat/Log All Combat State")]
    public static void LogAllCombatState()
    {
        Instance?.LogCombatState();
    }
    
    #endregion
    
    #region Card Return to Hand Mode
    
    /// <summary>
    /// Intercepts card play to make cards return to hand instead of discarding
    /// Call this from HandleCardPlay before normal processing
    /// </summary>
    public bool ShouldReturnToHand(GameObject cardObject)
    {
        if (!enableReturnToHandMode) return false;
        
        // Store original location if not already stored
        Card card = cardObject.GetComponent<Card>();
        if (card != null && !originalCardLocations.ContainsKey(cardObject))
        {
            originalCardLocations[cardObject] = card.CurrentContainer;
        }
        
        if (showDebugLogs)
            Debug.Log($"TestCombat: Card {cardObject.name} will return to hand after play");
        
        return true;
    }
    
    /// <summary>
    /// Returns a card to hand after it was played (for testing mode)
    /// </summary>
    public void ReturnCardToHand(GameObject cardObject)
    {
        if (!enableReturnToHandMode) return;
        
        Card card = cardObject.GetComponent<Card>();
        if (card == null) return;
        
        // Find the hand manager for the card's owner
        NetworkEntity owner = card.OwnerEntity;
        if (owner == null) return;
        
        HandManager handManager = GetHandManagerForEntity(owner);
        if (handManager == null) return;
        
        // Force the card back to hand
        if (handManager.IsServerInitialized)
        {
            // Use reflection to call the private MoveCardToHand method
            var moveToHandMethod = typeof(HandManager).GetMethod("MoveCardToHand", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (moveToHandMethod != null)
            {
                moveToHandMethod.Invoke(handManager, new object[] { cardObject });
                if (showDebugLogs)
                    Debug.Log($"TestCombat: Returned {cardObject.name} to hand");
            }
        }
    }
    
    #endregion
    
    #region Card Spawning Utilities
    
    /// <summary>
    /// Spawns a specific card in the target entity's hand
    /// </summary>
    public void SpawnCardInHand(NetworkEntity targetEntity, CardData cardData)
    {
        if (targetEntity == null || cardData == null)
        {
            Debug.LogError("TestCombat: Cannot spawn card - target entity or card data is null");
            return;
        }
        
        HandManager handManager = GetHandManagerForEntity(targetEntity);
        if (handManager == null)
        {
            Debug.LogError($"TestCombat: No HandManager found for entity {targetEntity.EntityName.Value}");
            return;
        }
        
        // Get the CardSpawner from the hand entity
        CardSpawner cardSpawner = handManager.GetComponent<CardSpawner>();
        if (cardSpawner == null)
        {
            Debug.LogError($"TestCombat: No CardSpawner found on HandManager for {targetEntity.EntityName.Value}");
            return;
        }
        
        if (cardSpawner.IsServerInitialized)
        {
            GameObject spawnedCard = cardSpawner.SpawnCard(cardData);
            if (spawnedCard != null)
            {
                // Move the card to hand using reflection to access private method
                var moveToHandMethod = typeof(HandManager).GetMethod("MoveCardToHand", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (moveToHandMethod != null)
                {
                    moveToHandMethod.Invoke(handManager, new object[] { spawnedCard });
                    Debug.Log($"TestCombat: Spawned {cardData.CardName} in {targetEntity.EntityName.Value}'s hand");
                }
            }
        }
        else
        {
            Debug.LogWarning("TestCombat: CardSpawner is not server initialized");
        }
    }
    
    /// <summary>
    /// Spawns random test cards in an entity's hand
    /// </summary>
    public void SpawnRandomCardsInHand(NetworkEntity targetEntity, int count)
    {
        if (CardDatabase.Instance == null)
        {
            Debug.LogError("TestCombat: CardDatabase instance not found");
            return;
        }
        
        List<CardData> allCards = CardDatabase.Instance.GetAllCards();
        List<CardData> testCards = allCards.Where(card => card.CardName.StartsWith("Test_")).ToList();
        
        if (testCards.Count == 0)
        {
            Debug.LogWarning("TestCombat: No test cards found. Create test cards first using CardFactory.");
            return;
        }
        
        for (int i = 0; i < count; i++)
        {
            CardData randomCard = testCards[Random.Range(0, testCards.Count)];
            SpawnCardInHand(targetEntity, randomCard);
        }
    }
    
    /// <summary>
    /// Clears all cards from an entity's hand
    /// </summary>
    public void ClearEntityHand(NetworkEntity targetEntity)
    {
        HandManager handManager = GetHandManagerForEntity(targetEntity);
        if (handManager != null && handManager.IsServerInitialized)
        {
            handManager.DiscardHand();
            Debug.Log($"TestCombat: Cleared hand for {targetEntity.EntityName.Value}");
        }
    }
    
    #endregion
    
    #region Debug Utilities
    
    /// <summary>
    /// Logs comprehensive combat state information
    /// </summary>
    public void LogCombatState()
    {
        Debug.Log("=== COMBAT STATE DEBUG ===");
        
        // Find all entities
        NetworkEntity[] entities = FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        
        foreach (var entity in entities)
        {
            if (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet)
            {
                LogEntityState(entity);
            }
        }
        
        // Log combat manager state
        CombatManager combatManager = FindFirstObjectByType<CombatManager>();
        if (combatManager != null)
        {
            Debug.Log($"CombatManager found: {combatManager.gameObject.name}");
        }
        else
        {
            Debug.Log("CombatManager not found");
        }
        
        Debug.Log("=== END COMBAT STATE ===");
    }
    
    /// <summary>
    /// Logs detailed state for a specific entity
    /// </summary>
    private void LogEntityState(NetworkEntity entity)
    {
        Debug.Log($"--- Entity: {entity.EntityName.Value} ({entity.EntityType}) ---");
        Debug.Log($"Health: {entity.CurrentHealth.Value}/{entity.MaxHealth.Value}");
        Debug.Log($"IsOwner: {entity.IsOwner}");
        
        // EntityTracker state
        EntityTracker tracker = entity.GetComponent<EntityTracker>();
        if (tracker != null)
        {
            var trackingData = tracker.GetTrackingDataForScaling();
            Debug.Log($"Cards played this turn: {trackingData.cardsPlayedThisTurn}");
            Debug.Log($"Cards played this fight: {trackingData.cardsPlayedThisFight}");
            Debug.Log($"Zero cost cards this turn: {trackingData.zeroCostCardsThisTurn}");
            Debug.Log($"Combo count: {trackingData.comboCount}");
            Debug.Log($"Current stance: {tracker.CurrentStance}");
            Debug.Log($"Strength stacks: {tracker.StrengthStacks}");
            Debug.Log($"Is stunned: {tracker.IsStunned}");
        }
        
        // Hand state
        HandManager handManager = GetHandManagerForEntity(entity);
        if (handManager != null)
        {
            List<GameObject> cardsInHand = handManager.GetCardsInHand();
            List<GameObject> cardsInDeck = handManager.GetCardsInDeck();
            List<GameObject> cardsInDiscard = handManager.GetCardsInDiscard();
            
            Debug.Log($"Cards in hand: {cardsInHand.Count}");
            Debug.Log($"Cards in deck: {cardsInDeck.Count}");
            Debug.Log($"Cards in discard: {cardsInDiscard.Count}");
            
            // Log card names in hand
            if (cardsInHand.Count > 0)
            {
                string handCards = "Hand cards: ";
                foreach (var cardObj in cardsInHand)
                {
                    Card card = cardObj.GetComponent<Card>();
                    if (card?.CardData != null)
                    {
                        handCards += card.CardData.CardName + ", ";
                    }
                }
                Debug.Log(handCards.TrimEnd(',', ' '));
            }
        }
        
        // Energy state
        if (entity.CurrentEnergy != null && entity.MaxEnergy != null)
        {
            Debug.Log($"Energy: {entity.CurrentEnergy.Value}/{entity.MaxEnergy.Value}");
        }
        
        Debug.Log($"--- End {entity.EntityName.Value} ---");
    }
    
    #endregion
    
    #region Helper Methods
    
    private static NetworkEntity GetLocalPlayer()
    {
        NetworkEntity[] entities = FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        foreach (NetworkEntity entity in entities)
        {
            if (entity.EntityType == EntityType.Player && entity.IsOwner)
            {
                return entity;
            }
        }
        return null;
    }
    
    private HandManager GetHandManagerForEntity(NetworkEntity entity)
    {
        if (entity == null) return null;

        var relationshipManager = entity.GetComponent<RelationshipManager>();
        if (relationshipManager?.HandEntity == null) return null;

        var handEntity = relationshipManager.HandEntity.GetComponent<NetworkEntity>();
        if (handEntity == null) return null;

        return handEntity.GetComponent<HandManager>();
    }
    
    #endregion
}

/// <summary>
/// Editor window for spawning cards directly into player hands
/// </summary>
public class CardSpawnerWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private string searchFilter = "";
    private List<CardData> filteredCards = new List<CardData>();
    private List<CardData> allCards = new List<CardData>();
    
    public static void ShowWindow()
    {
        CardSpawnerWindow window = GetWindow<CardSpawnerWindow>("Card Spawner");
        window.RefreshCardList();
    }
    
    private void OnEnable()
    {
        RefreshCardList();
    }
    
    private void RefreshCardList()
    {
        allCards.Clear();
        if (CardDatabase.Instance != null)
        {
            allCards = CardDatabase.Instance.GetAllCards();
        }
        else
        {
            // Try to find cards in project
            string[] guids = AssetDatabase.FindAssets("t:CardData");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
                if (card != null)
                {
                    allCards.Add(card);
                }
            }
        }
        
        FilterCards();
    }
    
    private void FilterCards()
    {
        filteredCards.Clear();
        
        if (string.IsNullOrEmpty(searchFilter))
        {
            filteredCards.AddRange(allCards);
        }
        else
        {
            foreach (var card in allCards)
            {
                if (card.CardName.ToLower().Contains(searchFilter.ToLower()) ||
                    card.Description.ToLower().Contains(searchFilter.ToLower()))
                {
                    filteredCards.Add(card);
                }
            }
        }
        
        // Sort by name
        filteredCards.Sort((a, b) => a.CardName.CompareTo(b.CardName));
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Card Spawner", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();
        
        // Search filter
        EditorGUI.BeginChangeCheck();
        searchFilter = EditorGUILayout.TextField("Search:", searchFilter);
        if (EditorGUI.EndChangeCheck())
        {
            FilterCards();
        }
        
        // Refresh button
        if (GUILayout.Button("Refresh Card List"))
        {
            RefreshCardList();
        }
        
        EditorGUILayout.Space();
        
        // Target selection
        GUILayout.Label("Target:", EditorStyles.boldLabel);
        if (GUILayout.Button("Spawn in Local Player Hand"))
        {
            // This will be handled per card
        }
        
        EditorGUILayout.Space();
        GUILayout.Label($"Cards Found: {filteredCards.Count}", EditorStyles.helpBox);
        EditorGUILayout.Space();
        
        // Card list
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        foreach (var card in filteredCards)
        {
            DrawCardEntry(card);
        }
        
        EditorGUILayout.EndScrollView();
        
        // Bottom controls
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Spawn 5 Random Test Cards"))
        {
            SpawnRandomTestCards(5);
        }
        
        if (GUILayout.Button("Clear Player Hand"))
        {
            ClearPlayerHand();
        }
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawCardEntry(CardData card)
    {
        EditorGUILayout.BeginVertical("box");
        
        // Card header
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(card.CardName, EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        
        // Card details
        GUILayout.Label($"Cost: {card.EnergyCost}", GUILayout.Width(60));
        GUILayout.Label($"Type: {card.CardType}", GUILayout.Width(80));
        
        if (GUILayout.Button("Spawn", GUILayout.Width(60)))
        {
            SpawnCardInPlayerHand(card);
        }
        
        EditorGUILayout.EndHorizontal();
        
        // Card description
        if (!string.IsNullOrEmpty(card.Description))
        {
            GUILayout.Label(card.Description, EditorStyles.wordWrappedMiniLabel);
        }
        
        // Display card details using new Effects system
        string effectDetails = "Effects: ";
        if (card.HasEffects)
        {
            for (int i = 0; i < card.Effects.Count; i++)
            {
                var effect = card.Effects[i];
                effectDetails += $"{effect.effectType}";
                if (effect.amount > 0) effectDetails += $" ({effect.amount})";
                if (effect.duration > 0) effectDetails += $" for {effect.duration} turns";
                effectDetails += $" -> {effect.targetType}";
                
                if (i < card.Effects.Count - 1) effectDetails += ", ";
            }
        }
        else
        {
            effectDetails += "None";
        }
        
        GUILayout.Label(effectDetails, EditorStyles.miniLabel);
        
        // Advanced features based on new clean interface
        List<string> features = new List<string>();
        
        if (card.HasEffects && card.Effects.Count > 1) features.Add("Multi-Effect");
        if (card.BuildsCombo) features.Add("Combo");
        if (card.RequiresCombo) features.Add("Finisher");
        if (card.ChangesStance) features.Add("Stance");
        if (card.CreatesPersistentEffects) features.Add("Persistent");
        
        // Check for specific effect types
        if (card.HasEffects)
        {
            bool hasScaling = card.Effects.Any(e => e.scalingType != ScalingType.None);
            bool hasConditional = card.Effects.Any(e => e.conditionType != ConditionalType.None);
            bool hasElemental = card.Effects.Any(e => e.elementalType != ElementalType.None);
            
            if (hasScaling) features.Add("Scaling");
            if (hasConditional) features.Add("Conditional");
            if (hasElemental) features.Add("Elemental");
        }
        
        if (features.Count > 0)
        {
            GUILayout.Label($"Features: {string.Join(", ", features)}", EditorStyles.miniLabel);
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }
    
    private void SpawnCardInPlayerHand(CardData card)
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Error", "Cannot spawn cards while not playing", "OK");
            return;
        }
        
        if (TestCombat.Instance == null)
        {
            EditorUtility.DisplayDialog("Error", "TestCombat instance not found in scene", "OK");
            return;
        }
        
        NetworkEntity localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            TestCombat.Instance.SpawnCardInHand(localPlayer, card);
        }
        else
        {
            EditorUtility.DisplayDialog("Error", "No local player found", "OK");
        }
    }
    
    private void SpawnRandomTestCards(int count)
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Error", "Cannot spawn cards while not playing", "OK");
            return;
        }
        
        TestCombat.Instance?.SpawnRandomCardsInHand(GetLocalPlayer(), count);
    }
    
    private void ClearPlayerHand()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Error", "Cannot clear hand while not playing", "OK");
            return;
        }
        
        TestCombat.Instance?.ClearEntityHand(GetLocalPlayer());
    }
    
    private static NetworkEntity GetLocalPlayer()
    {
        NetworkEntity[] entities = FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        foreach (NetworkEntity entity in entities)
        {
            if (entity.EntityType == EntityType.Player && entity.IsOwner)
            {
                return entity;
            }
        }
        return null;
    }
}

#endif 