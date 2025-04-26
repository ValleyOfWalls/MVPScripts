using UnityEngine;
using UnityEditor;

public class CardGeneratorMenu : Editor
{
    [MenuItem("Game/Generate Card Assets")]
    public static void GenerateCards()
    {
        CardGenerator.GenerateCardAssets();
    }
    
    [MenuItem("Game/Test Cards/Generate Test Cards")]
    public static void GenerateTestCards()
    {
        CardGenerator.GenerateCardAssets();
    }
    
    [MenuItem("Game/Test Cards/Setup Player Decks/Damage Test")]
    public static void SetupPlayerDamageTestDeck()
    {
        CardGenerator.SetupPlayerDamageTestDeck();
    }
    
    [MenuItem("Game/Test Cards/Setup Player Decks/Block Test")]
    public static void SetupPlayerBlockTestDeck()
    {
        CardGenerator.SetupPlayerBlockTestDeck();
    }
    
    [MenuItem("Game/Test Cards/Setup Player Decks/Status Effects Test")]
    public static void SetupPlayerStatusTestDeck()
    {
        CardGenerator.SetupPlayerStatusEffectsTestDeck();
    }
    
    [MenuItem("Game/Test Cards/Setup Both Decks/Damage vs. Block Test")]
    public static void SetupDamageVsBlockTest()
    {
        CardGenerator.SetupDamageVsBlockTest();
    }
    
    [MenuItem("Game/Test Cards/Setup Both Decks/Block vs. Damage Test")]
    public static void SetupBlockVsDamageTest()
    {
        CardGenerator.SetupBlockVsDamageTest();
    }
    
    [MenuItem("Game/Test Cards/Setup Both Decks/Status vs. Healing Test")]
    public static void SetupStatusVsHealingTest()
    {
        CardGenerator.SetupStatusVsHealingTest();
    }
} 