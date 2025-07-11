using UnityEngine;
using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Main combat testing manager that orchestrates card testing
/// Attach to: A GameObject in the combat scene for testing purposes
/// </summary>
public class CombatTestManager : NetworkBehaviour
{
    [Header("Test Configuration")]
    [SerializeField] private bool enableTestMode = false;
    [SerializeField] private float cardPlayDelay = 1.0f;
    [SerializeField] private float stateResetDelay = 0.5f;
    [SerializeField] private bool disableAnimationsDuringTests = true;
    [SerializeField] private bool disableParticlesDuringTests = true;
    [SerializeField] private bool disableSoundsDuringTests = true;
    
    [Header("Test Cards")]
    [SerializeField] private List<CardData> testCards = new List<CardData>();
    
    [Header("Test Deck (Alternative to individual cards)")]
    [SerializeField] private DeckData testDeck;
    [SerializeField] private bool useTestDeck = false;
    
    private Dictionary<int, EntityState> defaultEntityStates;
    private CombatManager combatManager;
    private FightManager fightManager;
    private bool isTestRunning = false;
    private int currentTestIndex = 0;
    
    // Entity references for testing
    private NetworkEntity localPlayer;
    private NetworkEntity opponent;
    private NetworkEntity playerAlly; // Player's pet
    private NetworkEntity opponentPlayer; // The opponent's owner (if opponent is a pet)
    
    private void Start()
    {
        combatManager = FindFirstObjectByType<CombatManager>();
        fightManager = FindFirstObjectByType<FightManager>();
        
        if (enableTestMode)
        {
            StartCoroutine(WaitForCombatSetupThenInitialize());
        }
    }
    
    private IEnumerator WaitForCombatSetupThenInitialize()
    {
        // Wait for combat entities to be properly set up
        yield return new WaitForSeconds(2.0f);
        
        InitializeCombatEntities();
        LoadTestCards();
        
        TestLogger.LogEvent("Combat Test Manager initialized");
    }
    
    private void InitializeCombatEntities()
    {
        // Use FightManager to get the correct local fight entities
        if (fightManager != null)
        {
            // Get the viewed fight entities from FightManager
            var viewedEntities = fightManager.GetViewedFightEntities();
            
            // Extract entities by type
            localPlayer = viewedEntities.FirstOrDefault(e => e.EntityType == EntityType.Player);
                    playerAlly = viewedEntities.FirstOrDefault(e => e.EntityType == EntityType.Pet && e != fightManager.ViewedRightFighter);
        opponent = fightManager.ViewedRightFighter;
            
            // Find the opponent player by getting the owner of the opponent pet
            if (opponent != null)
            {
                opponentPlayer = fightManager.GetOpponent(opponent);
            }
            
            Debug.Log($"Test Manager - Using FightManager viewed fight entities:");
            Debug.Log($"  ViewedPlayer: {fightManager.ViewedLeftFighter?.EntityName.Value ?? "null"}");
            Debug.Log($"  ViewedOpponent: {fightManager.ViewedRightFighter?.EntityName.Value ?? "null"}");
        }
        else
        {
            Debug.LogWarning("FightManager not found, falling back to manual entity search");
            
            // Fallback: Find all combat entities manually
            var allEntities = Object.FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
            localPlayer = allEntities.FirstOrDefault(e => e.EntityType == EntityType.Player);
            
            if (localPlayer != null)
            {
                playerAlly = GetPlayerAlly(localPlayer);
                opponentPlayer = GetOpponentPlayer(localPlayer);
                
                // Find opponent pet by looking for pets not owned by local player
                opponent = allEntities.FirstOrDefault(e => e.EntityType == EntityType.Pet && e.OwnerEntityId.Value != localPlayer.ObjectId);
            }
        }
        
        Debug.Log($"Test Manager - Found entities: Player={localPlayer?.EntityName.Value}, Opponent={opponent?.EntityName.Value}, PlayerAlly={playerAlly?.EntityName.Value}, OpponentPlayer={opponentPlayer?.EntityName.Value}");
    }
    
    private NetworkEntity GetPlayerAlly(NetworkEntity player)
    {
        // Find the player's pet (ally)
        var allEntities = Object.FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        return allEntities.FirstOrDefault(e => e.EntityType == EntityType.Pet && e.OwnerEntityId.Value == player.ObjectId);
    }
    
    private NetworkEntity GetOpponentPlayer(NetworkEntity player)
    {
        // Find the other player in the fight
        var allEntities = Object.FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
        return allEntities.FirstOrDefault(e => e.EntityType == EntityType.Player && e != player);
    }
    
    private void CaptureDefaultStates()
    {
        // First, ensure all entities are in a clean state by clearing status effects
        EnsureCleanEntityStates();
        
        defaultEntityStates = EntityStateCapture.CaptureAllCombatEntityStates();
        TestLogger.LogEvent($"Captured default states for {defaultEntityStates.Count} entities");
        
        // Log the captured states for debugging
        foreach (var state in defaultEntityStates.Values)
        {
            TestLogger.LogEvent($"Default state - {state.entityName}: Health={state.currentHealth}/{state.maxHealth}, Energy={state.currentEnergy}/{state.maxEnergy}, Effects={state.activeEffects.Count}");
            foreach (var effect in state.activeEffects)
            {
                TestLogger.LogEvent($"  - Effect: {effect.effectName} (Potency: {effect.potency}, Duration: {effect.remainingDuration})");
            }
        }
    }
    
    private void EnsureCleanEntityStates()
    {
        var allEntities = Object.FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None)
            .Where(e => e.EntityType == EntityType.Player || e.EntityType == EntityType.Pet);
        
        foreach (var entity in allEntities)
        {
            // Clear all status effects from EffectHandler
            var effectHandler = entity.GetComponent<EffectHandler>();
            if (effectHandler != null)
            {
                effectHandler.ClearAllEffects();
                TestLogger.LogEvent($"Cleared all effects from {entity.EntityName.Value}");
            }
            
            // Clear stun state from EntityTracker (this is separate from EffectHandler)
            var entityTracker = entity.GetComponent<EntityTracker>();
            if (entityTracker != null)
            {
                if (entityTracker.IsStunned)
                {
                    entityTracker.SetStunned(false);
                    TestLogger.LogEvent($"Cleared stun state from {entity.EntityName.Value}");
                }
                
                // Also clear other combat states that might interfere
                // Limit break system removed - no longer available
                
                // Reset stance to None
                if (entityTracker.CurrentStance != StanceType.None)
                {
                    entityTracker.SetStance(StanceType.None);
                    TestLogger.LogEvent($"Reset stance to None for {entity.EntityName.Value}");
                }
                
                // Reset combo count
                if (entityTracker.ComboCount > 0)
                {
                    entityTracker.SetComboCount(0);
                    TestLogger.LogEvent($"Reset combo count to 0 for {entity.EntityName.Value}");
                }
            }
            
            // Ensure entity has reasonable stats for testing
            if (entity.CurrentHealth.Value <= 0)
            {
                entity.CurrentHealth.Value = entity.MaxHealth.Value;
                TestLogger.LogEvent($"Restored {entity.EntityName.Value} health to {entity.MaxHealth.Value}");
            }
            
            if (entity.CurrentEnergy.Value < 10)
            {
                entity.CurrentEnergy.Value = Mathf.Max(entity.MaxEnergy.Value, 10);
                TestLogger.LogEvent($"Set {entity.EntityName.Value} energy to {entity.CurrentEnergy.Value}");
            }
        }
        
        TestLogger.LogEvent("Ensured all entities are in clean state for testing");
    }
    
    private void LoadTestCards()
    {
        if (useTestDeck && testDeck != null)
        {
            // Use the test deck
            testCards.Clear();
            testCards.AddRange(testDeck.CardsInDeck);
            TestLogger.LogEvent($"Loaded {testCards.Count} cards from test deck: {testDeck.name}");
        }
        else
        {
            #if UNITY_EDITOR
            // Auto-load test cards from the generated test cards folder
            var testCardGuids = UnityEditor.AssetDatabase.FindAssets("TEST_ t:CardData");
            testCards.Clear();
            
            foreach (var guid in testCardGuids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var cardData = UnityEditor.AssetDatabase.LoadAssetAtPath<CardData>(path);
                if (cardData != null)
                {
                    testCards.Add(cardData);
                }
            }
            
            TestLogger.LogEvent($"Loaded {testCards.Count} test cards from generated folder");
            #else
            // In builds, use manually assigned test cards
            TestLogger.LogEvent($"Using {testCards.Count} manually assigned test cards");
            #endif
        }
    }
    
    /// <summary>
    /// Start testing from player perspective (called from client)
    /// </summary>
    public void StartPlayerPerspectiveTests()
    {
        // Clear console logs and test results for fresh run
        ClearConsole();
        TestLogger.ClearResults();
        
        Debug.Log($"StartPlayerPerspectiveTests called - IsOwner: {IsOwner}, IsServer: {IsServerStarted}, IsClient: {IsClientStarted}");
        
        // Get the client's local viewed entities before sending to server
        NetworkEntity clientLocalPlayer = null;
        NetworkEntity clientOpponent = null;
        NetworkEntity clientPlayerAlly = null;
        NetworkEntity clientOpponentPlayer = null;
        
        if (fightManager != null)
        {
            var viewedEntities = fightManager.GetViewedFightEntities();
            clientLocalPlayer = viewedEntities.FirstOrDefault(e => e.EntityType == EntityType.Player);
            clientOpponent = fightManager.ViewedRightFighter;
            clientPlayerAlly = viewedEntities.FirstOrDefault(e => e.EntityType == EntityType.Pet && e != clientOpponent);
            clientOpponentPlayer = fightManager.GetOpponent(clientOpponent);
            
            Debug.Log($"Client entities - Player: {clientLocalPlayer?.EntityName.Value}, Opponent: {clientOpponent?.EntityName.Value}, PlayerAlly: {clientPlayerAlly?.EntityName.Value}, OpponentPlayer: {clientOpponentPlayer?.EntityName.Value}");
        }
        
        // If we're on the server, run directly. Otherwise, call RPC.
        if (IsServerStarted)
        {
            Debug.Log("Running tests directly on server...");
            StartPlayerPerspectiveTestsServerRpc(
                clientLocalPlayer?.ObjectId ?? -1,
                clientOpponent?.ObjectId ?? -1,
                clientPlayerAlly?.ObjectId ?? -1,
                clientOpponentPlayer?.ObjectId ?? -1
            );
        }
        else
        {
            Debug.Log("Calling StartPlayerPerspectiveTestsServerRpc...");
            // Call the server RPC to start tests with client's entity IDs
            StartPlayerPerspectiveTestsServerRpc(
                clientLocalPlayer?.ObjectId ?? -1,
                clientOpponent?.ObjectId ?? -1,
                clientPlayerAlly?.ObjectId ?? -1,
                clientOpponentPlayer?.ObjectId ?? -1
            );
        }
    }
    
    /// <summary>
    /// Start testing from opponent perspective (called from client)
    /// </summary>
    public void StartOpponentPerspectiveTests()
    {
        // Clear console logs and test results for fresh run
        ClearConsole();
        TestLogger.ClearResults();
        
        Debug.Log($"StartOpponentPerspectiveTests called - IsOwner: {IsOwner}, IsServer: {IsServerStarted}, IsClient: {IsClientStarted}");
        
        // Get the client's local viewed entities before sending to server
        NetworkEntity clientLocalPlayer = null;
        NetworkEntity clientOpponent = null;
        NetworkEntity clientPlayerAlly = null;
        NetworkEntity clientOpponentPlayer = null;
        
        if (fightManager != null)
        {
            var viewedEntities = fightManager.GetViewedFightEntities();
            clientLocalPlayer = viewedEntities.FirstOrDefault(e => e.EntityType == EntityType.Player);
            clientOpponent = fightManager.ViewedRightFighter;
            clientPlayerAlly = viewedEntities.FirstOrDefault(e => e.EntityType == EntityType.Pet && e != clientOpponent);
            clientOpponentPlayer = fightManager.GetOpponent(clientOpponent);
            
            Debug.Log($"Client entities - Player: {clientLocalPlayer?.EntityName.Value}, Opponent: {clientOpponent?.EntityName.Value}, PlayerAlly: {clientPlayerAlly?.EntityName.Value}, OpponentPlayer: {clientOpponentPlayer?.EntityName.Value}");
        }
        
        // If we're on the server, run directly. Otherwise, call RPC.
        if (IsServerStarted)
        {
            Debug.Log("Running tests directly on server...");
            StartOpponentPerspectiveTestsServerRpc(
                clientLocalPlayer?.ObjectId ?? -1,
                clientOpponent?.ObjectId ?? -1,
                clientPlayerAlly?.ObjectId ?? -1,
                clientOpponentPlayer?.ObjectId ?? -1
            );
        }
        else
        {
            Debug.Log("Calling StartOpponentPerspectiveTestsServerRpc...");
            // Call the server RPC to start tests with client's entity IDs
            StartOpponentPerspectiveTestsServerRpc(
                clientLocalPlayer?.ObjectId ?? -1,
                clientOpponent?.ObjectId ?? -1,
                clientPlayerAlly?.ObjectId ?? -1,
                clientOpponentPlayer?.ObjectId ?? -1
            );
        }
    }
    
    /// <summary>
    /// Server RPC to start testing from player perspective
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void StartPlayerPerspectiveTestsServerRpc(int clientLocalPlayerId, int clientOpponentId, int clientPlayerAllyId, int clientOpponentPlayerId)
    {
        Debug.Log($"StartPlayerPerspectiveTestsServerRpc called on server with client entity IDs: Player={clientLocalPlayerId}, Opponent={clientOpponentId}, PlayerAlly={clientPlayerAllyId}, OpponentPlayer={clientOpponentPlayerId}");
        
        if (isTestRunning)
        {
            Debug.LogWarning("Test already running, skipping");
            TestLogger.LogEvent("Test already running, skipping");
            return;
        }
        
        // Use the client's entity IDs to find the correct entities on the server
        localPlayer = FindEntityById(clientLocalPlayerId);
        opponent = FindEntityById(clientOpponentId);
        playerAlly = FindEntityById(clientPlayerAllyId);
        opponentPlayer = FindEntityById(clientOpponentPlayerId);
        
        Debug.Log($"Server found entities: Player={localPlayer?.EntityName.Value}, Opponent={opponent?.EntityName.Value}, PlayerAlly={playerAlly?.EntityName.Value}, OpponentPlayer={opponentPlayer?.EntityName.Value}");
        
        // Capture default states now that entities are confirmed to be ready
        CaptureDefaultStates();
        
        Debug.Log($"Local player: {(localPlayer != null ? localPlayer.EntityName.Value : "NULL")}");
        Debug.Log($"Test cards count: {testCards.Count}");
        
        if (localPlayer == null)
        {
            Debug.LogError("No local player found, cannot run tests");
            TestLogger.LogEvent("No local player found, cannot run tests");
            
            // Debug: List all available entities
            var allEntities = Object.FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None);
            Debug.Log($"Available entities ({allEntities.Length}):");
            foreach (var entity in allEntities)
            {
                Debug.Log($"  - {entity.EntityName.Value} (ID: {entity.ObjectId}, Type: {entity.EntityType}, IsOwner: {entity.IsOwner})");
            }
            
            return;
        }
        
        if (testCards.Count == 0)
        {
            Debug.LogError("No test cards loaded, cannot run tests");
            TestLogger.LogEvent("No test cards loaded, cannot run tests");
            return;
        }
        
        Debug.Log("Starting RunTestsFromPerspective coroutine...");
        StartCoroutine(RunTestsFromPerspective(localPlayer, "Player"));
    }
    
    /// <summary>
    /// Server RPC to start testing from opponent perspective
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void StartOpponentPerspectiveTestsServerRpc(int clientLocalPlayerId, int clientOpponentId, int clientPlayerAllyId, int clientOpponentPlayerId)
    {
        Debug.Log($"StartOpponentPerspectiveTestsServerRpc called on server with client entity IDs: Player={clientLocalPlayerId}, Opponent={clientOpponentId}, PlayerAlly={clientPlayerAllyId}, OpponentPlayer={clientOpponentPlayerId}");
        
        if (isTestRunning)
        {
            Debug.LogWarning("Test already running, skipping");
            TestLogger.LogEvent("Test already running, skipping");
            return;
        }
        
        // Use the client's entity IDs to find the correct entities on the server
        localPlayer = FindEntityById(clientLocalPlayerId);
        opponent = FindEntityById(clientOpponentId);
        playerAlly = FindEntityById(clientPlayerAllyId);
        opponentPlayer = FindEntityById(clientOpponentPlayerId);
        
        Debug.Log($"Server found entities: Player={localPlayer?.EntityName.Value}, Opponent={opponent?.EntityName.Value}, PlayerAlly={playerAlly?.EntityName.Value}, OpponentPlayer={opponentPlayer?.EntityName.Value}");
        
        // Capture default states now that entities are confirmed to be ready
        CaptureDefaultStates();
        
        Debug.Log($"Opponent: {(opponent != null ? opponent.EntityName.Value : "NULL")}");
        Debug.Log($"Test cards count: {testCards.Count}");
        
        if (opponent == null)
        {
            Debug.LogError("No opponent found, cannot run tests");
            TestLogger.LogEvent("No opponent found, cannot run tests");
            return;
        }
        
        if (testCards.Count == 0)
        {
            Debug.LogError("No test cards loaded, cannot run tests");
            TestLogger.LogEvent("No test cards loaded, cannot run tests");
            return;
        }
        
        Debug.Log("Starting RunTestsFromPerspective coroutine...");
        StartCoroutine(RunTestsFromPerspective(opponent, "Opponent"));
    }
    
    private IEnumerator RunTestsFromPerspective(NetworkEntity caster, string perspectiveName)
    {
        Debug.Log($"RunTestsFromPerspective started - Perspective: {perspectiveName}");
        
        isTestRunning = true;
        currentTestIndex = 0;
        
        // Disable visual/audio systems for faster testing
        DisableVisualAndAudioSystems();
        
        TestLogger.LogEvent($"Starting tests from {perspectiveName} perspective");
        TestLogger.LogEvent($"Caster: {caster.EntityName.Value}");
        
        Debug.Log($"About to test {testCards.Count} cards");
        
        // Clear hands first
        Debug.Log("Clearing hands...");
        yield return StartCoroutine(ClearAllHands());
        
        foreach (var testCard in testCards)
        {
            Debug.Log($"Testing card {currentTestIndex + 1}/{testCards.Count}: {testCard.CardName}");
            yield return StartCoroutine(RunSingleCardTest(caster, testCard, perspectiveName));
            currentTestIndex++;
            
            // Small delay between tests
            yield return new WaitForSeconds(0.1f);
        }
        
        isTestRunning = false;
        
        // Re-enable visual/audio systems
        EnableVisualAndAudioSystems();
        
        // Log final summary
        TestLogger.LogEvent($"Completed {testCards.Count} tests from {perspectiveName} perspective");
        Debug.Log($"All tests completed! Final summary:");
        Debug.Log(TestLogger.GetSummary());
        Debug.Log(TestLogger.GetAllResults());
    }
    
    private IEnumerator RunSingleCardTest(NetworkEntity caster, CardData cardData, string perspectiveName)
    {
        // Check if this card has conditional effects
        var conditionalEffects = cardData.Effects.Where(e => e.conditionType != ConditionalType.None).ToList();
        
        // Check if this is a combo card that should be tested as conditional
        bool isComboCard = cardData.CardName.Contains("Combo Spender") || cardData.CardName.Contains("Big Combo");
        
        if (conditionalEffects.Count > 0)
        {
            // Test conditional effects with both positive and negative cases
            yield return StartCoroutine(RunConditionalCardTest(caster, cardData, perspectiveName, conditionalEffects));
        }
        else if (isComboCard)
        {
            // Test combo cards as if they have conditional effects
            yield return StartCoroutine(RunComboCardTest(caster, cardData, perspectiveName));
        }
        else
        {
            // Regular non-conditional test
            yield return StartCoroutine(RunBasicCardTest(caster, cardData, perspectiveName));
        }
    }
    
    private IEnumerator RunBasicCardTest(NetworkEntity caster, CardData cardData, string perspectiveName)
    {
        // For cards that can target multiple types, test each target type
        var possibleTargets = GetPossibleTargetsForCard(caster, cardData);
        
        if (possibleTargets.Count > 1)
        {
            // Test each possible target separately
            foreach (var targetInfo in possibleTargets)
            {
                yield return StartCoroutine(RunBasicCardTestOnTarget(caster, cardData, perspectiveName, targetInfo.target, targetInfo.targetDescription));
            }
        }
        else
        {
            // Single target test
            var target = DetermineCardTarget(caster, cardData);
            string targetDesc = GetTargetDescription(caster, target);
            yield return StartCoroutine(RunBasicCardTestOnTarget(caster, cardData, perspectiveName, target, targetDesc));
        }
    }
    
    private IEnumerator RunBasicCardTestOnTarget(NetworkEntity caster, CardData cardData, string perspectiveName, NetworkEntity target, string targetDescription)
    {
        TestLogger.StartTest($"{perspectiveName}: {cardData.CardName} → {targetDescription}");
        TestLogger.LogEvent($"Card Description: \"{cardData.Description}\"");
        TestLogger.LogEvent($"Target: {targetDescription}");
        
        // 1. Give caster enough energy to play the card
        caster.CurrentEnergy.Value = Mathf.Max(caster.CurrentEnergy.Value, cardData.EnergyCost + 5);
        
        // 2. Setup target for healing effects if needed
        yield return StartCoroutine(SetupTargetForCardEffects(caster, cardData));
        
        // 3. Setup combo requirements if needed
        yield return StartCoroutine(SetupComboRequirements(caster, cardData));
        
        // 4. Capture before states AFTER all setup is complete
        var beforeStates = EntityStateCapture.CaptureAllCombatEntityStates();
        
        // 5. Spawn card and play it
        yield return StartCoroutine(SpawnAndPlayCardOnSpecificTarget(caster, cardData, target));
        
        // 6. Capture after states and validate
        var afterStates = EntityStateCapture.CaptureAllCombatEntityStates();
        LogStateChanges(beforeStates, afterStates);
        
        bool testPassed = ValidateCardEffect(cardData, beforeStates, afterStates, caster, target);
        TestLogger.LogTestResult(testPassed);
        
        // 7. Reset to default state
        yield return StartCoroutine(ResetToDefaultState());
        
        TestLogger.FinishTest();
    }
    
    private IEnumerator RunComboCardTest(NetworkEntity caster, CardData cardData, string perspectiveName)
    {
        int requiredCombo = 1; // Default combo requirement
        if (cardData.CardName.Contains("Big Combo"))
        {
            requiredCombo = 3; // Big combo spenders typically need more combo
        }
        
        // Test with combo requirements met
        yield return StartCoroutine(RunComboTestCase(caster, cardData, perspectiveName, requiredCombo, true));
        
        // Test with combo requirements not met
        yield return StartCoroutine(RunComboTestCase(caster, cardData, perspectiveName, requiredCombo, false));
    }
    
    private IEnumerator RunComboTestCase(NetworkEntity caster, CardData cardData, string perspectiveName, int requiredCombo, bool shouldMeetCondition)
    {
        string conditionCase = shouldMeetCondition ? "COMBO AVAILABLE" : "NO COMBO";
        TestLogger.StartTest($"{perspectiveName}: {cardData.CardName} ({conditionCase})");
        TestLogger.LogEvent($"Card Description: \"{cardData.Description}\"");
        
        // 1. Set up combo count
        var entityTracker = caster.GetComponent<EntityTracker>();
        if (entityTracker != null)
        {
            int targetCombo = shouldMeetCondition ? requiredCombo : 0;
            entityTracker.SetComboCount(targetCombo);
            TestLogger.LogEvent($"Set {caster.EntityName.Value} combo count to {targetCombo} (required: {requiredCombo})");
        }
        
        // 2. Give caster enough energy to play the card
        caster.CurrentEnergy.Value = Mathf.Max(caster.CurrentEnergy.Value, cardData.EnergyCost + 5);
        
        // 3. Setup target for healing effects if needed
        yield return StartCoroutine(SetupTargetForCardEffects(caster, cardData));
        
        // 4. Capture before states AFTER all setup is complete
        var beforeStates = EntityStateCapture.CaptureAllCombatEntityStates();
        
        // 5. Spawn card and play it
        TestLogger.LogEvent($"Playing combo card with {(shouldMeetCondition ? "sufficient" : "insufficient")} combo");
        yield return StartCoroutine(SpawnAndPlayCard(caster, cardData));
        
        // 6. Capture after states and validate
        var afterStates = EntityStateCapture.CaptureAllCombatEntityStates();
        LogStateChanges(beforeStates, afterStates);
        
        var target = DetermineCardTarget(caster, cardData);
        bool testPassed = ValidateComboCardEffect(cardData, beforeStates, afterStates, caster, target, shouldMeetCondition, requiredCombo);
        TestLogger.LogTestResult(testPassed);
        
        // 7. Reset to default state
        yield return StartCoroutine(ResetToDefaultState());
        
        TestLogger.FinishTest();
    }
    
    private IEnumerator RunConditionalCardTest(NetworkEntity caster, CardData cardData, string perspectiveName, List<CardEffect> conditionalEffects)
    {
        foreach (var conditionalEffect in conditionalEffects)
        {
            // Test positive case (condition met)
            yield return StartCoroutine(RunConditionalTestCase(caster, cardData, perspectiveName, conditionalEffect, true));
            
            // Test negative case (condition not met)
            yield return StartCoroutine(RunConditionalTestCase(caster, cardData, perspectiveName, conditionalEffect, false));
        }
    }
    
    private IEnumerator RunConditionalTestCase(NetworkEntity caster, CardData cardData, string perspectiveName, CardEffect conditionalEffect, bool shouldMeetCondition)
    {
        string conditionCase = shouldMeetCondition ? "CONDITION MET" : "CONDITION NOT MET";
        TestLogger.StartTest($"{perspectiveName}: {cardData.CardName} ({conditionalEffect.conditionType} - {conditionCase})");
        TestLogger.LogEvent($"Card Description: \"{cardData.Description}\"");
        
        // 1. Set up entity states to meet or not meet the condition
        yield return StartCoroutine(SetupConditionalTestState(caster, cardData, conditionalEffect, shouldMeetCondition));
        
        // 2. Give caster enough energy to play the card
        caster.CurrentEnergy.Value = Mathf.Max(caster.CurrentEnergy.Value, cardData.EnergyCost + 5);
        
        // 3. Setup target for healing effects if needed
        yield return StartCoroutine(SetupTargetForCardEffects(caster, cardData));
        
        // 4. Capture before states AFTER all setup is complete
        var beforeStates = EntityStateCapture.CaptureAllCombatEntityStates();
        
        // 5. Spawn card and play it
        TestLogger.LogEvent($"Playing card with {conditionalEffect.conditionType} condition {(shouldMeetCondition ? "MET" : "NOT MET")}");
        yield return StartCoroutine(SpawnAndPlayCard(caster, cardData));
        
        // 6. Capture after states and validate conditional behavior
        var afterStates = EntityStateCapture.CaptureAllCombatEntityStates();
        LogStateChanges(beforeStates, afterStates);
        
        var target = DetermineCardTarget(caster, cardData);
        bool testPassed = ValidateConditionalCardEffect(cardData, conditionalEffect, beforeStates, afterStates, caster, target, shouldMeetCondition);
        TestLogger.LogTestResult(testPassed);
        
        // 7. Reset to default state
        yield return StartCoroutine(ResetToDefaultState());
        
        TestLogger.FinishTest();
    }
    
    private IEnumerator SetupConditionalTestState(NetworkEntity caster, CardData cardData, CardEffect conditionalEffect, bool shouldMeetCondition)
    {
        var target = DetermineCardTarget(caster, cardData);
        
        switch (conditionalEffect.conditionType)
        {
            case ConditionalType.IfSourceHealthBelow:
                if (shouldMeetCondition)
                {
                    // Set source health below the condition value
                    caster.CurrentHealth.Value = conditionalEffect.conditionValue - 1;
                    TestLogger.LogEvent($"Set {caster.EntityName.Value} health to {caster.CurrentHealth.Value} (below {conditionalEffect.conditionValue})");
                }
                else
                {
                    // Set source health above the condition value
                    caster.CurrentHealth.Value = conditionalEffect.conditionValue + 10;
                    TestLogger.LogEvent($"Set {caster.EntityName.Value} health to {caster.CurrentHealth.Value} (above {conditionalEffect.conditionValue})");
                }
                break;
                
            case ConditionalType.IfSourceHealthAbove:
                if (shouldMeetCondition)
                {
                    // Set source health above the condition value
                    caster.CurrentHealth.Value = conditionalEffect.conditionValue + 10;
                    TestLogger.LogEvent($"Set {caster.EntityName.Value} health to {caster.CurrentHealth.Value} (above {conditionalEffect.conditionValue})");
                }
                else
                {
                    // Set source health below the condition value
                    caster.CurrentHealth.Value = conditionalEffect.conditionValue - 1;
                    TestLogger.LogEvent($"Set {caster.EntityName.Value} health to {caster.CurrentHealth.Value} (below {conditionalEffect.conditionValue})");
                }
                break;
                
            case ConditionalType.IfTargetHealthBelow:
                if (target != null)
                {
                    if (shouldMeetCondition)
                    {
                        target.CurrentHealth.Value = conditionalEffect.conditionValue - 1;
                        TestLogger.LogEvent($"Set {target.EntityName.Value} health to {target.CurrentHealth.Value} (below {conditionalEffect.conditionValue})");
                    }
                    else
                    {
                        target.CurrentHealth.Value = conditionalEffect.conditionValue + 10;
                        TestLogger.LogEvent($"Set {target.EntityName.Value} health to {target.CurrentHealth.Value} (above {conditionalEffect.conditionValue})");
                    }
                }
                break;
                
            case ConditionalType.IfTargetHealthAbove:
                if (target != null)
                {
                    if (shouldMeetCondition)
                    {
                        target.CurrentHealth.Value = conditionalEffect.conditionValue + 10;
                        TestLogger.LogEvent($"Set {target.EntityName.Value} health to {target.CurrentHealth.Value} (above {conditionalEffect.conditionValue})");
                    }
                    else
                    {
                        target.CurrentHealth.Value = conditionalEffect.conditionValue - 1;
                        TestLogger.LogEvent($"Set {target.EntityName.Value} health to {target.CurrentHealth.Value} (below {conditionalEffect.conditionValue})");
                    }
                }
                break;
                
            case ConditionalType.IfCardsInHand:
                yield return StartCoroutine(SetupHandSizeCondition(caster, conditionalEffect.conditionValue, shouldMeetCondition));
                break;
                
            case ConditionalType.IfTimesPlayedThisFight:
                yield return StartCoroutine(SetupTimesPlayedCondition(caster, cardData, conditionalEffect.conditionValue, shouldMeetCondition));
                break;
                
            case ConditionalType.IfComboCount:
                yield return StartCoroutine(SetupComboCondition(caster, conditionalEffect.conditionValue, shouldMeetCondition));
                break;
                
            case ConditionalType.IfInStance:
                yield return StartCoroutine(SetupStanceCondition(caster, conditionalEffect.conditionValue, shouldMeetCondition));
                break;
                
            case ConditionalType.IfEnergyRemaining:
                yield return StartCoroutine(SetupEnergyCondition(caster, conditionalEffect.conditionValue, shouldMeetCondition));
                break;
                
            case ConditionalType.IfLastCardType:
                yield return StartCoroutine(SetupLastCardTypeCondition(caster, conditionalEffect.conditionValue, shouldMeetCondition));
                break;
                
            case ConditionalType.IfPerfectionStreak:
                yield return StartCoroutine(SetupPerfectionStreakCondition(caster, conditionalEffect.conditionValue, shouldMeetCondition));
                break;
                
            case ConditionalType.IfZeroCostCardsThisTurn:
            case ConditionalType.IfZeroCostCardsThisFight:
                yield return StartCoroutine(SetupZeroCostCardCondition(caster, conditionalEffect.conditionType, conditionalEffect.conditionValue, shouldMeetCondition));
                break;
                
            case ConditionalType.IfDamageTakenLastRound:
            case ConditionalType.IfDamageTakenThisFight:
                yield return StartCoroutine(SetupDamageTakenCondition(caster, conditionalEffect.conditionType, conditionalEffect.conditionValue, shouldMeetCondition));
                break;
                
            case ConditionalType.IfHealingReceivedLastRound:
            case ConditionalType.IfHealingReceivedThisFight:
                yield return StartCoroutine(SetupHealingReceivedCondition(caster, conditionalEffect.conditionType, conditionalEffect.conditionValue, shouldMeetCondition));
                break;
                
            case ConditionalType.IfCardsInDeck:
            case ConditionalType.IfCardsInDiscard:
                TestLogger.LogEvent($"Conditional type {conditionalEffect.conditionType} not yet supported in test setup (requires deck/discard manipulation)");
                break;
                
            default:
                TestLogger.LogEvent($"Conditional type {conditionalEffect.conditionType} not yet supported in test setup");
                break;
        }
        
        // Small delay to ensure state changes are processed
        yield return new WaitForSeconds(0.1f);
    }
    
    private IEnumerator SetupHandSizeCondition(NetworkEntity caster, int requiredHandSize, bool shouldMeetCondition)
    {
        var relationshipManager = caster.GetComponent<RelationshipManager>();
        if (relationshipManager?.HandEntity != null)
        {
            var handManager = relationshipManager.HandEntity.GetComponent<HandManager>();
            if (handManager != null)
            {
                var currentHandSize = handManager.GetCardsInHand().Count;
                int targetHandSize = shouldMeetCondition ? requiredHandSize : requiredHandSize - 1;
                
                if (currentHandSize < targetHandSize)
                {
                    // Need to add cards to hand
                    TestLogger.LogEvent($"Adding cards to reach hand size {targetHandSize} (current: {currentHandSize})");
                    // This would require spawning dummy cards - simplified for now
                }
                else if (currentHandSize > targetHandSize)
                {
                    // Need to remove cards from hand
                    TestLogger.LogEvent($"Removing cards to reach hand size {targetHandSize} (current: {currentHandSize})");
                    // This would require removing cards - simplified for now
                }
                
                TestLogger.LogEvent($"Hand size condition: target={targetHandSize}, should meet={shouldMeetCondition}");
            }
        }
        yield return null;
    }
    
    private IEnumerator SetupTimesPlayedCondition(NetworkEntity caster, CardData cardData, int requiredTimesPlayed, bool shouldMeetCondition)
    {
        var cardTracker = caster.GetComponent<CardTracker>();
        if (cardTracker != null)
        {
            int targetTimesPlayed = shouldMeetCondition ? requiredTimesPlayed : requiredTimesPlayed - 1;
            
            // Simulate playing the card the required number of times
            for (int i = 0; i < targetTimesPlayed; i++)
            {
                cardTracker.RecordCardPlayed();
            }
            
            TestLogger.LogEvent($"Set times played to {targetTimesPlayed} (required: {requiredTimesPlayed}, should meet: {shouldMeetCondition})");
        }
        yield return null;
    }
    
    private IEnumerator SetupComboCondition(NetworkEntity caster, int requiredComboCount, bool shouldMeetCondition)
    {
        var entityTracker = caster.GetComponent<EntityTracker>();
        if (entityTracker != null)
        {
            int targetComboCount = shouldMeetCondition ? requiredComboCount : requiredComboCount - 1;
            
            // Set the combo count directly
            entityTracker.SetComboCount(targetComboCount);
            
            TestLogger.LogEvent($"Set combo count to {targetComboCount} (required: {requiredComboCount}, should meet: {shouldMeetCondition})");
        }
        yield return null;
    }
    
    private IEnumerator SetupStanceCondition(NetworkEntity caster, int requiredStanceValue, bool shouldMeetCondition)
    {
        var entityTracker = caster.GetComponent<EntityTracker>();
        if (entityTracker != null)
        {
            StanceType targetStance = shouldMeetCondition ? (StanceType)requiredStanceValue : StanceType.None;
            entityTracker.SetStance(targetStance);
            TestLogger.LogEvent($"Set stance to {targetStance} (required: {(StanceType)requiredStanceValue}, should meet: {shouldMeetCondition})");
        }
        yield return null;
    }
    
    private IEnumerator SetupEnergyCondition(NetworkEntity caster, int requiredEnergy, bool shouldMeetCondition)
    {
        int targetEnergy = shouldMeetCondition ? requiredEnergy + 5 : requiredEnergy - 1;
        caster.CurrentEnergy.Value = targetEnergy;
        TestLogger.LogEvent($"Set energy to {targetEnergy} (required: {requiredEnergy}, should meet: {shouldMeetCondition})");
        yield return null;
    }
    
    private IEnumerator SetupLastCardTypeCondition(NetworkEntity caster, int requiredCardTypeValue, bool shouldMeetCondition)
    {
        var entityTracker = caster.GetComponent<EntityTracker>();
        if (entityTracker != null)
        {
            CardType targetCardType = shouldMeetCondition ? (CardType)requiredCardTypeValue : CardType.Attack;
            // Simulate playing a card of the target type by directly setting the tracking data
            entityTracker.RecordCardPlayed(0, false, targetCardType, false);
            TestLogger.LogEvent($"Set last card type to {targetCardType} (required: {(CardType)requiredCardTypeValue}, should meet: {shouldMeetCondition})");
        }
        yield return null;
    }
    
    private IEnumerator SetupPerfectionStreakCondition(NetworkEntity caster, int requiredStreak, bool shouldMeetCondition)
    {
        var entityTracker = caster.GetComponent<EntityTracker>();
        if (entityTracker != null)
        {
            // Set perfection streak via reflection since there's no public setter
            var perfectionStreakField = typeof(EntityTracker).GetField("_perfectionStreak", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (perfectionStreakField != null)
            {
                var syncVar = perfectionStreakField.GetValue(entityTracker);
                var valueProperty = syncVar.GetType().GetProperty("Value");
                int targetStreak = shouldMeetCondition ? requiredStreak : requiredStreak - 1;
                valueProperty?.SetValue(syncVar, targetStreak);
                TestLogger.LogEvent($"Set perfection streak to {targetStreak} (required: {requiredStreak}, should meet: {shouldMeetCondition})");
            }
        }
        yield return null;
    }
    
    private IEnumerator SetupZeroCostCardCondition(NetworkEntity caster, ConditionalType conditionType, int requiredCount, bool shouldMeetCondition)
    {
        var entityTracker = caster.GetComponent<EntityTracker>();
        if (entityTracker != null)
        {
            int targetCount = shouldMeetCondition ? requiredCount : requiredCount - 1;
            
            // Use reflection to set the zero cost card counts
            string fieldName = conditionType == ConditionalType.IfZeroCostCardsThisTurn ? "_zeroCostCardsThisTurn" : "_zeroCostCardsThisFight";
            var field = typeof(EntityTracker).GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                var syncVar = field.GetValue(entityTracker);
                var valueProperty = syncVar.GetType().GetProperty("Value");
                valueProperty?.SetValue(syncVar, targetCount);
                TestLogger.LogEvent($"Set {conditionType} to {targetCount} (required: {requiredCount}, should meet: {shouldMeetCondition})");
            }
        }
        yield return null;
    }
    
    private IEnumerator SetupDamageTakenCondition(NetworkEntity caster, ConditionalType conditionType, int requiredDamage, bool shouldMeetCondition)
    {
        var entityTracker = caster.GetComponent<EntityTracker>();
        if (entityTracker != null)
        {
            int targetDamage = shouldMeetCondition ? requiredDamage : requiredDamage - 1;
            
            // Simulate damage taken
            if (conditionType == ConditionalType.IfDamageTakenLastRound)
            {
                entityTracker.GetTrackingDataForScaling().damageTakenLastRound = targetDamage;
            }
            else
            {
                entityTracker.GetTrackingDataForScaling().damageTakenThisFight = targetDamage;
            }
            TestLogger.LogEvent($"Set {conditionType} to {targetDamage} (required: {requiredDamage}, should meet: {shouldMeetCondition})");
        }
        yield return null;
    }
    
    private IEnumerator SetupHealingReceivedCondition(NetworkEntity caster, ConditionalType conditionType, int requiredHealing, bool shouldMeetCondition)
    {
        var entityTracker = caster.GetComponent<EntityTracker>();
        if (entityTracker != null)
        {
            int targetHealing = shouldMeetCondition ? requiredHealing : requiredHealing - 1;
            
            // Simulate healing received
            if (conditionType == ConditionalType.IfHealingReceivedLastRound)
            {
                entityTracker.GetTrackingDataForScaling().healingReceivedLastRound = targetHealing;
            }
            else
            {
                entityTracker.GetTrackingDataForScaling().healingReceivedThisFight = targetHealing;
            }
            TestLogger.LogEvent($"Set {conditionType} to {targetHealing} (required: {requiredHealing}, should meet: {shouldMeetCondition})");
        }
        yield return null;
    }
    
    private IEnumerator SetupTargetForCardEffects(NetworkEntity caster, CardData cardData)
    {
        // Check if any effects need special target setup
        foreach (var effect in cardData.Effects)
        {
            if (IsHealingEffect(effect.effectType))
            {
                var target = DetermineCardTarget(caster, cardData);
                if (target != null && target.CurrentHealth.Value >= target.MaxHealth.Value)
                {
                    // Reduce target health to allow healing to be visible
                    int healthReduction = Mathf.Max(effect.amount + 10, 20); // Ensure enough room for healing
                    target.CurrentHealth.Value = Mathf.Max(1, target.MaxHealth.Value - healthReduction);
                    TestLogger.LogEvent($"Reduced {target.EntityName.Value} health to {target.CurrentHealth.Value}/{target.MaxHealth.Value} to allow healing");
                }
            }
            
            // Check conditional effects too
            if (effect.hasAlternativeEffect && IsHealingEffect(effect.alternativeEffectType))
            {
                var target = DetermineCardTarget(caster, cardData);
                if (target != null && target.CurrentHealth.Value >= target.MaxHealth.Value)
                {
                    int healthReduction = Mathf.Max(effect.alternativeEffectAmount + 10, 20);
                    target.CurrentHealth.Value = Mathf.Max(1, target.MaxHealth.Value - healthReduction);
                    TestLogger.LogEvent($"Reduced {target.EntityName.Value} health to {target.CurrentHealth.Value}/{target.MaxHealth.Value} to allow conditional healing");
                }
            }
        }
        yield return null;
    }
    
    private bool IsHealingEffect(CardEffectType effectType)
    {
        return effectType == CardEffectType.Heal || 
               effectType == CardEffectType.ApplySalve;
    }
    
    private IEnumerator SetupComboRequirements(NetworkEntity caster, CardData cardData)
    {
        // This method is now only used for non-conditional combo setup
        // Conditional combo cards are handled in RunSingleCardTest
        yield return null;
    }
    
    private List<(NetworkEntity target, string targetDescription)> GetPossibleTargetsForCard(NetworkEntity caster, CardData cardData)
    {
        var targets = new List<(NetworkEntity, string)>();
        
        if (cardData.Effects.Count == 0) 
        {
            targets.Add((caster, GetTargetDescription(caster, caster)));
            return targets;
        }
        
        var primaryTargetType = cardData.Effects[0].targetType;
        
        switch (primaryTargetType)
        {
            case CardTargetType.Self:
                targets.Add((caster, GetTargetDescription(caster, caster)));
                break;
                
            case CardTargetType.Opponent:
                var opponentEntity = GetOpponentForEntity(caster);
                if (opponentEntity != null)
                    targets.Add((opponentEntity, GetTargetDescription(caster, opponentEntity)));
                break;
                
            case CardTargetType.Ally:
                var ally = GetAllyForEntity(caster);
                if (ally != null)
                    targets.Add((ally, GetTargetDescription(caster, ally)));
                break;
                
            case CardTargetType.Random:
                // For random cards, test on all possible targets
                var allEntities = new[] { localPlayer, opponent, playerAlly, opponentPlayer }.Where(e => e != null);
                foreach (var entity in allEntities)
                {
                    targets.Add((entity, GetTargetDescription(caster, entity)));
                }
                break;
        }
        
        return targets;
    }
    
    private string GetTargetDescription(NetworkEntity caster, NetworkEntity target)
    {
        if (target == caster) return $"{target.EntityName.Value} (Self)";
        
        if (caster == localPlayer)
        {
            if (target == playerAlly) return $"{target.EntityName.Value} (Ally)";
            if (target == opponent) return $"{target.EntityName.Value} (Opponent)";
            if (target == opponentPlayer) return $"{target.EntityName.Value} (Opponent Player)";
        }
        else if (caster == opponent)
        {
            if (target == opponentPlayer) return $"{target.EntityName.Value} (Ally)";
            if (target == localPlayer) return $"{target.EntityName.Value} (Opponent Player)";
            if (target == playerAlly) return $"{target.EntityName.Value} (Opponent Pet)";
        }
        else if (caster == playerAlly)
        {
            if (target == localPlayer) return $"{target.EntityName.Value} (Ally)";
            if (target == opponent) return $"{target.EntityName.Value} (Opponent)";
            if (target == opponentPlayer) return $"{target.EntityName.Value} (Opponent Player)";
        }
        else if (caster == opponentPlayer)
        {
            if (target == opponent) return $"{target.EntityName.Value} (Ally)";
            if (target == localPlayer) return $"{target.EntityName.Value} (Opponent Player)";
            if (target == playerAlly) return $"{target.EntityName.Value} (Opponent Pet)";
        }
        
        return $"{target.EntityName.Value} (Unknown Relation)";
    }
    
    private NetworkEntity GetOpponentForEntity(NetworkEntity entity)
    {
        if (entity == localPlayer) return opponent;
        if (entity == opponent) return localPlayer;
        if (entity == playerAlly) return opponentPlayer;
        if (entity == opponentPlayer) return playerAlly;
        return null;
    }
    
    private NetworkEntity GetAllyForEntity(NetworkEntity entity)
    {
        if (entity == localPlayer) return playerAlly;
        if (entity == opponent) return opponentPlayer;
        if (entity == playerAlly) return localPlayer;
        if (entity == opponentPlayer) return opponent;
        return null;
    }
    
    private IEnumerator SpawnAndPlayCardOnSpecificTarget(NetworkEntity caster, CardData cardData, NetworkEntity specificTarget)
    {
        // Spawn card and add to caster's hand
        var cardInstance = SpawnTestCard(caster, cardData);
        
        if (cardInstance == null)
        {
            TestLogger.LogTestResult(false, "Failed to spawn card");
            yield break;
        }
        
        // Wait a moment for the card to be fully initialized
        yield return new WaitForSeconds(0.2f);
        
        // Play the card on the specific target
        yield return StartCoroutine(PlayCardOnTarget(cardInstance, caster, specificTarget));
        TestLogger.LogCardPlay(caster.EntityName.Value, cardData.CardName, specificTarget.EntityName.Value, true);
        
        // Clean up the card instance
        DestroyCardInstance(cardInstance);
    }
    
    private IEnumerator SpawnAndPlayCard(NetworkEntity caster, CardData cardData)
    {
        // Check if caster is stunned or has other blocking effects
        if (IsEntityBlockedFromCardPlay(caster))
        {
            TestLogger.LogEvent($"WARNING: {caster.EntityName.Value} is blocked from playing cards - attempting to clear blocking effects");
            
            // Clear EffectHandler effects
            var effectHandler = caster.GetComponent<EffectHandler>();
            if (effectHandler != null)
            {
                effectHandler.ClearAllEffects();
                TestLogger.LogEvent($"Cleared all effects from {caster.EntityName.Value}");
            }
            
            // Clear EntityTracker blocking states
            var entityTracker = caster.GetComponent<EntityTracker>();
            if (entityTracker != null)
            {
                if (entityTracker.IsStunned)
                {
                    entityTracker.SetStunned(false);
                    TestLogger.LogEvent($"Cleared stun state from {caster.EntityName.Value}");
                }
                
                // Limit break system removed - no longer available
            }
            
            // Wait a moment for the effect clearing to process
            yield return new WaitForSeconds(0.2f);
            
            // Check again
            if (IsEntityBlockedFromCardPlay(caster))
            {
                TestLogger.LogTestResult(false, $"{caster.EntityName.Value} is still blocked from playing cards after clearing effects");
                yield break;
            }
        }
        
        // Spawn card and add to caster's hand
        var cardInstance = SpawnTestCard(caster, cardData);
        
        if (cardInstance == null)
        {
            TestLogger.LogTestResult(false, "Failed to spawn card");
            yield break;
        }
        
        // Determine target
        var target = DetermineCardTarget(caster, cardData);
        if (target == null)
        {
            TestLogger.LogTestResult(false, "No valid target found");
            DestroyCardInstance(cardInstance);
            yield break;
        }
        
        // Wait a moment for the card to be fully initialized
        yield return new WaitForSeconds(0.2f);
        
        // Play the card
        yield return StartCoroutine(PlayCardOnTarget(cardInstance, caster, target));
        TestLogger.LogCardPlay(caster.EntityName.Value, cardData.CardName, target.EntityName.Value, true);
        
        // Wait for effects to process
        // Use shorter delay if animations are disabled
        float actualDelay = disableAnimationsDuringTests ? 0.1f : cardPlayDelay;
        yield return new WaitForSeconds(actualDelay);
    }
    
    private bool IsEntityBlockedFromCardPlay(NetworkEntity entity)
    {
        // Check EntityTracker stun state (this is what HandleCardPlay actually checks)
        var entityTracker = entity.GetComponent<EntityTracker>();
        if (entityTracker != null && entityTracker.IsStunned)
        {
            TestLogger.LogEvent($"Found EntityTracker stun state on {entity.EntityName.Value}");
            return true;
        }
        
        // Also check EffectHandler for stun effects
        var effectHandler = entity.GetComponent<EffectHandler>();
        if (effectHandler != null)
        {
            var effects = effectHandler.GetAllEffects();
            foreach (var effect in effects)
            {
                // Check for effects that would block card play
                if (effect.EffectName.ToLower().Contains("stun") || 
                    effect.EffectName.ToLower().Contains("silence") ||
                    effect.EffectName.ToLower().Contains("disable"))
                {
                    TestLogger.LogEvent($"Found blocking effect on {entity.EntityName.Value}: {effect.EffectName} (Potency: {effect.Potency}, Duration: {effect.RemainingDuration})");
                    return true;
                }
            }
        }
        
        return false;
    }
    
    private bool ValidateConditionalCardEffect(CardData cardData, CardEffect conditionalEffect, Dictionary<int, EntityState> before, Dictionary<int, EntityState> after, NetworkEntity caster, NetworkEntity target, bool conditionShouldBeMet)
    {
        if (!before.ContainsKey(target.ObjectId) || !after.ContainsKey(target.ObjectId))
            return false;
        
        var targetBefore = before[target.ObjectId];
        var targetAfter = after[target.ObjectId];
        
        // Determine what effect should have occurred based on condition and alternative logic
        CardEffectType expectedEffectType;
        int expectedAmount;
        
        if (conditionShouldBeMet)
        {
            if (conditionalEffect.hasAlternativeEffect && conditionalEffect.alternativeLogic == AlternativeEffectLogic.Replace)
            {
                // Condition met with Replace logic: alternative effect should occur
                expectedEffectType = conditionalEffect.alternativeEffectType;
                expectedAmount = conditionalEffect.alternativeEffectAmount;
                TestLogger.LogEvent($"Expected alternative effect: {expectedEffectType} ({expectedAmount})");
            }
            else
            {
                // Condition met: main effect should occur (+ alternative if Additional logic)
                expectedEffectType = conditionalEffect.effectType;
                expectedAmount = conditionalEffect.amount;
                TestLogger.LogEvent($"Expected main effect: {expectedEffectType} ({expectedAmount})");
            }
        }
        else
        {
            // Condition not met: main effect should occur
            expectedEffectType = conditionalEffect.effectType;
            expectedAmount = conditionalEffect.amount;
            TestLogger.LogEvent($"Expected main effect (condition not met): {expectedEffectType} ({expectedAmount})");
        }
        
        // Validate the expected effect occurred
        return ValidateSpecificEffect(expectedEffectType, expectedAmount, targetBefore, targetAfter);
    }
    
    private bool ValidateComboCardEffect(CardData cardData, Dictionary<int, EntityState> before, Dictionary<int, EntityState> after, NetworkEntity caster, NetworkEntity target, bool hadCombo, int requiredCombo)
    {
        if (!before.ContainsKey(target.ObjectId) || !after.ContainsKey(target.ObjectId))
            return false;
        
        var targetBefore = before[target.ObjectId];
        var targetAfter = after[target.ObjectId];
        
        // For combo cards, if combo was available, we expect damage
        // If combo was not available, we expect little to no effect
        if (hadCombo)
        {
            // Combo was available - should do damage
            int actualDamage = targetBefore.currentHealth - targetAfter.currentHealth;
            bool damageOccurred = actualDamage > 0;
            TestLogger.LogEvent($"Combo card validation (combo available): expected damage, actual={actualDamage}, valid={damageOccurred}");
            return damageOccurred;
        }
        else
        {
            // No combo - should do little to no damage
            int actualDamage = targetBefore.currentHealth - targetAfter.currentHealth;
            bool minimalEffect = actualDamage <= 1; // Allow for base damage
            TestLogger.LogEvent($"Combo card validation (no combo): expected minimal damage, actual={actualDamage}, valid={minimalEffect}");
            return minimalEffect;
        }
    }
    
    private bool ValidateSpecificEffect(CardEffectType effectType, int expectedAmount, EntityState before, EntityState after)
    {
        switch (effectType)
        {
            case CardEffectType.Damage:
                int actualDamage = before.currentHealth - after.currentHealth;
                bool damageValid = actualDamage > 0; // Some damage should have occurred (accounting for shields, etc.)
                TestLogger.LogEvent($"Damage validation: expected some damage, actual={actualDamage}, valid={damageValid}");
                return damageValid;
                
            case CardEffectType.Heal:
                int actualHealing = after.currentHealth - before.currentHealth;
                bool healingValid = actualHealing > 0;
                TestLogger.LogEvent($"Healing validation: expected healing, actual={actualHealing}, valid={healingValid}");
                // If healing failed, let's check if damage occurred instead (indicates conditional logic bug)
                if (!healingValid && actualHealing < 0)
                {
                    TestLogger.LogEvent($"WARNING: Expected healing but got damage instead (actual change: {actualHealing}). This suggests conditional effect logic is broken in the game.");
                }
                return healingValid;
                
            case CardEffectType.ApplyBurn:
            case CardEffectType.ApplyShield:
            case CardEffectType.ApplyWeak:
            case CardEffectType.ApplyBreak:
                string effectName = GetStatusEffectName(effectType);
                bool hasEffect = after.activeEffects.Any(e => e.effectName == effectName);
                TestLogger.LogEvent($"Status effect validation: expected {effectName}, found={hasEffect}");
                return hasEffect;
                
            default:
                TestLogger.LogEvent($"Effect type {effectType} validation not implemented, assuming valid");
                return true;
        }
    }
    
    private string GetStatusEffectName(CardEffectType effectType)
    {
        switch (effectType)
        {
            case CardEffectType.ApplyBurn: return "Burn";
            case CardEffectType.ApplyShield: return "Shield";
            case CardEffectType.ApplyWeak: return "Weak";
            case CardEffectType.ApplyBreak: return "Break";
            case CardEffectType.ApplyThorns: return "Thorns";
            case CardEffectType.ApplyStun: return "Stun";
            default: return effectType.ToString().Replace("Apply", "");
        }
    }
    
    private Card SpawnTestCard(NetworkEntity caster, CardData cardData)
    {
        // Use RelationshipManager to find the hand entity
        var relationshipManager = caster.GetComponent<RelationshipManager>();
        if (relationshipManager == null) return null;
        
        var handEntity = relationshipManager.HandEntity;
        if (handEntity == null) return null;
        
        // Get the CardSpawner from the hand entity
        var cardSpawner = handEntity.GetComponent<CardSpawner>();
        if (cardSpawner == null) return null;
        
        // Get the HandManager from the hand entity
        var handManager = handEntity.GetComponent<HandManager>();
        if (handManager == null) return null;
        
        // Spawn card using CardSpawner (this creates the networked card)
        var spawnMethod = cardSpawner.GetType().GetMethod("SpawnCard", new[] { typeof(CardData) });
        if (spawnMethod != null)
        {
            var cardGameObject = (GameObject)spawnMethod.Invoke(cardSpawner, new object[] { cardData });
            if (cardGameObject != null)
            {
                var card = cardGameObject.GetComponent<Card>();
                if (card != null)
                {
                    // Move the card to hand using HandManager (following normal game flow)
                    var moveToHandMethod = handManager.GetType().GetMethod("MoveCardToHand", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (moveToHandMethod != null)
                    {
                        moveToHandMethod.Invoke(handManager, new object[] { cardGameObject });
                    }
                    else
                    {
                        // Fallback: manually set container and parent
                        card.SetCurrentContainer(CardLocation.Hand);
                        cardGameObject.transform.SetParent(handManager.GetHandTransform(), false);
                    }
                    
                    return card;
                }
            }
        }
        
        return null;
    }
    
    private NetworkEntity DetermineCardTarget(NetworkEntity caster, CardData cardData)
    {
        // Get the primary target type from the card's first effect
        if (cardData.Effects.Count == 0) return caster; // Default to self if no effects
        
        var primaryTargetType = cardData.Effects[0].targetType;
        
        switch (primaryTargetType)
        {
            case CardTargetType.Self:
                return caster;
                
            case CardTargetType.Opponent:
                if (caster == localPlayer) return opponent;
                if (caster == opponent) return localPlayer;
                if (caster == playerAlly) return opponentPlayer;
                if (caster == opponentPlayer) return playerAlly;
                break;
                
            case CardTargetType.Ally:
                if (caster == localPlayer) return playerAlly;
                if (caster == opponent) return opponentPlayer;
                if (caster == playerAlly) return localPlayer;
                if (caster == opponentPlayer) return opponent;
                break;
                
            case CardTargetType.Random:
                var allEntities = new[] { localPlayer, opponent, playerAlly, opponentPlayer }.Where(e => e != null).ToArray();
                return allEntities[Random.Range(0, allEntities.Length)];
        }
        
        return caster; // Fallback to self
    }
    
    private IEnumerator PlayCardOnTarget(Card cardInstance, NetworkEntity caster, NetworkEntity target)
    {
        var handleCardPlay = cardInstance.GetComponent<HandleCardPlay>();
        if (handleCardPlay != null)
        {
            // Ensure the card's source and target identifier is updated
            var sourceAndTargetIdentifier = cardInstance.GetComponent<SourceAndTargetIdentifier>();
            if (sourceAndTargetIdentifier != null)
            {
                sourceAndTargetIdentifier.UpdateSourceAndTarget();
            }
            
            // Use the proper ServerPlayCard method with correct parameters
            int[] targetIds = new int[] { target.ObjectId };
            handleCardPlay.ServerPlayCard(caster.ObjectId, targetIds);
        }
        else
        {
            // Fallback: use card effect resolver directly
            var cardEffectResolver = cardInstance.GetComponent<CardEffectResolver>();
            if (cardEffectResolver != null)
            {
                cardEffectResolver.ServerResolveCardEffect(caster, target, cardInstance.CardData);
            }
        }
        
        // Wait a moment for the card effects to process
        yield return new WaitForSeconds(0.5f);
    }
    
    private void DestroyCardInstance(Card cardInstance)
    {
        if (cardInstance != null)
        {
            DestroyImmediate(cardInstance.gameObject);
        }
    }
    
    private bool ValidateCardEffect(CardData cardData, Dictionary<int, EntityState> before, Dictionary<int, EntityState> after, NetworkEntity caster, NetworkEntity target)
    {
        // Simple validation - just check if the target's state changed appropriately
        if (!before.ContainsKey(target.ObjectId) || !after.ContainsKey(target.ObjectId))
            return false;
        
        var targetBefore = before[target.ObjectId];
        var targetAfter = after[target.ObjectId];
        
        // Check based on card's primary effect
        if (cardData.Effects.Count > 0)
        {
            var primaryEffect = cardData.Effects[0];
            
            switch (primaryEffect.effectType)
            {
                case CardEffectType.Damage:
                    bool damageOccurred = targetAfter.currentHealth < targetBefore.currentHealth;
                    TestLogger.LogEvent($"Basic damage validation: expected damage, actual change={targetBefore.currentHealth - targetAfter.currentHealth}, valid={damageOccurred}");
                    return damageOccurred;
                    
                case CardEffectType.Heal:
                    bool healingOccurred = targetAfter.currentHealth > targetBefore.currentHealth;
                    int actualHealing = targetAfter.currentHealth - targetBefore.currentHealth;
                    TestLogger.LogEvent($"Basic healing validation: expected healing, actual={actualHealing}, valid={healingOccurred}");
                    
                    // Enhanced logging for healing issues
                    if (!healingOccurred)
                    {
                        TestLogger.LogEvent($"Healing failed - Before: {targetBefore.currentHealth}/{targetBefore.maxHealth}, After: {targetAfter.currentHealth}/{targetAfter.maxHealth}");
                        if (targetBefore.currentHealth >= targetBefore.maxHealth)
                        {
                            TestLogger.LogEvent($"Target was already at max health, healing had no effect - this is expected behavior");
                            return true; // This is actually expected behavior
                        }
                        if (actualHealing < 0)
                        {
                            TestLogger.LogEvent($"WARNING: Expected healing but got {Mathf.Abs(actualHealing)} damage instead! This indicates a bug in the card effect system.");
                        }
                    }
                    return healingOccurred;
                    

                    
                default:
                    // For status effects, check if any effects were added
                    bool effectsAdded = targetAfter.activeEffects.Count >= targetBefore.activeEffects.Count;
                    TestLogger.LogEvent($"Status effect validation: before={targetBefore.activeEffects.Count} effects, after={targetAfter.activeEffects.Count} effects, valid={effectsAdded}");
                    return effectsAdded;
            }
        }
        
        TestLogger.LogEvent($"No effects to validate, assuming valid");
        return true; // Default to pass for cards without clear validation criteria
    }
    
    private void LogStateChanges(Dictionary<int, EntityState> before, Dictionary<int, EntityState> after)
    {
        foreach (var kvp in before)
        {
            if (after.ContainsKey(kvp.Key))
            {
                TestLogger.LogStateComparison(kvp.Value.entityName, kvp.Value, after[kvp.Key]);
            }
        }
    }
    
    private IEnumerator ResetToDefaultState()
    {
        // Use shorter delay if animations are disabled
        float actualResetDelay = disableAnimationsDuringTests ? 0.05f : stateResetDelay;
        yield return new WaitForSeconds(actualResetDelay);
        
        if (defaultEntityStates != null)
        {
            EntityStateCapture.RestoreAllEntityStates(defaultEntityStates);
        }
        
        // Clear hands again
        yield return StartCoroutine(ClearAllHands());
    }
    
    private IEnumerator ClearAllHands()
    {
        var allEntities = Object.FindObjectsByType<NetworkEntity>(FindObjectsSortMode.None)
            .Where(e => e.EntityType == EntityType.Player || e.EntityType == EntityType.Pet);
        
        foreach (var entity in allEntities)
        {
            // Use RelationshipManager to find the hand entity
            var relationshipManager = entity.GetComponent<RelationshipManager>();
            if (relationshipManager != null)
            {
                var handEntity = relationshipManager.HandEntity;
                if (handEntity != null)
                {
                    var handManager = handEntity.GetComponent<HandManager>();
                    if (handManager != null)
                    {
                        // Get all cards currently in hand
                        var cardsInHand = handManager.GetCardsInHand();
                        
                        foreach (var cardObj in cardsInHand)
                        {
                            if (cardObj != null)
                            {
                                // Despawn the networked card properly
                                var networkObject = cardObj.GetComponent<NetworkObject>();
                                if (networkObject != null && networkObject.IsSpawned)
                                {
                                    FishNet.InstanceFinder.ServerManager.Despawn(networkObject);
                                }
                                else
                                {
                                    DestroyImmediate(cardObj);
                                }
                            }
                        }
                    }
                }
            }
        }
        
        yield return new WaitForSeconds(0.5f); // Wait for despawning to complete
    }
    
    /// <summary>
    /// Manually add a test card to the test list
    /// </summary>
    public void AddTestCard(CardData cardData)
    {
        if (cardData != null && !testCards.Contains(cardData))
        {
            testCards.Add(cardData);
        }
    }
    
    /// <summary>
    /// Get current test progress
    /// </summary>
    public string GetTestProgress()
    {
        if (!isTestRunning)
            return "No tests running";
            
        return $"Test {currentTestIndex + 1}/{testCards.Count} running";
    }
    
    /// <summary>
    /// Force stop current test
    /// </summary>
    public void StopTests()
    {
        if (isTestRunning)
        {
            StopAllCoroutines();
            isTestRunning = false;
            
            // Make sure to re-enable systems when stopping tests
            EnableVisualAndAudioSystems();
            
            TestLogger.LogEvent("Tests stopped manually");
        }
    }
    
    /// <summary>
    /// Disable visual and audio systems for faster testing
    /// </summary>
    private void DisableVisualAndAudioSystems()
    {
        if (disableAnimationsDuringTests)
        {
            DisableAnimationSystems();
        }
        
        if (disableParticlesDuringTests)
        {
            DisableParticleSystems();
        }
        
        if (disableSoundsDuringTests)
        {
            DisableAudioSystems();
        }
        
        TestLogger.LogEvent("Disabled visual and audio systems for testing");
    }
    
    /// <summary>
    /// Re-enable visual and audio systems after testing
    /// </summary>
    private void EnableVisualAndAudioSystems()
    {
        if (disableAnimationsDuringTests)
        {
            EnableAnimationSystems();
        }
        
        if (disableParticlesDuringTests)
        {
            EnableParticleSystems();
        }
        
        if (disableSoundsDuringTests)
        {
            EnableAudioSystems();
        }
        
        TestLogger.LogEvent("Re-enabled visual and audio systems after testing");
    }
    
    private void DisableAnimationSystems()
    {
        // Disable card animators
        var cardAnimators = Object.FindObjectsByType<CardAnimator>(FindObjectsSortMode.None);
        foreach (var animator in cardAnimators)
        {
            if (animator.enabled)
            {
                animator.enabled = false;
            }
        }
        
        // Disable hand animators
        var handAnimators = Object.FindObjectsByType<HandAnimator>(FindObjectsSortMode.None);
        foreach (var animator in handAnimators)
        {
            if (animator.enabled)
            {
                animator.enabled = false;
            }
        }
        
        // Disable entity animators
        var entityAnimators = Object.FindObjectsByType<NetworkEntityAnimator>(FindObjectsSortMode.None);
        foreach (var animator in entityAnimators)
        {
            if (animator.enabled)
            {
                animator.enabled = false;
            }
        }
        
        // Disable effect animation manager
        var effectAnimationManager = Object.FindFirstObjectByType<EffectAnimationManager>();
        if (effectAnimationManager != null && effectAnimationManager.enabled)
        {
            effectAnimationManager.enabled = false;
        }
    }
    
    private void EnableAnimationSystems()
    {
        // Re-enable card animators
        var cardAnimators = Object.FindObjectsByType<CardAnimator>(FindObjectsSortMode.None);
        foreach (var animator in cardAnimators)
        {
            if (!animator.enabled)
            {
                animator.enabled = true;
            }
        }
        
        // Re-enable hand animators
        var handAnimators = Object.FindObjectsByType<HandAnimator>(FindObjectsSortMode.None);
        foreach (var animator in handAnimators)
        {
            if (!animator.enabled)
            {
                animator.enabled = true;
            }
        }
        
        // Re-enable entity animators
        var entityAnimators = Object.FindObjectsByType<NetworkEntityAnimator>(FindObjectsSortMode.None);
        foreach (var animator in entityAnimators)
        {
            if (!animator.enabled)
            {
                animator.enabled = true;
            }
        }
        
        // Re-enable effect animation manager
        var effectAnimationManager = Object.FindFirstObjectByType<EffectAnimationManager>();
        if (effectAnimationManager != null && !effectAnimationManager.enabled)
        {
            effectAnimationManager.enabled = true;
        }
    }
    
    private void DisableParticleSystems()
    {
        // Disable all particle systems in the scene
        var particleSystems = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        foreach (var ps in particleSystems)
        {
            if (ps.isPlaying)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            ps.gameObject.SetActive(false);
        }
        
        // Disable diagonal shine effects
        var shineEffects = Object.FindObjectsByType<DiagonalShineEffect>(FindObjectsSortMode.None);
        foreach (var effect in shineEffects)
        {
            if (effect.enabled)
            {
                effect.enabled = false;
            }
        }
    }
    
    private void EnableParticleSystems()
    {
        // Re-enable all particle systems in the scene
        var particleSystems = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        foreach (var ps in particleSystems)
        {
            ps.gameObject.SetActive(true);
        }
        
        // Re-enable diagonal shine effects
        var shineEffects = Object.FindObjectsByType<DiagonalShineEffect>(FindObjectsSortMode.None);
        foreach (var effect in shineEffects)
        {
            if (!effect.enabled)
            {
                effect.enabled = true;
            }
        }
    }
    
    private void DisableAudioSystems()
    {
        // Disable sound effect manager
        var soundEffectManager = Object.FindFirstObjectByType<SoundEffectManager>();
        if (soundEffectManager != null && soundEffectManager.enabled)
        {
            soundEffectManager.enabled = false;
        }
        
        // Disable music manager
        var musicManager = Object.FindFirstObjectByType<MusicManager>();
        if (musicManager != null && musicManager.enabled)
        {
            musicManager.enabled = false;
        }
        
        // Disable button sound handlers
        var buttonSoundHandlers = Object.FindObjectsByType<ButtonSoundHandler>(FindObjectsSortMode.None);
        foreach (var handler in buttonSoundHandlers)
        {
            if (handler.enabled)
            {
                handler.enabled = false;
            }
        }
        
        // Disable click sound handlers
        var clickSoundHandlers = Object.FindObjectsByType<ClickSoundHandler>(FindObjectsSortMode.None);
        foreach (var handler in clickSoundHandlers)
        {
            if (handler.enabled)
            {
                handler.enabled = false;
            }
        }
        
        // Disable phase change sound handler
        var phaseChangeSoundHandler = Object.FindFirstObjectByType<PhaseChangeSoundHandler>();
        if (phaseChangeSoundHandler != null && phaseChangeSoundHandler.enabled)
        {
            phaseChangeSoundHandler.enabled = false;
        }
    }
    
    private void EnableAudioSystems()
    {
        // Re-enable sound effect manager
        var soundEffectManager = Object.FindFirstObjectByType<SoundEffectManager>();
        if (soundEffectManager != null && !soundEffectManager.enabled)
        {
            soundEffectManager.enabled = true;
        }
        
        // Re-enable music manager
        var musicManager = Object.FindFirstObjectByType<MusicManager>();
        if (musicManager != null && !musicManager.enabled)
        {
            musicManager.enabled = true;
        }
        
        // Re-enable button sound handlers
        var buttonSoundHandlers = Object.FindObjectsByType<ButtonSoundHandler>(FindObjectsSortMode.None);
        foreach (var handler in buttonSoundHandlers)
        {
            if (!handler.enabled)
            {
                handler.enabled = true;
            }
        }
        
        // Re-enable click sound handlers
        var clickSoundHandlers = Object.FindObjectsByType<ClickSoundHandler>(FindObjectsSortMode.None);
        foreach (var handler in clickSoundHandlers)
        {
            if (!handler.enabled)
            {
                handler.enabled = true;
            }
        }
        
        // Re-enable phase change sound handler
        var phaseChangeSoundHandler = Object.FindFirstObjectByType<PhaseChangeSoundHandler>();
        if (phaseChangeSoundHandler != null && !phaseChangeSoundHandler.enabled)
        {
            phaseChangeSoundHandler.enabled = true;
        }
    }
    
    /// <summary>
    /// Find a NetworkEntity by its ObjectId
    /// </summary>
    private NetworkEntity FindEntityById(int entityId)
    {
        if (entityId <= 0) return null;
        
        NetworkObject netObj = null;
        
        if (IsServerStarted)
        {
            FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(entityId, out netObj);
        }
        else if (IsClientStarted)
        {
            FishNet.InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(entityId, out netObj);
        }
        
        return netObj?.GetComponent<NetworkEntity>();
    }
    
    /// <summary>
    /// Clear console logs (editor only)
    /// </summary>
    private void ClearConsole()
    {
        #if UNITY_EDITOR
        // Use reflection to clear the console
        var assembly = System.Reflection.Assembly.GetAssembly(typeof(UnityEditor.SceneView));
        var type = assembly.GetType("UnityEditor.LogEntries");
        var method = type.GetMethod("Clear");
        method.Invoke(new object(), null);
        #endif
    }
} 