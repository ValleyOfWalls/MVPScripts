using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class CardPoolAssigner : EditorWindow
{
    private GameManager gameManager;
    private List<CardData> allCards = new List<CardData>();
    private List<CardData> playerStarterCards = new List<CardData>();
    private List<CardData> petStarterCards = new List<CardData>();
    
    private Vector2 scrollPosition;
    private bool showStarterCards = true;

    [MenuItem("Game/Card Pool Assigner")]
    public static void ShowWindow()
    {
        GetWindow<CardPoolAssigner>("Card Pool Assigner");
    }

    private void OnEnable()
    {
        FindGameManager();
        LoadAllCards();
        SetupDefaultStarterCards();
    }

    private void FindGameManager()
    {
        GameObject gameManagerObj = GameObject.Find("GameManager");
        if (gameManagerObj != null)
        {
            gameManager = gameManagerObj.GetComponent<GameManager>();
        }
    }

    private void LoadAllCards()
    {
        allCards.Clear();
        string[] guids = AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Cards" });
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            
            // Only include unupgraded cards (those that don't have "+" in their file name)
            if (!path.Contains("+"))
            {
                allCards.Add(card);
            }
        }
    }

    private void SetupDefaultStarterCards()
    {
        playerStarterCards.Clear();
        petStarterCards.Clear();
        
        // Find recommended starter cards for player
        foreach (CardData card in allCards)
        {
            if (card.cardName == "Botanical Lacerator" || 
                card.cardName == "Sporal Carapace" || 
                card.cardName == "Chromomantic Insight")
            {
                playerStarterCards.Add(card);
            }
        }
        
        // Find recommended starter cards for pet
        foreach (CardData card in allCards)
        {
            if (card.cardName == "Botanical Lacerator" || 
                card.cardName == "Sporal Carapace" || 
                card.cardName == "Irradiant Spores")
            {
                petStarterCards.Add(card);
            }
        }
    }

    private void OnGUI()
    {
        if (gameManager == null)
        {
            EditorGUILayout.HelpBox("GameManager not found in the scene. Make sure a GameManager object exists with a GameManager component.", MessageType.Error);
            if (GUILayout.Button("Find GameManager"))
            {
                FindGameManager();
            }
            return;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Card Pool Assignment", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Assign All Unupgraded Cards to Both Pools"))
        {
            AssignCardsToBothPools();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Assign to Player Pool Only"))
        {
            AssignCardsToPlayerPool();
        }
        if (GUILayout.Button("Assign to Pet Pool Only"))
        {
            AssignCardsToPetPool();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Testing Options", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Use these buttons to add all cards to starter decks for testing.", MessageType.Info);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add ALL Cards to Player Starter Deck"))
        {
            AssignAllCardsToPlayerStarterDeck();
        }
        if (GUILayout.Button("Add ALL Cards to Pet Starter Deck"))
        {
            AssignAllCardsToPetStarterDeck();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        showStarterCards = EditorGUILayout.Foldout(showStarterCards, "Recommended Starter Cards");
        
        if (showStarterCards)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.LabelField("Player Starter Deck Recommendation:", EditorStyles.boldLabel);
            foreach (CardData card in playerStarterCards)
            {
                EditorGUILayout.LabelField("• " + card.cardName + " - " + card.description);
            }
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.LabelField("Pet Starter Deck Recommendation:", EditorStyles.boldLabel);
            foreach (CardData card in petStarterCards)
            {
                EditorGUILayout.LabelField("• " + card.cardName + " - " + card.description);
            }
            
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Assign Recommended Player Starter Cards"))
            {
                AssignPlayerStarterCards();
            }
            
            if (GUILayout.Button("Assign Recommended Pet Starter Cards"))
            {
                AssignPetStarterCards();
            }
            
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Card List", EditorStyles.boldLabel);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        foreach (CardData card in allCards)
        {
            EditorGUILayout.LabelField(card.cardName + " - " + card.description);
        }
        EditorGUILayout.EndScrollView();
    }

    private void AssignCardsToBothPools()
    {
        // Using SerializedObject to modify the serialized fields
        SerializedObject serializedManager = new SerializedObject(gameManager);
        
        // Find and modify the properties
        SerializedProperty playerCardPoolProperty = serializedManager.FindProperty("allPlayerCardPool");
        SerializedProperty petCardPoolProperty = serializedManager.FindProperty("allPetCardPool");
        
        // Clear and set the card lists
        playerCardPoolProperty.ClearArray();
        petCardPoolProperty.ClearArray();
        
        for (int i = 0; i < allCards.Count; i++)
        {
            // Add to player pool
            playerCardPoolProperty.arraySize++;
            playerCardPoolProperty.GetArrayElementAtIndex(playerCardPoolProperty.arraySize - 1).objectReferenceValue = allCards[i];
            
            // Add to pet pool
            petCardPoolProperty.arraySize++;
            petCardPoolProperty.GetArrayElementAtIndex(petCardPoolProperty.arraySize - 1).objectReferenceValue = allCards[i];
        }
        
        // Apply the changes
        serializedManager.ApplyModifiedProperties();
        
        Debug.Log($"Assigned {allCards.Count} cards to both player and pet card pools.");
    }

    private void AssignCardsToPlayerPool()
    {
        SerializedObject serializedManager = new SerializedObject(gameManager);
        SerializedProperty playerCardPoolProperty = serializedManager.FindProperty("allPlayerCardPool");
        
        playerCardPoolProperty.ClearArray();
        
        for (int i = 0; i < allCards.Count; i++)
        {
            playerCardPoolProperty.arraySize++;
            playerCardPoolProperty.GetArrayElementAtIndex(playerCardPoolProperty.arraySize - 1).objectReferenceValue = allCards[i];
        }
        
        serializedManager.ApplyModifiedProperties();
        
        Debug.Log($"Assigned {allCards.Count} cards to player card pool.");
    }

    private void AssignCardsToPetPool()
    {
        SerializedObject serializedManager = new SerializedObject(gameManager);
        SerializedProperty petCardPoolProperty = serializedManager.FindProperty("allPetCardPool");
        
        petCardPoolProperty.ClearArray();
        
        for (int i = 0; i < allCards.Count; i++)
        {
            petCardPoolProperty.arraySize++;
            petCardPoolProperty.GetArrayElementAtIndex(petCardPoolProperty.arraySize - 1).objectReferenceValue = allCards[i];
        }
        
        serializedManager.ApplyModifiedProperties();
        
        Debug.Log($"Assigned {allCards.Count} cards to pet card pool.");
    }
    
    private void AssignPlayerStarterCards()
    {
        SerializedObject serializedManager = new SerializedObject(gameManager);
        SerializedProperty starterDeckProperty = serializedManager.FindProperty("starterDeck");
        
        starterDeckProperty.ClearArray();
        
        for (int i = 0; i < playerStarterCards.Count; i++)
        {
            starterDeckProperty.arraySize++;
            starterDeckProperty.GetArrayElementAtIndex(starterDeckProperty.arraySize - 1).objectReferenceValue = playerStarterCards[i];
        }
        
        serializedManager.ApplyModifiedProperties();
        
        Debug.Log($"Assigned {playerStarterCards.Count} cards to player starter deck.");
    }
    
    private void AssignPetStarterCards()
    {
        SerializedObject serializedManager = new SerializedObject(gameManager);
        SerializedProperty starterPetDeckProperty = serializedManager.FindProperty("starterPetDeck");
        
        starterPetDeckProperty.ClearArray();
        
        for (int i = 0; i < petStarterCards.Count; i++)
        {
            starterPetDeckProperty.arraySize++;
            starterPetDeckProperty.GetArrayElementAtIndex(starterPetDeckProperty.arraySize - 1).objectReferenceValue = petStarterCards[i];
        }
        
        serializedManager.ApplyModifiedProperties();
        
        Debug.Log($"Assigned {petStarterCards.Count} cards to pet starter deck.");
    }
    
    private void AssignAllCardsToPlayerStarterDeck()
    {
        SerializedObject serializedManager = new SerializedObject(gameManager);
        SerializedProperty starterDeckProperty = serializedManager.FindProperty("starterDeck");
        
        starterDeckProperty.ClearArray();
        
        for (int i = 0; i < allCards.Count; i++)
        {
            starterDeckProperty.arraySize++;
            starterDeckProperty.GetArrayElementAtIndex(starterDeckProperty.arraySize - 1).objectReferenceValue = allCards[i];
        }
        
        serializedManager.ApplyModifiedProperties();
        
        Debug.Log($"Added all {allCards.Count} cards to player starter deck for testing.");
    }
    
    private void AssignAllCardsToPetStarterDeck()
    {
        SerializedObject serializedManager = new SerializedObject(gameManager);
        SerializedProperty starterPetDeckProperty = serializedManager.FindProperty("starterPetDeck");
        
        starterPetDeckProperty.ClearArray();
        
        for (int i = 0; i < allCards.Count; i++)
        {
            starterPetDeckProperty.arraySize++;
            starterPetDeckProperty.GetArrayElementAtIndex(starterPetDeckProperty.arraySize - 1).objectReferenceValue = allCards[i];
        }
        
        serializedManager.ApplyModifiedProperties();
        
        Debug.Log($"Added all {allCards.Count} cards to pet starter deck for testing.");
    }
} 