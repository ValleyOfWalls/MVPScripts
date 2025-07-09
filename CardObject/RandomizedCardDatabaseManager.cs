using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;

/// <summary>
/// Manages the integration between procedural card generation and the CardDatabase.
/// Host generates cards and syncs them to all clients via NetworkCardDatabase.
/// Clients wait for synced cards from the host.
/// </summary>
public class RandomizedCardDatabaseManager : NetworkBehaviour
{
    [Header("Randomization Configuration")]
    [SerializeField, Tooltip("Configuration for randomized card generation")]
    private RandomCardConfig randomCardConfig;
    
    [Header("Generation Targets")]
    [SerializeField, Tooltip("Number of draftable cards to generate")]
    private int targetDraftableCards = 150;
    
    [SerializeField, Tooltip("Number of starter cards to generate per class")]
    private int starterCardsPerClass = 8;

    [SerializeField, Tooltip("Maximum number of unique cards per starter deck (the rest will be duplicates)")]
    private int maxUniqueCardsPerStarterDeck = 5;
    
    [Header("Character Classes")]
    [SerializeField, Tooltip("Character classes to generate starter decks for")]
    private string[] characterClasses = { "Warrior", "Mystic", "Assassin" };
    
    [Header("Pet Classes")]
    [SerializeField, Tooltip("Pet classes to generate starter decks for")]
    private string[] petClasses = { "Beast", "Elemental", "Spirit" };
    
    private ProceduralCardGenerator cardGenerator;
    private bool hasRandomized = false;
    
    // Events
    public static event System.Action OnClientRandomizationComplete;
    
    private void Awake()
    {
        Debug.Log("[RANDOMDB] RandomizedCardDatabaseManager.Awake() called");
        
        // Check if this GameObject has required components
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[RANDOMDB] ❌ Missing NetworkObject component! This will prevent network spawning.");
        }
        else
        {
            Debug.Log("[RANDOMDB] ✅ NetworkObject component found");
        }
        
        // Log GameObject and scene info
        Debug.Log($"[RANDOMDB] GameObject: {gameObject.name} in scene: {gameObject.scene.name}");
        Debug.Log($"[RANDOMDB] GameObject active: {gameObject.activeInHierarchy}");
    }
    
    private void Start()
    {
        Debug.Log("[RANDOMDB] RandomizedCardDatabaseManager.Start() called");
        
        // Check network state in Start
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null)
        {
            Debug.Log($"[RANDOMDB] NetworkObject state - IsSpawned: {netObj.IsSpawned}, IsServer: {netObj.IsServerInitialized}, IsClient: {netObj.IsClientInitialized}");
            Debug.Log($"[RANDOMDB] NetworkObject Owner: {(netObj.Owner != null ? netObj.Owner.ClientId.ToString() : "null")}");
        }
        
        // Check if network callbacks should have been called by now
        if (this is NetworkBehaviour)
        {
            Debug.Log($"[RANDOMDB] NetworkBehaviour state - IsServerInitialized: {IsServerInitialized}, IsClientInitialized: {IsClientInitialized}");
        }
        else
        {
            Debug.LogError("[RANDOMDB] ❌ This component is not properly networked.");
        }
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        Debug.Log("[RANDOMDB] 🌐 OnStartNetwork() called - Network connection established");
        Debug.Log($"[RANDOMDB] Network role - IsServer: {IsServerInitialized}, IsClient: {IsClientInitialized}, IsHost: {IsHostInitialized}");
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[RANDOMDB] 🖥️ RandomizedCardDatabaseManager.OnStartServer() called");
        Debug.Log("[RANDOMDB] This machine is acting as SERVER/HOST");
        
        // Only the server/host generates cards
        CheckAndInitializeRandomization();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log("[RANDOMDB] 💻 RandomizedCardDatabaseManager.OnStartClient() called");
        Debug.Log($"[RANDOMDB] This machine is acting as CLIENT - IsServer: {IsServerInitialized}");
        
        // Clients wait for cards to be synced from server
        if (!IsServerInitialized)
        {
            Debug.Log("[RANDOMDB] 👥 Pure client detected: Waiting for cards to be synced from host...");
            
            // Subscribe to NetworkCardDatabase events
            if (NetworkCardDatabase.Instance != null)
            {
                Debug.Log("[RANDOMDB] ✅ NetworkCardDatabase.Instance found, subscribing to sync events");
                NetworkCardDatabase.Instance.OnCardsSynced += OnCardsSyncedFromHost;
                NetworkCardDatabase.Instance.OnSyncProgress += OnSyncProgressUpdate;
            }
            else
            {
                Debug.LogError("[RANDOMDB] ❌ NetworkCardDatabase.Instance not found! Cards cannot be synced.");
                Debug.LogError("[RANDOMDB] This will cause each client to have different cards!");
            }
        }
        else
        {
            Debug.Log("[RANDOMDB] 👑 Host-client detected: Will generate cards and also receive them");
        }
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        Debug.Log("[RANDOMDB] 🔌 OnStopNetwork() called - Network connection lost");
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        Debug.Log("[RANDOMDB] 💻 OnStopClient() called");
        
        // Unsubscribe from events
        if (NetworkCardDatabase.Instance != null)
        {
            Debug.Log("[RANDOMDB] Unsubscribing from NetworkCardDatabase events");
            NetworkCardDatabase.Instance.OnCardsSynced -= OnCardsSyncedFromHost;
            NetworkCardDatabase.Instance.OnSyncProgress -= OnSyncProgressUpdate;
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        Debug.Log("[RANDOMDB] 🖥️ OnStopServer() called");
    }
    
    /// <summary>
    /// Check if randomization is enabled and initialize if needed (Server only)
    /// </summary>
    [Server]
    private void CheckAndInitializeRandomization()
    {
        Debug.Log("[RANDOMDB] CheckAndInitializeRandomization() called on server");
        
        if (hasRandomized)
        {
            Debug.Log("[RANDOMDB] Already randomized, skipping initialization");
            return;
        }

        // Get randomization setting from OfflineGameManager
        bool randomizationEnabled = false;
        if (OfflineGameManager.Instance != null)
        {
            randomizationEnabled = OfflineGameManager.Instance.EnableRandomizedCards;
            Debug.Log($"[RANDOMDB] Randomization setting from OfflineGameManager: {randomizationEnabled}");
        }
        else
        {
            Debug.LogWarning("[RANDOMDB] OfflineGameManager.Instance not found, defaulting to false");
        }

        if (randomizationEnabled)
        {
            Debug.Log("[RANDOMDB] Randomization enabled! Waiting for OnlineGameManager and then initializing...");
            StartCoroutine(WaitForOnlineGameManagerAndInitialize());
        }
        else
        {
            Debug.Log("[RANDOMDB] Randomization disabled, no action needed");
        }
    }
    
    /// <summary>
    /// Wait for OnlineGameManager to be spawned, then initialize randomization
    /// </summary>
    [Server]
    private System.Collections.IEnumerator WaitForOnlineGameManagerAndInitialize()
    {
        Debug.Log("[RANDOMDB] Waiting for OnlineGameManager to be spawned...");
        
        float timeout = 10f; // 10 second timeout
        float elapsed = 0f;
        
        // Wait for OnlineGameManager to be available
        while (OnlineGameManager.Instance == null && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        
        if (OnlineGameManager.Instance != null)
        {
            Debug.Log($"[RANDOMDB] OnlineGameManager found after {elapsed:F1}s! Updating randomization setting and initializing database...");
            
            // Update OnlineGameManager with randomization setting
            bool randomizationEnabled = OfflineGameManager.Instance.EnableRandomizedCards;
            OnlineGameManager.Instance.SetRandomizationEnabled(randomizationEnabled);
            Debug.Log($"[RANDOMDB] Updated OnlineGameManager randomization to {randomizationEnabled}");
            
            // Now initialize the database
            InitializeRandomizedDatabase();
        }
        else
        {
            Debug.LogError($"[RANDOMDB] Timeout waiting for OnlineGameManager after {timeout}s! Cannot initialize randomized database");
        }
    }

    /// <summary>
    /// Called when cards are synced from host (Client only)
    /// </summary>
    private void OnCardsSyncedFromHost()
    {
        Debug.Log("[RANDOMDB] Client: Cards have been synced from host!");
        
        // Start coroutine to properly wait for all cards and then update
        StartCoroutine(WaitForCardSyncAndUpdateDecks());
        
        hasRandomized = true;
    }

    /// <summary>
    /// Called during sync progress updates (Client only)
    /// </summary>
    private void OnSyncProgressUpdate(float progress)
    {
        Debug.Log($"[RANDOMDB] Client: Card sync progress: {progress:P0}");
    }
    
    /// <summary>
    /// Initialize the randomized card database if randomization is enabled (Server only)
    /// </summary>
    [Server]
    private void InitializeRandomizedDatabase()
    {
        Debug.Log("[RANDOMDB] InitializeRandomizedDatabase() called on server");
        
        if (hasRandomized)
        {
            Debug.Log("[RANDOMDB] Already randomized, skipping initialization");
            return;
        }

        if (randomCardConfig == null)
        {
            Debug.LogError("[RANDOMDB] ❌ No RandomCardConfig assigned! Cannot generate randomized cards");
            Debug.LogError("[RANDOMDB] Please assign the RandomCardConfig asset in the inspector!");
            return;
        }

        Debug.Log("[RANDOMDB] ✅ RandomCardConfig asset loaded successfully");
        Debug.Log($"[RANDOMDB] Config check - targetDraftableCards: {targetDraftableCards}, starterCardsPerClass: {starterCardsPerClass}, maxUniqueCardsPerStarterDeck: {maxUniqueCardsPerStarterDeck}");
        Debug.Log($"[RANDOMDB] Character classes: [{string.Join(", ", characterClasses)}]");
        Debug.Log($"[RANDOMDB] Pet classes: [{string.Join(", ", petClasses)}]");
        
        // Initialize the card generator (this will trigger configuration validation)
        Debug.Log("[RANDOMDB] Creating ProceduralCardGenerator with RandomCardConfig...");
        cardGenerator = new ProceduralCardGenerator(randomCardConfig);
        Debug.Log("[RANDOMDB] ✅ ProceduralCardGenerator initialized successfully");
        
        // Generate all card collections
        var generatedCards = GenerateAllCards();
        Debug.Log($"[RANDOMDB] Generated {generatedCards.totalCards} total cards");
        
        // Replace cards in the database (server-side first)
        ReplaceCardsInDatabase(generatedCards);
        Debug.Log("[RANDOMDB] Cards replaced in local database");
        
        // Replace starter deck references in CharacterData and PetData components (server-side)
        ReplaceStarterDeckReferences(generatedCards);
        Debug.Log("[RANDOMDB] Starter deck references replaced on server");
        
        // Sync cards to all clients via NetworkCardDatabase
        if (NetworkCardDatabase.Instance != null)
        {
            NetworkCardDatabase.Instance.SyncGeneratedCards(
                generatedCards.draftableCards, 
                generatedCards.starterCards, 
                generatedCards.upgradedCards
            );
            Debug.Log("[RANDOMDB] Cards sent to NetworkCardDatabase for client sync");
        }
        else
        {
            Debug.LogError("[RANDOMDB] NetworkCardDatabase.Instance not found! Cannot sync cards to clients");
        }
        
        hasRandomized = true;
        Debug.Log($"[RANDOMDB] ✓ SERVER RANDOMIZATION COMPLETE! Generated {generatedCards.totalCards} total cards");
    }
    
    /// <summary>
    /// Generate all types of cards needed for the game
    /// </summary>
    private GeneratedCardCollection GenerateAllCards()
    {
        var collection = new GeneratedCardCollection();
        
        // Generate draftable cards with rarity distribution
        collection.draftableCards = GenerateDraftableCards();
        Debug.Log($"Generated {collection.draftableCards.Count} draftable cards");
        
        // Generate starter cards for each character class
        collection.starterCards = GenerateStarterCards();
        Debug.Log($"Generated {collection.starterCards.Count} starter cards");
        
        // Generate upgraded versions for all base cards
        collection.upgradedCards = GenerateUpgradedVersions(collection.draftableCards, collection.starterCards);
        Debug.Log($"Generated {collection.upgradedCards.Count} upgraded cards");
        
        return collection;
    }
    
    /// <summary>
    /// Generate draftable cards using rarity distribution
    /// </summary>
    private List<CardData> GenerateDraftableCards()
    {
        var cards = new List<CardData>();
        
        if (randomCardConfig?.rarityDistributionConfig == null)
        {
            Debug.LogWarning("RandomizedCardDatabaseManager: Missing rarity distribution config, using defaults");
            // Generate with default distribution
            for (int i = 0; i < targetDraftableCards; i++)
            {
                var rarity = GetRandomRarityFallback();
                var card = cardGenerator.GenerateRandomCard(rarity);
                card.SetCardCategory(CardCategory.Draftable);
                cards.Add(card);
            }
            return cards;
        }
        
        var distConfig = randomCardConfig.rarityDistributionConfig;
        
        // Calculate how many of each rarity to generate
        int commonCount = Mathf.RoundToInt(targetDraftableCards * (distConfig.draftCommonPercentage / 100f));
        int uncommonCount = Mathf.RoundToInt(targetDraftableCards * (distConfig.draftUncommonPercentage / 100f));
        int rareCount = targetDraftableCards - commonCount - uncommonCount; // Remainder goes to rare
        
        Debug.Log($"Generating draftable cards: {commonCount} common, {uncommonCount} uncommon, {rareCount} rare");
        
        // Generate commons
        for (int i = 0; i < commonCount; i++)
        {
            var card = cardGenerator.GenerateRandomCard(CardRarity.Common);
            card.SetCardCategory(CardCategory.Draftable);
            cards.Add(card);
        }
        
        // Generate uncommons
        for (int i = 0; i < uncommonCount; i++)
        {
            var card = cardGenerator.GenerateRandomCard(CardRarity.Uncommon);
            card.SetCardCategory(CardCategory.Draftable);
            cards.Add(card);
        }
        
        // Generate rares
        for (int i = 0; i < rareCount; i++)
        {
            var card = cardGenerator.GenerateRandomCard(CardRarity.Rare);
            card.SetCardCategory(CardCategory.Draftable);
            cards.Add(card);
        }
        
        return cards;
    }
    
    /// <summary>
    /// Generate starter cards for all character and pet classes
    /// </summary>
    private List<CardData> GenerateStarterCards()
    {
        var cards = new List<CardData>();
        
        // Generate starter cards for character classes
        foreach (string characterClass in characterClasses)
        {
            var classCards = cardGenerator.GenerateThematicStarterDeck(characterClass, maxUniqueCardsPerStarterDeck);
            
            // Mark as starter cards
            foreach (var card in classCards)
            {
                card.SetCardCategory(CardCategory.Starter);
            }
            
            cards.AddRange(classCards);
            Debug.Log($"Generated {classCards.Count} starter cards for {characterClass} ({maxUniqueCardsPerStarterDeck} max unique)");
        }
        
        // Generate starter cards for pet classes
        foreach (string petClass in petClasses)
        {
            var classCards = cardGenerator.GenerateThematicStarterDeck(petClass, maxUniqueCardsPerStarterDeck);
            
            // Mark as starter cards
            foreach (var card in classCards)
            {
                card.SetCardCategory(CardCategory.Starter);
            }
            
            cards.AddRange(classCards);
            Debug.Log($"Generated {classCards.Count} starter cards for {petClass} ({maxUniqueCardsPerStarterDeck} max unique)");
        }
        
        return cards;
    }
    
    /// <summary>
    /// Generate upgraded versions for all base cards that can upgrade
    /// </summary>
    private List<CardData> GenerateUpgradedVersions(List<CardData> draftableCards, List<CardData> starterCards)
    {
        var upgradedCards = new List<CardData>();
        
        // Combine all base cards
        var allBaseCards = new List<CardData>();
        allBaseCards.AddRange(draftableCards);
        allBaseCards.AddRange(starterCards);
        
        foreach (var baseCard in allBaseCards)
        {
            if (baseCard.CanUpgrade)
            {
                var upgradedCard = GenerateUpgradedVersion(baseCard);
                if (upgradedCard != null)
                {
                    upgradedCards.Add(upgradedCard);
                    
                    // Link the base card to its upgrade using SetupUpgrade
                    baseCard.SetupUpgrade(upgradedCard, baseCard.UpgradeConditionType, 
                        baseCard.UpgradeRequiredValue, baseCard.UpgradeComparisonType);
                }
            }
        }
        
        return upgradedCards;
    }
    
    /// <summary>
    /// Generate an upgraded version of a base card
    /// </summary>
    private CardData GenerateUpgradedVersion(CardData baseCard)
    {
        var upgradedCard = ScriptableObject.CreateInstance<CardData>();
        
        // Copy base properties
        upgradedCard.SetCardId(GenerateUniqueUpgradeId(baseCard.CardId));
        upgradedCard.SetCardName(GetUpgradedName(baseCard.CardName));
        upgradedCard.SetRarity(baseCard.Rarity);
        upgradedCard.SetCardType(baseCard.CardType);
        upgradedCard.SetCardCategory(CardCategory.Upgraded);
        
        // Improve the card (reduce energy cost or enhance effects)
        int newEnergyCost = Mathf.Max(0, baseCard.EnergyCost - 1);
        upgradedCard.SetEnergyCost(newEnergyCost);
        
        // Enhanced effects (increase values by 25-50%)
        var enhancedEffects = new List<CardEffect>();
        foreach (var effect in baseCard.Effects)
        {
            var enhancedEffect = new CardEffect
            {
                effectType = effect.effectType,
                amount = Mathf.RoundToInt(effect.amount * UnityEngine.Random.Range(1.25f, 1.5f)),
                targetType = effect.targetType,
                duration = effect.duration,

                conditionType = effect.conditionType,
                conditionValue = effect.conditionValue
            };
            enhancedEffects.Add(enhancedEffect);
        }
        
        upgradedCard.SetEffects(enhancedEffects);
        
        // Generate enhanced description
        string enhancedDescription = GenerateUpgradedDescription(baseCard.Description, enhancedEffects);
        upgradedCard.SetDescription(enhancedDescription);
        
        // Upgraded cards cannot upgrade further
        upgradedCard.SetUpgradeProperties(false, UpgradeConditionType.TimesPlayedThisFight, 0, UpgradeComparisonType.GreaterThanOrEqual);
        
        return upgradedCard;
    }
    
    /// <summary>
    /// Replace all cards in the CardDatabase with generated ones
    /// </summary>
    private void ReplaceCardsInDatabase(GeneratedCardCollection generatedCards)
    {
        var database = CardDatabase.Instance;
        
        // Use reflection to access private fields
        var starterField = typeof(CardDatabase).GetField("starterCardsList", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var draftableField = typeof(CardDatabase).GetField("draftableCardsList", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var upgradedField = typeof(CardDatabase).GetField("upgradedCardsList", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (starterField == null || draftableField == null || upgradedField == null)
        {
            Debug.LogError("RandomizedCardDatabaseManager: Failed to access CardDatabase fields via reflection!");
            return;
        }
        
        // Replace the lists
        starterField.SetValue(database, generatedCards.starterCards);
        draftableField.SetValue(database, generatedCards.draftableCards);
        upgradedField.SetValue(database, generatedCards.upgradedCards);
        
        // Reinitialize the database to update the internal dictionary
        var initMethod = typeof(CardDatabase).GetMethod("InitializeDatabase", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (initMethod != null)
        {
            initMethod.Invoke(database, null);
        }
        else
        {
            Debug.LogError("RandomizedCardDatabaseManager: Failed to find InitializeDatabase method!");
        }
    }
    
    /// <summary>
    /// Replace starter deck references in CharacterData and PetData components
    /// </summary>
    private void ReplaceStarterDeckReferences(GeneratedCardCollection generatedCards)
    {
        Debug.Log("[RANDOMDB] ReplaceStarterDeckReferences() called");
        
        // Find the CharacterSelectionManager to access character and pet data
        CharacterSelectionManager selectionManager = FindFirstObjectByType<CharacterSelectionManager>();
        if (selectionManager == null)
        {
            Debug.LogError("[RANDOMDB] CharacterSelectionManager not found! Cannot replace starter decks.");
            Debug.Log("[RANDOMDB] This means character selection will still show hardcoded decks");
            return;
        }

        Debug.Log("[RANDOMDB] CharacterSelectionManager found, proceeding with deck replacement");

        // Get available characters and pets from the selection manager
        var availableCharacters = selectionManager.GetAvailableCharacters();
        var availablePets = selectionManager.GetAvailablePets();
        
        Debug.Log($"[RANDOMDB] Found {availableCharacters.Count} available characters, {availablePets.Count} available pets");
        Debug.Log($"[RANDOMDB] Generated {generatedCards.starterCards.Count} total starter cards");

        // Get theme-based names for characters
        var characterThemes = GetMysticalCharacterThemes();
        
        // Create character decks by distributing starter cards evenly
        int cardsPerCharacterDeck = starterCardsPerClass;
        int totalCharacterCards = generatedCards.starterCards.Count / 2; // Half for characters, half for pets
        
        Debug.Log($"[RANDOMDB] Creating character decks: {cardsPerCharacterDeck} cards per deck, {totalCharacterCards} total character cards available");
        
        for (int i = 0; i < availableCharacters.Count; i++)
        {
            var characterData = availableCharacters[i];
            
            // Take a slice of cards for this character
            int startIndex = i * cardsPerCharacterDeck;
            int endIndex = Mathf.Min(startIndex + cardsPerCharacterDeck, totalCharacterCards);
            
            var characterCards = generatedCards.starterCards
                .Skip(startIndex)
                .Take(endIndex - startIndex)
                .ToList();

            // Get thematic name based on card themes and position
            string thematicName = GetThematicNameForCards(characterCards, characterThemes, i);
            
            Debug.Log($"[RANDOMDB] Assigning character {i}: {characterData.CharacterName} → {thematicName} - cards {startIndex} to {endIndex-1} ({characterCards.Count} cards)");
            
            if (characterCards.Count > 0)
            {
                // Update character name to match theme
                UpdateCharacterName(characterData, thematicName);
                
                // Create new DeckData with procedural cards
                string deckName = $"{thematicName}'s Grimoire";
                var newDeckData = CreateDeckDataFromCards(deckName, characterCards);

                // Use reflection to replace the private starterDeck field
                ReplaceStarterDeckField(characterData, newDeckData);
                
                Debug.Log($"[RANDOMDB] ✓ Assigned {thematicName} with {characterCards.Count} mystical cards");
                Debug.Log($"[RANDOMDB] Sample cards: [{string.Join(", ", characterCards.Take(3).Select(c => c.CardName))}]");
                
                // Verify the replacement worked
                Debug.Log($"[RANDOMDB] Verification: New deck name = {characterData.StarterDeck?.DeckName}, card count = {characterData.StarterDeck?.CardsInDeck?.Count ?? 0}");
            }
            else
            {
                Debug.LogWarning($"[RANDOMDB] ⚠ No cards available for character slot {i}");
            }
        }

        // Get theme-based names for pets/familiars
        var petThemes = GetMysticalPetThemes();
        
        // Create pet decks by distributing remaining starter cards
        int cardsPerPetDeck = starterCardsPerClass;
        int petStartIndex = totalCharacterCards; // Start after character cards
        
        Debug.Log($"[RANDOMDB] Creating pet decks: {cardsPerPetDeck} cards per deck, starting from card index {petStartIndex}");
        
        for (int i = 0; i < availablePets.Count; i++)
        {
            var petData = availablePets[i];
            
            // Take a slice of cards for this pet
            int startIndex = petStartIndex + (i * cardsPerPetDeck);
            int endIndex = Mathf.Min(startIndex + cardsPerPetDeck, generatedCards.starterCards.Count);
            
            var petCards = generatedCards.starterCards
                .Skip(startIndex)
                .Take(endIndex - startIndex)
                .ToList();

            // Get thematic name based on card themes and position
            string thematicName = GetThematicNameForCards(petCards, petThemes, i);
            
            Debug.Log($"[RANDOMDB] Assigning pet {i}: {petData.PetName} → {thematicName} - cards {startIndex} to {endIndex-1} ({petCards.Count} cards)");

            if (petCards.Count > 0)
            {
                // Update pet name to match theme
                UpdatePetName(petData, thematicName);
                
                // Create new DeckData with procedural cards
                string deckName = $"{thematicName}'s Covenant";
                var newDeckData = CreateDeckDataFromCards(deckName, petCards);

                // Use reflection to replace the private starterDeck field
                ReplaceStarterDeckField(petData, newDeckData);
                
                Debug.Log($"[RANDOMDB] ✓ Bound {thematicName} with {petCards.Count} arcane cards");
                Debug.Log($"[RANDOMDB] Sample cards: [{string.Join(", ", petCards.Take(3).Select(c => c.CardName))}]");
                
                // Verify the replacement worked
                Debug.Log($"[RANDOMDB] Verification: New deck name = {petData.StarterDeck?.DeckName}, card count = {petData.StarterDeck?.CardsInDeck?.Count ?? 0}");
            }
            else
            {
                Debug.LogWarning($"[RANDOMDB] ⚠ No cards available for pet slot {i}");
            }
        }
        
        Debug.Log("[RANDOMDB] ReplaceStarterDeckReferences() completed");
    }

    /// <summary>
    /// Replace starter deck references using a list of starter cards (for clients)
    /// </summary>
    private void ReplaceStarterDeckReferencesFromCards(List<CardData> starterCards)
    {
        Debug.Log("[RANDOMDB] ReplaceStarterDeckReferencesFromCards() called");
        
        // Find CharacterSelectionManager
        var characterSelectionManager = FindFirstObjectByType<CharacterSelectionManager>();
        if (characterSelectionManager == null)
        {
            Debug.LogError("[RANDOMDB] CharacterSelectionManager not found!");
            return;
        }
        
        Debug.Log("[RANDOMDB] CharacterSelectionManager found, proceeding with deck replacement");
        
        var availableCharacters = characterSelectionManager.GetAvailableCharacters();
        var availablePets = characterSelectionManager.GetAvailablePets();
        
        Debug.Log($"[RANDOMDB] Found {availableCharacters.Count} available characters, {availablePets.Count} available pets");
        Debug.Log($"[RANDOMDB] Generated {starterCards.Count} total starter cards");
        
        // Distribute cards between characters and pets
        int cardsPerDeck = starterCardsPerClass;
        int totalCharacterCards = availableCharacters.Count * cardsPerDeck;
        
        Debug.Log($"[RANDOMDB] Creating character decks: {cardsPerDeck} cards per deck, {totalCharacterCards} total character cards available");
        
        // Assign character decks
        for (int i = 0; i < availableCharacters.Count; i++)
        {
            int startIndex = i * cardsPerDeck;
            int endIndex = startIndex + cardsPerDeck - 1;
            
            if (startIndex >= starterCards.Count)
            {
                Debug.LogWarning($"[RANDOMDB] ⚠ No cards available for character slot {i}");
                continue;
            }
            
            endIndex = Mathf.Min(endIndex, starterCards.Count - 1);
            int actualCardCount = endIndex - startIndex + 1;
            
            Debug.Log($"[RANDOMDB] Assigning character {i}: {availableCharacters[i].CharacterName} → using cards {startIndex} to {endIndex} ({actualCardCount} cards)");
            
            if (actualCardCount <= 0)
            {
                Debug.LogWarning($"[RANDOMDB] ⚠ No cards available for character slot {i}");
                continue;
            }
            
            var characterCards = starterCards.GetRange(startIndex, actualCardCount);
            
            // Generate mystical name for character
            var characterThemes = GetMysticalCharacterThemes();
            string newCharacterName = GetThematicNameForCards(characterCards, characterThemes, i);
            
            // Update character name
            Debug.Log($"[RANDOMDB] ✓ Updated character name to: {newCharacterName}");
            UpdateCharacterName(availableCharacters[i], newCharacterName);
            
            // Create deck for character
            string deckName = $"{newCharacterName}'s Grimoire";
            var characterDeck = CreateDeckDataFromCards(deckName, characterCards);
            
            // Replace the starter deck field via reflection
            ReplaceStarterDeckField(availableCharacters[i], characterDeck);
            
            Debug.Log($"[RANDOMDB] ✓ Assigned {newCharacterName} with {characterCards.Count} mystical cards");
            Debug.Log($"[RANDOMDB] Sample cards: [{string.Join(", ", characterCards.Take(3).Select(c => c.CardName))}]");
            Debug.Log($"[RANDOMDB] Verification: New deck name = {characterDeck.DeckName}, card count = {characterDeck.CardsInDeck.Count}");
        }
        
        // Calculate pet card distribution
        int petStartIndex = totalCharacterCards;
        Debug.Log($"[RANDOMDB] Creating pet decks: {cardsPerDeck} cards per deck, starting from card index {petStartIndex}");
        
        // Assign pet decks
        for (int i = 0; i < availablePets.Count; i++)
        {
            int startIndex = petStartIndex + (i * cardsPerDeck);
            int endIndex = startIndex + cardsPerDeck - 1;
            
            if (startIndex >= starterCards.Count)
            {
                Debug.LogWarning($"[RANDOMDB] ⚠ No cards available for pet slot {i}");
                continue;
            }
            
            endIndex = Mathf.Min(endIndex, starterCards.Count - 1);
            int actualCardCount = endIndex - startIndex + 1;
            
            Debug.Log($"[RANDOMDB] Assigning pet {i}: {availablePets[i].PetName} → using cards {startIndex} to {endIndex} ({actualCardCount} cards)");
            
            if (actualCardCount <= 0)
            {
                Debug.LogWarning($"[RANDOMDB] ⚠ No cards available for pet slot {i}");
                continue;
            }
            
            var petCards = starterCards.GetRange(startIndex, actualCardCount);
            
            // Generate mystical name for pet
            var petThemes = GetMysticalPetThemes();
            string newPetName = GetThematicNameForCards(petCards, petThemes, i);
            
            // Update pet name  
            Debug.Log($"[RANDOMDB] ✓ Updated pet name to: {newPetName}");
            UpdatePetName(availablePets[i], newPetName);
            
            // Create deck for pet
            string deckName = $"{newPetName}'s Collection";
            var petDeck = CreateDeckDataFromCards(deckName, petCards);
            
            // Replace the starter deck field via reflection
            ReplaceStarterDeckField(availablePets[i], petDeck);
            
            Debug.Log($"[RANDOMDB] ✓ Assigned {newPetName} with {petCards.Count} mystical cards");
            Debug.Log($"[RANDOMDB] Sample cards: [{string.Join(", ", petCards.Take(3).Select(c => c.CardName))}]");
            Debug.Log($"[RANDOMDB] Verification: New deck name = {petDeck.DeckName}, card count = {petDeck.CardsInDeck.Count}");
        }
        
        Debug.Log("[RANDOMDB] ReplaceStarterDeckReferencesFromCards() completed");
    }

    /// <summary>
    /// Check if a card belongs to a specific class based on its name or description
    /// </summary>
    private bool IsCardForClass(CardData card, string className)
    {
        string cardName = card.CardName.ToLower();
        string cardDesc = card.Description.ToLower();
        string lowerClassName = className.ToLower();

        // Check if the card name or description contains class-specific keywords
        return cardName.Contains(lowerClassName) || cardDesc.Contains(lowerClassName) ||
               cardName.Contains(GetClassKeyword(className)) || cardDesc.Contains(GetClassKeyword(className));
    }

    /// <summary>
    /// Get a keyword associated with a class for card identification
    /// </summary>
    private string GetClassKeyword(string className)
    {
        return className.ToLower() switch
        {
            "warrior" => "strength",
            "mystic" => "arcane",
            "assassin" => "stealth",
            "beast" => "primal",
            "elemental" => "elemental",
            "spirit" => "spiritual",
            _ => className.ToLower()
        };
    }

    /// <summary>
    /// Create a DeckData ScriptableObject from a list of cards
    /// </summary>
    private DeckData CreateDeckDataFromCards(string deckName, List<CardData> cards)
    {
        var deckData = ScriptableObject.CreateInstance<DeckData>();
        
        // Use reflection to set the private fields
        var nameField = typeof(DeckData).GetField("_deckName", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cardsField = typeof(DeckData).GetField("_cardsInDeck", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (nameField != null)
            nameField.SetValue(deckData, deckName);
        else
            Debug.LogError("Could not find _deckName field in DeckData");
            
        if (cardsField != null)
            cardsField.SetValue(deckData, new List<CardData>(cards));
        else
            Debug.LogError("Could not find _cardsInDeck field in DeckData");
        
        return deckData;
    }

    /// <summary>
    /// Use reflection to replace the private starterDeck field in CharacterData or PetData
    /// </summary>
    private void ReplaceStarterDeckField(MonoBehaviour dataComponent, DeckData newDeckData)
    {
        Debug.Log($"[RANDOMDB] Attempting to replace starterDeck field in {dataComponent.GetType().Name}");
        
        var field = dataComponent.GetType().GetField("starterDeck", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            var oldValue = field.GetValue(dataComponent) as DeckData;
            Debug.Log($"[RANDOMDB] Found starterDeck field. Old value: {oldValue?.DeckName ?? "null"}");
            
            field.SetValue(dataComponent, newDeckData);
            Debug.Log($"[RANDOMDB] ✓ Successfully set starterDeck field to: {newDeckData.DeckName}");
        }
        else
        {
            Debug.LogError($"[RANDOMDB] ✗ Could not find starterDeck field in {dataComponent.GetType().Name}");
            
            // Log all available fields for debugging
            var allFields = dataComponent.GetType().GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Debug.Log($"[RANDOMDB] Available private fields: [{string.Join(", ", allFields.Select(f => f.Name))}]");
        }
    }
    
    /// <summary>
    /// Get a random rarity when configuration is not available
    /// </summary>
    private CardRarity GetRandomRarityFallback()
    {
        float random = UnityEngine.Random.value;
        if (random < 0.7f) return CardRarity.Common;
        if (random < 0.9f) return CardRarity.Uncommon;
        return CardRarity.Rare;
    }
    
    /// <summary>
    /// Generate a unique ID for an upgraded card
    /// </summary>
    private int GenerateUniqueUpgradeId(int baseCardId)
    {
        // Add a large offset to ensure upgraded IDs don't conflict with base IDs
        return baseCardId + 10000;
    }
    
    /// <summary>
    /// Generate an upgraded name for a card
    /// </summary>
    private string GetUpgradedName(string baseName)
    {
        var prefixes = new[] { "Superior", "Enhanced", "Perfected", "Master's", "Elite", "Primal" };
        string prefix = prefixes[UnityEngine.Random.Range(0, prefixes.Length)];
        return $"{prefix} {baseName}";
    }
    
    /// <summary>
    /// Generate an enhanced description for an upgraded card
    /// </summary>
    private string GenerateUpgradedDescription(string baseDescription, List<CardEffect> enhancedEffects)
    {
        // For now, just add "(Enhanced)" suffix
        // Could be more sophisticated by parsing and updating values
        return baseDescription + " (Enhanced)";
    }
    
    /// <summary>
    /// Public method to force regeneration (useful for testing)
    /// </summary>
    /// <summary>
    /// Update starter deck references using synced cards (Client only)
    /// </summary>
    private void UpdateStarterDeckReferencesFromSyncedCards()
    {
        Debug.Log("[RANDOMDB] Client: Updating starter deck references from synced cards");
        
        if (NetworkCardDatabase.Instance == null)
        {
            Debug.LogError("[RANDOMDB] Client: NetworkCardDatabase.Instance not found!");
            return;
        }
        
        // Get synced starter cards
        var starterCards = NetworkCardDatabase.Instance.GetSyncedCardsByCategory(CardCategory.Starter);
        Debug.Log($"[RANDOMDB] Client: Found {starterCards.Count} synced starter cards");
        
        // Create a mock GeneratedCardCollection for the existing method
        var generatedCards = new GeneratedCardCollection
        {
            starterCards = starterCards,
            draftableCards = NetworkCardDatabase.Instance.GetSyncedCardsByCategory(CardCategory.Draftable),
            upgradedCards = NetworkCardDatabase.Instance.GetSyncedCardsByCategory(CardCategory.Upgraded)
        };
        
        // Use existing method to replace starter deck references
        ReplaceStarterDeckReferences(generatedCards);
        Debug.Log("[RANDOMDB] Client: Starter deck references updated from synced cards");
    }

    /// <summary>
    /// Client waits for cards to be synced, then updates starter deck references
    /// </summary>
    private System.Collections.IEnumerator WaitForCardSyncAndUpdateDecks()
    {
        Debug.Log("[RANDOMDB] Client: Waiting for cards to be synced from host...");
        
        // Wait for NetworkCardDatabase to be ready
        while (NetworkCardDatabase.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        Debug.Log("[RANDOMDB] ✅ NetworkCardDatabase.Instance found, subscribing to sync events");
        
        // Wait for sync to complete - check every frame until we have cards
        int expectedCardCount = 220; // Total cards generated by host
        int timeoutSeconds = 30;
        float startTime = Time.time;
        
        while (NetworkCardDatabase.Instance.GetTotalSyncedCardCount() < expectedCardCount)
        {
            float elapsed = Time.time - startTime;
            if (elapsed > timeoutSeconds)
            {
                Debug.LogError($"[RANDOMDB] ❌ Timeout waiting for card sync! Only received {NetworkCardDatabase.Instance.GetTotalSyncedCardCount()}/{expectedCardCount} cards after {timeoutSeconds}s");
                break;
            }
            
            int currentCount = NetworkCardDatabase.Instance.GetTotalSyncedCardCount();
            float progress = (float)currentCount / expectedCardCount * 100f;
            Debug.Log($"[RANDOMDB] Client: Card sync progress: {currentCount}/{expectedCardCount} ({progress:F0}%)");
            
            yield return new WaitForSeconds(0.5f); // Check every half second
        }
        
        // Final verification
        int finalCount = NetworkCardDatabase.Instance.GetTotalSyncedCardCount();
        Debug.Log($"[RANDOMDB] Client: Card sync completed! Received {finalCount} total cards");
        
        // Now safely access the cards
        var starterCards = NetworkCardDatabase.Instance.GetSyncedCardsByCategory(CardCategory.Starter);
        var draftableCards = NetworkCardDatabase.Instance.GetSyncedCardsByCategory(CardCategory.Draftable);
        var upgradedCards = NetworkCardDatabase.Instance.GetSyncedCardsByCategory(CardCategory.Upgraded);
        
        Debug.Log($"[RANDOMDB] Client: Found {starterCards.Count} starter cards, {draftableCards.Count} draftable cards, {upgradedCards.Count} upgraded cards");
        
        if (starterCards.Count == 0)
        {
            Debug.LogError("[RANDOMDB] ❌ No starter cards received! Cannot update character selection.");
            yield break;
        }
        
        Debug.Log("[RANDOMDB] Client: Updating starter deck references from synced cards");
        ReplaceStarterDeckReferencesFromCards(starterCards);
        
        Debug.Log("[RANDOMDB] Client: Starter deck references updated from synced cards");
        Debug.Log("[RANDOMDB] Client: Randomization complete!");
        
        // Notify UI systems that randomization is complete
        OnClientRandomizationComplete?.Invoke();
    }

    [ContextMenu("Force Regenerate Cards")]
    public void ForceRegenerateCards()
    {
        if (IsServerInitialized)
        {
            hasRandomized = false;
            InitializeRandomizedDatabase();
        }
        else
        {
            Debug.LogWarning("ForceRegenerateCards can only be called on the server/host");
        }
    }
    
    /// <summary>
    /// Debug method to validate RandomizedCardDatabaseManager setup
    /// </summary>
    [ContextMenu("Debug: Validate Network Setup")]
    public void ValidateNetworkSetup()
    {
        Debug.Log("=== RANDOMIZED CARD DATABASE MANAGER NETWORK VALIDATION ===");
        
        // Check GameObject
        Debug.Log($"GameObject Name: {gameObject.name}");
        Debug.Log($"Scene: {gameObject.scene.name}");
        Debug.Log($"Active in Hierarchy: {gameObject.activeInHierarchy}");
        Debug.Log($"Tag: {gameObject.tag}");
        
        // Check NetworkObject component
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("❌ CRITICAL: Missing NetworkObject component!");
            Debug.LogError("   Fix: Add NetworkObject component to this GameObject");
        }
        else
        {
            Debug.Log("✅ NetworkObject component present");
            Debug.Log($"   NetworkObject.ObjectId: {netObj.ObjectId}");
            Debug.Log($"   NetworkObject.IsSpawned: {netObj.IsSpawned}");
            Debug.Log($"   NetworkObject.IsSceneObject: {netObj.IsSceneObject}");
            
            if (Application.isPlaying)
            {
                Debug.Log($"   NetworkObject.IsServerInitialized: {netObj.IsServerInitialized}");
                Debug.Log($"   NetworkObject.IsClientInitialized: {netObj.IsClientInitialized}");
                Debug.Log($"   NetworkObject.Owner: {(netObj.Owner != null ? netObj.Owner.ClientId.ToString() : "null")}");
            }
        }
        
        // Check NetworkBehaviour state
        if (Application.isPlaying)
        {
            Debug.Log($"NetworkBehaviour.IsServerInitialized: {IsServerInitialized}");
            Debug.Log($"NetworkBehaviour.IsClientInitialized: {IsClientInitialized}");
            Debug.Log($"NetworkBehaviour.IsHostInitialized: {IsHostInitialized}");
        }
        
        // Check RandomCardConfig assignment
        if (randomCardConfig == null)
        {
            Debug.LogError("❌ RandomCardConfig not assigned!");
            Debug.LogError("   Fix: Assign RandomCardConfig asset in inspector");
        }
        else
        {
            Debug.Log("✅ RandomCardConfig assigned");
            Debug.Log($"   Config name: {randomCardConfig.name}");
        }
        
        // Check if this is a scene object that should auto-spawn
        if (netObj != null && netObj.IsSceneObject)
        {
            Debug.Log("✅ This is a scene object - should auto-spawn with network");
        }
        else if (netObj != null)
        {
            Debug.LogWarning("⚠️ This is a prefab object - needs manual spawning");
            Debug.LogWarning("   Make sure this prefab is spawned on network start");
        }
        
        Debug.Log("=== END NETWORK VALIDATION ===");
    }
    
    [ContextMenu("Debug: Show Current Starter Decks")]
    public void DebugShowCurrentStarterDecks()
    {
        Debug.Log("=== CURRENT STARTER DECKS ===");
        
        CharacterSelectionManager selectionManager = FindFirstObjectByType<CharacterSelectionManager>();
        if (selectionManager == null)
        {
            Debug.LogWarning("CharacterSelectionManager not found!");
            return;
        }

        var availableCharacters = selectionManager.GetAvailableCharacters();
        var availablePets = selectionManager.GetAvailablePets();
        
        Debug.Log("CHARACTER STARTER DECKS:");
        for (int i = 0; i < availableCharacters.Count; i++)
        {
            var character = availableCharacters[i];
            var deck = character.StarterDeck;
            if (deck != null && deck.CardsInDeck != null)
            {
                Debug.Log($"  {character.CharacterName}: {deck.DeckName} ({deck.CardsInDeck.Count} cards)");
                for (int j = 0; j < deck.CardsInDeck.Count; j++)
                {
                    var card = deck.CardsInDeck[j];
                    Debug.Log($"    {j+1}. {card.CardName} (Cost: {card.EnergyCost}, Rarity: {card.Rarity})");
                }
            }
            else
            {
                Debug.Log($"  {character.CharacterName}: NO DECK ASSIGNED");
            }
        }
        
        Debug.Log("\nPET STARTER DECKS:");
        for (int i = 0; i < availablePets.Count; i++)
        {
            var pet = availablePets[i];
            var deck = pet.StarterDeck;
            if (deck != null && deck.CardsInDeck != null)
            {
                Debug.Log($"  {pet.PetName}: {deck.DeckName} ({deck.CardsInDeck.Count} cards)");
                for (int j = 0; j < deck.CardsInDeck.Count; j++)
                {
                    var card = deck.CardsInDeck[j];
                    Debug.Log($"    {j+1}. {card.CardName} (Cost: {card.EnergyCost}, Rarity: {card.Rarity})");
                }
            }
            else
            {
                Debug.Log($"  {pet.PetName}: NO DECK ASSIGNED");
            }
        }
        
        Debug.Log("=== END STARTER DECKS ===");
    }
    
    /// <summary>
    /// Get mystical character theme names inspired by occult orders and cosmic entities
    /// </summary>
    private List<string> GetMysticalCharacterThemes()
    {
        return new List<string>
        {
            // Ember/Fire Themes
            "Acolyte of the Ember Choir", "Cindervoice Hierophant", "Flameheart Songweaver",
            "Speaker of Burning Psalms", "Ashborn Cantor", "Pyroclastic Oracle",
            
            // Shadow/Void Themes  
            "Scribe of the Umbral Lexicon", "Voidinked Scholar", "Shadowtext Archivist",
            "Nihilistic Wordsmith", "Inkwell Heretic", "Glyph-Eater Apostate",
            
            // Cosmic/Star Themes
            "Starwarden of the Living Cradle", "Nebular Midwife", "Constellation Shaper",
            "Stellar Geometrist", "Cosmic Birthwright", "Supernova Sage",
            
            // Nature/Decay Themes
            "Verdant Pact-Bearer", "Sporebound Druid", "Mycelial Symbiant", 
            "Fleshroot Cultivator", "Parasitic Vanguard", "Rot-Singer",
            
            // Time/Clockwork Themes
            "Orrery Apostate", "Chronoshear Heretic", "Pendulum Oracle",
            "Clockwork Fateshaper", "Temporal Mechanist", "Hourglass Keeper",
            
            // Artifice/Metal Themes
            "Pale Emberwwright", "Brass-Seal Forgemaster", "Golem-Heart Artificer",
            "Burnished Architect", "Molten Artificer", "Construct-Binder",
            
            // Blood/Pact Themes
            "Thorncoven Ascendant", "Bone-Debt Collector", "Hollow Crown Bearer",
            "Oathroot Witch", "Blood-Price Oracle", "Barbed Favor Seeker"
        };
    }
    
    /// <summary>
    /// Get mystical pet/familiar theme names
    /// </summary>
    private List<string> GetMysticalPetThemes()
    {
        return new List<string>
        {
            // Ember/Fire Familiars
            "Cinder-Sworn Familiar", "Ashen Choir-Beast", "Emberheart Companion",
            "Flame-Tongue Sprite", "Smoke-Wreath Wisp", "Burning Psalm-Singer",
            
            // Shadow/Void Familiars
            "Ink-Bound Shade", "Umbral Text-Walker", "Void-Script Whisper",
            "Shadow-Word Devourer", "Null-Glyph Stalker", "Dark-Page Sentinel",
            
            // Cosmic/Star Familiars  
            "Star-Nursery Guardian", "Nebula-Born Watcher", "Cosmic Dust-Dancer",
            "Constellation Hound", "Stellar Wind-Rider", "Gravity-Well Keeper",
            
            // Nature/Decay Familiars
            "Spore-Cloud Drifter", "Mycelial Network-Node", "Root-Tangle Creeper",
            "Parasitic Symbiont", "Decay-Breath Hound", "Fleshvine Crawler",
            
            // Time/Clockwork Familiars
            "Clockwork Chronofox", "Pendulum-Tail Cat", "Temporal Gear-Hound",
            "Hourglass Sand-Spirit", "Mechanized Time-Mote", "Tick-Tock Wraith",
            
            // Artifice/Metal Familiars
            "Brass-Bound Construct", "Molten-Core Homunculus", "Forge-Fire Salamander",
            "Living Seal-Keeper", "Burnished Metal-Sprite", "Ember-Wrought Golem",
            
            // Blood/Pact Familiars
            "Thorn-Crown Imp", "Blood-Debt Collector", "Bone-Shard Familiar",
            "Oathroot Tendril", "Hollow-Crown Fetch", "Barbed-Wire Wisp"
        };
    }
    
    /// <summary>
    /// Get a thematic name based on card themes and available names
    /// </summary>
    private string GetThematicNameForCards(List<CardData> cards, List<string> availableThemes, int slotIndex)
    {
        // For now, cycle through themes based on slot index to ensure variety
        // Later could analyze card names/effects to pick most fitting theme
        return availableThemes[slotIndex % availableThemes.Count];
    }
    
    /// <summary>
    /// Update character name using reflection
    /// </summary>
    private void UpdateCharacterName(CharacterData characterData, string newName)
    {
        var nameField = typeof(CharacterData).GetField("characterName", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
        if (nameField != null)
        {
            nameField.SetValue(characterData, newName);
            Debug.Log($"[RANDOMDB] ✓ Updated character name to: {newName}");
        }
        else
        {
            Debug.LogWarning($"[RANDOMDB] Could not find characterName field to update");
        }
    }
    
    /// <summary>
    /// Update pet name using reflection
    /// </summary>
    private void UpdatePetName(PetData petData, string newName)
    {
        var nameField = typeof(PetData).GetField("petName", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
        if (nameField != null)
        {
            nameField.SetValue(petData, newName);
            Debug.Log($"[RANDOMDB] ✓ Updated pet name to: {newName}");
        }
        else
        {
            Debug.LogWarning($"[RANDOMDB] Could not find petName field to update");
        }
    }
}

/// <summary>
/// Container for all generated card collections
/// </summary>
public class GeneratedCardCollection
{
    public List<CardData> starterCards = new List<CardData>();
    public List<CardData> draftableCards = new List<CardData>();
    public List<CardData> upgradedCards = new List<CardData>();
    
    public int totalCards => starterCards.Count + draftableCards.Count + upgradedCards.Count;
} 