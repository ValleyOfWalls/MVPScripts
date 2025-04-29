using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Combat
{
    // This class is just to demonstrate how to create cards in the editor and through code
    public class CardExamples : MonoBehaviour
    {
        #region Editor Methods
        // Use these methods from the context menu in the editor
        
        [ContextMenu("Create Basic Attack Card")]
        public void CreateBasicAttackCard()
        {
#if UNITY_EDITOR
            // Create a new CardData asset
            CardData attackCard = ScriptableObject.CreateInstance<CardData>();
            
            // Setup basic attack card
            attackCard.cardName = "Basic Attack";
            attackCard.description = "Deal 6 damage to a single enemy.";
            attackCard.manaCost = 1;
            attackCard.cardType = CardType.Attack;
            attackCard.baseValue = 6;
            
            // Create damage effect
            CardEffect damageEffect = new CardEffect
            {
                effectType = EffectType.Damage,
                effectValue = 6,
                targetType = TargetType.SingleEnemy
            };
            
            // Add the effect to the card
            attackCard.cardEffects = new CardEffect[] { damageEffect };
            
            // Save the asset
            string path = "Assets/MVPScripts/Cards/BasicAttack.asset";
            AssetDatabase.CreateAsset(attackCard, path);
            AssetDatabase.SaveAssets();
            
            // Select the created asset
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = attackCard;
            
            Debug.Log("Created Basic Attack card at: " + path);
#endif
        }
        
        [ContextMenu("Create Healing Card")]
        public void CreateHealingCard()
        {
#if UNITY_EDITOR
            // Create a new CardData asset
            CardData healCard = ScriptableObject.CreateInstance<CardData>();
            
            // Setup healing card
            healCard.cardName = "Healing Touch";
            healCard.description = "Heal yourself for 8 HP.";
            healCard.manaCost = 2;
            healCard.cardType = CardType.Skill;
            healCard.baseValue = 8;
            
            // Create heal effect
            CardEffect healEffect = new CardEffect
            {
                effectType = EffectType.Heal,
                effectValue = 8,
                targetType = TargetType.Self
            };
            
            // Add the effect to the card
            healCard.cardEffects = new CardEffect[] { healEffect };
            
            // Save the asset
            string path = "Assets/MVPScripts/Cards/HealingTouch.asset";
            AssetDatabase.CreateAsset(healCard, path);
            AssetDatabase.SaveAssets();
            
            // Select the created asset
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = healCard;
            
            Debug.Log("Created Healing Touch card at: " + path);
#endif
        }
        
        [ContextMenu("Create Multi-Effect Card")]
        public void CreateMultiEffectCard()
        {
#if UNITY_EDITOR
            // Create a new CardData asset
            CardData comboCard = ScriptableObject.CreateInstance<CardData>();
            
            // Setup combo card
            comboCard.cardName = "Tactical Strike";
            comboCard.description = "Deal 4 damage and draw 1 card.";
            comboCard.manaCost = 2;
            comboCard.cardType = CardType.Attack;
            comboCard.baseValue = 4;
            
            // Create effects
            CardEffect damageEffect = new CardEffect
            {
                effectType = EffectType.Damage,
                effectValue = 4,
                targetType = TargetType.SingleEnemy
            };
            
            CardEffect drawEffect = new CardEffect
            {
                effectType = EffectType.DrawCard,
                effectValue = 1,
                targetType = TargetType.Self
            };
            
            // Add the effects to the card
            comboCard.cardEffects = new CardEffect[] { damageEffect, drawEffect };
            
            // Save the asset
            string path = "Assets/MVPScripts/Cards/TacticalStrike.asset";
            AssetDatabase.CreateAsset(comboCard, path);
            AssetDatabase.SaveAssets();
            
            // Select the created asset
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = comboCard;
            
            Debug.Log("Created Tactical Strike card at: " + path);
#endif
        }
        
        [ContextMenu("Create Player Starter Deck")]
        public void CreatePlayerStarterDeck()
        {
#if UNITY_EDITOR
            // Create a new Deck asset
            Deck starterDeck = ScriptableObject.CreateInstance<Deck>();
            
            // Setup deck
            starterDeck.name = "Player Starter Deck";
            
            // Find card assets to add to the deck
            List<CardData> cardsToAdd = new List<CardData>();
            
            // You would typically add cards by referencing them directly
            // For demo purposes, we'll find them by name in the project
            string[] cardGuids = AssetDatabase.FindAssets("t:CardData", new[] { "Assets/MVPScripts/Cards" });
            foreach (string guid in cardGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
                if (card != null)
                {
                    cardsToAdd.Add(card);
                    
                    // Add multiple copies of basic cards
                    if (card.cardName == "Basic Attack")
                    {
                        cardsToAdd.Add(card); // Add a second copy
                        cardsToAdd.Add(card); // Add a third copy
                    }
                }
            }
            
            // Set the cards in the deck
            starterDeck.Cards.AddRange(cardsToAdd);
            
            // Save the asset
            string deckPath = "Assets/MVPScripts/Cards/PlayerStarterDeck.asset";
            AssetDatabase.CreateAsset(starterDeck, deckPath);
            AssetDatabase.SaveAssets();
            
            // Select the created asset
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = starterDeck;
            
            Debug.Log("Created Player Starter Deck at: " + deckPath);
#endif
        }
        #endregion
        
        #region Runtime Examples
        // These methods show how to create cards at runtime
        
        // Create a card programmatically at runtime
        public CardData CreateCardAtRuntime(string cardName, string description, int cost, 
                                        CardType type, int baseValue, EffectType effectType, 
                                        int effectValue, TargetType targetType)
        {
            // Create a new CardData instance
            CardData newCard = ScriptableObject.CreateInstance<CardData>();
            
            // Set basic properties
            newCard.cardName = cardName;
            newCard.description = description;
            newCard.manaCost = cost;
            newCard.cardType = type;
            newCard.baseValue = baseValue;
            
            // Create the effect
            CardEffect effect = new CardEffect
            {
                effectType = effectType,
                effectValue = effectValue,
                targetType = targetType
            };
            
            // Add the effect to the card
            newCard.cardEffects = new CardEffect[] { effect };
            
            return newCard;
        }
        
        // Example of creating an advanced card with multiple effects
        public CardData CreateAdvancedCard()
        {
            CardData advancedCard = ScriptableObject.CreateInstance<CardData>();
            
            advancedCard.cardName = "Vampiric Strike";
            advancedCard.description = "Deal 5 damage to an enemy and heal yourself for 3.";
            advancedCard.manaCost = 2;
            advancedCard.cardType = CardType.Attack;
            advancedCard.baseValue = 5;
            
            // Create multiple effects
            CardEffect[] effects = new CardEffect[2];
            
            // Damage effect
            effects[0] = new CardEffect
            {
                effectType = EffectType.Damage,
                effectValue = 5,
                targetType = TargetType.SingleEnemy
            };
            
            // Heal effect
            effects[1] = new CardEffect
            {
                effectType = EffectType.Heal,
                effectValue = 3,
                targetType = TargetType.Self
            };
            
            advancedCard.cardEffects = effects;
            
            return advancedCard;
        }
        
        // Example of using the card system in gameplay
        public void DemoUseCard()
        {
            // Get references
            DeckManager deckManager = DeckManager.Instance;
            CombatPlayer player = FindFirstObjectByType<CombatPlayer>();
            PlayerHand hand = FindFirstObjectByType<PlayerHand>();
            
            if (deckManager != null && player != null && hand != null)
            {
                // Create a card
                CardData vampiricStrike = CreateAdvancedCard();
                
                // Create a game object for the card and add to hand
                Transform handTransform = hand.transform;
                GameObject cardObj = deckManager.CreateCardObject(vampiricStrike, handTransform, hand, player);
                
                // Make this card playable
                Card cardComponent = cardObj.GetComponent<Card>();
                cardComponent.SetPlayable(true);
                
                Debug.Log("Created Vampiric Strike card and added to player's hand");
            }
            else
            {
                Debug.LogError("Missing required components for demo");
            }
        }
        #endregion
    }
} 