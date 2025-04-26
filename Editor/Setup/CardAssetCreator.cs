using UnityEngine;
using UnityEditor;
using System.IO;

public class CardAssetCreator
{
    // Define the target directory for card assets
    private const string CardAssetSavePath = "Assets/Scripts/Assets/Cards"; 

    [MenuItem("Tools/Setup/Create Starter Card Assets", priority = 100)] // New menu item
    public static void CreateStarterCardAssets()
    {
        Debug.Log($"Creating starter card assets in {CardAssetSavePath}...");
        
        // --- Define Cards Here --- 
        // Format: CreateOrReplaceCardAsset(name, cost, description, damage, block, ...other params...);
        CreateOrReplaceCardAsset("Strike", 1, "Deal 6 damage.", 6);
        CreateOrReplaceCardAsset("Defend", 1, "Gain 5 block.", 0, 5);
        // Add more cards as needed:
        // CreateOrReplaceCardAsset("Bash", 2, "Deal 8 damage. Apply 2 Vulnerable.", 8, 0, vulnerable: 2);
        // CreateOrReplaceCardAsset("Neutralize", 0, "Deal 3 damage. Apply 1 Weak.", 3, 0, weak: 1);
        
        Debug.Log("Finished creating starter card assets.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.FocusProjectWindow();
    }
    
    [MenuItem("Tools/Setup/Create Draft Test Card Assets", priority = 101)] // New menu item for draft cards
    public static void CreateDraftTestCardAssets()
    {
        Debug.Log($"Creating draft test card assets in {CardAssetSavePath}...");

        // --- Define Cards for Draft Pools Here --- 
        // Player Cards
        CreateOrReplaceCardAsset("Quick Slash", 1, "Deal 4 damage twice.", 4); // Example: Modify damage later if needed for multi-hit
        CreateOrReplaceCardAsset("Iron Skin", 1, "Gain 7 Block.", 0, 7);
        CreateOrReplaceCardAsset("Adrenaline Rush", 0, "Draw 2 cards. Gain 1 Energy.", 0, 0); // Needs Draw/Energy effect logic
        CreateOrReplaceCardAsset("Power Through", 1, "Gain 15 Block. Add 1 Wound to Draw Pile.", 0, 15); // Needs Wound logic

        // Pet Cards
        CreateOrReplaceCardAsset("Ferocious Bite", 2, "Deal 12 damage.", 12);
        CreateOrReplaceCardAsset("Protective Growl", 1, "Gain 4 Block. Apply 1 Weak to target.", 0, 4); // Needs Weak logic
        CreateOrReplaceCardAsset("Go Fetch", 0, "Draw 1 Pet Card.", 0, 0); // Needs Pet Draw logic
        CreateOrReplaceCardAsset("Thick Hide", 1, "Gain 8 Block.", 0, 8);

        Debug.Log("Finished creating draft test card assets.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.FocusProjectWindow();
    }
    
    // Modified helper to create/replace and populate fields
    private static void CreateOrReplaceCardAsset(string cardName, int cost, string description, int damage = 0, int block = 0 /* Add other params like weak/vulnerable here */)
    {
        // Ensure the save directory exists
        if (!Directory.Exists(CardAssetSavePath))
        {
            Directory.CreateDirectory(CardAssetSavePath);
            AssetDatabase.Refresh();
            Debug.Log($"Created directory: {CardAssetSavePath}");
        }

        string assetPath = Path.Combine(CardAssetSavePath, $"{cardName}.asset");
        bool replacing = false;

        // Check if asset already exists and delete if it does
        if (AssetDatabase.LoadAssetAtPath<CardData>(assetPath) != null)
        {
            if (AssetDatabase.DeleteAsset(assetPath))
            {
                 Debug.Log($"Replacing existing card asset: {assetPath}");
                 AssetDatabase.Refresh(); 
                 replacing = true;
            }
            else
            {
                Debug.LogError($"Failed to delete existing card asset at {assetPath}. Skipping creation.");
                return;
            }
           
        }

        // Create new instance and populate
        CardData cardData = ScriptableObject.CreateInstance<CardData>();
        cardData.cardName = cardName;
        cardData.cost = cost;
        cardData.description = description;
        cardData.damage = damage;
        // cardData.block = block; // Uncomment if/when block field is added to CardData.cs
        // Assign other parameters here...
        
        // Create the asset file
        AssetDatabase.CreateAsset(cardData, assetPath);
        
        if (!replacing)
        {
            Debug.Log($"Created new Card asset at: {assetPath}");
        }
        
        // No need to select individual assets here, let the caller handle focus/refresh
    }
} 